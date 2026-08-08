using UnityEngine;

// =========================================================================
// 子類別 B：追蹤特寫資產 (必須獨佔 TrackingProfileSO.cs 檔名)
// =========================================================================
[CreateAssetMenu(fileName = "NewTrackingProfile", menuName = "Camera System/Profiles/Tracking Profile")]
public class TrackingProfileSO : CameraProfileSO
{
    [Header("--- 1. Cinemachine Follow 跟隨偏移曲線 ---")]
    public AnimationCurve followOffsetXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve followOffsetYCurve = AnimationCurve.Linear(0f, 0.1f, 1f, 0.1f);
    public AnimationCurve followOffsetZCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("--- 2. Position Damping 位置阻尼 ---")]
    [Range(0f, 10f)] public float dampingX = 1f;
    [Range(0f, 10f)] public float dampingY = 1f;
    [Range(0f, 10f)] public float dampingZ = 1f;

    [Header("--- 3. Rotation Composer 曲線庫 ---")]
    public AnimationCurve rotScreenXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotScreenYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotTargetOffsetXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotTargetOffsetYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotTargetOffsetZCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Header("--- 4. Rotation Composer Damping 旋轉阻尼 ---")]
    [Range(0f, 10f)] public float rotDampingX = 1f;
    [Range(0f, 10f)] public float rotDampingY = 1f;
}