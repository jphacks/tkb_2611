using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>
    /// game_sim.py の 8 人 × 8 週を C# で再生し、毎日の状態・来訪・段の変化・おかえりが Python と一致することを確かめる
    /// (double は 1e-9、整数は完全一致)。期待値は tools/gen_game_golden.py が生成した GameSimGolden.cs。
    /// </summary>
    public sealed class GameSimParityTests
    {
        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (var p in GameSimGolden.Personas)
            {
                yield return new TestCaseData(p, false).SetName("Parity_Default_" + p);
                yield return new TestCaseData(p, true).SetName("Parity_Decided_" + p);
            }
        }

        private static GameSession Replay(string persona, GameParameters parameters, List<DaySnapshot> snaps)
        {
            var clock = new ManualClock(GameSimGolden.Start);
            var first = AppCalendar.AppDay(GameSimGolden.Start);
            var last = first + 55;
            var session = new GameSession(clock, (w, rs) => rs.Count > 0 ? GameSimGolden.BonusStandIn : 0.0, parameters,
                (s, d) =>
                {
                    if (d >= first && d <= last)
                    {
                        snaps.Add(s.Snapshot(d));
                    }
                });
            foreach (var e in GameSimGolden.Events(persona))
            {
                clock.Set(e.At);
                switch (e.Op)
                {
                    case "receipt":
                        session.ScanReceipt(new ReceiptRecord(e.Id, e.PrintDay, e.Verifies));
                        break;
                    case "select":
                        session.SelectMission(e.Id, e.Verification);
                        break;
                    default:
                        session.CheckIn(e.Id, e.Answer, e.NoShopping);
                        break;
                }
            }

            clock.Set(GameSimGolden.End);
            session.Advance();
            return session;
        }

        [TestCaseSource(nameof(Cases))]
        public void MatchesPython(string persona, bool decided)
        {
            var snaps = new List<DaySnapshot>();
            var session = Replay(persona, decided ? GameParameters.Decided : GameParameters.Default, snaps);
            var expected = decided ? GameSimGolden.ExpectedDecided(persona) : GameSimGolden.ExpectedDefault(persona);
            Assert.That(snaps.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                var a = snaps[i];
                var x = expected[i];
                var at = persona + " day " + (i + 1);
                Assert.That(a.Day, Is.EqualTo(x.Day), at);
                Assert.That(a.Ice, Is.EqualTo(x.Ice).Within(1e-9), at + " ice");
                Assert.That(a.ShardsExact, Is.EqualTo(x.ShardsExact).Within(1e-9), at + " shards");
                Assert.That(a.Shards, Is.EqualTo(x.Shards), at);
                Assert.That(a.Shields, Is.EqualTo(x.Shields), at);
                Assert.That(a.Tier, Is.EqualTo(x.Tier), at);
                Assert.That(a.TotalRecordDays, Is.EqualTo(x.TotalRecordDays), at);
                Assert.That(a.Penguins, Is.EqualTo(x.Penguins), at);
                Assert.That(a.Gold, Is.EqualTo(x.Gold), at);
                Assert.That(a.WhiteCounted, Is.EqualTo(x.WhiteCounted), at);
                Assert.That(a.StampPoints, Is.EqualTo(x.StampPoints), at);
                Assert.That(a.ShieldsUsed, Is.EqualTo(x.ShieldsUsed), at);
                Assert.That(a.Recorded, Is.EqualTo(x.Recorded), at);
            }

            var arrivals = decided ? GameSimGolden.ArrivalsDecided(persona) : GameSimGolden.ArrivalsDefault(persona);
            Assert.That(session.Arrivals.Count, Is.EqualTo(arrivals.Length));
            for (var i = 0; i < arrivals.Length; i++)
            {
                Assert.That(session.Arrivals[i].Species, Is.EqualTo(arrivals[i].Species));
                Assert.That(session.Arrivals[i].Day, Is.EqualTo(arrivals[i].Day));
                Assert.That(session.Arrivals[i].Reason, Is.EqualTo(arrivals[i].Reason));
            }

            var tiers = decided ? GameSimGolden.TiersDecided(persona) : GameSimGolden.TiersDefault(persona);
            Assert.That(session.TierLog.Count, Is.EqualTo(tiers.Length));
            for (var i = 0; i < tiers.Length; i++)
            {
                Assert.That(session.TierLog[i].Day, Is.EqualTo(tiers[i].Day));
                Assert.That(session.TierLog[i].From, Is.EqualTo(tiers[i].From));
                Assert.That(session.TierLog[i].To, Is.EqualTo(tiers[i].To));
            }

            var comebacks = decided ? GameSimGolden.ComebacksDecided(persona) : GameSimGolden.ComebacksDefault(persona);
            Assert.That(session.ComebackLog, Is.EqualTo(comebacks));
        }

        [Test]
        public void CheaterNeverExceedsDailyPersona()
        {
            var daily = new List<DaySnapshot>();
            var cheater = new List<DaySnapshot>();
            Replay("daily", GameParameters.Default, daily);
            Replay("cheater", GameParameters.Default, cheater);
            for (var i = 0; i < daily.Count; i++)
            {
                Assert.That(cheater[i].Ice, Is.LessThanOrEqualTo(daily[i].Ice + 1e-9));
                Assert.That(cheater[i].Shards, Is.LessThanOrEqualTo(daily[i].Shards));
                Assert.That(cheater[i].Penguins, Is.LessThanOrEqualTo(daily[i].Penguins));
                Assert.That(cheater[i].StampPoints, Is.LessThanOrEqualTo(daily[i].StampPoints));
            }
        }
    }
}
