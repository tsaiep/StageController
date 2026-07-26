Noition版本: https://app.notion.com/p/Stage-Light-Controller-35c8a373d3178025894efc8a2d6fd02d?source=copy_link

## 一、系統概念

整套系統分成兩層：

- **燈具生成層**
    - 由 `StageLightArranger` 負責。
    - 根據排列模式產生多盞燈具。
    - 每盞燈會被整理成 `SLMUnit`。
    - 生成後會自動寫入同物件上的 `UnifiedStageController.slmUnits`。
- **Timeline 控制層**
    - 由 `UnifiedStageTrack`、`UnifiedStageClip`、`UnifiedStageMixer`、`UnifiedStageController` 組成。
    - Timeline Clip 負責定義顏色、亮度、燈型、旋轉模式、延遲、Beat 取樣等演出資料。
    - Mixer 將重疊 Clip 加權混合後，交給 `UnifiedStageController` 套用到每盞燈。

---

## 二、快速開始

大致的使用流程，細節將在後續章節說明

1. 要創建一個燈組，在一個空物件上加上 `StageLightArranger`
2. 同一個物件上加上 `UnifiedStageController`，做完這兩步驟後物件應掛載這兩個腳本
    
3. 在 `StageLightArranger.lightPrefab` 指定燈具 Prefab
    
    預設應為`Assets\_StageLightController\Prefab\P_MovingBeamLight`
    
4. 調整排列模式、數量、間距、朝向與 Light/Beam 設定
5. 按下 Inspector 內的 **Generate Lights**
    
6. 確認場景中產生燈組，階層應長這樣
    
7. 建立 Timeline，新增 **Unified Stage Control Track**
    
8. 將場景中的**燈組**綁定到該 Track
    
9. 在 Track 上建立 **整合舞台方案片段 (**`UnifiedStageClip`)，調整顏色、動作與燈型
    
10. 播放 Timeline 或拖曳時間軸檢查效果

---

## 三、燈具生成系統 Stage Light Arranger

!此腳本應掛載在場景物件上

此腳本應掛載在場景物件上

### 1. StageLightArranger

`StageLightArranger` 用來批次生成燈具，生成階層如下：

```
StageLightArranger 物件
  Group_0
    Light_Linear_0_00
    Light_Linear_0_01
  Group_1
    Light_Linear_1_00
    Light_Linear_1_01
```

- 生成時會做以下事情：
    - 刪除舊的子物件燈具。
    - 根據 `Build Mode` 計算每盞燈的位置。
    - 建立 `Group_n`。
    - 從 `lightPrefab` 產生每盞燈。
    - 套用燈具初始朝向。
    - 寫入所有子物件上的 `Light.range`。
    - 寫入所有子物件上的 `VLB.VolumetricLightBeamHD` 相關初始設定。
    - 確保每盞燈具上有 `SLMUnit`。
    - 寫入 `SLMUnit` 的 group/index 資訊。
    - 更新同物件上的 `UnifiedStageController.slmUnits`。

### 2. Base Setting

- Build Mode
    
    `Build Mode` 決定燈具的排列方式。不同的模式會於 **Shape Setting** 產生對應的選項
    
    | 模式 | 說明 |
    | --- | --- |
    | Linear | 直線排列，使用 `spacing` 控制間距 |
    | Arc | 弧形排列，使用 `radius` 與 `arcAngle` 控制半徑與角度 |
    | SShape | S 型排列，使用 `sSpacing`、`sIntensity`、`invertS` 控制形狀 |
    | Polygon | 多邊形排列，使用 `polygonSides`、`polygonRadius` 控制形狀 |
- Light Prefab
    
    燈光單元使用的Prefab，預設應為`Assets\_StageLightController\Prefab\P_MovingBeamLight`
    
- Use Secondary Prefab，是否啟用第二個燈具Prefab，此選項用於製作分散的雷射燈
- Secondary Prefab，Use Secondary Prefab啟用才會顯示於介面，會取代每個組別 Index 0 （第一個燈具）以外的Light Prefab。預設使用`Assets\_StageLightController\Prefab\P_MovingBeamLight_SplitLight` 一個把燈具模型都刪除的Light Prefab版本，用於製作分散的雷射燈
- Count
    
    生成的每一組別燈光數量
    

