using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Factory;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class FactoryLifestyleTests
    {
        [Serializable] private sealed class Sample
        {
            public string scenario, plan;
            public int day, steps, screenMinutes, facilities, effort, stage;
            public long fishDelivered, cumulativePoints, gasLeft, electricLeft;
        }
        [Serializable] private sealed class Report
        {
            public string scope = "Synthetic daily inputs, default proposal rules, fishery only, 61 active seconds per day; no claim of global optimality or user enjoyment";
            public List<Sample> samples = new List<Sample>();
        }

        [Test]
        public void SupplyRatioAndDailyChangesFavorDifferentInvestmentsWithDefaultRules()
        {
            var report = new Report();
            var gasEffort = OneDay(report, "gas-rich", "effort", 20000, 1200, false, 3);
            var gasBuild = OneDay(report, "gas-rich", "expansion", 20000, 1200, true, 0);
            var electricEffort = OneDay(report, "electric-rich", "effort", 2000, 0, false, 3);
            var electricBuild = OneDay(report, "electric-rich", "expansion", 2000, 0, true, 0);
            Assert.That(gasEffort.fishDelivered, Is.GreaterThan(gasBuild.fishDelivered));
            Assert.That(electricBuild.fishDelivered, Is.GreaterThan(electricEffort.fishDelivered));
            Assert.That(gasBuild.facilities, Is.EqualTo(1));
            Assert.That(electricBuild.facilities, Is.EqualTo(3));

            var adaptive = RunDays(report, "adaptive", new[] { 0, 3, 0 });
            var fixedLow = RunDays(report, "fixed-effort-zero", new[] { 0, 0, 0 });
            Assert.That(adaptive[0].fishDelivered, Is.EqualTo(fixedLow[0].fishDelivered));
            Assert.That(adaptive[1].fishDelivered, Is.GreaterThan(fixedLow[1].fishDelivered));
            Assert.That(adaptive.Select(s => s.facilities), Is.EqualTo(new[] { 3, 4, 4 }), "Saved electricity finances the fourth facility on day two");
            Assert.That(adaptive[2].cumulativePoints, Is.GreaterThan(adaptive[1].cumulativePoints));
            Assert.That(fixedLow[2].fishDelivered, Is.GreaterThan(adaptive[2].fishDelivered), "Conserved gas carries forward; yesterday's faster output is not a universal best strategy");
            Assert.That(fixedLow[2].cumulativePoints, Is.GreaterThan(adaptive[2].cumulativePoints));
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "tkb-factory-lifestyle.json"), JsonUtility.ToJson(report, true));
        }

        private static Sample OneDay(Report report, string scenario, string plan, int steps, int minutes, bool expand, int effort)
        {
            var directory = Path.Combine(Path.GetTempPath(), "factory-lifestyle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FactoryStore(directory, FactoryStore.LoadCatalog(), FactoryStore.LoadRules());
                var session = store.Open(out _);
                var sample = Day(session, scenario, plan, 297, steps, minutes, expand, effort);
                report.samples.Add(sample); return sample;
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static Sample[] RunDays(Report report, string plan, int[] efforts)
        {
            var directory = Path.Combine(Path.GetTempPath(), "factory-lifestyle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FactoryStore(directory, FactoryStore.LoadCatalog(), FactoryStore.LoadRules());
                var samples = new List<Sample>();
                for (var i = 0; i < efforts.Length; i++)
                {
                    // Reopen the actual versioned save each morning; no fresh grant or offline production.
                    var session = store.Open(out _);
                    var sample = Day(session, "electric-gas-electric", plan, 297 + i,
                        i == 1 ? 20000 : 2000, i == 1 ? 1200 : 0, true, efforts[i]);
                    samples.Add(sample); report.samples.Add(sample);
                    var reopened = store.Open(out _);
                    Assert.That(JsonUtility.ToJson(reopened.Snapshot), Is.EqualTo(JsonUtility.ToJson(session.Snapshot)));
                }
                return samples.ToArray();
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static Sample Day(FactorySession session, string scenario, string plan, int day, int steps, int minutes, bool expand, int effort)
        {
            Success(session.AcceptSteps(day, steps, day));
            Success(session.AcceptScreen("synthetic-screen-" + day, day - 1, minutes, day, true));
            foreach (var cell in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                var state = session.Snapshot;
                if (state.facilities.Any(n => n.x == cell.Item1 && n.y == cell.Item2)) continue;
                if ((!expand && state.facilities.Count > 0) || state.electric < session.NextBuildCost) break;
                Success(session.Build("fishery", cell.Item1, cell.Item2));
            }
            foreach (var node in session.Snapshot.facilities)
            { Success(session.SetEffort(node.id, effort)); Success(session.SetEnabled(node.id, true)); }
            Success(session.AdvanceTo(session.Snapshot.requestedMs + 61000));
            foreach (var node in session.Snapshot.facilities) Success(session.SetEnabled(node.id, false));
            Success(session.AdvanceTo(session.Snapshot.requestedMs + 11000));
            var fish = session.Snapshot.facilities.Sum(n => FactoryState.Amount(n.stock, "fish"));
            if (fish > 0) Success(session.Deliver("synthetic-delivery-" + day, "fish", fish));
            var final = session.Snapshot;
            return new Sample { scenario = scenario, plan = plan, day = day, steps = steps, screenMinutes = minutes,
                facilities = final.facilities.Count, effort = effort, fishDelivered = fish, cumulativePoints = final.points,
                stage = final.stage, gasLeft = final.gas, electricLeft = final.electric };
        }

        private static void Success(FactoryResult result) => Assert.That(result.Success, Is.True, result.Message);
    }
}
