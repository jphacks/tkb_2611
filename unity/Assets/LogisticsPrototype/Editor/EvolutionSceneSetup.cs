using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.LogisticsPrototype.Editor
{
    public static class EvolutionSceneSetup
    {
        public const string ScenePath="Assets/Scenes/LogisticsEvolutionPrototype.unity";
        [MenuItem("Production Island/Logistics Prototype/Setup Evolution Copied Scene")]
        public static void Setup()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath||Application.isPlaying)throw new InvalidOperationException("Only the copied evolution scene can be configured");
            if(scene.GetRootGameObjects().Any(g=>g.GetComponent<EvolutionDriver>()!=null))throw new InvalidOperationException("Already configured");
            var old=scene.GetRootGameObjects().Single(g=>g.GetComponent<LogisticsPrototypeController>()!=null);old.SetActive(false);
            var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=26;
            camera.transform.position=new Vector3(0,28,-20);camera.transform.LookAt(Vector3.zero);
            var root=new GameObject("EvolutionPrototype");
            var plot=new GameObject("WidePlotGeometry");plot.transform.SetParent(root.transform,false);
            var islandId="Tile_Ice_Core_v001";
            var platform=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{islandId}/{islandId}.prefab"),plot.transform);
            platform.name="WideIcePlot24x24";
            foreach(var b in platform.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var renderers=platform.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            plot.transform.localScale=new Vector3(24/bounds.size.x,.35f/Mathf.Max(.01f,bounds.size.y),24/bounds.size.z);
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            plot.transform.position=-new Vector3(bounds.center.x,bounds.max.y+.03f,bounds.center.z);
            var system=new GameObject("EvolutionEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));system.transform.SetParent(root.transform,false);
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var driver=root.AddComponent<EvolutionDriver>();var so=new SerializedObject(driver);
            so.FindProperty("_camera").objectReferenceValue=camera;
            so.FindProperty("_font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            var ids=new[]{"Building_Fishery_2x2_v002","Building_Saltworks_2x2_v002","Building_Warehouse_1x1_v002","Building_Chikuwa_2x2_v002","Building_Kamaboko_2x2_v002","Building_Drying_2x2_v002"};
            var models=so.FindProperty("_models");models.arraySize=ids.Length;
            for(var i=0;i<ids.Length;i++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{ids[i]}/{ids[i]}.prefab");
                if(prefab==null)throw new InvalidOperationException(ids[i]);models.GetArrayElementAtIndex(i).objectReferenceValue=prefab;
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
