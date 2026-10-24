using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Gestures for the existing separate-mesh Penguin_v002, without changing its mesh.</summary>
    public sealed class WorkshopPenguin : MonoBehaviour
    {
        public enum Gesture { Carry, Work, Wait, Cheer, Rest, Pull, Chop, Turn, Rake, Hang, Harvest, Stir, Inspect, Stamp, Stretch, Adjust, Wave }
        [SerializeField] private Transform _visual, _leftWing, _rightWing, _leftFoot, _rightFoot;
        [SerializeField] private Transform[] _eyes, _glints;
        private Vector3 _visualStart, _leftFootStart, _rightFootStart;
        private Quaternion _leftFootRotation, _rightFootRotation;
        private Vector3[] _eyeScales;
        private bool _initialized;
        public Gesture CurrentGesture { get; private set; }

        public void Configure(Transform visual, Transform leftWing, Transform rightWing,
            Transform leftFoot, Transform rightFoot, Transform[] eyes, Transform[] glints)
        {
            _visual = visual; _leftWing = leftWing; _rightWing = rightWing;
            _leftFoot = leftFoot; _rightFoot = rightFoot; _eyes = eyes; _glints = glints;
        }

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (_initialized) return;
            _visualStart = _visual.localPosition;
            _leftFootStart = _leftFoot.localPosition; _rightFootStart = _rightFoot.localPosition;
            _leftFootRotation = _leftFoot.localRotation; _rightFootRotation = _rightFoot.localRotation;
            _eyeScales = new Vector3[_eyes.Length];
            for (var i = 0; i < _eyes.Length; i++) _eyeScales[i] = _eyes[i].localScale;
            _initialized = true;
        }

        public void ApplyPose(Gesture gesture, float time, float action = 0)
        {
            Initialize(); CurrentGesture = gesture;
            var walking = gesture == Gesture.Carry;
            var wave = Mathf.Sin(time * 14f);
            _visual.localPosition = _visualStart + Vector3.up * (walking ? Mathf.Abs(wave) * .012f : 0);
            var lean = gesture == Gesture.Pull ? -8f - action * 8f : gesture == Gesture.Inspect ? 14f : gesture == Gesture.Stretch ? -9f : 0;
            _visual.localRotation = Quaternion.Euler(lean, 0, walking ? wave * 3f : gesture == Gesture.Rest ? 12f : 0);
            _leftFoot.localPosition = _leftFootStart + Vector3.up * (walking ? Mathf.Max(0, wave) * .025f : 0);
            _rightFoot.localPosition = _rightFootStart + Vector3.up * (walking ? Mathf.Max(0, -wave) * .025f : 0);
            _leftFoot.localRotation = Quaternion.Euler(walking ? wave * 12f : 0, 0, 0) * _leftFootRotation;
            _rightFoot.localRotation = Quaternion.Euler(walking ? -wave * 12f : 0, 0, 0) * _rightFootRotation;
            var lift = walking ? -70f : gesture == Gesture.Work ? -50f + Mathf.Sin(time * 9f) * 5f : 0;
            var inward = walking ? 25f : gesture == Gesture.Work ? 18f : gesture == Gesture.Cheer ? -60f : 0;
            _leftWing.localRotation = Quaternion.Euler(lift, 0, -inward);
            _rightWing.localRotation = Quaternion.Euler(lift, 0, inward);
            switch (gesture)
            {
                case Gesture.Pull:
                case Gesture.Harvest:
                    _leftWing.localRotation = Quaternion.Euler(-45f - action * 35f, action * 12f, -20);
                    _rightWing.localRotation = Quaternion.Euler(-45f - action * 35f, -action * 12f, 20); break;
                case Gesture.Chop:
                    _rightWing.localRotation = Quaternion.Euler(-40f - action * 45f, 0, 18);
                    _leftWing.localRotation = Quaternion.Euler(-55, 0, -22); break;
                case Gesture.Turn:
                case Gesture.Adjust:
                    _rightWing.localRotation = Quaternion.Euler(-65, Mathf.Sin(time * 4f) * 14f, 18);
                    _leftWing.localRotation = Quaternion.Euler(-30, 0, -10); break;
                case Gesture.Rake:
                    _leftWing.localRotation = Quaternion.Euler(-35f - action * 35f, 0, -25);
                    _rightWing.localRotation = Quaternion.Euler(-35f - action * 35f, 0, 25); break;
                case Gesture.Hang:
                    _leftWing.localRotation = Quaternion.Euler(-105, 0, -18);
                    _rightWing.localRotation = Quaternion.Euler(-105, 0, 18); break;
                case Gesture.Stir:
                    _rightWing.localRotation = Quaternion.Euler(-65, Mathf.Sin(time * 2f) * 18f, 22);
                    _leftWing.localRotation = Quaternion.Euler(-20, 0, -8); break;
                case Gesture.Inspect:
                    _rightWing.localRotation = Quaternion.Euler(-95, 0, 15); break;
                case Gesture.Stamp:
                    _rightWing.localRotation = Quaternion.Euler(-75f + action * 38f, 0, 18); break;
                case Gesture.Stretch:
                    _leftWing.localRotation = Quaternion.Euler(-20, 0, -65);
                    _rightWing.localRotation = Quaternion.Euler(-20, 0, 65); break;
                case Gesture.Wave:
                    _rightWing.localRotation = Quaternion.Euler(-30, 0, 50f + Mathf.Sin(time * 8f) * 15f); break;
            }
            for (var i = 0; i < _eyes.Length; i++)
                _eyes[i].localScale = Vector3.Scale(_eyeScales[i], new Vector3(1, gesture == Gesture.Rest ? .12f : 1, 1));
            foreach (var glint in _glints) glint.gameObject.SetActive(gesture != Gesture.Rest);
        }
    }
}
