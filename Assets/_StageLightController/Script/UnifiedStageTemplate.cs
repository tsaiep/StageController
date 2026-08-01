using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[CreateAssetMenu(fileName = "NewStageTemplate", menuName = "Stage Control/Unified Stage Template")]
public class UnifiedStageTemplate : ScriptableObject
{
    [Header("Template Tags")]
    public List<UnifiedStageTemplateTagSO> tags = new List<UnifiedStageTemplateTagSO>();

    [Header("燈具物理設定")]
    [Tooltip("燈光模式")] public UnifiedStageController.StageLightMode lightMode = UnifiedStageController.StageLightMode.VolumetricSpot;
    [Tooltip("Light component 的 Range")] public float lightRange = 12f;
    [Tooltip("光束角度")]
    [Range(1f, 179f)] public float beamAngle = 5f;
    [Range(0f, 100f), Tooltip("光束邊緣柔和度。Volumetric 模式對應 Side Softness，Spot 模式對應 Inner Spot Angle")]
    public float softness = 0f;
    [Tooltip("開啟散射模式")] public bool enableScatterMode = false;

    [Header("燈光感應設定")]
    [Tooltip("燈光漸變")] public Gradient lightGradient;
    [Tooltip("Beam Length Gradient：光束頭尾方向的顏色漸層控制")] public Gradient beamLengthGradient = UnifiedStageGradientUtility.CreateDefaultBeamLengthGradient();
    [Tooltip("總體亮度倍率")] public float intensityMultiplier = 1f;

    [Header("顏色取樣設定")]
    [Tooltip("顏色取樣模式")] public UnifiedStageController.ColorSampleMode colorSampleMode = UnifiedStageController.ColorSampleMode.MotionCycle;
    [Tooltip("靈敏度（AlongAudioSource 模式：音量放大倍率）")] public float sensitivity = 1.5f;
    [Tooltip("平滑度（AlongAudioSource 模式：音量追蹤速度，越低越不易閃爍）")] public float smoothness = 8f;
    [Tooltip("節拍速度（BPM）")] public float bpm = 120f;
    [Tooltip("節拍時間基準")] public UnifiedStageController.BeatTimeReference beatTimeRef = UnifiedStageController.BeatTimeReference.ClipLocal;
    [Tooltip("節拍相位偏移（秒）")] public float beatPhaseOffset = 0f;
    [Tooltip("Beat Snap 顏色列表（依拍順序循環）")] public Color[] beatSnapColors = new Color[] { Color.white, Color.red };
    [Tooltip("Beat Snap 顏色切換平滑時間（秒）。0 表示瞬間切換")]
    public float beatSnapTransitionTime = 0f;
    [Tooltip("Beat Gradient: 分組時間延遲（秒）。Beat Snap: 每幾個分組排序階層讓顏色 index 偏移 1 格")]
    public float beatGroupDelayFactor = 0f;
    [Tooltip("Beat Gradient: 組內時間延遲（秒）。Beat Snap: 每幾個組內排序階層讓顏色 index 偏移 1 格")]
    public float beatLightDelayFactor = 0f;
    [Tooltip("跟隨節拍分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）")]
    public AnimationCurve beatGroupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("跟隨節拍組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）")]
    public AnimationCurve beatLightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Header("Audio Analyzer Brightness")]
    [Tooltip("Use UnifiedStageController.audioAnalyzer Beat values to scale this template's final brightness.")]
    public bool useAudioAnalyzerBrightness = false;
    [Tooltip("How many lights keep the same Beat index before moving to the next entry in Audio Beat Indices.")]
    public int audioBeatLightInterval = 1;
    [Tooltip("Beat indices assigned across lights in each group.")]
    public int[] audioBeatIndices = new int[] { 0 };
    [Tooltip("Brightness scale when Beat.CurrentValue is 0. 0 = black, 1 = original brightness.")]
    public float audioBrightnessOffset = 1f;
    [Tooltip("Additional brightness scale applied by Beat.CurrentValue.")]
    public float audioBrightnessMultiplier = 1f;
    [Tooltip("Extra smoothing for the audio brightness scale. 0 = no extra smoothing.")]
    public float audioBrightnessLerp = 0f;
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

    [Header("分組偏移")]
    [Tooltip("分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）")]
    public AnimationCurve groupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("分組延遲係數（秒），group 延遲 = curve(t) × factor × groupCount")]
    public float groupDelayFactor = 0f;
    [Tooltip("分組旋轉幅度曲線（以 groupIndex/(groupCount-1) 取樣）\n數値 × rotationRange = 該組的實際旋轉幅度，1 表示不改變")]
    public AnimationCurve groupRotationRangeCurve = AnimationCurve.Constant(0, 1, 1);

    [Header("組內偏移")]
    [Tooltip("組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）")]
    public AnimationCurve lightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("組內延遲係數（秒），light 延遲 = curve(t) × factor × groupSize")]
    public float lightDelayFactor = 0f;
    [Tooltip("組內旋轉幅度曲線（以 indexInGroup/(groupSize-1) 取樣）\n數値 × rotationRange = 該顆燈的實際旋轉幅度，1 表示不改變")]
    public AnimationCurve lightRotationRangeCurve = AnimationCurve.Constant(0, 1, 1);

    [Header("分散效果設定")]
    [Tooltip("分散角度（SpreadTilt.x 最大值，度）")] public float spreadAngle = 0f;
    [Range(0f, 360f), Tooltip("組內展開弧度（0~360，預設 360=均勻一圈頭尾不重疊）")] public float spreadArcRange = 360f;
    [Tooltip("分散角度曲線（Sample 旋轉動作循環，0~1 乘以分散角度 → SpreadTilt.x）")] public AnimationCurve spreadAngleCurve = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("依所屬燈組內 Index 取樣的 Spread Angle 倍率曲線（indexInGroup/(groupSize-1)，再乘到 SpreadTilt.x）")]
    public AnimationCurve spreadAngleCurveByIndex = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("展開旋轉曲線（Sample 旋轉動作循環，0~1 → 0~360° 附加到 SpreadPan.y）")] public AnimationCurve spreadPanCurve = AnimationCurve.Constant(0, 1, 0);
    [Header("Fanned Laser Settings")]
    [Range(0f, 180f), Tooltip("Maximum fanned laser spread angle in degrees.")]
    public float fannedAngle = 0f;
    [Tooltip("Samples one motion cycle. Multiplied by Fanned Angle and sent to _Range.")]
    public AnimationCurve fannedAngleCurve = AnimationCurve.Constant(0, 1, 1);
    [HideInInspector]
    [Tooltip("Disabled. Retained only for legacy serialized data.")]
    public AnimationCurve fannedRollCurve = AnimationCurve.Constant(0, 1, 0);

    void OnValidate()
    {
        if (beamLengthGradient == null)
            beamLengthGradient = UnifiedStageGradientUtility.CreateDefaultBeamLengthGradient();

        audioBeatLightInterval = Mathf.Max(1, audioBeatLightInterval);
        if (audioBeatIndices == null || audioBeatIndices.Length == 0)
            audioBeatIndices = new int[] { 0 };
        for (int i = 0; i < audioBeatIndices.Length; i++)
            audioBeatIndices[i] = Mathf.Max(0, audioBeatIndices[i]);
        audioBrightnessLerp = Mathf.Max(0f, audioBrightnessLerp);
        fannedAngle = Mathf.Clamp(fannedAngle, 0f, 180f);
        if (fannedAngleCurve == null)
            fannedAngleCurve = AnimationCurve.Constant(0, 1, 1);
    }
}
