# Penguin v001：Unity取り込み用

このフォルダ内のFBXとPNGをまとめてUnityプロジェクトの `Assets` 以下へコピーしてください。`Penguin_Static_1p3m.fbx` は配置用の静止モデル、`Penguin_Waddle_1p3m.fbx` は同じテクスチャを使う約2秒の足踏みアニメーション付きモデルです。歩行版には足のBlend Shapeと親Transformの動きを含みます。歩行版を使うときは、そのFBXのモデルをシーンに置いてください。

両FBXはペンギンを1メッシュに整理し、UV0と `Penguin_Unity_PBR` マテリアルを含みます。静止版は身長1.3m、足元が原点、正面はローカル `-Y` です。歩行版は姿勢で見かけの高さが少し変わります。元の `Penguin.fbx` に含まれていた作業用Cube・Light・Cameraは含みません。

## テクスチャ

| ファイル | Unityでの用途 |
| --- | --- |
| `Penguin_DefaultMaterial_BaseColor.png` | Base Map / Albedo |
| `Penguin_DefaultMaterial_Normal.png` | Normal Map。UnityでTexture TypeをNormal mapに変更 |
| `Penguin_Unity_MetallicSmoothness.png` | Metallic Map。RGBにMetallic、Alphaに `1 - Roughness` |
| `Penguin_DefaultMaterial_Roughness.png` | 元のRoughness。直接Smoothnessへは使わない |
| `Penguin_DefaultMaterial_Metallic.png` | 元のMetallic |
| `Penguin_DefaultMaterial_Emissive.png` | 必要な場合だけEmission |
| `Penguin_DefaultMaterial_Height.png` | 必要な場合だけHeight/Parallax |

FBXにはBaseColor、Normal、元のRoughness・Metallic画像へのマテリアル参照を入れています。Unityが画像を自動で結び付けなかった場合は、Built-inのStandardまたはURPのLitマテリアルを作り、上表の画像を設定してください。Unityのマテリアル・シェーダー設定は利用中のRender Pipelineに合わせて保存してください。

歩行版はUnityのModel Import SettingsでAnimationを有効にし、クリップのLoop Timeを有効にしてください。BlenderでFBXを再読込した結果、左右の足上げがあり、ループの始点と終点は一致します。Unity内でのクリップ表示はライセンスサービスの不調により未確認です。

`Penguin_UV0_Layout.png` は元テクスチャの上にUVを重ねた確認用画像で、ゲームには読み込まないため `ArtSource/Penguin_v001/` に置いています。`Penguin_Unity_v001.blend` は親のPenguinフォルダにある静止版Blenderファイルで、プレビューとマテリアル確認に使えます。
