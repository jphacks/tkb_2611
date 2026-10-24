using System;
using System.Linq;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// ミッション画面。日付で決まる今日の 3 件から 2 件まで選び、夜 (18 時〜) に振り返る (docs/game-logic.md §3・§4)。
    /// 振り返りはどの答えでも記録日になり、オールマイティがもらえる。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionScreenPresenter : MonoBehaviour
    {
        private const int EveningHour = 18;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private IslandHudPresenter _hud;

        private VisualElement _list;
        private Label _count;
        private Label _speech;

        private void OnEnable()
        {
            if (_game == null || _navigator == null)
            {
                Debug.LogError($"{nameof(MissionScreenPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            var screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Mission));
            _list = screen.Q(UiNames.Mission.MissionList);
            _count = screen.Q<Label>(UiNames.Mission.SelectionCount);
            _speech = screen.Q<Label>(UiNames.Mission.MissionSpeech);
            _navigator.ScreenShown += OnScreenShown;
            _game.Changed += OnChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }

            if (_game != null)
            {
                _game.Changed -= OnChanged;
            }
        }

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            if (screen == AppScreen.Mission)
            {
                Rebuild();
            }
        }

        private void OnChanged(GameStateChange change)
        {
            if (_navigator.Current == AppScreen.Mission)
            {
                Rebuild();
            }
        }

        /// <summary>振り返りができる時間か。Editor と開発ビルドではいつでもできる (確かめやすくするため)。</summary>
        private bool IsEvening
        {
            get
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    return true;
                }

                var hour = _game.Now.ToOffset(TimeSpan.FromHours(9)).Hour;
                return hour >= EveningHour || hour < 3;
            }
        }

        private void Rebuild()
        {
            if (_list == null)
            {
                return;
            }

            var session = _game.Session;
            var today = session.Today;
            var selected = session.SelectedOn(today);
            var max = session.SelectionsPerDay;
            _count.text = $"{selected.Count} / {max} 選択";
            _list.Clear();

            var daily = MissionCatalog.DailyFor(today);
            var allChecked = selected.Count > 0 && selected.All(id => session.IsCheckedIn(today, id));
            _speech.text = selected.Count == 0
                ? "今日はどれにする？2 つまで選べるよ"
                : allChecked
                    ? "今日の振り返りはおしまい！また明日ね"
                    : IsEvening ? "今日はどうだった？正直に教えてね" : "夜になったら振り返りに来てね";

            foreach (var m in daily)
            {
                _list.Add(Card(m, selected.Contains(m.Id), session.IsCheckedIn(today, m.Id), selected.Count < max));
            }
        }

        private VisualElement Card(MissionDef m, bool isSelected, bool isChecked, bool canSelect)
        {
            var card = new VisualElement();
            card.AddToClassList(UiClasses.Card);
            card.AddToClassList(UiClasses.Elevated);
            card.AddToClassList(UiClasses.MissionCard.Root);
            card.AddToClassList(UiClasses.Mission.Card);
            card.EnableInClassList(UiClasses.Mission.CardSelected, isSelected && !isChecked);
            card.EnableInClassList(UiClasses.Mission.CardDone, isChecked);

            var main = EcoCategories.MainResource(m.Category);
            card.Add(IslandUi.IconBadge(IslandUi.IconClass(main), IslandUi.TintClass(main), true));

            var body = new VisualElement();
            body.AddToClassList(UiClasses.MissionCard.Body);
            body.Add(IslandUi.Text(m.Title, UiClasses.MissionCard.Title));
            body.Add(IslandUi.Text($"{m.Hint}（{EcoCategories.NameOf(m.Category)}）", UiClasses.Text.Caption, UiClasses.Text.Sub, UiClasses.Mission.Hint));
            card.Add(body);

            if (isChecked)
            {
                body.Add(IslandUi.Text("振り返り済み", UiClasses.Badge.Root, UiClasses.Badge.Eco));
            }
            else if (isSelected)
            {
                if (IsEvening)
                {
                    var answers = new VisualElement();
                    answers.AddToClassList(UiClasses.Mission.Answers);
                    answers.Add(IslandUi.Pill("できた！", () => CheckIn(m, CheckInAnswer.Done), UiClasses.Pill.Primary));
                    answers.Add(IslandUi.Pill("少しだけ", () => CheckIn(m, CheckInAnswer.Partial)));
                    answers.Add(IslandUi.Pill("できなかった", () => CheckIn(m, CheckInAnswer.Failed)));
                    body.Add(answers);
                }
                else
                {
                    body.Add(IslandUi.Text("挑戦中！夜に振り返ろう", UiClasses.Badge.Root));
                }
            }
            else
            {
                var select = IslandUi.Pill("これにする", () => Select(m), UiClasses.Pill.Primary);
                select.SetEnabled(canSelect);
                card.Add(select);
            }

            return card;
        }

        private void Select(MissionDef m)
        {
            if (_game.SelectMission(m))
            {
                _hud?.ShowToast($"「{m.Title}」に挑戦！");
            }

            Rebuild();
        }

        private void CheckIn(MissionDef m, CheckInAnswer answer)
        {
            var granted = _game.CheckIn(m, answer);
            if (granted != null)
            {
                var amount = granted[Resource.AllMighty];
                _hud?.ShowToast(answer == CheckInAnswer.Done
                    ? $"すごい！オールマイティ +{amount:0}"
                    : $"教えてくれてありがとう。オールマイティ +{amount:0}");
            }

            Rebuild();
        }
    }
}
