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
import xml.etree.ElementTree as ET

# Private fixture paths, package ID and v1 record schemas retain their original
# spelling. Existing inventories and captures are immutable; they are not branding.
CANONICAL = Path(r"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance")
REPO = Path(__file__).resolve().parents[1]
PACKAGE_ID = "local.rimworldloadingoptimizer.op7validation"
PACKAGE_LEAF = "RimWorldLoadingOptimizer-OP7-LocalValidation"
MENU_ID = "local.fixture.menuobserver"
MENU_LEAF = "FixtureMenuObserver"
XML_EXPANDED_MODS = frozenset({
    "ceteam.combatextended", "vr.missilegirl",
    "oskarpotocki.vanillafactionsexpanded.core", "vanillaexpanded.vgeneticse",
    "oskarpotocki.vfe.medieval2", "vanillaracesexpanded.android",
})
# Historical snapshot observation only; never use it as deployed-package admission.
PINNED_GAME = "5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a"
DEVELOPMENT_GAME = "4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28"


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
    exclusions = excluded_mods(root, m, state)
    excluded = {p["id"]: p for p in exclusions}
    expected = {p["leaf"] for p in m["packages"] if p["id"] not in excluded}
    if (root / "menu-observer.json").exists():
        menu = read(root / "menu-observer.json")
        require(menu["generation"] == m["generation"], "Menu observer from another generation; deploy again")
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
    for name, path, key, exclude in [("game", root / "game", "gameInventorySha256", ("Mods",)),
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
                  exit_after_menu_ready=False, gagarin_cache="off", png="off", png_source="native", loading_progress="off", character_presets="off", giddy_textures="off", activation="fixture", gameplay_smoke=False, residual_probe=False):
    require(not gameplay_smoke or exit_after_menu_ready, "Gameplay smoke requires automatic capture and normal exit")
    require(activation in ("fixture", "user"), "Unknown activation mode")
    require(activation != "user" or (mode == "candidate" and strategy == "startup-searches" and all(value == "off" for value in (gagarin_cache, png, loading_progress, character_presets, giddy_textures)) and png_source == "native"), "User activation uses saved/default settings, not feature selectors")
    require(giddy_textures in ("off", "on", "timing", "verify"), "Unknown Giddy-Up texture mode")
    require(giddy_textures == "off" or (mode == "candidate" and strategy == "startup-searches"), "Giddy textures require startup-searches candidate")
    require(character_presets in ("off", "on", "timing"), "Unknown Character Editor preset mode")
    require(character_presets == "off" or (mode == "candidate" and strategy == "startup-searches"), "Character Editor presets require startup-searches candidate")
    require(loading_progress in ("off", "coalesce"), "Unknown Loading Progress mode")
    require(loading_progress == "off" or mode == "candidate", "Loading Progress optimization requires candidate")
    require(png in ("off", "control", "cache", "verify-cache"), "Unknown PNG mode")
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
    if residual_probe: args.append("--fixture-residual-probe")
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
    if png_source != "native":
        args.append("--wake-up-png-source=" + png_source)
    return args


def candidate_matches_snapshot(root, manifest, generation):
    if not (root / "candidate.json").is_file():
        return False
    candidate = read(root / "candidate.json")
    if {row["path"] for row in candidate.get("files", []) if row["kind"] == "file"} != {
            "About/About.xml", "Assemblies/WakeUp.dll"}:
        return False
    inv = generation / "inventories/game.json"
    require(digest(inv) == manifest["gameInventorySha256"], "Game inventory identity drift")
    game_sha = next(r["sha256"] for r in read(inv) if r["path"] == "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")
    contract = candidate.get("runtimeContract", {})
    return contract.get("target") == "gog-rev573" and contract.get("gameAssemblySha256") == game_sha


def reference_environment(root):
    m, gen, state = current(root)
    prepatcher = next(p for p in m["packages"] if p["id"] == "zetrith.prepatcher")
    refs = Path(prepatcher["fixture"]) / "Assemblies"
    managed = root / "game/RimWorldWin64_Data/Managed"
    require(digest(managed / "Assembly-CSharp.dll") == DEVELOPMENT_GAME, "Pinned GOG development assembly drift")
    return dict(os.environ, WAKE_UP_RIMWORLD_MANAGED_DIR=str(managed),
               WAKE_UP_PREPATCHER_ASSEMBLIES_DIR=str(refs), WAKE_UP_REFERENCE_TARGET="gog-rev573")


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
    return {"offlineTests": "passed", "target": "gog-rev573", "gameLaunched": False, "results": str(output)}


