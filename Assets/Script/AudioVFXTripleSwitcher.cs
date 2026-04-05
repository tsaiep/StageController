using UnityEngine;

public class AudioVFXTripleSwitcher : MonoBehaviour
{
    [Header("音訊感應核心")]
    public UnifiedStageController audioProcessor;

    [Header("低音特效 (Bass/Kick)")]
    [Tooltip("適合：地面震動、大型粒子噴發、重拍衝擊")]
    public GameObject bassVFX;
    public float bassThreshold = 0.6f;

    [Header("中音特效 (Vocal/Snare)")]
    [Tooltip("適合：環境氛圍粒子、光芒閃爍")]
    public GameObject midVFX;
    public float midThreshold = 0.4f;

    [Header("高音特效 (Hi-hat/Synth)")]
    [Tooltip("適合：細碎火花、高頻閃爍、邊緣光")]
    public GameObject highVFX;
    public float highThreshold = 0.3f;

    void Update()
    {
        if (audioProcessor == null) return;

        // 1. 低音控制 (取自你原本的 curLow)
        HandleVFX(bassVFX, audioProcessor.GetLowEnergy(), bassThreshold);

        // 2. 中音控制 (需在 AudioGradientLight 增加 GetMidEnergy)
        HandleVFX(midVFX, audioProcessor.GetMidEnergy(), midThreshold);

        // 3. 高音控制 (需在 AudioGradientLight 增加 GetHighEnergy)
        HandleVFX(highVFX, audioProcessor.GetHighEnergy(), highThreshold);
    }

    void HandleVFX(GameObject vfx, float energy, float threshold)
    {
        if (vfx == null) return;
        // 能量超過閾值時開啟，否則關閉 (符合企劃書：特效可開關)
        vfx.SetActive(energy > threshold);
    }
}