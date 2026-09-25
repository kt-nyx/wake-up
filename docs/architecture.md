# Architecture

The 0.4.0 candidate retains the retirement boundaries below and adds operation-level
compatibility decisions described in the [compatibility guide](compatibility.md).
See [candidate notes](release-notes-0.4.0.md) for its actual qualification and measurements.

## Released 0.3.0 boundary

The release no longer selects parsed XML, inheritance caching, ordered read-ahead,
parsed-language persistence, faithful authored-DDS recaching or managed first-build
decoding. Existing coordinated XML bridges remain for processed XML and diagnostics;
the retired consumers stay inactive and delegate to native behavior. Old stores may
still be cleared explicitly. On-demand audio/texture/graphic and progressive work
remain dormant; automatic audio/bootstrap prepatch registration is removed.

Warm PNG/JPEG/PSD output reuse keeps its holder checks and shared storage. Cold
PNG/JPEG pixels come from native decoding. PSD decoding remains explicit. DDS bypasses
faithful cache reads/writes and native-preparation batches, while the native DDS
capture foundation remains available to explicit C08 quality conversion/export.
The quality helper is separate from the disconnected raw-pixel trial.

Sections below preserve mechanism/history detail; descriptions of the retired or
deferred paths are research reference, not release activation or acceptance claims.
See [C15A](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c15a-retained-candidate.md) for current boundaries.

Wake-Up avoids repeated loading work while keeping the game's normal order and
objects. Each selected feature checks the methods and cooperating hooks it uses;
unsupported conditions normally retain the native route. A hook intercepts or
rewrites a method, so coexistence depends on exact behavior, not merely a matching
mod name. Experimental audio has additional explicit consumer limits below.

This reference describes the released source, including inactive research retained
in that source tree. Historical reports apply only to their recorded binaries.
See [current state](current-state.md) for release identities and
[history](history.md) for stopped approaches. There is no active replacement plan.

## Background loading, native summary and XML timings

`BackgroundLoadingSession` owns temporary Unity background permission for
supported ordinary saves and native world-page creation. Saves wait for Play-scene
arrival; world generation completes in Entry. Shared queue, callback and fade
completion releases ownership and restores the current user preference, preserving
native pause behavior. Unknown lifecycle hooks refuse or release the session;
exact coordinated startup-completion hooks may coexist.

The background-loading option extends its owned session to ordinary
`PageUtility.InitGameStart` entry. A separate seven-method colony contract covers
preparation, `Game.InitNewGame` and its callbacks/error handling, while the shared
scene/queue/fade completion and current-preference restoration remain. Standalone
maps, world-map opening and initial direct/autostart entry remain outside admission.

`NativeLoadingSummaryRuntime` adds one guarded boolean filter to
`LongEventHandler.LongEventsOnGUI`, so native summary layout and drawing both
follow a separate default-off choice. All other native instructions, loading
acknowledgements and callbacks remain. `LoadingDisplayRuntime` recognizes only
this exact coordinated transpiler alongside its existing Prepatcher drawing
prefix contract. Active Loading Progress and unknown drawing hooks refuse the
choice. No summary method or mod-manager metadata is globally replaced.

`LoadingTimingRuntime` separately observes native `XmlAssetsInModFolder` and
`LoadPatches` with prefixes and void exception-preserving finalizers. Native
workers, joins, parsing, returns and order stay intact. The report bounds 512
records/32 open scopes and labels nested/overlapping durations without adding
them as wall time. A later `Root_Entry.Update` postfix finishes after queue drain;
patching the already executing `DoPlayLoad` from mod construction would miss
first-launch completion. World/colony guards recognize only this exact postfix.
Settings expose the report and its best-effort text file; many other stages are
explicitly not profiled. Existing display diagnostics sample retained feature
statuses on their 250 ms budget, with attribution only when timings are selected.

The changed parsed-language cost premise remained non-repeatable; no parsed
cache/input consumer, storage category or broader texture format/atlas route was
added. See [R4c source/evidence and capability table](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r4c-remaining-coverage.md).
F1 exercised normal report completion and layout code; actual summary hiding
and new-colony lifecycle remain untested. Prior live reports qualify their recorded
older core and narrower behavior.

