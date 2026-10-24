"""水の写真の表。数値と文はここにだけ置く (.claude/rules/backend.md の「数値と表」)。"""

from typing import Final

from penguin_backend.schemas import WaterContainer

# 容器ごとの容量 (ml)。出典はペットボトルのラベルの表示容量 (ID の数字と同じ)。
# 扱うのはペットボトルだけ (docs/decisions.md、手入力の量の裏付けが目的)。
CAPACITY_ML: Final[dict[WaterContainer, int]] = {
    "pet_bottle_350": 350,
    "pet_bottle_500": 500,
}

# これより確信度が低い読み取りは retake にする。
# 近道: 根拠となる評価 (写真セットでの確信度ごとの正解率) がまだ無い仮の値。
# 評価をしたら、その表を出典としてここに書き、値を決め直す。
CONFIDENCE_FLOOR: Final = 0.5

CONTAINER_DETAIL: Final[dict[WaterContainer, str]] = {
    "pet_bottle_350": "350ml のペットボトル",
    "pet_bottle_500": "500ml のペットボトル",
    "unknown": "ペットボトルが見えない",
}

RETAKE_HINT: Final = "ペットボトル全体が入るように撮ってね"
COMMENT_ACCEPT: Final = "水分補給、いいね"
COMMENT_RETAKE: Final = "うーん、よく見えなかったよ"
COMMENT_REJECT: Final = "本物のペットボトルを撮ってね"
