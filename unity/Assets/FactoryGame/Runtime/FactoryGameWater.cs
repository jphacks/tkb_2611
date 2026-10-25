using System;
using System.Linq;
using System.Threading.Tasks;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Reality;
using ProjectPenguin.Systems;

namespace ProjectPenguin.FactoryGame
{
    /// <summary>
    /// 水の段取り: 飲みきった空のペットボトルをその場で撮り、backend がラベルの容量と残りを見積もり、
    /// 飲んだ量 (容量 − 残り、<see cref="WaterEmptyBottle"/>) を候補にする。受け取るのは利用者が確かめてから (<see cref="Take"/>)。
    /// 写真は見積もりに送ったら捨てる。保存もログ出力もしない (.claude/rules/privacy.md)。
    /// </summary>
    internal sealed class FactoryGameWater
    {
        private readonly IWaterCameraSource _camera;
        private readonly Func<int> _today;
        private readonly Action<string> _notify;
        private IWaterEstimator _estimator;
        private bool _capturing, _estimating;

        internal FactoryGameWater(IWaterCameraSource camera, Func<int> today, Action<string> notify)
        {
            _camera = camera;
            _today = today;
            _notify = notify;
            _ = LoadEstimatorAsync();
        }

        /// <summary>確かめてもらう候補。無ければ null。</summary>
        internal WaterCandidate Candidate { get; private set; }

        internal bool Busy => _capturing || _estimating;

        internal string Status { get; private set; } = "水：飲みきった空のペットボトルを、ラベルの容量が写るように撮ってください";

        internal void Capture()
        {
            if (Busy) return;
            if (_estimator == null) { _notify("水の見積もりの準備中です。少し待ってから押してください"); return; }
            Candidate = null;
            _capturing = true;
            Status = "水：カメラを開いています";
            _camera.RequestCapture();
        }

        /// <summary>毎フレーム。撮り終わったら見積もりに送る。</summary>
        internal void Poll()
        {
            if (Candidate != null && Candidate.Day != _today())
            {
                Candidate = null;
                Status = "水：日付が変わりました。もう一度撮ってください";
            }

            if (!_capturing || !_camera.TryTake(out var status, out var capture)) return;
            _capturing = false;
            switch (status)
            {
                case WaterCaptureStatus.Captured:
                    _ = EstimateAsync(capture);
                    break;
                case WaterCaptureStatus.Cancelled:
                    Status = "水：撮るのをやめました。受け取っていません";
                    break;
                case WaterCaptureStatus.Denied:
                    Status = "水：カメラの許可がありません。iOS の設定でカメラを許可してください";
                    break;
                case WaterCaptureStatus.Unavailable:
                    Status = "水：この端末ではカメラを使えません";
                    break;
                default:
                    Status = "水：写真を作れませんでした。もう一度撮ってください";
                    break;
            }

            _notify(Status);
        }

        /// <summary>確かめた候補を受け取る。候補は 1 回で使い切る。</summary>
        internal WaterCandidate Take()
        {
            var candidate = Candidate;
            Candidate = null;
            if (candidate != null && candidate.Day != _today())
            {
                Status = "水：日付が変わりました。もう一度撮ってください";
                return null;
            }

            return candidate;
        }

        internal void Received() => Status = "水：受け取りました。次のボトルも撮れます";

        private async Task LoadEstimatorAsync()
        {
            try
            {
                _estimator = (await BackendServices.SharedAsync(null)).Water;
            }
            catch (Exception)
            {
                _estimator = new MockWaterEstimator();
            }
        }

        private async Task EstimateAsync(WaterCapture capture)
        {
            _estimating = true;
            Status = "水：写真を見積もっています";
            _notify(Status);
            try
            {
                var estimate = await _estimator.EstimateAsync(capture.Jpeg, default);
                var checks = string.Join("　", estimate.Checks.Select(c => (c.Passed ? "✓" : "×") + c.Label + (c.Detail != "" ? "：" + c.Detail : "")));
                var bottle = WaterEmptyBottle.Judge(estimate.ToPhoto(DateTimeOffset.UtcNow));
                if (estimate.Decision == WaterDecision.Reject)
                    Status = "水：使えない写真でした。" + (estimate.Comment != "" ? estimate.Comment : "本物のペットボトルを撮ってください");
                else if (bottle.Failure == WaterBottleFailure.NotAccepted)
                    Status = "水：" + (estimate.RetakeHint != "" ? estimate.RetakeHint : "ペットボトル全体とラベルが写るように撮り直してください");
                else if (bottle.Failure == WaterBottleFailure.NotEmpty)
                    Status = $"水：まだ{estimate.AmountMl:N0}mlほど残っています。飲みきってから撮ってください";
                else
                {
                    Candidate = new WaterCandidate(capture.ImageHash, bottle.Intake.Ml, estimate.CapacityMl, _today());
                    Status = $"水：{estimate.CapacityMl:N0}mlのボトルから{bottle.Intake.Ml:N0}ml飲んだと見積もりました。確かめて受け取ってください";
                }

                if (checks != "") Status += "\n" + checks;
            }
            catch (WaterEstimateException e)
            {
                Status = "水：見積もれませんでした。通信を確かめて、もう一度撮ってください" + (e.Code != null ? $" ({e.Code})" : "");
            }
            finally
            {
                _estimating = false;
            }

            _notify(Status.Split('\n')[0]);
        }
    }

    /// <summary>見積もりで決まった、受け取る前の飲んだ量。写真そのものは持たず、使い回しを弾くハッシュだけを持つ。</summary>
    internal sealed class WaterCandidate
    {
        internal WaterCandidate(string photo, int ml, int capacityMl, int day)
        {
            Photo = photo;
            Ml = ml;
            CapacityMl = capacityMl;
            Day = day;
        }

        internal string Photo { get; }
        internal int Ml { get; }
        internal int CapacityMl { get; }
        internal int Day { get; }
    }
}