The R4b native XML mutation candidate is a rejected, test-only experiment under
`experiments/XmlRegion`. It preserved original node references and source records,
but complete reuse cost exceeded ordinary processing. No region prepatch,
runtime registration, setting or storage category ships. See the
[R4b mechanism and disposition](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r4b-retained-xml.md).

## Ordered concurrent XML input

C03 adds default-off `orderedInput`. It factors the exact native filesystem
selector and schedules selected read/hash jobs across admitted mod boundaries.
Two workers own a queue of at most 24 jobs and 32 MiB of byte reservations;
results feed actual native constructors in file/mod order. C01 consumes the
same bytes/digests for its unchanged generation key and parser/cache path.
No worker runs mod constructors, XML callbacks, virtual providers or Unity work.
Native source hooks, diagnostics and publication retain their positions.

Held read leases keep queued bytes immutable until consumption. Cancellation
joins workers and releases leases before native fallback; C01 private batch
failures cannot replay constructors. Unknown interval hooks stay native, with
exact small contracts for the relevant Prepatcher and settings-framework
callbacks. The [C03 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c03-ordered-input.md) describes
bounds and actual consumed overlaps. The 11 September qualification retains this
read/hash design after worker-parsing and caller-help experiments failed to
establish useful benefit. Native XML factory guards also cover CDATA, processing
instructions, significant whitespace, whitespace and comments, preventing unknown
callbacks from running while future inputs are leased. Final functional checks
pass; standalone and combined useful performance remain unqualified. See the
dated C03 record for measured regressions and independent-review status.

## Experimental streaming XML input

`StreamingXmlPrepatch` adds an inert entry to the exact supported filesystem XML
constructor after native attribution. `StreamingXmlRuntime` streams UTF-8 text
into the native XML parser with 4 KiB buffers, at most three concurrent admitted
reads and a 16 MiB source limit. Native enumeration, workers, joins and indexed
publication remain unchanged. The complete original constructor handles disabled,
unsupported, hooked or failed cases. Native type-name/delegate caches already
cover the separate proposed memo; Harmony/leaf searches are unchanged.
See the [R4a report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r4a-ordered-reads-types.md) for lifetime,
Gagarin compatibility, focused evidence, costs and live qualification gaps.

## Experimental deferred audio

R3 postpones real stream opening and clip creation for native streaming-size
filesystem audio. `DeferredAudioPrepatch` coordinates with the existing texture
prepatch to cover native per-path/folder requests, public holder exposure, cleanup
and compiled `SongDef.clip` reads across eligible mod assemblies. Inactive bridges
use ordinary getters/fields and do not initialize optional private accessors.

`DeferredAudioRuntime` owns pending source metadata and a bounded menu warmup
queue; `DeferredAssetSet` retains entries through failed creation/publication and
cancels stale completions. Native holders own published clip/stream lifetimes.
First use returns native ready content on the main thread. Unsupported detected
consumers refuse; arbitrary dynamic/reflected access and worker-thread access
remain the explicit experimental boundary described in the
[R3 report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r3-deferred-assets.md). No new persistent store,
audio codec, general asset placeholders or progressive DDS mode is added.

## Native texture preparation and optional Windows DDS exports

Wake-Up supports [broad texture batches and Windows DDS exports](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/r2-texture-preparation.md).
`TexturePreparationBatch` shares testable admission, provider selection and final
publication decisions with the incremental menu workflow. Native capture admits
bounded compressed possibilities and checks actual output. C05 automatic caching
and preparation now share the same completed entry; no duplicate promotion is needed.
`PreparedTextureExports` owns ordinary DDS files and atomic source/path mappings
under the same `OwnedCacheStore` category lock and shared overall allowance as native
entries. Portable export identities and top-row DDS representation never enter
the native startup resolver. The optional Windows v2 helper is now connected to
explicit full/half/quarter export actions; native use and all preparation remain
default off. This supersedes the historical S10 source-only helper disposition. Native
preparation needs no helper; exports are a separate supported optional workflow.
F1 exercised one native JPEG through preparation, restored rendering and later startup,
plus all three DDS export sizes. This does not qualify arbitrary consumers or visual UI.

## Successful asset routes

