using UnityEngine;
using Unity.Cinemachine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;
#endif

namespace Runtime.CameraSystem
{
    public class CameraSystemMaster : MonoBehaviour
    {
        [Header("--- General Cameras ---")]
        [Tooltip("General 運鏡使用的 A Camera。")]
        public CinemachineCamera generalCamera;

        [Tooltip("General 運鏡使用的 B Camera。請複製 General A 的 Cinemachine 組件設定。")]
        public CinemachineCamera generalCameraB;

        [Header("--- Tracking Camera ---")]
        [Tooltip("Tracking 運鏡使用的 Camera。")]
        public CinemachineCamera trackingCamera;

        [Header("--- Dolly Camera ---")]
        [Tooltip("Dolly 運鏡使用的 Camera。")]
        public CinemachineCamera dollyCamera;

        [Header("--- Priority Settings ---")]
        public int livePriority = 100;
        public int inactivePriority = 0;

        private void Awake()
        {
            if (generalCamera == null && trackingCamera == null && dollyCamera == null)
            {
                Debug.LogError(
                    $"[{nameof(CameraSystemMaster)}] 沒有指定任何 Cinemachine Camera，Camera Profile 系統無法運作。",
                    this
                );
            }

            if (generalCamera != null && generalCameraB == null)
            {
                Debug.LogWarning(
                    $"[{nameof(CameraSystemMaster)}] General Camera B 尚未指定。General → General 連續 Clip 會退回單台 General Camera，可能仍會看到旋轉殘留。",
                    this
                );
            }
        }

        public CinemachineCamera GetGeneralCamera(bool useB)
        {
            if (useB && generalCameraB != null)
                return generalCameraB;

            return generalCamera;
        }

        public void SetOnlyThisCameraLive(CinemachineCamera liveCamera)
        {
            SetCameraPriority(generalCamera, liveCamera);
            SetCameraPriority(generalCameraB, liveCamera);
            SetCameraPriority(trackingCamera, liveCamera);
            SetCameraPriority(dollyCamera, liveCamera);
        }

        public void DisableAllCameras()
        {
            SetCameraPriority(generalCamera, null);
            SetCameraPriority(generalCameraB, null);
            SetCameraPriority(trackingCamera, null);
            SetCameraPriority(dollyCamera, null);
        }

        private void SetCameraPriority(CinemachineCamera camera, CinemachineCamera liveCamera)
        {
            if (camera == null)
                return;

            camera.Priority.Value = camera == liveCamera
                ? livePriority
                : inactivePriority;
        }

#if UNITY_EDITOR
        private enum CameraRigKind
        {
            General,
            Tracking,
            Dolly
        }

        private enum DebugSeverity
        {
            Warning,
            Error
        }

        private readonly struct CameraSlot
        {
            public readonly string Label;
            public readonly string FieldName;
            public readonly CameraRigKind Kind;
            public readonly bool Required;
            public readonly CinemachineCamera Camera;

            public CameraSlot(
                string label,
                string fieldName,
                CameraRigKind kind,
                bool required,
                CinemachineCamera camera)
            {
                Label = label;
                FieldName = fieldName;
                Kind = kind;
                Required = required;
                Camera = camera;
            }
        }

        private readonly struct DebugIssue
        {
            public readonly DebugSeverity Severity;
            public readonly string Message;
            public readonly Object Context;

            public DebugIssue(DebugSeverity severity, string message, Object context)
            {
                Severity = severity;
                Message = message;
                Context = context;
            }
        }

        public void DebugValidateCameraSetup()
        {
            List<DebugIssue> issues = CollectCameraSetupIssues();

            if (issues.Count == 0)
            {
                Debug.Log(
                    $"[{nameof(CameraSystemMaster)}] Camera setup 檢查完成：所有必要 Cinemachine Camera 與組件設定正常。",
                    this
                );
                return;
            }

            foreach (DebugIssue issue in issues)
            {
                switch (issue.Severity)
                {
                    case DebugSeverity.Error:
                        Debug.LogError(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;

                    case DebugSeverity.Warning:
                        Debug.LogWarning(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;

                    default:
                        Debug.Log(
                            $"[{nameof(CameraSystemMaster)}] {issue.Message}",
                            issue.Context != null ? issue.Context : this
                        );
                        break;
                }
            }
        }

        public void DebugAutoFixCameraSetup()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Auto Fix Camera System Master Setup");

            Undo.RecordObject(this, "Auto Fix Camera System Master Setup");

            if (generalCamera == null)
            {
                generalCamera = CreateCameraRig("CinemachineCamera_General_A");
            }

            if (generalCameraB == null)
            {
                generalCameraB = CreateCameraRig("CinemachineCamera_General_B");
            }

            if (trackingCamera == null)
            {
                trackingCamera = CreateCameraRig("CinemachineCamera_Tracking");
            }

            if (dollyCamera == null)
            {
                dollyCamera = CreateCameraRig("CinemachineCamera_Dolly");
            }

            EditorUtility.SetDirty(this);

            FixGeneralCamera(generalCamera, null);
            FixGeneralCamera(generalCameraB, generalCamera);
            FixTrackingCamera(trackingCamera);
            FixDollyCamera(dollyCamera);

            Undo.CollapseUndoOperations(undoGroup);

            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }

            Debug.Log(
                $"[{nameof(CameraSystemMaster)}] 已自動建立/補上 CameraSystemMaster 的 Cinemachine Camera 與必要組件。請重新按一次檢查確認細節。",
                this
            );
        }