### 3. Compound Mode

產生多組燈具及設定

| 模式 | 說明 | 參數 |
| --- | --- | --- |
| None | 只產生一組燈 | 無 |
| RingStacking | 產生多圈或多排。Arc/Polygon 會往外擴半徑，Linear/SShape 會往第二軸堆疊 | `GroupCount`控制組數；`RadiusStep`多圈或多排組別之間的距離；`ScaleCountWithRadius`多圈排列是否一半淨增加燈具數量 |
| YLayerStacking | 沿 Y 軸產生多層燈具 | `GroupCount`控制組數；`LayerSpacing`每一層的間距 |

### 4. Light Facing（燈光面向）

決定生成時每盞燈具**根物件**的初始旋轉

| 模式 | 說明 | 可調參數 |
| --- | --- | --- |
| 朝下（預設） | 預設向下 | 無 |
| 朝向排列中心 | 朝向排列中心 | `facingTiltX` 可調整俯仰角 |
| 背向排列中心 | 背向排列中心 | `facingTiltX` 可調整俯仰角 |
| 自訂Euler | 自訂角度 | `customEuler` 精確設定生成角度 |

### 5. Light & Beam Settings（燈光統一設定）

這些設定會在按下 **Generate Lights** 時套用到生成的燈具。

| 欄位 | 說明 |
| --- | --- |
| Range | 生成燈光的照射範圍 |
| Side Softness | 體積燈和Spot Light Inner Angle的預設數值（**Clip動畫會覆寫此設定**） |
| Attenuation Equation |  VLB 體積光插件的燈光衰減設定（應不需調整） |
| 3D Noise Enable | 是否開啟 VLB 體積光插件 Noise 模式 |
| 3D Noise Intensity | 寫入 VLB 體積光插件的 Noise 強度。 |

---

## 四、燈具 Prefab 與 SLMUnit （生成物架構解釋）

### 1. Prefab 基本需求

預設Prefab **P_MovingBeamLight** 包含：

- `SLMUnit腳本`使其可以被燈組定位控制
- **MovingBeamLight_Pan** Pan   用的 Transform(含模型)
- **MovingBeamLight_Tilt** Tilt 用   的Transform(含模型)
- **MovingBeamLight_Glass**   玻璃罩模型，包含同步光源顏色至模型材質顏色的腳本`SyncLightEmissionWithMPB`
- **MovingBeamLight_SpreadPan**   分散的雷射燈使用的Pan偏移
- **MovingBeamLight_SpreadTilt**   分散的雷射燈使用的Tilt偏移
- **SpotLight**  `Light`包含 `VLB.VolumetricLightBeamHD`使用體積光插件，包含 `VLB.VolumetricCookieHD`使用散射模式
- **Laser Mesh Light Mode**  為 Laser Mesh 時開啟的模型

### 2. SLMUnit 欄位

!螢幕擷取畫面 2026-06-10 230753.png

`SLMUnit` 是每盞燈的控制單位。

此腳本無特殊需求應保持預設設定

| 欄位 | 說明 |
| --- | --- |
| `panTransform` | 水平旋轉軸。 |
| `tiltTransform` | 垂直旋轉軸。 |
| `targetLight` | 實際被控制的 Unity `Light`。VLB 通常也掛在同一個物件上。 |
| `invertPan` | 反轉此燈的 Pan。 |
| `invertTilt` | 反轉此燈的 Tilt。 |
| `motionOffset` | 每盞燈自己的動作時間偏移。 |
| `rotationBase` | 作為 Timeline 動畫的零點基準。 |
| `LaserMeshRenderers`  | 指定 Light Mode 為 Laser Mesh 時開啟的模型 |

`groupIndex`、`groupCount`、`indexInGroup`、`groupSize` 由 `StageLightArranger.GenerateLights()` 自動寫入，供延遲與 Beat 分組使用。

---

## 五、UnifiedStageController

這個腳本是實際套用燈光與運動結果的核心。要受 Timeline 影響的話一定需要在物件上掛載此腳本

### 1. 受控單元配置

