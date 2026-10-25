# サードパーティの素材とデータ

このリポジトリのうち、チーム ueco が作ったもの (ソースコード・ドキュメント・自作の 3D モデルや画像) は [`LICENSE`](LICENSE) の MIT ライセンスで提供する。

下に挙げる外部の素材・データは MIT の対象ではなく、**それぞれの元のライセンスや利用規約に従う**。新しく外部のものを取り込んだら、同じコミットでこの一覧に足す。

## 同梱しているもの

| 対象 | 場所 | ライセンス |
|---|---|---|
| M PLUS Rounded 1c | `Assets/UI/Fonts/` | SIL Open Font License 1.1 (`OFL.txt` 同梱) |
| Oxanium (数字用。Bold と ExtraBold の静的版) | `Assets/UI/Fonts/` | SIL Open Font License 1.1 (`Oxanium-OFL.txt` 同梱。Copyright 2019 The Oxanium Project Authors) |
| TextMesh Pro の素材 | `Assets/TextMesh Pro/` | Unity Companion License |
| Liberation Sans (TextMesh Pro に同梱) | `Assets/TextMesh Pro/Fonts/` | SIL Open Font License 1.1 (`LiberationSans - OFL.txt` 同梱) |
| Unity のパッケージ | `Packages/manifest.json` で参照 (リポジトリには含まない) | 各パッケージのライセンス (多くは Unity Companion License) |
| SQLite-net for Unity (`com.gilzoide.sqlite-net` 1.3.3) | `Packages/manifest.json` で参照 (リポジトリには含まない)。端末の保存 (`Assets/Scripts/Data/`) に使う | MIT。同梱の SQLite-net は MIT、SQLite3 Multiple Ciphers は MIT、その元の SQLite はパブリックドメイン |
| Phosphor Icons v2.1.1 | `tools/icons/vendor/phosphor/` (原本と `LICENSE`) と、そこから書き出した `Assets/UI/DesignSystem/Icons/` のアイコン。対応は `tools/icons/icons.json` | MIT (Copyright (c) 2023 Phosphor Icons)。全文をアプリ内のライセンス表示 (`Assets/Scripts/Presentation/UI/ThirdPartyNotices.cs`) に載せる |
| エージェント用の外部スキル | `.agents/skills/`・`.claude/skills/` のうち `skills-lock.json` にあるもの | 提供元のライセンス (Unity-Technologies/skills は Unity Companion License、mattpocock/skills は MIT) |

## このプロジェクト向けに生成した画像

`Assets/ProductionIsland/QualityAreas_v001/Fish.png` と `SaltCrystal.png` は、2026-10-18にユーザーが確認した案をOpenAI画像生成ツールで制作したもの。魚は既存のItem_Fish_v001を参考に胴体を曲線化し、塩は四角い粒の山で表す。制作指示とハッシュは`ArtSource/ResourceAreaIcons_v002/README.md`に記録している。

これらは以前のシルエットAC由来の魚・鉱石画像を置き換える。旧画像と元ZIPは今回の共有候補に含めず、旧画像のライセンスを確認済みとは扱わない。海藻画像はユーザー提供のまま。

## 値や記述を参照している外部データ

ゲームの数値 (ミッションの削減量の目安、食品の排出係数など) や学びカードの文は、公開されている資料から取る。値を使うときは画面と文書に出典を書き、元の規約の範囲で使う。

| データ | 規約・ライセンス | 扱い |
|---|---|---|
| 環境省 デコ活データベース | 利用規約 (令和 8 年 3 月 16 日)。「デコ活応援団」への参画が利用の条件 | 参画するまで、DB 由来の値はリポジトリに入れない |
| 国立環境研究所「脱炭素型ライフスタイルの選択肢」データブック | CC BY-NC 4.0 | 出典を書き、非営利の範囲で使う |
| 環境省 ゼロカーボンアクション30、資源エネルギー庁 省エネポータル | 各サイトの利用規約 (政府標準利用規約) | 出典を書いて使う |
| 食品の排出係数の論文 (Sugimoto ら 2020、Clune ら 2017 など) | 各論文の条件 | 値を引用し、出典を書く |
| ペンギンの種と保全状況 (IOC World Bird List、IUCN レッドリスト) | 各サイトの規約 | 事実の記述にとどめ、出典を書く |

## 確認が要るもの

- `Assets/3Dmodel/Penguin_v001/` は、元の `Penguin.fbx` を取り込み用に整理したものと `IMPORT_README.md` に書かれている。元のモデルの作者とライセンスが記録に無いので、外部の素材なら作者・ライセンスをこの一覧に足す。
