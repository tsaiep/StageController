using UnityEngine.Playables;

public class SeededMMFVFXMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineVFXSeededMMFTrigger trigger = playerData as TimelineVFXSeededMMFTrigger;
        if (trigger == null)
            return;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            ScriptPlayable<SeededMMFVFXBehaviour> inputPlayable =
                (ScriptPlayable<SeededMMFVFXBehaviour>)playable.GetInput(i);
            SeededMMFVFXBehaviour behaviour = inputPlayable.GetBehaviour();

            if (playable.GetInputWeight(i) <= 0f)
            {
                behaviour.triggered = false;
                continue;
            }

            if (behaviour.triggered)
                continue;

            behaviour.triggered = true;
            trigger.PlayWithSeed(behaviour.seed);
        }
    }
}
