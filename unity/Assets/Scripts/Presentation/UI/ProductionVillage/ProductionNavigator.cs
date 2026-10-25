using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 村のホームの UI。ポケモンスリープ風の温かい「ホームモード」と、
    /// エンドフィールド風の盤面直結「集成工業モード (見下ろし視点・クイック建設ドック・空間バッジ・協約核心レポート)」を切り替える。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionNavigator : MonoBehaviour
    {
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
        private Action _refreshSheet;
        private bool _sheetOpen;
        private bool _industrialMode;
        private string _dockCategory = "all";
        private float _nextRefresh, _nextSheetRefresh;
        private double _fishShown, _fishTarget, _fishSpeed;
        private double _lastGas = double.NaN, _lastElectricity = double.NaN, _lastWater = double.NaN;
        private string _lastNotice;
        private string _thumbClass;
        private readonly Dictionary<int, Button> _spatialBadges = new Dictionary<int, Button>();
        private readonly Dictionary<string, VisualElement> _groupMarks = new Dictionary<string, VisualElement>();

        public bool WorldInputEnabled => !_sheetOpen && (_report == null || !_report.IsOpen) && !_session.IsSuspended && _session.Core.View != null;
        public VillagePlacementController Placement => _world.Placement;
        public VillageWorldPresenter World => _world;

        private void Start()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _app = _root.Q(ProductionUiNames.Root);
            _sheet = _root.Q(ProductionUiNames.Sheet);
            _content = _root.Q<ScrollView>(ProductionUiNames.Content);
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
            Bind(ProductionUiNames.ZoomIn, () => _world?.CameraController?.Zoom(.85f));
            Bind(ProductionUiNames.ZoomOut, () => _world?.CameraController?.Zoom(1f/.85f));
            Bind(ProductionUiNames.FrameCamera, () => _world?.CameraController?.Frame());
            Bind(ProductionUiNames.InsightBanner, ShowReport);
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
            ShowResources(view);
            ShowStatus(core);
            ShowPlacementTray();
            var groupState = _root.Q<Label>(ProductionUiNames.GroupState);
            if (groupState != null) groupState.text = (view?.GroupViewActive == true ? "施設群" : "個別施設") + "・2本指で拡大・縮小";
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
        // モード切替 (ホーム ⇔ 集成工業モード) & カメラ操作
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
                _industrialSwitchLabel.text = enabled ? "ホームへ戻る" : "工業モード";
            }

            IslandUi.SetVisible(_tacticalBar, enabled);
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
                _topDownLabel.text = topDown ? "見下ろし ON" : "見下ろし OFF";
            }

            _topDownToggle?.EnableInClassList(ProductionUiNames.TacticalBtnActiveClass, topDown);
        }

        // ------------------------------------------------------------------
        // ホーム & 空間ステータスバッジ
        // ------------------------------------------------------------------

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
            var score = view.Produced + view.Nodes.Count * 15L;
            var rank = Math.Min(9, 1 + (int)(score / 60));
            var progress = rank >= 9 ? 1f : (score % 60) / 60f;
            if (_rankText != null) _rankText.text = $"{rank}";
            if (_rankProgress != null) _rankProgress.value = progress;

            // 集成工業バーのガス収支表示
            if (_tacticalGas != null)
            {
                _tacticalGas.text = $"🔥 消費 {view.GasPerMinute:0.#}/分 (理論 {view.TheoreticalGasPerMinute:0.#}/分)";
            }

            // 左下ボトルネック・連携診断バナー
            if (_insightText != null && _insightBanner != null)
            {
                var synCount = view.Synergies?.Count ?? 0;
                var prefix = synCount > 0 ? $"✨連携{synCount}件・" : "";
                _insightText.text = prefix + (view.BottleneckSummary ?? "稼働順調");
                _insightBanner.EnableInClassList(ProductionUiNames.InsightBannerWarnClass, view.HasBottleneck);
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
            foreach (var n in view.Nodes)
            {
                activeIds.Add(n.Id);
                if (!_spatialBadges.TryGetValue(n.Id, out var badge))
                {
                    var nodeId = n.Id;
                    badge = new Button(() => ShowFacility(nodeId));
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

                IslandUi.SetVisible(badge, true);
                var panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                badge.style.left = panelPos.x - 44f;
                badge.style.top = panelPos.y - 28f;

                var warn = n.Status == "ガスを待っています" || n.Status == "出力の置き場が満杯" || n.Status == "材料を待っています";
                badge.EnableInClassList(ProductionUiNames.SpatialBadgeWarnClass, warn);
                badge.EnableInClassList(ProductionUiNames.SpatialBadgeSynergyClass, !warn && (n.InSynergy || n.Amplified));

                var def = IslandCatalog.Get(n.Kind);
                var recipe = def.Recipes.FirstOrDefault();
                var label = warn ? (n.Status == "ガスを待っています" ? "! ガス不足" : n.Status == "出力の置き場が満杯" ? "! 満杯" : "Zz 材料待")
                    : _industrialMode && n.InSynergy ? $"✨{def.Name} {(int)(n.Progress * 100)}%"
                    : _industrialMode && n.Amplified ? $"⚡{def.Name} {(int)(n.Progress * 100)}%"
                    : recipe != null && n.Total > 0 ? $"{IslandCatalog.NameOf(recipe.Output.Item)} ×{n.Total}"
                    : _industrialMode ? def.Name : "";

                if (string.IsNullOrEmpty(label))
                {
                    IslandUi.SetVisible(badge, false);
                }
                else
                {
                    badge.text = label;
                }
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
                !_sheetOpen && !_session.IsSuspended && view?.GroupViewActive == true)
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
        // 下部クイック建設ドック (盤面を隠さない Endfield スタイル)
        // ------------------------------------------------------------------

        private void RebuildQuickDock()
        {
            if (_dockTabs == null || _dockScroll == null) return;
            _dockTabs.Clear();
            var categories = new (string Key, string Label)[]
            {
                ("all", "すべて(16)"),
                ("gather", "資源採集"),
                ("cook", "加工・調理"),
                ("amp", "⚡増幅・物流")
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
            var shortE = view != null && !view.Unlimited && view.Electricity < def.ElectricityCost;
            var shortW = view != null && !view.Unlimited && view.Water < def.WaterCost;
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

            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(UiClasses.Thumb.Root);
            thumb.AddToClassList(ProductionUiNames.DockSlotThumbClass);
            IslandUi.SetThumb(thumb, UiClasses.Thumb.Of(def.Kind));
            slot.Add(thumb);

            slot.Add(IslandUi.Text(def.Name, ProductionUiNames.DockSlotNameClass));

            var cost = IslandUi.Cost();
            IslandUi.AddCost(cost, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, def.ElectricityCost, shortE);
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
                : placement.HasCandidate ? "ここでよければ ✓" : "";
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

        private void Open(string title, string tab)
        {
            Placement?.Cancel();
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

        private void Live(Action refresh)
        {
            _refreshSheet = refresh;
            _nextSheetRefresh = Time.unscaledTime + SheetRefreshSeconds;
            refresh();
        }

        private void CloseSheetOnly()
        {
            if (!_sheetOpen) return;
            _sheetOpen = false;
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

        private VisualElement Card(string title = null)
        {
            var card = new VisualElement();
            card.AddToClassList(UiClasses.Card);
            card.AddToClassList(UiClasses.Elevated);
            card.AddToClassList(ProductionUiNames.Card);
            if (!string.IsNullOrEmpty(title))
            {
                card.Add(IslandUi.Text(title, ProductionUiNames.CardTitle));
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
            Open("工業設備カタログ ＆ 連携図面", null);
            var view = _session.Core.View;

            // おすすめ連携図面カード
            var blueprintCard = Card("📐 施設群の図面 (縮小中に追加生産)");
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
            _content.Add(shelf);
            foreach (var def in IslandCatalog.Facilities.OrderBy(d => VillageGame.Supported(d.Kind) ? 0 : 1).ThenBy(d => (int)d.Kind))
            {
                shelf.Add(ShelfCard(def, view));
            }
        }

        private VisualElement ShelfCard(FacilityDef def, VillageView view)
        {
            var supported = VillageGame.Supported(def.Kind);
            var shortElectricity = view != null && !view.Unlimited && view.Electricity < def.ElectricityCost;
            var shortWater = view != null && !view.Unlimited && view.Water < def.WaterCost;

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
                IslandUi.AddCost(cost, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, def.ElectricityCost, shortElectricity);
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
            var node = _session.Core.View?.Nodes.FirstOrDefault(a => a.Id == id);
            if (node == null)
            {
                return;
            }

            var def = IslandCatalog.Get(node.Kind);
            Open(def.Name, null);
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
            var progress = new ProgressBar { lowValue = 0, highValue = 1 };
            progress.AddToClassList(UiClasses.Progress.Root);
            progress.AddToClassList(UiClasses.Progress.Eco);
            progress.AddToClassList(ProductionUiNames.FacilityProgress);
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
                var bonusText = node.InSynergy ? "✨ 施設群が成立。縮小中の稼働状況は群マークで確認。 " : "";
                if (node.Amplified) bonusText += "⚡ 増幅エリア速度 +25% 適用中！ ";
                top.Add(IslandUi.Text(bonusText + "つくるたびにガスを 1 つかう。近くの施設へ自動でとどけるよ", ProductionUiNames.Subtle));
            }
            else
            {
                top.Add(IslandUi.Text(node.Status, ProductionUiNames.Subtle));
            }

            Action(top, "動かす", () =>
            {
                Close();
                Placement.MoveSelect();
                Placement.Select(id);
            }, UiClasses.Icon.Move);
            Action(top, "片付ける", () =>
            {
                if (_session.Core.Remove(id, ProductionVillageSession.UtcNow))
                {
                    ShowToast($"{def.Name}を片付けたよ");
                    Close();
                }
                else
                {
                    ShowToast("品物や配達が残っていると片付けられないよ");
                }
            }, UiClasses.Icon.Demolish, primary: false);

            var stockCard = Card("置いてある品物");
            var items = new VisualElement();
            items.AddToClassList(ProductionUiNames.Items);
            stockCard.Add(items);

            Live(() =>
            {
                var view = _session.Core.View;
                var n = view?.Nodes.FirstOrDefault(a => a.Id == id);
                if (n == null)
                {
                    Close();
                    return;
                }

                status.Clear();
                status.Add(StatusPill(n.Status));
                progress.value = (float)n.Progress;
                IslandUi.SetVisible(progress, n.Progress > 0);
                items.Clear();
                var stock = n.Stock.Where(p => p.Value > 0).ToList();
                var incoming = view.Cargo.Count(c => c.To == id);
                if (stock.Count == 0)
                {
                    Empty(items, UiClasses.Icon.StorageFull, incoming > 0 ? $"とどく途中の品物が {incoming} 個あるよ" : "まだ何も置いていないよ");
                }
                else
                {
                    foreach (var p in stock)
                    {
                        items.Add(ItemCard(p.Key, p.Value, null));
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

            Open("出荷港 ＆ 島内倉庫", null);
            var stats = Card();
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

                stats.Clear();
                var warehouses = view.Nodes.Count(n => n.Kind == FacilityKind.Warehouse);
                stats.Add(IslandUi.Row(
                    IslandUi.IconBadge(UiClasses.Icon.Boat, UiClasses.Tint.Point),
                    "出荷港の倉庫連携",
                    warehouses > 0 ? $"倉庫 {warehouses} 棟が稼働中（保管枠 +{warehouses * 20}）" : "倉庫を建てると大量の品物を備蓄できるよ",
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

        // ---- 記録 ----

        private void ShowRecord()
        {
            var health = _session.GetComponent<VillageHealthInput>();
            Open("記録", ProductionUiNames.RecordTab);
            var steps = Card();
            steps.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Gas, UiClasses.Tint.Gas), "歩数", "500 歩ごとにガスが 1 たまるよ", null));
            var stepStatus = IslandUi.Text(health?.Status ?? "歩数につながっていないよ", ProductionUiNames.Subtle);
            steps.Add(stepStatus);
            Action(steps, "歩数を確かめる", () => health?.Request(), UiClasses.Icon.Check, primary: false, enabled: health != null);

            var list = Card();
            Live(() =>
            {
                stepStatus.text = health?.Status ?? "歩数につながっていないよ";
                var entries = _session.Core.Records?.Entries;
                var count = entries?.Count ?? 0;
                if (list.userData is int shown && shown == count)
                {
                    return;
                }

                list.userData = count;
                list.Clear();
                list.Add(IslandUi.Text("受け取った資源", ProductionUiNames.CardTitle));
                if (count == 0)
                {
                    Empty(list, UiClasses.Icon.Book, "まだ記録がないよ。歩くとガスがたまるよ");
                    return;
                }

                foreach (var e in entries.AsEnumerable().Reverse().Take(100))
                {
                    var (icon, tint) = RecordIcon(e.Kind);
                    var date = new DateTime(1970, 1, 1).AddDays(e.Day).ToString("M月d日");
                    list.Add(IslandUi.Row(IslandUi.IconBadge(icon, tint), RecordTitle(e.Kind), date, Stat("+" + IslandUi.Number(e.Amount))));
                }

                list.Children().Last().AddToClassList(UiClasses.Row.Last);
            });
        }

        private static (string Icon, string Tint) RecordIcon(string kind) =>
            kind.Contains("水") ? (UiClasses.Icon.Water, UiClasses.Tint.Water)
            : kind.Contains("電気") ? (UiClasses.Icon.Electricity, UiClasses.Tint.Electricity)
            : (UiClasses.Icon.Gas, UiClasses.Tint.Gas);

        private static string RecordTitle(string kind)
        {
            var arrow = kind.IndexOf('→');
            return arrow > 0 ? kind.Substring(0, arrow).Trim() : kind;
        }

        // ---- スキャン ----

        private void ShowScan()
        {
            Open("スキャン", ProductionUiNames.ScanTab);
            var card = Card("記録して資源をためる");
            card.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Water, UiClasses.Tint.Water), "水分補給", "水が 3 たまるよ", Badge("準備中", UiClasses.Badge.Accent)));
            card.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity), "きのうのスクリーンタイム", "使わなかった時間ぶん電気がたまるよ", Badge("準備中", UiClasses.Badge.Accent)));
            card.Children().Last().AddToClassList(UiClasses.Row.Last);
            card.Add(IslandUi.Text("写真の読み取りがつながると使えるようになるよ。写真は村に保存しないよ", ProductionUiNames.Subtle));
        }

        // ---- 工業・図面管理 (ShowManagement) ----

        private void ShowManagement()
        {
            Open("集成工業・連携図面管理", ProductionUiNames.ManageTab);
            var view = _session.Core.View;

            var modeCard = Card("⚡ ペンギン集成工業モード");
            modeCard.Add(IslandUi.Text("盤面を見下ろし視点 (90°) に切り替え、画面下のクイック建設ドックから直接設備や増幅器を配置・配線できます。", ProductionUiNames.Subtle));
            Action(modeCard, "集成工業モードを起動する", () =>
            {
                Close();
                SetIndustrialMode(true);
            }, UiClasses.Icon.Electricity);
            Action(modeCard, "生産レポートを見る", ShowReport, UiClasses.Icon.Production, primary: false);

            var synCard = Card("✨ 施設の並び連携ボーナス (evolve ルール)");
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Kamaboko, UiClasses.Tint.Point),
                "かまぼこ黄金ライン",
                "[漁場] - [塩田] - [漁場] を直線に並べる → かまぼこ追加生成",
                Badge(GroupStatus(view, Item.Kamaboko), UiClasses.Badge.Accent)));
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.Chikuwa, UiClasses.Tint.Point),
                "ちくわ3連ライン",
                "[漁場] × 3 を直線に並べる → ちくわ追加生成",
                Badge(GroupStatus(view, Item.Chikuwa), UiClasses.Badge.Accent)));
            synCard.Add(IslandUi.Row(
                IslandUi.IconBadge(UiClasses.Icon.DriedFish, UiClasses.Tint.Point),
                "干物L字ライン",
                "[塩田] を角にして [漁場] × 2 をL字配置 → 干物追加生成",
                Badge(GroupStatus(view, Item.DriedFish), UiClasses.Badge.Accent)));
            synCard.Children().Last().AddToClassList(UiClasses.Row.Last);

            var infraCard = Card("🏭 稼働中のインフラ・増幅効果");
            var dorms = view?.Nodes.Count(n => n.Kind == FacilityKind.Dormitory) ?? 0;
            var labs = view?.Nodes.Count(n => n.Kind == FacilityKind.Research) ?? 0;
            var amps = view?.Nodes.Count(n => n.Kind == FacilityKind.ElectricAmplifier || n.Kind == FacilityKind.WaterAmplifier || n.Kind == FacilityKind.GasAmplifier) ?? 0;
            var ices = view?.Nodes.Count(n => n.Kind == FacilityKind.IceHouse) ?? 0;
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "ペンギン宿舎", "1棟ごとに島全体の作業速度 +10%", Stat($"+{dorms * 10}%")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Research, UiClasses.Tint.Pearl), "研究所", "1棟ごとに島全体の研究・加工速度 +15%", Stat($"+{labs * 15}%")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity), "増幅器 (電・水・ガス)", "周囲6×6マスの生産速度 +25%", Stat($"{amps} 棟")));
            infraCard.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Snowflake, UiClasses.Tint.Water), "氷室", "周囲6×6マスの保管容量 ×2", Stat($"{ices} 棟")));
            infraCard.Children().Last().AddToClassList(UiClasses.Row.Last);
        }

        // ---- メニュー ----

        private void ShowMenu()
        {
            Open("メニュー", ProductionUiNames.MenuTab);
            var howTo = Card("遊び方");
            howTo.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Build, UiClasses.Tint.Primary), "工業モードで建てる", "下のクイック建設ドックから選んで盤面をタップ", null));
            howTo.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.TopDown, UiClasses.Tint.Primary), "見下ろし視点 (90°)", "工業モード上部のボタンで真上からの配線視点に切替", null));
            howTo.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Move, UiClasses.Tint.Primary), "動かす", "施設をタップして、移したい場所をタップして ✓", null));
            howTo.Children().Last().AddToClassList(UiClasses.Row.Last);

            var data = Card("データ");
            data.Add(IslandUi.Text("村はこの端末の中だけに保存するよ。写真や買い物の明細は保存しないよ", ProductionUiNames.Subtle));
            Action(data, "保存をやり直す", () => _session.RetryLoad(), UiClasses.Icon.Undo, primary: false);
            Action(data, "記録を見る", ShowRecord, UiClasses.Icon.Book, primary: false);
            Action(data, "ライセンス", ShowLicenses, UiClasses.Icon.Help, primary: false);
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
