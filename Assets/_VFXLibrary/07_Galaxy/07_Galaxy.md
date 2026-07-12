# 05_SciFiGrid

這是一個科幻風格的大型環境氛圍效果，主體是矩陣粒子網格，不斷有隨機位置的波紋掠過且帶來上升的子粒子效果，Prefab 加入了一個 01_ParticleScattering 增加層次

Noition 版本：https://www.notion.so/05_SciFiGrid-201c28986995491d9603b12a348a9dbf?source=copy_link

---

## 使用方式

### V_05_SciFiGrid

設定靜態的基本參數

1. Grids 分類下設定粒子使用的貼圖以及顏色
2. Grids 分類下設定粒子矩陣的行列數以及間隔
3. Grid 分類下透過曲線控制矩陣的根據位置(中央至邊緣)基礎高度變化
4. Wave 分類下設定粒子矩陣的高度擾動
5. Pluse 分類下設定隨機出現的波紋
6. Child 分類下設定子粒子的閃說速度

控制項

1. Timeline Animation Track 上控制 Control 分類下的Control 參數控制效果的顯示

---

## 參數解釋

### V_05_SciFiGrid

Control

Control - 整體效果的顯示控制

Grid

Particle Texture - 粒子使用的貼圖

Particle Color - 粒子的顏色

Particle Size - 粒子的尺寸

Column - 粒子矩陣的欄數，若改變須重播粒子系統

Row - 粒子矩陣的列數，若改變須重播粒子系統

Bend from Center - 此曲線控制矩陣的根據位置(中央至邊緣)基礎高度變化，可根據場景需求調整。數值單位為公尺

Particle Space - 行列之間的間距

Wave

Noise Strength - XZ平面的2D高度擾動的強度

Niuse Speed - XZ平面的2D高度擾動的移動速度

Noise Scale - XZ平面的23高度擾動的尺寸

Pluse

Pluse Color - 波紋掠過時粒子的顏色

Pluse Wave Strength - 波紋造成的高度變化強度

Pluse Range - 波紋的最大尺寸

Pluse Width - 波紋的寬度

Pluse Speed - 波紋掠過的速度

Child

Sparkling Speed - 子粒子閃爍的速度，0為不閃爍