# 地圖編輯器研究與精簡交付整理

2026-11-18。編輯器0.3.1實作已由`5ab911e4`合併至本repo，前輪實作為`68d2d21`。本次補存原Memts工作樹的研究、驗收與最新可攜封裝，不再重複保存全部舊版本或遊戲地圖。

## 使用入口

- 編輯器正式原始碼在repo根目錄；本資料夾不取代它。
- 最新測試套件：[0.3.1可攜ZIP](archive/deliverables/map-editor/TricksterMap-editor-0.3.1-win.zip)，完整解壓後執行TricksterMap.exe。套件含來源快照及操作說明，不含遊戲地圖。
- [0.3.1地磚修正與驗收](archive/reports/map-editor-0.3.1/REPORT.md)、[0.3.0效果預覽與匯出](archive/reports/map-editor-v3/REPORT.md)。
- [原生格式與場景分析](archive/reports/map-editor-20261117/REPORT.md)、[場景契約](archive/reports/map-editor-20261117/scene-audit.md)。
- [遊戲基本製圖驗收](archive/reports/map-editor-v2/game-acceptance/REPORT.md)。
- [歷史輔助程式](archive/tools/map_editor/README.md)。
- [逐檔取捨與原始SHA清單](inventory.json)。

## 證據界線與重建

歷史文件及腳本按原樣保存，內部相對連結／絕對路徑、輸入素材、測試資料夾、Memts的repo根目錄假設仍代表當時環境，不保證搬到本repo後可直接執行。遺留`../../deliverables`、`.memts`、`tools/map_editor/upstream`等路徑需依獨立測試工作區改接；不要為重跑測試覆蓋正式遊戲資料。缺少的原始遊戲MD3/LYR/TIL/BAC/NRI與EXE須由合法測試環境提供，不隨本次上傳。

141項自動測試、700個MDN與699套場景往返、封裝GUI及地磚方向等數字是既有驗收紀錄，本次沒有重跑完整遊戲／700圖測試，也不把靜態證據當所有粒子像素與遊戲事件一致的證明。完整GPU深度、alpha-test及事件時序仍有未覆蓋項。

省略：0.1/0.2/0.3.0舊封裝與重複解壓、失敗套件、遊戲圖資副本、obj/bin/cache、重複渲染輸出及含管理器等無關大範圍內容的native-sentinel-evidence。0.3.1的聚焦native-tile-evidence與必要原始碼／測試報告保留。補丁已進來源歷史，不再另存整份重複patch。

`inventory.json`的published項可由本提交取回；omitted項是經分類不再保留，不代表每個檔案曾上傳。原Memts工作樹僅在此提交成功推送、逐檔遠端讀回驗證後才允許清除。管理器、GM工具、NRI製作、商店與其他未提交內容不在清理範圍。
