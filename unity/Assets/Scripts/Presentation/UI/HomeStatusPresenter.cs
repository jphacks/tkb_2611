using System.Collections.Generic;
using System.Globalization;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.World;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// ホーム画面の氷の量・今週の記録のカードとペンギンの吹き出しを、ゲームの状態 (<see cref="GameStateSystem"/>) に合わせる。
    /// 開発ビルドと Editor では、見本の写真の記録・時計と島の早送り・資源の追加の操作を出す。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HomeStatusPresenter : MonoBehaviour
    {

        [SerializeField] private GameStateSystem _game;

        private readonly Queue<string> _recentLines = new();
        private int _iceLowShownDay = int.MinValue;
        private bool _shownOnce;

        private ProgressBar _iceProgress;
        private Label _iceValue;
        private Label _iceLabel;
        private ProgressBar _weekProgress;
        private Label _weekValue;
        private VisualElement _todayIcon;
        private Label _speech;
        private VisualElement _demo;
        private Label _demoBadge;
        private Button _demoRecord;
        private Button _demoAdvance;
        private Button _demoHour;
        private Button _demoGrant;
        private Button _demoSteps;

        private void OnEnable()
        {
            if (_game == null)
            {
                Debug.LogError($"{nameof(HomeStatusPresenter)}: {nameof(GameStateSystem)} が未設定です。", this);
                enabled = false;
                return;
            }

            var home = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Home));
            _iceProgress = home.Q<ProgressBar>(UiNames.Home.IceProgress);
            _iceValue = home.Q<Label>(UiNames.Home.IceValue);
            _iceLabel = home.Q<Label>(UiNames.Home.IceLabel);
            _weekProgress = home.Q<ProgressBar>(UiNames.Home.WeekProgress);
            _weekValue = home.Q<Label>(UiNames.Home.WeekValue);
            _todayIcon = home.Q(UiNames.Home.TodayIcon);
            _speech = home.Q<Label>(UiNames.Home.SpeechText);
            _demo = home.Q(UiNames.Home.DemoControls);
            _demoBadge = home.Q<Label>(UiNames.Home.DemoBadge);
            _demoRecord = home.Q<Button>(UiNames.Home.DemoRecordButton);
            _demoAdvance = home.Q<Button>(UiNames.Home.DemoAdvanceButton);
            _demoHour = home.Q<Button>(UiNames.Home.DemoHourButton);
            _demoGrant = home.Q<Button>(UiNames.Home.DemoGrantButton);
            _demoSteps = home.Q<Button>(UiNames.Home.DemoStepsButton);

            _game.Changed += OnChanged;
            BindDemoControls(true);
            _game.Refresh();
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.Changed -= OnChanged;
            }

            BindDemoControls(false);
        }

        /// <summary>開発ビルドと Editor だけ、デモ用の操作を出す。</summary>
        private static bool DemoEnabled => Application.isEditor || Debug.isDebugBuild;

        private void BindDemoControls(bool bind)
        {
            if (_demo == null || !DemoEnabled)
            {
                return;
            }

            _demo.EnableInClassList(UiClasses.Home.DemoVisible, bind);
            if (bind)
            {
                _demoRecord.clicked += RecordSample;
                _demoAdvance.clicked += AdvanceOneDay;
                _demoHour.clicked += AdvanceIslandHour;
                _demoGrant.clicked += GrantResources;
                _demoSteps.clicked += AddSteps;
            }
            else
            {
                _demoRecord.clicked -= RecordSample;
                _demoAdvance.clicked -= AdvanceOneDay;
                _demoHour.clicked -= AdvanceIslandHour;
                _demoGrant.clicked -= GrantResources;
                _demoSteps.clicked -= AddSteps;
            }
        }

        private void RecordSample() => _game.RecordSampleReceipt();

        private void AdvanceOneDay() => _game.AdvanceDays(1);

        private void AdvanceIslandHour() => _game.DevSimulateIsland(3600);

        private void GrantResources()
        {
            _game.DevGrant(Domain.Island.Resource.Electricity, 100);
            _game.DevGrant(Domain.Island.Resource.Water, 100);
            _game.DevGrant(Domain.Island.Resource.Gas, 100);
            _game.Island.DevAddStock(Domain.Island.Item.Fish, 1000);
        }

        private void AddSteps()
        {
            var fake = _game.FakeSteps;
            if (fake == null)
            {
                return;
            }

            fake.Steps += 1000;
            _game.RequestSteps();
        }

        private void OnChanged(GameStateChange change)
        {
            var state = change.After;
            ShowIce(state);
            ShowWeek();
            ShowSpeech(change);

            if (_demoBadge != null)
            {
                var days = _game.DemoDaysAdvanced;
                _demoBadge.text = days > 0 ? $"デモ用：{days} 日進めています" : "デモ用の操作";
            }
        }

        private void ShowIce(HomeState state)
        {
            // 氷の値は下限 40〜上限 150。メーターは上限に対する割合で見せる。
            _iceProgress.value = (float)(state.Ice / GameParameters.Default.Cap * 100.0);
            _iceValue.text = state.Ice.ToString("0", CultureInfo.InvariantCulture);
            if (_iceLabel != null)
            {
                _iceLabel.text = $"氷の量（{IceVisual.TierName(state.Tier)}）";
            }
        }

        private void ShowWeek()
        {
            if (_weekValue == null)
            {
                return;
            }

            var session = _game.Session;
            var days = session.RecordDaysInWeek(AppCalendar.WeekOf(session.Today));
            _weekProgress.value = days;
            _weekValue.text = $"{days}/7";

            // 今日の分を記録したら、カメラの印をチェックに変える。
            var recorded = session.IsRecorded(session.Today);
            _todayIcon.EnableInClassList(UiClasses.Icon.Check, recorded);
            _todayIcon.EnableInClassList(UiClasses.Icon.Camera, !recorded);
        }

        private void ShowSpeech(GameStateChange change)
        {
            // 何も起きていない再描画 (同じ日のうちの復帰など) では、今の行を出し続ける。
            if (_shownOnce && change.Events == SpeechEvent.None && !change.IsFirstOpenToday && !change.IsComeback)
            {
                return;
            }

            _shownOnce = true;
            var state = change.After;
            var context = new SpeechContext(
                state.Tier,
                PenguinSpeech.DayPartOf(_game.Now),
                change.Events,
                change.IsComeback,
                change.IsFirstOpenToday,
                state.LastRecord,
                _iceLowShownDay == state.Day);
            var line = PenguinSpeech.Pick(context, _recentLines, n => Random.Range(0, n));
            _speech.text = line.Text;
            _recentLines.Enqueue(line.Id);
            while (_recentLines.Count > PenguinSpeech.RecentLimit)
            {
                _recentLines.Dequeue();
            }

            if (line.Category == SpeechCategory.IceLow)
            {
                _iceLowShownDay = state.Day;
            }
        }
    }
}
