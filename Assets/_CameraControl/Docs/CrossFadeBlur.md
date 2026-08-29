# Cross Fade Blur 運作說明

`Cross Fade Blur` 是 Camera Profile Timeline 的一種 Blend Mode。它沿用 Storyboard RenderTexture Cross Fade，同時讓主畫面與離屏 RenderTexture 經歷：

```text
清楚 → 模糊 → 清楚
```

本文聚焦說明 Blur 在 `CameraProfileTrack.cs` 中如何被判定與計算。完整的 RenderTexture 相機交接架構請參考 [Storyboard RT Cross Fade](./StoryboardRTCrossFade.md)。

## 啟動條件

Blur 只會在下列條件同時成立時啟用：

1. Timeline 上正好有兩個有效的 Camera Profile Clip overlap。
2. 較晚開始的 incoming Clip，其 `Blend Mode` 設為 `Cross Fade Blur`。

如果 incoming Clip 使用 `Storyboard Render Texture Cross Fade`，系統仍會進行 RT 淡入，但不會套用 Blur。

## 核心計算

### 1. 原始 Cross Fade 進度

系統先以兩個 Clip 的 Timeline 權重計算 overlap 進度：

```text
rawAlpha = incomingWeight / (outgoingWeight + incomingWeight)
```

`rawAlpha` 會由 `0` 前進到 `1`：

- `0`：剛進入 overlap。
- `0.5`：overlap 中點。
- `1`：即將離開 overlap。

### 2. Blur 權重

Blur 使用正弦曲線：

```text
blurWeight = sin(π × clamp01(rawAlpha))
```

因此 Blur 會自然形成以下過程：

| Overlap 位置 | `rawAlpha` | `blurWeight` | 畫面 |
| --- | ---: | ---: | --- |
| 起點 | `0` | `0` | 清楚 |
| 中點 | `0.5` | `1` | 最大模糊 |
| 終點 | `1` | `0` | 恢復清楚 |

### 3. Blur 強度與畫面合成

incoming Clip 的 `Blur Max Intensity` 決定中點的最大模糊程度，範圍是 `0–5`：

```text
kernelIntensity = blurWeight × Blur Max Intensity
compositeWeight = blurWeight
```

兩個數值用途不同：

- `kernelIntensity` 控制 Kawase Blur 的取樣半徑。
- `compositeWeight` 控制原始清楚畫面與模糊結果的混合比例。

Renderer 最後會進行：

```text
finalColor = lerp(originalColor, blurredColor, compositeWeight)
```

這可確保 Blur 起點與終點完全等於原圖。即使降採樣或 Mipmap 本身帶有最低程度的柔化，也不會在 Blur 剛啟用時直接跳到該模糊程度。

## Alpha Timing 與 Blur 的關係

`Alpha Timing` 只調整 incoming RenderTexture 的淡入時間，不會改變 `rawAlpha` 或 `blurWeight`。

```text
Blur：始終依 rawAlpha 走完整的 清楚 → 模糊 → 清楚
RT Alpha：依 Alpha Timing 延後開始淡入，並提早達到不透明
```

因此提高 `Alpha Timing` 時，Blur 中點與最大強度不會移動。當 `Alpha Timing = 1`，RT Alpha 會在 overlap 中點切換，但 Blur 仍維持連續的正弦曲線。

## 資料流

```text
Timeline outgoing / incoming weights
        ↓
CameraProfileTrack
  rawAlpha
  blurWeight
  kernelIntensity
        ↓
CameraSystemMaster
        ↓
Main CameraBlurState + CrossFade Render CameraBlurState
        ↓
CameraBlurRendererFeature
  Kawase Blur
  original / blurred composite
        ↓
Storyboard RenderTexture Cross Fade
```

主輸出 Camera 與 CrossFade Render Camera 會收到相同的 Blur 強度與合成權重，所以 RT 淡入期間兩張畫面的模糊程度一致。

## 結束、切換與降級行為

- overlap 結束並進入 runtime handoff 時，系統會立即清除兩台 Camera 的 Blur state。
- 切換到非 `Cross Fade Blur` 模式時，也會清除 Blur，避免上一個轉場殘留。
- Timeline gap、seek 中斷或 Playable 銷毀時，Storyboard Cross Fade 的清理流程會一併清除 Blur。
- 如果 `CameraBlurState` 或 Blur Renderer Feature 設定不完整，Blur 會停用，但 RT Cross Fade 仍可繼續。
- 如果整套 Storyboard RT Cross Fade rig 無法啟動，該轉場會使用既有 hard-cut fallback。

## 調整建議

- `Blur Max Intensity = 0`：不產生 Blur。
- `Blur Max Intensity = 1`：適合作為一般轉場的起點。
- `Blur Max Intensity = 3–5`：適合需要明顯遮蔽相機切換的強模糊轉場。
- 調整 `Alpha Timing` 時，只需觀察 RT 淡入節奏；Blur 曲線不會受影響。

