using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectPenguin.Presentation.UI;
using UnityEditor;
using UnityEngine;
using IslandKind = ProjectPenguin.Domain.Island.FacilityKind;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>
    /// 建物一覧・説明・施設の画面で使う建物の絵を、3D モデルから描き出す。
    /// 開いているシーンの光で、全部同じ角度・同じ背景色 (カードの下地と同じ色) の正方形の PNG にする。
    /// あわせて、絵を指す USS (Thumbnails.uss) を書き直す。クラス名は <see cref="UiClasses.Thumb"/> を使う。
    /// </summary>
    public static class FacilityThumbnailRenderer
    {
        private const string OutputFolder = "Assets/UI/Thumbnails";
        private const string UssPath = "Assets/UI/DesignSystem/Thumbnails.uss";
        private const int Size = 512;

        /// <summary>カードの下地 (--color-ice-50) と同じ色。絵の背景がカードに溶け込む。</summary>
        private static readonly Color Background = new Color32(0xEE, 0xF7, 0xFF, 0xFF);

        /// <summary>ほかの物が写らない、シーンの遠く上の方で描く。</summary>
        private static readonly Vector3 Stage = new Vector3(0f, 600f, 0f);

        [MenuItem("Production Island/Render Facility Thumbnails")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play Mode を止めてから実行してください。");
            }

            Directory.CreateDirectory(OutputFolder);
            var entries = new List<(string Class, string File, GameObject Model)>();
            foreach (IslandKind kind in Enum.GetValues(typeof(IslandKind)))
            {
                entries.Add((UiClasses.Thumb.Of(kind), $"Thumb_{kind}.png", HomeIslandSetup.FacilityModel(kind)));
            }

            for (var i = 0; i < UiClasses.Thumb.Harbors.Length; i++)
            {
                var harbor = AssetDatabase.LoadAssetAtPath<GameObject>(GyojouHarborSetup.PrefabPath) ?? HomeIslandSetup.Model($"Harbor_Stage{i}_v002");
                entries.Add((UiClasses.Thumb.Harbors[i], $"Thumb_Harbor{i}.png", harbor));
            }

            entries.Add((UiClasses.Thumb.Expand, "Thumb_Expand.png", HomeIslandSetup.Model("Tile_Ice_Expanded_v001")));

            var uss = new StringBuilder();
            uss.AppendLine("/* 建物の絵。Production Island > Render Facility Thumbnails が描き出した PNG を指す (このファイルもメニューが書き直す) */");
            uss.AppendLine($".{UiClasses.Thumb.Root} {{");
            uss.AppendLine("    -unity-background-scale-mode: scale-to-fit;");
            uss.AppendLine("}");

            var rendered = 0;
            foreach (var (cls, file, model) in entries)
            {
                if (model == null)
                {
                    Debug.LogWarning($"FacilityThumbnailRenderer: {file} のモデルが見つかりません。");
                    continue;
                }

                var path = $"{OutputFolder}/{file}";
                File.WriteAllBytes(path, Render(model));
                rendered++;
                uss.AppendLine();
                uss.AppendLine($".{cls} {{");
                uss.AppendLine($"    background-image: url(\"project://database/{path}\");");
                uss.AppendLine("}");
            }

            AssetDatabase.Refresh();
            foreach (var (_, file, _) in entries)
            {
                ConfigureImporter($"{OutputFolder}/{file}");
            }

            File.WriteAllText(UssPath, uss.ToString());
            AssetDatabase.ImportAsset(UssPath);
            Debug.Log($"FacilityThumbnailRenderer: {rendered} 枚の絵を {OutputFolder} に描き出しました。");
        }

        private static byte[] Render(GameObject model)
        {
            // FBX のプレハブは根元に回転 (Z 上向きを Y 上向きへ) を持つので、回転は変えずに位置だけ動かす。
            var go = UnityEngine.Object.Instantiate(model, Stage, model.transform.rotation);
            go.hideFlags = HideFlags.HideAndDontSave;
            var cameraGo = new GameObject("ThumbnailCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                var bounds = BoundsOf(go);
                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
                camera.fieldOfView = 24f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.targetTexture = rt;

                // ゲームのカメラより少し低く、斜め前から見下ろす。
                var direction = Quaternion.Euler(28f, -32f, 0f) * Vector3.forward;
                var radius = bounds.extents.magnitude;
                var distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                cameraGo.transform.position = bounds.center - direction * distance;
                cameraGo.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                var png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                return png;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.one);
            }

            var b = renderers[0].bounds;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer)
                {
                    continue;
                }

                b.Encapsulate(r.bounds);
            }

            return b;
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
