using System;
using NUnit.Framework;
using ProjectPenguin.Systems;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageInspectorDebugTests
    {
        // Removing the debug initial factory must prevent an Inspector layout from being launched.
        [Test] public void InspectorLayoutHasAnIndependentInitialFactory()
        {
            var factory=typeof(ProductionVillageControl).GetMethod("CreateInspectorDebugInitial");
            Assert.That(factory,Is.Not.Null,"Inspector配置を生成する独立した入口が必要です");
        }
        [Test] public void InitialLayoutPreservesResourcesAndStockWithoutConstructionCost()
        {
            var settings=new VillageInspectorDebugSettings{Gas=7,Electricity=0,Water=0};
            settings.Facilities[0].Stock=new[]{new VillageInspectorStock(Item.Fish,3)};
            var view=settings.CreateInitial().CreateView();
            Assert.That(view.Nodes.Count,Is.EqualTo(4));Assert.That(view.Gas,Is.EqualTo(7));Assert.That(view.Electricity,Is.Zero);
            Assert.That(view.Nodes[0].Stock[Item.Fish],Is.EqualTo(3));Assert.That(view.Nodes[1].X,Is.EqualTo(10));
        }
        [Test] public void InvalidLayoutAndOptionsAreRejectedBeforeLaunch()
        {
            var s=new VillageInspectorDebugSettings();s.Facilities[1].Cell=s.Facilities[0].Cell;Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Cell=new Vector2Int(23,0);Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Kind=(FacilityKind)999;Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Stock=new[]{new VillageInspectorStock(Item.Fish,13)};Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Stock=new[]{new VillageInspectorStock(Item.Fish,-1)};Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Stock=new[]{new VillageInspectorStock((Item)999,0)};Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings();s.Facilities[0].Stock=new[]{new VillageInspectorStock(Item.Fish,1),new VillageInspectorStock(Item.Fish,1)};Assert.Catch(()=>s.CreateInitial());
            foreach(var bad in new[]{double.NaN,double.PositiveInfinity,-1,101}){s=new VillageInspectorDebugSettings{TimeScale=bad};Assert.Catch(()=>s.CreateInitial());}
            s=new VillageInspectorDebugSettings{RecipeSpeed=0};Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings{DeliveryMs=99};Assert.Catch(()=>s.CreateInitial());
            s=new VillageInspectorDebugSettings{Gas=double.MaxValue};Assert.Catch(()=>s.CreateInitial());
        }
        [Test] public void MemorySavesAreDetachedIndependentAndValidateProfiles()
        {
            var first=new ProductionVillageMemoryStore();var second=new ProductionVillageMemoryStore();
            var save=new ProductionVillageSave{State=new VillageInspectorDebugSettings().CreateInitial().Export(),UtcMs=10000};
            first.Save(save);save.State.Nodes.Clear();
            Assert.That(first.Load().State.Nodes.Count,Is.EqualTo(4));Assert.That(second.Load(),Is.Null);
            var loaded=first.Load();loaded.State.Nodes[0].Stock[0]=12;Assert.That(first.Load().State.Nodes[0].Stock[0],Is.Zero);
            loaded.Profile="development";Assert.Catch(()=>first.Save(loaded));Assert.That(first.Load().Profile,Is.EqualTo("production"));
        }
        [Test] public void EveryPresetFormsItsNamedGroupAndChikuwaProducesAfterTwentySimulationMinutes()
        {
            foreach(var preset in new[]{VillageInspectorPreset.Chikuwa,VillageInspectorPreset.Kamaboko,VillageInspectorPreset.DriedFish})
            {
                var s=new VillageInspectorDebugSettings();s.ApplyPreset(preset);var g=s.CreateInitial();
                Assert.That(g.CreateView().Synergies.Count,Is.EqualTo(1));
                Assert.That(g.CreateView().Synergies[0].BonusItem,Is.EqualTo(preset==VillageInspectorPreset.Chikuwa?Item.Chikuwa:preset==VillageInspectorPreset.Kamaboko?Item.Kamaboko:Item.DriedFish));
                if(preset!=VillageInspectorPreset.Chikuwa)continue;
                g.Configure(s.Unlimited,s.RecipeSpeed,s.DeliveryMs);g.SetGroupViewActive(true);g.AdvanceTo(1200000);
                Assert.That(g.CreateView().Totals[Item.Chikuwa],Is.EqualTo(1));
            }
        }
        [Test] public void ReleaseGuardRefusesInspectorFlagAndNormalInitialFactoryStaysUntouched()
        {
            Assert.That(ProductionVillageSession.ShouldUseInspectorDebugLayout(false,true),Is.False);
            Assert.That(ProductionVillageSession.ShouldUseInspectorDebugLayout(true,false),Is.False);
            Assert.That(ProductionVillageSession.ShouldUseInspectorDebugLayout(true,true),Is.True);
            var owner=new GameObject("Inspector debug factory test");owner.SetActive(false);
            try{var control=owner.AddComponent<ProductionVillageControl>();control.InspectorDebugSettings.ApplyPreset(VillageInspectorPreset.DriedFish);
                Assert.That(control.CreateInitial().CreateView().Nodes.Select(n=>n.Kind),Is.EqualTo(new[]{FacilityKind.Fishery,FacilityKind.Preparation,FacilityKind.Warehouse}));}
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test] public void ReapplyKeepsCoreIdentityAndRejectsInvalidLayoutWithoutResettingProgress()
        {
            var owner=new GameObject("Inspector debug reapply test");owner.SetActive(false);
            try
            {
                var session=owner.AddComponent<ProductionVillageSession>();var control=owner.GetComponent<ProductionVillageControl>();control.InspectorDebugSettings.Enabled=true;
                typeof(ProductionVillageSession).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,null);
                Assert.That(session.UsingInspectorDebugLayout,Is.True);var original=session.Core;original.Tick(1000);
                control.InspectorDebugSettings.Facilities[1].Cell=control.InspectorDebugSettings.Facilities[0].Cell;
                Assert.That(session.ApplyInspectorDebugLayout(),Is.False);Assert.That(original.View.AtMs,Is.EqualTo(1000));Assert.That(session.Core,Is.SameAs(original));
                control.InspectorDebugSettings.ApplyPreset(VillageInspectorPreset.Kamaboko);control.InspectorDebugSettings.Gas=23;
                var applied=0;session.LayoutApplied+=()=>applied++;
                Assert.That(session.ApplyInspectorDebugLayout(),Is.True);Assert.That(session.Core,Is.SameAs(original));Assert.That(original.View.AtMs,Is.Zero);
                Assert.That(original.View.Gas,Is.EqualTo(23));Assert.That(applied,Is.EqualTo(1));Assert.That(original.View.Nodes[1].Kind,Is.EqualTo(FacilityKind.Saltworks));
                control.InspectorDebugSettings.Enabled=false;Assert.That(session.UsingInspectorDebugLayout,Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test] public void ChangingCheckboxCannotApplyToAnExistingNormalSession()
        {
            var owner=new GameObject("Inspector normal-session protection");owner.SetActive(false);
            try
            {
                var session=owner.AddComponent<ProductionVillageSession>();var control=owner.GetComponent<ProductionVillageControl>();
                var core=new ProductionVillageSessionCore(new ProductionVillageMemoryStore(),new ProductionVillageMemoryStore(true),control.CreateInitial,true);core.Open(10000);core.Tick(1000);
                typeof(ProductionVillageSession).GetField("<Core>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,core);
                control.InspectorDebugSettings.Enabled=true;
                Assert.That(session.ApplyInspectorDebugLayout(),Is.False);Assert.That(session.UsingInspectorDebugLayout,Is.False);
                Assert.That(core.View.AtMs,Is.EqualTo(1000));Assert.That(core.View.Nodes.Count,Is.EqualTo(3));Assert.That(core.Development,Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [Test] public void InvalidMemoryStartupCanBeCorrectedWithoutPersistentStorage()
        {
            var owner=new GameObject("Inspector invalid startup");owner.SetActive(false);
            try
            {
                var session=owner.AddComponent<ProductionVillageSession>();var s=owner.GetComponent<ProductionVillageControl>().InspectorDebugSettings;
                s.Enabled=true;s.Gas=-1;
                typeof(ProductionVillageSession).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,null);
                Assert.That(session.UsingInspectorDebugLayout,Is.True);Assert.That(session.Core.View,Is.Null);Assert.That(session.InspectorDebugError,Is.Not.Empty);
                s.Gas=8;Assert.That(session.ApplyInspectorDebugLayout(),Is.True);Assert.That(session.Core.View.Gas,Is.EqualTo(8));Assert.That(session.Core.Development,Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
    }
}
