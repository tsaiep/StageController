#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(UnifiedStageTemplate))]
public class UnifiedStageTemplateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty tagsProp = serializedObject.FindProperty("tags");
        UnifiedStageTemplate template = (UnifiedStageTemplate)target;

        UnifiedStageTemplateEditorTools.DrawTemplateTags(template, tagsProp);

        EditorGUILayout.Space(6);
        DrawPropertiesExcluding(serializedObject, "m_Script", "tags");

        serializedObject.ApplyModifiedProperties();
    }
}

internal static class UnifiedStageTemplateEditorTools
{
    public static void DrawTemplateTags(UnifiedStageTemplate template, SerializedProperty tagsProp)
    {
        EditorGUILayout.LabelField("Template Tags", EditorStyles.boldLabel);

        if (tagsProp == null)
        {
            EditorGUILayout.HelpBox("The UnifiedStageTemplate.tags field was not found.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (template.tags != null)
            {
                for (int i = 0; i < template.tags.Count; i++)
                {
                    UnifiedStageTemplateTagSO tag = template.tags[i];
                    if (tag == null)
                        continue;

                    if (GUILayout.Button("#" + tag.name + "  X", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        Undo.RecordObject(template, "Remove Template Tag");
                        template.tags.RemoveAt(i);
                        EditorUtility.SetDirty(template);
                        break;
                    }
                }
            }
        }

        Rect buttonRect = EditorGUILayout.GetControlRect(false, 24f);
        if (GUI.Button(buttonRect, "Add Tag...", EditorStyles.popup))
        {
            PopupWindow.Show(buttonRect, new TemplateTagSearchPopup(tag =>
            {
                if (tag == null)
                    return;

                if (template.tags == null)
                    template.tags = new List<UnifiedStageTemplateTagSO>();

                if (template.tags.Contains(tag))
                    return;

                Undo.RecordObject(template, "Add Template Tag");
                template.tags.Add(tag);
                EditorUtility.SetDirty(template);
            }, template.tags));
        }
    }

    public static string GetTagText(UnifiedStageTemplate template)
    {
        if (template == null || template.tags == null || template.tags.Count == 0)
            return "";

        List<string> names = new List<string>();
        foreach (UnifiedStageTemplateTagSO tag in template.tags)
        {
            if (tag != null)
                names.Add("#" + tag.name);
        }

        return string.Join(" ", names);
    }

    public static List<UnifiedStageTemplate> FindTemplates()
    {
        HashSet<UnifiedStageTemplate> templates = new HashSet<UnifiedStageTemplate>();
        string[] guids = AssetDatabase.FindAssets("t:UnifiedStageTemplate");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnifiedStageTemplate template = AssetDatabase.LoadAssetAtPath<UnifiedStageTemplate>(path);
            if (template != null)
                templates.Add(template);
        }

        return templates
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<UnifiedStageTemplateTagSO> FindTags()
    {
        HashSet<UnifiedStageTemplateTagSO> tags = new HashSet<UnifiedStageTemplateTagSO>();
        string[] guids = AssetDatabase.FindAssets("t:UnifiedStageTemplateTagSO");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnifiedStageTemplateTagSO tag = AssetDatabase.LoadAssetAtPath<UnifiedStageTemplateTagSO>(path);
            if (tag != null)
                tags.Add(tag);
        }

        return tags
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static UnifiedStageController FindBoundController(UnifiedStageClip clip)
    {
        UnifiedStageController controller = FindBoundControllerFromDirector(TimelineEditor.inspectedDirector, clip);
        if (controller != null)
            return controller;

        foreach (PlayableDirector director in UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            controller = FindBoundControllerFromDirector(director, clip);
            if (controller != null)
                return controller;
        }

        return FindSinglePreviewControllerFallback();
    }

    public static bool IsFallbackController(UnifiedStageClip clip, UnifiedStageController controller)
    {
        if (controller == null)
            return false;

        UnifiedStageController timelineController = FindBoundControllerFromDirector(TimelineEditor.inspectedDirector, clip);
        if (timelineController == controller)
            return false;

        foreach (PlayableDirector director in UnityEngine.Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            timelineController = FindBoundControllerFromDirector(director, clip);
            if (timelineController == controller)
                return false;
        }

        return FindSinglePreviewControllerFallback() == controller;
    }

    private static UnifiedStageController FindBoundControllerFromDirector(PlayableDirector director, UnifiedStageClip clip)
    {
        if (director == null || director.playableAsset == null || clip == null)
            return null;

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        if (timeline == null)
            return null;

        foreach (TrackAsset track in EnumerateTracks(timeline))
        {
            foreach (TimelineClip timelineClip in track.GetClips())
            {
                if (timelineClip.asset != clip)
                    continue;

                return director.GetGenericBinding(track) as UnifiedStageController;
            }
        }

        return null;
    }

    private static IEnumerable<TrackAsset> EnumerateTracks(TimelineAsset timeline)
    {
        if (timeline == null)
            yield break;

        foreach (TrackAsset track in timeline.GetRootTracks())
        {
            foreach (TrackAsset nestedTrack in EnumerateTrackAndChildren(track))
                yield return nestedTrack;
        }
    }

    private static IEnumerable<TrackAsset> EnumerateTrackAndChildren(TrackAsset track)
    {
        if (track == null)
            yield break;

        yield return track;

        foreach (TrackAsset childTrack in track.GetChildTracks())
        {
            foreach (TrackAsset nestedTrack in EnumerateTrackAndChildren(childTrack))
                yield return nestedTrack;
        }
    }

    private static UnifiedStageController FindSinglePreviewControllerFallback()
    {
        UnifiedStageController[] controllers = UnityEngine.Object.FindObjectsByType<UnifiedStageController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<UnifiedStageController> candidates = controllers
            .Where(controller => controller != null && controller.templatePreviewPrefab != null)
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static void ApplyTemplateToClip(UnifiedStageClip clip, UnifiedStageTemplate template)
    {
        if (clip == null)
            return;

        ApplyTemplateToClip(
            clip,
            template,
            clip.applyTemplateColorSettings,
            clip.applyTemplateRotationSettings,
            clip.applyTemplateFixtureSettings
        );
    }

    public static void ApplyTemplateToClip(
        UnifiedStageClip clip,
        UnifiedStageTemplate template,
        bool applyColorSettings,
        bool applyRotationSettings,
        bool applyFixtureSettings)
    {
        if (clip == null || template == null)
            return;

        Undo.RecordObject(clip, "Apply Stage Template");
        clip.applyTemplateColorSettings = applyColorSettings;
        clip.applyTemplateRotationSettings = applyRotationSettings;
        clip.applyTemplateFixtureSettings = applyFixtureSettings;
        clip.ApplyTemplateValues(template);
        clip.applyTemplate = null;
        clip.selectedTemplate = template;
        clip.clipDisplayName = template.name;

        TimelineClip timelineClip = TimelineEditor.selectedClips.FirstOrDefault(c => c.asset == clip);
        if (timelineClip != null)
            timelineClip.displayName = template.name;

        EditorUtility.SetDirty(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
}

internal class TemplateTagSearchPopup : PopupWindowContent
{
    private readonly Action<UnifiedStageTemplateTagSO> _onPicked;
    private readonly HashSet<UnifiedStageTemplateTagSO> _excludedTags;
    private readonly List<UnifiedStageTemplateTagSO> _allTags;
    private string _search = "";
    private Vector2 _scroll;

    public TemplateTagSearchPopup(Action<UnifiedStageTemplateTagSO> onPicked, IEnumerable<UnifiedStageTemplateTagSO> excludedTags = null)
    {
        _onPicked = onPicked;
        _excludedTags = excludedTags != null
            ? new HashSet<UnifiedStageTemplateTagSO>(excludedTags.Where(t => t != null))
            : new HashSet<UnifiedStageTemplateTagSO>();
        _allTags = UnifiedStageTemplateEditorTools.FindTags();
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(260f, 320f);
    }

    public override void OnGUI(Rect rect)
    {
        EditorGUILayout.LabelField("Template Tags", EditorStyles.boldLabel);
        _search = EditorGUILayout.TextField(_search, EditorStyles.textField);

        EditorGUILayout.Space(4);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string keyword = string.IsNullOrWhiteSpace(_search) ? "" : _search.Trim().ToLowerInvariant();
        List<UnifiedStageTemplateTagSO> filtered = _allTags
            .Where(tag => tag != null)
            .Where(tag => !_excludedTags.Contains(tag))
            .Where(tag => keyword.Length == 0 || tag.name.ToLowerInvariant().Contains(keyword))
            .ToList();

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching tags. Create one from Assets/Create/Stage Control/Template Tag.", MessageType.Info);
        }
        else
        {
            foreach (UnifiedStageTemplateTagSO tag in filtered)
            {
                if (GUILayout.Button(tag.name, EditorStyles.label))
                {
                    _onPicked?.Invoke(tag);
                    editorWindow.Close();
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }
}

internal class UnifiedStageTemplateSelectorWindow : EditorWindow
{
    private UnifiedStageClip _clip;
    private UnifiedStageController _controller;
    private UnifiedStageTemplate _selectedTemplate;
    private UnifiedStageTemplatePreviewRenderer _previewRenderer;

    private List<UnifiedStageTemplate> _allTemplates = new List<UnifiedStageTemplate>();
    private List<UnifiedStageTemplateTagSO> _allTags = new List<UnifiedStageTemplateTagSO>();
    private List<UnifiedStageTemplate> _filteredTemplates = new List<UnifiedStageTemplate>();
    private readonly List<UnifiedStageTemplateTagSO> _selectedTags = new List<UnifiedStageTemplateTagSO>();

    private string _search = "";
    private bool _filterDirty = true;
    private Vector2 _templateScroll;
    private Vector2 _tagScroll;
    private Vector2 _mainScroll;
    private bool _applyColorSettings = true;
    private bool _applyRotationSettings = true;
    private bool _applyFixtureSettings = true;
    private bool _stackPreviewLights;
    private double _lastPreviewRepaintTime;

    public static void Open(UnifiedStageClip clip, UnifiedStageController controller)
    {
        UnifiedStageTemplateSelectorWindow window = GetWindow<UnifiedStageTemplateSelectorWindow>("Stage Template Selector");
        window.minSize = new Vector2(750f, 600f);
        window.SetContext(clip, controller);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        _previewRenderer = new UnifiedStageTemplatePreviewRenderer();
        RefreshDatabase();
        EditorApplication.update += RepaintPreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RepaintPreview;

        if (_previewRenderer != null)
        {
            _previewRenderer.Dispose();
            _previewRenderer = null;
        }
    }

    private void SetContext(UnifiedStageClip clip, UnifiedStageController controller)
    {
        _clip = clip;
        _controller = controller != null ? controller : UnifiedStageTemplateEditorTools.FindBoundController(clip);
        _selectedTemplate = clip != null && clip.selectedTemplate != null ? clip.selectedTemplate : clip != null ? clip.applyTemplate : null;

        if (clip != null)
        {
            _applyColorSettings = clip.applyTemplateColorSettings;
            _applyRotationSettings = clip.applyTemplateRotationSettings;
            _applyFixtureSettings = clip.applyTemplateFixtureSettings;
        }

        RefreshDatabase();
        Repaint();
    }

    private void RefreshDatabase()
    {
        _allTemplates = UnifiedStageTemplateEditorTools.FindTemplates();
        _allTags = UnifiedStageTemplateEditorTools.FindTags();
        _filterDirty = true;
    }

    private void OnGUI()
    {
        if (_previewRenderer == null)
            _previewRenderer = new UnifiedStageTemplatePreviewRenderer();

        if (_clip != null && _controller == null)
            _controller = UnifiedStageTemplateEditorTools.FindBoundController(_clip);

        _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

        DrawHeader();
        EditorGUILayout.Space(6);
        DrawFilters();
        EditorGUILayout.Space(0);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTemplateList();
            DrawPreviewAndActions();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Stage Template Selector", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Target Clip", _clip, typeof(UnifiedStageClip), false);
            EditorGUILayout.ObjectField("Bound Controller", _controller, typeof(UnifiedStageController), true);
            EditorGUILayout.ObjectField("Template Preview Prefab", GetPreviewPrefab(), typeof(GameObject), false);
        }

        if (_clip == null)
        {
            EditorGUILayout.HelpBox("No UnifiedStageClip is attached to this selector window. Close and reopen it from a clip inspector.", MessageType.Error);
        }
        else if (_controller == null)
        {
            EditorGUILayout.HelpBox("No UnifiedStageController binding was found for this Timeline track, and no single scene controller with Template Preview Prefab was available as fallback.", MessageType.Warning);
        }
        else if (UnifiedStageTemplateEditorTools.IsFallbackController(_clip, _controller))
        {
            EditorGUILayout.HelpBox("Using the only scene UnifiedStageController that has Template Preview Prefab assigned. This is a fallback because the Timeline track binding was not found.", MessageType.Info);
        }
        else if (_controller.templatePreviewPrefab == null)
        {
            EditorGUILayout.HelpBox("The bound UnifiedStageController has no Template Preview Prefab assigned. This reads from the scene component instance bound to the Timeline track, not from the UnifiedStageController script asset in the Project window.", MessageType.Warning);
        }
    }

    private void DrawFilters()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            _search = GUILayout.TextField(_search, EditorStyles.textField);
            if (EditorGUI.EndChangeCheck())
                _filterDirty = true;

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                RefreshDatabase();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(52f)))
            {
                _search = "";
                _selectedTags.Clear();
                _filterDirty = true;
                GUI.FocusControl(null);
            }
        }
        
        EditorGUILayout.Space(2);
        
        if (_allTags.Count == 0)
        {
            EditorGUILayout.HelpBox("No UnifiedStageTemplateTagSO assets were found. Create tags from Assets/Create/Stage Control/Template Tag.", MessageType.Info);
            return;
        }

        _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, false, false, GUILayout.Height(32f));
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTagChip(null, "All");
            foreach (UnifiedStageTemplateTagSO tag in _allTags)
                DrawTagChip(tag, "#" + tag.name);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTagChip(UnifiedStageTemplateTagSO tag, string label)
    {
        bool selected = tag == null ? _selectedTags.Count == 0 : _selectedTags.Contains(tag);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.55f, 0.75f, 1f, 1f) : old;

        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
        {
            if (tag == null)
                _selectedTags.Clear();
            else if (_selectedTags.Contains(tag))
                _selectedTags.Remove(tag);
            else
                _selectedTags.Add(tag);

            _filterDirty = true;
        }

        GUI.backgroundColor = old;
    }

