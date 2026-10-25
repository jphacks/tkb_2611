using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using ProjectPenguin.Backend;
using ProjectPenguin.Data;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.World;
using ProjectPenguin.Systems.Timekeeping;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectPenguin.Systems
{
    /// <summary>操作の前後のホームの状態。</summary>
    public readonly struct GameStateChange
    {
        public GameStateChange(HomeState before, HomeState after, bool isComeback, bool isFirstOpenToday)
        {
            Before = before;
            After = after;
            IsComeback = isComeback;
            IsFirstOpenToday = isFirstOpenToday;
        }

        public HomeState Before { get; }
        public HomeState After { get; }

        /// <summary>前回開いてから 3 日以上あいた。</summary>
        public bool IsComeback { get; }

        /// <summary>この操作がその日に初めて開いたとき。</summary>
        public bool IsFirstOpenToday { get; }

        public SpeechEvent Events => HomeState.EventsBetween(Before, After);
    }

    /// <summary>写真 1 枚を受け取った結果。</summary>
    public sealed class EcoPhotoResult
    {
        public EcoPhotoResult(EcoPhotoVerdict verdict, bool newRecordDay, ResourceBundle granted, HomeState before, HomeState after)
        {
            Verdict = verdict;
            NewRecordDay = newRecordDay;
            Granted = granted;
            Before = before;
            After = after;
        }

        public EcoPhotoVerdict Verdict { get; }

        /// <summary>この写真で今日が記録日になった (氷が増えた)。</summary>
        public bool NewRecordDay { get; }

        /// <summary>受け取った資源。今日すでに受け取っていれば null。</summary>
        public ResourceBundle Granted { get; }

        public HomeState Before { get; }
        public HomeState After { get; }
    }

    /// <summary>
    /// ゲームの状態を持つ。氷の状態機械 (<see cref="GameSession"/>) と生産の島 (<see cref="IslandGame"/>) を、
    /// 起動・復帰・操作のたびに進め、変化を知らせて端末に保存する。
    /// 計算は Domain が行い、ここは時計を渡して呼ぶだけ (.claude/rules/unity.md)。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameStateSystem : MonoBehaviour, IServerResponseObserver
    {
        /// <summary>この日数以上あけて開いたら「おかえり」(15c §2.2)。</summary>
        public const int ComebackDays = 3;

        /// <summary>島の変化を画面に知らせる間隔 (秒)。</summary>
        private const float IslandNotifyInterval = 0.2f;

        /// <summary>動いている間も、この間隔で保存する (突然終了しても進みを失わないように)。</summary>
        private const float AutosaveInterval = 20f;

        /// <summary>記録画面に残す受け取りの記録の数。</summary>
        private const int ActivityLimit = GameSave.ActivityWindow;

        /// <summary>開いている間に歩数を問い合わせ直す間隔 (秒)。歩いた分のガスが、開いたままでも増えるように。</summary>
        private const float StepPollInterval = 30f;

        /// <summary>時刻合わせの通信の待ち時間 (秒)。UnityWebRequest の既定 0 は無制限なので必ず付ける。</summary>
        private const int TimeSyncTimeoutSeconds = 25;

        [Tooltip("保存しない (確認用のシーン・テスト)")]
        [SerializeField] private bool _inMemoryOnly;

        [Tooltip("時刻合わせに使う backend の URL (GET。応答の Date ヘッダーだけを見る)。空なら penguin-config.json の backend の /v1/health を使い、それも無ければ通信しない。")]
        [SerializeField] private string _timeSyncUrl = "";

        private SessionClock _clock;
        private ServerTrustedClock _serverClock;
        private IMonotonicClock _monotonic;
        private bool _syncing;
        private GameSession _session;
        private IslandGame _island;
        private IGameStore _store;
        private IStepSource _steps;
        private GameSave _save;
        private readonly List<GameOp> _ops = new List<GameOp>();
        private readonly List<ActivityEntry> _activity = new List<ActivityEntry>();
        private int _lastOpenDay;
        private float _islandNotifyTimer;
        private float _autosaveTimer;
        private float _stepPollTimer;

        /// <summary>氷の状態が変わりうる操作のたびに呼ばれる (変化が無くても呼ぶ)。</summary>
        public event Action<GameStateChange> Changed;

        /// <summary>島の状態が変わった (生産・操作)。毎フレームではなく間引いて呼ぶ。</summary>
        public event Action IslandChanged;

        /// <summary>島の出来事 (建てた・作った・沈んだ…)。演出に使う。</summary>
        public event Action<IReadOnlyList<IslandEvent>> IslandEvents;

        /// <summary>資源を受け取った (写真・歩数・ミッション)。画面に飛んでくる演出に使う。</summary>
        public event Action<ResourceBundle, string> ResourcesReceived;

        public GameSession Session
        {
            get
            {
                EnsureCreated();
                return _session;
            }
        }

        public IslandGame Island
        {
            get
            {
                EnsureCreated();
                return _island;
            }
        }

        public HomeState Current => HomeState.Of(Session);

        /// <summary>今の時刻。開発ビルドで「時間を進める」を押した分を含む。</summary>
        public DateTimeOffset Now
        {
            get
            {
                EnsureCreated();
                return _clock.UtcNow;
            }
        }

        public int Today => Session.Today;

        /// <summary>
        /// 操作が数えられる日。時計が最後に見た日より前に戻っていれば最後に見た日 (<see cref="GameSession"/> の巻き戻しの扱いと同じ)。
        /// 写真・歩数の資源の「1 日 1 回」はこの日で数えるので、時計を前後させても何度も受け取れない。
        /// </summary>
        public int EffectiveToday => Math.Max(Session.Today, Session.LastStartedDay);

        /// <summary>「時間を進める」で進めた日数。0 でなければ画面に「デモ用」と出す。</summary>
        public int DemoDaysAdvanced => _clock == null ? 0 : (int)_clock.Offset.TotalDays;

        public string PlayerName => _save?.playerName ?? "";

        public bool Onboarded => _save != null && _save.onboarded;

        /// <summary>受け取りの記録 (新しいものが後ろ)。</summary>
        public IReadOnlyList<ActivityEntry> Activity => _activity;

        public bool StepsSupported => _steps != null && _steps.IsSupported;

        /// <summary>開発用の歩数 (Editor と iOS 以外)。</summary>
        public FakeStepSource FakeSteps => _steps as FakeStepSource;

        /// <summary>最後に届いた今日の歩数。まだ届いていなければ -1。</summary>
        public int StepsToday { get; private set; } = -1;

        // 他のコンポーネントの OnEnable が先に呼ばれても使えるよう、最初に触られたときに作る。
        private void EnsureCreated()
        {
            if (_session != null)
            {
                return;
            }

            // 端末の時計をサーバーの Date ヘッダーで確かめる時計で包む。基準を受け取るまでは端末の時計のまま (IsTrusted = false)。
            _monotonic = MonotonicClocks.CreateDefault();
            _serverClock = new ServerTrustedClock(new SystemClock(), _monotonic);
            Initialize(_inMemoryOnly ? new MemoryGameStore() : new SqliteGameStore(), _serverClock, StepSources.Create());
        }

        /// <summary>
        /// 置き場・端末の時計・歩数を差し込んで始める。ふだんは最初に触られたときに自動で呼ばれる。
        /// テストでは固定の時計とメモリの置き場を渡し、保存から同じ状態に戻ることを確かめる。
        /// </summary>
        public void Initialize(IGameStore store, Domain.Clock.IClock deviceClock, IStepSource steps = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _steps = steps ?? new FakeStepSource();
            _clock = new SessionClock(deviceClock ?? new SystemClock());
            LoadOrCreate(_store.Load());
            LoadStatus = _store.LastLoadStatus;
        }

        /// <summary>起動時に保存をどう読めたか。バックアップから戻した・読めなかった・新しい版の保存、を画面で知らせるのに使う。</summary>
        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.NotFound;

        private void LoadOrCreate(GameSave save)
        {
            _ops.Clear();
            _activity.Clear();
            if (save == null)
            {
                var now = _clock.UtcNow;
                _save = new GameSave { createdUnixMs = now.ToUnixTimeMilliseconds() };
                _session = new GameSession(_clock, NoWeeklyBonus, GameParameters.App);
                _island = new IslandGame(IslandParameters.Default, now);

                // 起動した日を「その日に初めて開いた」として挨拶させるため、前日に開いたことにしておく。
                _lastOpenDay = _session.Today - 1;
            }
            else
            {
                _save = save;
                _clock.ResetOffset();
                if (save.devOffsetMs > 0)
                {
                    _clock.Advance(TimeSpan.FromMilliseconds(save.devOffsetMs));
                }

                _session = Replay(save);
                _island = IslandGame.FromSave(save.island, IslandParameters.Default, _clock.UtcNow);
                _lastOpenDay = save.lastOpenDay;
                _activity.AddRange(save.activity ?? Array.Empty<ActivityEntry>());
            }

            SyncIsland();
            _island.Tick(_clock.UtcNow);
        }

        /// <summary>保存した操作を、その時刻に止めた時計で順に再生する。氷の計算は決定的なので同じ状態に戻る。</summary>
        private GameSession Replay(GameSave save)
        {
            _clock.Pin(DateTimeOffset.FromUnixTimeMilliseconds(save.createdUnixMs));
            var session = new GameSession(_clock, NoWeeklyBonus, GameParameters.App);
            foreach (var op in save.ops ?? Array.Empty<GameOp>())
            {
                _clock.Pin(DateTimeOffset.FromUnixTimeMilliseconds(op.atMs));
                Apply(session, op);
                _ops.Add(op);
            }

            _clock.Unpin();
            session.Advance();
            return session;
        }

        private static bool Apply(GameSession session, GameOp op)
        {
            switch (op.kind)
            {
                case GameOp.Photo:
                    return session.ScanReceipt(new ReceiptRecord(op.id, op.printDay));
                case GameOp.Select:
                    return session.SelectMission(op.id, (MissionVerification)op.verification);
                case GameOp.CheckIn:
                    return session.CheckIn(op.id, (CheckInAnswer)op.answer, op.noShopping);
                case GameOp.MeltGuard:
                    return session.SetMeltReduction(op.value);
                default:
                    return false;
            }
        }

        private void Awake() => EnsureCreated();

        private void Start() => StartTimeSync();

        private void StartTimeSync()
        {
            if (_serverClock != null && !_syncing)
            {
                StartCoroutine(SyncServerTime());
            }
        }

        /// <summary>backend に GET を 1 本送り、応答の Date ヘッダーを時計に渡す。失敗しても遊びは止めない。</summary>
        private IEnumerator SyncServerTime()
        {
            _syncing = true;
            var url = _timeSyncUrl;
            if (string.IsNullOrEmpty(url))
            {
                var config = BackendRuntime.ConfigAsync();
                yield return new WaitUntil(() => config.IsCompleted);
                url = config.IsCompletedSuccessfully ? config.Result.HealthUrl : null;
            }

            if (!string.IsNullOrEmpty(url))
            {
                using var request = UnityWebRequest.Get(url);
                request.timeout = TimeSyncTimeoutSeconds;
                var sent = _monotonic.Seconds;
                yield return request.SendWebRequest();
                ObserveServerResponse(request, sent, _monotonic.Seconds);
            }

            _syncing = false;
        }

        double IServerResponseObserver.MonotonicSeconds => _monotonic?.Seconds ?? 0;

        /// <summary>backend の応答の Date で時計を補正する。判定・みんなの氷山などの通信のたびに呼ばれる。</summary>
        public void ObserveServerResponse(UnityWebRequest request, double sentMonotonic, double receivedMonotonic)
        {
            if (_serverClock == null)
            {
                return;
            }

            if (ServerDateHeaders.Report(request, _serverClock, sentMonotonic, receivedMonotonic) == ServerTimeSample.Accepted)
            {
                // 信頼できる時刻が手に入ったので、保留していた日があれば確定し直す。
                Refresh();
            }
        }

        private void Update()
        {
            if (_island == null)
            {
                return;
            }

            _island.Tick(_clock.UtcNow);
            PollSteps();

            _islandNotifyTimer += Time.unscaledDeltaTime;
            if (_islandNotifyTimer >= IslandNotifyInterval)
            {
                _islandNotifyTimer = 0f;

                // お魚切れなどで氷室の効き目が変わったら、氷の状態機械にも伝える。
                SyncMeltGuard();
                PublishIsland();
            }

            _stepPollTimer += Time.unscaledDeltaTime;
            if (_stepPollTimer >= StepPollInterval)
            {
                _stepPollTimer = 0f;
                RequestSteps();
            }

            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer >= AutosaveInterval)
            {
                _autosaveTimer = 0f;
                Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // モバイルは予告なく終了されるので、裏に回るたびに保存する (.claude/rules/unity.md)。
                Save();
            }
            else
            {
                // 閉じている間の進行 (氷の融解・島の生産) は、復帰時に経過時間からまとめて計算する。
                Refresh();
                StartTimeSync();
            }
        }

        private void OnApplicationQuit() => Save();

        /// <summary>経過した日を確定させて今日を始める。起動・復帰・画面を開いたときに呼ぶ。</summary>
        public void Refresh()
        {
            Apply(() => _session.Advance());
            RequestSteps();
        }

        // ------------------------------------------------------------------
        // 現実の行動
        // ------------------------------------------------------------------

        /// <summary>今日まだ写真の資源を受け取れるか。</summary>
        public bool CanReceivePhotoToday => Island.CanReceivePhoto(EffectiveToday);

        public string QuickEcoError { get; private set; }

        /// <summary>確認画面の日付・予告額を再検証し、保存できた時だけ受取を確定する。</summary>
        public bool TryReceiveQuickEco(string id, int confirmedDay, double confirmedReward, out ResourceBundle granted)
        {
            granted = null;
            Refresh();
            QuickEcoError = "日付や受け取り状況が変わったよ。一覧からもう一度確認してね。";
            if (_session.PendingAheadDay.HasValue || Today != EffectiveToday || confirmedDay != EffectiveToday ||
                !_island.CanReceiveQuickEco(id, EffectiveToday)) return false;
            if (confirmedReward != _island.PreviewQuickEcoReward())
            {
                QuickEcoError = "報酬が変わったよ。一覧からもう一度確認してね。";
                return false;
            }

            var before = _island.ToSave();
            var activityBefore = _activity.ToArray();
            var trimmedBefore = _save.activityTrimmed;
            var reward = _island.ReceiveQuickEco(id, EffectiveToday);
            AddActivity(ActivityEntry.QuickEco, QuickEcoActions.Find(id).Title, reward, persist: false);
            if (!Save())
            {
                _island = IslandGame.FromSave(before, IslandParameters.Default, _clock.UtcNow);
                SyncIsland();
                _activity.Clear();
                _activity.AddRange(activityBefore);
                _save.activityTrimmed = trimmedBefore;
                _save.island = before;
                _save.activity = activityBefore;
                QuickEcoError = "保存できなかったよ。電気と回数は戻したので、もう一度試してね。";
                MarkIslandDirty();
                return false;
            }

            granted = reward;
            QuickEcoError = null;
            ResourcesReceived?.Invoke(reward, "ちょこっとエコ");
            MarkIslandDirty();
            return true;
        }

        /// <summary>
        /// エコ活動の写真 1 枚を記録する。写真そのものは保存しない。
        /// 判定サーバーが撮り直し・受け付けないと決めた写真 (<see cref="EcoPhotoReview.Counts"/> が false) は、記録も資源も出さない。
        /// </summary>
        public EcoPhotoResult RecordEcoPhoto(EcoPhotoVerdict verdict)
        {
            EnsureCreated();
            var before = HomeState.Of(_session);
            if (verdict != null && !verdict.Review.Counts)
            {
                return new EcoPhotoResult(verdict, false, null, before, before);
            }

            var wasRecorded = _session.IsRecorded(_session.Today);
            ResourceBundle granted = null;
            Apply(() =>
            {
                // 時計が戻っていれば最後に見た日、大きく進んで保留中なら null (記録も資源も受け付けない)。
                var day = _session.Advance();
                if (day == null)
                {
                    return;
                }

                var op = new GameOp
                {
                    kind = GameOp.Photo,
                    atMs = _clock.UtcNow.ToUnixTimeMilliseconds(),
                    id = $"photo-{day.Value}-{_ops.Count}",
                    printDay = day.Value,
                };

                // 資源は記録が通ったときだけ渡す (CheckIn と同じ形)。
                if (Apply(_session, op))
                {
                    _ops.Add(op);
                    granted = _island.ReceivePhoto(verdict, day.Value);
                }
            });

            if (granted != null)
            {
                AddActivity(ActivityEntry.Photo, verdict.Label, granted, (int)verdict.Category);
                ResourcesReceived?.Invoke(granted, EcoCategories.NameOf(verdict.Category));
            }

            return new EcoPhotoResult(verdict, !wasRecorded && _session.IsRecorded(_session.Today), granted, before,
                HomeState.Of(_session));
        }

        /// <summary>今日のミッションを選ぶ。</summary>
        public bool SelectMission(MissionDef mission)
        {
            if (mission == null)
            {
                return false;
            }

            var ok = false;
            Apply(() =>
            {
                var op = new GameOp
                {
                    kind = GameOp.Select,
                    atMs = _clock.UtcNow.ToUnixTimeMilliseconds(),
                    id = mission.Id,
                    verification = (int)MissionVerification.Self,
                };
                ok = Apply(_session, op);
                if (ok)
                {
                    _ops.Add(op);
                }
            });
            return ok;
        }

        /// <summary>夜の振り返り。どの答えでも記録日になり、オールマイティがもらえる。</summary>
        public ResourceBundle CheckIn(MissionDef mission, CheckInAnswer answer)
        {
            if (mission == null)
            {
                return null;
            }

            ResourceBundle granted = null;
            Apply(() =>
            {
                var op = new GameOp
                {
                    kind = GameOp.CheckIn,
                    atMs = _clock.UtcNow.ToUnixTimeMilliseconds(),
                    id = mission.Id,
                    answer = (int)answer,
                };
                if (Apply(_session, op))
                {
                    _ops.Add(op);
                    granted = _island.ReceiveCheckIn(answer);
                }
            });

            if (granted != null)
            {
                AddActivity(ActivityEntry.Mission, mission.Title, granted);
                ResourcesReceived?.Invoke(granted, "ミッション");
            }

            return granted;
        }

        /// <summary>今日の歩数を問い合わせる (結果は後で届く)。</summary>
        public void RequestSteps()
        {
            if (_steps == null || !_steps.IsSupported)
            {
                return;
            }

            _steps.Request(AppDayStartUtc(EffectiveToday));
        }

        private void PollSteps()
        {
            if (_steps == null || !_steps.TryTake(out var steps, out var fromUtc))
            {
                return;
            }

            // 日付が変わる直前に出した問い合わせの応答 (前日の歩数) が後から届いたら捨てる。
            var day = EffectiveToday;
            if (fromUtc != AppDayStartUtc(day))
            {
                return;
            }

            StepsToday = steps;
            var gas = _island.ReceiveSteps(steps, day);
            if (gas > 0)
            {
                var bundle = ResourceBundle.Of(Resource.Gas, gas);
                AddActivity(ActivityEntry.Steps, $"{steps:N0} 歩", bundle);
                ResourcesReceived?.Invoke(bundle, "歩いたぶん");
                MarkIslandDirty();
            }
        }

        /// <summary>アプリの日 (03:00 JST 区切り) の始まりの時刻。</summary>
        public static DateTimeOffset AppDayStartUtc(int appDay)
        {
            var epoch = new DateTimeOffset(1970, 1, 1, 3, 0, 0, TimeSpan.FromHours(9));
            var guess = epoch.AddDays(appDay - AppCalendar.AppDay(epoch));
            return guess.ToUniversalTime();
        }

        // ------------------------------------------------------------------
        // 島の操作 (成功したら保存する)
        // ------------------------------------------------------------------

        public IslandActionResult Build(FacilityKind kind, Cell origin) => IslandAction(g => g.Build(kind, origin));
        public IslandActionResult Move(int id, Cell origin) => IslandAction(g => g.Move(id, origin));
        public IslandActionResult Demolish(int id) => IslandAction(g => g.Demolish(id));
        public IslandActionResult SetWorkers(int id, int workers) => IslandAction(g => g.SetWorkers(id, workers));
        public IslandActionResult SetRecipe(int id, int recipe) => IslandAction(g => g.SetRecipe(id, recipe));
        public IslandActionResult AddPearl(int id) => IslandAction(g => g.AddPearl(id));
        public IslandActionResult Upgrade(int id) => IslandAction(g => g.Upgrade(id));
        public IslandActionResult Expand(Cell cell) => IslandAction(g => g.Expand(cell));
        public IslandActionResult Deliver(int slot) => IslandAction(g => g.Deliver(slot));

        private IslandActionResult IslandAction(Func<IslandGame, IslandActionResult> action)
        {
            EnsureCreated();
            var r = action(_island);
            if (r == IslandActionResult.Ok)
            {
                SyncMeltGuard();
                MarkIslandDirty();
                Save();
            }

            return r;
        }

        public void CompleteOnboarding(string playerName)
        {
            EnsureCreated();
            _save.playerName = string.IsNullOrWhiteSpace(playerName) ? "" : playerName.Trim();
            _save.onboarded = true;
            Save();
        }

        /// <summary>設定の「データを消す」。最初からやり直す。</summary>
        public void ResetAll()
        {
            EnsureCreated();
            _store.Delete();
            _clock.ResetOffset();
            LoadOrCreate(null);
            Save();
            Apply(() => { });
            MarkIslandDirty();
        }

        public OfflineReport TakeOfflineReport() => Island.TakeOfflineReport();

        // ------------------------------------------------------------------
        // 開発ビルドの操作
        // ------------------------------------------------------------------

        /// <summary>見本の写真を 1 枚記録する (開発用)。</summary>
        public bool RecordSampleReceipt()
        {
            var r = RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.Goods, "マイボトル", "見本の写真だよ", 1.0));
            return r.NewRecordDay;
        }

        /// <summary>デモ用に時計を進める。開発ビルドと Editor だけ画面から呼べる。</summary>
        public void AdvanceDays(int days)
        {
            if (days <= 0)
            {
                return;
            }

            EnsureCreated();
            _clock.AdvanceDays(days);
            Refresh();
            Save();
        }

        /// <summary>デモ用に島だけ時間を進める (時計は進めない)。</summary>
        public void DevSimulateIsland(double seconds)
        {
            Island.Simulate(seconds);
            MarkIslandDirty();
        }

        public void DevGrant(Resource resource, double amount)
        {
            Island.Grant(ResourceBundle.Of(resource, amount));
            MarkIslandDirty();
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private void Apply(Action action)
        {
            EnsureCreated();
            var before = HomeState.Of(_session);
            action();
            var today = _session.Today;
            var isFirstOpenToday = today > _lastOpenDay;
            var isComeback = today - _lastOpenDay >= ComebackDays;
            _lastOpenDay = Math.Max(_lastOpenDay, today);
            SyncIsland();
            _island.Tick(_clock.UtcNow);
            SyncMeltGuard();
            Changed?.Invoke(new GameStateChange(before, HomeState.Of(_session), isComeback, isFirstOpenToday));
            MarkIslandDirty();
            Save();
        }

        private void SyncIsland()
        {
            _island.SyncWorld(_session.Ice, _session.TotalRecordDays, _session.Arrivals.Select(a => a.Species), _session.Tier);
            _island.ObserveQuickEcoDay(_session.LastStartedDay);
        }

        /// <summary>
        /// 島の氷室の効き目 (<see cref="IslandGame.MeltReduction"/>) が氷の状態機械と違えば、操作として記録して渡す。
        /// 操作の記録を再生すれば同じ時刻に同じ値が入るので、起動し直しても氷は同じになる。
        /// </summary>
        private void SyncMeltGuard()
        {
            var reduction = _island.MeltReduction;
            if (Math.Abs(reduction - _session.MeltReduction) < 1e-6)
            {
                return;
            }

            var op = new GameOp
            {
                kind = GameOp.MeltGuard,
                atMs = _clock.UtcNow.ToUnixTimeMilliseconds(),
                id = "meltguard",
                value = reduction,
            };
            if (Apply(_session, op))
            {
                _ops.Add(op);
                SyncIsland();
            }
        }

        private void MarkIslandDirty() => PublishIsland();

        /// <summary>出来事を配り、数字の再描画を促す (生産は少しずつ進むので、出来事が無くても数字は変わる)。</summary>
        private void PublishIsland()
        {
            var events = _island.DrainEvents();
            if (events.Count > 0)
            {
                IslandEvents?.Invoke(events);
            }

            IslandChanged?.Invoke();
        }

        private void AddActivity(string kind, string label, ResourceBundle granted, int category = -1, bool persist = true)
        {
            _activity.Add(new ActivityEntry
            {
                atMs = _clock.UtcNow.ToUnixTimeMilliseconds(),
                day = _session.Today,
                kind = kind,
                category = category,
                label = label ?? "",
                electricity = granted[Resource.Electricity],
                water = granted[Resource.Water],
                gas = granted[Resource.Gas],
                allMighty = granted[Resource.AllMighty],
            });
            if (_activity.Count > ActivityLimit)
            {
                var drop = _activity.Count - ActivityLimit;
                _activity.RemoveRange(0, drop);
                _save.activityTrimmed += drop;
            }

            if (persist) Save();
        }

        private bool Save()
        {
            if (_store == null || _session == null)
            {
                return false;
            }

            _save.devOffsetMs = (long)_clock.Offset.TotalMilliseconds;
            _save.lastOpenDay = _lastOpenDay;
            _save.ops = _ops.ToArray();
            _save.island = _island.ToSave();
            _save.activity = _activity.ToArray();
            try
            {
                _store.Save(_save);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(GameStateSystem)}: 保存に失敗しました ({e.GetType().Name}: {e.Message})");
                return false;
            }
        }

        /// <summary>週の選び方の氷 B。写真では採点しないので 0 (docs/production-island/game-design.md)。</summary>
        private static double NoWeeklyBonus(int week, IReadOnlyList<ReceiptRecord> receipts)
        {
            return 0.0;
        }
    }
}
