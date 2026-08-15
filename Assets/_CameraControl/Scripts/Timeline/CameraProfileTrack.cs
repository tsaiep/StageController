using System.Linq;
using System.Collections.Generic;
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

    private struct CameraProfileInput
    {
        public int InputIndex;
        public float Weight;
        public float NormalizedTime;
        public double LocalTime;
        public CameraProfileKind Kind;
        public CameraProfileSO Profile;
        public CameraProfileBehaviour Behaviour;
        public Transform Target;
        public SplineContainer Spline;
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
    private bool _lastWasBlending;

    private Transform _blendTargetProxy;

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

        int inputCount = playable.GetInputCount();
        List<CameraProfileInput> activeInputs =
            new List<CameraProfileInput>(inputCount);

        CameraProfileInput dominantInput = default;
        bool hasDominantInput = false;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);

            if (inputWeight <= 0f)
                continue;

            ScriptPlayable<CameraProfileBehaviour> inputPlayable =
                (ScriptPlayable<CameraProfileBehaviour>)playable.GetInput(i);

            CameraProfileBehaviour behaviour = inputPlayable.GetBehaviour();

            if (behaviour == null || behaviour.profile == null)
                continue;

            double duration = inputPlayable.GetDuration();
            double currentTime = inputPlayable.GetTime();

            CameraProfileInput input = new CameraProfileInput
            {
                InputIndex = i,
                Weight = inputWeight,
                NormalizedTime = GetProfileNormalizedTime(
                    duration,
                    currentTime,
                    behaviour
                ),
                LocalTime = currentTime,
                Kind = GetProfileKind(behaviour.profile),
                Profile = behaviour.profile,
                Behaviour = behaviour,
                Target = behaviour.targetObject != null
                    ? behaviour.targetObject.transform
                    : null,
                Spline = behaviour.splineContainer
            };

            activeInputs.Add(input);

            if (!hasDominantInput || input.Weight > dominantInput.Weight)
            {
                dominantInput = input;
                hasDominantInput = true;
            }
        }

        CameraProfileSO currentProfile = hasDominantInput
            ? dominantInput.Profile
            : null;

        Transform finalTarget = hasDominantInput
            ? dominantInput.Target
            : null;

        SplineContainer finalSpline = hasDominantInput
            ? dominantInput.Spline
            : null;

        CameraProfileBehaviour dominantBehaviour = hasDominantInput
            ? dominantInput.Behaviour
            : null;

        int dominantInputIndex = hasDominantInput
            ? dominantInput.InputIndex
            : -1;

        float normalizedTime = hasDominantInput
            ? dominantInput.NormalizedTime
            : 0f;

        double dominantLocalTime = hasDominantInput
            ? dominantInput.LocalTime
            : 0.0;

        double currentTimelineTime = GetCurrentTimelineTime(
            playable,
            dominantInputIndex,
            dominantLocalTime
        );

        if (!hasDominantInput || currentProfile == null)
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

            _lastWasBlending = false;
            return;
        }

        bool shouldBlend = CanBlendActiveInputs(
            activeInputs,
            out CameraProfileKind blendedKind
        );

        CameraProfileKind currentKind = shouldBlend
            ? blendedKind
            : dominantInput.Kind;

        bool isClipChanged =
            dominantInputIndex != _lastDominantInputIndex ||
            currentProfile != _lastProfile ||
            finalTarget != _lastTarget ||
            finalSpline != _lastSpline ||
            currentKind != _lastKind;

        bool isOverlapping = activeInputs.Count > 1;
        bool isHardCutFrame = !isOverlapping && isClipChanged && !_lastWasBlending;

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

        if (shouldBlend)
        {
            ApplyBlendedProfileToCamera(
                activeCamera,
                activeInputs,
                currentKind,
                forceZeroDampingForActiveGeneral
            );
        }
        else
        {
            ApplyProfileToCamera(
                activeCamera,
                currentProfile,
                finalTarget,
                finalSpline,
                dominantBehaviour,
                normalizedTime,
                forceZeroDampingForActiveGeneral
            );
        }

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
        _lastWasBlending = shouldBlend;
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

    private static float GetProfileNormalizedTime(
        double duration,
        double currentTime,
        CameraProfileBehaviour behaviour)
    {
        float normalizedTime = duration > 0 && !double.IsInfinity(duration)
            ? Mathf.Clamp01((float)(currentTime / duration))
            : 0f;

        return IsReversePlayback(behaviour)
            ? 1f - normalizedTime
            : normalizedTime;
    }

    private static float GetStartSampleTime(CameraProfileBehaviour behaviour)
    {
        return IsReversePlayback(behaviour) ? 1f : 0f;
    }

    private static bool IsReversePlayback(CameraProfileBehaviour behaviour)
    {
        return behaviour != null && behaviour.reversePlayback;
    }

    private static Vector3 ApplyMirror(
        Vector3 value,
        CameraProfileBehaviour behaviour)
    {
        if (behaviour == null)
            return value;

        if (behaviour.mirrorX)
            value.x = -value.x;

        if (behaviour.mirrorY)
            value.y = -value.y;

        if (behaviour.mirrorZ)
            value.z = -value.z;

        return value;
    }

    private static Vector2 ApplyScreenMirror(
        Vector2 value,
        CameraProfileBehaviour behaviour)
    {
        if (behaviour == null)
            return value;

        if (behaviour.mirrorX)
            value.x = -value.x;

        if (behaviour.mirrorY)
            value.y = -value.y;

        return value;
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
            nextBehaviour,
            GetStartSampleTime(nextBehaviour),
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

    private static bool CanBlendActiveInputs(
        List<CameraProfileInput> inputs,
        out CameraProfileKind blendedKind)
    {
        blendedKind = CameraProfileKind.None;

        if (inputs == null || inputs.Count <= 1)
            return false;

        blendedKind = inputs[0].Kind;

        if (blendedKind != CameraProfileKind.General &&
            blendedKind != CameraProfileKind.Tracking)
        {
            return false;
        }

        for (int i = 1; i < inputs.Count; i++)
        {
            if (inputs[i].Kind != blendedKind)
                return false;
        }

        return true;
    }

    private void ApplyBlendedProfileToCamera(
        CinemachineCamera camera,
        List<CameraProfileInput> inputs,
        CameraProfileKind kind,
        bool forceZeroDamping)
    {
        if (camera == null || inputs == null || inputs.Count == 0)
            return;

        switch (kind)
        {
            case CameraProfileKind.General:
                ApplyBlendedGeneralProfile(camera, inputs, forceZeroDamping);
                break;

            case CameraProfileKind.Tracking:
                ApplyBlendedTrackingProfile(camera, inputs);
                break;
        }
    }

    private void ApplyBlendedGeneralProfile(
        CinemachineCamera camera,
        List<CameraProfileInput> inputs,
        bool forceZeroDamping)
    {
        float totalWeight = GetTotalWeight(inputs);

        if (totalWeight <= 0f)
            return;

        SetFollowAndLookAt(camera, GetBlendedTarget(inputs));

        float fov = 0f;
        float cameraDistance = 0f;
        Vector2 posScreenPosition = Vector2.zero;
        Vector3 posTargetOffset = Vector3.zero;
        Vector3 posDamping = Vector3.zero;
        Vector2 rotScreenPosition = Vector2.zero;
        Vector3 rotTargetOffset = Vector3.zero;
        Vector2 rotDamping = Vector2.zero;

        foreach (CameraProfileInput input in inputs)
        {
            GeneralProfileSO profile = input.Profile as GeneralProfileSO;

            if (profile == null)
                continue;

            float weight = input.Weight / totalWeight;
            float t = input.NormalizedTime;
            CameraProfileBehaviour behaviour = input.Behaviour;

            fov += GetBiasedFov(profile, behaviour, t) * weight;

            cameraDistance +=
                (profile.posDistanceCurve.Evaluate(t) + GetPosDistanceBias(behaviour)) *
                weight;

            posScreenPosition += ApplyScreenMirror(
                new Vector2(
                    profile.posScreenXCurve.Evaluate(t),
                    profile.posScreenYCurve.Evaluate(t)
                ),
                behaviour
            ) * weight;

            posTargetOffset += ApplyMirror(
                new Vector3(
                    profile.posTargetOffsetXCurve.Evaluate(t) + GetPosTargetOffsetXBias(behaviour),
                    profile.posTargetOffsetYCurve.Evaluate(t) + GetPosTargetOffsetYBias(behaviour),
                    profile.posTargetOffsetZCurve.Evaluate(t) + GetPosTargetOffsetZBias(behaviour)
                ),
                behaviour
            ) * weight;

            posDamping += new Vector3(
                profile.posDampingX,
                profile.posDampingY,
                profile.posDampingZ
            ) * weight;

            rotScreenPosition += ApplyScreenMirror(
                new Vector2(
                    profile.rotScreenXCurve.Evaluate(t),
                    profile.rotScreenYCurve.Evaluate(t)
                ),
                behaviour
            ) * weight;

            rotTargetOffset += ApplyMirror(
                new Vector3(
                    profile.rotTargetOffsetXCurve.Evaluate(t) + GetRotTargetOffsetXBias(behaviour),
                    profile.rotTargetOffsetYCurve.Evaluate(t) + GetRotTargetOffsetYBias(behaviour),
                    profile.rotTargetOffsetZCurve.Evaluate(t) + GetRotTargetOffsetZBias(behaviour)
                ),
                behaviour
            ) * weight;

            rotDamping += new Vector2(
                profile.rotDampingX,
                profile.rotDampingY
            ) * weight;
        }

        camera.Lens.FieldOfView = Mathf.Clamp(fov, 10f, 120f);

        CinemachinePositionComposer positionComposer =
            camera.GetComponent<CinemachinePositionComposer>();

        if (positionComposer != null)
        {
            positionComposer.CameraDistance = cameraDistance;
            positionComposer.Composition.ScreenPosition = posScreenPosition;
            positionComposer.TargetOffset = posTargetOffset;
            positionComposer.Damping = forceZeroDamping
                ? Vector3.zero
                : posDamping;
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = rotScreenPosition;
            rotationComposer.TargetOffset = rotTargetOffset;
            rotationComposer.Damping = forceZeroDamping
                ? Vector2.zero
                : rotDamping;
        }
    }

    private void ApplyBlendedTrackingProfile(
        CinemachineCamera camera,
        List<CameraProfileInput> inputs)
    {
        float totalWeight = GetTotalWeight(inputs);

        if (totalWeight <= 0f)
            return;

        SetFollowAndLookAt(camera, GetBlendedTarget(inputs));

        float fov = 0f;
        Vector3 followOffset = Vector3.zero;
        Vector3 positionDamping = Vector3.zero;
        Vector2 rotScreenPosition = Vector2.zero;
        Vector3 rotTargetOffset = Vector3.zero;
        Vector2 rotDamping = Vector2.zero;

        foreach (CameraProfileInput input in inputs)
        {
            TrackingProfileSO profile = input.Profile as TrackingProfileSO;

            if (profile == null)
                continue;

            float weight = input.Weight / totalWeight;
            float t = input.NormalizedTime;
            CameraProfileBehaviour behaviour = input.Behaviour;

            fov += GetBiasedFov(profile, behaviour, t) * weight;

            followOffset += ApplyMirror(
                new Vector3(
                    profile.followOffsetXCurve.Evaluate(t) + GetFollowOffsetXBias(behaviour),
                    profile.followOffsetYCurve.Evaluate(t) + GetFollowOffsetYBias(behaviour),
                    profile.followOffsetZCurve.Evaluate(t) + GetFollowOffsetZBias(behaviour)
                ),
                behaviour
            ) * weight;

            positionDamping += new Vector3(
                profile.dampingX,
                profile.dampingY,
                profile.dampingZ
            ) * weight;

            rotScreenPosition += ApplyScreenMirror(
                new Vector2(
                    profile.rotScreenXCurve.Evaluate(t),
                    profile.rotScreenYCurve.Evaluate(t)
                ),
                behaviour
            ) * weight;

            rotTargetOffset += ApplyMirror(
                new Vector3(
                    profile.rotTargetOffsetXCurve.Evaluate(t) + GetRotTargetOffsetXBias(behaviour),
                    profile.rotTargetOffsetYCurve.Evaluate(t) + GetRotTargetOffsetYBias(behaviour),
                    profile.rotTargetOffsetZCurve.Evaluate(t) + GetRotTargetOffsetZBias(behaviour)
                ),
                behaviour
            ) * weight;

            rotDamping += new Vector2(
                profile.rotDampingX,
                profile.rotDampingY
            ) * weight;
        }

        camera.Lens.FieldOfView = Mathf.Clamp(fov, 10f, 120f);

        CinemachineFollow follow = camera.GetComponent<CinemachineFollow>();

        if (follow != null)
        {
            follow.FollowOffset = followOffset;

            var trackerSettings = follow.TrackerSettings;
            trackerSettings.PositionDamping = positionDamping;
            follow.TrackerSettings = trackerSettings;
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = rotScreenPosition;
            rotationComposer.TargetOffset = rotTargetOffset;
            rotationComposer.Damping = rotDamping;
        }
    }

    private static float GetTotalWeight(List<CameraProfileInput> inputs)
    {
        float totalWeight = 0f;

        foreach (CameraProfileInput input in inputs)
        {
            totalWeight += input.Weight;
        }

        return totalWeight;
    }

    private Transform GetBlendedTarget(List<CameraProfileInput> inputs)
    {
        if (inputs == null || inputs.Count == 0)
            return null;

        float targetWeight = 0f;
        Vector3 position = Vector3.zero;

        foreach (CameraProfileInput input in inputs)
        {
            if (input.Target == null)
                continue;

            targetWeight += input.Weight;
            position += input.Target.position * input.Weight;
        }

        if (targetWeight <= 0f)
            return null;

        if (inputs.Count == 1)
            return inputs[0].Target;

        Transform proxy = EnsureBlendTargetProxy();
        proxy.position = position / targetWeight;
        proxy.rotation = GetBlendedTargetRotation(inputs, targetWeight);

        return proxy;
    }

    private Transform EnsureBlendTargetProxy()
    {
        if (_blendTargetProxy != null)
            return _blendTargetProxy;

        GameObject proxyObject = new GameObject("Camera Profile Blend Target Proxy");
        proxyObject.hideFlags = HideFlags.HideAndDontSave;
        _blendTargetProxy = proxyObject.transform;

        return _blendTargetProxy;
    }

    private static Quaternion GetBlendedTargetRotation(
        List<CameraProfileInput> inputs,
        float targetWeight)
    {
        Vector3 forward = Vector3.zero;
        Vector3 up = Vector3.zero;

        foreach (CameraProfileInput input in inputs)
        {
            if (input.Target == null)
                continue;

            float normalizedWeight = input.Weight / targetWeight;
            forward += input.Target.forward * normalizedWeight;
            up += input.Target.up * normalizedWeight;
        }

        if (forward.sqrMagnitude <= 0.0001f || up.sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        Vector3 normalizedForward = forward.normalized;
        Vector3 normalizedUp = up.normalized;

        if (Vector3.Cross(normalizedForward, normalizedUp).sqrMagnitude <= 0.0001f)
        {
            normalizedUp = Vector3.up;

            if (Vector3.Cross(normalizedForward, normalizedUp).sqrMagnitude <= 0.0001f)
            {
                normalizedUp = Vector3.right;
            }
        }

        return Quaternion.LookRotation(normalizedForward, normalizedUp);
    }

    private void ApplyProfileToCamera(
        CinemachineCamera camera,
        CameraProfileSO profile,
        Transform finalTarget,
        SplineContainer finalSpline,
        CameraProfileBehaviour behaviour,
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
                behaviour,
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
                behaviour,
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
                behaviour,
                t
            );
        }
    }

    private void ApplyGeneralProfile(
        CinemachineCamera camera,
        GeneralProfileSO profile,
        Transform finalTarget,
        CameraProfileBehaviour behaviour,
        float t,
        bool forceZeroDamping)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            GetBiasedFov(profile, behaviour, t),
            10f,
            120f
        );

        CinemachinePositionComposer positionComposer =
            camera.GetComponent<CinemachinePositionComposer>();

        if (positionComposer != null)
        {
            positionComposer.CameraDistance =
                profile.posDistanceCurve.Evaluate(t) + GetPosDistanceBias(behaviour);

            positionComposer.Composition.ScreenPosition = ApplyScreenMirror(
                new Vector2(
                    profile.posScreenXCurve.Evaluate(t),
                    profile.posScreenYCurve.Evaluate(t)
                ),
                behaviour
            );

            positionComposer.TargetOffset = ApplyMirror(
                new Vector3(
                    profile.posTargetOffsetXCurve.Evaluate(t) + GetPosTargetOffsetXBias(behaviour),
                    profile.posTargetOffsetYCurve.Evaluate(t) + GetPosTargetOffsetYBias(behaviour),
                    profile.posTargetOffsetZCurve.Evaluate(t) + GetPosTargetOffsetZBias(behaviour)
                ),
                behaviour
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
            rotationComposer.Composition.ScreenPosition = ApplyScreenMirror(
                new Vector2(
                    profile.rotScreenXCurve.Evaluate(t),
                    profile.rotScreenYCurve.Evaluate(t)
                ),
                behaviour
            );

            rotationComposer.TargetOffset = ApplyMirror(
                new Vector3(
                    profile.rotTargetOffsetXCurve.Evaluate(t) + GetRotTargetOffsetXBias(behaviour),
                    profile.rotTargetOffsetYCurve.Evaluate(t) + GetRotTargetOffsetYBias(behaviour),
                    profile.rotTargetOffsetZCurve.Evaluate(t) + GetRotTargetOffsetZBias(behaviour)
                ),
                behaviour
            );

            rotationComposer.Damping = forceZeroDamping
                ? Vector2.zero
                : new Vector2(
                    profile.rotDampingX,
                    profile.rotDampingY
                );
        }
    }

    private static float GetPosDistanceBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.posDistanceBias : 0f;
    }

    private static float GetBiasedFov(
        CameraProfileSO profile,
        CameraProfileBehaviour behaviour,
        float t)
    {
        if (profile == null)
            return 60f;

        return profile.fovCurve.Evaluate(t) + GetFovBias(behaviour);
    }

    private static float GetFovBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.fovBias : 0f;
    }

    private static float GetPosTargetOffsetXBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.posTargetOffsetXBias : 0f;
    }

    private static float GetPosTargetOffsetYBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.posTargetOffsetYBias : 0f;
    }

    private static float GetPosTargetOffsetZBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.posTargetOffsetZBias : 0f;
    }

    private static float GetRotTargetOffsetXBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.rotTargetOffsetXBias : 0f;
    }

    private static float GetRotTargetOffsetYBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.rotTargetOffsetYBias : 0f;
    }

    private static float GetRotTargetOffsetZBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.rotTargetOffsetZBias : 0f;
    }

    private static float GetFollowOffsetXBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.followOffsetXBias : 0f;
    }

    private static float GetFollowOffsetYBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.followOffsetYBias : 0f;
    }

    private static float GetFollowOffsetZBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.followOffsetZBias : 0f;
    }

    private static float GetSplinePositionBias(CameraProfileBehaviour behaviour)
    {
        return behaviour != null ? behaviour.splinePositionBias : 0f;
    }

    private void ApplyTrackingProfile(
        CinemachineCamera camera,
        TrackingProfileSO profile,
        Transform finalTarget,
        CameraProfileBehaviour behaviour,
        float t)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            GetBiasedFov(profile, behaviour, t),
            10f,
            120f
        );

        CinemachineFollow follow =
            camera.GetComponent<CinemachineFollow>();

        if (follow != null)
        {
            follow.FollowOffset = ApplyMirror(
                new Vector3(
                    profile.followOffsetXCurve.Evaluate(t) + GetFollowOffsetXBias(behaviour),
                    profile.followOffsetYCurve.Evaluate(t) + GetFollowOffsetYBias(behaviour),
                    profile.followOffsetZCurve.Evaluate(t) + GetFollowOffsetZBias(behaviour)
                ),
                behaviour
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
            rotationComposer.Composition.ScreenPosition = ApplyScreenMirror(
                new Vector2(
                    profile.rotScreenXCurve.Evaluate(t),
                    profile.rotScreenYCurve.Evaluate(t)
                ),
                behaviour
            );

            rotationComposer.TargetOffset = ApplyMirror(
                new Vector3(
                    profile.rotTargetOffsetXCurve.Evaluate(t) + GetRotTargetOffsetXBias(behaviour),
                    profile.rotTargetOffsetYCurve.Evaluate(t) + GetRotTargetOffsetYBias(behaviour),
                    profile.rotTargetOffsetZCurve.Evaluate(t) + GetRotTargetOffsetZBias(behaviour)
                ),
                behaviour
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
        CameraProfileBehaviour behaviour,
        float t)
    {
        if (camera == null)
            return;

        SetFollowAndLookAt(camera, finalTarget);

        camera.Lens.FieldOfView = Mathf.Clamp(
            GetBiasedFov(profile, behaviour, t),
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
            settings.Position = GetBiasedSplinePosition(profile, behaviour, t);
            dolly.SplineSettings = settings;
        }

        CinemachineRotationComposer rotationComposer =
            camera.GetComponent<CinemachineRotationComposer>();

        if (rotationComposer != null)
        {
            rotationComposer.Composition.ScreenPosition = ApplyScreenMirror(
                new Vector2(
                    profile.rotScreenXCurve.Evaluate(t),
                    profile.rotScreenYCurve.Evaluate(t)
                ),
                behaviour
            );

            rotationComposer.TargetOffset = ApplyMirror(
                new Vector3(
                    profile.rotTargetOffsetXCurve.Evaluate(t) + GetRotTargetOffsetXBias(behaviour),
                    profile.rotTargetOffsetYCurve.Evaluate(t) + GetRotTargetOffsetYBias(behaviour),
                    profile.rotTargetOffsetZCurve.Evaluate(t) + GetRotTargetOffsetZBias(behaviour)
                ),
                behaviour
            );

            rotationComposer.Damping = new Vector2(
                profile.rotDampingX,
                profile.rotDampingY
            );
        }
    }

    private static float GetBiasedSplinePosition(
        DollyProfileSO profile,
        CameraProfileBehaviour behaviour,
        float t)
    {
        if (profile == null)
            return 0f;

        float position = profile.splinePositionCurve.Evaluate(t) +
            GetSplinePositionBias(behaviour);

        return profile.positionUnits == PathIndexUnit.Normalized
            ? Mathf.Clamp01(position)
            : position;
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
        _lastWasBlending = false;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        ClearState();
        DestroyBlendTargetProxy();
    }

    private void DestroyBlendTargetProxy()
    {
        if (_blendTargetProxy == null)
            return;

        GameObject proxyObject = _blendTargetProxy.gameObject;
        _blendTargetProxy = null;

        if (Application.isPlaying)
        {
            Object.Destroy(proxyObject);
        }
        else
        {
            Object.DestroyImmediate(proxyObject);
        }
    }
}
