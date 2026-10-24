using System;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// backend が返すエラーの code (api/openapi.yaml の ErrorCode)。
    /// 契約との一致は scripts/check_api_contract.py が確かめる。
    /// </summary>
    public static class BackendErrorCodes
    {
        public const string ImageMissing = "image_missing";
        public const string ImageInvalid = "image_invalid";
        public const string ImageTooLarge = "image_too_large";
        public const string CodeInvalid = "code_invalid";
        public const string QuestClosed = "quest_closed";
        public const string AlreadyRedeemed = "already_redeemed";
        public const string TokenInvalid = "token_invalid";
        public const string DayInvalid = "day_invalid";
        public const string RateLimited = "rate_limited";
        public const string UpstreamUnavailable = "upstream_unavailable";
        public const string Internal = "internal";

        /// <summary>接続できなかった・タイムアウト・応答が読めなかった。サーバーは返さない、クライアントだけの code。</summary>
        public const string Network = "network";

        private static readonly string[] Known =
        {
            ImageMissing, ImageInvalid, ImageTooLarge, CodeInvalid, QuestClosed, AlreadyRedeemed, TokenInvalid, DayInvalid, RateLimited,
            UpstreamUnavailable, Internal,
        };

        /// <summary>知らない code は <see cref="Internal"/> として扱う (契約の前方互換の規則)。</summary>
        public static string Normalize(string code) => Array.IndexOf(Known, code) >= 0 ? code : Internal;
    }

    /// <summary>
    /// backend の呼び出しが失敗した。<see cref="UserMessage"/> は画面にそのまま出せる日本語で、写真や応答の中身は含まない。
    /// </summary>
    public sealed class BackendException : Exception
    {
        public BackendException(string code, string userMessage, bool retryable, long status = 0, int retryAfterSeconds = 0)
            : base($"backend error {code} (HTTP {status})")
        {
            Code = code;
            UserMessage = string.IsNullOrEmpty(userMessage) ? DefaultMessage(code) : userMessage;
            Retryable = retryable;
            Status = status;
            RetryAfterSeconds = retryAfterSeconds;
        }

        /// <summary><see cref="BackendErrorCodes"/> のどれか。</summary>
        public string Code { get; }

        public string UserMessage { get; }

        /// <summary>少し待ってもう一度呼べば通るかもしれない。</summary>
        public bool Retryable { get; }

        /// <summary>HTTP の状態。接続できなかったときは 0。</summary>
        public long Status { get; }

        /// <summary>Retry-After の秒数。無ければ 0。</summary>
        public int RetryAfterSeconds { get; }

        public static string DefaultMessage(string code) => code switch
        {
            BackendErrorCodes.Network => "通信できなかったよ。電波のいいところでもう一度ためしてね",
            BackendErrorCodes.ImageTooLarge => "写真が大きすぎたよ。もう一度撮ってね",
            BackendErrorCodes.ImageMissing or BackendErrorCodes.ImageInvalid => "この写真は読めなかったよ。もう一度撮ってね",
            BackendErrorCodes.RateLimited => "混みあってるみたい。少し待ってからもう一度ためしてね",
            BackendErrorCodes.UpstreamUnavailable => "いまは写真を見分けられないみたい。少し待ってね",
            _ => "うまくいかなかったよ。少し待ってからもう一度ためしてね",
        };
    }
}
