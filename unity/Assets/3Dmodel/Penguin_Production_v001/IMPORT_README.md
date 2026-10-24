# Penguin Production v001

P-01 CarryWalk / P-02 WorkLoop / P-03 Idle / P-04 Cheer。

元データは Penguin_v001/Penguin_Waddle_1p3m.fbx。元の親子階層、メッシュ、足のブレンドシェイプを維持し、ヒレと頭のブレンドシェイプを追加しています。元モデルにはボーンがありません。v002 の静止モデルへクリップだけを直接割り当てる構成ではありません。

24fps の一本のベイク済みタイムラインを Unity Editor の Build Asset Pack が4つに切り分けます。

| clip | start | end | loop |
|---|---:|---:|---|
| CarryWalk | 0 | 48 | yes |
| WorkLoop | 60 | 108 | yes |
| Idle | 120 | 192 | yes |
| Cheer | 204 | 252 | no |

CarrySocket に Item_* の Prefab を子として付けてください。アニメーションはその場で動き、経路移動はゲーム側で制御します。正面 -Z、足元 Y=0、元モデルと同じ約1.3m。PNG は元モデルのテクスチャをコピーしています。URP Lit の Base Map / Normal Map に元と同じ画像を指定してください。

編集元: ArtSource/Penguin_Production_v001/Penguin_Production_v001.blend。
