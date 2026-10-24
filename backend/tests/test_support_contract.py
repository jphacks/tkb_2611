"""契約の検証の道具 (tests/support/contract.py) が、正しいものを通し、壊れたものを落とすこと。"""

from typing import Any

import pytest
from fastapi import FastAPI
from fastapi.responses import JSONResponse
from fastapi.testclient import TestClient

from tests.support.contract import (
    assert_error,
    assert_matches_contract,
    error_table,
    example,
    operations,
    validate,
)


def _answering(
    path: str, status: int, body: Any, headers: dict[str, str] | None = None
) -> TestClient:
    app = FastAPI()

    @app.get(path)
    def answer() -> JSONResponse:
        return JSONResponse(body, status_code=status, headers=headers)

    return TestClient(app)


@pytest.mark.parametrize(
    ("schema", "name"),
    [
        ("JudgeResult", "judge.200.accept"),
        ("JudgeResult", "judge.200.unknown"),
        ("CommunityGoal", "community-goal.200.active"),
        ("ErrorResponse", "error.503.upstream_unavailable"),
    ],
)
def test_contract_examples_pass_their_schema(schema: str, name: str) -> None:
    assert validate(schema, example(name)) == []


def test_a_broken_judge_result_is_reported() -> None:
    broken = {**example("judge.200.accept"), "confidence": 1.5, "subject": "dragon"}

    problems = validate("JudgeResult", broken)

    assert any("confidence" in p for p in problems)
    assert any("subject" in p for p in problems)


def test_operations_list_every_path_of_the_contract() -> None:
    found = set(operations())

    assert ("POST", "/v1/eco-photo/judge") in found
    assert ("GET", "/v1/community/goal") in found
    assert ("HEAD", "/v1/health") in found


def test_matching_response_passes() -> None:
    client = _answering(
        "/api/v1/health", 200, example("health.200.ok"), {"Cache-Control": "no-store"}
    )

    assert_matches_contract(client.get("/api/v1/health"))


def test_missing_no_store_is_reported() -> None:
    client = _answering("/api/v1/health", 200, example("health.200.ok"))

    with pytest.raises(AssertionError, match="no-store"):
        assert_matches_contract(client.get("/api/v1/health"))


def test_community_goal_is_shared_through_the_cdn() -> None:
    """ゲージの口だけは全員に同じ値なので、契約で CDN に 10 秒置く (CacheControlShared)。"""
    client = _answering(
        "/api/v1/community/goal",
        200,
        example("community-goal.200.active"),
        {"Cache-Control": "public, s-maxage=10"},
    )

    assert_matches_contract(client.get("/api/v1/community/goal"))


def test_community_goal_with_no_store_is_reported() -> None:
    client = _answering(
        "/api/v1/community/goal",
        200,
        example("community-goal.200.active"),
        {"Cache-Control": "no-store"},
    )

    with pytest.raises(AssertionError, match="s-maxage=10"):
        assert_matches_contract(client.get("/api/v1/community/goal"))


def test_status_not_in_the_contract_is_reported() -> None:
    client = _answering("/api/v1/community/goal", 418, {"tea": True}, {"Cache-Control": "no-store"})

    with pytest.raises(AssertionError, match="418"):
        assert_matches_contract(client.get("/api/v1/community/goal"))


def test_path_not_in_the_contract_is_reported() -> None:
    client = _answering("/api/v1/secret", 200, {}, {"Cache-Control": "no-store"})

    with pytest.raises(AssertionError, match="契約に無い道"):
        assert_matches_contract(client.get("/api/v1/secret"))


def test_error_table_comes_from_the_contract_checker() -> None:
    statuses, retryable = error_table()["upstream_unavailable"]

    assert statuses == {503}
    assert retryable is True


def test_error_with_retry_after_passes() -> None:
    client = _answering(
        "/api/v1/community/goal",
        429,
        example("error.429.rate_limited"),
        {"Retry-After": "10", "Cache-Control": "no-store"},
    )

    assert_error(client.get("/api/v1/community/goal"), "rate_limited")


def test_error_without_retry_after_is_reported() -> None:
    client = _answering(
        "/api/v1/community/goal",
        429,
        example("error.429.rate_limited"),
        {"Cache-Control": "no-store"},
    )

    with pytest.raises(AssertionError, match="Retry-After"):
        assert_error(client.get("/api/v1/community/goal"), "rate_limited")


def test_error_with_the_wrong_status_is_reported() -> None:
    client = _answering("/api/v1/community/goal", 500, example("error.503.upstream_unavailable"))

    with pytest.raises(AssertionError, match="upstream_unavailable"):
        assert_error(client.get("/api/v1/community/goal"), "upstream_unavailable")
