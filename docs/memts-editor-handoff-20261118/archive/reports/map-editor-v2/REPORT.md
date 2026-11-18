# TricksterMap 0.2.0：互動預覽與場景製作

本輪承接2026-11-17的REA分析與0.1.0初版，完成使用者指定的滾輪縮放、拖曳平移、可調筆刷、精細／簡易切換，並加入可實際保存的場景製作流程。

## 交付

- [完整0.2.0可攜版](../../deliverables/map-editor/TricksterMap-editor-0.2.0-win.zip)：解壓後啟動`TricksterMap.exe`，視窗標題含0.2.0。
- [操作說明](../../deliverables/map-editor/0.2.0/EDITOR_PROTOTYPE.md)。
- [本地來源與重跑方式](../../tools/map_editor/README.md)。可攜版內含原始碼ZIP、上游補丁與授權。
- [前輪REA證據及格式判斷](../map-editor-20261117/REPORT.md)。本輪沒有把既有Evidence誤稱重新分析整個客戶端。

GUI為純C#／.NET Framework4.8，不需要Python。遊戲素材仍由所選地圖同名的MD3／LYR／TIL／BAC讀取。

## 使用者要求與已完成行為

| 要求 | 0.2.0行為 |
|---|---|
| 滾輪縮放 | 以游標世界位置為錨，1%～1600%，可符合視窗／回1:1 |
| 拖曳平移 | 中鍵、右鍵或空白＋左鍵；平移不改格子、undo或dirty |
| 可調碰撞筆刷 | 阻擋／可通行、1～128格、圓／方、精確hover遮罩；拖曳補線、邊界裁切、整筆復原 |
| 直接地圖樣式預覽 | LYR bitmap raw/RLE、TIL RGB555、BAC NORI原始名稱查找；不再只看atlas |
| 隨時切精細／簡易 | 共用世界像素與視角，切換保留縮放／平移／歷史；精細可調碰撞疊圖透明度、格線 |
| 持續推進製圖 | 視覺地磚調色盤、圖層／群組、鋪設／翻轉／擦除、圖集匯入、BAC物件放置與變換、空白地圖建立 |

700個實際MDN的碰撞X×BppX、Y×BppY都等於地圖最大座標+1，因此本corpus的碰撞／場景共用像素座標有實際依據。步長無效時停用精細疊合。

「預覽→播放場景動畫」可選播放BAC物件動畫，時間僅影響renderer，不寫回模型。簡易模式避免重繪精細場景。

## 製圖流程

1. 開啟地圖後立即可編輯簡易碰撞；單一背景worker載入場景，過期請求不覆蓋新選取。
2. 碰撞分頁能看精細場景並畫道路／阻擋；相同視角切簡易可檢查格值。
3. 「地圖場景製作」選圖層、素材群組和縮圖，以地磚筆刷鋪設、翻轉或擦除；每層有undo／redo。
4. PNG／BMP圖集可匯入獨立TIL群組：32×32整數倍、每邊最多4096，轉成bottom-up tile-major RGB555。alpha<128採洋紅色鍵。原圖像不改動。
5. 「場景物件」可從BAC catalog新增到空Main層，或雙擊畫面定位；也能移動X／Y／Z、複製、刪除與復原。原物件完整raw紀錄保留，新物件依版本48／56bytes、原名與其他0值初始化。
6. 新建空圖以明確選定的目前地圖作資源模板，建立空Floor＋Main、全部阻擋的碰撞；不搬原圖配置、傳點、範圍或效果。可匯入自製地磚、放BAC物件並畫可通行區域。
7. 另存完整地圖同時保存MDN與目前LYR／TIL模型，複製BAC及既有圖的EFF；新空圖不繼承EFF。不存在的TIL可由新匯入的圖集產生。

新圖資源已通過建立、修改、匯出重開；**沒有自動註冊MapInfoEx或伺服器入口，也尚未進行遊戲內可玩性驗收**。
新建Main/Order6、Param14=1採既有corpus常見慣例：694/699關聯符合，5個例外保留；完整原生consumer仍未還原，不把此慣例稱為已證實的通用規格。

## 保存與重要修正

Core的`SceneDocument`保存全部LYR欄位、原始名稱bytes、48／56-byte物件、未知尾段及TIL群組。無修改時保留原始壓縮bytes；修改後重建offset、payload長度、object counts及MDN aggregate差額。

本輪發現Ionic.Zlib會對真實較大的改寫LYR輸出錯誤Adler。例如sq00的DEFLATE本體可解出333,396bytes，但檔尾checksum與獨立計算不同。已將場景wrapper writer改為平台raw DEFLATE、RFC1950頭及獨立Adler32，reader亦驗checksum。
真實sq00修改與5552／65521邊界、超過1MiB隨機／重複內容，都經獨立平台ZLibStream驗證，沒有以放寬reader掩蓋壞輸出。

