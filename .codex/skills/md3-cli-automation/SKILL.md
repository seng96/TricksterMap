---
name: md3-cli-automation
description: Use when inspecting or editing TricksterMap `.md3` map files in this repository, especially for CLI-driven `inspect`, `point`, or `range` operations, or when converting a concrete map-edit request into safe command-line steps instead of manually touching the binary format.
---

# Md3 Cli Automation

## Overview

Use this skill to inspect and modify TricksterMap `.md3` files through the bundled CLI publish output. Do not edit `.md3` binaries directly; always route reads and writes through the wrapper script so validation and save behavior stay consistent.

## Quick Start

1. Confirm the user gave a concrete `.md3` path and enough fields to identify the target operation.
2. If the request is exploratory, run `inspect` first.
3. For edits, translate the request into one CLI command.
4. Report the command result and any changed object details.

Use the wrapper:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 inspect --file C:\path\to\map.md3
```

## Supported Operations

- `inspect`
  - Use when the user wants map dimensions, layer count, point count, or range count.
- `point add|update|delete`
  - Use for concrete `PointObject` CRUD requests.
  - `update` and `delete` require `--index`.
- `range add|update|delete`
  - Use for concrete `RangeObject` CRUD requests.
  - `update` and `delete` require `--index`.

Read [references/cli-commands.md](references/cli-commands.md) for the exact parameter mapping and examples.

## Workflow

### 1. Resolve the operation

- If the user asks to "看內容" or "分析這張地圖", use `inspect`.
- If the user asks to add a point or range, collect the required fields and run `add`.
- If the user asks to edit or delete an existing object, require the exact `--index`.

### 2. Guardrails

- Do not patch `.md3` bytes directly.
- Do not invent object indexes.
- If the request is ambiguous, ask for the missing path or index before running the command.
- If the user asks for batch editing or high-level restructuring, explain that this first version only supports direct single-command operations.
- If a modification command fails because a companion `.bac`, `.til`, or `.lyr` file is missing, surface that error clearly.

### 3. Execute through the wrapper

Always use the skill wrapper script instead of hand-writing `dotnet` commands:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 <args...>
```

### 4. Summarize the result

- On success, report the command that was run and the important field values that changed.
- On failure, report the CLI error verbatim enough for the user to act on it.

## Updating The Bundled CLI

- If you changed files under `TricksterMap.Core/` or `TricksterMap.Cli/`, refresh the bundled binary with:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/publish-cli.ps1
```

- This publishes the CLI into `assets/bin/`.
- Do not edit files in `assets/bin/` by hand.

## Resources

- `scripts/run-md3-cli.ps1`
  - Wrapper around the bundled publish output.
- `scripts/publish-cli.ps1`
  - Refreshes the bundled publish output from the repo source.
- `references/cli-commands.md`
  - Exact command shapes, required options, and common examples.