    private void DrawTemplateList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(330f));
        List<UnifiedStageTemplate> filtered = GetFilteredTemplates();
        EditorGUILayout.LabelField("Templates (" + filtered.Count + ")", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(420f));
        _templateScroll = EditorGUILayout.BeginScrollView(_templateScroll);

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching templates.", MessageType.Info);
        }
        else
        {
            foreach (UnifiedStageTemplate template in filtered)
                DrawTemplateRow(template);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    private void DrawTemplateRow(UnifiedStageTemplate template)
    {
        bool selected = _selectedTemplate == template;
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.45f, 0.7f, 1f, 1f) : old;

        if (GUILayout.Button(GUIContent.none, EditorStyles.helpBox, GUILayout.Height(48f), GUILayout.ExpandWidth(true)))
            _selectedTemplate = template;

        Rect row = GUILayoutUtility.GetLastRect();
        GUI.backgroundColor = old;

        GUI.Label(new Rect(row.x + 8f, row.y + 5f, row.width - 16f, 18f), template.name, selected ? EditorStyles.boldLabel : EditorStyles.label);
        string tags = UnifiedStageTemplateEditorTools.GetTagText(template);
        GUI.Label(new Rect(row.x + 8f, row.y + 25f, row.width - 16f, 16f), string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
    }

    private void DrawPreviewAndActions()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        EditorGUILayout.LabelField("Selected Template", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(_selectedTemplate, typeof(UnifiedStageTemplate), false);
        }

        if (_selectedTemplate != null)
        {
            string tags = UnifiedStageTemplateEditorTools.GetTagText(_selectedTemplate);
            EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Apply Options", EditorStyles.boldLabel);
                _applyColorSettings = EditorGUILayout.Toggle("Apply Color Settings", _applyColorSettings);
                _applyRotationSettings = EditorGUILayout.Toggle("Apply Rotation Settings", _applyRotationSettings);
                _applyFixtureSettings = EditorGUILayout.Toggle("Apply Fixture Settings", _applyFixtureSettings);
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Preview Setting", EditorStyles.boldLabel);
                _stackPreviewLights = EditorGUILayout.Toggle("Stack Preview Lights", _stackPreviewLights);
            }
        }

        Rect previewRect = GUILayoutUtility.GetRect(10f, 250f, GUILayout.ExpandWidth(true), GUILayout.Height(250f));
        _previewRenderer.Render(previewRect, EditorStyles.helpBox, _selectedTemplate, GetPreviewPrefab(), _stackPreviewLights);

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _clip != null && _selectedTemplate != null;
            if (GUILayout.Button("Apply Template To Clip", GUILayout.Height(30f)))
            {
                UnifiedStageTemplateEditorTools.ApplyTemplateToClip(
                    _clip,
                    _selectedTemplate,
                    _applyColorSettings,
                    _applyRotationSettings,
                    _applyFixtureSettings
                );
                Repaint();
                GUI.changed = true;
            }

            GUI.enabled = true;

            if (GUILayout.Button("Ping", GUILayout.Width(70f), GUILayout.Height(30f)) && _selectedTemplate != null)
                EditorGUIUtility.PingObject(_selectedTemplate);
        }

        //EditorGUILayout.HelpBox("Apply Template To Clip copies template values into the Timeline clip fields, which are the values used by CreatePlayable and the mixer.", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private GameObject GetPreviewPrefab()
    {
        return _controller != null ? _controller.templatePreviewPrefab : null;
    }

    private List<UnifiedStageTemplate> GetFilteredTemplates()
    {
        if (!_filterDirty)
            return _filteredTemplates;

        string keyword = string.IsNullOrWhiteSpace(_search) ? "" : _search.Trim().ToLowerInvariant();

        _filteredTemplates = _allTemplates
            .Where(template => template != null)
            .Where(MatchesTags)
            .Where(template => MatchesSearch(template, keyword))
            .ToList();

        _filterDirty = false;
        return _filteredTemplates;
    }

    private bool MatchesTags(UnifiedStageTemplate template)
    {
        if (_selectedTags.Count == 0)
            return true;

        if (template.tags == null)
            return false;

        foreach (UnifiedStageTemplateTagSO tag in _selectedTags)
        {
            if (tag != null && !template.tags.Contains(tag))
                return false;
        }

        return true;
    }

    private static bool MatchesSearch(UnifiedStageTemplate template, string keyword)
    {
        if (keyword.Length == 0)
            return true;

        if (template.name.ToLowerInvariant().Contains(keyword))
            return true;

        if (template.tags == null)
            return false;

        return template.tags.Any(tag => tag != null && tag.name.ToLowerInvariant().Contains(keyword));
    }

    private void RepaintPreview()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastPreviewRepaintTime < 1.0 / 30.0)
            return;

        _lastPreviewRepaintTime = now;
        Repaint();
    }
}

