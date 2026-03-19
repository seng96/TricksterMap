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
- A modification command fails if matching `.bac`, `.til`, or `.lyr` files are missing.

## Refresh The Bundled CLI

If you are inside the source repository and changed the CLI implementation, refresh the publish output with:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/md3-cli-automation/scripts/publish-cli.ps1
```
