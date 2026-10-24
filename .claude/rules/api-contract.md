---
paths:
  - "api/**"
  - "backend/**"
  - "Assets/Scripts/Backend/**"
  - "tools/mock_backend/**"
---

# Unity と backend の契約ルール

Unity と backend のやり取りは、すべて契約 `api/openapi.yaml` (OpenAPI 3.1) に書いてあるものだけにする。Hack Day には backend だけを変え、Web ビルドは作り直さない (`docs/decisions.md` の 2026-10-19)。そのため、契約に無い通信はクライアントが受け取れず、当日まで食い違いに気付けない。

## 接続を足す・変えるときの順番

**コードより先に契約を書く。** エンドポイント・リクエストやレスポンスの項目・enum の値・エラーの code のどれを足すときも同じ。

1. `api/openapi.yaml` にパス・スキーマを書き、`info.version` を上げる。項目を足すだけなら minor を上げる。
2. `api/examples/<対象>.<HTTP 状態>.<場合>.json` に例を足し、`openapi.yaml` の `examples` から参照する。契約の外の値 (未来の enum など) を確かめる例は `api/examples/compat/` に置く。
3. 両側を直す。
   - Unity: `Assets/Scripts/Backend/` の `PenguinApiClient` にパスを足し、DTO (`ApiDtos.cs`) と口 (`Http*`・モック) を足す。エラーの code を足したら `BackendError.cs` にも足す。
   - backend: FastAPI の応答を契約の例と同じ形にする (契約テスト)。
   - ダミー: `tools/mock_backend/serve.py` が新しい例を返すようにする。
4. 検査を通す: `scripts/check_api_contract.py` と、`BackendContractTests` (Unity の EditMode テスト)。

## 守ること (前方互換)

- **必須の項目は増やさない・消さない・意味を変えない。** 新しい項目は省略可にする。
- クライアントは、知らない項目を無視し、知らない enum の値は安全側に倒す (`openapi.yaml` の `info.description`)。
- 壊す変更が要るときはパスの版を上げる (`/v2/`)。`/v1/` は審査が終わるまで残す。
- 写真の表の ID (`EcoSubjects`) を変えたら、契約の `EcoSubjectId` も同じにする (検査が落ちる)。

## 正本と写し

正本は提出用リポジトリ `jphacks/tkb_2611` の `api/`。project-penguin の `api/` は写しで、`check_api_contract.py --against ../tkb_2611/api` で一致を確かめ、`--sync` で写す。どちらで先に直しても、もう一方に同じ変更を入れてからマージする。詳しくは `api/README.md`。
