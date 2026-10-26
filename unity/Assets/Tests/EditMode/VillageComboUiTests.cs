using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class VillageComboUiTests
    {
        [UnityTest]
        public IEnumerator DirectionDiagramFitsANarrowScreenWithTheRealStyles()
        {
            var window = ScriptableObject.CreateInstance<VillageComboLayoutTestWindow>();
            try
            {
                window.position = new Rect(80, 80, 320, 700); window.Show();
                var root = window.rootVisualElement;
                foreach (var path in new[] { "Assets/UI/DesignSystem/Tokens.uss", "Assets/UI/DesignSystem/Components.uss", "Assets/UI/ProductionVillage/ProductionApp.uss" })
                    root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
                root.style.unityFontDefinition = FontDefinition.FromFont(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf"));
                var card = new VisualElement(); card.AddToClassList(ProjectPenguin.Presentation.UI.UiClasses.Card); card.AddToClassList(ProductionUiNames.Card); root.Add(card);
                var game = new VillageGame(true); game.Place(FacilityKind.Fishery, 4, 4);
                foreach (var cell in new[] { (4, 6), (6, 4), (4, 2), (2, 4) }) game.Place(FacilityKind.Fishery, cell.Item1, cell.Item2);
                new VillageComboPanel(card).Render(game.CreateView().Nodes[0]);
                yield return null; yield return null; yield return null;
                var cells = card.Query<Label>(className: ProductionUiNames.ComboCell).ToList();
                Assert.That(cells.Count, Is.EqualTo(5));
                foreach (var cell in cells)
                {
                    Assert.That(cell.worldBound.width, Is.GreaterThan(40));
                    Assert.That(cell.worldBound.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1));
                    Assert.That(cell.worldBound.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1));
                    Assert.That(cell.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1));
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); }
        }

        [Test]
        public void DetailsShowEachDirectionMaximumAndResetEveryDirectionAfterMoving()
        {
            var game = new VillageGame(true); game.Place(FacilityKind.Fishery, 4, 4);
            foreach (var cell in new[] { (4, 6), (6, 4), (4, 2), (2, 4) }) game.Place(FacilityKind.Fishery, cell.Item1, cell.Item2);
            var card = new VisualElement(); var panel = new VillageComboPanel(card); panel.Render(game.CreateView().Nodes[0]);
            Assert.That(card.Q<Label>(VillageComboPanel.CenterName).text, Does.Contain("×2").And.Contain("4/4方向"));
            Assert.That(card.Q<Label>(VillageComboPanel.HintName).text, Does.Contain("最大 ×2"));
            foreach (var direction in new[] { VillageComboDirection.Up, VillageComboDirection.Right, VillageComboDirection.Down, VillageComboDirection.Left })
                Assert.That(card.Q<Label>(VillageComboPanel.DirectionName(direction)).ClassListContains(ProductionUiNames.ComboDirectionActive), Is.True);
            game.Move(2, 12, 12); panel.Render(game.CreateView().Nodes[0]);
            Assert.That(card.Q<Label>(VillageComboPanel.DirectionName(VillageComboDirection.Up)).text, Does.Contain("未成立"));
            Assert.That(card.Q<Label>(VillageComboPanel.CenterName).ClassListContains(ProductionUiNames.ComboMaximum), Is.False);
            Assert.That(card.Q<Label>(VillageComboPanel.CenterName).text, Does.Contain("×1.75"));
            panel.Render(null);
            Assert.That(card.Query<Label>(className: ProductionUiNames.ComboDirectionActive).ToList(), Is.Empty);
        }

        [Test]
        public void DetailsFollowCurrentLayoutAndReturnToOneAfterMovingNeighbor()
        {
            var game = new VillageGame(true); game.Place(FacilityKind.Fishery, 0, 0); game.Place(FacilityKind.Fishery, 2, 0);
            var card = new VisualElement(); var panel = new VillageComboPanel(card);
            panel.Render(game.CreateView().Nodes[0]);
            Assert.That(card.Q<Label>(VillageComboPanel.SummaryName).text, Does.Contain("隣接 1棟").And.Contain("1.25"));
            game.Move(2, 10, 10); panel.Render(game.CreateView().Nodes[0]);
            Assert.That(card.Q<Label>(VillageComboPanel.SummaryName).text, Does.Contain("隣接 0棟").And.Contain("×1"));
        }

        [Test]
        public void DockAndCatalogDisplayTheSameIncreasedPriceAsTheGame()
        {
            var owner = new GameObject("Combo price UI test"); owner.SetActive(false);
            try
            {
                var core = new ProductionVillageSessionCore(new ProductionVillageMemoryStore(), new ProductionVillageMemoryStore(true), () =>
                {
                    var game = new VillageGame(false); game.SetResources(100, 100, 100);
                    game.Place(FacilityKind.Fishery, 0, 0); return game;
                }, true);
                core.Open(1000);
                var session = owner.AddComponent<ProductionVillageSession>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ProductionVillageSession).GetField("<Core>k__BackingField", flags).SetValue(session, core);
                var nav = owner.AddComponent<ProductionNavigator>();
                typeof(ProductionNavigator).GetField("_session", flags).SetValue(nav, session);
                foreach (var method in new[] { "BuildDockSlot", "ShelfCard" })
                {
                    var card = (VisualElement)typeof(ProductionNavigator).GetMethod(method, flags).Invoke(nav, new object[] { IslandCatalog.Get(FacilityKind.Fishery), core.View });
                    var labels = card.Query<Label>().ToList().Select(l => l.text);
                    Assert.That(labels, Does.Contain(core.View.BuildElectricityCost(FacilityKind.Fishery).ToString("0")), method);
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
    public sealed class VillageComboLayoutTestWindow : EditorWindow { }
}
