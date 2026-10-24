using System;
using System.IO;
using SQLite;
using UnityEngine;

namespace ProjectPenguin.Data
{
    public sealed class ProductionVillageStore : IProductionVillageStore
    {
        public const string FileName="penguin-production-v2.db", DebugFileName="penguin-production-v2-debug.db";
        private readonly string _profile;
        private bool _blocked;
        public string FilePath {get;}
        public ProductionVillageStore(string directory,bool development=false)
        { _profile=development?"development":"production";FilePath=Path.Combine(directory,development?DebugFileName:FileName); }
        public ProductionVillageSave Load()
        {
            if(!File.Exists(FilePath))return null;
            try
            {
                using(var db=new SQLiteConnection(FilePath,SQLiteOpenFlags.ReadOnly|SQLiteOpenFlags.FullMutex))
                {
                    if(db.ExecuteScalar<int>("PRAGMA user_version")!=1)throw new InvalidOperationException("保存DBの版が未対応です");
                    var json=db.ExecuteScalar<string>("SELECT payload FROM village WHERE id=1");
                    if(string.IsNullOrWhiteSpace(json))throw new InvalidOperationException("保存DBが空です");
                    var save=JsonUtility.FromJson<ProductionVillageSave>(json);if(save==null)throw new InvalidOperationException("保存を読めません");
                    save.Validate(_profile);_blocked=false;return save;
                }
            }
            catch{_blocked=true;throw;}
        }
        public void Save(ProductionVillageSave save)
        {
            if(_blocked)throw new InvalidOperationException("読込失敗したDBを保護しています。上書きしません");
            save.Validate(_profile);Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using(var db=new SQLiteConnection(FilePath,SQLiteOpenFlags.ReadWrite|SQLiteOpenFlags.Create|SQLiteOpenFlags.FullMutex))
            {
                var version=db.ExecuteScalar<int>("PRAGMA user_version");if(version>1)throw new InvalidOperationException("新しい版のDBを保護しています");
                db.RunInTransaction(()=>{
                    db.Execute("CREATE TABLE IF NOT EXISTS village (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL)");
                    db.Execute("INSERT OR REPLACE INTO village(id,payload) VALUES(1,?)",JsonUtility.ToJson(save));
                    db.Execute("PRAGMA user_version=1");
                });
            }
        }
    }
}
