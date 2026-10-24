using System;
using System.Collections;
using ProjectPenguin.Backend;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// スキャン画面にカメラ映像を映し、シャッターで今のフレームを JPEG にする。
    /// 写真はメモリ上だけで扱い、ディスクにもログにも残さない (.claude/rules/privacy.md)。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ReceiptCameraPreview : MonoBehaviour
    {
        private const int RequestedWidth = 1920;
        private const int RequestedHeight = 1080;

        // 起動直後の WebCamTexture は、実際の解像度が決まるまで 16x16 を返す。
        private const int PlaceholderSize = 16;

        private VisualElement _viewport;
        private Image _preview;
        private Label _status;

        private WebCamTexture _webcam;
        private Coroutine _startRoutine;
        private bool _awaitingPermission;
        private bool _wantsPreview;

        private int _appliedAngle;
        private bool _appliedMirrored;
        private Vector2 _appliedViewportSize;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _viewport = root.Q(UiNames.Scan.PreviewViewport);
            _preview = root.Q<Image>(UiNames.Scan.CameraPreview);
            _status = root.Q<Label>(UiNames.Scan.CameraStatus);
            _preview.scaleMode = ScaleMode.ScaleAndCrop;
        }

        private void OnDisable()
        {
            _wantsPreview = false;
            ReleaseCamera();
        }

        private void Update()
        {
            if (_webcam != null && _webcam.width > PlaceholderSize)
            {
                ApplyOrientationIfChanged();
            }
        }

        // アプリが裏に回っている間はカメラを掴んだままにしない。戻ってきたときはスキャン画面にいる場合だけ再開する。
        // 権限ダイアログの表示自体もアプリを一時停止させるので、応答待ちのリクエストは止めない
        // (止めると復帰時に同じダイアログをもう一度出してしまう)。
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                ReleaseCamera(keepPermissionRequest: true);
            }
            else if (_wantsPreview)
            {
                StartPreview();
            }
        }

        public void StartPreview()
        {
            _wantsPreview = true;
            if (_webcam == null && _startRoutine == null)
            {
                _startRoutine = StartCoroutine(StartRoutine());
            }
        }

        public void StopPreview()
        {
            _wantsPreview = false;
            ReleaseCamera();
        }

        /// <summary>カメラ映像が映っているか。</summary>
        public bool IsCapturing => _webcam != null && _webcam.isPlaying && _webcam.width > PlaceholderSize;

        /// <summary>
        /// 今のフレームを、画面に見えている向きに直して JPEG にする。長い辺は <paramref name="maxSize"/> まで縮める。
        /// カメラが動いていなければ null。返すバイト列は呼び出し側が判定に渡したら捨てる (保存しない)。
        /// </summary>
        public byte[] CaptureJpeg(int maxSize = BackendLimits.MaxImageSide, int quality = 80)
        {
            var frame = GrabFrame();
            return frame == null ? null : Encode(frame, maxSize, quality);
        }

        /// <summary>
        /// 今のフレームを 1 回だけ読み、送れる大きさ (<see cref="BackendLimits.MaxImageBytes"/>) に収まるまで
        /// <see cref="PhotoFit"/> の順に縮め直した JPEG にする。利用者に「大きすぎる」を見せないため、シャッターはこちらを使う。
        /// カメラが動いていなければ null。
        /// </summary>
        public byte[] CaptureJpegWithin()
        {
            var frame = GrabFrame();
            return frame == null ? null : PhotoFit.Fit(step => Encode(frame, step.MaxSide, step.Quality));
        }

        /// <summary>シャッターを押した瞬間のフレーム。縮め直すあいだに映像が進んでも、同じ 1 枚から作る。</summary>
        private sealed class Frame
        {
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public int Angle;
            public bool Mirrored;
        }

        private Frame GrabFrame()
        {
            if (!IsCapturing)
            {
                return null;
            }

            return new Frame
            {
                Pixels = _webcam.GetPixels32(),
                Width = _webcam.width,
                Height = _webcam.height,
                Angle = ((_webcam.videoRotationAngle % 360) + 360) % 360,
                Mirrored = _webcam.videoVerticallyMirrored,
            };
        }

        private byte[] Encode(Frame frame, int maxSize, int quality)
        {
            var w = frame.Width;
            var h = frame.Height;
            var src = frame.Pixels;
            var angle = frame.Angle;
            var quarter = angle == 90 || angle == 270;
            var outW = quarter ? h : w;
            var outH = quarter ? w : h;
            var step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(outW, outH) / (float)maxSize));
            var dstW = outW / step;
            var dstH = outH / step;
            var dst = new Color32[dstW * dstH];
            for (var y = 0; y < dstH; y++)
            {
                for (var x = 0; x < dstW; x++)
                {
                    // 出力の (x, y) が元の映像のどこにあたるかを、回転を戻して求める。
                    int sx, sy;
                    var ox = x * step;
                    var oy = y * step;
                    switch (angle)
                    {
                        case 90:
                            sx = oy;
                            sy = h - 1 - ox;
                            break;
                        case 180:
                            sx = w - 1 - ox;
                            sy = h - 1 - oy;
                            break;
                        case 270:
                            sx = w - 1 - oy;
                            sy = ox;
                            break;
                        default:
                            sx = ox;
                            sy = oy;
                            break;
                    }

                    if (frame.Mirrored)
                    {
                        sy = h - 1 - sy;
                    }

                    dst[y * dstW + x] = src[Mathf.Clamp(sy, 0, h - 1) * w + Mathf.Clamp(sx, 0, w - 1)];
                }
            }

            var texture = new Texture2D(dstW, dstH, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(dst);
                texture.Apply(false);
                return texture.EncodeToJPG(quality);
            }
            finally
            {
                Destroy(texture);
            }
        }

        private IEnumerator StartRoutine()
        {
            SetStatus(null);

            var authorized = false;
            _awaitingPermission = true;
            yield return RequestPermission(result => authorized = result);
            _awaitingPermission = false;
            if (!authorized)
            {
                SetStatus("カメラの使用が許可されていません。\nメニューの「カメラの許可」から許可してください。");
                _startRoutine = null;
                yield break;
            }

            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                SetStatus("カメラが見つかりません");
                _startRoutine = null;
                yield break;
            }

            _webcam = new WebCamTexture(SelectBackCamera(devices).name, RequestedWidth, RequestedHeight);
            _webcam.Play();
            _preview.image = _webcam;
            _appliedViewportSize = Vector2.zero;
            _startRoutine = null;
        }

        private static IEnumerator RequestPermission(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                onResult(true);
                yield break;
            }

            bool? granted = null;
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => granted = true;
            callbacks.PermissionDenied += _ => granted = false;
            callbacks.PermissionRequestDismissed += _ => granted = false;
            Permission.RequestUserPermission(Permission.Camera, callbacks);

            while (granted == null)
            {
                yield return null;
            }

            onResult(granted.Value);
