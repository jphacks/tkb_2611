using System;
using ProjectPenguin.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Editor
{
    public static class ClassroomRaidSetup
    {
        public const string ScenePath="Assets/Scenes/ClassroomQuizRescueRaidPreview.unity";
        private const string PanelPath="Assets/UI/ClassroomRaidPanelSettings.asset";

        [MenuItem("Production Island/Raid/Open Classroom Quiz Preview")]
        public static void OpenPreview()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if(panel==null)
            {
                panel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset"));
                panel.name="ClassroomRaidPanelSettings";
                panel.referenceResolution=new Vector2Int(1280,900);
                panel.match=0;
                AssetDatabase.CreateAsset(panel,PanelPath);
                AssetDatabase.SaveAssets();
                panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            }
            // Keep controls readable on narrow screens; the layout reflows instead of shrinking.
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;
            panel.scale=1;
            panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/ClassroomRaidTheme.tss");
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null)
            {
                var existing=EditorSceneManager.OpenScene(ScenePath);
                var document=UnityEngine.Object.FindAnyObjectByType<ClassroomRaidPreview>().GetComponent<UIDocument>();
                BindPanel(document,panel);
                EditorSceneManager.SaveScene(existing);
                return;
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var ui=new GameObject("Classroom Quiz Rescue Raid — LOCAL ONLY");
            var newDocument=ui.AddComponent<UIDocument>();
            var presenter=ui.AddComponent<ClassroomRaidPreview>();
            var serialized=new SerializedObject(presenter);
            serialized.FindProperty("_style").objectReferenceValue=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Screens/Raid/ClassroomRaidPreview.uss");
            serialized.FindProperty("_font").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BindPanel(newDocument,panel);
            var camera=new GameObject("Background Camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.91f,.96f,.98f); camera.cullingMask=0;
            EditorSceneManager.SaveScene(scene,ScenePath);
            Selection.activeGameObject=ui;
            Debug.Log("Classroom local prototype created: "+ScenePath);
        }

        private static void BindPanel(UIDocument document,PanelSettings panel)
        {
            var settings=new SerializedObject(document);
            settings.FindProperty("m_PanelSettings").objectReferenceValue=panel;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
        }
    }
}
