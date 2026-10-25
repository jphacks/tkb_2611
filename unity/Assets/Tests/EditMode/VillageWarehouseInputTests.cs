using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageWarehouseInputTests
    {
        [UnityTest]
        public IEnumerator DraftSurvivesLiveUpdatesAndSaveUsesTheTypedNumber()
        {
            var window = ScriptableObject.CreateInstance<VillageWarehouseInputTestWindow>(); window.Show();
            try
            {
                var game = new VillageGame(false); game.SetResources(100, 100, 100);
                var id = game.Place(FacilityKind.Warehouse, 0, 0).Id; game.Inject(id, Item.Fish, 5);
                var card = new VisualElement(); window.rootVisualElement.Add(card);
                var panel = new VillageWarehousePanel(card, (nodeId, value) => game.SetWarehouseCapacity(nodeId, value).Success);
                panel.Render(game.CreateView().Nodes.Single(), 0); yield return null;
                var field = card.Q<IntegerField>(VillageWarehousePanel.InputName);
                var save = card.Q<Button>(VillageWarehousePanel.SaveButtonName);
                field.value = 50; panel.Render(game.CreateView().Nodes.Single(), 0);
                Assert.That(field.value, Is.EqualTo(50)); Assert.That(save.enabledSelf, Is.True);
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = save; save.SendEvent(evt); }
                yield return null; panel.Render(game.CreateView().Nodes.Single(), 0);
                Assert.That(game.CreateView().Nodes.Single().Capacity, Is.EqualTo(50)); Assert.That(save.enabledSelf, Is.False);
                field.value = 0; Assert.That(save.enabledSelf, Is.False);
                field.value = 4; Assert.That(save.enabledSelf, Is.False);
                field.value = 5; panel.Render(game.CreateView().Nodes.Single(), 1); Assert.That(save.enabledSelf, Is.False);
                panel.Render(game.CreateView().Nodes.Single(), 0); Assert.That(save.enabledSelf, Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }
    }
    public sealed class VillageWarehouseInputTestWindow : EditorWindow { }
}
