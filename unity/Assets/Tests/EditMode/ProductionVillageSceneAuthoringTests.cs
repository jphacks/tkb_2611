using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ProductionVillageSceneAuthoringTests
    {
        private static float BoardHalf => VillageWorldRenderer.Cell * 12;

        [Test] public void SceneHasIslandWithMaterialAndWalkingPenguins()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/ProductionVillageHome.unity",OpenSceneMode.Additive);
            try
            {
                var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToList();
                Assert.That(all.Any(t=>t.name=="VillageIcePlot"||t.name=="VillageIceSurface"),Is.False,"古い引き伸ばした板が残っています");
                var island=all.Select(t=>t.GetComponent<VillageIslandView>()).Single(v=>v!=null);
                Assert.That(new UnityEditor.SerializedObject(island).FindProperty("_material").objectReferenceValue,Is.Not.Null,"島の材質 (色見本) が未設定です");
                Assert.That(island.transform.position,Is.EqualTo(Vector3.zero));
                var penguins=all.Select(t=>t.GetComponent<VillagePenguinsView>()).Single(v=>v!=null);
                Assert.That(new UnityEditor.SerializedObject(penguins).FindProperty("_template").objectReferenceValue,Is.Not.Null);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void IslandOutlineCoversEveryBoardCornerWithMargin()
        {
            var outline=VillageIslandView.OutlineOf(BoardHalf);
            foreach(var corner in new[]{new Vector2(1,1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(-1,-1)})
                Assert.That(Inside(outline,corner*BoardHalf),Is.True,$"盤面の角 {corner} が島の外です");
            // 縁はどこも盤面の外側にある (盤面の端のマスが海に落ちない)
            foreach(var p in outline)
                Assert.That(Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.z)),Is.GreaterThanOrEqualTo(BoardHalf),$"縁の点 {p} が盤面に食い込んでいます");
        }

        [Test] public void IslandMeshTopIsAtGroundAndCliffReachesUnderTheSea()
        {
            var mesh=VillageIslandView.BuildMesh(VillageIslandView.OutlineOf(BoardHalf));
            Assert.That(mesh.bounds.max.y,Is.EqualTo(0).Within(.001f),"上面は地面 (y=0) にそろえる。施設と選択枠がここに載る");
            Assert.That(mesh.bounds.min.y,Is.LessThan(-.35f),"崖の根元は海面 (y=-0.35) より下");
            var up=mesh.normals.Count(n=>n.y>.99f);
            Assert.That(up,Is.GreaterThan(VillageIslandView.OutlineOf(BoardHalf).Length),"上面の法線は真上を向く");
        }

        private static bool Inside(IReadOnlyList<Vector3> polygon,Vector2 p)
        {
            var inside=false;
            for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
            {
                var a=polygon[i];var b=polygon[j];
                if((a.z>p.y)!=(b.z>p.y)&&p.x<(b.x-a.x)*(p.y-a.z)/(b.z-a.z)+a.x)inside=!inside;
            }
            return inside;
        }
    }
}
