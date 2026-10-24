# Unity と backend の契約

> **接続 (エンドポイント・項目・エラーの code) を足す・変えるときは、コードより先にこの契約 (`openapi.yaml` と `examples/`) に書く。** 契約に無い通信は書かない。手順は下の「変えるとき」。

Unity クライアント (`Sh1n1230/project-penguin`) と backend (提出用リポジトリ `jphacks/tkb_2611`) のやり取りの決まり。**正本は tkb_2611 の `api/`** で、ここはその写し。Hack Day には backend だけを変え、Web ビルドは作り直さないので (`docs/decisions.md` の 2026-10-19)、クライアントが受け取れる形をここで先に固めておく。

| ファイル | 中身 |
|---|---|
| `openapi.yaml` | OpenAPI 3.1 の契約。前方互換の規則・共通のヘッダー・上限・プライバシーは `info.description` に書いてある |
| `examples/<対象>.<HTTP 状態>.<場合>.json` | 応答の例。契約から参照され、スキーマに合う。Unity の EditMode テスト (`BackendContractTests`)・ダミーサーバー (`tools/mock_backend`)・当日の pytest が同じものを読む |
| `examples/compat/` | 契約の外の値 (未来の版の enum・知らない code)。クライアントが安全側に倒れることを確かめる。スキーマには合わない |

## エンドポイント

| | パス (実際は `/api` の下) | Unity 側の口 |
|---|---|---|
| 起動の確認・時計 | `GET /v1/health` | `GameStateSystem` の時刻合わせ |
| 写真判定 | `POST /v1/eco-photo/judge` | `IEcoPhotoJudge` → `HttpEcoPhotoJudge` |
| みんなの氷山 | `GET /v1/community/goal` | `ICommunityGoalSource` → `HttpCommunityGoalSource` |
| 期間限定のお題 | `GET /v1/quests` | `IQuestSource` → `HttpQuestSource` |
| QR・合言葉の照合 | `POST /v1/quests/{id}/redeem` | `IQuestSource.RedeemAsync` |
| 水の写真 (1.2.0) | `POST /v1/water/estimate` | iOS ネイティブの受け口 (project-penguin) |
| すれ違いの相互確認 (1.2.0) | `POST /v1/encounters` | iOS ネイティブの受け口 (project-penguin) |

通信はすべて `PenguinApiClient` (`Assets/Scripts/Backend/`) を通る。接続先と使う機能は実行時に `StreamingAssets/penguin-config.json` から読む (`BackendConfig`)。

## 変えるとき

1. tkb_2611 の `api/` を直し、`info.version` を上げる (項目を足すだけなら minor)。**必須の項目を増やさない・消さない・意味を変えない。** 新しい項目は省略可にする。
2. 例を足す・直す。新しい例は `openapi.yaml` の `examples` から参照する。
3. こちらに写して検査する:

   ```bash
   uv run --no-project --with openapi-spec-validator==0.9.0 --with jsonschema==4.26.0 --with pyyaml==6.0.3 \
     python scripts/check_api_contract.py --against ../tkb_2611/api --sync
   ```

   写真の表 (`EcoSubjects`) とエラーの code (`BackendError.cs`) が契約と食い違うと、ここで落ちる。

## 手元で試す

```bash
uv run --no-project python tools/mock_backend/serve.py                       # http://127.0.0.1:8787/api
uv run --no-project python tools/mock_backend/serve.py --static Builds/WebGL # Web ビルドも同じ配信元で
curl 'http://127.0.0.1:8787/__mock?judge=retake&delay=3'                      # 起動中に切り替える
```

Editor から繋ぐときは `Assets/StreamingAssets/penguin-config.json` を `{"api_base_url": "http://127.0.0.1:8787/api", "judge": "http", ...}` にする (コミットしない)。
