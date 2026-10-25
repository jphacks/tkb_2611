using System;
using System.Threading;
using System.Threading.Tasks;
#if !RAID_EXTERNAL_CHECKS
using UnityEngine.Networking;
#endif

namespace ProjectPenguin.Backend
{
    /// <summary>送る HTTP の要求 1 本。本文は写真を含みうるので、ログに出さない。</summary>
    public sealed class HttpRequestSpec
    {
        public HttpRequestSpec(string method, string url, byte[] body = null, string contentType = null, int timeoutSeconds = BackendLimits.TimeoutSeconds, string idempotencyKey = null, System.Collections.Generic.IReadOnlyDictionary<string,string> headers = null)
        {
            Method = method;
            Url = url;
            Body = body;
            ContentType = contentType;
            TimeoutSeconds = timeoutSeconds;
            IdempotencyKey = idempotencyKey;
            Headers = new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(headers == null ? new System.Collections.Generic.Dictionary<string,string>() : new System.Collections.Generic.Dictionary<string,string>(headers));
        }

        public System.Collections.Generic.IReadOnlyDictionary<string,string> Headers { get; }
        public string IdempotencyKey { get; }
        public string Method { get; }
        public string Url { get; }
        public byte[] Body { get; }
        public string ContentType { get; }
        public int TimeoutSeconds { get; }
    }

    /// <summary>受け取った応答。接続できなかった・タイムアウトのときは <see cref="ConnectionFailed"/>。</summary>
    public sealed class HttpResult
    {
        public HttpResult(long status, string text, string retryAfter = null)
        {
            Status = status;
            Text = text ?? "";
            RetryAfter = retryAfter;
        }

        private HttpResult()
        {
            Text = "";
            ConnectionFailed = true;
        }

        public static HttpResult Failed() => new HttpResult();

        public long Status { get; }
        public string Text { get; }
        public string RetryAfter { get; }
        public bool ConnectionFailed { get; }
    }

    /// <summary>HTTP の送り口。EditMode テストでは偽物に差し替える。</summary>
    public interface IHttpTransport
    {
        /// <summary>
        /// 要求を送り、応答を返す。HTTP のエラー (4xx・5xx) も例外にせず <see cref="HttpResult"/> で返す。
        /// キャンセルされたら <see cref="OperationCanceledException"/> を投げる。
        /// </summary>
        Task<HttpResult> SendAsync(HttpRequestSpec request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// backend の応答のたびに呼ばれる。応答の Date ヘッダーで端末の時計を補正する (<see cref="ServerDateHeaders"/>)。
    /// GameStateSystem が実装する。
    /// </summary>
#if !RAID_EXTERNAL_CHECKS
    public interface IServerResponseObserver
    {
        /// <summary>単調時計の今の秒。</summary>
        double MonotonicSeconds { get; }

        void ObserveServerResponse(UnityWebRequest request, double sentMonotonic, double receivedMonotonic);
    }

    /// <summary>
    /// UnityWebRequest で送る。Web ビルドで効く書き方に限る (#85):
    /// タイムアウトは <see cref="UnityWebRequest.timeout"/>、キャンセルは <see cref="UnityWebRequest.Abort"/>。
    /// CancellationTokenSource のタイムアウト・Task.Delay・Task.Run・ConfigureAwait(false) は使わない (Web ではスレッドもタイマーも無い)。
    /// </summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        private readonly IServerResponseObserver _observer;

        public UnityWebRequestTransport(IServerResponseObserver observer = null)
        {
            _observer = observer;
        }

        public async Task<HttpResult> SendAsync(HttpRequestSpec spec, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var upload = spec.Body != null ? new UploadHandlerRaw(spec.Body) { contentType = spec.ContentType } : null;
            using var request = new UnityWebRequest(spec.Url, spec.Method, new DownloadHandlerBuffer(), upload)
            {
                timeout = spec.TimeoutSeconds,
                redirectLimit = 0,
                disposeUploadHandlerOnDispose = true,
                disposeDownloadHandlerOnDispose = true,
            };
            if (spec.ContentType != null && upload == null)
            {
                request.SetRequestHeader("Content-Type", spec.ContentType);
            }

            foreach (var header in spec.Headers) request.SetRequestHeader(header.Key, header.Value);
            if (spec.IdempotencyKey != null) request.SetRequestHeader("Idempotency-Key", spec.IdempotencyKey);
            request.SetRequestHeader("Accept", "application/json");
            var sent = _observer?.MonotonicSeconds ?? 0;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested) { request.Abort(); cancellationToken.ThrowIfCancellationRequested(); }
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested();

            _observer?.ObserveServerResponse(request, sent, _observer.MonotonicSeconds);

            // 接続できなかった・タイムアウトは応答が無い。HTTP のエラーは本文 (エラーの形) を読む。
            // file:// (Editor・iOS の StreamingAssets) は成功しても responseCode が 0 なので、result で見分ける。
            if (request.result == UnityWebRequest.Result.ConnectionError
                || (request.responseCode == 0 && request.result != UnityWebRequest.Result.Success))
            {
                return HttpResult.Failed();
            }

            return new HttpResult(request.responseCode, request.downloadHandler?.text, request.GetResponseHeader("Retry-After"));
        }
    }
#endif
}
