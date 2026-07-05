using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Timeline;

[System.Serializable]
[MovedFrom(false, null, null, "SeededMMFVFXClip")]
public class MMFVFXClip : PlayableAsset, ITimelineClipAsset
{
    public MMFVFXBehaviour template = new MMFVFXBehaviour();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<MMFVFXBehaviour>.Create(graph, template);
    }
}
