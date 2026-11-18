# TricksterMap 原始碼與實際地圖審查

日期：2026-11-17。上游基線：`seng96/TricksterMap` commit `1d56fad372e428ccb1123f42a0795b31a9c306f0`。本報告以原始碼和離線檔案為證據；遊戲端原生 REA 結論由主報告另行匯整。

## 可行性判斷

能以現有 Core 的 MDN 區段模型開始建置正式地圖編輯器。現成工具已有點、範圍 CRUD 及碰撞顯示，但還沒有地圖視覺圖層編輯、BAC 素材管理、完整動態場景、碰撞繪製工作流和新地圖完整匯出。因此目前不能將這個上游稱為完整製圖器，也不能承諾「完美」。

可驗收的目標應拆成：原始檔案無損往返；編輯局部不改動未知欄位；配置和素材索引相容；遊戲實際載入；視覺、碰撞和傳點分別測試。

## 實際 compile 路徑

`TricksterMap/TricksterMap.csproj` 明確只編入 GUI、`Data/TileData.cs`、`Data/EffectObject.cs` 等，主要 loader/save/model 的重複舊檔不在 Compile 清單。專案參照 `TricksterMap.Core`，實際修改 Core 即可供 CLI 和 legacy GUI 使用。AGENTS.md 目前「GUI 使用重複 model」說明已過時，不應再同步修改未編譯的舊副本。

另外，AGENTS.md 與 `MapDataInfo.Version` 舊註解將 303 描述為 MDN body 壓縮，也不能用作格式依據。原始 `MapDataLoader.Load()` 根本沒有依 303 解壓，而實際 corpus 的 303 可按 raw MDN sections 直接解析。配套壓縮必須從各檔的 envelope 與原生讀取證據判斷。

## 上游可重現的資料保留缺陷

以下行號指未修改的上述基線，可用 `git show HEAD:<path>` 重現。

| 問題 | 原始碼位置 | 影響 |
|---|---|---|
| 已知區段之後直接 return，未保留尾端 | `TricksterMap.Core/MapDataLoader.cs:130–139` | 另存後遺失原始未知尾段 |
| 重新計算 MapSizeSum | `TricksterMap.Core/MapSaveHelper.cs:104–123` | 原始 aggregate 與檔案大小本就不符時，無修改另存也改 header |
| `ReadBytes()` 不檢查 exact read；layer X*Y 未 checked | `TricksterMap.Core/MapDataLoader.cs:72,83,107,130` | 截斷檔、負數或溢位尺寸不能可靠地預先拒絕 |
| 原地 FileMode.Create | `TricksterMap.Core/MapSaveHelper.cs:109` | 後續例外或中斷可將原始檔截斷 |
| update 建立新物件但不帶 Options | `TricksterMap.Cli/CliApplication.cs:90,107–117,142,159–172` | 未公開的 Options 會在更新時被清零 |
| 服務只檢查 companion 存在 | `TricksterMap.Core/MapDocumentService.cs:40–61` | 檔案存在不能證明 BAC/LYR/TIL 的內容、索引或配套一致 |

`MapDocumentService.Load()` 實際允許缺 companion 的 MDN 檢查；只有 `Save()` 強制要求三種 companion。舊 GUI 則在 OpenMap 過程直接讀 TIL 與取得三種大小，缺件會拋錯。這也與 AGENTS「loader refuses missing companions」概述不同。

## Corpus 證據

輸入全為唯讀。`mdn-corpus-data-map.json` 和 `mdn-corpus-deliverables-map.json` 保存每檔 header/count/layer、解析終點、未知尾段長度與 hex、原檔 SHA256、配套缺件和 aggregate 值。

| 資料集（根目錄 *.md3） | MDN | 301 / 302 / 303 | NORI 等非 MDN | 有未知尾段 |
|---|---:|---:|---:|---:|
| `data/map` | 121 | 2 / 45 / 74 | 2 | 11 |
| `deliverables/map` | 700 | 6 / 456 / 238 | 2 | 32 |

700 個 MDN 都可依 96-byte header、20-byte config header + X*Y bytes、32-byte range、24-byte point、52-byte effect 的結構解析，沒有越界；32 檔在已知結尾之後還有 8–600 bytes。這些尾段的語意尚未確定，不能推斷全是 padding 或同一種隱藏物件。範例：`map_fd28g.md3` 多 600 bytes、`map_fd27a_ev.md3` 多 184 bytes，`map_dg18b.md3` 多 8 bytes。

