# 03_Raining

這是下雨的效果包含接觸地面的漣漪效果。可調整參數變為下雪效果，然而下雪不會產生漣漪，為節省運算開銷另製作一個無漣漪效果的版本

Notion版本：https://www.notion.so/03_Raining-4436e34969084c7196f434739f48cecb?source=copy_link

---

## 使用方式

### V_Raining

設定靜態的基本參數

1. Ground Height 設定地面高度，即漣漪產生的高度
2. Spawn 分類下設定粒子每秒生成數量、生成範圍
3. Spawn 分類下設定粒子的外觀參數，例如尺寸、貼圖、顏色
4. Force 分類下設定與速度有關的參數，Wind Direction與Wind Strength 設定與烙下的斜度與方向
5. Ripple & Splash 分類下設定漣漪與水花的數量與外觀參數

控制項

1. 建議使用 `Animation Track 或 Assets\_FeelCue\Script\VFXPropertiesControl.cs 控制 Spawn Control` 參數，達成控制粒子生成

### V_Raining_WithoutRippleAndSplash

設定靜態的基本參數

1. Ground Height 設定地面高度，即漣漪產生的高度
2. Spawn 分類下設定粒子每秒生成數量、生成範圍
3. Spawn 分類下設定粒子的外觀參數，例如尺寸、貼圖、顏色
4. Force 分類下設定與速度有關的參數，Wind Direction與Wind Strength 設定與烙下的斜度與方向

控制項

1. 建議使用 `Animation Track 或 Assets\_FeelCue\Script\VFXPropertiesControl.cs 控制 Spawn Control` 參數，達成控制粒子生成

---

## 參數解釋

### V_Raining

Control

**Spawn Control** - 生成控制項，生成動畫建議由此控制

**Fade Out by Spawn Control** - 是否於 Spawn Control 數值遞減時粒子同步淡出，若不勾選當 Spawn Control 為0時，既存的粒子會持續存在直到生命結束

Ground Height - 設定地面高度，即漣漪產生的高度

Spawn

Spawn Rate - 每秒生成數量

Lifetime Range - 粒子生命範圍

Spawn Box Size - 粒子生成範圍，單位為公尺

Particle Size Range - 粒子尺寸範圍

Particle Stretch - 粒子的Y軸拉伸倍率

Particle Texture - 模型使用的 Diffuse 貼圖

Color 1 - 粒子顏色之一，Alpha為透明度，最終結果在與 Color 2 之間隨機

Color 2 - 粒子顏色之二，Alpha為透明度，最終結果在與 Color 1 之間隨機

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含Alpha

Force

Rain Velocity - 粒子生成時往下的初始速度

Wind Direction - 粒子傾斜的方向，方向為0至360度，需配合Wind Strength

Wind Strength - 粒子傾斜的強度

Wind Randomize - 個別粒子受 Wind Direction 與 Wind Strength 影響的隨機係數，0 為不隨機

Turbulence Frequency - 粒子的立場 Noise 頻率

Turbulence Strength -  粒子的立場 Noise 強度

Ripple & Splash

Ripple and Splah Alpha - 漣漪與水花的透明度

Ripple Lifetime - 漣漪的生命時長

Splash Lifetime - 水花的生命時長

Ripple Size - 漣漪的尺寸

Splash Size - 水花的尺寸

### V_Raining_WithoutRippleAndSplash

Control

**Spawn Control** - 生成控制項，生成動畫建議由此控制

**Fade Out by Spawn Control** - 是否於 Spawn Control 數值遞減時粒子同步淡出，若不勾選當 Spawn Control 為0時，既存的粒子會持續存在直到生命結束

Ground Height - 設定地面高度，即漣漪產生的高度

Spawn

Spawn Rate - 每秒生成數量

Lifetime Range - 粒子生命範圍

Spawn Box Size - 粒子生成範圍，單位為公尺

Particle Size Range - 粒子尺寸範圍

Particle Stretch - 粒子的Y軸拉伸倍率

Particle Texture - 模型使用的 Diffuse 貼圖

Color 1 - 粒子顏色之一，Alpha為透明度，最終結果在與 Color 2 之間隨機

Color 2 - 粒子顏色之二，Alpha為透明度，最終結果在與 Color 1 之間隨機

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含Alpha

Force

Rain Velocity - 粒子生成時往下的初始速度

Wind Direction - 粒子傾斜的方向，方向為0至360度，需配合Wind Strength

Wind Strength - 粒子傾斜的強度

Wind Randomize - 個別粒子受 Wind Direction 與 Wind Strength 影響的隨機係數，0 為不隨機

Turbulence Frequency - 粒子的立場 Noise 頻率

Turbulence Strength -  粒子的立場 Noise 強度