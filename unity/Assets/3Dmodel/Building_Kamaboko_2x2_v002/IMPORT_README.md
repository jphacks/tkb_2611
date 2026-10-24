# かまぼこ工場 v002

大きなピンク・白のかまぼこ形の屋根と、魚→蒸し器→完成品の表示工程。

再生成：BlenderでArtSource/ProductionIsland_v002/generate_kamaboko.pyを実行し、Unity EditorのProduction Island > Build Kamaboko Design Revisionを実行。確認シーン：Assets/Scenes/KamabokoDesignReview.unity。

専用KamabokoProductionCycleが工程を7.5秒で繰り返す。SetRunning(false)で工程停止、RestartCycle()で投入から再開。生産計算や在庫は含まない。汎用ProductionIslandMotionの_Anim処理とは併用しない。
