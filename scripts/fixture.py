# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Permanent Windows DRM-free Wake-Up fixture. Standard-library Python 3.11+.

No source writes, links, game installation, automatic refresh, or implicit launch.
Machine payload and all detailed records live exclusively in CANONICAL.
"""
from __future__ import annotations

import argparse
import contextlib
import ctypes
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time
import uuid
import zipfile
import xml.etree.ElementTree as ET
import types
try:
    import fixture_audio_bootstrap
    import fixture_serialized_texture
    import fixture_demand_texture
except ModuleNotFoundError:
    from scripts import fixture_audio_bootstrap, fixture_serialized_texture, fixture_demand_texture

# Private fixture paths, package ID and v1 record schemas retain their original
# spelling. Existing inventories and captures are immutable; they are not branding.
CANONICAL = Path(r"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance")
REPO = Path(__file__).resolve().parents[1]
PACKAGE_ID = "local.rimworldloadingoptimizer.op7validation"
PACKAGE_LEAF = "RimWorldLoadingOptimizer-OP7-LocalValidation"
MENU_ID = "local.fixture.menuobserver"
MENU_LEAF = "FixtureMenuObserver"
USER_SETTINGS_FILE = "Mod_" + PACKAGE_LEAF + "_WakeUpMod.xml"
REVIEWED_HELPER_BUNDLE = "dc7a94213b77353570eb26ce52e7376f2b9fe7d76167b4b41afe737ec097957a"
REVIEWED_HELPER_SHA = "b351d1794b82f2310f643c307d54498693db8c6818380bc6dd5e4141489023e5"
C08_HELPER_SHA = "ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36"
HELPER_PATH = "Tools/win-x64/WakeUp.TextureHelper.exe"
USER_BOOLEAN_SETTINGS = frozenset({
    "translationApplication", "assetRouting", "deferredAudio", "streamingXml", "backgroundLoading", "loadingDisplay",
    "loadingDisplayDiagnostics", "hideLoadingSummary", "loadingTimings", "pngCache", "preparedTextures", "compressTextureStorage", "psdSupport", "firstBuildImages", "staticAtlases", "atlasBatching", "orderedInput", "parsedXml", "processedXml", "resolvedInheritance", "xmlQueryExtensions", "parsedLanguage",
})
XML_EXPANDED_MODS = frozenset({
    "ceteam.combatextended", "vr.missilegirl",
    "oskarpotocki.vanillafactionsexpanded.core", "vanillaexpanded.vgeneticse",
    "oskarpotocki.vfe.medieval2", "vanillaracesexpanded.android",
})
# Historical snapshot observation only; never use it as deployed-package admission.
PINNED_GAME = "5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a"
DEVELOPMENT_GAME = "4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28"
STEAM_GAME = "5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a"
GAME_TARGETS = {"gog-rev573": DEVELOPMENT_GAME, "steam-rev590": STEAM_GAME}


def runtime_contract(root):
    m, gen, _ = current(root)
    contract = m.get("runtimeContract", {"target": "gog-rev573", "gameAssemblySha256": DEVELOPMENT_GAME})
    require(GAME_TARGETS.get(contract.get("target")) == contract.get("gameAssemblySha256"), "Unreviewed runtime contract")
    require(digest(root / "game/RimWorldWin64_Data/Managed/Assembly-CSharp.dll") == contract["gameAssemblySha256"], "Active runtime identity drift")
    return contract


def stage_steam(root, source):
    """Switch the sole active game; preserve frozen evidence and every old byte in recovery."""
    audit(root)
    m, gen, state = current(root)
    require(runtime_contract(root)["target"] == "gog-rev573", "Steam staging starts only from the frozen GOG generation")
    source = physical(source)
    require(not source.is_relative_to(root), "Steam source must be outside the fixture")
    require((source / "Version.txt").read_text().strip() == "1.6.4871 rev590"
            and digest(source / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll") == STEAM_GAME, "Unreviewed Steam version")
    gid = time.strftime("steam-rev590-%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8]
    stage = inside(root, root / "staging" / gid)
    stage.mkdir(parents=True)
    # Mods are the existing frozen copies, never refreshed from normal Steam.
    rows = scan(source, exclude=("Mods",))
    # copy_stable expects its selected source tree to match; copy top-level units
    # separately so excluding Mods cannot weaken any unit's checks.
    incoming = stage / "game"
    incoming.mkdir()
    for item in source.iterdir():
        if item.name == "Mods":
            continue
        physical(item)
        print("Stage Steam " + item.name, flush=True)
        if item.is_dir():
            copy_stable(item, incoming / item.name)
        else:
            before = digest(item)
            shutil.copy2(item, incoming / item.name)
            require(before == digest(item) == digest(incoming / item.name), "Steam file changed during copy")
    require(rows == scan(source, exclude=("Mods",)), "Steam source changed while staging")
    runtime = inventory(incoming)
    for row in runtime:
        if row["kind"] == "file":
            require(digest(source / row["path"]) == row["sha256"], "Steam source content changed")
    shutil.copytree(gen / "inventories", stage / "inventories")
    shutil.copytree(gen / "seed", stage / "seed")
    write(stage / "inventories/game.json", runtime)
    official = []
    for old in m["official"]:
        actual = package(incoming / "Data" / old["leaf"], "official")
        require(actual["id"] == old["id"], "Steam official package identity mismatch")
        inv = inventory(incoming / "Data" / old["leaf"])
        write(stage / old["inventory"], inv)
        official.append(dict(old, source=str(source / "Data" / old["leaf"]),
            inventorySha256=digest(stage / old["inventory"]), files=sum(r["kind"] == "file" for r in inv),
            bytes=sum(r.get("bytes", 0) for r in inv), provenance="Exact Steam rev590 copy; frozen mods unchanged"))
    contract = {"target": "steam-rev590", "gameAssemblySha256": STEAM_GAME}
    updated = dict(m, generation=gid, repositoryHead=git("rev-parse", "HEAD"), official=official,
        runtimeContract=contract, gameInventorySha256=digest(stage / "inventories/game.json"),
        steamVersion="1.6.4871 rev590", steamSource=str(source),
        frozenParent=state, developmentRuntimePolicy="explicit-isolated-Steam-qualification",
        developmentRuntimeSha256=STEAM_GAME)
    write(stage / "manifest.json", updated)
    final = root / "snapshots" / gid
    stage.rename(final)
    new_state = {"generation": gid, "manifestSha256": digest(final / "manifest.json")}
    write(final / "incoming-current.json", new_state)
    replacements = []
    names = {p.name for p in (root / "game").iterdir() if p.name != "Mods"} | {p.name for p in (final / "game").iterdir()}
    for name in sorted(names):
        item = final / "game" / name
        replacements.append((item if item.exists() else None, root / "game" / name))
    for name in ("candidate.json", "excluded-mods.json"):
        if (root / name).exists():
            receipt = dict(read(root / name), generation=gid)
            if name == "excluded-mods.json": receipt["manifestSha256"] = new_state["manifestSha256"]
            write(final / ("incoming-" + name), receipt)
            replacements.append((final / ("incoming-" + name), root / name))
    shutil.copytree(final / "seed", final / "incoming-profile")
    replacements.extend([(final / "incoming-current.json", root / "current.json"),
                         (final / "incoming-profile", root / "profile"), (None, root / "prepared.json"),
                         (None, root / "menu-observer.json"), (None, root / "game/Mods" / MENU_LEAF)])
    tid = swap(root, replacements, "stage-Steam-rev590-preserve-GOG")
    write(final / "transition.json", {"transaction": tid, "parent": state, "runtimeContract": contract})
    return {"generation": gid, "runtimeContract": contract, "transaction": tid, "audit": audit(root)}


def stage_gog(root, transaction):
    """Copy the authenticated GOG recovery into a new, reversible active generation."""
    audit(root)
    m, gen, state = current(root)
    require(runtime_contract(root)["target"] == "steam-rev590", "GOG restoration starts only from Steam")
    transition_path = inside(gen, gen / "transition.json")
    transition = read(transition_path)
    tid = token(transaction)
    require(transition["transaction"] == tid and transition["parent"] == m["frozenParent"]
            and transition["runtimeContract"] == m["runtimeContract"], "GOG recovery relationship mismatch")
    recovery = inside(root, root / "recovery" / tid)
    journal_path = inside(recovery, recovery / "committed.json")
    journal = read(journal_path)
    require(journal["id"] == tid and journal["purpose"] == "stage-Steam-rev590-preserve-GOG",
            "Not a committed GOG-to-Steam transition")
    require(not (recovery / "rolled-back.json").exists() and not (recovery / "recovered.json").exists(),
            "GOG recovery transaction was already unwound")
    parent = transition["parent"]
    oldgen = inside(root, root / "snapshots" / token(parent["generation"]))
    require(digest(inside(oldgen, oldgen / "manifest.json")) == parent["manifestSha256"], "GOG parent manifest drift")
    old = read(oldgen / "manifest.json")
    contract = {"target": "gog-rev573", "gameAssemblySha256": DEVELOPMENT_GAME}
    require(old["canonicalRoot"] == str(root) and old["schema"] == "rlo-drm-fixture.v1"
            and old["generation"] == parent["generation"]
            and old.get("runtimeContract", contract) == contract
            and old["developmentRuntimeSha256"] == DEVELOPMENT_GAME, "Wrong GOG parent identity")
    for key in ("packages", "activeOrder", "profileInventorySha256"):
        require(old[key] == m[key], "Frozen GOG/Steam metadata differs: " + key)

    def rows_for(generation, name, expected):
        path = inside(generation, generation / name)
        require(digest(path) == expected, "Restoration inventory identity drift: " + name)
        rows = read(path)
        seen = set()
        for row in rows:
            relative = row["path"]
            require(isinstance(relative, str) and "\\" not in relative and ":" not in relative
                    and all(p not in ("", ".", "..") for p in relative.split("/"))
                    and not Path(relative).is_absolute(), "Unsafe restoration inventory path")
            require(relative.casefold() not in seen, "Duplicate restoration inventory path")
            seen.add(relative.casefold())
            require(row["kind"] in ("file", "directory"), "Invalid restoration inventory kind")
        return rows

    oldrows = rows_for(oldgen, "inventories/game.json", old["gameInventorySha256"])
    steamrows = rows_for(gen, "inventories/game.json", m["gameInventorySha256"])
    seedrows = rows_for(oldgen, "inventories/profile.json", old["profileInventorySha256"])
    names = {r["path"].split("/")[0] for r in oldrows}
    steamnames = {r["path"].split("/")[0] for r in steamrows}
    require("mods" not in {n.casefold() for n in names | steamnames}, "Runtime inventory includes Mods")
    verify_tree(root / "game", steamrows, True, ("Mods",))
    verify_tree(oldgen / "seed", seedrows, True)
    verify_tree(gen / "seed", seedrows, True)
    allowed = {root / "game" / n for n in names | steamnames}
    controls = {"candidate.json": "incoming-candidate.json", "excluded-mods.json": "incoming-excluded-mods.json",
                "current.json": "incoming-current.json", "profile": "incoming-profile",
                "prepared.json": None, "menu-observer.json": None, "game/Mods/" + MENU_LEAF: None}
    allowed.update(root / n for n in controls)
    mapping = {}
    for index, item in enumerate(journal["items"]):
        target = inside(root, item["target"])
        backup = inside(recovery, item["backup"])
        require(target in allowed and target not in mapping and backup == recovery / str(index),
                "Foreign/duplicate GOG recovery target or backup")
        if target.parent == root / "game":
            expected = gen / "game" / target.name if target.name in steamnames else None
        else:
            leaf = controls[target.relative_to(root).as_posix()]
            expected = gen / leaf if leaf else None
        incoming = inside(gen, item["incoming"]) if item["incoming"] is not None else None
        require(incoming == expected, "Foreign GOG recovery incoming path")
        mapping[target] = backup
    required = {root / "game" / n for n in names | steamnames}
    required.update(root / n for n in controls if n not in ("candidate.json", "excluded-mods.json"))
    require(required <= mapping.keys(), "Missing GOG recovery mapping")
    require(read(mapping[root / "current.json"]) == parent, "GOG recovery pointer mismatch")
    for name in names:
        require(mapping[root / "game" / name].exists(), "Missing GOG recovery unit: " + name)
    exclusions = excluded_mods(root, m, state)
    for row in exclusions:
        pkg = next(p for p in m["packages"] if p["id"] == row["id"])
        inv = rows_for(gen, pkg["inventory"], pkg["inventorySha256"])
        verify_tree(inside(root, row["parked"]), inv, True)
    needed = sum(r.get("bytes", 0) for r in oldrows) + 2 * sum(r.get("bytes", 0) for r in seedrows)
    require(shutil.disk_usage(root).free >= needed + 64 * 1024 * 1024, "Insufficient space to retain both generations")
    gid = time.strftime("gog-rev573-%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8]
    stage = inside(root, root / "staging" / gid)
    incoming_game = stage / "game"
    incoming_game.mkdir(parents=True)
    for name in sorted(names):
        source = mapping[root / "game" / name]
        print("Stage GOG " + name, flush=True)
        if source.is_dir():
            copy_stable(source, incoming_game / name)
        else:
            before = digest(source)
            shutil.copy2(source, incoming_game / name)
            require(before == digest(source) == digest(incoming_game / name), "GOG recovery changed during copy")
    verify_tree(incoming_game, oldrows, True)
    require(digest(incoming_game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll") == DEVELOPMENT_GAME
            and (incoming_game / "Version.txt").read_text().strip() == "1.6.4871 rev573"
            and (incoming_game / "goggame-1094900565.info").is_file(), "Wrong staged GOG runtime")
    copy_stable(gen / "inventories", stage / "inventories")
    copy_stable(oldgen / "seed", stage / "seed", seedrows)
    shutil.copy2(oldgen / "inventories/game.json", stage / "inventories/game.json")
    require({(p["id"], p["leaf"]) for p in old["official"]} ==
            {(p["id"], p["leaf"]) for p in m["official"]}, "Official package selection mismatch")
    for pkg in old["official"]:
        inv = rows_for(oldgen, pkg["inventory"], pkg["inventorySha256"])
        content = inside(incoming_game, incoming_game / "Data" / pkg["leaf"])
        verify_tree(content, inv, True)
        require(package(content, "official")["id"] == pkg["id"], "GOG official package identity mismatch")
        shutil.copy2(oldgen / pkg["inventory"], inside(stage, stage / pkg["inventory"]))
    provenance = {"steamParent": state, "gogParent": parent, "recoveryTransaction": tid,
                  "recoveryJournalSha256": digest(journal_path), "steamTransitionSha256": digest(transition_path)}
    updated = dict(m, generation=gid, repositoryHead=git("rev-parse", "HEAD"), official=old["official"],
                   runtimeContract=contract, gameInventorySha256=old["gameInventorySha256"], frozenParent=state,
                   developmentRuntimePolicy=old["developmentRuntimePolicy"], developmentRuntimeSha256=DEVELOPMENT_GAME,
                   gogRecovery=provenance)
    updated.pop("steamSource", None)
    updated.pop("steamVersion", None)
    write(stage / "manifest.json", updated)
    copy_stable(stage / "seed", stage / "incoming-profile", seedrows)
    final = inside(root, root / "snapshots" / gid)
    stage.rename(final)
    new_state = {"generation": gid, "manifestSha256": digest(final / "manifest.json")}
    write(final / "incoming-current.json", new_state)
    replacements = [(final / "game" / n if n in names else None, root / "game" / n)
                    for n in sorted(names | steamnames)]
    if (root / "excluded-mods.json").exists():
        write(final / "incoming-excluded-mods.json", dict(read(root / "excluded-mods.json"),
              generation=gid, manifestSha256=new_state["manifestSha256"]))
        replacements.append((final / "incoming-excluded-mods.json", root / "excluded-mods.json"))
    replacements.extend([(final / "incoming-current.json", root / "current.json"),
                         (final / "incoming-profile", root / "profile")])
    replacements.extend((None, root / n) for n in ("candidate.json", "prepared.json", "menu-observer.json",
                        "game/Mods/" + PACKAGE_LEAF, "parked-rlo/package", "game/Mods/" + MENU_LEAF))
    newtid = swap(root, replacements, "stage-GOG-rev573-preserve-Steam")
    write(final / "transition.json", dict(provenance, transaction=newtid, runtimeContract=contract))
    verify_tree(root / "game", oldrows, True, ("Mods",))
    return {"generation": gid, "transaction": newtid, "runtimeContract": runtime_contract(root),
            "provenance": provenance, "matchingBuildsRequired": True, "audit": audit(root)}


def bind_comparison_package(root, source):
    """Copy an authenticated retained/public package into the private two-file fixture contract."""
    contract = runtime_contract(root)
    source = physical(source)
    release = source / "release-manifest.json"
    if release.exists():
        origin = read(release)
        public = (origin.get("sourceRevision") == "b2d83c324a1fdf6095d927d6ed12c695f4a49d5f"
                  and origin.get("version") == "0.2.1")
        retained = (origin.get("sourceRevision") == "ed36d07cb9135f335c74ae454b1d85ea32a0087a"
                    and origin.get("buildSourceRevision") == "36d739b1497c576e74e337e224bb1712f2337661"
                    and origin.get("version") == "0.3.0-rc.2"
                    and digest(release) == "a175b2f846a7c67df0ba76f397320a236b4570c7103bf225a02ebd9110081adf")
        require(public or retained, "Only the assigned public baseline or accepted retained archive is admitted")
        for name, sha in origin["files"].items():
            require(digest(inside(source, source / name)) == sha, "Public release content drift: " + name)
        expected_dll = ("47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d" if public else
                        "6cdf5fe145760de3c7570a3c0ad5ef8f139d57d32e20a97cf6b4d05b26d74b07")
        require(digest(source / "Assemblies/WakeUp.dll") == expected_dll, "Wrong assigned comparison DLL")
        origin_receipt = release
        reference = origin["buildReferenceTarget"]
    else:
        origin_receipt = source.with_suffix(".json")
        origin = read(origin_receipt)
        verify_tree(source, origin["files"], True)
        # Preserve exact reviewed bytes when checking the Steam compatibility
        # correction against GOG after the platform transaction is restored.
        require((origin.get("sourceRevision"), digest(source / "Assemblies/WakeUp.dll")) in {
            ("d584eb36aa68cd374a26621f97295dbb63626a33", "59ed42a69895930e8c5cfe04c903005f925cdf063beb02ffe6329244faac8825"),
            ("67265e5696c80c9a461423722870a742054dd43b", "a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda")},
            "Wrong accepted retained candidate")
        reference = origin["referenceTarget"]
    destination = REPO / "artifacts/comparison-package" / contract["target"] / origin["sourceRevision"]
    require(not destination.exists(), "Comparison package already exists; use its immutable receipt")
    (destination / "About").mkdir(parents=True)
    (destination / "Assemblies").mkdir()
    shutil.copy2(REPO / "validation/fixture-package/About/About.xml", destination / "About/About.xml")
    shutil.copy2(source / "Assemblies/WakeUp.dll", destination / "Assemblies/WakeUp.dll")
    require(digest(destination / "Assemblies/WakeUp.dll") == digest(source / "Assemblies/WakeUp.dll"), "Comparison DLL copy drift")
    write(destination.with_suffix(".json"), {"sourceRevision": origin["sourceRevision"],
        "referenceTarget": reference, "runtimeContract": contract, "files": inventory(destination),
        "comparisonProjection": {"originalPackage": str(source), "originalReceiptSha256": digest(origin_receipt),
            "originalBuildSourceRevision": origin.get("buildSourceRevision", origin["sourceRevision"]),
            "originalAboutSha256": digest(source / "About/About.xml"), "metadataOnly": True,
            "policy": "Unchanged authenticated DLL; private About identity; runtime binding is test admission, not qualification"}})
    return {"package": str(destination), "sourceRevision": origin["sourceRevision"], "runtimeContract": contract}


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def encoded(value):
    return (json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(",", ":")) + "\n").encode()


def identity(value):
    return hashlib.sha256(encoded(value)).hexdigest()


def read(path):
    return json.loads(Path(path).read_bytes())


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
    with temp.open("xb") as stream:
        stream.write(encoded(value))
        stream.flush()
        os.fsync(stream.fileno())
    # A short-lived Windows reader can deny replacement while the launcher is
    # publishing its child PID. Retain the original and temp file if the lock
    # persists; never lose the launch record or silently accept a partial write.
    for attempt in range(10):
        try:
            os.replace(temp, path)
            return
        except PermissionError:
            if attempt == 9:
                raise
            time.sleep(0.05)


def physical(path):
    """Check ancestors without resolving links away; tree scans check descendants."""
    path = Path(os.path.abspath(path))
    for item in [path, *path.parents]:
        if os.path.lexists(item):
            s = item.lstat()
            require(not (getattr(s, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)
                    and not stat.S_ISLNK(s.st_mode), f"Reparse point refused: {item}")
    return path


def inside(root, path):
    root, path = physical(root), physical(path)
    require(path != root and path.is_relative_to(root), f"Not a fixture child: {path}")
    return path


def token(value):
    require(bool(re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}", value)), "Invalid label")
    return value


def scan(root, exclude=()):
    """File and directory metadata, rejecting links before descending."""
    root = physical(root)
    require(root.is_dir(), f"Missing directory: {root}")
    result = []
    def visit(directory, prefix=""):
        with os.scandir(directory) as stream:
            entries = sorted(stream, key=lambda e: e.name)
        for entry in entries:
            relative = prefix + entry.name
            if relative in exclude:
                continue
            s = entry.stat(follow_symlinks=False)
            require(not (getattr(s, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)
                    and not stat.S_ISLNK(s.st_mode), f"Reparse point refused: {entry.path}")
            if stat.S_ISDIR(s.st_mode):
                result.append({"path": relative, "kind": "directory"})
                visit(entry.path, relative + "/")
            else:
                require(stat.S_ISREG(s.st_mode), f"Nonregular file refused: {entry.path}")
                result.append({"path": relative, "kind": "file", "bytes": s.st_size,
                               "mtime_ns": s.st_mtime_ns})
    visit(root)
    return result


def metadata(rows):
    return [{k: v for k, v in row.items() if k != "sha256"} for row in rows]


def inventory(root, exclude=()):
    rows = scan(root, exclude)
    for row in rows:
        if row["kind"] == "file":
            row["sha256"] = digest(root / row["path"])
    require(metadata(rows) == scan(root, exclude), f"Source changed during hashing: {root}")
    return rows


def copy_stable(source, destination, rows=None):
    """Hash copy, then independently hash source AND destination before accepting.

    A changed package fails as a unit; a later explicit refresh can retry it.
    No mutation of source and no silent acceptance of mixed package versions.
    """
    rows = scan(source) if rows is None else metadata(rows)
    destination.mkdir(parents=True, exist_ok=False)
    copied = []
    for row in rows:
        target = destination / row["path"]
        original = source / row["path"]
        if row["kind"] == "directory":
            target.mkdir(parents=True, exist_ok=True)
            copied.append(dict(row))
            continue
        physical(original)
        target.parent.mkdir(parents=True, exist_ok=True)
        h = hashlib.sha256()
        with original.open("rb") as src, target.open("xb") as dst:
            while block := src.read(1024 * 1024):
                h.update(block)
                dst.write(block)
        shutil.copystat(original, target, follow_symlinks=False)
        copied.append(dict(row, sha256=h.hexdigest()))
    # A second independent read detects in-place Steam rewrites during the copy.
    for row in copied:
        if row["kind"] == "file":
            require(digest(source / row["path"]) == row["sha256"]
                    and digest(destination / row["path"]) == row["sha256"],
                    f"Content changed while copying package {source}: {row['path']}")
    require(rows == scan(source), f"Source package changed during copying: {source}")
    require(rows == scan(destination), f"Copy metadata mismatch: {destination}")
    return copied


def package(path, kind):
    about = physical(path / "About/About.xml")
    require(about.is_file(), f"Discoverable folder lacks About/About.xml; resolve explicitly: {path}")
    x = ET.parse(about).getroot()
    pid = x.findtext("packageId", "").strip().lower()
    require(bool(pid), f"Missing package ID: {about}")
    version = x.findtext("modVersion")
    version_source = "About/About.xml:modVersion" if version else None
    version_manifest = physical(path / "About/Manifest.xml")
    if not version and version_manifest.is_file():
        try:
            version = ET.parse(version_manifest).findtext("version")
            version_source = "About/Manifest.xml:version" if version else None
        except ET.ParseError:
            pass  # The exact malformed bytes remain in the content inventory.
    published = physical(path / "About/PublishedFileId.txt")
    return {"id": pid, "name": x.findtext("name"), "version": version, "versionSource": version_source,
            "publishedFileId": published.read_text(encoding="utf-8-sig").strip() if published.is_file() else None,
            "supportedVersions": [n.text for n in x.findall("supportedVersions/li")],
            "source": str(path), "kind": kind, "leaf": path.name}


def order(path):
    return [n.text for n in ET.parse(path).findall("activeMods/li")]


def discover():
    import winreg
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
        steam = physical(winreg.QueryValueEx(key, "SteamPath")[0])
    library_file = steam / "steamapps/libraryfolders.vdf"
    libraries = [Path(p.replace("\\\\", "\\")) for p in re.findall(
        r'"path"\s+"([^"]+)"', library_file.read_text(encoding="utf-8"))]
    libraries = list(dict.fromkeys([steam, *libraries]))
    installs = []
    workshop = []
    for lib in libraries:
        acf = lib / "steamapps/appmanifest_294100.acf"
        if acf.is_file():
            directory = re.search(r'"installdir"\s+"([^"]+)"', acf.read_text()).group(1)
            installs.append((physical(lib / "steamapps/common" / directory), acf))
        content = lib / "steamapps/workshop/content/294100"
        if content.is_dir():
            workshop.append(physical(content))
    require(len(installs) == 1, f"Expected one actual Steam installation, found {installs}")
    game, acf = installs[0]
    require((game / "RimWorldWin64.exe").is_file(), "Steam executable missing")
    profile = physical(Path.home() / "AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios")
    config = profile / "Config/ModsConfig.xml"
    require(config.is_file(), "Normal ModsConfig.xml missing")
    packages = []
    for root, kind in [(p, "workshop") for p in workshop] + [(game / "Mods", "local")]:
        physical(root)
        for child in sorted(root.iterdir()):
            physical(child)
            if child.is_dir():
                packages.append(package(child, kind))
    official = [package(p, "official") for p in sorted((game / "Data").iterdir()) if p.is_dir()]
    active = order(config)
    pids = [p["id"] for p in packages + official]
    require(len(pids) == len(set(pids)), "Duplicate source package IDs; explicit resolution required")
    require(len(active) == len(set(i.lower() for i in active)), "Duplicate enabled IDs")
    require(all(i.lower() in pids for i in active), "An enabled ID has no physical supplier")
    require(PACKAGE_ID not in pids, "Source contains Wake-Up validation package; resolve before freezing")
    leaves = [p["leaf"].lower() for p in packages]
    require(len(leaves) == len(set(leaves)), "Folder-name collision; preserve settings identity before proceeding")
    return {"steam": str(steam), "game": str(game), "workshopRoots": list(map(str, workshop)),
            "localMods": str(game / "Mods"), "profile": str(profile),
            "steamAppManifest": str(acf), "steamAppManifestSha256": digest(acf),
            "steamBuildId": re.search(r'"buildid"\s+"([^"]+)"', acf.read_text()).group(1),
            "steamAssemblySha256": digest(game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll"),
            "packages": packages, "official": official, "activeOrder": active,
            "modsConfigSha256": digest(config), "steamVersion": (game / "Version.txt").read_text().strip()}


def profile_selection(source, packages):
    # Folder leaves are deliberately unchanged, preserving RimWorld's Mod_<folder> naming.
    prefixes = tuple("Mod_" + p["leaf"] + "_" for p in packages)
    general = {"ModsConfig.xml", "Prefs.xml", "KeyPrefs.xml", "Knowledge.xml",
               "LastPlayedVersion.txt", "ChordedKeybinds.xml", "CameraPlusDefaultRules.xml",
               "Rimpsyche_PsycheDataSlots.xml"}
    all_rows = scan(source / "Config")
    selected = []
    for row in all_rows:
        path = row["path"]
        if "/" not in path and (path in general or (path.startswith(prefixes) and path.endswith(".xml"))):
            selected.append(dict(row, path="Config/" + path))
        elif path.startswith(("ModFeatures/", "RimHUD/", "VanillaBackgroundsExpanded/", "CustomLandforms-v1/")):
            selected.append(dict(row, path="Config/" + path))
    hugs = source / "HugsLib/ModSettings.xml"
    if hugs.is_file():
        s = hugs.stat()
        selected.append({"path": "HugsLib/ModSettings.xml", "kind": "file", "bytes": s.st_size, "mtime_ns": s.st_mtime_ns})
    return sorted(selected, key=lambda row: row["path"])


def copy_profile(source, dest, selected):
    dest.mkdir(parents=True)
    for row in selected:
        p = dest / row["path"]
        if row["kind"] == "directory":
            p.mkdir(parents=True, exist_ok=True)
        else:
            physical(source / row["path"])
            p.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source / row["path"], p)
            require(digest(p) == digest(source / row["path"]), f"Settings changed: {p}")
    return inventory(dest)


def git(*args):
    return subprocess.check_output(["git", "-C", str(REPO), *args], text=True).strip()


def no_game():
    result = subprocess.check_output(["powershell", "-NoProfile", "-Command",
        "@(Get-Process -Name RimWorldWin64,RimWorld -ErrorAction SilentlyContinue).Count"], text=True)
    require(result.strip() == "0", "RimWorld is running; fixture operation refused")


def validate_nonfixture_processes(processes, root, other_session=None):
    """Fixture operations may coexist only with freshly identified outside games."""
    require(isinstance(processes, list), "RimWorld process inventory is uncertain")
    root = physical(root).resolve(strict=True)
    for process in processes:
        require(isinstance(process, dict) and isinstance(process.get("ProcessId"), int)
                and process["ProcessId"] > 0, "RimWorld process identity is uncertain")
        raw = process.get("ExecutablePath")
        # Explicit owner-authorized ASTER coexistence only. Known paths are
        # always checked below, even when the game belongs to another session.
        if raw is None and isinstance(other_session, int) and other_session > 0:
            session = process.get("SessionId")
            if isinstance(session, int) and session > 0 and session != other_session:
                continue
        require(isinstance(raw, str) and bool(raw) and Path(raw).is_absolute(),
                "RimWorld executable path is unavailable; fixture operation refused")
        executable = physical(raw).resolve(strict=True)
        require(executable.is_file() and executable.name.casefold() in {"rimworldwin64.exe", "rimworld.exe"},
                "RimWorld executable identity is uncertain")
        require(not executable.is_relative_to(root), "Fixture RimWorld is running; fixture operation refused")


def no_fixture_game(root, allow_other_session=False):
    # Fresh OS query every invocation; never admit from saved PID/run metadata.
    query = ("$ErrorActionPreference = 'Stop'; "
             "@{session=(Get-Process -Id $PID).SessionId; processes=@(Get-CimInstance Win32_Process -Filter "
             "\"Name = 'RimWorldWin64.exe' OR Name = 'RimWorld.exe'\" | "
             "Select-Object ProcessId,ExecutablePath,SessionId)} | ConvertTo-Json -Depth 3 -Compress")
    result = subprocess.check_output(["powershell", "-NoProfile", "-Command", query], text=True)
    inventory = json.loads(result)
    # Never exempt a process recorded as ours, even if session visibility changes.
    if (root / "prepared.json").exists():
        prepared = read(root / "prepared.json")
        run_path = root / "results" / token(prepared["label"]) / "run.json"
        if run_path.exists():
            owned_pid = read(run_path).get("pid")
            require(not any(p.get("ProcessId") == owned_pid for p in inventory["processes"]),
                    "Recorded fixture RimWorld is running; fixture operation refused")
    validate_nonfixture_processes(inventory["processes"], root,
                                 inventory.get("session") if allow_other_session else None)


@contextlib.contextmanager
def fixture_lock(root):
    """Windows kernel lock disappears on crash; no stale lock-file deletion needed."""
    import msvcrt
    physical(root)
    root.mkdir(parents=True, exist_ok=True)
    p = inside(root, root / "operation.lock")
    with p.open("a+b") as stream:
        if stream.tell() == 0:
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        try:
            yield
        finally:
            stream.seek(0)
            msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)


def guard_operation_processes(root, action, label=None, allow_other_session=False):
    """Functional work isolates the fixture; performance launches isolate load."""
    if action == "launch":
        run = read(root / "results" / token(label) / "run.json")
        purpose = run.get("purpose", "performance")  # Legacy records stay strict.
        require(purpose in ("functional", "performance"), "Unknown run purpose")
        if purpose == "performance":
            no_game()
            return
    if allow_other_session:
        no_fixture_game(root, allow_other_session=True)
    else:
        no_fixture_game(root)


def swap(root, replacements, purpose):
    """Journal before renames; keep every previous target for rollback. Never delete."""
    pending = root / "pending.json"
    require(not pending.exists(), "Pending transaction: run recover first")
    tid = time.strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8]
    folder = inside(root, root / "recovery" / tid)
    folder.mkdir(parents=True)
    items = []
    for index, (incoming, target) in enumerate(replacements):
        incoming = inside(root, incoming) if incoming is not None else None
        target = inside(root, target)
        require(incoming is None or incoming.exists(), f"Missing staged input: {incoming}")
        backup = folder / str(index)
        items.append({"incoming": str(incoming) if incoming else None, "target": str(target), "backup": str(backup)})
    record = {"id": tid, "purpose": purpose, "items": items}
    write(pending, record)
    for item in items:
        incoming = Path(item["incoming"]) if item["incoming"] else None
        target, backup = Path(item["target"]), Path(item["backup"])
        if target.exists():
            target.rename(backup)
        target.parent.mkdir(parents=True, exist_ok=True)
        if incoming is not None:
            incoming.rename(target)
    pending.rename(folder / "committed.json")
    return tid


def recover(root):
    pending = root / "pending.json"
    require(pending.is_file(), "No pending transaction")
    record = read(pending)
    for item in reversed(record["items"]):
        incoming = inside(root, item["incoming"]) if item["incoming"] else None
        target, backup = [inside(root, item[k]) for k in ("target", "backup")]
        if incoming is not None and not incoming.exists() and target.exists():
            target.rename(incoming)
        if backup.exists():
            require(not target.exists(), "Ambiguous recovery target; preserve and inspect")
            backup.rename(target)
    pending.rename(root / "recovery" / token(record["id"]) / "recovered.json")
    return {"recovered": record["id"]}


def rollback(root, tid):
    record_path = inside(root, root / "recovery" / token(tid) / "committed.json")
    record = read(record_path)
    if record["purpose"] == "deploy-audio-bootstrap":
        fixture_audio_bootstrap.verify(root, types.SimpleNamespace(**globals()))
    # Only rollback the latest transaction, avoiding restoring stale partial state.
    latest = max((root / "recovery").glob("*/committed.json"), key=lambda p: p.stat().st_mtime_ns)
    require(latest == record_path, "Rollback must target the latest committed transaction")
    write(root / "pending.json", record)
    result = recover(root)
    record_path.rename(record_path.with_name("rolled-back.json"))
    return result


def source_identity(sources):
    # Steam rewrites download-accounting fields while remaining open. Preserve
    # the full receipt, but bind game build/content and mod inputs, not counters.
    return {k: v for k, v in sources.items() if k != "steamAppManifestSha256"}


def initialize(root, refresh=False, resume=None):
    require(refresh == (root / "current.json").exists(), "Use initialize once; refresh is explicit thereafter")
    require(not (root / "pending.json").exists(), "Recover pending transaction first")
    game = inside(root, root / "game")
    require((game / "goggame-1094900565.info").is_file(), "Canonical GOG installation marker missing")
    require((game / "RimWorldWin64.exe").is_file(), "Canonical DRM-free executable missing")
    require(digest(game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll") == DEVELOPMENT_GAME,
            "GOG development runtime drift; refreshing mods does not authorize changing the pinned game")
    sources = discover()
    if not refresh:
        require(not any((game / "Mods").glob("*/About/About.xml")), "Unowned fixture mods already exist")
        require(not any((root / "profile").iterdir()), "Unowned fixture profile is not empty")
    gid = token(resume) if resume else time.strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8]
    stage = inside(root, root / "staging" / gid)
    if resume:
        previous = read(stage / "sources.json")
        for key in ("game", "profile", "activeOrder"):
            require(previous[key] == sources[key], "Resume discovery/order changed; initialize a fresh snapshot")
        require([(p["id"], p["source"]) for p in previous["packages"]] ==
                [(p["id"], p["source"]) for p in sources["packages"]], "Resume package suppliers changed")
        (stage / "sources.json").rename(stage / ("sources-before-resume-" + uuid.uuid4().hex + ".json"))
        if (stage / "seed").exists():
            (stage / "seed").rename(stage / ("seed-before-resume-" + uuid.uuid4().hex))
    else:
        stage.mkdir(parents=True)
    (stage / "Mods").mkdir(exist_ok=bool(resume))
    (stage / "inventories").mkdir(exist_ok=bool(resume))
    write(stage / "sources.json", sources)
    prepared = []
    for index, pkg in enumerate(sources["packages"]):
        source = Path(pkg["source"])
        print(f"[{index+1}/{len(sources['packages'])}] freeze {pkg['leaf']} {pkg['id']}", flush=True)
        inv = f"inventories/{index:04}.json"
        dest = stage / "Mods" / pkg["leaf"]
        prior = read(stage / inv) if resume and (stage / inv).exists() else None
        if prior is not None and dest.exists() and scan(source) == metadata(prior):
            # This receipt exists only after copy_stable's source/destination
            # content verification completed. Retain that evidence when both
            # trees are unchanged; do not repeat full hashing for bookkeeping
            # failures. Explicit full-audit remains available for content doubt.
            verify_tree(dest, prior)
            rows = prior
        else:
            if dest.exists():
                retries = stage / "retries"
                retries.mkdir(exist_ok=True)
                dest.rename(retries / (pkg["leaf"] + "-" + uuid.uuid4().hex))
            rows = copy_stable(source, dest)
        write(stage / inv, rows)
        prepared.append(dict(pkg, fixture=str(game / "Mods" / pkg["leaf"]), inventory=inv,
                             inventorySha256=digest(stage / inv),
                             files=sum(r["kind"] == "file" for r in rows),
                             bytes=sum(r.get("bytes", 0) for r in rows)))
    official = []
    for pkg in sources["official"]:
        dest = game / "Data" / pkg["leaf"]
        actual = package(dest, "official")
        require(actual["id"] == pkg["id"], "GOG official package identity mismatch")
        rows = inventory(dest)
        inv = "inventories/official-" + pkg["leaf"] + ".json"
        write(stage / inv, rows)
        official.append(dict(pkg, fixture=str(dest), inventory=inv, inventorySha256=digest(stage / inv),
                             files=sum(r["kind"] == "file" for r in rows), bytes=sum(r.get("bytes", 0) for r in rows),
                             provenance="Existing GOG install; Steam path is identity comparison only, not copied"))
    profile = Path(sources["profile"])
    selection = profile_selection(profile, sources["packages"])
    seed_rows = copy_profile(profile, stage / "seed", selection)
    require(selection == profile_selection(profile, sources["packages"]), "Source settings selection changed")
    write(stage / "inventories/profile.json", seed_rows)
    # Runtime recorded once, including all game files outside Mods; no content hashing before launches.
    runtime = inventory(game, exclude=("Mods",))
    write(stage / "inventories/game.json", runtime)
    after = discover()
    require(source_identity(sources) == source_identity(after), "Steam discovery/order/build changed; refresh again explicitly")
    write(stage / "sources-after.json", after)
    for pkg in prepared:
        require(scan(Path(pkg["source"])) == metadata(read(stage / pkg["inventory"])),
                f"Source changed after its copy: {pkg['source']}; staged payload retained, not published")
    for row in selection:
        if row["kind"] == "file":
            require(digest(profile / row["path"]) == digest(stage / "seed" / row["path"]), "Source settings changed after copy")
    suppliers = {p["id"]: p["fixture"] for p in prepared + official}
    assembly = "RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
    manifest = {"schema": "rlo-drm-fixture.v1", "generation": gid, "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "repositoryHead": git("rev-parse", "HEAD"), "canonicalRoot": str(root), "sources": sources,
                "packages": prepared, "official": official, "activeOrder": sources["activeOrder"],
                "enabledSuppliers": [{"index": i, "id": pid, "path": suppliers[pid.lower()]} for i, pid in enumerate(sources["activeOrder"])],
                "orderSha256": hashlib.sha256("\n".join(sources["activeOrder"]).encode()).hexdigest(),
                "profileInventorySha256": digest(stage / "inventories/profile.json"),
                "gameInventorySha256": digest(stage / "inventories/game.json"),
                "gogVersion": (game / "Version.txt").read_text().strip(), "gogAssemblySha256": digest(game / assembly),
                "steamAssemblySha256": digest(Path(sources["game"]) / assembly),
                "acceptedCandidateRuntimeMatches": digest(game / assembly) == PINNED_GAME,
                "developmentRuntimeSha256": DEVELOPMENT_GAME,
                "developmentRuntimePolicy": "owner-approved-fixed-GOG-rev573",
                "steamProductionPolicy": "latest-Steam-at-post-MVP-test-time",
                "copiedModFiles": sum(p["files"] for p in prepared), "copiedModBytes": sum(p["bytes"] for p in prepared),
                "profileFiles": sum(r["kind"] == "file" for r in seed_rows), "profileBytes": sum(r.get("bytes", 0) for r in seed_rows),
                "excludedProfilePolicy": "No saves, presets, generated caches, logs or Wake-Up state. Applicable Mod_<folder> XML, startup preferences, Config settings subfolders and HugsLib/ModSettings.xml only."}
    write(stage / "manifest.json", manifest)
    final = inside(root, root / "snapshots" / gid)
    final.parent.mkdir(exist_ok=True)
    stage.rename(final)
    shutil.copytree(final / "seed", final / "incoming-profile")
    write(final / "incoming-current.json", {"generation": gid, "manifestSha256": digest(final / "manifest.json")})
    tid = swap(root, [(final / "Mods", game / "Mods"), (final / "incoming-profile", root / "profile"),
                      (final / "incoming-current.json", root / "current.json"),
                      (None, root / "candidate.json"), (None, root / "menu-observer.json"), (None, root / "prepared.json"),
                      (None, root / "excluded-mods.json"), (None, root / "parked-mods"),
                      (None, root / "parked-rlo/package")], "refresh" if refresh else "initialize")
    return {"manifest": str(final / "manifest.json"), "identity": digest(final / "manifest.json"), "transaction": tid,
            "mods": len(prepared), "enabled": len(sources["activeOrder"]),
            "copiedFiles": manifest["copiedModFiles"], "copiedBytes": manifest["copiedModBytes"]}


def current(root):
    require(not (root / "pending.json").exists(), "Incomplete transaction; run recover")
    state = read(root / "current.json")
    generation = inside(root, root / "snapshots" / token(state["generation"]))
    require(digest(generation / "manifest.json") == state["manifestSha256"], "Manifest identity drift")
    m = read(generation / "manifest.json")
    require(m["canonicalRoot"] == str(root) and m["schema"] == "rlo-drm-fixture.v1", "Wrong manifest root/schema")
    return m, generation, state


def verify_tree(path, rows, full=False, exclude=()):
    require(scan(path, exclude) == metadata(rows), f"Metadata drift: {path}")
    if full:
        for r in rows:
            if r["kind"] == "file":
                require(digest(path / r["path"]) == r["sha256"], f"Content drift: {path / r['path']}")


def audit(root, full=False):
    start = time.perf_counter()
    m, gen, state = current(root)
    bootstrap = fixture_audio_bootstrap.verify(root, types.SimpleNamespace(**globals()))
    exclusions = excluded_mods(root, m, state)
    excluded = {p["id"]: p for p in exclusions}
    expected = {p["leaf"] for p in m["packages"] if p["id"] not in excluded}
    if (root / "menu-observer.json").exists():
        menu = read(root / "menu-observer.json")
        require(menu["generation"] == m["generation"], "Menu observer from another generation; deploy again")
        require(menu.get("gameSha256") == m.get("runtimeContract", {"gameAssemblySha256": DEVELOPMENT_GAME})["gameAssemblySha256"], "Observer runtime mismatch; rebuild/deploy")
        verify_tree(root / "game/Mods" / MENU_LEAF, menu["files"], True)
        expected.add(MENU_LEAF)
    if (root / "candidate.json").exists():
        candidate = read(root / "candidate.json")
        require(candidate["generation"] == m["generation"], "Candidate from another generation; deploy again")
        location = candidate_location(root)
        verify_tree(location, candidate["files"], True)
        if location == root / "game/Mods" / PACKAGE_LEAF:
            expected.add(PACKAGE_LEAF)
    else:
        require(not (root / "parked-rlo/package").exists(), "Parked package has no candidate receipt")
    mods = root / "game/Mods"
    require({p.name for p in mods.iterdir()} == expected, "Unexpected/missing discovery entries in fixture Mods")
    for pkg in m["packages"]:
        inv = inside(gen, gen / pkg["inventory"])
        require(digest(inv) == pkg["inventorySha256"], "Inventory identity drift")
        dest = inside(root, excluded[pkg["id"]]["parked"] if pkg["id"] in excluded else pkg["fixture"])
        verify_tree(dest, read(inv), full)
        require(package(dest, pkg["kind"])["id"] == pkg["id"], "Package-ID drift")
    for name, path, key, exclude in [("game", root / "game", "gameInventorySha256", ("Mods",) + (fixture_audio_bootstrap.OWNED if bootstrap else ())),
                                     ("profile", gen / "seed", "profileInventorySha256", ())]:
        inv = gen / f"inventories/{name}.json"
        require(digest(inv) == m[key], "Inventory identity drift")
        verify_tree(path, read(inv), full, exclude)
    require(order(gen / "seed/Config/ModsConfig.xml") == m["activeOrder"], "Frozen order drift")
    return {"audit": "full-content" if full else "metadata", "manifestSha256": state["manifestSha256"],
            "mods": len(m["packages"]), "enabled": len(m["activeOrder"]), "elapsedSeconds": round(time.perf_counter()-start, 3),
            "excludedMods": exclusions,
            "selectedFrozenEnabled": len([pid for pid in m["activeOrder"] if pid not in excluded]),
            "snapshotHistoricalAdapterMatched": m["acceptedCandidateRuntimeMatches"],
            "deployedRuntimeMatches": candidate_matches_snapshot(root, m, gen)}


def candidate_location(root):
    live = root / "game/Mods" / PACKAGE_LEAF
    parked = root / "parked-rlo/package"
    require(live.exists() != parked.exists(), "Candidate must exist exactly once, installed or parked")
    return live if live.exists() else parked


def excluded_mods(root, manifest, state):
    """A reversible selection overlay; frozen manifests and inventories stay intact."""
    receipt = root / "excluded-mods.json"
    rows = []
    if receipt.exists():
        data = read(receipt)
        require(data.get("schema") == "rlo-excluded-mods.v1" and data.get("generation") == manifest["generation"]
                and data.get("manifestSha256") == state["manifestSha256"], "Excluded-mod receipt binding drift")
        rows = data["packages"]
    packages = {p["id"]: p for p in manifest.get("packages", [])}
    require(len({row["id"] for row in rows}) == len(rows), "Duplicate excluded package IDs")
    for row in rows:
        pkg = packages.get(row["id"])
        require(pkg is not None and row["id"] not in ("zetrith.prepatcher", "brrainz.harmony"), "Invalid excluded frozen package")
        parked = inside(root, root / "parked-mods" / token(pkg["leaf"]))
        live = inside(root, pkg["fixture"])
        require(row == {"id": pkg["id"], "original": str(live), "parked": str(parked),
                        "inventorySha256": pkg["inventorySha256"]}, "Excluded package identity/path drift")
        require(parked.is_dir() and not live.exists(), "Excluded package must exist exactly once, parked only")
    parked_root = root / "parked-mods"
    require(not parked_root.exists() or {p.name for p in parked_root.iterdir()} ==
            {packages[row["id"]]["leaf"] for row in rows}, "Unexpected parked mod without exclusion receipt")
    return rows



def exclude_mod(root, package_id, restore=False):
    audit(root)
    manifest, _, state = current(root)
    rows = excluded_mods(root, manifest, state)
    package_id = package_id.strip().lower()
    pkg = next((p for p in manifest["packages"] if p["id"] == package_id), None)
    require(pkg is not None and package_id not in ("zetrith.prepatcher", "brrainz.harmony"),
            "Select an existing frozen non-bootstrap mod package ID")
    live = inside(root, pkg["fixture"])
    parked = inside(root, root / "parked-mods" / token(pkg["leaf"]))
    previous = next((p for p in rows if p["id"] == package_id), None)
    require((previous is not None) == restore, "Package is not excluded" if restore else "Package is already excluded")
    if restore:
        rows = [p for p in rows if p["id"] != package_id]
    else:
        rows = sorted(rows + [{"id": package_id, "original": str(live), "parked": str(parked),
                               "inventorySha256": pkg["inventorySha256"]}], key=lambda p: p["id"])
    staged = root / "staging" / ("exclusions-" + uuid.uuid4().hex + ".json")
    write(staged, {"schema": "rlo-excluded-mods.v1", "generation": manifest["generation"],
                   "manifestSha256": state["manifestSha256"], "packages": rows})
    transaction = swap(root, [(parked, live) if restore else (live, parked),
                              (staged, root / "excluded-mods.json"), (None, root / "prepared.json")],
                       "restore-mod" if restore else "exclude-mod")
    return {"restoredMod" if restore else "excludedMod": package_id, "transaction": transaction,
            "excludedMods": rows, "frozenEnabled": len(manifest["activeOrder"]),
            "selectedFrozenEnabled": len([pid for pid in manifest["activeOrder"] if pid not in {p["id"] for p in rows}])}



def benchmark_selection(manifest, state, value, excluded_ids=()):
    require(isinstance(value, dict) and value.get("schema") == "rlo-benchmark-selection.v1",
            "Invalid benchmark selection")
    require(value.get("manifestSha256") == state["manifestSha256"], "Benchmark manifest mismatch")
    token(value.get("name", ""))
    active = value.get("activeOrder")
    require(isinstance(active, list) and all(isinstance(pid, str) for pid in active), "Invalid benchmark order")
    require(len(active) == len(set(active)) and set(active).issubset(set(manifest["activeOrder"])),
            "Benchmark order contains duplicate or unfrozen IDs")
    require(not set(active).intersection(excluded_ids), "Benchmark selects excluded mod")
    require(active[:2] == ["zetrith.prepatcher", "brrainz.harmony"] and "ludeon.rimworld" in active,
            "Benchmark requires reviewed bootstrap and Core")
    return value


def run_order(manifest, lane, mode, menu_observer=False, excluded_ids=(), selection=None):
    require(lane in ("representative", "activation", "xml-expanded"), "Unknown fixture lane")
    require(mode in ("absent", "baseline", "candidate"), "Unknown fixture mode")
    require(selection is None or lane == "representative", "Benchmark selection requires representative lane")
    active = list(selection["activeOrder"]) if selection else [pid for pid in manifest["activeOrder"] if pid not in excluded_ids]
    if lane in ("activation", "xml-expanded"):
        official = {p["id"] for p in manifest["official"]}
        extras = XML_EXPANDED_MODS if lane == "xml-expanded" else set()
        require(extras.issubset(set(active)), "Frozen smoke supplier absent")
        active = [pid for pid in active if pid in official or pid in ("zetrith.prepatcher", "brrainz.harmony") or pid in extras]
    require(active[:2] == ["zetrith.prepatcher", "brrainz.harmony"], "Reviewed candidate insertion requires Prepatcher/Harmony at indexes 0/1")
    additions = ([MENU_ID] if menu_observer else []) + ([] if mode == "absent" else [PACKAGE_ID])
    return active[:2] + additions + active[2:]

def run_arguments(root, mode, observation="timing", strategy="startup-searches",
                  exit_after_menu_ready=False, gagarin_cache="off", png="off", png_source="native", loading_progress="off", character_presets="off", giddy_textures="off", activation="fixture", gameplay_smoke=False, residual_probe=False, background_hold=False, gameplay_save_from=None, purpose="performance", settings=None, world_background=False, native_stack_traces=False, xml_source_observer=False, processed_xml_observer=False, c14_background=None, first_build_images=False, raw_pixel_helper=False):
    require(not xml_source_observer or (purpose == "functional" and exit_after_menu_ready),
            "XML source observer requires functional automatic normal exit")
    require(not world_background or (purpose == "functional" and exit_after_menu_ready and activation == "user"
            and not gameplay_smoke and not residual_probe and (settings or {}).get("backgroundLoading") is True),
            "World background requires functional automatic user activation, backgroundLoading=true and no other scene probe")
    require(not c14_background or (purpose == "functional" and exit_after_menu_ready and activation == "user"
            and not gameplay_smoke and not world_background and not residual_probe
            and re.fullmatch(r"(?:autostart|save|save-hold|refused-save|world|colony|settle|camp|encounter|standalone|pocket|portal|portal-error|portal-job|world-render|world-render-cancel)-(?:false|true|false-to-true|true-to-false)(?:-unpaused)?", c14_background)),
            "C14 requires an isolated functional automatic native loading phase")
    require(not gameplay_smoke or exit_after_menu_ready, "Gameplay smoke requires automatic capture and normal exit")
    require(not background_hold or (purpose == "functional" and gameplay_smoke and activation == "user"
            and (settings or {}).get("backgroundLoading") is True),
            "Background hold requires functional gameplay smoke, user activation and backgroundLoading=true")
    require(gameplay_save_from is None or (purpose == "functional" and (gameplay_smoke or c14_background or not exit_after_menu_ready)),
            "Captured gameplay save requires functional gameplay smoke or explicit manual exit")
    if gameplay_save_from is not None:
        token(gameplay_save_from)
    require(activation in ("fixture", "user"), "Unknown activation mode")
    require(activation != "user" or (mode == "candidate" and strategy == "startup-searches" and all(value == "off" for value in (gagarin_cache, png, loading_progress, character_presets, giddy_textures)) and png_source == "native"), "User activation uses saved/default settings, not feature selectors")
    require(giddy_textures in ("off", "on", "timing", "verify"), "Unknown Giddy-Up texture mode")
    require(giddy_textures == "off" or (mode == "candidate" and strategy == "startup-searches"), "Giddy textures require startup-searches candidate")
    require(character_presets in ("off", "on", "timing"), "Unknown Character Editor preset mode")
    require(character_presets == "off" or (mode == "candidate" and strategy == "startup-searches"), "Character Editor presets require startup-searches candidate")
    require(loading_progress in ("off", "coalesce"), "Unknown Loading Progress mode")
    require(loading_progress == "off" or mode == "candidate", "Loading Progress optimization requires candidate")
    require(type(first_build_images) is bool, "First-build selector must be boolean")
    require(not first_build_images or (activation == "fixture" and mode == "candidate" and png != "off"),
            "First-build selector requires explicit candidate PNG fixture activation")
    require(png in ("off", "control", "cache", "verify-cache", "first-build", "verify-first-build"), "Unknown PNG mode")
    require(png_source in ("native", "original"), "Unknown PNG source")
    require(png == "off" or (mode == "candidate" and strategy == "startup-searches"), "PNG requires startup-searches candidate")
    require(png_source == "native" or png != "off", "Original PNG comparison requires explicit PNG mode")
    require(mode in ("absent", "baseline", "candidate"), "Unknown optimization mode")
    require(gagarin_cache in ("off", "timing", "on", "verify"), "Unknown Gagarin cache mode")
    require(gagarin_cache == "off" or (strategy == "startup-searches" and mode == "candidate"), "Gagarin cache requires startup-searches candidate")
    require(observation in ("none", "timing") and (observation != "none" or mode == "absent"), "Unknown observation mode")
    require(strategy in ("def-lookup", "type-lookup", "startup-searches"), "Unknown optimization strategy")
    args = ["-savedatafolder=" + str(root / "profile")]
    if exit_after_menu_ready:
        args.append("--fixture-exit-after-menu-ready")
    if gameplay_smoke: args.append("--fixture-gameplay-smoke")
    if background_hold: args.append("--fixture-background-hold")
    if world_background: args.append("--fixture-world-background")
    if c14_background: args.append("--fixture-c14-background=" + c14_background)
    require(not native_stack_traces or (purpose == "functional" and (gameplay_smoke or world_background or c14_background)), "Native stack traces require an explicit functional scene probe")
    if native_stack_traces: args.append("--fixture-native-stack-traces")
    if gameplay_save_from is not None and gameplay_smoke: args.append("--fixture-gameplay-save")
    if residual_probe: args.append("--fixture-residual-probe")
    require(not processed_xml_observer or (purpose == "functional" and exit_after_menu_ready), "Processed XML observation is functional automatic only")
    require(not (processed_xml_observer and xml_source_observer), "Choose one XML observer")
    if processed_xml_observer: args.append("--fixture-processed-xml-observer")
    if xml_source_observer: args.append("--fixture-parsed-source-observer")
    if activation == "user":
        return args
    if mode != "absent":
        args.extend(["--wake-up-mode=" + mode, "--wake-up-strategy=" + strategy])
    if gagarin_cache != "off":
        args.append("--wake-up-gagarin-cache=" + gagarin_cache)
    if png != "off":
        args.append("--wake-up-png=" + png)
    if giddy_textures != "off":
        args.append("--wake-up-giddy-textures=" + giddy_textures)
    if character_presets != "off":
        args.append("--wake-up-character-presets=" + character_presets)
    if loading_progress != "off":
        args.append("--wake-up-loading-progress=" + loading_progress)
    if first_build_images:
        args.append("--wake-up-first-build-images=on")
    require(type(raw_pixel_helper) is bool, "Raw helper selector must be boolean")
    if raw_pixel_helper:
        require(activation == "fixture" and mode == "candidate" and exit_after_menu_ready
                and (first_build_images or png in ("first-build", "verify-first-build")),
                "Raw helper trial requires explicit fixture first-build selection and automatic exit")
        trial = read(root / "candidate.json").get("rawPixelHelper", {})
        require(trial.get("path") == "Tools/win-x64/WakeUp.RawPixelTrial.exe"
                and re.fullmatch(r"[a-f0-9]{64}", trial.get("sha256", "")), "Raw helper trial is not deployed")
        args.append("--fixture-raw-pixel-helper=" + trial["sha256"])
    if png_source != "native":
        args.append("--wake-up-png-source=" + png_source)
    return args


def candidate_matches_snapshot(root, manifest, generation):
    if not (root / "candidate.json").is_file():
        return False
    candidate = read(root / "candidate.json")
    expected_files = {"About/About.xml", "Assemblies/WakeUp.dll"}
    helper = candidate.get("optionalHelper")
    if helper:
        legacy = helper == {"bundleSha256": REVIEWED_HELPER_BUNDLE, "sha256": REVIEWED_HELPER_SHA, "path": HELPER_PATH}
        c08 = (helper.get("sha256") == C08_HELPER_SHA and helper.get("path") == HELPER_PATH
               and re.fullmatch(r"[a-f0-9]{64}", helper.get("buildReceiptSha256", "")) is not None)
        if not legacy and not c08:
            return False
        expected_files.add(HELPER_PATH)
        if not any(row.get("path") == HELPER_PATH and row.get("sha256") == helper["sha256"]
                   for row in candidate.get("files", [])):
            return False
    trial = candidate.get("rawPixelHelper")
    if trial:
        if trial.get("path") != "Tools/win-x64/WakeUp.RawPixelTrial.exe" or trial.get("protocol") != "stdio-raw-v1":
            return False
        if not re.fullmatch(r"[a-f0-9]{64}", trial.get("sha256", "")) or not re.fullmatch(r"[a-f0-9]{64}", trial.get("buildReceiptSha256", "")):
            return False
        expected_files.add(trial["path"])
        if not any(row.get("path") == trial["path"] and row.get("sha256") == trial["sha256"] for row in candidate.get("files", [])):
            return False
    if {row["path"] for row in candidate.get("files", []) if row["kind"] == "file"} != expected_files:
        return False
    inv = generation / "inventories/game.json"
    require(digest(inv) == manifest["gameInventorySha256"], "Game inventory identity drift")
    game_sha = next(r["sha256"] for r in read(inv) if r["path"] == "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")
    contract = candidate.get("runtimeContract", {})
    expected = manifest.get("runtimeContract", {"target": "gog-rev573", "gameAssemblySha256": DEVELOPMENT_GAME})
    return contract == expected and contract.get("gameAssemblySha256") == game_sha


def reference_environment(root):
    m, gen, state = current(root)
    prepatcher = next(p for p in m["packages"] if p["id"] == "zetrith.prepatcher")
    refs = Path(prepatcher["fixture"]) / "Assemblies"
    managed = root / "game/RimWorldWin64_Data/Managed"
    contract = runtime_contract(root)
    return dict(os.environ, WAKE_UP_RIMWORLD_MANAGED_DIR=str(managed),
               WAKE_UP_PREPATCHER_ASSEMBLIES_DIR=str(refs), WAKE_UP_REFERENCE_TARGET=contract["target"])


def test(root, test_filter=None):
    """Retained feature and fixture checks; no game execution or normal-data writes."""
    env = reference_environment(root)
    output = REPO / "artifacts/fixture-tests" / uuid.uuid4().hex
    temporary = output / "tmp"
    temporary.mkdir(parents=True)
    env.update(TEMP=str(temporary), TMP=str(temporary))
    commands = [["dotnet", "restore", "--locked-mode"],
                ["dotnet", "build", "--configuration", "Release", "--no-restore"],
                ["dotnet", "test", "tests/WakeUp.Tests", "-c", "Release", "--no-build",
                 "--logger", "trx;LogFileName=features.trx", "--results-directory", str(output)],
                [sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py", "-q"]]
    if test_filter:
        commands[2].extend(["--filter", test_filter])
    for command in commands:
        subprocess.run(command, cwd=REPO, env=env, check=True)
    return {"offlineTests": "passed", "target": env["WAKE_UP_REFERENCE_TARGET"], "gameLaunched": False, "results": str(output)}


def build(root):
    revision = git("rev-parse", "HEAD")
    require(not git("status", "--porcelain", "--", "src", "build", "third-party", "scripts/fixture.py", "Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.config", "validation/fixture-package"), "Uncommitted build inputs")
    env = reference_environment(root)
    refs = Path(env["WAKE_UP_PREPATCHER_ASSEMBLIES_DIR"])
    # Verifier checks exact approved build references; runtime admission stays separate.
    product = "src/WakeUp/WakeUp.csproj"
    for command in [["pwsh", "-NoProfile", "-File", str(REPO / "build/Verify-LocalReferences.ps1")],
                    ["dotnet", "restore", product, "--locked-mode"],
                    ["dotnet", "build", product, "--configuration", "Release", "--no-restore", f"-p:SourceRevisionId={revision}"]]:
        subprocess.run(command, cwd=REPO, env=env, check=True)
    package_root = REPO / "artifacts/fixture-package" / revision
    require(not package_root.exists(), "Build package already exists; use its identity or a new commit")
    (package_root / "About").mkdir(parents=True)
    (package_root / "Assemblies").mkdir()
    shutil.copy2(REPO / "validation/fixture-package/About/About.xml", package_root / "About/About.xml")
    filename = "WakeUp.dll"
    shutil.copy2(REPO / "artifacts/bin/WakeUp/Release/net472" / filename, package_root / "Assemblies" / filename)
    check_script = r'''
    $expected = 'WakeUp'
    $path = Join-Path $env:WAKE_UP_PACKAGE_CHECK_ROOT ('Assemblies/' + $expected + '.dll')
    $identity = [Reflection.AssemblyName]::GetAssemblyName($path)
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    if ($identity.Name -cne $expected -or $identity.Version.ToString() -cne '1.0.0.0' -or
        $version.ProductVersion -cne ('1.0.0+' + $env:WAKE_UP_PACKAGE_CHECK_REVISION)) {
        throw ('Package assembly/source revision mismatch: ' + $path)
    }
'''
    subprocess.run(["pwsh", "-NoProfile", "-Command", "$ErrorActionPreference='Stop';" + check_script],
                   env=dict(env, WAKE_UP_PACKAGE_CHECK_ROOT=str(package_root), WAKE_UP_PACKAGE_CHECK_REVISION=revision), check=True)
    receipt = {"sourceRevision": revision, "referenceTarget": env["WAKE_UP_REFERENCE_TARGET"],
               "runtimeContract": runtime_contract(root),
               "files": inventory(package_root), "buildReferences": {
        str(Path(env["WAKE_UP_RIMWORLD_MANAGED_DIR"]) / "Assembly-CSharp.dll"): digest(Path(env["WAKE_UP_RIMWORLD_MANAGED_DIR"]) / "Assembly-CSharp.dll"),
        str(refs / "0Harmony.dll"): digest(refs / "0Harmony.dll"),
        str(refs / "0PrepatcherAPI.dll"): digest(refs / "0PrepatcherAPI.dll")}}
    write(package_root.with_suffix(".json"), receipt)
    return {"package": str(package_root), "sourceRevision": revision}


def deploy(root, package_root):
    audit(root)
    m, gen, state = current(root)
    package_root = physical(package_root)
    receipt = read(package_root.with_suffix(".json"))
    verify_tree(package_root, receipt["files"], True)
    require(package(package_root, "candidate")["id"] == PACKAGE_ID, "Wrong candidate ID")
    require({r["path"] for r in receipt["files"] if r["kind"] == "file"} == {
        "About/About.xml", "Assemblies/WakeUp.dll"}, "Candidate must contain only About.xml and the retained runtime DLL; stale Core and third-party assemblies are refused")
    stage = root / "staging" / ("candidate-" + uuid.uuid4().hex)
    stage.mkdir(parents=True)
    rows = copy_stable(package_root, stage / "package")
    write(stage / "candidate.json", dict(receipt, files=rows, generation=m["generation"]))
    tid = swap(root, [(None, root / "parked-rlo/package"),
                      (stage / "package", root / "game/Mods" / PACKAGE_LEAF),
                      (stage / "candidate.json", root / "candidate.json")], "deploy-candidate")
    return {"deployed": str(root / "game/Mods" / PACKAGE_LEAF), "transaction": tid, "sourceRevision": receipt["sourceRevision"]}


def deploy_helper(root, bundle, raw_pixel_helper=False):
    """Overlay only the pinned reviewed helper onto the private candidate, transactionally."""
    audit(root)
    bundle = physical(bundle)
    if raw_pixel_helper:
        require(bundle.is_dir(), "Raw trial requires a source-bound helper build package")
        build_path = bundle / "helper-build.json"
        build = read(build_path)
        require(build.get("target") == "win-x64" and build.get("rawHandshakePassed") is True
                and build.get("rawProtocol") == "stdio-raw-v1", "Raw helper handshake/target mismatch")
        tool = bundle / HELPER_PATH
        require(digest(tool) == build.get("sha256"), "Raw helper binary identity mismatch")
        for relative, expected in build["inputs"].items():
            require(digest(REPO / relative) == expected, "Raw helper source changed: " + relative)
        receipt = read(root / "candidate.json")
        location = candidate_location(root)
        original_core = digest(location / "Assemblies/WakeUp.dll")
        stage = root / "staging" / ("raw-helper-" + uuid.uuid4().hex)
        stage.mkdir(parents=True)
        copy_stable(location, stage / "package")
        relative = "Tools/win-x64/WakeUp.RawPixelTrial.exe"
        target = stage / "package" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(tool, target)
        receipt["files"] = inventory(stage / "package")
        receipt["rawPixelHelper"] = {"sha256": build["sha256"], "path": relative,
            "protocol": "stdio-raw-v1", "buildReceiptSha256": digest(build_path)}
        write(stage / "candidate.json", receipt)
        tid = swap(root, [(stage / "package", location), (stage / "candidate.json", root / "candidate.json")], "deploy-raw-helper-trial")
        return {"transaction": tid, "rawPixelHelper": receipt["rawPixelHelper"],
                "coreUnchanged": digest(location / "Assemblies/WakeUp.dll") == original_core}
    if bundle.is_dir():
        tool = bundle / HELPER_PATH
        if (bundle / "release-manifest.json").is_file():
            import package_release
            _, build = package_release.helper_overlay(bundle)
            build_path = bundle / "release-manifest.json"
        else:
            build_path = bundle / "helper-build.json"
            build = read(build_path)
            for relative, expected in build["inputs"].items():
                require(digest(REPO / relative) == expected, "Helper build source changed: " + relative)
        require(build.get("target") == "win-x64" and build.get("handshakePassed") is True,
                "Helper build target or handshake mismatch")
        require(build.get("sha256") == C08_HELPER_SHA and digest(tool) == C08_HELPER_SHA, "Unreviewed C08 helper binary")
        receipt = read(root / "candidate.json")
        location = candidate_location(root)
        original_core = digest(location / "Assemblies/WakeUp.dll")
        stage = root / "staging" / ("helper-" + uuid.uuid4().hex)
        stage.mkdir(parents=True)
        copy_stable(location, stage / "package")
        target = stage / "package" / HELPER_PATH
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(tool, target)
        receipt["files"] = inventory(stage / "package")
        receipt["optionalHelper"] = {"sha256": C08_HELPER_SHA, "path": HELPER_PATH,
                                     "buildReceiptSha256": digest(build_path)}
        write(stage / "candidate.json", receipt)
        tid = swap(root, [(stage / "package", location), (stage / "candidate.json", root / "candidate.json")], "deploy-c08-helper")
        return {"transaction": tid, "optionalHelper": receipt["optionalHelper"],
                "coreUnchanged": digest(location / "Assemblies/WakeUp.dll") == original_core}
    require(digest(bundle) == REVIEWED_HELPER_BUNDLE, "Unreviewed helper bundle")
    receipt = read(root / "candidate.json")
    location = candidate_location(root)
    with zipfile.ZipFile(bundle) as archive:
        bundled_core_sha = hashlib.sha256(archive.read("wake-up/Assemblies/WakeUp.dll")).hexdigest()
        data = archive.read("wake-up/" + HELPER_PATH)
    require(hashlib.sha256(data).hexdigest() == REVIEWED_HELPER_SHA, "Helper identity mismatch")
    stage = root / "staging" / ("helper-" + uuid.uuid4().hex)
    stage.mkdir(parents=True)
    copy_stable(location, stage / "package")
    target = stage / "package" / HELPER_PATH
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    receipt["files"] = inventory(stage / "package")
    receipt["optionalHelper"] = {"bundleSha256": REVIEWED_HELPER_BUNDLE,
                                 "sha256": REVIEWED_HELPER_SHA, "path": HELPER_PATH}
    write(stage / "candidate.json", receipt)
    tid = swap(root, [(stage / "package", location), (stage / "candidate.json", root / "candidate.json")],
               "deploy-reviewed-helper")
    return {"transaction": tid, "optionalHelper": receipt["optionalHelper"], "coreUnchanged": True,
            "bundleCoreSha256": bundled_core_sha, "deployedCoreSha256": digest(location / "Assemblies/WakeUp.dll")}


def observer_artwork(root, specification):
    """Bounded copies for real provider-collision/PNG tests; never change sources."""
    if specification is None:
        return []
    specification = inside(REPO / "artifacts", specification)
    require(specification.is_file() and specification.stat().st_size <= 16384, "Observer artwork specification bound")
    rows = read(specification)
    require(isinstance(rows, list) and 1 <= len(rows) <= 8, "Observer artwork count bound")
    targets = set()
    for row in rows:
        require(isinstance(row, dict) and row.keys() == {"source", "target", "sha256"}, "Observer artwork fields")
        source = inside(root / "game", Path(row["source"]))
        target = row["target"]
        require(isinstance(target, str) and target.startswith("Textures/") and "\\" not in target and ":" not in target
                and all(p not in ("", ".", "..") for p in target.split("/"))
                and Path(target).suffix.lower() in (".png", ".dds") and target not in targets,
                "Observer artwork target must be a unique texture path")
        require(source.is_file() and 0 < source.stat().st_size <= 1024 * 1024
                and source.suffix.lower() == Path(target).suffix.lower() and digest(source) == row["sha256"],
                "Observer artwork source identity changed")
        targets.add(target)
    return rows


def build_native_device_probe(destination):
    """Experimental Windows SDK-only DLL; built solely through the fixture owner lock."""
    vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
    vs = Path(subprocess.check_output([str(vswhere), "-latest", "-products", "*", "-requires",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"], text=True).strip())
    require(vs.is_dir(), "Existing C++ toolchain required; no installation is performed")
    cmake = vs / "Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe"
    require(cmake.is_file(), "Existing CMake required")
    build_dir = REPO / "artifacts/c10-native-build" / git("rev-parse", "HEAD")
    subprocess.run([str(cmake), "-S", str(REPO / "validation/menu-observer/native"), "-B", str(build_dir),
                    "-A", "x64", f"-DCMAKE_GENERATOR_INSTANCE={vs}"], check=True)
    subprocess.run([str(cmake), "--build", str(build_dir), "--config", "Release", "--parallel", "1"], check=True)
    target = destination / "Tools/win-x64/C10NativeProbe.dll"
    target.parent.mkdir(parents=True)
    shutil.copy2(build_dir / "Release/C10NativeProbe.dll", target)
    toolset = sorted((vs / "VC/Tools/MSVC").iterdir())[-1]
    imports = subprocess.check_output([str(toolset / "bin/Hostx64/x64/dumpbin.exe"), "/dependents", str(target)], text=True)
    print(imports)
    return {"path": "Tools/win-x64/C10NativeProbe.dll", "sha256": digest(target), "imports": imports}


def build_menu_observer(root, artwork_specification=None, native_texture_probe=False):
    revision = git("rev-parse", "HEAD")
    require(not git("status", "--porcelain", "--", "validation/menu-observer", "scripts/fixture.py",
                    "Directory.Build.props", "Directory.Packages.props"), "Uncommitted observer build inputs")
    env = reference_environment(root)
    artwork = observer_artwork(root, artwork_specification)
    require(not native_texture_probe or any(r["target"] == "Textures/C10NativeProbe.dds" for r in artwork),
            "Native device probe requires an identified authored DDS artwork input")
    native_input = next((r for r in artwork if r["target"] == "Textures/C10NativeProbe.dds"), None) if native_texture_probe else None
    if native_input:
        artwork.remove(native_input)  # Keep the native input outside RimWorld's eager Textures content.
    project = REPO / "validation/menu-observer/MenuObserver.csproj"
    subprocess.run(["dotnet", "restore", str(project), "--locked-mode"], cwd=REPO, env=env, check=True)
    subprocess.run(["dotnet", "build", str(project), "-c", "Release", "--no-restore",
                    f"-p:SourceRevisionId={revision}"], cwd=REPO, env=env, check=True)
    destination = REPO / "artifacts/fixture-menu-observer" / revision
    require(not destination.exists(), "Observer package already exists; use it or a new commit")
    (destination / "About").mkdir(parents=True)
    (destination / "Assemblies").mkdir()
    shutil.copy2(project.parent / "About.xml", destination / "About/About.xml")
    shutil.copy2(REPO / "artifacts/bin/MenuObserver/Release/net472/FixtureMenuObserver.dll",
                 destination / "Assemblies/FixtureMenuObserver.dll")
    for row in artwork:
        target = inside(destination, destination / row["target"])
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(row["source"], target)
        require(digest(target) == row["sha256"], "Observer artwork copy changed")
    native_probe = build_native_device_probe(destination) if native_texture_probe else None
    if native_probe:
        shutil.copy2(native_input["source"], destination / "Tools/C10NativeProbe.dds")
        require(digest(destination / "Tools/C10NativeProbe.dds") == native_input["sha256"], "Native probe input copy changed")
        native_probe["payload"] = dict(native_input, target="Tools/C10NativeProbe.dds")
    write(destination.with_suffix(".json"), {"sourceRevision": revision, "gameSha256": runtime_contract(root)["gameAssemblySha256"],
                                          "functionalArtwork": artwork,
                                          "nativeDeviceProbe": native_probe,
                                          "supportsNativeStackTraces": True,
                                          "supportsAudioStreamProbe": True,
                                          "supportsAudioBootstrapProbe": True,
                                          "supportsAudioNativeEntryProbe": True,
                                          "supportsAutomaticExit": True, "supportsGameplaySmoke": True, "supportsResidualProbe": True,
                                          "supportsBackgroundHold": True, "supportsGameplaySave": True, "supportsWorldBackground": True, "supportsC14Background": True,
                                          "files": inventory(destination)})
    return {"package": str(destination), "sourceRevision": revision}


def deploy_menu_observer(root, package_root):
    audit(root)
    manifest, _, _ = current(root)
    package_root = physical(package_root)
    receipt = read(package_root.with_suffix(".json"))
    verify_tree(package_root, receipt["files"], True)
    require(receipt["gameSha256"] == runtime_contract(root)["gameAssemblySha256"], "Observer game target mismatch")
    require(package(package_root, "observer")["id"] == MENU_ID, "Wrong observer package ID")
    require({r["path"] for r in receipt["files"] if r["kind"] == "file"} ==
            {"About/About.xml", "Assemblies/FixtureMenuObserver.dll"} | {r["target"] for r in receipt.get("functionalArtwork", [])}
            | ({"Tools/win-x64/C10NativeProbe.dll", "Tools/C10NativeProbe.dds"} if receipt.get("nativeDeviceProbe") else set()),
            "Unexpected observer package contents")
    if receipt.get("nativeDeviceProbe"):
        probe = receipt["nativeDeviceProbe"]
        require(probe["path"] == "Tools/win-x64/C10NativeProbe.dll"
                and digest(package_root / probe["path"]) == probe["sha256"], "Native device probe identity mismatch")
        require(probe["payload"]["target"] == "Tools/C10NativeProbe.dds"
                and digest(package_root / "Tools/C10NativeProbe.dds") == probe["payload"]["sha256"], "Native probe payload identity mismatch")
    for row in receipt.get("functionalArtwork", []):
        require(row["target"].startswith("Textures/") and Path(row["target"]).suffix.lower() in (".png", ".dds")
                and digest(inside(package_root, package_root / row["target"])) == row["sha256"], "Observer artwork receipt mismatch")
    stage = root / "staging" / ("menu-observer-" + uuid.uuid4().hex)
    stage.mkdir(parents=True)
    rows = copy_stable(package_root, stage / "package")
    write(stage / "receipt.json", dict(receipt, files=rows, generation=manifest["generation"]))
    transaction = swap(root, [(stage / "package", root / "game/Mods" / MENU_LEAF),
                              (stage / "receipt.json", root / "menu-observer.json")], "deploy-menu-observer")
    return {"deployed": str(root / "game/Mods" / MENU_LEAF), "transaction": transaction}


def start_menu_clock(root, run):
    """Use raw Windows QPC in both processes; Unity Mono Stopwatch has a different origin."""
    frequency, counter = ctypes.c_longlong(), ctypes.c_longlong()
    require(ctypes.windll.kernel32.QueryPerformanceFrequency(ctypes.byref(frequency)), "QPC frequency unavailable")
    require(ctypes.windll.kernel32.QueryPerformanceCounter(ctypes.byref(counter)), "QPC counter unavailable")
    run["menuObserverRunId"] = uuid.uuid4().hex
    run["menuObserverClock"] = {"frequency": frequency.value, "startCounter": counter.value}
    folder = root / "profile/FixtureMenuObserver"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "launch-clock.txt").write_text(
        f'{run["menuObserverRunId"]}\n{frequency.value}\n{counter.value}\n', encoding="utf-8")


def menu_ready_signal(root, run):
    path = root / "profile/FixtureMenuObserver/menu-ready.json"
    if not run.get("menuObserver") or not path.is_file():
        return None
    try:
        signal = read(path)
        clock = run["menuObserverClock"]
        if (signal["schema"] != "fixture-menu-ready.v1" or signal["event"] != "menu-ready"
                or signal["runId"] != run["menuObserverRunId"] or signal["processId"] != run["pid"]
                or signal["counterFrequency"] != clock["frequency"]):
            return None
        elapsed = (signal["counter"] - clock["startCounter"]) / clock["frequency"]
        if not math.isfinite(elapsed) or elapsed < 0 or abs(elapsed - signal["elapsedSeconds"]) > 0.000002:
            return None
        return signal
    except (OSError, ValueError, KeyError, TypeError, ZeroDivisionError):
        return None


def user_settings(value, activation, purpose):
    """Recorded ordinary persisted feature choices, also usable in matched timing arms."""
    if value is None:
        return None
    require(activation == "user" and purpose in ("functional", "performance"),
            "User settings overrides require user activation and a valid purpose")
    require(isinstance(value, dict), "User settings must be a JSON object")
    require(set(value).issubset(USER_BOOLEAN_SETTINGS | {"preparedPreset", "cacheMaintenanceNextLaunch", "sharedCacheMiB"}),
            "Unknown user setting override")
    for key, selected in value.items():
        if key in USER_BOOLEAN_SETTINGS:
            require(type(selected) is bool, "User setting " + key + " requires a JSON boolean")
        elif key == "preparedPreset":
            require(type(selected) is int and 0 <= selected <= 2, "preparedPreset requires an integer from 0 to 2")
        elif key == "sharedCacheMiB":
            require(type(selected) is int and 1 <= selected <= 65536, "sharedCacheMiB requires an integer from 1 to 65536")
        else:
            require(type(selected) is str and selected in ("normal", "bypass", "rebuild", "clear"),
                    "Unknown cacheMaintenanceNextLaunch value")
    return dict(sorted(value.items()))


def profile_overrides(exit_after_menu_ready, settings, activation, purpose, language=None, texture_quality_probe=None, loading_ui_layout=None, audio_measurement=None, c14_background=None):
    overrides = {"runInBackground": True} if exit_after_menu_ready else {}
    if c14_background and c14_background.startswith("autostart-"):
        require(purpose == "functional", "Native autostart qualification is functional only")
        overrides.update(devMode=True, pauseOnLoad=not c14_background.endswith("-unpaused"),
                         runInBackground=c14_background.split("-")[1] == "true")
    if audio_measurement:
        overrides.update(screenWidth=1280, screenHeight=720, uiScale=1, volumeMaster=0)
    if loading_ui_layout:
        require(purpose == "functional", "Rendered UI layout is functional only")
        require(loading_ui_layout in ("1280x720@1", "1920x1080@1.5"), "Unknown rendered UI layout")
        width, height, scale = (1280, 720, 1) if loading_ui_layout == "1280x720@1" else (1920, 1080, 1.5)
        overrides.update(screenWidth=width, screenHeight=height, uiScale=scale, volumeMaster=0)
    normalized = user_settings(settings, activation, purpose)
    if normalized is not None:
        overrides["userSettings"] = normalized
    if language is not None:
        require(purpose in ("functional", "performance"), "Language overrides require a valid purpose")
        require(language in ("English", "French (Français)", "ChineseSimplified (简体中文)"), "Unsupported qualification language")
        overrides["langFolderName"] = language
        if purpose == "performance":
            overrides["volumeMaster"] = 0
    if texture_quality_probe == "unity-exposure":
        require(purpose == "functional", "Retained Unity texture probe is functional only")
        overrides["textureCompression"] = False
    return overrides


def apply_user_settings(profile, settings):
    if settings is None:
        return
    path = profile / "Config" / USER_SETTINGS_FILE
    doc = ET.parse(path) if path.exists() else ET.ElementTree(ET.Element("SettingsBlock"))
    require(doc.getroot().tag == "SettingsBlock", "Unexpected ordinary mod settings root")
    nodes = doc.findall("ModSettings")
    require(len(nodes) <= 1, "Duplicate ordinary mod settings block")
    node = nodes[0] if nodes else ET.SubElement(doc.getroot(), "ModSettings", {"Class": "WakeUp.WakeUpSettings"})
    require(node.get("Class") == "WakeUp.WakeUpSettings", "Unexpected ordinary mod settings class")
    for key, selected in settings.items():
        matches = node.findall(key)
        require(len(matches) <= 1, "Duplicate ordinary user setting " + key)
        entry = matches[0] if matches else ET.SubElement(node, key)
        entry.text = str(selected)
    path.parent.mkdir(parents=True, exist_ok=True)
    doc.write(path, encoding="utf-8", xml_declaration=True)


def png_cache_selected(mode, activation, png, settings):
    """A warm store can serve either the development selector or normal user choice."""
    return mode == "candidate" and (png in ("cache", "verify-cache") or (
        activation == "user" and (settings or {}).get("pngCache") is True
        and (settings or {}).get("cacheMaintenanceNextLaunch", "normal") == "normal"))


def verify_profile_overrides(profile, run):
    expected = profile_overrides(run.get("exitAfterMenuReady", False), run.get("userSettings"),
                                 run.get("activation", "fixture"), run.get("purpose", "performance"), run.get("language"), run.get("textureQualityProbe"), run.get("loadingUiLayout"), run.get("audioMeasurement"), run.get("c14Background"))
    require(run.get("profileOverrides", {}) == expected, "Prepared profile overrides drift (including runInBackground)")
    if run.get("exitAfterMenuReady"):
        require(ET.parse(profile / "Config/Prefs.xml").findtext("runInBackground") == str(expected["runInBackground"]),
                "Automatic launch background preference drift")
    if "langFolderName" in expected:
        require(ET.parse(profile / "Config/Prefs.xml").findtext("langFolderName") == expected["langFolderName"],
                "Prepared language drift")
    if "textureCompression" in expected:
        require(ET.parse(profile / "Config/Prefs.xml").findtext("textureCompression") == "False",
                "Retained Unity texture probe requires compression disabled")
    for key in ("screenWidth", "screenHeight", "uiScale", "volumeMaster", "devMode", "pauseOnLoad"):
        if key in expected:
            require(ET.parse(profile / "Config/Prefs.xml").findtext(key) == str(expected[key]), "Prepared loading UI layout drift: " + key)
    if "userSettings" in expected:
        doc = ET.parse(profile / "Config" / USER_SETTINGS_FILE)
        node = doc.find("ModSettings")
        require(doc.getroot().tag == "SettingsBlock" and len(doc.findall("ModSettings")) == 1
                and node.get("Class") == "WakeUp.WakeUpSettings", "Prepared user settings wrapper drift")
        for key, selected in expected["userSettings"].items():
            require(len(node.findall(key)) == 1 and node.findtext(key) == str(selected),
                    "Prepared user setting drift: " + key)


def gameplay_save_source(root, label, lane, generation, manifest_sha, active, exclusions):
    """Admit one fixture-created save, never a caller-selected filesystem path."""
    folder = inside(root, root / "results" / token(label))
    run_path = inside(root, folder / "run.json")
    previous = read(run_path)
    require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
            and previous.get("exitCode") == 0 and previous.get("exitAfterMenuReady") is True
            and previous.get("automaticTestPassed") is True and previous.get("gameplaySmoke") is True
            and previous.get("gameplayTestPassed") is True,
            "Gameplay save source must be a captured successful automatic gameplay run")
    content = lambda order: [pid for pid in order if pid not in (MENU_ID, PACKAGE_ID)]
    same_generation = (previous.get("generation") == generation and previous.get("manifestSha256") == manifest_sha)
    transition = None
    if not same_generation:
        # The approved Steam UI check carries only this accepted fixture-created
        # scene from the staged generation's direct frozen GOG parent.
        m, _, state = current(root)
        parent = {"generation": previous.get("generation"), "manifestSha256": previous.get("manifestSha256")}
        require(label == "c15a-owner-scene-setup-09"
                and m.get("frozenParent") == parent
                and state == {"generation": generation, "manifestSha256": manifest_sha}
                and runtime_contract(root)["target"] == "steam-rev590",
                "Gameplay save source is not the approved direct-parent Steam scene")
        transition = {"from": parent, "to": state, "policy": "Exact accepted fixture scene across approved Steam staging"}
    require(previous.get("lane") == lane
            and previous.get("excludedMods", []) == exclusions
            and content(previous.get("activeOrder", [])) == content(active),
            "Gameplay save source content selection mismatch")
    rows = {}
    for name in ("Saves/RloFixtureSmoke.rws", "FixtureMenuObserver/gameplay-smoke.json"):
        path = inside(root, folder / "profile" / name)
        recorded = [row for row in previous.get("evidence", []) if row["path"] == name]
        require(len(recorded) == 1 and recorded[0]["kind"] == "file" and path.is_file()
                and path.stat().st_size == recorded[0]["bytes"] and digest(path) == recorded[0]["sha256"],
                "Gameplay save source evidence drift: " + name)
        rows[name] = recorded[0]
    require(read(folder / "profile/FixtureMenuObserver/gameplay-smoke.json").get("passed") is True,
            "Captured gameplay receipt did not pass")
    source = folder / "profile/Saves/RloFixtureSmoke.rws"
    if transition is not None:
        require(rows["Saves/RloFixtureSmoke.rws"]["sha256"] == "e949173b778500b82bd8dfb65621b830ccb936cd013ad74e5396cdf33440f3e6",
                "Wrong accepted Steam inspection scene")
    reference = {"label": label, "runSha256": digest(run_path), "saveSha256": rows["Saves/RloFixtureSmoke.rws"]["sha256"],
                    "saveBytes": rows["Saves/RloFixtureSmoke.rws"]["bytes"],
                    "gameplayReceiptSha256": rows["FixtureMenuObserver/gameplay-smoke.json"]["sha256"]}
    if transition is not None:
        reference["platformTransition"] = transition
    return source, reference


def validate_audio_measurement(run, bootstrap_receipt):
    selected = run.get("audioMeasurement")
    if selected is None:
        return
    require(selected in ("absent", "off", "cold", "warm") and run.get("purpose") in ("functional", "performance")
            and run.get("mode") == "candidate" and run.get("menuObserver") and run.get("exitAfterMenuReady")
            and run.get("gameplaySmoke"), "Audio measurement requires candidate automatic gameplay observation")
    if selected == "absent":
        require(bootstrap_receipt is None and not run.get("audioBootstrapProbe") and not run.get("audioNativeEntryProbe"),
                "Absent audio measurement cannot select bootstrap or native entry registration")
        require(not any(arg.lower().startswith(("-monoprofiler", "--profile", "--fixture-c10-bootstrap-probe", "--fixture-c10-native-entry"))
                        for arg in run.get("arguments", [])), "Absent audio measurement cannot pass startup profiler or bootstrap arguments")
    else:
        require(run.get("audioBootstrapProbe") and run.get("audioNativeEntryProbe") and (bootstrap_receipt or {}).get("nativeEntry"),
                "Installed audio measurement requires qualified native bootstrap")
    require(not any(run.get(k) for k in (
        "audioPreparedProbe", "audioPatchProbe", "audioStreamProbe", "audioNativeEntryStopOnly", "audioNativeEntryDomainProbe",
        "residualProbe", "worldBackground", "backgroundHold", "gameplaySaveFrom", "nativeStackTraces",
        "languageObserver", "languageLifecycle", "languageSourceProbe", "textureStorageProbe", "textureSourceChange", "textureQualityProbe",
        "atlasLifecycle", "atlasCacheCorrupt", "loadingUiProbe", "loadingUiLayout", "loadingXmlRequestFrom",
        "xmlSourceObserver", "processedXmlObserver")), "Audio measurement cannot run diagnostic controls or alternate scene workloads")
    require(bool(run.get("audioCacheFrom")) == (selected == "warm"), "Only warm audio measurement requires an audio cache source")


def prepare(root, mode, label, lane="representative", observation="timing", strategy="startup-searches",
            foreign_cache_from=None, menu_observer=True, exit_after_menu_ready=True, gagarin_cache="off", png="off", png_source="native", png_cache_from=None, selection_path=None, loading_progress="off", character_presets="off", giddy_textures="off", activation="fixture", gameplay_smoke=False, residual_probe=False, purpose="performance", user_settings_path=None, language=None, prepared_cache_from=None, background_hold=False, gameplay_save_from=None, world_background=False, native_stack_traces=False, parsed_xml_cache_from=None, xml_source_observer=False, processed_xml_cache_from=None, inheritance_cache_from=None, processed_xml_observer=False, language_cache_from=None, language_lifecycle=False, language_source_probe=False, texture_storage_probe=False, texture_source_change=False, atlas_cache_from=None, atlas_cache_corrupt=False, atlas_lifecycle=False, texture_quality_probe=None, audio_stream_probe=False, audio_bootstrap_probe=False, loading_ui_probe=False, loading_ui_layout=None, loading_xml_request_from=None, audio_prepared_probe=None, audio_cache_from=None, audio_native_entry_probe=False, audio_native_entry_stop_only=False, audio_native_entry_domain_probe=False, audio_patch_probe=None, audio_measurement=None, language_observer=False, serialized_texture_input=None, demand_texture_probe=None, demand_texture_selection=None, c14_background=None, first_build_images=False, raw_pixel_helper=False):
    require(purpose in ("functional", "performance"), "Unknown run purpose")
    demand_texture = fixture_demand_texture.admit(demand_texture_probe, demand_texture_selection, purpose, exit_after_menu_ready, menu_observer, mode, selection_path, types.SimpleNamespace(**globals()))
    serialized_texture = fixture_serialized_texture.admit(serialized_texture_input, purpose, exit_after_menu_ready, menu_observer, mode, selection_path, types.SimpleNamespace(**globals()))
    require(not serialized_texture or not (audio_bootstrap_probe or audio_measurement or gameplay_smoke or texture_storage_probe or texture_quality_probe or loading_ui_probe), "Serialized texture probe requires its isolated workload")
    bootstrap_receipt = fixture_audio_bootstrap.admit(root, purpose, audio_bootstrap_probe, types.SimpleNamespace(**globals()), audio_measurement)
    validate_audio_measurement({
        "audioMeasurement": audio_measurement, "purpose": purpose, "mode": mode, "menuObserver": menu_observer,
        "exitAfterMenuReady": exit_after_menu_ready, "gameplaySmoke": gameplay_smoke,
        "audioBootstrapProbe": audio_bootstrap_probe, "audioNativeEntryProbe": audio_native_entry_probe,
        "audioCacheFrom": audio_cache_from, "audioPreparedProbe": audio_prepared_probe, "audioPatchProbe": audio_patch_probe,
        "audioStreamProbe": audio_stream_probe, "audioNativeEntryStopOnly": audio_native_entry_stop_only,
        "audioNativeEntryDomainProbe": audio_native_entry_domain_probe, "residualProbe": residual_probe,
        "worldBackground": world_background, "backgroundHold": background_hold, "gameplaySaveFrom": gameplay_save_from,
        "nativeStackTraces": native_stack_traces, "languageObserver": language_observer, "languageLifecycle": language_lifecycle, "languageSourceProbe": language_source_probe,
        "textureStorageProbe": texture_storage_probe, "textureSourceChange": texture_source_change, "textureQualityProbe": texture_quality_probe,
        "atlasLifecycle": atlas_lifecycle, "atlasCacheCorrupt": atlas_cache_corrupt, "loadingUiProbe": loading_ui_probe,
        "loadingUiLayout": loading_ui_layout, "loadingXmlRequestFrom": loading_xml_request_from,
        "xmlSourceObserver": xml_source_observer, "processedXmlObserver": processed_xml_observer}, bootstrap_receipt)
    require(not audio_native_entry_stop_only or (audio_native_entry_probe and not audio_prepared_probe), "Native stop-only probe requires native boundary selection without prepared audio")
    require(not audio_native_entry_domain_probe or (audio_native_entry_probe and not audio_prepared_probe and not audio_native_entry_stop_only), "Native domain probe requires separate native boundary selection")
    require(not audio_native_entry_probe or (audio_bootstrap_probe and (purpose == "functional" or audio_measurement) and (bootstrap_receipt or {}).get("nativeEntry")), "Native entry probe requires its functional bootstrap package")
    require(not (bootstrap_receipt or {}).get("nativeEntry") or audio_native_entry_probe, "Native entry package requires explicit native entry probe selection")
    require(not atlas_cache_corrupt or (atlas_cache_from and purpose == "functional"),
            "Atlas cache corruption requires --atlas-cache-from and functional purpose")
    require(mode in ("absent", "baseline", "candidate"), "Unknown fixture mode")
    require(menu_observer or not exit_after_menu_ready, "Automatic exit requires the menu observer")
    require(not audio_patch_probe or (audio_patch_probe in ("public", "same-file") and purpose == "functional" and menu_observer and exit_after_menu_ready
            and ((mode == "absent" and not audio_bootstrap_probe and not audio_prepared_probe)
                or (mode == "candidate" and audio_bootstrap_probe and audio_native_entry_probe
                    and (bootstrap_receipt or {}).get("nativeEntry")
                    and audio_prepared_probe in ("formats-prepare", "formats-warm")))),
            "Public audio patch probe requires functional automatic observation: absent without bootstrap or native-bootstrap candidate formats probe")
    require(not audio_stream_probe or (purpose == "functional" and menu_observer and exit_after_menu_ready
            and not gameplay_smoke and not world_background and not language_lifecycle),
            "Audio stream probe requires functional automatic observation with ordinary menu exit")
    require(not audio_bootstrap_probe or ((purpose == "functional" or audio_measurement) and menu_observer and exit_after_menu_ready
            and mode in ("baseline", "candidate") and (audio_measurement or not gameplay_smoke or audio_prepared_probe in ("warm", "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm")) and not world_background
            and not language_lifecycle and not audio_stream_probe and not residual_probe and not atlas_lifecycle
            and not texture_storage_probe and not texture_quality_probe and not language_source_probe),
            "Audio bootstrap probe requires core-present functional automatic observation with ordinary menu exit and no other probe")
    require(not audio_prepared_probe or (audio_bootstrap_probe and bootstrap_receipt is not None
            and audio_prepared_probe in ("completion", "prepare", "warm", "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm")), "Prepared audio probe requires the qualified experimental preloader")
    require(audio_prepared_probe not in ("frames-prepare", "frames-warm") or audio_native_entry_probe, "Frozen-provider frame prototype requires native entry selection")
    require(not audio_cache_from or audio_measurement == "warm" or audio_prepared_probe in ("warm", "natural-warm", "wav-warm", "formats-warm", "frames-warm", "readiness-warm", "advance-warm"), "Audio cache carry requires the warm functional probe")
    require(audio_prepared_probe not in ("warm", "natural-warm", "wav-warm", "formats-warm", "frames-warm", "readiness-warm", "advance-warm") or audio_cache_from is not None, "Warm audio probe requires captured prepared audio")
    require(audio_prepared_probe not in ("natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm") or gameplay_smoke, "Natural audio probe requires native gameplay playback")
    settings = user_settings(read(physical(user_settings_path)), activation, purpose) if user_settings_path is not None else None
    require(not loading_ui_layout or (purpose == "functional" and menu_observer and exit_after_menu_ready),
            "Muted UI layout requires a functional automatic observer run")
    overrides = profile_overrides(exit_after_menu_ready, settings, activation, purpose, language, texture_quality_probe, loading_ui_layout, audio_measurement, c14_background)
    arguments = run_arguments(root, mode, observation, strategy, exit_after_menu_ready, gagarin_cache, png, png_source, loading_progress, character_presets, giddy_textures, activation, gameplay_smoke, residual_probe, background_hold, gameplay_save_from, purpose, settings, world_background, native_stack_traces, xml_source_observer, processed_xml_observer, c14_background, first_build_images, raw_pixel_helper)
    arguments.extend(fixture_demand_texture.arguments(root, demand_texture))
    if serialized_texture:
        arguments.append("--fixture-c10-serialized-texture")
    if texture_quality_probe:
        require(purpose == "functional" and menu_observer and exit_after_menu_ready and mode == "candidate", "Quality probe requires functional candidate automatic observation")
        require(texture_quality_probe != "inspection-scene" or (gameplay_smoke and gameplay_save_from and native_stack_traces),
                "Inspection scene requires copied-save gameplay and ordinary native logging")
        arguments.append("--fixture-c08-quality=" + texture_quality_probe)
    if loading_ui_probe:
        require(purpose == 'functional' and menu_observer and exit_after_menu_ready and mode == 'candidate', 'Loading UI proof requires functional candidate automatic observation')
        arguments.append('--fixture-c13-loading-probe')
    if audio_measurement:
        arguments.append("--fixture-c10-audio-measurement=" + audio_measurement)
    if audio_patch_probe:
        arguments.append("--fixture-c10-public-patch-probe=" + audio_patch_probe)
    if audio_stream_probe:
        arguments.append("--fixture-c10-stream-probe")
    if audio_bootstrap_probe:
        arguments.append("--fixture-c10-bootstrap-probe")
    if audio_native_entry_probe:
        arguments.extend(["-monoProfiler", "wakeupentry", "--fixture-c10-native-entry-probe"])
    if audio_native_entry_stop_only:
        arguments.append("--fixture-c10-native-entry-stop-only")
    if audio_native_entry_domain_probe:
        arguments.append("--fixture-c10-native-entry-domain-probe")
    if audio_prepared_probe:
        arguments.append("--fixture-c10-prepared-audio=" + audio_prepared_probe)
    require(not texture_source_change or texture_storage_probe, "Texture source change requires its functional probe")
    if texture_storage_probe:
        require(purpose == "functional" and menu_observer and exit_after_menu_ready, "Texture storage probes require functional automatic observation")
        arguments.append("--fixture-c05-texture-probe")
        if texture_source_change: arguments.append("--fixture-c05-source-change")
    if purpose == "functional" and exit_after_menu_ready and not audio_measurement:
        arguments.append("--fixture-functional-probes")
        if not atlas_lifecycle and ((settings or {}).get("staticAtlases") or (settings or {}).get("atlasBatching")):
            arguments.append("--fixture-atlas-probe")
    if atlas_lifecycle:
        require(purpose == "functional" and menu_observer and exit_after_menu_ready
                and mode == "candidate" and (settings or {}).get("atlasBatching") is True,
                "Atlas lifecycle requires functional automatic observation and user atlas batching")
        arguments.append("--fixture-atlas-lifecycle")
    if language_observer:
        require(menu_observer and exit_after_menu_ready, "Language observation requires automatic menu observation")
        arguments.append("--fixture-language-observer")
    if language_lifecycle:
        require(purpose == "functional" and menu_observer and exit_after_menu_ready
                and not gameplay_smoke and not world_background, "Language lifecycle requires an isolated functional menu run")
        arguments.append("--fixture-language-lifecycle")
    if language_source_probe:
        require(purpose == "functional" and menu_observer and exit_after_menu_ready
                and not gameplay_smoke and not world_background, "Language source probe requires an isolated functional menu run")
        arguments.append("--fixture-language-source-probe")
    check = audit(root)
    m, gen, state = current(root)
    receipt = read(root / "candidate.json") if (root / "candidate.json").is_file() else None
    if mode != "absent":
        require(receipt is not None, "Deploy the observation-capable package for baseline/candidate modes")
        require(candidate_matches_snapshot(root, m, gen), "Deployed package predates fixture runtime integration or targets another build; rebuild/deploy")
    menu_receipt = read(root / "menu-observer.json") if menu_observer and (root / "menu-observer.json").is_file() else None
    require(not menu_observer or menu_receipt is not None, "Build/deploy the menu observer before selecting it")
    require(not audio_stream_probe or (menu_receipt or {}).get("supportsAudioStreamProbe"), "Rebuild observer for audio stream probe")
    require(not audio_bootstrap_probe or (menu_receipt or {}).get("supportsAudioBootstrapProbe"), "Rebuild observer for audio bootstrap probe")
    require(not audio_native_entry_probe or (menu_receipt or {}).get("supportsAudioNativeEntryProbe"), "Rebuild observer for native entry probe")
    require(not (menu_receipt or {}).get("nativeDeviceProbe") or (purpose == "functional" and exit_after_menu_ready),
            "Experimental native device observer requires functional automatic exit")
    require(not native_stack_traces or menu_receipt.get("supportsNativeStackTraces"), "Rebuild observer for native stack traces")
    require(not exit_after_menu_ready or menu_receipt.get("supportsAutomaticExit"), "Rebuild/deploy the observer with automatic exit support")
    require(not gameplay_smoke or menu_receipt.get("supportsGameplaySmoke"), "Rebuild observer for gameplay smoke")
    require(not background_hold or menu_receipt.get("supportsBackgroundHold"), "Rebuild observer for background hold")
    require(not world_background or menu_receipt.get("supportsWorldBackground"), "Rebuild observer for world background")
    require(gameplay_save_from is None or not gameplay_smoke or menu_receipt.get("supportsGameplaySave"), "Rebuild observer for captured gameplay saves")
    require(not residual_probe or menu_receipt.get("supportsResidualProbe"), "Rebuild observer for residual probe")
    exclusions = excluded_mods(root, m, state)
    excluded_ids = {p["id"] for p in exclusions}
    selection = benchmark_selection(m, state, read(physical(selection_path)), excluded_ids) if selection_path else None
    active = run_order(m, lane, mode, menu_observer, excluded_ids, selection)
    label = token(label)
    results = root / "results" / label
    require(not results.exists(), "Run label already used")
    gameplay_save = gameplay_reference = None
    require(not c14_background or (menu_receipt.get("supportsC14Background") and loading_ui_layout and not audio_bootstrap_probe and not audio_measurement and not loading_ui_probe and not serialized_texture and not demand_texture), "C14 requires its observer, muted layout and no other probe")
    require(not (c14_background and c14_background.startswith("autostart-")) or gameplay_save_from is not None,
            "Native autostart requires a verified copied fixture save")
    if gameplay_save_from is not None:
        gameplay_save, gameplay_reference = gameplay_save_source(root, gameplay_save_from, lane,
            m["generation"], state["manifestSha256"], active, exclusions)
    foreign_cached = None
    terrain_cached = None
    if foreign_cache_from:
        previous_folder = inside(root, root / "results" / token(foreign_cache_from))
        previous = read(previous_folder / "run.json")
        require(previous.get("excludedMods", []) == exclusions and previous.get("profileOverrides", {}) == overrides,
                "Cache selected content or profile override mismatch")
        require(previous.get("menuObserver", False) == menu_observer
                and previous.get("menuObserverPackage") == menu_receipt
                and previous.get("exitAfterMenuReady", False) == exit_after_menu_ready, "Foreign cache observer configuration mismatch")
        require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
                and previous.get("exitCode") == 0 and previous.get("mode") == mode
                and (not exit_after_menu_ready or previous.get("automaticTestPassed") is True)
                and previous.get("lane") == lane and previous.get("generation") == m["generation"]
                and previous.get("manifestSha256") == state["manifestSha256"]
                and (mode == "absent" or previous.get("candidate") == receipt),
                "Foreign cache source must be a captured successful matching mode/lane/generation/package run")
        require(previous.get("activeOrder") == active, "Foreign cache active order mismatch")
        # This is the one confirmed Gagarin cache directory, never a caller-selected path.
        prefix = "MissileGirl/"
        foreign_rows = [dict(row, path=row["path"][len(prefix):]) for row in previous.get("evidence", [])
                        if row["path"].startswith(prefix)]
        require(any(row["kind"] == "file" for row in foreign_rows), "Foreign source has no recorded MissileGirl cache files")
        foreign_cached = inside(root, previous_folder / "profile/MissileGirl")
        verify_tree(foreign_cached, foreign_rows, True)
        # The selected supplier also persists this small terrain-color cache.
        # Restore only this exact recorded file, never all captured Config/settings.
        terrain_rows = [row for row in previous.get("evidence", [])
                        if row["path"] == "Config/TrueTerrainColorsCache.xml"]
        if terrain_rows:
            require(len(terrain_rows) == 1 and terrain_rows[0]["kind"] == "file", "Invalid terrain cache evidence")
            terrain_cached = inside(root, previous_folder / "profile/Config/TrueTerrainColorsCache.xml")
            require(terrain_cached.stat().st_size == terrain_rows[0]["bytes"]
                    and digest(terrain_cached) == terrain_rows[0]["sha256"], "Terrain cache content drift")
    png_cached = None
    if png_cache_from:
        require(png_cache_selected(mode, activation, png, settings), "PNG cache restore requires cache mode or normal user pngCache=true")
        previous_folder = inside(root, root / "results" / token(png_cache_from))
        previous = read(previous_folder / "run.json")
        require(previous.get("automaticTestPassed") is True and previous.get("exitCode") == 0, "PNG cache source did not pass")
        expected = {"lane": lane, "mode": mode, "strategy": strategy, "generation": m["generation"], "candidate": receipt,
                    "activeOrder": active, "manifestSha256": state["manifestSha256"], "pngSource": png_source,
                    "gagarinCache": gagarin_cache, "menuObserverPackage": menu_receipt, "profileOverrides": overrides, "excludedMods": exclusions}
        require(all(previous.get(k) == v for k, v in expected.items()), "PNG cache source configuration mismatch")
        require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
                and png_cache_selected(previous.get("mode"), previous.get("activation"),
                                       previous.get("png"), previous.get("userSettings")), "PNG cache source mode mismatch")
        prefix = "WakeUp/PngCache/"
        png_rows = [dict(row, path=row["path"][len(prefix):]) for row in previous.get("evidence", []) if row["path"].startswith(prefix)]
        require(any(row["kind"] == "file" for row in png_rows), "PNG cache source empty")
        require(all((row["kind"] == "directory" and row["path"] == "v1")
                    or (row["kind"] == "file" and re.fullmatch(r"v1/(?:[A-F0-9]{64}\.bin|usage)", row["path"]))
                    or (row["kind"] == "file" and row["path"] == "v1/.owner" and row["bytes"] == 0)
                    for row in png_rows), "Unexpected PNG cache files")
        png_cached = inside(root, previous_folder / "profile/WakeUp/PngCache")
        verify_tree(png_cached, png_rows, True)
    prepared_cached = None
    prepared_reference = None
    if prepared_cache_from:
        maintenance_case = purpose == "functional" and (settings or {}).get("cacheMaintenanceNextLaunch") in ("bypass", "rebuild", "clear")
        fixture_texture_cache = activation == "fixture" and png in ("cache", "verify-cache")
        require(mode == "candidate" and (fixture_texture_cache or (activation == "user"
                and ((settings or {}).get("preparedTextures") is True or (settings or {}).get("pngCache") is True or maintenance_case))),
                "Prepared cache restore requires explicit fixture PNG cache mode, user texture caching or functional cache maintenance")
        previous_folder = inside(root, root / "results" / token(prepared_cache_from))
        previous_path = previous_folder / "run.json"
        previous = read(previous_path)
        require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
                and previous.get("exitCode") == 0
                and (previous.get("automaticTestPassed") is True or previous.get("exitAfterMenuReady") is False),
                "Prepared cache source must be a captured successful automatic or manual run")
        signal = menu_ready_signal(previous_folder, previous)
        require(signal is not None and signal == previous.get("menuReadyObservation"),
                "Prepared cache source requires a valid captured menu observation")
        signal_path = inside(root, previous_folder / "profile/FixtureMenuObserver/menu-ready.json")
        signal_rows = [row for row in previous.get("evidence", []) if row["path"] == "FixtureMenuObserver/menu-ready.json"]
        require(len(signal_rows) == 1 and signal_rows[0]["kind"] == "file"
                and signal_rows[0]["bytes"] == signal_path.stat().st_size
                and signal_rows[0]["sha256"] == digest(signal_path), "Prepared cache menu evidence drift")
        # Preparation is an on-demand public action; its next launch can opt in
        # with different settings and observer instrumentation. Source content,
        # native game and product identities must still match exactly.
        expected = {"lane": lane, "mode": mode, "generation": m["generation"], "candidate": receipt,
                    "activeOrder": active, "manifestSha256": state["manifestSha256"],
                    "pngSource": png_source, "excludedMods": exclusions}
        if fixture_texture_cache:
            # Automatic PNG/DDS entries now share the prepared store. Keep the
            # development comparison's controls bound as in the legacy PNG route;
            # user preparation still permits changed settings/instrumentation.
            expected.update(activation="fixture", strategy=strategy, gagarinCache=gagarin_cache,
                            menuObserverPackage=menu_receipt, profileOverrides=overrides)
            require(previous.get("png") in ("cache", "verify-cache"), "Prepared cache source PNG mode mismatch")
        require(all(previous.get(k) == v for k, v in expected.items()), "Prepared cache source configuration mismatch")
        prefix = "WakeUp/PreparedTextures/"
        prepared_rows = [dict(row, path=row["path"][len(prefix):]) for row in previous.get("evidence", [])
                         if row["path"].startswith(prefix)]
        require(any(row["kind"] == "file" and (row["path"].endswith(".bin") or row["path"] == "v1/groups/index") for row in prepared_rows), "Prepared cache source empty")
        require(all((row["kind"] == "directory" and row["path"] in ("v1", "v1/exports", "v1/groups"))
                    or (row["kind"] == "file" and re.fullmatch(r"v1/(?:[A-F0-9]{64}\.bin|usage)", row["path"]))
                    or (row["kind"] == "file" and re.fullmatch(r"v1/groups/(?:index|[a-f0-9]{32}\.group|[a-f0-9]{16}_[a-f0-9]{32}\.delta)", row["path"]))
                    or (row["kind"] == "file" and re.fullmatch(r"v1/exports/[A-F0-9]{64}(?:\.manifest|\.[A-F0-9]{64}\.dds)", row["path"]))
                    or (row["kind"] == "file" and row["path"] == "v1/.owner" and row["bytes"] == 0)
                    for row in prepared_rows), "Unexpected prepared cache files")
        prepared_cached = inside(root, previous_folder / "profile/WakeUp/PreparedTextures")
        verify_tree(prepared_cached, prepared_rows, True)
        prepared_reference = {"label": prepared_cache_from, "runSha256": digest(previous_path),
                              "menuObserverRunId": previous["menuObserverRunId"], "capturedUtc": previous.get("capturedUtc"),
                              "storeInventorySha256": identity(prepared_rows)}
    parsed_cached = parsed_reference = None
    if parsed_xml_cache_from:
        selected = settings or {}
        maintenance = selected.get("cacheMaintenanceNextLaunch", "normal")
        require(mode == "candidate" and activation == "user"
                and ((selected.get("parsedXml") is True and maintenance == "normal")
                     or (purpose == "functional" and (selected.get("parsedXml") is False
                         or maintenance in ("bypass", "rebuild", "clear")))),
                "Parsed XML restoration requires user parsedXml=true or a functional disabled/maintenance case")
        previous_folder = inside(root, root / "results" / token(parsed_xml_cache_from))
        previous_path = inside(root, previous_folder / "run.json")
        previous = read(previous_path)
        previous_settings = previous.get("userSettings") or {}
        require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
                and previous.get("exitCode") == 0
                and (previous.get("automaticTestPassed") is True or previous.get("exitAfterMenuReady") is False)
                and previous.get("activation") == "user" and previous_settings.get("parsedXml") is True
                and previous_settings.get("cacheMaintenanceNextLaunch", "normal") in ("normal", "rebuild"),
                "Parsed XML source must be a captured successful enabled ordinary-settings run")
        signal = menu_ready_signal(previous_folder, previous)
        require(signal is not None and signal == previous.get("menuReadyObservation"), "Parsed XML source menu observation mismatch")
        signal_path = inside(root, previous_folder / "profile/FixtureMenuObserver/menu-ready.json")
        signal_rows = [r for r in previous.get("evidence", []) if r["path"] == "FixtureMenuObserver/menu-ready.json"]
        require(len(signal_rows) == 1 and signal_rows[0]["kind"] == "file"
                and signal_rows[0]["bytes"] == signal_path.stat().st_size
                and signal_rows[0]["sha256"] == digest(signal_path), "Parsed XML source menu evidence drift")
        expected = {"lane": lane, "mode": mode, "generation": m["generation"], "candidate": receipt,
                    "activeOrder": active, "manifestSha256": state["manifestSha256"], "pngSource": png_source,
                    "excludedMods": exclusions, "menuObserverPackage": menu_receipt, "language": language}
        require(all(previous.get(k) == v for k, v in expected.items()), "Parsed XML source configuration mismatch")
        variable = {"parsedXml", "sharedCacheMiB", "cacheMaintenanceNextLaunch"} if purpose == "functional" else set()
        require({k: v for k, v in previous_settings.items() if k not in variable}
                == {k: v for k, v in selected.items() if k not in variable}, "Parsed XML source settings mismatch")
        prefix = "WakeUp/ParsedXml/"
        parsed_rows = [dict(r, path=r["path"][len(prefix):]) for r in previous.get("evidence", []) if r["path"].startswith(prefix)]
        require(any(r["kind"] == "file" and r["path"].endswith(".bin") for r in parsed_rows), "Parsed XML source cache empty")
        require(all((r["kind"] == "directory" and r["path"] == "v1")
                    or (r["kind"] == "file" and re.fullmatch(r"v1/(?:[A-F0-9]{64}\.bin|usage)", r["path"]))
                    or (r["kind"] == "file" and r["path"] == "v1/.owner" and r["bytes"] == 0)
                    for r in parsed_rows), "Unexpected parsed XML cache files")
        parsed_cached = inside(root, previous_folder / "profile/WakeUp/ParsedXml")
        verify_tree(parsed_cached, parsed_rows, True)
        parsed_reference = {"label": parsed_xml_cache_from, "runSha256": digest(previous_path),
                            "menuObserverRunId": previous["menuObserverRunId"], "capturedUtc": previous.get("capturedUtc"),
                            "storeInventorySha256": identity(parsed_rows)}
    extra_xml = {}
    for setting, category, cache_label in (("processedXml", "ProcessedXml", processed_xml_cache_from),
                                         ("resolvedInheritance", "ResolvedInheritance", inheritance_cache_from),
                                         ("parsedLanguage", "ParsedLanguage", language_cache_from),
                                         ("staticAtlases", "StaticAtlases", atlas_cache_from)):
        if not cache_label:
            continue
        selected = settings or {}
        maintenance = selected.get("cacheMaintenanceNextLaunch", "normal")
        require(mode == "candidate" and activation == "user"
                and ((selected.get(setting) is True and maintenance == "normal")
                     or (purpose == "functional" and (selected.get(setting) is False
                         or maintenance in ("bypass", "rebuild", "clear")))),
                "Owned cache restoration requires user " + setting + "=true or a functional disabled/maintenance case")
        previous_folder = inside(root, root / "results" / token(cache_label))
        previous_path = inside(root, previous_folder / "run.json")
        previous = read(previous_path)
        previous_settings = previous.get("userSettings") or {}
        require(previous.get("status") == "captured-awaiting-human-and-runtime-review"
                and previous.get("exitCode") == 0
                and (previous.get("automaticTestPassed") is True or previous.get("exitAfterMenuReady") is False)
                and previous.get("activation") == "user" and previous_settings.get(setting) is True
                and previous_settings.get("cacheMaintenanceNextLaunch", "normal") in ("normal", "rebuild"),
                "Owned cache source must be a captured successful enabled ordinary-settings run")
        signal = menu_ready_signal(previous_folder, previous)
        require(signal is not None and signal == previous.get("menuReadyObservation"), "Owned cache source menu observation mismatch")
        signal_path = inside(root, previous_folder / "profile/FixtureMenuObserver/menu-ready.json")
        signal_rows = [r for r in previous.get("evidence", []) if r["path"] == "FixtureMenuObserver/menu-ready.json"]
        require(len(signal_rows) == 1 and signal_rows[0]["kind"] == "file"
                and signal_rows[0]["bytes"] == signal_path.stat().st_size
                and signal_rows[0]["sha256"] == digest(signal_path), "Owned cache source menu evidence drift")
        expected = {"lane": lane, "mode": mode, "generation": m["generation"], "candidate": receipt,
                    "activeOrder": active, "manifestSha256": state["manifestSha256"], "pngSource": png_source,
                    "excludedMods": exclusions, "menuObserverPackage": menu_receipt, "language": language}
        # Functional language switching deliberately presents old-language entries
        # to the product's content/language key. This never relaxes frozen source,
        # game, package, observer or capture integrity checks.
        if category == "ParsedLanguage" and purpose == "functional":
            expected.pop("language")
        if category == "StaticAtlases":
            expected.update(strategy=strategy, gagarinCache=gagarin_cache, png=png,
                            benchmarkSelection=selection,
                            benchmarkSelectionSha256=identity(selection) if selection else None)
            require({k: v for k, v in (previous.get("profileOverrides") or {}).items() if k != "userSettings"}
                    == {k: v for k, v in overrides.items() if k != "userSettings"}, "Atlas source profile overrides mismatch")
        require(all(previous.get(k) == v for k, v in expected.items()), "Owned cache source configuration mismatch")
        variable = {"parsedXml", "processedXml", "resolvedInheritance", "xmlQueryExtensions", "sharedCacheMiB", "cacheMaintenanceNextLaunch"} if purpose == "functional" else set()
        if category == "ParsedLanguage" and purpose == "functional":
            variable.add("parsedLanguage")
        if category == "StaticAtlases":
            # Only atlas enablement/batching and shared maintenance controls may
            # vary in a functional restoration case. Other feature settings,
            # selection/order, source, runtime and observer identities stay bound.
            variable = {"staticAtlases", "atlasBatching", "sharedCacheMiB", "cacheMaintenanceNextLaunch"} if purpose == "functional" else set()
        require({k: v for k, v in previous_settings.items() if k not in variable}
                == {k: v for k, v in selected.items() if k not in variable}, "Owned cache source settings mismatch")
        prefix = "WakeUp/" + category + "/"
        extra_rows = [dict(r, path=r["path"][len(prefix):]) for r in previous.get("evidence", []) if r["path"].startswith(prefix)]
        grouped_atlas = category == "StaticAtlases"
        require(any(r["kind"] == "file" and (r["path"] == "v1/groups/index" if grouped_atlas
                    else r["path"].endswith(".bin")) for r in extra_rows), "Owned cache source cache empty")
        require(all((r["kind"] == "directory" and (r["path"] == "v1" or grouped_atlas and r["path"] == "v1/groups"))
                    or (r["kind"] == "file" and re.fullmatch(r"v1/(?:[A-F0-9]{64}\.bin|usage)", r["path"]))
                    or (grouped_atlas and r["kind"] == "file"
                        and re.fullmatch(r"v1/groups/(?:index|[a-f0-9]{32}\.group|[a-f0-9]{16}_[a-f0-9]{32}\.delta)", r["path"]))
                    or (r["kind"] == "file" and r["path"] == "v1/.owner" and r["bytes"] == 0)
                    for r in extra_rows), "Unexpected owned cache files")
        extra_cached = inside(root, previous_folder / "profile/WakeUp" / category)
        verify_tree(extra_cached, extra_rows, True)
        extra_reference = {"label": cache_label, "runSha256": digest(previous_path),
                            "menuObserverRunId": previous["menuObserverRunId"], "capturedUtc": previous.get("capturedUtc"),
                            "storeInventorySha256": identity(extra_rows)}
        if category == "ParsedLanguage":
            extra_reference["language"] = previous.get("language")
            extra_reference["manifestSha256"] = previous["manifestSha256"]
        extra_xml[category] = (extra_cached, extra_rows, extra_reference)
    audio_cached = audio_rows = audio_reference = None
    if audio_cache_from:
        require(purpose == "functional" or audio_measurement == "warm", "Audio cache carry requires functional qualification or explicit warm measurement")
        audio_cached, audio_rows, audio_reference = fixture_audio_bootstrap.cache_source(root, audio_cache_from,
            {"lane": lane, "mode": mode, "generation": m["generation"], "manifestSha256": state["manifestSha256"],
             "candidate": receipt, "menuObserverPackage": menu_receipt, "activeOrder": active,
             "excludedMods": exclusions, "activation": activation, "userSettings": settings,
             "audioPreloaderPackage": bootstrap_receipt,
             **({"audioMeasurement": "cold", "purpose": purpose, "gameplaySmoke": gameplay_smoke,
                 "strategy": strategy, "observation": observation, "profileOverrides": overrides, "language": language,
                 "gagarinCache": gagarin_cache, "png": png, "pngSource": png_source, "loadingProgress": loading_progress,
                 "characterPresets": character_presets, "giddyTextures": giddy_textures,
                 "preparedCacheFrom": prepared_cache_from, "pngCacheFrom": png_cache_from,
                 "parsedXmlCacheFrom": parsed_xml_cache_from, "processedXmlCacheFrom": processed_xml_cache_from,
                 "inheritanceCacheFrom": inheritance_cache_from, "languageCacheFrom": language_cache_from,
                 "atlasCacheFrom": atlas_cache_from, "foreignCacheFrom": foreign_cache_from}
                if audio_measurement else {"audioPreparedProbe": {"natural-warm": "natural-prepare", "wav-warm": "wav-prepare", "formats-warm": "formats-prepare", "frames-warm": "frames-prepare", "readiness-warm": "readiness-prepare", "advance-warm": "advance-prepare"}.get(audio_prepared_probe, "prepare")})}, types.SimpleNamespace(**globals()))
    results.mkdir(parents=True)
    staged_profile = root / "staging" / ("profile-" + uuid.uuid4().hex)
    shutil.copytree(gen / "seed", staged_profile)
    xml = staged_profile / "Config/ModsConfig.xml"
    doc = ET.parse(xml)
    enabled = doc.find("activeMods")
    enabled[:] = []
    for pid in active:
        ET.SubElement(enabled, "li").text = pid
    doc.write(xml, encoding="utf-8", xml_declaration=True)
    if exit_after_menu_ready or language is not None or "textureCompression" in overrides:
        prefs = staged_profile / "Config/Prefs.xml"
        prefs_doc = ET.parse(prefs) if prefs.exists() else ET.ElementTree(ET.Element("PrefsData"))
        for key in ("runInBackground", "langFolderName", "textureCompression", "screenWidth", "screenHeight", "uiScale", "volumeMaster", "devMode", "pauseOnLoad"):
            if key not in overrides:
                continue
            entry = prefs_doc.find(key)
            if entry is None:
                entry = ET.SubElement(prefs_doc.getroot(), key)
            entry.text = str(overrides[key])
        prefs_doc.write(prefs, encoding="utf-8", xml_declaration=True)
    apply_user_settings(staged_profile, settings)
    fixture_serialized_texture.stage(staged_profile, serialized_texture, types.SimpleNamespace(**globals()))
    fixture_demand_texture.stage(staged_profile, demand_texture, types.SimpleNamespace(**globals()))
    if loading_xml_request_from:
        require(purpose == "functional" and mode == "candidate" and menu_observer and exit_after_menu_ready,
                "XML request replay requires a functional automatic candidate run")
        source_folder = root / "results" / token(loading_xml_request_from)
        source_run = read(source_folder / "run.json")
        request_relative = "WakeUp/xml-export-next-launch.request"
        source_request = source_folder / "profile" / request_relative
        request_row = next((r for r in source_run.get("evidence", []) if r["path"] == request_relative), None)
        require(source_run.get("automaticTestPassed") and source_run.get("capturedUtc") and request_row is not None,
                "XML request must come from a captured successful functional handler run")
        require(source_request.is_file() and source_request.stat().st_size < 4096 and digest(source_request) == request_row["sha256"],
                "Captured XML request changed")
        target_request = staged_profile / request_relative
        target_request.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_request, target_request)
    if gameplay_save is not None:
        target = staged_profile / "Saves/RloFixtureSmoke.rws"
        target.parent.mkdir(parents=True, exist_ok=True)
        require(not target.exists(), "Seed unexpectedly contains the fixture gameplay save")
        shutil.copy2(gameplay_save, target)
        require(digest(target) == gameplay_reference["saveSha256"]
                and digest(gameplay_save) == gameplay_reference["saveSha256"], "Gameplay save changed while copying")
        if c14_background and c14_background.startswith("autostart-"):
            autostart = staged_profile / "Saves/autostart.rws"
            require(not autostart.exists(), "Seed unexpectedly contains autostart save")
            shutil.copy2(gameplay_save, autostart)
            require(digest(autostart) == gameplay_reference["saveSha256"], "Autostart copy identity drift")
    if foreign_cached is not None:
        shutil.copytree(foreign_cached, staged_profile / "MissileGirl")
        verify_tree(staged_profile / "MissileGirl", foreign_rows, True)
    if terrain_cached is not None:
        terrain_target = staged_profile / "Config/TrueTerrainColorsCache.xml"
        require(not terrain_target.exists(), "Seed unexpectedly contains terrain cache")
        shutil.copy2(terrain_cached, terrain_target)
        require(digest(terrain_target) == terrain_rows[0]["sha256"], "Terrain cache changed while copying")
    if png_cached is not None:
        shutil.copytree(png_cached, staged_profile / "WakeUp/PngCache")
        verify_tree(staged_profile / "WakeUp/PngCache", png_rows, True)
    if prepared_cached is not None:
        shutil.copytree(prepared_cached, staged_profile / "WakeUp/PreparedTextures")
        verify_tree(staged_profile / "WakeUp/PreparedTextures", prepared_rows, True)
    if audio_cached is not None:
        copy_stable(audio_cached, staged_profile / "WakeUp/PreparedAudio", audio_rows)
    if parsed_cached is not None:
        copy_stable(parsed_cached, staged_profile / "WakeUp/ParsedXml", parsed_rows)
        verify_tree(staged_profile / "WakeUp/ParsedXml", parsed_rows, True)
    for category, (source, rows, reference) in extra_xml.items():
        copy_stable(source, staged_profile / "WakeUp" / category, rows)
        verify_tree(staged_profile / "WakeUp" / category, rows, True)
    atlas_corruption = None
    if atlas_cache_corrupt:
        # Corrupt one payload byte only after the captured owner store has been
        # copied and verified. Captured sources and authored inputs stay intact.
        retained = sorted(r["path"] for r in extra_xml["StaticAtlases"][1]
                          if r["kind"] == "file" and re.fullmatch(r"v1/groups/[a-f0-9]{32}\.group", r["path"]))
        require(retained, "Atlas corruption source has no retained group payload")
        relative = "WakeUp/StaticAtlases/" + retained[0]
        target = inside(root, staged_profile / relative)
        before_hash = digest(target)
        with target.open("r+b") as stream:
            header = stream.read(113)  # GroupedTextureBlocks.BlockHeader; WTB1.
            require(len(header) == 113 and int.from_bytes(header[:4], "little") == 0x31425457
                    and re.fullmatch(b"[A-F0-9]{64}", header[4:68]), "Unexpected atlas group block header")
            stored = int.from_bytes(header[76:80], "little", signed=True)
            require(stored > 0 and 113 + stored <= target.stat().st_size, "Invalid atlas group block payload")
            offset = 113 + stored // 2
            stream.seek(offset)
            before_byte = stream.read(1)[0]
            stream.seek(offset)
            stream.write(bytes([before_byte ^ 1]))
        atlas_corruption = {"path": relative, "offset": offset, "bytes": target.stat().st_size,
                            "beforeByte": before_byte, "afterByte": before_byte ^ 1,
                            "beforeSha256": before_hash, "afterSha256": digest(target),
                            "entryKey": header[4:68].decode("ascii"),
                            "blockOrdinal": int.from_bytes(header[68:72], "little", signed=True)}
    staged_selection = root / "staging" / ("selection-" + uuid.uuid4().hex + ".json")
    write(staged_selection, {"label": label})
    replacements = []
    if receipt is not None:
        location = candidate_location(root)
        target = root / "parked-rlo/package" if mode == "absent" else root / "game/Mods" / PACKAGE_LEAF
        if location != target:
            replacements.append((location, target))
    tid = swap(root, replacements + [(staged_profile, root / "profile"), (staged_selection, root / "prepared.json")], "prepare-profile")
    run = {"schema": "rlo-drm-run.v1", "label": label, "mode": mode, "lane": lane, "purpose": purpose,
           "serializedTextureInput": serialized_texture, "demandTextureProbe": demand_texture,
           "benchmarkSelection": selection, "benchmarkSelectionSha256": identity(selection) if selection else None,
           "menuObserver": menu_observer, "menuObserverPackage": menu_receipt,
           "audioPreloaderPackage": bootstrap_receipt, "audioMeasurement": audio_measurement, "audioPatchProbe": audio_patch_probe, "audioPreparedProbe": audio_prepared_probe, "audioNativeEntryProbe": audio_native_entry_probe, "audioNativeEntryStopOnly": audio_native_entry_stop_only, "audioNativeEntryDomainProbe": audio_native_entry_domain_probe,
           "audioCacheFrom": audio_cache_from, "audioCacheSource": audio_reference,
           "exitAfterMenuReady": exit_after_menu_ready,
           "gagarinCache": gagarin_cache, "loadingProgress": loading_progress, "characterPresets": character_presets, "giddyTextures": giddy_textures, "activation": activation, "gameplaySmoke": gameplay_smoke, "residualProbe": residual_probe,
           "png": png, "pngSource": png_source, "pngCacheFrom": png_cache_from, "firstBuildImages": first_build_images, "rawPixelHelper": raw_pixel_helper,
           "preparedCacheFrom": prepared_cache_from, "preparedCacheSource": prepared_reference,
           "parsedXmlCacheFrom": parsed_xml_cache_from, "parsedXmlCacheSource": parsed_reference,
           "processedXmlCacheFrom": processed_xml_cache_from, "inheritanceCacheFrom": inheritance_cache_from,
           "languageCacheFrom": language_cache_from,
           "atlasCacheFrom": atlas_cache_from,
           "atlasCacheCorrupt": atlas_cache_corrupt, "atlasCacheCorruption": atlas_corruption,
           "atlasLifecycle": atlas_lifecycle,
           "atlasCacheSource": extra_xml["StaticAtlases"][2] if "StaticAtlases" in extra_xml else None,
           "languageObserver": language_observer, "languageLifecycle": language_lifecycle,
           "languageSourceProbe": language_source_probe, "textureStorageProbe": texture_storage_probe, "textureQualityProbe": texture_quality_probe, "textureSourceChange": texture_source_change, "audioStreamProbe": audio_stream_probe, "audioBootstrapProbe": audio_bootstrap_probe, "loadingUiProbe": loading_ui_probe, "loadingUiLayout": loading_ui_layout, "loadingXmlRequestFrom": loading_xml_request_from,
           "languageCacheSource": extra_xml["ParsedLanguage"][2] if "ParsedLanguage" in extra_xml else None,
           "extraXmlCacheSources": {k: v[2] for k, v in extra_xml.items() if k not in ("ParsedLanguage", "StaticAtlases")}, "processedXmlObserver": processed_xml_observer,
           "xmlSourceObserver": xml_source_observer,
           "nativeStackTraces": native_stack_traces, "backgroundHold": background_hold, "gameplaySaveFrom": gameplay_save_from, "gameplaySaveSource": gameplay_reference, "worldBackground": world_background, "c14Background": c14_background,
           "excludedMods": exclusions, "profileOverrides": overrides,
           "userSettings": settings,
           "language": language,
           "frozenEnabledCount": len(m["activeOrder"]),
           "selectedFrozenEnabledCount": len([pid for pid in active if pid not in (MENU_ID, PACKAGE_ID)]),
           "strategy": strategy,
           "observation": "none" if mode == "absent" else observation, "manifestSha256": state["manifestSha256"],
           "generation": m["generation"], "candidate": receipt, "repositoryHead": git("rev-parse", "HEAD"),
           "profile": str(root / "profile"), "executable": str(root / "game/RimWorldWin64.exe"),
           "arguments": arguments,
           "activeOrder": active, "profileBefore": inventory(root / "profile"),
           "profileTransaction": tid, "preflight": check,
           "foreignCacheFrom": foreign_cache_from,
           "restoredCacheKinds": (["gagarin-MissileGirl"] if foreign_cached is not None else []) + (["terrain-colors"] if terrain_cached is not None else []) + (["wake-up-PngCache"] if png_cached is not None else []) + (["wake-up-PreparedTextures"] if prepared_cached is not None else []) + (["wake-up-ParsedXml"] if parsed_cached is not None else []) + ["wake-up-" + k for k in extra_xml] + (["wake-up-PreparedAudio"] if audio_cached is not None else []),
           "runtimeMismatch": mode != "absent" and not check["deployedRuntimeMatches"], "status": "prepared-offline"}
    write(results / "run.json", run)
    return {"prepared": label, "mode": mode, "transaction": tid, "preflight": check,
            "command": f'python "{REPO / "scripts/fixture.py"}" launch --label {label} --authorize-live-launch'}


def restore_profile(root):
    m, gen, state = current(root)
    inv = gen / "inventories/profile.json"
    require(digest(inv) == m["profileInventorySha256"], "Seed inventory drift")
    verify_tree(gen / "seed", read(inv), True)
    staged = root / "staging" / ("profile-reset-" + uuid.uuid4().hex)
    shutil.copytree(gen / "seed", staged)
    tid = swap(root, [(staged, root / "profile"), (None, root / "prepared.json")], "restore-clean-profile")
    return {"restored": str(root / "profile"), "transaction": tid, "activeOrder": "exact source order; Wake-Up not enabled"}


def startup_impact_summary(path):
    """Read the existing Loading Progress report without changing instrumentation."""
    if not path.is_file():
        return {"status": "absent"}
    try:
        data = ET.parse(path).getroot().find("sessionData")
        require(data is not None, "Missing Startup Impact sessionData")
        def milliseconds(value):
            number = float(value)
            require(math.isfinite(number) and number >= 0, "Invalid Startup Impact duration")
            return number
        def metrics(name, source=data):
            node = source.find(name)
            if node is None:
                return {}
            keys, values = node.findall("keys/li"), node.findall("values/li")
            require(len(keys) == len(values), "Startup Impact metric keys/values differ")
            return {key.text: milliseconds(value.text) for key, value in zip(keys, values)}
        def mod_metrics(name):
            totals = {}
            for mod in data.findall("mods/li"):
                for key, value in metrics(name, mod).items():
                    category = key.partition("|")[0]
                    totals[category] = totals.get(category, 0) + value
            return totals
        def count(name):
            value = data.findtext(name)
            if not value:
                return None
            result = int(value)
            require(result >= 0, "Invalid Startup Impact count")
            return result
        return {"status": "recorded", "source": "profile/StartupImpactData.xml", "units": "milliseconds",
                "loadingTimeMs": milliseconds(data.findtext("loadingTime")),
                "baseGameMetricsMs": metrics("metrics"), "offThreadMetricsMs": metrics("offThreadMetrics"),
                "modMetricsMs": mod_metrics("metrics"), "modOffThreadMetricsMs": mod_metrics("offThreadMetrics"),
                "modsLoaded": count("modsLoaded"), "defsParsed": count("defsParsed"),
                "patchOperationsApplied": count("patchOperationsApplied"),
                "modAggregation": "Sum across mods by category before the optional | detail separator; separate owner-thread and worker-thread totals.",
                "boundaryCaveat": "Loading Progress constructor through AbstractFilesystem.ClearAllCache completion; excludes earlier process/Prepatcher startup, report writing, and menu dwell. Stage and worker timings are not additive wall-clock durations."}
    except (ET.ParseError, OSError, RuntimeError, TypeError, ValueError) as error:
        return {"status": "unreadable", "error": str(error)}


def restore_runtime_file(root, package_id, relative):
    """Restore one inventoried file from an exact frozen-byte source, never refresh."""
    manifest, generation, _ = current(root)
    package = next((p for p in manifest["packages"] if p["id"] == package_id), None)
    require(package is not None, "Unknown frozen package")
    row = next((r for r in read(generation / package["inventory"])
                if r["path"] == relative and r["kind"] == "file"), None)
    require(row is not None, "Path is not an inventoried package file")
    target = inside(root, Path(package["fixture"]) / relative)
    source = physical(Path(package["source"]) / relative)
    require(source.is_relative_to(physical(Path(package["source"]))), "Source escapes package")
    require(source.stat().st_size == row["bytes"] and digest(source) == row["sha256"],
            "Original source no longer has frozen bytes; refusing refresh")
    before = {"sha256": digest(target), "bytes": target.stat().st_size,
              "mtime_ns": target.stat().st_mtime_ns}
    staged = inside(root, root / "staging" / ("restore-file-" + uuid.uuid4().hex))
    shutil.copy2(source, staged)
    require(staged.stat().st_size == row["bytes"] and digest(staged) == row["sha256"],
            "Frozen source changed while copying")
    os.utime(staged, ns=(staged.stat().st_atime_ns, row["mtime_ns"]))
    transaction = swap(root, [(staged, target)], "restore-runtime-file")
    return {"restoredFile": str(target), "packageId": package_id, "before": before,
            "restored": row, "transaction": transaction,
            "preservation": "Prior live file preserved by fixture transaction; source read only."}


def restore_vehicle_update_timestamp(root):
    """Vehicle Framework rewrites unchanged update-log bytes during startup."""
    manifest, generation, _ = current(root)
    package = next((p for p in manifest["packages"] if p["leaf"] == "3014915404"), None)
    if package is None:
        return None
    relative = "Updates/UpdateLog.xml"
    row = next((r for r in read(generation / package["inventory"]) if r["path"] == relative), None)
    if row is None:
        return None
    path = inside(root, Path(package["fixture"]) / relative)
    current_stat = path.stat()
    if current_stat.st_mtime_ns == row["mtime_ns"]:
        return None
    require(current_stat.st_size == row["bytes"] and digest(path) == row["sha256"],
            "Vehicle update-log content changed; preserve for review")
    os.utime(path, ns=(current_stat.st_atime_ns, row["mtime_ns"]))
    return {"path": str(path), "sha256": row["sha256"], "contentUnchanged": True,
            "beforeMtimeNs": current_stat.st_mtime_ns, "restoredMtimeNs": row["mtime_ns"]}


def capture(root, label):
    folder = root / "results" / token(label)
    run = read(folder / "run.json")
    require(read(root / "prepared.json")["label"] == label, "Profile belongs to a different run")
    require("startedUtc" in run, "No recorded launch for this label")
    require("pid" in run and run["status"] == "running", "No recorded child process to capture")
    require(not (folder / "profile").exists(), "Capture already exists")
    scan(root / "profile")
    shutil.copytree(root / "profile", folder / "profile")
    player = folder / "profile/Player.log"
    if player.is_file():
        shutil.copy2(player, folder / "Player.log")
    timing_lines = []
    if player.is_file():
        timing_lines = [line for line in player.read_text(errors="replace").splitlines()
                        if re.search(r"(load|startup|elapsed|time).*\b(ms|seconds|secs)\b", line, re.I)]
    write(folder / "timing-lines.json", {"authority": "raw log excerpts only; not menu/semantic acceptance", "lines": timing_lines})
    run["capturedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    run["evidence"] = inventory(folder / "profile")
    run["startupImpactSummary"] = startup_impact_summary(folder / "profile/StartupImpactData.xml")
    run["runtimeMetadataRestoration"] = restore_vehicle_update_timestamp(root)
    run["status"] = "captured-awaiting-human-and-runtime-review"
    write(folder / "run.json", run)
    return {"capture": str(folder), "hasPlayerLog": player.is_file(), "liveAcceptance": False}


def verify_prepared(root, label):
    """Shared launch preflight, also callable offline without consuming the label."""
    label = token(label)
    require(read(root / "prepared.json")["label"] == label, "Prepare this label first")
    folder = root / "results" / label
    run = read(folder / "run.json")
    bootstrap_receipt = fixture_audio_bootstrap.admit(root, run.get("purpose", "performance"), run.get("audioBootstrapProbe"), types.SimpleNamespace(**globals()), run.get("audioMeasurement"))
    validate_audio_measurement(run, bootstrap_receipt)
    require(run.get("audioPreloaderPackage") == bootstrap_receipt, "Bootstrap deployment changed; prepare again")
    require(run["status"] == "prepared-offline", "Run already launched")
    audit(root)
    m, gen, state = current(root)
    receipt = read(root / "candidate.json") if (root / "candidate.json").is_file() else None
    require(run["manifestSha256"] == state["manifestSha256"] and run["candidate"] == receipt, "Run binding drift")
    if run.get("menuObserver"):
        require(run["menuObserverPackage"] == read(root / "menu-observer.json"), "Observer package changed; prepare again")
    require(not (run.get("menuObserverPackage") or {}).get("nativeDeviceProbe")
            or (run.get("purpose") == "functional" and run.get("exitAfterMenuReady")),
            "Experimental native device observer requires functional automatic exit")
    require(not run.get("exitAfterMenuReady") or (run.get("menuObserver")
            and run["menuObserverPackage"].get("supportsAutomaticExit")), "Automatic exit requires a supported observer")
    require(not run.get("xmlSourceObserver") or (run.get("purpose") == "functional"
            and run.get("menuObserver") and run.get("exitAfterMenuReady")),
            "XML source observer requires functional automatic normal exit with the menu observer")
    require(not run.get("backgroundHold") or (run.get("menuObserverPackage") or {}).get("supportsBackgroundHold"),
            "Prepared observer does not support background hold")
    require(not run.get("worldBackground") or (run.get("menuObserverPackage") or {}).get("supportsWorldBackground"),
            "Prepared observer does not support world background")
    require(run.get("gameplaySaveFrom") is None or not run.get("gameplaySmoke")
            or (run.get("menuObserverPackage") or {}).get("supportsGameplaySave"),
            "Prepared observer does not support captured gameplay saves")
    if run["mode"] == "absent":
        require(not (root / "game/Mods" / PACKAGE_LEAF).exists(), "Absent run has an installed optimizer")
    else:
        require(candidate_matches_snapshot(root, m, gen), "Prepared package does not support the frozen runtime; rebuild/deploy/prepare")
        require(candidate_location(root) == root / "game/Mods" / PACKAGE_LEAF, "Prepared optimizer is parked")
    verify_tree(root / "profile", run["profileBefore"], True)
    require(type(run.get("atlasCacheCorrupt", False)) is bool, "Atlas corruption flag drift")
    if run.get("atlasCacheCorrupt", False):
        require(run.get("purpose") == "functional" and run.get("atlasCacheFrom"), "Atlas corruption purpose/source drift")
        corruption = run.get("atlasCacheCorruption")
        require(isinstance(corruption, dict)
                and re.fullmatch(r"WakeUp/StaticAtlases/v1/groups/[a-f0-9]{32}\.group", corruption.get("path", "")),
                "Atlas corruption record path drift")
        target = inside(root, root / "profile" / corruption["path"])
        require(type(corruption.get("offset")) is int and 113 <= corruption["offset"] < target.stat().st_size
                and corruption.get("bytes") == target.stat().st_size
                and digest(target) == corruption.get("afterSha256"), "Atlas corruption payload drift")
        source_path = inside(root, root / "results" / token(run["atlasCacheFrom"]) / "run.json")
        require(digest(source_path) == (run.get("atlasCacheSource") or {}).get("runSha256"), "Atlas corruption source receipt drift")
        source_rows = [r for r in read(source_path).get("evidence", []) if r["path"] == corruption["path"]]
        require(len(source_rows) == 1 and source_rows[0]["kind"] == "file"
                and source_rows[0]["sha256"] == corruption.get("beforeSha256")
                and source_rows[0]["bytes"] == corruption["bytes"], "Atlas corruption source digest drift")
        with target.open("rb") as stream:
            header = stream.read(113)
            stored = int.from_bytes(header[76:80], "little", signed=True)
            require(len(header) == 113 and int.from_bytes(header[:4], "little") == 0x31425457
                    and stored > 0 and 113 + stored <= corruption["bytes"]
                    and corruption["offset"] == 113 + stored // 2
                    and header[4:68].decode("ascii") == corruption.get("entryKey")
                    and int.from_bytes(header[68:72], "little", signed=True) == corruption.get("blockOrdinal"),
                    "Atlas corruption block binding drift")
            stream.seek(corruption["offset"])
            value = stream.read(1)[0]
        require(type(corruption.get("beforeByte")) is int and 0 <= corruption["beforeByte"] <= 255
                and value == corruption.get("afterByte") == (corruption["beforeByte"] ^ 1), "Atlas corruption byte drift")
    else:
        require(run.get("atlasCacheCorruption") is None, "Unexpected atlas corruption record")
    require(order(root / "profile/Config/ModsConfig.xml") == run["activeOrder"], "Launch order drift")
    exclusions = excluded_mods(root, m, state)
    require(run.get("excludedMods", []) == exclusions, "Prepared excluded-mod selection drift")
    selection = run.get("benchmarkSelection")
    if selection is not None:
        benchmark_selection(m, state, selection, {p["id"] for p in exclusions})
        require(identity(selection) == run.get("benchmarkSelectionSha256"), "Benchmark selection identity drift")
    require(run["activeOrder"] == run_order(m, run["lane"], run["mode"], run.get("menuObserver", False),
            {p["id"] for p in exclusions}, selection), "Frozen launch order drift")
    verify_profile_overrides(root / "profile", run)
    if run.get("serializedTextureInput"):
        require(run.get("purpose") == "functional" and run.get("menuObserver") and run.get("exitAfterMenuReady") and run.get("benchmarkSelection"), "Serialized texture purpose/selection drift")
        fixture_serialized_texture.verify(root / "profile", run["serializedTextureInput"], types.SimpleNamespace(**globals()))
    if run.get("gameplaySaveFrom") is not None:
        _, reference = gameplay_save_source(root, run["gameplaySaveFrom"], run["lane"],
            m["generation"], state["manifestSha256"], run["activeOrder"], exclusions)
        require(reference == run.get("gameplaySaveSource"), "Prepared gameplay save source binding drift")
        require(digest(root / "profile/Saves/RloFixtureSmoke.rws") == reference["saveSha256"],
                "Prepared gameplay save content drift")
        if (run.get("c14Background") or "").startswith("autostart-"):
            require(digest(root / "profile/Saves/autostart.rws") == reference["saveSha256"], "Prepared autostart save drift")
    else:
        require(run.get("gameplaySaveSource") is None, "Unexpected gameplay save source binding")
    exe = inside(root, root / "game/RimWorldWin64.exe")
    observation = "none" if run["mode"] == "absent" else run.get("observation", "timing")
    strategy = run.get("strategy", "startup-searches")
    args = run_arguments(root, run["mode"], observation, strategy,
                         run.get("exitAfterMenuReady", False), run.get("gagarinCache", "off"), run.get("png", "off"), run.get("pngSource", "native"), run.get("loadingProgress", "off"), run.get("characterPresets", "off"), run.get("giddyTextures", "off"), run.get("activation", "fixture"), run.get("gameplaySmoke", False), run.get("residualProbe", False), run.get("backgroundHold", False), run.get("gameplaySaveFrom"), run.get("purpose", "performance"), run.get("userSettings"), run.get("worldBackground", False), run.get("nativeStackTraces", False), run.get("xmlSourceObserver", False), run.get("processedXmlObserver", False), run.get("c14Background"), run.get("firstBuildImages", False), run.get("rawPixelHelper", False))
    if run.get("c14Background"):
        require(run.get("menuObserver") and (run.get("menuObserverPackage") or {}).get("supportsC14Background") and run.get("loadingUiLayout"), "C14 observer/layout drift")
    if run.get("demandTextureProbe"):
        require(run.get("purpose") == "functional" and run.get("exitAfterMenuReady") and run.get("menuObserver"), "Demand prototype purpose drift")
        fixture_demand_texture.verify(root / "profile", run["demandTextureProbe"], types.SimpleNamespace(**globals()))
        args.extend(fixture_demand_texture.arguments(root, run["demandTextureProbe"]))
    if run.get("serializedTextureInput"):
        args.append("--fixture-c10-serialized-texture")
    if run.get("textureQualityProbe"):
        require(run.get("purpose") == "functional" and run.get("exitAfterMenuReady")
                and run.get("menuObserver") and run["mode"] == "candidate", "Texture quality probe purpose drift")
        require(run["textureQualityProbe"] != "inspection-scene" or (run.get("gameplaySmoke") and run.get("gameplaySaveFrom") and run.get("nativeStackTraces")),
                "Inspection scene copied-save/native-logging drift")
        args.append("--fixture-c08-quality=" + run["textureQualityProbe"])
    if run.get("textureStorageProbe"):
        require(run.get("purpose") == "functional" and run.get("exitAfterMenuReady"), "Texture storage probe purpose drift")
        args.append("--fixture-c05-texture-probe")
        if run.get("textureSourceChange"): args.append("--fixture-c05-source-change")
    if run.get("loadingUiProbe"):
        require(run.get("purpose") == "functional" and run.get("menuObserver") and run.get("exitAfterMenuReady")
                and run["mode"] == "candidate", "Loading UI proof purpose drift")
        args.append("--fixture-c13-loading-probe")
    if run.get("audioMeasurement"):
        args.append("--fixture-c10-audio-measurement=" + run["audioMeasurement"])
    if run.get("audioPatchProbe"):
        require(run["audioPatchProbe"] in ("public", "same-file") and run.get("purpose") == "functional" and run.get("menuObserver") and run.get("exitAfterMenuReady")
                and ((run["mode"] == "absent" and not run.get("audioBootstrapProbe") and not run.get("audioPreparedProbe"))
                    or (run["mode"] == "candidate" and run.get("audioBootstrapProbe") and run.get("audioNativeEntryProbe")
                        and (bootstrap_receipt or {}).get("nativeEntry")
                        and run.get("audioPreparedProbe") in ("formats-prepare", "formats-warm"))),
                "Public audio patch probe purpose or package binding drift")
        args.append("--fixture-c10-public-patch-probe=" + run["audioPatchProbe"])
    if run.get("audioStreamProbe"):
        require(run.get("purpose") == "functional" and run.get("menuObserver") and run.get("exitAfterMenuReady")
                and not run.get("gameplaySmoke") and not run.get("worldBackground") and not run.get("languageLifecycle"), "Audio stream probe purpose drift")
        require((run.get("menuObserverPackage") or {}).get("supportsAudioStreamProbe"), "Rebuild observer for audio stream probe")
        args.append("--fixture-c10-stream-probe")
    if run.get("audioBootstrapProbe"):
        require((run.get("purpose") == "functional" or run.get("audioMeasurement")) and run.get("menuObserver") and run.get("exitAfterMenuReady")
                and run["mode"] in ("baseline", "candidate") and (run.get("audioMeasurement") or not run.get("gameplaySmoke") or run.get("audioPreparedProbe") in ("warm", "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm")) and not run.get("worldBackground")
                and not run.get("languageLifecycle") and not run.get("audioStreamProbe") and not run.get("residualProbe")
                and not run.get("atlasLifecycle") and not run.get("textureStorageProbe") and not run.get("textureQualityProbe")
                and not run.get("languageSourceProbe"), "Audio bootstrap probe purpose drift")
        require((run.get("menuObserverPackage") or {}).get("supportsAudioBootstrapProbe"), "Rebuild observer for audio bootstrap probe")
        args.append("--fixture-c10-bootstrap-probe")
    require(bool(run.get("audioNativeEntryProbe")) == bool((bootstrap_receipt or {}).get("nativeEntry")), "Native entry probe package binding drift")
    require(not run.get("audioNativeEntryStopOnly") or (run.get("audioNativeEntryProbe") and not run.get("audioPreparedProbe")), "Native stop-only probe binding drift")
    require(not run.get("audioNativeEntryDomainProbe") or (run.get("audioNativeEntryProbe") and not run.get("audioPreparedProbe") and not run.get("audioNativeEntryStopOnly")), "Native domain probe binding drift")
    if run.get("audioNativeEntryProbe"):
        require((run.get("purpose") == "functional" or run.get("audioMeasurement")) and run.get("audioBootstrapProbe"), "Native entry purpose drift")
        require((run.get("menuObserverPackage") or {}).get("supportsAudioNativeEntryProbe"), "Rebuild observer for native entry probe")
        args.extend(["-monoProfiler", "wakeupentry", "--fixture-c10-native-entry-probe"])
    if run.get("audioNativeEntryStopOnly"):
        args.append("--fixture-c10-native-entry-stop-only")
    if run.get("audioNativeEntryDomainProbe"):
        args.append("--fixture-c10-native-entry-domain-probe")
    if run.get("audioPreparedProbe"):
        require(run.get("audioBootstrapProbe") and bootstrap_receipt is not None
                and run["audioPreparedProbe"] in ("completion", "prepare", "warm", "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm"), "Prepared audio probe binding drift")
        require(run["audioPreparedProbe"] not in ("natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm") or run.get("gameplaySmoke"), "Natural audio gameplay binding drift")
        args.append("--fixture-c10-prepared-audio=" + run["audioPreparedProbe"])
    if run.get("purpose") == "functional" and run.get("exitAfterMenuReady", False) and not run.get("audioMeasurement"):
        args.append("--fixture-functional-probes")
        if not run.get("atlasLifecycle") and ((run.get("userSettings") or {}).get("staticAtlases") or (run.get("userSettings") or {}).get("atlasBatching")):
            args.append("--fixture-atlas-probe")
    if run.get("atlasLifecycle"):
        require(run.get("purpose") == "functional" and run.get("menuObserver") and run.get("exitAfterMenuReady")
                and run["mode"] == "candidate" and (run.get("userSettings") or {}).get("atlasBatching") is True,
                "Atlas lifecycle purpose/settings drift")
        args.append("--fixture-atlas-lifecycle")
    if run.get("languageObserver"):
        require(run.get("menuObserver") and run.get("exitAfterMenuReady"), "Language observation selection drift")
        args.append("--fixture-language-observer")
    if run.get("languageLifecycle"):
        require(run.get("purpose") == "functional" and run.get("exitAfterMenuReady"), "Language lifecycle purpose drift")
        args.append("--fixture-language-lifecycle")
    if run.get("languageSourceProbe"):
        require(run.get("purpose") == "functional" and run.get("exitAfterMenuReady"), "Language source probe purpose drift")
        args.append("--fixture-language-source-probe")
    require(str(exe) == run["executable"] and args == run["arguments"], "Launch command drift")
    require(run["mode"] in ("absent", "baseline", "candidate") and run["lane"] in ("activation", "representative", "xml-expanded"), "Run mode/lane drift")
    require("startedUtc" not in run, "Launch already attempted; prepare a new label")
    return {"verifiedPrepared": label, "lane": run["lane"], "mode": run["mode"],
            "sourceRevision": receipt["sourceRevision"] if receipt else None, "gameLaunched": False,
            "command": [str(exe), *args, "-logFile", str(root / "profile/Player.log")]}


def preparation_job(root, path):
    """Bind a finite backend request to this prepared fixture, never normal data."""
    path = inside(REPO / "artifacts", path)
    require(path.is_file() and path.stat().st_size <= 1024 * 1024, "Preparation job must be an artifacts JSON file under 1 MiB")
    job = read(path)
    fields = {"gameRoot", "modsConfigPath", "saveDataRoot", "additionalRoots", "paths",
              "preset", "filter", "aniso", "mipBias", "action", "budgetMiB"}
    require(isinstance(job, dict) and fields <= job.keys()
            and job.keys() <= fields | {"cancelAfterCompleted", "previewOnly", "psdEnabled"}, "Preparation job fields differ from the functional contract")
    for key, target in (("gameRoot", root / "game"), ("modsConfigPath", root / "profile/Config/ModsConfig.xml"),
                        ("saveDataRoot", root / "profile")):
        require(isinstance(job[key], str) and Path(job[key]).is_absolute()
                and physical(job[key]) == physical(target), "Preparation job escapes fixture: " + key)
    require(isinstance(job["additionalRoots"], list) and len(job["additionalRoots"]) <= 1, "Only the fixture local Mods root may be added")
    for extra in job["additionalRoots"]:
        require(isinstance(extra, dict) and extra.keys() == {"path", "kind"} and extra["kind"] == "Local"
                and isinstance(extra["path"], str) and Path(extra["path"]).is_absolute()
                and physical(extra["path"]) == physical(root / "game/Mods"), "Additional preparation root escapes fixture Mods")
    require(job["action"] in ("prepare", "reset", "clear", "rebuild", "bypass"), "Unknown preparation action")
    for key, lower, upper in (("preset", 0, 3), ("filter", 0, 2), ("aniso", 0, 16), ("budgetMiB", 1, 65536)):
        require(type(job[key]) is int and lower <= job[key] <= upper, "Preparation option out of bounds: " + key)
    require(type(job["mipBias"]) in (int, float) and math.isfinite(job["mipBias"])
            and -3 <= job["mipBias"] <= 3, "Preparation mipBias out of bounds")
    require("cancelAfterCompleted" not in job or (type(job["cancelAfterCompleted"]) is int
            and 0 <= job["cancelAfterCompleted"] <= 512), "Preparation cancellation boundary out of bounds")
    require(all(key not in job or type(job[key]) is bool for key in ("previewOnly", "psdEnabled")),
            "Preparation preview/PSD flags must be booleans")
    require(isinstance(job["paths"], list) and len(job["paths"]) <= 128, "Preparation paths bound")
    for selected in job["paths"]:
        require(isinstance(selected, str) and selected == selected.strip() and len(selected) <= 1024,
                "Preparation selection must be a relative texture path")
        parts = selected.replace("\\", "/").rstrip("/").split("/")
        require(parts[0].lower() == "textures" and all(p and p not in (".", "..") for p in parts)
                and not any(c in selected for c in ':*?\x00'), "Preparation selection escapes Textures")
    return path, job


def preparation_owned_path(row):
    """Only preparation records and the shared budget's empty ownership locks."""
    name = row["path"]
    categories = "PngCache|PreparedTextures|PreparedAudio|StaticAtlases|ParsedXml|ProcessedXml|ResolvedInheritance|ParsedLanguage"
    if row["kind"] == "directory":
        return name in ("WakeUp", "WakeUp/PreparedTextures/v1/groups") or bool(re.fullmatch(
            r"WakeUp/(?:" + categories + r")(?:/v1)?", name))
    if name == "WakeUp/.budget-owner" or re.fullmatch(r"WakeUp/(?:" + categories + r")/v1/\.owner", name):
        return row.get("bytes") == 0
    return bool(re.fullmatch(r"WakeUp/PreparedTextures/(?:request\.xml|\.request\.pending-[a-f0-9]{32}|"
        r"v1/(?:usage|usage\.[a-f0-9]{32}\.pending|groups/(?:index|[a-f0-9]{32}\.(?:group|pending)|"
        r"[a-f0-9]{16}_[a-f0-9]{32}\.delta)))", name))


