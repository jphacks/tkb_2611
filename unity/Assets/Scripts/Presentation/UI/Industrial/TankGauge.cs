using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.Industrial
{
    /// <summary>
    /// 六角形ガスタンク・波打つ水位・左右パイプ・流れるシェブロンを描画するゲージ。
    /// </summary>
    [UxmlElement]
    public partial class TankGauge : VisualElement
    {
        private static readonly CustomStyleProperty<Color> s_NavyColor = new CustomStyleProperty<Color>("--ind-navy");
        private static readonly CustomStyleProperty<Color> s_YellowColor = new CustomStyleProperty<Color>("--ind-yellow");
        private static readonly CustomStyleProperty<Color> s_CyanColor = new CustomStyleProperty<Color>("--ind-cyan");

        private Color _navyColor = new Color(0.09f, 0.22f, 0.37f, 1f); // #17385F
        private Color _yellowColor = new Color(1.0f, 0.79f, 0.16f, 1f); // #FFC928
        private Color _cyanColor = new Color(0.36f, 0.78f, 0.94f, 1f); // #5CC8F0
        private Color _liquidColor = new Color(0.18f, 0.42f, 0.66f, 1f); // #2E6BA8
        private Color _pipeInColor = new Color(0.85f, 0.95f, 0.99f, 1f); // #D9F2FC
        private Color _pipeOutColor = new Color(1.0f, 0.95f, 0.78f, 1f); // #FFF2C6

        private float _currentLevel = 0.5f;
        private float _targetLevel = 0.5f;
        private bool _hasSupply = true;
        private bool _hasConsume = true;

        private float _wavePhase = 0f;
        private float _flowPhaseIn = 0f;
        private float _flowPhaseOut = 0f;
        private IVisualElementScheduledItem _animSchedule;

        public TankGauge()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<AttachToPanelEvent>(_ => StartAnimation());
            RegisterCallback<DetachFromPanelEvent>(_ => StopAnimation());
        }

        public void SetLevel(double level, bool hasSupply, bool hasConsume)
        {
            _targetLevel = Mathf.Clamp01((float)level);
            _hasSupply = hasSupply;
            _hasConsume = hasConsume;
            MarkDirtyRepaint();
        }

        public void StartAnimation()
        {
            _animSchedule ??= schedule.Execute(OnAnimate).Every(33);
        }

        public void StopAnimation()
        {
            _animSchedule?.Pause();
            _animSchedule = null;
        }

        private void OnAnimate()
        {
            // 水位をイージング
            _currentLevel = Mathf.MoveTowards(_currentLevel, _targetLevel, 0.04f);

            // 波のアニメーション
            _wavePhase += 1.8f;
            if (_wavePhase > 3600f) _wavePhase -= 3600f;

            // シェブロンの流れ
            if (_hasSupply)
            {
                _flowPhaseIn = (_flowPhaseIn + 1.2f) % 44f;
            }
            if (_hasConsume)
            {
                _flowPhaseOut = (_flowPhaseOut + 1.2f) % 44f;
            }

            MarkDirtyRepaint();
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(s_NavyColor, out var n)) _navyColor = n;
            if (evt.customStyle.TryGetValue(s_YellowColor, out var y)) _yellowColor = y;
            if (evt.customStyle.TryGetValue(s_CyanColor, out var c)) _cyanColor = c;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var w = contentRect.width;
            var h = contentRect.height;
            if (w <= 0 || h <= 0) return;

            var scale = Mathf.Min(w / 960f, h / 440f);
            var ox = (w - 960f * scale) * 0.5f;
            var oy = (h - 440f * scale) * 0.5f;

            var painter = ctx.painter2D;

            float S(float val) => val * scale;
            Vector2 P(float x, float y) => new Vector2(ox + x * scale, oy + y * scale);

            var cx = 480f;
            var cy = 220f;
            var r = 190f;
            var innerR = r - 24f;
            var hw = innerR * 0.866f;

            // 1. パイプ背景
            var pipeH = 56f;
            var pipeY = cy - pipeH * 0.5f;
            var pipeInW = cx - r * 0.866f + 8f;
            var pipeOutX = cx + r * 0.866f - 8f;
            var pipeOutW = 960f - pipeOutX;

            // 左パイプ (供給: 水色系)
            painter.fillColor = _pipeInColor;
            painter.strokeColor = _navyColor;
            painter.lineWidth = S(6f);
            painter.BeginPath();
            painter.MoveTo(P(0, pipeY));
            painter.LineTo(P(pipeInW, pipeY));
            painter.LineTo(P(pipeInW, pipeY + pipeH));
            painter.LineTo(P(0, pipeY + pipeH));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            // 右パイプ (消費: 黄色系)
            painter.fillColor = _pipeOutColor;
            painter.BeginPath();
            painter.MoveTo(P(pipeOutX, pipeY));
            painter.LineTo(P(960f, pipeY));
            painter.LineTo(P(960f, pipeY + pipeH));
            painter.LineTo(P(pipeOutX, pipeY + pipeH));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();

            // 2. パイプ内のシェブロン (›››)
            painter.lineWidth = S(8f);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            if (_hasSupply)
            {
                painter.strokeColor = _cyanColor;
                for (var i = 0; i < 4; i++)
                {
                    var chevX = 30f + i * 44f + _flowPhaseIn;
                    if (chevX < pipeInW - 12f)
                    {
                        painter.BeginPath();
                        painter.MoveTo(P(chevX - 12f, cy - 14f));
                        painter.LineTo(P(chevX, cy));
                        painter.LineTo(P(chevX - 12f, cy + 14f));
                        painter.Stroke();
                    }
                }
            }

            if (_hasConsume)
            {
                painter.strokeColor = new Color(0.94f, 0.68f, 0f, 1f); // Amber Yellow
                for (var i = 0; i < 4; i++)
                {
                    var chevX = pipeOutX + 24f + i * 44f + _flowPhaseOut;
                    if (chevX < 960f - 12f)
                    {
                        painter.BeginPath();
                        painter.MoveTo(P(chevX - 12f, cy - 14f));
                        painter.LineTo(P(chevX, cy));
                        painter.LineTo(P(chevX - 12f, cy + 14f));
                        painter.Stroke();
                    }
                }
            }

            // 3. 外六角形 (紺)
            Vector2 HexPoint(float radius, int idx)
            {
                var angle = (-90 + idx * 60) * Mathf.Deg2Rad;
                return new Vector2(cx + radius * Mathf.Cos(angle), cy + radius * Mathf.Sin(angle));
            }

            painter.fillColor = _navyColor;
            painter.BeginPath();
            var hp0 = HexPoint(r, 0);
            painter.MoveTo(P(hp0.x, hp0.y));
            for (var i = 1; i < 6; i++)
            {
                var hp = HexPoint(r, i);
                painter.LineTo(P(hp.x, hp.y));
            }
            painter.ClosePath();
            painter.Fill();

            // 4. 内六角形 (白)
            painter.fillColor = Color.white;
            painter.BeginPath();
            var ihp0 = HexPoint(innerR, 0);
            painter.MoveTo(P(ihp0.x, ihp0.y));
            for (var i = 1; i < 6; i++)
            {
                var ihp = HexPoint(innerR, i);
                painter.LineTo(P(ihp.x, ihp.y));
            }
            painter.ClosePath();
            painter.Fill();

            // 5. 液面多角形 (内六角形の範囲で切り抜いて Fill)
            float TopY(float x)
            {
                if (x < cx)
                    return (cy - innerR * 0.5f) + ((cy - innerR) - (cy - innerR * 0.5f)) * (x - (cx - hw)) / hw;
                return (cy - innerR) + ((cy - innerR * 0.5f) - (cy - innerR)) * (x - cx) / hw;
            }

            float BottomY(float x)
            {
                if (x < cx)
                    return (cy + innerR * 0.5f) + ((cy + innerR) - (cy + innerR * 0.5f)) * (x - (cx - hw)) / hw;
                return (cy + innerR) + ((cy + innerR * 0.5f) - (cy + innerR)) * (x - cx) / hw;
            }

            var yLiquidTop = (cy + innerR) - 2f * innerR * _currentLevel;
            var pointsTop = new List<Vector2>();
            var pointsBottom = new List<Vector2>();

            var step = 8f;
            for (var x = cx - hw; x <= cx + hw + 1e-4f; x += step)
            {
                var clampedX = Mathf.Min(x, cx + hw);
                var waveY = yLiquidTop + 5f * Mathf.Sin((clampedX + _wavePhase) / 24f);
                var ty = TopY(clampedX);
                var by = BottomY(clampedX);
                var liquidY = Mathf.Clamp(waveY, ty, by);
                pointsTop.Add(P(clampedX, liquidY));
                pointsBottom.Add(P(clampedX, by));
            }

            if (_currentLevel > 0.01f)
            {
                painter.fillColor = _liquidColor;
                painter.BeginPath();
                painter.MoveTo(pointsTop[0]);
                for (var i = 1; i < pointsTop.Count; i++) painter.LineTo(pointsTop[i]);
                for (var i = pointsBottom.Count - 1; i >= 0; i--) painter.LineTo(pointsBottom[i]);
                painter.ClosePath();
                painter.Fill();

                // 液面内の斜線ハッチ (45度ストライプ)
                painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                painter.lineWidth = S(5f);
                for (var diag = cx - hw - innerR; diag <= cx + hw + innerR; diag += 20f)
                {
                    // line: x - y = diag => y = x - diag
                    var xStart = cx - hw;
                    var xEnd = cx + hw;
                    for (var x = xStart; x <= xEnd; x += 12f)
                    {
                        var y = x - diag;
                        var ty = TopY(x);
                        var by = BottomY(x);
                        var waveY = yLiquidTop + 5f * Mathf.Sin((x + _wavePhase) / 24f);
                        if (y >= waveY && y >= ty && y <= by)
                        {
                            var pt = P(x, y);
                            painter.BeginPath();
                            painter.MoveTo(pt);
                            painter.LineTo(P(x + 8f, y + 8f));
                            painter.Stroke();
                        }
                    }
                }
            }

            // 6. 目盛り 3本 (右側内壁: 25%, 50%, 75%)
            painter.strokeColor = _navyColor;
            painter.lineWidth = S(5f);
            painter.lineCap = LineCap.Round;
            foreach (var k in new[] { 0.25f, 0.5f, 0.75f })
            {
                var my = (cy + innerR) - 2f * innerR * k;
                painter.BeginPath();
                painter.MoveTo(P(cx + innerR * 0.866f - 18f, my));
                painter.LineTo(P(cx + innerR * 0.866f + 2f, my));
                painter.Stroke();
            }
        }
    }
}
