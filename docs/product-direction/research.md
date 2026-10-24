# 競合・既存コード・実現性の調査

調査日: 2026-10-24。一次資料とローカルの既存コードを読んだ机上調査。競合の実機プレイ、利用者調査、性能測定は未実施。公式に確認できた事実と、本製品への示唆を分ける。

## 競合と代替手段

| 製品 | 公式資料で確認した特徴 | 競合する利用動機 | 本製品への示唆・差別化の仮説 |
|---|---|---|---|
| [Pikmin Bloom](https://pikminbloom.com/gameplay) | 歩数で苗を育て、仲間を集める。発見場所がデコの種類に関係する | 歩く楽しさ、日常の移動、コレクション | 現実の行動を仲間獲得だけでなく、生産網の設計と組み合わせに使う |
| [Forest](https://www.forestapp.cc/) | 集中する時間を木の成長として見せ、集中セッションを森として蓄積する | スマホから離れる動機、行動の可視化 | 非使用時間を電気に変え、後から工場の設計に使う。集中タイマーと端末全体の非使用時間は同じ指標ではない |
| [shapez の公式ソース・説明](https://github.com/tobspr-games/shapez.io) | 図形を切断・回転・結合・着色する工場ゲーム。ソースが公開されている | 生産工程の最適化、工場を作る満足感 | 入出力・処理・表示の責務を参考にできる。現実由来のエネルギーと階層ごとの新しい生産は本製品で設計する |
| [Mindustry](https://mindustrygame.github.io/) | 素材の加工網を構築するタワーディフェンス。公式サイトにApp Storeへの導線がある | モバイルで工場をつなぐ遊び | スマホの工場ゲーム自体は既存。接続の分かりやすさと階層編集を具体的に比較する必要がある |

「日常行動による複数エネルギー」「すれ違いのペンギン収集」「内部を編集できる階層型の組み合わせ」を一体化することが、本製品の差別化仮説。調査した範囲での整理であり、同じ仕組みの競合が存在しない、新規性が保証される、とは結論しない。

次の比較では、配置して資源が流れ始めるまでの手数、物流経路の修正、広い工場の見渡しやすさ、資源不足時の遊び、現実の行動がない日の体験を実機で確認する。本製品では隣接配置と倉庫の操作を、競合では各製品の物流操作を確認する。価格・人気・市場規模の比較は今回の調査範囲に含めない。

## 既存プロジェクトで読んだコード

参照元は既存クライアント project-penguin のローカル作業ツリー。リポジトリへの導線は[既存仕様の索引](../README.md#仕様の置き場所-project-penguin)を参照。以下はファイル内容を読んだ結果で、動作確認・最新ブランチとの一致確認はしていない。既存の文書だけでは分からない Village 系の実装も存在した。

| 参照ファイル（project-penguin 内） | 確認した内容 | 参考にする点 | 今回との違い・扱い |
|---|---|---|---|
| `Assets/Scripts/Domain/Island/IslandGame.cs` | `Tick`、`StepProducer`、共有在庫からの材料消費・出力、ガス消費、停止理由、保存状態 | 生産の進行、材料不足・満杯の表示、時計の扱い | 接続先ごとの輸送ではない。これをそのまま新しい物流仕様にしない |
| `Assets/Scripts/Domain/Island/IslandCatalog.cs` | 資源と品目、施設、材料と成果物のレシピを定義 | エネルギー・品目・レシピを分ける構造 | 電気は建築、水は開拓、ガスは運転が中心。新仕様の各エネルギーの消費規則は別途決める |
| `Assets/Scripts/Systems/StepSource.cs` と `Assets/Plugins/iOS/PenguinPedometer.mm` | iOSの `CMPedometer` への橋渡し。開始時刻を持つ非同期の歩数要求 | 歩数取得の境界と、古い応答の識別 | 取得口の再利用候補。旧換算率・上限は引き継ぎ確定ではない |
| `Assets/Scripts/Domain/Village/VillageFlow.cs` | 入出力ポート・線、接続検証、建物ごとの保有品、輸送と生産の評価 | 施設間の関係、停止理由、輸送計画 | 新方針は隣接配置と倉庫による物流。手動の配線・ポート選択・配線距離の旧制約はそのまま採用しない |
| `Assets/Scripts/Domain/Village/VillageShapes.cs` | `FindShapes` で3棟の直線・L字などを探し、配置プレビューで成立・消失を示す | 配置による組み合わせ検出、配置前の予告、重複の処理 | 4マス・城レベル・ズーム階層と同じ仕組みではない。成立条件は新しく定義する |
| `Assets/Scripts/Domain/Village/VillageTimeline.cs` と `VillageGame.cs` | 時刻を進める処理。10分刻みの更新、日次の水維持費、不足時の土地・建物喪失 | 決定的な更新、復帰時の経過計算 | 10分刻みと喪失ペナルティを自動採用しない。旧機能の見直し対象 |
| `Assets/Scripts/Presentation/World/IslandCameraRig.cs` | ピンチ・ホイールによる距離変更と平行移動 | スマホ操作の入力処理 | 通常のカメラズーム。城レベルで階層を解放する処理とは分ける |

旧 `docs/production-island/game-design.md` は「共有在庫を使い、線でつながない」としている一方、Village 系には接続実装がある。この相違を、どちらかが唯一の現行実装であると推測して解消しない。新方針は横並びの施設間で資源をやり取りし、遠方へは施設を連ねるか倉庫を介して運ぶ。線を手動で引く操作は基本にしないため、Village 系の配線操作をそのまま移植しない。

写真獲得・共有在庫中心・記録/実績によるペンギン獲得を新方針で見直すことは確認済み。氷・港・通貨・旧セーブの扱いは[未決事項](questions.md)に残す。既存の数値やペナルティを持ち込む場合も先に確認する。

## 外部の工場ゲームコードからの参考

shapez の次のファイルを読んだ。コードのコピーや依存追加は行っていない。

| 一次資料 | 確認した構造 | 本製品への適用案 |
|---|---|---|
| [`src/js/game/map_view.js`](https://github.com/tobspr-games/shapez.io/blob/master/src/js/game/map_view.js) | 可視チャンクの描画、背景のキャッシュ、描画層の分離 | 見える範囲・階層に応じた描画。生産状態とは別に管理する |
| [`src/js/game/systems/item_processor.js`](https://github.com/tobspr-games/shapez.io/blob/master/src/js/game/systems/item_processor.js) | 加工種別ごとの処理ハンドラと、進行中の加工を扱うシステム | 材料・処理時間・成果物を持つレシピと、組み合わせ判定を分離する |

[公式 README](https://github.com/tobspr-games/shapez.io/blob/master/README.md)では元リポジトリの積極的な保守は終了していると説明されている。設計の参考にする範囲とする。[LICENSE](https://github.com/tobspr-games/shapez.io/blob/master/LICENSE)は GPL v3。現在の本リポジトリの MIT と同じ条件とは扱わず、実コード・アセットを取り込む場合は、対象とライセンス条件を別途確認してから判断する。

## iOS連携の検証事項

| 項目 | 調べて確認したこと | 本製品で必要な判断・検証 |
|---|---|---|
| スクリーンタイム | Appleの [DeviceActivityReport](https://developer.apple.com/documentation/deviceactivity/deviceactivityreport) は利用情報を表示する仕組みで、拡張の外へ機微な内容を移せないサンドボックスで動く | 端末全体の時間をゲームへ自由に取り出せるとは置かない。必要な日次判定・換算を、許されたAPIとデータ境界で実現できるか試作する |
| 配布の権限 | [Family Controls の配布用 entitlement](https://developer.apple.com/documentation/familycontrols/requesting-the-family-controls-entitlement) は申請が必要 | 開発機での動作と配布できることを分けて検証する |
| 利用目的 | [Apple Developer Program License Agreement §3.3.3 P](https://developer.apple.com/support/terms/apple-developer-program-license-agreement/) は Family Controls の主目的と利用データの用途を家族管理・本人の端末利用管理に制限している | 純粋なゲーム報酬用途が認められると断定しない。「非使用時間から電気」の目的・機能が条件に合うか確認する。難しい場合の集中タイマー等は、別の指標になるため承認前に置き換えない |
| バーコード | [GS1 の GTIN 解説](https://www.gs1uk.org/knowledge-hub/product-identification/what-is-a-gtin)は商品を識別する番号として説明する | スキャン時に水エネルギーを付与する方針。商品容量に応じて換算する場合は商品データとの照合が必要で、飲んだ量の実測とは区別する。同一商品の1日1回制限は暫定案であり、実際に同じ商品を複数本飲んでも制限される。データ提供元・対象商品・総量上限は未決 |
| すれ違い | Appleの [Core Bluetooth ガイド](https://developer.apple.com/library/archive/documentation/NetworkingInternetWeb/Conceptual/CoreBluetooth_concepts/CoreBluetoothBackgroundProcessingForIOSApps/PerformingTasksWhileYourAppIsInTheBackground.html)では、背景走査の間隔増加・広告内容の制限・プロセス終了の可能性がある | BLEを候補としつつ、画面ロック・両端末が背景・短時間の通過などで検出率を実測する。常時確実な検知は前提にしない。資料はアーカイブのため、対象OSでも確認する |

歩数についても、[同契約 §3.3.3 H](https://developer.apple.com/support/terms/apple-developer-program-license-agreement/)の Motion & Fitness の目的・利用条件を踏まえ、歩く行動を支える体験と説明を設計する。既存コードがあることは配布条件の確認済みを意味しない。

今回の新機能は既存の `/v1/eco-photo/judge` 等の写真中心の契約では定義されていない。まず端末内で完結する範囲を決め、商品照合・遭遇情報などをサーバーへ送る必要が出た段階で、新方針に必要な契約とデータ取り扱いを設計する。既存の「端末や利用者を特定する値を保存しない」規則との衝突も、識別子を導入する前に確認する。

## 組み合わせと性能の検証案

- 内部4棟・上位建物の生産はゲーム状態として保持し、表示対象だけをカメラ段階に応じて切り替える案を検討する。生産をいつ有効化するかは未決。
- 内部が非表示でも、在庫・停止・材料消費・加工中の進捗を保つ。上位が内部の品物を使う場合は、外部輸送との消費競合も決める。
- 施設の隣接関係と倉庫による自動配送から物流経路を扱う。移設・組み合わせ・再展開で経路が変わる場合は、在庫と輸送途中の品物の扱いを決める。下位施設は上位資源も中継するが、材料としては消費しない。
- 集約計算は、内部の材料不足や満杯を無視した単純な「毎秒生産量×時間」では同じ結果になるとは限らない。詳細計算と同じ消費・生産になることを確認する。
- カメラ往復、再編集、保存・再開、資源不足で、余分な生産・二重消費・進捗消失が起こらないことを検証する。
- 同じ施設構成で描画とシミュレーションの時間を別々に測り、軽量化の効果を報告する。まだ削減率は主張しない。
