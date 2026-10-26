using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    // Save DTOs are detached copies. Never hand these to presentation as live state.
    [Serializable] public sealed class VillageState
    {
        public long At, Requested, NextCargo = 1, Produced;
        public int NextId = 1, DeliveryMs = 1500;
        public int EffortSchema;
        public int ComboSchema;
        public int[] BuildCounts;
        public double Speed = 1, Gas, Electricity, Water, Spent;
        public bool Unlimited;
        /// <summary>資源無限のモード。製品用の保存には入れない (ProductionVillageSave.Validate)。</summary>
        public bool FreeResources;
        public VillageGroupProgressState GroupProgress = new VillageGroupProgressState();
        public List<VillageNodeState> Nodes = new List<VillageNodeState>();
        public List<VillageCargoState> Cargo = new List<VillageCargoState>();
        public List<VillageHistoryState> History = new List<VillageHistoryState>();
        public List<VillageTrendState> Trend = new List<VillageTrendState>();
        public HealthRecordLedger Ledger = new HealthRecordLedger();
        /// <summary>すれ違いの台帳。この項目が無い古い保存は空の台帳として読む。</summary>
        public EncounterLedgerState Encounters = new EncounterLedgerState();
    }
    [Serializable] public sealed class VillageTrendState
    {
        public long Minute;
        public double Gas;
        public int[] Made = new int[8];
        public int[] Used = new int[8];
    }
    [Serializable] public sealed class VillageGroupProgressState
    {
        public long ElapsedMs, Tick;
        public int Chikuwa, Kamaboko, DriedFish;
    }
    [Serializable] public sealed class VillageNodeState
    {
        public int Id, X, Y;
        public int Level = 1; // Legacy saves only; effort saves use EffortSchema.
        public int Effort;
        public int ProcessYield, ComboRemainder;
        public int WarehouseCapacity; // Zero keeps the legacy default.
        public FacilityKind Kind;
        public long Started, Finish;
        public int[] Stock = new int[8];
    }
    [Serializable] public sealed class VillageCargoState
    {
        public long Id, Start, End;
        public Item Item;
        public int From, To, FromX, FromY, ToX, ToY;
    }
    [Serializable] public sealed class VillageHistoryState { public long At; public double Gas; public int Made; }
}
