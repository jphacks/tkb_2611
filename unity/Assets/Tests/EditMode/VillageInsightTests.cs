using NUnit.Framework;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageInsightTests
    {
        [Test]
        public void SupplyPerMinute_CalculatesCorrectly()
        {
            // 0 以下の場合は 0
            Assert.That(VillageInsight.SupplyPerMinute(0, 120), Is.EqualTo(0));
            Assert.That(VillageInsight.SupplyPerMinute(-10, 120), Is.EqualTo(0));
            Assert.That(VillageInsight.SupplyPerMinute(double.NaN, 120), Is.EqualTo(0));

            // 経過分が 60 分未満でも最低 60 分として計算
            Assert.That(VillageInsight.SupplyPerMinute(120, 30), Is.EqualTo(2.0));
            Assert.That(VillageInsight.SupplyPerMinute(120, 0), Is.EqualTo(2.0));

            // 60 分以上の場合は実際の経過分で割る
            Assert.That(VillageInsight.SupplyPerMinute(240, 120), Is.EqualTo(2.0));
            Assert.That(VillageInsight.SupplyPerMinute(300, 150), Is.EqualTo(2.0));
        }

        [Test]
        public void MinutesUntilEmpty_CalculatesCorrectly()
        {
            // 収支 >= 0 は減らないので null
            Assert.That(VillageInsight.MinutesUntilEmpty(100, 0), Is.Null);
            Assert.That(VillageInsight.MinutesUntilEmpty(100, 2), Is.Null);
            Assert.That(VillageInsight.MinutesUntilEmpty(100, double.NaN), Is.Null);

            // 残量 <= 0 は 0 分
            Assert.That(VillageInsight.MinutesUntilEmpty(0, -3), Is.EqualTo(0));
            Assert.That(VillageInsight.MinutesUntilEmpty(-5, -3), Is.EqualTo(0));

            // 正常計算: 100 / 3 = 33 分 (切り捨て)
            Assert.That(VillageInsight.MinutesUntilEmpty(100, -3), Is.EqualTo(33));
            Assert.That(VillageInsight.MinutesUntilEmpty(128, -3), Is.EqualTo(42));
        }

        [Test]
        public void TankLevel_CalculatesCorrectly()
        {
            // 残量 <= 0 は 0
            Assert.That(VillageInsight.TankLevel(0, 5), Is.EqualTo(0));
            Assert.That(VillageInsight.TankLevel(-1, 5), Is.EqualTo(0));

            // 消費 <= 0 のときは満タン (1.0)
            Assert.That(VillageInsight.TankLevel(50, 0), Is.EqualTo(1.0));
            Assert.That(VillageInsight.TankLevel(50, -1), Is.EqualTo(1.0));

            // 正常計算: cap = use * 60 = 2 * 60 = 120。gas = 60 なら 0.5
            Assert.That(VillageInsight.TankLevel(60, 2), Is.EqualTo(0.5).Within(1e-5));

            // 120 を超えても 1.0 で clamp
            Assert.That(VillageInsight.TankLevel(150, 2), Is.EqualTo(1.0));
        }

        [Test]
        public void TankCapacity_CalculatesCorrectly()
        {
            // 消費 <= 0 のときは null
            Assert.That(VillageInsight.TankCapacity(0), Is.Null);
            Assert.That(VillageInsight.TankCapacity(-1), Is.Null);

            // 正常計算: use * 60
            Assert.That(VillageInsight.TankCapacity(2), Is.EqualTo(120.0));
            Assert.That(VillageInsight.TankCapacity(10), Is.EqualTo(600.0));
        }
    }
}
