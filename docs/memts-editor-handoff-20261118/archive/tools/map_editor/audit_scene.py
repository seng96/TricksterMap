"""Read-only corpus audit of MDN companions; no editable map model or writer.

Run from any directory. Only the requested JSON and optional preview are written.
The scene layout follows the existing effect_editor/map_scene.py reader.
"""
from collections import Counter
from pathlib import Path
import argparse
import hashlib
import json
import struct
import sys
import xml.etree.ElementTree as ET
import zlib

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ENVELOPES = {b'\xb0\xa0\0\0', b'\xd0\xc0\0\0', b'\xf0\xe0\0\0'}


def unpack(path):
    raw = path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if raw[:4] not in ENVELOPES:
        return raw, 'raw', digest
    if len(raw) < 12:
        raise ValueError('short compression header')
    length, packed = struct.unpack_from('<II', raw, 4)
    if length > 512 * 1024 * 1024 or packed != len(raw) - 12:
        raise ValueError('bad compression length')
    decoder = zlib.decompressobj()
    data = decoder.decompress(raw[12:], length + 1)
    if len(data) != length or not decoder.eof or decoder.unused_data:
        raise ValueError('compression payload mismatch')
    return data, raw[:4].hex(), digest


def count_array(values):
    keys, counts = np.unique(values, return_counts=True)
    return Counter({int(key): int(count) for key, count in zip(keys, counts)})


def inspect(path, counters):
    raw = path.read_bytes()
    if raw[:4] != b'MDN_':
        return None
    h = struct.unpack_from('<24I', raw)
    row = {'file': path.name, 'sha256': hashlib.sha256(raw).hexdigest(),
           'version': h[1], 'header_param3': h[3], 'header_param4': h[4],
           'pixel_maximum': list(h[8:10]), 'visual_layers': h[15],
           'tile_groups': h[16], 'effects_count': h[20], 'missing': [], 'layers': [], 'companions': {}}
    counters['versions'][h[1]] += 1
    counters['header_param3'][h[3]] += 1
    counters['header_param4'][h[4]] += 1
    counters['effect_counts'][h[20]] += 1
    for ext in ('.lyr', '.til', '.bac', '.eff'):
        companion = path.with_suffix(ext)
        if not companion.exists():
            row['missing'].append(ext)
            counters['missing'][ext] += 1
            continue
        data, encoding, digest = unpack(companion)
        row['companions'][ext] = {'file': companion.name, 'encoding': encoding,
                                 'source_sha256': digest, 'decoded_bytes': len(data)}
        counters['companion_encodings'][f'{h[1]}:{ext}:{encoding}'] += 1
        if ext == '.eff':
            row['eff'] = {'size': len(data), 'prefix_hex': data[:24].hex(),
                          'sha256': hashlib.sha256(data).hexdigest()}
            counters['eff_prefixes'][data[:4].hex()] += 1
            if b'<!DOCTYPE' in data.upper() or b'<!ENTITY' in data.upper():
                raise ValueError('EFF contains unsupported DTD/entity declaration')
            root = ET.fromstring(data)
            row['eff']['xml_root'] = root.tag
            row['eff']['attributes'] = root.attrib
            row['eff']['cake_count'] = len(root.findall('CAKE'))
            row['eff']['cream_count'] = len(root.findall('CREAM'))
            nori = root.get('NoriFileName', '').strip().replace('\\', '/')
            resource = Path(nori)
            logical = nori.lstrip('/')
            if logical.lower().startswith('data/map/'):
                resource = path.parent / logical[len('data/map/'):]
            elif nori and not resource.is_absolute():
                resource = path.parent / resource
            row['eff']['nori_resolves_in_corpus'] = bool(nori) and resource.is_file()
            row['eff']['nori_resolution_policy'] = 'data/map prefix maps to corpus; relative paths resolve next to EFF; absolute author paths are not remapped'
            counters['eff_xml_roots'][root.tag] += 1
            if nori and not resource.is_file():
                counters['eff_nori_unresolved'][nori] += 1
        if ext != '.lyr':
            continue
        table = h[15] * 4
        offsets = struct.unpack_from(f'<{h[15]}I', data)
        if tuple(sorted(offsets)) != offsets:
            raise ValueError('unordered LYR offsets')
        stride = 48 if h[1] == 301 else 56
        for index, offset in enumerate(offsets):
            start = table + offset
            end = table + offsets[index + 1] if index + 1 < len(offsets) else len(data)
            if start + 72 > end or end > len(data):
                raise ValueError('LYR layer header out of bounds')
            kind, order, a, b, x, y, length, width, height = struct.unpack_from('<9i', data, start + 32)
            if min(length, width, height) < 0 or start + 72 + length > end:
                raise ValueError('LYR dimensions or payload out of bounds')
            tail = start + 68 + length
            count = struct.unpack_from('<I', data, tail)[0]
            if tail + 4 + stride * count != end:
                raise ValueError('LYR object stride mismatch')
            entry = {'index': index, 'kind': kind, 'order': order,
                     'a_b': [a, b], 'offset': [x, y], 'size': [width, height],
                     'payload_bytes': length, 'objects': count, 'object_stride': stride,
                     'nonzero_object_flags': 0, 'nonzero_extra_pairs': 0, 'object_anomaly_samples': []}
            counters['layer_kinds'][kind] += 1
            counters['layer_orders'][order] += 1
            counters['layer_a_b'][f'{a},{b}'] += 1
            counters['object_strides'][stride] += count
            if kind == 2:
                if length != width * height * 4:
                    raise ValueError('LYR tile count mismatch')
                grid = np.frombuffer(data, dtype='<u4', count=width * height, offset=start + 68)
                flags = count_array(grid >> 24)
                occupied = grid[(grid & 65535) != 65535]
                visible_flags = count_array(occupied >> 24)
                counters['tile_flags_all'].update(flags)
                counters['tile_flags_nonempty'].update(visible_flags)
                entry['tile_flags_nonempty'] = dict(visible_flags)
                entry['empty_tiles'] = int(np.count_nonzero((grid & 65535) == 65535))
            for number in range(count):
                at = tail + 4 + stride * number
                flags, px, py, pz = struct.unpack_from('<4i', data, at)
                counters['object_flags'][flags] += 1
                if flags:
                    entry['nonzero_object_flags'] += 1
                if pz:
                    counters['object_nonzero_z_maps'][path.name] += 1
                if stride == 56:
                    pair = struct.unpack_from('<2i', data, at + 48)
                    counters['object_extra_pairs'][','.join(map(str, pair))] += 1
                    if any(pair):
                        entry['nonzero_extra_pairs'] += 1
                if (flags or (stride == 56 and any(pair))) and len(entry['object_anomaly_samples']) < 3:
                    entry['object_anomaly_samples'].append({'index': number, 'lyr_decoded_offset': at,
                                                            'raw_hex': data[at:at+stride].hex()})
            row['layers'].append(entry)
    return row


