# 09_Trail

這是用於製作拖尾、流星、火花軌跡的特效，包含沿著曲線飛行的版本，以及綁定物件位置產生拖尾的版本。可用於舞台上的飛行光點、魔法彈道、火花殘影或需要跟隨目標移動的視覺軌跡。

---

## 使用方式

### P_MeteorAlongCurve

設定靜態的基本參數

1. 直接將 prefab 放入場景，調整 Root 物件的位置作為特效起點
2. 調整 prefab 底下的 `Target` 位置作為飛行目標點
3. Root 物件上的 Visual Effect 使用 `V_TrailAlongCurve`，可在 `Curve Shape` 分類調整路徑彎曲、高度、角度與控制點分布
4. `Head Display` 分類控制流星頭部外觀與擾動
5. `Path Display` 分類控制路徑拖尾的貼圖、寬度、顏色與流動速度
6. `Dust` 分類控制路徑周圍散佈的粒子

控制項

1. 建議使用 Animation Track 或 `Assets\_FeelCue\Script\VFXPropertiesControl.cs` 控制 `Control` 參數，讓流星沿曲線從起點跑到終點
2. 使用 `Fade` 控制整體淡出
3. 可在 Timeline 或 Animation 中移動 `Target`，搭配 VFX Property Binder 將 `Target Postion` 更新到 VFX Graph

### P_MeteorBinding / P_SparkTrailBinding

設定靜態的基本參數

1. 直接將 prefab 放入場景，將需要拖尾的物件位置綁定到 VFX Graph
2. Root 物件上的 Visual Effect 使用 `V_TrailBinding`，適合物件移動時持續留下軌跡
3. `Spawn Control` 控制是否生成拖尾粒子
4. `Trail Count` 控制拖尾線段數量，可依效果需求調整
5. `Head Display` 分類控制頭部粒子的貼圖、寬度、生命與擾動
6. `Dust` 分類控制拖尾周圍火花或碎粒子的生成數量、範圍、生命、尺寸、顏色與閃爍

控制項

1. 建議使用 Animation Track 或 `Assets\_FeelCue\Script\VFXPropertiesControl.cs` 控制 `Spawn Control` 參數，達成拖尾生成與停止
2. `P_MeteorBinding` 適合較大的流星或能量拖尾
3. `P_SparkTrailBinding` 適合較細碎、粒子量較高的火花拖尾
4. 若目標物件在播放時移動，需確認 VFX Property Binder 會持續更新綁定目標位置

---

## 參數解釋

### V_TrailAlongCurve

Control

Control - 曲線播放進度控制，0 為起點，1 為終點

Fade - 整體淡出控制

Head Display

Head Texture - 頭部粒子使用的貼圖

Head Color - 頭部粒子顏色

Head Width - 頭部粒子寬度

Head Lifetime Range - 頭部粒子的生命範圍

Head Lifetime Noise Scale - 頭部粒子生命的 Noise 取樣尺寸

Head Turbulence Strength - 頭部粒子的擾動強度

Head Turbulence Scale - 頭部粒子的擾動尺寸

Path Display

Path Texture - 拖尾路徑使用的貼圖

Pathl Texture Scale - 拖尾路徑貼圖沿路徑的縮放倍率

Path Color - 拖尾路徑顏色

Path Width - 拖尾路徑寬度

Path Scrolling Speed - 拖尾路徑貼圖流動速度

Dust

Dust Spawn Rate - Dust 粒子生成數量

Dust Spawn Radius - Dust 粒子偏離路徑的生成半徑

Dust Texture - Dust 粒子使用的貼圖

Dust Color 1 - Dust 粒子顏色之一，最終顏色會在 Color 1 與 Color 2 之間取樣

Dust Color 2 - Dust 粒子顏色之二，最終顏色會在 Color 1 與 Color 2 之間取樣

Dust Lifetime Range - Dust 粒子生命範圍

Dust Size Range - Dust 粒子尺寸範圍

Dust Sparkling Speed - Dust 粒子閃爍速度，0 為不閃爍

Dust Turbulence Strength - Dust 粒子擾動強度

Dust Turbulence Scale - Dust 粒子擾動尺寸

Curve Shape

Segement Count - 曲線取樣段數，數值越高路徑越細緻，但運算成本也越高

Curve Strength - 曲線彎曲強度

Control Point Slide - 控制點沿起終點方向的偏移

Control Point Pinch - 控制點往中心收束的程度

Same DIr? - 控制曲線兩端方向是否採用相同方向邏輯

Height - 曲線高度

Angle - 曲線彎曲方向角度

Binding

Target Postion - 飛行目標位置，通常由 VFX Property Binder 綁定 `Target` Transform，不用手動輸入

### V_TrailBinding

Spawn Control

Spawn Control - 生成控制項，生成動畫建議由此控制

Head Display

Trail Count - 拖尾線段數量

Head Texture - 頭部粒子使用的貼圖

Head Color - 頭部粒子顏色

Head Width - 頭部粒子寬度

Head Lifetime Range - 頭部粒子的生命範圍

Head Lifetime Noise Scale - 頭部粒子生命的 Noise 取樣尺寸

Head Turbulence Strength - 頭部粒子的擾動強度

Head Turbulence Scale - 頭部粒子的擾動尺寸

Dust

Dust Spawn Rate - Dust 粒子生成數量

Dust Spawn Radius - Dust 粒子偏離拖尾中心的生成半徑

Dust Texture - Dust 粒子使用的貼圖

Dust Color 1 - Dust 粒子顏色之一，最終顏色會在 Color 1 與 Color 2 之間取樣

Dust Color 2 - Dust 粒子顏色之二，最終顏色會在 Color 1 與 Color 2 之間取樣

Dust Lifetime Range - Dust 粒子生命範圍

Dust Size Range - Dust 粒子尺寸範圍

Dust Sparkling Speed - Dust 粒子閃爍速度，0 為不閃爍

Dust Turbulence Strength - Dust 粒子擾動強度

Dust Turbulence Scale - Dust 粒子擾動尺寸

---

## Prefab 結構

P_MeteorAlongCurve

Visual Effect - 播放 `V_TrailAlongCurve`

VFX Property Binder - 將 `Target` Transform 的位置寫入 `Target Postion`

Target - 曲線終點，調整位置即可改變流星飛行目標

P_MeteorBinding / P_SparkTrailBinding

Visual Effect - 播放 `V_TrailBinding`

VFX Property Binder - 將綁定目標的位置傳入 Visual Effect，使移動物件產生拖尾
