"""Prepare a private client below the supplied workspace; never edit source assets."""
import argparse
import concurrent.futures
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, required=True)
    parser.add_argument("--baseline-client", type=Path, required=True)
    parser.add_argument("--fixture", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--channel", type=int, choices=(2, 3), default=3)
    args = parser.parse_args()
    output = args.output.resolve()
    output.relative_to(ROOT / ".memts")
    if output.exists():
        raise FileExistsError(output)
    fixture = args.fixture.resolve()
    fixture.relative_to(ROOT / "reports/map-editor-v2/game-acceptance")
    baseline = args.baseline_client.resolve()
    baseline.relative_to(ROOT / ".memts")
    resources = args.resources.resolve()
    names = ("map_sq11.md3", "map_sq11.bac", "map_sq11.lyr", "map_sq11.til")
    for name in names:
        if not (fixture / name).is_file():
            raise FileNotFoundError(fixture / name)
    if sha(baseline / "memts1.bin") != sha(ROOT / "bin/memts1.bin"):
        raise ValueError("Baseline executable differs from the inspected original client")
    output.mkdir()
    data = output / "data"
    data.mkdir()
    source_hashes = {}
    for name in ("memts1.bin", "ijl15.dll", "LIBEXPAT.dll", "mss32.dll",
                 "memts.ini", "memts_ui.ini", "setup.ini", "TricksterTW.ini", "GameGuard.des"):
        source = baseline / name
        if source.exists():
            shutil.copy2(source, output / name)
            source_hashes[name] = sha(source)
    for name in ("memts2.bin", "libconfig_twn.dat", "libconfig_twn.idx"):
        source = ROOT / name
        target = output / name if name == "memts2.bin" else data / name
        shutil.copy2(source, target)
        source_hashes[name] = sha(source)
    shutil.copytree(baseline / "native_skills", output / "native_skills")
    junctions = []
    for source in (resources / "data").iterdir():
        target = data / source.name
        if source.is_dir() and source.name.lower() != "map":
            quote = lambda value: "'" + str(value).replace("'", "''") + "'"
            command = "New-Item -ItemType Junction -Path " + quote(target) + " -Target " + quote(source) + " | Out-Null"
            subprocess.run(["powershell.exe", "-NoProfile", "-Command", command], check=True, capture_output=True)
            junctions.append({"path": str(target), "source": str(source)})
        elif source.is_file() and not target.exists():
            shutil.copy2(source, target)
    # Map assets are real private copies: the four authored files cannot write through a junction.
    map_root = data / "map"
    map_root.mkdir()
    tasks = []
    for source in (resources / "data/map").rglob("*"):
        target = map_root / source.relative_to(resources / "data/map")
        if source.is_dir():
            target.mkdir(exist_ok=True)
        elif source.is_file():
            tasks.append((source, target))
    def copy(pair):
        shutil.copy2(*pair)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(copy, tasks))
    for name in names:
        shutil.copy2(fixture / name, map_root / name)
    # Do not inherit unrelated template effects under the same basename.
    effect = map_root / "map_sq11.eff"
    if effect.exists():
        effect.rename(map_root / "map_sq11.original-eff-unused")
    # Match the working Dev runtime, including its JPEG proxy and startup assets.
    # A pre-entry native-hook fixture alone is not a complete playable client.
    for name in ("ijl15.dll", "ijl15_original.dll", "ActionSpy.dll", "clinkAPI.dll", "SSOWebDLL.dll",
                 "TricksterAutoLoot_Logic.dll", "libmySQL.dll", "_dbghelp.dll", "_dbghelp51.dll",
                 "filelist", "layout.bin", "splash.dmy", "version.dat", "trickster_proxy.ini"):
        source = resources / name
        if source.exists():
            shutil.copy2(source, output / name)
            source_hashes[name] = sha(source)
    for name in ("dev.exe", "memts.dll"):
        source = ROOT / "bin" / name
        shutil.copy2(source, output / name)
        source_hashes[name] = sha(source)
    # The working proxy has native payload injection disabled. Preserve that setting.
    payload = resources / "native_skills/payload.json"
    if payload.exists():
        settings = json.loads(payload.read_text(encoding="utf-8-sig"))
        if settings.get("enabled"):
            raise ValueError("Working client has a path-bound native payload enabled; inspect it before copying")
        shutil.copy2(payload, output / "native_skills/payload.json")
    launch = '@echo off\r\ncd /d "%~dp0"\r\nstart "Map editor acceptance - Island 3, Map 148" "%~dp0dev.exe"\r\n'
    (output / "Start-MapEditor-148-Test.cmd").write_text(launch, encoding="ascii", newline="")
    record = {"client": str(output), "channel": args.channel, "map_id": 148,
              "private_map_directory": True, "game_started": False, "login_attempted": False,
              "original_files_modified": False, "shared_read_only_resources": junctions,
              "source_sha256": source_hashes,
              "fixture_sha256": {name: sha(map_root / name) for name in names}}
    (output / "map-acceptance-client.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: record[key] for key in ("client", "channel", "map_id", "game_started")}), flush=True)


if __name__ == "__main__":
    main()
