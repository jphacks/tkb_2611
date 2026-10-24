using NUnit.Framework;
using ProjectPenguin.Presentation.World;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class WorldCameraFramerTests
    {
        private const float ReferenceFov = 50f;
        private const float ReferenceAspect = 1080f / 1920f;

        [Test]
        public void SameAspectKeepsReferenceFov()
        {
            Assert.That(WorldCameraFramer.VerticalFovFor(ReferenceFov, ReferenceAspect, ReferenceAspect), Is.EqualTo(ReferenceFov).Within(1e-4f));
        }

        [TestCase(1179f / 2556f)] // 19.5:9 の縦長スマホ
        [TestCase(1536f / 2048f)] // 3:4 のタブレット
        public void HorizontalFovMatchesReference(float aspect)
        {
            var fov = WorldCameraFramer.VerticalFovFor(ReferenceFov, ReferenceAspect, aspect);

            Assert.That(HorizontalFov(fov, aspect), Is.EqualTo(HorizontalFov(ReferenceFov, ReferenceAspect)).Within(1e-3f));
        }

        [Test]
        public void TallerScreenGetsWiderVerticalFov()
        {
            Assert.That(WorldCameraFramer.VerticalFovFor(ReferenceFov, ReferenceAspect, 1179f / 2556f), Is.GreaterThan(ReferenceFov));
        }

        private static float HorizontalFov(float verticalFov, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
    }
}
