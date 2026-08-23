# Unified Universal Blur 整合規範

## 目標

Camera Profile Timeline 新增 `Cross Fade Blur`。它沿用既有 Storyboard RenderTexture Cross Fade，在相同 alpha dissolve 期間讓相機畫面經歷：

```text
清楚 → 模糊 → 清楚
```

正式成品不得依賴 `com.unify.unified-universal-blur`。專案只保留由該套件衍生、已改成 Camera Profile 架構的 Kawase rendering 核心與 MIT 授權聲明。

---

## 參考實作檢查結論

### 可以重用

- Kawase blur shader 與多次 iteration。
- 兩張低解析度暫存貼圖的 ping-pong 流程。
- Downsample、mipmap、scale、offset 計算。
- Unity 6 RenderGraph unsafe pass 與 compatibility rendering path。
- `MaterialPropertyBlock`，避免改寫共享 Material 的參數。

### 不可直接沿用

- `_GlobalUniversalBlurTexture`：同幀多 Camera 時會被後渲染的 Camera 覆寫。
- Renderer Feature 上共享的 `_intensity`：不是 per-camera state。
- 只產生供 UI shader 取樣的低解析度 global texture：本需求必須將模糊結果輸出成該 Camera 的最終 color buffer。
- 原套件的 UI Material、Tinted UI shader 與 UI workflow。

原 Storyboard 使用 `ScreenSpaceOverlay`，因此 Main Camera 的 Renderer Feature 看不到已合成的 Storyboard overlay。不能採用「先 Cross Fade、再模糊最終畫面」；必須讓主輸出與離屏輸出各自模糊後再由 Storyboard 合成。

---

## 最終架構

```text
Timeline incoming/outgoing weight
        ↓
rawAlpha
        ↓
blurWeight = sin(π × rawAlpha)
        ↓
incoming Clip × crossFadeBlurMaxIntensity
        ↓
Main Camera CameraBlurState
CrossFade Render Camera CameraBlurState
        ↓
CameraBlurRendererFeature（per-camera）
        ↓
Kawase downsample / ping-pong / full-resolution upsample
        ↓
兩張已模糊畫面進行原本的 Storyboard alpha Cross Fade
```

兩台 Camera 必須使用相同 blur weight 與 kernel。Kawase blur 是線性運算，因此分別模糊後再 alpha 合成，視覺上等價於模糊合成後的整體 Camera 畫面，同時不會受到 Screen Space Overlay 的渲染順序限制。

`Cross Fade Blur` 只擴充 rendering capability，不改變：

- Cross Fade pair 判定。
- Incoming clip 控制模式的規則。
- RenderTexture 與 CinemachineStoryboard 架構；Cross Fade Blur 可選擇只壓縮顯示 alpha 的混合區間。
- Runtime transition camera promotion／handoff。
- Editor Timeline 手動求值與預覽。
- Cross Fade rig 失敗時的 hard-cut 規則。

---

## Blend 與狀態規則

`CameraProfileBlendMode` 的序列化數值固定為：

```text
ParameterBlend = 0
StoryboardRenderTextureCrossFade = 1
CrossFadeBlur = 2
```

這可保留現有 Timeline clip 的值，不需要 migration。

Cross Fade Blur 只在恰好兩個有效 clip overlap、且後開始的 incoming clip 選擇此模式時啟動。incoming Clip 保存 `crossFadeBlurMaxIntensity`（`0～5`，預設 `1`）與 `crossFadeAlphaTiming`（`0～1`，預設 `0`）：

```text
rawAlpha = incomingWeight / (outgoingWeight + incomingWeight)
blurWeight = sin(π × clamp01(rawAlpha))
intensity = blurWeight × incomingClip.crossFadeBlurMaxIntensity

hold = incomingClip.crossFadeAlphaTiming × 0.5
displayAlpha = clamp01((rawAlpha - hold) / (1 - 2 × hold))
```

