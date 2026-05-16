using UnityEngine;

public class SLMUnit : MonoBehaviour
{
    [Header("實體元件")]
    public Transform panTransform;
    public Transform tiltTransform;
    public Light targetLight; // 腳本會自動同步此 Light 與 VLB

    [Header("單元演出設定")]
    public bool invertPan = false;
    public bool invertTilt = false;
    [Tooltip("動作位移")] public float motionOffset = 0f;

    [Header("旋轉基準偏移（非動畫狀態下的靜止角）")]
    [Tooltip("x = Pan 靜止旋轉角（±180°），y = Tilt 靜止旋轉角（±180°）\n" +
             "作為 Custom Track 動畫的零點基準：動畫 0° = 燈光靜止位置\n" +
             "修改此數值將即時更新燈光的 Transform")]
    public Vector2 rotationBase = Vector2.zero;

    [HideInInspector] public float curPan, curTilt, velPan, velTilt;
    [HideInInspector] public float frozenPan, frozenTilt;
    [HideInInspector] public Color frozenColor;

    // tiltRotationVector.x 的符號快取（由 UnifiedStageController 寫入）
    // Vector3.left (x=-1) → -1f；Vector3.right (x=+1) → +1f
    // 用於 ApplyBaseToTransforms 決定 eulerAngles.x 的正確符號
    [HideInInspector] public float tiltAxisSignCache = -1f;

    // ── 分組資訊（由 StageLightArranger.GenerateLights() 寫入）──
    [HideInInspector] public int groupIndex    = 0; // 所屬分組 (0-based)
    [HideInInspector] public int groupCount    = 1; // 總分組數（≥1，預設 1 以維持舊場景相容性）
    [HideInInspector] public int indexInGroup  = 0; // 在分組內的順序 (0-based)
    [HideInInspector] public int groupSize     = 1; // 所屬分組的燈光總數（≥1，預設 1 以維持舊場景相容性）

    // ─────────────────────────────────────────────────────────
    //  工具方法
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 將 rotationBase 寫回 panTransform / tiltTransform 的 localRotation（僅修改對應軸）
    /// </summary>
    public void ApplyBaseToTransforms()
    {
        if (panTransform != null)
        {
            Vector3 e = panTransform.localEulerAngles;
            e.y = rotationBase.x;   // panRotationVector = Vector3.up，方向一致，不需取反
            panTransform.localEulerAngles = e;
        }
        if (tiltTransform != null)
        {
            Vector3 e = tiltTransform.localEulerAngles;
            // AngleAxis(angle, tiltAxis) → eulerAngles.x ≈ angle * tiltAxis.x
            // tiltAxisSignCache = tiltRotationVector.x（由 Controller 更新）
            // 例：Vector3.left(x=-1) → e.x = -rotationBase.y
            //     Vector3.right(x=+1) → e.x = +rotationBase.y
            e.x = rotationBase.y * tiltAxisSignCache;
            tiltTransform.localEulerAngles = e;
        }
    }

    /// <summary>
    /// 將 Unity 的 [0°, 360°) 角度轉為 ±180° 有號角度
    /// </summary>
    public static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)  angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    // ─────────────────────────────────────────────────────────
    //  Editor 模式下，Inspector 修改 rotationBase 時立即同步 Transform
    //  單向：數值 → Transform，不反讀 Transform（避免 Timeline scrub 干擾）
    // ─────────────────────────────────────────────────────────
    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            ApplyBaseToTransforms();
#endif
    }
}