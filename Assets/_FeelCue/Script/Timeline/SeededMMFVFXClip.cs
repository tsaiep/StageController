using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class SeededMMFVFXClip : PlayableAsset, ITimelineClipAsset
{
    public SeededMMFVFXBehaviour template = new SeededMMFVFXBehaviour();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<SeededMMFVFXBehaviour>.Create(graph, template);
    }
}
