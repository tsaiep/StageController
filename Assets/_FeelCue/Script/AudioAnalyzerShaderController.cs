using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

[AddComponentMenu("Stage Controller/Audio Analyzer Shader Controller")]
public class AudioAnalyzerShaderController : MonoBehaviour
{
    public enum AudioAnalyzerValueSource
    {
        Beat,
        NormalizedBufferedBandLevel,
        NormalizedBufferedAmplitude
    }

    public enum ShaderParameterType
    {
        Float,
        Int,
        Bool,
        Vector,
        Color,
        Keyword
    }

    [Serializable]
    public class RendererParameterBinding
    {
        public Renderer renderer;
        public int materialID;
        public string parameterName;
        public ShaderParameterType parameterType = ShaderParameterType.Float;

        [Header("Vector Components")]
        public bool x = true;
        public bool y;
        public bool z;
        public bool w;

        [Header("Bool / Keyword")]
        public float boolThreshold = 0.5f;

        [Header("Color")]
        [GradientUsage(true)] public Gradient colorRamp = CreateDefaultGradient();

        [Header("Per Target Remap")]
        public bool usePerTargetRemap;
        public float multiplier = 1f;
        public float offset = 0f;
        public float audioAnalyzerLerp = 60f;

        [NonSerialized] public int ParameterId;
        [NonSerialized] public bool ParameterIdCached;
        [NonSerialized] public bool MissingParameterWarningLogged;
        [NonSerialized] public bool Initialized;
        [NonSerialized] public float CurrentValue;
        [NonSerialized] public Vector4 CurrentVector;
        [NonSerialized] public Material TargetMaterial;
        [NonSerialized] public MaterialPropertyBlock PropertyBlock;
        [NonSerialized] public bool InitialValueCached;
        [NonSerialized] public float InitialScalarValue;
        [NonSerialized] public Vector4 InitialVectorValue;
        [NonSerialized] public Color InitialColorValue;
        [NonSerialized] public bool InitialKeywordValue;
        [NonSerialized] public float ReturnStartScalarValue;
        [NonSerialized] public Vector4 ReturnStartVectorValue;
        [NonSerialized] public Color ReturnStartColorValue;
        [NonSerialized] public bool ReturnStartKeywordValue;
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

    [Header("Renderer Settings")]
    public bool useMaterialPropertyBlocks = true;
    public bool createMaterialInstances;

    [Header("Targets")]
    public List<RendererParameterBinding> targets = new List<RendererParameterBinding>();

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

        Renderer targetRenderer = GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targets.Add(new RendererParameterBinding { renderer = targetRenderer });
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
        currentValue = SmoothValue(currentValue, targetValue, audioAnalyzerLerp);
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

        foreach (RendererParameterBinding target in targets)
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

        foreach (RendererParameterBinding target in targets)
        {
            if (target == null || target.InitialValueCached)
            {
                continue;
            }

            if (!CanApply(target))
            {
                continue;
            }

            CaptureCurrentValue(target, true);
            target.InitialValueCached = true;
        }
    }

    private void CaptureReturnStartValues()
    {
        if (targets == null)
        {
            return;
        }

        foreach (RendererParameterBinding target in targets)
        {
            if (!CanApply(target) || !target.InitialValueCached)
            {
                continue;
            }

            CaptureCurrentValue(target, false);
        }
    }

    private void CaptureCurrentValue(RendererParameterBinding target, bool initial)
    {
        bool hasPropertyBlockValue = useMaterialPropertyBlocks &&
                                     target.PropertyBlock != null &&
                                     GetPropertyBlock(target).HasProperty(target.ParameterId);

        switch (target.parameterType)
        {
            case ShaderParameterType.Float:
                float scalarValue = hasPropertyBlockValue
                    ? target.PropertyBlock.GetFloat(target.ParameterId)
                    : target.TargetMaterial.GetFloat(target.ParameterId);
                if (initial) { target.InitialScalarValue = scalarValue; }
                else { target.ReturnStartScalarValue = scalarValue; }
                break;
            case ShaderParameterType.Int:
            case ShaderParameterType.Bool:
                int integerValue = hasPropertyBlockValue
                    ? target.PropertyBlock.GetInt(target.ParameterId)
                    : target.TargetMaterial.GetInteger(target.ParameterId);
                if (initial) { target.InitialScalarValue = integerValue; }
                else { target.ReturnStartScalarValue = integerValue; }
                break;
            case ShaderParameterType.Vector:
                Vector4 vectorValue = hasPropertyBlockValue
                    ? target.PropertyBlock.GetVector(target.ParameterId)
                    : target.TargetMaterial.GetVector(target.ParameterId);
                if (initial) { target.InitialVectorValue = vectorValue; }
                else { target.ReturnStartVectorValue = vectorValue; }
                break;
            case ShaderParameterType.Color:
                Color colorValue = hasPropertyBlockValue
                    ? target.PropertyBlock.GetColor(target.ParameterId)
                    : target.TargetMaterial.GetColor(target.ParameterId);
                if (initial) { target.InitialColorValue = colorValue; }
                else { target.ReturnStartColorValue = colorValue; }
                break;
            case ShaderParameterType.Keyword:
                bool keywordValue = target.TargetMaterial.IsKeywordEnabled(target.parameterName);
                if (initial) { target.InitialKeywordValue = keywordValue; }
                else { target.ReturnStartKeywordValue = keywordValue; }
                break;
        }
    }

