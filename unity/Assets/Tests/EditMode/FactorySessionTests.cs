using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Factory;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class FactorySessionTests
    {
        private FactoryCatalog _catalog;
        private FactoryRules _rules;
        private FactoryState _saved;
        private const int Day = 297;
        private const string Self = "AQEBAQEBAQEBAQEBAQEBAQ";
        private const string Peer = "AgICAgICAgICAgICAgICAg";

        [SetUp]
        public void SetUp()
        {
            _catalog = FactoryStore.LoadCatalog(); _rules = FactoryStore.LoadRules();
            _rules.startingGas = 1000; _rules.startingElectric = 1000; _rules.startingWater = 1000;
            _saved = null;
        }

        private FactorySession Session(FactoryState state = null) => new FactorySession(_catalog, _rules, s => _saved = s, state);
        private static void Succeeds(FactoryResult r) => Assert.That(r.Success, Is.True, r.Message);
        private static long Stock(FactoryState s, string resource) => s.facilities.Sum(f => FactoryState.Amount(f.stock, resource));
        private static long Transit(FactoryState s, string resource) => s.cargo.Where(c => c.resource == resource).Sum(c => c.amount);

        [Test]
        public void CatalogCoversAllRecipesAndStageWeights()
        {
            Assert.That(_catalog.recipes.Length, Is.EqualTo(10));
            Assert.That(_catalog.penguins.Length, Is.EqualTo(19));
            Assert.That(Enumerable.Range(1, 4).Select(stage => _catalog.penguins.Where(p => p.stage <= stage).Sum(p => p.weight)),
                Is.EqualTo(new[] { 6000, 8600, 9900, 10000 }));
            Assert.That(_catalog.Recipe("oden").inputs.Select(i => i.amount), Is.All.EqualTo(10000));
        }

        [Test]
        public void ReceiptsAndStepMaximumSurviveJsonRestartAndDoNotDoubleGrant()
        {
            var f = Session();
            Succeeds(f.AcceptSteps(Day, 2500, Day));
            Succeeds(f.AcceptScreen("screen", Day - 1, 240, Day, true));
            Succeeds(f.AcceptWaterBarcode("4901234567894", Day, Day, true));
            var before = JsonUtility.ToJson(f.Snapshot);
            var again = Session(JsonUtility.FromJson<FactoryState>(before));
            Assert.That(again.AcceptSteps(Day, 2500, Day).Amount, Is.Zero);
            Assert.That(again.AcceptScreen("another-id", Day - 1, 240, Day, true).Success, Is.False);
            Assert.That(again.AcceptWaterBarcode("4901234567894", Day, Day, true).Success, Is.False);
            Assert.That(JsonUtility.ToJson(again.Snapshot), Is.EqualTo(before));
            Assert.That(again.AcceptSteps(Day - 1, 10000, Day - 1).Success, Is.False);
            Assert.That(again.AcceptSteps(Day + 1, 500, Day + 1).Amount, Is.EqualTo(1));
        }

        [Test]
        public void FailedSaveDoesNotPublishAnyPartOfTheTransaction()
        {
            var fail = false;
            var f = new FactorySession(_catalog, _rules, s => { if (fail) throw new IOException(); _saved = s; });
            Succeeds(f.AcceptSteps(Day, 500, Day));
            var before = JsonUtility.ToJson(f.Snapshot);
            fail = true;
            Assert.That(f.AcceptSteps(Day, 1000, Day).Success, Is.False);
            Assert.That(f.Build("fishery", 0, 0).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
            fail = false;
            Assert.That(f.AcceptSteps(Day, 1000, Day).Amount, Is.EqualTo(1));
        }

        [Test]
        public void ProductionContinuesBeyondFourItemsAndWarehouseRoutingConservesGoods()
        {
            var f = Session();
            Succeeds(f.Build("fishery", 0, 0));
            Succeeds(f.Build("warehouse", 1, 0));
            Succeeds(f.AdvanceTo(61000));
            var s = f.Snapshot;
            Assert.That(Stock(s, "fish") + Transit(s, "fish"), Is.EqualTo(600));
            Assert.That(s.gas, Is.EqualTo(993), "Seven starts are paid, including the current batch");
            Assert.That(s.facilities.Single(n => n.recipe == "warehouse").stock.Single(a => a.id == "fish").amount, Is.GreaterThan(4));
        }

        [Test]
        public void AdjacencyIsAutomaticAndDisconnectedStockNeverTeleports()
        {
            var f = Session(); Succeeds(f.Expand(1, 0));
            Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.Build("warehouse", 3, 0));
            Succeeds(f.AdvanceTo(21000));
            Assert.That(f.Snapshot.cargo, Is.Empty);
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(200));
            Succeeds(f.Build("warehouse", 1, 0)); Succeeds(f.Build("warehouse", 2, 0));
            Succeeds(f.AdvanceTo(31000));
            Assert.That(f.Snapshot.cargo.Count + f.Snapshot.facilities.Where(n => n.recipe == "warehouse").Sum(n => n.stock.Count), Is.GreaterThan(0));
        }

        [Test]
        public void PaidBatchFinishesWithoutFuelAndWaitsWithoutDestroyingStock()
        {
            _rules.startingGas = 1;
            var f = Session(); Succeeds(f.Build("fishery", 0, 0));
            Succeeds(f.AdvanceTo(11000));
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(100));
            Assert.That(f.Snapshot.gas, Is.Zero);
            Assert.That(f.StopReason(f.Snapshot, f.Snapshot.facilities[0]), Is.EqualTo("ガス不足"));
            Succeeds(f.AdvanceTo(31000));
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(100));
        }

        [Test]
        public void TimePartitionAndRestartPreserveInventoryReservationsAndProgress()
        {
            var whole = Session(); Succeeds(whole.Build("fishery", 0, 0)); Succeeds(whole.Build("warehouse", 1, 0));
            var split = Session(whole.Snapshot);
            Succeeds(whole.AdvanceTo(45000));
            foreach (var at in new long[] { 1234, 11000, 11250, 22111, 30000 }) Succeeds(split.AdvanceTo(at));
            split = Session(JsonUtility.FromJson<FactoryState>(JsonUtility.ToJson(split.Snapshot)));
            Succeeds(split.AdvanceTo(45000));
            Assert.That(JsonUtility.ToJson(split.Snapshot), Is.EqualTo(JsonUtility.ToJson(whole.Snapshot)));
        }

        [Test]
        public void DeliveryConsumesStockAndScoresExactlyOnceWithNoWeeklyReward()
        {
            var f = Session(); Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.AdvanceTo(21000));
            Succeeds(f.Deliver("a", "fish", 150));
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(50)); Assert.That(f.Snapshot.points, Is.EqualTo(150));
            var before = JsonUtility.ToJson(f.Snapshot);
            Assert.That(f.Deliver("a", "fish", 1).Success, Is.False);
            Assert.That(f.Deliver("b", "fish", -1).Success, Is.False);
            Assert.That(f.Deliver("c", "gas", 1).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void TwoByTwoExpansionBuildCostsAndComboEffortAreIndependentFromZoom()
        {
            var f = Session();
            Assert.That(f.Snapshot.blocks.Count, Is.EqualTo(1)); Succeeds(f.Expand(1, 0));
            Assert.That(f.Snapshot.blocks.Count, Is.EqualTo(2));
            Assert.That(f.Expand(3, 0).Success, Is.False);
            Assert.That(f.NextBuildCost, Is.EqualTo(2));
            var a = f.Build("fishery", 0, 0); Succeeds(a);
            Assert.That(f.NextBuildCost, Is.EqualTo(3)); Succeeds(f.Build("fishery", 1, 0));
            Succeeds(f.SetEffort(a.FacilityId, 1)); Succeeds(f.AdvanceTo(11000));
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(375), "250 effort+combo and 125 combo-only");
        }

        [Test]
        public void PenguinFormulaUsesAdditiveCountsAndIndependentResourceCarries()
        {
            var state = Session().Snapshot; state.stage = 4;
            state.penguins.Add(new FactoryAmount("western_rockhopper", 3));
            state.penguins.Add(new FactoryAmount("emperor", 2));
            state.penguins.Add(new FactoryAmount("adelie", 3)); state.penguins.Add(new FactoryAmount("gentoo", 2));
            var f = Session(state);
            Assert.That(f.AcceptSteps(Day, 50000, Day).Amount, Is.EqualTo(143), "100 x 1.30 x 1.10");
            var a = f.Build("fishery", 0, 0); Succeeds(a); Succeeds(f.AdvanceTo(11000));
            Assert.That(Stock(f.Snapshot, "fish"), Is.EqualTo(3000 * 1.92));
            Assert.That(f.AcceptWaterBarcode("4901234567894", Day, Day, true).Amount, Is.EqualTo(3));
        }

        [Test]
        public void ConfirmedPairAndBirdCommitTogetherAndSurviveDayChanges()
        {
            var fail = false;
            var f = new FactorySession(_catalog, _rules, _ => { if (fail) throw new IOException(); });
            Succeeds(f.BeginEncounterDay(Day, Self)); Succeeds(f.ObservePeer(Peer, 0));
            var before = JsonUtility.ToJson(f.Snapshot); fail = true;
            var pair = EncounterToken.PairId(Self, Peer);
            Assert.That(f.ConfirmEncounter(Day, Peer, pair, true, 0).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before)); fail = false;
            Succeeds(f.ConfirmEncounter(Day, Peer, pair, true, 0));
            var restored = Session(JsonUtility.FromJson<FactoryState>(JsonUtility.ToJson(f.Snapshot)));
            Assert.That(restored.ConfirmEncounter(Day, Peer, pair, true, 0).Success, Is.False);
            Assert.That(restored.Snapshot.penguins.Sum(p => p.amount), Is.EqualTo(1));
            Succeeds(restored.BeginEncounterDay(Day + 1, Peer));
            Assert.That(restored.Snapshot.penguins.Sum(p => p.amount), Is.EqualTo(1));
            Assert.That(restored.Snapshot.dailyBirds, Is.Zero);
        }

        [Test]
        public void PityGuaranteesAnUnlockedUnownedSpeciesAndSkipsIneligibleResults()
        {
            var state = Session().Snapshot;
            foreach (var bird in _catalog.penguins.Where(p => p.stage == 1 && p.id != "king")) state.penguins.Add(new FactoryAmount(bird.id, 1));
            state.pityMisses = 20;
            var f = Session(state); Succeeds(f.BeginEncounterDay(Day, Self)); Succeeds(f.ObservePeer(Peer, 0));
            Succeeds(f.ConfirmEncounter(Day, Peer, EncounterToken.PairId(Self, Peer), false, 0));
            Assert.That(f.Snapshot.pityMisses, Is.EqualTo(20));
            Succeeds(f.ConfirmEncounter(Day, Peer, EncounterToken.PairId(Self, Peer), true, 0));
            Assert.That(FactoryState.Amount(f.Snapshot.penguins, "king"), Is.EqualTo(1));
            Assert.That(f.Snapshot.pityMisses, Is.Zero);
            Assert.That(f.SpeciesProbability("emperor"), Is.Zero);
        }

        [Test]
        public void CorruptSaveRestoresTheWholePreviousTransaction()
        {
            var dir = Path.Combine(Path.GetTempPath(), "factory-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FactoryStore(dir, _catalog, _rules);
                var f = store.Open(out _); Succeeds(f.AcceptSteps(Day, 500, Day)); Succeeds(f.AcceptSteps(Day, 1000, Day));
                File.WriteAllText(Path.Combine(dir, "factory-v1.json"), "broken");
                var restored = store.Open(out var status);
                Assert.That(status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
                Assert.That(restored.Snapshot.maxSteps, Is.EqualTo(500));
                Assert.That(restored.AcceptSteps(Day, 1000, Day).Amount, Is.EqualTo(1));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void NewerSaveCannotBeReplacedByItsOlderBackup()
        {
            var dir = Path.Combine(Path.GetTempPath(), "factory-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                var s = new SaveStore(dir, "f.json", 1);
                s.Save("{}"); s.Save("{}");
                File.WriteAllText(s.MainPath, SaveStore.Encode("{}", 2));
                Assert.That(s.Load().Status, Is.EqualTo(SaveLoadStatus.TooNew));
                Assert.Throws<InvalidOperationException>(() => s.Save("{}"));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void LargeOutputUsesLongQuantitiesAndFitsAtLeastOneBoostedBatch()
        {
            var state = Session().Snapshot; state.stage = 4;
            state.penguins.Add(new FactoryAmount("adelie", 1000000));
            state.penguins.Add(new FactoryAmount("gentoo", 1000000));
            var f = Session(state); Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.AdvanceTo(11000));
            var output = Stock(f.Snapshot, "fish");
            Assert.That(output, Is.GreaterThan(int.MaxValue));
            Assert.That(f.Capacity(f.Snapshot, f.Snapshot.facilities[0], "fish"), Is.GreaterThanOrEqualTo(output));
            Succeeds(f.Deliver("large", "fish", output));
            Assert.That(f.Snapshot.points, Is.EqualTo(output));
        }

        [Test]
        public void OverflowInPenguinBonusRollsBackReceiptAndCarry()
        {
            var state = Session().Snapshot; state.penguins.Add(new FactoryAmount("western_rockhopper", long.MaxValue));
            state.penguins.Add(new FactoryAmount("emperor", long.MaxValue));
            var f = Session(state); var before = JsonUtility.ToJson(f.Snapshot);
            Assert.That(f.AcceptSteps(Day, 500, Day).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void FractionalCarryPersistsAcrossRestartAndIsNotReappliedByDelivery()
        {
            var state = Session().Snapshot; state.penguins.Add(new FactoryAmount("western_rockhopper", 1));
            var f = Session(state);
            Assert.That(f.AcceptSteps(Day, 2500, Day).Amount, Is.EqualTo(5));
            f = Session(JsonUtility.FromJson<FactoryState>(JsonUtility.ToJson(f.Snapshot)));
            Assert.That(f.AcceptSteps(Day, 5000, Day).Amount, Is.EqualTo(6));
            Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.Build("warehouse", 1, 0)); Succeeds(f.AdvanceTo(21000));
            Succeeds(f.Deliver("goods", "fish", 100));
            Assert.That(Stock(f.Snapshot, "fish") + Transit(f.Snapshot, "fish"), Is.EqualTo(100));
        }

        [Test]
        public void DeliveryStageRaisesExistingAndFutureLevelsWithoutGrantingBirds()
        {
            var f = Session(); var a = f.Build("fishery", 0, 0); Succeeds(a); Succeeds(f.AdvanceTo(101000));
            Succeeds(f.Deliver("stage", "fish", 1000));
            Assert.That(f.Snapshot.stage, Is.EqualTo(2)); Assert.That(f.Snapshot.facilities[0].level, Is.EqualTo(2));
            Assert.That(f.Snapshot.penguins, Is.Empty);
            Succeeds(f.Build("preparation", 1, 0));
            Assert.That(f.Snapshot.facilities.Single(n => n.recipe == "preparation").level, Is.EqualTo(2));
            Assert.That(f.SpeciesProbability("emperor"), Is.EqualTo(200d / 8600).Within(1e-12));
        }

        [Test]
        public void ProcessorConsumesInputsWithoutAlsoScoringThemAndRelaysForeignGoods()
        {
            var state = Session().Snapshot; state.stage = 2;
            var f = Session(state); Succeeds(f.Expand(1, 0));
            Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.Build("preparation", 1, 0));
            Succeeds(f.Build("warehouse", 2, 0));
            Succeeds(f.AdvanceTo(61000));
            var s = f.Snapshot;
            Assert.That(Stock(s, "surimi") + Transit(s, "surimi"), Is.GreaterThan(0));
            Assert.That(s.points, Is.Zero);
            foreach (var c in s.cargo) Assert.That(c.path.Distinct().Count(), Is.EqualTo(c.path.Count));
        }

        private FactorySession GiftBlock()
        {
            var state = Session().Snapshot; state.stage = 3;
            var f = Session(state);
            Succeeds(f.Expand(1, 0));
            Succeeds(f.Build("fishery", 0, 0)); Succeeds(f.Build("saltworks", 1, 0));
            Succeeds(f.Build("drying", 0, 1)); Succeeds(f.Build("warehouse", 1, 1));
            return f;
        }

        [Test]
        public void TwoByTwoMatchesKindsInAnyOrderAndDoesNotConsumeTheInternalFacilities()
        {
            var f = GiftBlock();
            Assert.That(f.Snapshot.facilities.Count(n => !n.upper), Is.EqualTo(4));
            Assert.That(f.Snapshot.facilities.Count(n => n.upper && n.recipe == "gift"), Is.EqualTo(1));
            Succeeds(f.AdvanceTo(61000));
            Assert.That(f.Snapshot.facilities.Where(n => !n.upper && n.recipe != "warehouse").Any(n => n.processEnd > 0 || n.stock.Any(a => a.amount > 0)), Is.True);
            var before = JsonUtility.ToJson(f.Snapshot);
            for (var i = 0; i < 20; i++) { var displayed = f.Snapshot; Assert.That(displayed.facilities.Count, Is.EqualTo(5)); }
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void BreakingAFormationKeepsPaidUpperBatchAndReformingReusesItsIdentity()
        {
            var original = GiftBlock().Snapshot;
            var upper = original.facilities.Single(n => n.upper);
            upper.processEnd = 30000; upper.processOutput = "gift"; upper.processYield = 1000;
            var f = Session(original);
            var member = original.facilities.First(n => !n.upper && n.recipe == "fishery");
            Succeeds(f.Move(member.id, 2, 0));
            Assert.That(f.Snapshot.facilities.Single(n => n.upper).retired, Is.True);
            Succeeds(f.AdvanceTo(30000));
            Assert.That(Stock(f.Snapshot, "gift") + Transit(f.Snapshot, "gift"), Is.EqualTo(1000));
            // Stop clocks and drain cargo before moving a node involved in a path.
            foreach (var n in f.Snapshot.facilities.Where(n => !n.retired)) Succeeds(f.SetEnabled(n.id, false));
            Succeeds(f.AdvanceTo(60000));
            var move = f.Move(member.id, 0, 0);
            if (!move.Success) { Succeeds(f.AdvanceTo(90000)); move = f.Move(member.id, 0, 0); }
            Succeeds(move);
            Assert.That(f.Snapshot.facilities.Single(n => n.upper).id, Is.EqualTo(upper.id));
            Assert.That(f.Snapshot.facilities.Single(n => n.upper).retired, Is.False);
        }

        [Test]
        public void FourWarehousesIncreaseInternalCapacityWithoutDuplicatingGoods()
        {
            var f = Session();
            foreach (var p in new[] { (0, 0), (1, 0), (0, 1), (1, 1) }) Succeeds(f.Build("warehouse", p.Item1, p.Item2));
            var s = f.Snapshot;
            Assert.That(s.facilities.Count(n => n.upper), Is.EqualTo(1));
            Assert.That(f.Capacity(s, s.facilities.First(n => !n.upper), "fish"), Is.EqualTo(6000));
            Assert.That(Stock(s, "fish"), Is.Zero);
        }

        [Test]
        public void IncomingReservationsPreventOverDeliveryAndMovementUntilArrival()
        {
            var f = Session(); Succeeds(f.Build("fishery", 0, 0)); var w = f.Build("warehouse", 1, 0); Succeeds(w);
            Succeeds(f.AdvanceTo(11000));
            var before = JsonUtility.ToJson(f.Snapshot);
            Assert.That(f.Move(w.FacilityId, 0, 1).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
            Succeeds(f.AdvanceTo(12000));
            Assert.That(Stock(f.Snapshot, "fish") + Transit(f.Snapshot, "fish"), Is.EqualTo(100));
            var state = f.Snapshot;
            foreach (var n in state.facilities)
                Assert.That(FactoryState.Amount(n.stock, "fish") + state.cargo.Where(c => c.to == n.id && c.resource == "fish").Sum(c => c.amount),
                    Is.LessThanOrEqualTo(f.Capacity(state, n, "fish")));
        }

        [Test]
        public void PairedRewardsAreCappedPerGameDayAndDoNotAdvancePityAtTheCap()
        {
            var f = Session(); Succeeds(f.BeginEncounterDay(Day, Self));
            for (var i = 0; i < 11; i++)
            {
                var peer = EncounterToken.FromBytes(Enumerable.Repeat((byte)(i + 2), 16).ToArray());
                Succeeds(f.ObservePeer(peer, 0));
                var before = f.Snapshot.pityMisses;
                Succeeds(f.ConfirmEncounter(Day, peer, EncounterToken.PairId(Self, peer), true, 0));
                if (i == 10) Assert.That(f.Snapshot.pityMisses, Is.EqualTo(before));
            }
            Assert.That(f.Snapshot.penguins.Sum(a => a.amount), Is.EqualTo(10));
            var pity = f.Snapshot.pityMisses; Succeeds(f.BeginEncounterDay(Day + 1, Peer));
            Assert.That(f.Snapshot.pityMisses, Is.EqualTo(pity));
        }

        [Test]
        public void MalformedSnapshotAndNegativeOrFutureInputsAreRejected()
        {
            var state = Session().Snapshot; state.gas = -1;
            Assert.Throws<ArgumentException>(() => Session(state));
            var f = Session(); var before = JsonUtility.ToJson(f.Snapshot);
            Assert.That(f.AcceptScreen("s", Day, 240, Day, true).Success, Is.False);
            Assert.That(f.AcceptScreen("s", Day - 1, 1441, Day, true).Success, Is.False);
            Assert.That(f.AcceptWaterBarcode("4901234567890", Day, Day, true).Success, Is.False);
            Assert.That(f.AcceptSteps(Day, -1, Day).Success, Is.False);
            Assert.That(f.AcceptSteps(Day, 172801, Day).Success, Is.False);
            Assert.That(f.AdvanceTo(120001).Success, Is.False);
            Assert.That(JsonUtility.ToJson(f.Snapshot), Is.EqualTo(before));
        }

        [Test]
        public void CompetingReceiversBothMakeProgressWithoutManualRouting()
        {
            var state = Session().Snapshot; state.stage = 2;
            var f = Session(state); Succeeds(f.Build("fishery", 0, 0));
            var a = f.Build("preparation", 1, 0); var b = f.Build("preparation", 0, 1); Succeeds(a); Succeeds(b);
            Succeeds(f.AdvanceTo(120000));
            var s = f.Snapshot;
            Assert.That(FactoryState.Amount(s.facilities.Single(n => n.id == a.FacilityId).stock, "surimi"), Is.GreaterThan(0));
            Assert.That(FactoryState.Amount(s.facilities.Single(n => n.id == b.FacilityId).stock, "surimi"), Is.GreaterThan(0));
        }

        [Test]
        public void LifestyleSupplyChangesConstructionCapacityAndSustainedEffort()
        {
            int BuildCount(long electricity)
            {
                var rules = FactoryStore.LoadRules(); rules.startingElectric = electricity; rules.startingWater = 100;
                var f = new FactorySession(_catalog, rules, _ => { });
                Succeeds(f.Expand(1, 0)); Succeeds(f.Expand(0, 1));
                foreach (var cell in new[] { (0,0), (1,0), (0,1), (1,1), (2,0), (3,0), (2,1), (3,1), (0,2), (1,2), (0,3), (1,3) })
                    f.Build("fishery", cell.Item1, cell.Item2);
                return f.Snapshot.facilities.Count;
            }
            Assert.That(BuildCount(100), Is.GreaterThan(BuildCount(10)));
            var gasRich = Session(); var id = gasRich.Build("fishery", 0, 0).FacilityId;
            Succeeds(gasRich.SetEffort(id, 3)); Succeeds(gasRich.AdvanceTo(41000));
            Assert.That(Stock(gasRich.Snapshot, "fish"), Is.EqualTo(1600));
            var rulesPoor = FactoryStore.LoadRules(); rulesPoor.startingElectric = 10; rulesPoor.startingGas = 4;
            var gasPoor = new FactorySession(_catalog, rulesPoor, _ => { });
            var poor = gasPoor.Build("fishery", 0, 0); Succeeds(poor); Succeeds(gasPoor.SetEffort(poor.FacilityId, 3));
            Succeeds(gasPoor.AdvanceTo(41000));
            Assert.That(Stock(gasPoor.Snapshot, "fish"), Is.EqualTo(400));
            Assert.That(gasPoor.Snapshot.gas, Is.Zero);
        }

        [Test]
        public void ExternalSupplyAndInternalProductionFeedTheUpperOdenRecipe()
        {
            var state = Session().Snapshot; state.stage = 4;
            var f = Session(state); Succeeds(f.Expand(-1, 0));
            foreach (var entry in new[] { ("kamaboko",0,0), ("chikuwa",1,0), ("kelp_farm",0,1), ("warehouse",1,1),
                ("fishery",-2,0), ("preparation",-1,0), ("drying",-2,1), ("saltworks",-1,1) })
                Succeeds(f.Build(entry.Item1, entry.Item2, entry.Item3));
            Assert.That(f.Snapshot.facilities.Count(n => n.upper && n.recipe == "oden"), Is.EqualTo(1));
            for (var at = 120000; at <= 600000; at += 120000) Succeeds(f.AdvanceTo(at));
            Assert.That(Stock(f.Snapshot, "oden") + Transit(f.Snapshot, "oden"), Is.GreaterThanOrEqualTo(3000));
            Assert.That(f.Snapshot.facilities.Where(n => !n.upper && n.recipe == "chikuwa").Any(n => n.processEnd > 0 || n.stock.Any(a => a.amount > 0)), Is.True);
            Assert.That(f.Snapshot.gas, Is.LessThan(1000)); Assert.That(f.Snapshot.electric, Is.LessThan(1000)); Assert.That(f.Snapshot.water, Is.LessThan(1000));
        }

        [Test]
        public void OdenPenguinsApplyBothFramesOnceAtCompletion()
        {
            var state = Session().Snapshot; state.stage = 4; state.nextId = 2; state.builtCount = 1;
            state.penguins.Add(new FactoryAmount("eastern_rockhopper", 1)); state.penguins.Add(new FactoryAmount("erect_crested", 1));
            state.facilities.Add(new FactoryFacility { id = 1, recipe = "oden", level = 4,
                stock = new System.Collections.Generic.List<FactoryAmount> { new FactoryAmount("kamaboko", 10000), new FactoryAmount("chikuwa", 10000), new FactoryAmount("kelp", 10000) } });
            var f = Session(state); Succeeds(f.AdvanceTo(61000));
            Assert.That(Stock(f.Snapshot, "oden"), Is.EqualTo(7200));
            Succeeds(f.Deliver("oden", "oden", 7200));
            Assert.That(f.Snapshot.points, Is.EqualTo(2160000)); Assert.That(Stock(f.Snapshot, "oden"), Is.Zero);
        }

        [Test]
        public void RetiredUpperNodeDoesNotCreateANewBridgeAcrossMissingInternalCells()
        {
            var f = GiftBlock();
            var fishery = f.Snapshot.facilities.Single(n => !n.upper && n.recipe == "fishery");
            var warehouse = f.Snapshot.facilities.Single(n => !n.upper && n.recipe == "warehouse");
            Succeeds(f.Move(fishery.id, 2, 0)); Succeeds(f.Move(warehouse.id, 3, 0));
            var state = f.Snapshot;
            var drying = state.facilities.Single(n => n.recipe == "drying");
            drying.stock.Add(new FactoryAmount("fish", 200));
            var resumed = Session(state); Succeeds(resumed.AdvanceTo(1000));
            Assert.That(resumed.Snapshot.cargo.Any(c => c.from == drying.id), Is.False);
            Assert.That(FactoryState.Amount(resumed.Snapshot.facilities.Single(n => n.id == drying.id).stock, "fish"), Is.EqualTo(200));
        }
    }
}
