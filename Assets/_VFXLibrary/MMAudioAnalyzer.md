# MMAudioAnalyzer 音訊驅動操作教學

參考文件：https://feel-docs.moremountains.com/mmaudioanalyzer.html

`MMAudioAnalyzer` 是 More Mountains Feel / MMTools 提供的音訊分析元件，可在 Runtime 分析音樂、場景輸出或麥克風輸入，並輸出整體音量、各頻段音量、正規化值、平滑值與 Beat 事件。它適合用來驅動各種特效或物件，例如燈光亮度、物件縮放、材質參數、VFX Graph 參數、Animator 參數、Feedbacks 或自訂 UnityEvent。

---

## 基本設定

1. 在場景建立一個空物件，例如 `AudioAnalyzer`。
2. 加上 Component：`More Mountains/Tools/Audio/MM Audio Analyzer`。
3. 在 `Source` 設定要分析的音訊來源：
   - `Global`：分析整個場景經過 `AudioListener` 的聲音。適合讓效果跟著整體場景聲音動。
   - `AudioSource`：只分析指定的 `AudioSource`。適合固定跟著某一首音樂、某一軌音效或單一音源反應。
   - `Microphone`：分析麥克風輸入。官方文件提到可用，但目前專案內的 `MMAudioAnalyzer.cs` 麥克風相關程式碼有 `UNCOMMENT_MICROPHONE` 註解標記，實作前需要先確認該版本是否已啟用麥克風讀取。
4. 若使用 `AudioSource`，把要分析的音源拖到 `TargetAudioSource`。
5. Sampling 參數建議先維持預設：
   - `SampleInterval = 0.02`：每 0.02 秒分析一次。越小越即時，但成本越高。
   - `SpectrumSamples = 1024`：頻譜取樣數。越高越細，但成本越高。
   - `Window = Rectangular`：FFT window 類型。沒有特殊需求先不改。
   - `NumberOfBands = 8`：把頻譜切成幾段。官方建議大多數用途用 1 到 8 段即可，段數越多越容易挑出指定頻率，但成本也越高。
   - `BufferSpeed = 2`：Buffered 值回落速度。數值越高，值越快貼近當前音量；越低，視覺反應越平滑。

---

## Runtime 觀察數值

進入 Play Mode 並播放聲音後，Inspector 會顯示幾組可用數值：

- `Amplitude`：當前整體音量。
- `NormalizedAmplitude`：整體音量正規化到 0 到 1。
- `BufferedAmplitude`：平滑後的整體音量，適合控制縮放、寬度、亮度等連續變化。
- `NormalizedBufferedAmplitude`：正規化且平滑後的整體音量，最常拿來直接驅動視覺參數。
- `BandLevels[index]`：某個頻段的即時音量。
- `NormalizedBandLevels[index]`：某個頻段的正規化音量。
- `BufferedBandLevels[index]`：某個頻段的平滑音量。
- `NormalizedBufferedBandLevels[index]`：某個頻段的正規化平滑音量。

如果只是要物件跟整首歌一起呼吸，優先使用 `NormalizedBufferedAmplitude`。如果要針對鼓、低頻、鈸、高頻或某個音色反應，使用 `NormalizedBufferedBandLevels[BandID]`，並在 Inspector 的 Levels / Visualization 觀察哪一段最接近目標聲音。

---

## 正規化流程

若要可靠使用 Normalized 值，官方文件建議先讓 Analyzer 掃描整首音檔找峰值：

1. 使用 `AudioSource` 模式，確認 `TargetAudioSource` 有指定音檔。
2. 進入 Play Mode，並讓音樂開始播放。
3. 在 `MMAudioAnalyzer` Inspector 底部按 `Find Peaks`。
4. Analyzer 會高速播放並掃描整首音檔峰值。
5. 掃描完成後退出 Play Mode。
6. 回到 Edit Mode 按 `Paste Peaks`。
7. 之後即可使用 `NormalizedAmplitude`、`NormalizedBufferedAmplitude` 與各種 normalized band 值。