- **Sim Units**
所有被控制的燈具List。通常由 `StageLightArranger` 自動填入
- **Default Target**
Timeline 動畫的 Target 模式沒有指定 clip target 時使用的預設追蹤目標
- ~~**Audio Source**
`AlongAudioSource` 顏色取樣模式使用的音訊來源~~ Legacy功能不使用
- **Audio Analyzer**
Audio Analyzer Brightness 使用的 `MMAudioAnalyzer` 來源。若 Clip 沒有啟用 `useAudioAnalyzerBrightness`，此欄位不會影響燈光亮度

### 2. 播放控制

此區無特殊需求應保持預設設定

- **Enable Motion**
預設應開啟。關閉後不更新燈具旋轉
- **Enable Color Update**
預設應開啟。關閉後不更新顏色

### 3. 群組對稱設定

此區無特殊需求應保持預設設定

- **Invert Controller Pan**
    
    控制燈具 Pan 的旋轉正負，預設為不勾選
    
- **Invert Controller Tilt**
    
    控制燈具 Tilt 的旋轉正負，預設為不勾選
    

| 欄位                   | 說明                                                 |
|----------------------|----------------------------------------------------|
| `slmUnits`           | 所有被控制的燈具。通常由 `StageLightArranger` 自動填入。            |
| `defaultTarget`      | Target 模式沒有指定 clip target 時使用的預設追蹤目標。              |
| ~~`audioSource`~~    | ~~`AlongAudioSource` 顏色取樣模式使用的音訊來源。~~  **Lagacy功能不使用** |
| `audioAnalyzer`      | Audio Analyzer Brightness 使用的 `MMAudioAnalyzer`。   |
| `enableMotion`       | 全域開關，關閉後不更新燈具旋轉。                                   |
| `enableColorUpdate`  | 全域開關，關閉後不更新顏色。                                     |
| `baseIntensity`      | Unity Light intensity 的基準值。                        |
| `waveIntensity`      | 影響 `SLMUnit.motionOffset` 的時間偏移倍率。                 |
| `baseSmoothTime`     | 一般旋轉平滑時間。                                          |
| `maxRotationSpeed`   | 最大旋轉速度限制。                                          |
| `trackingSmoothTime` | Target 模式使用的平滑時間。                                  |

### 4. 基礎物理參數

為確保動畫資產可以重複運用，此區無特殊需求應保持預設設定

!基礎物理參數參考預設值

基礎物理參數參考預設值

- **Pan Rotation Vector**
    
    負責 Pan 的子物件旋轉軸向
    
- **Tlit Roattion Vector**
    
    負責 Tilt 的子物件旋轉軸向
    
- **Base Intensity**
Light intensity 的基準值
- **Wave Intensity**
影響 `SLMUnit.motionOffset` 的時間偏移倍率
- **Base Smooth Time**
一般旋轉平滑時間

### 5. 追蹤進階修正

目標追蹤模式的基本參數

此區無特殊需求應保持預設設定

!追蹤進階修正的參考預設值

追蹤進階修正的參考預設值

- Pan Offset
    
    Pan 追蹤偏移
    
- Tilt Offset
Tilt 追蹤偏移
- Invert Vertical Tracking
追蹤水平反轉
- Vertical Base Offset
水平旋轉的基準點

### 6. 追蹤自然度微調

燈光旋轉與跟隨設定

- Max Rotation Speed
    
    最大旋轉速度限制，無特殊需求應保持預設設定
    
- Tracking Smooth Time
    
    目標追蹤模式的平滑時間
    

### 7. 旋轉基準偏移

這裡會列出此燈組所有綁定的燈光單位`SLMUnit`，並且可以分別設定每一盞光的歸零角度，此燈光在受到Custom Clip控制時會以此角度為基準做進一步的偏移、運動

- 每一盞燈可以點開設定Pan, Tilt，一旦更改此數值燈光會立即更新其旋轉
- 最上方的批次設定，可以一次更改所有燈光，輸入數值後需要按左邊的「套用到全部」按鈕

---

## 六、Timeline Custom Clip 系統

### 1. 建立 Track

