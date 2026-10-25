using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.World.ProductionVillage;
namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageZoomInputTests
    {
        [Test] public void PinchSwitchesOnlyAfterCrossingThreshold()
        {
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Overview,1.1f),Is.EqualTo(VillageZoomLevel.Overview));
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Overview,VillageZoomPolicy.SpreadToDetail),Is.EqualTo(VillageZoomLevel.Detail));
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Overview,.5f),Is.EqualTo(VillageZoomLevel.Overview),"全体からさらに縮めても変わらない");
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Detail,.9f),Is.EqualTo(VillageZoomLevel.Detail));
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Detail,VillageZoomPolicy.SpreadToOverview),Is.EqualTo(VillageZoomLevel.Overview));
            Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Detail,3f),Is.EqualTo(VillageZoomLevel.Detail),"詳細からさらに広げても変わらない");
            foreach(var bad in new[]{float.NaN,float.PositiveInfinity,0f,-1f})
                Assert.That(VillageZoomPolicy.AfterPinch(VillageZoomLevel.Overview,bad),Is.EqualTo(VillageZoomLevel.Overview),bad.ToString());
        }
        [Test] public void OnePinchSwitchesAtMostOnce()
        {
            var pinch=new VillagePinch();
            var level=VillageZoomLevel.Overview;
            Assert.That(pinch.Observe(100,level,out _),Is.False,"つまみ始めは基準を記録するだけ");
            Assert.That(pinch.Observe(110,level,out _),Is.False);
            Assert.That(pinch.Observe(130,level,out var next),Is.True);
            Assert.That(next,Is.EqualTo(VillageZoomLevel.Detail));
            level=next;
            Assert.That(pinch.Observe(40,level,out _),Is.False,"同じつまみの中では戻らない");
            pinch.Reset();
            Assert.That(pinch.Observe(100,level,out _),Is.False);
            Assert.That(pinch.Observe(80,level,out next),Is.True,"指を離して次のつまみなら全体へ戻れる");
            Assert.That(next,Is.EqualTo(VillageZoomLevel.Overview));
            Assert.That(pinch.Observe(float.NaN,level,out _),Is.False);
        }
        [Test] public void WheelMovesOneLevelPerDirection()
        {
            Assert.That(VillageZoomPolicy.AfterWheel(VillageZoomLevel.Overview,120),Is.EqualTo(VillageZoomLevel.Detail));
            Assert.That(VillageZoomPolicy.AfterWheel(VillageZoomLevel.Detail,-120),Is.EqualTo(VillageZoomLevel.Overview));
            Assert.That(VillageZoomPolicy.AfterWheel(VillageZoomLevel.Detail,.05f),Is.EqualTo(VillageZoomLevel.Detail));
            Assert.That(VillageZoomPolicy.AfterWheel(VillageZoomLevel.Overview,float.NaN),Is.EqualTo(VillageZoomLevel.Overview));
        }
        [Test] public void EaseStartsFastAndLandsExactly()
        {
            Assert.That(VillageZoomPolicy.Ease(0),Is.EqualTo(0));
            Assert.That(VillageZoomPolicy.Ease(1),Is.EqualTo(1));
            Assert.That(VillageZoomPolicy.Ease(.5f),Is.GreaterThan(.5f));
            Assert.That(VillageZoomPolicy.Ease(2),Is.EqualTo(1));
            Assert.That(VillageZoomPolicy.Ease(float.NaN),Is.EqualTo(1));
        }
        [Test] public void IndustrialControlsAreAvailableWithPlacementTray()
        {
            var xml=System.Xml.Linq.XDocument.Load("Assets/UI/ProductionVillage/ProductionApp.uxml");
            foreach(var name in new[]{"villageZoomOverview","villageZoomDetail","villageGroupState"})
                Assert.That(xml.Descendants().Any(e=>(string)e.Attribute("name")==name),Is.True,name);
        }
        [Test] public void GestureWaitsForAllFingersAndBlocksUiOrigin()
        {
            var gate=new VillageGestureGate();
            Assert.That(gate.Observe(1,true,true),Is.True);
            Assert.That(gate.Observe(2,false,true),Is.True);
            Assert.That(gate.AllowPinch,Is.False);
            gate.Observe(0,false,true);
            Assert.That(gate.Observe(2,false,true),Is.True);
            Assert.That(gate.AllowPinch,Is.True);
            Assert.That(gate.Observe(1,false,true),Is.True);
            gate.Observe(0,false,true);
            Assert.That(gate.Observe(1,false,true),Is.False);
            gate.Interrupt();
            Assert.That(gate.Observe(1,false,true),Is.True);
            gate.Observe(0,false,true);
            Assert.That(gate.Observe(1,false,true),Is.False);
        }
    }
}
