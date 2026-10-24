using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectPenguin.Domain.Encounter
{
    /// <summary>
    /// すれ違いのトークン (api/openapi.yaml の EncounterToken)。端末が作る 16 バイトの乱数を base64url (パディングなし) にした 22 文字。
    /// 端末や人とは結び付かない乱数で、アプリの 1 日ごとに作り直す。ログには出さない (.claude/rules/privacy.md)。
    /// </summary>
    public static class EncounterToken
    {
        public const int ByteLength = 16;
        public const int Length = 22;

        // .NET の $ は末尾の改行の手前にも当たるので \z で終わりを決める。
        private static readonly Regex Shape = new Regex(@"^[A-Za-z0-9_-]{22}\z", RegexOptions.CultureInvariant);

        public static string FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length != ByteLength)
            {
                throw new ArgumentException("トークンの元は 16 バイト", nameof(bytes));
            }

            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>暗号用の乱数で新しいトークンを作る。</summary>
        public static string Create()
        {
            var bytes = new byte[ByteLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return FromBytes(bytes);
        }

        public static bool IsValid(string token) => token != null && Shape.IsMatch(token);

        /// <summary>
        /// 2 つのトークンの組の ID。並べる順によらず、両方の端末で同じ値になる。
        /// 定義は api/openapi.yaml の EncounterResult.pair_id (backend の encounters.pair_id と同じ計算)。
        /// </summary>
        public static string PairId(string a, string b)
        {
            if (!IsValid(a) || !IsValid(b))
            {
                throw new ArgumentException("トークンの形が違う");
            }

            var low = string.CompareOrdinal(a, b) <= 0 ? a : b;
            var high = ReferenceEquals(low, a) ? b : a;
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.ASCII.GetBytes(low + high));
            }

            var id = new StringBuilder(16);
            for (var i = 0; i < 8; i++)
            {
                id.Append(hash[i].ToString("x2"));
            }

            return id.ToString();
        }
    }
}
