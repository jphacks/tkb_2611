using System.Linq;
using ProjectPenguin.Domain.Island;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectPenguin.VillageUiPrototype
{
    public sealed class VillageInteraction : MonoBehaviour
    {
        private VillageDriver _driver;
        private bool _pressed,_panning,_dragging,_shelfDrag,_choose,_touch;
        private Vector2 _start,_last,_lastPointer,_grab;
        private float _downAt,_pinch;
        private int _moving,_x,_y;private FacilityKind _kind;
        private bool _valid;
        public bool IsPlacing=>_choose||_dragging;
        public void Initialize(VillageDriver driver){_driver=driver;}
        public void Choose(FacilityKind kind){Cancel();_kind=kind;_choose=true;}
        public void BeginShelf(FacilityKind kind,Vector2 p){Cancel();_kind=kind;_shelfDrag=true;_dragging=true;Preview(p);}
        public void DragShelf(Vector2 p){if(_shelfDrag)Preview(p);}
        public void EndShelf(Vector2 p){if(!_shelfDrag)return;Preview(p);Commit();_driver.Hud.CloseBuild();}
        public void Cancel()
        {_pressed=_panning=_dragging=_shelfDrag=_choose=false;_moving=0;_pinch=0;_driver?.World?.Preview(_kind,0,0,false,false);}
        private void Update()
        {
            if(_driver?.Factory==null)return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Mouse.current?.rightButton.wasPressedThisFrame==true){Cancel();_driver.Hud.CloseSheet();return;}
            var touches=Touchscreen.current?.touches.Where(t=>t.press.isPressed).ToArray();
            if(touches!=null&&touches.Length>=2)
            {
                if(_pressed||_dragging||_choose)Cancel();
                var distance=Vector2.Distance(touches[0].position.ReadValue(),touches[1].position.ReadValue());
                if(_pinch>0)Zoom(_pinch/Mathf.Max(1,distance));_pinch=distance;return;
            }
            _pinch=0;if(_shelfDrag)return;
            var primary=Touchscreen.current?.primaryTouch;
            _touch=primary!=null&&(primary.press.isPressed||primary.press.wasReleasedThisFrame);
            if(_touch&&primary.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled){Cancel();return;}
            var p=_touch?primary.position.ReadValue():Mouse.current?.position.ReadValue()??Vector2.zero;
            var down=_touch?primary.press.wasPressedThisFrame:Mouse.current?.leftButton.wasPressedThisFrame==true;
            var held=_touch?primary.press.isPressed:Mouse.current?.leftButton.isPressed==true;
            var up=_touch?primary.press.wasReleasedThisFrame:Mouse.current?.leftButton.wasReleasedThisFrame==true;
            if(!_touch&&Mouse.current!=null&&!_driver.Hud.Blocks(p))
            {var wheel=Mouse.current.scroll.ReadValue().y;if(Mathf.Abs(wheel)>.1f)Zoom(Mathf.Exp(-wheel*.0015f));}
            if(_choose)Preview(p);
            if(down&&!_driver.Hud.Blocks(p))
            {
                _pressed=true;_start=_last=p;_downAt=Time.unscaledTime;
                if(Ground(p,out var point))
                {
                    var gx=point.x/VillageWorld.Cell+12;var gy=point.z/VillageWorld.Cell+12;
                    var n=_driver.View.Nodes.LastOrDefault(a=>gx>=a.X&&gx<a.X+a.Size&&gy>=a.Y&&gy<a.Y+a.Size);
                    _moving=_choose?0:n?.Id??0;_grab=n==null||_choose?Vector2.zero:new Vector2(gx-n.X,gy-n.Y);
                }
            }
            if(_pressed&&held)
            {
                if(!_choose&&!_dragging&&!_panning&&_moving>0&&Time.unscaledTime-_downAt>.35f)
                {_dragging=true;_kind=_driver.View.Nodes.First(n=>n.Id==_moving).Kind;}
                if(_dragging||_choose)Preview(p);
                else if(Vector2.Distance(p,_start)>18){_panning=true;Pan(_last,p);}
                _last=p;
            }
            if(_pressed&&up)
            {
                if(_dragging||_choose){Preview(p);Commit();}
                else if(!_panning&&Ground(p,out var point))
                {
                    if(Vector3.Distance(new Vector3(point.x,.1f,point.z),_driver.World.HarborPosition)<1.7f)_driver.Hud.ShowStats(true);
                    else{var gx=Mathf.FloorToInt(point.x/VillageWorld.Cell+12);var gy=Mathf.FloorToInt(point.z/VillageWorld.Cell+12);
                        var n=_driver.View.Nodes.LastOrDefault(a=>gx>=a.X&&gx<a.X+a.Size&&gy>=a.Y&&gy<a.Y+a.Size);if(n!=null)_driver.Select(n.Id);else _driver.Hud.CloseSheet();}
                }
                _pressed=_panning=false;_moving=0;
            }
            _lastPointer=p;
        }
        private void Preview(Vector2 p)
        {
            var aim=p+(_touch&&_moving==0?new Vector2(0,Screen.height*.055f):Vector2.zero);
            if(!Ground(aim,out var point))return;
            var size=IslandCatalog.Get(_kind).Size;
            _x=Mathf.FloorToInt(point.x/VillageWorld.Cell+12-(_moving>0?_grab.x:size*.5f));
            _y=Mathf.FloorToInt(point.z/VillageWorld.Cell+12-(_moving>0?_grab.y:size*.5f));
            var reason=_moving>0?_driver.Factory.CanMove(_moving,_x,_y):_driver.Factory.CanPlace(_kind,_x,_y);
            _valid=reason.Length==0&&!_driver.Hud.Blocks(p);
            _driver.World.Preview(_kind,_x,_y,_valid,true);
        }
        private void Commit()
        {
            if(_valid){if(_moving>0)_driver.Move(_moving,_x,_y);else _driver.Place(_kind,_x,_y);}
            else _driver.ShowNotice("配置を取り消しました。重ならない空き地へ置いてください");
            Cancel();
        }
        private bool Ground(Vector2 p,out Vector3 point)
        {var ray=_driver.ViewCamera.ScreenPointToRay(p);if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var d)){point=ray.GetPoint(d);return true;}point=Vector3.zero;return false;}
        private void Pan(Vector2 from,Vector2 to)
        {if(!Ground(from,out var a)||!Ground(to,out var b))return;var c=_driver.ViewCamera;c.transform.position+=a-b;var pos=c.transform.position;pos.x=Mathf.Clamp(pos.x,-8,8);pos.z=Mathf.Clamp(pos.z,-30,-8);c.transform.position=pos;}
        private void Zoom(float factor){var c=_driver.ViewCamera;c.orthographicSize=Mathf.Clamp(c.orthographicSize*factor,5,20);}
    }
}
