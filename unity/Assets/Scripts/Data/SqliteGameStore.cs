using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProjectPenguin.Data.Persistence;
using SQLite;
using UnityEngine;

namespace ProjectPenguin.Data
{
    /// <summary>
    /// 端末の保存を SQLite に置く (<c>persistentDataPath/penguin.db</c>)。写真や購買の明細は入れない (.claude/rules/privacy.md)。
    /// <list type="bullet">
    /// <item><c>meta</c>: 名前・初回起動の時刻などの 1 行ずつの値。</item>
    /// <item><c>ops</c>: 氷の状態機械への操作 (追記だけ)。起動時に順に再生して氷を戻す。</item>
    /// <item><c>island</c>: 島の状態 (構造が複雑なので JSON 1 行)。</item>
    /// <item><c>activity</c>: 資源を受け取った記録 (追記だけ。全件が残り、読み込みでは新しい 200 件を返す)。</item>
    /// </list>
    /// 書き込みは 1 トランザクション (WAL) なので、途中でアプリが落ちても前の状態が残る。
    /// 初期の JSON の保存 (<c>penguin-save.json</c>) があれば、最初の起動で取り込み、元のファイルは <c>.imported</c> に改名して残す。
    /// </summary>
    public sealed class SqliteGameStore : IGameStore
    {
        public const string FileName = "penguin.db";

        /// <summary>スキーマの版 (<c>PRAGMA user_version</c>)。</summary>
        public const int SchemaVersion = 1;

        private const string KeyCreated = "createdUnixMs";
        private const string KeyDevOffset = "devOffsetMs";
        private const string KeyLastOpenDay = "lastOpenDay";
        private const string KeyPlayerName = "playerName";
        private const string KeyOnboarded = "onboarded";
        private const string KeySaveVersion = "saveVersion";

        private readonly string _directory;
        private readonly string _path;
        private bool _blockedByNewerFile;

        public SqliteGameStore(string directory = null)
        {
            _directory = directory ?? Application.persistentDataPath;
            _path = Path.Combine(_directory, FileName);
        }

        public string FilePath => _path;

        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.NotFound;

        public GameSave Load()
        {
            _blockedByNewerFile = false;
            if (!File.Exists(_path))
            {
                return ImportLegacyJson();
            }

            try
            {
                using (var db = Open())
                {
                    var version = db.ExecuteScalar<int>("PRAGMA user_version");
                    if (version > SchemaVersion)
                    {
                        // 新しい版のアプリが書いた DB。古いアプリで上書きしない。
                        _blockedByNewerFile = true;
                        LastLoadStatus = SaveLoadStatus.TooNew;
                        return null;
                    }

                    EnsureSchema(db);
                    var save = Read(db);
                    LastLoadStatus = save == null ? SaveLoadStatus.NotFound : SaveLoadStatus.Loaded;
                    return save;
                }
            }
            catch (SQLiteException e)
            {
                // 読めない DB は消さずに退避し、最初から始める (原因を後で調べられるように)。
                Debug.LogWarning($"{nameof(SqliteGameStore)}: 保存を読めませんでした ({e.Result})。最初から始めます。");
                Quarantine();
                LastLoadStatus = SaveLoadStatus.Corrupt;
                return null;
            }
        }

        public void Save(GameSave save)
        {
            if (save == null)
            {
                return;
            }

            if (_blockedByNewerFile)
            {
                throw new InvalidOperationException("the save database was written by a newer app version; refusing to overwrite it");
            }

            Directory.CreateDirectory(_directory);
            using (var db = Open())
            {
                EnsureSchema(db);
                db.RunInTransaction(() => Write(db, save));
            }
        }

