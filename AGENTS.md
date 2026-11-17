# AGENTS.md

This file is the canonical guidance document for **any AI coding agent** (Claude Code, Codex, Cursor, Aider, GitHub Copilot Workspace, etc.) operating in this repository. Tool-specific guide files such as `CLAUDE.md` inherit from this one — do not duplicate content there.

## Project at a glance

C# multi-project solution for reading/writing **Trickster Online** map data files (`.md3` binary format, with companion files `.bac`, `.til`, `.lyr`). The solution has three live tracks and one legacy track:

| Project | Target | Role |
|---|---|---|
| `TricksterMap.Core` | .NET Framework 4.8 **and** .NET 10.0 (dual-target) | Headless library — format parser, serializer, data model, services. Consumed by Cli + Tests. |
| `TricksterMap.Cli` | .NET 10.0 | Console executable (`inspect`, `point {add\|update\|delete}`, `range {add\|update\|delete}`). Hand-rolled arg parser, no CommandLineParser/Spectre. |
| `TricksterMap.Tests` | .NET 10.0, **xUnit 2.9.3** | Integration tests that spawn the published CLI as a subprocess; fixtures synthesize temp `.md3` + companion files in `%TEMP%`. |
| `TricksterMap` | .NET Framework 4.8, WinForms | **Legacy** MDI GUI. Consumes the Core model/loaders; obsolete duplicate source files remain excluded from compilation. |

Both `Core` and `TricksterMap` use root namespace `TricksterMap`, with the data model under `TricksterMap.Data`. Core retains **Ionic.Zlib 1.9.1.5** as a loose legacy dependency under `packages/`; the current Scene envelope reader/writer uses platform DEFLATE and independent Adler instead. The GUI additionally uses System.Resources.Extensions and its transitive runtime DLLs. MDN revisions301/302/303 use a raw header and configuration/object sections in the verified corpus; do not assume revision303 makes the MDN body zlib-compressed. Companion compression is identified by its envelope and native reader path.

## Common commands

```powershell
# Build everything (multi-targets in Core)
dotnet build TricksterMap.sln

# Build a single project
dotnet build TricksterMap.Cli

# Run the CLI directly
dotnet run --project TricksterMap.Cli -- inspect --file C:\path\to\map.md3
dotnet run --project TricksterMap.Cli -- point add --file map.md3 --type 1 --id 100 --map-id 0 --x 256 --y 128

# Run the full test suite (xUnit; CLI must be built first because tests shell out to it)
dotnet test

# Run a single test class / method
dotnet test --filter "ClassName=InspectCommandTests"
dotnet test --filter "FullyQualifiedName~InspectCommandTests.Inspect_existing"
```

The WinForms project builds on Windows with the .NET Framework 4.8 targeting pack and .NET SDK 10. `--verify-map <md3> --output <new-dir>` exercises an offscreen, transparent, nonactivating UI without system input injection; see `EDITOR_PROTOTYPE.md`. Ship System.Resources.Extensions and its transitive runtime DLLs with the GUI.

## Architecture

### `.md3` file model (authoritative version lives in `TricksterMap.Core`)

- **Header** (96 bytes): signature `MDN_`, revision (301/302/303), tile bpp, map maximum pixel coordinates, tile size/count, then four section counts (LayerCount, RangeCount, PointCount, EffectCount). Pixel extents are maximum coordinates plus one. `Param4 & 1` selects an embedded-resource layout in the native reader and is explicitly unsupported by the editing Core until implemented.
- **Sections, in order:**
  1. **ConfigLayers** (variable): 5-int header (`Type, X, Y, BppX, BppY`) + `X*Y` bytes. Types 1–4 = collision / special-effect / ground-type / height.
  2. **RangeObjects** (32 bytes each): 8×int32 — portals, debris, monster movement, gather, NPC movement, spawn zones.
  3. **PointObjects** (24 bytes each): 6×int32 — portals, respawns, NPCs (general/shop/skill), teleport items/NPCs, NORI entities.
  4. **EffectObjects** (52 bytes each): currently parsed as raw blobs.
