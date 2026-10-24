# map: factory-economy

問い: 資源と施設に何次かを付け、既存アセットを活かす工場ゲームの生産・物流・納品バランスを、他のゲームの設計記録から調整するにはどうするか。

最初の検索語: factory game production ratios bottleneck progression economy balance / 工場ゲーム 生産比率 段階 ゲームバランス

## 要る主張

- [x] C1: この分野の知見はどこに集まっているか。原文: factorio-2016-research.md:1「Friday Facts」、research-2011-economy.txt:107「Resource Flow」。
- [x] C2: 資源・施設の段階を増やしたとき、工程数・入力数・費用のどの変化が難度の急増を生むか。原文: factorio-2018-science.md:100「4.5 times」。
- [x] C3: 生産時間と入出力量から、施設比率と律速をどのように定量化できるか。原文: factorio-2018-science.md:232「total crafting time」、factorio-2017-resources.md:104「Ratio: 0.67」。補足: factorio-wiki-science.md:565「same tier」。
- [x] C4: 納品価値と需要の設定により、一つの品物だけを作る戦略が有利になる条件は何か。原文: shapez-2024-research.md:61「multiple shape requirements」、research-2024-geevo.md:198「converter」。本製品の数量比較は balance-review.md に分ける。普遍的に多様性を保証する比率は未確認。
- [x] C5: 日常の行動から得るエネルギーに供給上限がある場合、参考ゲームの生産・待ち時間・復帰設計をそのまま適用すると何が足りないか。原文: shapez-store.md:435「resources never run out」、factorytown-2019-rebalance.md:342「deadlock」。本製品の日常連携は製品方針の確定事項を参照する。同じ数値を適用できる実証は未到達。
- [x] C6: ゲーム経済のバランスを検証する手法について、元になる設計記録や研究はあるか。原文: research-2011-economy.txt:107、research-2024-geevo.md:473「playtests are still required」。

## 用語

- production ratio / throughput — 生産比率・処理量。[科学パックの比率](https://wiki.factorio.com/Science_pack)。
- crafting time / resource requirements — 加工時間・原料需要。[工場規模の計算](https://www.factorio.com/blog/post/fff-275)、[需要からの逆算](https://www.factorio.com/blog/post/fff-217)。
- source / pool / converter / drain — 供給・在庫・変換・消費先。[資源グラフの要素](https://arxiv.org/html/2404.18574v1#S3)。
- progression / complexity / technology dependencies — 解放段階・複雑さ・技術の依存関係。[公式の設計変更](https://www.factorio.com/blog/post/fff-275)。
- milestones / side tasks — 段階の到達目標・任意の課題。[研究システムの変遷](https://steamcommunity.com/games/2162800/announcements/detail/4103413768091304193)。

## 人

リポジトリの規則に従い、発信者は制作組織または役割で記録する。個人名は記録しない。

- Wube Software — Factorioの制作組織。Friday Facts。
- tobspr Games — shapez 2の制作組織。公式開発告知。
- Factory Townの開発役 — 公式の更新・調整理由。
- ゲーム経済の研究チーム — AIIDE論文とGEEvo論文。

## 場

- Factorio公式開発ブログ — https://www.factorio.com/blog/ 。3記事を取得。
- shapez 2公式開発告知と製品説明 — https://store.steampowered.com/news/app/2162800 。開発告知フィードと製品説明を取得。
- Factory Town公式開発告知 — https://store.steampowered.com/news/app/860890 。2019年の告知フィードを取得。
- AIIDE公開論文 — https://ojs.aaai.org/index.php/AIIDE 。1論文をPDF・本文で取得。
- arXiv公開論文 — https://arxiv.org/ 。GEEvoのHTML本文を取得。
- Factorio公式サイト配下Wiki — https://wiki.factorio.com/ 。共同編集資料として補足に使う。

## 原文（sources/）

原文はリポジトリ外のローカル調査用フォルダに保存し、公開資料の再配布と個人名の転載を避ける。保存に成功して本文を確認した資料だけを下表に追加する。

| 論文・資料 | ローカル原文の識別子と該当行 | 元のURL | どこから来たか |
|---|---|---|---|
| 〔公式〕Friday Facts #275: 0.17 Science changes（2018） | factorio-2018-science.md:100、206〜232 | https://www.factorio.com/blog/post/fff-275 | 1周目・英日両探索 |
| 〔公式〕Friday Facts #217: Just another Friday Facts（2017） | factorio-2017-resources.md:96〜114 | https://www.factorio.com/blog/post/fff-217 | 1周目・需要比率の用語 |
| 〔公式〕Friday Facts #159: Research revolution（2016） | factorio-2016-research.md:1、記事本文 | https://www.factorio.com/blog/post/fff-159 | 1周目・#275から過去の設計へ |
| 〔公式〕Devlog 016: The evolution of the Research System（2024） | shapez-2024-research.md:45〜67 | https://steamcommunity.com/games/2162800/announcements/detail/4103413768091304193 | 1周目・英日両探索。本文は公式告知フィードで取得 |
| 〔論文〕GEEvo: Game Economy Generation and Balancing with Evolutionary Algorithms（2024） | research-2024-geevo.md:196〜202、449〜473 | https://arxiv.org/html/2404.18574v1 | 1周目・研究の探索 |
| 〔論文〕Simulating Mechanics to Study Emergence in Games（2011） | research-2011-economy.pdf、research-2011-economy.txt:107〜132 | https://ojs.aaai.org/index.php/AIIDE/article/view/12477 | 2周目・資源フロー研究を遡る |
| 〔公式〕shapez 2 製品説明（取得時点） | shapez-store.md:431〜435 | https://store.steampowered.com/app/2162800/shapez_2/ | 2周目・開発記事から製品の前提へ |
| 〔公式〕Factory Town: Progress update on Rebalance patch（2019） | factorytown-2019-rebalance.md:322〜342 | https://store.steampowered.com/news/posts/?appids=860890&enddate=1564409079 | 2周目・復帰不能の問題。公式告知フィードで取得 |
| 〔まとめ〕Science pack（公式サイト配下Wiki・共同編集） | factorio-wiki-science.md:551〜565 | https://wiki.factorio.com/Science_pack | 2周目・同じ機械等級という比率の前提を補足 |

## 次の手がかり

- 実機の補給分布・初回生産・配送・組み合わせを測る。現時点では知識の原文が付いた範囲で探索を終了し、この項目は未検証として残す。
- Satisfactoryの代替レシピ、戦略の多様性に関する追加研究、原記事の公開数値表は未調査。今回の数値の根拠に使わない。
