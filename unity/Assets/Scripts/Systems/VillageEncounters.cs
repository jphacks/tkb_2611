using System;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Game;
using UnityEngine;

namespace ProjectPenguin.Systems
{
    /// <summary>
    /// 村のすれ違いの段取り: BLE で見た相手を村の台帳に入れ、報告を 1 件ずつ backend (<c>POST /v1/encounters</c>) に送り、
    /// 両方の報告がそろったら成立にして資源を受け取る。台帳と受け取りは村の保存に入る (<see cref="ProductionVillageSessionCore"/>) ので、
    /// ここは通信だけを持つ (工場ゲームの <c>FactoryGameEncounters</c> と同じ分け方)。
    /// アプリが前面にあるときだけ動かす。トークンは画面にもログにも出さない。
    /// </summary>
    [RequireComponent(typeof(ProductionVillageSession))]
    public sealed class VillageEncounters : MonoBehaviour
    {
        private ProductionVillageSession _session;
        private IEncounterRadio _radio;
        private IEncounterReporter _reporter;
        private bool _usesBackend, _reporting, _running;
        private string _problem;

        /// <summary>成立して資源を受け取ったとき (画面のトーストに出す文)。</summary>
        public event Action<string> Granted;

        /// <summary>開発用の「合成の相手」を使えるか (Editor と Development Build)。</summary>
        public bool CanFakeEncounter => Application.isEditor || Debug.isDebugBuild;

        public string Status
        {
            get
            {
                if (_radio == null || _session == null || _session.Core == null)
                {
                    return "すれ違いの準備をしています";
                }

                var (seen, waiting, granted) = _session.Core.Encounters;
                var line = $"{RadioLabel(_radio.State)} · 今日 {seen}人 · 確認待ち {waiting} · 受け取り {granted}/{EncounterLedger.PenguinsPerDay}回";
                if (_reporter != null && !_usesBackend)
                {
                    line += " · 端末内で確認";
                }

                return _problem != null ? line + "\n" + _problem : line;
            }
        }

        private void Awake()
        {
            _session = GetComponent<ProductionVillageSession>();
            _radio = EncounterRadios.Create();
            _ = LoadReporterAsync();
        }

        private void Update()
        {
            var core = _session.Core;
            // View は毎回村の写しを作るので見ない。村を読めていなければ、下のコマンドがすぐ失敗する。
            if (core == null || _session.IsSuspended)
            {
                return;
            }

            var now = ProductionVillageSession.UtcNow;
            var appDay = AppCalendar.AppDay(DateTimeOffset.UtcNow);
            if (core.EncounterDay < appDay)
            {
                // 日が替わったらトークンを作り直す。広告するトークンも差し替える。
                if (!core.BeginEncounterDay(appDay, now))
                {
                    return;
                }

                _running = false;
            }

            if (!_running && EncounterToken.IsValid(core.EncounterSelfToken))
            {
                _radio.Start(core.EncounterSelfToken);
                _running = true;
            }

            while (_radio.TryTakePeer(out var peer))
            {
                // 同じ相手・上限は台帳が断る (近くにいる間は何度も届くため、知らせない)。
                core.ObservePeer(peer, now);
            }

            if (_reporter == null || _reporting)
            {
                return;
            }

            var report = core.NextEncounterReport(now);
            if (report != null)
            {
                _ = ReportAsync(report);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Suspend();
            }
        }

        private void OnDisable() => Suspend();

        private void Suspend()
        {
            _radio?.Stop();
            _running = false;
        }

        /// <summary>
        /// 開発用。合成の相手のトークンを作り、相手の側の報告を先に backend へ送ってから、自分が BLE で見つけたことにする。
        /// 自分の報告で成立する。decisions.md の「自作自演の穴」をデモに使う形で、Editor と Development Build だけで呼ぶ。
        /// </summary>
        public async Task<string> FakeEncounterAsync()
        {
            var core = _session.Core;
            var self = core.EncounterSelfToken;
            if (!CanFakeEncounter || _reporter == null || !EncounterToken.IsValid(self))
            {
                return "すれ違いの準備がまだだよ。少し待ってから押してね";
            }

            var peer = EncounterToken.Create();
            try
            {
                await _reporter.ReportAsync(EncounterLedger.ContractDay(core.EncounterDay), peer, self, default);
            }
            catch (BackendException e)
            {
                return "合成の相手の報告に失敗：" + e.UserMessage;
            }

            if (this == null)
            {
                return "";
            }

            if (_radio is FakeEncounterRadio fake)
            {
                fake.Inject(peer);
            }
            else
            {
                core.ObservePeer(peer, ProductionVillageSession.UtcNow);
            }

            return "合成の相手を報告したよ。自分の報告がそろうと成立するよ";
        }

        private async Task LoadReporterAsync()
        {
            try
            {
                var services = await BackendServices.SharedAsync(null);
                _usesBackend = services.Config.EncountersEnabled;
                _reporter = services.Encounters;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(VillageEncounters)}: すれ違いの設定を読めなかったので、端末の中で確かめます ({e.GetType().Name})");
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
                if (this == null)
                {
                    return;
                }

                _problem = null;
                var outcome = _session.Core.ApplyEncounter(report, reply.Matched, reply.PairId, ProductionVillageSession.UtcNow);
                if (outcome == EncounterOutcome.Granted || outcome == EncounterOutcome.CapReached)
                {
                    Granted?.Invoke(_session.Core.Notice);
                }
            }
            catch (BackendException e) when (!e.Retryable && this != null)
            {
                _session.Core.DiscardEncounter(report, ProductionVillageSession.UtcNow);
                _problem = "受け付けられなかった相手を除いたよ";
            }
            catch (BackendException e) when (this != null)
            {
                _session.Core.EncounterFailed(report, e.RetryAfterSeconds * 1000L, ProductionVillageSession.UtcNow);
                _problem = "確かめられなかったよ。30秒後に送り直すね";
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
