using UnityEngine;

public class AudioVFXSwitcher : MonoBehaviour
{
    // 改回最原始標籤，確保不報錯
    public UnifiedStageController audioSourceProcessor;
    public GameObject[] vfxPresets;
    public float threshold = 0.5f;
    public float cooldown = 0.5f;

    private int currentIndex = -1;
    private float lastSwitchTime;

    void Update()
    {
        if (audioSourceProcessor == null || vfxPresets == null || vfxPresets.Length == 0) return;

        // 取得平滑後的能量 (請確保 AudioGradientLight 裡面有公開這個變數或方法)
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