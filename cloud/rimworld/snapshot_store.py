#!/usr/bin/env python3
"""Private compressed snapshot storage and incremental, hash-verified transport.

Snapshots remain complete manifests. Objects are compressed whole file contents,
named by their uncompressed SHA-256, outside disposable game fixtures.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import tempfile
import time
import xml.etree.ElementTree as ET
import zipfile

from datetime import datetime, timezone
from mod_snapshot import digest, files, no_link, read, require, select, verify_snapshot, write


def ordinary_path(path):
    """Reject redirected ancestors before reading or creating private content."""
    path = Path(path).absolute()
    for part in [*reversed(path.parents), path]:
        if part.exists() or part.is_symlink():
            no_link(part)
    return path


def safe_relative(value):
    path = PurePosixPath(value)
    require(bool(value) and path.as_posix() == value and not path.is_absolute()
            and '..' not in path.parts and '\\' not in value and ':' not in value,
            'Unsafe or noncanonical snapshot path')
    return path


def validate(manifest):
    require(manifest.get('schema') == 'rimworld-mod-snapshot.v1', 'Wrong snapshot schema')
    body = {k: v for k, v in manifest.items() if k != 'snapshotId'}
    require(manifest['snapshotId'] == 'rw-' + hashlib.sha256(json.dumps(body, sort_keys=True).encode()).hexdigest()[:16],
            'Snapshot manifest identity changed')
    seen = set()
    for row in manifest['files']:
        p = safe_relative(row['path'])
        require(not p.is_absolute() and '..' not in p.parts and '\\' not in row['path'] and ':' not in row['path']
                and row['path'] not in seen, 'Unsafe or duplicate snapshot path')
        seen.add(row['path'])
        require(len(row['sha256']) == 64 and all(c in '0123456789abcdef' for c in row['sha256'])
                and isinstance(row['bytes'], int) and row['bytes'] >= 0, 'Invalid content identity')
    order = manifest['activeOrder']
    packages = manifest['packages']
    require(len(order) == len(set(order)) and [p['packageId'] for p in packages] == order,
            'Package inventory differs from active order')
    folders = []
    for package in packages:
        folder = safe_relative(package['folderName'])
        require(len(folder.parts) == 1, 'Unsafe package folder')
        if package['kind'] != 'official':
            require(package['snapshotPath'] == 'packages/' + package['packageId']
                    and len(safe_relative(package['snapshotPath']).parts) == 2, 'Unsafe package snapshot path')
            folders.append(package['folderName'].casefold())
    require(len(folders) == len(set(folders)), 'Selected package folders collide')
    for row in manifest['files']:
        path = row['path']
        require(path == 'source/ModsConfig.xml' or path.startswith('profile/')
                or any(p['kind'] != 'official' and path.startswith(p['snapshotPath'] + '/') for p in packages),
                'Unrecognized snapshot file')
        require(path != 'profile/Config/ModsConfig.xml', 'Generated config cannot be a selected setting')
    return manifest


def object_path(store, key):
    return ordinary_path(Path(store) / 'objects' / key[:2] / (key + '.gz'))


def manifest_path(store, identity):
    require(identity.startswith('rw-') and len(identity) == 19 and all(c in '0123456789abcdef' for c in identity[3:]),
            'Invalid snapshot ID')
    return ordinary_path(Path(store) / 'snapshots' / identity / 'manifest.json')


def unique_rows(manifest):
    result = {}
    for row in manifest['files']:
        require(row['sha256'] not in result or result[row['sha256']]['bytes'] == row['bytes'], 'Conflicting object sizes')
        result[row['sha256']] = row
    return result


def verify_object(path, row, sink=None):
    require(path.is_file(), f'Missing required payload: {row["sha256"]}')
    no_link(path)
    h, count = hashlib.sha256(), 0
    with gzip.open(path, 'rb') as source:
        while data := source.read(1024 * 1024):
            count += len(data)
            require(count <= row['bytes'], 'Object exceeds manifest size')
            h.update(data)
            if sink:
                sink.write(data)
    require(count == row['bytes'] and h.hexdigest() == row['sha256'], f'Corrupt payload: {row["sha256"]}')


def verify(store, identity):
    path = manifest_path(store, identity)
    require(path.is_file(), f'Missing required snapshot: {identity}')
    manifest = validate(read(path))
    require(manifest['snapshotId'] == identity, 'Wrong snapshot directory identity')
    for key, row in unique_rows(manifest).items():
        verify_object(object_path(store, key), row)
    return manifest


def subset(store, identity, request):
    parent = verify(store, identity)
    require(request.get('sourceSnapshotId') == identity, 'Child request must name the exact parent snapshotId')
    inv = dict(activeOrder=parent['activeOrder'], packages={p['packageId']: p for p in parent['packages']}, ambiguous={})
    order, added = select(inv, request)
    packages = [p for p in parent['packages'] if p['packageId'] in order]
    names = {p['folderName'].lower() for p in packages} | set(order)
    rows = []
    for row in parent['files']:
        path = row['path']
        keep = path.startswith('source/') or any(path.startswith('packages/' + p + '/') for p in order)
        if path.startswith('profile/Config/Mod_'):
            keep = any(Path(path).name.lower().startswith('mod_' + n + '_') for n in names)
        if path == 'profile/HugsLib/ModSettings.xml':
            keep = 'unlimitedhugs.hugslib' in order
        if path in {'profile/' + p for p in request.get('extraSettings', [])}:
            keep = True
        if keep:
            rows.append(row)
    manifest = dict(parent, createdUtc=datetime.now(timezone.utc).isoformat(), request=request,
                    parentSnapshotId=identity, activeOrder=order, dependenciesAdded=added,
                    omitted=[p for p in parent['sourceActiveOrder'] if p not in order], packages=packages, files=rows)
    manifest.pop('snapshotId')
    manifest['snapshotId'] = 'rw-' + hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()[:16]
    target = manifest_path(store, manifest['snapshotId'])
    require(not target.exists(), 'Use a new immutable child snapshot')
    target.parent.mkdir(parents=True)
    write(target, manifest)
    target.chmod(0o444)
    return dict(snapshotId=manifest['snapshotId'], parentSnapshotId=identity, activeOrder=order, dependenciesAdded=added)


class PartWriter:
    def __init__(self, out, prefix, limit):
        self.out, self.prefix, self.limit = out, prefix, limit
        self.buffer, self.parts = bytearray(), []
        self.hash, self.size = hashlib.sha256(), 0

    def write(self, data):
        self.hash.update(data)
        self.size += len(data)
        self.buffer.extend(data)
        while len(self.buffer) >= self.limit:
            self.flush_part(self.limit)
        return len(data)

    def flush_part(self, size):
        data = bytes(self.buffer[:size])
        del self.buffer[:size]
        number = len(self.parts) + 1
        name, member = f'{self.prefix}-part{number:04}.zip', f'archive.part{number:04}'
        with zipfile.ZipFile(self.out / name, 'x', zipfile.ZIP_STORED) as z:
            z.writestr(member, data)
        self.parts.append(dict(fileName=name, member=member, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                               zipBytes=(self.out / name).stat().st_size, zipSha256=digest(self.out / name)))

    def finish(self):
        if self.buffer:
            self.flush_part(len(self.buffer))


class PartReader:
    def __init__(self, index, parts):
        self.index, self.parts, self.position, self.buffer = index, Path(parts), 0, b''
        self.hash, self.size = hashlib.sha256(), 0

    def read(self, size):
        while len(self.buffer) < size and self.position < len(self.index['parts']):
            row = self.index['parts'][self.position]
            name = row['fileName']
            require(Path(name).name == name and '\\' not in name and ':' not in name, 'Unsafe part name')
            path = self.parts / name
            require(path.is_file(), f'Missing transfer part: {name}')
            require(path.stat().st_size == row['zipBytes'] and digest(path) == row['zipSha256'], 'Wrong ZIP part bytes/hash')
            with zipfile.ZipFile(path) as z:
                require(z.namelist() == [row['member']] and z.getinfo(row['member']).file_size == row['bytes']
                        and row['bytes'] <= 31 * 1024 * 1024, 'Wrong part contents/size')
                data = z.read(row['member'])
            require(hashlib.sha256(data).hexdigest() == row['sha256'], 'Wrong raw part hash')
            self.hash.update(data)
            self.size += len(data)
            self.buffer += data
            self.position += 1
        result, self.buffer = self.buffer[:size], self.buffer[size:]
        return result

    def finish(self):
        while self.read(1024 * 1024):
            pass
        require(self.size == self.index['archiveBytes'] and self.hash.hexdigest() == self.index['archiveSha256'],
                'Wrong complete transfer archive')


def pack(snapshot, out, base=None, part_bytes=31 * 1024 * 1024):
    started = time.monotonic()
    root, out = Path(snapshot).resolve(), Path(out).resolve()
    manifest = validate(verify_snapshot(root))
    parent = validate(read(base)) if base else None
    reusable = set(unique_rows(parent)) if parent else set()
    missing = {k: row for k, row in unique_rows(manifest).items() if k not in reusable}
    require(not out.exists() and not out.is_relative_to(root) and not root.is_relative_to(out), 'Use a fresh separate transfer directory')
    require(0 < part_bytes <= 31 * 1024 * 1024, 'Parts must fit the 32 MiB private attachment limit')
    out.mkdir(parents=True)
    index = dict(schema='rimworld-snapshot-store-transfer.v1', snapshotId=manifest['snapshotId'],
                 requiredParentSnapshotId=parent['snapshotId'] if parent else None, manifest=manifest,
                 payloadHashes=sorted(missing), reusedObjects=len(unique_rows(manifest)) - len(missing))
    writer = PartWriter(out, manifest['snapshotId'], part_bytes)
    with tarfile.open(fileobj=writer, mode='w|') as tar:
        for key, row in sorted(missing.items()):
            # One compressed object at a time; no second whole-collection archive.
            with tempfile.TemporaryFile() as compressed:
                with gzip.GzipFile(fileobj=compressed, mode='wb', filename='', mtime=0, compresslevel=3) as sink:
                    with (root / row['path']).open('rb') as source:
                        shutil.copyfileobj(source, sink)
                length = compressed.tell()
                compressed.seek(0)
                info = tarfile.TarInfo('objects/' + key + '.gz')
                info.size, info.mode = length, 0o644
                tar.addfile(info, compressed)
    writer.finish()
    index.update(parts=writer.parts, archiveBytes=writer.size, archiveSha256=writer.hash.hexdigest(),
                 payloadUncompressedBytes=sum(r['bytes'] for r in missing.values()),
                 resultingFileBytes=sum(r['bytes'] for r in manifest['files']))
    write(out / 'transfer-index.json', index)
    with zipfile.ZipFile(out / (manifest['snapshotId'] + '-index.zip'), 'x', zipfile.ZIP_DEFLATED) as z:
        z.write(out / 'transfer-index.json', 'transfer-index.json')
    receipt = dict(snapshotId=manifest['snapshotId'], requiredParentSnapshotId=index['requiredParentSnapshotId'],
                   objectsTransferred=len(missing), objectsReused=index['reusedObjects'], parts=len(writer.parts),
                   transferBytes=sum(r['zipBytes'] for r in writer.parts) + (out / (manifest['snapshotId'] + '-index.zip')).stat().st_size,
                   indexZipSha256=digest(out / (manifest['snapshotId'] + '-index.zip')), elapsedSeconds=time.monotonic()-started)
    write(out / 'pack-receipt.json', receipt)
    return receipt


def receive(index_path, parts, store):
    started = time.monotonic()
    store = ordinary_path(store)
    require('.rlo-test-instance' not in store.parts, 'Retained objects must stay outside disposable fixtures')
    index = read(index_path)
    require(index.get('schema') == 'rimworld-snapshot-store-transfer.v1', 'Wrong store transfer schema')
    manifest = validate(index['manifest'])
    require(index['snapshotId'] == manifest['snapshotId'], 'Wrong target snapshot')
    parent = verify(store, index['requiredParentSnapshotId']) if index['requiredParentSnapshotId'] else None
    rows = unique_rows(manifest)
    expected = set(index['payloadHashes'])
    require(len(expected) == len(index['payloadHashes']) and expected == set(rows) - (set(unique_rows(parent)) if parent else set()),
            'Payload identities do not match the required parent')
    for key in set(rows) - expected:
        verify_object(object_path(store, key), rows[key])
    ancestor = store
    while not ancestor.exists():
        ancestor = ancestor.parent
    require(shutil.disk_usage(ancestor).free >= index['archiveBytes'] + 4 * 1024**3,
            'Insufficient receive space and 4 GiB reserve')
    reader, seen = PartReader(index, parts), set()
    with tarfile.open(fileobj=reader, mode='r|') as tar:
        for member in tar:
            key = member.name.removeprefix('objects/').removesuffix('.gz')
            require(member.isfile() and member.name == f'objects/{key}.gz' and key in expected and key not in seen,
                    'Unexpected or duplicate payload member')
            seen.add(key)
            target = object_path(store, key)
            target.parent.mkdir(parents=True, exist_ok=True)
            # Never overwrite an already-retained object, even on a retry.
            with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as sink:
                temporary = Path(sink.name)
                with tar.extractfile(member) as source:
                    shutil.copyfileobj(source, sink)
            try:
                verify_object(temporary, rows[key])
                if target.exists():
                    verify_object(target, rows[key])
                else:
                    temporary.rename(target)
                    target.chmod(0o444)
            finally:
                temporary.unlink(missing_ok=True)
    reader.finish()
    require(seen == expected, 'Missing required payload objects')
    for key, row in rows.items():
        verify_object(object_path(store, key), row)
    destination = manifest_path(store, manifest['snapshotId'])
    if destination.exists():
        require(read(destination) == manifest, 'Refusing to replace an immutable snapshot')
    else:
        destination.parent.mkdir(parents=True)
        write(destination, manifest)
        destination.chmod(0o444)
    receipt = dict(snapshotId=manifest['snapshotId'], requiredParentSnapshotId=index['requiredParentSnapshotId'],
                   filesVerified=len(manifest['files']), objectsTransferred=len(seen), objectsReused=len(rows)-len(seen),
                   retainedObjectBytes=sum(object_path(store, key).stat().st_size for key in rows),
                   transferBytes=sum(r['zipBytes'] for r in index['parts']), elapsedSeconds=time.monotonic()-started)
    return receipt


def deploy_path(row, manifest, game, profile):
    if row['path'].startswith('profile/'):
        return profile / row['path'][8:]
    for package in manifest['packages']:
        if package['kind'] != 'official' and row['path'].startswith(package['snapshotPath'] + '/'):
            folder = package['folderName']
            require(Path(folder).name == folder and folder not in ('.', '..') and '\\' not in folder and ':' not in folder,
                    'Unsafe package folder')
            return game / 'Mods' / folder / row['path'][len(package['snapshotPath'])+1:]
    require(row['path'] == 'source/ModsConfig.xml', 'Unrecognized snapshot file')
    return None


def verify_stage(store, identity, game, profile):
    manifest = validate(read(manifest_path(store, identity)))
    require(manifest['snapshotId'] == identity, 'Wrong snapshot directory identity')
    game, profile = ordinary_path(game).resolve(), ordinary_path(profile).resolve()
    require(digest(game / 'Assembly-CSharp.dll') == manifest['gameAssemblySha256'], 'Wrong fixture game assembly')
    expected = set()
    expected_settings = {'Config/ModsConfig.xml', 'snapshot-stage.json'}
    for package in manifest['packages']:
        if package['kind'] == 'official':
            require(digest(ordinary_path(game / 'Data' / package['folderName'] / 'About/About.xml')) == package['aboutSha256'],
                    'Official package differs from captured source')
    for row in manifest['files']:
        target = deploy_path(row, manifest, game, profile)
        if target:
            ordinary_path(target)
            require(target.stat().st_size == row['bytes'] and digest(target) == row['sha256'], f'Changed deployed file: {target}')
            if target.is_relative_to(game / 'Mods'):
                expected.add(target.relative_to(game / 'Mods').as_posix())
            else:
                expected_settings.add(target.relative_to(profile).as_posix())
    require({p.relative_to(game / 'Mods').as_posix() for p in files(game / 'Mods')} == expected, 'Wrong deployed mod file set')
    require({p.relative_to(profile).as_posix() for p in files(profile)} <= expected_settings, 'Unlisted deployed settings')
    order = [x.text for x in ET.parse(profile / 'Config/ModsConfig.xml').findall('./activeMods/li')]
    require(order == manifest['activeOrder'], 'Wrong deployed active order')
    return dict(snapshotId=identity, activePackages=len(order), verified=True)


def stage(store, identity, game, profile):
    manifest = verify(store, identity)
    game, profile = ordinary_path(game).resolve(), ordinary_path(profile).resolve()
    require('.rlo-test-instance' in game.parts and '.rlo-test-instance' in profile.parts, 'Use isolated fixture paths')
    require(digest(game / 'Assembly-CSharp.dll') == manifest['gameAssemblySha256'], 'Snapshot and fixture target different game assemblies')
    require(not (game / 'Mods').exists() and not profile.exists(), 'Use a fresh game copy without Mods and a fresh profile')
    folders = [p['folderName'].casefold() for p in manifest['packages'] if p['kind'] != 'official']
    require(len(folders) == len(set(folders)), 'Selected package folders collide')
    for package in manifest['packages']:
        if package['kind'] == 'official':
            require(digest(ordinary_path(game / 'Data' / package['folderName'] / 'About/About.xml')) == package['aboutSha256'],
                    'Official package differs from captured source')
    required = sum(((row['bytes'] + 4095) // 4096) * 4096 + 4096
                   for row in manifest['files'] if deploy_path(row, manifest, game, profile))
    require(shutil.disk_usage(game).free >= required + 4 * 1024**3,
            'Insufficient space for independent staging and 4 GiB reserve')
    (game / 'Mods').mkdir()
    profile.mkdir(parents=True)
    for row in manifest['files']:
        target = deploy_path(row, manifest, game, profile)
        if target:
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as sink:
                verify_object(object_path(store, row['sha256']), row, sink)
    config = ET.Element('ModsConfigData')
    ET.SubElement(config, 'version').text = manifest['gameVersion']
    active = ET.SubElement(config, 'activeMods')
    for pid in manifest['activeOrder']:
        ET.SubElement(active, 'li').text = pid
    (profile / 'Config').mkdir(exist_ok=True)
    ET.ElementTree(config).write(profile / 'Config/ModsConfig.xml', encoding='utf-8', xml_declaration=True)
    receipt = verify_stage(store, identity, game, profile)
    receipt.update(schema='rimworld-store-stage.v1', game=str(game), profile=str(profile), gameLaunched=False)
    write(profile / 'snapshot-stage.json', receipt)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('pack')
    p.add_argument('snapshot')
    p.add_argument('--out', required=True)
    p.add_argument('--base-manifest')
    for name in ('receive', 'verify', 'subset', 'stage', 'verify-stage'):
        p = commands.add_parser(name)
        p.add_argument('--store', required=True)
        if name == 'receive':
            p.add_argument('--index', required=True)
            p.add_argument('--parts', required=True)
        else:
            p.add_argument('--snapshot', required=True)
        if name == 'subset':
            p.add_argument('--request', required=True)
        if name in ('stage', 'verify-stage'):
            p.add_argument('--game', required=True)
            p.add_argument('--profile', required=True)
    args = parser.parse_args()
    if args.command == 'pack':
        result = pack(args.snapshot, args.out, args.base_manifest)
    elif args.command == 'receive':
        result = receive(args.index, args.parts, args.store)
    elif args.command == 'verify':
        manifest = verify(args.store, args.snapshot)
        result = dict(snapshotId=manifest['snapshotId'], filesVerified=len(manifest['files']), verified=True)
    elif args.command == 'subset':
        result = subset(args.store, args.snapshot, read(args.request))
    else:
        result = (stage if args.command == 'stage' else verify_stage)(args.store, args.snapshot, args.game, args.profile)
    print(json.dumps(result))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, FileNotFoundError) as error:
        raise SystemExit(str(error))