def external_prepare(root, label, app_path, job_path):
    """Run the packaged backend before launch and retain both success and failure evidence.

    main holds the fixture lock and applies the same fixture-process guard as prepare.
    A failed attempt blocks launch. An explicitly retried, unchanged safe
    cancellation can resume; other failures require a fresh prepare.
    """
    folder = root / "results" / token(label)
    run_path = folder / "run.json"
    run = read(run_path)
    require(run.get("purpose") == "functional" and run.get("mode") == "candidate"
            and run.get("activation") == "user" and "startedUtc" not in run,
            "External preparation requires a never-launched functional candidate with user activation")
    app = inside(REPO / "artifacts", app_path)
    require(app.is_file() and app.name == "WakeUp.Preparation.exe" and app.parent != physical(REPO / "artifacts"),
            "Provide a packaged WakeUp.Preparation.exe in an artifacts distribution directory")
    source_job, job = preparation_job(root, job_path)
    if run.get("status") == "external-preparation-failed":
        previous = (run.get("externalPreparations") or [{}])[-1]
        canceled = previous.get("result") or {}
        require(previous.get("exitStatus") == 3 and canceled.get("canceled") is True
                and canceled.get("success") is False and previous.get("forbiddenChanges") == [],
                "Only a safely captured cancellation may resume; otherwise prepare a fresh label")
        require(run.get("purpose") == "functional" and "startedUtc" not in run,
                "Canceled preparation was already launched")
        verify_tree(root / "profile", previous["profileAfter"], True)
        config_sha = digest(root / "profile/Config/ModsConfig.xml")
        require(str(canceled.get("modsConfigBefore", "")).lower() == config_sha
                and str(canceled.get("modsConfigAfter", "")).lower() == config_sha,
                "Canceled preparation config changed")
        for source in canceled.get("selected", []):
            source_path = inside(root / "game", Path(source["sourcePath"]))
            require(str(source.get("sourceBefore", "")).lower() == digest(source_path)
                    and str(source.get("sourceAfter", "")).lower() == digest(source_path),
                    "Canceled preparation source changed")
        original = read(run_path)
        run["profileBefore"] = previous["profileAfter"]
        run["status"] = "prepared-offline"
        write(run_path, run)
        try:
            verify_prepared(root, label)
        except Exception:
            write(run_path, original)
            raise
    verify_prepared(root, label)
    contract = runtime_contract(root)
    require(contract["target"] == "gog-rev573", "External preparation qualification requires the approved GOG fixture")
    app_files = inventory(app.parent)
    before = inventory(root / "profile")
    evidence = folder / ("external-preparation-" + uuid.uuid4().hex)
    evidence.mkdir()
    copy_stable(root / "profile", evidence / "profile-before", before)
    write(evidence / "job.json", job)
    result_path = evidence / "result.json"
    log_path = evidence / "backend.log"
    receipt = {"status": "attempted", "gameLaunched": False, "action": job["action"],
        "app": str(app), "distribution": app_files, "distributionSha256": identity(app_files),
        "sourceJob": str(source_job), "sourceJobSha256": digest(source_job),
        "job": str(evidence / "job.json"), "jobSha256": digest(evidence / "job.json"),
        "resultPath": str(result_path), "profileBefore": before, "profileSnapshot": str(evidence / "profile-before"),
        "exitStatus": None, "result": None}
    run.setdefault("externalPreparations", []).append(receipt)
    run["status"] = "external-preparation-attempted"
    write(run_path, run)
    error = None
    try:
        options = {}
        if os.name == "nt":
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 0
            options = {"startupinfo": startup, "creationflags": subprocess.CREATE_NO_WINDOW}
        command = [str(app), "--functional-job", str(evidence / "job.json"), "--result", str(result_path)]
        receipt["command"] = command
        with log_path.open("xb") as log:
            completed = subprocess.run(command, cwd=app.parent, stdin=subprocess.DEVNULL,
                                       stdout=log, stderr=subprocess.STDOUT, check=False, **options)
        receipt["exitStatus"] = completed.returncode
    except Exception as exc:
        error = str(exc)
    try:
        after = inventory(root / "profile")
        receipt["profileAfter"] = after
        # Compare all profile bytes, including other Wake-Up features and unknown files.
        old = {r["path"]: r for r in before}
        new = {r["path"]: r for r in after}
        changed = sorted(p for p in old.keys() | new.keys() if old.get(p) != new.get(p))
        receipt["changedPaths"] = changed
        forbidden = [p for p in changed if not all(preparation_owned_path(r) for r in (old.get(p), new.get(p)) if r)]
        receipt["forbiddenChanges"] = forbidden
        if log_path.is_file(): receipt["logSha256"] = digest(log_path)
        if result_path.is_file():
            receipt["resultSha256"] = digest(result_path)
            receipt["result"] = read(result_path)
        require(not forbidden, "External preparation changed unowned profile paths: " + ", ".join(forbidden))
        verify_tree(app.parent, app_files, True)
        require(digest(source_job) == receipt["sourceJobSha256"] and digest(evidence / "job.json") == receipt["jobSha256"],
                "Preparation job changed during execution")
        result = receipt["result"]
        require(not error and receipt["exitStatus"] == 0 and isinstance(result, dict) and result.get("success") is True,
                error or "Preparation backend failed; see captured result and log")
        require(result.get("action") == job["action"] and str(result.get("gameAssemblySha256", "")).lower() == contract["gameAssemblySha256"],
                "Preparation result runtime/action binding drift")
        config_sha = digest(root / "profile/Config/ModsConfig.xml")
        require(str(result.get("modsConfigBefore", "")).lower() == config_sha
                and str(result.get("modsConfigAfter", "")).lower() == config_sha, "Preparation result ModsConfig binding drift")
        require(isinstance(result.get("selected"), list), "Preparation result source receipts missing")
        for source in result["selected"]:
            path = inside(root / "game", Path(source["sourcePath"]))
            require(str(source.get("sourceBefore", "")).lower() == digest(path)
                    and str(source.get("sourceAfter", "")).lower() == digest(path), "Preparation source content drift")
        run["profileBefore"] = after
        receipt["status"] = "completed"
        run["status"] = "prepared-offline"
        write(run_path, run)
        verified = verify_prepared(root, label)
    except Exception as exc:
        error = str(exc)
    if error:
        receipt["status"] = "failed"
        receipt["error"] = error
        run["status"] = "external-preparation-failed"
        write(run_path, run)
        raise RuntimeError("External preparation failed; evidence retained at " + str(evidence) + ": " + error)
    return {"externalPrepared": label, "gameLaunched": False, "evidence": str(evidence), "result": receipt["result"],
            "verifiedPrepared": verified["verifiedPrepared"]}


