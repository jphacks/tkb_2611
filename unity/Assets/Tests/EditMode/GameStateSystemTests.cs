using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>保存と読み込み: 操作の記録を再生して、氷と島が同じ状態に戻ること。</summary>
    public sealed class GameStateSystemTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(9));

        private GameObject _go;

        private sealed class FailingQuickEcoStore : IGameStore
        {
            private readonly MemoryGameStore _inner = new MemoryGameStore();
            public bool Fail;
            public ProjectPenguin.Data.Persistence.SaveLoadStatus LastLoadStatus => _inner.LastLoadStatus;
            public GameSave Load() => _inner.Load();
            public void Delete() => _inner.Delete();
            public void Save(GameSave save)
            {
                if (Fail) throw new System.IO.IOException("test write failure");
                _inner.Save(save);
            }
        }

        [Test]
        public void QuickEcoFailedSaveRestoresRewardClaimsAndActivityAndAllowsRetry()
        {
            var store = new FailingQuickEcoStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);
            var electricity = a.Island.Get(Resource.Electricity);
            store.Fail = true;
            var warning = new System.Text.RegularExpressions.Regex("GameStateSystem: 保存に失敗しました");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, warning);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, warning);
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", a.EffectiveToday, 3, out var granted), Is.False);
            Assert.That(granted, Is.Null);
            Assert.That(a.Island.Get(Resource.Electricity), Is.EqualTo(electricity));
            Assert.That(a.Island.QuickEcoCount(a.EffectiveToday), Is.Zero);
            Assert.That(a.Activity.Count, Is.Zero);
            Assert.That(a.QuickEcoError, Does.Contain("保存"));
            store.Fail = false;
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", a.EffectiveToday, 3, out _), Is.True);
            var b = New(store, clock);
            Assert.That(b.Island.Get(Resource.Electricity), Is.EqualTo(electricity + 3));
            Assert.That(b.Island.QuickEcoCount(b.EffectiveToday), Is.EqualTo(1));
            Assert.That(b.Activity.Count, Is.EqualTo(1));
        }

        [Test]
        public void QuickEcoRejectsRollbackAfterRestartEvenIfNothingClaimedOnLatestDay()
        {
            var store = new MemoryGameStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);
            a.Refresh();
            clock.Advance(TimeSpan.FromDays(1));
            a.Refresh();
            clock.Set(Start);
            var b = New(store, clock);
            Assert.That(b.TryReceiveQuickEco("switch_off_unused_light", b.EffectiveToday, 3, out _), Is.False);
        }

        [Test]
        public void QuickEcoSavesOnceWithoutRecordingPhotoOrIce()
        {
            var store = new MemoryGameStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);
            var ice = a.Session.Ice;
            var electricity = a.Island.Get(Resource.Electricity);
            var day = a.EffectiveToday;
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", day, 3, out var got), Is.True);
            Assert.That(got[Resource.Electricity], Is.EqualTo(3));
            Assert.That(a.Session.Ice, Is.EqualTo(ice));
            Assert.That(a.Session.TotalRecordDays, Is.Zero);
            Assert.That(a.Island.PhotosToday, Is.Zero);
            Assert.That(a.Activity.Count, Is.EqualTo(1));
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", day, 3, out _), Is.False);
            var b = New(store, clock);
            Assert.That(b.Island.Get(Resource.Electricity), Is.EqualTo(electricity + 3));
            Assert.That(b.TryReceiveQuickEco("switch_off_unused_light", day, 3, out _), Is.False);
        }

        [Test]
        public void QuickEcoRejectsStaleDayAndRewardAndClockRollback()
        {
            var clock = new ManualClock(Start);
            var a = New(new MemoryGameStore(), clock);
            var day = a.EffectiveToday;
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", day, 30, out _), Is.False);
            clock.Advance(TimeSpan.FromDays(1));
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", day, 3, out _), Is.False);
            Assert.That(a.TryReceiveQuickEco("switch_off_unused_light", a.EffectiveToday, 3, out _), Is.True);
            clock.Set(Start);
            Assert.That(a.TryReceiveQuickEco("unplug_unused_device", a.EffectiveToday, 3, out _), Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        private GameStateSystem New(IGameStore store, IClock clock)
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }

            _go = new GameObject("GameState (test)");
            var system = _go.AddComponent<GameStateSystem>();
            system.Initialize(store, clock);
            return system;
        }

        [Test]
        public void 保存した操作を再生すると氷と島が同じになる()
        {
            var store = new MemoryGameStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);

            a.CompleteOnboarding("ペン");
            a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.SaveWater, "節水", "", 0.9));
            var mission = MissionCatalog.DailyFor(a.Today)[0];
            Assert.That(a.SelectMission(mission), Is.True);
            Assert.That(a.CheckIn(mission, CheckInAnswer.Done), Is.Not.Null);
            Assert.That(a.Build(FacilityKind.Fishery, new Cell(3, 2)), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(a.Expand(new Cell(2, 2)), Is.EqualTo(IslandActionResult.Ok));

            clock.Advance(TimeSpan.FromDays(2));
            a.Refresh();
            a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.Goods, "マイボトル", "", 0.9));
            var ice = a.Session.Ice;
            var records = a.Session.TotalRecordDays;
            var fish = a.Island.Fish;

            var b = New(store, clock);

            Assert.That(b.Onboarded, Is.True);
            Assert.That(b.PlayerName, Is.EqualTo("ペン"));
            Assert.That(b.Session.Ice, Is.EqualTo(ice).Within(1e-9));
            Assert.That(b.Session.TotalRecordDays, Is.EqualTo(records));
            Assert.That(b.Island.Facilities.Select(f => f.Kind), Is.EqualTo(new[] { FacilityKind.Fishery }));
            Assert.That(b.Island.Board.Expansions, Is.EqualTo(new[] { new Cell(2, 2) }));
            Assert.That(b.Island.Fish, Is.EqualTo(fish));
            Assert.That(b.Activity.Count, Is.EqualTo(3), "写真 2 枚とミッション 1 件");
            Assert.That(b.CanReceivePhotoToday, Is.False, "今日の写真はもう受け取った");
        }

        [Test]
        public void 写真は1日1枚だけ資源になり_記録は1回目で付く()
        {
            var a = New(new MemoryGameStore(), new ManualClock(Start));
            var first = a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.SaveElectricity, "", "", 1));
            var second = a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.SaveElectricity, "", "", 1));

            Assert.That(first.NewRecordDay, Is.True);
            Assert.That(first.Granted, Is.Not.Null);
            Assert.That(second.NewRecordDay, Is.False);
            Assert.That(second.Granted, Is.Null);
        }

        [Test]
        public void 時計を前後させても写真の資源は1日1回()
        {
            var clock = new ManualClock(Start);
            var a = New(new MemoryGameStore(), clock);
            Assert.That(a.RecordEcoPhoto(new EcoPhotoVerdict(EcoSubject.EcoBag, "", 1)).Granted, Is.Not.Null);

            clock.Advance(TimeSpan.FromDays(1));
            a.Refresh();
            Assert.That(a.RecordEcoPhoto(new EcoPhotoVerdict(EcoSubject.EcoBag, "", 1)).Granted, Is.Not.Null);

            // 端末の時計を 1 日戻すと、操作は「最後に見た日」のものになり、その日の資源はもう受け取っている。
            clock.Set(Start);
            Assert.That(a.CanReceivePhotoToday, Is.False);
            Assert.That(a.RecordEcoPhoto(new EcoPhotoVerdict(EcoSubject.EcoBag, "", 1)).Granted, Is.Null);
            Assert.That(a.Activity.Count(x => x.kind == ActivityEntry.Photo), Is.EqualTo(2));
        }

        [Test]
        public void 氷室があると休んだ日に溶ける氷が減り_再生しても同じ氷になる()
        {
            // 氷室なしで休んだ氷 (盾 2 枚を使い切って溶ける)。
            var plainClock = new ManualClock(Start);
            var plainIce = Rest(New(new MemoryGameStore(), plainClock), plainClock);

            var store = new MemoryGameStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);
            Assert.That(a.Build(FacilityKind.IceHouse, new Cell(4, 3)), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(a.Session.MeltReduction, Is.EqualTo(a.Island.MeltReduction).Within(1e-9));
            Assert.That(a.Session.MeltReduction, Is.GreaterThan(0));

            // 同じだけ休む。お魚を足して、氷室のごはんを切らさない。
            a.Island.DevAddStock(Item.Fish, 10000);
            var ice = Rest(a, clock);
            Assert.That(ice, Is.GreaterThan(plainIce), "氷室があると溶けにくい");

            var b = New(store, clock);
            Assert.That(b.Session.Ice, Is.EqualTo(ice).Within(1e-9), "操作の記録の再生で同じ氷");
            Assert.That(b.Session.MeltReduction, Is.EqualTo(a.Session.MeltReduction).Within(1e-9));
        }

        /// <summary>1 日ずつ 6 日進め、記録せずに氷を返す。</summary>
        private static double Rest(GameStateSystem system, ManualClock clock)
        {
            for (var i = 0; i < 6; i++)
            {
                clock.Advance(TimeSpan.FromDays(1));
                system.Refresh();
            }

            return system.Session.Ice;
        }

        [Test]
        public void 閉じていた間も島は進み_報告が残る()
        {
            var store = new MemoryGameStore();
            var clock = new ManualClock(Start);
            var a = New(store, clock);
            a.Build(FacilityKind.Fishery, new Cell(3, 2));
            var fish = a.Island.Fish;

            clock.Advance(TimeSpan.FromHours(2));
            var b = New(store, clock);

            // オンボーディングで来たアデリーペンギンが漁場を速くするので、速さを掛けて数える。
            var speed = b.Island.SpeedOf(b.Island.Facilities[0]);
            Assert.That(b.Island.Fish - fish, Is.EqualTo(Math.Floor(2 * 3600 * speed / 4)).Within(1));
            Assert.That(b.TakeOfflineReport(), Is.Not.Null);
        }

        [Test]
        public void データを消すと最初からになる()
        {
            var store = new MemoryGameStore();
            var a = New(store, new ManualClock(Start));
            a.CompleteOnboarding("ペン");
            a.Build(FacilityKind.Fishery, new Cell(3, 2));

            a.ResetAll();

            Assert.That(a.Onboarded, Is.False);
            Assert.That(a.Island.Facilities, Is.Empty);
            Assert.That(store.Load().onboarded, Is.False);
        }

        [Test]
        public void JSONの置き場は書いて読める()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "penguin-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                var store = new JsonGameStore(dir);
                Assert.That(store.Load(), Is.Null);
                store.Save(new GameSave { createdUnixMs = 123, playerName = "ペン" });
                store.Save(new GameSave { createdUnixMs = 456, playerName = "ギン" });
                var loaded = store.Load();
                Assert.That(loaded.createdUnixMs, Is.EqualTo(456));
                Assert.That(loaded.playerName, Is.EqualTo("ギン"));
                store.Delete();
                Assert.That(store.Load(), Is.Null);
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void SQLiteの置き場でも保存した状態に戻る()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "penguin-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            try
            {
                var clock = new ManualClock(Start);
                var a = New(new SqliteGameStore(dir), clock);
                a.CompleteOnboarding("ペン");
                a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.SaveWater, "節水", "", 0.9));
                Assert.That(a.Build(FacilityKind.Fishery, new Cell(3, 2)), Is.EqualTo(IslandActionResult.Ok));
                clock.Advance(TimeSpan.FromDays(2));
                a.Refresh();
                a.RecordEcoPhoto(new EcoPhotoVerdict(EcoCategory.Goods, "マイボトル", "", 0.9));
                var ice = a.Session.Ice;
                var activity = a.Activity.Count;
                Assert.That(activity, Is.GreaterThan(0));

                // 別のシステムが、同じフォルダの DB から始める。
                var b = New(new SqliteGameStore(dir), clock);
                Assert.That(b.PlayerName, Is.EqualTo("ペン"));
                Assert.That(b.Onboarded, Is.True);
                Assert.That(b.Session.Ice, Is.EqualTo(ice).Within(1e-9));
                Assert.That(b.Activity.Count, Is.EqualTo(activity));
                Assert.That(b.Island.Facilities.Count, Is.EqualTo(1));
                Assert.That(b.LoadStatus, Is.EqualTo(ProjectPenguin.Data.Persistence.SaveLoadStatus.Loaded));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }
    }
}
