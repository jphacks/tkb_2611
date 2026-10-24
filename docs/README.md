# 設計・開発ドキュメント

このリポジトリには、backend と API 契約の設計資料を置く。既存ゲームの仕様・クライアントの実装は project-penguin にある。

2026-10-24 に決まった新しい製品方針は、`product-direction/` にまとめている。目的や課題の仮説、既存仕様との違い、旧機能の見直しについては、まずこちらを参照する。

## 新しい製品方針

日常の行動で得たエネルギーを使い、工場をつないで育てる iOS ゲーム。中心となる機能は、4マスの建物をズームアウトすると別の建物になる「組み合わせ」。確定した仕様と、仮説・提案・未決事項を分けて記載している。

| 文書 | 内容 |
|---|---|
| [方向性とゲーム仕様](product-direction/README.md) | 目的・課題の仮説、現実の行動とエネルギーの対応、工場・拡張・レベル上げ用の納品・ペンギンの解放と獲得のルール |
| [組み合わせと倉庫の提案](product-direction/logistics-proposal.md) | 不規則な配置の扱い、自動配送、大型倉庫、階層をまたぐ資源の中継 |
| [施設・資源の次数とバランス](product-direction/factory-flow-proposal.md) | 加工の次数、施設候補、生産比率、エネルギー、納品評価の案 |
| [ペンギンの種類と効果](product-direction/penguin-effects-proposal.md) | 既存19種の所持効果、羽数による成長、すれ違いの出現確率、新種保証 |
| [ドメイン知識](domain/README.md) | 工場ゲームの原典調査、設計の知識、数量検証、収集効果の考え方 |
| [競合・既存コードの調査](product-direction/research.md) | 競合との比較、参考にする実装、iOS 連携と性能の検証事項 |
| [ペルソナ分析](product-direction/personas.md) | 行動・動機に基づく利用者像の仮説と検証方法 |
| [課題・対象者・独自性と発表の整理](product-direction/positioning.md) | セルフケアを包括する課題候補、日常行動を工場のエネルギー源にする特徴、参考採点、90秒発表案 |
| [確認事項](product-direction/questions.md) | 回答済みの仕様と、優先順位を付けた未決事項 |

## backend と API の設計

既存の構成・契約・開発手順をまとめた資料。新しい製品方針を文書化した段階であり、既存のコードや API 契約は変更していない。

| 文書 | 内容 |
|---|---|
| [`architecture.md`](architecture.md) | Unity の Web ビルド・Vercel・FastAPI・AI の構成、公開の流れ、設定ファイルの切り替え |
| [`submission.md`](submission.md) | JPHACKS の登録フォームに記入するチーム名・アイデア・技術 |
| [`hackday.md`](hackday.md) | 事前開発と当日開発の区分、3 人の分担、当日の流れ、完了の定義 |
| [`decisions.md`](decisions.md) | このリポジトリに関わる決定と理由 |
| [`personal-info.md`](personal-info.md) | 個人に関わる内容を入れないためのフック・CI・拒否リスト |
| [`../api/README.md`](../api/README.md) | API 契約の読み方と変更手順 |

## 仕様の置き場所 (project-penguin)

以下は既存版の仕様を確かめるための参照先。各仕様の正本はリンク先にあり、このリポジトリには写さない。新しい製品方針との違いや、引き継ぐ機能の判断は `product-direction/` に記載する。

| 知りたいこと | 文書 |
|---|---|
| 写真の表 (写っているもの → 資源) と ID | [`docs/production-island/resources.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/resources.md) の「写真の表」 |
| 写真判定の API の要点・decision の扱い | [`docs/production-island/game-design.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/game-design.md) の §12 |
| 既存版の「生産の島」の規則 | [`docs/production-island/game-design.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/game-design.md) |
| チームの決定事項・JPHACKS の規定 | [`docs/decisions.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/decisions.md) |
| Web 版の公開手順・Vercel の制約 | [`docs/web-deploy.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/web-deploy.md) |
| プライバシーポリシー | [`docs/privacy-policy.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/privacy-policy.md) |

## Issue (project-penguin)

Hack Day までの Issue は project-penguin で管理している。backend に関係するものを以下に挙げる。

- [#112](https://github.com/Sh1n1230/project-penguin/issues/112) 写真判定の検証層 (確信度・使い回し・画面撮影・換算表)
- [#113](https://github.com/Sh1n1230/project-penguin/issues/113) みんなの氷山
- [#123](https://github.com/Sh1n1230/project-penguin/issues/123) 期間限定のお題
- [#125](https://github.com/Sh1n1230/project-penguin/issues/125) 判定に使う AI の選定 (学習に使われない条件・費用)
- [#89](https://github.com/Sh1n1230/project-penguin/issues/89) 評価用の写真セット、[#90](https://github.com/Sh1n1230/project-penguin/issues/90) 排出係数・換算の根拠
- [#119](https://github.com/Sh1n1230/project-penguin/issues/119) このリポジトリの構成、[#120](https://github.com/Sh1n1230/project-penguin/issues/120) 契約
