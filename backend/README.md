# backend

Project Penguin のバックエンド (FastAPI)。Unity クライアントから受け取ったエコ写真を外部の AI で判定し、写っているもの・検証の結果・CO₂e の目安を返す。みんなの氷山とお題の配信も受け持つ。やり取りの形は `../api/openapi.yaml` が正本。

**いまあるのは土台と疎通の確認 (`GET /api/v1/health`) だけです。** 写真判定・みんなの氷山・お題は Hack Day に作ります (`../docs/hackday.md`)。

## 公開先

Vercel に、Unity の Web ビルドと同じ URL で置きます (`/` が Web ビルド、`/api/*` がこのアプリ)。**ルートはすべて `/api/` の下に置いてください** (それ以外の道は Web ビルドの静的ファイルと重なります)。公開の流れは `../docs/architecture.md`。

## セットアップ

パッケージマネージャは [uv](https://docs.astral.sh/uv/)。

```bash
cd backend
uv sync
uv run uvicorn penguin_backend.app:app --reload   # http://localhost:8000/api/v1/health
```

設定は `PENGUIN_` で始まる環境変数から読みます (`src/penguin_backend/settings.py`)。手元では見本のファイル (`.env.example`) を写して使います。写したファイルは `.gitignore` 済みです。

| 変数 | 用途 |
|---|---|
| `PENGUIN_<AI>_API_KEY` | 判定に使う AI のキー (例: `PENGUIN_GEMINI_API_KEY`)。AI が決まったら `Settings` と見本のファイルに足す |
| `PENGUIN_CORS_ORIGINS` | 手元で Web ビルドを別のポートから開いて呼ぶときだけ、許可する配信元をカンマ区切りで渡す (例: `http://localhost:8090`)。本番は空 |

public リポジトリなので、実キーのコミットは回復できない事故になります (履歴に残る)。

## 開発コマンド

| 目的 | コマンド |
|---|---|
| 整形 | `uv run ruff format .` |
| lint | `uv run ruff check .` |
| 型検査 | `uv run mypy .` (strict) |
| テスト | `uv run pytest` |

CI でも同じものが回ります (`../.github/workflows/ci.yml`)。ruff の `S` (bandit) と `T20` (`print` の禁止) は、写真の情報を標準出力・ログに漏らさないために有効にしています。

## 守ること

実装の規約は `../.claude/rules/backend.md`、写真の扱いは `../.claude/rules/privacy.md`。要点:

- 写真と AI の生の出力をディスク・ログ・応答に残さない。
- AI の応答は Pydantic で検証してから使う。写真の表に無い答えは `unknown`。
- 応答は契約の形だけ。エラーは `{"error": {"code", "message", "retryable"}}`。
- エンドポイントの認証は無い。クライアントに固定のシークレットを埋める方式は対策にならないので実装しない。
