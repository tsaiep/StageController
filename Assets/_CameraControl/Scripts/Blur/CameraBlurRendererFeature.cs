// Kawase blur rendering is derived from Unified Universal Blur by Luka Kldiashvili.
// See Assets/_CameraControl/ThirdParty/UnifiedUniversalBlur-LICENSE.txt.

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
    public enum CameraBlurScaleMode
    {
        Disabled,
        ScreenHeight,
        ScreenWidth
    }

    /// <summary>
    /// Applies a full-screen Kawase blur only to cameras carrying an active
    /// CameraBlurState. No global texture or global blur weight is used.
    /// </summary>
    public sealed class CameraBlurRendererFeature : ScriptableRendererFeature
    {
        [Header("Blur Quality")]
        [Range(1, 12)]
        [SerializeField] private int iterations = 4;

        [Range(1f, 10f)]
        [SerializeField] private float downsample = 2f;

        [SerializeField] private bool enableMipMaps = true;
        [SerializeField] private float scale = 1f;
        [SerializeField] private float offset = 1f;

        [Header("Resolution Scaling")]
        [SerializeField] private CameraBlurScaleMode scaleMode =
            CameraBlurScaleMode.ScreenHeight;

        [Min(1f)]
        [SerializeField] private float scaleReferenceSize = 1080f;

        [Header("Rendering")]
        [SerializeField] private RenderPassEvent injectionPoint =
            RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField, HideInInspector]
        private Shader shader;

        private Material _material;
        private CameraBlurPass _blurPass;
        private bool _hasLoggedMissingShader;

        public override void Create()
        {
            EnsureMaterial();
            _blurPass = new CameraBlurPass
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

            if (!camera.TryGetComponent(out CameraBlurState state) ||
                !state.isActiveAndEnabled ||
                state.Intensity <= CameraBlurPass.MinimumIntensity ||
                state.BlendWeight <= CameraBlurPass.MinimumIntensity)
            {
                return;
            }

            if (!EnsureMaterial())
                return;

            RenderTextureDescriptor descriptor =
                renderingData.cameraData.cameraTargetDescriptor;

            CameraBlurConfig config = new CameraBlurConfig
            {
                Material = _material,
                Intensity = state.Intensity,
                CompositeWeight = state.BlendWeight,
                Downsample = Mathf.Clamp(downsample, 1f, 10f),
                Scale = CalculateScale(descriptor),
                Offset = offset,
                Iterations = Mathf.Clamp(iterations, 1, 12),
                Width = Mathf.Max(1, Mathf.RoundToInt(
                    descriptor.width / Mathf.Max(1f, downsample))),
                Height = Mathf.Max(1, Mathf.RoundToInt(
                    descriptor.height / Mathf.Max(1f, downsample))),
                GraphicsFormat = descriptor.graphicsFormat != GraphicsFormat.None
                    ? descriptor.graphicsFormat
                    : GraphicsFormat.B10G11R11_UFloatPack32,
                EnableMipMaps = enableMipMaps,
                TextureDimension = descriptor.dimension,
                VolumeDepth = Mathf.Max(1, descriptor.volumeDepth),
                VrUsage = descriptor.vrUsage
            };

            _blurPass.renderPassEvent = injectionPoint;
            _blurPass.Setup(config);
            renderer.EnqueuePass(_blurPass);
        }

        protected override void Dispose(bool disposing)
        {
            _blurPass?.Dispose();
            _blurPass = null;
            CoreUtils.Destroy(_material);
            _material = null;
        }

        private bool EnsureMaterial()
        {
            if (shader == null)
                shader = Shader.Find("Hidden/StageController/CameraBlur");

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
                    $"[{nameof(CameraBlurRendererFeature)}] 找不到 Camera Blur shader，已略過模糊 pass。",
                    this
                );
                _hasLoggedMissingShader = true;
            }

            return false;
        }

        private float CalculateScale(RenderTextureDescriptor descriptor)
        {
            float referenceSize = Mathf.Max(1f, scaleReferenceSize);

            return scaleMode switch
            {
                CameraBlurScaleMode.ScreenHeight =>
                    scale * descriptor.height / referenceSize,
                CameraBlurScaleMode.ScreenWidth =>
                    scale * descriptor.width / referenceSize,
                _ => scale
            };
        }
    }

    internal struct CameraBlurConfig
    {
        public Material Material;
        public float Downsample;
        public float Intensity;
        public float CompositeWeight;
        public float Scale;
        public float Offset;
        public int Iterations;
        public int Width;
        public int Height;
        public GraphicsFormat GraphicsFormat;
        public bool EnableMipMaps;
        public TextureDimension TextureDimension;
        public int VolumeDepth;
        public VRTextureUsage VrUsage;
    }

    internal interface ICameraBlurCommandBuffer
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

    internal readonly struct CameraBlurCommandBuffer : ICameraBlurCommandBuffer
    {
        private readonly CommandBuffer _commandBuffer;

        public CameraBlurCommandBuffer(CommandBuffer commandBuffer)
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
    internal readonly struct CameraBlurUnsafeCommandBuffer :
        ICameraBlurCommandBuffer
    {
        private readonly UnsafeCommandBuffer _commandBuffer;

        public CameraBlurUnsafeCommandBuffer(UnsafeCommandBuffer commandBuffer)
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

    internal static class CameraBlurPasses
    {
        private static readonly Vector4 DefaultBlitBias =
            new Vector4(1f, 1f, 0f, 0f);

        private static readonly int IterationId =
            Shader.PropertyToID("_Iteration");

        private static readonly int BlurParamsId =
            Shader.PropertyToID("_BlurParams");

        private static readonly int BlitTextureId =
            Shader.PropertyToID("_BlitTexture");

        private static readonly int OriginalTextureId =
            Shader.PropertyToID("_CameraBlurOriginalTexture");

        private static readonly int CompositeWeightId =
            Shader.PropertyToID("_CameraBlurCompositeWeight");

        private static readonly int BlitScaleBiasId =
            Shader.PropertyToID("_BlitScaleBias");

        private static readonly int BlitMipLevelId =
            Shader.PropertyToID("_BlitMipLevel");

        public static void Execute<T>(
            T commandBuffer,
            Texture colorSource,
            Texture source,
            Texture destination,
            Texture output,
            CameraBlurConfig config,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraBlurCommandBuffer
        {
            Texture ping = source;
            Texture pong = destination;

            // This keeps the final blurred image in destination for both odd
            // and even iteration counts.
            if (config.Iterations % 2 == 1)
                (ping, pong) = (pong, ping);

            BlitKawase(
                commandBuffer,
                colorSource,
                ping,
                config,
                propertyBlock,
                CalculateOffset(config, 0),
                0
            );

            for (int i = 1; i < config.Iterations; i++)
            {
                BlitKawase(
                    commandBuffer,
                    ping,
                    pong,
                    config,
                    propertyBlock,
                    CalculateOffset(config, i),
                    i - 1
                );

                (ping, pong) = (pong, ping);
            }

            BlitComposite(
                commandBuffer,
                colorSource,
                destination,
                output,
                config.Material,
                config.CompositeWeight,
                propertyBlock
            );
        }

        public static void Copy<T>(
            T commandBuffer,
            Texture source,
            Texture destination,
            Material material,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraBlurCommandBuffer
        {
            BlitComposite(
                commandBuffer,
                source,
                source,
                destination,
                material,
                1f,
                propertyBlock
            );
        }

        private static float CalculateOffset(
            CameraBlurConfig config,
            int iteration)
        {
            return (config.Offset + iteration * config.Scale) /
                config.Downsample;
        }

        private static void BlitKawase<T>(
            T commandBuffer,
            Texture source,
            Texture destination,
            CameraBlurConfig config,
            MaterialPropertyBlock propertyBlock,
            float mipOffset,
            int iteration)
            where T : ICameraBlurCommandBuffer
        {
            propertyBlock.Clear();
            propertyBlock.SetVector(BlitScaleBiasId, DefaultBlitBias);
            propertyBlock.SetInt(IterationId, iteration);
            propertyBlock.SetVector(
                BlurParamsId,
                new Vector4(
                    config.Intensity,
                    config.Scale,
                    config.Downsample,
                    config.Offset
                )
            );
            propertyBlock.SetTexture(BlitTextureId, source);
            propertyBlock.SetFloat(
                BlitMipLevelId,
                config.EnableMipMaps && mipOffset > 0f
                    ? Mathf.Log(mipOffset, 2f)
                    : 0f
            );

            commandBuffer.SetRenderTarget(
                destination,
                0,
                CubemapFace.Unknown,
                0
            );
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                config.Material,
                0,
                MeshTopology.Quads,
                4,
                1,
                propertyBlock
            );
        }

        private static void BlitComposite<T>(
            T commandBuffer,
            Texture original,
            Texture blurred,
            Texture destination,
            Material material,
            float compositeWeight,
            MaterialPropertyBlock propertyBlock)
            where T : ICameraBlurCommandBuffer
        {
            propertyBlock.Clear();
            propertyBlock.SetVector(BlitScaleBiasId, DefaultBlitBias);
            propertyBlock.SetTexture(BlitTextureId, blurred);
            propertyBlock.SetTexture(OriginalTextureId, original);
            propertyBlock.SetFloat(
                CompositeWeightId,
                Mathf.Clamp01(compositeWeight)
            );
            propertyBlock.SetFloat(BlitMipLevelId, 0f);

            commandBuffer.SetRenderTarget(
                destination,
                0,
                CubemapFace.Unknown,
                0
            );
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                material,
                1,
                MeshTopology.Quads,
                4,
                1,
                propertyBlock
            );
        }
    }

    internal sealed class CameraBlurPass : ScriptableRenderPass, IDisposable
    {
        public const float MinimumIntensity = 0.0001f;

        private const string PassName = "Camera Cross Fade Blur";
        private const string OriginalName = PassName + " - Original";
        private const string SourceName = PassName + " - Ping";
        private const string DestinationName = PassName + " - Pong";
        private const string OutputName = PassName + " - Full Resolution";

        private readonly ProfilingSampler _profilingSampler =
            new ProfilingSampler(PassName);

        private readonly MaterialPropertyBlock _propertyBlock =
            new MaterialPropertyBlock();

        private CameraBlurConfig _config;
        private RTHandle _originalRT;
        private RTHandle _sourceRT;
        private RTHandle _destinationRT;

        public CameraBlurPass()
        {
#if UNITY_6000_0_OR_NEWER
            requiresIntermediateTexture = true;
#endif
        }

        public void Setup(CameraBlurConfig config)
        {
            _config = config;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void Dispose()
        {
            _originalRT?.Release();
            _sourceRT?.Release();
            _destinationRT?.Release();
            _originalRT = null;
            _sourceRT = null;
            _destinationRT = null;
        }

        private RenderTextureDescriptor GetBlurDescriptor()
        {
            return new RenderTextureDescriptor(
                _config.Width,
                _config.Height,
                _config.GraphicsFormat,
                0
            )
            {
                msaaSamples = 1,
                useMipMap = _config.EnableMipMaps,
                autoGenerateMips = _config.EnableMipMaps,
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
            RenderTextureDescriptor descriptor = GetBlurDescriptor();
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

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _sourceRT,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: SourceName
            );
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _destinationRT,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: DestinationName
            );

            RTHandle colorTarget =
                renderingData.cameraData.renderer.cameraColorTargetHandle;

            if (colorTarget == null)
            {
                CommandBufferPool.Release(commandBuffer);
                return;
            }

            using (new ProfilingScope(commandBuffer, _profilingSampler))
            {
                CameraBlurPasses.Copy(
                    new CameraBlurCommandBuffer(commandBuffer),
                    colorTarget,
                    _originalRT,
                    _config.Material,
                    _propertyBlock
                );

                CameraBlurPasses.Execute(
                    new CameraBlurCommandBuffer(commandBuffer),
                    _originalRT,
                    _sourceRT,
                    _destinationRT,
                    colorTarget,
                    _config,
                    _propertyBlock
                );
            }

            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }
#pragma warning restore 618, 672
#endif

#if UNITY_6000_0_OR_NEWER
        private sealed class RenderGraphPassData
        {
            public TextureHandle ColorSource;
            public TextureHandle Source;
            public TextureHandle Destination;
            public TextureHandle Output;
            public CameraBlurConfig Config;
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
                    $"[{nameof(CameraBlurRendererFeature)}] Camera color 仍是 back buffer，已略過本幀模糊。"
                );
                return;
            }

            TextureHandle colorSource = resourceData.activeColorTexture;

            if (!colorSource.IsValid())
                return;

            TextureDesc blurDescriptor =
                new TextureDesc(GetBlurDescriptor())
                {
                    clearBuffer = false
                };

            blurDescriptor.name = SourceName;
            TextureHandle source = renderGraph.CreateTexture(blurDescriptor);

            blurDescriptor.name = DestinationName;
            TextureHandle destination = renderGraph.CreateTexture(blurDescriptor);

            TextureDesc outputDescriptor =
                renderGraph.GetTextureDesc(colorSource);
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
                passData.Source = source;
                passData.Destination = destination;
                passData.Output = output;
                passData.Config = _config;
                passData.PropertyBlock = _propertyBlock;

                builder.AllowPassCulling(false);
                builder.UseTexture(colorSource, AccessFlags.Read);
                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(destination, AccessFlags.ReadWrite);
                builder.UseTexture(output, AccessFlags.Write);

                builder.SetRenderFunc<RenderGraphPassData>((data, context) =>
                {
                    CameraBlurPasses.Execute(
                        new CameraBlurUnsafeCommandBuffer(context.cmd),
                        data.ColorSource,
                        data.Source,
                        data.Destination,
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
