namespace ProjectPenguin.Domain.LogisticsPrototype
{
    public static class EvolutionFactory
    {
        /// <summary>Three unfinished shapes on a 24x24 plot. The displaced member of each is deliberately obvious.</summary>
        public static EvolutionFacade CreateDemonstration()
        {
            var f=new EvolutionFacade();
            f.Place(WorkshopKind.Fishery,2,16);f.Place(WorkshopKind.Fishery,4,16);f.Place(WorkshopKind.Fishery,10,16);f.Place(WorkshopKind.Warehouse,4,20);
            f.Place(WorkshopKind.Fishery,14,16);f.Place(WorkshopKind.Saltworks,16,16);f.Place(WorkshopKind.Fishery,22,16);f.Place(WorkshopKind.Warehouse,16,20);
            f.Place(WorkshopKind.Saltworks,4,4);f.Place(WorkshopKind.Fishery,6,4);f.Place(WorkshopKind.Fishery,4,10);f.Place(WorkshopKind.Warehouse,9,4);
            foreach(var id in new[]{1,2,3}) f.Connect(id,4);
            foreach(var id in new[]{5,6,7}) f.Connect(id,8);
            foreach(var id in new[]{9,10,11}) f.Connect(id,12);
            return f;
        }
    }
}
