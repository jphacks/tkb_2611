using System;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.HealthFactory;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.HealthFactoryPrototype
{
    /// <summary>Clock and input adapter for this copied scene only. Never opens a product store.</summary>
    public sealed class HealthFactoryDriver : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Font _font;
        [SerializeField] private Transform[] _facilities;
        [SerializeField] private Transform _plot;
        private HealthFactoryPresenter _presenter;
        private HealthFactoryEncounters _encounters;
        private IStepSource _native;
        private DateTimeOffset _queryFrom;
        private int _queryDay;
        private bool _querying, _suspended;
        private float _queryAt, _nextRefresh, _nextQuery, _resetConfirmUntil;
        private double _elapsedMs;
        private int _selected;
        public HealthFactoryFacade Factory {get;private set;}
        public bool Paused {get;private set;}=true;
        public string Message {get;private set;}="配置の予測を比較し、開発用の累計歩数を受領して再開してください";
        public string StepStatus {get;private set;}="実歩数：iOS専用・未接続（偽入力とは別）";
        public string EncounterStatus=>_encounters?.Status??"すれ違い：準備中";
        public bool DevelopmentInputs=>Application.isEditor||Debug.isDebugBuild;
        public int Selected=>_selected;
        public string RecordDay=>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        public int CurrentAppDay=>Today;
        private static int RecordToday=>ProjectPenguin.Domain.Clock.JstCalendar.DayNumber(DateTimeOffset.UtcNow);
        private static int Today=>AppCalendar.AppDay(DateTimeOffset.UtcNow);
        private void Start()
        {
            Factory=new HealthFactoryFacade();
            _encounters=new HealthFactoryEncounters(EncounterRadios.Create(),()=>(long)(Time.realtimeSinceStartupAsDouble*1000),
                id=>Apply(Factory.GrantPenguin(id)),message=>{Message=message;Refresh();});
            _presenter=new HealthFactoryPresenter(transform,_camera,_font,_facilities,_plot,this);
#if UNITY_IOS && !UNITY_EDITOR
            _native=StepSources.Create();RequestNativeSteps();
#endif
            Refresh();
        }
        private void Update()
        {
            if(!_suspended&&!Paused)
            {
                _elapsedMs+=Math.Min(Time.unscaledDeltaTime,.5f)*1000;
                Factory.AdvanceTo((long)_elapsedMs);
            }
            PollNative();
            if(!_suspended)_encounters.Poll(Today);
            if(Time.unscaledTime>=_nextRefresh){Refresh();_nextRefresh=Time.unscaledTime+.1f;}
        }
        public void Apply(HealthFactoryResult result){Message=result.Reason;Refresh();}
        public void TogglePause(){Paused=!Paused;Message=Paused?"時計を停止しました。予測・配置編集は無料です":"時計を再開しました";Refresh();}
        public void Select(int id){_selected=id;Message=$"施設{id+1}を選択。方向ボタンで2マスずつ移動できます";Refresh();}
        public void MoveSelected(int x,int y)
        {var n=Factory.CreateView().Nodes[_selected];Apply(Factory.Move(_selected,n.X+x,n.Y+y));}
        public void SetLayout(bool compact)=>Apply(Factory.SetLayout(compact));
        public void ToggleRoute(int route){var v=Factory.CreateView();Apply(Factory.SetRoute(route,!v.Routes[route]));}
        public void Build()=>Apply(Factory.BuildBuffer());
        public void Upgrade()=>Apply(Factory.Upgrade());
        public void Expand()=>Apply(Factory.Expand());
        public void FakeSteps(int cumulative)
        {if(DevelopmentInputs)Apply(Factory.AcceptSteps(Today,cumulative,Today));}
        public void FakeWater()
        {if(DevelopmentInputs)Apply(Factory.AcceptWater("fixture-water-"+RecordToday,RecordToday,RecordToday,true));}
        public void FakeScreen()
        {if(DevelopmentInputs)Apply(Factory.AcceptScreen("fixture-screen-"+(RecordToday-1),RecordToday-1,240,RecordToday,true,true));}
        public void TogglePassive()
        {if(DevelopmentInputs){var v=Factory.CreateView();Apply(Factory.SetPenguinFixture(!v.PassiveFixture,v.AssignedFixture));}}
        public void ToggleAssignment()
        {if(DevelopmentInputs){var v=Factory.CreateView();Apply(Factory.SetPenguinFixture(v.PassiveFixture,!v.AssignedFixture));}}
        public void FakeEncounter()
        {if(DevelopmentInputs)_=_encounters.FakeEncounterAsync();}
        public void RestartTrial()
        {
            if(!DevelopmentInputs)return;
            if(Time.unscaledTime>_resetConfirmUntil)
            {_resetConfirmUntil=Time.unscaledTime+4;Message="試作の台帳も消去します。4秒以内にもう一度押して初期化";Refresh();return;}
            Factory=new HealthFactoryFacade();_elapsedMs=0;Paused=true;_selected=0;_resetConfirmUntil=0;
            Message="開発用セッションを初期化しました。製品保存には接続していません";Refresh();
        }
        public void RequestNativeSteps()
        {
            if(_native==null){StepStatus="実歩数：この環境は未接続。成功0歩ではありません";Refresh();return;}
            if(!_native.IsSupported){StepStatus="実歩数：端末非対応";Refresh();return;}
            if(_querying)return;
            _queryDay=Today;_queryFrom=GameStateSystem.AppDayStartUtc(_queryDay);
            _native.Request(_queryFrom);_querying=true;_queryAt=Time.unscaledTime;
            StepStatus="実歩数：取得中";_nextQuery=Time.unscaledTime+30;Refresh();
        }
        private void PollNative()
        {
            if(_native==null||_suspended)return;
            if(_querying&&_queryDay!=Today){_querying=false;RequestNativeSteps();return;}
            if(_querying&&_native.TryTake(out var steps,out var from))
            {
                _querying=false;
                if(from!=_queryFrom||_queryDay!=Today){StepStatus="実歩数：対象日が違う応答を破棄";return;}
                var result=Factory.AcceptSteps(_queryDay,steps,Today);StepStatus=result.Success?$"実歩数：取得成功 {steps}歩":"実歩数：値の確認が必要";
                Apply(result);
            }
            if(_querying&&Time.unscaledTime-_queryAt>12){_querying=false;StepStatus="実歩数：応答なし／取得失敗（0歩とは異なります）";}
            if(!_querying&&Time.unscaledTime>=_nextQuery)RequestNativeSteps();
        }
        private void OnApplicationPause(bool paused)
        {
            _suspended=paused;if(paused)_encounters?.Suspend();
            if(!paused&&Factory!=null&&_native!=null){_querying=false;RequestNativeSteps();}
        }
        private void OnDestroy()=>_encounters?.Suspend();
        private void Refresh(){if(_presenter!=null&&Factory!=null)_presenter.Render(Factory.CreateView(),Factory.Quote(Today));}
    }
}
