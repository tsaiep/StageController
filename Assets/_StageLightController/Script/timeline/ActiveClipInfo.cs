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
    public Vector2 staticOffset;    // x=pan偏移, y=tilt偏移
    public float effectiveTime;       // clip 內的有效時間（秒）
    public float normalizedClipTime;  // clip 內的正規化時間 (0~1)
    public Transform target;
    public bool scatterMode;
    public float intensity;
    public float sensitivity;
    public float smoothness;
    public float beamAngle;
    public float lightRange;
    public float softness;
    public UnifiedStageController.StageLightMode lightMode;
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

    public float clipDuration;        // Clip 長度（秒），供 Static 延遲偏移計算用

    // ── 顏色取樣模式 ──
    public UnifiedStageController.ColorSampleMode colorSampleMode;
    public float bpm;                          // 節拍速度（Beat 系列模式使用）
    public UnifiedStageController.BeatTimeReference beatTimeRef; // 節拍時間基準
    public float beatPhaseOffset;              // 全域節拍相位偏移（秒）
    public Color[] beatSnapColors;             // BeatSnap 顏色列表（依拍順序循環）
    public float beatGroupDelayFactor;         // BeatGradient: 秒；BeatSnap: 每 N 個 group rank 偏移 1 格
    public float beatLightDelayFactor;         // BeatGradient: 秒；BeatSnap: 每 N 個 group 內 rank 偏移 1 格
    public AnimationCurve beatGroupDelayCurve; // 跟隨節拍分組延遲曲線
    public AnimationCurve beatLightDelayCurve; // 跟隨節拍組內延遲曲線
    public Color globalColor;                  // 全域顏色乘算（HDR，乘在所有模式輸出上）
    public float clipStartTime;                // Clip 在 Timeline 上的絕對起始時間（秒）
                                               // = rootTime - effectiveTime，供 BeatTimeRef.TimelineGlobal 使用
}
