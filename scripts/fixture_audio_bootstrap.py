# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Bounded C10 Doorstop experiment, operated only through fixture.py's lock."""
import os
from pathlib import Path
import shutil
import subprocess
import uuid
import xml.etree.ElementTree as ET
import zipfile

ARCHIVE_SHA = "7bb953e8d883c8bde76ced96f6d0e45660ad6e0151880d8ab5856bf4f532b147"
PROXY_SHA = "8c6cdbc38836dee87e3368f5de1994d7c0ccebf29e4ce7aba3c0981f9375412c"
LICENSE_SHA = "20c17d8b8c48a600800dfd14f95d5cb9ff47066a9641ddeab48dc54aec96e331"
HARMONY_SHA = "7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c"
SOURCE = "33dab9a6733862eb81869ff08431d9478b28784b"
NATIVE_ENTRY = "mono-profiler-wakeupentry.dll"
MONO_SHA = "d2f4348a5aa80bbdd0e73582cc2a00b3a17fe4a497da7052436e780cdee2a0fa"
UNITY_SHA = "e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96"
OWNED = ("winhttp.dll", "doorstop_config.ini", "WakeUpBootstrap", NATIVE_ENTRY)
CONFIG = """[General]
enabled=true
target_assembly=WakeUpBootstrap/WakeUp.AudioBootstrap.dll
redirect_output_log=false
boot_config_override=
ignore_disable_switch=false
[UnityMono]
dll_search_path_override=
debug_enabled=false
debug_address=127.0.0.1:10000
debug_suspend=false
"""


def environment(f):
    conflicts = [k for k in os.environ if k.upper().startswith("DOORSTOP_")
                 or k.upper() in ("DNSPY_UNITY_DBG", "DNSPY_UNITY_DBG2", "MONO_ENV_OPTIONS")]
    f.require(not conflicts, "Conflicting inherited bootstrap/debug environment: " + ", ".join(conflicts))


def build(root, f, native_entry=False):
    revision = f.git("rev-parse", "HEAD")
    f.require(not f.git("status", "--porcelain", "--", "validation/audio-bootstrap", "scripts/fixture.py",
                       "scripts/fixture_audio_bootstrap.py", "Directory.Build.props", "Directory.Packages.props"),
              "Uncommitted audio bootstrap build inputs")
    source = f.REPO / "artifacts/ecosystem-next-20260910/c10/preloader"
    archive = source / "doorstop_win_release_4.5.0.zip"
    license_file = source / "UnityDoorstop-LICENSE.txt"
    f.require(f.digest(archive) == ARCHIVE_SHA and f.digest(license_file) == LICENSE_SHA,
              "Official Doorstop archive/license identity mismatch")
    project = f.REPO / "validation/audio-bootstrap/AudioBootstrap.csproj"
    env = f.reference_environment(root)
    subprocess.run(["dotnet", "restore", str(project), "--locked-mode"], cwd=f.REPO, env=env, check=True)
    subprocess.run(["dotnet", "build", str(project), "-c", "Release", "--no-restore",
                    f"-p:SourceRevisionId={revision}"], cwd=f.REPO, env=env, check=True)
    destination = f.REPO / "artifacts/fixture-audio-bootstrap" / revision
    f.require(not destination.exists(), "Bootstrap package already exists; use it or a new commit")
    payload = destination / "WakeUpBootstrap"
    payload.mkdir(parents=True)
    with zipfile.ZipFile(archive) as package:
        (destination / "winhttp.dll").write_bytes(package.read("x64/winhttp.dll"))
    f.require(f.digest(destination / "winhttp.dll") == PROXY_SHA, "Doorstop x64 proxy identity mismatch")
    shutil.copy2(f.REPO / "artifacts/bin/AudioBootstrap/Release/net472/WakeUp.AudioBootstrap.dll", payload)
    shutil.copy2(license_file, payload)
    (destination / "doorstop_config.ini").write_text(CONFIG, encoding="utf-8")
    if native_entry:
        build_native_entry(destination, f)
    f.write(payload / "provenance.json", {"version": "4.5.0", "sourceCommit": SOURCE,
        "releaseUrl": "https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0",
        "archiveSha256": ARCHIVE_SHA, "proxySha256": PROXY_SHA, "sourceRevision": revision})
    f.write(destination.with_suffix(".json"), {"schema": "c10-audio-bootstrap-package.v1",
        "sourceRevision": revision, "nativeEntry": native_entry, "gameSha256": f.runtime_contract(root)["gameAssemblySha256"],
        "files": f.inventory(destination)})
    return {"package": str(destination), "sourceRevision": revision}


