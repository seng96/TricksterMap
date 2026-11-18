"""Back up, stage and restore the complete native map-148 bundle.

Adventure Island 3 uses gserverc1/c2/c3; map 148 belongs to gserverc2.
Its effective current directory is Gameserver1. The native server reads visual
companions too: MD3 must never be deployed independently of LYR/TIL/BAC.
"""
import argparse
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = Path("C:/Memts/Server/Gameserver1/data/map")
NAMES = ("map_sq11.md3", "map_sq11.lyr", "map_sq11.til", "map_sq11.bac")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def replace(source, target):
    with tempfile.NamedTemporaryFile(dir=target.parent, prefix=".map148-", delete=False) as file:
        temporary = Path(file.name)
    try:
        shutil.copy2(source, temporary)
        os.replace(temporary, target)
    finally:
        if temporary.exists():
            temporary.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("prepare", "apply", "restore", "status"))
    parser.add_argument("--record", type=Path, required=True)
    parser.add_argument("--fixture", type=Path)
    args = parser.parse_args()
    record = args.record.resolve()
    record.relative_to(ROOT / "reports/map-editor-v2/game-acceptance")
    if args.action == "prepare":
        if record.exists():
            raise FileExistsError(record)
        fixture = args.fixture.resolve()
        fixture.relative_to(ROOT / "reports/map-editor-v2/game-acceptance")
        backup = record.parent / (record.stem + "-original")
        if backup.exists():
            raise FileExistsError(backup)
        files = []
        for name in NAMES:
            target = TARGET / name
            if sha(target) != sha(ROOT / "data/map" / name):
                raise ValueError("Server asset differs from inspected source: " + name)
            files.append({"name": name, "before_sha256": sha(target), "fixture_sha256": sha(fixture / name)})
        backup.mkdir()
        for name in NAMES:
            shutil.copy2(TARGET / name, backup / name)
        result = {"target": str(TARGET), "fixture": str(fixture), "backup": str(backup), "files": files,
                  "logical_channel": 3, "channel_label": "Adventure Island 3", "map_owner": "gserverc2",
                  "manager_server_name": "GameServer2", "map_id": 148, "state": "prepared"}
    else:
        result = json.loads(record.read_text(encoding="utf-8"))
        if Path(result["target"]).resolve() != TARGET.resolve() or tuple(row["name"] for row in result["files"]) != NAMES:
            raise ValueError("Unexpected target set")
        if args.action == "status":
            print(json.dumps({"state": result["state"],
                              "all_disk_files_original": all(sha(TARGET / row["name"]) == row["before_sha256"] for row in result["files"]),
                              "all_disk_files_fixture": all(sha(TARGET / row["name"]) == row["fixture_sha256"] for row in result["files"])}))
            return
        apply = args.action == "apply"
        expected = "before_sha256" if apply else "fixture_sha256"
        new_hash = "fixture_sha256" if apply else "before_sha256"
        source = Path(result["fixture"] if apply else result["backup"])
        for row in result["files"]:
            if sha(TARGET / row["name"]) != row[expected] or sha(source / row["name"]) != row[new_hash]:
                raise ValueError("Concurrent change or invalid source: " + row["name"])
        completed = []
        try:
            for row in result["files"]:
                if row[expected] != row[new_hash]:
                    replace(source / row["name"], TARGET / row["name"])
                    completed.append(row)
        except Exception:
            rollback = Path(result["backup"] if apply else result["fixture"])
            for row in reversed(completed):
                replace(rollback / row["name"], TARGET / row["name"])
            raise
        result["state"] = "applied-awaiting-gserverc2-load" if apply else "original-disk-files-restored"
    record.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"state": result["state"], "target": result["target"]}))


if __name__ == "__main__":
    main()
