using System.Threading.Tasks;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// 設定 (<see cref="BackendConfig"/>) から、判定・みんなの氷山・お題の口を組み立てる。画面の側はここから受け取るだけにして、
    /// どの口が backend を使うかを知らなくて済むようにする。
    /// </summary>
    public sealed class BackendServices
    {
        private BackendServices(BackendConfig config, PenguinApiClient client, IEcoPhotoJudge judge, ICommunityGoalSource community,
            IQuestSource quests, IEncounterReporter encounters, IWaterEstimator water)
        {
            Config = config;
            Client = client;
            Judge = judge;
            Community = community;
            Quests = quests;
            Encounters = encounters;
            Water = water;
        }

        public BackendConfig Config { get; }

        /// <summary>backend を使わない設定なら null。</summary>
        public PenguinApiClient Client { get; }

        public IEcoPhotoJudge Judge { get; }

        /// <summary>みんなの氷山を切っているなら null (画面はゲージを出さない)。</summary>
        public ICommunityGoalSource Community { get; }

        /// <summary>お題を切っているなら null。</summary>
        public IQuestSource Quests { get; }

        /// <summary>すれ違いの相互確認。切っているときは端末の中だけで確かめる口 (null にはしない)。</summary>
        public IEncounterReporter Encounters { get; }

        /// <summary>水の写真の見積もり。切っているときは端末の中の固定の見積もり (null にはしない)。</summary>
        public IWaterEstimator Water { get; }

        public static BackendServices Create(BackendConfig config, IHttpTransport transport)
        {
            config ??= BackendConfig.Default;
            var mock = new MockEcoPhotoJudge();
            if (!config.UsesBackend)
            {
                return new BackendServices(config, null, mock, null, null, new MockEncounterReporter(), new MockWaterEstimator());
            }

            var client = new PenguinApiClient(config.ApiBaseUrl, transport);
            IEcoPhotoJudge judge = mock;
            if (config.Judge == JudgeMode.Http)
            {
                var http = new HttpEcoPhotoJudge(client);
                judge = config.DemoMode ? new FallbackEcoPhotoJudge(http, mock) : http;
            }

            return new BackendServices(config, client, judge,
                config.CommunityEnabled ? new HttpCommunityGoalSource(client) : null,
                config.QuestsEnabled ? new HttpQuestSource(client) : null,
                config.EncountersEnabled ? new HttpEncounterReporter(client) : new MockEncounterReporter(),
                config.WaterEnabled ? new HttpWaterEstimator(client) : new MockWaterEstimator());
        }

        private static Task<BackendServices> _shared;

        /// <summary>
        /// 設定ファイルを読んでから組み立てたもの。起動後に 1 回だけ作り、全員で同じものを使う
        /// (判定の連続の失敗の回数などを共有するため)。<paramref name="observer"/> は最初の呼び出しのものを使う。
        /// </summary>
        public static Task<BackendServices> SharedAsync(IServerResponseObserver observer) => _shared ??= CreateSharedAsync(observer);

        /// <summary>テストと Editor の確認用。</summary>
        public static void ResetShared()
        {
            _shared = null;
            BackendRuntime.Reset();
        }

        private static async Task<BackendServices> CreateSharedAsync(IServerResponseObserver observer)
        {
            var config = await BackendRuntime.ConfigAsync();
            return Create(config, new UnityWebRequestTransport(observer));
        }
    }
}
