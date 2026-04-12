using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization; // For FormerlySerializedAs

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

/// <summary>
/// 掛在 PatternPlacer 生成的 Container 上：
/// 1) 依子物件本地軸向做位移動畫（t ∈ [0,1] 由曲線控制位移量）
/// 2) 依「圈內順序 col」與 ring/row 增加延遲（共用一個 Loop 設定）
/// 3) 可選在動畫中加入繞指定本地軸的旋轉（同樣由曲線控制角度）
/// 4) 支援執行期「程序驅動」播放，或「烘焙成 AnimationClip」存檔（固定取樣或自動關鍵化）
///
/// ★ 非循環(loop=false)烘焙時，總片長 = duration + maxDelay，使最後一個延遲的物件也能完整跑到 t=1。
/// </summary>
[ExecuteAlways]
public class ContainerMotionAnimator : MonoBehaviour
{
    public enum Axis { X, Y, Z, NegX, NegY, NegZ }
    public enum PlayMode { Disabled, RuntimePreview } // Disabled：不在 Update 播放，只用來烘焙
    public enum BakeMode { FixedSampleRate, AutoKeyReduce }

    [Header("Target & Playback")]
    [Tooltip("只要是此 Container 的直接子物件、且帶有 PatternPlacer.InstanceId 都會被驅動/烘焙")]
    public Transform container; // 建議直接拖自身(=this.transform)
    public PlayMode playMode = PlayMode.Disabled;

    [Header("Timing")]
    [Min(0.01f)] public float duration = 2f;      // 一個循環用時（秒）
    public bool loop = true;                       // 與 col/ring 延遲共用的循環設定
    [Tooltip("全域時間縮放（只影響 Runtime 預覽）")]
    public float timeScale = 1f;

    [Header("Movement (位移)")]
    public bool enablePosition = true;
    [Tooltip("位移軸向取自每個子物件的本地軸向")]
    public Axis moveAxis = Axis.Z;
    [Tooltip("位移最大距離（曲線值會乘上此距離）")]
    public float moveDistance = 1f;
    [Tooltip("t ∈ [0,1] → 位移量（0~1）")]
    public AnimationCurve positionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Rotation (旋轉，可選)")]
    public bool enableRotation = false;
    public Axis rotateAxis = Axis.Y;
    [Tooltip("曲線輸出角度的最大值（度）。實際角度 = curve(t) * maxAngleDeg")]
    public float maxAngleDeg = 30f;
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Per-Object Delay (依圈內順序)")]
    [FormerlySerializedAs("indexDelayStep")]
    [Tooltip("每一個 ring/row 內的物件順序（InstanceId.col, 0-based） * orderDelayStep")]
    public float orderDelayStep = 0.1f;

    [Header("Per-Ring/Row Delay (依圈/列延遲)")]
    [Tooltip("每一個 ring/row 的額外延遲 = ringOrRow * ringDelayStep")]
    public float ringDelayStep = 0.25f;

    [Header("Baking (AnimationClip)")]
    public BakeMode bakeMode = BakeMode.FixedSampleRate;

    [Tooltip("FixedSampleRate：目標鍵密度（配合 preSampleFPS 以 stride 等間距降採樣）")]
    [Min(1)] public int sampleRate = 30;

    [Tooltip("兩種模式皆先用此 FPS 進行高頻取樣；Fixed 再依 sampleRate 等間距降採樣；AutoKey 再做關鍵簡化")]
    [Min(15)] public int preSampleFPS = 60;

    [Tooltip("AutoKeyReduce：位置/標量用的 RDP 簡化誤差容忍（越大關鍵越少）")]
    [Min(0)] public float simplifyTolerance = 0.001f;

    [Tooltip("AutoKeyReduce：四元數旋轉的角度簡化門檻（度）（越大關鍵越少）")]
    [Min(0)] public float simplifyAngleToleranceDeg = 0.5f;

#if UNITY_EDITOR
    [Header("Save Path")]
    [Tooltip("將生成的 AnimationClip 儲存到此相對路徑（例如：Assets/Animations/MyMotion.anim）")]
    public string savePath = "Assets/Animations/ContainerMotion.anim";
#endif

