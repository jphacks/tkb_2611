using System;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Presentation-only motion and stopped material switching for the asset pack.</summary>
    public sealed class ProductionIslandMotion : MonoBehaviour
    {
        [SerializeField] private Material _runningMaterial;
        [SerializeField] private Material _stoppedMaterial;
        [SerializeField] private bool _running = true;
        [SerializeField] private bool _highHeat;

        private Transform[] _parts;
        private Vector3[] _positions;
        private Quaternion[] _rotations;
        private Vector3[] _scales;
        private Renderer[] _renderers;
        private Material[][] _originalMaterials;
        private ParticleSystem[] _particles;
        private float _phase;

        public void Configure(Material running, Material stopped)
        {
            _runningMaterial = running;
            _stoppedMaterial = stopped;
        }

        public void SetRunning(bool running)
        {
            _running = running;
            ApplyState();
        }

        public void SetHighHeat(bool highHeat)
        {
            _highHeat = highHeat;
            ApplyState();
        }

        private void Awake()
        {
            _parts = Array.FindAll(GetComponentsInChildren<Transform>(true), p => p.name.EndsWith(ModelParts.AnimSuffix, StringComparison.Ordinal));
            _positions = new Vector3[_parts.Length];
            _rotations = new Quaternion[_parts.Length];
            _scales = new Vector3[_parts.Length];
            for (var i = 0; i < _parts.Length; i++)
            {
                _positions[i] = _parts[i].localPosition;
                _rotations[i] = _parts[i].localRotation;
                _scales[i] = _parts[i].localScale;
            }
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalMaterials = new Material[_renderers.Length][];
            for (var i = 0; i < _renderers.Length; i++) _originalMaterials[i] = _renderers[i].sharedMaterials;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            ApplyState();
        }

        private void ApplyState()
        {
            if (_parts == null) return;
            for (var index = 0; index < _renderers.Length; index++)
            {
                var renderer = _renderers[index];
                if (renderer is ParticleSystemRenderer) continue;
                // 稼働中は元のマテリアルに戻すだけなので、_runningMaterial が未設定でも戻す。
                if (_running) { renderer.sharedMaterials = _originalMaterials[index]; continue; }
                var material = _stoppedMaterial;
                if (material == null) continue;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            foreach (var part in _parts)
            {
                if (part.name.StartsWith(ModelParts.FlameHigh, StringComparison.Ordinal)) part.gameObject.SetActive(_running && _highHeat);
                if (part.name.StartsWith(ModelParts.FlameLow, StringComparison.Ordinal)) part.gameObject.SetActive(_running && !_highHeat);
            }
            foreach (var particle in _particles)
            {
                if (_running && !particle.isPlaying) particle.Play();
                else if (!_running) particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Update()
        {
            if (!_running) return;
            _phase += Time.deltaTime;
            for (var i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                var name = part.name;
                var wave = Mathf.Sin(_phase * 3f);
                if (name.Contains(ModelParts.Motion.Rotor))
                    part.localRotation = _rotations[i] * Quaternion.Euler(0f, 0f, _phase * 90f);
                else if (name.Contains(ModelParts.Motion.Waterwheel))
                    part.localRotation = _rotations[i] * Quaternion.Euler(_phase * 90f, 0f, 0f);
                else if (name.Contains(ModelParts.Motion.Grill) || name.Contains(ModelParts.Motion.SortingPlate))
                    part.localRotation = _rotations[i] * Quaternion.Euler(0f, _phase * 65f, 0f);
                else if (name.Contains(ModelParts.Motion.Knife) || name.Contains(ModelParts.Motion.Net) || name.Contains(ModelParts.Motion.Lid))
                    part.localPosition = _positions[i] + Vector3.up * (.04f + .04f * wave);
                else if (name.Contains(ModelParts.Motion.Door))
                    part.localRotation = _rotations[i] * Quaternion.Euler(0f, 12f * wave, 0f);
                else if (name.Contains(ModelParts.Motion.Flame))
                    part.localScale = _scales[i] * (1f + .08f * wave);
                else
                    part.localRotation = _rotations[i] * Quaternion.Euler(0f, 0f, 4f * wave);
            }
        }
    }
}
