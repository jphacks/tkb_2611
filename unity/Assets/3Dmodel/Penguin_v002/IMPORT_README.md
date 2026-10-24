# Penguin v002：Unity取り込み用

Blenderから書き出したペンギンの静止モデルです。`Penguin_Static_v002.fbx` と同じフォルダのPNG 2枚をまとめて `Assets` 以下に置いてください。アニメーションは含みません（歩行が必要な場合は `Penguin_v001/Penguin_Waddle_1p3m.fbx` を使用）。

FBXは複数メッシュ構成で、テクスチャ参照はファイル名 `PG_Body_*.png` で結び付きます。テクスチャ名は変更しないでください。

## テクスチャ（2048×2048）

| ファイル | Unityでの用途 |
| --- | --- |
| `PG_Body_BaseColor.png` | Base Map / Albedo |
| `PG_Body_Normal.png` | Normal Map。UnityでTexture TypeをNormal mapに変更 |

Unityが画像を自動で結び付けなかった場合は、URPのLitマテリアルを作り、上表の画像を設定してモデルに割り当ててください。
