using System;
using System.IO;
using NUnit.Framework;
using ProjectPenguin.Data;
using ProjectPenguin.Data.Persistence;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectPenguin.Tests.EditMode.Data
{
    /// <summary>端末の保存 (<see cref="JsonGameStore"/>)：封筒・バックアップ・初期の素の JSON の読み込み。</summary>
    public class JsonGameStoreTests
    {
        private string _dir;

        [Test]
        public void QuickEcoRewardAndClaimsSurviveJsonRestart()
        {
            var now = DateTimeOffset.FromUnixTimeMilliseconds(1_770_000_000_000);
            var island = new ProjectPenguin.Domain.Island.IslandGame(null, now);
            var before = island.Get(ProjectPenguin.Domain.Island.Resource.Electricity);
            island.ReceiveQuickEco("switch_off_unused_light", 10);
            var save = Sample();
            save.island = island.ToSave();
            new JsonGameStore(_dir).Save(save);
            var loaded = new JsonGameStore(_dir).Load();
            var restored = ProjectPenguin.Domain.Island.IslandGame.FromSave(loaded.island, null, now);
            Assert.That(restored.Get(ProjectPenguin.Domain.Island.Resource.Electricity), Is.EqualTo(before + 3));
            Assert.That(restored.ReceiveQuickEco("switch_off_unused_light", 10), Is.Null);
            Assert.That(restored.ReceiveQuickEco("unplug_unused_device", 10), Is.Not.Null);
        }

        [Test]
        public void LegacyIslandWithoutQuickEcoFieldsStartsUnclaimed()
        {
            var path = new JsonGameStore(_dir).FilePath;
            File.WriteAllText(path, "{\"version\":1,\"createdUnixMs\":1770000000000,\"island\":{\"version\":2}}");
            var loaded = new JsonGameStore(_dir).Load();
            var island = ProjectPenguin.Domain.Island.IslandGame.FromSave(loaded.island, null, DateTimeOffset.UtcNow);
            Assert.That(island.ReceiveQuickEco("switch_off_unused_light", 10), Is.Not.Null);
            Assert.That(island.ReceiveQuickEco("switch_off_unused_light", 10), Is.Null);
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pp-jsongamestore-" + Guid.NewGuid().ToString("N"));
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

        private static GameSave Sample(string name = "ぺん")
        {
            return new GameSave { createdUnixMs = 1_770_000_000_000, playerName = name, onboarded = true, lastOpenDay = 12 };
        }

        [Test]
        public void FirstLaunchHasNoSave()
        {
            var store = new JsonGameStore(_dir);
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.NotFound));
        }

        [Test]
        public void SaveRoundTripsThroughEnvelopeWithChecksum()
        {
            var store = new JsonGameStore(_dir);
            store.Save(Sample());

            StringAssert.Contains(SaveStore.FormatName, File.ReadAllText(store.FilePath));
            var loaded = new JsonGameStore(_dir).Load();
            Assert.That(loaded.playerName, Is.EqualTo("ぺん"));
            Assert.That(loaded.lastOpenDay, Is.EqualTo(12));
        }

        [Test]
        public void ReadsPlainJsonWrittenBeforeTheEnvelopeAndWrapsItOnNextSave()
        {
            var path = new JsonGameStore(_dir).FilePath;
            File.WriteAllText(path, JsonUtility.ToJson(Sample("むかし")));

            var store = new JsonGameStore(_dir);
            var loaded = store.Load();
            Assert.That(loaded.playerName, Is.EqualTo("むかし"));
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Loaded));

            store.Save(loaded);
            StringAssert.Contains(SaveStore.FormatName, File.ReadAllText(path));
        }

        [Test]
        public void DamagedMainFileRestoresFromBackupAndRewritesMain()
        {
            var store = new JsonGameStore(_dir);
            store.Save(Sample("いち"));
            store.Save(Sample("に"));

            var text = File.ReadAllText(store.FilePath);
            File.WriteAllText(store.FilePath, text.Replace("\"に\"", "\"ば\""));

            var again = new JsonGameStore(_dir);
            var loaded = again.Load();
            Assert.That(again.LastLoadStatus, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            Assert.That(loaded.playerName, Is.EqualTo("いち"));
            Assert.That(new JsonGameStore(_dir).Load().playerName, Is.EqualTo("いち"));
        }

        [Test]
        public void UnreadableFileIsSetAsideAndStartsOver()
        {
            var store = new JsonGameStore(_dir);
            File.WriteAllText(store.FilePath, "これは JSON ではない");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("保存を読めませんでした"));
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(File.Exists(store.FilePath + ".corrupt"), Is.True);
        }

        [Test]
        public void DeleteRemovesEveryGeneration()
        {
            var store = new JsonGameStore(_dir);
            store.Save(Sample());
            store.Save(Sample("に"));
            store.Delete();
            Assert.That(Directory.GetFiles(_dir), Is.Empty);
        }
    }
}
