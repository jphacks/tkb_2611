using System;

namespace ProjectPenguin.Domain.Clock
{
    /// <summary><see cref="ServerTrustedClock.ObserveResponse"/> の結果。Accepted 以外は時計を変えない。</summary>
    public enum ServerTimeSample
    {
        /// <summary>基準時刻として採った。</summary>
        Accepted,

        /// <summary>Date ヘッダーが無い (5xx、通信失敗など)。</summary>
        MissingDate,

        /// <summary>Date ヘッダーを HTTP-date として読めない。</summary>
        InvalidDate,

        /// <summary>Age ヘッダーがある。途中のキャッシュが返した応答で、Date は今ではない (RFC 9111 §5.1)。</summary>
        FromCache,

        /// <summary>往復が長すぎる、または送信と受信の順序がおかしい。</summary>
        RoundTripRejected,

        /// <summary>Date が <see cref="ServerTrustedClockOptions.MinPlausibleUtc"/> より前 (サーバーの時計が壊れている)。</summary>
        Implausible,
    }

    /// <summary><see cref="ServerTrustedClock"/> の設定。</summary>
    public sealed class ServerTrustedClockOptions
    {
        /// <summary>
        /// これより長い往復の応答は基準にしない。Unity 側の UnityWebRequest.timeout (18c §4.2 の 25 秒) と同じ値にしておけば、
        /// タイムアウトしなかった応答はすべて使える。誤差は最大で往復の半分 + 1 秒なので、日付の判定 (03:00 区切り) には十分小さい。
        /// </summary>
        public double MaxRoundTripSeconds { get; set; } = 25.0;

        /// <summary>これより前の Date は壊れた時計とみなして捨てる。既定はアプリの日 0 (2026-01-01 JST) の前日。</summary>
        public DateTimeOffset MinPlausibleUtc { get; set; } = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// 自前の FastAPI の応答の Date ヘッダーを基準にし、そこからの経過を単調時計で足す時計 (v5、16d §1.2)。
    ///
    /// <para>基準を一度でも受け取ったプロセスでは <see cref="IsTrusted"/> が true になり、<see cref="UtcNow"/> は
    /// 「Date + 0.5 秒 (秒未満の切り捨ての中央) + (今の単調時計 − 往復の中点の単調時計)」を返す。端末の時計を動かしても変わらない。</para>
    ///
    /// <para>基準が無いあいだ (起動直後、オフライン) は端末の時計 (<c>deviceClock</c>) をそのまま返し、<see cref="IsTrusted"/> は false。
    /// <see cref="Game.GameSession"/> は、信頼できない時計が最後に見た日から <c>MaxCatchUpDays</c> を超えて進んだときだけ日を保留する
    /// (<c>ClockStatus.AheadPending</c>)。保留は、基準を受け取ってから <c>Advance()</c> を呼び直すと、サーバー時刻で解ける。</para>
    ///
    /// <para>基準はメモリだけに持ち、保存しない。単調時計の起点はプロセス (Unity) または端末の起動 (mach_continuous_time) ごとに変わるので、
    /// 保存した基準を次の起動で使うと、端末を再起動したときに誤る。アプリはほぼ毎回の操作でバックエンドと通信するので、起動ごとに取り直せば足りる。</para>
    ///
    /// <para>Domain の規則 (現在時刻を直接読まない) を守るため、端末の時計と単調時計は外から渡す。スレッドセーフではない
    /// (Unity のメインスレッドから呼ぶ。UnityWebRequest の完了もメインスレッドで受け取れる)。</para>
    /// </summary>
    public sealed class ServerTrustedClock : ITrustedClock
    {
        private readonly IClock _device;
        private readonly IMonotonicClock _monotonic;
        private readonly ServerTrustedClockOptions _options;

        private bool _hasAnchor;
        private DateTimeOffset _anchorUtc;
        private double _anchorMonotonic;
        private DateTimeOffset _lastReturned = DateTimeOffset.MinValue;

        public ServerTrustedClock(IClock deviceClock, IMonotonicClock monotonic, ServerTrustedClockOptions options = null)
        {
            _device = deviceClock ?? throw new ArgumentNullException(nameof(deviceClock));
            _monotonic = monotonic ?? throw new ArgumentNullException(nameof(monotonic));
            _options = options ?? new ServerTrustedClockOptions();
        }

        /// <summary>このプロセスでサーバー時刻を受け取っていて、単調時計が戻っていない。</summary>
        public bool IsTrusted
        {
            get
            {
                DropAnchorIfMonotonicWentBack();
                return _hasAnchor;
            }
        }

        /// <summary>最後に採った基準の誤差の上限 (往復の半分 + 0.5 秒)。基準が無ければ null。</summary>
        public TimeSpan? Uncertainty { get; private set; }

