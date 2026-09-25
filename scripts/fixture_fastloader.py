# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Functional native FastLoader texture-cache carry, bound to one fixture run."""
import shutil
import xml.etree.ElementTree as ET

BINDINGS = ('generation', 'manifestSha256', 'candidate', 'menuObserverPackage', 'activeOrder',
            'profileOverrides', 'supplierSettings', 'mode', 'lane', 'png', 'pngSource', 'loadingProgress', 'activation')


def validate(mode, source, purpose, compatibility, active, api):
    if not mode:
        api.require(not source, 'FastLoader cache carry requires its warm probe')
        return
    api.require(mode in ('build', 'warm') and purpose == 'functional' and compatibility
                and 'solaris.fastloader' in active and 'local.fixture.compatibilityimages' in active,
                'FastLoader probe requires functional compatibility observation and its explicit image workload')
    api.require(bool(source) == (mode == 'warm'), 'FastLoader warm probe requires captured native build; build starts clean')


def carry(root, profile, label, bindings, api):
    folder = api.inside(root, root / 'results' / api.token(label))
    run = api.read(folder / 'run.json')
    api.require(run.get('exitCode') == 0 and run.get('automaticTestPassed') is True
                and run.get('compatibilityTestPassed') is True and run.get('fastLoaderProbe') == 'build'
                and run.get('purpose') == 'functional', 'FastLoader source must be a successful captured native build')
    for key, value in bindings.items():
        api.require(run.get(key) == value, 'FastLoader cache source binding differs: ' + key)
    evidence = {row['path']: row for row in run.get('evidence', []) if row['kind'] == 'file'}
    proof = 'FixtureMenuObserver/fastloader-native.json'
    api.require(proof in evidence and api.digest(folder / 'profile' / proof) == evidence[proof]['sha256'], 'FastLoader build evidence changed')
    result = api.read(folder / 'profile' / proof)
    api.require(result.get('passed') is True and result.get('mode') == 'build', 'Native FastLoader build did not pass')
    names = [name for name in evidence if name == 'Config/FastLoader/cache_state.xml'
             or (name.startswith('Config/FastLoader/TextureCache/') and name.count('/') == 3
                 and (name.endswith('.texcache') or name.endswith('/groups.xml')))]
    api.require('Config/FastLoader/cache_state.xml' in names
                and 'Config/FastLoader/TextureCache/mod_local.fixture.compatibilityimages.texcache' in names,
                'Captured FastLoader texture output missing')
    rows = [evidence[name] for name in sorted(names)]
    for row in rows:
        source = api.inside(root, folder / 'profile' / row['path'])
        api.require(source.is_file() and source.stat().st_size == row['bytes'] and api.digest(source) == row['sha256'], 'Captured RAW cache changed')
        if profile is not None:
            target = profile / row['path']
            api.require(not target.exists(), 'Clean fixture profile already contains FastLoader cache')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            api.require(api.digest(target) == row['sha256'], 'Copied RAW cache changed')
    state = ET.parse(folder / 'profile/Config/FastLoader/cache_state.xml').getroot().find("stages/stage[@kind='texture']")
    api.require(state is not None and state.get('success') == 'true'
                and int(state.get('itemCount', '-1')) == result.get('savedTextures'), 'Native texture stage record mismatch')
    return {'label': label, 'runSha256': api.digest(folder / 'run.json'), 'files': rows}
