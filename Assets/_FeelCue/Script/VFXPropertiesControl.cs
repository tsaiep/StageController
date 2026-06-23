using UnityEngine;
using UnityEngine.VFX;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class VFXPropertiesControl : MonoBehaviour
{
    public enum VFXParameterType
    {
        Float,
        FloatSlider,
        Color,
        Bool
    }

    [Header("Target")]
    public VisualEffect targetVisualEffect;
    public VFXParameterType parameterType = VFXParameterType.Float;
    public string parameterName;

    [Header("Timing")]
    [Min(0f)] public float delay = 0f;
    [Min(0f)] public float duration = 1f;

    [Header("Float")]
    public AnimationCurve floatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public float sliderMin = 0f;
    public float sliderMax = 1f;

    [Header("Color")]
    [GradientUsage(true)] public Gradient colorGradient = CreateDefaultGradient();

    [Header("Bool")]
    public bool boolValue;

    private bool _isControlling;
    private int _parameterId;
    private float _startTime;
    private float _currentDuration;

    public void Play()
    {
        StartControl();
    }

    public void StartControl()
    {
        if (!CanApplyParameter())
        {
            _isControlling = false;
            return;
        }

        _parameterId = Shader.PropertyToID(parameterName);
        float currentDelay = Mathf.Max(0f, delay);
        _startTime = Time.time + currentDelay;
        _currentDuration = Mathf.Max(0f, duration);

        if (currentDelay <= 0f && _currentDuration <= 0f)
        {
            ApplyFinalValue();
            _isControlling = false;
            return;
        }

        _isControlling = true;

        if (currentDelay <= 0f && parameterType != VFXParameterType.Bool)
        {
            ApplySampledValue(0f);
        }
    }

    private void Update()
    {
        if (!_isControlling)
        {
            return;
        }

        if (targetVisualEffect == null)
        {
            _isControlling = false;
            return;
        }

        float elapsedTime = Time.time - _startTime;

        if (elapsedTime < 0f)
        {
            return;
        }

        if (elapsedTime >= _currentDuration)
        {
            ApplyFinalValue();
            _isControlling = false;
            return;
        }

        if (parameterType == VFXParameterType.Bool)
        {
            return;
        }

        float normalizedTime = Mathf.Clamp01(elapsedTime / _currentDuration);
        ApplySampledValue(normalizedTime);
    }

    private bool CanApplyParameter()
    {
        if (targetVisualEffect == null)
        {
            Debug.LogWarning($"{nameof(VFXPropertiesControl)} on {name}: Target VisualEffect is null.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(parameterName))
        {
            Debug.LogWarning($"{nameof(VFXPropertiesControl)} on {name}: Parameter Name is empty.", this);
            return false;
        }

        return true;
    }

    private void ApplyFinalValue()
    {
        if (targetVisualEffect == null)
        {
            return;
        }

        if (parameterType == VFXParameterType.Bool)
        {
            targetVisualEffect.SetBool(_parameterId, boolValue);
            return;
        }

        ApplySampledValue(1f);
    }

    private void ApplySampledValue(float normalizedTime)
    {
        switch (parameterType)
        {
            case VFXParameterType.Float:
                targetVisualEffect.SetFloat(_parameterId, EvaluateFloatCurve(normalizedTime));
                break;
            case VFXParameterType.FloatSlider:
                float curveValue = EvaluateFloatCurve(normalizedTime);
                targetVisualEffect.SetFloat(_parameterId, Mathf.Lerp(sliderMin, sliderMax, curveValue));
                break;
            case VFXParameterType.Color:
                Color color = EvaluateColorGradient(normalizedTime);
                targetVisualEffect.SetVector4(_parameterId, new Vector4(color.r, color.g, color.b, color.a));
                break;
        }
    }

    private float EvaluateFloatCurve(float normalizedTime)
    {
        return floatCurve != null ? floatCurve.Evaluate(normalizedTime) : normalizedTime;
    }

    private Color EvaluateColorGradient(float normalizedTime)
    {
        return colorGradient != null ? colorGradient.Evaluate(normalizedTime) : Color.white;
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        duration = Mathf.Max(0f, duration);

        if (sliderMax < sliderMin)
        {
            sliderMax = sliderMin;
        }
    }

    private static Gradient CreateDefaultGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VFXPropertiesControl))]
public class VFXPropertiesControlEditor : Editor
{
    private SerializedProperty _targetVisualEffect;
    private SerializedProperty _parameterType;
    private SerializedProperty _parameterName;
    private SerializedProperty _delay;
    private SerializedProperty _duration;
    private SerializedProperty _floatCurve;
    private SerializedProperty _sliderMin;
    private SerializedProperty _sliderMax;
    private SerializedProperty _colorGradient;
    private SerializedProperty _boolValue;

    private void OnEnable()
    {
        _targetVisualEffect = serializedObject.FindProperty(nameof(VFXPropertiesControl.targetVisualEffect));
        _parameterType = serializedObject.FindProperty(nameof(VFXPropertiesControl.parameterType));
        _parameterName = serializedObject.FindProperty(nameof(VFXPropertiesControl.parameterName));
        _delay = serializedObject.FindProperty(nameof(VFXPropertiesControl.delay));
        _duration = serializedObject.FindProperty(nameof(VFXPropertiesControl.duration));
        _floatCurve = serializedObject.FindProperty(nameof(VFXPropertiesControl.floatCurve));
        _sliderMin = serializedObject.FindProperty(nameof(VFXPropertiesControl.sliderMin));
        _sliderMax = serializedObject.FindProperty(nameof(VFXPropertiesControl.sliderMax));
        _colorGradient = serializedObject.FindProperty(nameof(VFXPropertiesControl.colorGradient));
        _boolValue = serializedObject.FindProperty(nameof(VFXPropertiesControl.boolValue));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_targetVisualEffect, new GUIContent("Target VisualEffect"));
        EditorGUILayout.PropertyField(_parameterType, new GUIContent("Parameter Type"));
        EditorGUILayout.PropertyField(_parameterName, new GUIContent("Parameter Name"));
        EditorGUILayout.PropertyField(_delay, new GUIContent("Delay"));
        EditorGUILayout.PropertyField(_duration, new GUIContent("Duration"));

        EditorGUILayout.Space();

        VFXPropertiesControl.VFXParameterType parameterType =
            (VFXPropertiesControl.VFXParameterType)_parameterType.enumValueIndex;

        switch (parameterType)
        {
            case VFXPropertiesControl.VFXParameterType.Float:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                break;
            case VFXPropertiesControl.VFXParameterType.FloatSlider:
                EditorGUILayout.PropertyField(_floatCurve, new GUIContent("Curve"));
                EditorGUILayout.PropertyField(_sliderMin, new GUIContent("Slider Min"));
                EditorGUILayout.PropertyField(_sliderMax, new GUIContent("Slider Max"));
                break;
            case VFXPropertiesControl.VFXParameterType.Color:
                EditorGUILayout.PropertyField(_colorGradient, new GUIContent("Gradient"));
                break;
            case VFXPropertiesControl.VFXParameterType.Bool:
                EditorGUILayout.PropertyField(_boolValue, new GUIContent("Toggle"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
