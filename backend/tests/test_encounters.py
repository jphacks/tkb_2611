"""POST /api/v1/encounters (契約の reportEncounter、例は api/examples/encounter.*)。"""

from collections.abc import Callable
from datetime import UTC, datetime, timedelta
from typing import Any

import pytest
from botocore.exceptions import ClientError
from fastapi.testclient import TestClient
from mypy_boto3_dynamodb import DynamoDBClient

from penguin_backend.app import create_app
from penguin_backend.aws import Clients, fixed
from penguin_backend.encounters import JST, app_day, pair_id
from penguin_backend.settings import Settings
from tests.support import bedrock as bedrock_support
from tests.support.contract import assert_error, assert_matches_contract

PATH = "/api/v1/encounters"
# 契約の EncounterResult.pair_id の例 (0x01 と 0x02 が 16 バイトずつ)
A = "AQEBAQEBAQEBAQEBAQEBAQ"
B = "AgICAgICAgICAgICAgICAg"
C = "AwMDAwMDAwMDAwMDAwMDAw"
# 2026-10-24 12:00 JST (アプリの 1 日は 2026-10-24)
NOON = datetime(2026, 10, 24, 12, 0, tzinfo=JST)
DAY = "2026-10-24"


class Clock:
    def __init__(self, now: datetime) -> None:
        self.now = now

    def __call__(self) -> datetime:
        return self.now


@pytest.fixture
def clock() -> Clock:
    return Clock(NOON)


@pytest.fixture
def make(
    dynamodb: DynamoDBClient, community_tables: dict[str, str], clock: Clock
) -> Callable[..., TestClient]:
    def _make(**settings: Any) -> TestClient:
        clients = Clients(bedrock=bedrock_support.stubbed_bedrock().client, dynamodb=dynamodb)
        app = create_app(Settings(**settings), clients=fixed(clients), clock=clock)
        return TestClient(app, raise_server_exceptions=False)

    return _make


@pytest.fixture
def client(make: Callable[..., TestClient]) -> TestClient:
    return make()


def report(client: TestClient, me: str, peer: str, day: str = DAY) -> Any:
    return client.post(PATH, json={"day": day, "self_token": me, "peer_token": peer})


def test_pair_id_matches_the_contract_example() -> None:
    assert pair_id(A, B) == pair_id(B, A) == "e5ecc329c8fcc058"


@pytest.mark.parametrize(
    ("now", "day"),
    [
        (datetime(2026, 10, 25, 2, 59, tzinfo=JST), "2026-10-24"),
        (datetime(2026, 10, 25, 3, 0, tzinfo=JST), "2026-10-25"),
        (datetime(2026, 10, 24, 17, 59, tzinfo=UTC), "2026-10-24"),  # 02:59 JST
    ],
)
def test_app_day_starts_at_three_in_japan(now: datetime, day: str) -> None:
    assert app_day(now).isoformat() == day


def test_first_report_waits_for_the_peer(client: TestClient) -> None:
    response = report(client, A, B)

    assert_matches_contract(response)
    assert response.json() == {
        "schema_version": "1.2.0",
        "matched": False,
        "pair_id": "e5ecc329c8fcc058",
    }


def test_both_reports_make_a_match(client: TestClient) -> None:
    report(client, A, B)

    response = report(client, B, A)

    assert_matches_contract(response)
    assert response.json()["matched"] is True


def test_the_first_reporter_sees_the_match_when_resending(client: TestClient) -> None:
    report(client, A, B)
    report(client, B, A)

    assert report(client, A, B).json()["matched"] is True


def test_resending_the_same_report_does_not_match(client: TestClient) -> None:
    report(client, A, B)

    assert report(client, A, B).json()["matched"] is False


def test_reports_on_other_days_do_not_match(client: TestClient) -> None:
    report(client, A, B, day="2026-10-23")

    assert report(client, B, A, day="2026-10-24").json()["matched"] is False


