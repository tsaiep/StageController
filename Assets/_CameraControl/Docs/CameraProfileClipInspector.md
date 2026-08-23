# Camera Profile Clip Inspector 調整指南

這份文件說明 Camera Profile Timeline Clip 在 Inspector 中可調整的欄位，以及這些欄位在目前 `CameraProfileTrack` Mixer 中的實際套用規則。

> 本文只涵蓋 **Clip Inspector**。`General Cut Prewarm`、`General Cut Zero Damping Frames`、`Keep Last Camera During Gap` 是 Track Inspector 設定，不在本文範圍內。

## 建議操作流程

1. 先設定 `Camera Profile` 與 `Tracking Target`。
2. Dolly Profile 再指定 `Spline Container`。
3. 在 Timeline 分別停在 Clip 的開頭、中間、結尾，確認 Profile 原始運鏡。
4. 再決定是否使用反向播放、固定速度或鏡像。
5. 最後才用各種 Bias 做單一 Clip 的微調。
6. Clip 有 overlap 時，確認使用的 Blend Mode 與 Profile 類型是否相容。

Bias 的 `0` 代表完全沿用 Profile。建議先用小幅度調整：FOV 每次約 `2～5` 度、位置每次約 `0.05～0.5` Unity unit、Normalized Spline Position 每次約 `0.02～0.1`。實際幅度仍需依角色尺寸、場景比例與鏡頭距離調整。

## 共通欄位

### Camera Profile

指定這個 Clip 使用的 `CameraProfileSO`。目前支援三種：

- `GeneralProfileSO`：以 Position Composer 配置一般構圖。
- `TrackingProfileSO`：以 Cinemachine Follow 追蹤目標。
- `DollyProfileSO`：沿 Spline 移動相機。

點擊 Profile 欄位會開啟選擇器：

- 可用名稱、類型或 Tag 搜尋。
- 選取多個 Tag 時採 **AND／交集**，Profile 必須同時具有全部 Tag。
- 第一次點 Profile 會更新預覽；再點同一個 Profile，或按「選擇此 Profile」，才會套用。
- `Ping` 會在 Project 視窗定位 Profile；`X` 會清除目前選擇。

Profile 決定基礎曲線與阻尼；Clip Inspector 的 Bias 只影響目前 Clip，不會修改原始 Profile 資產。

### Tracking Target

指定場景中的追蹤／注視目標。Mixer 會把它同時指定給目前 Cinemachine Camera 的 `Follow` 與 `LookAt`。

- General：決定構圖與跟隨的目標。
- Tracking：決定相機跟隨與注視的目標。
- Dolly：相機位置由 Spline 決定，但仍使用此目標作為 Follow／LookAt。
- 未指定時，`Follow` 與 `LookAt` 會被設為 `null`；畫面通常不會得到預期構圖。

### Blend Mode

| 選項 | 用途 | 規則與限制 |
| --- | --- | --- |
| `Parameter Blend` | 直接依 Timeline overlap 權重混合 Profile 參數 | 完整參數混合只支援 `General ↔ General` 或 `Tracking ↔ Tracking`。Profile 參數、阻尼及 Target 的位置／旋轉都會依權重混合。 |
| `Storyboard Render Texture Cross Fade` | 將 incoming 畫面以 RenderTexture 疊圖淡入 | 設定在 **後開始的 incoming Clip**；只有恰好兩個有效 Clip overlap 時才會啟動，且必須先完成 Cross Fade Camera Rig 設定。 |
| `Cross Fade Blur` | 沿用 Storyboard RT Cross Fade，並讓 Camera 畫面清楚→模糊→清楚 | 規則與一般 Cross Fade 相同；模糊在 overlap 中點達最大值，強度與 Alpha Timing 由 incoming Clip 獨立設定。 |

Parameter Blend 的重要限制：