因此 rawAlpha 為 `0 / 0.5 / 1` 時，模糊強度永遠為 `0 / 最大 / 0`，不受 `displayAlpha` 影響。`crossFadeAlphaTiming = 1` 時另行處理為中點瞬切，避免除以零。

Blur state 必須在以下情況歸零：

- overlap 結束並進入 runtime handoff。
- 切回普通 Cross Fade 或 Parameter Blend。
- Timeline gap、seek 中斷、Playable 銷毀。
- `CameraSystemMaster` 停用或清除 Storyboard Cross Fade。
- Cross Fade rig 啟動失敗並進入 fallback。

缺少 `CameraBlurState` 時只記錄一次警告並退化為普通 Cross Fade，不因 blur 設定錯誤破壞原本 dissolve。

---

## CameraBlurRendererFeature

Feature 只在以下條件成立時 enqueue pass：

- `CameraType.Game`。
- Camera 上有啟用中的 `CameraBlurState`。
- state intensity 大於零。

每台 Camera 各自建立當幀 RenderGraph texture handles；禁止使用 global texture、global weight 或跨 Camera render target。流程為：

1. 在 `AfterRenderingPostProcessing` 讀取該 Camera 的 active color。
2. 以 camera descriptor 建立低解析度 ping／pong textures。
3. 使用 Kawase shader 進行指定 iterations。
4. 上採樣至與原 active color 相同的 full-resolution output。
5. 將 `UniversalResourceData.cameraColor` 指向 output。

RenderGraph 關閉時使用 RTHandle compatibility path，完成 blur 後寫回原 camera color target。HDR／SDR graphics format、texture dimension、volume depth 與 VR usage 來自該 Camera descriptor，不使用 `Screen.width` 作為 render target 尺寸來源。

初始 PC／Mobile 設定一致：

```text
Iterations = 4
Downsample = 2
Mip Maps = true
Scale = 1
Offset = 1
Scale Mode = Screen Height
Reference Size = 1080
Injection Point = After Rendering Post Processing
```

---

## 專案設定與所有權

- Main Camera 與 CrossFade Render Camera 各掛一個 `CameraBlurState`。
- `CameraSystemMaster` 只保存並驅動兩個 state reference；最大強度由 incoming Clip 提供。
- Camera setup 工具會建立／補齊兩個 state；設定檢查會驗證 state 與所有 Quality Level 使用的 URP Renderer。
- `PC_Renderer` 與 `Mobile_Renderer` 都必須包含 `CameraBlurRendererFeature`。
- Screen Space Overlay UI 不屬於 Camera color buffer；排序在 Storyboard 上方的 UI 會保持清楚。

自有程式與 shader 放在 `Assets/_CameraControl`，namespace 與 shader path 均不得引用 `Unified.UniversalBlur` 或 `Packages/com.unify.unified-universal-blur`。衍生檔案保留來源註記，完整授權位於：

```text
Assets/_CameraControl/ThirdParty/UnifiedUniversalBlur-LICENSE.txt
```

`Packages/manifest.json`、`Packages/packages-lock.json` 與 `Packages/` 下不得保留原套件依賴。

---

## 驗收

- `Parameter Blend` 與原 `Storyboard Render Texture Cross Fade` 行為不變。
- Cross Fade Blur 的兩端清楚，中點兩張畫面同時達最大模糊。
- 不同 incoming Clip 可使用不同的 `0～5` 最大模糊強度。
- `Alpha Timing = 0 / 0.5 / 1` 分別呈現原線性淡入、中央半段淡入與中點瞬切，且三者的 Blur 曲線一致。
- General、Tracking、Dolly 的同類與跨類型 Cross Fade Blur 均可使用。
- Edit Mode 播放、拖曳、反向 seek 與 Play Mode handoff 不閃爍、不殘留 blur。
- PC／Mobile Renderer 都能處理 Main Camera 與離屏 RenderTexture Camera。
- 缺少 blur state 或 shader 時仍保留普通 Cross Fade。
- C#、shader 編譯成功，且正式程式與資產不存在原 Unified Universal Blur 套件引用。
