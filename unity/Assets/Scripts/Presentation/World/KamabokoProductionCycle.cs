using System;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Readable presentation recipe: fish enters, lid closes, steam, kamaboko emerges.</summary>
    public sealed class KamabokoProductionCycle : MonoBehaviour
    {
        public enum ProcessStage { Loading, Closing, Steaming, Opening, Output, Finished }
        [SerializeField] private Transform _fish;
        [SerializeField] private Transform _product;
        [SerializeField] private Transform _lid;
        [SerializeField] private Transform _chamber;
        [SerializeField] private Transform[] _steam;
        [SerializeField] private bool _running = true;
        private Vector3 _fishStart, _productEnd, _lidClosed;
        private Vector3 _fishUp, _productUp, _lidUp;
        private Vector3[] _steamStart;
        private Vector3[] _steamUp, _steamScale;
        private float _time;
        public const float CycleDuration = 7.5f;
        public float CycleTime => _time;
        public ProcessStage Stage { get; private set; }
        public bool IsRunning => _running;
        public event Action<bool> RunningChanged;

        public void Configure(Transform fish, Transform product, Transform lid, Transform chamber, Transform[] steam)
        {
            _fish = fish; _product = product; _lid = lid; _chamber = chamber; _steam = steam;
        }

        private void Awake()
        {
            if (_fish == null || _product == null || _lid == null || _chamber == null) { enabled = false; return; }
            _fishStart = _fish.localPosition;
            _productEnd = _product.localPosition;
            _lidClosed = _lid.localPosition;
            // FBX root axes and scale differ from Unity world metres.
            _fishUp = _fish.parent.InverseTransformVector(Vector3.up);
            _productUp = _product.parent.InverseTransformVector(Vector3.up);
            _lidUp = _lid.parent.InverseTransformVector(Vector3.up);
            _steamStart = new Vector3[_steam.Length];
            _steamUp = new Vector3[_steam.Length];
            _steamScale = new Vector3[_steam.Length];
            for (var i = 0; i < _steam.Length; i++)
            {
                _steamStart[i] = _steam[i].localPosition;
                _steamUp[i] = _steam[i].parent.InverseTransformVector(Vector3.up);
                _steamScale[i] = _steam[i].localScale;
            }
            ApplyPose();
        }

        public void SetRunning(bool running)
        {
            _running = running;
            if (_steamStart != null) ApplyPose();
            RunningChanged?.Invoke(running);
        }
        public void RestartCycle() { _time = 0f; if (_steamStart != null) ApplyPose(); }
        private void Update()
        {
            if (!_running) return;
            _time = Mathf.Repeat(_time + Time.deltaTime, CycleDuration);
            ApplyPose();
        }

        private void ApplyPose()
        {
            var chamber = _fish.parent.InverseTransformPoint(_chamber.position);
            var t = _time;
            Stage = t < 1.3f ? ProcessStage.Loading : t < 2f ? ProcessStage.Closing : t < 4.3f ? ProcessStage.Steaming : t < 4.9f ? ProcessStage.Opening : t < 6.3f ? ProcessStage.Output : ProcessStage.Finished;
            _fish.gameObject.SetActive(t < 1.85f);
            var fishTravel = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 1.3f));
            // Hold at the worker's flippers until the final lift into the basket.
            var fishHeight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .8f) / .5f));
            var fishPosition = Vector3.Lerp(_fishStart, chamber, fishTravel);
            var heldHeight = _fish.parent.TransformPoint(_fishStart).y;
            var desiredHeight = Mathf.Lerp(heldHeight, _chamber.position.y + .2f, fishHeight);
            _fish.localPosition = fishPosition + _fishUp * (desiredHeight - _fish.parent.TransformPoint(fishPosition).y);
            if (t >= 1.3f) _fish.localPosition = Vector3.Lerp(chamber + _fishUp * .2f, chamber - _fishUp * .07f, Mathf.Clamp01((t - 1.3f) / .55f));
            var lidLift = t < 1.3f ? 1f : t < 2f ? 1f - Mathf.SmoothStep(0, 1, (t - 1.3f) / .7f) : t < 4.3f ? 0f : Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 4.3f) / .6f));
            _lid.localPosition = _lidClosed + _lidUp * (.37f * lidLift);
            _product.gameObject.SetActive(t >= 4.3f);
            var productChamber = _product.parent.InverseTransformPoint(_chamber.position);
            if (t < 4.9f) _product.localPosition = productChamber + _productUp * Mathf.Lerp(-.06f, .16f, Mathf.Clamp01((t - 4.3f) / .6f));
            else
            {
                var travel = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 4.9f) / 1.4f));
                var productHeight = t < 5.3f ? Mathf.Lerp(.75f, .60f, Mathf.SmoothStep(0, 1, (t - 4.9f) / .4f))
                    : t < 6f ? .60f : Mathf.Lerp(.60f, .55f, Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 6f) / .3f)));
                var position = Vector3.Lerp(productChamber, _productEnd, travel);
                var chamberWorldHeight = _chamber.position.y;
                _product.localPosition = position + _productUp * (productHeight - _product.parent.TransformPoint(position).y + chamberWorldHeight - .59f);
            }
            for (var i = 0; i < _steam.Length; i++)
            {
                _steam[i].gameObject.SetActive(_running && Stage == ProcessStage.Steaming);
                var rise = Mathf.Repeat((t - 2f) * .7f + i * .32f, 1f);
                _steam[i].localPosition = _steamStart[i] + _steamUp[i] * (.24f * rise);
                _steam[i].localScale = _steamScale[i] * (.65f + rise * .55f);
            }
        }
    }
}
