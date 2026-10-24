using System;

namespace ProjectPenguin.Domain.World
{
    /// <summary>ペンギンの気分。段から決め、レシートの点では変えない (docs/game-logic.md §2.1、15c §1.4)。</summary>
    public enum PenguinMood
    {
        /// <summary>寄り添って温まる (T0・T1)。悲しむ・泣く表現は使わない。</summary>
        Cold,
        Normal,
        Happy,
    }

    /// <summary>
    /// 氷の値と段から、ホームの見た目を決める規則 (15c §1.3・§1.4)。
    /// 段そのものの判定 (ヒステリシス) は <see cref="Game.IceTierRule"/> が持ち、ここは段の後の見せ方だけを扱う。
    /// </summary>
    public static class IceVisual
    {
        public const int TierCount = 5;

        private static readonly string[] TierNames = { "ちいさな氷", "ひびの入った氷", "いつもの氷", "ゆたかな氷", "ひろがる氷" };

        // 段ごとの、氷に乗るペンギンの上限。超えた分は小屋で休む (15c §1.4)。
        private static readonly int[] OnIceCapacity = { 3, 5, 8, 12, int.MaxValue };

        // 段ごとのひびの本数 (15c §1.3: T2 は細いひび 1〜2 本、T1 は 2 本、T0 は 3 本)。
        private static readonly int[] Cracks = { 3, 2, 2, 0, 0 };

        // 段ごとの流氷の数 (15c §1.3)。
        private static readonly int[] Floes = { 0, 0, 1, 2, 3 };

        /// <summary>
        /// 氷山の水平の大きさ。面積が氷の値に比例するよう √(氷/100)。下限 40 で約 0.63 倍、上限 150 で約 1.22 倍。
        /// </summary>
        public static double AreaScale(double ice)
        {
            return Math.Sqrt(Math.Max(0.0, ice) / 100.0);
        }

        public static PenguinMood MoodOf(int tier)
        {
            var t = Clamp(tier);
            return t >= 3 ? PenguinMood.Happy : t == 2 ? PenguinMood.Normal : PenguinMood.Cold;
        }

        /// <summary>歩く速さの倍率。さむそうな段ではゆっくり歩く (15c §1.4 の T1 の 0.7 倍)。</summary>
        public static double WalkSpeedMultiplier(int tier)
        {
            return MoodOf(tier) == PenguinMood.Cold ? 0.7 : 1.0;
        }

        /// <summary>氷に乗るペンギンの数。残りは小屋で休む。図鑑からもホームからも誰もいなくならない。</summary>
        public static int PenguinsOnIce(int tier, int penguins)
        {
            return Math.Max(0, Math.Min(penguins, OnIceCapacity[Clamp(tier)]));
        }

        public static int CrackCount(int tier)
        {
            return Cracks[Clamp(tier)];
        }

        public static int FloeCount(int tier)
        {
            return Floes[Clamp(tier)];
        }

        /// <summary>UI に出す段の名前。</summary>
        public static string TierName(int tier)
        {
            return TierNames[Clamp(tier)];
        }

        private static int Clamp(int tier)
        {
            return Math.Max(0, Math.Min(TierCount - 1, tier));
        }
    }
}
