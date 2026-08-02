using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;

[ExecuteAlways]
public class UnifiedStageController : MonoBehaviour
{
    private const float MinVlbSideSoftness = 0.0001f;
    private const float MaxVlbSideSoftness = 10f;
    private static readonly int LaserMeshBaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int LaserMeshSoftnessShaderId = Shader.PropertyToID("_Softness");
    private static readonly int LaserMeshRangeShaderId = Shader.PropertyToID("_Range");
    private static readonly int FannedLaserAngleShaderId = Shader.PropertyToID("_Angle");
    private MaterialPropertyBlock _laserMeshPropertyBlock;

    public struct WeightedGradientContribution
    {
        public Gradient gradient;
        public Color tint;
        public float weight;
    }

    public enum RotationMode
    {
        [InspectorName("靜止模式")] Static,
        [InspectorName("掃描模式")] Scan,
        [InspectorName("圓周運動")] Circle,
        [InspectorName("隨機跳動")] Random,
        [InspectorName("目標追蹤")] Target,
        [InspectorName("上下搖擺")] VerticalSwing,
        [InspectorName("交叉掃描")] Cross,
        [InspectorName("凍結前幀")] FreezeFrame
    }

    public enum ColorSampleMode
    {
        [InspectorName("動作循環")]              MotionCycle,
        [InspectorName("片段進度")]              ClipProgress,
        [InspectorName("跟隨節拍（漸層取樣）")] BeatGradient,
        [InspectorName("跟隨節拍（瞬間切換）")] BeatSnap,
        [InspectorName("跟隨音樂")]              AlongAudioSource,
    }

    public enum StageLightMode
    {
        [InspectorName("Volumetric Spot Light")] VolumetricSpot,
        [InspectorName("Spot Light")] Spot,
        [InspectorName("Point Light")] Point,
        [InspectorName("Laser Mesh")] LaserMesh,
        [InspectorName("Fanned Laser")] FannedLaser
    }

    public enum BeatTimeReference
    {
        [InspectorName("Clip 起點為第一拍")] ClipLocal,
        [InspectorName("Timeline 全域時間")] TimelineGlobal,
    }

    [Header("受控單元配置")]
    public SLMUnit[] slmUnits;
    public Transform defaultTarget;
    public AudioSource audioSource;
    public MMAudioAnalyzer audioAnalyzer;

    [Header("Template Preview")]
    [Tooltip("Prefab used by the editor preview. The preview instantiates three hidden copies and only updates mesh colors/rotations.")]
    public GameObject templatePreviewPrefab;

    [Header("播放控制 (可由 Animation Track K 幀)")]
    [Tooltip("動作啟動")] public bool enableMotion = true;
    [Tooltip("顏色更新啟動")] public bool enableColorUpdate = true;

    [Header("群組對稱設定")]
    public bool invertControllerPan = false;
    public bool invertControllerTilt = false;

    [Header("基礎物理參數")]
    public Vector3 panRotationVector = Vector3.up;
    public Vector3 tiltRotationVector = Vector3.left;
    public float baseIntensity = 50f;
    public float waveIntensity = 1.0f;
    public float baseSmoothTime = 0.3f;

    [Header("追蹤進階修正")]
    public float panOffset = 0f;
    public float tiltOffset = 0f;
    [Tooltip("反轉垂直追蹤")] public bool invertVerticalTracking = true;
    [Tooltip("垂直基礎偏移")] public float verticalBaseOffset = -90f;

    [Header("追蹤自然度微調")]
    public float maxRotationSpeed = 300f;
    public float trackingSmoothTime = 0.15f;

    // --- 內部狀態 ---
    private float[] spectrum = new float[256];
    private float lowMax = 0.1f, midMax = 0.1f, highMax = 0.1f;
    private float curLow, curMid, curHigh;

    public float GetLowEnergy()  { return curLow; }
    public float GetMidEnergy()  { return curMid; }
    public float GetHighEnergy() { return curHigh; }
    public float LowEnergy  => curLow;
    public float MidEnergy  => curMid;
    public float HighEnergy => curHigh;

    // ==========================================
    //  主入口：由 Mixer 呼叫
    // ==========================================
    public void UpdateStage(
        List<ActiveClipInfo> clips, float[] spec, bool isTimeJump,
        float mixedInten, float mixedBeamAngle, float mixedLightRange, float mixedSoftness,
        bool activeScatter, StageLightMode activeLightMode,
        float totalMotionWeight, float weightedEffectiveTime,
        bool freezeJustActivated, float rootTime)
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;

