using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using UnityEngine.Splines;
using Runtime.CameraSystem;

[TrackColor(0.3f, 0.6f, 0.85f)]
[TrackBindingType(typeof(CameraSystemMaster))]
[TrackClipType(typeof(CameraProfileAsset))]
public class CameraProfileTrack : TrackAsset
{
    [Header("--- General Cut Prewarm ---")]
    [Tooltip("在下一個 General Clip 開始前幾秒，先把另一台 General Camera 預先就位。建議 0.5 ~ 1.0。")]
    public double generalPrewarmTime = 0.8;

    [Tooltip("General hard cut 後，前幾幀強制 Position / Rotation damping = 0，避免切過去後微調旋轉。建議 2 ~ 3。")]
    [Range(0, 10)]
    public int generalCutZeroDampingFrames = 2;

    [Tooltip("Clip 之間有空白 gap 時，是否維持上一台 live camera。建議開啟，這樣 General A/B 狀態不會因短 gap 被重置。")]
    public bool keepLastCameraDuringGap = true;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<CameraProfileMixer> mixer =
            ScriptPlayable<CameraProfileMixer>.Create(graph, inputCount);

        CameraProfileMixer behaviour = mixer.GetBehaviour();
        behaviour.Initialize(
            GetClips().ToArray(),
            generalPrewarmTime,
            generalCutZeroDampingFrames,
            keepLastCameraDuringGap
        );

        return mixer;
    }
}

public class CameraProfileMixer : PlayableBehaviour
{
    private enum CameraProfileKind
    {
        None,
        General,
        Tracking,
        Dolly
    }

    private TimelineClip[] _clips;
    private double _generalPrewarmTime = 0.8;
    private int _generalCutZeroDampingFrames = 2;
    private bool _keepLastCameraDuringGap = true;

    private CameraProfileSO _lastProfile;
    private Transform _lastTarget;
    private SplineContainer _lastSpline;
    private int _lastDominantInputIndex = -1;
    private CameraProfileKind _lastKind = CameraProfileKind.None;

    private bool _currentGeneralUseB;
    private bool _hasUsedGeneralCamera;
    private bool _hasEverAppliedCamera;

    private int _preparedGeneralInputIndex = -1;
    private bool _preparedGeneralUseB;

    private int _generalZeroDampingUntilFrame = -1;

    public void Initialize(
        TimelineClip[] clips,
        double generalPrewarmTime,
        int generalCutZeroDampingFrames,
        bool keepLastCameraDuringGap)
    {
        _clips = clips;
        _generalPrewarmTime = Mathf.Max(0.01f, (float)generalPrewarmTime);
        _generalCutZeroDampingFrames = Mathf.Max(0, generalCutZeroDampingFrames);
        _keepLastCameraDuringGap = keepLastCameraDuringGap;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CameraSystemMaster master = playerData as CameraSystemMaster;

        if (master == null)
        {
            ClearState();
            return;
        }

        CameraProfileSO currentProfile = null;
        Transform finalTarget = null;
        SplineContainer finalSpline = null;

        float maxWeight = -1f;
        float normalizedTime = 0f;
        double dominantLocalTime = 0.0;
        int dominantInputIndex = -1;
        int activeClipCount = 0;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);

            if (inputWeight <= 0f)
                continue;

            activeClipCount++;

            ScriptPlayable<CameraProfileBehaviour> inputPlayable =
                (ScriptPlayable<CameraProfileBehaviour>)playable.GetInput(i);

            CameraProfileBehaviour behaviour = inputPlayable.GetBehaviour();

            if (behaviour == null || behaviour.profile == null)
                continue;

