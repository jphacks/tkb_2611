using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    [Serializable] public sealed class VillagePrefabEntry
    {
        [SerializeField] private FacilityKind _kind;
        [SerializeField] private GameObject _prefab;
        public FacilityKind Kind=>_kind;
        public GameObject Prefab=>_prefab;
    }
    [Serializable] public sealed class VillageItemPrefabEntry
    {
        [SerializeField] private Item _item;
        [SerializeField] private GameObject _prefab;
        public Item Item=>_item;
        public GameObject Prefab=>_prefab;
    }
    /// <summary>つながりの部品の見え方。出入口は「つながっていない / つながっている / 選べる」の 3 つ。</summary>
    public enum VillagePortState { Free, Connected, Selectable }
    /// <summary>配送の道と出入口に置く 3D の部品 (Assets/3Dmodel/Link_*)。</summary>
    [Serializable] public sealed class VillageLinkPrefabs
    {
        [SerializeField] private GameObject _arrow,_arrowBlocked;
        [SerializeField] private GameObject _inputFree,_inputConnected,_inputSelectable;
        [SerializeField] private GameObject _outputFree,_outputConnected,_outputSelectable;
        public GameObject Arrow(bool blocked)=>blocked?_arrowBlocked:_arrow;
        public GameObject Input(VillagePortState s)=>s==VillagePortState.Free?_inputFree:s==VillagePortState.Connected?_inputConnected:_inputSelectable;
        public GameObject Output(VillagePortState s)=>s==VillagePortState.Free?_outputFree:s==VillagePortState.Connected?_outputConnected:_outputSelectable;
    }
    [Serializable] public sealed class VillagePrefabCatalog
    {
        [SerializeField] private VillagePrefabEntry[] _entries;
        [SerializeField] private VillageItemPrefabEntry[] _items;
        [SerializeField] private VillageLinkPrefabs _links=new VillageLinkPrefabs();
        public GameObject Get(FacilityKind kind)=>_entries?.FirstOrDefault(e=>e.Kind==kind)?.Prefab;
        /// <summary>品物のモデル (Assets/3Dmodel/Item_*)。荷物と、できあがりの演出に使う。</summary>
        public GameObject ItemModel(Item item)=>_items?.FirstOrDefault(e=>e.Item==item)?.Prefab;
        public VillageLinkPrefabs Links=>_links??(_links=new VillageLinkPrefabs());
    }
    public static class ProductionWorldNames
    {
        public const string WorldCanvas="VillageWorldCanvas",Grid="VillageGrid",SubtleGrid="VillageSubtleGrid",TacticalFrame="VillageTacticalFrame",Selection="VillageSelection",Preview="VillagePreview",Port="VillagePort",Facility="VillageFacility",Footprint="VillageFootprint",Zone="VillageZone",Synergy="VillageSynergy",Route="VillageRoute",Cargo="VillageCargo",Ghost="VillageGhost",
            Links="VillageLinks",LinkArrow="VillageLinkArrow",LinkPort="VillageLinkPort",CargoModel="VillageCargoModel",Pop="VillagePop",
            Guide="VillageGuide",Reach="VillageReach",GuideLink="VillageGuideLink",HintGhost="VillageHintGhost",HintPad="VillageHintPad";
    }
}
