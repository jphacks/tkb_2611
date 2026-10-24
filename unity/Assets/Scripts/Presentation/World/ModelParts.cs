namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 施設の FBX (Blender で作る) の中で、名前で探して動かす部品。モデル側で名前を変えるときは、ここも一緒に直す。
    /// </summary>
    public static class ModelParts
    {
        /// <summary>
        /// <see cref="ProductionIslandMotion"/> が動かす部品の名前の末尾。動き方は名前に含む語 (<see cref="Motion"/>) で決まる。
        /// </summary>
        public const string AnimSuffix = "_Anim";

        /// <summary>強火・弱火で出し分ける炎 (名前の先頭)。</summary>
        public const string FlameHigh = "FlameHigh";

        public const string FlameLow = "FlameLow";

        /// <summary><see cref="ProductionIslandMotion"/> の動き方を決める、部品名に含む語。上から順に当てはめる。</summary>
        public static class Motion
        {
            /// <summary>Z 軸で回り続ける (風車の羽根)。</summary>
            public const string Rotor = "Rotor";

            /// <summary>X 軸で回り続ける (水車)。</summary>
            public const string Waterwheel = "Waterwheel";

            /// <summary>Y 軸で回り続ける (網焼き・仕分けの皿)。</summary>
            public const string Grill = "Grill";

            public const string SortingPlate = "SortingPlate";

            /// <summary>上下に動く (包丁・網・蓋)。</summary>
            public const string Knife = "Knife";

            public const string Net = "Net";
            public const string Lid = "Lid";

            /// <summary>開け閉めする (扉)。</summary>
            public const string Door = "Door";

            /// <summary>揺らめく (炎)。</summary>
            public const string Flame = "Flame";
        }

        /// <summary><see cref="FacilityProductionCycle"/> が探す、あれば使う部品。</summary>
        public static class Cycle
        {
            public const string SecondaryTool = "SecondaryTool";
            public const string FluidLevel = "FluidLevel";

            /// <summary>材料を入れる口 (Charge0〜2)。</summary>
            public static readonly string[] Charges = { "Charge0", "Charge1", "Charge2" };

            /// <summary>品物の通り道 (Route0〜2)。</summary>
            public static readonly string[] Routes = { "Route0", "Route1", "Route2" };
        }
    }
}
