using System;

namespace ProjectPenguin.Domain.Game
{
    /// <summary>
    /// 統合ルール (15d-simulation.md) の定数。game_sim.py の <c>Params</c> と 1 対 1。
    /// 既定値 <see cref="Default"/> は 15d で 2 点を変えた版、<see cref="Decided"/> は 2026-10-15 に決めたままの版。
    /// </summary>
    public sealed class GameParameters
    {
        public double Initial { get; set; } = 100.0;
        public double Floor { get; set; } = 40.0;
        public double Cap { get; set; } = 150.0;
        public double RecordGain { get; set; } = 2.0;
        public double Melt { get; set; } = 2.0;
        public double RescueBelow { get; set; } = 60.0;
        public double RescueMultiplier { get; set; } = 2.0;
        public int ShieldsStart { get; set; } = 2;
        public int ShieldsMax { get; set; } = 2;
        public int ComebackMinGap { get; set; } = 3;
        /// <summary>
        /// おかえりで足す氷の割合 (休んで溶けた量 × この値)。2026-10-16 に 0 にした: おかえりのごほうびは氷ではなく、
        /// 留守の間に島が作ったお魚 (<c>IslandGame.OfflineReport</c>)。来訪の合図 (ガラパゴス) の条件は残る。0.5 にすると v3/v4 の挙動に戻る。
        /// </summary>
        public double ComebackRatio { get; set; }
        public int ComebackMax { get; set; } = 10;
        public double WeeklyBonusMax { get; set; } = 4.0;

        /// <summary>段 T1〜T4 の下端 (T0 は下限から)。</summary>
        public double[] TierBounds { get; set; } = { 60.0, 80.0, 100.0, 125.0 };

        /// <summary>下がるのは 境目 − この値 を下回ったとき。15d で 4 → 6 (週 2 の人の段のちらつき)。</summary>
        public double TierHysteresis { get; set; } = 6.0;

        public int WeeklyArrivalMinDays { get; set; } = 3;

        /// <summary>初回起動のアデリーペンギンを「週 1 羽」の枠に数えない (15d の変更)。</summary>
        public bool OnboardingExemptFromCap { get; set; } = true;

        public int SelectionsPerDay { get; set; } = 2;
        public int GoldPoints { get; set; } = 2;
        public int WhitePoints { get; set; } = 1;
        public int WhitePerDay { get; set; } = 1;
        public int WhitePerWeek { get; set; } = 5;

        // ---- v4 (調査 17a。docs/game-logic.md) ----

        /// <summary>おかえりは「盾で守れず氷が溶けた日が 1 日以上あった後の最初の記録」で出す。false なら v3 の「記録のない日が <see cref="ComebackMinGap"/> 日以上」。</summary>
        public bool ComebackRequiresMelt { get; set; } = true;

        /// <summary>時計が最後に見た日より前に戻ったとき、操作を最後に見た日のものとして扱う。false なら v3 の「何もしない」。</summary>
        public bool RollbackClamp { get; set; } = true;

        /// <summary>
        /// 信頼できない時計 (<see cref="Clock.ITrustedClock"/> でない、または IsTrusted が false) で、最後に見た日よりこの日数を超えて進んだら
        /// 確定させずに保留する。null なら制限なし。調査は 14 を勧めるが、サーバーの時計 (backend の Date ヘッダー) が無いうちに 14 にすると、
        /// 2 週間以上あけて戻った人が「保留」で止まるので、サーバーができるまでは null にしておく。
        /// </summary>
        public int? MaxCatchUpDays { get; set; }

        // ---- 生産の島とのつなぎ (2026-10-17。docs/game-logic.md §2・§5) ----

        /// <summary>
        /// 前の週に 3 日以上記録したら、週の順番表の次の種が来る。false にすると週の来訪を止め、節目 (はじめて・累計 14 日など) だけで来る。
        /// アプリでは週の順の 11 種を島の実績 (<c>PenguinUnlocks</c>) で解放するので false にする (<see cref="App"/>)。
        /// </summary>
        public bool WeeklyArrivals { get; set; } = true;

        /// <summary>溶ける量を減らす割合の上限 (氷室の効き目。<see cref="GameSession.SetMeltReduction"/>)。</summary>
        public double MaxMeltReduction { get; set; } = 0.8;

        /// <summary>v4 の推奨値 (15d の値 + 17a の修正)。氷のおかえりは 0 (留守の間のお魚に一本化)。</summary>
        public static GameParameters Default => new GameParameters();

        /// <summary>アプリで使う値。<see cref="Default"/> から週の来訪だけを止める (種は島の実績で解放する)。</summary>
        public static GameParameters App => new GameParameters { WeeklyArrivals = false };

        /// <summary>15d (v3) のままの値。おかえりは空白 3 日以上、巻き戻しは無視、先送りの制限なし。</summary>
        public static GameParameters V3 => new GameParameters { ComebackRatio = 0.5, ComebackRequiresMelt = false, RollbackClamp = false, MaxCatchUpDays = null };

        /// <summary>2026-10-15 に決めたままの値 (段の幅 4、初回のアデリーも週の枠に数える。v4 の修正も入れない)。</summary>
        public static GameParameters Decided => new GameParameters
        {
            TierHysteresis = 4.0, OnboardingExemptFromCap = false, ComebackRatio = 0.5, ComebackRequiresMelt = false, RollbackClamp = false, MaxCatchUpDays = null,
        };
    }

    /// <summary>氷の見た目の段 (15c §1.2)。</summary>
    public static class IceTierRule
    {
        /// <summary>上がるのは境目に届いた時点で即、下がるのは 境目 − 幅 を下回ったときだけ。何段でも一度に動く。</summary>
        public static int Next(int current, double ice, GameParameters p)
        {
            if (p == null)
            {
                throw new ArgumentNullException(nameof(p));
            }

            var t = current;
            while (t < p.TierBounds.Length && ice >= p.TierBounds[t])
            {
                t++;
            }

            while (t > 0 && ice < p.TierBounds[t - 1] - p.TierHysteresis)
            {
                t--;
            }

            return t;
        }

        /// <summary>履歴のない段 (初期表示)。</summary>
        public static int Of(double ice, GameParameters p)
        {
            var t = 0;
            foreach (var b in p.TierBounds)
            {
                if (ice >= b)
                {
                    t++;
                }
            }

            return t;
        }
    }

    /// <summary>アプリの日 (03:00 JST 区切り) と週 (月曜 03:00 始まり)。</summary>
    public static class AppCalendar
    {
        public static readonly TimeSpan DayBoundary = TimeSpan.FromHours(3);

        /// <summary>(時刻 − 3 時間) の JST の日番号。2026-01-02 02:59 JST は 2026-01-01 の日 (0)。</summary>
        public static int AppDay(DateTimeOffset instant)
        {
            return Clock.JstCalendar.DayNumber(instant - DayBoundary);
        }

        public static int WeekOf(int appDay)
        {
            return Clock.JstCalendar.WeekOfDay(appDay);
        }

        public static bool IsMonday(int appDay)
        {
            return (((appDay + 3) % 7) + 7) % 7 == 0;
        }

        /// <summary>0.5 は切り上げ (Math.Round の偶数丸めは使わない。Python 側と合わせる)。</summary>
        public static int RoundHalfUp(double x)
        {
            return (int)Math.Floor(x + 0.5);
        }
    }
}
