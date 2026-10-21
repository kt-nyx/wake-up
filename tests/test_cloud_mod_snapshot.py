"""Exercise frozen local/Workshop collections and their private transfer boundary."""
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

TOOL = Path(__file__).resolve().parents[1] / 'cloud/rimworld'
sys.path.insert(0, str(TOOL))
import mod_snapshot as snapshot
import transfer
import snapshot_store


class SnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root / 'played-game'
        self.profile = self.root / 'played-profile'
        self.workshop = self.root / 'workshop'
        self.game.mkdir()
        self.workshop.mkdir()
        (self.profile / 'Config').mkdir(parents=True)
        (self.game / 'Version.txt').write_text('1.7.4982 rev1280')
        (self.game / 'Assembly-CSharp.dll').write_bytes(b'exact game reference')
        self.package(self.game / 'Data/Core', 'ludeon.rimworld')
        self.package(self.workshop / '123', 'test.library')
        self.custom = self.game / 'Mods/Custom'
        self.package(self.custom, 'test.custom', 'test.library')
        self.package(self.workshop / '456', 'test.unrelated')
        (self.profile / 'Config/ModsConfig.xml').write_text('<ModsConfigData><activeMods>' + ''.join(
            f'<li>{x}</li>' for x in ['ludeon.rimworld', 'test.library_steam', 'test.custom', 'test.unrelated']) + '</activeMods></ModsConfigData>')
        (self.profile / 'Config/Mod_Custom_Settings.xml').write_text('<settings><value>current</value></settings>')
        (self.profile / 'Config/Mod_456_Unrelated.xml').write_text('<settings/>')
        self.source = dict(schema='rimworld-local-source.v1', gameDir=str(self.game), workshopDir=str(self.workshop), profileDir=str(self.profile))
        self.request = dict(schema='rimworld-cloud-test-request.v1', requestId='test-1', purpose='compatibility',
                            selection=dict(mode='subset', packageIds=['test.custom'], reason='Exercise custom mod with its dependency'))

    def package(self, path, pid, dependency=None):
        (path / 'About').mkdir(parents=True)
        dep = f'<modDependencies><li><packageId>{dependency}</packageId></li></modDependencies>' if dependency else ''
        (path / 'About/About.xml').write_text(f'<ModMetaData><packageId>{pid}</packageId>{dep}</ModMetaData>')
        (path / 'content.xml').write_text('latest bytes')

    def test_subset_dependency_order_settings_and_roundtrip_stage(self):
        output = self.root / 'frozen'
        manifest = snapshot.export_snapshot(self.source, self.request, output)
        self.assertEqual(manifest['activeOrder'], ['ludeon.rimworld', 'test.library', 'test.custom'])
        self.assertEqual(manifest['dependenciesAdded'], ['test.library'])
        self.assertFalse((output / 'packages/test.unrelated').exists())
        self.assertTrue((output / 'profile/Config/Mod_Custom_Settings.xml').is_file())
        self.assertFalse((output / 'profile/Config/Mod_456_Unrelated.xml').exists())
        parts = self.root / 'parts'
        transfer.pack(output, parts, part_bytes=500)
        restored = self.root / 'restored'
        transfer.unpack(parts / 'transfer-index.json', parts, restored)
        with self.assertRaisesRegex(ValueError, 'isolated'):
            snapshot.stage_snapshot(restored, self.game, self.profile)
        fixture = self.root / '.rlo-test-instance/case/game'
        shutil.copytree(self.game / 'Data', fixture / 'Data')
        shutil.copy2(self.game / 'Assembly-CSharp.dll', fixture / 'Assembly-CSharp.dll')
        profile = self.root / '.rlo-test-instance/case/profile'
        snapshot.stage_snapshot(restored, fixture, profile)
        self.assertEqual((fixture / 'Mods/Custom/content.xml').read_text(), 'latest bytes')
        self.assertEqual((profile / 'Config/Mod_Custom_Settings.xml').read_text(), '<settings><value>current</value></settings>')
        self.assertTrue((self.custom / 'content.xml').is_file())
        with self.assertRaisesRegex(ValueError, 'fresh'):
            snapshot.stage_snapshot(restored, fixture, profile)

    def test_new_capture_gets_updates_without_mutating_frozen_input(self):
        first = self.root / 'first'
        snapshot.export_snapshot(self.source, self.request, first)
        (self.custom / 'content.xml').write_text('newer Workshop/custom source state')
        second = self.root / 'second'
        snapshot.export_snapshot(self.source, self.request, second)
        self.assertEqual((first / 'packages/test.custom/content.xml').read_text(), 'latest bytes')
        self.assertEqual((second / 'packages/test.custom/content.xml').read_text(), 'newer Workshop/custom source state')
        snapshot.verify_snapshot(first)
        parent = snapshot.verify_snapshot(second)
        child_request = dict(self.request, sourceSnapshotId=parent['snapshotId'],
                             selection=dict(mode='subset', packageIds=['test.library'], reason='Narrow follow-up'))
        child = self.root / 'child'
        result = snapshot.subset_snapshot(second, child_request, child)
        self.assertEqual(result['activeOrder'], ['ludeon.rimworld', 'test.library'])
        self.assertFalse((child / 'packages/test.custom').exists())
        self.assertFalse((child / 'profile/Config/Mod_Custom_Settings.xml').exists())

    def test_missing_or_ambiguous_package_is_reported_without_silent_substitution(self):
        shutil.rmtree(self.workshop / '123')
        with self.assertRaisesRegex(ValueError, 'unavailable locally: test.library'):
            snapshot.export_snapshot(self.source, self.request, self.root / 'missing')
        self.package(self.workshop / '123', 'test.library')
        self.package(self.game / 'Mods/OtherCustom', 'test.custom')
        with self.assertRaisesRegex(ValueError, 'Ambiguous'):
            snapshot.export_snapshot(self.source, self.request, self.root / 'ambiguous')

    def test_current_version_dependencies_replace_legacy_default_dependencies(self):
        about = self.custom / 'About/About.xml'
        about.write_text('<ModMetaData><packageId>test.custom</packageId>'
            '<modDependencies><li><packageId>old.runtime</packageId></li></modDependencies>'
            '<modDependenciesByVersion><v1.7><li><packageId>test.library</packageId></li></v1.7>'
            '</modDependenciesByVersion></ModMetaData>')
        manifest = snapshot.export_snapshot(self.source, self.request, self.root / 'versioned')
        self.assertEqual(manifest['dependenciesAdded'], ['test.library'])

    def test_credential_settings_and_changed_snapshots_are_rejected(self):
        settings = self.profile / 'Config/Mod_Custom_Settings.xml'
        settings.write_text('<settings><apiKey>private-placeholder</apiKey></settings>')
        with self.assertRaisesRegex(ValueError, 'credential field'):
            snapshot.export_snapshot(self.source, self.request, self.root / 'credentials')
        settings.write_text('<settings/>')
        output = self.root / 'good'
        snapshot.export_snapshot(self.source, self.request, output)
        (output / 'packages/test.custom/content.xml').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'Changed snapshot file'):
            snapshot.verify_snapshot(output)

    def test_store_initial_incremental_subset_and_complete_staging(self):
        first = self.root / 'first'
        original = snapshot.export_snapshot(self.source, self.request, first)
        store = self.root / 'retained-private-store'
        parts = self.root / 'first-parts'
        initial = snapshot_store.pack(first, parts, part_bytes=500)
        snapshot_store.receive(parts / 'transfer-index.json', parts, store)
        # Rename/remove/add package files, and change selected settings and order.
        (self.custom / 'content.xml').rename(self.custom / 'renamed.xml')
        (self.workshop / '123/content.xml').unlink()
        (self.custom / 'new.xml').write_text('added bytes')
        (self.profile / 'Config/Mod_Custom_Settings.xml').write_text('<settings><value>changed</value></settings>')
        (self.profile / 'Config/ModsConfig.xml').write_text('<ModsConfigData><activeMods>' + ''.join(
            f'<li>{p}</li>' for p in ['test.library', 'ludeon.rimworld', 'test.custom', 'test.unrelated']) + '</activeMods></ModsConfigData>')
        second = self.root / 'second'
        updated = snapshot.export_snapshot(self.source, self.request, second)
        update_parts = self.root / 'update-parts'
        delta = snapshot_store.pack(second, update_parts, first / 'manifest.json', part_bytes=500)
        self.assertLess(delta['objectsTransferred'], initial['objectsTransferred'])
        with self.assertRaisesRegex(ValueError, 'Missing required snapshot'):
            snapshot_store.receive(update_parts / 'transfer-index.json', update_parts, self.root / 'empty-store')
        snapshot_store.receive(update_parts / 'transfer-index.json', update_parts, store)
        snapshot_store.verify(store, original['snapshotId'])
        snapshot_store.verify(store, updated['snapshotId'])
        child = snapshot_store.subset(store, updated['snapshotId'], dict(self.request,
            sourceSnapshotId=updated['snapshotId']))
        self.assertEqual(child['activeOrder'], ['test.library', 'ludeon.rimworld', 'test.custom'])
        fixture = self.root / '.rlo-test-instance/store-case/game'
        shutil.copytree(self.game / 'Data', fixture / 'Data')
        shutil.copy2(self.game / 'Assembly-CSharp.dll', fixture / 'Assembly-CSharp.dll')
        profile = fixture.parent / 'profile'
        snapshot_store.stage(store, updated['snapshotId'], fixture, profile)
        self.assertFalse((fixture / 'Mods/123/content.xml').exists())
        self.assertFalse((fixture / 'Mods/Custom/content.xml').exists())
        self.assertEqual((fixture / 'Mods/Custom/renamed.xml').read_text(), 'latest bytes')
        self.assertEqual((fixture / 'Mods/Custom/new.xml').read_text(), 'added bytes')
        with self.assertRaisesRegex(ValueError, 'fresh'):
            snapshot_store.stage(store, updated['snapshotId'], fixture, profile)
        (fixture / 'Mods/Custom/new.xml').write_text('corrupted')
        with self.assertRaisesRegex(ValueError, 'Changed deployed file'):
            snapshot_store.verify_stage(store, updated['snapshotId'], fixture, profile)

    def test_store_missing_or_corrupt_payload_cannot_publish_snapshot(self):
        source = self.root / 'source'
        manifest = snapshot.export_snapshot(self.source, self.request, source)
        parts = self.root / 'parts'
        snapshot_store.pack(source, parts, part_bytes=500)
        first_part = next(parts.glob('*-part*.zip'))
        first_part.write_bytes(b'broken')
        store = self.root / 'store'
        with self.assertRaisesRegex(ValueError, 'Wrong ZIP part'):
            snapshot_store.receive(parts / 'transfer-index.json', parts, store)
        self.assertFalse(snapshot_store.manifest_path(store, manifest['snapshotId']).exists())


    def test_store_rejects_noncanonical_paths_and_redirected_store(self):
        import hashlib, json
        output = self.root / 'frozen'
        manifest = snapshot.export_snapshot(self.source, self.request, output)
        manifest['files'][0]['path'] = 'packages//test.custom/content.xml'
        body = {k: v for k, v in manifest.items() if k != 'snapshotId'}
        manifest['snapshotId'] = 'rw-' + hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()[:16]
        with self.assertRaisesRegex(ValueError, 'noncanonical'):
            snapshot_store.validate(manifest)
        real = self.root / 'real-store'
        real.mkdir()
        redirected = self.root / 'redirected-store'
        redirected.symlink_to(real, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'Linked or redirected'):
            snapshot_store.object_path(redirected, 'a' * 64)

    def test_store_requires_declared_payload_and_exact_settings(self):
        output = self.root / 'frozen'
        manifest = snapshot.export_snapshot(self.source, self.request, output)
        parts, store = self.root / 'parts', self.root / 'store'
        snapshot_store.pack(output, parts, part_bytes=500)
        snapshot_store.receive(parts / 'transfer-index.json', parts, store)
        index = snapshot.read(parts / 'transfer-index.json')
        index['payloadHashes'] = []
        snapshot.write(parts / 'bad-index.json', index)
        with self.assertRaisesRegex(ValueError, 'required parent'):
            snapshot_store.receive(parts / 'bad-index.json', parts, store)
        fixture = self.root / '.rlo-test-instance/store-case/game'
        shutil.copytree(self.game / 'Data', fixture / 'Data')
        shutil.copy2(self.game / 'Assembly-CSharp.dll', fixture / 'Assembly-CSharp.dll')
        profile = fixture.parent / 'profile'
        snapshot_store.stage(store, manifest['snapshotId'], fixture, profile)
        (profile / 'Config/Unexpected.xml').write_text('<settings/>')
        with self.assertRaisesRegex(ValueError, 'Unlisted deployed settings'):
            snapshot_store.verify_stage(store, manifest['snapshotId'], fixture, profile)


if __name__ == '__main__':
    unittest.main()
