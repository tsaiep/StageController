using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace NiloToon.NiloToonURP
{
    /// <summary>
    /// Draw the skybox into the given color buffer using the given depth buffer for depth testing.
    ///
    /// This pass renders the standard Unity skybox.
    /// </summary>
    public class NiloToonDrawSkyboxPass : ScriptableRenderPass
    {
        public NiloToonDrawSkyboxPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(NiloToonDrawSkyboxPass));
            renderPassEvent = evt;
        }
        
#if !UNITY_6000_4_OR_NEWER
        #if UNITY_6000_0_OR_NEWER
        [Obsolete]
        #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;

#if ENABLE_VR && ENABLE_XR_MODULE
            // XRTODO: Remove this code once Skybox pass is moved to SRP land.
            if (cameraData.xr.enabled)
            {
                // Setup Legacy XR buffer states
                if (cameraData.xr.singlePassEnabled)
                {
                    // Setup legacy skybox stereo buffer
                    camera.SetStereoProjectionMatrix(Camera.StereoscopicEye.Left, cameraData.GetProjectionMatrix(0));
                    camera.SetStereoViewMatrix(Camera.StereoscopicEye.Left, cameraData.GetViewMatrix(0));
                    camera.SetStereoProjectionMatrix(Camera.StereoscopicEye.Right, cameraData.GetProjectionMatrix(1));
                    camera.SetStereoViewMatrix(Camera.StereoscopicEye.Right, cameraData.GetViewMatrix(1));

                    CommandBuffer cmd = CommandBufferPool.Get();

                    // Use legacy stereo instancing mode to have legacy XR code path configured
                    cmd.SetSinglePassStereo(SystemInfo.supportsMultiview ? SinglePassStereoMode.Multiview : SinglePassStereoMode.Instancing);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    // Calling into built-in skybox pass - use old API for compatibility
#if UNITY_2022_1_OR_NEWER
                    var skyboxRendererList = context.CreateSkyboxRendererList(camera);
                    cmd.DrawRendererList(skyboxRendererList);
#else
                    context.DrawSkybox(camera);
#endif

                    // Disable Legacy XR path
                    cmd.SetSinglePassStereo(SinglePassStereoMode.None);
                    context.ExecuteCommandBuffer(cmd);
                    // We do not need to submit here due to special handling of stereo matrices in core.
                    // context.Submit();
                    CommandBufferPool.Release(cmd);

                    camera.ResetStereoProjectionMatrices();
                    camera.ResetStereoViewMatrices();
                }
                else
                {
                    camera.projectionMatrix = cameraData.GetProjectionMatrix(0);
                    camera.worldToCameraMatrix = cameraData.GetViewMatrix(0);

#if UNITY_2022_1_OR_NEWER
                    var skyboxRendererList = context.CreateSkyboxRendererList(camera);
                    CommandBuffer cmd = CommandBufferPool.Get();
                    cmd.DrawRendererList(skyboxRendererList);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
#else
                    context.DrawSkybox(camera);
#endif

                    // XRTODO: remove this call because it creates issues with nested profiling scopes
                    // See examples in UniversalRenderPipeline.RenderSingleCamera() and in ScriptableRenderer.Execute()
                    context.Submit(); // Submit and execute the skybox pass before resetting the matrices

                    camera.ResetProjectionMatrix();
                    camera.ResetWorldToCameraMatrix();
                }
            }
            else
#endif
            {
#if UNITY_2022_1_OR_NEWER
                var skyboxRendererList = context.CreateSkyboxRendererList(camera);
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.DrawRendererList(skyboxRendererList);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
#else
                context.DrawSkybox(camera);
#endif
            }
        }
#endif

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Empty RG implementation - does nothing but satisfies RG requirements
        /// </summary>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            // Empty implementation - RG pass does nothing
            // This satisfies the RG system but doesn't actually render anything
        }
#endif
    }
}