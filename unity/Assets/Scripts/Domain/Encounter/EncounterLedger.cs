using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProjectPenguin.Domain.Encounter
{
    public enum EncounterObservation
    {
        Accepted,
        /// <summary>トークンの形が違う。</summary>
        Invalid,
        /// <summary>自分のトークン。</summary>
        Self,
        /// <summary>今日もう見た相手。</summary>
        Duplicate,
        /// <summary>今日見た相手が上限に達した。</summary>
        Full,
        /// <summary>まだ日を始めていない。</summary>
        NoDay,
    }

    public enum EncounterOutcome
    {
        /// <summary>相手がまだ自分を報告していない。あとで送り直す。</summary>
        Waiting,
        /// <summary>成立し、ペンギンを 1 羽獲得した。</summary>
        Granted,
        /// <summary>成立したが、今日の獲得の上限に達していた。</summary>
        CapReached,
        /// <summary>今日の台帳に無い相手の結果、または pair_id が手元の計算と合わない。</summary>
        Ignored,
    }

    public sealed class EncounterReport
    {
        internal EncounterReport(string day, string selfToken, string peerToken)
        {
            Day = day;
            SelfToken = selfToken;
            PeerToken = peerToken;
        }

        /// <summary>契約の day (アプリの 1 日、"yyyy-MM-dd")。</summary>
        public string Day { get; }

        public string SelfToken { get; }

        public string PeerToken { get; }
    }

    public sealed class EncounterResultView
    {
        internal EncounterResultView(EncounterOutcome outcome, EncounterSpecies species)
        {
            Outcome = outcome;
            Species = species;
        }

        public EncounterOutcome Outcome { get; }

        /// <summary><see cref="EncounterOutcome.Granted"/> のときに獲得した種類。</summary>
        public EncounterSpecies Species { get; }
    }

    /// <summary><see cref="EncounterLedger"/> の保存の形。トークンは端末の保存にだけ入れ、画面にもログにも出さない。</summary>
    [Serializable]
    public sealed class EncounterLedgerState
    {
        public int Day = -1;
        public string SelfToken;
        public int GrantedToday;
        public List<EncounterPeerState> Peers = new List<EncounterPeerState>();
    }

    [Serializable]
    public sealed class EncounterPeerState
    {
        public string Token;
        /// <summary>次に報告してよい時刻 (ミリ秒)。保存して読み直すなら、端末の UTC のように再起動で戻らない時計を渡す。</summary>
        public long NextReportAtMs;
        public bool Matched;
        public bool Dropped;
    }

    /// <summary>
    /// すれ違いの台帳。アプリの 1 日 (AppCalendar.AppDay、03:00 JST 区切り。契約の day と同じ) ごとに、
    /// 自分のトークン・見た相手・確認待ち・成立した組・獲得した数を持つ。時刻は外から渡す (端末の時計を読まない)。
    /// 健康の工場の試作はメモリにだけ持つ (再起動するとその日の数も戻る、unity/docs/health-factory-prototype.md)。
    /// 村のホームは <see cref="Export"/> と <see cref="Restore"/> で村の保存に入れる。
    /// </summary>
    public sealed class EncounterLedger
    {
        /// <summary>1 日に見る相手の上限。報告の数 (backend の費用) を抑える。</summary>
        public const int MaxPeersPerDay = 50;

        /// <summary>1 日に獲得するペンギンの上限 (penguin-effects-proposal.md の仮置き)。</summary>
        public const int PenguinsPerDay = 10;

        /// <summary>確認待ちの報告を送り直す間隔。</summary>
        public const long RetryMs = 30000;

        private sealed class Peer
        {
            public string Token;
            public long NextReportAtMs;
            public bool Matched;
            /// <summary>backend に受け付けられなかった (400)。送り直さず、今日はもう入れない。</summary>
            public bool Dropped;
        }

        private readonly Func<string> _newToken;
        private readonly List<Peer> _peers = new List<Peer>();

        public EncounterLedger(Func<string> newToken = null)
        {
            _newToken = newToken ?? EncounterToken.Create;
        }

        /// <summary>今の日番号。まだ始めていなければ -1。</summary>
        public int Day { get; private set; } = -1;

        public string SelfToken { get; private set; }

        public int Seen => _peers.Count;

        public int Waiting => Count(p => !p.Matched && !p.Dropped);

        public int Matched => Count(p => p.Matched);

        public int GrantedToday { get; private set; }

        /// <summary>契約の day の形 ("yyyy-MM-dd")。</summary>
        public static string ContractDay(int appDay) =>
            Clock.JstCalendar.DateOfDay(appDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>
        /// 日を始める。日が替わったら、トークンを作り直して台帳を空にし true を返す。
        /// 同じ日や、時計が戻って前の日になったときは何もしない (前の日のトークンには戻さない)。
        /// </summary>
        public bool BeginDay(int appDay)
        {
            if (appDay <= Day)
            {
                return false;
            }

            Day = appDay;
            SelfToken = _newToken();
            if (!EncounterToken.IsValid(SelfToken))
            {
                throw new InvalidOperationException("作ったトークンの形が違う");
            }

            _peers.Clear();
            GrantedToday = 0;
            return true;
        }

        /// <summary>BLE で見た相手のトークンを入れる。入れたら、すぐ報告の候補になる。</summary>
        public EncounterObservation Observe(string peerToken, long nowMs)
        {
            var check = Check(peerToken);
            if (check == EncounterObservation.Accepted)
            {
                _peers.Add(new Peer { Token = peerToken, NextReportAtMs = nowMs });
            }

            return check;
        }

        /// <summary><see cref="Observe"/> が受け付けるかを、台帳を変えずに確かめる。</summary>
        public EncounterObservation Check(string peerToken)
        {
            if (Day < 0)
            {
                return EncounterObservation.NoDay;
            }

            if (!EncounterToken.IsValid(peerToken))
            {
                return EncounterObservation.Invalid;
            }

            if (peerToken == SelfToken)
            {
                return EncounterObservation.Self;
            }

            if (FindPeer(peerToken) != null)
            {
                return EncounterObservation.Duplicate;
            }

            if (_peers.Count >= MaxPeersPerDay)
            {
                return EncounterObservation.Full;
            }

            return EncounterObservation.Accepted;
        }

        /// <summary>今送る報告があるかを、台帳を変えずに確かめる (<see cref="NextReport"/> の前に軽く調べるため)。</summary>
        public bool HasReportDue(long nowMs)
        {
            foreach (var p in _peers)
            {
                if (!p.Matched && !p.Dropped && p.NextReportAtMs <= nowMs)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 今送る報告を 1 件返す。無ければ null。返した相手は、結果が来るまで (<see cref="RetryMs"/> の間) 選ばない。
        /// </summary>
        public EncounterReport NextReport(long nowMs)
        {
            foreach (var p in _peers)
            {
                if (!p.Matched && !p.Dropped && p.NextReportAtMs <= nowMs)
                {
                    p.NextReportAtMs = nowMs + RetryMs;
                    return new EncounterReport(ContractDay(Day), SelfToken, p.Token);
                }
            }

            return null;
        }

        /// <summary>報告が失敗したとき (通信・429・503)。<paramref name="retryAfterMs"/> が長ければそれだけ待つ。</summary>
        public void ReportFailed(EncounterReport report, long nowMs, long retryAfterMs = 0)
        {
            var p = Current(report);
            if (p != null && !p.Matched)
            {
                p.NextReportAtMs = nowMs + Math.Max(RetryMs, retryAfterMs);
            }
        }

        /// <summary>送り直しても通らない失敗 (400 の token_invalid・day_invalid)。その相手は今日はもう報告しない。</summary>
        public void Discard(EncounterReport report)
        {
            var p = Current(report);
            if (p != null && !p.Matched)
            {
                p.Dropped = true;
            }
        }

        /// <summary>報告の結果を入れる。成立して上限に余りがあれば、pair_id を種に 1 羽獲得する。</summary>
        public EncounterResultView ApplyResult(EncounterReport report, bool matched, string pairId, long nowMs)
        {
            var p = Current(report);
            if (p == null || p.Matched || p.Dropped || pairId != EncounterToken.PairId(SelfToken, p.Token))
            {
                return new EncounterResultView(EncounterOutcome.Ignored, null);
            }

            if (!matched)
            {
                p.NextReportAtMs = nowMs + RetryMs;
                return new EncounterResultView(EncounterOutcome.Waiting, null);
            }

            p.Matched = true;
            if (GrantedToday >= PenguinsPerDay)
            {
                return new EncounterResultView(EncounterOutcome.CapReached, null);
            }

            GrantedToday++;
            return new EncounterResultView(EncounterOutcome.Granted, EncounterPenguins.Draw(pairId));
        }

        /// <summary>保存の形に写す (台帳とは切り離した写し)。</summary>
        public EncounterLedgerState Export()
        {
            var state = new EncounterLedgerState { Day = Day, SelfToken = SelfToken, GrantedToday = GrantedToday };
            foreach (var p in _peers)
            {
                state.Peers.Add(new EncounterPeerState { Token = p.Token, NextReportAtMs = p.NextReportAtMs, Matched = p.Matched, Dropped = p.Dropped });
            }

            return state;
        }

        /// <summary>保存から戻す。null (この項目が無い古い保存) なら空の台帳。形が違えば <see cref="ArgumentException"/>。</summary>
        public static EncounterLedger Restore(EncounterLedgerState state, Func<string> newToken = null)
        {
            var ledger = new EncounterLedger(newToken);
            if (state == null || state.Day < 0)
            {
                return ledger;
            }

            if (!EncounterToken.IsValid(state.SelfToken) || state.GrantedToday < 0 || state.GrantedToday > PenguinsPerDay
                || state.Peers == null || state.Peers.Count > MaxPeersPerDay)
            {
                throw new ArgumentException("すれ違いの台帳が不正です");
            }

            ledger.Day = state.Day;
            ledger.SelfToken = state.SelfToken;
            ledger.GrantedToday = state.GrantedToday;
            foreach (var p in state.Peers)
            {
                if (p == null || !EncounterToken.IsValid(p.Token) || p.Token == state.SelfToken || ledger.FindPeer(p.Token) != null
                    || p.NextReportAtMs < 0 || p.Matched && p.Dropped)
                {
                    throw new ArgumentException("すれ違いの相手の記録が不正です");
                }

                ledger._peers.Add(new Peer { Token = p.Token, NextReportAtMs = p.NextReportAtMs, Matched = p.Matched, Dropped = p.Dropped });
            }

            return ledger;
        }

        /// <summary>今日の台帳の、その報告の相手。日が替わった後に届いた前の日の結果は null。</summary>
        private Peer Current(EncounterReport report)
        {
            if (report == null || report.SelfToken != SelfToken || report.Day != ContractDay(Day))
            {
                return null;
            }

            return FindPeer(report.PeerToken);
        }

        private int Count(Func<Peer, bool> match)
        {
            var n = 0;
            foreach (var p in _peers)
            {
                if (match(p))
                {
                    n++;
                }
            }

            return n;
        }

        private Peer FindPeer(string token)
        {
            foreach (var p in _peers)
            {
                if (p.Token == token)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
