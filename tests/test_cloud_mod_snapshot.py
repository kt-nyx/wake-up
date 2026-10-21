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

    def test_retry_reuses_only_unchanged_copies_and_isolates_played_files(self):
        from unittest.mock import patch
        prior = self.root / 'failed-copy'
        snapshot.export_snapshot(self.source, self.request, prior)
        (prior / 'manifest.json').unlink()  # Failed captures need no manifest.
        changed = self.custom / 'content.xml'
        changed.write_text('new content')
        output = self.root / 'retry'
        links = []
        original_link = snapshot.os.link

        def link(source, target):
            links.append((Path(source), Path(target)))
            return original_link(source, target)

        with patch.object(snapshot.os, 'link', side_effect=link):
            snapshot.export_snapshot(self.source, self.request, output, reuse_files=prior)
        unchanged = 'packages/test.library/content.xml'
        self.assertIn((prior / unchanged, output / unchanged), links)
        self.assertTrue((prior / unchanged).samefile(output / unchanged))
        self.assertFalse((self.workshop / '123/content.xml').samefile(output / unchanged))
        relative = 'packages/test.custom/content.xml'
        self.assertEqual((output / relative).read_text(), 'new content')
        self.assertEqual((prior / relative).read_text(), 'latest bytes')
        self.assertFalse((prior / relative).samefile(output / relative))
        self.assertFalse(changed.samefile(output / relative))
        (output / unchanged).write_text('snapshot-only edit')
        self.assertEqual((self.workshop / '123/content.xml').read_text(), 'latest bytes')
        changed.write_text('later played change')
        self.assertEqual((output / relative).read_text(), 'new content')
        for reuse in (self.game, self.workshop, self.profile, prior.parent):
            with self.assertRaisesRegex(ValueError, 'separate'):
                snapshot.export_snapshot(self.source, self.request, self.root / 'rejected', reuse_files=reuse)
        fallback = self.root / 'fallback'
        fallback_reuse = self.root / 'fallback-reuse'
        shutil.copytree(prior, fallback_reuse)
        with patch.object(snapshot.os, 'link', side_effect=OSError('unsupported')) as unsupported:
            snapshot.export_snapshot(self.source, self.request, fallback, reuse_files=fallback_reuse)
        self.assertTrue(unsupported.called)
        self.assertEqual((fallback / relative).read_text(), 'later played change')
        self.assertFalse((fallback / relative).samefile(changed))

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

    def test_store_inventory_reuses_removed_and_readded_content(self):
        request = dict(self.request, selection=dict(mode='full', reason='Retain full collection'))
        unique = self.workshop / '456/content.xml'
        unique.write_text('unique removed and re-added package bytes')
        original_config = (self.profile / 'Config/ModsConfig.xml').read_text()
        first = self.root / 'first'
        original = snapshot.export_snapshot(self.source, request, first)
        store, parts = self.root / 'store', self.root / 'first-parts'
        snapshot_store.pack(first, parts, part_bytes=500)
        self.assertNotIn('additionalReuseHashes', snapshot.read(parts / 'transfer-index.json'))
        snapshot_store.receive(parts / 'transfer-index.json', parts, store)
        (self.profile / 'Config/ModsConfig.xml').write_text(original_config.replace('<li>test.unrelated</li>', ''))
        second = self.root / 'second'
        parent = snapshot.export_snapshot(self.source, request, second)
        removed_parts = self.root / 'removed-parts'
        snapshot_store.pack(second, removed_parts, first / 'manifest.json', part_bytes=500)
        snapshot_store.receive(removed_parts / 'transfer-index.json', removed_parts, store)
        inventory = snapshot_store.inventory(store)
        self.assertEqual(set(inventory['snapshotIds']), {original['snapshotId'], parent['snapshotId']})
        available = self.root / 'have.json'
        snapshot.write(available, inventory)
        (self.profile / 'Config/ModsConfig.xml').write_text(original_config)
        third = self.root / 'third'
        restored = snapshot.export_snapshot(self.source, request, third)
        update = self.root / 'readd-parts'
        receipt = snapshot_store.pack(third, update, second / 'manifest.json', part_bytes=500, have_inventory=available)
        self.assertEqual(receipt['objectsTransferred'], 0)
        index = snapshot.read(update / 'transfer-index.json')
        self.assertEqual(index['payloadUncompressedBytes'], 0)
        key = snapshot.digest(unique)
        self.assertIn(key, index['additionalReuseHashes'])
        retained = snapshot_store.object_path(store, key)
        payload = retained.read_bytes()
        retained.chmod(0o600)
        retained.unlink()
        with self.assertRaisesRegex(ValueError, 'Missing required payload'):
            snapshot_store.receive(update / 'transfer-index.json', update, store)
        self.assertFalse(snapshot_store.manifest_path(store, restored['snapshotId']).exists())
        with self.assertRaisesRegex(ValueError, 'Missing required payload'):
            snapshot_store.inventory(store)
        retained.write_bytes(payload)
        retained.chmod(0o444)
        snapshot_store.receive(update / 'transfer-index.json', update, store)
        snapshot_store.verify(store, restored['snapshotId'])

    def test_administrative_exclusions_and_legacy_runtime_projection(self):
        import hashlib, json
        (self.custom / '1.7').mkdir()
        (self.custom / '1.7/runtime.xml').write_text('<runtime/>')
        (self.custom / 'LoadFolders.xml').write_text('<loadFolders><v1.7><li>1.7</li></v1.7></loadFolders>')
        clean = self.root / 'clean'
        raw = snapshot.export_snapshot(self.source, self.request, clean)
        self.assertEqual(snapshot_store.runtime_manifest(raw)['snapshotId'], raw['snapshotId'])
        metadata = {'.git/config': 'credential-placeholder', '.git/worktrees/one/.env': 'private-placeholder',
                    '.hg/hgrc': 'history-placeholder', '.svn/wc.db': 'history-placeholder',
                    '.gitmodules': 'metadata-placeholder', '.gitconfig': 'metadata-placeholder'}
        for relative, text in metadata.items():
            path = self.custom / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text)
            # Simulate the already-complete legacy raw snapshot without recapture.
            target = clean / 'packages/test.custom' / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(text)
            raw['files'].append(dict(path='packages/test.custom/' + relative, bytes=target.stat().st_size,
                                     sha256=snapshot.digest(target)))
        raw.pop('snapshotId')
        raw['snapshotId'] = 'rw-' + hashlib.sha256(json.dumps(raw, sort_keys=True).encode()).hexdigest()[:16]
        snapshot.write(clean / 'manifest.json', raw)
        original_manifest_bytes = (clean / 'manifest.json').read_bytes()
        parts, store = self.root / 'projected-parts', self.root / 'store'
        from collections import Counter
        from unittest.mock import patch
        opened = Counter()
        original_open = Path.open

        def guarded_open(path, *args, **kwargs):
            if path.is_relative_to(clean):
                relative = path.relative_to(clean).as_posix()
                self.assertFalse(relative.startswith('packages/test.custom/')
                                 and snapshot.administrative_rule(relative.removeprefix('packages/test.custom/')))
                opened[relative] += 1
            return original_open(path, *args, **kwargs)

        with patch.object(Path, 'open', guarded_open):
            packed = snapshot_store.pack(clean, parts, part_bytes=500)
        self.assertNotEqual(packed['snapshotId'], raw['snapshotId'])
        projected = snapshot.read(parts / 'snapshot-manifest.json')
        self.assertTrue(all(opened[row['path']] == 1 for row in projected['files']))
        self.assertEqual(projected['sourceRawCaptureId'], raw['snapshotId'])
        self.assertEqual(projected['activeOrder'], raw['activeOrder'])
        self.assertEqual(projected['omitted'], raw['omitted'])
        self.assertEqual(projected['dependenciesAdded'], raw['dependenciesAdded'])
        excluded = projected['administrativeExclusions'][0]
        self.assertEqual(excluded['packageId'], 'test.custom')
        self.assertEqual(excluded['fileCount'], len(metadata))
        self.assertEqual(excluded['bytes'], sum(len(text.encode()) for text in metadata.values()))
        self.assertEqual({p['path'] for p in excluded['paths']}, set(metadata))
        index = snapshot.read(parts / 'transfer-index.json')
        metadata_hashes = {snapshot.digest(clean / 'packages/test.custom' / name) for name in metadata}
        self.assertTrue(metadata_hashes.isdisjoint(index['payloadHashes']))
        snapshot_store.receive(parts / 'transfer-index.json', parts, store)
        received = snapshot_store.verify(store, packed['snapshotId'])
        self.assertEqual(received, projected)
        self.assertFalse(snapshot_store.manifest_path(store, raw['snapshotId']).exists())
        self.assertEqual((clean / 'manifest.json').read_bytes(), original_manifest_bytes)
        snapshot.verify_snapshot(clean)
        output = self.root / 'filtered-export'
        filtered = snapshot.export_snapshot(self.source, self.request, output)
        self.assertFalse((output / 'packages/test.custom/.git').exists())
        self.assertTrue((output / 'packages/test.custom/1.7/runtime.xml').is_file())
        self.assertTrue((output / 'packages/test.custom/LoadFolders.xml').is_file())
        self.assertEqual(filtered['administrativeExclusions'], projected['administrativeExclusions'])
        fixture = self.root / '.rlo-test-instance/projected/game'
        shutil.copytree(self.game / 'Data', fixture / 'Data')
        shutil.copy2(self.game / 'Assembly-CSharp.dll', fixture / 'Assembly-CSharp.dll')
        snapshot_store.stage(store, packed['snapshotId'], fixture, fixture.parent / 'profile')
        self.assertFalse((fixture / 'Mods/Custom/.git').exists())
        self.assertTrue((fixture / 'Mods/Custom/1.7/runtime.xml').is_file())
        self.assertTrue((fixture / 'Mods/Custom/LoadFolders.xml').is_file())
        for name in ('.env', 'account.config', 'loginusers.vdf', 'id_ed25519', 'private-key.pem'):
            candidate = self.custom / name
            candidate.write_text('credential-placeholder')
            with self.subTest(credential=name):
                with self.assertRaisesRegex(ValueError, 'Credential-file candidate'):
                    snapshot.export_snapshot(self.source, self.request, self.root / ('reject-' + name))
            candidate.unlink()

    def test_pack_checks_duplicate_bytes_before_emitting_final_outputs(self):
        output = self.root / 'frozen'
        snapshot.export_snapshot(self.source, self.request, output)
        representative = output / 'packages/test.library/content.xml'
        duplicate = output / 'packages/test.custom/content.xml'
        self.assertEqual(snapshot.digest(representative), snapshot.digest(duplicate))
        duplicate.write_bytes(b'X' * duplicate.stat().st_size)
        parts = self.root / 'broken-parts'
        with self.assertRaisesRegex(ValueError, 'Changed snapshot file: packages/test.custom/content.xml'):
            snapshot_store.pack(output, parts, part_bytes=500)
        self.assertEqual(representative.read_text(), 'latest bytes')
        for name in ('snapshot-manifest.json', 'transfer-index.json', 'pack-receipt.json'):
            self.assertFalse((parts / name).exists())
        self.assertFalse(list(parts.glob('*-index.zip')))


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
        with self.subTest('redirected store'):
            try:
                redirected.symlink_to(real, target_is_directory=True)
            except OSError as error:
                if getattr(error, 'winerror', None) == 1314:
                    self.skipTest('Windows symlink creation requires privilege')
                raise
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
