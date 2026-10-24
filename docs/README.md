# docs

このリポジトリ (backend と契約) の設計メモ。ゲームそのものの仕様は project-penguin にある。

| 文書 | 中身 |
|---|---|
| [`architecture.md`](architecture.md) | 全体の構成 (Unity の Web ビルド ⇄ Vercel ⇄ FastAPI ⇄ AI)、公開の流れ、設定ファイルの切り替え |
| [`hackday.md`](hackday.md) | 事前と当日の境界、3 人の分担、当日の流れ、完了の定義 |
| [`decisions.md`](decisions.md) | このリポジトリに関わる決定と理由 |
| [`personal-info.md`](personal-info.md) | 個人に関わる内容を入れないための仕組み (フック・CI・拒否リスト) |
| [`../api/README.md`](../api/README.md) | 契約の読み方と変え方 |

## 仕様の置き場所 (project-penguin)

backend の実装で確かめることが多いものだけを挙げる。リンク先が正本で、ここには写さない。

| 知りたいこと | 文書 |
|---|---|
| 写真の表 (写っているもの → 資源) と ID | [`docs/production-island/resources.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/resources.md) の「写真の表」 |
| 写真判定の API の要点・decision の扱い | [`docs/production-island/game-design.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/game-design.md) の §12 |
| 生産の島 (ゲームの主軸) の規則 | [`docs/production-island/game-design.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/production-island/game-design.md) |
| チームの決定事項・JPHACKS の規定 | [`docs/decisions.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/decisions.md) |
| Web 版の公開手順・Vercel の制約 | [`docs/web-deploy.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/web-deploy.md) |
| プライバシーポリシー | [`docs/privacy-policy.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/privacy-policy.md) |

## Issue (project-penguin)

Hack Day まで Issue は project-penguin で管理している。backend に関わるもの:

- [#112](https://github.com/Sh1n1230/project-penguin/issues/112) 写真判定の検証層 (確信度・使い回し・画面撮影・換算表)
- [#113](https://github.com/Sh1n1230/project-penguin/issues/113) みんなの氷山
- [#123](https://github.com/Sh1n1230/project-penguin/issues/123) 期間限定のお題
- [#125](https://github.com/Sh1n1230/project-penguin/issues/125) 判定に使う AI の選定 (学習に使われない条件・費用)
- [#89](https://github.com/Sh1n1230/project-penguin/issues/89) 評価用の写真セット、[#90](https://github.com/Sh1n1230/project-penguin/issues/90) 排出係数・換算の根拠
- [#119](https://github.com/Sh1n1230/project-penguin/issues/119) このリポジトリの構成、[#120](https://github.com/Sh1n1230/project-penguin/issues/120) 契約
