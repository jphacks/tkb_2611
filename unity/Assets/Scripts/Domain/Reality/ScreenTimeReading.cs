using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Domain.Reality
{
    /// <summary>値をどこから得たか。ゲームロジックが量を変えられるように記録に残す。</summary>
    public enum ScreenTimeSourceKind
    {
        Ocr,
        Manual,
    }

    /// <summary>
    /// スクリーンタイムの 1 日分の合計 (project-penguin の docs/reality-inputs.md の「2」から写した。村では VillageGame.AcceptScreen が電気にする)。
    /// iOS 26 の 設定 > スクリーンタイム > (端末名) の「日」の表示から読む。端末名やアプリ名は持たない。
    /// </summary>
    public sealed class ScreenTimeReading
    {
        /// <summary>1 日の合計として受け付ける上限 (24 時間)。</summary>
        public const int MaxMinutes = 24 * 60;

        private ScreenTimeReading(int day, int totalMinutes, DateTimeOffset capturedAt, ScreenTimeSourceKind source)
        {
            Day = day;
            TotalMinutes = totalMinutes;
            CapturedAt = capturedAt;
            Source = source;
        }

        /// <summary>どの日の合計か (<see cref="JstCalendar.DayNumber(DateTimeOffset)"/> の日番号。スクリーンタイムは 0:00 区切り)。</summary>
        public int Day { get; }

        /// <summary>その日の合計 (分)。今日の分なら途中の値。</summary>
        public int TotalMinutes { get; }

        /// <summary>画面の「前回のアップデート」の時刻。読めなければ取り込んだ時刻。</summary>
        public DateTimeOffset CapturedAt { get; }

        public ScreenTimeSourceKind Source { get; }

        /// <summary>値を確かめて作る。範囲の外なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static ScreenTimeReading Create(int day, int totalMinutes, DateTimeOffset capturedAt, ScreenTimeSourceKind source)
        {
            if (totalMinutes < 0 || totalMinutes > MaxMinutes)
            {
                throw new ArgumentOutOfRangeException(nameof(totalMinutes));
            }

            return new ScreenTimeReading(day, totalMinutes, capturedAt, source);
        }
    }

    /// <summary>OCR の 1 行。座標は画像に対する割合 (0〜1) で、左上が原点 (y は下に向かって増える)。</summary>
    public readonly struct OcrLine
    {
        public OcrLine(string text, double x, double y, double width, double height)
        {
            Text = text ?? "";
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string Text { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>スクショを読めなかった理由。画面はそれぞれに合った案内を出す。</summary>
    public enum ScreenTimeParseFailure
    {
        None,

        /// <summary>「スクリーンタイム」の見出しがない (別の画面のスクショ)。</summary>
        NotScreenTime,

        /// <summary>「週」の表示のスクショ (日ごとの合計の数字が無い)。「日」を選んで撮り直してもらう。</summary>
        WeekView,

        /// <summary>「今日、10月24日」のような日付の行が読めない。</summary>
        DateMissing,

        /// <summary>日付の下の合計 (「4時間2分」) が読めない。</summary>
        TotalMissing,

        /// <summary><see cref="ScreenTimeOcrParser.MaxAgeDays"/> 日より前の日。</summary>
        TooOld,
    }

    /// <summary><see cref="ScreenTimeOcrParser.Parse"/> の結果。</summary>
    public sealed class ScreenTimeParseResult
    {
        private ScreenTimeParseResult(ScreenTimeReading reading, ScreenTimeParseFailure failure, bool isToday)
        {
            Reading = reading;
            Failure = failure;
            IsToday = isToday;
        }

        /// <summary>読めたときの値。読めなければ null。</summary>
        public ScreenTimeReading Reading { get; }

        public ScreenTimeParseFailure Failure { get; }

        public bool Succeeded => Reading != null;

        /// <summary>今日の画面 (合計は途中の値。資源にはせず途中経過として見せる)。</summary>
        public bool IsToday { get; }

        internal static ScreenTimeParseResult Ok(ScreenTimeReading reading, bool isToday) =>
            new ScreenTimeParseResult(reading, ScreenTimeParseFailure.None, isToday);

        internal static ScreenTimeParseResult Fail(ScreenTimeParseFailure failure) =>
            new ScreenTimeParseResult(null, failure, false);
    }

    /// <summary>
    /// 設定 > スクリーンタイム > (端末名) の「日」の表示 (iOS 26) を OCR した行から <see cref="ScreenTimeReading"/> を取り出す。
    /// 画面の並びは project-penguin の docs/reality-inputs.md の表。端末名 (利用者の名前が入る) とアプリ名の行は読まない。
    /// </summary>
    public static class ScreenTimeOcrParser
    {
        /// <summary>この日数より前の日のスクショは受け付けない (iOS は 1 週間ほどしか日ごとの表示を残さない)。</summary>
        public const int MaxAgeDays = 7;

        private static readonly string[] HeaderWords = { "スクリーンタイム", "ScreenTime" };

        // 週の表示にだけある見出し。日の表示と見分ける。
        private static readonly string[] WeekWords = { "1日の平均", "DailyAverage" };
        private static readonly string[] UpdateWords = { "前回のアップデート", "Updated" };

        // 「6時間20分」「2時間」「45分」「6h20m」「6h」「45m」「45min」。行全体が時間であるものだけを受け付ける。
        private static readonly Regex DurationJa = new Regex(@"^(?:(\d{1,2})時間)?(?:(\d{1,2})分)?$", RegexOptions.CultureInvariant);
        private static readonly Regex DurationEn = new Regex(@"^(?:(\d{1,2})h)?(?:(\d{1,2})m(?:in)?)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // 「今日、10月24日」「昨日、10月23日」「金曜日、10月23日」。英語は「Today, October 24」など。
        private static readonly Regex DateJa = new Regex(@"(\d{1,2})月(\d{1,2})日", RegexOptions.CultureInvariant);
        private static readonly Regex DateEn = new Regex(
            @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?(\d{1,2})(?!\d)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly string[] MonthsEn = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        // 日付の行から合計の行までの縦の距離の上限 (画像の高さに対する割合)。iOS 26 の実機では 0.04 前後、
        // グラフの目盛り「14時間」は 0.08 離れている。合計が読めなかったときに目盛りを合計と取り違えないための上限。
        private const double MaxTotalGap = 0.06;

        private static readonly Regex UpdateTime = new Regex(@"(今日|昨日|Today|Yesterday)?(?:at)?(\d{1,2}):(\d{2})", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <param name="lines">OCR の行。順番は問わない (座標で並べ直す)。</param>
        /// <param name="now">取り込んだ時刻。日付の年を決めるのと、読めなかったときの <see cref="ScreenTimeReading.CapturedAt"/> に使う。</param>
        public static ScreenTimeParseResult Parse(IReadOnlyList<OcrLine> lines, DateTimeOffset now)
        {
            var sorted = new List<(OcrLine line, string text)>();
            foreach (var line in lines)
            {
                sorted.Add((line, Normalize(line.Text)));
            }

            sorted.Sort((a, b) => a.line.Y.CompareTo(b.line.Y));

            if (FindIndex(sorted, HeaderWords) < 0)
            {
                return ScreenTimeParseResult.Fail(ScreenTimeParseFailure.NotScreenTime);
            }

            if (FindIndex(sorted, WeekWords) >= 0)
            {
                return ScreenTimeParseResult.Fail(ScreenTimeParseFailure.WeekView);
            }

            var today = JstCalendar.DayNumber(now);
            int? day = null;
            var dateIndex = -1;
            for (var i = 0; i < sorted.Count && day == null; i++)
            {
                day = ReadDate(sorted[i].text, now);
                dateIndex = i;
            }

            if (day is not { } d)
            {
                return ScreenTimeParseResult.Fail(ScreenTimeParseFailure.DateMissing);
            }

            if (today - d > MaxAgeDays)
            {
                return ScreenTimeParseResult.Fail(ScreenTimeParseFailure.TooOld);
            }

            if (ReadTotal(sorted, dateIndex) is not { } total || total > ScreenTimeReading.MaxMinutes)
            {
                return ScreenTimeParseResult.Fail(ScreenTimeParseFailure.TotalMissing);
            }

            var reading = ScreenTimeReading.Create(d, total, ReadCapturedAt(sorted, now), ScreenTimeSourceKind.Ocr);
            return ScreenTimeParseResult.Ok(reading, d == today);
        }

        /// <summary>
        /// 日付の行を日番号にする。年は書かれていないので、今日より後になるなら前の年とみなす (1 月に 12 月の画面を読む)。
        /// 日付でない行は null。
        /// </summary>
        internal static int? ReadDate(string normalized, DateTimeOffset now)
        {
            int month, dayOfMonth;
            var ja = DateJa.Match(normalized);
            if (ja.Success)
            {
                month = int.Parse(ja.Groups[1].Value, CultureInfo.InvariantCulture);
                dayOfMonth = int.Parse(ja.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            else
            {
                var en = DateEn.Match(normalized);
                if (!en.Success)
                {
                    return null;
                }

                month = Array.IndexOf(MonthsEn, en.Groups[1].Value.ToLowerInvariant()) + 1;
                dayOfMonth = int.Parse(en.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            var todayJst = JstCalendar.ToJst(now).Date;
            var date = DateOf(todayJst.Year, month, dayOfMonth);
            if (date > todayJst)
            {
                date = DateOf(todayJst.Year - 1, month, dayOfMonth);
            }

            return date == null ? (int?)null : JstCalendar.DayNumber(date.Value);
        }

        private static DateTime? DateOf(int year, int month, int day) =>
            month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateTime(year, month, day) : (DateTime?)null;

        /// <summary>日付の行の残り、なければその下の最初の時間の行。</summary>
        private static int? ReadTotal(List<(OcrLine line, string text)> sorted, int dateIndex)
        {
            var dateText = sorted[dateIndex].text;
            var at = dateText.LastIndexOf('日');
            if (at >= 0 && ParseDuration(dateText.Substring(at + 1)) is { } inline)
            {
                return inline;
            }

            var dateY = sorted[dateIndex].line.Y;
            for (var i = dateIndex + 1; i < sorted.Count && sorted[i].line.Y - dateY <= MaxTotalGap; i++)
            {
                if (ParseDuration(sorted[i].text) is { } value)
                {
                    return value;
                }
            }

            return null;
        }

        private static DateTimeOffset ReadCapturedAt(List<(OcrLine line, string text)> sorted, DateTimeOffset now)
        {
            var index = FindIndex(sorted, UpdateWords);
            if (index < 0)
            {
                return now;
            }

            var m = UpdateTime.Match(sorted[index].text);
            if (!m.Success)
            {
                return now;
            }

            var dayWord = m.Groups[1].Value;
            var isYesterday = dayWord == "昨日" || dayWord.Equals("Yesterday", StringComparison.OrdinalIgnoreCase);
            var isToday = dayWord == "今日" || dayWord.Equals("Today", StringComparison.OrdinalIgnoreCase);
            if (!isToday && !isYesterday)
            {
                return now;
            }

            var hour = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (hour > 23 || minute > 59)
            {
                return now;
            }

            var date = JstCalendar.ToJst(now).Date.AddDays(isYesterday ? -1 : 0);
            var at = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, JstCalendar.JstOffset);
            return at > now ? now : at;
        }

        private static int FindIndex(List<(OcrLine line, string text)> sorted, string[] words)
        {
            for (var i = 0; i < sorted.Count; i++)
            {
                foreach (var word in words)
                {
                    if (sorted[i].text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>時間の文字列を分にする。時間だけの行でなければ null。</summary>
        public static int? ParseDuration(string text)
        {
            var s = Normalize(text);
            if (s.Length == 0)
            {
                return null;
            }

            var m = DurationJa.Match(s);
            if (!m.Success || (!m.Groups[1].Success && !m.Groups[2].Success))
            {
                m = DurationEn.Match(s);
                if (!m.Success || (!m.Groups[1].Success && !m.Groups[2].Success))
                {
                    return null;
                }
            }

            var hours = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var minutes = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            if (minutes > 59)
            {
                return null;
            }

            return hours * 60 + minutes;
        }

        /// <summary>全角の英数字と記号を半角に、空白を除く (OCR は「4 時間 2 分」のように空白を挟むことがある)。</summary>
        internal static string Normalize(string text)
        {
            var normalized = (text ?? "").Normalize(NormalizationForm.FormKC);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