1. 打開 Timeline
2. 新增 Track：**Unified Stage Control Track**
3. 將場景中的**燈組**綁定到 Track
4. 在 Track 上新增**整合舞台方案片段 (**`UnifiedStageClip`)

### 2. Clip 混合方式

當多個 Clip 重疊時，`UnifiedStageMixer` 腳本負責處理 Timeline weight 混合：

- 顏色：每個 Clip 先算出自己的顏色，再乘上 weight 相加
- Pan/Tilt：每個 Clip 算出自己的角度，再乘上 weight 相加
- Intensity、Beam Angle、Light Range、Softness：依 weight 加權
- 燈型，指VLB Spot Light / Spot Light/ Point Light和 Scatter Mode 由權重較高的 Clip 決定

因此，如果兩個 Clip 交疊淡入淡出，顏色與動作會平滑混合；但燈型會由權重較高的 Clip 決定

---

## 七、UnifiedStageClip 欄位說明

點選 Clip 後可從 Inspector 視窗調整其參數

### 1. 燈光感應設定

| 欄位 | 說明 |
| --- | --- |
| `globalColor` | 全域顏色倍率，支援 HDR。最後顏色會乘上它 |
| `Beam Length Gradient` | 當 `Light Mode` 為`Volumetric Spot Light` 時，光束頭尾的固有顏色。`Light Mode`詳細請見`燈具物理設定` |
| `lightGradient` | 部分模式使用的主要顏色漸層，詳細請見`colorSampleMode` |
| `intensityMultiplier` | 此 Clip 的亮度倍率，最後顏色會乘上它 |

### 2. 顏色取樣設定 - 取樣模式

Color Sample Mode

| 模式 | 說明 |
| --- | --- |
| 動作循環 | 依目前旋轉模式的運動週期取樣 `lightGradient`。適合讓顏色跟動作同步 |
| 片段進度 | 依 Clip 播放進度 0 到 1 取樣 `lightGradient` |
| 跟隨節拍（漸層取樣） | 依 BPM 週期取樣 `lightGradient` |
| 跟隨節拍（瞬間切換） | 依 BPM 在 `beatSnapColors` 中跳色 |
| 跟隨音樂 | 根據 `audioSource` 的低中高頻能量取樣 `lightGradient` |

~~### 3. 顏色取樣設定 - 跟隨音樂設定~~

~~| 欄位 | 說明 |~~
~~| --- | --- |~~
~~| Sensitivity | 感應音樂強度的敏感度 |~~
~~| Smoothness | 跟隨音樂模式顏色的過度平滑 |~~

### 3. Audio Analyzer Brightness 設定

此區會使用 `UnifiedStageController.audioAnalyzer` 指定的 `MMAudioAnalyzer`，讀取 `Beats` 陣列中各 Beat 的 `CurrentValue`，再將結果乘到此 Clip 最後算出的顏色亮度上。

使用前需要在場景中準備 `MMAudioAnalyzer`，並在 `UnifiedStageController` 的 **Audio Analyzer** 欄位指定它。若沒有指定，或 `MMAudioAnalyzer.Beats` 為空，以下 Clip 參數不會產生效果。

| 欄位 | 說明 |
| --- | --- |
| `useAudioAnalyzerBrightness` | 是否啟用 Audio Analyzer 亮度控制。關閉時此 Clip 維持原本亮度 |
| `audioBeatLightInterval` | 每幾盞燈共用同一個 Beat Index。數值至少為 1。例如設為 2 時，組內每 2 盞燈才切到 `audioBeatIndices` 的下一個項目 |
| `audioBeatIndices` | 指定要讀取的 `MMAudioAnalyzer.Beats` 索引列表。索引從 0 開始，會依燈具的 `indexInGroup` 與 `audioBeatLightInterval` 分配到各燈 |
| `audioBrightnessOffset` | 當音量為 0 時的亮度倍率。0 代表無 Beat 時變黑，1 代表保留原始亮度 |
| `audioBrightnessMultiplier` | `Beat.CurrentValue` 放大的亮度倍率 |
| `audioBrightnessLerp` | 額外亮度平滑速度，0 表示不額外平滑 |

### 4. 顏色取樣設定 - Beat 相關欄位