注意：這個流程主要適用於固定音檔。`Global` 或即時輸入無法像固定 `AudioSource` 一樣預先掃完整首歌，因此 normalized 值可能比較依賴播放過程中累積到的峰值。

---

## Beats 設定

`Beats` 是一組可自訂的事件偵測器，每個 Beat 可以監聽某個頻段或整體音量，當數值穿越門檻時觸發事件。

建議流程：

1. 將 `NumberOfBands` 設為 `8`。
2. 進入 Play Mode，觀察 Raw / Normalized Visualization。
3. 找出目標聲音最明顯的頻段，例如低頻鼓通常在較低 Band，鈸或尖銳聲音可能在較高 Band。
4. 在 `Beats` 陣列新增一筆設定。
5. 設定：
   - `Name`：清楚命名，例如 `Kick`、`Snare`、`Crash`、`Flash`、`Pulse`。
   - `BandID`：要監聽的頻段 index。
   - `Mode`：一般建議先用 `BufferedNormalized` 或 `Normalized`。
   - `Threshold`：觸發門檻。從 `0.3` 到 `0.6` 試起，再依視覺化結果微調。
   - `MinimumTimeBetweenBeats`：兩次觸發的最短間隔，用來避免同一個聲音連續誤觸發。
   - `BeatValueMode`：`Remapped` 會在觸發後依 `RemappedAttack` / `RemappedDecay` 輸出 0 到 1 的包絡值；`Live` 則直接輸出目前讀到的值。
6. 將 `OnBeat` 綁定到要觸發的 UnityEvent，例如播放 `MMFeedbacks`、呼叫自訂腳本方法、切換物件、觸發 Animator、改變材質或啟動 VFX。

補充：官方文件描述 Beat 是當值往上超過 threshold 時觸發；目前專案內 `MMAudioAnalyzer.cs` 的實作在往上超過和往下跌破 threshold 時都會呼叫 `OnBeat`。如果只想在攻擊點觸發一次，請把 `MinimumTimeBetweenBeats` 設得比音符衰減時間長，或在自己的接收腳本中加額外冷卻。

---

## 用程式讀取數值

Feel 的 Brass demo 使用以下方式讓燈光強度跟著音樂動：

```csharp
using MoreMountains.Tools;
using UnityEngine;

public class AudioReactiveLight : MonoBehaviour
{
    public MMAudioAnalyzer analyzer;
    public Light targetLight;
    public float multiplier = 5f;

    private void Update()
    {
        if (analyzer == null || targetLight == null)
        {
            return;
        }

        targetLight.intensity = analyzer.NormalizedBufferedAmplitude * multiplier;
    }
}
```

讀取指定 Beat 或頻段：

```csharp
float beatValue = analyzer.Beats[0].CurrentValue;
float bandValue = analyzer.NormalizedBufferedBandLevels[2];
```

---

## 驅動特效或物件

### 做法 A：用 Beat 觸發一次性事件

適合鼓點、撞擊、閃光、爆發、切換狀態、播放 Feedback 或啟動一次性 VFX。

1. 在 `MMAudioAnalyzer` 的 `Beats` 中新增 Beat。
2. 調整 `BandID`、`Mode`、`Threshold`、`MinimumTimeBetweenBeats`。
3. 在該 Beat 的 `OnBeat` UnityEvent 綁定目標物件的方法。
4. 目標方法可以做任何事，例如：
   - 播放 `MMFeedbacks.PlayFeedbacks()`。
   - 呼叫自訂腳本的 `Play()`、`Pulse()`、`Trigger()`。
   - 切換 `GameObject.SetActive()`。
   - 觸發 `Animator.SetTrigger()`。
   - 啟動 `VisualEffect.Play()`。

### 做法 B：用音量連續控制 Float 參數

適合讓亮度、縮放、材質強度、VFX 生成率、Shader 數值或任何 float 參數跟著音樂起伏。

