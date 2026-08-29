#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

[CustomEditor(typeof(CameraProfileAsset))]
public class CameraProfileAssetEditor : Editor
{
    private SerializedProperty _cameraProfileProp;
    private SerializedProperty _trackingTargetProp;
    private SerializedProperty _splineContainerProp;
    private SerializedProperty _blendModeProp;
    private SerializedProperty _crossFadeBlurMaxIntensityProp;
    private SerializedProperty _crossFadeAlphaTimingProp;
    private SerializedProperty _motionCutAxisProp;
    private SerializedProperty _motionCutOutStrengthProp;
    private SerializedProperty _motionCutInStrengthProp;
    private SerializedProperty _reverseMotionCutInStrengthProp;
    private SerializedProperty _motionCutCurveProp;
    private SerializedProperty _motionCutRollAngleProp;
    private SerializedProperty _motionCutRollCurveProp;
    private SerializedProperty _enableNoiseProp;
    private SerializedProperty _noiseProfileProp;
    private SerializedProperty _noiseAmplitudeProp;
    private SerializedProperty _noiseFrequencyProp;
    private SerializedProperty _reversePlaybackProp;
    private SerializedProperty _mirrorXProp;
    private SerializedProperty _mirrorYProp;
    private SerializedProperty _mirrorZProp;
    private SerializedProperty _useFixedPlaybackSpeedProp;
    private SerializedProperty _fixedPlaybackSpeedProp;
    private SerializedProperty _fovBiasProp;
    private SerializedProperty _posDistanceBiasProp;
    private SerializedProperty _posTargetOffsetXBiasProp;
    private SerializedProperty _posTargetOffsetYBiasProp;
    private SerializedProperty _posTargetOffsetZBiasProp;
    private SerializedProperty _followOffsetXBiasProp;
    private SerializedProperty _followOffsetYBiasProp;
    private SerializedProperty _followOffsetZBiasProp;
    private SerializedProperty _splinePositionBiasProp;
    private SerializedProperty _rotTargetOffsetXBiasProp;
    private SerializedProperty _rotTargetOffsetYBiasProp;
    private SerializedProperty _rotTargetOffsetZBiasProp;

