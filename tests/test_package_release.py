# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Release boundary checks; no game files or live fixture required."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("package_release", Path(__file__).parents[1] / "scripts/package_release.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)


class ReleaseTests(unittest.TestCase):
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
        self.assertEqual(root.findtext("modDependencies/li/packageId"), "zetrith.prepatcher")
        self.assertEqual(root.findtext("incompatibleWith/li"), "local.rimworldloadingoptimizer.op7validation")
        self.assertIn("kt.nyx.startupfixes", [node.text for node in root.findall("incompatibleWith/li")])


if __name__ == "__main__":
    unittest.main()
