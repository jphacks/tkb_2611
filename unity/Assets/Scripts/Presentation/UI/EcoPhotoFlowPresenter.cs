using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// エコ写真の流れ: スキャン画面のシャッター → 解析中 (判定を待つ) → 結果 → 島へ。
    /// 写真の JPEG はメモリ上だけで判定に渡し、結果を受け取ったら捨てる (保存もログ出力もしない)。
    /// 判定は penguin-config.json の設定で選ぶ (<see cref="BackendServices"/>)。読めるまでと backend を使わない設定では
    /// 端末の中の判定 (<see cref="MockEcoPhotoJudge"/>)。判定サーバーが撮り直し・受け付けないと決めた写真は記録にしない。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EcoPhotoFlowPresenter : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "小さな行動でも、毎日続けると大きな差になるよ",
            "電気・水・ガスを大事にすると、島の施設がよく動くよ",
            "写真は毎日 1 枚で十分。続けることがいちばん大事",
            "歩いた歩数もガスになるよ。ちょっと遠回りしてみよう",
            "ミッションを振り返ると、どの資源にもなるオールマイティがもらえるよ",
        };

        [SerializeField] private GameStateSystem _game;
        [SerializeField] private ScreenNavigator _navigator;
        [SerializeField] private ReceiptCameraPreview _camera;
        [SerializeField] private AnalyzingScreenPresenter _analyzing;
        [SerializeField] private IslandHudPresenter _hud;

        private IEcoPhotoJudge _judge;
        private bool _judgeSetByCaller;
        private CancellationTokenSource _cts;
        private Button _shutter;
        private VisualElement _result;
        private bool _busy;

        /// <summary>判定の差し替え口 (テストと Hack Day の backend 用)。</summary>
        public void SetJudge(IEcoPhotoJudge judge)
        {
            _judge = judge;
            _judgeSetByCaller = true;
        }

        private void Awake()
        {
            _judge ??= new MockEcoPhotoJudge();
            _ = UseConfiguredJudgeAsync();
        }

        private async Task UseConfiguredJudgeAsync()
        {
            try
            {
                var services = await BackendServices.SharedAsync(_game);
                if (!_judgeSetByCaller && this != null)
                {
                    _judge = services.Judge;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(EcoPhotoFlowPresenter)}: 判定の設定を読めなかったので、端末の中の判定を使います ({e.GetType().Name})");
            }
        }

        private void OnEnable()
        {
            if (_game == null || _navigator == null || _analyzing == null)
            {
                Debug.LogError($"{nameof(EcoPhotoFlowPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            var root = GetComponent<UIDocument>().rootVisualElement;
            _shutter = root.Q(ScreenNavigator.ElementNameOf(AppScreen.Scan)).Q<Button>(UiNames.Scan.ShutterButton);
            _result = root.Q(ScreenNavigator.ElementNameOf(AppScreen.Result));
            _shutter.clicked += OnShutter;
            _navigator.ScreenShown += OnScreenShown;
        }

        private void OnDisable()
        {
            if (_shutter != null)
            {
                _shutter.clicked -= OnShutter;
            }

            if (_navigator != null)
            {
                _navigator.ScreenShown -= OnScreenShown;
            }

            CancelJudge();
        }

        private void OnScreenShown(AppScreen screen, AppScreen previous)
        {
            // 解析中に閉じたら判定もやめる。
            if (previous == AppScreen.Analyzing && screen != AppScreen.Analyzing && screen != AppScreen.Result)
            {
                CancelJudge();
                _analyzing.Cancel();
            }
        }

        private void OnShutter()
        {
            if (_busy)
            {
                return;
            }

            var jpeg = CaptureOrSample(_camera);
            if (jpeg == null)
            {
                _hud?.ShowToast("カメラが使えないみたい。メニューの「カメラの許可」を確かめてね");
                return;
            }

            _ = RunAsync(jpeg);
        }

        /// <summary>
        /// 撮った写真 (JPEG)。カメラが無い Editor と開発ビルドでは、見本の写真で流れを確かめられるようにする。
        /// 製品のビルドでカメラが使えなければ null。
        /// </summary>
        internal static byte[] CaptureOrSample(ReceiptCameraPreview camera)
        {
            var jpeg = camera != null ? camera.CaptureJpegWithin() : null;
            if (jpeg == null && (Application.isEditor || Debug.isDebugBuild))
            {
                jpeg = SampleJpeg();
            }

            return jpeg;
        }

        private async Task RunAsync(byte[] jpeg)
        {
            _busy = true;
            CancelJudge();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _navigator.Show(AppScreen.Analyzing);
            _analyzing.Begin();
            try
            {
                var verdict = await _judge.JudgeAsync(jpeg, token);
                jpeg = null;
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (!verdict.Review.Counts)
                {
                    // 撮り直し・受け付けない写真は、記録にも資源にもしない。理由を伝えてスキャン画面に戻す。
                    _analyzing.Cancel();
                    _navigator.Show(AppScreen.Scan);
                    _hud?.ShowToast(RefusalMessage(verdict));
                    return;
                }

                var result = _game.RecordEcoPhoto(verdict);
                _analyzing.Finish(() =>
                {
                    Fill(result);
                    _navigator.Show(AppScreen.Result);
                });
            }
            catch (OperationCanceledException)
            {
                // 画面を閉じた。何もしない。
            }
            catch (Exception e)
            {
                // 写真の中身はメッセージに含めない (.claude/rules/privacy.md)。
                Debug.LogWarning($"{nameof(EcoPhotoFlowPresenter)}: 判定に失敗しました ({e.GetType().Name})");
                _analyzing.Cancel();
                _navigator.Show(AppScreen.Scan);
                _hud?.ShowToast((e as EcoPhotoJudgeException)?.UserMessage ?? "うまく見分けられなかったよ。もう一度撮ってみてね");
            }
            finally
            {
                _busy = false;
            }
        }

        private void CancelJudge()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private void Fill(EcoPhotoResult result)
        {
            var v = result.Verdict;
            var now = _game.Now.ToOffset(TimeSpan.FromHours(9));
            _result.Q<Label>(UiNames.Result.ScannedAt).text = now.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            _result.Q<Label>(UiNames.Result.PhotoLabel).text = string.IsNullOrEmpty(v.Label) ? EcoCategories.NameOf(v.Category) : v.Label;
            _result.Q<Label>(UiNames.Result.CategoryName).text = EcoCategories.NameOf(v.Category);
            _result.Q<Label>(UiNames.Result.CommentText).text = string.IsNullOrEmpty(v.Comment) ? "撮ってくれてありがとう！" : v.Comment;

            var main = EcoCategories.MainResource(v.Category);
            var icon = _result.Q(UiNames.Result.CategoryIcon);
            foreach (var c in new[] { UiClasses.Icon.Leaf, UiClasses.Icon.Electricity, UiClasses.Icon.Water, UiClasses.Icon.Gas, UiClasses.Icon.AllMighty })
            {
                icon.RemoveFromClassList(c);
            }

            foreach (var c in new[] { UiClasses.Tint.Eco, UiClasses.Tint.Electricity, UiClasses.Tint.Water, UiClasses.Tint.Gas, UiClasses.Tint.AllMighty })
            {
                icon.RemoveFromClassList(c);
            }

            icon.AddToClassList(v.IsEco ? IslandUi.IconClass(main) : UiClasses.Icon.Leaf);
            icon.AddToClassList(v.IsEco ? IslandUi.TintClass(main) : UiClasses.Tint.Eco);

            var list = _result.Q(UiNames.Result.GrantList);
            list.Clear();
            var title = _result.Q<Label>(UiNames.Result.GrantTitle);
            if (result.Granted == null)
            {
                title.text = "今日の資源はもう受け取ったよ";
                list.Add(IslandUi.Row(IslandUi.IconBadge(UiClasses.Icon.Check, UiClasses.Tint.Eco, true), "また明日撮ってね",
                    "資源がもらえるのは 1 日 1 枚まで。記録は何枚撮っても同じだよ"));
            }
            else
            {
                title.text = "島に届いた資源";
                foreach (var kv in result.Granted.NonZero())
                {
                    var row = IslandUi.Row(IslandUi.IconBadge(IslandUi.IconClass(kv.Key), IslandUi.TintClass(kv.Key)),
                        IslandCatalog.NameOf(kv.Key), Purpose(kv.Key), IslandUi.Text("+" + IslandUi.Number(kv.Value), UiClasses.Text.Title, UiClasses.Text.Heavy));
                    list.Add(row);
                }

                if (list.childCount > 0)
                {
                    list[list.childCount - 1].AddToClassList(UiClasses.Row.Last);
                }
            }

            var recordCard = _result.Q(UiNames.Result.RecordCard);
            IslandUi.SetVisible(recordCard, result.NewRecordDay);
            if (result.NewRecordDay)
            {
                var gain = result.After.Ice - result.Before.Ice;
                _result.Q<Label>(UiNames.Result.IceGain).text = (gain >= 0 ? "+" : "") + gain.ToString("0.#", CultureInfo.InvariantCulture);
            }

            FillReview(v.Review);
            _result.Q<Label>(UiNames.Result.TipText).text = Tips[Mathf.Abs(_game.Today) % Tips.Length];
        }

        /// <summary>
        /// 判定サーバーが確かめたこと。中身はサーバーが決め、ここは並べるだけ
        /// (Hack Day に確かめる項目が増えても、この画面は作り直さずに済む)。無ければ隠す。
        /// </summary>
        private void FillReview(EcoPhotoReview review)
        {
            var list = _result.Q(UiNames.Result.CheckList);
            list.Clear();
            foreach (var check in review.Checks)
            {
                var badge = check.Passed
                    ? IslandUi.IconBadge(UiClasses.Icon.Check, UiClasses.Tint.Eco, true)
                    : IslandUi.IconBadge(UiClasses.Icon.Warning, UiClasses.Tint.Point);
                list.Add(IslandUi.Row(badge, check.Label, check.Detail));
            }

            if (list.childCount > 0)
            {
                list[list.childCount - 1].AddToClassList(UiClasses.Row.Last);
            }

            IslandUi.SetVisible(_result.Q(UiNames.Result.CheckSection), list.childCount > 0);
        }

        /// <summary>撮り直し・受け付けない写真のときに出す一言。</summary>
        private static string RefusalMessage(EcoPhotoVerdict verdict)
        {
            var review = verdict.Review;
            if (review.Decision == EcoPhotoDecision.Retake)
            {
                return !string.IsNullOrEmpty(review.RetakeHint) ? review.RetakeHint
                    : !string.IsNullOrEmpty(verdict.Comment) ? verdict.Comment
                    : "うまく見分けられなかったよ。もう一度撮ってみてね";
            }

            if (!string.IsNullOrEmpty(verdict.Comment))
            {
                return verdict.Comment;
            }

            foreach (var check in review.Checks)
            {
                if (!check.Passed)
                {
                    return $"この写真は受け取れなかったよ (「{check.Label}」を確かめられなかった)";
                }
            }

            return "この写真は受け取れなかったよ。ほかの写真を撮ってね";
        }

        private static string Purpose(Resource r) => r switch
        {
            Resource.Electricity => "施設を建てるのに使う",
            Resource.Water => "海を凍らせて島を広げるのに使う",
            Resource.Gas => "施設を動かすのに使う",
            Resource.AllMighty => "電気・水・ガスのどれにもなる",
            _ => "",
        };

        /// <summary>カメラが無い環境で流れを確かめるための見本の写真 (毎回少し違う色)。</summary>
        private static byte[] SampleJpeg()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            try
            {
                var color = Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 0.9f);
                var pixels = new Color[64 * 64];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = color * (0.8f + 0.2f * ((i % 64) / 64f));
                }

                texture.SetPixels(pixels);
                texture.Apply(false);
                return texture.EncodeToJPG(70);
            }
            finally
            {
                Destroy(texture);
            }
        }
    }
}
