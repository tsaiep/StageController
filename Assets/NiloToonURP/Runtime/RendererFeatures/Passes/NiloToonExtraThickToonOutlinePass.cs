using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace NiloToon.NiloToonURP
{
    public class NiloToonExtraThickOutlinePass : ScriptableRenderPass
    {
#if !UNITY_6000_4_OR_NEWER
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
#if UNITY_6000_0_OR_NEWER
        [Obsolete]
#endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // do nothing
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
#if UNITY_6000_0_OR_NEWER
        [Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Never draw in Preview
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType == CameraType.Preview)
                return;

            renderStencilRelatedPasses(context, renderingData);
        }
#endif
        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // do nothing
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // for rendering "LightMode"="ToonOutline" Pass (Classic outline)
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static readonly ShaderTagId NiloToonCharacterAreaStencilBufferFill_LightModeShaderTagId = new ShaderTagId("NiloToonCharacterAreaStencilBufferFill");
        static readonly ShaderTagId NiloToonExtraThickOutline_LightModeShaderTagId = new ShaderTagId("NiloToonExtraThickOutline");
        static readonly ShaderTagId NiloToonCharacterAreaColorFill_LightModeShaderTagId = new ShaderTagId("NiloToonCharacterAreaColorFill");
        
        NiloToonRendererFeatureSettings allSettings;
        ProfilingSampler m_ProfilingSamplerStencilFill;
        ProfilingSampler m_ProfilingSamplerExtraThickOutline;
        ProfilingSampler m_ProfilingSamplerColorFill;

        // constructor(will not construct on every frame)
        public NiloToonExtraThickOutlinePass(NiloToonRendererFeatureSettings allSettings)
        {
            this.allSettings = allSettings;
            m_ProfilingSamplerStencilFill = new ProfilingSampler("NiloToonCharacterAreaStencilBufferFill");
            m_ProfilingSamplerExtraThickOutline = new ProfilingSampler("NiloToonExtraThickOutline");
            m_ProfilingSamplerColorFill = new ProfilingSampler("NiloToonCharacterAreaColorFill");
        }

        // NOTE: [how to use ProfilingSampler to correctly]
        /*
            // [write as class member]
            ProfilingSampler m_ProfilingSampler;

            // [call once in constructor]
            m_ProfilingSampler = new ProfilingSampler("NiloToonToonOutlinePass");

            // [call in execute]
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            { 
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);
                cmd.SetGlobalTexture("_CameraNormalRT", _normalRT.Identifier());
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);	
        */

        private void renderStencilRelatedPasses(ScriptableRenderContext context, RenderingData renderingData)
        {
            // Note:
            // RenderQueueRange.Transparent should not be considered, since alpha can be 0~1, but stencil draw bit is 0/1 only
            // Imagine a character with mostly transparent cloths where many pixels using 0~0.5 alpha for alpha blending, it will destroy all stencil-related rendering if we use RenderQueueRange.all
            
            // optimization, check for all Nilo char, only render pass if needed
            bool needColorFill = false;
            bool needExtraThickOutline = false;
            
            var characters = NiloToonAllInOneRendererFeature.characterList;
            
            // early exit if list is null or empty
            if (characters == null || characters.Count == 0)
                return;
            
            // Use an index-based loop to avoid foreach allocations
            for (int i = 0, count = characters.Count; i < count; i++)
            {
                var c = characters[i];
                if (c == null)
                    continue;

                // Skip inactive or non-hierarchy objects quickly
                if (!c.isActiveAndEnabled || !c.gameObject.activeInHierarchy)
                    continue;

                // Check rendering flags
                if (!needColorFill && c.shouldRenderCharacterAreaColorFill)
                    needColorFill = true;

                if (!needExtraThickOutline && c.shouldRenderExtraThickOutline)
                    needExtraThickOutline = true;

                // Stop checking once both are required
                if (needColorFill && needExtraThickOutline)
                    break;
            }
            
            // No need to run any pass if neither is required
            if (!needColorFill && !needExtraThickOutline)
                return;
            
            renderPass_NiloToonCharacterAreaStencilBufferFill(context, renderingData);
            
            if(needExtraThickOutline)
                renderPass_NiloToonExtraThickOutline(context, renderingData);
            
            if(needColorFill)
                renderPass_NiloToonCharacterAreaColorFill(context, renderingData);
        }

        private void renderPass_NiloToonCharacterAreaColorFill(ScriptableRenderContext context, RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSamplerColorFill))
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

                // Then draw the #4 Pass(NiloToonCharacterAreaColorFill) of NiloToon_Character shader
                {
                    DrawingSettings characterAreaColorFillDrawingSettings = CreateDrawingSettings(NiloToonCharacterAreaColorFill_LightModeShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                    context.DrawRenderers(renderingData.cullResults, ref characterAreaColorFillDrawingSettings, ref filteringSettings);
                }
            }

            // must write these line after using{} finished, to ensure profiler and frame debugger display correctness
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        private void renderPass_NiloToonExtraThickOutline(ScriptableRenderContext context, RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSamplerExtraThickOutline))
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

                // Then draw the #3 Pass(NiloToonExtraThickOutline) of NiloToon_Character shader
                {
                    DrawingSettings extraThickOutlineDrawingSettings = CreateDrawingSettings(NiloToonExtraThickOutline_LightModeShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                    context.DrawRenderers(renderingData.cullResults, ref extraThickOutlineDrawingSettings, ref filteringSettings);
                }
            }

            // must write these line after using{} finished, to ensure profiler and frame debugger display correctness
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        private void renderPass_NiloToonCharacterAreaStencilBufferFill(ScriptableRenderContext context, RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSamplerStencilFill))
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

                // First draw the #2 Pass(NiloToonCharacterAreaStencilBufferFill) of NiloToon_Character shader
                {
                    DrawingSettings characterAreaStencilFillDrawingSettings = CreateDrawingSettings(NiloToonCharacterAreaStencilBufferFill_LightModeShaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                    context.DrawRenderers(renderingData.cullResults, ref characterAreaStencilFillDrawingSettings, ref filteringSettings);
                }
            }

            // must write these line after using{} finished, to ensure profiler and frame debugger display correctness
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        
        /////////////////////////////////////////////////////////////////////
        // RG support
        /////////////////////////////////////////////////////////////////////