這些參數僅在跟隨節拍（漸層取樣）與 跟隨節拍（瞬間切換）當中生效

| 欄位 | 說明 |
| --- | --- |
| `bpm` | 節拍速度 |
| `beatTimeRef` | Beat 時間基準• `ClipLocal`每個 Clip 起點視為第一拍
；`TimelineGlobal`以整條 Timeline 的全域時間作為 Beat 基準 |
| `beatPhaseOffset` | Beat 偏移，單位為秒 |
| `beatSnapColors` | Color Sample Mode 中`BeatSnap` 模式使用的顏色列表 |
| `beatSnapTransitionTime` | `BeatSnap` 換色前的漸變時間，單位為秒 |
| `beatGroupDelayFactor` | 依 group 排序造成 Beat 偏移 |
| `beatLightDelayFactor` | 依 group 內燈具排序造成 Beat 偏移 |
| `beatGroupDelayCurve` | group Beat 偏移曲線 |
| `beatLightDelayCurve` | group 內燈具 Beat 偏移曲線 |

節拍延遲參數補充 Group 排序、Group 內延遲的計算根據不同模式有相應的算法

- 跟隨節拍（漸層取樣）
    
    燈光排序經過Curve重新取樣後乘上延遲係數，套用到Beat的偏移
    
- 跟隨節拍（瞬間切換）
    
    燈光排序經過Curve重新取樣得一個偏移使用`beatSnapColors`的顏色順序，延遲係數則負責設定每幾個燈偏移一位`beatSnapColors`的顏色，例如設定為2，則每2個燈往後偏移一位
    

### 5. 燈具物理設定

| 欄位 | 說明 |
| --- | --- |
| `lightMode` | 決定使用 `VolumetricSpot`、`Spot` 或 `Point` |
| `lightRange` | 設定光的範圍 |
| `beamAngle` | Spot/VLB 的光束角度 |
| `softness` | 調整光束的邊緣硬度。Spot 模式對應 inner spot angle；VLB 模式對應 side softness |
| `enableScatterMode` | 啟用或關閉散射模式 |

Light Mode 細節

- `VolumetricSpot`：使用 Unity Spot Light，並啟用 VLB HD。
- `Spot`：只使用 Unity Spot Light，關閉 VLB。
- `Point`：使用 Unity Point Light，關閉 VLB。

Point Light 是全方向發光，同樣 intensity 會比其他更容易過亮。目前在 `Point` 模式套用 0.15 倍亮度校正。

### 6. 旋轉模式

| 模式 | 說明 |
| --- | --- |
| 靜止模式 | 固定在 `staticAngleOffset` |
| 掃描模式 | Pan 方向左右掃描 |
| 圓周運動 | 以圓形路徑運動 |
| 雖機跳動 | 使用 Perlin Noise 產生隨機掃動 |
| 目標追蹤 | 所有燈光朝向指定目標()`trackingTarget` 指定的物件) |
| 上下搖擺 | Tilt 方向上下擺動 |
| 交叉掃描 | 偶數與奇數燈具分別朝左右，Tilt 擺動 |
| 凍結前幀 | 凍結進入此 Clip 前一瞬間的 Pan/Tilt/顏色 |

### 7. 旋轉動作設定

| 欄位 | 說明 |
| --- | --- |
| `rotationSpeed` | 動作速度 |
| `rotationRange` | 動作幅度 |
| `staticAngleOffset` | 靜態角度或動作中心偏移，x 是 pan，y 是 tilt |
| `cyclePauseTime` | Clip 開始後延遲進入有效運動的時間 |
| `animationOffset` | 動作時間偏移。Static 與 FreezeFrame 不套用此偏移 |
| `trackingTarget` | Target 模式要追蹤的 Transform |

### 8. 分散效果設定

製作分散的雷射燈（將同一組別的燈具重疊）使用的參數

