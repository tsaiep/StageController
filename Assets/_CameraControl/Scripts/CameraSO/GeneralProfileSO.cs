using UnityEngine;

// =========================================================================
// 子類別 A：一般運鏡資產 (必須獨佔 GeneralProfileSO.cs 檔名)
// =========================================================================
[CreateAssetMenu(fileName = "NewGeneralProfile", menuName = "Camera System/Profiles/General Profile")]
public class GeneralProfileSO : CameraProfileSO
{
    [Header("--- 1. Position Composer 曲線庫 ---")]
    public AnimationCurve posDistanceCurve = AnimationCurve.Linear(0f, 2f, 1f, 2f);
    public AnimationCurve posScreenXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve posScreenYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve posTargetOffsetXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve posTargetOffsetYCurve = AnimationCurve.Linear(0f, 1.2f, 1f, 1.2f);
    public AnimationCurve posTargetOffsetZCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Header("--- 2. Position Composer Damping 位置阻尼 ---")]
    [Range(0f, 10f)] public float posDampingX = 1f;
    [Range(0f, 10f)] public float posDampingY = 1f;
    [Range(0f, 10f)] public float posDampingZ = 1f;

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