    // —— 內部暫存 —— //
    struct ChildState
    {
        public Transform tf;
        public PatternPlacer.InstanceId id;      // 來源：包含 ringOrRow 與 col（圈內順序）
        public Vector3 localPos0;                // 初始 localPosition
        public Quaternion localRot0;             // 初始 localRotation
    }

    private readonly List<ChildState> _children = new List<ChildState>();
    private float _runtimeTime;

    void OnEnable()
    {
        if (!container) container = transform;
        CaptureInitialPoses();
        _runtimeTime = 0f;
    }

    void OnValidate()
    {
        if (!container) container = transform;
    }

    void Update()
    {
        if (playMode == PlayMode.RuntimePreview && Application.isPlaying)
        {
            _runtimeTime += Time.deltaTime * Mathf.Max(0.0001f, timeScale);
            float T = duration <= 0 ? 0.0001f : duration;
            float globalT = _runtimeTime / T; // 純參考，個別還會套延遲
            ApplyProcedural(globalT);
        }
#if UNITY_EDITOR
        else if (playMode == PlayMode.RuntimePreview && !Application.isPlaying)
        {
            // 在編輯器下也能預覽（這裡不自動推進時間）
        }
#endif
    }

    // 重新抓取 Container 子物件清單與初始姿態
    [ContextMenu("Capture Initial Poses")]
    public void CaptureInitialPoses()
    {
        _children.Clear();
        if (!container) return;

        foreach (Transform child in container)
        {
            var id = child.GetComponent<PatternPlacer.InstanceId>();
            if (id == null) continue;

            _children.Add(new ChildState
            {
                tf = child,
                id = id,
                localPos0 = child.localPosition,
                localRot0 = child.localRotation
            });
        }
    }

    // 依目前參數對場景做「程序驅動」效果（不寫入動畫）
    public void ApplyProcedural(float globalCycles)
    {
        if (_children.Count == 0) return;

        float T = Mathf.Max(0.0001f, duration);

        foreach (var c in _children)
        {
            float delay = c.id.col * orderDelayStep + c.id.ringOrRow * ringDelayStep;
            float tRaw = globalCycles - (delay / T); // 以「循環數」表示的時間
            float t = loop ? Mathf.Repeat(tRaw, 1f) : Mathf.Clamp01(tRaw);

            Vector3 localPos = c.localPos0;
            Quaternion localRot = c.localRot0;

            // —— 位移（本地軸） —— //
            if (enablePosition)
            {
                float m = Mathf.Max(0f, positionCurve.Evaluate(Mathf.Clamp01(t))) * moveDistance;
                Vector3 dirLocal = AxisToVector(c.tf, moveAxis, spaceLocal: true); // 回傳本地單位向量
                localPos = c.localPos0 + dirLocal * m;
            }

            // —— 旋轉（本地軸） —— //
            if (enableRotation)
            {
                float ang = rotationCurve.Evaluate(Mathf.Clamp01(t)) * maxAngleDeg;
                Vector3 axisLocal = AxisToVector(c.tf, rotateAxis, spaceLocal: true);
                localRot = c.localRot0 * Quaternion.AngleAxis(ang, axisLocal);
            }

            c.tf.localPosition = localPos;
            c.tf.localRotation = localRot;
        }
    }

    // 計算在某個 globalCycles（= 已過循環數）時，該子物件的 localPosition 與 localRotation
    void EvaluateLocalTR(ChildState c, float globalCycles, out Vector3 lpos, out Quaternion lrot)
    {
        float T = Mathf.Max(0.0001f, duration);

        float delay = c.id.col * orderDelayStep + c.id.ringOrRow * ringDelayStep;
        float tRaw = globalCycles - (delay / T);
        float t = loop ? Mathf.Repeat(tRaw, 1f) : Mathf.Clamp01(tRaw);

        lpos = c.localPos0;
        lrot = c.localRot0;

        if (enablePosition)
        {
            float m = Mathf.Max(0f, positionCurve.Evaluate(Mathf.Clamp01(t))) * moveDistance;
            Vector3 dirLocal = AxisToVector(c.tf, moveAxis, true);
            lpos = c.localPos0 + dirLocal * m;
        }

        if (enableRotation)
        {
            float ang = rotationCurve.Evaluate(Mathf.Clamp01(t)) * maxAngleDeg;
            Vector3 axisLocal = AxisToVector(c.tf, rotateAxis, true);
            lrot = c.localRot0 * Quaternion.AngleAxis(ang, axisLocal);
        }
    }

