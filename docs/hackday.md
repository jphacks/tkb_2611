# Hack Day の進め方

日程: 2026-10-24 (土)・25 (日)、東京B 会場。開始タグは 1 日目の Hacking Time 開始 (10:20)、Code Freeze は 2 日目 15:00、作品提出フォームは 15:30 まで。オンライン審査は 10/26 (月) 8:00 から。

## 事前と当日の境界

評価されるのは開始タグ `hackday-start` 以降の差分だけ (JPHACKS の規定。事前に作ったものを当日の成果として報告するのは禁止)。

| 開始タグより前に入れてよい | 当日に作る |
|---|---|
| backend の土台 (設定・`/api/v1/health`・ミドルウェア)、CI、検査スクリプト | 写真判定のエンドポイントと AI の呼び出し |
| 契約 (`api/openapi.yaml`・例) | 検証層 (確信度での振り分け・画面撮影と文字だけの写真の判定) |
| `docs/`・`CLAUDE.md`・ルール | CO₂e の換算 (#90 の根拠から) |
| テストの土台 (`backend/tests/support/`: 契約の検証・合成の写真・Bedrock と DynamoDB の偽物・写真の漏れの検査、未実装の一覧、本物の AWS に当てるテスト) | 写真判定・みんなの氷山の機能のテスト (土台を使って当日に書く) |
| AWS の資源 (`infra/backend.yaml`: DynamoDB のテーブル・Vercel 用のロール・Nova の呼び出しの権限) | backend から AWS を呼ぶコード (OIDC でロールを借りる・boto3 の呼び出し) |
| | みんなの氷山の集計・節目、会場表示の Web ページ |
| | お題の配信、QR・合言葉の照合 |
| | 評価 (#89 の写真セットでの正解率・確信度の較正・モデル比較) |

迷ったら当日に回す。事前に作ったものを当日の差分に混ぜない。Unity クライアント (project-penguin) は事前に作ったもので、README で「事前に開発」と書く。

## 3 人の分担 (案、project-penguin #92 で確定)

| 担当 | 作るもの | 主なファイル |
|---|---|---|
| 1. 判定 | `POST /v1/eco-photo/judge`、AI の呼び出し (提供元を差し替え可能に)、検証層、`checks` の行 | `backend/src/penguin_backend/api/judge.py`、`judge/` |
| 2. 評価 | 写真セットでの正解率の表、確信度の区間ごとの実際の正解率、モデル比較 (#125)、プロンプトの調整、閾値の決定、README の表 | `eval/` (写真はリポジトリの外)、`docs/evaluation.md` |
| 3. 会場 | みんなの氷山 (`GET /v1/community/goal` と、判定の応答の `community`)、会場表示の Web ページ、お題の配信と照合 | `api/community.py`、`api/quests.py`、`venue/` |

AI の画像解析の入出力そのものは半日で終わる見込み。残りの時間は、審査で「API を呼ぶだけ」にならない部分 (検証層と評価の数字) と、会場で見える部分に回す。

## 当日の流れ

1. **10:20** オーナーが main に注釈付きタグ `hackday-start` を打つ (`git tag -a hackday-start -m "Hack Day 開始" origin/main && git push origin hackday-start`)。タグはルールセットで固定され、付け直せないので、打つ前に `git fetch && git log -1 origin/main` で打つ先を確かめる。
2. 各自 `<type>/<内容>` のブランチで作り、PR → CI → squash マージ。PR の説明に「何を作ったか」を 1 行で書く (README の差分の節に使う)。
3. 公開役が main を Vercel に上げる (`docs/architecture.md` の「公開の流れ」)。最初は `penguin-config.json` を `judge: "mock"` のままにし、判定が動いたら `"http"` にして上げ直す。みんなの氷山とお題も、できたものから `true` にする。
4. **中間発表 (1 日目 19:10)** で QR を配る前に、本番の URL で一周できることを確かめる。
5. **Code Freeze (2 日目 15:00)** までにマージを終える。README の「Hack Day 中に開発した差分」に `hackday-start...main` の比較リンクと主要な PR を並べる。

## 完了の定義 (各機能)

- 契約の例 (`api/examples/`) どおりの形で答える (契約テストが通る)。
- 失敗のとき (AI の遅れ・壊れた応答・大きすぎる画像) に、契約のエラーの形で答える。
- 写真と AI の生の出力がディスク・ログ・応答に残らない。
- 本番の URL で、Unity の画面から使える (`penguin-config.json` で有効にしてある)。
- AWS は Vercel の OIDC で借りたロールで呼ぶ。アクセスキーを Vercel・コード・ログに置かない。最初の公開で `PENGUIN_AWS_ROLE_ARN` のロールを借りられることを確かめる (`infra/README.md`)。

## 事前に決めた当日の実装の決まり (契約 1.1.0)

- **`decision` はサーバーが決める。** AI の出力をそのまま返さない。`subject: unknown` は `accept` (クライアントが「判定できない写真」の少しの資源を渡す)。画面を撮った写真・文字だけの写真は `reject`。
- **使い回し (写真のハッシュ) は扱わない。** アプリはカメラの今のフレームしか撮れない (アルバムやファイルからは選べない)。昔の写真を画面に映して撮る手口は、画面の撮影の判定で止める。
- **CO₂ の量は返さない。** 島ではインフラの素材として見せる (`co2e` は 1.1.0 で契約から外した)。
- **みんなの氷山に数えるのは、`Idempotency-Key` が付いた判定だけ。** そのキーで提出の記録を条件付き Put し、通ったときだけ 10 分割のカウンタに ADD する (project-penguin の ADR 0002)。
- **ゲージの口は `Cache-Control: public, s-maxage=10`。** `poll_seconds` は環境変数で切り替える (会場 15 秒、審査期間 60 秒)。Vercel Hobby は関数の呼び出し・CPU 時間の月の枠を超えると 30 日止まる (`docs/architecture.md`)。
- **写真の本文は 1 MB まで。** 超えたら 413 `image_too_large`。multipart を一時ファイルに退避させない (`.claude/rules/privacy.md`)。
- **`activity` は裏付けにだけ使う。** 写真の `subject` と合うとき (自転車の写真で、今日自転車に乗った記録がある) に確信度を上げる、など。無くても判定は同じように動く (Web 版からは来ない)。

## 当日にやらないこと

- Web 版の Unity のビルドし直し (2026-10-25 から凍結)。iOS 版は、このリポジトリの `unity/` で当日に作る (`decisions.md`)。
- 契約の必須の項目を増やすこと。足す項目は省略可にする (`.claude/rules/api-contract.md`)。
- 新しい依存を相談なしに足すこと。