- `Dolly ↔ Dolly`、不同 Profile 類型之間，以及超過兩個 Clip 的組合，都不會進行完整的 Profile 參數混合。
- 不支援參數混合時，Mixer 會採用當下 Timeline 權重最高的 Clip；權重交會時可能看起來像一次切鏡。
- 若需要不同類型間平順淡入，應在 incoming Clip 選擇 `Storyboard Render Texture Cross Fade` 或 `Cross Fade Blur`。
- Storyboard Cross Fade 設備缺失時，overlap 期間會保留 outgoing Camera，交接結果可能退化成切鏡。

完整 Rig 設定請見 [Storyboard RT Cross Fade](./StoryboardRTCrossFade.md)。

## Playback Options

### Reverse Playback

反轉 Profile 曲線的取樣方向。

```text
反轉前：sampleTime = t
反轉後：sampleTime = 1 - t
```

它會反轉整個 Profile 的動畫進程，包括 FOV、位置、Spline Position 與 Rotation Target Offset 曲線；不是把 Timeline Clip 本身倒放，也不會交換 Clip 的起訖位置。

### Use Fixed Playback Speed / Playback Speed

未啟用固定速度時，Profile 的 `0～1` 會映射到整段 Clip：

```text
sampleTime = Clip 內時間 / Clip 長度
```

因此拉長 Clip 會讓運鏡變慢，縮短 Clip 會讓運鏡變快。

啟用固定速度時：

```text
sampleTime = Clamp01(Clip 內秒數 × Playback Speed)
Profile 完整播放秒數 = 1 / Playback Speed
```

| Playback Speed | 結果 |
| ---: | --- |
| `0.5` | Profile 約 2 秒播完 |
| `1` | Profile 約 1 秒播完 |
| `2` | Profile 約 0.5 秒播完 |

規則：

- Inspector 最小值為 `0.001`。
- Clip 比完整播放時間長時，超出的部分會 Hold 在 Profile 終點。
- Clip 比完整播放時間短時，只會播到被裁掉前的進度。
- 同時啟用 Reverse 時，固定速度取樣完成後才套用 `1 - t`，所以 Hold 的會是反轉後的終點。

### Dynamic Mirror X / Y / Z

Mirror 會在執行時翻轉指定軸，不會修改 Profile 資產。向量的運算順序是：

```text
結果 = Mirror(Profile 曲線取樣值 + Clip Bias)
```

| 軸 | 會翻轉的內容 | 常見用途 |
| --- | --- | --- |
| `X` | Position／Follow／Rotation Target Offset X，以及 Profile 的 Screen Position X | 左右對稱重用同一個運鏡 |
| `Y` | Position／Follow／Rotation Target Offset Y，以及 Profile 的 Screen Position Y | 上下反轉構圖或偏移 |
| `Z` | Position／Follow／Rotation Target Offset Z | 前後方向反轉 |

Mirror **不會**翻轉 FOV、General Position Distance 或 Dolly Spline Position。因為 Bias 會一起被翻轉，例如 X 曲線值為 `1`、X Bias 為 `0.5` 且 Mirror X 開啟，最後結果是 `-1.5`，不是 `-0.5`。

## General Profile Bias

選到 `GeneralProfileSO` 時顯示。

| 欄位 | 實際運算 | 正值／負值的影響 | 建議調法 |
| --- | --- | --- | --- |
| `FOV Bias` | `fovCurve(t) + Bias`，最後 Clamp 到 `10～120` | 正值視角更廣、主體更小；負值視角更窄、主體更大 | 先以 `2～5` 度微調；若已碰到 10 或 120，繼續調整不會改變結果 |
| `Pos Distance Bias` | `posDistanceCurve(t) + Bias` | 正值增加 Camera Distance；負值減少 | 用來整體拉遠／推近，不改 Profile 曲線形狀；建議先以 `0.1～0.5` 調整 |
| `Pos Target Offset X/Y/Z Bias` | 各軸曲線取樣值加 Bias，再套用 Mirror | 改變 Position Composer 使用的目標位置 | 用來移動「相機構圖所跟隨的位置」，不是直接平移 Camera |
| `Rot Target Offset X/Y/Z Bias` | 各軸曲線取樣值加 Bias，再套用 Mirror | 改變 Rotation Composer 注視的位置 | 用來修正視線落點；主體位置正確但相機看錯位置時優先調整 |

