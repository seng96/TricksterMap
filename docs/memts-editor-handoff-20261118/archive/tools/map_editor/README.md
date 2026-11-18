# TricksterMap 0.3.1 地圖製作工具

基於 [seng96/TricksterMap](https://github.com/seng96/TricksterMap) commit `1d56fad372e428ccb1123f42a0795b31a9c306f0`，沿用前輪 REA 原生格式證據擴充。

- [可攜版 ZIP](../../deliverables/map-editor/TricksterMap-editor-0.3.1-win.zip)：完整解壓後啟動 `TricksterMap.exe`。
- [操作說明](../../deliverables/map-editor/0.3.1/README.md)。
- [本輪功能與驗證報告](../../reports/map-editor-0.3.1/REPORT.md)。
- [前輪 REA 與格式分析](../../reports/map-editor-20261117/REPORT.md)。

本版支援滑鼠滾輪縮放、拖曳平移、1～128格圓／方碰撞筆刷，精細／簡易模式即時切換，並直接讀取 LYR／TIL／BAC 預覽地圖。

0.3.1修正TIL地磚逐塊上下翻轉造成的接縫錯亂，同步修正PNG／BMP圖集匯入；原始地圖資料不變。新增不對稱四角、四種翻轉及匯入原始位元組回歸驗證。

0.3.0新增地圖EFF粒子預覽、效果顯示切換、CAKE／CREAM與NORI依賴檢查、素材根目錄選擇，以及包含遊戲相對路徑和SHA清單的完整資源ZIP匯出。EFF仍為離線預覽，完整深度遮擋、GPU alpha-test與遊戲事件時序尚未逐像素驗收。

場景編輯後請交付完整MD3／LYR／TIL／BAC配套；原生伺服器也會讀取場景檔，不能只替換MD3。冒險三島地圖148已通過完整配套原生載入；使用者確認地面、碰撞、出口往返與重新登入正常。這是基本製圖驗收，完整效果與渲染一致性進度見[驗收紀錄](../../reports/map-editor-v2/game-acceptance/REPORT.md)。
新增地磚調色盤、鋪設／翻轉／擦除、PNG／BMP圖集匯入、BAC物件新增／複製／移動／刪除，以及使用目前素材建立空白地圖。
碰撞、地磚與物件有各自的復原歷史；另存時一起輸出配套。EFF、動態頂點與完整遊戲深度／光照仍未完全整合，遊戲內可玩性驗收另需測試環境。

套件含GUI、CLI、原始碼快照、上游補丁與授權，不含原始遊戲地圖。遊戲資料與MapInfoEx／NPC／怪物設定沒有被改動。
獨立上游 checkout 在 `tools/map_editor/upstream`；此目錄由 `.gitignore` 排除，避免主專案誤加入gitlink；可重建補丁保留在本目錄。

## 建置／驗證

需要Windows、.NET SDK 10與.NET Framework4.8 targeting pack。來源快照包含Core使用的Ionic.Zlib loose dependency；正式場景wrapper的壓縮與校驗已改用平台DEFLATE及獨立Adler。

```powershell
Set-Location tools/map_editor/upstream
dotnet build TricksterMap.sln -c Release
$env:MEMTS_MAP_CORPUS='C:/Memts/new-memts/deliverables/map'
$env:MEMTS_SCENE_CORPUS='C:/Memts/new-memts/deliverables/map'
dotnet test TricksterMap.Tests/TricksterMap.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/publish-cli.ps1
```

離屏GUI驗證：`TricksterMap.exe --verify-map <完整地圖路徑> --output <全新目錄>`。
不用接管使用者滑鼠，會檢查模式／視角、筆刷、地磚／物件、匯出、圖集匯入、空白地圖與快速切換背景載入。
詳細PowerShell呼叫與生成圖檔見操作說明。

`package_editor.py`在全新版本資料夾封裝當前Release輸出，`verify_delivery.py`驗SHA、來源ZIP與封裝CLI實際局部修改。
兩者不覆寫既有版本套件。

## 唯讀調查工具

```powershell
python -X utf8 tools/map_editor/audit_scene.py --help
python -X utf8 tools/map_editor/native_probe.py
python -X utf8 tools/map_editor/build_carrier.py --pass1
python -X utf8 tools/map_editor/build_carrier.py
python -X utf8 tools/map_editor/build_carrier.py --render
```

原生工具需要pefile／capstone，scene audit需要Pillow／numpy與既有Python預覽相依；**執行編輯器不需要Python**。
REA carrier是不可啟動的聚焦分析載體，以SHA256與Evidence ID辨識；不能代替遊戲執行檔或實機驗收。
