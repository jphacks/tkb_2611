# サンプル（プロダクト名）

<!-- Hack Day の前に置いた雛形。製品の説明は #93 と当日に埋める。 -->

> **▶ 遊ぶ (Web 版、スマホのブラウザで開けます): https://project-penguin-nu.vercel.app**

## このリポジトリについて (事前に作ったもの / Hack Day に作ったもの)

| | 場所 | いつ |
|---|---|---|
| Unity クライアント (3D の島・ゲームの規則・UI・保存・Web 化・backend の受け口) | [Sh1n1230/project-penguin](https://github.com/Sh1n1230/project-penguin) | **事前に開発** |
| Unity クライアントの写し (歩数とガスの工場の試作を含む) | このリポジトリの `unity/`。project-penguin の PR #143 (`ab741ea`) から 2026-10-25 に取り込んだスナップショット | **取り込み** (取り込みのコミットは当日の差分に数えない) |
| Unity と backend の契約 (`api/openapi.yaml`) と backend の土台 (`backend/` の設定と疎通の確認だけ) | このリポジトリ (開始タグより前のコミット) | **事前に用意** |
| 写真判定と検証層・みんなの氷山・お題の配信・会場表示 | このリポジトリ | **Hack Day に開発** |

Hack Day 中に開発した差分: [`hackday-start...main`](https://github.com/jphacks/tkb_2611/compare/hackday-start...main) (開始タグは 2026-10-24 10:20 に打つ)

Web 版は Unity クライアントの Web ビルドで、このリポジトリの `backend/` を同じ URL の `/api` に置いている (公開の手順は project-penguin の `docs/web-deploy.md`)。Web 版のクライアントは 2026-10-25 から凍結し、新しい機能は `unity/` の iOS 版に入れる。

開発する人へ: clone したら最初に `sh scripts/setup_hooks.sh` を実行してください (個人に関わる内容をコミット・push の前に止める git のフック、[`docs/personal-info.md`](docs/personal-info.md))。構成は [`docs/architecture.md`](docs/architecture.md)、当日の進め方は [`docs/hackday.md`](docs/hackday.md)、Unity との契約は [`api/`](api/README.md)。AI エージェントは [`CLAUDE.md`](CLAUDE.md) (Codex などは [`AGENTS.md`](AGENTS.md)) から読む。


## iOS 版をビルドして遊ぶ

iOS 版は App Store などで配っていないので、手元の Mac でビルドして iPhone に入れる。有料の Apple Developer Program は要らない (無料の Apple ID の個人チームで入れられる)。

**要るもの**: macOS・git-lfs・Xcode (26.5 で確かめた)・Unity 6000.6.0f1 (Unity Hub で iOS Build Support のモジュールも入れる)・iPhone (iOS 15 以上)・Apple ID。

1. このリポジトリを clone する。数字用のフォント (Oxanium) の 2 つだけは Git LFS で入っているので、先に [git-lfs](https://git-lfs.com/) を入れておく (`brew install git-lfs && git lfs install`)。ほかの素材はそのまま入っている。GitHub の「Download ZIP」は LFS のファイルを含まないことがあるので、clone を使う。
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
> It's the Climate, Stupid!

参考文献：

Baum, Rudy M. “Editor’s Page: It’s The Climate, Stupid.”
*Chemical & Engineering News* 80, no. 10 (March 11, 2002): 5.
https://doi.org/10.1021/cen-v080n010.p005

気候変動は社会全体で取り組むべき課題である一方、個人の日常的な消費行動と環境負荷のつながりは見えにくい。そこで、環境問題を身近な行動と結び付け、環境配慮行動を促す仕組みを考える。

### 製品説明（具体的な製品の説明）
### 特長
#### 1. 特長1
#### 2. 特長2
#### 3. 特長3

### 解決出来ること
### 今後の展望
### 注力したこと（こだわり等）
* 
* 

## 開発技術
### 活用した技術
#### API・データ
* 
* 

#### フレームワーク・ライブラリ・モジュール
* 
* 

#### デバイス
* 
* 

### 独自技術
#### ハッカソンで開発した独自機能・技術
* 独自で開発したものの内容をこちらに記載してください
* 特に力を入れた部分をファイルリンク、またはcommit_idを記載してください。
