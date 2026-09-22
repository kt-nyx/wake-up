# C03: ordered concurrent XML input

**Original steward functional review passed; current qualification awaits review. Not fully accepted.** C03 implements matrix 1d. The dated 11 September qualification below supersedes the original no-window statements: functional checks pass, but useful performance was not established within the explicit overnight grant.

## What changes for the user

The new ordinary **Prepare XML input concurrently across mods** option reads upcoming definition files while earlier selected files are being prepared or consumed. It defaults off and works independently of the parsed, processed and inheritance caches. Actual asset constructors and definition loading remain in native mod/file order. The purpose is to remove the per-mod wait between independent source reads; whether this improves complete startup remains to be measured.

`orderedInput` respects master disable and explicit development controls (`--wake-up-ordered-input=on`). One-launch bypass and clear use native input; rebuild permits fresh concurrent input. It creates no persistent category, helper dependency or separate user preparation step. Unsupported source intervals load normally.

## Actual source and lifecycle ownership

The exact GOG `LoadModXML` loops over mods and enumerates each native `LoadDefs` iterator. Each iterator selects its definition files, constructs assets and yields them. Its intervening native work consists of profiler calls and bookkeeping; arbitrary mod constructors have already executed. Native file construction already uses two workers plus its caller, but joins them separately for each mod.

The coordinated Prepatcher rewrite factors the **actual native selection instructions** into `DirectXmlLoader.WakeUpSelectXmlFiles`. Native effective-folder order, `DirectoryInfo.GetFiles` enumeration, hidden-name filtering, first dictionary winner and dictionary-value order remain intact. It is not an alphabetical crawl or package allowlist. Original native worker code and its empty-array return remain available. C03 captures the selector's real lists once on the loading thread and returns those same lists when the native consumers reach them.

Planning begins at the first admitted native source selection, after native `LoadModXML` prefixes. It stops before an already-populated mod, failed selection or descriptor bound. The active source interval is checked against exact native method bodies and published Harmony hooks. The selected GOG filesystem path has no virtual file-provider call; altered source loaders, debug-folder selections and unknown providers keep their ordinary route.

Two background workers perform only private filesystem reads and content hashing. A sliding queue admits at most 24 jobs and reserves at most 32 MiB of input buffers before allocation. Individual files are at most 16 MiB; descriptors are limited to 32,768 files and 512 mods. Queued, running, completed and transferred slots remain accounted until disposal. No new pool surrounds the native worker pool: an admitted batch consumes private results through serial native constructors, and fallback first cancels and joins the private workers.

Each snapshot owns an open `FileStream` with `FileShare.Read` from capture through transfer. This read lease prevents mutation/deletion while those bytes are pending on Windows, avoiding a second freshness read/hash. It is a bounded change to file-lock lifetime; the native memory-map implementation's exact sharing policy is not visible in available managed IL, so identical sharing flags are not claimed. The contract is a private selected-input snapshot inside a callback-free interval, not a guarantee against an external process changing the whole loadout concurrently with selection.

With C01 disabled, the existing constructor bridge parses the immutable captured bytes once. With C01 enabled, those bytes and their existing digest feed the selected-generation key and cold parser/warm cache consumer. The key format is unchanged. C01 may additionally retain its current byte group while the queue refills: that group targets 4 MiB and permits one file up to 16 MiB, so the input-buffer bound is queue plus current group, at most 48 MiB, plus bounded stream/digest/descriptor overhead. Existing native/C01 document allocations remain separate.

Native asset attribution assignments still execute. Source documents and combined documents retain C01's separate mutable ownership; C02's processing, inheritance and downstream observer boundaries are unchanged. Results are never published by workers. Consumption order is independent of completion order, and a consumed lease releases its reservation so subsequent jobs can enter.

## Callbacks, errors and cancellation

