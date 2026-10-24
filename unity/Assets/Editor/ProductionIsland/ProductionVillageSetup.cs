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
                    foreach(Transform t in root.transform)
                    {
                        if(t.name=="IceFloes"||t.name=="House")t.gameObject.SetActive(false);
                        if(t.name=="HomeIceberg")t.position=new Vector3(-5,-.35f,11);
                    }
                }
            }
            camera.orthographic=true;camera.orthographicSize=14;camera.transform.position=new Vector3(0,24,-22);camera.transform.LookAt(new Vector3(0,0,1));
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
            var plot=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/3Dmodel/Tile_Ice_Core_v001/Tile_Ice_Core_v001.prefab"),scene);plot.name="VillageIcePlot";
            foreach(var b in plot.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(b);
            NormalizePlot(plot);
            var app=new GameObject("AppUI");var doc=app.AddComponent<UIDocument>();
            const string panel="Assets/UI/ProductionVillage/PanelSettings.asset";if(AssetDatabase.LoadAssetAtPath<PanelSettings>(panel)==null&&!AssetDatabase.CopyAsset("Assets/UI/PanelSettings.asset",panel))throw new InvalidOperationException("PanelSettingsを複製できませんでした");
            doc.panelSettings=Load<PanelSettings>(panel);doc.visualTreeAsset=Load<VisualTreeAsset>("Assets/UI/ProductionVillage/ProductionApp.uxml");app.AddComponent<SafeAreaApplier>();
            var nav=app.AddComponent<ProductionNavigator>();var n=new SerializedObject(nav);Set(n,"_session",session);Set(n,"_world",world);
            var thumbnails=n.FindProperty("_thumbnails");thumbnails.arraySize=ids.Length;
            for(var i=0;i<ids.Length;i++){var e=thumbnails.GetArrayElementAtIndex(i);e.FindPropertyRelative("_kind").enumValueIndex=i;e.FindPropertyRelative("_texture").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Thumbnails/Thumb_"+((FacilityKind)i)+".png");}
            n.ApplyModifiedPropertiesWithoutUndo();
            var input=runtime.AddComponent<VillagePointerInput>();var p=new SerializedObject(input);Set(p,"_session",session);Set(p,"_navigator",nav);Set(p,"_camera",cameraController);p.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("統合シーンを保存できませんでした");
            // All prior scenes were checked clean. Close them without saving; only the copy runs.
            for(var i=SceneManager.sceneCount-1;i>=0;i--){var other=SceneManager.GetSceneAt(i);if(other!=scene)EditorSceneManager.CloseScene(other,true);}
            Selection.activeGameObject=runtime;
        }
        private static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("参照がありません: "+path);
        private static void Set(SerializedObject o,string name,UnityEngine.Object value)=>o.FindProperty(name).objectReferenceValue=value;
        public static void NormalizePlot(GameObject plot)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(plot);
            plot.transform.SetLocalPositionAndRotation(Vector3.zero,source.transform.localRotation);plot.transform.localScale=source.transform.localScale;
            var holder=new GameObject("VillageIceSurface");SceneManager.MoveGameObjectToScene(holder,plot.scene);plot.transform.SetParent(holder.transform,false);
            var renderers=plot.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            holder.transform.localScale=new Vector3(16.2f/bounds.size.x,.3f/Mathf.Max(.001f,bounds.size.y),16.2f/bounds.size.z);
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);holder.transform.position-=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
        }
    }
}
