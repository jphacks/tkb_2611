using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using ProjectPenguin.Data.Persistence;

namespace ProjectPenguin.Tests.EditMode.Data
{
    /// <summary>v5：セーブデータの原子的な保存・破損検知・バックアップからの復元・版の移行 (19b-clock-save.md)。</summary>
    public class SaveStoreTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pp-savestore-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private SaveStore Store(int version = 1, SaveMigrations migrations = null, bool useReplace = true)
        {
            return new SaveStore(_dir, "game.json", version, migrations, useReplace);
        }

        [Test]
        public void NotFoundOnFirstLaunch()
        {
            var r = Store().Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.NotFound));
            Assert.That(r.HasPayload, Is.False);
        }

        [Test]
        public void SaveThenLoadRoundTripsAndCreatesDirectory()
        {
            var s = Store();
            s.Save("{\"a\":1}");
            Assert.That(File.Exists(s.MainPath), Is.True);
            Assert.That(File.Exists(s.TempPath), Is.False);
            Assert.That(File.Exists(s.BackupPath), Is.False);
            var r = Store().Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(r.Payload, Is.EqualTo("{\"a\":1}"));
            Assert.That(r.StoredVersion, Is.EqualTo(1));
            Assert.That(r.Migrated, Is.False);
        }

        [Test]
        public void FileIsOneJsonDocumentWithChecksum()
        {
            var s = Store();
            s.Save("{\"ice\":100.5,\"name\":\"アデリー\"}");
            var text = File.ReadAllText(s.MainPath, Encoding.UTF8);
            StringAssert.StartsWith("{\"format\":\"ProjectPenguin.Save\",\"envelope\":1,\"schemaVersion\":1,\"sha256\":\"", text);
            StringAssert.EndsWith(",\"payload\":{\"ice\":100.5,\"name\":\"アデリー\"}}\n", text);
#if !UNITY_5_3_OR_NEWER
            // dotnet-harness では、ファイル全体が JSON として読めることも確かめる (Unity には System.Text.Json が無い)
            using (var doc = System.Text.Json.JsonDocument.Parse(text))
            {
                var root = doc.RootElement;
                Assert.That(root.GetProperty("format").GetString(), Is.EqualTo("ProjectPenguin.Save"));
                Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
                Assert.That(root.GetProperty("sha256").GetString(), Has.Length.EqualTo(64));
                Assert.That(root.GetProperty("payload").GetProperty("ice").GetDouble(), Is.EqualTo(100.5));
            }
#endif

            // BOM なし
            var bytes = File.ReadAllBytes(s.MainPath);
            Assert.That(bytes[0], Is.EqualTo((byte)'{'));
        }

        [Test]
        public void SecondSaveKeepsPreviousAsBackup()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2}");
            Assert.That(SaveStore.TryDecode(File.ReadAllText(s.BackupPath), out var bak, out _, out _), Is.True);
            Assert.That(bak, Is.EqualTo("{\"n\":1}"));
            s.Save("{\"n\":3}");
            SaveStore.TryDecode(File.ReadAllText(s.BackupPath), out bak, out _, out _);
            Assert.That(bak, Is.EqualTo("{\"n\":2}"), "backup is exactly one generation old");
            Assert.That(Store().Load().Payload, Is.EqualTo("{\"n\":3}"));
        }

        [Test]
        public void FlippedByteInMainRestoresFromBackup()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2}");
            var bytes = File.ReadAllBytes(s.MainPath);
            var i = Array.LastIndexOf(bytes, (byte)'2');
            bytes[i] = (byte)'9';
            File.WriteAllBytes(s.MainPath, bytes);

            var r = Store().Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            Assert.That(r.Payload, Is.EqualTo("{\"n\":1}"));
            StringAssert.Contains("checksum mismatch", r.Detail);
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(80)]
        [TestCase(150)]
        public void TruncatedMainRestoresFromBackup(int keepBytes)
        {
            var s = Store();
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2,\"pad\":\"" + new string('x', 200) + "\"}");
            var bytes = File.ReadAllBytes(s.MainPath);
            var cut = new byte[Math.Min(keepBytes, bytes.Length)];
            Array.Copy(bytes, cut, cut.Length);
            File.WriteAllBytes(s.MainPath, cut);

            var r = Store().Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            Assert.That(r.Payload, Is.EqualTo("{\"n\":1}"));
        }

        [Test]
        public void TamperedVersionNumberIsDetected()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            var text = File.ReadAllText(s.MainPath).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");
            File.WriteAllText(s.MainPath, text);
            Assert.That(Store(version: 2).Load().Status, Is.EqualTo(SaveLoadStatus.Corrupt));
        }

        [Test]
        public void BothCorruptReportsCorruptAndQuarantineAllowsFreshStart()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2}");
            File.WriteAllText(s.MainPath, "not json");
            File.WriteAllBytes(s.BackupPath, new byte[] { 0xff, 0xfe, 0x00 });

            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(r.HasPayload, Is.False);

            s.QuarantineAll();
            Assert.That(File.Exists(s.MainPath + ".corrupt"), Is.True);
            Assert.That(File.Exists(s.BackupPath + ".corrupt"), Is.True);
            Assert.That(s.Load().Status, Is.EqualTo(SaveLoadStatus.NotFound));
            s.Save("{\"n\":0}");
            Assert.That(s.Load().Payload, Is.EqualTo("{\"n\":0}"));
        }

        [Test]
        public void SavingOverCorruptMainDoesNotDestroyGoodBackup()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2}");
            File.WriteAllText(s.MainPath, "garbage");

            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            s.Save("{\"n\":3}");

            SaveStore.TryDecode(File.ReadAllText(s.BackupPath), out var bak, out _, out _);
            Assert.That(bak, Is.EqualTo("{\"n\":1}"), "corrupt main must not become the backup");
            Assert.That(s.Load().Payload, Is.EqualTo("{\"n\":3}"));
        }

        [Test]
        public void LeftoverPartialTempIsIgnoredAndOverwritten()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            File.WriteAllText(s.TempPath, "{\"format\":\"ProjectPenguin.Save\",\"env");
            Assert.That(s.Load().Payload, Is.EqualTo("{\"n\":1}"));
            s.Save("{\"n\":2}");
            Assert.That(File.Exists(s.TempPath), Is.False);
            Assert.That(s.Load().Payload, Is.EqualTo("{\"n\":2}"));
        }

        [Test]
        public void CompleteTempIsUsedWhenMainAndBackupAreMissing()
        {
            var s = Store();
            Directory.CreateDirectory(_dir);
            File.WriteAllText(s.TempPath, SaveStore.Encode("{\"n\":7}", 1), new UTF8Encoding(false));
            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromTemp));
            Assert.That(r.Payload, Is.EqualTo("{\"n\":7}"));
        }

        [Test]
        public void StoppedBetweenTheTwoRenamesOfMonoReplacePrefersNewerTemp()
        {
            // Mono の File.Replace: rename(本体→.bak) の後、rename(.tmp→本体) の前に止まった状態
            var s = Store();
            s.Save("{\"n\":1}");
            File.Move(s.MainPath, s.BackupPath);
            File.WriteAllText(s.TempPath, SaveStore.Encode("{\"n\":2}", 1), new UTF8Encoding(false));

            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromTemp));
            Assert.That(r.Payload, Is.EqualTo("{\"n\":2}"));

            s.Save(r.Payload);
            Assert.That(s.Load().Status, Is.EqualTo(SaveLoadStatus.Loaded));
        }

        [Test]
        public void PartialTempWithMissingMainFallsBackToBackup()
        {
            var s = Store();
            s.Save("{\"n\":1}");
            File.Move(s.MainPath, s.BackupPath);
            File.WriteAllText(s.TempPath, "{\"format\":\"ProjectPenguin.Save\",\"envelope\":1,\"sche");
            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            Assert.That(r.Payload, Is.EqualTo("{\"n\":1}"));
        }

        [Test]
        public void FallbackWithoutFileReplaceKeepsOneBackupGeneration()
        {
            var s = Store(useReplace: false);
            s.Save("{\"n\":1}");
            s.Save("{\"n\":2}");
            Assert.That(s.UsedFallbackOnLastSave, Is.True);
            SaveStore.TryDecode(File.ReadAllText(s.BackupPath), out var bak, out _, out _);
            Assert.That(bak, Is.EqualTo("{\"n\":1}"));
            Assert.That(s.Load().Payload, Is.EqualTo("{\"n\":2}"));
        }

        [Test]
        public void ValidatorRejectingMainFallsBackToBackup()
        {
            var s = Store();
            s.Save("{\"ok\":true}");
            s.Save("{\"ok\":false}");
            var r = s.Load(json => json.Contains("true"));
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.RestoredFromBackup));
            var r2 = s.Load(json => throw new FormatException());
            Assert.That(r2.Status, Is.EqualTo(SaveLoadStatus.Corrupt));
        }

        [Test]
        public void OlderVersionIsMigratedStepByStep()
        {
            Store(version: 1).Save("{\"v\":1}");
            var migrations = new SaveMigrations()
                .Register(1, j => j.Replace("\"v\":1", "\"v\":2"))
                .Register(2, j => j.Replace("\"v\":2", "\"v\":3,\"added\":0"));
            var s = Store(version: 3, migrations: migrations);
            var r = s.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(r.Migrated, Is.True);
            Assert.That(r.StoredVersion, Is.EqualTo(1));
            Assert.That(r.Payload, Is.EqualTo("{\"v\":3,\"added\":0}"));

            s.Save(r.Payload);
            var again = s.Load();
            Assert.That(again.StoredVersion, Is.EqualTo(3));
            Assert.That(again.Migrated, Is.False);
        }

        [Test]
        public void MissingMigrationStepFails()
        {
            Store(version: 1).Save("{}");
            var r = Store(version: 3, migrations: new SaveMigrations().Register(1, j => j)).Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.MigrationFailed));
            StringAssert.Contains("version 2", r.Detail);
        }

        [Test]
        public void NewerFileIsNotOverwrittenByOlderApp()
        {
            Store(version: 2).Save("{\"v\":2}");
            var old = Store(version: 1);
            var r = old.Load();
            Assert.That(r.Status, Is.EqualTo(SaveLoadStatus.TooNew));
            Assert.That(r.StoredVersion, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => old.Save("{\"v\":1}"));
            Assert.That(Store(version: 2).Load().Payload, Is.EqualTo("{\"v\":2}"));
        }

        [Test]
        public void ChecksumIsStableLowercaseHex()
        {
            var a = SaveStore.Checksum("{}", 1);
            Assert.That(a, Is.EqualTo(SaveStore.Checksum("{}", 1)));
            Assert.That(a, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(a, Is.Not.EqualTo(SaveStore.Checksum("{}", 2)));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentException>(() => new SaveStore("", "a.json", 1));
            Assert.Throws<ArgumentException>(() => new SaveStore(_dir, "a/b.json", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveStore(_dir, "a.json", 0));
            Assert.Throws<ArgumentNullException>(() => Store().Save(null));
        }
    }
}
