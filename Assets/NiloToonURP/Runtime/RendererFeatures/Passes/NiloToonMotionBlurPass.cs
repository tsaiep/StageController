// Already existing Unity Motion Blur solutions (NiloToon's motion blur develop based on KinoMotion):
// https://github.com/keijiro/KinoMotion/tree/master
// https://github.com/keijiro/MotionBlurTest

// Important Reference:
// https://casual-effects.com/research/McGuire2012Blur/McGuire12Blur.pdf

// Other reference:
// https://github.com/raysjoshua/UnrealEngine/blob/master/Engine/Shaders/PostProcessMotionBlur.usf
// https://github.com/EpicGames/UnrealEngine/tree/release/Engine/Shaders/Private/MotionBlur
// https://john-chapman-graphics.blogspot.com/2013/01/per-object-motion-blur.html
// https://youtu.be/b0S6WMAfi0o?si=8KSxKDHz9Z95VbVt (A Reconstruction Filter for Plausible Motion Blur (I3D 12))

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
#endif

namespace NiloToon.NiloToonURP
{
    public class NiloToonMotionBlurPass : ScriptableRenderPass
    {
        [System.Serializable]
        public class Settings
        {
            [Revertible]
            public bool allowRender = true;
        }
#if UNITY_2022_3_OR_NEWER        
        private Material blurMaterial;
        private RTHandle source;
        private RTHandle tempRT0;
        private RTHandle tempRT1;
        private RTHandle copyRT;

        static class Uniforms
        {
            internal static readonly int _VelocityScale     = Shader.PropertyToID("_VelocityScale");
            internal static readonly int _MaxBlurRadius     = Shader.PropertyToID("_MaxBlurRadius");
            internal static readonly int _RcpMaxBlurRadius  = Shader.PropertyToID("_RcpMaxBlurRadius");
            internal static readonly int _VelocityTex       = Shader.PropertyToID("_VelocityTex");
            internal static readonly int _MainTex           = Shader.PropertyToID("_MainTex");
            internal static readonly int _Tile2RT           = Shader.PropertyToID("_Tile2RT");
            internal static readonly int _Tile4RT           = Shader.PropertyToID("_Tile4RT");
            internal static readonly int _Tile8RT           = Shader.PropertyToID("_Tile8RT");
            internal static readonly int _TileMaxOffs       = Shader.PropertyToID("_TileMaxOffs");
            internal static readonly int _TileMaxLoop       = Shader.PropertyToID("_TileMaxLoop");
            internal static readonly int _TileVRT           = Shader.PropertyToID("_TileVRT");
            internal static readonly int _NeighborMaxTex    = Shader.PropertyToID("_NeighborMaxTex");
            internal static readonly int _LoopCount         = Shader.PropertyToID("_LoopCount");
        }

        enum Pass
        {
            VelocitySetup,
            TileMax1,
            TileMax2,
            TileMaxV,
            NeighborMax,
            Reconstruction,
        }

        private Settings settings;
        private string profilerTag;
        private float lastValidDeltaTime;
        private Vector3 lastValidCameraPosWS;
        private Matrix4x4 lastValidCameraViewProjMatrix;
        private bool lastFrameIsSafeToApplyMotionBlur;

