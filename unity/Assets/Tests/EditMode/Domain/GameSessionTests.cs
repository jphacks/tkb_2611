using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>ペルソナの操作 1 つ (GameSimGolden 用)。</summary>
    internal sealed class GameEvent
    {
        public DateTimeOffset At;
        public string Op;
        public string Id;
        public int PrintDay;
        public string[] Verifies;
        public MissionVerification Verification;
        public CheckInAnswer Answer;
        public bool NoShopping;

        public static GameEvent Receipt(DateTimeOffset at, string id, int printDay, string[] verifies)
        {
            return new GameEvent { At = at, Op = "receipt", Id = id, PrintDay = printDay, Verifies = verifies };
        }

        public static GameEvent Select(DateTimeOffset at, string id, MissionVerification v)
        {
            return new GameEvent { At = at, Op = "select", Id = id, Verification = v };
        }

        public static GameEvent CheckIn(DateTimeOffset at, string id, CheckInAnswer a, bool noShopping)
        {
            return new GameEvent { At = at, Op = "checkin", Id = id, Answer = a, NoShopping = noShopping };
        }
    }

    /// <summary>統合ルールの単体テスト (game_sim_test.py と対応)。</summary>
    public sealed class GameSessionTests
    {
        private static DateTimeOffset T(int y, int mo, int d, int h, int mi = 0)
        {
            return new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(9));
        }

        private static readonly Func<int, IReadOnlyList<ReceiptRecord>, double> Bonus0 = (w, rs) => 0.0;

        private ManualClock _clock;

        /// <summary>氷のおかえり (溶けた量の半分、上限 10) を戻した値。v3/v4 の挙動を確かめるため。</summary>
        private static GameParameters IceComeback => new GameParameters { ComebackRatio = 0.5 };

        private GameSession New(DateTimeOffset start, GameParameters p = null, Func<int, IReadOnlyList<ReceiptRecord>, double> bonus = null)
        {
            _clock = new ManualClock(start);
            return new GameSession(_clock, bonus ?? Bonus0, p);
        }

        private bool Scan(GameSession s, DateTimeOffset at, string id, int printDay = 0, params string[] verifies)
        {
            _clock.Set(at);
            return s.ScanReceipt(new ReceiptRecord(id, printDay, verifies));
        }

        private void At(DateTimeOffset at)
        {
            _clock.Set(at);
        }

        [Test]
        public void DayBoundaryIs0300Jst()
        {
            Assert.That(AppCalendar.AppDay(T(2026, 1, 2, 2, 59)), Is.EqualTo(0));
            Assert.That(AppCalendar.AppDay(T(2026, 1, 2, 3, 0)), Is.EqualTo(1));
            Assert.That(AppCalendar.IsMonday(JstCalendar.DayNumber(new DateTime(2026, 11, 2))), Is.True);
            Assert.That(AppCalendar.RoundHalfUp(2.5), Is.EqualTo(3));
            Assert.That(AppCalendar.RoundHalfUp(0.49), Is.EqualTo(0));
        }

        [Test]
        public void RecordOncePerAppDay()
        {
            var s = New(T(2026, 10, 27, 9));
            Scan(s, T(2026, 10, 27, 10), "a");
            Scan(s, T(2026, 10, 27, 11), "b");
            Assert.That(s.Ice, Is.EqualTo(102.0));
            Scan(s, T(2026, 10, 28, 2, 59), "c");
            Assert.That(s.Ice, Is.EqualTo(102.0));
            Scan(s, T(2026, 10, 28, 3, 1), "d");
            Assert.That(s.Ice, Is.EqualTo(104.0));
        }

        [Test]
        public void FailedCheckInStillRecordsWithoutStamp()
        {
            var s = New(T(2026, 10, 27, 7));
            At(T(2026, 10, 27, 8));
            Assert.That(s.SelectMission("E01", MissionVerification.Self), Is.True);
            At(T(2026, 10, 27, 21));
            Assert.That(s.CheckIn("E01", CheckInAnswer.Failed), Is.True);
            Assert.That(s.Ice, Is.EqualTo(102.0));
            Assert.That(s.WhiteCounted, Is.EqualTo(0));
        }

        [Test]
        public void CheckInWithoutSelectionIsRejected()
        {
            var s = New(T(2026, 10, 27, 7));
            At(T(2026, 10, 27, 21));
            Assert.That(s.CheckIn("E01", CheckInAnswer.Done), Is.False);
            Assert.That(s.Ice, Is.EqualTo(100.0));
        }

        [Test]
        public void ShieldsAbsorbThenMeltAndRefillOnMonday()
        {
            var s = New(T(2026, 10, 27, 9));
            At(T(2026, 11, 1, 12));
            s.Advance();
            Assert.That(s.Shields, Is.EqualTo(0));
            Assert.That(s.Ice, Is.EqualTo(94.0));
            At(T(2026, 11, 2, 12));
            s.Advance();
            Assert.That(s.Ice, Is.EqualTo(92.0));
            Assert.That(s.Shields, Is.EqualTo(2));
            Assert.That(s.ShieldsUsed, Is.EqualTo(2));
        }

        [Test]
        public void ComebackAddsNoIceByDefaultButStillFires()
        {
            // 2026-10-16: おかえりのごほうびは氷ではなく、留守の間に島が作ったお魚。来訪の合図 (ガラパゴス) は残る。
            var s = New(T(2026, 10, 27, 9));
            At(T(2026, 11, 2, 12));
            s.Advance();
            Assert.That(s.GapMelted, Is.EqualTo(8.0));
            Scan(s, T(2026, 11, 2, 13), "a");
            Assert.That(s.Ice, Is.EqualTo(92.0 + 2));
            Assert.That(s.Comebacks, Is.EqualTo(1));
            Assert.That(s.ComebackLog[0].Value, Is.EqualTo(0));
            Assert.That(s.Arrivals.Any(a => a.Species == PenguinCatalog.FirstComeback) || s.MilestoneQueue.Contains(PenguinCatalog.FirstComeback), Is.True);
        }

        [Test]
        public void ComebackAddsHalfOfMeltedWhenTheIceBonusIsOn()
        {
            var s = New(T(2026, 10, 27, 9), IceComeback);
            At(T(2026, 11, 2, 12));
            s.Advance();
            Assert.That(s.GapMelted, Is.EqualTo(8.0));
            Scan(s, T(2026, 11, 2, 13), "a");
            Assert.That(s.Ice, Is.EqualTo(92.0 + 2 + 4));
            Assert.That(s.ComebackLog[0].Value, Is.EqualTo(4));
        }

        [Test]
        public void ComebackCappedAt10AndRescueDoubles()
        {
            var s = New(T(2026, 10, 27, 9), IceComeback);
            At(T(2027, 2, 1, 12));
            s.Advance();
            Assert.That(s.Ice, Is.EqualTo(40.0));
            Scan(s, T(2027, 2, 1, 13), "a");
            Assert.That(s.Ice, Is.EqualTo(54.0));
            Assert.That(s.Tier, Is.EqualTo(0));
        }

        [Test]
        public void GainAboveCapBecomesShards()
        {
            var p = GameParameters.Default;
            p.Initial = 149.0;
            var s = New(T(2026, 10, 27, 9), p, (w, rs) => 2.5);
            var d0 = JstCalendar.DayNumber(new DateTime(2026, 10, 27));
            Scan(s, T(2026, 10, 27, 10), "a", d0);
            Assert.That(s.Ice, Is.EqualTo(150.0));
            Assert.That(s.ShardsExact, Is.EqualTo(1.0));
            for (var i = 1; i < 6; i++)
            {
                Scan(s, T(2026, 10, 27, 10).AddDays(i), "x" + i);
            }

            At(T(2026, 11, 2, 12));
            s.Advance();
            Assert.That(s.ShardsExact, Is.EqualTo(13.5).Within(1e-9));
            Assert.That(s.Shards, Is.EqualTo(13));
        }

        [Test]
        public void ClockRollbackIsIgnoredInV3()
        {
            // v3 の規則。v4 では最後に見た日の操作として受け付ける (GameSessionV4Tests.ClockRollbackClampsToLastSeenDay)。
            var s = New(T(2026, 10, 27, 9), GameParameters.V3);
            At(T(2026, 10, 29, 12));
            s.Advance();
            Assert.That(Scan(s, T(2026, 10, 28, 12), "a"), Is.False);
            Assert.That(s.Ice, Is.EqualTo(100.0));
        }

        [Test]
        public void DuplicateReceiptIsRejected()
        {
            var s = New(T(2026, 10, 27, 9));
            Assert.That(Scan(s, T(2026, 10, 27, 10), "a"), Is.True);
            Assert.That(Scan(s, T(2026, 10, 28, 10), "a"), Is.False);
            Assert.That(s.Ice, Is.EqualTo(102.0));
        }

        [Test]
        public void TierHysteresis()
        {
            var p = GameParameters.Decided;
            Assert.That(IceTierRule.Next(2, 100, p), Is.EqualTo(3));
            Assert.That(IceTierRule.Next(3, 96, p), Is.EqualTo(3));
            Assert.That(IceTierRule.Next(3, 95.9, p), Is.EqualTo(2));
            Assert.That(IceTierRule.Next(0, 130, p), Is.EqualTo(4));
            Assert.That(IceTierRule.Next(4, 40, p), Is.EqualTo(0));
            Assert.That(IceTierRule.Next(3, 94, GameParameters.Default), Is.EqualTo(3));
            Assert.That(IceTierRule.Next(3, 93.9, GameParameters.Default), Is.EqualTo(2));
        }

        [Test]
        public void GoldOnlyWithinPrintWindowAndOnce()
        {
            var s = New(T(2026, 10, 27, 7));
            var d0 = JstCalendar.DayNumber(new DateTime(2026, 10, 27));
            At(T(2026, 10, 27, 8));
            s.SelectMission("F01", MissionVerification.Receipt);
            Scan(s, T(2026, 10, 30, 10), "late", d0 + 3, "F01");
            Assert.That(s.Gold, Is.EqualTo(0));
            Scan(s, T(2026, 10, 30, 11), "ok", d0 + 2, "F01");
            Assert.That(s.Gold, Is.EqualTo(1));
            Scan(s, T(2026, 10, 30, 12), "again", d0 + 2, "F01");
            Assert.That(s.Gold, Is.EqualTo(1));
        }

        [Test]
        public void WhiteStampsCountedOncePerDayFivePerWeek()
        {
            var s = New(T(2026, 11, 2, 7));
            for (var i = 0; i < 7; i++)
            {
                foreach (var m in new[] { "E01", "E02" })
                {
                    At(T(2026, 11, 2, 8).AddDays(i));
                    s.SelectMission(m + i, MissionVerification.Self);
                    At(T(2026, 11, 2, 21).AddDays(i));
                    s.CheckIn(m + i, CheckInAnswer.Done);
                }
            }

            Assert.That(s.WhitePressed, Is.EqualTo(14));
            Assert.That(s.WhiteCounted, Is.EqualTo(5));
            Assert.That(s.StampPoints, Is.EqualTo(5));
        }

        [Test]
        public void AtMostTwoSelectionsPerDay()
        {
            var s = New(T(2026, 11, 2, 7));
            Assert.That(s.SelectMission("a", MissionVerification.Self), Is.True);
            Assert.That(s.SelectMission("b", MissionVerification.Self), Is.True);
            Assert.That(s.SelectMission("c", MissionVerification.Self), Is.False);
        }

        [Test]
        public void MissionsNeverAddIceBeyondTheDailyRecord()
        {
            var s = New(T(2026, 11, 2, 7));
            At(T(2026, 11, 2, 8));
            s.SelectMission("a", MissionVerification.Self);
            s.SelectMission("b", MissionVerification.Self);
            At(T(2026, 11, 2, 21));
            s.CheckIn("a", CheckInAnswer.Done);
            s.CheckIn("b", CheckInAnswer.Done);
            Scan(s, T(2026, 11, 2, 22), "r");
            Assert.That(s.Ice, Is.EqualTo(102.0));
        }

        [Test]
        public void OnboardingAdelieArrivesOnFirstLaunch()
        {
            var s = New(T(2026, 10, 27, 7));
            Assert.That(s.Arrivals.Count, Is.EqualTo(1));
            Assert.That(s.Arrivals[0].Species, Is.EqualTo(PenguinCatalog.Onboarding));
            Assert.That(s.Arrivals[0].Reason, Is.EqualTo(ArrivalReason.Onboarding));
            Scan(s, T(2026, 10, 27, 10), "a");
            Assert.That(s.Arrivals.Count, Is.EqualTo(2));
            Assert.That(s.Arrivals[1].Species, Is.EqualTo(PenguinCatalog.FirstRecord));

            var decided = New(T(2026, 10, 27, 7), GameParameters.Decided);
            Scan(decided, T(2026, 10, 27, 10), "a");
            Assert.That(decided.Arrivals.Count, Is.EqualTo(1));
            Assert.That(decided.MilestoneQueue, Has.Member(PenguinCatalog.FirstRecord));
        }

        [Test]
        public void SpeciesCatalogHas19DistinctSpecies()
        {
            var all = new HashSet<string>(PenguinCatalog.MilestoneOrder);
            all.UnionWith(PenguinCatalog.WeeklyOrder);
            Assert.That(all.Count, Is.EqualTo(19));
        }
    }
}
