using UnityEngine.Playables;

public class SeededMMFVFXMixer : PlayableBehaviour
{
    private const double SeekThreshold = 0.1;
    private const double TimeEpsilon = 1e-5;

    private double lastRootTime = double.NaN;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineVFXSeededMMFTrigger trigger = playerData as TimelineVFXSeededMMFTrigger;
        if (trigger == null)
            return;

        double rootTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        bool hasLastRootTime = !double.IsNaN(lastRootTime);
        double deltaTime = hasLastRootTime ? rootTime - lastRootTime : 0.0;
        bool isSeekOrScrub = !hasLastRootTime ||
                             deltaTime <= 0.0 ||
                             deltaTime > SeekThreshold ||
                             info.evaluationType != FrameData.EvaluationType.Playback;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            ScriptPlayable<SeededMMFVFXBehaviour> inputPlayable =
                (ScriptPlayable<SeededMMFVFXBehaviour>)playable.GetInput(i);
            SeededMMFVFXBehaviour behaviour = inputPlayable.GetBehaviour();
            double localTime = inputPlayable.GetTime();
            double duration = inputPlayable.GetDuration();
            bool isInsideClipTime = localTime >= -TimeEpsilon &&
                                    (double.IsInfinity(duration) || localTime <= duration + TimeEpsilon);
            bool isActive = playable.GetInputWeight(i) > 0f && isInsideClipTime;

            if (!isActive)
            {
                if (deltaTime < 0.0)
                    behaviour.triggered = false;

                behaviour.wasActive = false;
                continue;
            }

            bool enteredClipDuringThisFrame = !behaviour.wasActive &&
                                              !isSeekOrScrub &&
                                              localTime >= -TimeEpsilon &&
                                              localTime <= deltaTime + TimeEpsilon;
            behaviour.wasActive = true;

            if (!enteredClipDuringThisFrame || behaviour.triggered)
                continue;

            behaviour.triggered = true;
            trigger.PlayWithSeed(behaviour.seed);
        }

        lastRootTime = rootTime;
    }

    public override void OnGraphStart(Playable playable)
    {
        lastRootTime = double.NaN;
    }

    public override void OnGraphStop(Playable playable)
    {
        lastRootTime = double.NaN;
    }
}
