using UnityEngine;

public class AudioVFXSwitcher : MonoBehaviour
{
    public UnifiedStageController audioSourceProcessor;
    public GameObject[] vfxPresets;
    public float threshold = 0.5f;
    public float cooldown = 0.5f;

    private int currentIndex = -1;
    private float lastSwitchTime;

    void Update()
    {
        if (audioSourceProcessor == null || vfxPresets == null || vfxPresets.Length == 0) return;

        // Use the smoothed low-band energy to drive VFX switching.
        float currentEnergy = audioSourceProcessor.GetLowEnergy();

        if (currentEnergy > threshold && Time.time > lastSwitchTime + cooldown)
        {
            SwitchToNextVFX();
            lastSwitchTime = Time.time;
        }
    }

    void SwitchToNextVFX()
    {
        currentIndex = (currentIndex + 1) % vfxPresets.Length;

        for (int i = 0; i < vfxPresets.Length; i++)
        {
            if (vfxPresets[i] != null)
                vfxPresets[i].SetActive(i == currentIndex);
        }
    }
}
