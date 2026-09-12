using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.VFX;

[AddComponentMenu("Stage Controller/Audio Analyzer VFX Controller")]
public class AudioAnalyzerVFXController : MonoBehaviour
{
    public enum AudioAnalyzerValueSource
    {
        Beat,
        NormalizedBufferedBandLevel
    }

    public enum VFXParameterType
    {
        Float,
        Int,
        Bool,
        Vector2,
        Vector3,
        Vector4,
        Color
    }

    [Serializable]
    public class VFXParameterBinding
    {
        public VisualEffect visualEffect;
        public string parameterName;
        public VFXParameterType parameterType = VFXParameterType.Float;

        [Header("Vector Components")]
        public bool x = true;
        public bool y;
        public bool z;
        public bool w;

        [Header("Bool")]
        public float boolThreshold = 0.5f;

        [Header("Color")]
        [GradientUsage(true)] public Gradient colorRamp = CreateDefaultGradient();

        [Header("Per Target Remap")]
        public bool usePerTargetRemap;
        public float multiplier = 1f;
        public float offset = 0f;
        public float audioAnalyzerLerp = 0f;

        [NonSerialized] public int ParameterId;
        [NonSerialized] public bool ParameterIdCached;
        [NonSerialized] public bool MissingParameterWarningLogged;
        [NonSerialized] public float CurrentValue;
        [NonSerialized] public bool InitialValueCached;
        [NonSerialized] public float InitialScalarValue;
        [NonSerialized] public Vector4 InitialVectorValue;
        [NonSerialized] public float ReturnStartScalarValue;
        [NonSerialized] public Vector4 ReturnStartVectorValue;
    }

    [Header("AudioAnalyzer")]
    public MMAudioAnalyzer audioAnalyzer;
    public AudioAnalyzerValueSource valueSource = AudioAnalyzerValueSource.Beat;
    public int beatID;
    public int normalizedLevelID;
    public float audioAnalyzerMultiplier = 1f;
    public float audioAnalyzerOffset = 0f;
    public float audioAnalyzerLerp = 60f;
    public bool useUnscaledTime = true;

    [Header("Targets")]
    public List<VFXParameterBinding> targets = new List<VFXParameterBinding>();

    [Header("Debug")]
    [SerializeField, MMReadOnly] private float currentSourceValue;
    [SerializeField, MMReadOnly] private float currentValue;
    [SerializeField, MMReadOnly] private float currentValueNormalized;

    private bool _returningToInitialValues;
    private float _returnDuration;
    private float _returnElapsed;
    private AnimationCurve _returnCurve;
    private bool _returnUsesUnscaledTime;
    private bool _fadingIn;
    private float _fadeInDuration;
    private float _fadeInElapsed;
    private AnimationCurve _fadeInCurve;
    private bool _fadeInUsesUnscaledTime;

    public bool IsReturningToInitialValues => _returningToInitialValues;

    private void Awake()
    {
        CaptureInitialValues();
    }

    private void Reset()
    {
        targets.Clear();

        VisualEffect visualEffect = GetComponent<VisualEffect>();
        if (visualEffect != null)
        {
            targets.Add(new VFXParameterBinding { visualEffect = visualEffect });
        }
    }

    private void Update()
    {
        CaptureInitialValues();

        if (_returningToInitialValues)
        {
            UpdateReturnToInitialValues();
            return;
        }

        float fadeInWeight = UpdateFadeIn();

        if (!TryGetSourceValue(out float sourceValue))
        {
            return;
        }

        currentSourceValue = sourceValue;
        float targetValue = sourceValue * audioAnalyzerMultiplier + audioAnalyzerOffset;
        currentValue = Mathf.Lerp(currentValue, targetValue, audioAnalyzerLerp * GetDeltaTime());
        currentValueNormalized = Mathf.Clamp01(sourceValue);

        ApplyValueToTargets(sourceValue, fadeInWeight);
    }

    public void ResumeAudioControl()
    {
        _returningToInitialValues = false;
        _fadingIn = false;
        enabled = true;
    }

    public void FadeInAudioControl(float duration, AnimationCurve curve, bool useUnscaledTime)
    {
        CaptureInitialValues();
        CaptureReturnStartValues();

        _returningToInitialValues = false;
        _fadeInDuration = Mathf.Max(0f, duration);
        _fadeInElapsed = 0f;
        _fadeInCurve = curve;
        _fadeInUsesUnscaledTime = useUnscaledTime;
        _fadingIn = _fadeInDuration > 0f && gameObject.activeInHierarchy;
        enabled = true;
    }

