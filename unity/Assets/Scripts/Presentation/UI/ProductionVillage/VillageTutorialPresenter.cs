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
    /// 村のチュートリアル (操作しながら進む 7 段) の吹き出しと、押す場所を光らせる枠。
    /// 段の進み方は <see cref="VillageTutorial"/>、進み具合の保存は <see cref="ProductionVillageSessionCore.SetTutorial"/>。
    /// 合図はナビゲーターの今の状態 (工房モード・開いているページ・生産レポート・カメラの段階・施設の数) の変化から作り、
    /// 既存の操作の流れには手を入れない。
    /// </summary>
    public sealed class VillageTutorialPresenter
    {
        private const float PulseSeconds = .6f;

        private readonly ProductionNavigator _navigator;
        private readonly ProductionVillageSession _session;
        private readonly VisualElement _root, _coach;
        private readonly Label _step, _title, _body;
        private readonly Button _next, _skip;
        private VillageTutorial _tutorial;
        private VisualElement _target;

        private bool _wasIndustrial, _wasReport, _wasFacilityPage, _wasPage;
        private VillageZoomLevel _wasLevel;
        private int _wasNodes = -1;
        private bool _started;
        private int _scrolledFor;

        public bool Active => _tutorial != null && _tutorial.Active;

        public VillageTutorialPresenter(VisualElement root, ProductionNavigator navigator, ProductionVillageSession session)
        {
            _root = root;
            _navigator = navigator;
            _session = session;
            _coach = root.Q(ProductionUiNames.Coach);
            _step = root.Q<Label>(ProductionUiNames.CoachStep);
            _title = root.Q<Label>(ProductionUiNames.CoachTitle);
            _body = root.Q<Label>(ProductionUiNames.CoachBody);
            _next = root.Q<Button>(ProductionUiNames.CoachNext);
            _skip = root.Q<Button>(ProductionUiNames.CoachSkip);
            if (_next != null) _next.clicked += () => Send(VillageTutorialSignal.Next);
            if (_skip != null) _skip.clicked += () => Send(VillageTutorialSignal.Skip);
        }

        /// <summary>メニューの「チュートリアルをもう一度」。</summary>
        public void Restart()
        {
            if (_session.Core.View == null) return;
            _tutorial ??= new VillageTutorial(VillageTutorial.NotStarted);
            _tutorial.Start();
            _scrolledFor = 0;
            Remember();
            Save();
            CatchUp();
            Show();
        }

        public void Update()
        {
            var core = _session.Core;
            var view = core.View;
            if (_coach == null || view == null) return;

            if (!_started)
            {
                // 初めて村を開いたときだけ自動で始める。途中の段なら、その段から再開する
                _started = true;
                _tutorial = new VillageTutorial(core.Tutorial);
                if (_tutorial.Step == VillageTutorial.NotStarted)
                {
                    _tutorial.Start();
                    Save();
                }

                Remember();
                CatchUp();
            }

            if (!Active)
            {
                Hide();
                return;
            }

            // 画面の状態の変化を合図にする
            var industrial = _navigator.IndustrialMode;
            var facility = _navigator.OpenPageName == ProductionNavigator.FacilityPage;
            var page = _navigator.OpenPageName != null;
            var report = _navigator.ReportOpen;
            var level = _navigator.World?.CameraController?.Level ?? VillageZoomLevel.Overview;
            var nodes = view.Nodes.Count;
            if (industrial && !_wasIndustrial) Send(VillageTutorialSignal.IndustrialOpened);
            if (facility && !_wasFacilityPage) Send(VillageTutorialSignal.FacilityOpened);
            if (!page && _wasPage) Send(VillageTutorialSignal.PageClosed);
            if (nodes > _wasNodes && _wasNodes >= 0) Send(VillageTutorialSignal.Built);
            if (level != _wasLevel) Send(level == VillageZoomLevel.Detail ? VillageTutorialSignal.ZoomedDetail : VillageTutorialSignal.ZoomedOverview);
            if (report && !_wasReport) Send(VillageTutorialSignal.ReportOpened);
            if (!report && _wasReport) Send(VillageTutorialSignal.ReportClosed);
            Remember();

            if (Active) Show();
            else Hide();
        }

        private void Remember()
        {
            _wasIndustrial = _navigator.IndustrialMode;
            _wasFacilityPage = _navigator.OpenPageName == ProductionNavigator.FacilityPage;
            _wasPage = _navigator.OpenPageName != null;
            _wasReport = _navigator.ReportOpen;
            _wasLevel = _navigator.World?.CameraController?.Level ?? VillageZoomLevel.Overview;
            _wasNodes = _session.Core.View?.Nodes.Count ?? -1;
        }

        private void Send(VillageTutorialSignal signal)
        {
            if (_tutorial == null) return;
            var before = _tutorial.Step;
            if (!_tutorial.Handle(signal)) return;
            if (_tutorial.Step != before)
            {
                _scrolledFor = 0;
                Save();
                CatchUp();
            }

            if (Active) Show();
            else Hide();
        }

        /// <summary>段に入った時点ですでに条件を満たしていれば進める (再開したときや、先に操作していたとき)。</summary>
        private void CatchUp()
        {
            if (_tutorial == null) return;
            if (_tutorial.Step == VillageTutorial.OpenWorkshop && _navigator.IndustrialMode) Send(VillageTutorialSignal.IndustrialOpened);
            if (_tutorial.Step == VillageTutorial.Flow && _navigator.OpenPageName == null && !_tutorial.Halfway) Send(VillageTutorialSignal.PageClosed);
        }

        private void Save() => _session.Core.SetTutorial(_tutorial.Step, ProductionVillageSession.UtcNow);

        private void Show()
        {
            var step = _tutorial.Step;
            var (title, body, target, top, next) = Content(step);
            IslandUi.SetVisible(_coach, true);
            _coach.EnableInClassList(ProductionUiNames.CoachTopClass, top);
            _step.text = $"{step} / {VillageTutorial.Count}";
            _title.text = title;
            _body.text = body;
            IslandUi.SetVisible(_next, next);
            if (_next != null) _next.text = step == VillageTutorial.Welcome ? "はじめる" : "次へ";
            Highlight(target);
        }

        private void Hide()
        {
            IslandUi.SetVisible(_coach, false);
            Highlight(null);
        }

        /// <summary>段ごとの吹き出し。(見出し, 本文, 光らせる要素, 吹き出しを上に出すか, 次へを出すか)</summary>
        private (string, string, VisualElement, bool, bool) Content(int step)
        {
            switch (step)
            {
                case VillageTutorial.Welcome:
                    return ("ペンギンの工房へようこそ",
                        "歩くとガス、スマホを休めると電気、水を飲むと水がたまる。この 3 つの資源で、島の工場を動かそう。",
                        _root.Q(ProductionUiNames.Resources), false, true);
                case VillageTutorial.OpenWorkshop:
                    return ("工房モードを開こう", "右上の「工房モード」を押すと、施設を建てたり、品物の流れを見たりできるよ。",
                        _root.Q(ProductionUiNames.IndustrialSwitch), false, false);
                case VillageTutorial.TapFacility:
                    return ("施設を押してみよう", "島の上の施設を押すと、何を作って、どこへ送るかが分かるよ。",
                        null, true, false);
                case VillageTutorial.Flow:
                    return _tutorial.Halfway
                        ? ("矢印は品物の通り道", "作った品物は、8 マス以内にある、それを使う施設や倉庫へ自動で届くよ。矢印が流れていれば運ばれている。",
                            null, true, true)
                        : ("見終わったら閉じよう", "右上の閉じるボタンで、島に戻ろう。",
                            null, false, false);
                case VillageTutorial.Build:
                {
                    var (kind, reason) = Suggestion();
                    var name = IslandCatalog.Get(kind).Name;
                    // 段に入ったときだけ、勧める施設の札までスクロールする (毎回だと自分でスクロールできない)。光らせるのはその札だけ
                    var slot = _navigator.DockSlot(kind, _scrolledFor != step) ?? _root.Q(ProductionUiNames.QuickDock);
                    _scrolledFor = step;
                    return reason.Length == 0
                        ? ("施設を 1 つ建てよう", $"下の一覧で光っている「{name}」を選んで、島の上の置きたい場所を押そう。届く範囲とつながる相手が光るよ。",
                            slot, true, false)
                        : ("施設を 1 つ建てよう", $"いまは{reason}。資源がたまったら建ててみよう。", slot, true, true);
                }
                case VillageTutorial.Zoom:
                    return _tutorial.Halfway
                        ? ("全体表示に戻ろう", "「全体」を押すか、2 本指で縮めよう。全体表示の間は、施設群が追加で生産するよ。",
                            _root.Q(ProductionUiNames.CameraRail), true, false)
                        : ("寄って見てみよう", "右の「詳細」を押すか、2 本指で広げると、施設を 1 棟ずつ見られるよ。",
                            _root.Q(ProductionUiNames.CameraRail), true, false);
                case VillageTutorial.Report:
                    return _tutorial.Halfway
                        ? ("ガスと止まっている理由", "ガスの残り、供給と消費、止まっている施設の理由が分かる。見終わったら閉じよう。", null, false, false)
                        : ("生産レポートを開こう", "上のバーを押すと、生産レポートが開くよ。", _root.Q(ProductionUiNames.TacticalBar), false, false);
                default:
                    return ("", "", null, false, false);
            }
        }

        /// <summary>段 5 で勧める施設と、建てられない理由 (無ければ空)。「次にやること」と同じ施設を勧める。</summary>
        private (FacilityKind Kind, string Reason) Suggestion()
        {
            var view = _session.Core.View;
            var kind = VillageInsight.NextStep(view, _navigator.World?.Hints).Build ?? FacilityKind.Fishery;
            if (view == null || view.Unlimited) return (kind, "");
            var def = IslandCatalog.Get(kind);
            if (view.Electricity < def.ElectricityCost) return (kind, "電気が足りないよ。スマホを休めると電気がたまる");
            if (view.Water < def.WaterCost) return (kind, "水が足りないよ。水を飲むと水がたまる");
            return (kind, "");
        }

        /// <summary>押す場所を脈打つ枠で囲む。前に光らせていた要素は元に戻す。</summary>
        private void Highlight(VisualElement target)
        {
            if (_target != target)
            {
                _target?.RemoveFromClassList(ProductionUiNames.CoachTargetClass);
                _target?.RemoveFromClassList(ProductionUiNames.CoachTargetOnClass);
                _target = target;
                _target?.AddToClassList(ProductionUiNames.CoachTargetClass);
            }

            _target?.EnableInClassList(ProductionUiNames.CoachTargetOnClass, Mathf.Repeat(Time.unscaledTime, PulseSeconds * 2) < PulseSeconds);
        }
    }
}
