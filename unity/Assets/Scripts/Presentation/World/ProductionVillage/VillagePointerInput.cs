using System.Linq;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public sealed class VillagePointerInput : MonoBehaviour
    {
        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private ProductionNavigator _navigator;
        [SerializeField] private VillageCameraController _camera;
        private bool _pressed,_panning;
        private Vector2 _start,_last;
        private readonly VillagePinch _pinch = new VillagePinch();
        private readonly VillageGestureGate _gesture = new VillageGestureGate();
        private void Update()
        {
            var placement=_navigator.Placement;if(placement==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Mouse.current?.rightButton.wasPressedThisFrame==true){placement.Cancel();_pressed=false;return;}
            var touches=Touchscreen.current?.touches.Where(t=>t.press.isPressed).ToArray();
            var count=touches?.Length??0;
            var overUi=touches!=null&&touches.Any(t=>_navigator.Blocks(t.position.ReadValue()));
            if(_gesture.Observe(count,overUi,_navigator.WorldInputEnabled))
            {
                _pressed=_panning=false;
                if(_gesture.AllowPinch)
                {
                    var a=touches[0].position.ReadValue();var b=touches[1].position.ReadValue();
                    if(_pinch.Observe(Vector2.Distance(a,b),_camera.Level,out var next))_camera.SetLevel(next,(a+b)*.5f);
                }
                else _pinch.Reset();
                return;
            }
            _pinch.Reset();
            if(!_navigator.WorldInputEnabled){ResetGesture();return;}
            var touch=Touchscreen.current?.primaryTouch;var useTouch=touch!=null&&(touch.press.isPressed||touch.press.wasReleasedThisFrame);
            if(useTouch&&touch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled){ResetGesture();return;}
            var point=useTouch?touch.position.ReadValue():Mouse.current?.position.ReadValue()??Vector2.zero;
            var down=useTouch?touch.press.wasPressedThisFrame:Mouse.current?.leftButton.wasPressedThisFrame==true;
            var held=useTouch?touch.press.isPressed:Mouse.current?.leftButton.isPressed==true;
            var up=useTouch?touch.press.wasReleasedThisFrame:Mouse.current?.leftButton.wasReleasedThisFrame==true;
            if(down){_pressed=!_navigator.Blocks(point);_panning=false;_start=_last=point;}
            if(_pressed&&held)
            {
                if(_navigator.Blocks(point)){_pressed=false;return;}
                if(placement.Mode==VillagePlacementMode.Build||placement.Mode==VillagePlacementMode.MovePreview)Preview(point);
                else if(Vector2.Distance(point,_start)>18){_panning=true;_camera.Pan(_last,point);}
                _last=point;
            }
            if(_pressed&&up)
            {
                _pressed=false;if(_navigator.Blocks(point))return;
                if(placement.Mode==VillagePlacementMode.Build||placement.Mode==VillagePlacementMode.MovePreview){Preview(point);return;}
                if(_panning||!_camera.Ground(point,out var ground))return;
                var x=Mathf.FloorToInt(ground.x/VillageWorldRenderer.Cell+12);var y=Mathf.FloorToInt(ground.z/VillageWorldRenderer.Cell+12);
                var n=_session.Core.View.Nodes.LastOrDefault(a=>x>=a.X&&x<a.X+a.Size&&y>=a.Y&&y<a.Y+a.Size);
                if(placement.Mode==VillagePlacementMode.MoveSelect){if(n!=null)placement.Select(n.Id);}
                else if(n!=null)_navigator.ShowFacility(n.Id);
                else if(Vector3.Distance(ground,_navigator.World.Renderer.HarborPosition)<1.7f)_navigator.ShowSummary(true);
            }
            if(!useTouch&&Mouse.current!=null&&!_navigator.Blocks(point))
            {var next=VillageZoomPolicy.AfterWheel(_camera.Level,Mouse.current.scroll.ReadValue().y);if(next!=_camera.Level&&!_camera.Switching)_camera.SetLevel(next,point);}
        }
        private void Preview(Vector2 point)
        {
            if(!_camera.Ground(point,out var ground))return;
            var size=ProjectPenguin.Domain.Island.IslandCatalog.Get(_navigator.Placement.Kind).Size;
            _navigator.Placement.Preview(Mathf.FloorToInt(ground.x/VillageWorldRenderer.Cell+12-size*.5f),Mathf.FloorToInt(ground.z/VillageWorldRenderer.Cell+12-size*.5f));
        }
        private void ResetGesture(){_pressed=_panning=false;_pinch.Reset();_gesture.Interrupt();}
        private void OnDisable()=>ResetGesture();
        private void OnApplicationFocus(bool focused){if(!focused)ResetGesture();}
        private void OnApplicationPause(bool paused){if(paused)ResetGesture();}
    }
}
