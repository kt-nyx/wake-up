> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_PERFORMANCE_INVESTIGATION.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# OP7 loading performance investigation

Final outcome: repeated measured improvement achieved. Read the
[completed closeout](startup-search-qualification.md) for the final eight-run
comparison, resource tradeoffs, limitations and exact reproduction commands.
The entries below retain the chronological investigation, including superseded
hypotheses and invalidated runs.

## Authority and starting state

The owner authorized autonomous live fixture experiments, ordinary redesign,
implementation, deployment and commits on 2026-09-04. This supersedes older
no-launch, frozen-design and phase approval gates for this investigation.
Normal Steam, Workshop, profiles, saves and caches remain outside the write
boundary. No existing save may be loaded and no release may be published.

Started clean in `C:\rlo-fixture`, branch
`codex/op7-fixture-runtime-integration`, commit
`77c2c737c4695c9b9ce0de7dbc968de44afc2021`. Other worktrees and stashes are
preserved. The permanent fixture is the existing nested
`Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance`;
the sibling spelling supplied in the request does not exist. No fixture is
copied, deleted, relocated or committed. The frozen generation remains
`20260904-182829-e058641c`.

## Initial findings

The prepared `gog-activation-r2-01-baseline` launched, reached a usable menu
with Core and five DLC visible, and exited through Quit to OS. It produced
complete diagnostics but OP7 refused installation with `harmony-file-unavailable`.
This is an activation defect, not a candidate performance result. The pinned
assembly identity remains the accepted GOG contract; the live game's version
string says rev574 although the installation record calls the contract rev573.

Code review found that X01 catalog hits still capture and parse every XML file.
The candidate additionally hashes game/Harmony twice per group, hashes all
loaded mod code for each group's catalog foundation, and rereads source XML
before commit. These are hypotheses for the historical slowdown until timed.

## Measurement method under development

Compare the full selected frozen order with the Wake-Up package physically parked
outside game/Mods against the same order plus Wake-Up. Separate full diagnostic
snapshots from timing-only observation. Start in the activation subset, then
use all 315 original enabled packages for representative comparisons.

External elapsed measurements end at Unity's startup asset-cleanup log block;
they exclude time sitting at the menu. Confirm usable menu separately. Treat
this as an observed cleanup endpoint, not an exact first-frame measurement.
Record Wake-Up stage times separately. Alternate comparable repetitions. A clean
fixture profile means cold application caches, not a cold Windows filesystem
cache; do not clear system or normal-game caches to manufacture cold results.

Detailed logs and measurements stay in the ignored permanent fixture results.
This investigation is in progress; no loading improvement is claimed yet.

## First live measurements and causes

Runtime admission was repaired in small, measured steps. The memory-loaded
Harmony supplier needs a fixture-local physical lookup. Raw loader IL hashes
changed when Prepatcher renumbered references; the GOG check now resolves
instruction operands. Live canonical dumps then proved that Harmony's
NoInlining hint and the offline reader's generic required-modifier handling
caused remaining comparison differences. The check now ignores only that
inlining hint and preserves signature modifiers. Original and serialized
fingerprints agree; 29 focused adapter/semantic/thread/observation checks pass.
Live Mono subsequently installed and executed the original catalog candidate.

Source/package checkpoint: `20b2eeb6600d7dd4dce0c59437aa943986fe47fc`.
Earlier refusing runs are retained as debugging evidence and never counted as
active optimization results. All activation launches reached the usable menu
and exited through Quit to OS, without loading a save.

Activation observations below are preliminary single runs except absent,
which has two repetitions. They use clean fixture application caches, warm or
uncontrolled Windows filesystem caches, and the same content in each arm.

