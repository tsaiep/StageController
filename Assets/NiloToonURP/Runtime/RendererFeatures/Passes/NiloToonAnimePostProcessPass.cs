using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace NiloToon.NiloToonURP
{
    /// <summary>
    /// Draw a pair of additive(top screen area) and multiply(bottom screen area) gradient to simulate an anime postprocess effect
    /// </summary>
    public class NiloToonAnimePostProcessPass : ScriptableRenderPass
    {
        // singleton
        public static NiloToonAnimePostProcessPass Instance
        {
            get => _instance;
        }
        static NiloToonAnimePostProcessPass _instance;

        [Serializable]
        public class Settings
        {
            [Revertible]
            [Tooltip("Can turn off to prevent rendering NiloToonAnimePostProcessVolume, which will improve performance for low quality graphics setting renderer")]
            [OverrideDisplayName("Allow render?")]
            public bool allowRender = true;
        }
        public Settings settings { get; }

        Material material;
        NiloToonRendererFeatureSettings allSettings;
        ProfilingSampler m_ProfilingSampler;
        private static readonly int TopLightRotationDegree = Shader.PropertyToID("_TopLightRotationDegree");
        private static readonly int TopLightIntensity = Shader.PropertyToID("_TopLightIntensity");
        private static readonly int TopLightDesaturate = Shader.PropertyToID("_TopLightDesaturate");
        private static readonly int TopLightMultiplyLightColor = Shader.PropertyToID("_TopLightMultiplyLightColor");
        private static readonly int TopLightTintColor = Shader.PropertyToID("_TopLightTintColor");
        private static readonly int TopLightDrawAreaHeight = Shader.PropertyToID("_TopLightDrawAreaHeight");
        private static readonly int BottomDarkenRotationDegree = Shader.PropertyToID("_BottomDarkenRotationDegree");
        private static readonly int BottomDarkenIntensity = Shader.PropertyToID("_BottomDarkenIntensity");
        private static readonly int BottomDarkenDrawAreaHeight = Shader.PropertyToID("_BottomDarkenDrawAreaHeight");
        private static readonly int TopLightSunTintColor = Shader.PropertyToID("_TopLightSunTintColor");
        private static readonly int TopLightReceiveMainLightShadow = Shader.PropertyToID("_TopLightReceiveMainLightShadow");

        public NiloToonAnimePostProcessPass(NiloToonRendererFeatureSettings allSettings)
        {
            this.allSettings = allSettings;
            settings = allSettings.animePostProcessSettings;
            _instance = this;
            m_ProfilingSampler = new ProfilingSampler("NiloToonAnimePostProcessPass");
        }
#if !UNITY_6000_4_OR_NEWER
#if UNITY_6000_0_OR_NEWER
        [Obsolete]
#endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            SetRenderPassEvent();
        }
#endif
        private void SetRenderPassEvent()
        {
            var animePP = VolumeManager.instance.stack.GetComponent<NiloToonAnimePostProcessVolume>();
            if (animePP.drawBeforePostProcess.value)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }
            else
            {
                // force render after "everything except UI", make it render correctly even FXAA on
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }
        }
#if !UNITY_6000_4_OR_NEWER
#if UNITY_6000_0_OR_NEWER
        [Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Render(context, ref renderingData);
        }
