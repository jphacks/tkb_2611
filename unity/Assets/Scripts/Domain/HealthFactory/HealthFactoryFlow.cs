using System.Collections.Generic;
using System.Linq;
namespace ProjectPenguin.Domain.HealthFactory
{
    internal static class HealthFactoryFlow
    {
        internal static void Tick(HealthFactoryState s,List<HealthFactoryEvent> events)
        {
            // Candidates use the start of the tick. New arrivals wait for the next tick.
            var processing=s.ProcessEnd==0&&s.Input>0&&s.Output<6;
            var shipRaw=s.Routes[0]&&s.Raw>0&&s.Deliveries.Count(d=>d.Route==0)<s.Slots
                &&s.Input+s.Deliveries.Count(d=>d.Route==0)<s.InputCapacity;
            var shipOutput=s.Routes[1]&&s.Output>0&&s.Deliveries.Count(d=>d.Route==1)<s.Slots
                &&s.Stored+s.Deliveries.Count(d=>d.Route==1)<20;
            s.AtMs+=50;
            foreach(var d in s.Deliveries.Where(d=>d.ArriveAt<=s.AtMs).ToArray())
            {if(d.Route==0)s.Input++;else s.Stored++;s.Deliveries.Remove(d);events.Add(new HealthFactoryEvent(s.AtMs,"Arrival",d.Route+1));}
            if(s.ProcessEnd>0&&s.ProcessEnd<=s.AtMs)
            {s.ProcessEnd=0;s.Output++;events.Add(new HealthFactoryEvent(s.AtMs,"Processed",1));}
            if(processing&&s.Gas>=1)
            {
                s.Gas--;s.Spent++;s.Input--;s.ProcessStart=s.AtMs;s.ProcessEnd=s.AtMs+s.ProcessingDurationMs;
                events.Add(new HealthFactoryEvent(s.AtMs,"Processing",1));
            }
            if(shipRaw)Ship(s,0,events);
            if(shipOutput)Ship(s,1,events);
        }
        private static void Ship(HealthFactoryState s,int route,List<HealthFactoryEvent> events)
        {
            var cost=s.RouteCost(route);if(s.Gas<cost)return;
            s.Gas-=cost;s.Spent+=cost;if(route==0)s.Raw--;else s.Output--;
            s.Deliveries.Add(new Delivery{Route=route,FromX=s.X[route],FromY=s.Y[route],ToX=s.X[route+1],ToY=s.Y[route+1],
                PaidGas=cost,DepartAt=s.AtMs,ArriveAt=s.AtMs+1500});events.Add(new HealthFactoryEvent(s.AtMs,"Dispatch",route));
        }
    }
}