- **Trailing bytes** after known MDN sections are retained verbatim in `TrailingData`; their meaning is unknown. Preserve them on every save.
- **Companion files** (same basename next to the `.md3`): `.bac` (NORI scene sprites/animations), `.til` (tile images), `.lyr` (visual layer data). Config Type 1 inside MDN supplies the collision grid. Core `Load()` can inspect standalone MDN; `MapDocumentService.Save()` requires all three companions. Companion payloads may have their own compression envelopes.

### Service layer (in `TricksterMap.Core`)

The CLI is the canonical consumer; mirror this entry pattern when adding new operations.

- `MapDocumentService` (`TricksterMap.Core/MapDocumentService.cs`) — load/save orchestration, computes companion paths, returns a `MapDocument` wrapping `MapDataInfo`.
- `MapDataLoader` (`TricksterMap.Core/MapDataLoader.cs`) — binary parser.
- `MapSaveHelper` / `MapSerializer`: validated serialization and same-directory atomic save. Loaded documents preserve their aggregate header value, adjusting only by MDN serialized-size delta; no-op saves preserve all bytes. Do not replace it by the actual companion-size sum, because original files can contain discrepancies.
- `PointObjectService` / `RangeObjectService` — CRUD + validation (duplicate-ID rejection lives here; CLI relies on it).
- `CompressedFileLoader` — Ionic.Zlib wrapper for compressed payloads.
- `Scene/` — lossless LYR/TIL models/codecs, typed envelopes, tile/object edit histories, image-buffer import and blank map authoring. Scene envelopes use platform raw DEFLATE plus independent RFC1950 Adler: legacy Ionic output can have an incorrect checksum on real edited maps.
- `MapBundleExportService` — staged export of MDN plus current Scene LYR/TIL and byte-preserved BAC/EFF. New maps omit template EFF. Newly generated companions may replace a missing input; existing outputs are never overwritten.
- Data POCOs in `TricksterMap.Core/Data/`: `MapDataInfo`, `ConfigLayer`, `PointObject`, `RangeObject`, `EffectObject`. Each typed object exposes lookup helpers for type-id ↔ human name.

### CLI surface (`TricksterMap.Cli/CliApplication.cs`)

Hand-written dispatcher: `args[0]` → `inspect | point | range`; flag values pulled with `GetRequiredValue` / `GetRequiredInt`. `point` and `range` further switch on `add | update | delete`; `update` and `delete` always require `--index`. Full reference at `.codex/skills/md3-cli-automation/references/cli-commands.md`.

### Legacy WinForms project: compilation boundary

`TricksterMap/` references `TricksterMap.Core` and its explicit `.csproj` Compile list excludes obsolete duplicate `MapDataLoader.cs`, `MapSaveHelper.cs`, `CompressedFileLoader.cs` and principal `Data/` POCOs. They remain in the source tree but do not define runtime behavior. `Data/EffectObject.cs` and `Data/TileData.cs` are GUI-specific and still compiled.

Apply format fixes in Core and verify the actual Compile list before changing legacy source. Do not copy fixes into excluded files or create a third data model. New headless features should land in Core with CLI/service tests. Legacy form behavior still needs separate GUI validation.

The interactive editor now uses a fixed viewport transform for collision and visual scene pixels. `Scene/` in the GUI decodes/renders images; `SceneEditing/` provides tile/object authoring, while the authoritative mutable models remain in Core. Large scenes load through a serialized background worker with epoch/cancellation checks; only the UI thread attaches shared renderers. Dispose stale/queued images and do not give child tabs ownership of a shared renderer.

### Localization

