#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[CustomEditor(typeof(LightstripTemplate))]
public class LightstripTemplateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty tagsProp = serializedObject.FindProperty("tags");
        LightstripTemplate template = (LightstripTemplate)target;

        LightstripTemplateEditorTools.DrawTemplateTags(template, tagsProp);

        EditorGUILayout.Space(6);
        DrawPropertiesExcluding(serializedObject, "m_Script", "tags");

        serializedObject.ApplyModifiedProperties();
    }
}

internal static class LightstripTemplateEditorTools
{
    public static void DrawTemplateTags(LightstripTemplate template, SerializedProperty tagsProp)
    {
        EditorGUILayout.LabelField("Template Tags", EditorStyles.boldLabel);

        if (tagsProp == null)
        {
            EditorGUILayout.HelpBox("The LightstripTemplate.tags field was not found.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (template.tags != null)
            {
                for (int i = 0; i < template.tags.Count; i++)
                {
                    LightstripTemplateTagSO tag = template.tags[i];
                    if (tag == null)
                        continue;

                    if (GUILayout.Button("#" + tag.name + "  X", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        Undo.RecordObject(template, "Remove Lightstrip Template Tag");
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
            PopupWindow.Show(buttonRect, new LightstripTemplateTagSearchPopup(tag =>
            {
                if (tag == null)
                    return;

                if (template.tags == null)
                    template.tags = new List<LightstripTemplateTagSO>();

                if (template.tags.Contains(tag))
                    return;

                Undo.RecordObject(template, "Add Lightstrip Template Tag");
                template.tags.Add(tag);
                EditorUtility.SetDirty(template);
            }, template.tags));
        }
    }

    public static string GetTagText(LightstripTemplate template)
    {
        if (template == null || template.tags == null || template.tags.Count == 0)
            return "";

        List<string> names = new List<string>();
        foreach (LightstripTemplateTagSO tag in template.tags)
        {
            if (tag != null)
                names.Add("#" + tag.name);
        }

        return string.Join(" ", names);
    }

    public static List<LightstripTemplate> FindTemplates()
    {
        HashSet<LightstripTemplate> templates = new HashSet<LightstripTemplate>();
        string[] guids = AssetDatabase.FindAssets("t:LightstripTemplate");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LightstripTemplate template = AssetDatabase.LoadAssetAtPath<LightstripTemplate>(path);
            if (template != null)
                templates.Add(template);
        }

        return templates
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<LightstripTemplateTagSO> FindTags()
    {
        HashSet<LightstripTemplateTagSO> tags = new HashSet<LightstripTemplateTagSO>();
        string[] guids = AssetDatabase.FindAssets("t:LightstripTemplateTagSO");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LightstripTemplateTagSO tag = AssetDatabase.LoadAssetAtPath<LightstripTemplateTagSO>(path);
            if (tag != null)
                tags.Add(tag);
        }

        return tags
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static LightstripMBPControl FindBoundController(LightstripClip clip)
    {
        LightstripMBPControl controller = FindBoundControllerFromDirector(TimelineEditor.inspectedDirector, clip);
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

    public static bool IsFallbackController(LightstripClip clip, LightstripMBPControl controller)
    {
        if (controller == null)
            return false;

        LightstripMBPControl timelineController = FindBoundControllerFromDirector(TimelineEditor.inspectedDirector, clip);
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

    private static LightstripMBPControl FindBoundControllerFromDirector(PlayableDirector director, LightstripClip clip)
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

                return director.GetGenericBinding(track) as LightstripMBPControl;
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

    private static LightstripMBPControl FindSinglePreviewControllerFallback()
    {
        LightstripMBPControl[] controllers = UnityEngine.Object.FindObjectsByType<LightstripMBPControl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<LightstripMBPControl> candidates = controllers
            .Where(controller => controller != null && controller.templatePreviewPrefab != null)
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static void ApplyTemplateToClip(LightstripClip clip, LightstripTemplate template)
    {
        if (clip == null)
            return;

        ApplyTemplateToClip(
            clip,
            template,
            clip.applyTemplateManualModeSettings,
            clip.applyTemplateColorSettings,
            clip.applyTemplateAnimationSettings
        );
    }

    public static void ApplyTemplateToClip(
        LightstripClip clip,
        LightstripTemplate template,
        bool applyManualModeSettings,
        bool applyColorSettings,
        bool applyAnimationSettings)
    {
        if (clip == null || template == null)
            return;

        Undo.RecordObject(clip, "Apply Lightstrip Template");
        clip.applyTemplateManualModeSettings = applyManualModeSettings;
        clip.applyTemplateColorSettings = applyColorSettings;
        clip.applyTemplateAnimationSettings = applyAnimationSettings;
        clip.ApplyTemplateValues(template);
        clip.selectedTemplate = template;

        TimelineClip timelineClip = TimelineEditor.selectedClips.FirstOrDefault(c => c.asset == clip);
        if (timelineClip != null)
            timelineClip.displayName = template.name;

        EditorUtility.SetDirty(clip);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
}

internal class LightstripTemplateTagSearchPopup : PopupWindowContent
{
    private readonly Action<LightstripTemplateTagSO> _onPicked;
    private readonly HashSet<LightstripTemplateTagSO> _excludedTags;
    private readonly List<LightstripTemplateTagSO> _allTags;
    private string _search = "";
    private Vector2 _scroll;

    public LightstripTemplateTagSearchPopup(Action<LightstripTemplateTagSO> onPicked, IEnumerable<LightstripTemplateTagSO> excludedTags = null)
    {
        _onPicked = onPicked;
        _excludedTags = excludedTags != null
            ? new HashSet<LightstripTemplateTagSO>(excludedTags.Where(t => t != null))
            : new HashSet<LightstripTemplateTagSO>();
        _allTags = LightstripTemplateEditorTools.FindTags();
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(260f, 320f);
    }

    public override void OnGUI(Rect rect)
    {
        EditorGUILayout.LabelField("Lightstrip Template Tags", EditorStyles.boldLabel);
        _search = EditorGUILayout.TextField(_search, EditorStyles.textField);

        EditorGUILayout.Space(4);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        string keyword = string.IsNullOrWhiteSpace(_search) ? "" : _search.Trim().ToLowerInvariant();
        List<LightstripTemplateTagSO> filtered = _allTags
            .Where(tag => tag != null)
            .Where(tag => !_excludedTags.Contains(tag))
            .Where(tag => keyword.Length == 0 || tag.name.ToLowerInvariant().Contains(keyword))
            .ToList();

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching tags. Create one from Assets/Create/Stage Control/Lightstrip Template Tag.", MessageType.Info);
        }
        else
        {
            foreach (LightstripTemplateTagSO tag in filtered)
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

internal class LightstripTemplateSelectorWindow : EditorWindow
{
    private LightstripClip _clip;
    private LightstripMBPControl _controller;
    private LightstripTemplate _selectedTemplate;
    private LightstripTemplatePreviewRenderer _previewRenderer;

    private List<LightstripTemplate> _allTemplates = new List<LightstripTemplate>();
    private List<LightstripTemplateTagSO> _allTags = new List<LightstripTemplateTagSO>();
    private List<LightstripTemplate> _filteredTemplates = new List<LightstripTemplate>();
    private readonly List<LightstripTemplateTagSO> _selectedTags = new List<LightstripTemplateTagSO>();

    private string _search = "";
    private bool _filterDirty = true;
    private Vector2 _templateScroll;
    private Vector2 _tagScroll;
    private Vector2 _mainScroll;
    private bool _applyManualModeSettings = true;
    private bool _applyColorSettings = true;
    private bool _applyAnimationSettings = true;
    private double _lastPreviewRepaintTime;

    public static void Open(LightstripClip clip)
    {
        LightstripTemplateSelectorWindow window = GetWindow<LightstripTemplateSelectorWindow>("Lightstrip Template Selector");
        window.minSize = new Vector2(750f, 600f);
        window.SetContext(clip);
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        _previewRenderer = new LightstripTemplatePreviewRenderer();
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

    private void SetContext(LightstripClip clip)
    {
        _clip = clip;
        _controller = LightstripTemplateEditorTools.FindBoundController(clip);
        _selectedTemplate = clip != null ? clip.selectedTemplate : null;

        if (clip != null)
        {
            _applyManualModeSettings = clip.applyTemplateManualModeSettings;
            _applyColorSettings = clip.applyTemplateColorSettings;
            _applyAnimationSettings = clip.applyTemplateAnimationSettings;
        }

        RefreshDatabase();
        Repaint();
    }

    private void SyncContextWithTimelineSelection()
    {
        LightstripClip selectedClip = GetSelectedTimelineLightstripClip();
        if (selectedClip == null || selectedClip == _clip)
            return;

        SetContext(selectedClip);
    }

    private static LightstripClip GetSelectedTimelineLightstripClip()
    {
        TimelineClip selectedTimelineClip = TimelineEditor.selectedClips
            .FirstOrDefault(clip => clip != null && clip.asset is LightstripClip);

        return selectedTimelineClip != null ? selectedTimelineClip.asset as LightstripClip : null;
    }

    private void RefreshDatabase()
    {
        _allTemplates = LightstripTemplateEditorTools.FindTemplates();
        _allTags = LightstripTemplateEditorTools.FindTags();
        _filterDirty = true;
    }

    private void OnGUI()
    {
        if (_previewRenderer == null)
            _previewRenderer = new LightstripTemplatePreviewRenderer();

        SyncContextWithTimelineSelection();

        if (_clip != null && _controller == null)
            _controller = LightstripTemplateEditorTools.FindBoundController(_clip);

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
        EditorGUILayout.LabelField("Lightstrip Template Selector", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Target Clip", _clip, typeof(LightstripClip), false);
            EditorGUILayout.ObjectField("Bound Controller", _controller, typeof(LightstripMBPControl), true);
            EditorGUILayout.ObjectField("Template Preview Prefab", GetPreviewPrefab(), typeof(GameObject), false);
        }

        if (_clip != null)
        {
            EditorGUI.BeginChangeCheck();
            GameObject previewPrefab = (GameObject)EditorGUILayout.ObjectField("Clip Preview Prefab Override", _clip.templatePreviewPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_clip, "Set Lightstrip Preview Prefab");
                _clip.templatePreviewPrefab = previewPrefab;
                EditorUtility.SetDirty(_clip);
            }
        }

        if (_clip == null)
            EditorGUILayout.HelpBox("No LightstripClip is selected. Select a LightstripClip in the Timeline, or open this window from a clip inspector.", MessageType.Error);
        else if (_controller == null && _clip.templatePreviewPrefab == null)
            EditorGUILayout.HelpBox("No LightstripMBPControl binding was found for this Timeline track, no clip override prefab is assigned, and no single scene controller with Template Preview Prefab was available as fallback.", MessageType.Warning);
        else if (_controller != null && LightstripTemplateEditorTools.IsFallbackController(_clip, _controller))
            EditorGUILayout.HelpBox("Using the only scene LightstripMBPControl that has Template Preview Prefab assigned. This is a fallback because the Timeline track binding was not found.", MessageType.Info);
        else if (GetPreviewPrefab() == null)
            EditorGUILayout.HelpBox("Assign Template Preview Prefab on the bound LightstripMBPControl, or assign Clip Preview Prefab Override here.", MessageType.Warning);
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
            EditorGUILayout.HelpBox("No LightstripTemplateTagSO assets were found. Create tags from Assets/Create/Stage Control/Lightstrip Template Tag.", MessageType.Info);
            return;
        }

        _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, false, false, GUILayout.Height(32f));
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTagChip(null, "All");
            foreach (LightstripTemplateTagSO tag in _allTags)
                DrawTagChip(tag, "#" + tag.name);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTagChip(LightstripTemplateTagSO tag, string label)
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
        List<LightstripTemplate> filtered = GetFilteredTemplates();
        EditorGUILayout.LabelField("Templates (" + filtered.Count + ")", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(420f));
        _templateScroll = EditorGUILayout.BeginScrollView(_templateScroll);

        if (filtered.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching templates.", MessageType.Info);
        }
        else
        {
            foreach (LightstripTemplate template in filtered)
                DrawTemplateRow(template);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    private void DrawTemplateRow(LightstripTemplate template)
    {
        bool selected = _selectedTemplate == template;
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.45f, 0.7f, 1f, 1f) : old;

        if (GUILayout.Button(GUIContent.none, EditorStyles.helpBox, GUILayout.Height(48f), GUILayout.ExpandWidth(true)))
            _selectedTemplate = template;

        Rect row = GUILayoutUtility.GetLastRect();
        GUI.backgroundColor = old;

        GUI.Label(new Rect(row.x + 8f, row.y + 5f, row.width - 16f, 18f), template.name, selected ? EditorStyles.boldLabel : EditorStyles.label);
        string tags = LightstripTemplateEditorTools.GetTagText(template);
        GUI.Label(new Rect(row.x + 8f, row.y + 25f, row.width - 16f, 16f), string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
    }

    private void DrawPreviewAndActions()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        EditorGUILayout.LabelField("Selected Template", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(_selectedTemplate, typeof(LightstripTemplate), false);
        }

        if (_selectedTemplate != null)
        {
            string tags = LightstripTemplateEditorTools.GetTagText(_selectedTemplate);
            EditorGUILayout.LabelField("Tags", string.IsNullOrEmpty(tags) ? "No tags" : tags, EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Apply Options", EditorStyles.boldLabel);
        _applyManualModeSettings = EditorGUILayout.Toggle("Apply Manual Mode Setting", _applyManualModeSettings);
        _applyColorSettings = EditorGUILayout.Toggle("Apply Color Setting", _applyColorSettings);
        _applyAnimationSettings = EditorGUILayout.Toggle("Apply Animation Setting", _applyAnimationSettings);

        Rect previewRect = GUILayoutUtility.GetRect(10f, 250f, GUILayout.ExpandWidth(true), GUILayout.Height(250f));
        _previewRenderer.Render(previewRect, EditorStyles.helpBox, _selectedTemplate, GetPreviewPrefab());

        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _clip != null && _selectedTemplate != null;
            if (GUILayout.Button("Apply Template To Clip", GUILayout.Height(30f)))
            {
                LightstripTemplateEditorTools.ApplyTemplateToClip(
                    _clip,
                    _selectedTemplate,
                    _applyManualModeSettings,
                    _applyColorSettings,
                    _applyAnimationSettings
                );
                Repaint();
                GUI.changed = true;
            }

            GUI.enabled = true;

            if (GUILayout.Button("Ping", GUILayout.Width(70f), GUILayout.Height(30f)) && _selectedTemplate != null)
                EditorGUIUtility.PingObject(_selectedTemplate);
        }

        EditorGUILayout.EndVertical();
    }

    private GameObject GetPreviewPrefab()
    {
        if (_clip != null && _clip.templatePreviewPrefab != null)
            return _clip.templatePreviewPrefab;

        return _controller != null ? _controller.templatePreviewPrefab : null;
    }

    private List<LightstripTemplate> GetFilteredTemplates()
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

    private bool MatchesTags(LightstripTemplate template)
    {
        if (_selectedTags.Count == 0)
            return true;

        if (template.tags == null)
            return false;

        foreach (LightstripTemplateTagSO tag in _selectedTags)
        {
            if (tag != null && !template.tags.Contains(tag))
                return false;
        }

        return true;
    }

    private static bool MatchesSearch(LightstripTemplate template, string keyword)
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

internal sealed class LightstripTemplatePreviewRenderer : IDisposable
{
    private static readonly Vector3 PreviewCameraTarget = Vector3.zero;

    private PreviewRenderUtility _preview;
    private GameObject _sourcePrefab;
    private GameObject _instance;
    private LightstripMBPControl _controller;
    private LightstripTemplate _lastAppliedTemplate;
    private double _startTime;
    private float _cameraDistance = 5f;

    public LightstripTemplatePreviewRenderer()
    {
        _startTime = EditorApplication.timeSinceStartup;
    }

    public void Dispose()
    {
        DestroyPreview();
    }

    public void Render(Rect rect, GUIStyle background, LightstripTemplate template, GameObject previewPrefab)
    {
        if (rect.width <= 1f || rect.height <= 1f)
            return;

        if (template == null)
        {
            if (Event.current.type == EventType.Repaint)
                DrawCentered(rect, "Select a template to preview.");
            return;
        }

        if (previewPrefab == null)
        {
            if (Event.current.type == EventType.Repaint)
                DrawCentered(rect, "Assign Template Preview Prefab on the bound LightstripMBPControl or Clip override.");
            return;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        EnsurePreview(previewPrefab);

        if (_controller == null)
        {
            DrawCentered(rect, "Preview prefab must contain LightstripMBPControl.");
            return;
        }

        ApplyTemplate(template);
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
        _preview.camera.nearClipPlane = 0.01f;
        _preview.camera.farClipPlane = 200f;

        if (_preview.lights != null)
        {
            if (_preview.lights.Length > 0 && _preview.lights[0] != null)
                _preview.lights[0].intensity = 0f;

            if (_preview.lights.Length > 1 && _preview.lights[1] != null)
                _preview.lights[1].intensity = 0f;
        }

        _instance = UnityEngine.Object.Instantiate(prefab);
        _instance.name = "Lightstrip Template Preview";
        _instance.hideFlags = HideFlags.HideAndDontSave;
        _instance.transform.position = Vector3.zero;
        _instance.transform.rotation = Quaternion.identity;
        DisableLights(_instance);

        _controller = _instance.GetComponent<LightstripMBPControl>();
        if (_controller == null)
            _controller = _instance.GetComponentInChildren<LightstripMBPControl>(true);

        EnsurePreviewTargets();
        _preview.AddSingleGO(_instance);
        UpdateCameraDistance();
        UpdateCamera();
    }

    private void DestroyPreview()
    {
        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }

        if (_instance != null)
            UnityEngine.Object.DestroyImmediate(_instance);

        _instance = null;
        _controller = null;
        _sourcePrefab = null;
        _lastAppliedTemplate = null;
    }

    private void ApplyTemplate(LightstripTemplate template)
    {
        if (_controller == null || template == null)
            return;

        if (_lastAppliedTemplate != template)
        {
            _controller.MarkGradientDirty();
            _lastAppliedTemplate = template;
        }

        float normalizedTime = Mathf.Repeat((float)(EditorApplication.timeSinceStartup - _startTime) / 4f, 1f);
        float manualModeControl = template.manualModeControl != null ? template.manualModeControl.Evaluate(normalizedTime) : 0f;
        manualModeControl = manualModeControl - Mathf.Floor(manualModeControl);

        _controller.ApplyTimelineValues(
            template.color,
            template.gradient,
            LightstripMBPControl.GetGradientContentHash(template.gradient),
            template.colorMultiplier,
            template.manualMode ? 1f : 0f,
            manualModeControl,
            template.scrollingModeWeight,
            template.scrollingPingPongMode,
            template.scrollingFromCenter,
            template.linearMode,
            template.sparklingModeWeight,
            template.sparklingModeRandomWeight,
            template.scrollingSpeed,
            template.scrollingFrequency,
            template.scrollingIntervalDuration,
            template.scrollingHoldDuration,
            template.scrollingHeadLean,
            template.scrollingSmoothFactor,
            template.sparklingSpeed,
            template.sparklingSmoothFactor
        );
    }

    private void EnsurePreviewTargets()
    {
        if (_controller == null || _instance == null)
            return;

        List<LightstripMBPControl.LightstripRendererSettings> targets = _controller.LightstripRenderers;
        if (targets == null)
            return;

        bool hasUsableTarget = false;
        for (int i = 0; i < targets.Count; i++)
        {
            LightstripMBPControl.LightstripRendererSettings target = targets[i];
            if (target != null &&
                target.renderer != null &&
                target.renderer.transform.IsChildOf(_instance.transform))
            {
                hasUsableTarget = true;
                break;
            }
        }

        if (hasUsableTarget)
            return;

        targets.Clear();

        Renderer[] renderers = _instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            targets.Add(new LightstripMBPControl.LightstripRendererSettings
            {
                renderer = renderer,
                lightUnitCount = 12f
            });
        }

        _controller.MarkGradientDirty();
        _controller.MarkDirty();
    }

    private void UpdateCameraDistance()
    {
        Bounds bounds = CalculateBounds(_instance);
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        _cameraDistance = radius * 2.4f;
    }

    private void UpdateCamera()
    {
        if (_preview == null || _preview.camera == null)
            return;

        Bounds bounds = CalculateBounds(_instance);
        Vector3 target = bounds.size.sqrMagnitude > 0.0001f ? bounds.center : PreviewCameraTarget;
        _preview.camera.transform.position = target + Vector3.back * _cameraDistance;
        _preview.camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        if (root == null)
            return new Bounds(Vector3.zero, Vector3.one);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds : new Bounds(root.transform.position, Vector3.one);
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
