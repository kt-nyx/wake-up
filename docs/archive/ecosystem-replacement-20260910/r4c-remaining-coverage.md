# R4c: remaining cache and loading-experience coverage

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent acceptance — 10 September

Handoff `83b6487` and built source `9601473` are accepted for offline
implementation. Parent inspection checked the actual summary rewrite and native
condition, observer completion after installation, timing bounds and unchanged
exceptions, setting/fixture allowlist wiring, focused result receipts and the
built DLL hash. A bounded independent parent review found no actionable
new-colony ownership/preference/pause/cleanup defect. Existing checks are adequate;
no repeated build/test or live run was necessary. The revised language comparison
supports stopping that candidate, not declaring all language caching impossible.
R5 combined review/package preparation is next. Actual focus, playback, startup
performance and release qualification remain outstanding under the no-live rule.

Wake-Up now supports background loading for ordinary new colonies, can hide the
native loading-screen mod summary, and offers a small per-mod XML loading report.
The new display choices default off. These are implemented and checked offline;
actual focus, screen layout, gameplay and startup benefit remain unqualified.
The revised language-cache experiment did not show a repeatable useful gain, so
it adds no product cache, selector or maintenance category.

This is partial ecosystem coverage. Independent XML and parsed-language reuse,
general deferred textures, complete-atlas caching and full startup profiling
remain missing. Prepatcher remains required; supplier integrations remain
optional. This report describes source changes, not the unchanged deployed build.

## Scope and ownership

Dispatch `wake-up-r4c-coverage-offline-c46f8d7` verified clean base
`c46f8d71f8ef558233e64e09fbb364e184523354` and created
`codex/wake-up-r4c-coverage-c46f8d7` in the single existing checkout. The parent
assigned exclusive source/build ownership. Two bounded agents covered ordinary
colony implementation and independent cache research; one separate bounded
agent reviewed the final code. No app task or R5 was started.

Current Steam `Assembly-CSharp.dll` was freshly verified as SHA-256
`5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`.
Existing read-only ILSpy refreshed actual loading, summary, language and atlas
contracts under ignored `artifacts/r4c/`. The original ecosystem research,
independent audit and historical results were starting evidence, not authority
to repeat failed experiments or claim replacement.

No deployment, fixture preparation, profile/mod/cache/save changes, game launch,
UI interaction, normal Steam/Workshop operations, Deck access, security changes,
push or publication occurred. The reset English fixture and deployed DLL remain
unchanged: `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`.
Synthetic inputs, reports and CPU probe storage stay under ignored `artifacts/`.

## Ordinary new-colony background loading

The existing default-off `backgroundLoading` option now includes the normal
new-colony entry pages. Its session temporarily allows the game to run unfocused
only while it owns loading, then restores the user's **current** background
preference. It never writes saved preferences or changes time speed to unpause.

The native path is `PageUtility.InitGameStart` → asynchronous preparation → Play
scene → `Root_Play.Start` → `Game.InitNewGame` → callbacks and fade. The new
admission acquires the existing session before the first queue call. It retains
ownership through the scene gap, native worker, native pause-on-load callback,
queued tails and synchronous fade acknowledgement. The shared completion-frame
guard blocks an extra unfocused gameplay update when the current preference is
false. The native pause callback remains responsible for its initialization tick
and paused state; Wake-Up does not replace it.

A separate seven-method contract and `ColonyStatus` refuse changed colony
entry/initialization/error methods independently. Existing save/world admission
remains available under its own checks. Nested owned loading preserves the
Play-arrival requirement and world-chain guards. Escaping exceptions, abort to
the menu and cancellation release the owned lifetime. Current preference changes
remain authoritative. The exact coordinated R3 audio-idle and R4a XML-menu hooks
remain recognized; unrelated hook admission was not widened.

Direct/autostart initial Play entry does not pass this page boundary and has a
separate initial asset-loading background override. Existing-game standalone and
pocket-map generation calls `MapGenerator`/`PocketMapUtility` directly; it has no
new Play transition. Neither family, world-map opening nor arbitrary mod-created
events is newly admitted. Ordinary page entry is not proof of all-loading support.
See [native evidence and focused checks](../../../artifacts/r4c/background-notes.md).

