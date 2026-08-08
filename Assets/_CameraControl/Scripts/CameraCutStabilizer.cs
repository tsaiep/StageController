using UnityEngine;
using Unity.Cinemachine;

namespace Runtime.CameraSystem
{
    public class CameraCutStabilizer : MonoBehaviour
    {
        [Header("--- Stabilizer Settings ---")]
        public bool enableStabilizer = true;

        [Min(0)]
        public int dampingSuppressionFrames = 3;

        public bool resetPreviousState = true;

        [Header("--- Cinemachine Snap ---")]
        public bool useCancelDamping = true;
        public bool updateImmediately = true;

        [Header("--- Damping Suppression ---")]
        public bool suppressPositionComposerDamping = true;
        public bool suppressRotationComposerDamping = true;
        public bool suppressFollowDamping = true;

        private CinemachineCamera _stabilizingCamera;
        private int _suppressUntilFrame = -1;

        public void BeginHardCut(CinemachineCamera incomingCamera)
        {
            if (!Application.isPlaying)
                return;

            if (!enableStabilizer)
                return;

            if (incomingCamera == null)
                return;

            _stabilizingCamera = incomingCamera;
            _suppressUntilFrame = Time.frameCount + dampingSuppressionFrames;

            ResetPreviousState(incomingCamera);
        }

        public bool IsSuppressing(CinemachineCamera camera)
        {
            if (!Application.isPlaying)
                return false;

            if (!enableStabilizer)
                return false;

            if (camera == null)
                return false;

            if (_stabilizingCamera == null)
                return false;

            if (camera != _stabilizingCamera)
                return false;

            if (Time.frameCount > _suppressUntilFrame)
            {
                Cancel();
                return false;
            }

            return true;
        }

        public void ApplyStateResetIfNeeded(CinemachineCamera camera)
        {
            if (!IsSuppressing(camera))
                return;

            ResetPreviousState(camera);
        }

        public void ForceSnapIfNeeded(CinemachineCamera camera)
        {
            if (!IsSuppressing(camera))
                return;

            ResetPreviousState(camera);

            if (useCancelDamping)
            {
                camera.CancelDamping(updateImmediately);
            }
        }

        public void Cancel()
        {
            _stabilizingCamera = null;
            _suppressUntilFrame = -1;
        }

        private void ResetPreviousState(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            if (resetPreviousState)
            {
                camera.PreviousStateIsValid = false;
            }
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}