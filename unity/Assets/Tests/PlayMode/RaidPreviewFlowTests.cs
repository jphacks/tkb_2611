#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Systems;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class RaidPreviewFlowTests
    {
        [UnityTest]
        public IEnumerator QuizConfirmationBackAndQuickEcoWorkThroughTheActualUi()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/RaidRescuePreview.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var document=UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            var root=document.rootVisualElement;
            var navigator=document.GetComponent<ScreenNavigator>();
            var game=UnityEngine.Object.FindAnyObjectByType<GameStateSystem>();
            yield return WaitFor(()=>navigator.Current==AppScreen.Raid && root.Q<Button>("raidAction").enabledSelf);
            Assert.That(game.Onboarded,Is.False);
            Assert.That(RaidScreenPresenter.DeferOnboarding,Is.True);
            Assert.That(root.Q<Image>("raidRescueView").image,Is.Not.Null);
            Submit(root.Q<Button>("raidAction"));
            for(var question=0;question<3;question++)
            {
                yield return WaitFor(()=>root.Q("raidOptions").childCount==3);
                var before=root.Q<Label>("raidProgressText").text;
                Submit((Button)root.Q("raidOptions")[0]);
                yield return WaitFor(()=>root.Q<Button>("raidAction").text.StartsWith("次へ"));
                Assert.That(root.Q<Label>("raidProgressText").text,Is.EqualTo(before),"Answering alone must not award points");
                Submit(root.Q<Button>("raidAction"));
                yield return null;
                Assert.That(root.Q<Label>("raidProgressText").text,Is.Not.EqualTo(before));
            }
            Assert.That(root.Q<Label>("raidPersonal").text,Does.Contain("3問終了"));
            Submit(root.Q<Button>("raidClose"));
            yield return null;
            Assert.That(navigator.Current,Is.EqualTo(AppScreen.Home));
            Assert.That(RaidScreenPresenter.DeferOnboarding,Is.False);
            Assert.That(root.Q<Button>("onboardingNext").worldBound.height,Is.GreaterThan(0));
            for(var page=0;page<3;page++) Submit(root.Q<Button>("onboardingNext"));
            Submit(root.Q<Button>("onboardingLater"));
            var electricity=game.Island.Get(Resource.Electricity);
            Submit(root.Q<Button>("quickEcoButton"));
            Submit(root.Q("modalBody").Q<Button>());
            Submit(root.Q<Button>("modalCancel"));
            Assert.That(game.Island.Get(Resource.Electricity),Is.EqualTo(electricity));
            Submit(root.Q<Button>("quickEcoButton"));
            Submit(root.Q("modalBody").Q<Button>());
            Submit(root.Q<Button>("modalButton"));
            Assert.That(game.Island.Get(Resource.Electricity),Is.EqualTo(electricity+3));
            Assert.That(game.Island.QuickEcoCount(game.EffectiveToday),Is.EqualTo(1));
            Assert.That(root.Q<Label>("modalText").text,Does.Contain("受け取ったよ"));
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            var deadline=Time.realtimeSinceStartup+10;
            while(!condition() && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(condition(),Is.True,"Timed out waiting for the raid UI");
        }

        private static void Submit(Button button)
        {
            Assert.That(button,Is.Not.Null);
            Assert.That(button.enabledInHierarchy,Is.True);
            using(var evt=NavigationSubmitEvent.GetPooled()) { evt.target=button; button.SendEvent(evt); }
        }
    }
}
#endif
