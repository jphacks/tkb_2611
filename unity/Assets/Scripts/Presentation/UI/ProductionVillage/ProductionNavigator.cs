using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.Industrial;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 村のホームの UI。島を眺めるホームモードと、施設を配置して生産状況を確認する工房モードを切り替える。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionNavigator : MonoBehaviour
    {
        private const string WaterActionName = "villageWaterAction";
        private const string ScreenTimeActionName = "villageScreenTimeAction";
        private const string FakeEncounterActionName = "villageFakeEncounterAction";
        private const string DebugActionName = "villageFreeResourcesAction";
        /// <summary>開発用の歩数のボタン 1 回で足す歩数。</summary>
        private const int DevelopmentSteps = 1000;
        private const float RefreshSeconds = .1f;
        private const float SheetRefreshSeconds = .5f;
        private const float CountUpSeconds = .4f;
        private const float ToastSeconds = 2.6f;

        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private VillageWorldPresenter _world;

        private VisualElement _root, _app, _sheet, _tools, _tray, _toastLayer, _hero, _error, _thumb;
        private VisualElement _tacticalBar, _quickDock, _dockTabs, _spatialLayer;
        private ScrollView _content, _dockScroll;
        private ProgressBar _rankProgress;
        private Label _fish, _fishRate, _rankText, _gas, _electricity, _water, _profile, _errorText, _reason, _title;
        private Label _industrialSwitchLabel, _tacticalGas, _topDownLabel, _insightText;
        private Button _confirm, _activeTab, _topDownToggle, _insightBanner;
        private ProductionReportPresenter _report;
        private VillageWaterScanPresenter _waterScan;
        private VillageScreenTimePresenter _screenTime;
        private VillageEncounters _encounters;
        private Action _refreshSheet;
        private bool _sheetOpen, _pageOpen, _softOpen;
        private ScrollView _sheetContent, _pageContent, _softContent;
        private VisualElement _page, _softPage;
        private Label _pageTitle, _pageMicro, _softTitle, _softSub;
        private string _sheetPageName;
        private bool _industrialMode;
        private string _dockCategory = "all";
        private float _nextRefresh, _nextSheetRefresh;
        private double _fishShown, _fishTarget, _fishSpeed;
        private double _lastGas = double.NaN, _lastElectricity = double.NaN, _lastWater = double.NaN;
        private string _lastNotice;
        private string _thumbClass;
        private readonly Dictionary<int, Button> _spatialBadges = new Dictionary<int, Button>();
        private readonly Dictionary<string, VisualElement> _groupMarks = new Dictionary<string, VisualElement>();

        public bool WorldInputEnabled => !_sheetOpen && (_report == null || !_report.IsOpen)
            && !(_waterScan != null && _waterScan.IsOpen) && !_session.IsSuspended && _session.Core.View != null;
        public VillagePlacementController Placement => _world.Placement;
        public VillageWorldPresenter World => _world;

        /// <summary>施設の詳細ページの英字の小見出し。チュートリアルが「施設のページが開いた」を見分けるのに使う。</summary>
        public const string FacilityPage = "FACILITY";
        public bool IndustrialMode => _industrialMode;
        /// <summary>開いている工房や施設詳細の識別子。開いていなければ null。</summary>
        public string OpenPageName => _pageOpen ? _pageMicro?.text : _sheetOpen ? _sheetPageName : null;
        public bool ReportOpen => _report != null && _report.IsOpen;
        private VillageTutorialPresenter _tutorial;

        /// <summary>施設一覧のその施設の札。無ければ null。<paramref name="scroll"/> なら見えるところまでスクロールする (チュートリアルで光らせるため)。</summary>
        public VisualElement DockSlot(FacilityKind kind, bool scroll)
        {
            var slot = _dockScroll?.Children().FirstOrDefault(s => s.userData is FacilityKind k && k == kind);
            if (slot != null && scroll) _dockScroll.ScrollTo(slot);
            return slot;
        }

        private void Start()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _waterScan = GetComponent<VillageWaterScanPresenter>();
            _screenTime = GetComponent<VillageScreenTimePresenter>();
            _encounters = _session.GetComponent<VillageEncounters>();
            if (_encounters != null)
            {
                _encounters.Granted += ShowToast;
            }
            _app = _root.Q(ProductionUiNames.Root);
            _sheet = _root.Q(ProductionUiNames.Sheet);
            _content = _sheetContent = _root.Q<ScrollView>(ProductionUiNames.Content);
            _page = _root.Q(ProductionUiNames.WorkshopPage);
            _pageContent = _root.Q<ScrollView>(ProductionUiNames.WorkshopContent);
            _pageTitle = _root.Q<Label>(ProductionUiNames.WorkshopTitle);
            _pageMicro = _root.Q<Label>(ProductionUiNames.WorkshopMicro);
            _softPage = _root.Q(ProductionUiNames.SoftPage);
            _softContent = _root.Q<ScrollView>(ProductionUiNames.SoftContent);
            _softTitle = _root.Q<Label>(ProductionUiNames.SoftTitle);
            _softSub = _root.Q<Label>(ProductionUiNames.SoftSub);
            _tools = _root.Q(ProductionUiNames.Tools);
            _tray = _root.Q(ProductionUiNames.Tray);
            _toastLayer = _root.Q(ProductionUiNames.ToastLayer);
            _hero = _root.Q(ProductionUiNames.Hero);
            _error = _root.Q(ProductionUiNames.Error);
            _thumb = _root.Q(ProductionUiNames.PlacementThumb);
            _tacticalBar = _root.Q(ProductionUiNames.TacticalBar);
            _quickDock = _root.Q(ProductionUiNames.QuickDock);
            _dockTabs = _root.Q(ProductionUiNames.DockTabs);
            _dockScroll = _root.Q<ScrollView>(ProductionUiNames.DockScroll);
            _spatialLayer = _root.Q(ProductionUiNames.SpatialLayer);
            _rankProgress = _root.Q<ProgressBar>(ProductionUiNames.RankProgress);
            _rankText = _root.Q<Label>(ProductionUiNames.RankText);
            _fish = _root.Q<Label>(ProductionUiNames.Fish);
            _fishRate = _root.Q<Label>(ProductionUiNames.FishRate);
            _gas = _root.Q<Label>(ProductionUiNames.Gas);
            _electricity = _root.Q<Label>(ProductionUiNames.Electricity);
            _water = _root.Q<Label>(ProductionUiNames.Water);
            _profile = _root.Q<Label>(ProductionUiNames.Profile);
            _errorText = _root.Q<Label>(ProductionUiNames.ErrorText);
            _reason = _root.Q<Label>(ProductionUiNames.Reason);
            _title = _root.Q<Label>(ProductionUiNames.Title);
            _industrialSwitchLabel = _root.Q<Label>(ProductionUiNames.IndustrialSwitchLabel);
            _tacticalGas = _root.Q<Label>(ProductionUiNames.TacticalGas);
            _topDownLabel = _root.Q<Label>(ProductionUiNames.TopDownLabel);
            _topDownToggle = _root.Q<Button>(ProductionUiNames.TopDownToggle);
            _insightBanner = _root.Q<Button>(ProductionUiNames.InsightBanner);
            _insightText = _root.Q<Label>(ProductionUiNames.InsightText);
            _confirm = _root.Q<Button>(ProductionUiNames.Confirm);
            _activeTab = _root.Q<Button>(ProductionUiNames.HomeTab);

            Bind(ProductionUiNames.IndustrialSwitch, () => SetIndustrialMode(!_industrialMode));
            Bind(ProductionUiNames.TopDownToggle, ToggleTopDownCamera);
            Bind(ProductionUiNames.RotateCamera, RotateCamera90);
            _world.ZoomChanged += OnZoomChanged;
            Bind(ProductionUiNames.ZoomDistrict, () => _world?.CameraController?.SetLevel(VillageZoomLevel.District));
            Bind(ProductionUiNames.ZoomOverview, () => _world?.CameraController?.SetLevel(VillageZoomLevel.Overview));
            Bind(ProductionUiNames.ZoomDetail, () => _world?.CameraController?.SetLevel(VillageZoomLevel.Detail));
            Bind(ProductionUiNames.InsightBanner, ShowReport);
            Bind(ProductionUiNames.TacticalBar, ShowReport);
            Bind(ProductionUiNames.DockCatalogBtn, ShowPlacement);
            Bind(ProductionUiNames.Placement, () => SetIndustrialMode(true));
            Bind(ProductionUiNames.Move, () => Placement?.MoveSelect());
            Bind(ProductionUiNames.Production, ShowReport);
            Bind(ProductionUiNames.Harbor, () => ShowSummary(true));
            Bind(ProductionUiNames.HomeTab, () =>
            {
                SetIndustrialMode(false);
                Close();
            });
            Bind(ProductionUiNames.Close, Close);
            Bind(ProductionUiNames.WorkshopClose, Close);
            Bind(ProductionUiNames.SoftBack, Close);
            Bind(ProductionUiNames.RecordTab, ShowRecord);
            Bind(ProductionUiNames.ScanTab, ShowScan);
            Bind(ProductionUiNames.ManageTab, ShowManagement);
            Bind(ProductionUiNames.MenuTab, ShowMenu);
            Bind(ProductionUiNames.Retry, () => _session.RetryLoad());
            Bind(ProductionUiNames.Confirm, ConfirmPlacement);
            Bind(ProductionUiNames.Cancel, CancelPlacement);
            _root.Q(ProductionUiNames.Scrim).RegisterCallback<ClickEvent>(_ => Close());

            _lastNotice = _session.Core.Notice;
            var view = _session.Core.View;
            _fishShown = _fishTarget = view == null ? 0 : view.Totals.GetValueOrDefault(Item.Fish);
            _fish.text = IslandUi.Number(_fishShown);

            var reportRoot = _root.Q(ProductionReportNames.Root);
            if (reportRoot != null)
            {
                _report = new ProductionReportPresenter(reportRoot, _session);
            }

            RebuildQuickDock();
            _tutorial = new VillageTutorialPresenter(_root, this, _session);
        }

        private void Bind(string name, Action action)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.clicked += action;
        }

        private void Update()
        {
            if (_root == null)
            {
                return;
            }

            CountUpFish();
            _tutorial?.Update();
            var core = _session.Core;
            var view = core.View;
            UpdateSpatialBadges(view);
            UpdateGroupMarks(view);
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            IslandUi.SetVisible(_profile, core.Development);
            if (_profile != null) _profile.text = view != null && view.FreeResources ? "資源 ∞" : "開発用";
            ShowResources(view);
            ShowStatus(core);
            ShowPlacementTray();
            ShowZoomLevel();
            if (_sheetOpen && _refreshSheet != null && Time.unscaledTime >= _nextSheetRefresh)
            {
                _nextSheetRefresh = Time.unscaledTime + SheetRefreshSeconds;
                _refreshSheet();
            }

            if (_report != null && _report.IsOpen && Time.unscaledTime >= _nextRefresh)
            {
                _report.Refresh();
            }
        }

        // ------------------------------------------------------------------
        // モード切替 (ホーム ⇔ 工房モード) & カメラ操作
        // ------------------------------------------------------------------

        public void SetIndustrialMode(bool enabled)
        {
            _industrialMode = enabled;
            if (_world != null)
            {
                _world.IndustrialMode = enabled;
                if (!enabled && _world.CameraController != null && _world.CameraController.IsTopDown)
                {
                    _world.CameraController.SetTopDown(false);
                }
            }

            if (_industrialSwitchLabel != null)
            {
                _industrialSwitchLabel.text = enabled ? "ホームへ戻る" : "工房モード";
            }

            IslandUi.SetVisible(_tacticalBar, enabled);
            if (_root != null) IslandUi.SetVisible(_root.Q(ProductionUiNames.CameraRail), enabled);
            UpdateTopDownUi();
            if (enabled)
            {
                CloseSheetOnly();
                RebuildQuickDock();
            }
            else
            {
                Placement?.Cancel();
            }

            ShowPlacementTray();
        }

        private void ToggleTopDownCamera()
        {
            var cam = _world != null ? _world.CameraController : null;
            if (cam == null) return;
            cam.ToggleTopDown();
            UpdateTopDownUi();
        }

        private void RotateCamera90()
        {
            var cam = _world != null ? _world.CameraController : null;
            if (cam == null) return;
            cam.RotateStep90();
        }

        private void UpdateTopDownUi()
        {
            var topDown = _world?.CameraController != null && _world.CameraController.IsTopDown;
            if (_topDownLabel != null)
            {
                _topDownLabel.text = topDown ? "真上" : "斜め";
            }

            _topDownToggle?.EnableInClassList(ProductionUiNames.TacticalBtnActiveClass, topDown);
        }

        private void OnZoomChanged(){Close();_nextRefresh=0;}
        private void ShowZoomLevel()
        {
            var view=_session.Core.View;
            var camera=_world?.CameraController;
            if(view==null)return;
            camera?.SetMaximumZoom(view.MaximumZoom);
            var level=camera!=null?camera.Level:VillageZoomLevel.Overview;
            foreach(var pair in new[]{(ProductionUiNames.ZoomDetail,VillageZoomLevel.Detail),(ProductionUiNames.ZoomOverview,VillageZoomLevel.Overview),(ProductionUiNames.ZoomDistrict,VillageZoomLevel.District)})
                _root.Q<Button>(pair.Item1)?.EnableInClassList(ProductionUiNames.TacticalBtnActiveClass,level==pair.Item2);
            var third=_root.Q<Button>(ProductionUiNames.ZoomDistrict);
            if(third!=null){third.SetEnabled(view.MaximumZoom>=3);third.text=view.MaximumZoom>=3?"3ズーム":"3 未解放";third.tooltip="すり身・干物を合計5個納品して解放";}
            var state=_root.Q<Label>(ProductionUiNames.GroupState);
            if(state!=null)state.text=level==VillageZoomLevel.Detail?"1ズーム\n施設を編集":$"{(int)level}ズーム\n同種2×2で成立";
        }
        private static (int Rank, float Progress) IslandRank(VillageView view) => (view.Rank,view.RankProgress);

        private void ShowResources(VillageView view)
        {
            if (view == null)
            {
                return;
            }

            var fish = view.Totals.GetValueOrDefault(Item.Fish);
            if (fish != _fishTarget)
            {
                if (fish > _fishTarget)
                {
                    Bump(_hero, ProductionUiNames.HeroBump);
                }

                _fishTarget = fish;
                _fishSpeed = Math.Max(1, Math.Abs(_fishTarget - _fishShown) / CountUpSeconds);
            }

            IslandUi.SetVisible(_fishRate, view.ProducedLastMinute > 0);
            _fishRate.text = $"+{IslandUi.Number(view.ProducedLastMinute)}/分";

            // 島ランクと次ランクまでの進捗バー (ポケモンスリープ風)
            var (rank, progress) = IslandRank(view);
            if (_rankText != null) _rankText.text = $"{rank}";
            if (_rankProgress != null) _rankProgress.value = progress;

            // 工房の操作バー: ガスの残りと消費、施設の稼働・停止・配送の数 (生産レポートと同じ数え方)
            if (_tacticalGas != null)
            {
                _tacticalGas.text = view.Unlimited ? "ガス ∞ (開発用)"
                    : view.FreeResources ? $"ガス ∞ (資源無限)・消費 {view.GasPerMinute:0.#}/分"
                    : $"ガス {IslandUi.Number(view.Gas)}・消費 {view.GasPerMinute:0.#}/分";
                _tacticalGas.EnableInClassList(ProductionUiNames.TacticalGasEmptyClass, !view.ResourcesUnlimited && view.Gas < 1);
            }

            var counts = VillageInsight.Counts(view);
            var status = _root.Q<Label>(ProductionUiNames.TacticalStatus);
            if (status != null)
            {
                status.text = $"稼働 {counts.Active}/{counts.Total}　停止 {counts.Stopped}　配送中 {counts.Delivering} 便";
                status.EnableInClassList(ProductionUiNames.TacticalStatusBadClass, counts.Stopped > 0);
            }

            // 次にやること。ホームの左下と工房モードの操作バーに同じ文を出す (VillageInsight.NextStep)
            var step = VillageInsight.NextStep(view, _world != null ? _world.Hints : null);
            var stepText = step.Hint.Length > 0 ? $"{step.Text} — {step.Hint}" : step.Text;
            var trouble = step.Tone == VillageNextStepTone.Warn || step.Tone == VillageNextStepTone.Bad;
            if (_insightText != null && _insightBanner != null)
            {
                _insightText.text = stepText;
                _insightBanner.EnableInClassList(ProductionUiNames.InsightBannerWarnClass, trouble);
            }

            var next = _root.Q<Label>(ProductionUiNames.NextStep);
            if (next != null)
            {
                next.text = stepText;
                next.EnableInClassList(ProductionUiNames.NextStepWarnClass, trouble);
            }

            // 勧めている施設を、下の施設一覧で光らせる
            if (_dockScroll != null)
            {
                foreach (var slot in _dockScroll.Children())
                {
                    slot.EnableInClassList(ProductionUiNames.DockSlotSuggestedClass, step.Build.HasValue && slot.userData is FacilityKind kind && kind == step.Build.Value);
                }
            }

            Resource(_gas, view.Gas, ref _lastGas);
            Resource(_electricity, view.Electricity, ref _lastElectricity);
            Resource(_water, view.Water, ref _lastWater);
        }

        private void UpdateSpatialBadges(VillageView view)
        {
            if (_spatialLayer == null || _world?.ViewCamera == null || view == null || _sheetOpen)
            {
                if (_spatialLayer != null) IslandUi.SetVisible(_spatialLayer, false);
                return;
            }

            IslandUi.SetVisible(_spatialLayer, true);
            var cam = _world.ViewCamera;
            var activeIds = new HashSet<int>();
            var overview = _world.CameraController == null || _world.CameraController.Level == VillageZoomLevel.Overview;
            // 画面の部品 (状態バー・施設一覧・カメラ操作) の下に来る札は出さない
            var covers = new[] { _tacticalBar, _quickDock, _root.Q(ProductionUiNames.CameraRail) }
                .Where(e => e != null && !e.ClassListContains(UiClasses.Hidden)).Select(e => e.worldBound).ToArray();
            var placed = new List<Rect>();
            foreach (var n in (_world.VisibleView?.Nodes ?? view.Nodes).OrderBy(a => a.Y).ThenBy(a => a.X))
            {
                activeIds.Add(n.Id);
                if (!_spatialBadges.TryGetValue(n.Id, out var badge))
                {
                    var nodeId = n.Id;
                    badge = new Button(() => ShowFacility(nodeId));
                    // 札の印は絵文字ではなくアイコン (お魚などと同じ SVG) を使う
                    var badgeIcon = new VisualElement { pickingMode = PickingMode.Ignore };
                    badgeIcon.AddToClassList(UiClasses.Icon.Root);
                    badgeIcon.AddToClassList(ProductionUiNames.SpatialBadgeIconClass);
                    badge.Add(badgeIcon);
                    badge.Add(new Label { pickingMode = PickingMode.Ignore });
                    var comboBadge = new Label { name = "villageComboBadge" + n.Id, pickingMode = PickingMode.Ignore };
                    comboBadge.AddToClassList(ProductionUiNames.ComboBadge); badge.Add(comboBadge);
                    badge.AddToClassList(ProductionUiNames.SpatialBadgeClass);
                    _spatialLayer.Add(badge);
                    _spatialBadges[n.Id] = badge;
                }

                var worldPos = VillageWorldRenderer.Position(n.X, n.Y, n.Size) + Vector3.up * 0.65f;
                var screenPos = cam.WorldToScreenPoint(worldPos);
                if (screenPos.z <= 0 || _root.panel == null)
                {
                    IslandUi.SetVisible(badge, false);
                    continue;
                }

                // 札は spatial layer の中に置くので、パネルの座標をこの層の座標に直す (セーフエリアの余白でずれないように)
                var onPanel = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                var panelPos = _spatialLayer.WorldToLocal(onPanel);

                var warn = VillageInsight.IsStopped(n.Status);
                var comboChange = _world.PlacementCombo?.Changes.FirstOrDefault(c => c.Id == n.Id);
                var comboCount = comboChange?.After ?? n.ComboCount;
                var showCombo = comboCount > 0 || comboChange != null;
                badge.EnableInClassList(ProductionUiNames.SpatialBadgeCombo, !warn && showCombo);
                badge.EnableInClassList(ProductionUiNames.SpatialBadgeWarnClass, warn);
                badge.EnableInClassList(ProductionUiNames.SpatialBadgeSynergyClass, !warn && (n.InSynergy || n.Amplified));

                var def = IslandCatalog.Get(n.Kind);
                var recipe = def.Recipes.FirstOrDefault();
                // 全体表示では止まっている施設の札だけを出す。詳細表示では短い札をすべて出す
                var label = warn ? (n.Status == VillageInsight.GasWait ? "ガス不足" : n.Status == VillageInsight.Full ? "満杯" : "材料待ち")
                    : overview ? ""
                    : _industrialMode && n.InSynergy ? $"施設群 {(int)(n.Progress * 100)}%"
                    : _industrialMode && n.Amplified ? $"増幅 {(int)(n.Progress * 100)}%"
                    : recipe != null && n.Total > 0 ? $"{IslandCatalog.NameOf(recipe.Output.Item)} ×{n.Total}"
                    : _industrialMode ? def.Name : "";

                if (string.IsNullOrEmpty(label) && !showCombo || covers.Any(r => r.Contains(onPanel)))
                {
                    IslandUi.SetVisible(badge, false);
                    continue;
                }

                IslandUi.SetVisible(badge, true);
                var icon = badge.Q(className: ProductionUiNames.SpatialBadgeIconClass);
                var iconClass = warn ? (n.Status == VillageInsight.GasWait ? UiClasses.Icon.GasEmpty : n.Status == VillageInsight.Full ? UiClasses.Icon.StorageFull : UiClasses.Icon.MaterialWait)
                    : _industrialMode && n.InSynergy ? UiClasses.Icon.Production
                    : _industrialMode && n.Amplified ? UiClasses.Icon.Electricity
                    : recipe != null && n.Total > 0 ? IslandUi.IconClass(recipe.Output.Item) : null;
                if (icon.userData as string != iconClass)
                {
                    if (icon.userData is string old) icon.RemoveFromClassList(old);
                    if (iconClass != null) icon.AddToClassList(iconClass);
                    icon.userData = iconClass;
                }

                IslandUi.SetVisible(icon, iconClass != null);
                badge.Q<Label>().text = label;
                var comboLabel = badge.Q<Label>("villageComboBadge" + n.Id);
                IslandUi.SetVisible(comboLabel, showCombo);
                comboLabel.text = (comboChange != null ? "予定 " : "") + $"×{VillageComboRules.Multiplier(comboCount):0.##}";
                comboLabel.tooltip = $"コンボ {comboCount} / 4方向、最大2倍";
                // 隣の札と重なるときは上へずらす (前のフレームの大きさで判定する)
                var width = float.IsNaN(badge.layout.width) || badge.layout.width <= 0 ? 120f : badge.layout.width;
                var height = float.IsNaN(badge.layout.height) || badge.layout.height <= 0 ? 44f : badge.layout.height;
                var rect = new Rect(panelPos.x - width * .5f, panelPos.y - height, width, height);
                for (var tries = 0; tries < 4 && placed.Any(r => r.Overlaps(rect)); tries++) rect.y -= height + 4f;
                placed.Add(rect);
                badge.style.left = rect.x;
                badge.style.top = rect.y;
            }

            foreach (var oldId in _spatialBadges.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            {
                _spatialBadges[oldId].RemoveFromHierarchy();
                _spatialBadges.Remove(oldId);
            }
        }

        private void UpdateGroupMarks(VillageView view)
        {
            var keys = new HashSet<string>();
            if (_spatialLayer != null && _root.panel != null && _world?.ViewCamera != null &&
                !_sheetOpen && !_session.IsSuspended && _world.CameraController?.Level == VillageZoomLevel.Detail && view?.GroupViewActive == true)
            {
                foreach (var group in view.Synergies)
                {
                    var key = group.BonusItem + ":" + string.Join(",", group.NodeIds.OrderBy(id => id));
                    keys.Add(key);
                    if (!_groupMarks.TryGetValue(key, out var mark))
                    {
                        mark = new VisualElement { pickingMode = PickingMode.Ignore };
                        mark.AddToClassList(UiClasses.Icon.Root);
                        mark.AddToClassList(group.BonusItem == Item.Chikuwa ? UiClasses.Icon.Chikuwa :
                            group.BonusItem == Item.Kamaboko ? UiClasses.Icon.Kamaboko : UiClasses.Icon.DriedFish);
                        mark.AddToClassList(ProductionUiNames.GroupMarkClass);
                        _spatialLayer.Insert(0, mark);
                        _groupMarks[key] = mark;
                    }
                    mark.EnableInClassList(ProductionUiNames.GroupMarkWaitingClass, !group.Active);
                    mark.tooltip = group.Status;
                    var center = new Vector3((group.MinX + group.Width * .5f - 12) * VillageWorldRenderer.Cell,
                        1.1f, (group.MinY + group.Height * .5f - 12) * VillageWorldRenderer.Cell);
                    var point = _world.ViewCamera.WorldToViewportPoint(center);
                    var visible = point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
                    IslandUi.SetVisible(mark, visible);
                    if (!visible) continue;
                    var panel = RuntimePanelUtils.ScreenToPanel(_root.panel,
                        new Vector2(point.x * Screen.width, (1 - point.y) * Screen.height));
                    var local = _spatialLayer.WorldToLocal(panel);
                    mark.style.left = local.x - 23;
                    mark.style.top = local.y - 23;
                }
            }
            foreach (var key in _groupMarks.Keys.Where(k => !keys.Contains(k)).ToArray())
            {
                _groupMarks[key].RemoveFromHierarchy();
                _groupMarks.Remove(key);
            }
        }

        private static string GroupStatus(VillageView view, Item item)
        {
            var groups = view?.Synergies?.Where(s => s.BonusItem == item).ToArray();
            if (groups == null || groups.Length == 0) return $"成立で平均{VillageGroupRules.ItemsPerMinute * 60:0.#}個/時";
            var active = groups.Count(s => s.Active);
            return active > 0 ? $"追加生産 {groups.Sum(s => s.BonusPerMinute) * 60:0.#}個/時" : groups[0].Status;
        }

        private void CountUpFish()
        {
            if (_fishShown == _fishTarget)
            {
                return;
            }

            var step = _fishSpeed * Time.unscaledDeltaTime;
            _fishShown = _fishShown < _fishTarget ? Math.Min(_fishTarget, _fishShown + step) : Math.Max(_fishTarget, _fishShown - step);
            _fish.text = IslandUi.Number(_fishShown);
        }

        private static void Resource(Label label, double value, ref double last)
        {
            label.text = IslandUi.Number(value);
            var box = label.parent;
            box.EnableInClassList(UiClasses.Resource.Empty, value < 1);
            if (!double.IsNaN(last) && value > last + 1e-6)
            {
                Bump(box, UiClasses.Resource.Bump);
            }

            last = value;
        }

        private static void Bump(VisualElement e, string bumpClass)
        {
            e.AddToClassList(bumpClass);
            e.schedule.Execute(() => e.RemoveFromClassList(bumpClass)).StartingIn(180);
        }

        private void ShowStatus(ProductionVillageSessionCore core)
        {
            IslandUi.SetVisible(_error, core.Error.Length > 0);
            _errorText.text = core.Error;
            if (core.Notice != _lastNotice)
            {
                _lastNotice = core.Notice;
                ShowToast(core.Notice);
            }
        }

        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message) || _toastLayer.Children().OfType<Label>().Any(l => l.text == message))
            {
                return;
            }

            var toast = IslandUi.Text(message, UiClasses.Toast.Root, UiClasses.Toast.Hidden);
            _toastLayer.Add(toast);
            while (_toastLayer.childCount > 3)
            {
                _toastLayer.RemoveAt(0);
            }

            toast.schedule.Execute(() => toast.RemoveFromClassList(UiClasses.Toast.Hidden)).StartingIn(16);
            toast.schedule.Execute(() => toast.AddToClassList(UiClasses.Toast.Hidden)).StartingIn((long)(ToastSeconds * 1000));
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn((long)(ToastSeconds * 1000) + 300);
        }

        // ------------------------------------------------------------------
        // 盤面を隠さない下部の施設選択欄
        // ------------------------------------------------------------------

        private void RebuildQuickDock()
        {
            if (_dockTabs == null || _dockScroll == null) return;
            _dockTabs.Clear();
            var categories = new (string Key, string Label)[]
            {
                ("all", "すべて"),
                ("gather", "採集"),
                ("cook", "加工"),
                ("amp", "増幅・物流")
            };
            foreach (var cat in categories)
            {
                var key = cat.Key;
                var tab = new Button(() =>
                {
                    _dockCategory = key;
                    RebuildQuickDock();
                })
                { text = cat.Label };
                tab.AddToClassList(ProductionUiNames.DockTabClass);
                tab.EnableInClassList(ProductionUiNames.DockTabActiveClass, _dockCategory == key);
                _dockTabs.Add(tab);
            }

            _dockScroll.Clear();
            var view = _session.Core.View;
            foreach (var def in IslandCatalog.Facilities.Where(MatchesDockCategory).OrderBy(d => (int)d.Kind))
            {
                _dockScroll.Add(BuildDockSlot(def, view));
            }
        }

        private bool MatchesDockCategory(FacilityDef def) => _dockCategory switch
        {
            "gather" => def.Kind == FacilityKind.Fishery || def.Kind == FacilityKind.Saltworks || def.Kind == FacilityKind.KelpFarm,
            "cook" => def.Kind == FacilityKind.Preparation || def.Kind == FacilityKind.Kamaboko || def.Kind == FacilityKind.Chikuwa || def.Kind == FacilityKind.Drying || def.Kind == FacilityKind.Oden,
            "amp" => def.Kind >= FacilityKind.Warehouse,
            _ => true
        };

        private VisualElement BuildDockSlot(FacilityDef def, VillageView view)
        {
            var electricityCost = view?.BuildElectricityCost(def.Kind) ?? def.ElectricityCost;
            var shortE = view != null && !view.ResourcesUnlimited && view.Electricity < electricityCost;
            var shortW = view != null && !view.ResourcesUnlimited && view.Water < def.WaterCost;
            var slot = new Button(() =>
            {
                if (_session.Core.View == null)
                {
                    ShowToast("村を読み込めていないよ");
                }
                else if (shortE)
                {
                    ShowToast("電気が足りないよ。スクリーンタイムを記録してためよう");
                }
                else if (shortW)
                {
                    ShowToast("水が足りないよ。水分補給を記録してためよう");
                }
                else
                {
                    Placement.Build(def.Kind);
                }
            });
            slot.AddToClassList(ProductionUiNames.DockSlotClass);
            slot.userData = def.Kind;
            slot.tooltip = "同種施設を建てるたびに、次の建設費が電気1ずつ増えます";

            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(UiClasses.Thumb.Root);
            thumb.AddToClassList(ProductionUiNames.DockSlotThumbClass);
            IslandUi.SetThumb(thumb, UiClasses.Thumb.Of(def.Kind));
            slot.Add(thumb);

            slot.Add(IslandUi.Text(def.Name, ProductionUiNames.DockSlotNameClass));

            var cost = IslandUi.Cost();
            IslandUi.AddCost(cost, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, electricityCost, shortE);
            IslandUi.AddCost(cost, UiClasses.Icon.Water, UiClasses.Tint.Water, def.WaterCost, shortW);
            slot.Add(cost);
            return slot;
        }

        // ------------------------------------------------------------------
        // 置き場所を選ぶ間
        // ------------------------------------------------------------------

        private void ShowPlacementTray()
        {
            var placement = Placement;
            var placing = placement != null && placement.Mode != VillagePlacementMode.Browse;
            IslandUi.SetVisible(_tools, !placing && !_industrialMode);
            IslandUi.SetVisible(_insightBanner, !placing && !_industrialMode);
            IslandUi.SetVisible(_quickDock, !placing && _industrialMode);
            IslandUi.SetVisible(_tray, placing);
            if (!placing)
            {
                return;
            }

            var choosingSpot = placement.Mode == VillagePlacementMode.Build || placement.Mode == VillagePlacementMode.MovePreview;
            var bad = placement.HasCandidate && placement.Reason.Length > 0;
            _reason.text = placement.Reason.Length > 0 ? placement.Reason
                : placement.Mode == VillagePlacementMode.Build && !placement.HasCandidate ? "置きたい場所をタップしてね"
                : placement.HasCandidate ? "ここでよければ決定を押してね" : "";
            var combo = _world.PlacementCombo;
            if (placement.HasCandidate && placement.Reason.Length == 0 && combo != null)
            {
                _reason.text += $"\n配置後のコンボ {combo.Candidate.ComboCount} / 4方向・生産量 ×{combo.Candidate.ComboMultiplier:0.##}";
                foreach (var change in combo.Changes.Take(3))
                    _reason.text += $"\n周囲の施設：×{VillageComboRules.Multiplier(change.Before):0.##} から ×{VillageComboRules.Multiplier(change.After):0.##}";
            }
            _reason.EnableInClassList(ProductionUiNames.HintBad, bad);
            IslandUi.SetVisible(_reason, _reason.text.Length > 0);
            IslandUi.SetVisible(_confirm, choosingSpot);
            _confirm.SetEnabled(placement.HasCandidate && placement.Reason.Length == 0);
            IslandUi.SetVisible(_thumb, choosingSpot);
            var thumbClass = choosingSpot ? UiClasses.Thumb.Of(placement.Kind) : null;
            if (thumbClass != _thumbClass)
            {
                _thumbClass = thumbClass;
                IslandUi.SetThumb(_thumb, thumbClass);
            }
        }

        private void ConfirmPlacement()
        {
            var placement = Placement;
            if (placement == null)
            {
                return;
            }

            var moving = placement.Mode == VillagePlacementMode.MovePreview;
            var name = IslandCatalog.Get(placement.Kind).Name;
            if (placement.Confirm())
            {
                ShowToast(moving ? $"{name}を移したよ" : $"{name}を建てたよ！");
                RebuildQuickDock();
            }
        }

        private void CancelPlacement()
        {
            if (Placement?.Mode == VillagePlacementMode.MovePreview)
            {
                Placement.CancelPreview();
            }
            else
            {
                Placement?.Cancel();
            }
        }

        public bool Blocks(Vector2 screen)
        {
            if (_root?.panel == null)
            {
                return true;
            }

            var p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            for (var hit = _root.panel.Pick(p); hit != null; hit = hit.parent)
            {
                if (hit == _app)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // シート
        // ------------------------------------------------------------------

        private void Open(string title, string tab, string pageName = null)
        {
            Placement?.Cancel();
            HidePage();
            _report?.Close();
            _sheetPageName = pageName;
            _sheet.EnableInClassList(ProductionUiNames.FacilitySheetClass, pageName == FacilityPage);
            _content = _sheetContent;
            _content.Clear();
            _title.text = title;
            _refreshSheet = null;
            _content.scrollOffset = Vector2.zero;
            SetTab(tab);
            if (!_sheetOpen)
            {
                _sheetOpen = true;
                IslandUi.SetVisible(_sheet, true);
                _sheet.schedule.Execute(() => _sheet.RemoveFromClassList(UiClasses.Sheet.Closed)).StartingIn(16);
            }
        }

        /// <summary>
        /// 工房まわりのウィンドウ (施設・図面・港・工房) を、生産レポートと同じ枠の全画面ページで開く。
        /// 中身の組み立て (Card・Action など) はシートと共通で、ページの中では工業風の見た目になる (ProductionApp.uss)。
        /// </summary>
        private void OpenPage(string micro, string title, string tab)
        {
            BeginPage(_pageContent, tab);
            _pageMicro.text = micro;
            _pageTitle.text = title;
            _sheetOpen = _pageOpen = true;
            IslandUi.SetVisible(_page, true);
        }

        /// <summary>
        /// メニューと記録を、ホームと同じ手触りのやわらかい全画面ページで開く (氷色の地に、厚みのある白いカード)。
        /// 下のタブはページの上に残るので、タブでそのまま行き来できる。
        /// </summary>
        private void OpenSoft(string title, string sub, string tab)
        {
            BeginPage(_softContent, tab);
            _softTitle.text = title;
            _softSub.text = sub ?? "";
            IslandUi.SetVisible(_softSub, !string.IsNullOrEmpty(sub));
            _sheetOpen = _softOpen = true;
            IslandUi.SetVisible(_softPage, true);
        }

        /// <summary>全画面ページを開く前に、シート・ほかのページ・生産レポートを閉じて、中身の置き場を切り替える。</summary>
        private void BeginPage(ScrollView content, string tab)
        {
            Placement?.Cancel();
            _sheetPageName = null;
            if (_sheetOpen && !_pageOpen && !_softOpen)
            {
                _sheet.AddToClassList(UiClasses.Sheet.Closed);
                IslandUi.SetVisible(_sheet, false);
            }

            HidePage();
            _report?.Close();
            _content = content;
            _content.Clear();
            _refreshSheet = null;
            _content.scrollOffset = Vector2.zero;
            SetTab(tab);
        }

        private void HidePage()
        {
            if (!_pageOpen && !_softOpen) return;
            _pageOpen = _softOpen = false;
            _sheetOpen = false;
            IslandUi.SetVisible(_page, false);
            IslandUi.SetVisible(_softPage, false);
        }

        private void Live(Action refresh)
        {
            _refreshSheet = refresh;
            _nextSheetRefresh = Time.unscaledTime + SheetRefreshSeconds;
            refresh();
        }

        private void CloseSheetOnly()
        {
            if (_pageOpen || _softOpen)
            {
                HidePage();
                return;
            }

            if (!_sheetOpen) return;
            _sheetOpen = false;
            _sheetPageName = null;
            _sheet.AddToClassList(UiClasses.Sheet.Closed);
            _sheet.schedule.Execute(() =>
            {
                if (!_sheetOpen) IslandUi.SetVisible(_sheet, false);
            }).StartingIn(240);
        }

        public void Close()
        {
            Placement?.Cancel();
            _world.SelectedId = 0;
            _refreshSheet = null;
            SetTab(ProductionUiNames.HomeTab);
            CloseSheetOnly();
            _report?.Close();
        }

        private void SetTab(string name)
        {
            _activeTab?.RemoveFromClassList(UiClasses.Tab.Active);
            _activeTab = name == null ? null : _root.Q<Button>(name);
            _activeTab?.AddToClassList(UiClasses.Tab.Active);
        }

        /// <summary>見出し付きの節。見出しの印は絵文字ではなく、デザインシステムのアイコン (<paramref name="icon"/>) を使う。</summary>
        private VisualElement Card(string title = null, string icon = null, string tint = null)
        {
            var card = new VisualElement();
            card.AddToClassList(UiClasses.Card);
            card.AddToClassList(UiClasses.Elevated);
            card.AddToClassList(ProductionUiNames.Card);
            if (!string.IsNullOrEmpty(title))
            {
                if (icon == null)
                {
                    card.Add(IslandUi.Text(title, ProductionUiNames.CardTitle));
                }
                else
                {
                    var head = new VisualElement { pickingMode = PickingMode.Ignore };
                    head.AddToClassList(ProductionUiNames.CardHead);
                    head.Add(IslandUi.Icon(icon, tint ?? UiClasses.Tint.Point));
                    head.Add(IslandUi.Text(title, ProductionUiNames.CardTitle));
                    card.Add(head);
                }
            }

            _content.Add(card);
            return card;
        }

        private static Button Action(VisualElement parent, string title, Action onClick, string iconClass = null, bool primary = true, bool enabled = true)
        {
            var actions = parent.Q(className: ProductionUiNames.CardActions);
            if (actions == null)
            {
                actions = new VisualElement();
                actions.AddToClassList(ProductionUiNames.CardActions);
                parent.Add(actions);
            }

            var b = new Button(onClick);
            b.AddToClassList(UiClasses.Button);
            b.AddToClassList(primary ? UiClasses.Btn.Raised : UiClasses.Btn.Ghost);
            if (iconClass != null)
            {
                var icon = IslandUi.Icon(iconClass, primary ? null : UiClasses.Tint.Primary);
                icon.AddToClassList(UiClasses.Btn.Icon);
                b.Add(icon);
            }

            b.Add(IslandUi.Text(title));
            b.SetEnabled(enabled);
            actions.Add(b);
            return b;
        }

        private static VisualElement Badge(string text, string variant = null)
        {
            var badge = IslandUi.Text(text, UiClasses.Badge.Root);
            if (variant != null)
            {
                badge.AddToClassList(variant);
            }

            return badge;
        }

        private static Label Stat(string text) => IslandUi.Text(text, ProductionUiNames.Stat);

        private void Empty(VisualElement parent, string iconClass, string text)
        {
            var empty = new VisualElement { pickingMode = PickingMode.Ignore };
            empty.AddToClassList(ProductionUiNames.Empty);
            empty.Add(IslandUi.IconBadge(iconClass, UiClasses.Tint.Sub));
            empty.Add(IslandUi.Text(text, ProductionUiNames.EmptyText));
            parent.Add(empty);
        }

        // ---- 設備カタログ & 連携図面 (ShowPlacement) ----

        public void ShowPlacement()
        {
            OpenPage("BLUEPRINTS", "施設と連携図面", null);
            var view = _session.Core.View;

            // おすすめ連携図面カード
            var blueprintCard = Card("施設群の図面 (全体表示の間に追加生産)", UiClasses.Icon.Production);
            blueprintCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Kamaboko, UiClasses.Tint.Point),
                "かまぼこ黄金ライン",
                "[漁場] - [塩田] - [漁場] を縦または横に並べる",
                Badge(GroupStatus(view, Item.Kamaboko), UiClasses.Badge.Accent)));
            blueprintCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Chikuwa, UiClasses.Tint.Point),
                "ちくわ3連ライン",
                "[漁場] を3棟まっすぐ並べる",
                Badge(GroupStatus(view, Item.Chikuwa), UiClasses.Badge.Accent)));
            blueprintCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.DriedFish, UiClasses.Tint.Point),
                "干物L字ライン",
                "[塩田] の縦と横に [漁場] をL字に並べる",
                Badge(GroupStatus(view, Item.DriedFish), UiClasses.Badge.Accent)));
            blueprintCard.Children().Last().AddToClassList(UiClasses.Row.Last);

            var shelf = new VisualElement();
            shelf.AddToClassList(ProductionUiNames.Shelf);
            _content.Add(IslandUi.Text("同種施設を建てるたびに、次の建設費が電気1ずつ増えます。撤去しても費用は戻りません。", ProductionUiNames.Subtle));
            _content.Add(shelf);
            foreach (var def in IslandCatalog.Facilities.OrderBy(d => VillageGame.Supported(d.Kind) ? 0 : 1).ThenBy(d => (int)d.Kind))
            {
                shelf.Add(ShelfCard(def, view));
            }
        }

        private VisualElement ShelfCard(FacilityDef def, VillageView view)
        {
            var supported = VillageGame.Supported(def.Kind);
            var electricityCost = view?.BuildElectricityCost(def.Kind) ?? def.ElectricityCost;
            var shortElectricity = view != null && !view.ResourcesUnlimited && view.Electricity < electricityCost;
            var shortWater = view != null && !view.ResourcesUnlimited && view.Water < def.WaterCost;

            var card = new VisualElement();
            card.AddToClassList(UiClasses.ShelfCard.Root);
            card.EnableInClassList(UiClasses.ShelfCard.Locked, !supported);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(UiClasses.ShelfCard.Thumb);
            IslandUi.SetThumb(thumb, UiClasses.Thumb.Of(def.Kind));
            card.Add(thumb);
            card.Add(IslandUi.Text(def.Name, UiClasses.ShelfCard.Name));

            var recipe = def.Recipes.FirstOrDefault();
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList(ProductionUiNames.ShelfRecipe);
            if (recipe != null)
            {
                foreach (var input in recipe.Inputs)
                {
                    line.Add(IslandUi.Count(IslandUi.IconClass(input.Item), UiClasses.Tint.Fish, $"×{input.Count}"));
                }

                line.Add(IslandUi.Icon(UiClasses.Icon.Chevrons, UiClasses.Tint.Primary));
                line.Add(IslandUi.Count(IslandUi.IconClass(recipe.Output.Item), UiClasses.Tint.Fish, recipe.Output.Count > 1 ? $"×{recipe.Output.Count}" : ""));
            }
            else
            {
                var effectNote = def.Kind == FacilityKind.Warehouse ? "置き場 20"
                    : def.Kind == FacilityKind.IceHouse ? "周囲容量 ×2"
                    : def.Kind == FacilityKind.Sorter ? "配送距離拡張"
                    : def.Kind == FacilityKind.Dormitory ? "全体速度 +10%"
                    : def.Kind == FacilityKind.Research ? "研究効率 +15%"
                    : "周囲速度 +25%";
                line.Add(IslandUi.Count(IslandUi.EffectIcon(def), UiClasses.Tint.Primary, effectNote));
            }

            card.Add(line);
            if (supported)
            {
                var cost = IslandUi.Cost();
                cost.AddToClassList(ProductionUiNames.ShelfCost);
                IslandUi.AddCost(cost, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, electricityCost, shortElectricity);
                IslandUi.AddCost(cost, UiClasses.Icon.Water, UiClasses.Tint.Water, def.WaterCost, shortWater);
                card.Add(cost);
            }
            else
            {
                var soon = Badge("準備中", UiClasses.Badge.Accent);
                soon.AddToClassList(ProductionUiNames.ShelfSoon);
                card.Add(soon);
            }

            card.RegisterCallback<ClickEvent>(_ =>
            {
                if (!supported)
                {
                    ShowToast($"{def.Name}はまだ準備中だよ");
                }
                else if (_session.Core.View == null)
                {
                    ShowToast("村を読み込めていないよ");
                }
                else if (shortElectricity)
                {
                    ShowToast("電気が足りないよ。スクリーンタイムを記録してためよう");
                }
                else if (shortWater)
                {
                    ShowToast("水が足りないよ。水分補給を記録してためよう");
                }
                else
                {
                    Close();
                    SetIndustrialMode(true);
                    Placement.Build(def.Kind);
                }
            });
            return card;
        }

        // ---- 施設詳細 ----

        public void ShowFacility(int id)
        {
            var fullView = _session.Core.View;
            var node = fullView?.Nodes.Concat(fullView.UpperNodes).FirstOrDefault(a => a.Id == id);
            if (node == null)
            {
                return;
            }

            _world.CameraController?.SetMaximumZoom(fullView.MaximumZoom);
            _world.CameraController?.SetLevel((VillageZoomLevel)node.Zoom);
            var def = IslandCatalog.Get(node.Kind);
            Open(node.Zoom>1?$"{node.Zoom}ズーム · {def.Name}":def.Name, null, FacilityPage);
            _world.SelectedId = id;
            var top = Card();
            var header = new VisualElement();
            header.AddToClassList(ProductionUiNames.Facility);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(ProductionUiNames.FacilityThumb);
            IslandUi.SetThumb(thumb, UiClasses.Thumb.Of(node.Kind));
            header.Add(thumb);
            var body = new VisualElement();
            body.AddToClassList(ProductionUiNames.FacilityBody);
            var status = new VisualElement();
            var effortLabel = IslandUi.Text("", ProductionUiNames.FacilityEffort);
            var progress = new ProgressBar { lowValue = 0, highValue = 1 };
            progress.AddToClassList(UiClasses.Progress.Root);
            progress.AddToClassList(UiClasses.Progress.Eco);
            progress.AddToClassList(ProductionUiNames.FacilityProgress);
            body.Add(effortLabel);
            body.Add(status);
            body.Add(progress);
            header.Add(body);
            top.Add(header);

            var recipe = def.Recipes.FirstOrDefault();
            if (recipe != null)
            {
                var recipeLine = new VisualElement();
                recipeLine.AddToClassList(UiClasses.Recipe.Root);
                IslandUi.FillRecipe(recipeLine, node.Kind, recipe);
                top.Add(recipeLine);
                var bonusText = node.InSynergy ? "施設群が成立しているよ。全体表示の間は追加生産が動く。" : "";
                if (node.Amplified) bonusText += "増幅器の近くなので、速さが +25%。";
                top.Add(IslandUi.Text(bonusText + "エフォートに応じて加工時にガスを消費。近くの施設へ自動でとどけるよ", ProductionUiNames.Subtle));
            }
            else
            {
                top.Add(IslandUi.Text(node.Status, ProductionUiNames.Subtle));
            }

            if(node.Zoom==1)
            {
                Action(top, "動かす", () => { Close(); Placement.MoveSelect(); Placement.Select(id); }, UiClasses.Icon.Move);
                Action(top, "片付ける", () =>
                {
                    if(_session.Core.Remove(id,ProductionVillageSession.UtcNow)){ShowToast($"{def.Name}を片付けたよ");Close();}
                    else ShowToast("品物や配達が残っていると片付けられないよ");
                }, UiClasses.Icon.Demolish, primary:false);
            }
            else
            {
                top.Add(IslandUi.Text("下位の同種施設2×2から成立。材料は内部4施設の在庫から受け取り、各階層の生産は表示段階にかかわらず続きます。",ProductionUiNames.Subtle));
                Action(top,"内部を見る",()=>{Close();_world.CameraController?.SetLevel((VillageZoomLevel)(node.Zoom-1));},UiClasses.Icon.Move);
            }

            VillageWarehousePanel warehousePanel = null;
            VillageEffortPanel effortPanel = null;
            VillageComboPanel comboPanel = null;
            if (VillageComboRules.Supports(node.Kind))
            {
                comboPanel = new VillageComboPanel(Card());
            }
            if (node.Kind == FacilityKind.Warehouse)
            {
                warehousePanel = new VillageWarehousePanel(Card(), (nodeId, value) =>
                {
                    var ok = _session.Core.SetWarehouseCapacity(nodeId, value, ProductionVillageSession.UtcNow);
                    ShowToast(ok ? _session.Core.Notice : string.IsNullOrEmpty(_session.Core.Error) ? _session.Core.Notice : _session.Core.Error);
                    if (ok) ShowFacility(nodeId);
                    return ok;
                });
            }
            else
            {
                effortPanel = new VillageEffortPanel(Card(), (nodeId, effort) =>
                {
                    if (_session.Core.SetEffort(nodeId, effort, ProductionVillageSession.UtcNow))
                    {
                        ShowToast(_session.Core.Notice);
                        ShowFacility(nodeId);
                        return true;
                    }
                    ShowToast(string.IsNullOrEmpty(_session.Core.Error) ? _session.Core.Notice : _session.Core.Error);
                    return false;
                });
            }
            var stockCard = Card("置いてある品物");
            var capacity = IslandUi.Text("", ProductionUiNames.Subtle);
            stockCard.Add(capacity);
            var items = new VisualElement();
            items.AddToClassList(ProductionUiNames.Items);
            stockCard.Add(items);

            Live(() =>
            {
                var view = _session.Core.View;
                var n = view?.Nodes.Concat(view.UpperNodes).FirstOrDefault(a => a.Id == id);
                if (n == null)
                {
                    Close();
                    return;
                }

                status.Clear();
                effortLabel.text = $"エフォート {n.Effort}" + (n.ProductionSeconds > 0 ? $"  ·  次回 {n.ProductionSeconds:0.##}秒" : "");
                comboPanel?.Render(n);
                status.Add(StatusPill(n.Status));
                progress.value = (float)n.Progress;
                IslandUi.SetVisible(progress, n.Progress > 0);
                items.Clear();
                var stock = n.Stock.Where(p => p.Value > 0).ToList();
                var incoming = view.Cargo.Count(c => c.To == id);
                capacity.text = $"保管 {n.Total} / {n.Capacity}個  ·  配送待ち {incoming}個";
                effortPanel?.Render(n, view.Gas);
                warehousePanel?.Render(n, incoming);
                if (stock.Count == 0)
                {
                    Empty(items, UiClasses.Icon.StorageFull, incoming > 0 ? $"とどく途中の品物が {incoming} 個あるよ" : "まだ何も置いていないよ");
                }
                else
                {
                    foreach (var p in stock)
                    {
                        var row=ItemCard(p.Key,p.Value,null);
                        AddDelivery(row,n.Id,p.Key,p.Value,()=>ShowFacility(id));
                        items.Add(row);
                    }
                }
            });
        }

        private static VisualElement StatusPill(string status)
        {
            var (text, icon, variant) = status switch
            {
                "生産中" => ("つくっているよ", UiClasses.Icon.Production, (string)null),
                "待機" => ("出番を待っているよ", UiClasses.Icon.Clock, null),
                "受け取り待ち" => ("品物を受け取るよ", UiClasses.Icon.Check, null),
                "出力の置き場が満杯" => ("置き場がいっぱい", UiClasses.Icon.StorageFull, UiClasses.Status.Warn),
                "材料を待っています" => ("材料を待っている", UiClasses.Icon.MaterialWait, UiClasses.Status.Warn),
                "ガスを待っています" => ("ガスが切れている", UiClasses.Icon.GasEmpty, UiClasses.Status.Bad),
                _ => (status, UiClasses.Icon.Electricity, null),
            };
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList(UiClasses.Status.Root);
            if (variant != null)
            {
                pill.AddToClassList(variant);
            }

            var i = IslandUi.Icon(icon, null);
            i.AddToClassList(UiClasses.Status.Icon);
            pill.Add(i);
            pill.Add(IslandUi.Text(text, UiClasses.Status.Label));
            return pill;
        }

        private static VisualElement ItemCard(Item item, int count, string note)
        {
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList(UiClasses.ItemCard.Root);
            card.EnableInClassList(UiClasses.ItemCard.Idle, count == 0);
            card.Add(IslandUi.Icon(IslandUi.IconClass(item), UiClasses.Tint.Fish));
            card.Add(IslandUi.Text(IslandUi.Number(count), UiClasses.ItemCard.Value));
            card.Add(IslandUi.Text(note ?? IslandCatalog.NameOf(item), UiClasses.ItemCard.Rate));
            return card;
        }

        // ---- 生産レポート & 出荷港 ----

        public void ShowReport()
        {
            if (_report != null)
            {
                CloseSheetOnly();
                _report.Open();
            }
            else
            {
                ShowSummary(false);
            }
        }

        public void ShowSummary(bool harbor)
        {
            if (!harbor)
            {
                ShowReport();
                return;
            }

            OpenPage("HARBOR", "出荷港と島の倉庫", null);
            var stats = Card();
            var delivery = Card("ランクと納品");
            var goal = IslandUi.Text("",ProductionUiNames.Subtle);goal.name="villageDeliveryGoal";delivery.Add(goal);
            var deliveryItems=new VisualElement();delivery.Add(deliveryItems);
            var rateSection = Card("村にある品物 (倉庫・施設在庫)");
            var items = new VisualElement();
            items.AddToClassList(ProductionUiNames.Items);

            rateSection.Add(items);
            Live(() =>
            {
                var view = _session.Core.View;
                if (view == null)
                {
                    return;
                }

                goal.text=$"ランク {view.Rank} · すり身・干物の納品 {view.SecondTierDelivered}個\n"+
                    (view.MaximumZoom>=3?"3ズーム解放済み":$"あと{VillageHierarchyRules.SecondTierPerRank-view.SecondTierDelivered}個でランク2・3ズーム解放");
                deliveryItems.Clear();
                foreach(var facility in view.Nodes.Concat(view.UpperNodes))
                    foreach(var stock in facility.Stock.Where(s=>s.Value>0))
                    {
                        var row=ItemCard(stock.Key,stock.Value,$"{facility.Zoom}ズーム · {IslandCatalog.Get(facility.Kind).Name}");
                        AddDelivery(row,facility.Id,stock.Key,stock.Value,()=>ShowSummary(true));deliveryItems.Add(row);
                    }
                if(!deliveryItems.Children().Any())deliveryItems.Add(IslandUi.Text("在庫ができると納品できます。配送中の品物は到着を待ちます。",ProductionUiNames.Subtle));
                stats.Clear();
                var warehouses = view.Nodes.Count(n => n.Kind == FacilityKind.Warehouse);
                stats.Add(IslandUi.Row(
                    IslandUi.IconBadge(UiClasses.Icon.Boat, UiClasses.Tint.Point),
                    "出荷港の倉庫連携",
                    warehouses > 0 ? $"倉庫 {warehouses} 棟が稼働中（合計容量 {view.Nodes.Where(n => n.Kind == FacilityKind.Warehouse).Sum(n => n.Capacity):N0}個）" : "倉庫を建てると大量の品物を備蓄できるよ",
                    Badge(warehouses > 0 ? $"Lv.{warehouses}" : "未設置", UiClasses.Badge.Accent)));
                stats.Children().Last().AddToClassList(UiClasses.Row.Last);
                items.Clear();
                foreach (var item in (Item[])Enum.GetValues(typeof(Item)))
                {
                    var moving = view.Cargo.Count(c => c.Item == item);
                    items.Add(ItemCard(item, view.Totals.GetValueOrDefault(item), moving > 0 ? $"輸送中 {moving}" : null));
                }
            });
        }

        private void AddDelivery(VisualElement parent,int id,Item item,int count,Action refresh)
        {
            var request=Guid.NewGuid().ToString("N");
            var button=new Button(()=>
            {
                var ok=_session.Core.Deliver(id,item,count,request,ProductionVillageSession.UtcNow);
                ShowToast(ok?_session.Core.Notice:string.IsNullOrEmpty(_session.Core.Error)?_session.Core.Notice:_session.Core.Error);
                if(ok)refresh();
            }){text=$"{count}個納品",name=DeliveryButtonName(id,item)};
            button.AddToClassList(UiClasses.Button);button.AddToClassList(UiClasses.Btn.Raised);parent.Add(button);
        }
        public static string DeliveryButtonName(int id,Item item)=>"villageDeliver-"+id+"-"+item;

        // ---- 記録 ----

        private const float WeekBarMaxHeight = 220f, WeekBarMinHeight = 14f;
        private static readonly string[] WeekdayNames = { "日", "月", "火", "水", "木", "金", "土" };

        private void ShowRecord()
        {
            var health = _session.GetComponent<VillageHealthInput>();
            OpenSoft("記録", "歩いた分と、受け取った資源", ProductionUiNames.RecordTab);

            // 今日: 歩数と次のガスまでの輪、受け取った資源の札
            var todayCard = SoftCard("今日", UiClasses.Icon.Gas, UiClasses.Tint.Gas);
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.AddToClassList(ProductionUiNames.RecordToday);
            var ring = new StepRing();
            ring.AddToClassList(ProductionUiNames.RecordRing);
            var center = new VisualElement { pickingMode = PickingMode.Ignore };
            center.AddToClassList(ProductionUiNames.RecordRingCenter);
            var steps = IslandUi.Text("0", ProductionUiNames.RecordSteps);
            center.Add(steps);
            center.Add(IslandUi.Text("歩", ProductionUiNames.RecordStepsUnit));
            ring.Add(center);
            var hint = IslandUi.Text("", ProductionUiNames.RecordRingHint);
            top.Add(ring);
            top.Add(hint);
            todayCard.Add(top);

            var chips = new VisualElement { pickingMode = PickingMode.Ignore };
            chips.AddToClassList(ProductionUiNames.RecordChips);
            var gas = RecordChip(chips, UiClasses.Icon.Gas, UiClasses.Tint.Gas, "ガス");
            var electricity = RecordChip(chips, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, "電気");
            var water = RecordChip(chips, UiClasses.Icon.Water, UiClasses.Tint.Water, "水");
            todayCard.Add(chips);

            var stepStatus = IslandUi.Text("", ProductionUiNames.SoftNote);
            stepStatus.style.marginTop = 16;
            todayCard.Add(stepStatus);
            var check = Action(todayCard, "歩数を確かめる", () => health?.Request(), UiClasses.Icon.Check, primary: false, enabled: health != null);

            var weekCard = SoftCard("この 7 日", UiClasses.Icon.Clock, UiClasses.Tint.Primary);
            var week = new VisualElement { pickingMode = PickingMode.Ignore };
            week.AddToClassList(ProductionUiNames.RecordWeek);
            weekCard.Add(week);

            var historyCard = SoftCard("受け取った資源", UiClasses.Icon.Book, UiClasses.Tint.Primary);
            var history = new VisualElement { pickingMode = PickingMode.Ignore };
            historyCard.Add(history);

            string shown = null;
            Live(() =>
            {
                var summary = VillageRecordSummary.From(_session.Core.Records, VillageHealthInput.Day(DateTimeOffset.Now));
                steps.text = summary.TodaySteps.ToString("#,0");
                // 輪は今日の歩数の目安までの進み具合。ガスは 1 歩ごとにたまるので、輪の区切りとは関係しない
                ring.Progress = (float)summary.GoalProgress;
                var perStep = $"1 歩でガスが {VillageGame.GasPerStep} たまるよ。";
                hint.text = summary.StepsToGoal == 0
                    ? $"{perStep}今日の目安の {VillageRecordSummary.DailyStepGoal:#,0} 歩を歩いたよ"
                    : $"{perStep}目安の {VillageRecordSummary.DailyStepGoal:#,0} 歩まで、あと {summary.StepsToGoal:#,0} 歩";
                gas.text = "+" + IslandUi.Number(summary.TodayTotals.Gas);
                electricity.text = "+" + IslandUi.Number(summary.TodayTotals.Electricity);
                water.text = "+" + IslandUi.Number(summary.TodayTotals.Water);

                // 今日の歩数が届いていれば、つながりの説明と確かめるボタンは要らない
                var connected = summary.TodaySteps > 0;
                stepStatus.text = health?.Status ?? "歩数につながっていないよ";
                IslandUi.SetVisible(stepStatus, !connected);
                IslandUi.SetVisible(check.parent, !connected);

                // 7 日と履歴は、記録が変わったときだけ組み直す
                var key = $"{summary.Today}:{_session.Core.Records?.Entries?.Count ?? 0}";
                if (key == shown)
                {
                    return;
                }

                shown = key;
                BuildRecordWeek(week, summary);
                BuildRecordHistory(history, summary);
            });
        }

        private static Label RecordChip(VisualElement parent, string icon, string tint, string label)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList(ProductionUiNames.RecordChip);
            chip.Add(IslandUi.Icon(icon, tint));
            var value = IslandUi.Text("+0", ProductionUiNames.RecordChipValue);
            chip.Add(value);
            chip.Add(IslandUi.Text(label, ProductionUiNames.RecordChipLabel));
            parent.Add(chip);
            return value;
        }

        /// <summary>この 7 日の棒。受け取ったガス・電気・水を下から積み、いちばん多い日を高さの上限にする。記録の無い日は低い点。</summary>
        private static void BuildRecordWeek(VisualElement week, VillageRecordSummary summary)
        {
            week.Clear();
            var max = Math.Max(1, summary.Week.Max(d => d.Total));
            // 1 本も棒が無い週は、点と曜日だけの低い帯にする (空の大きな余白を見せない)
            week.EnableInClassList(ProductionUiNames.RecordWeekEmpty, summary.Week.All(d => d.Total <= 0));
            foreach (var day in summary.Week)
            {
                var col = new VisualElement { pickingMode = PickingMode.Ignore };
                col.AddToClassList(ProductionUiNames.RecordBarCol);
                col.EnableInClassList(ProductionUiNames.RecordBarToday, day.Day == summary.Today);
                if (day.Total <= 0)
                {
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList(ProductionUiNames.RecordBarDot);
                    col.Add(dot);
                }
                else
                {
                    var stack = new VisualElement { pickingMode = PickingMode.Ignore };
                    stack.AddToClassList(ProductionUiNames.RecordBarStack);
                    stack.style.height = Math.Max(WeekBarMinHeight, (float)(WeekBarMaxHeight * day.Total / max));
                    AddSegment(stack, ProductionUiNames.RecordBarGas, day.Gas);
                    AddSegment(stack, ProductionUiNames.RecordBarElectricity, day.Electricity);
                    AddSegment(stack, ProductionUiNames.RecordBarWater, day.Water);
                    col.Add(stack);
                }

                var label = day.Day == summary.Today ? "今日" : WeekdayNames[(int)DateOfDay(day.Day).DayOfWeek];
                col.Add(IslandUi.Text(label, ProductionUiNames.RecordBarLabel));
                week.Add(col);
            }

            static void AddSegment(VisualElement stack, string className, double amount)
            {
                if (amount <= 0) return;
                var segment = new VisualElement { pickingMode = PickingMode.Ignore };
                segment.AddToClassList(className);
                segment.style.flexGrow = (float)amount;
                stack.Add(segment);
            }
        }

        /// <summary>日ごとの履歴 (新しい日から 30 日)。同じ日の同じ種類はまとめて 1 行にする。</summary>
        private static void BuildRecordHistory(VisualElement history, VillageRecordSummary summary)
        {
            history.Clear();
            if (summary.Empty)
            {
                var empty = new VisualElement { pickingMode = PickingMode.Ignore };
                empty.AddToClassList(ProductionUiNames.RecordEmpty);
                var mascot = IslandUi.Icon(UiClasses.Icon.Penguin, UiClasses.Tint.Original);
                mascot.AddToClassList(ProductionUiNames.RecordEmptyMascot);
                empty.Add(mascot);
                empty.Add(IslandUi.Text("まだ記録がないよ。歩くとガスがたまるよ。水の写真やすれ違いでも資源がもらえるよ", ProductionUiNames.SoftNote));
                history.Add(empty);
                return;
            }

            foreach (var day in summary.History)
            {
                var title = day.Key == summary.Today ? "今日"
                    : day.Key == summary.Today - 1 ? "昨日"
                    : DateOfDay(day.Key).ToString("M月d日");
                history.Add(IslandUi.Text(title, ProductionUiNames.RecordDay));
                foreach (var line in day.GroupBy(l => (l.Resource, l.Title)))
                {
                    var (icon, tint) = RecordIcon(line.Key.Resource);
                    var row = new VisualElement { pickingMode = PickingMode.Ignore };
                    row.AddToClassList(ProductionUiNames.RecordLine);
                    row.Add(IslandUi.IconBadge(icon, tint));
                    row.Add(IslandUi.Text(line.Key.Title, ProductionUiNames.RecordLineTitle));
                    row.Add(IslandUi.Text("+" + IslandUi.Number(line.Sum(l => l.Amount)), ProductionUiNames.RecordLineAmount));
                    history.Add(row);
                }
            }
        }

        private static DateTime DateOfDay(int day) => new DateTime(1970, 1, 1).AddDays(day);

        private static (string Icon, string Tint) RecordIcon(VillageRecordResource resource) =>
            resource == VillageRecordResource.Water ? (UiClasses.Icon.Water, UiClasses.Tint.Water)
            : resource == VillageRecordResource.Electricity ? (UiClasses.Icon.Electricity, UiClasses.Tint.Electricity)
            : (UiClasses.Icon.Gas, UiClasses.Tint.Gas);

        private void OnDestroy()
        {
            if(_world!=null)_world.ZoomChanged-=OnZoomChanged;
            if (_encounters != null)
            {
                _encounters.Granted -= ShowToast;
            }
        }

        // ---- スキャン ----

        private void ShowScan()
        {
            Open("スキャン", ProductionUiNames.ScanTab);
            var scanner = _waterScan;
            var water = Card("水分補給");
            water.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Water, UiClasses.Tint.Water),
                "空のペットボトル", "飲みきったボトルの写真を AI が見て、受け付けられると水がたまるよ (1 日 1 回)", null));
            water.Children().Last().AddToClassList(UiClasses.Row.Last);
            var waterAction = Action(water, "水を撮る", () =>
            {
                Close();
                scanner.Open();
            }, UiClasses.Icon.Camera, enabled: scanner != null && scanner.enabled);
            waterAction.name = WaterActionName;
            water.Add(IslandUi.Text("写真は判定後に破棄し、村には保存しないよ", ProductionUiNames.Subtle));

            var screenTime = _screenTime;
            var screen = Card("きのうのスクリーンタイム");
            screen.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity), "スクリーンタイム", "使わなかった時間ぶん電気がたまるよ (1 日 1 回)", null));
            screen.Children().Last().AddToClassList(UiClasses.Row.Last);
            screen.Add(IslandUi.Text("設定 > スクリーンタイム で上の「日」を選び、きのうの棒を押してからスクショを撮ってね", ProductionUiNames.Subtle));
            var screenAction = Action(screen, "スクショを選ぶ", () => screenTime.Open(), UiClasses.Icon.Check,
                enabled: screenTime != null && screenTime.enabled && screenTime.IsSupported);
            screenAction.name = ScreenTimeActionName;
            screen.Add(IslandUi.Text("スクショは端末の中で読んで捨て、送らないよ", ProductionUiNames.Subtle));

            var encounters = _encounters;
            var encounter = Card("すれ違い");
            encounter.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "近くの人とすれ違う",
                $"アプリを開いた人どうしが近くにいると、ペンギンが来て資源をくれるよ (1 日 {EncounterLedger.PenguinsPerDay} 回まで)", null));
            encounter.Children().Last().AddToClassList(UiClasses.Row.Last);
            var encounterStatus = IslandUi.Text(encounters?.Status ?? "すれ違いにつながっていないよ", ProductionUiNames.Subtle);
            encounter.Add(encounterStatus);
            if (encounters != null && encounters.CanFakeEncounter)
            {
                var fake = Action(encounter, "合成の相手とすれ違う (開発用)", async () => ShowToast(await encounters.FakeEncounterAsync()), UiClasses.Icon.Penguin, primary: false);
                fake.name = FakeEncounterActionName;
            }

            Live(() => encounterStatus.text = encounters?.Status ?? "すれ違いにつながっていないよ");
        }

        // ---- 工房・図面管理 (ShowManagement) ----

        private void ShowManagement()
        {
            OpenPage("WORKSHOP", "ペンギン工房", ProductionUiNames.ManageTab);
            var view = _session.Core.View;

            var modeCard = Card("ペンギン工房", UiClasses.Icon.Build, UiClasses.Tint.Point);
            modeCard.Add(IslandUi.Text("島を真上から見ながら、画面下の一覧から施設を選んで建てられるよ。施設の並べ方や資源を運ぶ道を工夫して、ペンギンたちのものづくりを手伝おう。", ProductionUiNames.Subtle));
            Action(modeCard, "工房モードを開く", () =>
            {
                Close();
                SetIndustrialMode(true);
            }, UiClasses.Icon.Electricity);
            Action(modeCard, "生産レポートを見る", ShowReport, UiClasses.Icon.Production, primary: false);

            var synCard = Card("施設の並べ方ボーナス", UiClasses.Icon.Production);
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Kamaboko, UiClasses.Tint.Point),
                "かまぼこ黄金ライン",
                "[漁場] - [塩田] - [漁場] を直線に並べると、かまぼこを追加で作る",
                Badge(GroupStatus(view, Item.Kamaboko), UiClasses.Badge.Accent)));
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Chikuwa, UiClasses.Tint.Point),
                "ちくわ3連ライン",
                "[漁場] × 3 を直線に並べると、ちくわを追加で作る",
                Badge(GroupStatus(view, Item.Chikuwa), UiClasses.Badge.Accent)));
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.DriedFish, UiClasses.Tint.Point),
                "干物L字ライン",
                "[塩田] を角にして [漁場] × 2 をL字に置くと、干物を追加で作る",
                Badge(GroupStatus(view, Item.DriedFish), UiClasses.Badge.Accent)));
            synCard.Children().Last().AddToClassList(UiClasses.Row.Last);

            var infraCard = Card("稼働中の設備と増幅", UiClasses.Icon.Research);
            var dormBonus = (view?.Nodes.Count(n => n.Kind == FacilityKind.Dormitory) ?? 0) * .10;
            var labBonus = (view?.Nodes.Count(n => n.Kind == FacilityKind.Research) ?? 0) * .15;
            var amps = view?.Nodes.Count(n => n.Kind == FacilityKind.ElectricAmplifier || n.Kind == FacilityKind.WaterAmplifier || n.Kind == FacilityKind.GasAmplifier) ?? 0;
            var ices = view?.Nodes.Count(n => n.Kind == FacilityKind.IceHouse) ?? 0;
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "ペンギン宿舎", "1棟ごとに村全体の生産速度を上げる", Stat($"+{dormBonus * 100:0.#}%")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Research, UiClasses.Tint.Pearl), "研究所", "1棟ごとに村全体の生産速度を上げる", Stat($"+{labBonus * 100:0.#}%")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity), "増幅器 (電・水・ガス)", "縦横6マス以内の速度を上げる。重なる場合は最も高い効果を適用", Stat($"{amps} 棟")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Snowflake, UiClasses.Tint.Water), "氷室", "縦横6マス以内の保管容量を増加。効果は施設詳細で確認", Stat($"{ices} 棟")));
            infraCard.Children().Last().AddToClassList(UiClasses.Row.Last);
        }

        // ---- メニュー ----

        private void ShowMenu()
        {
            OpenSoft("メニュー", null, ProductionUiNames.MenuTab);

            // 上: 大きなペンギンと島のランク
            var view = _session.Core.View;
            var heroCard = SoftCard();
            var hero = new VisualElement { pickingMode = PickingMode.Ignore };
            hero.AddToClassList(ProductionUiNames.SoftHero);
            var mascot = new VisualElement { pickingMode = PickingMode.Ignore };
            mascot.AddToClassList(ProductionUiNames.SoftHeroMascot);
            mascot.Add(IslandUi.Icon(UiClasses.Icon.Penguin, UiClasses.Tint.Original));
            hero.Add(mascot);
            var heroText = new VisualElement { pickingMode = PickingMode.Ignore };
            heroText.style.flexShrink = 1;
            heroText.Add(IslandUi.Text("ペンギンの島", ProductionUiNames.SoftHeroName));
            if (view != null)
            {
                heroText.Add(IslandUi.Text($"島ランク {IslandRank(view).Rank}", ProductionUiNames.SoftHeroRank));
                heroText.Add(IslandUi.Text($"お魚 {IslandUi.Number(view.Totals.GetValueOrDefault(Item.Fish))}・施設 {view.Nodes.Count} 棟", ProductionUiNames.SoftNote));
            }

            hero.Add(heroText);
            heroCard.Add(hero);

            // 押せるタイル (2 列)
            var tiles = new VisualElement();
            tiles.AddToClassList(ProductionUiNames.SoftTiles);
            _content.Add(tiles);
            SoftTile(tiles, UiClasses.Icon.Help, UiClasses.Tint.Primary, "チュートリアル", "操作しながら、もう一度", () =>
            {
                Close();
                SetIndustrialMode(false);
                _tutorial?.Restart();
            });
            SoftTile(tiles, UiClasses.Icon.Book, UiClasses.Tint.Gas, "記録", "歩数と受け取った資源", ShowRecord);
            SoftTile(tiles, UiClasses.Icon.Production, UiClasses.Tint.Point, "生産レポート", "施設の動きと品物", ShowReport);
            SoftTile(tiles, UiClasses.Icon.Build, UiClasses.Tint.Primary, "ペンギン工房", "建てる・並べ方のボーナス", ShowManagement);
            SoftTile(tiles, UiClasses.Icon.Undo, UiClasses.Tint.Sub, "読み込み直す", "保存した村をもう一度読む", () => _session.RetryLoad());
            SoftTile(tiles, UiClasses.Icon.Flag, UiClasses.Tint.Sub, "ライセンス", "使っている素材の表記", ShowLicenses);

            // 遊び方: 絵とひとことの横並び
            var howTo = SoftCard("遊び方", UiClasses.Icon.Penguin, UiClasses.Tint.Original);
            var tips = new VisualElement { pickingMode = PickingMode.Ignore };
            tips.AddToClassList(ProductionUiNames.SoftTips);
            SoftTip(tips, UiClasses.Icon.Build, "一覧から選んで建てる");
            SoftTip(tips, UiClasses.Icon.TopDown, "真上から見て並べる");
            SoftTip(tips, UiClasses.Icon.Move, "タップして動かす");
            howTo.Add(tips);

            var data = SoftCard("データ", UiClasses.Icon.Lock, UiClasses.Tint.Sub);
            data.Add(IslandUi.Text("村はこの端末の中だけに保存するよ。写真や買い物の明細は保存しないよ", ProductionUiNames.SoftNote));

            ShowDebugCard();
        }

        /// <summary>
        /// 審査・デバッグ用。ガス・電気・水を減らさないモードの切り替え (全ビルド) と、
        /// 歩数を測れない環境 (Editor) で合成の歩数を入れる操作。
        /// </summary>
        private void ShowDebugCard()
        {
            var core = _session.Core;
            var free = core.FreeResources;
            var debug = SoftCard("デバッグ (審査用)", UiClasses.Icon.Warning, UiClasses.Tint.Point);
            debug.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity), "資源無限のモード",
                free ? "オン: 建設・生産でガス・電気・水が減らないよ" : "オフ: ふだんどおり、歩数・写真で集めた資源を使うよ",
                Badge(free ? "オン" : "オフ", UiClasses.Badge.Accent)));
            debug.Children().Last().AddToClassList(UiClasses.Row.Last);
            debug.Add(IslandUi.Text("材料と物流はふだんどおり動くよ。資源無限の村はふだんの村と別に保存するので、オフにすればもとの村に戻るよ", ProductionUiNames.SoftNote));
            Action(debug, free ? "ふだんの村に戻る" : "資源無限にする", () =>
            {
                _session.SetFreeResources(!free);
                ShowToast(core.Error.Length > 0 ? core.Error : core.Notice);
                ShowMenu();
            }, UiClasses.Icon.Electricity, primary: !free).name = DebugActionName;

            var health = _session.GetComponent<VillageHealthInput>();
            if (health != null && health.CanAddDevelopmentSteps)
            {
                Action(debug, $"歩数 +{DevelopmentSteps} (開発用)", () =>
                {
                    health.AddDevelopmentSteps(DevelopmentSteps);
                    ShowToast($"今日の歩数に {DevelopmentSteps} 歩を足したよ");
                }, UiClasses.Icon.Gas, primary: false);
            }
        }

        /// <summary>やわらかいページのカード。見出しの印はデザインシステムのアイコン。</summary>
        private VisualElement SoftCard(string title = null, string icon = null, string tint = null)
        {
            var card = new VisualElement();
            card.AddToClassList(ProductionUiNames.SoftCard);
            if (!string.IsNullOrEmpty(title))
            {
                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                head.AddToClassList(ProductionUiNames.SoftCardHead);
                if (icon != null)
                {
                    head.Add(IslandUi.Icon(icon, tint ?? UiClasses.Tint.Primary));
                }

                head.Add(IslandUi.Text(title, ProductionUiNames.SoftCardTitle));
                card.Add(head);
            }

            _content.Add(card);
            return card;
        }

        /// <summary>メニューの押せるタイル (大きなアイコンの丸・名前・ひとこと)。</summary>
        private static void SoftTile(VisualElement parent, string icon, string tint, string name, string sub, Action onClick)
        {
            var tile = new Button(onClick);
            tile.AddToClassList(ProductionUiNames.SoftTile);
            var circle = new VisualElement { pickingMode = PickingMode.Ignore };
            circle.AddToClassList(ProductionUiNames.SoftTileCircle);
            circle.Add(IslandUi.Icon(icon, tint));
            tile.Add(circle);
            tile.Add(IslandUi.Text(name, ProductionUiNames.SoftTileName));
            tile.Add(IslandUi.Text(sub, ProductionUiNames.SoftTileSub));
            parent.Add(tile);
        }

        private static void SoftTip(VisualElement parent, string icon, string text)
        {
            var tip = new VisualElement { pickingMode = PickingMode.Ignore };
            tip.AddToClassList(ProductionUiNames.SoftTip);
            tip.Add(IslandUi.IconBadge(icon, UiClasses.Tint.Primary));
            tip.Add(IslandUi.Text(text, ProductionUiNames.SoftTipText));
            parent.Add(tip);
        }

        /// <summary>同梱している外部の素材 (アイコン・フォント・ライブラリ) の著作権表示と許諾文。</summary>
        private void ShowLicenses()
        {
            Open("ライセンス", ProductionUiNames.MenuTab);
            foreach (var entry in ThirdPartyNotices.Entries)
            {
                var card = Card(entry.Name);
                card.Add(IslandUi.Text(entry.License, ProductionUiNames.Subtle));
                card.Add(IslandUi.Text(entry.Text, ProductionUiNames.Subtle));
            }
        }
    }
}
