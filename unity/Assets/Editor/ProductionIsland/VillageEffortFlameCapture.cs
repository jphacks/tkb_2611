using System;
using System.IO;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Renders the actual village renderer in an isolated scene without opening game saves.</summary>
    public static class VillageEffortFlameCapture
    {
        public const string Folder = "/tmp/tkb-effort-flames";
        [MenuItem("Project Penguin/Village/Capture Effort Flames")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("EffortPreview"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var cameraGo = new GameObject("PreviewCamera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            var rt = new RenderTexture(1600, 750, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                camera.cameraType = CameraType.Game; camera.enabled = false;
                camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.orthographic = true; camera.orthographicSize = 2.6f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(225, 240, 248, 255);
                camera.nearClipPlane = .1f; camera.farClipPlane = 50; camera.targetTexture = rt;
                camera.transform.rotation = Quaternion.Euler(38, 0, 0);
                var focus = new Vector3(-.65f, .85f, -.65f);
                camera.transform.position = focus - camera.transform.forward * 12;
                var lightGo = new GameObject("PreviewLight"); lightGo.transform.SetParent(root.transform);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.color = new Color(1, .98f, .93f);
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
                var world = root.AddComponent<VillageWorldPresenter>(); world.enabled = false;
                var so = new SerializedObject(world);
                var entries = so.FindProperty("_catalog").FindPropertyRelative("_entries"); entries.arraySize = 1;
                var entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("_kind").intValue = (int)FacilityKind.Fishery;
                entry.FindPropertyRelative("_prefab").objectReferenceValue = HomeIslandSetup.FacilityModel(FacilityKind.Fishery);
                so.ApplyModifiedPropertiesWithoutUndo();
                var field = typeof(VillageWorldPresenter).GetField("_catalog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var renderer = new VillageWorldRenderer(root.transform, camera, (VillagePrefabCatalog)field.GetValue(world), null);
                var game = new VillageGame(false); game.SetResources(10000, 10000, 10000);
                for (var effort = 0; effort <= 3; effort++)
                {
                    var node = game.Place(FacilityKind.Fishery, 4 + effort * 4, 10); game.SetEffort(node.Id, effort);
                }
                renderer.Render(game.CreateView(), 0);
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
                for (var effort = 0; effort <= 3; effort++)
                {
                    var p = VillageWorldRenderer.Position(4 + effort * 4, 10, 2);
                    Label(root.transform, camera, font, "エフォート " + effort, p - camera.transform.up * .80f - camera.transform.forward * 2, .19f);
                    Label(root.transform, camera, font, effort == 0 ? "通常運転" : "炎 " + effort + "つ", p - camera.transform.up * 1.17f - camera.transform.forward * 2, .14f);
                }
                Label(root.transform, camera, font, "エフォートを炎の数と大きさで表示", focus + camera.transform.up * 2.05f, .22f, 10);
                var flames = root.GetComponentsInChildren<VillageEffortFlameGraphic>(true);
                for (var frame = 0; frame < 24; frame++)
                {
                    foreach (var flame in flames)
                    {
                        flame.Render(flame.Effort, true, frame / 12f);
                        flame.Rebuild(CanvasUpdate.PreRender);
                    }
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    Write(camera, rt, Path.Combine(Folder, $"frame-{frame:00}.png"));
                }
                File.Copy(Path.Combine(Folder, "frame-06.png"), Path.Combine(Folder, "preview.png"), true);
            }
            finally
            {
                camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Debug.Log("Effort preview: " + Folder);
        }

        private static void Label(Transform parent, Camera camera, Font font, string caption, Vector3 position, float height, float width = 2.4f)
        {
            var go = new GameObject("Caption", typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent);
            go.transform.position = position; go.transform.rotation = camera.transform.rotation;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(width * 200, height * 300); rect.localScale = Vector3.one / 200;
            var text = go.AddComponent<Text>(); text.font = font; text.fontSize = 32; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(33, 65, 85, 255); text.text = caption; text.raycastTarget = false;
        }
        private static void Write(Camera camera, RenderTexture rt, string path)
        {
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try { tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(tex); }
        }
    }
}
