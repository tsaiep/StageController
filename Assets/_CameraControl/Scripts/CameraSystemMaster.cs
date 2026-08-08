using UnityEngine;
using Unity.Cinemachine;

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
    }
}