# 構成

## 全体

```
スマホのブラウザ
   │  https://project-penguin-nu.vercel.app
   ▼
Vercel (1 つのプロジェクト、東京 hnd1)
 ├─ /                              Unity の Web ビルド (CDN)            ← project-penguin で作る
 │   └─ /StreamingAssets/penguin-config.json   接続先と使う機能 (no-store)
 └─ /api/*                         FastAPI (Python Functions)           ← このリポジトリの backend/
        ├─ /api/v1/health
        ├─ /api/v1/eco-photo/judge ──▶ Amazon Bedrock の Nova 2 Lite (jp. 推論プロファイル、東京・大阪)
        ├─ /api/v1/community/goal  ──▶ Amazon DynamoDB (東京、infra/backend.yaml)
        └─ /api/v1/quests …
```

- **Web ビルドと API が同じ配信元**なので、本番では CORS が要らない。手元で別のポートから呼ぶときだけ `PENGUIN_CORS_ORIGINS` で許可する。
- Unity は起動時に `penguin-config.json` を 1 回読み、`api_base_url` (`/api`)・`judge` (`http` / `mock`)・`community`・`quests`・`demo_mode` で動きを決める。**当日変えてよいのはこのファイルだけ**で、Unity のビルドは作り直さない。
- 関数は状態を持てない。みんなの氷山のように数を共有するものは外部の保存先に置く (保存するのは件数と節目だけ、`.claude/rules/privacy.md`)。

## 契約

やり取りはすべて `api/openapi.yaml` にある。Unity 側の受け口 (`PenguinApiClient`・`HttpEcoPhotoJudge` など) は project-penguin の `Assets/Scripts/Backend/` にあり、契約の例 (`api/examples/`) を全部通すテストで守られている。

| | パス | Unity 側 | 当日 backend が作るもの |
|---|---|---|---|
| 起動の確認・時計 | `GET /v1/health` | 時刻合わせ | (事前にある) |
| 写真判定 | `POST /v1/eco-photo/judge` | `HttpEcoPhotoJudge` | AI の呼び出し、検証層 (`decision`・`checks`)、CO₂e |
| みんなの氷山 | `GET /v1/community/goal` | `HttpCommunityGoalSource` | 集計・節目の判定・ずる対策 |
| お題 | `GET /v1/quests`、`POST /v1/quests/{id}/redeem` | `HttpQuestSource` | お題の配信、QR・合言葉の照合 |

クライアントが解釈せずに並べるもの (`checks` の行、`message`、`comment`) は、当日に中身を増やしても画面がそのまま追従する。**画面に「backend が確かめたこと」を見せる口**なので、審査で差分を見せるときに使う。

## 公開の流れ

Vercel のプロジェクトは公開役 (project-penguin のオーナー) のアカウントにあり、このリポジトリは Vercel に繋いでいない (jphacks の org に外部アプリの承認を頼まなくて済むため)。

1. このリポジトリで PR を作り、CI を通して main にマージする。
2. 公開役が、project-penguin と tkb_2611 を隣同士に clone した状態で:

   ```bash
   git -C ../tkb_2611 pull
   uv run --no-project python scripts/deploy_web.py --backend ../tkb_2611/backend          # preview
   uv run --no-project python scripts/deploy_web.py --backend ../tkb_2611/backend --prod   # 本番
   ```

3. `deploy_web.py` は `backend/src/penguin_backend` と `uv.lock` から書き出した依存を写し、写したコミットを `penguin_backend/DEPLOYED_FROM` に残す。未コミットの変更があれば止まる。

AI のキーは置かない。backend は Vercel の OIDC トークン (`x-vercel-oidc-token`) で AWS の IAM ロール (`PENGUIN_AWS_ROLE_ARN`) を借り、1 時間で切れる認証情報で Bedrock と DynamoDB を呼ぶ。ロールに許すのは Nova 2 Lite の呼び出しとテーブルの読み書きだけ。資源とデプロイの手順は `infra/README.md`。

## 制約 (Vercel Hobby)

| | 値 | 対処 |
|---|---|---|
| 関数が受け取れる本文 | 4.5 MB | 契約で 1 MB まで。クライアントが長い辺 1280px に縮め、超えたら品質を下げて縮め直す |
| 関数の呼び出し・CPU 時間 (月) | 100 万回・4 時間。超えると 30 日止まる | ゲージの口だけ CDN に 10 秒置く (`public, s-maxage=10`)。問い合わせの間隔は `poll_seconds` で延ばす |
| 関数の実行時間 | `maxDuration` 60 秒 | AI の呼び出しは 15 秒前後で切る。クライアントは 25 秒で切る |
| 寝起き | 1 秒前後 | クライアントは retryable の失敗を 1 回だけ送り直す |
| プロジェクトの共有 | できない (Hobby) | 公開は公開役 1 人が行う |
