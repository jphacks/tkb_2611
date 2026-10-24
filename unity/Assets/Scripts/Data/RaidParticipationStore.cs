using System;
using System.IO;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Data
{
    /// <summary>Event-scoped save, separate from the island database; no device ID or photo data.</summary>
    public sealed class RaidParticipationStore : IRaidParticipationStore
    {
        private readonly string _directory;
        private readonly Func<RaidParticipation,string> _encode;
        private readonly Func<string,RaidParticipation> _decode;
        private readonly string _prefix;
        public RaidParticipationStore(string directory, Func<RaidParticipation,string> encode,
            Func<string,RaidParticipation> decode, bool localDemo = false)
        {
            _directory=directory ?? throw new ArgumentNullException(nameof(directory));
            _encode=encode ?? throw new ArgumentNullException(nameof(encode));
            _decode=decode ?? throw new ArgumentNullException(nameof(decode));
            _prefix=localDemo?"raid-demo-":"raid-";
        }
        private SaveStore FileFor(string eventId)
        {
            if (!RaidLaunch.ValidEventId(eventId)) throw new ArgumentException(nameof(eventId));
            return new SaveStore(_directory,_prefix+eventId+".json",1);
        }
        public RaidParticipation Load(string eventId)
        {
            var store=FileFor(eventId);
            var result=store.Load(payload => {
                try { var s=_decode(payload); return s!=null && s.version==1 && s.event_id==eventId; }
                catch (ArgumentException) { return false; }
            });
            if (result.Status==SaveLoadStatus.TooNew || result.Status==SaveLoadStatus.MigrationFailed || result.Status==SaveLoadStatus.Corrupt)
                throw new IOException("レイドの参加情報を読み込めません。保存データを確認してください。");
            return result.HasPayload ? _decode(result.Payload) : null;
        }
        public void Save(RaidParticipation state)
        {
            if (state==null) throw new ArgumentNullException(nameof(state));
            // Load first so SaveStore refuses to overwrite an unsupported newer format.
            var store=FileFor(state.event_id); store.Load(); store.Save(_encode(state));
        }
        public void Delete(string eventId) => FileFor(eventId).DeleteAll();
    }
    public sealed class ClassroomParticipationStore : IClassroomParticipationStore
    {
        private readonly string _directory;
        private readonly Func<ClassroomParticipation,string> _encode;
        private readonly Func<string,ClassroomParticipation> _decode;
        public ClassroomParticipationStore(string directory,Func<ClassroomParticipation,string> encode,Func<string,ClassroomParticipation> decode)
        { _directory=directory; _encode=encode; _decode=decode; }
        private SaveStore FileFor(string id)
        {
            if(!RaidLaunch.ValidEventId(id)) throw new ArgumentException(nameof(id));
            return new SaveStore(_directory,"classroom-raid-"+id+".json",2);
        }
        public ClassroomParticipation Load(string eventId)
        {
            var file=FileFor(eventId);
            var result=file.Load(payload=> {
                try { var p=_decode(payload); return p!=null && p.version==2 && p.event_id==eventId; }
                catch(Exception) { return false; }
            });
            if(result.Status==SaveLoadStatus.TooNew || result.Status==SaveLoadStatus.MigrationFailed || result.Status==SaveLoadStatus.Corrupt)
                throw new IOException("教室レイドの参加情報を読み込めません。");
            return result.HasPayload?_decode(result.Payload):null;
        }
        public void Save(ClassroomParticipation state)
        { if(state==null || state.version!=2) throw new ArgumentException(nameof(state)); var file=FileFor(state.event_id); file.Load(); file.Save(_encode(state)); }
        public void Delete(string eventId) => FileFor(eventId).DeleteAll();
        public System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
#if UNITY_WEBGL && !UNITY_EDITOR
            return ClassroomBrowserPersistence.FlushAsync(ct);
#else
            return System.Threading.Tasks.Task.CompletedTask;
#endif
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>Linked from tools, without creating a Unity asset or handcrafted meta.</summary>
    internal static class ClassroomBrowserPersistence
    {
        private delegate void Completion(int ticket,int success);
        private static readonly Completion Callback=Complete;
        private static readonly System.Collections.Generic.Dictionary<int,System.Threading.Tasks.TaskCompletionSource<bool>> Pending=
            new System.Collections.Generic.Dictionary<int,System.Threading.Tasks.TaskCompletionSource<bool>>();
        private static int _ticket;
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void ClassroomRaid_Sync(int ticket,Completion callback);
        [AOT.MonoPInvokeCallback(typeof(Completion))]
        private static void Complete(int ticket,int success)
        {
            lock(Pending) if(Pending.TryGetValue(ticket,out var completion)) completion.TrySetResult(success!=0);
        }
        internal static async System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var completion=new System.Threading.Tasks.TaskCompletionSource<bool>(); int ticket;
            lock(Pending) { ticket=++_ticket; Pending.Add(ticket,completion); }
            try
            {
                using(ct.Register(()=>completion.TrySetCanceled()))
                {
                    try { ClassroomRaid_Sync(ticket,Callback); }
                    catch(Exception) { throw new IOException("ブラウザ保存の同期機能を利用できません。対応するWebビルドが必要です。"); }
                    if(!await completion.Task) throw new IOException("ブラウザへ参加情報を保存できません。保存設定を確認してください。");
                }
                ct.ThrowIfCancellationRequested();
            }
            finally { lock(Pending) Pending.Remove(ticket); }
        }
    }
#endif
}
