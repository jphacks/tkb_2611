using System;
using System.Linq;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Facility-specific, display-only recipes based on characteristic work.</summary>
    public sealed class FacilityProductionCycle : MonoBehaviour
    {
        public enum FacilityKind { Fishery, Preparation, Chikuwa, Saltworks, Drying, KelpFarm, Oden,
            Warehouse, Sorter, Research, Dormitory, ElectricAmplifier, WaterAmplifier, GasAmplifier, Harbor, IceHouse }
        public enum ProcessStage { Loading, Working, Output, Returning }
        [SerializeField] private FacilityKind _kind;
        [SerializeField] private Transform _frame, _input, _raw, _finished, _output, _center, _stand, _tool, _effect;
        [SerializeField] private WorkshopPenguin _worker;
        [SerializeField] private Renderer[] _facilityRenderers;
        [SerializeField] private Material _runningMaterial, _stoppedMaterial;
        [SerializeField] private bool _running = true;
        [SerializeField] private float _heat = 1f;
        private Transform _secondary, _fluid;
        private Transform[] _charges, _routes;
        private Vector3 _inputStart, _outputEnd, _processCenter, _workerStand, _toolStart, _effectStart, _secondaryStart, _fluidStart;
        private Vector3 _rawStart, _finishedStart, _rawScale, _finishedScale, _effectScale, _fluidScale;
        private Quaternion _toolRotation, _secondaryRotation, _inputRotation;
        private float _time;
        private int _loop;
        private bool _initialized;
        private Timing _timing;

        public readonly struct Timing
        {
            public readonly float Load, Transform, Work, Output, End;
            public Timing(float load, float transform, float work, float output, float end)
            { Load = load; Transform = transform; Work = work; Output = output; End = end; }
        }
        public static Timing Recipe(FacilityKind kind) => kind switch
        {
            FacilityKind.Fishery => new Timing(1, 4, 4.8f, 6, 7),
            FacilityKind.Preparation => new Timing(.8f, 2.4f, 3.5f, 4.5f, 5.5f),
            FacilityKind.Chikuwa => new Timing(1.1f, 4.5f, 5.5f, 6.3f, 7.4f),
            FacilityKind.Saltworks => new Timing(1.2f, 4.8f, 6, 7.3f, 8.8f),
            FacilityKind.Drying => new Timing(1.7f, 6.4f, 7.2f, 8, 10),
            FacilityKind.KelpFarm => new Timing(.8f, 2.5f, 3.4f, 4.7f, 6),
            FacilityKind.Oden => new Timing(1, 5.4f, 6.4f, 7.3f, 9),
            FacilityKind.Warehouse => new Timing(.7f, 1.6f, 2.2f, 3.2f, 4.5f),
            FacilityKind.Sorter => new Timing(.55f, 1.2f, 1.5f, 2.2f, 3),
            FacilityKind.Research => new Timing(.9f, 4.2f, 5, 5.8f, 7.5f),
            FacilityKind.Dormitory => new Timing(1, 8.5f, 10, 11.4f, 12.8f),
            FacilityKind.ElectricAmplifier => new Timing(.6f, 2.1f, 2.5f, 3.2f, 4.4f),
            FacilityKind.WaterAmplifier => new Timing(1, 3.8f, 4.7f, 5.5f, 6.8f),
            FacilityKind.GasAmplifier => new Timing(.8f, 3, 3.8f, 4.7f, 6),
            FacilityKind.IceHouse => new Timing(1, 4, 5, 6.2f, 7.6f),
            _ => new Timing(1, 2.5f, 3.2f, 4.6f, 6.5f)
        };
        public FacilityKind Kind => _kind;
        public ProcessStage Stage { get; private set; }
        public float CycleTime => _time;
        public float Duration => Recipe(_kind).End;
        public float InputEndTime => Recipe(_kind).Load;
        public float TransformationTime => Recipe(_kind).Transform;
        public float WorkEndTime => Recipe(_kind).Work;
        public float OutputEndTime => Recipe(_kind).Output;
        public bool IsRunning => _running;
        public WorkshopPenguin Worker => _worker;
        public int RouteIndex => _loop % 3;
        public static ProcessStage StageAt(FacilityKind kind, float elapsedTime)
        {
            var timing = Recipe(kind);
            var t = Mathf.Repeat(Mathf.Max(0, elapsedTime), timing.End);
            return t < timing.Load ? ProcessStage.Loading : t < timing.Work ? ProcessStage.Working
                : t < timing.Output ? ProcessStage.Output : ProcessStage.Returning;
        }
        public string ActionLabel
        {
            get
            {
                if (Stage == ProcessStage.Loading) return _kind == FacilityKind.Fishery ? "水中の魚を網へ誘う" : _kind == FacilityKind.Dormitory ? "休みに戻る" : "受け入れ";
                if (Stage == ProcessStage.Output) return _kind switch
                {
                    FacilityKind.Harbor => "納品して舟を見送る",
                    FacilityKind.Sorter => "三方向へ振り分ける",
                    FacilityKind.Dormitory => "目を開けて体を伸ばす",
                    FacilityKind.Warehouse => "棚から搬出する",
                    FacilityKind.ElectricAmplifier or FacilityKind.WaterAmplifier or FacilityKind.GasAmplifier => "増幅結果を表示する",
                    FacilityKind.IceHouse => "冷たい風を島へ送る",
                    _ => "完成品を取り出す"
                };
                if (Stage == ProcessStage.Returning) return _kind == FacilityKind.Dormitory ? "起きて伸びをする" : "次の仕事へ";
                var raw = _time < _timing.Transform;
                return _kind switch
                {
                    FacilityKind.Fishery => raw ? "力を込めて網を引き上げる" : "水を切って箱へ移す",
                    FacilityKind.Preparation => raw ? "リズムよく刻む" : "すり身をすり混ぜる",
                    FacilityKind.Chikuwa => raw ? "棒を回してじっくり焼く" : "焼き色を確認する",
                    FacilityKind.Saltworks => raw ? "水分が減るのを待つ" : "結晶をかき集める",
                    FacilityKind.Drying => raw ? "掛けて、風に任せて乾かす" : "乾き具合を確かめる",
                    FacilityKind.KelpFarm => raw ? "かぎ竿をねじって巻き上げる" : "海藻を束ねる",
                    FacilityKind.Oden => raw ? "弱火で静かに煮込む" : "おたまで具をすくう",
                    FacilityKind.Warehouse => raw ? "箱を持ち上げる" : "上の棚へ格納する",
                    FacilityKind.Sorter => "分岐板で送り先を切り替える",
                    FacilityKind.Research => raw ? "レンズで観察して考える" : "記録へ判定印を押す",
                    FacilityKind.Dormitory => raw ? "目を閉じて休む" : "ゆっくり目を覚ます",
                    FacilityKind.ElectricAmplifier => "スイッチを入れ、残量灯を順に点ける",
                    FacilityKind.WaterAmplifier => "水位と浮きを確かめる",
                    FacilityKind.GasAmplifier => "弁を回し、圧力針の落ち着きを待つ",
                    FacilityKind.IceHouse => raw ? "氷のかたまりを雪のドームへ運ぶ" : "冷たさをたくわえる",
                    _ => "荷を吊り上げて桟橋へ渡す"
                };
            }
        }

        public void Configure(FacilityKind kind, Transform frame, Transform input, Transform raw, Transform finished,
            Transform output, Transform center, Transform stand, Transform tool, Transform effect, WorkshopPenguin worker)
        {
            _kind = kind; _frame = frame; _input = input; _raw = raw; _finished = finished;
            _output = output; _center = center; _stand = stand; _tool = tool; _effect = effect; _worker = worker;
        }
        public void ConfigureMaterials(Renderer[] renderers, Material running, Material stopped)
        { _facilityRenderers = renderers; _runningMaterial = running; _stoppedMaterial = stopped; }

        private void Awake()
        {
            var required = new[] { (_frame, nameof(_frame)), (_input, nameof(_input)), (_raw, nameof(_raw)),
                (_finished, nameof(_finished)), (_output, nameof(_output)), (_center, nameof(_center)), (_stand, nameof(_stand)) };
            var missing = required.Where(part => part.Item1 == null).Select(part => part.Item2).ToArray();
            if (missing.Length != 0)
            {
                Debug.LogError($"{nameof(FacilityProductionCycle)}: 必須参照が未設定です: {string.Join(", ", missing)}", this);
                enabled = false;
                return;
            }
            _timing = Recipe(_kind);
            _inputStart = Point(_input); _outputEnd = Point(_output); _processCenter = Point(_center); _workerStand = Point(_stand);
            _inputRotation = Quaternion.Inverse(_frame.rotation) * _input.rotation;
            _rawStart = Point(_raw); _finishedStart = Point(_finished);
            _rawScale = _raw.localScale; _finishedScale = _finished.localScale;
            if (_tool != null) { _toolStart = Point(_tool); _toolRotation = Quaternion.Inverse(_frame.rotation) * _tool.rotation; }
            if (_effect != null) { _effectStart = Point(_effect); _effectScale = _effect.localScale; }
            var parts = GetComponentsInChildren<Transform>(true);
            Transform Part(string name) => parts.FirstOrDefault(p => p.name == name);
            _secondary = Part(ModelParts.Cycle.SecondaryTool); _fluid = Part(ModelParts.Cycle.FluidLevel);
            if (_secondary != null) { _secondaryStart = Point(_secondary); _secondaryRotation = Quaternion.Inverse(_frame.rotation) * _secondary.rotation; }
            if (_fluid != null) { _fluidStart = Point(_fluid); _fluidScale = _fluid.localScale; }
            _charges = Array.ConvertAll(ModelParts.Cycle.Charges, Part);
            _routes = Array.ConvertAll(ModelParts.Cycle.Routes, Part);
            _initialized = true; ApplyPose(); ApplyMaterials();
        }
        public void SetRunning(bool running) { _running = running; if (_initialized) { ApplyPose(); ApplyMaterials(); } }
        public void SetHighHeat(bool highHeat) => _heat = highHeat ? 1.5f : .7f;
        public void RestartCycle() { _time = 0; _loop = 0; if (_initialized) ApplyPose(); }
        private void ApplyMaterials()
        {
            var material = _running ? _runningMaterial : _stoppedMaterial;
            if (_facilityRenderers == null || material == null) return;
            foreach (var renderer in _facilityRenderers) FillMaterialSlots(renderer, material);
        }

        /// <summary>
        /// すべてのマテリアルスロットを同じマテリアルにする。sharedMaterial への代入はスロット 0 だけを
        /// 置き換えるため、マルチマテリアルのメッシュでは 2 つ目以降が元のまま (または空) になる。
        /// </summary>
        public static void FillMaterialSlots(Renderer renderer, Material material)
        {
            var slots = renderer.sharedMaterials;
            if (slots.Length == 0) slots = new Material[1];
            for (var i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
        private void Update()
        {
            if (!_running || !_initialized) return;
            _time += Time.deltaTime;
            if (_time >= _timing.End) { _loop++; _time = Mathf.Repeat(_time, _timing.End); }
            ApplyPose();
        }
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        private static float Stroke(float t) => .5f - .5f * Mathf.Cos(t * Mathf.PI * 2);
        private Vector3 Point(Transform part) => _frame.InverseTransformPoint(part.position);
        private void Position(Transform part, Vector3 point) => part.position = _frame.TransformPoint(point);
        private void ScaleInFrame(Transform part, Vector3 original, Vector3 factors)
        {
            float Factor(Vector3 axis)
            {
                var v = _frame.InverseTransformDirection(part.TransformDirection(axis));
                return Mathf.Abs(v.x) * factors.x + Mathf.Abs(v.y) * factors.y + Mathf.Abs(v.z) * factors.z;
            }
            part.localScale = Vector3.Scale(original, new Vector3(Factor(Vector3.right), Factor(Vector3.up), Factor(Vector3.forward)));
        }

        private void ApplyPose()
        {
            var t = _time;
            Stage = StageAt(_kind, t);
            var loading = Stage == ProcessStage.Loading; var working = Stage == ProcessStage.Working;
            var raw = working && t < _timing.Transform; var outputting = Stage == ProcessStage.Output;
            var resting = _kind == FacilityKind.Dormitory;
            var a = Mathf.Clamp01((t - _timing.Load) / (_timing.Transform - _timing.Load));
            var b = Mathf.Clamp01((t - _timing.Transform) / (_timing.Work - _timing.Transform));
            var work = Mathf.Clamp01((t - _timing.Load) / (_timing.Work - _timing.Load));
            _input.gameObject.SetActive(loading && !resting); _raw.gameObject.SetActive(raw && !resting);
            _finished.gameObject.SetActive(working && !raw && !resting); _output.gameObject.SetActive(t >= _timing.Work && !resting);
            _raw.localScale = _rawScale; _finished.localScale = _finishedScale;
            _input.rotation = _frame.rotation * _inputRotation;
            var center = _processCenter;
            var destination = _kind == FacilityKind.Sorter && _routes[RouteIndex] != null ? Point(_routes[RouteIndex]) : _outputEnd;
            if (_kind == FacilityKind.Warehouse) center.y += .10f;
            Position(_input, Vector3.Lerp(_inputStart, _processCenter, Ease(t / _timing.Load)));
            Position(_output, Vector3.Lerp(center, destination, Ease((t - _timing.Work) / (_timing.Output - _timing.Work))));
            var point = _toolStart; var rotation = _toolRotation;
            var secondaryPoint = _secondaryStart; var secondaryRotation = _secondaryRotation;
            var gesture = WorkshopPenguin.Gesture.Wait; var action = 0f;

            switch (_kind)
            {
                case FacilityKind.Fishery:
                    if (loading)
                    {
                        var swim = Point(_input); swim.y = .30f + Mathf.Sin(t * 7) * .014f; Position(_input, swim);
                        _input.rotation = _frame.rotation * Quaternion.AngleAxis(Mathf.Sin(t * 8) * 13, Vector3.up) * _inputRotation;
                    }
                    var pull = a < .46f ? .45f * Ease(a / .46f) : .45f + .55f * Ease((a - .55f) / .45f);
                    point.y += loading ? -.12f : working ? -.12f + .42f * pull : .30f;
                    gesture = raw ? WorkshopPenguin.Gesture.Pull : WorkshopPenguin.Gesture.Inspect; action = Stroke(a * 2); break;
                case FacilityKind.Preparation:
                    action = Stroke(a * 3);
                    point.y += raw ? -.08f + .15f * action : .16f;
                    point.x += raw ? .08f * a : .1f;
                    secondaryPoint.x += Mathf.Sin(b * Mathf.PI * 2) * .065f;
                    secondaryPoint.z += Mathf.Cos(b * Mathf.PI * 2) * .045f;
                    if (_secondary != null) _secondary.gameObject.SetActive(working && !raw);
                    gesture = raw ? WorkshopPenguin.Gesture.Chop : WorkshopPenguin.Gesture.Stir; break;
                case FacilityKind.Chikuwa:
                    rotation = Quaternion.AngleAxis(working ? (t - _timing.Load) * 160 : 0, Vector3.right) * rotation;
                    gesture = raw ? WorkshopPenguin.Gesture.Turn : WorkshopPenguin.Gesture.Inspect; break;
                case FacilityKind.Saltworks:
                    if (raw) ScaleInFrame(_raw, _rawScale, new Vector3(Mathf.Lerp(1, .35f, a), Mathf.Lerp(1, .08f, a), Mathf.Lerp(1, .35f, a)));
                    action = Stroke(b * 2);
                    point.z += working && !raw ? -.09f + .18f * action : .1f;
                    point.y += raw || !working ? .11f : .03f * (1 - action);
                    if (working && !raw) ScaleInFrame(_finished, _finishedScale, new Vector3(1 - b * .22f, .45f + b * .55f, 1 - b * .22f));
                    gesture = raw ? WorkshopPenguin.Gesture.Wait : WorkshopPenguin.Gesture.Rake; break;
                case FacilityKind.Drying:
                    rotation = Quaternion.AngleAxis(working ? Mathf.Sin(t * 1.4f) * 1.5f : 0, Vector3.forward) * rotation;
                    gesture = raw && a < .14f ? WorkshopPenguin.Gesture.Hang : raw ? WorkshopPenguin.Gesture.Wait : WorkshopPenguin.Gesture.Inspect; break;
                case FacilityKind.KelpFarm:
                    point.y += working ? .18f * Ease(a) : .02f;
                    rotation = Quaternion.AngleAxis(raw ? a * 360 : 360, Vector3.up) * rotation;
                    if (raw) { Position(_raw, _rawStart + Vector3.up * (.13f * Ease(a))); ScaleInFrame(_raw, _rawScale, new Vector3(1 - a * .15f, 1, 1)); }
                    gesture = raw ? WorkshopPenguin.Gesture.Harvest : WorkshopPenguin.Gesture.Hang; action = a; break;
                case FacilityKind.Oden:
                    var stir = working && raw && (a < .24f || a > .72f);
                    point.x += stir ? Mathf.Sin(t * 2) * .075f : 0;
                    point.z += stir ? Mathf.Cos(t * 2) * .06f : 0;
                    point.y += working && !raw ? -.05f + .16f * Ease(b) : 0;
                    gesture = stir ? WorkshopPenguin.Gesture.Stir : raw ? WorkshopPenguin.Gesture.Wait : WorkshopPenguin.Gesture.Pull; action = b; break;
                case FacilityKind.Warehouse:
                    rotation = Quaternion.AngleAxis(loading || outputting || t >= _timing.Output ? -80 : 0, Vector3.up) * rotation;
                    secondaryPoint.y += working ? .10f * Ease(a) : outputting ? .10f * (1 - Ease((t - _timing.Work) / (_timing.Output - _timing.Work))) : 0;
                    if (raw) Position(_raw, _rawStart + Vector3.up * (.10f * Ease(a)));
                    if (working && !raw) Position(_finished, _finishedStart + Vector3.up * .10f); break;
                case FacilityKind.Sorter:
                    rotation = Quaternion.AngleAxis(working || outputting ? Mathf.Lerp(0, (RouteIndex - 1) * 55, Ease(work)) : 0, Vector3.up) * rotation;
                    if (working && !raw) Position(_finished, Vector3.Lerp(_finishedStart, destination, b * .35f)); break;
                case FacilityKind.Research:
                    point.x += raw && (a < .30f || a > .7f) ? Mathf.Sin(a * Mathf.PI * 3) * .15f : -.04f;
                    point.y += raw ? -.06f : .06f;
                    action = raw ? Ease((a - .85f) / .15f) : 1 - Ease(b);
                    secondaryPoint.y += working ? -.10f * action : .04f;
                    gesture = raw && a < .85f ? WorkshopPenguin.Gesture.Inspect : WorkshopPenguin.Gesture.Stamp; break;
                case FacilityKind.Dormitory:
                    point.y += raw ? Mathf.Sin(t * 1.7f) * .006f : 0;
                    gesture = raw ? WorkshopPenguin.Gesture.Rest : WorkshopPenguin.Gesture.Stretch; break;
                case FacilityKind.ElectricAmplifier:
                    rotation = Quaternion.AngleAxis(-35 + 70 * Ease(work), Vector3.forward) * rotation;
                    secondaryPoint.y -= working && work < .18f ? .055f * Stroke(work / .18f) : 0;
                    for (var i = 0; i < _charges.Length; i++) if (_charges[i] != null) _charges[i].gameObject.SetActive(t >= _timing.Load && work >= (i + 1) * .24f);
                    gesture = working && work < .18f ? WorkshopPenguin.Gesture.Adjust : WorkshopPenguin.Gesture.Wait; break;
                case FacilityKind.WaterAmplifier:
                    rotation = Quaternion.AngleAxis(-35 + 70 * Ease(work), Vector3.forward) * rotation;
                    secondaryPoint.y += .10f * Ease(work);
                    if (_fluid != null)
                    {
                        var amount = .12f + .88f * Ease(work);
                        ScaleInFrame(_fluid, _fluidScale, new Vector3(1, amount, 1));
                        Position(_fluid, _fluidStart - Vector3.up * (.135f * (1 - amount)));
                    }
                    gesture = WorkshopPenguin.Gesture.Inspect; break;
                case FacilityKind.GasAmplifier:
                    secondaryRotation = Quaternion.AngleAxis(working ? 135 * Ease(a) : t >= _timing.Work ? 135 : 0, Vector3.up) * secondaryRotation;
                    var pressure = -35 + 70 * Ease(work) + (working ? Mathf.Sin(work * Mathf.PI * 5) * 14 * (1 - work) : 0);
                    rotation = Quaternion.AngleAxis(pressure, Vector3.forward) * rotation;
                    gesture = raw && a < .7f ? WorkshopPenguin.Gesture.Turn : WorkshopPenguin.Gesture.Inspect; break;
                case FacilityKind.Harbor:
                    point.x += t >= _timing.Work ? .70f * Ease((t - _timing.Work) / (_timing.End - _timing.Work)) : 0;
                    point.y += Mathf.Sin(t * 2) * .012f;
                    secondaryPoint.y += working ? .16f * Ease(a) : .16f;
                    if (raw) Position(_raw, _rawStart + Vector3.up * (.16f * Ease(a)));
                    if (working && !raw) Position(_finished, _finishedStart + Vector3.up * .16f);
                    gesture = raw ? WorkshopPenguin.Gesture.Pull : WorkshopPenguin.Gesture.Wave; action = a; break;
            }
            if (_tool != null) { Position(_tool, point); _tool.rotation = _frame.rotation * rotation; }
            if (_secondary != null) { Position(_secondary, secondaryPoint); _secondary.rotation = _frame.rotation * secondaryRotation; }
            if (_effect != null)
            {
                _effect.gameObject.SetActive(working || _kind == FacilityKind.Harbor);
                Position(_effect, _effectStart + Vector3.up * (_kind == FacilityKind.Oden && working ? Mathf.Repeat(t * .25f, 1) * .20f : 0));
                _effect.localScale = _effectScale * (_kind == FacilityKind.Chikuwa ? _heat * (1 + Mathf.Sin(t * 11) * .07f) : 1);
            }
            if (_worker == null) return;
            var workerPoint = _workerStand;
            if (resting)
            {
                workerPoint.x = working ? 0 : Mathf.Lerp(-.25f, .25f, Ease(t / _timing.End));
                if (loading) gesture = WorkshopPenguin.Gesture.Carry;
            }
            else if (loading)
            {
                workerPoint.x = _kind == FacilityKind.Fishery ? -.28f : Point(_input).x;
                gesture = _kind == FacilityKind.Fishery ? WorkshopPenguin.Gesture.Wait : WorkshopPenguin.Gesture.Carry;
            }
            else if (working)
            {
                workerPoint.x = _kind == FacilityKind.Drying && raw && a > .14f ? .42f : -.16f;
            }
            else if (outputting)
            {
                workerPoint.x = Point(_output).x;
                gesture = _kind == FacilityKind.Harbor ? WorkshopPenguin.Gesture.Wave : WorkshopPenguin.Gesture.Carry;
            }
            else
            {
                workerPoint.x = Mathf.Lerp(destination.x, _inputStart.x, Ease((t - _timing.Output) / (_timing.End - _timing.Output)));
                gesture = _kind == FacilityKind.Harbor ? WorkshopPenguin.Gesture.Wave : WorkshopPenguin.Gesture.Carry;
            }
            Position(_worker.transform, workerPoint);
            _worker.transform.rotation = _frame.rotation * Quaternion.Euler(0, 180, 0);
            _worker.ApplyPose(gesture, t, action);
        }
    }
}
