using System.Collections.Generic;

namespace ProjectPenguin.Domain.Game
{
    /// <summary>来訪の理由。</summary>
    public enum ArrivalReason
    {
        Onboarding,
        Milestone,
        Weekly,
    }

    /// <summary>来たペンギン 1 羽。来たペンギンは消えない。</summary>
    public readonly struct PenguinArrival
    {
        public PenguinArrival(string species, int day, ArrivalReason reason)
        {
            Species = species;
            Day = day;
            Reason = reason;
        }

        public string Species { get; }

        /// <summary>来たアプリの日。</summary>
        public int Day { get; }

        public ArrivalReason Reason { get; }
    }

    /// <summary>
    /// 19 種と、節目・週ごとの順番 (15c §3.2・§3.3)。
    /// 種のキーは 17b-zukan.csv の <c>id</c> 列 ("adelie"、"little" など) で、コードと保存データにはこれだけを書く
    /// (19 §5、18a C26・D18)。和名は <see cref="DisplayNameJa"/> で表示にだけ使う。
    /// </summary>
    public static class PenguinCatalog
    {
        public const string Onboarding = "adelie";
        public const string FirstRecord = "little";
        public const string AllKinds = "magellanic";
        public const string FirstComeback = "galapagos";
        public const string FiveMissions = "chinstrap";
        public const string Total14 = "king";
        public const string Total30 = "emperor";
        public const string FourWeeks = "yellow_eyed";

        /// <summary>19 種の id (17b-zukan.csv の行の順)。</summary>
        public static readonly IReadOnlyList<string> AllIds = new[]
        {
            "emperor", "king", "adelie", "chinstrap", "gentoo",
            "little", "galapagos", "humboldt", "magellanic", "african",
            "yellow_eyed", "macaroni", "royal", "northern_rockhopper", "western_rockhopper",
            "eastern_rockhopper", "fiordland", "snares", "erect_crested",
        };

        /// <summary>節目の種 (表の順)。週の順番表を使い切った後はここから未着の種を出す。</summary>
        public static readonly IReadOnlyList<string> MilestoneOrder = new[]
        {
            Onboarding, FirstRecord, AllKinds, FirstComeback, FiveMissions, Total14, Total30, FourWeeks,
        };

        /// <summary>週ごとの来訪の順番 (節目の種を含まない 11 種)。</summary>
        public static readonly IReadOnlyList<string> WeeklyOrder = new[]
        {
            "gentoo", "humboldt", "macaroni", "royal", "african",
            "fiordland", "snares", "northern_rockhopper", "western_rockhopper",
            "eastern_rockhopper", "erect_crested",
        };

        /// <summary>表示用の和名 (17b-zukan.csv の <c>name_ja</c>、IOC 15.2 の日本語欄)。ロジックでは使わない。</summary>
        public static readonly IReadOnlyDictionary<string, string> DisplayNameJa = new Dictionary<string, string>
        {
            { "emperor", "コウテイペンギン" },
            { "king", "オウサマペンギン" },
            { "adelie", "アデリーペンギン" },
            { "chinstrap", "ヒゲペンギン" },
            { "gentoo", "ジェンツーペンギン" },
            { "little", "コビトペンギン" },
            { "galapagos", "ガラパゴスペンギン" },
            { "humboldt", "フンボルトペンギン" },
            { "magellanic", "マゼランペンギン" },
            { "african", "ケープペンギン" },
            { "yellow_eyed", "キンメペンギン" },
            { "macaroni", "マカロニペンギン" },
            { "royal", "ロイヤルペンギン" },
            { "northern_rockhopper", "キタイワトビペンギン" },
            { "western_rockhopper", "ミナミイワトビペンギン" },
            { "eastern_rockhopper", "ヒガシイワトビペンギン（仮称）" },
            { "fiordland", "キマユペンギン" },
            { "snares", "ハシブトペンギン" },
            { "erect_crested", "マユダチペンギン" },
        };

        /// <summary><see cref="AllIds"/> の中の番号。知らない id なら -1。</summary>
        public static int IndexOf(string id)
        {
            for (var i = 0; i < AllIds.Count; i++)
            {
                if (AllIds[i] == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>19 種の id のどれかか。</summary>
        public static bool IsKnown(string id)
        {
            return id != null && DisplayNameJa.ContainsKey(id);
        }
    }
}