    // —— 烘焙成 AnimationClip —— //
#if UNITY_EDITOR
    [ContextMenu("Bake To AnimationClip")]
    public void BakeToClip()
    {
        if (_children.Count == 0) CaptureInitialPoses();
        if (_children.Count == 0)
        {
            Debug.LogWarning("[ContainerMotionAnimator] 找不到可烘焙的子物件（需有 InstanceId）。");
            return;
        }

        var clip = new AnimationClip();
        clip.name = "ContainerMotion";
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        float T = Mathf.Max(0.0001f, duration);
        float maxDelaySec = ComputeMaxDelaySeconds();
        float bakeSpan = loop ? T : (T + maxDelaySec); // 非循環：包含最大延遲

        foreach (var c in _children)
        {
            BakeOneChild(clip, c, bakeSpan);
        }

        // 四元數曲線連續化（處理跨 180° 與 ±q 半球）
        clip.EnsureQuaternionContinuity();

        EnsureAssetFolder(savePath);
        AssetDatabase.CreateAsset(clip, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ContainerMotionAnimator] Clip 已儲存：{savePath}");
        TryAttachClipToAnimator(clip);
    }

    float ComputeMaxDelaySeconds()
    {
        float maxDelay = 0f;
        foreach (var c in _children)
        {
            float d = c.id.col * orderDelayStep + c.id.ringOrRow * ringDelayStep;
            if (d > maxDelay) maxDelay = d;
        }
        return maxDelay;
    }

