using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>ある場所に施設を置いたら、どの施設とつながるか。配送の規則 (Links) と同じ判定で、村の状態は変えない。</summary>
    public sealed class VillageLinkPreview
    {
        /// <summary>置いた施設へ品物を送ってくる施設。</summary>
        public IReadOnlyList<int> Sources {get; internal set;}
        /// <summary>置いた施設が品物を送る施設。</summary>
        public IReadOnlyList<int> Targets {get; internal set;}
        /// <summary>置いた施設から届くマンハッタン距離 (レシピの無い施設は 0)。</summary>
        public int Reach {get; internal set;}
        public bool Connected=>Sources.Count>0||Targets.Count>0;
    }

    /// <summary>あと 1 棟置けば施設群が成立する場所。</summary>
    public sealed class VillageGroupHint
    {
        public string Name {get; internal set;}
        public Item Product {get; internal set;}
        public FacilityKind Kind {get; internal set;}
        public int X {get; internal set;}
        public int Y {get; internal set;}
        /// <summary>すでに置いてあって、この案内に使う施設。</summary>
        public IReadOnlyList<int> NodeIds {get; internal set;}
    }

    public sealed partial class VillageGame
    {
        public VillageLinkPreview PreviewLinks(FacilityKind kind,int x,int y,int ignoreId=0)
        {
            var others=_nodes.Where(n=>n.Id!=ignoreId).ToList();
            var placed=new Node{Id=-1,Kind=kind,X=x,Y=y};
            var all=new List<Node>(others){placed};
            return new VillageLinkPreview{
                Sources=Array.AsReadOnly(others.Where(a=>Links(a,placed,all)).Select(a=>a.Id).ToArray()),
                Targets=Array.AsReadOnly(others.Where(b=>Links(placed,b,all)).Select(b=>b.Id).ToArray()),
                Reach=RecipeOf(placed)==null?0:ReachOf(placed,all)};
        }

        /// <summary>置いてある施設から自動配送が届く距離。レシピの無い施設や見つからない施設は 0。</summary>
        public int ReachOfNode(int id)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);
            return n==null||RecipeOf(n)==null?0:ReachOf(n,_nodes);
        }

        /// <summary>
        /// 図面 (VillageGroupRules) のうち、あと 1 棟で成立する並び。足りない 1 マスが空いていて置けるものだけ。
        /// すでに成立している施設群に使われている施設は数えない。
        /// </summary>
        public IReadOnlyList<VillageGroupHint> GroupHints()
        {
            var used=new HashSet<int>(FindSynergies().SelectMany(s=>s.NodeIds));
            var index=_nodes.Where(n=>!used.Contains(n.Id)).GroupBy(n=>(n.X,n.Y,n.Kind)).ToDictionary(g=>g.Key,g=>g.First());
            var hints=new List<VillageGroupHint>();
            var seen=new HashSet<(string,int,int,FacilityKind)>();
            foreach(var def in VillageGroupRules.Definitions)
            foreach(var cells in def.Orientations)
            foreach(var anchor in cells)
            foreach(var start in index.Values.Where(n=>n.Kind==anchor.Kind))
            {
                var ox=start.X-anchor.X*VillageGroupRules.AnchorSpacing;var oy=start.Y-anchor.Y*VillageGroupRules.AnchorSpacing;
                var present=new List<int>();VillageGroupCell? missing=null;var gaps=0;
                foreach(var c in cells)
                {
                    if(index.TryGetValue((ox+c.X*VillageGroupRules.AnchorSpacing,oy+c.Y*VillageGroupRules.AnchorSpacing,c.Kind),out var n))present.Add(n.Id);
                    else{gaps++;missing=c;}
                }
                if(gaps!=1)continue;
                var m=missing.Value;var x=ox+m.X*VillageGroupRules.AnchorSpacing;var y=oy+m.Y*VillageGroupRules.AnchorSpacing;
                if(Placement(m.Kind,x,y,0)!=""||!seen.Add((def.Name,x,y,m.Kind)))continue;
                hints.Add(new VillageGroupHint{Name=def.Name,Product=def.Product,Kind=m.Kind,X=x,Y=y,NodeIds=Array.AsReadOnly(present.OrderBy(i=>i).ToArray())});
            }
            return hints.OrderBy(h=>h.Y).ThenBy(h=>h.X).ThenBy(h=>h.Name,StringComparer.Ordinal).ToArray();
        }
    }
}
