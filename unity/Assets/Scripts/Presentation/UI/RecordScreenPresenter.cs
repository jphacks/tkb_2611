using System;
using System.Globalization;
using System.Linq;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 記録画面。今週の日ごとに受け取った資源の量 (棒グラフ)、記録日・連続・来たペンギン・納品の数、受け取りの記録を出す。
    /// 写真そのものは保存していないので、記録にあるのは分類と受け取った量だけ。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RecordScreenPresenter : MonoBehaviour
    {
        private const int HistoryLimit = 30;

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;

        private VisualElement _screen;

        private void OnEnable()
        {
            _screen = GetComponent<UIDocument>().rootVisualElement.Q(ScreenNavigator.ElementNameOf(AppScreen.Record));
            if (_navigator != null)
            {
                _navigator.ScreenShown += OnScreenShown;
            }

            Refresh();
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
            if (screen == AppScreen.Record)
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

            var session = _game.Session;
            var today = session.Today;
            var week = AppCalendar.WeekOf(today);
            var monday = Enumerable.Range(today - 6, 7).First(d => AppCalendar.WeekOf(d) == week);

            var values = new float[7];
            foreach (var a in _game.Activity)
            {
                var i = a.day - monday;
                if (i >= 0 && i < 7)
                {
                    values[i] += (float)(a.electricity + a.water + a.gas + a.allMighty);
                }
            }

            ShowWeek(_screen.Q(UiNames.Record.ResourceChart), values, today - monday);
            _screen.Q<Label>(UiNames.Record.WeekSummary).text = $"記録 {session.RecordDaysInWeek(week)} / 7 日";
            _screen.Q<Label>(UiNames.Record.TotalValue).text = $"{session.TotalRecordDays} 日";
            _screen.Q<Label>(UiNames.Record.StreakValue).text = $"{session.Streak} 日";
            _screen.Q<Label>(UiNames.Record.PenguinValue).text = $"{_game.Island.Species.Count} 種";
            _screen.Q<Label>(UiNames.Record.DeliveredValue).text = $"{_game.Island.Delivered} 回";

            var history = _screen.Q(UiNames.Record.History);
            history.Clear();
            var entries = _game.Activity.Reverse().Take(HistoryLimit).ToList();
            if (entries.Count == 0)
            {
                history.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Camera, UiClasses.Tint.Primary), "まだ記録がないよ",
                    "エコ写真を撮ると、ここに残るよ"));
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var row = HistoryRow(entries[i]);
                row.EnableInClassList(UiClasses.Row.Last, i == entries.Count - 1);
                history.Add(row);
            }
        }

        private static VisualElement HistoryRow(ActivityEntry a)
        {
            var when = DateTimeOffset.FromUnixTimeMilliseconds(a.atMs).ToOffset(TimeSpan.FromHours(9))
                .ToString("M/d HH:mm", CultureInfo.InvariantCulture);
            string title, icon, tint;
            switch (a.kind)
            {
                case ActivityEntry.Photo:
                    var category = a.category >= 0 ? (EcoCategory)a.category : EcoCategory.Unknown;
                    title = string.IsNullOrEmpty(a.label) ? EcoCategories.NameOf(category) : a.label;
                    var main = EcoCategories.MainResource(category);
                    icon = IslandUi.IconClass(main);
                    tint = IslandUi.TintClass(main);
                    break;
                case ActivityEntry.Steps:
                    title = $"歩いた（{a.label}）";
                    icon = UiClasses.Icon.Gas;
                    tint = UiClasses.Tint.Gas;
                    break;
                default:
                    title = a.label;
                    icon = UiClasses.Icon.Flag;
                    tint = UiClasses.Tint.Eco;
                    break;
            }

            var cost = IslandUi.Cost();
            IslandUi.AddCost(cost, UiClasses.Icon.Electricity, UiClasses.Tint.Electricity, a.electricity, false);
            IslandUi.AddCost(cost, UiClasses.Icon.Water, UiClasses.Tint.Water, a.water, false);
            IslandUi.AddCost(cost, UiClasses.Icon.Gas, UiClasses.Tint.Gas, a.gas, false);
            IslandUi.AddCost(cost, UiClasses.Icon.AllMighty, UiClasses.Tint.AllMighty, a.allMighty, false);
            var row = IslandUi.Row(IslandUi.IconBadge(icon, tint), title, when);
            row.Q(className: UiClasses.Row.Body).Add(cost);
            return row;
        }

        private static void ShowWeek(VisualElement chart, float[] values, int todayIndex)
        {
            var max = Mathf.Max(1f, values.Max());
            var columns = chart.Query(className: UiClasses.BarChart.Column).ToList();
            for (var i = 0; i < columns.Count && i < values.Length; i++)
            {
                var bar = columns[i].Q(className: UiClasses.BarChart.Bar);

                // 棒の高さはデータ次第で USS に書けないため、ここでだけ style に入れる。
                bar.style.height = Length.Percent(values[i] / max * 100f);
                bar.EnableInClassList(UiClasses.BarChart.BarToday, i == todayIndex);
                columns[i].Q<Label>(className: UiClasses.BarChart.Value).text = values[i] > 0 ? values[i].ToString("0", CultureInfo.InvariantCulture) : "-";
            }
        }
    }
}
