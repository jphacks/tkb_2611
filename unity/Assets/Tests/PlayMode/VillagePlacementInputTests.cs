using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class VillagePlacementInputTests
    {
        private static IEnumerator Click(Mouse mouse,Vector2 p)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return null;yield return null;
        }
        [UnityTest] public IEnumerator RealPointerSelectsPreviewsAndNeverCommitsUntilConfirmation()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ProductionVillageHome.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
            var roots=SceneManager.GetActiveScene().GetRootGameObjects();var s=roots.SelectMany(g=>g.GetComponentsInChildren<ProductionVillageSession>()).Single();s.Core.BeginDevelopment(ProductionVillageSession.UtcNow);s.Core.ResetDevelopment(ProductionVillageSession.UtcNow);s.Core.Configure(false,1,1500,1,false,ProductionVillageSession.UtcNow);
            var nav=roots.SelectMany(g=>g.GetComponentsInChildren<ProductionNavigator>()).Single();var w=s.GetComponent<VillageWorldPresenter>();nav.Close();w.Placement.MoveSelect();yield return null;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                var n=s.Core.View.Nodes[0];var original=n.X;var point=w.ViewCamera.WorldToScreenPoint(VillageWorldRenderer.Position(n.X,n.Y,n.Size));
                Assert.That(nav.Blocks(point),Is.False);yield return Click(mouse,point);
                Assert.That(w.Placement.Mode,Is.EqualTo(VillagePlacementMode.MovePreview));
                var target=w.ViewCamera.WorldToScreenPoint(VillageWorldRenderer.Position(17,16,n.Size));Assert.That(nav.Blocks(target),Is.False);yield return Click(mouse,target);
                Assert.That(w.Placement.HasCandidate,Is.True);Assert.That(s.Core.View.Nodes[0].X,Is.EqualTo(original));
                w.Placement.CancelPreview();Assert.That(s.Core.View.Nodes[0].X,Is.EqualTo(original));
                // Header touch cannot select a facility or place a preview.
                yield return Click(mouse,new Vector2(Screen.width*.5f,Screen.height*.94f));Assert.That(w.Placement.Mode,Is.EqualTo(VillagePlacementMode.MoveSelect));
                Assert.That(w.Placement.HasCandidate,Is.False);
                yield return Click(mouse,point);yield return Click(mouse,target);Assert.That(w.Placement.Confirm(),Is.True);
                Assert.That(s.Core.View.Nodes[0].X,Is.EqualTo(17));Assert.That(s.Core.View.Nodes[0].Y,Is.EqualTo(16));
                // Navigating to another screen cancels an uncommitted preview.
                w.Placement.Select(n.Id);w.Placement.Preview(20,20);nav.ShowSummary(false);Assert.That(w.Placement.Mode,Is.EqualTo(VillagePlacementMode.Browse));
                LogAssert.NoUnexpectedReceived();
            }
            finally{InputSystem.RemoveDevice(mouse);}
#else
            Assert.Ignore("Editor input verification");yield break;
#endif
        }
    }
}
