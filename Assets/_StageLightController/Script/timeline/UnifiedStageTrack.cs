using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.7f, 0.3f)]
[TrackBindingType(typeof(UnifiedStageController))]
[TrackClipType(typeof(UnifiedStageClip))]
[DisplayName("Unified Stage Control Track")]
public class UnifiedStageTrack : TrackAsset
{
    // This override lets Timeline treat the clip fields as recordable properties.
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<UnifiedStageMixer>.Create(graph, inputCount);
    }

    // Keeping this override helps Timeline surface recording support on the track.
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
    }
}
