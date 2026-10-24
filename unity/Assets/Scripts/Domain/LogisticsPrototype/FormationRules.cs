using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    /// <summary>Placement rules adapted from penguin-village evolve.ts. Wires are a separate concern.</summary>
    internal static class FormationRules
    {
        private static readonly int[][] Straight = {new[]{0,0,1,0,2,0},new[]{0,0,0,1,0,2}};
        private static readonly int[][] Corners = {new[]{0,0,1,0,0,1},new[]{1,0,0,0,1,1},new[]{1,1,1,0,0,1},new[]{0,1,0,0,1,1}};
        internal static List<FormationView> Find(IReadOnlyList<WorkshopState> nodes)
        {
            var result=new List<FormationView>();
            foreach(var product in new[]{WorkshopGood.Chikuwa,WorkshopGood.Kamaboko,WorkshopGood.Himono})
            {
                var candidates=new List<(FormationView Shape,int Orientation)>();
                var patterns=product==WorkshopGood.Himono?Corners:Straight;
                for(var oi=0;oi<patterns.Length;oi++)
                {
                    var p=patterns[oi];
                    var kinds=product==WorkshopGood.Chikuwa?new[]{WorkshopKind.Fishery,WorkshopKind.Fishery,WorkshopKind.Fishery}
                        :product==WorkshopGood.Kamaboko?new[]{WorkshopKind.Fishery,WorkshopKind.Saltworks,WorkshopKind.Fishery}
                        :new[]{WorkshopKind.Saltworks,WorkshopKind.Fishery,WorkshopKind.Fishery};
                    foreach(var first in nodes.Where(n=>n.Kind==kinds[0]))
                    {
                        var x=first.X-2*p[0];var y=first.Y-2*p[1];
                        var found=Enumerable.Range(0,3).Select(i=>nodes.FirstOrDefault(n=>n.X==x+2*p[i*2]&&n.Y==y+2*p[i*2+1]&&n.Kind==kinds[i])).ToArray();
                        if(found.Any(n=>n==null)) continue;
                        var core=product==WorkshopGood.Chikuwa?found[1]:found.First(n=>n.Kind==WorkshopKind.Saltworks);
                        candidates.Add((new FormationView(product,core.Id,found.Select(n=>n.Id),x,y,found.Max(n=>n.X)-x+2,found.Max(n=>n.Y)-y+2),oi));
                    }
                }
                var used=new HashSet<int>();var seen=new HashSet<string>();
                foreach(var candidate in candidates.OrderBy(c=>c.Shape.Y).ThenBy(c=>c.Shape.X).ThenBy(c=>c.Orientation))
                {
                    var s=candidate.Shape;
                    if(!seen.Add(s.Key)||s.Members.Any(used.Contains)) continue;
                    foreach(var id in s.Members) used.Add(id);
                    result.Add(s);
                }
            }
            return result;
        }
    }
}
