// SPDX-License-Identifier: (Not available for this version, you are only allowed to use this software if you have express permission from the copyright holder and agreed to the latest NiloToonURP EULA)
// Copyright (c) 2021 Kuroneko ShaderLab Limited

// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// #pragma once is a safe guard best practice in almost every .hlsl, 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

TEXTURE2D_X(_NiloToonPrepassBufferTex);
SAMPLER(sampler_NiloToonPrepassBufferTex);

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// core functions
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// Sample with UV coordinates (0-1 range)
half4 SampleNiloToonPrepassColor(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_NiloToonPrepassBufferTex, sampler_NiloToonPrepassBufferTex, UnityStereoTransformScreenSpaceTex(uv));
}

// Load with pixel coordinates
half4 LoadNiloToonPrepassColor(uint2 uv)
{
    return LOAD_TEXTURE2D_X(_NiloToonPrepassBufferTex, uv);
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// high level helper functions
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
half4 LoadNiloToonPrepassColorSafe(int2 loadTexPos)
{
    // clamp loadTexPos to prevent loading outside of _CameraDepthTexture's valid area
    loadTexPos.x =  max(loadTexPos.x,0);
    loadTexPos.y =  max(loadTexPos.y,0);
    loadTexPos = min(loadTexPos,GetScaledScreenWidthHeight()-1); 

    return LoadNiloToonPrepassColor(loadTexPos);
}

