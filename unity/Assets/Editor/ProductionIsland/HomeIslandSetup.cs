using System;
using System.Linq;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.World;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using IslandKind = ProjectPenguin.Domain.Island.FacilityKind;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>
    /// 生産の島のホーム (ProductionIslandHome.unity) を組み立てる。シーンが無ければ HomeWorld.unity を複製して作る
    /// (HomeWorld.unity 自体は変えない)。シーンの YAML を手で書かずに済むよう、参照の設定もすべてここで行う。
    /// 何度実行しても同じ状態になる。
    /// </summary>
    public static class HomeIslandSetup
    {
        public const string ScenePath = "Assets/Scenes/ProductionIslandHome.unity";
        private const string SourceScenePath = "Assets/Scenes/HomeWorld.unity";
        private const string MaterialFolder = "Assets/ProductionIsland/Materials";

        /// <summary>足踏みのアニメーションを持つペンギン (約 2 秒のループ)。歩くペンギンは全員これにそろえる。</summary>
        private const string WaddleModelPath = "Assets/3Dmodel/Penguin_v001/Penguin_Waddle_1p3m.fbx";

        private const string WaddleControllerPath = "Assets/3Dmodel/Penguin_v001/Penguin_Waddle.controller";

        /// <summary>HomeWorld の Penguin1 (足踏みしている 1 羽) の Model の下の置き方。FBX の単位が静止版と違うので大きさも違う。</summary>
        private static readonly Vector3 WaddleLocalPosition = new Vector3(0f, 0.269f, -0.097f);

        private static readonly Vector3 WaddleLocalEuler = new Vector3(270f, 0f, 0f);
        private const float WaddleLocalScale = 11.887f;

        [MenuItem("Production Island/Setup Home Island")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Play Mode を止めてから実行してください。");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && !AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                throw new InvalidOperationException($"{SourceScenePath} を {ScenePath} に複製できませんでした。");
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var game = UnityEngine.Object.FindAnyObjectByType<GameStateSystem>();
            var appUi = GameObject.Find("AppUI");
            var camera = Camera.main;
            if (game == null || appUi == null || camera == null)
            {
                throw new InvalidOperationException("GameState・AppUI・Main Camera が見つかりません。");
            }

            HideOldWorld();
            PlaceHomeIceberg(game);
            var island = SetupIsland(game);
            var rig = Ensure<IslandCameraRig>(camera.gameObject);
            Set(rig, "_island", island.transform);
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("_distance").floatValue = 16f;
            rigSo.FindProperty("_homeOffset").vector3Value = new Vector3(0f, 0f, 0.4f);
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            var interaction = Ensure<IslandInteraction>(island.gameObject);
            Set(interaction, "_game", game);
            Set(interaction, "_board", island);
            Set(interaction, "_rig", rig);
            Set(interaction, "_ui", appUi.GetComponent<UIDocument>());
            Set(interaction, "_okMaterial", Highlight("IslandHighlight_Ok", new Color(0.18f, 0.72f, 0.45f, 0.55f)));
            Set(interaction, "_ngMaterial", Highlight("IslandHighlight_Ng", new Color(0.9f, 0.33f, 0.29f, 0.55f)));
            Set(interaction, "_selectMaterial", Highlight("IslandHighlight_Select", new Color(1f, 0.79f, 0.16f, 0.6f)));

            var penguins = Ensure<IslandPenguinsView>(island.gameObject);
            Set(penguins, "_game", game);
            Set(penguins, "_board", island);
            var worldRoot = GameObject.Find("World")?.transform;
            var template = worldRoot != null ? FindUnder(worldRoot, "Penguins")?.GetChild(0) : null;
            Set(penguins, "_template", template != null ? template.gameObject : null);

            SetupIce(island, game, interaction);
            SetupUi(appUi, game, interaction);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            PutFirstInBuild();
            Debug.Log("HomeIslandSetup: ホームに生産の島を組み込みました。");
        }

        private static void HideOldWorld()
        {
            var world = GameObject.Find("World");
            if (world == null)
            {
                return;
            }

            var iceView = world.GetComponent<IceWorldView>();
            if (iceView != null)
            {
                UnityEngine.Object.DestroyImmediate(iceView);
            }

            // HomeWorld の試作の箱 (小屋・流氷・遠くの氷山) は隠す。小屋の乗った氷山とペンギンは PlaceHomeIceberg で島の奥に置き直す。
            foreach (var name in new[] { "House", "IceFloes", "Icebergs" })
            {
                var t = world.transform.Find(name);
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                }
            }

            // 海の板は陸 (0) より下げ、氷の崖が見えるようにする (IslandBoardView._waterLevel と同じ高さ)。
            var sea = world.transform.Find("Sea");
            if (sea != null)
            {
                sea.localPosition = new Vector3(0f, -0.35f, 0f);
                sea.localScale = new Vector3(60f, 1f, 60f);
            }
        }

        /// <summary>HomeWorld の氷山 (小屋つき) を縮めて置く、島の奥の位置と大きさ。</summary>
        /// <summary>小屋が上のカードに隠れず、島の左奥に見える位置 (縦長の画面で確かめた値)。</summary>
        private static readonly Vector3 HomeIcebergPosition = new Vector3(-4.9f, -0.35f, 7.9f);

        private const float HomeIcebergScale = 0.3f;

        /// <summary>HomeWorld の大きなペンギンのもとの位置 (隠すときに戻しておく)。</summary>
        private static readonly Vector3 BigPenguinHome = new Vector3(0f, 0.27f, -0.1f);

        /// <summary>
        /// HomeWorld の良いところ (小屋の乗った氷山・歩くペンギン・氷の量で氷山が育ち縮む動き) を、島の奥に置き直す。
        /// 盤面の外なので、島の規則には関わらない。何度実行しても同じ状態になる。
        /// </summary>
        private static void PlaceHomeIceberg(GameStateSystem game)
        {
            var world = GameObject.Find("World");
            if (world == null)
            {
                return;
            }

            var home = world.transform.Find("HomeIceberg");
            if (home == null)
            {
                home = new GameObject("HomeIceberg").transform;
                home.SetParent(world.transform, false);
            }

            home.localPosition = HomeIcebergPosition;
            home.localRotation = Quaternion.identity;
            home.localScale = Vector3.one * HomeIcebergScale;

            var iceberg = FindUnder(world.transform, "IcebergWithCabin");
            var penguins = FindUnder(world.transform, "Penguins");
            foreach (var t in new[] { iceberg, penguins })
            {
                if (t == null)
                {
                    continue;
                }

                // HomeWorld では氷山が原点にあったので、ローカルの位置をそのまま残して付け替える。
                if (t.parent != home)
                {
                    t.SetParent(home, false);
                }

                t.gameObject.SetActive(true);
            }

            // HomeWorld の大きなペンギン (原点に立つ) は、島の施設と重なるので隠す。小屋の氷山の上は小さなペンギンたちが歩く。
            // HomeWorld には同じ名前のものが 2 つある (Animator 付きと、置いただけのもの) ので、どちらも隠す。
            // 歩くペンギン (Penguins の下) の足踏みのモデルも同じ名前なので、それは除く (除き忘れると Penguin1 だけ姿が消える)。
            foreach (var big in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (big.name != "Penguin_Waddle_1p3m" || IsUnderPenguins(big))
                {
                    continue;
                }

                if (big.parent == home)
                {
                    big.SetParent(null, false);
                    big.position = BigPenguinHome;
                }

                big.gameObject.SetActive(false);
            }

            if (penguins != null)
            {
                UseWaddlePenguins(penguins);
            }

            if (iceberg == null)
            {
                Debug.LogWarning("HomeIslandSetup: IcebergWithCabin が見つかりません。");
                return;
            }

            var view = Ensure<IceWorldView>(world);
            var so = new SerializedObject(view);
            so.FindProperty("_game").objectReferenceValue = game;
            so.FindProperty("_iceberg").objectReferenceValue = iceberg;
            so.FindProperty("_cabin").objectReferenceValue = iceberg.Find("Cabin_Root_GroundPivot");
            var penguinList = so.FindProperty("_penguins");
            penguinList.arraySize = penguins != null ? penguins.childCount : 0;
            for (var i = 0; i < penguinList.arraySize; i++)
            {
                penguinList.GetArrayElementAtIndex(i).objectReferenceValue = penguins.GetChild(i);
            }

            var cracks = Enumerable.Range(1, 4).Select(i => iceberg.Find($"SurfaceFissure_0{i}")).Where(t => t != null).ToArray();
            var crackList = so.FindProperty("_cracks");
            crackList.arraySize = cracks.Length;
            for (var i = 0; i < cracks.Length; i++)
            {
                crackList.GetArrayElementAtIndex(i).objectReferenceValue = cracks[i].gameObject;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool IsUnderPenguins(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name == "Penguins")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 氷山を歩くペンギン (と、それを雛形に島を歩くペンギン) を、全員足踏みのアニメーション付きのモデルにそろえる。
        /// HomeWorld では Penguin1 だけが足踏みし、ほかは静止モデルを滑らせていた。足の動きがそろわないよう、再生位置もずらす。
        /// </summary>
        private static void UseWaddlePenguins(Transform penguins)
        {
            var waddle = AssetDatabase.LoadAssetAtPath<GameObject>(WaddleModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WaddleControllerPath);
            if (waddle == null || controller == null)
            {
                Debug.LogWarning("HomeIslandSetup: 足踏みのペンギンのモデルかコントローラーが見つかりません。");
                return;
            }

            foreach (Transform penguin in penguins)
            {
                var model = penguin.Find("Model");
                if (model == null)
                {
                    model = new GameObject("Model").transform;
                    model.SetParent(penguin, false);
                }

                var keep = model.childCount == 1 && PrefabUtility.GetCorrespondingObjectFromSource(model.GetChild(0).gameObject) == waddle
                    ? model.GetChild(0).gameObject
                    : null;
                for (var i = model.childCount - 1; i >= 0; i--)
                {
                    if (model.GetChild(i).gameObject != keep)
                    {
                        UnityEngine.Object.DestroyImmediate(model.GetChild(i).gameObject);
                    }
                }

                if (keep == null)
                {
                    keep = (GameObject)PrefabUtility.InstantiatePrefab(waddle, penguin.gameObject.scene);
                    keep.transform.SetParent(model, false);
                }

                keep.SetActive(true);
                keep.transform.localPosition = WaddleLocalPosition;
                keep.transform.localRotation = Quaternion.Euler(WaddleLocalEuler);
                keep.transform.localScale = Vector3.one * WaddleLocalScale;
                var animator = Ensure<Animator>(keep);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Ensure<AnimatorPhaseOffset>(penguin.gameObject);
            }
        }

        private static Transform FindUnder(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t.parent != null && (t.parent == root || t.parent.name == "HomeIceberg"))
                {
                    return t;
                }
            }

            return null;
        }

        private static IslandBoardView SetupIsland(GameStateSystem game)
        {
            var go = GameObject.Find("Island");
            if (go == null)
            {
                go = new GameObject("Island");
            }

            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var view = Ensure<IslandBoardView>(go);
            Set(view, "_game", game);
            Set(view, "_baseTile", Model("Tile_Ice_Core_v001"));
            Set(view, "_expandedTile", Model("Tile_Ice_Expanded_v001"));
            Set(view, "_crackSmallTile", Model("Tile_Ice_CrackSmall_v001"));
            Set(view, "_crackLargeTile", Model("Tile_Ice_CrackLarge_v001"));
            Set(view, "_sinkingTile", Model("Tile_Ice_Sinking_v001"));
            // 普通の海はシーンの海の板に任せ、タイルを置かない (盤面の境目が箱のように見えないように)。
            Set(view, "_seaTile", null);
            Set(view, "_seaExpandableTile", Model("Tile_Sea_Expandable_v001"));
            Set(view, "_canalTile", Model("Tile_Canal_TwoBanks_v001"));
            QualityAreaSetup.Assign(view);
            Set(view, "_buildVfxSmall", Vfx("BuildSnow_1x1"));
            Set(view, "_buildVfxLarge", Vfx("BuildSnow_2x2"));
            Set(view, "_sinkVfx", Vfx("IceMelt"));
            Set(view, "_plusOneVfx", Vfx("ProductionPlusOne"));
            Set(view, "_confettiVfx", Vfx("MilestoneConfetti"));
            Set(view, "_pearlVfx", Vfx("PearlRing"));

            var so = new SerializedObject(view);
            var harbors = so.FindProperty("_harborStages");
            harbors.arraySize = 6;
            for (var i = 0; i < 6; i++)
            {
                harbors.GetArrayElementAtIndex(i).objectReferenceValue = Model($"Harbor_Stage{i}_v002");
            }

            var kinds = (IslandKind[])Enum.GetValues(typeof(IslandKind));
            var facilities = so.FindProperty("_facilityPrefabs");
            facilities.arraySize = kinds.Length;
            for (var i = 0; i < kinds.Length; i++)
            {
                var element = facilities.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Kind").enumValueIndex = (int)kinds[i];
                element.FindPropertyRelative("Prefab").objectReferenceValue = FacilityModel(kinds[i]);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            GyojouHarborSetup.Assign(view);
            return view;
        }

        internal static GameObject FacilityModel(IslandKind kind)
        {
            var name = kind.ToString();
            var small = ProjectPenguin.Domain.Island.IslandCatalog.Get(kind).Size == 1;
            var model = Model($"Building_{name}_{(small ? "1x1" : "2x2")}_v002");
            if (model == null)
            {
                Debug.LogWarning($"HomeIslandSetup: {kind} のモデルが見つかりません。");
            }

            return model;
        }

        private static void SetupUi(GameObject appUi, GameStateSystem game, IslandInteraction interaction)
        {
            var navigator = appUi.GetComponent<ScreenNavigator>();
            Set(navigator, "_islandInteraction", interaction);

            var hud = Ensure<IslandHudPresenter>(appUi);
            var sheets = Ensure<IslandSheetPresenter>(appUi);
            var photo = Ensure<EcoPhotoFlowPresenter>(appUi);
            var mission = Ensure<MissionScreenPresenter>(appUi);
            var dex = Ensure<DexScreenPresenter>(appUi);
            var onboarding = Ensure<OnboardingPresenter>(appUi);
            var menu = Ensure<MenuScreenPresenter>(appUi);
            var record = Ensure<RecordScreenPresenter>(appUi);
            var build = Ensure<BuildScreenPresenter>(appUi);
            var harbor = Ensure<HarborScreenPresenter>(appUi);
            var facility = Ensure<FacilityScreenPresenter>(appUi);
            var production = Ensure<ProductionScreenPresenter>(appUi);

            Set(hud, "_game", game);
            Set(hud, "_navigator", navigator);
            Set(hud, "_sheets", sheets);
            Set(hud, "_build", build);
            Set(hud, "_harbor", harbor);
            Set(hud, "_onboarding", onboarding);

            Set(sheets, "_game", game);
            Set(sheets, "_interaction", interaction);
            Set(sheets, "_navigator", navigator);
            Set(sheets, "_hud", hud);
            Set(sheets, "_build", build);
            Set(sheets, "_harbor", harbor);
            Set(sheets, "_facility", facility);
            Set(sheets, "_production", production);

            Set(build, "_game", game);
            Set(build, "_navigator", navigator);
            Set(build, "_interaction", interaction);
            Set(build, "_sheets", sheets);
            Set(build, "_hud", hud);

            Set(harbor, "_game", game);
            Set(harbor, "_navigator", navigator);
            Set(harbor, "_hud", hud);

            Set(facility, "_game", game);
            Set(facility, "_navigator", navigator);
            Set(facility, "_interaction", interaction);
            Set(facility, "_hud", hud);

            Set(production, "_game", game);
            Set(production, "_navigator", navigator);

            Set(photo, "_game", game);
            Set(photo, "_navigator", navigator);
            Set(photo, "_camera", appUi.GetComponent<ReceiptCameraPreview>());
            Set(photo, "_analyzing", appUi.GetComponent<AnalyzingScreenPresenter>());
            Set(photo, "_hud", hud);

            Set(mission, "_game", game);
            Set(mission, "_navigator", navigator);
            Set(mission, "_hud", hud);

            Set(dex, "_game", game);
            Set(dex, "_navigator", navigator);

            Set(onboarding, "_game", game);
            Set(onboarding, "_navigator", navigator);

            Set(menu, "_game", game);
            Set(menu, "_navigator", navigator);
            Set(menu, "_onboarding", onboarding);

            Set(record, "_game", game);
            Set(record, "_navigator", navigator);
        }

        /// <summary>アプリを起動したときに開くシーンを、生産の島のホームにする (HomeWorld も残す)。</summary>
        private static void PutFirstInBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            // Editor の GetComponent は「無い」ときに偽の null を返すことがあるので ?? は使わない。
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name}.{field} がありません。");
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GameObject Model(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/3Dmodel/{name}/{name}.prefab");

        private static GameObject Vfx(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/VFX/ProductionIsland/Prefabs/{name}.prefab");

        private const string IcebergPath = "Assets/3Dmodel/Cabin_Field_v001/Iceberg_With_Cabin_v001.fbx";

        /// <summary>陸を HomeWorld の氷山と同じ見た目の 1 枚の氷で描く。島の外には遠くの氷山と浮かぶ氷を置く。</summary>
        private static void SetupIce(IslandBoardView island, GameStateSystem game, IslandInteraction interaction)
        {
            var boardSo = new SerializedObject(island);
            boardSo.FindProperty("_drawLandTiles").boolValue = false;
            boardSo.FindProperty("_waterLevel").floatValue = -0.35f;
            boardSo.FindProperty("_submergedDepth").floatValue = 0.8f;
            boardSo.ApplyModifiedPropertiesWithoutUndo();

            var ice = Ensure<IslandIceView>(island.gameObject);
            Set(ice, "_game", game);
            Set(ice, "_interaction", interaction);
            Set(ice, "_snow", IcebergMaterial("Snow_CoolFacet"));
            Set(ice, "_snowShade", IcebergMaterial("Snow_WindBlue"));
            Set(ice, "_gridLine", Highlight("IslandGridLine", new Color(1f, 1f, 1f, 0.7f)));

            var so = new SerializedObject(ice);
            var bands = new[] { "Snow_Porcelain", "Ice_Frosted", "Ice_Aqua", "Ice_Underwater" };
            var bandsProp = so.FindProperty("_cliffBands");
            bandsProp.arraySize = bands.Length;
            for (var i = 0; i < bands.Length; i++)
            {
                bandsProp.GetArrayElementAtIndex(i).objectReferenceValue = IcebergMaterial(bands[i]);
            }

            SetPiece(so.FindProperty("_fissure"), "SurfaceFissure_02");

            var decor = new (string Piece, Vector3 Position, float Scale, float Yaw)[]
            {
                // 奥の水路 (港の左右) に遠くの氷山、島の外の海に浮かぶ氷。どちらも建てられないマスか盤の外。
                // 左奥は HomeWorld の小屋の氷山 (PlaceHomeIceberg) を置くので、峰は右奥だけにする。
                ("IcePeak_03", new Vector3(3.7f, -0.2f, 5.5f), 0.3f, 40f),
                ("IcePeak_04", new Vector3(4.8f, -0.2f, 4.1f), 0.22f, 0f),
                ("IceBoulder_03", new Vector3(-5.7f, -0.3f, -1.6f), 0.5f, 30f),
                ("IceBoulder_06", new Vector3(5.8f, -0.3f, -3.0f), 0.45f, -50f),
                ("IceBoulder_02", new Vector3(3.9f, -0.3f, -5.9f), 0.5f, 80f),
                ("IceBoulder_07", new Vector3(-4.3f, -0.3f, -5.8f), 0.4f, 10f),
            };
            var decorProp = so.FindProperty("_decor");
            decorProp.arraySize = decor.Length;
            for (var i = 0; i < decor.Length; i++)
            {
                var e = decorProp.GetArrayElementAtIndex(i);
                SetPiece(e.FindPropertyRelative("Piece"), decor[i].Piece);
                e.FindPropertyRelative("Position").vector3Value = decor[i].Position;
                e.FindPropertyRelative("Scale").floatValue = decor[i].Scale;
                e.FindPropertyRelative("Yaw").floatValue = decor[i].Yaw;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material IcebergMaterial(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(IcebergPath))
            {
                if (o is Material m && m.name == name)
                {
                    return m;
                }
            }

            throw new InvalidOperationException($"{IcebergPath} にマテリアル {name} がありません。");
        }

        /// <summary>氷山の FBX の子 (形とマテリアル) を <see cref="IslandIceView.Piece"/> に入れる。</summary>
        private static void SetPiece(SerializedProperty piece, string childName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(IcebergPath);
            var child = model.transform.Find(childName);
            if (child == null)
            {
                throw new InvalidOperationException($"{IcebergPath} に {childName} がありません。");
            }

            piece.FindPropertyRelative("Mesh").objectReferenceValue = child.GetComponent<MeshFilter>().sharedMesh;
            var materials = child.GetComponent<MeshRenderer>().sharedMaterials;
            var prop = piece.FindPropertyRelative("Materials");
            prop.arraySize = materials.Length;
            for (var i = 0; i < materials.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            }
        }

        /// <summary>マスの強調に使う半透明の色 (URP Unlit)。無ければ作る。</summary>
        private static Material Highlight(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            // 半透明 (Surface Type = Transparent, Blend = Alpha)。
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor(ShaderIds.BaseColor, color);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
