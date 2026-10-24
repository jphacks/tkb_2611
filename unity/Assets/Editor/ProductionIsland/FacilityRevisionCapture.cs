using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Observe real PlayerLoop frames. Never invoke Update manually or advance the recipe clock.</summary>
    public static class FacilityRevisionCapture
    {
        private const string Folder = "ArtSource/ProductionIsland_v002/Facilities/Review";
        [Serializable] private sealed class Sample
        {
            public string stage, gesture, action;
            public float time;
            public int frame;
            public bool input, raw, finished, output;
            public Vector3 tool, worker, secondary, rawScale, outputPosition;
            public Quaternion toolRotation;
        }
        [Serializable] private sealed class Result
        {
            public string name, label;
            public int renderers, triangles, firstFrame, lastFrame;
            public float duration;
            public bool staticStage, requiredParts, stageSequence, itemSequence, stopFreezes, restartWorks, existingPenguin, attachedItems, characteristicMotion;
            public List<Sample> samples = new List<Sample>();
        }
        [Serializable] private sealed class Report
        {
            public string date = "2026-10-08", unity;
            public bool completed, playMode = true;
            public List<Result> facilities = new List<Result>();
        }
        private static float[] _times;
        private static FacilityDesignGallery _gallery;
        private static FacilityProductionCycle _cycle;
        private static Transform _input, _raw, _finished, _output, _tool, _secondary;
        private static Transform[] _poseParts;
        private static Vector3[] _positions, _scales;
        private static Quaternion[] _rotations;
        private static Report _report;
        private static Result _result;
        private static int _index, _sample, _step, _frame;
        private static float _phaseStarted, _frozenTime;

        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play the facility review scene first.");
            if (_report != null) throw new InvalidOperationException("Already capturing.");
            _gallery = UnityEngine.Object.FindAnyObjectByType<FacilityDesignGallery>();
            if (_gallery == null) throw new InvalidOperationException("Facility gallery required.");
            Directory.CreateDirectory(Folder);
            _report = new Report { unity = Application.unityVersion };
            _index = 0; _frame = -1; BeginFacility();
            EditorApplication.update += Observe;
        }

        public static string Status() => _report == null ? "idle" : (_index + 1) + "/" + _gallery.FacilityCount + " " + _result.label;
        private static void BeginFacility()
        {
            _gallery.Select(_index);
            var root = _gallery.GetFacility(_index);
            _cycle = root.GetComponent<FacilityProductionCycle>();
            _result = new Result { name = root.name, label = _gallery.SelectedLabel, firstFrame = Time.frameCount,
                renderers = root.GetComponentsInChildren<Renderer>(true).Length,
                triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(m => m.sharedMesh.triangles.Length / 3) };
            _report.facilities.Add(_result); _sample = 0; _step = 0; _phaseStarted = Time.time;
            if (_cycle == null) { _result.staticStage = true; return; }
            Transform Part(string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            _input = Part("InputItem"); _raw = Part("ProcessRaw"); _finished = Part("ProcessFinished"); _output = Part("OutputItem"); _tool = Part("WorkingPart");
            _secondary = Part(ModelParts.Cycle.SecondaryTool);
            _result.duration = _cycle.Duration;
            _times = new[] { _cycle.InputEndTime * .5f, (_cycle.InputEndTime + _cycle.TransformationTime) * .5f,
                (_cycle.TransformationTime + _cycle.WorkEndTime) * .5f, (_cycle.WorkEndTime + _cycle.OutputEndTime) * .5f,
                (_cycle.OutputEndTime + _cycle.Duration) * .5f };
            _result.requiredParts = _input != null && _raw != null && _finished != null && _output != null && _tool != null;
            var penguinMeshes = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.name.StartsWith("PG_", StringComparison.Ordinal)).ToArray();
            _result.existingPenguin = _cycle.Worker == null ? penguinMeshes.Length == 0 : penguinMeshes.Length == 10
                && penguinMeshes.All(m => AssetDatabase.GetAssetPath(m.sharedMesh) == FacilityRevisionBuilder.PenguinPath);
            _result.attachedItems = _cycle.Kind != FacilityProductionCycle.FacilityKind.Drying && _cycle.Kind != FacilityProductionCycle.FacilityKind.Chikuwa
                || _raw.IsChildOf(_tool) && _finished.IsChildOf(_tool);
            _cycle.SetRunning(true); _cycle.RestartCycle();
        }

        private static void Observe()
        {
            if (_report == null) return;
            if (!EditorApplication.isPlaying || _gallery == null) { Finish(false); return; }
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            if (_result.staticStage)
            {
                if (Time.time - _phaseStarted < .15f) return;
                ScreenCapture.CaptureScreenshot(Folder + "/" + _result.name + ".png"); Next(); return;
            }
            if (Time.time - _phaseStarted > _cycle.Duration + 3) { Next(); return; }
            if (_sample < _times.Length)
            {
                if (_cycle.CycleTime < _times[_sample]) return;
                _result.samples.Add(new Sample { stage = _cycle.Stage.ToString(), time = _cycle.CycleTime, frame = Time.frameCount,
                    input = _input.gameObject.activeSelf, raw = _raw.gameObject.activeSelf, finished = _finished.gameObject.activeSelf, output = _output.gameObject.activeSelf,
                    tool = _tool.position, worker = _cycle.Worker == null ? Vector3.zero : _cycle.Worker.transform.position,
                    toolRotation = _tool.rotation, secondary = _secondary == null ? Vector3.zero : _secondary.position,
                    rawScale = _raw.localScale, outputPosition = _output.position, action = _cycle.ActionLabel,
                    gesture = _cycle.Worker == null ? "none" : _cycle.Worker.CurrentGesture.ToString() });
                if (_sample == 1 || _sample == 3) ScreenCapture.CaptureScreenshot(Folder + "/" + _result.name + (_sample == 1 ? "-Working.png" : "-Output.png"));
                _sample++; return;
            }
            if (_step == 0)
            {
                _cycle.SetRunning(false); _phaseStarted = Time.time; _frozenTime = _cycle.CycleTime;
                _poseParts = _cycle.GetComponentsInChildren<Transform>(true);
                _positions = _poseParts.Select(t => t.localPosition).ToArray();
                _rotations = _poseParts.Select(t => t.localRotation).ToArray();
                _scales = _poseParts.Select(t => t.localScale).ToArray();
                _step = 1; return;
            }
            if (_step == 1)
            {
                if (Time.time - _phaseStarted < .3f) return;
                _result.stopFreezes = Mathf.Abs(_cycle.CycleTime - _frozenTime) < .00001f
                    && _poseParts.Select((t, i) => Vector3.Distance(t.localPosition, _positions[i]) < .00001f
                        && Quaternion.Angle(t.localRotation, _rotations[i]) < .01f && Vector3.Distance(t.localScale, _scales[i]) < .00001f).All(v => v);
                _cycle.SetRunning(true); _cycle.RestartCycle(); _phaseStarted = Time.time; _step = 2; return;
            }
            if (Time.time - _phaseStarted < .15f) return;
            _result.restartWorks = _cycle.CycleTime > 0 && _cycle.CycleTime < .5f && _cycle.Stage == FacilityProductionCycle.ProcessStage.Loading;
            _result.stageSequence = _result.samples.Select(s => s.stage).SequenceEqual(new[] { "Loading", "Working", "Working", "Output", "Returning" });
            _result.itemSequence = _cycle.Kind == FacilityProductionCycle.FacilityKind.Dormitory
                ? _result.samples.All(s => !s.input && !s.raw && !s.finished && !s.output)
                : _result.samples[0].input && !_result.samples[0].output
                  && _result.samples[1].raw && !_result.samples[1].finished && !_result.samples[1].input
                  && _result.samples[2].finished && !_result.samples[2].raw
                  && _result.samples[3].output && !_result.samples[3].finished && _result.samples[4].output;
            _result.characteristicMotion = _result.samples.Skip(1).Any(s => Vector3.Distance(s.tool, _result.samples[0].tool) > .001f
                || Quaternion.Angle(s.toolRotation, _result.samples[0].toolRotation) > .5f
                || Vector3.Distance(s.secondary, _result.samples[0].secondary) > .001f
                || Vector3.Distance(s.rawScale, _result.samples[0].rawScale) > .001f);
            Next();
        }

        private static void Next()
        {
            _result.lastFrame = Time.frameCount;
            File.WriteAllText(Folder + "/motion-verification.json", JsonUtility.ToJson(_report, true));
            _index++;
            if (_index >= _gallery.FacilityCount) { Finish(true); return; }
            BeginFacility();
        }

        private static void Finish(bool completed)
        {
            EditorApplication.update -= Observe;
            _report.completed = completed;
            File.WriteAllText(Folder + "/motion-verification.json", JsonUtility.ToJson(_report, true));
            _report = null;
            if (_gallery != null) _gallery.Select(2); // Leave the new Chikuwa factory visible.
        }
    }
}
