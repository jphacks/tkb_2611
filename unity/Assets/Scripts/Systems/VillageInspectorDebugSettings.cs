using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectPenguin.Systems
{
    [Serializable] public sealed class VillageInspectorStock
    {
        [SerializeField,FormerlySerializedAs("Item")] private Item _item;
        [SerializeField,FormerlySerializedAs("Count")] private int _count;
        public Item Item {get=>_item;set=>_item=value;}
        public int Count {get=>_count;set=>_count=value;}
        public VillageInspectorStock(Item item,int count){Item=item;Count=count;}
    }
    [Serializable] public sealed class VillageInspectorFacility
    {
        [SerializeField,FormerlySerializedAs("Kind")] private FacilityKind _kind;
        [SerializeField,FormerlySerializedAs("Cell")] private Vector2Int _cell;
        [SerializeField,FormerlySerializedAs("Stock")] private VillageInspectorStock[] _stock=Array.Empty<VillageInspectorStock>();
        public FacilityKind Kind {get=>_kind;set=>_kind=value;}
        public Vector2Int Cell {get=>_cell;set=>_cell=value;}
        public VillageInspectorStock[] Stock {get=>_stock;set=>_stock=value;}
        public VillageInspectorFacility(FacilityKind kind,int x,int y){Kind=kind;Cell=new Vector2Int(x,y);}
    }
    public enum VillageInspectorPreset { Chikuwa, Kamaboko, DriedFish }
    [Serializable] public sealed class VillageInspectorDebugSettings
    {
        [SerializeField,FormerlySerializedAs("Enabled")] private bool _enabled;
        [SerializeField,FormerlySerializedAs("Gas")] private double _gas=10000;
        [SerializeField,FormerlySerializedAs("Electricity")] private double _electricity=100;
        [SerializeField,FormerlySerializedAs("Water")] private double _water=100;
        [SerializeField,FormerlySerializedAs("RecipeSpeed")] private double _recipeSpeed=.01;
        [SerializeField,FormerlySerializedAs("TimeScale")] private double _timeScale=100;
        [SerializeField,FormerlySerializedAs("DeliveryMs")] private int _deliveryMs=1500;
        [SerializeField,FormerlySerializedAs("Unlimited")] private bool _unlimited;
        [SerializeField,FormerlySerializedAs("Facilities")] private VillageInspectorFacility[] _facilities={
            new VillageInspectorFacility(FacilityKind.Fishery,8,10),
            new VillageInspectorFacility(FacilityKind.Fishery,10,10),
            new VillageInspectorFacility(FacilityKind.Fishery,12,10),
            new VillageInspectorFacility(FacilityKind.Warehouse,10,13)};
        public bool Enabled {get=>_enabled;set=>_enabled=value;}
        public double Gas {get=>_gas;set=>_gas=value;}
        public double Electricity {get=>_electricity;set=>_electricity=value;}
        public double Water {get=>_water;set=>_water=value;}
        public double RecipeSpeed {get=>_recipeSpeed;set=>_recipeSpeed=value;}
        public double TimeScale {get=>_timeScale;set=>_timeScale=value;}
        public int DeliveryMs {get=>_deliveryMs;set=>_deliveryMs=value;}
        public bool Unlimited {get=>_unlimited;set=>_unlimited=value;}
        public VillageInspectorFacility[] Facilities {get=>_facilities;set=>_facilities=value;}

        public void ApplyPreset(VillageInspectorPreset preset)
        {
            if(!Enum.IsDefined(typeof(VillageInspectorPreset),preset))throw new ArgumentOutOfRangeException(nameof(preset));
            Facilities=preset==VillageInspectorPreset.DriedFish?new[]{
                new VillageInspectorFacility(FacilityKind.Saltworks,8,10),new VillageInspectorFacility(FacilityKind.Fishery,10,10),
                new VillageInspectorFacility(FacilityKind.Fishery,8,12),new VillageInspectorFacility(FacilityKind.Warehouse,7,9)}:new[]{
                new VillageInspectorFacility(FacilityKind.Fishery,8,10),
                new VillageInspectorFacility(preset==VillageInspectorPreset.Kamaboko?FacilityKind.Saltworks:FacilityKind.Fishery,10,10),
                new VillageInspectorFacility(FacilityKind.Fishery,12,10),new VillageInspectorFacility(FacilityKind.Warehouse,10,13)};
        }
        /// <summary>Returns a production-profile-compatible initial snapshot; development options are applied after BeginDevelopment.</summary>
        public VillageGame CreateInitial()
        {
            if(!VillageGame.Finite(Gas)||!VillageGame.Finite(Electricity)||!VillageGame.Finite(Water)||Gas<0||Electricity<0||Water<0||Gas>int.MaxValue||Electricity>int.MaxValue||Water>int.MaxValue)
                throw new InvalidOperationException("ガス・電気・水は有限の0〜2147483647を指定してください");
            if(!VillageGame.Finite(RecipeSpeed)||RecipeSpeed<.01||RecipeSpeed>100)throw new InvalidOperationException("通常生産の速度は有限の0.01〜100を指定してください");
            if(DeliveryMs<100||DeliveryMs>3600000)throw new InvalidOperationException("配送時間は100〜3600000msを指定してください");
            if(!VillageGame.Finite(TimeScale)||TimeScale<0||TimeScale>100)throw new InvalidOperationException("時間倍率は有限の0〜100を指定してください");
            var game=new VillageGame(true);
            game.SetResources(Gas,Electricity,Water);
            // Validate all development options before any published village can be reset.
            game.Configure(Unlimited,RecipeSpeed,DeliveryMs);
            game.Configure(true,1,1500);
            if(Facilities==null)throw new InvalidOperationException("デバッグ施設一覧が未設定です");
            foreach(var f in Facilities)
            {
                if(f==null)throw new InvalidOperationException("施設一覧に空の要素があります");
                var result=game.Place(f.Kind,f.Cell.x,f.Cell.y);
                if(!result.Success)throw new InvalidOperationException($"施設 {f.Kind} ({f.Cell.x}, {f.Cell.y}): {result.Reason}");
            }
            // Inject after all facilities, so cooling capacity is independent of list order.
            for(var i=0;i<Facilities.Length;i++)
            {
                var stock=Facilities[i].Stock;
                if(stock==null)throw new InvalidOperationException($"施設 {i+1} の在庫一覧が未設定です");
                var seen=new HashSet<Item>();
                foreach(var s in stock)
                {
                    if(s==null||!Enum.IsDefined(typeof(Item),s.Item)||s.Count<0||!seen.Add(s.Item))throw new InvalidOperationException($"施設 {i+1} の在庫品目・個数・重複を確認してください");
                    if(s.Count==0)continue;
                    var result=game.Inject(i+1,s.Item,s.Count);
                    if(!result.Success)throw new InvalidOperationException($"施設 {i+1} の在庫: {result.Reason}");
                }
            }
            game.Configure(false,1,1500);
            return game;
        }
    }
}
