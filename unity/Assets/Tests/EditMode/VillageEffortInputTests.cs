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
    public sealed class VillageEffortInputTests
    {
        private EditorWindow _window;

        [TearDown]
        public void Cleanup() { if (_window != null) _window.Close(); }

        [UnityTest]
        public IEnumerator ButtonsRaiseLowerAndResetWithoutPayingEvenWithNoGas()
        {
            var store = new ProductionVillageMemoryStore();
            var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), () =>
            {
                var game = new VillageGame(false); game.SetResources(100, 100, 100); game.Place(FacilityKind.Fishery, 0, 0);
                game.SetResources(0, 0, 0); return game;
            }, true);
            core.Open(1000);
            _window = ScriptableObject.CreateInstance<VillageEffortInputTestWindow>();
            _window.position = new Rect(50, 50, 640, 800); _window.Show();
            var card = new VisualElement(); _window.rootVisualElement.Add(card);
            var panel = new VillageEffortPanel(card, (id, effort) => core.SetEffort(id, effort, 1000));
            panel.Render(core.View.Nodes.Single(), 0);
            yield return null; yield return null;
            Assert.That(card.Q<Button>(VillageEffortPanel.DecreaseButtonName).enabledSelf, Is.False);
            for (var effort = 1; effort <= 3; effort++)
            {
                Click(card.Q<Button>(VillageEffortPanel.IncreaseButtonName)); yield return null;
                Assert.That(core.View.Nodes.Single().Effort, Is.EqualTo(effort));
                panel.Render(core.View.Nodes.Single(), 0);
            }
            Assert.That(card.Q<Button>(VillageEffortPanel.IncreaseButtonName).enabledSelf, Is.False);
            Click(card.Q<Button>(VillageEffortPanel.DecreaseButtonName)); yield return null;
            Assert.That(core.View.Nodes.Single().Effort, Is.EqualTo(2));
            panel.Render(core.View.Nodes.Single(), 0);
            Click(card.Q<Button>(VillageEffortPanel.ResetButtonName)); yield return null;
            panel.Render(core.View.Nodes.Single(), 0);
            Assert.That(core.View.Nodes.Single().Effort, Is.Zero);
            Assert.That(store.Load().State.Nodes.Single().Effort, Is.Zero);
            Assert.That(core.View.Gas + core.View.Electricity + core.View.Water, Is.Zero);
            Assert.That(card.Q<Button>(VillageEffortPanel.ResetButtonName).enabledSelf, Is.False);
            panel.Render(null, 0);
            Assert.That(card.Q<Button>(VillageEffortPanel.IncreaseButtonName).enabledSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
    public sealed class VillageEffortInputTestWindow : EditorWindow { }
}
