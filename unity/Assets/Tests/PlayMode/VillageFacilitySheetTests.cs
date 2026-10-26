using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class VillageFacilitySheetTests
    {
        [UnityTest]
        public IEnumerator FacilityOpensFromBottomAndScrollsToStockWithoutOpeningTheFullPage()
        {
#if UNITY_EDITOR
            var owner = new GameObject("Facility sheet verification"); owner.SetActive(false);
            try
            {
                var control = owner.AddComponent<ProductionVillageControl>();
                control.InspectorDebugSettings.Enabled = true;
                control.InspectorDebugSettings.TimeScale = 0;
                var session = owner.AddComponent<ProductionVillageSession>();
                var world = owner.AddComponent<VillageWorldPresenter>();
                Set(world, "_session", session); Set(world, "_catalog", new VillagePrefabCatalog());
                var document = owner.AddComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/ProductionVillage/PanelSettings.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/ProductionVillage/ProductionApp.uxml");
                var nav = owner.AddComponent<ProductionNavigator>();
                Set(nav, "_session", session); Set(nav, "_world", world);
                owner.SetActive(true); yield return null; yield return null;
                Assert.That(session.UsingInspectorDebugLayout, Is.True, "Keep game saves isolated");
                Assert.That(session.Core.SetTutorial(-1, ProductionVillageSession.UtcNow), Is.True);
                nav.ShowFacility(session.Core.View.Nodes[0].Id);
                yield return new WaitForSecondsRealtime(.35f);
                var root = document.rootVisualElement;
                var sheet = root.Q(ProductionUiNames.Sheet);
                var panel = sheet.Q(className: "village-sheet__panel");
                var content = root.Q<ScrollView>(ProductionUiNames.Content);
                Assert.That(sheet.ClassListContains(UiClasses.Hidden), Is.False);
                Assert.That(root.Q(ProductionUiNames.WorkshopPage).ClassListContains(UiClasses.Hidden), Is.True);
                Assert.That(nav.OpenPageName, Is.EqualTo(ProductionNavigator.FacilityPage), "Tutorial must still detect the facility");
                Assert.That(panel.worldBound.height / sheet.worldBound.height, Is.InRange(.48f, .52f));
                Assert.That(panel.worldBound.yMin, Is.GreaterThan(sheet.worldBound.yMin + sheet.worldBound.height * .45f));
                Assert.That(content.Q<Label>(VillageComboPanel.SummaryName), Is.Not.Null);
                Assert.That(content.Q<Button>(VillageEffortPanel.IncreaseButtonName), Is.Not.Null);
                Assert.That(content.verticalScroller.highValue, Is.GreaterThan(0));
                content.scrollOffset = new Vector2(0, content.verticalScroller.highValue);
                yield return null; yield return null;
                var lastCard = content.contentContainer[content.contentContainer.childCount - 1];
                Assert.That(lastCard.worldBound.Overlaps(content.contentViewport.worldBound), Is.True, "Stock must be reachable at the bottom");
                using (var evt = NavigationSubmitEvent.GetPooled())
                {
                    var close = root.Q<Button>(ProductionUiNames.Close); evt.target = close; close.SendEvent(evt);
                }
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(sheet.ClassListContains(UiClasses.Hidden), Is.True);
                Assert.That(nav.OpenPageName, Is.Null);
                Assert.That(world.SelectedId, Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.Destroy(owner); }
            yield return null;
#else
            Assert.Ignore("Editor UI verification"); yield break;
#endif
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
