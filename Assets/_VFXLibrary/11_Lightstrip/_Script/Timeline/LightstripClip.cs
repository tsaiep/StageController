using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[DisplayName("Lightstrip Clip")]
public class LightstripClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Template")]
    [Tooltip("Template selected in the inspector. Use Apply Template to copy selected categories into this clip.")]
    public LightstripTemplate selectedTemplate;
    public bool applyTemplateManualModeSettings = true;
    public bool applyTemplateColorSettings = true;
    public bool applyTemplateAnimationSettings = true;
    [Tooltip("Prefab used by the template selector preview. The prefab root or a child should contain LightstripMBPControl.")]
    public GameObject templatePreviewPrefab;

    [Header("Manual Mode")]
    [Tooltip("When enabled, outputs manualMode = 1. When disabled, outputs manualMode = 0.")]
    public bool manualMode;
    [Tooltip("X axis is normalized clip time: 0 = clip start, 1 = clip end.")]
    public AnimationCurve manualModeControl = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Color")]
    [ColorUsage(true, true)] public Color color = Color.white;
    [Min(0f)] public float colorMultiplier = 1f;
    [GradientUsage(true)] public Gradient gradient = LightstripMBPControl.CreateDefaultGradient();

    [Header("Animation Control")]
    [Range(0f, 1f)] public float scrollingModeWeight = 1f;
    [Range(0f, 1f)] public float scrollingPingPongMode = 0f;
    [Range(0f, 1f)] public float scrollingFromCenter = 0f;
    [Range(0f, 1f)] public float sparklingModeWeight = 0f;
    [Range(0f, 1f)] public float sparklingModeRandomWeight = 0f;

    [Header("Scrolling")]
    [Min(0f)] public float scrollingSpeed = 1f;
    [Min(0f)] public float scrollingFrequency = 1f;
    [Range(0f, 1f)] public float scrollingIntervalDuration = 0f;
    [Range(0f, 1f)] public float scrollingHoldDuration = 0f;
    [Range(-1f, 1f)] public float scrollingHeadLean = 0f;
    [Range(0f, 1f)] public float scrollingSmoothFactor = 0f;

    [Header("Sparkling")]
    [Min(0f)] public float sparklingSpeed = 3f;
    [Range(0f, 1f)] public float sparklingSmoothFactor = 1f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public void ApplyTemplateValues(LightstripTemplate template)
    {
        if (template == null)
            return;

        selectedTemplate = template;

        if (applyTemplateManualModeSettings)
        {
            manualMode = template.manualMode;
            manualModeControl = CloneAnimationCurve(template.manualModeControl);
        }

        if (applyTemplateColorSettings)
        {
            color = template.color;
            colorMultiplier = template.colorMultiplier;
            gradient = CloneGradient(template.gradient);
        }

        if (applyTemplateAnimationSettings)
        {
            scrollingModeWeight = template.scrollingModeWeight;
            scrollingPingPongMode = template.scrollingPingPongMode;
            scrollingFromCenter = template.scrollingFromCenter;
            sparklingModeWeight = template.sparklingModeWeight;
            sparklingModeRandomWeight = template.sparklingModeRandomWeight;
            scrollingSpeed = template.scrollingSpeed;
            scrollingFrequency = template.scrollingFrequency;
            scrollingIntervalDuration = template.scrollingIntervalDuration;
            scrollingHoldDuration = template.scrollingHoldDuration;
            scrollingHeadLean = template.scrollingHeadLean;
            scrollingSmoothFactor = template.scrollingSmoothFactor;
            sparklingSpeed = template.sparklingSpeed;
            sparklingSmoothFactor = template.sparklingSmoothFactor;
        }
    }

    public void CopyValuesToTemplate(LightstripTemplate template)
    {
        if (template == null)
            return;

        template.manualMode = manualMode;
        template.manualModeControl = CloneAnimationCurve(manualModeControl);
        template.color = color;
        template.colorMultiplier = colorMultiplier;
        template.gradient = CloneGradient(gradient);
        template.scrollingModeWeight = scrollingModeWeight;
        template.scrollingPingPongMode = scrollingPingPongMode;
        template.scrollingFromCenter = scrollingFromCenter;
        template.sparklingModeWeight = sparklingModeWeight;
        template.sparklingModeRandomWeight = sparklingModeRandomWeight;
        template.scrollingSpeed = scrollingSpeed;
        template.scrollingFrequency = scrollingFrequency;
        template.scrollingIntervalDuration = scrollingIntervalDuration;
        template.scrollingHoldDuration = scrollingHoldDuration;
        template.scrollingHeadLean = scrollingHeadLean;
        template.scrollingSmoothFactor = scrollingSmoothFactor;
        template.sparklingSpeed = sparklingSpeed;
        template.sparklingSmoothFactor = sparklingSmoothFactor;
    }

    public static Gradient CloneGradient(Gradient source)
    {
        if (source == null)
            return null;

        Gradient clone = new Gradient();
        clone.SetKeys(source.colorKeys, source.alphaKeys);
        clone.mode = source.mode;
        return clone;
    }

    public static AnimationCurve CloneAnimationCurve(AnimationCurve source)
    {
        if (source == null)
            return null;

        return new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<LightstripPlayableBehaviour> playable = ScriptPlayable<LightstripPlayableBehaviour>.Create(graph);
        LightstripPlayableBehaviour behaviour = playable.GetBehaviour();

        behaviour.manualMode = manualMode;
        behaviour.manualModeControl = manualModeControl;
        behaviour.color = color;
        behaviour.colorMultiplier = colorMultiplier;
        behaviour.gradient = gradient;
        behaviour.gradientHash = LightstripMBPControl.GetGradientContentHash(gradient);
        behaviour.scrollingModeWeight = scrollingModeWeight;
        behaviour.scrollingPingPongMode = scrollingPingPongMode;
        behaviour.scrollingFromCenter = scrollingFromCenter;
        behaviour.sparklingModeWeight = sparklingModeWeight;
        behaviour.sparklingModeRandomWeight = sparklingModeRandomWeight;
        behaviour.scrollingSpeed = scrollingSpeed;
        behaviour.scrollingFrequency = scrollingFrequency;
        behaviour.scrollingIntervalDuration = scrollingIntervalDuration;
        behaviour.scrollingHoldDuration = scrollingHoldDuration;
        behaviour.scrollingHeadLean = scrollingHeadLean;
        behaviour.scrollingSmoothFactor = scrollingSmoothFactor;
        behaviour.sparklingSpeed = sparklingSpeed;
        behaviour.sparklingSmoothFactor = sparklingSmoothFactor;

        return playable;
    }
}