| Arm / label | XML load | All mod loading | External cleanup endpoint |
|---|---:|---:|---:|
| Absent `perf-a01-absent` | not instrumented | not instrumented | 8.8584 s |
| Absent `perf-a03-absent` | not instrumented | not instrumented | 8.7901 s |
| Timers, original loader `perf-a08-thread-baseline` | 295.134 ms | 1948.224 ms | 9.2185 s |
| Original loader, 4 total workers `perf-a07-threads4` | 265.003 ms | 1986.357 ms | 9.1330 s |
| Original loader, 8 total workers `perf-a09-threads8` | 412.367 ms | 2124.818 ms | 9.4262 s |
| Catalog cold `perf-a10-catalog-cold` | included in total | 6276 ms | 13.5806 s |
| Catalog warm `perf-a11-catalog-warm` | included in total | 6146 ms | 13.4723 s |

The catalog cold run spent 1330 ms in repeated admission checks, 1666 ms in
precommit revalidation, 562 ms selecting inputs, 297 ms building the catalog
foundation, 445 ms capturing/preparing XML, and 94 ms publishing the catalog.
It committed five groups (148/212/219/198/231 files) with complete joins and
ordered owner-thread publication; Core's larger group fell back after hitting
the 512-file bound. The warm run hit all five catalogs but still read and parsed
the XML and took almost as long. Peak reported catalog memory was 322,466,502
bytes, with four CPU/I/O slots and four open handles.

The worker-count experiment preserves the original enumerator, duplicate
resolution, parser constructor, indexed results and complete joins. Vanilla
already uses two background threads plus the calling thread. Both experiments
processed 1572 files across ten nonempty groups, including Core. Four workers
saved only 30 ms in XML loading in the first pair; eight were slower. Neither
demonstrates an end-to-end improvement over Wake-Up absent. Definition construction
(`ParseAndProcessXML`) took about 1.07 seconds, substantially longer than raw
XML loading in this small profile.

Direction: discontinue the metadata catalog as a performance candidate unless
it can remove real work. Measure the complete frozen representative profile
with Wake-Up absent, then instrument it and compare the smaller concurrency change.
Do not optimize unmeasured stages or describe reduced diagnostic overhead as
a product speedup over absence.

## Representative profile discovery

The frozen `vr.missilegirl` package (Workshop `3712928623`) supplies
`1.6/Assemblies/Cosmodrome.dll` and `1.6/Plugins/Stable/Gagarin.dll`.
Package-ID-only inspection incorrectly concluded Gagarin was absent. Its
Harmony owners include `VR.MissileGirl` and `VR.MissileGirl.Gagarin`. Its XML
constructor postfix serializes/hashes source XML, and its loading hooks cache
combined/patched XML. Bypassing that constructor loses mod behavior; the
worker-count experiment keeps it. IL and live files confirm its cache path
derives from the selected game configuration root and stays within fixture
`profile/MissileGirl/Cache`. Fresh and restored-cache comparisons must be
reported separately, without changing this mod or its functionality.

The existing frozen Loading Progress 0.14.0 settings already enable startup
impact tracking and automatic saving of `profile/StartupImpactData.xml`.
`sessionData/loadingTime` is milliseconds from Loading Progress construction
through the AbstractFilesystem.ClearAllCache completion observer. It excludes
earlier process/Prepatcher startup and the subsequent report write. It also
records per-stage/per-mod metrics and counts. This common instrumentation is
present with Wake-Up absent and enabled; it is used alongside external timing and
menu verification rather than adding another measurement mod.


First representative absent run `perf-r01-absent` reached the menu and exited
normally (exit 0), without loading a save. External startup cleanup: 746.4278 s;
existing Loading Progress clock: 715.054125 s. Fresh application caches;
Windows filesystem caches uncontrolled/warm after earlier fixture inspection.
315 packages, 47,449 parsed definitions, 14,748 patch operations. This is one
run, not a repeatability claim. Existing startup errors are retained in its log.

Aggregated mod-attributed report times: textures 335.527 s, XML patches
102.075 s, static constructors 88.125 s, mod constructors 65.303 s, XML file
loading 15.467 s, audio 15.450 s. Base-game definition construction is 12.401 s.
These stage and worker figures are attribution, not additive wall-clock totals.
They justify investigating texture loading rather than expecting XML workers
to solve most of this profile's delay.

