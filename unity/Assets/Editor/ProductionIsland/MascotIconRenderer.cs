using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>
    /// ゲームの看板のペンギン (Assets/3Dmodel/Penguin_v002) の頭を、UI のアイコン用に透明背景の PNG へ描き出す。
    /// 画面のあちこちにある「ペンギンの顔」(`.pp-icon--penguin`) はこの絵を指す。
    /// 透明度は、白と黒の背景で 2 回描いた差から求める (URP の後処理でアルファが残らなくても正しく抜ける)。
    /// </summary>
    public static class MascotIconRenderer
    {
        public const string ModelPath = "Assets/3Dmodel/Penguin_v002/Penguin_Static_v002.fbx";
        public const string IconPath = "Assets/UI/DesignSystem/Icons/penguin-mascot.png";

        /// <summary>アプリのアイコンなど、大きな絵の下書きに使う全身・頭の高解像度版 (ゲームには読み込まない)。</summary>
        public const string ArtFolder = "ArtSource/Penguin_v002";

        private const int Size = 512;

        /// <summary>ほかの物が写らない、シーンの遠く上の方で描く。</summary>
        private static readonly Vector3 Stage = new Vector3(0f, 900f, 0f);

        [MenuItem("Production Island/Render Mascot Icon")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play Mode を止めてから実行してください。");
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                throw new FileNotFoundException("看板のペンギンのモデルが見つかりません。", ModelPath);
            }

            File.WriteAllBytes(IconPath, Render(model, Size, headOnly: true));
            Directory.CreateDirectory(ArtFolder);
            File.WriteAllBytes($"{ArtFolder}/Penguin_Mascot_Head_1024.png", Render(model, 1024, headOnly: true));
            File.WriteAllBytes($"{ArtFolder}/Penguin_Mascot_Full_1024.png", Render(model, 1024, headOnly: false));
            AssetDatabase.ImportAsset(IconPath);
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            Debug.Log($"MascotIconRenderer: {IconPath} と {ArtFolder} の下書きを描き出しました。");
        }

        /// <param name="headOnly">頭 (上の方) だけを大きく写す。false なら全身。</param>
        public static byte[] Render(GameObject model, int size, bool headOnly)
        {
            var go = UnityEngine.Object.Instantiate(model, Stage, model.transform.rotation);
            go.hideFlags = HideFlags.HideAndDontSave;
            var cameraGo = new GameObject("MascotCamera") { hideFlags = HideFlags.HideAndDontSave };

            // シーンの光は寒色で、白いお腹が青く沈む。看板の絵は白い正面光と、輪郭を出す後ろからの光で描く。
            var key = Light("MascotKey", Quaternion.Euler(30f, 160f, 0f), 1.15f, new Color(1f, 0.98f, 0.95f));
            var rim = Light("MascotRim", Quaternion.Euler(20f, -20f, 0f), 0.6f, new Color(0.85f, 0.93f, 1f));
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                var bounds = BoundsOf(go);
                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.fieldOfView = 20f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.targetTexture = rt;

                // 正面から少しだけ上・右より。頭のアイコンは、小さく出しても顔が分かるよう頭を枠いっぱいに写す (胸元で切る)。
                var height = bounds.size.y;
                var focus = headOnly
                    ? new Vector3(bounds.center.x, bounds.max.y - height * 0.27f, bounds.center.z)
                    : bounds.center;
                var span = headOnly ? height * 0.31f : bounds.extents.magnitude;
                var direction = Quaternion.Euler(8f, 192f, 0f) * Vector3.forward;
                var distance = span / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cameraGo.transform.position = focus - direction * distance;
                cameraGo.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

                var onWhite = Shoot(camera, rt, Color.white);
                var onBlack = Shoot(camera, rt, Color.black);
                var w = onWhite.GetPixels();
                var b = onBlack.GetPixels();
                var result = new Color[w.Length];
                for (var i = 0; i < w.Length; i++)
                {
                    // 白背景 − 黒背景 = 1 − α (チャンネルの平均)。色は黒背景の値を α で割って戻す。
                    var diff = ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f;
                    var alpha = Mathf.Clamp01(1f - diff);
                    result[i] = alpha < 0.002f
                        ? new Color(0f, 0f, 0f, 0f)
                        : new Color(Mathf.Clamp01(b[i].r / alpha), Mathf.Clamp01(b[i].g / alpha), Mathf.Clamp01(b[i].b / alpha), alpha);
                }

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels(result);
                tex.Apply();
                var png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);
                UnityEngine.Object.DestroyImmediate(onWhite);
                UnityEngine.Object.DestroyImmediate(onBlack);
                return png;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(key);
                UnityEngine.Object.DestroyImmediate(rim);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static GameObject Light(string name, Quaternion rotation, float intensity, Color color)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.rotation = rotation;
            var light = go.AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            return go;
        }

        private static Texture2D Shoot(Camera camera, RenderTexture rt, Color background)
        {
            camera.backgroundColor = background;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
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
                b.Encapsulate(r.bounds);
            }

            return b;
        }
    }
}
