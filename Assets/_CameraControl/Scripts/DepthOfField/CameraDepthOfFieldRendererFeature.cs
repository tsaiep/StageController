using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Runtime.CameraSystem
{
    /// <summary>
    /// PC-oriented, depth-aware near/far DOF. The image is split into
    /// premultiplied near and far layers, processed at half resolution with a
    /// separable circular Gaussian approximation, then composited at full
    /// resolution using the camera depth texture.
    /// </summary>
    public sealed class CameraDepthOfFieldRendererFeature :
        ScriptableRendererFeature
    {
        [Header("Quality")]
        [Range(1f, 4f)]
        [SerializeField] private float downsample = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float nearDilation = 0.35f;

        [Min(1f)]
        [SerializeField] private float radiusReferenceHeight = 1080f;

        [Header("Rendering")]
        [SerializeField] private RenderPassEvent injectionPoint =
            RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField, HideInInspector]
        private Shader shader;

        private Material _material;
        private CameraDepthOfFieldPass _pass;
        private bool _hasLoggedMissingShader;

        public override void Create()
        {
            EnsureMaterial();
            _pass = new CameraDepthOfFieldPass
            {
                renderPassEvent = injectionPoint
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;

            if (camera == null || camera.cameraType != CameraType.Game)
                return;

            if (!CameraDepthOfFieldState.TryGet(
                    camera,
                    out CameraDepthOfFieldSettings settings))
            {
                return;
            }

            if (!EnsureMaterial())
                return;

            RenderTextureDescriptor descriptor =
                renderingData.cameraData.cameraTargetDescriptor;

            float safeDownsample = Mathf.Clamp(downsample, 1f, 4f);
            float resolutionScale = descriptor.height /
                Mathf.Max(1f, radiusReferenceHeight);

            float nearRadius = settings.NearBlurRadius * resolutionScale /
                safeDownsample;
            float farRadius = settings.FarBlurRadius * resolutionScale /
                safeDownsample;

            CameraDepthOfFieldConfig config = new CameraDepthOfFieldConfig
            {
                Material = _material,
                FocusDistance = settings.FocusDistance,
                NearFocusRange = settings.NearFocusRange,
                FarFocusRange = settings.FarFocusRange,
                Intensity = settings.Intensity,
                DebugView = settings.DebugView,
                NearRadius = nearRadius,
                FarRadius = farRadius,
                NearDilationRadius = nearRadius * Mathf.Clamp01(nearDilation),
                FullWidth = Mathf.Max(1, descriptor.width),
                FullHeight = Mathf.Max(1, descriptor.height),
                HalfWidth = Mathf.Max(1, Mathf.RoundToInt(
                    descriptor.width / safeDownsample)),
                HalfHeight = Mathf.Max(1, Mathf.RoundToInt(
                    descriptor.height / safeDownsample)),
                // The near/far layers pack CoC coverage in alpha, so they
                // must not inherit an HDR camera format without alpha.
                GraphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                TextureDimension = descriptor.dimension,
                VolumeDepth = Mathf.Max(1, descriptor.volumeDepth),
                VrUsage = descriptor.vrUsage
            };

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(config);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private bool EnsureMaterial()
        {
            if (shader == null)
            {
                shader = Shader.Find(
                    "Hidden/StageController/CameraDepthOfField");
            }

            if (_material == null && shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);

            if (_material != null)
            {
                _hasLoggedMissingShader = false;
                return true;
            }

            if (!_hasLoggedMissingShader)
            {
                Debug.LogError(
                    $"[{nameof(CameraDepthOfFieldRendererFeature)}] 找不到 DOF shader，已略過景深 pass。",
                    this
                );
                _hasLoggedMissingShader = true;
            }

            return false;
        }
    }

    internal struct CameraDepthOfFieldConfig
    {
        public Material Material;
        public float FocusDistance;
        public float NearFocusRange;
        public float FarFocusRange;
        public float Intensity;
        public CameraDepthOfFieldDebugView DebugView;
        public float NearRadius;
        public float FarRadius;
        public float NearDilationRadius;
        public int FullWidth;
        public int FullHeight;
        public int HalfWidth;
        public int HalfHeight;
        public GraphicsFormat GraphicsFormat;
        public TextureDimension TextureDimension;
        public int VolumeDepth;
        public VRTextureUsage VrUsage;
    }

    internal interface ICameraDepthOfFieldCommandBuffer
    {
        void SetRenderTarget(
            RenderTargetIdentifier target,
            int mipLevel,
            CubemapFace cubemapFace,
            int depthSlice);

        void DrawProcedural(
            Matrix4x4 matrix,
            Material material,
            int shaderPass,
            MeshTopology topology,
            int vertexCount,
            int instanceCount,
            MaterialPropertyBlock properties);
    }

    internal readonly struct CameraDepthOfFieldCommandBuffer :
        ICameraDepthOfFieldCommandBuffer
    {
        private readonly CommandBuffer _commandBuffer;

        public CameraDepthOfFieldCommandBuffer(CommandBuffer commandBuffer)
        {
            _commandBuffer = commandBuffer;
        }

        public void SetRenderTarget(
            RenderTargetIdentifier target,
            int mipLevel,
            CubemapFace cubemapFace,
            int depthSlice)
        {
            _commandBuffer.SetRenderTarget(
                target,
                mipLevel,
                cubemapFace,
                depthSlice
            );
        }

        public void DrawProcedural(
            Matrix4x4 matrix,
            Material material,
            int shaderPass,
            MeshTopology topology,
            int vertexCount,
            int instanceCount,
            MaterialPropertyBlock properties)
        {
            _commandBuffer.DrawProcedural(
                matrix,
                material,
                shaderPass,
                topology,
                vertexCount,
                instanceCount,
                properties
            );
        }
    }

#if UNITY_6000_0_OR_NEWER
    internal readonly struct CameraDepthOfFieldUnsafeCommandBuffer :
        ICameraDepthOfFieldCommandBuffer
    {
        private readonly UnsafeCommandBuffer _commandBuffer;

        public CameraDepthOfFieldUnsafeCommandBuffer(
            UnsafeCommandBuffer commandBuffer)
        {
            _commandBuffer = commandBuffer;
        }

        public void SetRenderTarget(
            RenderTargetIdentifier target,
            int mipLevel,
            CubemapFace cubemapFace,
            int depthSlice)
        {
            _commandBuffer.SetRenderTarget(
                target,
                mipLevel,
                cubemapFace,
                depthSlice
            );
        }

        public void DrawProcedural(
            Matrix4x4 matrix,
            Material material,
            int shaderPass,
            MeshTopology topology,
            int vertexCount,
            int instanceCount,
            MaterialPropertyBlock properties)
        {
            _commandBuffer.DrawProcedural(
                matrix,
                material,
                shaderPass,
                topology,
                vertexCount,
                instanceCount,
                properties
            );
        }
    }
#endif

    internal static class CameraDepthOfFieldPasses
    {
        private const int PrefilterNearPass = 0;
        private const int PrefilterFarPass = 1;
        private const int DilatePass = 2;
        private const int BlurPass = 3;
        private const int CompositePass = 4;
        private const int CopyPass = 5;

        private static readonly Vector4 DefaultBlitBias =
            new Vector4(1f, 1f, 0f, 0f);

        private static readonly int BlitTextureId =
            Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId =
            Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitMipLevelId =
            Shader.PropertyToID("_BlitMipLevel");
        private static readonly int CameraDepthTextureId =
            Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int OriginalTextureId =
            Shader.PropertyToID("_DofOriginalTexture");
        private static readonly int NearTextureId =
            Shader.PropertyToID("_DofNearTexture");
        private static readonly int FarTextureId =
            Shader.PropertyToID("_DofFarTexture");
        private static readonly int DofParamsId =
            Shader.PropertyToID("_DofParams");
        private static readonly int DofKernelId =
            Shader.PropertyToID("_DofKernel");
        private static readonly int SourceTexelSizeId =
            Shader.PropertyToID("_DofSourceTexelSize");
        private static readonly int DebugModeId =
            Shader.PropertyToID("_DofDebugMode");

        public static void Execute<T>(
            T commandBuffer,
            Texture original,
            Texture depth,
            Texture nearA,
            Texture nearB,
            Texture farA,
            Texture farB,
            Texture output,
            CameraDepthOfFieldConfig config,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            DrawPrefilter(
                commandBuffer,
                original,
                depth,
                nearA,
                config,
                propertyBlock,
                PrefilterNearPass
            );

            DrawPrefilter(
                commandBuffer,
                original,
                depth,
                farA,
                config,
                propertyBlock,
                PrefilterFarPass
            );

            DrawFilter(
                commandBuffer,
                nearA,
                depth,
                nearB,
                config.NearDilationRadius,
                Vector2.right,
                0f,
                config,
                propertyBlock,
                DilatePass
            );
            DrawFilter(
                commandBuffer,
                nearB,
                depth,
                nearA,
                config.NearDilationRadius,
                Vector2.up,
                0f,
                config,
                propertyBlock,
                DilatePass
            );

            DrawFilter(
                commandBuffer,
                nearA,
                depth,
                nearB,
                config.NearRadius,
                Vector2.right,
                -1f,
                config,
                propertyBlock,
                BlurPass
            );
            DrawFilter(
                commandBuffer,
                nearB,
                depth,
                nearA,
                config.NearRadius,
                Vector2.up,
                -1f,
                config,
                propertyBlock,
                BlurPass
            );

            DrawFilter(
                commandBuffer,
                farA,
                depth,
                farB,
                config.FarRadius,
                Vector2.right,
                1f,
                config,
                propertyBlock,
                BlurPass
            );
            DrawFilter(
                commandBuffer,
                farB,
                depth,
                farA,
                config.FarRadius,
                Vector2.up,
                1f,
                config,
                propertyBlock,
                BlurPass
            );

            DrawComposite(
                commandBuffer,
                original,
                depth,
                nearA,
                farA,
                output,
                config,
                propertyBlock
            );
        }

        public static void Copy<T>(
            T commandBuffer,
            Texture source,
            Texture destination,
            Material material,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            PrepareCommon(propertyBlock, source);
            Draw(
                commandBuffer,
                destination,
                material,
                CopyPass,
                propertyBlock
            );
        }

        private static void DrawPrefilter<T>(
            T commandBuffer,
            Texture source,
            Texture depth,
            Texture destination,
            CameraDepthOfFieldConfig config,
            MaterialPropertyBlock propertyBlock,
            int shaderPass)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            PrepareCommon(propertyBlock, source);
            SetDepthTexture(propertyBlock, depth);
            propertyBlock.SetVector(
                DofParamsId,
                new Vector4(
                    config.FocusDistance,
                    config.NearFocusRange,
                    config.FarFocusRange,
                    config.Intensity
                )
            );
            propertyBlock.SetVector(
                SourceTexelSizeId,
                GetTexelSize(config.FullWidth, config.FullHeight)
            );

            Draw(
                commandBuffer,
                destination,
                config.Material,
                shaderPass,
                propertyBlock
            );
        }

        private static void DrawFilter<T>(
            T commandBuffer,
            Texture source,
            Texture depth,
            Texture destination,
            float radius,
            Vector2 direction,
            float layerMode,
            CameraDepthOfFieldConfig config,
            MaterialPropertyBlock propertyBlock,
            int shaderPass)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            PrepareCommon(propertyBlock, source);
            SetDepthTexture(propertyBlock, depth);
            propertyBlock.SetVector(
                DofParamsId,
                new Vector4(
                    config.FocusDistance,
                    config.NearFocusRange,
                    config.FarFocusRange,
                    config.Intensity
                )
            );
            propertyBlock.SetVector(
                DofKernelId,
                new Vector4(
                    direction.x,
                    direction.y,
                    Mathf.Max(0f, radius),
                    layerMode
                )
            );
            propertyBlock.SetVector(
                SourceTexelSizeId,
                GetTexelSize(config.HalfWidth, config.HalfHeight)
            );

            Draw(
                commandBuffer,
                destination,
                config.Material,
                shaderPass,
                propertyBlock
            );
        }

        private static void DrawComposite<T>(
            T commandBuffer,
            Texture original,
            Texture depth,
            Texture near,
            Texture far,
            Texture destination,
            CameraDepthOfFieldConfig config,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            PrepareCommon(propertyBlock, original);
            SetDepthTexture(propertyBlock, depth);
            propertyBlock.SetTexture(OriginalTextureId, original);
            propertyBlock.SetTexture(NearTextureId, near);
            propertyBlock.SetTexture(FarTextureId, far);
            propertyBlock.SetVector(
                DofParamsId,
                new Vector4(
                    config.FocusDistance,
                    config.NearFocusRange,
                    config.FarFocusRange,
                    config.Intensity
                )
            );
            propertyBlock.SetVector(
                SourceTexelSizeId,
                GetTexelSize(config.FullWidth, config.FullHeight)
            );
            propertyBlock.SetFloat(DebugModeId, (float)config.DebugView);

            Draw(
                commandBuffer,
                destination,
                config.Material,
                CompositePass,
                propertyBlock
            );
        }

        private static void PrepareCommon(
            MaterialPropertyBlock propertyBlock,
            Texture source)
        {
            propertyBlock.Clear();
            propertyBlock.SetVector(BlitScaleBiasId, DefaultBlitBias);
            propertyBlock.SetFloat(BlitMipLevelId, 0f);
            propertyBlock.SetTexture(BlitTextureId, source);
        }

        private static void SetDepthTexture(
            MaterialPropertyBlock propertyBlock,
            Texture depth)
        {
            if (depth != null)
                propertyBlock.SetTexture(CameraDepthTextureId, depth);
        }

        private static Vector4 GetTexelSize(int width, int height)
        {
            return new Vector4(
                1f / Mathf.Max(1, width),
                1f / Mathf.Max(1, height),
                width,
                height
            );
        }

        private static void Draw<T>(
            T commandBuffer,
            Texture destination,
            Material material,
            int shaderPass,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraDepthOfFieldCommandBuffer
        {
            commandBuffer.SetRenderTarget(
                destination,
                0,
                CubemapFace.Unknown,
                0
            );
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                material,
                shaderPass,
                MeshTopology.Quads,
                4,
                1,
                propertyBlock
            );
        }
    }

    internal sealed class CameraDepthOfFieldPass :
        ScriptableRenderPass,
        IDisposable
    {
        private const string PassName = "Camera Depth Of Field";
        private const string OriginalName = PassName + " - Original";
        private const string NearAName = PassName + " - Near A";
        private const string NearBName = PassName + " - Near B";
        private const string FarAName = PassName + " - Far A";
        private const string FarBName = PassName + " - Far B";
        private const string OutputName = PassName + " - Full Resolution";

        private readonly ProfilingSampler _profilingSampler =
            new ProfilingSampler(PassName);
        private readonly MaterialPropertyBlock _propertyBlock =
            new MaterialPropertyBlock();

        private CameraDepthOfFieldConfig _config;
        private RTHandle _originalRT;
        private RTHandle _nearART;
        private RTHandle _nearBRT;
        private RTHandle _farART;
        private RTHandle _farBRT;

        public CameraDepthOfFieldPass()
        {
#if UNITY_6000_0_OR_NEWER
            requiresIntermediateTexture = true;
#endif
        }

        public void Setup(CameraDepthOfFieldConfig config)
        {
            _config = config;
            ConfigureInput(
                ScriptableRenderPassInput.Color |
                ScriptableRenderPassInput.Depth
            );
        }

        public void Dispose()
        {
            _originalRT?.Release();
            _nearART?.Release();
            _nearBRT?.Release();
            _farART?.Release();
            _farBRT?.Release();
            _originalRT = null;
            _nearART = null;
            _nearBRT = null;
            _farART = null;
            _farBRT = null;
        }

        private RenderTextureDescriptor GetHalfDescriptor()
        {
            return new RenderTextureDescriptor(
                _config.HalfWidth,
                _config.HalfHeight,
                _config.GraphicsFormat,
                0
            )
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                dimension = _config.TextureDimension,
                volumeDepth = _config.VolumeDepth,
                vrUsage = _config.VrUsage
            };
        }

        private static RenderTextureDescriptor GetOriginalDescriptor(
            RenderTextureDescriptor descriptor)
        {
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

#if !UNITY_6000_4_OR_NEWER
#pragma warning disable 618, 672
        public override void Execute(
            ScriptableRenderContext context,
            ref RenderingData renderingData)
        {
            CommandBuffer commandBuffer = CommandBufferPool.Get(PassName);
            RenderTextureDescriptor halfDescriptor = GetHalfDescriptor();
            RenderTextureDescriptor originalDescriptor = GetOriginalDescriptor(
                renderingData.cameraData.cameraTargetDescriptor
            );

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _originalRT,
                originalDescriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: OriginalName
            );
            AllocateHalfTexture(ref _nearART, halfDescriptor, NearAName);
            AllocateHalfTexture(ref _nearBRT, halfDescriptor, NearBName);
            AllocateHalfTexture(ref _farART, halfDescriptor, FarAName);
            AllocateHalfTexture(ref _farBRT, halfDescriptor, FarBName);

            RTHandle colorTarget =
                renderingData.cameraData.renderer.cameraColorTargetHandle;

            if (colorTarget == null)
            {
                CommandBufferPool.Release(commandBuffer);
                return;
            }

            Texture depthTexture = Shader.GetGlobalTexture(
                "_CameraDepthTexture");

            using (new ProfilingScope(commandBuffer, _profilingSampler))
            {
                CameraDepthOfFieldPasses.Copy(
                    new CameraDepthOfFieldCommandBuffer(commandBuffer),
                    colorTarget,
                    _originalRT,
                    _config.Material,
                    _propertyBlock
                );

                CameraDepthOfFieldPasses.Execute(
                    new CameraDepthOfFieldCommandBuffer(commandBuffer),
                    _originalRT,
                    depthTexture,
                    _nearART,
                    _nearBRT,
                    _farART,
                    _farBRT,
                    colorTarget,
                    _config,
                    _propertyBlock
                );
            }

            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }

        private static void AllocateHalfTexture(
            ref RTHandle handle,
            RenderTextureDescriptor descriptor,
            string name)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref handle,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: name
            );
        }
