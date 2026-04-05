using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace NiloToon.NiloToonURP
{
    [System.Serializable, VolumeComponentMenu("NiloToon/Cinematic Rim Light (NiloToon)")]
    //[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [HelpURL("https://docs.google.com/document/d/1iEh1E5xLXnXuICM0ElV3F3x_J2LND9Du2SSKzcIuPXw/edit?pli=1&tab=t.0#heading=h.aapeix6wksbg")]
    public class NiloToonCinematicRimLightVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("Rim (2D Style)(= Default style if all style's strength are 0)")]
        [OverrideDisplayName("Strength")]
        public ClampedFloatParameter strengthRimMask2D = new ClampedFloatParameter(0, 0, 1);

        [Header("Rim (3D Classic Style)")]
        [OverrideDisplayName("Strength")]
        public ClampedFloatParameter strengthRimMask3D_ClassicStyle = new ClampedFloatParameter(0, 0, 1);
        [OverrideDisplayName("    Rim Width")]
        public ClampedFloatParameter widthRimMask3D_ClassicStyle = new ClampedFloatParameter(0.3f, 0, 1);
        [OverrideDisplayName("    Rim Blur")]
        public ClampedFloatParameter blurRimMask3D_ClassicStyle = new ClampedFloatParameter(0.02f, 0, 1);
        [OverrideDisplayName("    Rim Sharpness")]
        public ClampedFloatParameter sharpnessRimMask3D_ClassicStyle = new ClampedFloatParameter(0f, 0, 1);

        [FormerlySerializedAs("strengthRimMask3D_DynmaicStyle")]
        [Header("Rim (3D Dynamic Style)")]
        [OverrideDisplayName("Strength")]
        public ClampedFloatParameter strengthRimMask3D_DynamicStyle = new ClampedFloatParameter(0, 0, 1);
        [OverrideDisplayName("    Rim Width")]
        public ClampedFloatParameter widthRimMask3D_DynamicStyle = new ClampedFloatParameter(0.5f, 0, 1);
        [OverrideDisplayName("    Rim Blur")]
        public ClampedFloatParameter blurRimMask3D_DynamicStyle = new ClampedFloatParameter(0.5f, 0, 1);
        [OverrideDisplayName("    Rim Sharpness")]
        public ClampedFloatParameter sharpnessRimMask3D_DynamicStyle = new ClampedFloatParameter(0.375f, 0, 1);

        [Header("Rim (3D BackLight only Style)")]
        [OverrideDisplayName("Strength")]
        public ClampedFloatParameter strengthRimMask3D_StableStyle = new ClampedFloatParameter(0, 0, 1);
        [OverrideDisplayName("    Rim Sharpness")]
        public ClampedFloatParameter sharpnessRimMask3D_StableStyle = new ClampedFloatParameter(0.5f, 0, 1);

        [Header("--------------------------------------------------------------------------------------------------------------------------------------")]
        [Header("Rim Light Intensity & color style")]
        [OverrideDisplayName("Intensity")]
        public MinFloatParameter lightIntensityMultiplier = new MinFloatParameter(1f, 0);
        [OverrideDisplayName("Tint BaseMap?")]
        public ClampedFloatParameter tintBaseMap = new ClampedFloatParameter(0.5f, 0, 1);
        
        [Header("--------------------------------------------------------------------------------------------------------------------------------------")]
        [Header("Style Safe guard")]
        [OverrideDisplayName("Auto fix unsafe Style?")]
        public BoolParameter enableStyleSafeGuard = new (true, false);

        public bool IsActive() => true;

        public bool IsTileCompatible() => false;
    }
}

