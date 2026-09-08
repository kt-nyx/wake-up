# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Explicit PNG research selection and original-source isolation."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('png_fixture', Path(__file__).resolve().parents[1] / 'scripts/op7_fixture.py')
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)

class PngSelectionTests(unittest.TestCase):
    def test_explicit_comparison_and_ordinary_default(self):
        root = Path('unused-fixture')
        ordinary = f.run_arguments(root, 'candidate')
        self.assertFalse(any('png' in s for s in ordinary))
        control = f.run_arguments(root, 'candidate', png='control', png_source='original')
        trial = f.run_arguments(root, 'candidate', png='cache', png_source='original')
        self.assertEqual([s for s in control if not s.startswith('--rlo-png=')], [s for s in trial if not s.startswith('--rlo-png=')])
        with self.assertRaises(RuntimeError): f.run_arguments(root, 'absent', png='cache')
        with self.assertRaises(RuntimeError): f.run_arguments(root, 'candidate', png_source='original')

if __name__ == '__main__': unittest.main()
