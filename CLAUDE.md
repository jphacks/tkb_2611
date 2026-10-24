# tkb_2611 — ueco / Project Penguin の backend

JPHACKS 2026 (東京B) の提出用リポジトリ。毎日のエコ写真で資源を集め、ペンギンと島を育てるアプリ「Project Penguin」の **backend (FastAPI) と、Unity クライアントとの契約** を置く。Unity クライアントの元は [Sh1n1230/project-penguin](https://github.com/Sh1n1230/project-penguin) にあり、Web ビルドとして https://project-penguin-nu.vercel.app で動いている (2026-10-25 から凍結)。ピボット後の iOS 版は、project-penguin の PR #143 を取り込んだ `unity/` で作る。全体像は `docs/README.md`。

## 応答の言語

**ユーザーへの返答・説明・質問はすべて日本語で書く。** コード・識別子・コマンド・ログの引用は原文のままでよい。

## このリポジトリの性質 (最初に読む)

- **評価されるのは Hack Day (2026-10-24 10:20 の開始タグ `hackday-start` 以降) に作った差分だけ。** 開始タグより前に入れてよいのは土台 (設定・疎通の確認・CI) と契約 (`api/`) まで。**写真判定・検証層・みんなの氷山・お題のエンドポイントの中身は、開始タグより前に書かない。** 迷ったら `docs/hackday.md` の「事前と当日の境界」を見る。
- **Web 版の Unity は作り直さない (凍結)。** 新しい機能は `unity/` の iOS 版に入れる。クライアントは契約にある項目しか受け取れないので、backend が返すものは必ず `api/openapi.yaml` の範囲にする。
- **`unity/` の最初のコミットは取り込み (project-penguin PR #143 の写し) で、当日の差分に数えない。** それ以降の `unity/` の変更は当日の差分。
- 判定に使う AI の選定・費用・データの扱いは project-penguin の Issue #125。

## 技術スタック

| 領域 | 使用技術 |
|---|---|
| backend | FastAPI (Python 3.11+)、Pydantic v2、pydantic-settings |
| 契約 | OpenAPI 3.1 (`api/openapi.yaml`)、JSON Schema 2020-12 |
| 公開先 | Vercel Hobby (Python Functions、東京 `hnd1`)。Unity の Web ビルドと同じ URL の `/api/*` |
| ツール | uv、ruff、mypy (strict)、pytest |

## ディレクトリ

| パス | 内容 |
|---|---|
| `backend/` | FastAPI (`src/penguin_backend/`、`tests/`)。ルートはすべて `/api/` の下 |
| `api/` | Unity との契約の**正本** (`openapi.yaml`、`examples/`)。project-penguin の `api/` はこの写し |
| `unity/` | Unity クライアント (Unity 6000.6.0f1、iOS 版)。歩数とガスの工場の試作は `Assets/Scenes/HealthFactoryPrototype.unity`、説明は `unity/docs/health-factory-prototype.md` |
| `docs/` | 構成 (`architecture.md`)・当日の進め方 (`hackday.md`)・このリポジトリの決定 (`decisions.md`) |
| `scripts/` | CI とローカル共用の検査 (`check_secrets.py`、`check_api_contract.py`) |
| `.claude/` | ルール (`rules/`、該当ファイルを読むと自動で効く)・フック・スキル (`/verify`) |

## コマンド

| 目的 | コマンド |
|---|---|
| backend を手元で起動 | `cd backend && uv run uvicorn penguin_backend.app:app --reload` (http://localhost:8000/api/v1/health) |
| 整形 / lint / 型 / テスト | `cd backend && uv run ruff format . && uv run ruff check . && uv run mypy . && uv run pytest` |
| 契約の検査 | `uv run --no-project --with openapi-spec-validator==0.9.0 --with jsonschema==4.26.0 --with pyyaml==6.0.3 python scripts/check_api_contract.py` |
| 秘密情報の検査 | `python3 scripts/check_secrets.py` |
| 個人に関わる内容の検査 | `python3 scripts/check_personal_info.py --all` (ステージした分は `--staged`) |
| git のフックを有効にする (clone したら最初に 1 回) | `sh scripts/setup_hooks.sh` |
| コミット / PR の前の一括検査 | `/verify` |
| Unity の EditMode テスト (Unity の CI は無いので手元で回す) | `"C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe" -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults <リポジトリの外>/editmode.xml` |
| 公開 (preview / 本番) | project-penguin の clone で `uv run --no-project python scripts/deploy_web.py --backend ../tkb_2611/backend` / `--prod` (公開役だけ。`docs/architecture.md`) |

CI (`.github/workflows/ci.yml`) は個人に関わる内容・秘密情報・契約・backend の検査を PR ごとに回す。

## 絶対規則

0. **個人に関わる内容をこのリポジトリに入れない。** 人の名前 (本名・ニックネーム)、連絡先、学校・住まい・予定などの個人の事情、チームの会話や議事録の中身は、コード・docs・コメント・コミットメッセージ・PR の本文のどこにも書かない。役割 (「公開役」「担当 A」) で書く。このリポジトリは jphacks の org に public で残り続け、消せない前提で扱う。チームの内部の資料は Unity 側の非公開のリポジトリに置く。Claude Code のフック・git のフック・CI で機械的にも止める → `docs/personal-info.md`。clone したら最初に `sh scripts/setup_hooks.sh`。
1. **写真と AI の生の出力をディスク・ログ・応答に残さない。** → `.claude/rules/privacy.md`
2. **API キーをコードにも設定ファイルにも書かない。** Vercel の環境変数 (`PENGUIN_` で始まる名前) から `Settings` で読む。キーをチャットやリポジトリに貼らない。
3. **接続 (エンドポイント・項目・エラーの code) を足す・変えるときは、先に `api/openapi.yaml` と `api/examples/` に書く。** 必須の項目は増やさない。→ `.claude/rules/api-contract.md`
4. **外部の AI の応答は Pydantic で検証してから使う。** 写真の表 (`EcoSubjectId`) に無い答えは `unknown` にする。→ `.claude/rules/backend.md`
5. **コードを読まずに書かない。** 変更前に既存の実装と契約を確かめる。未実装のものを探して見つからなければ「まだ無い」が正しい。
6. **3 ステップ以上のタスクは Plan モードで始める。**

## Git

- **commit・push・PR はユーザーが指示したときだけ。** `main` に直接 push しない。`<type>/<内容>` の作業ブランチを切る。force push・`--no-verify`・履歴の書き換えはしない。
- コミットメッセージは `<type>(<scope>): <日本語の要約>` (type は `feat` / `fix` / `refactor` / `perf` / `test` / `docs` / `chore` / `ci`、scope は `backend` / `api` / `unity` / `docs` / `ci`)。1 コミット 1 論点。
- **GitHub flow。** main はルールセットで守ってあり、PR を通さないと入らない。マージは squash だけで、CI の 5 つの検査 (`.github/workflows/ci.yml` のジョブ) が通っている必要がある。squash したコミットの題と本文は PR の題と説明になるので、PR の題もコミットメッセージと同じ形で書く。マージしたブランチは自動で消える。
- **CI のジョブの `name:` を変えたら、ルールセット「main (GitHub flow)」の必須の検査も同じ名前に直す。** 直さないと、どの PR もマージできなくなる。
- 開始タグ `hackday-start` はルールセットで固定してあり、打った後は消すことも付け直すこともできない。
- Hack Day 中は、主要な PR を README の「Hack Day 中に開発した差分」に載せられるよう、PR の説明に「何を作ったか」を 1 行で書く。

## スキル

| 作業 | 使うもの |
|---|---|
| 日本語の文書の作成・推敲 | `yomiyasu` (`.claude/skills/yomiyasu/SKILL.md`) |
| 原典・先行研究をたどる調査 | `imozuru` (`.claude/skills/imozuru/SKILL.md`) |
| コミット / PR の前の検査 | `/verify` |

日本語の文書を作成・推敲するときは、先に `yomiyasu` の指示を読む。意味・条件・未確定事項を保って文章を整え、保存時に同梱のリンターで点検する。

`imozuru` の調査資料・抜き書き・ログは、既定の `~/research/<話題>/` など、このリポジトリの外に保存する。原文や個人に関わる内容を public リポジトリへ転載しない。写真や AI の生の出力を保存しない規則は、調査時にも適用する。導入元と更新手順は `.claude/skills/imozuru/UPSTREAM.md`。

既存版のゲームの規則・画面の仕様は project-penguin にある。仕様を確かめるときは `docs/README.md` の「仕様の置き場所」から辿る。iOS 版の Unity の実装は `unity/` にある。
