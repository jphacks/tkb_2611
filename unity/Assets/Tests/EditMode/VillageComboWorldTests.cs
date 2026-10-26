using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageComboWorldTests
    {
        private GameObject _root;
        private VillageWorldRenderer _renderer;
        private VillageGame _game;

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("Combo world test");
            _renderer = new VillageWorldRenderer(_root.transform, null, new VillagePrefabCatalog(), null);
            _game = new VillageGame(true); _game.Place(FacilityKind.Fishery, 4, 4); _game.Place(FacilityKind.Fishery, 6, 4);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_root);
        private Image[] Lines => _root.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith(ProductionWorldNames.ComboLink)).ToArray();

        [Test]
        public void OneUndirectedBridgeIsReusedAndSelectionHighlightsBothFacilities()
        {
            _renderer.Render(_game.CreateView(), 0); var line = Lines.Single(); var faint = line.color.a;
            Assert.That(line.raycastTarget, Is.False);
            _renderer.Render(_game.CreateView(), 1);
            Assert.That(Lines.Single(), Is.SameAs(line)); Assert.That(line.color.a, Is.GreaterThan(faint));
            Assert.That(_root.GetComponentsInChildren<Image>().Count(i => i.enabled && i.name.StartsWith(ProductionWorldNames.ComboOutline)), Is.EqualTo(8));
            var expected = VillageWorldRenderer.Flat(VillageWorldRenderer.Position(4, 4, 2)) + Vector2.right * (2 * VillageWorldRenderer.Cell * .4f);
            Assert.That(Vector2.Distance(line.rectTransform.anchoredPosition, expected), Is.LessThan(.001f));
            _game.Move(2, 10, 10); _renderer.Render(_game.CreateView(), 1);
            Assert.That(line.enabled, Is.False);
            Assert.That(_root.GetComponentsInChildren<Image>().Any(i => i.enabled && i.name.StartsWith(ProductionWorldNames.ComboOutline)), Is.False);
            _game.Move(2, 6, 4); _renderer.Render(_game.CreateView(), 1);
            Assert.That(Lines.Single(), Is.SameAs(line)); Assert.That(line.enabled, Is.True);
        }

        [Test]
        public void DiagonalAndDifferentKindsDoNotCreateBridgesAndFourSidesCreateExactlyFour()
        {
            _game.Move(2, 6, 6); _game.Place(FacilityKind.Saltworks, 6, 4);
            _renderer.Render(_game.CreateView(), 1); Assert.That(Lines.Any(i => i.enabled), Is.False);
            _game.Move(2, 16, 16); _game.Move(3, 20, 20);
            foreach (var cell in new[] { (4, 6), (6, 4), (4, 2), (2, 4) }) _game.Place(FacilityKind.Fishery, cell.Item1, cell.Item2);
            _renderer.Render(_game.CreateView(), 1);
            Assert.That(Lines.Count(i => i.enabled), Is.EqualTo(4));
        }

        [Test]
        public void PreviewAddsBridgesAndCancellationRestoresTheActualLayout()
        {
            var view = _game.CreateView();
            var preview = VillageComboRules.Preview(view.Nodes, FacilityKind.Fishery, 4, 6);
            _renderer.Render(view, 0, true, preview);
            Assert.That(Lines.Count(i => i.enabled), Is.EqualTo(2));
            _renderer.Render(view, 0);
            Assert.That(Lines.Count(i => i.enabled), Is.EqualTo(1));
            Assert.That(_game.CreateView().Nodes.Count, Is.EqualTo(2));
            preview = VillageComboRules.Preview(view.Nodes, FacilityKind.Fishery, 10, 10, 2);
            _renderer.Render(view, 2, true, preview);
            Assert.That(Lines.Any(i => i.enabled), Is.False);
        }
    }
}
