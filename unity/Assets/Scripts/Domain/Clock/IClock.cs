using System;

namespace ProjectPenguin.Domain.Clock
{
    /// <summary>
    /// 現在時刻の注入口。Domain は DateTime.Now / UtcNow を直接呼ばない (.claude/rules/unity.md)。
    /// 本番は Systems 側で DateTimeOffset.UtcNow を返す実装を渡し、デモと EditMode テストは <see cref="ManualClock"/> を渡す。
    /// </summary>
    public interface IClock
    {
        /// <summary>現在の UTC 時刻。</summary>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary>
    /// 時刻が確かめられているかを示せる時計 (v4)。<see cref="Game.GameSession"/> は、IsTrusted が false の時計で
    /// 最後に見た日から <see cref="Game.GameParameters.MaxCatchUpDays"/> を超えて進んだとき、その日を確定させずに保留する。
    /// 本番の Systems 側の時計は、サーバーの Date ヘッダーで同期できているときだけ true を返す (16d §1.2)。
    /// このインターフェースを実装しない <see cref="IClock"/> は信頼できないものとして扱う。
    /// </summary>
    public interface ITrustedClock : IClock
    {
        bool IsTrusted { get; }
    }

    /// <summary>
    /// 手で進める時計。EditMode テストと、Development ビルドの「時間を進める」ボタン用。
    /// 自分では現在時刻を読まないので、開始時刻は呼び出し側が渡す。
    /// </summary>
    public sealed class ManualClock : ITrustedClock
    {
        /// <summary>テストでは既定で信頼する。端末の時計を模すときは false にする。</summary>
        public bool IsTrusted { get; set; } = true;

        public ManualClock(DateTimeOffset start)
        {
            UtcNow = start.ToUniversalTime();
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "ManualClock only moves forward");
            }

            UtcNow = UtcNow + delta;
        }

        public void AdvanceDays(int days)
        {
            Advance(TimeSpan.FromDays(days));
        }

        public void Set(DateTimeOffset utcNow)
        {
            UtcNow = utcNow.ToUniversalTime();
        }
    }

    /// <summary>
    /// 早送りできる時計。実時計 (inner) に、デモで進めた分のずれを足して返す。
    /// 「時間を進める」を押したあとも実時間は流れ続けるので、録画中の操作が自然に見える。
    /// </summary>
    public sealed class OffsetClock : ITrustedClock
    {
        /// <summary>
        /// デモで意図して進めた時計なので、進めていれば信頼する。進めていないときは、内側が <see cref="ITrustedClock"/> なら
        /// その判断に従う (v5。<see cref="ServerTrustedClock"/> を包んでもサーバー時刻の確認が効くように)。
        /// 内側が <see cref="ITrustedClock"/> でなければ v4 と同じく信頼する。
        /// </summary>
        public bool IsTrusted => Offset > TimeSpan.Zero || !(_inner is ITrustedClock trusted) || trusted.IsTrusted;

        private readonly IClock _inner;

        public OffsetClock(IClock inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>進めた合計。保存しておけば再起動後も同じずれを再現できる。</summary>
        public TimeSpan Offset { get; private set; }

        public DateTimeOffset UtcNow => _inner.UtcNow + Offset;

        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "OffsetClock only moves forward");
            }

            Offset += delta;
        }

        public void AdvanceDays(int days)
        {
            Advance(TimeSpan.FromDays(days));
        }

        public void Reset()
        {
            Offset = TimeSpan.Zero;
        }
    }
}
