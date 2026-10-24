using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// ScreenNavigator が名前で探す要素が App.uxml にそろっているかを確かめる。
    /// UXML の name を変えただけでは画面遷移が実行時まで壊れたことに気づけないため。
    /// </summary>
    public sealed class AppLayoutTests
    {
        private const string AppUxmlPath = "Assets/UI/App/App.uxml";

        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AppUxmlPath);
            Assert.That(tree, Is.Not.Null, $"{AppUxmlPath} が読み込めません");
            _root = tree.Instantiate();
        }

        [Test]
        public void EveryScreenExistsInApp()
        {
            foreach (AppScreen screen in Enum.GetValues(typeof(AppScreen)))
            {
                var name = ScreenNavigator.ElementNameOf(screen);
                Assert.That(_root.Q(name), Is.Not.Null, $"{screen} の要素 '{name}' がありません");
            }
        }

        [Test]
        public void EveryTabScreenHasTabButton()
        {
            var tabBar = _root.Q(ScreenNavigator.TabBarName);
            Assert.That(tabBar, Is.Not.Null);

            foreach (AppScreen screen in Enum.GetValues(typeof(AppScreen)))
            {
                var tabName = ScreenNavigator.TabButtonOf(screen);
                if (tabName != null)
                {
                    Assert.That(tabBar.Q<Button>(tabName), Is.Not.Null, $"{screen} のタブ '{tabName}' がありません");
                }
            }
        }

        [Test]
        public void EveryRouteButtonExistsInItsScope()
        {
            foreach (var route in ScreenNavigator.Routes)
            {
                var scope = _root.Q(route.Scope);
                Assert.That(scope, Is.Not.Null, $"'{route.Scope}' がありません");
                Assert.That(scope.Q<Button>(route.Button), Is.Not.Null, $"'{route.Scope}' に Button '{route.Button}' がありません");
            }
        }

        [Test]
        public void EveryAppSettingsButtonExistsInItsScope()
        {
            foreach (var (scopeName, button) in ScreenNavigator.AppSettingsButtons)
            {
                var scope = _root.Q(scopeName);
                Assert.That(scope, Is.Not.Null, $"'{scopeName}' がありません");
                Assert.That(scope.Q<Button>(button), Is.Not.Null, $"'{scopeName}' に Button '{button}' がありません");
            }
        }
    }

    /// <summary>製造モードを変えたときの表示と、操作中のボタンの保持。</summary>
    public sealed class FacilityModeDisplayTests
    {
        private GameObject _owner;
        private GameStateSystem _game;
        private FacilityScreenPresenter _presenter;
        private Facility _facility;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Facility display (test)");
            _owner.SetActive(false);
            _game = _owner.AddComponent<GameStateSystem>();
            var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(9));
            _game.Initialize(new MemoryGameStore(), new ManualClock(now));

            var island = new IslandGame(new IslandParameters { StartElectricity = 1000, StartFish = 20000 }, now);
            island.SyncWorld(100, 1, null);
            Assert.That(island.Build(FacilityKind.Chikuwa, new Cell(4, 3)), Is.EqualTo(IslandActionResult.Ok));
            _facility = island.Facilities[0];
            SetField(_game, "_island", island);

            _presenter = _owner.AddComponent<FacilityScreenPresenter>();
            SetField(_presenter, "_game", _game);
            SetField(_presenter, "_id", _facility.Id);
            // 実物の UI Toolkit 要素を使い、シーンの保存・カメラ・歩数・DB を必要としない表示確認にする。
            foreach (var field in typeof(FacilityScreenPresenter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (typeof(VisualElement).IsAssignableFrom(field.FieldType))
                {
                    field.SetValue(_presenter, Activator.CreateInstance(field.FieldType));
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
            {
                UnityEngine.Object.DestroyImmediate(_owner);
            }
        }

        [TestCase(0, 1, "6 秒", "ガス 1.5/時")]
        [TestCase(1, 0, "10 秒", "ガス 0.5/時")]
        public void 製造モードを変えると時間とガス表示が変わりボタンは保たれる(int before, int after, string seconds, string gas)
        {
            Assert.That(_game.SetRecipe(_facility.Id, before), Is.EqualTo(IslandActionResult.Ok));
            Refresh();
            var buttons = Field<List<Button>>(_presenter, "_modeButtons").ToArray();
            var level = _facility.Level;

            Assert.That(_game.SetRecipe(_facility.Id, after), Is.EqualTo(IslandActionResult.Ok));
            Refresh();

            var text = Field<Label>(_presenter, "_speed").text;
            Assert.That(text, Does.Contain(seconds));
            Assert.That(Field<Label>(_presenter, "_consumption").text, Does.Contain(gas));
            Assert.That(Field<Label>(_presenter, "_consumption").text, Does.Contain("ごはん 60/時"));
            Assert.That(_facility.Level, Is.EqualTo(level));
            var current = Field<List<Button>>(_presenter, "_modeButtons");
            Assert.That(current.Count, Is.EqualTo(2));
            Assert.That(current[0], Is.SameAs(buttons[0]));
            Assert.That(current[1], Is.SameAs(buttons[1]));
            Assert.That(current[after].ClassListContains(UiClasses.Pill.Selected), Is.True);
            Assert.That(current[before].ClassListContains(UiClasses.Pill.Selected), Is.False);
        }

        [Test]
        public void 強化した施設は速さを反映した秒数を表示する()
        {
            _game.Island.DevSetLevel(2);
            Assert.That(_game.Upgrade(_facility.Id), Is.EqualTo(IslandActionResult.Ok));

            Refresh();

            Assert.That(Field<Label>(_presenter, "_speed").text, Does.Contain("8 秒"), "とろ火 10 秒 / Lv2 の速さ 1.25");
            Assert.That(Field<Label>(_presenter, "_consumption").text, Does.Contain("ガス 0.5/時"));
        }

        [Test]
        public void 同じモードの再描画ではレシピの要素も作り直さない()
        {
            Refresh();
            var recipe = Field<VisualElement>(_presenter, "_recipe");
            var first = recipe[0];

            Refresh();

            Assert.That(recipe[0], Is.SameAs(first));
        }

        private void Refresh() => typeof(FacilityScreenPresenter)
            .GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
