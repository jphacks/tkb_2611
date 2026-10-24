# AGENTS.md

このリポジトリで作業するエージェント (Codex など) 向けの入口。**前提・規則・コマンドは `CLAUDE.md` が正本** なので、作業を始める前に必ず読むこと。ここには要点だけを書く。

- 返答は日本語で書く。
- **作業を始める前に `sh scripts/setup_hooks.sh` を 1 回実行する** (git のフックで、個人に関わる内容をコミット・push の前に止める。`docs/personal-info.md`)。フックを外したり `--no-verify` で通したりしない。
- **個人に関わる内容 (人の名前・連絡先・個人の事情・チームの会話の中身) を、コード・docs・コミットメッセージ・PR の本文のどこにも書かない。** 役割で書く。このリポジトリは public で残り続ける。
- 開始タグ `hackday-start` (2026-10-24 10:20) より前に、写真判定・みんなの氷山・お題のエンドポイントの中身を書かない (`docs/hackday.md`)。
- 接続を足す・変えるときは、先に `api/openapi.yaml` と `api/examples/` に書く (`.claude/rules/api-contract.md`)。
- 写真と AI の生の出力をディスク・ログ・応答に残さない。API キーは環境変数だけ (`.claude/rules/privacy.md`)。
- backend の規約は `.claude/rules/backend.md`。コミット・PR の前に `.claude/skills/verify/SKILL.md` の検査を通す。
- commit・push・PR はユーザーが指示したときだけ。`main` に直接 push しない。
