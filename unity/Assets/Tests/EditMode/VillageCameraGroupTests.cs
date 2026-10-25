using NUnit.Framework;
using UnityEngine;
using ProjectPenguin.Presentation.World.ProductionVillage;
namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageCameraGroupTests
    {
        [Test] public void FrameZoomAndInvalidFactorsKeepGroupStateConsistent()
        {
            var go=new GameObject("ZoomCameraTest",typeof(Camera));
            try
            {
                var controller=go.AddComponent<VillageCameraController>();
                var state=typeof(VillageCameraController).GetProperty("GroupViewActive");
                Assert.That(state,Is.Not.Null,"縮小状態をPresentationへ公開する必要があります");
                controller.Frame();Assert.That(state.GetValue(controller),Is.True);
                controller.Zoom(.7f);Assert.That(state.GetValue(controller),Is.False);
                var size=go.GetComponent<Camera>().orthographicSize;
                controller.Zoom(float.NaN);controller.Zoom(-1);
                Assert.That(go.GetComponent<Camera>().orthographicSize,Is.EqualTo(size));
                controller.Zoom(1.4f);Assert.That(state.GetValue(controller),Is.True);
                controller.SetTopDown(true);controller.Frame();Assert.That(state.GetValue(controller),Is.True);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
