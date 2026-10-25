using System.Collections.Generic;
using System.Linq;
namespace ProjectPenguin.Domain.HealthFactory
{
    internal sealed class HealthFactoryState
    {
        internal long AtMs, RequestedMs, Gas, Spent, Water, Electricity, GrantedGas, ProcessStart, ProcessEnd;
        internal int Raw=4, Input, Output, Stored, LastDay=-1, MaxSteps;
        internal bool BufferBuilt, Upgraded, Expanded, PassiveFixture, AssignedFixture;
        internal int[] X={6,6,6}; internal int[] Y={2,8,14}; internal bool[] Routes={true,true};
        internal List<Delivery> Deliveries=new List<Delivery>();
        internal HashSet<string> ReceiptIds=new HashSet<string>();
        internal HashSet<int> WaterDays=new HashSet<int>(), ScreenDays=new HashSet<int>();
        // すれ違いで得たペンギン (種ID→羽数) と、加算の小数の繰り越し (10分の1単位、資源ごと)。
        internal Dictionary<string,int> Penguins=new Dictionary<string,int>();
        internal int GasCarry, ElectricityCarry, WaterCarry;
        internal int BoardSize=>Expanded?20:16;
        internal int InputCapacity=>BufferBuilt?4:2;
        internal int Slots=>BufferBuilt?2:1;
        internal int StepMultiplier=>PassiveFixture?2:1;
        internal int Birds(Encounter.EncounterResource resource)
        {var n=0;foreach(var p in Penguins){var s=Encounter.EncounterPenguins.Find(p.Key);if(s!=null&&s.Resource==resource)n+=p.Value;}return n;}
        internal int ProcessingDurationMs=>(Upgraded?1000:2000)/(AssignedFixture?2:1);
        internal int RouteCost(int route)=>System.Math.Max(1,(System.Math.Abs(X[route]-X[route+1])+System.Math.Abs(Y[route]-Y[route+1])+3)/4);
        internal HealthFactoryState Clone()
        {
            var s=(HealthFactoryState)MemberwiseClone();s.X=(int[])X.Clone();s.Y=(int[])Y.Clone();s.Routes=(bool[])Routes.Clone();
            s.Deliveries=Deliveries.Select(d=>d.Clone()).ToList();s.ReceiptIds=new HashSet<string>(ReceiptIds);
            s.WaterDays=new HashSet<int>(WaterDays);s.ScreenDays=new HashSet<int>(ScreenDays);
            s.Penguins=new Dictionary<string,int>(Penguins);return s;
        }
    }
    internal sealed class Delivery
    {
        internal int Route, FromX, FromY, ToX, ToY, PaidGas;
        internal long DepartAt, ArriveAt;
        internal Delivery Clone()=>(Delivery)MemberwiseClone();
    }
}
