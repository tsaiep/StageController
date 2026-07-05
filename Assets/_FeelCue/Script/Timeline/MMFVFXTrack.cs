using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

[TrackColor(0.55f, 0.25f, 0.95f)]
[TrackBindingType(typeof(TimelineMMFVFXTrigger))]
[TrackClipType(typeof(MMFVFXClip))]
[DisplayName("MMF VFX Track")]
[MovedFrom(false, null, null, "SeededMMFVFXTrack")]
public class MMFVFXTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<MMFVFXMixer> mixer = ScriptPlayable<MMFVFXMixer>.Create(graph, inputCount);
        MMFVFXMixer behaviour = mixer.GetBehaviour();
        behaviour.clipTimings = GetClips()
            .OrderBy(clip => clip.start)
            .Select(clip => new MMFVFXMixer.ClipTiming
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
        clip.displayName = "MMF VFX";
        clip.duration = 1.0;
    }
}
