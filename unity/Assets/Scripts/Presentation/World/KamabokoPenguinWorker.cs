using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Chef penguin moves and gestures in step with the visible production recipe.</summary>
    public sealed class KamabokoPenguinWorker : MonoBehaviour
    {
        [SerializeField] private KamabokoProductionCycle _cycle;
        [SerializeField] private Transform _visual, _leftWing, _rightWing, _leftFoot, _rightFoot;
        [SerializeField] private Transform _fish, _product, _lid, _chamber, _carrySocket, _lidHandle;
        private PenguinActivity _activity;
        private Vector3 _groundOrigin;
        private Vector3 _visualStart, _leftFootStart, _rightFootStart;
        private Quaternion _leftFootRotation, _rightFootRotation;
        public PenguinActivity Activity => _activity;
        public Transform CarrySocket => _carrySocket;
        public float PoseTime => _cycle.CycleTime;

        public void Configure(KamabokoProductionCycle cycle, Transform visual, Transform leftWing, Transform rightWing,
            Transform leftFoot, Transform rightFoot, Transform fish, Transform product,
            Transform lid, Transform chamber, Transform carrySocket, Transform lidHandle)
        {
            _cycle = cycle; _visual = visual; _leftWing = leftWing; _rightWing = rightWing;
            _leftFoot = leftFoot; _rightFoot = rightFoot; _fish = fish; _product = product;
            _lid = lid; _chamber = chamber; _carrySocket = carrySocket; _lidHandle = lidHandle;
        }

        private void Awake()
        {
            _groundOrigin = _chamber.position - Vector3.up * .45f + Vector3.forward * .4f;
            _visualStart = _visual.localPosition;
            _leftFootStart = _leftFoot.localPosition; _rightFootStart = _rightFoot.localPosition;
            _leftFootRotation = _leftFoot.localRotation; _rightFootRotation = _rightFoot.localRotation;
        }

        private void LateUpdate()
        {
            if (_cycle == null || _visual == null) return;
            var t = _cycle.CycleTime;
            var stage = _cycle.Stage;
            var center = _chamber.position;
            var x = center.x;
            var direction = Vector3.back;
            var activity = PenguinActivity.WorkLoop;
            if (stage == KamabokoProductionCycle.ProcessStage.Loading)
            {
                x = _fish.position.x;
                activity = t < .8f ? PenguinActivity.CarryWalk : PenguinActivity.WorkLoop;
            }
            else if (stage == KamabokoProductionCycle.ProcessStage.Closing)
            {
                x -= .28f * Mathf.SmoothStep(0, 1, (t - 1.3f) / .7f);
                direction = (center - new Vector3(x, center.y, _groundOrigin.z)); direction.y = 0;
            }
            else if (stage == KamabokoProductionCycle.ProcessStage.Steaming)
            {
                x -= .28f; activity = PenguinActivity.Idle;
                direction = new Vector3(.45f, 0, -.9f);
            }
            else if (stage == KamabokoProductionCycle.ProcessStage.Opening)
                x -= .28f * (1f - Mathf.SmoothStep(0, 1, (t - 4.3f) / .6f));
            else if (stage == KamabokoProductionCycle.ProcessStage.Output)
            {
                x = _product.position.x; activity = PenguinActivity.CarryWalk;
            }
            else if (t < 6.8f)
            {
                x = _product.position.x; activity = PenguinActivity.Cheer;
            }
            else
            {
                x = Mathf.Lerp(center.x + .65f, center.x - .65f, Mathf.SmoothStep(0, 1, (t - 6.8f) / .7f));
                activity = PenguinActivity.CarryWalk;
            }
            transform.position = new Vector3(x, _groundOrigin.y, _groundOrigin.z);
            // The existing v002 character faces local +Z. Animate its separate parts intact.
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _activity = activity;
            var walk = activity == PenguinActivity.CarryWalk;
            var wave = Mathf.Sin(t * (t >= 6.8f ? 24f : 14f));
            _visual.localPosition = _visualStart + Vector3.up * (walk ? Mathf.Abs(wave) * .012f : 0);
            _visual.localRotation = Quaternion.Euler(0, 0, walk ? wave * 3f : 0);
            _leftFoot.localPosition = _leftFootStart + Vector3.up * (walk ? Mathf.Max(0, wave) * .025f : 0);
            _rightFoot.localPosition = _rightFootStart + Vector3.up * (walk ? Mathf.Max(0, -wave) * .025f : 0);
            _leftFoot.localRotation = Quaternion.Euler(walk ? wave * 12f : 0, 0, 0) * _leftFootRotation;
            _rightFoot.localRotation = Quaternion.Euler(walk ? -wave * 12f : 0, 0, 0) * _rightFootRotation;
            var carrying = stage == KamabokoProductionCycle.ProcessStage.Loading || stage == KamabokoProductionCycle.ProcessStage.Output;
            var working = activity == PenguinActivity.WorkLoop;
            var lift = carrying ? -70f : working ? -50f + Mathf.Sin(t * 9f) * 5f : 0;
            var inward = carrying ? 25f : working ? 18f : activity == PenguinActivity.Cheer ? -65f : 0;
            _leftWing.localRotation = Quaternion.Euler(lift, 0, -inward);
            _rightWing.localRotation = Quaternion.Euler(lift, 0, inward);
            // A little wooden handle connects the working flippers to the hot lid.
            var usingHandle = stage == KamabokoProductionCycle.ProcessStage.Closing || stage == KamabokoProductionCycle.ProcessStage.Opening;
            _lidHandle.gameObject.SetActive(usingHandle);
            if (usingHandle)
            {
                var a = _carrySocket.position;
                var b = _lid.position + Vector3.up * .05f;
                var offset = b - a;
                _lidHandle.position = (a + b) * .5f;
                _lidHandle.rotation = Quaternion.FromToRotation(Vector3.up, offset.normalized);
                var parentScale = _lidHandle.parent.lossyScale;
                _lidHandle.localScale = new Vector3(.025f / parentScale.x, offset.magnitude * .5f / parentScale.y, .025f / parentScale.z);
            }
        }
    }
}
