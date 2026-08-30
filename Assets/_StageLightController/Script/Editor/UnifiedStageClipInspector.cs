using UnityEditor;
using UnityEngine;
using ColorSampleMode = UnifiedStageController.ColorSampleMode;
using RotationMode = UnifiedStageController.RotationMode;
using StageLightMode = UnifiedStageController.StageLightMode;

[CustomEditor(typeof(UnifiedStageClip))]
public class UnifiedStageClipInspector : Editor
{
    private const float FoldoutIndent = 12f;

    private string _feedbackMessage = "";
    private double _feedbackTime;
    private bool _showFixtureAdvanced;
    private bool _showColorAdvanced;
    private bool _showBeatAdvanced;
    private bool _showBeatOffset;
    private bool _showClipProgressOffset;
    private bool _showAudioBrightnessAdvanced;
    private bool _showMotionAdvanced;
    private bool _showMotionOffset;
    private bool _showFannedLaserAdvanced;

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
        DrawFixtureSection();
        EditorGUILayout.Space(6f);
        DrawColorSection();
        EditorGUILayout.Space(6f);
        DrawMotionSection();
        EditorGUILayout.Space(6f);
        DrawSpreadSection();
    }

    private void DrawFixtureSection()
    {
        SerializedProperty lightModeProp = serializedObject.FindProperty("lightMode");
        StageLightMode lightMode = (StageLightMode)lightModeProp.enumValueIndex;

        EditorGUILayout.LabelField("Fixture", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(lightModeProp);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightRange"));

        if (lightMode == StageLightMode.VolumetricSpot || lightMode == StageLightMode.Spot)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("beamAngle"));
        }

        _showFixtureAdvanced = DrawIndentedFoldout(_showFixtureAdvanced, "Advanced");
        if (_showFixtureAdvanced)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (lightMode != StageLightMode.Point)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("softness"));

                if (lightMode == StageLightMode.VolumetricSpot || lightMode == StageLightMode.Spot)
                {
                    SerializedProperty scatterModeProp = serializedObject.FindProperty("enableScatterMode");
                    EditorGUILayout.PropertyField(scatterModeProp);
                    if (scatterModeProp.boolValue)
                    {
                        using (new EditorGUI.IndentLevelScope())
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("scatterTexture"));
                    }
                }

            }
        }

        if (lightMode == StageLightMode.FannedLaser)
        {
            _showFannedLaserAdvanced = DrawIndentedFoldout(_showFannedLaserAdvanced, "Fanned Laser");
            if (_showFannedLaserAdvanced)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fannedAngle"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fannedRoll"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fannedAngleCurve"));
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawColorSection()
    {
        SerializedProperty colorModeProp = serializedObject.FindProperty("colorSampleMode");
        ColorSampleMode colorMode = (ColorSampleMode)colorModeProp.enumValueIndex;
        RotationMode rotationMode = (RotationMode)serializedObject.FindProperty("rotationMode").enumValueIndex;

        EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightGradient"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("intensityMultiplier"));
        EditorGUILayout.PropertyField(colorModeProp);

        switch (colorMode)
        {
            case ColorSampleMode.AlongAudioSource:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sensitivity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothness"));
                break;

            case ColorSampleMode.BeatGradient:
                DrawBeatTimingFields();
                break;

            case ColorSampleMode.BeatSnap:
                DrawBeatTimingFields();
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("beatSnapColors"), true);
                }
                break;
        }

        _showColorAdvanced = DrawIndentedFoldout(_showColorAdvanced, "Color Advanced");
        if (_showColorAdvanced)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("beamLengthGradient"));
            }
        }

        bool hasAdvancedColor = colorMode == ColorSampleMode.BeatGradient ||
                                colorMode == ColorSampleMode.BeatSnap;

        if (hasAdvancedColor)
        {
            _showBeatAdvanced = DrawIndentedFoldout(_showBeatAdvanced, "Beat Advanced");
            if (_showBeatAdvanced)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("beatPhaseOffset"));
                    if (colorMode == ColorSampleMode.BeatSnap)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatSnapTransitionTime"));
                }
            }

            _showBeatOffset = DrawIndentedFoldout(_showBeatOffset, "Beat Group / Light Offset");
            if (_showBeatOffset)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawBeatOffsetFields();
                }
            }
        }

        bool showClipProgressDelay = colorMode == ColorSampleMode.ClipProgress &&
                                     !HasMotionCycle(rotationMode) &&
                                     rotationMode != RotationMode.FreezeFrame;
        if (showClipProgressDelay)
        {
            _showClipProgressOffset = DrawIndentedFoldout(_showClipProgressOffset, "Clip Progress Group / Light Offset");
            if (_showClipProgressOffset)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawPerUnitMotionDelayFields();
                }
            }
        }

        DrawAudioBrightnessSection();
        EditorGUILayout.EndVertical();
    }

    private void DrawBeatTimingFields()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bpm"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatTimeRef"));
    }

    private void DrawAudioBrightnessSection()
    {
        SerializedProperty useAudioProp = serializedObject.FindProperty("useAudioAnalyzerBrightness");

        EditorGUILayout.PropertyField(useAudioProp);
        if (!useAudioProp.boolValue)
            return;

        _showAudioBrightnessAdvanced = DrawIndentedFoldout(_showAudioBrightnessAdvanced, "Audio Analyzer Brightness");
        if (!_showAudioBrightnessAdvanced)
            return;

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioBeatLightInterval"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioBeatIndices"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioBrightnessOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioBrightnessMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("audioBrightnessLerp"));
        }
    }

    private void DrawMotionSection()
    {
        SerializedProperty rotationModeProp = serializedObject.FindProperty("rotationMode");
        RotationMode rotationMode = (RotationMode)rotationModeProp.enumValueIndex;
        bool hasMotionCycle = HasMotionCycle(rotationMode);

        EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(rotationModeProp);

        switch (rotationMode)
        {
            case RotationMode.Static:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("staticAngleOffset"));
                break;

            case RotationMode.Target:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("trackingTarget"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("staticAngleOffset"));
                break;

            case RotationMode.FreezeFrame:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("freezeUseClipGradient"));
                break;

            default:
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationRange"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("staticAngleOffset"));
                break;
        }

        if (hasMotionCycle && rotationMode != RotationMode.Random)
        {
            _showMotionOffset = DrawIndentedFoldout(_showMotionOffset, "Group / Light Offset");
            if (_showMotionOffset)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawPerUnitMotionOffsetFields();
                }
            }
        }

        if (hasMotionCycle)
        {
            _showMotionAdvanced = DrawIndentedFoldout(_showMotionAdvanced, "Advanced");
            if (_showMotionAdvanced)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("cyclePauseTime"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("animationOffset"));
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSpreadSection()
    {
        EditorGUILayout.LabelField("Spread", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadAngle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadArcRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadAngleCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadAngleCurveByIndex"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadPanCurve"));
        EditorGUILayout.EndVertical();
    }

    private void DrawPerUnitMotionDelayFields()
    {
        EditorGUILayout.LabelField("分組偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groupDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groupDelayFactor"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("組內偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDelayFactor"));
    }

    private void DrawPerUnitMotionOffsetFields()
    {
        EditorGUILayout.LabelField("分組偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groupDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groupDelayFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groupRotationRangeCurve"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("組內偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightDelayFactor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lightRotationRangeCurve"));
    }

    private void DrawBeatOffsetFields()
    {
        EditorGUILayout.LabelField("分組偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatGroupDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatGroupDelayFactor"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("組內偏移", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatLightDelayCurve"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("beatLightDelayFactor"));
    }

    private static bool DrawIndentedFoldout(bool expanded, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(FoldoutIndent);
            return EditorGUILayout.Foldout(expanded, label, true);
        }
    }

    private static bool HasMotionCycle(RotationMode rotationMode)
    {
        return rotationMode == RotationMode.Scan ||
               rotationMode == RotationMode.Circle ||
               rotationMode == RotationMode.VerticalSwing ||
               rotationMode == RotationMode.Random ||
               rotationMode == RotationMode.Cross;
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
        template.scatterTexture = clip.scatterTexture;
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
        template.fannedRoll = clip.fannedRoll;
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
