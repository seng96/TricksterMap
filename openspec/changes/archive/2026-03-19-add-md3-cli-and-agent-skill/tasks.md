## 1. 專案骨架與共用核心

- [x] 1.1 新增 `TricksterMap.Core` 類別庫並搬移 `.md3` 讀寫與資料模型
- [x] 1.2 將 `PointObject` / `RangeObject` 編輯規則抽成可重用服務
- [x] 1.3 調整現有 WinForms 專案以參考 `TricksterMap.Core`

## 2. CLI 與測試

- [x] 2.1 新增 `TricksterMap.Cli` 專案並建立命令列骨架
- [x] 2.2 建立自動化測試專案與可重複使用的 `.md3` 測試夾具
- [x] 2.3 以 TDD 實作 `inspect` 命令與相關驗證
- [x] 2.4 以 TDD 實作 `point` CRUD 命令與相關驗證
- [x] 2.5 以 TDD 實作 `range` CRUD 命令與相關驗證

## 3. Skill 與整體驗證

- [x] 3.1 在 `.codex/skills/` 建立 repo 內 `md3` 自動化 skill
- [x] 3.2 補上 skill 的參考文件或腳本資源，讓代理能穩定呼叫 CLI
- [x] 3.3 執行建置、測試與 CLI smoke test，修正問題直到可交付