    private MaterialPropertyBlock GetPropertyBlock(RendererParameterBinding target)
    {
        target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
        return target.PropertyBlock;
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

        foreach (RendererParameterBinding target in targets)
        {
            if (!CanApply(target) || !target.InitialValueCached)
            {
                continue;
            }

            switch (target.parameterType)
            {
                case ShaderParameterType.Float:
                    SetFloat(target, Mathf.LerpUnclamped(target.ReturnStartScalarValue, target.InitialScalarValue, interpolation));
                    break;
                case ShaderParameterType.Int:
                case ShaderParameterType.Bool:
                    SetInt(target, Mathf.RoundToInt(Mathf.LerpUnclamped(
                        target.ReturnStartScalarValue, target.InitialScalarValue, interpolation)));
                    break;
                case ShaderParameterType.Vector:
                    Vector4 vector = Vector4.LerpUnclamped(
                        target.ReturnStartVectorValue, target.InitialVectorValue, interpolation);
                    target.CurrentVector = vector;
                    if (useMaterialPropertyBlocks)
                    {
                        GetPropertyBlock(target);
                        target.PropertyBlock.SetVector(target.ParameterId, vector);
                        target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
                    }
                    else
                    {
                        target.TargetMaterial.SetVector(target.ParameterId, vector);
                    }
                    break;
                case ShaderParameterType.Color:
                    SetColor(target, Color.LerpUnclamped(target.ReturnStartColorValue, target.InitialColorValue, interpolation));
                    break;
                case ShaderParameterType.Keyword:
                    if (interpolation >= 1f)
                    {
                        SetKeyword(target, target.InitialKeywordValue);
                    }
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

            case AudioAnalyzerValueSource.NormalizedBufferedAmplitude:
                sourceValue = audioAnalyzer.NormalizedBufferedAmplitude;
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

        foreach (RendererParameterBinding target in targets)
        {
            if (!CanApply(target))
            {
                continue;
            }

            float effectiveMultiplier = target.usePerTargetRemap ? target.multiplier : audioAnalyzerMultiplier;
            float effectiveOffset = target.usePerTargetRemap ? target.offset : audioAnalyzerOffset;
            float effectiveLerp = target.usePerTargetRemap ? target.audioAnalyzerLerp : audioAnalyzerLerp;
            float targetValue = sourceValue * effectiveMultiplier + effectiveOffset;
            target.CurrentValue = SmoothValue(target.CurrentValue, targetValue, effectiveLerp);

            ApplyValue(target, target.CurrentValue, activationWeight);
        }
    }

    private bool CanApply(RendererParameterBinding target)
    {
        if (target == null || target.renderer == null || string.IsNullOrWhiteSpace(target.parameterName))
        {
            return false;
        }

        if (!InitializeTarget(target))
        {
            return false;
        }

        if (!target.ParameterIdCached)
        {
            target.ParameterId = Shader.PropertyToID(target.parameterName);
            target.ParameterIdCached = true;
        }

        if (target.parameterType == ShaderParameterType.Keyword)
        {
            return true;
        }

        if (target.TargetMaterial == null || !target.TargetMaterial.HasProperty(target.ParameterId))
        {
            if (!target.MissingParameterWarningLogged)
            {
                Debug.LogWarning(
                    $"{nameof(AudioAnalyzerShaderController)} on {name}: Renderer '{target.renderer.name}' material {target.materialID} has no {target.parameterType} parameter named '{target.parameterName}'.",
                    this);
                target.MissingParameterWarningLogged = true;
            }

            return false;
        }

        return true;
    }

    private bool InitializeTarget(RendererParameterBinding target)
    {
        if (target.Initialized)
        {
            return target.TargetMaterial != null;
        }

        Material[] sharedMaterials = target.renderer.sharedMaterials;
        if (sharedMaterials == null || target.materialID < 0 || target.materialID >= sharedMaterials.Length)
        {
            if (!target.MissingParameterWarningLogged)
            {
                Debug.LogWarning(
                    $"{nameof(AudioAnalyzerShaderController)} on {name}: Renderer '{target.renderer.name}' has no material slot {target.materialID}.",
                    this);
                target.MissingParameterWarningLogged = true;
            }

            return false;
        }

        if (createMaterialInstances && !useMaterialPropertyBlocks)
        {
            Material[] materials = target.renderer.materials;
            materials[target.materialID] = new Material(materials[target.materialID]);
            target.renderer.materials = materials;
        }

        target.TargetMaterial = useMaterialPropertyBlocks
            ? target.renderer.sharedMaterials[target.materialID]
            : target.renderer.materials[target.materialID];

        if (target.TargetMaterial != null && target.parameterType == ShaderParameterType.Vector && target.TargetMaterial.HasProperty(target.parameterName))
        {
            target.CurrentVector = target.TargetMaterial.GetVector(target.parameterName);
        }

        if (useMaterialPropertyBlocks)
        {
            target.PropertyBlock = new MaterialPropertyBlock();
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
        }

        target.Initialized = true;
        return target.TargetMaterial != null;
    }

    private void ApplyValue(RendererParameterBinding target, float value, float activationWeight)
    {
        switch (target.parameterType)
        {
            case ShaderParameterType.Float:
                SetFloat(target, Mathf.LerpUnclamped(
                    target.ReturnStartScalarValue, value, activationWeight));
                break;
            case ShaderParameterType.Int:
                SetInt(target, Mathf.RoundToInt(Mathf.LerpUnclamped(
                    target.ReturnStartScalarValue, value, activationWeight)));
                break;
            case ShaderParameterType.Bool:
                SetInt(target, activationWeight >= 1f
                    ? value > target.boolThreshold ? 1 : 0
                    : Mathf.RoundToInt(target.ReturnStartScalarValue));
                break;
            case ShaderParameterType.Vector:
                SetVector(target, value, activationWeight);
                break;
            case ShaderParameterType.Color:
                Color color = target.colorRamp != null
                    ? target.colorRamp.Evaluate(Mathf.Clamp01(value))
                    : Color.white;
                SetColor(target, Color.LerpUnclamped(
                    target.ReturnStartColorValue, color, activationWeight));
                break;
            case ShaderParameterType.Keyword:
                SetKeyword(target, activationWeight >= 1f
                    ? value > target.boolThreshold
                    : target.ReturnStartKeywordValue);
                break;
        }
    }

    private void SetFloat(RendererParameterBinding target, float value)
    {
        if (useMaterialPropertyBlocks)
        {
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
            target.PropertyBlock.SetFloat(target.ParameterId, value);
            target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
            return;
        }

        target.TargetMaterial.SetFloat(target.ParameterId, value);
    }

    private void SetInt(RendererParameterBinding target, int value)
    {
        if (useMaterialPropertyBlocks)
        {
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
            target.PropertyBlock.SetInt(target.ParameterId, value);
            target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
            return;
        }

        target.TargetMaterial.SetInt(target.ParameterId, value);
    }

    private void SetVector(RendererParameterBinding target, float value, float activationWeight)
    {
        Vector4 vector = activationWeight >= 1f ? target.CurrentVector : target.ReturnStartVectorValue;
        if (target.x) { vector.x = Mathf.LerpUnclamped(target.ReturnStartVectorValue.x, value, activationWeight); }
        if (target.y) { vector.y = Mathf.LerpUnclamped(target.ReturnStartVectorValue.y, value, activationWeight); }
        if (target.z) { vector.z = Mathf.LerpUnclamped(target.ReturnStartVectorValue.z, value, activationWeight); }
        if (target.w) { vector.w = Mathf.LerpUnclamped(target.ReturnStartVectorValue.w, value, activationWeight); }
        target.CurrentVector = vector;

        if (useMaterialPropertyBlocks)
        {
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
            target.PropertyBlock.SetVector(target.ParameterId, vector);
            target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
            return;
        }

        target.TargetMaterial.SetVector(target.ParameterId, vector);
    }

    private void SetColor(RendererParameterBinding target, Color value)
    {
        if (useMaterialPropertyBlocks)
        {
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
            target.PropertyBlock.SetColor(target.ParameterId, value);
            target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
            return;
        }

        target.TargetMaterial.SetColor(target.ParameterId, value);
    }

    private void SetKeyword(RendererParameterBinding target, bool enabled)
    {
        if (enabled)
        {
            target.TargetMaterial.EnableKeyword(target.parameterName);
            return;
        }

        target.TargetMaterial.DisableKeyword(target.parameterName);
    }

    private float SmoothValue(float from, float to, float lerpSpeed)
    {
        if (lerpSpeed <= 0f)
        {
            return to;
        }

        return Mathf.Lerp(from, to, lerpSpeed * GetDeltaTime());
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

        foreach (RendererParameterBinding target in targets)
        {
            if (target == null)
            {
                continue;
            }

            target.materialID = Mathf.Max(0, target.materialID);
            target.boolThreshold = Mathf.Clamp01(target.boolThreshold);
            target.audioAnalyzerLerp = Mathf.Max(0f, target.audioAnalyzerLerp);
            target.ParameterIdCached = false;
            target.MissingParameterWarningLogged = false;
            target.Initialized = false;
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
