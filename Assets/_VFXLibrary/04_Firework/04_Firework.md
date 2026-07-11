# 04_Firework

包含數組煙火效果，分為單次發射與持續分設兩類，並各有數種變體，其中單次發射有一變體可匯入自訂圖片，令煙火爆開時形狀取樣該圖

Notion版本：https://www.notion.so/04_Firework-d6ecadc081ca427e935b3f613f2fef7e?source=copy_link

---

## 使用方式

### V_Firework_Single_General / V_Firework_Constant_Split

設定靜態的基本參數

1. Parent 分類下設定由地面衝上爆炸高度的粒子，包含尺寸、使用貼圖、顏色以及爆炸高度(公尺)
2. Child 分類下設定爆炸的外觀，包含生命、閃爍、顏色透明度
3. Force 分類下設定地心引力、立場擾動、阻力等參數

控制項

1. 參考 `Assets\_VFXLibrary\02_ParticleBurst\P_FeelCue_ParticleBurst_Confetti.prefab` 配合 MMF VFX Track 於 Timeline 上控制

### V_Firework_Single_PCShape_Bear

設定靜態的基本參數

1. 因為點雲(.pcache)檔案無法設為公開變數，因此每一個圖樣的煙火都需要複製一個檔案，進入VFX Graph替換點雲檔案
2. Parent 分類下設定由地面衝上爆炸高度的粒子，包含尺寸、使用貼圖、顏色以及爆炸高度(公尺)
3. Child 分類下設定爆炸的外觀，包含生命、閃爍、顏色透明度
4. Force 分類下設定地心引力、立場擾動、阻力等參數

控制項

1. 參考 `Assets\_VFXLibrary\02_ParticleBurst\P_FeelCue_ParticleBurst_Confetti.prefab` 配合 MMF VFX Track 於 Timeline 上控制

### V_Firework_Constant_General / V_Firework_Constant_Split

設定靜態的基本參數

1. Spawn 分類向上設定每秒煙火發射數量以及生成粒子範圍
2. Parent 分類下設定由地面衝上爆炸高度的粒子，包含尺寸、使用貼圖、顏色以及爆炸高度(公尺)
3. Child 分類下設定爆炸的外觀，包含生命、閃爍、顏色透明度
4. Force 分類下設定地心引力、立場擾動、阻力等參數

控制項

1. 建議使用 `Animation Track 或 Assets\_FeelCue\Script\VFXPropertiesControl.cs 控制 Spawn Control` 參數，達成控制粒子生成

---

## 參數解釋

### V_Firework_Single_General 單次發射的普通煙火

Parent

Parent Particle Size - 設定爆炸前煙火粒子的尺寸

Target Height - 設定煙火由地面衝上爆炸高度

Parent Base Map - 爆炸前煙火粒子的貼圖

Color Gradient - 煙火粒子的主色，每次發射取樣一個 Gradient 上隨機位置

Child

Child Lifetime - 爆炸後產生的粒子生命長度

Trail Lifetiome - 爆炸後產生的粒子的拖尾粒子生命長度

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Child Base Map - 爆炸後產生的粒子使用的貼圖

Child Hue Variety - 煙火粒子的副色，此顏色來自主色色相的偏移，偏移數值由此數值控制，若為0則副色與主色相同

Trail Alpha -  爆炸後產生的粒子的拖尾粒子的透明度

Force

Burst Speed Range - 爆炸後產生的粒子的初速度範圍

Burst Drag -  爆炸後產生的粒子的阻力

Gravity - 地心引力

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率

### V_Firework_Constant_Split 單次發射的分裂煙火

Parent

Parent Particle Size - 設定爆炸前煙火粒子的尺寸

Target Height - 設定煙火由地面衝上爆炸高度

Parent Base Map - 爆炸前煙火粒子的貼圖

Color Gradient - 煙火粒子的主色，每次發射取樣一個 Gradient 上隨機位置

Child

Child Spawn Radius - 爆炸產生的粒子的分散範圍

Child Lifetime - 爆炸後產生的粒子生命長度

Trail Lifetiome - 爆炸後產生的粒子的拖尾粒子生命長度

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Child Base Map - 爆炸後產生的粒子使用的貼圖

