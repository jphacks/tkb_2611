"""scripts/check_svg_icons.py の判定と、unity/tools/icons/build_icons.py の正規化。

アイコンは白一色で描き、色は USS で付ける。白以外の色や Unity で崩れる要素が入ったら 1 を返すことを確かめる。
"""
import importlib.util
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ICONS = "unity/Assets/UI/DesignSystem/Icons"
WHITE = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><path d="M0,0H8V8Z" fill="#FFFFFF"/></svg>'


def builder():
    spec = importlib.util.spec_from_file_location("build_icons", ROOT / "unity/tools/icons/build_icons.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def check(repo: Path, name: str, text: str) -> subprocess.CompletedProcess:
    (repo / "scripts").mkdir(exist_ok=True)
    shutil.copy(ROOT / "scripts/check_svg_icons.py", repo / "scripts")
    path = repo / ICONS / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return subprocess.run([sys.executable, "scripts/check_svg_icons.py"], cwd=repo, capture_output=True, text=True, encoding="utf-8")


def test_white_icon_passes(tmp_path):
    assert check(tmp_path, "icon-a.svg", WHITE).returncode == 0


def test_line_icon_with_white_stroke_passes(tmp_path):
    line = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><g fill="none" stroke="white"><path d="M0,0H8"/></g></svg>'
    assert check(tmp_path, "icon-a.svg", line).returncode == 0


def test_colored_fill_fails(tmp_path):
    result = check(tmp_path, "icon-a.svg", WHITE.replace("#FFFFFF", "#1B2A3A"))
    assert result.returncode == 1
    assert "icon-a.svg" in result.stderr


def test_forbidden_element_fails(tmp_path):
    result = check(tmp_path, "icon-a.svg", WHITE.replace("<path", "<style>path{fill:red}</style><path"))
    assert result.returncode == 1
    assert "style" in result.stderr


def test_missing_viewbox_fails(tmp_path):
    assert check(tmp_path, "icon-a.svg", WHITE.replace(' viewBox="0 0 256 256"', "")).returncode == 1


def test_normalize_makes_phosphor_white_per_element():
    out = builder().normalize('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" fill="currentColor"><path d="M0,0H8V8Z"/></svg>')
    assert 'fill="#FFFFFF"' in out
    assert "currentColor" not in out
    assert 'viewBox="0 0 256 256"' in out


def test_normalize_keeps_cutouts_and_none():
    out = builder().normalize('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><g fill="none" stroke="white"><path d="M0,0H8" fill-rule="evenodd"/></g></svg>')
    assert 'fill="none"' in out
    assert 'stroke="#FFFFFF"' in out
    assert 'fill-rule="evenodd"' in out


def test_repository_icons_follow_rules_and_match_sources():
    """リポジトリのアイコンが決まりに合い、unity/tools/icons の原本から書き出したものと一致する (CI と同じ確認)。"""
    rules = subprocess.run([sys.executable, "scripts/check_svg_icons.py"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    assert rules.returncode == 0, rules.stderr
    build = subprocess.run([sys.executable, "unity/tools/icons/build_icons.py", "--check"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    assert build.returncode == 0, build.stderr
