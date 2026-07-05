using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.9f, 0.55f, 0.15f)]
[TrackBindingType(typeof(TimelineVFXSeededMMFTrigger))]
[TrackClipType(typeof(SeededMMFVFXClip))]
[DisplayName("Seeded MMF VFX Track")]
public class SeededMMFVFXTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<SeededMMFVFXMixer> mixer = ScriptPlayable<SeededMMFVFXMixer>.Create(graph, inputCount);
        SeededMMFVFXMixer behaviour = mixer.GetBehaviour();
        behaviour.clipTimings = GetClips()
            .OrderBy(clip => clip.start)
            .Select(clip => new SeededMMFVFXMixer.ClipTiming
            {
                start = clip.start,
                end = clip.end
            })
            .ToArray();

        return mixer;
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);
        clip.displayName = "Seeded MMF VFX";
        clip.duration = 0.05;
    }
}
