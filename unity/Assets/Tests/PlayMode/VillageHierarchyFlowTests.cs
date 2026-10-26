using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class VillageHierarchyFlowTests
    {
        [UnityTest] public IEnumerator DeliveryUnlocksThirdLayerAndUpperDetailsShowIndependentEffortAndCombo()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ProductionVillageHome.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var session=Object.FindFirstObjectByType<ProductionVillageSession>();var core=session.Core;var utc=ProductionVillageSession.UtcNow;
            if(!core.Development)Assert.That(core.BeginDevelopment(utc),Is.True);
            Assert.That(core.Configure(false,1,1500,1,false,utc),Is.True);
            Assert.That(core.Resources(100000,100000,100000,false,utc),Is.True);
            var originX=-1;var originY=-1;
            for(var y=0;y<24&&originX<0;y+=8)
                for(var x=0;x<24&&originX<0;x+=8)
                {
                    var free=true;
                    for(var dy=0;dy<8;dy+=2)for(var dx=0;dx<8;dx+=2)
                        free&=core.CanPlace(FacilityKind.Fishery,x+dx,y+dy).Length==0;
                    if(free){originX=x;originY=y;}
                }
            Assert.That(originX,Is.GreaterThanOrEqualTo(0));
            for(var dy=0;dy<8;dy+=2)for(var dx=0;dx<8;dx+=2)Assert.That(core.Place(FacilityKind.Fishery,originX+dx,originY+dy,utc),Is.True);
            var world=Object.FindFirstObjectByType<VillageWorldPresenter>();var nav=Object.FindFirstObjectByType<ProductionNavigator>();
            var root=nav.GetComponent<UIDocument>().rootVisualElement;nav.SetIndustrialMode(true);nav.Close();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(root.Q<Button>(ProductionUiNames.ZoomDistrict).enabledSelf,Is.False);
            var upper=core.View.AtZoom(2).Nodes.First(n=>n.X==originX&&n.Y==originY);
            nav.ShowFacility(upper.Id);yield return null;
            Assert.That(world.CameraController.Level,Is.EqualTo(VillageZoomLevel.Overview));
            Assert.That(root.Q<Label>(VillageComboPanel.SummaryName).text,Does.Contain("1.5"));
            Click(root.Q<Button>(VillageEffortPanel.IncreaseButtonName));yield return null;
            Assert.That(core.View.UpperNodes.Single(n=>n.Id==upper.Id).Effort,Is.EqualTo(1));
            Assert.That(core.View.Nodes.All(n=>n.Effort==0),Is.True);
            var child=core.View.Nodes.First(n=>n.X==originX&&n.Y==originY);
            Assert.That(core.Inject(child.Id,Item.Surimi,5,utc),Is.True);
            nav.ShowSummary(true);yield return null;
            Click(root.Q<Button>(ProductionNavigator.DeliveryButtonName(child.Id,Item.Surimi)));yield return null;
            Assert.That(core.View.Rank,Is.EqualTo(2));Assert.That(core.View.MaximumZoom,Is.EqualTo(3));
            Assert.That(core.View.Nodes.Single(n=>n.Id==child.Id).Stock[Item.Surimi],Is.Zero);
            nav.Close();yield return new WaitForSecondsRealtime(.2f);
            Click(root.Q<Button>(ProductionUiNames.ZoomDistrict));yield return new WaitForSecondsRealtime(.25f);
            Assert.That(world.CameraController.Level,Is.EqualTo(VillageZoomLevel.District));
            Assert.That(world.VisibleView.Nodes.Any(n=>n.Zoom!=3),Is.False);
            var grand=world.VisibleView.Hit(originX+7,originY+7);Assert.That(grand.Kind,Is.EqualTo(FacilityKind.Kamaboko));
            CaptureWorld(world.ViewCamera);yield return null;
            nav.ShowFacility(grand.Id);yield return null;
            Assert.That(root.Query<Label>(className:ProductionUiNames.FacilityEffort).First().text,Does.Contain("エフォート 0"));
            Assert.That(root.Q<Label>(VillageComboPanel.SummaryName).text,Does.Contain("隣接 0棟"));
            nav.Close();world.Placement.Build(FacilityKind.Fishery);yield return null;
            Assert.That(world.CameraController.Level,Is.EqualTo(VillageZoomLevel.Detail));
            Assert.That(world.Placement.Mode,Is.EqualTo(VillagePlacementMode.Build));world.Placement.Cancel();
            Assert.That(core.Save(utc),Is.True);core.Open(utc);Assert.That(core.View.MaximumZoom,Is.EqualTo(3));
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Editor scene verification");yield break;
#endif
        }
        private static void CaptureWorld(Camera camera)
        {
            var target=new RenderTexture(800,600,24);var texture=new Texture2D(800,600,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,800,600),0,0);texture.Apply();
                System.IO.File.WriteAllBytes("/tmp/village-zoom-three.png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;Object.Destroy(target);Object.Destroy(texture);}
        }
        private static void Click(Button button)
        {
            Assert.That(button,Is.Not.Null);Assert.That(button.enabledInHierarchy,Is.True);
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}
        }
    }
}
