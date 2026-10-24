#!/bin/sh
# git のフック (.githooks/) を、この clone で有効にする。clone したら最初に 1 回だけ実行する。
# Claude Code で開いた場合は、起動時に自動で同じ設定をする (.claude/settings.json の SessionStart)。
set -e
root="$(git rev-parse --show-toplevel)"
git -C "$root" config core.hooksPath .githooks
chmod +x "$root"/.githooks/* 2>/dev/null || true
echo "git のフックを有効にしました (core.hooksPath = .githooks)。"
if [ ! -f "$root/.personal-denylist" ] && [ ! -f "$HOME/.config/ueco/personal-denylist" ]; then
  echo "拒否リスト (名前など) がまだありません。docs/personal-info.md の手順で作ってください。"
fi
