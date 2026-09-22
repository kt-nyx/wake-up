# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Focused offline checks; never use the canonical game or profile."""
import importlib.util
import os
import shutil
import types
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("fixture", Path(__file__).parents[1] / "scripts/fixture.py")
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)


class FixtureTests(unittest.TestCase):
    def test_reviewed_steam_core_binding_keeps_exact_binary_and_reference_target(self):
        source = self.base / "steam-core"
        (source / "Assemblies").mkdir(parents=True)
        (source / "About").mkdir()
        (source / "Assemblies/WakeUp.dll").write_bytes(b"reviewed steam core")
        (source / "About/About.xml").write_bytes(b"private metadata")
        template = self.base / "validation/fixture-package/About/About.xml"
        template.parent.mkdir(parents=True)
        template.write_bytes(b"private metadata")
        actual_digest = f.digest
        def reviewed_digest(path):
            path = Path(path)
            if path.name == "WakeUp.dll" and path.read_bytes() == b"reviewed steam core":
                return "a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda"
            return actual_digest(path)
        contract = {"target": "gog-rev573", "gameAssemblySha256": f.DEVELOPMENT_GAME}
        with patch.object(f, "REPO", self.base), patch.object(f, "runtime_contract", return_value=contract), \
                patch.object(f, "digest", side_effect=reviewed_digest):
            origin = dict(sourceRevision="67265e5696c80c9a461423722870a742054dd43b",
                          referenceTarget="steam-rev590", files=f.inventory(source))
            for revision in ("a" * 40, "d584eb36aa68cd374a26621f97295dbb63626a33"):
                f.write(source.with_suffix(".json"), dict(origin, sourceRevision=revision))
                with self.assertRaisesRegex(RuntimeError, "Wrong accepted"):
                    f.bind_comparison_package(self.root, source)
            f.write(source.with_suffix(".json"), origin)
            (source / "Assemblies/WakeUp.dll").write_bytes(b"changed core")
            with self.assertRaises(RuntimeError): f.bind_comparison_package(self.root, source)
            (source / "Assemblies/WakeUp.dll").write_bytes(b"reviewed steam core")
            f.write(source.with_suffix(".json"), dict(origin, files=f.inventory(source)))
            result = f.bind_comparison_package(self.root, source)
            receipt = f.read(Path(result["package"]).with_suffix(".json"))
            self.assertEqual(receipt["referenceTarget"], "steam-rev590")
            self.assertEqual(receipt["runtimeContract"], contract)
            self.assertEqual((Path(result["package"]) / "Assemblies/WakeUp.dll").read_bytes(), b"reviewed steam core")

    def test_steam_scene_carry_requires_exact_save_direct_parent_and_content(self):
        label = "c15a-owner-scene-setup-09"
        folder = self.root / "results" / label
        (folder / "profile/Saves").mkdir(parents=True)
        (folder / "profile/Saves/RloFixtureSmoke.rws").write_bytes(b"accepted scene")
        f.write(folder / "profile/FixtureMenuObserver/gameplay-smoke.json", {"passed": True})
        actual_digest = f.digest
        save_sha = "e949173b778500b82bd8dfb65621b830ccb936cd013ad74e5396cdf33440f3e6"
        def approved_digest(path):
            return save_sha if Path(path).name == "RloFixtureSmoke.rws" else actual_digest(path)
        parent = {"generation": "gog-parent", "manifestSha256": "parent-hash"}
        state = {"generation": "steam-child", "manifestSha256": "child-hash"}
        m = {"frozenParent": parent}
        with patch.object(f, "digest", side_effect=approved_digest), \
                patch.object(f, "current", return_value=(m, None, state)), \
                patch.object(f, "runtime_contract", return_value={"target": "steam-rev590"}):
            run = dict(parent, lane="representative", status="captured-awaiting-human-and-runtime-review",
                       exitCode=0, exitAfterMenuReady=True, automaticTestPassed=True, gameplaySmoke=True,
                       gameplayTestPassed=True, activeOrder=["ludeon.rimworld"], excludedMods=[],
                       evidence=f.inventory(folder / "profile"))
            f.write(folder / "run.json", run)
            args = (self.root, label, "representative", "steam-child", "child-hash", ["ludeon.rimworld"], [])
            _, receipt = f.gameplay_save_source(*args)
            self.assertEqual(receipt["platformTransition"], {"from": parent, "to": state,
                "policy": "Exact accepted fixture scene across approved Steam staging"})
            for changes in (dict(activeOrder=["changed.mod"]), dict(manifestSha256="wrong-parent"),
                            dict(automaticTestPassed=False), dict(excludedMods=[{"id": "changed.mod"}])):
                f.write(folder / "run.json", dict(run, **changes))
                with self.assertRaises(RuntimeError): f.gameplay_save_source(*args)
            f.write(folder / "run.json", run)
            with patch.object(f, "runtime_contract", return_value={"target": "gog-rev573"}):
                with self.assertRaises(RuntimeError): f.gameplay_save_source(*args)
            (folder / "profile/Saves/RloFixtureSmoke.rws").write_bytes(b"corrupted scene")
            with self.assertRaises(RuntimeError): f.gameplay_save_source(*args)

    def test_reviewed_retained_archive_binding_is_exact_and_keeps_build_provenance(self):
        source = self.base / "accepted"
        (source / "Assemblies").mkdir(parents=True)
        (source / "About").mkdir()
        (source / "Assemblies/WakeUp.dll").write_bytes(b"accepted core")
        (source / "About/About.xml").write_bytes(b"public metadata")
        template = self.base / "validation/fixture-package/About/About.xml"
        template.parent.mkdir(parents=True)
        template.write_bytes(b"private metadata")
        origin = {"sourceRevision": "ed36d07cb9135f335c74ae454b1d85ea32a0087a",
                  "buildSourceRevision": "36d739b1497c576e74e337e224bb1712f2337661",
                  "version": "0.3.0-rc.2", "buildReferenceTarget": "gog-rev573",
                  "files": {"Assemblies/WakeUp.dll": "6cdf5fe145760de3c7570a3c0ad5ef8f139d57d32e20a97cf6b4d05b26d74b07",
                            "About/About.xml": f.digest(source / "About/About.xml")}}
        manifest = source / "release-manifest.json"
        f.write(manifest, origin)
        actual_digest = f.digest
        def approved_digest(path):
            path = Path(path)
            if path == manifest:
                return "a175b2f846a7c67df0ba76f397320a236b4570c7103bf225a02ebd9110081adf"
            if path.name == "WakeUp.dll" and path.read_bytes() == b"accepted core":
                return origin["files"]["Assemblies/WakeUp.dll"]
            return actual_digest(path)
        contract = {"target": "steam-rev590", "gameAssemblySha256": f.STEAM_GAME}
        with patch.object(f, "REPO", self.base), patch.object(f, "runtime_contract", return_value=contract), \
                patch.object(f, "digest", side_effect=approved_digest):
            for key, bad in (("sourceRevision", "a" * 40), ("buildSourceRevision", "b" * 40), ("version", "0.3.0")):
                f.write(manifest, dict(origin, **{key: bad}))
                with self.assertRaisesRegex(RuntimeError, "Only the assigned"):
                    f.bind_comparison_package(self.root, source)
            f.write(manifest, origin)
            (source / "Assemblies/WakeUp.dll").write_bytes(b"unreviewed core")
            with self.assertRaisesRegex(RuntimeError, "content drift"):
                f.bind_comparison_package(self.root, source)
            (source / "Assemblies/WakeUp.dll").write_bytes(b"accepted core")
            (source / "About/About.xml").write_bytes(b"changed metadata")
            with self.assertRaisesRegex(RuntimeError, "content drift"):
                f.bind_comparison_package(self.root, source)
            (source / "About/About.xml").write_bytes(b"public metadata")
            result = f.bind_comparison_package(self.root, source)
            receipt = f.read(Path(result["package"]).with_suffix(".json"))
            self.assertEqual(receipt["runtimeContract"], contract)
            self.assertEqual(receipt["referenceTarget"], "gog-rev573")
            self.assertEqual(receipt["comparisonProjection"]["originalBuildSourceRevision"], origin["buildSourceRevision"])
            self.assertEqual((Path(result["package"]) / "Assemblies/WakeUp.dll").read_bytes(), b"accepted core")
        with patch.object(f, "runtime_contract", return_value=contract):
            with self.assertRaisesRegex(RuntimeError, "Only the assigned"):
                f.bind_comparison_package(self.root, source)

    def test_raw_helper_trial_is_explicit_fixture_only_and_manifest_bound(self):
        trial = {"path": "Tools/win-x64/WakeUp.RawPixelTrial.exe", "sha256": "1" * 64}
        f.write(self.root / "candidate.json", {"rawPixelHelper": trial})
        options = dict(mode="candidate", png="first-build", exit_after_menu_ready=True)
        ordinary = f.run_arguments(self.root, **options)
        enabled = f.run_arguments(self.root, **options, raw_pixel_helper=True)
        flag = "--fixture-raw-pixel-helper=" + trial["sha256"]
        self.assertNotIn(flag, ordinary)
        self.assertEqual(enabled.count(flag), 1)
        for changes in (dict(activation="user"), dict(mode="absent"), dict(png="off"), dict(exit_after_menu_ready=False)):
            with self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict(options, raw_pixel_helper=True, **changes))

    def test_first_build_selector_is_explicit_and_manifest_bound(self):
        flag = "--wake-up-first-build-images=on"
        ordinary = f.run_arguments(self.root, "candidate", png="cache", png_source="original")
        combined = f.run_arguments(self.root, "candidate", png="cache", png_source="original", first_build_images=True)
        self.assertNotIn(flag, ordinary)
        self.assertEqual(combined.count(flag), 1)
        self.assertEqual([a for a in combined if a != flag], ordinary)
        for changes in ({"activation": "user"}, {"mode": "absent"}, {"png": "off"}, {"first_build_images": "on"}):
            with self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict({"mode": "candidate", "png": "cache", "first_build_images": True}, **changes))
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        (seed / "Prefs.xml").write_text("<PrefsData><runInBackground>False</runInBackground></PrefsData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        f.write(self.root / "candidate.json", {"sourceRevision": "a" * 40})
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True})
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40):
            f.prepare(self.root, "candidate", "combined", png="cache", png_source="original", first_build_images=True)
            self.assertIn(flag, f.verify_prepared(self.root, "combined")["command"])
            path = self.root / "results/combined/run.json"
            run = f.read(path)
            self.assertIs(run["firstBuildImages"], True)
            f.write(path, dict(run, firstBuildImages=False))
            with self.assertRaisesRegex(RuntimeError, "command|arguments|Arguments|Command"):
                f.verify_prepared(self.root, "combined")

    def test_raw_helper_saved_selector_cannot_drift(self):
        gen = self.root / 'snapshots/test'
        seed = gen / 'seed/Config'
        seed.mkdir(parents=True)
        (seed / 'ModsConfig.xml').write_text('<ModsConfigData><activeMods /></ModsConfigData>')
        (seed / 'Prefs.xml').write_text('<PrefsData><runInBackground>False</runInBackground></PrefsData>')
        manifest = {'activeOrder': ['zetrith.prepatcher', 'brrainz.harmony', 'ludeon.rimworld'], 'generation': 'test'}
        f.write(self.root / 'candidate.json', {'sourceRevision': 'a' * 40,
            'rawPixelHelper': {'path': 'Tools/win-x64/WakeUp.RawPixelTrial.exe', 'sha256': '1' * 64}})
        (self.root / 'game/Mods' / f.PACKAGE_LEAF).mkdir(parents=True)
        f.write(self.root / 'menu-observer.json', {'supportsAutomaticExit': True})
        with patch.object(f, 'audit', return_value={'deployedRuntimeMatches': True}), \
                patch.object(f, 'current', return_value=(manifest, gen, {'manifestSha256': 'snapshot'})), \
                patch.object(f, 'candidate_matches_snapshot', return_value=True), \
                patch.object(f, 'git', return_value='a' * 40):
            f.prepare(self.root, 'candidate', 'raw', png='first-build', raw_pixel_helper=True)
            self.assertIn('--fixture-raw-pixel-helper=' + '1' * 64, f.verify_prepared(self.root, 'raw')['command'])
            path = self.root / 'results/raw/run.json'
            run = f.read(path)
            self.assertIs(run['rawPixelHelper'], True)
            f.write(path, dict(run, rawPixelHelper=False))
            with self.assertRaisesRegex(RuntimeError, 'command|arguments|Arguments|Command'):
                f.verify_prepared(self.root, 'raw')

    def test_c14_requires_functional_isolated_native_phase(self):
        options = dict(mode="candidate", purpose="functional", activation="user", exit_after_menu_ready=True,
                       c14_background="save-true-to-false-unpaused", gameplay_save_from="c13-final-shown-03")
        args = f.run_arguments(self.root, **options)
        self.assertIn("--fixture-c14-background=save-true-to-false-unpaused", args)
        for changes in (dict(purpose="performance"), dict(gameplay_smoke=True), dict(world_background=True),
                        dict(c14_background="arbitrary-event"), dict(gameplay_save_from="../normal-save")):
            with self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict(options, **changes))

    def test_audio_absent_measurement_requires_physical_bootstrap_absence(self):
        bootstrap = f.fixture_audio_bootstrap
        with patch.object(bootstrap, "environment"):
            self.assertIsNone(bootstrap.admit(self.root, "performance", False, f, "absent"))
            for path in [self.root / "audio-bootstrap.json"] + [self.root / "game" / name for name in bootstrap.OWNED]:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"must not be present")
                with self.assertRaisesRegex(RuntimeError, "physical absence"):
                    bootstrap.admit(self.root, "performance", False, f, "absent")
                path.unlink()
            with self.assertRaisesRegex(RuntimeError, "cannot select"):
                bootstrap.admit(self.root, "performance", True, f, "absent")

    def test_audio_absent_measurement_rejects_installed_flags_and_profiler_arguments(self):
        run = dict(audioMeasurement="absent", purpose="performance", mode="candidate", menuObserver=True,
                   exitAfterMenuReady=True, gameplaySmoke=True)
        f.validate_audio_measurement(run, None)
        for changes in ({"audioBootstrapProbe": True}, {"audioNativeEntryProbe": True},
                        {"arguments": ["-monoProfiler", "wakeupentry"]}, {"audioCacheFrom": "cold"}):
            with self.assertRaises(RuntimeError):
                f.validate_audio_measurement(dict(run, **changes), None)
        with self.assertRaises(RuntimeError):
            f.validate_audio_measurement(run, {"nativeEntry": True})
        for selected in ("off", "cold", "warm"):
            installed = dict(run, audioMeasurement=selected, audioBootstrapProbe=True, audioNativeEntryProbe=True,
                             audioCacheFrom="cold" if selected == "warm" else None)
            f.validate_audio_measurement(installed, {"nativeEntry": True})
            with self.assertRaises(RuntimeError):
                f.validate_audio_measurement(installed, None)

    def test_experimental_preloader_requires_functional_bootstrap_probe(self):
        bootstrap = f.fixture_audio_bootstrap
        with patch.object(bootstrap, "verify", return_value={"session": "test"}), \
                patch.object(bootstrap, "environment"):
            for purpose, probe in (("performance", True), ("functional", False)):
                with self.assertRaisesRegex(RuntimeError, "functional bootstrap probe"):
                    bootstrap.admit(self.root, purpose, probe, f)
            self.assertEqual(bootstrap.admit(self.root, "functional", True, f), {"session": "test"})

    def test_native_device_observer_rejects_performance_prepare_and_launch(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        (seed / "Prefs.xml").write_text("<PrefsData><runInBackground>False</runInBackground></PrefsData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True, "nativeDeviceProbe": {"sha256": "probe"}})
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "git", return_value="a" * 40):
            with self.assertRaisesRegex(RuntimeError, "requires functional automatic exit"):
                f.prepare(self.root, "absent", "refused")
            self.assertFalse((self.root / "prepared.json").exists())
            f.prepare(self.root, "absent", "native", purpose="functional")
            f.verify_prepared(self.root, "native")
            path = self.root / "results/native/run.json"
            f.write(path, dict(f.read(path), purpose="performance"))
            with self.assertRaisesRegex(RuntimeError, "requires functional automatic exit"):
                f.verify_prepared(self.root, "native")

    def test_stream_probe_is_functional_only_and_command_bound(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        (seed / "Prefs.xml").write_text("<PrefsData><runInBackground>False</runInBackground></PrefsData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True, "supportsAudioStreamProbe": True})
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "git", return_value="a" * 40):
            with self.assertRaisesRegex(RuntimeError, "requires functional automatic observation"):
                f.prepare(self.root, "absent", "refused", audio_stream_probe=True)
            for alternate_exit in ("gameplay_smoke", "world_background", "language_lifecycle"):
                with self.assertRaisesRegex(RuntimeError, "ordinary menu exit"):
                    f.prepare(self.root, "absent", "refused-exit", purpose="functional", audio_stream_probe=True, **{alternate_exit: True})
            self.assertFalse((self.root / "prepared.json").exists())
            f.prepare(self.root, "absent", "stream", purpose="functional", audio_stream_probe=True)
            verified = f.verify_prepared(self.root, "stream")
            self.assertIn("--fixture-c10-stream-probe", verified["command"])
            path = self.root / "results/stream/run.json"
            f.write(path, dict(f.read(path), purpose="performance"))
            with self.assertRaisesRegex(RuntimeError, "Audio stream probe purpose drift"):
                f.verify_prepared(self.root, "stream")

    def test_bootstrap_probe_requires_core_functional_exit_and_binds_receipt_capability(self):
        for mode, changes in (("absent", {}), ("baseline", {"purpose": "performance"}),
                              ("baseline", {"exit_after_menu_ready": False}),
                              ("baseline", {"audio_stream_probe": True}),
                              ("baseline", {"gameplay_smoke": True})):
            options = dict(purpose="functional", audio_bootstrap_probe=True)
            options.update(changes)
            with self.assertRaisesRegex(RuntimeError, "Audio bootstrap probe requires"):
                f.prepare(self.root, mode, "refused-bootstrap", **options)
        self.assertFalse((self.root / "prepared.json").exists())
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        (seed / "Prefs.xml").write_text("<PrefsData><runInBackground>False</runInBackground></PrefsData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        f.write(self.root / "candidate.json", {"sourceRevision": "a" * 40})
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True})
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40):
            with self.assertRaisesRegex(RuntimeError, "Rebuild observer for audio bootstrap"):
                f.prepare(self.root, "baseline", "missing-bootstrap-capability", purpose="functional", audio_bootstrap_probe=True)
            f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True, "supportsAudioBootstrapProbe": True})
            f.prepare(self.root, "baseline", "bootstrap", purpose="functional", audio_bootstrap_probe=True)
            verified = f.verify_prepared(self.root, "bootstrap")
            self.assertIn("--fixture-c10-bootstrap-probe", verified["command"])
            path = self.root / "results/bootstrap/run.json"
            original = f.read(path)
            self.assertIs(original["audioBootstrapProbe"], True)
            for changes in ({"purpose": "performance"}, {"mode": "absent"}, {"audioStreamProbe": True}):
                f.write(path, dict(original, **changes))
                with self.assertRaises(RuntimeError):
                    f.verify_prepared(self.root, "bootstrap")
            f.write(path, original)

            with self.assertRaisesRegex(RuntimeError, "Native entry probe requires"):
                f.prepare(self.root, "baseline", "native-without-package", purpose="functional",
                          audio_bootstrap_probe=True, audio_native_entry_probe=True)
            with patch.object(f.fixture_audio_bootstrap, "admit", return_value={"nativeEntry": True}):
                with self.assertRaisesRegex(RuntimeError, "explicit native entry probe"):
                    f.prepare(self.root, "baseline", "native-without-selection", purpose="functional", audio_bootstrap_probe=True)
                f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True,
                    "supportsAudioBootstrapProbe": True, "supportsAudioNativeEntryProbe": True})
                f.prepare(self.root, "baseline", "native-entry", purpose="functional",
                          audio_bootstrap_probe=True, audio_native_entry_probe=True)
                command = f.verify_prepared(self.root, "native-entry")["command"]
                at = command.index("-monoProfiler")
                self.assertEqual(command[at:at + 3], ["-monoProfiler", "wakeupentry", "--fixture-c10-native-entry-probe"])
                native_path = self.root / "results/native-entry/run.json"
                f.write(native_path, dict(f.read(native_path), audioNativeEntryProbe=False))
                with self.assertRaisesRegex(RuntimeError, "Native entry probe package binding drift"):
                    f.verify_prepared(self.root, "native-entry")
                f.prepare(self.root, "baseline", "native-stop", purpose="functional",
                          audio_bootstrap_probe=True, audio_native_entry_probe=True, audio_native_entry_stop_only=True)
                self.assertIn("--fixture-c10-native-entry-stop-only", f.verify_prepared(self.root, "native-stop")["command"])
                stop_path = self.root / "results/native-stop/run.json"
                f.write(stop_path, dict(f.read(stop_path), audioPreparedProbe="warm"))
                with self.assertRaisesRegex(RuntimeError, "Native stop-only probe binding drift"):
                    f.verify_prepared(self.root, "native-stop")
                f.prepare(self.root, "baseline", "native-domain", purpose="functional",
                          audio_bootstrap_probe=True, audio_native_entry_probe=True, audio_native_entry_domain_probe=True)
                self.assertIn("--fixture-c10-native-entry-domain-probe", f.verify_prepared(self.root, "native-domain")["command"])
                domain_path = self.root / "results/native-domain/run.json"
                f.write(domain_path, dict(f.read(domain_path), audioNativeEntryStopOnly=True))
                with self.assertRaisesRegex(RuntimeError, "Native domain probe binding drift"):
                    f.verify_prepared(self.root, "native-domain")

    def test_observer_artwork_is_bounded_to_exact_fixture_sources(self):
        source = self.root / "game/source.png"
        source.parent.mkdir(parents=True)
        source.write_bytes(b"private artwork")
        specification = self.base / "artifacts/artwork.json"
        specification.parent.mkdir()
        row = {"source": str(source), "target": "Textures/UI/icon.png", "sha256": f.digest(source)}
        with patch.object(f, "REPO", self.base):
            f.write(specification, [row])
            self.assertEqual(f.observer_artwork(self.root, specification), [row])
            for changes in ({"target": "Textures/../outside.png"}, {"target": "Textures/name:stream.png"},
                            {"sha256": "0" * 64}, {"source": str(self.base / "outside.png")}):
                f.write(specification, [dict(row, **changes)])
                with self.assertRaises((RuntimeError, OSError)):
                    f.observer_artwork(self.root, specification)

    def gog_recovery_case(self, parked=False):
        """Tiny real manifests/journals exercise both transitions without a game."""
        game = self.root / "game"
        (game / "RimWorldWin64_Data/Managed").mkdir(parents=True)
        assembly = game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
        assembly.write_bytes(b"gog assembly")
        gogsha = f.digest(assembly)
        (game / "RimWorldWin64.exe").write_bytes(b"gog executable")
        (game / "goggame-1094900565.info").write_bytes(b"gog marker")
        (game / "Version.txt").write_text("1.6.4871 rev573")
        (game / "Data/Core/About").mkdir(parents=True)
        (game / "Data/Core/About/About.xml").write_text("<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId></ModMetaData>")
        (game / "Mods").mkdir()
        mod = self.root / "parked-mods/content"
        (mod / "About").mkdir(parents=True)
        (mod / "About/About.xml").write_text("<ModMetaData><name>Content</name><packageId>test.content</packageId></ModMetaData>")
        gen = self.root / "snapshots/gog-original"
        (gen / "seed/Config").mkdir(parents=True)
        (gen / "seed/Config/ModsConfig.xml").write_text("<ModsConfigData><activeMods><li>ludeon.rimworld</li><li>test.content</li></activeMods></ModsConfigData>")
        (gen / "seed/Config/Prefs.xml").write_bytes(b"frozen prefs")
        f.write(gen / "inventories/game.json", f.inventory(game, ("Mods",)))
        f.write(gen / "inventories/profile.json", f.inventory(gen / "seed"))
        f.write(gen / "inventories/official-Core.json", f.inventory(game / "Data/Core"))
        f.write(gen / "inventories/content.json", f.inventory(mod))
        pkg = dict(f.package(mod, "local"), leaf="content", fixture=str(game / "Mods/content"),
                   inventory="inventories/content.json", inventorySha256=f.digest(gen / "inventories/content.json"))
        official = dict(f.package(game / "Data/Core", "official"), leaf="Core", inventory="inventories/official-Core.json",
                        inventorySha256=f.digest(gen / "inventories/official-Core.json"))
        manifest = dict(schema="rlo-drm-fixture.v1", canonicalRoot=str(self.root), generation=gen.name,
                        packages=[pkg], official=[official], activeOrder=["ludeon.rimworld", "test.content"],
                        gameInventorySha256=f.digest(gen / "inventories/game.json"),
                        profileInventorySha256=f.digest(gen / "inventories/profile.json"),
                        developmentRuntimePolicy="owner-approved-fixed-GOG-rev573", developmentRuntimeSha256=gogsha,
                        acceptedCandidateRuntimeMatches=False)
        f.write(gen / "manifest.json", manifest)
        state = dict(generation=gen.name, manifestSha256=f.digest(gen / "manifest.json"))
        f.write(self.root / "current.json", state)
        f.write(self.root / "excluded-mods.json", dict(schema="rlo-excluded-mods.v1", **state, packages=[
            dict(id=pkg["id"], original=pkg["fixture"], parked=str(mod), inventorySha256=pkg["inventorySha256"])]))
        shutil.copytree(gen / "seed", self.root / "profile")
        source = self.base / "Steam"
        shutil.copytree(game, source)
        (source / "goggame-1094900565.info").unlink()
        (source / "steam-only.dll").write_bytes(b"Steam only")
        (source / "Version.txt").write_text("1.6.4871 rev590")
        (source / "RimWorldWin64.exe").write_bytes(b"steam executable")
        (source / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll").write_bytes(b"steam assembly")
        steamsha = f.digest(source / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")
        for name, value in (("DEVELOPMENT_GAME", gogsha), ("STEAM_GAME", steamsha),
                            ("GAME_TARGETS", {"gog-rev573": gogsha, "steam-rev590": steamsha})):
            mock = patch.object(f, name, value)
            mock.start()
            self.addCleanup(mock.stop)
        mock = patch.object(f, "git", return_value="a" * 40)
        mock.start()
        self.addCleanup(mock.stop)
        transition = f.stage_steam(self.root, source)
        candidate = self.root / "parked-rlo/package" if parked else game / "Mods" / f.PACKAGE_LEAF
        (candidate / "Tools").mkdir(parents=True)
        (candidate / "Tools/helper").write_bytes(b"later steam helper")
        f.write(self.root / "candidate.json", dict(generation=transition["generation"], files=f.inventory(candidate)))
        observer = game / "Mods" / f.MENU_LEAF
        observer.mkdir()
        (observer / "observer.dll").write_bytes(b"later steam observer")
        f.write(self.root / "menu-observer.json", dict(generation=transition["generation"], gameSha256=steamsha, files=f.inventory(observer)))
        f.write(self.root / "prepared.json", {"generation": transition["generation"], "stale": True})
        (self.root / "profile/save").write_bytes(b"later steam profile")
        f.write(self.root / "results/retained/run.json", {"retained": True})
        # A later committed operation must remain preserved; old rollback is forbidden.
        f.write(self.root / "later-input.json", {"later": True})
        f.swap(self.root, [(self.root / "later-input.json", self.root / "later.json")], "later-operation")
        f.audit(self.root)
        return transition, candidate

    def test_gog_transition_preserves_recoveries_exclusions_and_removes_stale_admission(self):
        transition, candidate = self.gog_recovery_case(parked=True)
        recovery = self.root / "recovery" / transition["transaction"]
        original = f.inventory(recovery)
        steam_profile = f.inventory(self.root / "profile")
        old_state = f.read(self.root / "current.json")
        with self.assertRaisesRegex(RuntimeError, "latest"):
            f.rollback(self.root, transition["transaction"])
        result = f.stage_gog(self.root, transition["transaction"])
        self.assertEqual(f.inventory(recovery), original)
        self.assertEqual(result["runtimeContract"]["target"], "gog-rev573")
        self.assertTrue(result["matchingBuildsRequired"])
        self.assertFalse((self.root / "game/steam-only.dll").exists())
        self.assertEqual((self.root / "game/RimWorldWin64.exe").read_bytes(), b"gog executable")
        self.assertEqual(f.read(self.root / "results/retained/run.json"), {"retained": True})
        self.assertTrue((self.root / "later.json").exists())
        for name in ("candidate.json", "menu-observer.json", "prepared.json", "parked-rlo/package",
                     "game/Mods/" + f.PACKAGE_LEAF, "game/Mods/" + f.MENU_LEAF):
            self.assertFalse((self.root / name).exists(), name)
        self.assertFalse(f.candidate_matches_snapshot(self.root, *f.current(self.root)[:2]))
        with self.assertRaises(FileNotFoundError):
            f.verify_prepared(self.root, "retained")
        self.assertFalse((self.root / "profile/save").exists())
        self.assertEqual(f.read(self.root / "excluded-mods.json")["generation"], result["generation"])
        f.rollback(self.root, result["transaction"])
        self.assertEqual(f.read(self.root / "current.json"), old_state)
        self.assertEqual(f.inventory(self.root / "profile"), steam_profile)
        self.assertEqual((candidate / "Tools/helper").read_bytes(), b"later steam helper")
        self.assertTrue((self.root / "game/steam-only.dll").exists())
        self.assertTrue((self.root / "prepared.json").exists())
        f.audit(self.root)

    def test_gog_transition_refuses_bad_relationship_inventory_and_journal_before_swap(self):
        transition, _ = self.gog_recovery_case()
        gen = self.root / "snapshots" / transition["generation"]
        recovery = self.root / "recovery" / transition["transaction"]
        journal = f.read(recovery / "committed.json")
        original = f.read(gen / "transition.json")
        f.write(gen / "transition.json", dict(original, transaction="wrong"))
        with self.assertRaisesRegex(RuntimeError, "relationship"), patch.object(f, "swap") as swap:
            f.stage_gog(self.root, transition["transaction"])
        swap.assert_not_called()
        f.write(gen / "transition.json", original)
        inventory = self.root / "snapshots/gog-original/inventories/game.json"
        original_inventory = inventory.read_bytes()
        inventory.write_bytes(b"[]\n")
        with self.assertRaisesRegex(RuntimeError, "inventory identity drift"), patch.object(f, "swap") as swap:
            f.stage_gog(self.root, transition["transaction"])
        swap.assert_not_called()
        inventory.write_bytes(original_inventory)
        for field, value in (("target", str(self.base / "foreign")), ("backup", str(self.base / "foreign")),
                             ("incoming", str(self.base / "foreign")), ("target", journal["items"][1]["target"])):
            changed = dict(journal, items=[dict(journal["items"][0], **{field: value}), *journal["items"][1:]])
            f.write(recovery / "committed.json", changed)
            with self.subTest(field=field, value=value), self.assertRaises(RuntimeError), patch.object(f, "swap") as swap:
                f.stage_gog(self.root, transition["transaction"])
            swap.assert_not_called()
        f.write(recovery / "committed.json", journal)
        unit = next(Path(i["backup"]) for i in journal["items"] if i["target"] == str(self.root / "game/RimWorldWin64.exe"))
        saved = unit.with_name("saved-unit")
        unit.rename(saved)
        with self.assertRaisesRegex(RuntimeError, "Missing GOG recovery unit"), patch.object(f, "swap") as swap:
            f.stage_gog(self.root, transition["transaction"])
        swap.assert_not_called()
        saved.rename(unit)
        stamp = unit.stat()
        unit.write_bytes(b"bad executable")
        os.utime(unit, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
        with self.assertRaisesRegex(RuntimeError, "drift"), patch.object(f, "swap") as swap:
            f.stage_gog(self.root, transition["transaction"])
        swap.assert_not_called()
        self.assertEqual((self.root / "game/RimWorldWin64.exe").read_bytes(), b"steam executable")

    def test_gog_transition_mid_swap_recovery_restores_steam_and_active_candidate(self):
        transition, candidate = self.gog_recovery_case()
        before = f.inventory(self.root / "game")
        original = Path.rename
        def fail_profile(path, destination):
            if path.name == "incoming-profile" and Path(destination) == self.root / "profile":
                raise OSError("injected profile publication failure")
            return original(path, destination)
        with patch.object(Path, "rename", fail_profile), self.assertRaisesRegex(OSError, "injected"):
            f.stage_gog(self.root, transition["transaction"])
        self.assertTrue((self.root / "pending.json").exists())
        f.recover(self.root)
        self.assertEqual(f.inventory(self.root / "game"), before)
        self.assertTrue((candidate / "Tools/helper").exists())
        self.assertTrue((self.root / "profile/save").exists())
        f.audit(self.root)

    def test_gog_transition_refuses_reparse_recovery_unit(self):
        transition, _ = self.gog_recovery_case()
        journal = f.read(self.root / "recovery" / transition["transaction"] / "committed.json")
        unit = Path(journal["items"][0]["backup"])
        original = Path.lstat
        def reparse(path):
            info = original(path)
            return types.SimpleNamespace(st_mode=info.st_mode, st_file_attributes=0x400) if path == unit else info
        with patch.object(Path, "lstat", reparse), self.assertRaisesRegex(RuntimeError, "Reparse"), patch.object(f, "swap") as swap:
            f.stage_gog(self.root, transition["transaction"])
        swap.assert_not_called()

    def test_steam_transition_preserves_source_mods_and_recoverable_gog(self):
        game = self.root / "game"
        (game / "Mods/keep").mkdir(parents=True)
        (game / "Mods/keep/file").write_bytes(b"frozen mod")
        (game / "RimWorldWin64.exe").write_bytes(b"gog exe")
        (game / "gog-marker").write_bytes(b"old")
        gen = self.root / "snapshots/old"
        (gen / "seed/Config").mkdir(parents=True)
        (gen / "inventories").mkdir()
        (gen / "seed/Config/Prefs.xml").write_bytes(b"seed")
        (self.root / "profile").mkdir()
        (self.root / "profile/save").write_bytes(b"preserve save")
        state = {"generation": "old", "manifestSha256": "old-sha"}
        f.write(self.root / "current.json", state)
        f.write(self.root / "menu-observer.json", {"gameSha256": "old"})
        (game / "Mods" / f.MENU_LEAF).mkdir()
        (game / "Mods" / f.MENU_LEAF / "observer").write_bytes(b"gog observer")
        source = self.base / "Steam"
        (source / "RimWorldWin64_Data/Managed").mkdir(parents=True)
        assembly = source / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
        assembly.write_bytes(b"steam assembly")
        (source / "Version.txt").write_text("1.6.4871 rev590")
        (source / "RimWorldWin64.exe").write_bytes(b"steam exe")
        (source / "Mods").mkdir()
        (source / "Mods/do-not-copy").write_bytes(b"normal data")
        before = f.inventory(source)
        with patch.object(f, "audit", return_value={}), patch.object(f, "current", return_value=({"official": []}, gen, state)), \
                patch.object(f, "runtime_contract", return_value={"target": "gog-rev573"}), \
                patch.object(f, "STEAM_GAME", f.digest(assembly)), patch.object(f, "git", return_value="a" * 40):
            result = f.stage_steam(self.root, source)
        self.assertEqual(f.inventory(source), before)
        self.assertEqual((game / "RimWorldWin64.exe").read_bytes(), b"steam exe")
        self.assertEqual((game / "Mods/keep/file").read_bytes(), b"frozen mod")
        self.assertFalse((game / "Mods/do-not-copy").exists())
        self.assertFalse((self.root / "menu-observer.json").exists())
        f.rollback(self.root, result["transaction"])
        self.assertEqual((game / "RimWorldWin64.exe").read_bytes(), b"gog exe")
        self.assertEqual((self.root / "profile/save").read_bytes(), b"preserve save")
        self.assertEqual((game / "Mods" / f.MENU_LEAF / "observer").read_bytes(), b"gog observer")

    def test_native_stack_traces_cannot_enter_performance_measurements(self):
        options = dict(mode="absent", gameplay_smoke=True, exit_after_menu_ready=True, native_stack_traces=True)
        with self.assertRaisesRegex(RuntimeError, "functional scene"):
            f.run_arguments(self.root, **options)
        self.assertIn("--fixture-native-stack-traces", f.run_arguments(self.root, purpose="functional", **options))

    def test_world_background_is_functional_exclusive_and_requires_ordinary_setting(self):
        options = dict(mode="candidate", activation="user", purpose="functional", exit_after_menu_ready=True,
                       settings={"backgroundLoading": True}, world_background=True)
        self.assertIn("--fixture-world-background", f.run_arguments(self.root, **options))
        for change in (dict(purpose="performance"), dict(settings={}), dict(activation="fixture"),
                       dict(exit_after_menu_ready=False), dict(gameplay_smoke=True), dict(residual_probe=True)):
            with self.subTest(change=change), self.assertRaisesRegex(RuntimeError, "World background requires"):
                f.run_arguments(self.root, **dict(options, **change))
        self.assertNotIn("--fixture-world-background", f.run_arguments(self.root, "candidate"))

    def test_process_guard_distinguishes_normal_fixture_mixed_and_unknown_processes(self):
        normal = self.base / "Steam" / "RimWorldWin64.exe"
        fixture = self.root / "game" / "RimWorldWin64.exe"
        normal.parent.mkdir(); fixture.parent.mkdir()
        normal.write_bytes(b"normal"); fixture.write_bytes(b"fixture")
        outside = dict(ProcessId=1, ExecutablePath=str(normal))
        inside = dict(ProcessId=2, ExecutablePath=str(fixture))
        f.validate_nonfixture_processes([], self.root)
        f.validate_nonfixture_processes([outside], self.root)
        for rows in ([inside], [outside, inside], [dict(ProcessId=3, ExecutablePath=None)],
                     [dict(ProcessId=4, ExecutablePath="relative.exe")]):
            with self.assertRaises(RuntimeError):
                f.validate_nonfixture_processes(rows, self.root)

    def test_functional_launch_uses_recorded_purpose_and_other_operations_only_block_fixture(self):
        for action in ("stage-gog", "test", "build", "deploy", "prepare", "capture", "verify-prepared", "build-menu-observer", "deploy-menu-observer"):
            with self.subTest(action=action), patch.object(f, "no_fixture_game") as fixture, patch.object(f, "no_game") as all_games:
                f.guard_operation_processes(self.root, action)
                fixture.assert_called_once_with(self.root)
                all_games.assert_not_called()
        path = self.root / "results/check/run.json"
        for purpose in ("functional", "performance", None, "unknown"):
            f.write(path, {} if purpose is None else {"purpose": purpose})
            with self.subTest(purpose=purpose), patch.object(f, "no_fixture_game") as fixture, patch.object(f, "no_game") as all_games:
                if purpose == "unknown":
                    with self.assertRaises(RuntimeError):
                        f.guard_operation_processes(self.root, "launch", "check")
                    fixture.assert_not_called(); all_games.assert_not_called()
                else:
                    f.guard_operation_processes(self.root, "launch", "check")
                    self.assertEqual(fixture.call_count, int(purpose == "functional"))
                    self.assertEqual(all_games.call_count, int(purpose != "functional"))

    def test_explicit_other_session_exception_keeps_fixture_and_performance_guards(self):
        foreign = dict(ProcessId=3, ExecutablePath=None, SessionId=2)
        f.validate_nonfixture_processes([foreign], self.root, other_session=1)
        for session in (None, 0, 1):
            with self.assertRaises(RuntimeError):
                f.validate_nonfixture_processes([dict(foreign, SessionId=session)], self.root, other_session=1)
        with self.assertRaises(RuntimeError):
            f.validate_nonfixture_processes([foreign], self.root)
        game = self.root / "game/RimWorldWin64.exe"
        game.parent.mkdir(); game.write_bytes(b"fixture")
        with self.assertRaises(RuntimeError):
            f.validate_nonfixture_processes([dict(foreign, ExecutablePath=str(game))], self.root, other_session=1)
        for purpose in ("functional", "performance", None):
            f.write(self.root / "results/check/run.json", {} if purpose is None else {"purpose": purpose})
            with patch.object(f, "no_fixture_game") as fixture, patch.object(f, "no_game") as all_games:
                f.guard_operation_processes(self.root, "launch", "check", allow_other_session=True)
                if purpose == "functional":
                    fixture.assert_called_once_with(self.root, allow_other_session=True)
                    all_games.assert_not_called()
                else:
                    all_games.assert_called_once_with(); fixture.assert_not_called()

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

    def test_xml_source_observer_requires_functional_automatic_menu_observation(self):
        options = dict(activation="user", purpose="functional", exit_after_menu_ready=True, xml_source_observer=True)
        self.assertIn("--fixture-parsed-source-observer", f.run_arguments(self.root, "candidate", **options))
        for change in (dict(purpose="performance"), dict(exit_after_menu_ready=False)):
            with self.subTest(change=change), self.assertRaisesRegex(RuntimeError, "XML source observer requires"):
                f.run_arguments(self.root, "candidate", **dict(options, **change))
        with self.assertRaisesRegex(RuntimeError, "Automatic exit requires the menu observer"):
            f.prepare(self.root, "candidate", "no-observer", menu_observer=False, **options)
        self.assertNotIn("--fixture-parsed-source-observer", f.run_arguments(self.root, "candidate"))

    def test_processed_xml_observer_is_functional_automatic_and_exclusive(self):
        options = dict(activation="user", purpose="functional", exit_after_menu_ready=True, processed_xml_observer=True)
        self.assertIn("--fixture-processed-xml-observer", f.run_arguments(self.root, "candidate", **options))
        for change in (dict(purpose="performance"), dict(exit_after_menu_ready=False)):
            with self.subTest(change=change), self.assertRaisesRegex(RuntimeError, "Processed XML observation"):
                f.run_arguments(self.root, "candidate", **dict(options, **change))
        with self.assertRaisesRegex(RuntimeError, "Choose one XML observer"):
            f.run_arguments(self.root, "candidate", **dict(options, xml_source_observer=True))
        self.assertNotIn("--fixture-processed-xml-observer", f.run_arguments(self.root, "candidate"))

    def test_user_settings_reject_unknown_values_and_nonuser_activation(self):
        selected = {key: True for key in f.USER_BOOLEAN_SETTINGS}
        selected.update(preparedPreset=2, cacheMaintenanceNextLaunch="bypass", sharedCacheMiB=1024)
        self.assertEqual(f.user_settings(selected, "user", "functional"), selected)
        self.assertEqual(f.user_settings(selected, "user", "performance"), selected)
        for action in ("normal", "bypass", "rebuild", "clear"):
            self.assertEqual(f.user_settings({"cacheMaintenanceNextLaunch": action}, "user", "functional"),
                             {"cacheMaintenanceNextLaunch": action})
        invalid = [[], {"enabled": False}, {"language": "French"}, {"pngCache": 1}, {"independentXmlReuse": True},
                   {"assetRouting": "true"}, {"preparedPreset": True}, {"preparedPreset": -1},
                   {"preparedPreset": 3}, {"preparedPreset": 1.0}, {"cacheMaintenanceNextLaunch": "delete"},
                   {"sharedCacheMiB": True}, {"sharedCacheMiB": 0}, {"sharedCacheMiB": 65537},
                   {"sharedCacheMiB": 1.5}, {"parsedXml": "true"}, {"parsedLanguage": "true"}]
        for value in invalid:
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                f.user_settings(value, "user", "functional")
        for activation, purpose in (("fixture", "functional"), ("user", "invalid")):
            with self.subTest(activation=activation, purpose=purpose), self.assertRaises(RuntimeError):
                f.user_settings({}, activation, purpose)

    def test_background_hold_and_captured_save_require_explicit_functional_controls(self):
        options = dict(mode="candidate", exit_after_menu_ready=True, activation="user", gameplay_smoke=True,
                       purpose="functional", settings={"backgroundLoading": True}, background_hold=True,
                       gameplay_save_from="successful-save")
        args = f.run_arguments(self.root, **options)
        self.assertIn("--fixture-background-hold", args)
        self.assertIn("--fixture-gameplay-save", args)
        self.assertFalse(any(arg.startswith("--wake-up-") for arg in args))
        for change in (dict(purpose="performance"), dict(gameplay_smoke=False), dict(activation="fixture"),
                       dict(settings={"backgroundLoading": False}), dict(settings=None),
                       dict(exit_after_menu_ready=False), dict(gameplay_save_from="../normal-save")):
            with self.subTest(change=change), self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict(options, **change))
        for change in (dict(purpose="performance"), dict(gameplay_smoke=False)):
            with self.subTest(save_only=change), self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict(options, background_hold=False, **change))

    def test_captured_gameplay_save_copies_only_fixed_save_and_rechecks_source_binding(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]
        manifest = {"activeOrder": active, "generation": "test"}
        f.write(self.root / "candidate.json", {"sourceRevision": "new-product"})
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True, "supportsGameplaySmoke": True,
                "supportsBackgroundHold": True, "supportsGameplaySave": True, "sourceRevision": "new-observer"})
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/successful-save/profile"
        save = profile / "Saves/RloFixtureSmoke.rws"
        save.parent.mkdir(parents=True)
        save.write_bytes(b"fixture save")
        (save.parent / "unrelated.rws").write_bytes(b"do not copy")
        f.write(profile / "FixtureMenuObserver/gameplay-smoke.json", {"passed": True})
        source = {"status": "captured-awaiting-human-and-runtime-review", "exitCode": 0,
                  "exitAfterMenuReady": True, "automaticTestPassed": True, "gameplaySmoke": True,
                  "gameplayTestPassed": True, "lane": "representative", "generation": "test",
                  "manifestSha256": "snapshot", "activeOrder": f.run_order(manifest, "representative", "absent", True),
                  "excludedMods": [], "mode": "absent", "menuObserverPackage": {"sourceRevision": "old-observer"},
                  "evidence": f.inventory(profile)}
        source_path = profile.parent / "run.json"
        f.write(source_path, source)
        settings = self.base / "hold-settings.json"
        f.write(settings, {"backgroundLoading": True})
        kwargs = dict(activation="user", purpose="functional", gameplay_smoke=True, background_hold=True,
                      user_settings_path=settings, gameplay_save_from="successful-save")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40), patch.object(f.subprocess, "Popen") as launch:
            for change in (dict(automaticTestPassed=False), dict(gameplayTestPassed=False), dict(exitCode=1),
                           dict(status="started"), dict(lane="activation"), dict(activeOrder=active[:-1])):
                f.write(source_path, dict(source, **change))
                with self.subTest(change=change), self.assertRaises(RuntimeError):
                    f.prepare(self.root, "candidate", "hold", **kwargs)
                self.assertFalse((self.root / "results/hold").exists())
            f.write(source_path, source)
            stamp = save.stat()
            save.write_bytes(b"changed save")
            os.utime(save, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "evidence drift"):
                f.prepare(self.root, "candidate", "hold", **kwargs)
            save.write_bytes(b"fixture save")
            os.utime(save, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            f.prepare(self.root, "candidate", "hold", **kwargs)
            run_path = self.root / "results/hold/run.json"
            run = f.read(run_path)
            self.assertTrue(run["backgroundHold"])
            self.assertEqual(run["gameplaySaveSource"]["runSha256"], f.digest(source_path))
            self.assertEqual((self.root / "profile/Saves/RloFixtureSmoke.rws").read_bytes(), b"fixture save")
            self.assertFalse((self.root / "profile/Saves/unrelated.rws").exists())
            f.verify_prepared(self.root, "hold")
            f.write(run_path, dict(run, arguments=[arg for arg in run["arguments"] if arg != "--fixture-background-hold"]))
            with self.assertRaisesRegex(RuntimeError, "command drift"):
                f.verify_prepared(self.root, "hold")
            f.write(run_path, run)
            f.write(source_path, dict(source, addedAfterPreparation=True))
            with self.assertRaisesRegex(RuntimeError, "binding drift"):
                f.verify_prepared(self.root, "hold")
            f.write(source_path, source)
            save.write_bytes(b"changed save")
            with self.assertRaisesRegex(RuntimeError, "evidence drift"):
                f.verify_prepared(self.root, "hold")
            save.write_bytes(b"fixture save")
            # Manual loading reuses the same provenance checks, without requiring
            # observer gameplay support or starting an automatic scene probe.
            f.write(self.root / "menu-observer.json", {"sourceRevision": "menu-only-observer"})
            for observer in (False, True):
                label = "manual-save-" + str(observer)
                manual_options = dict(kwargs, gameplay_smoke=False, background_hold=False,
                                      exit_after_menu_ready=False, menu_observer=observer)
                f.prepare(self.root, "candidate", label, **manual_options)
                manual_path = self.root / "results" / label / "run.json"
                manual = f.read(manual_path)
                self.assertFalse(manual["gameplaySmoke"])
                self.assertFalse(manual["exitAfterMenuReady"])
                self.assertEqual(manual["menuObserver"], observer)
                self.assertFalse(any(arg.startswith("--fixture-") for arg in manual["arguments"]))
                self.assertEqual((self.root / "profile/Saves/RloFixtureSmoke.rws").read_bytes(), b"fixture save")
                self.assertFalse((self.root / "profile/Saves/unrelated.rws").exists())
                f.verify_prepared(self.root, label)
                f.write(source_path, dict(source, addedAfterPreparation=True))
                with self.assertRaisesRegex(RuntimeError, "binding drift"):
                    f.verify_prepared(self.root, label)
                f.write(source_path, source)
                save.write_bytes(b"changed save")
                with self.assertRaisesRegex(RuntimeError, "evidence drift"):
                    f.verify_prepared(self.root, label)
                save.write_bytes(b"fixture save")
            launch.assert_not_called()

    def test_manual_captured_save_requires_functional_purpose_and_no_automatic_exit(self):
        options = dict(mode="candidate", purpose="functional", activation="user",
                       gameplay_save_from="successful-save", exit_after_menu_ready=False)
        self.assertEqual(f.run_arguments(self.root, **options), ["-savedatafolder=" + str(self.root / "profile")])
        for change in (dict(purpose="performance"), dict(exit_after_menu_ready=True),
                       dict(gameplay_save_from="../normal-save"), dict(gameplay_smoke=True)):
            with self.subTest(change=change), self.assertRaises(RuntimeError):
                f.run_arguments(self.root, **dict(options, **change))

    def test_functional_user_settings_prepare_native_xml_capture_and_verify_drift(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        (seed / "Prefs.xml").write_text("<PrefsData><volumeMaster>0.8</volumeMaster></PrefsData>")
        original_settings = '<SettingsBlock><ModSettings Class="WakeUp.WakeUpSettings"><typeSearches>False</typeSearches></ModSettings></SettingsBlock>'
        (seed / f.USER_SETTINGS_FILE).write_text(original_settings)
        manifest = {"activeOrder": active, "generation": "test"}
        candidate = self.root / "game/Mods" / f.PACKAGE_LEAF
        candidate.mkdir(parents=True)
        f.write(self.root / "candidate.json", {"sourceRevision": "a" * 40})
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True})
        input_path = self.base / "settings.json"
        settings = {"translationApplication": True, "loadingDisplay": False, "preparedPreset": 0,
                    "cacheMaintenanceNextLaunch": "rebuild"}
        f.write(input_path, settings)
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40):
            f.prepare(self.root, "candidate", "settings", activation="user", purpose="functional",
                      user_settings_path=input_path, language="French (Français)", xml_source_observer=True,
                      texture_quality_probe="catalog", texture_storage_probe=True)
            run = f.read(self.root / "results/settings/run.json")
            self.assertLess(run["arguments"].index("--fixture-c08-quality=catalog"), run["arguments"].index("--fixture-c05-texture-probe"))
            self.assertLess(run["arguments"].index("--fixture-c05-texture-probe"), run["arguments"].index("--fixture-functional-probes"))
            self.assertTrue(run["xmlSourceObserver"])
            self.assertIn("--fixture-parsed-source-observer", run["arguments"])
            self.assertEqual(run["userSettings"], settings)
            self.assertEqual(run["profileOverrides"], {"runInBackground": True, "userSettings": settings, "langFolderName": "French (Français)"})
            self.assertEqual(run["language"], "French (Français)")
            self.assertFalse(any(arg.startswith("--wake-up-") for arg in run["arguments"]))
            profile = self.root / "profile"
            node = f.ET.parse(profile / "Config" / f.USER_SETTINGS_FILE).find("ModSettings")
            self.assertEqual(node.get("Class"), "WakeUp.WakeUpSettings")
            self.assertEqual(node.findtext("translationApplication"), "True")
            self.assertEqual(node.findtext("loadingDisplay"), "False")
            self.assertEqual(node.findtext("preparedPreset"), "0")
            self.assertEqual(node.findtext("typeSearches"), "False")
            self.assertEqual((seed / f.USER_SETTINGS_FILE).read_text(), original_settings)
            self.assertEqual(f.ET.parse(profile / "Config/Prefs.xml").findtext("volumeMaster"), "0.8")
            self.assertEqual(f.ET.parse(profile / "Config/Prefs.xml").findtext("langFolderName"), "French (Français)")
            # Launch reconstruction uses captured values, not the mutable input file.
            f.write(input_path, {"translationApplication": False})
            f.verify_prepared(self.root, "settings")
            f.write(self.root / "results/settings/run.json", dict(run, xmlSourceObserver=False))
            with self.assertRaisesRegex(RuntimeError, "Launch command drift"):
                f.verify_prepared(self.root, "settings")
            f.write(self.root / "results/settings/run.json", dict(run, userSettings={"translationApplication": False}))
            with self.assertRaisesRegex(RuntimeError, "overrides drift"):
                f.verify_prepared(self.root, "settings")
            f.write(self.root / "results/settings/run.json", run)
            f.apply_user_settings(profile, {"translationApplication": False})
            with self.assertRaisesRegex(RuntimeError, "user setting drift"):
                f.verify_profile_overrides(profile, run)
            with self.assertRaises(RuntimeError):
                f.verify_prepared(self.root, "settings")
            # Omitting overrides restores the unchanged seed on the next preparation.
            f.prepare(self.root, "candidate", "default", activation="user", purpose="functional")
            ordinary = f.read(self.root / "results/default/run.json")
            self.assertIsNone(ordinary["userSettings"])
            self.assertEqual(ordinary["profileOverrides"], {"runInBackground": True})
            self.assertEqual((profile / "Config" / f.USER_SETTINGS_FILE).read_text(), original_settings)
            self.assertIsNone(f.ET.parse(profile / "Config/Prefs.xml").findtext("langFolderName"))

    def test_language_override_validates_selection_and_detects_preference_drift(self):
        for language in ("English", "French (Français)"):
            self.assertEqual(f.profile_overrides(False, None, "fixture", "functional", language), {"langFolderName": language})
        self.assertEqual(f.profile_overrides(False, None, "user", "performance", "English"), {"langFolderName": "English", "volumeMaster": 0})
        for language, purpose in (("French", "functional"), ("English", "invalid")):
            with self.assertRaises(RuntimeError):
                f.profile_overrides(False, None, "fixture", purpose, language)
        (self.root / "Config").mkdir()
        (self.root / "Config/Prefs.xml").write_text("<PrefsData><langFolderName>English</langFolderName></PrefsData>")
        run = {"purpose": "functional", "language": "French (Français)", "profileOverrides": {"langFolderName": "French (Français)"}}
        with self.assertRaisesRegex(RuntimeError, "language drift"):
            f.verify_profile_overrides(self.root, run)

    def test_user_settings_create_native_wrapper_when_seed_has_no_product_settings(self):
        f.apply_user_settings(self.root, {"assetRouting": True, "deferredAudio": True,
                                         "streamingXml": True, "hideLoadingSummary": True, "loadingTimings": True})
        doc = f.ET.parse(self.root / "Config" / f.USER_SETTINGS_FILE)
        self.assertEqual(doc.getroot().tag, "SettingsBlock")
        self.assertEqual(doc.find("ModSettings").get("Class"), "WakeUp.WakeUpSettings")
        self.assertEqual(doc.findtext("ModSettings/assetRouting"), "True")
        self.assertEqual(doc.findtext("ModSettings/deferredAudio"), "True")
        for setting in ("streamingXml", "hideLoadingSummary", "loadingTimings"):
            self.assertEqual(doc.findtext("ModSettings/" + setting), "True")

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="wake-up-fixture-test-")
        self.base = Path(self.temp.name)
        self.root = self.base / "fixture"
        self.root.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def external_preparation_case(self):
        profile = self.root / "profile"
        (profile / "Config").mkdir(parents=True)
        (profile / "Config/ModsConfig.xml").write_bytes(b"<ModsConfigData />")
        (profile / "Config/Prefs.xml").write_bytes(b"unchanged preferences")
        (profile / "WakeUp/ParsedXml/v1").mkdir(parents=True)
        (profile / "WakeUp/ParsedXml/v1/other-record").write_bytes(b"other cache")
        (self.root / "game/Mods").mkdir(parents=True)
        source = self.root / "game/Mods/color.dds"
        source.write_bytes(b"authored DDS")
        app = self.base / "artifacts/app/WakeUp.Preparation.exe"
        app.parent.mkdir(parents=True)
        app.write_bytes(b"packaged app")
        (app.parent / "WakeUp.TextureHelper.exe").write_bytes(b"packaged helper")
        job_path = self.base / "artifacts/job.json"
        job = dict(gameRoot=str(self.root / "game"), modsConfigPath=str(profile / "Config/ModsConfig.xml"),
                   saveDataRoot=str(profile), additionalRoots=[], paths=["Textures/Things/color.dds"],
                   preset=1, filter=2, aniso=2, mipBias=0, action="prepare", budgetMiB=128)
        f.write(job_path, job)
        run_path = self.root / "results/external/run.json"
        f.write(run_path, dict(status="prepared-offline", purpose="functional", mode="candidate",
                              activation="user", profileBefore=f.inventory(profile), arguments=["unchanged"],
                              activeOrder=[f.PACKAGE_ID], candidate={"sourceRevision": "fixed"}))

        def verify(root, label):
            self.assertEqual((root, label), (self.root, "external"))
            run = f.read(run_path)
            f.require(run["status"] == "prepared-offline", "Run already launched")
            f.verify_tree(profile, run["profileBefore"], True)
            return {"verifiedPrepared": label}

        def backend(command, **options):
            self.assertEqual(command[0], str(app))
            self.assertEqual(command[1::2], ["--functional-job", "--result"])
            self.assertEqual(options["cwd"], app.parent)
            self.assertEqual(options["stdin"], subprocess.DEVNULL)
            self.assertFalse(options["check"])
            if os.name == "nt":
                self.assertEqual(options["startupinfo"].wShowWindow, 0)
                self.assertTrue(options["creationflags"] & subprocess.CREATE_NO_WINDOW)
            self.assertEqual(f.read(run_path)["status"], "external-preparation-attempted")
            self.assertNotEqual(Path(command[2]), job_path)
            self.assertEqual(f.read(Path(command[2])), job)
            for category in ("PngCache", "PreparedTextures", "StaticAtlases", "ParsedXml", "ProcessedXml", "ResolvedInheritance", "ParsedLanguage"):
                owner = profile / "WakeUp" / category / "v1/.owner"
                owner.parent.mkdir(parents=True, exist_ok=True)
                owner.write_bytes(b"")
            (profile / "WakeUp/.budget-owner").write_bytes(b"")
            (profile / "WakeUp/PreparedTextures/request.xml").write_bytes(b"preparation request")
            config_sha = f.digest(profile / "Config/ModsConfig.xml")
            result = dict(success=True, action="prepare", prepared=1, reused=0, skipped=0, failed=0,
                          storedBytes=100, textureBytes=128, summary="prepared", error="",
                          gameAssemblySha256=f.DEVELOPMENT_GAME, modsConfigBefore=config_sha, modsConfigAfter=config_sha,
                          selected=[dict(sourcePath=str(source), logicalPath="Textures/Things/color.dds", provider="fixture",
                                         sourceBefore=f.digest(source), sourceAfter=f.digest(source))], progress=[])
            f.write(Path(command[4]), result)
            options["stdout"].write(b"backend complete\n")
            return types.SimpleNamespace(returncode=0)
        return app, job_path, run_path, job, verify, backend

    def test_external_preparation_records_owned_changes_then_revalidates_before_launch(self):
        app, job, run_path, _, verify, backend = self.external_preparation_case()
        original = f.read(run_path)
        with patch.object(f, "REPO", self.base), patch.object(f, "verify_prepared", side_effect=verify) as preflight, \
                patch.object(f, "runtime_contract", return_value={"target": "gog-rev573", "gameAssemblySha256": f.DEVELOPMENT_GAME}), \
                patch.object(f.subprocess, "run", side_effect=backend) as process:
            result = f.external_prepare(self.root, "external", app, job)
        self.assertEqual(preflight.call_count, 2)
        process.assert_called_once()
        self.assertFalse(result["gameLaunched"])
        run = f.read(run_path)
        self.assertEqual(run["status"], "prepared-offline")
        for key in ("arguments", "activeOrder", "candidate"):
            self.assertEqual(run[key], original[key])
        receipt = run["externalPreparations"][0]
        self.assertEqual(receipt["status"], "completed")
        self.assertEqual(receipt["exitStatus"], 0)
        self.assertEqual(receipt["forbiddenChanges"], [])
        self.assertEqual(receipt["resultSha256"], f.digest(Path(receipt["resultPath"])))
        self.assertEqual(receipt["distributionSha256"], f.identity(receipt["distribution"]))
        self.assertEqual(run["profileBefore"], f.inventory(self.root / "profile"))
        f.verify_tree(Path(receipt["profileSnapshot"]), original["profileBefore"], True)

    def test_external_preparation_rejects_outside_roots_and_nonfunctional_or_launched_run(self):
        app, job_path, run_path, job, verify, _ = self.external_preparation_case()
        original_run = f.read(run_path)
        bad_jobs = [dict(job, saveDataRoot=str(self.base)), dict(job, gameRoot=str(self.base)),
                    dict(job, modsConfigPath=str(self.base / "normal.xml")),
                    dict(job, additionalRoots=[dict(path=str(self.base), kind="Workshop")]),
                    dict(job, paths=["Textures/../normal"]), dict(job, action="delete"),
                    dict(job, preset=True), dict(job, mipBias=float("nan"))]
        with patch.object(f, "REPO", self.base), patch.object(f, "verify_prepared", side_effect=verify), \
                patch.object(f.subprocess, "run") as process:
            for bad in bad_jobs:
                f.write(job_path, bad)
                with self.subTest(job=bad), self.assertRaises(RuntimeError):
                    f.external_prepare(self.root, "external", app, job_path)
            f.write(job_path, job)
            for change in (dict(purpose="performance"), dict(mode="baseline"), dict(activation="fixture"),
                           dict(startedUtc="already started")):
                f.write(run_path, dict(original_run, **change))
                with self.subTest(change=change), self.assertRaises(RuntimeError):
                    f.external_prepare(self.root, "external", app, job_path)
            process.assert_not_called()

    def test_external_preparation_failure_captures_result_and_blocks_launch(self):
        app, job, run_path, _, verify, backend = self.external_preparation_case()

        def canceled(command, **options):
            backend(command, **options)
            result = f.read(Path(command[4]))
            f.write(Path(command[4]), dict(result, success=False, canceled=True, error="canceled"))
            return types.SimpleNamespace(returncode=3)

        with patch.object(f, "REPO", self.base), patch.object(f, "verify_prepared", side_effect=verify) as preflight, \
                patch.object(f, "runtime_contract", return_value={"target": "gog-rev573", "gameAssemblySha256": f.DEVELOPMENT_GAME}), \
                patch.object(f.subprocess, "run", side_effect=canceled):
            with self.assertRaisesRegex(RuntimeError, "evidence retained"):
                f.external_prepare(self.root, "external", app, job)
        self.assertEqual(preflight.call_count, 1)
        run = f.read(run_path)
        self.assertEqual(run["status"], "external-preparation-failed")
        receipt = run["externalPreparations"][0]
        self.assertEqual(receipt["exitStatus"], 3)
        self.assertTrue(receipt["result"]["canceled"])
        self.assertTrue(receipt["profileAfter"])
        with self.assertRaisesRegex(RuntimeError, "already launched"):
            verify(self.root, "external")
        with patch.object(f, "REPO", self.base), patch.object(f, "verify_prepared", side_effect=verify), \
                patch.object(f, "runtime_contract", return_value={"target": "gog-rev573", "gameAssemblySha256": f.DEVELOPMENT_GAME}), \
                patch.object(f.subprocess, "run", side_effect=backend):
            f.external_prepare(self.root, "external", app, job)
        resumed = f.read(run_path)
        self.assertEqual(resumed["status"], "prepared-offline")
        self.assertEqual([r["status"] for r in resumed["externalPreparations"]], ["failed", "completed"])

    def test_external_preparation_preserves_evidence_of_unowned_profile_write(self):
        app, job, run_path, _, verify, backend = self.external_preparation_case()

        def writes_unowned(command, **options):
            result = backend(command, **options)
            (self.root / "profile/Config/Prefs.xml").write_bytes(b"unexpected change")
            (self.root / "profile/WakeUp/ParsedXml/v1/other-record").write_bytes(b"unexpected cache change")
            return result

        with patch.object(f, "REPO", self.base), patch.object(f, "verify_prepared", side_effect=verify), \
                patch.object(f, "runtime_contract", return_value={"target": "gog-rev573", "gameAssemblySha256": f.DEVELOPMENT_GAME}), \
                patch.object(f.subprocess, "run", side_effect=writes_unowned):
            with self.assertRaisesRegex(RuntimeError, "unowned profile"):
                f.external_prepare(self.root, "external", app, job)
        run = f.read(run_path)
        self.assertEqual(run["status"], "external-preparation-failed")
        receipt = run["externalPreparations"][0]
        self.assertEqual(receipt["forbiddenChanges"], ["Config/Prefs.xml", "WakeUp/ParsedXml/v1/other-record"])
        self.assertEqual((Path(receipt["profileSnapshot"]) / "Config/Prefs.xml").read_bytes(), b"unchanged preferences")
        self.assertTrue(receipt["result"]["success"])

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
            f.prepare(self.root, "absent", "absent-activation", lane="activation", menu_observer=False, exit_after_menu_ready=False, purpose="functional")
            self.assertEqual(f.read(self.root / "results/absent-activation/run.json")["purpose"], "functional")
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), active[:2])
            f.prepare(self.root, "baseline", "restored-baseline", observation="timing", strategy="type-lookup", menu_observer=False, exit_after_menu_ready=False)
            self.assertTrue(live.is_dir())
            self.assertFalse(parked.exists())
            self.assertEqual(f.read(self.root / "results/restored-baseline/run.json")["purpose"], "performance")
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

    def test_user_png_warm_restore_keeps_selection_and_content_guards(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        receipt = {"sourceRevision": "a" * 40}
        observer = {"supportsAutomaticExit": True}
        f.write(self.root / "candidate.json", receipt)
        f.write(self.root / "menu-observer.json", observer)
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        settings = {"pngCache": True, "cacheMaintenanceNextLaunch": "normal"}
        settings_path = self.base / "settings.json"
        f.write(settings_path, settings)
        profile = self.root / "results/cold/profile"
        store = profile / "WakeUp/PngCache/v1"
        store.mkdir(parents=True)
        entry = store / ("A" * 64 + ".bin")
        entry.write_bytes(b"png")
        terrain = profile / "Config/TrueTerrainColorsCache.xml"
        terrain.parent.mkdir()
        terrain.write_bytes(b"terrain")
        (profile / "MissileGirl").mkdir()
        (profile / "MissileGirl/cache.bin").write_bytes(b"supplier")
        run = {"mode": "candidate", "activation": "user", "userSettings": settings, "png": "off",
               "status": "captured-awaiting-human-and-runtime-review", "automaticTestPassed": True, "exitCode": 0,
               "lane": "representative", "strategy": "startup-searches", "manifestSha256": "snapshot",
               "generation": "test", "candidate": receipt, "excludedMods": [], "pngSource": "native",
               "activeOrder": f.run_order(manifest, "representative", "candidate", True), "gagarinCache": "off",
               "menuObserver": True, "menuObserverPackage": observer, "exitAfterMenuReady": True,
               "profileOverrides": f.profile_overrides(True, settings, "user", "performance"), "evidence": f.inventory(profile)}
        previous = profile.parent / "run.json"
        f.write(previous, run)
        kwargs = dict(activation="user", user_settings_path=settings_path, png_cache_from="cold", foreign_cache_from="cold")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), patch.object(f, "git", return_value="a" * 40):
            for changed in ({"pngCache": False}, {"pngCache": True, "cacheMaintenanceNextLaunch": "bypass"}):
                self.assertFalse(f.png_cache_selected("candidate", "user", "off", changed))
            for key, value in (("candidate", {}), ("userSettings", {"pngCache": False})):
                f.write(previous, dict(run, **{key: value}))
                with self.assertRaises(RuntimeError):
                    f.prepare(self.root, "candidate", "warm", **kwargs)
            f.write(previous, run)
            for file, original in ((entry, b"png"), (terrain, b"terrain")):
                stamp = file.stat()
                file.write_bytes(b"tampered")
                with self.assertRaises(RuntimeError):
                    f.prepare(self.root, "candidate", "warm", **kwargs)
                file.write_bytes(original)
                os.utime(file, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            f.prepare(self.root, "candidate", "warm", **kwargs)
            self.assertEqual((self.root / "profile/WakeUp/PngCache/v1" / entry.name).read_bytes(), b"png")
            self.assertEqual((self.root / "profile/Config/TrueTerrainColorsCache.xml").read_bytes(), b"terrain")
            self.assertEqual(f.read(self.root / "results/warm/run.json")["restoredCacheKinds"],
                             ["gagarin-MissileGirl", "terrain-colors", "wake-up-PngCache"])

    def test_prepared_cache_manual_capture_restore_rejects_tamper_and_unowned_paths(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]
        manifest = {"activeOrder": active, "generation": "test"}
        receipt = {"sourceRevision": "a" * 40}
        f.write(self.root / "candidate.json", receipt)
        f.write(self.root / "menu-observer.json", {"supportsAutomaticExit": True, "sourceRevision": "new-observer"})
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/public-preparation/profile"
        store = profile / "WakeUp/PreparedTextures/v1"
        store.mkdir(parents=True)
        entry = store / ("A" * 64 + ".bin")
        entry.write_bytes(b"prepared")
        (store / ".owner").write_bytes(b"")
        exports = store / "exports"
        exports.mkdir()
        dds = exports / ("B" * 64 + "." + "C" * 64 + ".dds")
        dds.write_bytes(b"captured DDS")
        mapping = exports / ("B" * 64 + ".manifest")
        mapping.write_bytes(b"captured mapping")
        (profile / "Saves").mkdir()
        (profile / "Saves/never-copy.rws").write_bytes(b"private save")
        signal = {"schema": "fixture-menu-ready.v1", "event": "menu-ready", "runId": "observed",
                  "processId": 123, "counterFrequency": 1000, "counter": 1100, "elapsedSeconds": 1.0}
        f.write(profile / "FixtureMenuObserver/menu-ready.json", signal)
        run = {"mode": "candidate", "status": "captured-awaiting-human-and-runtime-review", "exitCode": 0,
               "lane": "representative", "manifestSha256": "snapshot", "generation": "test", "candidate": receipt,
               "activeOrder": f.run_order(manifest, "representative", "candidate", True), "excludedMods": [], "pngSource": "native",
               "menuObserver": True, "menuObserverPackage": {"sourceRevision": "old-observer"}, "exitAfterMenuReady": False,
               "menuObserverRunId": "observed", "menuObserverClock": {"frequency": 1000, "startCounter": 100},
               "pid": 123, "menuReadyObservation": signal, "evidence": f.inventory(profile),
               "userSettings": {"loadingDisplay": True}, "capturedUtc": "2026-09-10T01:00:00Z"}
        previous = profile.parent / "run.json"
        f.write(previous, run)
        settings = self.base / "prepared-settings.json"
        f.write(settings, {"preparedTextures": True, "preparedPreset": 0})
        kwargs = dict(activation="user", purpose="functional", user_settings_path=settings,
                      prepared_cache_from="public-preparation")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40), patch.object(f.subprocess, "Popen") as launch:
            stamp = entry.stat()
            entry.write_bytes(b"tampered")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.prepare(self.root, "candidate", "warm", **kwargs)
            self.assertFalse((self.root / "results/warm").exists())
            entry.write_bytes(b"prepared")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            export_stamp = dds.stat()
            dds.write_bytes(b"tampered DDS")
            os.utime(dds, ns=(export_stamp.st_atime_ns, export_stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.prepare(self.root, "candidate", "warm", **kwargs)
            dds.write_bytes(b"captured DDS")
            os.utime(dds, ns=(export_stamp.st_atime_ns, export_stamp.st_mtime_ns))
            unsafe = dict(run, evidence=run["evidence"] + [{"kind": "file", "path": "WakeUp/PreparedTextures/v1/../outside.bin"}])
            f.write(previous, unsafe)
            with self.assertRaisesRegex(RuntimeError, "Unexpected prepared cache files"):
                f.prepare(self.root, "candidate", "warm", **kwargs)
            f.write(previous, run)
            f.prepare(self.root, "candidate", "warm", **kwargs)
            prepared = f.read(self.root / "results/warm/run.json")
            self.assertEqual((self.root / "profile/WakeUp/PreparedTextures/v1" / entry.name).read_bytes(), b"prepared")
            self.assertEqual((self.root / "profile/WakeUp/PreparedTextures/v1/exports" / dds.name).read_bytes(), b"captured DDS")
            self.assertFalse((self.root / "profile/Saves").exists())
            self.assertEqual(prepared["preparedCacheFrom"], "public-preparation")
            self.assertEqual(prepared["preparedCacheSource"]["runSha256"], f.digest(previous))
            self.assertEqual(prepared["restoredCacheKinds"], ["wake-up-PreparedTextures"])
            self.assertEqual(prepared["userSettings"], {"preparedTextures": True, "preparedPreset": 0})
            launch.assert_not_called()

    def test_fixture_png_grouped_restore_binds_controls_and_captured_bytes(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        receipt = {"sourceRevision": "a" * 40}
        observer = {"supportsAutomaticExit": True, "sourceRevision": "observer"}
        f.write(self.root / "candidate.json", receipt)
        f.write(self.root / "menu-observer.json", observer)
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/png-cold/profile"
        groups = profile / "WakeUp/PreparedTextures/v1/groups"
        groups.mkdir(parents=True)
        (groups / "index").write_bytes(b"captured index")
        group = groups / ("a" * 32 + ".group")
        group.write_bytes(b"captured pixels")
        delta = groups / ("0000000000000001_" + "b" * 32 + ".delta")
        delta.write_bytes(b"captured delta")
        signal = {"schema": "fixture-menu-ready.v1", "event": "menu-ready", "runId": "observed",
                  "processId": 123, "counterFrequency": 1000, "counter": 1100, "elapsedSeconds": 1.0}
        f.write(profile / "FixtureMenuObserver/menu-ready.json", signal)
        run = {"mode": "candidate", "activation": "fixture", "png": "cache", "pngSource": "original",
               "status": "captured-awaiting-human-and-runtime-review", "exitCode": 0, "automaticTestPassed": True,
               "lane": "representative", "strategy": "startup-searches", "manifestSha256": "snapshot",
               "generation": "test", "candidate": receipt, "excludedMods": [], "gagarinCache": "off",
               "activeOrder": f.run_order(manifest, "representative", "candidate", True),
               "menuObserver": True, "menuObserverPackage": observer, "exitAfterMenuReady": True,
               "menuObserverRunId": "observed", "menuObserverClock": {"frequency": 1000, "startCounter": 100},
               "pid": 123, "menuReadyObservation": signal, "evidence": f.inventory(profile),
               "profileOverrides": f.profile_overrides(True, None, "fixture", "performance")}
        previous = profile.parent / "run.json"
        kwargs = dict(activation="fixture", png="cache", png_source="original", prepared_cache_from="png-cold")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40), patch.object(f.subprocess, "Popen") as launch:
            for key, value in (("candidate", {}), ("generation", "other"), ("manifestSha256", "other"),
                               ("activeOrder", []), ("excludedMods", [{"id": "other"}]), ("pngSource", "native"),
                               ("menuObserverPackage", {}), ("profileOverrides", {}), ("gagarinCache", "on"),
                               ("activation", "user"), ("png", "control"), ("status", "running"),
                               ("exitCode", 1), ("automaticTestPassed", False)):
                f.write(previous, dict(run, **{key: value}))
                with self.subTest(key=key), self.assertRaises(RuntimeError):
                    f.prepare(self.root, "candidate", "png-warm", **kwargs)
                self.assertFalse((self.root / "results/png-warm").exists())
            f.write(previous, run)
            for mode in ("control", "off"):
                with self.subTest(mode=mode), self.assertRaises(RuntimeError):
                    f.prepare(self.root, "candidate", "png-warm", **dict(kwargs, png=mode, png_source="native"))
            for invalid in ("unknown.delta", delta.name + ".pending", "f" * 15 + "_" + "b" * 32 + ".delta"):
                unexpected = {"kind": "file", "path": "WakeUp/PreparedTextures/v1/groups/" + invalid}
                f.write(previous, dict(run, evidence=run["evidence"] + [unexpected]))
                with self.subTest(invalid=invalid), self.assertRaisesRegex(RuntimeError, "Unexpected prepared cache files"):
                    f.prepare(self.root, "candidate", "png-warm", **kwargs)
            f.write(previous, run)
            for entry, contents in ((group, b"captured pixels"), (delta, b"captured delta")):
                stamp = entry.stat()
                entry.write_bytes(b"X" * len(contents))
                os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
                with self.subTest(entry=entry.name), self.assertRaisesRegex(RuntimeError, "Content drift"):
                    f.prepare(self.root, "candidate", "png-warm", **kwargs)
                entry.write_bytes(contents)
                os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            for mode in ("cache", "verify-cache"):
                f.prepare(self.root, "candidate", "png-warm-" + mode, **dict(kwargs, png=mode))
                prepared = f.read(self.root / ("results/png-warm-" + mode + "/run.json"))
                self.assertEqual(prepared["pngSource"], "original")
                self.assertEqual(prepared["preparedCacheSource"]["runSha256"], f.digest(previous))
                self.assertEqual(prepared["restoredCacheKinds"], ["wake-up-PreparedTextures"])
                self.assertEqual((self.root / "profile/WakeUp/PreparedTextures/v1/groups" / group.name).read_bytes(),
                                 b"captured pixels")
                self.assertEqual((self.root / "profile/WakeUp/PreparedTextures/v1/groups" / delta.name).read_bytes(),
                                 b"captured delta")
            launch.assert_not_called()

    def test_parsed_xml_capture_restore_binds_sources_and_supports_functional_maintenance(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld", "unrelated.content"]
        manifest = {"activeOrder": active, "generation": "test"}
        receipt = {"sourceRevision": "a" * 40, "runtimeContract": {"target": "gog-rev573"}}
        observer = {"supportsAutomaticExit": True, "sourceRevision": "observer"}
        f.write(self.root / "candidate.json", receipt)
        f.write(self.root / "menu-observer.json", observer)
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/parsed-cold/profile"
        store = profile / "WakeUp/ParsedXml/v1"
        store.mkdir(parents=True)
        entry = store / ("A" * 64 + ".bin")
        entry.write_bytes(b"parsed XML")
        (store / ".owner").write_bytes(b"")
        (profile / "Saves").mkdir()
        (profile / "Saves/never-copy.rws").write_bytes(b"private save")
        signal = {"schema": "fixture-menu-ready.v1", "event": "menu-ready", "runId": "observed",
                  "processId": 123, "counterFrequency": 1000, "counter": 1100, "elapsedSeconds": 1.0}
        f.write(profile / "FixtureMenuObserver/menu-ready.json", signal)
        selected = {"parsedXml": True, "sharedCacheMiB": 1024}
        run = {"mode": "candidate", "activation": "user", "status": "captured-awaiting-human-and-runtime-review",
               "exitCode": 0, "automaticTestPassed": True, "lane": "representative", "manifestSha256": "snapshot",
               "generation": "test", "candidate": receipt, "activeOrder": f.run_order(manifest, "representative", "candidate", True),
               "excludedMods": [], "pngSource": "native", "menuObserver": True, "menuObserverPackage": observer,
               "exitAfterMenuReady": True, "menuObserverRunId": "observed", "menuObserverClock": {"frequency": 1000, "startCounter": 100},
               "pid": 123, "menuReadyObservation": signal, "evidence": f.inventory(profile), "userSettings": selected}
        previous = profile.parent / "run.json"
        f.write(previous, run)
        settings = self.base / "parsed-settings.json"
        f.write(settings, selected)
        kwargs = dict(activation="user", purpose="functional", user_settings_path=settings, parsed_xml_cache_from="parsed-cold")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40), patch.object(f.subprocess, "Popen") as launch:
            for key, value in (("candidate", {"sourceRevision": "other"}), ("generation", "steam"),
                               ("manifestSha256", "other"), ("activeOrder", list(reversed(run["activeOrder"]))),
                               ("excludedMods", [{"id": "other"}]), ("menuObserverPackage", {})):
                f.write(previous, dict(run, **{key: value}))
                with self.subTest(key=key), self.assertRaisesRegex(RuntimeError, "configuration mismatch"):
                    f.prepare(self.root, "candidate", "parsed-warm", **kwargs)
            f.write(previous, run)
            stamp = entry.stat()
            entry.write_bytes(b"tamper XML")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.prepare(self.root, "candidate", "parsed-warm", **kwargs)
            entry.write_bytes(b"parsed XML")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            original = Path.lstat
            def reparse(path):
                info = original(path)
                return types.SimpleNamespace(st_mode=info.st_mode, st_file_attributes=0x400) if path == previous else info
            with patch.object(Path, "lstat", reparse), self.assertRaisesRegex(RuntimeError, "Reparse"):
                f.prepare(self.root, "candidate", "parsed-warm", **kwargs)
            f.write(previous, dict(run, evidence=run["evidence"] + [{"kind": "file", "path": "WakeUp/ParsedXml/v1/../outside.bin"}]))
            with self.assertRaisesRegex(RuntimeError, "Unexpected parsed XML cache files"):
                f.prepare(self.root, "candidate", "parsed-warm", **kwargs)
            f.write(previous, run)
            self.assertFalse((self.root / "results/parsed-warm").exists())
            for label, choices in (("warm", selected), ("disabled", {"parsedXml": False}),
                                   ("bypass", {"cacheMaintenanceNextLaunch": "bypass"}),
                                   ("rebuild", {"cacheMaintenanceNextLaunch": "rebuild"}),
                                   ("clear", {"cacheMaintenanceNextLaunch": "clear"})):
                f.write(settings, choices)
                f.prepare(self.root, "candidate", "parsed-" + label, **kwargs)
                prepared = f.read(self.root / "results" / ("parsed-" + label) / "run.json")
                self.assertEqual((self.root / "profile/WakeUp/ParsedXml/v1" / entry.name).read_bytes(), b"parsed XML")
                self.assertFalse((self.root / "profile/Saves").exists())
                self.assertEqual(prepared["parsedXmlCacheSource"]["runSha256"], f.digest(previous))
                self.assertEqual(prepared["restoredCacheKinds"], ["wake-up-ParsedXml"])
                self.assertEqual(prepared["userSettings"], choices)
            with self.assertRaisesRegex(RuntimeError, "functional disabled/maintenance"):
                f.prepare(self.root, "candidate", "performance-clear", **dict(kwargs, purpose="performance"))
            launch.assert_not_called()

    def test_language_cache_restore_supports_functional_switching_with_exact_provenance(self):
        gen = self.root / "snapshots/test"
        seed = gen / "seed/Config"
        seed.mkdir(parents=True)
        (seed / "ModsConfig.xml").write_text("<ModsConfigData><activeMods /></ModsConfigData>")
        manifest = {"activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"], "generation": "test"}
        receipt = {"sourceRevision": "a" * 40, "runtimeContract": {"target": "gog-rev573"}}
        observer = {"supportsAutomaticExit": True, "sourceRevision": "observer"}
        f.write(self.root / "candidate.json", receipt)
        f.write(self.root / "menu-observer.json", observer)
        (self.root / "game/Mods" / f.PACKAGE_LEAF).mkdir(parents=True)
        profile = self.root / "results/language-cold/profile"
        store = profile / "WakeUp/ParsedLanguage/v1"
        store.mkdir(parents=True)
        entry = store / ("A" * 64 + ".bin")
        entry.write_bytes(b"language records")
        (store / ".owner").write_bytes(b"")
        (profile / "Saves").mkdir()
        (profile / "Saves/never-copy.rws").write_bytes(b"private save")
        signal = {"schema": "fixture-menu-ready.v1", "event": "menu-ready", "runId": "observed",
                  "processId": 123, "counterFrequency": 1000, "counter": 1100, "elapsedSeconds": 1.0}
        f.write(profile / "FixtureMenuObserver/menu-ready.json", signal)
        selected = {"parsedLanguage": True, "sharedCacheMiB": 1024}
        run = {"mode": "candidate", "activation": "user", "status": "captured-awaiting-human-and-runtime-review",
               "exitCode": 0, "automaticTestPassed": True, "lane": "representative", "manifestSha256": "snapshot",
               "generation": "test", "candidate": receipt, "activeOrder": f.run_order(manifest, "representative", "candidate", True),
               "excludedMods": [], "pngSource": "native", "menuObserver": True, "menuObserverPackage": observer,
               "exitAfterMenuReady": True, "menuObserverRunId": "observed", "menuObserverClock": {"frequency": 1000, "startCounter": 100},
               "pid": 123, "menuReadyObservation": signal, "evidence": f.inventory(profile),
               "userSettings": selected, "language": "French (Français)"}
        previous = profile.parent / "run.json"
        f.write(previous, run)
        settings = self.base / "language-settings.json"
        f.write(settings, selected)
        kwargs = dict(activation="user", purpose="functional", user_settings_path=settings,
                      language_cache_from="language-cold", language="English")
        with patch.object(f, "audit", return_value={"deployedRuntimeMatches": True}), \
                patch.object(f, "current", return_value=(manifest, gen, {"manifestSha256": "snapshot"})), \
                patch.object(f, "candidate_matches_snapshot", return_value=True), \
                patch.object(f, "git", return_value="a" * 40), patch.object(f.subprocess, "Popen") as launch:
            for key, value in (("candidate", {}), ("generation", "other"), ("manifestSha256", "changed-content"),
                               ("activeOrder", []), ("excludedMods", [{"id": "other"}]), ("menuObserverPackage", {})):
                f.write(previous, dict(run, **{key: value}))
                with self.subTest(key=key), self.assertRaisesRegex(RuntimeError, "configuration mismatch"):
                    f.prepare(self.root, "candidate", "language-warm", **kwargs)
            for values in ({"automaticTestPassed": False}, {"exitCode": 1}, {"userSettings": {"parsedLanguage": False}}):
                f.write(previous, dict(run, **values))
                with self.subTest(values=values), self.assertRaisesRegex(RuntimeError, "captured successful"):
                    f.prepare(self.root, "candidate", "language-warm", **kwargs)
            f.write(previous, run)
            with self.assertRaisesRegex(RuntimeError, "configuration mismatch"):
                f.prepare(self.root, "candidate", "language-performance-switch", **dict(kwargs, purpose="performance"))
            stamp = entry.stat()
            entry.write_bytes(b"tampered records")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            with self.assertRaisesRegex(RuntimeError, "Content drift"):
                f.prepare(self.root, "candidate", "language-warm", **kwargs)
            entry.write_bytes(b"language records")
            os.utime(entry, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
            f.write(previous, dict(run, evidence=run["evidence"] + [{"kind": "file", "path": "WakeUp/ParsedLanguage/v1/../outside.bin"}]))
            with self.assertRaisesRegex(RuntimeError, "Unexpected owned cache files"):
                f.prepare(self.root, "candidate", "language-warm", **kwargs)
            f.write(previous, run)
            for label, choices, language in (("warm", selected, "French (Français)"), ("away", selected, "English"),
                    ("back", selected, "French (Français)"), ("disabled", {"parsedLanguage": False}, "French (Français)"),
                    ("interaction", dict(selected, processedXml=True, resolvedInheritance=True), "French (Français)"),
                    ("bypass", {"cacheMaintenanceNextLaunch": "bypass"}, "French (Français)"),
                    ("rebuild", {"cacheMaintenanceNextLaunch": "rebuild"}, "French (Français)"),
                    ("clear", {"cacheMaintenanceNextLaunch": "clear"}, "French (Français)")):
                f.write(settings, choices)
                f.prepare(self.root, "candidate", "language-" + label, **dict(kwargs, language=language))
                prepared = f.read(self.root / "results" / ("language-" + label) / "run.json")
                self.assertEqual((self.root / "profile/WakeUp/ParsedLanguage/v1" / entry.name).read_bytes(), b"language records")
                self.assertFalse((self.root / "profile/Saves").exists())
                self.assertEqual(prepared["languageCacheFrom"], "language-cold")
                self.assertEqual(prepared["languageCacheSource"]["runSha256"], f.digest(previous))
                self.assertEqual(prepared["languageCacheSource"]["language"], "French (Français)")
                self.assertEqual(prepared["languageCacheSource"]["manifestSha256"], "snapshot")
                self.assertEqual(prepared["language"], language)
                self.assertEqual(prepared["restoredCacheKinds"], ["wake-up-ParsedLanguage"])
                self.assertEqual(prepared["extraXmlCacheSources"], {})
                self.assertEqual(prepared["userSettings"], choices)
            launch.assert_not_called()

    def test_reviewed_helper_overlay_preserves_core_and_refuses_other_bundle(self):
        location = self.root / "game/Mods" / f.PACKAGE_LEAF
        (location / "Assemblies").mkdir(parents=True)
        (location / "Assemblies/WakeUp.dll").write_bytes(b"core")
        f.write(self.root / "candidate.json", {"files": f.inventory(location)})
        bundle = self.root / "helper.zip"
        with f.zipfile.ZipFile(bundle, "w") as archive:
            archive.writestr("wake-up/Assemblies/WakeUp.dll", b"core")
            archive.writestr("wake-up/" + f.HELPER_PATH, b"helper")
        with patch.object(f, "audit"), patch.object(f, "swap", return_value="transaction") as swap:
            with self.assertRaisesRegex(RuntimeError, "Unreviewed helper"):
                f.deploy_helper(self.root, bundle)
            swap.assert_not_called()
            with patch.object(f, "REVIEWED_HELPER_BUNDLE", f.digest(bundle)), \
                    patch.object(f, "REVIEWED_HELPER_SHA", f.hashlib.sha256(b"helper").hexdigest()):
                result = f.deploy_helper(self.root, bundle)
                staged = swap.call_args.args[1][0][0]
                self.assertEqual((staged / "Assemblies/WakeUp.dll").read_bytes(), b"core")
                self.assertEqual((staged / f.HELPER_PATH).read_bytes(), b"helper")
                self.assertTrue(result["coreUnchanged"])
                # A repaired core reuses the independently pinned helper; no
                # bundled core bytes may overwrite that accepted deployment.
                (location / "Assemblies/WakeUp.dll").write_bytes(b"repaired")
                result = f.deploy_helper(self.root, bundle)
                staged = swap.call_args.args[1][0][0]
                self.assertEqual((staged / "Assemblies/WakeUp.dll").read_bytes(), b"repaired")
                self.assertNotEqual(result["bundleCoreSha256"], result["deployedCoreSha256"])

    def test_nonactivating_launch_requires_recorded_purpose_and_passes_windows_request(self):
        folder = self.root / "results/window"
        for purpose, style, show in (("functional", "minimized-noactivate", 7),
                                     ("functional", "noactivate", 4),
                                     ("performance", "minimized-noactivate", 7),
                                     ("performance", "noactivate", 4),
                                     (None, "minimized-noactivate", None)):
            f.write(folder / "run.json", {"status": "prepared-offline", "purpose": purpose,
                                          "executable": str(self.root / "game/fake.exe")})
            with patch.object(f, "verify_prepared", return_value={"command": ["never-executed"]}), \
                    patch.object(f.subprocess, "Popen", side_effect=OSError("stop before game")) as child:
                if show is None:
                    with self.assertRaisesRegex(RuntimeError, "explicitly recorded run purpose"):
                        f.launch(self.root, "window", True, style)
                    child.assert_not_called()
                else:
                    with self.assertRaisesRegex(OSError, "stop before game"):
                        f.launch(self.root, "window", True, style)
                    startup = child.call_args.kwargs["startupinfo"]
                    self.assertEqual(startup.wShowWindow, show)
                    self.assertTrue(startup.dwFlags & f.subprocess.STARTF_USESHOWWINDOW)
                    self.assertEqual(f.read(folder / "run.json")["launchWindowStyle"], style)

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
