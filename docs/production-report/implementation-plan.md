# 生産レポートを工業モードの全画面として作り直す (実装方針)

見た目と数値の決め方は [`README.md`](README.md) (設計仕様) を正とする。この文書は、それをどう実装するかの方針と、実装上の注意。

## 背景

村の「生産効率レポート」(今は `ProductionNavigator.ShowSummary(false)` が出す下からのシート) は、カードごとに並びがばらばらで、数字・バッジ・飾りが同じ重さで並び、どこを見ればよいかが分からない。参考にしたゲームの用語もそのまま入っている。

見本を作り直しながら、次のように決めた。

- **ホームはポケモンスリープ寄りの柔らかさのまま。工業モードの画面はエンドフィールド寄り** (角張った形、高い密度、数字が主役)。
- 似すぎないよう、題材をこのゲームに寄せる。
  - ゲージは「六角形のガスのタンクに、供給と消費のパイプがつながる」形にする。
  - グラフは「1 分ごとの上下の棒」にする。
  - 見出しの印は六角形にする。
  - 色はペンギンの紺 (体)・黄 (くちばし)・氷の水色にする。
- 他作品の固有の用語は使わない (「生産レポート」「動力」「品目」「ガスのタンク」)。
- タンクの水位は**表示だけの目盛り** (今の消費の速さで「1 時間ぶん」あれば満タン)。ゲームの規則 (ガスの上限) は変えない。
- 数字だけ **Oxanium (OFL)** を足す。本文は M PLUS Rounded 1c のまま。

![見本 (数字は架空の値)](mockup.jpg)

## 方針

### 1. データ (Domain、`Assets/Scripts/Domain/Village/`)

今の `VillageGame` は、ガスと作った総数を直近 60 秒だけ持っている (`_history`)。品目ごとの推移は無く、`VillageItemRateView.Actual*PerMinute` も実測ではなく「動いている施設の理論値の和」になっている。そこで次を足す。

- **1 分刻みの記録**: キーは分番号 (`_at / 60000`)。中身は品目ごとの作った数・使った数と、ガスの消費。直近 60 分ぶんだけ残す。
- **数える場所**:
  - 作った数: 加工が完成して在庫が増えるところと、並びのおまけで実際に在庫が増えるところ。`BonusPerMinute` は表示用の理論値なので、記録には使わない。
  - 使った数: 加工の開始時に材料を引くところ。無制限モードでは引いていないので数えない。
  - ガスの消費: ガスを払うところ (`_spent` を増やすところ)。
  - 並びのおまけの処理 (`VillageGameGroups.cs`) は別の作業中なので、記録を足す 1 行だけにとどめ、規則は変えない。
- **表示用の値**: `VillageView` に次を足す。
  - `Trend` (品目ごとの直近 N 分の作った数・使った数。古い順、欠けた分は 0)
  - 品目ごとの、直近 1 分の実測の生産・消費。既存の `ActualMakePerMinute` / `ActualUsePerMinute` は、この実測に置き換える。`Theoretical*` はそのまま
  - 今日受け取ったガス (受領台帳の「歩数 → ガス」の行のうち、今日の分の合計。日付番号は引数で受け取る)
- **保存**: `VillageState` に `Trend` を足す。
  - JSON への追加だけなので、古い保存は空の記録として読める。保存の版は上げない。
  - `Restore` で検証する: 分番号が保存時刻以前かつ 60 分以内、数は 0 以上で有限、件数に上限。不正なら既存と同じく `ArgumentException`。
- **計算** (`VillageInsight`。`UnityEngine` に依存しない静的関数):
  - `SupplyPerMinute(受け取った量, 今日 0 時からの経過分)`: 経過分は最低 60 分として扱う (朝方に値が跳ねないように)。経過分は Presenter が端末の時計から求めて渡す。
  - `MinutesUntilEmpty(ガスの量, 収支)`: 収支が 0 以上なら「減っていない」(値なし)。
  - `TankLevel(ガスの量, 消費/分)`: 0〜1。消費が 0 なら満タン。
  - `TankCapacity(消費/分)`: 消費/分 × 60。消費が 0 なら値なし。

### 2. 工業モードの見た目 (USS とトークン)

- **トークン**: `Tokens.uss` に `--ind-*` を足す。
  - 値: 下地 `#E9EFF5` / `#DCE5EE`、紺 `#17385F`、濃い紺 `#0F2742`、紺 2 `#21466F`、黄 `#FFC928`、水色 `#5CC8F0`、赤 `#FF5A5F`、文字 `#F4F8FC` / `#9FB4CB`、`--font-num`。
