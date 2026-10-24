using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Data.Persistence;
using SQLite;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectPenguin.Tests.EditMode.Data
{
    /// <summary>端末の保存 (<see cref="SqliteGameStore"/>)：往復・追記だけ・全履歴・破損・JSON からの取り込み。</summary>
    public class SqliteGameStoreTests
    {
        private string _dir;

        [Test]
        public void QuickEcoRewardAndClaimsSurviveSqliteRestart()
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(1_770_000_000_000);
            var island = new ProjectPenguin.Domain.Island.IslandGame(null, now);
            var before = island.Get(ProjectPenguin.Domain.Island.Resource.Electricity);
            island.ReceiveQuickEco("switch_off_unused_light", 10);
            var save = Sample();
            save.island = island.ToSave();
            new SqliteGameStore(_dir).Save(save);
            var loaded = new SqliteGameStore(_dir).Load();
            var restored = ProjectPenguin.Domain.Island.IslandGame.FromSave(loaded.island, null, now);
            Assert.That(restored.Get(ProjectPenguin.Domain.Island.Resource.Electricity), Is.EqualTo(before + 3));
            Assert.That(restored.ReceiveQuickEco("switch_off_unused_light", 10), Is.Null);
            Assert.That(restored.ReceiveQuickEco("unplug_unused_device", 10), Is.Not.Null);
        }

        [Test]
        public void LegacyIslandWithoutQuickEcoFieldsStartsUnclaimedInSqlite()
        {
            var save = Sample();
            save.island = JsonUtility.FromJson<ProjectPenguin.Domain.Island.IslandSave>("{\"version\":2}");
            new SqliteGameStore(_dir).Save(save);
            var loaded = new SqliteGameStore(_dir).Load();
            var island = ProjectPenguin.Domain.Island.IslandGame.FromSave(loaded.island, null, DateTimeOffset.UtcNow);
            Assert.That(island.ReceiveQuickEco("switch_off_unused_light", 10), Is.Not.Null);
            Assert.That(island.ReceiveQuickEco("switch_off_unused_light", 10), Is.Null);
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pp-sqlitestore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private static GameSave Sample(string name = "ぺん", int ops = 2, int activity = 3, int trimmed = 0)
        {
            var save = new GameSave
            {
                createdUnixMs = 1_770_000_000_000,
                devOffsetMs = 86_400_000,
                lastOpenDay = 12,
                playerName = name,
                onboarded = true,
                ops = Enumerable.Range(0, ops).Select(i => new GameOp
                {
                    kind = GameOp.Photo, atMs = 1_770_000_000_000 + i * 1000, id = "photo-" + i, printDay = 3 + i, verification = 1, answer = 2, noShopping = i % 2 == 0,
                }).ToArray(),
                activity = Enumerable.Range(trimmed, activity).Select(i => Entry(i)).ToArray(),
                activityTrimmed = trimmed,
            };
            return save;
        }

        private static ActivityEntry Entry(int i)
        {
            return new ActivityEntry { atMs = 1000 + i, day = i, kind = ActivityEntry.Photo, category = 2, label = "写真 " + i, electricity = i, water = 1.5, gas = 0, allMighty = 10 };
        }

        private int Rows(string table)
        {
            using (var db = new SQLiteConnection(Path.Combine(_dir, SqliteGameStore.FileName), SQLiteOpenFlags.ReadOnly))
            {
                return db.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table);
            }
        }

        [Test]
        public void FirstLaunchHasNoSave()
        {
            var store = new SqliteGameStore(_dir);
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.NotFound));
        }

        [Test]
        public void SaveRoundTripsEveryField()
        {
            var store = new SqliteGameStore(_dir);
            store.Save(Sample());

            var again = new SqliteGameStore(_dir);
            var loaded = again.Load();
            Assert.That(again.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(loaded.playerName, Is.EqualTo("ぺん"));
            Assert.That(loaded.onboarded, Is.True);
            Assert.That(loaded.createdUnixMs, Is.EqualTo(1_770_000_000_000));
            Assert.That(loaded.devOffsetMs, Is.EqualTo(86_400_000));
            Assert.That(loaded.lastOpenDay, Is.EqualTo(12));
            Assert.That(loaded.ops.Length, Is.EqualTo(2));
            Assert.That(loaded.ops[1].id, Is.EqualTo("photo-1"));
            Assert.That(loaded.ops[1].printDay, Is.EqualTo(4));
            Assert.That(loaded.ops[0].noShopping, Is.True);
            Assert.That(loaded.activity.Length, Is.EqualTo(3));
            Assert.That(loaded.activity[2].label, Is.EqualTo("写真 2"));
            Assert.That(loaded.activity[1].water, Is.EqualTo(1.5));
            Assert.That(loaded.activityTrimmed, Is.EqualTo(0));
        }

        [Test]
        public void SavingAgainOnlyAppendsOpsAndActivity()
        {
            var store = new SqliteGameStore(_dir);
            store.Save(Sample(ops: 2, activity: 3));
            store.Save(Sample(ops: 5, activity: 4));
            store.Save(Sample(ops: 5, activity: 4));

            Assert.That(Rows("ops"), Is.EqualTo(5));
            Assert.That(Rows("activity"), Is.EqualTo(4));
            Assert.That(new SqliteGameStore(_dir).Load().ops.Select(o => o.id), Is.EqualTo(new[] { "photo-0", "photo-1", "photo-2", "photo-3", "photo-4" }));
        }

        [Test]
        public void ActivityBeyondTheWindowStaysInTheDatabase()
        {
            var store = new SqliteGameStore(_dir);
            // 窓 (200 件) より多い 260 件を、窓の移動 (activityTrimmed) といっしょに少しずつ保存する。
            store.Save(Sample(activity: 200, trimmed: 0));
            store.Save(Sample(activity: 200, trimmed: 60));
            Assert.That(Rows("activity"), Is.EqualTo(260));

            var loaded = new SqliteGameStore(_dir).Load();
            Assert.That(loaded.activity.Length, Is.EqualTo(GameSave.ActivityWindow));
            Assert.That(loaded.activityTrimmed, Is.EqualTo(60));
            Assert.That(loaded.activity[0].label, Is.EqualTo("写真 60"));
            Assert.That(loaded.activity[loaded.activity.Length - 1].label, Is.EqualTo("写真 259"));
        }

        [Test]
        public void ShorterOpsMeansStartedOverAndRewrites()
        {
            var store = new SqliteGameStore(_dir);
            store.Save(Sample(ops: 5, activity: 4));
            store.Save(Sample("さいしょから", ops: 1, activity: 1));

            Assert.That(Rows("ops"), Is.EqualTo(1));
            Assert.That(Rows("activity"), Is.EqualTo(1));
            Assert.That(new SqliteGameStore(_dir).Load().playerName, Is.EqualTo("さいしょから"));
        }

        [Test]
        public void IslandStateIsStoredAsJson()
        {
            var save = Sample();
            save.island = new ProjectPenguin.Domain.Island.IslandSave { version = 1, level = 7, exp = 42, resources = new[] { 1.0, 2.0, 3.0, 4.0 } };
            new SqliteGameStore(_dir).Save(save);

            var loaded = new SqliteGameStore(_dir).Load();
            Assert.That(loaded.island.level, Is.EqualTo(7));
            Assert.That(loaded.island.exp, Is.EqualTo(42));
            Assert.That(loaded.island.resources, Is.EqualTo(new[] { 1.0, 2.0, 3.0, 4.0 }));
        }

        [Test]
        public void UnreadableFileIsSetAsideAndStartsOver()
        {
            var store = new SqliteGameStore(_dir);
            File.WriteAllText(store.FilePath, "これは SQLite のファイルではない。" + new string('x', 200));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("保存を読めませんでした"));
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(File.Exists(store.FilePath + ".corrupt"), Is.True);

            // 退避したあとは、新しく保存できる。
            store.Save(Sample("あたらしい"));
            Assert.That(new SqliteGameStore(_dir).Load().playerName, Is.EqualTo("あたらしい"));
        }

        [Test]
        public void NewerSchemaIsNotOverwritten()
        {
            var store = new SqliteGameStore(_dir);
            store.Save(Sample());
            using (var db = new SQLiteConnection(store.FilePath))
            {
                db.Execute("PRAGMA user_version = " + (SqliteGameStore.SchemaVersion + 1));
            }

            var newer = new SqliteGameStore(_dir);
            Assert.That(newer.Load(), Is.Null);
            Assert.That(newer.LastLoadStatus, Is.EqualTo(SaveLoadStatus.TooNew));
            Assert.Throws<InvalidOperationException>(() => newer.Save(Sample()));
            Assert.That(File.Exists(store.FilePath + ".corrupt"), Is.False);
        }

        [Test]
        public void ImportsTheOldJsonSaveOnceAndSetsItAside()
        {
            var json = new JsonGameStore(_dir);
            json.Save(Sample("むかし", ops: 3, activity: 2));
            Assert.That(File.Exists(json.FilePath), Is.True);

            var store = new SqliteGameStore(_dir);
            var loaded = store.Load();
            Assert.That(loaded.playerName, Is.EqualTo("むかし"));
            Assert.That(loaded.ops.Length, Is.EqualTo(3));
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(File.Exists(json.FilePath), Is.False);
            Assert.That(File.Exists(json.FilePath + ".imported"), Is.True);
            Assert.That(Rows("ops"), Is.EqualTo(3));

            // 次の起動は DB から読む。
            Assert.That(new SqliteGameStore(_dir).Load().playerName, Is.EqualTo("むかし"));
        }

        [Test]
        public void ImportsAPlainJsonSaveWrittenBeforeTheEnvelope()
        {
            File.WriteAllText(new JsonGameStore(_dir).FilePath, JsonUtility.ToJson(Sample("はじめの版")));

            var loaded = new SqliteGameStore(_dir).Load();
            Assert.That(loaded.playerName, Is.EqualTo("はじめの版"));
            Assert.That(Rows("ops"), Is.EqualTo(2));
        }

        [Test]
        public void DeleteRemovesTheDatabase()
        {
            var store = new SqliteGameStore(_dir);
            store.Save(Sample());
            store.Delete();
            Assert.That(Directory.GetFiles(_dir), Is.Empty);
            Assert.That(store.Load(), Is.Null);
        }
    }
}
