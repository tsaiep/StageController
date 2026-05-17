using UnityEngine;
using UnityEngine.Playables;

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

    [Header("顏色取樣設定")]
    [Tooltip("顏色取樣模式")] public UnifiedStageController.ColorSampleMode colorSampleMode = UnifiedStageController.ColorSampleMode.MotionCycle;
    [Tooltip("靈敏度（AlongAudioSource 模式：音量放大倍率）")] public float sensitivity = 1.5f;
    [Tooltip("平滑度（AlongAudioSource 模式：音量追蹤速度，越低越不易閃爍）")] public float smoothness = 8f;
    [Tooltip("節拍速度（BPM）")] public float bpm = 120f;
    [Tooltip("節拍時間基準")] public UnifiedStageController.BeatTimeReference beatTimeRef = UnifiedStageController.BeatTimeReference.ClipLocal;
    [Tooltip("節拍相位偏移（秒）")] public float beatPhaseOffset = 0f;
    [Tooltip("Beat Snap 顏色列表（依拍順序循環）")] public Color[] beatSnapColors = new Color[] { Color.white, Color.red };
    [ColorUsage(true, true), Tooltip("全域顏色乘算（HDR）")] public Color globalColor = Color.white;
    [Tooltip("凍結前幀——啟用後改為以 Clip 自身 Light Gradient 取色（Clip 頭尾對應 0-1），並與前後 Clip 正常 Blending；停用則凍結前一個 Clip 的瞬間顏色")] public bool freezeUseClipGradient = false;

    [Header("旋轉動作設定")]
    [Tooltip("旋轉模式")] public UnifiedStageController.RotationMode rotationMode;
    [Tooltip("旋轉速度")] public float rotationSpeed = 2f;
    [Tooltip("旋轉幅度")] public float rotationRange = 45f;
    [Tooltip("静止角度偏移 (x=pan基底, y=tilt基底)")] public Vector2 staticAngleOffset;
    [Tooltip("週期停頓時間")] public float cyclePauseTime = 0f;
    [Tooltip("動畫起點偏移(秒)，對循環動畫的相位起點產生時間偏移")] public float animationOffset = 0f;
    [Tooltip("追蹤目標")] public ExposedReference<Transform> trackingTarget;

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
}
