# 06_Smoke

包含兩個煙霧效果，一是持續瀰漫空間中的煙霧，二是一次噴發數秒的噴射煙柱

Notion 版本：https://www.notion.so/06_Smoke-cbc29174713e402795d7048d6a27690d?source=copy_link

---

## 使用方式

### V_SmokeField

設定靜態的基本參數

1. Spawn 分類下設置每秒生成數量、粒子尺寸、生命時長、貼圖與顏色
2. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化
3. Force 分類下控制粒子的動態

控制項

1. Timeline Animation Track 上控制 Control 分類下的Control 參數控制效果的顯示

### V_SmokeJet

設定靜態的基本參數

1. Spawn 分類下設置每秒生成數量、粒子尺寸、生命時長、貼圖與顏色
2. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化
3. Force 分類下控制粒子的動態

控制項

1. 參考 `Assets\_VFXLibrary\06_Smoke\P_FeelCue_ParticleConstant_SmokeJet.prefab`配合 MMF VFX Track 於 Timeline 上控制

---

## 參數解釋

### V_SmokeField

Control

Spawn Control - 粒子的生成控制項

Spawn

Spawn Rate - 每秒生成數量

Particle Size Range - 粒子的尺寸範圍

Spawn Range - 煙霧生成的XZ軸範圍

Lifetime Range - 粒子的生命時長範圍

Base Map 2x2 - 粒子使用的貼圖，須為2*2的Sprite Sheet，生成粒子將隨機使用其中一位

Color - 粒子顏色

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含 Alpha

Pluse

Radial Velocity - 套用生成範圍中心往外擴散的初速度

Gravity - 地心引力，設為正值將會往上飄

Drag - 粒子所受阻力

Turbulence Strength - 立場擾動的強度

Turbulence Frequency - 立場擾動的頻率

Rotation Speed Range - 粒子自轉速度的範圍

Wind Strength - 粒子受一方向的力，模擬風

Wind Direction - 風的方向，0到1對應0至360度

### V_SmokeJet

Spawn

Spawn Duration - 粒子持續生成時間

Spawn Rate - 每秒生成粒子數量

Particle Size Range - 粒子尺寸範圍

Lifetime Range - 粒子生命範圍

Spread Angle - 粒子噴發的擴散角度，範圍將重建為0至180度

Spawn Mesh - 粒子使用模型

Along Velocity - 勾選此項使粒子指向其速度方向

Base Map - 模型使用的 Diffuse 貼圖

Color - 粒子顏色

Muzzle Spawn Rate - 噴射口的氣流粒子每秒生成數量

Muzzle Radius - 噴射口的氣流粒子的半徑尺寸

Muzzle Length - 噴射口的氣流粒子的長度

Lifetime Animation

Size Over Lifetime - 模型生命期間尺寸變化

Color Over Lifetime - 模型生命期間顏色變化，含Alpha

Force

Initial Velocity - 粒子生成時的初始速度

Speed Randomize - 個別粒子的初始速度隨機程度，設定0為全部粒子等速，1為初始速度1倍至0倍之間隨機

Gravity - 地心引力強度

Drag - 粒子受到的阻力，配合其他力相關參數做出夠聚集、黏稠的動態

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率

Rotation Speed Range - 粒子的自轉速度範圍