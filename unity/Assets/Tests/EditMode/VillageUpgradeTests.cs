using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageUpgradeTests
    {
        private static VillageGame Game()
        {
            var game = new VillageGame(false); game.SetResources(10000, 10000, 10000); return game;
        }
        private static void Succeeds(VillageResult result) => Assert.That(result.Success, Is.True, result.Reason);
        private static VillageNodeView Node(VillageGame game, int id) => game.CreateView().Nodes.Single(n => n.Id == id);

        [Test]
        public void UpgradeSpendsTheQuoteOnceAndDoesNotAddMaterialOrGasCosts()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var before = game.CreateView(); var quote = game.PreviewUpgrade(id);
            Assert.That(quote.ElectricityCost, Is.EqualTo(10));
            Succeeds(game.Upgrade(id, 1));
            Assert.That(Node(game, id).Level, Is.EqualTo(2));
            Assert.That(game.CreateView().Electricity, Is.EqualTo(before.Electricity - quote.ElectricityCost));
            Assert.That(game.CreateView().Gas, Is.EqualTo(before.Gas)); Assert.That(game.CreateView().Water, Is.EqualTo(before.Water));
            var saved = JsonUtility.ToJson(game.Export());
            Assert.That(game.Upgrade(id, 1).Success, Is.False);
            Assert.That(JsonUtility.ToJson(game.Export()), Is.EqualTo(saved));
            Assert.That(game.PreviewUpgrade(id).ElectricityCost, Is.EqualTo(20));
        }

        [Test]
        public void InsufficientElectricityUnknownFacilityAndMaxLevelNeverSpend()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 0, 0).Id;
            game.SetResources(0, 9, 0); var before = JsonUtility.ToJson(game.Export());
            Assert.That(game.PreviewUpgrade(id).Reason, Does.Contain("あと 1"));
            Assert.That(game.Upgrade(id, 1).Success, Is.False); Assert.That(game.Upgrade(999, 1).Success, Is.False);
            Assert.That(JsonUtility.ToJson(game.Export()), Is.EqualTo(before));
            game.SetResources(0, 1000, 0);
            for (var level = 1; level < VillageUpgradeRules.MaxLevel; level++) Succeeds(game.Upgrade(id, level));
            before = JsonUtility.ToJson(game.Export());
            Assert.That(game.PreviewUpgrade(id).CanUpgrade, Is.False);
            Assert.That(game.PreviewUpgrade(id).Reason, Does.Contain("最大"));
            Assert.That(game.Upgrade(id, VillageUpgradeRules.MaxLevel).Success, Is.False);
            Assert.That(JsonUtility.ToJson(game.Export()), Is.EqualTo(before));
        }

        [Test]
        public void PaidBatchAndDeliveryDeadlinesRemainUnchangedButNextBatchRunsFaster()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 0, 0).Id;
            game.Place(FacilityKind.Warehouse, 3, 0); game.AdvanceTo(4200);
            var before = game.Export(); Assert.That(before.Cargo.Count, Is.GreaterThan(0));
            Succeeds(game.Upgrade(id, 1)); var after = game.Export();
            Assert.That(after.Nodes.Select(n => n.Finish), Is.EqualTo(before.Nodes.Select(n => n.Finish)));
            Assert.That(after.Cargo.Select(c => c.End), Is.EqualTo(before.Cargo.Select(c => c.End)));
            Assert.That(Node(game, id).ProductionSeconds, Is.EqualTo(3.2).Within(.001));
            game.AdvanceTo(before.Nodes.Single(n => n.Id == id).Finish + 100);
            var next = game.Export().Nodes.Single(n => n.Id == id);
            Assert.That(next.Finish - next.Started, Is.EqualTo(3200));
        }

        [TestCase(FacilityKind.Dormitory)]
        [TestCase(FacilityKind.Research)]
        [TestCase(FacilityKind.ElectricAmplifier)]
        [TestCase(FacilityKind.WaterAmplifier)]
        [TestCase(FacilityKind.GasAmplifier)]
        public void SupportUpgradeImprovesNearbyOrGlobalProductionAndReportRates(FacilityKind kind)
        {
            var game = Game(); var fish = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var support = game.Place(kind, 3, 0).Id;
            var before = Node(game, fish).ProductionSeconds; var rate = game.CreateView().ItemRates[Item.Fish].TheoreticalMakePerMinute;
            Succeeds(game.Upgrade(support, 1));
            Assert.That(Node(game, fish).ProductionSeconds, Is.LessThan(before));
            Assert.That(game.CreateView().ItemRates[Item.Fish].TheoreticalMakePerMinute, Is.GreaterThan(rate));
        }

        [Test]
        public void StorageAndCoolingUpgradesPreserveLargeStockAcrossJsonRestart()
        {
            var game = Game(); var warehouse = game.Place(FacilityKind.Warehouse, 0, 0).Id;
            var ice = game.Place(FacilityKind.IceHouse, 2, 0).Id;
            Assert.That(Node(game, warehouse).Capacity, Is.EqualTo(40));
            for (var level = 1; level < VillageUpgradeRules.MaxLevel; level++)
            { Succeeds(game.Upgrade(warehouse, level)); Succeeds(game.Upgrade(ice, level)); }
            Assert.That(Node(game, warehouse).Capacity, Is.EqualTo(160));
            Succeeds(game.Inject(warehouse, Item.Fish, 160));
            var restored = VillageGame.Restore(JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(game.Export())));
            Assert.That(Node(restored, warehouse).Level, Is.EqualTo(5));
            Assert.That(Node(restored, warehouse).Stock[Item.Fish], Is.EqualTo(160));
            Assert.That(restored.Inject(warehouse, Item.Salt, 1).Success, Is.False);
        }

        [Test]
        public void SorterUpgradeReachesDistantWarehouseWithoutTeleportingCargo()
        {
            var game = Game(); var source = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var sorter = game.Place(FacilityKind.Sorter, 0, 3).Id; var destination = game.Place(FacilityKind.Warehouse, 14, 0).Id;
            Assert.That(game.CreateView().Routes.Any(r => r.From == source && r.To == destination), Is.False);
            Succeeds(game.Upgrade(sorter, 1));
            Assert.That(game.CreateView().Routes.Any(r => r.From == source && r.To == destination), Is.True);
            Succeeds(game.Inject(source, Item.Fish, 1)); game.AdvanceTo(100);
            Assert.That(game.CreateView().Cargo.Any(c => c.To == destination), Is.True);
            Assert.That(Node(game, destination).Total, Is.Zero);
        }

        [Test]
        public void LegacyLevelAndSaveVersionAreAcceptedAndFutureOrInvalidLevelsAreRejected()
        {
            var game = Game(); game.Place(FacilityKind.Warehouse, 0, 0); var state = game.Export(); state.Nodes[0].Level = 0;
            var old = new ProductionVillageSave { Version = 1, State = state, UtcMs = 1000 };
            Assert.DoesNotThrow(() => old.Validate("production"));
            Assert.That(VillageGame.Restore(state).CreateView().Nodes[0].Level, Is.EqualTo(1));
            old.Version = ProductionVillageSave.CurrentVersion + 1; Assert.Throws<InvalidOperationException>(() => old.Validate("production"));
            state.Nodes[0].Level = 6; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state.Nodes[0].Level = -1; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
        }

        [Test]
        public void EveryFacilityHasAMeaningfulUpgradeAndPreviewIsDetached()
        {
            foreach (var kind in Enum.GetValues(typeof(FacilityKind)).Cast<FacilityKind>())
            {
                var game = Game(); var id = game.Place(kind, 0, 0).Id; var preview = game.PreviewUpgrade(id);
                Assert.That(preview.CanUpgrade, Is.True); Assert.That(preview.NextEffect, Is.Not.EqualTo(preview.CurrentEffect));
                Succeeds(game.Upgrade(id, 1)); Assert.That(Node(game, id).Level, Is.EqualTo(2));
                Assert.That(preview.Level, Is.EqualTo(1));
            }
        }

        private sealed class Store : IProductionVillageStore
        {
            private readonly ProductionVillageMemoryStore _inner = new ProductionVillageMemoryStore();
            public bool Fail;
            public ProductionVillageSave Load() => _inner.Load();
            public void Save(ProductionVillageSave value) { if (Fail) throw new IOException("test write failure"); _inner.Save(value); }
        }

        [Test]
        public void SaveFailureDoesNotPublishLevelOrChargeAndSuccessfulRetrySurvivesRestart()
        {
            var store = new Store(); var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), Game, true);
            core.Open(1000); Assert.That(core.Place(FacilityKind.Fishery, 0, 0, 1000), Is.True);
            var id = core.View.Nodes[0].Id; var electric = core.View.Electricity;
            store.Fail = true; Assert.That(core.Upgrade(id, 1, 1000), Is.False);
            Assert.That(core.View.Nodes[0].Level, Is.EqualTo(1)); Assert.That(core.View.Electricity, Is.EqualTo(electric));
            store.Fail = false; Assert.That(core.Upgrade(id, 1, 1000), Is.True);
            var again = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), Game, true); again.Open(1000);
            Assert.That(again.View.Nodes[0].Level, Is.EqualTo(2)); Assert.That(again.View.Electricity, Is.EqualTo(electric - 10));
            Assert.That(store.Load().Version, Is.EqualTo(ProductionVillageSave.CurrentVersion));
            Assert.That(again.Upgrade(id, 1, 1000), Is.False);
        }

        [Test]
        public void PreviewPanelDisablesUnavailableUpgradesAndShowsCurrentAndNextEffects()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var root = new VisualElement(); var panel = new VillageUpgradePanel(root, (_, __) => throw new InvalidOperationException("Preview must never submit"));
            var before = JsonUtility.ToJson(game.Export()); panel.Render(Node(game, id), game.CreateView().Electricity);
            Assert.That(root.Q<Button>(VillageUpgradePanel.UpgradeButtonName).enabledSelf, Is.True);
            Assert.That(root.Query<Label>().ToList().Any(l => l.text.Contains("Lv 1 → 2")), Is.True);
            Assert.That(root.Query<Label>().ToList().Any(l => l.text.Contains("×1.25")), Is.True);
            Assert.That(JsonUtility.ToJson(game.Export()), Is.EqualTo(before));
            game.SetResources(0, 0, 0); panel.Render(Node(game, id), 0);
            Assert.That(root.Q<Button>(VillageUpgradePanel.UpgradeButtonName).enabledSelf, Is.False);
            Assert.That(root.Query<Label>().ToList().Any(l => l.text.Contains("あと 10")), Is.True);
        }
    }
}
