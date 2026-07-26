# 11_Emissive Lightstrip FX

這是模擬燈條/燈泡群體動態的特效系統，以自發光材質為基底，以 Tiemline Custom Track 控制，並且包含模板功能

Notion 版本：
https://www.notion.so/11_Emissive-Lightstrip-FX-37c8a373d31780678397fbc8b8c237ac?source=copy_link

---

# 一、模型UV規範

1. 發光動態使用UV1
2. UV1排列方式，假設有三個燈泡，按照想要燈光流動的方向，將第一個排列在UV切分為為8*8方格的左下第一格(0~0.125, 0~0.125)，二號則是(0.125~0.2.5, 0~0.125)。若超過8組，將第11位y軸座標增加0.125
3. 若有平滑連續的燈條，把UV攤平並等根據流動方向距排列，Y軸Sclale可以適度縮小，避免剛好切到每一列的上下邊緣

---

# 二、快速使用

1. 可使用Asset/ _VFXLibrary/ 11_Lightstrip/ P_LightstripGroup 為基底製作Lightstrip燈組
2. 首先在核心腳本 LightstripMBPControl 把要一起控制的模型填入List，並填入各成員有幾個單元
3. 於 Timeline 上建立Lightstrip Control Track，填入有 LightstripMBPControl 的物件，右鍵增加Clip
4. Clip 的Inspector 最上方有模板選擇的按鈕，點擊會開啟模板選擇視窗
5. 模板選擇視窗可以查找並預覽模板。Apply Template to Clip 按鈕可以把選擇的模板數值套用到 Clip 之上，Ping 按鈕可以查找模板在資料夾的位置。
6. Apply Option 可以分別套用模板不同的分類數值
   - Apply Manual Mode Setting 負責 Manual Mode 分類下兩項數值
   - Apply Color Setting 負責Color分類下三項數值
   - Apply Animation Setting 負責其餘數值
7. 在Timeline上排列Clip可以編輯Lightstrip的自發光動畫，兩個Clip混合時，自發光會在Weight = 1~0.5 持續採用第一個 Clip 的設定，但逐漸黯淡直至全黑；Weight = 0.5~0 時改採第二個 Clip 的設定，並且發光強度逐漸從全黑到全亮（此處 Weight 意指第一個光的權重）

---

# 三、模板編輯與使用

1. Clip最底下有一匯出模板按鈕，點即可將目前模板的數值儲存成模板
2. 若要手動建立模板，於Create/ Stage Control/ Lightstrip Template 建立
3. 模板最上方可編輯 Tag，供模板選擇視窗查找
4. 建立Tag，於Create/ Stage Control/ Lightstrip Template Tag 新增，Tag 名即為檔名
5. 目前建立的模板路徑為 Asset/ _VFXLibrary/ 11_Lightstrip/ P_LightstripGroup / Template

---

# 四、結構
1. 最底層是Shader，燈光邏輯和控制項都在這裡被設計好
2. MonoBehaviour 腳本使用 MPB 覆蓋Shader的參數，以此指定操控對象與控制燈光表現
3. Custom Timeline Track 控制 腳本上的數值，達成在時間軸上控制不同的表現，並使管理方便
---

# 五、參數細節

## Manual Mode

### Manual Mode

預設不開啟

- Off: 根據 Scrolling Speed 與 Sparkling Speed 自動循環撥放
- On: 動畫播放進度根據 Manual Mode Control 曲線控制

### Manual Mode Control

Animation Curve。X 軸代表 clip 內的正規化時間：

- `0`：clip 開始
- `1`：clip 結束

曲線輸出會控制動畫播放進度的位置。會對曲線值做 repeat 處理，因此超過 `1` 的值會循環回 `0-1` 範圍

---

## Color

### Color

Lightstrip 的主要顏色

### Color Multiplier

顏色亮度倍率

### Gradient

Lightstrip 使用的漸層

---

## Animation Control

### Linear Mode

預設不開啟。須配合「一、模型UV規範」

- Off: 顏色與動畫進度分為 10*10 的Step
- On: 顏色與動畫進度平滑連續

### Scrolling Mode Weight

控制流動模式的權重

### Scrolling Ping Pong Mode

預設不開啟。用於控制流動方向是否以 ping-pong 方式往返

### Scrolling From Center

預設不開啟。用於控制 scrolling 是否從中心向外展開

### Sparkling Mode Weight

控制閃爍模式權重

### Sparkling Mode Random Weight

控制閃爍隨機性的權重。數值越高，sparkling 分布越偏向隨機

---

## Scrolling

### Scrolling Speed

控制 scrolling 動畫速度。數值越高，流動越快

### Scrolling Frequency

控制 scrolling 重複頻率。數值越高，單位距離內的流動段落越密集

### Scrolling Interval Duration

控制 scrolling 循環中全暗的比率

### Scrolling Hold Duration

控制 scrolling 循環中全亮的比率

### Scrolling Head Lean

控制 scrolling head 的傾斜方向與強度

### Scrolling Smooth Factor

控制 scrolling 循環中黑白漸層的滑順過度，藍色為設定為0的過度曲線；紅色為設定為1的過度曲線

!image.png

---

## Sparkling

### Sparkling Speed

控制 sparkling 動畫速度

### Sparkling Smooth Factor

控制 sparkling 的平滑程度。數值越高，閃爍變化越柔和，曲線同 Scrolling Smooth Factor

---

# 六、Lightstrip燈具合成工具

這是一個將複數模型轉換成 Lightstrip 系統使用的模型的工具，生成的模型將會帶有符合規範的UV1（限於非平滑過度的模型UV規範）

## 1. 介面

於上方欄 Window/ Stage Control/ Mesh UV Combiner 開啟工具視窗

## 2. 使用

1. List代表要合併的模型和排列順序，將場景模型填入此欄位
   1. 可以在 Hierarchy 複選物件，點選 Add Selected Mesh Object 會將選取物件一次新增到 List 之上
   2. 按下 Remove Null / Duplicates 會移除空欄位以及重複指定同一物件的欄位
2. Lightstrip Material 指定要套用的材質
3. Asset Path 指定生成檔案的儲存路徑
4. UV Padding 每一分割格的安全邊界，應不需調整
5. 按下 Generate 後，會生成一份符合Lightstrip 系統U規範的 .mesh 檔案，同時放入一份進入當前場景，位置將與來源模型重合同時套用指定的材質