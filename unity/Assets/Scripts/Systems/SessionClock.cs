using System;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Systems
{
    /// <summary>
    /// 氷の状態機械に渡す時計。保存した操作を再生する間だけ、その操作の時刻に止める (<see cref="Pin"/>)。
    /// 再生が終われば端末の時計 (開発ビルドの早送りを含む) に戻る。
    /// </summary>
    public sealed class SessionClock : ITrustedClock
    {
        private readonly OffsetClock _live;
        private DateTimeOffset? _pinned;

        public SessionClock(IClock device)
        {
            _live = new OffsetClock(device ?? throw new ArgumentNullException(nameof(device)));
        }

        public DateTimeOffset UtcNow => _pinned ?? _live.UtcNow;

        /// <summary>再生中は保存した操作の時刻なので信頼する。それ以外は、包んだ時計 (サーバー時刻で確かめた時計など) に従う。</summary>
        public bool IsTrusted => _pinned.HasValue || _live.IsTrusted;

        /// <summary>開発ビルドで進めた時間。</summary>
        public TimeSpan Offset => _live.Offset;

        public void Pin(DateTimeOffset at) => _pinned = at;

        public void Unpin() => _pinned = null;

        public void AdvanceDays(int days) => _live.AdvanceDays(days);

        public void Advance(TimeSpan delta) => _live.Advance(delta);

        public void ResetOffset() => _live.Reset();
    }
}
