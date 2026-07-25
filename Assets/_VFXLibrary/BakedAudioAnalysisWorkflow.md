# MMAudioAnalyzer to Baked Audio Analysis Workflow

目標：保留 More Mountains FEEL 的 `MMAudioAnalyzer` 作為即時預覽與調參工具，但最終 offline render 時改用已 bake 的資料，並讓場景上的接收腳本盡量不用重新接線。

---

## 結論

不需要修改 FEEL 插件原始碼。

推薦做法是新增專案自己的 baked audio 系統，讓自製接收腳本可以在兩種資料來源之間切換：

- `MMAudioAnalyzer`：即時 preview、調 band、調 beat threshold。
- `BakedAudioAnalysis`：Timeline / Recorder / offline render 使用。

如果場景裡使用的是自製的 `AudioAnalyzerVFXController`，只要在這個腳本加 source mode，就可以保留原本的 VFX parameter binding、multiplier、offset、lerp 設定。

如果場景裡直接使用 FEEL 的 `FloatController` 或 `ShaderController` 的 `AudioAnalyzer` 模式，這些元件本身只認得 `MMAudioAnalyzer`，不能直接讀 baked data。這種情況不建議改 FEEL source，建議複製或重做一份自己的接收腳本。

---

## FEEL 原本負責的事情

`MMAudioAnalyzer` 會在 Play Mode 中即時分析音訊，輸出：

- `NormalizedBufferedAmplitude`
- `NormalizedBufferedBandLevels[index]`
- `Beats[index].CurrentValue`
- `Beats[index].OnBeat`

這些資料可以接：

- VFX Graph float / int / bool / vector / color
- Light intensity
- Material / shader property
- Transform scale / position / rotation
- Animator parameter
- Particle emission
- MMFeedbacks
- 自製事件接收器

FEEL 的 `Find Peaks` / `Paste Peaks` 只會預先取得 normalization peak，不會 bake 整首歌每個時間點的 value。

---

## 新增系統規劃

### 1. BakedAudioAnalysis

新增一個 `ScriptableObject` asset，用來存整首音樂的分析資料。

建議欄位：

```csharp
public AudioClip sourceClip;
public float sampleInterval;
public int numberOfBands;
public AnimationCurve normalizedBufferedAmplitude;
public AnimationCurve[] normalizedBufferedBandLevels;
public AnimationCurve[] beatValues;
public float[][] beatTimes;
```

用途：

- 儲存從 `MMAudioAnalyzer` 錄下來的結果。
- Offline render 時用 Timeline time 取樣。
- 同一首歌可以重複使用同一份 baked asset。

### 2. FeelAudioAnalysisRecorder

新增一個場景用 recorder 腳本。

功能：

- 指向場景中的 `MMAudioAnalyzer`。
- 播放 Timeline 或 AudioSource 時，每隔固定時間記錄一次 FEEL 輸出的值。
- 把資料寫入 `BakedAudioAnalysis`。
- 可選擇記錄：
  - amplitude
  - band levels
  - beat current values
  - beat trigger times

這個腳本只負責 bake，不負責最終 render。

### 3. BakedAudioAnalysisSampler

新增一個 runtime sampler。

功能：

- 指向 `BakedAudioAnalysis`。
- 指向 `PlayableDirector`。
- 透過 `PlayableDirector.time` 取得目前 Timeline 時間。
- 對外提供類似 FEEL 的讀取介面。

建議 API：

```csharp
public float GetNormalizedBufferedAmplitude();
public float GetNormalizedBufferedBandLevel(int bandID);
public float GetBeatValue(int beatID);
public bool WasBeatTriggered(int beatID, float previousTime, float currentTime);
```

---

## AudioAnalyzerVFXController 的推薦改法

目前 `AudioAnalyzerVFXController` 已經集中管理：

- VFX target list
- parameter name
- parameter type
- multiplier
- offset
- lerp
- bool threshold
- color ramp

這些設定應該保留。

只建議新增資料來源模式：

```csharp
public enum AudioAnalysisInputMode
{
    MMAudioAnalyzer,
    BakedTimeline
}
```

新增欄位：

```csharp
public AudioAnalysisInputMode inputMode;
public MMAudioAnalyzer audioAnalyzer;
public BakedAudioAnalysisSampler bakedSampler;
```

然後把 `TryGetSourceValue()` 改成：

