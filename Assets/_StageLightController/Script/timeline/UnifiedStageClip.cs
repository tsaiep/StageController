using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

[System.Serializable]
[DisplayName("UnifiedStageClip")]
public class UnifiedStageClip : PlayableAsset, ITimelineClipAsset
{
    [Header("燈光感應設定")]
    [ColorUsage(true, true), Tooltip("全域顏色乘算（HDR）")] public Color globalColor = Color.white;
    [Tooltip("燈光漸變")] public Gradient lightGradient = new Gradient();
    [Tooltip("Beam Length Gradient：光束頭尾方向的顏色漸層控制")] public Gradient beamLengthGradient = UnifiedStageGradientUtility.CreateDefaultBeamLengthGradient();
    [Tooltip("總體亮度倍率")] public float intensityMultiplier = 1.0f;

    [Header("顏色取樣設定")]
    [Tooltip("顏色取樣模式")] public UnifiedStageController.ColorSampleMode colorSampleMode = UnifiedStageController.ColorSampleMode.MotionCycle;

    [Tooltip("靈敏度（AlongAudioSource 模式：音量放大倍率）")] public float sensitivity = 1.5f;
    [Tooltip("平滑度（AlongAudioSource 模式：音量追蹤速度，越低越不易閃爍）")] public float smoothness = 8.0f;

    [Tooltip("節拍速度（BPM）")] public float bpm = 120f;
    [Tooltip("節拍時間基準")] public UnifiedStageController.BeatTimeReference beatTimeRef = UnifiedStageController.BeatTimeReference.ClipLocal;
    [Tooltip("節拍相位偏移（秒），Timeline Global 模式下用來微調節拍與畫面的同步")] public float beatPhaseOffset = 0f;
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
    [Tooltip("Use UnifiedStageController.audioAnalyzer Beat values to scale this clip's final brightness.")]
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
    [Tooltip("凍結前幀——啟用後改為以 Clip 自身 Light Gradient 取色（Clip 頭尾對應 0-1），並與前後 Clip 正常 Blending；停用則凍結前一個 Clip 的瞬間顏色")] public bool freezeUseClipGradient = false;

    [Header("燈具物理設定")]
    [Tooltip("燈光模式")] public UnifiedStageController.StageLightMode lightMode = UnifiedStageController.StageLightMode.VolumetricSpot;
    [Tooltip("Light component 的 Range")] public float lightRange = 12f;
    [Tooltip("光束角度")] public float beamAngle = 5.0f;
    [Range(0f, 100f), Tooltip("光束邊緣柔和度。Volumetric 模式對應 Side Softness，Spot 模式對應 Inner Spot Angle")]
    public float softness = 0f;
    [Tooltip("開啟散射模式")] public bool enableScatterMode = false;

    [Header("旋轉動作設定")]
    [Tooltip("旋轉模式")] public UnifiedStageController.RotationMode rotationMode;
    [Tooltip("旋轉速度")] public float rotationSpeed = 1.0f;
    [Tooltip("旋轉幅度")] public float rotationRange = 45.0f;
    [Tooltip("静止角度偏移 (x=pan基底, y=tilt基底)")] public Vector2 staticAngleOffset;
    [Tooltip("週期停頓時間")] public float cyclePauseTime = 0f;
    [Tooltip("動畫起點偏移(秒)，對循環動畫的相位起點產生時間偏移")] public float animationOffset = 0f;

