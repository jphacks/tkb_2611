using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class ClassroomRaidSessionTests
    {
        [Test]
        public void CanAnswerBeyondThreeAndEveryRoundHasAllQuestionsInADifferentOrder()
        {
            var session = new ClassroomRaidSession();
            session.Reset(30, 100); session.Start();
            var ids = new string[24];
            for (var i=0; i<24; i++)
            {
                Assert.That(session.BeginQuestion(), Is.True);
                ids[i] = session.CurrentQuestion.Id;
                session.Answer(0);
                Assert.That(session.TotalPoints, Is.EqualTo(session.PersonalPoints));
                session.Confirm(session.PendingAnswer.Id);
            }
            Assert.That(session.AnsweredCount, Is.EqualTo(24));
            Assert.That(ids.Take(12).Distinct().Count(), Is.EqualTo(12));
            Assert.That(ids.Skip(12).OrderBy(x=>x), Is.EqualTo(ids.Take(12).OrderBy(x=>x)));
            Assert.That(ids.Skip(12), Is.Not.EqualTo(ids.Take(12)));
            Assert.That(ids[11], Is.Not.EqualTo(ids[12]));
        }

        [Test]
        public void ExplanationConfirmationAwardsOnceAndOldIdCannotConfirmNextAnswer()
        {
            var s = new ClassroomRaidSession(); s.Reset(30,100); s.Start(); s.BeginQuestion(); s.Answer(0);
            var id=s.PendingAnswer.Id;
            Assert.That(s.TotalPoints, Is.Zero);
            var awarded=s.Confirm(id);
            Assert.That(awarded, Is.InRange(1,2));
            Assert.That(s.Confirm(id), Is.Zero);
            s.BeginQuestion(); s.Answer(0);
            Assert.That(s.Confirm(id), Is.Zero);
            Assert.That(s.PendingAnswer, Is.Not.Null);
            Assert.That(s.TotalPoints, Is.EqualTo(awarded));
        }

        [Test]
        public void GoalClampsPointsAndAllowsInFlightQuestionAndExplanation()
        {
            var s=new ClassroomRaidSession(); s.Reset(2,3); s.Start(); s.BeginQuestion();
            s.AddSimulatedPoints(99);
            Assert.That(s.TotalPoints, Is.EqualTo(3));
            Assert.That(s.Phase, Is.EqualTo(ClassroomRaidPhase.Succeeded));
            Assert.That(s.Answer(0), Is.True);
            Assert.That(s.PendingAnswer.Explanation, Is.Not.Empty);
            Assert.That(s.Confirm(s.PendingAnswer.Id), Is.Zero);
            Assert.That(s.BeginQuestion(), Is.False);
            Assert.That(s.AddSimulatedPoints(5), Is.Zero);
        }

        [Test]
        public void GoalDuringExplanationKeepsItAndPauseCanResumeWithoutLosingQuestion()
        {
            var s=new ClassroomRaidSession(); s.Reset(2,3); s.Start(); s.BeginQuestion();
            var q=s.CurrentQuestion; s.Watch();
            Assert.That(s.IsWatching, Is.True); s.Resume();
            Assert.That(s.CurrentQuestion, Is.SameAs(q));
            s.Answer(1); var answer=s.PendingAnswer; s.AddSimulatedPoints(3);
            Assert.That(s.PendingAnswer, Is.SameAs(answer));
            Assert.That(s.Confirm(answer.Id), Is.Zero);
        }

        [Test]
        public void RestartClearsPersonalAndClassStateAndRejectsStaleConfirmation()
        {
            var s=new ClassroomRaidSession(); s.Reset(2,3); s.Start(); s.BeginQuestion(); s.Answer(0);
            var old=s.PendingAnswer.Id; s.Confirm(old); s.AddSimulatedPoints(9);
            var revision=s.Revision; s.Reset(10,60);
            Assert.That(s.Revision, Is.GreaterThan(revision));
            Assert.That(s.Phase, Is.EqualTo(ClassroomRaidPhase.Waiting));
            Assert.That(s.TotalPoints, Is.Zero); Assert.That(s.PersonalPoints, Is.Zero);
            Assert.That(s.AnsweredCount, Is.Zero); Assert.That(s.CurrentQuestion, Is.Null);
            Assert.That(s.PendingAnswer, Is.Null); Assert.That(s.BeginQuestion(), Is.False);
            s.Start(); s.BeginQuestion(); s.Answer(0);
            Assert.That(s.Confirm(old), Is.Zero);
        }

        [Test]
        public void LastContributionOnlyAwardsRemainingRoom()
        {
            var s=new ClassroomRaidSession(); s.Reset(1,1); s.Start(); s.BeginQuestion();
            Assert.That(s.Start(), Is.False); Assert.That(s.Answer(-1), Is.False);
            s.Answer(0); var expected=s.PendingAnswer.Points;
            Assert.That(s.Answer(1), Is.False);
            Assert.That(s.Confirm(s.PendingAnswer.Id), Is.EqualTo(1));
            Assert.That(s.TotalPoints, Is.EqualTo(1)); Assert.That(s.PersonalPoints, Is.EqualTo(1));
            Assert.That(expected, Is.InRange(1,2)); Assert.That(s.BeginQuestion(), Is.False);
        }
    }
}