    public void ReturnToInitialValuesAndDisable(float duration, AnimationCurve curve, bool useUnscaledTime)
    {
        CaptureInitialValues();
        CaptureReturnStartValues();

        _fadingIn = false;
        _returnDuration = Mathf.Max(0f, duration);
        _returnElapsed = 0f;
        _returnCurve = curve;
        _returnUsesUnscaledTime = useUnscaledTime;
        _returningToInitialValues = true;
        enabled = true;

        if (_returnDuration <= 0f || !gameObject.activeInHierarchy)
        {
            ApplyInitialValues(1f);
            CompleteReturnToInitialValues();
        }
    }

    public void DisableImmediately(bool restoreInitialValues)
    {
        _returningToInitialValues = false;
        _fadingIn = false;
        if (restoreInitialValues)
        {
            CaptureInitialValues();
            CaptureReturnStartValues();
            ApplyInitialValues(1f);
        }
        enabled = false;
    }

    public void RecaptureInitialValues()
    {
        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (target != null)
            {
                target.InitialValueCached = false;
            }
        }
        CaptureInitialValues();
    }

    private void CaptureInitialValues()
    {
        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (target == null || target.InitialValueCached)
            {
                continue;
            }

            if (!CanApply(target))
            {
                continue;
            }

            switch (target.parameterType)
            {
                case VFXParameterType.Float:
                    target.InitialScalarValue = target.visualEffect.GetFloat(target.ParameterId);
                    break;
                case VFXParameterType.Int:
                    target.InitialScalarValue = target.visualEffect.GetInt(target.ParameterId);
                    break;
                case VFXParameterType.Bool:
                    target.InitialScalarValue = target.visualEffect.GetBool(target.ParameterId) ? 1f : 0f;
                    break;
                case VFXParameterType.Vector2:
                    target.InitialVectorValue = target.visualEffect.GetVector2(target.ParameterId);
                    break;
                case VFXParameterType.Vector3:
                    target.InitialVectorValue = target.visualEffect.GetVector3(target.ParameterId);
                    break;
                case VFXParameterType.Vector4:
                case VFXParameterType.Color:
                    target.InitialVectorValue = target.visualEffect.GetVector4(target.ParameterId);
                    break;
            }

            target.InitialValueCached = true;
        }
    }

    private void CaptureReturnStartValues()
    {
        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (!CanApply(target) || !target.InitialValueCached)
            {
                continue;
            }

            switch (target.parameterType)
            {
                case VFXParameterType.Float:
                    target.ReturnStartScalarValue = target.visualEffect.GetFloat(target.ParameterId);
                    break;
                case VFXParameterType.Int:
                    target.ReturnStartScalarValue = target.visualEffect.GetInt(target.ParameterId);
                    break;
                case VFXParameterType.Bool:
                    target.ReturnStartScalarValue = target.visualEffect.GetBool(target.ParameterId) ? 1f : 0f;
                    break;
                case VFXParameterType.Vector2:
                    target.ReturnStartVectorValue = target.visualEffect.GetVector2(target.ParameterId);
                    break;
                case VFXParameterType.Vector3:
                    target.ReturnStartVectorValue = target.visualEffect.GetVector3(target.ParameterId);
                    break;
                case VFXParameterType.Vector4:
                case VFXParameterType.Color:
                    target.ReturnStartVectorValue = target.visualEffect.GetVector4(target.ParameterId);
                    break;
            }
        }
    }

    private void UpdateReturnToInitialValues()
    {
        _returnElapsed += _returnUsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float normalizedTime = _returnDuration <= 0f ? 1f : Mathf.Clamp01(_returnElapsed / _returnDuration);
        float interpolation = _returnCurve == null ? normalizedTime : _returnCurve.Evaluate(normalizedTime);
        ApplyInitialValues(interpolation);

        if (normalizedTime >= 1f)
        {
            ApplyInitialValues(1f);
            CompleteReturnToInitialValues();
        }
    }

    private void ApplyInitialValues(float interpolation)
    {
        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (!CanApply(target) || !target.InitialValueCached)
            {
                continue;
            }

            switch (target.parameterType)
            {
                case VFXParameterType.Float:
                    target.visualEffect.SetFloat(target.ParameterId,
                        Mathf.LerpUnclamped(target.ReturnStartScalarValue, target.InitialScalarValue, interpolation));
                    break;
                case VFXParameterType.Int:
                    target.visualEffect.SetInt(target.ParameterId,
                        Mathf.RoundToInt(Mathf.LerpUnclamped(target.ReturnStartScalarValue, target.InitialScalarValue, interpolation)));
                    break;
                case VFXParameterType.Bool:
                    if (interpolation >= 1f)
                    {
                        target.visualEffect.SetBool(target.ParameterId, target.InitialScalarValue > 0.5f);
                    }
                    break;
                case VFXParameterType.Vector2:
                    target.visualEffect.SetVector2(target.ParameterId,
                        Vector2.LerpUnclamped(target.ReturnStartVectorValue, target.InitialVectorValue, interpolation));
                    break;
                case VFXParameterType.Vector3:
                    target.visualEffect.SetVector3(target.ParameterId,
                        Vector3.LerpUnclamped(target.ReturnStartVectorValue, target.InitialVectorValue, interpolation));
                    break;
                case VFXParameterType.Vector4:
                case VFXParameterType.Color:
                    target.visualEffect.SetVector4(target.ParameterId,
                        Vector4.LerpUnclamped(target.ReturnStartVectorValue, target.InitialVectorValue, interpolation));
                    break;
            }
        }
    }

    private void CompleteReturnToInitialValues()
    {
        _returningToInitialValues = false;
        enabled = false;
    }

    private float UpdateFadeIn()
    {
        if (!_fadingIn)
        {
            return 1f;
        }

        _fadeInElapsed += _fadeInUsesUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float normalizedTime = _fadeInDuration <= 0f ? 1f : Mathf.Clamp01(_fadeInElapsed / _fadeInDuration);
        if (normalizedTime >= 1f)
        {
            _fadingIn = false;
            return 1f;
        }

        return _fadeInCurve == null ? normalizedTime : _fadeInCurve.Evaluate(normalizedTime);
    }

    private bool TryGetSourceValue(out float sourceValue)
    {
        sourceValue = 0f;

        if (audioAnalyzer == null)
        {
            return false;
        }

        switch (valueSource)
        {
            case AudioAnalyzerValueSource.Beat:
                if (audioAnalyzer.Beats == null || beatID < 0 || beatID >= audioAnalyzer.Beats.Length)
                {
                    return false;
                }

                sourceValue = audioAnalyzer.Beats[beatID].CurrentValue;
                return true;

            case AudioAnalyzerValueSource.NormalizedBufferedBandLevel:
                if (audioAnalyzer.NormalizedBufferedBandLevels == null ||
                    normalizedLevelID < 0 ||
                    normalizedLevelID >= audioAnalyzer.NormalizedBufferedBandLevels.Length)
                {
                    return false;
                }

                sourceValue = audioAnalyzer.NormalizedBufferedBandLevels[normalizedLevelID];
                return true;

            default:
                return false;
        }
    }

    private void ApplyValueToTargets(float sourceValue, float activationWeight)
    {
        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (!CanApply(target))
            {
                continue;
            }

            float effectiveMultiplier = target.usePerTargetRemap ? target.multiplier : audioAnalyzerMultiplier;
            float effectiveOffset = target.usePerTargetRemap ? target.offset : audioAnalyzerOffset;
            float effectiveLerp = target.usePerTargetRemap ? target.audioAnalyzerLerp : audioAnalyzerLerp;
            float targetValue = sourceValue * effectiveMultiplier + effectiveOffset;
            target.CurrentValue = Mathf.Lerp(target.CurrentValue, targetValue, effectiveLerp * GetDeltaTime());

            float remappedNormalizedValue = Mathf.Clamp01(target.CurrentValue);

            switch (target.parameterType)
            {
                case VFXParameterType.Float:
                    target.visualEffect.SetFloat(target.ParameterId,
                        Mathf.LerpUnclamped(target.ReturnStartScalarValue, target.CurrentValue, activationWeight));
                    break;
                case VFXParameterType.Int:
                    target.visualEffect.SetInt(target.ParameterId, Mathf.RoundToInt(Mathf.LerpUnclamped(
                        target.ReturnStartScalarValue, target.CurrentValue, activationWeight)));
                    break;
                case VFXParameterType.Bool:
                    target.visualEffect.SetBool(target.ParameterId, activationWeight >= 1f
                        ? target.CurrentValue > target.boolThreshold
                        : target.ReturnStartScalarValue > 0.5f);
                    break;
                case VFXParameterType.Vector2:
                    SetVector2(target, target.CurrentValue, activationWeight);
                    break;
                case VFXParameterType.Vector3:
                    SetVector3(target, target.CurrentValue, activationWeight);
                    break;
                case VFXParameterType.Vector4:
                    SetVector4(target, target.CurrentValue, activationWeight);
                    break;
                case VFXParameterType.Color:
                    Color color = target.colorRamp != null
                        ? target.colorRamp.Evaluate(remappedNormalizedValue)
                        : Color.white;
                    target.visualEffect.SetVector4(target.ParameterId, Vector4.LerpUnclamped(
                        target.ReturnStartVectorValue,
                        new Vector4(color.r, color.g, color.b, color.a),
                        activationWeight));
                    break;
            }
        }
    }

    private bool CanApply(VFXParameterBinding target)
    {
        if (target == null || target.visualEffect == null || string.IsNullOrWhiteSpace(target.parameterName))
        {
            return false;
        }

        if (!target.ParameterIdCached)
        {
            target.ParameterId = Shader.PropertyToID(target.parameterName);
            target.ParameterIdCached = true;
        }

        if (!HasParameter(target))
        {
            if (!target.MissingParameterWarningLogged)
            {
                Debug.LogWarning(
                    $"{nameof(AudioAnalyzerVFXController)} on {name}: VisualEffect '{target.visualEffect.name}' has no {target.parameterType} parameter named '{target.parameterName}'.",
                    this);
                target.MissingParameterWarningLogged = true;
            }

            return false;
        }

        return true;
    }

    private bool HasParameter(VFXParameterBinding target)
    {
        switch (target.parameterType)
        {
            case VFXParameterType.Float:
                return target.visualEffect.HasFloat(target.ParameterId);
            case VFXParameterType.Int:
                return target.visualEffect.HasInt(target.ParameterId);
            case VFXParameterType.Bool:
                return target.visualEffect.HasBool(target.ParameterId);
            case VFXParameterType.Vector2:
                return target.visualEffect.HasVector2(target.ParameterId);
            case VFXParameterType.Vector3:
                return target.visualEffect.HasVector3(target.ParameterId);
            case VFXParameterType.Vector4:
            case VFXParameterType.Color:
                return target.visualEffect.HasVector4(target.ParameterId);
            default:
                return false;
        }
    }

    private void SetVector2(VFXParameterBinding target, float value, float activationWeight)
    {
        Vector2 vector = target.visualEffect.GetVector2(target.ParameterId);
        if (target.x) { vector.x = Mathf.LerpUnclamped(target.ReturnStartVectorValue.x, value, activationWeight); }
        if (target.y) { vector.y = Mathf.LerpUnclamped(target.ReturnStartVectorValue.y, value, activationWeight); }
        target.visualEffect.SetVector2(target.ParameterId, vector);
    }

    private void SetVector3(VFXParameterBinding target, float value, float activationWeight)
    {
        Vector3 vector = target.visualEffect.GetVector3(target.ParameterId);
        if (target.x) { vector.x = Mathf.LerpUnclamped(target.ReturnStartVectorValue.x, value, activationWeight); }
        if (target.y) { vector.y = Mathf.LerpUnclamped(target.ReturnStartVectorValue.y, value, activationWeight); }
        if (target.z) { vector.z = Mathf.LerpUnclamped(target.ReturnStartVectorValue.z, value, activationWeight); }
        target.visualEffect.SetVector3(target.ParameterId, vector);
    }

    private void SetVector4(VFXParameterBinding target, float value, float activationWeight)
    {
        Vector4 vector = target.visualEffect.GetVector4(target.ParameterId);
        if (target.x) { vector.x = Mathf.LerpUnclamped(target.ReturnStartVectorValue.x, value, activationWeight); }
        if (target.y) { vector.y = Mathf.LerpUnclamped(target.ReturnStartVectorValue.y, value, activationWeight); }
        if (target.z) { vector.z = Mathf.LerpUnclamped(target.ReturnStartVectorValue.z, value, activationWeight); }
        if (target.w) { vector.w = Mathf.LerpUnclamped(target.ReturnStartVectorValue.w, value, activationWeight); }
        target.visualEffect.SetVector4(target.ParameterId, vector);
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void OnValidate()
    {
        beatID = Mathf.Max(0, beatID);
        normalizedLevelID = Mathf.Max(0, normalizedLevelID);
        audioAnalyzerLerp = Mathf.Max(0f, audioAnalyzerLerp);

        if (targets == null)
        {
            return;
        }

        foreach (VFXParameterBinding target in targets)
        {
            if (target == null)
            {
                continue;
            }

            target.boolThreshold = Mathf.Clamp01(target.boolThreshold);
            target.audioAnalyzerLerp = Mathf.Max(0f, target.audioAnalyzerLerp);
            target.ParameterIdCached = false;
            target.MissingParameterWarningLogged = false;
            target.InitialValueCached = false;
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
