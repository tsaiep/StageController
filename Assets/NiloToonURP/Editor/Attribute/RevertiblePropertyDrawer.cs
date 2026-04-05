// For how to use [Revertible], see RevertibleAttribute.cs
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System;
using System.Linq;

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(RevertibleAttribute))]
public class RevertiblePropertyDrawer : PropertyDrawer
{
    private const float BUTTON_WIDTH = 20f;
    private const float SPACING = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Get the default value
        object defaultValue = GetDefaultValue(property);
        
        // Check if current value differs from default
        bool isDifferentFromDefault = !IsEqualToDefault(property, defaultValue);
        
        // Calculate rects
        Rect propertyRect = position;
        if (isDifferentFromDefault)
        {
            propertyRect.width -= BUTTON_WIDTH + SPACING;
        }
        
        // Draw the property field with other attributes (like Range)
        DrawPropertyWithOtherAttributes(propertyRect, property, label);
        
        // Draw revert button if value is different from default
        if (isDifferentFromDefault)
        {
            Rect buttonRect = new Rect(
                position.x + position.width - BUTTON_WIDTH,
                position.y,
                BUTTON_WIDTH,
                EditorGUIUtility.singleLineHeight
            );
            
            // Create tooltip
            GUIContent buttonContent = new GUIContent("↺", "Revert to default value");
            
            // Alternative symbols you can use:
            // "⟲" - circle arrow
            // "↶" - anticlockwise arrow
            // "⎌" - undo symbol
            // "✕" - X mark
            // "⌫" - delete/backspace
            // "◀" - back arrow
            
            // Style the button
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.padding = new RectOffset(0, 0, 0, 0);
            
            // Use orange tint for visibility
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.8f, 0.5f, 1f); // Orange tint
            
            if (GUI.Button(buttonRect, buttonContent, buttonStyle))
            {
                RevertToDefault(property, defaultValue);
            }
            