匯出使用序列化快照，避免重複另存累加來源baseline。配套先staging再移入，目的檔存在時拒絕覆寫，普通例外清理本次輸出。此策略不等於斷電時整套多檔原子交易。

封裝驗證亦修正快速切圖的生命週期問題：動畫先解除舊renderer再釋放分頁，重繪檢查已Dispose的控制項；未執行的背景UI回呼也會釋放過期renderer。修正後的封裝版在三張實際地圖上全流程重跑通過。

## 驗證

| 項目 | 結果／證據 |
|---|---|
| Release solution | Core net48／net10、CLI、GUI、Tests，0warnings／0errors |
| 自動測試 | [115／115通過](tests/map-editor-v2.trx)，無失敗／略過 |
| MDN無損往返 | [700／700](mdn-roundtrip.json)，32個未知尾段保留 |
| LYR／TIL無損往返 | [699／699完整配套](scene-roundtrip.json)，包括301／302／303與raw／typed wrapper |
| 全量C#預覽 | [700／700成功](corpus-render-final/REPORT.md)，699完整配套＋使用者的data/map/map_sq14a；83.03秒 |
| 全量預覽異常 | 0load/render exception、0結構／索引／圖層失敗警告、0單色候選；MDN前後SHA全相同 |
| BAC名稱交叉核對 | 11張／812筆缺失或歧義，[與既有Python結果一致](corpus-render-final/bac-python-crosscheck.json)，沒有任意補另一個素材 |
| 真實GUI流程 | [sq14a](sq14a-final/verification.txt)、[sq00](sq00-final/verification.txt)、[無地磚mapg02](no-tiles-final/verification.txt)：視角／模式、大小筆刷、tile/object操作、匯出重開、PNG匯入、空圖及來源保存 |
| 快速切圖 | [Lifecycle驗證](lifecycle-sq14a/verification.txt)，兩次連續載入只接受最後圖；過期或未執行UI callback的renderer會釋放 |
| 封裝 | [delivery-verification.json](delivery-verification.json)：檔案SHA、來源ZIP、封裝CLI局部修改；封裝GUI另以三張圖重跑 |

全量預覽快照保留renderer source、Core DLL和harness SHA；屬於所記錄的靜態配套、320×240 fit視圖覆蓋，不代表所有地圖的所有像素／動畫狀態都和遊戲比對。
NORI格式MD3兩檔與一套缺配套MDN明確排除，不把它們算成成功。

圖片範例：

- [使用者地圖的精細預覽](package-sq14a/fine-preview.png)
- [相同視角的簡易模式](package-sq14a/simple-preview.png)
- [純地圖樣式](package-sq14a/map-style.png)
- [地磚製作分頁](package-sq14a/tile-authoring.png)
- [新空圖新增BAC物件](package-sq14a/blank-object-placement.png)

這些是離屏WinForms繪製，已做目視檢查，並非遊戲截圖。GUI驗證不注入系統輸入、不取得使用者桌面控制。

## 尚未達到「完美」的範圍

- EFF粒子與外部材質依賴沒有完整合成／收集。
- 高byte0x10動態頂點仍是靜態預覽；0x20只涵蓋已確認的一般顏色分支，完整GPU lighting／alpha-test仍未重建。
- 場景按現有檔案層序及物件Y-Z近似排序，尚未重建角色與物件的完整原生深度遮擋。
- BAC播放是離線時鐘，尚未和遊戲事件／相機／RNG同步。
- 地磚圖集匯入尚無獨立撤銷匯入；碰撞、地磚與物件的歷史目前分開，沒有全域操作棧。
- BAC美術資源目前重用所選模板；沒有新動畫美術的完整BAC writer。
- MapInfoEx、入口／回程／復活、NPC／怪物Region與client/server一致性尚未在隔離遊戲環境驗收。

使用者已指定 `C:\Memts\new-memts`、冒險三島與地圖148。新製地面／碰撞／BAC物件的完整配套已通過 gserverc2 原生載入，磁碟原檔也已還原並核對SHA。使用者已實測確認棋盤地面、紅牆阻擋、缺口可通行、出口往返及重新登入均正常，本輪基本製圖驗收通過；DirectX畫面擷取受限，沒有宣稱自動圖像比對通過。EFF、完整光照、動態頂點與BAC遮擋仍未列為通過。詳見[冒險三島驗收紀錄](game-acceptance/REPORT.md)。