C09 remembers where a successful asset request resolved, then asks that live
provider for the current object. The default-off option covers filesystem
texture/audio/string holders, Resources and exact bundle/name routes including
shaders. It stores no assets or misses and changes no native readiness or quality.
Provider/order/dictionary/list changes invalidate proofs; bundle proofs also
check live engine identity, path metadata and mutable extension arrays.

Resources callbacks still execute once per global external lookup. Direct bundle
searches remain independent of Resources. Missing admitted lookups resume the
original diagnostics; unknown lookup hooks and unsupported worker/reentrant
calls retain the original method. The exact C08 holder-read prefix may coexist:
shared path exposure preserves its protection even when an external route skips
holder misses. No other prefix or patch position gains that allowance.

Direct holders already use dictionaries; folder queries already use native
prefix tries. These stay native, preserving their distinct ordering, duplicate
and public-mutation behavior. Legacy experimental deferred audio retains its
native audio lookup when coenabled. See the [C09 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c09-broad-routes.md)
for current qualification and pending performance, and the archived S11/R1
reports for historical texture-only evidence.

## Independent parsed XML and remaining mechanisms

C01 now provides default-off independent parsed XML generations selected from the
game's actual ordered source files. Warm decoding constructs separate source and
combined documents together, avoiding text parsing and native `ImportNode` copies.
Unknown source observers retain native behavior. See the [C01 implementation and
evidence](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c01-independent-parsed-xml.md) for bounds, identity
checks and functional coverage; no performance gain has been established.

C02 separately reuses completed patch output and resolved inherited children.
Processed setup runs after the reviewed native settings callback and before query
tracking. A private snapshot restores the complete combined document, detached
source-map roots, interned names and operation state. Native mod/settings wrappers
still run; exact CE gun adapters replay their recorded native mod lookups. Unknown
operations end the reusable prefix, and diagnostic output prevents publication.
Inheritance keeps native registration, parent selection and diagnostics, replacing
only the original clone/merge instructions with fresh detached cached results.
The additional live-query path reuses private XPath syntax plans while retaining
native lazy lists and evaluation; single-node result/miss entries are discarded
on XML mutation. All three options default off. See the [C02 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c02-processed-xml.md)
for exact coverage and pending measurements.

C04 adds default-off parsed language reuse at the actual native `LoadData`
consumer. Native discovery, read barriers, duplicate registration, legacy-folder
warnings, icon callbacks, WordInfo construction and translation application stay
in place. The keyed and injection parser loops restore only each group's final
changed entries; Strings restores appended lines after the successful native
read. This avoids replaying parse/merge operations while preserving existing
dictionaries, lists and untouched records. Records are captured before
application, and each read decodes fresh mutable objects. Source text/order,
selected/default language, parser/merge contracts and relevant Def-name context
bind entries. Unknown shared callbacks or unsupported/diagnostic groups remain native. Unknown
name-conversion callbacks keep injection parsing native without suppressing
independently guarded keyed and Strings reuse.
The owned store participates in the common budget, bypass, rebuild and clear;
load completion releases its language owner. See the [C04 record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c04-language-reuse.md)
for exact boundaries and functional evidence. Performance remains pending.

R1's
`experiments/XmlPrefix` and R4b's `experiments/XmlRegion` compile only into tests,
without runtime automatic registration, ordinary settings or maintenance.
Their complete tested costs lost to ordinary processing. Earlier parsed-input,
grouped-language and first-build PNG experiments likewise added no runtime path;
the revised R4c language premise did not show a repeatable gain. These are bounded
negative results, not proof that every possible alternative must fail.

General deferred/progressive textures, complete-atlas reuse,
broad format conversion, a full profiler, and standalone map/background paths
remain absent. Native type-name and XML-construction delegate caches already
exist; no redundant cache was added. The [capability table](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/retained-features.md)
separates delivered coverage, experimental limits and omissions.

## Entry and package

RimWorld constructs [WakeUpMod](../src/WakeUp/Startup/WakeUpMod.cs) before loading mod XML. It reads ordinary in-game settings unless explicit development selectors are present, then initializes the independently guarded selected features. An outer boundary isolates failures that occur before a feature enters its own setup method, including failed static initialization. There is one `net472` runtime project and one packaged runtime DLL. The local About file declares Prepatcher as its dependency, supplying the reviewed Harmony environment; no Gagarin assembly is linked or bundled.