Unknown source/constructor/filesystem/logging hooks refuse the interval. Runtime guards cover concrete filesystem property overrides, not only abstract base declarations. Native profiler verbosity also refuses it. The exact Prepatcher profiler callback is admitted because none of the three native labels inside this interval triggers its stage-changing branches. Its exact error/warning filters, the optional ModSettingsFramework flag-only error prefix, and C02's private diagnostic observers remain at native positions; changed callback bodies or hooks refuse reuse. The input hash uses a fixed managed SHA-256 fallback, so CryptoConfig cannot introduce a custom provider constructor on a worker.

Selection/read/parse failures remain private until their native position. A failed selected source cancels and joins ahead work, disposes leases, and lets native loading own the diagnostic. C01 abandons an unsuccessful private preparation batch before its first constructor, so a later warning cannot leave already-prepared following files stale. After the first constructor executes, exceptions propagate after cleanup; no whole-batch catch retries constructors. Stage completion and hot reload release pending work; hot reload remains native.

This differs from the archived extra-worker experiment: work overlaps across selected mod boundaries and retained results feed actual consumers. It differs from discarded read-ahead: successful snapshots supply the parser/cache key rather than merely warming filesystem pages. It extends C01's existing ownership instead of replaying R1/R4b XML mutation experiments.

## Functional evidence and exact identities

Qualification records, initial/restored endpoint identities, build receipts and captured run references are kept under ignored `artifacts/ecosystem-next-20260910/c03/`. Final identities and results are recorded at handoff below. Early captures that selected native fallback do not prove concurrent delivery, and no operation count is treated as a speedup.

Focused tests cover exact selector instruction/branch preservation; out-of-order completion with ordered transfer; digest/byte identity; same-size/same-timestamp source change after callback fallback; malformed private XML releasing current and future leases; file growth/read failures; job and byte bounds; cancellation, joins and transferred ownership; one-launch maintenance and independent ordinary settings; and unchanged C01 key identity.

## Deferred measurements

Once the owner explicitly grants an unattended window, compare matched supplier-absent native/C03 off-on startup, including C01/C02 off, first construction and warm reuse. Charge all selection, source verification, read/hash, queue start/join, materialization and cache lifecycle work. Measure whole startup and a colony endpoint separately; retain matched forward/reverse controls and identical content/settings/diagnostics. Functional durations are only diagnostic. Neither older C02 package `9e03e0d` nor its query correction `ba17b03` supplies performance or unrun compatibility claims for this new package.

Windows Steam remains a later combined-candidate promotion requiring explicit approval; Linux/Deck comes later under separate authorization. No merge, push, publication or normal installation change is part of C03.

## Delivered package, captured behavior and returned ownership

Dispatch `wake-up-next-c03-2d19378-20260910` started clean at
`2d193787f2e46d69f2f21921bbc63c0705282ef0` on `codex/ecosystem-next-plan-review`.
The final qualified **core source/package is `8d0901989ec83a9d32beb1f7d000f21057caa477`**,
DLL SHA-256 **`8eccf46f07d265f323fd6dbce3a91d75e5bba6efed40c6ce75b63c3442d6e505`**.
Its package remains in `artifacts/fixture-package/8d0901989ec83a9d32beb1f7d000f21057caa477/`.
No observer rebuild was needed: deployed observer source
`3d86b1636976678407583e9ba5b7e55c9ddd0f07`, DLL
`b1fa3c6541529ef92763af2599c59defbf8ffeb484b267d9c0c6c273b3ed0165`,
was used for every C03 capture.

All seven following captures use that exact final core, ordinary user activation,
functional purpose, minimized nonactivating startup, the independent observer,
and normal automatic exit. Each has **exit code 0 and `automaticTestPassed=true`**.

