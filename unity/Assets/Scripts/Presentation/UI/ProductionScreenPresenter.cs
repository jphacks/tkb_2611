using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 生産量の画面。ガス・働き手・置き場の 3 枚と、品物ごとの手もちと 1 時間あたりの増減だけを並べる。
    /// 使う方が多い品物は赤、まだ作っていない品物は薄くする。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;

        private readonly Dictionary<Item, (VisualElement Card, Label Value, Label Rate)> _items =
            new Dictionary<Item, (VisualElement, Label, Label)>();

        private VisualElement _grid;
        private Label _gasValue;
        private Label _gasRate;
        private Label _workersValue;
        private Label _storageValue;

        private void OnEnable()
        {
            if (_game == null || _navigator == null)
            {
                Debug.LogError($"{nameof(ProductionScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            var screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Production));
            _grid = screen.Q(UiNames.Production.ItemGrid);
            _gasValue = screen.Q<Label>(UiNames.Production.GasValue);
            _gasRate = screen.Q<Label>(UiNames.Production.GasRate);
            _workersValue = screen.Q<Label>(UiNames.Production.WorkersValue);
            _storageValue = screen.Q<Label>(UiNames.Production.StorageValue);

            _grid.Clear();
            _items.Clear();
            foreach (var item in IslandCatalog.Items)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList(UiClasses.ItemCard.Root);
                card.Add(IslandUi.Icon(IslandUi.IconClass(item), UiClasses.Tint.Fish));
                var value = IslandUi.Text("0", UiClasses.ItemCard.Value);
                var rate = IslandUi.Text("", UiClasses.ItemCard.Rate);
                card.Add(value);
                card.Add(rate);
                _grid.Add(card);
                _items[item] = (card, value, rate);
            }

            _navigator.ScreenShown += OnScreenShown;
            _game.IslandChanged += OnIslandChanged;
        }

        private void OnDisable()
        {
            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }

            if (_game != null)
            {
                _game.IslandChanged -= OnIslandChanged;
            }
        }

        public void Open() => _navigator.Show(AppScreen.Production);

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Production)
            {
                Refresh();
            }
        }

        private void OnIslandChanged()
        {
            if (_navigator.Current == AppScreen.Production)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var island = _game.Island;
            var hours = island.GasHoursLeft;
            _gasValue.text = double.IsInfinity(hours) ? "∞" : hours >= 48 ? $"{hours / 24:0} 日" : $"{hours:0.#} 時間";
            _gasRate.text = island.GasPerHour <= 0 ? "使っていない" : $"-{IslandUi.Rate(island.GasPerHour)}/時";
            _workersValue.text = $"{island.AssignedWorkers} / {island.Population}";
            _storageValue.text = island.StorageLimit.ToString();

            foreach (var rate in island.Rates())
            {
                if (!_items.TryGetValue(rate.Item, out var view))
                {
                    continue;
                }

                view.Value.text = IslandUi.Number(island.StockOf(rate.Item));
                var idle = rate.ProducedPerHour <= 0 && rate.ConsumedPerHour <= 0;
                var net = rate.NetPerHour;
                view.Card.EnableInClassList(UiClasses.ItemCard.Idle, idle);
                view.Card.EnableInClassList(UiClasses.ItemCard.Short, !idle && net < 0);
                view.Rate.text = idle ? "—" : $"{(net >= 0 ? "+" : "")}{IslandUi.Rate(net)}/時";
            }
        }
    }
}
