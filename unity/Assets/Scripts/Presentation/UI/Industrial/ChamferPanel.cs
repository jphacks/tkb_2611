using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.Industrial
{
    /// <summary>
    /// 四隅の角を45度に欠いたタクティカルパネル。Painter2D で描画。
    /// </summary>
    [UxmlElement]
    public partial class ChamferPanel : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_FillColor = new CustomStyleProperty<Color>("--chamfer-fill");
        private static readonly CustomStyleProperty<float> s_Chamfer = new CustomStyleProperty<float>("--chamfer");

        private Color _fillColor = new Color(0.09f, 0.22f, 0.37f, 1f); // #17385F
        private float _chamfer = 14f;

        [UxmlAttribute]
        public float Chamfer
        {
            get => _chamfer;
            set { _chamfer = Mathf.Max(0f, value); MarkDirtyRepaint(); }
        }

        public ChamferPanel()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(s_FillColor, out var col)) _fillColor = col;
            if (evt.customStyle.TryGetValue(s_Chamfer, out var ch)) _chamfer = Mathf.Max(0f, ch);
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var w = contentRect.width;
            var h = contentRect.height;
            if (w <= 0 || h <= 0) return;

            var c = Mathf.Min(_chamfer, Mathf.Min(w * 0.5f, h * 0.5f));
            var painter = ctx.painter2D;
            painter.fillColor = _fillColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(c, 0));
            painter.LineTo(new Vector2(w - c, 0));
            painter.LineTo(new Vector2(w, c));
            painter.LineTo(new Vector2(w, h - c));
            painter.LineTo(new Vector2(w - c, h));
            painter.LineTo(new Vector2(c, h));
            painter.LineTo(new Vector2(0, h - c));
            painter.LineTo(new Vector2(0, c));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
