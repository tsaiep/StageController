using System.ComponentModel;
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
        return ScriptPlayable<SeededMMFVFXMixer>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        base.OnCreateClip(clip);
        clip.displayName = "Seeded MMF VFX";
        clip.duration = 0.05;
    }
}
