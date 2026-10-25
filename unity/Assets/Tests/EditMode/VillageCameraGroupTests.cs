using NUnit.Framework;
using UnityEngine;
using ProjectPenguin.Presentation.World.ProductionVillage;
namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageCameraGroupTests
    {
        [Test] public void TwoLevelsDriveGroupViewAndFrameReturnsToOverview()
        {
            var go=new GameObject("ZoomCameraTest",typeof(Camera));
            try
            {
                var controller=go.AddComponent<VillageCameraController>();
                var camera=go.GetComponent<Camera>();
                controller.Frame();
                Assert.That(controller.Level,Is.EqualTo(VillageZoomLevel.Overview));
                Assert.That(controller.GroupViewActive,Is.True,"全体表示の間は施設群の追加生産が動く");
                Assert.That(camera.orthographicSize,Is.EqualTo(controller.FitSize).Within(1e-4f));

                controller.SetLevel(VillageZoomLevel.Detail);
                Assert.That(controller.Level,Is.EqualTo(VillageZoomLevel.Detail));
                Assert.That(controller.GroupViewActive,Is.False,"段階を決めた時点で詳細として扱う");
                Assert.That(controller.Switching,Is.True,"寄りは短い補間で動く");

                controller.ToggleLevel();
                Assert.That(controller.Level,Is.EqualTo(VillageZoomLevel.Overview));

                controller.SetLevel(VillageZoomLevel.Detail);
                controller.Frame();
                Assert.That(controller.Level,Is.EqualTo(VillageZoomLevel.Overview));
                Assert.That(controller.Switching,Is.False,"Frame は補間せずに全体へ戻す");
                Assert.That(camera.orthographicSize,Is.EqualTo(controller.FitSize).Within(1e-4f));

                controller.SetTopDown(true);
                Assert.That(controller.Level,Is.EqualTo(VillageZoomLevel.Overview));
                Assert.That(camera.orthographicSize,Is.EqualTo(controller.FitSize).Within(1e-4f));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void DetailSizeIsHalfOfOverview()
        {
            Assert.That(VillageZoomPolicy.SizeFor(VillageZoomLevel.Overview,10),Is.EqualTo(10));
            Assert.That(VillageZoomPolicy.SizeFor(VillageZoomLevel.Detail,10),Is.EqualTo(10*VillageZoomPolicy.DetailRatio));
            Assert.That(VillageZoomPolicy.GroupView(VillageZoomLevel.Overview),Is.True);
            Assert.That(VillageZoomPolicy.GroupView(VillageZoomLevel.Detail),Is.False);
        }
    }
}
