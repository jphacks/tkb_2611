using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Systems
{
    /// <summary>Pure C# coordinator. Store request IDs before sending; never predict shared points.</summary>
    public sealed class RaidCoordinator : IDisposable
    {
        private readonly IRaidClient _client;
        private readonly IRaidParticipationStore _store;
        private readonly SemaphoreSlim _operation = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly Func<DateTimeOffset> _now;
        private readonly Func<double> _jitter;
        private RaidParticipation _state;
        private DateTimeOffset _nextPoll;
        private int _failures;
        private bool _visible = true, _disposed, _venue;
        private bool _personalDirty;
        public RaidCoordinator(IRaidClient client, IRaidParticipationStore store,
            Func<DateTimeOffset> now = null, Func<double> jitter = null)
        {
            _client=client ?? throw new ArgumentNullException(nameof(client));
            _store=store ?? throw new ArgumentNullException(nameof(store));
            _now=now ?? (()=>DateTimeOffset.UtcNow);
            var random=new Random(); _jitter=jitter ?? (()=>random.NextDouble());
        }
        public event Action Changed;
        public RaidSession Session { get; private set; }
        public RaidPersonalState Personal { get; private set; }
        public RaidClientException Error { get; private set; }
        public bool Busy { get; private set; }
        public bool Joined => !string.IsNullOrEmpty(_state?.token);
        public bool Venue => _venue;
        public bool SouvenirReceived => _state?.souvenir_received == true;
        public bool ShouldPoll(DateTimeOffset now) => !_disposed && _visible && !Busy && Session != null &&
            Session.Progress?.status != "ended" && now >= _nextPoll;

        public Task OpenAsync(string eventId, bool venue, CancellationToken ct) => Run(async token => {
            if (Session!=null) throw new InvalidOperationException("Create a new coordinator to open another event.");
            if (!RaidLaunch.ValidEventId(eventId)) throw new RaidClientException("invalid_request","参加用URLを確認してください。");
            _venue=venue;
            _state=venue ? null : _store.Load(eventId);
            if (_state==null || _state.event_id!=eventId || _state.version!=1)
                _state=new RaidParticipation { event_id=eventId };
            if (!string.IsNullOrEmpty(_state.expires_at) &&
                (!DateTimeOffset.TryParse(_state.expires_at,out var expires) || _now()>=expires))
            { _state.token=null; _state.pending_request=null; _state.join_request_id=null; }
            Session=new RaidSession(eventId,_state.seen_milestones);
            Apply(await _client.GetEventAsync(eventId,token));
            token.ThrowIfCancellationRequested();
            if (Joined && !venue) await RefreshPersonal(token);
        }, ct);

        public Task JoinAsync(CancellationToken ct) => Run(async token => {
            RequireActive();
            if (_venue) throw new InvalidOperationException("Venue view cannot join.");
            if (Joined) { await RefreshPersonal(token); return; }
            if (string.IsNullOrEmpty(_state.join_request_id)) _state.join_request_id=Guid.NewGuid().ToString("N");
            _store.Save(_state); // failing persistence must prevent a network side effect
            var p=await _client.JoinAsync(_state.event_id,_state.join_request_id,token);
            token.ThrowIfCancellationRequested();
            if (p==null || string.IsNullOrEmpty(p.token) || string.IsNullOrEmpty(p.expires_at)) throw InvalidResponse();
            ValidatePersonal(p.personal);
            _state.token=p.token; _state.expires_at=p.expires_at;
            Personal=p.personal; _store.Save(_state);
        },ct);

        public Task SubmitAsync(string optionId, CancellationToken ct) => Run(async token => {
            RequireActive(); RequireJoined();
            if (Personal?.pending_answer!=null) return;
            var question=Personal?.question;
            if (question==null) throw new RaidClientException("invalid_question","回答できる問題がありません。");
            if (_state.pending_request==null)
            {
                var found=false;
                foreach(var option in question.options) if (option.id==optionId) found=true;
                if (!found) throw new RaidClientException("invalid_option","選択肢を確認してください。");
                _state.pending_request=new RaidAnswerRequest { request_id=Guid.NewGuid().ToString("N"), question_id=question.id,
                    option_id=optionId, content_version=_state.content_version };
            }
            // A lost response must be retried with the SAME saved payload, even if another button is tapped.
            _store.Save(_state);
            _personalDirty=true;
            var answer=await _client.AnswerAsync(_state.event_id,_state.token,_state.pending_request,token);
            token.ThrowIfCancellationRequested();
            if (answer==null || answer.question_id!=_state.pending_request.question_id || string.IsNullOrEmpty(answer.answer_id)) throw InvalidResponse();
            await RefreshPersonal(token);
        },ct);

        public Task ConfirmAsync(CancellationToken ct) => Run(async token => {
            RequireActive(); RequireJoined();
            var answer=Personal?.pending_answer;
            if (answer==null) return;
            _personalDirty=true;
            var result=await _client.AcknowledgeAsync(_state.event_id,_state.token,answer.answer_id,token);
            token.ThrowIfCancellationRequested();
            if (result==null || result.answer_id!=answer.answer_id) throw InvalidResponse();
            Apply(result.progress);
            await RefreshPersonal(token);
        },ct);

        public Task PollAsync(CancellationToken ct) => Run(async token => {
            if (Session==null) return;
            var progress=await _client.GetEventAsync(_state.event_id,token);
            token.ThrowIfCancellationRequested(); Apply(progress);
            if (Joined && (Personal==null || _personalDirty || _state.pending_request!=null)) await RefreshPersonal(token);
        },ct);

        private async Task RefreshPersonal(CancellationToken ct)
        {
            var personal=await _client.GetMeAsync(_state.event_id,_state.token,ct);
            ct.ThrowIfCancellationRequested(); ValidatePersonal(personal);
            Personal=personal;
            if (personal.pending_answer!=null || personal.completed ||
                (_state.pending_request!=null && personal.question?.id!=_state.pending_request.question_id))
                _state.pending_request=null;
            _store.Save(_state);
            _personalDirty=false;
        }
        private void ValidatePersonal(RaidPersonalState p)
        {
            if (p==null || p.schema_version!=1 || p.event_id!=_state.event_id || p.content_version!=_state.content_version ||
                p.answered_count<0 || p.answered_count>3 || p.personal_points<0 || p.personal_points>6 ||
                (p.completed && (p.answered_count!=3 || p.question!=null || p.pending_answer!=null)) ||
                (!p.completed && ((p.question==null)==(p.pending_answer==null))) ||
                (p.question!=null && (p.answered_count>=3 || !RaidLaunch.ValidEventId(p.question.id) ||
                    string.IsNullOrEmpty(p.question.prompt) || p.question.options==null || p.question.options.Length!=3)) ||
                (p.pending_answer!=null && (p.answered_count==0 || !RaidLaunch.ValidEventId(p.pending_answer.answer_id) ||
                    !RaidLaunch.ValidEventId(p.pending_answer.question_id) || p.pending_answer.acknowledged ||
                    string.IsNullOrEmpty(p.pending_answer.explanation) ||
                    p.pending_answer.points_pending!=(p.pending_answer.correct?2:1))))
                throw InvalidResponse();
            if(p.question!=null)
            {
                var ids=new System.Collections.Generic.HashSet<string>();
                foreach(var option in p.question.options)
                    if(option==null || !RaidLaunch.ValidEventId(option.id) || string.IsNullOrEmpty(option.text) || !ids.Add(option.id))
                        throw InvalidResponse();
            }
        }
        private void Apply(RaidProgress progress)
        {
            if (_disposed) return;
            if (progress?.event_id!=_state.event_id || progress.schema_version!=1) throw InvalidResponse();
            if (Session.Progress!=null && progress.content_version!=Session.Progress.content_version)
                throw new RaidClientException("version_conflict","問題の版が変わりました。開き直してください。");
            if (!Session.ApplyProgress(progress))
            {
                // Delayed or repeated snapshots are harmless, malformed initial snapshots are not.
                if (Session.Progress==null) throw InvalidResponse();
                return;
            }
            _state.content_version=progress.content_version;
            _state.seen_milestones=Session.SeenMilestones;
            if (!_venue)
            {
                if (progress.Ratio>=1 && (Personal?.personal_points??0)>0) _state.souvenir_received=true;
                _store.Save(_state);
            }
        }
        private async Task Run(Func<CancellationToken,Task> action, CancellationToken external)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RaidCoordinator));
            if (!await _operation.WaitAsync(0,external)) return;
            using (var linked=CancellationTokenSource.CreateLinkedTokenSource(external,_lifetime.Token))
            {
                Busy=true; Error=null;
                try
                {
                    Changed?.Invoke();
                    await action(linked.Token); linked.Token.ThrowIfCancellationRequested();
                    _failures=0;
                    _nextPoll=_now().AddSeconds(_venue?2:10);
                    if (!_venue && _state!=null && Session?.Progress?.Ratio>=1 && (Personal?.personal_points??0)>0)
                    { _state.souvenir_received=true; _store.Save(_state); }
                }
                catch (RaidClientException e)
                {
                    Error=e;
                    if (e.Code=="unauthorized" && _state!=null)
                    { _state.token=null; _state.join_request_id=null; _state.pending_request=null; Personal=null; _store.Save(_state); }
                    _failures=Math.Min(4,_failures+1);
                    _nextPoll=_now().AddSeconds(Math.Min(60,10*Math.Pow(2,_failures-1))*(1+0.15*_jitter()));
                    throw;
                }
                catch (System.IO.IOException)
                {
                    _nextPoll=_now().AddSeconds(60);
                    throw;
                }
                finally { Busy=false; _operation.Release(); if (!_disposed) Changed?.Invoke(); }
            }
        }
        private void RequireActive()
        {
            if (Session?.Progress?.Active!=true) throw new RaidClientException("event_closed","このレイドは受付時間外です。");
        }
        private void RequireJoined() { if (!Joined) throw new RaidClientException("unauthorized","参加し直してください。"); }
        private static RaidClientException InvalidResponse() => new RaidClientException("invalid_response","応答を確認できません。もう一度お試しください。");
        public void SetVisible(bool visible) { _visible=visible; if (visible) _nextPoll=_now(); }
        public void Dispose() { if (_disposed) return; _disposed=true; _lifetime.Cancel(); /* in-flight operations own their CTS */ }
    }
    /// <summary>v2 state machine. Commands and refreshes share one gate; durable requests survive lost replies.</summary>
    public sealed class ClassroomRaidCoordinator : IDisposable
    {
        private readonly IClassroomRaidClient _client;
        private readonly IClassroomParticipationStore _store;
        private readonly Func<DateTimeOffset> _now;
        private readonly SemaphoreSlim _gate=new SemaphoreSlim(1,1);
        private readonly CancellationTokenSource _life=new CancellationTokenSource();
        private ClassroomParticipation _state;
        private bool _venue,_visible=true,_disposed;
        private bool _cleanupPending;
        private CancellationTokenSource _visibility=new CancellationTokenSource();
        private DateTimeOffset _nextPoll;
        private int _failures;
        public ClassroomRaidCoordinator(IClassroomRaidClient client,IClassroomParticipationStore store,Func<DateTimeOffset> now=null)
        { _client=client; _store=store; _now=now??(()=>DateTimeOffset.UtcNow); }
        public event Action Changed;
        public ClassroomProgress Progress { get; private set; }
        public ClassroomPersonalState Personal { get; private set; }
        public RaidClientException Error { get; private set; }
        public bool Busy { get; private set; }
        public bool Joined => !_cleanupPending && !string.IsNullOrEmpty(_state?.token);
        public bool Venue => _venue;
        public bool CleanupPending => _cleanupPending;
        public bool HasPendingJoin => !_venue && !_cleanupPending && !Joined && RaidLaunch.ValidGeneratedId(_state?.join_request_id);
        public bool HasPending => HasPendingJoin || _state?.pending_next!=null || _state?.pending_answer!=null;
        public ClassroomRaidActionState ActionState => new ClassroomRaidActionState(
            !_venue && !_cleanupPending && (HasPendingJoin || Personal?.pending_answer!=null || Personal?.question==null && Progress?.phase=="running"),
            !Busy && !_cleanupPending && (HasPendingJoin || !HasPending),
            HasPendingJoin?"参加結果を確認する":!Joined?"参加する":Personal?.pending_answer!=null?
                Progress?.Terminal==true?"解説を読み終えた ・今回は0点":"解説を読み終えた ・貢献を確定":"次の問題を始める");
        public bool ShouldPoll(DateTimeOffset now) => !_disposed && _visible && !Busy && _state!=null &&
            Progress?.Terminal!=true && now>=_nextPoll;
        public Task OpenAsync(string eventId,bool venue,CancellationToken ct) => Run(async t=> {
            if(!RaidLaunch.ValidEventId(eventId)) throw new ArgumentException(nameof(eventId));
            if(_state!=null && _state.event_id!=eventId) throw new InvalidOperationException();
            _venue=venue;
            _state=venue?null:(_state??_store.Load(eventId));
            if(_state!=null && (_state.version!=2 || _state.event_id!=eventId)) throw Invalid();
            _state=_state??new ClassroomParticipation {event_id=eventId};
            if(_state.join_request_id!=null && !RaidLaunch.ValidGeneratedId(_state.join_request_id)) throw Invalid();
            if(Joined && (!DateTimeOffset.TryParse(_state.expires_at,out var expires) || _now()>=expires))
                await ClearParticipation(t);
            ApplyProgress(await _client.GetEventAsync(eventId,t),t);
            if(Joined) await Refresh(t);
            await Recover(t);
        },ct);
        public Task JoinAsync(CancellationToken ct) => Run(t=>JoinCoreAsync(true,t),ct);
        public Task RecoverJoinAsync(CancellationToken ct) => Run(t=>JoinCoreAsync(false,t),ct);
        private async Task JoinCoreAsync(bool allowCreate,CancellationToken t)
        {
            if(_venue) throw new InvalidOperationException("Venue cannot join.");
            if(Joined) { await Refresh(t); return; }
            if(_state==null) throw Invalid();
            if(_state.join_request_id!=null && !RaidLaunch.ValidGeneratedId(_state.join_request_id)) throw Invalid();
            if(_state.join_request_id==null && (!allowCreate || Progress?.phase!="running")) throw Closed();
            if(_state.join_request_id==null) _state.join_request_id=Guid.NewGuid().ToString("N");
            await Save(t);
            var r=await _client.JoinAsync(_state.event_id,_state.join_request_id,t);
            t.ThrowIfCancellationRequested();
            if(string.IsNullOrEmpty(r?.token) || !DateTimeOffset.TryParse(r.expires_at,out var expires) || expires<=_now()) throw Invalid();
            ValidateResult(r,t);
            var candidate=new ClassroomParticipation {event_id=_state.event_id,content_version=_state.content_version,
                join_request_id=_state.join_request_id,token=r.token,expires_at=r.expires_at,
                pending_next=_state.pending_next,pending_answer=_state.pending_answer};
            // A token is not usable until its browser persistence has completed.
            await Save(candidate,t);
            Apply(r,t); _state=candidate;
            await Refresh(t);
        }
        public Task NextAsync(CancellationToken ct) => Run(async t=> {
            RequireJoined();
            if(_state.pending_next!=null) { await Refresh(t); await Recover(t); return; }
            if(Progress?.phase!="running" || Personal?.phase!="idle") throw Closed();
            _state.pending_next=new ClassroomNextRequest {request_id=Guid.NewGuid().ToString("N"),content_version=_state.content_version};
            await Save(t); await SendNext(t);
        },ct);
        public Task SubmitAsync(string optionId,CancellationToken ct) => Run(async t=> {
            RequireJoined();
            if(_state.pending_answer!=null) { await Refresh(t); await Recover(t); return; }
            var q=Personal?.question;
            if(q==null) throw Invalid();
            var valid=false; foreach(var o in q.options) if(o.id==optionId) valid=true;
            if(!valid) throw new ArgumentException(nameof(optionId));
            _state.pending_answer=new ClassroomAnswerRequest {request_id=Guid.NewGuid().ToString("N"),attempt_id=q.attempt_id,
                content_version=_state.content_version,option_id=optionId};
            await Save(t); await SendAnswer(t);
        },ct);
        public Task ConfirmAsync(CancellationToken ct) => Run(async t=> {
            RequireJoined(); var id=Personal?.pending_answer?.answer_id; if(id==null) return;
            var r=await _client.AcknowledgeAsync(_state.event_id,_state.token,id,t);
            var receipt=r?.receipt;
            if(receipt==null || receipt.answer_id!=id || receipt.attempt_id!=id || receipt.awarded_points<0 || receipt.awarded_points>2 ||
                receipt.personal_points_after<0 || receipt.progress_revision<0 || receipt.progress_revision>r.progress.revision ||
                receipt.personal_points_after>r.personal.personal_points) throw Invalid();
            Apply(r,t); await Refresh(t);
        },ct);
        public Task PollAsync(CancellationToken ct) => Run(async t=> {
            if(_state==null) return;
            ApplyProgress(await _client.GetEventAsync(_state.event_id,t),t);
            if(Joined) await Refresh(t);
            await Recover(t);
        },ct);
        private async Task SendNext(CancellationToken t)
        {
            await Save(t); var r=await _client.NextAsync(_state.event_id,_state.token,_state.pending_next,t);
            if(!RaidLaunch.ValidGeneratedId(r?.issued_attempt_id)) throw Invalid();
            Apply(r,t); await Refresh(t);
        }
        private async Task SendAnswer(CancellationToken t)
        {
            await Save(t); var request=_state.pending_answer;
            var r=await _client.AnswerAsync(_state.event_id,_state.token,request,t);
            if(r?.answer==null || r.answer.attempt_id!=request.attempt_id || r.answer.answer_id!=request.attempt_id) throw Invalid();
            Apply(r,t); await Refresh(t);
        }
        private async Task Recover(CancellationToken t)
        {
            if(HasPendingJoin) await JoinCoreAsync(false,t);
            if(!Joined) return;
            if(_state.pending_next!=null) await SendNext(t);
            if(_state.pending_answer!=null) await SendAnswer(t);
        }
        private async Task Refresh(CancellationToken t)
        {
            var r=await _client.GetMeAsync(_state.event_id,_state.token,t); Apply(r,t);
            // Only /me confirms that an uncertain write is durable. Older replies cannot clear pending.
            if(r.personal.personal_version!=Personal.personal_version || r.progress.revision!=Progress.revision) return;
            if(_state.pending_next!=null && (Personal.question!=null || Personal.pending_answer!=null)) _state.pending_next=null;
            if(_state.pending_answer!=null && Personal.pending_answer?.attempt_id==_state.pending_answer.attempt_id) _state.pending_answer=null;
            await Save(t);
        }
        private void ValidateResult(ClassroomStateResult r,CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            if(r==null || !ClassroomState.Valid(r.progress,_state.event_id) || !ClassroomState.Valid(r.personal,r.progress)) throw Invalid();
            if(_state.content_version!=null && r.progress.content_version!=_state.content_version) throw Invalid();
            var candidate=r.personal;
            if(Personal!=null && candidate.personal_version<Personal.personal_version) candidate=Personal;
            if(Personal!=null && candidate.personal_version==Personal.personal_version && !ClassroomState.SamePersonal(Personal,candidate)) throw Invalid();
            if(Personal!=null && (candidate.personal_points<Personal.personal_points || candidate.answered_count<Personal.answered_count)) throw Invalid();
            ValidateProgress(r.progress,t);
        }
        private void Apply(ClassroomStateResult r,CancellationToken t)
        {
            ValidateResult(r,t);
            var candidate=Personal!=null && r.personal.personal_version<Personal.personal_version?Personal:r.personal;
            ApplyProgress(r.progress,t);
            if(Progress.Terminal && candidate.phase=="idle")
                candidate=new ClassroomPersonalState {schema_version=2,event_id=candidate.event_id,content_version=candidate.content_version,
                    personal_version=candidate.personal_version,answered_count=candidate.answered_count,personal_points=candidate.personal_points,phase="finished"};
            // Equal personal_version may reflect a newer terminal public state (idle → finished).
            if(Personal==null || candidate.personal_version>Personal.personal_version || r.progress.revision>=Progress.revision) Personal=candidate;
        }
        private void ValidateProgress(ClassroomProgress p,CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            if(!ClassroomState.Valid(p,_state.event_id)) throw Invalid();
            if(_state.content_version!=null && _state.content_version!=p.content_version) throw Invalid();
            if(Progress!=null && p.revision<Progress.revision) return;
            if(!ClassroomState.CanAdvance(Progress,p)) throw Invalid();
            if(Progress!=null && p.revision==Progress.revision && (p.total_points!=Progress.total_points || p.phase!=Progress.phase)) throw Invalid();
        }
        private void ApplyProgress(ClassroomProgress p,CancellationToken t)
        {
            ValidateProgress(p,t);
            if(Progress!=null && p.revision<Progress.revision) return;
            Progress=p; _state.content_version=p.content_version;
        }
        private Task Save(CancellationToken ct) => Save(_state,ct);
        private async Task Save(ClassroomParticipation state,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if(!_venue) { _store.Save(state); await _store.FlushAsync(ct); ct.ThrowIfCancellationRequested(); } }
        private async Task ClearParticipation(CancellationToken ct)
        {
            _cleanupPending=true;
            var eventId=_state.event_id;
            _state=new ClassroomParticipation {event_id=eventId,content_version=_state.content_version}; Personal=null;
            _store.Delete(eventId); await _store.FlushAsync(ct); ct.ThrowIfCancellationRequested();
            _cleanupPending=false;
        }
        private void RequireJoined() { if(_venue || !Joined) throw new RaidClientException("unauthorized","参加してください。"); }
        private static RaidClientException Invalid() => new RaidClientException("invalid_response","応答を確認できません。再取得してください。");
        private static RaidClientException Closed() => new RaidClientException("event_closed","このレイドは受付時間外です。");
        private async Task Run(Func<CancellationToken,Task> action,CancellationToken external)
        {
            if(_disposed) throw new ObjectDisposedException(nameof(ClassroomRaidCoordinator));
            if(!_visible) return;
            if(!await _gate.WaitAsync(0,external)) return;
            using(var linked=CancellationTokenSource.CreateLinkedTokenSource(external,_life.Token,_visibility.Token))
            {
                Busy=true; Error=null;
                try { Changed?.Invoke(); if(_cleanupPending) await ClearParticipation(linked.Token); await action(linked.Token); linked.Token.ThrowIfCancellationRequested(); _failures=0; _nextPoll=_now().AddSeconds(_venue?2:10); }
                catch(RaidClientException e)
                {
                    Error=e;
                    if(e.Code=="unauthorized" && _state!=null && !_venue)
                        await ClearParticipation(linked.Token);
                    _failures=Math.Min(4,_failures+1);
                    var seconds=Math.Min(60,10*Math.Pow(2,_failures-1));
                    if(double.TryParse(e.RetryAfter,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var retry) && !double.IsNaN(retry) && !double.IsInfinity(retry)) seconds=Math.Max(seconds,Math.Min(86400,retry));
                    else if(DateTimeOffset.TryParse(e.RetryAfter,out var at)) seconds=Math.Max(seconds,(at-_now()).TotalSeconds);
                    _nextPoll=_now().AddSeconds(seconds); throw;
                }
                catch(System.IO.IOException) { _nextPoll=_now().AddSeconds(60); throw; }
                finally { Busy=false; _gate.Release(); if(!_disposed && _visible) Changed?.Invoke(); }
            }
        }
        public void SetVisible(bool visible)
        {
            if(_disposed || visible==_visible) return;
            _visible=visible;
            if(!visible) _visibility.Cancel();
            else { _visibility.Dispose(); _visibility=new CancellationTokenSource(); _nextPoll=_now(); Changed?.Invoke(); }
        }
        public void Dispose() { if(_disposed) return; _disposed=true; _life.Cancel(); }
    }
    /// <summary>The actual online Render uses this state; recovery tests do not duplicate UI predicates.</summary>
    public readonly struct ClassroomRaidActionState
    {
        public ClassroomRaidActionState(bool visible,bool enabled,string text) { Visible=visible; Enabled=enabled; Text=text; }
        public bool Visible { get; }
        public bool Enabled { get; }
        public string Text { get; }
    }
    public enum ClassroomStartupState { NotStarted, LoadingConfig, ConfigUnavailable, OpeningEvent, EventOpenFailed, Online, Suspended, Closed }
    public sealed class ClassroomRaidStartup<T>:IDisposable where T:class,IDisposable
    {
        private readonly Func<bool,CancellationToken,Task<ClassroomConfigResult>> _load;
        private readonly Func<BackendConfig,T> _create;
        private readonly Func<T,CancellationToken,Task> _open;
        private readonly Action<T,bool> _visibility;
        private TaskCompletionSource<bool> _pending;
        private Task _resume;
        private CancellationTokenSource _attempt;
        private bool _visible=true,_closed,_opened;
        private int _generation;
        public ClassroomRaidStartup(Func<bool,CancellationToken,Task<ClassroomConfigResult>> load,Func<BackendConfig,T> create,
            Func<T,CancellationToken,Task> open,Action<T,bool> visible)
        { _load=load; _create=create; _open=open; _visibility=visible; }
        public T Online { get; private set; }
        public ClassroomStartupState State { get; private set; }
        public ClassroomConfigResult ConfigResult { get; private set; }
        public bool Busy => State==ClassroomStartupState.LoadingConfig || State==ClassroomStartupState.OpeningEvent;
        public Task EnsureAsync(bool reload=false)
        {
            if(_closed || !_visible) return Task.CompletedTask;
            if(_pending!=null)
            {
                if(_attempt.IsCancellationRequested) return _resume??(_resume=ResumeAsync(_pending.Task));
                return _pending.Task;
            }
            if(Online!=null && _opened) { State=ClassroomStartupState.Online; return Task.CompletedTask; }
            var completion=new TaskCompletionSource<bool>(); _pending=completion;
            var attempt=new CancellationTokenSource(); _attempt=attempt;
            State=Online==null?ClassroomStartupState.LoadingConfig:ClassroomStartupState.OpeningEvent;
            _=StartAsync(completion,attempt,_generation,reload);
            return completion.Task;
        }
        private async Task ResumeAsync(Task previous)
        {
            try { await previous; } catch(OperationCanceledException) { }
            finally { _resume=null; }
            await EnsureAsync();
        }
        private void Check(int generation,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if(_closed || !_visible || generation!=_generation) throw new OperationCanceledException(ct); }
        private async Task StartAsync(TaskCompletionSource<bool> completion,CancellationTokenSource attempt,int generation,bool reload)
        {
            Exception error=null; var canceled=false;
            try
            {
                var ct=attempt.Token;
                if(Online==null)
                {
                    var result=await _load(reload,ct); Check(generation,ct); ConfigResult=result;
                    if(result.Status!=ClassroomConfigStatus.Ready) { State=ClassroomStartupState.ConfigUnavailable; return; }
                    T candidate=null;
                    try
                    {
                        candidate=_create(result.Config); Check(generation,ct);
                        Online=candidate; candidate=null; _visibility(Online,true);
                    }
                    finally { candidate?.Dispose(); }
                }
                State=ClassroomStartupState.OpeningEvent;
                await _open(Online,ct); Check(generation,ct);
                _opened=true; State=ClassroomStartupState.Online;
            }
            catch(OperationCanceledException) { canceled=true; if(!_closed) State=ClassroomStartupState.Suspended; }
            catch(Exception e) { error=e; if(!_closed) State=Online==null?ClassroomStartupState.ConfigUnavailable:ClassroomStartupState.EventOpenFailed; }
            finally
            {
                _pending=null; _attempt=null; attempt.Dispose();
                if(error!=null) completion.TrySetException(error);
                else if(canceled) completion.TrySetCanceled();
                else completion.TrySetResult(true);
            }
        }
        public void SetVisible(bool visible)
        {
            if(_closed) return; _visible=visible; if(Online!=null) _visibility(Online,visible);
            if(!visible) { _generation++; State=ClassroomStartupState.Suspended; _attempt?.Cancel(); }
            else if(Online!=null && _opened) State=ClassroomStartupState.Online;
        }
        public void Dispose()
        {
            if(_closed) return; _closed=true; _generation++; State=ClassroomStartupState.Closed;
            _attempt?.Cancel(); Online?.Dispose();
        }
    }
}
