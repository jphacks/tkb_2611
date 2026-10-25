using System.Collections;
using NUnit.Framework;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionReportInputTests
    {
        private EditorWindow _window;
        private Button _homeButton;
        private VisualElement _report;
        private Button _close;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/ProductionVillage/ProductionApp.uxml");
            Assert.That(asset, Is.Not.Null);
            var app = asset.Instantiate();
            _homeButton = app.Q<Button>(ProductionUiNames.IndustrialSwitch);
            var reportInstance = app.Q<TemplateContainer>(ProductionUiNames.VillageProductionReport);
            _report = reportInstance?.Q(ProductionReportNames.Root);
            _close = _report?.Q<Button>(ProductionReportNames.Close);
            Assert.That(_homeButton, Is.Not.Null);
            Assert.That(reportInstance, Is.Not.Null);
            Assert.That(_report, Is.Not.Null);
            Assert.That(_close, Is.Not.Null);

            _window = ScriptableObject.CreateInstance<ProductionReportInputTestWindow>();
            _window.position = new Rect(100, 100, 640, 480);
            _window.Show();

            // Keep the asset's picking configuration; isolate its input boundary from
            // unrelated home UI and runtime-only theme/layout dependencies.
            _window.rootVisualElement.Add(_homeButton);
            _window.rootVisualElement.Add(reportInstance);
            Place(_homeButton, 200, 200, 120, 48);
            reportInstance.style.position = Position.Absolute;
            reportInstance.style.left = reportInstance.style.top = 0;
            reportInstance.style.right = reportInstance.style.bottom = 0;
            _report.style.display = DisplayStyle.None;

            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
                if (_homeButton.worldBound.width > 0 && reportInstance.worldBound.width > 0)
                    break;
            }

            Assert.That(_homeButton.panel, Is.Not.Null);
            Assert.That(_homeButton.worldBound.width, Is.GreaterThan(0));
            Assert.That(reportInstance.worldBound.Contains(_homeButton.worldBound.center), Is.True,
                "The real report instance must overlap the underlying home button.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
        }

        [UnityTest]
        public IEnumerator ClosedReportAllowsPickingUnderlyingHomeButton()
        {
            yield return null;

            var picked = _homeButton.panel.Pick(_homeButton.worldBound.center);

            Assert.That(IsWithin(_homeButton, picked), Is.True,
                $"Closed report intercepted the home button: {picked?.name} ({picked?.GetType().Name}).");
        }

        [UnityTest]
        public IEnumerator OpenReportKeepsCloseReachableAndBlocksUnderlyingHomeButton()
        {
            _report.style.display = DisplayStyle.Flex;
            Place(_report, 0, 0, 640, 480);
            // Use the real close button, while removing content layout as a variable.
            _report.Add(_close);
            Place(_close, 500, 20, 48, 48);
            yield return null;
            yield return null;

            var panel = _report.panel;
            Assert.That(IsWithin(_close, panel.Pick(_close.worldBound.center)), Is.True,
                "The report close button must remain reachable through its instance.");
            Assert.That(IsWithin(_report, panel.Pick(_homeButton.worldBound.center)), Is.True,
                "An open report must intercept input above the underlying home button.");
        }

        private static bool IsWithin(VisualElement ancestor, VisualElement picked) =>
            picked != null && (picked == ancestor || ancestor.Contains(picked));

        private static void Place(VisualElement element, float left, float top, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = left;
            element.style.top = top;
            element.style.width = width;
            element.style.height = height;
        }
    }

    public sealed class ProductionReportInputTestWindow : EditorWindow { }
}
