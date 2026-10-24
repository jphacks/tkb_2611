"""個人に関わる内容 (名前・連絡先など) がリポジトリに入るのを止める。標準ライブラリだけで動く。

このリポジトリは jphacks の org に public で残り続け、消せない前提で扱う (CLAUDE.md の絶対規則 0)。
一度 push したものは PR の履歴から消せないので、git のフック (.githooks/) と Claude Code のフックで
push の前に止め、CI で最後に確かめる。

見るもの:
- 決まった形の個人の情報: メールアドレス (noreply・example を除く)、電話番号、郵便番号
- 拒否リスト: チームの人の名前・ニックネーム・アカウント名など。**リストはリポジトリに入れない。**
  次の順に読み、全部を合わせて使う。
    1. 環境変数 PENGUIN_PERSONAL_DENYLIST (改行かカンマ区切り。CI は GitHub の Secret から渡す)
    2. リポジトリ直下の .personal-denylist (.gitignore 済み)
    3. ~/.config/ueco/personal-denylist
  英数字だけの語は単語として、それ以外 (日本語の名前など) は部分一致で探す。大文字と小文字は区別しない。

    python3 scripts/check_personal_info.py --staged            # pre-commit: ステージした追加行
    python3 scripts/check_personal_info.py --message-file F    # commit-msg: コミットメッセージ
    python3 scripts/check_personal_info.py --range A..B        # pre-push / CI: 範囲のコミットの追加行とメッセージ
    python3 scripts/check_personal_info.py --text-file F       # PR の本文など
    python3 scripts/check_personal_info.py --all               # 追跡しているファイル全部

見つかったときは、どこに何の種類があったかだけを出す (見つかった語そのものは出さない。CI のログも public のため)。
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from collections.abc import Iterable, Iterator
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DENYLIST_FILE = ".personal-denylist"
HOME_DENYLIST = Path.home() / ".config" / "ueco" / "personal-denylist"
ENV_DENYLIST = "PENGUIN_PERSONAL_DENYLIST"

# 生成物・バイナリは見ない (依存のロックはハッシュの数字の並びで電話番号に誤って当たる)。
SKIP_PATHS = re.compile(r"(^|/)(uv\.lock|package-lock\.json)$|\.(png|jpe?g|gif|webp|ico|pdf|zip|gz)$")

EMAIL = re.compile(r"(?<![\w.+-])[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}(?![\w-])")
# 公開してよいアドレス (ボットやツールの noreply、例示用のドメイン)。
EMAIL_ALLOWED = re.compile(
    r"noreply|no-reply|@example\.(com|org|net)$|@users\.noreply\.github\.com$", re.IGNORECASE
)
PATTERNS: list[tuple[re.Pattern[str], str]] = [
    # 携帯 (070/080/090) と固定電話 (市外局番つき、ハイフン区切り)。
    (re.compile(r"(?<![0-9A-Za-z])0[789]0-?\d{4}-?\d{4}(?![0-9A-Za-z])"), "電話番号"),
    (re.compile(r"(?<![0-9A-Za-z])0\d{1,4}-\d{1,4}-\d{4}(?![0-9A-Za-z])"), "電話番号"),
    (re.compile(r"〒\s*\d{3}-?\d{4}"), "郵便番号"),
]


@dataclass(frozen=True)
class Finding:
    where: str
    kind: str


def load_denylist(root: Path = ROOT) -> list[str]:
    words: list[str] = []
    sources: list[str] = [os.environ.get(ENV_DENYLIST, "")]
    for path in (root / DENYLIST_FILE, HOME_DENYLIST):
        if path.is_file():
            sources.append(path.read_text(encoding="utf-8"))
    for source in sources:
        for raw in re.split(r"[\n,]", source):
            word = raw.strip()
            if word and not word.startswith("#"):
                words.append(word)
    return sorted(set(words), key=len, reverse=True)


def denylist_patterns(words: Iterable[str]) -> list[re.Pattern[str]]:
    patterns = []
    for word in words:
        escaped = re.escape(word)
        if word.isascii():
            # 英数字の語は単語として探す (「Shin」で「shine」に当てない)。
            patterns.append(re.compile(rf"(?<![A-Za-z0-9]){escaped}(?![A-Za-z0-9])", re.IGNORECASE))
        else:
            patterns.append(re.compile(escaped, re.IGNORECASE))
    return patterns


def scan_text(text: str, where: str, deny: list[re.Pattern[str]]) -> Iterator[Finding]:
    for number, line in enumerate(text.splitlines(), start=1):
        at = f"{where}:{number}"
        for match in EMAIL.finditer(line):
            if not EMAIL_ALLOWED.search(match.group(0)):
                yield Finding(at, "メールアドレス")
        for pattern, kind in PATTERNS:
            if pattern.search(line):
                yield Finding(at, kind)
        if any(p.search(line) for p in deny):
            yield Finding(at, "拒否リストの語 (名前など)")


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=ROOT, capture_output=True, text=True, check=True, encoding="utf-8"
    ).stdout


def added_lines(diff: str) -> Iterator[tuple[str, int, str]]:
    """unified diff (-U0) から (ファイル, 行番号, 追加された行) を取り出す。"""
    path = ""
    line_no = 0
    for line in diff.splitlines():
        if line.startswith("+++ "):
            path = line[6:] if line.startswith("+++ b/") else ""
        elif line.startswith("@@"):
            m = re.search(r"\+(\d+)", line)
            line_no = int(m.group(1)) if m else 0
        elif line.startswith("+") and not line.startswith("+++") and path:
            yield path, line_no, line[1:]
            line_no += 1


def scan_diff(diff: str, deny: list[re.Pattern[str]], label: str = "") -> Iterator[Finding]:
    for path, line_no, text in added_lines(diff):
        if SKIP_PATHS.search(path):
            continue
        for f in scan_text(text, f"{label}{path}", deny):
            yield Finding(f"{label}{path}:{line_no}", f.kind)


def check_tracked_denylist() -> list[Finding]:
    tracked = git("ls-files", "--", DENYLIST_FILE).strip()
    return [Finding(DENYLIST_FILE, "拒否リストそのものが追跡されている")] if tracked else []


def scan_staged(deny: list[re.Pattern[str]]) -> list[Finding]:
    return [*check_tracked_denylist(), *scan_diff(git("diff", "--cached", "-U0", "--no-color"), deny)]


def scan_range(rev_range: str, deny: list[re.Pattern[str]]) -> list[Finding]:
    findings: list[Finding] = []
    commits = git("rev-list", "--reverse", rev_range).split()
    for commit in commits:
        short = commit[:8]
        message = git("log", "-1", "--format=%B", commit)
        findings += scan_text(message, f"コミット {short} のメッセージ", deny)
        diff = git("show", "--format=", "-U0", "--no-color", commit)
        findings += scan_diff(diff, deny, label=f"{short} ")
    return findings


def scan_all(deny: list[re.Pattern[str]]) -> list[Finding]:
    findings = check_tracked_denylist()
    for path in git("ls-files").splitlines():
        if SKIP_PATHS.search(path):
            continue
        file = ROOT / path
        try:
            text = file.read_text(encoding="utf-8")
        except (UnicodeDecodeError, FileNotFoundError, IsADirectoryError):
            continue
        findings += scan_text(text, path, deny)
    return findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="個人に関わる内容がリポジトリに入るのを止める。")
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--staged", action="store_true", help="ステージした追加行")
    mode.add_argument("--message-file", type=Path, help="コミットメッセージのファイル")
    mode.add_argument("--range", dest="rev_range", help="コミットの範囲 (A..B)")
    mode.add_argument("--text-file", type=Path, help="任意の文 (PR の本文など)")
    mode.add_argument("--all", action="store_true", help="追跡しているファイル全部")
    parser.add_argument(
        "--require-denylist", action="store_true", help="拒否リストが無ければ失敗にする (CI 用)"
    )
    args = parser.parse_args(argv)

    words = load_denylist()
    if not words:
        message = (
            "拒否リスト (名前など) がありません。形の決まった情報 (メールアドレス・電話番号) だけを見ます。"
            f" {DENYLIST_FILE} か ~/.config/ueco/personal-denylist を作ってください (docs/personal-info.md)。"
        )
        if args.require_denylist:
            print(message, file=sys.stderr)
            return 1
        print(f"注意: {message}", file=sys.stderr)
    deny = denylist_patterns(words)

    if args.staged:
        findings = scan_staged(deny)
    elif args.message_file is not None:
        text = args.message_file.read_text(encoding="utf-8")
        # git が付ける注釈の行 (# から始まる) は見ない。
        body = "\n".join(line for line in text.splitlines() if not line.startswith("#"))
        findings = list(scan_text(body, "コミットメッセージ", deny))
    elif args.rev_range is not None:
        findings = scan_range(args.rev_range, deny)
    elif args.text_file is not None:
        findings = list(scan_text(args.text_file.read_text(encoding="utf-8"), args.text_file.name, deny))
    else:
        findings = scan_all(deny)

    if findings:
        print("個人に関わる内容らしきものがあります (CLAUDE.md の絶対規則 0):", file=sys.stderr)
        for f in dict.fromkeys(findings):
            print(f"  {f.where}: {f.kind}", file=sys.stderr)
        print(
            "このリポジトリは public で残り続け、push したものは消せません。役割 (「公開役」など) で書き直してください。",
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