```csharp
switch (inputMode)
{
    case AudioAnalysisInputMode.MMAudioAnalyzer:
        // 保留原本讀 MMAudioAnalyzer 的邏輯
        break;

    case AudioAnalysisInputMode.BakedTimeline:
        // 改讀 bakedSampler
        break;
}
```

這樣場景上的 controller 只要切換 `inputMode`，不需要重設 VFX targets。

---

## FloatController / ShaderController 要不要改

不建議直接修改 FEEL 附的：

- `FloatController.cs`
- `ShaderController.cs`

原因：

- 它們是插件 source，更新 FEEL 時容易被覆蓋。
- 它們的 `AudioAnalyzer` 模式直接綁死 `MMAudioAnalyzer`。
- 要支援 baked data 會牽涉 inspector、serialized fields、runtime logic，修改範圍會擴大。

如果只是少量使用，可以改場景接法，換成自製 controller。

如果大量使用，推薦複製概念做自己的版本，例如：

- `StageAudioFloatController`
- `StageAudioShaderController`
- `StageAudioVFXController`

這些自製 controller 統一讀一個 interface：

```csharp
public interface IAudioAnalysisSource
{
    float GetBeatValue(int beatID);
    float GetNormalizedBufferedBandLevel(int bandID);
    float GetNormalizedBufferedAmplitude();
}
```

再做兩個 source implementation：

- `FeelAudioAnalysisSource`：包住 `MMAudioAnalyzer`。
- `BakedAudioAnalysisSource`：包住 `BakedAudioAnalysisSampler`。

如此接收端只認 `IAudioAnalysisSource`，不需要知道資料來自 FEEL 還是 baked asset。

---

## 操作流程

### A. 即時調參

1. 場景中放 `MMAudioAnalyzer`。
2. 使用 `AudioSource` 模式並指定音樂。
3. 設定 `NumberOfBands`、`BufferSpeed`、`Beats`、`Threshold`。
4. `AudioAnalyzerVFXController` 設為 `MMAudioAnalyzer` 模式。
5. 播放 Timeline，調整 VFX 反應。

### B. Bake 資料

1. 場景中加入 `FeelAudioAnalysisRecorder`。
2. 指定同一個 `MMAudioAnalyzer`。
3. 指定或建立 `BakedAudioAnalysis.asset`。
4. 播放 Timeline 或音樂一次。
5. Recorder 把 FEEL 的輸出寫成 curves / beat times。

### C. Offline Render

1. 場景中加入 `BakedAudioAnalysisSampler`。
2. 指定 `BakedAudioAnalysis.asset`。
3. 指定 Timeline 的 `PlayableDirector`。
4. 將 `AudioAnalyzerVFXController` 切成 `BakedTimeline` 模式。
5. 使用 Unity Recorder render。

---

## Source Manager Editor Window

建議新增一個 Editor Window：`Audio Analysis Source Manager`。

功能：

- 搜尋場景中所有 `AudioAnalyzerVFXController`。
- 顯示目前 source mode。
- 顯示 `MMAudioAnalyzer` 是否有指定。
- 顯示 `BakedAudioAnalysisSampler` 是否有指定。
- 顯示每個 controller 使用的 `valueSource`、`beatID`、`normalizedLevelID`。
- 批次切換 selected controllers：
  - `MMAudioAnalyzer`
  - `BakedTimeline`
- 批次指定：
  - `MMAudioAnalyzer`
  - `BakedAudioAnalysisSampler`
  - `PlayableDirector`
- 檢查 VFX parameter 是否存在。

這個工具可以讓場景從 preview 切到 offline render 時，不需要逐一點開每個 GameObject。

---

## 推薦實作順序

1. 先改 `AudioAnalyzerVFXController`，加入 `MMAudioAnalyzer` / `BakedTimeline` source mode。
2. 新增 `BakedAudioAnalysis` asset 格式。
3. 新增 `BakedAudioAnalysisSampler`。
4. 新增 `FeelAudioAnalysisRecorder`，先用 FEEL runtime output 錄資料。
5. 新增 `Audio Analysis Source Manager` 批次管理工具。
6. 最後再視需求做 `StageAudioFloatController` / `StageAudioShaderController`，取代 FEEL 的 `FloatController` / `ShaderController`。

---

## 建議原則

- 不改 FEEL source。
- FEEL 用來 preview。
- Baked data 用來 final render。
- 接收端盡量只切 source，不重設 target binding。
- 新功能放在 `Assets/_FeelCue/Script` 或專案自己的資料夾。
- FEEL 內建 controller 若需要 baked data，複製成自製版本，不直接修改插件檔案。
