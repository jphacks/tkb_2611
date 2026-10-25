using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 端末の縦横比が変わっても、3D ワールドの左右に見える範囲を基準解像度のときと同じに保つ。
    /// Unity の fieldOfView は垂直方向なので、そのままだと縦長の端末ほど左右が切れる。
    /// UI (PanelSettings の Match = 0) と同じく幅を基準にし、基準の縦横比での水平画角を実際の縦横比でも再現する。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class WorldCameraFramer : MonoBehaviour
    {
        [Tooltip("PanelSettings の Reference Resolution と揃える")]
        [SerializeField] private Vector2 _referenceResolution = new(1080f, 1920f);

        [Tooltip("基準解像度のときの垂直方向の画角 (度)")]
        [SerializeField] private float _referenceVerticalFov = 50f;

        private Camera _camera;
        private float _appliedAspect;

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnEnable() => Apply();

        // aspect の比較だけなら毎フレームでも軽い。回転・分割画面・解像度変更をまとめて拾える。
        private void Update()
        {
            if (!Mathf.Approximately(_camera.aspect, _appliedAspect))
            {
                Apply();
            }
        }

        private void Apply()
        {
            _appliedAspect = _camera.aspect;
            _camera.fieldOfView = VerticalFovFor(_referenceVerticalFov, _referenceResolution.x / _referenceResolution.y, _appliedAspect);
        }

        /// <summary>
        /// 基準の縦横比 <paramref name="referenceAspect"/> で垂直画角 <paramref name="referenceVerticalFov"/> のときと
        /// 同じ水平画角になる、縦横比 <paramref name="aspect"/> での垂直画角を返す。縦横比は幅 / 高さ。
        /// </summary>
        public static float VerticalFovFor(float referenceVerticalFov, float referenceAspect, float aspect)
        {
            var halfHorizontalTan = Mathf.Tan(referenceVerticalFov * 0.5f * Mathf.Deg2Rad) * referenceAspect;
            return 2f * Mathf.Atan(halfHorizontalTan / aspect) * Mathf.Rad2Deg;
        }
    }
}
