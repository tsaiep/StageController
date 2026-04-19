using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class UnifiedStageBehaviour : PlayableBehaviour
{
    public Gradient clipGradient;
    [Tooltip("總體亮度倍率")] public float clipIntensity;
    [Tooltip("最低亮度")] public float minBrightness;
    [Tooltip("靈敏度")] public float sensitivity;
    [Tooltip("平滑度")] public float smoothness;
    [Tooltip("光束角度")] public float beamAngle;
    [Tooltip("散射模式")] public bool scatterMode;
    public UnifiedStageController.RotationMode clipMode;
    [Tooltip("旋轉速度")] public float rotationSpeed;
    [Tooltip("旋轉幅度")] public float rotationRange;
    [Tooltip("靜止偏移")] public Vector2 staticOffset;
    [Tooltip("停頓時間")] public float pauseTime;
    public Transform clipTarget;

    [Header("播放控制")]
    [Tooltip("啟用動作")] public bool enableMotion = true;
    [Tooltip("動作強度")] public float motionStrength = 1.0f;

    [Header("分組延遲")]
    [Tooltip("分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）")]
    public AnimationCurve groupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("分組延遲係數（秒）")]
    public float groupDelayFactor = 0f;

    [Header("組內逐顆延遲")]
    [Tooltip("組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）")]
    public AnimationCurve lightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("組內延遲係數（秒）")]
    public float lightDelayFactor = 0f;

    [Header("動畫起點偏移")]
    [Tooltip("動畫循環起點的時間偏移（秒）")] public float animationOffset = 0f;

    [Header("凍結前幀設定")]
    [Tooltip("啟用後改為以 Clip 自身的 Light Gradient 取色（Clip 頭尾對應 0-1 取樣），並與前後 Clip 正常 Blending；停用則凍結前一個 Clip 的瞬間顏色")]
    public bool freezeUseClipGradient = false;

    /// <summary>
    /// 取得 Clip 內的正規化時間 (0~1)
    /// </summary>
    public float GetNormalizedClipTime(Playable playable)
    {
        double time = playable.GetTime();
        double duration = playable.GetDuration();
        if (duration <= 0) return 0f;
        return Mathf.Clamp01((float)(time / duration));
    }

    /// <summary>
    /// 計算該 Clip 內部的有效運動進度 (絕對秒數)
    /// </summary>
    public float GetEffectiveLocalTime(Playable playable, bool globalActionEnable)
    {
        float localTime = (float)playable.GetTime();
        if (!globalActionEnable || !enableMotion) return 0f;
        return localTime;
    }

    /// <summary>
    /// 根據運動模式計算一個循環的週期（秒）
    /// 回傳 0 表示無固定循環（Static / Target）
    /// </summary>
    public static float GetMotionCyclePeriod(UnifiedStageController.RotationMode mode, float speed)
    {
        if (speed <= 0.0001f) return 0f;
        switch (mode)
        {
            case UnifiedStageController.RotationMode.Scan:
            case UnifiedStageController.RotationMode.VerticalSwing:
            case UnifiedStageController.RotationMode.Cross:
                return (2f * Mathf.PI) / speed; // sin 週期

            case UnifiedStageController.RotationMode.Circle:
                return 360f / (speed * 20f); // 一圈 = 360°/(speed*20°/s)

            case UnifiedStageController.RotationMode.Random:
                return 1f / speed; // 用速度決定漸層週期

            case UnifiedStageController.RotationMode.Static:
            case UnifiedStageController.RotationMode.Target:
            case UnifiedStageController.RotationMode.FreezeFrame:
            default:
                return 0f; // 無循環，用 clip normalizedTime 做 fallback
        }
    }
}