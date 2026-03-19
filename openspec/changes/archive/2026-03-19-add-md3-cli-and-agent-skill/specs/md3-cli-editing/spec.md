## ADDED Requirements

### Requirement: CLI can inspect md3 map metadata and object counts
系統 MUST 提供命令列介面來讀取指定 `.md3` 檔案，並輸出足以快速判斷地圖內容的摘要資訊，至少包含地圖尺寸、圖層數量、`PointObject` 數量與 `RangeObject` 數量。

#### Scenario: Inspect existing map
- **WHEN** 使用者執行 `inspect` 命令並提供有效的 `.md3` 路徑
- **THEN** 系統 MUST 成功載入檔案並輸出摘要資訊

#### Scenario: Inspect fails when map file does not exist
- **WHEN** 使用者執行 `inspect` 命令但指定的 `.md3` 檔案不存在
- **THEN** 系統 MUST 以非零退出碼失敗並輸出明確錯誤訊息

### Requirement: CLI can create update and delete point objects
系統 MUST 提供命令列介面來新增、修改與刪除 `PointObject`，並沿用與 GUI 相同的資料驗證規則，包括型別對應、重複 ID 檢查，以及特定型別的 ID 正規化。

#### Scenario: Add point object
- **WHEN** 使用者執行 `point add` 並提供合法參數
- **THEN** 系統 MUST 將新 `PointObject` 寫入指定 `.md3` 檔案

#### Scenario: Update point object
- **WHEN** 使用者執行 `point update` 並指定既有物件與修改欄位
- **THEN** 系統 MUST 更新目標 `PointObject` 並寫回指定 `.md3` 檔案

#### Scenario: Delete point object
- **WHEN** 使用者執行 `point delete` 並指定既有物件
- **THEN** 系統 MUST 刪除目標 `PointObject` 並寫回指定 `.md3` 檔案

#### Scenario: Reject duplicate point id
- **WHEN** 使用者新增或更新 `PointObject` 造成相同型別下的重複 ID
- **THEN** 系統 MUST 以非零退出碼失敗並輸出重複 ID 錯誤

### Requirement: CLI can create update and delete range objects
系統 MUST 提供命令列介面來新增、修改與刪除 `RangeObject`，並維持與 GUI 共用的資料欄位與驗證規則。

#### Scenario: Add range object
- **WHEN** 使用者執行 `range add` 並提供合法參數
- **THEN** 系統 MUST 將新 `RangeObject` 寫入指定 `.md3` 檔案

#### Scenario: Update range object
- **WHEN** 使用者執行 `range update` 並指定既有物件與修改欄位
- **THEN** 系統 MUST 更新目標 `RangeObject` 並寫回指定 `.md3` 檔案

#### Scenario: Delete range object
- **WHEN** 使用者執行 `range delete` 並指定既有物件
- **THEN** 系統 MUST 刪除目標 `RangeObject` 並寫回指定 `.md3` 檔案

### Requirement: CLI writes changes in place and validates related files
系統 MUST 以原地覆蓋方式寫回 `.md3`，並在儲存前確認 `.bac`、`.til`、`.lyr` 等同名伴隨檔案可被解析所需的檔案大小資訊取得。

#### Scenario: Save succeeds when companion files exist
- **WHEN** 使用者執行任一修改命令且同名伴隨檔案存在
- **THEN** 系統 MUST 成功寫回 `.md3` 檔案

#### Scenario: Save fails when companion files are missing
- **WHEN** 使用者執行任一修改命令但缺少必要伴隨檔案
- **THEN** 系統 MUST 以非零退出碼失敗並指出缺失的檔案
