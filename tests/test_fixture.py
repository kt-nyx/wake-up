# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Focused offline checks; never use the canonical game or profile."""
import importlib.util
import os
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("fixture", Path(__file__).parents[1] / "scripts/fixture.py")
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)


class FixtureTests(unittest.TestCase):
    def test_user_activation_has_no_optimizer_selectors_and_rejects_mixed_controls(self):
        args = f.run_arguments(Path('Z:/fixture'), 'candidate', exit_after_menu_ready=True, activation='user')
        self.assertTrue(any(a.startswith('-savedatafolder=') for a in args))
        self.assertIn('--fixture-exit-after-menu-ready', args)
        self.assertFalse(any(a.startswith('--wake-up-') for a in args))
        for options in [dict(mode='baseline'), dict(mode='candidate', png='cache')]:
            with self.assertRaises(Exception):
                f.run_arguments(Path('Z:/fixture'), activation='user', **options)

    def test_post_menu_checks_require_explicit_selection_and_automatic_exit(self):
        ordinary = f.run_arguments(Path('Z:/fixture'), 'candidate', exit_after_menu_ready=True)
        self.assertNotIn('--fixture-gameplay-smoke', ordinary)
        self.assertNotIn('--fixture-residual-probe', ordinary)
        smoke = f.run_arguments(Path('Z:/fixture'), 'candidate', exit_after_menu_ready=True, gameplay_smoke=True, residual_probe=True)
        self.assertIn('--fixture-gameplay-smoke', smoke)
        self.assertIn('--fixture-residual-probe', smoke)
        with self.assertRaises(Exception):
            f.run_arguments(Path('Z:/fixture'), 'candidate', gameplay_smoke=True)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="wake-up-fixture-test-")
        self.base = Path(self.temp.name)
        self.root = self.base / "fixture"
        self.root.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def test_atomic_write_retries_transient_reader_lock(self):
        target = self.root / "run.json"
        f.write(target, {"status": "prepared"})
        replace = os.replace
        calls = []

        def locked_once(source, destination):
            calls.append((source, destination))
            if len(calls) == 1:
                raise PermissionError("reader still holds destination")
            return replace(source, destination)

        with patch.object(f.os, "replace", side_effect=locked_once), patch.object(f.time, "sleep") as sleep:
            f.write(target, {"status": "running", "pid": 123})
        self.assertEqual(f.read(target), {"status": "running", "pid": 123})
        self.assertEqual(len(calls), 2)
        sleep.assert_called_once_with(0.05)
        self.assertFalse(list(self.root.glob("run.json.tmp-*")))

    def test_atomic_write_preserves_evidence_when_lock_persists(self):
        target = self.root / "run.json"
        f.write(target, {"status": "prepared"})
        with patch.object(f.os, "replace", side_effect=PermissionError("locked")) as replace, patch.object(f.time, "sleep"):
            with self.assertRaises(PermissionError):
                f.write(target, {"status": "running", "pid": 123})
        self.assertEqual(replace.call_count, 10)
        self.assertEqual(f.read(target), {"status": "prepared"})
        pending = list(self.root.glob("run.json.tmp-*"))
        self.assertEqual(len(pending), 1)
        self.assertEqual(f.read(pending[0]), {"status": "running", "pid": 123})

    def test_guards_reject_escape_sibling_root_and_traversal(self):
        for p in [self.root, self.base / "fixture-other/file", self.root / "../outside"]:
            with self.assertRaises(RuntimeError):
                f.inside(self.root, p)
        self.assertEqual(f.inside(self.root, self.root / "safe"), self.root / "safe")
        with self.assertRaises(RuntimeError):
            f.token("../other")

    def test_reparse_refused_before_following(self):
        target = self.base / "normal-profile"
        target.mkdir()
        link = self.root / "linked-profile"
        try:
            link.symlink_to(target, target_is_directory=True)
        except OSError:
            subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)], check=True, capture_output=True)
        try:
            with self.assertRaises(RuntimeError):
                f.scan(self.root)
            with self.assertRaises(RuntimeError):
                f.inside(self.root, link / "Config")
        finally:
            link.rmdir()

    def test_stable_copy_hashes_and_keeps_source(self):
        source = self.base / "source"
        source.mkdir()
        (source / "empty").mkdir()
        (source / "data").write_bytes(b"original")
        rows = f.copy_stable(source, self.root / "copy")
        f.verify_tree(self.root / "copy", rows, True)
        self.assertEqual((source / "data").read_bytes(), b"original")

    def test_source_change_during_copy_refuses_package(self):
        source = self.base / "source"
        source.mkdir()
        (source / "data").write_bytes(b"original")
        digest = f.digest
        def mutate(path):
            if Path(path) == source / "data":
                (source / "data").write_bytes(b"changed!")
            return digest(path)
        with patch.object(f, "digest", side_effect=mutate):
            with self.assertRaisesRegex(RuntimeError, "Content changed"):
                f.copy_stable(source, self.root / "copy")

    def test_swap_rollback_preserves_both_versions(self):
        incoming, target = self.root / "new", self.root / "profile"
        incoming.mkdir(); target.mkdir()
        (incoming / "file").write_text("new")
        (target / "file").write_text("old")
        tid = f.swap(self.root, [(incoming, target)], "test")
        self.assertEqual((target / "file").read_text(), "new")
        f.rollback(self.root, tid)
        self.assertEqual((target / "file").read_text(), "old")
        self.assertEqual((incoming / "file").read_text(), "new")

    def test_runtime_file_restore_requires_frozen_bytes_and_preserves_change(self):
        (self.root / "staging").mkdir()
        source = self.base / "source"
        source.mkdir()
        (source / "data").write_bytes(b"frozen")
        target = self.root / "package"
        target.mkdir()
        (target / "data").write_bytes(b"downloaded")
        rows = f.inventory(source)
        f.write(self.root / "inventory.json", rows)
        package = {"id": "test.mod", "source": str(source), "fixture": str(target),
                   "inventory": "inventory.json"}
        with patch.object(f, "current", return_value=({"packages": [package]}, self.root, {})):
            with self.assertRaisesRegex(RuntimeError, "not an inventoried"):
                f.restore_runtime_file(self.root, "test.mod", "../outside")
            (source / "data").write_bytes(b"newest")
            with self.assertRaisesRegex(RuntimeError, "refusing refresh"):
                f.restore_runtime_file(self.root, "test.mod", "data")
            self.assertEqual((target / "data").read_bytes(), b"downloaded")
            (source / "data").write_bytes(b"frozen")
            result = f.restore_runtime_file(self.root, "test.mod", "data")
        self.assertEqual((target / "data").read_bytes(), b"frozen")
        self.assertEqual((source / "data").read_bytes(), b"frozen")
        f.rollback(self.root, result["transaction"])
        self.assertEqual((target / "data").read_bytes(), b"downloaded")

    def test_benchmark_selection_is_frozen_unique_and_exclusion_bound(self):
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld", "sample.mod"]
        manifest = {"activeOrder": active}
        state = {"manifestSha256": "frozen"}
        selection = {"schema": "rlo-benchmark-selection.v1", "name": "sample", "manifestSha256": "frozen", "activeOrder": active}
        self.assertEqual(f.benchmark_selection(manifest, state, selection), selection)
        for change in [{"manifestSha256": "other"}, {"activeOrder": active + ["sample.mod"]},
                       {"activeOrder": active + ["unfrozen.mod"]}, {"activeOrder": active[1:]}]:
            with self.assertRaises(RuntimeError):
                f.benchmark_selection(manifest, state, dict(selection, **change))
        with self.assertRaises(RuntimeError):
            f.benchmark_selection(manifest, state, selection, {"sample.mod"})
        self.assertEqual(f.run_order(manifest, "representative", "absent", True, selection=selection),
                         active[:2] + [f.MENU_ID] + active[2:])
        with self.assertRaises(RuntimeError):
            f.run_order(manifest, "activation", "absent", selection=selection)

    def test_interrupted_swap_recovers_after_old_target_move(self):
        incoming, target = self.root / "new", self.root / "profile"
        incoming.mkdir(); target.mkdir()
        (target / "file").write_text("old")
        rename = Path.rename
        def fail_new(path, dest):
            if path == incoming:
                raise OSError("simulated interrupted final rename")
            return rename(path, dest)
        with patch.object(Path, "rename", fail_new):
            with self.assertRaises(OSError):
                f.swap(self.root, [(incoming, target)], "test")
        f.recover(self.root)
        self.assertEqual((target / "file").read_text(), "old")
        self.assertTrue(incoming.exists())

    def test_offline_initialization_order_integrity_reset_and_refresh(self):
        game = self.root / "game"
        (game / "Mods").mkdir(parents=True)
        (game / "RimWorldWin64_Data/Managed").mkdir(parents=True)
        (game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll").write_bytes(b"fixture-runtime")
        (game / "RimWorldWin64.exe").write_bytes(b"not executable")
        (game / "goggame-1094900565.info").write_text("fixture")
        (game / "Version.txt").write_text("fixture-version")
        (self.root / "profile").mkdir()
        source = self.base / "normal"
        (source / "Config").mkdir(parents=True)
        (source / "Saves").mkdir()
        (source / "Saves/private.rws").write_text("never copy")
        active = ["zetrith.prepatcher", "brrainz.harmony", "sample.mod"]
        (source / "Config/ModsConfig.xml").write_text("<ModsConfigData><activeMods>" + "".join("<li>"+i+"</li>" for i in active) + "</activeMods></ModsConfigData>")
        (source / "Config/Mod_123_Test.xml").write_text("<settings/>")
        (source / "Config/Mod_999_Stale.xml").write_text("<stale/>")
        mod = self.base / "workshop/123"
        (mod / "About").mkdir(parents=True)
        (mod / "About/About.xml").write_text("<ModMetaData><packageId>sample.mod</packageId></ModMetaData>")
        (mod / "file").write_bytes(b"abc")
        packages = [f.package(mod, "workshop")]
        for index, pid in enumerate(active[:2]):
            dep = self.base / "workshop" / str(index)
            (dep / "About").mkdir(parents=True)
            (dep / "About/About.xml").write_text("<ModMetaData><packageId>"+pid+"</packageId></ModMetaData>")
            packages.append(f.package(dep, "workshop"))
        sources = {"game": str(game), "profile": str(source), "packages": packages,
                   "official": [], "activeOrder": active}
        with patch.object(f, "discover", return_value=sources), patch.object(f, "git", return_value="a" * 40), patch.object(f, "DEVELOPMENT_GAME", f.digest(game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")):
            with patch.object(f, "discover", side_effect=[sources, RuntimeError("simulated final refusal")]):
                with self.assertRaisesRegex(RuntimeError, "simulated final refusal"):
                    f.initialize(self.root)
            staged = next((self.root / "staging").iterdir())
            (mod / "file").write_bytes(b"abcd")
            with patch.object(f, "copy_stable", wraps=f.copy_stable) as copier:
                f.initialize(self.root, resume=staged.name)
                self.assertEqual(copier.call_count, 1, "resume re-copies only the changed package")
            self.assertEqual(f.audit(self.root)["enabled"], 3)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active)
            self.assertFalse((self.root / "profile/Saves").exists())
            self.assertFalse((self.root / "profile/Config/Mod_999_Stale.xml").exists())
            copied = game / "Mods/123/file"
            stamp = copied.stat()
            copied.write_bytes(b"wxyz")
            os.utime(copied, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            # Metadata is intentionally advisory; full audit detects timestamp-preserving corruption.
            f.audit(self.root)
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.audit(self.root, True)
            (self.root / "profile/user-cache").write_text("discard only in fixture")
            f.restore_profile(self.root)
            self.assertFalse((self.root / "profile/user-cache").exists())
            f.initialize(self.root, refresh=True)
            f.audit(self.root, True)
            self.assertEqual(copied.read_bytes(), b"abcd")
            f.prepare(self.root, "absent", "no-package", menu_observer=False, exit_after_menu_ready=False)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active)
            self.assertIsNone(f.verify_prepared(self.root, "no-package")["sourceRevision"])
            candidate = self.base / "candidate"
            (candidate / "About").mkdir(parents=True)
            (candidate / "Assemblies").mkdir()
            (candidate / "About/About.xml").write_text("<ModMetaData><packageId>"+f.PACKAGE_ID+"</packageId></ModMetaData>")
            (candidate / "Assemblies/WakeUp.dll").write_bytes(b"offline mock")
            f.write(candidate.with_suffix(".json"), {"sourceRevision": "a"*40, "files": f.inventory(candidate),
                    "runtimeContract": {"target": "gog-rev573", "gameAssemblySha256": f.digest(game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")}})
            f.deploy(self.root, candidate)
            installed = f.read(self.root / "candidate.json")
            # A legacy two-DLL installation remains auditable and recoverable,
            # but cannot be prepared for launch under the new runtime contract.
            legacy_core = game / "Mods" / f.PACKAGE_LEAF / "Assemblies/RimWorldLoadingOptimizer.Core.dll"
            legacy_core.write_bytes(b"old core")
            f.write(self.root / "candidate.json", dict(installed, files=f.inventory(legacy_core.parents[1])))
            f.audit(self.root)
            with self.assertRaisesRegex(RuntimeError, "predates fixture runtime integration"):
                f.prepare(self.root, "candidate", "legacy-refused", menu_observer=False, exit_after_menu_ready=False)
            migration = f.deploy(self.root, candidate)
            self.assertFalse(legacy_core.exists())
            f.rollback(self.root, migration["transaction"])
            self.assertEqual(legacy_core.read_bytes(), b"old core")
            f.audit(self.root)
            f.deploy(self.root, candidate)
            self.assertFalse(legacy_core.exists())
            stale_core = candidate / "Assemblies/RimWorldLoadingOptimizer.Core.dll"
            stale_core.write_bytes(b"stale core")
            f.write(candidate.with_suffix(".json"), dict(installed, files=f.inventory(candidate)))
            with self.assertRaisesRegex(RuntimeError, "stale Core"):
                f.deploy(self.root, candidate)
            stale_core.unlink()
            f.write(candidate.with_suffix(".json"), dict(installed, files=f.inventory(candidate)))
            f.write(self.root / "candidate.json", dict(installed, runtimeContract={}))
            with self.assertRaisesRegex(RuntimeError, "predates fixture runtime integration"):
                f.prepare(self.root, "candidate", "refused-old-package", menu_observer=False, exit_after_menu_ready=False)
            self.assertFalse((self.root / "results/refused-old-package").exists())
            f.write(self.root / "candidate.json", installed)
            for mode in ["baseline", "candidate"]:
                prepared = f.prepare(self.root, mode, mode, menu_observer=False, exit_after_menu_ready=False,
                                     character_presets="on" if mode == "candidate" else "off",
                                     giddy_textures="on" if mode == "candidate" else "off")
                selected = f.order(self.root / "profile/Config/ModsConfig.xml")
                self.assertEqual(selected, active[:2]+[f.PACKAGE_ID]+active[2:])
                self.assertEqual(f.read(self.root / "results" / mode / "run.json")["mode"], mode)
                self.assertEqual(f.read(self.root / "results" / mode / "run.json")["strategy"], "startup-searches")
            with patch.object(f.subprocess, "Popen") as launch:
                verified = f.verify_prepared(self.root, "candidate")
                self.assertFalse(verified["gameLaunched"])
                self.assertEqual(verified["command"], [str(game / "RimWorldWin64.exe"),
                    "-savedatafolder=" + str(self.root / "profile"),
                    "--wake-up-mode=candidate", "--wake-up-strategy=startup-searches", "--wake-up-giddy-textures=on", "--wake-up-character-presets=on", "-logFile", str(self.root / "profile/Player.log")])
                config = self.root / "profile/Config/ModsConfig.xml"
                original = config.read_bytes()
                stamp = config.stat()
                config.write_bytes(original.replace(b"sample.mod", b"changed.id"))
                os.utime(config, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
                with self.assertRaisesRegex(RuntimeError, "Content drift"):
                    f.verify_prepared(self.root, "candidate")
                config.write_bytes(original)
                os.utime(config, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
                f.write(self.root / "candidate.json", dict(installed, sourceRevision="b"*40))
                with self.assertRaisesRegex(RuntimeError, "Run binding drift"):
                    f.verify_prepared(self.root, "candidate")
                f.write(self.root / "candidate.json", installed)
                self.assertEqual(f.read(self.root / "results/candidate/run.json")["status"], "prepared-offline")
                launch.assert_not_called()
            f.rollback(self.root, prepared["transaction"])
            self.assertEqual(f.read(self.root / "prepared.json")["label"], "baseline")
            f.prepare(self.root, "candidate", "activation", lane="activation", menu_observer=False, exit_after_menu_ready=False)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active[:2]+[f.PACKAGE_ID])
            self.assertTrue((self.root / "game/Mods/123/file").is_file(), "Disabled discovery payload remains frozen")
            f.restore_profile(self.root)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active)
            self.assertFalse((self.root / "prepared.json").exists())
            live = self.root / "game/Mods" / f.PACKAGE_LEAF
            parked = self.root / "parked-rlo/package"
            absent = f.prepare(self.root, "absent", "absent", menu_observer=False, exit_after_menu_ready=False)
            self.assertFalse(live.exists())
            self.assertTrue(parked.is_dir())
            f.audit(self.root, True)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active)
            self.assertEqual(f.verify_prepared(self.root, "absent")["command"], [str(game / "RimWorldWin64.exe"),
                "-savedatafolder=" + str(self.root / "profile"), "-logFile", str(self.root / "profile/Player.log")])
            self.assertTrue((game / "Mods/123/file").exists())
            f.rollback(self.root, absent["transaction"])
            self.assertTrue(live.is_dir())
            self.assertFalse(parked.exists())
            f.prepare(self.root, "absent", "absent-activation", lane="activation", menu_observer=False, exit_after_menu_ready=False)
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active[:2])
            f.prepare(self.root, "baseline", "restored-baseline", observation="timing", strategy="type-lookup", menu_observer=False, exit_after_menu_ready=False)
            self.assertTrue(live.is_dir())
            self.assertFalse(parked.exists())
            verified = f.verify_prepared(self.root, "restored-baseline")
            self.assertFalse(any(a.startswith("--wake-up-observe=") for a in verified["command"]))
            self.assertIn("--wake-up-strategy=type-lookup", verified["command"])
            self.assertFalse(any(a.startswith("--wake-up-workers=") for a in verified["command"]))
            run_path = self.root / "results/restored-baseline/run.json"
            prepared_run = f.read(run_path)
            self.assertEqual(prepared_run["strategy"], "type-lookup")
            f.write(run_path, dict(prepared_run, strategy="def-lookup"))
            with self.assertRaisesRegex(RuntimeError, "Launch command drift"):
                f.verify_prepared(self.root, "restored-baseline")
            f.write(run_path, prepared_run)
            f.prepare(self.root, "absent", "absent-before-deploy", menu_observer=False, exit_after_menu_ready=False)
            f.deploy(self.root, candidate)
            self.assertTrue(live.is_dir())
            self.assertFalse(parked.exists())
            with self.assertRaisesRegex(RuntimeError, "installed optimizer"):
                f.verify_prepared(self.root, "absent-before-deploy")
            f.prepare(self.root, "absent", "absent-before-refresh", menu_observer=False, exit_after_menu_ready=False)
            f.initialize(self.root, refresh=True)
            self.assertFalse((self.root / "candidate.json").exists())
            self.assertFalse(parked.exists())
            f.audit(self.root, True)

            frozen, generation, _ = f.current(self.root)
            manifest_before = (generation / "manifest.json").read_bytes()
            seed_before = (generation / "seed/Config/ModsConfig.xml").read_bytes()
            excluded = f.exclude_mod(self.root, "sample.mod")
            self.assertEqual(excluded["selectedFrozenEnabled"], 2)
            self.assertFalse((game / "Mods/123").exists())
            self.assertTrue((self.root / "parked-mods/123/file").is_file())
            self.assertEqual(f.audit(self.root, True)["selectedFrozenEnabled"], 2)
            f.prepare(self.root, "absent", "excluded", menu_observer=False, exit_after_menu_ready=False)
            excluded_run = f.read(self.root / "results/excluded/run.json")
            self.assertEqual(excluded_run["activeOrder"], active[:2])
            self.assertEqual(excluded_run["frozenEnabledCount"], 3)
            self.assertEqual(excluded_run["selectedFrozenEnabledCount"], 2)
            self.assertEqual(excluded_run["excludedMods"][0]["id"], "sample.mod")
            f.verify_prepared(self.root, "excluded")
            f.write(self.root / "results/excluded/run.json", dict(excluded_run, excludedMods=[]))
            with self.assertRaisesRegex(RuntimeError, "selection drift"):
                f.verify_prepared(self.root, "excluded")
            (game / "Mods/123").mkdir()
            with self.assertRaisesRegex(RuntimeError, "exactly once"):
                f.audit(self.root)
            (game / "Mods/123").rmdir()
            f.exclude_mod(self.root, "sample.mod", restore=True)
            self.assertTrue((game / "Mods/123/file").is_file())
            self.assertFalse((self.root / "parked-mods/123").exists())
            self.assertEqual(f.audit(self.root, True)["selectedFrozenEnabled"], 3)
            self.assertEqual((generation / "manifest.json").read_bytes(), manifest_before)
            self.assertEqual((generation / "seed/Config/ModsConfig.xml").read_bytes(), seed_before)

    def test_launch_needs_explicit_authorization(self):
        with patch.object(f.subprocess, "Popen") as launch:
            with self.assertRaisesRegex(RuntimeError, "explicit"):
                f.launch(self.root, "test", False)
            launch.assert_not_called()

    def test_failed_process_creation_cannot_be_captured_or_reused(self):
        folder = self.root / "results/failed"
        f.write(folder / "run.json", {"status": "prepared-offline", "executable": str(self.root / "game/fake.exe")})
        f.write(self.root / "prepared.json", {"label": "failed"})
        with patch.object(f, "verify_prepared", return_value={"command": ["never-executed"]}), \
                patch.object(f.subprocess, "Popen", side_effect=OSError("simulated failure")):
            with self.assertRaisesRegex(OSError, "simulated failure"):
                f.launch(self.root, "failed", True)
        self.assertEqual(f.read(folder / "run.json")["status"], "launch-failed")
        with self.assertRaisesRegex(RuntimeError, "No recorded child"):
            f.capture(self.root, "failed")
        with self.assertRaisesRegex(RuntimeError, "already launched"):
            f.verify_prepared(self.root, "failed")

    def test_successful_child_exit_captures_isolated_profile(self):
        folder = self.root / "results/success"
        profile = self.root / "profile"
        profile.mkdir()
        (profile / "Player.log").write_text("Startup time 10 ms")
        f.write(folder / "run.json", {"status": "prepared-offline", "executable": str(self.root / "game/fake.exe")})
        f.write(self.root / "prepared.json", {"label": "success"})
        with patch.object(f, "verify_prepared", return_value={"command": ["never-executed"]}), \
                patch.object(f, "restore_vehicle_update_timestamp", return_value=None), \
                patch.object(f.subprocess, "Popen") as child:
            child.return_value.pid = 123
            child.return_value.poll.return_value = 0
            child.return_value.wait.return_value = 0
            result = f.launch(self.root, "success", True)
        self.assertTrue(result["hasPlayerLog"])
        captured = f.read(folder / "run.json")
        self.assertEqual(captured["status"], "captured-awaiting-human-and-runtime-review")
        self.assertEqual(captured["exitCode"], 0)
        self.assertEqual((folder / "Player.log").read_bytes(), (profile / "Player.log").read_bytes())

    def test_gagarin_cache_requires_matching_capture_and_recorded_bytes(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods>" +
            "".join("<li>" + pid + "</li>" for pid in active) + "</activeMods></ModsConfigData>")
        receipt = {"sourceRevision": "a"*40}
        f.write(self.root / "candidate.json", receipt)
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/cold/profile"
        profile.mkdir(parents=True)
        foreign_cache = profile / "MissileGirl/cache.bin"
        foreign_cache.parent.mkdir()
        foreign_cache.write_bytes(b"foreign cache")
        (profile / "Saves").mkdir()
        (profile / "Saves/never-copy.rws").write_text("private save")
        (profile / "Config").mkdir()
        (profile / "Config/unrelated.xml").write_text("not a cache")
        run = {"mode": "candidate", "status": "captured-awaiting-human-and-runtime-review",
               "exitCode": 0, "lane": "activation", "manifestSha256": "snapshot", "candidate": receipt,
               "generation": "test", "activeOrder": active[:2] + [f.PACKAGE_ID] + active[2:], "evidence": f.inventory(profile)}
        previous = self.root / "results/cold/run.json"
        manifest = {"activeOrder": active, "official": [{"id": active[2]}], "generation": "test"}
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a"*40), patch.object(f.subprocess, "Popen") as launch:
            for key, value in [("mode", "absent"), ("status", "running"), ("exitCode", 1),
                               ("lane", "representative"), ("candidate", {}), ("manifestSha256", "other"),
                               ("generation", "other")]:
                f.write(previous, dict(run, **{key: value}))
                with self.assertRaisesRegex(RuntimeError, "Foreign cache source must be"):
                    f.prepare(self.root, "candidate", "warm", lane="activation", foreign_cache_from="cold", menu_observer=False, exit_after_menu_ready=False)
                self.assertFalse((self.root / "results/warm").exists())
            f.write(previous, run)
            foreign_stamp = foreign_cache.stat()
            foreign_cache.write_bytes(b"changed cache")
            os.utime(foreign_cache, ns=(foreign_stamp.st_atime_ns, foreign_stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.prepare(self.root, "candidate", "warm", lane="activation", foreign_cache_from="cold", menu_observer=False, exit_after_menu_ready=False)
            self.assertFalse((self.root / "results/warm").exists())
            foreign_cache.write_bytes(b"foreign cache")
            os.utime(foreign_cache, ns=(foreign_stamp.st_atime_ns, foreign_stamp.st_mtime_ns))
            f.prepare(self.root, "candidate", "warm", lane="activation", foreign_cache_from="cold", menu_observer=False, exit_after_menu_ready=False)
            self.assertEqual((self.root / "profile/MissileGirl/cache.bin").read_bytes(), b"foreign cache")
            self.assertFalse((self.root / "profile/Saves").exists())
            self.assertFalse((self.root / "profile/Config/unrelated.xml").exists())
            prepared = f.read(self.root / "results/warm/run.json")
            self.assertEqual(prepared["foreignCacheFrom"], "cold")
            self.assertEqual(prepared["restoredCacheKinds"], ["gagarin-MissileGirl"])
            f.write(previous, dict(run, mode="absent", activeOrder=active))
            f.write(self.root / "candidate.json", {"sourceRevision": "b"*40})
            f.prepare(self.root, "absent", "foreign-only", lane="activation", foreign_cache_from="cold", menu_observer=False, exit_after_menu_ready=False)
            self.assertEqual((self.root / "profile/MissileGirl/cache.bin").read_bytes(), b"foreign cache")
            self.assertFalse((self.root / "profile/Config/WakeUp").exists())
            self.assertFalse((self.root / "game/Mods" / f.PACKAGE_LEAF).exists())
            self.assertEqual(f.read(self.root / "results/foreign-only/run.json")["candidate"]["sourceRevision"], "b"*40)
            launch.assert_not_called()

    def test_existing_startup_impact_summary_preserves_units_and_boundary(self):
        report = self.root / "StartupImpactData.xml"
        self.assertEqual(f.startup_impact_summary(report)["status"], "absent")
        report.write_text('<StartupImpactSession><sessionData><loadingTime>1200.5</loadingTime>'
            '<metrics><keys><li>LoadDefs</li></keys><values><li>50</li></values></metrics>'
            '<offThreadMetrics><keys><li>Parse</li></keys><values><li>100</li></values></offThreadMetrics>'
            '<mods><li><metrics><keys><li>Textures|a</li></keys><values><li>200</li></values></metrics>'
            '<offThreadMetrics><keys><li>Parse|a</li></keys><values><li>30</li></values></offThreadMetrics></li>'
            '<li><metrics><keys><li>Textures|b</li></keys><values><li>300</li></values></metrics></li></mods>'
            '<modsLoaded>315</modsLoaded><defsParsed>50000</defsParsed><patchOperationsApplied>16000</patchOperationsApplied>'
            '</sessionData></StartupImpactSession>')
        summary = f.startup_impact_summary(report)
        self.assertEqual(summary["status"], "recorded")
        self.assertEqual(summary["loadingTimeMs"], 1200.5)
        self.assertEqual(summary["baseGameMetricsMs"], {"LoadDefs": 50})
        self.assertEqual(summary["offThreadMetricsMs"], {"Parse": 100})
        self.assertEqual(summary["modMetricsMs"], {"Textures": 500})
        self.assertEqual(summary["modOffThreadMetricsMs"], {"Parse": 30})
        self.assertEqual([summary[key] for key in ("modsLoaded", "defsParsed", "patchOperationsApplied")], [315, 50000, 16000])
        self.assertEqual(summary["units"], "milliseconds")
        self.assertIn("excludes earlier process/Prepatcher startup", summary["boundaryCaveat"])
        report.write_text('<StartupImpactSession><sessionData><loadingTime>NaN</loadingTime></sessionData></StartupImpactSession>')
        self.assertEqual(f.startup_impact_summary(report)["status"], "unreadable")

    def test_startup_observation_waits_for_complete_block_and_keeps_first_endpoint(self):
        log = self.root / "Player.log"
        observer = f.StartupLogObserver(log, 10)
        observer.sample()
        log.write_bytes(b"noise\nUnloading 17 unused Assets to reduce memory usage.\nTotal: 3 ms (FindLive")
        observer.sample()
        self.assertIsNone(observer.endpoint)
        with log.open("ab") as stream:
            stream.write(b"Objects: 1 ms)\n")
        with patch.object(f.time, "perf_counter", return_value=12.5):
            observer.sample()
        self.assertEqual(observer.endpoint["elapsedSeconds"], 2.5)
        self.assertEqual(len(observer.endpoint["excerpt"]), 2)
        with log.open("ab") as stream:
            stream.write(b"Unloading 20 unused Assets\nTotal: 5 ms (FindLiveObjects: 2 ms)\n")
        observer.sample()
        self.assertEqual(observer.endpoint["elapsedSeconds"], 2.5)

    def test_menu_signal_is_bound_to_current_launch_and_uses_in_game_time(self):
        run = {"menuObserver": True, "menuObserverRunId": "a" * 32, "pid": 123,
               "menuObserverClock": {"frequency": 1000, "startCounter": 10000}}
        path = self.root / "profile/FixtureMenuObserver/menu-ready.json"
        signal = {"schema": "fixture-menu-ready.v1", "event": "menu-ready", "runId": "a" * 32,
                  "processId": 123, "counterFrequency": 1000, "counter": 12500, "elapsedSeconds": 2.5}
        self.assertIsNone(f.menu_ready_signal(self.root, run))
        f.write(path, signal)
        with patch.object(f.time, "perf_counter", return_value=999999):
            self.assertEqual(f.menu_ready_signal(self.root, run), signal)
        for key, value in [("runId", "b" * 32), ("processId", 124), ("counterFrequency", 2000),
                           ("counter", 9000), ("elapsedSeconds", 100.0), ("event", "installed")]:
            f.write(path, dict(signal, **{key: value}))
            self.assertIsNone(f.menu_ready_signal(self.root, run), key)
        path.write_text("{unfinished", encoding="utf-8")
        self.assertIsNone(f.menu_ready_signal(self.root, run))

    def test_menu_observer_is_independent_of_optimizer_in_each_arm(self):
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]}
        absent = f.run_order(manifest, "representative", "absent", True)
        candidate = f.run_order(manifest, "representative", "candidate", True)
        self.assertEqual(absent, ["zetrith.prepatcher", "brrainz.harmony", f.MENU_ID, "ludeon.rimworld"])
        self.assertEqual([item for item in candidate if item != f.PACKAGE_ID], absent)
        self.assertEqual(f.run_arguments(self.root, "absent"), ["-savedatafolder=" + str(self.root / "profile")])

    def test_default_preparation_automates_menu_exit_with_explicit_manual_opt_out(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        seed_prefs = "<PrefsData><runInBackground>False</runInBackground><volumeMaster>0.8</volumeMaster></PrefsData>"
        (seed / "Prefs.xml").write_text(seed_prefs)
        manifest = {"activeOrder": active, "generation": "test"}
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True})
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "git", return_value="a" * 40):
            f.prepare(self.root, "absent", "automatic")
            run = f.read(self.root / "results/automatic/run.json")
            self.assertTrue(run["menuObserver"])
            self.assertTrue(run["exitAfterMenuReady"])
            self.assertEqual(run["profileOverrides"], {"runInBackground": True})
            self.assertEqual(f.ET.parse(self.root / "profile/Config/Prefs.xml").findtext("runInBackground"), "True")
            self.assertEqual(f.ET.parse(self.root / "profile/Config/Prefs.xml").findtext("volumeMaster"), "0.8")
            self.assertEqual((seed / "Prefs.xml").read_text(), seed_prefs)
            f.verify_prepared(self.root, "automatic")
            f.write(self.root / "results/automatic/run.json", dict(run, profileOverrides={}))
            with self.assertRaisesRegex(RuntimeError, "runInBackground"):
                f.verify_prepared(self.root, "automatic")
            self.assertIn(f.MENU_ID, run["activeOrder"])
            self.assertNotIn(f.PACKAGE_ID, run["activeOrder"])
            self.assertIn("--fixture-exit-after-menu-ready", run["arguments"])
            self.assertFalse(any(arg.startswith("--wake-up-") for arg in run["arguments"]))
            f.prepare(self.root, "absent", "manual", exit_after_menu_ready=False)
            manual = f.read(self.root / "results/manual/run.json")
            self.assertEqual(manual["profileOverrides"], {})
            self.assertEqual((self.root / "profile/Config/Prefs.xml").read_text(), seed_prefs)
            self.assertTrue(manual["menuObserver"])
            self.assertNotIn("--fixture-exit-after-menu-ready", manual["arguments"])
            with self.assertRaisesRegex(RuntimeError, "requires the menu observer"):
                f.prepare(self.root, "absent", "invalid", menu_observer=False)
            f.write(self.root / "menu-observer.json", {})
            with self.assertRaisesRegex(RuntimeError, "automatic exit support"):
                f.prepare(self.root, "absent", "old-observer")

    def test_automatic_launch_requires_menu_signal_and_clean_exit_after_capture(self):
        for label, signal, exit_code in [("good", {"elapsedSeconds": 2.5}, 0),
                                          ("missing", None, 0), ("crashed", {"elapsedSeconds": 2.5}, 1)]:
            with self.subTest(label=label):
                folder = self.root / "results" / label
                (self.root / "profile").mkdir(exist_ok=True)
                f.write(folder / "run.json", {"status": "prepared-offline", "menuObserver": True,
                        "exitAfterMenuReady": True, "executable": str(self.root / "game/fake.exe")})
                f.write(self.root / "prepared.json", {"label": label})
                with patch.object(f, "verify_prepared", return_value={"command": ["never-executed"]}), \
                        patch.object(f, "start_menu_clock"), patch.object(f, "menu_ready_signal", return_value=signal), \
                        patch.object(f, "restore_vehicle_update_timestamp", return_value=None), \
                        patch.object(f.subprocess, "Popen") as child:
                    child.return_value.pid = 123
                    child.return_value.poll.return_value = exit_code
                    child.return_value.wait.return_value = exit_code
                    if label == "good":
                        f.launch(self.root, label, True)
                    else:
                        with self.assertRaisesRegex(RuntimeError, "Automatic test failed"):
                            f.launch(self.root, label, True)
                    child.return_value.terminate.assert_not_called()
                    child.return_value.kill.assert_not_called()
                captured = f.read(folder / "run.json")
                self.assertEqual(captured["menuReadyObservation"], signal)
                self.assertEqual(captured["exitCode"], exit_code)
                self.assertEqual(captured["automaticTestPassed"], label == "good")
                self.assertTrue((folder / "profile").is_dir())

    def test_automation_timeout_leaves_child_running_and_records_failure(self):
        for label, signal, now in [("no-menu", None, 1801), ("no-exit", {"elapsedSeconds": 2.5}, 65)]:
            folder = self.root / "results" / label
            f.write(folder / "run.json", {"status": "prepared-offline", "menuObserver": True,
                    "exitAfterMenuReady": True, "executable": str(self.root / "game/fake.exe")})
            with patch.object(f, "verify_prepared", return_value={"command": ["never-executed"]}), \
                    patch.object(f, "start_menu_clock"), patch.object(f, "menu_ready_signal", return_value=signal), \
                    patch.object(f.time, "perf_counter", side_effect=[0, now]), \
                    patch.object(f.subprocess, "Popen") as child:
                child.return_value.pid = 123
                child.return_value.poll.return_value = None
                with self.assertRaisesRegex(RuntimeError, "game left running"):
                    f.launch(self.root, label, True)
                child.return_value.terminate.assert_not_called()
                child.return_value.kill.assert_not_called()
            run = f.read(folder / "run.json")
            self.assertEqual(run["status"], "running")
            self.assertIn("automationFailure", run)
            self.assertNotIn("exitCode", run)

    def test_timing_arguments_never_add_optimizer_to_absent_run(self):
        self.assertEqual(f.run_arguments(self.root, "absent"), ["-savedatafolder=" + str(self.root / "profile")])
        for mode in ("baseline", "candidate"):
            args = f.run_arguments(self.root, mode)
            self.assertIn("--wake-up-strategy=startup-searches", args)
            self.assertIn("--wake-up-mode=" + mode, args)
        for strategy in ("threads", "catalog", "indexed-xml", "buffered-dds", "character-presets"):
            with self.assertRaisesRegex(RuntimeError, "Unknown optimization strategy"):
                f.run_arguments(self.root, "candidate", strategy=strategy)
        with self.assertRaisesRegex(RuntimeError, "Unknown Gagarin"):
            f.run_arguments(self.root, "candidate", gagarin_cache="profile")

    def test_giddy_textures_are_optional_and_candidate_only(self):
        self.assertNotIn("--wake-up-giddy-textures=on", f.run_arguments(self.root, "candidate"))
        for selection in ("on", "timing", "verify"):
            self.assertIn("--wake-up-giddy-textures=" + selection, f.run_arguments(self.root, "candidate", giddy_textures=selection))
        for mode in ("baseline", "absent"):
            with self.assertRaises(RuntimeError):
                f.run_arguments(self.root, mode, giddy_textures="on")
        with self.assertRaises(RuntimeError):
            f.run_arguments(self.root, "candidate", giddy_textures="unknown")

    def test_character_presets_are_optional_and_candidate_only(self):
        self.assertNotIn("--wake-up-character-presets=on", f.run_arguments(self.root, "candidate"))
        for selection in ("on", "timing"):
            self.assertIn("--wake-up-character-presets=" + selection,
                          f.run_arguments(self.root, "candidate", character_presets=selection))
        for mode in ("absent", "baseline"):
            with self.assertRaisesRegex(RuntimeError, "Character Editor presets require"):
                f.run_arguments(self.root, mode, character_presets="on")
        with self.assertRaisesRegex(RuntimeError, "Unknown Character Editor"):
            f.run_arguments(self.root, "candidate", character_presets="unknown")

    def test_git_guard_rejects_force_staged_payload(self):
        repo = self.base / "repo"
        repo.mkdir()
        subprocess.run(["git", "init", "-q", str(repo)], check=True)
        (repo / ".gitignore").write_text(".rlo-test-instance/\n")
        guard = Path(__file__).parents[1] / "build/Verify-FixtureBoundary.ps1"
        command = ["pwsh", "-NoProfile", "-File", str(guard), "-RepositoryRoot", str(repo)]
        self.assertEqual(subprocess.run(command, capture_output=True).returncode, 0)
        payload = repo / ".rlo-test-instance/game/fake.txt"
        payload.parent.mkdir(parents=True)
        payload.write_text("synthetic guard fixture")
        subprocess.run(["git", "-C", str(repo), "add", "-f", str(payload)], check=True)
        self.assertNotEqual(subprocess.run(command, capture_output=True).returncode, 0)

    def test_steam_download_counters_do_not_define_snapshot_identity(self):
        a = {"steamBuildId": "123", "steamAssemblySha256": "same", "steamAppManifestSha256": "old"}
        b = dict(a, steamAppManifestSha256="new")
        self.assertEqual(f.source_identity(a), f.source_identity(b))
        self.assertNotEqual(f.source_identity(a), f.source_identity(dict(b, steamBuildId="124")))


if __name__ == "__main__":
    unittest.main()
