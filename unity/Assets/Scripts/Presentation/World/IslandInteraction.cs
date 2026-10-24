using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>島の上での操作のモード。</summary>
    public enum IslandMode
    {
        /// <summary>眺める。施設をタップすると選ぶ。</summary>
        Browse,

        /// <summary>建てる場所を選んでいる。</summary>
        Place,

        /// <summary>施設を移す先を選んでいる。</summary>
        Move,

        /// <summary>海をタップして島を広げる。</summary>
        Expand,
    }

    /// <summary>
    /// 島へのタッチを受けて、眺める・選ぶ・建てる・移す・広げるを切り替える。
    /// 画面の UI の上で始まったタッチは扱わない (UI が先に受け取る)。
    /// 建てる・移すは、置き場所の候補 (ゴースト) を出して、UI の「ここに建てる」で確定する。
    /// </summary>
    public sealed class IslandInteraction : MonoBehaviour
    {
        /// <summary>この距離 (px) 以上動いたらタップではなくドラッグ。</summary>
        private const float DragThreshold = 18f;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private IslandBoardView _board;
        [SerializeField] private IslandCameraRig _rig;

        [Tooltip("タッチが UI の上かどうかを調べる")]
        [SerializeField] private UIDocument _ui;

        [Header("マスの強調")]
        [SerializeField] private Material _okMaterial;
        [SerializeField] private Material _ngMaterial;
        [SerializeField] private Material _selectMaterial;

        private readonly List<GameObject> _footprint = new List<GameObject>();
        private GameObject _ghost;
        private Transform _overlayRoot;

        private bool _pressing;
        private bool _dragging;
        private bool _startedOnUi;
        private Vector2 _pressStart;
        private Vector2 _lastPos;
        private float _lastPinch;

        /// <summary>モード・選択・候補の場所が変わった。</summary>
        public event Action Changed;

        /// <summary>施設をタップして選んだ。</summary>
        public event Action<int> FacilitySelected;

        /// <summary>港をタップした。</summary>
        public event Action HarborTapped;

        /// <summary>色付きの資源エリアをタップした。</summary>
        public event Action<Cell, Item> QualityAreaTapped;

        /// <summary>海をタップして広げようとした結果。</summary>
        public event Action<Cell, IslandActionResult> ExpandAttempted;

        public IslandMode Mode { get; private set; } = IslandMode.Browse;
        public FacilityKind PlacingKind { get; private set; }
        public int MovingId { get; private set; } = -1;
        public int SelectedId { get; private set; } = -1;
        public Cell GhostOrigin { get; private set; }
        public IslandActionResult GhostResult { get; private set; } = IslandActionResult.CannotPlace;

        /// <summary>ホームを開いている間だけ受け付ける (ScreenNavigator が切り替える)。</summary>
        public bool InputEnabled { get; set; } = true;

        private int Size => _game.Island.Board.Size;

        private void Awake()
        {
            var go = new GameObject("Overlay");
            go.transform.SetParent(_board != null ? _board.transform : transform, false);
            _overlayRoot = go.transform;
        }

        private void OnEnable()
        {
            if (_game == null || _board == null || _rig == null)
            {
                Debug.LogError($"{nameof(IslandInteraction)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _game.IslandChanged += OnIslandChanged;
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.IslandChanged -= OnIslandChanged;
            }
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                _pressing = false;
                return;
            }

            if (!HandleTouch())
            {
                HandleMouse();
            }
        }

        // ------------------------------------------------------------------
        // モード
        // ------------------------------------------------------------------

        public void BeginPlace(FacilityKind kind)
        {
            ClearSelection(false);
            Mode = IslandMode.Place;
            PlacingKind = kind;
            MovingId = -1;
            GhostOrigin = SuggestOrigin(kind, -1);
            RebuildGhost();
            FocusGhost();
            Changed?.Invoke();
        }

        public void BeginMove(int facilityId)
        {
            var f = _game.Island.Find(facilityId);
            if (f == null)
            {
                return;
            }

            ClearSelection(false);
            Mode = IslandMode.Move;
            PlacingKind = f.Kind;
            MovingId = facilityId;
            GhostOrigin = f.Origin;
            RebuildGhost();
            Changed?.Invoke();
        }

        public void BeginExpand()
        {
            ClearSelection(false);
            Mode = IslandMode.Expand;
            ClearGhost();
            _board.ShowExpandable = true;
            _rig.Home();
            Changed?.Invoke();
        }

        /// <summary>建てる・移すを確定する。失敗の理由を返す。</summary>
        public IslandActionResult Confirm()
        {
            IslandActionResult r;
            switch (Mode)
            {
                case IslandMode.Place:
                    r = _game.Build(PlacingKind, GhostOrigin);
                    break;
                case IslandMode.Move:
                    r = _game.Move(MovingId, GhostOrigin);
                    break;
                default:
                    return IslandActionResult.NotFound;
            }

            if (r == IslandActionResult.Ok)
            {
                Cancel();
            }

            return r;
        }

        /// <summary>眺めるモードに戻る。</summary>
        public void Cancel()
        {
            Mode = IslandMode.Browse;
            MovingId = -1;
            _board.ShowExpandable = false;
            ClearGhost();
            Changed?.Invoke();
        }

        public void Select(int facilityId)
        {
            SelectedId = facilityId;
            RebuildSelection();
            var f = _game.Island.Find(facilityId);
            if (f != null)
            {
                _rig.Focus(IslandGrid.FootprintCenter(f.Origin, f.Def.Size, Size) + new Vector3(0f, 0f, -1.2f), 11f);
            }

            Changed?.Invoke();
        }

        public void ClearSelection(bool notify = true)
        {
            if (SelectedId < 0)
            {
                return;
            }

            SelectedId = -1;
            RebuildSelection();
            if (notify)
            {
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------------
        // 入力
        // ------------------------------------------------------------------

        private bool HandleTouch()
        {
            var touch = Touchscreen.current;
            if (touch == null)
            {
                return false;
            }

            var active = 0;
            Vector2 p0 = default, p1 = default;
            foreach (var t in touch.touches)
            {
                if (!t.press.isPressed)
                {
                    continue;
                }

                if (active == 0)
                {
                    p0 = t.position.ReadValue();
                }
                else if (active == 1)
                {
                    p1 = t.position.ReadValue();
                }

                active++;
            }

            if (active >= 2)
            {
                var d = Vector2.Distance(p0, p1);
                if (_lastPinch > 0f && d > 0f && !_startedOnUi)
                {
                    _rig.Zoom(_lastPinch / d);
                }

                _lastPinch = d;
                _dragging = true;
                return true;
            }

            _lastPinch = 0f;
            if (active == 1)
            {
                Press(p0);
                return true;
            }

            if (_pressing)
            {
                Release(_lastPos);
                return true;
            }

            return touch.touches.Count > 0 && Mouse.current == null;
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var pos = mouse.position.ReadValue();
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(pos))
            {
                _rig.Zoom(scroll > 0 ? 0.9f : 1.1f);
            }

            if (mouse.leftButton.isPressed)
            {
                Press(pos);
            }
            else if (_pressing)
            {
                Release(pos);
            }
        }

        private void Press(Vector2 pos)
        {
            if (!_pressing)
            {
                _pressing = true;
                _dragging = false;
                _pressStart = pos;
                _lastPos = pos;
                _startedOnUi = IsOverUi(pos);
                return;
            }

            if (_startedOnUi)
            {
                _lastPos = pos;
                return;
            }

            if (!_dragging && (pos - _pressStart).magnitude > DragThreshold)
            {
                _dragging = true;
            }

            if (_dragging)
            {
                _rig.Pan(_lastPos, pos);
            }

            _lastPos = pos;
        }

        private void Release(Vector2 pos)
        {
            var wasTap = _pressing && !_dragging && !_startedOnUi;
            _pressing = false;
            _dragging = false;
            if (wasTap)
            {
                Tap(pos);
            }
        }

        private bool IsOverUi(Vector2 screen)
        {
            var panel = _ui != null ? _ui.rootVisualElement?.panel : null;
            if (panel == null)
            {
                return false;
            }

            var panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(panelPos) != null;
        }

        private void Tap(Vector2 screen)
        {
            if (Mode == IslandMode.Browse && _board.TryHarbor(_rig.ScreenRay(screen)))
            {
                ClearSelection();
                HarborTapped?.Invoke();
                return;
            }

            if (!_rig.TryGround(screen, out var world))
            {
                return;
            }

            var local = world;
            var cell = IslandGrid.CellAt(local, Size);
            switch (Mode)
            {
                case IslandMode.Place:
                case IslandMode.Move:
                {
                    var s = IslandCatalog.Get(PlacingKind).Size;

                    // タップした点に足元の中心が来るように左下のマスを決める (マスの中心が整数になる座標で丸める)。
                    var u = local.x + Size * 0.5f - 0.5f - (s - 1) * 0.5f;
                    var v = local.z + Size * 0.5f - 0.5f - (s - 1) * 0.5f;
                    var origin = new Cell(Mathf.RoundToInt(u), Mathf.RoundToInt(v));
                    GhostOrigin = new Cell(Mathf.Clamp(origin.X, 0, Size - s), Mathf.Clamp(origin.Y, 0, Size - s));
                    RebuildGhost();
                    Changed?.Invoke();
                    break;
                }

                case IslandMode.Expand:
                {
                    var r = _game.Expand(cell);
                    ExpandAttempted?.Invoke(cell, r);
                    if (r == IslandActionResult.Ok && _game.Island.ExpandRoom == 0)
                    {
                        Changed?.Invoke();
                    }

                    break;
                }

                default:
                {
                    var f = _game.Island.FacilityAt(cell);
                    var qualityHit = false;
                    if (f == null && !IsHarbor(cell) && _board.TryQualityArea(_rig.ScreenRay(screen), out var qualityCell))
                    {
                        var island = _game.Island;
                        // 手前の陸の下を通って海の印に届いた視線は採用しない。
                        var hiddenByLand = !qualityCell.Equals(cell) && island.Board.IsSolidLand(cell, island.Ice) &&
                                           !island.Board.IsSolidLand(qualityCell, island.Ice);
                        if (!hiddenByLand)
                        {
                            cell = qualityCell;
                            qualityHit = true;
                            f = island.FacilityAt(cell);
                        }
                    }

                    if (f != null)
                    {
                        Select(f.Id);
                        FacilitySelected?.Invoke(f.Id);
                    }
                    else if (IsHarbor(cell))
                    {
                        ClearSelection();
                        HarborTapped?.Invoke();
                    }
                    else
                    {
                        ClearSelection();
                        var quality = _game.Island.Board.QualityAt(cell);
                        if (qualityHit && quality.HasValue)
                        {
                            QualityAreaTapped?.Invoke(cell, quality.Value);
                        }
                    }

                    break;
                }
            }
        }

        private bool IsHarbor(Cell c)
        {
            if (_board.HasHarborAnchor) return false;
            var s = Size;
            return c.Y >= s - 2 && c.X >= s / 2 - 1 && c.X <= s / 2;
        }

        // ------------------------------------------------------------------
        // 候補と強調
        // ------------------------------------------------------------------

        private void OnIslandChanged()
        {
            if (Mode == IslandMode.Place || Mode == IslandMode.Move)
            {
                var r = Validate();
                if (r != GhostResult)
                {
                    GhostResult = r;
                    PaintFootprint(GhostOrigin, IslandCatalog.Get(PlacingKind).Size, r == IslandActionResult.Ok ? _okMaterial : _ngMaterial);
                    Changed?.Invoke();
                }
            }

            if (SelectedId >= 0 && _game.Island.Find(SelectedId) == null)
            {
                ClearSelection();
            }
        }

        private IslandActionResult Validate()
        {
            var island = _game.Island;
            var place = island.CanPlace(PlacingKind, GhostOrigin, Mode == IslandMode.Move ? MovingId : -1);
            if (place != IslandActionResult.Ok || Mode == IslandMode.Move)
            {
                return place;
            }

            return island.CanBuild(PlacingKind);
        }

        /// <summary>見ている辺りで建てられる場所 (無ければ基礎の島の左下)。</summary>
        private Cell SuggestOrigin(FacilityKind kind, int ignoreId)
        {
            var island = _game.Island;
            var s = IslandCatalog.Get(kind).Size;
            var def = IslandCatalog.Get(kind);
            Cell? best = null;
            var bestScore = float.MaxValue;
            for (var x = 0; x <= Size - s; x++)
            {
                for (var y = 0; y <= Size - s; y++)
                {
                    var c = new Cell(x, y);
                    if (island.CanPlace(kind, c, ignoreId) != IslandActionResult.Ok)
                    {
                        continue;
                    }

                    var center = IslandGrid.FootprintCenter(c, s, Size);
                    var score = center.sqrMagnitude;
                    if (def.NeedsCoast && !HasCoastAt(kind, c))
                    {
                        score += 1000f;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
            }

            return best ?? new Cell(island.Parameters.BaseX, island.Parameters.BaseY);
        }

        private bool HasCoastAt(FacilityKind kind, Cell origin)
        {
            var board = _game.Island.Board;
            var s = IslandCatalog.Get(kind).Size;
            for (var dx = -1; dx <= s; dx++)
            {
                for (var dy = -1; dy <= s; dy++)
                {
                    var edge = dx == -1 || dy == -1 || dx == s || dy == s;
                    var corner = (dx == -1 || dx == s) && (dy == -1 || dy == s);
                    if (edge && !corner && board.IsWaterAt(new Cell(origin.X + dx, origin.Y + dy), _game.Island.Ice))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RebuildGhost()
        {
            ClearGhost();
            var s = IslandCatalog.Get(PlacingKind).Size;
            GhostResult = Validate();
            PaintFootprint(GhostOrigin, s, GhostResult == IslandActionResult.Ok ? _okMaterial : _ngMaterial);

            var prefab = _board.PrefabOf(PlacingKind);
            if (prefab == null)
            {
                return;
            }

            // 動きの部品 (FacilityProductionCycle など) は、参照を設定する前に OnEnable が走るとエラーを出す。
            // 無効な入れ物の中で生成し、部品を止めてから表に出す。
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            _ghost = Instantiate(prefab, holder.transform);
            _ghost.name = "Ghost";
            foreach (var behaviour in _ghost.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            _ghost.transform.SetParent(_overlayRoot, false);
            Destroy(holder);

            _ghost.transform.localPosition = IslandGrid.FootprintCenter(GhostOrigin, s, Size) + Vector3.up * 0.15f;
            if (Mode == IslandMode.Move)
            {
                var original = _board.FacilityObject(MovingId);
                if (original != null)
                {
                    original.SetActive(false);
                }
            }
        }

        private void FocusGhost()
        {
            var s = IslandCatalog.Get(PlacingKind).Size;
            _rig.Focus(IslandGrid.FootprintCenter(GhostOrigin, s, Size) + new Vector3(0f, 0f, -1.5f), 13f);
        }

        private void ClearGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }

            foreach (var f in _game.Island.Facilities)
            {
                var go = _board.FacilityObject(f.Id);
                if (go != null && !go.activeSelf)
                {
                    go.SetActive(true);
                }
            }

            ClearFootprint();
            RebuildSelection();
        }

        private void RebuildSelection()
        {
            if (Mode != IslandMode.Browse)
            {
                return;
            }

            ClearFootprint();
            var f = SelectedId >= 0 ? _game.Island.Find(SelectedId) : null;
            if (f != null)
            {
                PaintFootprint(f.Origin, f.Def.Size, _selectMaterial);
            }
        }

        private void PaintFootprint(Cell origin, int size, Material material)
        {
            ClearFootprint();
            if (material == null)
            {
                return;
            }

            for (var dx = 0; dx < size; dx++)
            {
                for (var dy = 0; dy < size; dy++)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(quad.GetComponent<Collider>());
                    quad.name = "Footprint";
                    quad.transform.SetParent(_overlayRoot, false);
                    quad.transform.localPosition = IslandGrid.CellCenter(new Cell(origin.X + dx, origin.Y + dy), Size) + Vector3.up * 0.04f;
                    quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    quad.transform.localScale = Vector3.one * 0.94f;
                    quad.GetComponent<Renderer>().sharedMaterial = material;
                    _footprint.Add(quad);
                }
            }
        }

        private void ClearFootprint()
        {
            foreach (var q in _footprint)
            {
                Destroy(q);
            }

            _footprint.Clear();
        }
    }
}
