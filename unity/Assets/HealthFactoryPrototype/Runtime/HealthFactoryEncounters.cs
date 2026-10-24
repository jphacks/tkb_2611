using System;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Systems;

namespace ProjectPenguin.HealthFactoryPrototype
{
    /// <summary>
    /// すれ違いの段取り: BLE で見た相手を台帳に入れ、報告を 1 件ずつ backend に送り、成立したら工場にペンギンを渡す。
    /// 時刻と日は Driver から受け取る。ペンギンの効果の計算は HealthFactoryFacade が持つ。トークンは画面にもログにも出さない。
    /// </summary>
    internal sealed class HealthFactoryEncounters
    {
        private readonly IEncounterRadio _radio;
        private readonly EncounterLedger _ledger = new EncounterLedger();
        private readonly Action<string> _grant;
        private readonly Action<string> _notify;
        private readonly Func<long> _clock;
        private IEncounterReporter _reporter;
        private bool _usesBackend, _reporting, _running;
        private string _lastSpecies, _lastProblem;

        /// <param name="clock">送り直しの間隔を測る時計 (ミリ秒、セッションの中で戻らないもの)。</param>
        internal HealthFactoryEncounters(IEncounterRadio radio, Func<long> clock, Action<string> grant, Action<string> notify)
        {
            _radio = radio;
            _clock = clock;
            _grant = grant;
            _notify = notify;
            _ = LoadReporterAsync();
        }

        internal string Status
        {
            get
            {
                var line = $"すれ違い：{RadioLabel(_radio.State)} · 今日 {_ledger.Seen}人 · 確認待ち {_ledger.Waiting} · 獲得 {_ledger.GrantedToday}/{EncounterLedger.PenguinsPerDay}羽";
                if (_reporter != null && !_usesBackend)
                {
                    line += " · 端末内で確認";
                }

                return line + (_lastProblem != null ? $"\n{_lastProblem}" : _lastSpecies != null ? $"\n最後に出会った：{_lastSpecies}" : "");
            }
        }

        /// <summary>毎フレーム。日が替わったらトークンを作り直し、広告するトークンも差し替える。</summary>
        internal void Poll(int appDay)
        {
            var nowMs = _clock();
            if (_ledger.BeginDay(appDay) || !_running)
            {
                _radio.Start(_ledger.SelfToken);
                _running = true;
            }

            while (_radio.TryTakePeer(out var peer))
            {
                _ledger.Observe(peer, nowMs);
            }

            if (_reporter == null || _reporting)
            {
                return;
            }

            var report = _ledger.NextReport(nowMs);
            if (report != null)
            {
                _ = ReportAsync(report);
            }
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
        internal async Task FakeEncounterAsync()
        {
            if (_reporter == null || _ledger.Day < 0)
            {
                _notify("すれ違いの準備がまだです。少し待ってから押してください");
                return;
            }

            var peer = EncounterToken.Create();
            try
            {
                await _reporter.ReportAsync(EncounterLedger.ContractDay(_ledger.Day), peer, _ledger.SelfToken, default);
            }
            catch (BackendException e)
            {
                _notify("合成の相手の報告に失敗：" + e.UserMessage);
                return;
            }

            if (_radio is FakeEncounterRadio fake)
            {
                fake.Inject(peer);
            }
            else
            {
                _ledger.Observe(peer, _clock());
            }

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

        private async Task ReportAsync(EncounterReport report)
        {
            _reporting = true;
            try
            {
                var reply = await _reporter.ReportAsync(report.Day, report.SelfToken, report.PeerToken, default);
                var result = _ledger.ApplyResult(report, reply.Matched, reply.PairId, _clock());
                _lastProblem = null;
                if (result.Outcome == EncounterOutcome.Granted)
                {
                    _lastSpecies = result.Species.Name;
                    _grant(result.Species.Id);
                }
                else if (result.Outcome == EncounterOutcome.CapReached)
                {
                    _notify($"すれ違いは成立。今日の獲得は {EncounterLedger.PenguinsPerDay}羽 までです");
                }
            }
            catch (BackendException e) when (!e.Retryable)
            {
                _ledger.Discard(report);
                _lastProblem = "受け付けられなかった相手を除きました";
            }
            catch (BackendException e)
            {
                _ledger.ReportFailed(report, _clock(), e.RetryAfterSeconds * 1000L);
                _lastProblem = "確認できませんでした。30秒後に送り直します";
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
