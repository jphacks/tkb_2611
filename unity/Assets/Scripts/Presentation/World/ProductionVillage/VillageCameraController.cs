using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 村を斜め上から見下ろす正投影カメラ。起動時と画面の縦横比が変わったときに、島の幅が画面に収まる寄りを計算する。
    /// パンは見ている点 (地面の上) を動かし、盤面の外へは出さない。ズームの限界も島の大きさから決める。
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

        [Tooltip("全体表示サイズに対する施設群の開始・解除比率")]
        [SerializeField] private float _groupEnterRatio = .9f;
        [SerializeField] private float _groupExitRatio = .8f;
        public bool GroupViewActive { get; private set; }

        private Camera _camera;
        private Vector3 _focus;
        private float _fitSize;
        private float _aspect;
        private float _activePitch = 55f;
        private float _targetPitch = 55f;
        private float _activeYaw;
        private float _targetYaw;

        /// <summary>島全体が収まるときの orthographicSize。</summary>
        public float FitSize => _fitSize;

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
                Frame();
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

            if (dirty)
            {
                Apply();
            }
        }

        public void SetTopDown(bool topDown)
        {
            IsTopDown = topDown;
            _targetPitch = topDown ? 88f : _pitch;
            if (_camera != null)
            {
                _fitSize = FitSizeFor(_camera.aspect, IslandHalf, _targetPitch, _fill);
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, _fitSize * .35f, _fitSize * 1.15f);
                Apply();
            }
        }

        public void ToggleTopDown() => SetTopDown(!IsTopDown);

        public void RotateStep90()
        {
            _targetYaw = (_targetYaw + 90f) % 360f;
            if (_camera != null) Apply();
        }

        /// <summary>島全体が見える寄りに戻す。</summary>
        public void Frame()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _aspect = _camera.aspect;
            _fitSize = FitSizeFor(_aspect, IslandHalf, _targetPitch, _fill);
            _camera.orthographicSize = _fitSize;
            _focus = Vector3.zero;
            Apply();
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

        public void Pan(Vector2 from, Vector2 to)
        {
            if (!Ground(from, out var a) || !Ground(to, out var b))
            {
                return;
            }

            _focus += a - b;
            Apply();
        }

        public void Zoom(float factor)
        {
            if (!VillageZoomPolicy.ValidFactor(factor) || _camera == null) return;
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor, _fitSize * .35f, _fitSize * 1.15f);
            Apply();
        }

        /// <summary>見ている点を盤面の中に収め、カメラをそこから引いた位置に置く。寄るほど動ける範囲を広げる。</summary>
        private void Apply()
        {
            GroupViewActive = VillageZoomPolicy.GroupActive(_camera.orthographicSize, _fitSize, GroupViewActive, _groupEnterRatio, _groupExitRatio);
            var half = VillageWorldRenderer.Cell * 12;
            var slack = half * Mathf.Clamp01(1 - _camera.orthographicSize / Mathf.Max(.01f, _fitSize));
            _focus.x = Mathf.Clamp(_focus.x, -slack, slack);
            _focus.z = Mathf.Clamp(_focus.z, -slack, slack);
            _focus.y = 0;
            var shift = transform.up * (_camera.orthographicSize * 2 * _viewportShift);
            transform.position = _focus - transform.forward * Distance - shift;
        }
    }
}