`--wake-up-mode=candidate` selects optimization and requires an absolute local save-data path. `--wake-up-mode=baseline` selects measurement of the original path. Within explicit development mode, missing, unknown or conflicting selectors refuse activation, and reference/bypass arguments take precedence. Any explicit `--wake-up-` argument prevents fallback to normal settings. Ordinary launches resolve the saved settings into the same guarded feature selections using RimWorld's actual save-data directory; they need no launch arguments. Definition/template and Harmony type searches plus supported optional supplier integrations default on. Image caching/preparation, translations, routing, background loading, display/diagnostics, summary hiding, XML timings and both experimental options default off. Settings changes apply only on restart, with independent search opt-outs leaving optional features available. The fixture can exercise this ordinary path through `--activation user`. `startup-searches` enables the two search components together; `def-lookup` and `type-lookup` support focused comparisons. See [development](development.md).

Release 0.3.0 has bounded Windows Steam and GOG qualification; Linux/Deck is not qualified for this release. The private fixture must be recreated before future operations. The 0.2.1 [forward compatibility change](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/forward-compatibility.md) removes whole-game version, MVID and file-hash admission restrictions: [GameBuildContract](../src/WakeUp/Compatibility/GameBuildContract.cs) selects a platform path for future builds. Expected method behavior and relevant Harmony patch state still constrain individual features; Harmony remains exact and optional suppliers use required-function checks. A Harmony patch is another component's interception or rewriting of a method. These checks do not establish universal future compatibility.

## XML definition and named-template searches

Many patch queries first ask for one definition by its literal name, such as `Defs/ThingDef[defName='Example']/comps`. [ScopedDefLookup](../src/WakeUp/XmlSearch/ScopedDefLookup.cs) builds a temporary dictionary for that definition type, finds the unique matching element, then asks the ordinary XML query engine to evaluate the remaining supported query relative to it. This avoids repeatedly scanning unrelated definitions while preserving normal query evaluation and node objects.

Other queries find a named inheritance template, such as `Defs/ThingDef[@Name='ApparelBase']/statBases`. The `@` means an XML attribute: this template key differs from the `defName` child element used for a definition. The lookup keeps the two key kinds separate, even if their names are identical. It indexes the current document on first use during each patching scope, rather than storing results across launches or shipping a prebuilt index for a particular modlist. Query descriptions retain their index key so repeated queries need not allocate that key again.

[DefLookupRuntime](../src/WakeUp/XmlSearch/DefLookupRuntime.cs) substitutes only a unique query callsite in the supported vanilla workers: Add, AddModExtension, Insert, Remove, Replace, SetName, AttributeAdd, AttributeRemove, AttributeSet, Test and Conditional. It does not register a CombatExtended or other individual-mod worker. Existing foreign rewrites or changed patch state retain native queries.

The lookup is scoped to one `LoadedModManager.ApplyPatches` execution and the current thread. XML mutation events invalidate affected indexes, and queries during pending mutations use the original path. Definition insertion/removal invalidates both key kinds for that type. Template attribute insertion/removal, moving an attribute and edits to its value invalidate the template index through the owning element, including the former owner during removal. An unrelated attribute or suffix edit need not rebuild the template index; normal suffix evaluation still observes the current XML.

Duplicate or missing names, unsupported syntax, unfamiliar XML nodes and exceeded limits use the original path. A missing `Name` attribute is distinct from an attribute with an empty value; namespaced attributes are not mistaken for unqualified `Name`. Both key kinds share the 65,536-entry limit and the cache of at most 4,096 query descriptions. Disposal removes event handlers and releases references, including on patch failure. This is an index over the current document, not a persistent processed-XML cache or a replacement for patch order.

Supported workers retain their existing result-consumption behavior. Multi-result suffixes keep the original document iterator; only the established zero/single-result list path is accelerated. Arbitrary `XmlNode.SelectNodes` calls are not intercepted globally. The final `def-lookup.jsonl` receipt counts all indexed queries in `hits` and the template subset in `attributeHits`, alongside fallbacks, builds and invalidations. With a supplier cache hit that skips patching, no template index is needed or built; the Gagarin reuse feature remains independent.

