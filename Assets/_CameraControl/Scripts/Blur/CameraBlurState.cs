using UnityEngine;

namespace Runtime.CameraSystem
{
    /// <summary>
    /// Stores the blur intensity for one Unity Camera. The value is deliberately
    /// runtime-only so Timeline preview does not dirty scenes or prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraBlurState : MonoBehaviour
    {
        [System.NonSerialized]
        private float _intensity;

        [System.NonSerialized]
        private float _blendWeight;

        public float Intensity => _intensity;
        public float BlendWeight => _blendWeight;

        public void SetIntensity(float intensity)
        {
            SetBlur(intensity, intensity > 0f ? 1f : 0f);
        }

        public void SetBlur(float intensity, float blendWeight)
        {
            _intensity = Mathf.Max(0f, intensity);
            _blendWeight = Mathf.Clamp01(blendWeight);
        }

        public void Clear()
        {
            _intensity = 0f;
            _blendWeight = 0f;
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
