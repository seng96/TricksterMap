"""Build a non-runnable, address-preserving PE for bounded REA map analysis."""
from pathlib import Path
import hashlib
import json
import struct
import sys
import pefile
from native_probe import EXPECTED

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'bin/memts1.bin'
OUTPUT = ROOT/'reports/map-editor-20261117/native/client-map-focused.exe'
RANGES = [(0x57a000, 0x18000, True), (0x56b000, 0x2000, True),
          (0xa2d000, 0x2000, True), (0xab5000, 0x1000, False),
          (0xa95000, 0x1000, False)]
# Synthetic exports only seed independently located original function entries.
# The names are analyst-generated and are not recovered original symbols.
SEEDS = [0x57c600, 0x57cbc0, 0x57cdc0, 0x57d160, 0x57df70]
SEED_RVA = 0xb00000
if '--render' in sys.argv:
    RANGES[0] = (0x579000, 0x19000, True)
    OUTPUT = OUTPUT.with_name('client-map-render-focused.exe')
if '--pass1' in sys.argv:
    RANGES[0] = (0x57a000, 0x4000, True)
    SEEDS = []
    OUTPUT = OUTPUT.with_name('client-map-pass1.exe')


def build():
    raw = SOURCE.read_bytes()
    if hashlib.sha256(raw).hexdigest() != EXPECTED: raise ValueError('Unexpected client SHA256')
    pe = pefile.PE(data=raw)
    base = pe.OPTIONAL_HEADER.ImageBase
    header = bytearray(0x1000)
    header[:2] = b'MZ'
    struct.pack_into('<I', header, 0x3c, 0x80)
    header[0x80:0x84] = b'PE\0\0'
    struct.pack_into('<HHIIIHH', header, 0x84, 0x14c, len(RANGES)+bool(SEEDS), 0, 0, 0, 224, 0x102)
    optional = 0x98
    struct.pack_into('<H', header, optional, 0x10b)
    struct.pack_into('<III', header, optional+16, 0x57c600-base, RANGES[0][0]-base, 0)
    struct.pack_into('<III', header, optional+28, base, 0x1000, 0x200)
    struct.pack_into('<HHHHHH', header, optional+40, 4, 0, 0, 0, 4, 0)
    struct.pack_into('<II', header, optional+56,
                     SEED_RVA+0x1000 if SEEDS else max(a+n-base for a,n,_ in RANGES), len(header))
    struct.pack_into('<HH', header, optional+68, 3, 0)
    struct.pack_into('<IIIIII', header, optional+72, 0x100000, 0x1000, 0x100000, 0x1000, 0, 16)
    if SEEDS: struct.pack_into('<II', header, optional+96, SEED_RVA, 0x1000)
    body, copied = bytearray(), []
    for index, (address, size, code) in enumerate(RANGES):
        offset = pe.get_offset_from_rva(address-base)
        data = raw[offset:offset+size]
        if len(data) != size: raise ValueError('Incomplete source range')
        destination = len(header)+len(body)
        section = optional+224+index*40
        header[section:section+8] = f'.m{index}'.encode().ljust(8, b'\0')
        struct.pack_into('<IIIIIIHHI', header, section+8, size, address-base, size, destination,
                         0, 0, 0, 0, 0x60000020 if code else 0x40000040)
        body.extend(data)
        copied.append(dict(address=hex(address), length=size, source_file_offset=offset,
                           carrier_file_offset=destination, sha256=hashlib.sha256(data).hexdigest()))
    if SEEDS:
        seeds = bytearray(0x1000)
        struct.pack_into('<IIHHIIIIIII', seeds, 0, 0, 0, 0, 0, SEED_RVA+0x300, 1,
                         len(SEEDS), len(SEEDS), SEED_RVA+0x40, SEED_RVA+0x80, SEED_RVA+0xc0)
        name_at = 0x100
        for index, address in enumerate(SEEDS):
            struct.pack_into('<I', seeds, 0x40+index*4, address-base)
            struct.pack_into('<I', seeds, 0x80+index*4, SEED_RVA+name_at)
            struct.pack_into('<H', seeds, 0xc0+index*2, index)
            label = f'analysis_seed_{address:x}'.encode()+b'\0'
            seeds[name_at:name_at+len(label)] = label
            name_at += len(label)
        seeds[0x300:0x300+len(b'analysis-only.dll\0')] = b'analysis-only.dll\0'
        section = optional+224+len(RANGES)*40
        header[section:section+8] = b'.seeds\0\0'
        struct.pack_into('<IIIIIIHHI', header, section+8, len(seeds), SEED_RVA, len(seeds), len(header)+len(body),
                         0, 0, 0, 0, 0x40000040)
        body.extend(seeds)
    result = header+body
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(result)
    manifest = dict(source=str(SOURCE), source_sha256=EXPECTED, carrier=str(OUTPUT),
                    carrier_sha256=hashlib.sha256(result).hexdigest(), ranges=copied,
                    synthetic_function_seeds=[hex(address) for address in SEEDS],
                    limitations=['Non-runnable synthetic PE; never execute it.',
                                 'Original selected code/data bytes and VAs preserved, headers synthesized.',
                                 'Missing imports, globals and code outside copied ranges limit call and data-flow conclusions.',
                                 'REA original-client analysis hit its 330000ms startup deadline; this bounded target is a distinct analysis.'])
    OUTPUT.with_suffix('.ranges.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(path=str(OUTPUT), bytes=len(result), sha256=manifest['carrier_sha256'])))


if __name__ == '__main__': build()
