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
    public sealed class VillageWarehouseTests
    {
        private static VillageGame Game()
        { var game = new VillageGame(false); game.SetResources(1000, 1000, 1000); return game; }
        private static VillageNodeView Warehouse(VillageGame game) => game.CreateView().Nodes.Single(n => n.Kind == FacilityKind.Warehouse);

        [Test]
        public void CapacityAndStockBeyondLegacyLimitSurviveJsonRestartWithoutSpendingResources()
        {
            var game = Game(); var id = game.Place(FacilityKind.Warehouse, 0, 0).Id;
            Assert.That(Warehouse(game).Capacity, Is.EqualTo(20));
            var balance = game.CreateView(); Assert.That(game.SetWarehouseCapacity(id, 1000).Success, Is.True);
            Assert.That(game.Inject(id, Item.Fish, 500).Success, Is.True);
            game = VillageGame.Restore(JsonUtility.FromJson<VillageState>(JsonUtility.ToJson(game.Export())));
            Assert.That(Warehouse(game).WarehouseBaseCapacity, Is.EqualTo(1000));
            Assert.That(Warehouse(game).Capacity, Is.EqualTo(1000)); Assert.That(Warehouse(game).Total, Is.EqualTo(500));
            Assert.That(game.CreateView().Electricity, Is.EqualTo(balance.Electricity));
            Assert.That(game.CreateView().Gas, Is.EqualTo(balance.Gas)); Assert.That(game.CreateView().Water, Is.EqualTo(balance.Water));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(10001)] [TestCase(int.MaxValue)]
        public void InvalidCapacityAndOtherFacilityNeverMutateTheWarehouse(int capacity)
        {
            var game = Game(); var id = game.Place(FacilityKind.Warehouse, 0, 0).Id;
            var fish = game.Place(FacilityKind.Fishery, 3, 0).Id;
            Assert.That(game.SetWarehouseCapacity(id, capacity).Success, Is.False);
            Assert.That(game.SetWarehouseCapacity(fish, 100).Success, Is.False);
            Assert.That(game.SetWarehouseCapacity(999, 100).Success, Is.False);
            Assert.That(Warehouse(game).Capacity, Is.EqualTo(20));
        }

        [Test]
        public void ShrinkingIncludesIncomingReservationsAndRetainsTheCargo()
        {
            var game = Game(); game.Place(FacilityKind.Fishery, 0, 0);
            var id = game.Place(FacilityKind.Warehouse, 3, 0).Id;
            game.Inject(id, Item.Fish, 5); game.AdvanceTo(4200);
            Assert.That(game.CreateView().Cargo.Count(c => c.To == id), Is.EqualTo(1));
            Assert.That(game.SetWarehouseCapacity(id, 5).Success, Is.False);
            Assert.That(game.SetWarehouseCapacity(id, 6).Success, Is.True);
            game = VillageGame.Restore(game.Export()); game.AdvanceTo(5700);
            Assert.That(Warehouse(game).Total, Is.EqualTo(6)); Assert.That(Warehouse(game).Capacity, Is.EqualTo(6));
            Assert.That(game.Inject(id, Item.Fish, 1).Success, Is.False);
        }

        [Test]
        public void AutomaticDeliveryUsesTheEnteredCapacity()
        {
            var game = Game(); var fish = game.Place(FacilityKind.Fishery, 0, 0).Id;
            var id = game.Place(FacilityKind.Warehouse, 3, 0).Id;
            game.Inject(id, Item.Fish, 20); game.Inject(fish, Item.Fish, 1);
            game.AdvanceTo(100); Assert.That(game.CreateView().Cargo, Is.Empty);
            game.SetWarehouseCapacity(id, 21); game.AdvanceTo(200);
            Assert.That(game.CreateView().Cargo.Count(c => c.To == id), Is.EqualTo(1));
            game.AdvanceTo(1700); Assert.That(Warehouse(game).Total, Is.EqualTo(21));
            Assert.That(game.SetWarehouseCapacity(id, 20).Success, Is.False);
        }

        [Test]
        public void CoolingAppliesToBasicCapacityAndLosingCoolingNeverDeletesStock()
        {
            var game = Game(); var id = game.Place(FacilityKind.Warehouse, 0, 0).Id;
            var ice = game.Place(FacilityKind.IceHouse, 3, 0).Id;
            game.SetWarehouseCapacity(id, 50); Assert.That(Warehouse(game).Capacity, Is.EqualTo(100));
            game.Inject(id, Item.Fish, 70);
            Assert.That(game.SetWarehouseCapacity(id, 34).Success, Is.False);
            Assert.That(game.SetWarehouseCapacity(id, 35).Success, Is.True);
            game.Move(ice, 20, 20); game = VillageGame.Restore(game.Export());
            Assert.That(Warehouse(game).Capacity, Is.EqualTo(35)); Assert.That(Warehouse(game).Total, Is.EqualTo(70));
            Assert.That(game.Inject(id, Item.Fish, 1).Success, Is.False);
        }

        [Test]
        public void MissingLegacyFieldKeepsDefaultAndInvalidSavedCapacityIsRejected()
        {
            var game = Game(); game.Place(FacilityKind.Warehouse, 0, 0); var state = game.Export();
            Assert.That(VillageGame.Restore(state).CreateView().Nodes.Single().Capacity, Is.EqualTo(20));
            state.Nodes[0].WarehouseCapacity = -1; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state.Nodes[0].WarehouseCapacity = 10001; Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
            state.Nodes[0].WarehouseCapacity = 100; state.Nodes[0].Kind = FacilityKind.Fishery;
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
        }

        private sealed class FailingStore : IProductionVillageStore
        {
            internal bool Fail;
            private readonly ProductionVillageMemoryStore _inner = new ProductionVillageMemoryStore();
            public ProductionVillageSave Load() => _inner.Load();
            public void Save(ProductionVillageSave save) { if (Fail) throw new IOException(); _inner.Save(save); }
        }
        [Test]
        public void OnlySuccessfulSavePublishesTheCapacityChange()
        {
            var store = new FailingStore();
            Func<VillageGame> initial = () => { var game = Game(); game.Place(FacilityKind.Warehouse, 0, 0); return game; };
            var core = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), initial, true); core.Open(1000);
            var id = core.View.Nodes.Single().Id; store.Fail = true;
            Assert.That(core.SetWarehouseCapacity(id, 200, 1000), Is.False);
            Assert.That(core.View.Nodes.Single().Capacity, Is.EqualTo(20));
            store.Fail = false; Assert.That(core.SetWarehouseCapacity(id, 200, 1000), Is.True);
            var again = new ProductionVillageSessionCore(store, new ProductionVillageMemoryStore(true), initial, true); again.Open(1000);
            Assert.That(again.View.Nodes.Single().Capacity, Is.EqualTo(200));
        }
    }
}