        public NiloToonMotionBlurPass(NiloToonRendererFeatureSettings settings)
        {
            this.settings = settings.motionBlurSettings;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            profilerTag = "NiloToon Motion Blur";
            
            if (blurMaterial == null)
            {
                string shaderPath = "Hidden/NiloToon/NiloToonKinoMotionBlur";
                Shader shader = Shader.Find(shaderPath);
                if (shader != null)
                {
                    blurMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }
        }

        // please always and only call this in ScriptableRendererFeature.AddRenderPasses()
        public void ConfigureInputs(Camera camera)
        {
            ScriptableRenderPassInput input = ScriptableRenderPassInput.None;

            if (ShouldRender(camera))
            {
                input = ScriptableRenderPassInput.Motion | ScriptableRenderPassInput.Depth;
            }
            
            ConfigureInput(input);
        }
#if !UNITY_6000_4_OR_NEWER
#if UNITY_6000_0_OR_NEWER        
        [Obsolete]
#endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if(!ShouldRender(renderingData.cameraData.camera)) return;
            
            source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Allocate temporary RTHandles
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.colorFormat = RenderTextureFormat.RGHalf; // Use RGHalf for higher precision if needed

            float rt_TargetLength = GetRTTargetLength(descriptor);
            float tileSize = GetTileSize(rt_TargetLength);
            
            int tileWidth = Mathf.CeilToInt(descriptor.width / tileSize);
            int tileHeight = Mathf.CeilToInt(descriptor.height / tileSize);

            var tileDesc = descriptor;
            tileDesc.width = tileWidth;
            tileDesc.height = tileHeight;
            
            RenderingUtils.ReAllocateIfNeeded(ref tempRT0, tileDesc, name: "_TempRT0");
            RenderingUtils.ReAllocateIfNeeded(ref tempRT1, tileDesc, name: "_TempRT1");

            var copyRTDesc = renderingData.cameraData.cameraTargetDescriptor;
            copyRTDesc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref copyRT, copyRTDesc, name: "_CopyTex");
        }
#endif
        public float GetRTTargetLength(RenderTextureDescriptor descriptor)
        {
            return Mathf.Min(descriptor.width, descriptor.height);
        }

        public float GetIntensity()
        {
            float deltaTime = Time.deltaTime;
            
            // handle editor pause, where Time.deltaTime is 0
            if (deltaTime <= 0f)
            {
                deltaTime = lastValidDeltaTime; 
            }
            else
            {
                lastValidDeltaTime = deltaTime;
            }

            if (deltaTime <= 0) return 0; // should not happen except the first frame
            
            var motionBlurPP = VolumeManager.instance.stack.GetComponent<NiloToonMotionBlurVolume>();

            float intensity = motionBlurPP.intensity.value; // user custom multiplier control
            
            // make it fps independent (24fps 180 shutter angle as base, it is the perfect cinematic motion blur)
            intensity /= deltaTime / (1f/24f);  

            // * 0.5, so that when intensity in volume is 1, the result is matching ground truth 24fps 180 shutter angle from NiloToonMotionBlurVideoBaker)
            intensity *= 0.5f;
            return intensity;
        }
        public int GetTileSize(float rt_TargetLength)
        {
            // the ideal 1/48s shutter speed tileSize for:
            // 4320p = 30
            // 2160p = 15
            // 1080p = 7.5 (=8)
            int tileSize = Mathf.CeilToInt(rt_TargetLength / 4320f * 30f);
            
            // allow more blur bleed out
            tileSize *= 4;
            
            return tileSize;
        }

        bool IsCameraSafeToApplyMotionBlur(
            Matrix4x4 prevViewProj, 
            Matrix4x4 currentViewProj,
            Vector3 prevCamPos,
            Vector3 currentCamPos)
        {
            // Default thresholds
            const float maxPositionDiff = 2.0f;     // units in world space
            const float maxAngleDegrees = 15.0f;    // degrees
            const float maxSizeRatio = 2.0f;        // maximum 2x screen size change (100% change)

            // 1. Position Check
            float positionDiff = Vector3.Distance(currentCamPos, prevCamPos);
            if (positionDiff > maxPositionDiff)
                return false;

            // 2. Camera Direction Check
            Vector3 prevForward = -new Vector3(prevViewProj.m20, prevViewProj.m21, prevViewProj.m22).normalized;
            Vector3 currentForward = -new Vector3(currentViewProj.m20, currentViewProj.m21, currentViewProj.m22).normalized;
            float dotProduct = Vector3.Dot(prevForward, currentForward);
            float angleDiff = Mathf.Acos(Mathf.Clamp(dotProduct, -1.0f, 1.0f)) * Mathf.Rad2Deg;

            if (angleDiff > maxAngleDegrees)
                return false;

            // 3. FOV Check - using screen size ratio directly
            float prevTanHalfFOV = 1.0f / prevViewProj.m11;
            float currentTanHalfFOV = 1.0f / currentViewProj.m11;
            float screenSizeRatio = currentTanHalfFOV / prevTanHalfFOV;

            if (screenSizeRatio > maxSizeRatio || screenSizeRatio < 1.0f/maxSizeRatio)
                return false;

            return true;
        }

