using System;
using NUnit.Framework;
using ProjectPenguin.Domain.LogisticsPrototype;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class LogisticsChainTests
    {
        [Test] public void DispatchRemovesExactlyOneAndArrivalIsDelayed()
        {
            var c = new LogisticsChain(); c.Advance(.05);
            Assert.That(c.Raw, Is.EqualTo(11)); Assert.That(c.InTransit, Is.EqualTo(1));
            c.Advance(1); Assert.That(c.Input, Is.Zero);
            c.Advance(.5); Assert.That(c.Input, Is.EqualTo(1)); Assert.That(c.Processing, Is.False);
        }
        [Test] public void ProcessingConsumesOnceAndOutputNeedsSeparateDelivery()
        {
            var c = new LogisticsChain(raw: 1);
            c.Advance(.05); c.Advance(1.5); c.Advance(.05);
            Assert.That(c.Input, Is.Zero); Assert.That(c.Processing, Is.True);
            c.Advance(1); Assert.That(c.Output, Is.Zero);
            c.Advance(1); Assert.That(c.Output, Is.EqualTo(1)); Assert.That(c.Stored, Is.Zero);
            c.Advance(.05); Assert.That(c.Output, Is.Zero); Assert.That(c.InTransit, Is.EqualTo(1));
            c.Advance(1.5); Assert.That(c.Stored, Is.EqualTo(1)); Assert.That(c.Raw, Is.Zero);
        }
        [Test] public void RejectsInvalidSelfReverseAndDuplicateConnections()
        {
            var c = new LogisticsChain(); c.DisconnectAll();
            Assert.That(c.Connect(0, 0), Is.False); Assert.That(c.Connect(0, 2), Is.False);
            Assert.That(c.Connect(2, 1), Is.False); Assert.That(c.Connect(-1, 1), Is.False);
            Assert.That(c.Connect(0, 1), Is.True); Assert.That(c.Connect(0, 1), Is.False);
        }
        [Test] public void HugeDeltaCannotCrossWholeChain()
        {
            var c = new LogisticsChain(); c.Advance(10000);
            Assert.That(c.Stored, Is.Zero); Assert.That(c.Output, Is.Zero);
            Assert.That(c.Raw, Is.EqualTo(11)); Assert.That(c.InTransit, Is.EqualTo(1));
        }
        [Test] public void DisconnectPreservesAlreadyDispatchedCargo()
        {
            var c = new LogisticsChain(raw: 1); c.Advance(.05); c.DisconnectAll(); c.Advance(2);
            Assert.That(c.Input, Is.EqualTo(1)); Assert.That(c.InTransit, Is.Zero);
            Assert.That(c.Connected(0), Is.False);
        }
        [Test] public void FullWarehouseBackpressureReservesAllCapacitiesAndConservesGoods()
        {
            var c = new LogisticsChain(raw: 30, warehouseCapacity: 1);
            for (var i=0;i<4000;i++)
            {
                c.Advance(.05);
                Assert.That(c.Raw, Is.GreaterThanOrEqualTo(0));
                Assert.That(c.Input + (c.Flying(0) ? 1 : 0), Is.LessThanOrEqualTo(2));
                Assert.That(c.Output + (c.Processing ? 1 : 0), Is.LessThanOrEqualTo(6));
                Assert.That(c.Stored + (c.Flying(1) ? 1 : 0), Is.LessThanOrEqualTo(1));
                Assert.That(c.Raw+c.Input+c.Output+c.Stored+c.InTransit+(c.Processing?1:0), Is.EqualTo(30));
            }
            Assert.That(c.Stored, Is.EqualTo(1)); Assert.That(c.Output, Is.EqualTo(6));
        }
        [Test] public void DisconnectedChainDoesNotConsumeStock()
        {
            var c = new LogisticsChain(); c.DisconnectAll(); c.Advance(100);
            Assert.That(c.Raw, Is.EqualTo(12)); Assert.That(c.InTransit, Is.Zero);
        }
        [Test] public void InvalidTimesAreRejectedBeforeMutatingState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LogisticsChain(0));
            var c = new LogisticsChain(); Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(-1));
            c.Advance(0); Assert.That(c.Raw, Is.EqualTo(12));
        }
    }
}
