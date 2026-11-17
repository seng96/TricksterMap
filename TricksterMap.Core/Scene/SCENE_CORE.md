# Scene authoring core

`SceneDocument.Load(MapDocument)` reads MDN-associated LYR/TIL files for revisions
301/302/303 without rewriting the source. `SceneLayer` retains the exact 32-byte
name, metadata, pixel/grid payload, complete object records and trailing bytes.
`SceneTileGroup` retains RGB555 tile pixels and group trailing bytes. `SceneObject`
retains all 48/56 bytes; native in-memory clamps are deliberately not serialized.

`SerializeLayers()` / `SerializeTiles()` return original file bytes when the
model has no changes. Changes rebuild offset tables, payload lengths and object
counts, then retain the original resource's raw/wrapped compression choice.
`ValidateHeaderCounts()` catches MDN-to-companion count mismatches before export.
Changing layer/group counts also requires updating the associated MDN counts.

`SceneEnvelope` handles raw resources or one typed wrapper (A0B0/C0D0/E0F0), checks
declared lengths and RFC1950 Adler32, and limits output to 512 MiB. It uses the
platform raw DEFLATE decoder because Ionic.Zlib 1.9.1.5's streaming reader fails
on some otherwise valid corpus inputs, including map_dg06. Its writer also emits
an incorrect Adler for a real edited map_sq00 LYR, so both new envelope creation
and modified output now use platform DEFLATE plus an explicitly written RFC1950
header/Adler. Independent platform ZLibStream regressions cover 5552/65521 byte
boundaries, repetitive/random payloads above 1 MiB and the real sq00 edit.
Original no-op compression bytes are retained exactly.

`SceneTileEditService` validates tile/group references and provides coalesced
strokes, inclusive rectangles, undo/redo and a saved baseline. `SceneObjectEditService`
changes existing object X/Y/Z/flags with exact-record undo, duplicates complete
records and deletes objects with structural undo snapshots. Both preserve unknown
bytes. History assumes the list/payload is not structurally changed outside the
service while it remains active.

`SceneTileImportService.Append(scene,tilesX,tilesY,tileMajorRgb555)` imports an
independent RGB555 tile-major buffer, each 32x32 tile top-down, 2-byte little
endian pixels. It checks 1..65535 tiles/group, at most256 groups, exact lengths,
header counts and resource-size limits before mutating either group list or
MDN header. PNG/BMP decoding and user-facing import undo belong to the UI.

`BlankMapService.Create(template,width,height)` creates an unregistered blank map
using template BAC/TIL resources. Dimensions must be multiples of 32, 32..16384
per axis, with at most 64M world pixels. Collision uses 16-pixel steps and starts
fully blocked. A tile Floor/Order2 layer starts with empty 0xffff cells, plus an
empty Main/Order6 layer at index1. New MDN Param14 is set to1 as a conservative
authoring convention: 694/699 supplied maps point Param14 to Order6, with five
exceptions; the complete native consumer is not confirmed. Original maps are
never normalized. Correlation evidence is in the main workspace report
`reports/map-editor-20261117/param14-main-layer-correlation.json`.

The new map clears points/ranges/effects, noncollision config and opaque MDN tail,
retains unknown header fields from its explicitly selected template, resets the
aggregate-size baseline, and returns `IncludeEffects=false` so old EFF is not
inherited. `BlankSceneResult.Document.Md3Path` still identifies the template for
resource copying: export to a new path before opening it as a standalone map.
Template TIL bytes remain identical and in-memory pixel data is independent.

No service here registers MapInfoEx/NPC/monster data or proves game runtime
compatibility. The caller must export MDN and new LYR/TIL consistently, copy BAC,
update aggregate sizes, and complete game-side validation separately.

Optional corpus tests use `MEMTS_SCENE_CORPUS` and
`MEMTS_SCENE_CORPUS_REPORT`; the supplied 699 complete maps passed byte-identical
LYR/TIL round trips. Synthetic tests additionally cover edited/rebuilt companions,
new model creation, record/tile history, corrupt resources and blank-map reopen.
