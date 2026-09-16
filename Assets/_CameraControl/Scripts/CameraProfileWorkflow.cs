using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

#if UNITY_EDITOR
using UnityEditor;
using System;
using System.IO;
using System.Linq;
#endif

[RequireComponent(typeof(CinemachineCamera))]
public class CameraProfileWorkflow : MonoBehaviour
{
    public enum BakeProfileType { General, Tracking, Dolly }
    public enum BakeSourceMode { ProceduralAnimationClip, SceneTransform }
    public enum SceneAnimationSource { AnimationClip, PlayableDirector }
    public enum ScenePlaybackMode { StandardParameters, DeterministicPose }

    public BakeProfileType bakeProfileType;
    public CameraProfileSO targetProfileSO;
    public string profileSavePath = "Assets/_CameraControl/GeneratedProfiles";
    public string outputProfileName = "";
    public BakeSourceMode bakeSourceMode;
    public AnimationClip sourceAnimationClip;
    public SceneAnimationSource sceneAnimationSource;
    [Tooltip("Standard Parameters 與一般 SO 共用解算；姿態驗證失敗時不寫入。Deterministic Pose 保留既有 Scene Bake 初始化。")]
    public ScenePlaybackMode scenePlaybackMode = ScenePlaybackMode.StandardParameters;
    [Tooltip("Animation Clip 的綁定根物件。留空使用來源相機。")]
    public Transform sceneAnimationRoot;
    [Tooltip("來源相機。留空使用掛載 Workflow 的 Cinemachine Camera；Timeline 模式會自動使用選定 Track 的綁定。")]
    public Transform animatedCameraTransform;
    public PlayableDirector scenePlayableDirector;
    [HideInInspector] public AnimationPlayableAsset selectedTimelineClip;
    [Tooltip("SO 回放使用的 Target。留空自動使用來源 Cinemachine Camera 的 Follow。")]
    public Transform sceneTrackingTarget;
    [Tooltip("進階：一般 Unity Camera 的鏡頭來源。Cinemachine 相機自動讀取自己的 Lens，不需填寫。")]
    public Camera sceneLensCamera;
    [Tooltip("無法找到輸出 Camera 時使用的畫幅比例。")]
    public float fallbackAspect = 16f / 9f;
    [Tooltip("把整段來源分成 N 等分；60 = 0–1 間共 61 個鍵，與來源秒數無關。")]
    [Range(2, 120)] public int sampleRate = 60;
    [Tooltip("要還原為 Animation Clip 的來源 SO，與烘焙目標分開指定。")]
    public CameraProfileSO restoreProfileSO;
    public string outputAnimName = "Restored_Camera_Anim";
    public string animSavePath = "Assets/RestoredAnimations";
    [HideInInspector] public string lastBakeReport;

    public void BakeCurvesToSO()
    {
#if UNITY_EDITOR
        CameraProfileSO baked = null;
        try
        {
            baked = CameraProfileBakeUtility.Build(this, out string report);
            if (targetProfileSO != null && targetProfileSO.GetType() != baked.GetType())
                throw new InvalidOperationException("Existing Profile 類型不同，請清空或指定相同類型的 SO。");
            if (targetProfileSO == null)
            {
                string folder = EnsureFolder(profileSavePath);
                string name = string.IsNullOrWhiteSpace(outputProfileName)
                    ? "Baked_" + bakeProfileType : outputProfileName;
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(name) + ".asset");
                AssetDatabase.CreateAsset(baked, path);
                Undo.RecordObject(this, "Assign Baked Camera Profile");
                targetProfileSO = baked;
                baked = null;
            }
            else
            {
                Undo.RecordObject(targetProfileSO, "Bake Camera Profile");
                // Keep the existing asset's name and tags.
                baked.name = targetProfileSO.name;
                baked.tags = targetProfileSO.tags;
                EditorUtility.CopySerialized(baked, targetProfileSO);
            }
            lastBakeReport = report;
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(targetProfileSO);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(targetProfileSO);
            Debug.Log(report, targetProfileSO);
            EditorUtility.DisplayDialog("烘焙完成", report, "確定");
        }
        catch (Exception e)
        {
            lastBakeReport = "烘焙未寫入資產：\n" + e.Message;
            Debug.LogException(e, this);
            EditorUtility.DisplayDialog("無法烘焙", lastBakeReport, "確定");
        }
        finally { if (baked != null) DestroyImmediate(baked); }
#endif
    }

    public void DeBakeSOToAnimationClip()
    {
#if UNITY_EDITOR
        AnimationClip clip = null;
        try
        {
            if (restoreProfileSO == null) throw new InvalidOperationException("請先指定 Restore Profile。");
            clip = CameraProfileBakeUtility.CreateAnimationClip(restoreProfileSO, sampleRate);
            string folder = EnsureFolder(animSavePath);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(outputAnimName) + ".anim");
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(clip);
            clip = null;
            EditorUtility.DisplayDialog("還原完成", "已輸出 0–1 秒的 Composer 參數 Animation Clip。\n" + path
                + (restoreProfileSO.scenePoseBaked ? "\n此 Clip 使用 Scene Transform 反算後的 Composer 參數；請在相機上設定正確的 Follow／LookAt Target。" : ""), "確定");
        }
        catch (Exception e) { EditorUtility.DisplayDialog("無法解凍", e.Message, "確定"); }
        finally { if (clip != null) DestroyImmediate(clip); }