#pragma warning restore 618, 672
#endif

#if UNITY_6000_0_OR_NEWER
        private sealed class RenderGraphPassData
        {
            public TextureHandle ColorSource;
            public TextureHandle Depth;
            public TextureHandle NearA;
            public TextureHandle NearB;
            public TextureHandle FarA;
            public TextureHandle FarB;
            public TextureHandle Output;
            public CameraDepthOfFieldConfig Config;
            public MaterialPropertyBlock PropertyBlock;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning(
                    $"[{nameof(CameraDepthOfFieldRendererFeature)}] Camera color 仍是 back buffer，已略過本幀 DOF。"
                );
                return;
            }

            TextureHandle colorSource = resourceData.activeColorTexture;
            TextureHandle depth = resourceData.cameraDepthTexture;

            if (!colorSource.IsValid() || !depth.IsValid())
                return;

            TextureDesc halfDescriptor = new TextureDesc(GetHalfDescriptor())
            {
                clearBuffer = false
            };

            halfDescriptor.name = NearAName;
            TextureHandle nearA = renderGraph.CreateTexture(halfDescriptor);
            halfDescriptor.name = NearBName;
            TextureHandle nearB = renderGraph.CreateTexture(halfDescriptor);
            halfDescriptor.name = FarAName;
            TextureHandle farA = renderGraph.CreateTexture(halfDescriptor);
            halfDescriptor.name = FarBName;
            TextureHandle farB = renderGraph.CreateTexture(halfDescriptor);

            TextureDesc outputDescriptor = renderGraph.GetTextureDesc(colorSource);
            outputDescriptor.name = OutputName;
            outputDescriptor.clearBuffer = false;
            TextureHandle output = renderGraph.CreateTexture(outputDescriptor);

            using (IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass<RenderGraphPassData>(
                    PassName,
                    out RenderGraphPassData passData,
                    _profilingSampler))
            {
                passData.ColorSource = colorSource;
                passData.Depth = depth;
                passData.NearA = nearA;
                passData.NearB = nearB;
                passData.FarA = farA;
                passData.FarB = farB;
                passData.Output = output;
                passData.Config = _config;
                passData.PropertyBlock = _propertyBlock;

                builder.AllowPassCulling(false);
                builder.UseTexture(colorSource, AccessFlags.Read);
                builder.UseTexture(depth, AccessFlags.Read);
                builder.UseTexture(nearA, AccessFlags.ReadWrite);
                builder.UseTexture(nearB, AccessFlags.ReadWrite);
                builder.UseTexture(farA, AccessFlags.ReadWrite);
                builder.UseTexture(farB, AccessFlags.ReadWrite);
                builder.UseTexture(output, AccessFlags.Write);

                builder.SetRenderFunc<RenderGraphPassData>((data, context) =>
                {
                    CameraDepthOfFieldPasses.Execute(
                        new CameraDepthOfFieldUnsafeCommandBuffer(context.cmd),
                        data.ColorSource,
                        data.Depth,
                        data.NearA,
                        data.NearB,
                        data.FarA,
                        data.FarB,
                        data.Output,
                        data.Config,
                        data.PropertyBlock
                    );
                });
            }

            resourceData.cameraColor = output;
        }
#endif
    }
}
