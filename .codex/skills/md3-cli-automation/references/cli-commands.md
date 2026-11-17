# MD3 CLI Commands

The commands below assume the skill already contains published CLI files under `assets/bin/`.

## Inspect

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 inspect --file C:\path\to\map.md3
```

Outputs:
- `MapSizeX`
- `MapSizeY`
- `ConfigLayers`
- `PointObjects`
- `RangeObjects`

## Config Grid Commands (editor prototype)

`config list --file <md3>` prints each config layer's zero-based index, type,
grid size and stored X/Y step. Cell coordinates are zero-based row-major grid
coordinates, not world pixels. Run `list` first; do not infer a layer index.

```powershell
dotnet TricksterMap.Cli.dll config list --file C:\scratch\map.md3
dotnet TricksterMap.Cli.dll config set --file C:\scratch\map.md3 --layer 0 --x 10 --y 20 --value 1
dotnet TricksterMap.Cli.dll config rect --file C:\scratch\map.md3 --layer 0 --x1 10 --y1 20 --x2 12 --y2 22 --value 0
```

Rectangle corners are inclusive and must be ordered. Raw values must be 0..255.
For collision Type 1, the GUI offers 0/1 brushes; other values are retained and
must not be interpreted as ordinary blocking cells without native evidence.
Edits validate all bounds before saving; no-op edits do not write. Modified MDN
files preserve unknown trailing data and use an atomic same-directory save.
Use a copied complete bundle for experiments; these commands edit in place.

## Point Commands

### Add

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 point add --file C:\path\to\map.md3 --type 4 --id 22 --map-id 0 --x 321 --y 654
```

### Update

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 point update --file C:\path\to\map.md3 --index 0 --type 5 --id 11 --map-id 3 --x 12 --y 34
```

### Delete

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 point delete --file C:\path\to\map.md3 --index 0
```

Notes:
- `update` and `delete` require `--index`.
- `update` preserves the existing opaque `Options` value, which is not exposed as a CLI parameter.
- `PointObject` type `1` and `2` normalize the ID to `0`.
- Duplicate IDs for the same point type are rejected.

## Range Commands

### Add

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 range add --file C:\path\to\map.md3 --type 6 --id 9 --destination 123 --x1 11 --y1 22 --x2 33 --y2 44
```

### Update

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 range update --file C:\path\to\map.md3 --index 0 --type 5 --id 88 --destination 456 --x1 100 --y1 101 --x2 200 --y2 201
```

### Delete

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/run-md3-cli.ps1 range delete --file C:\path\to\map.md3 --index 0
```

Notes:
- `update` and `delete` require `--index`.
- `update` preserves the existing opaque `Options` value, which is not exposed as a CLI parameter.
- A modification command fails if matching `.bac`, `.til`, or `.lyr` files are missing.

## Refresh The Bundled CLI

If you are inside the source repository and changed the CLI implementation, refresh the publish output with:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/publish-cli.ps1
```
