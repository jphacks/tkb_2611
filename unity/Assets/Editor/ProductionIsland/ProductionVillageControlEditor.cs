using ProjectPenguin.Systems;
using UnityEditor;
using UnityEngine;

namespace ProjectPenguin.Editor.ProductionIsland
{
    [CustomEditor(typeof(ProductionVillageControl))]
    public sealed class ProductionVillageControlEditor : UnityEditor.Editor
    {
        private bool _showNormalInitial;
        public override bool RequiresConstantRepaint()=>Application.isPlaying;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var control=(ProductionVillageControl)target;
            var session=control.GetComponent<ProductionVillageSession>();
            _showNormalInitial=EditorGUILayout.Foldout(_showNormalInitial,"通常の初回起動設定（保存済みの村には反映しません）",true);
            if(_showNormalInitial)DrawPropertiesExcluding(serializedObject,"m_Script","_inspectorDebugSettings");
            EditorGUILayout.LabelField("Inspector配置デバッグ",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("チェックを入れてPlayするとメモリだけで起動します。Editor / 開発ビルド専用です。停止後にチェックを外すと通常保存の村へ戻ります。Play中のチェック変更では保存先は切り替わりません。",MessageType.Info);
            var settings=serializedObject.FindProperty("_inspectorDebugSettings");
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("_enabled"),new GUIContent("Inspectorの配置でデバッグ起動"));
            EditorGUILayout.PropertyField(settings.FindPropertyRelative("_facilities"),new GUIContent("施設・グリッド座標・初期在庫"),true);
            Draw(settings,"_gas","初期ガス");Draw(settings,"_electricity","初期電気");Draw(settings,"_water","初期水");
            Draw(settings,"_recipeSpeed","通常生産の速度");Draw(settings,"_timeScale","時間倍率");Draw(settings,"_deliveryMs","配送時間 (ms)");Draw(settings,"_unlimited","資源・材料を無制限にする");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField("配置プリセット（施設一覧のみ置換）",EditorStyles.boldLabel);
            if(GUILayout.Button("漁場3棟直線 → ちくわ + 倉庫"))Preset(control,VillageInspectorPreset.Chikuwa);
            if(GUILayout.Button("漁場・製塩所・漁場 → かまぼこ + 倉庫"))Preset(control,VillageInspectorPreset.Kamaboko);
            if(GUILayout.Button("塩を角にしたL字 → 干物 + 倉庫"))Preset(control,VillageInspectorPreset.DriedFish);
            EditorGUILayout.HelpBox("初期配置の建設費は消費しません。速度0.01・時間倍率100なら魚は実時間約4秒、群の追加品は稼働と空きがあれば約12秒で1個です。群表示へ縮小して確認してください。",MessageType.Info);
            EditorGUILayout.LabelField("現在のモード",!Application.isPlaying?"停止中（次回Play時に選択）":session!=null&&session.UsingInspectorDebugLayout?"メモリのみ・Inspector配置":"通常保存（SQLite）");
            using(new EditorGUI.DisabledScope(!Application.isPlaying||session==null||!session.UsingInspectorDebugLayout))
                if(GUILayout.Button(new GUIContent("編集したデバッグ配置を適用","検証時間・進捗と在庫を初期化し、配置/移動プレビューを解除します")))session.ApplyInspectorDebugLayout();
            if(session!=null&&!string.IsNullOrEmpty(session.InspectorDebugError))EditorGUILayout.HelpBox(session.InspectorDebugError,MessageType.Error);
            if(session?.Core!=null)
            {
                if(!string.IsNullOrEmpty(session.Core.Error))EditorGUILayout.HelpBox(session.Core.Error,MessageType.Error);
                var view=session.Core.View;
                if(view!=null)
                {
                    EditorGUILayout.LabelField("経過（シミュレーション秒）",(view.AtMs/1000.0).ToString("0.0"));
                    if(view.Synergies.Count==0)EditorGUILayout.HelpBox("施設群は未成立です。施設の種類と2マス間隔の形を確認してください。",MessageType.Info);
                    foreach(var group in view.Synergies)EditorGUILayout.LabelField(group.Name,group.Status);
                    EditorGUILayout.HelpBox(view.BottleneckSummary,MessageType.Info);
                }
            }
        }
        private static void Draw(SerializedProperty settings,string field,string label)=>EditorGUILayout.PropertyField(settings.FindPropertyRelative(field),new GUIContent(label));
        private void Preset(ProductionVillageControl control,VillageInspectorPreset preset)
        {
            Undo.RecordObject(control,"デバッグ配置プリセット");control.InspectorDebugSettings.ApplyPreset(preset);EditorUtility.SetDirty(control);serializedObject.Update();
        }
    }
}
