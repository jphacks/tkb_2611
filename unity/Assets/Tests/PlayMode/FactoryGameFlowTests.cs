using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Factory;
using ProjectPenguin.FactoryGame;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class FactoryGameFlowTests
    {
        private string _directory;
        private sealed class FakeReader : IFactoryRecordReader
        {
            public bool Supported => true;
            public int Cancelled;
            public void Open(string target, int serial, string date, bool barcode) { }
            public void Cancel(int serial) { Cancelled++; }
        }

        [Test]
        public void CancelDeniedTimeoutAndDateChangeNeverPublishARecord()
        {
            var reader = new FakeReader(); var day = 297; float now = 0; var records = 0;
            var input = new FactoryRecordInput("test", _ => { }, _ => records++, _ => records++, reader, () => day, () => now);
            input.Open(true); input.Receive("{\"status\":\"denied\",\"requestId\":1}");
            input.Open(false); input.Receive("{\"status\":\"cancelled\",\"requestId\":2}");
            input.Open(false); now = 121; input.Poll();
            Assert.That(reader.Cancelled, Is.EqualTo(1));
            input.Receive("{\"status\":\"ok\",\"requestId\":3,\"minutes\":240}");
            input.Open(false); day++; input.Receive("{\"status\":\"ok\",\"requestId\":4,\"minutes\":240}");
            Assert.That(records, Is.Zero);
        }

        [Test]
        public void RecordReaderPublishesAValidatedCandidateOnceForTheMatchingRequest()
        {
            var records = 0;
            var input = new FactoryRecordInput("test", _ => { }, _ => records++, _ => records++, new FakeReader(), () => 297, () => 0);
            input.Open(true);
            input.Receive("{\"status\":\"ok\",\"requestId\":99,\"barcode\":\"4901234567894\"}");
            Assert.That(records, Is.Zero);
            input.Receive("{\"status\":\"ok\",\"requestId\":1,\"barcode\":\"4901234567894\"}");
            input.Receive("{\"status\":\"ok\",\"requestId\":1,\"barcode\":\"4901234567894\"}");
            Assert.That(records, Is.EqualTo(1));
        }

        [Test]
        public void MissingScreenMinutesAndMalformedResultsDoNotBecomeZeroMinuteRecords()
        {
            var records = 0; var reader = new FakeReader();
            var input = new FactoryRecordInput("test", _ => { }, _ => records++, _ => records++, reader, () => 297, () => 0);
            input.Open(false);
            input.Receive("{\"status\":\"ok\",\"requestId\":1}");
            Assert.That(records, Is.Zero);
            input.Open(false); input.Receive("invalid-json");
            Assert.That(reader.Cancelled, Is.EqualTo(1), "Malformed callbacks must release the active native reader");
            input.Open(false); input.Receive("{\"status\":\"ok\",\"requestId\":3,\"minutes\":0}");
            Assert.That(records, Is.EqualTo(1), "An explicit zero is a valid candidate requiring confirmation");
        }

        [Serializable] private sealed class RenderSample
        { public string mode; public int frames, drawCalls, triangles, visibleRenderers; public double averageRenderSubmissionMs; public long allocatedBytes; }
        [Serializable] private sealed class RenderReport
        { public string environment = "macOS Unity Editor, 1080x1920 render target, 64 warehouses; explicit Camera.Render CPU submission; GPU/frame timing unavailable; -1 means unavailable"; public List<RenderSample> samples = new List<RenderSample>(); }

        [UnityTest]
        public IEnumerator DetailedAndCombinedViewsPreserveStateAndMeasureRenderingSeparately()
        {
#if UNITY_EDITOR
            _directory = Path.Combine(Path.GetTempPath(), "factory-render-" + Guid.NewGuid().ToString("N"));
            var catalog = FactoryStore.LoadCatalog(); var rules = FactoryStore.LoadRules();
            var state = new FactoryState { stage = 4, points = rules.stagePoints[3], builtCount = 64 };
            state.blocks.Clear();
            for (var by = 0; by < 4; by++) for (var bx = 0; bx < 4; bx++)
            {
                state.blocks.Add(new FactoryCell(bx, by));
                for (var dy = 0; dy < 2; dy++) for (var dx = 0; dx < 2; dx++)
                    state.facilities.Add(new FactoryFacility { id = state.nextId++, x = bx * 2 + dx, y = by * 2 + dy, recipe = "warehouse", level = 4 });
                state.facilities.Add(new FactoryFacility { id = state.nextId++, x = bx, y = by, recipe = "warehouse", level = 4, upper = true });
            }
            new FactoryStore(_directory, catalog, rules).Save(state);
            FactoryGameDriver.EditorSaveDirectoryOverride = _directory;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/FactoryGame.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var driver = UnityEngine.Object.FindFirstObjectByType<FactoryGameDriver>();
            Assert.That(driver.Session, Is.Not.Null, driver.Message); driver.TogglePause(); driver.FitCamera();
            var before = JsonUtility.ToJson(driver.Session.Snapshot); var report = new RenderReport();
            var camera = Camera.main; var renderTarget = new RenderTexture(1080, 1920, 24);
            camera.targetTexture = renderTarget; driver.FitCamera();
            foreach (var mode in new[] { "detail", "combined" })
            {
                if (mode == "combined") driver.ToggleOverview();
                for (var i = 0; i < 10; i++) { camera.Render(); yield return null; }
                double elapsed = 0;
                for (var i = 0; i < 30; i++)
                { var watch = System.Diagnostics.Stopwatch.StartNew(); camera.Render(); watch.Stop(); elapsed += watch.Elapsed.TotalMilliseconds; yield return null; }
                report.samples.Add(new RenderSample { mode = mode, frames = 30, averageRenderSubmissionMs = elapsed / 30,
                    drawCalls = UnityEditor.UnityStats.drawCalls > 0 ? UnityEditor.UnityStats.drawCalls : -1,
                    triangles = UnityEditor.UnityStats.triangles > 0 ? UnityEditor.UnityStats.triangles : -1,
                    visibleRenderers = driver.GetComponentsInChildren<Renderer>().Count(r => r.enabled),
                    allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() });
                Assert.That(JsonUtility.ToJson(driver.Session.Snapshot), Is.EqualTo(before));
            }
            camera.targetTexture = null; renderTarget.Release(); UnityEngine.Object.Destroy(renderTarget);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "tkb-factory-rendering.json"), JsonUtility.ToJson(report, true));
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Editor rendering measurement"); yield break;
#endif
        }

        [UnityTest]
        public IEnumerator InputsConstructionDeliveryZoomAndRestartUseOneSavedFactory()
        {
#if UNITY_EDITOR
            _directory = Path.Combine(Path.GetTempPath(), "factory-ui-" + Guid.NewGuid().ToString("N"));
            FactoryGameDriver.EditorSaveDirectoryOverride = _directory;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/FactoryGame.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var driver = UnityEngine.Object.FindFirstObjectByType<FactoryGameDriver>();
            Assert.That(driver, Is.Not.Null); Assert.That(driver.Session, Is.Not.Null, driver.Message);
            driver.TogglePause();
            Assert.That(driver.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("基本出力") && t.text.Contains("消費")), Is.True);
            Assert.That(driver.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("2×2区画") && t.text.Contains("不足")), Is.True);
            Assert.That(driver.GetComponentsInChildren<MeshRenderer>().Any(r => r.name == "BlockBoundaries"), Is.True, "Fixed-block boundaries are visible before construction");
            driver.FakeSteps(); driver.ConfirmScreen(0); driver.ConfirmBarcode("4901234567894");
            Assert.That(driver.Session.Snapshot.gas, Is.EqualTo(10));
            driver.SelectCell(0, 0); Click(driver, "を建設");
            driver.SelectCell(1, 0);
            for (var i = 0; i < 7; i++) Click(driver, "施設を切替");
            Click(driver, "を建設");
            Assert.That(driver.Session.Snapshot.facilities.Count, Is.EqualTo(2));
            driver.Apply(driver.Session.AdvanceTo(21000));
            Click(driver, "納品を確認");
            var confirmation = driver.GetComponentsInChildren<Toggle>(true).Single();
            Assert.That(confirmation.gameObject.activeInHierarchy, Is.True);
            var beforeCancel = JsonUtility.ToJson(driver.Session.Snapshot);
            Click(driver, "取消");
            Assert.That(JsonUtility.ToJson(driver.Session.Snapshot), Is.EqualTo(beforeCancel));
            Click(driver, "納品を確認"); confirmation.isOn = true; Click(driver, "確定して保存");
            Assert.That(driver.Session.Snapshot.points, Is.EqualTo(100));
            driver.ToggleOverview(); Assert.That(driver.Overview, Is.False, "S1 has not unlocked the overview");
            driver.Apply(driver.Session.AdvanceTo(121000));
            driver.Deliver("fish", 900);
            Assert.That(driver.Session.Snapshot.stage, Is.EqualTo(2));
            var beforeZoom = JsonUtility.ToJson(driver.Session.Snapshot);
            driver.ToggleOverview(); Assert.That(driver.Overview, Is.True);
            driver.ToggleOverview();
            for (var i = 0; i < 20; i++) driver.ToggleOverview();
            Assert.That(JsonUtility.ToJson(driver.Session.Snapshot), Is.EqualTo(beforeZoom));
            Assert.That(driver.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("在庫") && t.text.Contains("配送")), Is.True);
            Assert.That(driver.GetComponentsInChildren<Canvas>().Length, Is.EqualTo(1));
            var store = new FactoryStore(_directory, driver.Session.Catalog, driver.Session.Rules);
            var restored = store.Open(out var status);
            Assert.That(status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(JsonUtility.ToJson(restored.Snapshot), Is.EqualTo(beforeZoom));
            CapturePortrait(driver);
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Editor scene acceptance test"); yield break;
#endif
        }

        private static void Click(FactoryGameDriver driver, string label)
        {
            var button = driver.GetComponentsInChildren<Button>(true).Single(b => b.gameObject.activeInHierarchy
                && b.GetComponentsInChildren<Text>().Any(t => t.text.Contains(label)));
            button.onClick.Invoke();
        }

        private static void CapturePortrait(FactoryGameDriver driver)
        {
            var worldCamera = Camera.main;
            var canvas = driver.GetComponentsInChildren<Canvas>().Single();
            var target = new RenderTexture(1080, 1920, 24);
            var uiTarget = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            var uiObject = new GameObject("FactorySnapshotCamera", typeof(Camera));
            var uiCamera = uiObject.GetComponent<Camera>();
            var layers = canvas.GetComponentsInChildren<Transform>(true).Select(t => (t.gameObject, t.gameObject.layer)).ToArray();
            var originalMask = worldCamera.cullingMask;
            try
            {
                foreach (var pair in layers) pair.gameObject.layer = 5;
                worldCamera.cullingMask = ~(1 << 5); worldCamera.targetTexture = target;
                worldCamera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var worldImage = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
                worldImage.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); worldImage.Apply();
                var worldPixels = worldImage.GetPixels(); UnityEngine.Object.Destroy(worldImage);
                uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.backgroundColor = Color.clear; uiCamera.cullingMask = 1 << 5;
                uiCamera.orthographic = true; uiCamera.targetTexture = uiTarget;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); uiCamera.Render();
                RenderTexture.active = uiTarget;
                var image = new Texture2D(1080, 1920, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); image.Apply();
                var uiPixels = image.GetPixels();
                for (var i = 0; i < uiPixels.Length; i++) uiPixels[i] = Color.Lerp(worldPixels[i], uiPixels[i], uiPixels[i].a);
                image.SetPixels(uiPixels); image.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "tkb-factory-portrait.png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image); RenderTexture.active = previous;
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                worldCamera.targetTexture = null; worldCamera.cullingMask = originalMask;
                foreach (var pair in layers) pair.gameObject.layer = pair.layer;
                UnityEngine.Object.Destroy(uiObject); target.Release(); uiTarget.Release();
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(uiTarget);
            }
        }

        [TearDown]
        public void TearDown()
        {
#if UNITY_EDITOR
            FactoryGameDriver.EditorSaveDirectoryOverride = null;
#endif
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
