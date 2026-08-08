using UnityEngine;
using Unity.Cinemachine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[RequireComponent(typeof(CinemachineCamera))]
public class CameraProfileWorkflow : MonoBehaviour
{
    [Header("--- 核心資產對齊 ---")]
    public CameraProfileSO targetProfileSO;
    public AnimationClip sourceAnimationClip;

    [Header("--- 逆向解凍還原設定 ---")]
    public string outputAnimName = "Restored_Camera_Anim";
    public string animSavePath = "Assets/RestoredAnimations";

    [Header("--- 烘焙採樣率 ---")]
    [Range(30, 120)] public int sampleRate = 60;

    public void BakeCurvesToSO()
    {
#if UNITY_EDITOR
        if (sourceAnimationClip == null || targetProfileSO == null)
        {
            EditorUtility.DisplayDialog("提示", "請務必正確指派動畫片段與目標 SO！", "確定");
            return;
        }

        CinemachineCamera vcam = GetComponent<CinemachineCamera>();

        CinemachinePositionComposer pos = null;
        CinemachineRotationComposer rot = null;
        CinemachineFollow follow = null;
        CinemachineSplineDolly dollyComp = null;

        if (targetProfileSO is GeneralProfileSO)
        {
            pos = vcam.GetComponent<CinemachinePositionComposer>();
            rot = vcam.GetComponent<CinemachineRotationComposer>();

            if (pos == null || rot == null)
            {
                EditorUtility.DisplayDialog(
                    "錯誤",
                    "指派了 General Profile，但相機缺少 Position Composer 或 Rotation Composer 組件！",
                    "確定"
                );
                return;
            }
        }
        else if (targetProfileSO is TrackingProfileSO)
        {
            follow = vcam.GetComponent<CinemachineFollow>();
            rot = vcam.GetComponent<CinemachineRotationComposer>();

            if (follow == null || rot == null)
            {
                EditorUtility.DisplayDialog(
                    "錯誤",
                    "指派了 Tracking Profile，但相機缺少 CinemachineFollow 或 Rotation Composer 組件！",
                    "確定"
                );
                return;
            }
        }
        else if (targetProfileSO is DollyProfileSO)
        {
            dollyComp = vcam.GetComponent<CinemachineSplineDolly>();
            rot = vcam.GetComponent<CinemachineRotationComposer>();

            if (dollyComp == null || rot == null)
            {
                EditorUtility.DisplayDialog(
                    "錯誤",
                    "指派了 Dolly Profile，但相機缺少 Spline Dolly 或 Rotation Composer 組件！",
                    "確定"
                );
                return;
            }
        }

        Undo.RecordObject(targetProfileSO, "Bake Camera Profile Curves");

        targetProfileSO.fovCurve = new AnimationCurve();

        if (targetProfileSO is GeneralProfileSO general)
        {
            general.posDistanceCurve = new AnimationCurve();

            general.posScreenXCurve = new AnimationCurve();
            general.posScreenYCurve = new AnimationCurve();

            general.posTargetOffsetXCurve = new AnimationCurve();
            general.posTargetOffsetYCurve = new AnimationCurve();
            general.posTargetOffsetZCurve = new AnimationCurve();

            general.rotScreenXCurve = new AnimationCurve();
            general.rotScreenYCurve = new AnimationCurve();

            general.rotTargetOffsetXCurve = new AnimationCurve();
            general.rotTargetOffsetYCurve = new AnimationCurve();
            general.rotTargetOffsetZCurve = new AnimationCurve();
        }
        else if (targetProfileSO is TrackingProfileSO tracking)
        {
            tracking.followOffsetXCurve = new AnimationCurve();
            tracking.followOffsetYCurve = new AnimationCurve();
            tracking.followOffsetZCurve = new AnimationCurve();

            tracking.rotScreenXCurve = new AnimationCurve();
            tracking.rotScreenYCurve = new AnimationCurve();

            tracking.rotTargetOffsetXCurve = new AnimationCurve();
            tracking.rotTargetOffsetYCurve = new AnimationCurve();
            tracking.rotTargetOffsetZCurve = new AnimationCurve();
        }
        else if (targetProfileSO is DollyProfileSO dollyProfile)
        {
        dollyProfile.splinePositionCurve = new AnimationCurve();

        dollyProfile.rotScreenXCurve = new AnimationCurve();
        dollyProfile.rotScreenYCurve = new AnimationCurve();

        dollyProfile.rotTargetOffsetXCurve = new AnimationCurve();
        dollyProfile.rotTargetOffsetYCurve = new AnimationCurve();
        dollyProfile.rotTargetOffsetZCurve = new AnimationCurve();

        dollyProfile.rotDampingX = rot.Damping.x;
        dollyProfile.rotDampingY = rot.Damping.y;
        }

        float duration = sourceAnimationClip.length;
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);

