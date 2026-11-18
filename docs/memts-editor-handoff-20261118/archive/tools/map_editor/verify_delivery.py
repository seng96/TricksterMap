"""Verify packaged hashes, source snapshot and a real CLI cell edit on a copy."""
from pathlib import Path
import hashlib
import json
import shutil
import struct
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = ROOT/'deliverables/map-editor/0.3.1'


def verify():
    manifest = json.loads((PACKAGE/'manifest.json').read_text(encoding='utf-8'))
    for entry in manifest['files']:
        raw = (PACKAGE/entry['path']).read_bytes()
        assert len(raw) == entry['bytes']
        assert hashlib.sha256(raw).hexdigest() == entry['sha256'], entry['path']
    with zipfile.ZipFile(PACKAGE/'TricksterMap-editor-source.zip') as archive:
        assert archive.testzip() is None
        for name in ('TricksterMap.Core/ConfigLayerEditService.cs', 'TricksterMap.Core/Scene/SceneDocument.cs',
                     'TricksterMap/Scene/SceneRenderer.cs', 'TricksterMap/SceneEditing/SceneAuthoringForm.cs',
                     'TricksterMap/MapCollisionEditorForm.cs',
                     'TricksterMap.Core/Effects/EffectDocument.cs', 'TricksterMap.Core/Effects/EffectSimulation.cs',
                     'TricksterMap.Core/MapResourceArchiveService.cs', 'TricksterMap/Scene/MapEffectPreview.cs',
                     'TricksterMap.Core/Scene/SceneTilePixelCodec.cs',
                     'TricksterMap.Cli/Commands/ConfigCommand.cs'):
            assert archive.read('TricksterMap/'+name) == (ROOT/'tools/map_editor/upstream'/name).read_bytes()
    source = ROOT/'deliverables/map/map_sq00.md3'
    source_sha = hashlib.sha256(source.read_bytes()).hexdigest()
    cli = PACKAGE/'cli/TricksterMap.Cli.dll'
    with tempfile.TemporaryDirectory(prefix='TricksterMap-package-') as temporary:
        target = Path(temporary)/source.name
        for extension in ('.md3', '.bac', '.lyr', '.til', '.eff'):
            companion = source.with_suffix(extension)
            if companion.exists(): shutil.copy2(companion, target.with_suffix(extension))
        before = target.read_bytes()
        at = 96
        layer_index = None
        for index in range(struct.unpack_from('<I', before, 68)[0]):
            kind, width, height, _, _ = struct.unpack_from('<5I', before, at)
            if kind == 1:
                layer_index, cell_offset = index, at+20
                break
            at += 20+width*height
        assert layer_index is not None
        listed = subprocess.run(['dotnet', str(cli), 'config', 'list', '--file', str(target)],
                                capture_output=True, text=True, encoding='utf-8', check=True)
        value = 1 if before[cell_offset] == 0 else 0
        subprocess.run(['dotnet', str(cli), 'config', 'set', '--file', str(target), '--layer', str(layer_index),
                        '--x', '0', '--y', '0', '--value', str(value)], capture_output=True, check=True)
        after = target.read_bytes()
        differences = [i for i, (left, right) in enumerate(zip(before, after)) if left != right]
        assert len(before) == len(after) and differences == [cell_offset]
        assert after[cell_offset] == value
    assert hashlib.sha256(source.read_bytes()).hexdigest() == source_sha
    gui_reports = ['package-final-sq14a', 'package-final-sq00', 'package-final-no-tiles']
    for name in gui_reports:
        folder = ROOT/'reports/map-editor-0.3.1'/name
        assert (folder/'verification.txt').read_text(encoding='utf-8-sig').startswith('PASS:'), name
        assert (folder/'effect-resources.png').stat().st_size > 5000, name
    result = dict(manifest_files_verified=len(manifest['files']), source_archive_crc_ok=True,
                  packaged_cli_real_edit_changed_only_selected_byte=True, source_preserved=True,
                  cli_list=listed.stdout, gui_smoke_reports=gui_reports)
    output = ROOT/'reports/map-editor-0.3.1/delivery-verification.json'
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__': verify()