    [Header("分散效果設定")]
    [Tooltip("分散角度（SpreadTilt.x 最大值，度）")] public float spreadAngle = 0f;
    [Range(0f, 360f), Tooltip("組內展開弧度（0~360，預設 360=均勻一圈頭尾不重疊）")] public float spreadArcRange = 360f;
    [Tooltip("分散角度曲線（Sample 旋轉動作循環，0~1 乘以分散角度 → SpreadTilt.x）")] public AnimationCurve spreadAngleCurve = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("依所屬燈組內 Index 取樣的 Spread Angle 倍率曲線（indexInGroup/(groupSize-1)，再乘到 SpreadTilt.x）")]
    public AnimationCurve spreadAngleCurveByIndex = AnimationCurve.Constant(0, 1, 1);
    [Tooltip("展開旋轉曲線（Sample 旋轉動作循環，0~1 → 0~360° 附加到 SpreadPan.y）")] public AnimationCurve spreadPanCurve = AnimationCurve.Constant(0, 1, 0);

    [Header("扇形光束設定")]
    [Range(0f, 180f), Tooltip("Maximum fanned laser spread angle in degrees.")]
    public float fannedAngle = 30f;
    [Tooltip("Samples one motion cycle. Multiplied by Fanned Angle and sent to _Range.")]
    public AnimationCurve fannedAngleCurve = AnimationCurve.Constant(0, 1, 1);
    [HideInInspector]
    [Tooltip("Disabled. Retained only for legacy serialized data.")]
    public AnimationCurve fannedRollCurve = AnimationCurve.Constant(0, 1, 0);

    [Header("目標追蹤設定")]
    [Tooltip("追蹤目標")] public ExposedReference<Transform> trackingTarget;

    [Header("分組偏移")]
    [Tooltip("分組延遲曲線（以 groupIndex/(groupCount-1) 取樣）\n" +
             "group 延遲 = groupDelayCurve(t) × groupDelayFactor × groupCount")]
    public AnimationCurve groupDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("分組延遲係數（秒）")]
    public float groupDelayFactor = 0f;

    [Tooltip("分組旋轉幅度曲線（以 groupIndex/(groupCount-1) 取樣）\n" +
             "數値 × rotationRange = 該組的實際旋轉幅度，1 表示不改變")]
    public AnimationCurve groupRotationRangeCurve = AnimationCurve.Constant(0, 1, 1);

    [Header("組內偏移")]
    [Tooltip("組內延遲曲線（以 indexInGroup/(groupSize-1) 取樣）\n" +
             "light 延遲 = lightDelayCurve(t) × lightDelayFactor × groupSize")]
    public AnimationCurve lightDelayCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("組內延遲係數（秒）")]
    public float lightDelayFactor = 0f;

    [Tooltip("組內旋轉幅度曲線（以 indexInGroup/(groupSize-1) 取樣）\n" +
             "數値 × rotationRange = 該顆燈的實際旋轉幅度，1 表示不改變")]
    public AnimationCurve lightRotationRangeCurve = AnimationCurve.Constant(0, 1, 1);

    public ClipCaps clipCaps => ClipCaps.Blending;

    [Header("模板套用")]
    [Tooltip("套用模板")] public UnifiedStageTemplate applyTemplate;

    [HideInInspector] public UnifiedStageTemplate selectedTemplate;
    [Tooltip("套用模板中的顏色設定")] public bool applyTemplateColorSettings = true;
    [Tooltip("套用模板中的旋轉動畫設定")] public bool applyTemplateRotationSettings = true;
    [Tooltip("套用模板中的燈具物理設定")] public bool applyTemplateFixtureSettings = true;
    [HideInInspector] public string clipDisplayName;
    [SerializeField, HideInInspector] private bool mutableDataDetached;

    void OnValidate()
    {
        if (applyTemplate != null)
        {
            ApplyTemplateValues(applyTemplate);
            applyTemplate = null;
        }

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

        if (!mutableDataDetached)
        {
            DetachMutableData();
            mutableDataDetached = true;
        }
    }

    public void ApplyTemplateValues(UnifiedStageTemplate template)
    {
        if (template == null) return;

        selectedTemplate = template;

        if (applyTemplateColorSettings)
        {
            globalColor            = template.globalColor;
            lightGradient          = CloneGradient(template.lightGradient);
            beamLengthGradient     = UnifiedStageGradientUtility.CloneOrDefaultBeamLengthGradient(template.beamLengthGradient);
            intensityMultiplier    = template.intensityMultiplier;
            colorSampleMode        = template.colorSampleMode;
            sensitivity            = template.sensitivity;
            smoothness             = template.smoothness;
            bpm                    = template.bpm;
            beatTimeRef            = template.beatTimeRef;
            beatPhaseOffset        = template.beatPhaseOffset;
            beatSnapColors         = CloneColorArray(template.beatSnapColors);
            beatSnapTransitionTime = template.beatSnapTransitionTime;
            beatGroupDelayFactor   = template.beatGroupDelayFactor;
            beatLightDelayFactor   = template.beatLightDelayFactor;
            beatGroupDelayCurve    = CloneAnimationCurve(template.beatGroupDelayCurve);
            beatLightDelayCurve    = CloneAnimationCurve(template.beatLightDelayCurve);
            useAudioAnalyzerBrightness = template.useAudioAnalyzerBrightness;
            audioBeatLightInterval = template.audioBeatLightInterval;
            audioBeatIndices = CloneIntArray(template.audioBeatIndices);
            audioBrightnessOffset = template.audioBrightnessOffset;
            audioBrightnessMultiplier = template.audioBrightnessMultiplier;
            audioBrightnessLerp = template.audioBrightnessLerp;
            freezeUseClipGradient  = template.freezeUseClipGradient;
        }

        if (applyTemplateRotationSettings)
        {
            rotationMode           = template.rotationMode;
            rotationSpeed          = template.rotationSpeed;
            rotationRange          = template.rotationRange;
            staticAngleOffset      = template.staticAngleOffset;
            cyclePauseTime         = template.cyclePauseTime;
            animationOffset        = template.animationOffset;
            trackingTarget         = template.trackingTarget;
            groupDelayCurve        = CloneAnimationCurve(template.groupDelayCurve);
            groupDelayFactor       = template.groupDelayFactor;
            groupRotationRangeCurve = CloneAnimationCurve(template.groupRotationRangeCurve);
            lightDelayCurve        = CloneAnimationCurve(template.lightDelayCurve);
            lightDelayFactor       = template.lightDelayFactor;
            lightRotationRangeCurve = CloneAnimationCurve(template.lightRotationRangeCurve);
            spreadAngle            = template.spreadAngle;
            spreadArcRange         = template.spreadArcRange;
            spreadAngleCurve       = CloneAnimationCurve(template.spreadAngleCurve);
            spreadAngleCurveByIndex = CloneAnimationCurve(template.spreadAngleCurveByIndex);
            spreadPanCurve         = CloneAnimationCurve(template.spreadPanCurve);
            fannedAngle            = template.fannedAngle;
            fannedAngleCurve       = CloneAnimationCurve(template.fannedAngleCurve);
        }

        if (applyTemplateFixtureSettings)
        {
            lightMode              = template.lightMode;
            lightRange             = template.lightRange;
            beamAngle              = template.beamAngle;
            softness               = template.softness;
            enableScatterMode      = template.enableScatterMode;
        }

        clipDisplayName = template.name;
        mutableDataDetached = true;
    }

    private void DetachMutableData()
    {
        lightGradient = CloneGradient(lightGradient);
        beamLengthGradient = UnifiedStageGradientUtility.CloneOrDefaultBeamLengthGradient(beamLengthGradient);
        beatSnapColors = CloneColorArray(beatSnapColors);
        beatGroupDelayCurve = CloneAnimationCurve(beatGroupDelayCurve);
        beatLightDelayCurve = CloneAnimationCurve(beatLightDelayCurve);
        audioBeatIndices = CloneIntArray(audioBeatIndices);
        groupDelayCurve = CloneAnimationCurve(groupDelayCurve);
        groupRotationRangeCurve = CloneAnimationCurve(groupRotationRangeCurve);
        lightDelayCurve = CloneAnimationCurve(lightDelayCurve);
        lightRotationRangeCurve = CloneAnimationCurve(lightRotationRangeCurve);
        spreadAngleCurve = CloneAnimationCurve(spreadAngleCurve);
        spreadAngleCurveByIndex = CloneAnimationCurve(spreadAngleCurveByIndex);
        spreadPanCurve   = CloneAnimationCurve(spreadPanCurve);
        fannedAngleCurve = CloneAnimationCurve(fannedAngleCurve);
    }

    public static Gradient CloneGradient(Gradient source)
    {
        return UnifiedStageGradientUtility.CloneGradient(source);
    }

    public static AnimationCurve CloneAnimationCurve(AnimationCurve source)
    {
        if (source == null) return null;

        var clone = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
        return clone;
    }

    public static Color[] CloneColorArray(Color[] source)
    {
        return source != null ? (Color[])source.Clone() : null;
    }

    public static int[] CloneIntArray(int[] source)
    {
        return source != null ? (int[])source.Clone() : null;
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable  = ScriptPlayable<UnifiedStageBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();

        behaviour.clipGradient          = lightGradient;
        behaviour.beamLengthGradient    = beamLengthGradient;
        behaviour.clipIntensity         = intensityMultiplier;
        behaviour.sensitivity           = sensitivity;
        behaviour.smoothness            = smoothness;
        behaviour.beamAngle             = beamAngle;
        behaviour.lightMode             = lightMode;
        behaviour.lightRange            = lightRange;
        behaviour.softness              = softness;
        behaviour.scatterMode           = enableScatterMode;
        behaviour.clipMode              = rotationMode;
        behaviour.rotationSpeed         = rotationSpeed;
        behaviour.rotationRange         = rotationRange;
        behaviour.staticOffset          = staticAngleOffset;
        behaviour.pauseTime             = cyclePauseTime;
        behaviour.clipTarget            = trackingTarget.Resolve(graph.GetResolver());

        behaviour.groupDelayCurve          = groupDelayCurve;
        behaviour.groupDelayFactor          = groupDelayFactor;
        behaviour.groupRotationRangeCurve   = groupRotationRangeCurve;
        behaviour.lightDelayCurve           = lightDelayCurve;
        behaviour.lightDelayFactor          = lightDelayFactor;
        behaviour.lightRotationRangeCurve   = lightRotationRangeCurve;
        behaviour.animationOffset       = animationOffset;
        behaviour.freezeUseClipGradient = freezeUseClipGradient;
        behaviour.spreadAngle           = spreadAngle;
        behaviour.spreadArcRange        = spreadArcRange;
        behaviour.spreadAngleCurve      = spreadAngleCurve;
        behaviour.spreadAngleCurveByIndex = spreadAngleCurveByIndex;
        behaviour.spreadPanCurve        = spreadPanCurve;
        behaviour.fannedAngle           = fannedAngle;
        behaviour.fannedAngleCurve      = fannedAngleCurve;

        behaviour.colorSampleMode       = colorSampleMode;
        behaviour.bpm                   = bpm;
        behaviour.beatTimeRef           = beatTimeRef;
        behaviour.beatPhaseOffset       = beatPhaseOffset;
        behaviour.beatSnapColors        = beatSnapColors;
        behaviour.beatSnapTransitionTime = beatSnapTransitionTime;
        behaviour.beatGroupDelayFactor  = beatGroupDelayFactor;
        behaviour.beatLightDelayFactor  = beatLightDelayFactor;
        behaviour.beatGroupDelayCurve   = beatGroupDelayCurve;
        behaviour.beatLightDelayCurve   = beatLightDelayCurve;
        behaviour.useAudioAnalyzerBrightness = useAudioAnalyzerBrightness;
        behaviour.audioBeatLightInterval = audioBeatLightInterval;
        behaviour.audioBeatIndices = audioBeatIndices;
        behaviour.audioBrightnessOffset = audioBrightnessOffset;
        behaviour.audioBrightnessMultiplier = audioBrightnessMultiplier;
        behaviour.audioBrightnessLerp = audioBrightnessLerp;
        behaviour.globalColor           = globalColor;

        return playable;
    }
}
