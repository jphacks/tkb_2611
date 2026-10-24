using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    // Save DTOs are detached copies. Never hand these to presentation as live state.
    [Serializable] public sealed class VillageState
    {
        public long At, Requested, NextCargo = 1, Produced;
        public int NextId = 1, DeliveryMs = 1500;
        public double Speed = 1, Gas, Electricity, Water, Spent;
        public bool Unlimited;
        public List<VillageNodeState> Nodes = new List<VillageNodeState>();
        public List<VillageCargoState> Cargo = new List<VillageCargoState>();
        public List<VillageHistoryState> History = new List<VillageHistoryState>();
        public HealthRecordLedger Ledger = new HealthRecordLedger();
    }
    [Serializable] public sealed class VillageNodeState
    {
        public int Id, X, Y;
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
