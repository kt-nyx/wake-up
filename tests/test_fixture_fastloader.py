# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
import hashlib
import json
from pathlib import Path
import tempfile
import types
import unittest
from scripts import fixture_fastloader as fast


class FastLoaderCarryTests(unittest.TestCase):
    def test_native_carry_checks_source_and_excludes_other_cache_kinds(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); source = root/'results/cold/profile'; target = root/'staged'
            def require(value, reason):
                if not value: raise ValueError(reason)
            def inside(base, path):
                require(path.resolve().is_relative_to(base.resolve()), 'Outside fixture')
                return path
            api = types.SimpleNamespace(require=require, inside=inside, token=lambda s:s,
                read=lambda p:json.loads(p.read_text()), digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest())
            files = {
                'FixtureMenuObserver/fastloader-native.json': json.dumps(dict(passed=True, mode='build', savedTextures=2)),
                'Config/FastLoader/cache_state.xml': '<FastLoaderCacheState><stages><stage kind="texture" success="true" itemCount="2"/></stages></FastLoaderCacheState>',
                'Config/FastLoader/TextureCache/mod_local.fixture.compatibilityimages.texcache':'native-raw-cache',
                'Config/FastLoader/AtlasCache/unrelated.bin':'must-not-copy',
            }
            rows=[]
            for name, value in files.items():
                p=source/name; p.parent.mkdir(parents=True, exist_ok=True); p.write_text(value)
                rows.append(dict(path=name, kind='file', bytes=p.stat().st_size, sha256=api.digest(p)))
            run=dict(exitCode=0, automaticTestPassed=True, compatibilityTestPassed=True,
                fastLoaderProbe='build', purpose='functional', evidence=rows, generation='one')
            (source.parent/'run.json').write_text(json.dumps(run))
            with self.assertRaises(ValueError): fast.carry(root,target,'cold',dict(generation='different'),api)
            result=fast.carry(root,target,'cold',dict(generation='one'),api)
            self.assertEqual(len(result['files']),2)
            self.assertFalse((target/'Config/FastLoader/AtlasCache').exists())
            self.assertEqual(fast.carry(root,None,'cold',dict(generation='one'),api),result)
            (source/'Config/FastLoader/TextureCache/mod_local.fixture.compatibilityimages.texcache').write_text('changed')
            with self.assertRaises(ValueError): fast.carry(root,None,'cold',dict(generation='one'),api)

    def test_build_and_warm_are_functional_and_supplier_bound(self):
        def require(value, reason):
            if not value: raise ValueError(reason)
        api=types.SimpleNamespace(require=require)
        active=['solaris.fastloader','local.fixture.compatibilityimages']
        fast.validate('build',None,'functional','observe',active,api)
        fast.validate('warm','cold','functional','observe',active,api)
        for mode,source,purpose,mods in [('build',None,'performance',active),('warm',None,'functional',active),
                ('build','cold','functional',active),('warm','cold','functional',[])]:
            with self.assertRaises(ValueError): fast.validate(mode,source,purpose,'observe',mods,api)
