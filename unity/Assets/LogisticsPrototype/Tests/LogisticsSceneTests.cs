#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ProjectPenguin.LogisticsPrototype.Tests
{
    public sealed class LogisticsSceneTests
    {
        [UnityTest]
        public IEnumerator CopiedSceneDeliversAndSupportsReconnectPauseAndReset()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/LogisticsPrototype.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var controller=Object.FindFirstObjectByType<LogisticsPrototypeController>();
            Assert.That(controller,Is.Not.Null,"Copied scene must contain its independent controller");
            foreach(var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(behaviour.GetType().FullName=="ProjectPenguin.Presentation.World.FacilityProductionCycle")
                    Assert.That(behaviour.enabled,Is.False,"Decorative production must not run independently");
            var buttons=Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            Button Find(string name) => System.Array.Find(buttons,b=>b.name==name);
            Assert.That(Find("FacilityCard0"),Is.Not.Null);
            yield return null;
            foreach(var button in buttons)
            {
                var rect=button.GetComponent<RectTransform>();
                var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
                Assert.That(hits.Count,Is.GreaterThan(0)); Assert.That(hits[0].gameObject,Is.EqualTo(button.gameObject));
            }
            Find("線を外す").onClick.Invoke();
            yield return new WaitForSeconds(.2f);
            Assert.That(controller.Chain.Raw,Is.EqualTo(12));
            Find("FacilityCard0").onClick.Invoke(); Find("FacilityCard1").onClick.Invoke();
            Find("FacilityCard1").onClick.Invoke(); Find("FacilityCard2").onClick.Invoke();
            Assert.That(controller.Chain.Connected(0)&&controller.Chain.Connected(1),Is.True);
            var sawDelivery=false; var sawProcessing=false; var deadline=Time.realtimeSinceStartup+15;
            while(controller.Chain.Stored==0&&Time.realtimeSinceStartup<deadline)
            {
                sawDelivery |= controller.Chain.InTransit>0; sawProcessing |= controller.Chain.Processing;
                var c=controller.Chain;
                Assert.That(c.Raw+c.Input+c.Output+c.Stored+c.InTransit+(c.Processing?1:0),Is.EqualTo(12));
                yield return null;
            }
            Assert.That(sawDelivery&&sawProcessing,Is.True); Assert.That(controller.Chain.Stored,Is.GreaterThan(0));
            Assert.That(Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None),Is.Empty);
            Assert.That(GameObject.Find("RectWire0").GetComponent<RectTransform>(),Is.Not.Null);
            Find("一時停止").onClick.Invoke(); var raw=controller.Chain.Raw; var stored=controller.Chain.Stored;
            yield return new WaitForSeconds(.3f);
            Assert.That(controller.Chain.Raw,Is.EqualTo(raw)); Assert.That(controller.Chain.Stored,Is.EqualTo(stored));
            Find("リセット").onClick.Invoke();
            Assert.That(controller.Paused,Is.False); Assert.That(controller.Chain.Raw,Is.EqualTo(12)); Assert.That(controller.Chain.Stored,Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