## Native summary choice and loading information

The game's summary is a separate expansion/mod list, not Wake-Up's small panel.
Current `LongEventsOnGUI` displays it only with a current visible long event,
an existing UI root and game, a nonstandard window, and `showExtraUIInfo`.
Ordinary initial menu startup commonly lacks a game, so hiding the summary is
not presented as a menu speed improvement.

**Hide native loading mod summary**, `hideLoadingSummary=false`, independently
selects `--wake-up-hide-loading-summary=on` next launch. A guarded rewrite adds
one boolean filter to the native summary condition. Both its reserved layout and
its drawing use that same condition. Every other original instruction and call
remains: loading text and `alreadyDisplayed` acknowledgement, tips, tooltips,
callbacks and error handling. `ModSummaryWindow` itself and mod-manager metadata,
version warnings and information elsewhere remain unchanged. This deliberately
hides both expansion and mod information in that loading summary.

Active Loading Progress refuses this choice, leaving its screen in use. Unknown
early drawing hooks refuse installation; late hooks restore ordinary drawing.
Wake-Up's existing additive panel and this one exact summary rewrite recognize
each other by method, owner and hook kind. The previously reviewed Prepatcher
drawing prefix remains allowed by the panel; no broad owner-name exception was
added. A status distinguishes off, selected/no eligible screen, actual hidden
drawing calls, and refusal. Counts are drawing calls, not hidden mods or time saved.

The existing panel's optional diagnostics now also show retained streaming-XML,
texture-routing, deferred-audio and prepared-texture status/counters. Sampling
uses the existing 250 ms formatting budget. It does not inspect engine state,
enumerate assets, request repaints, sleep or yield. PNG/JPEG counters remain
separate from prepared-texture hits. These are selected/active/refused/hit facts,
not an aggregate speed claim. Mod identifiers appear only when the separate
per-mod timing option is also selected.

## Bounded per-mod XML timing report

**Record per-mod XML loading timings**, `loadingTimings=false`, independently
selects `--wake-up-loading-timings=on`. The report is available in Wake-Up settings
and saved as `WakeUp/loading-timings.txt` under the selected game data folder.
It records at most 512 calls, at most 32 simultaneous scopes, bounded package
and stage text, start offset, duration, thread, nesting depth and returned/threw
outcome. Omitted and unfinished calls are counted. Report failure leaves the
in-memory result available and never changes an original exception.

Two freshly checked native boundaries supply honest attribution:

* `DirectXmlLoader.XmlAssetsInModFolder`: folder selection/discovery, native
  parallel file reading/parsing and full worker join, attributed to the provider
  and requested folder. Native workers and returned array order stay unchanged.
* `ModContentPack.LoadPatches`: reading and constructing patch-operation objects.
  Its duration **includes** the nested XML-files call. It does not time applying
  those operations to the combined document.

Native `LoadDefs` is an iterator: timing its initial return would measure creation
of an iterator, not reading definitions. `ReloadContent` queues later work: its
return is not texture/audio loading time. Neither misleading timer was added.
Only work after Wake-Up installs observers is covered. A future native
`Root_Entry.Update` postfix completes the report after the queue drains, or marks
it partial if play data is still unloaded. A changed observed hook also ends
observation conservatively. Original return values, exceptions, callbacks and
native caught-error behavior remain. A normal return is explicitly not a loading
success receipt. The added menu postfix becomes inert after report completion;
background world/colony guards recognize only that exact in-package postfix.

Durations can nest or overlap and must not be summed as startup or per-mod wall
time. Inheritance, patch application, definition construction, static/mod
constructors, textures/audio, saves and complete startup remain unprofiled. This
fills a useful diagnostic subset; it does not replace Loading Progress or a full
Startup Impact-style report. Screen responsiveness still follows the native loop.

