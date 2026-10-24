using System;
using System.IO;
using ProjectPenguin.Data.Persistence;
using UnityEngine;

namespace ProjectPenguin.Data
{
    /// <summary>プレイヤーの状態の置き場。今は JSON のファイル。Hack Day に SQLite の実装へ差し替える。</summary>
    public interface IGameStore
    {
        /// <summary>保存が無い・読めないときは null。</summary>
        GameSave Load();

        void Save(GameSave save);

        /// <summary>設定の「データを消す」。</summary>
        void Delete();

        /// <summary>直近の <see cref="Load"/> の結果。読めなかった理由を画面に出すために使う。</summary>
        SaveLoadStatus LastLoadStatus { get; }
    }

    /// <summary>
    /// <c>Application.persistentDataPath</c> に JSON で保存する。<see cref="SaveStore"/> が書き込みを担い、
    /// チェックサムで壊れを見つけ、1 世代のバックアップと一時ファイルから戻す。途中でアプリが落ちても前の保存が壊れない。
    /// 封筒に包む前の素の JSON (初期の保存) も読める。次の保存で封筒に包まれる。
    /// </summary>
    public sealed class JsonGameStore : IGameStore
    {
        private const string FileName = "penguin-save.json";

        private readonly SaveStore _store;

        public JsonGameStore(string directory = null)
        {
            _store = new SaveStore(directory ?? Application.persistentDataPath, FileName, GameSave.CurrentVersion);
        }

        public string FilePath => _store.MainPath;

        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.NotFound;

        public GameSave Load()
        {
            var result = _store.Load(IsUsable);
            switch (result.Status)
            {
                case SaveLoadStatus.NotFound:
                case SaveLoadStatus.TooNew:
                    LastLoadStatus = result.Status;
                    return null;

                case SaveLoadStatus.Corrupt:
                case SaveLoadStatus.MigrationFailed:
                    // 封筒に包む前の素の JSON かもしれない。読めればそのまま使う (次の保存で封筒になる)。
                    var legacy = TryReadLegacy();
                    if (legacy != null)
                    {
                        LastLoadStatus = SaveLoadStatus.Loaded;
                        return legacy;
                    }

                    // 読めない保存は消さずに退避し、最初から始める (原因を後で調べられるように)。
                    Debug.LogWarning($"{nameof(JsonGameStore)}: 保存を読めませんでした ({result.Status})。最初から始めます。");
                    TryQuarantine();
                    LastLoadStatus = result.Status;
                    return null;

                default:
                    LastLoadStatus = result.Status;
                    var save = JsonUtility.FromJson<GameSave>(result.Payload);
                    if (result.Status != SaveLoadStatus.Loaded || result.Migrated)
                    {
                        // バックアップから戻した・版を上げたときは、すぐ保存し直して本体を作り直す。
                        Save(save);
                    }

                    return save;
            }
        }

        public void Save(GameSave save)
        {
            if (save == null)
            {
                return;
            }

            _store.Save(JsonUtility.ToJson(save));
        }

        public void Delete() => _store.DeleteAll();

        private static bool IsUsable(string payload)
        {
            try
            {
                var save = JsonUtility.FromJson<GameSave>(payload);
                return save != null && save.createdUnixMs > 0 && save.version <= GameSave.CurrentVersion;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private GameSave TryReadLegacy()
        {
            try
            {
                if (!File.Exists(_store.MainPath))
                {
                    return null;
                }

                var text = File.ReadAllText(_store.MainPath);
                if (text.Contains(SaveStore.FormatName))
                {
                    return null;
                }

                var save = JsonUtility.FromJson<GameSave>(text);
                return save != null && save.createdUnixMs > 0 ? save : null;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException)
            {
                return null;
            }
        }

        private void TryQuarantine()
        {
            try
            {
                _store.QuarantineAll();
            }
            catch (IOException)
            {
                // 退避できなくても起動は続ける。
            }
        }
    }

    /// <summary>テストと、保存したくない場面 (確認用のシーン) のための置き場。</summary>
    public sealed class MemoryGameStore : IGameStore
    {
        private string _json;

        public SaveLoadStatus LastLoadStatus => string.IsNullOrEmpty(_json) ? SaveLoadStatus.NotFound : SaveLoadStatus.Loaded;

        public GameSave Load() => string.IsNullOrEmpty(_json) ? null : JsonUtility.FromJson<GameSave>(_json);

        public void Save(GameSave save) => _json = save == null ? null : JsonUtility.ToJson(save);

        public void Delete() => _json = null;
    }
}
