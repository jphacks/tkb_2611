# 個人に関わる内容を入れないための仕組み

このリポジトリは jphacks の org に public で残り続け、消せない前提で扱う。**人の名前・連絡先・個人の事情・チームの会話の中身は、どこにも書かない** (`CLAUDE.md` の絶対規則 0)。人は役割 (「公開役」「担当 A」) で書く。

GitHub では、一度 push したものは、ブランチを消しても書き換えても PR の履歴から見られる。だから**止めるのは push の前**で、CI は最後の確認に使う。使う人のエージェントやエディタの設定に頼らないよう、何重にも止める。

## 4 つの層

| 層 | いつ | 誰に効くか | 中身 |
|---|---|---|---|
| `CLAUDE.md`・`AGENTS.md` | 作業を始めるとき | エージェント全般 | 決まりと理由。強制力は無い |
| Claude Code のフック (`.claude/hooks/guard_personal.py`) | ファイルを書く前・`git commit`・`git tag`・`gh pr/issue create` などの前 | Claude Code で開いた人 | 書く中身と、コミットメッセージ・PR の本文 (`--body-file` のファイルも) を見て止める |
| git のフック (`.githooks/`) | `git commit` (pre-commit・commit-msg) と `git push` (pre-push) | フックを有効にした clone (どのエージェント・エディタでも) | ステージした追加行、コミットメッセージ、push するコミット全部を見て止める |
| CI (`.github/workflows/ci.yml` の「個人に関わる内容の検査」) | PR と main への push | 全員 | 追跡しているファイル全部、PR のコミットの追加行とメッセージ、PR の題と本文 |

検出の規則はすべて `scripts/check_personal_info.py` にまとめてある。

- 形の決まったもの: メールアドレス (noreply と例示用のドメインは除く)、電話番号、郵便番号。
- 拒否リストの語: 名前・ニックネーム・アカウント名など。**リストはリポジトリに入れない** (入れたらリストそのものが漏れる)。

見つかったときに出すのは、場所と種類だけ。見つかった語そのものは出さない (CI のログも public のため)。

## clone したら最初にやること

```bash
sh scripts/setup_hooks.sh      # git のフックを有効にする (core.hooksPath = .githooks)
```

Claude Code で開いた場合は、起動時に自動で同じ設定をする (`.claude/settings.json` の SessionStart)。ほかのエージェントやエディタでは、上のコマンドを 1 回実行する。

### 拒否リストを作る

次のどちらかに、1 行に 1 語ずつ書く (`#` から始まる行は無視される)。チームの人の本名 (姓・名・フルネーム)、ニックネーム、ふだん使うアカウント名、学校名など、出てほしくない語を入れる。

- `~/.config/ueco/personal-denylist` (おすすめ。どの clone でも効く)
- リポジトリ直下の `.personal-denylist` (`.gitignore` 済み。誤って追跡すると検査が落ちる)

リストの中身はチャット・Issue・リポジトリに貼らない。Claude Code はこのファイルを読めないように設定してある (`.claude/settings.json` の deny)。

英数字だけの語は単語として探す (「Fictional」で「fictionalize」に当てない)。日本語の名前は部分一致で探す。

### CI に拒否リストを渡す (リポジトリの管理者が 1 回だけ)

```bash
gh secret set PERSONAL_DENYLIST -R jphacks/tkb_2611 < ~/.config/ueco/personal-denylist
```

Secret の値はログに出ない。設定していない間は、CI に注意が出て、名前の検査だけが抜ける。

## 引っかかったとき

- 名前を役割に書き換える。連絡先は消す。
- 検査の誤りで止まった (例: 名前と同じ綴りの普通の単語) なら、拒否リストの語を見直す。**フックを外したり `--no-verify` で通したりしない。**
- push してしまった後に気付いたら、すぐにリポジトリの管理者に知らせる。PR の履歴からの削除は GitHub Support に頼む必要がある。
