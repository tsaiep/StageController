# Storyboard RT Cross Fade

Camera Profile Timeline 可以用 RenderTexture 與 CinemachineStoryboard，在兩個 clip 的畫面之間進行 dissolve。Play Mode 與 Edit Mode Timeline 預覽共用相同的畫面組件，但使用不同的交接流程。

## 運作方式

- `Parameter Blend`：沿用原本的 profile 參數混合。
- `Storyboard Render Texture Cross Fade`：
  - outgoing clip 繼續驅動主 CinemachineCamera。
  - incoming clip 驅動獨立的 CrossFade CinemachineCamera，畫面輸出到 RenderTexture。
  - 專用 muted Storyboard camera 將 RenderTexture 疊在主畫面上。
  - Timeline 的 incoming weight 控制 Main Brain camera override 的混合權重。
- `Cross Fade Blur`：
  - 沿用相同 RenderTexture、Storyboard alpha 與 runtime handoff。
  - 主輸出與離屏 Unity Camera 各自套用相同 Kawase blur。
  - 模糊權重由原始 overlap alpha 計算為 `sin(π × alpha)`，因此兩端清楚、中點最模糊。
  - incoming Clip 可獨立設定 `Blur Max Intensity`（`0～5`）與 `Alpha Timing`（`0～1`）。
  - `Alpha Timing` 只壓縮 Storyboard alpha 的混合區間，不改變模糊曲線。

Play Mode 在 overlap 結束後會把已經完整評估 Clip2 的 transition camera 升格為主輸出，保留 Composer、Follow 或 Spline Dolly 的內部 damping 狀態。

Edit Mode 預覽不交換 camera 引用。每次 Timeline 評估都直接更新兩套 camera 並立即渲染 RT；離開 overlap 時會清除 Storyboard override，讓主 Brain 直接顯示當前單一 clip。

## 一鍵設置

選取 `CameraSystemMaster`，在 Inspector 上方按「建立 / 修復全部」。工具會：

- 建立或補齊帶有 `Camera`、`CinemachineBrain`、MainCamera tag 與 URP Additional Camera Data 的主輸出 Camera。
- 建立 General A/B、Tracking、Dolly 主運鏡 cameras。
- 建立離屏 Render Camera、Render Brain 和三台 transition cameras。
- 建立只承載 `CinemachineStoryboard` 的 muted Storyboard camera。
- 在 Main Camera 與 CrossFade Render Camera 建立 `CameraBlurState`。
- 設定 Default 與 crossfade output channel，避免兩個 Brain 選到錯誤 camera。
- 將 Render Brain 的 Default Blend 設為 Cut / 0。

完成後可按「檢查設定」查看詳細問題。Inspector 上方也會顯示目前錯誤與警告數量。

若 `Render Texture (Optional)` 留空，系統會依 Game View 尺寸、目前 URP HDR Color Buffer Precision 與平台支援的 MSAA 動態建立 Linear HDR RenderTexture，並在 crossfade 結束後釋放。PC 64-bit HDR precision 優先使用 `R16G16B16A16_SFloat`；32-bit HDR precision 優先使用 `B10G11R11_UFloatPack32`，不支援時再使用平台 HDR fallback。

不可使用 `RenderTextureFormat.Default`、ARGB32 或其他 LDR target。URP 會把 Camera 的外部 target texture format 當作內部 color buffer format；LDR RT 會在 Bloom 與 Tonemapping 前截斷大於 1 的 HDR 亮度，造成 Bloom 消失、ACES/Neutral 亮部偏暗以及 runtime handoff 色跳。若指定的 RT 不是目前平台支援的 Linear HDR 2D 格式，runtime 會警告並改用自動建立的 HDR RT。

專案提供 `Assets/_CameraControl/RenderTextures/RT_StoryboardCrossFade_HDR_Reference.renderTexture` 作為 PC 固定解析度參照：1920×1080、`R16G16B16A16_SFloat`、Linear、Depth 24/Stencil 8、8x MSAA、Bilinear、Clamp、無 mipmap。正式使用仍建議讓欄位留空，以便系統依 Game View、Quality Level 與硬體能力自動配置。

## Timeline 設定與預覽

1. 讓兩個 Camera Profile clips 在同一條 Camera Profile track 上交疊。
2. 選取後開始的 incoming clip。
3. 將 `Blend Mode` 設為 `Storyboard Render Texture Cross Fade` 或 `Cross Fade Blur`。
4. 若使用 `Cross Fade Blur`，依需要調整該 Clip 的 `Blur Max Intensity` 與 `Alpha Timing`。
5. 在 Edit Mode 開啟 Timeline Preview，於 Game View 拖曳或播放時間軸即可預覽。
6. 進入 Play Mode 確認 runtime handoff。

Edit Mode 停住、單格跳轉或反向拖曳時，RT 會按目前 Timeline 時間重新求值。非連續跳轉會清除前一個 camera damping 狀態，避免預覽結果依賴先前停留的位置。

## 手動設置參考

```text
Main Camera
  Camera
  CinemachineBrain
    Channel Mask = Default

CrossFade Render Camera
  Camera
  CinemachineBrain
    Channel Mask = Channel01
    Default Blend = Cut / 0

CrossFade Storyboard Camera
  CinemachineCamera
    Output Channel = Default
  CinemachineStoryboard
    Mute Camera = true
    Alpha = 1
    Show Image = false（非轉場期間）

CameraSystemMaster
  Cross Fade Render Camera = CrossFade Render Camera
  Cross Fade Render Brain = CrossFade Render Camera 的 CinemachineBrain
  Cross Fade Output Channel = Channel01
  Cross Fade General Camera = transition General camera
  Cross Fade Tracking Camera = transition Tracking camera
  Cross Fade Dolly Camera = transition Dolly camera
  Cross Fade Storyboard Camera = CrossFade Storyboard Camera
  Render Texture = 留空（推薦，自動建立 HDR RT）
    或 RT_StoryboardCrossFade_HDR_Reference（PC 1920×1080 對照用）

Incoming Cross Fade Blur Clip
  Blur Max Intensity = 1
  Alpha Timing = 0

Main Camera / CrossFade Render Camera
  CameraBlurState

PC Renderer / Mobile Renderer
  CameraBlurRendererFeature
```

Storyboard camera 必須是獨立物件，不可和任何主運鏡或 transition camera 共用。

## 限制

- Editor 預覽顯示於 Game View，不另外繪製 Scene View overlay。
- 僅支援正好兩個重疊 clips。
- 必須由後開始的 incoming clip 選擇 Cross Fade mode。
- 這是 incoming 畫面覆蓋 outgoing 畫面的 dissolve，不是雙 RenderTexture compositor。
- Camera Blur 只處理 Camera color；排序在 Storyboard 上方的 Screen Space Overlay UI 維持清楚。
- Crossfade rig 不完整時會記錄錯誤並在 clip 邊界 hard cut，不會退回 profile 參數混合。
- 只有 Blur state／Feature 缺失時會退化為無模糊的普通 Cross Fade，不會因而 hard cut。
