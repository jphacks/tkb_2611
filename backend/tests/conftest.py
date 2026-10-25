"""テスト全体のフィクスチャ。道具の中身は tests/support/、使い方は backend/README.md にある。

- 既定では、どのテストも偽の AWS の認証情報で動く (aws_env)。
  手元の AWS_PROFILE があっても本物に届かない
- 本物の AWS に当てるテストは印 aws を付け、PENGUIN_TEST_AWS=1 のときだけ動く
"""

from __future__ import annotations

import os
from collections.abc import Callable, Iterator

import boto3
import pytest
from fastapi.testclient import TestClient
from moto import mock_aws
from mypy_boto3_dynamodb import DynamoDBClient

from penguin_backend.app import create_app
from penguin_backend.settings import Settings
from tests.support import bedrock as bedrock_support
from tests.support.dynamodb import create_tables
from tests.support.privacy import block_disk_writes

REGION = "ap-northeast-1"
_LIVE_AWS = os.environ.get("PENGUIN_TEST_AWS") == "1"


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    if _LIVE_AWS:
        return
    skip = pytest.mark.skip(reason="本物の AWS に当てるテスト。PENGUIN_TEST_AWS=1 で動かす")
    for item in items:
        if "aws" in item.keywords:
            item.add_marker(skip)


@pytest.fixture(autouse=True)
def aws_env(request: pytest.FixtureRequest, monkeypatch: pytest.MonkeyPatch) -> None:
    """偽の認証情報とリージョン。印 aws のテストだけは手元の認証情報 (AWS_PROFILE) を使う。"""
    if request.node.get_closest_marker("aws"):
        return
    for name in ("AWS_PROFILE", "AWS_DEFAULT_PROFILE", "AWS_SESSION_TOKEN", "AWS_SECURITY_TOKEN"):
        monkeypatch.delenv(name, raising=False)
    monkeypatch.setenv("AWS_ACCESS_KEY_ID", "testing")
    monkeypatch.setenv("AWS_SECRET_ACCESS_KEY", "testing")
    monkeypatch.setenv("AWS_DEFAULT_REGION", REGION)
    monkeypatch.setenv("AWS_REGION", REGION)
    # ~/.aws/config の設定 (プロファイルの既定値など) を読ませない
    monkeypatch.setenv("AWS_CONFIG_FILE", os.devnull)
    monkeypatch.setenv("AWS_SHARED_CREDENTIALS_FILE", os.devnull)


@pytest.fixture
def make_client() -> Callable[..., TestClient]:
    """設定を変えたアプリの TestClient を作る。処理されない例外は 500 の応答として見る。"""

    def _make(settings: Settings | None = None) -> TestClient:
        return TestClient(create_app(settings or Settings()), raise_server_exceptions=False)

    return _make


@pytest.fixture
def client(make_client: Callable[..., TestClient]) -> TestClient:
    return make_client()


@pytest.fixture
def dynamodb() -> Iterator[DynamoDBClient]:
    """moto の DynamoDB のクライアント (テーブルはまだ無い)。"""
    with mock_aws():
        yield boto3.client("dynamodb", region_name=REGION)


@pytest.fixture
def community_tables(dynamodb: DynamoDBClient) -> dict[str, str]:
    """infra/backend.yaml と同じ形のテーブルを moto に作る。論理 ID → テーブル名。

    例: {"CommunityTable": "penguin-backend-community",
         "CommunityDevTable": "penguin-backend-community-dev",
         "EncounterTable": "penguin-backend-encounters", ...}
    """
    return create_tables(dynamodb)


@pytest.fixture
def bedrock() -> Iterator[bedrock_support.StubbedBedrock]:
    """Stubber 付きの bedrock-runtime。テストの最後に、積んだ応答がすべて使われたかを確かめる。"""
    stubbed = bedrock_support.stubbed_bedrock()
    with stubbed.stubber:
        yield stubbed
        stubbed.stubber.assert_no_pending_responses()


@pytest.fixture
def no_disk_writes(monkeypatch: pytest.MonkeyPatch) -> Iterator[None]:
    """このテストの間、ファイルの書き込みと一時ファイルの作成を失敗させる。

    写真をディスクに残さないことを確かめる。書こうとした記録が残っていれば、最後に失敗させる。
    """
    with block_disk_writes(monkeypatch) as attempts:
        yield
    assert not attempts, f"写真の処理中にディスクへ書こうとした: {attempts}"