The next XML experiment keeps the original generated deserializer and replaces
its repeated linked-list indexing with a bounded sequential cursor. It retains
live-list behavior and falls back upon structural mutation. This addresses
actual repeated work rather than adding another metadata catalog. Separately,
the existing representative texture path is under read-only investigation.

Vehicle Framework rewrote `Updates/UpdateLog.xml` during absent startup.
A complete mod metadata comparison found this sole changed file. Its SHA-256
and length remained exactly frozen; only mtime changed. Capture now restores
that one known file's frozen mtime only after exact content authentication,
and records the restoration. Actual content changes still refuse. No normal
Workshop file was touched and no fixture content was removed.


## Timing boundary correction and next checkpoint

**The previous external cleanup endpoint is NOT complete load-to-menu time.**
Pinned `PlayDataLoader` calls filesystem cache cleanup and Unity asset unloading
before later queued mod callbacks. Loading Progress stops at that cache cleanup.
The absent log has patching through 23:55:01, over a minute after its first
cleanup, followed by more CE startup work. Keep 715.054/746.428 seconds only as
partial intervals. Exact complete startup cannot be reconstructed for that run.

Pinned `UIRoot_Entry.ShouldDoMainMenu` refuses while a long event is running or
queued. Future full-load comparisons use timestamped last-loading and first
unobscured-main-menu screenshots, bounding observation uncertainty explicitly.
Core-stage clocks remain useful to attribute costs, but cannot prove end-to-end
improvement by themselves. Menu dwell remains excluded.

The indexed candidate's `perf-a12-indexed`, `perf-a13-indexed`, and
`perf-a14-indexed-texture` runs safely refused before optimization. Its dictionary
builder fingerprint throws BadImageFormatException on live Mono, despite
original/serialized/offline parity. Investigation is narrowed to identifying
that exact operand; these runs are not active candidate performance evidence.
All reached the menu and exited normally. Texture attribution installed in a14
and reported zero texture source files in the activation profile (official
content is supplied through other game assets).

Source/package `986a2a0abeeccc05b236c25e13e88f5058faeedf` includes optional
`--texture-timing`, currently running as `perf-r02-texture-baseline` with full
representative content, original worker count, clean application caches and
optimization disabled. It counts texture reload, enumeration, DDS load, DDS
creation/upload and PNG conversion with aggregate timestamp counters, not
per-file evidence writes. It will identify where to direct the next change.


The indexed experiment now uses focused getter admission rather than a complete
fingerprint of unrelated generator instructions. Its patch requires the exact
unique `Callvirt`, child-list MethodInfo field, `ILGenerator.Emit(OpCode,
MethodInfo)` sequence, and checks the field currently contains the native
XmlNode.ChildNodes getter. Pinned game/library identity, foreign-transpiler and
XML accessor refusal, empty generated-delegate caches, native element scope,
mutation fallback and resource limits remain. Offline golden fingerprints are
retained as original-build reference tests, not a live admission blocker.
This deliberately accepts a narrow limitation: non-Harmony changes elsewhere
in the builder are not completely fingerprinted. No such change was evidenced;
real generated-parser behavior is the next necessary check.


`perf-r02-texture-baseline` is INVALID as a performance comparison. The new
profiler patched a closed generic texture-holder method; Mono shared that
implementation with audio holders, and hundreds of AudioClip resolution errors
appeared that were absent from r01. Its 7.455-second texture total is not a
speedup claim. The profiler is corrected to patch only the two non-generic DDS
methods. Texture totals will come from the existing Loading Progress report.
The full diagnostic must repeat and preserve the absent error/content baseline.


Current package `f75586724b8a15275e919fe9152e0edffada2ff2`: indexed a15
successfully activated on the small profile, processed 24,581 cursors and
220,610 sequential steps with zero mutation fallback and no parse failure.
ParseAndProcessXML took 1086.387 ms; no small-profile improvement established.
The active next representative run is `perf-r03-dds-baseline`, with corrected
non-generic DDS timers, cursor disabled, fresh application caches and full
content. Its separate ParseAndProcessXML timing hook refused a foreign-patch
composition; DDS timers installed independently. Use the existing Loading
Progress parse metric. The prior invalid r02 is permanently excluded.


