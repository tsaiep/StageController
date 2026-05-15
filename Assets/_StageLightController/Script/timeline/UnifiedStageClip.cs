using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

[System.Serializable]
[DisplayName("整合舞台方案片段")]
public class UnifiedStageClip : PlayableAsset, ITimelineClipAsset
{
    [Header("燈光感應設定")]
    [ColorUsage(true, true), Tooltip("全域顏色乘算（HDR）")] public Color globalColor = Color.white;
    [Tooltip("燈光漸變")] public Gradient lightGradient = new Gradient();
    [Tooltip("總體亮度倍率")] public float intensityMultiplier = 1.0f;

    [Header("顏色取樣設定")]
    [Tooltip("顏色取樣模式")] public UnifiedStageController.ColorSampleMode colorSampleMode = UnifiedStageController.ColorSampleMode.MotionCycle;

    [Tooltip("靈敏度（AlongAudioSource 模式：音量放大倍率）")] public float sensitivity = 1.5f;
    [Tooltip("平滑度（AlongAudioSource 模式：音量追蹤速度，越低越不易閃爍）")] public float smoothness = 8.0f;

    [Tooltip("節拍速度（BPM）")] public float bpm = 120f;
    [Tooltip("節拍時間基準")] public UnifiedStageController.BeatTimeReference beatTimeRef = UnifiedStageController.BeatTimeReference.ClipLocal;
    [Tooltip("節拍相位偏移（秒），Timeline Global 模式下用來微調節拍與畫面的同步")] public float beatPhaseOffset = 0f;
    [Tooltip("Beat Snap 顏色列表（依拍順序循環）")] public Color[] beatSnapColors = new Color[] { Color.white, Color.red };

    [Header("靜止模式顏色選項")]
    [Tooltip("片段進度模式顏色動畫完成後的行為：Clamp — 停在漸層末端 / Loop — 循環回起點")]
    public UnifiedStageController.ColorFinishMode staticColorFinishMode = UnifiedStageController.ColorFinishMode.Clamp;

    [Header("燈具物理設定")]
    [Tooltip("光束角度")] public float beamAngle = 5.0f;
    [Tooltip("開啟散射模式")] public bool enableScatterMode = false;

    [Header("旋轉動作設定")]
    [Tooltip("旋轉模式")] public UnifiedStageController.RotationMode rotationMode;
    [Tooltip("旋轉速度")] public float rotationSpeed = 1.0f;
    [Tooltip("旋轉幅度")] public float rotationRange = 45.0f;
    [Tooltip("静止角度偏移 (x=pan基底, y=tilt基底)")] public Vector2 staticAngleOffset;
    [Tooltip("週期停頓時間")] public float cyclePauseTime = 0f;
    [Tooltip("動畫起點偏移(秒)，對循環動畫的相位起點產生時間偏移")] public float animationOffset = 0f;
    [Tooltip("凍結前幀——啟用後改為以 Clip 自身 Light Gradient 取色（Clip 頭尾對應 0-1），並與前後 Clip 正常 Blending；停用則凍結前一個 Clip 的瞬間顏色")] public bool freezeUseClipGradient = false;

    [Header("目標追蹤設定")]
    [Tooltip("追蹤目標")] public ExposedReference<Transform> trackingTarget;

    [Header("分組延遲")]
    [Tooltip("分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）\n" +
             "group 延遲 = groupDelayCurve(t) × groupDelayFactor × groupCount")]
    public AnimationCurve groupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("分組延遲係數（秒）")]
    public float groupDelayFactor = 0f;

    [Header("組內逐顆延遲")]
    [Tooltip("組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）\n" +
             "light 延遲 = lightDelayCurve(t) × lightDelayFactor × groupSize")]
    public AnimationCurve lightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("組內延遲係數（秒）")]
    public float lightDelayFactor = 0f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    [Header("模板套用")]
    [Tooltip("套用模板")] public UnifiedStageTemplate applyTemplate;
    [HideInInspector] public string clipDisplayName;

    void OnValidate()
    {
        if (applyTemplate != null)
        {
            lightGradient       = applyTemplate.lightGradient;
            intensityMultiplier = applyTemplate.intensityMultiplier;
            sensitivity         = applyTemplate.sensitivity;
            smoothness          = applyTemplate.smoothness;

            colorSampleMode     = applyTemplate.colorSampleMode;
            bpm                 = applyTemplate.bpm;
            beatTimeRef         = applyTemplate.beatTimeRef;
            beatPhaseOffset     = applyTemplate.beatPhaseOffset;
            beatSnapColors      = applyTemplate.beatSnapColors;
            globalColor         = applyTemplate.globalColor;
            staticColorFinishMode = applyTemplate.staticColorFinishMode;

            beamAngle           = applyTemplate.beamAngle;
            enableScatterMode   = applyTemplate.enableScatterMode;
            rotationMode        = applyTemplate.rotationMode;
            rotationSpeed       = applyTemplate.rotationSpeed;
            rotationRange       = applyTemplate.rotationRange;
            staticAngleOffset   = applyTemplate.staticAngleOffset;
            cyclePauseTime      = applyTemplate.cyclePauseTime;
            animationOffset     = applyTemplate.animationOffset;

            groupDelayCurve     = applyTemplate.groupDelayCurve;
            groupDelayFactor    = applyTemplate.groupDelayFactor;
            lightDelayCurve     = applyTemplate.lightDelayCurve;
            lightDelayFactor    = applyTemplate.lightDelayFactor;

            clipDisplayName     = applyTemplate.name;
            applyTemplate       = null;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable  = ScriptPlayable<UnifiedStageBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.clipGradient          = lightGradient;
        behaviour.clipIntensity         = intensityMultiplier;
        behaviour.sensitivity           = sensitivity;
        behaviour.smoothness            = smoothness;
        behaviour.beamAngle             = beamAngle;
        behaviour.scatterMode           = enableScatterMode;
        behaviour.clipMode              = rotationMode;
        behaviour.rotationSpeed         = rotationSpeed;
        behaviour.rotationRange         = rotationRange;
        behaviour.staticOffset          = staticAngleOffset;
        behaviour.pauseTime             = cyclePauseTime;
        behaviour.clipTarget            = trackingTarget.Resolve(graph.GetResolver());

        behaviour.groupDelayCurve       = groupDelayCurve;
        behaviour.groupDelayFactor      = groupDelayFactor;
        behaviour.lightDelayCurve       = lightDelayCurve;
        behaviour.lightDelayFactor      = lightDelayFactor;
        behaviour.animationOffset       = animationOffset;
        behaviour.freezeUseClipGradient = freezeUseClipGradient;
        behaviour.staticColorFinishMode = staticColorFinishMode;

        behaviour.colorSampleMode       = colorSampleMode;
        behaviour.bpm                   = bpm;
        behaviour.beatTimeRef           = beatTimeRef;
        behaviour.beatPhaseOffset       = beatPhaseOffset;
        behaviour.beatSnapColors        = beatSnapColors;
        behaviour.globalColor           = globalColor;

        return playable;
    }
}