def build_native_entry(destination, f):
    """SDK-only correctness adapter; this does not enable sampling or execute it."""
    vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
    vs = Path(subprocess.check_output([str(vswhere), "-latest", "-products", "*", "-requires",
        "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"], text=True).strip())
    f.require(vs.is_dir(), "Existing C++ toolchain required")
    cmake = vs / "Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe"
    f.require(cmake.is_file(), "Existing CMake required")
    build_dir = f.REPO / "artifacts/c10-native-entry-build" / f.git("rev-parse", "HEAD")
    subprocess.run([str(cmake), "-S", str(f.REPO / "validation/audio-bootstrap/native"), "-B", str(build_dir),
                    "-A", "x64", f"-DCMAKE_GENERATOR_INSTANCE={vs}"], check=True)
    subprocess.run([str(cmake), "--build", str(build_dir), "--config", "Release", "--parallel", "1"], check=True)
    shutil.copy2(build_dir / "Release" / NATIVE_ENTRY, destination / NATIVE_ENTRY)
    shutil.copy2(f.REPO / "validation/audio-bootstrap/native/vendor/minhook/LICENSE.txt", destination / "WakeUpBootstrap/MinHook-LICENSE.txt")
    shutil.copy2(f.REPO / "validation/audio-bootstrap/native/vendor/minhook/PROVENANCE.json", destination / "WakeUpBootstrap/MinHook-PROVENANCE.json")


def deploy(root, package_root, f):
    f.audit(root)
    environment(f)
    f.require(f.runtime_contract(root)["target"] == "gog-rev573", "Bootstrap experiment is GOG-only")
    game = root / "game"
    for name in OWNED + ("version.dll", "winmm.dll", "dinput8.dll", "dxgi.dll", "d3d11.dll", "BepInEx", "MelonLoader"):
        f.require(not (game / name).exists(), "Bootstrap/proxy conflict: " + name)
    f.require(not (root / "audio-bootstrap.json").exists(), "Bootstrap already deployed")
    package_root = f.inside(f.REPO / "artifacts/fixture-audio-bootstrap", Path(package_root))
    receipt = f.read(package_root.with_suffix(".json"))
    f.require(receipt.get("schema") == "c10-audio-bootstrap-package.v1"
              and receipt["gameSha256"] == f.DEVELOPMENT_GAME, "Bootstrap package contract mismatch")
    f.verify_tree(package_root, receipt["files"], True)
    f.require({r["path"] for r in receipt["files"] if r["kind"] == "file"} == {
        "winhttp.dll", "doorstop_config.ini", "WakeUpBootstrap/WakeUp.AudioBootstrap.dll",
        "WakeUpBootstrap/UnityDoorstop-LICENSE.txt", "WakeUpBootstrap/provenance.json"}
        | ({NATIVE_ENTRY, "WakeUpBootstrap/MinHook-LICENSE.txt", "WakeUpBootstrap/MinHook-PROVENANCE.json"} if receipt.get("nativeEntry") else set()), "Unexpected bootstrap payload")
    if receipt.get("nativeEntry"):
        native_identity(root, f)
    f.require(f.digest(package_root / "winhttp.dll") == PROXY_SHA
              and (package_root / "doorstop_config.ini").read_text() == CONFIG, "Bootstrap proxy/config drift")
    harmony = game / "Mods/2934420800/Assemblies/0Harmony.dll"
    f.require(f.digest(harmony) == HARMONY_SHA, "Existing Prepatcher Harmony identity drift")
    stage = root / "staging" / ("audio-bootstrap-" + uuid.uuid4().hex)
    f.copy_stable(package_root, stage / "game", receipt["files"])
    session = uuid.uuid4().hex
    config = ET.Element("bootstrap", harmonyPath=str(harmony), harmonySha256=HARMONY_SHA,
                        fixtureGame=str(game), session=session,
                        receiptPath=str(root / "profile/FixtureMenuObserver/c10-preloader-early.xml"))
    ET.ElementTree(config).write(stage / "game/WakeUpBootstrap/bootstrap.xml", encoding="utf-8", xml_declaration=True)
    _, _, state = f.current(root)
    deployed = dict(receipt, schema="c10-audio-bootstrap-deployed.v1", session=session,
                    generation=state["generation"], manifestSha256=state["manifestSha256"],
                    originalAbsent=list(OWNED) + ["../audio-bootstrap.json"], files=f.inventory(stage / "game"))
    f.write(stage / "audio-bootstrap.json", deployed)
    tid = f.swap(root, [(stage / "game" / name, game / name) for name in OWNED if (stage / "game" / name).exists()]
                 + [(stage / "audio-bootstrap.json", root / "audio-bootstrap.json")], "deploy-audio-bootstrap")
    verify(root, f)
    return {"transaction": tid, "session": session, "experimentalOnly": True}


