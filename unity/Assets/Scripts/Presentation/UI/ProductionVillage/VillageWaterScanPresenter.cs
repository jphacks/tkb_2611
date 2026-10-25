using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 村の水分補給のスキャン: 空のペットボトルの写真を <c>POST /v1/water/estimate</c> に送り、
    /// 受け付けられたら村の記録にして水を足す (1 日 1 回は村の規則が決める)。
    /// 写真はメモリ上だけで渡し、結果を受け取ったら捨てる (保存もログ出力もしない、.claude/rules/privacy.md)。
    /// 見積もりは設定ファイルの <c>water</c> で backend かモックかを選ぶ (<see cref="BackendServices"/>)。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class VillageWaterScanPresenter : MonoBehaviour
    {
        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private ReceiptCameraPreview _camera;

        private ProductionNavigator _navigator;
        private IWaterEstimator _estimator;
        private CancellationTokenSource _cts;
        private VisualElement _overlay;
        private Button _shutter;
        private Button _close;
        private Label _status;
        private VisualElement _card;
        private Label _title;
        private Label _amount;
        private Label _comment;
        private bool _busy;

        public bool IsOpen => _overlay != null && !_overlay.ClassListContains(UiClasses.Hidden);

        private void Awake()
        {
            _estimator = new MockWaterEstimator();
            _ = UseConfiguredEstimatorAsync();
        }

        private async Task UseConfiguredEstimatorAsync()
        {
            try
            {
                var services = await BackendServices.SharedAsync(null);
                if (this != null)
                {
                    _estimator = services.Water;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(VillageWaterScanPresenter)}: 水の見積もりの設定を読めなかったので、端末の中の見積もりを使います ({e.GetType().Name})");
            }
        }

        private void OnEnable()
        {
            if (_session == null || _camera == null)
            {
                Debug.LogError($"{nameof(VillageWaterScanPresenter)}: 参照が未設定です。", this);
                enabled = false;
                return;
            }

            _navigator = GetComponent<ProductionNavigator>();
            var root = GetComponent<UIDocument>().rootVisualElement;
            _overlay = root.Q(ProductionUiNames.WaterCamera);
            _shutter = _overlay.Q<Button>(ProductionUiNames.WaterShutter);
            _close = _overlay.Q<Button>(ProductionUiNames.WaterCameraClose);
            _status = _overlay.Q<Label>(UiNames.Scan.CameraStatus);
            _card = _overlay.Q(ProductionUiNames.WaterResult);
            _title = _overlay.Q<Label>(ProductionUiNames.WaterTitle);
            _amount = _overlay.Q<Label>(ProductionUiNames.WaterAmount);
            _comment = _overlay.Q<Label>(ProductionUiNames.WaterComment);
            _shutter.clicked += OnShutter;
            _close.clicked += Close;
        }

        private void OnDisable()
        {
            if (_shutter != null)
            {
                _shutter.clicked -= OnShutter;
                _close.clicked -= Close;
            }

            Cancel();
        }

        /// <summary>カメラを開く。カメラは開いている間だけ起動する (電池とプライバシーのため)。</summary>
        public void Open()
        {
            IslandUi.SetVisible(_card, false);
            IslandUi.SetVisible(_status, false);
            IslandUi.SetVisible(_overlay, true);
            _camera.StartPreview();
        }

        public void Close()
        {
            Cancel();
            _busy = false;
            _camera.StopPreview();
            IslandUi.SetVisible(_overlay, false);
        }

        private void OnShutter()
        {
            if (_busy)
            {
                return;
            }

            var jpeg = _camera.CaptureJpegWithin();
            if (jpeg == null)
            {
                ShowCard("カメラが使えないみたい", "", "端末の設定でカメラを許可してね");
                return;
            }

            _ = RunAsync(jpeg);
        }

        private async Task RunAsync(byte[] jpeg)
        {
            _busy = true;
            Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IslandUi.SetVisible(_card, false);
            _status.text = "水の量を見ているよ…";
            IslandUi.SetVisible(_status, true);
            try
            {
                var estimate = await _estimator.EstimateAsync(jpeg, token);
                jpeg = null;
                if (token.IsCancellationRequested)
                {
                    return;
                }

                Show(estimate);
            }
            catch (OperationCanceledException)
            {
                // 画面を閉じた。何もしない。
            }
            catch (Exception e)
            {
                // 写真の中身はメッセージに含めない (.claude/rules/privacy.md)。
                Debug.LogWarning($"{nameof(VillageWaterScanPresenter)}: 見積もりに失敗しました ({e.GetType().Name})");
                ShowCard("見積もれなかったよ", "", (e.InnerException as BackendException)?.UserMessage ??"うまく見られなかったよ。もう一度撮ってみてね");
            }
            finally
            {
                IslandUi.SetVisible(_status, false);
                _busy = false;
            }
        }

        private void Show(WaterEstimate e)
        {
            if (e.Decision != WaterDecision.Accept || e.AmountMl <= 0)
            {
                var hint = !string.IsNullOrEmpty(e.RetakeHint) ? e.RetakeHint
                    : !string.IsNullOrEmpty(e.Comment) ? e.Comment
                    : "ペットボトルが見えなかったよ。もう一度撮ってみてね";
                ShowCard(e.Decision == WaterDecision.Reject ? "対象外の写真だよ" : "撮り直してね", "", hint);
                return;
            }

            var ml = e.AmountMl.ToString("0", CultureInfo.InvariantCulture);
            var percent = (e.FillRatio * 100).ToString("0", CultureInfo.InvariantCulture);
            ShowCard(ContainerName(e), $"約 {ml} ml (水位 {percent}%)", Record(e));
        }

        /// <summary>受け付けた見積もりを村の記録にする。1 日 1 回までで、受け取り済みなら村がそう答える。</summary>
        private string Record(WaterEstimate e)
        {
            var day = VillageHealthInput.Day(DateTimeOffset.Now);
            // 写真ごとの乱数。村の側が「同じ記録」「同じ日」で二重に受け取らせない。
            var id = "water:" + day + ":" + Guid.NewGuid().ToString("N");
            var accepted = _session.Core.AcceptWater(id, day, day, true, ProductionVillageSession.UtcNow);
            var message = accepted || _session.Core.Error.Length == 0 ? _session.Core.Notice : _session.Core.Error;
            if (accepted)
            {
                _navigator?.ShowToast(message);
            }

            return string.IsNullOrEmpty(e.Comment) ? message : e.Comment + "\n" + message;
        }

        private void ShowCard(string title, string amount, string comment)
        {
            _title.text = title;
            _amount.text = amount;
            _comment.text = comment;
            IslandUi.SetVisible(_amount, !string.IsNullOrEmpty(amount));
            IslandUi.SetVisible(_comment, !string.IsNullOrEmpty(comment));
            IslandUi.SetVisible(_card, true);
        }

        private static string ContainerName(WaterEstimate estimate) =>
            estimate.CapacityMl > 0 ? $"{estimate.CapacityMl}ml のペットボトル" : "ペットボトル";

        private void Cancel()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
