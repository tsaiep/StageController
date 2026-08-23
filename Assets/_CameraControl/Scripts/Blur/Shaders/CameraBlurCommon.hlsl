// Kawase blur rendering is derived from Unified Universal Blur by Luka Kldiashvili.
// See Assets/_CameraControl/ThirdParty/UnifiedUniversalBlur-LICENSE.txt.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

SAMPLER(sampler_BlitTexture);

#define CAMERA_BLUR_SAMPLE(uv) SAMPLE_TEXTURE2D_X_LOD( \
    _BlitTexture, sampler_LinearClamp, \
    UnityStereoTransformScreenSpaceTex(uv), _BlitMipLevel)

TEXTURE2D_X(_CameraBlurOriginalTexture);

#define CAMERA_BLUR_SAMPLE_ORIGINAL(uv) SAMPLE_TEXTURE2D_X_LOD( \
    _CameraBlurOriginalTexture, sampler_LinearClamp, \
    UnityStereoTransformScreenSpaceTex(uv), 0)

int _Iteration;
half4 _BlurParams;
half _CameraBlurCompositeWeight;

#if UNITY_VERSION <= 600000
half2 _BlitTexture_TexelSize;
#endif

#define CAMERA_BLUR_INTENSITY _BlurParams.x
#define CAMERA_BLUR_DOWNSAMPLE _BlurParams.z
#define CAMERA_BLUR_OFFSET _BlurParams.w

half4 CameraKawaseFilter(half2 uv, half2 pixelSize, half iteration)
{
    half2 halfPixelSize = pixelSize * half(0.5);
    half2 delta = pixelSize * half2(iteration, iteration) + halfPixelSize;

    half4 color = CAMERA_BLUR_SAMPLE(uv + half2(-delta.x, delta.y));
    color += CAMERA_BLUR_SAMPLE(uv + half2(delta.x, delta.y));
    color += CAMERA_BLUR_SAMPLE(uv + half2(delta.x, -delta.y));
    color += CAMERA_BLUR_SAMPLE(uv + half2(-delta.x, -delta.y));
    return color * half(0.25);
}

half4 CameraKawaseBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half2 texelSize = _BlitTexture_TexelSize.xy /
        max(CAMERA_BLUR_DOWNSAMPLE, half(1.0));

    return CameraKawaseFilter(
        input.texcoord,
        texelSize * CAMERA_BLUR_INTENSITY,
        _Iteration * CAMERA_BLUR_OFFSET
    );
}

half4 CameraBlurCopy(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 original = CAMERA_BLUR_SAMPLE_ORIGINAL(input.texcoord);
    half4 blurred = CAMERA_BLUR_SAMPLE(input.texcoord);
    return lerp(
        original,
        blurred,
        saturate(_CameraBlurCompositeWeight)
    );
}
