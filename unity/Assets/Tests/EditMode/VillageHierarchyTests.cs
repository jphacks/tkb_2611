using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageHierarchyTests
    {
        internal static VillageGame Grid(int columns=4,int rows=4)
        {
            var game=new VillageGame(false);game.SetResources(100000,100000,100000);
            for(var y=0;y<rows;y++)for(var x=0;x<columns;x++)
                Assert.That(game.Place(FacilityKind.Fishery,x*2,y*2).Success,Is.True);
            return game;
        }
        private static void Unlock(VillageGame game)
        {
            var id=game.CreateView().Nodes[0].Id;
            Assert.That(game.Inject(id,Item.Surimi,5).Success,Is.True);
            Assert.That(game.Deliver(id,Item.Surimi,5,"unlock").Success,Is.True);
        }
        [Test] public void InitialTwoLevelsAndOnlySecondTierDeliveryUnlocksThird()
        {
            var game=Grid();game.AdvanceTo(30000);
            var view=game.CreateView();Assert.That(view.Rank,Is.EqualTo(1));Assert.That(view.MaximumZoom,Is.EqualTo(2));
            var id=view.Nodes[0].Id;
            if(view.Nodes[0].Stock.TryGetValue(Item.Fish,out var fish)&&fish>0)game.Deliver(id,Item.Fish,fish,"fish");
            Assert.That(game.CreateView().Rank,Is.EqualTo(1),"生産・建設・1次資源の納品では解放しない");
            Assert.That(game.Inject(id,Item.Surimi,4).Success,Is.True);game.Deliver(id,Item.Surimi,4,"four");
            Assert.That(game.CreateView().RankProgress,Is.EqualTo(.8f));Assert.That(game.MaximumZoom,Is.EqualTo(2));
            Assert.That(game.Inject(id,Item.DriedFish,1).Success,Is.True);game.Deliver(id,Item.DriedFish,1,"fifth");
            view=game.CreateView();Assert.That(view.SecondTierDelivered,Is.EqualTo(5));Assert.That(view.Rank,Is.EqualTo(2));Assert.That(view.MaximumZoom,Is.EqualTo(3));
        }
        [Test] public void EveryLevelGroupsTwoByTwoAndHitsOnlyItsOwnFacilities()
        {
            var game=Grid();var view=game.CreateView();
            Assert.That(view.UpperNodes.Count(n=>n.Formed&&n.Zoom==2),Is.EqualTo(4));
            Assert.That(view.AtZoom(2).Hit(1,1).Kind,Is.EqualTo(FacilityKind.Preparation));
            Assert.That(view.AtZoom(1).Hit(1,1).Kind,Is.EqualTo(FacilityKind.Fishery));
            Assert.That(view.AtZoom(3).Nodes,Is.Empty);
            Unlock(game);view=game.CreateView();var upper=view.AtZoom(3).Nodes.Single();
            Assert.That(upper.Kind,Is.EqualTo(FacilityKind.Kamaboko));Assert.That(upper.Size,Is.EqualTo(8));
            Assert.That(upper.Size/VillageHierarchyRules.BaseGridSize,Is.EqualTo(4));
            Assert.That(view.AtZoom(3).Hit(7,7).Id,Is.EqualTo(upper.Id));Assert.That(view.AtZoom(3).Hit(8,8),Is.Null);
            Assert.That(view.Nodes.Count,Is.EqualTo(16));Assert.That(view.AtZoom(2).Nodes.All(n=>n.Size==4),Is.True);
        }
        [Test] public void IncompleteMixedAndMisalignedBlocksDoNotCreateUpperFacilities()
        {
            var game=Grid(2,1);Assert.That(game.CreateView().UpperNodes,Is.Empty);
            game.Place(FacilityKind.Fishery,0,2);game.Place(FacilityKind.Saltworks,2,2);
            Assert.That(game.CreateView().UpperNodes,Is.Empty);
            var odd=new VillageGame(true);odd.Place(FacilityKind.Fishery,1,1);odd.Place(FacilityKind.Fishery,3,1);odd.Place(FacilityKind.Fishery,1,3);odd.Place(FacilityKind.Fishery,3,3);
            Assert.That(odd.CreateView().UpperNodes,Is.Empty);
        }
        [Test] public void UpperEffortAndComboAreIndependentAndConsumeActualChildStock()
        {
            var game=Grid(4,2);var view=game.CreateView();var upper=view.AtZoom(2).Nodes.First();
            Assert.That(upper.ComboCount,Is.EqualTo(1));Assert.That(upper.ComboMultiplier,Is.EqualTo(1.25));
            Assert.That(game.SetEffort(upper.Id,2).Success,Is.True);view=game.CreateView();
            Assert.That(view.Nodes.All(n=>n.Effort==0),Is.True);Assert.That(view.AtZoom(2).Nodes.First(n=>n.Id==upper.Id).Effort,Is.EqualTo(2));
            game.AdvanceTo(5000);var saved=game.Export();var job=saved.UpperNodes.Single(n=>n.Id==upper.Id);
            Assert.That(job.Finish,Is.GreaterThan(5000));Assert.That(job.Finish-job.Started,Is.EqualTo(2000));
            Assert.That(saved.Nodes.Sum(n=>n.Stock[(int)Item.Fish]),Is.EqualTo(2),"8個完成から上位2棟が各3個消費");
            Assert.That(game.SetEffort(upper.Id,0).Success,Is.True);Assert.That(game.Export().UpperNodes.Single(n=>n.Id==upper.Id).Finish,Is.EqualTo(job.Finish));
        }
        [Test] public void RetiringAGroupPreservesPaidBatchStockAndIdentity()
        {
            var game=Grid(2,2);game.AdvanceTo(5000);var upper=game.Export().UpperNodes.Single();
            Assert.That(upper.Finish,Is.GreaterThan(0));var child=game.CreateView().Nodes[0];
            Assert.That(game.Move(child.Id,12,12).Success,Is.True);Assert.That(game.CreateView().AtZoom(2).Nodes,Is.Empty);
            game=VillageGame.Restore(game.Export());game.AdvanceTo(upper.Finish+100);
            Assert.That(game.CreateView().UpperNodes.Single().Stock[Item.Surimi],Is.EqualTo(1));
            Assert.That(game.Deliver(upper.Id,Item.Surimi,1,"retired").Success,Is.True);
            game.Move(child.Id,child.X,child.Y);Assert.That(game.CreateView().AtZoom(2).Nodes.Single().Id,Is.EqualTo(upper.Id));
            Assert.That(game.Export().UpperNodes.Count,Is.EqualTo(1));
        }
        [Test] public void ZoomRoundTripsAndSplitTimePreserveInventoryAndProgress()
        {
            var a=Grid();Unlock(a);var b=VillageGame.Restore(a.Export());
            a.AdvanceTo(30000);
            for(var time=100;time<=30000;time+=100){b.CreateView().AtZoom(1);b.CreateView().AtZoom(2);b.CreateView().AtZoom(3);b.AdvanceTo(time);}
            Assert.That(b.CreateView().Totals,Is.EquivalentTo(a.CreateView().Totals));Assert.That(b.CreateView().Gas,Is.EqualTo(a.CreateView().Gas));
            Assert.That(b.Export().UpperNodes.Select(n=>n.Finish),Is.EqualTo(a.Export().UpperNodes.Select(n=>n.Finish)));
        }
        [Test] public void DeliveryIsAtomicAndIdempotentAcrossRestart()
        {
            var game=Grid(2,2);Unlock(game);var json=JsonUtility.ToJson(game.Export());
            game=VillageGame.Restore(JsonUtility.FromJson<VillageState>(json));var view=game.CreateView();
            Assert.That(game.Deliver(view.Nodes[0].Id,Item.Surimi,5,"unlock").Success,Is.False);
            Assert.That(view.Rank,Is.EqualTo(2));Assert.That(game.CreateView().SecondTierDelivered,Is.EqualTo(5));
            foreach(var count in new[]{0,-1,int.MaxValue})Assert.That(game.Deliver(view.Nodes[0].Id,Item.Fish,count,"bad").Success,Is.False);
            var state=game.Export();state.SecondTierDelivered=-1;Assert.Throws<ArgumentException>(()=>VillageGame.Restore(state));
            state=game.Export();state.UpperNodes[0].Stock[0]=12;state.UpperNodes[0].Stock[1]=12;Assert.Throws<ArgumentException>(()=>VillageGame.Restore(state));
        }
        private sealed class Store:IProductionVillageStore
        {
            public bool Fail;private string _json;
            public ProductionVillageSave Load()=>_json==null?null:JsonUtility.FromJson<ProductionVillageSave>(_json);
            public void Save(ProductionVillageSave save){if(Fail)throw new IOException();_json=JsonUtility.ToJson(save);}
        }
        [Test] public void FailedDeliverySavePreservesStockRankAndAllowsSameRequestToRetry()
        {
            var store=new Store();var core=new ProductionVillageSessionCore(store,new Store(),()=>{var game=Grid(2,2);game.Inject(1,Item.Surimi,5);return game;},true);core.Open(10000);
            store.Fail=true;Assert.That(core.Deliver(1,Item.Surimi,5,"retry",10000),Is.False);
            Assert.That(core.View.SecondTierDelivered,Is.Zero);Assert.That(core.View.Nodes[0].Stock[Item.Surimi],Is.EqualTo(5));
            store.Fail=false;Assert.That(core.Deliver(1,Item.Surimi,5,"retry",10000),Is.True);
            core.Open(10000);Assert.That(core.View.MaximumZoom,Is.EqualTo(3));Assert.That(core.Deliver(1,Item.Surimi,5,"retry",10000),Is.False);
        }
        [Test] public void LegacyJsonWithoutNewFieldsStartsWithTwoZoomLevels()
        {
            var state=Grid(2,1).Export();var json=JsonUtility.ToJson(state);
            json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"(?:DeliveryPoints|SecondTierDelivered)\\\":0,","");
            var restored=VillageGame.Restore(JsonUtility.FromJson<VillageState>(json));
            Assert.That(restored.CreateView().Rank,Is.EqualTo(1));Assert.That(restored.MaximumZoom,Is.EqualTo(2));
        }
        [Test] public void ThirdCameraLevelHonorsUnlockAndPinchStillSwitchesOnlyOnce()
        {
            var go=new GameObject("HierarchyCamera",typeof(Camera),typeof(VillageCameraController));
            try
            {
                var camera=go.GetComponent<VillageCameraController>();camera.Frame();camera.SetLevel(VillageZoomLevel.District);Assert.That(camera.Level,Is.EqualTo(VillageZoomLevel.Overview));
                camera.SetMaximumZoom(3);camera.SetLevel(VillageZoomLevel.District);Assert.That(camera.Level,Is.EqualTo(VillageZoomLevel.District));
                camera.SetMaximumZoom(2);Assert.That(camera.Level,Is.EqualTo(VillageZoomLevel.Overview));
                var pinch=new VillagePinch();pinch.Observe(100,VillageZoomLevel.Overview,out _,3);
                Assert.That(pinch.Observe(80,VillageZoomLevel.Overview,out var next,3),Is.True);Assert.That(next,Is.EqualTo(VillageZoomLevel.District));
                Assert.That(pinch.Observe(50,next,out _,3),Is.False);
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void RendererReplacesModelsAndSelectionWhenChangingLayers()
        {
            var go=new GameObject("HierarchyRenderer");
            try
            {
                var game=Grid(2,2);var renderer=new VillageWorldRenderer(go.transform,null,new VillagePrefabCatalog(),null);
                var view=game.CreateView();renderer.Render(view,view.Nodes[0].Id);renderer.RenderLayerGrid(view,1);
                var upper=view.AtZoom(2).Nodes.Single();renderer.Render(view.AtZoom(2),upper.Id);renderer.RenderLayerGrid(view,2);
                Assert.That(go.transform.Find(ProductionWorldNames.Facility+view.Nodes[0].Id),Is.Null);
                Assert.That(go.transform.Find(ProductionWorldNames.Facility+upper.Id),Is.Not.Null);
                var selection=go.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(i=>i.name==ProductionWorldNames.Selection);
                Assert.That(selection.rectTransform.sizeDelta.x,Is.EqualTo(4*VillageWorldRenderer.Cell));
                renderer.Render(view,0);Assert.That(go.transform.Find(ProductionWorldNames.Facility+upper.Id),Is.Null);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
