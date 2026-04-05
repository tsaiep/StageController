// SPDX-License-Identifier: (Not available for this version, you are only allowed to use this software if you have express permission from the copyright holder and agreed to the latest NiloToonURP EULA)
// Copyright (c) 2021 Kuroneko ShaderLab Limited

// For more information, visit -> https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample

// #pragma once is a safe guard best practice in almost every .hlsl, 
// doing this can make sure your .hlsl's user can include this .hlsl anywhere anytime without producing any multi include conflict
#pragma once

// HLSL Glitter Effect using Unity Lightmap UVs
// Lightmap UVs are unique and non-overlapping, perfect for glitter placement

// Random function based on position
float hash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash2(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float3 hash3(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yzz) * p3.zyx) * 2.0 - 1.0;
}

// Main glitter function using lightmap UVs
float3 NiloGlitter(
    float2 lightmapUV,      // Unity's lightmap UV (TEXCOORD1)
    float3 viewDir,         // world space (should be normalized)
    float3 normal,          // world space (should be normalized)
    float3 lightDir,        // world space (should be normalized)
    float density,          // expect default input = 1
    float size,             // expect default input = 1
    float brightness,       // expect default input = 1
    float randomNormalStrength) // default 0.5       
{
    // Normalize inputs to be safe
    viewDir = normalize(viewDir);
    normal = normalize(normal);
    lightDir = normalize(lightDir);
    
    float finalSize = size * 0.25;
    
    // Use lightmap UV - these are unique across the entire mesh
    float2 scaledUV = lightmapUV * density * 1000.0;
    float2 cellID = floor(scaledUV);
    float2 cellUV = frac(scaledUV);
    
    float3 glitter = float3(0.0, 0.0, 0.0);
    
    // Check neighboring cells for glitter particles
    for(int y = -1; y <= 1; y++)
    {
        for(int x = -1; x <= 1; x++)
        {
            float2 neighborCell = cellID + float2(x, y);
            
            // Random position within the cell
            float2 randomOffset = hash2(neighborCell);
            float2 particlePos = float2(x, y) + randomOffset;
            
            // Distance to particle
            float2 toParticle = particlePos - cellUV;
            float dist = length(toParticle);
            
            // Only process if within glitter size
            if(dist < finalSize)
            {
                // Generate random orientation for this glitter particle
                float3 glitterNormal = normalize(hash3(neighborCell));
                
                // Blend with surface normal for realistic placement
                glitterNormal = normalize(lerp(normal, glitterNormal, randomNormalStrength));
                
                // Calculate reflection direction
                float3 reflectDir = reflect(-viewDir, glitterNormal);
                
                // Use the actual light direction
                float specular = pow(max(dot(reflectDir, lightDir), 0.0), 1000.0);
                
                // View alignment - glitter only visible at the right angle
                float viewAlignment = max(dot(viewDir, glitterNormal), 0.0);
                viewAlignment = pow(viewAlignment, 5.0);
                
                // Smooth falloff based on distance
                float fade = 1.0 - smoothstep(finalSize * 0.2, finalSize, dist);
                
                // Combine all factors
                float intensity = specular * viewAlignment * fade * brightness * 100.0;
                
                // Color variation
                float colorVariation = hash(neighborCell + 50.0);
                float3 glitterColor = lerp(float3(1.0, 0.98, 0.95), float3(0.95, 0.97, 1.0), colorVariation);
                
                glitter += glitterColor * intensity;
            }
        }
    }
    
    return glitter;
}