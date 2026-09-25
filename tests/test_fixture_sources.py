# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

"""Explicit small-fixture selection, using synthetic files only."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("fixture", Path(__file__).parents[1] / "scripts/fixture.py")
f = importlib.util.module_from_spec(spec)
spec.loader.exec_module(f)


class ExplicitSourcesTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "fixture"
        self.game = self.root / "game"
        (self.game / "RimWorldWin64_Data/Managed").mkdir(parents=True)
        (self.game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll").write_bytes(b"synthetic-game")
        (self.game / "RimWorldWin64.exe").write_bytes(b"not executable")
        (self.game / "goggame-1094900565.info").write_text("synthetic marker")
        (self.game / "Version.txt").write_text("1.6.1234 rev1")
        self.mod(self.game / "Data/Core", "ludeon.rimworld")
        self.mod(self.game / "Data/UnusedDLC", "ludeon.unused")
        self.prepatcher = self.mod(self.base / "sources/prepatcher", "zetrith.prepatcher")
        self.harmony = self.mod(self.base / "sources/harmony", "brrainz.harmony")
        self.value = {"schema": "rlo-fixture-sources.v1", "packages": [self.prepatcher, self.harmony],
                      "official": ["ludeon.rimworld"],
                      "activeOrder": ["zetrith.prepatcher", "brrainz.harmony", "ludeon.rimworld"]}
        self.plan = self.base / "sources.json"
        self.save()

    def mod(self, path, pid):
        (path / "About").mkdir(parents=True)
        (path / "About/About.xml").write_text(f"<ModMetaData><packageId>{pid}</packageId></ModMetaData>")
        (path / "payload").write_bytes(path.name.encode())
        return {"path": str(path), "packageId": pid}

    def save(self, value=None):
        f.write(self.plan, self.value if value is None else value)

    def test_explicit_initialize_refresh_preserves_game_and_never_reads_normal_profile(self):
        before = f.inventory(self.game)
        with patch.object(f, "discover", side_effect=AssertionError("normal discovery forbidden")), \
                patch.object(f, "git", return_value="a" * 40), \
                patch.object(f, "DEVELOPMENT_GAME", f.digest(self.game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")):
            f.initialize(self.root, sources_path=self.plan)
            self.assertEqual(f.audit(self.root, True)["mods"], 2)
            self.assertEqual(f.inventory(self.game, exclude=("Mods",)), before)
            self.assertEqual([p.relative_to(self.root / "profile").as_posix()
                              for p in (self.root / "profile").rglob("*") if p.is_file()], ["Config/ModsConfig.xml"])
            self.assertEqual(f.order(self.root / "profile/Config/ModsConfig.xml"), self.value["activeOrder"])
            m, _, _ = f.current(self.root)
            self.assertIsNone(m["steamAssemblySha256"])
            self.assertEqual([p["id"] for p in m["official"]], ["ludeon.rimworld"])
            with self.assertRaisesRegex(RuntimeError, "requires --sources"):
                f.initialize(self.root, refresh=True)
            for edition in ("original", "continued"):
                supplier = self.mod(self.base / "sources" / edition, "shared.loader")
                self.value["packages"] = [self.prepatcher, self.harmony, supplier]
                self.value["activeOrder"] = ["zetrith.prepatcher", "brrainz.harmony", "shared.loader", "ludeon.rimworld"]
                self.save()
                f.initialize(self.root, refresh=True, sources_path=self.plan)
                self.assertEqual(f.audit(self.root, True)["mods"], 3)
                self.assertEqual({p.name for p in (self.game / "Mods").iterdir()}, {"prepatcher", "harmony", edition})
                self.assertEqual(f.inventory(self.game, exclude=("Mods",)), before)

    def test_duplicate_editions_and_folder_collisions_are_rejected(self):
        a = self.mod(self.base / "one/edition", "shared.loader")
        b = self.mod(self.base / "two/other", "shared.loader")
        c = self.mod(self.base / "three/edition", "different.loader")
        for suppliers, message in (([a, b], "Duplicate source package IDs"), ([a, c], "Folder-name collision")):
            value = copy.deepcopy(self.value)
            value["packages"] += suppliers
            self.save(value)
            with self.assertRaisesRegex(RuntimeError, message):
                f.explicit_sources(self.root, self.plan)

    def test_source_identity_order_and_official_selection_refuse_invalid_inputs(self):
        changes = [
            ("packages", [{"path": "relative", "packageId": "sample"}]),
            ("packages", [dict(self.harmony, packageId="wrong.identity")]),
            ("official", ["ludeon.rimworld", "missing.dlc"]),
            ("official", ["ludeon.rimworld", "ludeon.rimworld"]),
            ("activeOrder", ["brrainz.harmony", "zetrith.prepatcher", "ludeon.rimworld"]),
            ("activeOrder", self.value["activeOrder"] + ["missing.mod"]),
            ("activeOrder", self.value["activeOrder"] + ["ludeon.rimworld"]),
        ]
        for key, replacement in changes:
            with self.subTest(key=key, value=replacement):
                self.save(dict(self.value, **{key: replacement}))
                with self.assertRaises(RuntimeError):
                    f.explicit_sources(self.root, self.plan)

    def test_fixture_overlap_reserved_product_and_link_sources_are_rejected(self):
        nested = self.mod(self.root / "staging/source", "nested.mod")
        ancestor = self.mod(self.base, "ancestor.mod")
        reserved = self.mod(self.base / "reserved", f.PACKAGE_ID)
        for supplier in (nested, ancestor, reserved):
            self.save(dict(self.value, packages=[supplier]))
            with self.assertRaises(RuntimeError):
                f.explicit_sources(self.root, self.plan)
        link = self.base / "linked"
        try:
            link.symlink_to(Path(self.harmony["path"]), target_is_directory=True)
        except OSError:
            return  # Windows may not grant symlink creation; overlap tests still ran.
        self.save(dict(self.value, packages=[dict(self.harmony, path=str(link))]))
        with self.assertRaisesRegex(RuntimeError, "Reparse point"):
            f.explicit_sources(self.root, self.plan)

    def test_changed_specification_is_not_published_or_resumed(self):
        original = f.clean_profile
        def changing_profile(*args):
            result = original(*args)
            self.value["official"].append("ludeon.unused")
            self.save()
            return result
        with patch.object(f, "git", return_value="a" * 40), \
                patch.object(f, "DEVELOPMENT_GAME", f.digest(self.game / "RimWorldWin64_Data/Managed/Assembly-CSharp.dll")):
            with patch.object(f, "clean_profile", side_effect=changing_profile):
                with self.assertRaisesRegex(RuntimeError, "Source discovery/order/build changed"):
                    f.initialize(self.root, sources_path=self.plan)
            self.assertFalse((self.root / "current.json").exists())
            staged = next((self.root / "staging").iterdir())
            with self.assertRaisesRegex(RuntimeError, "specification changed"):
                f.initialize(self.root, resume=staged.name, sources_path=self.plan)


if __name__ == "__main__":
    unittest.main()
