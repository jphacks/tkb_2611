using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Reality;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode.Backend
{
    /// <summary>
    /// 契約の例 (api/examples) を、偽の HTTP 越しにクライアントの口へ通す。
    /// Hack Day には backend だけを変えるので、例のどれでも画面とゲームが崩れないことをここで守る。
    /// </summary>
    public sealed class BackendContractTests
    {
        private const string Base = "https://penguin.example/api";

        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 0xFF, 0xD9 };

        /// <summary>契約の正本 (リポジトリ直下の api/examples)。Unity のプロジェクトは unity/ にある。</summary>
        internal static readonly string ExamplesDir = Path.Combine(Application.dataPath, "..", "..", "api", "examples");

        internal static string Example(string name)
        {
            var path = Path.Combine(ExamplesDir, name);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static IEnumerable<string> ExampleNames(string prefix) =>
            Directory.GetFiles(ExamplesDir, prefix + "*.json")
                .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal);

        /// <summary>例のファイル名の HTTP 状態 (「error.503.upstream_unavailable.json」の 503) を返す。</summary>
        private static long StatusOf(string name) => long.Parse(name.Split('.')[1]);

        private static HttpResult Respond(string name, string retryAfter = null) =>
            new HttpResult(StatusOf(name.Replace("compat/", "")), Example(name), retryAfter);

        private static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private sealed class FakeTransport : IHttpTransport
        {
            private readonly Queue<HttpResult> _results;

            public FakeTransport(params HttpResult[] results)
            {
                _results = new Queue<HttpResult>(results);
            }

            public List<HttpRequestSpec> Sent { get; } = new List<HttpRequestSpec>();

            public Task<HttpResult> SendAsync(HttpRequestSpec request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Sent.Add(request);
                return Task.FromResult(_results.Count > 1 ? _results.Dequeue() : _results.Peek());
            }
        }

        private static HttpEcoPhotoJudge Judge(FakeTransport transport) => new HttpEcoPhotoJudge(new PenguinApiClient(Base, transport));

        private static EcoPhotoJudgeException JudgeFails(FakeTransport transport)
        {
            var judge = Judge(transport);
            return Assert.Throws<EcoPhotoJudgeException>(() => Run(judge.JudgeAsync(Jpeg, default)));
        }

        // ------------------------------------------------------------------
        // 写真判定
        // ------------------------------------------------------------------

        [Test]
        public void JudgeSendsTheJpegAsMultipartToTheContractPath()
        {
            var transport = new FakeTransport(Respond("judge.200.accept.json"));

            Run(Judge(transport).JudgeAsync(Jpeg, default));

            var sent = transport.Sent.Single();
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo(Base + "/v1/eco-photo/judge"));
            Assert.That(sent.ContentType, Does.StartWith("multipart/form-data; boundary="));
            Assert.That(sent.TimeoutSeconds, Is.EqualTo(BackendLimits.TimeoutSeconds));
            var head = Encoding.UTF8.GetString(sent.Body, 0, Math.Min(200, sent.Body.Length));
            Assert.That(head, Does.Contain("name=\"image\""));
            Assert.That(head, Does.Contain("Content-Type: image/jpeg"));
        }

        [TestCaseSource(nameof(JudgeExamples))]
        public void EverySuccessfulJudgeExampleBecomesAVerdict(string name)
        {
            var verdict = Run(Judge(new FakeTransport(Respond(name))).JudgeAsync(Jpeg, default));

            Assert.That(verdict, Is.Not.Null);
            Assert.That(verdict.Confidence, Is.InRange(0.0, 1.0));
            Assert.That(verdict.Review.Checks.All(c => !string.IsNullOrEmpty(c.Label)), Is.True);
        }

        private static IEnumerable<string> JudgeExamples => ExampleNames("judge.200.");

        [Test]
        public void AcceptExampleCarriesTheSubjectAndChecks()
        {
            var judge = Judge(new FakeTransport(Respond("judge.200.accept.json")));

            var verdict = Run(judge.JudgeAsync(Jpeg, default));

            Assert.That(verdict.Subject, Is.EqualTo(EcoSubject.MyBottle));
            Assert.That(verdict.Label, Is.EqualTo("マイボトル"));
            Assert.That(verdict.Review.Decision, Is.EqualTo(EcoPhotoDecision.Accept));
            Assert.That(verdict.Review.Checks.Count, Is.EqualTo(3));
            Assert.That(judge.LastContribution.Current, Is.EqualTo(128));
        }

        [TestCase("judge.200.reduce.json", EcoPhotoDecision.Reduce, true)]
        [TestCase("judge.200.retake.json", EcoPhotoDecision.Retake, false)]
        [TestCase("judge.200.reject.json", EcoPhotoDecision.Reject, false)]
        public void DecisionDecidesWhetherThePhotoCounts(string name, EcoPhotoDecision decision, bool counts)
        {
            var verdict = Run(Judge(new FakeTransport(Respond(name))).JudgeAsync(Jpeg, default));

            Assert.That(verdict.Review.Decision, Is.EqualTo(decision));
            Assert.That(verdict.Review.Counts, Is.EqualTo(counts));
        }

        [Test]
        public void RetakeExampleCarriesTheHint()
        {
            var verdict = Run(Judge(new FakeTransport(Respond("judge.200.retake.json"))).JudgeAsync(Jpeg, default));

            Assert.That(verdict.Review.RetakeHint, Does.Contain("撮ってね"));
            Assert.That(verdict.IsEco, Is.False);
        }

        [Test]
        public void MinimalAnswerWithoutTheNewFieldsIsAccepted()
        {
            var verdict = Run(Judge(new FakeTransport(Respond("judge.200.minimal.json"))).JudgeAsync(Jpeg, default));

            Assert.That(verdict.Subject, Is.EqualTo(EcoSubject.Walk));
            Assert.That(verdict.Review.Decision, Is.EqualTo(EcoPhotoDecision.Accept));
            Assert.That(verdict.Review.Checks, Is.Empty);
        }

        [Test]
        public void UnknownSubjectAndDecisionFallToTheSafeSide()
        {
            var verdict = Run(Judge(new FakeTransport(Respond("compat/judge.200.unknown_values.json"))).JudgeAsync(Jpeg, default));

            Assert.That(verdict.Subject, Is.EqualTo(EcoSubject.None));
            Assert.That(verdict.IsEco, Is.False);
            Assert.That(verdict.Review.Decision, Is.EqualTo(EcoPhotoDecision.Accept));
        }

        [TestCaseSource(nameof(ErrorExamples))]
        public void EveryErrorExampleBecomesAJudgeErrorWithItsMessage(string name)
        {
            var error = JudgeFails(new FakeTransport(Respond(name)));

            var backend = (BackendException)error.InnerException;
            Assert.That(backend.Status, Is.EqualTo(StatusOf(name)));
            Assert.That(error.UserMessage, Is.Not.Empty);
            Assert.That(error.UserMessage, Is.EqualTo(backend.UserMessage));
        }

        private static IEnumerable<string> ErrorExamples => ExampleNames("error.");

        [Test]
        public void UnknownErrorCodeIsTreatedAsInternal()
        {
            var error = JudgeFails(new FakeTransport(Respond("compat/error.418.unknown_code.json")));

            Assert.That(((BackendException)error.InnerException).Code, Is.EqualTo(BackendErrorCodes.Internal));
        }

        [Test]
        public void ErrorWithoutTheContractBodyIsReadFromTheStatus()
        {
            var error = JudgeFails(new FakeTransport(new HttpResult(413, "<html>Request Entity Too Large</html>")));

            Assert.That(((BackendException)error.InnerException).Code, Is.EqualTo(BackendErrorCodes.ImageTooLarge));
        }

        [Test]
        public void RetryableErrorIsRetriedOnceThenSucceeds()
        {
            var transport = new FakeTransport(Respond("error.503.upstream_unavailable.json"), Respond("judge.200.accept.json"));

            var verdict = Run(Judge(transport).JudgeAsync(Jpeg, default));

            Assert.That(transport.Sent.Count, Is.EqualTo(2));
            Assert.That(verdict.Subject, Is.EqualTo(EcoSubject.MyBottle));
        }

        [Test]
        public void ResentPhotoKeepsItsIdempotencyKey()
        {
            var transport = new FakeTransport(Respond("error.503.upstream_unavailable.json"), Respond("judge.200.accept.json"));

            Run(Judge(transport).JudgeAsync(Jpeg, default));

            // 送り直しで同じキーなら、サーバーはみんなの氷山で 2 回数えない (契約の Idempotency-Key)
            var key = transport.Sent[0].IdempotencyKey;
            Assert.That(key, Does.Match("^[A-Za-z0-9-]{16,64}$"));
            Assert.That(transport.Sent[1].IdempotencyKey, Is.EqualTo(key));
        }

        [Test]
        public void EachPhotoGetsItsOwnIdempotencyKey()
        {
            var transport = new FakeTransport(Respond("judge.200.accept.json"));
            var judge = Judge(transport);

            Run(judge.JudgeAsync(Jpeg, default));
            Run(judge.JudgeAsync(Jpeg, default));

            Assert.That(transport.Sent[1].IdempotencyKey, Is.Not.EqualTo(transport.Sent[0].IdempotencyKey));
        }

        [Test]
        public void OnlyTheJudgeCarriesAnIdempotencyKey()
        {
            var transport = new FakeTransport(Respond("community-goal.200.active.json"));

            Run(new HttpCommunityGoalSource(new PenguinApiClient(Base, transport)).GetAsync(default));

            Assert.That(transport.Sent[0].IdempotencyKey, Is.Null);
        }

        [Test]
        public void ConnectionFailureIsRetriedOnceThenReportedAsNetwork()
        {
            var transport = new FakeTransport(HttpResult.Failed());

            var error = JudgeFails(transport);

            Assert.That(transport.Sent.Count, Is.EqualTo(2));
            Assert.That(((BackendException)error.InnerException).Code, Is.EqualTo(BackendErrorCodes.Network));
        }

        [Test]
        public void LongRetryAfterIsNotRetried()
        {
            var transport = new FakeTransport(Respond("error.429.rate_limited.json", "30"), Respond("judge.200.accept.json"));

            var error = JudgeFails(transport);

            Assert.That(transport.Sent.Count, Is.EqualTo(1));
            Assert.That(((BackendException)error.InnerException).RetryAfterSeconds, Is.EqualTo(30));
        }

        [Test]
        public void NonRetryableErrorIsNotRetried()
        {
            var transport = new FakeTransport(Respond("error.400.image_invalid.json"), Respond("judge.200.accept.json"));

            JudgeFails(transport);

            Assert.That(transport.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void EmptyOrOversizedPhotoIsNotSent()
        {
            var transport = new FakeTransport(Respond("judge.200.accept.json"));
            var judge = Judge(transport);

            Assert.Throws<EcoPhotoJudgeException>(() => Run(judge.JudgeAsync(Array.Empty<byte>(), default)));
            Assert.Throws<EcoPhotoJudgeException>(() => Run(judge.JudgeAsync(new byte[BackendLimits.MaxImageBytes + 1], default)));
            Assert.That(transport.Sent, Is.Empty);
        }

        [Test]
        public void PhotoThatFitsIsEncodedOnceAtFullQuality()
        {
            var tried = new List<PhotoFit.Step>();

            var jpeg = PhotoFit.Fit(step =>
            {
                tried.Add(step);
                return new byte[1000];
            });

            Assert.That(jpeg.Length, Is.EqualTo(1000));
            Assert.That(tried.Count, Is.EqualTo(1));
            Assert.That(tried[0].MaxSide, Is.EqualTo(BackendLimits.MaxImageSide));
            Assert.That(tried[0].Quality, Is.EqualTo(80));
        }

        [Test]
        public void LargePhotoLowersQualityBeforeShrinking()
        {
            var tried = new List<PhotoFit.Step>();

            // 品質 40 で長い辺 960 にしたときに初めて収まる写真
            var jpeg = PhotoFit.Fit(step =>
            {
                tried.Add(step);
                var fits = step.MaxSide <= 960;
                return new byte[fits ? BackendLimits.MaxImageBytes : BackendLimits.MaxImageBytes + 1];
            });

            Assert.That(jpeg.Length, Is.EqualTo(BackendLimits.MaxImageBytes));
            Assert.That(tried.Select(s => (s.MaxSide, s.Quality)), Is.EqualTo(new[]
            {
                (BackendLimits.MaxImageSide, 80), (BackendLimits.MaxImageSide, 60), (BackendLimits.MaxImageSide, 40), (960, 40),
            }));
        }

        [Test]
        public void PhotoThatNeverFitsReturnsTheSmallestSoTheSenderStopsIt()
        {
            var jpeg = PhotoFit.Fit(step => new byte[BackendLimits.MaxImageBytes + step.MaxSide]);

            Assert.That(jpeg.Length, Is.EqualTo(BackendLimits.MaxImageBytes + 640));
        }

        [Test]
        public void StoppedCameraEndsFittingWithNull()
        {
            var calls = 0;

            var jpeg = PhotoFit.Fit(_ =>
            {
                calls++;
                return null;
            });

            Assert.That(jpeg, Is.Null);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CancelledJudgeThrowsCancellation()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Catch<OperationCanceledException>(() =>
                Run(Judge(new FakeTransport(Respond("judge.200.accept.json"))).JudgeAsync(Jpeg, cts.Token)));
        }

        // ------------------------------------------------------------------
        // 判定の結論と資源
        // ------------------------------------------------------------------

        private static EcoPhotoVerdict Verdict(EcoPhotoDecision decision) =>
            new EcoPhotoVerdict(EcoSubject.EcoBag, "", 0.7, new EcoPhotoReview(decision, null, null));

        [Test]
        public void ReducedPhotoGivesHalfOfTheTable()
        {
            var p = new IslandParameters();
            var full = EcoGrant.ForPhoto(Verdict(EcoPhotoDecision.Accept), p);
            var reduced = EcoGrant.ForPhoto(Verdict(EcoPhotoDecision.Reduce), p);

            Assert.That(full[Resource.AllMighty], Is.EqualTo(15));
            Assert.That(reduced[Resource.AllMighty], Is.EqualTo(15 * p.PhotoReducedRate));
        }

        [TestCase(EcoPhotoDecision.Retake)]
        [TestCase(EcoPhotoDecision.Reject)]
        public void RefusedPhotoGivesNothing(EcoPhotoDecision decision)
        {
            Assert.That(EcoGrant.ForPhoto(Verdict(decision), new IslandParameters()).NonZero(), Is.Empty);
        }

        [Test]
        public void VerdictWithoutReviewKeepsTheOldBehaviour()
        {
            var verdict = new EcoPhotoVerdict(EcoSubject.EcoBag, "", 0.7);

            Assert.That(verdict.Review, Is.SameAs(EcoPhotoReview.None));
            Assert.That(EcoGrant.ForPhoto(verdict, new IslandParameters())[Resource.AllMighty], Is.EqualTo(15));
        }

        // ------------------------------------------------------------------
        // 会場のデモで判定サーバーが落ちたとき
        // ------------------------------------------------------------------

        [Test]
        public void DemoFallsBackToTheLocalJudgeAfterRepeatedFailuresAndSaysSo()
        {
            var transport = new FakeTransport(HttpResult.Failed());
            var judge = new FallbackEcoPhotoJudge(Judge(transport), new MockEcoPhotoJudge(TimeSpan.Zero), 2);

            Assert.Throws<EcoPhotoJudgeException>(() => Run(judge.JudgeAsync(Jpeg, default)));
            Assert.That(judge.IsOffline, Is.False);

            var verdict = Run(judge.JudgeAsync(Jpeg, default));

            Assert.That(judge.IsOffline, Is.True);
            Assert.That(verdict.Review.Counts, Is.True);
            Assert.That(verdict.Review.Checks.Last().Id, Is.EqualTo(FallbackEcoPhotoJudge.OfflineCheckId));
        }

        [Test]
        public void NonRetryableErrorDoesNotSwitchToTheLocalJudge()
        {
            var transport = new FakeTransport(Respond("error.400.image_invalid.json"));
            var judge = new FallbackEcoPhotoJudge(Judge(transport), new MockEcoPhotoJudge(TimeSpan.Zero), 1);

            Assert.Throws<EcoPhotoJudgeException>(() => Run(judge.JudgeAsync(Jpeg, default)));
            Assert.That(judge.IsOffline, Is.False);
        }

        // ------------------------------------------------------------------
        // みんなの氷山
        // ------------------------------------------------------------------

        private static HttpCommunityGoalSource Community(FakeTransport t) => new HttpCommunityGoalSource(new PenguinApiClient(Base, t));

        [Test]
        public void ActiveGoalIsRead()
        {
            var transport = new FakeTransport(Respond("community-goal.200.active.json"));

            var goal = Run(Community(transport).GetAsync(default));

            Assert.That(transport.Sent.Single().Url, Is.EqualTo(Base + "/v1/community/goal"));
            Assert.That(goal.Target, Is.EqualTo(300));
            Assert.That(goal.Progress, Is.EqualTo(128 / 300.0).Within(1e-9));
            Assert.That(goal.Milestones.Select(m => m.At), Is.EqualTo(new[] { 100, 200, 300 }));
            Assert.That(goal.PollSeconds, Is.EqualTo(15));
            Assert.That(goal.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 10, 24, 5, 12, 30, TimeSpan.Zero)));
        }

        [Test]
        public void DisabledOrFailingGoalIsHidden()
        {
            Assert.That(Run(Community(new FakeTransport(Respond("community-goal.200.disabled.json"))).GetAsync(default)), Is.Null);
            Assert.That(Run(Community(new FakeTransport(Respond("error.500.internal.json"))).GetAsync(default)), Is.Null);
            Assert.That(Run(Community(new FakeTransport(HttpResult.Failed())).GetAsync(default)), Is.Null);
        }

        // ------------------------------------------------------------------
        // お題
        // ------------------------------------------------------------------

        private static HttpQuestSource Quests(FakeTransport t) => new HttpQuestSource(new PenguinApiClient(Base, t));

        [Test]
        public void QuestsAreRead()
        {
            var quests = Run(Quests(new FakeTransport(Respond("quests.200.some.json"))).ListAsync(default));

            Assert.That(quests.Select(q => q.Id), Is.EqualTo(new[] { "bottle-week", "venue-tkb" }));
            Assert.That(quests[0].Proof, Is.EqualTo(QuestProof.Photo));
            Assert.That(quests[0].Subjects, Is.EqualTo(new[] { EcoSubject.MyBottle }));
            Assert.That(quests[1].Reward.Kind, Is.EqualTo(QuestRewardKind.Decoration));
            Assert.That(quests[1].IsOpen(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero)), Is.True);
            Assert.That(quests[1].IsOpen(new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero)), Is.False);
        }

        [Test]
        public void QuestsOfUnknownKindAreLeftOut()
        {
            var quests = Run(Quests(new FakeTransport(Respond("compat/quests.200.unknown_kind.json"))).ListAsync(default));

            Assert.That(quests.Select(q => q.Id), Is.EqualTo(new[] { "eco-bag" }));
        }

        [Test]
        public void QuestsAreEmptyWhenTheServerIsAway()
        {
            Assert.That(Run(Quests(new FakeTransport(Respond("quests.200.empty.json"))).ListAsync(default)), Is.Empty);
            Assert.That(Run(Quests(new FakeTransport(HttpResult.Failed())).ListAsync(default)), Is.Empty);
        }

        [Test]
        public void RedeemSendsTheCodeAndReadsTheReward()
        {
            var transport = new FakeTransport(Respond("redeem.200.accepted.json"));

            var result = Run(Quests(transport).RedeemAsync("venue-tkb", "TKB-2611", default));

            var sent = transport.Sent.Single();
            Assert.That(sent.Url, Is.EqualTo(Base + "/v1/quests/venue-tkb/redeem"));
            Assert.That(sent.ContentType, Is.EqualTo("application/json"));
            Assert.That(Encoding.UTF8.GetString(sent.Body), Is.EqualTo("{\"code\":\"TKB-2611\"}"));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Reward.Id, Is.EqualTo("jphacks_flag"));
        }

        [TestCase("error.400.code_invalid.json", BackendErrorCodes.CodeInvalid)]
        [TestCase("error.404.quest_closed.json", BackendErrorCodes.QuestClosed)]
        [TestCase("error.409.already_redeemed.json", BackendErrorCodes.AlreadyRedeemed)]
        public void RefusedRedeemCarriesTheReason(string name, string code)
        {
            var result = Run(Quests(new FakeTransport(Respond(name))).RedeemAsync("venue-tkb", "x", default));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(code));
            Assert.That(result.Message, Is.Not.Empty);
        }

        // ------------------------------------------------------------------
        // 設定
        // ------------------------------------------------------------------

        [Test]
        public void MissingOrBrokenConfigUsesTheLocalJudge()
        {
            Assert.That(BackendConfig.Parse("", null), Is.SameAs(BackendConfig.Default));
            Assert.That(BackendConfig.Parse("{ broken", null), Is.SameAs(BackendConfig.Default));
            Assert.That(BackendConfig.Default.Judge, Is.EqualTo(JudgeMode.Mock));
            Assert.That(BackendConfig.Default.UsesBackend, Is.False);
        }

        [Test]
        public void RelativeApiUrlIsResolvedFromThePage()
        {
            var config = BackendConfig.Parse("{\"api_base_url\": \"/api\", \"judge\": \"http\", \"community\": true}",
                "https://project-penguin-nu.vercel.app/index.html");

            Assert.That(config.ApiBaseUrl, Is.EqualTo("https://project-penguin-nu.vercel.app/api"));
            Assert.That(config.HealthUrl, Is.EqualTo("https://project-penguin-nu.vercel.app/api/v1/health"));
            Assert.That(config.Judge, Is.EqualTo(JudgeMode.Http));
            Assert.That(config.CommunityEnabled, Is.True);
            Assert.That(config.QuestsEnabled, Is.False);
        }

        [Test]
        public void RelativeApiUrlWithoutAPageTurnsTheBackendOff()
        {
            // Editor・iOS には開いているページが無いので、相対の URL は解けない。
            var config = BackendConfig.Parse("{\"api_base_url\": \"/api\", \"judge\": \"http\", \"community\": true}", "");

            Assert.That(config.UsesBackend, Is.False);
            Assert.That(config.Judge, Is.EqualTo(JudgeMode.Mock));
            Assert.That(config.CommunityEnabled, Is.False);
        }

        [Test]
        public void ServicesFollowTheConfig()
        {
            var transport = new FakeTransport(Respond("health.200.ok.json"));

            Assert.That(BackendServices.Create(BackendConfig.Default, transport).Judge, Is.InstanceOf<MockEcoPhotoJudge>());

            var http = BackendServices.Create(new BackendConfig(Base, JudgeMode.Http, true, true, false), transport);
            Assert.That(http.Judge, Is.InstanceOf<HttpEcoPhotoJudge>());
            Assert.That(http.Community, Is.InstanceOf<HttpCommunityGoalSource>());
            Assert.That(http.Quests, Is.InstanceOf<HttpQuestSource>());

            var demo = BackendServices.Create(new BackendConfig(Base, JudgeMode.Http, false, false, true), transport);
            Assert.That(demo.Judge, Is.InstanceOf<FallbackEcoPhotoJudge>());
            Assert.That(demo.Community, Is.Null);
        }

        [Test]
        public void ShippedConfigsParse()
        {
            var inBuild = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, BackendRuntime.ConfigFileName));
            Assert.That(BackendConfig.Parse(inBuild, null).Judge, Is.EqualTo(JudgeMode.Mock));
            Assert.That(BackendConfig.Parse(inBuild, null).Water, Is.EqualTo(JudgeMode.Http), "工場ゲームの水は写真の見積もりを使う");
            // Web 版の公開用の設定 (deploy/vercel/) は凍結した Web 版とともに project-penguin に残し、ここには取り込んでいない。
        }

        // ------------------------------------------------------------------
        // 水の写真の見積もり (契約 1.2.0、容量は 1.3.0 からラベルの表示)
        // ------------------------------------------------------------------

        private static HttpWaterEstimator Water(FakeTransport transport) => new HttpWaterEstimator(new PenguinApiClient(Base, transport));

        [Test]
        public void WaterSendsTheJpegAsMultipartToTheContractPath()
        {
            var transport = new FakeTransport(Respond("water-estimate.200.accept.json"));

            Run(Water(transport).EstimateAsync(Jpeg, default));

            var sent = transport.Sent.Single();
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo(Base + "/v1/water/estimate"));
            Assert.That(sent.ContentType, Does.StartWith("multipart/form-data; boundary="));
            Assert.That(Encoding.UTF8.GetString(sent.Body, 0, Math.Min(200, sent.Body.Length)), Does.Contain("name=\"image\""));
        }

        [TestCaseSource(nameof(WaterExamples))]
        public void EveryWaterExampleBecomesAnEstimate(string name)
        {
            var estimate = Run(Water(new FakeTransport(Respond(name))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.FillRatio, Is.InRange(0.0, 1.0));
            Assert.That(estimate.AmountMl, Is.InRange(0, WaterIntake.MaxMlPerEntry));
            Assert.That(estimate.Checks.All(c => !string.IsNullOrEmpty(c.Label)), Is.True);
        }

        private static IEnumerable<string> WaterExamples => ExampleNames("water-estimate.200.");

        [Test]
        public void AcceptedWaterCarriesTheAmount()
        {
            var estimate = Run(Water(new FakeTransport(Respond("water-estimate.200.accept.json"))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.Container, Is.EqualTo(WaterContainer.PetBottle));
            Assert.That(estimate.CapacityMl, Is.EqualTo(600), "容量はラベルから読んだ値");
            Assert.That(estimate.AmountMl, Is.EqualTo(30));
            Assert.That(estimate.Decision, Is.EqualTo(WaterDecision.Accept));
            Assert.That(estimate.IsUsable, Is.True);
        }

        [Test]
        public void UnreadableCapacityAsksToShowTheLabel()
        {
            var estimate = Run(Water(new FakeTransport(Respond("water-estimate.200.retake_capacity.json"))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.Container, Is.EqualTo(WaterContainer.PetBottle));
            Assert.That(estimate.CapacityMl, Is.EqualTo(0));
            Assert.That(estimate.Decision, Is.EqualTo(WaterDecision.Retake));
            Assert.That(estimate.RetakeHint, Does.Contain("ラベルの容量"));
            Assert.That(WaterEmptyBottle.Judge(estimate.ToPhoto(DateTimeOffset.UnixEpoch)).Failure, Is.EqualTo(WaterBottleFailure.NotAccepted));
        }

        [TestCase("pet_bottle_500")]
        [TestCase("pet_bottle_350")]
        public void OldServerWithoutLabelCapacityIsNotGuessed(string container)
        {
            // 1.2.0 のサーバーは容量を容器の値 (350・500) で伝えていた。こちらで容量を決めず、撮り直しにする。
            var json = "{\"schema_version\":\"1.2.0\",\"container\":\"" + container +
                       "\",\"fill_ratio\":0,\"amount_ml\":0,\"confidence\":0.9,\"decision\":\"accept\"}";
            var estimate = Run(Water(new FakeTransport(new HttpResult(200, json))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.CapacityMl, Is.EqualTo(0));
            Assert.That(estimate.Decision, Is.EqualTo(WaterDecision.Retake));
            Assert.That(estimate.RetakeHint, Is.EqualTo(HttpWaterEstimator.CapacityRetakeHint));
            Assert.That(WaterEmptyBottle.Judge(estimate.ToPhoto(DateTimeOffset.UnixEpoch)).Succeeded, Is.False);
        }

        [Test]
        public void EmptyBottleWithReadCapacityBecomesTheIntake()
        {
            var json = "{\"schema_version\":\"1.3.0\",\"container\":\"pet_bottle\",\"capacity_ml\":2000,\"fill_ratio\":0.02," +
                       "\"amount_ml\":40,\"confidence\":0.9,\"decision\":\"accept\"}";
            var estimate = Run(Water(new FakeTransport(new HttpResult(200, json))).EstimateAsync(Jpeg, default));

            var result = WaterEmptyBottle.Judge(estimate.ToPhoto(DateTimeOffset.UnixEpoch));
            Assert.That(result.Intake.Ml, Is.EqualTo(1960), "2L のボトルなら 2000 − 残り");
        }

        [TestCase("water-estimate.200.retake.json", WaterDecision.Retake)]
        [TestCase("water-estimate.200.reject.json", WaterDecision.Reject)]
        public void RetakeAndRejectAreNotUsable(string name, WaterDecision decision)
        {
            var estimate = Run(Water(new FakeTransport(Respond(name))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.Decision, Is.EqualTo(decision));
            Assert.That(estimate.IsUsable, Is.False);
        }

        [Test]
        public void UnknownWaterValuesFallBackToRetakeWithoutAmount()
        {
            // 未来の版の容器 (glass_bottle) と結論 (partial)。知らない容器は容量があっても量を使わない。
            var estimate = Run(Water(new FakeTransport(Respond("compat/water-estimate.200.unknown_values.json"))).EstimateAsync(Jpeg, default));

            Assert.That(estimate.Container, Is.EqualTo(WaterContainer.Unknown));
            Assert.That(estimate.Decision, Is.EqualTo(WaterDecision.Retake));
            Assert.That(estimate.AmountMl, Is.EqualTo(0));
            Assert.That(estimate.IsUsable, Is.False);
        }

        [Test]
        public void WaterErrorKeepsTheBackendCode()
        {
            var estimator = Water(new FakeTransport(Respond("error.413.image_too_large.json")));
            var error = Assert.Throws<WaterEstimateException>(() => Run(estimator.EstimateAsync(Jpeg, default)));
            Assert.That(error.Code, Is.EqualTo(BackendErrorCodes.ImageTooLarge));
        }

        [Test]
        public void ConfigTurnsOnWaterAndEncounters()
        {
            var inBuild = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, BackendRuntime.ConfigFileName));
            var shipped = BackendConfig.Parse(inBuild, null);
            Assert.That(shipped.Judge, Is.EqualTo(JudgeMode.Http));
            Assert.That(shipped.Water, Is.EqualTo(JudgeMode.Http));

            var config = BackendConfig.Parse("{\"api_base_url\":\"https://x.example/api\",\"water\":\"http\",\"encounters\":true}", null);
            Assert.That(config.Water, Is.EqualTo(JudgeMode.Http));
            Assert.That(config.EncountersEnabled, Is.True);

            var services = BackendServices.Create(config, new FakeTransport(Respond("health.200.ok.json")));
            Assert.That(services.Water, Is.InstanceOf<HttpWaterEstimator>());
            Assert.That(services.Encounters, Is.InstanceOf<HttpEncounterReporter>());

            var offline = BackendServices.Create(BackendConfig.Parse("{\"water\":\"http\",\"encounters\":true}", null),
                new FakeTransport(Respond("health.200.ok.json")));
            Assert.That(offline.Water, Is.InstanceOf<MockWaterEstimator>(), "接続先が無ければ見本");
            Assert.That(offline.Encounters, Is.InstanceOf<MockEncounterReporter>());
        }
    }
}
