using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Splines;
using UnityEngine.Serialization;
using Unity.Cinemachine;
using Runtime.CameraSystem;

public enum CameraProfileBlendMode
{
    ParameterBlend = 0,
    CrossFade = 1,
    CrossFadeBlur = 2,
    MotionCut = 3
}

public enum CameraProfileDirectionalAxis
{
    Horizontal = 0,
    Vertical = 1,
    Depth = 2
}

[System.Serializable]
public class CameraProfileAsset : PlayableAsset, ITimelineClipAsset
{
    private const float MinFixedPlaybackSpeed = 0.001f;
    internal const float MaxCrossFadeBlurIntensity = 5f;

    public CameraProfileSO cameraProfile;

    [Tooltip("將各專案當前場景的追蹤目標，例如主角、BOSS，拖入此欄位")]
    public ExposedReference<GameObject> trackingTarget;

    [Tooltip("當使用 Dolly Profile 時，請將場景中的 Spline Container 拖入此欄位")]
    public ExposedReference<SplineContainer> splineContainer;

    [Tooltip("Overlap 時的混合方式。Parameter Blend 會混合 Profile 參數；Storyboard RT Cross Fade 會用 RenderTexture 疊圖淡入；Cross Fade Blur 會在相同淡入期間讓整體畫面清楚、模糊、再恢復清楚。")]
    public CameraProfileBlendMode blendMode = CameraProfileBlendMode.ParameterBlend;

    [Range(0f, MaxCrossFadeBlurIntensity)]
    [Tooltip("Cross Fade Blur 在 overlap 中點使用的最大模糊強度。每個 incoming Clip 可獨立設定；1 保留原本強度。")]
    public float crossFadeBlurMaxIntensity = 1f;

    [Range(0f, 1f)]
    [Tooltip("壓縮 RenderTexture Alpha 混合發生的時間。0 保留原本線性結果；提高後會延後開始淡入並提早達到 1，不影響 Blur 曲線。")]
    public float crossFadeAlphaTiming;

    [FormerlySerializedAs("directionalAxis")]
    [Tooltip("Motion Cut 的位移軸。Horizontal / Vertical / Depth 分別對應相機 Local X / Y / Z。")]
    public CameraProfileDirectionalAxis motionCutAxis =
        CameraProfileDirectionalAxis.Horizontal;

    [FormerlySerializedAs("directionalStrength")]
    [Tooltip("Motion Cut 前一個 Clip 的位移量（公尺）。正值代表右 / 上 / 前；負值代表左 / 下 / 後。")]
    public float motionCutOutStrength = 1f;

    [Tooltip("Motion Cut 後一個 Clip 的位移量（公尺）。")]
    public float motionCutInStrength = 1f;

    [Tooltip("反轉 incoming Clip 的移動方向。開啟後，相同正值的 Out / In Strength 會朝相反方向運動。")]
    public bool reverseMotionCutInStrength = true;

    [FormerlySerializedAs("directionalCurve")]
    [Tooltip("Motion Cut 位移的 0~1 速度曲線。Out 使用正向取樣，In 使用反向取樣。")]
    public AnimationCurve motionCutCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [FormerlySerializedAs("motionCutOutPanAngle")]
    [Tooltip("Motion Cut 前後兩個 Clip 加總的 Roll 角度（度），繞攝影機 Local Z 軸旋轉。")]
    public float motionCutRollAngle;

