using System;
using UnityEngine;

namespace ProjectPenguin.Systems
{
    [RequireComponent(typeof(ProductionVillageSession))]
    public sealed class VillageHealthInput : MonoBehaviour
    {
        private IStepSource _source;
        private ProductionVillageSession _session;
        private float _nextRequest;
        private bool _pending;
        public string Status {get;private set;}="歩数を確認しています";
        private void Start(){_session=GetComponent<ProductionVillageSession>();_source=StepSources.Create();Request();}
        public static int Day(DateTimeOffset date)=>(int)(date.Date-new DateTime(1970,1,1)).TotalDays;
        /// <summary>歩数を測れない環境 (Editor など、<see cref="FakeStepSource"/>) で、開発用の操作から歩数を入れられる。</summary>
        public bool CanAddDevelopmentSteps=>_source is FakeStepSource;
        /// <summary>今日の歩数に足して、実機と同じ受け取りの道 (<see cref="Update"/>) で村に渡す。</summary>
        public void AddDevelopmentSteps(int steps)
        {
            if(!(_source is FakeStepSource fake)||steps<=0)return;
            fake.Steps=Math.Min(172800,fake.Steps+steps);
            var now=DateTimeOffset.Now;fake.Request(new DateTimeOffset(now.Date,now.Offset).ToUniversalTime());
            _pending=true;_nextRequest=Time.unscaledTime+30;
        }
        public void Request()
        {
            if(_source==null)return;
#if !UNITY_IOS || UNITY_EDITOR
            Status="歩数はiOS実機のヘルス権限が必要です";return;
#else
            if(!_source.IsSupported){Status="この端末は歩数の取得に対応していません";return;}
            var now=DateTimeOffset.Now;var midnight=new DateTimeOffset(now.Date,now.Offset);_source.Request(midnight.ToUniversalTime());_pending=true;_nextRequest=Time.unscaledTime+30;
#endif
        }
        private void Update()
        {
            if(_source==null||_session.Core.View==null)return;
            if(_pending&&_source.TryTake(out var steps,out var from))
            {
                _pending=false;var now=DateTimeOffset.Now;
                var day=Day(from.ToOffset(now.Offset));
                var accepted=_session.Core.AcceptSteps(day,steps,Day(now),ProductionVillageSession.UtcNow);
                Status=accepted?_session.Core.Notice:_session.Core.Error.Length>0?_session.Core.Error:_session.Core.Notice;
            }
            if(_pending&&Time.unscaledTime>=_nextRequest){_pending=false;Status="歩数を取得できませんでした。権限を確認して再試行してください";}
#if UNITY_IOS && !UNITY_EDITOR
            if(!_pending&&Time.unscaledTime>=_nextRequest)Request();
#endif
        }
        private void OnApplicationPause(bool paused){if(!paused){_pending=false;Request();}}
    }
}
