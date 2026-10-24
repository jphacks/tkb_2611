"""Bedrock (Nova 2 Lite) の偽物。botocore の Stubber で、本物の Converse の応答の形を作る。

moto は bedrock-runtime を持たないので Stubber を使う。
Stubber は応答をサービスの定義で検証するため、
ここで作った応答は本物の Converse と同じ形をしている (形が違えば Stubber が落ちる)。

使い方 (conftest.py の bedrock フィクスチャがクライアントと Stubber を用意する):

    bedrock.stubber.add_response("converse", converse_tool_use({"subject": "my_bottle", ...}))
    # → 本番のコードに bedrock.client を渡して呼ぶ

失敗は add_client_error で作る (THROTTLED など下の定数)。読み込みのタイムアウト
(botocore.exceptions.ReadTimeoutError) は Stubber では作れないので、
monkeypatch で converse を差し替える。
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any

import boto3
from botocore.stub import Stubber
from mypy_boto3_bedrock_runtime import BedrockRuntimeClient

# 写真判定に使う推論プロファイル (project-penguin の docs/adr/0001)。本番のコードは設定から読む。
NOVA_MODEL_ID = "jp.amazon.nova-2-lite-v1:0"
REGION = "ap-northeast-1"

# (エラーの code, HTTP 状態)。契約では、どれも 503 upstream_unavailable (retryable) に写す
# (ValidationException だけは送ったものの問題なので、500 internal か 400 image_invalid かを
# 当日決める)。
THROTTLED = ("ThrottlingException", 429)
MODEL_TIMEOUT = ("ModelTimeoutException", 408)
UNAVAILABLE = ("ServiceUnavailableException", 503)
INTERNAL = ("InternalServerException", 500)
INVALID = ("ValidationException", 400)


@dataclass
class StubbedBedrock:
    client: BedrockRuntimeClient
    stubber: Stubber

    def respond(self, response: dict[str, Any]) -> None:
        self.stubber.add_response("converse", response)

    def fail(self, error: tuple[str, int], message: str = "stubbed") -> None:
        code, status = error
        self.stubber.add_client_error(
            "converse", service_error_code=code, service_message=message, http_status_code=status
        )


def stubbed_bedrock() -> StubbedBedrock:
    """Stubber を付けた bedrock-runtime のクライアント。偽の認証情報で作り、本物には届かない。"""
    client: BedrockRuntimeClient = boto3.client(
        "bedrock-runtime",
        region_name=REGION,
        aws_access_key_id="testing",
        aws_secret_access_key="testing",  # noqa: S106 (偽物の値)
    )
    return StubbedBedrock(client=client, stubber=Stubber(client))


def _usage(input_tokens: int, output_tokens: int) -> dict[str, int]:
    return {
        "inputTokens": input_tokens,
        "outputTokens": output_tokens,
        "totalTokens": input_tokens + output_tokens,
    }


def converse_tool_use(
    tool_input: dict[str, Any],
    *,
    tool_name: str = "judge",
    input_tokens: int = 1429,
    output_tokens: int = 80,
    latency_ms: int = 900,
) -> dict[str, Any]:
    """モデルがツールを呼んで答えた応答 (判定の結果を JSON で返させる形)。

    トークン数の既定値は、ADR 0001 の検証で Nova 2 Lite が 64x64〜1280px の写真に使った実測値。
    """
    return {
        "output": {
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "toolUse": {
                            "toolUseId": "tooluse_test",
                            "name": tool_name,
                            "input": tool_input,
                        }
                    }
                ],
            }
        },
        "stopReason": "tool_use",
        "usage": _usage(input_tokens, output_tokens),
        "metrics": {"latencyMs": latency_ms},
    }


def converse_text(
    text: str,
    *,
    stop_reason: str = "end_turn",
    input_tokens: int = 1429,
    output_tokens: int = 40,
) -> dict[str, Any]:
    """ツールを呼ばずに文で答えた応答 (指示に従わなかった・途中で切れたときの確認用)。"""
    return {
        "output": {"message": {"role": "assistant", "content": [{"text": text}]}},
        "stopReason": stop_reason,
        "usage": _usage(input_tokens, output_tokens),
        "metrics": {"latencyMs": 700},
    }


def judge_input(
    subject: str = "my_bottle",
    *,
    confidence: float = 0.95,
    decision: str = "accept",
    comment: str = "マイボトル、えらいね",
    **extra: Any,
) -> dict[str, Any]:
    """判定のツールの入力の例 (ADR 0001 の検証で使った形)。壊れた入力は extra で上書きして作る。"""
    return {
        "subject": subject,
        "confidence": confidence,
        "decision": decision,
        "comment": comment,
        **extra,
    }


def broken_json_text() -> str:
    """JSON のつもりで途中で切れた文 (ツールを使わず文で返したモデルの、壊れた答え)。"""
    return json.dumps(judge_input())[:25]
