using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[AddComponentMenu("Stage Controller/Lightstrip MPB Control")]
public class LightstripMBPControl : MonoBehaviour
{
    [Serializable]
    public sealed class LightstripFloatParameters
    {
        [Header("Manual Mode")]
        [Range(0f, 1f)] public float manualMode = 0f;
        [Range(0f, 1f)] public float manualModeControl = 0f;
        
        [Header("Animation Control")]
        [Range(0f, 1f)] public float scrollingModeWeight = 1f;
        [Range(0f, 1f)] public float scrollingPingPongMode = 0f;
        [Range(0f, 1f)] public float scrollingFromCenter = 0f;
        [Range(-1f, 1f)] public float scrollingHeadLean = 0f;
        [Range(0f, 1f)] public float sparklingModeWeight = 0f;
        [Range(0f, 1f)] public float sparklingModeRandomWeight = 0f;
        
        [Header("Scrolling")]
        [Min(0f)] public float scrollingSpeed = 1f;
        [Min(0f)] public float scrollingFrequency = 1f;
        [Min(0f)] public float scrollingIntervalDuration = 0f;
        [Min(0f)] public float scrollingHoldDuration = 0f;
        [Range(0f, 1f)] public float scrollingSmoothFactor = 0f;

        [Header("Sparkling")]
        [Min(0f)] public float sparklingSpeed = 3f;
        [Range(0f, 1f)] public float sparklingSmoothFactor = 1f;
    }

    private static readonly int ManualModeControlId = Shader.PropertyToID("_Manual_Mode_Control");
    private static readonly int ManualModeId = Shader.PropertyToID("_Manual_Mode");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int ColorMultiplierId = Shader.PropertyToID("_Color_Multiplyer");
    private static readonly int LightUnitCountId = Shader.PropertyToID("_Light_Unit_Count");
    private static readonly int ScrollingModeWeightId = Shader.PropertyToID("_Scrolling_Mode_Weight");
    private static readonly int ScrollingPingPongModeId = Shader.PropertyToID("_Scrolling_PingPong_Mode");
    private static readonly int ScrollingFromCenterId = Shader.PropertyToID("_Scrolling_From_Center");
    private static readonly int ScrollingHeadLeanId = Shader.PropertyToID("_Scrolling_Head_Lean");
    private static readonly int ScrollingSmoothFactorId = Shader.PropertyToID("_Scrolling_Smooth_Factor");
    private static readonly int ScrollingSpeedId = Shader.PropertyToID("_Scrolling_Speed");
    private static readonly int ScrollingFrequencyId = Shader.PropertyToID("_Scrolling_Frequency");
    private static readonly int ScrollingIntervalDurationId = Shader.PropertyToID("_Scrolling_Interval_Duration");
    private static readonly int ScrollingHoldDurationId = Shader.PropertyToID("_Scrolling_Hold_Duration");
    private static readonly int SparklingModeWeightId = Shader.PropertyToID("_Sparkling_Mode_Weight");
    private static readonly int SparklingModeRandomWeightId = Shader.PropertyToID("_Sparkling_Mode_Random_Weight");
    private static readonly int SparklingSmoothFactorId = Shader.PropertyToID("_Sparkling_Smooth_Factor");
    private static readonly int SparklingSpeedId = Shader.PropertyToID("_Sparkling_Speed");

    [Header("Targets")]
    [Tooltip("Renderers that share this controller's Lightstrip material parameters. Null entries are skipped.")]
    [SerializeField] private List<Renderer> lightstripRenderers = new List<Renderer>();
    
    [Header("Light Units")]
    [Min(0f)] public float lightUnitCount = 12f;
    
    [Header("Color")]
    [ColorUsage(true, true)]
    [SerializeField] private Color color = Color.white;
    [SerializeField, Min(0f)] public float colorMultiplier = 1f;
    
    [Header("Gradient Texture")]
    [GradientUsage(true)]
    [SerializeField] private Gradient gradient = CreateDefaultGradient();

    [Tooltip("Texture property reference used by the shader to sample the baked gradient. SH_Lightstrip's built-in Gradient property is named _GradientMap, but Shader Graph gradients cannot be set directly through MPB.")]
    [SerializeField] private string gradientTexturePropertyReference = "_GradientMap";

    [Tooltip("Width of the generated 1D gradient texture.")]
    [Min(2)]
    [SerializeField] private int gradientTextureResolution = 256;

    [SerializeField] private LightstripFloatParameters properties = new LightstripFloatParameters();
    
    private MaterialPropertyBlock propertyBlock;
    private Texture2D gradientTexture;
    private int gradientTexturePropertyId;
    private int bakedGradientHash;
    private int bakedGradientResolution;
    private bool propertiesDirty = true;
    private bool gradientDirty = true;

    public List<Renderer> LightstripRenderers => lightstripRenderers;
    public LightstripFloatParameters Properties => properties;

    private void OnEnable()
    {
        EnsureInitialized();
        MarkDirty();
        ApplyProperties();
    }

    private void Update()
    {
        if (!propertiesDirty && !gradientDirty)
            return;

        ApplyProperties();
    }

