"""AWS の偽物と合成の写真 (tests/support/) が、本物と同じ前提で動くこと。

ここが通っていれば、当日の機能のテストは「偽物の振る舞い」を信じて書ける。
"""

import struct

import pytest
from botocore.exceptions import ClientError
from mypy_boto3_dynamodb import DynamoDBClient

from tests.support import bedrock as fake_bedrock
from tests.support import photos
from tests.support.dynamodb import table_specs


def _jpeg_segments(data: bytes) -> list[int]:
    """JPEG のマーカー (FF xx) を、画像の本体 (SOS) の手前まで並べる。"""
    assert data[:2] == b"\xff\xd8"
    markers, i = [], 2
    while i < len(data):
        assert data[i] == 0xFF
        marker = data[i + 1]
        markers.append(marker)
        if marker == 0xDA:  # SOS: ここから先は画像の本体
            break
        (length,) = struct.unpack(">H", data[i + 2 : i + 4])
        i += 2 + length
    return markers


def test_synthetic_jpeg_is_a_well_formed_jpeg() -> None:
    data = photos.jpeg()

    assert data.endswith(b"\xff\xd9")
    markers = _jpeg_segments(data)
    assert 0xC0 in markers  # 大きさの定義 (SOF0)
    assert markers[-1] == 0xDA


def test_marker_is_embedded_as_a_jpeg_comment() -> None:
    data = photos.jpeg(marker="leak-check-123")

    assert b"leak-check-123" in data
    assert _jpeg_segments(data)[0] == 0xFE  # COM


def test_too_large_is_just_over_the_contract_limit() -> None:
    assert len(photos.too_large()) == photos.MAX_BYTES + 1


def test_non_jpegs_do_not_look_like_jpegs() -> None:
    assert not photos.png().startswith(b"\xff\xd8")
    assert not photos.broken_jpeg().endswith(b"\xff\xd9")


def test_tables_follow_the_iac_template(
    community_tables: dict[str, str], dynamodb: DynamoDBClient
) -> None:
    assert community_tables == {
        "CommunityTable": "penguin-backend-community",
        "CommunityDevTable": "penguin-backend-community-dev",
        "EncounterTable": "penguin-backend-encounters",
        "EncounterDevTable": "penguin-backend-encounters-dev",
    }
    for logical_id, name in community_tables.items():
        spec = table_specs()[logical_id]
        table = dynamodb.describe_table(TableName=name)["Table"]
        assert table["KeySchema"] == spec.key_schema
        ttl = dynamodb.describe_time_to_live(TableName=name)["TimeToLiveDescription"]
        assert ttl.get("AttributeName") == spec.ttl_attribute == "ttl"


def test_conditional_put_lets_a_submission_through_only_once(
    community_tables: dict[str, str], dynamodb: DynamoDBClient
) -> None:
    """ADR 0002 の数え方の前提: 同じ提出 ID の 2 回目は ConditionalCheckFailed になる。"""
    table = community_tables["CommunityTable"]
    item = {"pk": {"S": "sub#goal-1#abc"}}

    dynamodb.put_item(TableName=table, Item=item, ConditionExpression="attribute_not_exists(pk)")
    with pytest.raises(ClientError) as raised:
        dynamodb.put_item(
            TableName=table, Item=item, ConditionExpression="attribute_not_exists(pk)"
        )

    assert raised.value.response["Error"]["Code"] == "ConditionalCheckFailedException"


def test_add_counts_up_from_nothing(
    community_tables: dict[str, str], dynamodb: DynamoDBClient
) -> None:
    table = community_tables["CommunityTable"]
    key = {"pk": {"S": "goal#goal-1#s3"}}

    for _ in range(3):
        dynamodb.update_item(
            TableName=table,
            Key=key,
            UpdateExpression="ADD #c :one",
            ExpressionAttributeNames={"#c": "current"},
            ExpressionAttributeValues={":one": {"N": "1"}},
        )

    assert dynamodb.get_item(TableName=table, Key=key)["Item"]["current"] == {"N": "3"}


def test_stubbed_bedrock_returns_a_real_converse_shape(
    bedrock: fake_bedrock.StubbedBedrock,
) -> None:
    bedrock.respond(fake_bedrock.converse_tool_use(fake_bedrock.judge_input("bicycle")))

    response = bedrock.client.converse(
        modelId=fake_bedrock.NOVA_MODEL_ID,
        messages=[{"role": "user", "content": [{"text": "判定して"}]}],
        inferenceConfig={"maxTokens": 400},
    )

    block = response["output"]["message"]["content"][0]
    assert block["toolUse"]["input"]["subject"] == "bicycle"
    assert response["stopReason"] == "tool_use"
    assert response["usage"]["totalTokens"] == 1509


@pytest.mark.parametrize(
    "error",
    [fake_bedrock.THROTTLED, fake_bedrock.MODEL_TIMEOUT, fake_bedrock.UNAVAILABLE],
)
def test_stubbed_bedrock_raises_the_errors_bedrock_raises(
    bedrock: fake_bedrock.StubbedBedrock, error: tuple[str, int]
) -> None:
    bedrock.fail(error)

    with pytest.raises(ClientError) as raised:
        bedrock.client.converse(
            modelId=fake_bedrock.NOVA_MODEL_ID,
            messages=[{"role": "user", "content": [{"text": "判定して"}]}],
        )

    assert raised.value.response["Error"]["Code"] == error[0]
    assert raised.value.response["ResponseMetadata"]["HTTPStatusCode"] == error[1]
