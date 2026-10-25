using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class VillageUpgradeFlowTests
    {
        [UnityTest]
        public IEnumerator FacilityButtonsPreviewCancelUpgradeAndRefreshInTheRealVillageScene()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ProductionVillageHome.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var session = Object.FindFirstObjectByType<ProductionVillageSession>();
            var core = session.Core; var now = ProductionVillageSession.UtcNow;
            Assert.That(core.BeginDevelopment(now), Is.True);
            Assert.That(core.Configure(false, 1, 1500, 1, false, now), Is.True);
            Assert.That(core.Resources(1000, 1000, 1000, false, now), Is.True);
            var placed = false;
            for (var y = 0; y < 22 && !placed; y += 2)
                for (var x = 0; x < 22 && !placed; x += 2)
                    if (core.CanPlace(FacilityKind.Fishery, x, y).Length == 0) placed = core.Place(FacilityKind.Fishery, x, y, now);
            Assert.That(placed, Is.True);
            var id = core.View.Nodes.Max(n => n.Id);
            var nav = Object.FindFirstObjectByType<ProductionNavigator>(); nav.ShowFacility(id);
            yield return null;
            var root = nav.GetComponent<UIDocument>().rootVisualElement;
            root.Q<ScrollView>(ProductionUiNames.Content).ScrollTo(root.Q(className: ProductionUiNames.Upgrade));
            yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "village-upgrade-preview.png"));
            var electric = core.View.Electricity;
            Click(root.Q<Button>(VillageUpgradePanel.UpgradeButtonName)); yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "village-upgrade-confirm.png"));
            Assert.That(core.View.Nodes.Single(n => n.Id == id).Level, Is.EqualTo(1));
            Assert.That(core.View.Electricity, Is.EqualTo(electric));
            Click(root.Q<Button>(VillageUpgradePanel.CancelButtonName)); yield return null;
            Assert.That(core.View.Electricity, Is.EqualTo(electric));
            Click(root.Q<Button>(VillageUpgradePanel.UpgradeButtonName)); yield return null;
            Click(root.Q<Button>(VillageUpgradePanel.ConfirmButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single(n => n.Id == id).Level, Is.EqualTo(2));
            Assert.That(core.View.Electricity, Is.EqualTo(electric - 10));
            nav.Close(); nav.ShowFacility(id); yield return null;
            Assert.That(root.Query<Label>(className: ProductionUiNames.FacilityLevel).First().text, Does.Contain("Lv 2"));
            Assert.That(core.Resources(0, 0, 0, false, now), Is.True);
            nav.ShowFacility(id); yield return null;
            Assert.That(root.Q<Button>(VillageUpgradePanel.UpgradeButtonName).enabledSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Editor scene verification"); yield break;
#endif
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
