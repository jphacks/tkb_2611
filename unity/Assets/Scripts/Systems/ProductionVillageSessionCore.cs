using System;
using System.Collections.Generic;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Systems
{
    /// <summary>Only owner of the published village. All commands commit a detached candidate first.</summary>
    public sealed class ProductionVillageSessionCore
    {
        private VillageGame _game;
        private readonly IProductionVillageStore _productionStore,_debugStore;
        private IProductionVillageStore _store;
        private readonly Func<VillageGame> _initial;
        private readonly bool _developmentAllowed;
        private long _utc;
        private int _tutorial;
        public bool Development {get;private set;}
        public bool Running {get;private set;}=true;
        public double TimeScale {get;private set;}=1;
        public string Error {get;private set;}="";
        public string Notice {get;private set;}="配置から建てる・動かすを選べます";
        public VillageView View=>_game?.CreateView();
        public HealthRecordLedger Records=>_game?.Records;
        public event Action Changed;
        public ProductionVillageSessionCore(IProductionVillageStore production,IProductionVillageStore debug,Func<VillageGame> initial,bool developmentAllowed)
        {_productionStore=production;_debugStore=debug;_store=production;_initial=initial;_developmentAllowed=developmentAllowed;}
        public void Open(long utc)
        {
            try
            {
                var s=_store.Load();
                if(s==null){_tutorial=VillageTutorial.NotStarted;var candidate=_initial();_store.Save(Snapshot(candidate,utc));_game=candidate;_utc=utc;}
                else{s.Validate("production");_tutorial=new VillageTutorial(s.Tutorial).Step;var g=VillageGame.Restore(s.State);g.AdvanceTo(checked(g.RequestedMs+Math.Max(0,utc-s.UtcMs)));_utc=Math.Max(utc,s.UtcMs);_store.Save(Snapshot(g,_utc));_game=g;}
                Error="";
            }
            catch(Exception){Error="村を読み込めませんでした。保存データを保護しています。メニューから再試行してください";_game=null;}
            Changed?.Invoke();
        }
        private ProductionVillageSave Snapshot(VillageGame g,long utc)=>new ProductionVillageSave{State=g.Export(),UtcMs=utc,Profile=Development?"development":"production",Running=Running,TimeScale=TimeScale,Tutorial=_tutorial};
        /// <param name="quiet">true なら <see cref="Notice"/> を書き換えず、失敗しても知らせない (すれ違いの台帳の更新など、画面に出さない記録)。</param>
        private bool Command(Func<VillageGame,VillageResult> command,long utc,bool quiet=false)
        {
            if(_game==null)return false;
            try
            {
                var candidate=VillageGame.Restore(_game.Export());candidate.SetGroupViewActive(_game.GroupViewActive);var result=command(candidate);if(!quiet)Notice=result.Reason;
                if(!result.Success){if(!quiet)Changed?.Invoke();return false;}
                _store.Save(Snapshot(candidate,Math.Max(_utc,utc)));_game=candidate;_utc=Math.Max(_utc,utc);Error="";Changed?.Invoke();return true;
            }
            catch(Exception){Error="保存できませんでした。操作を反映せず、直前の村を維持しています";Changed?.Invoke();return false;}
        }
        /// <summary>チュートリアルの段 (VillageTutorial)。0 = 未開始、-1 = 終わり。</summary>
        public int Tutorial=>_tutorial;
        /// <summary>チュートリアルの段を保存する。村の状態は変えない。</summary>
        public bool SetTutorial(int step,long utc)
        {
            if(_game==null||!VillageTutorial.IsValid(step))return false;
            if(_tutorial==step)return true;
            var previous=_tutorial;_tutorial=step;
            try{_store.Save(Snapshot(_game,Math.Max(_utc,utc)));_utc=Math.Max(_utc,utc);return true;}
            catch(Exception){_tutorial=previous;Error="保存できませんでした。チュートリアルの進み具合は次の保存で反映します";Changed?.Invoke();return false;}
        }
        public void SetGroupViewActive(bool active)
        {
            if(_game==null||_game.GroupViewActive==active)return;
            _game.SetGroupViewActive(active);Changed?.Invoke();
        }
        private static VillageResult Ok()=>VillageCommands.Accepted();
        public bool Place(FacilityKind kind,int x,int y,long utc)=>Command(g=>g.Place(kind,x,y),utc);
        public bool Move(int id,int x,int y,long utc)=>Command(g=>g.Move(id,x,y),utc);
        public bool Remove(int id,long utc)=>Command(g=>g.Remove(id),utc);
        public string CanPlace(FacilityKind kind,int x,int y)=>_game?.CanPlace(kind,x,y)??"村を読み込めません";
        public string CanMove(int id,int x,int y)=>_game?.CanMove(id,x,y)??"村を読み込めません";
        /// <summary>置いたらどこにつながるか (状態は変えない)。動かしている施設は <paramref name="ignoreId"/> で除く。</summary>
        public VillageLinkPreview PreviewLinks(FacilityKind kind,int x,int y,int ignoreId=0)=>_game?.PreviewLinks(kind,x,y,ignoreId);
        public int ReachOfNode(int id)=>_game?.ReachOfNode(id)??0;
        public IReadOnlyList<VillageGroupHint> GroupHints()=>_game?.GroupHints()??Array.Empty<VillageGroupHint>();
        public bool AcceptSteps(int day,int steps,int today,long utc)=>Command(g=>g.AcceptSteps(day,steps,today),utc);
        public bool AcceptWater(string id,int day,int today,bool confirmed,long utc)=>Command(g=>g.AcceptWater(id,day,today,confirmed),utc);
        public bool AcceptScreen(string id,int day,int used,int today,bool full,bool confirmed,long utc)=>Command(g=>g.AcceptScreen(id,day,used,today,full,confirmed),utc);
        // ---- すれ違い (通信は VillageEncounters。時刻は端末の UTC のミリ秒) ----
        public int EncounterDay=>_game?.EncounterDay??-1;
        public string EncounterSelfToken=>_game?.EncounterSelfToken;
        public (int Seen,int Waiting,int Granted) Encounters=>_game==null?(0,0,0):(_game.EncountersSeen,_game.EncountersWaiting,_game.EncountersGranted);
        public bool BeginEncounterDay(int appDay,long utc)=>Command(g=>g.BeginEncounterDay(appDay),utc,quiet:true);
        // 台帳の更新は村を写し直すので、記録しない相手・送る報告が無いときは、先に読むだけで確かめて何もしない。
        public bool ObservePeer(string token,long utc)=>_game!=null&&_game.WouldObservePeer(token)&&Command(g=>g.ObservePeer(token,utc),utc,quiet:true);
        /// <summary>今送る報告。選んだら送り直しの予定を進めて保存する。無ければ null。</summary>
        public EncounterReport NextEncounterReport(long utc)
        {
            if(_game==null||!_game.HasEncounterReportDue(utc))return null;
            EncounterReport report=null;
            Command(g=>g.TakeEncounterReport(utc,out report),utc,quiet:true);
            return report;
        }
        public bool EncounterFailed(EncounterReport report,long retryAfterMs,long utc)=>Command(g=>g.EncounterFailed(report,utc,retryAfterMs),utc,quiet:true);
        public bool DiscardEncounter(EncounterReport report,long utc)=>Command(g=>g.DiscardEncounter(report),utc,quiet:true);
        /// <summary>報告の結果を入れる。成立して資源を受け取ったら <see cref="Notice"/> にそう書く。</summary>
        public EncounterOutcome ApplyEncounter(EncounterReport report,bool matched,string pairId,long utc)
        {
            var outcome=EncounterOutcome.Ignored;var reason="";
            var applied=Command(g=>{var r=g.ApplyEncounter(report,matched,pairId,utc,out var view);outcome=view.Outcome;reason=r.Reason;return r;},utc,quiet:true);
            if(!applied)return EncounterOutcome.Ignored;
            if(outcome==EncounterOutcome.Granted||outcome==EncounterOutcome.CapReached){Notice=reason;Changed?.Invoke();}
            return outcome;
        }
        public void Tick(long elapsedMs)
        {
            if(_game==null||!Running||elapsedMs<=0)return;
            try{_game.AdvanceTo(checked(_game.RequestedMs+elapsedMs));}catch(Exception){Error="時間の範囲を超えたため進行を停止しました";Running=false;}
        }
        public bool Save(long utc)
        {
            if(_game==null)return false;
            try{_store.Save(Snapshot(_game,Math.Max(_utc,utc)));_utc=Math.Max(_utc,utc);Error="";return true;}
            catch(Exception){Error="保存できません。空き容量と保存先を確認して再試行してください";Changed?.Invoke();return false;}
        }
        public bool Resume(long utc,long suspendedUtc)
        {
            if(_game==null)return false;
            return Command(g=>{g.SetGroupViewActive(false);if(Running)g.AdvanceTo(checked(g.RequestedMs+(long)(Math.Max(0,utc-suspendedUtc)*TimeScale)));return Ok();},utc);
        }
        public bool BeginDevelopment(long utc)
        {
            if(!_developmentAllowed||_game==null||Development)return false;
            try
            {
                var save=Snapshot(_game,Math.Max(utc,_utc));save.Profile="development";_debugStore.Save(save);
                Development=true;_store=_debugStore;Notice="開発プロフィールへ切替済み。製品用DBへは保存しません";Changed?.Invoke();return true;
            }
            catch(Exception){Error="開発用保存を開始できませんでした";Changed?.Invoke();return false;}
        }
        public bool FreeResources=>_game?.FreeResources==true;
        private bool _freeFromProduction;
        /// <summary>
        /// ガス・電気・水を減らさないデバッグのモード。全ビルドで使える (審査員が自分でビルドした iOS でも遊べるように)。
        /// 製品用の村からは開発用の保存へ写してから入り、切ると製品用の村を読み直す。製品用の保存は書き換えない。
        /// 前回の資源無限の村が開発用の保存に残っていれば、その続きから始める。
        /// </summary>
        public bool SetFreeResources(bool on,long utc)
        {
            if(_game==null)return false;
            if(FreeResources==on)return true;
            // Editor の開発プロフィールにいる、または開発用の保存で切り替えたときは、その村の旗だけを替える。
            if(on?Development:!_freeFromProduction)
            {
                var changed=Command(g=>{g.SetFreeResources(on);return Ok();},utc);
                if(changed)Notice=on?"資源無限のモードにしました":"資源無限のモードを切りました";
                Changed?.Invoke();return changed;
            }
            if(!Save(utc))return false;
            return on?EnterFreeResources(Math.Max(utc,_utc)):LeaveFreeResources(utc);
        }
        private bool EnterFreeResources(long utc)
        {
            try
            {
                var candidate=ResumeFreeVillage(utc)??VillageGame.Restore(_game.Export());
                candidate.SetFreeResources(true);candidate.SetGroupViewActive(_game.GroupViewActive);
                Development=true;
                try{_debugStore.Save(Snapshot(candidate,utc));}catch(Exception){Development=false;throw;}
                _store=_debugStore;_game=candidate;_utc=utc;_freeFromProduction=true;Error="";
                Notice="資源無限のモードにしました。ふだんの村はそのまま残っているよ";Changed?.Invoke();return true;
            }
            catch(Exception){Error="資源無限のモードを始められませんでした";Changed?.Invoke();return false;}
        }
        private bool LeaveFreeResources(long utc)
        {
            _store=_productionStore;Development=false;_freeFromProduction=false;Open(utc);
            if(_game==null)return false;
            Notice="ふだんの村に戻りました";Changed?.Invoke();return true;
        }
        private VillageGame ResumeFreeVillage(long utc)
        {
            try
            {
                var s=_debugStore.Load();
                if(s?.State==null||!s.State.FreeResources)return null;
                var g=VillageGame.Restore(s.State);g.AdvanceTo(checked(g.RequestedMs+Math.Max(0,utc-s.UtcMs)));return g;
            }
            catch(Exception){return null;}
        }
        private bool CanDevelop()=>_developmentAllowed&&Development&&_game!=null;
        public bool Resources(double gas,double electricity,double water,bool add,long utc)
        {
            if(!CanDevelop())return false;
            if(!VillageGame.Finite(gas)||!VillageGame.Finite(electricity)||!VillageGame.Finite(water)||gas<0||electricity<0||water<0){Notice="有限の0以上の値を入力してください";return false;}
            return Command(g=>{var v=g.CreateView();g.SetResources(gas+(add?v.Gas:0),electricity+(add?v.Electricity:0),water+(add?v.Water:0));return Ok();},utc);
        }
        public bool Inject(int id,Item item,int count,long utc)=>CanDevelop()&&Command(g=>g.Inject(id,item,count),utc);
        public bool Configure(bool infinite,double speed,int deliveryMs,double timeScale,bool running,long utc)
        {
            if(!CanDevelop()||!VillageGame.Finite(timeScale)||timeScale<0||timeScale>100)return false;
            var oldRunning=Running;var oldScale=TimeScale;Running=running;TimeScale=timeScale;
            var ok=Command(g=>{g.Configure(infinite,speed,deliveryMs);return Ok();},utc);
            if(!ok){Running=oldRunning;TimeScale=oldScale;}return ok;
        }
        public bool Forward(double seconds,long utc)
        {
            if(!CanDevelop()||!VillageGame.Finite(seconds)||seconds<0||seconds>3600)return false;
            return Command(g=>{g.AdvanceTo(checked(g.RequestedMs+(long)(seconds*1000)));return Ok();},utc);
        }
        public bool ResetDevelopment(long utc)
        {
            if(!CanDevelop())return false;
            try{var candidate=_initial();_store.Save(Snapshot(candidate,Math.Max(_utc,utc)));_game=candidate;Notice="開発用村を初期化しました";Changed?.Invoke();return true;}
            catch(Exception){Error="開発用村を初期化できませんでした。直前の状態を保持します";return false;}
        }
    }
}
