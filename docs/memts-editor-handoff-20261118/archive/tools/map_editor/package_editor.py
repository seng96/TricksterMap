"""Package the tested editor and an exact source snapshot; never ships game assets."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
UPSTREAM = HERE/'upstream'
VERSION = '0.3.1'
DEST = ROOT/'deliverables/map-editor'/VERSION
BASELINE = '1d56fad372e428ccb1123f42a0795b31a9c306f0'


def git(*args):
    return subprocess.check_output(['git', '-C', str(UPSTREAM), *args])


def package():
    baseline = subprocess.run(['git', '-C', str(UPSTREAM), 'merge-base', '--is-ancestor', BASELINE, 'HEAD'])
    if baseline.returncode != 0:
        raise ValueError('Upstream HEAD must descend from the recorded baseline')
    if DEST.exists(): raise FileExistsError('Package directory must be new: '+str(DEST))
    gui = UPSTREAM/'TricksterMap/bin/Release'
    cli = UPSTREAM/'.codex/skills/md3-cli-automation/assets/bin'
    required = [gui/'TricksterMap.exe', gui/'TricksterMap.Core.dll', cli/'TricksterMap.Cli.dll']
    for path in required:
        if not path.is_file(): raise FileNotFoundError(path)
    DEST.mkdir(parents=True)
    for path in gui.iterdir():
        if path.name.startswith(('VerifyEffects', 'AuditEffects', 'CorpusRenderV3', 'ProbeTileOrientation')): continue
        if path.is_dir(): shutil.copytree(path, DEST/path.name)
        else: shutil.copy2(path, DEST/path.name)
    shutil.copytree(cli, DEST/'cli')
    for name in ('LICENSE', 'COPYING', 'CREDITS.md', 'EDITOR_PROTOTYPE.md', 'EFF_PREVIEW.md'):
        shutil.copy2(UPSTREAM/name, DEST/name)
    shutil.copy2(HERE/'PORTABLE_README.md', DEST/'README.md')
    tracked = [p.decode('utf-8') for p in git('ls-files', '-z').split(b'\0') if p]
    new = [p.decode('utf-8') for p in git('ls-files', '--others', '--exclude-standard', '-z').split(b'\0') if p]
    # Intent-to-add only records these newly authored files in this isolated
    # checkout's index, so the review patch also contains their full contents.
    if new: subprocess.run(['git', '-C', str(UPSTREAM), 'add', '-N', '--', *new], check=True)
    # Keep a complete, rebuildable implementation patch after source commits
    # and PR merges, rather than exporting only the remaining working changes.
    patch = git('diff', '--binary', BASELINE)
    (HERE/'trickstermap-editor.patch').write_bytes(patch)
    shutil.copy2(HERE/'trickstermap-editor.patch', DEST/'trickstermap-editor.patch')
    paths = {UPSTREAM/p for p in tracked+new}
    # Core uses a loose DLL reference; NuGet metadata is retained alongside it.
    packages = UPSTREAM/'packages/Ionic.Zlib.1.9.1.5'
    if packages.exists(): paths.update(p for p in packages.rglob('*') if p.is_file())
    with zipfile.ZipFile(DEST/'TricksterMap-editor-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(paths):
            if path.is_file(): archive.write(path, 'TricksterMap/'+path.relative_to(UPSTREAM).as_posix())
    entries = []
    for path in sorted(DEST.rglob('*')):
        if path.is_file():
            entries.append(dict(path=path.relative_to(DEST).as_posix(), bytes=path.stat().st_size,
                                sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    (DEST/'manifest.json').write_text(json.dumps(dict(version=VERSION, baseline=BASELINE,
        capability='map authoring; corrected TIL top-down row order and atlas import; EFF offline preview; declared material dependency checks; portable game-layout resource ZIP; full native pixel/effect fidelity not yet verified',
        files=entries), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    archive_path = DEST.parent/('TricksterMap-editor-'+VERSION+'-win.zip')
    with zipfile.ZipFile(archive_path, 'x', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(DEST.rglob('*')):
            if path.is_file(): archive.write(path, 'TricksterMap-editor-'+VERSION+'/'+path.relative_to(DEST).as_posix())
    print(json.dumps(dict(directory=str(DEST), archive=str(archive_path), files=len(entries)), indent=2))


if __name__ == '__main__': package()
