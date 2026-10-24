using ProjectPenguin.Domain.World;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// ホームの 3D の世界をゲームの状態に合わせる (15c §1.3・§1.4)。
    /// <list type="bullet">
    /// <item>氷山の水平の大きさを √(氷/100) 倍にする。小屋とペンギンの大きさは変えない。</item>
    /// <item>段に応じてひびの本数を変える。</item>
    /// <item>来たペンギンの数だけ、段ごとの上限まで氷の上に出す。残りは小屋で休んでいる扱いで隠す。</item>
    /// <item>さむそうな段では歩く速さを落とす。</item>
    /// </list>
    /// 計算は <see cref="IceVisual"/> が持ち、ここは Transform に当てるだけ。
    /// </summary>
    public sealed class IceWorldView : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;

        [Tooltip("水平に伸び縮みさせる氷山 (小屋ごと)")]
        [SerializeField] private Transform _iceberg;

        [Tooltip("氷山の子にある小屋。氷山に合わせて縮まないよう逆に拡大する")]
        [SerializeField] private Transform _cabin;

        [Tooltip("並び順に出していくペンギン")]
        [SerializeField] private Transform[] _penguins;

        [Tooltip("段が下がると順に出すひび")]
        [SerializeField] private GameObject[] _cracks;

        private Vector3 _icebergScale;
        private Vector3 _cabinScale;
        private Vector3[] _penguinPositions;

        private void Awake()
        {
            _icebergScale = _iceberg != null ? _iceberg.localScale : Vector3.one;
            _cabinScale = _cabin != null ? _cabin.localScale : Vector3.one;
            _penguinPositions = new Vector3[_penguins?.Length ?? 0];
            for (var i = 0; i < _penguinPositions.Length; i++)
            {
                // Awake の時点ではまだ歩き出していないので、配置位置が円の中心になる。
                _penguinPositions[i] = _penguins[i].position;
            }
        }

        private void OnEnable()
        {
            if (_game == null || _iceberg == null)
            {
                Debug.LogError($"{nameof(IceWorldView)}: {nameof(GameStateSystem)} か氷山が未設定です。", this);
                enabled = false;
                return;
            }

            _game.Changed += OnChanged;
            Apply(_game.Current);
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.Changed -= OnChanged;
            }
        }

        private void OnChanged(GameStateChange change) => Apply(change.After);

        private void Apply(HomeState state)
        {
            var scale = (float)IceVisual.AreaScale(state.Ice);
            _iceberg.localScale = new Vector3(_icebergScale.x * scale, _icebergScale.y, _icebergScale.z * scale);
            if (_cabin != null)
            {
                _cabin.localScale = new Vector3(_cabinScale.x / scale, _cabinScale.y, _cabinScale.z / scale);
            }

            ApplyPenguins(state, scale);
            ApplyCracks(state.Tier);
        }

        private void ApplyPenguins(HomeState state, float scale)
        {
            if (_penguins == null)
            {
                return;
            }

            var onIce = IceVisual.PenguinsOnIce(state.Tier, state.Penguins);
            var speed = (float)IceVisual.WalkSpeedMultiplier(state.Tier);
            var center = _iceberg.position;
            for (var i = 0; i < _penguins.Length; i++)
            {
                var penguin = _penguins[i];
                penguin.gameObject.SetActive(i < onIce);

                // 氷山が縮んでも海に落ちないよう、氷山の中心に向かって同じ割合で寄せる。
                var world = ScaleAround(center, _penguinPositions[i], scale);
                var local = penguin.parent != null ? penguin.parent.InverseTransformPoint(world) : world;
                var walker = penguin.GetComponent<PenguinCircleWalker>();
                if (walker != null)
                {
                    walker.SetCircle(local, scale);
                    walker.SpeedMultiplier = speed;
                }
                else
                {
                    penguin.localPosition = local;
                }
            }
        }

        private void ApplyCracks(int tier)
        {
            if (_cracks == null)
            {
                return;
            }

            var count = IceVisual.CrackCount(tier);
            for (var i = 0; i < _cracks.Length; i++)
            {
                _cracks[i].SetActive(i < count);
            }
        }

        /// <summary><paramref name="center"/> を中心に、水平方向 (XZ) だけ <paramref name="scale"/> 倍した点。高さは変えない。</summary>
        public static Vector3 ScaleAround(Vector3 center, Vector3 point, float scale) =>
            new Vector3(center.x + (point.x - center.x) * scale, point.y, center.z + (point.z - center.z) * scale);
    }
}
