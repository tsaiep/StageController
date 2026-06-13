#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(CameraProfileSO), true)]
public class CameraProfileSOEditor : Editor
{
    private const float PreviewDuration = 2f;
    private const double PreviewRepaintInterval = 1.0 / 24.0;

    private CameraProfilePreviewRenderer _previewRenderer;

    private bool _isPlaying;
    private float _playbackTime;
    private double _lastSystemTime;
    private double _lastPreviewRepaintTime;
    private bool _showRawCurves;

    private void OnEnable()
    {
        _previewRenderer = new CameraProfilePreviewRenderer();

        _lastSystemTime = EditorApplication.timeSinceStartup;
        _lastPreviewRepaintTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += UpdatePlayback;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdatePlayback;

        if (_previewRenderer != null)
        {
            _previewRenderer.Dispose();
            _previewRenderer = null;
        }
    }

    private void UpdatePlayback()
    {
        double currentSystemTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentSystemTime - _lastSystemTime);
        _lastSystemTime = currentSystemTime;

        if (!_isPlaying)
            return;

        _playbackTime += deltaTime;

        if (_playbackTime > PreviewDuration)
        {
            _playbackTime %= PreviewDuration;
        }

        if (currentSystemTime - _lastPreviewRepaintTime >= PreviewRepaintInterval)
        {
            _lastPreviewRepaintTime = currentSystemTime;
            Repaint();
        }
    }

    public override bool HasPreviewGUI()
    {
        return true;
    }

    public override void OnInspectorGUI()
    {
        CameraProfileSO profile = (CameraProfileSO)target;

        serializedObject.Update();

        DrawTagSection(profile);
        DrawMainProperties();
        DrawRawCurvesFoldout();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTagSection(CameraProfileSO profile)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("當前綁定的標籤：", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < profile.tags.Count; i++)
            {
                CameraTagSO tag = profile.tags[i];

                if (tag == null)
                    continue;

                Color originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.1f, 0.4f, 0.2f);

                GUIStyle tagStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    normal = { textColor = Color.white }
                };

                if (GUILayout.Button(tag.name + "  X", tagStyle, GUILayout.ExpandWidth(false)))
                {
                    Undo.RecordObject(profile, "Remove Camera Tag");
                    profile.tags.RemoveAt(i);
                    EditorUtility.SetDirty(profile);

                    GUI.backgroundColor = originalColor;
                    break;
                }

                GUI.backgroundColor = originalColor;
            }
        }

        EditorGUILayout.Space();

        Rect buttonRect = EditorGUILayout.GetControlRect(false, 25f);

        if (GUI.Button(buttonRect, "點擊搜尋並新增標籤...", EditorStyles.popup))
        {
            PopupWindow.Show(buttonRect, new TagSearchPopupContent(profile));
        }

        EditorGUILayout.Space();
        EditorGUILayout.Separator();
    }

    private void DrawMainProperties()
    {
        SerializedProperty fovProp = serializedObject.FindProperty("fovCurve");

        if (fovProp != null)
        {
            EditorGUILayout.PropertyField(fovProp);
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "編輯器效能防護：已自動隱藏烘焙高密度曲線。除非必要，請勿展開下方原始數據。",
            MessageType.Info
        );
    }

    private void DrawRawCurvesFoldout()
    {
        _showRawCurves = EditorGUILayout.Foldout(
            _showRawCurves,
            "展開原始曲線數據 (解鎖手動調教，極耗效能)",
            true
        );

        if (!_showRawCurves)
            return;

        EditorGUI.indentLevel++;

        SerializedProperty prop = serializedObject.GetIterator();

        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "m_Script" || prop.name == "tags" || prop.name == "fovCurve")
                    continue;

                EditorGUILayout.PropertyField(prop, true);
            }
            while (prop.NextVisible(false));
        }

        EditorGUI.indentLevel--;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        if (_previewRenderer == null || target == null)
            return;

        if (r.width <= 1f || r.height <= 1f)
            return;

        CameraProfileSO profile = (CameraProfileSO)target;

        float controlBarHeight = 28f;

        Rect previewRect = new Rect(
            r.x,
            r.y,
            r.width,
            Mathf.Max(1f, r.height - controlBarHeight)
        );

        Rect controlRect = new Rect(
            r.x,
            r.y + r.height - controlBarHeight,
            r.width,
            controlBarHeight
        );

        float normalizedTime = Mathf.Clamp01(_playbackTime / PreviewDuration);

        _previewRenderer.Render(previewRect, background, profile, normalizedTime);
        DrawPreviewOverlay(previewRect, profile, normalizedTime);
        DrawPreviewControls(controlRect, normalizedTime);
    }

    private void DrawPreviewOverlay(Rect previewRect, CameraProfileSO profile, float normalizedTime)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Rect labelBackground = new Rect(
            previewRect.x + 8f,
            previewRect.y + 8f,
            300f,
            56f
        );

        EditorGUI.DrawRect(labelBackground, new Color(0f, 0f, 0f, 0.32f));

        GUI.Label(
            new Rect(labelBackground.x + 8f, labelBackground.y + 4f, labelBackground.width - 16f, 18f),
            $"{GetProfileTypeName(profile)} Preview  ({Mathf.RoundToInt(normalizedTime * 100f)}%)",
            EditorStyles.whiteMiniLabel
        );

        GUI.Label(
            new Rect(labelBackground.x + 8f, labelBackground.y + 21f, labelBackground.width - 16f, 16f),
            $"FOV: {Mathf.RoundToInt(profile.fovCurve.Evaluate(normalizedTime))}",
            EditorStyles.whiteMiniLabel
        );

        GUI.Label(
            new Rect(labelBackground.x + 8f, labelBackground.y + 38f, labelBackground.width - 16f, 16f),
            GetPreviewTargetLabel(profile),
            EditorStyles.whiteMiniLabel
        );
    }

    private void DrawPreviewControls(Rect controlRect, float normalizedTime)
    {
        GUI.Box(controlRect, "", EditorStyles.toolbar);

        Rect playButtonRect = new Rect(
            controlRect.x + 5f,
            controlRect.y + 5f,
            78f,
            18f
        );

        Rect sliderRect = new Rect(
            controlRect.x + 92f,
            controlRect.y + 6f,
            Mathf.Max(20f, controlRect.width - 150f),
            18f
        );

        Rect timeLabelRect = new Rect(
            controlRect.x + controlRect.width - 50f,
            controlRect.y + 5f,
            45f,
            18f
        );

        string buttonText = _isPlaying ? "■ Pause" : "▶ Play";

        if (GUI.Button(playButtonRect, buttonText, EditorStyles.toolbarButton))
        {
            _isPlaying = !_isPlaying;

            if (_isPlaying)
            {
                _lastSystemTime = EditorApplication.timeSinceStartup;
                _lastPreviewRepaintTime = EditorApplication.timeSinceStartup;
            }
        }

        EditorGUI.BeginChangeCheck();

        float newTime = GUI.HorizontalSlider(sliderRect, normalizedTime, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
        {
            _isPlaying = false;
            _playbackTime = newTime * PreviewDuration;
            Repaint();
        }

        GUI.Label(
            timeLabelRect,
            $"{Mathf.RoundToInt(normalizedTime * 100f)}%",
            EditorStyles.miniLabel
        );
    }

    private static string GetPreviewTargetLabel(CameraProfileSO profile)
    {
        if (CameraProfileTypeUtility.IsTracking(profile))
            return "Target: Head Bottom (0,1.45,0)";

        return "Target: Foot Origin (0,0,0)";
    }

    private static string GetProfileTypeName(CameraProfileSO profile)
    {
        return CameraProfileTypeUtility.GetProfileTypeName(profile, "Camera");
    }
}

