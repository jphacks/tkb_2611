# Project Penguin (プロジェクトペンギン)

**自分の日常の行動が、ゲーム内の工場のエネルギーになる iOS の工場ゲーム。** 歩数はガス、スマホを使わなかった時間は電気、飲みきった空のペットボトルの写真は水になる。生活によって集まる資源の量と比率が変わるので、工場の攻略も人ごとに変わる。

## このリポジトリについて (事前に作ったもの / Hack Day に作ったもの)

| | 場所 | いつ |
|---|---|---|
| Unity クライアント (3D の島・ゲームの規則・UI・保存・Web 化・backend の受け口) | [Sh1n1230/project-penguin](https://github.com/Sh1n1230/project-penguin) | **事前に開発** |
| Unity クライアントの写し (歩数とガスの工場の試作を含む) | このリポジトリの `unity/`。project-penguin の PR #143 (`ab741ea`) から 2026-10-25 に取り込んだスナップショット | **取り込み** (取り込みのコミットは当日の差分に数えない) |
| Unity と backend の契約 (`api/openapi.yaml`) と backend の土台 (`backend/` の設定と疎通の確認だけ) | このリポジトリ (開始タグより前のコミット) | **事前に用意** |
| 水の写真の判定・すれ違いの相互確認 (backend)、工房モードの工場ゲーム・施設の強化・生産レポート・iOS の入力 (`unity/`) | このリポジトリ | **Hack Day に開発** |

Hack Day 中に開発した差分: [`hackday-start...main`](https://github.com/jphacks/tkb_2611/compare/hackday-start...main) (開始タグは 2026-10-24 10:20 に打った)

### Hack Day 中に開発した主な差分

| PR | 何を作ったか |
|---|---|
| [#12](https://github.com/jphacks/tkb_2611/pull/12) | 日常の行動で動かす工場ゲームへの方針転換を、設計文書にまとめた |
| [#13](https://github.com/jphacks/tkb_2611/pull/13) | 契約 1.2.0 に、水の写真の見積もりとすれ違いの相互確認の口を足した |
| [#14](https://github.com/jphacks/tkb_2611/pull/14) | ペットボトルの写真から水の量を見積もる口と、すれ違いの相互確認の口を backend に作った |
| [#16](https://github.com/jphacks/tkb_2611/pull/16) | 生活習慣で攻略が変わる工場ゲームの企画と、施設・資源・ペンギンの数値モデルを整理した |
| [#18](https://github.com/jphacks/tkb_2611/pull/18) | Unity クライアントを `unity/` に取り込み、Core Bluetooth のすれ違いを iOS に作った |
| [#19](https://github.com/jphacks/tkb_2611/pull/19) | 工場ゲーム (自動配送・2×2 の組み合わせ・エフォート・コンボ・保存) と村のホームを実装した |
| [#21](https://github.com/jphacks/tkb_2611/pull/21) | 契約 1.3.0 で、ペットボトルの容量を AI にラベルの表示から読ませ、量をサーバーで決めるようにした |
| [#24](https://github.com/jphacks/tkb_2611/pull/24) | 工場ゲームの水を空のペットボトルの写真で受け取り、すれ違いを backend の相互確認につないだ |
| [#27](https://github.com/jphacks/tkb_2611/pull/27) | 何も写っていない画像に容量を作って答える AI の作り話を、撮り直しにした |
| [#23](https://github.com/jphacks/tkb_2611/pull/23)・[#29](https://github.com/jphacks/tkb_2611/pull/29) | UI のアイコンの原本と数字用のフォントを足し、工房モードの呼び方と生産レポートの崩れを直した |
| [#22](https://github.com/jphacks/tkb_2611/pull/22)・[#30](https://github.com/jphacks/tkb_2611/pull/30) | clone した人が素材の実体を取り出して、村のホームを iOS でビルドできるようにした |
| — | 施設を電気で Lv5 まで強化し、生産速度・保管容量・効果範囲を伸ばせるようにした |

Web 版は Unity クライアントの Web ビルドで、このリポジトリの `backend/` を同じ URL の `/api` に置いている (公開の手順は project-penguin の `docs/web-deploy.md`)。Web 版のクライアントは 2026-10-25 から凍結し、新しい機能は `unity/` の iOS 版に入れる。

開発する人へ: clone したら最初に `sh scripts/setup_hooks.sh` を実行してください (個人に関わる内容をコミット・push の前に止める git のフック、[`docs/personal-info.md`](docs/personal-info.md))。構成は [`docs/architecture.md`](docs/architecture.md)、当日の進め方は [`docs/hackday.md`](docs/hackday.md)、Unity との契約は [`api/`](api/README.md)。AI エージェントは [`CLAUDE.md`](CLAUDE.md) (Codex などは [`AGENTS.md`](AGENTS.md)) から読む。


## iOS 版をビルドして遊ぶ

iOS 版は App Store などで配っていないので、手元の Mac でビルドして iPhone に入れる。有料の Apple Developer Program は要らない (無料の Apple ID の個人チームで入れられる)。

**要るもの**: macOS・git-lfs・Xcode (26.5 で確かめた)・Unity 6000.6.0f1 (Unity Hub で iOS Build Support のモジュールも入れる)・iPhone (iOS 15 以上)・Apple ID。

1. このリポジトリを clone する。数字用のフォント (Oxanium) の 2 つだけは Git LFS で入っているので、先に [git-lfs](https://git-lfs.com/) を入れておく (`brew install git-lfs && git lfs install`)。ほかの素材はそのまま入っている。GitHub の「Download ZIP」は LFS のファイルを含まないことがあるので、clone を使う。 git 2.45.1 (Homebrew などの古い版) だけは、git-lfs があると clone の途中で `active post-checkout hook found during git clone` と止まる (git の不具合で 2.45.2 で直った)。そのときは clone したフォルダで `git restore --source=HEAD :/` を実行すれば続きが取り出せる。Xcode に付いてくる git (`/usr/bin/git`) では起きない。
2. Unity Hub で `unity/` をプロジェクトとして開く (初回は取り込みに数分かかる)。
3. メニューの **Build > iOS Xcode プロジェクト** を選ぶ。`unity/Builds/iOS/` に Xcode のプロジェクトができる (入るのは正規のシーン `Assets/Scenes/ProductionVillageHome.unity` だけ)。
4. `unity/Builds/iOS/Unity-iPhone.xcodeproj` を Xcode で開き、`Unity-iPhone` のターゲットの **Signing & Capabilities** で次を設定する。
   - **Automatically manage signing** を入れ、**Team** に自分の Apple ID (Personal Team) を選ぶ。
   - **Bundle Identifier** を自分だけのものに変える (例: `com.<自分の名前>.projectpenguin`)。既定の `com.ueco.projectpenguin` は開発チームの個人チームで使っていて、ほかの人の個人チームでは登録できない。
5. iPhone を Mac につなぎ、Xcode の上の実行先に選んで ▶ (Run) を押す。
6. 初回だけ、iPhone の **設定 > 一般 > VPN とデバイス管理** で自分の Apple ID の開発元を信頼する。iOS 16 以上では、**設定 > プライバシーとセキュリティ > デベロッパモード** も入れる (再起動が要る)。

歩数 (モーション)・カメラ・写真・Bluetooth の許可は、使う操作をしたときにアプリが尋ねる。無料の個人チームで入れたアプリは 7 日で起動できなくなるので、そのときは手順 5 からやり直す。

Unity の Editor の再生 (▶) でも遊べる。歩数・すれ違いなどの端末の入力は、Editor では画面の「開発用」のボタンで合成の値を入れる。

[![IMAGE ALT TEXT HERE](https://jphacks.com/wp-content/uploads/2026/07/IMG_5316.png)](https://www.youtube.com/watch?v=piaNsc6ilBI)

## 製品概要
### 背景(製品開発のきっかけ、課題等）
工場ゲームでは、資源を集め、加工する施設をつなぎ、生産を自動化する。どう組み合わせれば効率よく作れるかを考えるのが魅力だが、最適な進め方が見えてくると、プレイヤーの間で共通の攻略 (メタ) が定まり、定番の手順をなぞるだけになりやすい。マップや入手できるレシピを乱数で変えるゲームでも、条件ごとの強い進め方は誰にとっても同じになる。

そこで、資源の集め方を変えた。生活習慣はその人に継続して付く条件なので、これを資源の供給条件にすれば、全員に共通する最適な進め方が成り立ちにくくなると考えた。対象は、工場ゲームや放置ゲームで最適化の方向が見え、新しい判断や発見がなくなって飽きた経験がある人である。

### 製品説明（具体的な製品の説明）
現実の行動で受け取ったエネルギーを使って、ペンギンの村 (工房モード) に施設を建て、資源の流れを設計する iOS のゲーム。

| 現実の行動 | 受け取るもの | 使い道 |
|---|---|---|
| 歩数 | ガス | 施設が品物を 1 回作るたびに使う |
| スマホを使わなかった時間 (24 時間 − 前日のスクリーンタイム) | 電気 | 施設の建設と強化 |
| 飲みきった空のペットボトルの写真 | 水 | 施設の建設 |
| ほかの利用者とのすれ違い | ペンギン | 解放した種類のペンギンが仲間になる |

漁場で魚をとり、さばき場ですり身にし、かまぼこ・ちくわ・干物・おでんへ加工していく。施設は近くの施設へ品物を自動で届けるので、線を手で引く必要はない。生産レポートでは、ガスの供給と消費の収支、あと何分でガスが空になるか、品目ごとの生産と消費、止まっている施設とその理由を確かめられる。

### 特長
#### 1. 生活によって攻略が変わる
ガスが多い日は、ガスを多く使って速く作る (ちくわ工場の「強火」など)。電気が多い日は、施設を建てたり強化したりする。どの資源をどこに使うかは、その人の生活とその日の行動で変わる。

#### 2. 並べ方で動く物流と施設群
施設は近くの施設や倉庫へ品物を自動で送る。仕分け所は配送距離を、氷室は保管容量を、増幅所は生産速度を、周りの施設に対して伸ばす。決まった形に施設を並べると施設群 (例: 漁場 3 つの「ちくわ3連ライン」) が成立し、品物を追加で生む。縮小すると、施設群の稼働状況をまとめて見られる。

#### 3. 電気で施設を強化する
施設は電気を使って Lv5 まで強化できる。生産する施設は、Lv が 1 上がるごとに生産速度が Lv1 の 0.25 倍ずつ上がり (Lv5 で 2 倍)、保管容量も増える。倉庫・仕分け所・氷室・宿舎・研究所・増幅所は、それぞれの効果が強くなる。強化の費用は施設の建設費 × 今の Lv で、強化の前に今の効果と次の効果を並べて確かめられる。

### 解決出来ること
資源の供給量と比率がプレイヤーの生活で変わるので、同じ生産目標でも、増産と増設のどちらに投資するか、どの順で進めるかが人ごとに変わることを狙う。同じ人でも、たくさん歩いた日にはいつもと違う進め方を選べる。

合成の入力で数日分を比べた検査 (`FactoryLifestyleTests`) では、日ごとの供給と残高によって、増産と増設のどちらが有利かが入れ替わることを確かめた。全レシピを含む長期のバランスや、遊ぶ人が楽しめるかは、まだ確かめていない。

### 今後の展望
- 他のプレイヤーと共同で取り組む目標 (レイド)、ペンギン・施設・資源の追加、新しい供給条件 (0 次資源) の追加。
- スクリーンタイムを Family Controls で自動で取得する。配布には entitlement の申請が要り、ゲームの報酬に使う用途が認められるかは未確認。
- iPhone 2 台でのすれ違い、実機での権限・読み取り・性能の確認。
- 4 マスの組み合わせより上の階層。

### 注力したこと（こだわり等）
* **同じ行動から二重に受け取らない。** 同じ写真は 1 回だけ受け取り、受け取った記録と工場の状態を同じ保存に入れる。保存に失敗したときはバックアップから戻し、古いバックアップで新しい状態を上書きしない。
* **AI の答えをそのまま信じない。** 水の写真では、AI にペットボトルか・ラベルの容量・水位を読ませ、飲んだ量と判定はサーバーが決める。何も写っていない画像に容量を作って答える場合は撮り直しにした ([#27](https://github.com/jphacks/tkb_2611/pull/27))。
* **すれ違いは、両方の報告がそろったときだけ成立にする。** 片方の端末だけの報告では、ペンギンを受け取れない。
* **写真と AI の生の出力を残さない。** 写真は判定のあいだだけメモリで扱い、ディスクやログに残さない。スクリーンタイムの画像と読み取った文字は端末の中だけで扱い、送らない。
* **鍵を置かない。** backend は Vercel の OIDC トークンで AWS の IAM ロールを借り、1 時間で切れる認証情報で AI と DB を呼ぶ。

## 開発技術
### 活用した技術
#### API・データ
* Amazon Bedrock の Nova 2 Lite (jp. 推論プロファイル、東京・大阪): 水の写真の判定
* Amazon DynamoDB (東京): すれ違いの相互確認
* Unity と backend の契約: OpenAPI 3.1 ([`api/openapi.yaml`](api/openapi.yaml))

#### フレームワーク・ライブラリ・モジュール
* Unity 6000.6.0f1 (UI Toolkit、URP)
* FastAPI・Pydantic v2・pydantic-settings (Python 3.11)、uv・ruff・mypy・pytest
* Vercel (Python Functions、東京 `hnd1`)、AWS IAM (Vercel の OIDC 連携)
* iOS: Core Motion (歩数)、Core Bluetooth (すれ違い)、AVFoundation (カメラ)、PhotosUI・Vision (スクリーンタイムの画面の読み取り)

#### デバイス
* iPhone (iOS 15 以上)

### 独自技術
#### ハッカソンで開発した独自機能・技術
* 村の工場ゲーム (工房モード): 自動配送・容量の予約・施設群・生産レポート。[`VillageGame.cs`](unity/Assets/Scripts/Domain/Village/VillageGame.cs)、[`VillageGroupRules.cs`](unity/Assets/Scripts/Domain/Village/VillageGroupRules.cs)、[`ProductionReportPresenter.cs`](unity/Assets/Scripts/Presentation/UI/ProductionVillage/ProductionReportPresenter.cs)
* 施設の強化: Lv ごとの効果と費用、支払い済みの加工を変えない強化。[`VillageUpgrades.cs`](unity/Assets/Scripts/Domain/Village/VillageUpgrades.cs)、[`VillageUpgradePanel.cs`](unity/Assets/Scripts/Presentation/UI/ProductionVillage/VillageUpgradePanel.cs)
* 水の写真の判定: AI の答えを Pydantic で検証し、量と判定をサーバーで決める。[`backend/src/penguin_backend/water/`](backend/src/penguin_backend/water/) ([#14](https://github.com/jphacks/tkb_2611/pull/14)・[#21](https://github.com/jphacks/tkb_2611/pull/21)・[#27](https://github.com/jphacks/tkb_2611/pull/27))
* すれ違いの相互確認: iOS の Core Bluetooth で相手を見つけ、両方の報告がそろったら成立にする。[`PenguinEncounter.mm`](unity/Assets/Plugins/iOS/PenguinEncounter.mm)、[`encounters.py`](backend/src/penguin_backend/encounters.py) ([#18](https://github.com/jphacks/tkb_2611/pull/18)・[#24](https://github.com/jphacks/tkb_2611/pull/24))
* 工場ゲームの試作: 2×2 の組み合わせ・エフォート・コンボ・19 種のペンギンと、生活条件を変えた数日分の比較。[`unity/Assets/Scripts/Domain/Factory/`](unity/Assets/Scripts/Domain/Factory/)、[`FactoryLifestyleTests.cs`](unity/Assets/Tests/EditMode/FactoryLifestyleTests.cs) ([#19](https://github.com/jphacks/tkb_2611/pull/19))
