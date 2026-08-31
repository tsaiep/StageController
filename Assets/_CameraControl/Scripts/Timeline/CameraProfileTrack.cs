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

    private enum CrossFadeHandoffPhase
    {
        None,
        RootSwitchCovered,
        HandoffRevealed
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

    private struct StoryboardCrossFadeContext
    {
        public bool Active;
        public int IncomingInputIndex;
        public CameraProfileKind IncomingKind;
        public CinemachineCamera BaseCamera;
        public CinemachineCamera RenderTextureCamera;
        public CinemachineCamera HandoffCamera;
        public bool HandoffGeneralUseB;
        public bool UsesMotionCut;
        public CrossFadeHandoffPhase HandoffPhase;
        public int HandoffPhaseFrame;
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
    private StoryboardCrossFadeContext _storyboardCrossFadeContext;
    private CameraSystemMaster _activeMaster;

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
            if (_activeMaster != null)
            {
                ClearDepthOfField(_activeMaster);
                ClearStoryboardCrossFade(_activeMaster);
            }

            ClearState();
            return;
        }

        if (_activeMaster != null && _activeMaster != master)
        {
            ClearDepthOfField(_activeMaster);
            ClearStoryboardCrossFade(_activeMaster);
        }

        _activeMaster = master;

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
            ClearDepthOfField(master);

            // Crossfade 被 gap / seek 中斷時，先停止離屏渲染與 Storyboard，
            // 再讓一般的 gap prewarm 接手，避免預熱改到仍在交接中的 camera。
            ClearStoryboardCrossFade(master);

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

        if (TryGetStoryboardCrossFadePair(
                activeInputs,
                out CameraProfileInput outgoingCrossFadeInput,
                out CameraProfileInput incomingCrossFadeInput,
                out bool useCrossFadeBlur,
                out bool useMotionCut))
        {
            ApplyCrossFadeDepthOfField(
                master,
                outgoingCrossFadeInput,
                incomingCrossFadeInput
            );

            float rawCrossFadeAlpha = GetRawCrossFadeAlpha(
                outgoingCrossFadeInput,
                incomingCrossFadeInput
            );

            float crossFadeAlphaTiming = useCrossFadeBlur
                ? Mathf.Clamp01(
                    incomingCrossFadeInput.Behaviour.crossFadeAlphaTiming)
                : 0f;

            float displayCrossFadeAlpha = useMotionCut
                ? CalculateMotionCutDisplayAlpha(rawCrossFadeAlpha)
                : useCrossFadeBlur
                    ? CalculateCrossFadeDisplayAlpha(
                        rawCrossFadeAlpha,
                        crossFadeAlphaTiming)
                    : rawCrossFadeAlpha;

            bool useTimedCrossFadeAlpha =
                useMotionCut ||
                (useCrossFadeBlur && crossFadeAlphaTiming > 0f);

            if (useCrossFadeBlur)
            {
                float outgoingBlurWeight = Mathf.Clamp01(
                    rawCrossFadeAlpha
                );
                float incomingBlurWeight = 1f - outgoingBlurWeight;
                float maxBlurIntensity = Mathf.Clamp(
                    incomingCrossFadeInput.Behaviour
                        .crossFadeBlurMaxIntensity,
                    0f,
                    CameraProfileAsset.MaxCrossFadeBlurIntensity
                );

                master.TrySetCrossFadeBlur(
                    outgoingBlurWeight * maxBlurIntensity,
                    outgoingBlurWeight,
                    incomingBlurWeight * maxBlurIntensity,
                    incomingBlurWeight
                );
            }
            else
            {
                master.ClearCrossFadeBlur();
            }

            bool appliedCrossFade = Application.isPlaying
                ? TryApplyStoryboardRenderTextureCrossFade(
                    master,
                    outgoingCrossFadeInput,
                    incomingCrossFadeInput,
                    rawCrossFadeAlpha,
                    displayCrossFadeAlpha,
                    useTimedCrossFadeAlpha,
                    useMotionCut,
                    info.deltaTime)
                : TryApplyStoryboardRenderTextureCrossFadePreview(
                    master,
                    outgoingCrossFadeInput,
                    incomingCrossFadeInput,
                    rawCrossFadeAlpha,
                    displayCrossFadeAlpha,
                    useTimedCrossFadeAlpha,
                    useMotionCut,
                    info.deltaTime);

            if (!appliedCrossFade)
            {
                if (useMotionCut)
                {
                    master.ClearAllMotionCutCameraEffects();
                }

                ApplyStoryboardCrossFadeHardCutFallback(
                    master,
                    outgoingCrossFadeInput
                );
            }

            return;
        }

        if (Application.isPlaying)
        {
            ApplyDepthOfField(master, dominantInput);
        }

        if (Application.isPlaying &&
            TryCompleteStoryboardCrossFadeHandoff(
            master,
            activeInputs,
            dominantInput,
            info.deltaTime))
        {
            return;
        }

        bool endedEditorCrossFadePreview =
            !Application.isPlaying && _storyboardCrossFadeContext.Active;

        ClearStoryboardCrossFade(master);

        if (shouldBlend)
        {
            ApplyBlendedDepthOfField(master, activeInputs);
        }
        else
        {
            ApplyDepthOfField(master, dominantInput);
        }

        // 有效 Clip 期間：不管目前是 General / Tracking / Dolly，
        // 都可以往後找下一個 General，並在接近時預熱 General A/B。
        // Storyboard crossfade 會在上方早退，避免 prewarm 改寫交接中的 camera。
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
            ClearDepthOfField(master);
            master.DisableAllCameras();
            ClearStoryboardCrossFade(master);
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
            if (endedEditorCrossFadePreview)
            {
                master.RefreshEditorCameraPreview(activeCamera);
            }
            else
            {
                activeCamera.InternalUpdateCameraState(Vector3.up, 0.016f);
            }
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
            TimelineClip clip = _clips[dominantInputIndex];

            // Timeline 將 Playable local time 定義為：
            // (timelineTime - clip.start) * timeScale + clipIn。
            // 反算時必須扣掉 clipIn，否則左側 trim 後會讓 General prewarm 提早觸發。
            return clip.start +
                   ((dominantLocalTime - clip.clipIn) / clip.timeScale);
        }

        return playable.GetTime();
    }

    private static float GetProfileNormalizedTime(
        double duration,
        double currentTime,
        CameraProfileBehaviour behaviour)
    {
        float normalizedTime;

        if (UseFixedPlaybackSpeed(behaviour))
        {
            normalizedTime = Mathf.Clamp01(
                (float)currentTime * GetFixedPlaybackSpeed(behaviour)
            );
        }
        else
        {
            normalizedTime = duration > 0 && !double.IsInfinity(duration)
                ? Mathf.Clamp01((float)(currentTime / duration))
                : 0f;
        }

        return IsReversePlayback(behaviour)
            ? 1f - normalizedTime
            : normalizedTime;
    }

    private static float GetClipStartSampleTime(
        TimelineClip clip,
        CameraProfileBehaviour behaviour)
    {
        float normalizedTime = 0f;

        if (clip != null && UseFixedPlaybackSpeed(behaviour))
        {
            normalizedTime = Mathf.Clamp01(
                (float)clip.clipIn * GetFixedPlaybackSpeed(behaviour)
            );
        }

        return IsReversePlayback(behaviour)
            ? 1f - normalizedTime
            : normalizedTime;
    }

    private static bool IsReversePlayback(CameraProfileBehaviour behaviour)
    {
        return behaviour != null && behaviour.reversePlayback;
    }

    private static bool UseFixedPlaybackSpeed(CameraProfileBehaviour behaviour)
    {
        return behaviour != null && behaviour.useFixedPlaybackSpeed;
    }

    private static float GetFixedPlaybackSpeed(CameraProfileBehaviour behaviour)
    {
        return behaviour != null
            ? Mathf.Max(0.001f, behaviour.fixedPlaybackSpeed)
            : 1f;
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
            GetClipStartSampleTime(nextClip, nextBehaviour),
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

    private bool TryApplyStoryboardRenderTextureCrossFadePreview(
        CameraSystemMaster master,
        CameraProfileInput outgoingInput,
        CameraProfileInput incomingInput,
        float rawAlpha,
        float displayAlpha,
        bool useTimedAlpha,
        bool useMotionCut,
        float deltaTime)
    {
#if UNITY_EDITOR
        if (!TryGetCrossFadeRenderCamera(
            master,
            incomingInput.Kind,
            out CinemachineCamera renderTextureCamera))
        {
            master.ReportCrossFadeSetupFailure(
                $"找不到 {incomingInput.Kind} 對應的離屏 transition camera。"
            );
            return false;
        }

        bool ignoredPreparedCamera;
        CinemachineCamera baseCamera = GetCameraForProfile(
            master,
            outgoingInput.Kind,
            outgoingInput.InputIndex,
            false,
            out ignoredPreparedCamera
        );

        if (baseCamera == null)
        {
            master.ReportCrossFadeSetupFailure(
                $"找不到 {outgoingInput.Kind} 對應的 outgoing camera。"
            );
            return false;
        }

        bool startsNewEditorCrossFade =
            !_storyboardCrossFadeContext.Active;

        if (_storyboardCrossFadeContext.Active &&
            (_storyboardCrossFadeContext.IncomingInputIndex !=
                incomingInput.InputIndex ||
             _storyboardCrossFadeContext.RenderTextureCamera !=
                renderTextureCamera ||
             _storyboardCrossFadeContext.BaseCamera != baseCamera))
        {
            ClearStoryboardCrossFade(master);
            startsNewEditorCrossFade = true;
        }

        ApplyProfileToCamera(
            baseCamera,
            outgoingInput.Profile,
            outgoingInput.Target,
            outgoingInput.Spline,
            outgoingInput.Behaviour,
            outgoingInput.NormalizedTime,
            false
        );

        ApplyProfileToCamera(
            renderTextureCamera,
            incomingInput.Profile,
            incomingInput.Target,
            incomingInput.Spline,
            incomingInput.Behaviour,
            incomingInput.NormalizedTime,
            false
        );

        ApplyOrClearMotionCut(
            master,
            baseCamera,
            renderTextureCamera,
            incomingInput.Behaviour,
            rawAlpha,
            useMotionCut
        );

        if (startsNewEditorCrossFade)
        {
            renderTextureCamera.PreviousStateIsValid = false;
        }

        bool appliedCrossFade = useTimedAlpha
            ? master.TrySetStoryboardCrossFadePreviewTimedAlpha(
                baseCamera,
                renderTextureCamera,
                rawAlpha,
                displayAlpha,
                deltaTime)
            : master.TrySetStoryboardCrossFadePreview(
                baseCamera,
                renderTextureCamera,
                displayAlpha,
                deltaTime);

        if (!appliedCrossFade)
        {
            return false;
        }

        _storyboardCrossFadeContext = new StoryboardCrossFadeContext
        {
            Active = true,
            IncomingInputIndex = incomingInput.InputIndex,
            IncomingKind = incomingInput.Kind,
            BaseCamera = baseCamera,
            RenderTextureCamera = renderTextureCamera,
            UsesMotionCut = useMotionCut
        };

        if (outgoingInput.Kind == CameraProfileKind.General ||
            incomingInput.Kind == CameraProfileKind.General)
        {
            _hasUsedGeneralCamera = true;
        }

        _hasEverAppliedCamera = true;
        _lastProfile = outgoingInput.Profile;
        _lastTarget = outgoingInput.Target;
        _lastSpline = outgoingInput.Spline;
        _lastDominantInputIndex = outgoingInput.InputIndex;
        _lastKind = outgoingInput.Kind;
        _lastWasBlending = true;

        return true;
#else
        return false;
#endif
    }

    private bool TryApplyStoryboardRenderTextureCrossFade(
        CameraSystemMaster master,
        CameraProfileInput outgoingInput,
        CameraProfileInput incomingInput,
        float rawAlpha,
        float displayAlpha,
        bool useTimedAlpha,
        bool useMotionCut,
        float deltaTime)
    {
        if (!TryGetCrossFadeRenderCamera(
            master,
            incomingInput.Kind,
            out CinemachineCamera renderTextureCamera))
        {
            master.ReportCrossFadeSetupFailure(
                $"找不到 {incomingInput.Kind} 對應的離屏 transition camera。"
            );
            return false;
        }

        bool ignoredPreparedCamera;
        CinemachineCamera baseCamera = GetCameraForProfile(
            master,
            outgoingInput.Kind,
            outgoingInput.InputIndex,
            false,
            out ignoredPreparedCamera
        );

        if (baseCamera == null)
        {
            master.ReportCrossFadeSetupFailure(
                $"找不到 {outgoingInput.Kind} 對應的 outgoing camera。"
            );
            return false;
        }

        CinemachineCamera handoffCamera;
        bool handoffGeneralUseB;

        bool canReuseHandoff =
            _storyboardCrossFadeContext.Active &&
            _storyboardCrossFadeContext.HandoffPhase ==
                CrossFadeHandoffPhase.None &&
            _storyboardCrossFadeContext.IncomingInputIndex ==
                incomingInput.InputIndex &&
            _storyboardCrossFadeContext.RenderTextureCamera ==
                renderTextureCamera &&
            _storyboardCrossFadeContext.BaseCamera == baseCamera;

        if (_storyboardCrossFadeContext.Active && !canReuseHandoff)
        {
            ClearStoryboardCrossFade(master);
        }

        if (canReuseHandoff)
        {
            handoffCamera = _storyboardCrossFadeContext.HandoffCamera;
            handoffGeneralUseB =
                _storyboardCrossFadeContext.HandoffGeneralUseB;
        }
        else
        {
            handoffCamera = GetCrossFadeHandoffCamera(
                master,
                incomingInput.Kind,
                baseCamera,
                out handoffGeneralUseB
            );
        }

        if (handoffCamera == null)
        {
            master.ReportCrossFadeSetupFailure(
                $"找不到 {incomingInput.Kind} 對應的主畫面 handoff camera。"
            );
            return false;
        }

        ApplyProfileToCamera(
            baseCamera,
            outgoingInput.Profile,
            outgoingInput.Target,
            outgoingInput.Spline,
            outgoingInput.Behaviour,
            outgoingInput.NormalizedTime,
            false
        );

        ApplyProfileToCamera(
            renderTextureCamera,
            incomingInput.Profile,
            incomingInput.Target,
            incomingInput.Spline,
            incomingInput.Behaviour,
            incomingInput.NormalizedTime,
            false
        );

        ApplyOrClearMotionCut(
            master,
            baseCamera,
            renderTextureCamera,
            incomingInput.Behaviour,
            rawAlpha,
            useMotionCut
        );

        master.SetOnlyThisCameraLive(baseCamera);

        bool appliedCrossFade = useTimedAlpha
            ? master.TrySetStoryboardCrossFadeTimedAlpha(
                baseCamera,
                renderTextureCamera,
                rawAlpha,
                displayAlpha,
                deltaTime)
            : master.TrySetStoryboardCrossFade(
                baseCamera,
                renderTextureCamera,
                displayAlpha,
                deltaTime);

        if (!appliedCrossFade)
        {
            return false;
        }

        _storyboardCrossFadeContext = new StoryboardCrossFadeContext
        {
            Active = true,
            IncomingInputIndex = incomingInput.InputIndex,
            IncomingKind = incomingInput.Kind,
            BaseCamera = baseCamera,
            RenderTextureCamera = renderTextureCamera,
            HandoffCamera = handoffCamera,
            HandoffGeneralUseB = handoffGeneralUseB,
            UsesMotionCut = useMotionCut
        };

        if (outgoingInput.Kind == CameraProfileKind.General ||
            incomingInput.Kind == CameraProfileKind.General)
        {
            _hasUsedGeneralCamera = true;
        }

        _hasEverAppliedCamera = true;
        _lastProfile = outgoingInput.Profile;
        _lastTarget = outgoingInput.Target;
        _lastSpline = outgoingInput.Spline;
        _lastDominantInputIndex = outgoingInput.InputIndex;
        _lastKind = outgoingInput.Kind;
        _lastWasBlending = true;

        return true;
    }

    private void ApplyStoryboardCrossFadeHardCutFallback(
        CameraSystemMaster master,
        CameraProfileInput outgoingInput)
    {
        ClearStoryboardCrossFade(master);
        ApplyDepthOfField(master, outgoingInput);

        bool ignoredPreparedCamera;
        CinemachineCamera outgoingCamera = GetCameraForProfile(
            master,
            outgoingInput.Kind,
            outgoingInput.InputIndex,
            false,
            out ignoredPreparedCamera
        );

        if (outgoingCamera == null)
        {
            ClearDepthOfField(master);
            master.DisableAllCameras();
            ClearState();
            return;
        }

        ApplyProfileToCamera(
            outgoingCamera,
            outgoingInput.Profile,
            outgoingInput.Target,
            outgoingInput.Spline,
            outgoingInput.Behaviour,
            outgoingInput.NormalizedTime,
            false
        );

        master.SetOnlyThisCameraLive(outgoingCamera);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            master.RefreshEditorCameraPreview(outgoingCamera);
        }
#endif

        if (outgoingInput.Kind == CameraProfileKind.General)
        {
            _hasUsedGeneralCamera = true;
        }

        _hasEverAppliedCamera = true;
        _lastProfile = outgoingInput.Profile;
        _lastTarget = outgoingInput.Target;
        _lastSpline = outgoingInput.Spline;
        _lastDominantInputIndex = outgoingInput.InputIndex;
        _lastKind = outgoingInput.Kind;
        _lastWasBlending = false;
    }

    private bool TryCompleteStoryboardCrossFadeHandoff(
        CameraSystemMaster master,
        List<CameraProfileInput> activeInputs,
        CameraProfileInput incomingInput,
        float deltaTime)
    {
        if (!_storyboardCrossFadeContext.Active ||
            activeInputs == null ||
            activeInputs.Count != 1 ||
            incomingInput.InputIndex !=
                _storyboardCrossFadeContext.IncomingInputIndex ||
            incomingInput.Kind != _storyboardCrossFadeContext.IncomingKind)
        {
            return false;
        }

        // Overlap 已結束。即使 handoff 需要數幀遮罩，畫面也必須回到清楚，
        // 並避免 seek 直接離開中點時殘留上一幀的 blur state。
        master.ClearCrossFadeBlur();

        CinemachineCamera renderTextureCamera =
            _storyboardCrossFadeContext.RenderTextureCamera;
        CinemachineCamera handoffCamera =
            _storyboardCrossFadeContext.HandoffCamera;
        if (renderTextureCamera == null || handoffCamera == null)
            return false;

        if (_storyboardCrossFadeContext.UsesMotionCut)
        {
            master.ClearMotionCutCameraEffects(
                _storyboardCrossFadeContext.BaseCamera
            );
            master.ClearMotionCutCameraEffects(renderTextureCamera);
            master.ClearMotionCutCameraEffects(handoffCamera);
        }

        // 這個物件在 phase None 還是 RT camera；升格後則是同一個主 camera。
        // 全程只驅動這一套 pipeline，避免兩台 camera 的 damping 狀態分岔。
        ApplyProfileToCamera(
            renderTextureCamera,
            incomingInput.Profile,
            incomingInput.Target,
            incomingInput.Spline,
            incomingInput.Behaviour,
            incomingInput.NormalizedTime,
            false
        );

        bool handoffGeneralUseB =
            _storyboardCrossFadeContext.HandoffGeneralUseB;

        switch (_storyboardCrossFadeContext.HandoffPhase)
        {
            case CrossFadeHandoffPhase.None:
                // 不再把 RT camera 的最終座標複製到另一台 camera。
                // 直接把已由 offscreen Brain 評估整段 Clip2 的同一台 vcam
                // 升格到主輸出，才能保留 Composer / Follow / Dolly 的內部狀態。
                if (!master.TryPromoteCrossFadeCamera(
                    renderTextureCamera,
                    handoffCamera,
                    out CinemachineCamera promotedCamera))
                {
                    return false;
                }

                _storyboardCrossFadeContext.HandoffCamera = promotedCamera;

                // RT 已凍結；先以不透明 Storyboard 遮住 root camera 角色交換。
                if (!master.TrySetStoryboardOverlayOverride(
                    promotedCamera,
                    1f,
                    deltaTime))
                {
                    return false;
                }

                _storyboardCrossFadeContext.HandoffPhase =
                    CrossFadeHandoffPhase.RootSwitchCovered;
                _storyboardCrossFadeContext.HandoffPhaseFrame =
                    Time.frameCount;
                return true;

            case CrossFadeHandoffPhase.RootSwitchCovered:
                if (Time.frameCount <=
                    _storyboardCrossFadeContext.HandoffPhaseFrame)
                {
                    return master.TrySetStoryboardOverlayOverride(
                        handoffCamera,
                        1f,
                        deltaTime
                    );
                }

                // Camera override 的 A 已經是同步完成的 handoff camera。
                // 權重直接切到 0，不執行第二段 dissolve。
                if (!master.TrySetStoryboardOverlayOverride(
                    handoffCamera,
                    0f,
                    deltaTime))
                {
                    return false;
                }

                _storyboardCrossFadeContext.HandoffPhase =
                    CrossFadeHandoffPhase.HandoffRevealed;
                _storyboardCrossFadeContext.HandoffPhaseFrame =
                    Time.frameCount;
                return true;

            case CrossFadeHandoffPhase.HandoffRevealed:
                if (Time.frameCount <=
                    _storyboardCrossFadeContext.HandoffPhaseFrame)
                {
                    return master.TrySetStoryboardOverlayOverride(
                        handoffCamera,
                        0f,
                        deltaTime
                    );
                }

                ClearStoryboardCrossFade(master);
                CommitStoryboardCrossFadeHandoff(
                    incomingInput,
                    handoffGeneralUseB
                );
                return true;

            default:
                return false;
        }
    }

    private void CommitStoryboardCrossFadeHandoff(
        CameraProfileInput incomingInput,
        bool handoffGeneralUseB)
    {
        if (incomingInput.Kind == CameraProfileKind.General)
        {
            _currentGeneralUseB = handoffGeneralUseB;
            _hasUsedGeneralCamera = true;
            _preparedGeneralInputIndex = -1;
        }

        _hasEverAppliedCamera = true;
        _lastProfile = incomingInput.Profile;
        _lastTarget = incomingInput.Target;
        _lastSpline = incomingInput.Spline;
        _lastDominantInputIndex = incomingInput.InputIndex;
        _lastKind = incomingInput.Kind;
        _lastWasBlending = false;
        _generalZeroDampingUntilFrame = -1;
    }

    private CinemachineCamera GetCrossFadeHandoffCamera(
        CameraSystemMaster master,
        CameraProfileKind incomingKind,
        CinemachineCamera baseCamera,
        out bool generalUseB)
    {
        generalUseB = false;

        if (master == null)
            return null;

        switch (incomingKind)
        {
            case CameraProfileKind.General:
                if (baseCamera == master.generalCamera &&
                    master.generalCameraB != null)
                {
                    generalUseB = true;
                    return master.generalCameraB;
                }

                if (baseCamera == master.generalCameraB &&
                    master.generalCamera != null)
                {
                    generalUseB = false;
                    return master.generalCamera;
                }

                CinemachineCamera preferredGeneral =
                    master.GetGeneralCamera(_currentGeneralUseB);

                if (preferredGeneral != null && preferredGeneral != baseCamera)
                {
                    generalUseB = preferredGeneral == master.generalCameraB;
                    return preferredGeneral;
                }

                if (master.generalCamera != null)
                {
                    generalUseB = false;
                    return master.generalCamera;
                }

                generalUseB = master.generalCameraB != null;
                return master.generalCameraB;

            case CameraProfileKind.Tracking:
                return master.trackingCamera;

            case CameraProfileKind.Dolly:
                return master.dollyCamera;

            default:
                return null;
        }
    }

    private bool TryGetStoryboardCrossFadePair(
        List<CameraProfileInput> activeInputs,
        out CameraProfileInput outgoingInput,
        out CameraProfileInput incomingInput,
        out bool useCrossFadeBlur,
        out bool useMotionCut)
    {
        outgoingInput = default;
        incomingInput = default;
        useCrossFadeBlur = false;
        useMotionCut = false;

        if (activeInputs == null || activeInputs.Count != 2)
            return false;

        CameraProfileInput first = activeInputs[0];
        CameraProfileInput second = activeInputs[1];

        double firstStart = GetClipStart(first.InputIndex);
        double secondStart = GetClipStart(second.InputIndex);

        if (secondStart > firstStart ||
            (Mathf.Approximately((float)secondStart, (float)firstStart) &&
                second.InputIndex > first.InputIndex))
        {
            outgoingInput = first;
            incomingInput = second;
        }
        else
        {
            outgoingInput = second;
            incomingInput = first;
        }

        if (incomingInput.Behaviour == null)
            return false;

        CameraProfileBlendMode blendMode =
            incomingInput.Behaviour.blendMode;

        useCrossFadeBlur = blendMode ==
            CameraProfileBlendMode.CrossFadeBlur;

        if (blendMode == CameraProfileBlendMode.MotionCut)
        {
            useMotionCut = true;
            return true;
        }

        return blendMode ==
                CameraProfileBlendMode.CrossFade ||
            useCrossFadeBlur;
    }

    private static float GetRawCrossFadeAlpha(
        CameraProfileInput outgoingInput,
        CameraProfileInput incomingInput)
    {
        float totalWeight = outgoingInput.Weight + incomingInput.Weight;

        return totalWeight > 0f
            ? Mathf.Clamp01(incomingInput.Weight / totalWeight)
            : Mathf.Clamp01(incomingInput.Weight);
    }

    internal static float CalculateCrossFadeDisplayAlpha(
        float rawAlpha,
        float alphaTiming)
    {
        float alpha = Mathf.Clamp01(rawAlpha);
        float timing = Mathf.Clamp01(alphaTiming);

        if (timing >= 1f)
            return alpha < 0.5f ? 0f : 1f;

        float hold = timing * 0.5f;
        return Mathf.InverseLerp(hold, 1f - hold, alpha);
    }

    internal static float CalculateMotionCutDisplayAlpha(float rawAlpha)
    {
        return Mathf.Clamp01(rawAlpha) < 0.5f ? 0f : 1f;
    }

    private static void ApplyOrClearMotionCut(
        CameraSystemMaster master,
        CinemachineCamera outgoingCamera,
        CinemachineCamera incomingCamera,
        CameraProfileBehaviour incomingBehaviour,
        float rawAlpha,
        bool useMotionCut)
    {
        if (master == null)
            return;

        if (!useMotionCut || incomingBehaviour == null)
        {
            master.ClearMotionCutCameraEffects(outgoingCamera);
            master.ClearMotionCutCameraEffects(incomingCamera);
            return;
        }

        float alpha = Mathf.Clamp01(rawAlpha);
        AnimationCurve curve = incomingBehaviour.motionCutCurve;

        float outgoingProgress = EvaluateMotionCutCurve(
            curve,
            Mathf.Clamp01(alpha * 2f)
        );

        float incomingRemaining = EvaluateMotionCutCurve(
            curve,
            1f - Mathf.Clamp01((alpha - 0.5f) * 2f)
        );

        Vector3 axisVector = GetDirectionalAxisVector(
            incomingBehaviour.motionCutAxis
        );

        float inStrength = incomingBehaviour.motionCutInStrength;

        if (!incomingBehaviour.reverseMotionCutInStrength)
        {
            inStrength = -inStrength;
        }

        master.TrySetDirectionalCameraOffset(
            outgoingCamera,
            axisVector *
                incomingBehaviour.motionCutOutStrength *
                outgoingProgress
        );

        master.TrySetDirectionalCameraOffset(
            incomingCamera,
            axisVector * inStrength * incomingRemaining
        );

        AnimationCurve rollCurve = incomingBehaviour.motionCutRollCurve;

        float outgoingRollProgress = EvaluateMotionCutCurve(
            rollCurve,
            Mathf.Clamp01(alpha * 2f)
        );

        float incomingRollRemaining = EvaluateMotionCutCurve(
            rollCurve,
            1f - Mathf.Clamp01((alpha - 0.5f) * 2f)
        );

        float halfRollAngle = incomingBehaviour.motionCutRollAngle * 0.5f;

        master.TrySetMotionCutCameraRoll(
            outgoingCamera,
            halfRollAngle * outgoingRollProgress
        );

        master.TrySetMotionCutCameraRoll(
            incomingCamera,
            -halfRollAngle * incomingRollRemaining
        );
    }

    private static float EvaluateMotionCutCurve(
        AnimationCurve curve,
        float normalizedTime)
    {
        float time = Mathf.Clamp01(normalizedTime);
        return curve != null ? curve.Evaluate(time) : time;
    }

    private static Vector3 GetDirectionalAxisVector(
        CameraProfileDirectionalAxis axis)
    {
        switch (axis)
        {
            case CameraProfileDirectionalAxis.Vertical:
                return Vector3.up;

            case CameraProfileDirectionalAxis.Depth:
                return Vector3.forward;

            default:
                return Vector3.right;
        }
    }

    private double GetClipStart(int inputIndex)
    {
        if (_clips == null ||
            inputIndex < 0 ||
            inputIndex >= _clips.Length ||
            _clips[inputIndex] == null)
        {
            return 0.0;
        }

        return _clips[inputIndex].start;
    }

    private static bool TryGetCrossFadeRenderCamera(
        CameraSystemMaster master,
        CameraProfileKind kind,
        out CinemachineCamera camera)
    {
        camera = null;

        if (master == null)
            return false;

        switch (kind)
        {
            case CameraProfileKind.General:
                camera = master.GetCrossFadeGeneralCamera();
                break;

            case CameraProfileKind.Tracking:
                camera = master.GetCrossFadeTrackingCamera();
                break;

            case CameraProfileKind.Dolly:
                camera = master.GetCrossFadeDollyCamera();
                break;
        }

        return camera != null;
    }

    private void ClearStoryboardCrossFade(CameraSystemMaster master)
    {
        if (master != null &&
            _storyboardCrossFadeContext.Active &&
            _storyboardCrossFadeContext.UsesMotionCut)
        {
            master.ClearMotionCutCameraEffects(
                _storyboardCrossFadeContext.BaseCamera
            );
            master.ClearMotionCutCameraEffects(
                _storyboardCrossFadeContext.RenderTextureCamera
            );
            master.ClearMotionCutCameraEffects(
                _storyboardCrossFadeContext.HandoffCamera
            );
        }

        if (master != null)
        {
            master.ClearStoryboardCrossFade();
        }

        _storyboardCrossFadeContext = default;
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

        ApplyBlendedNoise(camera, inputs);
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

    private static CameraDepthOfFieldSettings EvaluateDepthOfField(
        CameraProfileInput input)
    {
        CameraProfileBehaviour behaviour = input.Behaviour;

        if (behaviour == null || !behaviour.enableDepthOfField)
            return default;

        float normalizedFocus = behaviour.normalizedFocusDistanceCurve != null
            ? behaviour.normalizedFocusDistanceCurve.Evaluate(
                input.NormalizedTime)
            : 0.2f;
        float minimum = Mathf.Max(0.01f, behaviour.focusDistanceMin);
        float maximum = Mathf.Max(
            minimum + 0.01f,
            behaviour.focusDistanceMax
        );
        float focusDistance = Mathf.Lerp(
            minimum,
            maximum,
            Mathf.Clamp01(normalizedFocus)
        );

        return new CameraDepthOfFieldSettings
        {
            Enabled = true,
            FocusDistance = focusDistance,
            NearFocusRange = behaviour.depthOfFieldNearRange,
            FarFocusRange = behaviour.depthOfFieldFarRange,
            NearBlurRadius = behaviour.depthOfFieldMaxRadius,
            FarBlurRadius = behaviour.depthOfFieldMaxRadius,
            Intensity = 1f,
            DebugView = behaviour.depthOfFieldDebugView
        }.Sanitized();
    }

    private static void ApplyDepthOfField(
        CameraSystemMaster master,
        CameraProfileInput input)
    {
        Camera mainCamera = Camera.main;
        CameraDepthOfFieldSettings settings = EvaluateDepthOfField(input);

        CameraDepthOfFieldState.Set(mainCamera, settings);

        if (master != null)
            CameraDepthOfFieldState.Clear(master.crossFadeRenderCamera);
    }

    private static void ApplyBlendedDepthOfField(
        CameraSystemMaster master,
        List<CameraProfileInput> inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            ClearDepthOfField(master);
            return;
        }

        float totalWeight = GetTotalWeight(inputs);
        float enabledWeight = 0f;
        float focusDistance = 0f;
        float nearRange = 0f;
        float farRange = 0f;
        float nearRadius = 0f;
        float farRadius = 0f;
        float intensity = 0f;
        float debugViewWeight = -1f;
        CameraDepthOfFieldDebugView debugView =
            CameraDepthOfFieldDebugView.Final;

        foreach (CameraProfileInput input in inputs)
        {
            CameraDepthOfFieldSettings settings =
                EvaluateDepthOfField(input);

            if (!settings.IsActive)
                continue;

            float weight = Mathf.Max(0f, input.Weight);
            enabledWeight += weight;
            focusDistance += settings.FocusDistance * weight;
            nearRange += settings.NearFocusRange * weight;
            farRange += settings.FarFocusRange * weight;
            nearRadius += settings.NearBlurRadius * weight;
            farRadius += settings.FarBlurRadius * weight;
            intensity += settings.Intensity * weight;

            if (weight >= debugViewWeight)
            {
                debugViewWeight = weight;
                debugView = settings.DebugView;
            }
        }

        if (enabledWeight <= 0f || totalWeight <= 0f)
        {
            ClearDepthOfField(master);
            return;
        }

        CameraDepthOfFieldSettings blendedSettings =
            new CameraDepthOfFieldSettings
            {
                Enabled = true,
                FocusDistance = focusDistance / enabledWeight,
                NearFocusRange = nearRange / enabledWeight,
                FarFocusRange = farRange / enabledWeight,
                NearBlurRadius = nearRadius / enabledWeight,
                FarBlurRadius = farRadius / enabledWeight,
                Intensity = intensity / totalWeight,
                DebugView = debugView
            };

        CameraDepthOfFieldState.Set(Camera.main, blendedSettings);

        if (master != null)
            CameraDepthOfFieldState.Clear(master.crossFadeRenderCamera);
    }

    private static void ApplyCrossFadeDepthOfField(
        CameraSystemMaster master,
        CameraProfileInput outgoing,
        CameraProfileInput incoming)
    {
        CameraDepthOfFieldState.Set(
            Camera.main,
            EvaluateDepthOfField(outgoing)
        );

        if (master != null)
        {
            CameraDepthOfFieldState.Set(
                master.crossFadeRenderCamera,
                EvaluateDepthOfField(incoming)
            );
        }
    }

    private static void ClearDepthOfField(CameraSystemMaster master)
    {
        CameraDepthOfFieldState.Clear(Camera.main);

        if (master != null)
            CameraDepthOfFieldState.Clear(master.crossFadeRenderCamera);
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

        ApplyNoise(camera, behaviour);
    }

    private static void ApplyNoise(
        CinemachineCamera camera,
        CameraProfileBehaviour behaviour)
    {
        NoiseSettings noiseProfile = behaviour != null
            ? behaviour.noiseProfile
            : null;

        float amplitude = behaviour != null
            ? Mathf.Max(0f, behaviour.noiseAmplitude)
            : 0f;

        float frequency = behaviour != null
            ? Mathf.Max(0f, behaviour.noiseFrequency)
            : 0f;

        bool shouldEnable =
            behaviour != null &&
            behaviour.enableNoise &&
            noiseProfile != null &&
            amplitude > 0f;

        ApplyNoise(camera, noiseProfile, amplitude, frequency, shouldEnable);
    }

    private static void ApplyBlendedNoise(
        CinemachineCamera camera,
        List<CameraProfileInput> inputs)
    {
        if (camera == null || inputs == null || inputs.Count == 0)
            return;

        float totalWeight = GetTotalWeight(inputs);

        if (totalWeight <= 0f)
        {
            ApplyNoise(camera, null, 0f, 0f, false);
            return;
        }

        CameraProfileBehaviour selectedBehaviour = null;
        float selectedWeight = -1f;
        float amplitude = 0f;
        float frequency = 0f;
        float enabledNoiseWeight = 0f;

        foreach (CameraProfileInput input in inputs)
        {
            CameraProfileBehaviour behaviour = input.Behaviour;

            if (behaviour == null ||
                !behaviour.enableNoise ||
                behaviour.noiseProfile == null)
            {
                continue;
            }

            float weight = Mathf.Max(0f, input.Weight);
            amplitude += Mathf.Max(0f, behaviour.noiseAmplitude) * weight;
            frequency += Mathf.Max(0f, behaviour.noiseFrequency) * weight;
            enabledNoiseWeight += weight;

            if (weight > selectedWeight)
            {
                selectedBehaviour = behaviour;
                selectedWeight = weight;
            }
        }

        if (selectedBehaviour == null || enabledNoiseWeight <= 0f)
        {
            ApplyNoise(camera, null, 0f, 0f, false);
            return;
        }

        amplitude /= totalWeight;
        frequency /= enabledNoiseWeight;

        ApplyNoise(
            camera,
            selectedBehaviour.noiseProfile,
            amplitude,
            frequency,
            amplitude > 0f
        );
    }

    private static void ApplyNoise(
        CinemachineCamera camera,
        NoiseSettings noiseProfile,
        float amplitude,
        float frequency,
        bool shouldEnable)
    {
        if (camera == null)
            return;

        CinemachineBasicMultiChannelPerlin noise =
            camera.GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (noise == null)
            return;

        noise.enabled = shouldEnable;

        if (!shouldEnable)
        {
            noise.AmplitudeGain = 0f;
            return;
        }

        noise.NoiseProfile = noiseProfile;
        noise.AmplitudeGain = Mathf.Max(0f, amplitude);
        noise.FrequencyGain = Mathf.Max(0f, frequency);
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
        _storyboardCrossFadeContext = default;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (_activeMaster != null)
        {
            ClearDepthOfField(_activeMaster);
            ClearStoryboardCrossFade(_activeMaster);
        }

        ClearState();
        _activeMaster = null;
        DestroyBlendTargetProxy();
    }

    public override void OnGraphStop(Playable playable)
    {
        if (_activeMaster != null)
            ClearDepthOfField(_activeMaster);
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
