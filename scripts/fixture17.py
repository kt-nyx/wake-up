"""Bounded 1.7 lane, invoked through scripts/fixture.py modern.

The legacy fixture installation and the played profiles remain source-only.
This lane copies installed content and uses native game APIs for observation/exit.
"""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / '.rlo-test-instance' / '1.7'
GAME = Path(r'Z:\Games\SteamLibrary\steamapps\common\RimWorld')
WORKSHOP = Path(r'Z:\Games\SteamLibrary\steamapps\workshop\content\294100')
HOST = Path(r'C:\Users\Host\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios')
NORMAL = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios'
OBSERVER_ID = 'local.fixture.wakeup17observer'
WAKE_ID = 'kt.nyx.wakeup'
EXPECTED_GAME = '3d4f1ddfe71bd8760cb488255e3bdeac9366f537dc1ef813cc6f8d09824e2da9'
EXPECTED_HARMONY = '94dd929a06f720869f6a718a64bc6713a37dbe153c2f4c16c031b2587d7ee694'


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2), encoding='utf-8')
    temporary.replace(path)


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def no_game():
    result = subprocess.run(['pwsh', '-NoProfile', '-Command',
        "@(Get-CimInstance Win32_Process -Filter \"Name LIKE 'RimWorld%'\").Count"],
        capture_output=True, text=True, check=True)
    require(result.stdout.strip() == '0', 'A RimWorld process is running; operation refused.')


def token(value):
    require(value and all(c.isalnum() or c in '-_' for c in value), 'Unsafe lane/run name')
    return value


def inventory():
    found = {}
    for base, kind in [(WORKSHOP, 'workshop'), (GAME/'Mods', 'local'), (GAME/'Data', 'official')]:
        for directory in base.iterdir():
            about = directory/'About/About.xml'
            if not about.is_file():
                continue
            try:
                tree = ET.parse(about)
                package = tree.findtext('packageId', '').strip().lower()
                if package:
                    found.setdefault(package, []).append(dict(path=str(directory), kind=kind,
                        name=tree.findtext('name'), supported=[x.text for x in tree.findall('./supportedVersions/li')]))
            except ET.ParseError:
                continue
    return found


def initialize():
    no_game()
    require(not (ROOT/'manifest.json').exists(), 'This frozen lane already exists.')
    require(sha(GAME/'Assembly-CSharp.dll') == EXPECTED_GAME, 'Unreviewed 1.7 game')
    require(sha(WORKSHOP/'2009463077/1.7/Assemblies/0Harmony.dll') == EXPECTED_HARMONY, 'Unreviewed Harmony')
    ROOT.mkdir(parents=True, exist_ok=True)
    originals = ROOT/'originals'
    originals.mkdir(exist_ok=True)
    for user, profile in [('Host', HOST), ('vex', NORMAL)]:
        for relative in ['Logs/Player.log', 'Logs/Player-prev.log', 'Config/ModsConfig.xml']:
            path = profile/relative
            if path.is_file():
                target = originals/user/relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
    shutil.copytree(HOST/'Config', ROOT/'seed/Config', dirs_exist_ok=True)
    if (HOST/'HugsLib/ModSettings.xml').is_file():
        (ROOT/'seed/HugsLib').mkdir(parents=True, exist_ok=True)
        shutil.copy2(HOST/'HugsLib/ModSettings.xml', ROOT/'seed/HugsLib/ModSettings.xml')
    source_order = [e.text.lower() for e in ET.parse(HOST/'Config/ModsConfig.xml').findall('./activeMods/li')]
    available = inventory()
    packages = []
    missing = []
    for package in source_order:
        bare = package.removesuffix('_steam')
        choices = available.get(bare, [])
        if not choices:
            missing.append(package)
            continue
        selected = next((p for p in choices if p['kind'] == 'official'), None)
        selected = selected or next((p for p in choices if p['kind'] == 'local'), choices[0])
        packages.append(dict(selected, id=bare, configuredId=package))
    write(ROOT/'selection.json', dict(packages=packages, missing=missing, sourceOrder=source_order))
    require(not missing, 'Configured packages missing: '+repr(missing))
    destination = ROOT/'game'
    destination.mkdir(exist_ok=True)
    for source in GAME.iterdir():
        if source.name == 'Mods':
            continue
        target = destination/source.name
        if source.is_dir():
            shutil.copytree(source, target, dirs_exist_ok=True)
        else:
            shutil.copy2(source, target)
    (destination/'Mods').mkdir(exist_ok=True)
    for i, package in enumerate(packages):
        source = Path(package['path'])
        target = destination/('Data' if package['kind'] == 'official' else 'Mods')/source.name
        if package['kind'] != 'official':
            shutil.copytree(source, target, dirs_exist_ok=True)
        package['fixture'] = str(target)
        package['aboutSha256'] = sha(target/'About/About.xml')
        package['assemblies'] = {str(p.relative_to(target)):sha(p) for p in target.rglob('*.dll')}
        print(f'copied {i+1}/{len(packages)} {package["id"]}', flush=True)
    manifest = dict(schema='wake-up-fixture-17.v1', createdUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),
        gameSource=str(GAME), gameSha256=sha(destination/'Assembly-CSharp.dll'),
        engineSha256=sha(destination/'Waylith.dll'), sourceProfile=str(HOST), sourceOrder=source_order,
        packages=packages, originalFixtureGameChanged=False)
    write(ROOT/'manifest.json', manifest)
    return dict(root=str(ROOT), packages=len(packages))


