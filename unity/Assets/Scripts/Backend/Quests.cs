using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Backend
{
    /// <summary>お題の種類 (#123)。</summary>
    public enum QuestKind
    {
        /// <summary>その場ですぐできる。</summary>
        Chokotto,

        /// <summary>会場・場所に行く。</summary>
        Odekake,
    }

    /// <summary>お題の達成の確かめ方。</summary>
    public enum QuestProof
    {
        /// <summary>写真判定を通す (<see cref="Quest.Subjects"/> のどれか)。</summary>
        Photo,

        /// <summary>QR を読む。</summary>
        Qr,

        /// <summary>合言葉を入れる。</summary>
        Passphrase,

        /// <summary>自己申告。</summary>
        None,
    }

    /// <summary>ご褒美の種類。中身 (お魚の袋の量・時短の分数・飾り) はクライアントの表で決める。</summary>
    public enum QuestRewardKind
    {
        FishPack,
        TimeSkip,
        Decoration,
        Resource,
    }

    /// <summary>お題のご褒美。</summary>
    public sealed class QuestReward
    {
        public QuestReward(QuestRewardKind kind, string id, double amount)
        {
            Kind = kind;
            Id = id ?? "";
            Amount = Math.Max(0, amount);
        }

        public QuestRewardKind Kind { get; }
        public string Id { get; }

        /// <summary>量。無ければ 0 (表の既定の量を使う)。</summary>
        public double Amount { get; }
    }

    /// <summary>サーバーが配る期間限定のお題 1 つ。</summary>
    public sealed class Quest
    {
        public Quest(string id, QuestKind kind, string title, string description, DateTimeOffset startsAt, DateTimeOffset endsAt,
            QuestProof proof, IReadOnlyList<EcoSubject> subjects, QuestReward reward)
        {
            Id = id ?? "";
            Kind = kind;
            Title = title ?? "";
            Description = description ?? "";
            StartsAt = startsAt;
            EndsAt = endsAt;
            Proof = proof;
            Subjects = subjects ?? Array.Empty<EcoSubject>();
            Reward = reward;
        }

        public string Id { get; }
        public QuestKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public DateTimeOffset StartsAt { get; }
        public DateTimeOffset EndsAt { get; }
        public QuestProof Proof { get; }

        /// <summary><see cref="QuestProof.Photo"/> のとき、達成になる写真の表のもの。</summary>
        public IReadOnlyList<EcoSubject> Subjects { get; }

        public QuestReward Reward { get; }

        /// <summary>いま出してよいか。<paramref name="now"/> は補正済みの時計の時刻を渡す。</summary>
        public bool IsOpen(DateTimeOffset now) => StartsAt <= now && now < EndsAt;
    }

    /// <summary>QR・合言葉の照合の結果。</summary>
    public sealed class QuestRedeemResult
    {
        private QuestRedeemResult(bool accepted, QuestReward reward, string message, string errorCode)
        {
            Accepted = accepted;
            Reward = reward;
            Message = message ?? "";
            ErrorCode = errorCode;
        }

        public static QuestRedeemResult Ok(QuestReward reward, string message) => new QuestRedeemResult(true, reward, message, null);

        public static QuestRedeemResult Refused(string code, string message) => new QuestRedeemResult(false, null, message, code);

        public bool Accepted { get; }

        /// <summary>受け取れたご褒美。サーバーが返さなければお題の <see cref="Quest.Reward"/> を使う。</summary>
        public QuestReward Reward { get; }

        /// <summary>画面にそのまま出せる日本語。</summary>
        public string Message { get; }

        /// <summary>受け取れなかった理由 (<see cref="BackendErrorCodes"/>)。受け取れたら null。</summary>
        public string ErrorCode { get; }
    }

    /// <summary>
    /// お題を取ってくる口と、QR・合言葉の照合の口 (#123)。画面とご褒美の反映は別に作り、この型に合わせて繋ぐ。
    /// 同じお題を 2 回受け取らないようにするのは端末の保存の役目 (サーバーは照合だけ)。
    /// </summary>
    public interface IQuestSource
    {
        /// <summary>配っているお題。機能を切っている・通信できないときは空。知らない種類のお題は入れない。</summary>
        Task<IReadOnlyList<Quest>> ListAsync(CancellationToken cancellationToken);

        /// <summary>照合する。通信できないときも例外にせず、<see cref="QuestRedeemResult.Accepted"/> が false の結果を返す。</summary>
        Task<QuestRedeemResult> RedeemAsync(string questId, string code, CancellationToken cancellationToken);
    }

    /// <summary>backend の <c>GET /v1/quests</c> と <c>POST /v1/quests/{id}/redeem</c>。</summary>
    public sealed class HttpQuestSource : IQuestSource
    {
        private readonly PenguinApiClient _client;

        public HttpQuestSource(PenguinApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<IReadOnlyList<Quest>> ListAsync(CancellationToken cancellationToken)
        {
            try
            {
                return ToQuests(await _client.QuestsAsync(cancellationToken));
            }
            catch (BackendException)
            {
                return Array.Empty<Quest>();
            }
        }

        public async Task<QuestRedeemResult> RedeemAsync(string questId, string code, CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _client.RedeemAsync(questId, code, cancellationToken);
                return dto.accepted
                    ? QuestRedeemResult.Ok(RewardOf(dto.reward), dto.message)
                    : QuestRedeemResult.Refused(BackendErrorCodes.CodeInvalid, BackendException.DefaultMessage(BackendErrorCodes.CodeInvalid));
            }
            catch (BackendException e)
            {
                return QuestRedeemResult.Refused(e.Code, e.UserMessage);
            }
        }

        internal static IReadOnlyList<Quest> ToQuests(QuestListDto dto)
        {
            var quests = new List<Quest>();
            if (dto?.quests == null)
            {
                return quests;
            }

            foreach (var q in dto.quests)
            {
                var quest = ToQuest(q);
                if (quest != null)
                {
                    quests.Add(quest);
                }
            }

            return quests;
        }

        /// <summary>出せないお題 (切ってある・知らない種類・期間が読めない・ご褒美が読めない) は null。</summary>
        private static Quest ToQuest(QuestDto q)
        {
            if (q == null || !q.enabled || string.IsNullOrEmpty(q.id))
            {
                return null;
            }

            QuestKind kind;
            switch (q.kind)
            {
                case "chokotto": kind = QuestKind.Chokotto; break;
                case "odekake": kind = QuestKind.Odekake; break;
                default: return null;
            }

            QuestProof proof;
            switch (q.proof)
            {
                case "photo": proof = QuestProof.Photo; break;
                case "qr": proof = QuestProof.Qr; break;
                case "passphrase": proof = QuestProof.Passphrase; break;
                case "none": proof = QuestProof.None; break;
                default: return null;
            }

            var starts = PenguinApiClient.ParseTime(q.starts_at);
            var ends = PenguinApiClient.ParseTime(q.ends_at);
            var reward = RewardOf(q.reward);
            if (starts == null || ends == null || reward == null)
            {
                return null;
            }

            var subjects = new List<EcoSubject>();
            if (q.subjects != null)
            {
                foreach (var id in q.subjects)
                {
                    var def = EcoSubjects.FindById(id);
                    if (def != null)
                    {
                        subjects.Add(def.Subject);
                    }
                }
            }

            // 写真のお題で、知っているものが 1 つも無ければ達成できないので出さない。
            if (proof == QuestProof.Photo && subjects.Count == 0)
            {
                return null;
            }

            return new Quest(q.id, kind, q.title, q.description, starts.Value, ends.Value, proof, subjects, reward);
        }

        private static QuestReward RewardOf(RewardDto r)
        {
            if (r == null || string.IsNullOrEmpty(r.id))
            {
                return null;
            }

            switch (r.kind)
            {
                case "fish_pack": return new QuestReward(QuestRewardKind.FishPack, r.id, r.amount);
                case "time_skip": return new QuestReward(QuestRewardKind.TimeSkip, r.id, r.amount);
                case "decoration": return new QuestReward(QuestRewardKind.Decoration, r.id, r.amount);
                case "resource": return new QuestReward(QuestRewardKind.Resource, r.id, r.amount);
                default: return null;
            }
        }
    }

    /// <summary>backend 無しで画面を作るための固定のお題 (api/examples/quests.200.some.json と同じ中身)。合言葉は「penguin」。</summary>
    public sealed class MockQuestSource : IQuestSource
    {
        public const string Passphrase = "penguin";

        private static readonly Quest[] Samples =
        {
            new Quest("bottle-week", QuestKind.Chokotto, "マイボトルの日", "マイボトルの写真を撮ると、お魚の袋がもらえるよ",
                new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 26, 15, 0, 0, TimeSpan.Zero),
                QuestProof.Photo, new[] { EcoSubject.MyBottle }, new QuestReward(QuestRewardKind.FishPack, "fish_small", 1)),
            new Quest("venue-tkb", QuestKind.Odekake, "会場のペンギンに会いに行こう", "東京B 会場の QR を読むと、限定の飾りがもらえるよ",
                new DateTimeOffset(2026, 10, 24, 1, 20, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero),
                QuestProof.Qr, null, new QuestReward(QuestRewardKind.Decoration, "jphacks_flag", 0)),
        };

        public Task<IReadOnlyList<Quest>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Quest>>(Samples);

        public Task<QuestRedeemResult> RedeemAsync(string questId, string code, CancellationToken cancellationToken)
        {
            var quest = Array.Find(Samples, q => q.Id == questId);
            if (quest == null)
            {
                return Task.FromResult(QuestRedeemResult.Refused(BackendErrorCodes.QuestClosed, "このお題はもう終わったよ"));
            }

            return Task.FromResult(code == Passphrase || code == questId
                ? QuestRedeemResult.Ok(quest.Reward, "ご褒美をもらったよ！")
                : QuestRedeemResult.Refused(BackendErrorCodes.CodeInvalid, "合言葉がちがうみたい"));
        }
    }
}
