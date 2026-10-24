using System;
using System.Threading.Tasks;
using System.Threading;
#if !RAID_EXTERNAL_CHECKS
using UnityEngine;
using UnityEngine.Networking;
#endif

namespace ProjectPenguin.Backend
{
    /// <summary>api/openapi.yaml の「上限」の表と同じ値。</summary>
    public static class BackendLimits
    {
        /// <summary>クライアントのタイムアウト (秒)。UnityWebRequest.timeout に渡す。</summary>
        public const int TimeoutSeconds = 25;

        /// <summary>送る写真の大きさの上限。契約1.1.0の1 MBに収める。</summary>
        public const int MaxImageBytes = 1024 * 1024;

        /// <summary>写真の長い辺の上限。撮るとき (ReceiptCameraPreview.CaptureJpegWithin) にこの大きさへ縮める。</summary>
        public const int MaxImageSide = 1280;

        /// <summary>retryable なエラーと接続の失敗のときだけ、あと何回試すか。</summary>
        public const int Retries = 1;
    }

    /// <summary>
    /// 撮った写真を <see cref="BackendLimits.MaxImageBytes"/> に収める段取り。
    /// 「大きすぎる」と言われたら利用者は二度と撮らないので、同じフレームから品質 → 大きさの順に縮め直し、
    /// 利用者には何も見せずに収める。
    /// </summary>
    public static class PhotoFit
    {
        /// <summary>縮め直す 1 段。長い辺の上限と JPEG の品質。</summary>
        public readonly struct Step
        {
            public Step(int maxSide, int quality)
            {
                MaxSide = maxSide;
                Quality = quality;
            }

            public int MaxSide { get; }
            public int Quality { get; }
        }

        /// <summary>試す順。見分けに効く大きさを残すため、先に品質を下げ、最後に小さくする。</summary>
        public static readonly Step[] Steps =
        {
            new Step(BackendLimits.MaxImageSide, 80),
            new Step(BackendLimits.MaxImageSide, 60),
            new Step(BackendLimits.MaxImageSide, 40),
            new Step(960, 40),
            new Step(640, 40),
        };

        /// <summary>
        /// <see cref="Steps"/> の順に <paramref name="encode"/> を呼び、<paramref name="maxBytes"/> に収まった最初のものを返す。
        /// どれも収まらなければ最後 (いちばん小さいもの) を返す (送る側の上限の確認で止まる)。
        /// <paramref name="encode"/> が null を返したら (カメラが止まったなど)、そこでやめて null を返す。
        /// </summary>
        public static byte[] Fit(Func<Step, byte[]> encode, int maxBytes = BackendLimits.MaxImageBytes)
        {
            if (encode == null)
            {
                throw new ArgumentNullException(nameof(encode));
            }

            byte[] last = null;
            foreach (var step in Steps)
            {
                last = encode(step);
                if (last == null || last.Length <= maxBytes)
                {
                    return last;
                }
            }

            return last;
        }
    }

    /// <summary>判定に何を使うか。</summary>
    public enum JudgeMode
    {
        /// <summary>端末の中の固定の判定 (<see cref="MockEcoPhotoJudge"/>)。backend を使わない。</summary>
        Mock,

        /// <summary>backend の写真判定。</summary>
        Http,
    }

    /// <summary>
    /// 実行時に読む接続先の設定 (#121)。Web ビルドを作り直さずに、公開先に置いた
    /// StreamingAssets/penguin-config.json の差し替えだけで接続先と使う機能を変える。秘密は置かない (絶対規則 2)。
    /// </summary>
    public sealed class BackendConfig
    {
        public static readonly BackendConfig Default = new BackendConfig(null, JudgeMode.Mock, false, false, false);

        public BackendConfig(string apiBaseUrl, JudgeMode judge, bool communityEnabled, bool questsEnabled, bool demoMode)
        {
            ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? null : apiBaseUrl.TrimEnd('/');
            Judge = ApiBaseUrl == null ? JudgeMode.Mock : judge;
            CommunityEnabled = ApiBaseUrl != null && communityEnabled;
            QuestsEnabled = ApiBaseUrl != null && questsEnabled;
            DemoMode = demoMode;
        }

        /// <summary>backend の根 (「https://…/api」)。無ければ null で、backend を使う機能はすべて切る。</summary>
        public string ApiBaseUrl { get; }

        public JudgeMode Judge { get; }
        public bool CommunityEnabled { get; }
        public bool QuestsEnabled { get; }

