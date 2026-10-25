"""水の写真の表。数値と文はここにだけ置く (.claude/rules/backend.md の「数値と表」)。"""

from typing import Final

# ラベルから読んだ容量 (ml) として受け付ける範囲。
# 容量は表から決めず、ラベルの表示を読んだ値だけを使う (ボトルの形から 350ml・500ml を決めない)。
# 下は小さいペットボトル (185ml・200ml など) を含め、上は契約の amount_ml の上限 (2000) に合わせる。
# この外の値は読み違いとして扱い、撮り直してもらう。
CAPACITY_MIN_ML: Final = 100
CAPACITY_MAX_ML: Final = 2000

# これより確信度が低い読み取りは retake にする。
# 近道: 根拠となる評価 (写真セットでの確信度ごとの正解率) がまだ無い仮の値。
# 評価をしたら、その表を出典としてここに書き、値を決め直す。
CONFIDENCE_FLOOR: Final = 0.5

DETAIL_BOTTLE: Final = "ペットボトル"
DETAIL_NO_BOTTLE: Final = "ペットボトルが見えない"
DETAIL_CAPACITY_UNREAD: Final = "読めなかった"


def detail_capacity(capacity_ml: int) -> str:
    """ラベルの容量の確かめの 1 行。1000ml 以上はラベルと同じ L で書く。"""
    if capacity_ml >= 1000 and capacity_ml % 100 == 0:
        return f"{capacity_ml / 1000:g}L と書いてある"
    return f"{capacity_ml}ml と書いてある"


RETAKE_HINT: Final = "ペットボトル全体が入るように撮ってね"
RETAKE_HINT_CAPACITY: Final = "ラベルの容量 (500ml・2L など) が写るように撮ってね"
COMMENT_ACCEPT: Final = "水分補給、いいね"
COMMENT_RETAKE: Final = "うーん、よく見えなかったよ"
COMMENT_RETAKE_CAPACITY: Final = "何ml のボトルか読めなかったよ"
COMMENT_REJECT: Final = "本物のペットボトルを撮ってね"
