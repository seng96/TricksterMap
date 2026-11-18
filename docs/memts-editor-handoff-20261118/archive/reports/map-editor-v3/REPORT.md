# TricksterMap 0.3.0 實作與驗證

本輪完成 EFF 離線預覽、材質依賴檢查與完整遊戲路徑資源 ZIP 匯出。既有滾輪縮放、拖曳、可調碰撞筆刷、精細／簡易切換、地磚及物件編輯持續可用。這個版本尚未達到遊戲逐像素一致或所有製圖功能完備的階段。

## 交付

- [可攜套件](../../deliverables/map-editor/TricksterMap-editor-0.3.0-win.zip)
- [執行檔](../../deliverables/map-editor/0.3.0/TricksterMap.exe)
- [中文操作說明](../../deliverables/map-editor/0.3.0/README.md)
- [EFF 技術說明](../../deliverables/map-editor/0.3.0/EFF_PREVIEW.md)
- [機器可讀驗證摘要](verification-summary.json)

完整解壓 ZIP 後啟動 EXE；保留 DLL。套件包含來源快照、補丁、CLI 與授權，沒有原始遊戲地圖。

EXE SHA-256：`65b87b943d662ae40a19836ec9b178c984c792f85b2badd89b005ccc79df57b0`。

ZIP SHA-256：`702e055ae8e98f98de54baefb9adb47d9cba16d5ac3f98d757cd58bff6daa099`。

## 本輪功能

1. 精細預覽繪製 MDN 特效實例對應的 EFF CAKE／CREAM，含粒子生成、移動、大小／顏色／透明度、材質影格及支援的混合公式；效果可獨立切換並隨場景播放。簡易碰撞模式維持獨立。
2. 素材對話框顯示原始引用、實際路徑、CAKE／CREAM 數量、缺檔、缺動畫與失效引用。可指定客戶端根目錄或 data；只依明確路徑解析，避免任意同名檔誤配。
3. 新完整 ZIP 匯出收集 MD3／LYR／TIL／BAC、已有 EFF 與指定 NRI，保留遊戲相對路徑及雜湊清單。只正規化匯出副本的材質路徑，保留 XML 屬性順序及未知節點；原檔不變。既有 ZIP 不覆寫，依賴不完整則拒絕完整匯出。
4. Core 統一 NORI 動畫索引供預覽與匯出檢查。舊式零長度／零位移表只在圖片鏈、版本化影格鏈可完整驗證至檔尾時讀取；不改寫、不任意掃描、不把截斷資料搬位猜讀。每個素材庫使用獨立圖片快取命名空間。

## 驗證

| 檢查 | 結果 |
|---|---|
| Release 建置（Core net48/net10、GUI、CLI、Tests） | 0 警告、0 錯誤 |
| 自動測試 | 135 通過、0 失敗；含 14 組獨立 Python 原生粒子數值向量 |
| MDN 無操作往返 | 700 份逐位元相同；另略過 2 份非 MDN |
| LYR／TIL 無操作往返 | 699 組逐位元相同 |
| 全量地圖離線繪製 | 700 通過、0 失敗；3 份非 MDN／配套不全略過 |
| EFF 掃描 | 155 份，0 載入例外；140 張有活動粒子，138 張在 0.5 秒局部取樣有可見效果 |
| EFF 原檔保留 | 全部受查 MDN／EFF 雜湊未變 |
| 最終套件 GUI | 3 案通過：sq14a、sq00、無地磚 mapg02；含效果選單、素材對話框、視角、筆刷、復原、編輯與匯出 |
| 最終套件與 CLI | 34 個交付檔雜湊核對；來源 ZIP CRC 正常；CLI 實改副本只變指定格的 1 byte |
| 可攜資源包 | 獨立解壓根目錄重新載入、再次匯出通過；效果 PNG 與原取樣逐位元相同，資源清單逐檔核對 |