    private void OnValidate()
    {
        if (gradientTextureResolution < 2)
            gradientTextureResolution = 2;

        gradientDirty = true;
        MarkDirty();

        if (isActiveAndEnabled)
            ApplyProperties();
    }

    private void OnDestroy()
    {
        DestroyGradientTexture();
    }

    [ContextMenu("Apply Lightstrip Properties")]
    public void ApplyProperties()
    {
        EnsureInitialized();
        RebuildGradientTextureIfNeeded();

        for (int i = 0; i < lightstripRenderers.Count; i++)
        {
            Renderer targetRenderer = lightstripRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            FillPropertyBlock();
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        propertiesDirty = false;
        gradientDirty = false;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            SceneView.RepaintAll();
#endif
    }

    public void MarkDirty()
    {
        propertiesDirty = true;
    }

    public void MarkGradientDirty()
    {
        gradientDirty = true;
        MarkDirty();
    }

    private void EnsureInitialized()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        gradientTexturePropertyId = Shader.PropertyToID(string.IsNullOrWhiteSpace(gradientTexturePropertyReference)
            ? "_GradientMap"
            : gradientTexturePropertyReference);
    }

    private void FillPropertyBlock()
    {
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetFloat(ManualModeControlId, properties.manualModeControl);
        propertyBlock.SetFloat(ManualModeId, properties.manualMode);
        propertyBlock.SetFloat(ColorMultiplierId, colorMultiplier);
        propertyBlock.SetFloat(LightUnitCountId, lightUnitCount);
        propertyBlock.SetFloat(ScrollingModeWeightId, properties.scrollingModeWeight);
        propertyBlock.SetFloat(ScrollingPingPongModeId, properties.scrollingPingPongMode);
        propertyBlock.SetFloat(ScrollingFromCenterId, properties.scrollingFromCenter);
        propertyBlock.SetFloat(ScrollingHeadLeanId, properties.scrollingHeadLean);
        propertyBlock.SetFloat(ScrollingSmoothFactorId, properties.scrollingSmoothFactor);
        propertyBlock.SetFloat(ScrollingSpeedId, properties.scrollingSpeed);
        propertyBlock.SetFloat(ScrollingFrequencyId, properties.scrollingFrequency);
        propertyBlock.SetFloat(ScrollingIntervalDurationId, properties.scrollingIntervalDuration);
        propertyBlock.SetFloat(ScrollingHoldDurationId, properties.scrollingHoldDuration);
        propertyBlock.SetFloat(SparklingModeWeightId, properties.sparklingModeWeight);
        propertyBlock.SetFloat(SparklingModeRandomWeightId, properties.sparklingModeRandomWeight);
        propertyBlock.SetFloat(SparklingSmoothFactorId, properties.sparklingSmoothFactor);
        propertyBlock.SetFloat(SparklingSpeedId, properties.sparklingSpeed);

        if (gradientTexture != null)
            propertyBlock.SetTexture(gradientTexturePropertyId, gradientTexture);
    }

    private void RebuildGradientTextureIfNeeded()
    {
        if (!gradientDirty)
            return;

        int currentHash = GetGradientHash();
        if (gradientTexture != null &&
            bakedGradientResolution == gradientTextureResolution &&
            bakedGradientHash == currentHash)
        {
            return;
        }

        if (gradientTexture == null || gradientTexture.width != gradientTextureResolution)
        {
            DestroyGradientTexture();
            gradientTexture = new Texture2D(gradientTextureResolution, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = $"{nameof(LightstripMBPControl)} Gradient",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
        }

        Gradient sourceGradient = gradient ?? CreateDefaultGradient();
        int lastPixel = gradientTextureResolution - 1;
        for (int x = 0; x < gradientTextureResolution; x++)
        {
            float time = lastPixel <= 0 ? 0f : (float)x / lastPixel;
            gradientTexture.SetPixel(x, 0, sourceGradient.Evaluate(time));
        }

        gradientTexture.Apply(false, false);
        bakedGradientHash = currentHash;
        bakedGradientResolution = gradientTextureResolution;
    }

    private int GetGradientHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + gradientTextureResolution;

            if (gradient == null)
                return hash;

            GradientColorKey[] colorKeys = gradient.colorKeys;
            GradientAlphaKey[] alphaKeys = gradient.alphaKeys;

            for (int i = 0; i < colorKeys.Length; i++)
            {
                hash = hash * 31 + colorKeys[i].color.GetHashCode();
                hash = hash * 31 + colorKeys[i].time.GetHashCode();
            }

            for (int i = 0; i < alphaKeys.Length; i++)
            {
                hash = hash * 31 + alphaKeys[i].alpha.GetHashCode();
                hash = hash * 31 + alphaKeys[i].time.GetHashCode();
            }

            hash = hash * 31 + gradient.mode.GetHashCode();
            return hash;
        }
    }

    private void DestroyGradientTexture()
    {
        if (gradientTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(gradientTexture);
        else
            DestroyImmediate(gradientTexture);

        gradientTexture = null;
        bakedGradientHash = 0;
        bakedGradientResolution = 0;
    }

    private static Gradient CreateDefaultGradient()
    {
        Gradient defaultGradient = new Gradient();
        defaultGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.black, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return defaultGradient;
    }
}
