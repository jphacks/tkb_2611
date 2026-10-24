using System;

namespace ProjectPenguin.Domain.Clock
{
    /// <summary>
    /// 単調に増える経過時間 (秒) の読み口 (v5)。起点は任意で、壁時計とは関係がない。
    /// <see cref="ServerTrustedClock"/> は、サーバー時刻を受け取ったときの値と今の値の差を経過時間として足す。
    /// 本番の実装は Systems 側に置く。iOS 実機は mach_continuous_time (スリープ中も進む) をネイティブプラグインで読み、
    /// Editor と他の環境は Time.realtimeSinceStartupAsDouble を読む。EditMode テストは <see cref="ManualMonotonicClock"/> を渡す。
    /// </summary>
    public interface IMonotonicClock
    {
        /// <summary>起点からの経過秒。同じプロセスの中では減らない。</summary>
        double Seconds { get; }
    }

    /// <summary>手で進める単調時計。EditMode テストと dotnet-harness 用。</summary>
    public sealed class ManualMonotonicClock : IMonotonicClock
    {
        public ManualMonotonicClock(double startSeconds = 0.0)
        {
            Seconds = startSeconds;
        }

        public double Seconds { get; private set; }

        public void Advance(TimeSpan delta)
        {
            Advance(delta.TotalSeconds);
        }

        public void Advance(double seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "ManualMonotonicClock only moves forward");
            }

            Seconds += seconds;
        }

        /// <summary>アプリの再起動 (プロセスが変わり、起点が変わる) を模す。値が戻ってもよいのはこのときだけ。</summary>
        public void SimulateProcessRestart(double newStartSeconds = 0.0)
        {
            Seconds = newStartSeconds;
        }
    }
}
