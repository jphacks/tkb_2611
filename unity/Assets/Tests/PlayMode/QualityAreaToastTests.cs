using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Presentation.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.PlayMode
{
    public sealed class QualityAreaToastTests
    {
        [UnityTest]
        public IEnumerator AreaDescriptionAppearsOnceAllowsInputAndDisappears()
        {
            var owner = new GameObject("Area description (test)");
            owner.SetActive(false);
            var hud = owner.AddComponent<IslandHudPresenter>();
            hud.enabled = false;
            var layer = new VisualElement();
            typeof(IslandHudPresenter).GetField("_toastLayer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, layer);
            owner.SetActive(true);
            try
            {
                var message = IslandUi.QualityAreaDescription(Item.Salt, false, 1.5);
                hud.ShowToast(message);
                hud.ShowToast(message);
                Assert.That(layer.childCount, Is.EqualTo(1));
                var label = (Label)layer[0];
                Assert.That(label.text, Does.Contain("塩の結晶"));
                Assert.That(label.text, Does.Contain("製塩所"));
                Assert.That(label.pickingMode, Is.EqualTo(PickingMode.Ignore));
                yield return null;
                yield return null;
                Assert.That(label.ClassListContains(UiClasses.Toast.Hidden), Is.False);
                yield return new WaitForSecondsRealtime(3.2f);
                Assert.That(layer.childCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(owner);
            }
        }
    }
}
