using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>One-shot floating reward and sinking ice presentation.</summary>
    public sealed class ProductionIslandBurst : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private bool _sink;
        [SerializeField] private float _duration = 1.2f;
        private Vector3 _start;
        private float _time;
        private TextMesh _text;

        public void Configure(Transform visual, bool sink)
        {
            _visual = visual;
            _sink = sink;
        }

        private void Awake()
        {
            if (_visual == null) return;
            _start = _visual.localPosition;
            _text = _visual.GetComponentInChildren<TextMesh>();
        }

        private void OnEnable()
        {
            _time = 0f;
            if (_visual != null)
            {
                _visual.gameObject.SetActive(true);
                _visual.localPosition = _start;
            }
            if (_text != null)
            {
                var color = _text.color;
                color.a = 1f;
                _text.color = color;
            }
        }

        private void Update()
        {
            if (_visual == null) return;
            _time += Time.deltaTime;
            var fraction = Mathf.Clamp01(_time / Mathf.Max(.01f, _duration));
            _visual.localPosition = _start + Vector3.up * (fraction * (_sink ? -.35f : .4f));
            if (_text != null) _text.color = new Color(1f,1f,1f,1f-fraction);
            if (fraction >= 1f) _visual.gameObject.SetActive(false);
        }
    }
}
