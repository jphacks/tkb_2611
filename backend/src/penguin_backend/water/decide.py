"""AI の読み取りから契約の WaterEstimate を作る。

decision と量はここで決め、AI の答えをそのまま返さない。
容量はラベルから読んだ値だけを使い、読めなければ撮り直してもらう (表から決めない)。
"""

from penguin_backend.schemas import Check, WaterEstimate
from penguin_backend.water import label, table
from penguin_backend.water.model import WaterReading


def _is_bottle(reading: WaterReading) -> bool:
    """実物のペットボトルが写っていると AI が答えたか。

    容器の答えと「見えるか」の両方がそろったときだけ。
    """
    return reading.container == "pet_bottle" and reading.bottle_visible


def _capacity(reading: WaterReading) -> int | None:
    """ラベルから読んだ容量。

    ペットボトルでない・書き写しから読めない・数字の答えと合わない・範囲の外なら None。

    書き写しと数字の答えが食い違うのは、表示を読まずに容量を作った答えなので使わない。
    """
    if not _is_bottle(reading):
        return None
    read = label.capacity_ml(reading.label_text)
    if read is None or read != reading.label_capacity_ml:
        return None
    if not table.CAPACITY_MIN_ML <= read <= table.CAPACITY_MAX_ML:
        return None
    return read


def estimate(reading: WaterReading) -> WaterEstimate:
    bottle = _is_bottle(reading)
    capacity = _capacity(reading)
    fill_ratio = table.REMAINING_RATIO.get(reading.remaining)
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
            container="pet_bottle" if bottle else "unknown",
            capacity_ml=0,
            fill_ratio=fill_ratio if bottle and fill_ratio is not None else 0,
            amount_ml=0,
            confidence=reading.confidence,
            decision="retake",
            checks=checks,
            retake_hint=table.RETAKE_HINT_CAPACITY if bottle else table.RETAKE_HINT,
            comment=table.COMMENT_RETAKE_CAPACITY if bottle else table.COMMENT_RETAKE,
        )
    if fill_ratio is None:
        return WaterEstimate(
            container="pet_bottle",
            capacity_ml=capacity,
            fill_ratio=0,
            amount_ml=0,
            confidence=reading.confidence,
            decision="retake",
            checks=checks,
            retake_hint=table.RETAKE_HINT_REMAINING,
            comment=table.COMMENT_RETAKE,
        )

    result = WaterEstimate(
        container="pet_bottle",
        capacity_ml=capacity,
        fill_ratio=fill_ratio,
        amount_ml=round(capacity * fill_ratio),
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
