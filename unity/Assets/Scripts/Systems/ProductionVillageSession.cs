using System;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Village;
using UnityEngine;

namespace ProjectPenguin.Systems
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProductionVillageControl))]
    public sealed class ProductionVillageSession : MonoBehaviour
    {
        public ProductionVillageSessionCore Core {get;private set;}
        public static long UtcNow=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private double _fraction;
        private float _nextSave;
        private long _suspendedUtc;
        public event Action Suspending;
        public event Action LayoutApplied;
        public bool UsingInspectorDebugLayout {get;private set;}
        public string InspectorDebugError {get;private set;}="";
        private VillageGame _inspectorInitial;
        public static bool ShouldUseInspectorDebugLayout(bool developmentAllowed,bool enabled)=>developmentAllowed&&enabled;
        public bool IsSuspended=>_suspendedUtc!=0;
        private bool _paused, _unfocused;
        public bool DevelopmentAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        private void Awake()
        {
            var settings=GetComponent<ProductionVillageControl>();
            UsingInspectorDebugLayout=ShouldUseInspectorDebugLayout(DevelopmentAllowed,settings.InspectorDebugSettings?.Enabled==true);
            if(UsingInspectorDebugLayout)
            {
                // No persistent store is even constructed along this path, including invalid settings.
                Core=new ProductionVillageSessionCore(new ProductionVillageMemoryStore(),new ProductionVillageMemoryStore(true),()=>VillageGame.Restore(_inspectorInitial.Export()),DevelopmentAllowed);
                PrepareInspectorInitial(settings);
            }
            else Core=new ProductionVillageSessionCore(new ProductionVillageStore(Application.persistentDataPath),new ProductionVillageStore(Application.persistentDataPath,true),settings.CreateInitial,DevelopmentAllowed);
            Core.Open(UtcNow);_nextSave=Time.unscaledTime+20;
            if(UsingInspectorDebugLayout&&Core.View!=null)
            {
                if(!Core.BeginDevelopment(UtcNow)||!ConfigureInspector(settings))InspectorDebugError="メモリ村の開発設定を開始できませんでした";
            }
        }
        private bool PrepareInspectorInitial(ProductionVillageControl control)
        {
            try
            {
                var candidate=control.CreateInspectorDebugInitial();var s=control.InspectorDebugSettings;
                var configured=VillageGame.Restore(candidate.Export());configured.Configure(s.Unlimited,s.RecipeSpeed,s.DeliveryMs);
                new ProductionVillageSave{State=configured.Export(),Profile="development",UtcMs=UtcNow,TimeScale=s.TimeScale}.Validate("development");
                _inspectorInitial=candidate;InspectorDebugError="";return true;
            }
            catch(Exception e){InspectorDebugError="デバッグ設定を適用できません: "+e.Message;return false;}
        }
        private bool ConfigureInspector(ProductionVillageControl control)
        {var s=control.InspectorDebugSettings;return Core.Configure(s.Unlimited,s.RecipeSpeed,s.DeliveryMs,s.TimeScale,true,UtcNow);}
        public bool ApplyInspectorDebugLayout()
        {
            if(!UsingInspectorDebugLayout||!DevelopmentAllowed||Core==null)
            {InspectorDebugError="Inspector配置でデバッグ起動したメモリ村でのみ適用できます";return false;}
            var settings=GetComponent<ProductionVillageControl>();
            if(!PrepareInspectorInitial(settings))return false;
            if(Core.View==null)
            {
                Core.Open(UtcNow);
                if(Core.View==null||!Core.BeginDevelopment(UtcNow))return false;
            }
            else if(!Core.ResetDevelopment(UtcNow))return false;
            if(!ConfigureInspector(settings))return false;
            _fraction=0;LayoutApplied?.Invoke();return true;
        }
        public void RetryLoad(){if(Core.View==null)Core.Open(UtcNow);else if(_suspendedUtc>0)Resume();else Core.Save(UtcNow);}
        private void Update()
        {
            if(Core?.View==null||_suspendedUtc>0)return;
            _fraction+=Time.unscaledDeltaTime*1000*Core.TimeScale;var elapsed=(long)_fraction;_fraction-=elapsed;Core.Tick(elapsed);
            if(Time.unscaledTime>=_nextSave){Core.Save(UtcNow);_nextSave=Time.unscaledTime+20;}
        }
        private void Suspend()
        {if(_suspendedUtc!=0)return;_suspendedUtc=UtcNow;Core?.SetGroupViewActive(false);Suspending?.Invoke();Core?.Save(_suspendedUtc);}
        private void Resume()
        {if(_paused||_unfocused||_suspendedUtc==0)return;if(Core==null||!Core.Resume(UtcNow,_suspendedUtc))return;_suspendedUtc=0;_fraction=0;}
        private void OnApplicationPause(bool paused){_paused=paused;if(paused)Suspend();else Resume();}
        private void OnApplicationFocus(bool focused){_unfocused=!focused;if(!focused)Suspend();else Resume();}
        private void OnApplicationQuit()=>Core?.Save(UtcNow);
        private void OnDestroy()=>Core?.Save(UtcNow);
    }
}
