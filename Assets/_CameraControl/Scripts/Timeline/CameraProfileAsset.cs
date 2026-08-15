using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Splines;

[System.Serializable]
public class CameraProfileAsset : PlayableAsset, ITimelineClipAsset
{
    private const float MinFixedPlaybackSpeed = 0.001f;

    public CameraProfileSO cameraProfile;

    [Tooltip("將各專案當前場景的追蹤目標，例如主角、BOSS，拖入此欄位")]
    public ExposedReference<GameObject> trackingTarget;

    [Tooltip("當使用 Dolly Profile 時，請將場景中的 Spline Container 拖入此欄位")]
    public ExposedReference<SplineContainer> splineContainer;

    //[Header("--- Playback Options ---")]
    [Tooltip("勾選後會以 1 - normalizedTime 取樣 Profile 曲線，等同將此 Clip 的運鏡倒轉播放。")]
    public bool reversePlayback;

    [Tooltip("動態鏡像 X 軸相關偏移量，不會修改原始 CameraProfileSO。")]
    public bool mirrorX;

    [Tooltip("動態鏡像 Y 軸相關偏移量，不會修改原始 CameraProfileSO。")]
    public bool mirrorY;

    [Tooltip("動態鏡像 Z 軸相關偏移量，不會修改原始 CameraProfileSO。")]
    public bool mirrorZ;

    [Tooltip("關閉時維持原本行為：Clip 長度會把 Profile 重新映射到 0~1。開啟時改用固定速度播放，Clip 拉長會在超出 Profile 範圍後 Hold。")]
    public bool useFixedPlaybackSpeed;

    [Min(MinFixedPlaybackSpeed)]
    [Tooltip("固定速度模式下，Profile normalized time 每秒前進多少。1 = 完整 Profile 用 1 秒播完。")]
    public float fixedPlaybackSpeed = 1f;

    [Tooltip("加到目前 Profile 的 fovCurve 取樣結果上的偏移量")]
    public float fovBias;

   // [Header("--- General Profile Bias ---")]
    [Tooltip("加到 GeneralProfileSO.posDistanceCurve 取樣結果上的偏移量")]
    public float posDistanceBias;

    [HideInInspector]
    [Tooltip("保留舊資料用。Screen Position Bias 容易和 Rotation Composer 互相修正，目前不套用。")]
    public float posScreenXBias;

    [HideInInspector]
    [Tooltip("保留舊資料用。Screen Position Bias 容易和 Rotation Composer 互相修正，目前不套用。")]
    public float posScreenYBias;

    [Tooltip("加到 GeneralProfileSO.posTargetOffsetXCurve 取樣結果上的偏移量")]
    public float posTargetOffsetXBias;

    [Tooltip("加到 GeneralProfileSO.posTargetOffsetYCurve 取樣結果上的偏移量")]
    public float posTargetOffsetYBias;

    [Tooltip("加到 GeneralProfileSO.posTargetOffsetZCurve 取樣結果上的偏移量")]
    public float posTargetOffsetZBias;

   // [Header("--- Tracking Profile Bias ---")]
    [Tooltip("加到 TrackingProfileSO.followOffsetXCurve 取樣結果上的偏移量")]
    public float followOffsetXBias;

    [Tooltip("加到 TrackingProfileSO.followOffsetYCurve 取樣結果上的偏移量")]
    public float followOffsetYBias;

    [Tooltip("加到 TrackingProfileSO.followOffsetZCurve 取樣結果上的偏移量")]
    public float followOffsetZBias;

    //[Header("--- Dolly Profile Bias ---")]
    [Tooltip("加到 DollyProfileSO.splinePositionCurve 取樣結果上的偏移量")]
    public float splinePositionBias;

   // [Header("--- Rotation Composer Bias ---")]
    [HideInInspector]
    [Tooltip("保留舊資料用。Rotation Screen Position Bias 會直接驅動相機旋轉，目前不套用。")]
    public float rotScreenXBias;

