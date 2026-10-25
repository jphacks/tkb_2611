using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public sealed class VillageWorldPresenter : MonoBehaviour
    {
        /// <summary>施設群の案内 (あと 1 棟) を数え直す間隔。毎フレームは数えない。</summary>
        private const float HintSeconds=.5f;

        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private Camera _camera;
        [SerializeField] private VillagePrefabCatalog _catalog;
        [SerializeField] private GameObject _harbor;
        public VillageWorldRenderer Renderer {get;private set;}
        public VillageLinkView Links {get;private set;}
        public VillageGuideView Guide {get;private set;}
        public VillagePlacementController Placement {get;private set;}
        public Camera ViewCamera=>_camera;
        public VillageCameraController CameraController=>_camera!=null?_camera.GetComponent<VillageCameraController>():null;
        public int SelectedId {get;set;}
        public bool IndustrialMode {get;set;}
        /// <summary>あと 1 棟で施設群が成立する場所 (HintSeconds ごとに更新)。</summary>
        public IReadOnlyList<VillageGroupHint> Hints {get;private set;}=Array.Empty<VillageGroupHint>();
        /// <summary>配置中の候補が、どこにつながるか。候補が無ければ null。</summary>
        public VillageLinkPreview PlacementLinks {get;private set;}
        private bool _focused=true, _paused;
        private float _nextHints;
        private (FacilityKind Kind,int X,int Y,int Moving,int Layout) _previewKey;
        private int _layout;
        private void Start()
        {
            Renderer=new VillageWorldRenderer(transform,_camera,_catalog,_harbor);
            Links=new VillageLinkView(transform,_catalog);
            Guide=new VillageGuideView(Renderer.Canvas,transform,_catalog);
            Placement=new VillagePlacementController(_session.Core);_session.Suspending+=Placement.Cancel;_session.LayoutApplied+=OnLayoutApplied;
        }
        private void OnLayoutApplied(){Placement.Cancel();SelectedId=0;_nextHints=0;_layout++;}
        private void LateUpdate()
        {
            var camera=CameraController;
            var active=_focused&&!_paused&&!_session.IsSuspended&&camera!=null&&camera.GroupViewActive;
            _session.Core.SetGroupViewActive(active);
            var v=_session.Core.View;if(v==null||Renderer==null)return;
            var level=camera!=null?camera.Level:VillageZoomLevel.Overview;
            var placing=Placement.Mode==VillagePlacementMode.Build||Placement.Mode==VillagePlacementMode.MovePreview;
            var selected=Placement.MovingId>0?Placement.MovingId:SelectedId;
            Renderer.IndustrialMode=IndustrialMode;
            Renderer.Render(v,selected,_session.Core.Running);
            Links.Render(v,level,selected,placing,_session.Core.Running);
            Renderer.Preview(Placement.Kind,Placement.X,Placement.Y,Placement.Reason.Length==0,Placement.HasCandidate);

            if(Time.unscaledTime>=_nextHints){_nextHints=Time.unscaledTime+HintSeconds;Hints=_session.Core.GroupHints();}
            var candidate=placing&&Placement.HasCandidate;
            if(candidate)
            {
                var key=(Placement.Kind,Placement.X,Placement.Y,Placement.MovingId,_layout+v.Nodes.Count);
                if(PlacementLinks==null||key!=_previewKey){_previewKey=key;PlacementLinks=_session.Core.PreviewLinks(Placement.Kind,Placement.X,Placement.Y,Placement.MovingId);}
            }
            else PlacementLinks=null;
            Guide.Render(v,new VillageGuideView.Input{
                Industrial=IndustrialMode||placing,Placing=candidate,Kind=Placement.Kind,X=Placement.X,Y=Placement.Y,Preview=PlacementLinks,
                SelectedId=placing?0:SelectedId,SelectedReach=placing||SelectedId==0?0:_session.Core.ReachOfNode(SelectedId),Hints=Hints});
        }
        private void OnApplicationFocus(bool focused){_focused=focused;if(!focused)_session?.Core?.SetGroupViewActive(false);}
        private void OnApplicationPause(bool paused){_paused=paused;if(paused)_session?.Core?.SetGroupViewActive(false);}
        private void OnDisable()=>_session?.Core?.SetGroupViewActive(false);
        private void OnDestroy(){_session?.Core?.SetGroupViewActive(false);if(Placement!=null){_session.Suspending-=Placement.Cancel;_session.LayoutApplied-=OnLayoutApplied;}}
    }
}
