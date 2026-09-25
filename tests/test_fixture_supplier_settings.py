# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
import hashlib
import json
from pathlib import Path
import tempfile
import types
import unittest
import xml.etree.ElementTree as ET
from scripts import fixture_supplier_settings as settings

class SupplierSettingsTests(unittest.TestCase):
    def test_constructor_order_retains_bootstrap_and_other_supplier_order(self):
        from scripts import fixture
        manifest={'activeOrder':['zetrith.prepatcher','brrainz.harmony','ludeon.rimworld','vopaga.hyperdrive','other.mod']}
        default=fixture.run_order(manifest,'representative','candidate',True)
        reverse=fixture.run_order(manifest,'representative','candidate',True,product_after='vopaga.hyperdrive')
        self.assertEqual(reverse[:3],default[:3])
        self.assertEqual([x for x in reverse if x != fixture.PACKAGE_ID], [x for x in default if x != fixture.PACKAGE_ID])
        self.assertEqual(reverse.index(fixture.PACKAGE_ID),reverse.index('vopaga.hyperdrive')+1)
        with self.assertRaises(RuntimeError): fixture.run_order(manifest,'representative','candidate',True,product_after='missing.mod')

    def test_minimal_config_is_scoped_and_receipted(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); (root/'Config').mkdir(); source=root/'input.json'
            source.write_text(json.dumps({'solaris.fastloader':{'atlasCacheEnabled':False}}))
            def require(value, reason):
                if not value: raise ValueError(reason)
            api=types.SimpleNamespace(require=require, read=lambda p:json.loads(p.read_text()), digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest())
            packages=[{'id':'solaris.fastloader','leaf':'FastLoader'}]
            with self.assertRaises(ValueError): settings.stage(root,source,packages,['solaris.fastloader'],'performance',api)
            with self.assertRaises(ValueError): settings.stage(root,source,packages,[],'functional',api)
            result=settings.stage(root,source,packages,['solaris.fastloader'],'functional',api)
            target=root/'Config/Mod_FastLoader_FastLoaderMod.xml'
            self.assertEqual(result[0]['sha256'],api.digest(target))
            node=ET.parse(target).find('ModSettings')
            self.assertEqual(node.attrib,{'Class':'FastLoader.FastLoaderSettings'})
            self.assertEqual([(e.tag,e.text) for e in node],[('atlasCacheEnabled','False')])
            with self.assertRaises(ValueError): settings.stage(root,source,packages,['solaris.fastloader'],'functional',api)

if __name__=='__main__': unittest.main()