            if (inputWeight > maxWeight)
            {
                maxWeight = inputWeight;
                dominantInputIndex = i;
                currentProfile = behaviour.profile;

                finalTarget = behaviour.targetObject != null
                    ? behaviour.targetObject.transform
                    : null;

                finalSpline = behaviour.splineContainer;

                double duration = inputPlayable.GetDuration();
                double currentTime = inputPlayable.GetTime();

                dominantLocalTime = currentTime;

                normalizedTime = duration > 0 && !double.IsInfinity(duration)
                    ? Mathf.Clamp01((float)(currentTime / duration))
                    : 0f;
            }
        }

        double currentTimelineTime = GetCurrentTimelineTime(
            playable,
            dominantInputIndex,
            dominantLocalTime
        );

        if (maxWeight <= 0f || currentProfile == null)
        {
            // 空白 gap：不清掉 A/B 狀態，並且仍然可以提前預熱下一個 General。
            PrewarmNextGeneralClipContinuously(
                playable,
                master,
                currentTimelineTime,
                -1
            );

            if (!_keepLastCameraDuringGap || !_hasEverAppliedCamera)
            {
                master.DisableAllCameras();
            }

            return;
        }

        CameraProfileKind currentKind = GetProfileKind(currentProfile);

        bool isClipChanged =
            dominantInputIndex != _lastDominantInputIndex ||
            currentProfile != _lastProfile ||
            finalTarget != _lastTarget ||
            finalSpline != _lastSpline ||
            currentKind != _lastKind;

        bool isOverlapping = activeClipCount > 1;
        bool isHardCutFrame = !isOverlapping && isClipChanged;

        // 有效 Clip 期間：不管目前是 General / Tracking / Dolly，
        // 都可以往後找下一個 General，並在接近時預熱 General A/B。
        PrewarmNextGeneralClipContinuously(
            playable,
            master,
            currentTimelineTime,
            dominantInputIndex
        );

        bool usedPreparedCamera;

        CinemachineCamera activeCamera = GetCameraForProfile(
            master,
            currentKind,
            dominantInputIndex,
            isHardCutFrame,
            out usedPreparedCamera
        );

        if (activeCamera == null)
        {
            master.DisableAllCameras();
            ClearState();
            return;
        }

        if (isHardCutFrame && currentKind == CameraProfileKind.General)
        {
            _generalZeroDampingUntilFrame =
                Application.isPlaying
                    ? Time.frameCount + _generalCutZeroDampingFrames
                    : -1;
        }

        bool forceZeroDampingForActiveGeneral =
            currentKind == CameraProfileKind.General &&
            IsWithinGeneralZeroDampingWindow();

        ApplyProfileToCamera(
            activeCamera,
            currentProfile,
            finalTarget,
            finalSpline,
            normalizedTime,
            forceZeroDampingForActiveGeneral
        );

        if (isHardCutFrame && !usedPreparedCamera)
        {
            PrepareCameraImmediately(activeCamera);
        }

        master.SetOnlyThisCameraLive(activeCamera);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            activeCamera.InternalUpdateCameraState(Vector3.up, 0.016f);
        }
