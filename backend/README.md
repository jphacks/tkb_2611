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
| `PENGUIN_AWS_ROLE_ARN` | Vercel の OIDC で借りる IAM ロール (`../infra/backend.yaml` の `VercelBackendRoleArn`)。AI のキーは置かない。手元では空のままにし、`AWS_PROFILE` の認証情報を使う |
| `PENGUIN_COMMUNITY_TABLE` | みんなの氷山のテーブル。本番は `penguin-backend-community`、手元と preview は `penguin-backend-community-dev` |
| `PENGUIN_CORS_ORIGINS` | 手元で Web ビルドを別のポートから開いて呼ぶときだけ、許可する配信元をカンマ区切りで渡す (例: `http://localhost:8090`)。本番は空 |

public リポジトリなので、実キーのコミットは回復できない事故になります (履歴に残る)。

## 開発コマンド

| 目的 | コマンド |
|---|---|
| 整形 | `uv run ruff format .` |
| lint | `uv run ruff check .` |
| 型検査 | `uv run mypy .` (strict) |
| テスト | `uv run pytest` (`-rs` で未実装の契約の一覧も出る) |
| 本物の AWS に当てるテスト | `PENGUIN_TEST_AWS=1 AWS_PROFILE=penguin uv run pytest -m aws` (費用は 1 回 1 円未満) |

CI でも同じものが回ります (`../.github/workflows/ci.yml`)。ruff の `S` (bandit) と `T20` (`print` の禁止) は、写真の情報を標準出力・ログに漏らさないために有効にしています。

## テストの回し方 (SDD → TDD)

仕様は契約 (`../api/openapi.yaml` と `../api/examples/`)。**契約 → 落ちるテスト → 実装 → 通る** の順に回す。AWS (Bedrock・DynamoDB) は偽物に差し替えるので、手元でも CI でも本物を呼ばない。

1. **契約に書く。** 足す・変える応答やエラーの code は、先に `openapi.yaml` と例に書く (`../.claude/rules/api-contract.md`)。
2. **未実装の一覧を見る。** `uv run pytest -rs` の skip の理由に、まだ道が無い操作 (`未実装: POST /api/v1/eco-photo/judge` など) が並ぶ。
3. **落ちるテストを書く。** 応答の形は `assert_matches_contract`、エラーは `assert_error` で 1 行で確かめる。
4. **偽物で振る舞いを決める。** Bedrock は `bedrock` フィクスチャ (Stubber)、DynamoDB は `community_tables` フィクスチャ (moto。キーと TTL は `../infra/backend.yaml` から読む)。
5. **実装して通す。** `pytest`・`ruff`・`mypy` を通す。
6. **本物で 1 回確かめる。** 偽物と本物の振る舞いの差が気になるときだけ、`PENGUIN_TEST_AWS=1` で `tests/aws/` を動かす。

**本番のコードは boto3 のクライアントを外から受け取る形にする** (`create_app(settings, clients=...)` や FastAPI の `Depends` など)。関数の中で `boto3.client(...)` を直に作ると、テストで偽物に差し替えられない。

### 道具 (`tests/support/`)

| モジュール | 使うもの |
|---|---|
| `contract.py` | `assert_matches_contract(response)`・`assert_error(response, code)`・`example("judge.200.accept")`・`validate("JudgeResult", body)` |
| `photos.py` | `jpeg(marker=...)` (合成の写真。実在の写真は使わない)・`too_large()`・`png()`・`broken_jpeg()`・`multipart(data)` |
| `bedrock.py` | `converse_tool_use(judge_input(...))`・`converse_text(...)`・失敗の定数 (`THROTTLED`・`MODEL_TIMEOUT`・`UNAVAILABLE`…) |
| `dynamodb.py` | `table_specs()` (テンプレートのテーブルの形) |
| `privacy.py` | `assert_no_leak(marker, response, caplog)`・`no_disk_writes` フィクスチャ (写真をディスクに書いたら失敗) |

### 最初に書くテストの例

写真判定 (契約の例 `judge.200.accept` の形で答える):

```python
def test_bottle_photo_is_accepted(bedrock, no_disk_writes, caplog):
    bedrock.respond(converse_tool_use(judge_input("my_bottle", confidence=0.95)))
    client = TestClient(create_app(Settings(), clients=Clients(bedrock=bedrock.client)))

    response = client.post("/api/v1/eco-photo/judge", files=multipart(jpeg(marker="leak-1")))

    assert_matches_contract(response)
    assert response.json()["subject"] == "my_bottle"
    assert_no_leak("leak-1", response, caplog)


def test_throttled_model_answers_upstream_unavailable(bedrock):
    bedrock.fail(THROTTLED)
    ...
    assert_error(response, "upstream_unavailable")
```

みんなの氷山 (同じ提出の再送を 2 回数えない、ADR 0002):

```python
def test_resent_submission_is_counted_once(community_tables, dynamodb):
    counter = CommunityCounter(dynamodb, table=community_tables["CommunityTable"])

    counter.add("goal-1", submission_id="abc")
    counter.add("goal-1", submission_id="abc")

    assert counter.current("goal-1") == 1
```

`Clients`・`CommunityCounter` は例の名前で、形は当日決める。

## 守ること

実装の規約は `../.claude/rules/backend.md`、写真の扱いは `../.claude/rules/privacy.md`。要点:

- 写真と AI の生の出力をディスク・ログ・応答に残さない。
- AI の応答は Pydantic で検証してから使う。写真の表に無い答えは `unknown`。
- 応答は契約の形だけ。エラーは `{"error": {"code", "message", "retryable"}}`。
- エンドポイントの認証は無い。クライアントに固定のシークレットを埋める方式は対策にならないので実装しない。
