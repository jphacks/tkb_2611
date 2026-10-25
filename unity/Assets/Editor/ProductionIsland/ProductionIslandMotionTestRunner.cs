using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Isolated, sampled production-method tests. Does not enter Play Mode or save scenes.</summary>
    public static class ProductionIslandMotionTestRunner
    {
        private const string Output = ".local-tools/production-island-motion-results";
        [Serializable] private sealed class Check { public string id, status, evidence; }
        [Serializable] private sealed class Report
        {
            public string utc, unity, mode = "PreviewScene: manually invoke production lifecycle methods; Animator.Update engine evaluation. Not a PlayMode/render acceptance test.";
            public bool playModeBefore, playModeAfter, sceneStatePreserved;
            public int frameStart, frameEnd;
            public float timeStart, timeEnd, observedDeltaTime;
            public List<Check> checks = new List<Check>();
        }
        private static Report _report;
        private static Scene _scene;
        private static IEnumerator _routine;
        private static string _before;
        private static double _next;
        private static bool _play;
        private static readonly List<GameObject> Spawned = new List<GameObject>();

        [MenuItem("Production Island/Tests/Run Isolated Motion Samples")]
        public static void Start()
        {
            if (_routine != null) throw new InvalidOperationException("Motion tests already running.");
            _report = new Report { utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, playModeBefore = EditorApplication.isPlaying };
            _report.frameStart = Time.frameCount; _report.timeStart = Time.time; _report.observedDeltaTime = Time.deltaTime;
            _play = EditorApplication.isPlaying;
            if (_play) _report.mode = "PlayMode: automatic production lifecycle/Update and Animator evaluated by Unity frames. Editor observer samples transforms; no manual runtime stepping.";
            _before = SceneState();
            _scene = _play ? SceneManager.GetActiveScene() : EditorSceneManager.NewPreviewScene();
            Spawned.Clear();
            _routine = Run();
            EditorApplication.update += Advance;
            AssemblyReloadEvents.beforeAssemblyReload += Abort;
            Directory.CreateDirectory(".local-tools");
            File.WriteAllText(Output + ".running", _report.utc);
        }

        private static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => { var s = SceneManager.GetSceneAt(i); return s.handle + ":" + s.path + ":" + s.isDirty; })) + " active=" + SceneManager.GetActiveScene().handle;
        private static void Advance()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + .025;
            try { if (!_routine.MoveNext()) Finish(); }
            catch (Exception e) { Add("HARNESS", false, e.ToString()); Finish(); }
        }
        private static void Abort() { if (_routine == null) return; _report.checks.Add(new Check { id = "HARNESS", status = "BLOCKED", evidence = "Assembly reload interrupted test." }); Finish(); }
        private static void Finish()
        {
            EditorApplication.update -= Advance;
            AssemblyReloadEvents.beforeAssemblyReload -= Abort;
            _routine = null;
            foreach (var go in Spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            Spawned.Clear();
            if (!_play && _scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _report.playModeAfter = EditorApplication.isPlaying;
            _report.frameEnd = Time.frameCount; _report.timeEnd = Time.time;
            _report.sceneStatePreserved = SceneState() == _before && _report.playModeAfter == _report.playModeBefore;
            Add("ISOLATION", _report.sceneStatePreserved, "Before=" + _before + "; after=" + SceneState());
            File.WriteAllText(Output + ".json", JsonUtility.ToJson(_report, true));
            File.WriteAllText(Output + ".md", "# 生産の島 動作サンプリング結果\n\nUTC: " + _report.utc + " / Unity " + _report.unity + "\n\n" + _report.mode + "\n\n| ID | 結果 | 実測 |\n|---|---|---|\n" + string.Join("\n", _report.checks.Select(c => "| " + c.id + " | " + c.status + " | " + c.evidence.Replace("\n", " ").Replace("|", "/") + " |")) + "\n");
            File.Delete(Output + ".running");
        }
        private static void Add(string id, bool pass, string evidence) => _report.checks.Add(new Check { id = id, status = pass ? "PASS" : "FAIL", evidence = evidence });
        private static void Block(string id, string evidence) => _report.checks.Add(new Check { id = id, status = "BLOCKED", evidence = evidence });
        private static void Call(Component component, string method) { if (!_play) component.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(component, null); }
        private static T Field<T>(Component component, string field) => (T)component.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(component);
        private static GameObject Spawn(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            var go = _play ? UnityEngine.Object.Instantiate(prefab) : (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            if (_play) SceneManager.MoveGameObjectToScene(go, _scene);
            Spawned.Add(go);
            return go;
        }
        private static float Pose(GameObject go) => go.GetComponentsInChildren<Transform>(true).Sum(t => t.localPosition.sqrMagnitude + t.localRotation.x * 3 + t.localRotation.y * 7 + t.localRotation.z * 11 + t.localScale.sqrMagnitude) + go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Sum(r.GetBlendShapeWeight));
        private static IEnumerator Tick(Component component, int count)
        { for (var i = 0; i < count; i++) { Call(component, "Update"); yield return null; } }
        private static float[] Snapshot(GameObject go) => go.GetComponentsInChildren<Transform>(true).SelectMany(t => new[] { t.localPosition.x, t.localPosition.y, t.localPosition.z, t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w, t.localScale.x, t.localScale.y, t.localScale.z }).ToArray();
        private static float Difference(float[] a, float[] b) => a.Select((v,i) => Mathf.Abs(v-b[i])).Max();
        private static IEnumerator Sample(Component component, GameObject go, int count, float[] baseline, List<float> changes)
        { for (var i=0;i<count;i++) { Call(component,"Update"); changes.Add(Difference(Snapshot(go),baseline)); yield return null; } }

        private static IEnumerator Run()
        {
            var buildings = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/3Dmodel" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => Path.GetFileName(p).StartsWith("Building_", StringComparison.Ordinal) || Path.GetFileName(p).StartsWith("Harbor_", StringComparison.Ordinal)).OrderBy(p => p).ToArray();
            foreach (var path in buildings)
            {
                var go = Spawn(path); var motion = go.GetComponent<ProductionIslandMotion>();
                if (motion == null) { Add("MOTION:" + go.name, false, "No motion component"); UnityEngine.Object.DestroyImmediate(go); continue; }
                Call(motion, "Awake");
                var parts = Field<Transform[]>(motion, "_parts"); var start = Snapshot(go); var changes = new List<float>();
                var ticks = Sample(motion,go,12,start,changes); while (ticks.MoveNext()) yield return null;
                var phase = Field<float>(motion, "_phase");
                if (phase <= 0) Block("MOTION:" + go.name, "Editor Time.deltaTime did not advance.");
                else if (parts.Length == 0) Block("MOTION:" + go.name, "No _Anim parts; motion assertion is not applicable.");
                else Add("MOTION:" + go.name, changes.Max() > .00001f, "parts=" + parts.Length + "; phase=" + phase + "; maxIndividualTransformDelta=" + changes.Max());
                var original = go.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterials).ToArray();
                motion.SetRunning(false); var stopped = Snapshot(go); ticks = Tick(motion, 6); while (ticks.MoveNext()) yield return null;
                Add("STOP:" + go.name, Difference(Snapshot(go),stopped) < .00001f && go.GetComponentsInChildren<ParticleSystem>(true).All(p => !p.isEmitting), "maxIndividualTransformDelta=" + Difference(Snapshot(go),stopped) + "; particles emission stopped");
                var stoppedMat = Field<Material>(motion, "_stoppedMaterial");
                Add("STOP_MATERIAL:" + go.name, go.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterials.All(m => m == stoppedMat)), "Every MeshRenderer slot compared against configured stopped material.");
                motion.SetRunning(true); changes.Clear(); ticks = Sample(motion,go,6,stopped,changes); while (ticks.MoveNext()) yield return null;
                Add("RESTORE_MATERIAL:" + go.name, go.GetComponentsInChildren<MeshRenderer>(true).Select((r,i) => r.sharedMaterials.SequenceEqual(original[i])).All(v => v), "Original material arrays restored.");
                if (parts.Length > 0 && phase > 0) Add("RESUME:" + go.name, changes.Max() > .00001f, "maxIndividualTransformDelta=" + changes.Max());
                if (go.name.Contains("Chikuwa"))
                {
                    motion.SetHighHeat(false); var low = parts.Where(p => p.name.StartsWith(ModelParts.FlameLow)).ToArray(); var high = parts.Where(p => p.name.StartsWith(ModelParts.FlameHigh)).ToArray();
                    var lowPass = low.Length > 0 && high.Length > 0 && low.All(p => p.gameObject.activeSelf) && high.All(p => !p.gameObject.activeSelf);
                    motion.SetHighHeat(true); Add("FLAME", lowPass && low.All(p => !p.gameObject.activeSelf) && high.All(p => p.gameObject.activeSelf), "lowParts=" + low.Length + "; highParts=" + high.Length);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
            var linkGo = Spawn("Assets/ProductionIsland/Prefabs/Link_Normal.prefab"); var link = linkGo.GetComponent<ProductionIslandLink>(); Call(link, "Awake");
            link.SetPath(new[] { Vector3.zero, Vector3.right * 4 }); link.SetFlow(Color.magenta, 1);
            var beads = Field<Transform[]>(link, "_beads"); Call(link, "Update"); var initial = beads[0].position; var tlink = Tick(link, 20); while (tlink.MoveNext()) yield return null;
            var slow = Mathf.Repeat(beads[0].position.x - initial.x,4f);
            link.SetFlow(Color.cyan, 3); initial = beads[0].position; tlink = Tick(link, 20); while (tlink.MoveNext()) yield return null;
            var fast = Mathf.Repeat(beads[0].position.x - initial.x,4f); var block = new MaterialPropertyBlock(); beads[0].GetComponent<Renderer>().GetPropertyBlock(block);
            Add("LINK_FLOW", slow > 0 && fast > slow && beads.Count(b => b.gameObject.activeSelf) == 6 && block.GetColor(ShaderIds.BaseColor) == Color.cyan, "xDelta@1=" + slow + "; xDelta@3=" + fast + "; count=" + beads.Count(b => b.gameObject.activeSelf) + "; tint=" + block.GetColor(ShaderIds.BaseColor));
            var arrow = Field<Transform>(link, "_arrow"); Add("LINK_DIRECTION", Vector3.Distance(arrow.position, Vector3.right * 4) < .0001f && Vector3.Dot(-arrow.forward, Vector3.right) > .999f, "end=" + arrow.position + "; directionDot=" + Vector3.Dot(-arrow.forward, Vector3.right));
            foreach(var rate in new[] { .5f, 2f, 10f, 0f, -1f })
            {
                link.SetFlow(Color.magenta,rate); var activeCount=beads.Count(b=>b.gameObject.activeSelf); var position=beads[0].position; var distanceBefore=Field<float>(link,"_distance"); var timeBefore=Time.time;
                tlink=Tick(link,6); while(tlink.MoveNext()) yield return null;
                var advance=Mathf.Repeat(Field<float>(link,"_distance")-distanceBefore,4f);
                Add("LINK_RATE:"+rate, activeCount==Mathf.Clamp(Mathf.CeilToInt(rate*2),0,6) && (rate>0?advance>0:position==beads[0].position), "rate="+rate+"; active="+activeCount+"; distanceAdvance="+advance+"; unityElapsed="+(Time.time-timeBefore));
            }
            var reversed = new[] { new Vector3(2,0,2),new Vector3(2,0,0),Vector3.zero };
            link.SetPath(reversed); link.SetFlow(Color.yellow,2); tlink=Tick(link,8); while(tlink.MoveNext()) yield return null;
            Add("LINK_BENT_REVERSED", Vector3.Distance(arrow.position,Vector3.zero)<.0001f && Vector3.Dot(-arrow.forward,Vector3.left)>.999f && beads.Where(b=>b.gameObject.activeSelf).All(b=>Mathf.Abs(b.position.x-2)<.002f || Mathf.Abs(b.position.z)<.002f), "end="+arrow.position+"; arrowDot="+Vector3.Dot(-arrow.forward,Vector3.left)+"; beadsOnBentSegments");
            link.SetFlow(Color.white,3);
            foreach (ProductionIslandLink.VisualState state in Enum.GetValues(typeof(ProductionIslandLink.VisualState)))
            { link.SetState(state); var line = linkGo.GetComponent<LineRenderer>(); Add("LINK_STATE:" + state, line.sharedMaterial.name == "Link_" + state && Mathf.Approximately(line.widthMultiplier, state == ProductionIslandLink.VisualState.Selected ? .12f : .08f) && beads.All(b => b.gameObject.activeSelf == (state == ProductionIslandLink.VisualState.Flowing)), "material=" + line.sharedMaterial.name + "; width=" + line.widthMultiplier); }
            link.SetFlow(Color.white, 0); initial = beads[0].position; tlink = Tick(link, 6); while (tlink.MoveNext()) yield return null;
            Add("LINK_ZERO", beads.All(b => !b.gameObject.activeSelf) && beads[0].position == initial, "Zero flow hides all beads and preserves position."); UnityEngine.Object.DestroyImmediate(linkGo);
            var penguin = Spawn("Assets/3Dmodel/Penguin_Production_v001/Penguin_Production_v001.prefab"); var view = penguin.GetComponent<ProductionIslandPenguinView>(); var animator = Field<Animator>(view, "_animator"); var item = Field<GameObject>(view, "_carriedItem"); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); if (!_play) animator.Update(0); yield return null;
            foreach (PenguinActivity activity in Enum.GetValues(typeof(PenguinActivity)))
            {
                view.SetActivity(activity); if (!_play) animator.Update(.05f); yield return null; Call(view, "Update"); var samples = new List<float> { Pose(penguin) };
                var animationGuard=0; while(animator.GetCurrentAnimatorStateInfo(0).normalizedTime<1.05f && animationGuard++<300) { if (!_play) animator.Update(.05f); Call(view, "Update"); samples.Add(Pose(penguin)); yield return null; }
                Add("ANIMATOR:" + activity, animator.GetCurrentAnimatorStateInfo(0).IsName(PenguinAnimatorStates.NameOf(activity)) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime>=1 && samples.Max()-samples.Min() > .0001f && item.activeSelf == (activity == PenguinActivity.CarryWalk), "samples="+samples.Count+"; frame=" + Time.frameCount + "; unityTime=" + Time.time + "; poseRange=" + (samples.Max()-samples.Min()) + "; carry=" + item.activeSelf + "; normalized=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                if(activity==PenguinActivity.Cheer) { view.SetActivity(activity); if(!_play) animator.Update(.05f); yield return null; Add("CHEER_RESTART",animator.GetCurrentAnimatorStateInfo(0).normalizedTime<.5f,"normalized="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime); }
            }
            view.SetActivity(PenguinActivity.CarryWalk); if(!_play) animator.Update(.05f); yield return null; Call(view,"Update"); Add("CARRY_RETURN",item.activeSelf && animator.GetCurrentAnimatorStateInfo(0).IsName(PenguinAnimatorStates.NameOf(PenguinActivity.CarryWalk)),"CarryWalk after Cheer; carry="+item.activeSelf+"; parent="+item.transform.parent.name);
            UnityEngine.Object.DestroyImmediate(penguin);
            foreach (var name in new[] { "ProductionPlusOne", "IceMelt" })
            {
                var go = Spawn("Assets/VFX/ProductionIsland/Prefabs/" + name + ".prefab"); var burst = go.GetComponent<ProductionIslandBurst>(); Call(burst, "Awake"); Call(burst, "OnEnable"); var visual = Field<Transform>(burst, "_visual"); var start = visual.localPosition; var alpha = visual.GetComponentInChildren<TextMesh>(true);
                var guard=0; while(Field<float>(burst,"_time") < .45f && guard++ < 240) { Call(burst,"Update"); yield return null; } var mid = visual.localPosition; var a = alpha == null ? 1 : alpha.color.a;
                guard=0; while(Field<float>(burst,"_time") < 1.3f && guard++ < 240) { Call(burst,"Update"); yield return null; }
                var elapsed = Field<float>(burst, "_time");
                if (elapsed < 1.2f) Block("BURST:" + name, "Editor Time.deltaTime insufficient; elapsed=" + elapsed);
                else Add("BURST:" + name, (name == "IceMelt" ? mid.y < start.y : mid.y > start.y && a < 1) && !visual.gameObject.activeSelf, "start=" + start + "; mid=" + mid + "; alphaMid=" + a + "; elapsed=" + elapsed + "; hidden=" + !visual.gameObject.activeSelf);
                if (_play) { go.SetActive(false); go.SetActive(true); } else Call(burst, "OnEnable"); Add("BURST_REENABLE:" + name, visual.gameObject.activeSelf && visual.localPosition == start, "Reset position=" + visual.localPosition + "; alphaOnEnable=" + (alpha == null ? -1 : alpha.color.a));
                if (alpha != null) Add("BURST_ALPHA_RESET:" + name, alpha.color.a > .99f, "Expected immediately visible text after re-enable, alpha=" + alpha.color.a);
                guard=0; while(Field<float>(burst,"_time") < .45f && guard++<240) { Call(burst,"Update"); yield return null; }
                if(_play) { go.SetActive(false);go.SetActive(true); } else Call(burst,"OnEnable");
                Add("BURST_INTERRUPT:"+name,visual.gameObject.activeSelf && visual.localPosition==start && (alpha==null || alpha.color.a>.99f),"position="+visual.localPosition+"; time="+Field<float>(burst,"_time")+"; alpha="+(alpha==null?-1:alpha.color.a));
                guard=0; while(Field<float>(burst,"_time") < 1.3f && guard++<240) { Call(burst,"Update"); yield return null; }
                Add("BURST_SECOND_COMPLETE:"+name,!visual.gameObject.activeSelf,"time="+Field<float>(burst,"_time")+"; hidden="+!visual.gameObject.activeSelf);
                UnityEngine.Object.DestroyImmediate(go);
            }
            foreach(var portName in new[] { "Input","Output" })
            {
            var port = Spawn("Assets/3Dmodel/Link_Port_"+portName+"_Selectable_v001/Link_Port_"+portName+"_Selectable_v001.prefab"); var pulse = port.GetComponent<ProductionIslandPortPulse>(); Call(pulse, "Awake"); var values = new List<float>(); var renderer = port.GetComponentInChildren<Renderer>();
            yield return null;
            for (var i = 0; i < 30; i++) { Call(pulse, "Update"); renderer.GetPropertyBlock(block); values.Add(block.GetColor(ShaderIds.BaseColor).r); yield return null; }
            if (values.Max()-values.Min() < .001f) Block("PORT_PULSE:"+portName, "Editor Time.time did not advance; brightnessRange=" + (values.Max()-values.Min()));
            else Add("PORT_PULSE:"+portName, values.Min() >= .29f && values.Max() <= 1.01f, "brightnessMin=" + values.Min() + "; max=" + values.Max());
            UnityEngine.Object.DestroyImmediate(port);
            }
            if (!_play) Block("PLAYMODE_ACCEPTANCE", "Current user scene and Play Mode left untouched. This preview harness does not verify automatic lifecycle scheduling, rendering or device performance.");
            Block("VISUAL_ACCEPTANCE", "Numeric transform/state/color samples only; screenshots and device performance not verified.");
        }
    }
}
