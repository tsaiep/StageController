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

    [HideInInspector] public float curPan, curTilt, velPan, velTilt;
    [HideInInspector] public float frozenPan, frozenTilt;
    [HideInInspector] public Color frozenColor;

    // ── 分組資訊（由 StageLightArranger.GenerateLights() 寫入）──
    [HideInInspector] public int groupIndex    = 0; // 所屬分組 (0-based)
    [HideInInspector] public int groupCount    = 1; // 總分組數（≥1，預設 1 以維持舊場景相容性）
    [HideInInspector] public int indexInGroup  = 0; // 在分組內的順序 (0-based)
    [HideInInspector] public int groupSize     = 1; // 所屬分組的燈光總數（≥1，預設 1 以維持舊場景相容性）
}