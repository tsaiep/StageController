using UnityEngine;

[ExecuteAlways]
public class SyncLightEmissionWithMPB : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sourceLight;
    [SerializeField, InspectorName("Source Renderer")] private Renderer[] sourceRenderers;
    [SerializeField] private Renderer targetRenderer;

    [Header("Emission Settings")]
    [SerializeField] private float emissionMultiplier = 1f;
    [SerializeField] private bool includeLightIntensity = false;

    [Header("Source Selection")]
    [SerializeField] private bool requireSourceLightEnabled = true;
    [SerializeField] private bool requireSourceRendererEnabled = true;

    [Header("Shader Property")]
    [SerializeField] private string sourceRendererColorProperty = "_BaseColor";
    [SerializeField] private string emissionColorProperty = "_EmissionColor";

    private enum SourceType
    {
        None,
        Light,
        Renderer
    }

    private MaterialPropertyBlock targetPropertyBlock;
    private MaterialPropertyBlock sourcePropertyBlock;
    private int sourceRendererColorId;
    private int emissionColorId;

    private SourceType lastSourceType;
    private int lastSourceRendererIndex = -1;
    private Color lastSourceColor;
    private float lastSourceIntensity;
    private float lastEmissionMultiplier;
    private bool lastIncludeLightIntensity;

    private void Reset()
    {
        sourceLight = GetComponentInChildren<Light>();
        targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        Init();
        ApplyEmission(true);
    }

    private void OnValidate()
    {
        Init();
        ApplyEmission(true);
    }

    private void LateUpdate()
    {
        ApplyEmission(false);
    }

    private void Init()
    {
        targetPropertyBlock ??= new MaterialPropertyBlock();
        sourcePropertyBlock ??= new MaterialPropertyBlock();
        sourceRendererColorId = Shader.PropertyToID(sourceRendererColorProperty);
        emissionColorId = Shader.PropertyToID(emissionColorProperty);
    }

    private void ApplyEmission(bool forceUpdate)
    {
        if (targetRenderer == null)
            return;

        if (!TryGetSourceColor(out Color sourceColor, out float sourceIntensity, out SourceType sourceType, out int sourceRendererIndex))
            return;

        bool changed =
            forceUpdate ||
            sourceType != lastSourceType ||
            sourceRendererIndex != lastSourceRendererIndex ||
            sourceColor != lastSourceColor ||
            !Mathf.Approximately(sourceIntensity, lastSourceIntensity) ||
            !Mathf.Approximately(emissionMultiplier, lastEmissionMultiplier) ||
            includeLightIntensity != lastIncludeLightIntensity;

        if (!changed)
            return;

        Color emissionColor = sourceColor * sourceIntensity * emissionMultiplier;

        targetRenderer.GetPropertyBlock(targetPropertyBlock);
        targetPropertyBlock.SetColor(emissionColorId, emissionColor);
        targetRenderer.SetPropertyBlock(targetPropertyBlock);

        lastSourceType = sourceType;
        lastSourceRendererIndex = sourceRendererIndex;
        lastSourceColor = sourceColor;
        lastSourceIntensity = sourceIntensity;
        lastEmissionMultiplier = emissionMultiplier;
        lastIncludeLightIntensity = includeLightIntensity;
    }

    private bool TryGetSourceColor(out Color color, out float intensity, out SourceType sourceType, out int sourceRendererIndex)
    {
        if (sourceLight != null && (!requireSourceLightEnabled || IsSourceLightEnabled(sourceLight)))
        {
            color = sourceLight.color;
            intensity = includeLightIntensity ? sourceLight.intensity : 1f;
            sourceType = SourceType.Light;
            sourceRendererIndex = -1;
            return true;
        }

        if (sourceRenderers != null)
        {
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                if (!TryGetRendererColor(sourceRenderers[i], out color))
                    continue;

                intensity = 1f;
                sourceType = SourceType.Renderer;
                sourceRendererIndex = i;
                return true;
            }
        }

        color = default;
        intensity = 1f;
        sourceType = SourceType.None;
        sourceRendererIndex = -1;
        return false;
    }

    private bool TryGetRendererColor(Renderer sourceRenderer, out Color color)
    {
        if (sourceRenderer == null)
        {
            color = default;
            return false;
        }

        if (requireSourceRendererEnabled && !IsSourceRendererEnabled(sourceRenderer))
        {
            color = default;
            return false;
        }

        bool hasMaterialColor = TryGetSharedMaterialColor(sourceRenderer, out Color materialColor);
        color = materialColor;

        if (sourceRenderer.HasPropertyBlock())
        {
            sourcePropertyBlock.Clear();
            sourceRenderer.GetPropertyBlock(sourcePropertyBlock);
            Color propertyBlockColor = sourcePropertyBlock.GetColor(sourceRendererColorId);
            if (propertyBlockColor != default || !hasMaterialColor)
            {
                color = propertyBlockColor;
                return true;
            }
        }

        return hasMaterialColor;
    }

    private bool TryGetSharedMaterialColor(Renderer sourceRenderer, out Color color)
    {
        Material[] materials = sourceRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || !material.HasProperty(sourceRendererColorId))
                continue;

            color = material.GetColor(sourceRendererColorId);
            return true;
        }

        color = default;
        return false;
    }

    private static bool IsSourceLightEnabled(Light light)
    {
        return light.enabled && light.gameObject.activeInHierarchy;
    }

    private static bool IsSourceRendererEnabled(Renderer renderer)
    {
        return renderer.enabled && renderer.gameObject.activeInHierarchy;
    }
}
