using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public sealed class VillageWorldPresenter : MonoBehaviour
    {
        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private Camera _camera;
        [SerializeField] private VillagePrefabCatalog _catalog;
        [SerializeField] private GameObject _harbor;
        public VillageWorldRenderer Renderer {get;private set;}
        public VillagePlacementController Placement {get;private set;}
        public Camera ViewCamera=>_camera;
        public VillageCameraController CameraController=>_camera!=null?_camera.GetComponent<VillageCameraController>():null;
        public int SelectedId {get;set;}
        public bool IndustrialMode {get;set;}
        private bool _focused=true, _paused;
        private void Start()
        {
            Renderer=new VillageWorldRenderer(transform,_camera,_catalog,_harbor);
            Placement=new VillagePlacementController(_session.Core);_session.Suspending+=Placement.Cancel;_session.LayoutApplied+=OnLayoutApplied;
        }
        private void OnLayoutApplied(){Placement.Cancel();SelectedId=0;}
        private void LateUpdate()
        {
            var active=_focused&&!_paused&&!_session.IsSuspended&&CameraController!=null&&CameraController.GroupViewActive;
            _session.Core.SetGroupViewActive(active);
            var v=_session.Core.View;if(v==null||Renderer==null)return;
            Renderer.IndustrialMode=IndustrialMode;
            Renderer.Render(v,Placement.MovingId>0?Placement.MovingId:SelectedId,_session.Core.Running);Renderer.UpdateCargo(v);
            Renderer.Preview(Placement.Kind,Placement.X,Placement.Y,Placement.Reason.Length==0,Placement.HasCandidate);
        }
        private void OnApplicationFocus(bool focused){_focused=focused;if(!focused)_session?.Core?.SetGroupViewActive(false);}
        private void OnApplicationPause(bool paused){_paused=paused;if(paused)_session?.Core?.SetGroupViewActive(false);}
        private void OnDisable()=>_session?.Core?.SetGroupViewActive(false);
        private void OnDestroy(){_session?.Core?.SetGroupViewActive(false);if(Placement!=null){_session.Suspending-=Placement.Cancel;_session.LayoutApplied-=OnLayoutApplied;}}
    }
}