        // 1. 頻譜分析（僅 AlongAudioSource 模式使用）
        bool hasAudioMode = false;
        float audioWeightedSmooth = 0f, audioTotalWeight = 0f;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i].colorSampleMode == ColorSampleMode.AlongAudioSource)
            {
                hasAudioMode = true;
                audioWeightedSmooth += clips[i].smoothness * clips[i].weight;
                audioTotalWeight    += clips[i].weight;
            }
        }

        if (hasAudioMode && audioTotalWeight > 0f)
        {
            System.Array.Copy(spec, spectrum, 256);
            ProcessSpectrum(audioWeightedSmooth / audioTotalWeight, isTimeJump, dt);
        }
        else if (isTimeJump)
        {
            curLow = curMid = curHigh = 0f;
            lowMax = midMax = highMax = 0.01f;
        }

        // 2. 物理 dt
        bool isMoving = enableMotion && totalMotionWeight > 0.01f;
        float physicalDt = Application.isPlaying ? Time.unscaledDeltaTime : 0.02f;
        bool forceSnap = !isMoving || isTimeJump;

        if (forceSnap)
        {
            physicalDt = 0f;
            if (slmUnits != null)
                foreach (var u in slmUnits)
                {
                    if (u != null)
                    {
                        u.velPan = 0;
                        u.velTilt = 0;
                        u.velSpreadPan  = 0;
                        u.velSpreadTilt = 0;
                    }
                }
        }
        else if (Application.isPlaying)
        {
            physicalDt = Mathf.Max(physicalDt, 0.012f);
        }

        // 3. Per-unit 處理
        if (slmUnits == null) return;
        int unitCount = slmUnits.Length;

        for (int ui = 0; ui < unitCount; ui++)
        {
            var unit = slmUnits[ui];
            if (unit == null) continue;

            // 更新 tiltAxisSignCache，供 ApplyBaseToTransforms 參考正確的 eulerAngles.x
            // 注意：這裡使用處理後的 tiltRotationVector，也就是 X 乘上 -1 後的軸向。
            float tAxisX = GetSafeAxis(GetProcessedTiltRotationVector(), Vector3.left).x;
            unit.tiltAxisSignCache = (tAxisX >= 0f) ? 1f : -1f;

            // --- 累加變數 ---
            EnsureGradientRuntimeCache(unit, clips.Count);
            var gradientContributions = unit.gradientContributions;
            gradientContributions.Clear();
            float totalPan = 0f, totalTilt = 0f;
            float totalSpreadPan = 0f, totalSpreadTilt = 0f;
            float totalFannedAngle = 0f;
            float totalFannedRoll = 0f;
            float targetModeWeight = 0f;

            // --- FreezeFrame Rising Edge: 快取此 unit 的現在狀態 ---
            if (freezeJustActivated)
            {
                unit.frozenPan   = unit.curPan;
                unit.frozenTilt  = unit.curTilt;
                unit.frozenColor = (unit.targetLight != null) ? unit.targetLight.color : Color.black;
                CaptureCurrentGradient(unit);
            }

            // --- 逐 Clip 計算 ---
            for (int ci = 0; ci < clips.Count; ci++)
            {
                var clip = clips[ci];

                // ===== Per-unit 延遲（兩層：分組延遲 + 組內逐顆延遲）=====
                float unitDelay = 0f;
                bool isRandomMode = (clip.mode == RotationMode.Random);
                if (!isRandomMode)
                {
                    // 層一：分組延遲（以 groupIndex/(groupCount-1) 取樣曲線）
                    float groupDelay = 0f;
                    if (clip.groupDelayCurve != null && clip.groupDelayFactor > 0f)
                    {
                        float normalizedGroup = (unit.groupCount > 1)
                            ? (float)unit.groupIndex / (unit.groupCount - 1)
                            : 0f;
                        float gv = clip.groupDelayCurve.Evaluate(normalizedGroup);
                        groupDelay = clip.groupDelayFactor * gv * unit.groupCount;
                    }

                    // 層二：組內逐顆延遲（以 indexInGroup/(groupSize-1) 取樣曲線）
                    float lightDelay = 0f;
                    if (clip.lightDelayCurve != null && clip.lightDelayFactor > 0f)
                    {
                        float normalizedInGroup = (unit.groupSize > 1)
                            ? (float)unit.indexInGroup / (unit.groupSize - 1)
                            : 0f;
                        float lv = clip.lightDelayCurve.Evaluate(normalizedInGroup);
                        lightDelay = clip.lightDelayFactor * lv * unit.groupSize;
                    }

                    unitDelay = groupDelay + lightDelay;
                }

                // 全域有效時間（所有 unit 共用）
                float globalEt = Mathf.Max(0, clip.effectiveTime - clip.pauseTime);

                // Per-unit 帶延遲的有效時間（相位偏移）
                // AnimationOffset 不適用於 Static 與 FreezeFrame
                bool applyAnimOffset = clip.mode != RotationMode.Static && clip.mode != RotationMode.FreezeFrame;
                float unitEt = globalEt - unitDelay + (applyAnimOffset ? clip.animationOffset : 0f);

                // ===== 顏色（依 ColorSampleMode 計算）=====
                if (enableColorUpdate)
                {
                    Color clipColor = ComputeClipColor(clip, unit, rootTime, unitEt, unitDelay);
                    float audioBrightnessScale = ComputeAudioAnalyzerBrightnessScale(clip, unit, ci, isTimeJump, dt);
                    if (!Mathf.Approximately(audioBrightnessScale, 1f))
                        clipColor = UnifiedStageGradientUtility.ForceOpaque(clipColor * audioBrightnessScale);
                    AddGradientContribution(gradientContributions, clip, unit, clipColor);
                }

                // ===== 旋轉 =====
                if (unit.panTransform == null || unit.tiltTransform == null) continue;

                float clipPan, clipTilt;

                if (clip.isFreezeFrame)
                {
                    // --- FreezeFrame: 使用凍結的 pan/tilt ---
                    clipPan  = unit.frozenPan;
                    clipTilt = unit.frozenTilt;
                    // 不累加 targetModeWeight，物理更新使用一般 smooth
                }
                else if (clip.mode == RotationMode.Target)
                {
                    // --- 目標追蹤：LookAt 角度 ---
                    Transform finalTarget = (clip.target != null) ? clip.target : defaultTarget;
                    if (finalTarget != null)
                    {
                        Vector2 look = CalculateLookAtAngles(unit.panTransform, unit.tiltTransform, finalTarget);
                        clipPan  = look.x;
                        clipTilt = look.y;
                    }
                    else
                    {
                        clipPan  = clip.staticOffset.x;
                        clipTilt = clip.staticOffset.y;
                    }

                    // Target 不套用 invert
                    targetModeWeight += clip.weight;
                }
                else
                {
                    // --- 非追蹤模式：直接 pan/tilt 計算 ---
                    float unitTimeOffset = unit.motionOffset * waveIntensity;
                    float adjustedEt = unitEt + unitTimeOffset;

                    // ── 計算有效旋轉幅度（分組 × 組內 × 基礎 range）──
                    float groupRangeMult = 1f;
                    if (clip.groupRotationRangeCurve != null)
                    {
                        float normalizedGroup = (unit.groupCount > 1)
                            ? (float)unit.groupIndex / (unit.groupCount - 1) : 0f;
                        groupRangeMult = clip.groupRotationRangeCurve.Evaluate(normalizedGroup);
                    }
                    float lightRangeMult = 1f;
                    if (clip.lightRotationRangeCurve != null)
                    {
                        float normalizedInGroup = (unit.groupSize > 1)
                            ? (float)unit.indexInGroup / (unit.groupSize - 1) : 0f;
                        lightRangeMult = clip.lightRotationRangeCurve.Evaluate(normalizedInGroup);
                    }
                    float effectiveRange = clip.range * groupRangeMult * lightRangeMult;

                    Vector2 angles;
                    if (clip.mode == RotationMode.Circle)
                    {
                        // Circle：幾何圓錐方向式 solver（正確等速圓）
                        angles = CalculateCircleAngles(adjustedEt, clip.speed, effectiveRange, clip.staticOffset);
                    }
                    else
                    {
                        angles = CalculateAnglesForUnit(
                            clip.mode, clip.speed, effectiveRange,
                            adjustedEt, ui, clip.staticOffset, clip.randomStrength
                        );
                    }

                    clipPan  = angles.x;
                    clipTilt = angles.y;

                    // ── 套用每盞燈的旋轉基準偏移（rotationBase）──
                    // 在 invertTilt 之前加入，使其與 staticOffset 走同一條路，
                    // 避免 invertTilt 導致兩者方向相反而互相抵銷。
                    clipPan  += unit.rotationBase.x;
                    clipTilt += unit.rotationBase.y;

                    // 套用對稱反轉
                    clipPan  = (unit.invertPan  ^ invertControllerPan)  ? -clipPan  : clipPan;
                    clipTilt = (unit.invertTilt ^ invertControllerTilt) ? -clipTilt : clipTilt;
                }

                // ── 正規化角度：確保每個 Clip 的角度在 curPan/curTilt 的 ±180° 範圍內 ──
                // 這解決了 Circle 模式累積大角度與其他 Clip 混合時的反轉問題：
                // 加權平均前先把所有角度映射到以 curPan 為中心的連續範圍，
                // 使 weighted sum 等同於物理上正確的中間角度。
                // 同時修正 FreezeFrame 繼承累積角度後混合的問題。
                clipPan  = unit.curPan  + Mathf.DeltaAngle(unit.curPan,  clipPan);
                clipTilt = unit.curTilt + Mathf.DeltaAngle(unit.curTilt, clipTilt);

                totalPan  += clipPan  * clip.weight;
                totalTilt += clipTilt * clip.weight;

                // ── Spread 分散效果（僅在有任一 Spread Transform 時執行）──
                if (unit.spreadPanTransform != null || unit.spreadTiltTransform != null)
                {
                    // 取樣循環進度（與 MotionCycle 顏色模式相同邏輯）
                    float cyclePeriod = UnifiedStageBehaviour.GetMotionCyclePeriod(clip.mode, clip.speed);
                    float cycleT;
                    if (cyclePeriod > 0.0001f)
                    {
                        float raw = unitEt / cyclePeriod;
                        cycleT = raw - Mathf.Floor(raw);
                    }
                    else
                    {
                        cycleT = clip.normalizedClipTime;
                    }

                    // SpreadTilt：spreadAngle × 曲線值
                    float curveAngle = (clip.spreadAngleCurve != null) ? clip.spreadAngleCurve.Evaluate(cycleT) : 1f;
                    float normalizedIndexInGroup = (unit.groupSize > 1)
                        ? (float)unit.indexInGroup / (unit.groupSize - 1)
                        : 0f;
                    float curveAngleByIndex = (clip.spreadAngleCurveByIndex != null)
                        ? clip.spreadAngleCurveByIndex.Evaluate(normalizedIndexInGroup)
                        : 1f;
                    float clipSpreadTilt = clip.spreadAngle * curveAngle * curveAngleByIndex;

                    // SpreadPan：組內基礎偏移 + 曲線動態偏移
                    // 除以 groupSize（非 groupSize-1），使 360° 時頭尾不重疊
                    float normInGroup = (unit.groupSize > 1) ? (float)unit.indexInGroup / unit.groupSize : 0f;
                    float baseSpreadPan = normInGroup * clip.spreadArcRange;
                    float curvePan = (clip.spreadPanCurve != null) ? clip.spreadPanCurve.Evaluate(cycleT) : 0f;
                    // DeltaAngle 正規化，防止視覺跳轉，確保數值不無限累加
                    float clipSpreadPan = unit.curSpreadPan + Mathf.DeltaAngle(unit.curSpreadPan, baseSpreadPan + curvePan * 360f);

                    totalSpreadTilt += clipSpreadTilt * clip.weight;
                    totalSpreadPan  += clipSpreadPan  * clip.weight;
                }

                if (activeLightMode == StageLightMode.FannedLaser)
                {
                    float cycleT = CalculateMotionCycleT(clip, unitEt);
                    float angleCurve = (clip.fannedAngleCurve != null) ? clip.fannedAngleCurve.Evaluate(cycleT) : 1f;
                    float clipFannedAngle = Mathf.Clamp(clip.fannedAngle * angleCurve, 0f, 180f);
                    float clipFannedRoll = unit.curFannedRoll + Mathf.DeltaAngle(unit.curFannedRoll, clip.fannedRoll);

                    totalFannedAngle += clipFannedAngle * clip.weight;
                    totalFannedRoll  += clipFannedRoll  * clip.weight;
                }
            }

            // ===== 物理更新 =====
            if (unit.panTransform != null && unit.tiltTransform != null)
            {
                // 追蹤模式依權重調整 smooth time
                float sTime = Mathf.Lerp(baseSmoothTime, trackingSmoothTime, targetModeWeight);
                float mSpeed = Mathf.Lerp(maxRotationSpeed, Mathf.Max(maxRotationSpeed, 600f), targetModeWeight);

                if (physicalDt <= 0.0001f)
                {
                    // Snap（手動拖曳 / 停止 / 時間跳轉）
                    unit.curPan = totalPan;
                    unit.curTilt = totalTilt;
                    unit.velPan = 0;
                    unit.velTilt = 0;
                }
                else
                {
                    sTime = Mathf.Max(sTime, 0.02f);

                    // Circle 模式輸出已改為有界的 sin/cos，統一用 SmoothDampAngle
                    unit.curPan  = Mathf.SmoothDampAngle(unit.curPan,  totalPan,  ref unit.velPan,  sTime, mSpeed, physicalDt);
                    unit.curTilt = Mathf.SmoothDampAngle(unit.curTilt, totalTilt, ref unit.velTilt, sTime, mSpeed, physicalDt);
                }

                unit.panTransform.localRotation =
                    Quaternion.AngleAxis(unit.curPan, GetSafeAxis(panRotationVector, Vector3.up));

                unit.tiltTransform.localRotation =
                    Quaternion.AngleAxis(unit.curTilt, GetSafeAxis(GetProcessedTiltRotationVector(), Vector3.left));
            }

            // ===== Spread Transform 更新 =====
            if (unit.spreadPanTransform != null || unit.spreadTiltTransform != null)
            {
                float sTime  = Mathf.Lerp(baseSmoothTime, trackingSmoothTime, targetModeWeight);
                float mSpeed = Mathf.Lerp(maxRotationSpeed, Mathf.Max(maxRotationSpeed, 600f), targetModeWeight);

                if (physicalDt <= 0.0001f)
                {
                    unit.curSpreadPan  = totalSpreadPan;
                    unit.curSpreadTilt = totalSpreadTilt;
                    unit.velSpreadPan  = 0f;
                    unit.velSpreadTilt = 0f;
                }
                else
                {
                    // Spread curves are authored against the motion cycle, so apply them directly.
                    // Smoothing here makes curve endpoints and tangents feel delayed.
                    unit.curSpreadPan  = totalSpreadPan;
                    unit.curSpreadTilt = totalSpreadTilt;
                    unit.velSpreadPan  = 0f;
                    unit.velSpreadTilt = 0f;
                }

                if (unit.spreadPanTransform  != null)
                    unit.spreadPanTransform.localRotation  = Quaternion.AngleAxis(unit.curSpreadPan,  Vector3.up);
                if (unit.spreadTiltTransform != null)
                    unit.spreadTiltTransform.localRotation = Quaternion.AngleAxis(unit.curSpreadTilt, Vector3.right);
            }

            // ===== 燈光 =====
            bool activeRendererLightMode = activeLightMode == StageLightMode.LaserMesh ||
                                           activeLightMode == StageLightMode.FannedLaser;
            if (activeRendererLightMode)
            {
                bool rendererLightIsTimelineBlending = gradientContributions.Count > 1;
                Gradient rendererLightBuildTarget = (isTimeJump || rendererLightIsTimelineBlending) ? unit.currentGradient : unit.targetGradient;

                BuildWeightedFinalGradient(
                    unit,
                    gradientContributions,
                    rendererLightBuildTarget,
                    unit.gradientKeyTimes,
                    ref unit.gradientColorKeys,
                    ref unit.gradientAlphaKeys);

                if (!isTimeJump && !rendererLightIsTimelineBlending)
                {
                    LerpGradientInto(
                        unit.currentGradient,
                        unit.currentGradient,
                        unit.targetGradient,
                        dt * 25f,
                        unit.gradientKeyTimes,
                        ref unit.gradientColorKeys,
                        ref unit.gradientAlphaKeys);
                }

                Color rendererLightFinalColor = unit.currentGradient != null
                    ? UnifiedStageGradientUtility.ForceOpaque(unit.currentGradient.Evaluate(0f))
                    : Color.black;

                bool useLaserMesh = activeLightMode == StageLightMode.LaserMesh;
                bool useFannedLaser = activeLightMode == StageLightMode.FannedLaser;
                float fannedLaserRoll = SLMUnit.NormalizeAngle(totalFannedRoll);
                if (useFannedLaser)
                    unit.curFannedRoll = fannedLaserRoll;

                ApplyLaserMeshRenderers(unit, useLaserMesh, rendererLightFinalColor, mixedSoftness, mixedLightRange);
                ApplyFannedLaserRenderers(unit, useFannedLaser, rendererLightFinalColor, mixedSoftness, mixedLightRange, totalFannedAngle, fannedLaserRoll);
            }
            else
            {
                ApplyLaserMeshRenderers(unit, false, Color.black, mixedSoftness, mixedLightRange);
                ApplyFannedLaserRenderers(unit, false, Color.black, mixedSoftness, 0f, 0f, 0f);
            }

            if (unit.targetLight != null)
            {
                if (activeLightMode == StageLightMode.LaserMesh || activeLightMode == StageLightMode.FannedLaser)
                {
                    var laserMeshVlb = unit.targetLight.GetComponent<VLB.VolumetricLightBeamHD>();
                    ApplyLightMode(unit, laserMeshVlb, activeLightMode);

                    var laserMeshCookie = unit.targetLight.GetComponent<VLB.VolumetricCookieHD>();
                    if (laserMeshCookie != null) laserMeshCookie.enabled = false;

                    continue;
                }

                float modeIntensityScale = activeLightMode == StageLightMode.Point ? 0.15f : 1f;
                unit.targetLight.intensity = baseIntensity * mixedInten * modeIntensityScale;
                unit.targetLight.range = Mathf.Max(0.01f, mixedLightRange);

                bool isTimelineBlending = gradientContributions.Count > 1;
                Gradient buildTarget = (isTimeJump || isTimelineBlending) ? unit.currentGradient : unit.targetGradient;

                BuildWeightedFinalGradient(
                    unit,
                    gradientContributions,
                    buildTarget,
                    unit.gradientKeyTimes,
                    ref unit.gradientColorKeys,
                    ref unit.gradientAlphaKeys);

                if (!isTimeJump && !isTimelineBlending)
                {
                    LerpGradientInto(
                        unit.currentGradient,
                        unit.currentGradient,
                        unit.targetGradient,
                        dt * 25f,
                        unit.gradientKeyTimes,
                        ref unit.gradientColorKeys,
                        ref unit.gradientAlphaKeys);
                }

                Color finalColor = unit.currentGradient != null
                    ? UnifiedStageGradientUtility.ForceOpaque(unit.currentGradient.Evaluate(0f))
                    : Color.black;

                unit.targetLight.color = finalColor;

                var vlb = unit.targetLight.GetComponent<VLB.VolumetricLightBeamHD>();
                ApplyLightMode(unit, vlb, activeLightMode);

                if (unit.targetLight.type == LightType.Spot)
                {
                    unit.targetLight.spotAngle = mixedBeamAngle;
                    unit.targetLight.innerSpotAngle = CalculateInnerSpotAngle(mixedBeamAngle, mixedSoftness);
                }

                if (vlb != null)
                {
                    if (vlb.colorFromLight)
                        vlb.colorFromLight = false;
                    EnsureVlbGradientMode(unit, vlb);
                    vlb.colorFlat = finalColor;
                    vlb.spotAngle = mixedBeamAngle;
                    vlb.sideSoftness = CalculateVlbSideSoftness(mixedSoftness);
                    if (vlb.enabled)
                        vlb.UpdateAfterManualPropertyChange();
                }

                var cookie = unit.targetLight.GetComponent<VLB.VolumetricCookieHD>();
                if (cookie != null) cookie.enabled = activeScatter && activeLightMode != StageLightMode.LaserMesh && activeLightMode != StageLightMode.FannedLaser;
            }
        }
    }

    private void ApplyLaserMeshRenderers(SLMUnit unit, bool active, Color color, float softness, float lightRange)
    {
        if (unit == null || unit.laserMeshRenderers == null)
            return;

        for (int i = 0; i < unit.laserMeshRenderers.Length; i++)
        {
            MeshRenderer renderer = unit.laserMeshRenderers[i];
            if (renderer == null)
                continue;

            if (renderer.gameObject.activeSelf != active)
                renderer.gameObject.SetActive(active);

            if (renderer.enabled != active)
                renderer.enabled = active;

            if (!active)
                continue;

            if (_laserMeshPropertyBlock == null)
                _laserMeshPropertyBlock = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_laserMeshPropertyBlock);
            _laserMeshPropertyBlock.SetColor(LaserMeshBaseColorShaderId, color);
            _laserMeshPropertyBlock.SetFloat(LaserMeshSoftnessShaderId, softness);
            _laserMeshPropertyBlock.SetFloat(LaserMeshRangeShaderId, Mathf.Max(0f, lightRange));
            renderer.SetPropertyBlock(_laserMeshPropertyBlock);
        }
    }

    private void ApplyFannedLaserRenderers(SLMUnit unit, bool active, Color color, float softness, float lightRange, float angle, float roll)
    {
        if (unit == null || unit.fannedLaserRenderers == null)
            return;

        for (int i = 0; i < unit.fannedLaserRenderers.Length; i++)
        {
            MeshRenderer renderer = unit.fannedLaserRenderers[i];
            if (renderer == null)
                continue;

            if (renderer.gameObject.activeSelf != active)
                renderer.gameObject.SetActive(active);

            if (renderer.enabled != active)
                renderer.enabled = active;

            if (!active)
                continue;

            Transform rendererTransform = renderer.transform;
            Vector3 localEulerAngles = rendererTransform.localEulerAngles;
            localEulerAngles.y = Mathf.Repeat(roll, 360f);
            rendererTransform.localEulerAngles = localEulerAngles;

            if (_laserMeshPropertyBlock == null)
                _laserMeshPropertyBlock = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_laserMeshPropertyBlock);
            _laserMeshPropertyBlock.SetColor(LaserMeshBaseColorShaderId, color);
            _laserMeshPropertyBlock.SetFloat(LaserMeshSoftnessShaderId, softness);
            _laserMeshPropertyBlock.SetFloat(LaserMeshRangeShaderId, Mathf.Max(0f, lightRange));
            _laserMeshPropertyBlock.SetFloat(FannedLaserAngleShaderId, Mathf.Clamp(angle, 0f, 180f));
            renderer.SetPropertyBlock(_laserMeshPropertyBlock);
        }
    }

    private float ComputeAudioAnalyzerBrightnessScale(ActiveClipInfo clip, SLMUnit unit, int clipIndex, bool isTimeJump, float dt)
    {
        if (!clip.useAudioAnalyzerBrightness)
            return 1f;

        if (audioAnalyzer == null || audioAnalyzer.Beats == null || audioAnalyzer.Beats.Length == 0)
            return 1f;

        if (clip.audioBeatIndices == null || clip.audioBeatIndices.Length == 0)
            return 1f;

        int interval = Mathf.Max(1, clip.audioBeatLightInterval);
        int indexInGroup = unit != null ? Mathf.Max(0, unit.indexInGroup) : 0;
        int slot = (indexInGroup / interval) % clip.audioBeatIndices.Length;
        int beatID = Mathf.Max(0, clip.audioBeatIndices[slot]);

        if (beatID >= audioAnalyzer.Beats.Length)
            return 1f;

        Beat beat = audioAnalyzer.Beats[beatID];
        if (beat == null)
            return 1f;

        float beatValue = Mathf.Max(0f, beat.CurrentValue);
        float targetScale = Mathf.Max(0f, clip.audioBrightnessOffset + beatValue * clip.audioBrightnessMultiplier);
        float lerpSpeed = Mathf.Max(0f, clip.audioBrightnessLerp);

        if (unit == null || lerpSpeed <= 0f || isTimeJump || dt <= 0f)
            return targetScale;

        EnsureAudioBrightnessRuntimeCache(unit, clipIndex + 1);
        if (!unit.audioBrightnessInitialized[clipIndex])
        {
            unit.audioBrightnessValues[clipIndex] = targetScale;
            unit.audioBrightnessInitialized[clipIndex] = true;
            return targetScale;
        }

        unit.audioBrightnessValues[clipIndex] = Mathf.Lerp(
            unit.audioBrightnessValues[clipIndex],
            targetScale,
            lerpSpeed * dt);

        return unit.audioBrightnessValues[clipIndex];
    }

    private static void AddGradientContribution(
        List<WeightedGradientContribution> contributions,
        ActiveClipInfo clip,
        SLMUnit unit,
        Color clipColor)
    {
        if (contributions == null || clip.weight <= 0f)
            return;

        if (clip.isFreezeFrame && !clip.freezeUseClipGradient && unit != null && unit.frozenGradient != null)
        {
            contributions.Add(new WeightedGradientContribution
            {
                gradient = unit.frozenGradient,
                tint = clip.globalColor,
                weight = clip.weight
            });
            return;
        }

        contributions.Add(new WeightedGradientContribution
        {
            gradient = clip.beamLengthGradient,
            tint = clipColor,
            weight = clip.weight
        });
    }

    private static void EnsureGradientRuntimeCache(SLMUnit unit, int clipCapacity)
    {
        if (unit.gradientContributions == null)
            unit.gradientContributions = new List<WeightedGradientContribution>(Mathf.Max(4, clipCapacity));
        if (unit.gradientKeyTimes == null)
            unit.gradientKeyTimes = new List<float>(8);
        EnsureAudioBrightnessRuntimeCache(unit, clipCapacity);
        if (unit.currentGradient == null)
            unit.currentGradient = UnifiedStageGradientUtility.CreateSolidGradient(Color.black);
        if (unit.targetGradient == null)
            unit.targetGradient = UnifiedStageGradientUtility.CreateSolidGradient(Color.black);
        if (unit.frozenGradient == null)
            unit.frozenGradient = UnifiedStageGradientUtility.CreateSolidGradient(Color.black);
    }

    private static void EnsureAudioBrightnessRuntimeCache(SLMUnit unit, int clipCapacity)
    {
        if (unit == null)
            return;

        int capacity = Mathf.Max(1, clipCapacity);
        if (unit.audioBrightnessValues != null && unit.audioBrightnessValues.Length >= capacity &&
            unit.audioBrightnessInitialized != null && unit.audioBrightnessInitialized.Length >= capacity)
            return;

        float[] oldValues = unit.audioBrightnessValues;
        bool[] oldInitialized = unit.audioBrightnessInitialized;
        unit.audioBrightnessValues = new float[capacity];
        unit.audioBrightnessInitialized = new bool[capacity];

        if (oldValues == null || oldInitialized == null)
            return;

        int copyCount = Mathf.Min(oldValues.Length, unit.audioBrightnessValues.Length);
        System.Array.Copy(oldValues, unit.audioBrightnessValues, copyCount);
        copyCount = Mathf.Min(oldInitialized.Length, unit.audioBrightnessInitialized.Length);
        System.Array.Copy(oldInitialized, unit.audioBrightnessInitialized, copyCount);
    }

    private static void EnsureVlbGradientMode(SLMUnit unit, VLB.VolumetricLightBeamHD vlb)
    {
        if (unit == null || vlb == null)
            return;

        if (unit.hasConfiguredVlbGradientMode && vlb.colorMode == VLB.ColorMode.Gradient && vlb.colorGradient == unit.currentGradient)
            return;

        vlb.colorMode = VLB.ColorMode.Gradient;
        vlb.colorGradient = unit.currentGradient;
        unit.hasConfiguredVlbGradientMode = true;
    }

    private static void BuildWeightedFinalGradient(
        SLMUnit unit,
        List<WeightedGradientContribution> contributions,
        Gradient result,
        List<float> keyTimes,
        ref GradientColorKey[] colorKeys,
        ref GradientAlphaKey[] alphaKeys)
    {
        if (result == null)
            return;

        if (contributions == null || contributions.Count == 0)
        {
            SetSolidGradient(result, Color.black, ref colorKeys, ref alphaKeys);
            return;
        }

        if (contributions.Count == 1 && TryBuildSingleContributionGradientFromCache(unit, contributions[0], result, keyTimes, ref colorKeys, ref alphaKeys))
            return;

        CollectContributionTimes(contributions, keyTimes);
        EnsureGradientKeyArraySizes(keyTimes.Count, ref colorKeys, ref alphaKeys);
        float weightScale = ComputeContributionWeightScale(contributions);
        GradientMode mode = GradientMode.Blend;

        for (int i = 0; i < contributions.Count; i++)
        {
            if (contributions[i].gradient != null)
            {
                mode = contributions[i].gradient.mode;
                break;
            }
        }

        for (int i = 0; i < keyTimes.Count; i++)
        {
            float time = keyTimes[i];
            Color color = new Color(0f, 0f, 0f, 1f);

            for (int c = 0; c < contributions.Count; c++)
            {
                WeightedGradientContribution contribution = contributions[c];
                Color sample = EvaluateBeamLengthColor(contribution.gradient, time);
                Color tinted = UnifiedStageGradientUtility.MultiplyRgb(sample, contribution.tint);
                color += tinted * contribution.weight * weightScale;
            }

            color = UnifiedStageGradientUtility.ForceOpaque(color);
            colorKeys[i] = new GradientColorKey(color, time);
            alphaKeys[i] = new GradientAlphaKey(1f, time);
        }

        result.SetKeys(colorKeys, alphaKeys);
        result.mode = mode;
    }

    private static void LerpGradientInto(
        Gradient result,
        Gradient from,
        Gradient to,
        float t,
        List<float> keyTimes,
        ref GradientColorKey[] colorKeys,
        ref GradientAlphaKey[] alphaKeys)
    {
        if (result == null || from == null || to == null)
            return;

        t = Mathf.Clamp01(t);
        CollectGradientTimes(from, to, keyTimes);
        EnsureGradientKeyArraySizes(keyTimes.Count, ref colorKeys, ref alphaKeys);

        for (int i = 0; i < keyTimes.Count; i++)
        {
            float time = keyTimes[i];
            Color color = Color.Lerp(from.Evaluate(time), to.Evaluate(time), t);
            color = UnifiedStageGradientUtility.ForceOpaque(color);
            colorKeys[i] = new GradientColorKey(color, time);
            alphaKeys[i] = new GradientAlphaKey(1f, time);
        }

        result.SetKeys(colorKeys, alphaKeys);
        result.mode = to.mode;
    }

    private static void SetSolidGradient(
        Gradient result,
        Color color,
        ref GradientColorKey[] colorKeys,
        ref GradientAlphaKey[] alphaKeys)
    {
        color = UnifiedStageGradientUtility.ForceOpaque(color);
        EnsureGradientKeyArraySizes(2, ref colorKeys, ref alphaKeys);
        colorKeys[0] = new GradientColorKey(color, 0f);
        colorKeys[1] = new GradientColorKey(color, 1f);
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);
        result.SetKeys(colorKeys, alphaKeys);
        result.mode = GradientMode.Blend;
    }

    private static void EnsureGradientKeyArraySizes(
        int count,
        ref GradientColorKey[] colorKeys,
        ref GradientAlphaKey[] alphaKeys)
    {
        if (colorKeys == null || colorKeys.Length != count)
            colorKeys = new GradientColorKey[count];
        if (alphaKeys == null || alphaKeys.Length != count)
            alphaKeys = new GradientAlphaKey[count];
    }

    private static bool TryBuildSingleContributionGradientFromCache(
        SLMUnit unit,
        WeightedGradientContribution contribution,
        Gradient result,
        List<float> keyTimes,
        ref GradientColorKey[] colorKeys,
        ref GradientAlphaKey[] alphaKeys)
    {
        if (unit == null || result == null)
            return false;

        EnsureBeamLengthCache(unit, contribution.gradient, keyTimes);
        int count = unit.cachedBeamLengthTimes != null ? unit.cachedBeamLengthTimes.Length : 0;
        if (count <= 0)
            return false;

        EnsureGradientKeyArraySizes(count, ref colorKeys, ref alphaKeys);

        float weightScale = Mathf.Min(contribution.weight, 1f);
        Color weightedTint = contribution.tint * weightScale;
        for (int i = 0; i < count; i++)
        {
            Color color = UnifiedStageGradientUtility.MultiplyRgb(unit.cachedBeamLengthColors[i], weightedTint);
            float time = unit.cachedBeamLengthTimes[i];
            colorKeys[i] = new GradientColorKey(color, time);
            alphaKeys[i] = new GradientAlphaKey(1f, time);
        }

        result.SetKeys(colorKeys, alphaKeys);
        result.mode = unit.cachedBeamLengthGradientMode;
        return true;
    }

    private static void EnsureBeamLengthCache(SLMUnit unit, Gradient gradient, List<float> keyTimes)
    {
        bool useContentHash = !Application.isPlaying;
        int hash = useContentHash ? GetGradientContentHash(gradient) : 0;
        if (unit.cachedBeamLengthGradient == gradient &&
            (!useContentHash || unit.cachedBeamLengthGradientHash == hash) &&
            unit.cachedBeamLengthTimes != null &&
            unit.cachedBeamLengthColors != null &&
            unit.cachedBeamLengthAlphas != null)
        {
            return;
        }

        unit.cachedBeamLengthGradient = gradient;
        unit.cachedBeamLengthGradientHash = hash;
        unit.cachedBeamLengthGradientMode = gradient != null ? gradient.mode : GradientMode.Blend;

        CollectGradientTimes(gradient, null, keyTimes);
        int count = keyTimes.Count;

        if (unit.cachedBeamLengthTimes == null || unit.cachedBeamLengthTimes.Length != count)
        {
            unit.cachedBeamLengthTimes = new float[count];
            unit.cachedBeamLengthColors = new Color[count];
            unit.cachedBeamLengthAlphas = new float[count];
        }

        for (int i = 0; i < count; i++)
        {
            float time = keyTimes[i];
            unit.cachedBeamLengthTimes[i] = time;
            unit.cachedBeamLengthColors[i] = EvaluateBeamLengthColor(gradient, time);
            unit.cachedBeamLengthAlphas[i] = 1f;
        }
    }

    private static float ComputeContributionWeightScale(List<WeightedGradientContribution> contributions)
    {
        float totalWeight = 0f;
        for (int i = 0; i < contributions.Count; i++)
            totalWeight += Mathf.Max(0f, contributions[i].weight);

        return totalWeight > 1f ? 1f / totalWeight : 1f;
    }

    private static Color EvaluateBeamLengthColor(Gradient gradient, float time)
    {
        if (gradient != null)
            return UnifiedStageGradientUtility.ForceOpaque(gradient.Evaluate(time));

        return UnifiedStageGradientUtility.ForceOpaque(Color.Lerp(Color.white, Color.black, Mathf.Clamp01(time)));
    }

    private static int GetGradientContentHash(Gradient gradient)
    {
        if (gradient == null)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)gradient.mode;
            foreach (GradientColorKey key in gradient.colorKeys)
            {
                hash = hash * 31 + key.time.GetHashCode();
                hash = hash * 31 + UnifiedStageGradientUtility.ForceOpaque(key.color).GetHashCode();
            }

            return hash;
        }
    }

    private static void CollectContributionTimes(List<WeightedGradientContribution> contributions, List<float> times)
    {
        ResetGradientTimes(times);

        for (int i = 0; i < contributions.Count; i++)
        {
            Gradient gradient = contributions[i].gradient;
            if (gradient == null)
            {
                AddUniqueGradientTime(times, 0f);
                AddUniqueGradientTime(times, 1f);
                continue;
            }

            foreach (GradientColorKey key in gradient.colorKeys)
                AddUniqueGradientTime(times, key.time);
        }

        times.Sort();
    }

    private static void CollectGradientTimes(Gradient a, Gradient b, List<float> times)
    {
        ResetGradientTimes(times);
        AddGradientTimes(a, times);
        AddGradientTimes(b, times);
        times.Sort();
    }

    private static void ResetGradientTimes(List<float> times)
    {
        times.Clear();
        times.Add(0f);
        times.Add(1f);
    }

    private static void AddGradientTimes(Gradient gradient, List<float> times)
    {
        if (gradient == null)
            return;

        foreach (GradientColorKey key in gradient.colorKeys)
            AddUniqueGradientTime(times, key.time);
    }

    private static void AddUniqueGradientTime(List<float> times, float time)
    {
        time = Mathf.Clamp01(time);
        for (int i = 0; i < times.Count; i++)
        {
            if (Mathf.Abs(times[i] - time) <= 0.0001f)
                return;
        }

        times.Add(time);
    }

    private static void CaptureCurrentGradient(SLMUnit unit)
    {
        if (unit == null)
            return;

        if (unit.frozenGradient == null)
            unit.frozenGradient = UnifiedStageGradientUtility.CreateSolidGradient(Color.black);

        if (unit.currentGradient != null)
        {
            UnifiedStageGradientUtility.CopyGradientInto(unit.currentGradient, unit.frozenGradient);
            return;
        }

        Color fallbackColor = unit.targetLight != null ? unit.targetLight.color : Color.black;
        if (unit.targetLight != null)
        {
            var vlb = unit.targetLight.GetComponent<VLB.VolumetricLightBeamHD>();
            if (vlb != null)
            {
                if (vlb.colorMode == VLB.ColorMode.Gradient && vlb.colorGradient != null)
                {
                    UnifiedStageGradientUtility.CopyGradientInto(vlb.colorGradient, unit.frozenGradient);
                    return;
                }

                fallbackColor = vlb.colorFlat;
            }
        }

        SetSolidGradient(unit.frozenGradient, fallbackColor, ref unit.gradientColorKeys, ref unit.gradientAlphaKeys);
    }

    private void ApplyLightMode(SLMUnit unit, VLB.VolumetricLightBeamHD vlb, StageLightMode mode)
    {
        if (unit == null || unit.targetLight == null) return;
        if (unit.hasAppliedLightMode && unit.appliedLightMode == mode) return;

        switch (mode)
        {
            case StageLightMode.VolumetricSpot:
                RestoreLightEnabledAfterLaserMesh(unit);
                unit.targetLight.type = LightType.Spot;
                if (vlb != null) vlb.enabled = true;
                break;

            case StageLightMode.Spot:
                RestoreLightEnabledAfterLaserMesh(unit);
                unit.targetLight.type = LightType.Spot;
                if (vlb != null) vlb.enabled = false;
                break;

            case StageLightMode.Point:
                RestoreLightEnabledAfterLaserMesh(unit);
                unit.targetLight.type = LightType.Point;
                if (vlb != null) vlb.enabled = false;
                break;

            case StageLightMode.LaserMesh:
            case StageLightMode.FannedLaser:
                if (!unit.lightDisabledByLaserMesh)
                {
                    unit.lightEnabledBeforeLaserMesh = unit.targetLight.enabled;
                    unit.lightDisabledByLaserMesh = true;
                }

                unit.targetLight.enabled = false;
                if (vlb != null) vlb.enabled = false;
                break;
        }

        unit.appliedLightMode = mode;
        unit.hasAppliedLightMode = true;
    }

    private static void RestoreLightEnabledAfterLaserMesh(SLMUnit unit)
    {
        if (unit == null || unit.targetLight == null || !unit.lightDisabledByLaserMesh)
            return;

        unit.targetLight.enabled = unit.lightEnabledBeforeLaserMesh;
        unit.lightDisabledByLaserMesh = false;
    }

    private static float CalculateVlbSideSoftness(float softness)
    {
        float t = Mathf.Clamp01(softness / 100f);
        return Mathf.Lerp(MinVlbSideSoftness, MaxVlbSideSoftness, t);
    }

    private static float CalculateInnerSpotAngle(float outerSpotAngle, float softness)
    {
        float t = Mathf.Clamp01(softness / 100f);
        return Mathf.Clamp(outerSpotAngle * (1f - t), 0f, outerSpotAngle);
    }

    private static float CalculateMotionCycleT(ActiveClipInfo clip, float unitEt)
    {
        float cyclePeriod = UnifiedStageBehaviour.GetMotionCyclePeriod(clip.mode, clip.speed);
        if (cyclePeriod > 0.0001f)
        {
            float raw = unitEt / cyclePeriod;
            return raw - Mathf.Floor(raw);
        }

        return clip.normalizedClipTime;
    }

    // ==========================================
    //  角度計算（per-unit，含 Random 兩段式）
    //  直接輸出 pan/tilt 角度，包含 staticOffset
    // ==========================================
    private Vector2 CalculateAnglesForUnit(
        RotationMode mode, float speed, float range,
        float et, int index, Vector2 staticOffset, float randomStrength)
    {
        float p = 0, t = 0;

        switch (mode)
        {
            case RotationMode.Static:
                p = staticOffset.x;
                t = staticOffset.y;
                break;

            case RotationMode.Scan:
                p = Mathf.Sin(et * speed) * range + staticOffset.x;
                t = staticOffset.y;
                break;

            case RotationMode.Circle:
                // Euler 空間圓：pan/tilt 各自 sin/cos，相位差 90°
                // staticOffset 控制圓心，range 是角度半徑，與軸向設定無關
                float theta = et * speed * 20f * Mathf.Deg2Rad;
                p = Mathf.Sin(theta) * range + staticOffset.x;
                t = Mathf.Cos(theta) * range + staticOffset.y;
                break;

            case RotationMode.VerticalSwing:
                p = staticOffset.x;
                t = Mathf.Sin(et * speed) * range + staticOffset.y;
                break;

            case RotationMode.Random:
                float initP = (Mathf.PerlinNoise(0f, index * 0.5f) - 0.5f) * 2f * range + staticOffset.x;
                float initT = (Mathf.PerlinNoise(index * 0.5f, 0f) - 0.5f) * 2f * range + staticOffset.y;
                float fullP = (Mathf.PerlinNoise(et * speed, index * 0.5f) - 0.5f) * 2f * range + staticOffset.x;
                float fullT = (Mathf.PerlinNoise(index * 0.5f, et * speed) - 0.5f) * 2f * range + staticOffset.y;
                p = Mathf.Lerp(initP, fullP, randomStrength);
                t = Mathf.Lerp(initT, fullT, randomStrength);
                break;

            case RotationMode.Cross:
                float panSide = (index % 2 == 0) ? 1f : -1f;
                p = (90f * panSide) + staticOffset.x;
                t = (Mathf.Sin(et * speed) * range) + staticOffset.y;
                break;
        }

        return new Vector2(p, t);
    }

    // ==========================================
    //  Circle 模式：幾何圓錐方向式 solver
    //  適用於 beam-along-localY 燈具（tiltTransform.up 為光束方向）
    //  staticOffset 定義圓心，range=角度半徑，speed=繞圓速度
    //  輸出經 DeltaAngle 正規化後，由 SmoothDampAngle 連續追蹤
    // ==========================================
    private Vector2 CalculateCircleAngles(float et, float speed, float range, Vector2 staticOffset)
    {
        Vector3 panAxis  = GetSafeAxis(panRotationVector, Vector3.up);
        Vector3 tiltAxis = GetSafeAxis(GetProcessedTiltRotationVector(), Vector3.left);

        // 1. 圓心方向：beam(P0, T0) = AngleAxis(P0, panAxis) * AngleAxis(T0, tiltAxis) * Vector3.up
        Quaternion centerPanQ  = Quaternion.AngleAxis(staticOffset.x, panAxis);
        Quaternion centerTiltQ = Quaternion.AngleAxis(staticOffset.y, tiltAxis);
        Vector3 centerDir = centerPanQ * centerTiltQ * Vector3.up;

        // 2. 圓錐邊緣起點：tilt 多偏移 range 度
        Quaternion startTiltQ = Quaternion.AngleAxis(staticOffset.y + range, tiltAxis);
        Vector3 startEdge = centerPanQ * startTiltQ * Vector3.up;

        // 3. 繞 centerDir 旋轉 theta 度，產生幾何等速圓
        float thetaDeg = et * speed * 20f;
        Vector3 finalDir = Quaternion.AngleAxis(thetaDeg, centerDir) * startEdge;

        // 4. 反解 pan：以「T=90°時的水平投影方向」為 pan=0° 參考
        //    不需寫死就能適用於任意 tiltAxis
        Vector3 panRef = Vector3.ProjectOnPlane(
            Quaternion.AngleAxis(90f, tiltAxis) * Vector3.up,
            panAxis
        ).normalized;

        Vector3 hProj = Vector3.ProjectOnPlane(finalDir, panAxis);
        float pan;
        if (hProj.sqrMagnitude < 0.0001f)
            pan = staticOffset.x; // 近天頂/底部：保持參考 pan
        else
            pan = SignedAngleOnAxis(panRef, hProj, panAxis);

        // 5. 反解 tilt：消除 pan 後，從 Vector3.up（T=0 的光束方向）量起
        Quaternion undoPan = Quaternion.AngleAxis(-pan, panAxis);
        Vector3 undone = undoPan * finalDir;
        float tilt = SignedAngleOnAxis(Vector3.up, undone, tiltAxis);

        return new Vector2(pan, tilt);
    }

    // ==========================================
    //  顏色計算（依 ColorSampleMode 分支）
    // ==========================================
    private Color ComputeClipColor(ActiveClipInfo clip, SLMUnit unit, float rootTime, float unitEt, float unitDelay)
    {
        // FreezeFrame: 維持原有邏輯，不受 colorSampleMode 影響
        if (clip.isFreezeFrame)
        {
            Color fc = clip.freezeUseClipGradient
                ? ((clip.gradient != null) ? clip.gradient.Evaluate(clip.normalizedClipTime) : Color.white)
                : unit.frozenColor;

            return UnifiedStageGradientUtility.ForceOpaque(fc * clip.globalColor);
        }

        Color baseColor;
        switch (clip.colorSampleMode)
        {
            case ColorSampleMode.MotionCycle:
            {
                float cyclePeriod = UnifiedStageBehaviour.GetMotionCyclePeriod(clip.mode, clip.speed);
                float t;
                if (cyclePeriod > 0.0001f)
                {
                    t = unitEt / cyclePeriod;
                    t = t - Mathf.Floor(t);
                }
                else
                {
                    t = clip.normalizedClipTime;
                }

                baseColor = (clip.gradient != null) ? clip.gradient.Evaluate(t) : Color.white;
                break;
            }

            case ColorSampleMode.ClipProgress:
            {
                float delayShift = (clip.clipDuration > 0.0001f) ? unitDelay / clip.clipDuration : 0f;
                float rawPhase   = clip.normalizedClipTime - delayShift;
                float window     = Mathf.Max(1f - delayShift, 0.0001f);
                float phase      = Mathf.Clamp01(rawPhase / window);

                baseColor = (clip.gradient != null) ? clip.gradient.Evaluate(phase) : Color.white;
                break;
            }

            case ColorSampleMode.BeatGradient:
            {
                float beatTime = (clip.beatTimeRef == BeatTimeReference.ClipLocal) ? clip.effectiveTime : rootTime;
                float beatLen  = 60f / Mathf.Max(clip.bpm, 0.001f);
                float beatOffset = ComputeBeatTimeOffset(clip, unit);
                float t        = (beatTime - beatOffset + clip.beatPhaseOffset) / beatLen;

                t = t - Mathf.Floor(t);
                if (t < 0f) t += 1f;

                baseColor = (clip.gradient != null) ? clip.gradient.Evaluate(t) : Color.white;
                break;
            }

            case ColorSampleMode.BeatSnap:
            {
                if (clip.beatSnapColors == null || clip.beatSnapColors.Length == 0)
                    return UnifiedStageGradientUtility.ForceOpaque(Color.white * clip.globalColor);

                float beatTime = (clip.beatTimeRef == BeatTimeReference.ClipLocal) ? clip.effectiveTime : rootTime;
                float beatLen  = 60f / Mathf.Max(clip.bpm, 0.001f);
                float beatPosition = (beatTime + clip.beatPhaseOffset) / beatLen;
                int beatIdx    = Mathf.FloorToInt(beatPosition);
                int indexOffset = ComputeBeatSnapIndexOffset(clip, unit);

                if (beatIdx < 0) beatIdx = 0;

                int colorIdx = PositiveModulo(beatIdx + indexOffset, clip.beatSnapColors.Length);
                baseColor = clip.beatSnapColors[colorIdx];

                float transitionTime = Mathf.Max(clip.beatSnapTransitionTime, 0f);
                if (transitionTime > 0f && clip.beatSnapColors.Length > 1)
                {
                    float transitionLen = Mathf.Min(transitionTime, beatLen);
                    float beatLocalTime = (beatPosition - Mathf.Floor(beatPosition)) * beatLen;
                    float transitionStart = beatLen - transitionLen;

                    if (beatLocalTime >= transitionStart)
                    {
                        int nextColorIdx = PositiveModulo(beatIdx + 1 + indexOffset, clip.beatSnapColors.Length);
                        float lerpT = Mathf.InverseLerp(transitionStart, beatLen, beatLocalTime);
                        baseColor = Color.Lerp(baseColor, clip.beatSnapColors[nextColorIdx], lerpT);
                    }
                }
                break;
            }

            case ColorSampleMode.AlongAudioSource:
            {
                float energy = ((curLow + curMid + curHigh) / 3f) * clip.sensitivity;
                float t      = Mathf.Clamp01(energy);

                baseColor = (clip.gradient != null) ? clip.gradient.Evaluate(t) : Color.white;
                break;
            }

            default:
                baseColor = (clip.gradient != null) ? clip.gradient.Evaluate(clip.normalizedClipTime) : Color.white;
                break;
        }

        return UnifiedStageGradientUtility.ForceOpaque(baseColor * clip.globalColor);
    }

    private static float ComputeBeatTimeOffset(ActiveClipInfo clip, SLMUnit unit)
    {
        if (unit == null) return 0f;

        float groupOffset = 0f;
        if (clip.beatGroupDelayFactor > 0f)
        {
            float normalizedGroup = (unit.groupCount > 1)
                ? (float)unit.groupIndex / (unit.groupCount - 1)
                : 0f;
            float gv = EvaluateDelayCurve(clip.beatGroupDelayCurve, normalizedGroup);
            groupOffset = gv * clip.beatGroupDelayFactor;
        }

        float lightOffset = 0f;
        if (clip.beatLightDelayFactor > 0f)
        {
            float normalizedInGroup = (unit.groupSize > 1)
                ? (float)unit.indexInGroup / (unit.groupSize - 1)
                : 0f;
            float lv = EvaluateDelayCurve(clip.beatLightDelayCurve, normalizedInGroup);
            lightOffset = lv * clip.beatLightDelayFactor;
        }

        return groupOffset + lightOffset;
    }

    private static int ComputeBeatSnapIndexOffset(ActiveClipInfo clip, SLMUnit unit)
    {
        if (unit == null) return 0;

        int groupOffset = ComputeRankStepOffset(
            unit.groupIndex,
            unit.groupCount,
            clip.beatGroupDelayCurve,
            clip.beatGroupDelayFactor);

        int lightOffset = ComputeRankStepOffset(
            unit.indexInGroup,
            unit.groupSize,
            clip.beatLightDelayCurve,
            clip.beatLightDelayFactor);

        return groupOffset + lightOffset;
    }

    private static int ComputeRankStepOffset(int index, int count, AnimationCurve curve, float step)
    {
        if (step <= 0f || count <= 1) return 0;

        int rank = ComputeCurveRank(index, count, curve);
        return Mathf.FloorToInt(rank / step);
    }

    private static int ComputeCurveRank(int index, int count, AnimationCurve curve)
    {
        if (count <= 1) return 0;

        int safeIndex = Mathf.Clamp(index, 0, count - 1);
        float currentValue = EvaluateDelayCurveAtIndex(curve, safeIndex, count);
        int rank = 0;

        for (int i = 0; i < count; i++)
        {
            if (i == safeIndex) continue;

            float value = EvaluateDelayCurveAtIndex(curve, i, count);
            if (value < currentValue || (Mathf.Approximately(value, currentValue) && i < safeIndex))
                rank++;
        }

        return rank;
    }

    private static float EvaluateDelayCurveAtIndex(AnimationCurve curve, int index, int count)
    {
        float normalized = (count > 1) ? (float)index / (count - 1) : 0f;
        return EvaluateDelayCurve(curve, normalized);
    }

    private static float EvaluateDelayCurve(AnimationCurve curve, float normalized)
    {
        float t = Mathf.Clamp01(normalized);
        return curve != null ? curve.Evaluate(t) : t;
    }

    private static int PositiveModulo(int value, int length)
    {
        int result = value % length;
        return result < 0 ? result + length : result;
    }

    // ==========================================
    //  頻譜處理
    // ==========================================
    private void ProcessSpectrum(float smooth, bool isTimeJump, float dt)
    {
        if (!enableColorUpdate) return;

        float rL = GetAverage(0, 2);
        float rM = GetAverage(3, 20);
        float rH = GetAverage(21, 100);

        if (Application.isPlaying)
        {
            rL *= 15f;
            rM *= 15f;
            rH *= 15f;
        }

        if (isTimeJump)
        {
            curLow = 0;
            curMid = 0;
            curHigh = 0;
            lowMax = 0.01f;
            midMax = 0.01f;
            highMax = 0.01f;
        }

        lowMax  = Mathf.Max(lowMax  * 0.99f, rL, 0.005f);
        midMax  = Mathf.Max(midMax  * 0.99f, rM, 0.005f);
        highMax = Mathf.Max(highMax * 0.99f, rH, 0.005f);

        curLow  = Mathf.Lerp(curLow,  Mathf.Clamp01(rL / lowMax),  dt * smooth * 15f);
        curMid  = Mathf.Lerp(curMid,  Mathf.Clamp01(rM / midMax),  dt * smooth * 15f);
        curHigh = Mathf.Lerp(curHigh, Mathf.Clamp01(rH / highMax), dt * smooth * 15f);
    }

    // ==========================================
    //  LookAt 計算
    // ==========================================
    private Vector2 CalculateLookAtAngles(Transform pR, Transform tR, Transform target)
    {
        if (pR == null || tR == null || target == null || pR.parent == null)
            return new Vector2(panOffset, verticalBaseOffset + tiltOffset);

        Vector3 panAxis = GetSafeAxis(panRotationVector, Vector3.up);
        Vector3 tiltAxis = GetSafeAxis(GetProcessedTiltRotationVector(), Vector3.left);

        Vector3 targetInPanParent = pR.parent.InverseTransformPoint(target.position) - pR.localPosition;
        if (targetInPanParent.sqrMagnitude < 0.000001f)
            return new Vector2(panOffset, verticalBaseOffset + tiltOffset);

        float pan = SignedAngleOnAxis(Vector3.forward, targetInPanParent, panAxis);

        Quaternion undoPan = Quaternion.AngleAxis(-pan, panAxis);
        Vector3 targetInPanSpace = undoPan * targetInPanParent;
        Vector3 targetFromTiltPivot = targetInPanSpace - tR.localPosition;
        float tilt = SignedAngleOnAxis(Vector3.forward, targetFromTiltPivot, tiltAxis);

        // invertVerticalTracking 手動控制方向。
        // SignedAngleOnAxis 已經自動考慮 tiltAxis 方向（軸反轉則角度符號同步反轉），
        // 因此不需額外補償，不論 Vector3.left 或 Vector3.right 邏輯均正確。
        tilt = invertVerticalTracking ? -tilt : tilt;

        return new Vector2(pan + panOffset, tilt + verticalBaseOffset + tiltOffset);
    }

    // ==========================================
    //  Tilt 軸內部處理
    // ==========================================
    private Vector3 GetProcessedTiltRotationVector()
    {
        return Vector3.Scale(tiltRotationVector, new Vector3(1f, 1f, 1f));
    }

    private static Vector3 GetSafeAxis(Vector3 axis, Vector3 fallback)
    {
        return axis.sqrMagnitude > 0.000001f ? axis.normalized : fallback;
    }

    private static float SignedAngleOnAxis(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 fromOnPlane = Vector3.ProjectOnPlane(from, axis);
        Vector3 toOnPlane = Vector3.ProjectOnPlane(to, axis);

        if (fromOnPlane.sqrMagnitude < 0.000001f || toOnPlane.sqrMagnitude < 0.000001f)
            return 0f;

        return Vector3.SignedAngle(fromOnPlane, toOnPlane, axis);
    }

    private float GetAverage(int s, int e)
    {
        float sum = 0;
        for (int i = s; i <= e; i++) sum += spectrum[i];
        return sum / (e - s + 1);
    }

}
