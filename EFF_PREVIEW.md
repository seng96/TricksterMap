# EFF map preview and portable resources (0.3.0)

The map editor now renders declared EFF particles in the fine viewport. Simple
collision mode remains independent. Use **預覽 → 顯示地圖 EFF 特效** to toggle them;
**播放場景動畫** advances both scene and effect clocks. A paused effect view shows
the one-second sample. Neither operation modifies map data or edit histories.

**特效素材依賴／素材根目錄** lists the EFF, original NoriFileName, resolution status,
actual selected file, CAKE/CREAM counts and warnings. Select the game directory
containing `data`, or `data` itself, when materials are outside the map folder.
An explicit root takes precedence. No global basename search is performed.
Historic author paths containing a `data/...` suffix may resolve to that exact
suffix in the selected game root; the original XML is never silently rewritten.

**檔案 → 匯出完整遊戲資源包 ZIP** creates a fresh game-relative resource tree:

```text
asset-manifest.xml
data/map/<archive-name>.md3
data/map/<archive-name>.lyr
data/map/<archive-name>.til
data/map/<archive-name>.bac
data/map/<archive-name>.eff        # when present
data/<declared material path>     # when present
```

The manifest records SHA-256 and lengths. EFF material references in the exported
copy are normalized to the collected game-relative path; attribute order and
unknown XML nodes remain in order. Missing/ambiguous material, invalid NORI metadata,
missing or ambiguous TextureName animations, duplicate definitions, or missing referenced CAKE/CREAM refuse this complete ZIP
export before creating the destination. Existing archives are never overwritten.
Original files stay unchanged. Registration of MapInfoEx/NPC/monster data remains
separate. The older flat bundle export remains available for existing workflows.

## Implementation and evidence

Core `Effects/` has a secure read-only XML model, bounded path resolver,
MDN effect-placement view, deterministic simulation, particle math/alpha/animation
and Test/Bounding/SizeResistance/Mud patterns. The 52-byte original MDN records
remain opaque for serialization. The view reads X/Y at offsets4/8 and the32-byte
CAKE name at20; unknown values including offset12 and layer hint16 are preserved.

Particle math is ported from the existing MemtsEffectStudio `native_math.py`,
whose native evidence is bound to client SHA256
`4bb6843be21f5bd52d3e61e4d3a036a8cd49d2ac9f8af47401f81f7dc80dfd43`.
Tests retain14 independent Python spawn/update vectors and its source SHA,
including exact CRT RNG state and float-tolerant numeric comparison. This is reuse
of prior evidence, not a new runtime observation or new native reverse engineering.
The legacy SUBCREAM command conversion was separately rechecked with REA:
`@Name` is the registration key used for `_LOOP_`, `end`, `_RESTART_`, `_STOP_`,
while `SubCreamName` remains the CREAM reference. Loop/Normal use the current
child-list tail time; Restart/Stop retain their own start time. The focused
carrier, source-range identity and static inference limits are recorded in the
0.3 validation report. Original XML is preserved throughout.

The GUI uses lazy NORI frames and a bounded scene image cache. Each NORI library
has a distinct cache namespace so equal bitmap indexes cannot alias between BAC
and EFF textures. A viewport-sized CPU target implements NORMAL/COPY/BLEND,
ADD, MULT, INVMULT and INVERSE equations with transparent cutout exclusion.
World-XY planar effects use the existing recovered orthographic camera projection.
Core `Scene/NoriAnimationIndex` validates the same material metadata used by both
preview and archive export. Older raw NRI files with zero length/offset tables are
accepted only when the self-sized bitmap chain and versioned sequential animation
records validate through the exact end of file. Truncated files are not relocated,
no arbitrary byte scan is used, and all compatibility recovery remains read-only.

## Deliberate preview limits

- The clock uses a fixed CRT seed and a60-second preview cycle, not the shared
  game RNG or event timeline. At most3000 emitters/particles are previewed, with
  explicit warnings at the limits.
- Source-over and the recorded blend equations are supported; full GPU alpha-test,
  texture-stage precision, lighting and per-character/native occlusion are not.
- Sort channels order particles in the overlay; they are not yet interleaved with
  every native scene/character depth boundary. Ground effects may differ behind
  foreground objects. The UI keeps this limitation visible.
- XML and material parsing is read-only. No EFF timeline/placement authoring UI or
  BAC artwork writer is implied by this preview implementation.
- EFF is optional for loading an existing map: some original maps have placement
  records without a companion. They receive a warning rather than invented effects.
  Such unresolved external runtime dependencies are retained as manifest warnings.

Validation reports in the main workspace distinguish offline numeric/render
checks, native map-load checks from0.2, and still-pending0.3 effect fidelity checks.
