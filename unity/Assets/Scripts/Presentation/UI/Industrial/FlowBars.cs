using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.Industrial
{
    /// <summary>
    /// 1分刻みの生産 (上向き: 黄) と消費 (下向き: 水色/赤) を描画するフローバーチャート。
    /// </summary>
    [UxmlElement]
    public partial class FlowBars : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_UpColor = new CustomStyleProperty<Color>("--bars-up-color");
        private static readonly CustomStyleProperty<Color> s_DownColor = new CustomStyleProperty<Color>("--bars-down-color");
        private static readonly CustomStyleProperty<Color> s_OverColor = new CustomStyleProperty<Color>("--bars-over-color");
        private static readonly CustomStyleProperty<Color> s_AxisColor = new CustomStyleProperty<Color>("--bars-axis-color");

        private Color _upColor = new Color(1.0f, 0.79f, 0.16f, 1f); // #FFC928 (Yellow)
        private Color _downColor = new Color(0.36f, 0.78f, 0.94f, 1f); // #5CC8F0 (Cyan)
        private Color _overColor = new Color(1.0f, 0.35f, 0.37f, 1f); // #FF5A5F (Red)
        private Color _axisColor = new Color(1f, 1f, 1f, 0.25f);

        private int[] _made = Array.Empty<int>();
        private int[] _used = Array.Empty<int>();

        public FlowBars()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public void SetData(int[] made, int[] used)
        {
            _made = made ?? Array.Empty<int>();
            _used = used ?? Array.Empty<int>();
            MarkDirtyRepaint();
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(s_UpColor, out var u)) _upColor = u;
            if (evt.customStyle.TryGetValue(s_DownColor, out var d)) _downColor = d;
            if (evt.customStyle.TryGetValue(s_OverColor, out var o)) _overColor = o;
            if (evt.customStyle.TryGetValue(s_AxisColor, out var a)) _axisColor = a;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var w = contentRect.width;
            var h = contentRect.height;
            if (w <= 0 || h <= 0) return;

            var painter = ctx.painter2D;
            var mid = h * 0.5f;

            // 中央軸線
            painter.strokeColor = _axisColor;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, mid));
            painter.LineTo(new Vector2(w, mid));
            painter.Stroke();

            var count = Math.Max(_made.Length, _used.Length);
            if (count == 0) return;

            var maxVal = 1;
            for (var i = 0; i < _made.Length; i++) if (_made[i] > maxVal) maxVal = _made[i];
            for (var i = 0; i < _used.Length; i++) if (_used[i] > maxVal) maxVal = _used[i];

            var slot = w / count;
            var bw = slot * 0.58f;
            var maxH = mid - 4f;

            for (var i = 0; i < count; i++)
            {
                var p = i < _made.Length ? _made[i] : 0;
                var c = i < _used.Length ? _used[i] : 0;
                var x = i * slot + (slot - bw) * 0.5f;

                // 上向き (生産)
                if (p > 0)
                {
                    var ph = maxH * p / maxVal;
                    painter.fillColor = _upColor;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, mid - 2f));
                    painter.LineTo(new Vector2(x + bw, mid - 2f));
                    painter.LineTo(new Vector2(x + bw, mid - 2f - ph));
                    painter.LineTo(new Vector2(x, mid - 2f - ph));
                    painter.ClosePath();
                    painter.Fill();
                }

                // 下向き (消費)
                if (c > 0)
                {
                    var ch = maxH * c / maxVal;
                    var over = c > p;
                    painter.fillColor = over ? _overColor : _downColor;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, mid + 2f));
                    painter.LineTo(new Vector2(x + bw, mid + 2f));
                    painter.LineTo(new Vector2(x + bw, mid + 2f + ch));
                    painter.LineTo(new Vector2(x, mid + 2f + ch));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