Position／Rotation Target Offset 使用 Target 的 local space：

- `+X / -X`：目標的右／左。
- `+Y / -Y`：目標的上／下。
- `+Z / -Z`：目標的前／後。

Target 本身有旋轉時，這些方向會跟著 Target 旋轉。若只是要改鏡頭遠近，優先調 `Pos Distance Bias`；若只是要改相機看角色頭部或身體的位置，優先調 Y 軸 Target Offset。

## Tracking Profile Bias

選到 `TrackingProfileSO` 時顯示。

| 欄位 | 實際運算 | 用途與調整方式 |
| --- | --- | --- |
| `FOV Bias` | `fovCurve(t) + Bias`，最後 Clamp 到 `10～120` | 正值變廣角、負值變望遠；建議每次調 `2～5` 度 |
| `Follow Offset X/Y/Z Bias` | 各軸曲線取樣值加 Bias，再套用 Mirror | 改變 Camera 相對 Target 的跟隨位置；建議每次由 `0.05～0.2` 開始調 |
| `Rot Target Offset X/Y/Z Bias` | 各軸曲線取樣值加 Bias，再套用 Mirror | 改變注視點，不直接改變 Camera 位置 |

`Rot Target Offset` 使用 Target local space，方向與 General 相同。`Follow Offset` 的座標解讀會受 Tracking Camera 上 Cinemachine Follow 的 `Binding Mode` 與 Target 朝向影響；調整時應以 Game View 為準：

1. 先只改一個軸，確認該 Camera Rig 中的實際方向。
2. 位置不對時調 `Follow Offset`。
3. Camera 位置正確、但看錯位置時調 `Rot Target Offset`。
4. 最後才調 FOV，避免用 FOV 掩蓋位置問題。

## Dolly Profile Settings / Bias

選到 `DollyProfileSO` 時顯示。

### Spline Container

指定場景中的 `SplineContainer`。Dolly Camera 會使用這條 Spline，並由 Profile 的 `positionUnits` 與 `splinePositionCurve` 決定位置。

- 未指定時，Mixer 不會替 Dolly Camera 換上新的 Spline；Camera 可能繼續使用元件上原有的 Spline，因此容易造成看似「有移動但路徑錯誤」的問題。
- 同一個 Profile 可以搭配不同場景 Spline 重用。

### Dolly Bias

| 欄位 | 實際運算 | 用途與限制 |
| --- | --- | --- |
| `FOV Bias` | `fovCurve(t) + Bias`，最後 Clamp 到 `10～120` | 調整視角，不影響在 Spline 上的位置 |
| `Spline Position Bias` | `splinePositionCurve(t) + Bias` | 整段運鏡沿 Spline 前移或後移；不會被 Mirror 或 Reverse 直接改變 |
| `Rot Target Offset X/Y/Z Bias` | 各軸曲線取樣值加 Bias，再套用 Mirror | 改變 Dolly Camera 的注視點 |

Spline Position 的單位由 Profile 資產的 `positionUnits` 決定：

- `Normalized`：`0～1` 代表 Spline 起點到終點，結果會 Clamp 在 `0～1`；Bias `0.1` 約等於整條路徑的 10%。
- 其他單位：沿用該 Profile 的原生單位，Mixer 不另外 Clamp；數值應依 Spline 與 Profile 設定調整。

Reverse Playback 會讓 `splinePositionCurve` 以 `1 - t` 取樣，常用於讓同一運鏡反向走完整條路徑；`Spline Position Bias` 則是把整段取樣位置前後平移，兩者用途不同。

## Overlap 與混合規則

### Parameter Blend

General 與 Tracking 在同類型 overlap 時，會依 Timeline input weight 混合：

- FOV、位置／距離、Target Offset、Screen Position、Damping 都採加權平均。
- 每個 Clip 會先個別套用自己的播放進度、Bias 與 Mirror，再參與混合。
- Tracking Target 不同時，Mixer 會建立暫時 Target，混合兩者的位置與旋轉。

