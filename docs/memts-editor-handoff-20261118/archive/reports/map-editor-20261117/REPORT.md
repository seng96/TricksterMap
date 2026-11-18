# TricksterMap × 原生地圖讀取：可行性與編輯器初版

日期：2026-11-17。

**可以沿 TricksterMap 實作完整地圖編輯器；本輪已完成可啟動的碰撞／點／範圍編輯初版，以及避免毀損原檔的核心修正。目前仍不能宣稱能從空白製作任意地圖，或與遊戲畫面完全一致。**

## 交付

- [可直接啟動的編輯器](../../deliverables/map-editor/0.1.0/TricksterMap.exe)，需保留同目錄 DLL。
- [完整 ZIP](../../deliverables/map-editor/TricksterMap-editor-0.1.0-win.zip)，含工具、CLI、原始碼快照、補丁與授權，未附遊戲素材。
- [操作說明](../../deliverables/map-editor/0.1.0/EDITOR_PROTOTYPE.md)。
- [本地來源與重跑方式](../../tools/map_editor/README.md)、[上游補丁](../../tools/map_editor/trickstermap-editor.patch)。
- [原始碼審查](upstream-audit.md)、[場景資源審查](scene-audit.md)、[REA索引](native/index.json)。

基線為 [seng96/TricksterMap](https://github.com/seng96/TricksterMap/tree/1d56fad372e428ccb1123f42a0795b31a9c306f0)。上游已有點／範圍編輯、碰撞顯示及 tile atlas 顯示，但 README 的完整場景與碰撞編輯仍列為計畫。實際 GUI 專案已參照 Core；留在舊目錄的 loader/model 副本沒有被編譯。本次依真實 compile 清單修改 Core，並修正陳舊 AGENTS 說明，沒有再造第三套可編輯 MDN model。

## 本次已實作

1. **MDN 保真讀寫**：保留未知尾段與未編輯欄位；不再於無修改另存時重算原始 aggregate。結構增減只按 MDN 長度差額調整，這是保守保存政策，並非宣稱所有 header 欄位語意已還原。
2. **保存防護**：exact read、負數／溢位／截斷／尺寸檢查；不支援的版本與內嵌資源變體明確拒絕。先完整序列化，再同目錄暫存、Flush、File.Replace/Move，避免先截斷原檔。
3. **碰撞編輯 GUI**：0／1 筆刷、拖曳補線、每筆畫復原重做、縮放與捲動、格座標／原值顯示。值 2、3 及其他未知值分色保留，不在開檔時正規化。
4. **點／範圍編輯修正**：CLI 與 GUI 更新時保留原 Options；關閉對話框取消，不再因按 X 自動套用。
5. **Save As bundle**：以新路徑匯出 MD3，逐 byte 複製同名 BAC／LYR／TIL 和來源存在的 EFF；拒絕覆蓋來源或任何既存輸出。一般失敗清理本次輸出。無 tile group 的地圖可開啟。
6. **CLI**：新增 `config list|set|rect`，使用原始零起算格索引，矩形含兩端，raw 值限 0..255；越界、反向矩形和錯誤參數不寫檔。

碰撞格 0／1 的工具標籤沿用現有格式知識；本輪驗證的是資料結構與編輯保存，尚未以遊戲角色實際走動證明每個情境的碰撞判定。GUI 當前不顯示可編輯的完整場景，tile atlas 不是地圖合成視圖。

匯出是多檔 staging／移入及例外清理，**不提供斷電時整套原子交易**；EFF 內引用的外部 NRI／MD3 尚未自動收集。也未修改 MapInfoEx、NPC、怪物、伺服器或原始遊戲地圖。

## 驗證結果

| 驗證 | 結果與證據 |
|---|---|
| 完整 Release build | Core net48／net10、CLI、WinForms GUI、Tests 全部成功，0 warnings／0 errors |
| 自動測試 | [47／47通過](tests/map-editor-final.trx)，含範圍檢查、Options 保留、未知資料、筆畫歷史與匯出 |
| 真實 MDN 無修改另存 | [700／700逐 byte 相同](corpus-roundtrip-final.json)，301=6、302=456、303=238；非 MDN 的2檔排除 |
| 未知 MDN 尾段 | 32張有8～600 bytes，全部保留；舊上游會丟棄 |
| 封裝版 GUI | [map_sq00](package-smoke-sq00/verification.txt)及[無tile group的mapg02](package-smoke-no-tiles/verification.txt)通過 MDI 開圖、真正筆刷 handler、undo／redo、匯出重開、來源及配套 SHA 比對 |
| 封裝完整性 | [33檔SHA、來源ZIP CRC、新CLI實際修改](delivery-verification.json)通過；一格修改僅改該byte，原始來源不變 |
| 視覺檢查 | 已目視檢查[封裝版碰撞畫面](package-smoke-sq00/collision-editor.png)與[離線場景近似](scene-preview-approximation.png)，兩者都不是遊戲內逐像素驗收 |

GUI 驗證於透明、離屏且不啟用的視窗執行，不注入系統輸入；DrawToBitmap 保存 UI 證據。CLI 整合測試已修正舊 runner 固定啟動 Debug 的問題，最終 Release 測試使用對應 Release CLI。

## REA 原生證據與結論

原始客戶端：`bin/memts1.bin`，SHA256：

```text
4bb6843be21f5bd52d3e61e4d3a036a8cd49d2ac9f8af47401f81f7dc80dfd43
```

原檔 REA／Ghidra 完整自動分析超過330,000 ms啟動時限，沒有將未完成分析當作可用結果。改以原檔位元組／literal anchors定位，再建立保留原 VA 與選定位元組的非執行分析PE。三個載體及逐範圍 SHA／來源offset保留於 `native/*.ranges.json`；`build_carrier.py --pass1`／預設／`--render`可重建。第三版另外用合成export種子告知獨立定位的函式入口；`analysis_seed_*`不是原始符號。

REA 覆蓋的是選定載體。未複製的外部函式、global 初始化、間接呼叫與執行中狀態仍是未知；不能把 decompiler 的 recovered signature 當作原始 ABI。主要 dossier 均報告 callee coverage partial。重要欄位與控制分支已對照原始 Capstone 組語，原始碼與位址均只適用此 SHA。

| 問題 | 靜態觀察／推論邊界 | Evidence |
|---|---|---|
| MDN 主體是否因303壓縮 | `0x57c600`讀96-byte header、驗MDN_，接著依count讀config／range／point／effect；主體沒有依303解壓。301以上range=32、point=24、effect=52 bytes | `ev_046dd390aa907b370f282927541e86712b196813a8ecf860bcc522ef904cd84d` |
| Config格儲存 | `0x57a960`讀5個int、按X×Y配置及讀入bytes；支持現有row-major byte model的長度，不等同全種類語意已知 | `ev_ac46c601bf2ad706cbbb4527cba8d12b0d8385d5566708b6145dd6419a2c6866` |
| 內嵌資源變體 | MDN offset0x10的bit0會在config前呼叫`0x56bc20`；本corpus全為0，故reader在`Param4 & 1 != 0`時拒絕，避免錯位編輯 | 同MDN dossier |
| 配套壓縮版本 | `0x57cbc0`／`0x57cdc0`在version>=303選壓縮路徑，LYR／TIL wrapper分別驗0xC0D0／0xE0F0；BAC wrapper驗0xA0B0 | `ev_db7a2c6c06f57c065525a3dd4ea8ca8af71b0502db41a40b1e400d79332c246a`、`ev_bb8d90a00239c2e50f9f43a40ed95c78d03d1fe6f75b740d5481c4b597867adc`及第一輪bundle |
| LYR物件48／56 bytes | 版本>301額外讀兩個uint32，第一值>4會在記憶體clamp到0；這兩欄不是可以直接省略的padding | `ev_cbb5c36af591a53b4ebf10656f72530ad1fa35525cb3b9b544354120885d86cb`等，見[原始反編譯](native/decompiled/lyr_legacy.c) |
| Tile word | `0x57f670`低16=tile index，0xffff為空；中間8=group；高8傳入sprite draw並另處理0x20 | `ev_f07b0e4398966e8d68f9b33bdd2eefbf5de46a52d81d1c7a7b3bb6478b93b8b0` |
| 高byte mask 0x01／0x02 | `0x579a40`的bit0交換left/right參數，bit1交換top/bottom；對應水平／垂直翻轉。lit路徑另有頂點對換，不能只套單一通用圖片操作就聲稱完整等價 | `ev_08ec77964f198042ec674221703eae68379d9e980e6a753c2a4d07e63db8034b`；[原始組語](native/sprite_flag_bounds.asm.txt)的0x579a81與0x579aa1 |
| 高byte0x10 | `0x57df70`掃描此bit，配置(X+1)×(Y+1)的12-byte per-vertex資料、設選取頂點及亂數初值；draw再取頂點offset。尚未完整追蹤時間更新公式，不命名為已完全確證的水波 | `ev_38dcbfcd125fc552c8c0b30b0f5ce1f841ec2bc739267d2e689d74cced82e005` |
| 高byte0x20 | 一般／tint tile draw將四角顏色設為0xa0ffffff；其他lighting分支有不同處理。這是已觀察的顏色狀態寫入，不保證最終畫面單純乘160/255透明度 | `ev_4ff62a21d7da57282f47b7881318abf06e5d38f47e204fbaab05a311116ff4a3`、`ev_52335b20c6ef221b47a9fdb83cc8d5a5cafbfcca361653dcc30b704ea66b4f83` |
| 場景排序 | `0x57f140`依runtime物件的+0x80／+0x84等狀態排序，`0x580730`另處理角色／場景遮擋關係；現有MapScene單純Y排序不足，尚未完整對應LYR.order與runtime欄位 | [排序反編譯](native/decompiled/object_sort.c)、`ev_01764c67e92d01701efe9607a8a38336acbd52d6a1a2a094e6abab5f9eabdca1` |

原始名稱不確定的欄位仍保留原值，不用推測改寫檔案。

完整 REA 記錄：[pass1](native/rea-pass1.json)、[expanded](native/rea-expanded.json)、[render與dossiers](native/rea-render.json)。[index.json](native/index.json)列各反編譯與對應載體SHA，原始檔補充證據見[original-byte-evidence.json](native/original-byte-evidence.json)。pass1的初始選取路徑後來重用，該載體以原SHA另存`client-map-pass1.exe`，不能只按檔名辨識Evidence。

## 為何現在還不能稱為「完美製圖」

實際盤點700 MDN、699 LYR、3,338圖層與357,397個場景物件，結構檢查通過。但：

- 186張圖有107,034個非空tile使用未模擬高byte，值為1／2／3／16／32／48；本輪已找出部分原生分支，**尚未整合到編輯器renderer**。
- 463張圖的LYR表序不等於order遞增。不能把表序當draw priority，也不能未確認便自行排序。
- 302／303有1,578筆物件尾8 bytes非零；加上flag、原始名稱bytes、Z、動畫狀態與深度遮擋，都需要完整model及writer。
- 155份EFF都是CAKEWAPPER XML；依明確corpus路徑政策，有107份的材質引用無法解析。這是離線政策結果，並非觀察到遊戲一定缺材質；資源fallback需繼續追查。
- MDN effectCount與EFF存在性並非一對一，不能用存在性自行建立或刪除效果。
- 本次未啟動遊戲驗證，也未完整重新分析伺服器map loader。出生Region與MapInfoEx等線索重用既有報告，其證據／限制列於[場景審查](scene-audit.md)。

## 從初版到完整製圖器的工作順序

| 階段 | 功能 | 驗收門檻 | 本輪狀態 |
|---|---|---|---|
| 1 | 安全MDN核心、碰撞／點／範圍編輯 | corpus無損、局部修改隔離、可回復、配套另存 | 已完成初版及離線驗證 |
| 2 | 共用Core的LYR／TIL／BAC完整model與writer | 原始bytes無損往返；未知名稱／flags／尾段完整保留 | 尚未實作 |
| 3 | 正確場景預覽與座標overlay | 翻轉、動態頂點、透明、圖層、動畫、角色／特效遮擋逐案例對照 | 原生部分定位，尚未整合 |
| 4 | Tile畫筆、物件放置、圖層及素材庫 | 每個動作可undo，export→reopen語意與未編輯資料一致 | 尚未實作 |
| 5 | 空白新圖、匯入美術、RGB555／RLE／BAC素材產生 | 自製MD3+LYR+TIL+BAC／EFF能經原生載入 | 尚未實作 |
| 6 | 可玩新地圖整合 | client/server地圖一致、入口／回程／復活、NPC／怪物Region、碰撞／高度／鑽地實測 | 尚未實作及遊戲內驗收 |

下一個具體里程碑應是**把完整LYR／TIL資料納入Core，先做無損writer與正確場景＋碰撞疊圖**。等場景座標與保存可信，再加入tile paint與物件放置；最終以一張從空白建立、能在隔離遊戲環境遊玩的測試圖完成端到端驗收。

## 原始問題的完成度

| 問題 | 狀態 |
|---|---|
| 能否沿TricksterMap製作地圖編輯器 | 已回答：可行，已交付可用初版 |
| 是否有REA結合遊戲讀取邏輯的新證據 | 已回答：有，格式、版本、物件stride及部分tile render分支已交叉核對 |
| 是否能無損處理現有MDN | 已回答：本輪700檔通過；未涵蓋其他版本、內嵌變體或所有客戶端 |
| 是否已完整還原場景與全新製圖 | 尚未完成，缺口與里程碑如上 |
| 是否達到遊戲內完美一致 | 未驗證，不能宣稱 |
