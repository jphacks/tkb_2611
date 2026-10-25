"""ラベルの内容量の書き写しから容量を読む (water/label.py)。"""

import pytest

from penguin_backend.water.label import capacity_ml


@pytest.mark.parametrize(
    ("text", "ml"),
    [
        ("500ml", 500),
        ("500 mL", 500),
        ("内容量 500ML", 500),
        ("\uff15\uff10\uff10\uff4d\uff4c", 500),  # 全角
        ("2L", 2000),
        ("1.5L", 1500),
        ("2\u2113", 2000),  # リットルの記号
        ("280ミリリットル", 280),
        ("2リットル", 2000),
        ("天然水 600ml 0kcal", 600),
        ("500ml 500ml", 500),
    ],
)
def test_amount_is_read_from_the_label_text(text: str, ml: int) -> None:
    assert capacity_ml(text) == ml


@pytest.mark.parametrize("text", ["", "天然水", "500", "350ml 500ml", "500mg", "5lbs"])
def test_text_without_one_amount_is_unreadable(text: str) -> None:
    assert capacity_ml(text) is None
