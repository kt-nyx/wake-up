#!/usr/bin/env python3
"""Freeze selected installed RimWorld packages; never modify the played setup."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import xml.etree.ElementTree as ET


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def package_id(value):
    return value.strip().lower().removesuffix('_steam')


def no_link(path):
    p = Path(path)
    require(not p.is_symlink() and not (getattr(p.lstat(), 'st_file_attributes', 0) & 1024),
            f'Linked or redirected content requires an explicit ordinary copy: {p}')


def files(root):
    root = Path(root)
    no_link(root)
    result = []
    for directory, dirs, names in os.walk(root, followlinks=False):
        for name in dirs + names:
            no_link(Path(directory) / name)
        result.extend(Path(directory) / name for name in names)
    return sorted(result)


def administrative_rule(relative):
    """Package-only exclusions; ordinary file discovery/verification stays exact."""
    for part in PurePosixPath(relative).parts:
        if part.casefold() in {'.git', '.hg', '.svn', '.gitconfig', '.gitmodules'}:
            return part.casefold()
    return None


def reject_credential_file(relative):
    parts = [part.casefold() for part in PurePosixPath(relative).parts]
    name = parts[-1]
    private_key = (re.fullmatch(r'id_(rsa|dsa|ecdsa|ed25519)(?:[._-].*)?', name)
                   and not name.endswith('.pub')) or re.fullmatch(r'private[._-]?key(?:[._-].*)?', name)
    require(not (name == '.env' or name.startswith('.env.')
                 or name in {'account.config', 'loginusers.vdf', 'config.vdf'}
                 or '.ssh' in parts or private_key or name.endswith('.ppk')),
            f'Credential-file candidate outside excluded administrative metadata: {relative}; provide sanitized inputs')


def package_files(root):
    retained, excluded = [], []
    for path in files(root):
        relative = path.relative_to(root).as_posix()
        rule = administrative_rule(relative)
        if rule:
            excluded.append(dict(path=relative, bytes=path.stat().st_size, rule=rule))
        else:
            reject_credential_file(relative)
            retained.append(path)
    return retained, excluded


def exclusion_record(package_id, rows):
    return dict(packageId=package_id, rules=sorted({r['rule'] for r in rows}),
                fileCount=len(rows), bytes=sum(r['bytes'] for r in rows),
                paths=sorted(rows, key=lambda r: r['path']))


def required_packages(metadata, version):
    specific = metadata.find(f'./modDependenciesByVersion/v{version}')
    nodes = specific.findall('./li/packageId') if specific is not None else metadata.findall('./modDependencies/li/packageId')
    return sorted({package_id(x.text or '') for x in nodes})


def inventory(source):
    require(source.get('schema') == 'rimworld-local-source.v1', 'Wrong local-source schema')
    profile = Path(source['profileDir'])
    config = profile / 'Config/ModsConfig.xml'
    order = [package_id(x.text or '') for x in ET.parse(config).findall('./activeMods/li')]
    require(len(order) == len(set(order)), 'Duplicate active package IDs')
    game_version = (Path(source['gameDir']) / 'Version.txt').read_text(encoding='utf-8-sig').strip()
    version = re.match(r'(\d+\.\d+)', game_version).group(1)
    found = {}
    for base, kind in [(Path(source['gameDir']) / 'Data', 'official'),
                       (Path(source['gameDir']) / 'Mods', 'local'),
                       (Path(source['workshopDir']), 'workshop')]:
        if not base.exists():
            continue
        for folder in sorted(base.iterdir()):
            about = folder / 'About/About.xml'
            if not about.is_file():
                continue
            root = ET.parse(about).getroot()
            pid = package_id(root.findtext('packageId', ''))
            if not pid:
                continue
            require(bool(re.fullmatch(r'[a-z0-9_-]+(?:\.[a-z0-9_-]+)*', pid)), f'Unsafe package ID in {about}')
            found.setdefault(pid, []).append(dict(packageId=pid, name=root.findtext('name'),
                sourcePath=str(folder.resolve()), folderName=folder.name, kind=kind,
                supportedVersions=[x.text for x in root.findall('./supportedVersions/li')],
                requiredPackages=required_packages(root, version),
                aboutSha256=digest(about)))
    chosen = {}
    ambiguous = {}
    for pid, options in found.items():
        override = source.get('packageOverrides', {}).get(pid)
        if override:
            options = [x for x in options if Path(x['sourcePath']) == Path(override).resolve()]
            require(len(options) == 1, f'Package override does not identify exactly one {pid}')
        if len(options) == 1:
            chosen[pid] = options[0]
        else:
            ambiguous[pid] = options
    return dict(schema='rimworld-local-inventory.v1', gameVersion=game_version,
                gameAssemblySha256=digest(Path(source['gameDir']) / 'Assembly-CSharp.dll'),
                activeOrder=order, modsConfigSha256=digest(config), packages=chosen,
                missing=[x for x in order if x not in found], ambiguous=ambiguous)


def select(inv, request):
    require(request.get('schema') == 'rimworld-cloud-test-request.v1', 'Wrong test-request schema')
    require(request.get('requestId') and request.get('purpose'), 'Request ID and purpose are required')
    selection = request['selection']
    require(selection.get('reason'), 'Explain why this collection is needed')
    mode = selection['mode']
    require(mode in ('full', 'subset'), 'Investigate mode needs an agent to resolve packageIds and record its reasoning first')
    order = inv['activeOrder']
    chosen = set(order if mode == 'full' else [package_id(x) for x in selection['packageIds']])
    # Keep installed active DLCs, and the game itself, in the same source order.
    chosen.update(x for x in order if inv['packages'].get(x, {}).get('kind') == 'official')
    chosen.add('ludeon.rimworld')
    requested = chosen.copy()
    pending = list(chosen)
    while pending:
        pid = pending.pop()
        require(pid in order, f'Required package is outside the active local list: {pid}')
        require(pid not in inv['ambiguous'], f'Ambiguous package needs packageOverrides: {pid}')
        require(pid in inv['packages'], f'Active package unavailable locally: {pid}')
        for dependency in inv['packages'][pid]['requiredPackages']:
            if dependency not in chosen:
                chosen.add(dependency)
                pending.append(dependency)
    return [x for x in order if x in chosen], sorted(chosen - requested)


def no_credentials(path):
    if Path(path).suffix.lower() != '.xml':
        raise ValueError(f'Only explicitly selected XML settings may be exported: {path}')
    root = ET.parse(path).getroot()
    sensitive = {'apikey', 'accesstoken', 'refreshtoken', 'password', 'authorization', 'clientsecret'}
    for item in root.iter():
        key = re.sub('[^a-z]', '', item.tag.lower())
        require(not (key in sensitive and (item.text or '').strip()),
                f'Populated credential field in settings; provide a sanitized copy: {Path(path).name}')


def export_snapshot(source, request, output, reuse_files=None):
    inv = inventory(source)
    order, added = select(inv, request)
    output = Path(output).resolve()
    roots = [Path(source[k]).resolve() for k in ('gameDir', 'workshopDir', 'profileDir')]
    require(all(output != p and not output.is_relative_to(p) and not p.is_relative_to(output) for p in roots),
            'Snapshot output must be separate from source game, Workshop and profile')
    reuse = None
    if reuse_files is not None:
        reuse = Path(reuse_files).absolute()
        for path in [*reversed(reuse.parents), reuse]:
            no_link(path)
        reuse = reuse.resolve()
        require(reuse.is_dir(), 'Reuse files must name an existing copied snapshot tree')
        require(all(reuse != p and not reuse.is_relative_to(p) and not p.is_relative_to(reuse)
                    for p in roots + [output]),
                'Reuse files must be separate from source game, Workshop, profile and output')
    require(not output.exists(), 'Use a fresh snapshot output directory')
    output.mkdir(parents=True)
    rows = []
    stamps = {}
    source_hashes = {}
    trees = {}
    exclusions = []

    def copy_one(src, relative):
        no_link(src)
        before = src.stat()
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        reused = False
        if reuse is not None:
            prior = reuse / relative
            if prior.exists() or prior.is_symlink():
                path = prior
                while path != reuse:
                    no_link(path)
                    path = path.parent
                # A prior ordinary copy has one link. Already-shared files fall
                # back to copying, so no played-file hard link can be propagated.
                if (prior.is_file() and prior.stat().st_nlink == 1 and not prior.samefile(src)
                        and prior.stat().st_size == before.st_size):
                    if digest(prior) == digest(src):
                        try:
                            os.link(prior, target)
                            reused = True
                        except OSError:
                            pass  # Cross-device/unsupported hard links: ordinary copy.
        if not reused:
            shutil.copy2(src, target)
        h = digest(target)
        require(h == digest(src), f'Source changed during capture: {src}')
        stamps[src] = (before.st_size, before.st_mtime_ns)
        source_hashes[src] = h
        require(stamps[src] == (src.stat().st_size, src.stat().st_mtime_ns), f'Source changed: {src}')
        rows.append(dict(path=relative, bytes=target.stat().st_size, sha256=h))

    packages = []
    for pid in order:
        package = dict(inv['packages'][pid])
        if package['kind'] != 'official':
            origin = Path(package['sourcePath'])
            package['snapshotPath'] = f'packages/{pid}'
            selected, excluded = package_files(origin)
            trees[origin] = (selected, excluded)
            if excluded:
                exclusions.append(exclusion_record(pid, excluded))
            for src in selected:
                copy_one(src, package['snapshotPath'] + '/' + src.relative_to(origin).as_posix())
        packages.append(package)
    profile = Path(source['profileDir'])
    names = {p['folderName'].lower() for p in packages} | set(order)
    settings = set()
    for candidate in (profile / 'Config').glob('Mod_*.xml'):
        if any(candidate.name.lower().startswith('mod_' + name + '_') for name in names):
            settings.add(candidate)
    if 'unlimitedhugs.hugslib' in order and (profile / 'HugsLib/ModSettings.xml').is_file():
        settings.add(profile / 'HugsLib/ModSettings.xml')
    for relative in source.get('extraSettings', []):
        pure = PurePosixPath(relative)
        require(not pure.is_absolute() and '..' not in pure.parts and '\\' not in relative and ':' not in relative,
                'Unsafe extra settings path')
        settings.add(profile / relative)
    for src in sorted(settings):
        require(src.resolve().is_relative_to(profile.resolve()), 'Settings escaped source profile')
        reject_credential_file(src.relative_to(profile).as_posix())
        no_credentials(src)
        copy_one(src, 'profile/' + src.relative_to(profile).as_posix())
    copy_one(profile / 'Config/ModsConfig.xml', 'source/ModsConfig.xml')
    require(inventory(source) == inv, 'Active list or package metadata changed during capture; retry in a fresh directory')
    require(all(package_files(root)[0] == names[0] for root, names in trees.items()), 'Package file list changed during capture')
    require(all((p.stat().st_size, p.stat().st_mtime_ns) == stamp for p, stamp in stamps.items()),
            'Source files changed during capture; retry in a fresh directory')
    require(all(digest(p) == h for p, h in source_hashes.items()),
            'Source bytes changed during capture; retry in a fresh directory')
    manifest = dict(schema='rimworld-mod-snapshot.v1', createdUtc=datetime.now(timezone.utc).isoformat(),
        gameVersion=inv['gameVersion'], gameAssemblySha256=inv['gameAssemblySha256'], request=request,
        activeOrder=order, sourceActiveOrder=inv['activeOrder'], dependenciesAdded=added,
        omitted=[x for x in inv['activeOrder'] if x not in order], packages=packages, files=rows,
        settingsPolicy='Selected package folder/name settings plus explicit extraSettings; no saves, logs, caches or Prefs.xml',
        sourceModsConfigSha256=inv['modsConfigSha256'])
    if source.get('captureContext'):
        manifest['captureContext'] = source['captureContext']
    if exclusions:
        manifest['administrativeExclusions'] = exclusions
    identity = hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()[:16]
    manifest['snapshotId'] = 'rw-' + identity
    write(output / 'manifest.json', manifest)
    return manifest


def verify_snapshot(root):
    root = Path(root)
    manifest = read(root / 'manifest.json')
    require(manifest.get('schema') == 'rimworld-mod-snapshot.v1', 'Wrong snapshot schema')
    expected = set()
    for row in manifest['files']:
        relative = PurePosixPath(row['path'])
        require(not relative.is_absolute() and '..' not in relative.parts and '\\' not in row['path'] and ':' not in row['path'], 'Unsafe snapshot path')
        require(row['path'] not in expected, 'Duplicate snapshot path')
        expected.add(row['path'])
        path = root / row['path']
        require(path.stat().st_size == row['bytes'] and digest(path) == row['sha256'], f'Changed snapshot file: {row["path"]}')
    require({p.relative_to(root).as_posix() for p in files(root)} == expected | {'manifest.json'},
            'Snapshot contains missing or unlisted files')
    identity_data = {k: v for k, v in manifest.items() if k != 'snapshotId'}
    identity = hashlib.sha256(json.dumps(identity_data, sort_keys=True).encode()).hexdigest()[:16]
    require(manifest['snapshotId'] == 'rw-' + identity, 'Snapshot manifest identity changed')
    return manifest


def subset_snapshot(root, request, output):
    root, output = Path(root), Path(output)
    parent = verify_snapshot(root)
    require(request.get('sourceSnapshotId') == parent['snapshotId'], 'Child request must name the exact parent snapshotId')
    inv = dict(activeOrder=parent['activeOrder'], packages={p['packageId']: p for p in parent['packages']}, ambiguous={})
    order, added = select(inv, request)
    require(not output.exists(), 'Use a fresh child snapshot directory')
    output.mkdir(parents=True)
    packages = [p for p in parent['packages'] if p['packageId'] in order]
    names = {p['folderName'].lower() for p in packages} | set(order)
    rows = []
    for row in parent['files']:
        path = row['path']
        keep = (path.startswith('source/') or any(path.startswith('packages/' + pid + '/') for pid in order))
        if path.startswith('profile/Config/Mod_'):
            keep = any(Path(path).name.lower().startswith('mod_' + name + '_') for name in names)
        if path == 'profile/HugsLib/ModSettings.xml':
            keep = 'unlimitedhugs.hugslib' in order
        # Explicit shared settings need a fresh explicit selection in child requests.
        if path in {'profile/' + p for p in request.get('extraSettings', [])}:
            keep = True
        if keep:
            destination = output / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(root / path, destination)
            rows.append(row)
    manifest = dict(parent, createdUtc=datetime.now(timezone.utc).isoformat(), request=request,
                    parentSnapshotId=parent['snapshotId'], activeOrder=order, dependenciesAdded=added,
                    omitted=[x for x in parent['sourceActiveOrder'] if x not in order], packages=packages, files=rows)
    manifest.pop('snapshotId')
    manifest['snapshotId'] = 'rw-' + hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()[:16]
    write(output / 'manifest.json', manifest)
    return verify_snapshot(output)


def stage_snapshot(snapshot, game, profile, overrides=None):
    """Populate an isolated game copy and fresh profile, without launching."""
    root, game, profile = Path(snapshot), Path(game).resolve(), Path(profile).resolve()
    manifest = verify_snapshot(root)
    require('.rlo-test-instance' in game.parts and '.rlo-test-instance' in profile.parts,
            'Staging is limited to explicitly isolated .rlo-test-instance paths')
    require(digest(game / 'Assembly-CSharp.dll') == manifest['gameAssemblySha256'],
            'Snapshot and fixture target different game assemblies; refresh or review the target first')
    require(not (game / 'Mods').exists() and not profile.exists(), 'Use a fresh game copy without Mods and a fresh profile')
    overrides = overrides or {}
    require(set(overrides).issubset(manifest['activeOrder']), 'Candidate override is outside the selected collection')
    folders = set()
    plan = []
    for package in manifest['packages']:
        pid = package['packageId']
        folder = package['folderName']
        require(Path(folder).name == folder and folder not in ('.', '..') and '\\' not in folder and ':' not in folder, 'Unsafe package folder')
        if package['kind'] == 'official':
            require(pid not in overrides, 'Official game/DLC overrides are not allowed')
            require(digest(game / 'Data' / folder / 'About/About.xml') == package['aboutSha256'], 'Official package differs from snapshot source')
            continue
        require(folder.casefold() not in folders, 'Selected package folders collide')
        folders.add(folder.casefold())
        source = Path(overrides[pid]) if pid in overrides else root / package['snapshotPath']
        metadata = ET.parse(source / 'About/About.xml').getroot()
        require(package_id(metadata.findtext('packageId', '')) == pid, 'Candidate override has the wrong package ID')
        version = re.match(r'(\d+\.\d+)', manifest['gameVersion']).group(1)
        require(set(required_packages(metadata, version)).issubset(manifest['activeOrder']),
                f'Candidate dependencies require a revised test selection: {pid}')
        rows = [(p.relative_to(source).as_posix(), digest(p)) for p in files(source)]
        plan.append((source, game / 'Mods' / folder, rows, pid))
    (game / 'Mods').mkdir()
    deployed = []
    for source, destination, rows, pid in plan:
        shutil.copytree(source, destination)
        require(all(digest(destination / relative) == h for relative, h in rows), f'Deployment changed package bytes: {pid}')
        deployed.append(dict(packageId=pid, folderName=destination.name, override=pid in overrides, files=rows))
    profile.mkdir(parents=True)
    if (root / 'profile').exists():
        shutil.copytree(root / 'profile', profile, dirs_exist_ok=True)
    config = ET.Element('ModsConfigData')
    ET.SubElement(config, 'version').text = manifest['gameVersion']
    active = ET.SubElement(config, 'activeMods')
    for pid in manifest['activeOrder']:
        ET.SubElement(active, 'li').text = pid
    (profile / 'Config').mkdir(exist_ok=True)
    ET.ElementTree(config).write(profile / 'Config/ModsConfig.xml', encoding='utf-8', xml_declaration=True)
    receipt = dict(schema='rimworld-snapshot-stage.v1', snapshotId=manifest['snapshotId'],
                   request=manifest['request'], game=str(game), profile=str(profile),
                   activeOrder=manifest['activeOrder'], deployed=deployed, gameLaunched=False)
    write(profile / 'snapshot-stage.json', receipt)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    inv = commands.add_parser('inventory')
    inv.add_argument('--source', required=True)
    inv.add_argument('--out', required=True)
    export = commands.add_parser('export')
    export.add_argument('--source', required=True)
    export.add_argument('--request', required=True)
    export.add_argument('--out', required=True)
    export.add_argument('--reuse-files', metavar='DIRECTORY',
                        help='Reuse hash-matching ordinary copies from a prior snapshot tree; never played files')
    verify = commands.add_parser('verify')
    verify.add_argument('snapshot')
    subset = commands.add_parser('subset')
    subset.add_argument('snapshot')
    subset.add_argument('--request', required=True)
    subset.add_argument('--out', required=True)
    stage = commands.add_parser('stage')
    stage.add_argument('snapshot')
    stage.add_argument('--game', required=True)
    stage.add_argument('--profile', required=True)
    stage.add_argument('--override', action='append', default=[], metavar='PACKAGE_ID=PACKAGE_PATH')
    args = parser.parse_args()
    if args.command == 'inventory':
        result = inventory(read(args.source))
        write(args.out, result)
        print(json.dumps(dict(active=len(result['activeOrder']), missing=result['missing'], ambiguous=list(result['ambiguous']))))
    else:
        if args.command == 'export':
            result = export_snapshot(read(args.source), read(args.request), args.out, args.reuse_files)
        elif args.command == 'subset':
            result = subset_snapshot(args.snapshot, read(args.request), args.out)
        elif args.command == 'stage':
            overrides = dict(x.split('=', 1) for x in args.override)
            result = stage_snapshot(args.snapshot, args.game, args.profile, overrides)
            print(json.dumps(dict(snapshotId=result['snapshotId'], deployed=len(result['deployed']), gameLaunched=False)))
            return
        else:
            result = verify_snapshot(args.snapshot)
        print(json.dumps(dict(snapshotId=result['snapshotId'], packages=len(result['packages']), files=len(result['files']), dependenciesAdded=result['dependenciesAdded'])))


if __name__ == '__main__':
    try:
        main()
    except ValueError as error:
        raise SystemExit(str(error))
