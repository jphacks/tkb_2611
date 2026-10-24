using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageSceneAuthoringTests
    {
        [Test] public void IceSurfaceFitsEntireBoardAndHasThinWorldHeight()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/ProductionVillageHome.unity",OpenSceneMode.Additive);
            try
            {
                var plot=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="VillageIcePlot");
                var rs=plot.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                Assert.That(b.size.x,Is.EqualTo(16.2f).Within(.05f));Assert.That(b.size.z,Is.EqualTo(16.2f).Within(.05f));Assert.That(b.size.y,Is.EqualTo(.3f).Within(.05f));Assert.That(b.max.y,Is.EqualTo(0).Within(.02f));
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
