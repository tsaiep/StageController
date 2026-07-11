# 02_ParticleBurst

這是通用的單次粒子噴發效果，噴發類型包含 Lit/Unlit Mesh/Unlit Billboard Particle 以及一個可設定持續噴發數秒的Lit Particles 變體，未來可依照需求再修改更多噴發類型。可自行替換模型與貼圖對應不同的主題與風格


Notion 版本：https://www.notion.so/02_ParticleBurst-e2454d4edca945e99a40d3e6416d9e35?source=copy_link

---

## 使用方式

### V_ParticleBurst_LitMesh / V_ParticleBurst_UnlitMesh / V_ParticleBurst_UnlitParticle 瞬間噴發的類型

設定靜態的基本參數

1. Spawn 分類下設定生成數量、Lifetime、生成範圍、粒子尺寸範圍、顏色、使用貼圖模型等基本參數
2. Spawn 分類下設定Spread Angle設定噴發擴散角度
3. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化
4. Force 分類下設定與速度有關的參數

控制項

1. 參考 `Assets\_VFXLibrary\02_ParticleBurst\P_FeelCue_ParticleBurst_Confetti.prefab` 配合 MMF VFX Track 於 Timeline 上控制

### V_ParticleConstant_LitMesh 持續噴發數秒的類型

設定靜態的基本參數

1. Spawn 分類下設設定 Spawn Duration 設定噴發時長
2. Spawn 分類下設定每秒生成數量、Lifetime、生成範圍、粒子尺寸範圍、顏色、使用貼圖模型等基本參數
3. Spawn 分類下設定Spread Angle設定噴發擴散角度
4. Lifetime Animation 分類下設定粒子生命期間的尺寸與顏色變化
5. Force 分類下設定與速度有關的參數

控制項

1. 參考 `Assets\_VFXLibrary\02_ParticleBurst\P_FeelCue_ParticleConstant_Confetti.prefab` 配合 MMF VFX Track 於 Timeline 上控制

---

## 參數解釋

### V_ParticleBurst_LitMesh

Spawn

Spawn Count - 生成粒子數量

Particle Size Range - 粒子尺寸範圍

Lifetime Range - 粒子生命範圍

Spread Angle - 粒子噴發的擴散角度，範圍將重建為0至180度

Spawn Mesh - 粒子使用模型

Base Map - 模型使用的 Diffuse 貼圖

Color Gradient - 個別粒子顏色於此漸層上隨機取樣

Smoothness - 模型光滑度

Metallic - 材質是否為金屬

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

### V_ParticleBurst_UnlitMesh

Spawn

Spawn Count - 生成粒子數量

Particle Size Range - 粒子尺寸範圍

Lifetime Range - 粒子生命範圍

Spread Angle - 粒子噴發的擴散角度，範圍將重建為0至180度

Spawn Mesh - 粒子使用模型

Base Map - 模型使用的 Diffuse 貼圖

Color Gradient - 個別粒子顏色於此漸層上隨機取樣

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

### V_ParticleBurst_UnlitParticle

Spawn

Spawn Count - 生成粒子數量

Particle Size Range - 粒子尺寸範圍

Lifetime Range - 粒子生命範圍

Spread Angle - 粒子噴發的擴散角度，範圍將重建為0至180度

Along Velocity - 勾選此項使粒子指向其速度方向

Base Map - 模型使用的 Diffuse 貼圖

Color Gradient - 個別粒子顏色於此漸層上隨機取樣

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

### V_ParticleConstant_LitMesh