"""POST /api/v1/water/estimate (契約の estimateWater、例は api/examples/water-estimate.*)。"""

from typing import Any

import boto3
import pytest
from botocore.exceptions import ReadTimeoutError
from fastapi.testclient import TestClient

from penguin_backend.app import create_app
from penguin_backend.aws import Clients, fixed
from penguin_backend.settings import Settings
from penguin_backend.water import table
from tests.support import photos
from tests.support.bedrock import (
    INVALID,
    MODEL_TIMEOUT,
    REGION,
    THROTTLED,
    UNAVAILABLE,
    StubbedBedrock,
    converse_text,
    converse_tool_use,
)
from tests.support.contract import assert_error, assert_matches_contract
from tests.support.privacy import assert_no_leak

PATH = "/api/v1/water/estimate"


def reading(
    container: str = "pet_bottle",
    *,
    label_capacity_ml: int = 500,
    fill_ratio: float = 0.6,
    confidence: float = 0.92,
    is_screen_photo: bool = False,
    **extra: Any,
) -> dict[str, Any]:
    return {
        "container": container,
        "label_capacity_ml": label_capacity_ml,
        "fill_ratio": fill_ratio,
        "confidence": confidence,
        "is_screen_photo": is_screen_photo,
        **extra,
    }


def answer(container: str = "pet_bottle", **kwargs: Any) -> dict[str, Any]:
    return converse_tool_use(reading(container, **kwargs), tool_name="estimate")


@pytest.fixture
def client(bedrock: StubbedBedrock) -> TestClient:
    # 水の道は DynamoDB を使わない (呼べば偽の認証情報で失敗する)
    clients = Clients(bedrock=bedrock.client, dynamodb=boto3.client("dynamodb", region_name=REGION))
    return TestClient(create_app(Settings(), clients=fixed(clients)), raise_server_exceptions=False)


def post(client: TestClient, data: bytes) -> Any:
    return client.post(PATH, files=photos.multipart(data))


@pytest.mark.usefixtures("no_disk_writes")
def test_bottle_photo_is_accepted_with_the_capacity_read_from_the_label(
    client: TestClient, bedrock: StubbedBedrock, caplog: pytest.LogCaptureFixture
) -> None:
    bedrock.respond(answer(label_capacity_ml=600, fill_ratio=0.5))

    response = post(client, photos.jpeg(marker="leak-water-1"))

    assert_matches_contract(response)
    body = response.json()
    assert body["decision"] == "accept"
    assert (body["container"], body["capacity_ml"], body["amount_ml"]) == ("pet_bottle", 600, 300)
    assert [check["passed"] for check in body["checks"]] == [True, True, True]
    assert body["checks"][1]["detail"] == "600ml と書いてある"
    assert_no_leak("leak-water-1", response, caplog)


def test_amount_is_rounded_from_the_capacity(client: TestClient, bedrock: StubbedBedrock) -> None:
    bedrock.respond(answer(label_capacity_ml=350, fill_ratio=0.333))

    assert post(client, photos.jpeg()).json()["amount_ml"] == round(350 * 0.333)


def test_litre_bottles_are_described_in_litres(client: TestClient, bedrock: StubbedBedrock) -> None:
    bedrock.respond(answer(label_capacity_ml=2000, fill_ratio=0.0))

    body = post(client, photos.jpeg()).json()

    assert (body["decision"], body["capacity_ml"], body["amount_ml"]) == ("accept", 2000, 0)
    assert body["checks"][1]["detail"] == "2L と書いてある"


@pytest.mark.parametrize("capacity", [0, table.CAPACITY_MIN_ML - 1, table.CAPACITY_MAX_ML + 1, -5])
def test_unreadable_capacity_asks_to_show_the_label(
    client: TestClient, bedrock: StubbedBedrock, capacity: int
) -> None:
    # 容量を表から決めない: ラベルが読めなければ、形が 500ml に見えても量は出さない
    bedrock.respond(answer(label_capacity_ml=capacity, fill_ratio=0.1, confidence=0.9))

    response = post(client, photos.jpeg())

    assert_matches_contract(response)
    body = response.json()
    assert (body["decision"], body["capacity_ml"], body["amount_ml"]) == ("retake", 0, 0)
    assert body["container"] == "pet_bottle"
    assert body["retake_hint"] == table.RETAKE_HINT_CAPACITY
    assert body["checks"][1] == {
        "id": "capacity",
        "label": "ラベルの容量",
        "passed": False,
        "detail": "読めなかった",
    }