## Harmony type searches

Harmony's `AccessTools.TypeByName` can fall back to enumerating many loaded types. [TypeLookupRuntime](../src/WakeUp/TypeSearch/TypeLookupRuntime.cs) reuses an index for that fallback while preserving the original search ordering and ordinary first path. Loaded-assembly changes invalidate the reusable snapshot; unsupported or dynamic conditions can remain on native enumeration. Conflicting changes to Harmony's enumeration helpers refuse the optimization.

The component does not globally batch patches, reorder mod constructors or cache arbitrary mod return values. Its receipts distinguish installation, actual indexed work, native fallbacks and final counters. Cleanup at startup completion releases retained lookup state. Faster summed per-call timing is supporting evidence; complete loading still has to be measured separately.

### Leaf-subclass searches

A leaf subclass has no further descendants. [LeafSubclassRuntime](../src/WakeUp/TypeSearch/LeafSubclassRuntime.cs) preserves the game's eager capture of its subclass list and the iterator that filters it as callers enumerate. [LeafSubclassIndex](../src/WakeUp/TypeSearch/LeafSubclassIndex.cs) walks the native type list's inheritance chains once per observed list version. For a proven leaf with no existing child-cache entry, the adapter publishes the same empty mutable list native code would have produced, then runs the original predicate. This skips terminal-child full-list scans; nonleaf misses retain native parallel-query population and ordering. Existing child lists remain authoritative, including caller edits and native stale-cache behavior.

Native type-list identity and its standard enumerator's modification check invalidate the proof. S02 reads published caches without initiating assembly enumeration; native partial-load and exception behavior remain. Dynamic/custom metadata, bounds, changed method bodies or foreign patches retain native searches. Six method-body contracts and published patch guards constrain the adapter, including the `AlertsReadout` constructor. It shares code-search selection but opens a bounded scope only during that constructor, which the play UI reaches on ordinary colony entry after the menu. The constructor acquires exclusive thread ownership independently of Wake-Up's installation thread. Return or exception clears the proof and releases ownership; nested constructors suspend reuse and other searches/threads remain native. Harmony name lookup keeps its existing menu cutoff. The [S02 evidence and limits](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/s02-code-searches.md) distinguish offline native-constructor checks from unmeasured loading benefit.

## Translation application

Translations assign text to definition fields. [TranslationRuntime](../src/WakeUp/Translation/TranslationRuntime.cs) reduces repeated duplicate-target searches while retaining the native setter. A fresh package scope lazily indexes successful normalized paths and original dictionary keys, then gives the native duplicate loop an empty or single-entry dictionary. The native loop still decides duplicate status and writes its warning. Only the caller's completed success records are added. Failed records remain retryable; native paths and current record state are reconciled at each package call. No field accessor or resolved list target is cached.

Both before/after implied-definition language passes call the guarded package method, including the late deferred callback. Method fingerprints, published patch checks, bounds, dictionary changes and nested/concurrent-call fallback constrain reuse; finalizers release state after return or exceptions. Native case/alias handling, list assignment, restrictions, replacement capture, value-type writeback and definition-cache clearing remain intact.

The independent feature entry consumes `--wake-up-translations=on` in candidate mode and the existing binary admission. The persisted, default-off **Faster translation application** setting now emits that selection on the next ordinary launch. Master-disable and explicit development-control precedence remain unchanged; older settings files and unchanged fixture presets leave it off. The [S03 handoff](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/s03-translations.md) records actual native-method offline comparisons, the user-control correction, modern-host limitations and the limits of its original evidence. Additional lookup caching was not justified; parsed-language persistence remains absent.

## Optional Gagarin reuse

Gagarin caches processed XML, but creating definition objects still happens afterward. Its supported cache-loading path already parses an XML document and ordinarily serializes/reparses some of that work while constructing source assets. [GagarinCacheRuntime](../src/WakeUp/Gagarin/GagarinCacheRuntime.cs) reuses that parsed information within the active call.

