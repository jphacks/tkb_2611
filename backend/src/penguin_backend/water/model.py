"""水の写真を AI に読ませる。読み取りの中身は提供元を知らない WaterReading で受け渡す。

AI に答えさせるのは容器・ラベルに書いてある容量・水位・確信度・画面の撮影らしさだけ。
容量はラベルの表示からだけ読ませ、形や大きさから推し量らせない。
ml と decision はサーバーが決める (decide.py)。
写真と AI の生の出力は、ログ・例外の文に入れない (.claude/rules/privacy.md)。記録するのは数だけ。
"""

import logging
import time
from typing import Any, Literal, Protocol, get_args

from botocore.exceptions import BotoCoreError, ClientError
from mypy_boto3_bedrock_runtime import BedrockRuntimeClient
from pydantic import BaseModel, ValidationError, field_validator

from penguin_backend.errors import ApiError

logger = logging.getLogger(__name__)

# AI に選ばせる容器。容量は container に入れず label_capacity_ml で答えさせる
ReadContainer = Literal["pet_bottle", "unknown"]
_CONTAINERS: tuple[str, ...] = get_args(ReadContainer)
TOOL_NAME = "estimate"

_PROMPT = (
    "写真に写っている飲み物の容器を見て、estimate を呼んでください。"
    "container は、ペットボトルなら pet_bottle、それ以外・見分けられないときは unknown。"
    "label_capacity_ml は、ラベルやキャップに印刷された内容量の表示を読んだ値 (ml)。"
    "「500ml」なら 500、「2L」なら 2000、「1.5L」なら 1500。"
    "表示が写っていない・読めないときは 0。ボトルの形や大きさから推し量ってはいけません。"
    "fill_ratio は容器の容量に対する、中に残っている液体の割合 (0〜1)。"
    "is_screen_photo は、画面や印刷物に映ったものを撮った写真なら true。"
)

_TOOL_CONFIG: dict[str, Any] = {
    "tools": [
        {
            "toolSpec": {
                "name": TOOL_NAME,
                "description": "ペットボトルの写真の読み取りを返す",
                "inputSchema": {
                    "json": {
                        "type": "object",
                        "required": [
                            "container",
                            "label_capacity_ml",
                            "fill_ratio",
                            "confidence",
                            "is_screen_photo",
                        ],
                        "properties": {
                            "container": {"type": "string", "enum": list(_CONTAINERS)},
                            "label_capacity_ml": {"type": "integer", "minimum": 0},
                            "fill_ratio": {"type": "number", "minimum": 0, "maximum": 1},
                            "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                            "is_screen_photo": {"type": "boolean"},
                        },
                    }
                },
            }
        }
    ],
    "toolChoice": {"tool": {"name": TOOL_NAME}},
}


def _clamp(value: float) -> float:
    return min(1.0, max(0.0, value))


class WaterReading(BaseModel):
    """AI の答えを検証したもの。知らない容器は unknown、割合と確信度は 0〜1 に収める。

    label_capacity_ml はラベルから読んだそのままの値。使える範囲かどうかは decide.py が決める。
    """

    container: ReadContainer
    label_capacity_ml: int
    fill_ratio: float
    confidence: float
    is_screen_photo: bool

    @field_validator("container", mode="before")
    @classmethod
    def _unknown_outside_the_choices(cls, value: object) -> object:
        return value if value in _CONTAINERS else "unknown"

    @field_validator("fill_ratio", "confidence", mode="after")
    @classmethod
    def _within_unit(cls, value: float) -> float:
        return _clamp(value)


class WaterModel(Protocol):
    def read(self, image: bytes) -> WaterReading: ...


class NovaWaterModel:
    """Amazon Bedrock の Nova 2 Lite (Converse のツール呼び出しで構造化して答えさせる)。"""

    def __init__(self, client: BedrockRuntimeClient, model_id: str) -> None:
        self._client = client
        self._model_id = model_id

    def read(self, image: bytes) -> WaterReading:
        started = time.monotonic()
        try:
            response = self._client.converse(
                modelId=self._model_id,
                messages=[
                    {
                        "role": "user",
                        "content": [
                            # 写真はインラインで送る (提供元に別に残る機能を使わない)
                            {"image": {"format": "jpeg", "source": {"bytes": image}}},
                            {"text": _PROMPT},
                        ],
                    }
                ],
                toolConfig=_TOOL_CONFIG,  # type: ignore[arg-type]
                inferenceConfig={"maxTokens": 200, "temperature": 0},
            )
        except ClientError as error:
            code = error.response.get("Error", {}).get("Code", "")
            logger.warning("water model failed: %s", code)
            if code == "ValidationException":
                # 送ったもの (ツールの定義など) の誤り。送り直しても直らない
                raise ApiError("internal", 500) from error
            raise ApiError("upstream_unavailable", 503) from error
        except BotoCoreError as error:
            # 読み込みのタイムアウト・接続の失敗
            logger.warning("water model failed: %s", type(error).__name__)
            raise ApiError("upstream_unavailable", 503) from error

        usage = response.get("usage", {})
        logger.info(
            "water model answered: %d bytes, %s input tokens, %s output tokens, %.0f ms",
            len(image),
            usage.get("inputTokens"),
            usage.get("outputTokens"),
            (time.monotonic() - started) * 1000,
        )
        answers = [
            block["toolUse"]["input"]
            for block in response["output"].get("message", {}).get("content", [])
            if "toolUse" in block and block["toolUse"].get("name") == TOOL_NAME
        ]
        if not answers:
            logger.warning("water model did not call the tool: %s", response.get("stopReason"))
            raise ApiError("upstream_unavailable", 503)
        try:
            return WaterReading.model_validate(answers[0])
        except ValidationError as error:
            # 検証の文には AI の答えが入るので記録しない
            logger.warning("water model answered out of shape: %d problems", error.error_count())
            raise ApiError("upstream_unavailable", 503) from None
