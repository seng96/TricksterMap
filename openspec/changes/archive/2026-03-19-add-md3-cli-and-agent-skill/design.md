## Context

目前 `TricksterMap` 是單一的 WinForms `.NET Framework 4.8` 專案。`.md3` 的讀寫能力已存在於 [MapDataLoader](C:/workspace/TricksterMap-master/TricksterMap/MapDataLoader.cs) 與 [MapSaveHelper](C:/workspace/TricksterMap-master/TricksterMap/MapSaveHelper.cs)，但物件編輯規則仍分散在 GUI 表單事件中，像是 `PointObject` 的重複 ID 驗證與特定型別的 ID 正規化邏輯。這使得自動化流程無法安全重用既有能力。

這次變更需要同時解決三件事：抽離共用核心、提供可腳本化 CLI、以及建立能穩定調用 CLI 的 repo 內 Agent skill。由於專案已有 GUI 使用者，設計必須以不破壞現有桌面流程為前提，讓 GUI 與 CLI 共用相同的地圖編輯服務。

## Goals / Non-Goals

**Goals:**
- 將 `.md3` 檔案讀寫與 `PointObject` / `RangeObject` 編輯規則抽為可重用核心元件。
- 新增命令列專案，提供 `inspect`、`point`、`range` 相關指令，並能直接修改指定 `.md3` 檔案。
- 讓現有 GUI 改為呼叫共用服務，避免 CLI 與 GUI 各自維護一套規則。
- 在 repo 內建立 Agent skill，讓 Codex 以固定工作流執行 `.md3` 修改任務。
- 提供可自動執行的測試，覆蓋核心讀寫與 CLI 命令路徑。

**Non-Goals:**
- 不處理 tile 視覺化、collision 編輯器或其他 GUI 新功能。
- 不在第一版提供 JSON/YAML 批次腳本輸入。
- 不在第一版讓 skill 直接用自然語言推導複雜重構任務；先以明確操作命令為主。
- 不更改 `.bac`、`.til`、`.lyr` 的格式處理方式，只沿用目前檔案大小依賴邏輯。

## Decisions

### 1. 建立三層結構：Core、CLI、現有 GUI

**Decision:** 新增 `TricksterMap.Core` 類別庫與 `TricksterMap.Cli` console app，並讓既有 `TricksterMap` GUI 專案改為參考 `Core`。

**Why:** `MapDataLoader` 與 `MapSaveHelper` 已接近純邏輯元件，適合抽離。將 CLI 做成獨立專案比在現有 `Program.cs` 混入參數模式更容易維持穩定的命令列介面，也更適合被 skill 調用。

**Alternatives considered:**
- 在原 GUI 專案內加入 CLI 模式：改動較少，但 UI 與 CLI 生命周期混雜，後續維護成本高。
- 另外寫外部腳本直接讀寫二進位：起步快，但會複製格式邏輯，風險高。

### 2. 將編輯規則抽成服務，不讓 CLI 直接碰表單邏輯

**Decision:** 新增 `MapDocumentService`、`PointObjectService`、`RangeObjectService` 類型，封裝檔案開啟、儲存、驗證與 CRUD 規則。

**Why:** 現有 `CreatePointObject` 與 `CreateRangeObject` 將驗證與資料寫入寫在表單事件中，CLI 無法直接重用。將規則抽離後，GUI 只做資料收集與結果顯示，CLI 與測試可以直接呼叫服務。

**Alternatives considered:**
- 讓 CLI 直接重用表單內方法：依賴 WinForms，不利測試與長期維護。
- 只抽 helper method、不建立服務：可行但責任分散，CLI 組裝成本高。

### 3. CLI 第一版以參數式命令為主，輸出以文字摘要為主

**Decision:** CLI 採 `inspect`、`point <verb>`、`range <verb>` 型命令，參數直接描述操作；結果預設輸出簡短人類可讀摘要。

**Why:** 使用者已決定第一版以固定命令型為主，且未來 skill 也會優先處理明確操作命令。文字摘要較容易閱讀，也足夠支援 skill 判斷成敗；未來若需要批次模式，可在此結構上增加 `apply` 類指令。

**Alternatives considered:**
- 先做 JSON 腳本模式：較利於批次，但不符合第一版優先需求。
- 一開始就雙軌支援命令與批次：範圍擴大，拖慢交付。

### 4. 預設直接覆蓋 `.md3`，但保留明確錯誤訊息與退出碼

