# 場景資源與完整製圖缺口審查

日期：2026-11-17。這份審查以 `deliverables/map` 的實際檔案與既有唯讀解碼器為依據，沒有啟動遊戲、修改原地圖、改寫配套檔或使用另一個 REA session。遊戲端的新原生證據由主調查報告負責。

可行性判斷：現有資料足以建立具備實際場景預覽、碰撞與點／範圍編輯的工具，但尚不足以宣稱能從空白製作任意完整地圖，或逐像素重現遊戲畫面。現有 `MapScene` 適合作為初版視覺參考；正式場景編輯器應把 LYR/TIL/BAC 的完整資料與無損 writer 納入共用核心，再以原生載入與實圖驗證補足語意。

## 可重跑證據

```powershell
python -B -X utf8 tools/map_editor/audit_scene.py `
  --maps deliverables/map `
  --output reports/map-editor-20261117/scene-corpus.json `
  --preview reports/map-editor-20261117/scene-preview-approximation.png
```

`audit_scene.py` 是唯讀統計工具，沒有第三套可編輯 MDN model 或 writer。`scene-corpus.json` 保存每張 MDN SHA256、配套檔 SHA256／封裝、每層 LYR 邊界統計及異常物件原位元組樣本。它只驗證其實際讀取的欄位、長度與壓縮邊界，並不證明未知欄位語意。預覽重用既有 `MapScene`，圖中已標明 STATIC APPROXIMATION；沒有將近似畫面當成遊戲驗收。

本次 corpus 僅掃描 `deliverables/map` 根層 `*.md3`，不宣稱涵蓋其他客戶端、封裝 DAT、遞迴子目錄與執行中資源。702 個同副檔名輸入中，700 個為 MDN 世界地圖；`map_mn05a.md3`、`map_mn05b.md3` 為非 MDN，正確排除。

## 實際盤點

| 項目 | 觀察 |
|---|---|
| MDN 版本 | 301：6 張；302：456 張；303：238 張 |
| MDN Param4（header index4／offset0x10） | 700 張全為0；沒有內嵌 NORI 分支的 corpus 覆蓋 |
| MDN Param3 | 0：331 張；1：369 張；既有 reader 將它用於 bitmap RLE 選擇 |
| LYR 結構 | 699 份存在，共3,338層；kind0=2,194、kind1=209、kind2=935 |
| LYR a／b | 全3,338層均為100／100；其他值未受 corpus 驗證 |
| LYR order | 實際0～7；463張地圖的檔案表序不等於 order 遞增 |
| LYR 場景物件 | 共357,397筆；301之48-byte=2,358筆；302／303之56-byte=355,039筆 |
| 場景物件 flags | 357,388筆為0；9筆非零，分布於5張圖 |
| 56-byte物件尾8 bytes | 353,461筆為0／0；1,578筆非零，原生有取用而完整語意仍待確認 |
| 非零物件Z | 僅 corpus 觀察到3張、6筆，不能據此認定其他圖不會需要高度 |
| tile group為0 | 97張；不應讓無條件開啟 TIL 成為碰撞編輯的前置要求 |
| EFF | 存在155份，全部 XML 根節點為 CAKEWAPPER |
| MDN effect records | 194張含記錄，共2,124筆；與 EFF 的存在性不是一對一 |
| 不完整配套 | `map_sq04_01(1).md3` 缺LYR／TIL／BAC；其餘699張均有這三種配套 |
| 本審查結構錯誤 | 0；不含上述缺件，亦不代表畫面已相容 |

LYR 301／302／303 的 stride 判斷沿用既有 reader，並以各層物件尾端精確吻合下一層起點／檔尾檢查。本次所有存在的 LYR 均吻合；這是結構觀察，不是對每一欄位的原生語意證明。

303 的238份 LYR 全為 `D0 C0 00 00` 封裝、238份 BAC 全為 `B0 A0 00 00` 封裝；TIL中221份為 `F0 E0 00 00`、17份raw。301／302存在的配套均為raw。不可單憑303就把所有檔案或 MDN body 都強制當作 zlib。精確分布保存在 JSON。

### Tile 高位旗標

僅統計 low16 tile index 不等於65535的非空格，避免把空白 sentinel 當成變換旗標。非空格共4,712,883；其中107,034格帶非零高8位，分布於186張地圖。

| 高8位十六進位值 | 非空格數 |
|---|---:|
| 0x00 | 4,605,849 |
| 0x01 | 9,111 |
| 0x02 | 758 |
| 0x03 | 881 |
| 0x10 | 54,219 |
| 0x20 | 2,579 |
| 0x30 | 39,486 |

目前 `map_scene.py:145`～`:149` 偵測後只發近似警告，`:201`～`:204` 只使用 low16 tile index 與 next8 group index，沒有套用高8位。本corpus統計本身不能證明各旗標語意；須和下述原生證據合用，再用四角異色、非對稱 tile 逐旗標驗證。

主調查本輪保存的[REA擴展證據](native/rea-expanded.json)已補上部分原生用途：`0x57df70`檢查高byte的`0x10`，並為相關格建立per-vertex資料，初始化其中浮點量時有亂數呼叫；`0x57f670`／`0x57fae0`檢查`0x20`後把四個顏色參數設為`0xa0ffffff`。對應Evidence為`ev_38dcbfcd125fc552c8c0b30b0f5ce1f841ec2bc739267d2e689d74cced82e005`、`ev_4ff62a21d7da57282f47b7881318abf06e5d38f47e204fbaab05a311116ff4a3`、`ev_52335b20c6ef221b47a9fdb83cc8d5a5cafbfcca361653dcc30b704ea66b4f83`。此為保留原位址的合成PE靜態反編譯；實際執行、時間更新公式與GPU最終畫面仍未觀察。不能僅因亂數per-vertex資料便將`0x10`命名為已確證的波浪效果，亦不能把`0x20`簡化為已驗證的最終透明度。主調查後續已由原始組語確認mask 0x01／0x02分別交換水平／垂直邊界，見[主報告](REPORT.md)與sprite_flag_bounds.asm.txt；lighting路徑的完整變換與最終像素仍待驗證。

### 不能丟棄的 LYR 物件資料

`map_scene.py:157`～`:161` 讀取 flag／X／Y／Z／32-byte名稱後只留下 `(X, Y-Z, name)`，並按轉換後Y排序；56-byte物件尾8 bytes亦未保留。此做法可供靜態背景，不能當作 LYR writer 的model。

非零flag分布：`map_fd05g`2筆、`map_fs05`2筆、`map_mn02`1筆、`map_sq04_07`3筆、`map_sq07`1筆。某些值不是小型位元旗標，額外尾段也含類似偵錯填充值／過去記憶體數值的模式。**這只是形狀推測**；不將它們命名為旋轉／透明／指標，也不以「垃圾資料」為由清零。新 writer 至少須逐byte保存它們。

主調查本輪[第一輪REA證據](native/rea-pass1.json)的`0x57b960`／`0x57be90`／`0x57c090`確認版本302／303會讀取尾8 bytes；第一個uint32大於4時會在載入後設為0。對應Evidence為`ev_cbb5c36af591a53b4ebf10656f72530ad1fa35525cb3b9b544354120885d86cb`、`ev_85cd9055d424679d5f865b589c28e41a2cc8b00cbaf0ab3eacce9e04ef37c1ac`、`ev_56821a7d1d1b71942b63f988a2fdb03adfe25f07abeff661fd7c2bd37eb0dd2d`。因此尾段不能整體視為不使用的padding；載入後的clamp也不構成無損writer改寫原檔的理由。0～4各值與第二個uint32的完整後續用途尚未在本子審查驗證。

另外，既有 reader 讀出的 `order` 未保留至 layer dictionary（`:135`／`:138`），render依檔案表序繪製（`:185`）。463張圖的表序並非order遞增說明兩者不能無條件混同；同樣不能立即改成按order排序，因其原生用途尚待證據。

### EFF 與外部材質依賴

155份 EFF 都是 CAKEWAPPER XML，不是另一份52-byte MDN效果記錄。194張有 MDN effect records 的地圖中40張無同名EFF；1張有EFF卻沒有MDN effect records。因此「MDN effectCount>0便要求EFF」或「有EFF便自動產生地圖效果」皆未被證明。

依本工具明確政策，將 `data/map/`（允許前導斜線）對應 corpus、相對檔名以EFF同目錄解析、絕對作者機器路徑不重映射，有107份EFF的NoriFileName無法解析到 corpus 檔案。例子包括 `D:/Datasafe/Trickster/data/effect/cream.nri`、`F:/datasafe/Trickster/data/effect/snowhill.nri` 與 `data/map/moonlight.nri`。這是**corpus路徑解析失敗**，不是已觀察到遊戲缺材質；遊戲可能採用另一根目錄／重映射／fallback，須追查原生資源管理器。完整引用與政策均存 JSON，不應在匯出時默默替換為任意同名圖片。

現有 `effect_editor/bac.py:36` 能處理 CAKEWAPPER XML與未修改區域保真；`assets.py:75`按文件的NoriFileName解析材質，可供後續模組重用。但地圖中的實例生命週期、觸發時間、sort channel、世界位置與地形遮擋仍須接回地圖場景，不能只把整份EFF丟進特效工作室便稱完成地圖效果。

## 既有預覽的適用範圍

已對產出的 `scene-preview-approximation.png` 做目視檢查：map_sq00 的地磚、建築與場景物件能組成合理畫面，標示清楚且沒有將警告隱藏。它只證明此時此處的離線合成可用，沒有與遊戲截圖逐像素比對。

尚有這些與製圖直接相關的限制：

- NORI只畫第一影格，沒有場景動畫時間／播放狀態（`map_scene.py:214`）。
- 忽略高8位tile旗標、LYR物件flag與尾8 bytes；名稱以cp949且errors=replace解碼，不能再用此字串還原原始32 bytes。
- scene採用Y-Z近似位置與檔案表序合成，沒有保證角色、場景物件、地圖效果的原生前後遮擋。
- TIL／LYR的RGB555底向上解碼、洋紅色鍵與bitmap RLE已有離線reader；GPU filter、alpha test、blend與材質狀態仍須獨立驗證。
- 沒有 LYR/TIL/BAC writer，沒有匯入新美術後的RGB555量化、tile分割／去重、offset重建、bitmap RLE重編碼、空白tile與group上限檢查。
- 碰撞／地形／高度資料、Point／Range和場景圖層是不同資料域，不能因畫面存在道路便自動推定可走或出生點有效。

## 伺服器端線索與驗證邊界

此部分重用既有 `reports/monster-system-20261117` 的原生靜態研究，**未重新執行REA，亦未將它誤稱本輪新執行證據**。`MAP-SPAWN.md:51`的既有證據顯示：

- GameServer `0x483320`依Region取點，`0x45BBF0`從區域點組隨機取點，`0x45BAD0`處理個別點ID；該函式Evidence為 `ev_83a21afc84cc8c02ede9e1656a8841f9043e6141f2dfe7ed1c85355504079f52`（`evidence/rea-45bbf0.json`），分析對象是保留原位址的server-stat-geometry.exe合成PE，覆蓋限制應隨原報告保留。
- 找不到Region會經 `0x4830F0`取預設／首個點，最後可能fallback到(600,400)。因此錯誤Region不一定完全不出怪，可能集中到錯位。
- `MapInfoEx.MonsterTable`／BossMonsterTable才將出生設定掛入地圖；編輯MD3不會自動建立Mon／BossMon表或NPC對話。
- 出生點與可活動邊界要分別驗證；伺服器怪物出生、高度與可走位置的消費者仍需專門地圖載入調查，不能由客戶端預覽推論全部相容。

若要交付「完整可玩新地圖」，匯出檢查需涵蓋 MapInfoEx路徑／ID、client與server MD3一致性、Point／Range ID引用、NPC位置／對話、Mon／BossMon Region、傳點與回程、重生／復活點、地形與鑽地／寶圖點，而不只四個場景檔。

## 後續實作與驗收關卡

| 階段 | 應完成的程式能力 | 通過條件 |
|---|---|---|
| 1. MDN核心 | 保真load/save、未知尾段、count／bounds、原子保存、碰撞筆刷與undo | 700 MDN no-op bytes相同；修改只影響指定資料；截斷不覆寫來源 |
| 2. 場景model | 在共用Core建立LYR/TIL解析與writer；保留原始名稱／全部flags／未知bytes | 699 LYR和同名TIL/BAC保真往返；301／302／303、raw／封裝、kind0/1/2全覆蓋 |
| 3. 原生場景預覽 | tile flags、圖層order、物件位置／動畫／遮擋、色鍵與GPU狀態 | 各flag非對稱測圖、前後景與高度案例逐像素／容許差異比較；未支援明確拒絕或標示 |
| 4. 場景製作工具 | tile畫筆／填色、素材庫、圖層順序、物件放置、選取／群組／undo、縮放與邊界 | 各操作export→重開model相同；未編輯資料不變；歷史操作還原一致 |
| 5. 新圖資源管線 | RGB555匯入、palette／透明、tile group／offset重建、LYR空間尺寸、BAC動畫與EFF依賴 | 空白圖→自製地磚／物件／效果→載入器成功；路徑與資源manifest無歧義 |
| 6. 可玩性整合 | MapInfoEx、NPC／怪物／傳點／復活／鑽地資料驗證、client/server成套匯出 | 隔離環境進圖、走路、擋路、角色高度、動畫／遮擋、換圖／回程、出怪／重生均實測 |

第1階段能直接在本輪現有專案上實作；第2～6階段仍有明確未完成項。「能做」與「已完美」必須以此分開，所有關卡實際通過後才可提高相容性承諾。
