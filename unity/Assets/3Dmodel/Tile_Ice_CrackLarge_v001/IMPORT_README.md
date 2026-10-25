# Tile_Ice_CrackLarge v001

依頼 ID: T-02。92 三角形。1 単位 = 1m。足元中心が原点、Unity Y=0 が地面、正面 -Z。

## 中身

- FBX: 別メッシュの動く部品を含むモデル。
- 共有アトラスと材質: Assets/3Dmodel/ProductionIsland_v001/。1024×1024 の Base Map。Normal は不要。
- 編集用 .blend: ArtSource/Tile_Ice_CrackLarge_v001/。

## Unity

Production Island > Build Asset Pack を実行すると、URP Lit、稼働中・停止マテリアル、Prefab を作成します。色は各メッシュの UV でアトラスを参照します。停止は彩度の低い別マテリアルに切り替えます。

動く部品: なし。ゲーム規則や自動配置は含みません。
