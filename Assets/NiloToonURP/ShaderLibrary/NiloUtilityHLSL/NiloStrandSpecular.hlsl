// SPDX-License-Identifier: (Not available for this version, you are only allowed to use this software if you have express permission from the copyright holder and agreed to the latest NiloToonURP EULA)
// Copyright (c) 2021 Kuroneko ShaderLab Limited

// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// #pragma once is a safe guard best practice in almost every .hlsl, 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

// note: we have received bug report about random NaN pixel in 2025-2, so we force all StrandSpecular code to use float instead of half
// reference: From mobile to high-end PC: Achieving high quality anime style rendering on Unity (https://youtu.be/egHSE0dpWRw?t=1222)

float3 ShiftTangent(float3 T, float3 N, float uvValue, float frequency = 750, float shift = 0.015, float offset = 0)
{
    //distort T without texture read
    float ALU_shift = sin(uvValue * frequency) * shift + offset;
    return normalize(T + ALU_shift * N);
}
// https://web.engr.oregonstate.edu/~mjb/cs519/Projects/Papers/HairRendering.pdf - page 10 & 11
float StrandSpecular(float3 T, float3 H, float exponent)
{
    float dotTH = dot(T,H);
    float sinTH = SafeSqrt(1-dotTH*dotTH);
    float dirAtten = smoothstep(-1.0,0.0,dotTH);
    return dirAtten * SafePositivePow_float(sinTH,exponent);
}
/////////////////////////////////////////////////////////////////////////////////
// helper functions
/////////////////////////////////////////////////////////////////////////////////
/*
half StrandSpecular(half3 T, half3 H, half3 N, half exponent, half uvX)
{
    return StrandSpecular(ShiftTangent(T,N,uvX), H, exponent);
}
half StrandSpecular(half3 T, half3 V, half3 L, half3 N, half exponent, half uvX)
{
    half3 H = normalize(L+V);
    return StrandSpecular(T, H, N, exponent, uvX);
}
*/