`Strings.resx` (en) + `Strings.zh.resx` (zh-Hans) generate `Strings.Designer.cs` and a satellite resource assembly. The same `Strings.Designer.cs` is duplicated under `TricksterMap.Core/` and `TricksterMap/`. Forms call an `Extensions.SetFonts()` extension at construction to apply `Strings.PreferredFont`. Adding a new locale = ship a satellite `.resx`; the MDI parent discovers cultures by reflecting over `CultureInfo` at startup.

### OpenSpec + agent skill workflow

- `openspec/specs/<capability>/spec.md` — main specs. Capabilities present: **md3-cli-editing** (CLI surface) and **md3-agent-skill** (the skill that wraps it). Requirements are bilingual (English + Traditional Chinese).
- `openspec/changes/<name>/` — in-flight changes (currently empty; the original add-md3-cli-and-agent-skill change is under `openspec/changes/archive/`).
- `.codex/skills/openspec-*` — workflow skills (explore / new / continue / apply / ff / sync / archive / bulk-archive / verify) that automate the artifact lifecycle. Use the corresponding `/opsx:*` slash commands; the mechanical ones delegate to subagents — do not bypass that.
- `.codex/skills/md3-cli-automation/` — wraps the published CLI through `scripts/run-md3-cli.ps1`. **Never patch `.md3` bytes directly.** Always go through the CLI: it enforces companion-file presence, validates indexes, and keeps save behavior consistent.

## Modular-maintenance rules (project-wide)

The user has explicitly required this codebase be maintained modularly — **no single file is allowed to dominate.** When extending the project:

1. **Soft cap: ~300 LOC per `.cs` file** (excluding `*.Designer.cs` and `*.resx`-generated files, which are tool-managed). The current ceiling is `TricksterMap\MDIParent.cs` at 258 LOC and `TricksterMap.Cli\CliApplication.cs` at 209 LOC — both are getting close. If you have to add to either, refactor first.
2. **Split monoliths before adding new behavior.** `CliApplication.cs` already mixes dispatch + per-command logic; new commands should land in their own file (e.g., `Commands/InspectCommand.cs`) with `CliApplication` reduced to a router. The same applies to `MDIParent.cs`'s 90-line `OpenMap()` — pipeline stages (load / render / save) should be extracted before further growth.
3. **One responsibility per class.** Loaders parse, services validate, POCOs hold data, forms display. Do not bury parsing logic in a form or serialization in a service.
4. **Never copy a third "Data" model.** If you find yourself replicating a POCO again, pause and bring the WinForms tree onto Core instead.
5. **New format sections get their own loader/serializer pair.** Do not extend the central `MapDataLoader.Load()` / `MapSaveHelper.Save()` into a 400-line method — add `Read<Section>` / `Write<Section>` helpers or, better, dedicated classes.
6. **Tests track the module boundary.** New service class ⇒ new test file under `TricksterMap.Tests/<Area>/`. Do not pile assertions into `InspectCommandTests.cs` (already 511 LOC and at the edge of reasonable).

## Things to know before editing

- `Strings.Designer.cs` files are auto-generated from `.resx`. Edit the `.resx`, let the designer regen.
- No sample `.md3` files are committed. Tests synthesize maps via `TestMapFixture` into `%TEMP%\TricksterMap.Tests\<guid>\` and clean up after themselves.
- The CLI is published into `assets/bin/` under the md3 skill and invoked by the skill's wrapper script. If you change CLI argument names or output format, **update `.codex/skills/md3-cli-automation/references/cli-commands.md` and any affected spec under `openspec/specs/md3-cli-editing/`** — those are the contracts the agent skill relies on.
- The build uses NuGet's *packages.config* style (`packages/Ionic.Zlib.1.9.1.5/`), not `<PackageReference>`. If you upgrade Ionic.Zlib, update the loose DLL path in every consuming `.csproj`.
- `.vscode/settings.json` sets `dotnet.preferCSharpExtension: true`; respect this when adding new IDE config.