def build(root):
    revision = git("rev-parse", "HEAD")
    require(not git("status", "--porcelain", "--", "src", "build", "scripts/fixture.py", "Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.config", "validation/fixture-package"), "Uncommitted build inputs")
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
    receipt = {"sourceRevision": revision, "referenceTarget": "gog-rev573",
               "runtimeContract": {"target": "gog-rev573", "gameAssemblySha256": DEVELOPMENT_GAME},
               "files": inventory(package_root), "buildReferences": {
        str(Path(env["WAKE_UP_RIMWORLD_MANAGED_DIR"]) / "Assembly-CSharp.dll"): digest(Path(env["WAKE_UP_RIMWORLD_MANAGED_DIR"]) / "Assembly-CSharp.dll"),
        str(refs / "0Harmony.dll"): digest(refs / "0Harmony.dll")}}
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


def build_menu_observer(root):
    revision = git("rev-parse", "HEAD")
    require(not git("status", "--porcelain", "--", "validation/menu-observer", "scripts/fixture.py",
                    "Directory.Build.props", "Directory.Packages.props"), "Uncommitted observer build inputs")
    env = reference_environment(root)
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
    write(destination.with_suffix(".json"), {"sourceRevision": revision, "gameSha256": DEVELOPMENT_GAME,
                                          "supportsAutomaticExit": True, "supportsGameplaySmoke": True, "supportsResidualProbe": True,
                                          "files": inventory(destination)})
    return {"package": str(destination), "sourceRevision": revision}


def deploy_menu_observer(root, package_root):
    audit(root)
    manifest, _, _ = current(root)
    package_root = physical(package_root)
    receipt = read(package_root.with_suffix(".json"))
    verify_tree(package_root, receipt["files"], True)
    require(receipt["gameSha256"] == DEVELOPMENT_GAME, "Observer game target mismatch")
    require(package(package_root, "observer")["id"] == MENU_ID, "Wrong observer package ID")
    require({r["path"] for r in receipt["files"] if r["kind"] == "file"} ==
            {"About/About.xml", "Assemblies/FixtureMenuObserver.dll"}, "Unexpected observer package contents")
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


