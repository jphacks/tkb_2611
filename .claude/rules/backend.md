---
paths:
  - "backend/**"
---

# backend (FastAPI) ルール

データの扱いは `.claude/rules/privacy.md`、契約は `.claude/rules/api-contract.md` が優先。ここは実装の規約。

## 構成の目安

```
backend/src/penguin_backend/
├─ app.py          create_app()。ミドルウェア (CORS・処理されない例外の 500) とルートの登録だけ
├─ settings.py     環境変数 (PENGUIN_*) の読み込み
├─ api/            ルート。契約のパスごとに 1 ファイル (judge.py・community.py・quests.py)
├─ schemas.py      契約の本文の Pydantic モデル (api/openapi.yaml と同じ名前・同じ制約)
├─ judge/          写真判定: AI の呼び出し (提供元ごとに差し替え可能に)・応答の検証・検証層 (#112)
└─ errors.py       エラーの形 {"error": {code, message, retryable}} と code の一覧
```

- **ルートはすべて `/api/` の下。** Vercel では同じ URL に Unity の Web ビルドが載っていて、それ以外の道は静的ファイルと重なる。契約のパス `/v1/...` は実際には `/api/v1/...`。
- **AI の提供元は差し替えられるようにする** (今は Amazon Bedrock の Nova 2 Lite、project-penguin の ADR 0001)。判定の中身 (`judge/`) は提供元を知らない形で書き、提供元のクラスだけを設定で切り替える。
- エラーは `errors.py` の 1 か所で作る。code は契約の `ErrorCode` だけを使い、HTTP の状態と `retryable` は契約の表に合わせる。429・503 には `Retry-After` を付ける。
- 応答には `Cache-Control: no-store` を付ける。例外は `GET /v1/community/goal` だけで、`public, s-maxage=10` (契約の `CacheControlShared`)。`Date` は付いたまま返す (クライアントの時計合わせに使う)。
- **`decision` はサーバーが決める。** AI には写っているもの・`subject`・確信度・画面の撮影らしさだけを返させる。`subject: unknown` は `accept`、画面を撮った写真・文字だけの写真は `reject`。
- **みんなの氷山に数えるのは、`Idempotency-Key` が付いた判定だけ。** そのキーで提出の記録を条件付き Put し、通ったときだけカウンタに ADD する (ADR 0002)。

## 外部の AI

- **応答は必ず Pydantic で検証してから使う。** 生の JSON を下流に流さない。構造化出力 (JSON スキーマの指定) を使い、それでも検証してから使う。
- 写真の表 (`EcoSubjectId`) に無い答えは `unknown` にする。確信度は 0〜1 に収める。
- タイムアウトを必ず付ける (クライアントは 25 秒で切るので、AI の呼び出しは 15 秒前後まで)。上限に当たった・落ちたときは `upstream_unavailable` (503、retryable)。
- 思考 (reasoning) のトークンは最小にする。画像の解像度とトークン数を記録して費用を見積もれるようにする (記録するのは数だけ)。

## 数値と表

- CO₂e の換算・確信度の閾値はコードに散らさず、出典や根拠を併記したデータとして 1 か所にまとめる。**根拠のない係数を作らない。** 出典が無ければ値を置かずにユーザーに確かめる (換算の根拠は project-penguin #90)。
- 写真の表の ID は Unity 側の `EcoSubjects` と同じ。増やすときは契約と Unity の両方を直す (`api-contract.md`)。

## 規約

- 型ヒントを必ず付ける (`mypy --strict`)。`Any` を逃げ道にしない。
- 設定は `Settings` (pydantic-settings) から読む。`os.getenv` を散らさない。
- 例外は握りつぶさない。ただし例外の文に写真や AI の出力を入れない。
- ruff の `S` (bandit) と `T20` (`print` の禁止) は外さない。写真の情報が標準出力・ログに漏れるのを機械的に止めるため。

## テスト

- AI と DynamoDB は偽物にする (実際の AWS を CI から呼ばない)。道具は `backend/tests/support/`、回し方は `backend/README.md` の「テストの回し方 (SDD → TDD)」。
  - Bedrock は `bedrock` フィクスチャ (botocore の Stubber)、DynamoDB は `community_tables` フィクスチャ (moto、形は `infra/backend.yaml` から読む)。
  - 本番のコードは boto3 のクライアントを外から受け取る。関数の中で `boto3.client(...)` を直に作らない (偽物に差し替えられなくなる)。
- **契約テスト**: 応答は `assert_matches_contract`、エラーは `assert_error` で、契約のスキーマと code の表に照らして確かめる。
- 失敗の場合 (AI のタイムアウト・スロットリング・壊れた JSON・表に無い答え・大きすぎる画像・JPEG でない画像) を必ず書く。
- 写真を受け取る道のテストには、`no_disk_writes` フィクスチャと `assert_no_leak` を付ける (`privacy.md` を機械的に確かめる)。
- 本物の AWS に当てるテストは `tests/aws/` に置いて印 `aws` を付ける。`PENGUIN_TEST_AWS=1` のときだけ動く。

## 依存

`pyproject.toml` と `uv.lock` が正。**新しい依存は理由を添えて提案してから足す** (`uv add` は確認が出る)。Vercel の関数の大きさの上限があるので、重い依存 (画像処理・機械学習の大きなライブラリ) は避ける。