internal sealed class UnifiedStageTemplatePreviewRenderer : IDisposable
{
    private const int PreviewUnitCount = 3;
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly Vector3 PreviewCameraTarget = new Vector3(0f, 0.8f, 0f);

    private PreviewRenderUtility _preview;
    private readonly GameObject[] _instances = new GameObject[PreviewUnitCount];
    private readonly List<MeshRenderer>[] _renderers = new List<MeshRenderer>[PreviewUnitCount];
    private readonly Transform[] _panTransforms = new Transform[PreviewUnitCount];
    private readonly Transform[] _tiltTransforms = new Transform[PreviewUnitCount];
    private readonly Transform[] _spreadPanTransforms = new Transform[PreviewUnitCount];
    private readonly Transform[] _spreadTiltTransforms = new Transform[PreviewUnitCount];
    private readonly float[] _currentPan = new float[PreviewUnitCount];
    private readonly float[] _currentTilt = new float[PreviewUnitCount];
    private readonly bool[] _hasCurrentAngles = new bool[PreviewUnitCount];
    private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
    private GameObject _sourcePrefab;
    private double _startTime;
    private float _cameraYaw = 45f;
    private float _cameraPitch = 45f;
    private float _cameraDistance = 7.5f;

    public UnifiedStageTemplatePreviewRenderer()
    {
        _startTime = EditorApplication.timeSinceStartup;
    }