## Language, stores, formats and asset-route decisions

The changed language premise removes **warm source decoding**, while retaining
content hashing, validation, record reconstruction and one shared store lifetime.
Both arms parallel-read keyed/definition-type partitions before serial merging,
closer to the actual native input barriers. S06's headline comparison already
shared one store lifetime; R4c does not claim otherwise. The four historical
groups still contain 177 files/773,730 bytes with matching hashes.

The new modern-host warm median was **36.387 ms cached versus 38.182 ms ordinary**,
but the candidate lost **6 of 12 paired comparisons**. First construction cost
**151.216 / 81.849 ms**. Full sampled snapshots matched. The weak median difference
does not justify integration, especially with first-build cost and unresolved
production lifecycle. No extra repeat or cache-format search was performed.
These are sampled CPU costs, not Mono, native `LoadData`, actual switching/
fallback/application, first-read disk or startup timings. The cost-only unknown
type substitution is not a permitted product fallback. See
[complete premise and evidence](../../../artifacts/r4c/cache-notes.md) and
[probe results](../../../artifacts/r4c/language-probe/cost.json).

| Remaining mechanism | Current evidence and disposition |
|---|---|
| Persistent parsed-language reuse | Revised warm premise remains non-repeatable; **stop before integration**. Native source/provider order, keyed rollback versus partial injection failure, fallback/language switching and icon/WordInfo callbacks would still need integration. [S06](s06-language-cache.md) remains historical. |
| Language input streaming | **Deferred.** `LoadData` parallel-reads complete strings before serial native parsing. Direct stream substitution would move reads across that barrier and change provider/encoding/read-error semantics. R4a's filesystem XML constructor is a different consumer; no automatic port or false parsed-cache claim. |
| Grouped/compressed texture stores | **Deferred.** Repacking authored DDS still has no newly demonstrated avoidable stage beyond rejected warm packs/read-ahead. Eligible processed PNG/JPEG already avoids decode/finalization. Additional decompression, validation and store mapping need a workload showing useful saved I/O; no new store format without such a consumer. |
| Completed-atlas reuse | **Deferred, not disproved by layout-only results.** Actual bake includes color and mask pixels, tile placement, UV/mapping, mesh creation and publication. Mutable texture/mask provenance and exact source/order/settings/ownership must be captured before replacing it. Prior ~577 ms bake/~20 ms root layout is not a newly available nonoverlapping wall-time budget. |
| Automatic first-build/extra image formats | **Absent.** The [S09 expanded-input loss](s09-first-build-images.md) is not reopened. Native color/mips/compression/upload fidelity remains necessary; R2 Windows DDS exports are a useful separate workflow, not an automatic PNG/JPEG/DDS first-build replacement. No PSD/new automatic format consumer. |
| Broad asset routes/progressive DDS | **Absent beyond current texture-holder routing and experimental R3 filesystem streamed audio.** Resources, bundles, strings, shaders, general deferred textures and progressive DDS still need their own consumers and ownership. R3's three examined alternatives and reflection/worker limitations remain; no unsupported route is silently admitted. |

R1 whole-prefix and R4b retained-node XML replay remain excluded from the product.
Neither was reopened unchanged. See [R1](r1-independent-xml.md),
[R4a](r4a-ordered-reads-types.md), [R4b](r4b-retained-xml.md),
[R2 preparation](r2-texture-preparation.md), [R3 assets](r3-deferred-assets.md),
and [archived negative results](../results.md).

## Current capability table

Historical live evidence belongs to its captured package. R1–R4c code inherits
none of that qualification. “Offline” means source/build and bounded managed/native
contract checks; “deferred” means missing, not proved impossible.

