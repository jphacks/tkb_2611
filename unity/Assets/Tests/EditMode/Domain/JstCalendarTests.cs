using System;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    public sealed class JstCalendarTests
    {
        [Test]
        public void EpochIsJanuaryFirstJst()
        {
            Assert.That(JstCalendar.DayNumber(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(9))), Is.EqualTo(0));
            Assert.That(JstCalendar.DayNumber(new DateTimeOffset(2025, 12, 31, 15, 0, 0, TimeSpan.Zero)), Is.EqualTo(0));
            Assert.That(JstCalendar.DayNumber(new DateTimeOffset(2025, 12, 31, 14, 59, 59, TimeSpan.Zero)), Is.EqualTo(-1));
        }

        [Test]
        public void DayBoundaryFollowsJstNotDeviceZone()
        {
            // 同じ瞬間を別のオフセットで表しても同じ日番号
            var utc = new DateTimeOffset(2026, 10, 14, 16, 0, 0, TimeSpan.Zero); // 10/15 01:00 JST
            var pacific = utc.ToOffset(TimeSpan.FromHours(-7));
            Assert.That(JstCalendar.DayNumber(pacific), Is.EqualTo(JstCalendar.DayNumber(utc)));
            Assert.That(JstCalendar.DateOfDay(JstCalendar.DayNumber(utc)), Is.EqualTo(new DateTime(2026, 10, 15)));
        }

        [TestCase(-4, -1)] // 2025-12-28 (日)
        [TestCase(-3, 0)]  // 2025-12-29 (月)
        [TestCase(3, 0)]   // 2026-01-04 (日)
        [TestCase(4, 1)]   // 2026-01-05 (月)
        public void WeeksStartOnMonday(int day, int week)
        {
            Assert.That(JstCalendar.WeekOfDay(day), Is.EqualTo(week));
        }

        [Test]
        public void FirstDayOfEveryWeekIsMonday()
        {
            for (var w = -3; w < 60; w++)
            {
                var monday = JstCalendar.DateOfDay(JstCalendar.FirstDayOfWeek(w));
                Assert.That(monday.DayOfWeek, Is.EqualTo(DayOfWeek.Monday), w.ToString());
                Assert.That(JstCalendar.WeekOfDay(JstCalendar.FirstDayOfWeek(w)), Is.EqualTo(w));
                Assert.That(JstCalendar.WeekOfDay(JstCalendar.FirstDayOfWeek(w) + 6), Is.EqualTo(w));
            }
        }

        [Test]
        public void ParsesPurchaseDate()
        {
            Assert.That(JstCalendar.ParsePurchaseDate("2026-01-01"), Is.EqualTo(0));
            Assert.That(JstCalendar.ParsePurchaseDate("2026-10-19"), Is.EqualTo(JstCalendar.FirstDayOfWeek(JstCalendar.WeekOfDay(291))));
            Assert.That(JstCalendar.ParsePurchaseDate(null), Is.Null);
            Assert.That(JstCalendar.ParsePurchaseDate("R8/10/19"), Is.Null);
        }
    }
}
