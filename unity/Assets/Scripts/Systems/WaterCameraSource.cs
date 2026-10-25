using System;
using ProjectPenguin.Backend;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ProjectPenguin.Systems
{
    /// <summary>ペットボトルを撮る操作の結果。</summary>
    public enum WaterCaptureStatus
    {
        /// <summary>撮れた。<see cref="WaterCapture.Jpeg"/> を見積もりに送る。</summary>
        Captured,

        /// <summary>撮らずに閉じた。</summary>
        Cancelled,

        /// <summary>画像を作れなかった。</summary>
        Failed,

        /// <summary>カメラが使えない端末 (シミュレーターなど)。</summary>
        Unavailable,

        /// <summary>カメラの許可がない。設定アプリで許可してもらう。</summary>
        Denied,
    }

    /// <summary>
    /// 撮ったペットボトルの写真。<see cref="BackendLimits.MaxImageBytes"/> に収めた JPEG と、使い回しを弾くためのハッシュ。
    /// 写真は見積もりに送ったら捨てる。保存もログ出力もしない (.claude/rules/privacy.md)。
    /// </summary>
    public sealed class WaterCapture
    {
        public WaterCapture(byte[] jpeg, string imageHash)
        {
            Jpeg = jpeg ?? throw new ArgumentNullException(nameof(jpeg));
            ImageHash = imageHash ?? "";
        }

        public byte[] Jpeg { get; }

        /// <summary>JPEG の SHA-256 (hex)。</summary>
        public string ImageHash { get; }
    }

    /// <summary>
    /// 飲みきった空のペットボトルをその場で撮る口 (docs/decisions.md の工場ゲームの水)。写真ライブラリからは選ばせない。
    /// <see cref="IStepSource"/> と同じく、要求 (<see cref="RequestCapture"/>) してから <see cref="TryTake"/> で受け取る。
    /// </summary>
    public interface IWaterCameraSource
    {
        /// <summary>カメラを開く。撮っている間に呼んでも何もしない。</summary>
        void RequestCapture();

        /// <summary>撮る操作が終わっていれば true。<paramref name="capture"/> は <see cref="WaterCaptureStatus.Captured"/> のときだけ入る。</summary>
        bool TryTake(out WaterCaptureStatus status, out WaterCapture capture);
    }

    public static class WaterCameraSources
    {
        /// <summary>実行環境に合った口。</summary>
        public static IWaterCameraSource Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosWaterCameraSource();
#else
            return new FakeWaterCameraSource();
#endif
        }
    }

    /// <summary>
    /// Editor と iOS 以外。カメラの代わりに、撮るたびに少しずつ色の違う合成の画像を返す (ハッシュが毎回変わる)。
    /// ペットボトルは写っていないので、backend につないでいれば撮り直しが返る。backend なしなら見本の見積もりが返る。
    /// </summary>
    public sealed class FakeWaterCameraSource : IWaterCameraSource
    {
        private bool _pending;
        private byte _shade = 96;

        public void RequestCapture()
        {
            _pending = true;
        }

        public bool TryTake(out WaterCaptureStatus status, out WaterCapture capture)
        {
            status = WaterCaptureStatus.Cancelled;
            capture = null;
            if (!_pending)
            {
                return false;
            }

            _pending = false;
            _shade = (byte)(_shade + 7);
            var jpeg = Synthetic(_shade);
            status = WaterCaptureStatus.Captured;
            capture = new WaterCapture(jpeg, $"fake-{_shade}");
            return true;
        }

        private static byte[] Synthetic(byte shade)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            try
            {
                var pixels = new Color32[64 * 64];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(shade, shade, shade, 255);
                }

                texture.SetPixels32(pixels);
                return texture.EncodeToJPG(80);
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>iOS のカメラ (Assets/Plugins/iOS/PenguinWaterCamera.swift)。縮め直す段は <see cref="PhotoFit.Steps"/> を渡す。</summary>
    public sealed class IosWaterCameraSource : IWaterCameraSource
    {
        private const int StateBusy = 1;
        private const int StateDone = 2;
        private const int StateCancelled = 3;
        private const int StateUnavailable = 5;
        private const int StateDenied = 6;

        [DllImport("__Internal")]
        private static extern void PenguinWaterCamera_Capture(int[] sides, int[] qualities, int count, int maxBytes);

        [DllImport("__Internal")]
        private static extern int PenguinWaterCamera_State();

        [DllImport("__Internal")]
        private static extern string PenguinWaterCamera_TakeResult();

        private bool _pending;

        public void RequestCapture()
        {
            if (_pending)
            {
                return;
            }

            var steps = PhotoFit.Steps;
            var sides = new int[steps.Length];
            var qualities = new int[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                sides[i] = steps[i].MaxSide;
                qualities[i] = steps[i].Quality;
            }

            _pending = true;
            Debug.Log("WaterCamera: カメラを開きます");
            PenguinWaterCamera_Capture(sides, qualities, steps.Length, BackendLimits.MaxImageBytes);
        }

        public bool TryTake(out WaterCaptureStatus status, out WaterCapture capture)
        {
            status = WaterCaptureStatus.Failed;
            capture = null;
            if (!_pending)
            {
                return false;
            }

            var state = PenguinWaterCamera_State();
            if (state == StateBusy)
            {
                return false;
            }

            _pending = false;
            // 写真の中身は出さず、どこで止まったかが分かる状態の番号だけを出す。
            Debug.Log($"WaterCamera: 撮る操作が終わりました (状態 {state})");
            switch (state)
            {
                case StateCancelled:
                    status = WaterCaptureStatus.Cancelled;
                    return true;
                case StateUnavailable:
                    status = WaterCaptureStatus.Unavailable;
                    return true;
                case StateDenied:
                    status = WaterCaptureStatus.Denied;
                    return true;
                case StateDone:
                    capture = Parse(PenguinWaterCamera_TakeResult());
                    status = capture != null ? WaterCaptureStatus.Captured : WaterCaptureStatus.Failed;
                    return true;
                default:
                    return true;
            }
        }

        [Serializable]
        private sealed class ResultDto
        {
            public string hash;
            public string jpeg;
        }

        private static WaterCapture Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                var dto = JsonUtility.FromJson<ResultDto>(json);
                if (dto == null || string.IsNullOrEmpty(dto.jpeg))
                {
                    return null;
                }

                return new WaterCapture(Convert.FromBase64String(dto.jpeg), dto.hash);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            {
                // 中身 (写真) は例外の文にもログにも出さない。
                return null;
            }
        }
    }
#endif
}
