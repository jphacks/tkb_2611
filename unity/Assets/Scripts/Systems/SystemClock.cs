using System;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Systems
{
    /// <summary>端末の時計。Domain は現在時刻を直接読まないので、Systems からこれを渡す。</summary>
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