## Corrected DDS attribution: promising product target

`perf-r03-dds-baseline` completed and exited normally with original loading
behavior. Loading Progress's partial interval is 605.478 s; first Unity cleanup
619.806 s. Texture category: 237.746 s, audio 11.850 s. Non-generic DDS timers
counted 39,978 loads, 236.231 s total and 25.257 s inside CreateTexture: about
210.974 s in the surrounding open/map/header/dispose path. This supports a
bounded buffered read that retains the entire original DDS parser and texture
creation. File mapping is lazy, so CreateTexture can still include page-fault
I/O; the comparison must use total loading rather than moving work between
sub-stages.

The absent run's335.527-second texture interval and this237.746-second interval
also show large run/cache variability. No optimization speedup is inferred from
those different baseline runs. Repeated absent/candidate comparisons remain
required. The main-menu UI interval for r03 is broad because mouse attempts to
close the existing debug overlay delayed confirmation; Escape works reliably.
Future timing should dismiss it immediately and use close-spaced screenshots.

The `textures` smoke profile retains Prepatcher, Harmony, all official content
and frozen Vanilla Textures Expanded (`vanillaexpanded.vtexe`,2016436324).
Its only required mod dependency is Harmony. This exercises DDS loading with
identical content in its absent and candidate arms; the representative profile
remains unchanged. The older activation profile had no mod DDS sources.

The first texture smoke (`perf-t01-absent`) is excluded: VTE's current assembly
needs ModSettingsFramework.PatchOperationWorker, supplied by frozen Vanilla
Expanded Framework despite not declaring that mod dependency in About.xml.
The textures lane now includes that framework in BOTH absent and candidate
orders. No frozen files were changed; the first incomplete smoke is retained.


DDS smoke checkpoint: corrected `perf-t02-absent` reaches a clean menu;
partial cleanup11.8682s. Candidate `perf-t03-buffered` and `perf-t04-buffered`
safely refused before optimization, with cleanup11.8031/11.8103s; these are not
active candidate results. Original DDS timers on t03 counted2853files with
484.972ms total/126.316ms creation,zeroerrors. Same small-profile content.

The full t04 exception identifies an OLD XML CONTRACT BINDING failure, before
DDS adapter creation: Op7RuntimeContract.RequireField in ResolveCurrent.
Earlier InvalidOperationException entries described as possible foreign-patch
composition were not established as such. The exact t04 cause is fixed-token
binding of unrelated XML bookkeeping fields after other startup rewriting.
The DDS experiment now needs only reusable pinned physical game/Harmony checks
and its reviewed DDS source-callsite contract. Do not weaken the original
catalog's checks merely to make this independent experiment initialize.


## Active DDS candidate checkpoint

Source/package `5024abec4045a48d447eae1ebf12fad41a0ecbb5` separates binary
admission from unrelated XML bindings. Release builds without warnings/errors;
nine focused buffered-DDS checks pass, including binary identity, bounded pool,
complete reads, failed/truncated/growing-file fallback and emitted adapter
branch/header/payload semantics. Original DDS IL retains both CreateTexture
calls and its sole finally/disposal path.

`perf-t05-buffered` ACTUALLY activated in Mono and reached the menu cleanly:
2853DDS files,293331836bytes buffered,zero mapped fallbacks,one8MiB buffer peak,
zero outstanding leases,zero DDS errors. Total DDS668.505ms,creation58.767ms;
partial cleanup12.1309s. The prior mapped small trial was484.972ms DDS and
126.316ms creation, with absent cleanup11.8682s. No small-profile gain is
established. Moving I/O ahead of creation changes substage attribution, not by
itself speed. The complete profile is necessary because its per-DDS surrounding
cost is much larger.

`perf-r04-buffered` completed with this active candidate, the same corrected
DDS timers, fresh application caches and unchanged content. DDS total fell
from r03's236.231s to10.510s over exactly39,978calls,zeroerrors. Its nested
CreateTexture interval fell25.257s to1.031s; do not add nested durations.
Loading Progress's partial interval fell605.478s to348.956s; its mod-texture
category fell237.746s to12.209s. First Unity cleanup was361.4763s.

