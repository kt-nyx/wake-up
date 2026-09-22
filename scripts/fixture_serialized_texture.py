# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Private, functional-only input for the bounded C10 serialized texture probe."""
from pathlib import Path
import shutil

FILES = ("experiment.bundle", "native.bin", "expected.xml")
RELATIVE = "WakeUp/C10SerializedProvider"


def admit(path, purpose, automatic, menu, mode, selection, f):
    if path is None:
        return None
    f.require(purpose == "functional" and automatic and menu and mode == "candidate" and selection,
              "Serialized texture probe requires functional automatic candidate and explicit sparse selection")
    source = f.inside(f.REPO / "artifacts", Path(path).resolve())
    rows = []
    for name in FILES:
        p = f.inside(source, source / name)
        f.require(p.is_file() and not p.is_symlink() and 0 < p.stat().st_size <= 8 * 1024 * 1024,
                  "Missing or oversized serialized texture input: " + name)
        rows.append(dict(path=name, kind="file", bytes=p.stat().st_size, sha256=f.digest(p)))
    return dict(source=str(source), files=rows)


def stage(profile, receipt, f):
    if receipt is None:
        return
    target = profile / RELATIVE
    target.mkdir(parents=True, exist_ok=False)
    for row in receipt["files"]:
        shutil.copy2(Path(receipt["source"]) / row["path"], target / row["path"])
    verify(profile, receipt, f)


def verify(profile, receipt, f):
    for row in receipt["files"]:
        p = f.inside(profile, profile / RELATIVE / row["path"])
        f.require(p.is_file() and p.stat().st_size == row["bytes"] and f.digest(p) == row["sha256"],
                  "Serialized texture input drift: " + row["path"])
