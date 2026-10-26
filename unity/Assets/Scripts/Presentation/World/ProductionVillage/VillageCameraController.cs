using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 村を斜め上から見下ろす正投影カメラ。起動時と画面の縦横比が変わったときに、島の幅が画面に収まる寄りを計算する。
    /// 寄りはランクで解放する 3 段階 (<see cref="VillageZoomLevel"/>) だけで、切り替えは短い補間でパッと動かす。
    /// パンは詳細のときだけ、見ている点 (地面の上) を盤面の中で動かす。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class VillageCameraController : MonoBehaviour
    {
        /// <summary>カメラから見ている点までの距離。正投影なので見た目の大きさには効かず、手前の切り取りだけに効く。</summary>
        private const float Distance = 40f;

        [Tooltip("見下ろす角度 (度)。90 で真上")]
        [SerializeField] private float _pitch = 55f;

        [Tooltip("島の幅のうち、画面の幅に収める割合")]
        [SerializeField] private float _fill = 1f;

        [Tooltip("見ている点を画面の中心からずらす量 (画面の高さに対する割合、+ で島が上へ寄る)。上下の UI に隠れる分の釣り合いを取る")]
        [SerializeField] private float _viewportShift = -.04f;

        [Tooltip("全体と詳細を切り替える時間 (秒)")]
        [SerializeField] private float _switchSeconds = .2f;

        private Camera _camera;
        private Vector3 _focus;
        private float _fitSize;
        private float _aspect;
        private float _activePitch = 55f;
        private float _targetPitch = 55f;
        private float _activeYaw;
        private float _targetYaw;

        private float _fromSize, _toSize, _switchT = 1f;
        private Vector3 _fromFocus, _toFocus;

        /// <summary>島全体が収まるときの orthographicSize。</summary>
        public float FitSize => _fitSize;

        /// <summary>今の寄りの段階。切り替え中は行き先の段階。</summary>
        public VillageZoomLevel Level { get; private set; } = VillageZoomLevel.Overview;
        public int MaximumZoom { get; private set; } = 2;
        public event System.Action LevelChanged;
        public void SetMaximumZoom(int maximum)
        {
            MaximumZoom = Mathf.Clamp(maximum, 2, 3);
            if ((int)Level > MaximumZoom) SetLevel((VillageZoomLevel)MaximumZoom);
        }

        /// <summary>全体表示の間は施設群の追加生産が動く (村の規則)。</summary>
        public bool GroupViewActive => VillageZoomPolicy.GroupView(Level);

        /// <summary>段階を切り替えている途中か。</summary>
        public bool Switching => _switchT < 1f;

        /// <summary>真上からの見下ろし視点 (88°) かどうか。</summary>
        public bool IsTopDown { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _activePitch = _targetPitch = _pitch;
            transform.rotation = Quaternion.Euler(_activePitch, _activeYaw, 0);
            Frame();
        }

        private void LateUpdate()
        {
            var dirty = false;
            if (!Mathf.Approximately(_aspect, _camera.aspect))
            {
                _aspect = _camera.aspect;
                _fitSize = FitSizeFor(_aspect, IslandHalf, _targetPitch, _fill);
                Jump(VillageZoomPolicy.SizeFor(Level, _fitSize), _focus);
                dirty = true;
            }

            if (!Mathf.Approximately(_activePitch, _targetPitch) || !Mathf.Approximately(_activeYaw, _targetYaw))
            {
                _activePitch = Mathf.Lerp(_activePitch, _targetPitch, Time.unscaledDeltaTime * 10f);
                _activeYaw = Mathf.LerpAngle(_activeYaw, _targetYaw, Time.unscaledDeltaTime * 10f);
                if (Mathf.Abs(_activePitch - _targetPitch) < 0.1f) _activePitch = _targetPitch;
                if (Mathf.Abs(Mathf.DeltaAngle(_activeYaw, _targetYaw)) < 0.1f) _activeYaw = _targetYaw;
                transform.rotation = Quaternion.Euler(_activePitch, _activeYaw, 0);
                dirty = true;
            }

            if (Switching)
            {
                _switchT = _switchSeconds > 0 ? Mathf.Min(1f, _switchT + Time.unscaledDeltaTime / _switchSeconds) : 1f;
                var k = VillageZoomPolicy.Ease(_switchT);
                _camera.orthographicSize = Mathf.Lerp(_fromSize, _toSize, k);
                _focus = Vector3.Lerp(_fromFocus, _toFocus, k);
                dirty = true;
            }

            if (dirty)
            {
                Apply();
            }
        }

        /// <summary>
        /// 段階を切り替える。詳細へ寄るときは <paramref name="screenPoint"/> (つまんだ中心やホイールの位置) の地面に寄る。
        /// 全体へ引くときは島の中心に戻す。
        /// </summary>
        public void SetLevel(VillageZoomLevel level, Vector2? screenPoint = null)
        {
            if (_camera == null || (int)level < 1 || (int)level > MaximumZoom || level == Level) return;
            var focus = Vector3.zero;
            if (level == VillageZoomLevel.Detail)
            {
                focus = screenPoint.HasValue && Ground(screenPoint.Value, out var ground) ? ground : _focus;
            }

            Level = level;
            var size = VillageZoomPolicy.SizeFor(level, _fitSize);
            _fromSize = _camera.orthographicSize;
            _fromFocus = _focus;
            _toSize = size;
            _toFocus = Clamp(focus, size);
            _switchT = 0f;
            LevelChanged?.Invoke();
        }

        public void ToggleLevel(Vector2? screenPoint = null) => SetLevel(VillageZoomPolicy.Toggle(Level), screenPoint);

        public void SetTopDown(bool topDown)
        {
            IsTopDown = topDown;
            _targetPitch = topDown ? 88f : _pitch;
            if (_camera != null)
            {
                _fitSize = FitSizeFor(_camera.aspect, IslandHalf, _targetPitch, _fill);
                Jump(VillageZoomPolicy.SizeFor(Level, _fitSize), _focus);
            }
        }

        public void ToggleTopDown() => SetTopDown(!IsTopDown);

        public void RotateStep90()
        {
            _targetYaw = (_targetYaw + 90f) % 360f;
            if (_camera != null) Apply();
        }

        /// <summary>島全体が見える寄りに、補間せずに戻す。</summary>
        public void Frame()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _aspect = _camera.aspect;
            _fitSize = FitSizeFor(_aspect, IslandHalf, _targetPitch, _fill);
            Level = VillageZoomLevel.Overview;
            Jump(_fitSize, Vector3.zero);
        }

        /// <summary>島の半分の幅 (盤面の半分 + 縁取り)。</summary>
        public static float IslandHalf => VillageWorldRenderer.Cell * 12 + VillageIslandView.Margin + .3f;

        /// <summary>
        /// 島 (一辺 <paramref name="islandHalf"/>×2 の正方形) が、横は画面幅の <paramref name="fill"/>、縦は画面の高さの 6 割に収まる orthographicSize。
        /// 縦長の画面では幅で決まり、横長の画面では高さで決まる。
        /// </summary>
        public static float FitSizeFor(float aspect, float islandHalf, float pitch, float fill)
        {
            var byWidth = islandHalf / Mathf.Max(.01f, aspect * fill);
            var depth = islandHalf * Mathf.Sin(pitch * Mathf.Deg2Rad) + Mathf.Cos(pitch * Mathf.Deg2Rad) * .5f;
            var byHeight = depth / .6f;
            return Mathf.Max(byWidth, byHeight);
        }

        public bool Ground(Vector2 p, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(p);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>詳細のときだけ動かす。全体では島の中心から動かさない。</summary>
        public void Pan(Vector2 from, Vector2 to)
        {
            if (Level != VillageZoomLevel.Detail || Switching || !Ground(from, out var a) || !Ground(to, out var b))
            {
                return;
            }

            _focus += a - b;
            Apply();
        }

        private void Jump(float size, Vector3 focus)
        {
            _switchT = 1f;
            _camera.orthographicSize = size;
            _focus = focus;
            Apply();
        }

        /// <summary>見ている点を盤面の中に収める。寄るほど動ける範囲を広げる。</summary>
        private Vector3 Clamp(Vector3 focus, float size)
        {
            var half = VillageWorldRenderer.Cell * 12;
            var slack = half * Mathf.Clamp01(1 - size / Mathf.Max(.01f, _fitSize));
            return new Vector3(Mathf.Clamp(focus.x, -slack, slack), 0, Mathf.Clamp(focus.z, -slack, slack));
        }

        private void Apply()
        {
            _focus = Clamp(_focus, _camera.orthographicSize);
            var shift = transform.up * (_camera.orthographicSize * 2 * _viewportShift);
            transform.position = _focus - transform.forward * Distance - shift;
        }
    }
}
