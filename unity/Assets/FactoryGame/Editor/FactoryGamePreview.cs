using System;
using System.IO;
using ProjectPenguin.Data.Persistence;
using ProjectPenguin.Domain.Factory;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProjectPenguin.FactoryGame.Editor
{
    public static class FactoryGamePreview
    {
        [MenuItem("Project Penguin/Factory/Play Isolated Development Preview")]
        public static void Open()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before opening the isolated preview");
            var catalog = FactoryStore.LoadCatalog(); var rules = FactoryStore.LoadRules();
            var state = new FactoryState { gas = 100, electric = 100, water = 100, stage = 4, points = rules.stagePoints[3] };
            var session = new FactorySession(catalog, rules, _ => { }, state);
            foreach (var entry in new[] { ("fishery",0,0), ("saltworks",1,0), ("drying",0,1), ("warehouse",1,1) })
            {
                var result = session.Build(entry.Item1, entry.Item2, entry.Item3);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            }
            var directory = Path.Combine(Path.GetTempPath(), "factory-preview-" + Guid.NewGuid().ToString("N"));
            new FactoryStore(directory, catalog, rules).Save(session.Snapshot);
            FactoryGameDriver.EditorSaveDirectoryOverride = directory;
            SessionState.SetString("FactoryGame.PreviewDirectory", directory);
            EditorSceneManager.OpenScene(FactoryGameSceneSetup.ScenePath);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
    }
}