        public void Delete()
        {
            foreach (var path in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            _blockedByNewerFile = false;
            LastLoadStatus = SaveLoadStatus.NotFound;
        }

        // ------------------------------------------------------------------

        private SQLiteConnection Open()
        {
            var db = new SQLiteConnection(_path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
            try
            {
                // PRAGMA の結果を読むので ExecuteScalar を使う (Execute は行が返ると失敗する)。
                db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");
                db.Execute("PRAGMA synchronous=NORMAL");
            }
            catch
            {
                db.Dispose();
                throw;
            }

            return db;
        }

        private static void EnsureSchema(SQLiteConnection db)
        {
            db.CreateTable<MetaRow>();
            db.CreateTable<OpRow>();
            db.CreateTable<IslandRow>();
            db.CreateTable<ActivityRow>();
            if (db.ExecuteScalar<int>("PRAGMA user_version") < SchemaVersion)
            {
                // 将来スキーマを変えるときは、ここで版ごとの変換を順に通す。
                db.Execute("PRAGMA user_version = " + SchemaVersion.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static GameSave Read(SQLiteConnection db)
        {
            var meta = db.Table<MetaRow>().ToDictionary(r => r.Key, r => r.Value);
            if (!meta.TryGetValue(KeyCreated, out var created) || !long.TryParse(created, NumberStyles.Integer, CultureInfo.InvariantCulture, out var createdMs) || createdMs <= 0)
            {
                // 作りかけ (最初の保存の前に落ちた) の DB は、保存が無いのと同じ。
                return null;
            }

            var save = new GameSave
            {
                version = GameSave.CurrentVersion,
                createdUnixMs = createdMs,
                devOffsetMs = ParseLong(meta, KeyDevOffset),
                lastOpenDay = (int)ParseLong(meta, KeyLastOpenDay),
                playerName = meta.TryGetValue(KeyPlayerName, out var name) ? name : "",
                onboarded = ParseLong(meta, KeyOnboarded) != 0,
            };

            save.ops = db.Table<OpRow>().OrderBy(r => r.Seq).ToList().Select(r => new GameOp
            {
                kind = r.Kind, atMs = r.AtMs, id = r.Id, printDay = r.PrintDay, verification = r.Verification, answer = r.Answer, noShopping = r.NoShopping, value = r.Value,
            }).ToArray();

            var island = db.Find<IslandRow>(1);
            save.island = island == null || string.IsNullOrEmpty(island.Json) ? null : JsonUtility.FromJson<ProjectPenguin.Domain.Island.IslandSave>(island.Json);

            var total = db.Table<ActivityRow>().Count();
            var window = db.Table<ActivityRow>().OrderByDescending(r => r.Seq).Take(GameSave.ActivityWindow).ToList();
            window.Reverse();
            save.activity = window.Select(r => new ActivityEntry
            {
                atMs = r.AtMs, day = r.Day, kind = r.Kind, category = r.Category, label = r.Label,
                electricity = r.Electricity, water = r.Water, gas = r.Gas, allMighty = r.AllMighty,
            }).ToArray();
            save.activityTrimmed = Math.Max(0, total - window.Count);
            return save;
        }

        private static void Write(SQLiteConnection db, GameSave save)
        {
            SetMeta(db, KeySaveVersion, save.version.ToString(CultureInfo.InvariantCulture));
            SetMeta(db, KeyCreated, save.createdUnixMs.ToString(CultureInfo.InvariantCulture));
            SetMeta(db, KeyDevOffset, save.devOffsetMs.ToString(CultureInfo.InvariantCulture));
            SetMeta(db, KeyLastOpenDay, save.lastOpenDay.ToString(CultureInfo.InvariantCulture));
            SetMeta(db, KeyPlayerName, save.playerName ?? "");
            SetMeta(db, KeyOnboarded, save.onboarded ? "1" : "0");

            db.InsertOrReplace(new IslandRow { Id = 1, Json = save.island == null ? "" : JsonUtility.ToJson(save.island) });

            // 操作は追記だけ。DB より短くなっていたら (はじめからやり直した) 作り直す。
            var ops = save.ops ?? Array.Empty<GameOp>();
            var storedOps = db.Table<OpRow>().Count();
            if (ops.Length < storedOps)
            {
                db.DeleteAll<OpRow>();
                storedOps = 0;
            }

            for (var i = storedOps; i < ops.Length; i++)
            {
                var o = ops[i];
                db.Insert(new OpRow { Kind = o.kind, AtMs = o.atMs, Id = o.id, PrintDay = o.printDay, Verification = o.verification, Answer = o.answer, NoShopping = o.noShopping, Value = o.value });
            }

            // 受け取りの記録も追記だけ。窓の i 番目は、全履歴の activityTrimmed + i 番目。
            var window = save.activity ?? Array.Empty<ActivityEntry>();
            var storedActivity = db.Table<ActivityRow>().Count();
            if (save.activityTrimmed + window.Length < storedActivity)
            {
                db.DeleteAll<ActivityRow>();
                storedActivity = 0;
            }

            for (var k = Math.Max(storedActivity, save.activityTrimmed); k < save.activityTrimmed + window.Length; k++)
            {
                var a = window[k - save.activityTrimmed];
                db.Insert(new ActivityRow
                {
                    AtMs = a.atMs, Day = a.day, Kind = a.kind, Category = a.category, Label = a.label,
                    Electricity = a.electricity, Water = a.water, Gas = a.gas, AllMighty = a.allMighty,
                });
            }
        }

        private static void SetMeta(SQLiteConnection db, string key, string value)
        {
            db.InsertOrReplace(new MetaRow { Key = key, Value = value });
        }

        private static long ParseLong(Dictionary<string, string> meta, string key)
        {
            return meta.TryGetValue(key, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        /// <summary>初期の JSON の保存があれば取り込む。取り込んだら元のファイルは <c>.imported</c> に改名して残す。</summary>
        private GameSave ImportLegacyJson()
        {
            var legacy = new JsonGameStore(_directory);
            var save = legacy.Load();
            if (save == null)
            {
                LastLoadStatus = legacy.LastLoadStatus;
                return null;
            }

            Save(save);
            try
            {
                var imported = legacy.FilePath + ".imported";
                if (File.Exists(imported))
                {
                    File.Delete(imported);
                }

                File.Move(legacy.FilePath, imported);
            }
            catch (IOException)
            {
                // 改名できなくても、DB に取り込めていれば先へ進む (次の起動では DB が優先される)。
            }

            LastLoadStatus = SaveLoadStatus.Loaded;
            return save;
        }

        private void Quarantine()
        {
            foreach (var path in new[] { _path, _path + "-wal", _path + "-shm" })
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var target = path + ".corrupt";
                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    File.Move(path, target);
                }
                catch (IOException)
                {
                    // 退避できなくても起動は続ける。
                }
            }
        }

        // ---- テーブル (SQLite-net の ORM)。列の属性は、コードの削減 (stripping) で落とされないために付ける。----

        [Preserve(AllMembers = true)]
        [Table("meta")]
        internal sealed class MetaRow
        {
            [PrimaryKey, Column("key")] public string Key { get; set; }
            [Column("value")] public string Value { get; set; }
        }

        [Preserve(AllMembers = true)]
        [Table("ops")]
        internal sealed class OpRow
        {
            [PrimaryKey, AutoIncrement, Column("seq")] public int Seq { get; set; }
            [Column("kind")] public string Kind { get; set; }
            [Column("at_ms")] public long AtMs { get; set; }
            [Column("id")] public string Id { get; set; }
            [Column("print_day")] public int PrintDay { get; set; }
            [Column("verification")] public int Verification { get; set; }
            [Column("answer")] public int Answer { get; set; }
            [Column("no_shopping")] public bool NoShopping { get; set; }

            /// <summary>数値の引数 (氷室の効き目など)。2026-10-17 に足した列。古い DB には CreateTable が列を足す。</summary>
            [Column("value")] public double Value { get; set; }
        }

        [Preserve(AllMembers = true)]
        [Table("island")]
        internal sealed class IslandRow
        {
            [PrimaryKey, Column("id")] public int Id { get; set; }
            [Column("json")] public string Json { get; set; }
        }

        [Preserve(AllMembers = true)]
        [Table("activity")]
        internal sealed class ActivityRow
        {
            [PrimaryKey, AutoIncrement, Column("seq")] public int Seq { get; set; }
            [Column("at_ms")] public long AtMs { get; set; }
            [Column("day")] public int Day { get; set; }
            [Column("kind")] public string Kind { get; set; }
            [Column("category")] public int Category { get; set; }
            [Column("label")] public string Label { get; set; }
            [Column("electricity")] public double Electricity { get; set; }
            [Column("water")] public double Water { get; set; }
            [Column("gas")] public double Gas { get; set; }
            [Column("all_mighty")] public double AllMighty { get; set; }
        }
    }
}