class StartupLogObserver:
    """Bounded incremental external timing, identical with and without Wake-Up."""
    def __init__(self, path, start):
        self.path, self.start = path, start
        self.offset = 0
        self.partial = b""
        self.unload = None
        self.endpoint = None

    def sample(self):
        if self.endpoint is not None or not self.path.is_file():
            return
        try:
            with self.path.open("rb") as stream:
                if stream.seek(0, 2) < self.offset:
                    self.offset, self.partial, self.unload = 0, b"", None
                stream.seek(self.offset)
                data = stream.read(65536)
                self.offset = stream.tell()
        except OSError:
            return  # A temporarily busy/unavailable log must not affect the game.
        lines = (self.partial + data).split(b"\n")
        self.partial = lines.pop()[-16384:]
        for raw in lines:
            line = raw.decode("utf-8", errors="replace").strip()
            if re.search(r"Unloading .*unused Assets", line):
                self.unload = line[:2000]
            elif self.unload and re.search(r"Total: .*ms \(FindLiveObjects", line):
                self.endpoint = {"kind": "observed-startup-cleanup-endpoint",
                    "elapsedSeconds": round(time.perf_counter() - self.start, 4),
                    "excerpt": [self.unload, line[:2000]], "pollIntervalSeconds": 0.05,
                    "caveat": "External observation of first complete Unity unused-assets cleanup block; log buffering and polling add delay. Confirm usable menu separately; not exact menu timing."}
                break


