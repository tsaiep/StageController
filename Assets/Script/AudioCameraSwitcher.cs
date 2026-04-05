using UnityEngine;

public class AudioCameraSwitcher : MonoBehaviour
{
    [Header("音訊感應配置")]
    public UnifiedStageController audioProcessor; // 引用你之前的音訊處理器

    [Header("鏡頭方案庫")]
    [Tooltip("對應企劃書：全身、半身、特寫等鏡頭樣板")]
    public GameObject[] cameraPresets;

    [Header("自動切換參數")]
    [Range(0f, 1f)] public float threshold = 0.5f; // 靈敏度
    public float cooldown = 1.5f; // 企劃書要求：穩定第一，防止頻繁閃切

    private int lastIndex = -1;
    private float lastSwitchTime;

    void Update()
    {
        if (audioProcessor == null || cameraPresets.Length < 2) return;

        // 取得低音能量
        float lowEnergy = audioProcessor.GetLowEnergy();

        // 判斷是否觸發切換
        if (lowEnergy > threshold && Time.time > lastSwitchTime + cooldown)
        {
            SwitchToRandomCamera();
            lastSwitchTime = Time.time;
        }
    }

    void SwitchToRandomCamera()
    {
        int newIndex = lastIndex;

        // 企劃書要求：避免連續使用過於相似的鏡頭
        while (newIndex == lastIndex)
        {
            newIndex = Random.Range(0, cameraPresets.Length);
        }

        // 執行切換
        for (int i = 0; i < cameraPresets.Length; i++)
        {
            if (cameraPresets[i] != null)
                cameraPresets[i].SetActive(i == newIndex);
        }

        lastIndex = newIndex;
        Debug.Log($"音訊觸發：已自動切換至鏡頭方案 {newIndex}");
    }
}