        public bool ShouldRender(Camera camera)
        {
            // if PIDI planar reflection enabled postFX, it will make the motion blur disable,
            // so we need to disable motion blur for all PIDI planar reflection
            if (NiloToonPlanarReflectionHelper.IsPlanarReflectionCamera(camera))
                return false;

            var motionBlurPP = VolumeManager.instance.stack.GetComponent<NiloToonMotionBlurVolume>();
            return motionBlurPP.IsActive() && settings.allowRender;
        }

        /////////////////////////////////////////////////////////////////////
        // SHARED KINO MOTION BLUR ALGORITHM CORE
        /////////////////////////////////////////////////////////////////////
        private void ExecuteKinoMotionBlurCore(CommandBuffer cmd, RenderTextureDescriptor descriptor, 
            float intensity, float rt_TargetLength)
        {
            const float kMaxBlurRadius = 5f; // max blur is 5% of screen
            Material material = blurMaterial;
            
            // Texture format for storing 2D vectors.
            RenderTextureFormat m_VectorRTFormat = RenderTextureFormat.RGHalf;
            // Texture format for storing packed velocity/depth.
            RenderTextureFormat m_PackedRTFormat = RenderTextureFormat.ARGB2101010;

            float shutterAngle = 180f * intensity;
            int sampleCount = 32;

            // Calculate the maximum blur radius in pixels.
            int maxBlurPixels = (int)(kMaxBlurRadius * rt_TargetLength / 100);

            // Calculate the TileMax size.
            // It should be a multiple of 8 and larger than maxBlur.
            int tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

            // Pass 1 - Velocity/depth packing
            var velocityScale = shutterAngle / 360f;
            cmd.SetGlobalFloat(Uniforms._VelocityScale, velocityScale);
            cmd.SetGlobalFloat(Uniforms._MaxBlurRadius, maxBlurPixels);
            cmd.SetGlobalFloat(Uniforms._RcpMaxBlurRadius, 1f / maxBlurPixels);

            int vbuffer = Uniforms._VelocityTex;
            cmd.GetTemporaryRT(vbuffer, descriptor.width, descriptor.height, 0, FilterMode.Point, m_PackedRTFormat, RenderTextureReadWrite.Linear);
            cmd.Blit((Texture)null, vbuffer, material, (int)Pass.VelocitySetup);

            // Pass 2 - First TileMax filter (1/2 downsize)
            int tile2 = Uniforms._Tile2RT;
            cmd.GetTemporaryRT(tile2, descriptor.width / 2, descriptor.height / 2, 0, FilterMode.Point, m_VectorRTFormat, RenderTextureReadWrite.Linear);
            cmd.SetGlobalTexture(Uniforms._MainTex, vbuffer);
            cmd.Blit(vbuffer, tile2, material, (int)Pass.TileMax1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            int tile4 = Uniforms._Tile4RT;
            cmd.GetTemporaryRT(tile4, descriptor.width / 4, descriptor.height / 4, 0, FilterMode.Point, m_VectorRTFormat, RenderTextureReadWrite.Linear);
            cmd.SetGlobalTexture(Uniforms._MainTex, tile2);
            cmd.Blit(tile2, tile4, material, (int)Pass.TileMax2);
            cmd.ReleaseTemporaryRT(tile2);

            // Pass 4 - Third TileMax filter (1/2 downsize)
            int tile8 = Uniforms._Tile8RT;
            cmd.GetTemporaryRT(tile8, descriptor.width / 8, descriptor.height / 8, 0, FilterMode.Point, m_VectorRTFormat, RenderTextureReadWrite.Linear);
            cmd.SetGlobalTexture(Uniforms._MainTex, tile4);
            cmd.Blit(tile4, tile8, material, (int)Pass.TileMax2);
            cmd.ReleaseTemporaryRT(tile4);

            // Pass 5 - Fourth TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8f - 1f) * -0.5f;
            cmd.SetGlobalVector(Uniforms._TileMaxOffs, tileMaxOffs);
            cmd.SetGlobalFloat(Uniforms._TileMaxLoop, (int)(tileSize / 8f));

            int tile = Uniforms._TileVRT;
            cmd.GetTemporaryRT(tile, descriptor.width / tileSize, descriptor.height / tileSize, 0, FilterMode.Point, m_VectorRTFormat, RenderTextureReadWrite.Linear);
            cmd.SetGlobalTexture(Uniforms._MainTex, tile8);
            cmd.Blit(tile8, tile, material, (int)Pass.TileMaxV);
            cmd.ReleaseTemporaryRT(tile8);

            // Pass 6 - NeighborMax filter
            int neighborMax = Uniforms._NeighborMaxTex;
            int neighborMaxWidth = descriptor.width / tileSize;
            int neighborMaxHeight = descriptor.height / tileSize;
            cmd.GetTemporaryRT(neighborMax, neighborMaxWidth, neighborMaxHeight, 0, FilterMode.Point, m_VectorRTFormat, RenderTextureReadWrite.Linear);
            cmd.SetGlobalTexture(Uniforms._MainTex, tile);
            cmd.Blit(tile, neighborMax, material, (int)Pass.NeighborMax);
            cmd.ReleaseTemporaryRT(tile);

            // Set up for reconstruction (loop count)
            cmd.SetGlobalFloat(Uniforms._LoopCount, Mathf.Clamp(sampleCount / 2, 1, 64));
            
            cmd.ReleaseTemporaryRT(vbuffer);
            cmd.ReleaseTemporaryRT(neighborMax);
        }

        // Non-RG version with RTHandle parameters
        private void ExecuteKinoMotionBlur(CommandBuffer cmd, RenderTextureDescriptor descriptor, 
            float intensity, float rt_TargetLength, RTHandle sourceRT, RTHandle destinationRT, RTHandle copyBuffer)
        {
            // Execute core algorithm
            ExecuteKinoMotionBlurCore(cmd, descriptor, intensity, rt_TargetLength);
            
            // Copy the color buffer to use for sampling during the blur pass
            cmd.Blit(sourceRT, copyBuffer);
            
            // Pass 7 - Reconstruction pass
            cmd.SetGlobalTexture(Uniforms._MainTex, copyBuffer);
            cmd.Blit(copyBuffer, destinationRT, blurMaterial, (int)Pass.Reconstruction);
        }

#if UNITY_6000_0_OR_NEWER
        // RG version with TextureHandle parameters
        private void ExecuteKinoMotionBlurRG(CommandBuffer cmd, RenderTextureDescriptor descriptor, 
            float intensity, float rt_TargetLength, TextureHandle sourceTexture, TextureHandle destinationTexture)
        {
            // Execute core algorithm
            ExecuteKinoMotionBlurCore(cmd, descriptor, intensity, rt_TargetLength);
            
            // Copy the color buffer to use for sampling during the blur pass
            int copyRT = Shader.PropertyToID("_CopyTexture_RG");
            cmd.GetTemporaryRT(copyRT, descriptor.width, descriptor.height, 0, FilterMode.Bilinear, descriptor.colorFormat);
            cmd.Blit(sourceTexture, copyRT);
            
            // Pass 7 - Reconstruction pass
            cmd.SetGlobalTexture(Uniforms._MainTex, copyRT);
            cmd.Blit(copyRT, destinationTexture, blurMaterial, (int)Pass.Reconstruction);
            
            cmd.ReleaseTemporaryRT(copyRT);
        }
#endif

        /////////////////////////////////////////////////////////////////////
        // NON-RG EXECUTE METHOD - Uses shared algorithm
        /////////////////////////////////////////////////////////////////////
#if !UNITY_6000_4_OR_NEWER
#if UNITY_6000_0_OR_NEWER        
        [Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if(!ShouldRender(renderingData.cameraData.camera)) return;
            
            if (blurMaterial == null || source == null)
            {
                Debug.LogError("Motion Blur: Missing required resources");
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                float rt_TargetLength = GetRTTargetLength(descriptor);
                float intensity = GetIntensity();

                Camera currentCam = renderingData.cameraData.camera;
                bool thisFrameIsSafeToApplyMotionBlur = IsCameraSafeToApplyMotionBlur(
                    lastValidCameraViewProjMatrix,
                    currentCam.projectionMatrix * currentCam.worldToCameraMatrix,
                    lastValidCameraPosWS,
                    currentCam.transform.position);

                lastValidCameraPosWS = currentCam.transform.position;
                lastValidCameraViewProjMatrix = currentCam.projectionMatrix * currentCam.worldToCameraMatrix;
                
                // debug
                /*
                if (lastFrameIsSafeToApplyMotionBlur == false && thisFrameIsSafeToApplyMotionBlur == false)
                {
                    Debug.LogWarning("NiloToonMotionBlur 2 frames not safe to draw, is the auto detection too harsh?");
                }
                */
                
                lastFrameIsSafeToApplyMotionBlur = thisFrameIsSafeToApplyMotionBlur;

                if (intensity > 0 && thisFrameIsSafeToApplyMotionBlur)
                {
                    RTHandle destination = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    
                    // Call shared algorithm
                    ExecuteKinoMotionBlur(cmd, descriptor, intensity, rt_TargetLength, source, destination, copyRT);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // do not write anything here
        }

        public void Dispose()
        {
            tempRT0?.Release();
            tempRT1?.Release();
            copyRT?.Release();
            
            CoreUtils.Destroy(blurMaterial);
        }
        
        /////////////////////////////////////////////////////////////////////
        // RG support
        /////////////////////////////////////////////////////////////////////
#if UNITY_6000_0_OR_NEWER
        // Render Graph data structures
        private class MotionBlurPassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle destination;
            public float intensity;
            public float rt_TargetLength;
            public RenderTextureDescriptor descriptor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            var cameraData = frameContext.Get<UniversalCameraData>();
            var resourceData = frameContext.Get<UniversalResourceData>();
            
            if (!ShouldRender(cameraData.camera))
                return;

            float intensity = GetIntensity();
            var descriptor = cameraData.cameraTargetDescriptor;
            float rt_TargetLength = GetRTTargetLength(descriptor);

            Camera currentCam = cameraData.camera;
            bool thisFrameIsSafeToApplyMotionBlur = IsCameraSafeToApplyMotionBlur(
                lastValidCameraViewProjMatrix,
                currentCam.projectionMatrix * currentCam.worldToCameraMatrix,
                lastValidCameraPosWS,
                currentCam.transform.position);

            lastValidCameraPosWS = currentCam.transform.position;
            lastValidCameraViewProjMatrix = currentCam.projectionMatrix * currentCam.worldToCameraMatrix;
            lastFrameIsSafeToApplyMotionBlur = thisFrameIsSafeToApplyMotionBlur;

            if (intensity <= 0 || !thisFrameIsSafeToApplyMotionBlur)
                return;

            using (var builder = renderGraph.AddUnsafePass<MotionBlurPassData>(profilerTag, out var passData))
            {
                passData.material = blurMaterial;
                passData.source = resourceData.activeColorTexture;
                passData.destination = resourceData.activeColorTexture;
                passData.intensity = intensity;
                passData.rt_TargetLength = rt_TargetLength;
                passData.descriptor = descriptor;

                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Write);
                
                builder.SetRenderFunc((MotionBlurPassData data, UnsafeGraphContext context) =>
                {
                    ExecuteMotionBlurRenderGraph(data, context);
                });
            }
        }

        private void ExecuteMotionBlurRenderGraph(MotionBlurPassData data, UnsafeGraphContext context)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            
            // Call shared algorithm - RG version
            ExecuteKinoMotionBlurRG(cmd, data.descriptor, data.intensity, data.rt_TargetLength, 
                data.source, data.destination);
        }
#endif
        
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // do nothing in Unity2021.3 (NiloToonMotionBlur requires Unity2022.3)
        }
#endif
    }
}