    private void OnEnable()
    {
        _cameraProfileProp = serializedObject.FindProperty("cameraProfile");
        _trackingTargetProp = serializedObject.FindProperty("trackingTarget");
        _splineContainerProp = serializedObject.FindProperty("splineContainer");
        _blendModeProp = serializedObject.FindProperty("blendMode");
        _crossFadeBlurMaxIntensityProp = serializedObject.FindProperty("crossFadeBlurMaxIntensity");
        _crossFadeAlphaTimingProp = serializedObject.FindProperty("crossFadeAlphaTiming");
        _motionCutAxisProp = serializedObject.FindProperty("motionCutAxis");
        _motionCutOutStrengthProp = serializedObject.FindProperty("motionCutOutStrength");
        _motionCutInStrengthProp = serializedObject.FindProperty("motionCutInStrength");
        _reverseMotionCutInStrengthProp = serializedObject.FindProperty("reverseMotionCutInStrength");
        _motionCutCurveProp = serializedObject.FindProperty("motionCutCurve");
        _motionCutRollAngleProp = serializedObject.FindProperty("motionCutRollAngle");
        _motionCutRollCurveProp = serializedObject.FindProperty("motionCutRollCurve");
        _enableNoiseProp = serializedObject.FindProperty("enableNoise");
        _noiseProfileProp = serializedObject.FindProperty("noiseProfile");
        _noiseAmplitudeProp = serializedObject.FindProperty("noiseAmplitude");
        _noiseFrequencyProp = serializedObject.FindProperty("noiseFrequency");
        _reversePlaybackProp = serializedObject.FindProperty("reversePlayback");
        _mirrorXProp = serializedObject.FindProperty("mirrorX");
        _mirrorYProp = serializedObject.FindProperty("mirrorY");
        _mirrorZProp = serializedObject.FindProperty("mirrorZ");
        _useFixedPlaybackSpeedProp = serializedObject.FindProperty("useFixedPlaybackSpeed");
        _fixedPlaybackSpeedProp = serializedObject.FindProperty("fixedPlaybackSpeed");
        _fovBiasProp = serializedObject.FindProperty("fovBias");
        _posDistanceBiasProp = serializedObject.FindProperty("posDistanceBias");
        _posTargetOffsetXBiasProp = serializedObject.FindProperty("posTargetOffsetXBias");
        _posTargetOffsetYBiasProp = serializedObject.FindProperty("posTargetOffsetYBias");
        _posTargetOffsetZBiasProp = serializedObject.FindProperty("posTargetOffsetZBias");
        _followOffsetXBiasProp = serializedObject.FindProperty("followOffsetXBias");
        _followOffsetYBiasProp = serializedObject.FindProperty("followOffsetYBias");
        _followOffsetZBiasProp = serializedObject.FindProperty("followOffsetZBias");
        _splinePositionBiasProp = serializedObject.FindProperty("splinePositionBias");
        _rotTargetOffsetXBiasProp = serializedObject.FindProperty("rotTargetOffsetXBias");
        _rotTargetOffsetYBiasProp = serializedObject.FindProperty("rotTargetOffsetYBias");
        _rotTargetOffsetZBiasProp = serializedObject.FindProperty("rotTargetOffsetZBias");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCameraProfilePicker();

        CameraProfileSO currentProfile = _cameraProfileProp != null
            ? _cameraProfileProp.objectReferenceValue as CameraProfileSO
            : null;

        EditorGUILayout.Space(6);

        if (_trackingTargetProp != null)
        {
            EditorGUILayout.PropertyField(_trackingTargetProp);
        }

        if (_blendModeProp != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(
                _blendModeProp,
                new GUIContent("Blend Mode")
            );

            if (_blendModeProp.enumValueIndex ==
                (int)CameraProfileBlendMode.CrossFadeBlur)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    "Cross Fade Blur Settings",
                    EditorStyles.boldLabel
                );
                EditorGUILayout.PropertyField(
                    _crossFadeBlurMaxIntensityProp,
                    new GUIContent("Blur Max Intensity")
                );
                EditorGUILayout.PropertyField(
                    _crossFadeAlphaTimingProp,
                    new GUIContent("Alpha Timing")
                );
            }
            else if (_blendModeProp.enumValueIndex ==
                (int)CameraProfileBlendMode.MotionCut)
            {
                DrawMotionCutSettings();
            }
        }

        DrawPlaybackSettings();

        if (currentProfile is DollyProfileSO && _splineContainerProp != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Dolly Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_splineContainerProp);
        }

        if (currentProfile is GeneralProfileSO)
        {
            DrawGeneralBiasSettings();
        }
        else if (currentProfile is TrackingProfileSO)
        {
            DrawTrackingBiasSettings();
        }
        else if (currentProfile is DollyProfileSO)
        {
            DrawDollyBiasSettings();
        }

        DrawNoiseSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawNoiseSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Handheld Noise", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(
            _enableNoiseProp,
            new GUIContent("Enable Noise")
        );

        if (_enableNoiseProp != null && _enableNoiseProp.boolValue)
        {
            DrawNoiseProfileField();
            EditorGUILayout.PropertyField(
                _noiseAmplitudeProp,
                new GUIContent("Amplitude")
            );
            EditorGUILayout.PropertyField(
                _noiseFrequencyProp,
                new GUIContent("Frequency")
            );

            if (_noiseProfileProp == null ||
                _noiseProfileProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "請指定 Noise Profile，否則不會產生手持晃動。",
                    MessageType.Warning
                );
            }
        }

        EditorGUI.indentLevel--;
    }

    private void DrawNoiseProfileField()
    {
        if (_noiseProfileProp == null)
            return;

        EditorGUI.BeginChangeCheck();

        NoiseSettings selectedProfile = EditorGUILayout.ObjectField(
            new GUIContent("Noise Profile"),
            _noiseProfileProp.objectReferenceValue as NoiseSettings,
            typeof(NoiseSettings),
            false
        ) as NoiseSettings;

        if (EditorGUI.EndChangeCheck())
        {
            _noiseProfileProp.objectReferenceValue = selectedProfile;
        }
    }

    private void DrawMotionCutSettings()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            "Motion Cut Settings",
            EditorStyles.boldLabel
        );

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("Position", EditorStyles.miniBoldLabel);

        if (_motionCutAxisProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutAxisProp,
                new GUIContent("Axis")
            );
        }

        if (_motionCutOutStrengthProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutOutStrengthProp,
                new GUIContent("Out Strength")
            );
        }

        if (_motionCutInStrengthProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutInStrengthProp,
                new GUIContent("In Strength")
            );
        }

        if (_reverseMotionCutInStrengthProp != null)
        {
            EditorGUILayout.PropertyField(
                _reverseMotionCutInStrengthProp,
                new GUIContent("Reverse In Strength")
            );
        }

        if (_motionCutCurveProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutCurveProp,
                new GUIContent("Position Curve")
            );
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Roll", EditorStyles.miniBoldLabel);

        if (_motionCutRollAngleProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutRollAngleProp,
                new GUIContent("Roll Angle")
            );
        }

        if (_motionCutRollCurveProp != null)
        {
            EditorGUILayout.PropertyField(
                _motionCutRollCurveProp,
                new GUIContent("Roll Curve")
            );
        }

        EditorGUI.indentLevel--;
    }

    private void DrawGeneralBiasSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("General Bias", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        DrawLensBiasFields();
        EditorGUILayout.Space(2);

        EditorGUILayout.PropertyField(
            _posDistanceBiasProp,
            new GUIContent("Pos Distance Bias")
        );

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Position Composer Target Offset", EditorStyles.miniBoldLabel);

        EditorGUILayout.PropertyField(
            _posTargetOffsetXBiasProp,
            new GUIContent("Pos Target Offset X Bias")
        );
        EditorGUILayout.PropertyField(
            _posTargetOffsetYBiasProp,
            new GUIContent("Pos Target Offset Y Bias")
        );
        EditorGUILayout.PropertyField(
            _posTargetOffsetZBiasProp,
            new GUIContent("Pos Target Offset Z Bias")
        );

        EditorGUILayout.Space(2);
        DrawRotationTargetOffsetBiasFields();

        EditorGUI.indentLevel--;
    }

    private void DrawPlaybackSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Playback Options", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        if (_reversePlaybackProp != null)
        {
            EditorGUILayout.PropertyField(
                _reversePlaybackProp,
                new GUIContent("Reverse Playback")
            );
        }

        DrawFixedPlaybackSpeedSettings();

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Dynamic Mirror", EditorStyles.miniBoldLabel);

        DrawMirrorToggleRow();

        EditorGUI.indentLevel--;
    }

    private void DrawFixedPlaybackSpeedSettings()
    {
        if (_useFixedPlaybackSpeedProp == null)
            return;

        EditorGUILayout.PropertyField(
            _useFixedPlaybackSpeedProp,
            new GUIContent("Use Fixed Playback Speed")
        );

        if (!_useFixedPlaybackSpeedProp.boolValue || _fixedPlaybackSpeedProp == null)
            return;

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(
            _fixedPlaybackSpeedProp,
            new GUIContent("Playback Speed")
        );

        if (_fixedPlaybackSpeedProp.floatValue < 0.001f)
        {
            _fixedPlaybackSpeedProp.floatValue = 0.001f;
        }

        EditorGUI.indentLevel--;
    }

    private void DrawMirrorToggleRow()
    {
        Rect rowRect = EditorGUILayout.GetControlRect(
            false,
            EditorGUIUtility.singleLineHeight
        );

        rowRect = EditorGUI.IndentedRect(rowRect);

        const float toggleWidth = 32f;
        const float toggleGap = 4f;

        rowRect.width = toggleWidth;
        DrawMirrorToggle(rowRect, _mirrorXProp, "X");

        rowRect.x += toggleWidth + toggleGap;
        DrawMirrorToggle(rowRect, _mirrorYProp, "Y");

        rowRect.x += toggleWidth + toggleGap;
        DrawMirrorToggle(rowRect, _mirrorZProp, "Z");
    }

    private static void DrawMirrorToggle(
        Rect rect,
        SerializedProperty property,
        string label)
    {
        if (property == null)
            return;

        property.boolValue = GUI.Toggle(
            rect,
            property.boolValue,
            label,
            EditorStyles.miniButton
        );
    }

    private void DrawTrackingBiasSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tracking Bias", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        DrawLensBiasFields();
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("Cinemachine Follow Offset", EditorStyles.miniBoldLabel);

        EditorGUILayout.PropertyField(
            _followOffsetXBiasProp,
            new GUIContent("Follow Offset X Bias")
        );
        EditorGUILayout.PropertyField(
            _followOffsetYBiasProp,
            new GUIContent("Follow Offset Y Bias")
        );
        EditorGUILayout.PropertyField(
            _followOffsetZBiasProp,
            new GUIContent("Follow Offset Z Bias")
        );

        EditorGUILayout.Space(2);
        DrawRotationTargetOffsetBiasFields();

        EditorGUI.indentLevel--;
    }

    private void DrawDollyBiasSettings()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Dolly Bias", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        DrawLensBiasFields();
        EditorGUILayout.Space(2);

        EditorGUILayout.PropertyField(
            _splinePositionBiasProp,
            new GUIContent("Spline Position Bias")
        );

        EditorGUILayout.Space(2);
        DrawRotationTargetOffsetBiasFields();

        EditorGUI.indentLevel--;
    }

    private void DrawLensBiasFields()
    {
        EditorGUILayout.LabelField("Lens", EditorStyles.miniBoldLabel);

        EditorGUILayout.PropertyField(
            _fovBiasProp,
            new GUIContent("FOV Bias")
        );
    }

    private void DrawRotationTargetOffsetBiasFields()
    {
        EditorGUILayout.LabelField("Rotation Composer Target Offset", EditorStyles.miniBoldLabel);

        EditorGUILayout.PropertyField(
            _rotTargetOffsetXBiasProp,
            new GUIContent("Rot Target Offset X Bias")
        );
        EditorGUILayout.PropertyField(
            _rotTargetOffsetYBiasProp,
            new GUIContent("Rot Target Offset Y Bias")
        );
        EditorGUILayout.PropertyField(
            _rotTargetOffsetZBiasProp,
            new GUIContent("Rot Target Offset Z Bias")
        );
    }

    private void DrawCameraProfilePicker()
    {
        if (_cameraProfileProp == null)
        {
            EditorGUILayout.HelpBox(
                "找不到 cameraProfile 欄位。請確認 CameraProfileAsset.cs 裡有 public CameraProfileSO cameraProfile。",
                MessageType.Error
            );
            return;
        }

        CameraProfileSO currentProfile =
            _cameraProfileProp.objectReferenceValue as CameraProfileSO;

        EditorGUILayout.LabelField("Camera Profile", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            string buttonLabel = currentProfile != null
                ? $"{currentProfile.name}  ({GetProfileTypeName(currentProfile)})"
                : "None  (點擊搜尋 Camera Profile)";

            Rect buttonRect = EditorGUILayout.GetControlRect(
                false,
                24f,
                GUILayout.ExpandWidth(true)
            );

            if (GUI.Button(buttonRect, buttonLabel, EditorStyles.popup))
            {
                PopupWindow.Show(
                    buttonRect,
                    new CameraProfilePickerPopup(
                        currentProfile,
                        selectedProfile =>
                        {
                            if (target == null)
                                return;

                            serializedObject.Update();

                            Undo.RecordObject(target, "Set Camera Profile");
                            _cameraProfileProp.objectReferenceValue = selectedProfile;

                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(target);

                            Repaint();
                        }
                    )
                );
            }

            GUI.enabled = currentProfile != null;

            if (GUILayout.Button("Ping", GUILayout.Width(46f)))
            {
                EditorGUIUtility.PingObject(currentProfile);
                Selection.activeObject = currentProfile;
            }

            if (GUILayout.Button("X", GUILayout.Width(26f)))
            {
                Undo.RecordObject(target, "Clear Camera Profile");
                _cameraProfileProp.objectReferenceValue = null;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Repaint();
            }

            GUI.enabled = true;
        }

        DrawCurrentProfileTags(currentProfile);
    }

    private static void DrawCurrentProfileTags(CameraProfileSO profile)
    {
        if (profile == null || profile.tags == null || profile.tags.Count == 0)
            return;

        EditorGUILayout.Space(2);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Tags:", GUILayout.Width(38f));

            foreach (CameraTagSO tag in profile.tags)
            {
                if (tag == null)
                    continue;

                GUILayout.Label(
                    "#" + tag.name,
                    EditorStyles.miniButton,
                    GUILayout.ExpandWidth(false)
                );
            }
        }
    }

    private static string GetProfileTypeName(CameraProfileSO profile)
    {
        if (profile is GeneralProfileSO)
            return "General";

        if (profile is TrackingProfileSO)
            return "Tracking";

        if (profile is DollyProfileSO)
            return "Dolly";

        return "Unknown";
    }
}

