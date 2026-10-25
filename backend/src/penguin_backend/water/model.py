"""水の写真を AI に読ませる。読み取りの中身は提供元を知らない WaterReading で受け渡す。

AI に答えさせるのは、見えているものの一文・ボトルの実物が見えるか・容器・
ラベルの容量の表示の書き写し・残りの段階・確信度・画面の撮影らしさだけ。
容量はラベルの表示からだけ読ませ、形や大きさから推し量らせない。
何も写っていない画像にも「500ml のペットボトル」と答える作り話を防ぐため、
先に見えているものを書かせ、容量はサーバーが書き写しの文字から読み直す (decide.py)。
ml と decision はサーバーが決める。
見えているものの一文と書き写しは AI の生の出力なので、記録にも応答にも出さない。
写真と AI の生の出力は、ログ・例外の文に入れない (.claude/rules/privacy.md)。記録するのは数だけ。
"""

from __future__ import annotations

import logging
import time
from typing import TYPE_CHECKING, Any, Literal, Protocol, get_args

from botocore.exceptions import BotoCoreError, ClientError
from pydantic import BaseModel, Field, ValidationError, field_validator

from penguin_backend.errors import ApiError

if TYPE_CHECKING:
    # 型のスタブは開発用の依存なので、型の検査のときだけ読む (aws.py と同じ)
    from mypy_boto3_bedrock_runtime import BedrockRuntimeClient

logger = logging.getLogger(__name__)

# AI に選ばせる容器。容量は container に入れず label_text と label_capacity_ml で答えさせる
ReadContainer = Literal["pet_bottle", "unknown"]
_CONTAINERS: tuple[str, ...] = get_args(ReadContainer)

# 残りの量の段階。0〜1 の数字で答えさせると、空のボトルを満杯と答えるなど
# ぶれが大きかった (合成の画像で確かめた)。
# 割合への直し方は table.REMAINING_RATIO
ReadRemaining = Literal["empty", "little", "quarter", "half", "three_quarters", "full", "unknown"]
_REMAINING: tuple[str, ...] = get_args(ReadRemaining)
TOOL_NAME = "estimate"

_PROMPT = (
    "写真を見て estimate を呼んでください。"
    "推測で埋めず、写真に実際に見えていることだけを答えてください。"
    "seen は、写真に写っているものを短い一文で。"
    "bottle_visible は、実物のペットボトル (ボトルの本体とキャップ) が写っていれば true。"
    "無地の画像・文字だけの画像・ペットボトル以外の物なら false。"
    "container は、bottle_visible が true のときだけ pet_bottle、それ以外は unknown。"
    "label_text は、ラベルやキャップに印刷された内容量の表示を、"
    "写真のとおりに書き写したもの (例: 500ml、2L)。"
    "表示が見えない・読めないときは空の文字列。見えていない数字を書いてはいけません。"
    "label_capacity_ml は label_text を ml にした値 (2L なら 2000)。label_text が空なら 0。"
    "ボトルの形や大きさから容量を推し量ってはいけません。"
    "remaining は、ボトルの中に残っている液体の量。empty は空 (底に数滴まで)、little は 1 割ほど、"
    "quarter は 4 分の 1、half は半分、three_quarters は 4 分の 3、full は満杯。"
    "見分けられなければ unknown。"
    "confidence は、答え全体の確かさ (0〜1)。"
    "is_screen_photo は、画面・印刷物・絵に映ったものを撮った写真なら true。"
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
                            "seen",
                            "bottle_visible",
                            "container",
                            "label_text",
                            "label_capacity_ml",
                            "remaining",
                            "confidence",
                            "is_screen_photo",
                        ],
                        # 見えているもの → ボトルか → ラベル → 残り、の順に答えさせる
                        "properties": {
                            "seen": {"type": "string", "maxLength": 200},
                            "bottle_visible": {"type": "boolean"},
                            "container": {"type": "string", "enum": list(_CONTAINERS)},
                            "label_text": {"type": "string", "maxLength": 40},
                            "label_capacity_ml": {"type": "integer", "minimum": 0},
                            "remaining": {"type": "string", "enum": list(_REMAINING)},
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
    """AI の答えを検証したもの。知らない容器・残りは unknown、確信度は 0〜1 に収める。

    label_text と label_capacity_ml はラベルから読んだそのままの値。
    使えるかどうかは decide.py が決める。
    seen と label_text は AI の生の出力なので、記録にも応答にも出さない (repr にも出さない)。
    """

    seen: str = Field(default="", repr=False)
    bottle_visible: bool
    container: ReadContainer
    label_text: str = Field(default="", repr=False)
    label_capacity_ml: int
    remaining: ReadRemaining
    confidence: float
    is_screen_photo: bool

    @field_validator("container", mode="before")
    @classmethod
    def _unknown_outside_the_choices(cls, value: object) -> object:
        return value if value in _CONTAINERS else "unknown"

    @field_validator("remaining", mode="before")
    @classmethod
    def _unknown_outside_the_levels(cls, value: object) -> object:
        return value if value in _REMAINING else "unknown"

    @field_validator("seen", "label_text", mode="before")
    @classmethod
    def _text_or_empty(cls, value: object) -> object:
        return value[:200] if isinstance(value, str) else ""

    @field_validator("confidence", mode="after")
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
                inferenceConfig={"maxTokens": 400, "temperature": 0},
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
