using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// backend の写真判定 (<c>POST /v1/eco-photo/judge</c>)。写真はメモリ上の JPEG のまま送り、保存もログ出力もしない。
    /// 応答は契約の前方互換の規則で読む: 知らない <c>subject</c> は「判定できない写真」、知らない <c>decision</c> は <c>accept</c>。
    /// </summary>
    public sealed class HttpEcoPhotoJudge : IEcoPhotoJudge
    {
        private readonly PenguinApiClient _client;

        public HttpEcoPhotoJudge(PenguinApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>直近の応答が伝えた、みんなの氷山の進み具合。無ければ null。</summary>
        public CommunityContribution LastContribution { get; private set; }

        public async Task<EcoPhotoVerdict> JudgeAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                throw new EcoPhotoJudgeException("写真が空でした");
            }

            if (jpeg.Length > BackendLimits.MaxImageBytes)
            {
                throw new EcoPhotoJudgeException("写真が大きすぎます",
                    new BackendException(BackendErrorCodes.ImageTooLarge, null, false));
            }

            JudgeResultDto dto;
            try
            {
                dto = await _client.JudgeAsync(jpeg, cancellationToken);
            }
            catch (BackendException e)
            {
                throw new EcoPhotoJudgeException($"判定に失敗しました ({e.Code})", e);
            }

            LastContribution = CommunityContribution.From(dto.community);
            return ToVerdict(dto);
        }

        /// <summary>契約の応答をゲームの判定結果にする。</summary>
        internal static EcoPhotoVerdict ToVerdict(JudgeResultDto dto)
        {
            var def = EcoSubjects.FindById(dto.subject);
            var subject = def?.Subject ?? EcoSubject.None;
            var confidence = double.IsNaN(dto.confidence) ? 0 : Math.Max(0, Math.Min(1, dto.confidence));
            return new EcoPhotoVerdict(subject, dto.comment, confidence, ToReview(dto));
        }

        private static EcoPhotoReview ToReview(JudgeResultDto dto)
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

            return new EcoPhotoReview(DecisionOf(dto.decision), checks, dto.retake_hint);
        }

        internal static EcoPhotoDecision DecisionOf(string value) => value switch
        {
            "reduce" => EcoPhotoDecision.Reduce,
            "retake" => EcoPhotoDecision.Retake,
            "reject" => EcoPhotoDecision.Reject,
            _ => EcoPhotoDecision.Accept,
        };
    }

    /// <summary>写真 1 枚でみんなの氷山がどれだけ進んだか。</summary>
    public sealed class CommunityContribution
    {
        public CommunityContribution(string goalId, int added, int current, int target)
        {
            GoalId = goalId ?? "";
            Added = Math.Max(0, added);
            Current = Math.Max(0, current);
            Target = Math.Max(1, target);
        }

        public string GoalId { get; }
        public int Added { get; }
        public int Current { get; }
        public int Target { get; }

        internal static CommunityContribution From(CommunityContributionDto dto) =>
            dto == null || string.IsNullOrEmpty(dto.goal_id) || dto.target <= 0
                ? null
                : new CommunityContribution(dto.goal_id, dto.added, dto.current, dto.target);
    }

    /// <summary>
    /// 会場のデモ用: 判定サーバーが続けて失敗したら、端末の中の判定に切り替えて止めない (#86)。
    /// 切り替えたことは隠さず、「確かめたこと」に 1 行足して画面に出す。
    /// 切り替えの条件は HTTP の状態ではなく連続の失敗の回数で数える (Web では CORS の無い 5xx が接続の失敗に見えるため)。
    /// </summary>
    public sealed class FallbackEcoPhotoJudge : IEcoPhotoJudge
    {
        public const string OfflineCheckId = "offline";

        private readonly IEcoPhotoJudge _primary;
        private readonly IEcoPhotoJudge _fallback;
        private readonly int _failuresBeforeFallback;
        private int _failures;

        public FallbackEcoPhotoJudge(IEcoPhotoJudge primary, IEcoPhotoJudge fallback, int failuresBeforeFallback = 2)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _failuresBeforeFallback = Math.Max(1, failuresBeforeFallback);
        }

        /// <summary>いま端末の中の判定を使っているか。</summary>
        public bool IsOffline => _failures >= _failuresBeforeFallback;

        public async Task<EcoPhotoVerdict> JudgeAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (!IsOffline)
            {
                try
                {
                    var verdict = await _primary.JudgeAsync(jpeg, cancellationToken);
                    _failures = 0;
                    return verdict;
                }
                catch (EcoPhotoJudgeException e) when ((e.InnerException as BackendException)?.Retryable ?? false)
                {
                    _failures++;
                    if (!IsOffline)
                    {
                        throw;
                    }
                }
            }

            var offline = await _fallback.JudgeAsync(jpeg, cancellationToken);
            var checks = new List<EcoPhotoCheck>(offline.Review.Checks)
            {
                new EcoPhotoCheck(OfflineCheckId, "サーバーにつながらないので、端末の中で見分けたよ", false),
            };
            var review = new EcoPhotoReview(offline.Review.Decision, checks, offline.Review.RetakeHint);
            return new EcoPhotoVerdict(offline.Subject, offline.Comment, offline.Confidence, review);
        }

        /// <summary>サーバーに戻す (「もう一度つなぐ」操作・テスト用)。</summary>
        public void Reconnect() => _failures = 0;
    }
}
