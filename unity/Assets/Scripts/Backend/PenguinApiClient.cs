using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// backend への通り道 (gateway)。契約 (api/openapi.yaml) のパス・本文の形・エラーの形・再試行をここに集め、
    /// 判定・みんなの氷山・お題・水・すれ違いの口 (Http*) はこれだけを使う。
    /// 写真と応答の本文はログにも例外の文にも入れない (.claude/rules/privacy.md)。
    /// </summary>
    public sealed class PenguinApiClient
    {
        public const string JudgePath = "/v1/eco-photo/judge";
        public const string HealthPath = "/v1/health";
        public const string CommunityGoalPath = "/v1/community/goal";
        public const string QuestsPath = "/v1/quests";
        public const string EncountersPath = "/v1/encounters";
        public const string WaterEstimatePath = "/v1/water/estimate";

        private readonly string _baseUrl;
        private readonly IHttpTransport _transport;
        private readonly int _retries;

        public PenguinApiClient(string baseUrl, IHttpTransport transport, int retries = BackendLimits.Retries)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentException("backend の URL がありません", nameof(baseUrl));
            }

            _baseUrl = baseUrl.TrimEnd('/');
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _retries = Math.Max(0, retries);
        }

        internal Task<HealthDto> HealthAsync(CancellationToken cancellationToken) =>
            SendAsync<HealthDto>(UnityWebRequest.kHttpVerbGET, HealthPath, null, null, cancellationToken);

        internal Task<JudgeResultDto> JudgeAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            var boundary = "penguin-" + Guid.NewGuid().ToString("N");
            var body = Multipart(boundary, "image", "photo.jpg", "image/jpeg", jpeg);
            // 写真 1 枚ごとの乱数。送り直し (下の SendAsync の再試行) でも同じ値を送り、みんなの氷山で 2 回数えさせない。
            // 端末や人とは結び付かない (api/openapi.yaml のプライバシー)。
            var idempotencyKey = Guid.NewGuid().ToString("N");
            return SendAsync<JudgeResultDto>(UnityWebRequest.kHttpVerbPOST, JudgePath, body, "multipart/form-data; boundary=" + boundary,
                cancellationToken, idempotencyKey);
        }

        internal Task<WaterEstimateDto> WaterEstimateAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            var boundary = "penguin-" + Guid.NewGuid().ToString("N");
            var body = Multipart(boundary, "image", "water.jpg", "image/jpeg", jpeg);
            // 判定と同じく写真 1 枚ごとの乱数。送り直し (下の SendAsync の再試行) の見分けに使う。
            var idempotencyKey = Guid.NewGuid().ToString("N");
            return SendAsync<WaterEstimateDto>(UnityWebRequest.kHttpVerbPOST, WaterEstimatePath, body, "multipart/form-data; boundary=" + boundary,
                cancellationToken, idempotencyKey);
        }

        internal Task<CommunityGoalDto> CommunityGoalAsync(CancellationToken cancellationToken) =>
            SendAsync<CommunityGoalDto>(UnityWebRequest.kHttpVerbGET, CommunityGoalPath, null, null, cancellationToken);

        internal Task<QuestListDto> QuestsAsync(CancellationToken cancellationToken) =>
            SendAsync<QuestListDto>(UnityWebRequest.kHttpVerbGET, QuestsPath, null, null, cancellationToken);

        internal Task<RedeemResultDto> RedeemAsync(string questId, string code, CancellationToken cancellationToken)
        {
            var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RedeemRequestDto { code = code ?? "" }));
            var path = QuestsPath + "/" + Uri.EscapeDataString(questId ?? "") + "/redeem";
            return SendAsync<RedeemResultDto>(UnityWebRequest.kHttpVerbPOST, path, body, "application/json", cancellationToken);
        }

        /// <summary>すれ違いの報告。同じ報告は何度送っても同じ結果になる (契約) ので、送り直しても数えすぎない。</summary>
        internal Task<EncounterResultDto> ReportEncounterAsync(string day, string selfToken, string peerToken, CancellationToken cancellationToken)
        {
            var body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new EncounterReportDto { day = day, self_token = selfToken, peer_token = peerToken }));
            return SendAsync<EncounterResultDto>(UnityWebRequest.kHttpVerbPOST, EncountersPath, body, "application/json", cancellationToken);
        }

        private async Task<T> SendAsync<T>(string method, string path, byte[] body, string contentType, CancellationToken cancellationToken,
            string idempotencyKey = null)
            where T : class
        {
            var spec = new HttpRequestSpec(method, _baseUrl + path, body, contentType, idempotencyKey: idempotencyKey);
            for (var attempt = 0; ; attempt++)
            {
                var result = await _transport.SendAsync(spec, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return Read<T>(result);
                }
                catch (BackendException e) when (e.Retryable && attempt < _retries)
                {
                    // 寝起きの失敗や一時的な混雑を 1 回だけ救う。待たずにすぐ送り直す
                    // (Web では Task.Delay が効かないため。Retry-After が長いときは送り直さない)。
                    if (e.RetryAfterSeconds > 2)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>応答を契約の形として読む。エラーの形なら <see cref="BackendException"/> にする。</summary>
        internal static T Read<T>(HttpResult result)
            where T : class
        {
            if (result.ConnectionFailed)
            {
                throw new BackendException(BackendErrorCodes.Network, null, true);
            }

            if (result.Status >= 400)
            {
                throw ErrorOf(result);
            }

            try
            {
                var dto = JsonUtility.FromJson<T>(result.Text);
                if (dto != null)
                {
                    return dto;
                }
            }
            catch (ArgumentException)
            {
                // 下で同じ扱いにする。本文は例外の文に入れない。
            }

            throw new BackendException(BackendErrorCodes.Internal, null, true, result.Status);
        }

        private static BackendException ErrorOf(HttpResult result)
        {
            var retryAfter = int.TryParse(result.RetryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? Math.Max(0, s) : 0;
            ErrorBodyDto error = null;
            try
            {
                error = JsonUtility.FromJson<ErrorResponseDto>(result.Text)?.error;
            }
            catch (ArgumentException)
            {
                // CDN や関数の入口が返す HTML などはエラーの形ではない。HTTP の状態だけで決める。
            }

            if (error == null || string.IsNullOrEmpty(error.code))
            {
                var retryable = result.Status >= 500 || result.Status == 429;
                var code = result.Status == 413 ? BackendErrorCodes.ImageTooLarge
                    : result.Status == 429 ? BackendErrorCodes.RateLimited
                    : result.Status == 503 ? BackendErrorCodes.UpstreamUnavailable
                    : BackendErrorCodes.Internal;
                return new BackendException(code, null, retryable, result.Status, retryAfter);
            }

            var normalized = BackendErrorCodes.Normalize(error.code);
            return new BackendException(normalized, error.message, error.retryable, result.Status, retryAfter);
        }

        /// <summary>multipart/form-data の本文 (ファイル 1 つ)。</summary>
        internal static byte[] Multipart(string boundary, string field, string fileName, string contentType, byte[] content)
        {
            using var stream = new MemoryStream(content.Length + 256);
            var head = Encoding.UTF8.GetBytes(
                $"--{boundary}\r\nContent-Disposition: form-data; name=\"{field}\"; filename=\"{fileName}\"\r\nContent-Type: {contentType}\r\n\r\n");
            var tail = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(content, 0, content.Length);
            stream.Write(tail, 0, tail.Length);
            return stream.ToArray();
        }

        /// <summary>契約の日時 (RFC 3339) を読む。読めなければ null。</summary>
        internal static DateTimeOffset? ParseTime(string value) =>
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : (DateTimeOffset?)null;
    }
}
