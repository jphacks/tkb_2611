using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageEffortTests
    {
        private static VillageGame Game()
        { var g = new VillageGame(false); g.SetResources(10000, 10000, 10000); return g; }
        private static VillageNodeView Node(VillageGame g, int id) => g.CreateView().Nodes.Single(n => n.Id == id);

        [Test]
        public void IntensityIsReversibleIdempotentAndFreeEvenWithoutEnergy()
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            g.SetResources(0, 0, 0);
            foreach (var effort in new[] { 3, 3, 1, 0 })
            {
                Assert.That(g.SetEffort(id, effort).Success, Is.True);
                Assert.That(Node(g, id).Effort, Is.EqualTo(effort));
                Assert.That(Node(g, id).Capacity, Is.EqualTo(12));
                Assert.That(g.CreateView().Gas + g.CreateView().Electricity + g.CreateView().Water, Is.Zero);
            }
        }

        [TestCase(-1)] [TestCase(4)] [TestCase(int.MaxValue)]
        public void InvalidIntensityDoesNotMutateState(int effort)
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            Assert.That(g.SetEffort(id, effort).Success, Is.False);
            Assert.That(g.SetEffort(999, 1).Success, Is.False);
            Assert.That(Node(g, id).Effort, Is.Zero);
        }

        [Test]
        public void EveryProductionFacilitySupportsEffortAndInfrastructureDoesNot()
        {
            foreach (var def in IslandCatalog.Facilities)
            {
                var g = Game(); var id = g.Place(def.Kind, 0, 0).Id;
                Assert.That(g.SetEffort(id, 1).Success, Is.EqualTo(def.Recipes.Count > 0), def.Name);
            }
        }

        [Test]
        public void PaidBatchAndCargoKeepDeadlinesAndNextBatchUsesNewGasAndSpeed()
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            g.Place(FacilityKind.Warehouse, 3, 0); g.AdvanceTo(4200);
            var before = g.Export(); Assert.That(before.Cargo.Count, Is.GreaterThan(0));
            Assert.That(g.SetEffort(id, 3).Success, Is.True);
            var after = g.Export();
            Assert.That(after.Nodes.Select(n => n.Finish), Is.EqualTo(before.Nodes.Select(n => n.Finish)));
            Assert.That(after.Cargo.Select(c => c.End), Is.EqualTo(before.Cargo.Select(c => c.End)));
            Assert.That(after.Gas, Is.EqualTo(before.Gas));
            Assert.That(Node(g, id).ProductionSeconds, Is.EqualTo(1));
            g.SetResources(0, 0, 0);
            g.AdvanceTo(before.Nodes.Single(n => n.Id == id).Finish);
            Assert.That(g.CreateView().Produced, Is.EqualTo(before.Produced + 1));
        }

        [Test]
        public void GasIsChargedAtStartAndShortageCanBeResolvedByLoweringIntensity()
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            g.SetResources(3, 0, 0); g.SetEffort(id, 3); g.AdvanceTo(100);
            Assert.That(g.CreateView().Gas, Is.EqualTo(3));
            Assert.That(Node(g, id).Status, Is.EqualTo("ガスを待っています"));
            g.SetEffort(id, 1); g.AdvanceTo(200);
            Assert.That(g.CreateView().Gas, Is.EqualTo(1));
            Assert.That(g.Export().Nodes.Single().Finish, Is.EqualTo(2200));
            g.SetEffort(id, 0); g.AdvanceTo(2300);
            Assert.That(g.CreateView().Produced, Is.EqualTo(1));
            Assert.That(g.CreateView().Gas, Is.Zero);
            Assert.That(g.Export().Nodes.Single().Finish, Is.EqualTo(6300));
        }

        [Test]
        public void ReportReflectsBothSpeedAndPerCycleGasAndReturnsToBaseline()
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            var baseline = g.CreateView(); g.SetEffort(id, 3); var high = g.CreateView();
            Assert.That(high.ItemRates[Item.Fish].TheoreticalMakePerMinute, Is.EqualTo(baseline.ItemRates[Item.Fish].TheoreticalMakePerMinute * 4));
            Assert.That(high.TheoreticalGasPerMinute, Is.EqualTo(baseline.TheoreticalGasPerMinute * 16));
            g.AdvanceTo(100); Assert.That(g.CreateView().GasSpent, Is.EqualTo(4));
            g.SetEffort(id, 0); Assert.That(g.CreateView().TheoreticalGasPerMinute, Is.EqualTo(baseline.TheoreticalGasPerMinute));
        }

        [Test]
        public void EffortAndPaidWorkSurviveJsonRestart()
        {
            var g = Game(); var id = g.Place(FacilityKind.Fishery, 0, 0).Id;
            g.SetEffort(id, 3); g.AdvanceTo(100); g.SetEffort(id, 0);
            var state = JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(g.Export()));
            var restored = VillageGame.Restore(state);
            Assert.That(Node(restored, id).Effort, Is.Zero);
            restored.AdvanceTo(1100); g.AdvanceTo(1100);
            Assert.That(restored.CreateView().Gas, Is.EqualTo(g.CreateView().Gas));
            Assert.That(restored.CreateView().Totals, Is.EquivalentTo(g.CreateView().Totals));
        }

        [Test]
        public void LegacyLevelsMigrateOnceAndPreserveStockBeyondCurrentCapacity()
        {
            var g = Game(); g.Place(FacilityKind.Fishery, 0, 0); g.Place(FacilityKind.Warehouse, 3, 0);
            var legacy = g.Export(); legacy.EffortSchema = 0;
            legacy.Nodes[0].Level = 5; legacy.Nodes[1].Level = 5; legacy.Nodes[1].Stock[(int)Item.Fish] = 160;
            var restored = VillageGame.Restore(legacy);
            Assert.That(restored.CreateView().Nodes[0].Effort, Is.EqualTo(3));
            Assert.That(restored.CreateView().Nodes[1].Effort, Is.Zero);
            Assert.That(restored.CreateView().Nodes[1].Total, Is.EqualTo(160));
            restored.SetEffort(legacy.Nodes[0].Id, 0);
            restored = VillageGame.Restore(restored.Export());
            Assert.That(restored.CreateView().Nodes[0].Effort, Is.Zero);
            Assert.That(restored.CreateView().Nodes[1].Total, Is.EqualTo(160));
            Assert.That(restored.Inject(legacy.Nodes[1].Id, Item.Fish, 1).Success, Is.False);
        }

        [Test]
        public void InvalidSavedEffortAndFutureSchemaAreRejected()
        {
            var g = Game(); g.Place(FacilityKind.Fishery, 0, 0); var s = g.Export();
            foreach (var bad in new[] { -1, 4 }) { s.Nodes[0].Effort = bad; Assert.Throws<ArgumentException>(() => VillageGame.Restore(s)); }
            s.Nodes[0].Effort = 0; s.EffortSchema = 2; Assert.Throws<ArgumentException>(() => VillageGame.Restore(s));
            s.EffortSchema = 0; s.Nodes[0].Level = 6; Assert.Throws<ArgumentException>(() => VillageGame.Restore(s));
        }

        [Test]
        public void SaveKeepsEffortWarehouseAndEncountersWithFreeResourcesEnabled()
        {
            var game = Game(); var fish = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var warehouse = game.Place(FacilityKind.Warehouse, 3, 0).Id;
            game.SetEffort(fish, 3); game.SetWarehouseCapacity(warehouse, 80); game.SetFreeResources(true);
            Assert.That(game.BeginEncounterDay(297).Success, Is.True);
            Assert.That(game.ObservePeer(EncounterToken.Create(), 1000).Success, Is.True);
            var gas = game.CreateView().Gas; game.AdvanceTo(100);
            var restored = VillageGame.Restore(JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(game.Export())));
            Assert.That(restored.CreateView().FreeResources, Is.True);
            Assert.That(Node(restored, fish).Effort, Is.EqualTo(3));
            Assert.That(Node(restored, warehouse).Capacity, Is.EqualTo(80));
            Assert.That(restored.EncounterDay, Is.EqualTo(297));
            Assert.That(restored.EncounterSelfToken, Is.EqualTo(game.EncounterSelfToken));
            Assert.That(restored.EncountersSeen, Is.EqualTo(1));
            Assert.That(restored.TakeEncounterReport(1000, out var report).Success, Is.True);
            Assert.That(report, Is.Not.Null);
            restored.AdvanceTo(1100);
            Assert.That(restored.CreateView().Gas, Is.EqualTo(gas));
            Assert.That(restored.CreateView().Produced, Is.EqualTo(1));
        }

        private sealed class FailingStore : IProductionVillageStore
        {
            internal bool Fail;
            private readonly ProductionVillageMemoryStore _inner = new ProductionVillageMemoryStore();
            public ProductionVillageSave Load() => _inner.Load();
            public void Save(ProductionVillageSave save) { if (Fail) throw new IOException(); _inner.Save(save); }
        }
        [Test]
        public void FailedSaveKeepsIntensityAndResourcesThenSuccessfulSaveSurvivesReopen()
        {
            var store = new FailingStore();
            Func<VillageGame> initial = () => { var g = Game(); g.Place(FacilityKind.Fishery, 0, 0); return g; };
            var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), initial, true); core.Open(1000);
            var id = core.View.Nodes.Single().Id; var gas = core.View.Gas;
            store.Fail = true; Assert.That(core.SetEffort(id, 3, 1000), Is.False);
            Assert.That(core.View.Nodes.Single().Effort, Is.Zero); Assert.That(core.View.Gas, Is.EqualTo(gas));
            store.Fail = false; Assert.That(core.SetEffort(id, 3, 1000), Is.True);
            var again = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), initial, true); again.Open(1000);
            Assert.That(again.View.Nodes.Single().Effort, Is.EqualTo(3)); Assert.That(again.View.Gas, Is.EqualTo(gas));
            Assert.That(again.SetEffort(id, 0, 1000), Is.True);
        }
    }
}
