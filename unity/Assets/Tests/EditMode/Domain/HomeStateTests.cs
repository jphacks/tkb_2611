using System;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.World;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>ホームの演出のための「何が起きたか」の判定。デモで見せる流れ (15c §5.2) をそのままなぞる。</summary>
    public sealed class HomeStateTests
    {
        private ManualClock _clock;
        private GameSession _session;
        private int _receipts;

        [SetUp]
        public void SetUp()
        {
            // 2026-10-27 (火) 07:00 JST に初回起動。
            _clock = new ManualClock(new DateTimeOffset(2026, 10, 27, 7, 0, 0, TimeSpan.FromHours(9)));
            _session = new GameSession(_clock, (_, _) => 0.0);
        }

        private HomeState Now() => HomeState.Of(_session);

        private SpeechEvent Record()
        {
            var before = Now();
            _session.ScanReceipt(new ReceiptRecord("r" + _receipts++, _session.Today));
            return HomeState.EventsBetween(before, Now());
        }

        private SpeechEvent AdvanceDay()
        {
            var before = Now();
            _clock.AdvanceDays(1);
            _session.Advance();
            return HomeState.EventsBetween(before, Now());
        }

        [Test]
        public void FirstLaunchHasNeverRecorded()
        {
            Assert.That(Now().LastRecord, Is.EqualTo(LastRecord.Never));
            Assert.That(Now().Penguins, Is.EqualTo(1));
        }

        [Test]
        public void FirstRecordIsRecordedAndBringsAPenguin()
        {
            var events = Record();

            Assert.That(events.HasFlag(SpeechEvent.Recorded), Is.True);
            Assert.That(events.HasFlag(SpeechEvent.NewPenguin), Is.True);
            Assert.That(Now().LastRecord, Is.EqualTo(LastRecord.Today));
        }

        [Test]
        public void SecondRecordOfTheDayIsNotAnEvent()
        {
            Record();
            Assert.That(Record(), Is.EqualTo(SpeechEvent.None));
        }

        [Test]
        public void SkippedDayUsesAShieldAndLastRecordAges()
        {
            Record();
            AdvanceDay();
            Assert.That(Now().LastRecord, Is.EqualTo(LastRecord.Yesterday));

            var events = AdvanceDay();
            Assert.That(events.HasFlag(SpeechEvent.ShieldUsed), Is.True);
            Assert.That(Now().LastRecord, Is.EqualTo(LastRecord.TwoOrMoreDaysAgo));
        }

        [Test]
        public void DemoFastForwardDropsATierAndARecordBringsItBack()
        {
            // 盾 2 枚を使い切ったあと、さらに溶けて段が T3 → T2 に落ちる。
            for (var i = 0; i < 6; i++)
            {
                AdvanceDay();
            }

            Assert.That(Now().Tier, Is.EqualTo(2));
            Assert.That(Now().Ice, Is.LessThan(100.0));

            // 空白のあとの記録は +2 だけ (氷のおかえりはやめた。ごほうびは留守の間に島が作ったお魚)。
            var events = Record();
            Assert.That(events.HasFlag(SpeechEvent.Recorded), Is.True);
            Assert.That(Now().Ice, Is.EqualTo(94.0));

            // 記録を続けると 100 に届き、段が T3 に戻る。
            for (var i = 0; i < 3; i++)
            {
                AdvanceDay();
                events = Record();
            }

            Assert.That(events.HasFlag(SpeechEvent.TierUp), Is.True);
            Assert.That(Now().Tier, Is.EqualTo(3));
        }
    }
}
