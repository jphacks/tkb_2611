using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.HealthFactoryPrototype.Editor
{
    public static class HealthFactorySceneSetup
    {
        public const string ScenePath="Assets/Scenes/HealthFactoryPrototype.unity";
        private const string WorldName="World", SeaName="Sea", RootName="HealthFactoryPrototype", EventSystemName="HealthFactoryEventSystem";
        public static void Setup()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!=ScenePath||scene.isDirty)throw new InvalidOperationException("Only the clean copied health scene may be configured");
            if(scene.GetRootGameObjects().Any(g=>g.GetComponent<HealthFactoryDriver>()!=null))throw new InvalidOperationException("Already configured");
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root==camera.gameObject){foreach(var b in root.GetComponents<MonoBehaviour>())b.enabled=false;}
                else if(root.GetComponent<Light>()!=null){}
                else if(root.name==WorldName)
                {foreach(var b in root.GetComponents<MonoBehaviour>())b.enabled=false;foreach(Transform child in root.transform)if(child.name!=SeaName)child.gameObject.SetActive(false);}
                else root.SetActive(false);
            }
            camera.orthographic=true;camera.orthographicSize=11;camera.transform.position=new Vector3(0,20,-16);camera.transform.LookAt(Vector3.zero);
            camera.transform.position-=camera.transform.up*1.3f;
            var parent=new GameObject(RootName);
            var plot=new GameObject("HealthIcePlot");plot.transform.SetParent(parent.transform,false);
            var geometry=new GameObject("HealthIceGeometry");geometry.transform.SetParent(plot.transform,false);
            var tile=Spawn("Tile_Ice_Core_v001",geometry.transform);FitPlot(geometry.transform,tile);
            var ids=new[]{"Building_Fishery_2x2_v002","Building_Preparation_2x2_v002","Building_Warehouse_1x1_v002"};
            var facilities=new Transform[3];
            var initialY=new[]{2,8,14};
            for(var i=0;i<3;i++)
            {
                var anchor=new GameObject("HealthFacility"+i);anchor.transform.SetParent(parent.transform,false);
                var model=Spawn(ids[i],anchor.transform);Fit(model,Vector3.zero,1.25f);facilities[i]=anchor.transform;
                anchor.transform.position=new Vector3(-.65f,.1f,(initialY[i]+1-8)*.65f);
            }
            var system=new GameObject(EventSystemName,typeof(EventSystem),typeof(InputSystemUIInputModule));system.transform.SetParent(parent.transform,false);
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var driver=parent.AddComponent<HealthFactoryDriver>();var serialized=new SerializedObject(driver);
            serialized.FindProperty("_camera").objectReferenceValue=camera;
            serialized.FindProperty("_font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            serialized.FindProperty("_plot").objectReferenceValue=plot.transform;
            var array=serialized.FindProperty("_facilities");array.arraySize=3;
            for(var i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=facilities[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Save failed");
        }
        private static GameObject Spawn(string id,Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{id}/{id}.prefab");if(prefab==null)throw new InvalidOperationException(id);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))a.enabled=false;
            return instance;
        }
        private static void FitPlot(Transform worldAxes,GameObject tile)
        {
            var renderers=tile.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            worldAxes.localScale=new Vector3(10.4f/bounds.size.x,.32f/bounds.size.y,10.4f/bounds.size.z);
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            worldAxes.position-=new Vector3(bounds.center.x,bounds.max.y+.03f,bounds.center.z);
        }
        private static void Fit(GameObject model,Vector3 position,float width)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            var scale=width/Mathf.Max(bounds.size.x,bounds.size.z);model.transform.localScale*=scale;
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            model.transform.position=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
    }
}
