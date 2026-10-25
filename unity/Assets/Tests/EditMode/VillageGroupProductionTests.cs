using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Data;
using ProjectPenguin.Systems;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageGroupProductionTests
    {
        static void Active(object game,bool value)
        {
            var method=game.GetType().GetMethod("SetGroupViewActive");
            Assert.That(method,Is.Not.Null,"zoom activation API must exist");method.Invoke(game,new object[]{value});
        }
        static VillageGame Line(int spacing=2)
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,0);g.Place(FacilityKind.Fishery,spacing,0);g.Place(FacilityKind.Fishery,2*spacing,0);
            var s=g.Export();s.Unlimited=false;s.Gas=1000000;s.Electricity=1000000;s.Water=1000000;s.Speed=.01;
            // Paid jobs stay busy so sampling is independent of the ordinary output clock.
            foreach(var n in s.Nodes){n.Started=0;n.Finish=100000000;}
            return VillageGame.Restore(s);
        }
        static int Bonus(VillageGame g)=>g.CreateView().Totals[Item.Chikuwa];
        [Test] public void SpacingThreeDoesNotFormAGroup()=>Assert.That(Line(3).CreateView().Synergies,Is.Empty);
        [Test] public void DifferentProductsMayShareFacilities()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,2);g.Place(FacilityKind.Saltworks,2,2);g.Place(FacilityKind.Fishery,4,2);g.Place(FacilityKind.Fishery,2,4);
            Assert.That(g.CreateView().Synergies.Select(s=>s.BonusItem),Is.EquivalentTo(new[]{Item.Kamaboko,Item.DriedFish}));
        }
        [Test] public void SameProductUsesTopLeftDeterministicDisjointGroups()
        {
            var g=new VillageGame(true);for(var i=5;i>=0;i--)g.Place(FacilityKind.Fishery,i*2,0);
            Assert.That(g.CreateView().Synergies.Select(s=>s.MinX),Is.EqualTo(new[]{0,6}));
        }
        [Test] public void TwoTenMinuteSamplesCreateActualCenterStock()
        {
            var g=Line();Active(g,true);g.AdvanceTo(600000);Assert.That(Bonus(g),Is.Zero);g.AdvanceTo(1200000);
            Assert.That(Bonus(g),Is.EqualTo(1));Assert.That(g.CreateView().Nodes.Single(n=>n.X==2).Stock[Item.Chikuwa],Is.EqualTo(1));
            Assert.That(g.CreateView().Synergies.Single().BonusPerMinute,Is.EqualTo(.05).Within(.000001));
        }
        [Test] public void ZoomAndReadsDoNotResetOrMultiplyActiveTime()
        {
            var g=Line();Active(g,true);g.AdvanceTo(300050);for(var i=0;i<12;i++){g.CreateView();Active(g,false);Active(g,true);}
            Active(g,false);g.AdvanceTo(900050);Active(g,true);g.AdvanceTo(1800000);Assert.That(Bonus(g),Is.EqualTo(1));
        }
        [Test] public void SplitAdvanceAndIdleJumpMatch()
        {
            var a=Line();var b=Line();Active(a,true);Active(b,true);a.AdvanceTo(1200000);
            for(var t=137;t<1200000;t+=137)b.AdvanceTo(t);b.AdvanceTo(1200000);Assert.That(Bonus(a),Is.EqualTo(1));Assert.That(Bonus(b),Is.EqualTo(1));
        }
        [Test] public void RestorePreservesProgressButStopsOfflineBonus()
        {
            var g=Line();Active(g,true);g.AdvanceTo(600000);var state=g.Export();var restored=VillageGame.Restore(state);restored.AdvanceTo(1800000);Assert.That(Bonus(restored),Is.Zero);
            Active(restored,true);restored.AdvanceTo(2400000);Assert.That(Bonus(restored),Is.EqualTo(1));
            var old=Line().Export();Assert.DoesNotThrow(()=>VillageGame.Restore(old));
        }
        [Test] public void FullStockAndIncomingReservationsPreventOverflow()
        {
            var g=Line();var s=g.Export();s.Nodes[1].Stock[(int)Item.Chikuwa]=6;g=VillageGame.Restore(s);Active(g,true);g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.EqualTo(6));
            s=Line().Export();s.Nodes[1].Stock[(int)Item.Fish]=10;s.Cargo.Add(new VillageCargoState{Id=1,From=1,To=2,Item=Item.Fish,Start=0,End=10000000,FromX=0,FromY=0,ToX=2,ToY=0});s.NextCargo=2;
            g=VillageGame.Restore(s);Active(g,true);g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.Zero);Assert.DoesNotThrow(()=>VillageGame.Restore(g.Export()));
        }
        [Test] public void GasExhaustionAndMoveStopBonus()
        {
            var g=Line();g.SetResources(0,0,0);Active(g,true);g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.Zero);
            g=Line();Active(g,true);g.AdvanceTo(600000);Assert.That(g.Move(3,8,0).Success,Is.True);g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.Zero);
        }
        [Test] public void BonusStockUsesOrdinaryWarehouseAndInputDeliveryAfterZoomIn()
        {
            var g=Line();Active(g,true);g.AdvanceTo(1200000);Active(g,false);g.Place(FacilityKind.Warehouse,3,3);g.Place(FacilityKind.Oden,6,0);g.AdvanceTo(1200100);
            Assert.That(g.CreateView().Cargo.Any(c=>c.Item==Item.Chikuwa),Is.True);g.AdvanceTo(1202000);Assert.That(g.CreateView().Nodes.Where(n=>n.Kind==FacilityKind.Oden||n.Kind==FacilityKind.Warehouse).Sum(n=>n.Stock.TryGetValue(Item.Chikuwa,out var held)?held:0),Is.EqualTo(1));
        }
        [Test] public void EverySaltCornerOrientationFormsDriedFish()
        {
            foreach(var dx in new[]{-2,2})foreach(var dy in new[]{-2,2})
            {var g=new VillageGame(true);g.Place(FacilityKind.Saltworks,4,4);g.Place(FacilityKind.Fishery,4+dx,4);g.Place(FacilityKind.Fishery,4,4+dy);Assert.That(g.CreateView().Synergies.Single().BonusItem,Is.EqualTo(Item.DriedFish));}
        }
        [Test] public void ProductCapIncludesInboundOfThatProduct()
        {
            var s=Line().Export();s.Nodes[1].Stock[(int)Item.Chikuwa]=5;s.Cargo.Add(new VillageCargoState{Id=1,From=1,To=2,Item=Item.Chikuwa,Start=0,End=10000000,FromX=0,FromY=0,ToX=2,ToY=0});s.NextCargo=2;
            var g=VillageGame.Restore(s);Active(g,true);g.AdvanceTo(1200000);Assert.That(g.CreateView().Nodes[1].Stock[Item.Chikuwa],Is.EqualTo(5));
        }
        [Test] public void RemovingMemberAndOldNullProgressAreSafe()
        {
            var s=Line().Export();foreach(var n in s.Nodes)n.Finish=0;s.GroupProgress=null;var g=VillageGame.Restore(s);
            Assert.That(g.Remove(3).Success,Is.True);Active(g,true);g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.Zero);Assert.That(g.CreateView().Synergies,Is.Empty);
        }
        [Test] public void SavedProgressIsDetachedAndInvalidProgressIsRejected()
        {
            var g=Line();Active(g,true);g.AdvanceTo(600000);var s=g.Export();s.GroupProgress.Chikuwa=0;g.AdvanceTo(1200000);Assert.That(Bonus(g),Is.EqualTo(1));
            s=g.Export();s.GroupProgress.Chikuwa=2;Assert.Throws<ArgumentException>(()=>VillageGame.Restore(s));
        }
        [Test] public void OrdinaryProductionContinuesWithGroupViewActive()
        {
            var g=new VillageGame(true);for(var x=0;x<6;x+=2)g.Place(FacilityKind.Fishery,x,0);Active(g,true);g.AdvanceTo(5000);
            Assert.That(g.CreateView().Totals[Item.Fish],Is.EqualTo(3));Assert.That(Bonus(g),Is.Zero);
        }
        [Test] public void SharedCoreLastSlotKeepsOtherProductProgress()
        {
            var g=new VillageGame(true);g.Place(FacilityKind.Fishery,0,2);g.Place(FacilityKind.Saltworks,2,2);g.Place(FacilityKind.Fishery,4,2);g.Place(FacilityKind.Fishery,2,4);
            var s=g.Export();s.Unlimited=false;s.Gas=1000000;s.Nodes[1].Stock[(int)Item.Fish]=10;
            foreach(var n in s.Nodes){n.Started=0;n.Finish=100000000;}s.GroupProgress.Kamaboko=1;s.GroupProgress.DriedFish=1;
            g=VillageGame.Restore(s);Active(g,true);g.AdvanceTo(600000);
            Assert.That(g.CreateView().Totals[Item.Kamaboko],Is.EqualTo(1));Assert.That(g.CreateView().Totals[Item.DriedFish],Is.Zero);
            Assert.That(g.Export().GroupProgress.DriedFish,Is.EqualTo(1));Assert.DoesNotThrow(()=>VillageGame.Restore(g.Export()));
        }
        [Test] public void MultipleGroupsPoolProgressAndStopAtProductCap()
        {
            var g=Line();g.Configure(true,1,1500);for(var x=0;x<6;x+=2)g.Place(FacilityKind.Fishery,x,6);
            var s=g.Export();foreach(var n in s.Nodes){n.Started=0;n.Finish=100000000;}g=VillageGame.Restore(s);Active(g,true);g.AdvanceTo(600000);
            Assert.That(Bonus(g),Is.EqualTo(1));g=Line();Active(g,true);g.AdvanceTo(8400000);Assert.That(Bonus(g),Is.EqualTo(6));
        }
        [Test] public void BonusDeliveryIsNotStarvedByNormalOutputOnTheSameRoute()
        {
            var s=Line().Export();s.Nodes[1].Stock[(int)Item.Fish]=6;s.Nodes[1].Stock[(int)Item.Chikuwa]=1;
            var g=VillageGame.Restore(s);g.Place(FacilityKind.Warehouse,3,3);g.AdvanceTo(100);
            Assert.That(g.CreateView().Cargo.Any(c=>c.Item==Item.Chikuwa),Is.True);
        }
        [Test] public void BonusCanEnterMatchingRecipeWithoutAWarehouse()
        {
            var g=Line();Active(g,true);g.AdvanceTo(1200000);Active(g,false);Assert.That(g.Place(FacilityKind.Oden,6,0).Success,Is.True);g.AdvanceTo(1200100);
            Assert.That(g.CreateView().Cargo.Single().Item,Is.EqualTo(Item.Chikuwa));g.AdvanceTo(1202000);
            Assert.That(g.CreateView().Nodes.Single(n=>n.Kind==FacilityKind.Oden).Stock[Item.Chikuwa],Is.EqualTo(1));
        }
        sealed class Store:IProductionVillageStore
        {public ProductionVillageSave Saved;public bool Fail;public ProductionVillageSave Load()=>Saved;public void Save(ProductionVillageSave s){if(Fail)throw new InvalidOperationException();Saved=s;}}
        [Test] public void SessionCommandsAndFailedSavesKeepTransientModeAndProgress()
        {
            var store=new Store();var core=new ProductionVillageSessionCore(store,new Store(),LineDefault,true);core.Open(0);Active(core,true);core.Tick(600000);store.Fail=true;
            Assert.That(core.AcceptSteps(1,500,1,0),Is.False);store.Fail=false;Assert.That(core.AcceptSteps(1,500,1,0),Is.True);core.Tick(600000);
            Assert.That(core.View.Totals[Item.Chikuwa],Is.EqualTo(1));
        }
        static VillageGame LineDefault()=>Line();
        [Test] public void SessionResumeDoesNotAwardSuspendedBonus()
        {
            var core=new ProductionVillageSessionCore(new Store(),new Store(),LineDefault,true);core.Open(0);Active(core,true);core.Tick(600000);Assert.That(core.Resume(1200000,0),Is.True);
            Assert.That(core.View.Totals[Item.Chikuwa],Is.Zero);Active(core,true);core.Tick(600000);Assert.That(core.View.Totals[Item.Chikuwa],Is.EqualTo(1));
        }
    }
}
