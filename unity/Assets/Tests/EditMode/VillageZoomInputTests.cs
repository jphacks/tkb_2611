using System;
using NUnit.Framework;
namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageZoomInputTests
    {
        private static Type Policy => typeof(VillageZoomInputTests).Assembly.GetType("ProjectPenguin.Presentation.World.ProductionVillage.VillageZoomPolicy") ?? AppDomain.CurrentDomain.GetAssemblies().SelectType("ProjectPenguin.Presentation.World.ProductionVillage.VillageZoomPolicy");
        [Test] public void ZoomHysteresisRetainsStateBetweenThresholds()
        {
            Assert.That(Policy, Is.Not.Null, "カメラのズーム判定が必要です");
            var method = Policy.GetMethod("GroupActive");
            bool Active(float size, bool old) => (bool)method.Invoke(null,new object[]{size,10f,old,.9f,.8f});
            Assert.That(Active(9f,false),Is.True);
            Assert.That(Active(8.5f,true),Is.True);
            Assert.That(Active(8.5f,false),Is.False);
            Assert.That(Active(8f,true),Is.False);
            Assert.That(Active(float.NaN,true),Is.False);
        }
        [Test] public void IndustrialControlsAreAvailableWithPlacementTray()
        {
            var xml=System.Xml.Linq.XDocument.Load("Assets/UI/ProductionVillage/ProductionApp.uxml");
            foreach(var name in new[]{"villageZoomIn","villageZoomOut","villageFrameCamera","villageGroupState"})
                Assert.That(System.Linq.Enumerable.Any(xml.Descendants(),e=>(string)e.Attribute("name")==name),Is.True,name);
        }
        [Test] public void GestureWaitsForAllFingersAndBlocksUiOrigin()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectType("ProjectPenguin.Presentation.World.ProductionVillage.VillageGestureGate");
            Assert.That(type,Is.Not.Null,"配置を壊さないgesture gateが必要です");
            dynamic gate=Activator.CreateInstance(type);
            Assert.That((bool)gate.Observe(1,true,true),Is.True);
            Assert.That((bool)gate.Observe(2,false,true),Is.True);
            Assert.That((bool)gate.AllowPinch,Is.False);
            gate.Observe(0,false,true);
            Assert.That((bool)gate.Observe(2,false,true),Is.True);
            Assert.That((bool)gate.AllowPinch,Is.True);
            Assert.That((bool)gate.Observe(1,false,true),Is.True);
            gate.Observe(0,false,true);
            Assert.That((bool)gate.Observe(1,false,true),Is.False);
            gate.Interrupt();
            Assert.That((bool)gate.Observe(1,false,true),Is.True);
            gate.Observe(0,false,true);
            Assert.That((bool)gate.Observe(1,false,true),Is.False);
        }
    }
    internal static class ZoomTestTypes
    {
        internal static Type SelectType(this System.Reflection.Assembly[] assemblies,string name)
        {foreach(var assembly in assemblies){var type=assembly.GetType(name);if(type!=null)return type;}return null;}
    }
}

