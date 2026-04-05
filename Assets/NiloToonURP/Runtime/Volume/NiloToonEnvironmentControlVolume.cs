using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NiloToon.NiloToonURP
{
    [System.Serializable, VolumeComponentMenu("NiloToon/Environment Control (NiloToon)")]
    public class NiloToonEnvironmentControlVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("GI Edit")]
        [OverrideDisplayName("Tint Color")]
        public ColorParameter GlobalIlluminationTintColor = new ColorParameter(Color.white, true, false, true);
        [OverrideDisplayName("Add Color")]
        public ColorParameter GlobalIlluminationAddColor = new ColorParameter(Color.black, true, false, true);

        [Header("GI Override")]
        [OverrideDisplayName("Override Color")]
        [Tooltip("Alpha = override strength")]
        public ColorParameter GlobalIlluminationOverrideColor = new ColorParameter(Color.clear,true,true,true);

        [Header("Albedo Override")]
        [OverrideDisplayName("Override Color")]
        [Tooltip("Alpha = override strength")]
        public ColorParameter GlobalAlbedoOverrideColor = new ColorParameter(new Color(1, 1, 1, 0), true, true, true);

        [Header("Surface Color Result Override")]
        [OverrideDisplayName("Override Color")]
        [Tooltip("Alpha = override strength")]
        public ColorParameter GlobalSurfaceColorResultOverrideColor = new ColorParameter(new Color(1, 1, 1, 0), true, true, true);

        [Header("Shadow Boarder Tint Color")]
        [OverrideDisplayName("Override Color")]
        [Tooltip("Alpha = override strength")]
        public ColorParameter GlobalShadowBoaderTintColorOverrideColor = new ColorParameter(new Color(0, 0, 0, 0), true, true, true);

        public bool IsActive() => true;

        public bool IsTileCompatible() => false;
    }
}