    void BakeOneChild(AnimationClip clip, ChildState c, float bakeSpan)
    {
        // Position 三條
        List<Keyframe> px = new List<Keyframe>();
        List<Keyframe> py = new List<Keyframe>();
        List<Keyframe> pz = new List<Keyframe>();
        // Rotation 四元數四條
        List<Keyframe> qx = new List<Keyframe>();
        List<Keyframe> qy = new List<Keyframe>();
        List<Keyframe> qz = new List<Keyframe>();
        List<Keyframe> qw = new List<Keyframe>();

        int baseFps = Mathf.Max(15, preSampleFPS);
        float dt = 1f / baseFps;
        int steps = Mathf.Max(1, Mathf.CeilToInt(bakeSpan / dt));

        bool useUniformDownsample = (bakeMode == BakeMode.FixedSampleRate);
        int stride = 1;
        if (useUniformDownsample)
        {
            stride = Mathf.Max(1, Mathf.RoundToInt(baseFps / (float)Mathf.Max(1, sampleRate)));
        }

        // —— 旋轉增量累乘 —— //
        Vector3 axisLocal = AxisToVector(c.tf, rotateAxis, true);
        Quaternion qAccum = c.localRot0;
        bool rotInitialized = false;
        float prevAccAngle = 0f;

        for (int i = 0; i <= steps; i++)
        {
            float tSec = i * dt;
            float T = Mathf.Max(0.0001f, duration);

            // 共用：位置照舊評估（t 用 Repeat/Clamp）
            float globalCycles = tSec / T;
            EvaluateLocalTR(c, globalCycles, out Vector3 lpos, out _ /* ignore rotation here */);

            // —— 位置取樣保留規則 —— //
            bool keepThis = !useUniformDownsample || (i == 0 || i == steps || (i % stride == 0));
            if (keepThis)
            {
                px.Add(new Keyframe(tSec, lpos.x));
                py.Add(new Keyframe(tSec, lpos.y));
                pz.Add(new Keyframe(tSec, lpos.z));
            }

            // —— 旋轉（只在 enableRotation 時處理） —— //
            if (!enableRotation || !keepThis) continue;

            // tRaw：含延遲的循環數
            float delay = c.id.col * orderDelayStep + c.id.ringOrRow * ringDelayStep;
            float tRaw = globalCycles - (delay / T);

            // frac：單循環相位；wraps：跨越了幾個整循環（負值代表先從 0.9…起步的情況）
            float frac = loop ? Mathf.Repeat(tRaw, 1f) : Mathf.Clamp01(tRaw);
            int wraps = loop ? Mathf.FloorToInt(tRaw) : 0;

            // —— 展開角：曲線角度 + 每跨一循環就累加 maxAngleDeg —— //
            float angleAcc = rotationCurve.Evaluate(frac) * maxAngleDeg + wraps * maxAngleDeg;

            if (!rotInitialized)
            {
                // 第一個保留幀：以「起始展開角」定位 qAccum
                qAccum = c.localRot0 * Quaternion.AngleAxis(angleAcc, axisLocal);
                prevAccAngle = angleAcc;
                rotInitialized = true;
            }
            else
            {
                // 後續幀：用「角度增量」右乘在當前姿態（圍繞物件本地軸）
                float dAng = angleAcc - prevAccAngle;
                qAccum = qAccum * Quaternion.AngleAxis(dAng, axisLocal);
                prevAccAngle = angleAcc;
            }

            qAccum = NormalizeSafe(qAccum);
            qx.Add(new Keyframe(tSec, qAccum.x));
            qy.Add(new Keyframe(tSec, qAccum.y));
            qz.Add(new Keyframe(tSec, qAccum.z));
            qw.Add(new Keyframe(tSec, qAccum.w));
        }

        // AutoKeyReduce：位置→1D RDP；旋轉→四元數 RDP（角度誤差）
        if (bakeMode == BakeMode.AutoKeyReduce)
        {
            px = Simplify(px, simplifyTolerance);
            py = Simplify(py, simplifyTolerance);
            pz = Simplify(pz, simplifyTolerance);

            SimplifyQuaternionCurves(qx, qy, qz, qw, simplifyAngleToleranceDeg,
                                     out qx, out qy, out qz, out qw);
        }

        string path = AnimationUtility.CalculateTransformPath(c.tf, transform);

        // localPosition（帶 loop 端點修正）
        SetCurve(clip, path, typeof(Transform), "m_LocalPosition.x", px, loop);
        SetCurve(clip, path, typeof(Transform), "m_LocalPosition.y", py, loop);
        SetCurve(clip, path, typeof(Transform), "m_LocalPosition.z", pz, loop);

        // localRotation (Quaternion)（帶 loop 端點修正）
        SetCurve(clip, path, typeof(Transform), "m_LocalRotation.x", qx, loop);
        SetCurve(clip, path, typeof(Transform), "m_LocalRotation.y", qy, loop);
        SetCurve(clip, path, typeof(Transform), "m_LocalRotation.z", qz, loop);
        SetCurve(clip, path, typeof(Transform), "m_LocalRotation.w", qw, loop);

        clip.frameRate = baseFps; // 顯示/播放格點維持 preSampleFPS
    }