        /// <summary>
        /// 会場のデモ。判定サーバーが続けて失敗したら、端末の中の判定に切り替えて止めない (#86)。
        /// ふだんは切り替えず「もう一度撮ってね」を出す。
        /// </summary>
        public bool DemoMode { get; }

        public bool UsesBackend => ApiBaseUrl != null;

        public string HealthUrl => ApiBaseUrl == null ? null : ApiBaseUrl + "/v1/health";

        /// <summary>
        /// 設定ファイルの中身を読む。壊れている・読めない項目は既定 (<see cref="Default"/>) に倒す。
        /// <paramref name="pageUrl"/> は Web で相対の <c>api_base_url</c> (「/api」) を解くのに使う。
        /// </summary>
#if !RAID_EXTERNAL_CHECKS
        public static BackendConfig Parse(string json, string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Default;
            }

            ConfigDto dto;
            try
            {
                dto = JsonUtility.FromJson<ConfigDto>(json);
            }
            catch (ArgumentException)
            {
                return Default;
            }

            if (dto == null)
            {
                return Default;
            }

            var judge = string.Equals(dto.judge, "http", StringComparison.OrdinalIgnoreCase) ? JudgeMode.Http : JudgeMode.Mock;
            return new BackendConfig(Resolve(dto.api_base_url, pageUrl), judge, dto.community, dto.quests, dto.demo_mode);
        }
#endif

        /// <summary>相対の URL を、開いているページの URL から解く。解けなければ null。</summary>
        public static string Resolve(string url, string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
            {
                return absolute.ToString();
            }

            if (!string.IsNullOrEmpty(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var page)
                && (page.Scheme == Uri.UriSchemeHttps || page.Scheme == Uri.UriSchemeHttp)
                && Uri.TryCreate(page, url, out var resolved))
            {
                return resolved.ToString();
            }

            return null;
        }

#pragma warning disable IDE1006 // 設定ファイルの項目名に合わせる
        [Serializable]
        private sealed class ConfigDto
        {
            public string api_base_url;
            public string judge;
            public bool community;
            public bool quests;
            public bool demo_mode;
        }
#pragma warning restore IDE1006
    }

    /// <summary>
    /// 設定ファイルを起動後に 1 回だけ読み、全員で同じ結果を使う。読めなければ <see cref="BackendConfig.Default"/>。
    /// Web では StreamingAssets も HTTP で取りに行く場所なので、どの環境でも UnityWebRequest で読む (#121 のコメント)。
    /// </summary>
#if !RAID_EXTERNAL_CHECKS
    public static class BackendRuntime
    {
        public const string ConfigFileName = "penguin-config.json";

        private const int ConfigTimeoutSeconds = 10;

        private static Task<BackendConfig> _config;
        private static readonly ClassroomConfigCache ClassroomConfig=new ClassroomConfigCache(LoadClassroomConfigAsync,()=>Application.absoluteURL);

        public static Task<BackendConfig> ConfigAsync() => _config ??= LoadAsync();

        /// <summary>テストと Editor の確認用。次の <see cref="ConfigAsync"/> で読み直す。</summary>
        public static void Reset() { _config=null; ClassroomConfig.Reset(); }

        public static Task<ClassroomConfigResult> ClassroomConfigAsync(bool forceReload,CancellationToken ct) =>
            ClassroomConfig.GetAsync(forceReload,ct);

        private static Task<HttpResult> LoadClassroomConfigAsync()
        {
            var path=Application.streamingAssetsPath+"/"+ConfigFileName;
            var url=path.Contains("://")?path:"file://"+path;
            return new UnityWebRequestTransport().SendAsync(
                new HttpRequestSpec(UnityWebRequest.kHttpVerbGET,url,timeoutSeconds:ConfigTimeoutSeconds),default);
        }

        private static async Task<BackendConfig> LoadAsync()
        {
            var path = Application.streamingAssetsPath + "/" + ConfigFileName;
            var url = path.Contains("://") ? path : "file://" + path;
            try
            {
                var result = await new UnityWebRequestTransport().SendAsync(
                    new HttpRequestSpec(UnityWebRequest.kHttpVerbGET, url, timeoutSeconds: ConfigTimeoutSeconds), default);
                if (result.ConnectionFailed || result.Status >= 400)
                {
                    Debug.LogWarning($"{nameof(BackendRuntime)}: {ConfigFileName} を読めなかったので、端末の中の判定で動きます");
                    return BackendConfig.Default;
                }

                return BackendConfig.Parse(result.Text, Application.absoluteURL);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(BackendRuntime)}: {ConfigFileName} を読めませんでした ({e.GetType().Name})");
                return BackendConfig.Default;
            }
        }
    }