建議 overlap 兩端使用同類型 Profile，並在交會中點檢查構圖。若兩個 Target 距離很遠，即使數值混合正確，中間構圖仍可能穿越不適合的位置。

### Storyboard Render Texture Cross Fade

- 將此模式設在 incoming Clip，而不是 outgoing Clip。
- 必須有且只有兩個有效 Clip 正在 overlap。
- 支援 General、Tracking、Dolly 之間的畫面淡入，但依賴 `CameraSystemMaster` 的 Cross Fade Camera／Brain／Storyboard 設定。
- 這是畫面層級的 dissolve，不是 Profile 參數插值。

### Cross Fade Blur

- 完整沿用 Storyboard Render Texture Cross Fade 的 clip、rig、預覽與 handoff 規則。
- 主 Camera 與離屏 Camera 使用相同的鐘形模糊權重；overlap 兩端清楚，中點最模糊。
- `Blur Max Intensity` 範圍為 `0～5`、預設 `1`；數值只作用於目前 incoming Clip。
- `Alpha Timing` 範圍為 `0～1`、預設 `0`。提高後 RenderTexture Alpha 會延後開始並提早到達 1；Blur 的時序不受影響。
- `Alpha Timing = 1` 時，RenderTexture Alpha 會在 overlap 中點瞬間由 0 切到 1。
- Main Camera 與 Cross Fade Render Camera 必須有 `CameraBlurState`，PC／Mobile Renderer 必須有 `CameraBlurRendererFeature`。
- Blur 設定缺失時退化為普通 Cross Fade，不改成參數混合。

## 快速排錯

| 現象 | 優先檢查 |
| --- | --- |
| 選了 Profile 但畫面沒有正確跟隨／注視 | `Tracking Target` 是否有指定，Timeline Track 是否綁定正確的 `CameraSystemMaster` |
| Dolly 沿錯誤路徑或完全不動 | `Spline Container` 是否指定正確、Profile 的 `positionUnits` 與曲線是否符合該 Spline |
| 拉長 Clip 後運鏡速度改變 | 這是非固定速度模式的預期行為；要保持速度請啟用 `Use Fixed Playback Speed` |
| 固定速度 Clip 後半段不再移動 | Profile 已取樣到 1 並 Hold；降低 Playback Speed 或縮短 Clip |
| Mirror 後 Bias 方向與預期相反 | Mirror 會翻轉「曲線值 + Bias」的總和；需要時同步反轉 Bias 符號 |
| Overlap 中途突然切鏡 | 確認是否為 Dolly／不同 Profile 類型／三個以上 Clip；這些不支援完整 Parameter Blend |
| Storyboard Cross Fade 沒有淡入 | 模式是否設在 incoming Clip、是否恰好兩個 Clip overlap，以及 Cross Fade Rig 是否完整 |
| Cross Fade Blur 有淡入但沒有模糊 | 兩台 Unity Camera 是否有 `CameraBlurState`、目前 Quality Renderer 是否有 `CameraBlurRendererFeature`，以及 incoming Clip 的 Blur Max Intensity 是否大於 0 |
| Bias 調很大但 FOV 不再變化 | 最終 FOV 已被限制在 `10～120` |
| Normalized Dolly Position 超過頭仍停在端點 | Normalized 結果會被限制在 `0～1` |

## 實作來源

本文件依照下列目前實作整理：

- [`CameraProfileAssetEditor.cs`](../Scripts/Editor/CameraProfileAssetEditor.cs)：決定 Clip Inspector 顯示的欄位與 Profile 選擇器行為。
- [`CameraProfileAsset.cs`](../Scripts/Timeline/CameraProfileAsset.cs)：保存 Clip 參數並傳入 Playable Behaviour。
- [`CameraProfileTrack.cs`](../Scripts/Timeline/CameraProfileTrack.cs)：實際取樣、Bias、Mirror、Clamp、Overlap 與 Cross Fade 規則。