#if UNITY_6000_0_OR_NEWER
        private class PassData
        {
            // Create a field to store the list of objects to draw
            public RendererListHandle rendererListHandle;
        }
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
        {
            // Never draw in Preview
            UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
            Camera camera = cameraData.camera;
            if (camera.cameraType == CameraType.Preview)
                return;
            
            // Note:
            // RenderQueueRange.Transparent should not be considered, since alpha can be 0~1, but stencil draw bit is 0/1 only
            // Imagine a character with mostly transparent cloths where many pixels using 0~0.5 alpha for alpha blending, it will destroy all stencil-related rendering if we use RenderQueueRange.all
            
            // optimization, check for all Nilo char, only render pass if needed
            bool needColorFill = false;
            bool needExtraThickOutline = false;
            
            var characters = NiloToonAllInOneRendererFeature.characterList;
            
            // early exit if list is null or empty
            if (characters == null || characters.Count == 0)
                return;
            
            // Use an index-based loop to avoid foreach allocations
            for (int i = 0, count = characters.Count; i < count; i++)
            {
                var c = characters[i];
                if (c == null)
                    continue;

                // Skip inactive or non-hierarchy objects quickly
                if (!c.isActiveAndEnabled || !c.gameObject.activeInHierarchy)
                    continue;

                // Check rendering flags
                if (!needColorFill && c.shouldRenderCharacterAreaColorFill)
                    needColorFill = true;

                if (!needExtraThickOutline && c.shouldRenderExtraThickOutline)
                    needExtraThickOutline = true;

                // Stop checking once both are required
                if (needColorFill && needExtraThickOutline)
                    break;
            }

            // No need to run any pass if neither is required
            if (!needColorFill && !needExtraThickOutline)
                return;

            DrawToActiveColorBufferByLightMode(
                renderGraph, 
                frameContext, 
                "NiloToonCharacterAreaStencilBufferFill", 
                NiloToonCharacterAreaStencilBufferFill_LightModeShaderTagId,
                SortingCriteria.CommonOpaque, 
                RenderQueueRange.opaque);

            if(needExtraThickOutline)
                DrawToActiveColorBufferByLightMode(
                    renderGraph, 
                    frameContext, 
                    "NiloToonExtraThickOutline", 
                    NiloToonExtraThickOutline_LightModeShaderTagId,
                    SortingCriteria.CommonTransparent, 
                    RenderQueueRange.opaque);

            if(needColorFill)
                DrawToActiveColorBufferByLightMode(
                    renderGraph, 
                    frameContext, 
                    "NiloToonCharacterAreaColorFill", 
                    NiloToonCharacterAreaColorFill_LightModeShaderTagId,
                    SortingCriteria.CommonTransparent, 
                    RenderQueueRange.opaque);
        }

        void DrawToActiveColorBufferByLightMode(RenderGraph renderGraph, ContextContainer frameContext, string passName, ShaderTagId lightMode, SortingCriteria sortFlags, RenderQueueRange renderQueueRange)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                // Get the data needed to create the list of objects to draw
                UniversalRenderingData renderingData = frameContext.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
                UniversalLightData lightData = frameContext.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();
                
                FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, ~0);

                // Redraw only objects that have their LightMode tag set to 'NiloToonCharacterAreaStencilBufferFill' 
                DrawingSettings drawSettings =
                    RenderingUtils.CreateDrawingSettings(lightMode,
                        renderingData, cameraData, lightData, sortFlags);

                // Create the list of objects to draw
                var rendererListParameters = new RendererListParams(renderingData.cullResults,
                    drawSettings, filterSettings);

                // Convert the list to a list handle that the render graph system can use
                passData.rendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                
                // Set the render target as the color and depth textures of the active camera texture
                builder.UseRendererList(passData.rendererListHandle);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }
            
        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            // Draw the objects in the list
            context.cmd.DrawRendererList(data.rendererListHandle);
        }
#endif
    }
}