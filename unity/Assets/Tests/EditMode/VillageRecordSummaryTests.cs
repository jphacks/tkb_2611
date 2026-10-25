using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageRecordSummaryTests
    {
        private const int Today = 20000;
        private static readonly string Steps = "歩数 " + '→' + " ガス", Water = "水分補給 " + '→' + " 水", Screen = "スクリーンタイム " + '→' + " 電気";

        private static HealthRecordLedger Ledger(params (string Kind, int Day, double Amount)[] entries)
        {
            var l = new HealthRecordLedger();
            foreach (var e in entries) l.Entries.Add(new HealthRecordEntry { Id = e.Kind + e.Day + e.Amount, Kind = e.Kind, Day = e.Day, Amount = e.Amount });
            return l;
        }

        [Test] public void EmptyLedgerHasZeroWeekAndNoHistory()
        {
            var s = VillageRecordSummary.From(new HealthRecordLedger(), Today);
            Assert.That(s.Empty, Is.True);
            Assert.That(s.TodaySteps, Is.Zero);
            Assert.That(s.StepsToNextGas, Is.EqualTo(VillageRecordSummary.StepsPerGas));
            Assert.That(s.Week.Count, Is.EqualTo(7));
            Assert.That(s.Week.Last().Day, Is.EqualTo(Today), "最後が今日");
            Assert.That(s.Week.All(d => d.Total == 0), Is.True);
            Assert.That(VillageRecordSummary.From(null, Today).Empty, Is.True);
        }

        [Test] public void TodayStepsComeOnlyFromToday()
        {
            var l = new HealthRecordLedger { LastStepDay = Today, MaxSteps = 1234 };
            var s = VillageRecordSummary.From(l, Today);
            Assert.That(s.TodaySteps, Is.EqualTo(1234));
            Assert.That(s.StepsToNextGas, Is.EqualTo(266), "1234 歩なら次のガスまで 266 歩");
            Assert.That(s.NextGasProgress, Is.EqualTo(234 / 500.0).Within(1e-9));

            l.LastStepDay = Today - 1;
            Assert.That(VillageRecordSummary.From(l, Today).TodaySteps, Is.Zero, "昨日の歩数は今日に数えない");
        }

        [Test] public void TotalsAreGroupedByResourceAndDay()
        {
            var l = Ledger((Steps, Today, 2), (Water, Today, 3), (Screen, Today, 5), (Steps, Today - 1, 4), (Steps, Today - 6, 1), (Steps, Today - 7, 9));
            var s = VillageRecordSummary.From(l, Today);
            Assert.That(s.TodayTotals.Gas, Is.EqualTo(2));
            Assert.That(s.TodayTotals.Water, Is.EqualTo(3));
            Assert.That(s.TodayTotals.Electricity, Is.EqualTo(5));
            Assert.That(s.Week.Select(d => d.Gas), Is.EqualTo(new double[] { 1, 0, 0, 0, 0, 4, 2 }), "7 日前の記録は 7 日の棒に入れない");
            Assert.That(s.History.Select(g => g.Key), Is.EqualTo(new[] { Today, Today - 1, Today - 6, Today - 7 }), "新しい日から順");
            Assert.That(s.History.First().Count(), Is.EqualTo(3));
        }

        [Test] public void HistoryKeepsThirtyDaysAndIgnoresBadEntries()
        {
            var l = Ledger((Steps, Today - 29, 1), (Steps, Today - 30, 1), (Steps, Today + 1, 1), ("ふしぎな記録", Today, 1), (Steps, Today, double.NaN), (Steps, Today, -2));
            var s = VillageRecordSummary.From(l, Today);
            Assert.That(s.History.Select(g => g.Key), Is.EqualTo(new[] { Today - 29 }), "30 日より前・未来・分からない種類・不正な量は出さない");
        }

        [Test] public void KindIsSplitIntoTitleAndResource()
        {
            Assert.That(VillageRecordSummary.ResourceOf(Steps), Is.EqualTo(VillageRecordResource.Gas));
            Assert.That(VillageRecordSummary.ResourceOf(Water), Is.EqualTo(VillageRecordResource.Water));
            Assert.That(VillageRecordSummary.ResourceOf(Screen), Is.EqualTo(VillageRecordResource.Electricity));
            Assert.That(VillageRecordSummary.ResourceOf(""), Is.Null);
            Assert.That(VillageRecordSummary.TitleOf(Water), Is.EqualTo("水分補給"));
        }
    }
}
