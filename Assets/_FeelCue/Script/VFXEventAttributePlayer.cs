using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(VisualEffect))]
[AddComponentMenu("Stage Controller/VFX Event Attribute Player")]
public sealed class VFXEventAttributePlayer : MonoBehaviour
{
    public enum AttributeType
    {
        Float,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Int,
        UInt,
        Bool,
        CueTime
    }

    [Serializable]
    public sealed class AttributeValue
    {
        [Tooltip("The attribute name defined in the VFX Graph event context.")]
        public string attributeName;

        public AttributeType type = AttributeType.Float;

        public float floatValue;
        public Vector2 vector2Value;
        public Vector3 vector3Value;
        public Vector4 vector4Value;

        [ColorUsage(true, true)]
        public Color colorValue = Color.white;

        public int intValue;
        public uint uintValue;
        public bool boolValue;

        public bool ApplyTo(VFXEventAttribute eventAttribute)
        {
            if (eventAttribute == null || string.IsNullOrWhiteSpace(attributeName))
            {
                return false;
            }

            int attributeId = Shader.PropertyToID(attributeName);

            switch (type)
            {
                case AttributeType.Float:
                    if (!eventAttribute.HasFloat(attributeId)) return false;
                    eventAttribute.SetFloat(attributeId, floatValue);
                    return true;

                case AttributeType.Vector2:
                    if (!eventAttribute.HasVector2(attributeId)) return false;
                    eventAttribute.SetVector2(attributeId, vector2Value);
                    return true;

                case AttributeType.Vector3:
                    if (!eventAttribute.HasVector3(attributeId)) return false;
                    eventAttribute.SetVector3(attributeId, vector3Value);
                    return true;

                case AttributeType.Vector4:
                    if (!eventAttribute.HasVector4(attributeId)) return false;
                    eventAttribute.SetVector4(attributeId, vector4Value);
                    return true;

                case AttributeType.Color:
                    if (!eventAttribute.HasVector3(attributeId)) return false;
                    eventAttribute.SetVector3(attributeId, new Vector3(colorValue.r, colorValue.g, colorValue.b));
                    return true;

                case AttributeType.Int:
                    if (!eventAttribute.HasInt(attributeId)) return false;
                    eventAttribute.SetInt(attributeId, intValue);
                    return true;

                case AttributeType.UInt:
                    if (!eventAttribute.HasUint(attributeId)) return false;
                    eventAttribute.SetUint(attributeId, uintValue);
                    return true;

                case AttributeType.Bool:
                    if (!eventAttribute.HasBool(attributeId)) return false;
                    eventAttribute.SetBool(attributeId, boolValue);
                    return true;

                case AttributeType.CueTime:
                    if (!eventAttribute.HasFloat(attributeId)) return false;
                    // Writes the current scaled game time into the custom attribute named above.
                    // This uses the same time basis as VFX Graph's Total Time (Game) operator.
                    eventAttribute.SetFloat(attributeId, Time.time);
                    return true;

                default:
                    return false;
            }
        }
    }

    [Header("Target")]
    [SerializeField] private VisualEffect targetVisualEffect;

    [Header("Event Attributes")]
    [SerializeField] private List<AttributeValue> attributes = new List<AttributeValue>();

    [Header("Diagnostics")]
    [SerializeField] private bool logInvalidAttributes = true;

    private void Reset()
    {
        targetVisualEffect = GetComponent<VisualEffect>();
    }

    /// <summary>
    /// Creates an event attribute, writes every configured value, and plays the VFX.
    /// This parameterless method can be assigned directly to a UnityEvent or Timeline Signal.
    /// </summary>
    public void Play()
    {
        if (targetVisualEffect == null)
        {
            targetVisualEffect = GetComponent<VisualEffect>();
        }

        if (targetVisualEffect == null)
        {
            Debug.LogWarning($"{nameof(VFXEventAttributePlayer)} on {name}: Target VisualEffect is missing.", this);
            return;
        }

        VFXEventAttribute eventAttribute = targetVisualEffect.CreateVFXEventAttribute();

        for (int i = 0; i < attributes.Count; i++)
        {
            AttributeValue attribute = attributes[i];

            if (attribute != null && attribute.ApplyTo(eventAttribute))
            {
                continue;
            }

            if (logInvalidAttributes)
            {
                string attributeName = attribute == null ? "<null>" : attribute.attributeName;
                Debug.LogWarning(
                    $"{nameof(VFXEventAttributePlayer)} on {name}: Attribute [{i}] '{attributeName}' " +
                    "is empty, missing from the VFX event, or has a mismatched type.",
                    this);
            }
        }

        targetVisualEffect.Play(eventAttribute);
    }

    private void OnValidate()
    {
        if (targetVisualEffect == null)
        {
            targetVisualEffect = GetComponent<VisualEffect>();
        }
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(VFXEventAttributePlayer.AttributeValue))]
public sealed class VFXEventAttributeValueDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3f + VerticalSpacing * 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect nameRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect typeRect = new Rect(position.x, nameRect.yMax + VerticalSpacing, position.width, lineHeight);
        Rect valueRect = new Rect(position.x, typeRect.yMax + VerticalSpacing, position.width, lineHeight);

        SerializedProperty nameProperty = property.FindPropertyRelative("attributeName");
        SerializedProperty typeProperty = property.FindPropertyRelative("type");

        EditorGUI.PropertyField(nameRect, nameProperty, new GUIContent(label.text));
        EditorGUI.PropertyField(typeRect, typeProperty);

        var attributeType = (VFXEventAttributePlayer.AttributeType)typeProperty.enumValueIndex;

        if (attributeType == VFXEventAttributePlayer.AttributeType.CueTime)
        {
            EditorGUI.LabelField(valueRect, "Value", "Automatic (Time.time)");
            EditorGUI.EndProperty();
            return;
        }

        SerializedProperty valueProperty = property.FindPropertyRelative(GetValuePropertyName(attributeType));
        EditorGUI.PropertyField(valueRect, valueProperty, new GUIContent("Value"));

        EditorGUI.EndProperty();
    }

    private static string GetValuePropertyName(VFXEventAttributePlayer.AttributeType type)
    {
        switch (type)
        {
            case VFXEventAttributePlayer.AttributeType.Float: return "floatValue";
            case VFXEventAttributePlayer.AttributeType.Vector2: return "vector2Value";
            case VFXEventAttributePlayer.AttributeType.Vector3: return "vector3Value";
            case VFXEventAttributePlayer.AttributeType.Vector4: return "vector4Value";
            case VFXEventAttributePlayer.AttributeType.Color: return "colorValue";
            case VFXEventAttributePlayer.AttributeType.Int: return "intValue";
            case VFXEventAttributePlayer.AttributeType.UInt: return "uintValue";
            case VFXEventAttributePlayer.AttributeType.Bool: return "boolValue";
            default: return "floatValue";
        }
    }
}
#endif