internal static class CameraProfileTypeUtility
{
    public const string GeneralProfileTypeName = "GeneralProfileSO";
    public const string TrackingProfileTypeName = "TrackingProfileSO";
    public const string DollyProfileTypeName = "DollyProfileSO";

    public static bool IsGeneral(CameraProfileSO profile)
    {
        return IsProfileType(profile, GeneralProfileTypeName);
    }

    public static bool IsTracking(CameraProfileSO profile)
    {
        return IsProfileType(profile, TrackingProfileTypeName);
    }

    public static bool IsDolly(CameraProfileSO profile)
    {
        return IsProfileType(profile, DollyProfileTypeName);
    }

    public static string GetProfileTypeName(CameraProfileSO profile, string fallbackName)
    {
        if (IsGeneral(profile))
            return "General";

        if (IsTracking(profile))
            return "Tracking";

        if (IsDolly(profile))
            return "Dolly";

        return fallbackName;
    }

    public static AnimationCurve GetCurve(CameraProfileSO profile, string fieldName, AnimationCurve fallbackCurve)
    {
        if (profile == null)
            return fallbackCurve;

        System.Reflection.FieldInfo field = profile.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic
        );

        if (field == null || !typeof(AnimationCurve).IsAssignableFrom(field.FieldType))
            return fallbackCurve;

