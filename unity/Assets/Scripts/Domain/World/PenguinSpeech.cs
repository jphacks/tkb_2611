using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Domain.World
{
    /// <summary>時間帯 (JST)。空の色と吹き出しの選び分けに使う。段とは独立。</summary>
    public enum DayPart
    {
        /// <summary>5〜10 時。</summary>
        Morning,

        /// <summary>10〜17 時。</summary>
        Daytime,

        /// <summary>17〜21 時。</summary>
        Evening,

        /// <summary>21〜5 時。記録を促さない。</summary>
        Night,
    }

    /// <summary>直前の操作で起きた出来事。複数が同時に起きうる。</summary>
    [Flags]
    public enum SpeechEvent
    {
        None = 0,
        NewPenguin = 1,
        ShardsArrived = 2,
        ShieldUsed = 4,
        TierUp = 8,
        Recorded = 16,
    }

    /// <summary>最後に記録した日。</summary>
    public enum LastRecord
    {
        Today,
        Yesterday,
        TwoOrMoreDaysAgo,
        Never,
    }

    /// <summary>吹き出しの区分。上から順に優先する (15c §2.2)。</summary>
    public enum SpeechCategory
    {
        Event,
        Comeback,
        Greeting,
        IceLow,
        Usual,
    }

    /// <summary>吹き出しを選ぶための状況。</summary>
    public readonly struct SpeechContext
    {
        public SpeechContext(int tier, DayPart dayPart, SpeechEvent events, bool isComeback, bool isFirstOpenToday,
            LastRecord lastRecord, bool iceLowShownToday)
        {
            Tier = tier;
            DayPart = dayPart;
            Events = events;
            IsComeback = isComeback;
            IsFirstOpenToday = isFirstOpenToday;
            LastRecord = lastRecord;
            IceLowShownToday = iceLowShownToday;
        }

        public int Tier { get; }
        public DayPart DayPart { get; }
        public SpeechEvent Events { get; }

        /// <summary>前回の起動から 3 日以上あいた。空いた日数は言わない。</summary>
        public bool IsComeback { get; }

        public bool IsFirstOpenToday { get; }
        public LastRecord LastRecord { get; }

        /// <summary>氷が少ないことを言う行は 1 日 1 回まで。</summary>
        public bool IceLowShownToday { get; }
    }

    /// <summary>吹き出し 1 行。</summary>
    public sealed class SpeechLine
    {
        internal SpeechLine(string id, SpeechCategory category, string text, Func<SpeechContext, bool> when)
        {
            Id = id;
            Category = category;
            Text = text;
            When = when;
        }

        /// <summary>直近の表示を覚えるための ID (15c §2.3 の E01 など)。</summary>
        public string Id { get; }

        public SpeechCategory Category { get; }
        public string Text { get; }
        internal Func<SpeechContext, bool> When { get; }
    }

    /// <summary>
    /// ペンギンの吹き出しの選び方 (15c §2)。主語はペンギンで、利用者を評価しない。数字・kg・現実のペンギンの被害は言わない。
    /// 起動のたびに、優先順の最初の区分で条件に合う行から、直近 <see cref="RecentLimit"/> 件を除いて無作為に 1 行を選ぶ。
    /// </summary>
    public static class PenguinSpeech
    {
        /// <summary>同じ行を繰り返さないために覚えておく件数。</summary>
        public const int RecentLimit = 6;

        private static readonly SpeechLine[] Lines =
        {
            Line("E01", SpeechCategory.Event, "あたらしい仲間が来たよ！", c => Has(c, SpeechEvent.NewPenguin)),
            Line("E02", SpeechCategory.Event, "流氷に乗って、だれか来たみたい。", c => Has(c, SpeechEvent.NewPenguin)),
            Line("E03", SpeechCategory.Event, "海に、氷のかけらが浮かんでるよ。", c => Has(c, SpeechEvent.ShardsArrived)),
            Line("E04", SpeechCategory.Event, "氷のブロックが守ってくれたよ。", c => Has(c, SpeechEvent.ShieldUsed)),
            Line("E05", SpeechCategory.Event, "氷が、ひとまわり広くなった！", c => Has(c, SpeechEvent.TierUp)),
            Line("E06", SpeechCategory.Event, "記録ありがとう。足もとがしっかりしたよ。",
                c => Has(c, SpeechEvent.Recorded) && c.Tier >= 2),
            Line("E07", SpeechCategory.Event, "氷が戻ってきた。もう少しで元どおり。",
                c => Has(c, SpeechEvent.Recorded) && c.Tier <= 1),
            Line("R01", SpeechCategory.Comeback, "おかえり！ 待ってたよ。", c => c.IsComeback),
            // 夜は記録を促さない (15c §2.1 の 8) ので、「なにを記録する？」は夜に出さない。
            Line("R02", SpeechCategory.Comeback, "おかえり。今日はなにを記録する？", c => c.IsComeback && c.DayPart != DayPart.Night),
            Line("G01", SpeechCategory.Greeting, "おはよう。きのうの記録、氷になってるよ。",
                c => Greeting(c, DayPart.Morning) && c.LastRecord == LastRecord.Yesterday),
            Line("G02", SpeechCategory.Greeting, "おはよう。今日のミッション、選んでみる？",
                c => Greeting(c, DayPart.Morning) && c.LastRecord == LastRecord.TwoOrMoreDaysAgo),
            Line("G03", SpeechCategory.Greeting, "おはよう。まずは 1 つ、やってみよう。",
                c => Greeting(c, DayPart.Morning) && c.LastRecord == LastRecord.Never),
            Line("G04", SpeechCategory.Greeting, "今日はもう記録したね。のんびりしよう。",
                c => Greeting(c, DayPart.Daytime) && c.LastRecord == LastRecord.Today),
            Line("G05", SpeechCategory.Greeting, "お昼だね。エコな写真があったら見せてね。",
                c => Greeting(c, DayPart.Daytime) && c.LastRecord != LastRecord.Today),
            Line("G06", SpeechCategory.Greeting, "おつかれさま。今日の氷、いい感じ。",
                c => Greeting(c, DayPart.Evening) && c.LastRecord == LastRecord.Today),
            Line("G07", SpeechCategory.Greeting, "今日のミッション、できたか教えてね。",
                c => Greeting(c, DayPart.Evening) && c.LastRecord != LastRecord.Today),
            Line("G08", SpeechCategory.Greeting, "こんばんは。今日はゆっくり休もうね。", c => Greeting(c, DayPart.Night)),
            Line("L01", SpeechCategory.IceLow, "ひびが入ってきた。ミッション 1 つで戻るよ。", c => IceLow(c, 1)),
            Line("L02", SpeechCategory.IceLow, "ちょっと寒いね。エコ写真 1 枚で直せるよ。", c => IceLow(c, 1)),
            Line("L03", SpeechCategory.IceLow, "みんなで寄りそってるよ。1 つで大きく戻るよ。", c => IceLow(c, 0)),
            Line("L04", SpeechCategory.IceLow, "小屋であたたまってる仲間もいるよ。", c => IceLow(c, 0)),
            Line("A01", SpeechCategory.Usual, "氷が広くて、すべり放題！", c => c.Tier == 4 && c.DayPart == DayPart.Daytime),
            Line("A02", SpeechCategory.Usual, "流氷までジャンプできそう。", c => c.Tier == 4),
            Line("A03", SpeechCategory.Usual, "今日も氷がきらきらしてる。", c => c.Tier == 3 && c.DayPart == DayPart.Morning),
            Line("A04", SpeechCategory.Usual, "夕焼けの氷、きれいだね。", c => c.Tier == 3 && c.DayPart == DayPart.Evening),
            Line("A05", SpeechCategory.Usual, "いつもの氷。落ちつくなあ。", c => c.Tier == 2),
            Line("A06", SpeechCategory.Usual, "魚、どこにいるかな。", c => c.Tier == 2 && c.DayPart == DayPart.Daytime),
            Line("A07", SpeechCategory.Usual, "くっついてると、あったかいね。", c => c.Tier == 1),
            Line("A08", SpeechCategory.Usual, "小屋の灯り、あったかいね。", c => c.Tier == 0 && c.DayPart == DayPart.Night),
            Line("A09", SpeechCategory.Usual, "星がよく見えるよ。おやすみ。", c => c.DayPart == DayPart.Night),
        };

        // 段 3 の昼のように、ふだんの行が 1 つも当てはまらない組み合わせで使う行。
        private static readonly SpeechLine Fallback =
            Line("A00", SpeechCategory.Usual, "今日もいっしょにいようね。", _ => true);

        private static readonly SpeechCategory[] Order =
        {
            SpeechCategory.Event, SpeechCategory.Comeback, SpeechCategory.Greeting, SpeechCategory.IceLow, SpeechCategory.Usual,
        };

        public static IReadOnlyList<SpeechLine> All => Lines;

        /// <summary>その瞬間の時間帯 (JST)。</summary>
        public static DayPart DayPartOf(DateTimeOffset instant)
        {
            var hour = JstCalendar.ToJst(instant).Hour;
            if (hour >= 5 && hour < 10)
            {
                return DayPart.Morning;
            }

            if (hour >= 10 && hour < 17)
            {
                return DayPart.Daytime;
            }

            return hour >= 17 && hour < 21 ? DayPart.Evening : DayPart.Night;
        }

        /// <summary>
        /// 吹き出しを 1 行選ぶ。<paramref name="randomIndex"/> は 0 以上・引数未満の整数を返す関数 (テストでは固定にする)。
        /// 区分の中で直近の行しか残らないときは、繰り返しを許してその区分から選ぶ。
        /// </summary>
        public static SpeechLine Pick(SpeechContext context, IReadOnlyCollection<string> recentIds, Func<int, int> randomIndex)
        {
            if (randomIndex == null)
            {
                throw new ArgumentNullException(nameof(randomIndex));
            }

            foreach (var category in Order)
            {
                var matching = new List<SpeechLine>();
                foreach (var line in Lines)
                {
                    if (line.Category == category && line.When(context))
                    {
                        matching.Add(line);
                    }
                }

                if (matching.Count == 0)
                {
                    continue;
                }

                var fresh = recentIds == null ? matching : matching.FindAll(l => !Contains(recentIds, l.Id));
                var pool = fresh.Count > 0 ? fresh : matching;
                return pool[randomIndex(pool.Count)];
            }

            return Fallback;
        }

        private static SpeechLine Line(string id, SpeechCategory category, string text, Func<SpeechContext, bool> when)
        {
            return new SpeechLine(id, category, text, when);
        }

        private static bool Has(SpeechContext c, SpeechEvent e)
        {
            return (c.Events & e) != 0;
        }

        private static bool Greeting(SpeechContext c, DayPart part)
        {
            return c.IsFirstOpenToday && c.DayPart == part;
        }

        private static bool IceLow(SpeechContext c, int tier)
        {
            return c.Tier == tier && !c.IceLowShownToday;
        }

        private static bool Contains(IReadOnlyCollection<string> ids, string id)
        {
            foreach (var x in ids)
            {
                if (x == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
