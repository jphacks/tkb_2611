using System;
namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public static class VillageZoomPolicy
    {
        public static bool GroupActive(float size,float fit,bool previous,float enter=.9f,float exit=.8f)
        {
            if(float.IsNaN(size)||float.IsInfinity(size)||float.IsNaN(fit)||float.IsInfinity(fit)||size<=0||fit<=0)return false;
            if(float.IsNaN(enter)||float.IsInfinity(enter)||enter<=0)enter=.9f;
            if(float.IsNaN(exit)||float.IsInfinity(exit)||exit<=0||exit>=enter)exit=Math.Min(.8f,enter*.8f);
            var ratio=size/fit;
            return previous?ratio>exit:ratio>=enter;
        }
        public static bool ValidFactor(float factor)=>factor>0&&!float.IsNaN(factor)&&!float.IsInfinity(factor);
    }
    /// <summary>A gesture owns all its contacts until the last finger is lifted.</summary>
    public sealed class VillageGestureGate
    {
        private bool _draining,_blocked,_multi;
        public bool AllowPinch {get;private set;}
        public bool Observe(int contacts,bool overUi,bool enabled)
        {
            AllowPinch=false;
            if(contacts==0){_draining=_blocked=_multi=false;return false;}
            if(!enabled){Interrupt();return true;}
            if(overUi)_blocked=true;
            if(contacts>=2)_multi=true;
            AllowPinch=_multi&&contacts>=2&&!_blocked&&!_draining;
            return _draining||_blocked||_multi;
        }
        public void Interrupt(){_draining=true;AllowPinch=false;}
    }
}