The candidate buffered39,962files/5,078,054,119bytes and used16mapped
fallbacks. Peak was one8MiB buffer, with zero outstanding leases at completion.
Independent comparison found identical definitions47,449/patch operations14,748
and exactly the absent r01 problem-pattern set: no new entries, no missing
entries, zero audio errors. These results support the reader change while
preserving the original parser, creation calls and content.

This is a promising FIRST full-profile result, not a repeated speed claim.
r01's optimizer-absent texture interval was335.527s, exposing substantial
baseline variation. Main-menu confirmation at05:00:53.371UTC was delayed by
the debug overlay/focus; that is an upper bound, not exact loading time.
Explicitly activating the game window before Escape fixed dismissal. Repeated
absent/candidate runs must use prompt menu observation and comparable caches.
`perf-r05-absent` now repeats the full profile with Wake-Up physically parked.

Source review identified one compatibility repair before the next candidate:
Harmony publishes patch state after rebuilding its method wrapper, so checking
only inside our transpiler can miss the first later foreign transpiler. A
cached per-open publication check now uses the pinned Harmony dictionary's
fresh byte-array identity; unchanged state avoids repeated deserialization.
Changed/foreign/failed checks fall back to the original mapped source. Tests
cover the first late addition, removal and cache reuse. Build/tests are deferred
until the timed absent run closes.

## Repetition rejects the initial DDS performance claim

`perf-r05-absent` completed with Wake-Up physically absent. Its texture total was
11.041s, its Loading Progress partial interval338.931s, and first cleanup
350.8322s. That is comparable to, slightly faster than, buffered r04's
12.209s/348.956s/361.4763s. The first slow mapped runs versus buffered r04
were confounded by Windows file-cache warming. No reliable DDS improvement is
established; do not promote236s-to10s as an optimizer result. Fresh fixture
application caches are not a cold Windows filesystem cache. No global cache
purge or normal profile/cache modification is authorized by this procedure.

r05 still performed47,449definition parses and14,748patch operations. Its
remaining major intervals are XML patch application96.878s, mod static
constructors85.271s, mod constructors54.795s, plus late queued initialization
outside Loading Progress's partial clock. Its debug overlay appeared at
05:12:18.212UTC, before CE finished generating object parameters; UI became
unresponsive/black and a usable menu was first observed05:13:32.070UTC.
Treat that menu observation as an upper bound; the overlay alone is not ready.

The bounded DDS approach remains an experiment, with a repeated comparison
still useful to separate its small overhead. Twelve focused checks now pass.
Compatibility review also added last-priority unchanged-instruction admission
and preserves the original typed local on mapped fallback. Explicit foreign
Harmony ordering can still run after our transform; arbitrary foreign
transpiler interoperability is not claimed.

Direction changes again: investigate the remaining XML patch, constructor and
late CE costs using exact frozen code and reports. Do not repeat the original
cache/worker design merely because it was the initial plan.

The guarded DDS smoke t06 activated cleanly:2853buffered files,one8MiB buffer,
zero mapped fallbacks/outstanding leases and one full Harmony patch-info read
across all files. Cleanup12.2376s; this validates the guard, not a speedup.
`perf-r06-buffered-no-timers` repeats representative candidate loading with the
separate DDS stopwatch hooks omitted. Existing frozen Loading Progress remains
identical in every representative arm.

New evidence-led experiments, each independently selectable:

* `def-lookup`: Combat Extended accounts for62.414s of r05's96.878s XML patch
  category. Many queries repeatedly find the same literal defName among root
  definitions. Scope a bounded index to original patch application, retaining
  original operations, remainder XPath evaluation and mutation fallback.
* `type-lookup`: pinned Harmony TypeByName repeatedly materializes all loaded
  types on direct-lookup misses. Several expensive optional-mod compatibility
  probes call this path. Retain direct lookup/loading semantics and invalidate
  any reused fallback search data when assemblies load.
