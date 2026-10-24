using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    internal static class EvolutionFlow
    {
        internal const int StepMs=50, CycleMs=2000, DeliveryMs=1500, OutputCap=6, WarehouseCap=20;
        private static readonly WorkshopGood[] SendOrder={WorkshopGood.Chikuwa,WorkshopGood.Kamaboko,WorkshopGood.Himono,WorkshopGood.Fish,WorkshopGood.Salt};
        internal static void Step(EvolutionState s,long at,List<EvolutionEvent> events)
        {
            // Capture dispatch eligibility before arrivals and production. New goods wait for the next step.
            var dispatches=new List<(RouteView Route,WorkshopGood Good)>();
            var reserved=s.Cargo.GroupBy(c=>c.To).ToDictionary(g=>g.Key,g=>g.Count());
            var available=s.Workshops.ToDictionary(n=>n.Id,n=>new Dictionary<WorkshopGood,int>(n.Stock));
            foreach(var route in s.Routes.OrderBy(r=>r.From).ThenBy(r=>r.To))
            {
                if(s.Cargo.Any(c=>c.From==route.From&&c.To==route.To)) continue;
                var to=s.Workshops.First(n=>n.Id==route.To);
                var room=WarehouseCap-to.Stock.Values.Sum()-(reserved.TryGetValue(to.Id,out var r)?r:0);
                if(room<=0) continue;
                foreach(var good in SendOrder)
                {
                    if(!available[route.From].TryGetValue(good,out var n)||n<=0) continue;
                    available[route.From][good]--;reserved[to.Id]=(reserved.TryGetValue(to.Id,out r)?r:0)+1;
                    dispatches.Add((route,good));break;
                }
            }
            foreach(var cargo in s.Cargo.Where(c=>c.ArriveAt<=at).ToArray())
            {
                s.Workshops.First(n=>n.Id==cargo.To).Add(cargo.Good,1);s.Cargo.Remove(cargo);
                events.Add(new EvolutionEvent(at,EvolutionEventKind.Arrived,cargo.To,cargo.Good));
            }
            if(at%CycleMs==0)
            {
                var working=new HashSet<int>();
                foreach(var n in s.Workshops.Where(n=>n.Kind!=WorkshopKind.Warehouse).OrderBy(n=>n.Id))
                {
                    var good=n.Kind==WorkshopKind.Fishery?WorkshopGood.Fish:WorkshopGood.Salt;
                    if(n.Held(good)>=OutputCap) continue;
                    n.Add(good,1);working.Add(n.Id);events.Add(new EvolutionEvent(at,EvolutionEventKind.Produced,n.Id,good));
                }
                foreach(var good in new[]{WorkshopGood.Chikuwa,WorkshopGood.Kamaboko,WorkshopGood.Himono})
                {
                    var accepted=s.Formations.Where(f=>f.Product==good&&f.Members.All(working.Contains)&&s.Workshops.First(n=>n.Id==f.CoreId).Held(good)<OutputCap).ToArray();
                    var progress=(s.ShapeProgress.TryGetValue(good,out var p)?p:0)+accepted.Length;
                    var amount=progress/2;s.ShapeProgress[good]=progress%2;
                    if(accepted.Length==0) continue;
                    var start=(int)(at/CycleMs%accepted.Length);
                    for(var i=0;i<amount;i++)
                    {
                        var core=s.Workshops.First(n=>n.Id==accepted[(start+i)%accepted.Length].CoreId);
                        core.Add(good,1);events.Add(new EvolutionEvent(at,EvolutionEventKind.Produced,core.Id,good));
                    }
                }
            }
            foreach(var d in dispatches)
            {
                var from=s.Workshops.First(n=>n.Id==d.Route.From);var to=s.Workshops.First(n=>n.Id==d.Route.To);
                from.Add(d.Good,-1);
                s.Cargo.Add(new CargoView(s.NextCargo++,d.Route.From,d.Route.To,d.Good,at,at+DeliveryMs,from.X,from.Y,to.X,to.Y));
                events.Add(new EvolutionEvent(at,EvolutionEventKind.Dispatched,d.Route.From,d.Good));
            }
            s.StepAt=at;
        }
    }
}
