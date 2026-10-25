using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Tests.EditMode
{
    public sealed class VillageTrendTests
    {
        [Test]
        public void TrendRecordsAndExpiresAfter60Minutes()
        {
            var game = new VillageGame(unlimited: false);
            game.SetResources(100, 100, 100);
            game.Place(FacilityKind.Fishery, 0, 0); // 漁場 (魚を生産)

            // 0 分目: 生産を進めて完了させる
            game.AdvanceTo(20000); // 20 秒
            var view0 = game.CreateView();
            Assert.That(view0.Trend10[Item.Fish].Made.Sum(), Is.GreaterThan(0));

            // 70 分経過させる (各ステップで進める)
            game.AdvanceTo(20000 + 70 * 60 * 1000L);

            // 70 分経過後、0 分目の記録は破棄され、直近 60 分のみ残る
            var state = game.Export();
            var currentMin = state.At / 60000;
            Assert.That(state.Trend.All(t => t.Minute >= currentMin - 60), Is.True);
            Assert.That(state.Trend.Any(t => t.Minute == 0), Is.False);
        }

        [Test]
        public void ExportAndRestore_RestoresTrendCorrectly()
        {
            var game = new VillageGame(unlimited: false);
            game.SetResources(100, 100, 100);
            game.Place(FacilityKind.Fishery, 0, 0);
            game.AdvanceTo(30000);

            var state = game.Export();
            Assert.That(state.Trend, Is.Not.Null);

            var restored = VillageGame.Restore(state);
            var view1 = game.CreateView();
            var view2 = restored.CreateView();

            for (int i = 0; i < 10; i++)
            {
                Assert.That(view2.Trend10[Item.Fish].Made[i], Is.EqualTo(view1.Trend10[Item.Fish].Made[i]));
                Assert.That(view2.Trend10[Item.Fish].Used[i], Is.EqualTo(view1.Trend10[Item.Fish].Used[i]));
            }
        }

        [Test]
        public void Restore_NullTrend_DoesNotThrow()
        {
            var game = new VillageGame(unlimited: false);
            game.SetResources(10, 10, 10);
            var state = game.Export();
            state.Trend = null; // 古い JSON の互換性

            var restored = VillageGame.Restore(state);
            var view = restored.CreateView();
            Assert.That(view.Trend10[Item.Fish].Made.Length, Is.EqualTo(10));
        }

        [Test]
        public void Restore_InvalidTrend_ThrowsArgumentException()
        {
            var game = new VillageGame(unlimited: false);
            var state = game.Export();

            // 未来の分番号
            state.Trend.Add(new VillageTrendState { Minute = (state.At / 60000) + 10 });
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));

            // 負の品目値
            state = game.Export();
            state.Trend.Add(new VillageTrendState { Minute = 0, Made = new int[] { -1, 0, 0, 0, 0, 0, 0, 0 } });
            Assert.Throws<ArgumentException>(() => VillageGame.Restore(state));
        }

        [Test]
        public void CreateView_GasReceivedToday_ReturnsCorrectAmount()
        {
            var game = new VillageGame(unlimited: false);
            game.AcceptSteps(1, 1000, 1); // day 1: 1000 steps
            game.AcceptSteps(2, 1500, 2); // day 2: 1500 steps

            var viewDay2 = game.CreateView(today: 2);
            Assert.That(viewDay2.GasReceivedToday, Is.EqualTo(1500.0 * VillageGame.GasPerStep));

            var viewDay1 = game.CreateView(today: 1);
            Assert.That(viewDay1.GasReceivedToday, Is.EqualTo(1000.0 * VillageGame.GasPerStep));
        }
    }
}