#endif
        private void InitMaterialIfNeeded()
        {
            // delay CreateEngineMaterial to as late as possible, to make it safe when ReimportAll is running
            if (!material)
                material = CoreUtils.CreateEngineMaterial("Hidden/NiloToon/AnimePostProcess");
        }
        private bool ShouldRender(CameraType cameraType, bool postProcessingEnabled)
        {
            // we only want to render anime postprocess to Game window
            if (cameraType != CameraType.Game) return false;

            var animePP = VolumeManager.instance.stack.GetComponent<NiloToonAnimePostProcessVolume>();
            
            // respect Camera's PostProcessing toggle
            if (animePP.affectedByCameraPostprocessToggle.value)
            {
                if (!postProcessingEnabled) return false;
            }

            // volume control
            if (!settings.allowRender || !animePP.IsActive()) return false;

            // in XR, maybe not a good idea to render this pass, since it maybe disturbing
            if (XRSettings.isDeviceActive) return false;

            // sometimes the shader is not yet compile when first time opening the project or opening the project after deleting Library folder,
            // if material is not ready, cmd.DrawMesh will produce "Invalid pass" error log, so we need to skip it.
            if (!material)
                return false;

            return true;
        }
        
        private static void CMDAction(CommandBuffer cmd, float topLightEffectIntensity, NiloToonAnimePostProcessVolume animePP,
            float bottomDarkenEffectIntensity, Camera camera, Material material)
        {
            // [how to draw a full screen quad without RT switch]
            // https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@13.1/manual/renderer-features/how-to-fullscreen-blit-in-xr-spi.html
            // https://gist.github.com/phi-lira/46c98fc67640cda47dcd27e9b3765b85#file-fullscreenquadpass-cs-L23

            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity); // set V,P to identity matrix so we can draw full screen quad (mesh's vertex position used as final NDC position)

            // optimization: only draw if it is affecting result
            if (topLightEffectIntensity > 0 && animePP.topLightEffectDrawHeight.value > 0)
            {
                // URP's RenderingUtils.fullscreenMesh is obsolete, so NiloToon write it's own NiloToonRenderingUtils.fullscreenMesh
                cmd.DrawMesh(NiloToonRenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, 0); // pass 0, top light pass
            }
            if (bottomDarkenEffectIntensity > 0 && animePP.bottomDarkenEffectDrawHeight.value > 0)
            {
                // URP's RenderingUtils.fullscreenMesh is obsolete, so NiloToon write it's own NiloToonRenderingUtils.fullscreenMesh
                cmd.DrawMesh(NiloToonRenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, 1); // pass 1, bottom darken pass
            }

            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix); // restore
        }
        
        private void SetMaterial(NiloToonAnimePostProcessVolume animePP, float topLightEffectIntensity,
            float bottomDarkenEffectIntensity)
        {
            material.SetFloat(TopLightRotationDegree, animePP.rotation.value + animePP.topLightExtraRotation.value);
            material.SetFloat(TopLightIntensity, topLightEffectIntensity);
            material.SetFloat(TopLightDesaturate, animePP.topLightDesaturate.value);
            material.SetFloat(TopLightMultiplyLightColor, animePP.topLightMultiplyLightColor.value);
            material.SetColor(TopLightTintColor, animePP.topLightTintColor.value);
            material.SetColor(TopLightSunTintColor, animePP.topLightSunTintColor.value);
            material.SetFloat(TopLightDrawAreaHeight, animePP.topLightEffectDrawHeight.value);
            material.SetFloat(TopLightReceiveMainLightShadow, animePP.topLightReceiveMainLightShadow.value);

            material.SetFloat(BottomDarkenRotationDegree, animePP.rotation.value + animePP.bottomDarkenExtraRotation.value);
            material.SetFloat(BottomDarkenIntensity, bottomDarkenEffectIntensity);
            material.SetFloat(BottomDarkenDrawAreaHeight, animePP.bottomDarkenEffectDrawHeight.value);
        }
        
        private void Render(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            InitMaterialIfNeeded();
            if(!ShouldRender(renderingData.cameraData.cameraType, renderingData.postProcessingEnabled)) return;

            var animePP = VolumeManager.instance.stack.GetComponent<NiloToonAnimePostProcessVolume>();
            
            float topLightEffectIntensity = animePP.topLightEffectIntensity.value * animePP.intensity.value;
            float bottomDarkenEffectIntensity = animePP.bottomDarkenEffectIntensity.value * animePP.intensity.value;

            SetMaterial(animePP, topLightEffectIntensity, bottomDarkenEffectIntensity);

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                /*
                Note : should always ExecuteCommandBuffer at least once before using
                ScriptableRenderContext functions (e.g. DrawRenderers) even if you
                don't queue any commands! This makes sure the frame debugger displays
                everything under the correct title.
                */
                // https://www.cyanilux.com/tutorials/custom-renderer-features/?fbclid=IwAR27j2f3VVo0IIYDa32Dh76G9KPYzwb8j1J5LllpSnLXJiGf_UHrQ_lDtKg
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CMDAction(cmd, topLightEffectIntensity, animePP, bottomDarkenEffectIntensity, renderingData.cameraData.camera, material);
            }
            
            // must write these line after using{} finished, to ensure profiler and frame debugger display correctness
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
        
        class PassData
        {
        }
        
        /////////////////////////////////////////////////////////////////////
        // RG support
        /////////////////////////////////////////////////////////////////////
