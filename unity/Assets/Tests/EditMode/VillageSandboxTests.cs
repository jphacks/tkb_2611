using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.VillageSandbox;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageSandboxTests
    {
        [Test] public void InfiniteResourcesAllowEveryFacilityWithoutPayment()
        {
            var f=new VillageSandboxFacade(true);
            var kinds=Enum.GetValues(typeof(FacilityKind)).Cast<FacilityKind>().ToArray();
            for(var i=0;i<kinds.Length;i++)Assert.That(f.Place(kinds[i],i%4*5,i/4*5).Success,Is.True);
            Assert.That(f.CreateView().Nodes.Count,Is.EqualTo(kinds.Length));
            Assert.That(f.CreateView().Electricity,Is.Zero);
        }
        [Test] public void FinitePaymentAndInvalidPlacementAreAtomic()
        {
            var f=new VillageSandboxFacade(false);
            Assert.That(f.Place(FacilityKind.Fishery,0,0).Success,Is.False);
            f.SetResources(100,100,100);
            Assert.That(f.Place(FacilityKind.Fishery,0,0).Success,Is.True);
            var electricity=f.CreateView().Electricity;
            Assert.That(f.Place(FacilityKind.Fishery,1,1).Success,Is.False);
            Assert.That(f.Place(FacilityKind.Fishery,23,23).Success,Is.False);
            Assert.That(f.CreateView().Electricity,Is.EqualTo(electricity));
        }
        [Test] public void InvalidMoveDoesNotChangeOriginalLocation()
        {
            var f=new VillageSandboxFacade(true);
            var a=f.Place(FacilityKind.Fishery,2,2).Id;f.Place(FacilityKind.Warehouse,6,2);
            Assert.That(f.Move(a,6,2).Success,Is.False);
            Assert.That(f.CreateView().Nodes.Single(n=>n.Id==a).X,Is.EqualTo(2));
            Assert.That(f.Move(a,3,5).Success,Is.True);
        }
        [Test] public void UnlimitedInputsCanRunAdvancedFactoryAndFiniteGasStopsIt()
        {
            var f=new VillageSandboxFacade(true);var id=f.Place(FacilityKind.Oden,2,2).Id;
            f.AdvanceTo(30000);
            Assert.That(f.CreateView().Nodes.Single(n=>n.Id==id).Total,Is.GreaterThan(0));
            var limited=new VillageSandboxFacade(false);limited.SetResources(0,1000,1000);
            var fish=limited.Place(FacilityKind.Fishery,2,2).Id;limited.AdvanceTo(30000);
            Assert.That(limited.CreateView().Nodes.Single(n=>n.Id==fish).Total,Is.Zero);
        }
        [Test] public void TimeSplittingAndDuplicateTimeAreDeterministic()
        {
            var a=new VillageSandboxFacade(true);var b=new VillageSandboxFacade(true);
            foreach(var f in new[]{a,b}){f.Place(FacilityKind.Fishery,2,2);f.Place(FacilityKind.Warehouse,5,2);}
            a.AdvanceTo(10000);for(var t=13;t<10000;t+=137)b.AdvanceTo(t);b.AdvanceTo(10000);
            Assert.That(a.CreateView().Nodes.Select(n=>n.Total),Is.EqualTo(b.CreateView().Nodes.Select(n=>n.Total)));
            var before=b.CreateView().Produced;b.AdvanceTo(10000);
            Assert.That(b.CreateView().Produced,Is.EqualTo(before));
            Assert.Throws<ArgumentOutOfRangeException>(()=>b.AdvanceTo(9999));
        }
        [Test] public void PlacementPreviewDoesNotSpendOrCreateAndSnapshotStaysDetached()
        {
            var f=new VillageSandboxFacade(true);var v=f.CreateView();
            Assert.That(f.CanPlace(FacilityKind.Fishery,2,2),Is.Empty);
            Assert.That(f.CreateView().Nodes.Count,Is.Zero);
            f.Place(FacilityKind.Fishery,2,2);Assert.That(v.Nodes.Count,Is.Zero);
        }
    }
}
