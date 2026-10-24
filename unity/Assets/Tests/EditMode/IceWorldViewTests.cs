using System.Collections.Generic;
using System.Reflection;
using System;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Presentation.UI;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class IceWorldViewTests
    {
        [Test]
        public void ScaleAroundMovesOnlyHorizontally()
        {
            var center = new Vector3(1f, 5f, -2f);
            var point = new Vector3(3f, 0.5f, 2f);

            var moved = IceWorldView.ScaleAround(center, point, 0.5f);

            Assert.That(moved.x, Is.EqualTo(2f).Within(1e-5));
            Assert.That(moved.y, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(moved.z, Is.EqualTo(0f).Within(1e-5));
        }

        [Test]
        public void ScaleOfOneKeepsThePoint()
        {
            var point = new Vector3(3f, 0.5f, 2f);
            Assert.That(IceWorldView.ScaleAround(Vector3.zero, point, 1f), Is.EqualTo(point));
        }
    }

    public sealed class QualityAreaViewTests
    {
        private GameObject _owner;
        private GameObject _template;
        private IslandBoardView _view;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Quality areas (test)");
            _owner.SetActive(false);
            _view = _owner.AddComponent<IslandBoardView>();
            typeof(IslandBoardView).GetField("_tileRoot", Private).SetValue(_view, _owner.transform);
            _template = new GameObject("Area template (test)");
            foreach (var field in new[] { "_qualityFish", "_qualitySalt", "_qualityKelp" })
            {
                typeof(IslandBoardView).GetField(field, Private).SetValue(_view, _template);
            }
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_template);
        }

        [TestCase(Item.Fish)]
        [TestCase(Item.Salt)]
        [TestCase(Item.Kelp)]
        public void QualityAreaIsCenteredOnItsActualCellAndFollowsSeaAndLand(Item item)
        {
            var cell = new Cell(2, 3);
            SetMarker(cell, item, false);
            var markers = (Dictionary<Cell, GameObject>)typeof(IslandBoardView).GetField("_markers", Private).GetValue(_view);
            var marker = markers[cell];
            var center = IslandGrid.CellCenter(cell, _view.BoardSize);
            Assert.That(marker.transform.localPosition.x, Is.EqualTo(center.x).Within(0.0001f));
            Assert.That(marker.transform.localPosition.z, Is.EqualTo(center.z).Within(0.0001f));
            Assert.That(marker.transform.localPosition.y, Is.EqualTo(-0.34f).Within(0.0001f));

            SetMarker(cell, item, true);
            Assert.That(markers[cell], Is.SameAs(marker));
            Assert.That(marker.transform.localPosition.y, Is.EqualTo(0.01f).Within(0.0001f));
            SetMarker(cell, item, false);
            Assert.That(marker.transform.localPosition.y, Is.EqualTo(-0.34f).Within(0.0001f));
        }

        [TestCase("Fish")]
        [TestCase("SaltCrystal")]
        [TestCase("Kelp")]
        public void AreaPrefabCoversOneCellWithATapOnlyTrigger(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ProductionIsland/QualityAreas_v001/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, "色付きエリアのプレハブが必要です");
            var collider = prefab.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, "エリア説明用の当たり判定が必要です");
            Assert.That(collider.isTrigger, Is.True, "物理的に進路を塞がない判定にする");
            Assert.That(collider.size.x, Is.EqualTo(0.94f).Within(0.001f));
            Assert.That(collider.size.z, Is.EqualTo(0.94f).Within(0.001f));
            var renderer = prefab.GetComponentInChildren<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.bounds.size.x, Is.EqualTo(0.94f).Within(0.001f));
            Assert.That(renderer.bounds.size.z, Is.EqualTo(0.94f).Within(0.001f));
            Assert.That(renderer.bounds.size.y, Is.LessThan(0.001f));
            Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.mainTexture.name, Is.EqualTo(name));
            Assert.That(renderer.sharedMaterial.renderQueue, Is.EqualTo(3000));
        }

        [Test]
        public void HomeSceneUsesTheColoredAreasIncludingCrystalForSalt()
        {
            const string path = "Assets/Scenes/ProductionIslandHome.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            var closeWhenDone = !scene.isLoaded;
            if (closeWhenDone) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                IslandBoardView board = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    board = root.GetComponentInChildren<IslandBoardView>(true);
                    if (board != null) break;
                }

                Assert.That(board, Is.Not.Null);
                var so = new SerializedObject(board);
                Assert.That(AssetDatabase.GetAssetPath(so.FindProperty("_qualityFish").objectReferenceValue),
                    Is.EqualTo("Assets/ProductionIsland/QualityAreas_v001/Fish.prefab"));
                Assert.That(AssetDatabase.GetAssetPath(so.FindProperty("_qualitySalt").objectReferenceValue),
                    Is.EqualTo("Assets/ProductionIsland/QualityAreas_v001/SaltCrystal.prefab"));
                Assert.That(AssetDatabase.GetAssetPath(so.FindProperty("_qualityKelp").objectReferenceValue),
                    Is.EqualTo("Assets/ProductionIsland/QualityAreas_v001/Kelp.prefab"));
            }
            finally
            {
                if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void SetMarker(Cell cell, Item item, bool onLand) => typeof(IslandBoardView)
            .GetMethod("SetMarker", Private).Invoke(_view, new object[] { cell, (Item?)item, onLand });
    }

    public sealed class QualityAreaInteractionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _owner;
        private GameObject _world;
        private GameStateSystem _game;
        private IslandInteraction _interaction;
        private Camera _camera;
        private Vector2 _screen;
        private int _taps;
        private Cell _cell;
        private Item _item;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_world);
        }

        private void Prepare(Item item, bool land)
        {
            _taps = 0;
            _owner = new GameObject("Quality input (test)");
            _owner.SetActive(false);
            _world = new GameObject("Quality hit world (test)");
            _game = _owner.AddComponent<GameStateSystem>();
            _game.Initialize(new MemoryGameStore(), new ManualClock(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero)));
            var board = _owner.AddComponent<IslandBoardView>();
            Set(board, "_game", _game);
            Set(board, "_tileRoot", _world.transform);
            var name = item == Item.Fish ? "Fish" : item == Item.Salt ? "SaltCrystal" : "Kelp";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ProductionIsland/QualityAreas_v001/{name}.prefab");
            Set(board, item == Item.Fish ? "_qualityFish" : item == Item.Salt ? "_qualitySalt" : "_qualityKelp", prefab);
            _cell = item == Item.Fish ? new Cell(1, 3) : item == Item.Salt ? new Cell(2, 0) : new Cell(1, 6);
            typeof(IslandBoardView).GetMethod("SetMarker", Private).Invoke(board, new object[] { _cell, (Item?)item, land });
            _camera = _owner.AddComponent<Camera>();
            _camera.pixelRect = new Rect(0, 0, 800, 1200);
            _camera.transform.position = new Vector3(0, 10, -8);
            _camera.transform.LookAt(Vector3.zero);
            var rig = _owner.AddComponent<IslandCameraRig>();
            Set(rig, "_camera", _camera);
            _interaction = _owner.AddComponent<IslandInteraction>();
            Set(_interaction, "_game", _game);
            Set(_interaction, "_board", board);
            Set(_interaction, "_rig", rig);
            var tapEvent = typeof(IslandInteraction).GetEvent("QualityAreaTapped");
            Assert.That(tapEvent, Is.Not.Null, "エリアのタップを説明UIへ知らせる必要があります");
            tapEvent.AddEventHandler(_interaction, new Action<Cell, Item>((cell, quality) => { _taps++; _item = quality; Assert.That(cell, Is.EqualTo(_cell)); }));
            var marker = ((Dictionary<Cell, GameObject>)typeof(IslandBoardView).GetField("_markers", Private).GetValue(board))[_cell];
            _screen = _camera.WorldToScreenPoint(marker.transform.position);
            Physics.SyncTransforms();
        }

        [TestCase(Item.Fish, false)]
        [TestCase(Item.Fish, true)]
        [TestCase(Item.Salt, false)]
        [TestCase(Item.Salt, true)]
        [TestCase(Item.Kelp, false)]
        [TestCase(Item.Kelp, true)]
        public void TappingRenderedAreaReportsTheCorrectCellAndItem(Item item, bool land)
        {
            Prepare(item, land);
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { _screen });
            Assert.That(_taps, Is.EqualTo(1));
            Assert.That(_item, Is.EqualTo(item));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void DragOrUiPressDoesNotOpenAnAreaDescription(bool dragging, bool ui)
        {
            Prepare(Item.Fish, false);
            Set(_interaction, "_pressing", true);
            Set(_interaction, "_dragging", dragging);
            Set(_interaction, "_startedOnUi", ui);
            typeof(IslandInteraction).GetMethod("Release", Private).Invoke(_interaction, new object[] { _screen });
            Assert.That(_taps, Is.Zero);
        }

        [Test]
        public void ExpansionModeStillAttemptsExpansionInsteadOfOpeningDescription()
        {
            Prepare(Item.Fish, false);
            typeof(IslandInteraction).GetProperty("Mode").SetValue(_interaction, IslandMode.Expand);
            var attempts = 0;
            _interaction.ExpandAttempted += (_, _) => attempts++;
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { _screen });
            Assert.That(_taps, Is.Zero);
            Assert.That(attempts, Is.EqualTo(1));
        }

        [TestCase(IslandMode.Place)]
        [TestCase(IslandMode.Move)]
        public void PlacementModesKeepUpdatingTheirCandidateInsteadOfOpeningDescription(IslandMode mode)
        {
            Prepare(Item.Fish, false);
            typeof(IslandInteraction).GetProperty("Mode").SetValue(_interaction, mode);
            typeof(IslandInteraction).GetProperty("PlacingKind").SetValue(_interaction, FacilityKind.Fishery);
            var changes = 0;
            _interaction.Changed += () => changes++;
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { _screen });
            Assert.That(_taps, Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void FacilityOnAQualityCellStillOpensTheFacility()
        {
            Prepare(Item.Fish, true);
            var island = new IslandGame(new IslandParameters { StartWater = 1000, StartFish = 20000, StartElectricity = 1000 }, _game.Now);
            island.SyncWorld(200, 1, null);
            foreach (var cell in new[] { new Cell(2, 3), new Cell(1, 3), new Cell(2, 4), new Cell(1, 4) })
            {
                Assert.That(island.Expand(cell), Is.EqualTo(IslandActionResult.Ok));
            }

            Assert.That(island.Build(FacilityKind.Fishery, _cell), Is.EqualTo(IslandActionResult.Ok));
            Set(_game, "_island", island);
            var selected = -1;
            _interaction.FacilitySelected += id => selected = id;
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { _screen });
            Assert.That(_taps, Is.Zero);
            Assert.That(selected, Is.EqualTo(island.Facilities[0].Id));
        }

        [Test]
        public void TappingOutsideTheAreaDoesNotOpenDescription()
        {
            Prepare(Item.Fish, false);
            var outside = _camera.WorldToScreenPoint(Vector3.zero);
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { (Vector2)outside });
            Assert.That(_taps, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LandInFrontOfASeaAreaOccludesItsDescription(bool withFacility)
        {
            Prepare(Item.Fish, false);
            var island = new IslandGame(new IslandParameters { StartWater = 1000, StartFish = 20000, StartElectricity = 1000 }, _game.Now);
            island.SyncWorld(200, 1, null);
            foreach (var cell in new[] { new Cell(2, 2), new Cell(1, 2), new Cell(2, 1), new Cell(1, 1) })
            {
                Assert.That(island.Expand(cell), Is.EqualTo(IslandActionResult.Ok));
            }

            if (withFacility) Assert.That(island.Build(FacilityKind.Fishery, new Cell(1, 1)), Is.EqualTo(IslandActionResult.Ok));
            Set(_game, "_island", island);
            var screen = (Vector2)_camera.WorldToScreenPoint(IslandGrid.CellCenter(_cell, 10) + new Vector3(0, -0.34f, -0.44f));
            var rig = (IslandCameraRig)typeof(IslandInteraction).GetField("_rig", Private).GetValue(_interaction);
            Assert.That(rig.TryGround(screen, out var ground), Is.True);
            Assert.That(IslandGrid.CellAt(ground, 10), Is.EqualTo(new Cell(1, 2)));
            var selected = -1;
            _interaction.FacilitySelected += id => selected = id;
            typeof(IslandInteraction).GetMethod("Tap", Private).Invoke(_interaction, new object[] { screen });
            Assert.That(_taps, Is.Zero, "手前の陸に隠れた海のマークは拾わない");
            Assert.That(selected, Is.EqualTo(withFacility ? island.Facilities[0].Id : -1));
        }

        [TestCase(Item.Fish, "豊漁", "漁場")]
        [TestCase(Item.Salt, "塩の結晶", "製塩所")]
        [TestCase(Item.Kelp, "海藻", "海藻場")]
        public void DescriptionExplainsTheMatchingFacilityAndCurrentBonus(Item item, string title, string facility)
        {
            var sea = IslandUi.QualityAreaDescription(item, false, 1.75);
            Assert.That(sea, Does.Contain(title));
            Assert.That(sea, Does.Contain(facility));
            Assert.That(sea, Does.Contain("×1.75"));
            Assert.That(sea, Does.Contain("島を広げる"));
            Assert.That(IslandUi.QualityAreaDescription(item, true, 1.5), Does.Not.Contain("島を広げる"));
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
