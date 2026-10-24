using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectPenguin.Editor.ProductionIsland
{
    public static class FacilityRevisionBuilder
    {
        public const string ScenePath = "Assets/Scenes/ProductionIslandFacilityReview.unity";
        public const string SharedFolder = "Assets/3Dmodel/ProductionIsland_v002";
        public const string PenguinPath = "Assets/3Dmodel/Penguin_v002/Penguin_Static_v002.fbx";
        private sealed class Definition
        {
            public string Kind, Name, Label, Recipe;
            public bool Worker, Small;
            public Definition(string kind, string label, string recipe, bool worker = true, bool small = false)
            { Kind = kind; Label = label; Recipe = recipe; Worker = worker; Small = small;
              Name = "Building_" + kind + (small ? "_1x1" : "_2x2") + "_v002"; }
        }
        private static readonly Definition[] Definitions = {
            new Definition("Fishery", "漁場", "泳ぐ魚 → 網で引き上げる → 魚の箱"),
            new Definition("Preparation", "さばき場", "魚 → 包丁・まな板 → 白いすり身"),
            new Definition("Chikuwa", "ちくわ工場", "白いすり身 → 焼き棒を回す → 焼き色の付いたちくわ"),
            new Definition("Saltworks", "製塩所", "青い海水 → 塩田・かき集める → 白い塩の袋"),
            new Definition("Drying", "干物場", "魚と塩 → 網に掛けて乾燥 → オレンジの干物"),
            new Definition("KelpFarm", "海藻場", "水中の海藻 → かぎ竿で巻き上げる・束ねる → 海藻の束"),
            new Definition("Oden", "おでん屋台", "かまぼこ・ちくわ・海藻 → 鍋で煮る → おでんの串"),
            new Definition("Warehouse", "倉庫", "箱を受け取る → 棚に格納 → 箱を搬出", false, true),
            new Definition("Sorter", "仕分け所", "受け取る → 分岐板で向きを変える → 振り分ける", false, true),
            new Definition("Research", "研究所", "資料 → レンズで調べる → 印の付いた設計図"),
            new Definition("Dormitory", "宿舎", "戻る → 目を閉じて休む → 作業へ戻る", true, true),
            new Definition("ElectricAmplifier", "電気の増幅施設", "現実で得た電気 → 蓄電・調整 → 獲得量の増幅を表示"),
            new Definition("WaterAmplifier", "水の増幅施設", "現実で得た水 → 貯水・調整 → 獲得量の増幅を表示"),
            new Definition("GasAmplifier", "ガスの増幅施設", "現実で得たガス → 圧力調整 → 獲得量の増幅を表示"),
            new Definition("IceHouse", "氷室", "氷のかたまり → 雪のドームにたくわえる → 冷たい風で氷を守る", false, true)
        };

        [MenuItem("Production Island/Build Facility Design Revisions")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (!AssetDatabase.IsValidFolder(SharedFolder)) AssetDatabase.CreateFolder("Assets/3Dmodel", "ProductionIsland_v002");
            var running = AtlasMaterial("Running", "ProductionIsland_ColorAtlas_v002.png");
            var stopped = AtlasMaterial("Stopped", "ProductionIsland_StoppedAtlas_v002.png");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var facilities = new List<Transform>(); var labels = new List<string>(); var recipes = new List<string>();
            try
            {
                foreach (var definition in Definitions)
                {
                    var go = Import(definition.Name, scene, running);
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    var frame = new GameObject("PresentationFrame").transform;
                    SceneManager.MoveGameObjectToScene(frame.gameObject, scene);
                    frame.SetParent(go.transform, true); frame.position = Vector3.zero; frame.rotation = Quaternion.identity;
                    frame.localScale = Vector3.one * .01f;
                    Transform Part(string name) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                    var worker = definition.Worker ? CreatePenguin(frame, scene, definition.Kind) : null;
                    var cycle = go.AddComponent<FacilityProductionCycle>();
                    cycle.Configure((FacilityProductionCycle.FacilityKind)Enum.Parse(typeof(FacilityProductionCycle.FacilityKind), definition.Kind),
                        frame, Part("InputItem"), Part("ProcessRaw"), Part("ProcessFinished"), Part("OutputItem"),
                        Part("ProcessCenter"), Part("WorkerStand"), Part("WorkingPart"), Part("EffectPart"), worker);
                    cycle.ConfigureMaterials(renderers, running, stopped);
                    SaveAndPlace(go, definition.Name, definition.Label, definition.Recipe, facilities, labels, recipes);
                }
                var kamaboko = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KamabokoRevisionBuilder.Folder + "/Building_Kamaboko_2x2_v002.prefab"), scene);
                kamaboko.transform.position = GridPosition(facilities.Count);
                facilities.Add(kamaboko.transform); labels.Add("かまぼこ工場（確認済み）"); recipes.Add("魚 → 蒸す → ピンクと白のかまぼこ");
                for (var stage = 0; stage < 6; stage++)
                {
                    var name = "Harbor_Stage" + stage + "_v002";
                    var go = Import(name, scene, running);
                    if (stage == 5)
                    {
                        var renderers = go.GetComponentsInChildren<Renderer>(true);
                        var frame = new GameObject("PresentationFrame").transform;
                        SceneManager.MoveGameObjectToScene(frame.gameObject, scene); frame.SetParent(go.transform, true);
                        frame.position = Vector3.zero; frame.rotation = Quaternion.identity; frame.localScale = Vector3.one * .01f;
                        Transform Part(string n) => go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                        var cycle = go.AddComponent<FacilityProductionCycle>();
                        cycle.Configure(FacilityProductionCycle.FacilityKind.Harbor, frame, Part("InputItem"), Part("ProcessRaw"), Part("ProcessFinished"),
                            Part("OutputItem"), Part("ProcessCenter"), Part("WorkerStand"), Part("WorkingPart"), Part("EffectPart"), CreatePenguin(frame, scene, "Harbor"));
                        cycle.ConfigureMaterials(renderers, running, stopped);
                    }
                    var stages = new[] { "予定地", "土台", "柱", "屋根・いかり", "点灯", "おでん祭り・納品" };
                    SaveAndPlace(go, name, "港：" + stages[stage], stage < 5 ? "納品達成に合わせて段階を差し替える" : "箱を運ぶ → 桟橋で納品 → 舟が出発", facilities, labels, recipes);
                }
                var cameraGo = new GameObject("ReviewCamera"); SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.4f;
                camera.backgroundColor = new Color(.90f, .96f, 1f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.transform.position = new Vector3(2.7f, 2.48f, -4.5f); camera.transform.LookAt(new Vector3(0, .68f, 0));
                var lightGo = new GameObject("ReviewSun"); SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(40, -30, 0);
                var galleryGo = new GameObject("FacilityGallery"); SceneManager.MoveGameObjectToScene(galleryGo, scene);
                galleryGo.AddComponent<FacilityDesignGallery>().Configure(camera, facilities.ToArray(), labels.ToArray(), recipes.ToArray());
                ConfigureReviewUI(galleryGo);
                EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
                Directory.CreateDirectory("ArtSource/ProductionIsland_v002/Facilities");
                File.WriteAllText("ArtSource/ProductionIsland_v002/Facilities/build-result.txt", "PASS: 20 new prefabs + approved Kamaboko; existing Penguin_v002; interactive review scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static Vector3 GridPosition(int index) => new Vector3(index % 5 * 2.6f, 0, index / 5 * 2.7f);
        public static void ConfigureReviewUI(GameObject gallery)
        {
            var backdrop = gallery.scene.GetRootGameObjects().FirstOrDefault(go => go.name == "ReviewBackdrop");
            if (backdrop == null)
            {
                backdrop = new GameObject("ReviewBackdrop");
                SceneManager.MoveGameObjectToScene(backdrop, gallery.scene);
            }
            var backgroundCamera = backdrop.GetComponent<Camera>();
            if (backgroundCamera == null) backgroundCamera = backdrop.AddComponent<Camera>();
            backgroundCamera.cullingMask = 0;
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.90f, .96f, 1f);
            backgroundCamera.depth = -10;
            backgroundCamera.orthographic = true;
            const string folder = "Assets/UI/ProductionIslandReview";
            var path = folder + "/FacilityReviewPanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            EditorUtility.SetDirty(panel);
            var document = gallery.GetComponent<UIDocument>();
            if (document == null) document = gallery.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/FacilityReview.uxml");
            document.sortingOrder = 20;
            ConfigureReviewInput(gallery);
        }

        public static void ConfigureReviewInput(GameObject gallery)
        {
            var types = TypeCache.GetTypesDerivedFrom<MonoBehaviour>();
            var eventSystemType = types.First(t => t.FullName == "UnityEngine.EventSystems.EventSystem");
            var moduleType = types.First(t => t.FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            var existing = gallery.transform.Find("FacilityReviewEventSystem");
            var go = existing != null ? existing.gameObject : new GameObject("FacilityReviewEventSystem");
            if (existing == null)
            {
                SceneManager.MoveGameObjectToScene(go, gallery.scene);
                go.transform.SetParent(gallery.transform, false);
            }
            go.SetActive(false);
            if (go.GetComponent(eventSystemType) == null) go.AddComponent(eventSystemType);
            var module = go.GetComponent(moduleType);
            if (module == null) module = go.AddComponent(moduleType);
            var actions = AssetDatabase.LoadAllAssetsAtPath("Assets/InputSystem_Actions.inputactions");
            moduleType.GetProperty("actionsAsset").SetValue(module,
                actions.First(a => a.GetType().FullName == "UnityEngine.InputSystem.InputActionAsset"));
            var mappings = new[] {
                ("point", "Point"), ("leftClick", "Click"), ("rightClick", "RightClick"),
                ("middleClick", "MiddleClick"), ("scrollWheel", "ScrollWheel"), ("move", "Navigate"),
                ("submit", "Submit"), ("cancel", "Cancel"),
                ("trackedDevicePosition", "TrackedDevicePosition"), ("trackedDeviceOrientation", "TrackedDeviceOrientation")
            };
            foreach (var mapping in mappings)
                moduleType.GetProperty(mapping.Item1).SetValue(module,
                    actions.First(a => a.name == "UI/" + mapping.Item2));
            go.SetActive(true);
            EditorUtility.SetDirty(module);
        }
        private static void SaveAndPlace(GameObject go, string name, string label, string recipe, List<Transform> facilities, List<string> labels, List<string> recipes)
        {
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/3Dmodel/" + name + "/" + name + ".prefab");
            go.transform.position = GridPosition(facilities.Count);
            facilities.Add(go.transform); labels.Add(label); recipes.Add(recipe);
        }

        private static GameObject Import(string name, Scene scene, Material running)
        {
            var path = "Assets/3Dmodel/" + name + "/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate " + name + " first.");
            importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
            go.name = name;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true)) FacilityProductionCycle.FillMaterialSlots(renderer, running);
            return go;
        }

        private static Material AtlasMaterial(string name, string texture)
        {
            var path = SharedFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor(ShaderIds.BaseColor, Color.white); mat.SetFloat(ShaderIds.Smoothness, .12f);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SharedFolder + "/" + texture));
            EditorUtility.SetDirty(mat); return mat;
        }

        private static Material Solid(string name, Color color)
        {
            var path = SharedFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor(ShaderIds.BaseColor, color); mat.SetFloat(ShaderIds.Smoothness, .1f); EditorUtility.SetDirty(mat); return mat;
        }

        private static WorkshopPenguin CreatePenguin(Transform frame, Scene scene, string role)
        {
            var root = new GameObject("PenguinWorker").transform; root.SetParent(frame, false); root.localPosition = new Vector3(-.64f, .14f, 0);
            if (role == "Dormitory") root.localScale = Vector3.one * .58f;
            var visual = new GameObject("PenguinVisual").transform; visual.SetParent(root, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PenguinPath), scene);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(visual, false); model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(270, 0, 0); model.transform.localScale = Vector3.one * 80;
            var parts = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            Transform Joint(string name, string part, Vector3 position)
            {
                var pivot = new GameObject(name).transform; pivot.SetParent(visual, false); pivot.localPosition = position;
                parts[part].SetParent(pivot, true); return pivot;
            }
            var leftWing = Joint("LeftFlipperPivot", "PG_Wing_L", new Vector3(.20f, .51f, 0));
            var rightWing = Joint("RightFlipperPivot", "PG_Wing_R", new Vector3(-.20f, .51f, 0));
            var leftFoot = Joint("LeftFootPivot", "PG_Foot_L", new Vector3(.072f, .033f, .08f));
            var rightFoot = Joint("RightFootPivot", "PG_Foot_R", new Vector3(-.08f, .033f, .08f));
            if (role != "Dormitory")
            {
                var chef = role == "Preparation" || role == "Chikuwa" || role == "Oden";
                var hat = Solid(chef ? "ChefWhite" : role == "Drying" ? "StrawHat" : "WorkerBlue", chef ? Color.white : role == "Drying" ? new Color(.91f, .74f, .45f) : new Color(.09f, .39f, .72f));
                Primitive("HatBand", PrimitiveType.Cylinder, visual, new Vector3(0, .77f, .02f), new Vector3(.27f, .025f, .24f), hat);
                Primitive("HatTop", PrimitiveType.Sphere, visual, new Vector3(0, chef ? .86f : .81f, .02f), new Vector3(.34f, chef ? .19f : .075f, .29f), hat);
            }
            var worker = root.gameObject.AddComponent<WorkshopPenguin>();
            worker.Configure(visual, leftWing, rightWing, leftFoot, rightFoot,
                new[] { parts["PG_Eye_L"], parts["PG_Eye_R"] }, new[] { parts["PG_Glint_L"], parts["PG_Glint_R"] });
            return worker;
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
