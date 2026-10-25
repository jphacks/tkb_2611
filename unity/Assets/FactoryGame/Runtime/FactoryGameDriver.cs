using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Factory;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectPenguin.FactoryGame
{
    [Serializable] public sealed class FactoryModel { public string recipe; public GameObject prefab; }

    public sealed class FactoryGameDriver : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Font _font;
        [SerializeField] private FactoryModel[] _models;
        private FactorySession _session;
        private FactoryGameHud _hud;
        private FactoryGameWorld _world;
        private FactoryRecordInput _records;
        private bool _barcodeRead, _screenRead;
        private int _barcodeDay, _screenDay;
        private IStepSource _steps;
        private bool _suspended, _stepPending, _pointerDown, _dragged;
        private int _stepDay;
        private DateTimeOffset _stepFrom;
        private double _pendingMs;
        private float _nextRefresh, _stepAt;
        private Vector2 _pointerStart, _pointerLast;
        private double _simulationMs, _renderMs;
        private float _pinchDistance;
        public bool Overview { get; private set; }
        public bool Paused { get; private set; }
        public int SelectedId { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public string Message { get; private set; } = "マスをタップして建設。数値は調整中の試作設定です";
        public string StepStatus { get; private set; } = "歩数：iOS以外は未接続";
        public bool DevelopmentInputs => Application.isEditor || UnityEngine.Debug.isDebugBuild;
        public FactorySession Session => _session;
        public double SimulationMilliseconds => _simulationMs;
        public double RenderMilliseconds => _renderMs;
        public static int AppToday => AppCalendar.AppDay(DateTimeOffset.UtcNow);
        public static int RecordToday => ProjectPenguin.Domain.Clock.JstCalendar.DayNumber(DateTimeOffset.UtcNow);
#if UNITY_EDITOR
        public static string EditorSaveDirectoryOverride { get; set; }
#endif

        private void Start()
        {
            try
            {
                var catalog = FactoryStore.LoadCatalog(); var rules = FactoryStore.LoadRules();
                var directory = Application.persistentDataPath;
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(EditorSaveDirectoryOverride))
                {
                    EditorSaveDirectoryOverride = UnityEditor.SessionState.GetString("FactoryGame.PreviewDirectory", "");
                    UnityEditor.SessionState.EraseString("FactoryGame.PreviewDirectory");
                }
                if (!string.IsNullOrEmpty(EditorSaveDirectoryOverride)) directory = EditorSaveDirectoryOverride;
#endif
                var store = new FactoryStore(directory, catalog, rules);
                _session = store.Open(out var status);
                _hud = new FactoryGameHud(transform, _font, this);
                Message = status == SaveLoadStatus.NotFound ? Message : status == SaveLoadStatus.Loaded
                    ? "保存を再開しました。中断中の生産は加算しません" : "バックアップから工場を復旧しました";
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorSaveDirectoryOverride)) Message = "開発用の独立した保存を使用中。合成の初期状態と実データを区別してください";
#endif
                _world = new FactoryGameWorld(transform, _camera, _models);
                _records = new FactoryRecordInput(gameObject.name, Notify, code => { _barcodeRead = true; _barcodeDay = RecordToday; _hud.SetBarcode(code); },
                    minutes => { _screenRead = true; _screenDay = RecordToday; _hud.SetScreenMinutes(minutes); });
#if UNITY_IOS && !UNITY_EDITOR
                _steps = StepSources.Create();
#endif
                Refresh();
            }
            catch (Exception)
            {
                if (_hud == null) _hud = new FactoryGameHud(transform, _font, this);
                Message = "保存または設定を読み込めません。既存の保存を保持しています。アプリの版と設定を確認してください";
                _hud.ShowFailure(Message);
            }
        }

        private void Update()
        {
            if (_session == null || _suspended) return;
            if (!Paused)
            {
                _pendingMs += Math.Min(Time.unscaledDeltaTime, .5f) * 1000;
                if (_pendingMs >= 1000)
                {
                    var elapsed = (long)_pendingMs / 1000 * 1000;
                    var watch = Stopwatch.StartNew();
                    var result = _session.AdvanceTo(checked(_session.Snapshot.requestedMs + elapsed));
                    watch.Stop(); _simulationMs = watch.Elapsed.TotalMilliseconds;
                    if (result.Success) _pendingMs -= elapsed;
                    else { Message = result.Message; Paused = true; }
                }
            }
            PollSteps(); _records?.Poll(); HandlePointer();
            if (Time.unscaledTime >= _nextRefresh) { Refresh(); _nextRefresh = Time.unscaledTime + .25f; }
        }

        public void Apply(FactoryResult result) { Message = result.Message + (result.Amount != 0 ? $" ({result.Amount:+#;-#;0})" : ""); Refresh(); }
        public void Notify(string message) { Message = message; Refresh(); }
        public void FitCamera()
        {
            if (_session == null) return;
            var blocks = _session.Snapshot.blocks;
            var center = new Vector3((blocks.Min(b => b.x) + blocks.Max(b => b.x) + .5f) * FactoryGameWorld.CellSize, 0,
                (blocks.Min(b => b.y) + blocks.Max(b => b.y) + .5f) * FactoryGameWorld.CellSize);
            _camera.transform.position = center + new Vector3(0, 12, -6.8f); _camera.transform.LookAt(center);
            var width = (blocks.Max(b => b.x) - blocks.Min(b => b.x) + 1) * 2 * FactoryGameWorld.CellSize;
            var height = (blocks.Max(b => b.y) - blocks.Min(b => b.y) + 1) * 2 * FactoryGameWorld.CellSize;
            _camera.orthographicSize = Mathf.Clamp(Mathf.Max(height * .65f, width / _camera.aspect * .65f), 3, 80);
        }
        public void ZoomCamera(float scale)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * scale, 2, 80);
            if (_camera.orthographicSize >= 6 && _session.Snapshot.stage >= _session.Rules.zoomUnlockStage) Overview = true;
            if (_camera.orthographicSize < 5) Overview = false;
            Refresh();
        }
        public void Build(string recipe) { var r = _session.Build(recipe, CellX, CellY); if (r.Success) SelectedId = r.FacilityId; Apply(r); }
        public void MoveSelected() => Apply(_session.Move(SelectedId, CellX, CellY));
        public void Effort(int value) => Apply(_session.SetEffort(SelectedId, value));
        public void Expand(int dx, int dy) => Apply(_session.Expand((int)Math.Floor(CellX / 2.0) + dx, (int)Math.Floor(CellY / 2.0) + dy));
        public void Deliver(string resource, long amount) => Apply(_session.Deliver(Guid.NewGuid().ToString("N"), resource, amount));
        public void TogglePause() { Paused = !Paused; _pendingMs = 0; Message = Paused ? "生産時計を一時停止しました" : "生産を再開しました"; Refresh(); }
        public void ToggleProduction()
        {
            var n = _session.Snapshot.facilities.FirstOrDefault(f => f.id == SelectedId);
            if (n != null) Apply(_session.SetEnabled(n.id, !n.enabled));
        }
        public void ToggleOverview()
        {
            if (_session.Snapshot.stage < _session.Rules.zoomUnlockStage)
            { Notify($"組み合わせの表示は段階S{_session.Rules.zoomUnlockStage}で解放されます"); return; }
            Overview = !Overview; Refresh();
        }
        public void SelectCell(int x, int y)
        {
            CellX = x; CellY = y;
            var node = _session.Snapshot.facilities.FirstOrDefault(n => !n.upper && n.x == x && n.y == y);
            if (node != null) SelectedId = node.id;
            Refresh();
        }
        public void SelectUpper()
        {
            var x = (int)Math.Floor(CellX / 2.0); var y = (int)Math.Floor(CellY / 2.0);
            var upper = _session.Snapshot.facilities.FirstOrDefault(n => n.upper && !n.retired && n.x == x && n.y == y);
            if (upper == null) { Notify("この区画には成立した上位施設がありません"); return; }
            SelectedId = upper.id; Notify("区画の上位施設を選択しました。内部と別に稼働・エフォートを変更できます");
        }

        public void RequestSteps()
        {
            if (_steps == null) { StepStatus = "実歩数：この環境は未接続（0歩の成功ではありません）"; Refresh(); return; }
            if (!_steps.IsSupported) { StepStatus = "実歩数：端末非対応"; Refresh(); return; }
            if (_stepPending) return;
            _stepDay = AppToday; _stepFrom = GameStateSystem.AppDayStartUtc(_stepDay);
            _steps.Request(_stepFrom); _stepPending = true; _stepAt = Time.unscaledTime;
            StepStatus = "実歩数：権限の確認・取得中"; Refresh();
        }

        private void PollSteps()
        {
            if (!_stepPending) return;
            if (_stepDay != AppToday) { _stepPending = false; StepStatus = "日付が変わりました。歩数を再取得してください"; return; }
            if (_steps.TryTake(out var count, out var from))
            {
                _stepPending = false;
                if (from != _stepFrom) { StepStatus = "対象日が違う歩数を破棄しました"; return; }
                var r = _session.AcceptSteps(_stepDay, count, AppToday);
                StepStatus = r.Success ? $"実歩数：{count:N0}歩を確認" : "実歩数：受領を保存できませんでした";
                Apply(r);
            }
            else if (Time.unscaledTime - _stepAt > 12)
            { _stepPending = false; StepStatus = "実歩数：応答なし・権限なし・取得失敗。設定を確認して再試行してください"; }
        }

        public void ScanBarcode() => _records?.Open(true);
        public void ReadScreenImage() => _records?.Open(false);
        public void OnFactoryRecordResult(string result) => _records?.Receive(result);
        public void ConfirmBarcode(string barcode)
        {
            if (!_barcodeRead && !DevelopmentInputs) { Notify("先に商品バーコードを読み取ってください"); return; }
            if (_barcodeRead && _barcodeDay != RecordToday) { _barcodeRead = false; Notify("記録の日付が変わりました。商品を再度読み取ってください"); return; }
            var result = _session.AcceptWaterBarcode(barcode, RecordToday, RecordToday, true);
            if (result.Success) _barcodeRead = false;
            Apply(result);
        }
        public void ConfirmScreen(int minutes)
        {
            if (!_screenRead && !DevelopmentInputs) { Notify("先に前日のスクリーンタイム画像を読み取ってください"); return; }
            if (_screenRead && _screenDay != RecordToday) { _screenRead = false; Notify("記録の日付が変わりました。前日の画像を再度選択してください"); return; }
            var result = _session.AcceptScreen("screen-record-" + (RecordToday - 1), RecordToday - 1, minutes, RecordToday, true);
            if (result.Success) _screenRead = false;
            Apply(result);
        }
        public void FakeSteps()
        { if (DevelopmentInputs) Apply(_session.AcceptSteps(AppToday, Math.Min(172800, _session.Snapshot.maxSteps + 5000), AppToday)); }
        public void FakeEncounter()
        {
            if (!DevelopmentInputs) return;
            var s = _session.Snapshot;
            if (s.encounterDay < AppToday)
            { var begin = _session.BeginEncounterDay(AppToday, EncounterToken.Create()); if (!begin.Success) { Apply(begin); return; } }
            var peer = EncounterToken.Create(); var now = (long)(Time.realtimeSinceStartupAsDouble * 1000);
            var observe = _session.ObservePeer(peer, now); if (!observe.Success) { Apply(observe); return; }
            Apply(_session.ConfirmEncounter(AppToday, peer, EncounterToken.PairId(_session.Snapshot.selfToken, peer), true, now));
        }

        private void HandlePointer()
        {
            var fingers = Touchscreen.current?.touches.Where(t => t.press.isPressed).ToArray();
            if (fingers != null && fingers.Length >= 2)
            {
                var a = fingers[0].position.ReadValue(); var b = fingers[1].position.ReadValue();
                var pinchLength = Vector2.Distance(a, b);
                if (_camera.pixelRect.Contains(a) && _camera.pixelRect.Contains(b) && _pinchDistance > 0 && pinchLength > 0)
                    ZoomCamera(_pinchDistance / pinchLength);
                _pinchDistance = pinchLength; _pointerDown = false; return;
            }
            _pinchDistance = 0;
            Vector2 position; bool pressed, released;
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
            { position = touch.position.ReadValue(); pressed = touch.press.wasPressedThisFrame; released = touch.press.wasReleasedThisFrame; }
            else if (Mouse.current != null)
            { position = Mouse.current.position.ReadValue(); pressed = Mouse.current.leftButton.wasPressedThisFrame; released = Mouse.current.leftButton.wasReleasedThisFrame; }
            else return;
            if (pressed && _camera.pixelRect.Contains(position))
            { _pointerDown = true; _dragged = false; _pointerStart = _pointerLast = position; }
            if (!_pointerDown) return;
            if ((position - _pointerStart).sqrMagnitude > 144) _dragged = true;
            if (_dragged)
            {
                var delta = position - _pointerLast;
                _camera.transform.position -= new Vector3(delta.x, 0, delta.y) * (_camera.orthographicSize * 2 / _camera.pixelHeight);
            }
            _pointerLast = position;
            if (!released) return;
            _pointerDown = false;
            if (_dragged) return;
            var ray = _camera.ScreenPointToRay(position);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
            {
                var point = ray.GetPoint(distance);
                SelectCell(Mathf.RoundToInt(point.x / FactoryGameWorld.CellSize), Mathf.RoundToInt(point.z / FactoryGameWorld.CellSize));
                if (Overview) { Overview = false; Refresh(); }
            }
        }

        private void Refresh()
        {
            if (_session == null || _world == null) return;
            var safe = Screen.safeArea;
            _camera.rect = new Rect(safe.x / Screen.width, (safe.y + safe.height * .43f) / Screen.height,
                safe.width / Screen.width, safe.height * .32f / Screen.height);
            var state = _session.Snapshot; var watch = Stopwatch.StartNew();
            _world.Render(state, Overview, CellX, CellY);
            watch.Stop(); _renderMs = watch.Elapsed.TotalMilliseconds;
            _hud.Render(state);
        }

        private void OnApplicationPause(bool paused) { _suspended = paused; _pendingMs = 0; if (!paused) _stepPending = false; }
        private void OnApplicationFocus(bool focused) { _suspended = !focused; _pendingMs = 0; }
        private void OnDestroy() { _records?.Cancel(); _world?.Dispose(); }
    }
}
