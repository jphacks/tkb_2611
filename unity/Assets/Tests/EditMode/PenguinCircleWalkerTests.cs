using NUnit.Framework;
using ProjectPenguin.Presentation.World;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class PenguinCircleWalkerTests
    {
        private static readonly Vector3 Center = new(2f, 0.5f, -3f);
        private const float Radius = 1.5f;

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(4f)]
        public void PointStaysOnCircleAtCenterHeight(float angle)
        {
            var point = PenguinCircleWalker.PointOnCircle(Center, Radius, angle);

            Assert.That(point.y, Is.EqualTo(Center.y).Within(1e-5f));
            Assert.That(Vector3.Distance(point, Center), Is.EqualTo(Radius).Within(1e-5f));
        }

        [TestCase(0.3f, false)]
        [TestCase(2.5f, true)]
        public void DirectionIsTangentUnitVector(float angle, bool clockwise)
        {
            var direction = PenguinCircleWalker.DirectionOnCircle(angle, clockwise);
            var radial = PenguinCircleWalker.PointOnCircle(Center, Radius, angle) - Center;

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Vector3.Dot(direction, radial), Is.EqualTo(0f).Within(1e-5f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectionPointsTowardNextPoint(bool clockwise)
        {
            const float angle = 1f;
            const float step = 1e-3f;
            var next = PenguinCircleWalker.PointOnCircle(Center, Radius, angle + (clockwise ? -step : step));
            var moved = (next - PenguinCircleWalker.PointOnCircle(Center, Radius, angle)).normalized;

            Assert.That(Vector3.Dot(PenguinCircleWalker.DirectionOnCircle(angle, clockwise), moved), Is.GreaterThan(0.99f));
        }
    }
}
