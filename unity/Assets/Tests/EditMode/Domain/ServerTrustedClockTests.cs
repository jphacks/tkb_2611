using System;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>v5：HTTP-date の読み取りと、サーバーの Date を基準にする時計 (19b-clock-save.md)。</summary>
    public class HttpDateTests
    {
        private static readonly DateTimeOffset Expected = new DateTimeOffset(1994, 11, 6, 8, 49, 37, TimeSpan.Zero);

        [TestCase("Sun, 06 Nov 1994 08:49:37 GMT")]
        [TestCase("Sunday, 06-Nov-94 08:49:37 GMT")]
        [TestCase("Sun Nov  6 08:49:37 1994")]
        [TestCase("  Sun, 06 Nov 1994 08:49:37 GMT  ")]
        public void ParsesAllThreeRfc9110Formats(string value)
        {
            Assert.That(HttpDate.TryParse(value, out var utc, new DateTimeOffset(1995, 1, 1, 0, 0, 0, TimeSpan.Zero)), Is.True);
            Assert.That(utc, Is.EqualTo(Expected));
            Assert.That(utc.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void AsctimeWithTwoDigitDay()
        {
            Assert.That(HttpDate.TryParse("Thu Oct 15 23:59:59 2026", out var utc), Is.True);
            Assert.That(utc, Is.EqualTo(new DateTimeOffset(2026, 10, 15, 23, 59, 59, TimeSpan.Zero)));
        }

        [Test]
        public void Rfc850TwoDigitYearMoreThan50YearsAheadMeansPastCentury()
        {
            var reference = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
            Assert.That(HttpDate.TryParse("Friday, 16-Oct-26 01:02:03 GMT", out var a, reference), Is.True);
            Assert.That(a.Year, Is.EqualTo(2026));
            Assert.That(HttpDate.TryParse("Sunday, 06-Nov-94 08:49:37 GMT", out var b, reference), Is.True);
            Assert.That(b.Year, Is.EqualTo(1994));
        }

        [Test]
        public void LeapSecondIsClampedTo59()
        {
            Assert.That(HttpDate.TryParse("Wed, 31 Dec 2025 23:59:60 GMT", out var utc), Is.True);
            Assert.That(utc, Is.EqualTo(new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("garbage")]
        [TestCase("Sun, 06 Nov 1994 08:49:37 UTC")]
        [TestCase("Sun, 06 Nov 1994 08:49:37 +0000")]
        [TestCase("Sun, 31 Feb 1994 08:49:37 GMT")]
        [TestCase("Sun, 06 Foo 1994 08:49:37 GMT")]
        [TestCase("Sun, 06 Nov 1994 24:00:00 GMT")]
        [TestCase("2026-10-16T00:00:00Z")]
        [TestCase("sun, 06 nov 1994 08:49:37 gmt")]
        public void RejectsInvalidValues(string value)
        {
            Assert.That(HttpDate.TryParse(value, out _), Is.False);
        }

        [Test]
        public void FormatRoundTrips()
        {
            var t = new DateTimeOffset(2026, 10, 16, 1, 2, 3, TimeSpan.FromHours(9));
            var s = HttpDate.Format(t);
            Assert.That(s, Is.EqualTo("Thu, 15 Oct 2026 16:02:03 GMT"));
            Assert.That(HttpDate.TryParse(s, out var back), Is.True);
            Assert.That(back, Is.EqualTo(t));
        }
    }

    public class ServerTrustedClockTests
    {
        // 2026-10-16 12:00 JST
        private static readonly DateTimeOffset Server = new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero);

        private ManualClock _device;
        private ManualMonotonicClock _mono;
        private ServerTrustedClock _clock;

        [SetUp]
        public void SetUp()
        {
            _device = new ManualClock(Server) { IsTrusted = false };
            _mono = new ManualMonotonicClock(1000.0);
            _clock = new ServerTrustedClock(_device, _mono);
        }

        /// <summary>送信、往復 rtt 秒後に受信、の順で単調時計を進めて応答を渡す。</summary>
        private ServerTimeSample Respond(DateTimeOffset serverDate, double rtt = 0.2, string age = null)
        {
            var sent = _mono.Seconds;
            _mono.Advance(rtt);
            _device.Advance(TimeSpan.FromSeconds(rtt));
            return _clock.ObserveResponse(HttpDate.Format(serverDate), age, sent, _mono.Seconds);
        }

        [Test]
        public void UntrustedUntilFirstResponseAndFollowsDeviceClock()
        {
            Assert.That(_clock.IsTrusted, Is.False);
            _device.AdvanceDays(30);
            Assert.That(_clock.UtcNow, Is.EqualTo(_device.UtcNow));
        }

        [Test]
        public void AcceptedDateBecomesTrustedAndIgnoresDeviceClockChanges()
        {
            Assert.That(Respond(Server), Is.EqualTo(ServerTimeSample.Accepted));
            Assert.That(_clock.IsTrusted, Is.True);

            // 端末の時計を 30 日進めても、返す時刻は単調時計の経過だけ進む
            _device.AdvanceDays(30);
            _mono.Advance(3600);
            var expected = Server + TimeSpan.FromSeconds(0.5 + 0.1 + 3600);
            Assert.That((_clock.UtcNow - expected).Duration(), Is.LessThan(TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void AnchorsMidpointOfRoundTripWithHalfSecondForTruncation()
        {
            Assert.That(Respond(Server, rtt: 4.0), Is.EqualTo(ServerTimeSample.Accepted));

            // 受信直後: Date + 0.5 + (受信 − 中点) = Date + 0.5 + 2.0
            Assert.That((_clock.UtcNow - (Server + TimeSpan.FromSeconds(2.5))).Duration(), Is.LessThan(TimeSpan.FromMilliseconds(1)));
            Assert.That(_clock.Uncertainty, Is.EqualTo(TimeSpan.FromSeconds(2.5)));
        }

        [Test]
        public void ReportsDeviceSkew()
        {
            _device.Set(Server - TimeSpan.FromHours(5));
            Respond(Server, rtt: 0.0);
            Assert.That(_clock.DeviceSkew.Value.TotalHours, Is.EqualTo(5.0).Within(0.001));
        }

        [Test]
        public void MissingInvalidCachedOrSlowResponsesAreIgnored()
        {
            Assert.That(_clock.ObserveResponse(null, null, 0, 1), Is.EqualTo(ServerTimeSample.MissingDate));
            Assert.That(_clock.ObserveResponse("yesterday", null, 0, 1), Is.EqualTo(ServerTimeSample.InvalidDate));
            Assert.That(Respond(Server, age: "120"), Is.EqualTo(ServerTimeSample.FromCache));
            Assert.That(Respond(Server, rtt: 25.5), Is.EqualTo(ServerTimeSample.RoundTripRejected));
            Assert.That(_clock.ObserveServerTime(Server, 10, 9), Is.EqualTo(ServerTimeSample.RoundTripRejected));
            Assert.That(Respond(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)), Is.EqualTo(ServerTimeSample.Implausible));
            Assert.That(_clock.IsTrusted, Is.False);
            Assert.That(_clock.SamplesSeen, Is.EqualTo(5));
            Assert.That(_clock.SamplesAccepted, Is.EqualTo(0));
        }

        [TestCase("abc")]
        [TestCase("-1")]
        [TestCase("")]
        public void InvalidAgeIsIgnoredPerRfc9111(string age)
        {
            Assert.That(Respond(Server, age: age), Is.EqualTo(ServerTimeSample.Accepted));
        }

        [Test]
        public void AgeListUsesFirstMember()
        {
            Assert.That(Respond(Server, age: "0, 5"), Is.EqualTo(ServerTimeSample.FromCache));
        }

        [Test]
        public void ResyncNeverMovesTimeBackwards()
        {
            Respond(Server);
            _mono.Advance(10);
            var before = _clock.UtcNow;

            // 2 秒古い Date (往復の誤差の範囲) を受け取っても、返す時刻は戻らない
            Respond(Server + TimeSpan.FromSeconds(8), rtt: 0.0);
            Assert.That(_clock.UtcNow, Is.GreaterThanOrEqualTo(before));
            _mono.Advance(5);
            Assert.That(_clock.UtcNow, Is.GreaterThan(before));
        }

        [Test]
        public void MonotonicRestartDropsAnchor()
        {
            Respond(Server);
            Assert.That(_clock.IsTrusted, Is.True);
            _mono.SimulateProcessRestart(0.0);
            Assert.That(_clock.IsTrusted, Is.False);
            Assert.That(_clock.UtcNow, Is.EqualTo(_device.UtcNow));
        }

        [Test]
        public void InvalidateFallsBackToDevice()
        {
            Respond(Server);
            _clock.Invalidate();
            Assert.That(_clock.IsTrusted, Is.False);
            Assert.That(_clock.DeviceSkew, Is.Null);
        }

        [Test]
        public void OffsetClockDefersToInnerTrustUntilAdvanced()
        {
            var demo = new OffsetClock(_clock);
            Assert.That(demo.IsTrusted, Is.False);
            Respond(Server);
            Assert.That(demo.IsTrusted, Is.True);
            _clock.Invalidate();
            demo.AdvanceDays(1);
            Assert.That(demo.IsTrusted, Is.True);

            // 内側が ITrustedClock でなければ v4 と同じく信頼する
            Assert.That(new OffsetClock(new FixedClock(Server)).IsTrusted, Is.True);
        }

        private sealed class FixedClock : IClock
        {
            public FixedClock(DateTimeOffset t)
            {
                UtcNow = t;
            }

            public DateTimeOffset UtcNow { get; }
        }
    }
}
