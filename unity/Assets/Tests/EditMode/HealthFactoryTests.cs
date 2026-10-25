using System;
using NUnit.Framework;
using ProjectPenguin.Domain.HealthFactory;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class HealthFactoryTests
    {
        private const int Today = 100;
        [Test] public void StepsHaveNoTwentyGasCapAndNoDuplicateGrant()
        {
            var f=new HealthFactoryFacade();
            Assert.That(f.AcceptSteps(Today,499,Today).Delta,Is.Zero);
            Assert.That(f.AcceptSteps(Today,15000,Today).Delta,Is.EqualTo(30));
            Assert.That(f.AcceptSteps(Today,15000,Today).Delta,Is.Zero);
            Assert.That(f.AcceptSteps(Today,500,Today).Delta,Is.Zero);
            Assert.That(f.AcceptSteps(Today+1,500,Today+1).Delta,Is.EqualTo(1));
            Assert.That(f.AcceptSteps(Today,20000,Today+1).Success,Is.False);
            Assert.That(f.CreateView().Gas,Is.EqualTo(31));
        }
        [TestCase(-1)] [TestCase(172801)] [TestCase(int.MaxValue)]
        public void SuspiciousStepsAreRejectedWithoutMutation(int steps)
        {
            var f=new HealthFactoryFacade(); Assert.That(f.AcceptSteps(Today,steps,Today).Success,Is.False);
            Assert.That(f.CreateView().Gas,Is.Zero);
        }
        [TestCase(true,6000,12)] [TestCase(false,10000,20)]
        public void SameGoalCanBeReachedAtDifferentFuelCosts(bool compact,int steps,int cost)
        {
            var f=new HealthFactoryFacade(compact); f.AcceptSteps(Today,steps,Today); f.AdvanceTo(120000);
            var v=f.CreateView(); Assert.That(v.Stored,Is.EqualTo(4)); Assert.That(v.Spent,Is.EqualTo(cost));
            Assert.That(v.Gas,Is.Zero); Assert.That(v.Raw+v.Input+v.Output+v.Deliveries.Count+(v.Processing?1:0)+v.Stored,Is.EqualTo(4));
        }
        [Test] public void AdditionalWalkingOvercomesLongLayoutWithoutMoving()
        {
            var f=new HealthFactoryFacade();f.AcceptSteps(Today,6000,Today);f.AdvanceTo(60000);
            Assert.That(f.CreateView().Stored,Is.LessThan(4)); Assert.That(f.Quote().GasShortfall,Is.EqualTo(8));
            f.AcceptSteps(Today,10000,Today);f.AdvanceTo(120000);Assert.That(f.CreateView().Stored,Is.EqualTo(4));
        }
        [Test] public void NoGasWaitsAndAlreadyPaidDeliveryFinishes()
        {
            var f=new HealthFactoryFacade();f.AdvanceTo(10000);Assert.That(f.CreateView().Raw,Is.EqualTo(4));
            f.AcceptSteps(Today,1000,Today);f.AdvanceTo(10050);Assert.That(f.CreateView().Gas,Is.Zero);
            f.SetRoute(0,false);f.AdvanceTo(20000);
            Assert.That(f.CreateView().Input,Is.EqualTo(1));Assert.That(f.CreateView().Raw,Is.EqualTo(3));
            Assert.That(f.CreateView().Spent,Is.EqualTo(2));Assert.That(f.CreateView().Deliveries.Count,Is.Zero);
        }
        [Test] public void PaidProcessingFinishesWithoutMoreFuel()
        {
            var f=new HealthFactoryFacade(true);f.AcceptSteps(Today,1000,Today);f.AdvanceTo(50);f.SetRoute(0,false);f.AdvanceTo(10000);
            Assert.That(f.CreateView().Output,Is.EqualTo(1));Assert.That(f.CreateView().Gas,Is.Zero);
        }
        [Test] public void QuoteDoesNotChangeRealBalancesOrReceiptsAndCountsRemainder()
        {
            var f=new HealthFactoryFacade(true);f.AcceptSteps(Today,499,Today);var q=f.Quote();
            Assert.That(q.FutureGas,Is.EqualTo(12));Assert.That(q.AdditionalSteps,Is.EqualTo(5501));
            Assert.That(f.CreateView().Gas,Is.Zero);Assert.That(f.CreateView().AtMs,Is.Zero);
            Assert.That(f.AcceptSteps(Today,6000,Today).Delta,Is.EqualTo(12));
            f.AdvanceTo(50);Assert.That(f.Quote().FutureGas,Is.EqualTo(11));
        }
        [Test] public void DisconnectedQuoteDoesNotClaimWalkingWillFixIt()
        {
            var f=new HealthFactoryFacade();f.SetRoute(1,false);Assert.That(f.Quote().Reachable,Is.False);
        }
        [Test] public void SplitAndRepeatedTimeMatchAndBackwardsTimeIsRejected()
        {
            var a=new HealthFactoryFacade();var b=new HealthFactoryFacade();
            a.AcceptSteps(Today,15000,Today);b.AcceptSteps(Today,15000,Today);a.AdvanceTo(120000);
            for(long t=17;t<120000;t+=137)b.AdvanceTo(t);b.AdvanceTo(120000);b.AdvanceTo(120000);
            Assert.That(b.CreateView().Stored,Is.EqualTo(a.CreateView().Stored));
            Assert.That(b.CreateView().Gas,Is.EqualTo(a.CreateView().Gas));Assert.That(b.AdvanceTo(119999).Success,Is.False);
        }
        [Test] public void DisconnectedDownstreamWaitsWithoutExtraFuelConsumption()
        {
            var f=new HealthFactoryFacade();f.AcceptSteps(Today,15000,Today);f.SetRoute(1,false);f.AdvanceTo(60000);
            var v=f.CreateView();Assert.That(v.Output,Is.EqualTo(4));Assert.That(v.Spent,Is.EqualTo(12));
            f.AdvanceTo(120000);Assert.That(f.CreateView().Spent,Is.EqualTo(12));
        }
        [Test] public void MovingReceiverWithIncomingCargoIsRejectedAndSenderCanMove()
        {
            var f=new HealthFactoryFacade();f.AcceptSteps(Today,10000,Today);f.AdvanceTo(50);
            Assert.That(f.Move(1,8,8).Success,Is.False);Assert.That(f.Move(0,8,2).Success,Is.True);
            Assert.That(f.CreateView().Deliveries[0].FromX,Is.EqualTo(6));
            Assert.That(f.Move(0,6,8).Success,Is.False);
        }
        [Test] public void FailedConstructionAndExpansionAreAtomic()
        {
            var f=new HealthFactoryFacade();Assert.That(f.BuildBuffer().Success,Is.False);Assert.That(f.Expand().Success,Is.False);
            Assert.That(f.CreateView().InputCapacity,Is.EqualTo(2));Assert.That(f.CreateView().BoardSize,Is.EqualTo(16));
            f.AcceptScreen("screen-a",Today-1,240,Today,true,true);Assert.That(f.CreateView().Electricity,Is.EqualTo(10));
            Assert.That(f.BuildBuffer().Success,Is.True);Assert.That(f.Upgrade().Success,Is.True);
            Assert.That(f.CreateView().Electricity,Is.EqualTo(2));Assert.That(f.CreateView().Slots,Is.EqualTo(2));
            Assert.That(f.BuildBuffer().Success,Is.False);Assert.That(f.CreateView().Electricity,Is.EqualTo(2));
            f.AcceptWater("water-a",Today,Today,true);f.Expand();Assert.That(f.CreateView().Water,Is.Zero);
            Assert.That(f.CreateView().BoardSize,Is.EqualTo(20));Assert.That(f.Move(0,18,2).Success,Is.True);
        }
        [Test] public void ImageRecordsNeedConfirmationDateAndOneReceiptPerTargetDay()
        {
            var f=new HealthFactoryFacade();Assert.That(f.AcceptWater("w",Today,Today,false).Success,Is.False);
            Assert.That(f.AcceptWater("w",Today,Today,true).Success,Is.True);
            Assert.That(f.AcceptWater("w2",Today,Today,true).Success,Is.False);
            Assert.That(f.AcceptScreen("s",Today,240,Today,true,true).Success,Is.False);
            Assert.That(f.AcceptScreen("s",Today-1,-1,Today,true,true).Success,Is.False);
            Assert.That(f.AcceptScreen("s",Today-1,240,Today,false,true).Success,Is.False);
            Assert.That(f.AcceptScreen("s",Today-1,240,Today,true,true).Success,Is.True);
            Assert.That(f.AcceptScreen("s2",Today-1,0,Today,true,true).Success,Is.False);
            Assert.That(f.AcceptWater("w",Today+1,Today+1,true).Success,Is.False);
            Assert.That(f.CreateView().Water,Is.EqualTo(3));Assert.That(f.CreateView().Electricity,Is.EqualTo(10));
        }
        [Test] public void WaterAndElectricityHaveNoInventoryCap()
        {
            var f=new HealthFactoryFacade();for(var d=1;d<=100;d++){f.AcceptWater("w"+d,d,d,true);f.AcceptScreen("s"+d,d-1,0,d,true,true);}
            Assert.That(f.CreateView().Water,Is.EqualTo(300));Assert.That(f.CreateView().Electricity,Is.EqualTo(1200));
        }
        [Test] public void FixtureMultiplierIsNotRetroactiveAndSpeedDoesNotChangeFuelCost()
        {
            var f=new HealthFactoryFacade(true);f.AcceptSteps(Today,500,Today);f.SetPenguinFixture(true,true);
            Assert.That(f.AcceptSteps(Today,500,Today).Delta,Is.Zero);
            Assert.That(f.AcceptSteps(Today,1000,Today).Delta,Is.EqualTo(2));
            Assert.That(f.Quote().FutureGas,Is.EqualTo(12));Assert.That(f.CreateView().ProcessingDurationMs,Is.EqualTo(1000));
        }
        [Test] public void NewDayQuoteDoesNotReuseYesterdayStepRemainder()
        {
            var f=new HealthFactoryFacade(true);f.AcceptSteps(Today,499,Today);
            Assert.That(f.Quote(Today).AdditionalSteps,Is.EqualTo(5501));
            Assert.That(f.Quote(Today+1).AdditionalSteps,Is.EqualTo(6000));
            Assert.That(f.CreateView().MaxSteps,Is.EqualTo(499));
        }
        [Test] public void BufferReservationsAndQuantityAreConservedAtEveryTick()
        {
            var f=new HealthFactoryFacade(true);f.AcceptScreen("screen",Today-1,0,Today,true,true);f.BuildBuffer();
            f.AcceptSteps(Today,15000,Today);
            for(var t=50;t<=20000;t+=50)
            {
                f.AdvanceTo(t);var v=f.CreateView();var inbound=0;foreach(var d in v.Deliveries)if(d.Route==0)inbound++;
                Assert.That(v.Input+inbound,Is.LessThanOrEqualTo(v.InputCapacity));
                Assert.That(v.Raw+v.Input+v.Output+v.Stored+v.Deliveries.Count+(v.Processing?1:0),Is.EqualTo(4));
                Assert.That(v.Gas,Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(f.CreateView().Stored,Is.EqualTo(4));
        }
        [Test] public void ViewCollectionsCannotMutateTheSimulation()
        {
            var f=new HealthFactoryFacade();var v=f.CreateView();
            Assert.Throws<NotSupportedException>(()=>((System.Collections.Generic.IList<bool>)v.Routes)[0]=false);
            Assert.That(f.CreateView().Routes[0],Is.True);
        }
    }
}
