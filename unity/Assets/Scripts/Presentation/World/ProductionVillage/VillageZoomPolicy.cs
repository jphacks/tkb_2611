using System;
namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>村のカメラの寄り。島全体 (施設群が追加生産する) と、施設を 1 棟ずつ見る詳細の 2 段階だけ。</summary>
    public enum VillageZoomLevel { Overview, Detail }

    /// <summary>
    /// 2 本指・ホイールの入力から次の段階を決める。連続では寄らず、しきい値を越えた瞬間に 1 段階だけ切り替える。
    /// 1 回のつまみで 1 回しか切り替えない判定は <see cref="VillagePinch"/> が持つ。
    /// </summary>
    public static class VillageZoomPolicy
    {
        /// <summary>詳細の orthographicSize は全体の何倍か。</summary>
        public const float DetailRatio=.5f;
        /// <summary>指の間隔がつまみ始めの何倍以上に広がったら詳細へ寄るか。</summary>
        public const float SpreadToDetail=1.18f;
        /// <summary>指の間隔がつまみ始めの何倍以下に狭まったら全体へ引くか。</summary>
        public const float SpreadToOverview=.85f;

        public static VillageZoomLevel AfterPinch(VillageZoomLevel current,float spread)
        {
            if(float.IsNaN(spread)||float.IsInfinity(spread)||spread<=0)return current;
            if(current==VillageZoomLevel.Overview&&spread>=SpreadToDetail)return VillageZoomLevel.Detail;
            if(current==VillageZoomLevel.Detail&&spread<=SpreadToOverview)return VillageZoomLevel.Overview;
            return current;
        }

        /// <summary>ホイールを上 (奥) へ回すと寄る。</summary>
        public static VillageZoomLevel AfterWheel(VillageZoomLevel current,float wheel)
        {
            if(float.IsNaN(wheel)||float.IsInfinity(wheel)||Math.Abs(wheel)<.1f)return current;
            return wheel>0?VillageZoomLevel.Detail:VillageZoomLevel.Overview;
        }

        public static VillageZoomLevel Toggle(VillageZoomLevel current)=>current==VillageZoomLevel.Overview?VillageZoomLevel.Detail:VillageZoomLevel.Overview;

        /// <summary>施設群の追加生産は全体表示の間だけ動く (村の規則)。</summary>
        public static bool GroupView(VillageZoomLevel level)=>level==VillageZoomLevel.Overview;

        public static float SizeFor(VillageZoomLevel level,float fitSize)=>level==VillageZoomLevel.Detail?fitSize*DetailRatio:fitSize;

        /// <summary>切り替えの補間。0〜1 の経過から、すばやく動いて静かに止まる割合を返す。</summary>
        public static float Ease(float t)
        {
            if(float.IsNaN(t))return 1;
            t=Math.Max(0,Math.Min(1,t));
            return 1-(1-t)*(1-t)*(1-t);
        }
    }

    /// <summary>1 回のつまみ (2 本目の指が触れてから全部の指が離れるまで) の切り替えを 1 回に抑える。</summary>
    public sealed class VillagePinch
    {
        private float _start;
        private bool _switched;
        public bool Active=>_start>0;

        /// <summary>指の間隔を渡す。段階を切り替えるべきときだけ true と次の段階を返す。</summary>
        public bool Observe(float distance,VillageZoomLevel current,out VillageZoomLevel next)
        {
            next=current;
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<=0)return false;
            if(_start<=0){_start=distance;_switched=false;return false;}
            if(_switched)return false;
            next=VillageZoomPolicy.AfterPinch(current,distance/_start);
            if(next==current)return false;
            _switched=true;
            return true;
        }

        public void Reset(){_start=0;_switched=false;}
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
