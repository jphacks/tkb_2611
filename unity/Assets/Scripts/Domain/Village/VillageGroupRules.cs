using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>Geometry is independent from the production clock and inventory policy.</summary>
    public readonly struct VillageGroupCell
    {
        public readonly int X,Y;
        public readonly FacilityKind Kind;
        public VillageGroupCell(int x,int y,FacilityKind kind){X=x;Y=y;Kind=kind;}
    }
    public sealed class VillageGroupDefinition
    {
        public string Name {get;}
        public Item Product {get;}
        public int CoreIndex {get;}
        public IReadOnlyList<IReadOnlyList<VillageGroupCell>> Orientations {get;}
        internal VillageGroupDefinition(string name,Item product,int coreIndex,params VillageGroupCell[][] orientations)
        {
            Name=name;Product=product;CoreIndex=coreIndex;
            var copy=new List<IReadOnlyList<VillageGroupCell>>();
            foreach(var cells in orientations)copy.Add(Array.AsReadOnly((VillageGroupCell[])cells.Clone()));
            Orientations=copy.AsReadOnly();
        }
    }
    public static class VillageGroupRules
    {
        public const int AnchorSpacing=2;
        public const long IntervalMs=600000;
        public const int ProgressPerItem=2;
        public const int ProductCap=6;
        public const double ItemsPerMinute=60000.0/IntervalMs/ProgressPerItem;
        static VillageGroupCell F(int x,int y)=>new VillageGroupCell(x,y,FacilityKind.Fishery);
        static VillageGroupCell S(int x,int y)=>new VillageGroupCell(x,y,FacilityKind.Saltworks);
        public static readonly IReadOnlyList<VillageGroupDefinition> Definitions=Array.AsReadOnly(new[]{
            new VillageGroupDefinition("ちくわ3連ライン",Item.Chikuwa,1,new[]{F(0,0),F(1,0),F(2,0)},new[]{F(0,0),F(0,1),F(0,2)}),
            new VillageGroupDefinition("かまぼこ黄金ライン",Item.Kamaboko,1,new[]{F(0,0),S(1,0),F(2,0)},new[]{F(0,0),S(0,1),F(0,2)}),
            // Keep salt in the first slot in every L orientation to identify the core explicitly.
            new VillageGroupDefinition("干物L字ライン",Item.DriedFish,0,new[]{S(0,0),F(1,0),F(0,1)},new[]{S(1,0),F(0,0),F(1,1)},new[]{S(1,1),F(1,0),F(0,1)},new[]{S(0,1),F(0,0),F(1,1)})
        });
    }
}
