"""UI のアイコン (Assets/UI/DesignSystem/Icons/*.svg) を、Phosphor Icons の原本と自作の原本から書き出す。

対応表は icons.json。手順と自作の描き方の決まりは README.md。標準ライブラリだけで動く。

    uv run --no-project python tools/icons/build_icons.py --fetch        # Phosphor の原本を vendor/ に取る (ネットが要る)
    uv run --no-project python tools/icons/build_icons.py                # Assets へ書き出す
    uv run --no-project python tools/icons/build_icons.py --check        # Assets が原本どおりか (CI)
    uv run --no-project python tools/icons/build_icons.py --preview out.html   # 見比べページ
"""

from __future__ import annotations

import argparse
import base64
import html
import json
import re
import sys
import urllib.request
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
ASSETS = ROOT / "Assets" / "UI" / "DesignSystem" / "Icons"
VENDOR = HERE / "vendor" / "phosphor"
CUSTOM = HERE / "custom"
CDN = "https://cdn.jsdelivr.net/npm/@phosphor-icons/core@{version}/{path}"
SVG_NS = "http://www.w3.org/2000/svg"

ET.register_namespace("", SVG_NS)


def load_table() -> dict:
    return json.loads((HERE / "icons.json").read_text(encoding="utf-8"))


def split_ref(ref: str, default_weight: str) -> tuple[str, str]:
    """"名前" か "名前:太さ" を (名前, 太さ) にする。"""
    name, _, weight = ref.partition(":")
    return name, weight or default_weight


def vendor_path(name: str, weight: str) -> Path:
    return VENDOR / weight / f"{name}-{weight}.svg"


def references(table: dict, with_alternatives: bool) -> list[tuple[str, str]]:
    refs: list[tuple[str, str]] = []
    for entry in table["icons"].values():
        if "phosphor" not in entry:
            continue
        refs.append((entry["phosphor"], entry["weight"]))
        if with_alternatives:
            refs += [split_ref(a, entry["weight"]) for a in entry.get("alternatives", [])]
    return sorted(set(refs))


def fetch(table: dict) -> None:
    """icons.json にある原本を取り、どこからも指されなくなった原本は消す (候補を決め終えたあとの片付け)。"""
    version = table["phosphor_version"]
    wanted = references(table, with_alternatives=True)
    for path in VENDOR.glob("*/*.svg"):
        if (path.stem.removesuffix(f"-{path.parent.name}"), path.parent.name) not in wanted:
            path.unlink()
            print(f"削除: {path.parent.name}/{path.name}")
    for name, weight in wanted:
        path = vendor_path(name, weight)
        if path.exists():
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        url = CDN.format(version=version, path=f"assets/{weight}/{name}-{weight}.svg")
        with urllib.request.urlopen(url, timeout=30) as r:
            path.write_bytes(r.read())
        print(f"取得: {weight}/{name}")
    license_path = VENDOR / "LICENSE"
    if not license_path.exists():
        with urllib.request.urlopen(CDN.format(version=version, path="LICENSE"), timeout=30) as r:
            license_path.write_bytes(r.read())


def normalize(svg_text: str) -> str:
    """色を白 (#FFFFFF) にそろえ、Unity が確実に読める最小の形にする。

    Phosphor は根の svg に fill="currentColor" を付けて子へ継がせている。継承に頼らず、描く要素ごとに白を書き込む。
    """
    root = ET.fromstring(svg_text)
    view_box = root.get("viewBox")
    if view_box is None:
        raise ValueError("viewBox がありません")
    root_fill = root.get("fill", "#FFFFFF")
    out = ET.Element(f"{{{SVG_NS}}}svg", {"viewBox": view_box})
    for child in root:
        out.append(_whiten(child, root_fill))
    ET.indent(out, space="  ")
    return ET.tostring(out, encoding="unicode") + "\n"


DRAWN = {"path", "circle", "ellipse", "rect", "polygon", "polyline", "line", "g"}


def _whiten(element: ET.Element, inherited_fill: str) -> ET.Element:
    tag = element.tag.split("}")[-1]
    if tag not in DRAWN:
        raise ValueError(f"使えない要素です: <{tag}>")
    attrs = {k: v for k, v in element.attrib.items() if k not in ("class", "style", "id")}
    fill = attrs.get("fill", inherited_fill)
    for key in ("fill", "stroke"):
        if key in attrs and attrs[key] != "none":
            attrs[key] = "#FFFFFF"
    if tag != "g" and "fill" not in attrs:
        attrs["fill"] = "none" if fill == "none" else "#FFFFFF"
    copy = ET.Element(f"{{{SVG_NS}}}{tag}", attrs)
    for child in element:
        copy.append(_whiten(child, "none" if fill == "none" else "#FFFFFF"))
    return copy


def source_of(name: str, entry: dict) -> Path | None:
    """書き出す元。自作がまだ無いものは None (Assets の今のファイルを残す)。"""
    if entry.get("custom"):
        path = CUSTOM / f"{name}.svg"
        return path if path.exists() else None
    return vendor_path(entry["phosphor"], entry["weight"])


def build(table: dict, check: bool) -> int:
    stale: list[str] = []
    for name, entry in table["icons"].items():
        source = source_of(name, entry)
        if source is None:
            continue
        if not source.exists():
            print(f"原本がありません: {source.relative_to(ROOT)} (--fetch を流してください)", file=sys.stderr)
            return 1
        expected = normalize(source.read_text(encoding="utf-8"))
        target = ASSETS / f"{name}.svg"
        current = target.read_text(encoding="utf-8") if target.exists() else None
        if current == expected:
            continue
        if check:
            stale.append(name)
        else:
            target.write_text(expected, encoding="utf-8", newline="\n")
            print(f"書き出し: {target.relative_to(ROOT)}")
    if stale:
        print("Assets のアイコンが tools/icons の原本と一致しません。build_icons.py を流してください:", file=sys.stderr)
        for name in stale:
            print(f"  {name}.svg", file=sys.stderr)
        return 1
    return 0


