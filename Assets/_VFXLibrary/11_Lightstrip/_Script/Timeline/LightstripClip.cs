using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[DisplayName("Lightstrip Clip")]
public class LightstripClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Manual Mode")]
    [Tooltip("When enabled, outputs manualMode = 1. When disabled, outputs manualMode = 0.")]
    public bool manualMode;

    [Tooltip("X axis is normalized clip time: 0 = clip start, 1 = clip end.")]
    public AnimationCurve manualModeControl = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Light Units")]
    [Min(0f)] public float lightUnitCount = 12f;

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

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<LightstripPlayableBehaviour> playable = ScriptPlayable<LightstripPlayableBehaviour>.Create(graph);
        LightstripPlayableBehaviour behaviour = playable.GetBehaviour();

        behaviour.manualMode = manualMode;
        behaviour.manualModeControl = manualModeControl;
        behaviour.lightUnitCount = lightUnitCount;
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