* `character-presets`: the late log prefix CE here means CHARACTER EDITOR,
  not Combat Extended. Its9128object-preset constructions repeatedly rebuild
  the same82-turret lookup by scanning/sorting all definitions. Investigate
  narrowly scoped reuse inside the original preset construction; do not skip
  presets, callbacks, mod content or functionality.

These are hypotheses under implementation, not accepted speed claims. Their
source and tests are kept separate while the current timed run proceeds; no
builds run concurrently with fixture loading.


## Search experiment verification checkpoint

After r07 closed,43 focused adapter checks passed, covering the three new
strategies plus the shared published-patch guard;17 fixture checks passed.
Source review fixed XML callback-order stale-index behavior and entity-expanded
children, made shared patch decisions immutable for concurrent callers, and
added all-patch-kind guards for Harmony type enumerators. No tests/builds ran
during timed game launches. The new `presets` smoke lane retains all official
content plus Prepatcher, Harmony and Character Editor in frozen source order.

r06 buffered without separate DDS timers: Loading Progress346.253s, textures
10.396s, first cleanup358.8777s. Definitions47449/patches14748 preserved.
This repeats the lack of useful DDS gain against warmed-file-cache absent r05.
r07 absent restored only r05's captured Gagarin cache; live log confirmed
finished loading XML from cache. First cleanup248.4395s. Compare it only with
other verified Gagarin-warm runs, not fresh-cache candidate runs.


Live search smoke: a16 type lookup installed but had zero calls in the official
profile. t07 definition lookup actually served111hits/27native fallbacks with
11workers,15bounded index builds and3643peak entries; clean menu/normal exit.
p01 Character Editor safely refused `character-editor-not-active` because
Root.Start runs before the completed active-mod list. Its small profile DID
include Character Editor and created1628objects/12turrets. New search strategies
now defer admission to the existing WakeUpMod constructor, when the active
mod list/assemblies exist. This also makes the optional CE worker discoverable
for definition lookup. Repeating activation is required before full measurement.


## First full search results (repetition still pending)

`perf-r08-character-presets` preserved all47,449reported definitions and14,748
patch operations, the full problem-pattern set, and all9,128object/82turret
presets. The original scopes completed in2.728s/0.810s with one dictionary build
per scope,9,208hits,zero fallbacks/exceptions and zero retained entries. Menu
was observed by431.679s, before r05's last unresponsive observation437.230s;
the46.391s difference between first-menu observations is NOT an exact saving.
Its earlier partial clock348.916s was9.985s slower than r05, so that stage
variation cannot be credited to this late optimization.

`perf-r09-type-lookup` preserved the same counts and the exact normalized
functional status lines as fresh-cache absent r05. Gagarin remained cold and
original XML patching still took101.279s. Loading Progress partial235.271s
versus338.931s; constructors17.610s versus54.795s and static constructors
23.955s versus85.271s. Of791type calls,119missed and none threw;150fallback
searches used the index, one used original enumeration. Seven index builds
covered68,880types as assemblies loaded; dynamic assemblies stayed fresh.
Menu was observed by379.675s, versus absent437.230–478.070s bounds. Existing
background1066/1065patch work completed earlier in the log rather than being
omitted. This is promising single-run evidence; repeat the final candidate.

`perf-r10-def-lookup` is running with12 methods patched (query-call adapters,
not background threads). Its original XML patch scope completed in21.297s
with13,103hits/2,124fallbacks,232index builds,121invalidations,31,403peak
entries and4,096cached queries. Complete content/output comparison remains
pending capture. A combined `startup-searches` selector initializes the three
independent search components with their existing per-component admission and
fallback; it never activates the earlier DDS/catalog/thread experiments.


Definition experiment r10 completed and exited normally. Independent byte-level
comparison found patched Unified.xml identical across r05(absent),r08,r09,r10:
89,144,185bytes, SHA2568866d024b518f79f28383426a760232765e3700cc269d57401d05f33a3198119.
Unified_Original.xml and all four per-definition Reports are identical too.
This is direct output evidence for preserving XML patch semantics, beyond
matching definition counts. First cleanup276.7852s; tight menu observation is
recorded by the delegated observer, with no save loaded and normal Quit to OS.

