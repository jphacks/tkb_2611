using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageComboTests
    {
        private static VillageGame Game()
        { var game = new VillageGame(false); game.SetResources(10000, 10000, 10000); return game; }
        private static VillageGame Pair()
        { var game = Game(); game.Place(FacilityKind.Fishery, 0, 0); game.Place(FacilityKind.Fishery, 2, 0); return game; }

        [Test]
        public void FourDirectionsMatchProductionAndUpdateAfterMovingANeighbor()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 4, 4).Id;
            foreach (var cell in new[] { (4, 6), (6, 4), (4, 2), (2, 4) }) game.Place(FacilityKind.Fishery, cell.Item1, cell.Item2);
            var center = game.CreateView().Nodes.Single(n => n.Id == id);
            Assert.That(center.ComboDirections, Is.EqualTo(VillageComboDirection.Up | VillageComboDirection.Right | VillageComboDirection.Down | VillageComboDirection.Left));
            Assert.That(center.ComboCount, Is.EqualTo(4)); Assert.That(center.ComboMultiplier, Is.EqualTo(2));
            game.Move(2, 12, 12); center = game.CreateView().Nodes.Single(n => n.Id == id);
            Assert.That(center.ComboDirections, Is.EqualTo(VillageComboDirection.Right | VillageComboDirection.Down | VillageComboDirection.Left));
            Assert.That(center.ComboMultiplier, Is.EqualTo(1.75));
        }

        [Test]
        public void BuildPreviewShowsMutualIncreaseWithoutChangingProductionOrSavedLayout()
        {
            var game = Pair(); game.AdvanceTo(100); var before = JsonUtility.ToJson(game.Export()); var view = game.CreateView();
            var preview = VillageComboRules.Preview(view.Nodes, FacilityKind.Fishery, 4, 0);
            Assert.That(preview.Candidate.ComboDirections, Is.EqualTo(VillageComboDirection.Left));
            Assert.That(preview.Candidate.ComboMultiplier, Is.EqualTo(1.25));
            Assert.That(preview.Changes.Count, Is.EqualTo(1));
            Assert.That(preview.Changes[0].Id, Is.EqualTo(2)); Assert.That(preview.Changes[0].Before, Is.EqualTo(1)); Assert.That(preview.Changes[0].After, Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(game.Export()), Is.EqualTo(before));
            Assert.That(view.Nodes[1].ComboCount, Is.EqualTo(1));
            Assert.That(VillageComboRules.Preview(view.Nodes, FacilityKind.Warehouse, 4, 0), Is.Null);
        }

        [Test]
        public void MovePreviewRemovesTheOldPositionAndNeverCountsTheMovingFacilityTwice()
        {
            var game = Pair(); var view = game.CreateView();
            var preview = VillageComboRules.Preview(view.Nodes, FacilityKind.Fishery, 10, 10, 2);
            Assert.That(preview.Candidate.ComboCount, Is.Zero);
            Assert.That(preview.Nodes.Count, Is.EqualTo(2));
            Assert.That(preview.Changes.Single().After, Is.Zero);
            preview = VillageComboRules.Preview(view.Nodes, FacilityKind.Fishery, 2, 0, 2);
            Assert.That(preview.Candidate.ComboCount, Is.EqualTo(1)); Assert.That(preview.Changes, Is.Empty);
        }

        [Test]
        public void OnlyAlignedCardinalNeighborsOfTheSameKindCountMutually()
        {
            var game = Pair(); game.Place(FacilityKind.Fishery, 2, 2); game.Place(FacilityKind.Saltworks, 0, 2);
            Assert.That(game.CreateView().Nodes.Select(n => n.ComboCount), Is.EqualTo(new[] { 1, 2, 1, 0 }));
            Assert.That(game.Move(2, 10, 10).Success, Is.True);
            Assert.That(game.CreateView().Nodes.All(n => n.ComboCount == 0), Is.True);
            game = Game(); game.Place(FacilityKind.Fishery, 0, 0); game.Place(FacilityKind.Fishery, 3, 0);
            Assert.That(game.CreateView().Nodes.All(n => n.ComboCount == 0), Is.True);
            game = Game(); game.Place(FacilityKind.Warehouse, 0, 0); game.Place(FacilityKind.Warehouse, 1, 0);
            Assert.That(game.CreateView().Nodes.All(n => n.ComboCount == 0), Is.True);
        }

        [Test]
        public void FourNeighborsDoubleOutputAndZoomDoesNotChangeIt()
        {
            var game = Game(); var id = game.Place(FacilityKind.Fishery, 4, 4).Id;
            foreach (var cell in new[] { (2, 4), (6, 4), (4, 2), (4, 6) }) game.Place(FacilityKind.Fishery, cell.Item1, cell.Item2);
            foreach (var overview in new[] { true, false, true })
            {
                game.SetGroupViewActive(overview);
                var node = game.CreateView().Nodes.Single(n => n.Id == id);
                Assert.That(node.ComboCount, Is.EqualTo(4)); Assert.That(node.ComboMultiplier, Is.EqualTo(2));
            }
            game.AdvanceTo(4100);
            Assert.That(game.CreateView().Nodes.Single(n => n.Id == id).Stock[Item.Fish], Is.EqualTo(2));
        }

        [Test]
        public void QuarterItemsAccumulateToFiveItemsInFourBatchesWithoutExtraGasOrTime()
        {
            var game = Pair(); game.AdvanceTo(16400); var view = game.CreateView();
            Assert.That(view.Nodes.Select(n => n.Stock[Item.Fish]), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(view.Produced, Is.EqualTo(10)); Assert.That(view.GasSpent, Is.EqualTo(8));
            Assert.That(view.Nodes.All(n => n.ProductionSeconds == 4), Is.True);
            Assert.That(view.ProducedLastMinute, Is.EqualTo(10));
            Assert.That(view.ItemRates[Item.Fish].ActualMakePerMinute, Is.EqualTo(10));
        }

        [Test]
        public void SquareAndEffortMultiplyProductionWhileGasAndInputRatesStayIndependentOfCombo()
        {
            var game = Pair(); game.Place(FacilityKind.Fishery, 0, 2); game.Place(FacilityKind.Fishery, 2, 2);
            game.AdvanceTo(8200);
            var square = game.CreateView();
            Assert.That(square.ItemRates[Item.Fish].ActualMakePerMinute, Is.EqualTo(12));
            Assert.That(square.Nodes.Sum(n => n.Stock[Item.Fish]), Is.EqualTo(9), "上位さばき場が魚3個を材料に使う");
            Assert.That(square.ItemRates[Item.Fish].ActualUsePerMinute, Is.EqualTo(3));
            game = Pair(); game.SetEffort(1, 1); var view = game.CreateView();
            Assert.That(view.ItemRates[Item.Fish].TheoreticalMakePerMinute, Is.EqualTo(15 * 1.25 * 3));
            Assert.That(view.TheoreticalGasPerMinute, Is.EqualTo(15 * 5));
            var other = Game(); other.Place(FacilityKind.Preparation, 0, 0); other.Place(FacilityKind.Preparation, 2, 0);
            Assert.That(other.CreateView().ItemRates[Item.Fish].TheoreticalUsePerMinute, Is.EqualTo(60));
            Assert.That(other.CreateView().ItemRates[Item.Surimi].TheoreticalMakePerMinute, Is.EqualTo(25));
        }

        [Test]
        public void LayoutChangesAndRestartPreservePaidOutputAndFractionalProgress()
        {
            var game = Pair(); game.AdvanceTo(12400);
            Assert.That(game.Export().Nodes.All(n => n.ProcessYield == 2), Is.True);
            var finish = game.Export().Nodes[0].Finish;
            Assert.That(game.Move(2, 10, 10).Success, Is.True);
            var restored = VillageGame.Restore(JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(game.Export())));
            restored.AdvanceTo(finish); game.AdvanceTo(finish);
            Assert.That(restored.CreateView().Totals[Item.Fish], Is.EqualTo(10));
            Assert.That(restored.CreateView().Totals, Is.EquivalentTo(game.CreateView().Totals));
            Assert.That(restored.CreateView().Nodes.All(n => n.ComboCount == 0), Is.True);
            var partial = Pair(); partial.AdvanceTo(100);
            partial = VillageGame.Restore(JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(partial.Export())));
            partial.AdvanceTo(16400); Assert.That(partial.CreateView().Produced, Is.EqualTo(10));
        }

        [Test]
        public void BuildingNeighborDuringPaidBatchOnlyAffectsTheNextBatch()
        {
            var game = Game(); game.Place(FacilityKind.Fishery, 0, 0); game.AdvanceTo(100);
            game.Place(FacilityKind.Fishery, 2, 0);
            Assert.That(game.Export().Nodes[0].ProcessYield, Is.EqualTo(1));
            Assert.That(game.Export().Nodes[0].ComboRemainder, Is.Zero);
            game.AdvanceTo(4100);
            Assert.That(game.CreateView().Nodes[0].Stock[Item.Fish], Is.EqualTo(1));
            game.AdvanceTo(4200); Assert.That(game.Export().Nodes[0].ComboRemainder, Is.EqualTo(1));
        }

        [Test]
        public void FullOutputOrNoGasDoesNotConsumeFractionalProgress()
        {
            var state = Pair().Export(); state.Nodes[0].ComboRemainder = 3; state.Nodes[0].Stock[(int)Item.Fish] = 5;
            var game = VillageGame.Restore(state); game.AdvanceTo(100);
            Assert.That(game.Export().Nodes[0].Finish, Is.Zero); Assert.That(game.Export().Nodes[0].ComboRemainder, Is.EqualTo(3));
            state = Pair().Export(); state.Nodes[0].ComboRemainder = 3; state.Gas = 0;
            game = VillageGame.Restore(state); game.AdvanceTo(100);
            Assert.That(game.Export().Nodes[0].Finish, Is.Zero); Assert.That(game.Export().Nodes[0].ComboRemainder, Is.EqualTo(3));
        }

        [Test]
        public void BoostedPaidOutputReservesCapacityForInjectionAndIncomingCargo()
        {
            var state = Pair().Export(); state.Nodes[0].ComboRemainder = 3;
            state.Nodes[0].Stock[(int)Item.Fish] = 4; state.Nodes[0].Stock[(int)Item.Salt] = 6;
            var game = VillageGame.Restore(state); game.AdvanceTo(100);
            Assert.That(game.Export().Nodes[0].ProcessYield, Is.EqualTo(2));
            Assert.That(game.Inject(1, Item.Kelp, 1).Success, Is.False);
            game = VillageGame.Restore(game.Export()); game.AdvanceTo(4100);
            Assert.That(game.CreateView().Nodes[0].Total, Is.EqualTo(12));
            Assert.That(game.CreateView().Nodes[0].Stock[Item.Fish], Is.EqualTo(6));
        }

        [Test]
        public void PricesIncreasePerKindAndFailedBuildMoveAndRemovalDoNotResetCounts()
        {
            var game = Game(); var first = game.NextBuildElectricityCost(FacilityKind.Fishery);
            var id = game.Place(FacilityKind.Fishery, 0, 0).Id;
            Assert.That(game.NextBuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(first + 1));
            Assert.That(game.NextBuildElectricityCost(FacilityKind.Warehouse), Is.EqualTo(IslandCatalog.Get(FacilityKind.Warehouse).ElectricityCost));
            Assert.That(game.Place(FacilityKind.Fishery, 0, 0).Success, Is.False);
            game.SetResources(0, first, 0); Assert.That(game.Place(FacilityKind.Fishery, 2, 0).Success, Is.False);
            Assert.That(game.CreateView().Electricity, Is.EqualTo(first));
            Assert.That(game.Move(id, 4, 0).Success, Is.True); Assert.That(game.Remove(id).Success, Is.True);
            game = VillageGame.Restore(game.Export());
            Assert.That(game.CreateView().BuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(first + 1));
            game.SetResources(0, first + 1, 0); Assert.That(game.Place(FacilityKind.Fishery, 0, 0).Success, Is.True);
            Assert.That(game.CreateView().Electricity, Is.Zero);
            Assert.That(game.NextBuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(first + 2));
        }

        [Test]
        public void LegacySaveSeedsCountsFromExistingFacilitiesAndKeepsPaidBaseline()
        {
            var state = Pair().Export(); state.ComboSchema = 0; state.BuildCounts = null;
            foreach (var node in state.Nodes) { node.Started = 0; node.Finish = 4000; }
            var game = VillageGame.Restore(state);
            Assert.That(game.NextBuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(IslandCatalog.Get(FacilityKind.Fishery).ElectricityCost + 2));
            game.AdvanceTo(4000); Assert.That(game.CreateView().Produced, Is.EqualTo(2));
            game = VillageGame.Restore(game.Export()); Assert.That(game.Export().ComboSchema, Is.EqualTo(1));
        }

        [Test]
        public void InvalidNewSaveFieldsAreRejectedAndExportedCountsAreDetached()
        {
            var game = Pair(); var state = game.Export(); state.BuildCounts[0] = 0;
            Assert.That(game.Export().BuildCounts[0], Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state = game.Export(); state.BuildCounts = null; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state = game.Export(); state.ComboSchema = 2; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state = game.Export(); state.Nodes[0].ComboRemainder = 4; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state = game.Export(); state.Nodes[0].ProcessYield = 2; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            game.AdvanceTo(100); state = game.Export(); state.Nodes[0].ProcessYield = 3;
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
        }

        [Test]
        public void LongAdvanceMatchesSplitAndRestartWithFractionalOutput()
        {
            var whole = Pair(); var split = Pair(); whole.AdvanceTo(30000);
            for (var time = 137; time < 30000; time += 137) split.AdvanceTo(time);
            split = VillageGame.Restore(split.Export()); split.AdvanceTo(30000);
            Assert.That(split.CreateView().Totals, Is.EquivalentTo(whole.CreateView().Totals));
            Assert.That(split.CreateView().GasSpent, Is.EqualTo(whole.CreateView().GasSpent));
            Assert.That(split.Export().Nodes.Select(n => n.ComboRemainder), Is.EqualTo(whole.Export().Nodes.Select(n => n.ComboRemainder)));
        }

        private sealed class Store : IProductionVillageStore
        {
            internal bool Fail;
            private readonly ProductionVillageMemoryStore _inner = new ProductionVillageMemoryStore();
            public ProductionVillageSave Load() => _inner.Load();
            public void Save(ProductionVillageSave save) { if (Fail) throw new IOException(); _inner.Save(save); }
        }

        [Test]
        public void SaveFailureDoesNotSpendOrIncrementAndSuccessfulBuildSurvivesReopen()
        {
            var store = new Store(); var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), Game, true); core.Open(1000);
            var price = core.View.BuildElectricityCost(FacilityKind.Fishery); var electricity = core.View.Electricity;
            store.Fail = true; Assert.That(core.Place(FacilityKind.Fishery, 0, 0, 1000), Is.False);
            Assert.That(core.View.Nodes, Is.Empty); Assert.That(core.View.Electricity, Is.EqualTo(electricity));
            Assert.That(core.View.BuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(price));
            store.Fail = false; Assert.That(core.Place(FacilityKind.Fishery, 0, 0, 1000), Is.True);
            core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), Game, true); core.Open(1000);
            Assert.That(core.View.BuildElectricityCost(FacilityKind.Fishery), Is.EqualTo(price + 1));
            Assert.That(core.View.Electricity, Is.EqualTo(electricity - price));
        }
    }
}
