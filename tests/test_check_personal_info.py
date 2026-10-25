"""scripts/check_personal_info.py と .claude/hooks/guard_personal.py の判定。

テストに使う名前は架空のもの (「架空太郎」「Fictional」)。本物の名前をテストに書かない。
引っかけるための例 (メールアドレス・電話番号) は、このファイル自体が検査に引っかからないよう、
文字列をつないで作る (AT・DASH)。
"""

from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path
from types import ModuleType

import pytest

ROOT = Path(__file__).resolve().parents[1]
AT = "@"
DASH = "-"
EMAIL = "someone.person" + AT + "gmail.com"


def load(path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(path.stem, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[path.stem] = module  # dataclass が自分のモジュールを引けるように
    spec.loader.exec_module(module)
    return module


checker = load(ROOT / "scripts" / "check_personal_info.py")
DENY = checker.denylist_patterns(["架空太郎", "Fictional"])


def kinds(text: str, deny: list = DENY) -> list[str]:
    return [f.kind for f in checker.scan_text(text, "t", deny)]


@pytest.mark.parametrize(
    "text",
    [
        f"連絡は {EMAIL} まで",
        "電話 090" + DASH + "1234" + DASH + "5678",
        "固定 03" + DASH + "1234" + DASH + "5678",
        "〒" + "100" + DASH + "0001",
        "担当は架空太郎さん",
        "reviewed by fictional",
    ],
)
def test_personal_info_is_found(text: str) -> None:
    assert kinds(text)


@pytest.mark.parametrize(
    "text",
    [
        "Co-Authored-By: Claude <noreply@anthropic.com>",
        "123+someone@users.noreply.github.com",
        "例: user@example.com",
        "uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1",
        '@app.api_route("/api/v1/health", methods=["GET"])',
        "日付は 2026-10-24、時刻は 10:20",
        "fictionalize は別の単語",
        "公開役が上げる。担当 A が判定を作る",
    ],
)
def test_ordinary_text_passes(text: str) -> None:
    assert kinds(text) == []


def test_denylist_reads_env_and_ignores_comments(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    monkeypatch.setenv(checker.ENV_DENYLIST, "架空太郎, Fictional\n# コメント\n")
    monkeypatch.setattr(checker, "HOME_DENYLIST", tmp_path / "none")

    assert set(checker.load_denylist(tmp_path)) == {"架空太郎", "Fictional"}


def test_added_lines_come_with_their_file_and_line() -> None:
    diff = "\n".join(
        [
            "+++ b/docs/x.md",
            "@@ -0,0 +3,2 @@",
            "+ふつうの行",
            f"+連絡は {EMAIL}",
        ]
    )

    findings = list(checker.scan_diff(diff, DENY))

    assert [(f.where, f.kind) for f in findings] == [("docs/x.md:4", "メールアドレス")]


def test_lock_files_are_skipped() -> None:
    diff = "+++ b/backend/uv.lock\n@@ -0,0 +1 @@\n+size = 090" + "12345678\n"

    assert list(checker.scan_diff(diff, DENY)) == []


def run_guard(payload: dict[str, object], env: dict[str, str]) -> str:
    out = subprocess.run(
        [sys.executable, str(ROOT / ".claude" / "hooks" / "guard_personal.py")],
        input=json.dumps(payload),
        capture_output=True,
        text=True,
        check=True,
        env=env,
    )
    return out.stdout


@pytest.fixture
def guard_env(tmp_path: Path) -> dict[str, str]:
    # 手元の拒否リストに左右されないよう、各OSのホームを空の場所にする。
    return {
        "PATH": "/usr/bin:/bin",
        "HOME": str(tmp_path),
        "USERPROFILE": str(tmp_path),
        checker.ENV_DENYLIST: "架空太郎",
    }


def test_guard_stops_writing_a_name(guard_env: dict[str, str]) -> None:
    payload = {
        "tool_name": "Write",
        "tool_input": {"file_path": "docs/a.md", "content": "架空太郎が作った"},
    }

    assert "deny" in run_guard(payload, guard_env)


def test_guard_stops_a_commit_message_with_an_email(guard_env: dict[str, str]) -> None:
    payload = {"tool_name": "Bash", "tool_input": {"command": f"git commit -m 'fix by {EMAIL}'"}}

    assert "deny" in run_guard(payload, guard_env)


def test_guard_reads_the_pr_body_file(guard_env: dict[str, str], tmp_path: Path) -> None:
    body = tmp_path / "body.md"
    body.write_text("レビュー: 架空太郎", encoding="utf-8")
    payload = {
        "tool_name": "Bash",
        "tool_input": {"command": f"gh pr create --title x --body-file '{body.as_posix()}'"},
    }

    assert "deny" in run_guard(payload, guard_env)


def test_guard_lets_ordinary_work_through(guard_env: dict[str, str]) -> None:
    for payload in (
        {
            "tool_name": "Write",
            "tool_input": {"file_path": "docs/a.md", "content": "公開役が上げる"},
        },
        {
            "tool_name": "Bash",
            "tool_input": {"command": "grep -rn 架空太郎 docs"},
        },  # 公開しないコマンドは見ない
        {
            "tool_name": "Write",
            "tool_input": {"file_path": ".personal-denylist", "content": "架空太郎"},
        },
    ):
        assert run_guard(payload, guard_env) == ""