public class CameraProfilePickerPopup : PopupWindowContent
{
    private enum ProfileTypeFilter
    {
        All,
        General,
        Tracking,
        Dolly
    }

    private readonly CameraProfileSO _initialProfile;
    private readonly Action<CameraProfileSO> _onProfilePicked;

    private readonly List<CameraProfileSO> _allProfiles = new List<CameraProfileSO>();
    private readonly List<CameraTagSO> _allTags = new List<CameraTagSO>();
    private readonly List<CameraTagSO> _selectedTags = new List<CameraTagSO>();

    private CameraProfileSO _selectedProfile;

    private ProfileTypeFilter _typeFilter = ProfileTypeFilter.All;

    private string _searchText = "";
    private Vector2 _resultScroll;
    private Vector2 _tagScroll;

    private Editor _previewEditor;
    private CameraProfileSO _previewEditorTarget;

    private GUIStyle _rowStyle;
    private GUIStyle _selectedRowStyle;
    private GUIStyle _smallMutedLabelStyle;
    private GUIStyle _centeredPreviewLabelStyle;

    public CameraProfilePickerPopup(
        CameraProfileSO initialProfile,
        Action<CameraProfileSO> onProfilePicked)
    {
        _initialProfile = initialProfile;
        _selectedProfile = initialProfile;
        _onProfilePicked = onProfilePicked;
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(780f, 660f);
    }

