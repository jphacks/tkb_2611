"""コミット済みファイルに秘密情報が混入していないか検査する。

検出パターンは .claude/hooks/guard_write.py と共有する。エージェントが書き込む
直前に止めるのが hook、人手や別ツール経由で入ったものを CI で拾うのがこちら。

    uv run --no-project python scripts/check_secrets.py
    uv run --no-project python scripts/check_secrets.py --path Builds/vercel  # 公開する前の成果物

--path を付けると、git の追跡対象ではなく指定したディレクトリの中の全ファイルを見る
(scripts/deploy_web.py が公開する直前に呼ぶ)。Brotli で縮めた Unity の成果物 (.unityweb) は
中身を読めないので見ていない。クライアントにキーを置かない規則 (絶対規則 2) は、
コミット前のソースの検査とフックで守る。
"""

from __future__ import annotations

import argparse
import importlib.util
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# コミットされていること自体が事故になるファイル。
FORBIDDEN_PATHS = re.compile(
    r"(^|/)\.env$"                      # 実キーの置き場
    r"|(^|/)\.env\.(?!example$)[^/]+$"  # .env.local など (.env.example は許可)
    r"|\.(db|sqlite|sqlite3)(-(journal|wal|shm))?$"  # 購買履歴が入るローカル DB と付随ファイル
    r"|\.(pem|key|p12|pfx|keystore|jks|mobileprovision)$"
    r"|(^|/)secrets\.json$"
    r"|(^|/)ApiKeys\.cs$"
)

# 中身を走査しても意味がないバイナリ / LFS 管理対象。
SKIP_CONTENT = re.compile(
    r"\.(png|jpg|jpeg|psd|tga|tif|tiff|bmp|gif|exr|hdr|cubemap"
    r"|fbx|blend|obj|dae|3ds|max|ma|mb"
    r"|wav|mp3|ogg|aif|aiff|mp4|mov|webm"
    r"|unitypackage|dll|so|ttf|otf|unityweb|wasm)$",
    re.IGNORECASE,
)


def load_secret_patterns() -> list[tuple[re.Pattern[str], str]]:
    hook = ROOT / ".claude" / "hooks" / "guard_write.py"
    spec = importlib.util.spec_from_file_location("guard_write", hook)
    if spec is None or spec.loader is None:
        raise SystemExit(f"パターン定義を読み込めません: {hook}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.SECRET_PATTERNS


def tracked_files() -> list[str]:
    out = subprocess.run(["git", "ls-files", "-z"], capture_output=True, text=True, check=True)
    return [p for p in out.stdout.split("\0") if p]


def files_under(directory: Path) -> list[str]:
    """directory の中の全ファイル。表示が読みやすいよう、リポジトリの中ならルートからの相対にする。"""
    base = directory.resolve()
    shown = base.relative_to(ROOT) if base.is_relative_to(ROOT) else base
    return sorted(
        (shown / p.relative_to(base)).as_posix() for p in base.rglob("*") if p.is_file()
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="秘密情報の混入を検査する。")
    parser.add_argument("--path", type=Path, help="git の追跡対象の代わりに検査するディレクトリ")
    args = parser.parse_args(argv)

    if args.path is not None and not args.path.is_dir():
        print(f"ディレクトリがありません: {args.path}", file=sys.stderr)
        return 2

    patterns = load_secret_patterns()
    errors: list[str] = []
    paths = tracked_files() if args.path is None else files_under(args.path)

    for path in paths:
        if FORBIDDEN_PATHS.search(path):
            errors.append(f"  コミット禁止のファイル: {path}")
            continue
        if SKIP_CONTENT.search(path):
            continue
        try:
            text = (ROOT / path).read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for pattern, label in patterns:
            match = pattern.search(text)
            if match:
                line = text[: match.start()].count("\n") + 1
                errors.append(f"  {label} らしき文字列: {path}:{line}")
                break

    if errors:
        print("秘密情報の検査に失敗しました:\n", file=sys.stderr)
        print("\n".join(errors), file=sys.stderr)
        print(
            "\npublic リポジトリです。一度コミットされたキーは履歴に残り、"
            "削除しても回復できません。該当キーは失効させて再発行してください。",
            file=sys.stderr,
        )
        return 1

    target = "追跡対象ファイル" if args.path is None else str(args.path)
    print(f"OK: {target}に秘密情報らしき文字列はありません。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