def test_unknown_container_asks_for_a_retake_with_no_water(
    client: TestClient, bedrock: StubbedBedrock
) -> None:
    bedrock.respond(answer("unknown", label_capacity_ml=500, fill_ratio=0.8, confidence=0.9))

    response = post(client, photos.jpeg())

    assert_matches_contract(response)
    body = response.json()
    assert (body["decision"], body["amount_ml"], body["fill_ratio"], body["capacity_ml"]) == (
        "retake",
        0,
        0,
        0,
    )
    assert body["retake_hint"] == table.RETAKE_HINT


@pytest.mark.parametrize("container", ["mug", "pet_bottle_500"])
def test_container_outside_the_choices_is_unknown(
    client: TestClient, bedrock: StubbedBedrock, container: str
) -> None:
    # 1.2.0 の容量付きの値を AI が答えても、容量としては使わない
    bedrock.respond(answer(container))

    body = post(client, photos.jpeg()).json()

    assert (body["container"], body["decision"]) == ("unknown", "retake")


def test_low_confidence_asks_for_a_retake(client: TestClient, bedrock: StubbedBedrock) -> None:
    bedrock.respond(answer(confidence=table.CONFIDENCE_FLOOR - 0.01))

    response = post(client, photos.jpeg())

    assert_matches_contract(response)
    assert response.json()["decision"] == "retake"


def test_photo_of_a_screen_is_rejected(client: TestClient, bedrock: StubbedBedrock) -> None:
    bedrock.respond(answer(is_screen_photo=True))

    response = post(client, photos.jpeg())

    assert_matches_contract(response)
    body = response.json()
    assert body["decision"] == "reject"
    assert body["checks"][2] == {
        "id": "screen",
        "label": "画面を撮った写真ではない",
        "passed": False,
    }


def test_out_of_range_numbers_are_kept_within_the_contract(
    client: TestClient, bedrock: StubbedBedrock
) -> None:
    bedrock.respond(answer(fill_ratio=1.4, confidence=-0.2))

    response = post(client, photos.jpeg())

    assert_matches_contract(response)
    assert response.json()["fill_ratio"] == 1
    assert response.json()["confidence"] == 0


@pytest.mark.parametrize(
    "broken",
    [
        converse_text("pet_bottle です"),
        converse_tool_use({"container": "pet_bottle"}, tool_name="estimate"),
        converse_tool_use(reading(), tool_name="judge"),
    ],
    ids=["text", "missing-fields", "other-tool"],
)
def test_answers_out_of_shape_are_upstream_unavailable(
    client: TestClient, bedrock: StubbedBedrock, broken: dict[str, Any]
) -> None:
    bedrock.respond(broken)

    assert_error(post(client, photos.jpeg()), "upstream_unavailable")


@pytest.mark.parametrize("failure", [THROTTLED, MODEL_TIMEOUT, UNAVAILABLE])
def test_model_failures_are_upstream_unavailable(
    client: TestClient, bedrock: StubbedBedrock, failure: tuple[str, int]
) -> None:
    bedrock.fail(failure)

    assert_error(post(client, photos.jpeg()), "upstream_unavailable")


def test_rejected_request_is_internal(client: TestClient, bedrock: StubbedBedrock) -> None:
    bedrock.fail(INVALID)

    assert_error(post(client, photos.jpeg()), "internal")


def test_read_timeout_is_upstream_unavailable(
    client: TestClient, bedrock: StubbedBedrock, monkeypatch: pytest.MonkeyPatch
) -> None:
    def timeout(**_kwargs: Any) -> Any:
        raise ReadTimeoutError(endpoint_url="https://bedrock-runtime")

    monkeypatch.setattr(bedrock.client, "converse", timeout)

    assert_error(post(client, photos.jpeg()), "upstream_unavailable")


@pytest.mark.usefixtures("no_disk_writes")
def test_too_large_photo_is_refused_without_spooling(client: TestClient) -> None:
    assert_error(post(client, photos.too_large()), "image_too_large")


def test_body_far_over_the_limit_is_refused(client: TestClient) -> None:
    big = photos.jpeg() + b"\x00" * (2 * photos.MAX_BYTES)

    assert_error(post(client, big), "image_too_large")


def test_png_is_unsupported(client: TestClient) -> None:
    response = post(client, photos.png())

    assert response.status_code == 415
    assert_error(response, "image_invalid")


def test_truncated_jpeg_is_invalid(client: TestClient) -> None:
    response = post(client, photos.broken_jpeg())

    assert response.status_code == 400
    assert_error(response, "image_invalid")


def test_missing_image_is_reported(client: TestClient) -> None:
    assert_error(
        client.post(PATH, files={"other": ("a.jpg", photos.jpeg(), "image/jpeg")}), "image_missing"
    )


def test_non_multipart_body_is_missing_the_image(client: TestClient) -> None:
    assert_error(client.post(PATH, json={"image": "x"}), "image_missing")
