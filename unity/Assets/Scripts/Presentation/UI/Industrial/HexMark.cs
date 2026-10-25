using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.Industrial
{
    /// <summary>
    /// 生産レポート見出し用のタクティカル六角形マーク (外六角形・縁・内六角形)。
    /// </summary>
    [UxmlElement]
    public partial class HexMark : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_OuterColor = new CustomStyleProperty<Color>("--hex-outer-color");
        private static readonly CustomStyleProperty<Color> s_StrokeColor = new CustomStyleProperty<Color>("--hex-stroke-color");
        private static readonly CustomStyleProperty<Color> s_InnerColor = new CustomStyleProperty<Color>("--hex-inner-color");

        private Color _outerColor = new Color(1.0f, 0.79f, 0.16f, 1f); // #FFC928 (Yellow)
        private Color _strokeColor = new Color(0.09f, 0.22f, 0.37f, 1f); // #17385F (Navy)
        private Color _innerColor = new Color(0.09f, 0.22f, 0.37f, 1f); // #17385F (Navy)

        public HexMark()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(s_OuterColor, out var o)) _outerColor = o;
            if (evt.customStyle.TryGetValue(s_StrokeColor, out var s)) _strokeColor = s;
            if (evt.customStyle.TryGetValue(s_InnerColor, out var i)) _innerColor = i;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var w = contentRect.width;
            var h = contentRect.height;
            if (w <= 0 || h <= 0) return;

            var sx = w / 40f;
            var sy = h / 46f;
            var painter = ctx.painter2D;

            // 外側六角形
            painter.fillColor = _outerColor;
            painter.strokeColor = _strokeColor;
            painter.lineWidth = 4f * Mathf.Min(sx, sy);
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(new Vector2(20f * sx, 3f * sy));
            painter.LineTo(new Vector2(37f * sx, 13f * sy));
            painter.LineTo(new Vector2(37f * sx, 33f * sy));
            painter.LineTo(new Vector2(20f * sx, 43f * sy));
            painter.LineTo(new Vector2(3f * sx, 33f * sy));
            painter.LineTo(new Vector2(3f * sx, 13f * sy));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            // 内側六角形
            painter.fillColor = _innerColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(20f * sx, 12f * sy));
            painter.LineTo(new Vector2(29f * sx, 17f * sy));
            painter.LineTo(new Vector2(29f * sx, 29f * sy));
            painter.LineTo(new Vector2(20f * sx, 34f * sy));
            painter.LineTo(new Vector2(11f * sx, 29f * sy));
            painter.LineTo(new Vector2(11f * sx, 17f * sy));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
