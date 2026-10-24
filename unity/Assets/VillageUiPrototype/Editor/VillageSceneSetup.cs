using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.VillageUiPrototype.Editor
{
    public static class VillageSceneSetup
    {
        public const string ScenePath="Assets/Scenes/VillageUiPrototype.unity";
        [MenuItem("Village Prototype/Create UI Sandbox Copy")]
        public static void Create()
        {
            var active=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||active.isDirty)throw new InvalidOperationException("Stop Play Mode and preserve unsaved scene changes first.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)throw new InvalidOperationException("The UI scene already exists; it will not be overwritten.");
            var source="Assets/Scenes/HealthFactoryPrototype.unity";
            if(active.path!=source)active=EditorSceneManager.OpenScene(source);
            if(!EditorSceneManager.SaveScene(active,ScenePath,true))throw new InvalidOperationException("Could not copy source scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var all=scene.GetRootGameObjects();
            var camera=all.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.gameObject.activeInHierarchy);
            var originalPlot=all.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="HealthIcePlot");
            foreach(var root in all)
            {
                if(root==camera.gameObject){foreach(var b in root.GetComponents<MonoBehaviour>())b.enabled=false;}
                else if(root.GetComponent<Light>()!=null){}
                else if(root.name=="World")
                {foreach(var b in root.GetComponents<MonoBehaviour>())b.enabled=false;foreach(Transform child in root.transform)if(child.name!="Sea")child.gameObject.SetActive(false);}
                else root.SetActive(false);
            }
            camera.orthographic=true;camera.orthographicSize=12.5f;camera.transform.position=new Vector3(0,23,-20);camera.transform.LookAt(Vector3.zero);
            var parent=new GameObject("VillageUiPrototype");
            var plot=UnityEngine.Object.Instantiate(originalPlot.gameObject,parent.transform);plot.name="VillageIcePlot";plot.SetActive(true);plot.transform.localScale=Vector3.one*1.5f;
            var ids=new[]{"Fishery_2x2","Preparation_2x2","Saltworks_2x2","KelpFarm_2x2","Kamaboko_2x2","Chikuwa_2x2","Drying_2x2","Oden_2x2","Warehouse_1x1","Dormitory_1x1","Research_2x2","Sorter_1x1","ElectricAmplifier_2x2","WaterAmplifier_2x2","GasAmplifier_2x2","IceHouse_1x1"};
            var prefabs=ids.Select(id=>{var name="Building_"+id+"_v002";var p=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{name}/{name}.prefab");if(p==null)throw new InvalidOperationException(name);return p;}).ToArray();
            var events=new GameObject("VillageEventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(parent.transform,false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var driver=parent.AddComponent<VillageDriver>();var serialized=new SerializedObject(driver);
            serialized.FindProperty("_camera").objectReferenceValue=camera;
            serialized.FindProperty("_font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            serialized.FindProperty("_plot").objectReferenceValue=plot.transform;
            serialized.FindProperty("_harborPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Harbor_Stage4_v002/Harbor_Stage4_v002.prefab");
            var array=serialized.FindProperty("_prefabs");array.arraySize=prefabs.Length;for(var i=0;i<prefabs.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=prefabs[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Could not save sandbox scene.");
            Selection.activeGameObject=parent;
        }
    }
    [CustomEditor(typeof(VillageDriver))]
    public sealed class VillageDriverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var driver=(VillageDriver)target;
            EditorGUILayout.Space();EditorGUILayout.HelpBox("ゲーム画面にはデバッグ操作を表示しません。以下は再生中だけ使えます。無限資源はEditor / Development Build限定です。",MessageType.Info);
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if(GUILayout.Button("Inspectorの資源量を反映"))driver.ApplyInspectorResources();
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("停止 / 再開"))driver.ToggleClock();
                if(GUILayout.Button("+1秒"))driver.AdvanceSeconds(1);
                if(GUILayout.Button("+1分"))driver.AdvanceSeconds(60);
                EditorGUILayout.EndHorizontal();
                if(driver.View!=null)EditorGUILayout.LabelField("経過時間",(driver.View.AtMs/1000.0).ToString("0.0")+" 秒");
                if(GUILayout.Button("更地にする（この試作セッションだけ）")&&EditorUtility.DisplayDialog("村を空にする","試作内の配置と在庫を消去します。製品の保存データには影響しません。","更地にする","キャンセル"))driver.ClearVillage();
            }
        }
    }
}
