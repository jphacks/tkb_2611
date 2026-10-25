using System;
using System.Globalization;

namespace ProjectPenguin.Domain.Clock
{
    /// <summary>
    /// HTTP の Date ヘッダー (RFC 9110 §5.6.7 の HTTP-date) を読む。受け手は 3 つの形式をすべて受け付けなければならない。
    /// <list type="bullet">
    /// <item>IMF-fixdate: <c>Sun, 06 Nov 1994 08:49:37 GMT</c> (送り手はこれだけを使う)</item>
    /// <item>RFC 850 (廃止): <c>Sunday, 06-Nov-94 08:49:37 GMT</c>。2 桁の年は「50 年より先に見えるなら直近の過去」と読む</item>
    /// <item>asctime (廃止): <c>Sun Nov  6 08:49:37 1994</c>。UTC とみなす</item>
    /// </list>
    /// 秒の 60 (うるう秒) は 59 に丸める (DateTime が 60 を表せないため)。曜日は検証しない (日付と食い違っても日付を採る)。
    /// https://www.rfc-editor.org/rfc/rfc9110#section-5.6.7
    /// </summary>
    public static class HttpDate
    {
        private static readonly string[] Months =
        {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
        };

        private static readonly string[] ShortDays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private static readonly string[] LongDays =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
        };

        /// <summary>読めれば true。<paramref name="referenceUtc"/> は RFC 850 の 2 桁の年を解釈するときの「今」(省略時は値の年をそのまま 19xx/20xx で決める)。</summary>
        public static bool TryParse(string value, out DateTimeOffset utc, DateTimeOffset? referenceUtc = null)
        {
            utc = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var s = value.Trim();
            return TryParseImfFixdate(s, out utc)
                || TryParseRfc850(s, referenceUtc, out utc)
                || TryParseAsctime(s, out utc);
        }

        /// <summary>IMF-fixdate で書く (テストと、必要ならリクエストの Date 用)。</summary>
        public static string Format(DateTimeOffset instant)
        {
            return instant.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        // "Sun, 06 Nov 1994 08:49:37 GMT" (固定長 29 文字)
        private static bool TryParseImfFixdate(string s, out DateTimeOffset utc)
        {
            utc = default;
            if (s.Length != 29 || s[3] != ',' || s[4] != ' ' || s[7] != ' ' || s[11] != ' ' || s[16] != ' ' || s[25] != ' ')
            {
                return false;
            }

            if (Array.IndexOf(ShortDays, s.Substring(0, 3)) < 0 || s.Substring(26, 3) != "GMT")
            {
                return false;
            }

            return TryInt(s, 5, 2, out var day)
                && TryMonth(s.Substring(8, 3), out var month)
                && TryInt(s, 12, 4, out var year)
                && TryTime(s, 17, out var h, out var m, out var sec)
                && TryBuild(year, month, day, h, m, sec, out utc);
        }

        // "Sunday, 06-Nov-94 08:49:37 GMT"
        private static bool TryParseRfc850(string s, DateTimeOffset? referenceUtc, out DateTimeOffset utc)
        {
            utc = default;
            var comma = s.IndexOf(',');
            if (comma < 0 || Array.IndexOf(LongDays, s.Substring(0, comma)) < 0)
            {
                return false;
            }

            var rest = s.Substring(comma + 1);

            // " 06-Nov-94 08:49:37 GMT" (23 文字)
            if (rest.Length != 23 || rest[0] != ' ' || rest[3] != '-' || rest[7] != '-' || rest[10] != ' ' || rest[19] != ' '
                || rest.Substring(20, 3) != "GMT")
            {
                return false;
            }

            if (!TryInt(rest, 1, 2, out var day) || !TryMonth(rest.Substring(4, 3), out var month) || !TryInt(rest, 8, 2, out var yy)
                || !TryTime(rest, 11, out var h, out var m, out var sec))
            {
                return false;
            }

            var refYear = (referenceUtc ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)).UtcDateTime.Year;
            var century = refYear / 100 * 100;
            var year = century + yy;

            // RFC 9110: 50 年より先に見える値は、下 2 桁が同じ直近の過去の年と読む
            if (year > refYear + 50)
            {
                year -= 100;
            }

            return TryBuild(year, month, day, h, m, sec, out utc);
        }

        // "Sun Nov  6 08:49:37 1994" (24 文字)
        private static bool TryParseAsctime(string s, out DateTimeOffset utc)
        {
            utc = default;
            if (s.Length != 24 || s[3] != ' ' || s[7] != ' ' || s[10] != ' ' || s[19] != ' ')
            {
                return false;
            }

            if (Array.IndexOf(ShortDays, s.Substring(0, 3)) < 0 || !TryMonth(s.Substring(4, 3), out var month))
            {
                return false;
            }

            int day;
            if (s[8] == ' ')
            {
                if (!TryInt(s, 9, 1, out day))
                {
                    return false;
                }
            }
            else if (!TryInt(s, 8, 2, out day))
            {
                return false;
            }

            return TryTime(s, 11, out var h, out var m, out var sec)
                && TryInt(s, 20, 4, out var year)
                && TryBuild(year, month, day, h, m, sec, out utc);
        }

        private static bool TryTime(string s, int at, out int h, out int m, out int sec)
        {
            h = m = sec = 0;
            return s.Length >= at + 8 && s[at + 2] == ':' && s[at + 5] == ':'
                && TryInt(s, at, 2, out h) && TryInt(s, at + 3, 2, out m) && TryInt(s, at + 6, 2, out sec);
        }

        private static bool TryMonth(string token, out int month)
        {
            month = Array.IndexOf(Months, token) + 1;
            return month > 0;
        }

        private static bool TryInt(string s, int at, int length, out int value)
        {
            value = 0;
            if (at < 0 || at + length > s.Length)
            {
                return false;
            }

            for (var i = at; i < at + length; i++)
            {
                var c = s[i];
                if (c < '0' || c > '9')
                {
                    return false;
                }

                value = value * 10 + (c - '0');
            }

            return true;
        }

        private static bool TryBuild(int year, int month, int day, int h, int m, int sec, out DateTimeOffset utc)
        {
            utc = default;
            if (sec == 60)
            {
                sec = 59;
            }

            if (year < 1 || year > 9999 || day < 1 || day > DateTime.DaysInMonth(year, month) || h > 23 || m > 59 || sec > 59)
            {
                return false;
            }

            utc = new DateTimeOffset(year, month, day, h, m, sec, TimeSpan.Zero);
            return true;
        }
    }
}
