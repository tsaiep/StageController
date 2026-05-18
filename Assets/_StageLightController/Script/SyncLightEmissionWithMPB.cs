using UnityEngine;

[ExecuteAlways]
public class SyncLightEmissionWithMPB : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light sourceLight;
    [SerializeField] private Renderer targetRenderer;

    [Header("Emission Settings")]
    [SerializeField] private float emissionMultiplier = 1f;
    [SerializeField] private bool includeLightIntensity = false;

    [Header("Shader Property")]
    [SerializeField] private string emissionColorProperty = "_EmissionColor";

    private MaterialPropertyBlock propertyBlock;
    private int emissionColorId;

    private Color lastLightColor;
    private float lastLightIntensity;
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
        propertyBlock ??= new MaterialPropertyBlock();
        emissionColorId = Shader.PropertyToID(emissionColorProperty);
    }

    private void ApplyEmission(bool forceUpdate)
    {
        if (sourceLight == null || targetRenderer == null)
            return;

        Color lightColor = sourceLight.color;
        float lightIntensity = includeLightIntensity ? sourceLight.intensity : 1f;

        bool changed =
            forceUpdate ||
            lightColor != lastLightColor ||
            !Mathf.Approximately(lightIntensity, lastLightIntensity) ||
            !Mathf.Approximately(emissionMultiplier, lastEmissionMultiplier) ||
            includeLightIntensity != lastIncludeLightIntensity;

        if (!changed)
            return;

        Color emissionColor = lightColor * lightIntensity * emissionMultiplier;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(emissionColorId, emissionColor);
        targetRenderer.SetPropertyBlock(propertyBlock);

        lastLightColor = lightColor;
        lastLightIntensity = lightIntensity;
        lastEmissionMultiplier = emissionMultiplier;
        lastIncludeLightIntensity = includeLightIntensity;
    }
}