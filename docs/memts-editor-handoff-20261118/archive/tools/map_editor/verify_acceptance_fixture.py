"""Independent read-only checks of the staged MDN and RFC1950 scene envelopes."""
import argparse
import hashlib
import json
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def mdn(path):
    raw = path.read_bytes()
    header = struct.unpack_from("<4s23i", raw)
    assert header[0] == b"MDN_" and header[1] in (301, 302, 303)
    offset = 96
    layers = []
    for _ in range(header[17]):
        meta = struct.unpack_from("<5i", raw, offset)
        length = meta[1] * meta[2]
        data = raw[offset + 20:offset + 20 + length]
        assert len(data) == length
        layers.append((meta, data))
        offset += 20 + length
    range_end = offset + header[18] * 32
    point_end = range_end + header[19] * 24
    effect_end = point_end + header[20] * 52
    assert effect_end <= len(raw)
    return header, layers, raw[offset:range_end], raw[range_end:point_end], raw[point_end:effect_end], raw[effect_end:]


def envelope(path, magic):
    data = path.read_bytes()
    if struct.unpack_from("<I", data)[0] != magic:
        return {"compressed": False, "payload_bytes": len(data)}
    actual_magic, length, packed = struct.unpack_from("<III", data)
    assert packed == len(data) - 12
    decoder = zlib.decompressobj()
    payload = decoder.decompress(data[12:]) + decoder.flush()
    assert decoder.eof and not decoder.unused_data and len(payload) == length
    return {"compressed": True, "payload_bytes": length, "adler32": zlib.adler32(payload)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fixture", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source = ROOT / "data/map/map_sq11.md3"
    fixture = args.fixture.resolve() / source.name
    fixture.relative_to(ROOT / "reports/map-editor-v2/game-acceptance")
    before, original_layers, original_ranges, original_points, _, _ = mdn(source)
    after, layers, ranges, points, effects, trailing = mdn(fixture)
    assert (before[8], before[9]) == (after[8], after[9]) == (3007, 2655)
    assert ranges == original_ranges and points == original_points
    assert layers[1:] == original_layers[1:]
    meta, data = layers[0]
    assert meta == original_layers[0][0] == (1, 188, 166, 16, 16)
    assert set(data) == {0, 1}
    cell = lambda x, y: data[(y // 16) * meta[1] + x // 16]
    assert cell(1512, 1288) == 1 and cell(1512, 1368) == 0 and cell(8, 8) == 1
    assert sum(value == 0 for value in data) == 29766
    names = [fixture.with_suffix(extension) for extension in (".md3", ".lyr", ".til", ".bac")]
    assert after[5] == sum(path.stat().st_size for path in names)
    sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    assert sha(source.with_suffix(".bac")) == sha(fixture.with_suffix(".bac"))
    result = {"passed": True, "map_id": 148, "dimensions": [3008, 2656],
              "original_points_and_ranges_byte_identical": True, "non_collision_layers_byte_identical": True,
              "expected_wall_and_gap_cells": True, "walkable_cells": 29766, "blocked_cells": 1442,
              "aggregate_matches_four_assets": True, "bac_byte_preserved": True,
              "layer_envelope": envelope(fixture.with_suffix(".lyr"), 0xc0d0),
              "tile_envelope": envelope(fixture.with_suffix(".til"), 0xe0f0),
              "sha256": {path.name: sha(path) for path in names},
              "gameplay_verified": False}
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("PASS: original routes/config preserved; wall/gap, aggregate, BAC and independent zlib checksums verified.")


if __name__ == "__main__":
    main()
