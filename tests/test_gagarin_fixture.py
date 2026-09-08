# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Focused selector/optional-supplier checks; no live fixture operations."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("gagarin_fixture", Path(__file__).resolve().parents[1] / "scripts/fixture.py")
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)


class GagarinSelectionTests(unittest.TestCase):
    def test_only_cache_selector_changes_between_arms(self):
        root = Path("unused-fixture")
        control = f.run_arguments(root, "candidate", "timing", gagarin_cache="timing")
        candidate = f.run_arguments(root, "candidate", "timing", gagarin_cache="on")
        self.assertEqual([x for x in control if not x.startswith("--wake-up-gagarin-cache=")],
                         [x for x in candidate if not x.startswith("--wake-up-gagarin-cache=")])
        self.assertIn("--wake-up-strategy=startup-searches", candidate)
        self.assertIn("--wake-up-gagarin-cache=on", candidate)
        self.assertNotIn("--wake-up-gagarin-cache=on", f.run_arguments(root, "absent"))
        with self.assertRaises(RuntimeError):
            f.run_arguments(root, "absent", gagarin_cache="on")

    def test_expanded_selection_retains_frozen_order_and_requires_supplier(self):
        suppliers = sorted(f.XML_EXPANDED_MODS)
        active = ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld", "unrelated"] + suppliers
        manifest = {"activeOrder": active, "official": [{"id": "ludeon.rimworld"}]}
        actual = f.run_order(manifest, "xml-expanded", "candidate", True)
        self.assertEqual(actual, active[:2] + [f.MENU_ID, f.PACKAGE_ID] + [active[2]] + suppliers)
        with self.assertRaises(RuntimeError):
            f.run_order(manifest, "xml-expanded", "candidate", excluded_ids=["vr.missilegirl"])


if __name__ == "__main__":
    unittest.main()