Admission checks the active supplier, required function signatures and loaded method behavior, constructor identity and relevant hooks. It does not assume any mod named Gagarin has the supported implementation. Absence or unsupported conditions leave ordinary loading available; Gagarin and Missile Girl are not mandatory dependencies of the product.

The implementation keeps the supplier's load body, asset constructor and callbacks, exact supplied input, ordered import, source-asset mapping and document ownership. It does not hand a document to a different owner or skip definition conversion. The fixture selector is `--gagarin-cache off|timing|on|verify`, default `off`. Timing is an original-path measurement; verification adds comparison checks and should not be silently mixed into a performance arm. [The qualified historical record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/gagarin-cache-loading.md) explains the behavior and evidence.

## Optional processed textures and shared grouped storage

C05 makes automatic caching and in-game preparation use the same completed texture
and source identity. PNG/JPEG retain the game's producer, including its actual
compressed or uncompressed output. Authored DDS uses the game's parser, channel
swizzle and upload descriptor; storing it does not claim to avoid image decoding
that native DDS never performs. Optional PSD is **new decoding**, described below.
The existing `pngCache` setting remains off by default; its UI now says processed
textures. `preparedTextures` independently consumes completed entries without
creating automatic entries. Ordinary automatic caching loads authored DDS through
the native loader directly unless compressed storage is selected. Explicit prepared
DDS is still consumed when both preparation and automatic caching are enabled.
The prepared resolver checks whether that provider/path has any entry before
reading the source; presence is only admission, never a freshness check or hit.
Unsupported automatic DDS capture falls back before source hashing or cache work.
Historical `--png` selectors and PNG log filenames remain for tooling compatibility.

The descriptor retains dimensions, native texture and graphics formats, all native
mips (including intentionally reduced chains), wraps, filtering, anisotropy, bias,
color interpretation and CPU readability. Capture runs before the native CPU copy
is discarded, then restores native unreadability. DDS capture scopes its Apply
interception and maps the same read-locked file snapshot used for identity. Exact
GOG method identities and relevant hooks guard this additional branch; a refused
DDS contract leaves native loading available. Native Alpha8 and ARGB32 format
values omitted from Unity's public enum are admitted only with their observed
texture-format pairs and rechecked during actual reconstruction.

Each source identity includes its provider, effective load folders, logical and
physical path, content hash, runtime/device/settings and representation contract.
A change to one source does not invalidate unrelated entries. Every provider keeps
its own holder and native selection/precedence; a global winning override is not
substituted into lower providers. Unsupported providers or hooks retain native
behavior. Automatic and prepared output do not duplicate entries or need a
promotion between cache categories.

`WakeUp/PreparedTextures/v1/groups` holds an atomically replaced checked index and
groups of at most 16 MiB with immutable committed ranges. New blocks append after
the committed prefix; an interrupted suffix is truncated on failure or reopen.
No prior blocks are copied for each publication. Explicit preparation commits one
complete entry at a time, using a checked atomic delta record with at most 128
records between full checkpoints. Every checkpoint has a fresh authenticated
generation, preventing an old delta from reviving a removed entry. Version 2
indexes remain readable; new checkpoints write version 3.

Automatic startup construction instead stages at most 128 complete entries and
64 MiB of newly written block data before a checkpoint; an oversized single entry
commits separately. Staged entries are readable in-process and charged to the
shared allowance. They become durable only after all touched data files flush and
the full index is atomically replaced. Normal startup completion flushes the final
batch. Interruption can discard the bounded unfinished batch, while reopening
retains the prior committed entries and removes uncommitted suffixes/orphans.
A failed checkpoint rolls back that batch and refuses new batch writes. Explicit
manual preparation keeps immediate publication so cancellation preserves its
already completed entries.

Each texture occupies independently checked blocks of at most 1 MiB. Reading one texture reads only its blocks; corruption
cannot return a partial texture. Optional `compressTextureStorage` uses managed
Deflate only when a block becomes smaller, and restores identical bytes. It does
not change quality, require a helper, or change the representation identity.

