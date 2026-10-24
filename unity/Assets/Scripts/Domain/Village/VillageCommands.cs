namespace ProjectPenguin.Domain.Village
{
    public static class VillageCommands
    {
        public static VillageResult Accepted()=>new VillageResult{Success=true,Reason="反映しました"};
    }
}
