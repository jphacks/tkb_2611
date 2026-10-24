using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>盤面のマスとワールド座標の変換。1 マス = 1m、盤面の中心がこのオブジェクトの原点。</summary>
    public static class IslandGrid
    {
        public static Vector3 CellCenter(Cell c, int size) => new Vector3(c.X - (size - 1) * 0.5f, 0f, c.Y - (size - 1) * 0.5f);

        /// <summary>施設の足元の中心 (2×2 なら 4 マスの真ん中)。</summary>
        public static Vector3 FootprintCenter(Cell origin, int footprint, int size) =>
            CellCenter(origin, size) + new Vector3((footprint - 1) * 0.5f, 0f, (footprint - 1) * 0.5f);

        public static Cell CellAt(Vector3 local, int size) =>
            new Cell(Mathf.FloorToInt(local.x + size * 0.5f), Mathf.FloorToInt(local.z + size * 0.5f));
    }

    /// <summary>
    /// 生産の島 (<see cref="IslandGame"/>) を 3D で描く。マス・施設・港・質の良いマスの印を状態に合わせて差し替え、
    /// 出来事 (建てた・作った・沈んだ…) に合わせて演出を出す。計算は Domain に任せ、ここは見せるだけ。
    /// </summary>
    public sealed class IslandBoardView : MonoBehaviour
    {
        [Serializable]
        public struct FacilityPrefab
        {
            public FacilityKind Kind;
            public GameObject Prefab;
        }

        /// <summary>マスの見た目の種類。</summary>
        private enum TileLook
        {
            None,
            Base,
            Expanded,
            CrackSmall,
            CrackLarge,
            Sunk,
            Sea,
            SeaExpandable,
            Canal,
        }

        private sealed class TileSlot
        {
            public TileLook Look;
            public GameObject Instance;
        }

        private sealed class FacilitySlot
        {
            public FacilityKind Kind;
            public Cell Origin;
            public GameObject Instance;
            public FacilityProductionCycle Cycle;
            public KamabokoProductionCycle Kamaboko;
            public bool Running;
            public float SinkDepth;
            public float LastPlusOne;
        }

        [SerializeField] private GameStateSystem _game;

        [Header("マス")]
        [Tooltip("陸のマスをタイルで描くか。切ると陸は IslandIceView が 1 枚の氷として描き、ここでは水路・沈んだマス・開拓できる海だけを描く")]
        [SerializeField] private bool _drawLandTiles;

        [Tooltip("陸を氷で描くときの水面の高さ (m)。氷の崖が見えるよう、陸 (0) より下げる。沈んだマス・開拓できる海・港はこの高さに置く")]
        [SerializeField] private float _waterLevel = -0.35f;

        [SerializeField] private GameObject _baseTile;
        [SerializeField] private GameObject _expandedTile;
        [SerializeField] private GameObject _crackSmallTile;
        [SerializeField] private GameObject _crackLargeTile;
        [SerializeField] private GameObject _sinkingTile;
        [SerializeField] private GameObject _seaTile;
        [SerializeField] private GameObject _seaExpandableTile;
        [SerializeField] private GameObject _canalTile;

        [Header("質の良いマスのエリア (魚・塩の代替鉱石・海藻)")]
        [SerializeField] private GameObject _qualityFish;
        [SerializeField] private GameObject _qualitySalt;
        [SerializeField] private GameObject _qualityKelp;

        [Header("施設と港")]
        [SerializeField] private FacilityPrefab[] _facilityPrefabs = Array.Empty<FacilityPrefab>();

        [Tooltip("港の 6 段階 (予定地〜おでん祭り)")]
        [SerializeField] private GameObject[] _harborStages = Array.Empty<GameObject>();

        [Tooltip("港を陸岸に接続する位置。氷山の子にすると岸の伸縮にも追従する。未設定なら従来の水路に置く")]
        [SerializeField] private Transform _harborAnchor;

        [Header("演出")]
        [SerializeField] private GameObject _buildVfxSmall;
        [SerializeField] private GameObject _buildVfxLarge;
        [SerializeField] private GameObject _sinkVfx;
        [SerializeField] private GameObject _plusOneVfx;
        [SerializeField] private GameObject _confettiVfx;
        [SerializeField] private GameObject _pearlVfx;

        [Tooltip("沈んだ施設が水に沈む深さ (m)")]
        [SerializeField] private float _submergedDepth = 0.55f;

        [Tooltip("同じ施設の「+1」を出す最短の間隔 (秒)")]
        [SerializeField] private float _plusOneInterval = 1.6f;

        private readonly Dictionary<Cell, TileSlot> _tiles = new Dictionary<Cell, TileSlot>();
        private readonly Dictionary<int, FacilitySlot> _facilities = new Dictionary<int, FacilitySlot>();
        private readonly Dictionary<Cell, GameObject> _markers = new Dictionary<Cell, GameObject>();
        private Transform _tileRoot;
        private Transform _facilityRoot;
        private Transform _fxRoot;
        private GameObject _harbor;
        private int _harborStage = -1;
        private bool _showExpandable;

        public int BoardSize => _game != null ? _game.Island.Board.Size : 10;

        /// <summary>水のマスと港を置く高さ。陸をタイルで描くときは陸と同じ 0。</summary>
        private float WaterY => _drawLandTiles ? 0f : _waterLevel;

        /// <summary>開拓できる海を光らせるか (開拓の操作中)。</summary>
        public bool ShowExpandable
        {
            get => _showExpandable;
            set
            {
                if (_showExpandable == value)
                {
                    return;
                }

                _showExpandable = value;
                Rebuild();
            }
        }

        public bool HasHarborAnchor => _harborAnchor != null;

        /// <summary>港の足元の中心 (ローカル)。配置点がなければ水路の真ん中。</summary>
        public Vector3 HarborCenter
        {
            get
            {
                if (_harborAnchor != null) return transform.InverseTransformPoint(_harborAnchor.position);
                var size = BoardSize;
                return IslandGrid.FootprintCenter(new Cell(size / 2 - 1, size - 2), 2, size);
            }
        }

        private void Awake()
        {
            _tileRoot = CreateRoot("Tiles");
            _facilityRoot = CreateRoot("Facilities");
            _fxRoot = CreateRoot("Effects");
        }

        private void OnEnable()
        {
            if (_game == null)
            {
                Debug.LogError($"{nameof(IslandBoardView)}: {nameof(GameStateSystem)} が未設定です。", this);
                enabled = false;
                return;
            }

            _game.IslandChanged += Rebuild;
            _game.IslandEvents += OnEvents;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.IslandChanged -= Rebuild;
                _game.IslandEvents -= OnEvents;
            }
        }

        private void Update()
        {
            // 沈む・浮かぶは一瞬で切り替えず、ゆっくり動かす。
            foreach (var slot in _facilities.Values)
            {
                var t = slot.Instance.transform;
                var target = IslandGrid.FootprintCenter(slot.Origin, IslandCatalog.Get(slot.Kind).Size, BoardSize) +
                             Vector3.down * slot.SinkDepth;
                t.localPosition = Vector3.Lerp(t.localPosition, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
        }

        private void LateUpdate()
        {
            if (_harbor != null && _harborAnchor != null)
            {
                // 氷山の伸縮で岸が動いても、港自体の大きさは変えない。
                _harbor.transform.SetPositionAndRotation(_harborAnchor.position, _harborAnchor.rotation);
            }
        }

        /// <summary>移動・回転した港の実際の当たり判定を拾う。</summary>
        public bool TryHarbor(Ray ray)
        {
            if (_harbor == null || !_harbor.activeInHierarchy) return false;
            foreach (var collider in _harbor.GetComponentsInChildren<Collider>())
            {
                if (collider.enabled && collider.Raycast(ray, out _, float.PositiveInfinity)) return true;
            }

            return false;
        }

        /// <summary>施設の見た目 (選んだときの強調などに使う)。</summary>
        public GameObject FacilityObject(int id) => _facilities.TryGetValue(id, out var s) ? s.Instance : null;

        public GameObject PrefabOf(FacilityKind kind) => _facilityPrefabs.FirstOrDefault(p => p.Kind == kind).Prefab;

        /// <summary>表示中の資源エリアを直接調べる。水面の印も、陸と同じ高さへ投影せずに拾う。</summary>
        public bool TryQualityArea(Ray ray, out Cell cell)
        {
            cell = default;
            var distance = float.PositiveInfinity;
            var found = false;
            foreach (var pair in _markers)
            {
                var marker = pair.Value;
                if (marker == null || !marker.activeInHierarchy) continue;
                var collider = marker.GetComponent<Collider>();
                if (collider == null || !collider.enabled || !collider.Raycast(ray, out var hit, distance)) continue;
                distance = hit.distance;
                cell = pair.Key;
                found = true;
            }

            return found;
        }

        /// <summary>今の状態に合わせて全体を描き直す (変わったものだけ作り直す)。</summary>
        public void Rebuild()
        {
            if (_game == null || _tileRoot == null)
            {
                return;
            }

            var island = _game.Island;
            RebuildTiles(island);
            RebuildFacilities(island);
            RebuildHarbor(island);
        }

        private void RebuildTiles(IslandGame island)
        {
            var board = island.Board;
            var expandable = _showExpandable && island.ExpandRoom > 0 && board.SunkCount(island.Ice) == 0
                ? new HashSet<Cell>(board.ExpandableCells(island.Ice))
                : new HashSet<Cell>();
            var cap = board.Capacity(island.Ice);
            for (var x = 0; x < board.Size; x++)
            {
                for (var y = 0; y < board.Size; y++)
                {
                    var c = new Cell(x, y);
                    var look = LookOf(board, c, island.Ice, cap, expandable);
                    // 氷で描くときは、陸は IslandIceView に任せ、水路は海の板をそのまま見せる。
                    if (!_drawLandTiles && (IsLand(look) || look == TileLook.Canal))
                    {
                        look = TileLook.None;
                    }

                    SetTile(c, look);
                    SetMarker(c, board.QualityAt(c), board.IsSolidLand(c, island.Ice));
                }
            }
        }

        private static bool IsLand(TileLook look) =>
            look == TileLook.Base || look == TileLook.Expanded || look == TileLook.CrackSmall || look == TileLook.CrackLarge;

        private static TileLook LookOf(IslandBoard board, Cell c, double ice, int capacity, HashSet<Cell> expandable)
        {
            switch (board.KindAt(c))
            {
                case TileKind.Base:
                    return TileLook.Base;
                case TileKind.Canal:
                    return TileLook.Canal;
                case TileKind.Expanded:
                    if (board.IsSunk(c, ice))
                    {
                        return TileLook.Sunk;
                    }

                    if (board.IsWarned(c, ice))
                    {
                        // 次に沈むマスほどひびを大きくする。
                        return board.ExpansionIndex(c) == capacity - 1 ? TileLook.CrackLarge : TileLook.CrackSmall;
                    }

                    return TileLook.Expanded;
                default:
                    return expandable.Contains(c) ? TileLook.SeaExpandable : TileLook.Sea;
            }
        }

        private void SetTile(Cell c, TileLook look)
        {
            if (!_tiles.TryGetValue(c, out var slot))
            {
                slot = new TileSlot();
                _tiles[c] = slot;
            }

            if (slot.Look == look)
            {
                return;
            }

            var appeared = slot.Look == TileLook.Sea || slot.Look == TileLook.SeaExpandable || slot.Look == TileLook.Sunk;
            if (slot.Instance != null)
            {
                Destroy(slot.Instance);
            }

            slot.Look = look;
            var prefab = PrefabOf(look);
            if (prefab == null)
            {
                slot.Instance = null;
                return;
            }

            var pos = IslandGrid.CellCenter(c, BoardSize);
            if (!IsLand(look))
            {
                pos.y = WaterY;
            }

            if (look == TileLook.Sunk)
            {
                pos += Vector3.down * (_drawLandTiles ? 0.06f : 0.01f);
            }

            slot.Instance = Instantiate(prefab, _tileRoot);
            slot.Instance.transform.localPosition = pos;
            slot.Instance.name = $"{look} {c}";
            if (appeared && (look == TileLook.Expanded || look == TileLook.CrackSmall || look == TileLook.CrackLarge))
            {
                IslandPop.Play(slot.Instance.transform);
            }
        }

        private GameObject PrefabOf(TileLook look) => look switch
        {
            TileLook.Base => _baseTile,
            TileLook.Expanded => _expandedTile,
            TileLook.CrackSmall => _crackSmallTile != null ? _crackSmallTile : _expandedTile,
            TileLook.CrackLarge => _crackLargeTile != null ? _crackLargeTile : _expandedTile,
            TileLook.Sunk => _sinkingTile,
            TileLook.Sea => _seaTile,
            TileLook.SeaExpandable => _seaExpandableTile != null ? _seaExpandableTile : _seaTile,
            TileLook.Canal => _canalTile,
            _ => null,
        };

        private void SetMarker(Cell c, Item? quality, bool onLand)
        {
            var exists = _markers.TryGetValue(c, out var marker) && marker != null;
            if (exists)
            {
                // 開拓されたり沈んだりしたら、陸の上か水面へ移す。
                var p = marker.transform.localPosition;
                p.y = onLand ? 0.01f : WaterY + 0.01f;
                marker.transform.localPosition = p;
                return;
            }

            if (!quality.HasValue)
            {
                return;
            }

            var prefab = quality.Value switch
            {
                Item.Fish => _qualityFish,
                Item.Salt => _qualitySalt,
                Item.Kelp => _qualityKelp,
                _ => null,
            };
            if (prefab == null)
            {
                return;
            }

            var go = Instantiate(prefab, _tileRoot);
            go.name = $"Quality {quality.Value} {c}";
            // 色付きの面を、その効果がある 1 マスの中心に重ねる。
            go.transform.localPosition = IslandGrid.CellCenter(c, BoardSize) + Vector3.up * (onLand ? 0.01f : WaterY + 0.01f);
            _markers[c] = go;
        }

        private void RebuildFacilities(IslandGame island)
        {
            var alive = new HashSet<int>();
            foreach (var f in island.Facilities)
            {
                alive.Add(f.Id);
                if (!_facilities.TryGetValue(f.Id, out var slot))
                {
                    slot = Spawn(f);
                    if (slot == null)
                    {
                        continue;
                    }

                    _facilities[f.Id] = slot;
                }

                if (!slot.Origin.Equals(f.Origin))
                {
                    slot.Origin = f.Origin;
                    slot.Instance.transform.localPosition =
                        IslandGrid.FootprintCenter(f.Origin, f.Def.Size, BoardSize) + Vector3.down * slot.SinkDepth;
                }

                slot.SinkDepth = f.Status == FacilityStatus.Submerged ? _submergedDepth : 0f;
                var running = f.Status == FacilityStatus.Running;
                if (running != slot.Running)
                {
                    slot.Running = running;
                    slot.Cycle?.SetRunning(running);
                    slot.Kamaboko?.SetRunning(running);
                }

                if (f.Kind == FacilityKind.Chikuwa)
                {
                    slot.Cycle?.SetHighHeat(f.RecipeIndex == 1);
                }
            }

            foreach (var id in _facilities.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                Destroy(_facilities[id].Instance);
                _facilities.Remove(id);
            }
        }

        private FacilitySlot Spawn(Facility f)
        {
            var prefab = PrefabOf(f.Kind);
            if (prefab == null)
            {
                Debug.LogWarning($"{nameof(IslandBoardView)}: {f.Kind} のプレハブが未設定です。", this);
                return null;
            }

            var go = Instantiate(prefab, _facilityRoot);
            go.name = $"{f.Kind} #{f.Id}";
            go.transform.localPosition = IslandGrid.FootprintCenter(f.Origin, f.Def.Size, BoardSize);
            var slot = new FacilitySlot
            {
                Kind = f.Kind,
                Origin = f.Origin,
                Instance = go,
                Cycle = go.GetComponent<FacilityProductionCycle>(),
                Kamaboko = go.GetComponent<KamabokoProductionCycle>(),
                Running = true,
                LastPlusOne = -999f,
            };
            return slot;
        }

        private void RebuildHarbor(IslandGame island)
        {
            if (_harborStages.Length == 0)
            {
                return;
            }

            var stage = Mathf.Clamp(island.HarborStage, 0, _harborStages.Length - 1);
            if (stage == _harborStage)
            {
                return;
            }

            var first = _harborStage < 0;
            _harborStage = stage;
            if (_harbor != null)
            {
                Destroy(_harbor);
            }

            if (_harborStages[stage] == null)
            {
                return;
            }

            _harbor = Instantiate(_harborStages[stage], _facilityRoot);
            _harbor.name = $"Harbor Stage {stage}";
            _harbor.transform.localPosition = HarborCenter + (HasHarborAnchor ? Vector3.zero : Vector3.up * WaterY);
            if (HasHarborAnchor) _harbor.transform.rotation = _harborAnchor.rotation;
            if (!first)
            {
                IslandPop.Play(_harbor.transform);
                Spawn(_confettiVfx, HarborCenter, 4f);
            }
        }

        private void OnEvents(IReadOnlyList<IslandEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case IslandEventKind.Built:
                    {
                        Rebuild();
                        if (_facilities.TryGetValue(e.FacilityId, out var slot))
                        {
                            var big = IslandCatalog.Get(slot.Kind).Size > 1;
                            Spawn(big ? _buildVfxLarge : _buildVfxSmall, slot.Instance.transform.localPosition, 3f);
                            IslandPop.Play(slot.Instance.transform);
                        }

                        break;
                    }

                    case IslandEventKind.Moved:
                        if (_facilities.TryGetValue(e.FacilityId, out var moved))
                        {
                            Spawn(_buildVfxSmall, IslandGrid.FootprintCenter(moved.Origin, IslandCatalog.Get(moved.Kind).Size, BoardSize), 3f);
                        }

                        break;
                    case IslandEventKind.Produced:
                        PlusOne(e.FacilityId, e.Item);
                        break;
                    case IslandEventKind.Expanded:
                    case IslandEventKind.Surfaced:
                        if (e.Cell.HasValue)
                        {
                            Spawn(_buildVfxSmall, IslandGrid.CellCenter(e.Cell.Value, BoardSize), 3f);
                        }

                        break;
                    case IslandEventKind.Sunk:
                        if (e.Cell.HasValue)
                        {
                            Spawn(_sinkVfx, IslandGrid.CellCenter(e.Cell.Value, BoardSize), 3f);
                        }

                        break;
                    case IslandEventKind.Delivered:
                        Spawn(_plusOneVfx, HarborCenter + Vector3.up * 1.2f, 2f);
                        break;
                    case IslandEventKind.LevelUp:
                        Spawn(_confettiVfx, Vector3.zero, 4f);
                        break;
                }
            }
        }

        private void PlusOne(int facilityId, Item? item)
        {
            if (!_facilities.TryGetValue(facilityId, out var slot))
            {
                return;
            }

            if (Time.time - slot.LastPlusOne < _plusOneInterval)
            {
                return;
            }

            slot.LastPlusOne = Time.time;
            var top = slot.Instance.transform.localPosition + Vector3.up * (IslandCatalog.Get(slot.Kind).Size > 1 ? 1.7f : 1.1f);
            Spawn(item.HasValue ? _plusOneVfx : _pearlVfx, top, 2f);
        }

        private void Spawn(GameObject prefab, Vector3 localPosition, float lifetime)
        {
            if (prefab == null)
            {
                return;
            }

            var go = Instantiate(prefab, _fxRoot);
            go.transform.localPosition = localPosition;
            Destroy(go, lifetime);
        }

        private Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
