using NUnit.Framework;
using ProjectPenguin.Domain.World;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    public sealed class IceVisualTests
    {
        [TestCase(100.0, 1.0)]
        [TestCase(40.0, 0.6325)]
        [TestCase(150.0, 1.2247)]
        public void AreaGrowsWithIceSoThatAreaIsProportional(double ice, double expected)
        {
            Assert.That(IceVisual.AreaScale(ice), Is.EqualTo(expected).Within(1e-4));
        }

        [TestCase(0, PenguinMood.Cold)]
        [TestCase(1, PenguinMood.Cold)]
        [TestCase(2, PenguinMood.Normal)]
        [TestCase(3, PenguinMood.Happy)]
        [TestCase(4, PenguinMood.Happy)]
        public void MoodFollowsTier(int tier, PenguinMood mood)
        {
            Assert.That(IceVisual.MoodOf(tier), Is.EqualTo(mood));
        }

        [Test]
        public void ColdPenguinsWalkSlower()
        {
            Assert.That(IceVisual.WalkSpeedMultiplier(1), Is.EqualTo(0.7));
            Assert.That(IceVisual.WalkSpeedMultiplier(3), Is.EqualTo(1.0));
        }

        [TestCase(0, 19, 3)]
        [TestCase(1, 19, 5)]
        [TestCase(2, 19, 8)]
        [TestCase(3, 19, 12)]
        [TestCase(4, 19, 19)]
        [TestCase(0, 2, 2)]
        public void PenguinsBeyondCapacityRestInTheCabin(int tier, int penguins, int onIce)
        {
            Assert.That(IceVisual.PenguinsOnIce(tier, penguins), Is.EqualTo(onIce));
        }

        [Test]
        public void CracksAppearOnlyBelowTheUsualTier()
        {
            Assert.That(IceVisual.CrackCount(4), Is.Zero);
            Assert.That(IceVisual.CrackCount(3), Is.Zero);
            Assert.That(IceVisual.CrackCount(2), Is.GreaterThan(0));
            Assert.That(IceVisual.CrackCount(0), Is.GreaterThanOrEqualTo(IceVisual.CrackCount(1)));
        }

        [Test]
        public void OutOfRangeTiersAreClamped()
        {
            Assert.That(IceVisual.TierName(-1), Is.EqualTo(IceVisual.TierName(0)));
            Assert.That(IceVisual.TierName(9), Is.EqualTo(IceVisual.TierName(4)));
        }
    }
}