        return field.GetValue(profile) as AnimationCurve ?? fallbackCurve;
    }

    private static bool IsProfileType(CameraProfileSO profile, string typeName)
    {
        return profile != null && profile.GetType().Name == typeName;
    }
}
// =========================================================================
// Profile Preview Renderer
// =========================================================================
public class CameraProfilePreviewRenderer
{
    private PreviewRenderUtility _previewRenderUtility;

    private Mesh _capsuleMesh;
    private Mesh _sphereMesh;
    private Mesh _cubeMesh;

    private Material _dummyBodyMaterial;
    private Material _faceMaterial;
    private Material _groundMaterial;
    private Material _gridMaterial;
    private Material _pathMaterial;
    private Material _footTargetMaterial;
    private Material _trackingTargetMaterial;

    public CameraProfilePreviewRenderer()
    {
        _previewRenderUtility = new PreviewRenderUtility();

        SetupCamera();
        SetupLights();
        CreateMeshes();
        CreateMaterials();
    }

    public void Dispose()
    {
        if (_previewRenderUtility != null)
        {
            _previewRenderUtility.Cleanup();
            _previewRenderUtility = null;
        }

        DestroyImmediateSafe(_capsuleMesh);
        DestroyImmediateSafe(_sphereMesh);
        DestroyImmediateSafe(_cubeMesh);

        DestroyImmediateSafe(_dummyBodyMaterial);
        DestroyImmediateSafe(_faceMaterial);
        DestroyImmediateSafe(_groundMaterial);
        DestroyImmediateSafe(_gridMaterial);
        DestroyImmediateSafe(_pathMaterial);
        DestroyImmediateSafe(_footTargetMaterial);
        DestroyImmediateSafe(_trackingTargetMaterial);
    }

    public void Render(Rect previewRect, GUIStyle background, CameraProfileSO profile, float normalizedTime)
    {
        if (_previewRenderUtility == null || profile == null)
            return;

        if (previewRect.width <= 1f || previewRect.height <= 1f)
            return;

        if (Event.current.type != EventType.Repaint)
            return;

        _previewRenderUtility.BeginPreview(previewRect, background);

        SetupPreviewCamera(profile, normalizedTime);

        DrawStudioScene(profile);
        DrawDummy();

        _previewRenderUtility.camera.Render();

        Texture resultTexture = _previewRenderUtility.EndPreview();

        if (resultTexture != null)
        {
            GUI.DrawTexture(previewRect, resultTexture, ScaleMode.StretchToFill, false);
        }
    }