        for (int i = 0; i <= totalSamples; i++)
        {
            float currentTime = (float)i / sampleRate;

            if (currentTime > duration)
            {
                currentTime = duration;
            }

            float normalizedTime = duration > 0f
                ? currentTime / duration
                : 0f;

            sourceAnimationClip.SampleAnimation(vcam.gameObject, currentTime);

            targetProfileSO.fovCurve.AddKey(
                normalizedTime,
                vcam.Lens.FieldOfView
            );

            if (targetProfileSO is GeneralProfileSO g)
            {
                g.posDistanceCurve.AddKey(normalizedTime, pos.CameraDistance);

                g.posScreenXCurve.AddKey(normalizedTime, pos.Composition.ScreenPosition.x);
                g.posScreenYCurve.AddKey(normalizedTime, pos.Composition.ScreenPosition.y);

                g.posTargetOffsetXCurve.AddKey(normalizedTime, pos.TargetOffset.x);
                g.posTargetOffsetYCurve.AddKey(normalizedTime, pos.TargetOffset.y);
                g.posTargetOffsetZCurve.AddKey(normalizedTime, pos.TargetOffset.z);

                g.rotScreenXCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.x);
                g.rotScreenYCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.y);

                g.rotTargetOffsetXCurve.AddKey(normalizedTime, rot.TargetOffset.x);
                g.rotTargetOffsetYCurve.AddKey(normalizedTime, rot.TargetOffset.y);
                g.rotTargetOffsetZCurve.AddKey(normalizedTime, rot.TargetOffset.z);
            }
            else if (targetProfileSO is TrackingProfileSO t)
            {
                t.followOffsetXCurve.AddKey(normalizedTime, follow.FollowOffset.x);
                t.followOffsetYCurve.AddKey(normalizedTime, follow.FollowOffset.y);
                t.followOffsetZCurve.AddKey(normalizedTime, follow.FollowOffset.z);

                t.rotScreenXCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.x);
                t.rotScreenYCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.y);

                t.rotTargetOffsetXCurve.AddKey(normalizedTime, rot.TargetOffset.x);
                t.rotTargetOffsetYCurve.AddKey(normalizedTime, rot.TargetOffset.y);
                t.rotTargetOffsetZCurve.AddKey(normalizedTime, rot.TargetOffset.z);
            }
            else if (targetProfileSO is DollyProfileSO d)
            {
                d.splinePositionCurve.AddKey(
                    normalizedTime,
                    dollyComp.SplineSettings.Position
                );

                d.rotScreenXCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.x);
                d.rotScreenYCurve.AddKey(normalizedTime, rot.Composition.ScreenPosition.y);

                d.rotTargetOffsetXCurve.AddKey(normalizedTime, rot.TargetOffset.x);
                d.rotTargetOffsetYCurve.AddKey(normalizedTime, rot.TargetOffset.y);
                d.rotTargetOffsetZCurve.AddKey(normalizedTime, rot.TargetOffset.z);
            }
        }

        EditorUtility.SetDirty(targetProfileSO);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "烘焙成功",
            $"已將動態數據提取至：\n[{targetProfileSO.GetType().Name}] 資產中！",
            "確定"
        );
