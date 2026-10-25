using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
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
    public sealed class ProductionVillageSceneTests
    {
        [UnityTest] public IEnumerator StandaloneSceneUsesOneStateAndInspectorChangesReachUiAndWorld()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ProductionVillageHome.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var roots=SceneManager.GetActiveScene().GetRootGameObjects();
            var sessions=roots.SelectMany(g=>g.GetComponentsInChildren<ProductionVillageSession>()).ToArray();
            Assert.That(sessions.Length,Is.EqualTo(1));Assert.That(roots.SelectMany(g=>g.GetComponentsInChildren<GameStateSystem>()),Is.Empty);
            var s=sessions[0];Assert.That(s.Core.Error,Is.Empty);Assert.That(s.Core.View.Unlimited,Is.False);
            Assert.That(s.Core.BeginDevelopment(ProductionVillageSession.UtcNow),Is.True);
            Assert.That(s.Core.Configure(false,1,1500,1,false,ProductionVillageSession.UtcNow),Is.True);
            Assert.That(s.Core.Resources(37,200,91,false,ProductionVillageSession.UtcNow),Is.True);
            var control=s.GetComponent<ProductionVillageControl>();Assert.That(control.Core,Is.SameAs(s.Core));
            var world=s.GetComponent<VillageWorldPresenter>();var nav=roots.SelectMany(g=>g.GetComponentsInChildren<ProductionNavigator>()).Single();
            yield return new WaitForSecondsRealtime(.3f);
            var root=nav.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Label>(ProductionUiNames.Gas).text,Does.Contain("37"));
            Assert.That(root.Q<Label>(ProductionUiNames.Water).text,Does.Contain("91"));
            Assert.That(world.Renderer,Is.Not.Null);
            var showScan=typeof(ProductionNavigator).GetMethod("ShowScan",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(showScan,Is.Not.Null);showScan.Invoke(nav,null);
            Assert.That(root.Q<Button>("villageWaterAction"),Is.Not.Null);
            // ビルドに入るシーンに、スクショの読み取りとすれ違いがつながっていること。
            Assert.That(root.Q<Button>("villageScreenTimeAction")?.enabledSelf,Is.True);
            Assert.That(root.Q<Button>("villageFakeEncounterAction"),Is.Not.Null);
            Assert.That(s.GetComponent<VillageEncounters>(),Is.Not.Null);
            nav.Close();
            nav.ShowPlacement();world.Placement.MoveSelect();world.Placement.Select(1);world.Placement.Preview(18,18);
            var before=s.Core.View.Nodes.Single(n=>n.Id==1);world.Placement.CancelPreview();Assert.That(s.Core.View.Nodes.Single(n=>n.Id==1).X,Is.EqualTo(before.X));
            nav.Close();world.Placement.MoveSelect();world.Placement.Select(1);world.Placement.Preview(18,18);Assert.That(world.Placement.Confirm(),Is.True);
            Assert.That(s.Core.View.Nodes.Single(n=>n.Id==1).X,Is.EqualTo(18));
            Assert.That(s.Core.Inject(s.Core.View.Nodes.First(n=>n.Kind==FacilityKind.Warehouse).Id,Item.Salt,1,ProductionVillageSession.UtcNow),Is.True);
            world.Placement.Preview(20,20);nav.ShowSummary(true);Assert.That(world.Placement.Mode,Is.EqualTo(VillagePlacementMode.Browse));
            yield return null;
            var canvas=s.GetComponentsInChildren<Canvas>().Single();Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(),Is.Null);
            Assert.That(s.GetComponentsInChildren<UnityEngine.UI.Image>().All(i=>!i.raycastTarget),Is.True);
            LogAssert.NoUnexpectedReceived();
#else
            Assert.Ignore("Editor authored scene verification");yield break;
#endif
        }
    }
}
