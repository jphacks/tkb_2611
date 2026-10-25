using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectPenguin.Domain.LogisticsPrototype;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class EvolutionFacadeTests
    {
        private static int Add(EvolutionFacade f,WorkshopKind k,int x,int y)
        { var r=f.Place(k,x,y); Assert.That(r.Success,Is.True,r.Reason); return r.SubjectId; }
        private static EvolutionFacade Row(bool vertical=false,bool salt=false)
        {
            var f=new EvolutionFacade();
            for(var i=0;i<3;i++) Add(f,i==1&&salt?WorkshopKind.Saltworks:WorkshopKind.Fishery,vertical?4:4+2*i,vertical?4+2*i:4);
            return f;
        }
        [TestCase(false,false,WorkshopGood.Chikuwa)] [TestCase(true,false,WorkshopGood.Chikuwa)]
        [TestCase(false,true,WorkshopGood.Kamaboko)] [TestCase(true,true,WorkshopGood.Kamaboko)]
        public void StraightPatternsHaveExpectedCoreAndExtent(bool vertical,bool salt,WorkshopGood expected)
        {
            var s=Row(vertical,salt).CreateView().Formations.Single();
            Assert.That(s.Product,Is.EqualTo(expected)); Assert.That(s.CoreId,Is.EqualTo(2));
            Assert.That(s.Width,Is.EqualTo(vertical?2:6)); Assert.That(s.Height,Is.EqualTo(vertical?6:2));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AllFourLShapesBecomeDryingRack(int orientation)
        {
            var f=new EvolutionFacade();
            var cells=new[]{new[]{0,0,1,0,0,1},new[]{1,0,0,0,1,1},new[]{1,1,1,0,0,1},new[]{0,1,0,0,1,1}}[orientation];
            Add(f,WorkshopKind.Saltworks,4+2*cells[0],4+2*cells[1]);
            Add(f,WorkshopKind.Fishery,4+2*cells[2],4+2*cells[3]);
            Add(f,WorkshopKind.Fishery,4+2*cells[4],4+2*cells[5]);
            Assert.That(f.CreateView().Formations.Single().Product,Is.EqualTo(WorkshopGood.Himono));
        }
        [Test] public void MovingBreaksAndRestoresShapeWithoutLosingInventoryOrIds()
        {
            var f=Row();f.AdvanceTo(4000);var before=f.CreateView();
            var broken=f.Move(3,12,4); Assert.That(broken.Success,Is.True);
            Assert.That(broken.Events.Count(e=>e.Kind==EvolutionEventKind.Dissolved),Is.EqualTo(1));
            Assert.That(f.CreateView().Formations,Is.Empty);
            Assert.That(f.CreateView().Workshops.Sum(n=>n.Total),Is.EqualTo(before.Workshops.Sum(n=>n.Total)));
            var restored=f.Move(3,8,4);Assert.That(restored.Events.Count(e=>e.Kind==EvolutionEventKind.Formed),Is.EqualTo(1));
            Assert.That(f.CreateView().Workshops.Select(n=>n.Id),Is.EquivalentTo(new[]{1,2,3}));
        }
        [Test] public void RejectsOccupiedOutOfBoundsAndUnknownOperationsAtomically()
        {
            var f=new EvolutionFacade();var id=Add(f,WorkshopKind.Fishery,0,0);
            Assert.That(f.Place(WorkshopKind.Saltworks,1,1).Success,Is.False);
            Assert.That(f.Place(WorkshopKind.Fishery,23,0).Success,Is.False);
            Assert.That(f.Move(id,-1,0).Success,Is.False);Assert.That(f.Move(99,4,4).Success,Is.False);
            Assert.That(f.Remove(99).Success,Is.False);Assert.That(f.Place((WorkshopKind)99,8,8).Success,Is.False);
            Assert.That(f.CreateView().Workshops.Count,Is.EqualTo(1));
        }
        [Test] public void WiresAndDistantFacilitiesDoNotCreateShapes()
        {
            var f=new EvolutionFacade();var a=Add(f,WorkshopKind.Fishery,2,2);
            Add(f,WorkshopKind.Fishery,8,2);Add(f,WorkshopKind.Fishery,16,2);
            var w=Add(f,WorkshopKind.Warehouse,4,8);
            Assert.That(f.Connect(a,w).Success,Is.True);Assert.That(f.Connect(a,w).Success,Is.False);
            Assert.That(f.Connect(a,a).Success,Is.False);Assert.That(f.Connect(w,a).Success,Is.False);
            Assert.That(f.CreateView().Formations,Is.Empty);
        }
        [Test] public void LongRowDoesNotDoubleUseMembersWithinSameRecipe()
        {
            var f=new EvolutionFacade();for(var i=0;i<6;i++) Add(f,WorkshopKind.Fishery,2+2*i,2);
            Assert.That(f.CreateView().Formations.Count(s=>s.Product==WorkshopGood.Chikuwa),Is.EqualTo(2));
            Assert.That(f.CreateView().Formations.SelectMany(s=>s.Members).Distinct().Count(),Is.EqualTo(6));
        }
        [Test] public void ViewsAreDetachedAndCollectionsRejectMutation()
        {
            var f=Row();var view=f.CreateView();f.Move(3,12,4);f.AdvanceTo(2000);
            Assert.That(view.Formations.Count,Is.EqualTo(1));Assert.That(view.Workshops[0].Total,Is.Zero);
            Assert.Throws<NotSupportedException>(()=>((IDictionary<WorkshopGood,int>)view.Workshops[0].Stock).Add(WorkshopGood.Fish,99));
            Assert.Throws<NotSupportedException>(()=>((IList<int>)view.Formations[0].Members)[0]=99);
        }
        [Test] public void EvolutionAddsProductEveryTwoWorkingCyclesAndIsNotReplayedAtSameTime()
        {
            var f=Row();var first=f.AdvanceTo(2000);Assert.That(f.CreateView().Workshops[1].Held(WorkshopGood.Chikuwa),Is.Zero);
            var second=f.AdvanceTo(4000);Assert.That(f.CreateView().Workshops[1].Held(WorkshopGood.Chikuwa),Is.EqualTo(1));
            Assert.That(second.Events.Count(e=>e.Kind==EvolutionEventKind.Produced&&e.Good==WorkshopGood.Chikuwa),Is.EqualTo(1));
            Assert.That(f.AdvanceTo(4000).Events,Is.Empty);Assert.That(f.AdvanceTo(3999).Success,Is.False);
        }
        [Test] public void DispatchIsDelayedReservedAndCannotDoubleDeliverAfterDisconnect()
        {
            var f=Row();var w=Add(f,WorkshopKind.Warehouse,4,10);f.Connect(2,w);
            f.AdvanceTo(2050);var flight=f.CreateView().Cargo.Single();Assert.That(flight.Good,Is.EqualTo(WorkshopGood.Fish));
            f.DisconnectAll();Assert.That(f.Move(w,10,10).Success,Is.False);Assert.That(f.Remove(w).Success,Is.False);
            f.AdvanceTo(3500);Assert.That(f.CreateView().Workshops.Single(n=>n.Id==w).Total,Is.Zero);
            f.AdvanceTo(3550);Assert.That(f.CreateView().Workshops.Single(n=>n.Id==w).Total,Is.EqualTo(1));
            f.AdvanceTo(6000);Assert.That(f.CreateView().Workshops.Single(n=>n.Id==w).Total,Is.EqualTo(1));
        }
        [Test] public void SplitAndBatchTimeProduceIdenticalResultsAndNeverExceedCapacities()
        {
            var a=Row();var b=Row();var wa=Add(a,WorkshopKind.Warehouse,4,10);var wb=Add(b,WorkshopKind.Warehouse,4,10);
            a.Connect(2,wa);b.Connect(2,wb);a.AdvanceTo(60000);
            for(var i=1;i<=1200;i++) b.AdvanceTo(i*50);
            string Summary(EvolutionFacade f) => string.Join(";",f.CreateView().Workshops.Select(n=>n.Id+":"+string.Join(",",n.Stock.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value))));
            Assert.That(Summary(a),Is.EqualTo(Summary(b)));
            Assert.That(a.CreateView().Workshops.Single(n=>n.Id==wa).Total,Is.LessThanOrEqualTo(20));
            Assert.That(a.CreateView().Workshops.Where(n=>n.Kind!=WorkshopKind.Warehouse).All(n=>n.Stock.Values.All(v=>v>=0&&v<=6)),Is.True);
        }
        [Test] public void MovingSenderKeepsDispatchedCargoAndReceiverReservation()
        {
            var f=Row();var w=Add(f,WorkshopKind.Warehouse,4,10);f.Connect(3,w);f.AdvanceTo(2050);
            f.DisconnectAll();Assert.That(f.Move(3,14,4).Success,Is.True);
            Assert.That(f.CreateView().Cargo.Count,Is.EqualTo(1));
            f.AdvanceTo(3550);Assert.That(f.CreateView().Workshops.Single(n=>n.Id==w).Total,Is.EqualTo(1));
        }
    }
}
