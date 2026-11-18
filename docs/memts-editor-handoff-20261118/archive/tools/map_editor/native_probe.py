"""Read-only, original-byte anchors supplementing REA native Evidence.

Addresses apply only to the pinned local client SHA. This script never launches
the game and never patches map or executable files.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import struct

import capstone
import pefile

EXPECTED = '4bb6843be21f5bd52d3e61e4d3a036a8cd49d2ac9f8af47401f81f7dc80dfd43'
# Boundaries located from original call sites and int3 padding; REA independently
# determines function bodies. These spans are not assertions of entire bodies.
SPANS = {
    'config_read': (0x57a960, 0x1a0),
    'map_header_objects': (0x57c600, 0x2da),
    'compressed_lyr': (0x57c8e0, 0x1be),
    'lyr_dispatch': (0x57cbc0, 0x1f3),
    'til_dispatch': (0x57cdc0, 0x160),
    'resource_dispatch': (0x57d140, 0x280),
    'sprite_flag_bounds': (0x579a40, 0x170),
    'sprite_flag_swaps': (0x579940, 0xe0),
    'tile_dynamic_setup': (0x57df70, 0x2fa),
    'tile_flags_draw': (0x57f670, 0x465),
}


def inspect(source, output):
    raw = source.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != EXPECTED:
        raise ValueError('Client identity differs; rediscover addresses instead of reusing this profile.')
    pe = pefile.PE(data=raw)
    base = pe.OPTIONAL_HEADER.ImageBase
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    output.mkdir(parents=True, exist_ok=True)
    spans = []
    for name, (address, size) in SPANS.items():
        offset = pe.get_offset_from_rva(address-base)
        body = raw[offset:offset+size]
        rows = [dict(address=hex(i.address), bytes=i.bytes.hex(), mnemonic=i.mnemonic, operands=i.op_str)
                for i in decoder.disasm(body, address)]
        (output / (name+'.asm.txt')).write_text('\n'.join(
            f"{i['address']}: {i['bytes']:24} {i['mnemonic']:8} {i['operands']}" for i in rows)+'\n', encoding='utf-8')
        spans.append(dict(name=name, address=hex(address), file_offset=offset,
                          length=size, sha256=hashlib.sha256(body).hexdigest(), instructions=rows))
    anchors = []
    for literal in (b'MDN_', b'.md3\0', b'.lyr\0', b'.til\0', b'.bac\0', b'.eff\0'):
        for match in re.finditer(re.escape(literal), raw):
            address = base+pe.get_rva_from_offset(match.start())
            candidates = []
            for ref in re.finditer(re.escape(struct.pack('<I', address)), raw):
                candidates.append(dict(file_offset=ref.start(), address=hex(base+pe.get_rva_from_offset(ref.start()))))
            anchors.append(dict(literal=repr(literal), address=hex(address), file_offset=match.start(),
                                pointer_candidates=candidates))
    report = dict(source=str(source.resolve()), source_sha256=digest, image_base=hex(base),
                  evidence_type='original-file bytes and static Capstone disassembly, not runtime',
                  limitations=['Byte pattern pointer candidates are not typed code xrefs.',
                               'Selected spans do not prove reachability or complete function coverage.',
                               'REA Evidence is retained separately; static analysis does not observe execution.'],
                  anchors=anchors, spans=spans)
    (output/'original-byte-evidence.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(sha256=digest, spans=len(spans), anchors=len(anchors)), indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, default=Path('bin/memts1.bin'))
    parser.add_argument('--output', type=Path, default=Path('reports/map-editor-20261117/native'))
    args = parser.parse_args()
    inspect(args.client, args.output)
