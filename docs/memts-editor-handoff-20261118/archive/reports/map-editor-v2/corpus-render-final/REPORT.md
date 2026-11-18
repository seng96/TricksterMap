# C# SceneRenderer corpus離線驗證

2026-11-17。以獨立net48 x64 harness快照測試，沒有修改主GUI、Core或原始遊戲檔案。

本輪共700次：`deliverables/map`的699個完整MDN配套，加使用者畫面中的`data/map/map_sq14a.md3`。
另外2份NORI簽名的MD3與1份缺少配套的MDN不納入渲染，並非渲染失敗。

- 700／700成功載入並渲染320×240整圖fit視窗，總耗時83.03秒。
- 沒有load/render exception、索引越界、BAC載入失敗或圖層未完成警告。
- 700份MDN前後SHA256均相同。
- 每張至少188種像素顏色；沒有單色／全背景輸出候選。
- 已目視檢查map_sq14a祭壇場景與map_sq00廣場縮圖，具可辨識地磚及BAC物件。
- 即使物件數為0也讀現存BAC的最新catalog支線，沒有新增無關的空BAC錯誤標籤。

11張圖共812筆BAC物件名稱缺失或歧義。逐張與既有Python MapScene比對後，缺失筆數全部完全相同；
這是現有資源引用問題的相容結果，不是本輪C#解碼器新引入的遺失。逐張比較見`bac-python-crosscheck.json`。

其餘警告分類為：EFF尚未合成、深度遮擋未遊戲驗收、動態頂點以靜態位置預覽、半透明分支光照仍未完全還原、動畫未與遊戲事件同步。
這些是明示功能邊界，沒有混計為檔案結構錯誤。

此驗證只證明指定視窗、倍率、第一影格的離線載入／渲染能完成。它不是遊戲內逐像素對照，也未逐張目視700張圖，
不能以像素非單色推論排序、動畫、光照或所有viewport位置皆正確。

`corpus-render.json`保存每張結果、zoom／origin、警告、耗時及MDN來源SHA。`summary.json`保存統計和程式SHA。
`../corpus-harness-final/source-manifest.json`保存編譯使用的renderer快照SHA；Core DLL亦以SHA綁定。
來源快照與當前`Scene/*.cs`於統計時完全一致。

```powershell
dotnet build reports/map-editor-v2/corpus-harness-final/CorpusRender.csproj -c Release
reports/map-editor-v2/corpus-harness-final/bin/Release/net48/CorpusRender.exe deliverables/map <new-output> data/map/map_sq14a.md3
```
