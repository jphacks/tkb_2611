using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEngine;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Focuses the review's Game view without changing input settings or device states.</summary>
    [InitializeOnLoad]
    public static class FacilityReviewFocus
    {
        static FacilityReviewFocus()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += FocusGameView;
        }

        [MenuItem("Production Island/Focus Review Game View")]
        public static void FocusGameView()
        {
            if (Application.isBatchMode || !EditorApplication.isPlaying
                || Object.FindAnyObjectByType<FacilityDesignGallery>() == null) return;
            if (!EditorApplication.ExecuteMenuItem("Window/General/Game"))
                Debug.LogWarning("Game画面を開けませんでした。Window > General > Game から開いてください。");
        }
    }
}