def test_records_expire_after_two_days(
    client: TestClient, dynamodb: DynamoDBClient, community_tables: dict[str, str]
) -> None:
    report(client, A, B)

    items = dynamodb.scan(TableName=community_tables["CommunityDevTable"])["Items"]

    expected = int((NOON + timedelta(days=2)).timestamp())
    assert {item["pk"]["S"].split("#")[0] for item in items} == {"enc", "encrate"}
    assert {int(item["ttl"]["N"]) for item in items} == {expected}
    # トークン以外の、端末や位置に結び付く値を残さない
    assert {name for item in items for name in item} <= {"pk", "seen", "n", "ttl"}


def test_reports_over_the_daily_limit_are_rate_limited(
    make: Callable[..., TestClient],
) -> None:
    client = make(encounter_daily_limit=1)
    report(client, A, B)

    response = report(client, A, C)

    assert_error(response, "rate_limited")
    # 次のアプリの 1 日 (2026-10-25 03:00 JST) まで
    assert int(response.headers["retry-after"]) == 15 * 3600


def test_resending_does_not_use_up_the_limit(make: Callable[..., TestClient]) -> None:
    client = make(encounter_daily_limit=1)
    report(client, A, B)

    assert report(client, A, B).status_code == 200
    assert report(client, B, A).json()["matched"] is True


def test_dedicated_table_can_be_configured(
    make: Callable[..., TestClient], dynamodb: DynamoDBClient, community_tables: dict[str, str]
) -> None:
    client = make(encounter_table=community_tables["CommunityTable"])

    report(client, A, B)

    assert dynamodb.scan(TableName=community_tables["CommunityTable"])["Count"] == 2
    assert dynamodb.scan(TableName=community_tables["CommunityDevTable"])["Count"] == 0


def test_reporting_yourself_is_invalid(client: TestClient) -> None:
    assert_error(report(client, A, A), "token_invalid")


@pytest.mark.parametrize(
    "token",
    ["short", A + "A", "AQEBAQEBAQEBAQEBAQEBA=", "AQEBAQEBAQEBAQEBAQEB+/"],
    ids=["short", "long", "padding", "not-url-safe"],
)
def test_malformed_tokens_are_invalid(client: TestClient, token: str) -> None:
    assert_error(report(client, A, token), "token_invalid")


@pytest.mark.parametrize(
    "body",
    [
        b"not json",
        b"[]",
        b'{"day": "2026-10-24", "self_token": "AQEBAQEBAQEBAQEBAQEBAQ"}',
        b'{"day": "2026-10-24", "self_token": "AQEBAQEBAQEBAQEBAQEBAQ",'
        b' "peer_token": "AgICAgICAgICAgICAgICAg", "lat": 35.0}',
        b"{" + b" " * 2048 + b"}",
    ],
    ids=["not-json", "array", "missing", "extra-field", "too-long"],
)
def test_unreadable_bodies_are_invalid_not_422(client: TestClient, body: bytes) -> None:
    response = client.post(PATH, content=body, headers={"Content-Type": "application/json"})

    assert_error(response, "token_invalid")


@pytest.mark.parametrize(
    "day", ["2026-10-22", "2026-10-26", "2026-13-01", "20261024", "2026-10-24T00:00:00"]
)
def test_days_away_from_the_server_are_invalid(client: TestClient, day: str) -> None:
    assert_error(report(client, A, B, day=day), "day_invalid")


@pytest.mark.parametrize("day", ["2026-10-23", "2026-10-25"])
def test_neighbouring_days_are_accepted(client: TestClient, day: str) -> None:
    assert report(client, A, B, day=day).status_code == 200


def test_store_unavailable_is_reported(
    client: TestClient, dynamodb: DynamoDBClient, monkeypatch: pytest.MonkeyPatch
) -> None:
    def throttled(**_kwargs: Any) -> Any:
        raise ClientError({"Error": {"Code": "ThrottlingException"}}, "GetItem")

    monkeypatch.setattr(dynamodb, "get_item", throttled)

    response = report(client, A, B)

    assert_error(response, "upstream_unavailable")
    assert "写真" not in response.json()["error"]["message"]


def test_tokens_are_not_logged(client: TestClient, caplog: pytest.LogCaptureFixture) -> None:
    report(client, A, B)
    report(client, B, A)

    assert A not in caplog.text
    assert B not in caplog.text
