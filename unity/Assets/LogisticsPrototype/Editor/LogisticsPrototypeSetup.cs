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
    public static class LogisticsPrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/LogisticsPrototype.unity";
        [MenuItem("Production Island/Logistics Prototype/Setup Copied Scene")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying) throw new InvalidOperationException("Open copied LogisticsPrototype scene in Edit Mode first.");
            if (scene.GetRootGameObjects().Any(g=>g.GetComponent<LogisticsPrototypeController>() != null))
                throw new InvalidOperationException("Prototype already configured; no overwrite performed.");
            var camera = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == camera.gameObject)
                {
                    foreach(var b in root.GetComponents<MonoBehaviour>())
                        if(b.GetType().Namespace?.StartsWith("ProjectPenguin") == true) b.enabled = false;
                }
                else if(root.GetComponent<Light>() != null) { }
                else if(root.name == "World")
                {
                    foreach(var b in root.GetComponents<MonoBehaviour>()) b.enabled = false;
                    foreach(Transform child in root.transform) if(child.name != "Sea") child.gameObject.SetActive(false);
                }
                else root.SetActive(false);
            }
            camera.orthographic = true; camera.orthographicSize = 8.4f;
            camera.transform.position = new Vector3(0, 15, -14);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            camera.transform.position -= camera.transform.up * .6f;
            var prototype = new GameObject("LogisticsPrototype");
            var positions = new[] { new Vector3(-.9f,.08f,5), new Vector3(.9f,.08f,0), new Vector3(-.9f,.08f,-5) };
            var ids = new[] { "Building_Fishery_2x2_v002", "Building_Preparation_2x2_v002", "Building_Warehouse_1x1_v002" };
            var facilities = new Transform[3];
            for(var i=0;i<3;i++)
            {
                var tile = Spawn("Tile_Ice_Core_v001", prototype.transform);
                Fit(tile, new Vector3(positions[i].x, -.15f, positions[i].z), 3.6f);
                var facility=Spawn(ids[i], prototype.transform); Fit(facility, positions[i], 2.25f);
                facilities[i]=facility.transform.parent;
            }
            var system = new GameObject("PrototypeEventSystem", typeof(EventSystem),typeof(InputSystemUIInputModule));
            system.transform.SetParent(prototype.transform,false);
            var module=system.GetComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
            var controller=prototype.AddComponent<LogisticsPrototypeController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_camera").objectReferenceValue=camera;
            so.FindProperty("_font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            var array=so.FindProperty("_facilities"); array.arraySize=3;
            for(var i=0;i<3;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=facilities[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed");
        }
        private static GameObject Spawn(string id, Transform parent)
        {
            var path=$"Assets/3Dmodel/{id}/{id}.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null) throw new InvalidOperationException(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            // Imported facility cycles are decorative loops, unrelated to this trial's inventory clock.
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            return instance;
        }
        private static void Fit(GameObject go, Vector3 position, float width)
        {
            go.transform.position=Vector3.zero;
            var renderers=go.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) throw new InvalidOperationException("Model has no renderer");
            var bounds=renderers[0].bounds; foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            go.transform.localScale *= width / Mathf.Max(bounds.size.x,bounds.size.z);
            bounds=renderers[0].bounds; foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            go.transform.position=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            // A stable ground anchor drives UI projection, independent of the imported model pivot.
            var anchor=new GameObject(go.name+"_Anchor"); anchor.transform.SetParent(go.transform.parent,false);
            anchor.transform.position=position; go.transform.SetParent(anchor.transform,true);
        }
    }
}
