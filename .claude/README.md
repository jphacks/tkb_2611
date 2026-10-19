# .claude/

Claude Code (と互換のエージェント) 向けの設定。**個人の設定は `settings.local.json` に書く** (コミットしない)。Codex などは `../AGENTS.md` から入る。

| ファイル | 役割 | 効くとき |
|---|---|---|
| `../CLAUDE.md` | 前提・絶対規則・コマンド | 毎セッションの開始時 |
| `rules/privacy.md` | 写真・AI の出力・API キーの扱い | `backend/`・`api/` を読んだとき |
| `rules/backend.md` | FastAPI の構成と規約 | `backend/` を読んだとき |
| `rules/api-contract.md` | 接続は先に契約に書く・前方互換 | `api/`・`backend/` を読んだとき |
| `skills/verify/SKILL.md` | CI と同じ検査 | `/verify`、コミット・PR の前 |
| `settings.json` | permissions と hooks。起動時に git のフック (`.githooks/`) を有効にする | 毎セッション |
| `hooks/guard_write.py` | 秘密情報らしき文字列と生成物への書き込みを止める | Edit / Write の直前 |
| `hooks/guard_personal.py` | 個人に関わる内容 (名前・連絡先) を、書く前・コミットや PR の前に止める (`docs/personal-info.md`) | Edit / Write、`git commit`・`gh pr create` などの直前 |
| `hooks/japanese-guard.py` | 最終回答が英語主体なら日本語で書き直させる | 応答の終了時 |

`CLAUDE.md` は短く保ち、領域ごとの細かい決まりは `rules/` に分けている。`CLAUDE.md` の記述には強制力が無いので、**確実に止めたいものは permissions か hooks に書く**。

permissions の考え方: 読み取りと検査は確認なしで通す。commit・push・タグ・PR の作成とマージ・依存の追加は毎回確認する (提出用の public リポジトリで、開始タグや main の履歴は審査に使われるため)。force push・タグの削除・`.env` の読み取りは止める。

フックの元は project-penguin の `.claude/hooks/` (`guard_write.py` は `scripts/check_secrets.py` と検出のパターンを共有している)。