#endif
    }

#if UNITY_EDITOR
    static string SafeName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        value = value.Trim().Trim('.');
        return string.IsNullOrEmpty(value) ? "CameraProfile" : value;
    }
    static string EnsureFolder(string folder)
    {
        folder = (folder ?? "").Trim().Replace('\\', '/').TrimEnd('/');
        var parts = folder.Split('/');
        if (parts[0] != "Assets" || parts.Any(p => string.IsNullOrWhiteSpace(p) || p == "." || p == ".."
            || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidOperationException("輸出資料夾必須位於 Assets 內，不能包含 .. 或無效字元。");
        string current = "Assets";
        foreach (var part in parts.Skip(1))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, part)))
                throw new IOException("無法建立資料夾：" + next);
            current = next;
        }
        return current;
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(CameraProfileWorkflow))]
public class CameraProfileWorkflowEditor : Editor
{
    bool advanced;

    GUIStyle SectionTitle
    {
        get
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.fontSize = 12;
            style.margin = new RectOffset(2, 2, 2, 6);
            return style;
        }
    }

    void Field(string name, string label, string tooltip) =>
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label, tooltip));

    static void Title(string text, GUIStyle style) => EditorGUILayout.LabelField(text, style);

    static bool ColoredButton(GUIContent content, Color color, params GUILayoutOption[] options)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = color;
        try { return GUILayout.Button(content, options); }
        finally { GUI.backgroundColor = previous; }
    }

    public override void OnInspectorGUI()
    {
        var w = (CameraProfileWorkflow)target;
        serializedObject.Update();

        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 168f;
        try
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("相機設定檔烘焙", new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold });
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Title("輸出設定", SectionTitle);
                Field("bakeProfileType", "Profile Type", "選擇要建立或覆寫的相機設定檔類型。");
                Field("targetProfileSO", "Existing Profile", "可留空以自動建立新 SO；指定時會覆寫相同類型的既有 SO。");
                if (serializedObject.FindProperty("targetProfileSO").objectReferenceValue == null)
                {
                    EditorGUI.indentLevel++;
                    Field("profileSavePath", "Save Folder", "新 SO 的儲存資料夾，必須位於 Assets 內。");
                    Field("outputProfileName", "Profile Name", "新 SO 的名稱；留空時會依類型自動命名。");
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(5);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Title("動畫來源", SectionTitle);
                var profileType = (CameraProfileWorkflow.BakeProfileType)serializedObject.FindProperty("bakeProfileType").enumValueIndex;
                var sourceModeProperty = serializedObject.FindProperty("bakeSourceMode");
                if (profileType == CameraProfileWorkflow.BakeProfileType.Dolly)
                {
                    sourceModeProperty.enumValueIndex = (int)CameraProfileWorkflow.BakeSourceMode.ProceduralAnimationClip;
                    EditorGUILayout.LabelField(new GUIContent("Source Mode", "Dolly 目前只支援從 procedural 元件動畫烘焙。"),
                        new GUIContent("Procedural Animation Clip"));
                }
                else
                    Field("bakeSourceMode", "Source Mode", "選擇讀取 procedural 元件動畫，或從 Timeline 的最終場景鏡位反解。");

                bool sceneMode = (CameraProfileWorkflow.BakeSourceMode)sourceModeProperty.enumValueIndex
                    == CameraProfileWorkflow.BakeSourceMode.SceneTransform;
                if (!sceneMode)
                    Field("sourceAnimationClip", "Animation Clip", "包含 Cinemachine procedural 元件參數動畫的來源 Clip。");
                else
                {
                    // Keep the legacy serialized option for compatibility, but Scene mode
                    // intentionally exposes only the Playable Director workflow.
                    serializedObject.FindProperty("sceneAnimationSource").enumValueIndex =
                        (int)CameraProfileWorkflow.SceneAnimationSource.PlayableDirector;
                    Field("scenePlayableDirector", "Playable Director", "包含相機 Animation Track 與 Target 動畫的 Playable Director。");
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();

                    TimelineClip[] choices = CameraProfileBakeUtility.GetTimelineClips(w.scenePlayableDirector);
                    SerializedProperty selectedProperty = serializedObject.FindProperty("selectedTimelineClip");
                    if (choices.Length == 0)
                    {
                        selectedProperty.objectReferenceValue = null;
                        EditorGUILayout.HelpBox("No Animation Track clips found in this Director.", MessageType.Warning);
                    }
                    else
                    {
                        int selected = Array.FindIndex(choices, c => c.asset == selectedProperty.objectReferenceValue);
                        if (selected < 0) selected = 0;
                        string[] labels = choices.Select(c => c.GetParentTrack().name + " / " + c.displayName
                            + "  [" + c.start.ToString("0.###") + "–" + c.end.ToString("0.###") + "s]").ToArray();
                        int next = EditorGUILayout.Popup(
                            new GUIContent("Timeline Clip", "選擇要烘焙的單一 Animation Track Clip。"), selected, labels);
                        selectedProperty.objectReferenceValue = choices[next].asset;

                        TimelineClip clip = choices[next];
                        EditorGUILayout.LabelField(new GUIContent("Clip Timing", "實際只會烘焙此 Clip 的長度，並套用 Clip In 與播放速度。"),
                            new GUIContent(clip.duration.ToString("0.###") + "s   |   In " + clip.clipIn.ToString("0.###")
                            + "   |   " + clip.timeScale.ToString("0.###") + "x"));
                    }

                    EditorGUILayout.Space(2);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(8f);
                        advanced = EditorGUILayout.Foldout(advanced,
                            new GUIContent("進階設定", "顯示通常可由系統自動辨識的相機、Target 與鏡頭設定。"), true);
                    }
                    if (advanced)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(14f);
                            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                            {
                                float advancedLabelWidth = EditorGUIUtility.labelWidth;
                                EditorGUIUtility.labelWidth = Mathf.Max(110f, advancedLabelWidth - 14f);
                                Field("scenePlaybackMode", "Playback Mode", "Standard Parameters：與舊 SO 使用相同參數解算與混合。Deterministic Pose：使用既有姿態初始化。兩者都必須通過回放驗證。");
                                EditorGUILayout.HelpBox("Standard Parameters 以目前相機系統的設定驗證：World Space Tracking、關閉 Dead Zone／Lookahead。General 若無法通過姿態驗證，請選 Deterministic Pose；系統不會自動切換模式。", MessageType.Info);
                                Field("animatedCameraTransform", "Source Camera", "選填。留空時會從所選 Animation Track 的綁定階層自動辨識相機。");
                                Field("sceneTrackingTarget", "Playback Target", "選填。留空時使用來源 Cinemachine Camera 的 Follow。");
                                Field("sceneLensCamera", "Lens Override", "通常不需要指定。一般 Unity Camera 可用它提供 FOV；Cinemachine 來源會使用自己的 Lens。");
                                Field("fallbackAspect", "Fallback Aspect", "找不到輸出 Camera 時，用於 Rotation Composer 取樣的畫面比例。");
                                EditorGUIUtility.labelWidth = advancedLabelWidth;
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(5);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Title("取樣設定", SectionTitle);
                Field("sampleRate", "Normalized Divisions", "將整段動畫標準化為 0–1 並切成 N 等分；60 會產生含頭尾共 61 個 keys。");
                int divisions = serializedObject.FindProperty("sampleRate").intValue;
                EditorGUILayout.LabelField(new GUIContent("Output Keys", "輸出的每條動畫曲線關鍵幀數量。"),
                    new GUIContent((divisions + 1) + " keys (0–1)"));
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(7);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (ColoredButton(new GUIContent("烘焙並驗證設定檔", "烘焙曲線、執行 Cinemachine 回放驗證，成功後才寫入 SO。"),
                    new Color(0.35f, 0.85f, 0.42f), GUILayout.Height(38)))
                {
                    w.BakeCurvesToSO();
                    serializedObject.Update();
                }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Title("動畫匯出", SectionTitle);
                Field("restoreProfileSO", "Restore Profile", "選擇要還原成 Composer 參數 Animation Clip 的 SO；不會使用或修改上方的烘焙目標。");
                Field("outputAnimName", "Animation Name", "輸出的 0–1 秒 procedural Animation Clip 名稱。");
                Field("animSavePath", "Save Folder", "輸出 Animation Clip 的 Assets 資料夾。");
                serializedObject.ApplyModifiedProperties();

                CameraProfileSO restoreProfile = serializedObject.FindProperty("restoreProfileSO").objectReferenceValue as CameraProfileSO;
                if (restoreProfile != null && restoreProfile.scenePoseBaked)
                    EditorGUILayout.HelpBox(
                        "Exports the inverse-solved Composer parameters. Assign the matching Follow / LookAt Target when using this clip.",
                        MessageType.Info);
                using (new EditorGUI.DisabledScope(restoreProfile == null))
                    if (ColoredButton(new GUIContent("還原為 Animation Clip", "將指定 SO 還原為標準化的 Composer 參數 Animation Clip。"),
                        new Color(0.95f, 0.35f, 0.35f), GUILayout.Height(30)))
                        w.DeBakeSOToAnimationClip();
            }
        }
        finally
        {
            EditorGUIUtility.labelWidth = oldLabelWidth;
        }
    }
}
#endif
