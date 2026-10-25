using System;
using System.Linq;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Factory;
using ProjectPenguin.Systems;

namespace ProjectPenguin.FactoryGame
{
    /// <summary>
    /// 工場のすれ違いの段取り: BLE で見た相手を工場の台帳 (<see cref="FactorySession"/>) に入れ、報告を 1 件ずつ backend に送り、
    /// 両方の報告がそろったら成立にしてペンギンを迎える。台帳と所持は工場の保存に入るので、ここは通信だけを持つ。
    /// 時刻と日は Driver から受け取る。トークンは画面にもログにも出さない。
    /// </summary>
    internal sealed class FactoryGameEncounters
    {
        private readonly FactorySession _session;
        private readonly IEncounterRadio _radio;
        private readonly Func<long> _clock;
        private readonly Action<FactoryResult> _apply;
        private readonly Action<string> _notify;
        private IEncounterReporter _reporter;
        private bool _usesBackend, _reporting, _running;
        private string _problem;

        /// <param name="clock">送り直しの間隔を測る時計 (ミリ秒、セッションの中で戻らないもの)。</param>
        internal FactoryGameEncounters(FactorySession session, IEncounterRadio radio, Func<long> clock, Action<FactoryResult> apply, Action<string> notify)
        {
            _session = session;
            _radio = radio;
            _clock = clock;
            _apply = apply;
            _notify = notify;
            _ = LoadReporterAsync();
        }

        internal string Status
        {
            get
            {
                var s = _session.Snapshot;
                var today = s.peers.Count(p => !p.dropped);
                var waiting = s.peers.Count(p => !p.matched && !p.dropped);
                var line = $"すれ違い：{RadioLabel(_radio.State)} · 今日 {today}人 · 確認待ち {waiting} · 獲得 {s.dailyBirds}/{_session.Catalog.dailyBirds}羽";
                if (_reporter != null && !_usesBackend) line += " · 端末内で確認";
                return _problem != null ? line + "\n" + _problem : line;
            }
        }

        /// <summary>毎フレーム。日が替わったらトークンを作り直し、広告するトークンも差し替える。</summary>
        internal void Poll(int appDay)
        {
            var nowMs = _clock();
            var s = _session.Snapshot;
            if (s.encounterDay < appDay)
            {
                var begin = _session.BeginEncounterDay(appDay, EncounterToken.Create());
                if (!begin.Success) { _problem = begin.Message; return; }
                _running = false;
            }

            if (!_running)
            {
                _radio.Start(_session.Snapshot.selfToken);
                _running = true;
            }

            while (_radio.TryTakePeer(out var peer))
            {
                // 同じ相手・上限は台帳が断る。断られたものは知らせない (近くにいる間は何度も届くため)。
                _session.ObservePeer(peer, nowMs);
            }

            if (_reporter == null || _reporting) return;
            var next = _session.Snapshot.peers.FirstOrDefault(p => !p.matched && !p.dropped && p.retryAt <= nowMs);
            if (next != null) _ = ReportAsync(appDay, next.token);
        }

        internal void Suspend()
        {
            _radio.Stop();
            _running = false;
        }

        /// <summary>
        /// 開発用。合成の相手のトークンを作り、相手の側の報告を先に backend へ送ってから、自分が BLE で見つけたことにする。
        /// 自分の報告で成立する。decisions.md の「自作自演の穴」をデモに使う形で、Editor と Development Build だけで呼ぶ。
        /// </summary>
        internal async Task FakeEncounterAsync(int appDay)
        {
            var self = _session.Snapshot.selfToken;
            if (_reporter == null || _session.Snapshot.encounterDay != appDay || !EncounterToken.IsValid(self))
            {
                _notify("すれ違いの準備がまだです。少し待ってから押してください");
                return;
            }

            var peer = EncounterToken.Create();
            try
            {
                await _reporter.ReportAsync(EncounterLedger.ContractDay(appDay), peer, self, default);
            }
            catch (BackendException e)
            {
                _notify("合成の相手の報告に失敗：" + e.UserMessage);
                return;
            }

            if (_radio is FakeEncounterRadio fake) fake.Inject(peer);
            else _session.ObservePeer(peer, _clock());
            _notify("合成の相手を報告しました。自分の報告がそろうと成立します");
        }

        private async Task LoadReporterAsync()
        {
            try
            {
                var services = await BackendServices.SharedAsync(null);
                _usesBackend = services.Config.EncountersEnabled;
                _reporter = services.Encounters;
            }
            catch (Exception)
            {
                _usesBackend = false;
                _reporter = new MockEncounterReporter();
            }
        }

        private async Task ReportAsync(int appDay, string peer)
        {
            _reporting = true;
            var self = _session.Snapshot.selfToken;
            try
            {
                var reply = await _reporter.ReportAsync(EncounterLedger.ContractDay(appDay), self, peer, default);
                _problem = null;
                var result = _session.ConfirmEncounter(appDay, peer, reply.PairId, reply.Matched, _clock());
                if (reply.Matched) _apply(result);
            }
            catch (BackendException e) when (!e.Retryable)
            {
                _session.DropPeer(peer);
                _problem = "受け付けられなかった相手を除きました";
            }
            catch (BackendException e)
            {
                _session.DeferPeer(peer, checked(_clock() + Math.Max(EncounterLedger.RetryMs, e.RetryAfterSeconds * 1000L)));
                _problem = "確認できませんでした。30秒後に送り直します";
            }
            finally
            {
                _reporting = false;
            }
        }

        private static string RadioLabel(EncounterRadioState state) => state switch
        {
            EncounterRadioState.Starting => "準備中",
            EncounterRadioState.Running => "探しています",
            EncounterRadioState.Unauthorized => "Bluetooth が許可されていません",
            EncounterRadioState.PoweredOff => "Bluetooth がオフです",
            EncounterRadioState.Unsupported => "この端末は非対応",
            _ => "止まっています",
        };
    }
}
