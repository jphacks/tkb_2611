using System;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>
    /// 生産の島の数値 (docs/production-island/game-design.md)。調整はここだけで済むよう、規則の数値はすべてここに置く。
    /// </summary>
    public sealed class IslandParameters
    {
        // ---- 盤面 ----

        /// <summary>盤面の幅と高さ (マス)。</summary>
        public int BoardSize { get; set; } = 10;

        /// <summary>上端から何列が水路か。港は水路に浮かぶ。</summary>
        public int CanalRows { get; set; } = 2;

        /// <summary>基礎の島 (沈まない) の左下と大きさ。</summary>
        public int BaseX { get; set; } = 3;

        public int BaseY { get; set; } = 2;
        public int BaseSize { get; set; } = 4;

        // ---- 氷と土地 ----

        /// <summary>氷の下限。これ以下では開拓したマスを 1 つも支えられない。</summary>
        public double IceFloor { get; set; } = 40.0;

        /// <summary>開拓したマス 1 つを支えるのに要る氷。</summary>
        public double IcePerTile { get; set; } = 2.5;

        /// <summary>次に沈むマスとして、ひびで予告する数。</summary>
        public int SinkWarningTiles { get; set; } = 3;

        // ---- 開拓 ----

        public double ExpandWaterBase { get; set; } = 5.0;
        public double ExpandWaterGrowth { get; set; } = 1.15;
        public double ExpandFishBase { get; set; } = 10.0;
        public double ExpandFishGrowth { get; set; } = 1.2;
        public int ExpandExp { get; set; } = 5;

        /// <summary>レベル 1 で開拓できるマス数と、レベルごとの追加。</summary>
        public int ExpandLimitBase { get; set; } = 4;

        public int ExpandLimitPerLevel { get; set; } = 4;

        // ---- 資源の初期値 ----

        public double StartElectricity { get; set; } = 30.0;
        public double StartWater { get; set; } = 20.0;
        public double StartGas { get; set; } = 30.0;
        public double StartAllMighty { get; set; } = 0.0;
        public double StartFish { get; set; } = 30.0;

        // ---- 現実の行動から得る資源 ----

        /// <summary>写真で判定したカテゴリの資源に入る量。</summary>
        public double PhotoMainGrant { get; set; } = 20.0;

        /// <summary>写真で、判定したカテゴリ以外の電気・水・ガスに少しずつ入る量。</summary>
        public double PhotoSideGrant { get; set; } = 5.0;

        /// <summary>
        /// 判定サーバーが「見分けの確かさが中くらい」(decision: reduce) と返した写真の資源に掛ける割合。
        /// </summary>
        public double PhotoReducedRate { get; set; } = 0.5;

        /// <summary>写真の資源を受け取れるのは 1 日何枚まで。記録としては 1 枚で足りる。</summary>
        public int PhotosPerDay { get; set; } = 1;

        /// <summary>何歩でガスが 1 になるか。</summary>
        public int StepsPerGas { get; set; } = 500;

        /// <summary>歩数から得るガスの 1 日の上限。</summary>
        public double StepGasPerDayMax { get; set; } = 20.0;

        /// <summary>ミッションの振り返りで「できた」と答えたときのオールマイティ。</summary>
        public double MissionAllMighty { get; set; } = 5.0;

        /// <summary>夜の振り返りで「できなかった」「少しだけ」と答えたときのオールマイティ。正直に答えても損をしない。</summary>
        public double MissionTriedAllMighty { get; set; } = 2.0;

        /// <summary>増幅施設 1 棟の倍率の上乗せ (1 棟で 1.25 倍、2 棟で 1.5 倍)。</summary>
        public double AmplifierBonus { get; set; } = 0.25;

        // ---- ペンギン (働き手) ----

        /// <summary>
        /// 最初からいる働き手。看板のペンギン 1 羽から始め、記録した日ごとに増える (最初の写真で 2 羽になり、氷室と漁場を動かせる)。
        /// 2026-10-17 に 3 から 1 にした (オーナー)。
        /// </summary>
        public int StartWorkers { get; set; } = 1;

        /// <summary>記録日 1 日につき増える働き手。</summary>
        public int WorkersPerRecordDay { get; set; } = 1;

        /// <summary>宿舎が無くても住める数と、宿舎 1 棟の定員。</summary>
        public int BaseHousing { get; set; } = 4;

        public int HousingPerDormitory { get; set; } = 4;

        // ---- 氷とペンギンのやる気 (2026-10-17 のコア) ----

        /// <summary>
        /// 氷の段 (T0〜T4) ごとのペンギンのやる気。作る速さ・増幅所の上乗せ・氷室の効き目に掛かる。
        /// 氷が大きいほど上がり、小さくても 1.0 より下げない (罰にしない。docs/game-logic.md §0)。
        /// </summary>
        public double[] MotivationByTier { get; set; } = { 1.0, 1.0, 1.0, 1.15, 1.3 };

        /// <summary>氷室 Lv1 が減らす、記録しなかった日に溶ける氷の割合。</summary>
        public double IceHouseReduction { get; set; } = 0.3;

        /// <summary>氷室 1 レベルごとに増える割合 (Lv5 で 0.7)。</summary>
        public double IceHouseReductionPerLevel { get; set; } = 0.1;

        /// <summary>氷室の効き目の上限 (やる気を掛けた後)。<c>GameParameters.MaxMeltReduction</c> と同じ値にする。</summary>
        public double MaxMeltReduction { get; set; } = 0.8;

        // ---- 在庫 ----

        /// <summary>倉庫が無いときの、お魚以外の品目の置き場の上限 (品目ごと)。</summary>
        public int BaseStorage { get; set; } = 20;

        public int StoragePerWarehouse { get; set; } = 30;

        // ---- 施設の強化 (docs/production-island/facilities.md) ----

        /// <summary>施設のレベルの上限。島のレベルより上には強化できない。</summary>
        public int FacilityMaxLevel { get; set; } = 5;

        /// <summary>作る施設の速さの上乗せ (1 レベルごと)。ガスの消費は変えないので、強化するほど効率が上がる。</summary>
        public double UpgradeSpeedPerLevel { get; set; } = 0.25;

        /// <summary>宿舎の定員の上乗せ (1 レベルごと)。倉庫は置き場 × レベルになる。</summary>
        public int UpgradeHousingPerLevel { get; set; } = 2;

        /// <summary>増幅所 1 棟の上乗せの増え方 (1 レベルごと)。</summary>
        public double UpgradeAmplifierPerLevel { get; set; } = 0.05;

        /// <summary>仕分け所 1 レベルごとに、次の舟が来るまでを縮める秒数。</summary>
        public double UpgradeOrderCooldownPerLevel { get; set; } = 5.0;

        /// <summary>強化のお魚 = (建てるお魚 + これ) × 2^今のレベル。電気 = 建てる電気 × 今のレベル。</summary>
        public double UpgradeFishBase { get; set; } = 10.0;

        // ---- 時間 ----

        /// <summary>閉じていた間の生産をまとめて計算する上限 (秒)。</summary>
        public double OfflineCapSeconds { get; set; } = 12 * 3600.0;

        /// <summary>計算の刻み (秒)。</summary>
        public double TickSeconds { get; set; } = 1.0;

        /// <summary>質の良いマスの上に建てた施設の速さの倍率。</summary>
        public double QualityTileSpeed { get; set; } = 1.5;

        /// <summary>真珠 1 つで上がる施設の速さ (加算) と、1 施設に入れられる数。</summary>
        public double PearlSpeed { get; set; } = 0.1;

        public int PearlsPerFacility { get; set; } = 5;

        // ---- 港 ----

        public int OrderSlots { get; set; } = 3;

        /// <summary>納品してから次の舟が来るまで (秒)。</summary>
        public double OrderCooldownSeconds { get; set; } = 45.0;

        /// <summary>注文の報酬 = 品目の値 × 個数 × この倍率。</summary>
        public double OrderRewardMultiplier { get; set; } = 1.5;

        /// <summary>港の見た目の段階 (0〜5) が上がるのに要る納品の累計。</summary>
        public int[] HarborStageDeliveries { get; set; } = { 1, 5, 12, 25, 45 };

        // ---- レベル ----

        /// <summary>レベル n → n+1 に要る経験値 = Base × n^Exponent (切り上げ)。</summary>
        public double LevelExpBase { get; set; } = 20.0;

        public double LevelExpExponent { get; set; } = 1.5;

        public int MaxLevel { get; set; } = 30;

        public static IslandParameters Default => new IslandParameters();

        public double ExpandWaterCost(int expanded) => Math.Ceiling(ExpandWaterBase * Math.Pow(ExpandWaterGrowth, expanded));

        public double ExpandFishCost(int expanded) => Math.Ceiling(ExpandFishBase * Math.Pow(ExpandFishGrowth, expanded));

        public int ExpandLimit(int level) => ExpandLimitBase + (Math.Max(1, level) - 1) * ExpandLimitPerLevel;

        /// <summary>レベル <paramref name="level"/> から 1 つ上げる電気。</summary>
        public double UpgradeElectricityCost(FacilityDef def, int level) => Math.Ceiling(def.ElectricityCost * Math.Max(1, level));

        /// <summary>レベル <paramref name="level"/> から 1 つ上げるお魚。</summary>
        public double UpgradeFishCost(FacilityDef def, int level) => Math.Ceiling((def.FishCost + UpgradeFishBase) * Math.Pow(2, Math.Max(1, level)));

        public int ExpToNext(int level) => (int)Math.Ceiling(LevelExpBase * Math.Pow(Math.Max(1, level), LevelExpExponent));
    }
}
