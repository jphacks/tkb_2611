using System;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>色付きシンボルを質の良いマスへ重ねる。魚と塩はプロジェクト向けに制作した絵で表す。</summary>
    public static class QualityAreaSetup
    {
        public const string Folder = "Assets/ProductionIsland/QualityAreas_v001";
        private static readonly int Surface = Shader.PropertyToID("_Surface");
        private static readonly int SourceBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DestinationBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        [MenuItem("Production Island/Update Quality Areas")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play Mode を止めてから実行してください。");
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(HomeIslandSetup.ScenePath);
            var closeWhenDone = !scene.isLoaded;
            if (closeWhenDone)
            {
                scene = EditorSceneManager.OpenScene(HomeIslandSetup.ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                IslandBoardView board = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    board = root.GetComponentInChildren<IslandBoardView>(true);
                    if (board != null) break;
                }

                if (board == null) throw new InvalidOperationException("生産の島が見つかりません。");
                Assign(board);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (closeWhenDone) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>ホーム再構築時にも同じ参照を設定する。既存の 3D マーカーは残す。</summary>
        internal static void Assign(IslandBoardView board)
        {
            var so = new SerializedObject(board);
            so.FindProperty("_qualityFish").objectReferenceValue = Build("Fish");
            so.FindProperty("_qualitySalt").objectReferenceValue = Build("SaltCrystal");
            so.FindProperty("_qualityKelp").objectReferenceValue = Build("Kelp");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Build(string name)
        {
            var texturePath = $"{Folder}/{name}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) throw new InvalidOperationException($"提供画像がありません: {texturePath}");
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var materialPath = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("URP Unlit が見つかりません。");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture(ShaderIds.BaseMap, texture);
            material.SetColor(ShaderIds.BaseColor, Color.white);
            material.SetFloat(Surface, 1);
            material.SetInt(SourceBlend, (int)BlendMode.SrcAlpha);
            material.SetInt(DestinationBlend, (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt(ZWrite, 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);

            var area = new GameObject($"Quality Area {name}");
            try
            {
                var hitArea = area.AddComponent<BoxCollider>();
                hitArea.isTrigger = true;
                hitArea.size = new Vector3(0.94f, 0.08f, 0.94f);
                hitArea.center = Vector3.up * 0.04f;
                var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
                surface.name = "Area";
                surface.transform.SetParent(area.transform, false);
                surface.transform.localRotation = Quaternion.Euler(90, 0, 0);
                surface.transform.localScale = Vector3.one * 0.94f;
                UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
                var renderer = surface.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return PrefabUtility.SaveAsPrefabAsset(area, $"{Folder}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(area);
            }
        }
    }
}
