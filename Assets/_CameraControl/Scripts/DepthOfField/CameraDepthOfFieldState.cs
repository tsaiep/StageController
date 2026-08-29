using System.Collections.Generic;
using UnityEngine;

namespace Runtime.CameraSystem
{
    public enum CameraDepthOfFieldDebugView
    {
        Final = 0,
        NearMask = 1,
        FarMask = 2,
        FocusPlane = 3
    }

    /// <summary>
    /// Runtime values consumed by CameraDepthOfFieldRendererFeature.
    /// Blur radii are expressed in full-resolution pixels at 1080p.
    /// </summary>
    public struct CameraDepthOfFieldSettings
    {
        public bool Enabled;
        public float FocusDistance;
        public float NearFocusRange;
        public float FarFocusRange;
        public float NearBlurRadius;
        public float FarBlurRadius;
        public float Intensity;
        public CameraDepthOfFieldDebugView DebugView;

        public bool IsActive =>
            Enabled &&
            Intensity > 0.0001f &&
            (DebugView != CameraDepthOfFieldDebugView.Final ||
             NearBlurRadius > 0.0001f ||
             FarBlurRadius > 0.0001f);

        public CameraDepthOfFieldSettings Sanitized()
        {
            CameraDepthOfFieldSettings value = this;
            value.FocusDistance = Mathf.Max(0.01f, FocusDistance);
            value.NearFocusRange = Mathf.Max(0.01f, NearFocusRange);
            value.FarFocusRange = Mathf.Max(0.01f, FarFocusRange);
            value.NearBlurRadius = Mathf.Clamp(NearBlurRadius, 0f, 64f);
            value.FarBlurRadius = Mathf.Clamp(FarBlurRadius, 0f, 64f);
            value.Intensity = Mathf.Clamp01(Intensity);

            int debugView = (int)DebugView;
            value.DebugView = debugView >= (int)CameraDepthOfFieldDebugView.Final &&
                debugView <= (int)CameraDepthOfFieldDebugView.FocusPlane
                ? DebugView
                : CameraDepthOfFieldDebugView.Final;
            return value;
        }
    }

    /// <summary>
    /// Per-camera DOF state without scene components. Timeline preview can
    /// therefore drive the effect without dirtying the current scene.
    /// </summary>
    public static class CameraDepthOfFieldState
    {
        private static readonly Dictionary<Camera, CameraDepthOfFieldSettings>
            States = new Dictionary<Camera, CameraDepthOfFieldSettings>();

        public static void Set(Camera camera, CameraDepthOfFieldSettings settings)
        {
            if (camera == null)
                return;

            settings = settings.Sanitized();

            if (!settings.IsActive)
            {
                States.Remove(camera);
                return;
            }

            States[camera] = settings;
        }

        public static bool TryGet(
            Camera camera,
            out CameraDepthOfFieldSettings settings)
        {
            if (camera != null && States.TryGetValue(camera, out settings))
            {
                if (settings.IsActive)
                    return true;

                States.Remove(camera);
            }

            settings = default;
            return false;
        }

        public static void Clear(Camera camera)
        {
            if (camera != null)
                States.Remove(camera);
        }

        public static void ClearAll()
        {
            States.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            States.Clear();
        }
    }
}
