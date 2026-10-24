using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    [RequireComponent(typeof(Camera))]
    public sealed class VillageCameraController : MonoBehaviour
    {
        private Camera _camera;
        private void Awake()=>_camera=GetComponent<Camera>();
        public bool Ground(Vector2 p,out Vector3 point)
        {var ray=_camera.ScreenPointToRay(p);if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance)){point=ray.GetPoint(distance);return true;}point=default;return false;}
        public void Pan(Vector2 from,Vector2 to)
        {
            if(!Ground(from,out var a)||!Ground(to,out var b))return;var pos=transform.position+a-b;
            pos.x=Mathf.Clamp(pos.x,-10,10);pos.z=Mathf.Clamp(pos.z,-38,-8);transform.position=pos;
        }
        public void Zoom(float factor)=>_camera.orthographicSize=Mathf.Clamp(_camera.orthographicSize*factor,6,24);
    }
}