def prepare(args):
    no_game()
    manifest = read(ROOT/'manifest.json')
    require(sha(ROOT/'game/Assembly-CSharp.dll') == manifest['gameSha256'] == EXPECTED_GAME, 'Game identity drift')
    name = token(args.profile)
    profile = ROOT/'profiles'/name
    if not profile.exists():
        shutil.copytree(ROOT/'seed', profile)
    if args.native_cache_from:
        source_cache=ROOT/'profiles'/token(args.native_cache_from)
        require(source_cache!=profile,'Cannot seed a profile from itself')
        for leaf in ['TextureCache','ShaderCache','BundleCache','HardwareProfile']:
            source=source_cache/leaf
            destination=profile/leaf
            require(not destination.exists(),'Native cache seed requires a fresh destination: '+leaf)
            if source.exists(): shutil.copytree(source,destination)
        write(profile/'native-cache-seed.json',dict(source=str(source_cache),
            note='Native files copied unchanged; ordinary game validation remains in charge. Supplier caches not copied.'))
    order = list(manifest['sourceOrder'])
    if args.minimal:
        order = [p for p in order if p == 'brrainz.harmony' or p.startswith('ludeon.rimworld')]
    omitted = {p.lower() for p in args.omit}
    order = [p for p in order if p.removesuffix('_steam') not in omitted and p.removesuffix('_steam') not in [WAKE_ID, 'zetrith.prepatcher']]
    extra = [p.lower() for p in args.add]
    known = {p['id'] for p in manifest['packages']}
    for package in extra:
        require(package in known, 'Supplier was not frozen: '+package)
        if package not in order:
            order.append(package)
    if args.wake != 'absent':
        source_position = manifest['sourceOrder'].index(WAKE_ID)
        previous = [p for p in manifest['sourceOrder'][:source_position] if p in order]
        position = 1 if args.wake_early else order.index(previous[-1])+1 if previous else 1
        order.insert(position, WAKE_ID)
    order.insert(1, OBSERVER_ID)
    config = ET.Element('ModsConfigData')
    ET.SubElement(config,'version').text='1.7.4977 rev1248'
    mods=ET.SubElement(config,'activeMods')
    for package in order:
        ET.SubElement(mods,'li').text=package
    ET.ElementTree(config).write(profile/'Config/ModsConfig.xml',encoding='utf-8',xml_declaration=True)
    # Dedicated 1.7 settings; never edit the retained 1.6 settings.
    package = next(p for p in manifest['packages'] if p['id']==WAKE_ID)
    leaf = Path(package['fixture']).name
    settings = ET.Element('SettingsBlock')
    block=ET.SubElement(settings,'ModSettings',{'Class':'WakeUp.V17.WakeUp17Settings'})
    for key,value in [('enabled17',args.wake!='off'),('definitionSearches17',not args.only_leaf),('typeNameSearches17',not args.only_leaf),
                      ('leafClasses17',True),('loadingMembers17',not args.only_leaf),('translationMembers17',args.translation and not args.only_leaf)]:
        ET.SubElement(block,key).text=str(value).lower()
    ET.ElementTree(settings).write(profile/'Config'/f'Mod_{leaf}_WakeUp17Mod.xml',encoding='utf-8',xml_declaration=True)
    prefs=profile/'Config/Prefs.xml'
    if prefs.exists():
        tree=ET.parse(prefs)
        values = [('screenWidth','1280'),('screenHeight','720'),('fullscreen','False'),('runInBackground','True')]
        if args.language:
            values.append(('langFolderName',{'French':'French (Français)'}.get(args.language,args.language)))
        for key,value in values:
            el=tree.find(key)
            if el is None: el=ET.SubElement(tree.getroot(),key)
            el.text=value
        tree.write(prefs,encoding='utf-8',xml_declaration=True)
    write(profile/'prepared.json',dict(profile=str(profile), order=order, wake=args.wake, omitted=sorted(omitted),
        minimal=args.minimal, translation=args.translation, wakeEarly=args.wake_early, onlyLeaf=args.only_leaf, language=args.language,
        gameSha256=manifest['gameSha256'],
        modsConfigSha256=sha(profile/'Config/ModsConfig.xml')))
    return dict(profile=str(profile), configuredMods=len(order), wake=args.wake)