| Capture | Actual exercised behavior |
|---|---|
| `c03-native-08` | All four XML features and C03 off; native source path. |
| `c03-ordered-08` | C03 only: 2,024 snapshots actually consumed; 10 recorded consumed cross-mod job pairs. |
| `c03-all-cold-08` | C03 plus parsed/processed/inheritance/query options; 2,024 snapshots consumed, 10 cross-mod pairs; C01/C02 generations built. |
| `c03-all-warm-08` | Same options and restored owned caches; 2,024 snapshots consumed, 10 cross-mod pairs; all 2,024 text parses and 18,628 native root imports avoided by C01; 2,998 processed calls skipped with 39 live wrappers and 43 required mod-lookup replays; 8,205 inherited children reused. |
| `c03-alpha-native-08` | Unrelated Alpha Animals workload, all XML options off. |
| `c03-alpha-ordered-08` | Alpha Animals with C03 only: 1,892 snapshots consumed and nine consumed cross-mod pairs. |
| `c03-source-fallback-08` | All options selected with a real unknown `LoadDefs` observer; C03 and C01 use native input. Retained source/combined attribution and final deliberately changed WoodLog label checks pass. |

The mixed selection contains official content, VEF, Vanilla Animals, Vanilla
Plants and Combat Extended. The second selection replaces CE with Alpha Animals.
These are actual representative mixed frozen selections, not the entire enabled
seed. Required Prepatcher/Harmony remain. Missile Girl/Gagarin and Loading Progress
were physically excluded; no input-loading supplier is active in either selection.
Neither workload is a product package allowlist.

Across the four mixed controls/variants, observed records match exactly:
18,618 ordered definition consumers, 114,471 inheritance calls, 3,011 operation
states, 2,021 source documents and 5,620 sampled ThingDefs. Across the two Alpha
controls, 15,162 consumers, 95,096 inheritance calls, 121 operation states, 1,890
sources and 4,077 sampled ThingDefs match. These cover actual downstream XML,
source ownership and selected final fields, not every possible field or gameplay
interaction. The foreign source observer runs once at native positions and
preserves the actual retained asset/nodes; its final label is `wood [C01 observer]`.

Every active final queue reached two overlapping jobs, at most 24 outstanding
slots, and a peak reserved source-byte count of 878,004 on these selections.
Every completion reports zero remaining jobs/bytes and `workersAlive=false`.
Cross-mod pairs explicitly name both supplying mods and include only indices
whose snapshots were consumed. These are functional relationships and bounded
reservation counters, not measured RAM use or speed results.

Qualification found that Prepatcher's profiler hook and VEF's settings-framework
logging prefix needed exact effect contracts. Earlier `c03-ordered-02` through
`-06` completed natively on their distinct packages; they do not prove C03
activation. `c03-ordered-07` first consumed all 2,024 files on predecessor
`0ae1ba67aaf933243586812554d3804563bc4e51` and matched the old C02 native control.
The final seven-run set above independently qualifies `8d09019`; it does not
inherit an unrun claim from `0ae1ba6`, old C02 seven-run `9e03e0d`, corrected
query-only `ba17b03`, or historical packages. Raw build/launch logs preserve those
intermediate identities. Initial compile errors were corrected before live work.

The final broader managed selection passed **163 tests**, with one optional cost
probe **skipped**, in `final-focused-tests.log`. The final four runtime regressions,
including the corrected same-size/same-timestamp mutation and a fixed prior-format
key fixture, passed again in `final-regressions.log`. No optional cost probe ran.
Python fixture checks passed all 61 in earlier C03 invocations. The final two
invocations each passed 60 and encountered the previously recorded `WinError 5`
renaming a temporary synthetic Steam-generation directory in unchanged transition
code. This remains an explicit tooling limitation; no security change or new
transition framework was attempted. All real C03 operations stayed GOG and passed.

Raw `functional-summary.json`, `mixed-comparison.json`, `alpha-comparison.json`,
`final-identities.json`, `final-stopped-state.json`, package receipts and the seven
captured profiles bind these claims. Managed tests provide error/cancellation/
source-change/bound checks; those failure cases were not all injected into separate
game launches. No colony/save, visual UI or performance qualification is claimed.

