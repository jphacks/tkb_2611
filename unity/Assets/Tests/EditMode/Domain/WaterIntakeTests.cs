using System;
using NUnit.Framework;
using ProjectPenguin.Domain.Reality;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>飲みきった空のペットボトルの写真 1 枚から、飲んだ水の量を決める規則。</summary>
    public sealed class WaterIntakeTests
    {
        [TestCase(0)]
        [TestCase(WaterIntake.MaxMlPerEntry + 1)]
        public void RejectsOutOfRangeWater(int ml)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterIntake.Create(DateTimeOffset.UnixEpoch, ml));
        }

        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 24, 15, 0, 0, TimeSpan.FromHours(9));

        [TestCase(500, 0, 500)]
        [TestCase(350, 0, 350)]
        [TestCase(500, 50, 450)]
        [TestCase(600, 0, 600)]
        [TestCase(2000, 100, 1900)]
        public void EmptyBottleCountsWhatWasDrunk(int capacity, int remaining, int drunk)
        {
            var result = WaterEmptyBottle.Judge(new WaterPhoto(T0, capacity, remaining, true));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Intake.Ml, Is.EqualTo(drunk));
            Assert.That(result.Intake.At, Is.EqualTo(T0));
        }

        [Test]
        public void BottleWithWaterLeftIsNotEmpty()
        {
            Assert.That(WaterEmptyBottle.Judge(new WaterPhoto(T0, 500, 300, true)).Failure, Is.EqualTo(WaterBottleFailure.NotEmpty));
            Assert.That(WaterEmptyBottle.Judge(new WaterPhoto(T0, 500, 76, true)).Failure, Is.EqualTo(WaterBottleFailure.NotEmpty),
                "容量の 15% を超えて残っている");
        }

        [Test]
        public void UnrecognizedBottleIsNotCounted()
        {
            Assert.That(WaterEmptyBottle.Judge(new WaterPhoto(T0, 500, 0, false)).Failure, Is.EqualTo(WaterBottleFailure.NotAccepted));
            Assert.That(WaterEmptyBottle.Judge(new WaterPhoto(T0, 0, 0, true)).Failure, Is.EqualTo(WaterBottleFailure.NotAccepted));
        }
    }
}
