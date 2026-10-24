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
    /// ペンギン図鑑。来たペンギンは事実の一行と島へのボーナスを、まだのペンギンは「？」と会える条件 (実績) の進み具合を出す。
    /// 条件は <see cref="PenguinUnlocks"/>。記録の節目は氷の状態機械 (<see cref="GameSession"/>)、ほかは島の実績で来る。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DexScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;

        private VisualElement _screen;

        private void OnEnable()
        {
            _screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Dex));
            if (_navigator != null)
            {
                _navigator.ScreenShown += OnScreenShown;
            }
        }

        private void OnDisable()
        {
            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }
        }

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Dex)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_game == null || _screen == null)
            {
                return;
            }

            // 記録の節目で来た種と、島の実績で来た種を合わせる。
            var island = _game.Island;
            var arrived = new HashSet<string>(island.Species);
            _screen.Q<Label>(UiNames.Dex.DexCount).text = $"{arrived.Count} / {PenguinDex.Entries.Count} 種";

            // 来た順に並べ、まだのペンギンは解放の条件の順で後ろにまとめる。
            var order = _game.Session.Arrivals.Select(a => a.Species)
                .Concat(island.AchievedSpecies)
                .Concat(PenguinUnlocks.All.Select(u => u.Species))
                .Distinct().ToList();
            var grid = _screen.Q(UiNames.Dex.DexGrid);
            grid.Clear();
            foreach (var species in order)
            {
                var entry = PenguinDex.Find(species);
                if (entry != null)
                {
                    grid.Add(Card(entry, PenguinUnlocks.Find(species), arrived.Contains(species)));
                }
            }
        }

        private VisualElement Card(PenguinEntry entry, PenguinUnlock unlock, bool arrived)
        {
            var card = new VisualElement();
            card.AddToClassList(UiClasses.DexCard.Root);
            card.EnableInClassList(UiClasses.DexCard.Locked, !arrived);
            var image = IslandUi.Icon(UiClasses.Icon.Penguin, arrived ? UiClasses.Tint.Original : null);
            image.AddToClassList(UiClasses.DexCard.Image);
            card.Add(image);
            card.Add(IslandUi.Text(arrived ? entry.Name : "？？？", UiClasses.DexCard.Name));
            if (arrived)
            {
                card.Add(IslandUi.Text(entry.Fact, UiClasses.DexCard.Fact));
                var bonus = IslandUi.Text(entry.BonusText, UiClasses.Badge.Root, UiClasses.Badge.Eco);
                bonus.AddToClassList(UiClasses.DexCard.Bonus);
                card.Add(bonus);
                return card;
            }

            // まだの種は、会える条件 (実績) と進み具合を見せる。
            card.Add(IslandUi.Text(unlock?.Condition ?? "記録を続けると会えるかも", UiClasses.DexCard.Fact));
            if (unlock != null)
            {
                var progress = unlock.Progress(_game.Session, _game.Island);
                var text = unlock.IsMet(_game.Session, _game.Island)
                    ? "もうすぐ来るよ"
                    : $"{IslandUi.Number(progress)} / {IslandUi.Number(unlock.Goal)}";
                var badge = IslandUi.Text(text, UiClasses.Badge.Root);
                badge.AddToClassList(UiClasses.DexCard.Bonus);
                card.Add(badge);
            }

            return card;
        }
    }
}