**Exact endpoint restored:** GOG generation
`gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`,
game assembly `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
The explicit assignment requested the original deployed endpoint back, so core
`ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8` (DLL
`e507575091f92f2b0d1882e828943666d3557cc7c8b0fc4487f2068b590b6f68`)
was restored through transaction `20260910-230031-ca72f378`. The qualified C03
package remains retained but is **not currently deployed**. The original observer
is unchanged. Profile restoration transaction `20260910-230032-e4ed43c5` restored
the seed profile/order; Wake-Up/observer are disabled, settings and prepared pointer
absent. Both temporary suppliers were restored; original exclusions
`astryl.moderndevtools`, `neachi.charactereditor.facialanimation.eyepatch` and
`void.charactereditor` remain. All seven initial file identities match exactly;
metadata audit reports `deployedRuntimeMatches=true`. No RimWorld process remains.

Both bounded subagents and every build/fixture operation have stopped. Exclusive
checkout and operating ownership returns to steward
`01a08d63-a8ef-7c40-8cd9-bbf9f512ce22` for independent functional review. The same
C03 task remains available for corrections and later measurements. Steward state
and timers were not edited. Preserved ancestry through `fe687d4` and main
`5299ff4974367e5492181f2f72ccd20f91e51629` remain unchanged/unmerged. No C04 task,
new checkout/worktree, normal-game modification, Deck access, push or publication
occurred. This is a handoff for review, not self-acceptance.

## Steward functional review — 10 September 2026

Reviewed clean handoff `208a00c2518979513f057e5113c02699c6c5cbff`, qualified core `8d0901989ec83a9d32beb1f7d000f21057caa477`, and the seven final-package captures listed above. Independent queue and native-boundary source reviews found no actionable blocker. Actual native selectors/callbacks, constructor-once behavior, source ownership, bounded reservations and cancellation/failure cleanup agree with the reported implementation.

The steward rehashed captured run and observer files, compared the actual downstream output partitions across mixed native/C03-only/all-cold/all-warm and Alpha native/C03 controls, and verified the retained-source fallback checks. Cross-mod receipts describe consumed snapshots, with zero outstanding jobs/reservations and stopped workers. All seven current endpoint file hashes match the original assignment identities. The deployed core is restored C02 `ba17b03`; the tested C03 package is retained separately. Independent raw review: `artifacts/ecosystem-next-20260910/c03/steward-evidence-review.json`. Existing final logs confirm 163 managed passes with the optional cost probe skipped and four regression passes; no repeat test or build was needed for this review.

This passes the agreed functional coverage/correctness/compatibility/expected-behavior gate and permits C04 after ownership transfer. It does not establish a speedup, broad colony qualification or full acceptance. Keep later matched C03 off/on measurements with native and C01/C02 cold/warm controls, source verification and queue overhead, whole startup and colony/first-use effects, and combined interactions. Measurements wait for an explicit unattended window. The same C03 task owns any later correction after serial reassignment.

## Overnight qualification — 11 September 2026

C03 still supplies real source bytes correctly, but the tested implementations
did not produce a repeatable useful startup improvement. Two attempted changes
were removed after measurement. The retained queue remains default off and fully
implemented; the remaining product correction refuses additional unknown XML
callbacks. These results reject the tested approaches under this workload, not
the goal of useful ordered cross-mod preparation in matrix 1d.

The original owner resumed from exact clean
`055a85c7fba0ef9acd8c0bd0458f683b85676d7e` under
`wake-up-c03-overnight-qualification-20260911`. Current C01 source-document/native
import behavior and all C02–C05 corrections were preserved. No historical C03
checkout or older main was substituted. The owner grant was 05:57:38–12:00:00 UTC,
ending earlier on return/revocation. Measurements ran from 11:15:06 to 11:53:50 UTC.
Every launch checked the remaining window and required all RimWorld processes
already stopped. Child work, builds, tests and substantial source work were paused
during measurements. No old grant, silence or unattended assumption extended it.

### Retained implementation and attempted corrections

The first current-source core `4cf5cb7e843d4cda939eea9b545dc6c9dc3ff3c4` passed
native/C03-only/combined cold/warm functional comparison. Its matched standalone
menus were native 16.270/16.538 seconds and C03 16.707/17.132; complete source
loading was 316.1/327.1 ms versus 626.6/781.5 ms. Actual overlap and consumption
therefore did not establish a speed benefit.

`dc0e09c6acfe47050bac105e565ea7ef2a5af513` moved private document parsing onto the
existing two input workers when C01 was off. The exact resulting documents reached
native constructors once and matched downstream output. C01 mode remained
read/hash-only, avoiding redundant text parsing on warm cache hits. This did not
establish useful improvement: C03 menus were 16.739/16.950 seconds; the ordinary
reverse native control was 16.238. The first native control was an unexplained
35.035-second outlier, with source loading at 3,394.5 ms. Its full evidence is
retained, and it is not used to claim benefit. This series is noisy, not a clean
speedup comparison.

One measured diagnostic at `b114d95992af10736b8833dd2c97ec0bbd6f6afc` separated
existing consumer costs: compatibility checks used 31.0 ms, queue takes/waits
453.0 ms, and constructors 36.0 ms within a 679.4 ms source boundary. The checks
inside constructors overlap these counters; they are not additive savings.
This pointed toward input preparation and waits rather than a new guard framework.

`1ba615c2886cc016520450c21cd81d3e1e8d146e` then allowed the waiting caller to claim
a pending private queue job alongside the same two workers. This restored native
worker capacity without another pool, retained all reservations, and made disposal
wait for a claimed caller job too. Exact downstream equality, ordered document
transfer and cancellation checks passed. Standalone menus were 17.251/16.624 versus
16.055/16.690 controls, without a repeatable gain. Warm parsed-cache menus were
17.029/17.108 with C03 versus 16.949/16.923 without it. One construction pair was
17.827/17.833, insufficient to establish a construction benefit. The larger planned
prototype matrix was deliberately stopped after captures 19–22; reverse controls
48–53 answered the remaining decision. Planned but unrun labels are not evidence.

The experiments and their diagnostic counters were removed at final product
**`a66db6f8ab8b7547ebc30546be4ab170581885c6`**. Its C03 queue and consumer sources
are exactly those at `4cf5cb7`; no prepared-document backlog or caller-help path
remains. Actual cross-mod read/hash consumption, two workers, 24 jobs, 32 MiB
reservations, held leases, native constructors, error order and joins remain.
The experimental document mode bounded source bytes/document count, not actual
document-tree memory; it did not establish a memory or latency advantage.

One correction stays: the shared C01/C03 parser guards now include native factories
for CDATA, processing instructions, significant whitespace, whitespace and
comments. Unknown hooks on these functions could otherwise run while future input
files were leased. Such hooks now use native loading. Native supported content is
not excluded, and no package/mod allowlist was added. The managed test setup also
disables and restores its uninitialized native profiler, so malformed-input checks
exercise their intended path instead of failing first on absent game preferences.

### Final functional coverage and exact package

Final GOG core/package is `a66db6f8ab8b7547ebc30546be4ab170581885c6`, DLL SHA-256
`bea68e552a87d541771a03614d6fef90df1f518f8e8dbd19f2600cdba5396ca5`.
Every qualification capture uses the retained observer
`b9723180e861f1a8b2408bf8f3cee2bcdcc29bad`, DLL
`95be46a47780c61d062126948012bb71a1e72794ab19eb99711f04caf2dd50d6`.
Game generation remains `gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`, game assembly
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.

Final functional captures `c03q-f-retained-54`, `c03q-f-retained-xml-cold-55` and
`c03q-f-retained-xml-warm-56` match the current-source native control
`c03q-f-native-01` in all five partitions: 18,618 ordered definition consumers,
114,471 inheritance calls, 3,011 operation states, 2,021 source records and 5,620
sampled ThingDefs. Each active queue consumes all 2,024 inputs, records consumed
cross-mod pairs and exits with zero outstanding bytes/jobs and stopped workers.
The retained queue peaks at two active jobs, 24 outstanding slots and 878,004
reserved source bytes on this selection. These are reservation counts, not a
measurement of process memory.

Warm C01 avoids 2,024 text parses while retaining native document imports; the
historical zero-import counts above are not current behavior. C02 warm processing
still skips 2,998 calls and repeats 43 required mod lookups; inheritance restores
8,205 children, with no native children or redundant generation write. Final
`c03q-f-retained-fallback-57` installs a real unknown source observer. C01/C03 use
native input, retain the same source asset/nodes, pass combination checks and
produce the deliberately changed `wood [C01 observer]` final label.

All 52 captures exit normally with code zero and `automaticTestPassed=true`:
13 functional and 39 measured. The final package has 24 captures: four functional
and twenty measured. Historical/prototype packages retain their own evidence;
their counts do not become final-package qualification. All outer-stage reports
are complete, without installation failure, unfinished calls or overlapping scopes.

Final focused checks pass **125 managed tests and all 63 fixture tests**; the
optional cost probe is skipped. Worker-parsing checks earlier passed 128 managed
cases; caller-help checks passed 28 and 63 fixture cases. Earlier temporary
synthetic Steam-directory rename failures are retained in their logs; final checks
pass without a security change or fixture-framework modification. Only GOG was
operated. No colony, save, visual/UI or universal mod compatibility claim follows.

### Final balanced measurements

The frozen mixed selection is the same official-content/VEF/Vanilla Animals/
Vanilla Plants/Combat Extended workload. Missile Girl and Loading Progress were
physically parked; no input-cache supplier was active. This selection is not the
whole enabled seed or a product allowlist. Ordinary retained settings, language,
quality, 1,024 MiB allowance and timing instrumentation match across each pair.
Language, texture, streaming XML, deferred audio and other newer options are off.
Combined XML below enables parsed, processed, inheritance and query extensions;
it is distinct from C02's earlier combined selection and does not inherit its claim.

Every measured run uses performance purpose, minimized nonactivation, independent
menu observation and normal automatic exit/capture. Detailed functional observation
is absent from both timed arms. Each warm run independently imports its matching
captured construction seed; it never borrows the preceding live profile. Exact
seed run hashes and storage inventories are recorded by `fixture.py`. Empty
application storage does not mean a cold Windows file cache.

Full labels use prefix `c03q-p-retained-`. `retained-series.json` records the actual
forward/reverse order 58–77. Parsed seeds are off 60 / on 61; combined seeds are
off 64 / on 65, reused independently in each matching warm arm.

| Same surrounding XML settings | C03 off menu seconds, forward/reverse | C03 on menu seconds, forward/reverse | Result |
|---|---:|---:|---|
| No XML caches/extensions: 58/59/76/77 | 16.180 / 16.834 | 16.865 / 16.762 | No repeatable startup gain; source stage slower both ways |
| Parsed construction: 60/61/74/75 | 17.722 / 17.620 | 18.403 / 17.474 | Direction reverses |
| Parsed warm: 62/63/72/73 | 17.283 / 16.668 | 16.926 / 16.872 | Direction reverses |
| Combined construction: 64/65/70/71 | 19.871 / 19.618 | 19.654 / 19.753 | Direction reverses |
| Combined warm: 66/67/68/69 | 16.880 / 16.672 | 17.435 / 17.041 | Slower in both directions |

Complete standalone source loading is 331.4/465.9 ms natively and 583.4/570.2 ms
with C03. Including the following nonoverlapping combination stage gives
468.5/679.1 ms versus 712.1/789.7 ms. Combined warm source-plus-combination costs
710.8/761.7 ms without C03 and 1,224.7/960.4 ms with it. The outer source wrapper
includes selection, read/hash, queue start/join, materialization and cleanup;
whole-menu time also charges initialization and the remaining startup. Nested
counters and stage differences are not added to menu time.

These samples show ordinary variation and no qualified useful C03 improvement.
Further work must address complete verification/preparation/materialization cost
or establish a materially different representative workload premise. More workers,
unchanged read-ahead or counting consumed jobs alone do not reopen these results.
First-read conditions, additional workload generalization, colony/first-use and
broader C04/C05 combined qualification remain separate, untested obligations.
C01/C02/C04/C05 retain their own acceptance and measurement failures; none is
accepted through another component's combined total.

### Restored endpoint and returned ownership

The tested package is retained under ignored `artifacts/fixture-package/`; it is
not currently deployed. Original core `ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8`
(DLL `e507575091f92f2b0d1882e828943666d3557cc7c8b0fc4487f2068b590b6f68`) and
observer `3d86b1636976678407583e9ba5b7e55c9ddd0f07`
(DLL `b1fa3c6541529ef92763af2599c59defbf8ffeb484b267d9c0c6c273b3ed0165`) are restored.
Profile transaction `20260911-075420-5bb6551d` restores seed order/preferences;
both temporarily parked suppliers are restored and the original three exclusions
remain. All seven original endpoint hashes match. Candidate/observer are disabled;
settings, prepared pointer and helper are absent; no RimWorld process remains.

Raw evidence is under `artifacts/ecosystem-next-20260910/c03-qualification/`:
`collected.json`, `qualification-proof.json`, `timing-table.json`, functional
comparisons, exact settings/selection/series, build/test/deploy/capture logs,
`restored-identities.json` and `closeout-proof.json`. Captures remain private.
The explicitly paused prototype series and unrun planned labels are distinguished
from all 52 successful captures. No fixture data is tracked.

All child work, builds, tests and fixture operations are stopped. Exclusive
ownership returns to steward `01a08d63-a8ef-7c40-8cd9-bbf9f512ce22` for independent
review; the same C03 owner remains available for corrections. This is not
self-acceptance. Pending/failed performance alone does not block the steward's
next useful serial implementation decision. No successor was dispatched, parent
state/timer edited, normal data or other application modified, Steam/Deck promoted,
checkout/worktree created, history reset, main merged, push or publication performed.
The remaining owner window still expires at 12:00 UTC or earlier return/revocation;
this handoff creates no extension.


## Steward qualification review — 11 September 2026

**Functional re-review passed; useful performance failed to qualify and C03 is
not fully accepted.** Clean handoff `177fc580ad3529099666dbf21b504185df0b010d`
preserves assignment `055a85c7fba0ef9acd8c0bd0458f683b85676d7e`. The final source,
tests, scripts and validation equal tested `a66db6f8ab8b7547ebc30546be4ab170581885c6`.
The retained product change extends the shared C01/C03 XML-factory guards;
unknown callbacks on the five additional native factories now retain ordinary
loading. Independent read-only source review found no blocker. Both unsuccessful
worker-document and caller-help experiments were removed; queue/runtime sources
match their assigned base. The test-only profiler setup restores its prior state.

The steward independently rehashed 25 capture manifests (24 final-core captures
and the current native functional control), their observer/runtime evidence and
all seven restored endpoint files. Three complete final functional observation
files are byte-identical to native. Actual consumed cross-mod receipts report
2,024 inputs, zero remaining jobs/bytes and stopped workers. Menu observations,
source/package identities and automatic normal exits match the report. No repeat
build or test was needed. No RimWorld process remains. Raw independent proof is
`artifacts/ecosystem-next-20260910/c03-qualification/steward-qualification-review.json`.

Standalone source loading remains slower with C03 in both directions; menu
variation and parsed-cache comparisons do not establish a useful gain. Combined
warm loading is slower in both directions. This rejects the tested performance
approaches, not the cross-mod capability. Future work must address the measured
complete cost with a materially different design or representative workload
premise. The original owner retains that obligation after serial reassignment.
No capability/default changes, supplier-removal claim or full acceptance follows.

The overnight measurement window expired at **12:00 UTC on 11 September**.
All C03 measurements ended before it. Further performance testing needs another
explicit unattended window. The steward continues with C06 implementation and
GOG correctness under the owner's approved sequencing, preserving the separate
C01–C05 performance and later combined/release obligations. Automatic external
helper execution remains a material policy decision if C06 proposes that route;
approved manual exports and the bundled PSD decoder do not grant it implicitly.
