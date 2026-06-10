# 10_CrowdWave

這是一個模擬手持螢光棒觀眾的效果，包含多種揮動模式以及各種動作的參數調整，並且可以生成於各種自定義的座位模型。可以分別控制左右手顏色或者搭配Render Texture取樣影片的顏色

Notion版本
https://www.notion.so/10_CrowdWave-3788a373d31780138b16f23824169292?source=copy_link
## 使用方式

1. Seat Mesh 指定座位模型，觀眾將會生成在模型頂點（Vertex）之上，因此 Seat Mesh Vertex Count - 觀眾的生成數量需要填入使用模型的頂點數量。
   需要注意的是，Unity和DCC軟體的頂點數量判斷可能會不一樣，因此製作時需要
    1. 保持所有Edge為軟邊
    2. 確保只有一張UV，並且其內容是空的，不要記錄任何頂點資料
    3. 匯入Unity後，確認兩邊的頂點數量相同
2.  Base 之下的參數可以調整基礎的顯示與動畫速度，例如螢光棒大小、左右手間距、揮動速度等等，並且可以調整隨機刪除觀眾的比例
3. Movement 之下的參數可以調整揮動的細節，例如揮動的角度、觀眾的動作一致性、動作模式切換
4. Coloring則控制顏色，左右手的顏色可以分開調整，也可以配合Render Texture取樣影片的顏色，影片是根據物件座標空間的XZ軸Sample的，建議拉一個方塊用拉伸得到觀眾席的長寬，並調整偏移來 Sample 正確位置。影片播放可以參考Prefab使用的內建Videos Player，若想要在Timeline精確控制播放影片可以下載官方的timeline 套件
   https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/samp-custom-samples.html
5. Binding之下的 Target 配合 VFX Property Binder，設定觀眾的面向，可以動態調整目標物件

## 參數解釋

Base

Seat Mesh - 座位模型

Seat Mesh Vertex Count - 座位模型頂點數量，若數量超過4096須進入 VFX 檔案調整 Capacity

Seat Mesh Scale - 座位模型的尺寸，建議保持在 1 建模時就採用正確的尺寸

Stick Size - 螢光棒大小

Wave Speed - 揮動的速度

Gap Between Stick - 左右手的間距

Random Discard - 隨機製造空位的比例

Movement

Wave Angle Range - 揮動的角度範圍

Stick Forward Distance - 螢光棒往下揮時的位置往前偏移

Height Offset - 觀眾高度的統一調整

Random Bias - 每個觀眾隨機的揮動延遲

X Wave Bias - 由左到右的觀眾揮動延遲

X Wave Bias from Center - 將X Wave Bias 從左到右的延遲變成由左右往中央延遲

Z Wave Bias - 由後到前的觀眾揮動延遲

Alternate Height Offset - 揮動每隔一次會增加高度，製造高低交錯的動態

Overhead Wave Switch - 從往前揮動的動態改為高舉過頭的左右揮動

Coloring

Left Color - 右手顏色

Right Color -左手顏色

Crowd Color Map Offset - 顏色圖片取樣的偏移

Crowd Color Map Scale - 顏色圖片取樣的尺寸，即觀眾席的長寬

Crowd Color Map - 顏色圖片

Crowd Color Map Weight - 切換使用左右手顏色或者圖片

Binding

Target - 觀眾指向的目標物