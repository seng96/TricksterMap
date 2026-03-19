## Why

目前專案只有 WinForms 介面，雖然已具備 `.md3` 地圖資料的讀寫與部分編輯能力，但無法被自動化流程穩定呼叫，也不適合作為 Agent skill 的可重用基礎。為了讓日常地圖資料維護能以指令方式執行、再進一步由 Codex skill 自動操作，需要補上可腳本化的 CLI 入口與對應技能包裝。

## What Changes

- 新增共用核心層，將 `.md3` 檔案的載入、儲存，以及 `PointObject` / `RangeObject` 編輯規則從 GUI 表單邏輯中抽離。
- 新增命令列專案，提供 `inspect` 與 `PointObject` / `RangeObject` 的基本 CRUD 指令，預設直接覆蓋原始 `.md3` 檔案。
- 調整現有 WinForms 專案，使 GUI 與 CLI 共用相同核心服務，避免規則重複與行為分岐。
- 在 repo 內新增 Agent skill，讓 Codex 能透過固定流程呼叫 CLI，自動修改 `.md3` 檔案，並將可攜式 CLI 發佈產物一併封裝進 skill。
- 補上最基本的自動化測試與驗證流程，確保 CLI 與 skill 的主要路徑可被重複驗證。

## Capabilities

### New Capabilities
- `md3-cli-editing`: 以命令列方式檢視與編輯 `.md3` 檔案，支援摘要查詢與 `PointObject` / `RangeObject` 基本 CRUD。
- `md3-agent-skill`: 提供可重用的 Codex skill，將自然語言任務轉為受控的 CLI 操作流程，用於自動修改 `.md3` 檔案。

### Modified Capabilities

無。

## Impact

- 影響現有 `TricksterMap` 專案的進入點與業務邏輯分層。
- 新增 CLI 專案與測試專案，並可能調整 solution 結構。
- 新增 repo 內 `.codex/skills/` skill 目錄、CLI 發佈產物，以及相關腳本/參考檔。
- 需要驗證與現有 `.md3` 讀寫流程的相容性，特別是檔案大小資訊與物件編輯規則。
