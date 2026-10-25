using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 島の施設を押したときの画面。大きな絵とレベル、状態、作るもの (材料 → できるもの)、働くペンギン、真珠、強化を出す。
    /// 文字は状態の札と効果の 1 行だけにし、ほかはアイコンと数字で伝える。移す・取り壊すは小さな丸いボタン。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FacilityScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandInteraction _interaction;
        [SerializeField] private IslandHudPresenter _hud;

        private readonly List<Button> _modeButtons = new List<Button>();
        private readonly List<(Button Button, System.Action Handler)> _bindings = new List<(Button, System.Action)>();
        private VisualElement _screen;
        private Label _title;
        private VisualElement _image;
        private Label _level;
        private VisualElement _status;
        private VisualElement _producePanel;
        private VisualElement _recipe;
        private Label _speed;
        private Label _consumption;
        private ProgressBar _progress;
        private VisualElement _modes;
        private VisualElement _effectPanel;
        private VisualElement _effectIcon;
        private Label _effect;
        private VisualElement _workersPanel;
        private VisualElement _workers;
        private Button _workerMinus;
        private Button _workerPlus;
        private VisualElement _pearlPanel;
        private Label _pearlValue;
        private Button _pearlAdd;
        private VisualElement _pips;
        private VisualElement _upgradeCosts;
        private Label _upgradeNote;
        private Button _upgradeButton;

        private int _id = -1;
        private int _builtFor = -1;
        private int _builtLevel;
        private int _builtRecipe;
        private string _effectIconClass;

        private void OnEnable()
        {
            if (_game == null || _navigator == null || _interaction == null)
            {
                Debug.LogError($"{nameof(FacilityScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Facility));
            _title = _screen.Q<Label>(UiNames.Facility.FacilityTitle);
            _image = _screen.Q(UiNames.Facility.FacilityImage);
            _level = _screen.Q<Label>(UiNames.Facility.FacilityLevel);
            _status = _screen.Q(UiNames.Facility.FacilityStatus);
            _producePanel = _screen.Q(UiNames.Facility.FacilityProduce);
            _recipe = _screen.Q(UiNames.Facility.FacilityRecipe);
            _speed = _screen.Q<Label>(UiNames.Facility.FacilitySpeed);
            _consumption = _screen.Q<Label>(UiNames.Facility.FacilityConsumption);
            _progress = _screen.Q<ProgressBar>(UiNames.Facility.FacilityProgress);
            _modes = _screen.Q(UiNames.Facility.FacilityModes);
            _effectPanel = _screen.Q(UiNames.Facility.FacilityEffectPanel);
            _effectIcon = _screen.Q(UiNames.Facility.FacilityEffectIcon);
            _effect = _screen.Q<Label>(UiNames.Facility.FacilityEffect);
            _workersPanel = _screen.Q(UiNames.Facility.FacilityWorkersPanel);
            _workers = _screen.Q(UiNames.Facility.FacilityWorkers);
            _workerMinus = _screen.Q<Button>(UiNames.Facility.WorkerMinus);
            _workerPlus = _screen.Q<Button>(UiNames.Facility.WorkerPlus);
            _pearlPanel = _screen.Q(UiNames.Facility.FacilityPearlPanel);
            _pearlValue = _screen.Q<Label>(UiNames.Facility.PearlValue);
            _pearlAdd = _screen.Q<Button>(UiNames.Facility.PearlAdd);
            _pips = _screen.Q(UiNames.Facility.FacilityPips);
            _upgradeCosts = _screen.Q(UiNames.Facility.UpgradeCosts);
            _upgradeNote = _screen.Q<Label>(UiNames.Facility.UpgradeNote);
            _upgradeButton = _screen.Q<Button>(UiNames.Facility.UpgradeButton);

            Bind(_workerMinus, () => ChangeWorkers(-1));
            Bind(_workerPlus, () => ChangeWorkers(+1));
            Bind(_pearlAdd, AddPearl);
            Bind(_upgradeButton, Upgrade);
            Bind(_screen.Q<Button>(UiNames.Facility.MoveButton), Move);
            Bind(_screen.Q<Button>(UiNames.Facility.DemolishButton), ConfirmDemolish);
            _navigator.ScreenShown += OnScreenShown;
            _game.IslandChanged += OnIslandChanged;
        }

        private void OnDisable()
        {
            foreach (var (button, handler) in _bindings)
            {
                button.clicked -= handler;
            }

            _bindings.Clear();
            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }

            if (_game != null)
            {
                _game.IslandChanged -= OnIslandChanged;
            }
        }

        private void Bind(Button button, System.Action handler)
        {
            if (button == null)
            {
                return;
            }

            button.clicked += handler;
            _bindings.Add((button, handler));
        }

        /// <summary>施設の画面を開く。</summary>
        public void Open(int facilityId)
        {
            _id = facilityId;
            _builtFor = -1;
            if (_navigator.Current == AppScreen.Facility)
            {
                Refresh();
            }
            else
            {
                _navigator.Show(AppScreen.Facility);
            }
        }

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Facility)
            {
                Refresh();
            }
            else if (previous == AppScreen.Facility)
            {
                _interaction.ClearSelection();
            }
        }

        private void OnIslandChanged()
        {
            if (_navigator.Current == AppScreen.Facility)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var island = _game.Island;
            var f = island.Find(_id);
            if (f == null)
            {
                _navigator.Show(AppScreen.Home);
                return;
            }

            var def = f.Def;
            if (_builtFor != f.Id || _builtLevel != f.Level)
            {
                BuildStatic(f);
            }

            _level.text = f.Level.ToString();
            _status.Clear();
            _status.Add(IslandUi.StatusPill(f));

            if (def.Role == FacilityRole.Producer)
            {
                if (_builtRecipe != f.RecipeIndex)
                {
                    IslandUi.FillRecipe(_recipe, def.Kind, f.Recipe);
                    _builtRecipe = f.RecipeIndex;
                }

                var speed = island.SpeedOf(f);
                _speed.text = $"{IslandUi.Rate(f.Recipe.Seconds / speed)} 秒（× {speed:0.0#}）";
                _consumption.text = $"ガス {IslandUi.Rate(f.Recipe.GasPerHour)}/時"
                    + (f.Def.FishPerHour > 0 ? $"　ごはん {IslandUi.Rate(f.Def.FishPerHour)}/時" : "");
                _progress.value = (float)f.ProgressRatio;
                for (var i = 0; i < _modeButtons.Count; i++)
                {
                    _modeButtons[i].EnableInClassList(UiClasses.Pill.Selected, i == f.RecipeIndex);
                }

                _pearlValue.text = $"{f.Pearls} / {island.Parameters.PearlsPerFacility}";
                _pearlAdd.SetEnabled(f.Pearls < island.Parameters.PearlsPerFacility && island.Get(Resource.Pearl) >= 1);
            }
            else
            {
                _effect.text = EffectNow(island, f);
            }

            if (def.Workers > 0)
            {
                var slots = _workers.Children();
                var n = 0;
                foreach (var slot in slots)
                {
                    slot.EnableInClassList(UiClasses.Worker.Empty, n++ >= f.Workers);
                }

                _workerMinus.SetEnabled(f.Workers > 0);
                _workerPlus.SetEnabled(f.Workers < def.Workers && island.FreeWorkers > 0);
            }

            RefreshUpgrade(island, f);
        }

        /// <summary>施設やレベルが変わったときだけ作り直す部分 (押している途中のボタンを作り直さないため)。</summary>
        private void BuildStatic(Facility f)
        {
            var def = f.Def;
            var island = _game.Island;
            _builtFor = f.Id;
            _builtLevel = f.Level;
            _builtRecipe = f.RecipeIndex;
            _title.text = def.Name;
            IslandUi.SetThumb(_image, UiClasses.Thumb.Of(def.Kind));

            var producer = def.Role == FacilityRole.Producer;
            IslandUi.SetVisible(_producePanel, producer);
            IslandUi.SetVisible(_pearlPanel, producer);
            IslandUi.SetVisible(_effectPanel, !producer);
            if (producer)
            {
                IslandUi.FillRecipe(_recipe, def.Kind, f.Recipe);
                _modes.Clear();
                _modeButtons.Clear();
                IslandUi.SetVisible(_modes, def.Recipes.Count > 1);
                for (var i = 0; i < def.Recipes.Count && def.Recipes.Count > 1; i++)
                {
                    var index = i;
                    var b = IslandUi.Pill(def.Recipes[i].Name, () => SetRecipe(index));
                    _modes.Add(b);
                    _modeButtons.Add(b);
                }
            }
            else
            {
                if (_effectIconClass != null)
                {
                    _effectIcon.RemoveFromClassList(_effectIconClass);
                }

                _effectIconClass = IslandUi.EffectIcon(def);
                _effectIcon.AddToClassList(_effectIconClass);
            }

            IslandUi.SetVisible(_workersPanel, def.Workers > 0);
            _workers.Clear();
            for (var i = 0; i < def.Workers; i++)
            {
                var w = IslandUi.Icon(UiClasses.Icon.Penguin, null);
                w.AddToClassList(UiClasses.Worker.Root);
                _workers.Add(w);
            }

            _pips.Clear();
            for (var i = 1; i <= island.Parameters.FacilityMaxLevel; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList(UiClasses.LevelPip.Root);
                pip.EnableInClassList(UiClasses.LevelPip.On, i <= f.Level);
                _pips.Add(pip);
            }
        }

        private void RefreshUpgrade(IslandGame island, Facility f)
        {
            var can = island.CanUpgrade(f.Id);
            var max = can == IslandActionResult.MaxLevel;
            _upgradeCosts.Clear();
            IslandUi.SetVisible(_upgradeCosts, !max);
            IslandUi.SetVisible(_upgradeButton, !max);
            if (!max)
            {
                var electricity = island.UpgradeElectricityCost(f);
                var fish = island.UpgradeFishCost(f);
                _upgradeCosts.Add(IslandUi.Count(UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, electricity,
                    island.Get(Resource.Electricity) + island.Get(Resource.AllMighty) < electricity));
                _upgradeCosts.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, fish, island.Fish < fish));
            }

            _upgradeButton.SetEnabled(can == IslandActionResult.Ok);
            IslandUi.SetVisible(_upgradeNote, max || can == IslandActionResult.Locked);
            _upgradeNote.text = max ? "最大レベル！" : $"島が Lv {f.Level + 1} になると強化できる";
        }

        private static string EffectNow(IslandGame island, Facility f) => f.Def.Role switch
        {
            FacilityRole.Storage => $"置き場 +{island.Parameters.StoragePerWarehouse * f.Level}",
            FacilityRole.Housing => $"住める数 +{island.HousingOf(f)}",
            FacilityRole.Amplifier => $"{IslandCatalog.NameOf(f.Def.Amplifies.GetValueOrDefault())} +{island.AmplifierBonusOf(f) * island.Motivation * 100:0}%" + MotivationNote(island),
            FacilityRole.AutoDeliver => $"自動でとどける（舟 {island.OrderCooldown:0} 秒）",
            FacilityRole.IceGuard => $"休んだ日に溶ける氷 −{Math.Min(island.Parameters.MaxMeltReduction, island.IceHouseReductionOf(f) * island.Motivation) * 100:0}%" + MotivationNote(island),
            _ => "",
        } + UpkeepNote(f);

        /// <summary>氷が大きくてペンギンのやる気が上がっているときだけ、倍率を添える。</summary>
        private static string MotivationNote(IslandGame island) =>
            island.Motivation > 1.0 + 1e-9 ? $"（やる気 {IslandUi.Motivation(island.Motivation)}）" : "";

        /// <summary>働くペンギンのごはん (お魚/時)。</summary>
        private static string UpkeepNote(Facility f) =>
            f.Def.FishPerHour > 0 ? $"\nごはん お魚 {IslandUi.Rate(f.Def.FishPerHour)}/時" : "";

        // ------------------------------------------------------------------
        // 操作
        // ------------------------------------------------------------------

        private void ChangeWorkers(int delta)
        {
            var f = _game.Island.Find(_id);
            if (f != null)
            {
                _game.SetWorkers(_id, f.Workers + delta);
            }
        }

        private void SetRecipe(int index) => _game.SetRecipe(_id, index);

        private void AddPearl() => _game.AddPearl(_id);

        private void Upgrade()
        {
            if (_game.Upgrade(_id) != IslandActionResult.Ok)
            {
                return;
            }

            // レベルの丸が増えるのを見せるため、絵のカードを一瞬縮めて戻す。
            var card = _image.parent;
            card.AddToClassList(UiClasses.PhotoCard.Swap);
            card.schedule.Execute(() => card.RemoveFromClassList(UiClasses.PhotoCard.Swap)).StartingIn(120);
        }

        private void Move()
        {
            var id = _id;
            _navigator.Show(AppScreen.Home);
            _interaction.BeginMove(id);
        }

        private void ConfirmDemolish()
        {
            var f = _game.Island.Find(_id);
            if (f == null || _hud == null)
            {
                return;
            }

            // 確かめる窓はホームにあるので、ホームへ戻ってから出す。
            var id = _id;
            var name = f.Def.Name;
            _navigator.Show(AppScreen.Home);
            _hud.ShowModal($"{name}を取り壊す？", "建てたときの半分が戻るよ", null, "取り壊す", () =>
            {
                if (_game.Demolish(id) == IslandActionResult.Ok)
                {
                    _hud.ShowToast($"{name}を取り壊したよ");
                }
            }, true);
        }
    }
}
