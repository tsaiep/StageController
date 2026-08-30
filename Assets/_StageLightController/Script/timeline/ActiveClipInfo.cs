using UnityEngine;

/// <summary>
/// 每個活躍 Clip 的完整資料，由 Mixer 建立後傳給 Controller 做 per-unit 計算
/// </summary>
[System.Serializable]
public struct ActiveClipInfo
{
    public float weight;
    public Gradient gradient;
    public Gradient beamLengthGradient;
    public UnifiedStageController.RotationMode mode;
    public float speed;
    public float range;
    public float pauseTime;
    public Vector2 staticOffset;    // x=pan偏移, y=tilt偏移
    public float effectiveTime;       // clip 內的有效時間（秒）
    public float normalizedClipTime;  // clip 內的正規化時間 (0~1)
    public Transform target;
    public bool scatterMode;
    public Texture2D scatterTexture;
    public float intensity;
    public float sensitivity;
    public float smoothness;
    public float beamAngle;
    public float lightRange;
    public float softness;
    public UnifiedStageController.StageLightMode lightMode;
    public float motionWeight;

    // ── 分組偏移 ──
    public AnimationCurve groupDelayCurve;        // 分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）
    public float groupDelayFactor;                // 分組延遲係數（秒）
    public AnimationCurve groupRotationRangeCurve; // 分組旋轉幅度曲線（以 groupIndex/(groupCount-1) 取樣，乘以 rotationRange）

    // ── 組內偏移 ──
    public AnimationCurve lightDelayCurve;        // 組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）
    public float lightDelayFactor;                // 組內延遲係數（秒）
    public AnimationCurve lightRotationRangeCurve; // 組內旋轉幅度曲線（以 indexInGroup/(groupSize-1) 取樣，乘以 rotationRange）

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
    public float beatSnapTransitionTime;       // BeatSnap 顏色切換平滑時間（秒）
    public float beatGroupDelayFactor;         // BeatGradient: 秒；BeatSnap: 每 N 個 group rank 偏移 1 格
    public float beatLightDelayFactor;         // BeatGradient: 秒；BeatSnap: 每 N 個 group 內 rank 偏移 1 格
    public AnimationCurve beatGroupDelayCurve; // 跟隨節拍分組延遲曲線
    public AnimationCurve beatLightDelayCurve; // 跟隨節拍組內延遲曲線
    public Color globalColor;                  // 全域顏色乘算（HDR，乘在所有模式輸出上）
    public float clipStartTime;                // Clip 在 Timeline 上的絕對起始時間（秒）
                                               // = rootTime - effectiveTime，供 BeatTimeRef.TimelineGlobal 使用

    // ── Spread 分散效果 ──
    public float spreadAngle;       // 分散角度（SpreadTilt.x 最大值，度）
    public float spreadArcRange;    // 組內展開弧度（0~360，360=均勻一圈頭尾不重疊）
    public AnimationCurve spreadAngleCurve; // 0~1 乘以 spreadAngle → SpreadTilt.x
    public AnimationCurve spreadAngleCurveByIndex; // indexInGroup/(groupSize-1) 乘到 SpreadTilt.x
    public AnimationCurve spreadPanCurve;   // 0~1 → 0~360°，附加到 SpreadPan.y
    public float fannedAngle;
    public float fannedRoll;
    public AnimationCurve fannedAngleCurve;
    public bool useAudioAnalyzerBrightness;
    public int audioBeatLightInterval;
    public int[] audioBeatIndices;
    public float audioBrightnessOffset;
    public float audioBrightnessMultiplier;
    public float audioBrightnessLerp;
}
