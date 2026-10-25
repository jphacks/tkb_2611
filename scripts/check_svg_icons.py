"""UI のアイコン (unity/Assets/UI/DesignSystem/Icons/*.svg) が、白一色に色を付ける仕組みに合っているかの検査。

アイコンは白 (#FFFFFF) で描き、画面ごとの色は USS の -unity-background-image-tint-color で付ける。
白以外の色が混ざると色付けが効かず、Unity の取り込みで崩れやすい要素が入ると表示が壊れるので、ここで止める。
描き方の決まりは unity/tools/icons/README.md。

    python scripts/check_svg_icons.py    # リポジトリのルートで
"""

from __future__ import annotations

import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ICONS = Path("unity/Assets/UI/DesignSystem/Icons")
ALLOWED_COLORS = {"none", "#ffffff", "#fff", "white"}
COLOR_ATTRS = ("fill", "stroke", "color", "stop-color")
# Unity の SVG 取り込みで崩れやすいもの、白一色の色付けと合わないもの。
FORBIDDEN = {"style", "mask", "filter", "text", "linearGradient", "radialGradient", "pattern", "image", "script", "foreignObject", "use"}

def problems(path: Path) -> list[str]:
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as e:
        return [f"XML として読めません ({e})"]
    found: list[str] = []
    if root.get("viewBox") is None:
        found.append("viewBox がありません")
    for element in root.iter():
        tag = element.tag.split("}")[-1]
        if tag in FORBIDDEN:
            found.append(f"<{tag}> は使えません")
        if "style" in element.attrib:
            found.append(f"<{tag}> の style 属性は使えません (fill / stroke 属性で書く)")
        for attr in COLOR_ATTRS:
            value = element.get(attr)
            if value is not None and value.strip().lower() not in ALLOWED_COLORS:
                found.append(f"<{tag}> の {attr}=\"{value}\" は白 (#FFFFFF) か none にしてください")
    return found


def main() -> int:
    if not ICONS.is_dir():
        print(f"{ICONS} がありません。")
        return 0
    failed = False
    for path in sorted(ICONS.glob("*.svg")):
        for p in problems(path):
            print(f"{path.as_posix()}: {p}", file=sys.stderr)
            failed = True
    if failed:
        print("アイコンの決まりは unity/tools/icons/README.md を参照してください。", file=sys.stderr)
        return 1
    print(f"OK: {len(list(ICONS.glob('*.svg')))} 個のアイコンが白一色の決まりに合っています。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
