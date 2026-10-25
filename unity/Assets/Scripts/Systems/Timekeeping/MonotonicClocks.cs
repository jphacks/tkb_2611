using ProjectPenguin.Domain.Clock;
using UnityEngine;

namespace ProjectPenguin.Systems.Timekeeping
{
    /// <summary>
    /// Unity の Time.realtimeSinceStartupAsDouble を読む単調時計。起点はアプリの起動 (プロセスごと)。
    /// Unity のドキュメントでは、モバイルを含む多くのプラットフォームでスリープ中の時間も含む。
    /// https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-realtimeSinceStartupAsDouble.html
    /// Time の API なのでメインスレッドから読む。
    /// </summary>
    public sealed class UnityRealtimeMonotonicClock : IMonotonicClock
    {
        public double Seconds => UnityEngine.Time.realtimeSinceStartupAsDouble;
    }

    /// <summary>環境に合った単調時計を選ぶ。テストでは使わず、ManualMonotonicClock を直接渡す。</summary>
    public static class MonotonicClocks
    {
        public static IMonotonicClock CreateDefault()
        {
            return new UnityRealtimeMonotonicClock();
        }
    }
}
