using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Reality;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>
    /// iOS 26 の 設定 > スクリーンタイム > (端末名) の「日」の表示を OCR した行の解析 (project-penguin から写した)。
    /// 行は実機のスクショ (リポジトリに入れない) の並びを手で書き写したもので、端末名とアプリ名は架空のものにしてある。
    /// </summary>
    public sealed class ScreenTimeOcrParserTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 24, 22, 16, 0, TimeSpan.FromHours(9));
        private static readonly int Today = JstCalendar.DayNumber(Now);

        private static List<OcrLine> Ios26DayScreen(string date = "今日、10月24日", string total = "4時間2分",
            string updated = "前回のアップデート：今日 22:15")
        {
            // y は画面の上からの割合 (縦 2556 の実機のスクショでの位置)。
            var lines = new List<OcrLine>
            {
                new OcrLine("22:16", 0.13, 0.025, 0.11, 0.02),
                new OcrLine("テストの iPhone15", 0.32, 0.085, 0.36, 0.02),
                new OcrLine("週", 0.27, 0.16, 0.03, 0.015),
                new OcrLine("日", 0.69, 0.16, 0.03, 0.015),
                new OcrLine("スクリーンタイム", 0.08, 0.225, 0.29, 0.02),
                new OcrLine("14 時間", 0.82, 0.355, 0.10, 0.015),
                new OcrLine("平均", 0.82, 0.41, 0.06, 0.015),
                new OcrLine("0", 0.82, 0.46, 0.02, 0.015),
                new OcrLine("日 月 火 水 木 金 土", 0.08, 0.48, 0.66, 0.015),
                new OcrLine("60分", 0.82, 0.52, 0.07, 0.015),
                new OcrLine("ソーシャル", 0.08, 0.675, 0.15, 0.02),
                new OcrLine("55分", 0.08, 0.70, 0.08, 0.02),
                new OcrLine("よく使われたもの", 0.08, 0.785, 0.28, 0.02),
                new OcrLine("アプリA", 0.19, 0.825, 0.05, 0.02),
                new OcrLine("26分", 0.71, 0.845, 0.06, 0.015),
            };
            if (date != null)
            {
                lines.Add(new OcrLine(date, 0.08, 0.278, 0.27, 0.02));
            }

            if (total != null)
            {
                lines.Add(new OcrLine(total, 0.08, 0.31, 0.36, 0.035));
            }

            if (updated != null)
            {
                lines.Add(new OcrLine(updated, 0.08, 0.745, 0.42, 0.015));
            }

            return lines;
        }

        [Test]
        public void ReadsDateAndTotalFromIos26DayScreen()
        {
            var result = ScreenTimeOcrParser.Parse(Ios26DayScreen(), Now);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.IsToday, Is.True);
            Assert.That(result.Reading.Day, Is.EqualTo(Today));
            Assert.That(result.Reading.TotalMinutes, Is.EqualTo(4 * 60 + 2));
            Assert.That(result.Reading.Source, Is.EqualTo(ScreenTimeSourceKind.Ocr));
            Assert.That(result.Reading.CapturedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 24, 22, 15, 0, TimeSpan.FromHours(9))));
        }

        [Test]
        public void PastDayIsNotToday()
        {
            var result = ScreenTimeOcrParser.Parse(Ios26DayScreen(date: "金曜日、10月23日", total: "3時間10分"), Now);
            Assert.That(result.IsToday, Is.False);
            Assert.That(result.Reading.Day, Is.EqualTo(Today - 1));
            Assert.That(result.Reading.TotalMinutes, Is.EqualTo(190));
        }

        [Test]
        public void LinesMayArriveInAnyOrder()
        {
            var lines = Ios26DayScreen();
            lines.Reverse();
            Assert.That(ScreenTimeOcrParser.Parse(lines, Now).Reading.TotalMinutes, Is.EqualTo(242));
        }

        [Test]
        public void ChartAxisIsNotMistakenForTheTotal()
        {
            // 合計が読めず、その下のグラフの目盛り「14 時間」だけが残っても、目盛りを合計にしない。
            var result = ScreenTimeOcrParser.Parse(Ios26DayScreen(total: null), Now);
            Assert.That(result.Failure, Is.EqualTo(ScreenTimeParseFailure.TotalMissing));
        }

        [Test]
        public void SpacesInsideTheTotalAreIgnored()
        {
            Assert.That(ScreenTimeOcrParser.Parse(Ios26DayScreen(total: "4 時間 2 分"), Now).Reading.TotalMinutes, Is.EqualTo(242));
        }

        [Test]
        public void ReadsEnglishScreen()
        {
            var lines = new List<OcrLine>
            {
                new OcrLine("Screen Time", 0.08, 0.225, 0.29, 0.02),
                new OcrLine("Yesterday, October 23", 0.08, 0.278, 0.3, 0.02),
                new OcrLine("3h 10m", 0.08, 0.31, 0.3, 0.035),
            };
            var result = ScreenTimeOcrParser.Parse(lines, Now);
            Assert.That(result.Reading.Day, Is.EqualTo(Today - 1));
            Assert.That(result.Reading.TotalMinutes, Is.EqualTo(190));
        }

        [Test]
        public void RejectsTheWeekView()
        {
            var lines = new List<OcrLine>
            {
                new OcrLine("スクリーンタイム", 0.34, 0.09, 0.32, 0.02),
                new OcrLine("1日の平均", 0.08, 0.22, 0.16, 0.02),
                new OcrLine("6時間20分", 0.08, 0.26, 0.40, 0.04),
                new OcrLine("①先週との差は7%", 0.64, 0.27, 0.28, 0.02),
            };
            Assert.That(ScreenTimeOcrParser.Parse(lines, Now).Failure, Is.EqualTo(ScreenTimeParseFailure.WeekView));
        }

        [Test]
        public void RejectsOtherScreens()
        {
            var lines = new List<OcrLine> { new OcrLine("写真", 0.4, 0.1, 0.2, 0.02), new OcrLine("6時間20分", 0.1, 0.3, 0.3, 0.04) };
            Assert.That(ScreenTimeOcrParser.Parse(lines, Now).Failure, Is.EqualTo(ScreenTimeParseFailure.NotScreenTime));
        }

        [Test]
        public void MissingDateFails()
        {
            Assert.That(ScreenTimeOcrParser.Parse(Ios26DayScreen(date: null), Now).Failure, Is.EqualTo(ScreenTimeParseFailure.DateMissing));
        }

        [Test]
        public void TooOldDayFails()
        {
            Assert.That(ScreenTimeOcrParser.Parse(Ios26DayScreen(date: "10月10日"), Now).Failure, Is.EqualTo(ScreenTimeParseFailure.TooOld));
        }

        [Test]
        public void DecemberInJanuaryIsLastYear()
        {
            var newYear = new DateTimeOffset(2027, 1, 2, 9, 0, 0, TimeSpan.FromHours(9));
            var result = ScreenTimeOcrParser.Parse(Ios26DayScreen(date: "木曜日、12月31日"), newYear);
            Assert.That(result.Reading.Day, Is.EqualTo(JstCalendar.DayNumber(new DateTime(2026, 12, 31))));
        }

        [Test]
        public void MissingUpdateUsesImportTime()
        {
            Assert.That(ScreenTimeOcrParser.Parse(Ios26DayScreen(updated: null), Now).Reading.CapturedAt, Is.EqualTo(Now));
        }

        [Test]
        public void FutureUpdateTimeIsClampedToNow()
        {
            // 端末の時計がずれていて「今日 23:00」が取り込みより後になっても、未来の時刻にはしない。
            var result = ScreenTimeOcrParser.Parse(Ios26DayScreen(updated: "前回のアップデート: 今日 23:00"), Now);
            Assert.That(result.Reading.CapturedAt, Is.EqualTo(Now));
        }

        [TestCase("6時間20分", 380)]
        [TestCase("６時間２０分", 380)]
        [TestCase("2時間", 120)]
        [TestCase("45分", 45)]
        [TestCase("6h 20m", 380)]
        [TestCase("45min", 45)]
        [TestCase("1h", 60)]
        public void ParsesDurations(string text, int minutes)
        {
            Assert.That(ScreenTimeOcrParser.ParseDuration(text), Is.EqualTo(minutes));
        }

        [TestCase("")]
        [TestCase("時間")]
        [TestCase("6時間90分")]
        [TestCase("今日、10月24日")]
        [TestCase("テストの iPhone15")]
        public void RejectsNonDurations(string text)
        {
            Assert.That(ScreenTimeOcrParser.ParseDuration(text), Is.Null);
        }

        [Test]
        public void RejectsOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ScreenTimeReading.Create(Today, ScreenTimeReading.MaxMinutes + 1, Now, ScreenTimeSourceKind.Manual));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScreenTimeReading.Create(Today, -1, Now, ScreenTimeSourceKind.Manual));
        }
    }
}
