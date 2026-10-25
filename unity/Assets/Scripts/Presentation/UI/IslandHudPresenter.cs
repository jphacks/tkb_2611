using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// ホームの島の部分 (レベルと資源の帯・支えられる土地・道具ボタン) と、吹き出しの中の「次にやること」、
    /// 一時的な知らせ (トースト)、受け取った資源の演出、留守の間の報告を受け持つ。
    /// 島の部分は UXML では隠してあり、このコンポーネントがあるシーン (生産の島) でだけ出す。
    /// 氷と今週の記録のカードは <see cref="HomeStatusPresenter"/> が受け持つ。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class IslandHudPresenter : MonoBehaviour
    {
        private const float ToastSeconds = 2.6f;

        /// <summary>はじめて氷室を建てたときの説明 (氷の大きさとやる気) を見せたか。デモの「最初から」で消す。</summary>
        private const string IceHouseIntroKey = "pp.island.iceHouseIntroShown";

        /// <summary>夜の振り返りを始める時刻 (JST、docs/game-logic.md §3)。</summary>
        private const int EveningHour = 18;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandSheetPresenter _sheets;
        [SerializeField] private BuildScreenPresenter _build;
        [SerializeField] private HarborScreenPresenter _harbor;
        [SerializeField] private OnboardingPresenter _onboarding;

        private VisualElement _home;
        private Label _level;
        private readonly Dictionary<Resource, (VisualElement Box, Label Value)> _resources = new Dictionary<Resource, (VisualElement, Label)>();
        private VisualElement _fishBox;
        private Label _fishValue;
        private Label _land;
        private Button _action;
        private Label _actionLabel;
        private VisualElement _actionIcon;
        private VisualElement _toastLayer;
        private VisualElement _toolBuild;
        private Button _demoReset;
        private Button _quickEco;
        private VisualElement _gainLayer;
        private VisualElement _modal;
        private Label _modalTitle;
        private Label _modalText;
        private VisualElement _modalBody;
        private Button _modalButton;
        private Button _modalCancel;
        private Action _modalAction;
        private readonly Queue<Action> _modalQueue = new Queue<Action>();
        private VisualElement _harborDot;

        private readonly Dictionary<Resource, double> _shown = new Dictionary<Resource, double>();
        private long _shownFish = -1;
        private Action _nextAction;
        private string _nextActionKey;

        private void OnEnable()
        {
            if (_game == null || _navigator == null)
            {
                Debug.LogError($"{nameof(IslandHudPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _home = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Home));
            _level = _home.Q<Label>(UiNames.Home.LevelValue);
            Bind(Resource.Electricity, UiNames.Home.ResElectricity, UiNames.Home.ResElectricityValue);
            Bind(Resource.Water, UiNames.Home.ResWater, UiNames.Home.ResWaterValue);
            Bind(Resource.Gas, UiNames.Home.ResGas, UiNames.Home.ResGasValue);
            Bind(Resource.AllMighty, UiNames.Home.ResAllMighty, UiNames.Home.ResAllMightyValue);
            _fishBox = _home.Q(UiNames.Home.ResFish);
            _fishValue = _home.Q<Label>(UiNames.Home.ResFishValue);
            _land = _home.Q<Label>(UiNames.Home.LandValue);
            _action = _home.Q<Button>(UiNames.Home.NextActionButton);
            _actionLabel = _home.Q<Label>(UiNames.Home.NextActionLabel);
            _actionIcon = _home.Q(UiNames.Home.NextActionIcon);
            _toastLayer = _home.Q(UiNames.Home.ToastLayer);
            _gainLayer = _home.Q(UiNames.Home.GainLayer);
            _modal = _home.Q(UiNames.Home.Modal);
            _modalTitle = _home.Q<Label>(UiNames.Home.ModalTitle);
            _modalText = _home.Q<Label>(UiNames.Home.ModalText);
            _modalBody = _home.Q(UiNames.Home.ModalBody);
            _modalButton = _home.Q<Button>(UiNames.Home.ModalButton);
            _modalCancel = _home.Q<Button>(UiNames.Home.ModalCancel);
            _harborDot = _home.Q(UiNames.Home.HarborDot);
            _toolBuild = _home.Q(UiNames.Home.ToolBuild);
            _demoReset = _home.Q<Button>(UiNames.Home.DemoResetButton);
            _quickEco = _home.Q<Button>(UiNames.Home.QuickEcoButton);
            _quickEco.clicked += OpenQuickEco;
            if (_demoReset != null && (Application.isEditor || Debug.isDebugBuild))
            {
                _demoReset.clicked += ResetForDemo;
            }

            IslandUi.SetVisible(_home.Q(UiNames.Home.IslandBar), true);
            IslandUi.SetVisible(_home.Q(UiNames.Home.Tools), true);
            IslandUi.SetVisible(_land, true);
            _home.Query(className: UiClasses.Home.DemoIsland).ForEach(e => IslandUi.SetVisible(e, true));

            _action.clicked += OnAction;
            _modalButton.clicked += CloseModal;
            _modalCancel.clicked += CancelModal;
            _game.IslandChanged += Refresh;
            _game.Changed += OnGameChanged;
            _game.ResourcesReceived += OnResourcesReceived;
            _game.IslandEvents += OnIslandEvents;
            Refresh();
        }

        private void OnDisable()
        {
            if (_quickEco != null) _quickEco.clicked -= OpenQuickEco;
            if (_demoReset != null)
            {
                _demoReset.clicked -= ResetForDemo;
            }

            if (_action != null)
            {
                _action.clicked -= OnAction;
                _modalButton.clicked -= CloseModal;
                _modalCancel.clicked -= CancelModal;
            }

            if (_game != null)
            {
                _game.IslandChanged -= Refresh;
                _game.Changed -= OnGameChanged;
                _game.ResourcesReceived -= OnResourcesReceived;
                _game.IslandEvents -= OnIslandEvents;
            }
        }

        private void Bind(Resource r, string box, string value)
        {
            _resources[r] = (_home.Q(box), _home.Q<Label>(value));
        }

        private void OpenQuickEco()
        {
            _game.Refresh();
            var day = _game.EffectiveToday;
            var body = new ScrollView();
            body.AddToClassList(UiClasses.QuickEcoList);
            foreach (var def in QuickEcoActions.All)
            {
                var available = _game.Today == day && !_game.Session.PendingAheadDay.HasValue &&
                    _game.Island.CanReceiveQuickEco(def.Id, day);
                var reward = _game.Island.PreviewQuickEcoReward();
                var label = available ? $"{def.Title}  電気 +{reward:0.#}" : $"{def.Title}  受取済み・受付できません";
                var button = IslandUi.Pill(label, () =>
                {
                    // 一覧を閉じてから詳細へ進む。取消では受取操作を呼ばない。
                    _modalAction = null;
                    IslandUi.SetVisible(_modal, false);
                    ShowModal("ちょこっとエコ", $"{def.Description}\nできたら電気 +{reward:0.#}", null, "できた", () =>
                    {
                        if (_game.TryReceiveQuickEco(def.Id, day, reward, out var got))
                            ShowModal("できたね！", $"電気 +{got[Resource.Electricity]:0.#} を受け取ったよ", null, "閉じる");
                        else
                            ShowModal("もう一度確認してね", _game.QuickEcoError, null, "一覧へ", OpenQuickEco);
                    }, true);
                });
                button.SetEnabled(available);
                button.AddToClassList(UiClasses.QuickEcoCard);
                body.Add(button);
            }

            ShowModal("ちょこっとエコ", $"今日 {_game.Island.QuickEcoCount(day)}/3 回受取済み\n各お題1日1回。03:00に切り替わるよ。", body, "閉じる");
        }

        // ------------------------------------------------------------------
        // 公開: ほかの画面からの知らせ
        // ------------------------------------------------------------------

        /// <summary>画面の上の方に短い知らせを出す。</summary>
        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message) || _toastLayer == null)
            {
                return;
            }

            // 同じ知らせが表示中なら重ねない。
            if (_toastLayer.Children().OfType<Label>().Any(l => l.text == message))
            {
                return;
            }

            var toast = IslandUi.Text(message, UiClasses.Toast.Root, UiClasses.Toast.Hidden);
            toast.pickingMode = PickingMode.Ignore;
            _toastLayer.Add(toast);
            while (_toastLayer.childCount > 3)
            {
                _toastLayer.RemoveAt(0);
            }

            StartCoroutine(ToastRoutine(toast));
        }

        /// <summary>
        /// 真ん中に知らせを出す。ボタンを押すと閉じて <paramref name="onClose"/> を呼ぶ。
        /// <paramref name="cancellable"/> なら「やめる」も出し、押したときは何も呼ばずに閉じる。
        /// </summary>
        public void ShowModal(string title, string text, VisualElement body, string button, Action onClose = null, bool cancellable = false)
        {
            // 知らせが重なったら (施設が開いた・ペンギンが来た・おかえり が同時など)、閉じてから順に出す。
            if (IsModalOpen)
            {
                _modalQueue.Enqueue(() => ShowModal(title, text, body, button, onClose, cancellable));
                return;
            }

            IslandUi.SetVisible(_modalCancel, cancellable);
            _modalTitle.text = title;
            _modalText.text = text;
            IslandUi.SetVisible(_modalText, !string.IsNullOrEmpty(text));
            _modalBody.Clear();
            if (body != null)
            {
                _modalBody.Add(body);
            }

            _modalButton.text = button;
            _modalAction = onClose;
            IslandUi.SetVisible(_modal, true);
        }

        private bool IsModalOpen => _modal != null && !_modal.ClassListContains(UiClasses.Hidden);

        private void CloseModal()
        {
            IslandUi.SetVisible(_modal, false);
            var a = _modalAction;
            _modalAction = null;
            a?.Invoke();
            ShowNextModal();
        }

        private void CancelModal()
        {
            _modalAction = null;
            IslandUi.SetVisible(_modal, false);
            ShowNextModal();
        }

        private void ShowNextModal()
        {
            if (!IsModalOpen && _modalQueue.Count > 0)
            {
                _modalQueue.Dequeue()();
            }
        }

        private IEnumerator ToastRoutine(Label toast)
        {
            yield return null;
            toast.RemoveFromClassList(UiClasses.Toast.Hidden);
            yield return new WaitForSecondsRealtime(ToastSeconds);
            toast.AddToClassList(UiClasses.Toast.Hidden);
            yield return new WaitForSecondsRealtime(0.3f);
            toast.RemoveFromHierarchy();
        }

        // ------------------------------------------------------------------
        // 表示
        // ------------------------------------------------------------------

        private void OnGameChanged(GameStateChange change)
        {
            Refresh();
            ShowOfflineReport();
        }

        private void Refresh()
        {
            if (_home == null)
            {
                return;
            }

            var island = _game.Island;
            _level.text = island.Level.ToString();
            foreach (var kv in _resources)
            {
                var v = island.Get(kv.Key);
                kv.Value.Value.text = IslandUi.Number(v);
                kv.Value.Box.EnableInClassList(UiClasses.Resource.Empty, kv.Key == Resource.Gas && v + island.Get(Resource.AllMighty) < 1 && island.GasPerHour > 0);
                if (_shown.TryGetValue(kv.Key, out var before) && v > before + 0.5)
                {
                    Bump(kv.Value.Box);
                }

                _shown[kv.Key] = v;
            }

            var fish = island.Fish;
            _fishValue.text = IslandUi.Number(fish);

            // お魚が尽きると、働くペンギンのごはんが無くて施設が止まる。帯で知らせる。
            _fishBox.EnableInClassList(UiClasses.Resource.Empty, fish < 1 && island.FishUpkeepPerHour > 0);
            if (_shownFish >= 0 && fish >= _shownFish + 10)
            {
                Bump(_fishBox);
            }

            if (_shownFish < 0 || fish >= _shownFish + 10 || fish < _shownFish)
            {
                _shownFish = fish;
            }

            var board = island.Board;
            var sunk = board.SunkCount(island.Ice);
            // 氷が大きいとペンギンのやる気が上がり、施設がよく働く。上がっているときだけ添える (カードの幅に収まるよう短く)。
            var motivated = island.Motivation > 1.0 + 1e-9;
            _land.text = sunk > 0 ? $"島の端が {sunk} マス沈んでいる"
                : motivated ? $"土地 {board.Expansions.Count} / {board.Capacity(island.Ice)}・やる気{IslandUi.Motivation(island.Motivation)}"
                : $"支えられる土地 {board.Expansions.Count} / {board.Capacity(island.Ice)}";
            _land.EnableInClassList(UiClasses.Home.StatNoteDanger, sunk > 0);

            var readyOrder = Enumerable.Range(0, island.Orders.Count)
                .Any(i => island.IsOrderReady(i) && island.StockOf(island.Orders[i].Item) >= island.Orders[i].Count);
            IslandUi.SetVisible(_harborDot, readyOrder);

            RefreshNextAction(readyOrder);
        }

        private void RefreshNextAction(bool readyOrder)
        {
            var session = _game.Session;
            var today = session.Today;
            var selected = session.SelectedOn(today);
            var hour = _game.Now.ToOffset(TimeSpan.FromHours(9)).Hour;
            var evening = hour >= EveningHour || hour < 3;

            string key, label, icon;
            Action action;
            var island = _game.Island;

            // はじめての流れ: 写真 → 氷室 (氷を溶けにくくする) → 漁場 (お魚をとる) → 港。
            // 1 度でも建てたら案内しない (取り壊した人に何度も言わない)。お金が足りないうちは出さず、次の案内に進む。
            var firstIceHouse = island.CountOf(FacilityKind.IceHouse) == 0 && island.Facilities.All(f => f.Kind != FacilityKind.Fishery) &&
                                island.CanBuild(FacilityKind.IceHouse) == IslandActionResult.Ok;
            var firstFishery = island.CountOf(FacilityKind.Fishery) == 0 && island.CanBuild(FacilityKind.Fishery) == IslandActionResult.Ok;
            if (_game.CanReceivePhotoToday)
            {
                (key, label, icon, action) = ("photo", "今日のエコ写真を撮る", UiClasses.Icon.Camera, () => _navigator.Show(AppScreen.Scan));
            }
            else if (firstIceHouse && _build != null)
            {
                // はじめての人は、写真で得たインフラで、まず氷室を建てる。説明の画面へ直接案内する。
                (key, label, icon, action) = ("build", "氷室を建てて、氷を溶けにくくしよう", UiClasses.Icon.Build,
                    () => _build.OpenDetail(FacilityKind.IceHouse));
            }
            else if (firstFishery && _build != null)
            {
                (key, label, icon, action) = ("build-fishery", "漁場を建てて魚をとろう", UiClasses.Icon.Build, () => _build.OpenDetail(FacilityKind.Fishery));
            }
            else if (readyOrder && _harbor != null)
            {
                (key, label, icon, action) = ("deliver", "港に品物を届ける", UiClasses.Icon.Deliver, () => _harbor.Open());
            }
            else if (selected.Count == 0)
            {
                (key, label, icon, action) = ("select", "今日のミッションを選ぶ", UiClasses.Icon.Flag, () => _navigator.Show(AppScreen.Mission));
            }
            else if (evening && selected.Any(id => !session.IsCheckedIn(today, id)))
            {
                (key, label, icon, action) = ("checkin", "夜の振り返りをする", UiClasses.Icon.Flag, () => _navigator.Show(AppScreen.Mission));
            }
            else
            {
                (key, label, icon, action) = (null, null, null, null);
            }

            _nextAction = action;

            // はじめての建てる案内のあいだは、建てるボタンも光らせる。
            _toolBuild?.EnableInClassList(UiClasses.Tool.Active, key == "build" || key == "build-fishery");
            IslandUi.SetVisible(_action, action != null);
            if (key == _nextActionKey)
            {
                return;
            }

            _nextActionKey = key;
            if (action != null)
            {
                _actionLabel.text = label;
                foreach (var c in new[] { UiClasses.Icon.Camera, UiClasses.Icon.Flag, UiClasses.Icon.Deliver, UiClasses.Icon.Build })
                {
                    _actionIcon.EnableInClassList(c, c == icon);
                }
            }
        }

        private void OnAction() => _nextAction?.Invoke();

        /// <summary>デモ用: すべて消して、はじめての案内から見せ直す (イベントで何度も最初から見せるため)。</summary>
        private void ResetForDemo()
        {
            PlayerPrefs.DeleteKey(IceHouseIntroKey);
            _modalQueue.Clear();
            _modalAction = null;
            IslandUi.SetVisible(_modal, false);
            _game.ResetAll();
            _nextActionKey = null;
            _navigator.Show(AppScreen.Home);
            _onboarding?.ShowIfNeeded();
        }

        private void Bump(VisualElement box)
        {
            box.AddToClassList(UiClasses.Resource.Bump);
            box.schedule.Execute(() => box.RemoveFromClassList(UiClasses.Resource.Bump)).StartingIn(180);
        }

        private void OnResourcesReceived(ResourceBundle bundle, string source)
        {
            var i = 0;
            foreach (var kv in bundle.NonZero())
            {
                SpawnGain(kv.Key, kv.Value, i++);
            }
        }

        private void SpawnGain(Resource r, double amount, int index)
        {
            if (_gainLayer == null || !_resources.TryGetValue(r, out var target))
            {
                return;
            }

            var gain = new VisualElement { pickingMode = PickingMode.Ignore };
            gain.AddToClassList(UiClasses.Gain.Root);
            gain.Add(IslandUi.Icon(IslandUi.IconClass(r), IslandUi.TintClass(r)));
            gain.Add(IslandUi.Text("+" + IslandUi.Number(amount), UiClasses.Gain.Value));
            _gainLayer.Add(gain);

            // 資源の表示の少し下から浮かび上がらせる。
            var anchor = target.Box.worldBound;
            var origin = _gainLayer.WorldToLocal(new Vector2(anchor.xMin, anchor.yMax));
            gain.style.left = origin.x;
            gain.style.top = origin.y + 240 + index * 90;
            gain.schedule.Execute(() => gain.AddToClassList(UiClasses.Gain.Gone)).StartingIn(60 + index * 120);
            gain.schedule.Execute(gain.RemoveFromHierarchy).StartingIn(1500 + index * 120);
        }

        private void OnIslandEvents(IReadOnlyList<IslandEvent> events)
        {
            // 同じ種類の出来事は 1 回の知らせにまとめる (沈んだマスが 4 つでも知らせは 1 つ)。
            var sunk = events.Count(e => e.Kind == IslandEventKind.Sunk);
            var surfaced = events.Count(e => e.Kind == IslandEventKind.Surfaced);
            if (sunk > 0)
            {
                ShowToast($"氷が溶けて、島の端が {sunk} マス沈んじゃった…記録すると戻るよ");
            }

            if (surfaced > 0)
            {
                ShowToast($"氷が戻って、沈んでいた土地が {surfaced} マス浮かんだよ！");
            }

            if (events.Any(e => e.Kind == IslandEventKind.OrderArrived))
            {
                ShowToast("港に舟が着いたよ");
            }

            var unlocked = events.Where(e => e.Kind == IslandEventKind.Unlocked).Select(e => IslandCatalog.Get((FacilityKind)e.Value)).ToList();
            if (unlocked.Count > 0)
            {
                ShowUnlocked(unlocked);
            }

            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case IslandEventKind.LevelUp:
                        ShowLevelUp(e.Value);
                        break;
                    case IslandEventKind.HarborStageUp:
                        ShowToast(e.Value >= 5 ? "港が完成！おでん祭りだ！" : "港が少し立派になったよ");
                        break;
                    case IslandEventKind.PenguinArrived:
                        ShowPenguinArrived(e.Value);
                        break;
                    case IslandEventKind.Built:
                        var built = _game.Island.Find(e.FacilityId);
                        if (built != null && built.Kind == FacilityKind.IceHouse)
                        {
                            ShowIceHouseIntro();
                        }

                        break;
                }
            }
        }

        private void ShowLevelUp(int level)
        {
            var p = _game.Island.Parameters;
            var text = level <= p.FacilityMaxLevel
                ? $"島を広げられる数が増えて、施設を Lv{level} まで強化できるようになったよ"
                : "島を広げられる数が増えたよ";
            ShowModal($"レベル {level}！", text, null, "やったね");
        }

        /// <summary>お魚の桁が上がって、新しい施設が開いた。建物の絵を並べて見せ、棚へ案内する。</summary>
        private void ShowUnlocked(IReadOnlyList<FacilityDef> defs)
        {
            var list = new VisualElement();
            foreach (var def in defs)
            {
                list.Add(IslandUi.Row(IslandUi.IconBadge(IslandUi.IconClass(def), IslandUi.TintClass(def)), def.Name, def.Description));
            }

            var fish = defs.Max(d => d.UnlockFish);
            ShowModal($"お魚 {IslandUi.Number(fish)} 匹！", "お魚の桁が上がって、新しい施設が建てられるようになったよ", list, "見に行く",
                () => _build?.Open());
        }

        /// <summary>実績を満たして、新しい種のペンギンが来た。</summary>
        private void ShowPenguinArrived(int index)
        {
            if (index < 0 || index >= PenguinCatalog.AllIds.Count)
            {
                return;
            }

            var species = PenguinCatalog.AllIds[index];
            var entry = PenguinDex.Find(species);
            var unlock = PenguinUnlocks.Find(species);
            var body = new VisualElement();
            if (unlock != null)
            {
                body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Check, UiClasses.Tint.Eco, true), "達成", unlock.Condition));
            }

            if (entry != null)
            {
                body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "島へのおくりもの", entry.BonusText));
            }

            ShowModal($"{PenguinCatalog.DisplayNameJa[species]}が来た！", entry?.Fact, body, "ようこそ");
        }

        /// <summary>はじめて氷室を建てたとき、氷の大きさとペンギンのやる気のつながりを 1 回だけ伝える。</summary>
        private void ShowIceHouseIntro()
        {
            if (PlayerPrefs.GetInt(IceHouseIntroKey, 0) != 0)
            {
                return;
            }

            PlayerPrefs.SetInt(IceHouseIntroKey, 1);
            PlayerPrefs.Save();
            var p = _game.Island.Parameters;
            var body = new VisualElement();
            body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Snowflake, UiClasses.Tint.Water), "休んだ日も溶けにくい",
                $"記録しなかった日に溶ける氷が {p.IceHouseReduction * 100:0}% 減るよ。強化するともっと減る"));
            body.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "氷が大きいと、やる気が出る",
                $"氷が「ゆたか」で {IslandUi.Motivation(p.MotivationByTier[3])}、「ひろがる」で {IslandUi.Motivation(p.MotivationByTier[4])}。施設がよく働くよ"));
            ShowModal("氷が溶けにくくなったよ", "毎日のエコ写真で氷が増えると、ペンギンたちのやる気も上がるんだ", body, "わかった");
        }

        private void ShowOfflineReport()
        {
            var report = _game.TakeOfflineReport();
            if (report == null || report.Seconds < 60)
            {
                return;
            }

            var list = new VisualElement();
            foreach (var item in IslandCatalog.Items)
            {
                if (report[item] > 0)
                {
                    list.Add(IslandUi.Row(IslandUi.IconBadge(IslandUi.IconClass(item), UiClasses.Tint.Fish), IslandCatalog.NameOf(item),
                        null, IslandUi.Text("+" + IslandUi.Number(report[item]), UiClasses.Text.Label, UiClasses.Text.Heavy)));
                }
            }

            if (report.Pearls > 0)
            {
                list.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Pearl, UiClasses.Tint.Pearl), "真珠", null,
                    IslandUi.Text("+" + report.Pearls, UiClasses.Text.Label, UiClasses.Text.Heavy)));
            }

            if (report.Delivered > 0)
            {
                list.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Deliver, UiClasses.Tint.Point), "港への納品", null,
                    IslandUi.Text(report.Delivered + " 回", UiClasses.Text.Label, UiClasses.Text.Heavy)));
            }

            if (report.FishEaten > 0)
            {
                list.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Penguin, UiClasses.Tint.Original), "ペンギンのごはん", null,
                    IslandUi.Text("−" + IslandUi.Number(report.FishEaten), UiClasses.Text.Label, UiClasses.Text.Heavy)));
            }

            if (list.childCount == 0)
            {
                return;
            }

            var hours = report.Seconds / 3600.0;
            var span = hours >= 1 ? $"{Math.Floor(hours)} 時間" : $"{Math.Max(1, Math.Floor(report.Seconds / 60))} 分";
            var fish = report[Item.Fish];
            var message = fish > 0
                ? $"留守の {span}の間に、お魚が {IslandUi.Number(fish)} 匹とれたよ"
                : $"留守の {span}の間に、みんなが働いてくれたよ";

            // 長く休んでも、働いてくれるのは上限までだと伝える (毎日のぞいたほうが得になる)。
            if (report.Seconds >= _game.Island.Parameters.OfflineCapSeconds - 1)
            {
                message += $"\n(留守番は {Math.Floor(_game.Island.Parameters.OfflineCapSeconds / 3600)} 時間ぶんまで)";
            }

            ShowModal("おかえり！", message, list, "受け取る");
        }
    }
}