#else
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            onResult(Application.HasUserAuthorization(UserAuthorization.WebCam));
#endif
        }

        // エコ活動を撮るので背面カメラを優先する。PC など背面カメラが無い環境では先頭のカメラを使う。
        private static WebCamDevice SelectBackCamera(WebCamDevice[] devices)
        {
            foreach (var device in devices)
            {
                if (!device.isFrontFacing)
                {
                    return device;
                }
            }

            return devices[0];
        }

        // モバイルのカメラ映像は横向きのまま届き、正しい向きは videoRotationAngle で渡される。
        // 90°/270° のときは縦横を入れ替えた箱に描いてから回すと、ちょうど画面を覆う。
        private void ApplyOrientationIfChanged()
        {
            var viewportSize = _viewport.layout.size;
            if (float.IsNaN(viewportSize.x) || float.IsNaN(viewportSize.y))
            {
                return;
            }

            var angle = _webcam.videoRotationAngle;
            var mirrored = _webcam.videoVerticallyMirrored;
            if (angle == _appliedAngle && mirrored == _appliedMirrored && viewportSize == _appliedViewportSize)
            {
                return;
            }

            _appliedAngle = angle;
            _appliedMirrored = mirrored;
            _appliedViewportSize = viewportSize;

            var quarterTurn = angle % 180 != 0;
            var width = quarterTurn ? viewportSize.y : viewportSize.x;
            var height = quarterTurn ? viewportSize.x : viewportSize.y;

            // 端末ごとに変わる実測値なので USS では表せず、インラインスタイルで入れる。
            var style = _preview.style;
            style.left = (viewportSize.x - width) / 2f;
            style.top = (viewportSize.y - height) / 2f;
            style.right = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;
            style.width = width;
            style.height = height;
            style.rotate = new Rotate(angle);
            style.scale = new Scale(new Vector3(1f, mirrored ? -1f : 1f, 1f));
        }

        private void ReleaseCamera(bool keepPermissionRequest = false)
        {
            if (_startRoutine != null && !(keepPermissionRequest && _awaitingPermission))
            {
                StopCoroutine(_startRoutine);
                _startRoutine = null;
                _awaitingPermission = false;
            }

            if (_preview != null)
            {
                _preview.image = null;
            }

            if (_webcam != null)
            {
                _webcam.Stop();
                Destroy(_webcam);
                _webcam = null;
            }
        }

        private void SetStatus(string message)
        {
            if (_status == null)
            {
                return;
            }

            _status.text = message ?? string.Empty;
            _status.EnableInClassList(UiClasses.Hidden, message == null);
        }
    }
}
