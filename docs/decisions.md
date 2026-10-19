# 決定事項 (tkb_2611)

このリポジトリに関わる決定だけを書く。チーム全体の決定と JPHACKS の規定の正本は [project-penguin の `docs/decisions.md`](https://github.com/Sh1n1230/project-penguin/blob/main/docs/decisions.md)。

- **このリポジトリには backend と契約と README だけを置き、Unity クライアントは project-penguin に残して Web 版のリンクで渡す。** 差分評価で当日の成果がリポジトリの形のまま見え、LFS の移行や Unity の CI の移設が要らないため。 (2026-10-19、project-penguin #119)
- **Unity との契約の正本はこのリポジトリの `api/` とする。** 当日に契約を広げるのは backend の担当なので。project-penguin の `api/` は写しで、`check_api_contract.py --against` で一致を確かめる。 (2026-10-19)
- **このリポジトリは Vercel に繋がない。** 公開役が project-penguin の `deploy_web.py --backend ../tkb_2611/backend` で上げる。jphacks の org に外部アプリの承認を頼まずに済み、Web ビルドと同じ URL に置けるため。 (2026-10-19)
- **当日は Unity を作り直さない。** 変えてよいのは公開先の `penguin-config.json` だけ。そのために、検証の結果・CO₂e・みんなの氷山・お題まで、契約とクライアントの受け口を事前に作った。 (2026-10-19)
- **接続を足す・変えるときは、先に契約に書く。** 契約に無い通信はクライアントが受け取れず、当日まで食い違いに気付けないため。 (2026-10-19)
- **個人に関わる内容 (名前・連絡先・個人の事情・チームの会話の中身) をこのリポジトリに入れない。** org がある限りリポジトリは残り、push したものは消せないため。決まりだけでなく、Claude Code のフック・git のフック・CI で機械的に止める (`docs/personal-info.md`)。名前の拒否リストはリポジトリに入れず、手元のファイルと CI の Secret で渡す。 (2026-10-19)
- **LICENSE は運営の MIT のまま。** (2026-10-15、project-penguin の決定)
- **JPHACKS の後、製品の開発は project-penguin に戻し、`git subtree add` でこのリポジトリの backend を履歴ごと取り込む。** Web 版と Vercel のプロジェクトは審査が終わったら閉じる。 (2026-10-19)

## 未決

- 判定に使う AI (Gemini 有料枠か Workers AI か、#125)。
- みんなの氷山の件数の保存先 (Upstash の無料枠を想定)。
- 著作権表示に `ueco` を併記するか。
