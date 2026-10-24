using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 島の陸 (基礎の島と、沈んでいない開拓したマス) を、HomeWorld の氷山と同じ雪の面と層になった崖で 1 枚の氷として描く。
    /// マスの枠は、建てる・移す・広げるの操作中だけ白い線で重ねる。
    /// <list type="bullet">
    /// <item>輪郭はマスの外側へ少しはみ出させ、角を丸めて揺らぎを足す (マス目の形に見せないため)。</item>
    /// <item>色は HomeWorld の氷山 (Iceberg_With_Cabin_v001.fbx) のマテリアルを使う。面は平らな陰影で、小さな起伏の面が見えるようにする。</item>
    /// <item>次に沈むマスには、氷山の裂け目の形を出して予告する。</item>
    /// </list>
    /// 形は陸が変わったときだけ作り直す。乱数は位置から決めるので、同じ島はいつも同じ形になる。
    /// </summary>
    public sealed class IslandIceView : MonoBehaviour
    {
        /// <summary>氷山の FBX の中の 1 つの形 (メッシュと、その面ごとのマテリアル)。</summary>
        [Serializable]
        public struct Piece
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        /// <summary>島の外に置く飾り (遠くの氷山や浮かぶ氷)。</summary>
        [Serializable]
        public struct Decor
        {
            public Piece Piece;
            public Vector3 Position;
            public float Scale;
            public float Yaw;
        }

        private const int Resolution = 3;
        private const float Threshold = 0.5f;
        private const float Reach = 0.5f;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private IslandInteraction _interaction;

        [Header("氷の面と崖 (HomeWorld の氷山のマテリアル)")]
        [SerializeField] private Material _snow;
        [SerializeField] private Material _snowShade;

        [Tooltip("崖の層。上から順に (雪・霜・水色・青・水の中)")]
        [SerializeField] private Material[] _cliffBands = Array.Empty<Material>();

        [Header("沈む予告と枠")]
        [SerializeField] private Piece _fissure;
        [SerializeField] private Material _gridLine;

        [Header("島の外の飾り")]
        [SerializeField] private Decor[] _decor = Array.Empty<Decor>();

        /// <summary>崖の層の高さと、外へのせり出し (m)。</summary>
        private static readonly float[] BandDepth = { -0.03f, -0.13f, -0.3f, -0.52f, -0.95f };
        private static readonly float[] BandFlare = { 0f, 0.05f, 0.1f, 0.15f, 0.08f };

        private readonly HashSet<Cell> _land = new HashSet<Cell>();
        private readonly List<GameObject> _fissures = new List<GameObject>();
        private readonly List<Cell> _warned = new List<Cell>();
        private MeshFilter _iceFilter;
        private MeshRenderer _iceRenderer;
        private Mesh _iceMesh;
        private GameObject _grid;
        private Mesh _gridMesh;
        private int _size;

        private void Awake()
        {
            var ice = new GameObject("Ice");
            ice.transform.SetParent(transform, false);
            _iceFilter = ice.AddComponent<MeshFilter>();
            _iceRenderer = ice.AddComponent<MeshRenderer>();
            _iceMesh = new Mesh { name = "Island Ice" };
            _iceFilter.sharedMesh = _iceMesh;

            _grid = new GameObject("Grid");
            _grid.transform.SetParent(transform, false);
            _gridMesh = new Mesh { name = "Island Grid" };
            _grid.AddComponent<MeshFilter>().sharedMesh = _gridMesh;
            var gridRenderer = _grid.AddComponent<MeshRenderer>();
            gridRenderer.sharedMaterial = _gridLine;
            gridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _grid.SetActive(false);

            SpawnDecor();
        }

        private void OnEnable()
        {
            if (_game == null || _snow == null || _cliffBands.Length == 0)
            {
                Debug.LogError($"{nameof(IslandIceView)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _game.IslandChanged += Rebuild;
            if (_interaction != null)
            {
                _interaction.Changed += RefreshGrid;
            }

            Rebuild();
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.IslandChanged -= Rebuild;
            }

            if (_interaction != null)
            {
                _interaction.Changed -= RefreshGrid;
            }
        }

        private void OnDestroy()
        {
            Destroy(_iceMesh);
            Destroy(_gridMesh);
        }

        /// <summary>陸の形が変わっていれば氷を作り直し、沈む予告と枠を合わせる。</summary>
        public void Rebuild()
        {
            var island = _game.Island;
            var board = island.Board;
            var land = new HashSet<Cell>();
            for (var x = 0; x < board.Size; x++)
            {
                for (var y = 0; y < board.Size; y++)
                {
                    var c = new Cell(x, y);
                    var kind = board.KindAt(c);
                    if (kind == TileKind.Base || (kind == TileKind.Expanded && !board.IsSunk(c, island.Ice)))
                    {
                        land.Add(c);
                    }
                }
            }

            if (_size != board.Size || !land.SetEquals(_land))
            {
                _size = board.Size;
                _land.Clear();
                _land.UnionWith(land);
                BuildIce();
                BuildGrid();
            }

            RefreshFissures(board, island.Ice);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            var placing = _interaction != null && _interaction.Mode != IslandMode.Browse;
            if (_grid.activeSelf != placing)
            {
                _grid.SetActive(placing);
            }
        }

        // ------------------------------------------------------------------
        // 氷の形
        // ------------------------------------------------------------------

        /// <summary>点 p (島のローカル、xz) がどれだけ陸の内側か。0.5 が輪郭。</summary>
        private float Field(float px, float pz)
        {
            var sum = 0f;
            var half = _size * 0.5f;
            var cx0 = Mathf.FloorToInt(px + half);
            var cz0 = Mathf.FloorToInt(pz + half);
            for (var cx = cx0 - 1; cx <= cx0 + 1; cx++)
            {
                for (var cz = cz0 - 1; cz <= cz0 + 1; cz++)
                {
                    if (!_land.Contains(new Cell(cx, cz)))
                    {
                        continue;
                    }

                    // マスの正方形までの距離 (中は 0)。
                    var minX = cx - half;
                    var minZ = cz - half;
                    var dx = Mathf.Max(minX - px, 0f, px - (minX + 1f));
                    var dz = Mathf.Max(minZ - pz, 0f, pz - (minZ + 1f));
                    var d = Mathf.Sqrt(dx * dx + dz * dz);
                    var w = Mathf.Max(0f, 1f - d / Reach);
                    sum += w * w;
                }
            }

            // 輪郭を少し揺らす (マスの中は 1 以上なので、揺らしても欠けない)。
            return Mathf.Min(sum, 1.5f) + (ValueNoise(px * 1.3f, pz * 1.3f) - 0.5f) * 0.22f;
        }

        private void BuildIce()
        {
            _iceMesh.Clear();
            var top = new List<int>();
            var shade = new List<int>();
            var bands = new List<int>[_cliffBands.Length];
            for (var i = 0; i < bands.Length; i++)
            {
                bands[i] = new List<int>();
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            if (_land.Count > 0)
            {
                var step = 1f / Resolution;
                var origin = -_size * 0.5f - 1f;
                var n = (_size + 2) * Resolution;
                var values = new float[n + 1, n + 1];
                for (var i = 0; i <= n; i++)
                {
                    for (var j = 0; j <= n; j++)
                    {
                        values[i, j] = Field(origin + i * step, origin + j * step);
                    }
                }

                var poly = new List<Vector3>(8);
                var isCrossing = new List<bool>(8);
                for (var i = 0; i < n; i++)
                {
                    for (var j = 0; j < n; j++)
                    {
                        poly.Clear();
                        isCrossing.Clear();
                        // 角を順に回る: (i,j) → (i+1,j) → (i+1,j+1) → (i,j+1)。
                        for (var k = 0; k < 4; k++)
                        {
                            var (ai, aj) = Corner(i, j, k);
                            var (bi, bj) = Corner(i, j, (k + 1) % 4);
                            var va = values[ai, aj];
                            var vb = values[bi, bj];
                            if (va >= Threshold)
                            {
                                poly.Add(TopPoint(origin + ai * step, origin + aj * step, false));
                                isCrossing.Add(false);
                            }

                            if ((va >= Threshold) != (vb >= Threshold))
                            {
                                poly.Add(Crossing(origin, step, ai, aj, va, bi, bj, vb));
                                isCrossing.Add(true);
                            }
                        }

                        if (poly.Count < 3)
                        {
                            continue;
                        }

                        for (var t = 1; t < poly.Count - 1; t++)
                        {
                            var a = poly[0];
                            var b = poly[t];
                            var c = poly[t + 1];
                            var centroid = (a + b + c) / 3f;
                            var list = ValueNoise(centroid.x * 0.55f + 11f, centroid.z * 0.55f - 7f) > 0.64f ? shade : top;
                            AddTriangle(verts, uvs, list, a, b, c, Vector3.up);
                        }

                        // 輪郭: 隣り合う 2 つの交点を結ぶ辺が崖になる。
                        for (var k = 0; k < poly.Count; k++)
                        {
                            var next = (k + 1) % poly.Count;
                            if (isCrossing[k] && isCrossing[next])
                            {
                                AddCliff(verts, uvs, bands, poly[k], poly[next]);
                            }
                        }
                    }
                }
            }

            _iceMesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _iceMesh.SetVertices(verts);
            _iceMesh.SetUVs(0, uvs);
            _iceMesh.subMeshCount = 2 + bands.Length;
            _iceMesh.SetTriangles(top, 0);
            _iceMesh.SetTriangles(shade, 1);
            for (var i = 0; i < bands.Length; i++)
            {
                _iceMesh.SetTriangles(bands[i], 2 + i);
            }

            _iceMesh.RecalculateNormals();
            _iceMesh.RecalculateBounds();

            var materials = new Material[2 + _cliffBands.Length];
            materials[0] = _snow;
            materials[1] = _snowShade != null ? _snowShade : _snow;
            for (var i = 0; i < _cliffBands.Length; i++)
            {
                materials[2 + i] = _cliffBands[i];
            }

            _iceRenderer.sharedMaterials = materials;
        }

        private static (int, int) Corner(int i, int j, int k) => k switch
        {
            0 => (i, j),
            1 => (i + 1, j),
            2 => (i + 1, j + 1),
            _ => (i, j + 1),
        };

        /// <summary>辺の上で輪郭がまたぐ点。隣のマスと同じ点になるよう、添字の小さい側から求める。</summary>
        private Vector3 Crossing(float origin, float step, int ai, int aj, float va, int bi, int bj, float vb)
        {
            if (ai > bi || aj > bj)
            {
                (ai, aj, va, bi, bj, vb) = (bi, bj, vb, ai, aj, va);
            }

            var t = Mathf.Clamp01((Threshold - va) / (vb - va));
            var x = origin + Mathf.Lerp(ai, bi, t) * step;
            var z = origin + Mathf.Lerp(aj, bj, t) * step;
            return TopPoint(x, z, true);
        }

        /// <summary>雪の面の高さ。中はわずかに起伏させ、縁は少し丸く下げる。</summary>
        private static Vector3 TopPoint(float x, float z, bool rim)
        {
            var y = rim ? BandDepth[0] : -0.03f * Hash(x, z);
            return new Vector3(x, y, z);
        }

        private void AddCliff(List<Vector3> verts, List<Vector2> uvs, List<int>[] bands, Vector3 a, Vector3 b)
        {
            for (var band = 0; band < bands.Length && band < BandDepth.Length - 1; band++)
            {
                var a0 = CliffPoint(a, band);
                var b0 = CliffPoint(b, band);
                var a1 = CliffPoint(a, band + 1);
                var b1 = CliffPoint(b, band + 1);
                var outward = Outward(a.x, a.z) + Outward(b.x, b.z);
                AddTriangle(verts, uvs, bands[band], a0, b0, b1, outward);
                AddTriangle(verts, uvs, bands[band], a0, b1, a1, outward);
            }
        }

        /// <summary>崖の層の点。同じ輪郭の点からはいつも同じ点になるので、隣の崖と隙間ができない。</summary>
        private Vector3 CliffPoint(Vector3 rim, int band)
        {
            if (band == 0)
            {
                return rim;
            }

            var o = Outward(rim.x, rim.z);
            var jitter = 0.7f + 0.6f * Hash(rim.x * 3.1f + band, rim.z * 2.7f - band);
            var p = rim + o * (BandFlare[band] * jitter);
            p.y = BandDepth[band] - 0.04f * Hash(rim.x + band * 5f, rim.z);
            return p;
        }

        /// <summary>輪郭の外向き (値が下がる向き)。</summary>
        private Vector3 Outward(float x, float z)
        {
            const float e = 0.05f;
            var gx = Field(x + e, z) - Field(x - e, z);
            var gz = Field(x, z + e) - Field(x, z - e);
            var g = new Vector3(-gx, 0f, -gz);
            return g.sqrMagnitude > 1e-8f ? g.normalized : Vector3.zero;
        }

        /// <summary>
        /// 三角形を 1 つ足す。頂点は共有せず、面ごとに平らな陰影にする (HomeWorld の氷山と同じ低ポリの見た目)。
        /// <paramref name="facing"/> の向きから見て表になるよう、巻き方をそろえる。
        /// </summary>
        private static void AddTriangle(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            uvs.Add(new Vector2(a.x, a.z) * 0.25f);
            uvs.Add(new Vector2(b.x, b.z) * 0.25f);
            uvs.Add(new Vector2(c.x, c.z) * 0.25f);
            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
        }

        // ------------------------------------------------------------------
        // 沈む予告・枠・飾り
        // ------------------------------------------------------------------

        private void RefreshFissures(IslandBoard board, double ice)
        {
            var warned = new List<Cell>();
            for (var x = 0; x < board.Size; x++)
            {
                for (var y = 0; y < board.Size; y++)
                {
                    var c = new Cell(x, y);
                    if (board.KindAt(c) == TileKind.Expanded && board.IsWarned(c, ice))
                    {
                        warned.Add(c);
                    }
                }
            }

            if (warned.Count == _warned.Count && warned.TrueForAll(_warned.Contains))
            {
                return;
            }

            _warned.Clear();
            _warned.AddRange(warned);
            foreach (var f in _fissures)
            {
                Destroy(f);
            }

            _fissures.Clear();
            if (_fissure.Mesh == null)
            {
                return;
            }

            var capacity = board.Capacity(ice);
            foreach (var c in warned)
            {
                // 次に沈むマスほど裂け目を大きくする。
                var next = board.ExpansionIndex(c) == capacity - 1;
                var center = IslandGrid.CellCenter(c, board.Size) + Vector3.up * 0.004f;
                var yaw = Hash(c.X * 1.7f, c.Y * 2.3f) * 360f;
                var go = Place(_fissure, center, next ? 0.75f : 0.5f, yaw, "Fissure");
                IslandPop.Play(go.transform);
                _fissures.Add(go);
            }
        }

        private void BuildGrid()
        {
            _gridMesh.Clear();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float w = 0.025f;
            const float y = 0.012f;
            foreach (var c in _land)
            {
                var p = IslandGrid.CellCenter(c, _size);
                var inset = 0.04f;
                var min = new Vector3(p.x - 0.5f + inset, y, p.z - 0.5f + inset);
                var max = new Vector3(p.x + 0.5f - inset, y, p.z + 0.5f - inset);
                AddLine(verts, tris, new Vector3(min.x, y, min.z), new Vector3(max.x, y, min.z + w));
                AddLine(verts, tris, new Vector3(min.x, y, max.z - w), new Vector3(max.x, y, max.z));
                AddLine(verts, tris, new Vector3(min.x, y, min.z), new Vector3(min.x + w, y, max.z));
                AddLine(verts, tris, new Vector3(max.x - w, y, min.z), new Vector3(max.x, y, max.z));
            }

            _gridMesh.SetVertices(verts);
            _gridMesh.SetTriangles(tris, 0);
            _gridMesh.RecalculateNormals();
            _gridMesh.RecalculateBounds();
        }

        /// <summary>xz の長方形 (min〜max) を上向きの 2 枚の三角形で足す。</summary>
        private static void AddLine(List<Vector3> verts, List<int> tris, Vector3 min, Vector3 max)
        {
            var s = verts.Count;
            verts.Add(new Vector3(min.x, min.y, min.z));
            verts.Add(new Vector3(min.x, min.y, max.z));
            verts.Add(new Vector3(max.x, min.y, max.z));
            verts.Add(new Vector3(max.x, min.y, min.z));
            tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }

        private void SpawnDecor()
        {
            var root = new GameObject("Decor").transform;
            root.SetParent(transform, false);
            foreach (var d in _decor)
            {
                if (d.Piece.Mesh != null)
                {
                    Place(d.Piece, d.Position, d.Scale, d.Yaw, d.Piece.Mesh.name).transform.SetParent(root, true);
                }
            }
        }

        /// <summary>
        /// 氷山の FBX の形を 1 つ置く。FBX の中では氷山全体の座標のまま入っているので、
        /// 底の中心が <paramref name="position"/> に来るようにずらす (FBX は cm 単位なので 100 倍)。
        /// </summary>
        private GameObject Place(Piece piece, Vector3 position, float scale, float yaw, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var child = new GameObject("Mesh");
            child.transform.SetParent(root.transform, false);
            var b = piece.Mesh.bounds;
            var unit = 100f * scale;
            child.transform.localScale = Vector3.one * unit;
            child.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            // FBX の軸 (Z が上) を Unity に合わせたあとの、底の中心。
            var bottom = child.transform.localRotation * new Vector3(b.center.x, b.center.y, b.min.z) * unit;
            child.transform.localPosition = -bottom;
            child.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
            child.AddComponent<MeshRenderer>().sharedMaterials = piece.Materials;
            return root;
        }

        // ------------------------------------------------------------------
        // 位置から決まる乱数
        // ------------------------------------------------------------------

        private static float Hash(float x, float z)
        {
            var h = Mathf.Sin(x * 127.1f + z * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static float ValueNoise(float x, float z)
        {
            var ix = Mathf.Floor(x);
            var iz = Mathf.Floor(z);
            var fx = x - ix;
            var fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            var a = Hash(ix, iz);
            var b = Hash(ix + 1f, iz);
            var c = Hash(ix, iz + 1f);
            var d = Hash(ix + 1f, iz + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
    }
}
