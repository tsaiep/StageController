# 09_Trail

這是用於製作拖尾、流星、火花軌跡的特效，包含沿著曲線飛行的版本，以及綁定物件位置產生拖尾的版本。可用於舞台上的飛行光點、魔法彈道、火花殘影或需要跟隨目標移動的視覺軌跡。
Notion 版本：https://www.notion.so/09_Trail-5b335489f8fc4d7f80f6ac51f1a8c4ed?v=39a8a373d31780a7a033000cf3a1b2f9&source=copy_link
---

## 使用方式

### V_TrailAlongCurve

設定靜態的基本參數

1. 直接將 prefab 放入場景，調整 Root 物件的位置作為特效起點
2. 調整 prefab 底下的 `Target` 位置作為飛行目標點
3. Root 物件上的 Visual Effect 使用 `V_TrailAlongCurve`，可在 `Curve Shape` 分類調整路徑彎曲、高度、角度與控制點分布
4. `Head Display` 分類控制流星頭部外觀與擾動
5. `Path Display` 分類控制路徑拖尾的貼圖、寬度、顏色與流動速度
6. `Dust` 分類控制路徑周圍散佈的粒子

控制項

1. 建議使用 Animation Track 或 `Assets\_FeelCue\Script\VFXPropertiesControl.cs` 控制 `Control` 參數，讓粒子沿曲線從起點跑到終點
2. 使用 `Fade` 控制路徑線段淡出
3. 可在 Timeline 或 Animation 中移動 `Target`，搭配 VFX Property Binder 將 `Target Postion` 更新到 VFX Graph

### V_TrailBinding

設定靜態的基本參數

1. 直接將 prefab 放入要跟隨的物件之下，或者使用 **Parent Constraint component** 來綁定位置
   https://docs.unity3d.com/6000.0/Documentation/Manual/class-ParentConstraint.html
2. `Spawn Control` 控制是否生成拖尾粒子
3. `Trail Count` 控制拖尾線段數量，可依效果需求調整
4. `Head Display` 分類控制頭部粒子的貼圖、寬度、生命與擾動
5. `Dust` 分類控制拖尾周圍火花或碎粒子的生成數量、範圍、生命、尺寸、顏色與閃爍

控制項

1. 使用`Spawn Control`控制粒子生成，建議使用 Animation Track 控制

---

## 參數解釋

### V_TrailAlongCurve

Control

Control - 曲線播放進度控制，0 為起點，1 為終點

Fade - 控制路徑線段淡出

Head Display

Head Texture - 頭部粒子使用的貼圖

Head Color - 頭部粒子顏色

Head Width - 頭部粒子寬度

Head Lifetime Range - 頭部粒子的生命範圍

Head Lifetime Noise Scale - 頭部粒子生命的 Noise 取樣尺寸

Head Turbulence Strength - 頭部粒子的擾動強度

Head Turbulence Scale - 頭部粒子的擾動尺寸

Path Display

Path Texture - 路徑線段使用的貼圖

Pathl Texture Scale - 路徑線段貼圖沿路徑的縮放倍率

Path Color - 路徑線段顏色

Path Width - 路徑線段寬度

Path Scrolling Speed - 路徑線段貼圖流動速度

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

Segement Count -  路徑線段段數，數值越高路徑越細緻，但運算成本也越高

Curve Strength -  路徑線段彎曲強度

Control Point Slide -  路徑線段控制點沿起終點方向的偏移

Control Point Pinch -  路徑線段控制點往中心收束的程度

Same DIr? - 控制 路徑線段兩端方向是否採用相同方向

Height -  路徑線段隆起高度

Angle -  路徑線段彎曲方向旋轉

Binding

Target Postion - 飛行目標位置，由 VFX Property Binder 綁定 `Target` Transform，不用手動輸入

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

VFX Property Binder - 將 `Target` Transform 的位置寫入 `Target Postion`

Target - 曲線終點，調整位置即可改變流星飛行目標