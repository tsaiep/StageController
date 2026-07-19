# 08_EnvironmentSpline

這是沿著一組 Transform 控制點生成的環境線條特效，可以製作漂浮光帶、魔法緞帶、金色絲線等空間路徑效果。Prefab 內建 VFX Property Binder，會把 `Lighting_P0` 到 `Lighting_P7` 的位置寫入 VFX Graph 的 `PositionMap` 與 `PositionCount`，用這些點決定線條形狀。
Notion 版本：https://www.notion.so/08_EnvironmentSpline-0f4016d70d794776b7be6bace8a897f5?source=copy_link
---

## 使用方式

### V_EnvironmentSpline / V_EnvironmentSpline_Facing

設定靜態的基本參數

1. 直接將 prefab 放入場景，調整底下的Knot位置決定光帶路徑，若需要新增節點只需要把目標物件新增到 VFX Property Binder 的欄位即可
2. Root 物件上的 Visual Effect 調整整體顯示、顏色、Trail、Glow、Dust 與 Shape 相關參數
3. 若只需要固定路徑，調整完控制點後保持 VFX Property Binder 的 EveryFrame 關閉即可
4. 若需要在播放時移動控制點，需開啟 Binder 的 EveryFrame，讓控制點位置每幀更新至 VFX Graph

控制項

1. 建議使用 Animation Track 或 `Assets\_FeelCue\Script\VFXPropertiesControl.cs` 控制 `Control` 參數，達成整體淡入淡出
2. 可使用 Timeline 或 Animation 直接控制 Knot 的位置，改變光帶路徑

### 兩版本差異

1. `V_EnvironmentSpline` 為一般環境光帶版本
2. `V_EnvironmentSpline_Facing` 為偏向面向攝影機觀看的版本，適合需要從觀眾方向保持可讀性的光帶
3. 兩個版本的主要公開參數相同，可依畫面需求替換 Visual Effect Asset

---

## 參數解釋

### V_EnvironmentSpline / V_EnvironmentSpline_Facing

Control

Control - 整體效果的顯示控制

Global Color - 整體顏色倍率，可用於統一調整亮度與色調

Head-Tail Fading Range - 線條頭尾淡出的範圍，X 為頭端淡出範圍，Y 為尾端淡出範圍

Trail

Trail Texture - 主線條使用的貼圖

Trail Color - 主線條顏色

Trail Scaling - 主線條貼圖沿路徑的縮放倍率

Trail Scrolling Speed - 主線條貼圖沿路徑流動的速度

Trail Width - 主線條寬度

Glow

Glow Texture - 外圍光暈使用的貼圖

Glow Color - 外圍光暈顏色

Glow Scaling - 光暈貼圖沿路徑的縮放倍率

Glow Scrolling Speed - 光暈貼圖沿路徑流動的速度

Glow Width - 光暈寬度

Dust

Dust Texture - 路徑周圍散佈粒子使用的貼圖

Dust Per Segement Spawn Rate - 每段路徑生成 Dust 粒子的數量倍率

Dust Spawn Range - Dust 粒子偏離路徑的生成範圍

Dust Color - Dust 粒子顏色

Dust Size - Dust 粒子尺寸

Shape

Resolution - 路徑取樣解析度，數值越高線條越細緻，但運算成本也越高

Spline Smoothness - 控制路徑在控制點之間的平滑程度

Swirl Strength - 線條繞路徑旋轉擾動的強度

Swirl Scale - 旋轉擾動的尺寸

Swirl Smoothness - 旋轉擾動的平滑程度

Noise Strength - 路徑位置 Noise 擾動強度

Noise Scale - Noise 擾動尺寸

Noise Speed - Noise 擾動動畫速度

Binding

PositionMap - 控制點位置貼圖，由 VFX Property Binder 自動寫入，不用手動指定

PositionCount - 控制點數量，自動寫入，不用手動指定

---

## Prefab 結構

P_EnvironmentSpline_GoldenString / P_EnvironmentSpline_MagicRibbon

Visual Effect - 播放 `V_EnvironmentSpline` 或 `V_EnvironmentSpline_Facing`

VFX Property Binder - 管理 VFX Binder，將控制點資料傳入 Visual Effect

PositionMap / PositionCount Binder - 讀取 Targets 內的控制點 Transform，產生路徑資料

Lighting_P0 ~ Lighting_P7 - 路徑控制點，調整位置即可改變光帶形狀