全量繪製取完整 MDN 配套、320×240 全圖取樣，基礎場景使用首影格，預設 EFF 使用一秒取樣；不代表遊戲渲染一致。完整繪製後僅修改離屏驗證對話框的截圖程式；產品 renderer 未再變更，最終封裝另以上述三案 GUI 與可攜效果包實測。各次 DLL 身分保留於原始 JSON。

原始檔：[測試 TRX](automated-tests.trx)、[MDN 往返](mdn-roundtrip-final.json)、[場景往返](scene-roundtrip-final.json)、[全量繪製](corpus-render-release/corpus-render.json)、[EFF 明細](eff-corpus-release/audit.json)、[交付檢查](delivery-verification.json)。

EFF 取樣的 155 張並非全部素材完整。完整匯出預檢有 82 張被阻擋，73 張通過；問題包含 14 張找不到宣告材質、55 張有缺少／不唯一的材質動畫、12 張有失效 CAKE 引用、9 張有缺少／重複 CREAM、1 張材質結構不合法，分類互有重疊。預覽可顯示其中有效部分並警告，不能把無例外視為效果完整。原資料的失效引用沒有被自動改寫。

代表圖：[sq00 特效](packaged-effects-final/effects-1s.png)、[舊式 NRI 特效](actual-effects-legacy-release/effects-1s.png)、[素材檢查介面](package-final-sq00/effect-resources.png)。sq00 在同一個基礎場景時間開／關 EFF 有 108450 個像素差異；只用這項比較判斷效果確實繪出。1→1.5 秒動畫差異另包含場景動畫，不能當作純 EFF 比對。

## REA 證據

粒子公式沿用既有 MemtsEffectStudio 的 native_math／native_patterns 及其原生證據，沒有改動該編輯器。
本輪另以 REA/Ghidra 靜態核對舊式 SUBCREAM 指令，確認 `@Name` 為登錄身分、`SubCreamName` 為材質效果引用；不能互換。

- `0x983560` 登錄與時間插入，`0x983420` 將登錄 key 寫入節點 `+0x64`，`0x9830c0` 初始化子效果欄位。
- `0x985e00` 從節點 `+0x64` 辨識 `_LOOP_`、`end`、`_RESTART_`、`_STOP_`；Loop／Normal 取目前子串列尾端時間，Restart／Stop 用自己的開始時間。
- `0x983310` 提供尾端時間。兩項回歸測試涵蓋兩個名稱欄位分離及不同取時規則。
- 原客戶端 SHA-256：`4bb6843be21f5bd52d3e61e4d3a036a8cd49d2ac9f8af47401f81f7dc80dfd43`。
- 載體 SHA-256：`48712dd87edc56f048b4afd7b8eb7f947aa2cd2e694a00972c71a1c04b661b0a`；全部 17 個原始區段範圍已核對相同。
- [完整 REA Evidence bundle](native-sentinel-evidence.json)；精確 Evidence ID 見驗證摘要。

此為保留原 VA／區段 bytes 的不可部署載體，只作靜態分析。Ghidra 輸出不是原始碼，載體未涵蓋整個客戶端，尚未構成遊戲特效逐像素或事件時序驗收；分析工作階段已關閉。

## 待完成範圍

完整場景／人物／特效深度交錯、GPU alpha-test／光照、動態頂點、遊戲共用 RNG 與事件時鐘仍未完整還原。預覽固定種子、60 秒循環與 3000 粒子上限。EFF 時間軸／實例製作介面、完整 BAC 美術寫入尚未提供。

前輪[地圖 148／冒險三島驗收](../map-editor-v2/game-acceptance/REPORT.md)的棋盤、紅牆、缺口、出口往返與重登入已由使用者確認；這個結論只涵蓋基本製圖流程。0.3 EFF 本輪屬離線驗證，未新啟動／替換遊戲客戶端或修改伺服器資源。下一階段應針對深度與特效在隔離客戶端作同時間、同視角比對，再擴充特效製作介面。
