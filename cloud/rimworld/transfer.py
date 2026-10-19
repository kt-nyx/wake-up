#!/usr/bin/env python3
"""Package private mod snapshots for the verified 32 MiB attachment route."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
import zipfile

from mod_snapshot import digest, files, read, require, verify_snapshot, write


def pack(snapshot, destination, part_bytes=31 * 1024 * 1024):
    root, out = Path(snapshot).resolve(), Path(destination).resolve()
    manifest = verify_snapshot(root)
    require(not out.exists() and not out.is_relative_to(root) and not root.is_relative_to(out), 'Use a fresh separate transfer directory')
    require(0 < part_bytes <= 31 * 1024 * 1024, 'Parts must fit within the 32 MiB receiver limit')
    out.mkdir(parents=True)
    archive = out / 'snapshot.tar.gz'
    with tarfile.open(archive, 'w:gz') as tar:
        for path in files(root):
            info = tar.gettarinfo(str(path), arcname=path.relative_to(root).as_posix())
            info.uid = info.gid = 0
            info.uname = info.gname = ''
            info.mode = 0o644
            with path.open('rb') as stream:
                tar.addfile(info, stream)
    parts = []
    with archive.open('rb') as stream:
        while data := stream.read(part_bytes):
            number = len(parts) + 1
            name = f'{manifest["snapshotId"]}-part{number:04}.zip'
            member = f'archive.part{number:04}'
            with zipfile.ZipFile(out / name, 'x', zipfile.ZIP_STORED) as z:
                z.writestr(member, data)
            parts.append(dict(fileName=name, member=member, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                              zipBytes=(out / name).stat().st_size, zipSha256=digest(out / name)))
    index = dict(schema='rimworld-snapshot-transfer.v1', snapshotId=manifest['snapshotId'],
                 archiveBytes=archive.stat().st_size, archiveSha256=digest(archive),
                 unpackedBytes=sum(p.stat().st_size for p in files(root)), parts=parts)
    write(out / 'transfer-index.json', index)
    with zipfile.ZipFile(out / f'{manifest["snapshotId"]}-index.zip', 'x', zipfile.ZIP_DEFLATED) as z:
        z.write(out / 'transfer-index.json', 'transfer-index.json')
    return index


def unpack(index_path, parts_directory, destination):
    index = read(index_path)
    require(index.get('schema') == 'rimworld-snapshot-transfer.v1', 'Wrong transfer index schema')
    out = Path(destination).resolve()
    require(not out.exists(), 'Use a fresh snapshot destination')
    archive = out.with_name(out.name + '.transfer.tar.gz')
    require(not archive.exists(), 'Refusing to overwrite a transfer archive')
    archive.parent.mkdir(parents=True, exist_ok=True)
    with archive.open('xb') as stream:
        for row in index['parts']:
            require(Path(row['fileName']).name == row['fileName'] and '\\' not in row['fileName'] and ':' not in row['fileName'], 'Unsafe part name')
            source = Path(parts_directory) / row['fileName']
            require(source.stat().st_size == row['zipBytes'] and digest(source) == row['zipSha256'], 'Wrong ZIP part bytes/hash')
            with zipfile.ZipFile(source) as z:
                require(z.namelist() == [row['member']], 'Unexpected ZIP entries')
                require(z.getinfo(row['member']).file_size == row['bytes'] <= 31 * 1024 * 1024, 'Oversized or wrong part')
                data = z.read(row['member'])
                require(hashlib.sha256(data).hexdigest() == row['sha256'], 'Wrong raw part hash')
                stream.write(data)
    require(archive.stat().st_size == index['archiveBytes'] and digest(archive) == index['archiveSha256'], 'Wrong reconstructed archive')
    out.mkdir()
    seen = set()
    total = 0
    with tarfile.open(archive, 'r|gz') as tar:
        for member in tar:
            path = PurePosixPath(member.name)
            require(member.isfile() and not path.is_absolute() and '..' not in path.parts and '\\' not in member.name and ':' not in member.name, 'Unsafe archive member')
            require(member.name not in seen, 'Duplicate archive member')
            seen.add(member.name)
            total += member.size
            require(total <= index['unpackedBytes'], 'Archive exceeds declared input size')
            target = out / member.name
            target.parent.mkdir(parents=True, exist_ok=True)
            with tar.extractfile(member) as source, target.open('xb') as sink:
                shutil.copyfileobj(source, sink)
            target.chmod(0o644)
    require(total == index['unpackedBytes'], 'Incomplete archive')
    manifest = verify_snapshot(out)
    require(manifest['snapshotId'] == index['snapshotId'], 'Wrong snapshot identity')
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('pack')
    p.add_argument('snapshot')
    p.add_argument('--out', required=True)
    u = commands.add_parser('unpack')
    u.add_argument('--index', required=True)
    u.add_argument('--parts', required=True)
    u.add_argument('--out', required=True)
    args = parser.parse_args()
    result = pack(args.snapshot, args.out) if args.command == 'pack' else unpack(args.index, args.parts, args.out)
    print(json.dumps(dict(snapshotId=result['snapshotId'], parts=len(result.get('parts', [])), verified=True)))


if __name__ == '__main__':
    try:
        main()
    except ValueError as error:
        raise SystemExit(str(error))
