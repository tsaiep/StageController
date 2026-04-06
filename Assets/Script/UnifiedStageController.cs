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
        [InspectorName("交叉掃描")] Cross
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
        float totalMotionWeight, float weightedEffectiveTime)
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

        for (int ui = 0; ui < unitCount; ui++)
        {
            var unit = slmUnits[ui];
            if (unit == null) continue;

            float normalizedIndex = (unitCount > 1) ? (float)ui / (unitCount - 1) : 0f;

            // --- 累加變數 ---
            Color unitColor = Color.black;
            float totalPan = 0f, totalTilt = 0f;
            float targetModeWeight = 0f; // Target 模式的混合權重
            bool hasContinuousRotation = false;

            // --- 逐 Clip 計算 ---
            for (int ci = 0; ci < clips.Count; ci++)
            {
                var clip = clips[ci];

                // ===== Per-unit 延遲 =====
                float unitDelay = 0f;
                bool isRandomMode = (clip.mode == RotationMode.Random);
                if (!isRandomMode && clip.delayCurve != null && clip.delayFactor > 0f)
                {
                    float dv = clip.delayCurve.Evaluate(normalizedIndex);
                    unitDelay = clip.delayFactor * dv * unitCount;
                }

                // 全域有效時間（所有 unit 共用）
                float globalEt = Mathf.Max(0, clip.effectiveTime - clip.pauseTime);
                // Per-unit 帶延遲的有效時間（相位偏移，不做 clamp）
                float unitEt = globalEt - unitDelay;

                // ===== 顏色（動態循環取樣）=====
                if (enableColorUpdate)
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
                        colorPhase = clip.normalizedClipTime; // Static / Target fallback
                    }

                    Color clipColor = (clip.gradient != null) ? clip.gradient.Evaluate(colorPhase) : Color.white;
                    unitColor += clipColor * clip.weight;
                }

                // ===== 旋轉 =====
                if (unit.panTransform == null || unit.tiltTransform == null) continue;

                float clipPan, clipTilt;

                if (clip.mode == RotationMode.Target)
                {
                    // --- 目標追蹤：LookAt 角度 ---
                    Transform finalTarget = (clip.target != null) ? clip.target : defaultTarget;
                    if (finalTarget != null)
                    {
                        Vector2 look = CalculateLookAtAngles(unit.panTransform, unit.tiltTransform, finalTarget);
                        clipPan = look.x;
                        clipTilt = look.y;
                    }
                    else
                    {
                        clipPan = clip.staticOffset.x;
                        clipTilt = clip.staticOffset.y;
                    }
                    // Target 不套用 invert
                    targetModeWeight += clip.weight;
                }
                else
                {
                    // --- 非追蹤模式 ---
                    float unitTimeOffset = unit.motionOffset * waveIntensity;
                    float adjustedEt = unitEt + unitTimeOffset;

                    Vector2 angles = CalculateAnglesForUnit(
                        clip.mode, clip.speed, clip.range,
                        adjustedEt, ui, clip.staticOffset, clip.randomStrength
                    );
                    clipPan = angles.x;
                    clipTilt = angles.y;

                    // 套用對稱反轉
                    clipPan = (unit.invertPan ^ invertControllerPan) ? -clipPan : clipPan;
                    clipTilt = (unit.invertTilt ^ invertControllerTilt) ? -clipTilt : clipTilt;

                    if (clip.mode == RotationMode.Circle) hasContinuousRotation = true;
                }

                totalPan += clipPan * clip.weight;
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
                    if (hasContinuousRotation)
                    {
                        // 圓周運動：用 SmoothDamp（不包角），避免 360°→0° 反轉
                        unit.curPan = Mathf.SmoothDamp(unit.curPan, totalPan, ref unit.velPan, sTime, mSpeed, physicalDt);
                        unit.curTilt = Mathf.SmoothDamp(unit.curTilt, totalTilt, ref unit.velTilt, sTime, mSpeed, physicalDt);
                    }
                    else
                    {
                        unit.curPan = Mathf.SmoothDampAngle(unit.curPan, totalPan, ref unit.velPan, sTime, mSpeed, physicalDt);
                        unit.curTilt = Mathf.SmoothDampAngle(unit.curTilt, totalTilt, ref unit.velTilt, sTime, mSpeed, physicalDt);
                    }
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
                p = (et * speed * 20f) + staticOffset.x;
                t = range + staticOffset.y;
                break;

            case RotationMode.VerticalSwing:
                p = staticOffset.x;
                t = Mathf.Sin(et * speed) * range + staticOffset.y;
                break;

            case RotationMode.Random:
                // 兩段式混合：randomStrength 控制噪波強度
                float initP = (Mathf.PerlinNoise(0f, index * 0.5f) - 0.5f) * 2f * range + staticOffset.x;
                float initT = (Mathf.PerlinNoise(index * 0.5f, 0f) - 0.5f) * 2f * range + staticOffset.y;
                float fullP = (Mathf.PerlinNoise(et * speed, index * 0.5f) - 0.5f) * 2f * range + staticOffset.x;
                float fullT = (Mathf.PerlinNoise(index * 0.5f, et * speed) - 0.5f) * 2f * range + staticOffset.y;
                p = Mathf.Lerp(initP, fullP, randomStrength);
                t = Mathf.Lerp(initT, fullT, randomStrength);
                break;

            case RotationMode.Cross:
                // 每個 unit 直接計算自己的角度（panSide 基於 index）
                float panSide = (index % 2 == 0) ? 1f : -1f;
                p = (90f * panSide) + staticOffset.x;
                t = (Mathf.Sin(et * speed) * range) + 35f + staticOffset.y + 10f;
                break;
        }

        return new Vector2(p, t);
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
}
