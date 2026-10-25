using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public enum VillagePlacementMode { Browse, Build, MoveSelect, MovePreview }
    public sealed class VillagePlacementController
    {
        private readonly ProductionVillageSessionCore _session;
        public VillagePlacementMode Mode {get;private set;}
        public FacilityKind Kind {get;private set;}
        public int MovingId {get;private set;}
        public int X {get;private set;}
        public int Y {get;private set;}
        public bool HasCandidate {get;private set;}
        public string Reason {get;private set;}="";
        public event Action Changed;
        public VillagePlacementController(ProductionVillageSessionCore session){_session=session;}
        public void Build(FacilityKind kind){Cancel();Kind=kind;Mode=VillagePlacementMode.Build;Changed?.Invoke();}
        public void MoveSelect(){Cancel();Mode=VillagePlacementMode.MoveSelect;Reason="動かす施設を選んでください";Changed?.Invoke();}
        public void Select(int id)
        {
            if(Mode!=VillagePlacementMode.MoveSelect)return;var n=_session.View.Nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return;
            MovingId=id;Kind=n.Kind;Mode=VillagePlacementMode.MovePreview;Reason="移動先をタップ、またはドラッグしてください";Changed?.Invoke();
        }
        public void Preview(int x,int y)
        {
            if(Mode!=VillagePlacementMode.Build&&Mode!=VillagePlacementMode.MovePreview)return;
            X=x;Y=y;HasCandidate=true;Reason=Mode==VillagePlacementMode.Build?_session.CanPlace(Kind,x,y):_session.CanMove(MovingId,x,y);Changed?.Invoke();
        }
        public bool Confirm()
        {
            if(!HasCandidate)return false;
            var moving=Mode==VillagePlacementMode.MovePreview;
            Reason=moving?_session.CanMove(MovingId,X,Y):_session.CanPlace(Kind,X,Y);
            if(Reason.Length>0){Changed?.Invoke();return false;}
            var ok=moving?_session.Move(MovingId,X,Y,ProductionVillageSession.UtcNow):_session.Place(Kind,X,Y,ProductionVillageSession.UtcNow);
            if(ok){if(moving)MoveSelect();else Cancel();}else{Reason=_session.Error.Length>0?_session.Error:_session.Notice;Changed?.Invoke();}return ok;
        }
        public void Cancel()
        {Mode=VillagePlacementMode.Browse;MovingId=0;HasCandidate=false;Reason="";Changed?.Invoke();}
        public void CancelPreview(){if(Mode==VillagePlacementMode.MovePreview)MoveSelect();else Cancel();}
    }
}
