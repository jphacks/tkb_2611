using System;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.Editor
{
    public static class RaidRescueSetup
    {
        public const string PrefabPath = "Assets/ProductionIsland/Raid/Resources/RaidRescueStage.prefab";
        public const string PreviewScenePath = "Assets/Scenes/RaidRescuePreview.unity";
        private const string SourceScenePath = "Assets/Scenes/ProductionIslandHome.unity";
        private const int PreviewLayer = 30;

        [MenuItem("Production Island/Raid/Build Rescue Stage")]
        public static void BuildStage()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before building the rescue stage.");
            EnsureFolder("Assets/ProductionIsland/Raid/Resources");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("RaidRescueStage");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.transform.position = new Vector3(10000, 0, 0);
                var presenter = root.AddComponent<RaidRescuePresenter>();
                PlaceTile(root.transform, "Safe ice", new Vector3(-3.5f, 0, 0), new Vector3(2.4f, 1.4f, 2.8f));
                PlaceTile(root.transform, "Stranded ice", new Vector3(3.5f, 0, 0), new Vector3(2.4f, 1.4f, 2.8f));
                var stages = new GameObject[4];
                for (var i = 0; i < 4; i++)
                {
                    stages[i] = PlaceTile(root.transform, $"Ice path {(i+1)*25}%", new Vector3(-1.8f + i*1.2f, 0, 0), new Vector3(1.22f, 1, 1.5f));
                    stages[i].SetActive(false);
                }
                var penguin = Model(root.transform, "Assets/3Dmodel/Penguin_v002/Penguin_Static_v002.fbx", "Penguin to rescue");
                penguin.transform.localPosition = new Vector3(3.5f, 0.03f, 0);
                penguin.transform.localScale = Vector3.one * 1.5f;
                penguin.transform.localRotation = Quaternion.Euler(0, 145, 0);
                var friend = Model(root.transform, "Assets/3Dmodel/Penguin_v002/Penguin_Static_v002.fbx", "Waiting friend");
                friend.transform.localPosition = new Vector3(-3.6f, 0.03f, 0.7f);
                friend.transform.localScale = Vector3.one * 1.15f;
                friend.transform.localRotation = Quaternion.Euler(0, 215, 0);
                var cameraObject = new GameObject("Rescue preview camera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = new Vector3(0, 7, -10);
                cameraObject.transform.LookAt(root.transform.position + new Vector3(0, 0.35f, 0));
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2.9f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 40;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.847f, 0.929f, 0.988f);
                camera.cullingMask = 1 << PreviewLayer;
                camera.enabled = false;
                var lightObject = new GameObject("Rescue light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localRotation = Quaternion.Euler(50, -35, 0);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                light.cullingMask = 1 << PreviewLayer;
                light.shadows = LightShadows.None;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = PreviewLayer;
                var serialized = new SerializedObject(presenter);
                var stageProperty = serialized.FindProperty("_rescuedStages");
                stageProperty.arraySize = stages.Length;
                for (var i = 0; i < stages.Length; i++) stageProperty.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
                serialized.FindProperty("_previewCamera").objectReferenceValue = camera;
                serialized.FindProperty("_penguin").objectReferenceValue = penguin.transform;
                serialized.FindProperty("_strandedPosition").vector3Value = penguin.transform.localPosition;
                serialized.FindProperty("_safePosition").vector3Value = new Vector3(-2.8f, 0.03f, 0);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Debug.Log("救助の4段階表示を作成しました: " + PrefabPath);
        }

        [MenuItem("Production Island/Raid/Open Local Preview")]
        public static void OpenPreview()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before opening the preview scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildStage();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScenePath) == null && !AssetDatabase.CopyAsset(SourceScenePath, PreviewScenePath))
                throw new InvalidOperationException("Could not copy the home scene for preview.");
            var scene = EditorSceneManager.OpenScene(PreviewScenePath);
            var game = UnityEngine.Object.FindAnyObjectByType<GameStateSystem>();
            var gameProperties = new SerializedObject(game);
            gameProperties.FindProperty("_inMemoryOnly").boolValue = true;
            gameProperties.ApplyModifiedPropertiesWithoutUndo();
            var navigator = UnityEngine.Object.FindAnyObjectByType<ScreenNavigator>();
            var presenter = navigator.GetComponent<RaidScreenPresenter>() ?? navigator.gameObject.AddComponent<RaidScreenPresenter>();
            var properties = new SerializedObject(presenter);
            properties.FindProperty("_editorLocalDemo").boolValue = true;
            properties.FindProperty("_editorVenue").boolValue = false;
            properties.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = navigator.gameObject;
            Debug.Log("ローカル模擬の確認シーンを開きました。Playで参加できます。島はメモリ保存、他端末との共有はありません。");
        }

        private static GameObject PlaceTile(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var tile = Model(parent, "Assets/3Dmodel/Tile_Ice_Core_v001/Tile_Ice_Core_v001.prefab", name);
            tile.transform.localPosition = position;
            tile.transform.localScale = scale;
            return tile;
        }

        private static GameObject Model(Transform parent, string path, string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Missing rescue model: " + path);
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            PrefabUtility.InstantiatePrefab(source, instance.transform);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            return instance;
        }

        private static void EnsureFolder(string path)
        {
            var parent = "Assets";
            foreach (var part in path.Substring(7).Split('/'))
            {
                var next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }
        }
    }
}
