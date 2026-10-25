using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ProjectPenguin.Systems
{
    /// <summary>今日の歩数の取り出し口。問い合わせは非同期なので、要求してから <see cref="TryTake"/> で受け取る。</summary>
    public interface IStepSource
    {
        bool IsSupported { get; }

        /// <summary><paramref name="fromUtc"/> から今までの歩数を問い合わせる。</summary>
        void Request(DateTimeOffset fromUtc);

        /// <summary>
        /// 問い合わせの結果が出ていれば true。<paramref name="fromUtc"/> はその結果を求めた問い合わせの開始時刻。
        /// 問い合わせが重なったとき、日付が変わる前の問い合わせの応答 (前日の歩数) を今日として受け取らないよう、呼ぶ側が見比べる。
        /// </summary>
        bool TryTake(out int steps, out DateTimeOffset fromUtc);
    }

    public static class StepSources
    {
        /// <summary>実行環境に合った歩数の取り出し口。</summary>
        public static IStepSource Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosPedometerStepSource();
#else
            return new FakeStepSource();
#endif
        }
    }

    /// <summary>Editor と iOS 以外。開発用の操作で歩数を足せる。</summary>
    public sealed class FakeStepSource : IStepSource
    {
        private bool _pending;
        private DateTimeOffset _from;

        /// <summary>今日歩いたことにする歩数。</summary>
        public int Steps { get; set; }

        public bool IsSupported => true;

        public void Request(DateTimeOffset fromUtc)
        {
            _pending = true;
            _from = fromUtc;
        }

        public bool TryTake(out int steps, out DateTimeOffset fromUtc)
        {
            steps = Steps;
            fromUtc = _from;
            if (!_pending)
            {
                return false;
            }

            _pending = false;
            return true;
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>iOS の CMPedometer (Assets/Plugins/iOS/PenguinPedometer.mm)。</summary>
    public sealed class IosPedometerStepSource : IStepSource
    {
        [DllImport("__Internal")]
        private static extern bool PenguinPedometer_IsAvailable();

        [DllImport("__Internal")]
        private static extern void PenguinPedometer_Query(double fromUnixSeconds);

        [DllImport("__Internal")]
        private static extern int PenguinPedometer_State();

        [DllImport("__Internal")]
        private static extern int PenguinPedometer_Steps();

        [DllImport("__Internal")]
        private static extern double PenguinPedometer_ResultFrom();

        private bool _pending;

        public bool IsSupported => PenguinPedometer_IsAvailable();

        public void Request(DateTimeOffset fromUtc)
        {
            _pending = true;
            PenguinPedometer_Query(fromUtc.ToUnixTimeMilliseconds() / 1000.0);
        }

        public bool TryTake(out int steps, out DateTimeOffset fromUtc)
        {
            steps = 0;
            fromUtc = default;
            if (!_pending)
            {
                return false;
            }

            var state = PenguinPedometer_State();
            if (state == 1)
            {
                return false;
            }

            _pending = false;
            if (state != 2)
            {
                return false;
            }

            steps = PenguinPedometer_Steps();
            fromUtc = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(PenguinPedometer_ResultFrom() * 1000.0));
            return steps >= 0;
        }
    }
#endif
}
