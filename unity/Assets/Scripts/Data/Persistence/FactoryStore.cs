using System;
using ProjectPenguin.Domain.Factory;
using UnityEngine;

namespace ProjectPenguin.Data.Persistence
{
    public sealed class FactoryStore
    {
        private readonly SaveStore _store;
        private readonly FactoryCatalog _catalog;
        private readonly FactoryRules _rules;

        public FactoryStore(string directory, FactoryCatalog catalog, FactoryRules rules)
        {
            _store = new SaveStore(directory, "factory-v1.json", FactoryState.CurrentVersion);
            _catalog = catalog; _rules = rules;
        }

        public static FactoryCatalog LoadCatalog()
        {
            var text = Resources.Load<TextAsset>("Factory/catalog");
            if (text == null) throw new InvalidOperationException("Factory catalog is missing");
            var catalog = JsonUtility.FromJson<FactoryCatalog>(text.text);
            catalog.Validate(); return catalog;
        }

        public static FactoryRules LoadRules()
        {
            var text = Resources.Load<TextAsset>("Factory/prototype-rules");
            if (text == null) throw new InvalidOperationException("Factory prototype settings are missing");
            var rules = JsonUtility.FromJson<FactoryRules>(text.text);
            rules.Validate(); return rules;
        }

        public SaveLoadResult Load() => _store.Load(json =>
        {
            var state = JsonUtility.FromJson<FactoryState>(json);
            var session = new FactorySession(_catalog, _rules, _ => { }, state);
            return state != null && session.Snapshot.version == FactoryState.CurrentVersion;
        });

        public FactorySession Open(out SaveLoadStatus status)
        {
            var loaded = Load(); status = loaded.Status;
            if (status != SaveLoadStatus.NotFound && !loaded.HasPayload)
                throw new InvalidOperationException(status == SaveLoadStatus.TooNew ? "この保存には新しいアプリが必要です" : "保存を復旧できませんでした。元のファイルを保持しています");
            var state = loaded.HasPayload ? JsonUtility.FromJson<FactoryState>(loaded.Payload) : null;
            return new FactorySession(_catalog, _rules, Save, state);
        }

        public void Save(FactoryState state) => _store.Save(JsonUtility.ToJson(state));
    }
}
