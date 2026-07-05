using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(false, null, null, "SeededMMFVFXMixer")]
public class MMFVFXMixer : PlayableBehaviour
{
    private const double SeekThreshold = 0.1;
    private const double TimeEpsilon = 1e-5;

    public ClipTiming[] clipTimings;

    private double lastRootTime = double.NaN;

    public struct ClipTiming
    {
        public double start;
        public double end;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        TimelineMMFVFXTrigger trigger = playerData as TimelineMMFVFXTrigger;
        if (trigger == null)
            return;

        double rootTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;
        if (director == null || director.state != PlayState.Playing)
        {
            lastRootTime = rootTime;
            return;
        }

        bool hasLastRootTime = !double.IsNaN(lastRootTime);
        double deltaTime = hasLastRootTime ? rootTime - lastRootTime : 0.0;
        bool isSeekOrScrub = !hasLastRootTime ||
                             deltaTime <= 0.0 ||
                             deltaTime > SeekThreshold ||
                             info.evaluationType != FrameData.EvaluationType.Playback;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            ScriptPlayable<MMFVFXBehaviour> inputPlayable =
                (ScriptPlayable<MMFVFXBehaviour>)playable.GetInput(i);
            MMFVFXBehaviour behaviour = inputPlayable.GetBehaviour();
            ClipTiming clipTiming = GetClipTiming(i);

            if (rootTime < clipTiming.start - TimeEpsilon)
            {
                behaviour.triggered = false;
                behaviour.wasActive = false;
                continue;
            }

            bool enteredClipDuringThisFrame =
                                              !isSeekOrScrub &&
                                              lastRootTime < clipTiming.start - TimeEpsilon &&
                                              rootTime >= clipTiming.start - TimeEpsilon &&
                                              rootTime <= clipTiming.end + TimeEpsilon;

            if (!enteredClipDuringThisFrame || behaviour.triggered)
                continue;

            behaviour.triggered = true;
            behaviour.wasActive = true;
            trigger.Play();
        }

        lastRootTime = rootTime;
    }

    private ClipTiming GetClipTiming(int inputIndex)
    {
        if (clipTimings != null && inputIndex >= 0 && inputIndex < clipTimings.Length)
            return clipTimings[inputIndex];

        return new ClipTiming
        {
            start = double.PositiveInfinity,
            end = double.PositiveInfinity
        };
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
