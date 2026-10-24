using System;
using ProjectPenguin.Data;
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
            Core=new ProductionVillageSessionCore(new ProductionVillageStore(Application.persistentDataPath),new ProductionVillageStore(Application.persistentDataPath,true),settings.CreateInitial,DevelopmentAllowed);
            Core.Open(UtcNow);_nextSave=Time.unscaledTime+20;
        }
        public void RetryLoad(){if(Core.View==null)Core.Open(UtcNow);else if(_suspendedUtc>0)Resume();else Core.Save(UtcNow);}
        private void Update()
        {
            if(Core?.View==null||_suspendedUtc>0)return;
            _fraction+=Time.unscaledDeltaTime*1000*Core.TimeScale;var elapsed=(long)_fraction;_fraction-=elapsed;Core.Tick(elapsed);
            if(Time.unscaledTime>=_nextSave){Core.Save(UtcNow);_nextSave=Time.unscaledTime+20;}
        }
        private void Suspend()
        {if(_suspendedUtc!=0)return;Suspending?.Invoke();_suspendedUtc=UtcNow;Core?.Save(_suspendedUtc);}
        private void Resume()
        {if(_suspendedUtc==0)return;if(Core==null||!Core.Resume(UtcNow,_suspendedUtc))return;_suspendedUtc=0;_fraction=0;}
        private void OnApplicationPause(bool paused){if(paused)Suspend();else Resume();}
        private void OnApplicationFocus(bool focused){if(!focused)Suspend();else Resume();}
        private void OnApplicationQuit()=>Core?.Save(UtcNow);
        private void OnDestroy()=>Core?.Save(UtcNow);
    }
}
