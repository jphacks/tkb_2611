using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageUpgradeInputTests
    {
        private EditorWindow _window;

        [TearDown]
        public void Cleanup() { if (_window != null) _window.Close(); }

        [UnityTest]
        public IEnumerator ActualButtonsCancelAndCommitExactlyOnceAndKeepUnavailableActionsDisabled()
        {
            var store = new ProductionVillageMemoryStore();
            var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), () =>
            {
                var game = new VillageGame(false); game.SetResources(100, 100, 100); game.Place(FacilityKind.Fishery, 0, 0); return game;
            }, true);
            core.Open(1000);
            _window = ScriptableObject.CreateInstance<VillageUpgradeInputTestWindow>();
            _window.position = new Rect(50, 50, 640, 800); _window.Show();
            foreach (var path in new[] { "Assets/UI/DesignSystem/Tokens.uss", "Assets/UI/DesignSystem/Components.uss", "Assets/UI/ProductionVillage/ProductionApp.uss" })
                _window.rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
            var card = new VisualElement(); _window.rootVisualElement.Add(card);
            var panel = new VillageUpgradePanel(card, (id, level) => core.Upgrade(id, level, 1000));
            panel.Render(core.View.Nodes.Single(), core.View.Electricity);
            yield return null; yield return null;
            var before = core.View.Electricity;
            Click(card.Q<Button>(VillageUpgradePanel.UpgradeButtonName)); yield return null;
            Assert.That(core.View.Electricity, Is.EqualTo(before));
            Click(card.Q<Button>(VillageUpgradePanel.CancelButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single().Level, Is.EqualTo(1));
            Click(card.Q<Button>(VillageUpgradePanel.UpgradeButtonName)); yield return null;
            Click(card.Q<Button>(VillageUpgradePanel.ConfirmButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single().Level, Is.EqualTo(2));
            Assert.That(core.View.Electricity, Is.EqualTo(before - 10));
            Assert.That(store.Load().State.Nodes.Single().Level, Is.EqualTo(2));
            panel.Render(core.View.Nodes.Single(), core.View.Electricity);
            Click(card.Q<Button>(VillageUpgradePanel.UpgradeButtonName)); yield return null;
            Assert.That(core.Upgrade(core.View.Nodes.Single().Id, 2, 1000), Is.True);
            panel.Render(core.View.Nodes.Single(), core.View.Electricity);
            yield return null;
            Assert.That(card.Q<Button>(VillageUpgradePanel.ConfirmButtonName).parent.parent.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(core.View.Nodes.Single().Level, Is.EqualTo(3));
            var gameWithNoPower = new VillageGame(false); gameWithNoPower.SetResources(0, 10, 0); gameWithNoPower.Place(FacilityKind.Fishery, 0, 0);
            panel.Render(gameWithNoPower.CreateView().Nodes.Single(), 0);
            Assert.That(card.Q<Button>(VillageUpgradePanel.UpgradeButtonName).enabledSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
    public sealed class VillageUpgradeInputTestWindow : EditorWindow { }
}