    static void EnsureAssetFolder(string assetPath)
    {
        string path = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(path)) return;
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }

    void TryAttachClipToAnimator(AnimationClip clip)
    {
        var animator = GetComponent<Animator>();
        if (!animator) animator = gameObject.AddComponent<Animator>();

        var ctrl = animator.runtimeAnimatorController as AnimatorController;
        if (!ctrl)
        {
            string ctrlPath = savePath.Replace(".anim", ".controller");
            EnsureAssetFolder(ctrlPath);
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            animator.runtimeAnimatorController = ctrl;
        }

        var sm = ctrl.layers[0].stateMachine;
        AnimatorState state;
        if (sm.states.Length == 0) state = sm.AddState("Motion");
        else state = sm.states[0].state;
        state.motion = clip;
    }

    // —— 寫入 Curve（Clamped Auto；Loop 端點切線修正） —— //
    static void SetCurve(AnimationClip clip, string path, System.Type type, string property, List<Keyframe> keys, bool makeLoop)
    {
        var curve = new AnimationCurve(keys.ToArray());

        // Loop：先把最後一點 value 對齊第一點（避免縫隙）
        if (makeLoop && curve.length >= 2)
        {
            int last = curve.length - 1;
            var k0 = curve.keys[0];
            var kL = curve.keys[last];
            if (!Mathf.Approximately(kL.value, k0.value))
            {
                kL.value = k0.value;
                curve.MoveKey(last, kL);
            }
        }

        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyBroken(curve, i, false);
        }

        if (makeLoop && curve.length >= 3)
        {
            FixLoopEndTangents(curve);
        }

        clip.SetCurve(path, type, property, curve);
    }

    // 跨接縫中央差分斜率，使 0 與 T 的一階導數一致
    static void FixLoopEndTangents(AnimationCurve curve)
    {
        int n = curve.length;
        int first = 0, last = n - 1, prev = n - 2, next = 1;

        var k0 = curve.keys[first];
        var kL = curve.keys[last];
        var kPrev = curve.keys[prev];
        var kNext = curve.keys[next];

        float dtWrap = (kL.time - kPrev.time) + (kNext.time - k0.time);
        if (dtWrap < 1e-6f) dtWrap = 1e-6f;

        float s = (kNext.value - kPrev.value) / dtWrap;

        AnimationUtility.SetKeyRightTangentMode(curve, first, AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyLeftTangentMode(curve,  last,  AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyBroken(curve, first, false);
        AnimationUtility.SetKeyBroken(curve, last,  false);

        k0.outTangent = s;
        kL.inTangent  = s;

        curve.MoveKey(first, k0);
        curve.MoveKey(last,  kL);
    }

    // —— 1D RDP —— //
    static List<Keyframe> Simplify(List<Keyframe> src, float tol)
    {
        if (src.Count <= 2 || tol <= 0f) return new List<Keyframe>(src);
        int n = src.Count;
        var keep = new bool[n];
        keep[0] = keep[n - 1] = true;
        RDP(src, 0, n - 1, tol, keep);

        var outKeys = new List<Keyframe>();
        for (int i = 0; i < n; i++) if (keep[i]) outKeys.Add(src[i]);
        return outKeys;
    }

    static void RDP(List<Keyframe> keys, int a, int b, float tol, bool[] keep)
    {
        float t0 = keys[a].time, v0 = keys[a].value;
        float t1 = keys[b].time, v1 = keys[b].value;

        float maxErr = -1f; int idx = -1;
        for (int i = a + 1; i < b; i++)
        {
            float ti = keys[i].time, vi = keys[i].value;
            float u = Mathf.InverseLerp(t0, t1, ti);
            float vLin = Mathf.Lerp(v0, v1, u);
            float err = Mathf.Abs(vi - vLin);
            if (err > maxErr) { maxErr = err; idx = i; }
        }

        if (maxErr > tol && idx >= 0)
        {
            keep[idx] = true;
            RDP(keys, a, idx, tol, keep);
            RDP(keys, idx, b, tol, keep);
        }
    }

    // —— 四元數簡化（RDP on Slerp 誤差，以角度為誤差） —— //
    struct QuatKey { public float t; public Quaternion q; public QuatKey(float t, Quaternion q) { this.t = t; this.q = q; } }

    static Quaternion NormalizeSafe(Quaternion q)
    {
        float m = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
        if (m > 1e-8f) return new Quaternion(q.x/m, q.y/m, q.z/m, q.w/m);
        return Quaternion.identity;
    }

    static void SimplifyQuaternionCurves(
        List<Keyframe> qx, List<Keyframe> qy, List<Keyframe> qz, List<Keyframe> qw, float tolDeg,
        out List<Keyframe> outQx, out List<Keyframe> outQy, out List<Keyframe> outQz, out List<Keyframe> outQw)
    {
        int n = qx.Count;
        if (n <= 2 || tolDeg <= 0f)
        {
            outQx = new List<Keyframe>(qx);
            outQy = new List<Keyframe>(qy);
            outQz = new List<Keyframe>(qz);
            outQw = new List<Keyframe>(qw);
            return;
        }

        var keys = new List<QuatKey>(n);
        Quaternion prev = NormalizeSafe(new Quaternion(qx[0].value, qy[0].value, qz[0].value, qw[0].value));
        keys.Add(new QuatKey(qx[0].time, prev));

        for (int i = 1; i < n; i++)
        {
            Quaternion cur = NormalizeSafe(new Quaternion(qx[i].value, qy[i].value, qz[i].value, qw[i].value));
            if (Quaternion.Dot(prev, cur) < 0f) cur = new Quaternion(-cur.x, -cur.y, -cur.z, -cur.w);
            keys.Add(new QuatKey(qx[i].time, cur));
            prev = cur;
        }

        var keep = new bool[n];
        keep[0] = keep[n - 1] = true;
        RDPQuat(keys, 0, n - 1, tolDeg, keep);

        outQx = new List<Keyframe>();
        outQy = new List<Keyframe>();
        outQz = new List<Keyframe>();
        outQw = new List<Keyframe>();
        for (int i = 0; i < n; i++)
        {
            if (!keep[i]) continue;
            var k = keys[i];
            var q = NormalizeSafe(k.q);
            outQx.Add(new Keyframe(k.t, q.x));
            outQy.Add(new Keyframe(k.t, q.y));
            outQz.Add(new Keyframe(k.t, q.z));
            outQw.Add(new Keyframe(k.t, q.w));
        }
    }

    static void RDPQuat(List<QuatKey> keys, int a, int b, float tolDeg, bool[] keep)
    {
        float t0 = keys[a].t, t1 = keys[b].t;
        Quaternion q0 = keys[a].q, q1 = keys[b].q;
        if (Quaternion.Dot(q0, q1) < 0f) q1 = new Quaternion(-q1.x, -q1.y, -q1.z, -q1.w);

        float maxErr = -1f; int idx = -1;
        for (int i = a + 1; i < b; i++)
        {
            float ti = keys[i].t;
            float u = Mathf.InverseLerp(t0, t1, ti);
            Quaternion qi = keys[i].q;
            Quaternion qs = Quaternion.Slerp(q0, q1, Mathf.Clamp01(u));
            float err = Quaternion.Angle(qi, qs); // 單位：度
            if (err > maxErr) { maxErr = err; idx = i; }
        }

        if (maxErr > tolDeg && idx >= 0)
        {
            keep[idx] = true;
            RDPQuat(keys, a, idx, tolDeg, keep);
            RDPQuat(keys, idx, b, tolDeg, keep);
        }
    }
#endif // UNITY_EDITOR

    // —— 公用工具 —— //
    // spaceLocal = true：回傳本地單位向量（±right/up/forward）
    // spaceLocal = false：回傳世界向量（TransformDirection）
    static Vector3 AxisToVector(Transform basis, Axis axis, bool spaceLocal)
    {
        Vector3 v =
            axis == Axis.X ? Vector3.right :
            axis == Axis.Y ? Vector3.up :
            axis == Axis.Z ? Vector3.forward :
            axis == Axis.NegX ? -Vector3.right :
            axis == Axis.NegY ? -Vector3.up :
            /* NegZ */        -Vector3.forward;

        if (spaceLocal || basis == null) return v;
        return basis.TransformDirection(v).normalized;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ContainerMotionAnimator))]
public class ContainerMotionAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var t = (ContainerMotionAnimator)target;

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture Initial Poses"))
        {
            t.CaptureInitialPoses();
        }
        if (GUILayout.Button("Bake To AnimationClip"))
        {
            t.CaptureInitialPoses();
            t.BakeToClip();
        }
        EditorGUILayout.EndHorizontal();

        if (!t.container)
            EditorGUILayout.HelpBox("建議把 container 指到此腳本所在物件（你的 *_Container）。", MessageType.Info);

        if (t.bakeMode == ContainerMotionAnimator.BakeMode.FixedSampleRate)
            EditorGUILayout.HelpBox("FixedSampleRate：先以 preSampleFPS 高頻取樣，再用 stride 等間距降採樣（sampleRate）。clip.frameRate 維持 preSampleFPS。", MessageType.None);

        if (t.bakeMode == ContainerMotionAnimator.BakeMode.AutoKeyReduce)
            EditorGUILayout.HelpBox("AutoKeyReduce：先以 preSampleFPS 取樣，再依 simplifyTolerance（位置）與 simplifyAngleToleranceDeg（旋轉）進行關鍵化簡。", MessageType.None);
    }
}
#endif
