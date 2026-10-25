# UI のアイコン

`Assets/UI/DesignSystem/Icons/*.svg` は、ここにある原本から `build_icons.py` で書き出す。Assets の SVG を直接書き換えない (CI が原本と一致するかを確かめる)。

- **既製**: [Phosphor Icons](https://phosphoricons.com/) v2.1.1 (MIT)。原本は `vendor/phosphor/<太さ>/` に置き、`vendor/phosphor/LICENSE` を同梱する。
- **自作**: `custom/<名前>.svg`。Phosphor に合う形が無い品目 (塩・かまぼこ・ちくわ・干物・おでん) を、Phosphor の形を改変して作る。すり身 (`bowl-food`) と海藻 (`plant`) は今は Phosphor で代用している。
- **ペンギンの顔** (`.pp-icon--penguin`) は SVG ではなく、3D モデルを描き出した多色の PNG (`penguin-mascot.png`、**Production Island > Render Mascot Icon**)。この仕組みの対象外。
- **対応表**: `icons.json` に、ゲームのアイコン名 → 元を書く。

## 手順

リポジトリのルートで実行する。

```bash
python unity/tools/icons/build_icons.py --fetch            # icons.json にある Phosphor の原本を取る (ネットが要る)
python unity/tools/icons/build_icons.py --preview out.html # 見比べページ (今 / 選んだもの / 候補)
python unity/tools/icons/build_icons.py                    # Assets へ書き出す
python unity/tools/icons/build_icons.py --check            # Assets が原本どおりか (CI)
python scripts/check_svg_icons.py                          # 白一色の決まりに合うか (CI)
```

- **アイコンを足す**: `icons.json` に 1 行足して `--fetch` と書き出しを流す。初めて Unity に取り込むときに `.meta` ができるので、SVG と一緒にコミットする。`Components.uss` に `.pp-icon--<名前>` を足し、C# から使うときは `UiClasses.Icon` に定数を足す。
- **差し替える**: `icons.json` の `phosphor` を変えて流す。同じファイル名に上書きするので `.meta` (GUID) は変わらず、参照もそのまま使える。
- **意味が変わるとき**: 上書きせず、新しい名前で足す。アイコンは全画面で共有しているので、上書きすると、ほかの画面の意味まで変わってしまう。
- **候補を並べて選ぶ**: `alternatives` に `"名前"` か `"名前:太さ"` を書くと、見比べページにだけ並ぶ。決まったら消す。

## 太さ

形のあるもの (炎・魚・箱…) は `fill`。記号 (✓・✕・＋・−・‹ ›・矢印・≡) は Fill だと四角の枠に入ってしまうので、`bold` にする。

## 自作の描き方の決まり

並べたときに Phosphor と見分けがつかないことが目標。

- `viewBox="0 0 256 256"`。絵は外周 16〜24 の余白の内側に収める。
- くり抜き・隙間・細い部分の幅は **16** (Phosphor の線の太さと同じ。20px に縮めても潰れない)。
- 角の丸みは半径 8 前後。線の端は丸く止める。
- 色は白 (`#FFFFFF`) か `currentColor`。書き出しで白にそろえる。
- `<path>` などの図形だけで描き、くり抜きは `fill-rule="evenodd"` で作る。`<style>`・`<mask>`・`<filter>`・`<text>`・グラデーションは使わない (`scripts/check_svg_icons.py` が止める)。
- 近い形の Phosphor の原本を写して改変すると、太さと丸みがそろう。

**進め方**: ユーザーのイメージ画像を参考に、2〜3 案を `custom/<名前>.draft-a.svg` のように置く。見比べページで選んだ案を `custom/<名前>.svg` にして書き出し、案のファイルは消す。