An entry is bounded below 96 MiB including descriptor/identity. Encoded source
snapshots are at most 96 MiB; PNG/JPEG and PSD base decoded RGBA space is at most
64 MiB, with dimensions at most 8192. The native mip layout is separately bounded.
These are per-item bounds, not a measured process-memory ceiling: a source,
encoded entry, pixel copy, GPU texture and native staging can coexist. One item is
processed at a time. C06/C08 retain broader quality/preparation scheduling work.

The existing shared allowance (default 1 GiB) accounts for all categories, live
and temporary groups, uncommitted suffixes and index metadata. A new generation
replaces a source's obsolete key only after complete publication. Normal completion
flushes usage and prunes at most 128 entries unused for 30 days; quota pressure can
prune a bounded batch of unvisited prior-session entries, preserving current work.
Unreferenced groups are reclaimed; obsolete blocks in a still-live group remain
charged until that group is reclaimed. Unknown files are preserved and charged.
Old individual PNG and prepared-native entries are retired conservatively when
this store opens; explicit exports remain. Bypass does not mutate stored files.

The shipped DLL also contains a bounded StbImageSharp-derived PSD reader and its
license. `psdSupport` is explicit opt-in and independent of caching. It decodes the
saved RGB composite under the [pinned format contract](../third-party/StbImageSharp-PSD.md),
uploads RGBA8 as sRGB, generates a full mip chain and keeps the final texture
unreadable. Its distinct identity cannot be mistaken for preserved native PSD
output. There is no separate library/framework installation, runtime download or
automatic helper. Unsupported PSD sources retain the ordinary loader.

The [C05 handoff](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/ecosystem-replacement/c05-texture-storage.md) separates source,
package, functional output and measured performance. `--png-source original`
remains qualification-only selection of existing PNG siblings; results from it
cannot describe normal DDS selection. Existing DDS exports are a separate,
explicit quality-changing workflow and are not substituted into ordinary loading.

## Character Editor preset lookup reuse

Character Editor prepares its item/turret editor presets during startup. Its inspected implementation repeatedly rebuilds a mapping between turret definitions and their guns. [CharacterPresetRuntime](../src/WakeUp/CharacterEditor/CharacterPresetRuntime.cs) redirects only the internal read-only use of that mapping to a [temporary scope](../src/WakeUp/CharacterEditor/CharacterPresetLookupScope.cs). The public property still returns a fresh dictionary, every preset constructor still runs, and the scope releases its reference when the original method finishes or throws.

The feature is an optional soft dependency. Normal settings enable it by default; fixture comparisons use candidate `startup-searches` plus `--character-presets on`. The game argument is `--wake-up-character-presets=on`. Supplier types are resolved after confirming the active package and uniquely matching required function signatures; loaded bodies must match the reviewed method fingerprints. Loaded method bodies and relevant generic instances are checked separately, and existing or newly published foreign Harmony patches disable reuse. Supplier absence or an unsupported update affects only this feature. There is no linked or bundled Character Editor DLL and no automatic Workshop version pin.

Reuse is limited to the first object/turret construction calls before the loaded menu is drawn. It retains at most 4,096 mapping entries, checks definition-list identity and modification count, and excludes nested/cross-thread sharing. Later calls use the original lookup. Canonical preset-value digests compare complete stored UI preset values in the original and optimized comparison arms. See the [reintegration report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/character-editor-reintegration.md) for identities and live qualification.

## Independent loading display

Wake-Up can add a compact panel to the game's own loading screen. [LoadingDisplayRuntime](../src/WakeUp/Startup/LoadingDisplayRuntime.cs) observes stage text and queue transitions while preserving native drawing, callbacks and static constructors. [LoadingDisplayState](../src/WakeUp/Startup/LoadingDisplayState.cs) formats stage, elapsed-since-observation and existing PNG/JPEG cache counters on a 250 ms budget. Native repaints draw cached content; no item adds a wait or repaint request. Display diagnostics can show existing feature status; per-mod attribution requires the separate timing setting.

Both persisted settings default off and take effect next launch. Optional diagnostics retain at most 64 stage transitions; they are not a performance profile. Required native method fingerprints and early/late foreign drawing guards constrain activation. Loading Progress owns the screen when present; only the independent display declines, preserving supplier texture/repaint paths. Queue end or an observed escaping exception releases display state without altering native error handling. Internally swallowed errors are not individually observed, and synchronous loading can hold the last visible frame. Current previews already load on first use, so no preview or global cleanup patch was added. The [S08 report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/s08-loading-display.md) records the original implementation. F1 executed combined layout code and bounded supplier refusal. Visual layout, broad compatibility and overhead remain unqualified.

