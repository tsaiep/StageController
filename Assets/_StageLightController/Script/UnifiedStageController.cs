using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class UnifiedStageController : MonoBehaviour
{
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

    /// <summary>
    /// 靜止模式顏色動畫完成後的行為
    /// </summary>
    public enum ColorFinishMode
    {
        [InspectorName("夾緊（停在漸層末端）")] Clamp,
        [InspectorName("循環（回到漸層起點）")] Loop
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

    [Header("Debug")]
    [Tooltip("在 Scene 視窗繪製每顆燈光的局部平面")] public bool debugDrawLocalPlane = false;
    [Tooltip("局部平面繪製大小")] public float debugPlaneSize = 0.5f;

    // --- 內部狀態 ---
    private float[] spectrum = new float[256];
    private float lowMax = 0.1f, midMax = 0.1f, highMax = 0.1f;
    private float curLow, curMid, curHigh;

    // Per-unit 靜態中心方向快取（pan-parent 局部空間），由 UpdateStage 寫入，由 OnDrawGizmos 讀取
    private Vector3[] _dbgCenterDir;

    public float GetLowEnergy() { return curLow; }
    public float GetMidEnergy() { return curMid; }
    public float GetHighEnergy() { return curHigh; }
    public float LowEnergy => curLow;
    public float MidEnergy => curMid;
    public float HighEnergy => curHigh;

    // ==========================================
    //  主入口：由 Mixer 呼叫
    // ==========================================
    public void UpdateStage(
        List<ActiveClipInfo> clips, float[] spec, bool isTimeJump,
        float mixedInten, float mixedBase, float mixedSense, float mixedSmooth,
        float mixedBeamAngle, bool activeScatter,
        float totalMotionWeight, float weightedEffectiveTime,
        bool freezeJustActivated)
    {
        // 1. 頻譜分析（全域）
        System.Array.Copy(spec, spectrum, 256);
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;
        ProcessSpectrum(mixedSmooth, mixedSense, isTimeJump, dt);

        float energy = ((curLow + curMid + curHigh) / 3f) * mixedSense;
        float normalizedEnergy = Mathf.Clamp01(energy);

        // 2. 物理 dt
        bool isMoving = enableMotion && totalMotionWeight > 0.01f;
        float physicalDt = Application.isPlaying ? Time.unscaledDeltaTime : 0.02f;
        bool forceSnap = !isMoving || isTimeJump;

        if (forceSnap)
        {
            physicalDt = 0f;
            if (slmUnits != null)
                foreach (var u in slmUnits) { if (u != null) { u.velPan = 0; u.velTilt = 0; } }
        }
        else if (Application.isPlaying)
        {
            physicalDt = Mathf.Max(physicalDt, 0.012f);
        }

        // 3. Per-unit 處理
        if (slmUnits == null) return;
        int unitCount = slmUnits.Length;

        // 確保 debug 快取陣列大小
        if (debugDrawLocalPlane && (_dbgCenterDir == null || _dbgCenterDir.Length != unitCount))
            _dbgCenterDir = new Vector3[unitCount];

        for (int ui = 0; ui < unitCount; ui++)
        {
            var unit = slmUnits[ui];
            if (unit == null) continue;



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

                // ===== 顏色（動態循環取樣）=====
                if (enableColorUpdate)
                {
                    Color clipColor;
                    if (clip.isFreezeFrame)
                    {
                        if (clip.freezeUseClipGradient)
                        {
                            // FreezeFrame + useClipGradient: 以 Clip 自身 Gradient 取色，
                            // 以 normalizedClipTime (0~1) 為取樣點（如同靜止模式），
                            // 並透過 weight 與前後 Clip 正常 Blending
                            clipColor = (clip.gradient != null)
                                ? clip.gradient.Evaluate(clip.normalizedClipTime)
                                : Color.white;
                        }
                        else
                        {
                            // FreezeFrame 預設: 使用快取的凍結顏色
                            clipColor = unit.frozenColor;
                        }
                    }
                    else if (clip.mode == RotationMode.Static)
                    {
                        // ── Static 模式：延遲後在剩餘窗口內 normalize，確保每顆燈都能走完完整漸層 ──
                        // delayShift: 延遲占 Clip 總長的比例（0~1）
                        // 可用窗口: [delayShift, 1]，長度 = window = 1 - delayShift
                        // 將 normalizedClipTime 在此窗口內 remap 成 0~1
                        float delayShift = (clip.clipDuration > 0.0001f) ? unitDelay / clip.clipDuration : 0f;
                        float rawPhase = clip.normalizedClipTime - delayShift;
                        float window = Mathf.Max(1f - delayShift, 0.0001f);

                        float phase;
                        switch (clip.staticColorFinishMode)
                        {
                            case ColorFinishMode.Loop:
                                // 延遲期間（rawPhase < 0）固定在漸層起點
                                // 之後在可用窗口內從 0 跑到 1 再循環
                                if (rawPhase < 0f)
                                    phase = 0f;
                                else
                                {
                                    float t = rawPhase / window;
                                    phase = t - Mathf.Floor(t);
                                }
                                break;
                            default: // Clamp
                                // rawPhase < 0  → 固定在起點（0）
                                // rawPhase 在 [0, window] → 線性走至 1
                                // rawPhase > window → 停在末端（1）
                                phase = Mathf.Clamp01(rawPhase / window);
                                break;
                        }

                        clipColor = (clip.gradient != null) ? clip.gradient.Evaluate(phase) : Color.white;
                    }
                    else
                    {
                        float cyclePeriod = UnifiedStageBehaviour.GetMotionCyclePeriod(clip.mode, clip.speed);
                        float colorPhase;
                        if (cyclePeriod > 0.0001f)
                        {
                            colorPhase = unitEt / cyclePeriod;
                            colorPhase = colorPhase - Mathf.Floor(colorPhase); // frac（處理負數）
                        }
                        else
                        {
                            colorPhase = clip.normalizedClipTime;
                        }
                        clipColor = (clip.gradient != null) ? clip.gradient.Evaluate(colorPhase) : Color.white;
                    }
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

            // 快取這張 Clip 的静態中心方向，供 Gizmo 使用
            if (debugDrawLocalPlane && _dbgCenterDir != null && ui < _dbgCenterDir.Length && clips.Count > 0)
            {
                // 找權重最大的 Clip
                var domClip = clips[0];
                for (int ci = 1; ci < clips.Count; ci++)
                    if (clips[ci].weight > domClip.weight) domClip = clips[ci];
                _dbgCenterDir[ui] = ComputeStaticCenterDir(domClip.mode, ui, domClip.staticOffset);
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

                unit.panTransform.localRotation = Quaternion.AngleAxis(unit.curPan, GetSafeAxis(panRotationVector, Vector3.up));
                unit.tiltTransform.localRotation = Quaternion.AngleAxis(unit.curTilt, GetSafeAxis(tiltRotationVector, Vector3.left));
            }

            // ===== 燈光 =====
            if (unit.targetLight != null)
            {
                float fI = Mathf.Lerp(mixedBase, 1.0f, normalizedEnergy);
                unit.targetLight.intensity = fI * baseIntensity * mixedInten;

                Color targetColor = unitColor;
                Color finalColor = isTimeJump ? targetColor : Color.Lerp(unit.targetLight.color, targetColor, dt * 25f);

                unit.targetLight.color = finalColor;
                unit.targetLight.spotAngle = mixedBeamAngle;

                var vlb = unit.targetLight.GetComponent<VLB.VolumetricLightBeamHD>();
                if (vlb != null)
                {
                    vlb.colorFromLight = false;
                    vlb.colorFlat = finalColor;
                    vlb.spotAngle = mixedBeamAngle;
                    vlb.UpdateAfterManualPropertyChange();
                }

                var cookie = unit.targetLight.GetComponent<VLB.VolumetricCookieHD>();
                if (cookie != null) cookie.enabled = activeScatter;
            }
        }
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
                t = 45f + staticOffset.y;
                break;

            case RotationMode.Circle:
                // Euler 空間圓：pan/tilt 各自 sin/cos，相位差 90°
                // staticOffset 控制圓心， range 是角度半徑，與軸向設定無關
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
                t = (Mathf.Sin(et * speed) * range) + 35f + staticOffset.y + 10f;
                break;
        }

        return new Vector2(p, t);
    }

    // ==========================================
    //  Circle 模式：幾何圓錐方向式 solver
    //  適用於 beam-along-localY 燈具（tiltTransform.up 為光束方向）
    //  staticOffset 定義圓心， range=角度半徑， speed=繞圓速度
    //  輸出經 DeltaAngle 正規化後，由 SmoothDampAngle 連續追蹤
    // ==========================================
    private Vector2 CalculateCircleAngles(float et, float speed, float range, Vector2 staticOffset)
    {
        Vector3 panAxis  = GetSafeAxis(panRotationVector, Vector3.up);
        Vector3 tiltAxis = GetSafeAxis(tiltRotationVector, Vector3.left);

        // 1. 圓心方向：beam(P0, T0) = AngleAxis(P0, panAxis) * AngleAxis(T0, tiltAxis) * Vector3.up
        Quaternion centerPanQ  = Quaternion.AngleAxis(staticOffset.x, panAxis);
        Quaternion centerTiltQ = Quaternion.AngleAxis(staticOffset.y, tiltAxis);
        Vector3 centerDir = centerPanQ * centerTiltQ * Vector3.up;

        // 2. 圓錐邊緣起點：tilt 多居移 range 度
        Quaternion startTiltQ = Quaternion.AngleAxis(staticOffset.y + range, tiltAxis);
        Vector3 startEdge = centerPanQ * startTiltQ * Vector3.up;

        // 3. 繞 centerDir 旋轉 theta 度，產生幾何等速圓
        float thetaDeg = et * speed * 20f;
        Vector3 finalDir = Quaternion.AngleAxis(thetaDeg, centerDir) * startEdge;

        // 4. 反解 pan：以「T=90°時的水平投影方向」為 pan=0° 參考
        //    不需磁白就能適用於任意 tiltAxis
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
    //  計算各模式的「静態中心方向」（含模式預設角度）
    //  用於 Gizmo 圖示展示
    // ==========================================
    private Vector3 ComputeStaticCenterDir(RotationMode mode, int unitIdx, Vector2 staticOffset)
    {
        Vector3 panAxis  = GetSafeAxis(panRotationVector, Vector3.up);
        Vector3 tiltAxis = GetSafeAxis(tiltRotationVector, Vector3.left);

        float pan, tilt;
        switch (mode)
        {
            case RotationMode.Scan:
                pan  = staticOffset.x;
                tilt = 45f + staticOffset.y;   // Scan 預設 tilt=45°
                break;
            case RotationMode.Cross:
                float panSide = (unitIdx % 2 == 0) ? 1f : -1f;
                pan  = 90f * panSide + staticOffset.x;
                tilt = 45f + staticOffset.y;   // Cross 預設 tilt=45° (35+10)
                break;
            default: // Static, Circle, VerticalSwing, Random
                pan  = staticOffset.x;
                tilt = staticOffset.y;
                break;
        }

        // beam(pan, tilt) 在 pan-parent 局部空間的方向（beam-along-localY）
        return Quaternion.AngleAxis(pan, panAxis)
             * Quaternion.AngleAxis(tilt, tiltAxis)
             * Vector3.up;
    }

    // ==========================================
    //  頻譜處理
    // ==========================================
    private void ProcessSpectrum(float smooth, float sense, bool isTimeJump, float dt)
    {
        if (!enableColorUpdate) return;

        float rL = GetAverage(0, 2);
        float rM = GetAverage(3, 20);
        float rH = GetAverage(21, 100);

        if (Application.isPlaying) { rL *= 15f; rM *= 15f; rH *= 15f; }

        if (isTimeJump) { curLow = 0; curMid = 0; curHigh = 0; lowMax = 0.01f; midMax = 0.01f; highMax = 0.01f; }

        lowMax = Mathf.Max(lowMax * 0.99f, rL, 0.005f);
        midMax = Mathf.Max(midMax * 0.99f, rM, 0.005f);
        highMax = Mathf.Max(highMax * 0.99f, rH, 0.005f);

        curLow = Mathf.Lerp(curLow, Mathf.Clamp01(rL / lowMax), dt * smooth * 15f);
        curMid = Mathf.Lerp(curMid, Mathf.Clamp01(rM / midMax), dt * smooth * 15f);
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
        Vector3 tiltAxis = GetSafeAxis(tiltRotationVector, Vector3.left);

        Vector3 targetInPanParent = pR.parent.InverseTransformPoint(target.position) - pR.localPosition;
        if (targetInPanParent.sqrMagnitude < 0.000001f)
            return new Vector2(panOffset, verticalBaseOffset + tiltOffset);

        float pan = SignedAngleOnAxis(Vector3.forward, targetInPanParent, panAxis);

        Quaternion undoPan = Quaternion.AngleAxis(-pan, panAxis);
        Vector3 targetInPanSpace = undoPan * targetInPanParent;
        Vector3 targetFromTiltPivot = targetInPanSpace - tR.localPosition;
        float tilt = SignedAngleOnAxis(Vector3.forward, targetFromTiltPivot, tiltAxis);

        tilt = invertVerticalTracking ? -tilt : tilt;

        return new Vector2(pan + panOffset, tilt + verticalBaseOffset + tiltOffset);
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

    // ==========================================
    //  Debug Gizmo：繪製每顆燈的静態中心方向與居部平面
    //  不跟著動画移動，顯示 staticOffset 所定義的圓心 / 標準朝向
    //  模式預設角度（Scan/Cross 的 45°）已納入計算
    // ==========================================
    void OnDrawGizmos()
    {
        if (!debugDrawLocalPlane || slmUnits == null) return;

        float s = debugPlaneSize;
        Vector3 panAxis  = GetSafeAxis(panRotationVector, Vector3.up);
        Vector3 tiltAxis = GetSafeAxis(tiltRotationVector, Vector3.left);

        for (int i = 0; i < slmUnits.Length; i++)
        {
            var unit = slmUnits[i];
            if (unit == null || unit.tiltTransform == null || unit.panTransform == null) continue;

            Vector3 origin = unit.tiltTransform.position;

            // 取 pan-parent Transform
            Transform panParent = unit.panTransform.parent;

            // 送展後  静態中心方向（世界空間）
            Vector3 centerDir;
            if (_dbgCenterDir != null && i < _dbgCenterDir.Length && _dbgCenterDir[i].sqrMagnitude > 0.0001f)
            {
                // 由 UpdateStage 快取的平面局部空間方向，轉到世界空間
                centerDir = panParent != null
                    ? panParent.TransformDirection(_dbgCenterDir[i])
                    : _dbgCenterDir[i];
            }
            else
            {
                // 未播放時的 fallback：用現在 tiltTransform.up 看似方向
                centerDir = unit.tiltTransform.up;
            }

            // pan 軸（世界空間）
            Vector3 wPanAxis = panParent != null ? panParent.TransformDirection(panAxis) : panAxis;

            // 垃直於 centerDir 的居部軸
            Vector3 wRight = Vector3.Cross(wPanAxis, centerDir);
            if (wRight.sqrMagnitude < 0.0001f)
            {
                // 天頂/底部 gimbal：fall back to tilt axis direction
                wRight = panParent != null
                    ? panParent.TransformDirection(Quaternion.AngleAxis(90f, panAxis) * Vector3.right)
                    : Vector3.right;
            }
            else wRight = wRight.normalized;
            Vector3 wUp = Vector3.Cross(centerDir, wRight).normalized;

            // 黃色線：静態中心方向
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, centerDir * s * 2f);

            // 青色方框：垃直於 centerDir 的平面
            Gizmos.color = Color.cyan;
            Vector3 r = wRight * s;
            Vector3 u = wUp * s;
            Vector3 c = origin + centerDir * s;
            Gizmos.DrawLine(c - r - u, c + r - u);
            Gizmos.DrawLine(c + r - u, c + r + u);
            Gizmos.DrawLine(c + r + u, c - r + u);
            Gizmos.DrawLine(c - r + u, c - r - u);

            // 紅：pan 軸；綠：tilt 軸
            Gizmos.color = Color.red;
            Gizmos.DrawRay(origin, wPanAxis * s * 0.5f);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(origin, unit.panTransform.TransformDirection(tiltAxis) * s * 0.5f);
        }
    }
}
