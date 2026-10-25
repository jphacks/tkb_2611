"""AI の読み取りから契約の WaterEstimate を作る。

decision と量はここで決め、AI の答えをそのまま返さない。
容量はラベルから読んだ値だけを使い、読めなければ撮り直してもらう (表から決めない)。
"""

from penguin_backend.schemas import Check, WaterEstimate
from penguin_backend.water import table
from penguin_backend.water.model import WaterReading


def _capacity(reading: WaterReading) -> int | None:
    """ラベルから読んだ容量。ペットボトルでない・範囲の外 (読み違い) なら None。"""
    if reading.container != "pet_bottle":
        return None
    if not table.CAPACITY_MIN_ML <= reading.label_capacity_ml <= table.CAPACITY_MAX_ML:
        return None
    return reading.label_capacity_ml


def estimate(reading: WaterReading) -> WaterEstimate:
    bottle = reading.container == "pet_bottle"
    capacity = _capacity(reading)
    checks = [
        Check(
            id="container",
            label="写っていたもの",
            passed=bottle,
            detail=table.DETAIL_BOTTLE if bottle else table.DETAIL_NO_BOTTLE,
        ),
        Check(
            id="capacity",
            label="ラベルの容量",
            passed=capacity is not None,
            detail=table.DETAIL_CAPACITY_UNREAD
            if capacity is None
            else table.detail_capacity(capacity),
        ),
        Check(id="screen", label="画面を撮った写真ではない", passed=not reading.is_screen_photo),
    ]
    if capacity is None:
        return WaterEstimate(
            container=reading.container,
            capacity_ml=0,
            fill_ratio=reading.fill_ratio if bottle else 0,
            amount_ml=0,
            confidence=reading.confidence,
            decision="retake",
            checks=checks,
            retake_hint=table.RETAKE_HINT_CAPACITY if bottle else table.RETAKE_HINT,
            comment=table.COMMENT_RETAKE_CAPACITY if bottle else table.COMMENT_RETAKE,
        )

    result = WaterEstimate(
        container="pet_bottle",
        capacity_ml=capacity,
        fill_ratio=reading.fill_ratio,
        amount_ml=round(capacity * reading.fill_ratio),
        confidence=reading.confidence,
        decision="accept",
        checks=checks,
        comment=table.COMMENT_ACCEPT,
    )
    if reading.is_screen_photo:
        return result.model_copy(update={"decision": "reject", "comment": table.COMMENT_REJECT})
    if reading.confidence < table.CONFIDENCE_FLOOR:
        return result.model_copy(
            update={
                "decision": "retake",
                "retake_hint": table.RETAKE_HINT,
                "comment": table.COMMENT_RETAKE,
            }
        )
    return result