**Decision:** CLI 操作預設修改原檔；當伴隨檔案缺失、目標物件找不到、參數不合法時，回傳非零退出碼並輸出明確錯誤。

**Why:** 使用者已明確選擇原地覆寫。為了讓 skill 可安全使用，錯誤型態需可被可靠辨識，避免 silent failure。

**Alternatives considered:**
- 預設輸出新檔：更安全，但不符合目前使用者偏好。
- 預設自動備份：也可行，但會額外產生副檔與流程複雜度。

### 5. Skill 放在 repo 內 `.codex/skills/`，以 CLI 作為唯一寫檔入口

**Decision:** skill 只負責將需求轉為受控 CLI 指令，並在必要時讀取 CLI 幫助與參考文件；不直接重寫二進位格式。

**Why:** 這符合 `skill-creator` 的低自由度設計原則。真正脆弱的 `.md3` 寫檔動作由已測試的 CLI 執行，skill 專注於判斷命令、前置檢查與回報結果。

**Alternatives considered:**
- skill 直接修改 C# 原始碼或二進位：上下文負擔高，也無法重複驗證。
- skill 只提供文字流程不附指令：可讀性高但自動化價值有限。

### 6. Skill 內封裝 framework-dependent publish 產物

**Decision:** skill 目錄內新增 `assets/bin/`，存放 `dotnet publish` 後的 CLI 輸出；`run-md3-cli.ps1` 只執行 skill 內的發佈產物，另提供 `publish-cli.ps1` 供 repo 開發時更新 binary。

**Why:** 使用者明確希望能直接搬運 skill。若 wrapper 仍依賴 repo 內的 `.csproj`，skill 離開原始碼就無法使用。採用 framework-dependent publish 可以維持 Windows + 已安裝 `dotnet` 的前提，同時讓 skill 保持可攜。

**Alternatives considered:**
- skill 執行 repo 內 `dotnet run`：開發期方便，但無法單獨搬移。
- self-contained publish：攜帶性更高，但體積大且更新成本高。
- 只在 skill 內放 build 輸出：容易混入多餘中間產物，結構不如 publish 穩定。

## Risks / Trade-offs

- [舊 GUI 與新服務分層時產生行為落差] → 先把表單中的規則搬到服務，再讓 GUI 呼叫服務，並用測試覆蓋關鍵規則。
- [現有專案為 .NET Framework 4.8，測試支援有限] → 將新 Core 與 CLI 優先設為 SDK-style 專案，並為 Core 建立可由 `dotnet test` 執行的測試專案。
- [`.md3` 寫回依賴同名 `.bac/.til/.lyr` 檔案大小] → 在文件服務集中檢查這些檔案是否存在，缺失時立即失敗並提供清楚訊息。
- [CLI 規格若過早擴大，skill 也會變複雜] → 第一版僅支援 `inspect` 與 `PointObject` / `RangeObject` 基本 CRUD，批次腳本與高階自然語言操作留待後續 change。
- [repo 內 skill 若缺乏驗證，可能只在本次上下文有效] → 建立可執行腳本與參考文件，並以真實命令進行 smoke test。
- [skill 內 binary 與 repo 原始碼可能不同步] → 提供 `publish-cli.ps1` 並在 skill 文件中明確要求每次 CLI 變更後重新 publish。

## Migration Plan

1. 建立 OpenSpec artifacts，固定提案、規格與任務分解。
2. 新增 `TricksterMap.Core`、`TricksterMap.Cli`、測試專案，將現有核心讀寫與資料模型搬入 `Core`。
3. 逐步讓 GUI 改用 `Core` 服務，確保原本功能仍能建置。
4. 實作 CLI 命令與測試，完成 `inspect`、`point`、`range` 路徑。
5. 在 `.codex/skills/` 建立新 skill，將 CLI 操作封裝為可重用工作流。
6. 將 CLI 以 `dotnet publish` 輸出同步到 skill 的 `assets/bin/`，確保 skill 脫離 repo 原始碼後仍可執行。
6. 執行建置、測試與基本命令驗證；若 CLI 或 GUI 驗證失敗，回滾到上一個可建置狀態。

## Open Questions

- 若未來要支援批次腳本模式，是否採 JSON 還是 YAML 較適合 repo workflow。
- skill 最終是否要同步安裝到個人 `$CODEX_HOME/skills/`，或維持 repo-local 使用模式即可。