            GUI.color = oldColor;
        }
    }
    
    private void DrawPropertyWithOtherAttributes(Rect position, SerializedProperty property, GUIContent label)
    {
        // Get the field info
        FieldInfo fieldInfo = GetFieldInfo(property);
        if (fieldInfo == null)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }
        
        // Check for Range attribute
        RangeAttribute rangeAttribute = fieldInfo.GetCustomAttribute<RangeAttribute>();
        if (rangeAttribute != null)
        {
            if (property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.Slider(position, property, rangeAttribute.min, rangeAttribute.max, label);
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUI.IntSlider(position, property, (int)rangeAttribute.min, (int)rangeAttribute.max, label);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
            return;
        }
        
        // Check for Min attribute (Unity 2021.2+)
        #if UNITY_2021_2_OR_NEWER
        MinAttribute minAttribute = fieldInfo.GetCustomAttribute<MinAttribute>();
        if (minAttribute != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, label, true);
            if (EditorGUI.EndChangeCheck())
            {
                if (property.propertyType == SerializedPropertyType.Float)
                {
                    property.floatValue = Mathf.Max(property.floatValue, minAttribute.min);
                }
                else if (property.propertyType == SerializedPropertyType.Integer)
                {
                    property.intValue = Mathf.Max(property.intValue, (int)minAttribute.min);
                }
            }
            return;
        }
        #endif
        
        // Check for TextArea attribute
        TextAreaAttribute textAreaAttribute = fieldInfo.GetCustomAttribute<TextAreaAttribute>();
        if (textAreaAttribute != null && property.propertyType == SerializedPropertyType.String)
        {
            position.height = EditorGUIUtility.singleLineHeight * (textAreaAttribute.maxLines + 1);
            property.stringValue = EditorGUI.TextArea(position, label.text, property.stringValue);
            return;
        }
        
        // Check for Multiline attribute
        MultilineAttribute multilineAttribute = fieldInfo.GetCustomAttribute<MultilineAttribute>();
        if (multilineAttribute != null && property.propertyType == SerializedPropertyType.String)
        {
            position.height = EditorGUIUtility.singleLineHeight * multilineAttribute.lines;
            property.stringValue = EditorGUI.TextArea(position, label.text, property.stringValue);
            return;
        }
        
        // Default property field
        EditorGUI.PropertyField(position, property, label, true);
    }
    
    private FieldInfo GetFieldInfo(SerializedProperty property)
    {
        Type targetType = property.serializedObject.targetObject.GetType();
        string[] path = property.propertyPath.Split('.');
        
        FieldInfo field = null;
        Type currentType = targetType;
        
        for (int i = 0; i < path.Length; i++)
        {
            // Skip array element paths
            if (path[i].Contains("["))
                continue;
                
            field = currentType.GetField(path[i], 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field != null && i < path.Length - 1)
            {
                currentType = field.FieldType;
                
                // Handle arrays and lists
                if (currentType.IsArray)
                {
                    currentType = currentType.GetElementType();
                }
                else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
                {
                    currentType = currentType.GetGenericArguments()[0];
                }
            }
        }
        
        return field;
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        FieldInfo fieldInfo = GetFieldInfo(property);
        if (fieldInfo != null)
        {
            // Check for TextArea attribute
            TextAreaAttribute textAreaAttribute = fieldInfo.GetCustomAttribute<TextAreaAttribute>();
            if (textAreaAttribute != null && property.propertyType == SerializedPropertyType.String)
            {
                return EditorGUIUtility.singleLineHeight * (textAreaAttribute.maxLines + 1);
            }
            
            // Check for Multiline attribute
            MultilineAttribute multilineAttribute = fieldInfo.GetCustomAttribute<MultilineAttribute>();
            if (multilineAttribute != null && property.propertyType == SerializedPropertyType.String)
            {
                return EditorGUIUtility.singleLineHeight * multilineAttribute.lines;
            }
        }
        
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
    
    private object GetDefaultValue(SerializedProperty property)
    {
        // Get the target object type
        Type targetType = property.serializedObject.targetObject.GetType();
        
        // Check if it's a MonoBehaviour or Component - if so, create temp GameObject
        if (typeof(MonoBehaviour).IsAssignableFrom(targetType) || 
            typeof(Component).IsAssignableFrom(targetType))
        {
            GameObject tempGO = new GameObject("TempForDefaults");
            tempGO.hideFlags = HideFlags.HideAndDontSave;
            
            try
            {
                Component tempComponent = SafeAddComponentWithRequirements(tempGO,targetType);
                
                // Navigate through the property path to get the field
                string[] path = property.propertyPath.Split('.');
                object currentObject = tempComponent;
                FieldInfo field = null;
                
                for (int i = 0; i < path.Length; i++)
                {
                    // Handle array elements
                    if (path[i].Contains("["))
                    {
                        return GetDefaultValueForType(property.propertyType);
                    }
                    
                    field = currentObject.GetType().GetField(path[i], 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (field != null && i < path.Length - 1)
                    {
                        currentObject = field.GetValue(currentObject);
                        if (currentObject == null)
                        {
                            return GetDefaultValueForType(property.propertyType);
                        }
                    }
                }
                
                if (field != null)
                {
                    return field.GetValue(currentObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempGO);
            }
            
            return GetDefaultValueForType(property.propertyType);
        }
        
        // Check if it's a ScriptableObject (including ScriptableRendererFeature)
        if (typeof(ScriptableObject).IsAssignableFrom(targetType))
        {
            ScriptableObject tempInstance = ScriptableObject.CreateInstance(targetType);
            
            try
            {
                // Navigate through the property path to get the field
                string[] path = property.propertyPath.Split('.');
                object currentObject = tempInstance;
                FieldInfo field = null;
                
                for (int i = 0; i < path.Length; i++)
                {
                    // Handle array elements
                    if (path[i].Contains("["))
                    {
                        return GetDefaultValueForType(property.propertyType);
                    }
                    
                    field = currentObject.GetType().GetField(path[i], 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (field != null && i < path.Length - 1)
                    {
                        currentObject = field.GetValue(currentObject);
                        if (currentObject == null)
                        {
                            return GetDefaultValueForType(property.propertyType);
                        }
                    }
                }
                
                if (field != null)
                {
                    return field.GetValue(currentObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempInstance);
            }
            
            return GetDefaultValueForType(property.propertyType);
        }
        
        // For other types, use Activator.CreateInstance
        object tempInstance2 = Activator.CreateInstance(targetType);
        
        // Navigate through the property path to get the field
        string[] path2 = property.propertyPath.Split('.');
        object currentObject2 = tempInstance2;
        FieldInfo field2 = null;
        
        for (int i = 0; i < path2.Length; i++)
        {
            // Handle array elements
            if (path2[i].Contains("["))
            {
                return GetDefaultValueForType(property.propertyType);
            }
            
            field2 = currentObject2.GetType().GetField(path2[i], 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (field2 != null && i < path2.Length - 1)
            {
                currentObject2 = field2.GetValue(currentObject2);
                if (currentObject2 == null)
                {
                    return GetDefaultValueForType(property.propertyType);
                }
            }
        }
        
        if (field2 != null)
        {
            return field2.GetValue(currentObject2);
        }
        
        return GetDefaultValueForType(property.propertyType);
    }
    
    private object GetDefaultValueForType(SerializedPropertyType propertyType)
    {
        switch (propertyType)
        {
            case SerializedPropertyType.Integer:
                return 0;
            case SerializedPropertyType.Float:
                return 0f;
            case SerializedPropertyType.Boolean:
                return false;
            case SerializedPropertyType.String:
                return "";
            case SerializedPropertyType.Vector2:
                return Vector2.zero;
            case SerializedPropertyType.Vector3:
                return Vector3.zero;
            case SerializedPropertyType.Vector4:
                return Vector4.zero;
            case SerializedPropertyType.Color:
                return Color.white;
            default:
                return null;
        }
    }
    
    private bool IsEqualToDefault(SerializedProperty property, object defaultValue)
    {
        if (defaultValue == null)
            return false;
            
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue == (int)defaultValue;
            case SerializedPropertyType.Float:
                return Mathf.Approximately(property.floatValue, (float)defaultValue);
            case SerializedPropertyType.Boolean:
                return property.boolValue == (bool)defaultValue;
            case SerializedPropertyType.String:
                return property.stringValue == (string)defaultValue;
            case SerializedPropertyType.Vector2:
                return property.vector2Value == (Vector2)defaultValue;
            case SerializedPropertyType.Vector3:
                return property.vector3Value == (Vector3)defaultValue;
            case SerializedPropertyType.Vector4:
                return property.vector4Value == (Vector4)defaultValue;
            case SerializedPropertyType.Color:
                return property.colorValue == (Color)defaultValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == (UnityEngine.Object)defaultValue;
            case SerializedPropertyType.Enum:
                // Handle enum properly - convert enum value to name then compare
                if (defaultValue is Enum enumValue)
                {
                    string defaultEnumName = enumValue.ToString();
                    string currentEnumName = property.enumNames.Length > property.enumValueIndex && property.enumValueIndex >= 0
                        ? property.enumNames[property.enumValueIndex]
                        : "";
                    return defaultEnumName == currentEnumName;
                }
                return false;
            default:
                return false;
        }
    }
    
    private void RevertToDefault(SerializedProperty property, object defaultValue)
    {
        if (defaultValue == null)
            return;
            
        Undo.RecordObject(property.serializedObject.targetObject, "Revert to Default");
        
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                property.intValue = (int)defaultValue;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = (float)defaultValue;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = (bool)defaultValue;
                break;
            case SerializedPropertyType.String:
                property.stringValue = (string)defaultValue;
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = (Vector2)defaultValue;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = (Vector3)defaultValue;
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = (Vector4)defaultValue;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = (Color)defaultValue;
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = (UnityEngine.Object)defaultValue;
                break;
            case SerializedPropertyType.Enum:
                // Handle enum properly - find the index by name
                if (defaultValue is Enum enumValue)
                {
                    string enumName = enumValue.ToString();
                    int index = Array.IndexOf(property.enumNames, enumName);
                    if (index >= 0)
                    {
                        property.enumValueIndex = index;
                    }
                    else
                    {
                        // If exact name not found, try to use the integer value if it's valid
                        int intValue = Convert.ToInt32(defaultValue);
                        if (intValue >= 0 && intValue < property.enumNames.Length)
                        {
                            property.enumValueIndex = intValue;
                        }
                        else
                        {
                            // Fall back to first enum value
                            property.enumValueIndex = 0;
                        }
                    }
                }
                break;
        }
        
        property.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(property.serializedObject.targetObject);
    }
    
    private Component SafeAddComponentWithRequirements(GameObject go, Type targetType)
    {
        // Add any required dependencies first
        var requireAttrs = (RequireComponent[])targetType.GetCustomAttributes(typeof(RequireComponent), true);
        foreach (var attr in requireAttrs)
        {
            if (attr.m_Type0 != null) SafeAddComponentWithRequirements(go, attr.m_Type0);
            if (attr.m_Type1 != null) SafeAddComponentWithRequirements(go, attr.m_Type1);
            if (attr.m_Type2 != null) SafeAddComponentWithRequirements(go, attr.m_Type2);
        }

        // Skip if already added
        Component existing = go.GetComponent(targetType);
        if (existing != null)
            return existing;

        // Handle abstract or problematic base types (like Renderer)
        if (targetType == typeof(Renderer))
        {
            return go.AddComponent<MeshRenderer>(); // safe default
        }

        // Skip unsupported abstract classes and interfaces
        if (targetType.IsAbstract || targetType.IsInterface)
        {
            Debug.LogWarning($"[Revertible] Skipped adding abstract/interface component type '{targetType}'.");
            return null;
        }

        // Finally, add the actual component
        return go.AddComponent(targetType);
    }
}
#endif