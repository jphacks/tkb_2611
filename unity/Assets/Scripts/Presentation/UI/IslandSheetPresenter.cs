using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// ホームの道具ボタン (建てる・港・生産量) から各画面を開き、島の上で置き場所を選ぶ間の ✕ / ✓ と、島を広げる流れを受け持つ。
    /// 建てる・港・施設・生産量は全体が切り替わる画面 (BuildScreenPresenter など) で、ここに残るシートは読み物の「資源の集め方」だけ。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class IslandSheetPresenter : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;
        [SerializeField] private IslandInteraction _interaction;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandHudPresenter _hud;
        [SerializeField] private BuildScreenPresenter _build;
        [SerializeField] private HarborScreenPresenter _harbor;
        [SerializeField] private FacilityScreenPresenter _facility;
        [SerializeField] private ProductionScreenPresenter _production;

        private VisualElement _home;
        private VisualElement _sheet;
        private Label _sheetTitle;
        private ScrollView _sheetBody;
        private VisualElement _sheetFooter;
        private VisualElement _banner;
        private Label _bannerText;
        private Button _bannerConfirm;
        private Button _bannerCancel;
        private VisualElement _bannerIcon;
        private VisualElement _bannerCosts;
        private readonly List<(Button Button, Action Handler)> _bindings = new List<(Button, Action)>();

        /// <summary>シートが開いている間、島が変わるたびに呼ぶ更新。</summary>
        private Action _refreshSheet;
        private bool _sheetOpen;

        public bool IsSheetOpen => _sheetOpen;

        private void OnEnable()
        {
            if (_game == null || _interaction == null || _navigator == null)
            {
                Debug.LogError($"{nameof(IslandSheetPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _home = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Home));
            _sheet = _home.Q(UiNames.Home.Sheet);
            _sheetTitle = _home.Q<Label>(UiNames.Home.SheetTitle);
            _sheetBody = _home.Q<ScrollView>(UiNames.Home.SheetBody);
            _sheetFooter = _home.Q(UiNames.Home.SheetFooter);
            _banner = _home.Q(UiNames.Home.PlacementBanner);
            _bannerText = _home.Q<Label>(UiNames.Home.BannerText);
            _bannerConfirm = _home.Q<Button>(UiNames.Home.BannerConfirm);
            _bannerCancel = _home.Q<Button>(UiNames.Home.BannerCancel);
            _bannerIcon = _home.Q(UiNames.Home.BannerIcon);
            _bannerCosts = _home.Q(UiNames.Home.BannerCosts);

            BindButton(UiNames.Home.ToolBuild, OpenBuild);
            BindButton(UiNames.Home.ToolHarbor, OpenHarbor);
            BindButton(UiNames.Home.ToolProduction, OpenProduction);
            BindButton(UiNames.Home.SheetClose, CloseSheet);
            BindButton(UiNames.Home.BannerConfirm, ConfirmPlacement);
            BindButton(UiNames.Home.BannerCancel, () => _interaction.Cancel());
            _home.Q(UiNames.Home.SheetScrim).RegisterCallback<PointerDownEvent>(OnScrim);
            _home.Q(UiNames.Home.IslandBar)?.RegisterCallback<ClickEvent>(OnResourceBar);

            _interaction.Changed += RefreshBanner;
            _interaction.FacilitySelected += OpenFacility;
            _interaction.HarborTapped += OpenHarbor;
            _interaction.QualityAreaTapped += OnQualityAreaTapped;
            _interaction.ExpandAttempted += OnExpandAttempted;
            _game.IslandChanged += OnIslandChanged;
            RefreshBanner();
        }

        private void OnDisable()
        {
            foreach (var (button, handler) in _bindings)
            {
                button.clicked -= handler;
            }

            _bindings.Clear();
            _home?.Q(UiNames.Home.SheetScrim)?.UnregisterCallback<PointerDownEvent>(OnScrim);
            _home?.Q(UiNames.Home.IslandBar)?.UnregisterCallback<ClickEvent>(OnResourceBar);
            if (_interaction != null)
            {
                _interaction.Changed -= RefreshBanner;
                _interaction.FacilitySelected -= OpenFacility;
                _interaction.HarborTapped -= OpenHarbor;
                _interaction.QualityAreaTapped -= OnQualityAreaTapped;
                _interaction.ExpandAttempted -= OnExpandAttempted;
            }

            if (_game != null)
            {
                _game.IslandChanged -= OnIslandChanged;
            }
        }

        private void BindButton(string name, Action handler)
        {
            var b = _home.Q<Button>(name);
            if (b == null)
            {
                return;
            }

            b.clicked += handler;
            _bindings.Add((b, handler));
        }

        private void OnScrim(PointerDownEvent e) => CloseSheet();

        private void OnResourceBar(ClickEvent e) => OpenResources();

        private void OnIslandChanged()
        {
            if (_sheetOpen)
            {
                _refreshSheet?.Invoke();
            }

            if (_interaction.Mode == IslandMode.Expand)
            {
                RefreshBanner();
            }
        }

        // ------------------------------------------------------------------
        // 画面を開く
        // ------------------------------------------------------------------

        public void OpenBuild()
        {
            CloseSheet();
            _build?.Open();
        }

        public void OpenHarbor()
        {
            CloseSheet();
            _harbor?.Open();
        }

        public void OpenProduction()
        {
            CloseSheet();
            _production?.Open();
        }

        private void OpenFacility(int id)
        {
            CloseSheet();
            _facility?.Open(id);
        }

        // ------------------------------------------------------------------
        // シートの開け閉め (資源の集め方)
        // ------------------------------------------------------------------

        private void OpenSheet(string title, Action<VisualElement, VisualElement> build, Action refresh = null)
        {
            if (_interaction.Mode != IslandMode.Browse)
            {
                _interaction.Cancel();
            }

            _sheetTitle.text = title;
            _sheetBody.Clear();
            _sheetFooter.Clear();
            build(_sheetBody.contentContainer, _sheetFooter);
            IslandUi.SetVisible(_sheetFooter, _sheetFooter.childCount > 0);
            _refreshSheet = refresh;
            _refreshSheet?.Invoke();
            _sheetBody.scrollOffset = Vector2.zero;
            if (!_sheetOpen)
            {
                _sheetOpen = true;
                IslandUi.SetVisible(_sheet, true);
                _sheet.schedule.Execute(() => _sheet.RemoveFromClassList(UiClasses.Sheet.Closed)).StartingIn(16);
            }
        }

        public void CloseSheet()
        {
            if (!_sheetOpen)
            {
                return;
            }

            _sheetOpen = false;
            _refreshSheet = null;
            _sheet.AddToClassList(UiClasses.Sheet.Closed);
            _sheet.schedule.Execute(() =>
            {
                if (!_sheetOpen)
                {
                    IslandUi.SetVisible(_sheet, false);
                }
            }).StartingIn(240);
        }

        // ------------------------------------------------------------------
        // 置き場所を選ぶ間の ✕ / ✓
        // ------------------------------------------------------------------

        private void ConfirmPlacement()
        {
            var name = IslandCatalog.Get(_interaction.PlacingKind).Name;
            var moving = _interaction.Mode == IslandMode.Move;
            var r = _interaction.Confirm();
            _hud?.ShowToast(r == IslandActionResult.Ok ? (moving ? $"{name}を移したよ" : $"{name}を建てたよ！") : IslandUi.Message(r));
        }

        /// <summary>島を広げる (光る海をタップしていく)。建物の説明の画面の「広げる」から呼ぶ。</summary>
        public void StartExpand()
        {
            CloseSheet();
            var island = _game.Island;
            var check = island.Board.SunkCount(island.Ice) > 0 || island.ExpandRoom == 0 ? island.CanExpand(new Cell(-1, -1)) : IslandActionResult.Ok;
            if (check == IslandActionResult.NotEnoughIce || check == IslandActionResult.LimitReached)
            {
                _hud?.ShowToast(check == IslandActionResult.LimitReached ? "今のレベルではこれ以上広げられないよ" : IslandUi.Message(check));
                return;
            }

            _interaction.BeginExpand();
        }

        private void OnQualityAreaTapped(Cell cell, Item item)
        {
            var island = _game.Island;
            _hud?.ShowToast(IslandUi.QualityAreaDescription(item, island.Board.IsSolidLand(cell, island.Ice), island.Parameters.QualityTileSpeed));
        }

        private void OnExpandAttempted(Cell cell, IslandActionResult r)
        {
            if (r != IslandActionResult.Ok)
            {
                _hud?.ShowToast(IslandUi.Message(r));
                return;
            }

            if (_game.Island.ExpandRoom == 0)
            {
                _hud?.ShowToast("今はここまで広げられるよ");
                _interaction.Cancel();
            }
        }

        private void RefreshBanner()
        {
            var mode = _interaction.Mode;
            var placing = mode != IslandMode.Browse;
            IslandUi.SetVisible(_banner, placing);
            _home.EnableInClassList(UiClasses.Home.Placing, placing);
            _navigator.SetTabBarSuppressed(placing);
            if (!placing)
            {
                return;
            }

            var island = _game.Island;
            var expand = mode == IslandMode.Expand;
            IslandUi.SetVisible(_bannerConfirm, !expand);
            IslandUi.SetVisible(_bannerCosts, expand);
            if (expand)
            {
                IslandUi.SetThumb(_bannerIcon, UiClasses.Thumb.Expand);
                IslandUi.SetVisible(_bannerText, false);
                _bannerCosts.Clear();
                _bannerCosts.Add(IslandUi.Count(UiClasses.Icon.Water, UiClasses.Tint.Water, island.ExpandWaterCost,
                    island.Get(Resource.Water) + island.Get(Resource.AllMighty) < island.ExpandWaterCost));
                _bannerCosts.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, island.ExpandFishCost, island.Fish < island.ExpandFishCost));
                _bannerCosts.Add(IslandUi.Count(UiClasses.Icon.ExpandIsland, UiClasses.Tint.Primary, $"あと {island.ExpandRoom}"));
                return;
            }

            IslandUi.SetThumb(_bannerIcon, UiClasses.Thumb.Of(_interaction.PlacingKind));
            var ok = _interaction.GhostResult == IslandActionResult.Ok;
            _bannerConfirm.SetEnabled(ok);
            IslandUi.SetVisible(_bannerText, !ok);
            _bannerText.text = IslandUi.Message(_interaction.GhostResult);
        }

        // ------------------------------------------------------------------
        // 資源の集め方
        // ------------------------------------------------------------------

        /// <summary>
        /// 資源ごとに、どうすれば増えるか・今日どこまで受け取ったか・倍率の内訳・何に使うかを出す。
        /// 数値は <see cref="IslandParameters"/> から読むので、調整してもここを直す必要はない。
        /// </summary>
        public void OpenResources()
        {
            var today = new Dictionary<Resource, Label>();
            var multipliers = new Dictionary<Resource, Label>();
            var amounts = new Dictionary<Resource, Label>();
            OpenSheet("資源の集め方", (body, footer) =>
            {
                var p = _game.Island.Parameters;
                body.Add(IslandUi.Text("インフラ (電気・水・ガス) とオールマイティは、現実のエコな行動からだけ増える。島の中では作れない。電気のエコなら電気、水のエコなら水、ガスのエコならガスが多くもらえる。",
                    UiClasses.Text.Caption, UiClasses.Text.Sub, UiClasses.IslandGuide.Lead));

                void Add(Resource r, string how, string use)
                {
                    var amount = IslandUi.Text("", UiClasses.Text.Label, UiClasses.Text.Heavy, UiClasses.IslandGuide.Amount);
                    var row = IslandUi.Row(IslandUi.IconBadge(IslandUi.IconClass(r), IslandUi.TintClass(r)), IslandCatalog.NameOf(r), how, amount);
                    var rowBody = row.Q(className: UiClasses.Row.Body);
                    var t = IslandUi.Text("", UiClasses.Text.Caption, UiClasses.Text.Bold, UiClasses.IslandGuide.Today);
                    var m = IslandUi.Text("", UiClasses.Text.Caption, UiClasses.Text.Sub);
                    rowBody.Add(t);
                    rowBody.Add(m);
                    rowBody.Add(IslandUi.Text("使い道：" + use, UiClasses.Text.Caption, UiClasses.Text.Sub));
                    today[r] = t;
                    multipliers[r] = m;
                    amounts[r] = amount;
                    body.Add(row);
                }

                Add(Resource.Electricity, $"写真：{PhotoSources(Resource.Electricity)}", "施設を建てる・強化する");
                Add(Resource.Water, $"写真：{PhotoSources(Resource.Water)}", "海を凍らせて島を広げる・氷室を建てる");
                Add(Resource.Gas,
                    $"{p.StepsPerGas:N0} 歩ごとに +1（1 日 {p.StepGasPerDayMax:0} まで）。写真：{PhotoSources(Resource.Gas)}",
                    "施設を動かす（動いている間ずっと減る）");
                Add(Resource.AllMighty,
                    $"夜の振り返りで「できた」+{p.MissionAllMighty:0}、「できなかった」でも +{p.MissionTriedAllMighty:0}。写真：{PhotoSources(Resource.AllMighty)}",
                    "電気・水・ガスが足りないとき、代わりに 1 対 1 で使われる");

                // 写真の表 (写っているもの → もらえる量)。1 日 1 枚まで。
                body.Add(IslandUi.Text($"写真の表（1 日 {p.PhotosPerDay} 枚まで）", UiClasses.SectionTitle));
                foreach (var def in EcoSubjects.All)
                {
                    var counts = new VisualElement { pickingMode = PickingMode.Ignore };
                    counts.AddToClassList(UiClasses.Counts);
                    foreach (var (res, v) in def.Grants)
                    {
                        counts.Add(IslandUi.Count(IslandUi.IconClass(res), IslandUi.TintClass(res), $"+{v:0}"));
                    }

                    var row = IslandUi.Row(null, def.Name, null);
                    row.Q(className: UiClasses.Row.Body).Add(counts);
                    body.Add(row);
                }

                var unknown = IslandUi.Row(null, "エコと判定できない写真", "記録にはなる。電気・水・ガスに少しずつ");
                unknown.EnableInClassList(UiClasses.Row.Last, true);
                body.Add(unknown);

                body.Add(IslandUi.Text("島の中で増えるもの", UiClasses.SectionTitle));
                body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Fish, UiClasses.Tint.Fish), "お魚",
                    $"港の注文に品物を届ける（品物の値 × 個数 × {p.OrderRewardMultiplier:0.#}）。漁場でもとれる。使い道：建てる・広げる・働くペンギンのごはん（尽きると施設が止まる。漁場は自分でとった魚を食べる）。持ったお魚の桁が上がると、新しい施設が開く（{string.Join("・", IslandCatalog.UnlockSteps.Where(n => n > 0).Select(n => IslandUi.Number(n)))} 匹）"));

                // 氷とペンギンのやる気。氷が大きいほど施設がよく働く。
                body.Add(IslandUi.Text("氷とペンギンのやる気", UiClasses.SectionTitle));
                body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Snowflake, UiClasses.Tint.Water), "氷",
                    "毎日の記録で増え、休むと溶ける。氷が支えられるだけ島を広げられる。氷室を建てると、休んだ日に溶ける量が減る"));
                body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "やる気",
                    $"氷が「ゆたか」で {IslandUi.Motivation(p.MotivationByTier[3])}、「ひろがる」で {IslandUi.Motivation(p.MotivationByTier[4])}。作る速さ・増幅所・氷室の効き目に掛かる。氷が小さくても下がらない"));
                var pearl = IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Pearl, UiClasses.Tint.Pearl), "真珠",
                    $"研究所で作る。使い道：施設 1 つに {p.PearlsPerFacility} 個まで入れて、1 個ごとに速さ +{p.PearlSpeed * 100:0}%");
                pearl.EnableInClassList(UiClasses.Row.Last, true);
                body.Add(pearl);
            }, () =>
            {
                var island = _game.Island;
                var p = island.Parameters;
                var session = _game.Session;
                foreach (var kv in amounts)
                {
                    kv.Value.text = IslandUi.Number(island.Get(kv.Key));
                }

                var photo = _game.CanReceivePhotoToday ? "今日：写真はまだ" : "今日：写真は受け取った";
                today[Resource.Electricity].text = photo;
                today[Resource.Water].text = photo;

                var steps = _game.StepsToday;
                var stepText = steps >= 0 ? $"今日：{steps:N0} 歩" : _game.StepsSupported ? "今日：歩数を読み込み中" : "今日：この端末では歩数を読めない";
                today[Resource.Gas].text = $"{stepText} → ガス {island.StepGasToday:0} / {p.StepGasPerDayMax:0}";

                var selected = session.SelectedOn(session.Today);
                var done = selected.Count(id => session.IsCheckedIn(session.Today, id));
                today[Resource.AllMighty].text = selected.Count == 0
                    ? "今日：ミッションをまだ選んでいない"
                    : $"今日：振り返り {done} / {selected.Count} 件";

                foreach (var kv in multipliers)
                {
                    kv.Value.text = MultiplierText(island, kv.Key);
                }
            });
        }

        /// <summary>その資源がもらえる写真 (「マイボトル +10・短いシャワー +20」のように)。</summary>
        private static string PhotoSources(Resource r)
        {
            var parts = EcoSubjects.All
                .SelectMany(d => d.Grants.Where(g => g.Resource == r).Select(g => $"{d.Name} +{g.Amount:0}"))
                .ToList();
            var unknown = EcoSubjects.UnknownGrants.Where(g => g.Resource == r).Select(g => $"判定できない写真 +{g.Amount:0}");
            return string.Join("・", parts.Concat(unknown));
        }

        /// <summary>受け取る量の倍率と、その内訳 (増幅施設・図鑑のペンギン)。</summary>
        private static string MultiplierText(IslandGame island, Resource r)
        {
            var amp = IslandCatalog.Facilities.FirstOrDefault(d => d.Amplifies == r);
            var amps = island.Facilities.Count(f => f.Def.Amplifies == r && f.Status == FacilityStatus.Running);
            var penguins = PenguinDex.Entries
                .Where(e => e.Bonus == BonusKind.ResourceGain && e.Resource == r && island.Species.Contains(e.Species))
                .Select(e => e.Name)
                .ToList();
            var parts = new List<string>();
            if (amps > 0 && amp != null)
            {
                parts.Add($"{amp.Name} {amps} 棟");
            }

            if (penguins.Count > 0)
            {
                parts.Add(string.Join("・", penguins));
            }

            if (parts.Count == 0)
            {
                return amp != null ? $"倍率 ×1（{amp.Name}を建てると増える）" : "倍率 ×1";
            }

            if (amps > 0 && island.Motivation > 1.0 + 1e-9)
            {
                parts.Add($"やる気 {IslandUi.Motivation(island.Motivation)}");
            }

            return $"倍率 ×{island.GainMultiplier(r):0.##}（{string.Join("、", parts)}）";
        }
    }
}
