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
    public sealed class VillageEffortFlowTests
    {
        [UnityTest]
        public IEnumerator FacilityButtonsRaiseAndResetEffortInTheRealVillageScene()
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
            root.Q<ScrollView>(ProductionUiNames.Content).ScrollTo(root.Q(className: ProductionUiNames.Effort));
            yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "village-effort.png"));
            yield return null;
            var electric = core.View.Electricity;
            Click(root.Q<Button>(VillageEffortPanel.IncreaseButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single(n => n.Id == id).Effort, Is.EqualTo(1));
            Assert.That(core.View.Electricity, Is.EqualTo(electric));
            nav.Close(); nav.ShowFacility(id); yield return null;
            Assert.That(root.Query<Label>(className: ProductionUiNames.FacilityEffort).First().text, Does.Contain("エフォート 1"));
            Assert.That(core.Resources(0, 0, 0, false, now), Is.True);
            nav.ShowFacility(id); yield return null;
            Click(root.Q<Button>(VillageEffortPanel.ResetButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single(n => n.Id == id).Effort, Is.Zero);
            Assert.That(root.Q<Button>(VillageEffortPanel.DecreaseButtonName).enabledSelf, Is.False);
            Assert.That(root.Q<Button>(VillageEffortPanel.IncreaseButtonName).enabledSelf, Is.True);
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
