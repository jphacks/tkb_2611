using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    public enum AppScreen
    {
        Home,
        Record,
        Mission,
        Scan,
        Analyzing,
        Result,
        Menu,
        Dex,

        // 生産の島の画面 (ホームの道具と島の施設から開く)
        Build,
        BuildDetail,
        Harbor,
        Facility,
        Production,
        Raid,
    }

    /// <summary>
    /// App.uxml に並べた画面のうち 1 つだけを表示する。シーンを分けずに画面を切り替えるための入口。
    /// 非表示の画面は破棄せず <see cref="UiClasses.Screen.Hidden"/> で隠すだけなので、戻ったときに状態が残る。
    /// 画面構成と遷移の考え方は docs/ui-structure.md を参照。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScreenNavigator : MonoBehaviour
    {
        public const string TabBarName = UiNames.App.TabBar;


        /// <summary>
        /// ボタンと行き先の対応。<see cref="Scope"/> の要素の中から <see cref="Button"/> を探す
        /// (closeButton のように画面をまたいで同じ名前があるため)。
        /// <see cref="Target"/> が null のボタンは直前のタブ画面へ戻る。
        /// </summary>
        public readonly struct Route
        {
            public Route(string scope, string button, AppScreen? target)
            {
                Scope = scope;
                Button = button;
                Target = target;
            }

            public string Scope { get; }
            public string Button { get; }
            public AppScreen? Target { get; }
        }

        public static readonly IReadOnlyList<Route> Routes = new[]
        {
            new Route(TabBarName, UiNames.TabBar.TabHome, AppScreen.Home),
            new Route(TabBarName, UiNames.TabBar.TabRecord, AppScreen.Record),
            new Route(TabBarName, UiNames.TabBar.TabScan, AppScreen.Scan),
            new Route(TabBarName, UiNames.TabBar.TabMission, AppScreen.Mission),
            new Route(TabBarName, UiNames.TabBar.TabMenu, AppScreen.Menu),
            new Route(ElementNameOf(AppScreen.Home), UiNames.Home.SettingsButton, AppScreen.Menu),
            new Route(ElementNameOf(AppScreen.Scan), UiNames.Scan.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Analyzing), UiNames.Analyzing.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Result), UiNames.Result.BackButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Result), UiNames.Result.ApplyButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Menu), UiNames.Menu.CloseBand, null),
            new Route(ElementNameOf(AppScreen.Menu), UiNames.Menu.MenuRecord, AppScreen.Record),
            new Route(ElementNameOf(AppScreen.Menu), UiNames.Menu.MenuMission, AppScreen.Mission),
            new Route(ElementNameOf(AppScreen.Menu), UiNames.Menu.MenuPokedex, AppScreen.Dex),
            new Route(ElementNameOf(AppScreen.Dex), UiNames.Dex.BackButton, null),
            new Route(ElementNameOf(AppScreen.Build), UiNames.Build.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.BuildDetail), UiNames.BuildDetail.BackButton, AppScreen.Build),
            new Route(ElementNameOf(AppScreen.BuildDetail), UiNames.BuildDetail.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Harbor), UiNames.Harbor.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Facility), UiNames.Facility.CloseButton, AppScreen.Home),
            new Route(ElementNameOf(AppScreen.Production), UiNames.Production.CloseButton, AppScreen.Home),
        };

        /// <summary>端末の設定アプリを開くボタン。アプリ内の設定画面ができるまでの暫定。</summary>
        public static readonly IReadOnlyList<(string Scope, string Button)> AppSettingsButtons = new[]
        {
            (ElementNameOf(AppScreen.Menu), UiNames.Menu.MenuCamera),
        };

        [Tooltip("ホーム以外の不透明な画面の表示中に止める 3D ワールドのカメラ。見えない世界を描画し続けないため")]
        [SerializeField] private Camera _worldCamera;

        [SerializeField] private ReceiptCameraPreview _cameraPreview;

        [Tooltip("ホームを開いている間だけ島へのタッチを受け付ける")]
        [SerializeField] private World.IslandInteraction _islandInteraction;

        private readonly Dictionary<AppScreen, VisualElement> _screens = new();
        private readonly Dictionary<AppScreen, Button> _tabButtons = new();
        private readonly List<(Button Button, Action Handler)> _bindings = new();
        private VisualElement _tabBar;
        private bool _tabBarSuppressed;
        private AppScreen _lastTab = AppScreen.Home;

        public AppScreen Current { get; private set; }

        /// <summary>画面が切り替わった (新しい画面, 前の画面)。</summary>
        public event Action<AppScreen, AppScreen> ScreenShown;

        public static string ElementNameOf(AppScreen screen) => screen switch
        {
            AppScreen.Home => UiNames.App.HomeScreen,
            AppScreen.Record => UiNames.App.RecordScreen,
            AppScreen.Mission => UiNames.App.MissionScreen,
            AppScreen.Scan => UiNames.App.ScanScreen,
            AppScreen.Analyzing => UiNames.App.AnalyzingScreen,
            AppScreen.Result => UiNames.App.ResultScreen,
            AppScreen.Menu => UiNames.App.MenuScreen,
            AppScreen.Dex => UiNames.App.DexScreen,
            AppScreen.Build => UiNames.App.BuildScreen,
            AppScreen.BuildDetail => UiNames.App.BuildDetailScreen,
            AppScreen.Harbor => UiNames.App.HarborScreen,
            AppScreen.Facility => UiNames.App.FacilityScreen,
            AppScreen.Production => UiNames.App.ProductionScreen,
            AppScreen.Raid => UiNames.App.RaidScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, null),
        };

        /// <summary>タブバーを出す画面と、そのときアクティブにするタブ。タブ画面でなければ null。</summary>
        public static string TabButtonOf(AppScreen screen) => screen switch
        {
            AppScreen.Home => UiNames.TabBar.TabHome,
            AppScreen.Record => UiNames.TabBar.TabRecord,
            AppScreen.Mission => UiNames.TabBar.TabMission,
            _ => null,
        };

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            foreach (AppScreen screen in Enum.GetValues(typeof(AppScreen)))
            {
                _screens[screen] = Require(root, ElementNameOf(screen));

                var tabName = TabButtonOf(screen);
                if (tabName != null)
                {
                    _tabButtons[screen] = Require<Button>(root, tabName);
                }
            }

            _tabBar = Require(root, TabBarName);

            foreach (var route in Routes)
            {
                var button = Require<Button>(Require(root, route.Scope), route.Button);
                var target = route.Target;
                Action handler = target.HasValue ? () => Show(target.Value) : () => Show(_lastTab);
                button.clicked += handler;
                _bindings.Add((button, handler));
            }

            foreach (var (scope, name) in AppSettingsButtons)
            {
                var button = Require<Button>(Require(root, scope), name);
                Action handler = AppSettingsLauncher.Open;
                button.clicked += handler;
                _bindings.Add((button, handler));
            }

            Show(AppScreen.Home);
        }

        private void OnDisable()
        {
            foreach (var (button, handler) in _bindings)
            {
                button.clicked -= handler;
            }

            _bindings.Clear();
            _screens.Clear();
            _tabButtons.Clear();
        }

        private void Update()
        {
            // Android の戻るキーは Input System では Escape キーとして届く。
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Back();
            }
        }

        public void Show(AppScreen screen)
        {
            var previous = Current;
            Current = screen;

            foreach (var (key, element) in _screens)
            {
                element.EnableInClassList(UiClasses.Screen.Hidden, key != screen);
            }

            // 画面が変わったときだけ、下からふわっと入れる (ホームは 3D の島が見えるのでそのまま出す)。
            if (screen != previous && screen != AppScreen.Home && _screens.TryGetValue(screen, out var shown))
            {
                shown.AddToClassList(UiClasses.Screen.Entering);
                shown.schedule.Execute(() => shown.RemoveFromClassList(UiClasses.Screen.Entering)).StartingIn(16);
            }

            var isTab = TabButtonOf(screen) != null;
            _tabBar.EnableInClassList(UiClasses.Screen.Hidden, !isTab || _tabBarSuppressed);
            if (isTab)
            {
                _lastTab = screen;
                foreach (var (key, tab) in _tabButtons)
                {
                    tab.EnableInClassList(UiClasses.Tab.Active, key == screen);
                }
            }

            if (_worldCamera != null)
            {
                _worldCamera.enabled = screen == AppScreen.Home;
            }

            // カメラはスキャン画面を開いている間だけ起動する (電池とプライバシーのため)。
            if (_cameraPreview != null)
            {
                if (screen == AppScreen.Scan)
                {
                    _cameraPreview.StartPreview();
                }
                else
                {
                    _cameraPreview.StopPreview();
                }
            }

            if (_islandInteraction != null)
            {
                _islandInteraction.InputEnabled = screen == AppScreen.Home;
                if (screen != AppScreen.Home && _islandInteraction.Mode != World.IslandMode.Browse)
                {
                    _islandInteraction.Cancel();
                }
            }

            ScreenShown?.Invoke(screen, previous);
        }

        /// <summary>
        /// タブバーを一時的に隠す (島の上で置き場所を選んでいる間など)。タブ画面でなければもともと出ていない。
        /// </summary>
        public void SetTabBarSuppressed(bool suppressed)
        {
            _tabBarSuppressed = suppressed;
            if (_tabBar != null)
            {
                _tabBar.EnableInClassList(UiClasses.Screen.Hidden, suppressed || TabButtonOf(Current) == null);
            }
        }

        /// <summary>戻るキー。フローの途中とタブ画面はホームへ、メニューは開く前のタブへ、建物の説明は一覧へ戻る。</summary>
        public void Back()
        {
            switch (Current)
            {
                case AppScreen.Home:
                    return;
                case AppScreen.Menu:
                    Show(_lastTab);
                    return;
                case AppScreen.BuildDetail:
                    Show(AppScreen.Build);
                    return;
                default:
                    Show(AppScreen.Home);
                    return;
            }
        }

        private static VisualElement Require(VisualElement scope, string name) =>
            scope.Q(name) ?? throw new InvalidOperationException($"App.uxml に '{name}' という要素がありません");

        private static T Require<T>(VisualElement scope, string name) where T : VisualElement =>
            scope.Q<T>(name) ?? throw new InvalidOperationException($"App.uxml に '{name}' という {typeof(T).Name} がありません");
    }
}