    [FormerlySerializedAs("motionCutPanCurve")]
    [Tooltip("Motion Cut Roll 的 0~1 速度曲線。Out 使用正向取樣，In 使用反向取樣。")]
    public AnimationCurve motionCutRollCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("為這個 Clip 開啟 Cinemachine 手持攝影機 Noise。")]
    public bool enableNoise;

    [Tooltip("手持晃動使用的 Cinemachine Noise Settings。開啟 Noise 時必須指定。")]
    public NoiseSettings noiseProfile;

    [Min(0f)]
    [Tooltip("晃動幅度倍率。1 代表 Noise Profile 的原始強度。")]
    public float noiseAmplitude = 1f;

    [Min(0f)]
    [Tooltip("晃動速度倍率。1 代表 Noise Profile 的原始速度。")]
    public float noiseFrequency = 1f;

    [Tooltip("為這個 Clip 啟用自訂動態景深。只控制 DOF，不會修改攝影機 FOV。")]
    public bool enableDepthOfField;

    [Tooltip("X 是 Clip 進度，Y 是 0~1 的對焦距離；0 對應 Focus Min，1 對應 Focus Max。")]
    public AnimationCurve normalizedFocusDistanceCurve =
        AnimationCurve.Linear(0f, 0.2f, 1f, 0.2f);

    [FormerlySerializedAs("focusDistanceRemapMin")]
    [Min(0.01f)]
    [Tooltip("Normalized Focus Distance = 0 時的對焦距離（公尺）。")]
    public float focusDistanceMin = 0.3f;

    [FormerlySerializedAs("focusDistanceRemapMax")]
    [Min(0.02f)]
    [Tooltip("Normalized Focus Distance = 1 時的對焦距離（公尺）。")]
    public float focusDistanceMax = 50f;

    [Min(0.01f)]
    [Tooltip("焦點前方從清晰過渡到最模糊所需的距離（公尺）。")]
    public float depthOfFieldNearRange = 1f;

    [Min(0.01f)]
    [Tooltip("焦點後方從清晰過渡到最模糊所需的距離（公尺）。")]
    public float depthOfFieldFarRange = 3f;

    [FormerlySerializedAs("depthOfFieldFarRadius")]
    [Range(0f, 64f)]
    [Tooltip("近景與遠景共用的最大模糊半徑；Renderer Feature 會依輸出解析度等比例縮放。")]
    public float depthOfFieldMaxRadius = 24f;

    [Tooltip("切換 DOF 最終畫面或 Near/Far/Focus Plane 可視化。")]
    public CameraDepthOfFieldDebugView depthOfFieldDebugView;

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
        behaviour.blendMode = blendMode;
        behaviour.crossFadeBlurMaxIntensity = crossFadeBlurMaxIntensity;
        behaviour.crossFadeAlphaTiming = crossFadeAlphaTiming;
        behaviour.motionCutAxis = motionCutAxis;
        behaviour.motionCutOutStrength = motionCutOutStrength;
        behaviour.motionCutInStrength = motionCutInStrength;
        behaviour.reverseMotionCutInStrength = reverseMotionCutInStrength;
        behaviour.motionCutCurve = motionCutCurve;
        behaviour.motionCutRollAngle = motionCutRollAngle;
        behaviour.motionCutRollCurve = motionCutRollCurve;
        behaviour.enableNoise = enableNoise;
        behaviour.noiseProfile = noiseProfile;
        behaviour.noiseAmplitude = Mathf.Max(0f, noiseAmplitude);
        behaviour.noiseFrequency = Mathf.Max(0f, noiseFrequency);
        behaviour.enableDepthOfField = enableDepthOfField;
        behaviour.normalizedFocusDistanceCurve =
            normalizedFocusDistanceCurve;
        behaviour.focusDistanceMin = Mathf.Max(
            0.01f,
            focusDistanceMin
        );
        behaviour.focusDistanceMax = Mathf.Max(
            behaviour.focusDistanceMin + 0.01f,
            focusDistanceMax
        );
        behaviour.depthOfFieldNearRange = Mathf.Max(
            0.01f,
            depthOfFieldNearRange
        );
        behaviour.depthOfFieldFarRange = Mathf.Max(
            0.01f,
            depthOfFieldFarRange
        );
        behaviour.depthOfFieldMaxRadius = Mathf.Clamp(
            depthOfFieldMaxRadius,
            0f,
            64f
        );
        behaviour.depthOfFieldDebugView = depthOfFieldDebugView;
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
    public CameraProfileBlendMode blendMode;
    public float crossFadeBlurMaxIntensity = 1f;
    public float crossFadeAlphaTiming;
    public CameraProfileDirectionalAxis motionCutAxis;
    public float motionCutOutStrength = 1f;
    public float motionCutInStrength = 1f;
    public bool reverseMotionCutInStrength = true;
    public AnimationCurve motionCutCurve;
    public float motionCutRollAngle;
    public AnimationCurve motionCutRollCurve;

    public bool enableNoise;
    public NoiseSettings noiseProfile;
    public float noiseAmplitude = 1f;
    public float noiseFrequency = 1f;

    public bool enableDepthOfField;
    public AnimationCurve normalizedFocusDistanceCurve;
    public float focusDistanceMin = 0.3f;
    public float focusDistanceMax = 50f;
    public float depthOfFieldNearRange = 1f;
    public float depthOfFieldFarRange = 3f;
    public float depthOfFieldMaxRadius = 24f;
    public CameraDepthOfFieldDebugView depthOfFieldDebugView;

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