- **部品の USS**: 新しく `Assets/UI/DesignSystem/Industrial.uss` を作り、`PenguinTheme.tss` から読み込む。どの部品も `.pp-industrial` の下だけで効くようにして、ホームの部品と混ぜない。生の色は `Tokens.uss` だけに書く (テストで止める)。
- **USS に無いもの** (`linear-gradient`・`radial-gradient`・`clip-path`・`grid`・`text-shadow`):
  - 点の格子と斜線の帯: 小さな PNG を `background-repeat` で敷く。
  - 下の帯のぼかし: 単色で代える。
  - 角を欠いた形: `ChamferPanel` (下の 3 を参照)。
  - 大きな数字の白フチ: `-unity-text-outline-width` / `-color`。
- **書体**: Oxanium の `.ttf` (Bold・ExtraBold) と OFL の文書を `Assets/UI/Fonts/` に置く。既存の `OFL.txt` と名前がぶつかるので、`Oxanium-OFL.txt` にする。
  - `-unity-font-definition` で `.ttf` を直接指す。FontAsset のアセットは作らない (`docs/design-system.md` の決まり)。
  - `THIRD_PARTY_NOTICES.md` とアプリ内のライセンス表示 (`ThirdPartyNotices.cs`) に足す。
  - **Oxanium に日本語は無い**。数字の Label と、単位や文 (「/分」「便」「分で空になる」) の Label は必ず分ける。

### 3. 描く部品 (`Assets/Scripts/Presentation/UI/Industrial/`)

すべて `[UxmlElement] public partial class X : VisualElement` にし、`generateVisualContent` (Painter2D) で描く。色は USS の独自の値 (`CustomStyleProperty<Color>`) から `CustomStyleResolvedEvent` で受け取り、`MarkDirtyRepaint` で描き直す。

| 部品 | 中身 |
|---|---|
| `ChamferPanel` | 角を欠いた背景。角の大きさと塗り色を USS から受け取り、8 頂点の多角形を塗る |
| `HexMark` | 見出しの六角形の印 (塗り・縁・芯の 3 色) |
| `FlowBars` | `SetData(作った数[], 使った数[])`。中央の線から上に生産 (黄)、下に消費 (水色)。使った数が作った数を上回った区間は赤。高さは最大値で正規化する |
| `TankGauge` | 六角形のタンク、水位、斜線の模様、目盛り 3 本、左右のパイプと ››› |

**TankGauge の描き方** (Painter2D には切り抜きが無い):
- 外の六角形 (紺) と内の六角形 (白) を塗る。
- 水位: 尖りが上の正六角形は凸なので、x ごとに内側の上端と下端が計算できる。x を 8px 刻みで走査し、波の高さ (上端より下に制限) の点列と、下端を逆向きにたどる点列で 1 つの多角形を作って塗る。
- 斜線: 45° の線分を一定の間隔で引き、同じ走査で求めた上下の範囲の中だけを描く。
- 動き: 水位は 0.4 秒で目標へ寄せる。波の位相と ››› の流れは一定の間隔で進め、供給・消費が 0 の側は止める。画面が見えていない間は止める。

### 4. 画面と Presenter

- **新規ファイル**: `Assets/UI/ProductionVillage/Report/ProductionReport.uxml`・`ProductionReport.uss`、`Assets/Scripts/Presentation/UI/ProductionVillage/ProductionReportPresenter.cs`・`ProductionReportNames.cs`。
- **構成** (上から): 見出し (英字ラベル・六角形の印・「生産レポート」、タブ「動力 / 品目」、閉じる) → 斜線の帯 → タンクと供給・消費の数字、中央のガスの量 → 収支の札 → 状態の札 3 つ → 表 (`ScrollView`) → 下の帯 (固定。並び順・絞り込み・期間)。セーフエリアは既存の `pp-safe-area` を使う。
- **タブ**: 「品目」では、タンクの部分を隠して表を全高にする。
- **更新**: 1 秒ごとに `VillageView` を取り直し、値だけ差し替える。行は品目ごとに 1 回だけ作って使い回す (毎秒作り直さない)。
- **開いたとき**: 行を上から 40ms ずつずらして出し、数字は 0.4 秒でカウントアップする。
- **並び順・絞り込み・期間**: 並び順は標準・不足が先・生産が多い順。絞り込みはすべて・作っている・止まっている。期間は 10 分・60 分。
- **行の状態**:
  - 不足: `IsDeficit` のとき。
  - 停止: 理論の生産が 0 より大きいのに、期間内に 1 つも作っていないとき。理由は、その品物を作る施設の状態から出す。ガス → 「ガスが足りない / 歩くとガスがたまるよ」、材料 → 「材料を待っている」、満杯 → 「置き場がいっぱい」。
  - 無制限モード: タンクは満タンのまま「∞」、収支の札は「開発用: 無制限」。