#endif
    }

    public void DeBakeSOToAnimationClip()
    {
#if UNITY_EDITOR
        if (targetProfileSO == null)
        {
            EditorUtility.DisplayDialog("提示", "請先指派 targetProfileSO 資產！", "確定");
            return;
        }

        AnimationClip newClip = new AnimationClip
        {
            frameRate = 60
        };

        string path = "";

        newClip.SetCurve(
            path,
            typeof(CinemachineCamera),
            "Lens.FieldOfView",
            targetProfileSO.fovCurve
        );

        if (targetProfileSO is GeneralProfileSO general)
        {
            System.Type posType = typeof(CinemachinePositionComposer);
            System.Type rotType = typeof(CinemachineRotationComposer);

            newClip.SetCurve(path, posType, "CameraDistance", general.posDistanceCurve);

            newClip.SetCurve(path, posType, "Composition.ScreenPosition.x", general.posScreenXCurve);
            newClip.SetCurve(path, posType, "Composition.ScreenPosition.y", general.posScreenYCurve);

            newClip.SetCurve(path, posType, "TargetOffset.x", general.posTargetOffsetXCurve);
            newClip.SetCurve(path, posType, "TargetOffset.y", general.posTargetOffsetYCurve);
            newClip.SetCurve(path, posType, "TargetOffset.z", general.posTargetOffsetZCurve);

            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.x", general.rotScreenXCurve);
            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.y", general.rotScreenYCurve);

            newClip.SetCurve(path, rotType, "TargetOffset.x", general.rotTargetOffsetXCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.y", general.rotTargetOffsetYCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.z", general.rotTargetOffsetZCurve);
        }
        else if (targetProfileSO is TrackingProfileSO tracking)
        {
            System.Type followType = typeof(CinemachineFollow);
            System.Type rotType = typeof(CinemachineRotationComposer);

            newClip.SetCurve(path, followType, "FollowOffset.x", tracking.followOffsetXCurve);
            newClip.SetCurve(path, followType, "FollowOffset.y", tracking.followOffsetYCurve);
            newClip.SetCurve(path, followType, "FollowOffset.z", tracking.followOffsetZCurve);

            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.x", tracking.rotScreenXCurve);
            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.y", tracking.rotScreenYCurve);

            newClip.SetCurve(path, rotType, "TargetOffset.x", tracking.rotTargetOffsetXCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.y", tracking.rotTargetOffsetYCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.z", tracking.rotTargetOffsetZCurve);
        }
        else if (targetProfileSO is DollyProfileSO dollyProfile)
        {
            System.Type dollyType = typeof(CinemachineSplineDolly);
            System.Type rotType = typeof(CinemachineRotationComposer);

            newClip.SetCurve(path, dollyType, "SplineSettings.Position", dollyProfile.splinePositionCurve);

            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.x", dollyProfile.rotScreenXCurve);
            newClip.SetCurve(path, rotType, "Composition.ScreenPosition.y", dollyProfile.rotScreenYCurve);

            newClip.SetCurve(path, rotType, "TargetOffset.x", dollyProfile.rotTargetOffsetXCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.y", dollyProfile.rotTargetOffsetYCurve);
            newClip.SetCurve(path, rotType, "TargetOffset.z", dollyProfile.rotTargetOffsetZCurve);
        }

        if (!Directory.Exists(animSavePath))
        {
            Directory.CreateDirectory(animSavePath);
        }

        string fullPath =
            $"{animSavePath}/{outputAnimName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.anim";

        AssetDatabase.CreateAsset(newClip, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = newClip;

        EditorUtility.DisplayDialog(
            "解凍成功",
            $"已成功將 [{targetProfileSO.name}] 還原為標準動畫檔！\n路徑: {fullPath}",
            "確定"
        );
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(CameraProfileWorkflow))]
public class CameraProfileWorkflowEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CameraProfileWorkflow workflow = target as CameraProfileWorkflow;

        if (workflow == null)
            return;

        DrawDefaultInspector();

        GUILayout.Space(15);
        GUILayout.Label("運鏡資產雙向控制台", EditorStyles.boldLabel);

        Color originalColor = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.15f, 0.7f, 0.35f);

        if (GUILayout.Button(" [正向] 一鍵烘焙 Timeline 動態至 SO 曲線", GUILayout.Height(40)))
        {
            workflow.BakeCurvesToSO();
        }

        GUILayout.Space(8);

        GUI.backgroundColor = new Color(0.9f, 0.45f, 0.1f);

        if (GUILayout.Button(" [逆向] 一鍵將 SO 數據解凍還原為 Animation Clip", GUILayout.Height(40)))
        {
            workflow.DeBakeSOToAnimationClip();
        }

        GUI.backgroundColor = originalColor;
    }
}
#endif