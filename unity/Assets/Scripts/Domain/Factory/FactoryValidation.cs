using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Encounter;

namespace ProjectPenguin.Domain.Factory
{
    public sealed partial class FactorySession
    {
        public void Validate(FactoryState s)
        {
            if (s == null || s.version != FactoryState.CurrentVersion || s.catalogVersion != _catalog.version
                || s.atMs < 0 || s.atMs % _rules.tickMs != 0 || s.requestedMs < s.atMs || s.requestedMs - s.atMs >= _rules.tickMs
                || s.gas < 0 || s.electric < 0 || s.water < 0 || s.points < 0 || s.builtCount < 0
                || s.stage < 1 || s.stage > 4 || s.nextId < 1 || s.stepDay < -1 || s.maxSteps < 0 || s.maxSteps > 172800
                || s.waterDay < -1 || s.waterMl < 0 || s.waterMl > _rules.waterDailyMaxMl
                || s.encounterDay < -1 || s.dailyBirds < 0 || s.dailyBirds > _catalog.dailyBirds || s.pityMisses < 0
                || s.dispatchCursor < 0 || s.blocks == null || s.facilities == null || s.cargo == null
                || s.penguins == null || s.carries == null || s.receipts == null || s.rewardedPairs == null || s.peers == null)
                throw new ArgumentException("Invalid factory snapshot");
            if (s.blocks.Count == 0 || s.blocks.Count > _rules.maxBlocks || s.blocks.Any(b => b == null || Math.Abs((long)b.x) > 1000 || Math.Abs((long)b.y) > 1000)
                || s.blocks.Select(b => (b.x, b.y)).Distinct().Count() != s.blocks.Count
                || !s.blocks.Any(b => b.x == 0 && b.y == 0)
                || s.facilities.Any(f => f == null || f.id <= 0 || f.id >= s.nextId || f.stock == null)
                || s.facilities.Select(f => f.id).Distinct().Count() != s.facilities.Count
                || s.facilities.Where(f => !f.upper).Select(f => (f.x, f.y)).Distinct().Count() != s.facilities.Count(f => !f.upper)
                || s.builtCount < s.facilities.Count(f => !f.upper))
                throw new ArgumentException("Invalid factory layout");
            var visited = new HashSet<(int, int)> { (0, 0) };
            while (true)
            {
                var added = s.blocks.Where(b => !visited.Contains((b.x, b.y)) && visited.Any(v => Math.Abs((long)v.Item1 - b.x) + Math.Abs((long)v.Item2 - b.y) == 1)).ToArray();
                if (added.Length == 0) break;
                foreach (var b in added) visited.Add((b.x, b.y));
            }
            if (visited.Count != s.blocks.Count) throw new ArgumentException("Disconnected land");
            ValidateAmounts(s.penguins, id => _catalog.Penguin(id) != null);
            ValidateAmounts(s.carries, id => FactoryCatalog.IsEnergy(id) || _catalog.Resource(id) != null);
            if (s.carries.Any(a => a.amount >= 10000)) throw new ArgumentException("Invalid fractional carry");
            if (s.receipts.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 180) || s.receipts.Distinct().Count() != s.receipts.Count
                || s.rewardedPairs.Any(id => id == null || id.Length != 16 || id.Any(c => !Uri.IsHexDigit(c)))
                || s.rewardedPairs.Distinct().Count() != s.rewardedPairs.Count || s.rewardedPairs.Count != s.dailyBirds
                || s.encounterDay >= 0 && !EncounterToken.IsValid(s.selfToken)
                || s.peers.Count > EncounterLedger.MaxPeersPerDay || s.peers.Any(p => p == null || !EncounterToken.IsValid(p.token)
                    || p.token == s.selfToken || p.retryAt < 0 || p.matched && p.dropped)
                || s.peers.Select(p => p.token).Distinct().Count() != s.peers.Count)
                throw new ArgumentException("Invalid receipt ledger");
            foreach (var pair in s.rewardedPairs)
                if (!s.peers.Any(p => p.matched && EncounterToken.PairId(s.selfToken, p.token) == pair))
                    throw new ArgumentException("Reward without confirmed peer");
            foreach (var n in s.facilities)
            {
                var recipe = _catalog.Recipe(n.recipe);
                if (n.level < 1 || n.level > _catalog.growth.Length || n.effort < 0 || n.effort > _rules.maxEffort
                    || recipe == null && n.recipe != "warehouse" || recipe != null && recipe.tier > s.stage
                    || !s.blocks.Any(b => b.x == (n.upper ? n.x : Block(n.x)) && b.y == (n.upper ? n.y : Block(n.y)))
                    || n.processEnd < 0 || n.processYield < 0 || n.processEnd > 0 && (recipe == null || n.processEnd <= s.atMs
                        || n.processYield <= 0 || n.processOutput != recipe.output || n.processEnd - s.atMs > recipe.durationMs)
                    || n.processEnd == 0 && (n.processYield != 0 || !string.IsNullOrEmpty(n.processOutput)))
                    throw new ArgumentException("Invalid facility");
                ValidateAmounts(n.stock, id => _catalog.Resource(id) != null);
                foreach (var resource in _catalog.resources)
                    if ((decimal)FactoryState.Amount(n.stock, resource.id) + Reserved(s, n, resource.id) > Capacity(s, n, resource.id))
                        throw new ArgumentException("Capacity exceeded");
            }
            foreach (var cargo in s.cargo)
            {
                if (cargo == null || cargo.amount <= 0 || _catalog.Resource(cargo.resource) == null
                    || cargo.arrivesAt <= s.atMs || cargo.path == null || cargo.path.Count < 2
                    || cargo.path[0] != cargo.from || cargo.path[cargo.path.Count - 1] != cargo.to
                    || cargo.path.Distinct().Count() != cargo.path.Count
                    || cargo.path.Any(id => !s.facilities.Any(f => f.id == id))) throw new ArgumentException("Invalid cargo");
                for (var i = 1; i < cargo.path.Count; i++)
                    if (!Adjacent(s.facilities.Single(f => f.id == cargo.path[i - 1]), s.facilities.Single(f => f.id == cargo.path[i])))
                        throw new ArgumentException("Cargo path changed");
            }
        }

        private static void ValidateAmounts(List<FactoryAmount> entries, Func<string, bool> allowed)
        {
            if (entries.Any(a => a == null || a.amount < 0 || !allowed(a.id)) || entries.Select(a => a.id).Distinct().Count() != entries.Count)
                throw new ArgumentException("Invalid inventory");
        }
    }
}
