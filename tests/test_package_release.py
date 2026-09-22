# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Release boundary checks; no game files or live fixture required."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import json
import io
import subprocess
import zipfile
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("package_release", Path(__file__).parents[1] / "scripts/package_release.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)


class ReleaseTests(unittest.TestCase):
    def test_preserved_helper_excludes_old_core_and_rejects_source_or_binary_drift(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            folder = root / "artifacts/releases/old/wake-up"
            name = "native/texture-helper/main.cpp"
            executable = "Tools/win-x64/WakeUp.TextureHelper.exe"
            source = io.BytesIO()
            with zipfile.ZipFile(source, "w") as archive: archive.writestr(name, b"old quality source")
            payload = {executable: b"qualified", "Source/source.zip": source.getvalue()}
            for path, data in payload.items():
                target = folder / path; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
            helper = dict(target="win-x64", handshakePassed=True, sha256=p.sha(b"qualified"), bytes=9,
                          inputs={name: p.sha(b"old quality source")}, sourceArchiveInputs={name: p.sha(b"old quality source")})
            manifest = dict(sourceRevision="a" * 40, optionalTextureHelper=helper,
                            files={name: p.sha(data) for name, data in payload.items()})
            (folder / "release-manifest.json").write_text(json.dumps(manifest))
            with mock.patch.object(p, "REPO", root), mock.patch.object(p, "git", side_effect=[b"", name.encode(), b"old quality source"]):
                files, summary = p.helper_overlay(folder)
                self.assertEqual(set(files), {executable, "Source/TextureHelper/source.zip"})
                self.assertEqual(summary["preservedSourceRevision"], "a" * 40)
            with mock.patch.object(p, "REPO", root), mock.patch.object(p, "git", side_effect=[b"", name.encode(), b"wrong source"]):
                with self.assertRaisesRegex(ValueError, "source drift"): p.helper_overlay(folder)
            (folder / executable).write_bytes(b"trial")
            with mock.patch.object(p, "REPO", root), mock.patch.object(p, "git", side_effect=[b"", name.encode()]):
                with self.assertRaisesRegex(ValueError, "payload drift"): p.helper_overlay(folder)

    def test_reused_build_requires_ancestry_and_rejects_changed_build_inputs(self):
        revision = "a" * 40
        with mock.patch.object(p, "git", side_effect=[b"", b"docs/current-state.md\0release/product.json\0scripts/package_release.py\0"]):
            p.verify_reused_build(revision, "b" * 40)
        for name in ["src/WakeUp/Runtime.cs", "Directory.Build.props", "build/Verify-LocalReferences.ps1",
                     "third-party/StbImageSharp-PSD.md", "validation/menu-observer/MenuObserver.csproj"]:
            with mock.patch.object(p, "git", side_effect=[b"", name.encode() + b"\0"]):
                with self.assertRaisesRegex(ValueError, "changed non-documentation"):
                    p.verify_reused_build(revision, "b" * 40)
        with mock.patch.object(p, "git", side_effect=subprocess.CalledProcessError(1, "git merge-base")):
            with self.assertRaises(subprocess.CalledProcessError):
                p.verify_reused_build(revision, "b" * 40)

    def test_release_description_changes_preserve_dependency_contract(self):
        before = b"<ModMetaData><description>Pending</description><modDependencies><li>original</li></modDependencies></ModMetaData>"
        after = before.replace(b"Pending", b"Bounded GOG proof; Steam pending")
        with mock.patch.object(p, "git", side_effect=[b"", b"release/About.xml\0", before, after]):
            p.verify_reused_build("a" * 40, "b" * 40)
        with mock.patch.object(p, "git", side_effect=[b"", b"release/About.xml\0", before, after.replace(b"original", b"changed")]):
            with self.assertRaisesRegex(ValueError, "About contract"):
                p.verify_reused_build("a" * 40, "b" * 40)

    def test_fixture_inspection_changes_do_not_waive_build_contract(self):
        before = b"def build(): return 'original'\ndef prepare(): return 1\ndef bind_comparison_package(): return 1\ndef gameplay_save_source(): return 1\n"
        after = before.replace(b"return 1", b"return 2")
        with mock.patch.object(p, "git", side_effect=[b"", b"scripts/fixture.py\0", before, after]):
            p.verify_reused_build("a" * 40, "b" * 40)
        with mock.patch.object(p, "git", side_effect=[b"", b"scripts/fixture.py\0", before, after.replace(b"original", b"changed")]):
            with self.assertRaisesRegex(ValueError, "build/tool inputs"):
                p.verify_reused_build("a" * 40, "b" * 40)
        with mock.patch.object(p, "git", side_effect=[b"", b"AGENTS.md\0tests/test_fixture.py\0"]):
            p.verify_reused_build("a" * 40, "b" * 40)

    def test_source_archive_preserves_committed_bytes_with_windows_conversion(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            def git(*args):
                return subprocess.check_output(["git", *args], cwd=root, stderr=subprocess.PIPE)
            git("init")
            git("config", "core.autocrlf", "true")
            (root / "input.txt").write_bytes(b"one\ntwo\n")
            git("add", "input.txt")
            git("-c", "user.name=Offline Test", "-c", "user.email=offline@example.invalid",
                "-c", "core.hooksPath=", "commit", "-m", "Synthetic source")
            with mock.patch.object(p, "REPO", root):
                with zipfile.ZipFile(io.BytesIO(p.source_zip("HEAD"))) as archive:
                    self.assertEqual(archive.read("input.txt"), git("show", "HEAD:input.txt"))

    def test_helper_source_archive_records_only_verified_line_ending_differences(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "helper.cpp").write_bytes(b"original\r\n")
            def source(data):
                output = io.BytesIO()
                with zipfile.ZipFile(output, "w") as archive:
                    archive.writestr("helper.cpp", data)
                return output.getvalue()
            helper = dict(inputs={"helper.cpp": p.sha(b"original\r\n")})
            with mock.patch.object(p, "REPO", root):
                p.verify_helper_source_archive(helper, source(b"original\n"))
                self.assertEqual(helper["sourceArchiveInputs"], {"helper.cpp": p.sha(b"original\n")})
                self.assertEqual(helper["sourceArchiveLineEndingOnlyDifferences"], ["helper.cpp"])
                with self.assertRaises(ValueError):
                    p.verify_helper_source_archive(helper, source(b"changed\n"))
                (root / "helper.cpp").write_bytes(b"drift\r\n")
                with self.assertRaises(ValueError):
                    p.verify_helper_source_archive(helper, source(b"original\n"))

    def test_private_and_binary_source_paths_rejected(self):
        for path in (".rlo-test-instance/game/a", "nested/artifacts/a", "x.dll", "../outside", "/absolute", ".git/config",
                     ".RLO-TEST-INSTANCE/game/a", "nested/Artifacts/a", "C:/private/file", "folder\\file"):
            self.assertFalse(p.safe_source_path(path), path)
        self.assertTrue(p.safe_source_path("src/Runtime.cs"))
        self.assertTrue(p.safe_source_path("LICENSE"))

    def test_input_drift_extra_binary_and_wrong_revision_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            rows = []
            for name in p.INPUT_FILES:
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(name.encode())
                rows.append(dict(path=name, kind="file", sha256=p.sha(target.read_bytes())))
            receipt = dict(sourceRevision="abc", files=rows)
            p.verify_input(root, receipt, "abc")
            with self.assertRaises(ValueError):
                p.verify_input(root, receipt, "different")
            extra = root / "Assemblies/0Harmony.dll"
            extra.write_bytes(b"unexpected")
            with self.assertRaises(ValueError):
                p.verify_input(root, receipt, "abc")
            extra.unlink()
            (root / "About/About.xml").write_bytes(b"changed")
            with self.assertRaises(ValueError):
                p.verify_input(root, receipt, "abc")

    def test_brand_change_preserves_dependency_and_incompatibility(self):
        product = dict(displayName="Future Name & More", packageId="kt.nyx.wakeup",
                       author="kt-nyx", version="0.2.0", sourceUrl="https://example.com/source")
        root = ET.fromstring(p.about_bytes(product, (p.REPO / "release/About.xml").read_bytes()))
        self.assertEqual(root.findtext("name"), product["displayName"])
        self.assertEqual([node.text for node in root.findall("modDependencies/li/packageId")], ["zetrith.prepatcher"])
        # S14: only duplicate legacy copies are unconditional conflicts. A
        # settings-dependent fallback must never become a blanket metadata ban.
        self.assertEqual([node.text for node in root.findall("incompatibleWith/li")],
                         ["local.rimworldloadingoptimizer.op7validation", "kt.nyx.startupfixes"])
        self.assertIsNone(root.find("incompatibleWithByVersion"))

    def test_optional_helper_requires_exact_inputs_notices_and_binary(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            folder = root / "artifacts/s10-helper/package/win-x64"
            executable = "Tools/win-x64/WakeUp.TextureHelper.exe"
            notice = "native/texture-helper/notices/LICENSE.txt"
            (root / notice).parent.mkdir(parents=True)
            (root / notice).write_bytes(b"notice")
            for name, data in {executable: b"helper", "Notices/TextureHelper/LICENSE.txt": b"notice"}.items():
                target = folder / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            receipt = dict(target="win-x64", handshakePassed=True, inputs={notice: p.sha(b"notice")},
                           executable=executable, bytes=6, sha256=p.sha(b"helper"), dependencies=[])
            (folder / "helper-build.json").write_text(json.dumps(receipt))
            with mock.patch.object(p, "REPO", root), mock.patch.object(p, "git", return_value=(notice + "\n").encode()):
                files, summary = p.helper_overlay(folder)
                self.assertNotIn("helper-build.json", files)
                self.assertFalse(summary["unityRenderingQualified"])
                (folder / executable).write_bytes(b"drift!")
                with self.assertRaises(ValueError):
                    p.helper_overlay(folder)


if __name__ == "__main__":
    unittest.main()
