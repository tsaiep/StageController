using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class KeyEventTrigger : MonoBehaviour
{
    public enum KeyTriggerType
    {
        KeyDown,
        Key,
        KeyUp
    }

    [System.Serializable]
    public class KeyEventModule
    {
        public KeyCode key = KeyCode.A;
        public KeyTriggerType triggerType = KeyTriggerType.KeyDown;
        public UnityEvent onTrigger;

        [System.NonSerialized] public float lastTriggeredRealtime = -999f;
    }

    [Header("Inspector")]
    [Min(0f)] [SerializeField] private float inspectorHighlightSeconds = 1f;

    [SerializeField] private List<KeyEventModule> keyEventModules = new List<KeyEventModule>();

    private void Update()
    {
        for (int i = 0; i < keyEventModules.Count; i++)
        {
            KeyEventModule module = keyEventModules[i];

            if (module == null || !ShouldTrigger(module))
            {
                continue;
            }

            module.lastTriggeredRealtime = Time.realtimeSinceStartup;
#if UNITY_EDITOR
            RequestEditorRepaint();
#endif
            Debug.Log(
                $"{nameof(KeyEventTrigger)} on {name}: Event triggered by {module.key} at {System.DateTime.Now:HH:mm:ss.fff} (Unity Time: {Time.time:F3}s).",
                this);
            module.onTrigger?.Invoke();
        }
    }

    private bool ShouldTrigger(KeyEventModule module)
    {
        switch (module.triggerType)
        {
            case KeyTriggerType.KeyDown:
                return Input.GetKeyDown(module.key);
            case KeyTriggerType.Key:
                return Input.GetKey(module.key);
            case KeyTriggerType.KeyUp:
                return Input.GetKeyUp(module.key);
            default:
                return false;
        }
    }

    private void OnValidate()
    {
        inspectorHighlightSeconds = Mathf.Max(0f, inspectorHighlightSeconds);
    }

#if UNITY_EDITOR
    public float EditorInspectorHighlightSeconds => inspectorHighlightSeconds;

    public KeyEventModule GetEditorModule(int index)
    {
        if (index < 0 || index >= keyEventModules.Count)
        {
            return null;
        }

        return keyEventModules[index];
    }

    public bool HasActiveInspectorHighlight()
    {
        if (!Application.isPlaying || inspectorHighlightSeconds <= 0f)
        {
            return false;
        }

        float currentTime = Time.realtimeSinceStartup;

        for (int i = 0; i < keyEventModules.Count; i++)
        {
            KeyEventModule module = keyEventModules[i];
            if (module != null && currentTime - module.lastTriggeredRealtime <= inspectorHighlightSeconds + 0.1f)
            {
                return true;
            }
        }

        return false;
    }

    private static void RequestEditorRepaint()
    {
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(KeyEventTrigger))]
public class KeyEventTriggerEditor : Editor
{
    private SerializedProperty _inspectorHighlightSeconds;
    private SerializedProperty _keyEventModules;

    private void OnEnable()
    {
        _inspectorHighlightSeconds = serializedObject.FindProperty("inspectorHighlightSeconds");
        _keyEventModules = serializedObject.FindProperty("keyEventModules");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_inspectorHighlightSeconds, new GUIContent("Inspector Highlight Seconds"));
        EditorGUILayout.PropertyField(_keyEventModules, true);

        serializedObject.ApplyModifiedProperties();
    }

    public override bool RequiresConstantRepaint()
    {
        KeyEventTrigger trigger = target as KeyEventTrigger;
        return trigger != null && trigger.HasActiveInspectorHighlight();
    }
}

[CustomPropertyDrawer(typeof(KeyEventTrigger.KeyEventModule))]
public class KeyEventModuleDrawer : PropertyDrawer
{
    private static readonly KeyCode[] AllowedKeys =
    {
        KeyCode.Alpha0,
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8,
        KeyCode.Alpha9,
        KeyCode.Keypad0,
        KeyCode.Keypad1,
        KeyCode.Keypad2,
        KeyCode.Keypad3,
        KeyCode.Keypad4,
        KeyCode.Keypad5,
        KeyCode.Keypad6,
        KeyCode.Keypad7,
        KeyCode.Keypad8,
        KeyCode.Keypad9,
        KeyCode.A,
        KeyCode.B,
        KeyCode.C,
        KeyCode.D,
        KeyCode.E,
        KeyCode.F,
        KeyCode.G,
        KeyCode.H,
        KeyCode.I,
        KeyCode.J,
        KeyCode.K,
        KeyCode.L,
        KeyCode.M,
        KeyCode.N,
        KeyCode.O,
        KeyCode.P,
        KeyCode.Q,
        KeyCode.R,
        KeyCode.S,
        KeyCode.T,
        KeyCode.U,
        KeyCode.V,
        KeyCode.W,
        KeyCode.X,
        KeyCode.Y,
        KeyCode.Z,
        KeyCode.Return,
        KeyCode.KeypadEnter,
        KeyCode.Tab,
        KeyCode.LeftShift,
        KeyCode.RightShift,
        KeyCode.LeftControl,
        KeyCode.RightControl,
        KeyCode.LeftAlt,
        KeyCode.RightAlt
    };

