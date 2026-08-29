Shader "Hidden/StageController/CameraDepthOfField"
{
    HLSLINCLUDE
        #pragma target 4.5
        #pragma editor_sync_compilation

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_DofOriginalTexture);
        TEXTURE2D_X(_DofNearTexture);
        TEXTURE2D_X(_DofFarTexture);

        float4 _DofParams;
        float4 _DofKernel;
        float4 _DofSourceTexelSize;
        float _DofDebugMode;

        #define DOF_FOCUS_DISTANCE _DofParams.x
        #define DOF_NEAR_RANGE _DofParams.y
        #define DOF_FAR_RANGE _DofParams.z
        #define DOF_INTENSITY _DofParams.w

        half4 SampleDofSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                uv
            );
        }

        float GetDofEyeDepth(float2 uv)
        {
            // uv is already stereo transformed by the fragment entry point.
            float rawDepth = SAMPLE_TEXTURE2D_X(
                _CameraDepthTexture,
                sampler_PointClamp,
                uv
            ).r;
            return LinearEyeDepth(rawDepth, _ZBufferParams);
        }

        half2 GetSignedDofWeights(float2 uv)
        {
            float eyeDepth = GetDofEyeDepth(uv);
            float nearWeight = saturate(
                (DOF_FOCUS_DISTANCE - eyeDepth) /
                max(DOF_NEAR_RANGE, 0.0001)
            );
            float farWeight = saturate(
                (eyeDepth - DOF_FOCUS_DISTANCE) /
                max(DOF_FAR_RANGE, 0.0001)
            );

            return half2(nearWeight, farWeight) *
                saturate(DOF_INTENSITY);
        }

        half4 PrefilterLayer(float2 uv, bool useNearLayer)
        {
            const float2 offsets[4] =
            {
                float2(-0.5, -0.5),
                float2( 0.5, -0.5),
                float2(-0.5,  0.5),
                float2( 0.5,  0.5)
            };

            half4 result = 0.0;

            UNITY_UNROLL
            for (int i = 0; i < 4; i++)
            {
                float2 sampleUv = uv + offsets[i] *
                    _DofSourceTexelSize.xy;
                half4 color = SampleDofSource(sampleUv);
                half2 weights = GetSignedDofWeights(sampleUv);
                half weight = useNearLayer ? weights.x : weights.y;

                result.rgb += color.rgb * weight;
                result.a += weight;
            }

            return result * 0.25;
        }

        half4 CameraDofPrefilterNear(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            return PrefilterLayer(uv, true);
        }

        half4 CameraDofPrefilterFar(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            return PrefilterLayer(uv, false);
        }

        half4 CameraDofDilate(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float2 direction = _DofKernel.xy;
            float radius = max(0.0, _DofKernel.z);

            half4 selected = SampleDofSource(uv);

            const float offsets[9] =
            {
                -1.0, -0.75, -0.5, -0.25, 0.0,
                 0.25, 0.5, 0.75, 1.0
            };

            UNITY_UNROLL
            for (int i = 0; i < 9; i++)
            {
                float2 sampleUv = uv + direction * offsets[i] * radius *
                    _DofSourceTexelSize.xy;
                half4 sampleValue = SampleDofSource(sampleUv);
                half distanceFade = 1.0h - saturate(abs(offsets[i]));
                half candidateCoverage = sampleValue.a * distanceFade;

                if (candidateCoverage > selected.a)
                {
                    half coverageScale = candidateCoverage /
                        max(sampleValue.a, 0.0001h);
                    selected = half4(
                        sampleValue.rgb * coverageScale,
                        candidateCoverage
                    );
                }
            }

            return selected;
        }

        half4 CameraDofBlur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float radius = max(0.0, _DofKernel.z);
            float2 stepUv = _DofKernel.xy * radius *
                _DofSourceTexelSize.xy;
            half4 centerSample = SampleDofSource(uv);
            half2 centerWeights = GetSignedDofWeights(uv);
            bool useNearLayer = _DofKernel.w < -0.5;
            bool useFarDepthRejection = _DofKernel.w > 0.5;
            half centerCoverage = useNearLayer
                ? max(centerWeights.x, centerSample.a)
                : centerWeights.y;

            if (radius <= 0.0001 || centerCoverage <= 0.0001h)
                return centerSample;

            // Keep all taps at stable positions, but continuously resize the
            // Gaussian with CoC. This produces an actual intermediate blur
            // instead of overlaying a sharp frame with one max-radius frame.
            float sigma = max((float)centerCoverage / 3.0, 0.02);
            float inverseTwoSigmaSquared = rcp(2.0 * sigma * sigma);
            half4 result = 0.0;
            half totalWeight = 0.0;
            float centerDepth = GetDofEyeDepth(uv);
            float depthTolerance = max(
                max(DOF_FAR_RANGE * 0.1, centerDepth * 0.002),
                0.05
            );

            UNITY_UNROLL
            for (int i = 0; i < 25; i++)
            {
                float offset = ((float)i - 12.0) / 12.0;
                float2 sampleUv = uv + stepUv * offset;
                half sampleWeight = (half)exp2(
                    -offset * offset * inverseTwoSigmaSquared * 1.442695
                );

                if (useFarDepthRejection)
                {
                    float sampleDepth = GetDofEyeDepth(sampleUv);
                    half depthWeight = saturate(
                        1.0 - max(sampleDepth - centerDepth, 0.0) /
                        depthTolerance
                    );
                    sampleWeight *= depthWeight * depthWeight;
                }

                result += SampleDofSource(sampleUv) * sampleWeight;
                totalWeight += sampleWeight;
            }

            return result / max(totalWeight, 0.0001h);
        }

        half SurfaceCoverageToBlend(half coverage)
        {
            // The blurred layer already uses a CoC-sized kernel. Replace the
            // sharp pixel early instead of leaving an obvious sharp ghost
            // underneath a wide translucent blur.
            return smoothstep(0.001h, 0.35h, saturate(coverage));
        }

        half4 CameraDofComposite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

            half4 original = SAMPLE_TEXTURE2D_X(
                _DofOriginalTexture,
                sampler_LinearClamp,
                uv
            );
            half2 centerWeights = GetSignedDofWeights(uv);

            if (_DofDebugMode > 0.5 && _DofDebugMode < 1.5)
                return half4(centerWeights.xxx, 1.0h);

            if (_DofDebugMode > 1.5 && _DofDebugMode < 2.5)
                return half4(centerWeights.yyy, 1.0h);

            if (_DofDebugMode > 2.5)
            {
                float eyeDepth = GetDofEyeDepth(uv);
                float distanceToPlane = abs(
                    eyeDepth - DOF_FOCUS_DISTANCE
                );
                float baseWidth = max(
                    0.01,
                    max(
                        min(DOF_NEAR_RANGE, DOF_FAR_RANGE) * 0.05,
                        DOF_FOCUS_DISTANCE * 0.002
                    )
                );
                float derivativeWidth = min(
                    fwidth(eyeDepth) * 1.5,
                    baseWidth * 4.0
                );
                float planeWidth = max(baseWidth, derivativeWidth);
                half plane = 1.0h - smoothstep(
                    planeWidth,
                    planeWidth * 2.0,
                    distanceToPlane
                );
                half luminance = dot(
                    original.rgb,
                    half3(0.2126h, 0.7152h, 0.0722h)
                );
                half3 background = luminance.xxx * 0.25h;
                return half4(
                    lerp(background, half3(0.1h, 1.0h, 0.15h), plane),
                    1.0h
                );
            }

            half4 nearLayer = SAMPLE_TEXTURE2D_X(
                _DofNearTexture,
                sampler_LinearClamp,
                uv
            );
            half4 farLayer = SAMPLE_TEXTURE2D_X(
                _DofFarTexture,
                sampler_LinearClamp,
                uv
            );

            half3 farColor = farLayer.a > 0.0001h
                ? farLayer.rgb / farLayer.a
                : original.rgb;
            half farBlend = SurfaceCoverageToBlend(centerWeights.y);
            half3 result = lerp(original.rgb, farColor, farBlend);

            half3 nearColor = nearLayer.a > 0.0001h
                ? nearLayer.rgb / nearLayer.a
                : result;
            half nearSpillBlend = saturate(nearLayer.a);
            half nearSurfaceBlend = SurfaceCoverageToBlend(
                centerWeights.x
            );
            half isNearSurface = smoothstep(
                0.001h,
                0.05h,
                centerWeights.x
            );
            half nearBlend = lerp(
                nearSpillBlend,
                max(nearSpillBlend, nearSurfaceBlend),
                isNearSurface
            );
            result = lerp(result, nearColor, nearBlend);

            return half4(result, original.a);
        }

        half4 CameraDofCopy(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            return SampleDofSource(uv);
        }
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
            Name "DOF Prefilter Near"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofPrefilterNear
            ENDHLSL
        }

        Pass
        {
            Name "DOF Prefilter Far"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofPrefilterFar
            ENDHLSL
        }

        Pass
        {
            Name "DOF Near Dilation"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofDilate
            ENDHLSL
        }

        Pass
        {
            Name "DOF Circular Separable Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofBlur
            ENDHLSL
        }

        Pass
        {
            Name "DOF Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofComposite
            ENDHLSL
        }

        Pass
        {
            Name "DOF Copy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CameraDofCopy
            ENDHLSL
        }
    }
}
