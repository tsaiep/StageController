using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

public class UnifiedStageMixer : PlayableBehaviour
{
    private double _lastRootTime = -1;
    private readonly List<ActiveClipInfo> _clipInfos = new List<ActiveClipInfo>(4);

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        UnifiedStageController controller = playerData as UnifiedStageController;
        if (controller == null) return;

        double rootTime = playable.GetGraph().GetRootPlayable(0).GetTime();
        bool isTimeJump = _lastRootTime < 0 || Mathf.Abs((float)(rootTime - _lastRootTime)) > 0.1f;

        int inputCount = playable.GetInputCount();
        _clipInfos.Clear();

        // 預混合的全域值（不受 per-unit delay 影響的參數）
        float mInten = 0, mBase = 0, mSense = 0, mSmooth = 0;
        float mBeamAngle = 0, totalMotionWeight = 0;
        float weightedEffectiveTime = 0f;
        bool activeScatter = false;
        float maxWeight = -1f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0) continue;

            var inputPlayable = (ScriptPlayable<UnifiedStageBehaviour>)playable.GetInput(i);
            var b = inputPlayable.GetBehaviour();

            totalWeight += weight;

            float clipProgress = b.GetEffectiveLocalTime(inputPlayable, controller.enableMotion);
            weightedEffectiveTime += clipProgress * weight;

            // --- Random 模式兩段式混合 ---
            float randomStrength = 1f;
            if (b.clipMode == UnifiedStageController.RotationMode.Random)
            {
                if (weight <= 0.5f)
                    randomStrength = 0f;          // 前半段：凍結在初始位置
                else
                    randomStrength = (weight - 0.5f) * 2f; // 後半段：0→1 漸增噪波
            }

            // 建立此 Clip 的完整資訊
            _clipInfos.Add(new ActiveClipInfo
            {
                weight = weight,
                gradient = b.clipGradient,
                mode = b.clipMode,
                speed = b.rotationSpeed,
                range = b.rotationRange,
                pauseTime = b.pauseTime,
                staticOffset = b.staticOffset,
                effectiveTime = clipProgress,
                normalizedClipTime = b.GetNormalizedClipTime(inputPlayable),
                target = b.clipTarget,
                scatterMode = b.scatterMode,
                intensity = b.clipIntensity,
                baseLevel = b.minBrightness,
                sensitivity = b.sensitivity,
                smoothness = b.smoothness,
                beamAngle = b.beamAngle,
                motionWeight = b.enableMotion ? b.motionStrength : 0f,
                delayCurve = b.delayCurve,
                delayFactor = b.delayFactor,
                randomStrength = randomStrength
            });

            // 全域值加權累加
            mInten += b.clipIntensity * weight;
            mBase += b.minBrightness * weight;
            mSense += b.sensitivity * weight;
            mSmooth += b.smoothness * weight;
            mBeamAngle += b.beamAngle * weight;
            totalMotionWeight += (b.enableMotion ? b.motionStrength : 0f) * weight;

            if (weight > maxWeight) { maxWeight = weight; activeScatter = b.scatterMode; }
        }

        if (totalWeight <= 0) return;

        // 頻譜採樣
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

        // 傳遞 per-clip 資料列表 + 全域值給 Controller
        controller.UpdateStage(
            _clipInfos, simSpectrum, isTimeJump,
            mInten, mBase, mSense, mSmooth, mBeamAngle, activeScatter,
            totalMotionWeight, weightedEffectiveTime
        );

        _lastRootTime = rootTime;
    }
}