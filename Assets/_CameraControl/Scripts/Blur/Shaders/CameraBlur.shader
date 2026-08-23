// Kawase blur rendering is derived from Unified Universal Blur by Luka Kldiashvili.
// See Assets/_CameraControl/ThirdParty/UnifiedUniversalBlur-LICENSE.txt.

Shader "Hidden/StageController/CameraBlur"
{
    HLSLINCLUDE
        #pragma editor_sync_compilation
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "CameraBlurCommon.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Kawase"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraKawaseBlur
            ENDHLSL
        }

        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraBlurCopy
            ENDHLSL
        }
    }
}