def launch(args):
    no_game()
    require(args.authorize_live_launch,'Explicit live authorization flag required')
    profile=ROOT/'profiles'/token(args.profile)
    prepared=read(profile/'prepared.json')
    require(sha(profile/'Config/ModsConfig.xml')==prepared['modsConfigSha256'],
            'Modlist changed after preparation; re-prepare, do not run an accidental recovery/Core-only control.')
    require(sha(ROOT/'game/Assembly-CSharp.dll')==EXPECTED_GAME,'Game drift')
    require(shutil.disk_usage(profile).free > 10 * 1024**3,
            'Less than 10 GiB free for native caches/captures; do not launch.')
    result=ROOT/'results'/token(args.label)
    result.mkdir(parents=True,exist_ok=False)
    run_id=uuid.uuid4().hex
    clock=ctypes.c_longlong()
    frequency=ctypes.c_longlong()
    require(ctypes.windll.kernel32.QueryPerformanceFrequency(ctypes.byref(frequency))!=0,'Clock unavailable')
    require(ctypes.windll.kernel32.QueryPerformanceCounter(ctypes.byref(clock))!=0,'Clock unavailable')
    write(result/'launch-clock.json',dict(runId=run_id,counter=clock.value,frequency=frequency.value))
    arguments=[str(ROOT/'game/RimWorld.exe'),'-savedatafolder='+str(profile),'-NoFileLogCallstacks',
        '-DisableLogStackTraces','-NoCrashDialog','-wakeFixtureDir='+str(result),'-wakeFixtureRun='+run_id,
        '-wakeFixtureMode='+args.mode]
    if args.mode=='load-save':
        require(args.save and Path(args.save).is_file(),'Copied-save source required')
        (profile/'Saves').mkdir(exist_ok=True)
        shutil.copy2(args.save,profile/'Saves/WakeFixtureColony.rws')
        arguments.append('-wakeFixtureSave=WakeFixtureColony')
    with (result/'stdout.txt').open('w',encoding='utf-8') as stdout, (result/'stderr.txt').open('w',encoding='utf-8') as stderr:
        process=subprocess.Popen(arguments,cwd=ROOT/'game',stdout=stdout,stderr=stderr)
    run=dict(schema='wake-up-fixture-run-17.v1',runId=run_id,pid=process.pid,arguments=arguments,
        startedUtc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),prepared=prepared,purpose=args.purpose,
        timeoutSeconds=args.timeout, observerSha256=sha(ROOT/'game/Mods/FixtureObserver17/Assemblies/FixtureObserver17.dll'))
    wake=next(p for p in read(ROOT/'manifest.json')['packages'] if p['id']==WAKE_ID)
    run['wakeDllSha256']=sha(Path(wake['fixture'])/'1.7/Assemblies/WakeUp17.dll')
    write(result/'run.json',run)
    start=time.monotonic()
    while process.poll() is None and time.monotonic()-start<args.timeout:
        time.sleep(1)
    run['wallSeconds']=time.monotonic()-start
    run['exitCode']=process.poll()
    run['timedOut']=process.poll() is None
    run['completedUtc']=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())
    for relative in ['Logs/Player.log','Logs/Player-prev.log']:
        source=NORMAL/relative
        if source.is_file(): shutil.copy2(source,result/Path(relative).name)
    for filename in ['menu-ready.json','functional.json','content.json']:
        if (result/filename).is_file(): run[filename.removesuffix('.json')]=read(result/filename)
    expected_language={'French':'French (Français)'}.get(prepared.get('language'),prepared.get('language'))
    run['languageMatched']=expected_language is None or run.get('content',{}).get('language')==expected_language
    run['automaticTestPassed']=run['exitCode']==0 and 'menu-ready' in run and 'content' in run \
        and run['languageMatched'] \
        and run.get('functional',{}).get('passed') is not False \
        and (args.mode=='menu' or run.get('functional',{}).get('passed') is True)
    write(result/'run.json',run)
    require(not run['timedOut'],'Owned run timed out and was left running for recovery; it is not a successful sample.')
    return dict(result=str(result),passed=run['automaticTestPassed'],exitCode=run['exitCode'],
        menuSeconds=run.get('menu-ready',{}).get('elapsedSeconds'))