## Loading Progress repaint coalescing

Loading Progress can stop loading early to repaint the screen after a stage change. [RepaintCoalescer](../src/WakeUp/Startup/RepaintCoalescer.cs) lets loading continue until its existing elapsed-time limit instead. This combines several display updates within the existing budget (repaint coalescing), preserving the framework's iterator and the order of loading work. It ends when the loaded startup menu is drawn; the independent observer still measures completion of that menu repaint.

Ordinary settings enable this optional integration when supported; explicit development mode selects it with candidate mode and `--wake-up-loading-progress=coalesce`. [LoadingProgressCompatibility](../src/WakeUp/Compatibility/LoadingProgressCompatibility.cs) discovers the active optional assembly by package and assembly name. The coalescer checks the inspected `ShouldStopEarly` and `Transpiler` method bodies. Published Harmony guards allow only our own hooks and the exact known framework transpiler on the loading loop; newly published foreign patches make the original stop result win. The transpiler itself has a separate guard. Missing frameworks return before method checks; incompatible functions retain ordinary behavior. PNG integration validates a separate function set.

The method reads the original stop result after Loading Progress has consumed its repaint request. Only an active, main-thread, compatible true result is suppressed. It does not remove the original elapsed-time check or change gameplay loading behavior after the startup menu. Progress can update less frequently during startup. This feature does not include the rejected Gagarin XML compatibility bridge. See the [five-opportunity report](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/five-loading-opportunities.md) for matched results and limits.

## Giddy-Up riding-offset preparation

Giddy-Up calculates where riders sit by reading the middle column of a creature texture. [GiddyTextureRuntime](../src/WakeUp/GiddyUp/GiddyTextureRuntime.cs) preserves the original full GPU conversion but reads back only that column. The installed supplier retains its offset calculation and setup order. Normal settings select this optional improvement; explicit fixture comparisons use `--giddy-textures on`.

The required supplier function signatures and method bodies, relevant patch state, startup window, thread and supported Windows Direct3D 11 or Linux OpenGL backend must all match. Other conditions retain the supplier's original texture reader. Temporary textures are released at the loaded menu. Verification mode compares the original column alpha values and completed offset hashes; the [qualification record](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/further-loading-improvements.md) separates those checks from timing runs.

## Checks and evidence

S14 keeps compatibility handling at existing feature boundaries. Static public
metadata names only the two legacy copies of this product. The independent
display's existing active-Loading-Progress refusal now gives both user choices
in its retained settings/log status. No additional unconditional conflict or
conditional launch dialog was justified by the accepted capabilities and pinned
supplier hooks, so there is no new queue, constructor callback or popup lifecycle.
See the [catalog and per-surface evidence](https://github.com/kt-nyx/wake-up/blob/v0.3.0/docs/archive/ecosystem-replacement-20260910/s14-conflict-warnings.md).

[Focused managed tests](../tests/WakeUp.Tests) cover search semantics, mutation and fallback behavior, optional reuse and compatibility guards. [Fixture checks](../tests/test_fixture.py) and [Gagarin fixture checks](../tests/test_gagarin_fixture.py) cover preparation, identities, selectors and cache comparisons. They support safe iteration but do not execute the Unity game or establish gameplay compatibility.

Runtime JSONL receipts are written under the selected game save-data folder's `WakeUp` directory. These narrow records establish whether a selected optimization actually ran. The independent menu observer measures the loading endpoint even when Wake-Up is absent; it is fixture tooling, not a shipped optimization. No broad diagnostic framework or legacy Core dependency remains in the product.

The source separates reusable search data structures (`ScopedDefLookup`, `TypeLookupIndex`), supplier-specific integration, and compatibility checks. `UserStartupSelection` translates persisted settings into existing selectors. `JsonLineLog` owns JSON string escaping and best-effort writing for product receipts; feature modules retain their event schemas. The independent observer keeps its own logging and does not depend on the product DLL.