#endif
    public enum ClassroomConfigStatus { Ready, Disabled, FetchFailed, InvalidConfig }
    public sealed class ClassroomConfigResult
    {
        public ClassroomConfigResult(ClassroomConfigStatus status,BackendConfig config=null) { Status=status; Config=config; }
        public ClassroomConfigStatus Status { get; }
        public BackendConfig Config { get; }
        public string Message => Status==ClassroomConfigStatus.Ready?"":Status==ClassroomConfigStatus.Disabled?
            "教室レイドの接続先は未設定です。開催の準備担当へ確認してください。":Status==ClassroomConfigStatus.FetchFailed?
            "接続設定を取得できませんでした。通信を確認して再取得してください。":"接続設定の内容を確認できません。準備担当へ確認し、再取得してください。";
        public static ClassroomConfigResult Parse(HttpResult response,string pageUrl)
        {
            if(response==null || response.ConnectionFailed || response.Status!=0 && (response.Status<200 || response.Status>=300))
                return new ClassroomConfigResult(ClassroomConfigStatus.FetchFailed);
            try
            {
                var values=ConfigJson.Read(response.Text);
                values.TryGetValue("api_base_url",out var value);
                if(value!=null && !(value is string)) throw new FormatException();
                foreach(var key in new[]{"community","quests","demo_mode"})
                    if(values.TryGetValue(key,out var flag) && !(flag is bool)) throw new FormatException();
                if(values.TryGetValue("judge",out var judge) && judge!=null && !(judge is string)) throw new FormatException();
                var url=value as string;
                if(string.IsNullOrWhiteSpace(url)) return new ClassroomConfigResult(ClassroomConfigStatus.Disabled);
                url=BackendConfig.Resolve(url,pageUrl);
                if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) ||
                    (uri.Scheme!="https" && !(uri.Scheme=="http" && uri.IsLoopback)) ||
                    uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="" || uri.AbsolutePath.TrimEnd('/')!="/api")
                    throw new FormatException();
                bool Flag(string key) => values.TryGetValue(key,out var f) && f is bool enabled && enabled;
                return new ClassroomConfigResult(ClassroomConfigStatus.Ready,new BackendConfig(url,
                    string.Equals(judge as string,"http",StringComparison.OrdinalIgnoreCase)?JudgeMode.Http:JudgeMode.Mock,
                    Flag("community"),Flag("quests"),Flag("demo_mode")));
            }
            catch(FormatException) { return new ClassroomConfigResult(ClassroomConfigStatus.InvalidConfig); }
        }
    }
    public sealed class ClassroomConfigCache
    {
        private readonly Func<Task<HttpResult>> _load;
        private readonly Func<string> _page;
        private readonly object _gate=new object();
        private Task<ClassroomConfigResult> _pending;
        private ClassroomConfigResult _cached;
        private int _generation;
        public ClassroomConfigCache(Func<Task<HttpResult>> load,Func<string> page)
        { _load=load??throw new ArgumentNullException(nameof(load)); _page=page??throw new ArgumentNullException(nameof(page)); }
        public Task<ClassroomConfigResult> GetAsync(bool reload,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            TaskCompletionSource<ClassroomConfigResult> start=null;
            Task<ClassroomConfigResult> task; int generation;
            lock(_gate)
            {
                generation=_generation;
                if(_pending!=null) task=_pending;
                else if(!reload && _cached!=null) task=Task.FromResult(_cached);
                else { start=new TaskCompletionSource<ClassroomConfigResult>(); task=_pending=start.Task; }
            }
            if(start!=null) _=LoadAsync(start,generation);
            return ClassroomAsync.WaitAsync(task,ct);
        }
        private async Task LoadAsync(TaskCompletionSource<ClassroomConfigResult> completion,int generation)
        {
            ClassroomConfigResult result;
            try { result=ClassroomConfigResult.Parse(await _load(),_page()); }
            catch(Exception) { result=new ClassroomConfigResult(ClassroomConfigStatus.FetchFailed); }
            lock(_gate) if(generation==_generation) { _cached=result; _pending=null; }
            completion.TrySetResult(result);
        }
        public void Reset() { lock(_gate) { _generation++; _pending=null; _cached=null; } }
    }
    public static class ClassroomAsync
    {
        // Cancels this waiter, never the shared HTTP request. No Task.Delay/Task.Run on Web.
        public static async Task<T> WaitAsync<T>(Task<T> shared,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(!ct.CanBeCanceled) return await shared;
            var canceled=new TaskCompletionSource<bool>();
            using(ct.Register(()=>canceled.TrySetResult(true)))
            {
                if(await Task.WhenAny(shared,canceled.Task)!=shared) throw new OperationCanceledException(ct);
                ct.ThrowIfCancellationRequested(); return await shared;
            }
        }
    }
    /// <summary>Strict, bounded JSON parsing for public runtime settings, independent of JsonUtility coercion.</summary>
    internal sealed class ConfigJson
    {
        private readonly string _text;
        private int _at;
        private ConfigJson(string text) { _text=text; }
        internal static System.Collections.Generic.Dictionary<string,object> Read(string text)
        {
            if(string.IsNullOrWhiteSpace(text) || text.Length>65536) throw new FormatException();
            var parser=new ConfigJson(text); var result=parser.Value(0) as System.Collections.Generic.Dictionary<string,object>;
            parser.Space(); if(result==null || parser._at!=text.Length) throw new FormatException(); return result;
        }
        private void Space() { while(_at<_text.Length && (_text[_at]==' ' || _text[_at]=='\r' || _text[_at]=='\n' || _text[_at]=='\t')) _at++; }
        private bool Take(char c) { Space(); if(_at<_text.Length && _text[_at]==c) { _at++; return true; } return false; }
        private void Need(char c) { if(!Take(c)) throw new FormatException(); }
        private object Value(int depth)
        {
            if(depth>32) throw new FormatException(); Space(); if(_at>=_text.Length) throw new FormatException();
            if(_text[_at]=='"') return Text();
            if(Take('{'))
            {
                var map=new System.Collections.Generic.Dictionary<string,object>(StringComparer.Ordinal);
                if(Take('}')) return map;
                do { var key=Text(); Need(':'); var value=Value(depth+1); if(map.ContainsKey(key)) throw new FormatException(); map.Add(key,value); } while(Take(','));
                Need('}'); return map;
            }
            if(Take('['))
            {
                if(!Take(']')) { do { Value(depth+1); } while(Take(',')); Need(']'); }
                return new object();
            }
            foreach(var literal in new[]{"true","false","null"})
                if(_at+literal.Length<=_text.Length && string.CompareOrdinal(_text,_at,literal,0,literal.Length)==0)
                { _at+=literal.Length; return literal=="null"?null:(object)(literal=="true"); }
            var start=_at;
            if(_text[_at]=='-') _at++;
            if(_at>=_text.Length) throw new FormatException();
            if(_text[_at]=='0') _at++;
            else { if(_text[_at]<'1' || _text[_at]>'9') throw new FormatException(); Digits(); }
            if(_at<_text.Length && _text[_at]=='.') { _at++; RequireDigits(); }
            if(_at<_text.Length && (_text[_at]=='e' || _text[_at]=='E'))
            { _at++; if(_at<_text.Length && (_text[_at]=='+' || _text[_at]=='-')) _at++; RequireDigits(); }
            if(!double.TryParse(_text.Substring(start,_at-start),System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,out var number)) throw new FormatException();
            return number;
        }
        private void Digits() { while(_at<_text.Length && _text[_at]>='0' && _text[_at]<='9') _at++; }
        private void RequireDigits() { var start=_at; Digits(); if(start==_at) throw new FormatException(); }
        private string Text()
        {
            Need('"'); var result=new System.Text.StringBuilder();
            while(_at<_text.Length)
            {
                var c=_text[_at++]; if(c=='"') return result.ToString(); if(c<32) throw new FormatException();
                if(c!='\\') { result.Append(c); continue; }
                if(_at>=_text.Length) throw new FormatException(); c=_text[_at++];
                switch(c)
                {
                    case '"': case '\\': case '/': result.Append(c); break;
                    case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break;
                    case 'u':
                        if(_at+4>_text.Length || !ushort.TryParse(_text.Substring(_at,4),System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,out var hex)) throw new FormatException();
                        result.Append((char)hex); _at+=4; break;
                    default: throw new FormatException();
                }
            }
            throw new FormatException();
        }
    }
}
