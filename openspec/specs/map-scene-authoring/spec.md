# Interactive map scene authoring

## Requirements

### Requirement: View navigation preserves data
The editor MUST zoom around the pointer with the mouse wheel and pan with middle/right drag or Space+left drag. View navigation MUST NOT change collision/scene bytes or undo state. Fine/simple mode switches MUST retain the same map transform and edit history.

### Requirement: Adjustable collision brush
The collision editor MUST support square/circle footprints with diameters1..128 cells. The hover mask and brush MUST share rasterization. Dragging MUST interpolate skipped cells, clip at boundaries and record one coherent undo step.

### Requirement: Direct scene preview
Fine preview MUST read same-basename LYR/TIL/BAC and draw through the same world-pixel transform as collision data. Simple mode MUST remain available if scene resources fail. Unsupported EFF, vertex effects, lighting/occlusion and missing BAC names MUST remain visible as preview limitations.

### Requirement: Lossless companions and authored export
Unchanged LYR/TIL MUST preserve original bytes including envelopes. Edited output MUST rebuild lengths, offsets, object counts and valid RFC1950 checksums, retaining all unedited opaque data. MDN and companion changes MUST export together to a fresh bundle; rejected operations MUST preserve source files.

### Requirement: Scene authoring
The editor MUST expose tile layer/group selection, tile paint/erase/flips, per-layer undo/redo, image-atlas import and BAC object placement/transforms/duplicate/delete with raw-record preservation. A blank map MUST reuse explicitly selected template resources without copying original layout, quests or effects.

### Requirement: Validation boundaries
Offline read/write and screenshot verification MUST NOT be described as game runtime validation. New maps require separate client/server registration and in-game checks.
