using UnityEditor;
using UnityEngine;

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

        EditorGUILayout.Space(10);
        DrawExportSection(clip);
    }

    private void DrawTemplateSection(LightstripClip clip)
    {
        SerializedProperty selectedTemplateProp = serializedObject.FindProperty("selectedTemplate");
        SerializedProperty previewPrefabProp = serializedObject.FindProperty("templatePreviewPrefab");
        LightstripTemplate selectedTemplate = selectedTemplateProp.objectReferenceValue as LightstripTemplate;
        LightstripMBPControl boundController = LightstripTemplateEditorTools.FindBoundController(clip);
        bool usingFallbackController = LightstripTemplateEditorTools.IsFallbackController(clip, boundController);
        GameObject effectivePreviewPrefab = previewPrefabProp.objectReferenceValue != null
            ? previewPrefabProp.objectReferenceValue as GameObject
            : boundController != null
                ? boundController.templatePreviewPrefab
                : null;

        EditorGUILayout.LabelField("Lightstrip Template", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Selected Template", selectedTemplate, typeof(LightstripTemplate), false);
            }

            GUI.enabled = selectedTemplate != null;
            if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                EditorGUIUtility.PingObject(selectedTemplate);
            GUI.enabled = true;
        }

        if (selectedTemplate != null)
        {
            string tags = LightstripTemplateEditorTools.GetTagText(selectedTemplate);
            EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("No template selected. Use Select Template to choose and apply one.", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Bound Controller", boundController, typeof(LightstripMBPControl), true);
            EditorGUILayout.ObjectField("Template Preview Prefab", effectivePreviewPrefab, typeof(GameObject), false);
        }

        EditorGUILayout.PropertyField(previewPrefabProp, new GUIContent("Clip Preview Prefab Override"));

        if (boundController == null && effectivePreviewPrefab == null)
        {
            EditorGUILayout.HelpBox("No LightstripMBPControl binding was found for this Timeline track, no clip override prefab is assigned, and no single scene controller with Template Preview Prefab was available as fallback.", MessageType.Warning);
        }
        else if (usingFallbackController)
        {
            EditorGUILayout.HelpBox("Using the only scene LightstripMBPControl that has Template Preview Prefab assigned. This is a fallback because the Timeline track binding was not found.", MessageType.Info);
        }
        else if (effectivePreviewPrefab == null)
        {
            EditorGUILayout.HelpBox("Assign Template Preview Prefab on the bound LightstripMBPControl, or assign Clip Preview Prefab Override here.", MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Template", GUILayout.Height(30f)))
            {
                serializedObject.ApplyModifiedProperties();
                LightstripTemplateSelectorWindow.Open(clip);
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
            "applyTemplateAnimationSettings",
            "templatePreviewPrefab"
        );
    }

    private void DrawExportSection(LightstripClip clip)
    {
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.7f);
        if (GUILayout.Button("Export Current Clip As LightstripTemplate", GUILayout.Height(32f)))
            ExportToTemplate(clip);
        GUI.backgroundColor = old;
    }

    private static void ExportToTemplate(LightstripClip clip)
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
        EditorGUIUtility.PingObject(template);
    }

    private static void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
    }
}