def preview(path, destination):
    # Reuses the established read-only renderer, with its limitations displayed.
    sys.path.insert(0, str(ROOT / 'tools/effect_editor'))
    from map_scene import MapScene
    from PIL import ImageDraw, ImageFont
    scene = MapScene(path)
    image = scene.render((1280, 800), 0.5, (640, 400))
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, 1280, 55), fill=(15, 22, 35, 235))
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 16)
    draw.text((14, 9), f'{path.name} — STATIC APPROXIMATION / not game pixel validation', font=font, fill='white')
    draw.text((14, 31), f'First animation frame only; unknown tile flags and occlusion are not emulated. Warnings: {len(scene.warnings)}', font=font, fill='#ffcc88')
    destination.parent.mkdir(parents=True, exist_ok=True)
    image.save(destination)
    return {'map': path.name, 'warnings': sorted(scene.warnings), 'output': str(destination)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--maps', type=Path, default=ROOT / 'deliverables/map')
    parser.add_argument('--output', type=Path, default=ROOT / 'reports/map-editor-20261117/scene-corpus.json')
    parser.add_argument('--preview', type=Path)
    args = parser.parse_args()
    names = ['versions', 'header_param3', 'header_param4', 'effect_counts', 'missing',
             'companion_encodings', 'eff_prefixes', 'layer_kinds', 'layer_orders', 'layer_a_b',
             'object_strides', 'tile_flags_all', 'tile_flags_nonempty', 'object_flags',
             'object_extra_pairs', 'object_nonzero_z_maps', 'eff_xml_roots', 'eff_nori_unresolved']
    counters = {key: Counter() for key in names}
    records, errors, ignored = [], [], []
    for path in sorted(args.maps.glob('*.md3')):
        try:
            row = inspect(path, counters)
            if row is None:
                ignored.append(path.name)
            else:
                records.append(row)
        except (ValueError, struct.error, OSError, zlib.error, ET.ParseError) as exc:
            errors.append({'file': path.name, 'error': str(exc)})
    report = {'scope': str(args.maps.resolve()), 'maps': len(records), 'ignored_non_MDN': ignored,
              'errors': errors, 'counters': {key: dict(value) for key, value in counters.items()},
              'records': records, 'evidence_type': 'read-only static corpus parsing',
              'limitations': ['No client or server execution.', 'Statistics do not establish unknown field semantics.',
                              'No LYR/TIL/BAC/EFF writing or roundtrip verification.']}
    if args.preview:
        report['preview'] = preview(args.maps / 'map_sq00.md3', args.preview)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'maps': len(records), 'errors': errors, 'versions': dict(counters['versions']),
                      'header_param4': dict(counters['header_param4']),
                      'layers': sum(counters['layer_kinds'].values()),
                      'objects': sum(counters['object_flags'].values()),
                      'tile_flags_nonempty': dict(counters['tile_flags_nonempty']),
                      'eff_nori_unresolved': dict(counters['eff_nori_unresolved'])}, ensure_ascii=False, indent=2))
    return bool(errors)


if __name__ == '__main__':
    raise SystemExit(main())
