using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.Industrial
{
    /// <summary>
    /// 記録の「次のガスまで」の輪。下地の輪の上に、進み具合 (0〜1) の弧を上から時計回りに描く。
    /// 色は USS の独自の値 (--ring-track-color / --ring-fill-color) から受け取る。
    /// </summary>
    [UxmlElement]
    public partial class StepRing : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_TrackColor = new CustomStyleProperty<Color>("--ring-track-color");
        private static readonly CustomStyleProperty<Color> s_FillColor = new CustomStyleProperty<Color>("--ring-fill-color");
        private static readonly CustomStyleProperty<float> s_Width = new CustomStyleProperty<float>("--ring-width");

        private Color _track = new Color(0.85f, 0.93f, 0.99f, 1f); // ice-100
        private Color _fill = new Color(0.95f, 0.46f, 0.18f, 1f); // res-gas
        private float _width = 18f;
        private float _progress;

        /// <summary>進み具合 (0〜1)。</summary>
        public float Progress
        {
            get => _progress;
            set
            {
                var v = float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
                if (Mathf.Approximately(v, _progress)) return;
                _progress = v;
                MarkDirtyRepaint();
            }
        }

        public StepRing()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(s_TrackColor, out var t)) _track = t;
            if (evt.customStyle.TryGetValue(s_FillColor, out var f)) _fill = f;
            if (evt.customStyle.TryGetValue(s_Width, out var w) && w > 0) _width = w;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * .5f - _width * .5f;
            if (radius <= 0) return;

            var painter = ctx.painter2D;
            painter.lineWidth = _width;
            painter.lineCap = LineCap.Round;

            painter.strokeColor = _track;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            if (_progress <= 0) return;
            painter.strokeColor = _fill;
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + 360f * _progress);
            painter.Stroke();
        }
    }
}
