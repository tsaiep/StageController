using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnifiedStageClip))]
public class UnifiedStageClipInspector : Editor
{
    private string _feedbackMessage = "";
    private double _feedbackTime;

    public override void OnInspectorGUI()
    {
        UnifiedStageClip clip = (UnifiedStageClip)target;

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

    private void DrawTemplateSection(UnifiedStageClip clip)
    {
        SerializedProperty applyTemplateProp = serializedObject.FindProperty("applyTemplate");
        SerializedProperty selectedTemplateProp = serializedObject.FindProperty("selectedTemplate");
        UnifiedStageTemplate legacyPendingTemplate = applyTemplateProp.objectReferenceValue as UnifiedStageTemplate;
        UnifiedStageTemplate selectedTemplate = selectedTemplateProp.objectReferenceValue as UnifiedStageTemplate;
        UnifiedStageTemplate shownTemplate = selectedTemplate != null ? selectedTemplate : legacyPendingTemplate;
        UnifiedStageController boundController = UnifiedStageTemplateEditorTools.FindBoundController(clip);
        bool usingFallbackController = UnifiedStageTemplateEditorTools.IsFallbackController(clip, boundController);
        GameObject previewPrefab = boundController != null ? boundController.templatePreviewPrefab : null;

        EditorGUILayout.LabelField("Stage Template", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Selected Template", shownTemplate, typeof(UnifiedStageTemplate), false);
            }

            GUI.enabled = shownTemplate != null;
            if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                EditorGUIUtility.PingObject(shownTemplate);
            GUI.enabled = true;
        }

        if (shownTemplate != null)
        {
            string tags = UnifiedStageTemplateEditorTools.GetTagText(shownTemplate);
            EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("No template selected. Use Select Template to choose and apply one.", MessageType.Info);
        }

        if (legacyPendingTemplate != null && selectedTemplate == null)
        {
            EditorGUILayout.HelpBox("Legacy applyTemplate is set. It will be applied by OnValidate and is shown here as a fallback.", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Bound Controller", boundController, typeof(UnifiedStageController), true);
            EditorGUILayout.ObjectField("Template Preview Prefab", previewPrefab, typeof(GameObject), false);
        }

        if (boundController == null)
        {
            EditorGUILayout.HelpBox("No UnifiedStageController binding was found for this Timeline track, and no single scene controller with Template Preview Prefab was available as fallback.", MessageType.Warning);
        }
        else if (usingFallbackController)
        {
            EditorGUILayout.HelpBox("Using the only scene UnifiedStageController that has Template Preview Prefab assigned. This is a fallback because the Timeline track binding was not found.", MessageType.Info);
        }
        else if (previewPrefab == null)
        {
            EditorGUILayout.HelpBox("The bound UnifiedStageController has no Template Preview Prefab assigned. This reads from the scene component instance bound to the Timeline track, not from the UnifiedStageController script asset in the Project window.", MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Template", GUILayout.Height(30f)))
            {
                UnifiedStageTemplateSelectorWindow.Open(clip, boundController);
            }
        }

        DrawFeedback();
        EditorGUILayout.EndVertical();
    }

    private void DrawClipProperties()
    {
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "applyTemplate",
            "selectedTemplate",
            "applyTemplateColorSettings",
            "applyTemplateRotationSettings",
            "applyTemplateFixtureSettings",
            "clipDisplayName"
        );
    }

    private void DrawExportSection(UnifiedStageClip clip)
    {
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.7f);
        if (GUILayout.Button("Export Current Clip As UnifiedStageTemplate", GUILayout.Height(32f)))
            ExportToTemplate(clip);
        GUI.backgroundColor = old;
    }

    private void ExportToTemplate(UnifiedStageClip clip)
    {
        UnifiedStageTemplate newAsset = ScriptableObject.CreateInstance<UnifiedStageTemplate>();
        CopyClipToTemplate(clip, newAsset);

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Stage Template",
            "NewStageTemplate",
            "asset",
            "Choose where to save the UnifiedStageTemplate asset."
        );

