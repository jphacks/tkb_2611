using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ProjectPenguin.FactoryGame.Editor
{
    public static class FactoryGameSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/FactoryGame.unity";
        [MenuItem("Project Penguin/Factory/Create Factory Game Scene")]
        public static void Create()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before generating the factory scene");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("FactoryCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            var light = new GameObject("FactorySun", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.75f, .82f, .9f);
            var system = new GameObject("FactoryEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var root = new GameObject("FactoryGame"); var driver = root.AddComponent<FactoryGameDriver>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("_camera").objectReferenceValue = camera;
            serialized.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            var kinds = new[] { "fishery", "kelp_farm", "saltworks", "preparation", "drying", "kamaboko", "chikuwa", "warehouse", "oden", "research", "gift" };
            var names = new[] { "Fishery_2x2", "KelpFarm_2x2", "Saltworks_2x2", "Preparation_2x2", "Drying_2x2", "Kamaboko_2x2", "Chikuwa_2x2", "Warehouse_1x1", "Oden_2x2", "Research_2x2", "Sorter_1x1" };
            var models = serialized.FindProperty("_models"); models.arraySize = kinds.Length;
            for (var i = 0; i < kinds.Length; i++)
            {
                var name = "Building_" + names[i] + "_v002";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{name}/{name}.prefab");
                if (prefab == null) throw new InvalidOperationException("Missing factory model: " + name);
                var element = models.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("recipe").stringValue = kinds[i];
                element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save factory scene");
        }
    }
}
