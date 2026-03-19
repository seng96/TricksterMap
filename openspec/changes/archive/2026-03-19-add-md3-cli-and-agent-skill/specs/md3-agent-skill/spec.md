## ADDED Requirements

### Requirement: Skill delegates md3 edits through the CLI
系統 MUST 提供 repo 內的 Codex skill，使代理在需要檢視或修改 `.md3` 檔案時，透過受控 CLI 命令執行，而不是直接操作二進位格式。

#### Scenario: Skill handles direct edit request
- **WHEN** 使用者要求新增、修改或刪除 `.md3` 內的 `PointObject` 或 `RangeObject`
- **THEN** skill MUST 將任務轉為對應的 CLI 命令流程

#### Scenario: Skill handles inspect request
- **WHEN** 使用者要求分析指定 `.md3` 檔案內容
- **THEN** skill MUST 先執行 CLI 的 `inspect` 命令再整理結果

### Requirement: Skill documents supported commands and guardrails
skill MUST 清楚說明支援的操作範圍、必要參數、常見錯誤情況，以及遇到超出第一版能力範圍時應如何回報。

#### Scenario: Skill receives unsupported batch editing request
- **WHEN** 使用者要求使用第一版尚未支援的批次腳本或高階複合修改
- **THEN** skill MUST 明確說明目前限制，並引導使用者拆成可支援的 CLI 操作

#### Scenario: Skill receives ambiguous edit target
- **WHEN** 使用者沒有提供足夠資訊讓 CLI 唯一定位要修改的物件
- **THEN** skill MUST 先要求補充必要資訊，再執行命令

### Requirement: Skill includes reusable execution resources
skill MUST 包含足夠的可重用資源，至少讓代理知道何時觸發 skill、如何呼叫 CLI，以及如何解讀常見結果。

#### Scenario: Skill is invoked in a fresh session
- **WHEN** 另一個代理首次載入此 skill
- **THEN** skill MUST 提供足夠的步驟與參考，讓代理能在不依賴當前對話上下文的情況下完成 `.md3` 基本操作

### Requirement: Skill bundles a portable CLI publish output
skill MUST 內含可執行的 CLI 發佈產物，讓 skill 在脫離 repo 原始碼後，仍可於 Windows 且已安裝 `dotnet` 的環境中執行。

#### Scenario: Skill runs outside the source repository
- **WHEN** 使用者將整個 skill 資料夾搬到另一台 Windows 機器，且該機器已安裝 `dotnet`
- **THEN** skill MUST 能透過 skill 內的 wrapper script 執行 bundled CLI，而不依賴 repo 內的 `.csproj` 路徑

#### Scenario: CLI source changes in the repository
- **WHEN** 開發者修改 CLI 原始碼
- **THEN** skill MUST 提供明確的更新方式，讓 bundled publish output 可以重新同步
