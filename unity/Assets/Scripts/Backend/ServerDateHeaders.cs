using ProjectPenguin.Domain.Clock;
using UnityEngine.Networking;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// FastAPI への通信のたびに、応答の Date ヘッダーを <see cref="ServerTrustedClock"/> に渡す (v5、19b-clock-save.md)。
    /// どのエンドポイントの応答でもよい。レシート解析の POST でも、起動時の GET /healthz でも同じように呼ぶ。
    ///
    /// 使い方 (コルーチンの中):
    /// <code>
    /// using var req = UnityWebRequest.Get(url);
    /// req.timeout = 25;                                 // 18c §4.2。既定の 0 は無制限
    /// var sent = monotonic.Seconds;
    /// yield return req.SendWebRequest();
    /// ServerDateHeaders.Report(req, clock, sent, monotonic.Seconds);
    /// </code>
    /// GetResponseHeader は名前の大文字小文字を区別せず、無ければ null を返す。リダイレクトがあれば最後の応答のヘッダーを見る
    /// (https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.GetResponseHeader.html)。
    /// </summary>
    public static class ServerDateHeaders
    {
        public static ServerTimeSample Report(UnityWebRequest request, ServerTrustedClock clock, double requestSentMonotonic, double responseReceivedMonotonic)
        {
            if (request == null || clock == null)
            {
                return ServerTimeSample.MissingDate;
            }

            // 接続できなかった (ConnectionError) ときは応答が無いので、ヘッダーも無い。HTTP エラー (4xx) の応答の Date は使える。
            return clock.ObserveResponse(
                request.GetResponseHeader("Date"),
                request.GetResponseHeader("Age"),
                requestSentMonotonic,
                responseReceivedMonotonic);
        }
    }
}
