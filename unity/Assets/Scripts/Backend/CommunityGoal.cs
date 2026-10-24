using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectPenguin.Backend
{
    /// <summary>みんなの氷山の節目 1 つ。</summary>
    public sealed class CommunityMilestone
    {
        public CommunityMilestone(int at, string rewardId, bool reached, string label)
        {
            At = at;
            RewardId = rewardId ?? "";
            Reached = reached;
            Label = label ?? "";
        }

        public int At { get; }
        public string RewardId { get; }
        public bool Reached { get; }
        public string Label { get; }
    }

    /// <summary>みんなの氷山 (会場のみんなで溜める目標、#113) の今の状態。</summary>
    public sealed class CommunityGoal
    {
        public CommunityGoal(string goalId, string title, int target, int current, IReadOnlyList<CommunityMilestone> milestones,
            DateTimeOffset? startsAt, DateTimeOffset? endsAt, DateTimeOffset? updatedAt, int pollSeconds)
        {
            GoalId = goalId ?? "";
            Title = title ?? "";
            Target = Math.Max(1, target);
            Current = Math.Max(0, current);
            Milestones = milestones ?? Array.Empty<CommunityMilestone>();
            StartsAt = startsAt;
            EndsAt = endsAt;
            UpdatedAt = updatedAt;
            PollSeconds = Math.Max(5, Math.Min(600, pollSeconds <= 0 ? 30 : pollSeconds));
        }

        public string GoalId { get; }
        public string Title { get; }
        public int Target { get; }
        public int Current { get; }
        public IReadOnlyList<CommunityMilestone> Milestones { get; }
        public DateTimeOffset? StartsAt { get; }
        public DateTimeOffset? EndsAt { get; }
        public DateTimeOffset? UpdatedAt { get; }

        /// <summary>次に問い合わせるまでの秒数。</summary>
        public int PollSeconds { get; }

        /// <summary>0〜1 の進み具合 (ゲージ用)。</summary>
        public double Progress => Math.Min(1.0, Current / (double)Target);
    }

    /// <summary>
    /// みんなの氷山を取ってくる口。機能を切っている・通信できないときは null を返し、画面はゲージを隠すだけでよい。
    /// 画面の側は別に作る (この口はゲームの状態を変えない)。
    /// </summary>
    public interface ICommunityGoalSource
    {
        Task<CommunityGoal> GetAsync(CancellationToken cancellationToken);
    }

    /// <summary>backend の <c>GET /v1/community/goal</c>。</summary>
    public sealed class HttpCommunityGoalSource : ICommunityGoalSource
    {
        private readonly PenguinApiClient _client;

        public HttpCommunityGoalSource(PenguinApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<CommunityGoal> GetAsync(CancellationToken cancellationToken)
        {
            try
            {
                return ToGoal(await _client.CommunityGoalAsync(cancellationToken));
            }
            catch (BackendException)
            {
                return null;
            }
        }

        internal static CommunityGoal ToGoal(CommunityGoalDto dto)
        {
            if (dto == null || !dto.enabled || string.IsNullOrEmpty(dto.goal_id) || dto.target <= 0)
            {
                return null;
            }

            var milestones = new List<CommunityMilestone>();
            if (dto.milestones != null)
            {
                foreach (var m in dto.milestones)
                {
                    if (m != null && m.at > 0)
                    {
                        milestones.Add(new CommunityMilestone(m.at, m.reward_id, m.reached, m.label));
                    }
                }
            }

            milestones.Sort((a, b) => a.At.CompareTo(b.At));
            return new CommunityGoal(dto.goal_id, dto.title, dto.target, dto.current, milestones,
                PenguinApiClient.ParseTime(dto.starts_at), PenguinApiClient.ParseTime(dto.ends_at),
                PenguinApiClient.ParseTime(dto.updated_at), dto.poll_seconds);
        }
    }

    /// <summary>backend 無しで画面を作るための固定の状態 (api/examples/community-goal.200.active.json と同じ中身)。</summary>
    public sealed class MockCommunityGoalSource : ICommunityGoalSource
    {
        private readonly CommunityGoal _goal;

        public MockCommunityGoalSource(CommunityGoal goal = null)
        {
            _goal = goal ?? new CommunityGoal("jphacks-2026", "会場のみんなで氷山を 300 枚ぶん育てよう", 300, 128,
                new[]
                {
                    new CommunityMilestone(100, "aurora", true, "オーロラが見えた"),
                    new CommunityMilestone(200, "whale", false, "クジラがやってくる"),
                    new CommunityMilestone(300, "festival", false, "島じゅうでお祭り"),
                },
                new DateTimeOffset(2026, 10, 24, 1, 20, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 25, 6, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 24, 5, 12, 30, TimeSpan.Zero), 15);
        }

        public Task<CommunityGoal> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_goal);
    }
}
