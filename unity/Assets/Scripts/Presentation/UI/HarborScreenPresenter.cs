using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 港の画面。舟の注文を 1 件ずつ大きなカードにし、品物の絵・個数・手もち・報酬だけを出す。
    /// そろったカードの丸いボタンで届ける。舟が着く前のカードは舟の絵と残り秒数にする。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HarborScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandHudPresenter _hud;

        private sealed class OrderTile
        {
            public Order Order;
            public VisualElement Root;
            public VisualElement ArtIcon;
            public Label Big;
            public Label Small;
            public VisualElement Reward;
            public Button Deliver;
        }

        private readonly List<OrderTile> _tiles = new List<OrderTile>();
        private VisualElement _list;
        private VisualElement _art;
        private Label _stage;
        private ProgressBar _progress;
        private Label _next;
        private float _flashUntil;

        private void OnEnable()
        {
            if (_game == null || _navigator == null)
            {
                Debug.LogError($"{nameof(HarborScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            var screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Harbor));
            _list = screen.Q(UiNames.Harbor.OrderList);
            _art = screen.Q(UiNames.Harbor.HarborArt);
            _stage = screen.Q<Label>(UiNames.Harbor.HarborStage);
            _progress = screen.Q<ProgressBar>(UiNames.Harbor.HarborProgress);
            _next = screen.Q<Label>(UiNames.Harbor.HarborNext);
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

        public void Open() => _navigator.Show(AppScreen.Harbor);

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Harbor)
            {
                Rebuild();
            }
        }

        private void OnIslandChanged()
        {
            if (_navigator.Current != AppScreen.Harbor)
            {
                return;
            }

            // 届けたあとは新しい注文に入れ替わるので、そのときだけ作り直す (押している途中のボタンを作り直さない)。
            var orders = _game.Island.Orders;
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (i >= orders.Count || !ReferenceEquals(orders[i], _tiles[i].Order))
                {
                    Rebuild();
                    return;
                }
            }

            Refresh();
        }

        private void Rebuild()
        {
            _list.Clear();
            _tiles.Clear();
            var orders = _game.Island.Orders;
            for (var i = 0; i < orders.Count; i++)
            {
                var slot = i;
                var tile = new OrderTile { Order = orders[i], Root = new VisualElement() };
                tile.Root.AddToClassList(UiClasses.Tile.Root);

                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList(UiClasses.Tile.Art);
                tile.ArtIcon = IslandUi.Icon(UiClasses.Icon.Fish, UiClasses.Tint.Fish);
                art.Add(tile.ArtIcon);
                tile.Root.Add(art);

                var body = new VisualElement { pickingMode = PickingMode.Ignore };
                body.AddToClassList(UiClasses.Tile.Body);
                tile.Big = IslandUi.Text("", UiClasses.Tile.Big);
                tile.Small = IslandUi.Text("", UiClasses.Tile.Small);
                tile.Reward = new VisualElement { pickingMode = PickingMode.Ignore };
                tile.Reward.AddToClassList(UiClasses.Counts);
                body.Add(tile.Big);
                body.Add(tile.Small);
                body.Add(tile.Reward);
                tile.Root.Add(body);

                tile.Deliver = new Button(() => Deliver(slot));
                tile.Deliver.AddToClassList(UiClasses.RoundButton.Root);
                tile.Deliver.AddToClassList(UiClasses.RoundButton.Small);
                var icon = IslandUi.Icon(UiClasses.Icon.Deliver, null);
                icon.AddToClassList(UiClasses.RoundButton.Icon);
                tile.Deliver.Add(icon);
                tile.Deliver.Add(IslandUi.Text("とどける", UiClasses.RoundButton.Label));
                tile.Root.Add(tile.Deliver);

                _list.Add(tile.Root);
                _tiles.Add(tile);
            }

            Refresh();
        }

        private void Refresh()
        {
            var island = _game.Island;
            var p = island.Parameters;
            var stage = island.HarborStage;
            IslandUi.SetThumb(_art, UiClasses.Thumb.OfHarbor(stage));
            _stage.text = $"港 Lv {stage + 1}";
            if (stage >= p.HarborStageDeliveries.Length)
            {
                _progress.value = 1;
                _next.text = "おでん祭りの港になった！";
            }
            else
            {
                var prev = stage == 0 ? 0 : p.HarborStageDeliveries[stage - 1];
                var need = p.HarborStageDeliveries[stage];
                _progress.value = (float)(island.Delivered - prev) / Math.Max(1, need - prev);
                if (Time.unscaledTime >= _flashUntil)
                {
                    _next.text = $"あと {need - island.Delivered} 回で港が育つ";
                }
            }

            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var o = tile.Order;
                if (o == null)
                {
                    IslandUi.SetVisible(tile.Root, false);
                    continue;
                }

                var ready = island.IsOrderReady(i);
                var have = island.StockOf(o.Item);
                tile.Root.EnableInClassList(UiClasses.Tile.Waiting, !ready);
                SetArt(tile.ArtIcon, ready ? IslandUi.IconClass(o.Item) : UiClasses.Icon.Boat);
                tile.Big.text = ready ? $"× {o.Count}" : Countdown(o.ArrivesAt - island.PlayTime);
                tile.Small.text = ready ? $"{IslandCatalog.NameOf(o.Item)}　{IslandUi.Number(Math.Min(have, o.Count))} / {o.Count}" : "舟が向かっているよ";
                tile.Reward.Clear();
                tile.Reward.Add(IslandUi.Count(UiClasses.Icon.Fish, UiClasses.Tint.Fish, $"+ {IslandUi.Number(o.Reward)}"));
                IslandUi.SetVisible(tile.Deliver, ready);
                tile.Deliver.SetEnabled(ready && have >= o.Count);
            }
        }

        private static void SetArt(VisualElement icon, string iconClass)
        {
            if (icon.userData is string previous)
            {
                icon.RemoveFromClassList(previous);
            }

            icon.AddToClassList(iconClass);
            icon.userData = iconClass;
        }

        private static string Countdown(double seconds)
        {
            var s = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private void Deliver(int slot)
        {
            var order = _game.Island.Orders[slot];
            var r = _game.Deliver(slot);
            if (r != IslandActionResult.Ok)
            {
                _hud?.ShowToast(IslandUi.Message(r));
                return;
            }

            // トーストはホームにしか出ないので、この画面では港の札に一瞬だけ出す。
            Rebuild();
            _next.text = $"お魚を {IslandUi.Number(order.Reward)} 匹もらったよ！";
            _flashUntil = Time.unscaledTime + 1.6f;
        }
    }
}
