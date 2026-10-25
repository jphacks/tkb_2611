using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.Factory
{
    // Values come from progression-model.json. This is an adjustable proposal, not final balancing.
    [Serializable]
    public sealed class FactoryCatalog
    {
        public int version;
        public string status;
        public long[] growth;
        public long storage, relay, warehouseRelay;
        public int dailyBirds, pityAfter;
        public FactoryResource[] resources;
        public FactoryRecipe[] recipes;
        public FactoryPenguin[] penguins;

        // Fixed-block proposal compositions are shared by simulation and build guidance.
        public static readonly FactoryFormation[] Formations =
        {
            new FactoryFormation("gift", "fishery", "saltworks", "drying", "warehouse"),
            new FactoryFormation("research", "fishery", "preparation", "kelp_farm", "warehouse"),
            new FactoryFormation("oden", "kamaboko", "chikuwa", "kelp_farm", "warehouse"),
            new FactoryFormation("warehouse", "warehouse", "warehouse", "warehouse", "warehouse")
        };

        public FactoryRecipe Recipe(string id) => recipes.FirstOrDefault(r => r.id == id);
        public FactoryResource Resource(string id) => resources.FirstOrDefault(r => r.id == id);
        public FactoryPenguin Penguin(string id) => penguins.FirstOrDefault(p => p.id == id);
        public long Growth(int level) => growth[level - 1];

        public void Validate()
        {
            if (version != 2 || status != "proposal" || growth == null || growth.Length != 5 || growth.Any(n => n <= 0)
                || storage <= 0 || relay <= 0 || warehouseRelay <= 0 || dailyBirds <= 0 || pityAfter < 0
                || resources == null || recipes == null || penguins == null)
                throw new ArgumentException("Invalid factory catalog");
            if (resources.Length != 10 || resources.Select(r => r.id).Distinct().Count() != resources.Length
                || recipes.Length != 10 || recipes.Select(r => r.id).Distinct().Count() != recipes.Length
                || penguins.Length != 19 || penguins.Select(p => p.id).Distinct().Count() != penguins.Length)
                throw new ArgumentException("Duplicate or missing factory catalog entries");
            foreach (var r in resources)
                if (string.IsNullOrWhiteSpace(r.id) || r.tier < 1 || r.tier > 4 || r.points <= 0)
                    throw new ArgumentException("Invalid resource");
            foreach (var r in recipes)
                if (string.IsNullOrWhiteSpace(r.id) || Resource(r.output) == null || Resource(r.output).tier != r.tier
                    || r.durationMs <= 0 || r.yieldAmount <= 0 || r.inputs == null
                    || r.inputs.Select(i => i.id).Distinct().Count() != r.inputs.Length
                    || r.inputs.Any(i => Resource(i.id) == null || Resource(i.id).tier >= r.tier || i.amount <= 0)
                    || r.energy == null || r.energy.Length != 3 || r.energy.Any(n => n < 0) || r.energy.All(n => n == 0))
                    throw new ArgumentException("Invalid recipe");
            foreach (var p in penguins)
                if (string.IsNullOrWhiteSpace(p.id) || (Resource(p.target) == null && !IsEnergy(p.target))
                    || p.percent <= 0 || p.weight <= 0 || p.stage < 1 || p.stage > 4)
                    throw new ArgumentException("Invalid penguin");
        }

        public static bool IsEnergy(string id) => id == "gas" || id == "electric" || id == "water";
    }

    public sealed class FactoryFormation
    {
        public string Recipe { get; }
        private readonly string[] _members;
        public FactoryFormation(string recipe, params string[] members) { Recipe = recipe; _members = members; }
        public string[] Missing(IEnumerable<string> present)
        {
            var available = present.ToList(); var missing = new List<string>();
            foreach (var member in _members)
                if (!available.Remove(member)) missing.Add(member);
            return missing.ToArray();
        }
    }

    [Serializable] public sealed class FactoryResource { public string id, label; public int tier; public long points; }
    [Serializable] public sealed class FactoryAmount
    {
        public string id;
        public long amount;
        public FactoryAmount() { }
        public FactoryAmount(string id, long amount) { this.id = id; this.amount = amount; }
        public FactoryAmount Clone() => new FactoryAmount(id, amount);
    }
    [Serializable] public sealed class FactoryRecipe
    {
        public string id, label, output;
        public int tier;
        public long durationMs, yieldAmount;
        public FactoryAmount[] inputs;
        public long[] energy;
    }
    [Serializable] public sealed class FactoryPenguin
    {
        public string id, label, target;
        public bool multiplicative;
        public int percent, stage, weight;
    }

    [Serializable]
    public sealed class FactoryRules
    {
        // Prototype choices for the previously undecided rules; load from editable JSON.
        public long startingGas, startingElectric, startingWater;
        public long buildBase = 2, buildIncrement = 1, expandWater = 3;
        public int maxBlocks = 64, maxEffort = 3, comboRadius = 1, comboPercent = 25;
        public int effortPercent = 100, effortGas = 1;
        public int largeWarehouseMultiplier = 2;
        public int zoomUnlockStage = 2;
        public long[] stagePoints = { 0, 1000, 10000, 100000 };
        public int stepsPerGas = 500, screenMinutesPerElectric = 120;
        public long waterPerRecord = 3;
        public long tickMs = 1000, maxAdvanceMs = 120000;
        public bool offlineProduction;

        public void Validate()
        {
            if (startingGas < 0 || startingElectric < 0 || startingWater < 0 || buildBase <= 0 || buildIncrement < 0
                || expandWater < 0 || maxBlocks < 1 || maxBlocks > 4096 || maxEffort < 0 || maxEffort > 20
                || comboRadius < 0 || comboRadius > 8 || comboPercent < 0 || effortPercent < 0 || effortGas < 0 || largeWarehouseMultiplier < 1 || largeWarehouseMultiplier > 10
                || zoomUnlockStage < 1 || zoomUnlockStage > 4
                || stagePoints == null || stagePoints.Length != 4 || stagePoints[0] != 0
                || stagePoints.Where((n, i) => n < 0 || i > 0 && n <= stagePoints[i - 1]).Any()
                || stepsPerGas <= 0 || screenMinutesPerElectric <= 0 || waterPerRecord <= 0
                || tickMs != 1000 || maxAdvanceMs < tickMs || maxAdvanceMs > 120000 || offlineProduction)
                throw new ArgumentException("Invalid prototype rules; offline production is not enabled");
        }
    }
}
