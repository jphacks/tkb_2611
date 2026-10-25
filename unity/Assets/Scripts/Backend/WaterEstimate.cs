using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Island;
using UnityEngine;

namespace ProjectPenguin.Backend
{
    /// <summary>写っていた容器 (api/openapi.yaml の WaterEstimate.container)。知らない値は <see cref="Unknown"/>。</summary>
    public enum WaterContainer
    {
        Unknown,
        PetBottle350,
        PetBottle500,
    }

    /// <summary>水の写真の見積もりの結果。資源にするかどうかは使う側が決める (契約)。</summary>
    public sealed class WaterEstimate
    {
        public WaterEstimate(WaterContainer container, double fillRatio, double amountMl, double confidence, EcoPhotoReview review, string comment)
        {
            Container = container;
            FillRatio = Clamp(fillRatio, 0, 1);
            AmountMl = Clamp(amountMl, 0, 2000);
            Confidence = Clamp(confidence, 0, 1);
            Review = review ?? throw new ArgumentNullException(nameof(review));
            Comment = comment ?? "";
        }

        public WaterContainer Container { get; }

        /// <summary>水位の割合 (0〜1)。</summary>
        public double FillRatio { get; }

        /// <summary>写っている水の量 (ml)。サーバーが容量 × 水位で計算する。</summary>
        public double AmountMl { get; }

        public double Confidence { get; }

        /// <summary>accept / retake / reject と、確かめたこと・撮り直しの案内。</summary>
        public EcoPhotoReview Review { get; }

        public string Comment { get; }

        private static double Clamp(double v, double min, double max) => double.IsNaN(v) ? min : Math.Max(min, Math.Min(max, v));
    }

    /// <summary>水の写真の見積もりの口。失敗は <see cref="WaterEstimateException"/> で知らせる。</summary>
    public interface IWaterEstimator
    {
        Task<WaterEstimate> EstimateAsync(byte[] jpeg, CancellationToken cancellationToken);
    }

    /// <summary>見積もれなかったとき (通信の失敗など)。画面は <see cref="UserMessage"/> を出す。</summary>
    public sealed class WaterEstimateException : Exception
    {
        public WaterEstimateException(string message, Exception inner = null)
            : base(message, inner)
        {
            UserMessage = (inner as BackendException)?.UserMessage ?? "うまく見られなかったよ。もう一度撮ってみてね";
        }

        /// <summary>画面にそのまま出せる日本語。</summary>
        public string UserMessage { get; }
    }

    /// <summary>
    /// backend の <c>POST /v1/water/estimate</c>。写真はメモリ上の JPEG のまま送り、保存もログ出力もしない。
    /// 知らない <c>container</c> は unknown、知らない <c>decision</c> は retake に倒す (契約の前方互換)。
    /// </summary>
    public sealed class HttpWaterEstimator : IWaterEstimator
    {
        private readonly PenguinApiClient _client;

        public HttpWaterEstimator(PenguinApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<WaterEstimate> EstimateAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                throw new WaterEstimateException("写真が空でした");
            }

            if (jpeg.Length > BackendLimits.MaxImageBytes)
            {
                throw new WaterEstimateException("写真が大きすぎます", new BackendException(BackendErrorCodes.ImageTooLarge, null, false));
            }

            WaterEstimateDto dto;
            try
            {
                dto = await _client.WaterEstimateAsync(jpeg, cancellationToken);
            }
            catch (BackendException e)
            {
                throw new WaterEstimateException($"見積もりに失敗しました ({e.Code})", e);
            }

            return ToEstimate(dto);
        }

        internal static WaterEstimate ToEstimate(WaterEstimateDto dto)
        {
            var checks = new List<EcoPhotoCheck>();
            if (dto.checks != null)
            {
                foreach (var c in dto.checks)
                {
                    if (c != null && !string.IsNullOrEmpty(c.label))
                    {
                        checks.Add(new EcoPhotoCheck(c.id, c.label, c.passed, c.detail));
                    }
                }
            }

            var review = new EcoPhotoReview(DecisionOf(dto.decision), checks, dto.retake_hint);
            return new WaterEstimate(ContainerOf(dto.container), dto.fill_ratio, dto.amount_ml, dto.confidence, review, dto.comment);
        }

        internal static WaterContainer ContainerOf(string value) => value switch
        {
            "pet_bottle_350" => WaterContainer.PetBottle350,
            "pet_bottle_500" => WaterContainer.PetBottle500,
            _ => WaterContainer.Unknown,
        };

        /// <summary>水の見積もりの <c>decision</c> は accept / retake / reject。知らない値は撮り直し側に倒す。</summary>
        internal static EcoPhotoDecision DecisionOf(string value) => value switch
        {
            "accept" => EcoPhotoDecision.Accept,
            "reject" => EcoPhotoDecision.Reject,
            _ => EcoPhotoDecision.Retake,
        };
    }

    /// <summary>backend を使わないとき (Editor・接続先なし)。500ml のペットボトルに半分入っている固定の見積もりを返す。</summary>
    public sealed class MockWaterEstimator : IWaterEstimator
    {
        private readonly TimeSpan _delay;

        public MockWaterEstimator(TimeSpan? delay = null)
        {
            _delay = delay ?? TimeSpan.FromSeconds(1.6);
        }

        public async Task<WaterEstimate> EstimateAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                throw new WaterEstimateException("写真が空でした");
            }

            if (_delay > TimeSpan.Zero)
            {
                // Task.Delay は Web ビルドでは戻ってこない。Unity の Awaitable で待つ (MockEcoPhotoJudge と同じ)。
                await Awaitable.WaitForSecondsAsync((float)_delay.TotalSeconds, cancellationToken);
            }

            return new WaterEstimate(WaterContainer.PetBottle500, 0.5, 250, 0.8,
                new EcoPhotoReview(EcoPhotoDecision.Accept, new List<EcoPhotoCheck>(), null), "水分補給、いいね");
        }
    }
}