Child Hue Variety - 煙火粒子的副色，此顏色來自主色色相的偏移，偏移數值由此數值控制，若為0則副色與主色相同

Trail Alpha -  爆炸後產生的粒子的拖尾粒子的透明度

Force

Burst Speed Range - 爆炸後產生的粒子的初速度範圍

Burst Drag -  爆炸後產生的粒子的阻力

Gravity - 地心引力

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率

### V_Firework_Single_PCShape 單次發射的圖樣煙火

> 攝影機參數，不須變動，會自動抓取Main Camera，使圖樣自動面向攝影機方向
>

Parent

Parent Particle Size - 設定爆炸前煙火粒子的尺寸

Target Height - 設定煙火由地面衝上爆炸高度

Parent Base Map - 爆炸前煙火粒子的貼圖

Color Gradient - 煙火粒子的主色，每次發射取樣一個 Gradient 上隨機位置

Child

Child Lifetime - 爆炸後產生的粒子生命長度

Trail Lifetiome - 爆炸後產生的粒子的拖尾粒子生命長度

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Child Base Map - 爆炸後產生的粒子使用的貼圖

Child Hue Variety - 煙火粒子的副色，此顏色來自主色色相的偏移，偏移數值由此數值控制，若為0則副色與主色相同

Trail Alpha -  爆炸後產生的粒子的拖尾粒子的透明度

Force

Burst Speed Range - 爆炸後產生的粒子的初速度範圍

Burst Drag -  爆炸後產生的粒子的阻力

Gravity - 地心引力

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率

### V_Firework_Constant_General 持續發射的普通煙火

Spawn

Spawn Control - 發射控制項

Spawn Rate - 每秒發射數量

Spawn Range - 發射範圍

Parent

Parent Particle Size - 設定爆炸前煙火粒子的尺寸

Target Height - 設定煙火由地面衝上爆炸高度

Parent Base Map - 爆炸前煙火粒子的貼圖

Color Gradient - 煙火粒子的主色，每次發射取樣一個 Gradient 上隨機位置

Child

Child Lifetime - 爆炸後產生的粒子生命長度

Trail Lifetiome - 爆炸後產生的粒子的拖尾粒子生命長度

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Child Base Map - 爆炸後產生的粒子使用的貼圖

Child Hue Variety - 煙火粒子的副色，此顏色來自主色色相的偏移，偏移數值由此數值控制，若為0則副色與主色相同

Trail Alpha -  爆炸後產生的粒子的拖尾粒子的透明度

Force

Burst Speed Range - 爆炸後產生的粒子的初速度範圍

Burst Drag -  爆炸後產生的粒子的阻力

Gravity - 地心引力

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率

### V_Firework_Constant_Split 持續發射的分裂煙火

Spawn

Spawn Control - 發射控制項

Spawn Rate - 每秒發射數量

Spawn Range - 發射範圍

Parent

Parent Particle Size - 設定爆炸前煙火粒子的尺寸

Target Height - 設定煙火由地面衝上爆炸高度

Parent Base Map - 爆炸前煙火粒子的貼圖

Color Gradient - 煙火粒子的主色，每次發射取樣一個 Gradient 上隨機位置

Child

Child Spawn Radius - 爆炸產生的粒子的分散範圍

Child Lifetime - 爆炸後產生的粒子生命長度

Trail Lifetiome - 爆炸後產生的粒子的拖尾粒子生命長度

Sparkling Speed - 粒子每秒閃爍的次數，0為不閃爍

Child Base Map - 爆炸後產生的粒子使用的貼圖

Child Hue Variety - 煙火粒子的副色，此顏色來自主色色相的偏移，偏移數值由此數值控制，若為0則副色與主色相同

Trail Alpha -  爆炸後產生的粒子的拖尾粒子的透明度

Force

Burst Speed Range - 爆炸後產生的粒子的初速度範圍

Burst Drag -  爆炸後產生的粒子的阻力

Gravity - 地心引力

Turbulence Strength -  粒子的立場 Noise 強度

Turbulence Frequency - 粒子的立場 Noise 頻率