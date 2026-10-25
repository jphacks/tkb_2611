using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageEffortTileTests
    {
        private GameObject _root;
        private VillageWorldRenderer _renderer;
        private VillageGame _game;
        private int _id;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TileTest");
            _renderer = new VillageWorldRenderer(_root.transform, null, new VillagePrefabCatalog(), null);
            _game = new VillageGame(false); _game.SetResources(10000, 10000, 10000);
            var placed = _game.Place(FacilityKind.Fishery, 0, 0);
            Assert.That(placed.Success, Is.True); _id = placed.Id;
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private Image Tile => _root.GetComponentsInChildren<Image>(true).Single(i => i.name == ProductionWorldNames.Footprint + _id);

        [Test]
        public void RaisingAndLoweringEffortChangesFlamesWhileTileAndModelStayTheSame()
        {
            _renderer.Render(_game.CreateView(), _id);
            var tile = Tile; var neutral = tile.color;
            var model = _root.transform.Find(ProductionWorldNames.Facility + _id);
            Assert.That(_root.GetComponentsInChildren<VillageEffortFlameGraphic>(true), Is.Empty);
            VillageEffortFlameGraphic first = null;
            foreach (var effort in new[] { 1, 2, 3, 1, 0 })
            {
                Assert.That(_game.SetEffort(_id, effort).Success, Is.True);
                _renderer.Render(_game.CreateView(), _id);
                var flame = _root.GetComponentsInChildren<VillageEffortFlameGraphic>(true).Single();
                first ??= flame; Assert.That(flame, Is.SameAs(first));
                Assert.That(flame.gameObject.activeInHierarchy, Is.EqualTo(effort > 0));
                if (effort > 0) Assert.That(flame.Effort, Is.EqualTo(effort));
                Assert.That(Tile, Is.SameAs(tile)); Assert.That(Tile.color, Is.EqualTo(neutral));
                Assert.That(_root.transform.Find(ProductionWorldNames.Facility + _id), Is.SameAs(model));
            }
        }

        [Test]
        public void WaitingAndPausedFacilitiesKeepConfiguredFlamesWithoutFlicker()
        {
            _game.SetEffort(_id, 3); _game.SetResources(0, 100, 100); _game.AdvanceTo(100);
            _renderer.Render(_game.CreateView(), _id);
            var flame = _root.GetComponentInChildren<VillageEffortFlameGraphic>();
            Assert.That(flame.Effort, Is.EqualTo(3)); Assert.That(flame.Animated, Is.False);
            Assert.That(Tile.transform.Find("Status").GetComponent<Image>().color.r, Is.GreaterThan(.8f));
            _game.SetResources(100, 100, 100); _game.AdvanceTo(200);
            _renderer.Render(_game.CreateView(), _id);
            Assert.That(flame.Animated, Is.True);
            var position = flame.transform.position;
            _renderer.Render(_game.CreateView(), _id, false);
            Assert.That(flame.Animated, Is.False); Assert.That(flame.gameObject.activeInHierarchy, Is.True);
            Assert.That(_game.Move(_id, 6, 6).Success, Is.True);
            _renderer.Render(_game.CreateView(), _id);
            Assert.That(flame.transform.position, Is.Not.EqualTo(position));
        }

        [Test]
        public void RestoredEffortMatchesWorldFlamesAndDetailsInsteadOfATileSwatch()
        {
            _game.SetEffort(_id, 3); _game = VillageGame.Restore(_game.Export());
            var view = _game.CreateView(); _renderer.Render(view, 0);
            Assert.That(_root.GetComponentInChildren<VillageEffortFlameGraphic>().Effort, Is.EqualTo(3));
            var card = new VisualElement(); var panel = new VillageEffortPanel(card, (_, _) => false);
            panel.Render(view.Nodes.Single(), view.Gas);
            var icons = card.Query<VisualElement>(className: ProductionUiNames.EffortFlame).ToList();
            Assert.That(icons.Count(i => !i.ClassListContains(UiClasses.Hidden)), Is.EqualTo(3));
            _game.SetEffort(_id, 0); panel.Render(_game.CreateView().Nodes.Single(), view.Gas);
            Assert.That(icons.All(i => i.ClassListContains(UiClasses.Hidden)), Is.True);
            Assert.That(card.Q("currentEffortTile"), Is.Null);
        }
    }
}