    public void Dispose()
    {
        DestroyPreview();
    }

    public void Render(Rect rect, GUIStyle background, UnifiedStageTemplate template, GameObject previewPrefab, bool stackUnitsAtOrigin)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;

        HandleCameraInput(rect);

        if (template == null)
        {
            if (Event.current.type == EventType.Repaint)
                DrawCentered(rect, "Select a template to preview.");
            return;
        }

        if (previewPrefab == null)
        {
            if (Event.current.type == EventType.Repaint)
                DrawCentered(rect, "Assign Template Preview Prefab on the bound UnifiedStageController.");
            return;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        EnsurePreview(previewPrefab);
        UpdateInstances(template, stackUnitsAtOrigin);

        UpdateCamera();
        _preview.BeginPreview(rect, background);
        _preview.camera.Render();
        Texture texture = _preview.EndPreview();

        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
    }

    private void EnsurePreview(GameObject prefab)
    {
        if (_preview != null && _sourcePrefab == prefab)
            return;

        DestroyPreview();

        _sourcePrefab = prefab;
        _preview = new PreviewRenderUtility();
        _preview.camera.clearFlags = CameraClearFlags.Color;
        _preview.camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f);
        _preview.camera.fieldOfView = 35f;
        _preview.camera.nearClipPlane = 0.05f;
        _preview.camera.farClipPlane = 80f;
        UpdateCamera();

