using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>記録で受け取った資源。記録の種類 (「歩数 → ガス」など) から決める。</summary>
    public enum VillageRecordResource { Gas, Electricity, Water }

    /// <summary>ある日の、資源ごとの受け取った量。</summary>
    public sealed class VillageRecordDay
    {
        public int Day { get; internal set; }
        public double Gas { get; internal set; }
        public double Electricity { get; internal set; }
        public double Water { get; internal set; }
        public double Total => Gas + Electricity + Water;
        public double Of(VillageRecordResource r) => r == VillageRecordResource.Gas ? Gas : r == VillageRecordResource.Electricity ? Electricity : Water;
    }

    /// <summary>履歴の 1 行 (受け取った記録 1 件)。</summary>
    public sealed class VillageRecordLine
    {
        public int Day { get; internal set; }
        public VillageRecordResource Resource { get; internal set; }
        /// <summary>表示する種類 (「歩数」「水分補給」「スクリーンタイム」)。</summary>
        public string Title { get; internal set; }
        public double Amount { get; internal set; }
    }

    /// <summary>記録の画面に出すまとめ。今日・この 7 日・日ごとの履歴。</summary>
    public sealed class VillageRecordSummary
    {
        /// <summary>この歩数ごとにガスが 1 たまる (VillageGame の AcceptSteps と同じ)。</summary>
        public const int StepsPerGas = 500;
        public const int WeekDays = 7, HistoryDays = 30;

        public int Today { get; private set; }
        /// <summary>今日の歩数。今日まだ受け取っていなければ 0。</summary>
        public int TodaySteps { get; private set; }
        /// <summary>次のガスまでの残りの歩数 (1〜500)。</summary>
        public int StepsToNextGas => StepsPerGas - TodaySteps % StepsPerGas;
        /// <summary>次のガスまでの進み具合 (0〜1)。</summary>
        public double NextGasProgress => TodaySteps % StepsPerGas / (double)StepsPerGas;
        public VillageRecordDay TodayTotals { get; private set; }
        /// <summary>古い順の 7 日 (最後が今日)。記録が無い日も 0 で並べる。</summary>
        public IReadOnlyList<VillageRecordDay> Week { get; private set; }
        /// <summary>新しい日から順の履歴 (最新 30 日)。同じ日の中は記録の順。</summary>
        public IReadOnlyList<IGrouping<int, VillageRecordLine>> History { get; private set; }
        public bool Empty => History.Count == 0;

        public static VillageRecordSummary From(HealthRecordLedger ledger, int today)
        {
            var entries = (ledger?.Entries ?? new List<HealthRecordEntry>())
                .Where(e => e != null && e.Day <= today && !double.IsNaN(e.Amount) && !double.IsInfinity(e.Amount) && e.Amount > 0)
                .Select(e => (Entry: e, Resource: ResourceOf(e.Kind)))
                .Where(x => x.Resource.HasValue)
                .ToList();

            VillageRecordDay DayOf(int day)
            {
                var items = entries.Where(x => x.Entry.Day == day).ToList();
                double Sum(VillageRecordResource r) => items.Where(x => x.Resource == r).Sum(x => x.Entry.Amount);
                return new VillageRecordDay { Day = day, Gas = Sum(VillageRecordResource.Gas), Electricity = Sum(VillageRecordResource.Electricity), Water = Sum(VillageRecordResource.Water) };
            }

            var history = entries
                .Where(x => x.Entry.Day > today - HistoryDays)
                .Select(x => new VillageRecordLine { Day = x.Entry.Day, Resource = x.Resource.Value, Title = TitleOf(x.Entry.Kind), Amount = x.Entry.Amount })
                .GroupBy(l => l.Day)
                .OrderByDescending(g => g.Key)
                .ToArray();

            return new VillageRecordSummary
            {
                Today = today,
                TodaySteps = ledger != null && ledger.LastStepDay == today ? Math.Max(0, ledger.MaxSteps) : 0,
                TodayTotals = DayOf(today),
                Week = Enumerable.Range(0, WeekDays).Select(i => DayOf(today - (WeekDays - 1) + i)).ToArray(),
                History = history,
            };
        }

        /// <summary>記録の種類 (「歩数 → ガス」) の矢印の後ろから資源を決める。分からなければ null。</summary>
        public static VillageRecordResource? ResourceOf(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            var arrow = kind.IndexOf('→');
            var target = arrow >= 0 ? kind.Substring(arrow + 1).Trim() : kind;
            return target.Contains("ガス") ? VillageRecordResource.Gas
                : target.Contains("電気") ? VillageRecordResource.Electricity
                : target.Contains("水") ? VillageRecordResource.Water
                : (VillageRecordResource?)null;
        }

        /// <summary>記録の種類の矢印の前 (「歩数」「水分補給」など)。</summary>
        public static string TitleOf(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "";
            var arrow = kind.IndexOf('→');
            return arrow > 0 ? kind.Substring(0, arrow).Trim() : kind;
        }
    }
}
