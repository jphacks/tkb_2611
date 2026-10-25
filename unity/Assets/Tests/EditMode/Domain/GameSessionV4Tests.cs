using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    public sealed class GameSessionV4Tests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(9)); // 火
        private static readonly Func<int, IReadOnlyList<ReceiptRecord>, double> Bonus0 = (w, rs) => 0.0;

        private static DateTimeOffset T(int y, int mo, int d, int h, int mi = 0)
        {
            return new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(9));
        }

        private static bool Scan(GameSession s, ManualClock c, DateTimeOffset at, string id, int printDay = 0)
        {
            c.Set(at);
            return s.ScanReceipt(new ReceiptRecord(id, printDay));
        }

        private static bool HonestCheckIn(GameSession s, ManualClock c, DateTimeOffset at, string id)
        {
            c.Set(at);
            s.SelectMission(id, MissionVerification.Self);
            return s.CheckIn(id, CheckInAnswer.Done);
        }

        // ----- 時計 -----

        [Test]
        public void ClockRollbackClampsToLastSeenDay()
        {
            var c = new ManualClock(T(2026, 10, 27, 9));
            var s = new GameSession(c, Bonus0);
            c.Set(T(2026, 10, 29, 12));
            var d29 = s.Advance().Value;
            Assert.That(Scan(s, c, T(2026, 10, 28, 12), "a"), Is.True);
            Assert.That(s.ClockStatus, Is.EqualTo(ClockStatus.Behind));
            Assert.That(s.ClockRollbacks, Is.EqualTo(1));
            Assert.That(s.IsRecorded(d29), Is.True);
            Assert.That(s.IsRecorded(d29 - 1), Is.False);
            Assert.That(s.Ice, Is.EqualTo(102.0));
            Assert.That(Scan(s, c, T(2026, 10, 27, 12), "b"), Is.True);
            Assert.That(s.Ice, Is.EqualTo(102.0));
            c.Set(T(2026, 10, 30, 12));
            s.Advance();
            Assert.That(s.ClockStatus, Is.EqualTo(ClockStatus.Ok));
        }

        [Test]
        public void A_ForwardJumpAndBackDoesNotLockOut()
        {
            var c = new ManualClock(Start) { IsTrusted = false };
            var s = new GameSession(c, Bonus0, Hold14);
            Scan(s, c, Start, "r0");
            c.Set(Start.AddDays(30));
            Assert.That(s.Advance(), Is.Null);
            Assert.That(s.ClockStatus, Is.EqualTo(ClockStatus.AheadPending));
            Assert.That(s.Ice, Is.EqualTo(102.0));
            var rejected = 0;
            for (var d = 1; d <= 31; d++)
            {
                if (!Scan(s, c, Start.AddDays(d), "a" + d))
                {
                    rejected++;
                }
            }

            Assert.That(rejected, Is.EqualTo(0));
            Assert.That(s.TotalRecordDays, Is.EqualTo(32));
        }

        [Test]
        public void A_V3LockoutIsReproduced()
        {
            var c = new ManualClock(Start);
            var s = new GameSession(c, Bonus0, GameParameters.V3);
            Scan(s, c, Start, "r0");
            c.Set(Start.AddDays(30));
            s.Advance();
            var rejected = 0;
            for (var d = 1; d <= 31; d++)
            {
                if (!Scan(s, c, Start.AddDays(d), "a" + d))
                {
                    rejected++;
                }
            }

            Assert.That(rejected, Is.EqualTo(29));
        }

        [Test]
        public void C_JumpTo2099IsHeldThenReleased()
        {
            var c = new ManualClock(Start) { IsTrusted = false };
            var s = new GameSession(c, Bonus0, Hold14);
            c.Set(new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.FromHours(9)));
            Assert.That(s.Advance(), Is.Null);
            Assert.That(s.PendingAheadDay, Is.Not.Null);
            Assert.That(s.Ice, Is.EqualTo(100.0));
            Assert.That(Scan(s, c, Start.AddDays(1), "x"), Is.True);
            Assert.That(s.ClockStatus, Is.EqualTo(ClockStatus.Ok));
        }

        [Test]
        public void JumpWithinLimitIsProcessed()
        {
            var c = new ManualClock(Start) { IsTrusted = false };
            var s = new GameSession(c, Bonus0, Hold14);
            c.Set(Start.AddDays(14));
            Assert.That(s.Advance(), Is.Not.Null);
            c.Set(Start.AddDays(29));
            Assert.That(s.Advance(), Is.Null);
        }

        [Test]
        public void ConfirmClockSettlesARealLongAbsence()
        {
            var c = new ManualClock(Start) { IsTrusted = false };
            var s = new GameSession(c, Bonus0, Hold14);
            Assert.That(Scan(s, c, Start.AddDays(30), "a"), Is.False);
            Assert.That(s.ConfirmClock(), Is.Not.Null);
            Assert.That(s.Ice, Is.LessThan(100.0));
            var before = s.Ice;
            Assert.That(Scan(s, c, Start.AddDays(30), "a"), Is.True);
            Assert.That(s.Ice, Is.EqualTo(before + 2));   // 氷のおかえりは既定で 0 (留守のお魚に一本化)
        }

        [Test]
        public void B_BankingDaysAheadGivesNoLastingGain()
        {
            var cc = new ManualClock(Start) { IsTrusted = false };
            var cheat = new GameSession(cc, Bonus0, Hold14);
            for (var d = 0; d < 7; d++)
            {
                HonestCheckIn(cheat, cc, Start.AddDays(d), "M" + d);
            }

            Assert.That(cheat.Ice, Is.EqualTo(114.0));
            var hc = new ManualClock(Start);
            var honest = new GameSession(hc, Bonus0, Hold14);
            for (var d = 0; d < 14; d++)
            {
                HonestCheckIn(cheat, cc, Start.AddDays(d).AddHours(1), "N" + d);
                HonestCheckIn(honest, hc, Start.AddDays(d).AddHours(1), "N" + d);
            }

            cc.Set(Start.AddDays(14));
            hc.Set(Start.AddDays(14));
            cheat.Advance();
            honest.Advance();
            Assert.That(cheat.Ice, Is.EqualTo(honest.Ice));
            Assert.That(cheat.TotalRecordDays, Is.EqualTo(honest.TotalRecordDays));
        }

        // ----- おかえり -----

        [Test]
        public void ShieldOnlyGapGivesNoComeback()
        {
            foreach (var (p, expectComeback) in new[] { (GameParameters.V3, true), (GameParameters.Default, false) })
            {
                var c = new ManualClock(T(2026, 10, 27, 9));
                var s = new GameSession(c, Bonus0, p);
                for (var i = 0; i < 4; i++)
                {
                    HonestCheckIn(s, c, T(2026, 10, 27, 20).AddDays(i), "E" + i);
                }

                HonestCheckIn(s, c, T(2026, 11, 3, 20), "E9");
                Assert.That(s.ComebackLog.Count, Is.EqualTo(expectComeback ? 1 : 0));
                if (expectComeback)
                {
                    Assert.That(s.ComebackLog[0].Value, Is.EqualTo(0));
                }

                Assert.That(s.Ice, Is.EqualTo(110.0));
            }
        }

        [Test]
        public void OneMeltedDayGivesPlusOne()
        {
            var c = new ManualClock(T(2026, 10, 27, 9));
            var s = new GameSession(c, Bonus0, new GameParameters { ComebackRatio = 0.5 });
            c.Set(T(2026, 10, 29, 12));
            s.Advance();
            HonestCheckIn(s, c, T(2026, 10, 29, 20), "a");
            c.Set(T(2026, 10, 31, 12));
            s.Advance();
            Assert.That(s.GapMelted, Is.EqualTo(2.0));
            HonestCheckIn(s, c, T(2026, 10, 31, 20), "b");
            Assert.That(s.ComebackLog[s.ComebackLog.Count - 1].Value, Is.EqualTo(1));
            Assert.That(s.Ice, Is.EqualTo(103.0));
        }

        // ----- サーバー時計と組み合わせたとき (17a の ConfirmClock の呼び方。保留は MaxCatchUpDays = 14 のとき) -----

        private static readonly DateTimeOffset GameStart = new DateTimeOffset(2026, 3, 2, 3, 0, 0, TimeSpan.Zero); // 月曜 12:00 JST

        private static GameParameters Hold14 => new GameParameters { MaxCatchUpDays = 14 };

        [Test]
        public void LongOfflineJumpIsPendingUntilServerConfirmsThenSettles()
        {
            var device = new ManualClock(GameStart) { IsTrusted = false };
            var mono = new ManualMonotonicClock();
            var clock = new ServerTrustedClock(device, mono);
            var session = new GameSession(clock, Bonus0, Hold14);
            var firstDay = session.Today;

            // 20 日放置 (本当に 20 日たった) して、オフラインで起動
            device.AdvanceDays(20);
            Assert.That(session.Advance(), Is.Null);
            Assert.That(session.ClockStatus, Is.EqualTo(ClockStatus.AheadPending));

            // 通信できた: サーバーも 20 日後と言う → Advance で確定 (ConfirmClock は要らない)
            var sent = mono.Seconds;
            mono.Advance(0.3);
            clock.ObserveResponse(HttpDate.Format(device.UtcNow), null, sent, mono.Seconds);
            Assert.That(session.Advance(), Is.EqualTo(firstDay + 20));
            Assert.That(session.ClockStatus, Is.EqualTo(ClockStatus.Ok));
            Assert.That(session.LastSettledDay, Is.EqualTo(firstDay + 19));
        }

        [Test]
        public void DeviceClockPushedAheadIsUndoneByServerTime()
        {
            var device = new ManualClock(GameStart) { IsTrusted = false };
            var mono = new ManualMonotonicClock();
            var clock = new ServerTrustedClock(device, mono);
            var session = new GameSession(clock, Bonus0, Hold14);
            var firstDay = session.Today;
            var realNow = device.UtcNow;

            // 端末の時計だけを 30 日進めた
            device.AdvanceDays(30);
            Assert.That(session.Advance(), Is.Null);
            Assert.That(session.ClockStatus, Is.EqualTo(ClockStatus.AheadPending));

            // サーバーは今日だと言う → 保留が解けて、日は進まない (氷は溶けない)
            clock.ObserveResponse(HttpDate.Format(realNow), null, mono.Seconds, mono.Seconds + 0.1);
            mono.Advance(0.1);
            Assert.That(session.Advance(), Is.EqualTo(firstDay));
            Assert.That(session.ClockStatus, Is.EqualTo(ClockStatus.Ok));
            Assert.That(session.PendingAheadDay, Is.Null);
            Assert.That(session.LastSettledDay, Is.LessThan(firstDay));
        }

        [Test]
        public void OfflineUserCanStillConfirmWithDeviceClock()
        {
            var device = new ManualClock(GameStart) { IsTrusted = false };
            var clock = new ServerTrustedClock(device, new ManualMonotonicClock());
            var session = new GameSession(clock, Bonus0, Hold14);
            var firstDay = session.Today;
            device.AdvanceDays(20);
            Assert.That(session.Advance(), Is.Null);

            // 利用者が「端末の時刻で進める」を選んだ → ConfirmClock
            Assert.That(session.ConfirmClock(), Is.EqualTo(firstDay + 20));
            Assert.That(session.ClockStatus, Is.EqualTo(ClockStatus.Ok));
        }
    }
}
