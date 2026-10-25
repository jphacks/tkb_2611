using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Domain.Game
{
    /// <summary>記録の種類 (15c §3.3 の「記録の仕方」)。</summary>
    public enum RecordKind
    {
        Receipt,
        Mission,
        NoShoppingMission,
    }

    /// <summary>ミッションの確かめ方 (missions_catalog.csv の verification)。</summary>
    public enum MissionVerification
    {
        Receipt,
        Self,
        Photo,
    }

    /// <summary>夜の振り返りの答え。どれを選んでもその日は記録になる。</summary>
    public enum CheckInAnswer
    {
        Done,
        Partial,
        Failed,
    }

    /// <summary>受け付けたレシート。印字日は週の選び方の氷・ミッションの判定・重複の判定にだけ使う。</summary>
    public sealed class ReceiptRecord
    {
        public ReceiptRecord(string receiptId, int printDay, IReadOnlyList<string> verifies = null)
        {
            ReceiptId = receiptId ?? throw new ArgumentNullException(nameof(receiptId));
            PrintDay = printDay;
            Verifies = verifies ?? Array.Empty<string>();
        }

        /// <summary>重複判定のキー (店・日時・合計などから作る)。</summary>
        public string ReceiptId { get; }

        /// <summary>印字日 (JST の日番号)。</summary>
        public int PrintDay { get; }

        /// <summary>このレシートが receipt_rule を満たすミッション ID。</summary>
        public IReadOnlyList<string> Verifies { get; }
    }

    /// <summary>1 日の終わりの状態 (演出・保存・テスト用)。</summary>
    public readonly struct DaySnapshot
    {
        public DaySnapshot(int day, double ice, int shields, double shardsExact, int shards, int tier, int totalRecordDays,
            int penguins, int gold, int whiteCounted, int stampPoints, int shieldsUsed, bool recorded)
        {
            Day = day;
            Ice = ice;
            Shields = shields;
            ShardsExact = shardsExact;
            Shards = shards;
            Tier = tier;
            TotalRecordDays = totalRecordDays;
            Penguins = penguins;
            Gold = gold;
            WhiteCounted = whiteCounted;
            StampPoints = stampPoints;
            ShieldsUsed = shieldsUsed;
            Recorded = recorded;
        }

        public int Day { get; }
        public double Ice { get; }
        public int Shields { get; }
        public double ShardsExact { get; }
        public int Shards { get; }
        public int Tier { get; }
        public int TotalRecordDays { get; }
        public int Penguins { get; }
        public int Gold { get; }
        public int WhiteCounted { get; }
        public int StampPoints { get; }
        public int ShieldsUsed { get; }
        public bool Recorded { get; }
    }

    /// <summary>段が変わった記録。</summary>
    public readonly struct TierChange
    {
        public TierChange(int day, int from, int to)
        {
            Day = day;
            From = from;
            To = to;
        }

        public int Day { get; }
        public int From { get; }
        public int To { get; }
    }

    /// <summary>直前の操作で時計をどう扱ったか (v4)。保存しない。</summary>
    public enum ClockStatus
    {
        /// <summary>通常。</summary>
        Ok,

        /// <summary>時計が最後に見た日より前だった。操作は最後に見た日のものとして扱った。UI で「端末の時計が戻っています」を 1 回出す。</summary>
        Behind,

        /// <summary>信頼できない時計が大きく先に進んでいる。日を確定させず、操作も受け付けていない。<see cref="GameSession.ConfirmClock"/> で確定できる。</summary>
        AheadPending,
    }

    /// <summary>
    /// 統合ルールの状態機械 (game_sim.py の <c>Session</c> と 1 対 1)。現在時刻は <see cref="IClock"/> から読む。
    /// <list type="bullet">
    /// <item>アプリの日は 03:00 JST で切り替わる。記録日は操作したアプリの日 (レシートの印字日ではない)。</item>
    /// <item>記録 = レシート登録 または 選んだミッションの夜の振り返り (「できなかった」も含む)。1 日 1 回 +2、氷 60 未満なら +4。</item>
    /// <item>記録のない日は盾 1 枚、盾がなければ −2 (下限 40)。盾は月曜 03:00 に 2 枚へ戻る。</item>
    /// <item>盾で守れず溶けた日が 1 日以上ある空白の後の最初の記録に、空白中に溶けた量 × 0.5 (0.5 切り上げ、上限 10) を足す (v4。v3 は空白 3 日以上)。</item>
    /// <item>上限 150 を超えた分は氷のかけら (1:1)。週の選び方の氷 B は月曜の始まりに足す。</item>
    /// <item>ミッションは氷を増やさない。金スタンプ (レシートで確認) 2 点、白スタンプ (自己申告・写真の「できた」) 1 点、白は 1 日 1・週 5 まで数える。</item>
    /// <item>ペンギンは週 1 羽まで (節目が先、週ごとの分は順番待ち)。来たペンギンは消えない。</item>
    /// <item>時計が最後に見た日より前に戻っていたら、操作は最後に見た日のものとして扱う (v4。昔の日は埋め直せない)。</item>
    /// <item>信頼できない時計で最後に見た日から <see cref="GameParameters.MaxCatchUpDays"/> を超えて進んだら、確定させずに保留する (v4)。</item>
    /// </list>
    /// 保存は、このクラスの状態をそのまま書き出す前提 (保存形式は未定。15c §6)。
    /// </summary>
    public sealed class GameSession
    {
        private readonly IClock _clock;
        private readonly GameParameters _p;
        private readonly Func<int, IReadOnlyList<ReceiptRecord>, double> _bonusFn;
        private readonly Action<GameSession, int> _onDaySettled;

        private readonly HashSet<int> _recordedDays = new HashSet<int>();
        private readonly List<ReceiptRecord> _receipts = new List<ReceiptRecord>();
        private readonly HashSet<string> _receiptIds = new HashSet<string>();
        private readonly SortedDictionary<int, List<KeyValuePair<string, MissionVerification>>> _selected =
            new SortedDictionary<int, List<KeyValuePair<string, MissionVerification>>>();
        private readonly HashSet<(int, string)> _checked = new HashSet<(int, string)>();
        private readonly HashSet<(int, string)> _verified = new HashSet<(int, string)>();
        private readonly Dictionary<int, int> _whiteByDay = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _whiteByWeek = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _weekRecordDays = new Dictionary<int, int>();
        private readonly HashSet<RecordKind> _kindsUsed = new HashSet<RecordKind>();
        private readonly HashSet<string> _doneMissions = new HashSet<string>();
        private readonly List<PenguinArrival> _arrivals = new List<PenguinArrival>();
        private readonly List<string> _milestoneQueue = new List<string>();
        private readonly List<TierChange> _tierLog = new List<TierChange>();
        private readonly List<KeyValuePair<int, int>> _comebackLog = new List<KeyValuePair<int, int>>();

        private int _weeklyIndex;
        private int _lastArrivalWeek = int.MinValue;

        /// <param name="clock">現在時刻。</param>
        /// <param name="weeklyBonus">閉じた週の選び方の氷 B (週番号, 印字日がその週のレシート) → 0〜4。本番は ScoreRules.ScoreWeek → WeeklyBonus。</param>
        /// <param name="parameters">null なら <see cref="GameParameters.Default"/>。</param>
        /// <param name="onDaySettled">1 日が確定するたびに呼ぶ (その日の終わりの状態。次の日の月曜処理の前)。</param>
        public GameSession(IClock clock, Func<int, IReadOnlyList<ReceiptRecord>, double> weeklyBonus,
            GameParameters parameters = null, Action<GameSession, int> onDaySettled = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _bonusFn = weeklyBonus ?? throw new ArgumentNullException(nameof(weeklyBonus));
            _p = parameters ?? GameParameters.Default;
            _onDaySettled = onDaySettled;
            Ice = _p.Initial;
            Shields = _p.ShieldsStart;
            Tier = IceTierRule.Of(_p.Initial, _p);
            FirstDay = AppCalendar.AppDay(_clock.UtcNow);
            LastStartedDay = FirstDay - 1;
            LastSettledDay = FirstDay - 1;
            StartDay(FirstDay, true);
        }

        public int FirstDay { get; }
        public int LastStartedDay { get; private set; }
        public int LastSettledDay { get; private set; }
        public double Ice { get; private set; }
        public int Shields { get; private set; }
        public int Tier { get; private set; }
        public double ShardsExact { get; private set; }

        /// <summary>氷のかけら (整数)。B が小数なので端数は <see cref="ShardsExact"/> に持ち越す。</summary>
        public int Shards => (int)Math.Floor(ShardsExact + 1e-9);

        public int TotalRecordDays { get; private set; }
        public int Streak { get; private set; }
        public int GapDays { get; private set; }
        public double GapMelted { get; private set; }
        public int Comebacks { get; private set; }
        public int ShieldsUsed { get; private set; }
        public int Gold { get; private set; }
        public int WhitePressed { get; private set; }
        public int WhiteCounted { get; private set; }
        public int ConsecutiveWeeks { get; private set; }
        public int WeeklyPending { get; private set; }
        public int StampPoints => Gold * _p.GoldPoints + WhiteCounted * _p.WhitePoints;
        public IReadOnlyList<PenguinArrival> Arrivals => _arrivals;
        public IReadOnlyList<string> MilestoneQueue => _milestoneQueue;
        public IReadOnlyList<TierChange> TierLog => _tierLog;

        /// <summary>(日, おかえりで足した量)。</summary>
        public IReadOnlyList<KeyValuePair<int, int>> ComebackLog => _comebackLog;

        /// <summary>時計が戻っていた操作の回数 (保存しない)。</summary>
        public int ClockRollbacks { get; private set; }

        /// <summary>直前の <see cref="Advance"/> の結果 (保存しない)。</summary>
        public ClockStatus ClockStatus { get; private set; }

        /// <summary>保留している先の日 (<see cref="ClockStatus.AheadPending"/> のとき)。</summary>
        public int? PendingAheadDay { get; private set; }

        public bool IsRecorded(int day) => _recordedDays.Contains(day);

        /// <summary>やりとげたミッションの種類の数 (図鑑の解放条件の進み具合に使う)。</summary>
        public int DoneMissionCount => _doneMissions.Count;

        /// <summary>その日に選んだミッション (選んだ順)。画面に出すための読み取り専用。</summary>
        public IReadOnlyList<string> SelectedOn(int day) =>
            _selected.TryGetValue(day, out var sel) ? sel.Select(x => x.Key).ToList() : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>その日のミッションを振り返り済みか。</summary>
        public bool IsCheckedIn(int day, string missionId) => _checked.Contains((day, missionId));

        /// <summary>1 日に選べるミッションの数。</summary>
        public int SelectionsPerDay => _p.SelectionsPerDay;

        /// <summary>その週 (月曜始まり) の記録日の数。</summary>
        public int RecordDaysInWeek(int week) => _recordedDays.Count(d => AppCalendar.WeekOf(d) == week);

        /// <summary>時計から求めた今日のアプリの日。</summary>
        public int Today => AppCalendar.AppDay(_clock.UtcNow);

        /// <summary>
        /// 今日より前の未確定の日を確定させ、今日を始める (月曜なら盾・B・来訪)。
        /// 時計が戻っていれば最後に見た日を返す (v3 の値では null)。
        /// 信頼できない時計が <see cref="GameParameters.MaxCatchUpDays"/> を超えて進んでいれば、何もせず null。
        /// </summary>
        public int? Advance()
        {
            return AdvanceCore(false);
        }

        /// <summary>保留した先送りを確定させる。Systems 側でサーバー時刻などにより時刻を確かめた後や、利用者が端末の時刻で進めると選んだときに呼ぶ。</summary>
        public int? ConfirmClock()
        {
            return AdvanceCore(true);
        }

        private bool ClockIsTrusted => _clock is ITrustedClock t && t.IsTrusted;

        /// <summary>今日として扱う日を決める。巻き戻りは最後に見た日に揃え、信頼できない時計の大きな先送りは保留 (null) にする。</summary>
        private int? ResolveToday(bool force)
        {
            var d = Today;
            if (d < LastStartedDay)
            {
                ClockRollbacks++;
                PendingAheadDay = null;
                ClockStatus = ClockStatus.Behind;
                return _p.RollbackClamp ? LastStartedDay : (int?)null;
            }

            if (_p.MaxCatchUpDays.HasValue && d - LastStartedDay > _p.MaxCatchUpDays.Value && !(force || ClockIsTrusted))
            {
                PendingAheadDay = d;
                ClockStatus = ClockStatus.AheadPending;
                return null;
            }

            PendingAheadDay = null;
            ClockStatus = ClockStatus.Ok;
            return d;
        }

        private int? AdvanceCore(bool force)
        {
            var resolved = ResolveToday(force);
            if (resolved == null)
            {
                return null;
            }

            var today = resolved.Value;

            for (var d = LastSettledDay + 1; d < today; d++)
            {
                if (d > LastStartedDay)
                {
                    StartDay(d, false);
                }

                if (!_recordedDays.Contains(d))
                {
                    NonRecord(d);
                }

                LastSettledDay = d;
                _onDaySettled?.Invoke(this, d);
            }

            if (today > LastStartedDay)
            {
                StartDay(today, false);
            }

            return today;
        }

        /// <summary>記録を 1 件登録する (写真でもレシートでも、記録の手段は問わない)。重複 (同じ ID) と、保留中の先送りは false。</summary>
        public bool ScanReceipt(ReceiptRecord receipt)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            var d = Advance();
            if (d == null || _receiptIds.Contains(receipt.ReceiptId))
            {
                return false;
            }

            _receiptIds.Add(receipt.ReceiptId);
            _receipts.Add(receipt);
            foreach (var kv in _selected)
            {
                var sd = kv.Key;
                foreach (var sel in kv.Value)
                {
                    if (sel.Value != MissionVerification.Receipt || _verified.Contains((sd, sel.Key)))
                    {
                        continue;
                    }

                    if (sd <= receipt.PrintDay && receipt.PrintDay <= sd + 2 && receipt.Verifies.Contains(sel.Key))
                    {
                        _verified.Add((sd, sel.Key));
                        Gold++;
                        MarkDoneMission(sel.Key);
                    }
                }
            }

            Record(d.Value, RecordKind.Receipt);
            return true;
        }

        /// <summary>記録しなかった日に溶ける量を減らす割合 (0〜<see cref="GameParameters.MaxMeltReduction"/>)。島の氷室の効き目。</summary>
        public double MeltReduction { get; private set; }

        /// <summary>
        /// 溶ける量を減らす割合を変える。先に今日まで確定させるので、それまでに過ぎた日は前の割合で溶ける。
        /// 島の状態から決まる値なので、Systems はこれを操作として記録し、再生で同じ氷に戻す。
        /// </summary>
        public bool SetMeltReduction(double reduction)
        {
            Advance();
            var r = Math.Max(0.0, Math.Min(_p.MaxMeltReduction, reduction));
            if (Math.Abs(r - MeltReduction) < 1e-9)
            {
                return false;
            }

            MeltReduction = r;
            return true;
        }

        /// <summary>今日のミッションを選ぶ (1 日 2 件まで)。</summary>
        public bool SelectMission(string missionId, MissionVerification verification)
        {
            var d = Advance();
            if (d == null)
            {
                return false;
            }

            if (!_selected.TryGetValue(d.Value, out var sel))
            {
                sel = new List<KeyValuePair<string, MissionVerification>>();
                _selected[d.Value] = sel;
            }

            if (sel.Count >= _p.SelectionsPerDay || sel.Any(x => x.Key == missionId))
            {
                return false;
            }

            sel.Add(new KeyValuePair<string, MissionVerification>(missionId, verification));
            return true;
        }

        /// <summary>今日選んだミッションの夜の振り返り。答えに関係なく記録になる。同じミッションの 2 回目は false。</summary>
        public bool CheckIn(string missionId, CheckInAnswer answer, bool noShopping = false)
        {
            var d = Advance();
            if (d == null || !_selected.TryGetValue(d.Value, out var sel))
            {
                return false;
            }

            var found = sel.FirstOrDefault(x => x.Key == missionId);
            if (found.Key == null || _checked.Contains((d.Value, missionId)))
            {
                return false;
            }

            _checked.Add((d.Value, missionId));
            if (answer == CheckInAnswer.Done && found.Value != MissionVerification.Receipt)
            {
                WhitePressed++;
                var w = AppCalendar.WeekOf(d.Value);
                if (Get(_whiteByDay, d.Value) < _p.WhitePerDay && Get(_whiteByWeek, w) < _p.WhitePerWeek)
                {
                    _whiteByDay[d.Value] = Get(_whiteByDay, d.Value) + 1;
                    _whiteByWeek[w] = Get(_whiteByWeek, w) + 1;
                    WhiteCounted++;
                }

                MarkDoneMission(missionId);
            }

            Record(d.Value, noShopping ? RecordKind.NoShoppingMission : RecordKind.Mission);
            return true;
        }

        public DaySnapshot Snapshot(int day)
        {
            return new DaySnapshot(day, Ice, Shields, ShardsExact, Shards, Tier, TotalRecordDays, _arrivals.Count, Gold,
                WhiteCounted, StampPoints, ShieldsUsed, _recordedDays.Contains(day));
        }

        private static int Get(Dictionary<int, int> map, int key)
        {
            return map.TryGetValue(key, out var v) ? v : 0;
        }

        private void SetTier(int day)
        {
            var nt = IceTierRule.Next(Tier, Ice, _p);
            if (nt != Tier)
            {
                _tierLog.Add(new TierChange(day, Tier, nt));
                Tier = nt;
            }
        }

        private void AddIce(double amount)
        {
            var next = Ice + amount;
            if (next > _p.Cap)
            {
                ShardsExact += next - _p.Cap;
                next = _p.Cap;
            }

            Ice = next;
        }

        private void StartDay(int d, bool first)
        {
            LastStartedDay = d;
            if (first)
            {
                if (_p.OnboardingExemptFromCap)
                {
                    _arrivals.Add(new PenguinArrival(PenguinCatalog.Onboarding, d, ArrivalReason.Onboarding));
                }
                else
                {
                    Enqueue(PenguinCatalog.Onboarding);
                    TryDeliver(d);
                }
            }

            if (!AppCalendar.IsMonday(d) || first)
            {
                return;
            }

            Shields = _p.ShieldsMax;
            var prev = AppCalendar.WeekOf(d) - 1;
            if (prev >= AppCalendar.WeekOf(FirstDay))
            {
                var rs = _receipts.Where(r => AppCalendar.WeekOf(r.PrintDay) == prev).ToList();
                var b = rs.Count > 0 ? Math.Max(0.0, Math.Min(_p.WeeklyBonusMax, _bonusFn(prev, rs))) : 0.0;
                if (b > 0)
                {
                    AddIce(b);
                    SetTier(d);
                }

                if (Get(_weekRecordDays, prev) >= _p.WeeklyArrivalMinDays)
                {
                    if (_p.WeeklyArrivals)
                    {
                        WeeklyPending++;
                    }

                    ConsecutiveWeeks++;
                    if (ConsecutiveWeeks >= 4)
                    {
                        Enqueue(PenguinCatalog.FourWeeks);
                    }
                }
                else
                {
                    ConsecutiveWeeks = 0;
                }
            }

            TryDeliver(d);
        }

        private void NonRecord(int d)
        {
            GapDays++;
            Streak = 0;
            if (Shields > 0)
            {
                Shields--;
                ShieldsUsed++;
                return;
            }

            var m = Math.Max(0.0, Math.Min(_p.Melt * (1.0 - MeltReduction), Ice - _p.Floor));
            Ice -= m;
            GapMelted += m;
            SetTier(d);
        }

        private void Record(int d, RecordKind kind)
        {
            if (_kindsUsed.Add(kind) && _kindsUsed.Count == 3)
            {
                Enqueue(PenguinCatalog.AllKinds);
            }

            if (_recordedDays.Contains(d))
            {
                TryDeliver(d);
                return;
            }

            _recordedDays.Add(d);
            var gain = _p.RecordGain * (Ice < _p.RescueBelow ? _p.RescueMultiplier : 1.0);
            var fires = _p.ComebackRequiresMelt ? GapMelted > 0 : GapDays >= _p.ComebackMinGap;
            if (fires)
            {
                var extra = Math.Min(_p.ComebackMax, AppCalendar.RoundHalfUp(_p.ComebackRatio * GapMelted));
                gain += extra;
                Comebacks++;
                _comebackLog.Add(new KeyValuePair<int, int>(d, extra));
                if (Comebacks == 1)
                {
                    Enqueue(PenguinCatalog.FirstComeback);
                }
            }

            GapDays = 0;
            GapMelted = 0.0;
            AddIce(gain);
            SetTier(d);
            Streak++;
            TotalRecordDays++;
            var w = AppCalendar.WeekOf(d);
            _weekRecordDays[w] = Get(_weekRecordDays, w) + 1;
            if (TotalRecordDays == 1)
            {
                Enqueue(PenguinCatalog.FirstRecord);
            }

            if (TotalRecordDays == 14)
            {
                Enqueue(PenguinCatalog.Total14);
            }

            if (TotalRecordDays == 30)
            {
                Enqueue(PenguinCatalog.Total30);
            }

            TryDeliver(d);
        }

        private bool Has(string species)
        {
            return _arrivals.Any(a => a.Species == species);
        }

        private void Enqueue(string species)
        {
            if (_milestoneQueue.Contains(species) || Has(species))
            {
                return;
            }

            _milestoneQueue.Add(species);
        }

        private string NextWeekly()
        {
            while (_weeklyIndex < PenguinCatalog.WeeklyOrder.Count)
            {
                var sp = PenguinCatalog.WeeklyOrder[_weeklyIndex];
                _weeklyIndex++;
                if (!Has(sp) && !_milestoneQueue.Contains(sp))
                {
                    return sp;
                }
            }

            foreach (var sp in PenguinCatalog.MilestoneOrder)
            {
                if (!Has(sp) && !_milestoneQueue.Contains(sp))
                {
                    return sp;
                }
            }

            return null;
        }

        private void TryDeliver(int d)
        {
            var w = AppCalendar.WeekOf(d);
            if (_lastArrivalWeek == w)
            {
                return;
            }

            if (_milestoneQueue.Count > 0)
            {
                var sp = _milestoneQueue[0];
                _milestoneQueue.RemoveAt(0);
                _arrivals.Add(new PenguinArrival(sp, d, ArrivalReason.Milestone));
                _lastArrivalWeek = w;
            }
            else if (WeeklyPending > 0)
            {
                var sp = NextWeekly();
                WeeklyPending--;
                if (sp != null)
                {
                    _arrivals.Add(new PenguinArrival(sp, d, ArrivalReason.Weekly));
                    _lastArrivalWeek = w;
                }
            }
        }

        private void MarkDoneMission(string missionId)
        {
            if (_doneMissions.Add(missionId) && _doneMissions.Count == 5)
            {
                Enqueue(PenguinCatalog.FiveMissions);
            }
        }
    }
}
