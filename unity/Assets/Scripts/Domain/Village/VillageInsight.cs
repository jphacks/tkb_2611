using System;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>
    /// 生産レポートや各種UIで使う計算ロジック (UnityEngine 非依存の純粋関数)。
    /// </summary>
    public static class VillageInsight
    {
        /// <summary>
        /// 本日の供給ペース (日平均ペース)。
        /// 朝方に跳ね上がらないよう経過分は最低 60 分として扱う。
        /// </summary>
        public static double SupplyPerMinute(double receivedToday, double minutesSinceMidnight)
        {
            if (receivedToday <= 0 || double.IsNaN(receivedToday) || double.IsInfinity(receivedToday)) return 0;
            var minutes = Math.Max(60.0, minutesSinceMidnight);
            return receivedToday / minutes;
        }

        /// <summary>
        /// あと何分でガスが空になるか。収支が 0 以上なら null (減っていない)。
        /// </summary>
        public static int? MinutesUntilEmpty(double gas, double net)
        {
            if (net >= -1e-6 || double.IsNaN(net) || double.IsInfinity(net)) return null;
            if (gas <= 0 || double.IsNaN(gas) || double.IsInfinity(gas)) return 0;
            return (int)Math.Floor(gas / -net);
        }

        /// <summary>
        /// タンクの水位割合 (0.0 〜 1.0)。
        /// 今の消費の 1 時間ぶん (usePerMinute * 60) を満タンとする。
        /// 消費が 0 のときは満タン (1.0) 固定。
        /// </summary>
        public static double TankLevel(double gas, double usePerMinute)
        {
            if (gas <= 0 || double.IsNaN(gas) || double.IsInfinity(gas)) return 0;
            if (usePerMinute <= 1e-6 || double.IsNaN(usePerMinute) || double.IsInfinity(usePerMinute)) return 1.0;
            var cap = usePerMinute * 60.0;
            return Math.Min(1.0, Math.Max(0.0, gas / cap));
        }

        /// <summary>
        /// タンクの目盛り上の満タン容量 (今の消費 1 時間ぶん)。
        /// 消費が 0 のときは null (分母非表示)。
        /// </summary>
        public static double? TankCapacity(double usePerMinute)
        {
            if (usePerMinute <= 1e-6 || double.IsNaN(usePerMinute) || double.IsInfinity(usePerMinute)) return null;
            return usePerMinute * 60.0;
        }
    }
}
