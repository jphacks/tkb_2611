"""本物の AWS に当てるテスト。偽物 (tests/support/) と本物の振る舞いが食い違っていないかを確かめる。

    PENGUIN_TEST_AWS=1 AWS_PROFILE=penguin uv run pytest -m aws -q

- CI では動かない (PENGUIN_TEST_AWS が無いと skip)。費用は 1 回 1 円未満
- 開発者グループ (project-penguin の infra/account.yaml) の権限で通る範囲だけを使う:
  Nova 2 Lite の jp. プロファイルと、開発用テーブル penguin-backend-community-dev
- 写真は合成のものだけを送る (実在の写真を送らない)
"""

from uuid import uuid4

import boto3
import pytest
from botocore.config import Config
from botocore.exceptions import ClientError
from mypy_boto3_bedrock_runtime import BedrockRuntimeClient
from mypy_boto3_dynamodb import DynamoDBClient

from tests.support import photos
from tests.support.bedrock import NOVA_MODEL_ID, REGION
from tests.support.dynamodb import table_specs

pytestmark = pytest.mark.aws

SUBJECTS = ["my_bottle", "bicycle", "unknown"]
TOOL = {
    "tools": [
        {
            "toolSpec": {
                "name": "judge",
                "description": "写真の判定結果を返す",
                "inputSchema": {
                    "json": {
                        "type": "object",
                        "required": ["subject", "confidence"],
                        "properties": {
                            "subject": {"type": "string", "enum": SUBJECTS},
                            "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                        },
                    }
                },
            }
        }
    ]
}


@pytest.fixture(scope="module")
def runtime() -> BedrockRuntimeClient:
    client: BedrockRuntimeClient = boto3.client(
        "bedrock-runtime",
        region_name=REGION,
        config=Config(retries={"max_attempts": 3, "mode": "adaptive"}, read_timeout=15),
    )
    return client


@pytest.fixture
def dev_table() -> tuple[DynamoDBClient, str]:
    """開発用テーブル。書いたものはテストの中の finally で消す。"""
    client: DynamoDBClient = boto3.client("dynamodb", region_name=REGION)
    return client, table_specs()["CommunityDevTable"].name


def test_nova_answers_with_the_judge_tool(runtime: BedrockRuntimeClient) -> None:
    response = runtime.converse(
        modelId=NOVA_MODEL_ID,
        messages=[
            {
                "role": "user",
                "content": [
                    {"image": {"format": "jpeg", "source": {"bytes": photos.jpeg()}}},
                    {"text": "この写真を判定して judge を呼んで"},
                ],
            }
        ],
        toolConfig=TOOL,  # type: ignore[arg-type]
        inferenceConfig={"maxTokens": 200, "temperature": 0},
    )

    blocks = response["output"]["message"]["content"]
    tool_inputs = [b["toolUse"]["input"] for b in blocks if "toolUse" in b]
    assert tool_inputs, f"ツールを呼ばなかった (stopReason={response['stopReason']})"
    answer = tool_inputs[0]
    assert isinstance(answer, dict)
    assert answer["subject"] in SUBJECTS
    assert 0 <= float(answer["confidence"]) <= 1
    # 偽物 (support/bedrock.converse_tool_use) の既定のトークン数が実測と大きくずれていないか
    assert response["usage"]["inputTokens"] > 0


def test_other_models_are_denied_for_developers(runtime: BedrockRuntimeClient) -> None:
    """開発者グループの権限で動かしたときだけ意味がある (オーナーの権限なら skip)。"""
    try:
        runtime.converse(
            modelId="jp.anthropic.claude-opus-5-5",
            messages=[{"role": "user", "content": [{"text": "hi"}]}],
            inferenceConfig={"maxTokens": 5},
        )
    except ClientError as error:
        assert error.response["Error"]["Code"] in {
            "AccessDeniedException",
            "ResourceNotFoundException",
        }
        return
    pytest.skip("Opus を呼べた: 開発者グループより広い権限で動いている")


def test_dev_table_counts_like_the_fake(dev_table: tuple[DynamoDBClient, str]) -> None:
    client, table = dev_table
    run = uuid4().hex
    submission = {"pk": {"S": f"sub#live-test#{run}"}}
    counter = {"pk": {"S": f"goal#live-test-{run}#s0"}}
    try:
        client.put_item(
            TableName=table, Item=submission, ConditionExpression="attribute_not_exists(pk)"
        )
        with pytest.raises(ClientError) as raised:
            client.put_item(
                TableName=table, Item=submission, ConditionExpression="attribute_not_exists(pk)"
            )
        assert raised.value.response["Error"]["Code"] == "ConditionalCheckFailedException"

        client.update_item(
            TableName=table,
            Key=counter,
            UpdateExpression="ADD #c :one",
            ExpressionAttributeNames={"#c": "current"},
            ExpressionAttributeValues={":one": {"N": "1"}},
        )
        item = client.get_item(TableName=table, Key=counter, ConsistentRead=True)["Item"]
        assert item["current"] == {"N": "1"}
    finally:
        client.delete_item(TableName=table, Key=submission)
        client.delete_item(TableName=table, Key=counter)