        if (_preview.lights != null)
        {
            if (_preview.lights.Length > 0 && _preview.lights[0] != null)
            {
                _preview.lights[0].intensity = 0f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            }
            if (_preview.lights.Length > 1 && _preview.lights[1] != null)
            {
                _preview.lights[1].intensity = 0f;
                _preview.lights[1].transform.rotation = Quaternion.Euler(330f, 220f, 0f);
            }
        }

        for (int i = 0; i < PreviewUnitCount; i++)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "Template Preview Unit " + i;
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = new Vector3((i - 1) * 1.5f, 0f, 0f);
            instance.transform.rotation = Quaternion.identity;
            DisableLights(instance);

            _instances[i] = instance;
            _renderers[i] = FindPreviewRenderers(instance);
            _panTransforms[i] = FindChildTransform(instance.transform, "MovingBeamLight_Pan");
            _tiltTransforms[i] = FindChildTransform(instance.transform, "MovingBeamLight_Tilt");
            _spreadPanTransforms[i] = FindChildTransform(instance.transform, "MovingBeamLight_SpreadPan");
            _spreadTiltTransforms[i] = FindChildTransform(instance.transform, "MovingBeamLight_SpreadTilt");
            ApplyPreviewRotations(i, 0f, 0f, 0f, 0f);
            _preview.AddSingleGO(instance);
        }
    }

    private void DestroyPreview()
    {
        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }

        for (int i = 0; i < _instances.Length; i++)
        {
            if (_instances[i] != null)
                UnityEngine.Object.DestroyImmediate(_instances[i]);

            _instances[i] = null;
            _renderers[i] = null;
            _panTransforms[i] = null;
            _tiltTransforms[i] = null;
            _spreadPanTransforms[i] = null;
            _spreadTiltTransforms[i] = null;
            _currentPan[i] = 0f;
            _currentTilt[i] = 0f;
            _hasCurrentAngles[i] = false;
        }

        _sourcePrefab = null;
    }

    private void UpdateInstances(UnifiedStageTemplate template, bool stackUnitsAtOrigin)
    {
        float rootTime = (float)(EditorApplication.timeSinceStartup - _startTime);

        for (int i = 0; i < PreviewUnitCount; i++)
        {
            GameObject instance = _instances[i];
            if (instance == null)
                continue;

            float normalizedInGroup = PreviewUnitCount > 1 ? (float)i / (PreviewUnitCount - 1) : 0f;
            float unitDelay = 0f;

            if (template.lightDelayCurve != null && template.lightDelayFactor > 0f)
                unitDelay += template.lightDelayCurve.Evaluate(normalizedInGroup) * template.lightDelayFactor * PreviewUnitCount;

            float unitTime = Mathf.Max(0f, rootTime - template.cyclePauseTime) - unitDelay + template.animationOffset;
            float rangeMultiplier = template.lightRotationRangeCurve != null
                ? template.lightRotationRangeCurve.Evaluate(normalizedInGroup)
                : 1f;

            Vector2 angles = EvaluateAngles(template, unitTime, i, template.rotationRange * rangeMultiplier);
            angles = NormalizeContinuousAngles(i, angles);
            instance.transform.position = GetPreviewUnitPosition(i, stackUnitsAtOrigin);
            instance.transform.rotation = Quaternion.identity;

            Vector2 spreadAngles = EvaluateSpreadAngles(template, rootTime, unitTime, i);
            ApplyPreviewRotations(i, angles.x, angles.y, spreadAngles.x, spreadAngles.y);

            Color color = EvaluateColor(template, rootTime, unitTime, unitDelay, i);
            ApplyColor(i, color);
        }
    }

    private static Vector3 GetPreviewUnitPosition(int index, bool stackUnitsAtOrigin)
    {
        return stackUnitsAtOrigin ? Vector3.zero : new Vector3((index - 1) * 1.5f, 0f, 0f);
    }

    private Vector2 NormalizeContinuousAngles(int index, Vector2 targetAngles)
    {
        if (!_hasCurrentAngles[index])
        {
            _currentPan[index] = targetAngles.x;
            _currentTilt[index] = targetAngles.y;
            _hasCurrentAngles[index] = true;
            return targetAngles;
        }

        _currentPan[index] = _currentPan[index] + Mathf.DeltaAngle(_currentPan[index], targetAngles.x);
        _currentTilt[index] = _currentTilt[index] + Mathf.DeltaAngle(_currentTilt[index], targetAngles.y);
        return new Vector2(_currentPan[index], _currentTilt[index]);
    }

    private void HandleCameraInput(Rect rect)
    {
        int controlId = GUIUtility.GetControlID("UnifiedStageTemplatePreviewCamera".GetHashCode(), FocusType.Passive, rect);
        Event current = Event.current;

        switch (current.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (rect.Contains(current.mousePosition) && (current.button == 0 || current.button == 1 || current.button == 2))
                {
                    GUIUtility.hotControl = controlId;
                    current.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != controlId)
                    break;

                if (current.button == 0)
                {
                    _cameraYaw += current.delta.x * 0.45f;
                    _cameraPitch = Mathf.Clamp(_cameraPitch - current.delta.y * 0.45f, 10f, 80f);
                }
                else
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance + current.delta.y * 0.04f, 3.5f, 16f);
                }

                current.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    current.Use();
                }
                break;

            case EventType.ScrollWheel:
                if (rect.Contains(current.mousePosition))
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance * (1f + current.delta.y * 0.05f), 3.5f, 16f);
                    current.Use();
                }
                break;
        }
    }

    private void UpdateCamera()
    {
        if (_preview == null || _preview.camera == null)
            return;

        Quaternion rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        Vector3 position = PreviewCameraTarget + rotation * new Vector3(0f, 0f, -_cameraDistance);
        _preview.camera.transform.position = position;
        _preview.camera.transform.rotation = Quaternion.LookRotation(PreviewCameraTarget - position, Vector3.up);
    }

    private void ApplyPreviewRotations(int index, float pan, float tilt, float spreadPan, float spreadTilt)
    {
        Transform panTransform = _panTransforms[index];
        Transform tiltTransform = _tiltTransforms[index];
        Transform spreadPanTransform = _spreadPanTransforms[index];
        Transform spreadTiltTransform = _spreadTiltTransforms[index];

        if (panTransform != null)
            panTransform.localRotation = Quaternion.AngleAxis(pan, Vector3.up);

        if (tiltTransform != null)
            tiltTransform.localRotation = Quaternion.AngleAxis(tilt, Vector3.left);

        if (spreadPanTransform != null)
            spreadPanTransform.localRotation = Quaternion.AngleAxis(spreadPan, Vector3.up);

        if (spreadTiltTransform != null)
            spreadTiltTransform.localRotation = Quaternion.AngleAxis(spreadTilt, Vector3.right);
    }

    private static Vector2 EvaluateAngles(UnifiedStageTemplate template, float time, int index, float range)
    {
        float pan = template.staticAngleOffset.x;
        float tilt = template.staticAngleOffset.y;

        switch (template.rotationMode)
        {
            case UnifiedStageController.RotationMode.Scan:
                pan += Mathf.Sin(time * template.rotationSpeed) * range;
                break;

            case UnifiedStageController.RotationMode.Circle:
                return CalculateCircleAngles(time, template.rotationSpeed, range, template.staticAngleOffset);

            case UnifiedStageController.RotationMode.VerticalSwing:
                tilt += Mathf.Sin(time * template.rotationSpeed) * range;
                break;

            case UnifiedStageController.RotationMode.Random:
            {
                float speed = Mathf.Max(template.rotationSpeed, 0.001f);
                pan += (Mathf.PerlinNoise(time * speed, index * 0.5f) - 0.5f) * 2f * range;
                tilt += (Mathf.PerlinNoise(index * 0.5f, time * speed) - 0.5f) * 2f * range;
                break;
            }

            case UnifiedStageController.RotationMode.Cross:
                pan += (index % 2 == 0 ? 90f : -90f);
                tilt += Mathf.Sin(time * template.rotationSpeed) * range;
                break;

            case UnifiedStageController.RotationMode.Target:
            case UnifiedStageController.RotationMode.FreezeFrame:
            case UnifiedStageController.RotationMode.Static:
            default:
                break;
        }

        return new Vector2(pan, tilt);
    }

    private static Vector2 CalculateCircleAngles(float time, float speed, float range, Vector2 staticOffset)
    {
        Vector3 panAxis = Vector3.up;
        Vector3 tiltAxis = Vector3.left;

        Quaternion centerPanQ = Quaternion.AngleAxis(staticOffset.x, panAxis);
        Quaternion centerTiltQ = Quaternion.AngleAxis(staticOffset.y, tiltAxis);
        Vector3 centerDir = centerPanQ * centerTiltQ * Vector3.up;

        Quaternion startTiltQ = Quaternion.AngleAxis(staticOffset.y + range, tiltAxis);
        Vector3 startEdge = centerPanQ * startTiltQ * Vector3.up;

        float thetaDeg = time * speed * 20f;
        Vector3 finalDir = Quaternion.AngleAxis(thetaDeg, centerDir) * startEdge;

        Vector3 panRef = Vector3.ProjectOnPlane(
            Quaternion.AngleAxis(90f, tiltAxis) * Vector3.up,
            panAxis
        ).normalized;

        Vector3 hProj = Vector3.ProjectOnPlane(finalDir, panAxis);
        float pan = hProj.sqrMagnitude < 0.0001f
            ? staticOffset.x
            : SignedAngleOnAxis(panRef, hProj, panAxis);

        Quaternion undoPan = Quaternion.AngleAxis(-pan, panAxis);
        Vector3 undone = undoPan * finalDir;
        float tilt = SignedAngleOnAxis(Vector3.up, undone, tiltAxis);

        return new Vector2(pan, tilt);
    }

    private static Vector2 EvaluateSpreadAngles(UnifiedStageTemplate template, float rootTime, float unitTime, int index)
    {
        float cyclePeriod = UnifiedStageBehaviour.GetMotionCyclePeriod(template.rotationMode, template.rotationSpeed);
        float cycleT = cyclePeriod > 0.0001f
            ? Mathf.Repeat(unitTime / cyclePeriod, 1f)
            : Mathf.Repeat(rootTime / 5f, 1f);

        float normalizedByLast = PreviewUnitCount > 1 ? (float)index / (PreviewUnitCount - 1) : 0f;
        float curveAngle = template.spreadAngleCurve != null ? template.spreadAngleCurve.Evaluate(cycleT) : 1f;
        float curveAngleByIndex = template.spreadAngleCurveByIndex != null
            ? template.spreadAngleCurveByIndex.Evaluate(normalizedByLast)
            : 1f;
        float spreadTilt = template.spreadAngle * curveAngle * curveAngleByIndex;

        float normalizedByCount = PreviewUnitCount > 1 ? (float)index / PreviewUnitCount : 0f;
        float baseSpreadPan = normalizedByCount * template.spreadArcRange;
        float curvePan = template.spreadPanCurve != null ? template.spreadPanCurve.Evaluate(cycleT) : 0f;
        float spreadPan = Mathf.DeltaAngle(0f, baseSpreadPan + curvePan * 360f);

        return new Vector2(spreadPan, spreadTilt);
    }

    private static Color EvaluateColor(UnifiedStageTemplate template, float rootTime, float unitTime, float unitDelay, int index)
    {
        Gradient gradient = template.lightGradient;
        Color baseColor;

        switch (template.colorSampleMode)
        {
            case UnifiedStageController.ColorSampleMode.MotionCycle:
            {
                float period = UnifiedStageBehaviour.GetMotionCyclePeriod(template.rotationMode, template.rotationSpeed);
                float t = period > 0.0001f ? Mathf.Repeat(unitTime / period, 1f) : Mathf.Repeat(rootTime / 5f, 1f);
                baseColor = gradient != null ? gradient.Evaluate(t) : Color.white;
                break;
            }

            case UnifiedStageController.ColorSampleMode.ClipProgress:
            {
                float duration = 5f;
                float delayShift = unitDelay / duration;
                float rawPhase = Mathf.Repeat(rootTime, duration) / duration - delayShift;
                float window = Mathf.Max(1f - delayShift, 0.0001f);
                baseColor = gradient != null ? gradient.Evaluate(Mathf.Clamp01(rawPhase / window)) : Color.white;
                break;
            }

            case UnifiedStageController.ColorSampleMode.BeatGradient:
            {
                float beatLen = 60f / Mathf.Max(template.bpm, 0.001f);
                float beatOffset = ComputeBeatOffset(template, index);
                float t = Mathf.Repeat((rootTime - beatOffset + template.beatPhaseOffset) / beatLen, 1f);
                baseColor = gradient != null ? gradient.Evaluate(t) : Color.white;
                break;
            }

            case UnifiedStageController.ColorSampleMode.BeatSnap:
            {
                baseColor = EvaluateBeatSnapColor(template, rootTime, index);
                break;
            }

            case UnifiedStageController.ColorSampleMode.AlongAudioSource:
            {
                float t = Mathf.PingPong(rootTime * Mathf.Max(template.sensitivity, 0.1f) * 0.25f, 1f);
                baseColor = gradient != null ? gradient.Evaluate(t) : Color.white;
                break;
            }

            default:
                baseColor = gradient != null ? gradient.Evaluate(Mathf.Repeat(rootTime / 5f, 1f)) : Color.white;
                break;
        }

        return baseColor * template.globalColor;
    }

    private static Color EvaluateBeatSnapColor(UnifiedStageTemplate template, float rootTime, int index)
    {
        if (template.beatSnapColors == null || template.beatSnapColors.Length == 0)
            return Color.white;

        float beatLen = 60f / Mathf.Max(template.bpm, 0.001f);
        float beatPosition = (rootTime + template.beatPhaseOffset) / beatLen;
        int beatIndex = Mathf.FloorToInt(beatPosition);
        int colorIndex = PositiveModulo(beatIndex + ComputeBeatSnapOffset(template, index), template.beatSnapColors.Length);
        Color color = template.beatSnapColors[colorIndex];

        float transition = Mathf.Max(template.beatSnapTransitionTime, 0f);
        if (transition > 0f && template.beatSnapColors.Length > 1)
        {
            float transitionLen = Mathf.Min(transition, beatLen);
            float beatLocal = Mathf.Repeat(beatPosition, 1f) * beatLen;
            float transitionStart = beatLen - transitionLen;

            if (beatLocal >= transitionStart)
            {
                int nextIndex = PositiveModulo(beatIndex + 1 + ComputeBeatSnapOffset(template, index), template.beatSnapColors.Length);
                color = Color.Lerp(color, template.beatSnapColors[nextIndex], Mathf.InverseLerp(transitionStart, beatLen, beatLocal));
            }
        }

        return color;
    }

    private static float ComputeBeatOffset(UnifiedStageTemplate template, int index)
    {
        float normalized = PreviewUnitCount > 1 ? (float)index / (PreviewUnitCount - 1) : 0f;
        float offset = 0f;

        if (template.beatLightDelayFactor > 0f)
            offset += EvaluateCurve(template.beatLightDelayCurve, normalized) * template.beatLightDelayFactor;

        return offset;
    }

    private static int ComputeBeatSnapOffset(UnifiedStageTemplate template, int index)
    {
        if (template.beatLightDelayFactor <= 0f)
            return 0;

        int rank = ComputeCurveRank(index, PreviewUnitCount, template.beatLightDelayCurve);
        return Mathf.FloorToInt(rank / template.beatLightDelayFactor);
    }

    private static int ComputeCurveRank(int index, int count, AnimationCurve curve)
    {
        float current = EvaluateCurveAtIndex(curve, index, count);
        int rank = 0;

        for (int i = 0; i < count; i++)
        {
            if (i == index)
                continue;

            float value = EvaluateCurveAtIndex(curve, i, count);
            if (value < current || (Mathf.Approximately(value, current) && i < index))
                rank++;
        }

        return rank;
    }

    private static float EvaluateCurveAtIndex(AnimationCurve curve, int index, int count)
    {
        float normalized = count > 1 ? (float)index / (count - 1) : 0f;
        return EvaluateCurve(curve, normalized);
    }

    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        return curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
    }

    private static float SignedAngleOnAxis(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 fromOnPlane = Vector3.ProjectOnPlane(from, axis);
        Vector3 toOnPlane = Vector3.ProjectOnPlane(to, axis);

        if (fromOnPlane.sqrMagnitude < 0.000001f || toOnPlane.sqrMagnitude < 0.000001f)
            return 0f;

        return Vector3.SignedAngle(fromOnPlane, toOnPlane, axis);
    }

    private static int PositiveModulo(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
    }

    private void ApplyColor(int unitIndex, Color color)
    {
        List<MeshRenderer> renderers = _renderers[unitIndex];
        if (renderers == null)
            return;

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorShaderId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private static List<MeshRenderer> FindPreviewRenderers(GameObject root)
    {
        MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(true);
        List<MeshRenderer> preferred = all
            .Where(r => r != null && r.name.IndexOf("cylinder", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        return preferred.Count > 0 ? preferred : all.Where(r => r != null).ToList();
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform result = FindChildTransform(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void DisableLights(GameObject root)
    {
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;
    }

    private static void DrawCentered(Rect rect, string text)
    {
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        EditorGUI.LabelField(rect, text, EditorStyles.centeredGreyMiniLabel);
    }
}
#endif
