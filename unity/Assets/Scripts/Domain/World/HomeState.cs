using System;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Domain.World
{
    /// <summary>
    /// ホームの見た目と吹き出しに要る、ある瞬間のゲームの状態。操作の前後で取って比べ、何が起きたかを求める。
    /// </summary>
    public readonly struct HomeState
    {
        public HomeState(int day, double ice, int tier, int shields, int shieldsUsed, int penguins, double shardsExact,
            int totalRecordDays, LastRecord lastRecord)
        {
            Day = day;
            Ice = ice;
            Tier = tier;
            Shields = shields;
            ShieldsUsed = shieldsUsed;
            Penguins = penguins;
            ShardsExact = shardsExact;
            TotalRecordDays = totalRecordDays;
            LastRecord = lastRecord;
        }

        /// <summary>アプリの日 (03:00 JST 区切り)。</summary>
        public int Day { get; }

        public double Ice { get; }
        public int Tier { get; }
        public int Shields { get; }
        public int ShieldsUsed { get; }
        public int Penguins { get; }
        public double ShardsExact { get; }
        public int TotalRecordDays { get; }
        public LastRecord LastRecord { get; }

        public bool RecordedToday => LastRecord == LastRecord.Today;

        /// <summary>今の <see cref="GameSession"/> から取る。日を進める (<see cref="GameSession.Advance"/>) のは呼び出し側。</summary>
        public static HomeState Of(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var today = session.LastStartedDay;
            return new HomeState(today, session.Ice, session.Tier, session.Shields, session.ShieldsUsed,
                session.Arrivals.Count, session.ShardsExact, session.TotalRecordDays, LastRecordOf(session, today));
        }

        /// <summary>前後の状態から、吹き出しで扱う出来事を求める。</summary>
        public static SpeechEvent EventsBetween(HomeState before, HomeState after)
        {
            var events = SpeechEvent.None;
            if (after.Penguins > before.Penguins)
            {
                events |= SpeechEvent.NewPenguin;
            }

            if (after.ShieldsUsed > before.ShieldsUsed)
            {
                events |= SpeechEvent.ShieldUsed;
            }

            if (after.Tier > before.Tier)
            {
                events |= SpeechEvent.TierUp;
            }

            if (after.RecordedToday && !(before.RecordedToday && before.Day == after.Day))
            {
                events |= SpeechEvent.Recorded;
            }

            // 記録で上限を超えた分もかけらになるが、「届いた」と言うのは日が進んだとき (月曜の選び方の氷) だけ。
            if (after.Day > before.Day && after.ShardsExact > before.ShardsExact)
            {
                events |= SpeechEvent.ShardsArrived;
            }

            return events;
        }

        private static LastRecord LastRecordOf(GameSession session, int today)
        {
            if (session.IsRecorded(today))
            {
                return LastRecord.Today;
            }

            if (session.IsRecorded(today - 1))
            {
                return LastRecord.Yesterday;
            }

            return session.TotalRecordDays > 0 ? LastRecord.TwoOrMoreDaysAgo : LastRecord.Never;
        }
    }
}