# ---------------------------------------------------------------------------
# 見比べページ
# ---------------------------------------------------------------------------


def _inline(svg_text: str) -> str:
    """白を currentColor にして、ページの色で塗れる inline SVG にする。"""
    text = re.sub(r"<\?xml[^>]*>", "", svg_text)
    text = re.sub(r'(?i)"(#FFFFFF|#fff|white|currentColor)"', '"currentColor"', text)
    if "fill=" not in text.split(">", 1)[0]:
        text = text.replace("<svg", '<svg fill="currentColor"', 1)
    return text


def _cell(svg_text: str | None, caption: str, chosen: bool = False) -> str:
    if svg_text is None:
        return f'<figure class="none"><div>まだ無い</div><figcaption>{html.escape(caption)}</figcaption></figure>'
    s = _inline(svg_text)
    cls = "chosen" if chosen else ""
    return (
        f'<figure class="{cls}"><div class="pair"><span class="tile light">{s}</span>'
        f'<span class="tile chip">{s}</span><span class="tiny">{s}</span></div>'
        f"<figcaption>{html.escape(caption)}</figcaption></figure>"
    )


def preview(table: dict, out: Path, only_custom: bool = False) -> None:
    rows = []
    for name, entry in table["icons"].items():
        if only_custom and not entry.get("custom"):
            continue
        current_path = ASSETS / f"{name}.svg"
        current = current_path.read_text(encoding="utf-8") if current_path.exists() else None
        cells = [_cell(current, "今")]
        if entry.get("custom"):
            source = CUSTOM / f"{name}.svg"
            cells.append(_cell(source.read_text(encoding="utf-8") if source.exists() else None, "自作", chosen=True))
            for draft in sorted(CUSTOM.glob(f"{name}.draft-*.svg")):
                cells.append(_cell(draft.read_text(encoding="utf-8"), draft.stem.split(".")[-1]))
        else:
            chosen = vendor_path(entry["phosphor"], entry["weight"])
            cells.append(_cell(chosen.read_text(encoding="utf-8") if chosen.exists() else None, f"{entry['phosphor']} ({entry['weight']})", chosen=True))
            for ref in entry.get("alternatives", []):
                alt_name, alt_weight = split_ref(ref, entry["weight"])
                alt = vendor_path(alt_name, alt_weight)
                cells.append(_cell(alt.read_text(encoding="utf-8") if alt.exists() else None, f"{alt_name} ({alt_weight})"))
        rows.append(f'<tr><th>{html.escape(name)}</th><td><div class="cands">{"".join(cells)}</div></td></tr>')
    out.write_text(
        f"""<!doctype html><html lang="ja"><head><meta charset="utf-8"><title>アイコンの見比べ</title>
<style>
body {{ margin:0; padding:24px 16px 64px; background:#EEF7FF; color:#17385F; font-family:"M PLUS Rounded 1c","Yu Gothic UI",sans-serif; }}
h1 {{ font-size:20px; margin:0 0 4px; }} p {{ color:#6B8199; font-size:13px; margin:0 0 16px; }}
table {{ border-collapse:collapse; background:#fff; border-radius:16px; overflow:hidden; width:100%; max-width:1000px; }}
th {{ width:150px; text-align:left; font-size:12px; padding:10px; vertical-align:top; border-top:1px solid #E3EEF7; font-weight:600; }}
td {{ padding:10px; border-top:1px solid #E3EEF7; }}
.cands {{ display:flex; flex-wrap:wrap; gap:14px; }}
figure {{ margin:0; padding:4px; border-radius:12px; }} figure.chosen {{ outline:3px solid #FFC928; }}
figcaption {{ font-size:11px; color:#6B8199; margin-top:4px; }}
.pair {{ display:flex; align-items:center; gap:6px; }}
.tile {{ width:56px; height:56px; display:grid; place-items:center; border-radius:14px; }}
.tile svg {{ width:40px; height:40px; }}
.light {{ background:#EEF7FF; color:#1F8BEA; border-bottom:4px solid #B5DBF7; }}
.chip {{ background:#1F8BEA; color:#fff; border-bottom:4px solid #1565C0; }}
.tiny {{ color:#17385F; }} .tiny svg {{ width:20px; height:20px; }}
figure.none div {{ width:56px; height:56px; display:grid; place-items:center; color:#B0BEC9; font-size:11px; }}
</style></head><body><h1>アイコンの見比べ</h1>
<p>黄色の枠が icons.json で選んでいるもの。それぞれ「淡い下地」「主色のボタン」「20px」で表示。</p>
<table>{"".join(rows)}</table></body></html>""",
        encoding="utf-8",
    )
    print(f"見比べページ: {out}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--fetch", action="store_true", help="Phosphor の原本を vendor/ に取る")
    parser.add_argument("--check", action="store_true", help="Assets が原本どおりか確かめるだけ")
    parser.add_argument("--preview", type=Path, help="見比べページを書き出す (Assets には書かない)")
    parser.add_argument("--custom-only", action="store_true", help="見比べページに自作のものだけを並べる")
    args = parser.parse_args()
    table = load_table()
    if args.fetch:
        fetch(table)
        return 0
    if args.preview:
        preview(table, args.preview, only_custom=args.custom_only)
        return 0
    return build(table, check=args.check)


if __name__ == "__main__":
    sys.exit(main())