        /// <summary>最後に基準を採ったときの「サーバー時刻 − 端末の時計」。大きければ端末の時計がずれている。基準が無ければ null。</summary>
        public TimeSpan? DeviceSkew { get; private set; }

        /// <summary>受け取った応答の数と、基準にした数 (診断用)。</summary>
        public int SamplesSeen { get; private set; }

        public int SamplesAccepted { get; private set; }

        public DateTimeOffset UtcNow
        {
            get
            {
                DropAnchorIfMonotonicWentBack();
                if (!_hasAnchor)
                {
                    return _device.UtcNow;
                }

                var now = _anchorUtc + TimeSpan.FromSeconds(_monotonic.Seconds - _anchorMonotonic);

                // 基準を取り直したとき (往復の誤差で 1 秒ほど戻りうる) に、返す時刻が戻らないようにする。
                if (now < _lastReturned)
                {
                    now = _lastReturned;
                }

                _lastReturned = now;
                return now;
            }
        }

        /// <summary>
        /// 応答のヘッダーを渡す。<paramref name="requestSentMonotonic"/> は送信直前、<paramref name="responseReceivedMonotonic"/> は
        /// 受信直後に <see cref="IMonotonicClock.Seconds"/> を読んだ値。どのエンドポイントの応答でもよく、状態コードも問わない
        /// (RFC 9110 §6.6.1: 時計を持つ origin server は 2xx〜4xx に Date を必ず付け、1xx と 5xx には付けてもよい)。
        /// </summary>
        public ServerTimeSample ObserveResponse(string dateHeader, string ageHeader, double requestSentMonotonic, double responseReceivedMonotonic)
        {
            SamplesSeen++;
            if (string.IsNullOrEmpty(dateHeader))
            {
                return ServerTimeSample.MissingDate;
            }

            // Age があるのは、origin がこのリクエストのために作った応答ではないとき (RFC 9111 §5.1)。
            // 値が非負の整数でなければ Age は無視する (同 §5.1)。
            if (HasValidAge(ageHeader))
            {
                return ServerTimeSample.FromCache;
            }

            if (!HttpDate.TryParse(dateHeader, out var date, _device.UtcNow))
            {
                return ServerTimeSample.InvalidDate;
            }

            return ObserveServerTime(date, requestSentMonotonic, responseReceivedMonotonic);
        }

        /// <summary>読み終えた Date を渡す (ヘッダー以外から時刻を得たとき用)。</summary>
        public ServerTimeSample ObserveServerTime(DateTimeOffset serverDate, double requestSentMonotonic, double responseReceivedMonotonic)
        {
            var roundTrip = responseReceivedMonotonic - requestSentMonotonic;
            if (double.IsNaN(roundTrip) || roundTrip < 0 || roundTrip > _options.MaxRoundTripSeconds)
            {
                return ServerTimeSample.RoundTripRejected;
            }

            if (serverDate < _options.MinPlausibleUtc)
            {
                return ServerTimeSample.Implausible;
            }

            // Date は「応答を作った時刻」の秒未満を切り捨てた値 (RFC 9110 §6.6.1・§5.6.7)。作った瞬間は送信から受信のあいだのどこかなので、
            // 往復の中点に Date + 0.5 秒を対応させる。誤差は最大で往復の半分 + 0.5 秒。
            _anchorUtc = serverDate.ToUniversalTime() + TimeSpan.FromMilliseconds(500);
            _anchorMonotonic = (requestSentMonotonic + responseReceivedMonotonic) / 2.0;
            _hasAnchor = true;
            Uncertainty = TimeSpan.FromSeconds(roundTrip / 2.0 + 0.5);

            var estimateNow = _anchorUtc + TimeSpan.FromSeconds(_monotonic.Seconds - _anchorMonotonic);
            DeviceSkew = estimateNow - _device.UtcNow;
            SamplesAccepted++;
            return ServerTimeSample.Accepted;
        }

        /// <summary>基準を捨てて端末の時計に戻す (テストや、利用者が「時刻をリセット」したとき用)。</summary>
        public void Invalidate()
        {
            _hasAnchor = false;
            Uncertainty = null;
            DeviceSkew = null;
            _lastReturned = DateTimeOffset.MinValue;
        }

        private void DropAnchorIfMonotonicWentBack()
        {
            // 単調時計が基準より前に戻るのは、起点が変わったとき (別プロセス・端末の再起動) だけ。基準は使えない。
            if (_hasAnchor && _monotonic.Seconds < _anchorMonotonic - 1.0)
            {
                Invalidate();
            }
        }

        private static bool HasValidAge(string ageHeader)
        {
            if (string.IsNullOrEmpty(ageHeader))
            {
                return false;
            }

            // リストで来たら最初の要素を使う (RFC 9111 §5.1)
            var first = ageHeader.Split(',')[0].Trim();
            if (first.Length == 0)
            {
                return false;
            }

            foreach (var c in first)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