| Original goal | Current source and offline evidence | Historical live evidence / remaining scope |
|---|---|---|
| 1: independent XML reuse | R1/R4b replay experiments rejected and test-only. R4a optional streaming input preserves native workers/order. | Established query searches/Gagarin assistance qualified on older packages; independent parsed/processed/inheritance caches **missing**. [R4a](r4a-ordered-reads-types.md), [R4b](r4b-retained-xml.md). |
| 2: translation loading | S03 faster application retained; revised parsed-language attempt stopped here. | Older French application evidence in [overnight report](overnight-qualification.md); persistent parsed-language/input change **missing**. |
| 3: faithful textures | PNG/JPEG native output reuse; R2 broad native preparation/export workflow offline. | Older rendered PNG/JPEG/native-prepared checks retained; first-build, grouped/compressed stores, new formats and complete atlases **missing**. [S07](s07-texture-coverage.md), [R2](r2-texture-preparation.md). |
| 4: cache ownership | Existing integrity, quota, interrupted-write recovery and bypass/rebuild/clear retained. Automatic 512 MiB plus prepared/export 512 MiB. | Historical maintenance evidence remains bounded; no new language/XML category. [S01](s01-cache-maintenance.md). |
| 5: on-demand assets | R1 routing guard/status fix; R3 default-off filesystem streamed-audio deferral with compiled consumer lifecycle, offline only. | Older texture routing qualified. General texture deferral, Resources/bundle routes, progressive DDS **missing**. R3 arbitrary reflection/generated and worker first-use limitations remain. |
| 6: preparation/quality | R2 native batches plus Windows full/half/quarter DDS exports; exports do not automatically substitute in-game. | Older narrow native capture/consumption qualified. Broad quality substitution/full converter parity **missing**; Linux helper owner-deferred. |
| 7: code searches | Existing Harmony type/leaf and definition/template search improvements. R4a confirmed native GenTypes/construction delegates already cache. | Historical narrow calls qualified; no duplicate native memo or broad reflection parity. [S02](s02-code-searches.md), [R4a](r4a-ordered-reads-types.md). |
| 8: loading display | R4c native-summary choice, bounded XML report/attribution and richer diagnostic status; offline only. | Older basic panel live evidence. Full profiler/progression/error inventory/constructor scheduling **missing**; previews already lazy. |
| 9: background loading | R4c ordinary new-colony lifetime added and checked offline; save/world preserved. | Older save/world minimized evidence remains. New colony needs actual focus/pause/gameplay checks; standalone/pocket maps, initial autostart and world-map opening **missing**. |
| S14: warnings | Existing exact duplicate-product conflicts; feature-local refusal/status. No new metadata or launch dialog justified. | No automatic disabling/reordering; required Prepatcher **not replaced**. Supplier overlaps do not establish removability. [S14](s14-conflict-warnings.md). |
| Recovered BetterLoading ideas | Current game already parallel-reads XML within a mod and caches native type results; R4a streams input, R4c adds a partial XML report and ordinary colony background lifetime. | Cross-mod scheduling, detailed save progress, constructor timing and general error/progress replacement **missing**. Existing Loading Progress repaint cooperation remains. |

## Compatibility and proportional verification

No newly verified duplicate/incompatible package combination justifies another
metadata conflict or launch warning. Summary suppression refuses active
`ilyvion.loadingprogress` and unknown drawing changes; timings refuse changed
observed method hooks. These are reduced coverage, not a request to remove a
supplier whose other capabilities remain useful. Existing public duplicate-copy
IDs `local.rimworldloadingoptimizer.op7validation` and `kt.nyx.startupfixes`,
required `zetrith.prepatcher`, and all load-order metadata remain unchanged.

The ordinary-settings fixture allowlist now includes the two R4c settings and
R4a's `streamingXml`; its synthetic wrapper/persistence check was extended.
**R4c has resolved that small R5 tooling item.** No actual fixture settings were
applied. Explicit controls still override persisted settings and master-disable
still wins ordinary activation. Missing keys retain default-off values.

Focused evidence includes 59 background managed cases and 50 Python checks,
then 56 display/selection managed cases and 50 Python checks, zero compilation
warnings/errors. A separate modern CPU host executes actual native XML worker
join/returned arrays, nested patch construction, late-hook refusal and unchanged
exception identity through the installed timing observers. Native layout
rewrite tests preserve every original instruction/call except the one inserted
boolean filter. Report tests bound history/scopes, identify failures/unfinished
work and separate nested durations; native settings round-trip checks include
old/default and selected states. The final focused check/build identities and
independent-review disposition are recorded in the closeout below.

