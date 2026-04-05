using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NiloToon.NiloToonURP
{
    [System.Serializable, VolumeComponentMenu("NiloToon/Screen Space Outline Control (NiloToon)")]
    public class NiloToonScreenSpaceOutlineControlVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Intensity")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0, 0, 1);
        [OverrideDisplayName("Intensity (Character)")]
        public ClampedFloatParameter intensityForCharacter = new ClampedFloatParameter(1, 0, 1);
        [OverrideDisplayName("Intensity (Environment)")]
        public ClampedFloatParameter intensityForEnvironment = new ClampedFloatParameter(1, 0, 1);

        [Header("Width")]
        [OverrideDisplayName("Width")]
        public ClampedFloatParameter widthMultiplier = new ClampedFloatParameter(1, 0, 10f);
        [OverrideDisplayName("Width (Character)")]
        public ClampedFloatParameter widthMultiplierForCharacter = new ClampedFloatParameter(1, 0, 10f);
        [OverrideDisplayName("Width (Environment)")]
        public ClampedFloatParameter widthMultiplierForEnvironment = new ClampedFloatParameter(1, 0, 10f);
        [OverrideDisplayName("Width (XR)")]
        public ClampedFloatParameter extraWidthMultiplierForXR = new ClampedFloatParameter(0.2f, 0, 10f);

        [Header("Normal Sensitivity Offset")]
        [OverrideDisplayName("Offset")]
        public ClampedFloatParameter normalsSensitivityOffset = new ClampedFloatParameter(0, -10f, 10f);
        [OverrideDisplayName("Offset (Character)")]
        public ClampedFloatParameter normalsSensitivityOffsetForCharacter = new ClampedFloatParameter(0, -10f, 10f);
        [OverrideDisplayName("Offset (Environment)")]
        public ClampedFloatParameter normalsSensitivityOffsetForEnvironment = new ClampedFloatParameter(0, -10f, 10f);

        [Header("Depth Sensitivity Offset")]
        [OverrideDisplayName("Offset")]
        public ClampedFloatParameter depthSensitivityOffset = new ClampedFloatParameter(0, -10f, 10f);
        [OverrideDisplayName("Offset (Character)")]
        public ClampedFloatParameter depthSensitivityOffsetForCharacter = new ClampedFloatParameter(0, -10f, 10f);
        [OverrideDisplayName("Offset (Environment)")]
        public ClampedFloatParameter depthSensitivityOffsetForEnvironment = new ClampedFloatParameter(0, -10f, 10f);

        [Header("Depth Sensitivity Distance Fadeout Strength")]
        [Tooltip("Fadeout depth sensitivity on far objects, to avoid artifact")]
        [OverrideDisplayName("Strength")]
        public ClampedFloatParameter depthSensitivityDistanceFadeoutStrength = new ClampedFloatParameter(1, 0, 10);
        [Tooltip("Fadeout depth sensitivity on far objects (for character shader), to avoid artifact")]
        [OverrideDisplayName("Strength (Character)")]
        public ClampedFloatParameter depthSensitivityDistanceFadeoutStrengthForCharacter = new ClampedFloatParameter(1, 0, 10);
        [Tooltip("Fadeout depth sensitivity on far objects (for environment shader), to avoid artifact")]
        [OverrideDisplayName("Strength (Environment")]
        public ClampedFloatParameter depthSensitivityDistanceFadeoutStrengthForEnvironment = new ClampedFloatParameter(1, 0, 10);

        [Header("Outline Tint Color")]
        [OverrideDisplayName("Tint Color")]
        public ColorParameter outlineTintColor = new ColorParameter(Color.white, true, false, true);
        [OverrideDisplayName("Tint Color (Character)")]
        public ColorParameter outlineTintColorForChar = new ColorParameter(Color.white, true, false, true);
        [OverrideDisplayName("Tint Color (Environment)")]
        public ColorParameter outlineTintColorForEnvi = new ColorParameter(new Color(0.12f, 0.12f, 0.12f), true, false, true);

        public bool IsActive() => intensity.value > 0.0f && widthMultiplier.value > 0.0f;

        public bool IsTileCompatible() => false;
    }
}

