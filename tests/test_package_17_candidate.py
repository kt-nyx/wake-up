# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Offline version selection and preservation checks; no game dependencies."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
spec = importlib.util.spec_from_file_location("package_17_candidate", ROOT / "scripts/package_17_candidate.py")
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)


class CandidateTests(unittest.TestCase):
    def test_one_package_selects_only_its_version_assembly_folder(self):
        folders = ET.fromstring(p.load_folders())
        self.assertEqual([v.text for v in folders.find("v1.6")], ["/", "1.6"])
        self.assertEqual([v.text for v in folders.find("v1.7")], ["1.7"])
        self.assertNotIn("/", [v.text for v in folders.find("v1.7")])

    def test_dependencies_and_exclusions_are_version_specific(self):
        legacy = b"<ModMetaData><modVersion>0.4.2</modVersion><supportedVersions><li>1.6</li></supportedVersions><modDependencies><li><packageId>zetrith.prepatcher</packageId></li></modDependencies><loadAfter><li>zetrith.prepatcher</li></loadAfter><incompatibleWith><li>private.copy</li></incompatibleWith><description>preserved</description></ModMetaData>"
        root = ET.fromstring(p.metadata(legacy, (ROOT / "release/1.7/About.xml").read_bytes()))
        self.assertEqual(root.findtext("description"), "preserved")
        self.assertEqual(root.findtext("modDependenciesByVersion/v1.6/li/packageId"), "zetrith.prepatcher")
        self.assertEqual(root.findtext("modDependenciesByVersion/v1.7/li/packageId"), "brrainz.harmony")
        self.assertIn("zetrith.prepatcher", [v.text for v in root.findall("incompatibleWithByVersion/v1.7/li")])
        self.assertEqual([v.text for v in root.findall("supportedVersions/li")], ["1.6", "1.7"])

    def test_legacy_payload_preserves_bytes_and_moves_assembly_out_of_root(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            files = {"Assemblies/WakeUp.dll": b"released-runtime", "Source/source.zip": b"released-source", "Source/source-revision.txt": b"old-revision", "About/About.xml": b"old-metadata"}
            for name, data in files.items():
                path = root / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
            manifest = dict(version="0.4.2", files={name: p.digest(data) for name, data in files.items()})
            (root / "release-manifest.json").write_text(json.dumps(manifest))
            expected = dict(preserved16Version="0.4.2", preserved16DllSha256=p.digest(files["Assemblies/WakeUp.dll"]))
            payload, _, mapping = p.legacy_payload(root, expected)
            self.assertEqual(payload["1.6/Assemblies/WakeUp.dll"], files["Assemblies/WakeUp.dll"])
            self.assertEqual(payload["Source/1.6/source.zip"], files["Source/source.zip"])
            self.assertNotIn("Assemblies/WakeUp.dll", payload)
            self.assertEqual(mapping["Source/source-revision.txt"], "Source/1.6/source-revision.txt")
            (root / "Assemblies/WakeUp.dll").write_bytes(b"drifted")
            with self.assertRaisesRegex(ValueError, "Changed preserved file"):
                p.legacy_payload(root, expected)

    def test_candidate_metadata_withholds_publication_and_live_qualification(self):
        product = json.loads((ROOT / "release/1.7/product.json").read_text())
        self.assertFalse(product["publicationAuthorized"])
        self.assertFalse(product["gameplayQualified"])
        self.assertFalse(product["performanceQualified"])


if __name__ == "__main__":
    unittest.main()