def verify(root, f):
    path = root / "audio-bootstrap.json"
    if not path.exists():
        return None
    receipt = f.read(path)
    _, _, state = f.current(root)
    f.require(receipt.get("schema") == "c10-audio-bootstrap-deployed.v1"
              and receipt["generation"] == state["generation"]
              and receipt["manifestSha256"] == state["manifestSha256"], "Bootstrap generation drift")
    game = root / "game"
    for row in receipt["files"]:
        target = f.inside(game, game / row["path"])
        f.require(row["path"].split("/")[0] in OWNED, "Unexpected bootstrap owned path")
        f.require(target.is_dir() if row["kind"] == "directory" else
                  target.is_file() and f.digest(target) == row["sha256"], "Bootstrap content drift: " + row["path"])
    subtree = [dict(r, path=r["path"][len("WakeUpBootstrap/"):]) for r in receipt["files"]
               if r["path"].startswith("WakeUpBootstrap/")]
    f.verify_tree(game / "WakeUpBootstrap", subtree, True)
    f.require(f.digest(game / "winhttp.dll") == PROXY_SHA
              and (game / "doorstop_config.ini").read_text() == CONFIG, "Bootstrap identity drift")
    if receipt.get("nativeEntry"):
        native_identity(root, f)
    else:
        f.require(not (game / NATIVE_ENTRY).exists(), "Unbound native entry adapter")
    return receipt


def native_identity(root, f):
    f.require(f.digest(root / "game/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll") == MONO_SHA
              and f.digest(root / "game/UnityPlayer.dll") == UNITY_SHA, "Native entry host identity mismatch")


def admit(root, purpose, probe, f, measurement=None):
    if measurement == "absent":
        f.require(purpose in ("functional", "performance") and not probe,
                  "Absent audio measurement cannot select the early bootstrap")
        paths = [root / "audio-bootstrap.json"] + [root / "game" / name for name in OWNED]
        f.require(not any(os.path.lexists(path) for path in paths),
                  "Absent audio measurement requires physical absence of the bootstrap receipt, injector, config, integration and profiler")
        environment(f)
        return None
    receipt = verify(root, f)
    if receipt:
        f.require(probe and (purpose == "functional" or (purpose == "performance" and measurement in ("off", "cold", "warm"))),
                  "Experimental preloader requires the functional bootstrap probe or explicit audio measurement")
        environment(f)
    return receipt


def cache_source(root, label, expected, f):
    """Carry only captured audio bytes from qualification or a matched cold measurement."""
    folder = f.inside(root, root / "results" / f.token(label))
    previous = f.read(folder / "run.json")
    measured = expected.get("audioMeasurement") == "cold"
    source_passed = (previous.get("audioMeasurement") == "cold" and previous.get("audioMeasurementPassed") is True
                     and previous.get("purpose") == expected.get("purpose") and previous.get("gameplayTestPassed") is True
                     and previous.get("audioNativeEntryShutdownPassed") is True
                     and previous.get("audioBootstrapObservationPassed") is True
                     and previous.get("audioPreloaderObservationPassed") is True) if measured else (
        previous.get("purpose") == "functional" and previous.get("audioPreparedProbe") in (
            "prepare", "natural-prepare", "wav-prepare", "formats-prepare", "frames-prepare", "readiness-prepare", "advance-prepare")
        and previous.get("audioPreparedTestPassed") is True)
    f.require(source_passed and previous.get("automaticTestPassed") is True and previous.get("exitCode") == 0
              and (not measured or previous.get("capturedUtc")), "Prepared audio source must be a successful captured first-build workload")
    f.require(all(previous.get(k) == value for k, value in expected.items()), "Prepared audio source configuration drift")
    prefix = "WakeUp/PreparedAudio/"
    rows = [dict(row, path=row["path"][len(prefix):]) for row in previous.get("evidence", []) if row["path"].startswith(prefix)]
    f.require(any(row["kind"] == "file" and row["path"].endswith(".bin") for row in rows), "Prepared audio source cache empty")
    import re
    f.require(all((row["kind"] == "directory" and row["path"] == "v1")
                  or (row["kind"] == "file" and re.fullmatch(r"v1/(?:[A-F0-9]{64}\.bin|usage)", row["path"]))
                  or (row["kind"] == "file" and row["path"] == "v1/.owner" and row["bytes"] == 0)
                  for row in rows), "Unexpected prepared audio cache files")
    path = f.inside(root, folder / "profile/WakeUp/PreparedAudio")
    f.verify_tree(path, rows, True)
    return path, rows, {"label": label, "runSha256": f.digest(folder / "run.json"), "storeInventorySha256": f.identity(rows)}
