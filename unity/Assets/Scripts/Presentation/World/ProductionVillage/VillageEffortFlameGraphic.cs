using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>A camera-facing, texture-free flame marker. One mesh holds all tongues and cores.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VillageEffortFlameGraphic : MaskableGraphic
    {
        public int Effort { get; private set; }
        public bool Animated { get; private set; }
        private int _frame = -1;
        private float _phase;
        private static readonly Vector2[] Outline = BuildOutline();
        private static readonly Color Outer = new Color32(235, 77, 26, 255);
        private static readonly Color Middle = new Color32(255, 167, 43, 255);
        private static readonly Color Core = new Color32(255, 240, 157, 255);

        public void Render(int effort, bool animated, float seconds)
        {
            effort = Mathf.Clamp(effort, 0, 3);
            var frame = animated ? Mathf.FloorToInt(seconds * 12) : 0;
            if (Effort == effort && Animated == animated && _frame == frame) return;
            Effort = effort; Animated = animated; _frame = frame; _phase = frame / 12f;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Effort == 0) return;
            var rect = rectTransform.rect;
            var height = rect.height * (.55f + .14f * Effort);
            var width = height * .62f;
            var spacing = width * .90f;
            for (var i = 0; i < Effort; i++)
            {
                var wave = Animated ? Mathf.Sin(_phase * (8 + i) + i * 2.1f) : 0;
                var h = height * (1 + wave * .055f) * (Effort == 3 && i != 1 ? .80f : 1);
                var center = new Vector2(rect.center.x + (i - (Effort - 1) * .5f) * spacing, rect.yMin + rect.height * .06f);
                var sway = wave * .035f * width;
                Layer(vh, center, width, h, sway, Outer);
                Layer(vh, center + Vector2.up * h * .035f, width * .64f, h * .70f, sway * .5f, Middle);
                Layer(vh, center + Vector2.up * h * .055f, width * .28f, h * .40f, 0, Core);
                if (Effort >= 2)
                {
                    var travel = Animated ? Mathf.Repeat(_phase * .9f + i * .37f, 1) : .45f;
                    var p = center + new Vector2((i % 2 == 0 ? -.15f : .17f) * width, h * (1.04f + travel * .25f));
                    Diamond(vh, p, width * .055f * (1 - travel * .5f), Core);
                }
            }
        }

        private static void Layer(VertexHelper vh, Vector2 center, float width, float height, float sway, Color tint)
        {
            var first = vh.currentVertCount;
            vh.AddVert(center + new Vector2(0, height * .32f), tint, Vector2.zero);
            foreach (var p in Outline)
                vh.AddVert(center + new Vector2(p.x * width + p.y * p.y * sway, p.y * height), tint, Vector2.zero);
            for (var i = 0; i < Outline.Length; i++) vh.AddTriangle(first, first + 1 + (i + 1) % Outline.Length, first + 1 + i);
        }

        private static void Diamond(VertexHelper vh, Vector2 p, float size, Color tint)
        {
            var first = vh.currentVertCount;
            vh.AddVert(p + Vector2.up * size * 1.6f, tint, Vector2.zero);
            vh.AddVert(p + Vector2.right * size, tint, Vector2.zero);
            vh.AddVert(p - Vector2.up * size * 1.6f, tint, Vector2.zero);
            vh.AddVert(p - Vector2.right * size, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }

        private static Vector2[] BuildOutline()
        {
            // Smooth silhouette with a bent tip and a small shoulder on the left.
            var points = new System.Collections.Generic.List<Vector2>();
            void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                for (var i = 0; i < 8; i++)
                {
                    var t = i / 8f; var u = 1 - t;
                    points.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
                }
            }
            Curve(new Vector2(0, 0), new Vector2(-.6f, 0), new Vector2(-.62f, .38f), new Vector2(-.30f, .67f));
            Curve(new Vector2(-.30f, .67f), new Vector2(-.36f, .45f), new Vector2(-.07f, .40f), new Vector2(.04f, 1));
            Curve(new Vector2(.04f, 1), new Vector2(.06f, .76f), new Vector2(.56f, .55f), new Vector2(.46f, .27f));
            Curve(new Vector2(.46f, .27f), new Vector2(.43f, .07f), new Vector2(.23f, 0), new Vector2(0, 0));
            return points.ToArray();
        }
    }
}
