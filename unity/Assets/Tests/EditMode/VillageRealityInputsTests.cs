using System;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Reality;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>村のホームのスクリーンタイム (スクショの読み取り → 村の規則) と、すれ違いの台帳・報酬・保存。</summary>
    public sealed class VillageRealityInputsTests
    {
        private const int Day = 297; // 2026-10-25 (アプリの日)

        private static VillageGame Empty()
        {
            var g = new VillageGame(false);
            g.SetResources(0, 0, 0);
            return g;
        }

        /// <summary>日を始め、相手を見て、報告を 1 件取り出す。</summary>
        private static (EncounterReport Report, string PairId) Meet(VillageGame g, long now = 1000)
        {
            if (g.EncounterDay < Day)
            {
                Assert.That(g.BeginEncounterDay(Day).Success, Is.True);
            }

            var peer = EncounterToken.Create();
            Assert.That(g.ObservePeer(peer, now).Success, Is.True);
            Assert.That(g.TakeEncounterReport(now, out var report).Success, Is.True);
            Assert.That(report, Is.Not.Null);
            return (report, EncounterToken.PairId(g.EncounterSelfToken, peer));
        }

        [Test]
        public void YesterdaysScreenshotBecomesElectricityByTheVillageRule()
        {
            var now = DateTimeOffset.UtcNow;
            var today = JstCalendar.DayNumber(now);
            var capture = FakeScreenTimeSource.Sample(JstCalendar.ToJst(now).Date.AddDays(-1), 4 * 60 + 2);
            var result = ScreenTimeOcrParser.Parse(capture.Lines, now);
            Assert.That(result.Succeeded && !result.IsToday, Is.True);

            var g = Empty();
            var id = "screen:" + capture.ImageHash;
            Assert.That(g.AcceptScreen(id, result.Reading.Day, result.Reading.TotalMinutes, today, true, true).Success, Is.True);
            Assert.That(g.CreateView().Electricity, Is.EqualTo((1440 - 242) / 120));
            Assert.That(g.AcceptScreen(id, result.Reading.Day, result.Reading.TotalMinutes, today, true, true).Success, Is.False, "同じ画像は 1 回だけ");
            Assert.That(g.AcceptScreen("screen:other", result.Reading.Day, 100, today, true, true).Success, Is.False, "同じ日は 1 回だけ");
        }

        [Test]
        public void TodaysScreenshotIsNotAccepted()
        {
            var now = DateTimeOffset.UtcNow;
            var capture = FakeScreenTimeSource.Sample(JstCalendar.ToJst(now).Date, 60, isToday: true);
            var result = ScreenTimeOcrParser.Parse(capture.Lines, now);
            Assert.That(result.IsToday, Is.True);
            var today = JstCalendar.DayNumber(now);
            Assert.That(Empty().AcceptScreen("screen:x", result.Reading.Day, 60, today, false, true).Success, Is.False);
        }

        [Test]
        public void MatchedEncounterGrantsTheDrawnSpeciesResourcesOnce()
        {
            var g = Empty();
            var (report, pairId) = Meet(g);
            var result = g.ApplyEncounter(report, true, pairId, 2000, out var view);
            Assert.That(result.Success, Is.True);
            Assert.That(view.Outcome, Is.EqualTo(EncounterOutcome.Granted));

            var species = EncounterPenguins.Draw(pairId);
            Assert.That(view.Species.Id, Is.EqualTo(species.Id));
            int Amount(EncounterResource r) => species.Resource == r ? VillageGame.EncounterResourceAmount
                : species.Resource == EncounterResource.None ? VillageGame.EncounterMixedAmount : 0;
            var after = g.CreateView();
            Assert.That(after.Gas, Is.EqualTo(Amount(EncounterResource.Gas)));
            Assert.That(after.Electricity, Is.EqualTo(Amount(EncounterResource.Electricity)));
            Assert.That(after.Water, Is.EqualTo(Amount(EncounterResource.Water)));
            Assert.That(g.Records.Entries.TrueForAll(e => e.Id.StartsWith("encounter:" + pairId + ":", StringComparison.Ordinal)), Is.True);

            Assert.That(g.ApplyEncounter(report, true, pairId, 3000, out var again).Success, Is.False, "同じ組は 2 回受け取らない");
            Assert.That(again.Outcome, Is.EqualTo(EncounterOutcome.Ignored));
        }

        [Test]
        public void WaitingAndWrongPairIdGiveNothing()
        {
            var g = Empty();
            var (report, pairId) = Meet(g);
            Assert.That(g.ApplyEncounter(report, false, pairId, 2000, out var waiting).Success, Is.True);
            Assert.That(waiting.Outcome, Is.EqualTo(EncounterOutcome.Waiting));
            Assert.That(g.ApplyEncounter(report, true, "0000000000000000", 2000, out var wrong).Success, Is.False);
            Assert.That(wrong.Outcome, Is.EqualTo(EncounterOutcome.Ignored));
            Assert.That(g.Records.Entries, Is.Empty);
        }

        [Test]
        public void DailyCapStopsRewardsButStillMatches()
        {
            var g = Empty();
            for (var i = 0; i < EncounterLedger.PenguinsPerDay; i++)
            {
                var (r, p) = Meet(g);
                Assert.That(g.ApplyEncounter(r, true, p, 2000, out var v).Success && v.Outcome == EncounterOutcome.Granted, Is.True);
            }

            var before = g.Records.Entries.Count;
            var (report, pairId) = Meet(g);
            Assert.That(g.ApplyEncounter(report, true, pairId, 2000, out var capped).Success, Is.True);
            Assert.That(capped.Outcome, Is.EqualTo(EncounterOutcome.CapReached));
            Assert.That(g.Records.Entries.Count, Is.EqualTo(before));
        }

        [Test]
        public void LedgerSurvivesTheVillageSaveThroughJson()
        {
            var g = Empty();
            var (report, pairId) = Meet(g);
            g.ApplyEncounter(report, true, pairId, 2000, out _);
            Meet(g);

            var json = JsonUtility.ToJson(g.Export());
            var restored = VillageGame.Restore(JsonUtility.FromJson<VillageState>(json));
            Assert.That(restored.EncounterDay, Is.EqualTo(Day));
            Assert.That(restored.EncounterSelfToken, Is.EqualTo(g.EncounterSelfToken));
            Assert.That(restored.EncountersSeen, Is.EqualTo(2));
            Assert.That(restored.EncountersGranted, Is.EqualTo(1));
            Assert.That(restored.EncountersWaiting, Is.EqualTo(1));
            Assert.That(restored.BeginEncounterDay(Day).Success, Is.False, "同じ日はトークンを作り直さない");
        }

        [Test]
        public void OldSaveWithoutEncountersReadsAsAnEmptyLedger()
        {
            var state = Empty().Export();
            state.Encounters = null;
            Assert.That(VillageGame.Restore(state).EncounterDay, Is.EqualTo(-1));

            var json = JsonUtility.ToJson(Empty().Export()).Replace("\"Encounters\"", "\"Removed\"");
            Assert.That(VillageGame.Restore(JsonUtility.FromJson<VillageState>(json)).EncounterDay, Is.EqualTo(-1));
        }

        [Test]
        public void BrokenLedgerIsRejected()
        {
            var g = Empty();
            Meet(g);
            var state = g.Export();
            state.Encounters.SelfToken = "broken";
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
        }

        [Test]
        public void SessionKeepsTheLedgerAndOnlyAnnouncesRewards()
        {
            var store = new Store();
            var core = new ProductionVillageSessionCore(store, new Store(), Empty, true);
            core.Open(10000);
            var notice = core.Notice;
            Assert.That(core.BeginEncounterDay(Day, 10000), Is.True);
            var peer = EncounterToken.Create();
            Assert.That(core.ObservePeer(peer, 10000), Is.True);
            Assert.That(core.ObservePeer(peer, 10000), Is.False, "同じ相手は記録しない");
            Assert.That(core.Notice, Is.EqualTo(notice), "台帳の更新は通知を上書きしない");

            var report = core.NextEncounterReport(10000);
            Assert.That(report, Is.Not.Null);
            Assert.That(core.NextEncounterReport(10000), Is.Null, "結果が来るまで同じ相手を選ばない");
            var outcome = core.ApplyEncounter(report, true, EncounterToken.PairId(core.EncounterSelfToken, peer), 10000);
            Assert.That(outcome, Is.EqualTo(EncounterOutcome.Granted));
            Assert.That(core.Notice, Does.StartWith("すれ違い成立"));

            var reopened = new ProductionVillageSessionCore(store, new Store(), () => throw new InvalidOperationException("保存を読むはず"), true);
            reopened.Open(20000);
            Assert.That(reopened.Encounters, Is.EqualTo((1, 0, 1)));
            Assert.That(reopened.EncounterSelfToken, Is.EqualTo(core.EncounterSelfToken));
        }

        private sealed class Store : IProductionVillageStore
        {
            private string _json;
            public ProductionVillageSave Load() => _json == null ? null : JsonUtility.FromJson<ProductionVillageSave>(_json);
            public void Save(ProductionVillageSave save) => _json = JsonUtility.ToJson(save);
        }
    }
}
