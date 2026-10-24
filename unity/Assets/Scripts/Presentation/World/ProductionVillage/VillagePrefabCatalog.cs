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
    [Serializable] public sealed class VillagePrefabCatalog
    {
        [SerializeField] private VillagePrefabEntry[] _entries;
        public GameObject Get(FacilityKind kind)=>_entries?.FirstOrDefault(e=>e.Kind==kind)?.Prefab;
    }
    public static class ProductionWorldNames
    {
        public const string WorldCanvas="VillageWorldCanvas",Grid="VillageGrid",Selection="VillageSelection",Preview="VillagePreview",Port="VillagePort",Facility="VillageFacility",Route="VillageRoute",Cargo="VillageCargo",Ghost="VillageGhost";
    }
}