def prepare(root, mode, label, lane="representative", observation="timing", strategy="startup-searches",
            foreign_cache_from=None, menu_observer=True, exit_after_menu_ready=True, gagarin_cache="off", png="off", png_source="native", png_cache_from=None, selection_path=None, loading_progress="off", character_presets="off", giddy_textures="off", activation="fixture", gameplay_smoke=False, residual_probe=False):
    require(mode in ("absent", "baseline", "candidate"), "Unknown fixture mode")
    require(menu_observer or not exit_after_menu_ready, "Automatic exit requires the menu observer")
    arguments = run_arguments(root, mode, observation, strategy, exit_after_menu_ready, gagarin_cache, png, png_source, loading_progress, character_presets, giddy_textures, activation, gameplay_smoke, residual_probe)
    check = audit(root)
    m, gen, state = current(root)
    receipt = read(root / "candidate.json") if (root / "candidate.json").is_file() else None
    if mode != "absent":
        require(receipt is not None, "Deploy the observation-capable package for baseline/candidate modes")
        require(candidate_matches_snapshot(root, m, gen), "Deployed package predates fixture runtime integration or targets another build; rebuild/deploy")
    menu_receipt = read(root / "menu-observer.json") if menu_observer and (root / "menu-observer.json").is_file() else None
    require(not menu_observer or menu_receipt is not None, "Build/deploy the menu observer before selecting it")
    require(not exit_after_menu_ready or menu_receipt.get("supportsAutomaticExit"), "Rebuild/deploy the observer with automatic exit support")
    require(not gameplay_smoke or menu_receipt.get("supportsGameplaySmoke"), "Rebuild observer for gameplay smoke")
    require(not residual_probe or menu_receipt.get("supportsResidualProbe"), "Rebuild observer for residual probe")
    exclusions = excluded_mods(root, m, state)
    excluded_ids = {p["id"] for p in exclusions}
    selection = benchmark_selection(m, state, read(physical(selection_path)), excluded_ids) if selection_path else None
    active = run_order(m, lane, mode, menu_observer, excluded_ids, selection)
    overrides = {"runInBackground": True} if exit_after_menu_ready else {}
    label = token(label)
    results = root / "results" / label
    require(not results.exists(), "Run label already used")
    foreign_cached = None
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
    png_cached = None
    if png_cache_from:
        require(png in ("cache", "verify-cache"), "PNG cache restore requires cache mode")
        previous_folder = inside(root, root / "results" / token(png_cache_from))
        previous = read(previous_folder / "run.json")
        require(previous.get("automaticTestPassed") is True and previous.get("exitCode") == 0, "PNG cache source did not pass")
        expected = {"lane": lane, "mode": mode, "strategy": strategy, "generation": m["generation"], "candidate": receipt,
                    "activeOrder": active, "manifestSha256": state["manifestSha256"], "pngSource": png_source,
                    "gagarinCache": gagarin_cache, "menuObserverPackage": menu_receipt, "profileOverrides": overrides, "excludedMods": exclusions}
        require(all(previous.get(k) == v for k, v in expected.items()), "PNG cache source configuration mismatch")
        require(previous.get("png") in ("cache", "verify-cache"), "PNG cache source mode mismatch")
        prefix = "WakeUp/PngCache/"
        png_rows = [dict(row, path=row["path"][len(prefix):]) for row in previous.get("evidence", []) if row["path"].startswith(prefix)]
        require(any(row["kind"] == "file" for row in png_rows), "PNG cache source empty")
        require(all(row["kind"] != "file" or re.fullmatch(r"[A-F0-9]{64}\.bin", row["path"]) for row in png_rows), "Unexpected PNG cache files")
        png_cached = inside(root, previous_folder / "profile/WakeUp/PngCache")
        verify_tree(png_cached, png_rows, True)
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
    if exit_after_menu_ready:
        prefs = staged_profile / "Config/Prefs.xml"
        prefs_doc = ET.parse(prefs) if prefs.exists() else ET.ElementTree(ET.Element("PrefsData"))
        background = prefs_doc.find("runInBackground")
        if background is None:
            background = ET.SubElement(prefs_doc.getroot(), "runInBackground")
        background.text = "True"
        prefs_doc.write(prefs, encoding="utf-8", xml_declaration=True)
    if foreign_cached is not None:
        shutil.copytree(foreign_cached, staged_profile / "MissileGirl")
        verify_tree(staged_profile / "MissileGirl", foreign_rows, True)
    if png_cached is not None:
        shutil.copytree(png_cached, staged_profile / "WakeUp/PngCache")
        verify_tree(staged_profile / "WakeUp/PngCache", png_rows, True)
    staged_selection = root / "staging" / ("selection-" + uuid.uuid4().hex + ".json")
    write(staged_selection, {"label": label})
    replacements = []
    if receipt is not None:
        location = candidate_location(root)
        target = root / "parked-rlo/package" if mode == "absent" else root / "game/Mods" / PACKAGE_LEAF
        if location != target:
            replacements.append((location, target))
    tid = swap(root, replacements + [(staged_profile, root / "profile"), (staged_selection, root / "prepared.json")], "prepare-profile")
    run = {"schema": "rlo-drm-run.v1", "label": label, "mode": mode, "lane": lane,
           "benchmarkSelection": selection, "benchmarkSelectionSha256": identity(selection) if selection else None,
           "menuObserver": menu_observer, "menuObserverPackage": menu_receipt,
           "exitAfterMenuReady": exit_after_menu_ready,
           "gagarinCache": gagarin_cache, "loadingProgress": loading_progress, "characterPresets": character_presets, "giddyTextures": giddy_textures, "activation": activation, "gameplaySmoke": gameplay_smoke, "residualProbe": residual_probe,
           "png": png, "pngSource": png_source, "pngCacheFrom": png_cache_from,
           "excludedMods": exclusions, "profileOverrides": overrides,
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
           "restoredCacheKinds": (["gagarin-MissileGirl"] if foreign_cached is not None else []) + (["wake-up-PngCache"] if png_cached is not None else []),
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
    require(run["status"] == "prepared-offline", "Run already launched")
    audit(root)
    m, gen, state = current(root)
    receipt = read(root / "candidate.json") if (root / "candidate.json").is_file() else None
    require(run["manifestSha256"] == state["manifestSha256"] and run["candidate"] == receipt, "Run binding drift")
    if run.get("menuObserver"):
        require(run["menuObserverPackage"] == read(root / "menu-observer.json"), "Observer package changed; prepare again")
    require(not run.get("exitAfterMenuReady") or (run.get("menuObserver")
            and run["menuObserverPackage"].get("supportsAutomaticExit")), "Automatic exit requires a supported observer")
    if run["mode"] == "absent":
        require(not (root / "game/Mods" / PACKAGE_LEAF).exists(), "Absent run has an installed optimizer")
    else:
        require(candidate_matches_snapshot(root, m, gen), "Prepared package does not support the frozen runtime; rebuild/deploy/prepare")
        require(candidate_location(root) == root / "game/Mods" / PACKAGE_LEAF, "Prepared optimizer is parked")
    verify_tree(root / "profile", run["profileBefore"], True)
    require(order(root / "profile/Config/ModsConfig.xml") == run["activeOrder"], "Launch order drift")
    exclusions = excluded_mods(root, m, state)
    require(run.get("excludedMods", []) == exclusions, "Prepared excluded-mod selection drift")
    selection = run.get("benchmarkSelection")
    if selection is not None:
        benchmark_selection(m, state, selection, {p["id"] for p in exclusions})
        require(identity(selection) == run.get("benchmarkSelectionSha256"), "Benchmark selection identity drift")
    require(run["activeOrder"] == run_order(m, run["lane"], run["mode"], run.get("menuObserver", False),
            {p["id"] for p in exclusions}, selection), "Frozen launch order drift")
    if run.get("exitAfterMenuReady"):
        require(run.get("profileOverrides") == {"runInBackground": True}
                and ET.parse(root / "profile/Config/Prefs.xml").findtext("runInBackground") == "True",
                "Automatic launch requires prepared runInBackground=True")
    exe = inside(root, root / "game/RimWorldWin64.exe")
    observation = "none" if run["mode"] == "absent" else run.get("observation", "timing")
    strategy = run.get("strategy", "startup-searches")
    args = run_arguments(root, run["mode"], observation, strategy,
                         run.get("exitAfterMenuReady", False), run.get("gagarinCache", "off"), run.get("png", "off"), run.get("pngSource", "native"), run.get("loadingProgress", "off"), run.get("characterPresets", "off"), run.get("giddyTextures", "off"), run.get("activation", "fixture"), run.get("gameplaySmoke", False), run.get("residualProbe", False))
    require(str(exe) == run["executable"] and args == run["arguments"], "Launch command drift")
    require(run["mode"] in ("absent", "baseline", "candidate") and run["lane"] in ("activation", "representative", "xml-expanded"), "Run mode/lane drift")
    require("startedUtc" not in run, "Launch already attempted; prepare a new label")
    return {"verifiedPrepared": label, "lane": run["lane"], "mode": run["mode"],
            "sourceRevision": receipt["sourceRevision"] if receipt else None, "gameLaunched": False,
            "command": [str(exe), *args, "-logFile", str(root / "profile/Player.log")]}


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


def launch(root, label, authorized):
    require(authorized, "Live launch requires explicit --authorize-live-launch; preparation is offline")
    verified = verify_prepared(root, label)
    folder = root / "results" / label
    run = read(folder / "run.json")
    start = time.perf_counter()
    run["startedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    run["status"] = "launch-attempted"
    if run.get("menuObserver"):
        start_menu_clock(root, run)
    write(folder / "run.json", run)
    try:
        process = subprocess.Popen(verified["command"], cwd=Path(run["executable"]).parent)
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
    print("DRM-free process started. " + ("Waiting for in-game menu-ready signal. " + exit_message
          if run.get("menuObserver") else "Observe usable menu; load no save; exit through Quit to OS."), flush=True)
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
            exit_budget = 600 if run.get("gameplaySmoke") else 60
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
    result = capture(root, label)
    if run.get("gameplaySmoke"):
        require(run.get("gameplayTestPassed"), "Gameplay smoke failed; evidence captured")
    if automatic:
        require(run["automaticTestPassed"],
                "Automatic test failed: missing menu-ready signal or unsuccessful process exit; evidence captured")
        result.update(automaticTestPassed=True, menuReadyElapsedSeconds=run["menuReadyObservation"]["elapsedSeconds"])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["discover", "initialize", "refresh", "audit", "full-audit", "test", "build", "deploy", "build-menu-observer", "deploy-menu-observer", "prepare", "verify-prepared", "restore-profile", "restore-runtime-file", "exclude-mod", "restore-mod", "launch", "capture", "recover", "rollback"])
    parser.add_argument("--test-filter", help="Optional focused managed checks; always retain fixture safety tests")
    parser.add_argument("--package-id", help="Exact frozen package ID for reversible exclusion")
    parser.add_argument("--relative-path", help="Exact inventoried file for restore-runtime-file")
    parser.add_argument("--gagarin-cache", choices=["off", "timing", "on", "verify"], default="off")
    parser.add_argument("--png", choices=["off", "control", "cache", "verify-cache"], default="off")
    parser.add_argument("--png-source", choices=["native", "original"], default="native", help="Original is a qualification-only PNG sibling selection, not a production optimization")
    parser.add_argument("--gameplay-smoke", action="store_true", help="Fixture-only new colony, ticking and save/reload qualification; not a speed arm")
    parser.add_argument("--residual-probe", action="store_true", help="Qualification-only first-menu/late-type cost observation")
    parser.add_argument("--activation", choices=["fixture", "user"], default="fixture", help="User exercises normal activation without Wake-Up command-line selectors")
    parser.add_argument("--giddy-textures", choices=["off", "on", "timing", "verify"], default="off")
    parser.add_argument("--character-presets", choices=["off", "on", "timing"], default="off")
    parser.add_argument("--loading-progress", choices=["off", "coalesce"], default="off")
    parser.add_argument("--png-cache-from", help="Restore only the captured matching processed PNG cache")
    parser.add_argument("--package", type=Path)
    parser.add_argument("--mode", choices=["absent", "baseline", "candidate"], default="baseline")
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
    args = parser.parse_args()
    require(os.name == "nt", "Windows-only fixture workflow")
    root = physical(CANONICAL)
    require(root == CANONICAL, "Canonical fixture path mismatch")
    require(not git("ls-files", "--", ".rlo-test-instance", ":(glob)**/.rlo-test-instance/**"), "Fixture payload is tracked by Git")
    if args.action == "discover":
        result = discover()
    else:
        with fixture_lock(root):
            no_game()
            if args.action in ("initialize", "refresh"):
                result = initialize(root, args.action == "refresh", args.resume)
            elif args.action in ("audit", "full-audit"):
                result = audit(root, args.action == "full-audit")
            elif args.action == "build":
                result = build(root)
            elif args.action == "build-menu-observer":
                result = build_menu_observer(root)
            elif args.action == "deploy-menu-observer":
                require(args.package is not None, "deploy-menu-observer requires --package")
                result = deploy_menu_observer(root, args.package)
            elif args.action == "test":
                result = test(root, args.test_filter)
            elif args.action == "deploy":
                require(args.package is not None, "deploy requires --package")
                result = deploy(root, args.package)
            elif args.action in ("exclude-mod", "restore-mod"):
                require(args.package_id is not None, args.action + " requires --package-id")
                result = exclude_mod(root, args.package_id, args.action == "restore-mod")
            elif args.action == "prepare":
                require(args.label is not None, "prepare requires --label")
                result = prepare(root, args.mode, args.label, args.lane, args.observation, args.strategy,
                                 args.foreign_cache_from, args.menu_observer, args.exit_after_menu_ready, args.gagarin_cache, args.png, args.png_source, args.png_cache_from, args.selection, args.loading_progress, args.character_presets, args.giddy_textures, args.activation, args.gameplay_smoke, args.residual_probe)
            elif args.action == "verify-prepared":
                require(args.label is not None, "verify-prepared requires --label")
                result = verify_prepared(root, args.label)
            elif args.action == "restore-profile":
                result = restore_profile(root)
            elif args.action == "restore-runtime-file":
                require(args.package_id and args.relative_path, "Provide package-id and relative-path")
                result = restore_runtime_file(root, args.package_id, args.relative_path)
            elif args.action == "launch":
                require(args.label is not None, "launch requires --label")
                result = launch(root, args.label, args.authorize_live_launch)
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