def wave(args):
    """Small declared forward/reverse comparison; every sample remains captured."""
    omissions = {
        'all': [],
        'early-all': [],
        'leaf-only': [],
        'no-missile': ['vr.missilegirl'],
        'no-progress': ['ilyvion.loadingprogress'],
        'native-loading': ['vr.missilegirl','ilyvion.loadingprogress'],
        'no-other-optimizers': ['arkymn.performanceesmolas','taranchuk.performanceoptimizer'],
        'no-display-mods': ['ferny.nomodlistonloading','ferny.loadingscreenrepository'],
        'no-giddy': ['memegoddess.giddyup'],
    }
    require(args.case in omissions,'Unknown supplier case')
    records=[]
    for variant in ['native','on']:
        selected=argparse.Namespace(**vars(args))
        selected.profile=args.case+'-'+variant
        selected.wake='absent' if variant=='native' else 'on'
        selected.wake_early=args.case=='early-all' or args.wake_early
        selected.only_leaf=args.case=='leaf-only' or args.only_leaf
        selected.omit=omissions[args.case]
        selected.native_cache_from=None if (ROOT/'profiles'/selected.profile/'TextureCache').exists() else 'current-native'
        prepare(selected)
        if args.warmup:
            selected.label=args.case+'-'+variant+'-'+args.batch+'-warmup'
            selected.mode='menu'; selected.purpose='functional'; selected.authorize_live_launch=True
            result=launch(selected)
            records.append(dict(variant=variant,warmup=True,**result))
            require(result['passed'],'Warmup failed; inspect before more runs.')
    for pair in range(args.pairs):
        order=['native','on'] if pair%2==0 else ['on','native']
        for variant in order:
            selected=argparse.Namespace(**vars(args))
            selected.profile=args.case+'-'+variant
            selected.wake='absent' if variant=='native' else 'on'
            selected.wake_early=args.case=='early-all' or args.wake_early
            selected.only_leaf=args.case=='leaf-only' or args.only_leaf
            selected.omit=omissions[args.case]; selected.native_cache_from=None
            prepare(selected)
            selected.label=args.case+'-'+variant+'-'+args.batch+'-pair'+str(pair+1)
            selected.mode='menu'; selected.purpose='performance'; selected.authorize_live_launch=True
            result=launch(selected)
            records.append(dict(pair=pair+1,variant=variant,warmup=False,**result))
            write(ROOT/'results'/('wave-'+args.case+'-'+args.batch+'.json'),records)
            print(json.dumps(records[-1]),flush=True)
            require(result['passed'],'Comparison failed; inspect before more runs.')
    return records


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['initialize','prepare','launch','wave'])
    parser.add_argument('--profile',default='minimal-on')
    parser.add_argument('--label')
    parser.add_argument('--wake',choices=['absent','off','on'],default='on')
    parser.add_argument('--minimal',action='store_true')
    parser.add_argument('--translation',action='store_true')
    parser.add_argument('--wake-early',action='store_true',help='Candidate constructor directly after Harmony/observer')
    parser.add_argument('--language',choices=['English','French'])
    parser.add_argument('--only-leaf',action='store_true',help='Isolate leaf-class reuse; other candidate features off')
    parser.add_argument('--native-cache-from')
    parser.add_argument('--case',default='all')
    parser.add_argument('--pairs',type=int,default=3)
    parser.add_argument('--batch',default='screen1')
    parser.add_argument('--warmup',action=argparse.BooleanOptionalAction,default=True)
    parser.add_argument('--omit',nargs='*',default=[])
    parser.add_argument('--add',nargs='*',default=[])
    parser.add_argument('--purpose',choices=['functional','performance'],default='functional')
    parser.add_argument('--mode',choices=['menu','quicktest','load-save'],default='menu')
    parser.add_argument('--save')
    parser.add_argument('--timeout',type=int,default=900)
    parser.add_argument('--authorize-live-launch',action='store_true')
    args=parser.parse_args(argv)
    require(os.name=='nt','Windows-only lane')
    require(ROOT.resolve().is_relative_to((REPO/'.rlo-test-instance').resolve()),'Unexpected lane root')
    if args.action=='initialize': result=initialize()
    elif args.action=='prepare': result=prepare(args)
    elif args.action=='wave':
        require(args.authorize_live_launch,'Explicit live authorization flag required')
        token(args.case); token(args.batch)
        require(1<=args.pairs<=5,'Use a bounded wave of one to five pairs')
        result=wave(args)
    else:
        require(args.label,'Run label required')
        result=launch(args)
    print(json.dumps(result,indent=2),flush=True)
