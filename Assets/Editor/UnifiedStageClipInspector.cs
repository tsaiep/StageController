using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEditor.Timeline;
using System.Linq;

[CustomEditor(typeof(UnifiedStageClip))]
public class UnifiedStageClipInspector : Editor
{
    private string feedbackMessage = "";
    private double feedbackTime = 0;

    public override void OnInspectorGUI()
    {
        UnifiedStageClip clip = (UnifiedStageClip)target;

        // --- 1. 繪製手動調整區 ---
        SerializedObject so = serializedObject;
        so.Update();
        SerializedProperty prop = so.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "applyTemplate" || prop.name == "m_Script" || prop.name == "clipDisplayName")
                    continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            while (prop.NextVisible(false));
        }
        so.ApplyModifiedProperties();

        GUILayout.Space(25);
        rectLine(new Color(0.5f, 0.5f, 0.5f, 0.4f));
        GUILayout.Space(10);

        // --- 2. 底部功能區 (資產管理) ---
        EditorGUILayout.LabelField("資產管理與模板操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("HelpBox");
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        clip.applyTemplate = (UnifiedStageTemplate)EditorGUILayout.ObjectField(
            "選擇模板",
            clip.applyTemplate,
            typeof(UnifiedStageTemplate),
            false
        );

        GUI.enabled = clip.applyTemplate != null;

        if (GUILayout.Button("確認套用", GUILayout.Width(80)))
        {
            if (clip.applyTemplate != null)
            {
                // 1. 註冊 Undo
                Undo.RecordObject(clip, "Apply Stage Template");

                var t = clip.applyTemplate;

                // 2. 數據同步
                clip.lightGradient = t.lightGradient;
                clip.intensityMultiplier = t.intensityMultiplier;
                clip.minBrightness = t.minBrightness;
                clip.sensitivity = t.sensitivity;
                clip.smoothness = t.smoothness;
                clip.beamAngle = t.beamAngle;
                clip.enableScatterMode = t.enableScatterMode;
                clip.rotationMode = t.rotationMode;
                clip.rotationSpeed = t.rotationSpeed;
                clip.rotationRange = t.rotationRange;
                clip.staticAngleOffset = t.staticAngleOffset;
                clip.cyclePauseTime = t.cyclePauseTime;
                clip.delayCurve = t.delayCurve;
                clip.delayFactor = t.delayFactor;

                // 3. 設定內部名稱
                string newName = t.name;
                clip.clipDisplayName = newName;

                // --- 核心修正：強制同步修改 Timeline 軌道上的名稱 ---
                // 找到目前正在編輯這個資產的 TimelineClip 物件
                var timelineClip = TimelineEditor.selectedClips.FirstOrDefault(c => c.asset == clip);
                if (timelineClip != null)
                {
                    timelineClip.displayName = newName;
                }

                clip.applyTemplate = null;

                // 4. 強制保存與刷新
                EditorUtility.SetDirty(clip);

                // 強制刷新 Timeline 視窗
                TimelineEditor.Refresh(RefreshReason.ContentsModified);

                feedbackMessage = "✔ 模板已成功套用且名稱已更新！";
                feedbackTime = EditorApplication.timeSinceStartup;
            }
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(feedbackMessage))
        {
            if (EditorApplication.timeSinceStartup - feedbackTime < 3.0)
            {
                GUI.contentColor = new Color(0.4f, 1f, 0.4f);
                EditorGUILayout.LabelField(feedbackMessage, EditorStyles.miniLabel);
                GUI.contentColor = Color.white;
            }
            else { feedbackMessage = ""; }
        }

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.7f);
        if (GUILayout.Button("將當前參數導出為新模板 (UnifiedStageTemplate)", GUILayout.Height(35)))
        {
            ExportToTemplate(clip);
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(10);
    }

    private void rectLine(Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, color);
    }

    private void ExportToTemplate(UnifiedStageClip clip)
    {
        UnifiedStageTemplate newAsset = ScriptableObject.CreateInstance<UnifiedStageTemplate>();
        newAsset.lightGradient = clip.lightGradient;
        newAsset.intensityMultiplier = clip.intensityMultiplier;
        newAsset.minBrightness = clip.minBrightness;
        newAsset.sensitivity = clip.sensitivity;
        newAsset.smoothness = clip.smoothness;
        newAsset.beamAngle = clip.beamAngle;
        newAsset.enableScatterMode = clip.enableScatterMode;
        newAsset.rotationMode = clip.rotationMode;
        newAsset.rotationSpeed = clip.rotationSpeed;
        newAsset.rotationRange = clip.rotationRange;
        newAsset.staticAngleOffset = clip.staticAngleOffset;
        newAsset.cyclePauseTime = clip.cyclePauseTime;
        newAsset.delayCurve = clip.delayCurve;
        newAsset.delayFactor = clip.delayFactor;

        string path = EditorUtility.SaveFilePanelInProject("儲存新模板", "NewStageTemplate", "asset", "請輸入模板名稱");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", "新模板已生成", "確定");
        }
    }
}