    private void SetupCamera()
    {
        Camera camera = _previewRenderUtility.camera;

        if (camera == null)
            return;

        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;

        var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }

        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorTexture = false;
        cameraData.requiresDepthTexture = false;
        cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
    }

    private void SetupLights()
    {
        if (_previewRenderUtility.lights == null)
            return;

        if (_previewRenderUtility.lights.Length > 0 && _previewRenderUtility.lights[0] != null)
        {
            _previewRenderUtility.lights[0].intensity = 1.35f;
            _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
        }

        if (_previewRenderUtility.lights.Length > 1 && _previewRenderUtility.lights[1] != null)
        {
            _previewRenderUtility.lights[1].intensity = 0.5f;
            _previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
        }
    }

    private void CreateMeshes()
    {
        _capsuleMesh = CreatePrimitiveMesh(PrimitiveType.Capsule);
        _sphereMesh = CreatePrimitiveMesh(PrimitiveType.Sphere);
        _cubeMesh = CreatePrimitiveMesh(PrimitiveType.Cube);
    }

    private Mesh CreatePrimitiveMesh(PrimitiveType primitiveType)
    {
        GameObject temp = GameObject.CreatePrimitive(primitiveType);
        temp.hideFlags = HideFlags.HideAndDontSave;

        MeshFilter meshFilter = temp.GetComponent<MeshFilter>();
        Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : null;
        Mesh mesh = sourceMesh != null ? Object.Instantiate(sourceMesh) : null;

        Object.DestroyImmediate(temp);

        if (mesh != null)
        {
            mesh.hideFlags = HideFlags.HideAndDontSave;
        }

        return mesh;
    }

    private void CreateMaterials()
    {
        _dummyBodyMaterial = CreateMaterial("Preview Dummy Body", new Color(0.78f, 0.78f, 0.74f, 1f));
        _faceMaterial = CreateMaterial("Preview Face T Mark", new Color(1f, 0.55f, 0.08f, 1f));

        _groundMaterial = CreateMaterial("Preview Ground", new Color(0.22f, 0.22f, 0.22f, 1f));
        _gridMaterial = CreateMaterial("Preview Grid", new Color(0.34f, 0.34f, 0.34f, 1f));
        _pathMaterial = CreateMaterial("Preview Dolly Path", new Color(1f, 0.62f, 0.12f, 1f));
        _footTargetMaterial = CreateMaterial("Preview Foot Target", new Color(0.9f, 0.95f, 1f, 1f));
        _trackingTargetMaterial = CreateMaterial("Preview Tracking Target", new Color(0.25f, 0.75f, 1f, 1f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }

    private void SetupPreviewCamera(CameraProfileSO profile, float t)
    {
        Camera camera = _previewRenderUtility.camera;

        camera.fieldOfView = Mathf.Clamp(profile.fovCurve.Evaluate(t), 10f, 120f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;

        Vector3 targetPosition = GetPreviewTargetPosition(profile);
        Vector3 lookTarget = targetPosition;

        if (CameraProfileTypeUtility.IsGeneral(profile))
        {
            AnimationCurve distanceCurve = CameraProfileTypeUtility.GetCurve(
                profile,
                "posDistanceCurve",
                AnimationCurve.Constant(0f, 1f, 3f)
            );

            float distance = Mathf.Max(distanceCurve.Evaluate(t), 0.25f);

            Vector3 targetOffset = new Vector3(
                CameraProfileTypeUtility.GetCurve(profile, "posTargetOffsetXCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "posTargetOffsetYCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "posTargetOffsetZCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t)
            );

            Vector2 screenOffset = new Vector2(
                CameraProfileTypeUtility.GetCurve(profile, "posScreenXCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "posScreenYCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t)
            );

            lookTarget = targetPosition + targetOffset;

            camera.transform.position =
                lookTarget +
                new Vector3(screenOffset.x * 1.35f, screenOffset.y * 0.9f, -distance);

            SafeLookAt(camera.transform, lookTarget);
            return;
        }
        if (CameraProfileTypeUtility.IsTracking(profile))
        {
            Vector3 followOffset = new Vector3(
                CameraProfileTypeUtility.GetCurve(profile, "followOffsetXCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "followOffsetYCurve", AnimationCurve.Constant(0f, 1f, 0.7f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "followOffsetZCurve", AnimationCurve.Constant(0f, 1f, 2.6f)).Evaluate(t)
            );

            if (followOffset.magnitude < 1.2f)
            {
                followOffset = followOffset.normalized * 2.0f;

                if (followOffset == Vector3.zero)
                {
                    followOffset = new Vector3(0f, 0.7f, 2.6f);
                }
            }

            Vector3 rotationOffset = new Vector3(
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetXCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetYCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetZCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t)
            );

            lookTarget = targetPosition + rotationOffset;

            camera.transform.position =
                targetPosition +
                new Vector3(followOffset.x, followOffset.y, -Mathf.Abs(followOffset.z));

            SafeLookAt(camera.transform, lookTarget);
            return;
        }
        if (CameraProfileTypeUtility.IsDolly(profile))
        {
            AnimationCurve splinePositionCurve = CameraProfileTypeUtility.GetCurve(
                profile,
                "splinePositionCurve",
                AnimationCurve.Linear(0f, 0f, 1f, 1f)
            );

            float normalizedPosition = Mathf.Clamp01(splinePositionCurve.Evaluate(t));

            Vector3 dollyStart = new Vector3(-2.5f, 1.15f, -3.0f);
            Vector3 dollyEnd = new Vector3(2.5f, 1.15f, -3.0f);

            Vector3 cameraPosition = Vector3.Lerp(dollyStart, dollyEnd, normalizedPosition);
            cameraPosition.y += Mathf.Sin(normalizedPosition * Mathf.PI) * 0.45f;

            Vector3 rotationOffset = new Vector3(
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetXCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetYCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t),
                CameraProfileTypeUtility.GetCurve(profile, "rotTargetOffsetZCurve", AnimationCurve.Constant(0f, 1f, 0f)).Evaluate(t)
            );

            lookTarget = targetPosition + rotationOffset;

            camera.transform.position = cameraPosition;
            SafeLookAt(camera.transform, lookTarget);
            return;
        }
        camera.transform.position = new Vector3(0f, 1.3f, -3.0f);
        SafeLookAt(camera.transform, targetPosition + Vector3.up);
    }

    private Vector3 GetPreviewTargetPosition(CameraProfileSO profile)
    {
        if (CameraProfileTypeUtility.IsTracking(profile))
            return new Vector3(0f, 1.45f, 0f);

        return Vector3.zero;
    }

    private void SafeLookAt(Transform cameraTransform, Vector3 target)
    {
        Vector3 direction = target - cameraTransform.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        Vector3 up = Vector3.up;

        if (Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f)
        {
            up = Vector3.forward;
        }

        cameraTransform.rotation = Quaternion.LookRotation(direction.normalized, up);
    }

    private void DrawStudioScene(CameraProfileSO profile)
    {
        DrawGroundPlane();
        DrawGridLines();
        DrawFootTargetMarker();

        if (CameraProfileTypeUtility.IsTracking(profile))
        {
            DrawTrackingTargetMarker();
        }

        if (CameraProfileTypeUtility.IsDolly(profile))
        {
            DrawDollyPath();
        }
    }

    private void DrawGroundPlane()
    {
        DrawCube(
            new Vector3(0f, -0.035f, 0f),
            Quaternion.identity,
            new Vector3(6f, 0.025f, 6f),
            _groundMaterial
        );
    }

    private void DrawGridLines()
    {
        for (int i = -2; i <= 2; i++)
        {
            float lineWidth = i == 0 ? 0.035f : 0.015f;

            DrawCube(
                new Vector3(0f, 0.005f, i),
                Quaternion.identity,
                new Vector3(5f, 0.012f, lineWidth),
                _gridMaterial
            );

            DrawCube(
                new Vector3(i, 0.006f, 0f),
                Quaternion.identity,
                new Vector3(lineWidth, 0.012f, 5f),
                _gridMaterial
            );
        }
    }

    private void DrawFootTargetMarker()
    {
        DrawCube(
            new Vector3(0f, 0.025f, 0f),
            Quaternion.identity,
            new Vector3(0.28f, 0.03f, 0.28f),
            _footTargetMaterial
        );
    }

    private void DrawTrackingTargetMarker()
    {
        Vector3 target = new Vector3(0f, 1.45f, 0f);

        DrawMesh(
            _sphereMesh,
            target,
            Quaternion.identity,
            new Vector3(0.16f, 0.16f, 0.16f),
            _trackingTargetMaterial
        );

        DrawCube(
            target,
            Quaternion.identity,
            new Vector3(0.42f, 0.035f, 0.035f),
            _trackingTargetMaterial
        );

        DrawCube(
            target,
            Quaternion.identity,
            new Vector3(0.035f, 0.42f, 0.035f),
            _trackingTargetMaterial
        );
    }

    private void DrawDollyPath()
    {
        Vector3 previousPoint = GetDollyPoint(0f);

        for (int i = 1; i <= 6; i++)
        {
            float t = i / 6f;
            Vector3 nextPoint = GetDollyPoint(t);

            DrawSegment(previousPoint, nextPoint, 0.035f, _pathMaterial);

            previousPoint = nextPoint;
        }
    }

    private Vector3 GetDollyPoint(float t)
    {
        Vector3 start = new Vector3(-2.5f, 0.08f, -3.0f);
        Vector3 end = new Vector3(2.5f, 0.08f, -3.0f);

        Vector3 point = Vector3.Lerp(start, end, t);
        point.y += Mathf.Sin(t * Mathf.PI) * 0.12f;

        return point;
    }

    private void DrawDummy()
    {
        DrawMesh(
            _capsuleMesh,
            new Vector3(0f, 0.82f, 0f),
            Quaternion.identity,
            new Vector3(0.42f, 0.72f, 0.42f),
            _dummyBodyMaterial
        );

        DrawMesh(
            _sphereMesh,
            new Vector3(0f, 1.78f, 0f),
            Quaternion.identity,
            new Vector3(0.66f, 0.68f, 0.66f),
            _dummyBodyMaterial
        );

        DrawCube(
            new Vector3(0f, 1.88f, -0.34f),
            Quaternion.identity,
            new Vector3(0.34f, 0.045f, 0.035f),
            _faceMaterial
        );

        DrawCube(
            new Vector3(0f, 1.76f, -0.345f),
            Quaternion.identity,
            new Vector3(0.055f, 0.28f, 0.035f),
            _faceMaterial
        );
    }

    private void DrawSegment(Vector3 start, Vector3 end, float thickness, Material material)
    {
        Vector3 direction = end - start;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 center = (start + end) * 0.5f;
        float length = direction.magnitude;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        DrawCube(
            center,
            rotation,
            new Vector3(thickness, thickness, length),
            material
        );
    }

    private void DrawCube(Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        DrawMesh(_cubeMesh, position, rotation, scale, material);
    }

    private void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        if (mesh == null || material == null)
            return;

        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
        _previewRenderUtility.DrawMesh(mesh, matrix, material, 0);
    }

    private void DestroyImmediateSafe(Object obj)
    {
        if (obj != null)
        {
            Object.DestroyImmediate(obj);
        }
    }
}

// =========================================================================
// Tag 搜尋 Popup
// =========================================================================
public class TagSearchPopupContent : PopupWindowContent
{
    private readonly CameraProfileSO _targetSO;

    private string _searchQuery = "";
    private List<CameraTagSO> _allProjectTags = new List<CameraTagSO>();
    private Vector2 _scrollPosition;

    public TagSearchPopupContent(CameraProfileSO targetSO)
    {
        _targetSO = targetSO;
        RefreshProjectTags();
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(250f, 300f);
    }

    public override void OnGUI(Rect rect)
    {
        GUILayout.Label("搜尋現有標籤", EditorStyles.boldLabel);

        GUI.SetNextControlName("SearchField");

        _searchQuery = EditorGUILayout.TextField(
            _searchQuery,
            EditorStyles.toolbarSearchField
        );

        GUI.FocusControl("SearchField");

        EditorGUILayout.Space();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        List<CameraTagSO> filteredTags = _allProjectTags
            .Where(tag =>
                tag != null &&
                (
                    string.IsNullOrEmpty(_searchQuery) ||
                    tag.name.ToLower().Contains(_searchQuery.ToLower())
                )
            )
            .ToList();

        if (filteredTags.Count == 0)
        {
            GUILayout.Label("找不到相符的標籤", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            foreach (CameraTagSO tag in filteredTags)
            {
                if (_targetSO.tags.Contains(tag))
                    continue;

                if (GUILayout.Button(tag.name, EditorStyles.label))
                {
                    Undo.RecordObject(_targetSO, "Add Camera Tag");
                    _targetSO.tags.Add(tag);
                    EditorUtility.SetDirty(_targetSO);
                    editorWindow.Close();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshProjectTags()
    {
        _allProjectTags.Clear();

        string[] guids = AssetDatabase.FindAssets("t:CameraTagSO");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CameraTagSO tag = AssetDatabase.LoadAssetAtPath<CameraTagSO>(path);

            if (tag != null)
            {
                _allProjectTags.Add(tag);
            }
        }
    }
}
#endif