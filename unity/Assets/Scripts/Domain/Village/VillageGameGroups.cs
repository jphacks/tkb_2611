using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed partial class VillageGame
    {
        private bool _groupViewActive;
        private long _groupElapsed,_groupTick;
        private readonly Dictionary<Item,int> _groupProgress=new Dictionary<Item,int>();
        public bool GroupViewActive=>_groupViewActive;
        public void SetGroupViewActive(bool active){_groupViewActive=active;}
        private int GroupRoom(Node n,Item item)=>Math.Min(VillageGroupRules.ProductCap-n.Held(item)-_cargo.Count(c=>c.To==n.Id&&c.Item==item),
            NodeCapacity(n)-n.Stock.Values.Sum()-_cargo.Count(c=>c.To==n.Id)-(n.Finish>0&&RecipeOf(n)!=null?RecipeOf(n).Output.Count:0));
        private bool GroupRunning(Node n)
        {
            var r=RecipeOf(n);if(r==null||!_unlimited&&_gas<1)return false;
            if(n.Finish>_at)return true;
            return (_unlimited||r.Inputs.All(i=>n.Held(i.Item)>=i.Count))&&n.Held(r.Output.Item)+r.Output.Count<=OutputCap(n)&&
                n.Stock.Values.Sum()-(_unlimited?0:r.Inputs.Sum(i=>i.Count))+_cargo.Count(c=>c.To==n.Id)+r.Output.Count<=NodeCapacity(n);
        }
        private List<VillageSynergyView> FindSynergies()
        {
            var result=new List<VillageSynergyView>();
            var index=_nodes.ToDictionary(n=>(n.X,n.Y,n.Kind));
            foreach(var def in VillageGroupRules.Definitions)
            {
                var candidates=new List<(int X,int Y,int Orientation,Node[] Nodes)>();
                for(var oi=0;oi<def.Orientations.Count;oi++)
                {
                    var cells=def.Orientations[oi];var first=cells[0];
                    foreach(var start in _nodes.Where(n=>n.Kind==first.Kind))
                    {
                        var x=start.X-first.X*VillageGroupRules.AnchorSpacing;var y=start.Y-first.Y*VillageGroupRules.AnchorSpacing;
                        var nodes=cells.Select(c=>index.TryGetValue((x+c.X*VillageGroupRules.AnchorSpacing,y+c.Y*VillageGroupRules.AnchorSpacing,c.Kind),out var n)?n:null).ToArray();
                        if(nodes.All(n=>n!=null))candidates.Add((x,y,oi,nodes));
                    }
                }
                var used=new HashSet<int>();
                foreach(var candidate in candidates.OrderBy(c=>c.Y).ThenBy(c=>c.X).ThenBy(c=>c.Orientation))
                {
                    var nodes=candidate.Nodes;if(nodes.Any(n=>used.Contains(n.Id)))continue;
                    foreach(var n in nodes)used.Add(n.Id);
                    var core=nodes[def.CoreIndex];var running=nodes.All(GroupRunning);var room=GroupRoom(core,def.Product)>0;
                    var active=_groupViewActive&&running&&room;
                    result.Add(new VillageSynergyView{Name=def.Name,BonusItem=def.Product,CoreNodeId=core.Id,
                        NodeIds=Array.AsReadOnly(nodes.Select(n=>n.Id).OrderBy(id=>id).ToArray()),MinX=candidate.X,MinY=candidate.Y,
                        Width=nodes.Max(n=>n.X+n.Size)-candidate.X,Height=nodes.Max(n=>n.Y+n.Size)-candidate.Y,
                        Active=active,BonusPerMinute=active?VillageGroupRules.ItemsPerMinute:0,
                        Status=!_groupViewActive?"全体表示で追加生産":!running?"施設の稼働待ち":!room?"中心施設の空き待ち":"追加生産中"});
                }
            }
            return result;
        }
        private void AdvanceGroupClock(long milliseconds)
        {
            if(!_groupViewActive)return;
            _groupElapsed+=milliseconds;
            if(_groupElapsed<VillageGroupRules.IntervalMs)return;
            _groupElapsed-=VillageGroupRules.IntervalMs;_groupTick++;
            var shapes=FindSynergies();
            foreach(var def in VillageGroupRules.Definitions)
            {
                var accepted=shapes.Where(s=>s.BonusItem==def.Product&&s.Active&&GroupRoom(_nodes.First(n=>n.Id==s.CoreNodeId),def.Product)>0).ToArray();
                var progress=(_groupProgress.TryGetValue(def.Product,out var p)?p:0)+accepted.Length;
                var gifts=progress/VillageGroupRules.ProgressPerItem;
                _groupProgress[def.Product]=progress%VillageGroupRules.ProgressPerItem;
                if(accepted.Length==0)continue;
                var start=(int)(_groupTick%accepted.Length);
                for(var j=0;j<gifts;j++)
                {
                    // Recheck shared-core space after each product and each gift.
                    var core=_nodes.First(n=>n.Id==accepted[(start+j)%accepted.Length].CoreNodeId);
                    if(GroupRoom(core,def.Product)<=0)continue;
                    core.Stock[def.Product]=core.Held(def.Product)+1;_produced++;_history.Add((_at,0,1));RecordMade(def.Product,1);
                }
            }
        }
        private static IEnumerable<Item> SendableItems(Node n)
        {
            // Rare group goods go first so a continuously replenished normal output cannot starve them.
            foreach(var def in VillageGroupRules.Definitions)
                if(def.Orientations[0][def.CoreIndex].Kind==n.Kind)yield return def.Product;
            var recipe=RecipeOf(n);if(recipe!=null)yield return recipe.Output.Item;
        }
        private static bool Accepts(Node n,Item item)=>n.Kind==FacilityKind.Warehouse||RecipeOf(n)!=null&&RecipeOf(n).Inputs.Any(i=>i.Item==item);
    }
}