- **空の状態**: 施設が 1 つも無いとき、表の代わりに「施設を建てると、ここに生産の流れが出るよ」と出す。
- **品物の絵**: 下の 5 で絵ができるまでは、白一色のアイコン (`IslandUi.IconClass(item)`) に品目の色を付けて代わりにする。
- **名前の定数**: 要素名は `ProductionReportNames`、クラスは `UiClasses` に足す。`ProductionVillageUiTests` と同じ形の突き合わせテストを `ProductionReportUiTests` として足す。
- **画面の重ね方**: 既存の `ProductionApp.uxml` に `<ui:Template>` / `<ui:Instance>` で入れる。シーンは変えない。`Resources` は使わない。
- **入口の付け替え** (`ProductionNavigator.cs`)。このファイルは別の作業でも大きく書き換わっているので、編集の直前に必ず読み直し、次のことだけを行う。
  - `ShowSummary(false)` を呼んでいるところ (生産量のボタン、工業モードの効率のボタン、診断の帯) を、レポートを開く呼び出しに替える。
  - `ShowSummary` の港ではない側の組み立てと、そのためだけにあるクラス・描画 (古いリングのゲージ・行のクラスなど) を消す。港 (`ShowSummary(true)`) は今のシートのまま残す。

### 5. 品物の絵

`Assets/Editor/ProductionIsland/FacilityThumbnailRenderer.cs` を広げ、`Assets/3Dmodel/Item_*_v001` (9 種) を `Assets/UI/Thumbnails/Thumb_Item_*.png` に描き出すメニューを足す。`Thumbnails.uss` に `.pp-thumb--item-*` を、`UiClasses.Thumb` に `OfItem(Item)` を足して、行の札を差し替える。

## 進める順

1. Domain の記録と保存、テスト
2. トークン・`Industrial.uss`・書体
3. 描く部品 4 つ
4. 画面と Presenter、入口の付け替え
5. 見た目を見本に合わせる
6. 品物の絵

1 つ終わるごとにコンパイルと EditMode のテストを通してから、次へ進む。1 手順 1 コミットを目安にする。

## やらないこと

- 並びのおまけの規則 (`VillageGameGroups.cs`・`VillageGroupRules.cs`) を変えること (記録を足す 1 行だけは可)。
- ホームの部品 (`.pp-hero` など) の見た目を変えること。工業モードの見た目は `.pp-industrial` の下だけ。
- `ProductionApp.uss`・`Industrial.uss`・`ProductionReport.uss` に生の色を書くこと。
- 保存の版の番号を変えること。
- `.unity`・`.prefab`・`.asset`・`.meta`・`.mat` をテキストとして書き換えること。新しいファイルの `.meta` は Unity に作らせる。

## 検証

- **EditMode**: 記録の積み上げ、61 分前の破棄、保存と復元で一致すること、`Trend` の無い古い保存が読めること、不正な値で例外になること、`VillageInsight` の各関数の境界 (0・負・大きな値)、名前とクラスの突き合わせ、描く部品に値を入れても例外が出ないこと。全件を流す。
- **PlayMode**: `ProductionVillageSceneTests` だけを流す (全件の実行中に Editor が落ちた記録があるため)。
- **見た目**: 再生して GameView を 1080×1920 と 1080×2400 にし、次の状態を撮って見本と並べる。
  - 通常、不足の行がある、停止の行がある、施設 0 件、品目のタブ、60 分の期間
  - ガスが 0、ガスがたくさん (水位が満タンで止まること)、無制限モード
- **操作**: 開閉、タブ、並び順・絞り込み・期間、スクロールと下の帯の重なり、セーフエリア。
- **検査**: `/verify` (`.meta`・Editor のバージョン・秘密情報・アイコンの決まり) を流す。
