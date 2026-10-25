using NUnit.Framework;
using ProjectPenguin.Presentation.UI.Industrial;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class IndustrialComponentsTests
    {
        [Test]
        public void ChamferPanel_InstantiatesAndRendersWithoutException()
        {
            var panel = new ChamferPanel { Chamfer = 16f };
            panel.style.width = 200;
            panel.style.height = 100;
            Assert.DoesNotThrow(() => panel.MarkDirtyRepaint());
        }

        [Test]
        public void HexMark_InstantiatesAndRendersWithoutException()
        {
            var mark = new HexMark();
            mark.style.width = 40;
            mark.style.height = 46;
            Assert.DoesNotThrow(() => mark.MarkDirtyRepaint());
        }

        [Test]
        public void FlowBars_SetData_RendersWithoutException()
        {
            var bars = new FlowBars();
            bars.style.width = 300;
            bars.style.height = 100;

            var made = new[] { 5, 10, 15, 20, 25 };
            var used = new[] { 2, 4, 18, 20, 30 };
            Assert.DoesNotThrow(() => bars.SetData(made, used));
            Assert.DoesNotThrow(() => bars.SetData(null, null));
        }

        [Test]
        public void TankGauge_SetLevel_RendersWithoutException()
        {
            var gauge = new TankGauge();
            gauge.style.width = 960;
            gauge.style.height = 440;

            Assert.DoesNotThrow(() => gauge.SetLevel(0.64, true, true));
            Assert.DoesNotThrow(() => gauge.SetLevel(0.0, false, false));
            Assert.DoesNotThrow(() => gauge.SetLevel(1.5, true, false));
        }
    }
}