#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // copy and edit of https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-write-render-pass.html
            string passName = "NiloToonAnimePostProcess(RG)";
            
            SetRenderPassEvent();
            
            // Add a raster render pass to the render graph. The PassData type parameter determines
            // the type of the passData output variable.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName,
                       out var passData))
            {
                // UniversalResourceData contains all the texture references used by URP,
                // including the active color and depth textures of the camera.
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                InitMaterialIfNeeded();
                if (!ShouldRender(cameraData.cameraType, cameraData.postProcessEnabled)) return;

                var animePP = VolumeManager.instance.stack.GetComponent<NiloToonAnimePostProcessVolume>();
            
                float topLightEffectIntensity = animePP.topLightEffectIntensity.value * animePP.intensity.value;
                float bottomDarkenEffectIntensity = animePP.bottomDarkenEffectIntensity.value * animePP.intensity.value;

                SetMaterial(animePP, topLightEffectIntensity, bottomDarkenEffectIntensity);
                
                // Since this is a fullscreen overlay effect, use the active color texture as render target
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                
                // RenderGraph automatically determines that it can remove this render pass
                // because its results, which are stored in the temporary destination texture,
                // are not used by other passes.
                // For demonstrative purposes, this sample turns off this behavior to make sure
                // that render graph executes the render pass. 
                builder.AllowPassCulling(false);

                // Set the ExecutePass method as the rendering function that render graph calls
                // for the render pass. 
                // This sample uses a lambda expression to avoid memory allocations.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    CMDAction_RG(context.cmd, topLightEffectIntensity, animePP, bottomDarkenEffectIntensity, cameraData.camera, material);
                });
            }
        }
        
        private static void CMDAction_RG(RasterCommandBuffer cmd, float topLightEffectIntensity, NiloToonAnimePostProcessVolume animePP,
            float bottomDarkenEffectIntensity, Camera camera, Material material)
        {
            // [how to draw a full screen quad without RT switch]
            // https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@13.1/manual/renderer-features/how-to-fullscreen-blit-in-xr-spi.html
            // https://gist.github.com/phi-lira/46c98fc67640cda47dcd27e9b3765b85#file-fullscreenquadpass-cs-L23

            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity); // set V,P to identity matrix so we can draw full screen quad (mesh's vertex position used as final NDC position)

            // optimization: only draw if it is affecting result
            if (topLightEffectIntensity > 0 && animePP.topLightEffectDrawHeight.value > 0)
            {
                // URP's RenderingUtils.fullscreenMesh is obsolete, so NiloToon write it's own NiloToonRenderingUtils.fullscreenMesh
                cmd.DrawMesh(NiloToonRenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, 0); // pass 0, top light pass
            }
            if (bottomDarkenEffectIntensity > 0 && animePP.bottomDarkenEffectDrawHeight.value > 0)
            {
                // URP's RenderingUtils.fullscreenMesh is obsolete, so NiloToon write it's own NiloToonRenderingUtils.fullscreenMesh
                cmd.DrawMesh(NiloToonRenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, 1); // pass 1, bottom darken pass
            }

            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix); // restore
        }
#endif
    }
}