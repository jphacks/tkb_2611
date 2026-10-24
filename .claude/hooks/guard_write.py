"""PreToolUse(Write|Edit): 壊れる書き込みと秘密情報の混入をブロックする。

1. Unity が管理する YAML アセットの直接編集 (GUID 参照が壊れる)
2. 生成物ディレクトリへの書き込み
3. .env への書き込みと、ソースへの API キー実値の埋め込み
"""

import json
import os
import re
import sys

# Unity Editor 経由でしか触ってはいけない拡張子 (Force Text で YAML として保存されるもの)。
# 判定は小文字で行う。.gitattributes の merge=unityyaml の行と .editorconfig の
# 「Unity が生成する YAML」の節も同じ集合にそろえる (tests/hooks で一致を検査している)。
UNITY_YAML = {
    ".unity", ".prefab", ".asset", ".meta", ".mat", ".anim", ".controller",
    ".overridecontroller", ".mask", ".playable", ".signal", ".terrainlayer",
    ".physicmaterial", ".physicsmaterial2d", ".lighting", ".spriteatlas",
    ".spriteatlasv2", ".mixer", ".rendertexture", ".preset", ".giparams",
    ".shadervariants", ".guiskin", ".fontsettings", ".flare", ".brush",
}
# Unity / IDE が再生成するもの。編集しても次のリコンパイルで消える。
#
# .gitignore と同じく「リポジトリ直下にあるときだけ生成物」として扱う。
# どの階層でも名前だけで判定すると Assets/Editor/Build/ のようなソース
# ディレクトリを誤って生成物と見なしてしまう。
GENERATED_ROOT_DIRS = (
    "library", "temp", "logs", "build", "builds",
    "usersettings", "memorycaptures", "recordings",
)
# 名前がその用途にしか使われないため、どの階層でも生成物と見てよいもの。
GENERATED_ANY_DIRS = ("obj", "__pycache__", ".venv", "node_modules")
GENERATED_EXT = {".csproj", ".sln", ".slnx", ".unityproj", ".pidb", ".user"}

# 実キーらしき文字列。.env.example のような空値やプレースホルダには当てない。
SECRET_PATTERNS = [
    (re.compile(r"AIza[0-9A-Za-z_\-]{30,}"), "Google/Gemini API キー"),
    (re.compile(r"sk-or-v1-[0-9a-f]{32,}"), "OpenRouter API キー"),
    (re.compile(r"sk-ant-[A-Za-z0-9_\-]{32,}"), "Anthropic API キー"),
    (re.compile(r"sk-(?:proj-)?[A-Za-z0-9_\-]{32,}"), "OpenAI 形式の API キー"),
    (
        re.compile(
            r"(?i)(?:GEMINI|OPENROUTER|OPENAI|ANTHROPIC|GOOGLE)_API_KEY\s*[:=]\s*"
            r"[\"']?(?!\s*$)(?!your[_-]|<|\$\{|\.\.\.)[A-Za-z0-9_\-]{20,}"
        ),
        "API キーの実値",
    ),
]


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


def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return

    tool_input = payload.get("tool_input") or {}
    path = tool_input.get("file_path") or ""
    if not path:
        return

    norm = path.replace("\\", "/")
    segments = norm.split("/")
    display = segments[-1]
    name = display.lower()
    ext = os.path.splitext(name)[1]

    # 生成物の判定はリポジトリ直下からの相対パスで行う。
    cwd = (payload.get("cwd") or "").replace("\\", "/").rstrip("/")
    rel = norm
    if cwd and norm.lower().startswith(cwd.lower() + "/"):
        rel = norm[len(cwd) + 1:]
    rel_dirs = [p.lower() for p in rel.split("/")[:-1] if p not in ("", ".")]

    if ext in UNITY_YAML:
        deny(
            f"`{display}` は Unity が管理する YAML です。テキストとして書き換えると "
            "GUID 参照が壊れて、シーンやプレハブの参照が全部外れます。"
            "Unity Editor 上での操作手順を提示してください。"
        )

    is_generated = (
        ext in GENERATED_EXT
        or (rel_dirs and rel_dirs[0] in GENERATED_ROOT_DIRS)
        or any(p in GENERATED_ANY_DIRS for p in rel_dirs)
    )
    if is_generated:
        deny(
            f"`{path}` は Unity / IDE が再生成する生成物です。編集しても次のリコンパイルで失われます。"
            "変更したい設定の本体 (asmdef, Packages/manifest.json, ProjectSettings) を特定してください。"
        )

    if name == ".env" or (name.startswith(".env.") and name != ".env.example"):
        deny(
            "`.env` は実キーを置く場所であり、Claude は書き換えません。"
            "必要な変更は `.env.example` にプレースホルダとして反映し、実値の記入はユーザーに依頼してください。"
        )

    content = tool_input.get("content") or tool_input.get("new_string") or ""
    for pattern, label in SECRET_PATTERNS:
        if pattern.search(content):
            deny(
                f"書き込もうとしている内容に {label} らしき文字列が含まれています。"
                "public リポジトリのため、コミットされると履歴に残って回復できません。"
                "キーは backend が環境変数から読む形にしてください。"
            )

    return


if __name__ == "__main__":
    main()
