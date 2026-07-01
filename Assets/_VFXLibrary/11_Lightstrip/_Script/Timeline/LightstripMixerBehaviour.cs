using UnityEngine;
using UnityEngine.Playables;

public class LightstripMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        LightstripMBPControl controller = playerData as LightstripMBPControl;
        if (controller == null)
            return;

        int inputCount = playable.GetInputCount();
        if (inputCount == 0)
            return;

        double rootTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        LightstripPlayableBehaviour selectedBehaviour = null;
        Playable selectedPlayable = default(Playable);
        double selectedStartTime = double.NegativeInfinity;

        LightstripPlayableBehaviour fallbackBehaviour = null;
        Playable fallbackPlayable = default(Playable);
        float fallbackWeight = -1f;

        float mixedColorMultiplier = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f)
                continue;

            ScriptPlayable<LightstripPlayableBehaviour> inputPlayable =
                (ScriptPlayable<LightstripPlayableBehaviour>)playable.GetInput(i);
            LightstripPlayableBehaviour behaviour = inputPlayable.GetBehaviour();

            float blendFactor = Mathf.Clamp01(Mathf.Abs(weight - 0.5f) * 2f);
            mixedColorMultiplier += behaviour.colorMultiplier * blendFactor;

            if (weight > fallbackWeight)
            {
                fallbackWeight = weight;
                fallbackBehaviour = behaviour;
                fallbackPlayable = inputPlayable;
            }

            if (weight < 0.5f)
                continue;

            double clipStartTime = rootTime - inputPlayable.GetTime();
            if (selectedBehaviour == null || clipStartTime >= selectedStartTime)
            {
                selectedBehaviour = behaviour;
                selectedPlayable = inputPlayable;
                selectedStartTime = clipStartTime;
            }
        }

        if (selectedBehaviour == null)
        {
            selectedBehaviour = fallbackBehaviour;
            selectedPlayable = fallbackPlayable;
        }

        if (selectedBehaviour == null)
            return;

        controller.ApplyTimelineValues(
            selectedBehaviour.color,
            selectedBehaviour.gradient,
            selectedBehaviour.gradientHash,
            mixedColorMultiplier,
            selectedBehaviour.manualMode ? 1f : 0f,
            selectedBehaviour.EvaluateManualModeControl(selectedPlayable),
            selectedBehaviour.scrollingModeWeight,
            selectedBehaviour.scrollingPingPongMode,
            selectedBehaviour.scrollingFromCenter,
            selectedBehaviour.linearMode,
            selectedBehaviour.sparklingModeWeight,
            selectedBehaviour.sparklingModeRandomWeight,
            selectedBehaviour.scrollingSpeed,
            selectedBehaviour.scrollingFrequency,
            selectedBehaviour.scrollingIntervalDuration,
            selectedBehaviour.scrollingHoldDuration,
            selectedBehaviour.scrollingHeadLean,
            selectedBehaviour.scrollingSmoothFactor,
            selectedBehaviour.sparklingSpeed,
            selectedBehaviour.sparklingSmoothFactor);

        controller.ApplyTimelineTime((float)rootTime);
    }
}
