using System;

namespace ProjectPenguin.Domain.Clock
{
    /// <summary>
    /// JST の日番号と週番号 (scoring_v2.py の「day」「week」)。
    /// 日番号 = 2026-01-01 (JST) を 0 とする通し番号。週番号 = 月曜 0 時 (JST) 始まりの通し番号で、
    /// 2025-12-29 (月) 〜 2026-01-04 (日) が 0。端末のタイムゾーン設定には依存しない (常に UTC+9)。
    /// </summary>
    public static class JstCalendar
    {
        /// <summary>日本は夏時間がないので固定オフセットでよい。</summary>
        public static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

        /// <summary>日番号 0 の日 (2026-01-01, 木曜)。</summary>
        public static readonly DateTime EpochDate = new DateTime(2026, 1, 1);

        // 2026-01-01 は木曜。その週の月曜 (2025-12-29) は日番号 -3。
        private const int EpochDayOffsetFromMonday = 3;

        public static DateTimeOffset ToJst(DateTimeOffset instant)
        {
            return instant.ToOffset(JstOffset);
        }

        /// <summary>その瞬間の JST の日番号。</summary>
        public static int DayNumber(DateTimeOffset instant)
        {
            var jstDate = ToJst(instant).Date;
            return (int)(jstDate - EpochDate).TotalDays;
        }

        /// <summary>レシートの印字日 (purchase_date、JST の日付) の日番号。</summary>
        public static int DayNumber(DateTime jstDate)
        {
            return (int)(jstDate.Date - EpochDate).TotalDays;
        }

        /// <summary>日番号から週番号 (月曜始まり)。負の日番号でも床関数で正しく割る。</summary>
        public static int WeekOfDay(int dayNumber)
        {
            return FloorDiv(dayNumber + EpochDayOffsetFromMonday, 7);
        }

        public static int WeekNumber(DateTimeOffset instant)
        {
            return WeekOfDay(DayNumber(instant));
        }

        /// <summary>その週の月曜の日番号。</summary>
        public static int FirstDayOfWeek(int weekNumber)
        {
            return weekNumber * 7 - EpochDayOffsetFromMonday;
        }

        /// <summary>日番号から JST の日付。</summary>
        public static DateTime DateOfDay(int dayNumber)
        {
            return EpochDate.AddDays(dayNumber);
        }

        /// <summary>
        /// receipt_schema.json の purchase_date ("YYYY-MM-DD"、null 可) を日番号にする。読めなければ null。
        /// </summary>
        public static int? ParsePurchaseDate(string purchaseDate)
        {
            if (string.IsNullOrEmpty(purchaseDate))
            {
                return null;
            }

            if (DateTime.TryParseExact(
                    purchaseDate,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date))
            {
                return DayNumber(date);
            }

            return null;
        }

        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0)))
            {
                q--;
            }

            return q;
        }
    }
}
