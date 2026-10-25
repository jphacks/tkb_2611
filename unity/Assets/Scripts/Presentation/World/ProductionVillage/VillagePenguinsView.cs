using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 村の空き地と島の縁をペンギンに散歩させる。施設の無いところを選んで歩き、着いたら少し立ち止まる。
    /// 見せるだけで、村の状態は変えない。
    /// </summary>
    public sealed class VillagePenguinsView : MonoBehaviour
    {
        private sealed class Walker
        {
            public Transform Transform;
            public Vector3 Target;
            public float Wait;
            public Animator Animator;
        }

        /// <summary>立ち止まっている間の足踏みの速さ (歩いている間は 1)。</summary>
        private const float IdleWaddleSpeed = .35f;

        [SerializeField] private ProductionVillageSession _session;

        [Tooltip("歩き方付きのペンギン (複製して使う。元は隠したままにする)")]
        [SerializeField] private GameObject _template;

        [SerializeField] private int _count = 6;

        [Tooltip("ペンギンの背の高さ (ワールド単位)")]
        [SerializeField] private float _height = .32f;

        [SerializeField] private float _speed = .35f;

        private readonly List<Walker> _walkers = new List<Walker>();

        // 施設の配置は毎フレームは変わらないので、村の表示用データは間をあけて取り直す (取るたびに盤面を複製するため)。
        private VillageView _view;
        private float _nextViewRefresh;

        private void Start()
        {
            if (_session == null || _template == null)
            {
                Debug.LogError($"{nameof(VillagePenguinsView)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            for (var i = 0; i < _count; i++)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            var holder = new GameObject("VillagePenguin").transform;
            holder.SetParent(transform, false);
            var go = Instantiate(_template, holder);
            foreach (var walker in go.GetComponentsInChildren<PenguinCircleWalker>(true))
            {
                Destroy(walker);
            }

            go.SetActive(true);
            go.transform.localPosition = Vector3.zero;
            // 歩き方のアニメーションが本体の大きさを書き換えるので、大きさは外側の入れ物に掛ける。
            if (RestBounds(go, out var bounds))
            {
                go.transform.localPosition -= holder.InverseTransformVector(new Vector3(bounds.center.x - holder.position.x, bounds.min.y - holder.position.y, bounds.center.z - holder.position.z));
                holder.localScale = Vector3.one * (_height / Mathf.Max(.001f, bounds.size.y));
            }

            var start = RandomFreePoint();
            holder.localPosition = start;
            holder.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            _walkers.Add(new Walker { Transform = holder, Target = start, Wait = Random.Range(0f, 2f), Animator = go.GetComponentInChildren<Animator>() });
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextViewRefresh)
            {
                _nextViewRefresh = Time.unscaledTime + .5f;
                _view = _session.Core.View;
            }

            foreach (var w in _walkers)
            {
                var t = w.Transform;
                if (w.Wait > 0)
                {
                    w.Wait -= Time.deltaTime;
                    SetWaddle(w, IdleWaddleSpeed);
                    continue;
                }

                var to = w.Target - t.localPosition;
                to.y = 0;
                if (to.magnitude < .05f || Blocked(w.Target))
                {
                    w.Target = RandomFreePoint();
                    w.Wait = Random.Range(.8f, 3f);
                    continue;
                }

                SetWaddle(w, 1);
                t.localPosition += to.normalized * Mathf.Min(to.magnitude, _speed * Time.deltaTime);
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                t.localRotation = Quaternion.Slerp(t.localRotation, look, 1 - Mathf.Exp(-8 * Time.deltaTime));
            }
        }

        /// <summary>
        /// 立ち姿のときの大きさ (ワールド座標)。スキンメッシュの <c>Renderer.bounds</c> は描くまで古い値のままなので、
        /// メッシュそのものの範囲を Transform で移して測る。
        /// </summary>
        private static bool RestBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                var local = mesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var world = r.transform.TransformPoint(corner);
                    if (!found)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                }
            }

            return found;
        }

        private static void SetWaddle(Walker w, float speed)
        {
            if (w.Animator != null)
            {
                w.Animator.speed = speed;
            }
        }

        /// <summary>施設の足元 (1 マスの余白込み) に入っているか。</summary>
        private bool Blocked(Vector3 p)
        {
            var view = _view ??= _session.Core.View;
            if (view == null)
            {
                return false;
            }

            var x = p.x / VillageWorldRenderer.Cell + 12;
            var y = p.z / VillageWorldRenderer.Cell + 12;
            return view.Nodes.Any(n => x > n.X - .6f && x < n.X + n.Size + .6f && y > n.Y - .6f && y < n.Y + n.Size + .6f);
        }

        /// <summary>盤面の空き地か、盤面の外の縁取りのどこか。</summary>
        private Vector3 RandomFreePoint()
        {
            var half = VillageWorldRenderer.Cell * 12;
            for (var i = 0; i < 24; i++)
            {
                var edge = half + VillageIslandView.Margin * .45f;
                var p = Random.value < .35f
                    ? EdgePoint(edge)
                    : new Vector3(Random.Range(-half, half), 0, Random.Range(-half, half));
                if (!Blocked(p))
                {
                    return p;
                }
            }

            return EdgePoint(half + VillageIslandView.Margin * .45f);
        }

        /// <summary>盤面を囲む正方形の縁の上の点。島の角は丸いので、角の近くは避ける。</summary>
        private static Vector3 EdgePoint(float edge)
        {
            var along = Random.Range(-edge * .85f, edge * .85f);
            return Random.Range(0, 4) switch
            {
                0 => new Vector3(along, 0, -edge),
                1 => new Vector3(along, 0, edge),
                2 => new Vector3(-edge, 0, along),
                _ => new Vector3(edge, 0, along),
            };
        }
    }
}
