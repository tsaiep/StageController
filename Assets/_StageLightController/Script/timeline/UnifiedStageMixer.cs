using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

public class UnifiedStageMixer : PlayableBehaviour
{
    private double _lastRootTime = -1;
    private readonly List<ActiveClipInfo> _clipInfos = new List<ActiveClipInfo>(4);

    // ── FreezeFrame 狀態 ──
    private bool _lastFreezeFrameActive = false;
    private float _frozenInten, _frozenBeamAngle, _frozenLightRange, _frozenSoftness;
    private bool  _frozenScatter;
    private UnifiedStageController.StageLightMode _frozenLightMode;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UnifiedStageController controller = playerData as UnifiedStageController;
        if (controller == null) return;

        double rootTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        bool isTimeJump = _lastRootTime < 0 || Mathf.Abs((float)(rootTime - _lastRootTime)) > 0.1f;

        int inputCount = playable.GetInputCount();
        _clipInfos.Clear();

        // ── FreezeFrame Rising Edge 偵測（先掃一遍）──
        bool freezeFrameActiveNow = false;
        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0) continue;
            var bScan = ((ScriptPlayable<UnifiedStageBehaviour>)playable.GetInput(i)).GetBehaviour();
            if (bScan.clipMode == UnifiedStageController.RotationMode.FreezeFrame)
            {
                freezeFrameActiveNow = true;
                break;
            }
        }

        bool freezeJustActivated = freezeFrameActiveNow && !_lastFreezeFrameActive;

        // 若剛啟動 FreezeFrame，從前一個（非 FreezeFrame）Clip 快取全域參數
        if (freezeJustActivated)
        {
            for (int i = 0; i < inputCount; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0) continue;
                var bPrev = ((ScriptPlayable<UnifiedStageBehaviour>)playable.GetInput(i)).GetBehaviour();
                if (bPrev.clipMode != UnifiedStageController.RotationMode.FreezeFrame)
                {
                    _frozenInten     = bPrev.clipIntensity;
                    _frozenBeamAngle = bPrev.beamAngle;
                    _frozenLightRange = bPrev.lightRange;
                    _frozenSoftness  = bPrev.softness;
                    _frozenScatter   = bPrev.scatterMode;
                    _frozenLightMode = bPrev.lightMode;
                    break;
                }
            }
        }

        _lastFreezeFrameActive = freezeFrameActiveNow;

        // ── 預混合全域值 ──
        float mInten = 0, mBeamAngle = 0, mLightRange = 0, mSoftness = 0, totalMotionWeight = 0;
        float weightedEffectiveTime = 0f;
        bool activeScatter = false;
        var activeLightMode = UnifiedStageController.StageLightMode.VolumetricSpot;
        float maxWeight = -1f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0) continue;

            var inputPlayable = (ScriptPlayable<UnifiedStageBehaviour>)playable.GetInput(i);
            var b = inputPlayable.GetBehaviour();

            totalWeight += weight;

            bool isFreezeFrame = (b.clipMode == UnifiedStageController.RotationMode.FreezeFrame);

            float clipProgress = b.GetEffectiveLocalTime(inputPlayable, controller.enableMotion);
            weightedEffectiveTime += clipProgress * weight;

            // ── Random 模式兩段式混合 ──
            float randomStrength = 1f;
            if (b.clipMode == UnifiedStageController.RotationMode.Random)
            {
                randomStrength = weight <= 0.5f ? 0f : (weight - 0.5f) * 2f;
            }

            // ── 全域值累加（FreezeFrame 使用凍結值替代）──
            float useInten   = isFreezeFrame ? _frozenInten     : b.clipIntensity;
            float useBeam    = isFreezeFrame ? _frozenBeamAngle : b.beamAngle;
            float useRange   = isFreezeFrame ? _frozenLightRange : b.lightRange;
            float useSoftness = isFreezeFrame ? _frozenSoftness  : b.softness;
            bool  useScatter = isFreezeFrame ? _frozenScatter   : b.scatterMode;
            var useLightMode = isFreezeFrame ? _frozenLightMode : b.lightMode;

            mInten     += useInten * weight;
            mBeamAngle += useBeam  * weight;
            mLightRange += useRange * weight;
            mSoftness   += useSoftness * weight;
            totalMotionWeight += (b.enableMotion ? b.motionStrength : 0f) * weight;

            if (weight > maxWeight)
            {
                maxWeight = weight;
                activeScatter = useScatter;
                activeLightMode = useLightMode;
            }

            // Clip 在 Timeline 上的絕對起始時間（供 BeatTimeRef.TimelineGlobal 使用）
            float clipStartTime = (float)(rootTime - inputPlayable.GetTime());

            // ── 建立 ActiveClipInfo ──
            _clipInfos.Add(new ActiveClipInfo
            {
                weight              = weight,
                gradient            = b.clipGradient,
                mode                = b.clipMode,
                speed               = b.rotationSpeed,
                range               = b.rotationRange,
                pauseTime           = b.pauseTime,
                staticOffset        = b.staticOffset,
                effectiveTime       = clipProgress,
                normalizedClipTime  = b.GetNormalizedClipTime(inputPlayable),
                target              = b.clipTarget,
                scatterMode         = b.scatterMode,
                intensity           = b.clipIntensity,
                sensitivity         = b.sensitivity,
                smoothness          = b.smoothness,
                beamAngle           = b.beamAngle,
                lightRange          = b.lightRange,
                softness            = b.softness,
                lightMode           = b.lightMode,
                motionWeight        = b.enableMotion ? b.motionStrength : 0f,
                groupDelayCurve     = b.groupDelayCurve,
                groupDelayFactor    = b.groupDelayFactor,
                lightDelayCurve     = b.lightDelayCurve,
                lightDelayFactor    = b.lightDelayFactor,
                randomStrength      = randomStrength,
                animationOffset     = b.animationOffset,
                isFreezeFrame       = isFreezeFrame,
                freezeUseClipGradient = b.freezeUseClipGradient,
                clipDuration        = (float)inputPlayable.GetDuration(),
                colorSampleMode     = b.colorSampleMode,
                bpm                 = b.bpm,
                beatTimeRef         = b.beatTimeRef,
                beatPhaseOffset     = b.beatPhaseOffset,
                beatSnapColors      = b.beatSnapColors,
                globalColor         = b.globalColor,
                clipStartTime       = clipStartTime,
            });
        }

        if (totalWeight <= 0) return;
        float mixedLightRange = mLightRange / totalWeight;
        float mixedSoftness = mSoftness / totalWeight;

        // ── 頻譜採樣（供 AlongAudioSource 模式使用）──
        float[] simSpectrum = new float[256];
        if (controller.audioSource != null && controller.audioSource.clip != null)
        {
            AudioClip clip = controller.audioSource.clip;
            int samplePos = Mathf.FloorToInt((float)rootTime * clip.frequency);
            if (samplePos >= 0 && samplePos < clip.samples - 1024)
            {
                float[] buffer = new float[1024];
                clip.GetData(buffer, samplePos);
                for (int j = 0; j < 256; j++) simSpectrum[j] = Mathf.Abs(buffer[j * 4]);
            }
        }

        // ── 傳遞資料給 Controller ──
        controller.UpdateStage(
            _clipInfos, simSpectrum, isTimeJump,
            mInten, mBeamAngle, mixedLightRange, mixedSoftness, activeScatter, activeLightMode,
            totalMotionWeight, weightedEffectiveTime,
            freezeJustActivated, (float)rootTime
        );

        _lastRootTime = rootTime;
    }
}
