using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageStateTests
    {
        internal static VillageGame Game()
        {var g=new VillageGame(false);g.SetResources(100,1000,1000);g.Place(FacilityKind.Fishery,2,2);g.Place(FacilityKind.Preparation,5,2);g.Place(FacilityKind.Warehouse,8,2);return g;}
        [Test] public void RestoreDuringDeliveryPreservesReservationsAndPaidFinish()
        {
            var g=Game();g.AdvanceTo(4200);var before=g.CreateView();Assert.That(before.Cargo.Count,Is.GreaterThan(0));
            var restored=VillageGame.Restore(g.Export());Assert.That(restored.Move(before.Cargo[0].To,15,15).Success,Is.False);
            g.AdvanceTo(30000);restored.AdvanceTo(30000);
            Assert.That(restored.CreateView().Totals,Is.EquivalentTo(g.CreateView().Totals));
            Assert.That(restored.CreateView().Gas,Is.EqualTo(g.CreateView().Gas));
        }
        [Test] public void InjectRespectsIncomingAndProductionReservations()
        {
            var g=Game();g.AdvanceTo(4200);var c=g.CreateView().Cargo.First();
            Assert.That(g.Inject(c.To,Item.Fish,12).Success,Is.False);
            Assert.That(g.Inject(c.To,Item.Fish,int.MaxValue).Success,Is.False);
            Assert.That(g.Inject(c.To,Item.Fish,-1).Success,Is.False);
        }
        [Test] public void ChangingSpeedDoesNotChangeExistingDeadlines()
        {
            var g=Game();g.AdvanceTo(4200);var s=g.Export();g.Configure(false,10,100);
            var after=g.Export();Assert.That(after.Nodes.Select(n=>n.Finish),Is.EqualTo(s.Nodes.Select(n=>n.Finish)));
            Assert.That(after.Cargo.Select(c=>c.End),Is.EqualTo(s.Cargo.Select(c=>c.End)));
        }
        [Test] public void InvalidCoordinatesAndUnsupportedEffectsDoNotSpend()
        {
            var g=Game();var balance=g.CreateView().Electricity;
            foreach(var kind in new[]{FacilityKind.Dormitory,FacilityKind.Research,FacilityKind.IceHouse,FacilityKind.Sorter,FacilityKind.GasAmplifier})Assert.That(g.Place(kind,20,20).Success,Is.False);
            Assert.That(g.Move(1,int.MaxValue,0).Success,Is.False);Assert.That(g.CreateView().Electricity,Is.EqualTo(balance));
        }
        [Test] public void SnapshotAndExportCannotMutateGame()
        {var g=Game();var s=g.Export();s.Nodes[0].Stock[0]=20;s.Ledger.MaxSteps=9000;Assert.That(g.CreateView().Nodes[0].Total,Is.Zero);Assert.That(g.Records.MaxSteps,Is.Zero);}
        [Test] public void HealthReceiptsAreIdempotentAcrossRestoreAndDayRollover()
        {
            var g=Game();Assert.That(g.AcceptSteps(5,1500,5).Success,Is.True);var gas=g.CreateView().Gas;
            g=VillageGame.Restore(g.Export());g.AcceptSteps(5,1000,5);g.AcceptSteps(5,1500,5);Assert.That(g.CreateView().Gas,Is.EqualTo(gas));
            Assert.That(g.AcceptSteps(5,2000,6).Success,Is.False);g.AcceptSteps(6,500,6);Assert.That(g.CreateView().Gas,Is.EqualTo(gas+1));
            Assert.That(g.AcceptWater("water",6,6,true).Success,Is.True);Assert.That(g.AcceptWater("other",6,6,true).Success,Is.False);
            Assert.That(g.AcceptScreen("screen",5,120,6,true,true).Success,Is.True);Assert.That(g.AcceptScreen("water",5,120,6,true,true).Success,Is.False);
            Assert.That(g.AcceptWater("late",5,6,true).Success,Is.False);
        }
        [Test,Timeout(3000)] public void LongOfflineAndSplitTimeProduceSameResult()
        {
            var a=Game();var b=Game();a.AdvanceTo(2592000000L);
            for(var t=0;t<=60000;t+=137)b.AdvanceTo(t);b.AdvanceTo(2592000000L);
            Assert.That(a.CreateView().Totals,Is.EquivalentTo(b.CreateView().Totals));Assert.That(a.CreateView().Gas,Is.EqualTo(b.CreateView().Gas));
        }
        [Test] public void ArrivalReopensRouteBeforeNextProductionDeadline()
        {
            var a=new VillageGame(false);a.SetResources(100,100,100);var id=a.Place(FacilityKind.Fishery,0,0).Id;a.Place(FacilityKind.Warehouse,3,0);a.Inject(id,Item.Fish,6);
            var b=VillageGame.Restore(a.Export());a.AdvanceTo(3000);for(var t=100;t<=3000;t+=100)b.AdvanceTo(t);
            Assert.That(a.CreateView().Gas,Is.EqualTo(b.CreateView().Gas));Assert.That(a.CreateView().Totals,Is.EquivalentTo(b.CreateView().Totals));
        }
    }
}