    [HideInInspector]
    [Tooltip("保留舊資料用。Rotation Screen Position Bias 會直接驅動相機旋轉，目前不套用。")]
    public float rotScreenYBias;

    [Tooltip("加到目前 Profile 的 rotTargetOffsetXCurve 取樣結果上的偏移量")]
    public float rotTargetOffsetXBias;

    [Tooltip("加到目前 Profile 的 rotTargetOffsetYCurve 取樣結果上的偏移量")]
    public float rotTargetOffsetYBias;

    [Tooltip("加到目前 Profile 的 rotTargetOffsetZCurve 取樣結果上的偏移量")]
    public float rotTargetOffsetZBias;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CameraProfileBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.profile = cameraProfile;
        behaviour.targetObject = trackingTarget.Resolve(graph.GetResolver());
        behaviour.splineContainer = splineContainer.Resolve(graph.GetResolver());
        behaviour.reversePlayback = reversePlayback;
        behaviour.mirrorX = mirrorX;
        behaviour.mirrorY = mirrorY;
        behaviour.mirrorZ = mirrorZ;
        behaviour.useFixedPlaybackSpeed = useFixedPlaybackSpeed;
        behaviour.fixedPlaybackSpeed = GetSafeFixedPlaybackSpeed();
        behaviour.fovBias = fovBias;
        behaviour.posDistanceBias = posDistanceBias;
        behaviour.posScreenXBias = posScreenXBias;
        behaviour.posScreenYBias = posScreenYBias;
        behaviour.posTargetOffsetXBias = posTargetOffsetXBias;
        behaviour.posTargetOffsetYBias = posTargetOffsetYBias;
        behaviour.posTargetOffsetZBias = posTargetOffsetZBias;
        behaviour.followOffsetXBias = followOffsetXBias;
        behaviour.followOffsetYBias = followOffsetYBias;
        behaviour.followOffsetZBias = followOffsetZBias;
        behaviour.splinePositionBias = splinePositionBias;
        behaviour.rotScreenXBias = rotScreenXBias;
        behaviour.rotScreenYBias = rotScreenYBias;
        behaviour.rotTargetOffsetXBias = rotTargetOffsetXBias;
        behaviour.rotTargetOffsetYBias = rotTargetOffsetYBias;
        behaviour.rotTargetOffsetZBias = rotTargetOffsetZBias;

        return playable;
    }

    public override double duration
    {
        get
        {
            return useFixedPlaybackSpeed
                ? 1.0 / GetSafeFixedPlaybackSpeed()
                : base.duration;
        }
    }

    public ClipCaps clipCaps
    {
        get
        {
            return useFixedPlaybackSpeed
                ? ClipCaps.Blending | ClipCaps.Extrapolation
                : ClipCaps.Blending;
        }
    }

    private float GetSafeFixedPlaybackSpeed()
    {
        return Mathf.Max(MinFixedPlaybackSpeed, fixedPlaybackSpeed);
    }
}

public class CameraProfileBehaviour : PlayableBehaviour
{
    public CameraProfileSO profile;
    public GameObject targetObject;
    public SplineContainer splineContainer;

    public bool reversePlayback;
    public bool mirrorX;
    public bool mirrorY;
    public bool mirrorZ;
    public bool useFixedPlaybackSpeed;
    public float fixedPlaybackSpeed = 1f;

    public float fovBias;
    public float posDistanceBias;
    public float posScreenXBias;
    public float posScreenYBias;
    public float posTargetOffsetXBias;
    public float posTargetOffsetYBias;
    public float posTargetOffsetZBias;
    public float followOffsetXBias;
    public float followOffsetYBias;
    public float followOffsetZBias;
    public float splinePositionBias;
    public float rotScreenXBias;
    public float rotScreenYBias;
    public float rotTargetOffsetXBias;
    public float rotTargetOffsetYBias;
    public float rotTargetOffsetZBias;
}