```csharp
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.VFX;

public class AudioReactiveVFXFloat : MonoBehaviour
{
    public MMAudioAnalyzer analyzer;
    public VisualEffect targetVisualEffect;
    public string parameterName = "Intensity";
    public int bandID = -1;
    public float multiplier = 10f;
    public float offset = 0f;
    public float lerpSpeed = 10f;

    private int _parameterId;
    private float _currentValue;

    private void Awake()
    {
        _parameterId = Shader.PropertyToID(parameterName);
    }

    private void Update()
    {
        if (analyzer == null || targetVisualEffect == null)
        {
            return;
        }

        float sourceValue = analyzer.NormalizedBufferedAmplitude;

        if (bandID >= 0 && analyzer.NormalizedBufferedBandLevels != null && bandID < analyzer.NormalizedBufferedBandLevels.Length)
        {
            sourceValue = analyzer.NormalizedBufferedBandLevels[bandID];
        }

        float targetValue = offset + sourceValue * multiplier;
        _currentValue = Mathf.Lerp(_currentValue, targetValue, Time.deltaTime * lerpSpeed);
        targetVisualEffect.SetFloat(_parameterId, _currentValue);
    }
}
```

使用方式：

1. 將上方腳本加到管理物件或特效物件。
2. `analyzer` 指向場景中的 `MMAudioAnalyzer`。
3. `targetVisualEffect` 指向要控制的 `VisualEffect`。
4. `parameterName` 填 VFX Graph 內公開參數名稱。名稱必須完全相同，包含空白與大小寫。
5. `bandID = -1` 代表使用整體音量；填 `0` 以上代表使用指定頻段。
6. 用 `multiplier` 和 `offset` 把 0 到 1 的音量值轉成目標參數需要的範圍。

### 做法 C：用音量控制 Transform

適合讓物件縮放、上下浮動、旋轉或震動。

```csharp
using MoreMountains.Tools;
using UnityEngine;

public class AudioReactiveScale : MonoBehaviour
{
    public MMAudioAnalyzer analyzer;
    public int bandID = -1;
    public float baseScale = 1f;
    public float scaleAmount = 1f;
    public float lerpSpeed = 10f;

    private Vector3 _initialScale;

    private void Awake()
    {
        _initialScale = transform.localScale;
    }

    private void Update()
    {
        if (analyzer == null)
        {
            return;
        }

        float sourceValue = analyzer.NormalizedBufferedAmplitude;

        if (bandID >= 0 && analyzer.NormalizedBufferedBandLevels != null && bandID < analyzer.NormalizedBufferedBandLevels.Length)
        {
            sourceValue = analyzer.NormalizedBufferedBandLevels[bandID];
        }

        float scale = baseScale + sourceValue * scaleAmount;
        Vector3 targetScale = _initialScale * scale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * lerpSpeed);
    }
}
```

---

## 調整建議

- 反應太碎：改用 `NormalizedBufferedAmplitude` 或降低 `BufferSpeed`。
- 反應太慢：提高 `BufferSpeed` 或降低額外腳本內的 smoothing。
- 觸發太頻繁：提高 `Threshold` 或增加 `MinimumTimeBetweenBeats`。
- 音量很小：先完成 `Find Peaks` / `Paste Peaks`，或提高 `multiplier`。
- 只想聽鼓點：降低 `BandID` 往低頻找，通常從 0、1、2 開始試。
- 只想聽高頻亮點：提高 `BandID` 往高頻找，並用 Visualization 確認目標聲音在哪一段最明顯。
- 效果參數沒反應：確認目標參數已公開，且名稱完全一致。
- 成本太高：降低 `NumberOfBands`、提高 `SampleInterval`、降低 `SpectrumSamples`。一般舞台或特效同步先用 `NumberOfBands = 8` 和預設 sampling 測試即可。

---

## 限制

- WebGL 不支援：官方文件指出 MMAudioAnalyzer 使用 Unity / FMOD 的 spectrum data API，而 Unity WebGL 不支援這類 API，因此 WebGL build 不能使用 MMAudioAnalyzer。
- `Find Peaks` 流程主要適合固定音檔；即時聲音、麥克風或整體場景輸出比較適合使用 raw / buffered 值，或在播放過程中觀察後手動調整倍率。
- 若同一場景有多個音訊反應物件，建議共用一個 `MMAudioAnalyzer`，不要每個物件各放一個 Analyzer。