所有 821 個 MDN（兩資料集合計，可能重複相同地圖）Param4 均為 0。原生證據指出 Param4 bit 0 涉及內嵌資源分支，故本次 Core 對該 bit 為 1 的檔明確拒絕編輯；沒有拿未出現的變體假裝已支援。

離線結構可解析不能證明遊戲會載入，也不能證明 LYR 座標、BAC 動畫或碰撞語意全部正確。

## 配套與畫面缺口

上游 `TricksterMap/TileReader.cs` 可讀 TIL 的相對 offset 表、tile group 與 bitmap，固定 `ReadBytes(2048)`，沒有完整的越界、群組尺寸與像素格式支援；也不寫回 TIL。`MDIParent.cs:134–180` 只是把各 tile atlas 排成圖，沒有解析 LYR 把 atlas 組合成真正場景。專案沒有 LYR loader/writer，也沒有 BAC 場景物件 authoring；BAC 並不是碰撞格，碰撞主要在 MDN config Type 1。

現有 `tools/effect_editor/map_scene.py` 可供預覽交叉核對：LYR 0/1/2 圖層、bitmap RLE、TIL group、301 的 48-byte 及 302/303 的 56-byte sprite 紀錄、BAC NORI 動畫名稱查找。但它明確是 read-only 靜態背景近似：

- `map_scene.py:135–149` 讀到 order 和倍率，但未使用原生排序／捲動比例，tile 高位旗標只警告。
- `map_scene.py:154–161` 未保留完整物件記錄，捨棄 flag、z 原值及 56-byte 紀錄後 8 bytes，僅轉成 `(x,y-z,name)`。
- `map_scene.py:212` 固定 animation frame 0，沒有場景物件動態更新。
- 沒有 MDN config、points、ranges、effects 的完整寫入；不能用它反序列化資料直接覆蓋原檔。
- 缺失／歧義 BAC 名稱可降級成警告，所以畫面能出現不代表所有物件都繪完。

## 本次安全性修正

在 `tools/map_editor/upstream/` 的 working tree 實作：

- Core exact-read、負數／乘法尺寸與文件大小上限、版號與內嵌資源變體防護。
- `MapDataInfo.TrailingData` 保留未知尾段。
- `MapSerializer` 依原 MDN 大小差額更新 aggregate；等長修改保留原 header，即使原 aggregate 與 companion 實際總和不一致。這是保守的保存政策，不能冒稱已證明遊戲要求 aggregate 完全相等。
- `MapSaveHelper.Save()` 先完整驗證和序列化，再在同目錄建立暫存檔、Flush(true)、以 File.Replace/Move 發布。驗證錯誤發生在觸碰目的檔前。
- CLI point/range update 保留原 Options；現有 CLI 沒有公開 Options 參數，這次沒有自行新增其語意。

Core 與 CLI 新增測試放在 `TricksterMap.Tests/Format/`，不把更多測試堆入既有大檔。`MdnRoundTripTests` 可用 `MEMTS_MAP_CORPUS` 選擇 corpus，對每個 MDN 實際 Save 到臨時檔，再逐 byte 與原檔比較，報告每檔 SHA256 和尾段長度。結果見 `corpus-roundtrip.json`；此檢查沒有更改原始地圖。

## 分階段工作與必要驗收

1. **安全編輯核心**：保留未知區段、原子保存、undo/redo、結構與資源引用校驗；以完整 corpus 無損往返、截斷／溢位拒絕、局部修改差異驗證。
2. **現有地圖製作**：場景與 MDN overlay 共用座標、碰撞／地形 brush、傳點與範圍視覺編輯、圖層 tile paint、BAC 物件放置；每種動作做最小修改副本，在遊戲測走路、遮擋、傳送及物件動畫。
3. **原生場景一致**：REA 還原 tile flags、sorting、z、parallax、動畫時序、EFF 和配色／光照；以固定地圖、固定相機和相同時間畫面比較。
4. **全新地圖匯出**：新建尺寸、tile atlas／LYR／BAC writer、動畫資源與 EFF authoring，打包完整 companion 和 server MapInfo／入口／怪物等設定。新地圖能進入且可走、重登、傳出、碰撞與外觀一致，才算可用的新地圖製作流程。

「完美」尚缺上述原生語意和遊戲實機比較，這次的無損測試不是其替代證據。
