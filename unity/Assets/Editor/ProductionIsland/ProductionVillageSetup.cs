using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectPenguin.Editor.ProductionIsland
{
    public static class ProductionVillageSetup
    {
        public const string ScenePath="Assets/Scenes/ProductionVillageHome.unity";
        private const string SourcePath="Assets/Scenes/ProductionIslandHome.unity";
        [MenuItem("Production Island/Create Production Village Home")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("再生を停止してください");
            for(var i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("未保存シーンを保護するため作成を停止しました。未保存の作業を保持してください");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new InvalidOperationException("統合シーンは既にあります。既存シーンを上書きしません");
            if(!AssetDatabase.CopyAsset(SourcePath,ScenePath))throw new InvalidOperationException("ホームを複製できませんでした");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var roots=scene.GetRootGameObjects();var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.enabled);
            foreach(var root in roots)
            {
                if(root.name=="AppUI"||root.name=="GameState"||root.name=="Island"){UnityEngine.Object.DestroyImmediate(root);continue;}
                foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(b==null)continue;
                    if(b is PenguinCircleWalker||b.GetType().Name=="AnimatorPhaseOffset"||b.GetType().Namespace=="UnityEngine.Rendering.Universal")continue;
                    UnityEngine.Object.DestroyImmediate(b);
                }
                if(root.name=="World")
                {
                    // 盤面の周りの海に浮き氷を散らし、元の氷山は背景へ下げる。箱形の氷山と小屋は使わない
                    var floes=new[]{new Vector3(11.2f,-.3f,-3f),new Vector3(-11.5f,-.3f,-6.5f),new Vector3(10.5f,-.3f,6f),new Vector3(-3f,-.3f,-12f),new Vector3(5.5f,-.3f,-11.5f),new Vector3(-10.8f,-.3f,1.5f)};
                    var floeScales=new[]{new Vector3(2.2f,.25f,1.8f),new Vector3(1.8f,.2f,1.5f),new Vector3(1.4f,.2f,1.2f),new Vector3(1.6f,.2f,1.3f),new Vector3(1.1f,.2f,1f),new Vector3(1.2f,.2f,1f)};
                    foreach(Transform t in root.transform)
                    {
                        if(t.name=="Icebergs"||t.name=="House")t.gameObject.SetActive(false);
                        if(t.name=="HomeIceberg")t.position=new Vector3(7.5f,-.35f,16.5f);
                        if(t.name!="IceFloes")continue;
                        t.gameObject.SetActive(true);var i=0;
                        foreach(Transform floe in t){if(i>=floes.Length)break;floe.position=floes[i];floe.localScale=floeScales[i];floe.rotation=Quaternion.Euler(0,i*37f,0);i++;}
                    }
                }
            }
            // 寄りと角度は VillageCameraController が画面の縦横比から決める
            var cameraController=camera.gameObject.AddComponent<VillageCameraController>();
            var runtime=new GameObject("VillageRuntime");runtime.AddComponent<ProductionVillageControl>();var session=runtime.AddComponent<ProductionVillageSession>();runtime.AddComponent<VillageHealthInput>();
            var world=runtime.AddComponent<VillageWorldPresenter>();
            var w=new SerializedObject(world);Set(w,"_session",session);Set(w,"_camera",camera);Set(w,"_harbor",Load<GameObject>("Assets/3Dmodel/Harbor_Stage4_v002/Harbor_Stage4_v002.prefab"));
            var ids=new[]{"Fishery_2x2","Preparation_2x2","Saltworks_2x2","KelpFarm_2x2","Kamaboko_2x2","Chikuwa_2x2","Drying_2x2","Oden_2x2","Warehouse_1x1","Dormitory_1x1","Research_2x2","Sorter_1x1","ElectricAmplifier_2x2","WaterAmplifier_2x2","GasAmplifier_2x2","IceHouse_1x1"};
            var array=w.FindProperty("_catalog").FindPropertyRelative("_entries");array.arraySize=ids.Length;
            for(var i=0;i<ids.Length;i++)
            {
                var name="Building_"+ids[i]+"_v002";var entry=array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_kind").enumValueIndex=i;entry.FindPropertyRelative("_prefab").objectReferenceValue=Load<GameObject>($"Assets/3Dmodel/{name}/{name}.prefab");
            }
            w.ApplyModifiedPropertiesWithoutUndo();
            var island=new GameObject("VillageIsland",typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(island,scene);
            var islandView=island.AddComponent<VillageIslandView>();var iv=new SerializedObject(islandView);Set(iv,"_material",Load<Material>("Assets/3Dmodel/ProductionIsland_v002/Running.mat"));iv.ApplyModifiedPropertiesWithoutUndo();
            var penguins=runtime.AddComponent<VillagePenguinsView>();var pv=new SerializedObject(penguins);Set(pv,"_session",session);
            Set(pv,"_template",roots.Where(g=>g!=null&&g.name=="Penguin_Waddle_1p3m").First(g=>g.GetComponent<Animator>()!=null));pv.ApplyModifiedPropertiesWithoutUndo();
            var app=new GameObject("AppUI");var doc=app.AddComponent<UIDocument>();
            const string panel="Assets/UI/ProductionVillage/PanelSettings.asset";if(AssetDatabase.LoadAssetAtPath<PanelSettings>(panel)==null&&!AssetDatabase.CopyAsset("Assets/UI/PanelSettings.asset",panel))throw new InvalidOperationException("PanelSettingsを複製できませんでした");
            doc.panelSettings=Load<PanelSettings>(panel);doc.visualTreeAsset=Load<VisualTreeAsset>("Assets/UI/ProductionVillage/ProductionApp.uxml");app.AddComponent<SafeAreaApplier>();
            var nav=app.AddComponent<ProductionNavigator>();var n=new SerializedObject(nav);Set(n,"_session",session);Set(n,"_world",world);
            n.ApplyModifiedPropertiesWithoutUndo();
            var input=runtime.AddComponent<VillagePointerInput>();var p=new SerializedObject(input);Set(p,"_session",session);Set(p,"_navigator",nav);Set(p,"_camera",cameraController);p.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("統合シーンを保存できませんでした");
            // All prior scenes were checked clean. Close them without saving; only the copy runs.
            for(var i=SceneManager.sceneCount-1;i>=0;i--){var other=SceneManager.GetSceneAt(i);if(other!=scene)EditorSceneManager.CloseScene(other,true);}
            Selection.activeGameObject=runtime;
        }
        private static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("参照がありません: "+path);
        private static void Set(SerializedObject o,string name,UnityEngine.Object value)=>o.FindProperty(name).objectReferenceValue=value;
    }
}
