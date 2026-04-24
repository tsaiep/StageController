using UnityEngine;

[CreateAssetMenu(fileName = "新燈光片段模板", menuName = "燈光控制/燈光片段模板")]
public class UnifiedStageTemplate : ScriptableObject
{
    [Header("燈具物理設定")]
    [Tooltip("光束角度")]
    [Range(1f, 179f)] public float beamAngle = 5f;
    [Tooltip("開啟散射模式")] public bool enableScatterMode = false;

    [Header("燈光感應設定")]
    [Tooltip("燈光漸變")] public Gradient lightGradient;
    [Tooltip("總體亮度倍率")] public float intensityMultiplier = 1f;
    [Tooltip("最低亮度")] public float minBrightness = 0.1f;
    [Tooltip("靈敏度")] public float sensitivity = 1.5f;
    [Tooltip("平滑度")] public float smoothness = 8f;

    [Header("旋轉動作設定")]
    [Tooltip("旋轉模式")] public UnifiedStageController.RotationMode rotationMode;
    [Tooltip("旋轉速度")] public float rotationSpeed = 2f;
    [Tooltip("旋轉幅度")] public float rotationRange = 45f;
    [Tooltip("靜止角度偏移")] public Vector2 staticAngleOffset;
    [Tooltip("週期停頓時間")] public float cyclePauseTime = 0f;
    [Tooltip("動畫起點偏移(秒)，對循環動畫的相位起點產生時間偏移")] public float animationOffset = 0f;

    [Header("分組延遲")]
    [Tooltip("分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）")]
    public AnimationCurve groupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("分組延遲係數（秒），group 延遲 = curve(t) × factor × groupCount")]
    public float groupDelayFactor = 0f;

    [Header("組內逐顆延遲")]
    [Tooltip("組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）")]
    public AnimationCurve lightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("組內延遲係數（秒），light 延遲 = curve(t) × factor × groupSize")]
    public float lightDelayFactor = 0f;

    [Header("靜止模式顏色選項")]
    [Tooltip("靜止模式顏色動畫完成後的行為：Clamp — 停在漸層末端 / Loop — 循環回起點")]
    public UnifiedStageController.ColorFinishMode staticColorFinishMode = UnifiedStageController.ColorFinishMode.Clamp;
}