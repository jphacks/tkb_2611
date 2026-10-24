using System;
using ProjectPenguin.Domain.Raid;
using ProjectPenguin.Presentation.World;
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Systems;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>Explicit standalone prototype. No navigator, game state, API or persistent storage.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClassroomRaidPreview : MonoBehaviour
    {
        private const string ReducedMotionName = "classroomReducedMotion";
        [SerializeField] private StyleSheet _style;
        [SerializeField] private Font _font;
        private VisualElement _root, _settings, _personal, _question, _explanation, _resetConfirm;
        private Label _status, _points, _personalStats, _prompt, _result, _explanationText, _feedback, _milestones, _successMessage, _finishNote;
        private ScrollView _scroll;
        private ProgressBar _progress;
        private IntegerField _participants, _target;
        private Button _next, _watch, _resume, _confirm, _start, _simulateOne, _simulateFive;
        private Image _image;
        private RaidRescuePresenter _rescue;
        private bool _venue, _compact, _reducedMotion;
        private string _renderedQuestion, _answerId;
        public ClassroomRaidSession Session { get; } = new ClassroomRaidSession();

        private void Start()
        {
            Session.Reset(30,180);
            Build();
            var prefab=Resources.Load<GameObject>("RaidRescueStage");
            if(prefab!=null) { _rescue=Instantiate(prefab).GetComponent<RaidRescuePresenter>(); _rescue.Bind(_image); }
            else _feedback.text="救助Prefabが見つかりません。進行確認はできます。";
            Render();
        }

        private void Build()
        {
            _root=GetComponent<UIDocument>().rootVisualElement;
            if(_style!=null) _root.styleSheets.Add(_style);
            if(_font!=null) _root.style.unityFont=_font;
            _root.AddToClassList("classroom");
            var page=new VisualElement(); page.AddToClassList("page"); _root.Add(page);
            Text(page,"LOCAL PROTOTYPE  /  ローカル模擬・他端末との同期なし", "badge");
            Text(page,"クイズ救助レイド", "title");
            Text(page,"吹雪の雪山へ、みんなで氷橋を届けよう。", "subtitle");
            var tabs=Box(page,"row");
            AddButton(tabs,"参加者の画面","classroomParticipant",()=>{_venue=false;Render();});
            AddButton(tabs,"教室の共有表示","classroomVenue",()=>{_venue=true;Render();});

            var shared=Box(page,UiClasses.ClassroomLandscape);
            _status=Text(shared,"", "heading"); _status.name="classroomStatus";
            _successMessage=Text(shared,"", "success-message"); _successMessage.name="classroomSuccessMessage";
            _image=new Image {name="classroomRescueView"}; _image.AddToClassList("rescue"); shared.Add(_image);
            _points=Text(shared,"", "points"); _points.name="classroomPoints";
            _progress=new ProgressBar {name="classroomProgress",lowValue=0,highValue=100}; shared.Add(_progress);
            _milestones=Text(shared,"", "subtitle");
            Text(shared,"雪山のペンギンへ、手前から奥に氷橋をつなぎます。", "body");
            _finishNote=Text(shared,"目標到達後は共同ポイントを増やさず、途中の問題も解説まで読めます。", "note");

            _scroll=new ScrollView(ScrollViewMode.Vertical); _scroll.AddToClassList("page-scroll"); page.Add(_scroll);
            _scroll.RegisterCallback<GeometryChangedEvent>(OnSheetGeometry);
            var sheet=Box(_scroll,UiClasses.ClassroomSheet);
            sheet.Add(_successMessage); sheet.Add(_finishNote);
            _settings=Box(sheet,"card");
            Text(_settings,"先生役：開催を準備", "heading");
            Text(_settings,"このPCだけの開催設定です。人数は模擬値です。", "body");
            _participants=new IntegerField("参加人数（1〜300人）") {name="classroomParticipants",value=30,isDelayed=true};
            _target=new IntegerField("目標（1〜100,000点）") {name="classroomTarget",value=180,isDelayed=true};
            _settings.Add(_participants); _settings.Add(_target);
            AddButton(_settings,"人数から仮のおすすめを入れる","classroomRecommend",()=>{
                _participants.value=Mathf.Clamp(_participants.value,1,300); _target.value=_participants.value*6;
                _feedback.text="仮のおすすめを反映しました。先生役が自由に調整できます。";
            });
            Text(_settings,"仮のおすすめ：人数 × 6点。開催方式や計算式は未確定です。", "note");
            _start=AddButton(_settings,"先生役：レイドを開始","classroomStart",()=>{
                if(_participants.value<1 || _participants.value>300 || _target.value<1 || _target.value>100000)
                { _feedback.text="人数は1〜300人、目標は1〜100,000点で入力してください。"; return; }
                Session.Reset(_participants.value,_target.value); _rescue?.ResetProgress(); Session.Start();
                _feedback.text="開始しました。参加者は好きなタイミングで問題を始められます。"; Render();
            }); _start.AddToClassList("primary");

            _personal=Box(sheet,"card");
            _personalStats=Text(_personal,"", "heading"); _personalStats.name="classroomPersonal";
            _question=Box(_personal,"question");
            _prompt=Text(_question,"", "heading"); _prompt.name="classroomPrompt";
            var options=Box(_question,"options"); options.name="classroomOptions";
            _explanation=Box(_personal,"explanation");
            _result=Text(_explanation,"", "heading");
            _explanationText=Text(_explanation,"", "body"); _explanationText.name="classroomExplanation";
            _confirm=AddButton(_explanation,"", "classroomConfirm",()=>{
                var awarded=Session.Confirm(_answerId);
                _feedback.text=awarded>0?$"解説を確認して、救助に {awarded} 点貢献しました。":"最後の解説まで読んでくれてありがとう！クラスの救助は成功しました。";
                Render();
                if(Session.Phase==ClassroomRaidPhase.Succeeded) _scroll.scrollOffset=Vector2.zero;
                else FocusPersonal();
            }); _confirm.AddToClassList("primary");
            _next=AddButton(_personal,"問題を始める","classroomNext",()=>{Session.BeginQuestion();_feedback.text="";Render();FocusPersonal();});
            _next.AddToClassList("primary");
            _watch=AddButton(_personal,"回答を休んで、みんなの進捗を見る","classroomWatch",()=>{Session.Watch();Render();});
            _resume=AddButton(_personal,"回答に戻る","classroomResume",()=>{Session.Resume();Render();FocusPersonal();});
            Text(_personal,$"正解2点・不正解1点。解説確認で貢献。{Session.QuestionCount}問を一巡してから、順番を変えて繰り返します。", "note");

            _feedback=Text(sheet,"", "feedback"); _feedback.name="classroomFeedback";
            var motion=AddButton(sheet,"動きを控えめに",ReducedMotionName,null);
            motion.clicked+=()=>{
                _reducedMotion=!_reducedMotion; _rescue?.SetReducedMotion(_reducedMotion);
                motion.text=_reducedMotion?"通常の動きに戻す":"動きを控えめに";
            };
            var tools=new Foldout {text="試作専用の確認操作（他参加者を模擬）",value=false,name="classroomTools"}; sheet.Add(tools);
            Text(tools,"実際の参加者や通信はありません。共有点だけを手動で進めます。", "note");
            var simRow=Box(tools,"row");
            _simulateOne=AddButton(simRow,"他参加者 +1点","classroomSimulateOne",()=>{Session.AddSimulatedPoints(1);Render();});
            _simulateFive=AddButton(simRow,"他参加者 +5点","classroomSimulateFive",()=>{Session.AddSimulatedPoints(5);Render();});
            AddButton(tools,"開催をリセット…","classroomReset",()=>{Show(_resetConfirm,true);});
            _resetConfirm=Box(tools,"explanation");
            Text(_resetConfirm,"この試作の回答・貢献を消して、準備に戻りますか？", "body");
            var resetRow=Box(_resetConfirm,"row");
            AddButton(resetRow,"リセットして準備へ","classroomResetYes",()=>{
                Session.Reset(Mathf.Clamp(_participants.value,1,300),Mathf.Clamp(_target.value,1,100000));
                _rescue?.ResetProgress();
                _renderedQuestion=null; _answerId=null; _feedback.text="新しい開催を準備できます。";
                Show(_resetConfirm,false); Render();
            });
            AddButton(resetRow,"やめる","classroomResetNo",()=>Show(_resetConfirm,false)); Show(_resetConfirm,false);
            Text(sheet,"試作：保存なし・AI生成なし・通常の島やエコの実績とは別です。", "note");
            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void Render()
        {
            var waiting=Session.Phase==ClassroomRaidPhase.Waiting;
            var success=Session.Phase==ClassroomRaidPhase.Succeeded;
            _root.EnableInClassList("venue",_venue);
            _status.text=waiting?"開催待ち — 先生役の開始を待っています":success?"救助成功！ 氷橋がつながった！":"雪山のペンギンへ、氷橋を届けよう";
            _successMessage.text=Session.AnsweredCount>0?"あなたもクイズに挑戦してくれてありがとう！\n氷橋がつながり、ペンギンがこちらへ帰ってきます。":"みんなで氷橋をつなぎました！\n解説を読んで、明日できるエコを見つけよう。";
            Show(_successMessage,success); Show(_finishNote,success);
            _points.text=$"{Session.TotalPoints:N0} / {Session.Target:N0} 救助ポイント";
            var ratio=Session.Target==0?0:(float)Session.TotalPoints/Session.Target;
            _progress.value=ratio*100;
            _milestones.text=$"{Mathf.FloorToInt(ratio*100)}%  ・  "+string.Join("   ",new[]{25,50,75,100}.ConvertAllMarks(ratio));
            var reading=!waiting&&!_venue&&!Session.IsWatching&&Session.CurrentQuestion!=null;
            _root.EnableInClassList(UiClasses.ClassroomReading,reading);
            ResizeLandscape(); _rescue?.SetReading(reading);
            _rescue?.Apply(new RaidProgress {event_id="classroom-local",content_version="classroom-preview-12",revision=Session.Revision,
                total_points=Session.TotalPoints,target_points=Session.Target,status=success?"ended":waiting?"scheduled":"active",local_demo=true}, !waiting);
            Show(_settings,waiting); Show(_personal,!waiting&&!_venue);
            _personalStats.text=Session.IsWatching?"みんなの進捗を見ています":$"あなたの貢献：{Session.PersonalPoints} 点  ・  回答 {Session.AnsweredCount} 問";
            var question=Session.CurrentQuestion; var answer=Session.PendingAnswer;
            Show(_question,question!=null&&answer==null&&!Session.IsWatching);
            Show(_explanation,answer!=null&&!Session.IsWatching);
            Show(_next,!success&&question==null&&!Session.IsWatching);
            _next.text=Session.AnsweredCount==0?"問題を始める":"次の問題を始める";
            Show(_watch,!Session.IsWatching); Show(_resume,Session.IsWatching);
            _resume.text=success&&question==null?"自分の結果を見る":"回答に戻る";
            if(question!=null)
            {
                _prompt.text=$"第{Session.AnsweredCount+(answer==null?1:0)}問  /  問数制限なし\n{question.Prompt}";
                // Rebuild only for a new occurrence, including repeated question IDs.
                var occurrence=$"{question.Id}:{Session.AnsweredCount}:{(answer==null)}";
                if(_renderedQuestion!=occurrence)
                {
                    _renderedQuestion=occurrence;
                    var options=_root.Q("classroomOptions"); options.Clear();
                    for(var i=0;i<question.Options.Length;i++)
                    { var index=i; AddButton(options,question.Options[i],"classroomOption"+i,()=>{Session.Answer(index);Render();FocusPersonal();}); }
                }
            }
            if(answer!=null)
            {
                _answerId=answer.Id;
                _result.text=answer.Correct?"正解！":"解説で、次に活かそう";
                _explanationText.text=answer.Explanation;
                _confirm.text=success?"最後の解説を読んで、救助をお祝いする":$"解説を確認して貢献 +{Math.Min(answer.Points,Session.Target-Session.TotalPoints)}点";
            }
            if(success&&question==null&&!Session.IsWatching)
                _personalStats.text=$"救助成功！ あなたは {Session.AnsweredCount} 問に回答し、{Session.PersonalPoints} 点貢献しました。";
            _simulateOne.SetEnabled(!waiting&&!success); _simulateFive.SetEnabled(!waiting&&!success);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var compact=evt.newRect.width<640;
            if(compact==_compact) { ResizeLandscape(); return; }
            _compact=compact; _root.EnableInClassList("compact",compact); Render();
        }

        private void ResizeLandscape()
        {
            var height=_root.contentRect.height;
            if(height<=0 || float.IsNaN(height)) return;
            var reading=_root.ClassListContains(UiClasses.ClassroomReading);
            var top=Mathf.Max(0,_image.worldBound.y-_root.worldBound.y);
            var tail=Mathf.Max(0,_scroll.worldBound.y-_image.worldBound.yMax);
            var minimumSheet=_venue?height*0.18:Mathf.Min(height*0.38f,280);
            var imageHeight=(float)RaidRescueFraming.LandscapeHeight(height,top+tail,
                _venue?0.52:reading?0.32:0.4,minimumSheet,_compact?340:520);
            _image.style.height=Mathf.Floor(imageHeight);
        }

        private void OnSheetGeometry(GeometryChangedEvent evt) => ResizeLandscape();

        private void FocusPersonal() => _personal.schedule.Execute(()=>_scroll.ScrollTo(_personal));

        private static Label Text(VisualElement parent,string text,string css)
        { var label=new Label(text); label.AddToClassList(css); parent.Add(label); return label; }
        private static VisualElement Box(VisualElement parent,string css)
        { var box=new VisualElement(); box.AddToClassList(css); parent.Add(box); return box; }
        private static Button AddButton(VisualElement parent,string text,string name,Action action)
        { var button=new Button(action) {text=text,name=name}; parent.Add(button); return button; }
        private static void Show(VisualElement element,bool visible) => element.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        private void OnEnable() => _rescue?.SetVisible(true);
        private void OnDisable() => _rescue?.SetVisible(false);
        private void OnApplicationPause(bool paused) => _rescue?.SetVisible(!paused);
        private void OnDestroy()
        {
            _root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _scroll?.UnregisterCallback<GeometryChangedEvent>(OnSheetGeometry);
            if(_rescue!=null) Destroy(_rescue.gameObject);
        }
    }

    internal static class ClassroomRaidMarks
    {
        internal static string[] ConvertAllMarks(this int[] marks,float ratio)
        { return Array.ConvertAll(marks,mark=>ratio*100>=mark?$"● {mark}%":$"○ {mark}%"); }
    }
    /// <summary>Online screen adapter; the standalone local prototype above keeps its own session.</summary>
    public sealed class ClassroomRaidOnlineScreen : IDisposable
    {
        private readonly VisualElement _screen;
        private readonly RaidRescuePresenter _rescue;
        private readonly ClassroomRaidCoordinator _coordinator;
        private readonly RaidLaunch _launch;
        private readonly CancellationToken _token;
        private readonly Action _rendered;
        private string _message;
        private bool _visible=true,_disposed;
        public ClassroomRaidOnlineScreen(VisualElement screen,RaidRescuePresenter rescue,ClassroomRaidCoordinator coordinator,
            RaidLaunch launch,CancellationToken token,Action rendered)
        {
            _screen=screen; _rescue=rescue; _coordinator=coordinator; _launch=launch; _token=token; _rendered=rendered;
            _coordinator.Changed+=Render;
        }
        public Task OpenAsync() => _coordinator.OpenAsync(_launch.EventId,_launch.Venue,_token);
        public Task OpenAsync(CancellationToken ct) => _coordinator.OpenAsync(_launch.EventId,_launch.Venue,ct);
        public void SetMessage(string message) { _message=message; }
        public void Tick() { if(_visible && _coordinator.ShouldPoll(DateTimeOffset.UtcNow)) Execute(()=>_coordinator.PollAsync(_token)); }
        public void SetVisible(bool visible) { _visible=visible; _coordinator.SetVisible(visible); }
        public void Action()
        {
            if(_coordinator.Busy || _launch.Venue) return;
            if(_coordinator.HasPendingJoin) Execute(()=>_coordinator.RecoverJoinAsync(_token));
            else if(!_coordinator.Joined) Execute(()=>_coordinator.JoinAsync(_token));
            else if(_coordinator.Personal?.pending_answer!=null) Execute(()=>_coordinator.ConfirmAsync(_token));
            else Execute(()=>_coordinator.NextAsync(_token));
        }
        public void Retry() => Execute(()=>_coordinator.Progress==null?OpenAsync():_coordinator.PollAsync(_token));
        private async void Execute(Func<Task> operation)
        {
            if(_disposed || !_visible || _coordinator.Busy) return;
            _message=null;
            try { await operation(); }
            catch(OperationCanceledException) { return; }
            catch(RaidClientException e) { _message=e.Code=="event_closed" && _coordinator.HasPendingJoin?
                "参加を確認できませんでした。受付は終了しています。参加結果を再確認できます。":e.Message; }
            catch(System.IO.IOException) { _message="参加情報を保存できません。ブラウザの保存設定や空き容量を確認してください。"; }
            catch(Exception) { _message="教室レイドに接続できません。再取得してください。"; }
            Render();
        }
        public void OpenSource()
        { var url=_coordinator.Personal?.pending_answer?.source_url; if(ClassroomState.AllowedSource(url)) Application.OpenURL(url); }
        public void Render()
        {
            if(_disposed || !_visible || _token.IsCancellationRequested) return;
            var c=_coordinator; var p=c.Progress; var me=c.Personal; var venue=_launch.Venue; var busy=c.Busy;
            _screen.Q<Label>(UiNames.Raid.Mode).text=p?.local_demo==true?"ローカル模擬API ・本番AWSではありません":venue?"教室の共有表示":"みんなでペンギンを助けよう";
            _screen.Q<Label>(UiNames.Raid.Error).text=_message??c.Error?.Message??"";
            var retry=_screen.Q<Button>(UiNames.Raid.Retry); retry.style.display=DisplayStyle.Flex; retry.text=c.HasPending?"送信状況を確認・同じ内容で再送":"状態を再取得"; retry.SetEnabled(!busy);
            _screen.Q<Label>(UiNames.Raid.Status).text=p==null?"教室レイドを読み込んでいます…":p.phase=="waiting"?"先生の開始を待っています":p.phase=="succeeded"?"救出成功！ 途中の問題は解説まで読めます。":p.phase=="ended"?p.end_reason=="timeout"?"開催時間が終わりました。途中の解説は読めます。":"先生が終了しました。途中の解説は読めます。":"あなたの回答が、みんなの氷の道になります。";
            if(p!=null)
            {
                _screen.Q<Label>(UiNames.Raid.Title).text=p.title;
                _screen.Q<ProgressBar>(UiNames.Raid.Progress).value=(float)(p.Ratio*100);
                _screen.Q<Label>(UiNames.Raid.ProgressText).text=$"{p.total_points:N0} / {p.target_points:N0} ポイント";
                _rescue?.Apply(p.ToVisualProgress(),true);
            }
            _screen.Q<Label>(UiNames.Raid.Personal).text=venue?"会場のQRから参加しよう":me==null?"エコクイズで雪山へ氷橋を届けよう！":$"あなたの貢献：{me.personal_points:N0} ポイント ・{me.answered_count:N0}問回答";
            _screen.Q<Label>(UiNames.Raid.VenueUrl).text=venue?new Uri(Application.absoluteURL).GetLeftPart(UriPartial.Path)+"?classroom="+_launch.EventId:"";
            var q=venue?null:me?.question; var answer=venue?null:me?.pending_answer;
            _screen.Q(UiNames.Raid.QuestionCard).style.display=q!=null?DisplayStyle.Flex:DisplayStyle.None;
            var options=_screen.Q(UiNames.Raid.Options); options.Clear();
            if(q!=null)
            {
                _screen.Q<Label>(UiNames.Raid.QuestionNumber).text=$"第{q.attempt_seq:N0}問";
                _screen.Q<Label>(UiNames.Raid.Question).text=q.prompt;
                foreach(var option in q.options)
                {
                    var id=option.id; var attempt=q.attempt_id;
                    var button=new Button(()=> { if(c.Personal?.question?.attempt_id==attempt) Execute(()=>c.SubmitAsync(id,_token)); }) {text=option.text};
                    button.AddToClassList(UiClasses.Button); button.AddToClassList(UiClasses.ButtonBlock); button.AddToClassList(UiClasses.RaidOption);
                    button.SetEnabled(!busy && !c.HasPending); options.Add(button);
                }
            }
            _screen.Q(UiNames.Raid.ExplanationCard).style.display=answer!=null?DisplayStyle.Flex:DisplayStyle.None;
            if(answer!=null)
            {
                _screen.Q<Label>(UiNames.Raid.Correct).text=answer.correct?"正解！":"なるほど、こうするといいんだね";
                _screen.Q<Label>(UiNames.Raid.Explanation).text=answer.explanation;
            }
            _screen.Q<Button>(UiNames.Raid.Source).style.display=ClassroomState.AllowedSource(answer?.source_url)?DisplayStyle.Flex:DisplayStyle.None;
            var action=_screen.Q<Button>(UiNames.Raid.Action);
            var actions=c.ActionState;
            action.style.display=actions.Visible?DisplayStyle.Flex:DisplayStyle.None;
            action.SetEnabled(actions.Enabled);
            action.text=actions.Text;
            var reading=q!=null || answer!=null;
            _screen.EnableInClassList(UiClasses.RaidReading,reading); _rescue?.SetReading(reading); _rendered?.Invoke();
        }
        public void Dispose() { if(_disposed) return; _disposed=true; _coordinator.Changed-=Render; _coordinator.Dispose(); }
    }
}
