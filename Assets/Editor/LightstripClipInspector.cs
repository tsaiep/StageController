using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomEditor(typeof(LightstripClip))]
public class LightstripClipInspector : Editor
{
    public override void OnInspectorGUI()
    {
        LightstripClip clip = (LightstripClip)target;

        serializedObject.Update();
        DrawTemplateSection(clip);
        EditorGUILayout.Space(10);
        DrawSeparator();
        EditorGUILayout.Space(8);
        DrawClipProperties();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTemplateSection(LightstripClip clip)
    {
        SerializedProperty selectedTemplateProp = serializedObject.FindProperty("selectedTemplate");
        SerializedProperty applyManualProp = serializedObject.FindProperty("applyTemplateManualModeSettings");
        SerializedProperty applyColorProp = serializedObject.FindProperty("applyTemplateColorSettings");
        SerializedProperty applyAnimationProp = serializedObject.FindProperty("applyTemplateAnimationSettings");

        EditorGUILayout.LabelField("Lightstrip Template", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.PropertyField(selectedTemplateProp, new GUIContent("Template"));
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Apply Categories", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(applyManualProp, new GUIContent("Manual Mode Setting"));
        EditorGUILayout.PropertyField(applyColorProp, new GUIContent("Color Setting"));
        EditorGUILayout.PropertyField(applyAnimationProp, new GUIContent("Animation Setting"));

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedTemplateProp.objectReferenceValue == null))
            {
                if (GUILayout.Button("Apply Template", GUILayout.Height(28f)))
                {
                    serializedObject.ApplyModifiedProperties();
                    ApplySelectedTemplate(clip);
                    serializedObject.Update();
                }
            }

            if (GUILayout.Button("Save As Template", GUILayout.Height(28f)))
            {
                serializedObject.ApplyModifiedProperties();
                SaveAsTemplate(clip);
                serializedObject.Update();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawClipProperties()
    {
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "selectedTemplate",
            "applyTemplateManualModeSettings",
            "applyTemplateColorSettings",
            "applyTemplateAnimationSettings"
        );
    }

    private static void ApplySelectedTemplate(LightstripClip clip)
    {
        if (clip.selectedTemplate == null)
            return;

        Undo.RecordObject(clip, "Apply Lightstrip Template");
        clip.ApplyTemplateValues(clip.selectedTemplate);
        EditorUtility.SetDirty(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    private static void SaveAsTemplate(LightstripClip clip)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Lightstrip Template",
            "NewLightstripTemplate",
            "asset",
            "Choose where to save the LightstripTemplate asset."
        );

        if (string.IsNullOrEmpty(path))
            return;

        LightstripTemplate template = ScriptableObject.CreateInstance<LightstripTemplate>();
        clip.CopyValuesToTemplate(template);

        AssetDatabase.CreateAsset(template, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.RecordObject(clip, "Assign Lightstrip Template");
        clip.selectedTemplate = template;
        EditorUtility.SetDirty(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        EditorGUIUtility.PingObject(template);
    }

    private static void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.25f, 0.25f, 0.25f, 1f));
    }
}
