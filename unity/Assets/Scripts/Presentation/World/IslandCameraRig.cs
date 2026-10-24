using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 島を斜め上から見下ろすカメラ。ドラッグで平行移動、ピンチ (マウスはホイール) で寄り引きする。
    /// 向きは変えない (施設の正面が手前を向くように作ってあるため)。画角は <see cref="WorldCameraFramer"/> が端末に合わせる。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class IslandCameraRig : MonoBehaviour
    {
        [Tooltip("見下ろす島 (盤面の中心)")]
        [SerializeField] private Transform _island;

        [SerializeField] private float _pitch = 52f;
        [SerializeField] private float _yaw;
        [SerializeField] private float _distance = 16f;
        [SerializeField] private float _minDistance = 8f;
        [SerializeField] private float _maxDistance = 24f;

        [Tooltip("見る点を盤面の中心からどこまで動かせるか (m)")]
        [SerializeField] private float _panLimit = 5.5f;

        [Tooltip("引いたときの見る点 (盤面の中心からのずれ)。UI の下に隠れる分だけ奥へずらす")]
        [SerializeField] private Vector3 _homeOffset = new Vector3(0f, 0f, 0.4f);

        private Camera _camera;
        private Vector3 _target;
        private Vector3 _focusGoal;
        private float _distanceGoal;
        private bool _focusing;

        private Vector3 IslandOrigin => _island != null ? _island.position : Vector3.zero;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _target = _homeOffset;
            _distanceGoal = _distance;
            Apply();
        }

        private void LateUpdate()
        {
            if (_focusing)
            {
                var k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
                _target = Vector3.Lerp(_target, _focusGoal, k);
                _distance = Mathf.Lerp(_distance, _distanceGoal, k);
                if ((_target - _focusGoal).sqrMagnitude < 0.0004f && Mathf.Abs(_distance - _distanceGoal) < 0.02f)
                {
                    _focusing = false;
                }
            }

            Apply();
        }

        /// <summary>画面上の 2 点の差だけ、地面をつかんで動かす。</summary>
        public void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            if (!TryGround(fromScreen, out var a) || !TryGround(toScreen, out var b))
            {
                return;
            }

            _focusing = false;
            _target -= b - a;
            _target.y = 0f;
            ClampTarget();
            Apply();
        }

        /// <summary>1 より大きいと引く。</summary>
        public void Zoom(float factor)
        {
            _focusing = false;
            _distance = Mathf.Clamp(_distance * factor, _minDistance, _maxDistance);
            _distanceGoal = _distance;
            Apply();
        }

        /// <summary>島のローカル座標の点へ寄る。</summary>
        public void Focus(Vector3 islandLocal, float distance)
        {
            _focusGoal = new Vector3(islandLocal.x, 0f, islandLocal.z);
            _distanceGoal = Mathf.Clamp(distance, _minDistance, _maxDistance);
            _focusing = true;
        }

        /// <summary>島の全体が見える位置に戻る。</summary>
        public void Home() => Focus(_homeOffset, 16f);

        /// <summary>画面の点を通る視線。</summary>
        public Ray ScreenRay(Vector2 screen) => _camera.ScreenPointToRay(screen);

        /// <summary>画面の点から地面 (島の高さ 0) への交点。</summary>
        public bool TryGround(Vector2 screen, out Vector3 islandLocal)
        {
            var ray = ScreenRay(screen);
            var plane = new Plane(Vector3.up, IslandOrigin);
            if (plane.Raycast(ray, out var enter))
            {
                islandLocal = ray.GetPoint(enter) - IslandOrigin;
                return true;
            }

            islandLocal = default;
            return false;
        }

        private void ClampTarget()
        {
            _target.x = Mathf.Clamp(_target.x, -_panLimit, _panLimit);
            _target.z = Mathf.Clamp(_target.z, -_panLimit, _panLimit);
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var focus = IslandOrigin + _target;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
