using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProjectPenguin.Domain.World;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    public sealed class PenguinSpeechTests
    {
        private static readonly Func<int, int> First = _ => 0;

        private static SpeechContext Context(int tier = 3, DayPart part = DayPart.Daytime, SpeechEvent events = SpeechEvent.None,
            bool comeback = false, bool firstOpen = false, LastRecord last = LastRecord.Today, bool iceLowShown = false)
        {
            return new SpeechContext(tier, part, events, comeback, firstOpen, last, iceLowShown);
        }

        [TestCase(4, DayPart.Night)]
        [TestCase(5, DayPart.Morning)]
        [TestCase(9, DayPart.Morning)]
        [TestCase(10, DayPart.Daytime)]
        [TestCase(17, DayPart.Evening)]
        [TestCase(21, DayPart.Night)]
        public void DayPartUsesJst(int jstHour, DayPart expected)
        {
            var instant = new DateTimeOffset(2026, 10, 15, jstHour, 30, 0, TimeSpan.FromHours(9));
            // 端末のタイムゾーンが違っても JST で判定する。
            Assert.That(PenguinSpeech.DayPartOf(instant.ToOffset(TimeSpan.FromHours(-7))), Is.EqualTo(expected));
        }

        [Test]
        public void EventsComeBeforeEverythingElse()
        {
            var line = PenguinSpeech.Pick(
                Context(tier: 0, events: SpeechEvent.NewPenguin, comeback: true, firstOpen: true), null, First);
            Assert.That(line.Category, Is.EqualTo(SpeechCategory.Event));
            Assert.That(line.Id, Does.StartWith("E0"));
        }

        [Test]
        public void RecordingAtLowTierSaysTheIceIsComingBack()
        {
            Assert.That(PenguinSpeech.Pick(Context(tier: 1, events: SpeechEvent.Recorded), null, First).Id, Is.EqualTo("E07"));
            Assert.That(PenguinSpeech.Pick(Context(tier: 3, events: SpeechEvent.Recorded), null, First).Id, Is.EqualTo("E06"));
        }

        [Test]
        public void ComebackComesBeforeGreeting()
        {
            var line = PenguinSpeech.Pick(Context(comeback: true, firstOpen: true, part: DayPart.Morning), null, First);
            Assert.That(line.Category, Is.EqualTo(SpeechCategory.Comeback));
        }

        [Test]
        public void ComebackAtNightDoesNotAskForARecord()
        {
            for (var i = 0; i < 4; i++)
            {
                var index = i;
                var line = PenguinSpeech.Pick(Context(comeback: true, part: DayPart.Night), null, n => index % n);
                Assert.That(line.Id, Is.EqualTo("R01"));
            }
        }

        [TestCase(DayPart.Morning, LastRecord.Yesterday, "G01")]
        [TestCase(DayPart.Morning, LastRecord.Never, "G03")]
        [TestCase(DayPart.Daytime, LastRecord.Today, "G04")]
        [TestCase(DayPart.Evening, LastRecord.TwoOrMoreDaysAgo, "G07")]
        [TestCase(DayPart.Night, LastRecord.Never, "G08")]
        public void FirstOpenOfTheDayGreets(DayPart part, LastRecord last, string id)
        {
            Assert.That(PenguinSpeech.Pick(Context(part: part, firstOpen: true, last: last), null, First).Id, Is.EqualTo(id));
        }

        [Test]
        public void LowIceLineIsShownOncePerDay()
        {
            Assert.That(PenguinSpeech.Pick(Context(tier: 0), null, First).Category, Is.EqualTo(SpeechCategory.IceLow));
            Assert.That(PenguinSpeech.Pick(Context(tier: 0, iceLowShown: true), null, First).Category, Is.Not.EqualTo(SpeechCategory.IceLow));
        }

        [Test]
        public void RecentLinesAreSkippedWhileOthersRemain()
        {
            var line = PenguinSpeech.Pick(Context(events: SpeechEvent.NewPenguin), new[] { "E01" }, First);
            Assert.That(line.Id, Is.EqualTo("E02"));

            // 当てはまる行がすべて直近なら、繰り返しを許す。
            line = PenguinSpeech.Pick(Context(events: SpeechEvent.NewPenguin), new[] { "E01", "E02" }, First);
            Assert.That(line.Category, Is.EqualTo(SpeechCategory.Event));
        }

        [TestCase(3, DayPart.Daytime)]
        [TestCase(0, DayPart.Morning)]
        public void SomethingIsAlwaysSaid(int tier, DayPart part)
        {
            var line = PenguinSpeech.Pick(Context(tier: tier, part: part, iceLowShown: true), null, First);
            Assert.That(line.Text, Is.Not.Empty);
        }

        [Test]
        public void LinesFollowTheWritingRules()
        {
            // 15c §2.1: 数字の量 (kg・点) を言わない、20 字前後に収める、責める言葉を使わない。
            foreach (var line in PenguinSpeech.All)
            {
                Assert.That(Regex.IsMatch(line.Text, "kg|点|％|%"), Is.False, line.Id);
                Assert.That(line.Text.Replace(" ", string.Empty).Length, Is.LessThanOrEqualTo(24), line.Id);
                Assert.That(line.Text, Does.Not.Contain("してない").And.Not.Contain("せいで").And.Not.Contain("サボ"), line.Id);
            }

            Assert.That(PenguinSpeech.All.Select(l => l.Id).Distinct().Count(), Is.EqualTo(PenguinSpeech.All.Count));
        }
    }
}
