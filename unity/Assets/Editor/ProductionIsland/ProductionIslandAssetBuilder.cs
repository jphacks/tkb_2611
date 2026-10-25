using System;
using System.IO;
using System.Linq;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Creates native Unity assets through Editor APIs; safe to rerun.</summary>
    public static class ProductionIslandAssetBuilder
    {
        private const string Pack = "Assets/ProductionIsland";
        public const string SharedModelFolder = "Assets/3Dmodel/ProductionIsland_v001";
        private static readonly string[] States = { "Normal", "Flowing", "Blocked", "Selected", "Drawing" };
        private static readonly Color Blue = new Color(31f / 255, 139f / 255, 234f / 255);
        private static readonly Color Red = new Color(229f / 255, 83f / 255, 75f / 255);

        public static void BuildAndReport()
        {
            const string report = ".local-tools/production-island-build-result.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, "RUNNING");
            try
            {
                Build();
                File.WriteAllText(report, "PASS: native assets generated; materials, prefab references and four penguin clips verified.");
            }
            catch (Exception exception)
            {
                File.WriteAllText(report, "FAIL: " + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Production Island/Build Asset Pack")]
        public static void Build()
        {
            Directory.CreateDirectory(Pack + "/Materials");
            Directory.CreateDirectory(Pack + "/Prefabs");
            Directory.CreateDirectory(Pack + "/Animations");
            Directory.CreateDirectory("Assets/VFX/ProductionIsland/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("URP Lit is required.");
            var shared = SharedModelMaterials();
            var particle = MaterialAt(Pack + "/Materials/Particle.mat", "Universal Render Pipeline/Particles/Unlit", Color.white);
            particle.SetFloat("_Surface", 1);
            particle.SetFloat("_Blend", 0);
            particle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particle.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            particle.SetFloat("_ZWrite", 0);
            particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particle.renderQueue = 3000;
            particle.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VFX/ProductionIsland/Textures/Particle_v001.png"));
            var shard = new Material(particle);
            shard.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VFX/ProductionIsland/Textures/IceShard_v001.png"));
            SaveAsset(shard, Pack + "/Materials/IceShard.mat");

            var files = Directory.GetFiles("Assets/3Dmodel", "*_v001.fbx", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).StartsWith("Building_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Tile_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Shore_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Harbor_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Marker_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Item_", StringComparison.Ordinal)
                    || Path.GetFileName(path).StartsWith("Link_", StringComparison.Ordinal)).OrderBy(path => path).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("Generate FBX files before running the builder.");
            foreach (var rawPath in files)
            {
                var path = rawPath.Replace('\\', '/');
                var folder = Path.GetDirectoryName(path).Replace('\\', '/');
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                var active = shared.running;
                var stopped = shared.stopped;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                go.name = Path.GetFileNameWithoutExtension(path);
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(active, renderer.sharedMaterials.Length).ToArray();
                if (go.name.StartsWith("Tile_Sea", StringComparison.Ordinal) || go.name.StartsWith("Tile_Canal", StringComparison.Ordinal))
                {
                    var water = MaterialAt(folder + "/Water.mat", "ProjectPenguin/ProductionIsland/Water", go.name.Contains("Canal") ? new Color(.08f,.35f,.68f) : Blue);
                    foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.name == "Tile") renderer.sharedMaterial = water;
                }
                if (go.name.StartsWith("Building_", StringComparison.Ordinal) || go.name.StartsWith("Harbor_", StringComparison.Ordinal))
                {
                    if (go.name.Contains("Kamaboko") || go.name.Contains("Oden") || go.name.Contains("Dormitory"))
                    {
                        var steam = new GameObject("Steam"); steam.transform.SetParent(go.transform, false);
                        steam.transform.localPosition = new Vector3(0,.95f,0);
                        var ps = CreateParticle(steam, particle, Color.white, .11f, 1.1f, true, 8);
                        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .12f;
                    }
                    go.AddComponent<ProductionIslandMotion>().Configure(active, stopped);
                    if (go.name.Contains("Stage4") || go.name.Contains("Stage5") || go.name.Contains("Oden"))
                    {
                        var glow = MaterialAt(folder + "/Lantern.mat", lit.name, Color.white);
                        glow.SetTexture("_BaseMap", active.GetTexture("_BaseMap"));
                        glow.SetColor("_EmissionColor", new Color(.65f,.35f,.05f)); glow.EnableKeyword("_EMISSION");
                        foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>())
                            if (renderer.name.Contains("Lantern")) renderer.sharedMaterial = glow;
                    }
                }
                if (go.name.StartsWith("Link_Port_", StringComparison.Ordinal) && go.name.Contains("Selectable"))
                    go.AddComponent<ProductionIslandPortPulse>();
                PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + go.name + ".prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
            BuildLinks(lit, particle);
            BuildEffects(particle, shard);
            BuildPenguinController();
            BuildCatalog(files);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate(files);
            Debug.Log($"Production Island: {files.Length} model prefabs, five link states, seven VFX, four penguin clips. Validation passed.");
        }

        private static (Material running, Material stopped) SharedModelMaterials()
        {
            if (!AssetDatabase.IsValidFolder(SharedModelFolder))
                AssetDatabase.CreateFolder("Assets/3Dmodel", "ProductionIsland_v001");
            foreach (var file in new[] { "ProductionIsland_ColorAtlas_v001.png", "ProductionIsland_StoppedAtlas_v001.png" })
            {
                var target = SharedModelFolder + "/" + file;
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(target) != null) continue;
                var source = Directory.GetFiles("Assets/3Dmodel", file, SearchOption.AllDirectories)
                    .Select(path => path.Replace('\\', '/')).FirstOrDefault(path => path != target);
                if (source == null) throw new InvalidOperationException("Generate the shared atlas first: " + target);
                var error = AssetDatabase.MoveAsset(source, target);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            Material Shared(string state, string texture)
            {
                var material = MaterialAt(SharedModelFolder + "/" + state + ".mat", "Universal Render Pipeline/Lit", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SharedModelFolder + "/" + texture));
                material.SetFloat("_Smoothness", .16f);
                AssetDatabase.SaveAssetIfDirty(material);
                return material;
            }
            return (Shared("Running", "ProductionIsland_ColorAtlas_v001.png"), Shared("Stopped", "ProductionIsland_StoppedAtlas_v001.png"));
        }

        [MenuItem("Production Island/Consolidate v001 Shared Resources")]
        public static void ConsolidateSharedResources()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var folders = Directory.GetFiles("Assets/3Dmodel", "ProductionIsland_ColorAtlas_v001.png", SearchOption.AllDirectories)
                .Select(path => Path.GetDirectoryName(path).Replace('\\', '/')).Where(path => path != SharedModelFolder).ToArray();
            var shared = SharedModelMaterials();
            Material Retarget(Material material)
            {
                if (material == null || !material.HasProperty("_BaseMap")) return material;
                var texture = material.GetTexture("_BaseMap");
                if (texture == null) return material;
                if (texture.name == "ProductionIsland_ColorAtlas_v001")
                {
                    if (material.name == "Running") return shared.running;
                    material.SetTexture("_BaseMap", shared.running.GetTexture("_BaseMap"));
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                else if (texture.name == "ProductionIsland_StoppedAtlas_v001") return shared.stopped;
                return material;
            }
            void RetargetRoot(GameObject root)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(Retarget).ToArray();
                foreach (var motion in root.GetComponentsInChildren<ProductionIslandMotion>(true))
                    motion.Configure(shared.running, shared.stopped);
            }
            foreach (var folder in folders)
            foreach (var path in Directory.GetFiles(folder, "*.prefab"))
            {
                var prefabPath = path.Replace('\\', '/');
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try { RetargetRoot(root); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            const string catalog = "Assets/Scenes/ProductionIslandAssetCatalog.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(catalog) != null)
            {
                var scene = EditorSceneManager.OpenScene(catalog, OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects()) RetargetRoot(root);
                    EditorSceneManager.SaveScene(scene);
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
            foreach (var folder in folders)
            foreach (var file in new[] { "ProductionIsland_ColorAtlas_v001.png", "ProductionIsland_StoppedAtlas_v001.png", "Running.mat", "Stopped.mat" })
            {
                var path = folder + "/" + file;
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && !AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not remove duplicated resource: " + path);
            }
            AssetDatabase.Refresh();
            Debug.Log($"v001: {folders.Length} model folders now use shared atlases and running/stopped materials.");
        }

        private static Material MaterialAt(string path, string shaderName, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor(ShaderIds.BaseColor, color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SaveAsset(UnityEngine.Object asset, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null) AssetDatabase.CreateAsset(asset, path);
            else { EditorUtility.CopySerialized(asset, existing); UnityEngine.Object.DestroyImmediate(asset); }
        }

        private static void BuildLinks(Shader lit, Material particle)
        {
            var materials = new Material[States.Length];
            for (var i = 0; i < States.Length; i++)
            {
                materials[i] = MaterialAt(Pack + "/Materials/Link_" + States[i] + ".mat", "ProjectPenguin/ProductionIsland/Line", i == 2 ? Red : i == 3 ? new Color(.71f,.86f,.97f) : Blue);
                materials[i].SetFloat("_Dashed", i == 4 ? 1f : 0f);
                materials[i].SetFloat("_Scroll", i == 1 ? .8f : 0f);
            }
            var beadMaterial = MaterialAt(Pack + "/Materials/FlowBead.mat", lit.name, Color.white);
            var colors = new[] { Blue, Color.white, new Color(.42f,.5f,.6f), new Color(.18f,.72f,.45f), new Color(1f,.7f,.78f), new Color(.66f,.41f,.25f), new Color(.96f,.65f,.14f), new Color(1f,.79f,.16f) };
            var names = new[] { "Fish", "Surimi", "Salt", "Kelp", "Kamaboko", "Chikuwa", "DriedFish", "Oden" };
            for (var i = 0; i < names.Length; i++)
            {
                var mat = new Material(particle); mat.SetColor(ShaderIds.BaseColor, colors[i]);
                SaveAsset(mat, Pack + "/Materials/FlowParticle_" + names[i] + ".mat");
            }
            for (var state = 0; state < States.Length; state++)
            {
                var root = new GameObject("Link_" + States[state]);
                var line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = true; line.widthMultiplier = state == 3 ? .12f : .08f;
                line.numCornerVertices = 5; line.numCapVertices = 5;
                line.textureMode = LineTextureMode.Tile; line.sharedMaterial = materials[state];
                line.positionCount = 4;
                line.SetPositions(new[] { new Vector3(-1,.16f,0), new Vector3(-.3f,.16f,0), new Vector3(.3f,.16f,.5f), new Vector3(1,.16f,.5f) });
                var arrowAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Link_Arrow_Normal_v001/Link_Arrow_Normal_v001.fbx");
                var arrow = (GameObject)PrefabUtility.InstantiatePrefab(arrowAsset); arrow.transform.SetParent(root.transform, false);
                foreach (var renderer in arrow.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = materials[state];
                var beads = new Transform[6];
                for (var b = 0; b < beads.Length; b++)
                {
                    var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere); bead.name = "FlowBead";
                    UnityEngine.Object.DestroyImmediate(bead.GetComponent<Collider>());
                    bead.transform.SetParent(root.transform, false); bead.transform.localScale = Vector3.one * .055f;
                    bead.GetComponent<Renderer>().sharedMaterial = beadMaterial; bead.SetActive(state == 1); beads[b] = bead.transform;
                }
                var view = root.AddComponent<ProductionIslandLink>(); view.Configure(materials, arrow.transform, beads);
                view.SetPath(new[] { new Vector3(-1,.16f,0), new Vector3(-.3f,.16f,0), new Vector3(.3f,.16f,.5f), new Vector3(1,.16f,.5f) });
                view.SetState((ProductionIslandLink.VisualState)state);
                PrefabUtility.SaveAsPrefabAsset(root, Pack + "/Prefabs/Link_" + States[state] + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem CreateParticle(GameObject go, Material material, Color color, float size, float duration, bool loop, int count)
        {
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = duration; main.loop = loop; main.startLifetime = duration * .75f;
            main.startSize = size; main.startSpeed = loop ? .12f : .6f; main.startColor = color; main.maxParticles = 96;
            if (loop) { var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.y = .18f; }
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission; emission.rateOverTime = loop ? count : 0;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) }, new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1) }); fade.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }

        private static void BuildEffects(Material particle, Material shard)
        {
            var names = new[] { "ProductionPlusOne", "BuildSnow_1x1", "BuildSnow_2x2", "LinkSpark", "MilestoneConfetti", "IceMelt", "PearlRing" };
            for (var i = 0; i < names.Length; i++)
            {
                var root = new GameObject(names[i]);
                var ps = CreateParticle(root, i == 5 ? shard : particle, i == 3 || i == 6 ? new Color(1f,.79f,.16f) : Color.white,
                    i == 5 ? .13f : .08f, i == 4 ? 2.4f : 1.2f, false, i == 4 ? 70 : i == 2 ? 40 : 20);
                var shape = ps.shape; shape.radius = i == 2 ? .85f : i == 1 ? .4f : .12f;
                if (i == 4) { var main = ps.main; main.startSpeed = 1.5f; main.gravityModifier = .25f; main.startColor = new ParticleSystem.MinMaxGradient(Blue, new Color(1f,.79f,.16f)); }
                if (i == 6) { shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .8f; shape.rotation = new Vector3(90f,0f,0f); var main = ps.main; main.startSpeed = .05f; }
                if (i == 5) { var main = ps.main; main.startSpeed = 1.2f; main.gravityModifier = .6f; }
                if (i == 0)
                {
                    var visual = new GameObject("FloatingReward"); visual.transform.SetParent(root.transform, false);
                    var text = new GameObject("PlusOne"); text.transform.SetParent(visual.transform, false); text.transform.localPosition = Vector3.up * .18f;
                    var label = text.AddComponent<TextMesh>(); label.text = "+1"; label.fontSize = 48; label.characterSize = .05f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
                    var icon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Item_Fish_v001/Item_Fish_v001.prefab"));
                    icon.name = "ItemIcon_ReplaceForProducedItem"; icon.transform.SetParent(visual.transform, false); icon.transform.localPosition = new Vector3(-.14f,.2f,0);
                    root.AddComponent<ProductionIslandBurst>().Configure(visual.transform, false);
                }
                if (i == 5)
                {
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Tile_Ice_Sinking_v001/Tile_Ice_Sinking_v001.prefab"));
                    tile.transform.SetParent(root.transform, false);
                    root.AddComponent<ProductionIslandBurst>().Configure(tile.transform, true);
                }
                if (i == 4)
                {
                    // Item meshes mix with the paper particles, as requested.
                    var itemGo = new GameObject("ItemConfetti"); itemGo.transform.SetParent(root.transform, false);
                    var itemPs = CreateParticle(itemGo, AssetDatabase.LoadAssetAtPath<Material>(SharedModelFolder + "/Running.mat"), Color.white, .35f, 2.4f, false, 12);
                    var main = itemPs.main; main.startSpeed = 1.3f; main.gravityModifier = .25f;
                    var renderer = itemPs.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
                    var mesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Item_Fish_v001/Item_Fish_v001.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
                    renderer.mesh = mesh;
                }
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/VFX/ProductionIsland/Prefabs/" + names[i] + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildPenguinController()
        {
            const string fbx = "Assets/3Dmodel/Penguin_Production_v001/Penguin_Production_v001.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate penguin animations first.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            var clips = new[]
            {
                new ModelImporterClipAnimation { name = PenguinAnimatorStates.NameOf(PenguinActivity.CarryWalk), firstFrame = 0, lastFrame = 48, loopTime = true, loopPose = true },
                new ModelImporterClipAnimation { name = PenguinAnimatorStates.NameOf(PenguinActivity.WorkLoop), firstFrame = 60, lastFrame = 108, loopTime = true, loopPose = true },
                new ModelImporterClipAnimation { name = PenguinAnimatorStates.NameOf(PenguinActivity.Idle), firstFrame = 120, lastFrame = 192, loopTime = true, loopPose = true },
                new ModelImporterClipAnimation { name = PenguinAnimatorStates.NameOf(PenguinActivity.Cheer), firstFrame = 204, lastFrame = 252, loopTime = false }
            };
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var animations = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            if (animations.Length != 4) throw new InvalidOperationException($"Expected four penguin clips, got {animations.Length}.");
            const string path = Pack + "/Animations/PenguinProduction.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            foreach (var clip in animations) { var state = machine.AddState(clip.name); state.motion = clip; if (clip.name.Contains(PenguinAnimatorStates.NameOf(PenguinActivity.Idle))) machine.defaultState = state; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            go.name = "Penguin_Production_v001";
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var folder = Path.GetDirectoryName(fbx).Replace('\\', '/');
            var material = MaterialAt(folder + "/Running.mat", "Universal Render Pipeline/Lit", Color.white);
            var texturePath = Directory.GetFiles(folder, "*BaseColor*.png", SearchOption.AllDirectories).FirstOrDefault();
            if (texturePath == null) throw new InvalidOperationException("Penguin base-colour texture is missing.");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath.Replace('\\', '/')));
            var normalPath = Directory.GetFiles(folder, "*Normal*.png", SearchOption.AllDirectories).FirstOrDefault();
            if (normalPath != null)
            {
                normalPath = normalPath.Replace('\\', '/');
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                textureImporter.textureType = TextureImporterType.NormalMap; textureImporter.SaveAndReimport();
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); material.EnableKeyword("_NORMALMAP");
            }
            foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            var socket = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "CarrySocket");
            if (socket == null) throw new InvalidOperationException("Penguin CarrySocket missing.");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Item_Fish_v001/Item_Fish_v001.prefab"));
            item.transform.SetParent(socket, false); item.SetActive(false);
            go.AddComponent<ProductionIslandPenguinView>().Configure(animator, item);
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/3Dmodel/Penguin_Production_v001/Penguin_Production_v001.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void BuildCatalog(string[] models)
        {
            var previous = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);
            for (var i = 0; i < models.Length; i++)
            {
                var folder = Path.GetDirectoryName(models[i]).Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(models[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); go.transform.position = new Vector3(i % 9 * 2.5f, 0, i / 9 * 2.6f);
            }
            for (var i = 0; i < States.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "/Prefabs/Link_" + States[i] + ".prefab"), scene);
                var x = i * 3f;
                go.GetComponent<ProductionIslandLink>().SetPath(new[] { new Vector3(x, .16f, 16f), new Vector3(x + .6f, .16f, 16f), new Vector3(x + 1.2f, .16f, 16.6f), new Vector3(x + 2f, .16f, 16.6f) });
            }
            var effects = Directory.GetFiles("Assets/VFX/ProductionIsland/Prefabs", "*.prefab").OrderBy(path => path).ToArray();
            for (var i = 0; i < effects.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(effects[i].Replace('\\', '/')), scene);
                go.transform.position = new Vector3(i * 2.5f, 0, 19f);
            }
            var penguin = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Penguin_Production_v001/Penguin_Production_v001.prefab"), scene);
            penguin.transform.position = new Vector3(19f, 0, 19f);
            var cameraGo = new GameObject("CatalogCamera"); var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 14; camera.backgroundColor = new Color(.93f,.97f,1); camera.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.transform.position = new Vector3(10,27,-18); cameraGo.transform.LookAt(new Vector3(10,0,10));
            var lightGo = new GameObject("CatalogSun"); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; lightGo.transform.rotation = Quaternion.Euler(45,-30,0);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ProductionIslandAssetCatalog.unity");
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) EditorSceneManager.SetActiveScene(previous);
        }

        private static void Validate(string[] models)
        {
            foreach (var raw in models)
            {
                var path = raw.Replace('\\', '/');
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.ChangeExtension(path, ".prefab"));
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterials.Any(m => m == null)) throw new InvalidOperationException("Missing material: " + path);
                    if (renderer.sharedMaterials.Any(m => ShaderUtil.ShaderHasError(m.shader))) throw new InvalidOperationException("Shader compile error: " + path);
                }
            }
            var warehouse = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3Dmodel/Building_Warehouse_1x1_v001/Building_Warehouse_1x1_v001.prefab");
            var door = warehouse.GetComponentsInChildren<Transform>().First(t => t.name == "Door_Anim");
            if (door.position.z >= 0f) throw new InvalidOperationException("Building front must point to Unity -Z.");
            var penguinClips = AssetDatabase.LoadAllAssetsAtPath("Assets/3Dmodel/Penguin_Production_v001/Penguin_Production_v001.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            foreach (var clip in penguinClips)
            {
                if (clip.length < 1f || AnimationUtility.GetCurveBindings(clip).Length == 0)
                    throw new InvalidOperationException("Empty penguin animation: " + clip.name);
            }
            foreach (var name in States)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "/Prefabs/Link_" + name + ".prefab") == null)
                    throw new InvalidOperationException("Missing link state: " + name);
        }
    }
}
