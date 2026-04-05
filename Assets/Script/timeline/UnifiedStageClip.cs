using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

[System.Serializable]
[DisplayName("整合舞台方案片段")]
public class UnifiedStageClip : PlayableAsset, ITimelineClipAsset
{
    [Header("燈光感應設定")]
    [Tooltip("燈光漸變")] public Gradient lightGradient = new Gradient();
    [Tooltip("總體亮度倍率")] public float intensityMultiplier = 1.0f;
    [Tooltip("最低亮度")] public float minBrightness = 0.1f;
    [Tooltip("靈敏度")] public float sensitivity = 1.5f;
    [Tooltip("平滑度")] public float smoothness = 8.0f;

    [Header("燈具物理設定")]
    [Tooltip("光束角度")] public float beamAngle = 5.0f;
    [Tooltip("開啟散射模式")] public bool enableScatterMode = false;

    [Header("旋轉動作設定")]
    [Tooltip("旋轉模式")] public UnifiedStageController.RotationMode rotationMode;
    [Tooltip("旋轉速度")] public float rotationSpeed = 1.0f;
    [Tooltip("旋轉幅度")] public float rotationRange = 45.0f;
    [Tooltip("靜止偏移角度")] public Vector2 staticAngleOffset;
    [Tooltip("週期停頓時間")] public float cyclePauseTime = 0f;

    [Header("目標追蹤設定")]
    [Tooltip("追蹤目標")] public ExposedReference<Transform> trackingTarget;

    [Header("燈光逐顆延遲")]
    [Tooltip("每顆燈依 index remap 到 0~1 後取樣此曲線，取得延遲係數 DV")]
    public AnimationCurve delayCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("實際延遲(秒) = 延遲係數 × DV × 燈數")]
    public float delayFactor = 0f;

    public ClipCaps clipCaps => ClipCaps.Blending;

    [Header("模板套用")]
    [Tooltip("套用模板")] public UnifiedStageTemplate applyTemplate;
    [HideInInspector] public string clipDisplayName;

    void OnValidate()
    {
        if (applyTemplate != null)
        {
            lightGradient = applyTemplate.lightGradient;
            intensityMultiplier = applyTemplate.intensityMultiplier;
            minBrightness = applyTemplate.minBrightness;
            sensitivity = applyTemplate.sensitivity;
            smoothness = applyTemplate.smoothness;

            beamAngle = applyTemplate.beamAngle;
            enableScatterMode = applyTemplate.enableScatterMode;
            rotationMode = applyTemplate.rotationMode;
            rotationSpeed = applyTemplate.rotationSpeed;
            rotationRange = applyTemplate.rotationRange;
            staticAngleOffset = applyTemplate.staticAngleOffset;
            cyclePauseTime = applyTemplate.cyclePauseTime;

            delayCurve = applyTemplate.delayCurve;
            delayFactor = applyTemplate.delayFactor;

            clipDisplayName = applyTemplate.name;
            applyTemplate = null;
        }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<UnifiedStageBehaviour>.Create(graph);
        var b = playable.GetBehaviour();

        b.clipGradient = lightGradient;
        b.clipIntensity = intensityMultiplier;
        b.minBrightness = minBrightness;
        b.sensitivity = sensitivity;
        b.smoothness = smoothness;
        b.beamAngle = beamAngle;
        b.scatterMode = enableScatterMode;
        b.clipMode = rotationMode;
        b.rotationSpeed = rotationSpeed;
        b.rotationRange = rotationRange;
        b.staticOffset = staticAngleOffset;
        b.pauseTime = cyclePauseTime;
        b.clipTarget = trackingTarget.Resolve(graph.GetResolver());

        // 逐顆延遲
        b.delayCurve = delayCurve;
        b.delayFactor = delayFactor;

        return playable;
    }
}