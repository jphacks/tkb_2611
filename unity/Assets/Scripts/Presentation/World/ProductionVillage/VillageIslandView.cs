using System.Collections.Generic;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 村の盤面を載せる氷の島。盤面より一回り大きい角の丸い島を起動時に組み立て、縁には厚みのある崖を付ける。
    /// 色は施設と同じ色見本 (ProductionIsland の Running.mat) のマスを UV で指して塗る。
    /// 盤面の外の縁取り (<see cref="Margin"/>) は施設を置けない帯で、ペンギンの散歩道にもなる。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VillageIslandView : MonoBehaviour
    {
        /// <summary>盤面の端から島の縁までの幅。</summary>
        public const float Margin = .8f;

        /// <summary>角の丸みの半径。</summary>
        public const float CornerRadius = 2.4f;

        /// <summary>縁の崖の深さ。海面 (y = -0.35) より下まで伸ばし、島が浮いて見えないようにする。</summary>
        public const float Depth = 1.2f;

        private const int OutlinePoints = 128;

        // 色見本 (4×4 マス) の中心の UV。上面は淡い氷 (真っ白だと光で飛ぶ)、面取りは雪の白で縁を光らせ、崖は水色、根元は青。
        private static readonly Vector2 SnowUv = new Vector2(.125f, .125f);
        private static readonly Vector2 BevelUv = new Vector2(.625f, .375f);
        private static readonly Vector2 CliffUv = new Vector2(.375f, .125f);
        private static readonly Vector2 BaseUv = new Vector2(.625f, .125f);

        [Tooltip("施設と同じ色見本の材質 (ProductionIsland_v002/Running.mat)")]
        [SerializeField] private Material _material;

        /// <summary>島の縁 (上面の外周) の点。盤面の中心が原点、y = 0。</summary>
        public IReadOnlyList<Vector3> Outline { get; private set; }

        private void Awake()
        {
            var half = VillageWorldRenderer.Cell * 12;
            Outline = OutlineOf(half);
            var mesh = BuildMesh(Outline);
            mesh.name = "VillageIsland";
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            if (_material != null)
            {
                renderer.sharedMaterial = _material;
            }
            else
            {
                Debug.LogError($"{nameof(VillageIslandView)}: 材質が未設定です。", this);
            }
        }

        /// <summary>
        /// 盤面 (一辺 <paramref name="boardHalf"/>×2) を覆う、角の丸い島の縁。縁は少しだけ外へ揺らし、手作りの島らしくする。
        /// 揺らしは外向きだけなので、盤面の四隅も必ず島の内側に入る。
        /// </summary>
        public static Vector3[] OutlineOf(float boardHalf)
        {
            var half = boardHalf + Margin;
            var corner = half - CornerRadius;
            var points = new Vector3[OutlinePoints];
            for (var i = 0; i < OutlinePoints; i++)
            {
                var angle = i * Mathf.PI * 2 / OutlinePoints;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = RoundedSquare(direction, half, corner);
                var wobble = .16f * (1 + Mathf.Sin(angle * 5 + .7f)) * .5f + .1f * (1 + Mathf.Sin(angle * 11 + 2.1f)) * .5f;
                points[i] = p + direction * wobble;
            }

            return points;
        }

        /// <summary>中心から <paramref name="direction"/> へ伸ばした線が、角の丸い正方形の縁とぶつかる点。</summary>
        private static Vector3 RoundedSquare(Vector3 direction, float half, float corner)
        {
            // 外側から二分探索で縁を探す (角の丸い正方形は中心から見て星形なので 1 点で交わる)。
            float inside = 0, outside = half * 2;
            for (var i = 0; i < 24; i++)
            {
                var mid = (inside + outside) * .5f;
                var p = direction * mid;
                var q = new Vector2(Mathf.Max(Mathf.Abs(p.x) - corner, 0), Mathf.Max(Mathf.Abs(p.z) - corner, 0));
                if (q.magnitude <= half - corner)
                {
                    inside = mid;
                }
                else
                {
                    outside = mid;
                }
            }

            return direction * inside;
        }

        /// <summary>上面 (扇形)・面取り・崖・根元の 4 段を、面ごとに頂点を分けたローポリの形で作る。</summary>
        public static Mesh BuildMesh(IReadOnlyList<Vector3> outline)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // 上面: 中心からの扇形。法線は真上。
            vertices.Add(Vector3.zero);
            uvs.Add(SnowUv);
            foreach (var p in outline)
            {
                vertices.Add(p);
                uvs.Add(SnowUv);
            }

            for (var i = 0; i < outline.Count; i++)
            {
                triangles.Add(0);
                triangles.Add(1 + (i + 1) % outline.Count);
                triangles.Add(1 + i);
            }

            // 縁の段: 上の輪 → 面取り → 崖の下 → 海の中の根元
            var rings = new[]
            {
                (Push: 0f, Y: 0f),
                (Push: .2f, Y: -.16f),
                (Push: .34f, Y: -.62f),
                (Push: .5f, Y: -Depth),
            };
            var bandUvs = new[] { BevelUv, CliffUv, BaseUv };
            for (var band = 0; band < bandUvs.Length; band++)
            {
                for (var i = 0; i < outline.Count; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Count];
                    var top = rings[band];
                    var bottom = rings[band + 1];
                    var start = vertices.Count;
                    vertices.Add(Ring(a, top.Push, top.Y));
                    vertices.Add(Ring(b, top.Push, top.Y));
                    vertices.Add(Ring(b, bottom.Push, bottom.Y));
                    vertices.Add(Ring(a, bottom.Push, bottom.Y));
                    for (var k = 0; k < 4; k++)
                    {
                        uvs.Add(bandUvs[band]);
                    }

                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Ring(Vector3 edge, float push, float y)
        {
            var outward = new Vector3(edge.x, 0, edge.z).normalized;
            return edge + outward * push + Vector3.up * y;
        }
    }
}
