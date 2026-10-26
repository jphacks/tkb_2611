using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Village;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>Directionless bridges identify production combos separately from delivery arrows.</summary>
    public sealed class VillageComboWorldView
    {
        private static readonly Color Purple = new Color32(148, 112, 214, 255);
        private readonly RectTransform _canvas;
        private readonly List<Image> _links = new List<Image>();
        private readonly List<Image> _edges = new List<Image>();
        private int _usedLinks, _usedEdges;

        public VillageComboWorldView(RectTransform canvas) { _canvas = canvas; }

        public void Render(IReadOnlyList<VillageNodeView> nodes, int selected, bool preview)
        {
            _usedLinks = _usedEdges = 0;
            var related = new HashSet<int>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var a = nodes[i];
                if (!VillageComboRules.Supports(a.Kind)) continue;
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    var b = nodes[j];
                    if (a.Kind != b.Kind || a.Zoom != b.Zoom || a.X != b.X && a.Y != b.Y
                        || System.Math.Abs(a.X-b.X)+System.Math.Abs(a.Y-b.Y)!=a.Size) continue;
                    var highlighted = a.Id == selected || b.Id == selected;
                    if (highlighted) { related.Add(a.Id); related.Add(b.Id); }
                    var line = Get(_links, _usedLinks++, ProductionWorldNames.ComboLink);
                    var pa = VillageWorldRenderer.Flat(VillageWorldRenderer.Position(a.X, a.Y, a.Size));
                    var pb = VillageWorldRenderer.Flat(VillageWorldRenderer.Position(b.X, b.Y, b.Size));
                    var dir = (pb - pa).normalized;
                    VillageModels.Line(line, pa + dir * (a.Size * VillageWorldRenderer.Cell * .40f),
                        pb - dir * (b.Size * VillageWorldRenderer.Cell * .40f), highlighted ? .105f : .06f);
                    line.color = Tint(highlighted ? .95f : .32f);
                }
            }
            foreach (var node in nodes.Where(n => related.Contains(n.Id)))
            {
                var center = VillageWorldRenderer.Flat(VillageWorldRenderer.Position(node.X, node.Y, node.Size));
                var half = node.Size * VillageWorldRenderer.Cell * .49f;
                var corners = new[] { center + new Vector2(-half, -half), center + new Vector2(half, -half),
                    center + new Vector2(half, half), center + new Vector2(-half, half) };
                for (var edge = 0; edge < 4; edge++)
                {
                    var line = Get(_edges, _usedEdges++, ProductionWorldNames.ComboOutline);
                    VillageModels.Line(line, corners[edge], corners[(edge + 1) % 4], .025f);
                    line.color = Tint(preview ? .8f : .6f);
                }
            }
            for (var i = _usedLinks; i < _links.Count; i++) _links[i].enabled = false;
            for (var i = _usedEdges; i < _edges.Count; i++) _edges[i].enabled = false;
        }

        private static Color Tint(float alpha) => new Color(Purple.r, Purple.g, Purple.b, alpha);
        private Image Get(List<Image> pool, int index, string name)
        {
            while (pool.Count <= index) pool.Add(VillageModels.Rect(name + pool.Count, _canvas, Vector2.zero, Vector2.zero, Purple));
            var image = pool[index]; image.enabled = true; image.transform.SetAsLastSibling(); return image;
        }
    }
}