Next: combined startup-searches activation, original-code timing attribution,
then repeated optimizer-absent/combined comparisons with fresh application
caches and separately verified restored Gagarin caches. Windows file cache
remains uncontrolled; do not reuse the early slow-DDS baselines as final wins.


## Combined checkpoint 65f6417 and original-cost attribution

Combined package `65f641736917246bd0225f823d79cb2b0542fb87` built with zero
warnings/errors. `perf-p04-startup-searches` activated all three components,
reached the unobscured small-profile menu and quit normally. Original outputs
were1628objects/12turrets, with101.762ms/1.916ms scopes; XML29hits/0fallbacks,
30.643ms; this small lane has no expensive TypeByName fallback calls.
Independent read-only combined review found no concrete new conflict: distinct
patch targets, independent publication guards and correct scope cleanup.

`perf-r11-searches-original-timing` keeps original code and narrow timers:
LP340.029406s versus absent r05 338.931s; cleanup352.6124s versus350.8322s.
That single-pair difference is1.098s/1.780s, not a general overhead guarantee.
Original TypeByName791calls/119misses/151fallbacks/0errors consumed128.621093s.
Original XML stage consumed98.194948s. CharacterEditor created9128objects in
60.355098s and82turrets in0.969669s. These timings identify real expensive work;
overlapping scopes/categories must not be added as independent wall time.
Menu first observed06:25:23.308Z after start06:17:28Z, lastunresponsive
06:25:06.757Z. Peak sampled process working set8567468032bytes. Normal Quit,
exit0, no save loaded. Combined full run r12 now in progress.


## Repeated fresh application-cache result

Unchanged package65f6417 completed combined r12/r14, alternating with physically
absent r13. LP partial clock: absent r05/r13=338.931/340.987156s; combined
r12/r14=152.773109/152.472219s. Cleanup: absent350.8322/353.2453s; combined
165.4680/165.4444s. Two-run means are339.959078vs152.622664s (LP) and
352.038750vs165.456200s (cleanup). These partial endpoints exclude late mod
initialization. Menus: r12bounds222.881-238.444s; r14bounds211.946-232.453s;
absent r05bounds437.230-478.070s andr13bounds475.312-491.461s, relative to
second-rounded run start. R14 observer missed an earlier loading screenshot;
its last-unresponsive/first-unobscured-menu bounds remain usable.

R12 XML/report hashes, content counts, functional-status and problem-pattern
sets match bothabsent r05andoriginal-timedr11. R13 reproduced all these withRLO
absent from actualargs/order/receipts/log. R14comparison pending. Candidate
process peakworking sets r12/r14=9113755648/9187667968bytes, compared with
absent r13=8445308928 andoriginal-timedr11=8567468032bytes. These include the
whole game and allocation/GC variation; not isolated index-memory measurements.
Noextraoptimizationworkers. Warmcandidate r15 restoresonlyr12Gagarincache;
actual cache-hit confirmed. Repeatwarmcomparison remains in progress.


## Final warm repetition and closeout

Warm absent r07/r16 Loading Progress means234.8851795s versus125.760961s for
combined r15/r17 (46.46% lower); cleanup246.9591s versus138.69945s (43.84%).
Both candidates reached the menu by202.472s; both absent warm runs were still
unfinished at or after356.731s. All cache-hit logs verified. No ordinary XML
patch work was reintroduced: candidate stage scopes21.342/30.858ms,0indexbuilds,
2fallbackqueries. Type791calls/119misses/0errors andallpresetcounts retained.
All eight comparison runs exited0. Full final details are in the closeout.

Promoted startup-searches to fixture workflow default, retaining explicit
catalog experiments and historical receipt defaults. Seventeen workflow tests
passed after this change. Final metadata audit preservedmanifestb9b53c8b...,
309physical packages/315enabled inputs; deployedruntime matches. No game process
remains. Otherworktrees/12stashes/main.gitignoreeditpreserved. Runtimepackage
remains65f6417; latercommitscontainworkflowdefaultsandevidence,notchangedDLLcode.
