using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Raid;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>QR entry, no scene edits required. Only explicit raidDemo=1 uses the local demo.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RaidScreenPresenter : MonoBehaviour
    {
        private static bool _dismissed;
        private static MockRaidClient _demo;
        public static bool DeferOnboarding => !_dismissed && RaidLaunch.Parse(EntryUrl).Requested;
#if UNITY_EDITOR
        private static string _editorSaveDirectory;
        [SerializeField] private bool _editorLocalDemo;
        [SerializeField] private bool _editorVenue;
#endif
        private static string EntryUrl
        {
            get
            {
#if UNITY_EDITOR
                foreach (var presenter in FindObjectsByType<RaidScreenPresenter>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                    if (presenter._editorLocalDemo)
                        return "https://project-penguin-nu.vercel.app/?raid=demo&raidDemo=1" + (presenter._editorVenue ? "&venue=1" : "");
#endif
                return Application.absoluteURL;
            }
        }
        private ScreenNavigator _navigator;
        private VisualElement _screen;
        private RaidCoordinator _coordinator;
        private ClassroomRaidStartup<ClassroomRaidOnlineScreen> _classroomStartup;
        private ClassroomRaidOnlineScreen _online => _classroomStartup?.Online;
        private CancellationTokenSource _life;
        private RaidLaunch _launch;
        private Button _action, _retry, _close, _source;
        private bool _open, _applicationVisible=true;
        private string _message;
        private World.RaidRescuePresenter _rescue;
        private Button _motion;
        private bool _reducedMotion;
        private ScrollView _sheet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _dismissed=false; _demo=null; SceneManager.sceneLoaded-=OnSceneLoaded;
#if UNITY_EDITOR
            _editorSaveDirectory=null;
#endif
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        { SceneManager.sceneLoaded+=OnSceneLoaded; Install(); }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();
        private static void Install()
        {
            if (!RaidLaunch.Parse(EntryUrl).Requested || _dismissed) return;
            foreach(var nav in FindObjectsByType<ScreenNavigator>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(nav.GetComponent<RaidScreenPresenter>()==null) nav.gameObject.AddComponent<RaidScreenPresenter>();
        }
        private void Start()
        {
            _launch=RaidLaunch.Parse(EntryUrl);
            _navigator=GetComponent<ScreenNavigator>();
            _screen=GetComponent<UIDocument>().rootVisualElement.Q(UiNames.App.RaidScreen);
            if (_navigator==null || _screen==null || !_launch.Requested) { enabled=false; return; }
            _action=_screen.Q<Button>(UiNames.Raid.Action); _retry=_screen.Q<Button>(UiNames.Raid.Retry);
            _close=_screen.Q<Button>(UiNames.Raid.Close); _source=_screen.Q<Button>(UiNames.Raid.Source);
            _action.clicked+=Action; _retry.clicked+=Retry; _close.clicked+=Close; _source.clicked+=OpenSource;
            _motion=_screen.Q<Button>(UiNames.Raid.ReducedMotion);
            _motion.clicked+=ToggleMotion;
            _screen.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _sheet=_screen.Q<ScrollView>();
            _sheet.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _navigator.ScreenShown+=OnScreen;
            _life=new CancellationTokenSource(); _open=true;
            _navigator.Show(AppScreen.Raid);
            if (!_launch.Valid)
            { _message="参加用URLが正しくありません。会場のQRを読み直してください。"; Render(); return; }
            _screen.EnableInClassList(UiClasses.RaidVenue,_launch.Venue);
            _screen.Q<Label>(UiNames.Raid.Mode).text=_launch.Demo?"ローカル模擬・他の端末とは共有されません":_launch.Venue?"会場の救助状況":"みんなでペンギンを助けよう";
            var stage=Resources.Load<GameObject>("RaidRescueStage");
            if (stage!=null)
            {
                _rescue=Instantiate(stage).GetComponent<World.RaidRescuePresenter>();
                _rescue?.Bind(_screen.Q<Image>(UiNames.Raid.RescueView));
            }
            if(_launch.Classroom)
            {
                _classroomStartup=new ClassroomRaidStartup<ClassroomRaidOnlineScreen>(
                    BackendRuntime.ClassroomConfigAsync,config=> {
                    var codec=new ClassroomJsonCodec();
                    var classroomStore=new ClassroomParticipationStore(Application.persistentDataPath,codec.Serialize,codec.Deserialize<ClassroomParticipation>);
                    var client=new HttpClassroomRaidClient(config.ApiBaseUrl,new UnityWebRequestTransport(),codec);
                    var coordinator=new ClassroomRaidCoordinator(client,classroomStore);
                    try { return new ClassroomRaidOnlineScreen(_screen,_rescue,coordinator,_launch,_life.Token,UpdateLandscapeHeight); }
                    catch { coordinator.Dispose(); throw; }
                },(screen,ct)=>screen.OpenAsync(ct),(screen,visible)=>screen.SetVisible(visible));
                _classroomStartup.SetVisible(_open && _applicationVisible);
                Execute(()=>_classroomStartup.EnsureAsync());
                return;
            }
            var saveDirectory=Application.persistentDataPath;
#if UNITY_EDITOR
            if (_editorLocalDemo)
                saveDirectory=_editorSaveDirectory ?? (_editorSaveDirectory=Path.Combine(Application.temporaryCachePath,"RaidEditorPreview",Guid.NewGuid().ToString("N")));
#endif
            var store=new RaidParticipationStore(saveDirectory,
                s=>JsonUtility.ToJson(s),s=>RaidJson.Deserialize<RaidParticipation>(s),_launch.Demo);
            IRaidClient client;
            if (_launch.Demo) client=_demo ?? (_demo=new MockRaidClient());
            else
            {
                // The current deployment serves /api on the same origin. Later inject the agreed runtime configuration here.
                var origin=new Uri(EntryUrl).GetLeftPart(UriPartial.Authority);
                client=new HttpRaidClient(origin+"/api/v1/raids");
            }
            _coordinator=new RaidCoordinator(client,store);
            _coordinator.Changed+=Render;
            Execute(()=>_coordinator.OpenAsync(_launch.EventId,_launch.Venue,_life.Token));
        }
        private void Update()
        {
            if(_open && _applicationVisible) _online?.Tick();
            if(_open && _applicationVisible && _coordinator?.ShouldPoll(DateTimeOffset.UtcNow)==true)
                Execute(()=>_coordinator.PollAsync(_life.Token));
        }
        private void OnApplicationPause(bool paused) { _applicationVisible=!paused; UpdateVisibility(); }
        private void OnApplicationFocus(bool focus) { _applicationVisible=focus; UpdateVisibility(); }
        private void UpdateVisibility()
        {
            var visible=_open && _applicationVisible;
            _coordinator?.SetVisible(visible); _classroomStartup?.SetVisible(visible); _rescue?.SetVisible(visible);
            if(visible && _classroomStartup!=null) Execute(()=>_classroomStartup.EnsureAsync());
        }
        private void OnScreen(AppScreen screen,AppScreen previous)
        {
            _open=screen==AppScreen.Raid; UpdateVisibility();
            if(previous==AppScreen.Raid && !_open && !_dismissed)
            {
                _dismissed=true; _classroomStartup?.Dispose(); _life?.Cancel();
                GetComponent<OnboardingPresenter>()?.ShowIfNeeded();
            }
        }
        private async void Execute(Func<Task> operation)
        {
            _message=null;
            try { var task=operation(); Render(); await task; }
            catch(OperationCanceledException) { return; }
            catch(RaidClientException e) { _message=e.Message; }
            catch(IOException) { _message="参加情報を保存できません。空き容量やブラウザの保存設定を確認してください。"; }
            catch(Exception) { _message="レイドを開けませんでした。もう一度お試しください。"; }
            if(this!=null && _open) Render();
        }
        private void Action()
        {
            if(_online!=null) { _online.Action(); return; }
            if(_coordinator==null || _coordinator.Busy) return;
            if(!_coordinator.Joined) Execute(()=>_coordinator.JoinAsync(_life.Token));
            else if(_coordinator.Personal?.pending_answer!=null) Execute(()=>_coordinator.ConfirmAsync(_life.Token));
        }
        private void Retry()
        {
            if(_classroomStartup!=null && (_online==null || _classroomStartup.State==ClassroomStartupState.EventOpenFailed ||
                _classroomStartup.State==ClassroomStartupState.Suspended))
            { Execute(()=>_classroomStartup.EnsureAsync(_online==null)); return; }
            if(_online!=null) { _online.Retry(); return; }
            if(_coordinator==null || _coordinator.Busy) return;
            if(_coordinator.Session==null)
                Execute(()=>_coordinator.OpenAsync(_launch.EventId,_launch.Venue,_life.Token));
            else Execute(()=>_coordinator.PollAsync(_life.Token));
        }
        private void Close()
        {
            _dismissed=true; _open=false;
            _coordinator?.SetVisible(false); _classroomStartup?.Dispose(); _rescue?.SetVisible(false); _life?.Cancel();
            _navigator.Show(AppScreen.Home);
            GetComponent<OnboardingPresenter>()?.ShowIfNeeded();
        }
        private void OpenSource()
        {
            if(_online!=null) { _online.OpenSource(); return; }
            var source=_coordinator?.Personal?.pending_answer?.source_url;
            // Source content is untrusted server data; only launch the documented primary source host.
            if(Uri.TryCreate(source,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.Host=="www.enecho.meti.go.jp") Application.OpenURL(uri.AbsoluteUri);
        }
        private void Render()
        {
            if(_screen==null || !_open || !_applicationVisible) return;
            if(_online!=null) { _online.SetMessage(_message); _online.Render(); return; }
            if(_launch?.Classroom==true)
            {
                _screen.Q<Label>(UiNames.Raid.Mode).text="教室レイド";
                var starting=_classroomStartup?.Busy==true;
                _screen.Q<Label>(UiNames.Raid.Status).text=starting?"接続の準備を確認しています。":"接続設定を確認してください。";
                _screen.Q<Label>(UiNames.Raid.Error).text=_message??_classroomStartup?.ConfigResult?.Message??"";
                _screen.Q(UiNames.Raid.QuestionCard).style.display=DisplayStyle.None;
                _screen.Q(UiNames.Raid.ExplanationCard).style.display=DisplayStyle.None;
                _action.style.display=DisplayStyle.None; _retry.style.display=DisplayStyle.Flex;
                _retry.text="接続設定を再取得"; _retry.SetEnabled(!starting);
                return;
            }
            var c=_coordinator; var progress=c?.Session?.Progress; var personal=c?.Personal;
            var active=progress?.Active==true; var busy=c?.Busy==true;
            _screen.Q<Label>(UiNames.Raid.Error).text=_message ?? c?.Error?.Message ?? "";
            _retry.style.display=(!string.IsNullOrEmpty(_message)||c?.Error!=null)?DisplayStyle.Flex:DisplayStyle.None;
            _retry.SetEnabled(!busy);
            _screen.Q<Label>(UiNames.Raid.Status).text=progress==null?"レイドに接続しています…":progress.status=="scheduled"?"もうすぐ始まります":progress.status=="ended"?"受付は終了しました。みんなの救助結果です。":progress.Ratio>=1?"救出成功！ 受付時間内は引き続き参加できます。":"あなたの回答が、みんなの氷の道になります。";
            if(progress!=null)
            {
                _screen.Q<Label>(UiNames.Raid.Title).text=progress.title;
                _screen.Q<ProgressBar>(UiNames.Raid.Progress).value=(float)(progress.Ratio*100);
                _screen.Q<Label>(UiNames.Raid.ProgressText).text=$"{progress.total_points:N0} / {progress.target_points:N0} ポイント";
                var milestones=_screen.Q(UiNames.Raid.Milestones); milestones.Clear();
                for(var i=1;i<=4;i++) { var label=new Label(progress.Ratio>=i/4.0?$"{i*25}% 到達":$"{i*25}%"); label.AddToClassList("pp-badge"); label.AddToClassList("raid__milestone"); milestones.Add(label); }
                var reached=c.Session.TakeNewMilestones();
                if(reached.Length>0) _screen.Q<Label>(UiNames.Raid.RescueSpeech).text=reached[reached.Length-1]==100?"みんなの道がつながった！ ペンギンを救出できたよ！":"氷の道が伸びたよ。救助まであと少し！";
                _rescue?.Apply(progress,true);
            }
            var venue=_launch?.Venue==true;
            _screen.Q<Label>(UiNames.Raid.VenueUrl).text=venue && _launch.Valid?"参加はこちら："+new Uri(EntryUrl).GetLeftPart(UriPartial.Path)+"?raid="+_launch.EventId+(_launch.Demo?"&raidDemo=1":""):"";
            _screen.Q<Label>(UiNames.Raid.Personal).text=venue?"会場のQRから参加しよう":personal==null?"3問のエコクイズで、ペンギンを助けよう！":$"あなたの貢献：{personal.personal_points} ポイント"+(c.SouvenirReceived?" ・救助の記念を受け取りました":personal.completed?" ・3問終了、おつかれさま！":"");
            var question=personal?.question;
            _screen.Q(UiNames.Raid.QuestionCard).style.display=!venue&&question!=null?DisplayStyle.Flex:DisplayStyle.None;
            var options=_screen.Q(UiNames.Raid.Options); options.Clear();
            if(question!=null && !venue)
            {
                _screen.Q<Label>(UiNames.Raid.QuestionNumber).text=$"第{personal.answered_count+1}問 / 3問";
                _screen.Q<Label>(UiNames.Raid.Question).text=question.prompt;
                foreach(var option in question.options)
                {
                    var id=option.id;
                    var button=new Button(()=>Execute(()=>c.SubmitAsync(id,_life.Token))) { text=option.text };
                    button.AddToClassList(UiClasses.Button); button.AddToClassList(UiClasses.ButtonBlock); button.AddToClassList(UiClasses.RaidOption);
                    button.SetEnabled(active&&!busy); options.Add(button);
                }
            }
            var answer=personal?.pending_answer;
            var reading=!venue && (question!=null || answer!=null);
            _screen.EnableInClassList(UiClasses.RaidReading,reading);
            _rescue?.SetReading(reading);
            UpdateLandscapeHeight();
            _screen.Q(UiNames.Raid.ExplanationCard).style.display=!venue&&answer!=null?DisplayStyle.Flex:DisplayStyle.None;
            if(answer!=null)
            { _screen.Q<Label>(UiNames.Raid.Correct).text=answer.correct?"正解！":"なるほど、こうするといいんだね";
              _screen.Q<Label>(UiNames.Raid.Explanation).text=answer.explanation; }
            _source.style.display=answer!=null && Uri.TryCreate(answer.source_url,UriKind.Absolute,out var sourceUri) &&
                sourceUri.Scheme=="https" && sourceUri.Host=="www.enecho.meti.go.jp"?DisplayStyle.Flex:DisplayStyle.None;
            var showAction=!venue && c!=null && (!c.Joined || answer!=null);
            _action.style.display=showAction?DisplayStyle.Flex:DisplayStyle.None;
            _action.text=answer!=null?$"次へ ・救助に +{answer.points_pending} ポイント":"参加する";
            _action.SetEnabled(active&&!busy);
        }

        private void ToggleMotion()
        {
            _reducedMotion=!_reducedMotion;
            _motion.text=_reducedMotion?"通常の動きに戻す":"動きを控えめに";
            _rescue?.SetReducedMotion(_reducedMotion);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => UpdateLandscapeHeight();

        private void UpdateLandscapeHeight()
        {
            var height=_screen.contentRect.height;
            if(height<=0 || float.IsNaN(height)) return;
            var reading=_screen.ClassListContains(UiClasses.RaidReading);
            var image=_screen.Q<Image>(UiNames.Raid.RescueView);
            var top=Mathf.Max(0,image.worldBound.y-_screen.worldBound.y);
            var tail=Mathf.Max(0,_sheet.worldBound.y-image.worldBound.yMax);
            image.style.height=(float)Math.Floor(RaidRescueFraming.LandscapeHeight(height,top+tail,
                _launch.Venue?0.6:reading?0.34:0.48,height*(_launch.Venue?0.18:0.38),720));
        }
        private void OnDestroy()
        {
            _classroomStartup?.Dispose(); _life?.Cancel(); _coordinator?.Dispose(); _life?.Dispose();
            if (_rescue!=null) Destroy(_rescue.gameObject);
            if (_motion!=null) _motion.clicked-=ToggleMotion;
            _screen?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _sheet?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if(_coordinator!=null) _coordinator.Changed-=Render;
            if(_navigator!=null) _navigator.ScreenShown-=OnScreen;
            if(_action!=null) { _action.clicked-=Action; _retry.clicked-=Retry; _close.clicked-=Close; _source.clicked-=OpenSource; }
        }
    }
}