| 欄位 | 說明 |
| --- | --- |
| Spread Angle | 根據組內ID輻射分散的角度 |
| Spread Arc Range | 燈具分散形成的圓弧角度(360為完整的圓；180則會排成半圓)。若為0配合`Spread Angle Curve By Index`和`Spread Pan Curve`可做出水平/垂直的分散燈 |
| Spread Angle Curve | 每次旋轉動作循環Spread Angle的變化曲線，數值最終和Spread Angle參數相乘，套用到燈具 MovingBeamLight_SpreadTilt |
| Spread Angle Curve By Index | 依 indexInGroup 正規化後取樣的 `Spread Angle` 強度係數，可用於水平/垂直的分散燈。 |
| Spread Pan Curve | 每次旋轉動作循環MovingBeamLight_SpreadPan的變化曲線，數值最終和Spread Arc Range參數的影響相加。若0-1統一控制可作為設定水平/垂直的分散燈 |

### 9. Group / Light 偏移（舊版分類為延遲）

讓同一個動畫控制的燈具根據分組ID、組內ID產生不同變化。

| 欄位 | 說明 |
| --- | --- |
| Group Delay Curve | 依 groupIndex 正規化後取樣的延遲曲線 |
| Group Delay Factor | group 延遲倍率 |
| Group Rotation Range Curve | 依 groupIndex 正規化後取樣的`rotationRange`強度係數，~~可用於分散雷射燈~~ |
| Light Delay Curve | 依 indexInGroup 正規化後取樣的延遲曲線 |
| Light Delay Factor | group 內燈具延遲倍率 |
| Light Rotation Range Curve | 依 indexInGroup 正規化後取樣的`rotationRange`強度係數，~~可用於分散雷射燈~~ |

### 10. 凍結前幀

此`rotationMode`比較特殊，它可以維持前一個Clip的姿態以及顏色數值

- 進入 FreezeFrame 的第一幀會記錄每盞燈當下的 pan、tilt、color
- FreezeFrame 期間會維持記錄到的 pan/tilt
- `freezeUseClipGradient` 關閉時，顏色使用進入前記錄的顏色
- `freezeUseClipGradient` 開啟時，顏色改用此 Clip 自己的 `lightGradient`，並依 Clip 進度取樣

---

## 八、Template 使用

### 製作 Template

1. 在Clip最下面有一藍色按鈕，可以將當前的數值導出為模板（**ScriptableObject）**，模板副檔名為`.asset`。模板命名可使用中文，建議規範名稱以方面查找
    
2. 選擇輸出的模板資產可以檢視或修改數值
3. 模板資產最上方有 Template Tags 欄位，可使用下拉選單編輯資產的 Tag，其正式名稱為 Unified Stage Template Tag 方便套用模板時查找
    
