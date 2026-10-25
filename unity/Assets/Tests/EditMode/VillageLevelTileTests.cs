using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageLevelTileTests
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
        public void EveryUpgradeRecolorsExistingTileAndKeepsModelAndSelection()
        {
            var colors = new System.Collections.Generic.HashSet<Color>();
            _renderer.Render(_game.CreateView(), _id);
            var tile = Tile;
            var model = _root.transform.Find(ProductionWorldNames.Facility + _id);
            var selection = _root.GetComponentsInChildren<Image>(true).Single(i => i.name == ProductionWorldNames.Selection);
            for (var level = 1; level <= VillageUpgradeRules.MaxLevel; level++)
            {
                _renderer.Render(_game.CreateView(), _id);
                Assert.That(Tile, Is.SameAs(tile));
                Assert.That(tile.color, Is.EqualTo(VillageLevelColors.Tile(level)));
                colors.Add(tile.color);
                Assert.That(_root.transform.Find(ProductionWorldNames.Facility + _id), Is.SameAs(model));
                Assert.That(selection.gameObject.activeSelf, Is.True);
                Assert.That(selection.color.a, Is.Zero, "選択してもレベル色を覆わない");
                Assert.That(selection.transform.childCount, Is.EqualTo(4));
                Assert.That(selection.transform.GetSiblingIndex(), Is.GreaterThan(tile.transform.GetSiblingIndex()));
                if (level < VillageUpgradeRules.MaxLevel) Assert.That(_game.Upgrade(_id, level).Success, Is.True);
            }
            Assert.That(colors.Count, Is.EqualTo(5));
            _renderer.Render(_game.CreateView(), 0);
            Assert.That(selection.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void StoppedAndAmplifiedStatusAndIndustrialModeKeepLevelColor()
        {
            Assert.That(_game.Upgrade(_id, 1).Success, Is.True);
            _game.SetResources(0, 100, 100); _game.AdvanceTo(1000);
            Assert.That(_game.CreateView().Nodes.Single().Status, Is.EqualTo("ガスを待っています"));
            _renderer.Render(_game.CreateView(), 0);
            var stopped = Tile.transform.Find("Status").GetComponent<Image>().color;
            Assert.That(stopped.r, Is.GreaterThan(stopped.g));
            Assert.That(Tile.color, Is.EqualTo(VillageLevelColors.Tile(2)));
            _renderer.IndustrialMode = true; _renderer.Render(_game.CreateView(), _id);
            Assert.That(Tile.color, Is.EqualTo(VillageLevelColors.Tile(2)));
            _game.SetResources(10000, 10000, 10000);
            Assert.That(_game.Place(FacilityKind.ElectricAmplifier, 3, 0).Success, Is.True);
            _game.AdvanceTo(2000); _renderer.Render(_game.CreateView(), 0);
            Assert.That(_game.CreateView().Nodes.Single(n => n.Id == _id).Amplified, Is.True);
            var amplified = Tile.transform.Find("Status").GetComponent<Image>().color;
            Assert.That(amplified, Is.Not.EqualTo(stopped));
            Assert.That(Tile.color, Is.EqualTo(VillageLevelColors.Tile(2)));
        }

        [Test]
        public void UpgradeSwatchesMatchTilesBeforeAndAfterSavedLevelIsRestored()
        {
            for (var level = 1; level < 4; level++) Assert.That(_game.Upgrade(_id, level).Success, Is.True);
            _game = VillageGame.Restore(_game.Export());
            var view = _game.CreateView();
            _renderer.Render(view, 0);
            var card = new VisualElement(); var panel = new VillageUpgradePanel(card, (_, _) => false);
            panel.Render(view.Nodes.Single(), view.Electricity);
            Assert.That(card.Q("currentLevelTile").style.backgroundColor.value, Is.EqualTo(Tile.color));
            Assert.That(card.Q("nextLevelTile").style.backgroundColor.value, Is.EqualTo(VillageLevelColors.Tile(5)));
        }
    }
}
