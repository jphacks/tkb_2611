using System;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine;

namespace ProjectPenguin.Systems
{
    [Serializable] public sealed class VillageInitialFacility
    {
        [SerializeField] private FacilityKind _kind;
        [SerializeField] private Vector2Int _cell;
        public VillageInitialFacility(FacilityKind kind,int x,int y){_kind=kind;_cell=new Vector2Int(x,y);}
        public FacilityKind Kind=>_kind;
        public Vector2Int Cell=>_cell;
    }
    [DisallowMultipleComponent]
    public sealed class ProductionVillageControl : MonoBehaviour
    {
        [SerializeField] private double _initialGas=100,_initialElectricity=100,_initialWater=100;
        [SerializeField] private VillageInitialFacility[] _initialFacilities={new VillageInitialFacility(FacilityKind.Fishery,8,10),new VillageInitialFacility(FacilityKind.Preparation,11,10),new VillageInitialFacility(FacilityKind.Warehouse,14,10)};
        [SerializeField] private VillageInspectorDebugSettings _inspectorDebugSettings=new VillageInspectorDebugSettings();
        public VillageInspectorDebugSettings InspectorDebugSettings=>_inspectorDebugSettings;
        public VillageGame CreateInspectorDebugInitial()=>_inspectorDebugSettings.CreateInitial();
        public ProductionVillageSessionCore Core=>GetComponent<ProductionVillageSession>()?.Core;
        public VillageGame CreateInitial()
        {
            var g=new VillageGame(false);g.SetResources(_initialGas,_initialElectricity,_initialWater);
            if(_initialFacilities==null)throw new InvalidOperationException("初期施設が未設定です");
            foreach(var f in _initialFacilities){if(f==null)throw new InvalidOperationException("初期施設が不正です");var r=g.Place(f.Kind,f.Cell.x,f.Cell.y);if(!r.Success)throw new InvalidOperationException(r.Reason);}
            return g;
        }
    }
}
