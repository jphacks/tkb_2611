# 港の桟橋 v007

ユーザー制作の `Gyojou_UV_Materials_v007.blend` から書き出した港モデルです。

- 配置用：`Harbor_Gyojou_v007.prefab`
- 横幅1.4m、長さ約3.56m、高さ約1.70m。床上面が原点の高さです。
- 4メッシュ、29,008三角形。木・塗装金属・鋼・縄・マットの5マテリアル。
- FBXの軸と単位の変換はModelの子に保持し、配置用の親は回転0・倍率1です。
- 床と巻き上げ機のColliderは港画面を開くためのTriggerです。
- 全6港Lvに同じモデルを設定しています。Lv判定と注文・納品・報酬は既存のままです。
- `Production Island > Setup Gyojou Harbor` で再設定できます。全体の `Setup Home Island` でもこの港の参照と既存の接続位置を維持します。
- 桟橋の接続位置は `World/HomeIceberg/HarborAnchor` で調整します。港の向きはUnityのY軸（BlenderのZ軸）で設定します。

元のBlendと旧港のアセットは保持しています。
