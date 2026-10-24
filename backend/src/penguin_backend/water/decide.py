"""AI の読み取りから契約の WaterEstimate を作る。

decision と量はここで決め、AI の答えをそのまま返さない。
"""

from penguin_backend.schemas import Check, WaterEstimate
from penguin_backend.water import table
from penguin_backend.water.model import WaterReading


def estimate(reading: WaterReading) -> WaterEstimate:
    capacity = table.CAPACITY_ML.get(reading.container)
    checks = [
        Check(
            id="container",
            label="写っていたもの",
            passed=capacity is not None,
            detail=table.CONTAINER_DETAIL[reading.container],
        ),
        Check(id="screen", label="画面を撮った写真ではない", passed=not reading.is_screen_photo),
    ]
    if capacity is None:
        return WaterEstimate(
            container="unknown",
            fill_ratio=0,
            amount_ml=0,
            confidence=reading.confidence,
            decision="retake",
            checks=checks,
            retake_hint=table.RETAKE_HINT,
            comment=table.COMMENT_RETAKE,
        )

    result = WaterEstimate(
        container=reading.container,
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
