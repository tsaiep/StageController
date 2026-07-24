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
        if (!TryGetSourceValue(out float sourceValue))
        {
            return;
        }

        currentSourceValue = sourceValue;
        float targetValue = sourceValue * audioAnalyzerMultiplier + audioAnalyzerOffset;
        currentValue = Mathf.Lerp(currentValue, targetValue, audioAnalyzerLerp * GetDeltaTime());
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
                    target.visualEffect.SetFloat(target.ParameterId, target.CurrentValue);
                    break;
                case VFXParameterType.Int:
                    target.visualEffect.SetInt(target.ParameterId, Mathf.RoundToInt(target.CurrentValue));
                    break;
                case VFXParameterType.Bool:
                    target.visualEffect.SetBool(target.ParameterId, target.CurrentValue > target.boolThreshold);
                    break;
                case VFXParameterType.Vector2:
                    SetVector2(target, target.CurrentValue);
                    break;
                case VFXParameterType.Vector3:
                    SetVector3(target, target.CurrentValue);
                    break;
                case VFXParameterType.Vector4:
                    SetVector4(target, target.CurrentValue);
                    break;
                case VFXParameterType.Color:
                    Color color = target.colorRamp != null
                        ? target.colorRamp.Evaluate(remappedNormalizedValue)
                        : Color.white;
                    target.visualEffect.SetVector4(target.ParameterId, new Vector4(color.r, color.g, color.b, color.a));
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

    private void SetVector2(VFXParameterBinding target, float value)
    {
        Vector2 vector = target.visualEffect.GetVector2(target.ParameterId);
        if (target.x) { vector.x = value; }
        if (target.y) { vector.y = value; }
        target.visualEffect.SetVector2(target.ParameterId, vector);
    }

    private void SetVector3(VFXParameterBinding target, float value)
    {
        Vector3 vector = target.visualEffect.GetVector3(target.ParameterId);
        if (target.x) { vector.x = value; }
        if (target.y) { vector.y = value; }
        if (target.z) { vector.z = value; }
        target.visualEffect.SetVector3(target.ParameterId, vector);
    }

    private void SetVector4(VFXParameterBinding target, float value)
    {
        Vector4 vector = target.visualEffect.GetVector4(target.ParameterId);
        if (target.x) { vector.x = value; }
        if (target.y) { vector.y = value; }
        if (target.z) { vector.z = value; }
        if (target.w) { vector.w = value; }
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
