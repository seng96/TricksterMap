# 冒險三島／地圖 148 原生載入驗收

使用者指定客戶端根目錄 `C:\Memts\new-memts`，測試地圖 ID 148；分流由最初的 2 更正為 **3／冒險三島**。

## 已完成

- 目前 MapInfoEx 的 1～627 全部佔用，沒有使用技能舊清單中的空號。
- 148 是 `data\map\map_sq11.md3`，沒有其他 MapInfoEx ID 共用該檔名。
- 建立 3008×2656 的實際新場景：自製 RGB555 棋盤地磚、紅色邊界與中央測試牆、可通行缺口、一個既有 BAC 物件。
- 保留原圖 32 個 gameplay points、4 個 ranges、非碰撞 config；保持原有入口、NPC、回程及地圖尺寸。這是沿用既有註冊的驗收圖，不是新增 MapInfoEx。
- 新檔案經 Core 匯出、重開與地磚逐 byte 比對。四個檔案和獨立客戶端的 SHA-256 全數相同，BAC 與模板相同。
- [獨立 Python 驗證](asset-validation.json)另行確認 points／ranges 與原始檔逐 byte 相同、非碰撞 config 相同、牆／缺口採樣正確、四檔總長度與 MDN aggregate 一致，並以 Python zlib 驗證 LYR／TIL Adler checksum。
- 原生伺服器日誌確認 `map_sq11.md3 loaded as id:148, type:1`，其後到達 `Server started`。見 [原生載入證據](native-map148-load.txt)／[結構化紀錄](native-map148-load.json)。
- 測試過程曾暫存四個地圖資源；載入完成後已全部還原，原始備份／fixture／現況 hash 由 [部署紀錄](server-148-bundle-v2.json) 保留。最後檢查 `all_disk_files_original=true`。
- 使用者已手動登入獨立測試端；其視窗標題為冒險3島，原生 API 回報有效角色，連線指向 gserverc2 的 20008。遊戲端日誌也有 map 148 的[進圖及 ACK](native-map148-entry.txt)。
- **使用者已實測確認**綠色棋盤地面與中央紅牆顯示正常，牆體阻擋且缺口可通過。這是使用者在遊戲內的確認，不是自動圖像比對。見[人工驗證紀錄](manual-verification.json)。
- **使用者另已確認出口往返與重新登入均正常**，回到148後新地面、紅牆與缺口通行維持正常。本輪「製作→完整配套匯出→原生載入→行走→出口往返→重登入」基本驗收通過；不代表全部場景效果或逐像素相容性已驗證。

## 重要修正與證據界線

冒險三島由 **gserverc1、gserverc2、gserverc3** 共同承載；`MasterServer.ini` 在第三個世界將 `zone 148` 指派給 `gserverc2`。管理器的 GameServer2 不是客戶端第二島。三個程序使用同一支 Gameserver1 EXE；REA 及外部唯讀 PEB 檢查都顯示初始化後 current directory 為 Gameserver1。不能只看資料夾名稱判斷實際讀取位置。

最初僅暫存新 MD3 時，LYR/TIL 尚為原圖，gserverc2 在讀取 148 時記錄約 1.48GB 的異常配置失敗。已還原，改用**完整 MD3／LYR／TIL／BAC 配套**後原生載入成功。這證明伺服器也讀取場景配套；場景製作後不能只交付 MD3。見 [不完整配套失敗記錄](incomplete-bundle-failure.txt)。

`groundAttribute` 警告在原圖 12:19:45 的舊日誌已存在，並非本輪新增。見 [原圖基準](original-map148-load.txt)。沒有為消除既有警告而猜填未知屬性。

在測試期間，外部操作者於 14:53:48 重啟三個 gserverc 程序，完整配套當時正在暫存；本助手未送出 start-all。還原磁碟檔案不會改變已讀入的測試區域；下次正常重新啟動會讀回原始圖。

## 客戶端

獨立測試端：`C:\Memts\new-memts\.memts\map-editor-acceptance-client-148-001\Dev.exe`。

地圖為實際私有複本，四個 fixture 檔沒有透過 junction 寫入原客戶端。其他素材以既有完整客戶端的目錄 junction 提供；地圖表與目前工作區 DAT/IDX 保持一致。啟動配套已補齊工作中 Dev 的 JPEG proxy、DLL 與啟動畫面資源，原 native-hook pre-entry fixture 不當作可玩客戶端啟動器。來源雜湊與 junction 清單保留於該目錄的 `map-acceptance-client.json`。

啟動：同目錄 `Start-MapEditor-148-Test.cmd`。登入需使用者操作，不保存或輸出帳密。

## 尚待完成

Windows Computer Use 能列出已登入的遊戲視窗，但 DirectX 視窗擷取反覆回報 `window capture timed out`，未取得可用的螢幕座標；內建截圖快捷鍵也未產生檔案。因此沒有在看不到畫面時猜測點擊位置，亦未宣稱自動圖像比對或自動行走驗證通過。遊戲畫面、通行、出口往返及重登入的結果由使用者實測確認。

新地面、牆體阻擋、缺口通行、出口往返及重新登入均已由使用者確認。BAC 遮擋、EFF、完整光照與動態頂點仍需實作及專項比對；本輪未將這些未完成項目列為通過。

測試牆世界座標：X=1504～1535，Y=1136～1535；缺口 Y=1344～1407。碰撞格 16×16，筆刷與場景預覽使用同一個世界座標。

**地圖148的基本製圖與遊戲內往返／重登入驗收通過；完整效果與原生渲染一致性仍未完成。**
