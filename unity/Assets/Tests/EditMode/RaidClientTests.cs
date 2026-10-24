using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectPenguin.Backend;
using ProjectPenguin.Domain.Raid;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>Unity serialization, imported UI, rescue assets, and client regression checks.</summary>
    public sealed class RaidClientTests
    {
        [Test]
        public async Task ClassroomReloadKeepsLegacyConfigTask()
        {
            var field=typeof(BackendRuntime).GetField("_config",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var previous=field.GetValue(null);
            var legacy=Task.FromResult(new BackendConfig("https://legacy.example/api",JudgeMode.Http,true,true,false));
            field.SetValue(null,legacy);
            try
            {
                await BackendRuntime.ClassroomConfigAsync(true,CancellationToken.None);
                Assert.That(BackendRuntime.ConfigAsync(),Is.SameAs(legacy));
                Assert.That((await BackendRuntime.ConfigAsync()).ApiBaseUrl,Is.EqualTo("https://legacy.example/api"));
            }
            finally { BackendRuntime.Reset(); field.SetValue(null,previous); }
        }

        [Test]
        public void ClassroomJsonExamplesPreserveExclusiveOptionalFields()
        {
            var codec=new ClassroomJsonCodec();
            var personal=codec.Deserialize<ClassroomPersonalState>(System.IO.File.ReadAllText("docs/raid/classroom-examples/personal.json"));
            var progress=codec.Deserialize<ClassroomProgress>(System.IO.File.ReadAllText("docs/raid/classroom-examples/progress.json"));
            Assert.That(personal.pending_answer,Is.Null);
            Assert.That(ClassroomState.Valid(personal,progress),Is.True);
            var answer=codec.Deserialize<ClassroomPersonalState>(System.IO.File.ReadAllText("docs/raid/classroom-examples/pending-answer.json"));
            Assert.That(answer.question,Is.Null);
            Assert.That(ClassroomState.Valid(answer,progress),Is.True);
            var ack=codec.Deserialize<ClassroomAckResult>(System.IO.File.ReadAllText("docs/raid/classroom-examples/ack.json"));
            Assert.That(ack.personal.question,Is.Null);
            Assert.That(ack.personal.pending_answer,Is.Null);
            Assert.That(ClassroomState.Valid(ack.personal,ack.progress),Is.True);
            var save=codec.Deserialize<ClassroomParticipation>("{\"version\":2,\"event_id\":\"demo\",\"pending_next\":null,\"pending_answer\":null}");
            Assert.That(save.pending_next,Is.Null); Assert.That(save.pending_answer,Is.Null);
            var roundTrip=codec.Deserialize<ClassroomParticipation>(codec.Serialize(new ClassroomParticipation {event_id="demo"}));
            Assert.That(roundTrip.pending_next,Is.Null); Assert.That(roundTrip.pending_answer,Is.Null);
        }

        [Test]
        public void ClassroomJsonPreservesPartiallyMalformedObjectsAndRuntimeUrlResolution()
        {
            var p=new ClassroomJsonCodec().Deserialize<ClassroomPersonalState>("{\"phase\":\"idle\",\"question\":{\"attempt_seq\":2},\"pending_answer\":null}");
            Assert.That(p.question,Is.Not.Null);
            Assert.That(BackendConfig.Resolve("/api","https://example.test/?classroom=demo"),Is.EqualTo("https://example.test/api"));
            Assert.That(BackendConfig.Parse("{}","https://example.test/").ApiBaseUrl,Is.Null);
        }
        [Test]
        public void UntrustedApiBaseIsRejected()
        {
            Assert.Throws<ArgumentException>(()=>new HttpRaidClient("http://untrusted.example/api"));
            Assert.Throws<ArgumentException>(()=>new HttpRaidClient("https://user:password@example.com/api"));
            Assert.Throws<ArgumentException>(()=>new HttpRaidClient("https://example.test/api?redirect=elsewhere"));
        }
        [Test]
        public void JsonUtilityReadsThePublicContractWithoutAnAnswerKey()
        {
            var json=System.IO.File.ReadAllText("docs/raid/examples/personal.json");
            var state=ReadRaidJson<RaidPersonalState>(json);
            Assert.That(state.event_id,Is.EqualTo("demo"));
            Assert.That(state.question.options.Length,Is.EqualTo(3));
            Assert.That(state.pending_answer,Is.Null);
        }

        [Test]
        public void RaidJsonPreservesAnExplanationAndClearsTheEmptyQuestion()
        {
            var state=ReadRaidJson<RaidPersonalState>("{\"schema_version\":1,\"event_id\":\"demo\",\"content_version\":\"1\",\"answered_count\":1,\"question\":null,\"pending_answer\":{\"answer_id\":\"00000000000000000000000000000001\",\"question_id\":\"lights\",\"correct\":true,\"explanation\":\"照明を消そう\",\"points_pending\":2}}");
            Assert.That(state.question,Is.Null);
            Assert.That(state.pending_answer.answer_id,Is.EqualTo("00000000000000000000000000000001"));
        }

        [Test]
        public void RaidJsonClearsCompletedQuestionsAndSavedEmptyRequests()
        {
            var participant=ReadRaidJson<RaidParticipant>("{\"token\":\"test\",\"personal\":{\"completed\":true,\"answered_count\":3,\"question\":null,\"pending_answer\":null}}");
            Assert.That(participant.personal.question,Is.Null);
            Assert.That(participant.personal.pending_answer,Is.Null);
            var save=ReadRaidJson<RaidParticipation>("{\"version\":1,\"event_id\":\"demo\",\"pending_request\":null}");
            Assert.That(save.pending_request,Is.Null);
        }

        [Test]
        public void RaidJsonDoesNotDiscardPartiallyInvalidServerData()
        {
            var state=ReadRaidJson<RaidPersonalState>("{\"question\":{\"prompt\":\"invalid\"},\"pending_answer\":{\"points_pending\":2}}");
            Assert.That(state.question,Is.Not.Null);
            Assert.That(state.pending_answer,Is.Not.Null);
        }

        private static T ReadRaidJson<T>(string json)
        {
            return RaidJson.Deserialize<T>(json);
        }
        [Test]
        public async Task RepeatedConfirmationAddsOnlyOneContribution()
        {
            var client=new MockRaidClient(); var ct=CancellationToken.None;
            var participant=await client.JoinAsync("demo","editor-test",ct);
            var answer=await client.AnswerAsync("demo",participant.token,new RaidAnswerRequest {
                request_id="00000000000000000000000000000002", question_id="lights", content_version="1",option_id="off" },ct);
            await client.AcknowledgeAsync("demo",participant.token,answer.answer_id,ct);
            await client.AcknowledgeAsync("demo",participant.token,answer.answer_id,ct);
            Assert.That((await client.GetEventAsync("demo",ct)).total_points,Is.EqualTo(2));
        }
        [Test]
        public void RaidControlsAreImportedByTheAppTemplate()
        {
            var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/App/App.uxml");
            var root=tree.Instantiate().Q("raidScreen");
            Assert.That(root,Is.Not.Null);
            Assert.That(root.Q<Button>("raidAction"),Is.Not.Null);
            Assert.That(root.Q<Button>("raidClose"),Is.Not.Null);
            Assert.That(root.Q<ProgressBar>("raidProgress"),Is.Not.Null);
            Assert.That(root.Q<Image>("raidRescueView"),Is.Not.Null);
        }

        [Test]
        public void RescueStageHasFourRegisteredBridgeSectionsAndAnIsolatedCamera()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProductionIsland/Raid/Resources/RaidRescueStage.prefab");
            Assert.That(prefab,Is.Not.Null);
            var presenter=prefab.GetComponent<ProjectPenguin.Presentation.World.RaidRescuePresenter>();
            Assert.That(presenter,Is.Not.Null);
            var serialized=new SerializedObject(presenter);
            var stages=serialized.FindProperty("_rescuedStages");
            Assert.That(stages.arraySize,Is.EqualTo(4));
            for(var i=0;i<4;i++) Assert.That(stages.GetArrayElementAtIndex(i).objectReferenceValue,Is.Not.Null);
            var camera=prefab.GetComponentInChildren<Camera>(true);
            Assert.That(camera,Is.Not.Null);
            Assert.That(camera.enabled,Is.False);
            Assert.That(camera.cullingMask,Is.EqualTo(1<<30));
        }

        [Test]
        public void RescueStageFollowsProgressAndIgnoresOlderSnapshots()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProductionIsland/Raid/Resources/RaidRescueStage.prefab");
            var instance=UnityEngine.Object.Instantiate(source);
            try
            {
                var presenter=instance.GetComponent<ProjectPenguin.Presentation.World.RaidRescuePresenter>();
                var serialized=new SerializedObject(presenter);
                var stages=serialized.FindProperty("_rescuedStages");
                var section=(GameObject)stages.GetArrayElementAtIndex(0).objectReferenceValue;
                var renderers=section.GetComponentsInChildren<Renderer>(true);
                var bounds=renderers[0].bounds;
                foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.size.x,Is.GreaterThan(1f),"Imported model scale must be preserved");
                for(var step=0;step<=4;step++)
                {
                    presenter.Apply(new RaidProgress {revision=step,total_points=step,target_points=4},false);
                    for(var i=0;i<4;i++) Assert.That(((GameObject)stages.GetArrayElementAtIndex(i).objectReferenceValue).activeSelf,Is.EqualTo(i<step));
                }
                presenter.Apply(new RaidProgress {revision=0,total_points=0,target_points=4},false);
                for(var i=0;i<4;i++) Assert.That(((GameObject)stages.GetArrayElementAtIndex(i).objectReferenceValue).activeSelf,Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}