    public override void OnOpen()
    {
        RefreshDatabase();
        EditorApplication.update += RepaintPopup;
    }

    public override void OnClose()
    {
        EditorApplication.update -= RepaintPopup;
        DestroyPreviewEditor();
    }

    public override void OnGUI(Rect rect)
    {
        EnsureStyles();

        DrawHeader();
        DrawSearchToolbar();
        DrawTypeFilter();
        DrawTagFilter();
        DrawResultList();
        DrawPreviewArea();
        DrawFooter();
    }

    private void RepaintPopup()
    {
        if (editorWindow != null)
        {
            editorWindow.Repaint();
        }
    }

    private void EnsureStyles()
    {
        if (_rowStyle == null)
        {
            _rowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(2, 2, 2, 2)
            };
        }

        if (_selectedRowStyle == null)
        {
            _selectedRowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 5, 5),
                margin = new RectOffset(2, 2, 2, 2),
                fontStyle = FontStyle.Bold
            };
        }

        if (_smallMutedLabelStyle == null)
        {
            _smallMutedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = false
            };
        }

        if (_centeredPreviewLabelStyle == null)
        {
            _centeredPreviewLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                "Camera Profile Picker",
                EditorStyles.boldLabel
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
            {
                RefreshDatabase();
            }
        }
    }

    private void DrawSearchToolbar()
    {
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUIStyle searchStyle =
                GUI.skin.FindStyle("ToolbarSearchTextField") ??
                GUI.skin.FindStyle("ToolbarSeachTextField") ??
                EditorStyles.textField;

            _searchText = GUILayout.TextField(
                _searchText,
                searchStyle,
                GUILayout.Height(20f),
                GUILayout.ExpandWidth(true)
            );

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                _searchText = "";
                GUI.FocusControl(null);
            }
        }
    }

    private void DrawTypeFilter()
    {
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Type", GUILayout.Width(36f));

            _typeFilter = (ProfileTypeFilter)GUILayout.Toolbar(
                (int)_typeFilter,
                new[] { "All", "General", "Tracking", "Dolly" },
                GUILayout.Height(22f)
            );
        }
    }

    private void DrawTagFilter()
    {
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Tags", GUILayout.Width(36f));

            string label = _selectedTags.Count == 0
                ? "All Tags"
                : $"{_selectedTags.Count} Tags Selected";

            if (GUILayout.Button(label, EditorStyles.popup, GUILayout.Width(200f)))
            {
                ShowTagMenu();
            }

            GUI.enabled = _selectedTags.Count > 0;

            if (GUILayout.Button("Clear Tags", GUILayout.Width(86f)))
            {
                _selectedTags.Clear();
            }

            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                "Tag 條件：交集（必須同時符合所有選取的 Tag）",
                EditorStyles.miniLabel,
                GUILayout.Width(260f)
            );
        }

        DrawTagChips();
    }

    private void ShowTagMenu()
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(
            new GUIContent("Clear All Tags"),
            _selectedTags.Count == 0,
            () => _selectedTags.Clear()
        );

        menu.AddSeparator("");

        foreach (CameraTagSO tag in _allTags)
        {
            if (tag == null)
                continue;

            CameraTagSO capturedTag = tag;

            menu.AddItem(
                new GUIContent(capturedTag.name),
                _selectedTags.Contains(capturedTag),
                () => ToggleTag(capturedTag)
            );
        }

        menu.ShowAsContext();
    }

    private void DrawTagChips()
    {
        if (_allTags.Count == 0)
            return;

        _tagScroll = EditorGUILayout.BeginScrollView(
            _tagScroll,
            false,
            false,
            GUILayout.Height(32f)
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            bool allSelected = _selectedTags.Count == 0;

            Color originalColor = GUI.backgroundColor;

            GUI.backgroundColor = allSelected
                ? new Color(0.55f, 0.75f, 1f, 1f)
                : originalColor;

            if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
            {
                _selectedTags.Clear();
            }

            GUI.backgroundColor = originalColor;

            foreach (CameraTagSO tag in _allTags)
            {
                if (tag == null)
                    continue;

                bool isSelected = _selectedTags.Contains(tag);

                GUI.backgroundColor = isSelected
                    ? new Color(0.55f, 0.75f, 1f, 1f)
                    : originalColor;

                if (GUILayout.Button("#" + tag.name, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    ToggleTag(tag);
                }

                GUI.backgroundColor = originalColor;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ToggleTag(CameraTagSO tag)
    {
        if (tag == null)
            return;

        if (_selectedTags.Contains(tag))
        {
            _selectedTags.Remove(tag);
        }
        else
        {
            _selectedTags.Add(tag);
        }
    }

    private void DrawResultList()
    {
        List<CameraProfileSO> filteredProfiles = GetFilteredProfiles();

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"Search Results ({filteredProfiles.Count})",
                EditorStyles.boldLabel
            );

            GUILayout.FlexibleSpace();

            if (_selectedTags.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "Tag Match: AND",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(100f)
                );
            }
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(210f));

        _resultScroll = EditorGUILayout.BeginScrollView(_resultScroll);

        if (filteredProfiles.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "沒有找到符合條件的 Camera Profile。",
                MessageType.Info
            );
        }
        else
        {
            foreach (CameraProfileSO profile in filteredProfiles)
            {
                DrawProfileResult(profile);
            }
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawProfileResult(CameraProfileSO profile)
    {
        if (profile == null)
            return;

        bool isSelected = _selectedProfile == profile;

        Rect rowRect = GUILayoutUtility.GetRect(
            10f,
            44f,
            GUILayout.ExpandWidth(true),
            GUILayout.Height(44f)
        );

        Color originalColor = GUI.backgroundColor;

        if (isSelected)
        {
            GUI.backgroundColor = new Color(0.45f, 0.7f, 1f, 1f);
        }

        GUI.Box(rowRect, GUIContent.none, isSelected ? _selectedRowStyle : _rowStyle);

        GUI.backgroundColor = originalColor;

        Rect nameRect = new Rect(
            rowRect.x + 8f,
            rowRect.y + 5f,
            rowRect.width - 16f,
            18f
        );

        Rect metaRect = new Rect(
            rowRect.x + 8f,
            rowRect.y + 23f,
            rowRect.width - 16f,
            16f
        );

        GUI.Label(
            nameRect,
            profile.name,
            isSelected ? EditorStyles.boldLabel : EditorStyles.label
        );

        string meta = string.IsNullOrEmpty(GetTagText(profile))
            ? $"[{GetProfileTypeName(profile)}]"
            : $"[{GetProfileTypeName(profile)}]    {GetTagText(profile)}";

        GUI.Label(metaRect, meta, _smallMutedLabelStyle);

        EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

        Event evt = Event.current;

        if (evt.type == EventType.MouseDown && rowRect.Contains(evt.mousePosition))
        {
            if (_selectedProfile == profile)
            {
                PickProfile(profile);
            }
            else
            {
                SelectProfileForPreview(profile);
            }

            evt.Use();
        }
    }

    private void DrawPreviewArea()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(
            10f,
            250f,
            GUILayout.ExpandWidth(true),
            GUILayout.Height(250f)
        );

        GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);

        if (_selectedProfile == null)
        {
            DrawCenteredPreviewText(
                previewRect,
                "先點選一個 Camera Profile 進行預覽。\n再次點選同一個 Profile 會完成選擇。"
            );
            return;
        }

        EnsurePreviewEditor();

        Rect titleRect = new Rect(
            previewRect.x + 8f,
            previewRect.y + 6f,
            Mathf.Max(1f, previewRect.width - 16f),
            22f
        );

        GUI.Label(
            titleRect,
            $"{_selectedProfile.name}  ({GetProfileTypeName(_selectedProfile)})",
            EditorStyles.boldLabel
        );

        Rect actualPreviewRect = new Rect(
            previewRect.x + 8f,
            previewRect.y + 32f,
            previewRect.width - 16f,
            previewRect.height - 40f
        );

        if (actualPreviewRect.width <= 1f || actualPreviewRect.height <= 1f)
            return;

        if (_previewEditor == null || !_previewEditor.HasPreviewGUI())
        {
            DrawCenteredPreviewText(
                actualPreviewRect,
                "這個 Profile 沒有可用的預覽畫面。"
            );
            return;
        }

        try
        {
            _previewEditor.OnPreviewGUI(actualPreviewRect, EditorStyles.helpBox);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            DrawCenteredPreviewText(
                actualPreviewRect,
                "預覽產生錯誤，請檢查 CameraProfileSOEditor 的 Preview 設定。"
            );
        }
    }

    private void DrawCenteredPreviewText(Rect rect, string text)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;

        EditorGUI.LabelField(
            rect,
            text,
            _centeredPreviewLabelStyle
        );
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _selectedProfile != null;

            if (GUILayout.Button("選擇此 Profile", GUILayout.Height(28f)))
            {
                PickProfile(_selectedProfile);
            }

            if (GUILayout.Button("Ping", GUILayout.Width(70f), GUILayout.Height(28f)))
            {
                EditorGUIUtility.PingObject(_selectedProfile);
                Selection.activeObject = _selectedProfile;
            }

            GUI.enabled = true;

            if (GUILayout.Button("Cancel", GUILayout.Width(80f), GUILayout.Height(28f)))
            {
                editorWindow.Close();
            }
        }

        EditorGUILayout.HelpBox(
            "操作方式：第一次點選 Profile 只會更新下方預覽；第二次點選同一個 Profile 會套用到 Clip。Tag 篩選採交集條件。",
            MessageType.None
        );
    }

    private void SelectProfileForPreview(CameraProfileSO profile)
    {
        if (_selectedProfile == profile)
            return;

        _selectedProfile = profile;
        DestroyPreviewEditor();
    }

    private void PickProfile(CameraProfileSO profile)
    {
        if (profile == null)
            return;

        _onProfilePicked?.Invoke(profile);
        editorWindow.Close();
    }

    private void EnsurePreviewEditor()
    {
        if (_selectedProfile == null)
        {
            DestroyPreviewEditor();
            return;
        }

        if (_previewEditor != null && _previewEditorTarget == _selectedProfile)
            return;

        DestroyPreviewEditor();

        _previewEditorTarget = _selectedProfile;
        _previewEditor = Editor.CreateEditor(_selectedProfile);
    }

    private void DestroyPreviewEditor()
    {
        if (_previewEditor != null)
        {
            UnityEngine.Object.DestroyImmediate(_previewEditor);
            _previewEditor = null;
        }

        _previewEditorTarget = null;
    }

    private void RefreshDatabase()
    {
        _allProfiles.Clear();
        _allTags.Clear();

        HashSet<string> profileGuids = new HashSet<string>();

        AddGuids(profileGuids, AssetDatabase.FindAssets("t:CameraProfileSO"));
        AddGuids(profileGuids, AssetDatabase.FindAssets("t:GeneralProfileSO"));
        AddGuids(profileGuids, AssetDatabase.FindAssets("t:TrackingProfileSO"));
        AddGuids(profileGuids, AssetDatabase.FindAssets("t:DollyProfileSO"));

        foreach (string guid in profileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CameraProfileSO profile =
                AssetDatabase.LoadAssetAtPath<CameraProfileSO>(path);

            if (profile != null && !_allProfiles.Contains(profile))
            {
                _allProfiles.Add(profile);
            }
        }

        _allProfiles.Sort((a, b) =>
        {
            int typeCompare = string.Compare(
                GetProfileTypeName(a),
                GetProfileTypeName(b),
                StringComparison.OrdinalIgnoreCase
            );

            if (typeCompare != 0)
                return typeCompare;

            return string.Compare(
                a.name,
                b.name,
                StringComparison.OrdinalIgnoreCase
            );
        });

        string[] tagGuids = AssetDatabase.FindAssets("t:CameraTagSO");

        foreach (string guid in tagGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CameraTagSO tag =
                AssetDatabase.LoadAssetAtPath<CameraTagSO>(path);

            if (tag != null && !_allTags.Contains(tag))
            {
                _allTags.Add(tag);
            }
        }

        _allTags.Sort((a, b) =>
            string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase)
        );

        if (_selectedProfile == null && _initialProfile != null)
        {
            _selectedProfile = _initialProfile;
        }
    }

    private static void AddGuids(HashSet<string> set, string[] guids)
    {
        if (guids == null)
            return;

        foreach (string guid in guids)
        {
            set.Add(guid);
        }
    }

    private List<CameraProfileSO> GetFilteredProfiles()
    {
        string keyword = string.IsNullOrWhiteSpace(_searchText)
            ? ""
            : _searchText.Trim().ToLowerInvariant();

        return _allProfiles
            .Where(profile => profile != null)
            .Where(MatchesTypeFilter)
            .Where(MatchesTagFilter)
            .Where(profile => MatchesSearch(profile, keyword))
            .ToList();
    }

    private bool MatchesTypeFilter(CameraProfileSO profile)
    {
        switch (_typeFilter)
        {
            case ProfileTypeFilter.General:
                return profile is GeneralProfileSO;

            case ProfileTypeFilter.Tracking:
                return profile is TrackingProfileSO;

            case ProfileTypeFilter.Dolly:
                return profile is DollyProfileSO;

            default:
                return true;
        }
    }

    private bool MatchesTagFilter(CameraProfileSO profile)
    {
        if (_selectedTags.Count == 0)
            return true;

        if (profile.tags == null)
            return false;

        foreach (CameraTagSO selectedTag in _selectedTags)
        {
            if (selectedTag == null)
                continue;

            if (!profile.tags.Contains(selectedTag))
                return false;
        }

        return true;
    }

    private static bool MatchesSearch(CameraProfileSO profile, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
            return true;

        if (profile.name.ToLowerInvariant().Contains(keyword))
            return true;

        if (GetProfileTypeName(profile).ToLowerInvariant().Contains(keyword))
            return true;

        if (profile.tags != null)
        {
            foreach (CameraTagSO tag in profile.tags)
            {
                if (tag != null &&
                    tag.name.ToLowerInvariant().Contains(keyword))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetProfileTypeName(CameraProfileSO profile)
    {
        if (profile is GeneralProfileSO)
            return "General";

        if (profile is TrackingProfileSO)
            return "Tracking";

        if (profile is DollyProfileSO)
            return "Dolly";

        return "Unknown";
    }

    private static string GetTagText(CameraProfileSO profile)
    {
        if (profile == null || profile.tags == null || profile.tags.Count == 0)
            return "";

        List<string> tagNames = new List<string>();

        foreach (CameraTagSO tag in profile.tags)
        {
            if (tag != null)
                tagNames.Add("#" + tag.name);
        }

        return string.Join(" ", tagNames);
    }
}
#endif
