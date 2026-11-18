# 載入／匯出生命週期唯讀檢查

2026-11-17。檢查當前ScenePreviewLoader、MDIParent、MapBundleExportService、BlankMapService及MapEditFingerprint原始碼。
此節為程式碼review，並非另外執行的快速切圖／故障注入測試；沒有修改受檢程式碼。

沒有發現需阻止本輪離線編輯器交付的資料遺失問題：

- 先前load在背景以CancellationToken中止等待；已開始的解析即使無法即時中止，完成時仍檢查token。
  BeginInvoke的UI回呼再以request epoch檢查，舊結果不能覆蓋新地圖，失效renderer會Dispose。
- static SemaphoreSlim讓場景解析單一執行，取消尚未取得worker的請求不會錯誤Release。
- Current renderer在UI thread卸下／Dispose；表單不自行Dispose共享renderer。MDI關閉時會取消loader並Dispose Current。
- Scene initial fingerprint在背景解析後、UI場景編輯前計算；新地圖Ready採對應epoch的baseline。
  碰撞、點／範圍的MDN fingerprint與場景fingerprint分開檢查，切圖不依標題星號判斷資料是否改動。
- 匯出先將MDN序列化為staged model快照，保存／失敗不會更新來源model的OriginalSerializedLength與MapSizeSum。
  其MDN長度差先在snapshot序列化套用一次，再加LYR／TIL相對來源檔長度差；重複另存不會累積相同delta。
- 空白圖清除既有點／範圍／效果和unknown tail、重設sum與serialized baseline；再以實際新MDN長度及
  來源BAC／TIL／LYR大小建立sum，匯出時用新LYR／TIL差額修正。來源template不會被就地變更。
- 另存前檢查目的地全部配套不存在，一般移入失敗只清理由本次建立的輸出。

仍保留既有邊界：多檔bundle不是斷電時整套原子交易；原始配套在其他程式同時改動時沒有檔案版本鎖定；
BlankMapService對部分未知header欄位採template繼承政策，Param14採已說明的Main層慣例，尚未經原生遊戲載入驗收。
這些限制不會因700張離線渲染成功而消失。
