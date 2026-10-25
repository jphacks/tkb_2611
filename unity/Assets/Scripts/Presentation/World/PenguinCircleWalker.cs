using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// ペンギンを配置位置を中心とした円周上で歩かせ、進行方向を向かせる。
    /// 位置と向きは親 Transform の空間で扱うので、親の回転・スケールをそのまま引き継ぐ。
    /// 足踏みは Animator 側のクリップが担い、ここでは移動と向きだけを動かす。
    /// </summary>
    public sealed class PenguinCircleWalker : MonoBehaviour
    {
        [Tooltip("円の半径 (親のローカル単位)")]
        [SerializeField] private float _radius = 1.2f;

        [Tooltip("円周上を進む速さ (親のローカル単位 / 秒)")]
        [SerializeField] private float _speed = 0.3f;

        [Tooltip("上から見て時計回りに歩く")]
        [SerializeField] private bool _clockwise;

        private Vector3 _center;
        private float _angle;
        private float _radiusScale = 1f;

        /// <summary>歩く速さの倍率。氷が少なくさむそうな段ではゆっくり歩く (IceWorldView が設定する)。</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>円の中心 (親のローカル座標)。</summary>
        public Vector3 Center => _center;

        private void Awake() => _center = transform.localPosition;

        /// <summary>氷山の大きさが変わったときに、円の中心と半径の倍率を差し替える。</summary>
        public void SetCircle(Vector3 center, float radiusScale)
        {
            _center = center;
            _radiusScale = Mathf.Max(0f, radiusScale);
        }

        private void Update()
        {
            var radius = _radius * _radiusScale;
            if (radius <= 0f)
            {
                return;
            }

            _angle += (_clockwise ? -1f : 1f) * _speed * SpeedMultiplier / radius * Time.deltaTime;
            _angle = Mathf.Repeat(_angle, 2f * Mathf.PI);
            transform.SetLocalPositionAndRotation(
                PointOnCircle(_center, radius, _angle),
                Quaternion.LookRotation(DirectionOnCircle(_angle, _clockwise)));
        }

        /// <summary>XZ 平面上で、+X 軸から反時計回り (上から見て) に <paramref name="angle"/> ラジアン進んだ円周上の点を返す。</summary>
        public static Vector3 PointOnCircle(Vector3 center, float radius, float angle) =>
            center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

        /// <summary><see cref="PointOnCircle"/> の点での進行方向 (単位ベクトル) を返す。</summary>
        public static Vector3 DirectionOnCircle(float angle, bool clockwise)
        {
            var counterClockwise = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            return clockwise ? -counterClockwise : counterClockwise;
        }

        private void OnDrawGizmosSelected()
        {
            var parent = transform.parent;
            var center = Application.isPlaying ? _center : transform.localPosition;
            Gizmos.matrix = parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            const int segments = 48;
            for (var i = 0; i < segments; i++)
            {
                Gizmos.DrawLine(
                    PointOnCircle(center, _radius, 2f * Mathf.PI * i / segments),
                    PointOnCircle(center, _radius, 2f * Mathf.PI * (i + 1) / segments));
            }
        }
    }
}