def launch(root, label, authorized, window_style="normal"):
    require(authorized, "Live launch requires explicit --authorize-live-launch; preparation is offline")
    verified = verify_prepared(root, label)
    folder = root / "results" / label
    run = read(folder / "run.json")
    require(window_style in ("normal", "minimized-noactivate", "noactivate"), "Unknown launch window style")
    require(window_style == "normal" or run.get("purpose") in ("functional", "performance"),
            "Nonactivating launch requires an explicitly recorded run purpose")
    if run.get("audioMeasurement") == "warm":
        source_run = read(root / "results" / token(run["audioCacheFrom"]) / "run.json")
        require(source_run.get("launchWindowStyle") == window_style, "Audio measurement window style differs from its cold source")
    launch_options = {}
    if window_style != "normal":
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        # SW_SHOWMINNOACTIVE / SW_SHOWNOACTIVATE: never restore or foreground later.
        startup.wShowWindow = 7 if window_style == "minimized-noactivate" else 4
        launch_options["startupinfo"] = startup
    run["launchWindowStyle"] = window_style
    start = time.perf_counter()
    run["startedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    run["status"] = "launch-attempted"
    if run.get("menuObserver"):
        start_menu_clock(root, run)
    write(folder / "run.json", run)
    try:
        process = subprocess.Popen(verified["command"], cwd=Path(run["executable"]).parent, **launch_options)
    except OSError as error:
        run["status"] = "launch-failed"
        run["launchError"] = str(error)
        write(folder / "run.json", run)
        raise
    run["pid"] = process.pid
    run["actualCommand"] = verified["command"]
    run["status"] = "running"
    write(folder / "run.json", run)
    automatic = run.get("exitAfterMenuReady", False)
    exit_message = "Waiting for normal automatic exit." if automatic else "Waiting for user to close the game."
    manual_message = ("Perform the authorized manual functional check; exit through Quit to OS."
                      if run.get("purpose") == "functional" else "Observe usable menu; load no save; exit through Quit to OS.")
    print("DRM-free process started. " + ("Waiting for in-game menu-ready signal. " + exit_message
          if run.get("menuObserver") else manual_message), flush=True)
    observer = StartupLogObserver(root / "profile/Player.log", start)
    while process.poll() is None:
        observer.sample()
        if run.get("menuObserver") and "menuReadyObservation" not in run:
            signal = menu_ready_signal(root, run)
            if signal is not None:
                run["menuReadyObservation"] = signal
                write(folder / "run.json", run)
                print(f'MENU READY at {signal["elapsedSeconds"]:.3f} seconds (in-game repaint). {exit_message}', flush=True)
        if automatic:
            elapsed = time.perf_counter() - start
            signal = run.get("menuReadyObservation")
            exit_budget = 600 if run.get("textureQualityProbe") else 1200 if run.get("languageLifecycle") else 600 if run.get("gameplaySmoke") or run.get("worldBackground") else 60
            failure = (f"Normal shutdown did not finish within {exit_budget} seconds of menu-ready"
                       if signal and elapsed - signal["elapsedSeconds"] > exit_budget else
                       "No menu-ready signal within 30 minutes" if not signal and elapsed > 1800 else None)
            if failure:
                run["automationFailure"] = failure
                write(folder / "run.json", run)
                raise RuntimeError(failure + "; game left running, no further launch allowed until it closes")
        if observer.endpoint is not None and "startupCleanupObservation" not in run:
            run["startupCleanupObservation"] = observer.endpoint
            write(folder / "run.json", run)
            print("Startup cleanup observed at " + str(observer.endpoint["elapsedSeconds"]) + " seconds; verify menu.", flush=True)
        time.sleep(0.05)
    observer.sample()
    if run.get("menuObserver") and "menuReadyObservation" not in run:
        run["menuReadyObservation"] = menu_ready_signal(root, run)
    run["startupCleanupObservation"] = observer.endpoint
    run["exitCode"] = process.wait()
    run["processLifetimeSeconds"] = round(time.perf_counter() - start, 3)
    run["timingCaveat"] = "Loading ends at menuReadyObservation. Process lifetime also includes menu dwell and shutdown; external cleanup is an earlier separate endpoint."
    if automatic:
        run["automaticTestPassed"] = run.get("menuReadyObservation") is not None and run["exitCode"] == 0
    write(folder / "run.json", run)
    if run.get("gameplaySmoke"):
        smoke = root / "profile/FixtureMenuObserver/gameplay-smoke.json"
        run["gameplayTestPassed"] = smoke.is_file() and read(smoke).get("passed") is True
        write(folder / "run.json", run)
    if run.get("audioMeasurement"):
        path = root / "profile/FixtureMenuObserver/c10-audio-measurement.json"
        measurement = read(path) if path.is_file() else {}
        run["audioMeasurementPassed"] = bool(measurement.get("schema") == "c10-audio-measurement.v1"
            and measurement.get("mode") == run["audioMeasurement"] and measurement.get("completed") is True
            and measurement.get("failure") == "" and measurement.get("bootstrapPresencePassed") is True)
        write(folder / "run.json", run)
    if run.get("audioPatchProbe"):
        path = root / "profile/FixtureMenuObserver/c10-public-patch-probe.json"
        probe = read(path) if path.is_file() else {}
        run["audioPublicPatchTestPassed"] = bool(probe.get("schema") == "c10-public-patch-functional.v1"
            and probe.get("functionalPassed") is True and probe.get("mode") == run["audioPatchProbe"])
        write(folder / "run.json", run)
    if run.get("audioStreamProbe"):
        path = root / "profile/FixtureMenuObserver/c10-stream-probe.json"
        probe = read(path) if path.is_file() else {}
        run["audioStreamProbeTestPassed"] = bool(probe.get("schema") == "c10-stream-causal.v1"
            and probe.get("nativeComparisonPassed") is True and probe.get("failure") == ""
            and len(probe.get("cases", [])) == 2
            and all(c.get("readerDisposed") and c.get("streamDisposed") for c in probe["cases"]))
        write(folder / "run.json", run)
    if run.get("audioBootstrapProbe"):
        path = root / "profile/FixtureMenuObserver/c10-bootstrap-probe.json"
        probe = read(path) if path.is_file() else {}
        run["audioBootstrapObservationPassed"] = bool(probe.get("schema") == "c10-bootstrap-observation.v1"
            and probe.get("observationPassed") is True and probe.get("failure") == ""
            and probe.get("featureAcceptance") is False and probe.get("finalGuardsVerified") is True)
        if run.get("audioPreloaderPackage"):
            run["audioPreloaderObservationPassed"] = bool(probe.get("preloaderObservationPassed") is True
                and probe.get("session") == run["audioPreloaderPackage"]["session"])
        write(folder / "run.json", run)
    if run.get("audioNativeEntryProbe"):
        path = root / "profile/FixtureMenuObserver/c10-native-entry-native.json"
        native = read(path) if path.is_file() else {}
        run["audioNativeEntryShutdownPassed"] = bool(native.get("schema") == "c10-native-entry-shutdown.v1"
            and native.get("stage") == "shutdown-end" and native.get("initReady") == 1 and native.get("bound") == 1
            and native.get("accepting") == 0 and native.get("inFlight") == 0
            and native.get("stopCompleted") == 1 and native.get("shutdownSeen") == 1 and native.get("shutdownInflight") == 0
            and native.get("bridgeCalls", 0) > 0 and native.get("filterUpdate", 0) > 0
            and all(native.get(k) == 0 for k in ("initFailure", "bindFailure", "receiptFailure", "stopRejected", "recoveryFailures",
                "lifecycleFailures", "contextFailures", "lifecycleInFlightAtReturn", "domainUnloadWithoutStop"))
            and native.get("managedProcessExitEntries", 0) > 0
            and isinstance(native.get("boundDomainId"), int) and isinstance(native.get("rootDomainId"), int)
            and native.get("boundIsRoot") == int(native.get("boundDomainId") == native.get("rootDomainId"))
            and native.get("crossDomainEntries") == native.get("contextRestores")
            and os.path.normcase(native.get("runtimeModule", "")) == os.path.normcase(str(root / "game/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll")))
        if not run.get("audioPreparedProbe") and not run.get("audioMeasurement"):
            path = root / "profile/FixtureMenuObserver/c10-native-entry-probe.json"
            probe = read(path) if path.is_file() else {}
            run["audioNativeEntryTestPassed"] = bool(probe.get("schema") == "c10-native-entry-functional.v1"
                and probe.get("functionalPassed") is True and probe.get("failure") == ""
                and probe.get("probeMode") == ("stop-only" if run.get("audioNativeEntryStopOnly") else "domain" if run.get("audioNativeEntryDomainProbe") else "boundary"))
        write(folder / "run.json", run)
    if run.get("audioPreparedProbe"):
        path = root / "profile/FixtureMenuObserver/c10-prepared-audio-probe.json"
        probe = read(path) if path.is_file() else {}
        natural = run["audioPreparedProbe"] in ("natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm")
        run["audioPreparedTestPassed"] = bool(probe.get("schema") == ("c10-mp3-completion.v1" if run["audioPreparedProbe"] == "completion" else "c10-natural-audio-functional.v1" if natural else "c10-prepared-audio-functional.v1")
            and probe.get("functionalPassed") is True and probe.get("failure") == ""
            and probe.get("mode") == run["audioPreparedProbe"]
            and (run["audioPreparedProbe"] not in ("wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm") or probe.get("wavContentPlaybackPassed") is True)
            and (run["audioPreparedProbe"] not in ("formats-prepare", "formats-warm", "frames-prepare", "frames-warm") or probe.get("nativeFormatsPassed") is True)
            and (run["audioPreparedProbe"] not in ("readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm") or probe.get("audioReadinessPassed") is True)
            and (run["audioPreparedProbe"] not in ("advance-prepare", "advance-warm") or probe.get("audioAdvancePassed") is True)
            and ((natural and probe.get("naturalContentLoadingPassed") is True
                and probe.get("sustainedNativePlaybackPassed") is True and probe.get("gameplaySaveReloadPassed") is True)
                or (not natural and (not run.get("gameplaySmoke") or (probe.get("gameplayRuntimeCleanupPassed") is True
                and probe.get("gameplaySaveReloadPassed") is True and probe.get("workerDemandWithoutMainProgress") is True
                and probe.get("naturalGameplayConsumers", {}).get("passed") is True)))))
        write(folder / "run.json", run)
    if run.get("worldBackground"):
        world = root / "profile/FixtureMenuObserver/world-background.json"
        run["worldBackgroundTestPassed"] = world.is_file() and read(world).get("passed") is True
        write(folder / "run.json", run)
    if run.get("atlasLifecycle"):
        events = root / "profile/FixtureMenuObserver/atlas-lifecycle-qualification.jsonl"
        rows = [json.loads(line) for line in events.read_text(encoding="utf-8").splitlines()] if events.is_file() else []
        completed = [row for row in rows if row.get("event") == "complete"]
        run["atlasLifecycleTestPassed"] = bool(len(completed) == 1 and completed[0].get("pass") is True
            and completed[0].get("errors") == 0 and not any(row.get("event") == "observer-error" for row in rows))
        write(folder / "run.json", run)
    if run.get("languageLifecycle"):
        events = root / "profile/FixtureMenuObserver/language-lifecycle/events.jsonl"
        rows = [json.loads(line) for line in events.read_text(encoding="utf-8").splitlines()] if events.is_file() else []
        run["languageLifecycleTestPassed"] = bool(rows and rows[-1].get("phase") == "complete" and rows[-1].get("passed") is True)
        write(folder / "run.json", run)
    if run.get("languageSourceProbe"):
        events = root / "profile/FixtureMenuObserver/language-source-probe/events.jsonl"
        rows = [json.loads(line) for line in events.read_text(encoding="utf-8").splitlines()] if events.is_file() else []
        run["languageSourceProbeTestPassed"] = bool(rows and rows[-1].get("phase") == "complete" and rows[-1].get("passed") is True)
        write(folder / "run.json", run)
    if run.get("demandTextureProbe"):
        report = root / "profile/FixtureMenuObserver/c10-demand-texture-probe.json"
        value = read(report) if report.is_file() else {}
        run["demandTextureProbePassed"] = value.get("completed") is True and value.get("functionalPassed") is True
        write(folder / "run.json", run)
    if run.get("serializedTextureInput"):
        report = root / "profile/FixtureMenuObserver/c10-serialized-texture.json"
        value = read(report) if report.is_file() else {}
        run["serializedTextureProbePassed"] = value.get("completed") is True and value.get("functionalPassed") is True
        write(folder / "run.json", run)
    if run.get("textureStorageProbe"):
        events = root / "profile/FixtureMenuObserver/c05-texture-probe.jsonl"
        rows = [json.loads(line) for line in events.read_text(encoding="utf-8").splitlines()] if events.is_file() else []
        complete = rows[-1] if rows else {}
        run["textureStorageProbeTestPassed"] = bool(complete.get("event") == "complete"
            and complete.get("failedSamples") == 0 and complete.get("passedSamples", 0) > 0)
        run["textureStorageAllRequestedSamplesPassed"] = complete.get("allRequestedSamplesPassed") is True
        write(folder / "run.json", run)
    if run.get("textureQualityProbe"):
        probe_name = "c08-unity-exposure-probe.jsonl" if run["textureQualityProbe"] == "unity-exposure" else "c08-boundary-probe.jsonl" if run["textureQualityProbe"] == "boundaries" else (
            "c08-coverage-probe.jsonl" if run["textureQualityProbe"].startswith("coverage-") else "c08-quality-probe.jsonl")
        events = root / "profile/FixtureMenuObserver" / probe_name
        rows = [json.loads(line) for line in events.read_text(encoding="utf-8").splitlines()] if events.is_file() else []
        complete = [row for row in rows if row.get("event") == "complete"]
        run["textureQualityProbeTestPassed"] = bool(len(complete) == 1 and complete[0].get("passed") is True)
        if run["textureQualityProbe"] == "boundaries":
            format_events = root / "profile/FixtureMenuObserver/c08-format-probe.jsonl"
            formats = [json.loads(line) for line in format_events.read_text(encoding="utf-8").splitlines()] if format_events.is_file() else []
            format_complete = [row for row in formats if row.get("event") == "complete"]
            run["textureFormatProbeTestPassed"] = bool(len(format_complete) == 1 and format_complete[0].get("passed") is True)
            run["textureQualityProbeTestPassed"] &= run["textureFormatProbeTestPassed"]
        write(folder / "run.json", run)
    if run.get("c14Background"):
        report = root / "profile/FixtureMenuObserver/c14-background.json"
        probe = read(report) if report.is_file() else {}
        run["c14BackgroundPassed"] = probe.get("passed") is True and probe.get("phase") == run["c14Background"]
        run["automaticTestPassed"] = run.get("automaticTestPassed") is True and run["c14BackgroundPassed"]
        write(folder / "run.json", run)
    result = capture(root, label)
    if run.get("c14Background"):
        require(run.get("c14BackgroundPassed"), "C14 loading checks failed; evidence captured")
    if run.get("textureQualityProbe"):
        require(run.get("textureQualityProbeTestPassed"), "Texture quality checks failed; evidence captured")
    if run.get("gameplaySmoke"):
        require(run.get("gameplayTestPassed"), "Gameplay smoke failed; evidence captured")
    if run.get("worldBackground"):
        require(run.get("worldBackgroundTestPassed"), "World background failed; evidence captured")
    if run.get("atlasLifecycle"):
        require(run.get("atlasLifecycleTestPassed"), "Atlas lifecycle failed; evidence captured")
    if run.get("languageLifecycle"):
        require(run.get("languageLifecycleTestPassed"), "Language lifecycle failed; evidence captured")
    if run.get("languageSourceProbe"):
        require(run.get("languageSourceProbeTestPassed"), "Language source probe failed; evidence captured")
    if run.get("demandTextureProbe"):
        require(run.get("demandTextureProbePassed"), "Demand texture probe failed; evidence captured")
    if run.get("serializedTextureInput"):
        require(run.get("serializedTextureProbePassed"), "Serialized texture probe failed; evidence captured")
    if run.get("textureStorageProbe"):
        require(run.get("textureStorageProbeTestPassed"), "Texture storage probe failed; evidence captured")
    if automatic:
        require(run["automaticTestPassed"],
                "Automatic test failed: missing menu-ready signal or unsuccessful process exit; evidence captured")
        result.update(automaticTestPassed=True, menuReadyElapsedSeconds=run["menuReadyObservation"]["elapsedSeconds"])
    if run.get("audioMeasurement"):
        require(run.get("audioMeasurementPassed"), "Audio measurement workload failed; evidence captured")
    if run.get("audioPatchProbe"):
        require(run.get("audioPublicPatchTestPassed"), "Public audio patch functional probe failed; evidence captured")
    if run.get("audioStreamProbe"):
        require(run.get("audioStreamProbeTestPassed"), "Audio stream probe failed; evidence captured")
    if run.get("audioBootstrapProbe"):
        require(run.get("audioBootstrapObservationPassed"), "Audio bootstrap observation failed; evidence captured")
        if run.get("audioPreloaderPackage"):
            require(run.get("audioPreloaderObservationPassed"), "Early preloader qualification failed; evidence captured")
    if run.get("audioPreparedProbe"):
        require(run.get("audioPreparedTestPassed"), "Prepared audio functional probe failed; evidence captured")
    if run.get("audioNativeEntryProbe"):
        require(run.get("audioNativeEntryShutdownPassed"), "Native entry shutdown qualification failed; evidence captured")
        if not run.get("audioPreparedProbe") and not run.get("audioMeasurement"):
            require(run.get("audioNativeEntryTestPassed"), "Native entry functional qualification failed; evidence captured")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["stage-gog", "stage-steam", "bind-comparison-package", "discover", "initialize", "refresh", "audit", "full-audit", "test", "build", "deploy", "deploy-helper", "build-menu-observer", "deploy-menu-observer", "build-audio-bootstrap", "deploy-audio-bootstrap", "prepare", "external-prepare", "verify-prepared", "restore-profile", "restore-runtime-file", "exclude-mod", "restore-mod", "launch", "capture", "recover", "rollback"])
    parser.add_argument("--steam-source", type=Path)
    parser.add_argument("--native-stack-traces", action="store_true", help="Functional scene probe without the private GOG logging workaround")
    parser.add_argument("--test-filter", help="Optional focused managed checks; always retain fixture safety tests")
    parser.add_argument("--package-id", help="Exact frozen package ID for reversible exclusion")
    parser.add_argument("--relative-path", help="Exact inventoried file for restore-runtime-file")
    parser.add_argument("--gagarin-cache", choices=["off", "timing", "on", "verify"], default="off")
    parser.add_argument("--png", choices=["off", "control", "cache", "verify-cache", "first-build", "verify-first-build"], default="off")
    parser.add_argument("--raw-pixel-helper", action="store_true", help="Isolated GOG trial only: deploy/select reusable raw-pixel helper; never a user setting")
    parser.add_argument("--first-build-images", action="store_true", help="Explicit fixture PNG mode only: also select the existing C06 first-build decoder")
    parser.add_argument("--png-source", choices=["native", "original"], default="native", help="Original is a qualification-only PNG sibling selection, not a production optimization")
    parser.add_argument("--gameplay-smoke", action="store_true", help="Fixture-only new colony, ticking and save/reload qualification; not a speed arm")
    parser.add_argument("--background-hold", action="store_true", help="Functional user gameplay smoke: hold real background=false across minimized completion")
    parser.add_argument("--c14-background", help="Functional native loading phase; muted nonactivating qualification")
    parser.add_argument("--world-background", action="store_true", help="Functional native world page with real external minimize/restore and current preference checks")
    parser.add_argument("--gameplay-save-from", help="Restore only RloFixtureSmoke.rws from a successful captured matching fixture gameplay run for functional automatic smoke or manual loading")
    parser.add_argument("--residual-probe", action="store_true", help="Qualification-only first-menu/late-type cost observation")
    parser.add_argument("--activation", choices=["fixture", "user"], default="fixture", help="User exercises normal activation without Wake-Up command-line selectors")
    parser.add_argument("--user-settings", type=Path, help="Prepare only: allowlisted ordinary settings JSON for functional user activation")
    parser.add_argument("--preparation-app", type=Path, help="External-prepare only: packaged WakeUp.Preparation.exe under artifacts")
    parser.add_argument("--preparation-job", type=Path, help="External-prepare only: fixture-bound functional job JSON under artifacts")
    parser.add_argument("--language", choices=["English", "French (Français)", "ChineseSimplified (简体中文)"], help="Prepare only: functional qualification language in the isolated profile")
    parser.add_argument("--giddy-textures", choices=["off", "on", "timing", "verify"], default="off")
    parser.add_argument("--character-presets", choices=["off", "on", "timing"], default="off")
    parser.add_argument("--loading-progress", choices=["off", "coalesce"], default="off")
    parser.add_argument("--png-cache-from", help="Restore only the captured matching processed PNG cache")
    parser.add_argument("--prepared-cache-from", help="Restore the matching owned texture store for user preparation/caching or explicit fixture PNG cache modes")
    parser.add_argument("--parsed-xml-cache-from", help="Restore only the captured matching owned parsed XML v1 store")
    parser.add_argument("--processed-xml-cache-from", help="Restore captured matching owned processed XML")
    parser.add_argument("--inheritance-cache-from", help="Restore captured matching owned resolved inheritance")
    parser.add_argument("--language-cache-from", help="Restore captured owned parsed language records; functional runs may change selected language")
    parser.add_argument("--atlas-cache-from", help="Restore only captured matching complete static atlas groups; functional disabled/maintenance cases are allowed")
    parser.add_argument("--atlas-lifecycle", action="store_true", help="Functional only: late startup hook, synchronous drain and callback ordering probe")
    parser.add_argument("--atlas-cache-corrupt", action="store_true", help="Functional only: flip one payload byte in the private staged restored atlas store")
    parser.add_argument("--texture-source-change", action="store_true", help="Functional probe only: change one private PSD source with equal size and timestamp")
    parser.add_argument("--texture-quality-probe", choices=("catalog", "prepare", "verify", "reset", "verify-native", "boundaries", "unity-exposure", "coverage-catalog", "coverage-prepare", "coverage-verify", "coverage-reset", "coverage-native", "inspection-scene"), help="Functional only: C08 quality checks or copied-save display preparation")
    parser.add_argument("--observer-artwork", type=Path, help="Build-menu-observer only: bounded private texture-copy specification under artifacts")
    parser.add_argument("--native-texture-probe", action="store_true", help="Build-menu-observer only: experimental D3D11 device-only functional DLL")
    parser.add_argument("--demand-texture-probe", choices=["on", "off"], help="Functional only: experimental private texture/graphic/icon demand path")
    parser.add_argument("--demand-texture-selection", type=Path, help="Private natural selection XML for off-mode comparison")
    parser.add_argument("--serialized-texture-input", type=Path, help="Functional only: bounded private C10 serialized texture input directory")
    parser.add_argument("--texture-storage-probe", action="store_true", help="Functional only: native texture preservation and grouped storage checks")
    parser.add_argument("--audio-measurement", choices=["absent", "off", "cold", "warm"], help="Explicit matched natural audio workload without diagnostic controls")
    parser.add_argument("--audio-patch-probe", choices=["public", "same-file"], help="Functional only: public audio patch compatibility in absent or native-bootstrap formats candidate")
    parser.add_argument("--audio-stream-probe", action="store_true", help="Functional only: silent native streaming reader and callback causal check")
    parser.add_argument("--loading-ui-layout", choices=["1280x720@1", "1920x1080@1.5"], help="Functional private framebuffer size and UI scale; mutes fixture")
    parser.add_argument("--loading-xml-request-from", help="Restore captured explicit next-launch XML request from a successful UI handler run")
    parser.add_argument("--loading-ui-probe", action="store_true", help="Functional only: in-fixture rendered loading/report evidence")
    parser.add_argument("--audio-prepared-probe", choices=["completion", "prepare", "warm", "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm", "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm"], help="Functional first-build/warm prepared audio qualification")
    parser.add_argument("--audio-cache-from", help="Restore captured prepared audio for the warm functional probe")
    parser.add_argument("--audio-native-entry-probe", action="store_true", help="Functional only: build/select the early Mono entry adapter, replacing the process default ETW profiler")
    parser.add_argument("--audio-native-entry-stop-only", action="store_true", help="Functional only: independently challenge native held-entry explicit stop")
    parser.add_argument("--audio-native-entry-domain-probe", action="store_true", help="Functional only: secondary-domain context and pre-unload settlement")
    parser.add_argument("--audio-bootstrap-probe", action="store_true", help="Functional only: observe existing assembly identities and guarded bootstrap; no codec loading or feature acceptance")
    parser.add_argument("--language-source-probe", action="store_true", help="Functional only: private native source update, failure and callback checks")
    parser.add_argument("--language-observer", action="store_true", help="Complete native language output after menu, without detailed timing hooks")
    parser.add_argument("--language-lifecycle", action="store_true", help="Functional only: native language switch/reload and return before normal exit")
    parser.add_argument("--processed-xml-observer", action="store_true", help="Functional automatic downstream XML comparison")
    parser.add_argument("--xml-source-observer", action="store_true", help="Functional automatic run only: private retained-source XML observer")
    parser.add_argument("--package", type=Path)
    parser.add_argument("--mode", choices=["absent", "baseline", "candidate"], default="baseline")
    parser.add_argument("--purpose", choices=["functional", "performance"], default="performance",
                        help="Prepare only: functional checks may coexist with a verified normal game; their timings are not performance evidence")
    parser.add_argument("--observation", choices=["timing"], default="timing")
    parser.add_argument("--strategy", choices=["def-lookup", "type-lookup", "startup-searches"], default="startup-searches")
    parser.add_argument("--lane", choices=["representative", "activation", "xml-expanded"], default="representative")
    parser.add_argument("--selection", type=Path, help="Frozen benchmark selection JSON; representative lane only")
    parser.add_argument("--label")
    parser.add_argument("--foreign-cache-from", help="Restore only captured Gagarin MissileGirl cache; other caches stay reset")
    parser.add_argument("--menu-observer", action=argparse.BooleanOptionalAction, default=True,
                        help="Timestamp the first completed main-menu repaint (default: enabled)")
    parser.add_argument("--exit-after-menu-ready", action=argparse.BooleanOptionalAction, default=True,
                        help="Quit normally after recording menu-ready (default: enabled); --no-exit-after-menu-ready leaves the game open")
    parser.add_argument("--transaction")
    parser.add_argument("--resume", help="Resume this tool's unpublished staging generation; re-copy only changed packages")
    parser.add_argument("--authorize-live-launch", action="store_true")
    parser.add_argument("--window-style", choices=["normal", "minimized-noactivate", "noactivate"], default="normal",
                        help="Launch only: recorded Windows startup display request; match styles across measurements; never restores or activates the window")
    parser.add_argument("--allow-other-session-game", action="store_true",
                        help="Owner-authorized functional ASTER coexistence: allow an inaccessible game only in another Windows session; never waive performance isolation")
    args = parser.parse_args()
    require(not args.allow_other_session_game or args.action in (
        "test", "build", "build-menu-observer", "deploy", "deploy-menu-observer", "deploy-helper", "prepare",
        "verify-prepared", "launch", "capture", "rollback", "audit", "restore-runtime-file"),
        "Other-session exception is limited to functional fixture work")
    require(not args.allow_other_session_game or args.action != "prepare" or args.purpose == "functional",
            "Other-session preparation requires functional purpose")
    require(os.name == "nt", "Windows-only fixture workflow")
    root = physical(CANONICAL)
    require(root == CANONICAL, "Canonical fixture path mismatch")
    require(not git("ls-files", "--", ".rlo-test-instance", ":(glob)**/.rlo-test-instance/**"), "Fixture payload is tracked by Git")
    if args.action == "discover":
        result = discover()
    else:
        with fixture_lock(root):
            guard_operation_processes(root, args.action, args.label, args.allow_other_session_game)
            if args.action == "stage-gog":
                require(args.transaction is not None, "stage-gog requires --transaction")
                result = stage_gog(root, args.transaction)
            elif args.action == "stage-steam":
                require(args.steam_source is not None, "stage-steam requires --steam-source")
                result = stage_steam(root, args.steam_source)
            elif args.action == "bind-comparison-package":
                require(args.package is not None, "bind-comparison-package requires --package")
                result = bind_comparison_package(root, args.package)
            elif args.action in ("initialize", "refresh"):
                result = initialize(root, args.action == "refresh", args.resume)
            elif args.action in ("audit", "full-audit"):
                result = audit(root, args.action == "full-audit")
            elif args.action == "build":
                result = build(root)
            elif args.action == "build-audio-bootstrap":
                result = fixture_audio_bootstrap.build(root, types.SimpleNamespace(**globals()), args.audio_native_entry_probe)
            elif args.action == "deploy-audio-bootstrap":
                require(args.package is not None, "deploy-audio-bootstrap requires --package")
                result = fixture_audio_bootstrap.deploy(root, args.package, types.SimpleNamespace(**globals()))
            elif args.action == "build-menu-observer":
                result = build_menu_observer(root, args.observer_artwork, args.native_texture_probe)
            elif args.action == "deploy-menu-observer":
                require(args.package is not None, "deploy-menu-observer requires --package")
                result = deploy_menu_observer(root, args.package)
            elif args.action == "test":
                result = test(root, args.test_filter)
            elif args.action == "deploy-helper":
                require(args.package is not None, "deploy-helper requires --package reviewed-ZIP")
                result = deploy_helper(root, args.package, args.raw_pixel_helper)
            elif args.action == "deploy":
                require(args.package is not None, "deploy requires --package")
                result = deploy(root, args.package)
            elif args.action in ("exclude-mod", "restore-mod"):
                require(args.package_id is not None, args.action + " requires --package-id")
                result = exclude_mod(root, args.package_id, args.action == "restore-mod")
            elif args.action == "prepare":
                require(args.label is not None, "prepare requires --label")
                result = prepare(root, args.mode, args.label, args.lane, args.observation, args.strategy,
                                 args.foreign_cache_from, args.menu_observer, args.exit_after_menu_ready, args.gagarin_cache, args.png, args.png_source, args.png_cache_from, args.selection, args.loading_progress, args.character_presets, args.giddy_textures, args.activation, args.gameplay_smoke, args.residual_probe, args.purpose, args.user_settings, args.language, args.prepared_cache_from, args.background_hold, args.gameplay_save_from, args.world_background, args.native_stack_traces, args.parsed_xml_cache_from, args.xml_source_observer, args.processed_xml_cache_from, args.inheritance_cache_from, args.processed_xml_observer, args.language_cache_from, args.language_lifecycle, args.language_source_probe, args.texture_storage_probe, args.texture_source_change, args.atlas_cache_from, args.atlas_cache_corrupt, args.atlas_lifecycle, args.texture_quality_probe, args.audio_stream_probe, args.audio_bootstrap_probe, args.loading_ui_probe, args.loading_ui_layout, args.loading_xml_request_from, args.audio_prepared_probe, args.audio_cache_from, args.audio_native_entry_probe, args.audio_native_entry_stop_only, args.audio_native_entry_domain_probe, args.audio_patch_probe, args.audio_measurement, args.language_observer, args.serialized_texture_input, args.demand_texture_probe, args.demand_texture_selection, args.c14_background, args.first_build_images, args.raw_pixel_helper)
            elif args.action == "verify-prepared":
                require(args.label is not None, "verify-prepared requires --label")
                result = verify_prepared(root, args.label)
            elif args.action == "external-prepare":
                require(args.label and args.preparation_app and args.preparation_job,
                        "external-prepare requires --label, --preparation-app and --preparation-job")
                result = external_prepare(root, args.label, args.preparation_app, args.preparation_job)
            elif args.action == "restore-profile":
                result = restore_profile(root)
            elif args.action == "restore-runtime-file":
                require(args.package_id and args.relative_path, "Provide package-id and relative-path")
                result = restore_runtime_file(root, args.package_id, args.relative_path)
            elif args.action == "launch":
                require(args.label is not None, "launch requires --label")
                result = launch(root, args.label, args.authorize_live_launch, args.window_style)
            elif args.action == "capture":
                require(args.label is not None, "capture requires --label")
                result = capture(root, args.label)
            elif args.action == "recover":
                result = recover(root)
            else:
                require(args.transaction is not None, "rollback requires --transaction")
                result = rollback(root, args.transaction)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        sys.exit(1)
