using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.HealthFactory;
using ProjectPenguin.Tests.EditMode.Backend;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>すれ違いのトークン・台帳・ペンギンの抽選・工場への効果と、POST /v1/encounters の口。</summary>
    public sealed class EncounterTests
    {
        // api/openapi.yaml の EncounterResult.pair_id の例。
        private const string A = "AQEBAQEBAQEBAQEBAQEBAQ";
        private const string B = "AgICAgICAgICAgICAgICAg";
        private const string ContractPairId = "e5ecc329c8fcc058";
        private const int Day = 297; // 2026-10-25

        private static EncounterLedger Ledger(params string[] tokens)
        {
            var queue = new Queue<string>(tokens.Length > 0 ? tokens : new[] { A });
            var ledger = new EncounterLedger(() => queue.Dequeue());
            ledger.BeginDay(Day);
            return ledger;
        }

        [Test]
        public void TokenIsBase64UrlOf16BytesWithoutPadding()
        {
            Assert.That(EncounterToken.FromBytes(Enumerable.Repeat((byte)1, 16).ToArray()), Is.EqualTo(A));
            var created = EncounterToken.Create();
            Assert.That(created.Length, Is.EqualTo(22));
            Assert.That(EncounterToken.IsValid(created), Is.True);
            Assert.That(EncounterToken.Create(), Is.Not.EqualTo(created));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AQEBAQEBAQEBAQEBAQEBA")]
        [TestCase("AQEBAQEBAQEBAQEBAQEBAQE")]
        [TestCase("AQEBAQEBAQEBAQEBAQEBA=")]
        [TestCase("AQEBAQEBAQEBAQEBAQEBA+")]
        [TestCase("AQEBAQEBAQEBAQEBAQEBAQ\n")]
        public void MalformedTokensAreInvalid(string token)
        {
            Assert.That(EncounterToken.IsValid(token), Is.False);
        }

        [Test]
        public void PairIdMatchesTheContractExampleInEitherOrder()
        {
            Assert.That(EncounterToken.PairId(A, B), Is.EqualTo(ContractPairId));
            Assert.That(EncounterToken.PairId(B, A), Is.EqualTo(ContractPairId));
        }

        [Test]
        public void ContractDayIsTheAppDayInJst()
        {
            Assert.That(EncounterLedger.ContractDay(Day), Is.EqualTo("2026-10-25"));
        }

        [Test]
        public void ObserveRejectsSelfMalformedAndDuplicateTokens()
        {
            var ledger = Ledger();
            Assert.That(ledger.Observe(A, 0), Is.EqualTo(EncounterObservation.Self));
            Assert.That(ledger.Observe("short", 0), Is.EqualTo(EncounterObservation.Invalid));
            Assert.That(ledger.Observe(B, 0), Is.EqualTo(EncounterObservation.Accepted));
            Assert.That(ledger.Observe(B, 0), Is.EqualTo(EncounterObservation.Duplicate));
            Assert.That(ledger.Seen, Is.EqualTo(1));
        }

        [Test]
        public void ObserveBeforeTheDayStartsIsRejected()
        {
            Assert.That(new EncounterLedger().Observe(B, 0), Is.EqualTo(EncounterObservation.NoDay));
        }

        [Test]
        public void PeersPerDayAreCapped()
        {
            var ledger = Ledger();
            for (var i = 0; i < EncounterLedger.MaxPeersPerDay; i++)
            {
                Assert.That(ledger.Observe(EncounterToken.Create(), 0), Is.EqualTo(EncounterObservation.Accepted));
            }

            Assert.That(ledger.Observe(B, 0), Is.EqualTo(EncounterObservation.Full));
        }

        [Test]
        public void NewDayRotatesTheTokenAndClearsTheLedgerButClockBackwardDoesNot()
        {
            var ledger = Ledger(A, B);
            ledger.Observe(EncounterToken.Create(), 0);
            Assert.That(ledger.BeginDay(Day), Is.False);
            Assert.That(ledger.BeginDay(Day - 1), Is.False);
            Assert.That(ledger.SelfToken, Is.EqualTo(A));
            Assert.That(ledger.BeginDay(Day + 1), Is.True);
            Assert.That(ledger.SelfToken, Is.EqualTo(B));
            Assert.That(ledger.Seen, Is.Zero);
        }

        [Test]
        public void WaitingReportIsResentAfterTheRetryInterval()
        {
            var ledger = Ledger();
            ledger.Observe(B, 1000);
            var report = ledger.NextReport(1000);
            Assert.That(report.Day, Is.EqualTo("2026-10-25"));
            Assert.That(report.SelfToken, Is.EqualTo(A));
            Assert.That(report.PeerToken, Is.EqualTo(B));
            Assert.That(ledger.NextReport(1000), Is.Null, "結果を待つ間は同じ相手を選ばない");

            Assert.That(ledger.ApplyResult(report, false, ContractPairId, 2000).Outcome, Is.EqualTo(EncounterOutcome.Waiting));
            Assert.That(ledger.NextReport(2000 + EncounterLedger.RetryMs - 1), Is.Null);
            Assert.That(ledger.NextReport(2000 + EncounterLedger.RetryMs), Is.Not.Null);
        }

        [Test]
        public void FailedReportWaitsForRetryAfterWhenLonger()
        {
            var ledger = Ledger();
            ledger.Observe(B, 0);
            var report = ledger.NextReport(0);
            ledger.ReportFailed(report, 0, 120000);
            Assert.That(ledger.NextReport(EncounterLedger.RetryMs), Is.Null);
            Assert.That(ledger.NextReport(120000), Is.Not.Null);
        }

        [Test]
        public void DiscardedPeerIsNotReportedAgain()
        {
            var ledger = Ledger();
            ledger.Observe(B, 0);
            ledger.Discard(ledger.NextReport(0));
            Assert.That(ledger.NextReport(10 * EncounterLedger.RetryMs), Is.Null);
            Assert.That(ledger.Waiting, Is.Zero);
            Assert.That(ledger.Observe(B, 0), Is.EqualTo(EncounterObservation.Duplicate));
        }

        [Test]
        public void MatchedPairGrantsOncePerDay()
        {
            var ledger = Ledger();
            ledger.Observe(B, 0);
            var report = ledger.NextReport(0);
            var first = ledger.ApplyResult(report, true, ContractPairId, 0);
            Assert.That(first.Outcome, Is.EqualTo(EncounterOutcome.Granted));
            Assert.That(first.Species, Is.SameAs(EncounterPenguins.Draw(ContractPairId)));
            Assert.That(ledger.ApplyResult(report, true, ContractPairId, 0).Outcome, Is.EqualTo(EncounterOutcome.Ignored));
            Assert.That(ledger.NextReport(10 * EncounterLedger.RetryMs), Is.Null);
            Assert.That(ledger.GrantedToday, Is.EqualTo(1));
            Assert.That(ledger.Matched, Is.EqualTo(1));
        }

        [Test]
        public void ResultWithAnotherPairIdOrFromYesterdayIsIgnored()
        {
            var ledger = Ledger(A, A);
            ledger.Observe(B, 0);
            var report = ledger.NextReport(0);
            Assert.That(ledger.ApplyResult(report, true, "0000000000000000", 0).Outcome, Is.EqualTo(EncounterOutcome.Ignored));
            ledger.BeginDay(Day + 1);
            ledger.Observe(B, 0);
            Assert.That(ledger.ApplyResult(report, true, ContractPairId, 0).Outcome, Is.EqualTo(EncounterOutcome.Ignored),
                "日が替わる前に出した報告の結果は今日の台帳に入れない");
        }

        [Test]
        public void PenguinsPerDayAreCapped()
        {
            var ledger = Ledger();
            for (var i = 0; i <= EncounterLedger.PenguinsPerDay; i++)
            {
                var peer = EncounterToken.Create();
                ledger.Observe(peer, 0);
                var outcome = ledger.ApplyResult(ledger.NextReport(0), true, EncounterToken.PairId(A, peer), 0).Outcome;
                Assert.That(outcome, Is.EqualTo(i < EncounterLedger.PenguinsPerDay ? EncounterOutcome.Granted : EncounterOutcome.CapReached));
            }
        }

        [Test]
        public void DrawIsTheSameOnBothDevicesAndFollowsTheWeights()
        {
            Assert.That(EncounterPenguins.S1.Sum(s => s.Weight), Is.EqualTo(6000));
            // 先頭 8 桁 mod 6000 で決まる: 0 → アデリー、1399 → アデリー、1400 → ヒゲ、5999 → マゼラン。
            Assert.That(EncounterPenguins.Draw("00000000ffffffff").Id, Is.EqualTo("adelie"));
            Assert.That(EncounterPenguins.Draw("00000577ffffffff").Id, Is.EqualTo("adelie"));
            Assert.That(EncounterPenguins.Draw("00000578ffffffff").Id, Is.EqualTo("chinstrap"));
            Assert.That(EncounterPenguins.Draw("0000176fffffffff").Id, Is.EqualTo("magellanic"));
            Assert.That(EncounterPenguins.Draw(EncounterToken.PairId(A, B)), Is.SameAs(EncounterPenguins.Draw(EncounterToken.PairId(B, A))));
            Assert.Throws<ArgumentException>(() => EncounterPenguins.Draw("xyz"));
        }

        [Test]
        public void GasPenguinAddsTenPercentToFutureStepsOnlyWithCarry()
        {
            var f = new HealthFactoryFacade();
            Assert.That(f.AcceptSteps(Day, 2500, Day).Delta, Is.EqualTo(5));
            Assert.That(f.GrantPenguin("western_rockhopper").Success, Is.True);
            Assert.That(f.CreateView().Gas, Is.EqualTo(5), "受領済みの歩数にはさかのぼらない");
            // 5 ガス × 10% = 0.5 → 0 (0.5 を繰り越し)、次の 5 ガスで 0.5 + 0.5 = 1。
            Assert.That(f.AcceptSteps(Day, 5000, Day).Delta, Is.EqualTo(5));
            Assert.That(f.AcceptSteps(Day, 7500, Day).Delta, Is.EqualTo(6));
            var v = f.CreateView();
            Assert.That(v.Gas, Is.EqualTo(16));
            Assert.That(v.GasBonusPercent, Is.EqualTo(10));
            Assert.That(v.Penguins["western_rockhopper"], Is.EqualTo(1));
        }

        [Test]
        public void WaterAndElectricityPenguinsAddToTheirOwnResource()
        {
            var f = new HealthFactoryFacade();
            for (var i = 0; i < 4; i++)
            {
                f.GrantPenguin("magellanic");
            }

            f.GrantPenguin("king");
            Assert.That(f.AcceptWater("w", Day, Day, true).Delta, Is.EqualTo(4), "水 3 × 40% = 1.2 → 1");
            Assert.That(f.AcceptScreen("s", Day - 1, 240, Day, true, true).Delta, Is.EqualTo(11), "電気 10 × 10% = 1");
            Assert.That(f.AcceptSteps(Day, 5000, Day).Delta, Is.EqualTo(10), "ガスには効かない");
        }

        [Test]
        public void PenguinWithoutPrototypeResourceIsOnlyRecorded()
        {
            var f = new HealthFactoryFacade();
            Assert.That(f.GrantPenguin("adelie").Success, Is.True);
            Assert.That(f.AcceptSteps(Day, 5000, Day).Delta, Is.EqualTo(10));
            Assert.That(f.GrantPenguin("unknown").Success, Is.False);
            Assert.That(f.CreateView().Penguins.Count, Is.EqualTo(1));
        }

        [Test]
        public void ViewPenguinsIsACopy()
        {
            var f = new HealthFactoryFacade();
            f.GrantPenguin("king");
            var before = f.CreateView().Penguins;
            f.GrantPenguin("king");
            Assert.That(before["king"], Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)before)["king"] = 5);
        }

        // ------------------------------------------------------------------
        // POST /v1/encounters
        // ------------------------------------------------------------------

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
                Sent.Add(request);
                return Task.FromResult(_results.Count > 1 ? _results.Dequeue() : _results.Peek());
            }
        }

        private static HttpResult Respond(string name, string retryAfter = null) =>
            new HttpResult(long.Parse(name.Split('.')[1]), BackendContractTests.Example(name), retryAfter);

        private static EncounterReply Report(FakeTransport transport) =>
            new HttpEncounterReporter(new PenguinApiClient("https://penguin.example/api", transport))
                .ReportAsync("2026-10-25", A, B, default).GetAwaiter().GetResult();

        [TestCase("encounter.200.waiting.json", false)]
        [TestCase("encounter.200.matched.json", true)]
        public void ReporterReadsTheContractExamples(string name, bool matched)
        {
            var transport = new FakeTransport(Respond(name));
            var reply = Report(transport);
            Assert.That(reply.Matched, Is.EqualTo(matched));
            Assert.That(reply.PairId, Is.EqualTo(ContractPairId));

            var sent = transport.Sent.Single();
            Assert.That(sent.Method, Is.EqualTo("POST"));
            Assert.That(sent.Url, Is.EqualTo("https://penguin.example/api/v1/encounters"));
            Assert.That(sent.ContentType, Is.EqualTo("application/json"));
            var body = JsonUtility.FromJson<EncounterBody>(Encoding.UTF8.GetString(sent.Body));
            Assert.That(body.day, Is.EqualTo("2026-10-25"));
            Assert.That(body.self_token, Is.EqualTo(A));
            Assert.That(body.peer_token, Is.EqualTo(B));
        }

        [TestCase("error.400.token_invalid.json", BackendErrorCodes.TokenInvalid)]
        [TestCase("error.400.day_invalid.json", BackendErrorCodes.DayInvalid)]
        public void BadRequestIsNotRetryable(string name, string code)
        {
            var transport = new FakeTransport(Respond(name));
            var error = Assert.Throws<BackendException>(() => Report(transport));
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(error.Retryable, Is.False);
            Assert.That(transport.Sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void RateLimitedCarriesRetryAfter()
        {
            var error = Assert.Throws<BackendException>(() => Report(new FakeTransport(Respond("error.429.rate_limited.json", "30"))));
            Assert.That(error.Code, Is.EqualTo(BackendErrorCodes.RateLimited));
            Assert.That(error.RetryAfterSeconds, Is.EqualTo(30));
        }

        [Test]
        public void MockReporterMatchesOnlyWhenBothSidesReport()
        {
            var mock = new MockEncounterReporter();
            var first = mock.ReportAsync("2026-10-25", A, B, default).GetAwaiter().GetResult();
            Assert.That(first.Matched, Is.False);
            Assert.That(first.PairId, Is.EqualTo(ContractPairId));
            Assert.That(mock.ReportAsync("2026-10-25", A, B, default).GetAwaiter().GetResult().Matched, Is.False, "同じ側の送り直しでは成立しない");
            Assert.That(mock.ReportAsync("2026-10-25", B, A, default).GetAwaiter().GetResult().Matched, Is.True);
            Assert.That(mock.ReportAsync("2026-10-26", B, A, default).GetAwaiter().GetResult().Matched, Is.False, "日ごとに別の記録");
            Assert.Throws<BackendException>(() => mock.ReportAsync("2026-10-25", A, A, default).GetAwaiter().GetResult());
        }

        [Test]
        public void ConfigReadsTheEncountersSwitchOnlyWithABackend()
        {
            Assert.That(BackendConfig.Parse("{\"api_base_url\":\"https://penguin.example/api\",\"encounters\":true}", null).EncountersEnabled, Is.True);
            Assert.That(BackendConfig.Parse("{\"api_base_url\":\"\",\"encounters\":true}", null).EncountersEnabled, Is.False);
            Assert.That(BackendConfig.Parse("{\"api_base_url\":\"https://penguin.example/api\"}", null).EncountersEnabled, Is.False);
        }

        [Test]
        public void ShippedConfigReportsEncountersToTheDeployedBackend()
        {
            var inBuild = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath, BackendRuntime.ConfigFileName));
            var config = BackendConfig.Parse(inBuild, null);
            Assert.That(config.ApiBaseUrl, Is.EqualTo("https://project-penguin-nu.vercel.app/api"));
            Assert.That(config.EncountersEnabled, Is.True);
            Assert.That(BackendServices.Create(config, new FakeTransport(Respond("encounter.200.waiting.json"))).Encounters,
                Is.InstanceOf<HttpEncounterReporter>());
            Assert.That(BackendServices.Create(BackendConfig.Default, new FakeTransport()).Encounters, Is.InstanceOf<MockEncounterReporter>());
        }

#pragma warning disable IDE1006 // 契約の項目名に合わせる
        [Serializable]
        private sealed class EncounterBody
        {
            public string day;
            public string self_token;
            public string peer_token;
        }
#pragma warning restore IDE1006
    }
}
