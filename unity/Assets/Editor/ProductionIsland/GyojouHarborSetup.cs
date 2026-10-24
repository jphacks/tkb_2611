using System;
using System.Linq;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>ユーザー制作の桟橋を港として取り込み、小屋の氷山の右岸へつなぐ。</summary>
    public static class GyojouHarborSetup
    {
        public const string Folder = "Assets/3Dmodel/Harbor_Gyojou_v007";
        public const string PrefabPath = Folder + "/Harbor_Gyojou_v007.prefab";

        [MenuItem("Production Island/Setup Gyojou Harbor")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode を止めてから実行してください。");
            Build();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(HomeIslandSetup.ScenePath);
            if (!scene.isLoaded) throw new InvalidOperationException("ProductionIslandHome を開いてください。");
            var board = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<IslandBoardView>(true)).First();
            if (!Assign(board)) throw new InvalidOperationException("桟橋または小屋の氷山が見つかりません。");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static bool Assign(IslandBoardView board)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var iceberg = board.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "HomeIceberg");
            if (prefab == null || iceberg == null) return false;
            var anchor = iceberg.Find("HarborAnchor");
            if (anchor == null)
            {
                anchor = new GameObject("HarborAnchor").transform;
                anchor.SetParent(iceberg, false);
                anchor.SetPositionAndRotation(new Vector3(-.3f, -.10f, 7f), Quaternion.Euler(0, 135, 0));
            }

            var so = new SerializedObject(board);
            so.FindProperty("_harborAnchor").objectReferenceValue = anchor;
            var stages = so.FindProperty("_harborStages");
            stages.arraySize = 6;
            for (var i = 0; i < stages.arraySize; i++) stages.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void Build()
        {
            var texturePath = Folder + "/Textures/Gyojou_BaseColor.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.sRGBTexture = true;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.mipmapEnabled = true;
            textureImporter.maxTextureSize = 512;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!AssetDatabase.IsValidFolder(Folder + "/Materials")) AssetDatabase.CreateFolder(Folder, "Materials");
            var names = new[] { "Gyojou_Wood_v005", "Gyojou_PaintedMetal_v005", "Gyojou_Steel_v005", "Gyojou_Rope_v005", "Gyojou_CargoMat_v007" };
            var metallic = new[] { 0f, .35f, .75f, 0f, 0f };
            var smoothness = new[] { .22f, .5f, .64f, .12f, .1f };
            var materials = new Material[names.Length];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit が見つかりません。");
            for (var i = 0; i < names.Length; i++)
            {
                var path = $"{Folder}/Materials/{names[i]}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.SetTexture(ShaderIds.BaseMap, i == 4 ? null : texture);
                material.SetColor(ShaderIds.BaseColor, i == 4 ? new Color(66 / 255f, 84 / 255f, 99 / 255f, 1) : Color.white);
                material.SetFloat(ShaderIds.Metallic, metallic[i]); material.SetFloat(ShaderIds.Smoothness, smoothness[i]);
                EditorUtility.SetDirty(material); materials[i] = material;
            }

            var fbxPath = Folder + "/Harbor_Gyojou_v007.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.importAnimation = false; importer.globalScale = 1; importer.useFileScale = true; importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            for (var i = 0; i < materials.Length; i++) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), names[i]), materials[i]);
            importer.SaveAndReimport();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                // FBX の軸・単位変換は子に保持する。配置用の親は回転 0・倍率 1 にする。
                var root = new GameObject("Harbor_Gyojou_v007");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
                model.name = "Model";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, preview);
                model.transform.SetParent(root.transform, false);
                var points = root.GetComponentsInChildren<MeshFilter>().SelectMany(m => m.sharedMesh.vertices.Select(v => m.transform.TransformPoint(v))).ToArray();
                AddTapBox(root, "HarborTapDeck", points.Where(p => p.y > -.15f && p.y < .025f).ToArray(), .15f);
                AddTapBox(root, "HarborTapHoist", points.Where(p => p.y > .6f).ToArray(), .1f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            importer.isReadable = false; importer.SaveAndReimport();
        }

        private static void AddTapBox(GameObject root, string name, Vector3[] points, float minHeight)
        {
            if (points.Length == 0) throw new InvalidOperationException(name + " の範囲がありません。");
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true;
            collider.center = bounds.center;
            collider.size = new Vector3(bounds.size.x, Mathf.Max(minHeight, bounds.size.y), Mathf.Max(.25f, bounds.size.z));
        }
    }
}
