using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class HarborPlacementTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _owner;
        private GameObject _harbor;
        private IslandBoardView _board;
        private IslandInteraction _input;
        private Camera _camera;
        private int _taps;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Moved harbor input (test)");
            _owner.SetActive(false);
            var game = _owner.AddComponent<GameStateSystem>();
            game.Initialize(new MemoryGameStore(), new ManualClock(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero)));
            _board = _owner.AddComponent<IslandBoardView>();
            Set(_board, "_game", game);
            _harbor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _harbor.transform.position = new Vector3(-1, 0, 9.5f);
            _harbor.transform.localScale = new Vector3(1.4f, .3f, 3.5f);
            Set(_board, "_harbor", _harbor);
            _camera = _owner.AddComponent<Camera>();
            _camera.pixelRect = new Rect(0, 0, 800, 1200);
            _camera.transform.position = new Vector3(0, 15, -8);
            _camera.transform.LookAt(new Vector3(0, 0, 5));
            var rig = _owner.AddComponent<IslandCameraRig>();
            Set(rig, "_camera", _camera);
            _input = _owner.AddComponent<IslandInteraction>();
            Set(_input, "_game", game); Set(_input, "_board", _board); Set(_input, "_rig", rig);
            _input.HarborTapped += () => _taps++;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_harbor);
        }

        [Test]
        public void TapOnTheMovedHarborOutsideTheBoardOpensHarbor()
        {
            Tap(_harbor.transform.position);
            Assert.That(_taps, Is.EqualTo(1), "移動した港の見た目にタップ判定を合わせる");
        }

        [TestCase(IslandMode.Place)]
        [TestCase(IslandMode.Move)]
        [TestCase(IslandMode.Expand)]
        public void EditingModesDoNotOpenHarbor(IslandMode mode)
        {
            typeof(IslandInteraction).GetProperty("Mode").SetValue(_input, mode);
            Tap(_harbor.transform.position);
            Assert.That(_taps, Is.Zero);
        }

        [Test]
        public void OldWaterwayDoesNotOpenHarborAfterAnchoringToTheCoast()
        {
            Set(_board, "_harborAnchor", _harbor.transform);
            Tap(new Vector3(0, 0, 4));
            Assert.That(_taps, Is.Zero);
        }

        [Test]
        public void HarborFollowsTheCoastWithoutChangingItsSize()
        {
            var anchor = new GameObject("Moving coast anchor");
            try
            {
                Set(_board, "_harborAnchor", anchor.transform);
                anchor.transform.position = new Vector3(-2, -.1f, 9);
                anchor.transform.rotation = Quaternion.Euler(0, -45, 0);
                anchor.transform.localScale = Vector3.one * .8f;
                var scale = _harbor.transform.localScale;
                typeof(IslandBoardView).GetMethod("LateUpdate", Private).Invoke(_board, null);
                Assert.That(_harbor.transform.position, Is.EqualTo(anchor.transform.position));
                Assert.That(_harbor.transform.rotation, Is.EqualTo(anchor.transform.rotation));
                Assert.That(_harbor.transform.localScale, Is.EqualTo(scale));
                Assert.That(_board.HarborCenter, Is.EqualTo(_board.transform.InverseTransformPoint(anchor.transform.position)));
            }
            finally { UnityEngine.Object.DestroyImmediate(anchor); }
        }

        [Test]
        public void HarborPrefabHasAnUprightUnitRootAndTapOnlyColliders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Harbor_Gyojou_v007/Harbor_Gyojou_v007.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.x, Is.EqualTo(1.4f).Within(.01f));
            Assert.That(bounds.size.y, Is.EqualTo(1.699f).Within(.01f));
            Assert.That(bounds.size.z, Is.EqualTo(3.557f).Within(.01f));
            var colliders = prefab.GetComponentsInChildren<BoxCollider>();
            Assert.That(colliders.Length, Is.EqualTo(2));
            foreach (var collider in colliders)
            {
                Assert.That(collider.isTrigger, Is.True);
                Assert.That(collider.bounds.size.magnitude, Is.LessThan(4));
            }
        }

        private void Tap(Vector3 world) => typeof(IslandInteraction).GetMethod("Tap", Private)
            .Invoke(_input, new object[] { (Vector2)_camera.WorldToScreenPoint(world) });
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
