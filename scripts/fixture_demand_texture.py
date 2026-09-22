# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Bounded functional-only demand prototype, including exact off-mode replay."""
from pathlib import Path
import shutil

RELATIVE = "FixtureMenuObserver/c10-demand-texture-selection.xml"


def admit(mode, selection, purpose, automatic, menu, candidate, sparse, f):
    if mode is None:
        f.require(selection is None, "Demand selection requires demand probe")
        return None
    f.require(purpose == "functional" and automatic and menu and candidate == "candidate" and sparse,
              "Demand prototype requires functional automatic candidate and explicit mixed selection")
    receipt = {"mode": mode, "selection": None}
    if selection is not None:
        source = f.inside(f.REPO / "artifacts", Path(selection).resolve())
        f.require(source.is_file() and not source.is_symlink() and 0 < source.stat().st_size <= 1024 * 1024,
                  "Demand selection must be bounded private XML")
        receipt["selection"] = {"source": str(source), "sha256": f.digest(source), "bytes": source.stat().st_size}
    f.require(mode != "off" or receipt["selection"], "Off demand comparison requires carried natural selection")
    return receipt


def arguments(root, receipt):
    if not receipt:
        return []
    result = ["--wake-up-demand-textures=" + receipt["mode"], "--wake-up-demand-probe=on", "--wake-up-prepared=0"]
    if receipt["selection"]:
        result.append("--wake-up-demand-selection=" + str(root / "profile" / RELATIVE))
    return result


def stage(profile, receipt, f):
    if receipt and receipt["selection"]:
        target = profile / RELATIVE
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(receipt["selection"]["source"], target)
        verify(profile, receipt, f)


def verify(profile, receipt, f):
    if receipt and receipt["selection"]:
        target = f.inside(profile, profile / RELATIVE)
        entry = receipt["selection"]
        f.require(target.is_file() and target.stat().st_size == entry["bytes"] and f.digest(target) == entry["sha256"],
                  "Demand selection drift")