#endif

        if (currentKind == CameraProfileKind.General)
        {
            _hasUsedGeneralCamera = true;
        }

        _hasEverAppliedCamera = true;

        _lastProfile = currentProfile;
        _lastTarget = finalTarget;
        _lastSpline = finalSpline;
        _lastDominantInputIndex = dominantInputIndex;
        _lastKind = currentKind;
    }

    private double GetCurrentTimelineTime(
        Playable playable,
        int dominantInputIndex,
        double dominantLocalTime)
    {
        if (_clips != null &&
            dominantInputIndex >= 0 &&
            dominantInputIndex < _clips.Length &&
            _clips[dominantInputIndex] != null)
        {
            return _clips[dominantInputIndex].start + dominantLocalTime;
        }

        return playable.GetTime();
    }

    private void PrewarmNextGeneralClipContinuously(
        Playable playable,
        CameraSystemMaster master,
        double currentTimelineTime,
        int currentInputIndex)
    {
        if (master == null)
            return;

        if (master.generalCamera == null || master.generalCameraB == null)
            return;

        if (_clips == null || _clips.Length == 0)
            return;

        int nextInputIndex = FindNextGeneralClipIndex(
            playable,
            currentTimelineTime,
            playable.GetInputCount()
        );

        if (nextInputIndex < 0)
            return;

        TimelineClip nextClip = _clips[nextInputIndex];

        if (nextClip == null)
            return;

        // 如果下一個 General 和目前正在播放的 Clip 有重疊，先視為 blend，不走 hard cut 預熱。
        if (currentInputIndex >= 0 &&
            currentInputIndex < _clips.Length &&
            _clips[currentInputIndex] != null &&
            nextClip.start < _clips[currentInputIndex].end)
        {
            return;
        }

        double timeUntilNextClip = nextClip.start - currentTimelineTime;

        if (timeUntilNextClip < 0.0)
            return;

        if (timeUntilNextClip > _generalPrewarmTime)
            return;

        CameraProfileBehaviour nextBehaviour = GetBehaviourFromInput(
            playable,
            nextInputIndex
        );

        if (nextBehaviour == null)
            return;

        GeneralProfileSO nextGeneralProfile = nextBehaviour.profile as GeneralProfileSO;

        if (nextGeneralProfile == null)
            return;

        Transform nextTarget = nextBehaviour.targetObject != null
            ? nextBehaviour.targetObject.transform
            : null;

        bool nextUseB = _hasUsedGeneralCamera
            ? !_currentGeneralUseB
            : false;

        CinemachineCamera prewarmCamera = master.GetGeneralCamera(nextUseB);

        if (prewarmCamera == null)
            return;

        ApplyGeneralProfile(
            prewarmCamera,
            nextGeneralProfile,
            nextTarget,
            0f,
            true
        );

        SnapInactiveCameraToPreparedState(prewarmCamera);

        _preparedGeneralInputIndex = nextInputIndex;
        _preparedGeneralUseB = nextUseB;
    }

    private int FindNextGeneralClipIndex(
        Playable playable,
        double currentTimelineTime,
        int inputCount)
    {
        if (_clips == null)
            return -1;

        int maxCount = Mathf.Min(inputCount, _clips.Length);

        int bestIndex = -1;
        double bestStart = double.MaxValue;

        for (int i = 0; i < maxCount; i++)
        {
            TimelineClip clip = _clips[i];

            if (clip == null)
                continue;

            if (clip.start < currentTimelineTime)
                continue;

            CameraProfileBehaviour behaviour = GetBehaviourFromInput(
                playable,
                i
            );

            if (behaviour == null)
                continue;

            if (behaviour.profile is not GeneralProfileSO)
                continue;

            if (clip.start < bestStart)
            {
                bestStart = clip.start;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private CameraProfileBehaviour GetBehaviourFromInput(
        Playable playable,
        int inputIndex)
    {
        if (inputIndex < 0 || inputIndex >= playable.GetInputCount())
            return null;

        ScriptPlayable<CameraProfileBehaviour> inputPlayable =
            (ScriptPlayable<CameraProfileBehaviour>)playable.GetInput(inputIndex);

        return inputPlayable.GetBehaviour();
    }

    private CinemachineCamera GetCameraForProfile(
        CameraSystemMaster master,
        CameraProfileKind kind,
        int dominantInputIndex,
        bool isHardCutFrame,
        out bool usedPreparedCamera)
    {
        usedPreparedCamera = false;

        if (master == null)
            return null;

        switch (kind)
        {
            case CameraProfileKind.General:
                {
                    if (isHardCutFrame)
                    {
                        if (_preparedGeneralInputIndex == dominantInputIndex)
                        {
                            _currentGeneralUseB = _preparedGeneralUseB;
                            usedPreparedCamera = true;
                            _preparedGeneralInputIndex = -1;
                        }
                        else if (_hasUsedGeneralCamera && master.generalCameraB != null)
                        {
                            _currentGeneralUseB = !_currentGeneralUseB;
                        }
                        else
                        {
                            _currentGeneralUseB = false;
                        }
                    }

                    return master.GetGeneralCamera(_currentGeneralUseB);
                }

            case CameraProfileKind.Tracking:
                return master.trackingCamera;

            case CameraProfileKind.Dolly:
                return master.dollyCamera;

            default:
                return null;
        }
    }

    private void ApplyProfileToCamera(
        CinemachineCamera camera,
        CameraProfileSO profile,
        Transform finalTarget,
        SplineContainer finalSpline,
        float t,
        bool forceZeroDamping)
    {
        if (camera == null || profile == null)
            return;

        if (profile is GeneralProfileSO generalProfile)
        {
            ApplyGeneralProfile(
                camera,
                generalProfile,
                finalTarget,
                t,
                forceZeroDamping
            );
        }
        else if (profile is TrackingProfileSO trackingProfile)
        {
            ApplyTrackingProfile(
                camera,
                trackingProfile,
                finalTarget,
                t
            );
        }
        else if (profile is DollyProfileSO dollyProfile)
        {
            ApplyDollyProfile(
                camera,
                dollyProfile,
                finalTarget,
                finalSpline,
                t
            );
        }
    }

    private void ApplyGeneralProfile(
        CinemachineCamera camera,
        GeneralProfileSO profile,
        Transform finalTarget,
        float t,
        bool forceZeroDamping)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            profile.fovCurve.Evaluate(t),
            10f,
            120f
        );

        CinemachinePositionComposer positionComposer =
            camera.GetComponent<CinemachinePositionComposer>();

        if (positionComposer != null)
        {
            positionComposer.CameraDistance =
                profile.posDistanceCurve.Evaluate(t);

            positionComposer.Composition.ScreenPosition = new Vector2(
                profile.posScreenXCurve.Evaluate(t),
                profile.posScreenYCurve.Evaluate(t)
            );

            positionComposer.TargetOffset = new Vector3(
                profile.posTargetOffsetXCurve.Evaluate(t),
                profile.posTargetOffsetYCurve.Evaluate(t),
                profile.posTargetOffsetZCurve.Evaluate(t)
            );

            positionComposer.Damping = forceZeroDamping
                ? Vector3.zero
                : new Vector3(
                    profile.posDampingX,
                    profile.posDampingY,
                    profile.posDampingZ
                );
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = new Vector2(
                profile.rotScreenXCurve.Evaluate(t),
                profile.rotScreenYCurve.Evaluate(t)
            );

            rotationComposer.TargetOffset = new Vector3(
                profile.rotTargetOffsetXCurve.Evaluate(t),
                profile.rotTargetOffsetYCurve.Evaluate(t),
                profile.rotTargetOffsetZCurve.Evaluate(t)
            );

            rotationComposer.Damping = forceZeroDamping
                ? Vector2.zero
                : new Vector2(
                    profile.rotDampingX,
                    profile.rotDampingY
                );
        }
    }

    private void ApplyTrackingProfile(
        CinemachineCamera camera,
        TrackingProfileSO profile,
        Transform finalTarget,
        float t)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            profile.fovCurve.Evaluate(t),
            10f,
            120f
        );

        CinemachineFollow follow =
            camera.GetComponent<CinemachineFollow>();

        if (follow != null)
        {
            follow.FollowOffset = new Vector3(
                profile.followOffsetXCurve.Evaluate(t),
                profile.followOffsetYCurve.Evaluate(t),
                profile.followOffsetZCurve.Evaluate(t)
            );

            var trackerSettings = follow.TrackerSettings;

            trackerSettings.PositionDamping = new Vector3(
                profile.dampingX,
                profile.dampingY,
                profile.dampingZ
            );

            follow.TrackerSettings = trackerSettings;
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = new Vector2(
                profile.rotScreenXCurve.Evaluate(t),
                profile.rotScreenYCurve.Evaluate(t)
            );

            rotationComposer.TargetOffset = new Vector3(
                profile.rotTargetOffsetXCurve.Evaluate(t),
                profile.rotTargetOffsetYCurve.Evaluate(t),
                profile.rotTargetOffsetZCurve.Evaluate(t)
            );

            rotationComposer.Damping = new Vector2(
                profile.rotDampingX,
                profile.rotDampingY
            );
        }
    }

    private void ApplyDollyProfile(
        CinemachineCamera camera,
        DollyProfileSO profile,
        Transform finalTarget,
        SplineContainer finalSpline,
        float t)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            profile.fovCurve.Evaluate(t),
            10f,
            120f
        );

        CinemachineSplineDolly dolly =
            camera.GetComponent<CinemachineSplineDolly>();

        if (dolly != null)
        {
            if (finalSpline != null && dolly.Spline != finalSpline)
            {
                dolly.Spline = finalSpline;
            }

            dolly.PositionUnits = profile.positionUnits;

            var settings = dolly.SplineSettings;
            settings.Position = profile.splinePositionCurve.Evaluate(t);
            dolly.SplineSettings = settings;
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = new Vector2(
                profile.rotScreenXCurve.Evaluate(t),
                profile.rotScreenYCurve.Evaluate(t)
            );

            rotationComposer.TargetOffset = new Vector3(
                profile.rotTargetOffsetXCurve.Evaluate(t),
                profile.rotTargetOffsetYCurve.Evaluate(t),
                profile.rotTargetOffsetZCurve.Evaluate(t)
            );

            rotationComposer.Damping = new Vector2(
                profile.rotDampingX,
                profile.rotDampingY
            );
        }
    }

    private bool IsWithinGeneralZeroDampingWindow()
    {
        if (!Application.isPlaying)
            return false;

        if (_generalZeroDampingUntilFrame < 0)
            return false;

        return Time.frameCount <= _generalZeroDampingUntilFrame;
    }

    private void SnapInactiveCameraToPreparedState(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        camera.PreviousStateIsValid = false;

        if (Application.isPlaying)
        {
            camera.CancelDamping(true);
        }
        else
        {
            camera.InternalUpdateCameraState(Vector3.up, 0.016f);
        }
    }

    private void PrepareCameraImmediately(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        camera.PreviousStateIsValid = false;

        if (!Application.isPlaying)
        {
            camera.InternalUpdateCameraState(Vector3.up, 0.016f);
        }
    }

    private static CameraProfileKind GetProfileKind(CameraProfileSO profile)
    {
        if (profile is GeneralProfileSO)
            return CameraProfileKind.General;

        if (profile is TrackingProfileSO)
            return CameraProfileKind.Tracking;

        if (profile is DollyProfileSO)
            return CameraProfileKind.Dolly;

        return CameraProfileKind.None;
    }

    private static void SetFollowAndLookAt(CinemachineCamera camera, Transform target)
    {
        if (camera == null)
            return;

        if (camera.Follow != target)
        {
            camera.Follow = target;
        }

        if (camera.LookAt != target)
        {
            camera.LookAt = target;
        }
    }

    private void ClearState()
    {
        _lastProfile = null;
        _lastTarget = null;
        _lastSpline = null;
        _lastDominantInputIndex = -1;
        _lastKind = CameraProfileKind.None;

        _currentGeneralUseB = false;
        _hasUsedGeneralCamera = false;
        _hasEverAppliedCamera = false;

        _preparedGeneralInputIndex = -1;
        _preparedGeneralUseB = false;
        _generalZeroDampingUntilFrame = -1;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        ClearState();
    }
}