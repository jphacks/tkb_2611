#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using ProjectPenguin.Domain.Raid;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Systems;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class ClassroomRaidPreviewFlowTests
    {
        [UnityTest]
        public IEnumerator StandaloneUiSupportsUnlimitedQuizSuccessInFlightAndRestart()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ClassroomQuizRescueRaidPreview.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var presenter=UnityEngine.Object.FindAnyObjectByType<ClassroomRaidPreview>();
            var document=presenter.GetComponent<UIDocument>();
            Assert.That(document.panelSettings,Is.Not.Null,"Standalone scene must serialize its panel settings");
            var root=document.rootVisualElement;
            yield return WaitFor(()=>root.Q<Button>("classroomStart")!=null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameStateSystem>(),Is.Null,"No island save system");
            Assert.That(root.Q<Image>("classroomRescueView").image,Is.Not.Null);
            root.Q<IntegerField>("classroomTarget").value=40;
            Submit(root.Q<Button>("classroomStart"));
            for(var i=0;i<4;i++)
            {
                Submit(root.Q<Button>("classroomNext"));
                var before=presenter.Session.TotalPoints;
                Submit(root.Q<Button>("classroomOption0"));
                Assert.That(presenter.Session.TotalPoints,Is.EqualTo(before));
                var id=presenter.Session.PendingAnswer.Id;
                Submit(root.Q<Button>("classroomConfirm"));
                Assert.That(presenter.Session.TotalPoints,Is.GreaterThan(before));
                Assert.That(presenter.Session.Confirm(id),Is.Zero);
            }
            Assert.That(presenter.Session.AnsweredCount,Is.EqualTo(4));
            Submit(root.Q<Button>("classroomNext"));
            Submit(root.Q<Button>("classroomWatch"));
            Assert.That(root.Q("classroomOptions").parent.resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            root.Q<Foldout>("classroomTools").value=true;
            while(presenter.Session.Phase==ClassroomRaidPhase.Running) Submit(root.Q<Button>("classroomSimulateFive"));
            Assert.That(presenter.Session.TotalPoints,Is.EqualTo(40));
            Submit(root.Q<Button>("classroomResume"));
            Submit(root.Q<Button>("classroomOption0"));
            Assert.That(root.Q<Label>("classroomExplanation").text,Is.Not.Empty);
            Submit(root.Q<Button>("classroomConfirm"));
            Assert.That(root.Q<Button>("classroomNext").style.display.value,Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<Label>("classroomStatus").text,Does.Contain("救助成功"));
            Submit(root.Q<Button>("classroomVenue"));
            Assert.That(root.Q<Label>("classroomPersonal").parent.style.display.value,Is.EqualTo(DisplayStyle.None));
            Submit(root.Q<Button>("classroomReset")); Submit(root.Q<Button>("classroomResetYes"));
            Assert.That(presenter.Session.TotalPoints,Is.Zero); Assert.That(presenter.Session.AnsweredCount,Is.Zero);
            Assert.That(presenter.Session.Phase,Is.EqualTo(ClassroomRaidPhase.Waiting));
            Submit(root.Q<Button>("classroomParticipant")); Submit(root.Q<Button>("classroomStart"));
            Assert.That(presenter.Session.Phase,Is.EqualTo(ClassroomRaidPhase.Running));
        }
        private static IEnumerator WaitFor(Func<bool> condition)
        {
            var until=Time.realtimeSinceStartup+10;
            while(!condition()&&Time.realtimeSinceStartup<until) yield return null;
            Assert.That(condition(),Is.True);
        }
        private static void Submit(Button button)
        {
            Assert.That(button,Is.Not.Null); Assert.That(button.enabledInHierarchy,Is.True);
            using(var evt=NavigationSubmitEvent.GetPooled()) {evt.target=button;button.SendEvent(evt);}
        }
    }
}
#endif
