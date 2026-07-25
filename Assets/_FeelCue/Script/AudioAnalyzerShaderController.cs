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
        if (!TryGetSourceValue(out float sourceValue))
        {
            return;
        }

        currentSourceValue = sourceValue;
        float targetValue = sourceValue * audioAnalyzerMultiplier + audioAnalyzerOffset;
        currentValue = SmoothValue(currentValue, targetValue, audioAnalyzerLerp);
        currentValueNormalized = Mathf.Clamp01(sourceValue);

        ApplyValueToTargets(sourceValue);
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

    private void ApplyValueToTargets(float sourceValue)
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

            ApplyValue(target, target.CurrentValue);
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

    private void ApplyValue(RendererParameterBinding target, float value)
    {
        switch (target.parameterType)
        {
            case ShaderParameterType.Float:
                SetFloat(target, value);
                break;
            case ShaderParameterType.Int:
                SetInt(target, Mathf.RoundToInt(value));
                break;
            case ShaderParameterType.Bool:
                SetInt(target, value > target.boolThreshold ? 1 : 0);
                break;
            case ShaderParameterType.Vector:
                SetVector(target, value);
                break;
            case ShaderParameterType.Color:
                SetColor(target, target.colorRamp != null ? target.colorRamp.Evaluate(Mathf.Clamp01(value)) : Color.white);
                break;
            case ShaderParameterType.Keyword:
                SetKeyword(target, value > target.boolThreshold);
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

    private void SetVector(RendererParameterBinding target, float value)
    {
        if (target.x) { target.CurrentVector.x = value; }
        if (target.y) { target.CurrentVector.y = value; }
        if (target.z) { target.CurrentVector.z = value; }
        if (target.w) { target.CurrentVector.w = value; }

        if (useMaterialPropertyBlocks)
        {
            target.renderer.GetPropertyBlock(target.PropertyBlock, target.materialID);
            target.PropertyBlock.SetVector(target.ParameterId, target.CurrentVector);
            target.renderer.SetPropertyBlock(target.PropertyBlock, target.materialID);
            return;
        }

        target.TargetMaterial.SetVector(target.ParameterId, target.CurrentVector);
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
