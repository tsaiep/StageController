using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Splines;

[System.Serializable]
public class CameraProfileAsset : PlayableAsset
{
    public CameraProfileSO cameraProfile;

    [Tooltip("將各專案當前場景的追蹤目標，例如主角、BOSS，拖入此欄位")]
    public ExposedReference<GameObject> trackingTarget;

    [Tooltip("當使用 Dolly Profile 時，請將場景中的 Spline Container 拖入此欄位")]
    public ExposedReference<SplineContainer> splineContainer;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraProfileBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.profile = cameraProfile;
        behaviour.targetObject = trackingTarget.Resolve(graph.GetResolver());
        behaviour.splineContainer = splineContainer.Resolve(graph.GetResolver());

        return playable;
    }
}

public class CameraProfileBehaviour : PlayableBehaviour
{
    public CameraProfileSO profile;
    public GameObject targetObject;
    public SplineContainer splineContainer;
}