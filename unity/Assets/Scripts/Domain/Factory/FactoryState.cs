using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.Factory
{
    [Serializable]
    public sealed class FactoryState
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion, catalogVersion = 2, nextId = 1, stage = 1;
        public long atMs, requestedMs, gas, electric, water, points, builtCount;
        public int stepDay = -1, maxSteps, waterDay = -1, waterMl, encounterDay = -1, dailyBirds, pityMisses;
        public string selfToken;
        public List<FactoryCell> blocks = new List<FactoryCell> { new FactoryCell(0, 0) };
        public List<FactoryFacility> facilities = new List<FactoryFacility>();
        public List<FactoryCargo> cargo = new List<FactoryCargo>();
        public List<FactoryAmount> penguins = new List<FactoryAmount>(), carries = new List<FactoryAmount>();
        public List<string> receipts = new List<string>(), rewardedPairs = new List<string>();
        public List<FactoryPeer> peers = new List<FactoryPeer>();
        public int dispatchCursor;

        public FactoryState Clone()
        {
            var s = (FactoryState)MemberwiseClone();
            s.blocks = blocks.Select(b => new FactoryCell(b.x, b.y)).ToList();
            s.facilities = facilities.Select(n => n.Clone()).ToList();
            s.cargo = cargo.Select(c => c.Clone()).ToList();
            s.penguins = penguins.Select(a => a.Clone()).ToList();
            s.carries = carries.Select(a => a.Clone()).ToList();
            s.receipts = new List<string>(receipts); s.rewardedPairs = new List<string>(rewardedPairs);
            s.peers = peers.Select(p => p.Clone()).ToList();
            return s;
        }

        public long Energy(string id) => id == "gas" ? gas : id == "electric" ? electric : id == "water" ? water : 0;
        public static long Amount(List<FactoryAmount> values, string id) => values.FirstOrDefault(a => a.id == id)?.amount ?? 0;
        internal static void Add(List<FactoryAmount> values, string id, long delta)
        {
            var entry = values.FirstOrDefault(a => a.id == id);
            var next = checked((entry?.amount ?? 0) + delta);
            if (next < 0) throw new InvalidOperationException("Insufficient stock");
            if (entry == null) values.Add(new FactoryAmount(id, next)); else entry.amount = next;
        }
    }

    [Serializable] public sealed class FactoryCell
    {
        public int x, y;
        public FactoryCell() { }
        public FactoryCell(int x, int y) { this.x = x; this.y = y; }
    }
    [Serializable] public sealed class FactoryFacility
    {
        public int id, x, y, level = 1, effort;
        public string recipe;
        public bool upper, enabled = true, retired;
        public long processEnd, processYield;
        public string processOutput;
        public List<FactoryAmount> stock = new List<FactoryAmount>();
        public FactoryFacility Clone()
        { var f = (FactoryFacility)MemberwiseClone(); f.stock = stock.Select(a => a.Clone()).ToList(); return f; }
    }
    [Serializable] public sealed class FactoryCargo
    {
        public int from, to;
        public string resource;
        public long amount, arrivesAt;
        public List<int> path = new List<int>();
        public FactoryCargo Clone()
        { var c = (FactoryCargo)MemberwiseClone(); c.path = new List<int>(path); return c; }
    }
    [Serializable] public sealed class FactoryPeer
    {
        public string token;
        public bool matched, dropped;
        public long retryAt;
        public FactoryPeer Clone() => (FactoryPeer)MemberwiseClone();
    }
    public sealed class FactoryResult
    {
        public bool Success { get; }
        public string Message { get; }
        public long Amount { get; }
        public int FacilityId { get; }
        public FactoryResult(bool success, string message, long amount = 0, int facilityId = 0)
        { Success = success; Message = message; Amount = amount; FacilityId = facilityId; }
    }
}
