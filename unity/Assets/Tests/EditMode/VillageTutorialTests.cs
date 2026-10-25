using NUnit.Framework;
using ProjectPenguin.Domain.Village;
using S = ProjectPenguin.Domain.Village.VillageTutorialSignal;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageTutorialTests
    {
        [Test] public void WholeTutorialFollowsTheSevenSteps()
        {
            var t = new VillageTutorial(VillageTutorial.NotStarted);
            Assert.That(t.Active, Is.False);
            t.Start();
            var path = new[] { S.Next, S.IndustrialOpened, S.FacilityOpened, S.PageClosed, S.Next, S.Built, S.ZoomedDetail, S.ZoomedOverview, S.ReportOpened, S.ReportClosed };
            var steps = new[] { 2, 3, 4, 4, 5, 6, 6, 7, 7, VillageTutorial.Done };
            for (var i = 0; i < path.Length; i++)
            {
                Assert.That(t.Handle(path[i]), Is.True, path[i].ToString());
                Assert.That(t.Step, Is.EqualTo(steps[i]), path[i].ToString());
            }

            Assert.That(t.Active, Is.False);
            Assert.That(t.Handle(S.Next), Is.False, "終わったあとは進まない");
        }

        [Test] public void UnrelatedSignalsDoNotAdvance()
        {
            var t = new VillageTutorial(VillageTutorial.OpenWorkshop);
            Assert.That(t.Handle(S.Next), Is.False, "工房モードを開くまで次へでは進まない");
            Assert.That(t.Handle(S.Built), Is.False);
            Assert.That(t.Step, Is.EqualTo(VillageTutorial.OpenWorkshop));

            var flow = new VillageTutorial(VillageTutorial.Flow);
            Assert.That(flow.Handle(S.Next), Is.False, "ページを閉じるまでは次へが効かない");
            Assert.That(flow.Handle(S.PageClosed), Is.True);
            Assert.That(flow.Halfway, Is.True);
            Assert.That(flow.Handle(S.PageClosed), Is.False);

            var zoom = new VillageTutorial(VillageTutorial.Zoom);
            Assert.That(zoom.Handle(S.ZoomedOverview), Is.False, "先に詳細へ寄る");
            Assert.That(zoom.Handle(S.ZoomedDetail), Is.True);
            Assert.That(zoom.Handle(S.ZoomedOverview), Is.True);
            Assert.That(zoom.Step, Is.EqualTo(VillageTutorial.Report));

            var report = new VillageTutorial(VillageTutorial.Report);
            Assert.That(report.Handle(S.ReportClosed), Is.False, "開く前に閉じた合図は数えない");
        }

        [Test] public void BuildStepAcceptsNextWhenBuildingIsNotPossible()
        {
            var t = new VillageTutorial(VillageTutorial.Build);
            Assert.That(t.Handle(S.Next), Is.True);
            Assert.That(t.Step, Is.EqualTo(VillageTutorial.Zoom));
        }

        [Test] public void SkipEndsFromAnyStepAndStartRestarts()
        {
            for (var step = VillageTutorial.Welcome; step <= VillageTutorial.Count; step++)
            {
                var t = new VillageTutorial(step);
                Assert.That(t.Handle(S.Skip), Is.True);
                Assert.That(t.Step, Is.EqualTo(VillageTutorial.Done));
                t.Start();
                Assert.That(t.Step, Is.EqualTo(VillageTutorial.Welcome), "メニューからやり直せる");
                Assert.That(t.Halfway, Is.False);
            }
        }

        [Test] public void InvalidSavedValueIsTreatedAsDone()
        {
            foreach (var bad in new[] { -2, 8, 100, int.MinValue })
            {
                Assert.That(VillageTutorial.IsValid(bad), Is.False);
                Assert.That(new VillageTutorial(bad).Step, Is.EqualTo(VillageTutorial.Done), bad.ToString());
            }

            Assert.That(new VillageTutorial(VillageTutorial.Zoom).Step, Is.EqualTo(VillageTutorial.Zoom), "途中の段から再開できる");
        }
    }
}
