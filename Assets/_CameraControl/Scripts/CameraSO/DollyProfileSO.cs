using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(fileName = "NewDollyProfile", menuName = "Camera System/Profiles/Dolly Profile")]
public class DollyProfileSO : CameraProfileSO
{
    [Header("--- 1. Spline Dolly 設定 ---")]
    public PathIndexUnit positionUnits = PathIndexUnit.Normalized;
    public AnimationCurve splinePositionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("--- 2. Rotation Composer 曲線庫 ---")]
    public AnimationCurve rotScreenXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotScreenYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotTargetOffsetXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    public AnimationCurve rotTargetOffsetYCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public AnimationCurve rotTargetOffsetZCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Header("--- 3. Rotation Composer Damping 旋轉阻尼 ---")]
    [Range(0f, 10f)] public float rotDampingX = 1f;
    [Range(0f, 10f)] public float rotDampingY = 1f;
}