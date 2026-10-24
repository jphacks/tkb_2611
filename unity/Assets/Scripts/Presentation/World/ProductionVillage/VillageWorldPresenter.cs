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
        public int SelectedId {get;set;}
        private void Start()
        {
            Renderer=new VillageWorldRenderer(transform,_camera,_catalog,_harbor);
            Placement=new VillagePlacementController(_session.Core);_session.Suspending+=Placement.Cancel;
        }
        private void LateUpdate()
        {
            var v=_session.Core.View;if(v==null||Renderer==null)return;
            Renderer.Render(v,Placement.MovingId>0?Placement.MovingId:SelectedId,_session.Core.Running);Renderer.UpdateCargo(v);
            Renderer.Preview(Placement.Kind,Placement.X,Placement.Y,Placement.Reason.Length==0,Placement.HasCandidate);
        }
        private void OnDestroy(){if(Placement!=null)_session.Suspending-=Placement.Cancel;}
    }
}
