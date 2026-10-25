using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Factory;
using UnityEngine;

namespace ProjectPenguin.FactoryGame.Editor
{
    public static class FactoryBenchmark
    {
        [Serializable] private sealed class Result
        { public int facilities, deliveries; public double simulation120SecondsMs, saveMs; public long managedBytes; }
        [Serializable] private sealed class Results
        { public string environment = "Unity Editor on macOS; CPU simulation and save only; no GPU measurement"; public List<Result> results = new List<Result>(); }

        public static void Run()
        {
            var results = new Results();
            foreach (var width in new[] { 4, 8, 16 })
            {
                var catalog = FactoryStore.LoadCatalog(); var rules = FactoryStore.LoadRules();
                var state = new FactoryState { stage = 4, points = rules.stagePoints[3], gas = 1000000, electric = 1000000, water = 1000000 };
                state.blocks.Clear();
                for (var y = 0; y < width / 2; y++) for (var x = 0; x < width / 2; x++) state.blocks.Add(new FactoryCell(x, y));
                for (var y = 0; y < width; y++) for (var x = 0; x < width; x++)
                {
                    var kind = x % 4 == 0 ? "fishery" : x % 4 == 1 ? "preparation" : x % 4 == 2 ? "warehouse" : "saltworks";
                    state.facilities.Add(new FactoryFacility { id = state.nextId++, x = x, y = y, recipe = kind, level = 4 });
                }
                state.builtCount = state.facilities.Count;
                var session = new FactorySession(catalog, rules, _ => { }, state);
                var bytes = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew();
                var result = session.AdvanceTo(120000);
                watch.Stop();
                if (!result.Success) throw new InvalidOperationException(result.Message);
                var item = new Result { facilities = state.facilities.Count, deliveries = session.Snapshot.cargo.Count,
                    simulation120SecondsMs = watch.Elapsed.TotalMilliseconds, managedBytes = GC.GetTotalMemory(false) - bytes };
                var dir = Path.Combine(Path.GetTempPath(), "factory-benchmark-" + width);
                var store = new FactoryStore(dir, catalog, rules);
                watch.Restart(); store.Save(session.Snapshot); watch.Stop(); item.saveMs = watch.Elapsed.TotalMilliseconds;
                results.results.Add(item);
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "tkb-factory-benchmark.json"), JsonUtility.ToJson(results, true));
            }
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "tkb-factory-benchmark.json"), JsonUtility.ToJson(results, true));
        }
    }
}
