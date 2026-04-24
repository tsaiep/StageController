using UnityEngine;

/// <summary>
/// 每個活躍 Clip 的完整資料，由 Mixer 建立後傳給 Controller 做 per-unit 計算
/// </summary>
[System.Serializable]
public struct ActiveClipInfo
{
    public float weight;
    public Gradient gradient;
    public UnifiedStageController.RotationMode mode;
    public float speed;
    public float range;
    public float pauseTime;
    public Vector2 staticOffset;
    public float effectiveTime;       // clip 內的有效時間（秒）
    public float normalizedClipTime;  // clip 內的正規化時間 (0~1)
    public Transform target;
    public bool scatterMode;
    public float intensity;
    public float baseLevel;
    public float sensitivity;
    public float smoothness;
    public float beamAngle;
    public float motionWeight;

    // ── 分組延遲 ──
    public AnimationCurve groupDelayCurve; // 分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）
    public float groupDelayFactor;         // 分組延遲係數（秒）

    // ── 組內逐顆延遲 ──
    public AnimationCurve lightDelayCurve; // 組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）
    public float lightDelayFactor;         // 組內延遲係數（秒）

    public float randomStrength;      // 0~1，Random 模式兩段式混合強度
    public float animationOffset;     // 動畫起點時間偏移（秒）
    public bool isFreezeFrame;        // 是否為 FreezeFrame 凍結模式
    public bool freezeUseClipGradient;// FreezeFrame: 啟用時以 Clip 自身 Gradient 取色（頭尾對應0-1）

    // ── Static 模式顏色選項 ──
    public UnifiedStageController.ColorFinishMode staticColorFinishMode; // 動畫完成後的行為
    public float clipDuration;        // Clip 長度（秒），供 Static 延遲偏移計算用
}
