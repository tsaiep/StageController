using UnityEngine;

public class AudioVFXTripleSwitcher : MonoBehaviour
{
    [Header("Audio Reactivity Core")]
    public UnifiedStageController audioProcessor;

    [Header("Low Band VFX (Bass/Kick)")]
    [Tooltip("Good for ground shakes, large particle bursts, and heavy impact hits.")]
    public GameObject bassVFX;
    public float bassThreshold = 0.6f;

    [Header("Mid Band VFX (Vocal/Snare)")]
    [Tooltip("Good for ambient particles and flashing glow accents.")]
    public GameObject midVFX;
    public float midThreshold = 0.4f;

    [Header("High Band VFX (Hi-hat/Synth)")]
    [Tooltip("Good for sparks, high-frequency flashes, and rim-light accents.")]
    public GameObject highVFX;
    public float highThreshold = 0.3f;

    void Update()
    {
        if (audioProcessor == null) return;

        // Low band.
        HandleVFX(bassVFX, audioProcessor.GetLowEnergy(), bassThreshold);

        // Mid band.
        HandleVFX(midVFX, audioProcessor.GetMidEnergy(), midThreshold);

        // High band.
        HandleVFX(highVFX, audioProcessor.GetHighEnergy(), highThreshold);
    }

    void HandleVFX(GameObject vfx, float energy, float threshold)
    {
        if (vfx == null) return;

        // Toggle the effect based on the current band's energy.
        vfx.SetActive(energy > threshold);
    }
}
