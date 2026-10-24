using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>World-space, rounded directional link with item-coloured flow beads.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ProductionIslandLink : MonoBehaviour
    {
        public enum VisualState { Normal, Flowing, Blocked, Selected, Drawing }

        [SerializeField] private Material[] _stateMaterials;
        [SerializeField] private Transform _arrow;
        [SerializeField] private Transform[] _beads;
        [SerializeField] private float _flowSpeed = .6f;
        [SerializeField] private VisualState _state;
        private LineRenderer _line;
        private float _distance;
        private Vector3[] _points;
        private float _length;
        private MaterialPropertyBlock _tint;
        private int _visibleBeadCount = 6;

        public void Configure(Material[] materials, Transform arrow, Transform[] beads)
        {
            _stateMaterials = materials;
            _arrow = arrow;
            _beads = beads;
        }

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _points = new Vector3[_line.positionCount];
            _line.GetPositions(_points);
            SetPath(_points);
            SetState(_state);
        }

        public void SetPath(Vector3[] points)
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _points = (Vector3[])points.Clone();
            _line.positionCount = _points.Length;
            _line.SetPositions(_points);
            _length = 0f;
            for (var i = 1; i < _points.Length; i++) _length += Vector3.Distance(_points[i - 1], _points[i]);
            if (_arrow != null && _points.Length >= 2)
            {
                _arrow.position = _points[_points.Length - 1];
                var direction = _points[_points.Length - 1] - _points[_points.Length - 2];
                if (direction.sqrMagnitude > .00001f) _arrow.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            }
        }

        public void SetState(VisualState state)
        {
            _state = state;
            if (_line == null) _line = GetComponent<LineRenderer>();
            if (_stateMaterials != null && _stateMaterials.Length > (int)state) _line.sharedMaterial = _stateMaterials[(int)state];
            _line.widthMultiplier = state == VisualState.Selected ? .12f : .08f;
            if (_beads != null)
                for (var i = 0; i < _beads.Length; i++) _beads[i].gameObject.SetActive(state == VisualState.Flowing && i < _visibleBeadCount);
            if (_arrow != null)
            {
                var renderer = _arrow.GetComponentInChildren<Renderer>();
                if (renderer != null && _stateMaterials != null && _stateMaterials.Length > (int)state)
                    renderer.sharedMaterial = _stateMaterials[(int)state];
            }
        }

        public void SetFlow(Color itemColor, float itemsPerSecond)
        {
            _flowSpeed = Mathf.Max(0f, itemsPerSecond) * .15f;
            _visibleBeadCount = _beads == null ? 0 : Mathf.Clamp(Mathf.CeilToInt(itemsPerSecond * 2f), 0, _beads.Length);
            _tint ??= new MaterialPropertyBlock();
            _tint.SetColor(ShaderIds.BaseColor, itemColor);
            if (_beads != null)
                foreach (var bead in _beads)
                {
                    var renderer = bead.GetComponent<Renderer>();
                    if (renderer != null) renderer.SetPropertyBlock(_tint);
                }
            SetState(itemsPerSecond > 0f ? VisualState.Flowing : VisualState.Normal);
        }

        private void Update()
        {
            if (_state != VisualState.Flowing || _points == null || _length <= .0001f || _beads == null || _visibleBeadCount == 0) return;
            _distance = Mathf.Repeat(_distance + _flowSpeed * Time.deltaTime, _length);
            for (var b = 0; b < _visibleBeadCount; b++)
            {
                var remaining = Mathf.Repeat(_distance + b * _length / _visibleBeadCount, _length);
                for (var i = 1; i < _points.Length; i++)
                {
                    var length = Vector3.Distance(_points[i - 1], _points[i]);
                    if (length > .00001f && remaining <= length)
                    {
                        _beads[b].position = Vector3.Lerp(_points[i - 1], _points[i], remaining / length);
                        break;
                    }
                    remaining -= length;
                }
            }
        }
    }
}