        private List<DebugIssue> CollectCameraSetupIssues()
        {
            List<DebugIssue> issues = new List<DebugIssue>();

            if (livePriority <= inactivePriority)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"Priority 設定錯誤：livePriority ({livePriority}) 必須大於 inactivePriority ({inactivePriority})。",
                    this
                ));
            }

            foreach (CameraSlot slot in GetCameraSlots())
            {
                ValidateCameraSlot(slot, issues);
            }

            ValidateGeneralCameraPair(issues);

            return issues;
        }

        private CameraSlot[] GetCameraSlots()
        {
            return new[]
            {
                new CameraSlot("General Camera A", nameof(generalCamera), CameraRigKind.General, true, generalCamera),
                new CameraSlot("General Camera B", nameof(generalCameraB), CameraRigKind.General, false, generalCameraB),
                new CameraSlot("Tracking Camera", nameof(trackingCamera), CameraRigKind.Tracking, true, trackingCamera),
                new CameraSlot("Dolly Camera", nameof(dollyCamera), CameraRigKind.Dolly, true, dollyCamera)
            };
        }

        private void ValidateCameraSlot(CameraSlot slot, List<DebugIssue> issues)
        {
            if (slot.Camera == null)
            {
                issues.Add(new DebugIssue(
                    slot.Required ? DebugSeverity.Error : DebugSeverity.Warning,
                    $"{slot.Label} ({slot.FieldName}) 尚未綁定 CinemachineCamera。",
                    this
                ));
                return;
            }

            if (!slot.Camera.gameObject.activeInHierarchy)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 綁定的物件未啟用，Timeline 可能無法切到這台 camera。",
                    slot.Camera
                ));
            }

            if (!slot.Camera.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 的 CinemachineCamera component 被停用。",
                    slot.Camera
                ));
            }

            if (!slot.Camera.Priority.Enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 的 Priority 尚未啟用。CameraSystemMaster 會用 priority 切換 live camera。",
                    slot.Camera
                ));
            }

            ValidateRequiredComponent<CinemachineRotationComposer>(
                slot,
                issues,
                "Rotation Composer"
            );

            switch (slot.Kind)
            {
                case CameraRigKind.General:
                    ValidateRequiredComponent<CinemachinePositionComposer>(
                        slot,
                        issues,
                        "Position Composer"
                    );
                    ValidateConflictingBody<CinemachinePositionComposer>(
                        slot,
                        issues
                    );
                    break;

                case CameraRigKind.Tracking:
                    ValidateRequiredComponent<CinemachineFollow>(
                        slot,
                        issues,
                        "Cinemachine Follow"
                    );
                    ValidateConflictingBody<CinemachineFollow>(
                        slot,
                        issues
                    );
                    break;

                case CameraRigKind.Dolly:
                    CinemachineSplineDolly dolly =
                        ValidateRequiredComponent<CinemachineSplineDolly>(
                            slot,
                            issues,
                            "Spline Dolly"
                        );

                    ValidateConflictingBody<CinemachineSplineDolly>(
                        slot,
                        issues
                    );

                    if (dolly != null && dolly.Spline == null)
                    {
                        issues.Add(new DebugIssue(
                            DebugSeverity.Warning,
                            $"{slot.Label} 的 Spline Dolly 尚未指定 Spline。若 Timeline clip 會動態提供 spline 可忽略，否則 Dolly camera 不會移動。",
                            dolly
                        ));
                    }
                    break;
            }
        }

        private T ValidateRequiredComponent<T>(
            CameraSlot slot,
            List<DebugIssue> issues,
            string displayName) where T : Behaviour
        {
            T component = slot.Camera.GetComponent<T>();

            if (component == null)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 缺少必要組件：{displayName}。",
                    slot.Camera
                ));
                return null;
            }

            if (!component.enabled)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    $"{slot.Label} 的必要組件 {displayName} 被停用。",
                    component
                ));
            }

            return component;
        }

        private void ValidateConflictingBody<TExpected>(
            CameraSlot slot,
            List<DebugIssue> issues) where TExpected : CinemachineComponentBase
        {
            CinemachineComponentBase[] components =
                slot.Camera.GetComponents<CinemachineComponentBase>();

            foreach (CinemachineComponentBase component in components)
            {
                if (component == null ||
                    component.Stage != CinemachineCore.Stage.Body ||
                    component is TExpected ||
                    !component.enabled)
                {
                    continue;
                }

                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    $"{slot.Label} 有額外啟用的 Body 組件：{component.GetType().Name}。同一台 CinemachineCamera 同時啟用多個 Body 組件時，只會有一個被 pipeline 採用，可能不是預期設定。",
                    component
                ));
            }
        }

        private void ValidateGeneralCameraPair(List<DebugIssue> issues)
        {
            if (generalCamera == null || generalCameraB == null)
                return;

            if (generalCamera == generalCameraB)
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Error,
                    "General Camera A 與 B 指到同一台 CinemachineCamera。General -> General 連續 clip 需要兩台不同 camera。",
                    generalCamera
                ));
                return;
            }

            if (!HasMatchingComposerSetup(generalCamera, generalCameraB))
            {
                issues.Add(new DebugIssue(
                    DebugSeverity.Warning,
                    "General Camera B 的 Position/Rotation Composer 設定與 General Camera A 不一致。建議按自動修復同步一次，避免 General A/B 交替時狀態差異。",
                    generalCameraB
                ));
            }
        }

        private bool HasMatchingComposerSetup(
            CinemachineCamera source,
            CinemachineCamera target)
        {
            CinemachinePositionComposer sourcePosition =
                source.GetComponent<CinemachinePositionComposer>();
            CinemachinePositionComposer targetPosition =
                target.GetComponent<CinemachinePositionComposer>();
            CinemachineRotationComposer sourceRotation =
                source.GetComponent<CinemachineRotationComposer>();
            CinemachineRotationComposer targetRotation =
                target.GetComponent<CinemachineRotationComposer>();

            if (sourcePosition == null ||
                targetPosition == null ||
                sourceRotation == null ||
                targetRotation == null)
            {
                return false;
            }

            return Mathf.Approximately(sourcePosition.CameraDistance, targetPosition.CameraDistance) &&
                sourcePosition.Composition.ScreenPosition == targetPosition.Composition.ScreenPosition &&
                sourcePosition.TargetOffset == targetPosition.TargetOffset &&
                sourcePosition.Damping == targetPosition.Damping &&
                sourceRotation.Composition.ScreenPosition == targetRotation.Composition.ScreenPosition &&
                sourceRotation.TargetOffset == targetRotation.TargetOffset &&
                sourceRotation.Damping == targetRotation.Damping;
        }

        private CinemachineCamera CreateCameraRig(string objectName)
        {
            GameObject cameraObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(cameraObject, $"Create {objectName}");

            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.transform.localScale = Vector3.one;

            CinemachineCamera camera =
                Undo.AddComponent<CinemachineCamera>(cameraObject);

            camera.Priority.Value = inactivePriority;

            return camera;
        }

        private void FixGeneralCamera(
            CinemachineCamera camera,
            CinemachineCamera copyFrom)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachinePositionComposer position =
                EnsureComponent<CinemachinePositionComposer>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);

            DisableConflictingBodyComponents<CinemachinePositionComposer>(camera);

            if (copyFrom != null)
            {
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachinePositionComposer>(),
                    position
                );
                CopyComponentSettings(
                    copyFrom.GetComponent<CinemachineRotationComposer>(),
                    rotation
                );
                CopyCameraCoreSettings(copyFrom, camera);
            }
            else
            {
                Undo.RecordObject(position, "Configure General Camera Position Composer");
                position.CameraDistance = Mathf.Max(0.01f, position.CameraDistance);
                position.Damping = Vector3.one;

                Undo.RecordObject(rotation, "Configure General Camera Rotation Composer");
                rotation.Damping = Vector2.one;
            }

            EditorUtility.SetDirty(position);
            EditorUtility.SetDirty(rotation);
        }

        private void FixTrackingCamera(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachineFollow follow =
                EnsureComponent<CinemachineFollow>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);

            DisableConflictingBodyComponents<CinemachineFollow>(camera);

            Undo.RecordObject(follow, "Configure Tracking Camera Follow");

            if (follow.FollowOffset == Vector3.zero)
            {
                follow.FollowOffset = new Vector3(0f, 0.1f, 1f);
            }

            Undo.RecordObject(rotation, "Configure Tracking Camera Rotation Composer");
            rotation.Damping = Vector2.one;

            EditorUtility.SetDirty(follow);
            EditorUtility.SetDirty(rotation);
        }

        private void FixDollyCamera(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            FixCommonCameraSettings(camera);

            CinemachineSplineDolly dolly =
                EnsureComponent<CinemachineSplineDolly>(camera);
            CinemachineRotationComposer rotation =
                EnsureComponent<CinemachineRotationComposer>(camera);

            DisableConflictingBodyComponents<CinemachineSplineDolly>(camera);

            Undo.RecordObject(dolly, "Configure Dolly Camera Spline Dolly");
            dolly.PositionUnits = UnityEngine.Splines.PathIndexUnit.Normalized;
            dolly.CameraRotation = CinemachineSplineDolly.RotationMode.Default;

            Undo.RecordObject(rotation, "Configure Dolly Camera Rotation Composer");
            rotation.TargetOffset = new Vector3(0f, 1f, 0f);
            rotation.Damping = Vector2.zero;

            EditorUtility.SetDirty(dolly);
            EditorUtility.SetDirty(rotation);
        }

        private void FixCommonCameraSettings(CinemachineCamera camera)
        {
            Undo.RecordObject(camera.gameObject, "Configure Cinemachine Camera GameObject");
            Undo.RecordObject(camera, "Configure Cinemachine Camera");

            if (!camera.gameObject.activeSelf)
            {
                camera.gameObject.SetActive(true);
            }

            camera.enabled = true;
            camera.Priority.Value = inactivePriority;

            if (camera.Lens.FieldOfView < 10f || camera.Lens.FieldOfView > 120f)
            {
                camera.Lens.FieldOfView = Mathf.Clamp(camera.Lens.FieldOfView, 10f, 120f);
            }

            EditorUtility.SetDirty(camera);
        }

        private T EnsureComponent<T>(CinemachineCamera camera) where T : Behaviour
        {
            T component = camera.GetComponent<T>();

            if (component == null)
            {
                component = Undo.AddComponent<T>(camera.gameObject);
            }

            Undo.RecordObject(component, $"Configure {typeof(T).Name}");
            component.enabled = true;

            return component;
        }

        private void DisableConflictingBodyComponents<TExpected>(
            CinemachineCamera camera) where TExpected : CinemachineComponentBase
        {
            CinemachineComponentBase[] components =
                camera.GetComponents<CinemachineComponentBase>();

            foreach (CinemachineComponentBase component in components)
            {
                if (component == null ||
                    component.Stage != CinemachineCore.Stage.Body ||
                    component is TExpected ||
                    !component.enabled)
                {
                    continue;
                }

                Undo.RecordObject(component, "Disable Conflicting Cinemachine Body Component");
                component.enabled = false;
                EditorUtility.SetDirty(component);
            }
        }

        private void CopyCameraCoreSettings(
            CinemachineCamera source,
            CinemachineCamera target)
        {
            if (source == null || target == null)
                return;

            Undo.RecordObject(target, "Copy Cinemachine Camera Settings");
            target.Lens = source.Lens;
            target.OutputChannel = source.OutputChannel;
            target.StandbyUpdate = source.StandbyUpdate;
            target.BlendHint = source.BlendHint;
            target.Target = source.Target;
            target.Priority.Value = inactivePriority;
            EditorUtility.SetDirty(target);
        }

        private void CopyComponentSettings<T>(T source, T target) where T : Component
        {
            if (source == null || target == null)
                return;

            Undo.RecordObject(target, $"Copy {typeof(T).Name} Settings");
            EditorUtility.CopySerialized(source, target);

            if (target is Behaviour behaviour)
            {
                behaviour.enabled = true;
            }

            EditorUtility.SetDirty(target);
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CameraSystemMaster))]
    public class CameraSystemMasterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CameraSystemMaster master = target as CameraSystemMaster;

            if (master == null)
                return;

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Camera Debug Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("檢查 Cinemachine Camera 設定", GUILayout.Height(32f)))
            {
                master.DebugValidateCameraSetup();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("自動建立/補上 Camera 設定", GUILayout.Height(32f)))
            {
                master.DebugAutoFixCameraSetup();
            }
        }
    }
#endif
}
