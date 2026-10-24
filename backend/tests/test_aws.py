"""AWS のクライアントの用意 (Vercel の OIDC でロールを借りる)。STS は moto の偽物。"""

from collections.abc import Iterator
from datetime import UTC, datetime

import boto3
import pytest
from fastapi.testclient import TestClient
from moto import mock_aws
from mypy_boto3_sts import STSClient
from starlette.requests import Request

from penguin_backend.app import create_app
from penguin_backend.aws import OIDC_HEADER, AwsClients
from penguin_backend.settings import Settings
from tests.support.contract import assert_error

ROLE = "arn:aws:iam::123456789012:role/penguin-backend-vercel"
TOKEN = "header.payload.signature"  # noqa: S105 (偽物の値)


def request(headers: dict[str, str] | None = None) -> Request:
    raw = [(name.encode(), value.encode()) for name, value in (headers or {}).items()]
    return Request({"type": "http", "method": "POST", "path": "/", "headers": raw})


class CountingSts:
    """moto の STS を呼び、AssumeRoleWithWebIdentity の回数を数える。"""

    def __init__(self, sts: STSClient) -> None:
        self.sts = sts
        self.calls = 0

    def assume_role_with_web_identity(self, **kwargs: object) -> object:
        self.calls += 1
        return self.sts.assume_role_with_web_identity(**kwargs)  # type: ignore[arg-type]


@pytest.fixture
def sts() -> Iterator[CountingSts]:
    with mock_aws():
        yield CountingSts(boto3.client("sts", region_name="ap-northeast-1"))


def test_role_is_assumed_with_the_vercel_token_and_reused(sts: CountingSts) -> None:
    now = [1_000.0]
    provider = AwsClients(
        Settings(aws_role_arn=ROLE),
        sts=sts,  # type: ignore[arg-type]
        clock=lambda: now[0],
    )

    first = provider(request({OIDC_HEADER: TOKEN}))
    second = provider(request({OIDC_HEADER: TOKEN}))

    assert first is second
    assert sts.calls == 1
    assert first.bedrock.meta.region_name == "ap-northeast-1"


def test_credentials_are_assumed_again_before_they_expire(sts: CountingSts) -> None:
    now = [1_000.0]
    provider = AwsClients(
        Settings(aws_role_arn=ROLE),
        sts=sts,  # type: ignore[arg-type]
        clock=lambda: now[0],
    )
    provider(request({OIDC_HEADER: TOKEN}))

    now[0] = float("inf")
    provider(request({OIDC_HEADER: TOKEN}))

    assert sts.calls == 2


def test_without_a_role_the_default_credentials_are_used(sts: CountingSts) -> None:
    provider = AwsClients(Settings(), sts=sts)  # type: ignore[arg-type]

    clients = provider(request())

    assert sts.calls == 0
    assert clients.dynamodb.meta.region_name == "ap-northeast-1"


def test_missing_token_with_a_role_is_an_internal_error() -> None:
    # Vercel の関数なら必ず付くヘッダーが無い = 設定の誤り。契約の 500 internal で答える
    noon = datetime(2026, 10, 24, 3, 0, tzinfo=UTC)
    app = create_app(Settings(aws_role_arn=ROLE), clock=lambda: noon)
    client = TestClient(app, raise_server_exceptions=False)

    response = client.post(
        "/api/v1/encounters",
        json={
            "day": "2026-10-24",
            "self_token": "AQEBAQEBAQEBAQEBAQEBAQ",
            "peer_token": "AgICAgICAgICAgICAgICAg",
        },
    )

    assert_error(response, "internal")
