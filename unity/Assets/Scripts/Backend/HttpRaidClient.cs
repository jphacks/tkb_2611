using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Backend
{
    /// <summary>Same-origin HTTP adapter. The trusted API URL is injected, never taken from the QR query.</summary>
    public sealed class HttpRaidClient : IRaidClient
    {
        private readonly string _baseUrl;
        private readonly IHttpTransport _transport;
        private readonly IClassroomJsonCodec _json;
#if !RAID_EXTERNAL_CHECKS
        public HttpRaidClient(string apiBaseUrl) : this(apiBaseUrl,new UnityWebRequestTransport(),new ClassroomJsonCodec()) { }
#endif
        public HttpRaidClient(string apiBaseUrl, IHttpTransport transport, IClassroomJsonCodec json)
        {
            if (!Uri.TryCreate(apiBaseUrl,UriKind.Absolute,out var uri) ||
                (uri.Scheme!="https" && !(uri.Scheme=="http" && uri.IsLoopback)) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Trusted HTTPS API base URL required.",nameof(apiBaseUrl));
            _baseUrl=apiBaseUrl.TrimEnd('/');
            _transport=transport; _json=json;
        }
        private static string EventPath(string id)
        { if (!RaidLaunch.ValidEventId(id)) throw new ArgumentException(nameof(id)); return "/"+id; }
        public Task<RaidProgress> GetEventAsync(string e,CancellationToken ct) => Send<RaidProgress>("GET",EventPath(e),null,null,ct);
        public Task<RaidParticipant> JoinAsync(string e,string requestId,CancellationToken ct) =>
            Send<RaidParticipant>("POST",EventPath(e)+"/participants",new JoinRequest { join_request_id=requestId },null,ct);
        public Task<RaidPersonalState> GetMeAsync(string e,string token,CancellationToken ct) => Send<RaidPersonalState>("GET",EventPath(e)+"/me",null,token,ct);
        public Task<RaidAnswerResult> AnswerAsync(string e,string token,RaidAnswerRequest request,CancellationToken ct) =>
            Send<RaidAnswerResult>("POST",EventPath(e)+"/answers",request,token,ct);
        public Task<RaidAckResult> AcknowledgeAsync(string e,string token,string answerId,CancellationToken ct)
        {
            if (!RaidLaunch.ValidEventId(answerId)) throw new ArgumentException(nameof(answerId));
            return Send<RaidAckResult>("POST",EventPath(e)+"/answers/"+answerId+"/ack",new EmptyBody(),token,ct);
        }
        private async Task<T> Send<T>(string method,string path,object body,string token,CancellationToken ct) where T:class
        {
            ct.ThrowIfCancellationRequested();
            var headers=new System.Collections.Generic.Dictionary<string,string>();
            if (!string.IsNullOrEmpty(token)) headers["Authorization"]="Bearer "+token;
            var result=await _transport.SendAsync(new HttpRequestSpec(method,_baseUrl+path,
                body==null?null:Encoding.UTF8.GetBytes(_json.Serialize(body)),body==null?null:"application/json",15,headers:headers),ct);
            ct.ThrowIfCancellationRequested();
            RaidHttp.Check(result,_json);
            try { return _json.Deserialize<T>(result.Text) ?? throw new ArgumentException(); }
            catch (Exception e) when (!(e is OperationCanceledException))
            { throw new RaidClientException("invalid_response","応答を確認できません。もう一度お試しください。"); }
        }
        [Serializable] private sealed class JoinRequest { public string join_request_id; }
        [Serializable] private sealed class EmptyBody { }
    }
    /// <summary>One HTTP attempt only. Retries belong to the persisted operation in the coordinator.</summary>
    public sealed class HttpClassroomRaidClient : IClassroomRaidClient
    {
        private readonly string _url;
        private readonly IHttpTransport _transport;
        private readonly IClassroomJsonCodec _json;
        public HttpClassroomRaidClient(string apiBaseUrl, IHttpTransport transport, IClassroomJsonCodec json)
        {
            if (!Uri.TryCreate(apiBaseUrl,UriKind.Absolute,out var uri) ||
                (uri.Scheme!="https" && !(uri.Scheme=="http" && uri.IsLoopback)) ||
                uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="" || uri.AbsolutePath.TrimEnd('/')!="/api")
                throw new ArgumentException("Trusted /api URL required.");
            _url=apiBaseUrl.TrimEnd('/'); _transport=transport ?? throw new ArgumentNullException(nameof(transport));
            _json=json ?? throw new ArgumentNullException(nameof(json));
        }
        private static string Path(string id)
        { if (!RaidLaunch.ValidEventId(id)) throw new ArgumentException(nameof(id)); return "/v2/classroom-raids/"+id; }
        public Task<ClassroomProgress> GetEventAsync(string e,CancellationToken ct) => Send<ClassroomProgress>("GET",Path(e),null,null,ct);
        public Task<ClassroomJoinResult> JoinAsync(string e,string id,CancellationToken ct)
        { if(!RaidLaunch.ValidGeneratedId(id)) throw new ArgumentException(nameof(id)); return Send<ClassroomJoinResult>("POST",Path(e)+"/participants",new Join {join_request_id=id},null,ct); }
        public Task<ClassroomStateResult> GetMeAsync(string e,string t,CancellationToken ct) => Send<ClassroomStateResult>("GET",Path(e)+"/me",null,t,ct);
        public Task<ClassroomNextResult> NextAsync(string e,string t,ClassroomNextRequest r,CancellationToken ct) => Send<ClassroomNextResult>("POST",Path(e)+"/questions/next",r,t,ct);
        public Task<ClassroomAnswerResponse> AnswerAsync(string e,string t,ClassroomAnswerRequest r,CancellationToken ct) => Send<ClassroomAnswerResponse>("POST",Path(e)+"/answers",r,t,ct);
        public Task<ClassroomAckResult> AcknowledgeAsync(string e,string t,string id,CancellationToken ct)
        { if(!RaidLaunch.ValidGeneratedId(id)) throw new ArgumentException(nameof(id)); return Send<ClassroomAckResult>("POST",Path(e)+"/answers/"+id+"/ack",new Empty(),t,ct); }
        private async Task<T> Send<T>(string method,string path,object body,string token,CancellationToken ct) where T:class
        {
            ct.ThrowIfCancellationRequested();
            var headers=new System.Collections.Generic.Dictionary<string,string>();
            if(token!=null) { if(string.IsNullOrWhiteSpace(token) || token.IndexOfAny(new[]{'\r','\n'})>=0) throw new ArgumentException("Invalid credential."); headers["Authorization"]="Bearer "+token; }
            var result=await _transport.SendAsync(new HttpRequestSpec(method,_url+path,
                body==null?null:Encoding.UTF8.GetBytes(_json.Serialize(body)),body==null?null:"application/json",15,headers:headers),ct);
            ct.ThrowIfCancellationRequested(); RaidHttp.Check(result,_json);
            try { return _json.Deserialize<T>(result.Text) ?? throw new ArgumentException(); }
            catch(Exception e) when (!(e is OperationCanceledException)) { throw RaidHttp.Invalid(); }
        }
        [Serializable] private sealed class Join { public string join_request_id; }
        [Serializable] private sealed class Empty { }
    }
    internal static class RaidHttp
    {
        [Serializable] private sealed class ErrorEnvelope { public ErrorBody error; }
        [Serializable] private sealed class ErrorBody { public string code; }
        internal static RaidClientException Invalid() => new RaidClientException("invalid_response","応答を確認できません。再取得してください。");
        internal static void Check(HttpResult r,IClassroomJsonCodec json)
        {
            if(r!=null && !r.ConnectionFailed && r.Status>=200 && r.Status<300 && !r.Text.TrimStart().StartsWith("<")) return;
            var status=r?.Status??0;
            var code=status==401?"unauthorized":status==404?"not_found":status==429?"rate_limited":status>=500||status==0?"unavailable":status>=300&&status<400?"redirect_refused":"invalid_request";
            try
            {
                var known=json.Deserialize<ErrorEnvelope>(r?.Text??"")?.error?.code;
                if(known=="event_closed" || known=="version_conflict" || known=="request_conflict" || known=="already_answered" || known=="state_conflict") code=known;
            }
            catch(Exception) { /* Only allowlisted error identifiers leave the transport boundary. */ }
            throw new RaidClientException(code,status==401?"参加情報の期限が切れました。参加し直してください。":status==429?"混み合っています。しばらくお待ちください。":"通信を確認できません。再取得してください。",status==0||status==429||status>=500,r?.RetryAfter);
        }
    }
}
