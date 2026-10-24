using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.Editor.ProductionIsland
{
    public static class KamabokoRevisionBuilder
    {
        public const string Folder = "Assets/3Dmodel/Building_Kamaboko_2x2_v002";
        public const string ScenePath = "Assets/Scenes/KamabokoDesignReview.unity";
        [Serializable] private sealed class Sample
        {
            public string stage, image;
            public int frame;
            public float time;
            public bool fishVisible, productVisible, steamVisible;
            public Vector3 fish, product, lid, worker;
            public string workerActivity;
            public float handToItem;
        }
        [Serializable] private sealed class CaptureReport
        {
            public string date = "2026-10-08", unity;
            public bool playMode, allStages, readableSequence, worldMotionInBounds, stopFreezes, restartWorks, workerInvolved, workerFreezes;
            public int firstFrame, lastFrame;
            public List<Sample> samples = new List<Sample>();
        }
        private static readonly float[] CaptureTimes = { .4f, 1.6f, 3f, 4.65f, 5.65f, 6.9f };
        private static CaptureReport _report;
        private static KamabokoProductionCycle _cycle;
        private static Transform _fish, _product, _lid;
        private static Transform[] _steam;
        private static KamabokoPenguinWorker _worker;
        private static int _captureIndex, _lastFrame, _finishStep;
        private static float _started, _stoppedAt, _frozenCycle;
        private static Vector3 _frozenProduct;
        private static Vector3 _frozenWorker;
        private static float _frozenWorkerAnimation;

        public static void StartCapture()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Run in the Kamaboko review scene's Play Mode.");
            if (_report != null) throw new InvalidOperationException("Capture already active.");
            _cycle = UnityEngine.Object.FindAnyObjectByType<KamabokoProductionCycle>();
            _worker = UnityEngine.Object.FindAnyObjectByType<KamabokoPenguinWorker>();
            Transform Part(string name) => _cycle.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            _fish = Part("InputFish"); _product = Part("OutputKamaboko"); _lid = Part("SteamerLid");
            _steam = Enumerable.Range(0, 3).Select(i => Part("SteamPuff" + i)).ToArray();
            _report = new CaptureReport { unity = Application.unityVersion, playMode = true, firstFrame = Time.frameCount };
            _captureIndex = 0; _finishStep = 0; _lastFrame = -1; _started = Time.time;
            _cycle.SetRunning(true); _cycle.RestartCycle();
            Directory.CreateDirectory("ArtSource/ProductionIsland_v002/Review");
            EditorApplication.update += Observe;
        }

        private static void Observe()
        {
            if (_report == null) return;
            if (!EditorApplication.isPlaying || _cycle == null || Time.time - _started > 13f)
            {
                SaveReport(); return;
            }
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            if (_captureIndex < CaptureTimes.Length)
            {
                if (_cycle.CycleTime < CaptureTimes[_captureIndex]) return;
                var image = "ArtSource/ProductionIsland_v002/Review/" + _captureIndex + "-" + _cycle.Stage + ".png";
                ScreenCapture.CaptureScreenshot(image);
                _report.samples.Add(new Sample { stage = _cycle.Stage.ToString(), image = image, frame = Time.frameCount, time = _cycle.CycleTime,
                    fishVisible = _fish.gameObject.activeSelf, productVisible = _product.gameObject.activeSelf, steamVisible = _steam.Any(t => t.gameObject.activeSelf),
                    fish = _fish.position, product = _product.position, lid = _lid.position,
                    worker = _worker.transform.position, workerActivity = PenguinAnimatorStates.NameOf(_worker.Activity),
                    handToItem = Vector3.Distance(_worker.CarrySocket.position, _captureIndex == 0 ? _fish.position : _product.position) });
                _captureIndex++;
                return;
            }
            if (_finishStep == 0)
            {
                _cycle.SetRunning(false); _stoppedAt = Time.time; _frozenCycle = _cycle.CycleTime; _frozenProduct = _product.position;
                _frozenWorker = _worker.transform.position; _frozenWorkerAnimation = _worker.PoseTime;
                _finishStep = 1; return;
            }
            if (_finishStep == 1)
            {
                if (Time.time - _stoppedAt < .35f) return;
                _report.stopFreezes = Mathf.Abs(_cycle.CycleTime - _frozenCycle) < .00001f && Vector3.Distance(_product.position, _frozenProduct) < .00001f;
                _report.workerFreezes = Vector3.Distance(_worker.transform.position, _frozenWorker) < .00001f
                    && Mathf.Abs(_worker.PoseTime - _frozenWorkerAnimation) < .001f;
                _cycle.SetRunning(true); _cycle.RestartCycle(); _stoppedAt = Time.time; _finishStep = 2; return;
            }
            if (Time.time - _stoppedAt < .15f) return;
            _report.restartWorks = _cycle.CycleTime > 0 && _cycle.CycleTime < .5f && _fish.gameObject.activeSelf && !_product.gameObject.activeSelf;
            _report.allStages = _report.samples.Select(s => s.stage).Distinct().Count() == 6;
            _report.readableSequence = _report.samples.Take(2).All(s => s.fishVisible && !s.productVisible) && _report.samples.Skip(2).All(s => !s.fishVisible)
                && _report.samples.Skip(3).All(s => s.productVisible) && _report.samples.Count(s => s.steamVisible) == 1 && _report.samples[2].steamVisible;
            _report.worldMotionInBounds = _report.samples.All(s => s.lid.y > .65f && s.lid.y < 1.12f && s.fish.y > .4f && s.fish.y < .9f && Mathf.Abs(s.product.x) < .8f && Mathf.Abs(s.product.z) < .8f)
                && Vector3.Distance(_report.samples[5].product, new Vector3(.65f, .55f, -.4f)) < .01f;
            _report.workerInvolved = _report.samples[0].workerActivity == nameof(PenguinActivity.CarryWalk) && _report.samples[0].handToItem < .08f
                && _report.samples[1].workerActivity == nameof(PenguinActivity.WorkLoop) && _report.samples[2].workerActivity == nameof(PenguinActivity.Idle)
                && _report.samples[3].workerActivity == nameof(PenguinActivity.WorkLoop) && _report.samples[4].workerActivity == nameof(PenguinActivity.CarryWalk) && _report.samples[4].handToItem < .08f;
            SaveReport();
        }

        private static void SaveReport()
        {
            EditorApplication.update -= Observe;
            _report.lastFrame = Time.frameCount;
            File.WriteAllText("ArtSource/ProductionIsland_v002/Review/motion-verification.json", JsonUtility.ToJson(_report, true));
            _report = null;
        }

        [MenuItem("Production Island/Build Kamaboko Design Revision")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            var path = Folder + "/Building_Kamaboko_2x2_v002.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate Kamaboko v002 first.");
            importer.globalScale = 1f; importer.useFileScale = true; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit required.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Running.mat");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, Folder + "/Running.mat"); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FacilityRevisionBuilder.SharedFolder + "/ProductionIsland_ColorAtlas_v002.png"));
            mat.SetColor(ShaderIds.BaseColor, Color.white); mat.SetFloat(ShaderIds.Smoothness, .12f); EditorUtility.SetDirty(mat);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                go.name = "Building_Kamaboko_2x2_v002";
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                Transform Part(string name) => go.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
                var cycle = go.AddComponent<KamabokoProductionCycle>();
                cycle.Configure(Part("InputFish"), Part("OutputKamaboko"), Part("SteamerLid"), Part("ChamberCenter"), Enumerable.Range(0, 3).Select(i => Part("SteamPuff" + i)).ToArray());
                BuildWorker(go, scene, cycle, Part("InputFish"), Part("OutputKamaboko"), Part("SteamerLid"), Part("ChamberCenter"));
                PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Building_Kamaboko_2x2_v002.prefab");
                var cameraGo = new GameObject("ReviewCamera"); SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.43f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.90f, .96f, 1f);
                camera.transform.position = new Vector3(2.7f, 2.5f, -4.5f); camera.transform.LookAt(new Vector3(0, .7f, 0));
                var lightGo = new GameObject("ReviewSun"); SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(40, -30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(".local-tools"); File.WriteAllText(".local-tools/kamaboko-build-result.txt", "PASS: v002 prefab and isolated review scene built.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void BuildWorker(GameObject building, Scene scene, KamabokoProductionCycle cycle,
            Transform fish, Transform product, Transform lid, Transform chamber)
        {
            var root = new GameObject("PenguinChef"); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(building.transform, true); root.transform.position = new Vector3(-.65f, .14f, 0);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * .01f; // Building FBX scale=100; worker uses world metres.
            var visual = new GameObject("PenguinVisual").transform; visual.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Penguin_v002/Penguin_Static_v002.fbx"), scene);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(visual, false); model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(270, 0, 0); model.transform.localScale = Vector3.one * 80f;
            Transform Part(string name) => model.GetComponentsInChildren<Transform>().First(t => t.name == name);
            Transform JointPivot(string name, Transform part, Vector3 position)
            {
                var pivot = new GameObject(name).transform; pivot.SetParent(visual, false);
                pivot.localPosition = position;
                part.SetParent(pivot, true); return pivot;
            }
            var leftWing = JointPivot("LeftFlipperPivot", Part("PG_Wing_L"), new Vector3(.20f, .51f, 0));
            var rightWing = JointPivot("RightFlipperPivot", Part("PG_Wing_R"), new Vector3(-.20f, .51f, 0));
            var leftFoot = JointPivot("LeftFootPivot", Part("PG_Foot_L"), new Vector3(.072f, .033f, .08f));
            var rightFoot = JointPivot("RightFootPivot", Part("PG_Foot_R"), new Vector3(-.08f, .033f, .08f));
            var socket = new GameObject("CarrySocket").transform; socket.SetParent(root.transform, false);
            socket.localPosition = new Vector3(0, .45f, .4f);
            var white = SolidMaterial("ChefWhite", Color.white);
            var pink = SolidMaterial("ChefPink", new Color(1f, .65f, .76f));
            var wood = SolidMaterial("ChefWood", new Color(.58f, .32f, .15f));
            Primitive("ChefHatBand", PrimitiveType.Cylinder, visual, new Vector3(0, .77f, .02f), new Vector3(.27f, .025f, .24f), pink);
            Primitive("ChefHatTop", PrimitiveType.Sphere, visual, new Vector3(0, .86f, .02f), new Vector3(.34f, .19f, .29f), white);
            var handle = Primitive("LidHandle", PrimitiveType.Cylinder, root.transform, Vector3.zero, Vector3.one, wood);
            handle.SetActive(false);
            root.AddComponent<KamabokoPenguinWorker>().Configure(cycle, visual, leftWing, rightWing, leftFoot, rightFoot, fish, product, lid, chamber, socket, handle.transform);
        }

        private static Material SolidMaterial(string name, Color color)
        {
            var path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor(ShaderIds.BaseColor, color); material.SetFloat(ShaderIds.Smoothness, .1f); EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
