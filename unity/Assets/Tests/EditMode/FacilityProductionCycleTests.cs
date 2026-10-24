using System;
using System.Collections;
using NUnit.Framework;
using ProjectPenguin.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class FacilityProductionCycleTests
    {
        private static IEnumerable Timings
        {
            get
            {
                yield return Row(FacilityProductionCycle.FacilityKind.Fishery, 1, 4, 4.8f, 6, 7);
                yield return Row(FacilityProductionCycle.FacilityKind.Preparation, .8f, 2.4f, 3.5f, 4.5f, 5.5f);
                yield return Row(FacilityProductionCycle.FacilityKind.Chikuwa, 1.1f, 4.5f, 5.5f, 6.3f, 7.4f);
                yield return Row(FacilityProductionCycle.FacilityKind.Saltworks, 1.2f, 4.8f, 6, 7.3f, 8.8f);
                yield return Row(FacilityProductionCycle.FacilityKind.Drying, 1.7f, 6.4f, 7.2f, 8, 10);
                yield return Row(FacilityProductionCycle.FacilityKind.KelpFarm, .8f, 2.5f, 3.4f, 4.7f, 6);
                yield return Row(FacilityProductionCycle.FacilityKind.Oden, 1, 5.4f, 6.4f, 7.3f, 9);
                yield return Row(FacilityProductionCycle.FacilityKind.Warehouse, .7f, 1.6f, 2.2f, 3.2f, 4.5f);
                yield return Row(FacilityProductionCycle.FacilityKind.Sorter, .55f, 1.2f, 1.5f, 2.2f, 3);
                yield return Row(FacilityProductionCycle.FacilityKind.Research, .9f, 4.2f, 5, 5.8f, 7.5f);
                yield return Row(FacilityProductionCycle.FacilityKind.Dormitory, 1, 8.5f, 10, 11.4f, 12.8f);
                yield return Row(FacilityProductionCycle.FacilityKind.ElectricAmplifier, .6f, 2.1f, 2.5f, 3.2f, 4.4f);
                yield return Row(FacilityProductionCycle.FacilityKind.WaterAmplifier, 1, 3.8f, 4.7f, 5.5f, 6.8f);
                yield return Row(FacilityProductionCycle.FacilityKind.GasAmplifier, .8f, 3, 3.8f, 4.7f, 6);
                yield return Row(FacilityProductionCycle.FacilityKind.Harbor, 1, 2.5f, 3.2f, 4.6f, 6.5f);
            }
        }

        private static TestCaseData Row(FacilityProductionCycle.FacilityKind kind, float load,
            float transform, float work, float output, float end) => new TestCaseData(kind, load, transform, work, output, end);

        [TestCaseSource(nameof(Timings))]
        public void RecipeKeepsAuthoredTimingAndTransitions(FacilityProductionCycle.FacilityKind kind,
            float load, float transform, float work, float output, float end)
        {
            var timing = FacilityProductionCycle.Recipe(kind);
            Assert.That(new[] { timing.Load, timing.Transform, timing.Work, timing.Output, timing.End },
                Is.EqualTo(new[] { load, transform, work, output, end }));
            const float epsilon = .001f;
            Assert.That(FacilityProductionCycle.StageAt(kind, load - epsilon), Is.EqualTo(FacilityProductionCycle.ProcessStage.Loading));
            Assert.That(FacilityProductionCycle.StageAt(kind, load), Is.EqualTo(FacilityProductionCycle.ProcessStage.Working));
            Assert.That(FacilityProductionCycle.StageAt(kind, transform), Is.EqualTo(FacilityProductionCycle.ProcessStage.Working));
            Assert.That(FacilityProductionCycle.StageAt(kind, work - epsilon), Is.EqualTo(FacilityProductionCycle.ProcessStage.Working));
            Assert.That(FacilityProductionCycle.StageAt(kind, work), Is.EqualTo(FacilityProductionCycle.ProcessStage.Output));
            Assert.That(FacilityProductionCycle.StageAt(kind, output - epsilon), Is.EqualTo(FacilityProductionCycle.ProcessStage.Output));
            Assert.That(FacilityProductionCycle.StageAt(kind, output), Is.EqualTo(FacilityProductionCycle.ProcessStage.Returning));
            Assert.That(FacilityProductionCycle.StageAt(kind, end - epsilon), Is.EqualTo(FacilityProductionCycle.ProcessStage.Returning));
            Assert.That(FacilityProductionCycle.StageAt(kind, end), Is.EqualTo(FacilityProductionCycle.ProcessStage.Loading));
            Assert.That(FacilityProductionCycle.StageAt(kind, end * 3 + (load + work) / 2), Is.EqualTo(FacilityProductionCycle.ProcessStage.Working));
        }

        [Test]
        public void EveryFacilityHasOrderedPositiveTiming()
        {
            foreach (FacilityProductionCycle.FacilityKind kind in Enum.GetValues(typeof(FacilityProductionCycle.FacilityKind)))
            {
                var timing = FacilityProductionCycle.Recipe(kind);
                Assert.That(timing.Load, Is.GreaterThan(0), kind.ToString());
                Assert.That(timing.Transform, Is.GreaterThan(timing.Load), kind.ToString());
                Assert.That(timing.Work, Is.GreaterThan(timing.Transform), kind.ToString());
                Assert.That(timing.Output, Is.GreaterThan(timing.Work), kind.ToString());
                Assert.That(timing.End, Is.GreaterThan(timing.Output), kind.ToString());
            }
        }

        [Test]
        public void MissingConfigurationReportsFieldsAndDisablesCycle()
        {
            var root = new GameObject("UnconfiguredFacility");
            try
            {
                var cycle = root.AddComponent<FacilityProductionCycle>();
                LogAssert.Expect(LogType.Error, "FacilityProductionCycle: 必須参照が未設定です: _frame, _input, _raw, _finished, _output, _center, _stand");
                // EditMode cannot send Unity's native Awake message to a runtime-only behaviour.
                typeof(FacilityProductionCycle).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(cycle, null);
                Assert.That(cycle.enabled, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
