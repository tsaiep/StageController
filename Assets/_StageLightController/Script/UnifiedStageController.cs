using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class UnifiedStageController : MonoBehaviour
{
    private const float MinVlbSideSoftness = 0.0001f;
    private const float MaxVlbSideSoftness = 10f;

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
        [InspectorName("Point Light")] Point
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
            Color unitColor = Color.black;
            float totalPan = 0f, totalTilt = 0f;
            float targetModeWeight = 0f;

            // --- FreezeFrame Rising Edge: 快取此 unit 的現在狀態 ---
            if (freezeJustActivated)
            {
                unit.frozenPan   = unit.curPan;
                unit.frozenTilt  = unit.curTilt;
                unit.frozenColor = (unit.targetLight != null) ? unit.targetLight.color : Color.black;
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
                    unitColor += clipColor * clip.weight;
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

                    Vector2 angles;
                    if (clip.mode == RotationMode.Circle)
                    {
                        // Circle：幾何圓錐方向式 solver（正確等速圓）
                        angles = CalculateCircleAngles(adjustedEt, clip.speed, clip.range, clip.staticOffset);
                    }
                    else
                    {
                        angles = CalculateAnglesForUnit(
                            clip.mode, clip.speed, clip.range,
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

            // ===== 燈光 =====
            if (unit.targetLight != null)
            {
                unit.targetLight.intensity = baseIntensity * mixedInten;
                unit.targetLight.range = Mathf.Max(0.01f, mixedLightRange);

                Color targetColor = unitColor;
                Color finalColor = isTimeJump ? targetColor : Color.Lerp(unit.targetLight.color, targetColor, dt * 25f);

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
                    vlb.colorFromLight = false;
                    vlb.colorFlat = finalColor;
                    vlb.spotAngle = mixedBeamAngle;
                    vlb.sideSoftness = CalculateVlbSideSoftness(mixedSoftness);
                    if (vlb.enabled)
                        vlb.UpdateAfterManualPropertyChange();
                }

                var cookie = unit.targetLight.GetComponent<VLB.VolumetricCookieHD>();
                if (cookie != null) cookie.enabled = activeScatter;
            }
        }
    }

    private void ApplyLightMode(SLMUnit unit, VLB.VolumetricLightBeamHD vlb, StageLightMode mode)
    {
        if (unit == null || unit.targetLight == null) return;
        if (unit.hasAppliedLightMode && unit.appliedLightMode == mode) return;

        switch (mode)
        {
            case StageLightMode.VolumetricSpot:
                unit.targetLight.type = LightType.Spot;
                if (vlb != null) vlb.enabled = true;
                break;

            case StageLightMode.Spot:
                unit.targetLight.type = LightType.Spot;
                if (vlb != null) vlb.enabled = false;
                break;

            case StageLightMode.Point:
                unit.targetLight.type = LightType.Point;
                if (vlb != null) vlb.enabled = false;
                break;
        }

        unit.appliedLightMode = mode;
        unit.hasAppliedLightMode = true;
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

            return fc * clip.globalColor;
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
                    return Color.white * clip.globalColor;

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

        return baseColor * clip.globalColor;
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
        return Vector3.Scale(tiltRotationVector, new Vector3(-1f, 1f, 1f));
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
