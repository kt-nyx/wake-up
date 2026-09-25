# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
"""Explicit minimal supplier settings for isolated functional fixtures only."""
import xml.etree.ElementTree as ET

CONTRACTS = {
    'arandomkiwi.rimthemes': ('RimThemes', 'aRandomKiwi.RimThemes.Settings', {'disableCustomLoader', 'lastVersionInfo'}),
    'solaris.fastloader': ('FastLoaderMod', 'FastLoader.FastLoaderSettings', {'cacheEnabled', 'xmlCacheEnabled', 'textureCacheEnabled', 'atlasCacheEnabled'}),
    'ilyvion.loadingprogress': ('LoadingProgressMod', 'ilyvion.LoadingProgress.Settings', {'patchInGameDeferredRepaint'}),
    'fluxxfield.defloadcache': ('DefLoadCacheMod', 'FluxxField.DefLoadCache.DefLoadCacheSettings', {'cacheEnabled', 'skipModFileLoading', 'skipPatchApplication'}),
}

def stage(profile, path, packages, active, purpose, api):
    if path is None:
        return []
    api.require(purpose == 'functional', 'Supplier setting overrides require functional purpose')
    data = api.read(path)
    api.require(isinstance(data, dict) and bool(data), 'Supplier settings must name explicit suppliers')
    receipts = []
    for package, values in data.items():
        api.require(package in CONTRACTS and package in active, 'Unknown or inactive settings supplier: ' + package)
        handle, cls, allowed = CONTRACTS[package]
        api.require(isinstance(values, dict) and bool(values) and set(values).issubset(allowed)
                    and all(type(v) is bool if k != 'lastVersionInfo' else v == 'RimThemes NX rev10'
                            for k, v in values.items()), 'Unsupported supplier settings')
        matches = [p for p in packages if p['id'] == package]
        api.require(len(matches) == 1, 'Ambiguous supplier package')
        leaf = matches[0]['leaf']
        api.require(leaf and not any(c in leaf for c in '/\\:'), 'Invalid supplier folder leaf')
        name = 'Mod_' + leaf + '_' + handle + '.xml'
        target = profile / 'Config' / name
        api.require(not target.exists(), 'Supplier settings already exist in seed; use a clean fixture source plan')
        root = ET.Element('SettingsBlock')
        settings = ET.SubElement(root, 'ModSettings', {'Class': cls})
        for key, value in values.items():
            ET.SubElement(settings, key).text = str(value)
        ET.ElementTree(root).write(target, encoding='utf-8', xml_declaration=True)
        receipts.append({'packageId': package, 'path': 'Config/' + name, 'class': cls,
                         'values': values, 'sha256': api.digest(target), 'sourceSha256': api.digest(path)})
    return receipts
