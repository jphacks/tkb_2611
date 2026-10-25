"""AI が書き写したラベルの内容量の表示 (「500ml」「2L」など) から、容量 (ml) を読む。

容量は AI の数字の答えをそのまま使わず、書き写した文字からサーバーが読み直す。
数字の答えと書き写しが食い違う (作り話の) 読み取りを見分けるため (decide.py)。
"""

import re
import unicodedata
from typing import Final

# 数字と単位。「500ml」「500 mL」「1.5L」「500ミリリットル」など。
# NFKC で全角の数字・文字と、リットルの記号 (U+2113) を半角にそろえてから読む
_AMOUNT: Final = re.compile(
    r"(\d+(?:\.\d+)?)\s*(ml|ミリリットル|l|リットル)(?![a-z])", re.IGNORECASE
)


def capacity_ml(label_text: str) -> int | None:
    """書き写しから読んだ容量。読めない・違う容量が 2 つ以上書いてあるなら None。"""
    text = unicodedata.normalize("NFKC", label_text)
    found = set()
    for number, unit in _AMOUNT.findall(text):
        value = float(number) * (1 if unit.lower() in ("ml", "ミリリットル") else 1000)
        found.add(round(value))
    return found.pop() if len(found) == 1 else None