The ordinary .NET Framework test host cannot resolve the game's
`Dictionary.TryAdd` while hashing/executing `XmlAssetsInModFolder`. The existing
modern-host original-PE scope normalizer supplies the exact fingerprint and
actual native-method check. This does not change production references or
relax runtime admission. An initial placeholder-hash run and one test-delegate
compile error were corrected before the passing runs; the isolated old hash
helper has three nullable warnings, separate from the warning-free product.

Future authorized live checks: ordinary menu-driven new colony with both
background preferences, changes during loading, focus loss before/during/at
completion, pause-on-load on/off, abort/repeated entry; summary visible/hidden
native conditions with and without the panel, tips/acknowledgements/mod info;
report completion/readability at supported resolutions and actual supplier
refusal/cooperation; combined R1–R4c Mono/Prepatcher compatibility, audio first-use
and gameplay. Any speed claim requires separate matched menu-observer controls.
No live, UI or performance claim follows from this handoff.

The independent review found one concrete first-launch completion bug: installing
a finalizer on `DoPlayLoad` from within the mod constructor could not observe the
already executing call. The fix uses the later native Entry update and preserves
queue/failure distinction. The modern native check now holds the report while a
native event exists, then executes the installed production menu postfix after
the queue clears; Unity root work is substituted, not called. The exact new
postfix is tested with background admission. See
[bounded review](../../../artifacts/r4c/independent-review.md). No second general
review or profiling infrastructure project was added.

Final guarded command used the focused filter in [development](../../development.md).
**160 managed checks passed, one optional representative cost probe skipped,
and all 50 Python checks passed**, with zero product compilation warnings/errors.
The receipt explicitly records `gameLaunched: false`.
[Final log](../../../artifacts/r4c/final-focused-tests.log),
[managed results](../../../artifacts/fixture-tests/f6ffb122081042239d3202aa5e2f4942/features.trx),
[modern native completion check](../../../artifacts/r4c/timing-native-check.log), and
[normal example report](../../../artifacts/r4c/timing-native-input/normal-native-report.txt)
record the evidence. `git diff --check` passed. All three subagents stopped.

## Source/package closeout

Source/build commit: **`9601473c09a692f0e770763048c146a45f5e5592`**.
One `py -3 scripts/fixture.py build` passed from that stable committed source,
with zero warnings/errors and approved current Steam/Harmony/Prepatcher API
references. The package remains **undeployed**.

| Layer | Exact identity |
|---|---|
| Core package | `artifacts/fixture-package/9601473c09a692f0e770763048c146a45f5e5592/` |
| `Assemblies/WakeUp.dll` | 335,872 bytes; SHA-256 `62fbb25d7cf149cba3b9812d2e83c17d3e6c7fe4513f915c5adeb16cab9d8c1f` |
| `About/About.xml` | 1,091 bytes; SHA-256 `f6e07f9178757aab64b287a1940cf672db8c6416c804f56d4df541b5d8492cee` |
| Existing deployed candidate | Source `8ca0dd763a2ad530668b59d6c3488f28eac311f5`; DLL SHA-256 `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`, unchanged |

[Build log](../../../artifacts/r4c/package-build.log) and
[source-specific receipt](../../../artifacts/fixture-package/9601473c09a692f0e770763048c146a45f5e5592.json)
record the package/reference checks. This subsequent documentation-only closeout
does not rebuild or change its bytes. All subagents, probes and build commands
have stopped; the remaining dotnet process is a retained MSBuild worker, not an
active task build. No game process or prepared launch exists.

Return clean checkout/source/build ownership to parent task
`01a08711-7899-7ff3-a1b5-4a0b105ba661` for review before R5. R4c does not accept
itself as release-qualified, start R5, deploy or authorize live work.