4. Unified Stage Template Tag 由另一組**ScriptableObject（檔名亦是.asset）定義，若要新增 Tag 可在Project 視窗Create/Stage Control/**Unified Stage Template Tag，Tag 顯示名稱將與檔案名同名。
**當前**的 Unified Stage Template Tag 清單存放於於Asset/_StageLightController/Template/_Tags
    

### 套用 Template

1. Unified Stage Clip 介面最上方有 Stage Template 區塊，按下 Select Template 會觸線模板選擇視窗 （Stage Template Seletor）
    
2. 搜尋欄位和其下面的 Unified Stage Template Tag 可以幫助使用者篩選模板
    
3. 點選左側欄位的模板後按下右下的 Apply Template To Clip 按鈕可以將此模板的數值套用至所選擇的 Unified Stage Clip ，若開啟此視窗期間在 Timeline 上選擇其他 Clip，套用對象將會改為新選擇的對象 。
4. 右下 Ping 按鈕會在 Project 視窗中標亮當前選擇的 Unified Stage Template
5. 右下3D預覽視窗能夠預覽模板的動態，此預覽環境當中質量光被替換成圓柱體模型，因此除了燈光長度、燈光角度外都能夠正常預覽
6. 右側 Apply Options 分類下的參數可依照類別部分套用，附圖中以框選顏色標註參數分屬於哪個類別
    - Apply Color Setting 套用顏色設定（紅）
    - Apply Rotation Setting 套用旋轉動畫設定（綠）
    - Apply Fixture Setting 套用燈具物理設定（藍）
    
7. 右側 Preview Setting 分類下的參數 Stack Preview Lights 可以將預覽3D視窗的燈光在原點重疊，能夠更好的預覽分散雷射光的動畫；Preview Light Amount 可以調整預覽燈具的數量

---

## 九、建議工作流程

1. 先準備好燈具 Prefab
2. 設定好燈組根物件同一物件上包含`StageLightArranger`與`UnifiedStageController`
3. 使用 `StageLightArranger` 產生燈組並設定需要的排列
4. 手動微調生成的 Prefab 位置
5. 於燈組最上層 `UnifiedStageController` 腳本設定燈光的歸零角度
6. 建立 Timeline，新增 **Unified Stage Control Track，將要編輯的燈組綁定在 Track**
7. Track 上建立 **整合舞台方案片段 (**`UnifiedStageClip`)
8. 若已有Template `UnifiedStageTemplate` 可直接套用
9. 調整燈組動畫的旋轉動畫設定
    1. 選擇旋轉模式
    2. 建議先將`rotationRange`調整為0，
    3. 設定`staticAngleOffset`決定動畫朝向，若旋轉模式為「目標追蹤」可以跳過此步驟
    4. 調整`rotationRange`的強度
10. 調整燈組動畫的顏色設定
11. 調整燈具的顏色和旋轉延遲
12. 調整燈組動畫的燈具物理設定
13. 在 Timeline 上用 `UnifiedStageClip` 編排演出

---

## 十、分散雷射燈的設定

基本操作流程請讀「九、建議工作流程」，這裡僅說明分散雷射燈需要的設置

### 生成燈組

1. `StageLightArranger` Base Setting中的 Use Secindary Prefab 打勾，並填入不含Mesh Renderer 的燈組Prefab `Assets\_StageLightController\Prefab\P_MovingBeamLight_SplitLight`
2. Build Mode 選擇 Line ，並且把 Shape Setting 的 Spacing（燈距）設為0。如此原先的一排燈具組合成了一顆分散雷射燈
    
3. 若需要一次生成多顆分散雷射燈在Compound Mode（分組複製）選擇Ring Stacking，並設定分組數以及間距（當然，生成後可以隨意調整每一組別的位置）
    
4. Light Facing（燈光面相）Facing Mode 請選擇朝下（預設）或者自訂 Euler，讓同一燈組下的燈具底座都朝向同一方向
    

### 動畫設定

1. **分散效果設定** 適用於製造燈光輻射狀分散的動態，透過燈具 Prefab 當中新的關節`MovingBeamLight_SpreadPan` 和 `MovingBeamLight_SpreadTilt` 來達成，參數細節見「七、UnifiedStageClip 欄位說明」當中的「8. 分散效果設定」
    
2. ~~要製作扇形的分散請使用組內偏移當中的Light Rotation Range Curve，跟駔組內ID 取樣Curve的值，此值會與原本的旋轉值相乘。
舉例來說，若想製作從中央散開的動畫，Rotation Mode 可設為掃描模式，此時燈光會左右搖擺，透或將 Light Rotation Range Curve 設為 -1 ~ 1 的線性曲線，ID在中央的燈將保持不變，ID靠前和靠後的燈將以反方向移動，並且數值根據編號遞增或遞減，從而得到扇形的~~燈組動畫
    
    !SplitLight01.gif
    
3. 2.的確可以做出以上示範效果，但作為分散雷射燈的變化較低。新增分散效果設定的Spread Angle Curve By Index 是比較好的製作水平/垂直分散雷射燈的方式。
要製作水平/垂直分散雷射燈，基本有三個參數要調整
    1. Spread Arc Range 應設為0，如此每一個燈具的 SpreadPan 會一致
    2. Spread Pan Curve 0-1設為統一的數值，一來確保不會旋轉，並且數值展開的方向（0為水平；0.25為垂直）
    3. Spread Angle Curve By Index 透過控制燈組內燈具ID的 SpearTilt 係數，做出扇形展開的效果
4. 由於分散雷射燈本質是將一個燈組重疊，因此可以運用組內燈光的延遲參數組合出不同的效果[@08_EvironmentSpline.md](file:///D:/UnityProject/StageController/Assets/_VFXLibrary/08_EnvironmentSpline/08_EvironmentSpline.md)
