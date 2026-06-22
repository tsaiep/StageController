using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class LightstripPlayableBehaviour : PlayableBehaviour
{
    [Header("Manual Mode")]
    public bool manualMode;
    public AnimationCurve manualModeControl = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Color")]
    public Color color = Color.white;
    public float colorMultiplier = 1f;
    public Gradient gradient = LightstripMBPControl.CreateDefaultGradient();
    public int gradientHash;

    [Header("Animation Control")]
    public float scrollingModeWeight = 1f;
    public float scrollingPingPongMode = 0f;
    public float scrollingFromCenter = 0f;
    public float sparklingModeWeight = 0f;
    public float sparklingModeRandomWeight = 0f;

    [Header("Scrolling")]
    public float scrollingSpeed = 1f;
    public float scrollingFrequency = 1f;
    public float scrollingIntervalDuration = 0f;
    public float scrollingHoldDuration = 0f;
    public float scrollingHeadLean = 0f;
    public float scrollingSmoothFactor = 0f;

    [Header("Sparkling")]
    public float sparklingSpeed = 3f;
    public float sparklingSmoothFactor = 1f;

    public float GetNormalizedClipTime(Playable playable)
    {
        double duration = playable.GetDuration();
        if (duration <= 0.0)
            return 0f;

        return Mathf.Clamp01((float)(playable.GetTime() / duration));
    }

    public float EvaluateManualModeControl(Playable playable)
    {
        if (manualModeControl == null)
            return 0f;

        float value = manualModeControl.Evaluate(GetNormalizedClipTime(playable));
        return value - Mathf.Floor(value);
    }
}
