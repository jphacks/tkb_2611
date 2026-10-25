using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Reality;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// 写っていた容器 (契約の <c>WaterContainer</c>)。扱うのはペットボトルだけ。
    /// 容量は容器の値から決めず、ラベルから読んだ <see cref="WaterEstimate.CapacityMl"/> だけを使う。
    /// </summary>
    public enum WaterContainer
    {
        Unknown,
        PetBottle,
    }

    /// <summary>サーバーの結論 (契約の <c>WaterDecision</c>)。知らない値は <see cref="Retake"/>。</summary>
    public enum WaterDecision
    {
        /// <summary><see cref="WaterEstimate.AmountMl"/> を使ってよい。</summary>
        Accept,

        /// <summary>ペットボトルが見分けられない。<see cref="WaterEstimate.RetakeHint"/> を出して撮り直してもらう。</summary>
        Retake,

        /// <summary>使わない (画面を撮った写真など)。理由は <see cref="WaterEstimate.Checks"/>。</summary>
        Reject,
    }

    /// <summary>
    /// ペットボトルの写真から見積もった、写っている水の量 (<c>POST /v1/water/estimate</c>)。
    /// 「飲んだ量」としてどう数えるか (空のボトルの規則・1 日の上限) はゲームロジックが決める (<see cref="WaterEmptyBottle"/>・FactorySession)。
    /// </summary>
    public sealed class WaterEstimate
    {
        public WaterEstimate(WaterContainer container, int capacityMl, double fillRatio, int amountMl, double confidence, WaterDecision decision,
            IReadOnlyList<InputCheck> checks, string retakeHint, string comment)
        {
            Container = container;
            CapacityMl = container == WaterContainer.Unknown ? 0 : Math.Max(0, Math.Min(WaterIntake.MaxMlPerEntry, capacityMl));
            FillRatio = Clamp01(fillRatio);
            AmountMl = CapacityMl == 0 ? 0 : Math.Max(0, Math.Min(CapacityMl, amountMl));
            Confidence = Clamp01(confidence);
            Decision = decision;
            Checks = checks ?? Array.Empty<InputCheck>();
            RetakeHint = retakeHint ?? "";
            Comment = comment ?? "";
        }

        public WaterContainer Container { get; }

        /// <summary>
        /// ラベルから読んだペットボトルの容量 (ml)。読めなければ 0 (撮り直し)。
        /// 1.2.0 のサーバー (容量を 350・500 の表から決めていた) は返さないので 0 になり、量を使わない。
        /// </summary>
        public int CapacityMl { get; }

        public double FillRatio { get; }

        /// <summary>写っている水の量 (ml)。容量 × <see cref="FillRatio"/>。容量が分からなければ 0。</summary>
        public int AmountMl { get; }

        public double Confidence { get; }
        public WaterDecision Decision { get; }
        public IReadOnlyList<InputCheck> Checks { get; }
        public string RetakeHint { get; }
        public string Comment { get; }

        /// <summary>受け取ってよい量があるか。</summary>
        public bool IsUsable => Decision == WaterDecision.Accept && CapacityMl > 0 && AmountMl > 0;

        /// <summary>
        /// 飲んだ量の規則 (<see cref="WaterEmptyBottle"/>) に渡す形にする。
        /// 空のボトル (量 0) こそ使う写真なので、<see cref="IsUsable"/> ではなく結論と容器で決める。
        /// </summary>
        public WaterPhoto ToPhoto(DateTimeOffset at) =>
            new WaterPhoto(at, CapacityMl, AmountMl, Decision == WaterDecision.Accept && CapacityMl > 0);

        private static double Clamp01(double value) => double.IsNaN(value) ? 0 : Math.Max(0, Math.Min(1, value));
    }

    /// <summary>水の写真の見積もりの口。</summary>
    public interface IWaterEstimator
    {
        Task<WaterEstimate> EstimateAsync(byte[] jpeg, CancellationToken cancellationToken);
    }

    /// <summary>見積もれなかった (通信の失敗など)。画面は撮り直しを案内する。</summary>
    public sealed class WaterEstimateException : Exception
    {
        public WaterEstimateException(string message, BackendException inner = null)
            : base(message, inner)
        {
        }

        /// <summary>backend の失敗なら、その code。</summary>
        public string Code => (InnerException as BackendException)?.Code;
    }

    /// <summary>
    /// backend の見積もり (<c>POST /v1/water/estimate</c>)。写真はメモリ上の JPEG のまま送り、保存もログ出力もしない。
    /// 知らない容器は <see cref="WaterContainer.Unknown"/>、知らない結論は <see cref="WaterDecision.Retake"/> (契約の前方互換の規則)。
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

            try
            {
                return ToEstimate(await _client.WaterEstimateAsync(jpeg, cancellationToken));
            }
            catch (BackendException e)
            {
                throw new WaterEstimateException($"見積もりに失敗しました ({e.Code})", e);
            }
        }

        internal static WaterEstimate ToEstimate(WaterEstimateDto dto)
        {
            var container = ContainerOf(dto.container);
            var decision = DecisionOf(dto.decision);
            // 知らない容器・ラベルの容量が無い応答 (1.2.0 のサーバー) は、量を使わない。容量をこちらで決めない。
            var capacity = container == WaterContainer.Unknown ? 0 : dto.capacity_ml;
            if (capacity <= 0 && decision == WaterDecision.Accept)
            {
                decision = WaterDecision.Retake;
            }

            var checks = new List<InputCheck>();
            foreach (var c in dto.checks ?? Array.Empty<CheckDto>())
            {
                if (c != null && !string.IsNullOrEmpty(c.label))
                {
                    checks.Add(new InputCheck(c.id, c.label, c.passed, c.detail));
                }
            }

            var retakeHint = dto.retake_hint;
            if (capacity <= 0 && container != WaterContainer.Unknown && string.IsNullOrEmpty(retakeHint))
            {
                retakeHint = CapacityRetakeHint;
            }

            return new WaterEstimate(container, capacity, dto.fill_ratio, dto.amount_ml, dto.confidence, decision, checks, retakeHint, dto.comment);
        }

        /// <summary>ラベルの容量が読めなかったときの案内 (サーバーが案内を返さなかったとき)。</summary>
        public const string CapacityRetakeHint = "ラベルの容量 (500ml・2L など) が写るように撮ってね";

        /// <summary>1.2.0 の容量付きの値もペットボトルとして受けるが、容量は <c>capacity_ml</c> からだけ取る。</summary>
        internal static WaterContainer ContainerOf(string value) => value switch
        {
            "pet_bottle" or "pet_bottle_350" or "pet_bottle_500" => WaterContainer.PetBottle,
            _ => WaterContainer.Unknown,
        };

        internal static WaterDecision DecisionOf(string value) => value switch
        {
            "accept" => WaterDecision.Accept,
            "reject" => WaterDecision.Reject,
            _ => WaterDecision.Retake,
        };
    }

    /// <summary>
    /// backend を使わない設定のときの見本。Editor と backend なしの実機で画面の流れを確かめるためのもので、
    /// 写真は見ずに固定の値を返す。見本だと分かるように確かめたことの 1 行に書く。
    /// </summary>
    public sealed class MockWaterEstimator : IWaterEstimator
    {
        public const string MockCheckId = "mock";

        public Task<WaterEstimate> EstimateAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                throw new WaterEstimateException("写真が空でした");
            }

            var checks = new[] { new InputCheck(MockCheckId, "見本の結果 (サーバーにつないでいない)", true, "") };
            // ラベルに 500ml と書いてある、飲みきったボトル (少しだけ残っている) として返す。
            return Task.FromResult(new WaterEstimate(WaterContainer.PetBottle, 500, 0.04, 20, 1, WaterDecision.Accept, checks, "", "見本だよ"));
        }
    }
}