        if (string.IsNullOrEmpty(path))
            return;

        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(newAsset);
    }

    private static void CopyClipToTemplate(UnifiedStageClip clip, UnifiedStageTemplate template)
    {
        template.lightMode = clip.lightMode;
        template.lightRange = clip.lightRange;
        template.lightGradient = UnifiedStageClip.CloneGradient(clip.lightGradient);
        template.beamLengthGradient = UnifiedStageGradientUtility.CloneOrDefaultBeamLengthGradient(clip.beamLengthGradient);
        template.intensityMultiplier = clip.intensityMultiplier;
        template.sensitivity = clip.sensitivity;
        template.smoothness = clip.smoothness;
        template.beamAngle = clip.beamAngle;
        template.softness = clip.softness;
        template.enableScatterMode = clip.enableScatterMode;
        template.colorSampleMode = clip.colorSampleMode;
        template.bpm = clip.bpm;
        template.beatTimeRef = clip.beatTimeRef;
        template.beatPhaseOffset = clip.beatPhaseOffset;
        template.beatSnapColors = UnifiedStageClip.CloneColorArray(clip.beatSnapColors);
        template.beatSnapTransitionTime = clip.beatSnapTransitionTime;
        template.beatGroupDelayFactor = clip.beatGroupDelayFactor;
        template.beatLightDelayFactor = clip.beatLightDelayFactor;
        template.beatGroupDelayCurve = UnifiedStageClip.CloneAnimationCurve(clip.beatGroupDelayCurve);
        template.beatLightDelayCurve = UnifiedStageClip.CloneAnimationCurve(clip.beatLightDelayCurve);
        template.useAudioAnalyzerBrightness = clip.useAudioAnalyzerBrightness;
        template.audioBeatLightInterval = clip.audioBeatLightInterval;
        template.audioBeatIndices = UnifiedStageClip.CloneIntArray(clip.audioBeatIndices);
        template.audioBrightnessOffset = clip.audioBrightnessOffset;
        template.audioBrightnessMultiplier = clip.audioBrightnessMultiplier;
        template.audioBrightnessLerp = clip.audioBrightnessLerp;
        template.globalColor = clip.globalColor;
        template.freezeUseClipGradient = clip.freezeUseClipGradient;
        template.rotationMode = clip.rotationMode;
        template.rotationSpeed = clip.rotationSpeed;
        template.rotationRange = clip.rotationRange;
        template.staticAngleOffset = clip.staticAngleOffset;
        template.cyclePauseTime = clip.cyclePauseTime;
        template.animationOffset = clip.animationOffset;
        template.trackingTarget = clip.trackingTarget;
        template.groupDelayCurve = UnifiedStageClip.CloneAnimationCurve(clip.groupDelayCurve);
        template.groupDelayFactor = clip.groupDelayFactor;
        template.groupRotationRangeCurve = UnifiedStageClip.CloneAnimationCurve(clip.groupRotationRangeCurve);
        template.lightDelayCurve = UnifiedStageClip.CloneAnimationCurve(clip.lightDelayCurve);
        template.lightDelayFactor = clip.lightDelayFactor;
        template.lightRotationRangeCurve = UnifiedStageClip.CloneAnimationCurve(clip.lightRotationRangeCurve);
        template.spreadAngle = clip.spreadAngle;
        template.spreadArcRange = clip.spreadArcRange;
        template.spreadAngleCurve = UnifiedStageClip.CloneAnimationCurve(clip.spreadAngleCurve);
        template.spreadAngleCurveByIndex = UnifiedStageClip.CloneAnimationCurve(clip.spreadAngleCurveByIndex);
        template.spreadPanCurve = UnifiedStageClip.CloneAnimationCurve(clip.spreadPanCurve);
        template.fannedAngle = clip.fannedAngle;
        template.fannedAngleCurve = UnifiedStageClip.CloneAnimationCurve(clip.fannedAngleCurve);
    }

    private void DrawFeedback()
    {
        if (string.IsNullOrEmpty(_feedbackMessage))
            return;

        if (EditorApplication.timeSinceStartup - _feedbackTime > 3.0)
        {
            _feedbackMessage = "";
            return;
        }

        Color old = GUI.contentColor;
        GUI.contentColor = new Color(0.35f, 0.95f, 0.45f);
        EditorGUILayout.LabelField(_feedbackMessage, EditorStyles.miniLabel);
        GUI.contentColor = old;
    }

    private static void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.35f));
    }
}