    private static readonly GUIContent[] AllowedKeyLabels =
    {
        new GUIContent("0"),
        new GUIContent("1"),
        new GUIContent("2"),
        new GUIContent("3"),
        new GUIContent("4"),
        new GUIContent("5"),
        new GUIContent("6"),
        new GUIContent("7"),
        new GUIContent("8"),
        new GUIContent("9"),
        new GUIContent("Numpad 0"),
        new GUIContent("Numpad 1"),
        new GUIContent("Numpad 2"),
        new GUIContent("Numpad 3"),
        new GUIContent("Numpad 4"),
        new GUIContent("Numpad 5"),
        new GUIContent("Numpad 6"),
        new GUIContent("Numpad 7"),
        new GUIContent("Numpad 8"),
        new GUIContent("Numpad 9"),
        new GUIContent("A"),
        new GUIContent("B"),
        new GUIContent("C"),
        new GUIContent("D"),
        new GUIContent("E"),
        new GUIContent("F"),
        new GUIContent("G"),
        new GUIContent("H"),
        new GUIContent("I"),
        new GUIContent("J"),
        new GUIContent("K"),
        new GUIContent("L"),
        new GUIContent("M"),
        new GUIContent("N"),
        new GUIContent("O"),
        new GUIContent("P"),
        new GUIContent("Q"),
        new GUIContent("R"),
        new GUIContent("S"),
        new GUIContent("T"),
        new GUIContent("U"),
        new GUIContent("V"),
        new GUIContent("W"),
        new GUIContent("X"),
        new GUIContent("Y"),
        new GUIContent("Z"),
        new GUIContent("Enter"),
        new GUIContent("Numpad Enter"),
        new GUIContent("Tab"),
        new GUIContent("Left Shift"),
        new GUIContent("Right Shift"),
        new GUIContent("Left Ctrl"),
        new GUIContent("Right Ctrl"),
        new GUIContent("Left Alt"),
        new GUIContent("Right Alt")
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        DrawTriggeredHighlight(position, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            SerializedProperty keyProperty = property.FindPropertyRelative("key");
            SerializedProperty triggerTypeProperty = property.FindPropertyRelative("triggerType");
            SerializedProperty onTriggerProperty = property.FindPropertyRelative("onTrigger");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect keyRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
            Rect triggerRect = new Rect(position.x, keyRect.y + lineHeight + spacing, position.width, lineHeight);
            Rect eventRect = new Rect(position.x, triggerRect.y + lineHeight + spacing, position.width, EditorGUI.GetPropertyHeight(onTriggerProperty, true));

            DrawAllowedKeyPopup(keyRect, keyProperty);
            EditorGUI.PropertyField(triggerRect, triggerTypeProperty);
            EditorGUI.PropertyField(eventRect, onTriggerProperty, true);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.isExpanded)
        {
            SerializedProperty onTriggerProperty = property.FindPropertyRelative("onTrigger");
            height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2f;
            height += EditorGUI.GetPropertyHeight(onTriggerProperty, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private static void DrawAllowedKeyPopup(Rect position, SerializedProperty keyProperty)
    {
        KeyCode currentKey = (KeyCode)keyProperty.intValue;
        int selectedIndex = System.Array.IndexOf(AllowedKeys, currentKey);

        if (selectedIndex < 0)
        {
            GUIContent[] labels = new GUIContent[AllowedKeyLabels.Length + 1];
            labels[0] = new GUIContent($"Unsupported ({currentKey})");
            System.Array.Copy(AllowedKeyLabels, 0, labels, 1, AllowedKeyLabels.Length);

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(position, new GUIContent("Key"), 0, labels);
            if (EditorGUI.EndChangeCheck() && nextIndex > 0)
            {
                keyProperty.intValue = (int)AllowedKeys[nextIndex - 1];
            }

            return;
        }

        EditorGUI.BeginChangeCheck();
        selectedIndex = EditorGUI.Popup(position, new GUIContent("Key"), selectedIndex, AllowedKeyLabels);
        if (EditorGUI.EndChangeCheck())
        {
            keyProperty.intValue = (int)AllowedKeys[selectedIndex];
        }
    }

    private static void DrawTriggeredHighlight(Rect position, SerializedProperty property)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        KeyEventTrigger trigger = property.serializedObject.targetObject as KeyEventTrigger;
        if (trigger == null || trigger.EditorInspectorHighlightSeconds <= 0f)
        {
            return;
        }

        int moduleIndex = GetArrayElementIndex(property.propertyPath);
        KeyEventTrigger.KeyEventModule module = trigger.GetEditorModule(moduleIndex);
        if (module == null)
        {
            return;
        }

        float elapsed = Time.realtimeSinceStartup - module.lastTriggeredRealtime;
        if (elapsed < 0f || elapsed > trigger.EditorInspectorHighlightSeconds)
        {
            return;
        }

        float remainingRatio = 1f - Mathf.Clamp01(elapsed / trigger.EditorInspectorHighlightSeconds);
        Color highlightColor = new Color(1f, 0.78f, 0.18f, Mathf.Lerp(0f, 0.36f, remainingRatio));
        EditorGUI.DrawRect(position, highlightColor);
    }

    private static int GetArrayElementIndex(string propertyPath)
    {
        int startIndex = propertyPath.LastIndexOf('[');
        int endIndex = propertyPath.LastIndexOf(']');

        if (startIndex < 0 || endIndex <= startIndex)
        {
            return -1;
        }

        string indexText = propertyPath.Substring(startIndex + 1, endIndex - startIndex - 1);
        return int.TryParse(indexText, out int index) ? index : -1;
    }
}
#endif
