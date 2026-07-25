# 01_ParticleScattering

這是通用的散佈環境粒子效果，有Lit模型與 Unlit Billboard 兩種變體，可以模擬諸如灰塵、螢火蟲、自訂義的飄浮模型等效果

Notion 版本：
https://www.notion.so/01_ParticleScattering-3958a373d31780e6b0f9ffa615f8ed12?source=copy_link

---

## 使用方式

### V_ParticleScattering_LitMesh 吃光的模型散佈

設定靜態的基本參數

1. Spawn 分類下設定每秒生成數量、Lifetime、生成範圍、粒子尺寸範圍、顏色、使用貼圖模型等基本參數
2. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化
3. Force 分類下設定與速度有關的參數

控制項

1. 建議使用 Animation Track 或 Assets\_FeelCue\Script\VFXPropertiesControl.cs 控制 Spawn Control 參數，達成控制粒子生成

Audio Visualizer 串接

1. 設有四個接口，分別對應隨機各半的粒子的尺寸與亮度，使用`Assets\_FeelCue\Script\AudioAnalyzerVFXController.cs`接收來`Assets\Feel\MMTools\Core\MMAudio\AudioAnalyzer\MMAudioAnalyzer.cs` 的資訊
2. 透過設定`AudioAnalyzerVFXController.cs` **上的參數，決定聽取音樂的模式以及處裡接收的數值，主要需要設定的有**
    1. `audioAnalyzerMultiplier`  數值的強度倍率
    2. `audioAnalyzerOffset` 數值的中心點，亮度與尺寸的Offset應為1
    3. `audioAnalyzerLerp`  數值的平滑度，越高越平滑，可設得很高

### V_ParticleScattering_UnlitQuad 不吃光的粒子散佈

設定靜態的基本參數

1. Spawn 分類下設定每秒生成數量、Lifetime、生成範圍、粒子尺寸範圍、顏色、使用貼圖等基本參數
2. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化，以及粒子閃爍頻率
3. Force 分類下設定與速度有關的參數

控制項

1. 建議使用 Animation Track 或 Assets\_FeelCue\Script\VFXPropertiesControl.cs 控制 Spawn Control 參數，達成控制粒子生成

Audio Visualizer 串接

1. 設有四個接口，分別對應隨機各半的粒子的尺寸與亮度，使用`Assets\_FeelCue\Script\AudioAnalyzerVFXController.cs`接收來`Assets\Feel\MMTools\Core\MMAudio\AudioAnalyzer\MMAudioAnalyzer.cs` 的資訊
2. 透過設定`AudioAnalyzerVFXController.cs` **上的參數，決定聽取音樂的模式以及處裡接收的數值，主要需要設定的有**
    1. `audioAnalyzerMultiplier`  數值的強度倍率
    2. `audioAnalyzerOffset` 數值的中心點，亮度與尺寸的Offset應為1
    3. `audioAnalyzerLerp`  數值的平滑度，越高越平滑，可設得很高

---

## 參數解釋

### V_ParticleScattering_LitMesh 吃光的模型散佈

Control

**Spawn Control** - 生成控制項，生成動畫建議由此控制

**Fade Out by Spawn Control** - 是否於 Spawn Control 數值遞減時粒子同步淡出，若不勾選當 Spawn Control 為0時，既存的粒子會持續存在直到生命結束

Spawn

Spawn Rate - 每秒生成數量

Lifetime Range - 粒子生命範圍

Spawn Box Size - 粒子生成範圍，單位為公尺

Particle Size Range - 粒子尺寸範圍

Spawn Mesh - 粒子使用模型

Base Map - 模型使用的 Diffuse 貼圖

Color 1 - 粒子顏色之一，Alpha為透明度，最終結果在與 Color 2 之間隨機

Color 2 - 粒子顏色之二，Alpha為透明度，最終結果在與 Color 1 之間隨機

Smoothness - 模型光滑度

Metallic - 材質是否為金屬

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含Alpha

Force

Initial Velocity - 粒子生成時的初始速度

Rotation Speed Range - 粒子的自轉速度範圍

Turbulence Frequency - 粒子的立場 Noise 頻率

Turbulence Strength -  粒子的立場 Noise 強度

Gravity - 地心引力強度

Vortex Strength - 粒子的螺旋運動強度

Drag - 粒子受到的阻力，配合其他力相關參數做出夠聚集、黏稠的動態

Audio Visualizer

Size Beat 0 - 串接 Audio Visualizer 腳本使用的數值，控制其中一半既存粒子的尺寸變化

Size Beat 1 - 同上，但控制另一半粒子

Color Beat 0 -  串接 Audio Visualizer 腳本使用的數值，控制其中一半既存粒子的顏色強度變化

Color Beat 1 - 同上，但控制另一半粒子

### V_ParticleScattering_UnlitQuad 不吃光的粒子散佈

Control

**Spawn Control** - 生成控制項，生成動畫建議由此控制

**Fade Out by Spawn Control** - 是否於 Spawn Control 數值遞減時粒子同步淡出，若不勾選當 Spawn Control 為0時，既存的粒子會持續存在直到生命結束

Spawn

Spawn Rate - 每秒生成數量

Lifetime Range - 粒子生命範圍

Spawn Box Size - 粒子生成範圍，單位為公尺

Particle Size Range - 粒子尺寸範圍

Base Map - 粒子使用的貼圖

Color 1 - 粒子顏色之一，Alpha為透明度，最終結果在與 Color 2 之間隨機

Color 2 - 粒子顏色之二，Alpha為透明度，最終結果在與 Color 1 之間隨機

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含Alpha

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Force

Initial Velocity - 粒子生成時的初始速度

Initial Velocity Randomize - 粒子初始速度的隨機程度，若設為1，最終初始速度將在0與Initial Velocity 數值之間隨機

Rotation Speed Range - 粒子的自轉速度範圍

Turbulence Frequency - 粒子的立場 Noise 頻率

Turbulence Strength -  粒子的立場 Noise 強度

Gravity - 地心引力強度

Vortex Strength - 粒子的螺旋運動強度

Drag - 粒子受到的阻力，配合其他力相關參數做出夠聚集、黏稠的動態

Audio Visualizer

Size Beat 0 - 串接 Audio Visualizer 腳本使用的數值，控制其中一半既存粒子的尺寸變化

Size Beat 1 - 同上，但控制另一半粒子

Color Beat 0 -  串接 Audio Visualizer 腳本使用的數值，控制其中一半既存粒子的顏色強度變化

Color Beat 1 - 同上，但控制另一半粒子