"""PreToolUse: 個人に関わる内容 (名前・連絡先) がこのリポジトリに入るのを、書く前に止める。

- Edit / Write / MultiEdit: 書き込む中身を見る
- Bash: コミットメッセージ・タグ・PR や Issue の本文になる部分 (コマンドの文字列と、--body-file などで渡すファイル) を見る

検出の規則は scripts/check_personal_info.py と同じものを使う (拒否リストもそちらの読み方に従う)。
git のフック (.githooks/) と CI が後ろに控えているが、push したものは消せないので、ここで先に止める。
"""

from __future__ import annotations

import importlib.util
import json
import re
import shlex
import sys
from pathlib import Path
from types import ModuleType

ROOT = Path(__file__).resolve().parents[2]

# 本文を外に出すコマンド。これ以外の Bash は見ない (ログの grep などで誤って止めないため)。
PUBLISHING = re.compile(r"\bgit\s+(commit|tag|notes)\b|\bgh\s+(pr|issue|release)\s+(create|edit|comment)\b")
BODY_FILE_FLAGS = {"-F", "--file", "--body-file", "--notes-file"}


def checker() -> ModuleType:
    path = ROOT / "scripts" / "check_personal_info.py"
    spec = importlib.util.spec_from_file_location("check_personal_info", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules["check_personal_info"] = module  # dataclass が自分のモジュールを引けるように
    spec.loader.exec_module(module)
    return module


def deny(reason: str) -> None:
    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": reason,
            }
        },
        sys.stdout,
        ensure_ascii=False,
    )
    sys.exit(0)


def texts_to_check(tool: str, tool_input: dict[str, object]) -> list[tuple[str, str]]:
    if tool in ("Edit", "Write", "MultiEdit"):
        where = str(tool_input.get("file_path", "書き込み"))
        if Path(where).name == ".personal-denylist":
            return []  # 拒否リストそのもの (.gitignore 済み) には書いてよい
        parts = [str(tool_input.get(k, "")) for k in ("content", "new_string")]
        for edit in tool_input.get("edits", []) or []:
            if isinstance(edit, dict):
                parts.append(str(edit.get("new_string", "")))
        return [(where, "\n".join(parts))]

    if tool == "Bash":
        command = str(tool_input.get("command", ""))
        if not PUBLISHING.search(command):
            return []
        found = [("コマンド", command)]
        try:
            tokens = shlex.split(command)
        except ValueError:
            tokens = command.split()
        for flag, value in zip(tokens, tokens[1:]):
            if flag in BODY_FILE_FLAGS and value != "-":
                path = Path(value)
                if path.is_file():
                    found.append((path.name, path.read_text(encoding="utf-8", errors="replace")))
        return found
    return []


def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError:
        return
    tool = str(payload.get("tool_name", ""))
    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return

    targets = texts_to_check(tool, tool_input)
    if not targets:
        return

    module = checker()
    deny_patterns = module.denylist_patterns(module.load_denylist(ROOT))
    findings = []
    for where, text in targets:
        findings += list(module.scan_text(text, where, deny_patterns))
    if findings:
        kinds = "、".join(dict.fromkeys(f.kind for f in findings))
        deny(
            f"個人に関わる内容らしきもの ({kinds}) が含まれています。このリポジトリは public で残り続け、"
            "push したものは消せません。人は役割 (「公開役」「担当 A」など) で書き、連絡先は書かないでください"
            " (CLAUDE.md の絶対規則 0)。"
        )


if __name__ == "__main__":
    main()
