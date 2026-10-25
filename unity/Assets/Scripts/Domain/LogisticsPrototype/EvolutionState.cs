using System.Collections.Generic;

namespace ProjectPenguin.Domain.LogisticsPrototype
{
    internal sealed class WorkshopState
    {
        internal int Id, X, Y;
        internal WorkshopKind Kind;
        internal readonly Dictionary<WorkshopGood,int> Stock = new Dictionary<WorkshopGood,int>();
        internal int Size => Kind==WorkshopKind.Warehouse ? 1 : 2;
        internal int Held(WorkshopGood g) => Stock.TryGetValue(g,out var n)?n:0;
        internal void Add(WorkshopGood g,int n) { Stock[g]=Held(g)+n; }
    }
    internal sealed class EvolutionState
    {
        internal readonly int Size;
        internal int NextId=1;
        internal long NextCargo=1, AtMs, StepAt;
        internal readonly List<WorkshopState> Workshops=new List<WorkshopState>();
        internal readonly List<FormationView> Formations=new List<FormationView>();
        internal readonly List<RouteView> Routes=new List<RouteView>();
        internal readonly List<CargoView> Cargo=new List<CargoView>();
        internal readonly Dictionary<WorkshopGood,int> ShapeProgress=new Dictionary<WorkshopGood,int>();
        internal EvolutionState(int size) { Size=size; }
    }
}
