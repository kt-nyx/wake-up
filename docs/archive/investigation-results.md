> Historical evidence. Package identities, activation defaults and next steps below describe their dated investigations. Use [current state](../current-state.md) and [results and decisions](../results.md) for the current product.

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.

# Results and decisions

The retained search mechanisms and optional Gagarin reuse have useful historical evidence. The reduced searches also have an XML-expanded live comparison, and a named-template lookup experiment showed a further useful gain against nearby reduced controls. That feature is now integrated into local `main` and its final package is qualified below. A second investigation rejected a slower cache-writing prototype; a third measured additional shared costs without finding a worthwhile replacement. No additional feature was promoted. Historical combined percentages included CharacterEditor and must not be presented as the current product's measured gain. The owner subsequently authorized its [optional reintegration](../character-editor-reintegration.md), with separate current measurements.

Use this record to avoid repeating settled failures. The [development policy](../development.md#development-policy) explains when new evidence justifies revisiting a route and keeps future techniques open beyond the stated restrictions. Historical experiments and suggested next steps do not impose a fixed development roadmap.

## Reading the measurements

A stage improvement is the reduction within one measured part of loading. A whole-startup improvement is the reduction to the first completed main-menu repaint. The share of total time spent in a stage is a third quantity. They are not interchangeable. Loading Progress and cleanup clocks in older records stop at different points from the menu observer, and overlapping or accumulated worker timers cannot be added.

These runs use particular frozen RimWorld inputs on one machine. Application-cache conditions were controlled where recorded, but Windows file caches and resource contention were not universally controlled. Early menu observations were bounds from polling, not the later precise observer timestamp. Successful startup, equivalent changed behavior, gameplay compatibility and speed remain separate claims.

## 2026-09-06 steward investigation

The reduced product itself has a repeatable loading benefit on the documented XML-expanded workload. A new, general XML template lookup also made complete startup faster against nearby controls. At the investigation checkpoint it was retained on a research branch and `main` remained `20d0e0a`; the subsequent authorized integration is recorded below.

### Reduced-package qualification

Verified the supplied checkpoint before operating: clean `main` at `20d0e0a`, runtime build `f886277`, one checkout, no stashes, and the older `7d464ea` package still deployed. Explicitly deployed the existing reduced package (runtime SHA-256 `76a07e45add1a04dbbc479ff5a32aa6d56179496f89c38afd8432cea720fa91c`). The existing 39 managed/23 fixture checks were adequate and were not rerun without a source change. The small activation smoke `steward-s01-reduced` reached menu at 23.297801 seconds with normal exit, 29 XML hits and no type searches. It qualifies startup and XML execution there, not type-search speed.

All following runs used XML-expanded content, startup searches, fresh seeded application profiles, the same independent observer/automatic exit, and Wake-Up Gagarin reuse off. The supplier itself remained active and reported a cache miss/cache creation in each run.

| Run label | Package mode | Menu seconds |
|---|---|---:|
| `steward-e01-absent` | Wake-Up physically absent | 74.494104 |
| `steward-e02-reduced` | Reduced candidate | 24.873233 |
| `steward-e03-reduced` | Reduced candidate | 24.427183 |
| `steward-e04-absent` | Wake-Up physically absent | 38.267105 |
| `steward-e05-reduced` | Reduced candidate | 24.536031 |
| `steward-e06-absent` | Wake-Up physically absent | 39.305417 |

The first absent run is unsuitable as a steady-state baseline: its reverse control was much faster. The additional candidate/absent repeat confirmed the remaining gap. The last four runs form candidate/absent/candidate/absent comparisons: absent mean **38.786261 seconds**, reduced mean **24.481607 seconds**, saving **14.304654 seconds (36.88%)**. This describes recently exercised file-cache conditions, not known cold storage. Do not turn two samples per arm into a universal average.

Frozen manifest, content order excluding the intentional Wake-Up entry, seeded configuration files, exclusions and observer matched across all six. Absence necessarily has no Wake-Up timing instrumentation; its candidate's retained measurement overhead is included in the net result. Both original and processed XML hashes matched in every capture. Reduced runs recorded 3293 XML hits and 762 fallbacks, plus 10 indexed Harmony fallbacks across 11 type calls, with no type errors/refusals. They all reached menu and exited normally with `automaticTestPassed=true`.

### Independently ranked questions and decisions

Loading completion includes deferred initialization, texture atlas creation and cleanup after XML processing. The actual `PlayDataLoader` and `LoadedModManager` bodies, existing cost records and new receipts informed this ranking. Remaining startup time is not fully attributed; subtracting a few overlapping historical timers does not identify a bottleneck.

| Priority/question | Work potentially avoided and why startup could improve | Evidence, uncertainty, effort/risk and smallest deciding experiment |
|---|---|---|
| 1. Literal named-template XML lookup | Find an inheritance template by its `@Name` attribute instead of rescanning unrelated roots; keep the ordinary suffix query and ordered patch mutations. | New reduced patch scopes were 2.86–2.93 seconds; frozen query shapes contained repeated template keys. Exact eligible cost was unknown. Small extension to existing workers, moderate XML mutation risk. One implementation, focused semantic checks and a forward/reverse expanded comparison; no syntax/worker expansion to rescue weak results. **Implemented; useful result below.** |
| 2. Reuse type ancestry work across different base-type requests | `GenTypes.AllSubclasses` scans the type inventory on each new requested base type; shared relationships could avoid repeated scans. | Actual code already caches repeated requests for the same base. This differs from Harmony name lookup and from rejected registration work. The second investigation below measured about 57 ms for the observed expanded calls and declined implementation. Earlier work outside the probe's coverage remains unmeasured. |
| 3. Reduce allocations behind costly final cleanup | Less temporary allocation might shorten collection before menu readiness. | Native deferred cleanup exists, but the current Unity unused-assets portion was only about 0.36 seconds and does not measure all managed GC. No attribution supports an allocation redesign. Inspect a bounded outer cost only if stronger evidence emerges; preserve required cleanup and initialization. **Low priority.** |
| Broader shared XML API coverage | Reach queries from any patch producer through `XmlNode` rather than individual worker adapters. | Review found a concrete lazy-list compatibility problem: arbitrary callers may save a result list, mutate XML and only then enumerate it, unlike the known worker consumption pattern. A global `SelectNodes` substitution cannot safely reuse the current helper unchanged. `SelectSingleNode` could be narrower but its eligible cost is unknown. **Not implemented; proposal rejected as specified, not a measured speed failure.** |
| Reuse unchanged Harmony index segments | Avoid rebuilding static-assembly indexes after another assembly loads. | All optimized type calls together cost only 0.31–0.34 seconds in new reduced runs; savings must be smaller. **Deprioritized before implementation.** |

The template experiment changes the mechanism that defeated earlier XML splitting: it avoids sequential search work with no copied document, worker scheduling or joins. It does not reopen failed worker-count, query-splitting or index-retention trials, and adds no individual-mod hook.

### Named-template experiment

An XML template attribute such as `@Name='ApparelBase'` and a child element such as `defName='Example'` identify different things. Research commit **`02b974e53cf5e8fb054e81c0e72a0b8acabc3e72`** adds a separate literal-attribute index through the same **11 vanilla workers**. It preserves native suffix evaluation, duplicate/missing/unsupported fallback, current zero/single-result list admission, mutation handling and shared resource bounds. Attribute insertion/removal/movement and value-child edits invalidate through the attribute's owning element. There are no new supplier requirements, selector families or global XML hooks.

The implementation passed **53 managed tests and 23 fixture checks**, with no skips; its build had zero warnings/errors. Focused changes cover attribute/definition key independence, namespaces, empty/missing values, duplicate transitions, callback timing, movement/value edits, custom-node fallback and shared bounds. Independent source review found no blocking issue. Package `artifacts/op7-fixture-package/02b974e53cf5e8fb054e81c0e72a0b8acabc3e72` has runtime SHA-256 `4177263426a87572214f867d80d8fa8d6fc501e4248c8b6559b0eae9e3308678`.

| Run label | Exact runtime source | Complete patch ms | Menu seconds | Template hits |
|---|---|---:|---:|---:|
| `steward-t01-control` | Reduced `f886277` | 4136.567 | 30.075451 | Not supported |
| `steward-t02-template` | Research `02b974e` | 2604.360 | 28.508558 | 266 |
| `steward-t03-template` | Research `02b974e` | 2664.948 | 28.360438 | 266 |
| `steward-t04-control` | Reduced `f886277` | 4381.910 | 30.273359 | Not supported |

Complete startup averaged **30.174405 → 28.434498 seconds**, saving **1.739907 seconds (5.77%)**. The complete patch stage averaged **4259.2385 → 2634.654 ms**, saving **1624.5845 ms (38.14%)**. Both forward and reverse comparisons improved. The patch timer includes index construction and invalidation costs; its reduction is not added to the complete-startup saving. Environmental conditions changed between series, as the reduced controls show; do not combine this percentage with the earlier 36.88% or subtract this saving from the earlier candidate mean.

Each research run increased XML hits from 3293 to 3559 and reduced fallbacks from 762 to 496, exactly accounted for by 266 attribute hits. Index builds increased from 46 to 57 and invalidations from 8 to 13; the net measured stage still improved. All 95 prepared-profile file hashes, active content/order, manifest, exclusions, observer, selectors and fresh supplier-cache conditions matched between these arms. Deployment/preparation receipts contained the intended source and DLL hashes. The only additional receipt field is the final attribute-hit counter, with no per-query logging. All runs captured normal zero exits and `automaticTestPassed=true`; independent log review found no new exception/error signatures.

Original XML SHA-256 was `b37856c344842f4fae83aa58aba1a1b44b63c8a473bb0e543f58abbf86891aef`; processed XML was `5ab199de6e5469557a58f27ae07b99bfd263d3888d3599a3f9ef4afdbb3e236c` across all ten expanded runs in both series. This supports unchanged captured XML on this input, not arbitrary gameplay equivalence or other-modlist qualification. Gagarin cache-hit reuse and template behavior on other workloads remain unmeasured in this investigation.

**Investigation decision:** retain the successful, independently reviewed experiment on `codex/loading-investigation` for a separate integration decision. The bounded trial ended without additional infrastructure or tuning. At that checkpoint, `main` remained `20d0e0a` and the final control restored the fixture to reduced `f886277`, label `steward-t04-control`, with normal exit. The owner subsequently authorized integration, completed below. No larger workload, normal game data, publication or push was involved.

Raw captures remain under `.rlo-test-instance/results/<label>/`. Private analysis and series scripts are under `artifacts/loading-investigation/`, including `summary.json`; checks/build logs are `artifacts/attribute-lookup-test-complete.log` and `artifacts/attribute-lookup-build.log`. No fixture or research output was added to Git.

## 2026-09-06 template lookup integration

The owner approved completing and integrating the template search feature. Focused follow-up review found no unresolved correctness gap in the existing worker interception, attribute mutation handling, duplicate/namespace fallback or shared limits. Existing semantic and matched performance evidence was retained. One small runtime improvement stores the index key with its parsed query rather than allocating the same key on every lookup; the installation receipt, package description, README and architecture now describe both definition and template searches.

Commit **`668d628a3b55bcc6c5a63470e9e80a3db5e5d61f`** was reviewed and fast-forward merged from `codex/loading-investigation` into local `main`. The final source passed **53 managed and 23 fixture checks**, zero skips; the exact package built on main had zero warnings/errors. No new experimental selector, global XML hook, individual-mod hook, persistent cache or mandatory supplier was introduced. Normal user activation and publication remain outside this feature integration.

Package `artifacts/op7-fixture-package/668d628a3b55bcc6c5a63470e9e80a3db5e5d61f` was explicitly deployed. Runtime DLL SHA-256: `cfb501d13b473abcc5ebf26b6ed6eb8a4c1026acb912a6b20938e7e3642eacb0`. All four prepared/captured runs identify that source/package, with the same pinned game and independent observer, verified deployment identity and no runtime mismatch.

| Integrated capture | Purpose and setting | Menu seconds | Complete XML patch ms | Template hits |
|---|---|---:|---:|---:|
| `template-main-s01` | Small activation, Gagarin reuse off | 27.566954 | 44.550 | 0 |
| `template-main-e01-cache-miss` | XML-expanded, fresh application cache, reuse on | 67.619083 | 2489.185 | 266 |
| `template-main-h01-cache-verify` | XML-expanded, supplier cache restored from e01, reuse verification | 28.235609 | 2.619 | 0 |
| `template-main-e02-default` | XML-expanded, fresh application cache, reuse off; final default configuration | 30.089103 | 3422.332 | 266 |

These are **functional integration checks**, not new matched speed comparisons. The long fresh-cache run and verification overhead must not be interpreted as a new gain/regression percentage. The earlier 5.77% complete-startup and 38.14% patch-stage comparisons remain the relevant experimental performance evidence; this closeout neither adds to nor recomputes them.

The two fresh expanded runs each recorded 3559 XML hits, including 266 template hits, and 496 fallbacks. All three expanded captures preserved the same original/processed XML hashes reported above. The restored cache was actually reused: its receipt reports `admitted=true`, `reused=true`, `sameSourceParse=true`, 19,534 roots, 19,525 mapped source assets, zero mapping errors and zero ownership errors. With no patch search work on that cache hit, both template hits and XML index builds remained zero. The small activation retained 29 ordinary definition hits and no supplier requirement. All four runs reached menu, exited normally with code zero, captured `automaticTestPassed=true`, and had no new exception/error signatures in independent review.

**Closeout:** the feature is integrated, committed and left deployed at `668d628`; the final captured label is `template-main-e02-default`, with Gagarin reuse off and the game closed. The fully merged research branch was deleted, leaving only `main`, one worktree and no stashes. Later closeout documentation commits do not change the built runtime identity. Private research/build/capture evidence is preserved under ignored paths; nothing was pushed or published, and normal game data was untouched. Broader gameplay/modlist/platform qualification remains separate.

Final offline/build logs: `artifacts/template-integration-checks.log` and `artifacts/template-integration-build.log`. The bounded run script and extracted receipts are `artifacts/loading-investigation/integration_runs.py` and `integration-summary.json`; authoritative captures remain under `.rlo-test-instance/results/`.

## 2026-09-06 second loading investigation

**No additional product optimization was justified.** Independent reviews and bounded native timers ruled out several small targets. A genuinely new optional cache-writing mechanism then showed a strong offline result but became slower when implemented and measured in the game. It was rejected and removed; the integrated package is restored.

### New questions, evidence and decisions

Started from clean `main` at `252af87`, integrated runtime `668d628`, the single checkout and fixture, and created `codex/shared-loading-investigation`. Read current code and related archived failures before selecting new routes. Only activation and XML-expanded lanes were launched. Read-only reviewers worked alongside implementation; one owner controlled mutations/builds/fixture operations, and other local work paused for measurements.

The initial ranking favoured different type-family searches, repeated mod-requirement parsing and ordered-file deduplication, then deferred initialization. Measurements redirected the investigation toward ingredient filters, shared Harmony work and finally cache writing:

| Question and potentially avoidable work | Evidence, uncertainty, risk and smallest deciding experiment | Decision |
|---|---|---|
| Reuse relationships across different base-type requests instead of rescanning every type | Existing same-base caches already work. A native miss/call timer was cheaper than building a new index with ordering/invalidation risks. Expanded `AllSubclasses` took 53.186 ms across 160 calls, including 145 misses; nonabstract searches took 3.330 ms across four misses. | Too small in the measured window. Earlier initial type discovery before Wake-Up construction was not measured. |
| Reuse parsing of identical mod-requirement strings | 121,508 `ValidateMayRequires` calls included 3,454 nonempty pairs and only 12 distinct observed pairs, but all calls totalled just 21.608 ms. A replacement would need to preserve current membership tests and error reporting. | Repetition existed, but no useful time ceiling. No implementation. |
| Compact overridden file entries instead of repeatedly shifting a list | Source review found repeated `List.RemoveAt` in `GetAllFilesForModPreserveOrder`, distinct from the rejected persistent catalog. First test was execution/cost, before changing ordering or validator callbacks. | No calls in the observed window. The actually used `GetAllFilesForMod` totalled only 62.248 ms across 64 calls. Do not label the unused method globally unimportant or a failed implementation. |
| Reduce repeated scans in ingredient filters | Native `ThingFilter.ResolveReferences` repeatedly scans definitions for tags/materials/exclusions. Sparse exclusions could avoid already-disallowed entries, but callbacks and virtual definitions constrain replacement. | Recipe base-method sums were misleading: 1,671 calls accumulated 1,409.327 ms across up to 32 threads, yet their first-entry/last-exit span was only 82.515 ms. This is the measured calls' window, not a timer around caller preparation/joins or derived overrides. No recipe replacement justified. All filter calls across other stages totalled 975.382 ms with overlap; their 14.45-second span includes gaps and is not removable work. |
| Reduce deferred initialization or texture conversion | Measured later methods, then inspected native DDS/PNG paths. A converted-texture cache would differ from failed read-ahead, but the workload already uses converted DDS extensively. | Content reload totalled 5,053.616 ms, including 4,350.032 ms for 4,320 DDS loads. A new PNG conversion cache cannot save that DDS work. Resets were 138.389/25.891 ms, implied generation 91.639/3.417 ms, ordinary ThingDef resolution 229.066 ms; separate probes measured DefOf binding 148.623 ms, static constructors 395.867 ms and atlases 564.444 ms. No new demonstrated avoidance mechanism; no repeat of warmed read-ahead failures. |
| Cache shared Harmony patch records or reduce repeated discovery | `PatchAll(Assembly)` took 2,197.348 ms; `PatchClassProcessor.Patch` took 3,672.522 ms, with overlap between these scopes. Actual shared record lookup/deserialization was only 15.248 ms across 2,088 calls. | Record caching rejected by its ceiling. Constructor attribute discovery remains **unmeasured**: the measured `Patch` method excludes its constructor. Reusing attributes or batching published patches risks changing attribute constructors, Prepare/target/Cleanup callbacks and immediate publication. No supported replacement proposed from these outer costs alone. |
| Avoid cloning XML just to write the framework's cache | Native Gagarin `Save` imports resolved XML into another document, then serializes it. Streaming could avoid that duplicate tree on cache misses. This differs from the failed file catalog, inheritance preparation and the retained cache-loading reuse. Moderate semantic risk: repairs, file-opening order, private state and repeated saves must match. First test was offline equivalent writing, followed by one bounded implementation and live comparison. | Offline promising; live negative, detailed below. |

The probes also measured ordinary XML loading/combining/conversion at 1,220.892/767.551/2,504.024 ms in the final probe. Those outer costs did not provide a materially new basis to repeat settled XML concurrency/preparation trials.

### Probe qualification and limits

Temporary packages `4cb6ab7`, `b0ac19a` and `64c3f00` successively measured the relevant native methods without replacing their results. Each passed 53 managed/23 fixture checks and built without warnings/errors. Captures were `newcost-s01` (activation, 21.217421 s), `newcost-e01` (expanded, 34.262792 s), `newcost-e02-deferred` (expanded, 37.465574 s) and `newcost-e03-shared` (expanded, 39.310108 s). All passed automatic startup and normal exit; intended probes installed with no measured exceptions, probe errors or unfinished calls. Expanded original/processed XML hashes matched the integrated controls.

Coverage begins at Wake-Up initialization and ends at the menu update; earlier loading is missed. Instrumentation adds overhead, nested scopes overlap, and worker sums are not wall time. First-entry/last-exit spans include gaps. These runs locate costs; their menu times are **not** candidate/control speed comparisons. No generic-method body hooks or blocked CPU profiling were used.

### Streaming cache-write experiment

The offline test used already-resolved captured XML, .NET 10 and a null output sink. Clone-and-write averaged **430.381833 ms** versus **91.734067 ms** for direct writing: **338.647767 ms / 78.69%** less time in that test. The clone arm allocated about 57.6 MB; direct writing produced exactly the same 31,837,097 output bytes. This excluded live admission, source repairs, disk publication and the retained byte buffer required for lifecycle compatibility; it was not a Unity/Mono startup result.

Research `f5ee037` implemented the writer; final guard-only revision **`af9aebf2dc4e530630b201ee13b5152fbeb6d2f8`** was deployed. Final DLL SHA-256: `bddfc61025cfe758209de615afea41566345a8fd478345edb60ef56fde993180`. Its 71 managed checks covered byte equivalence, source repairs, renamed roots, namespace/mixed-content cases, repeated-save restoration, unsupported inputs, event listeners and file failure ordering; all 23 fixture checks passed. Final build had zero warnings/errors. No supplier DLL was bundled and no new fixture selector was added.

The candidate required the exact optional supplier and relevant native bodies/hooks. It prepared a memory buffer before opening the output file, preserved source repairs, retained bytes for a subsequent native Save/Dump, and released them at cleanup. Unsupported/custom XML, change listeners and error-bearing entries retained native behavior. This extra work was included in the live timer.

| Capture | Setting and actual execution | Menu seconds | Complete save ms | Preparation ms |
|---|---|---:|---:|---:|
| `save-s01-optional` | Activation, `on`; supplier absent, adapter refused normally | 23.592247 | — | — |
| `save-e01-control` | Expanded, `timing`; native Save executed | 32.635153 | 493.717 | 0 |
| `save-e02-stream` | Expanded, `on`; streaming actually admitted/executed | 32.032732 | 791.220 | 295.388 |

The expanded arms used the same package, frozen content/order, seeded settings, observer and automatic exit, with fresh application caches and no restored supplier cache. The candidate admitted 19,534 entries, checked 1,295,002 nodes/14,121,309 characters and released its buffer at Clean. Both runs passed automatic startup and normal exit. Windows file caches were not forced cold.

**The complete save became 297.503 ms / 60.3% slower.** Excluding preparation leaves 495.832 ms, essentially the entire native 493.717 ms. There is no useful remaining measured saving to recover merely by tuning the checks. The 0.602421-second earlier menu in this single candidate is inconclusive without reversal and must not be promoted into a startup improvement.

The candidate wrote 31,841,422 bytes and failed the raw-output hash check, stopping the sequence automatically. Inspection found 264 line differences in empty-element spelling (`<x />` versus `<x></x>`): native ImportNode normalizes empty elements, while source WriteTo can preserve the explicit closing form. Canonical XML including comments matched SHA-256 `37bf52218620ded632ce366150bbc8d0dc83f7349e73dda582ec998a62cefe17`; original XML also matched. That does not turn the failed byte qualification into full compatibility proof. No further timing arms or cache-hit qualification were run. Fixing spelling alone would not address the measured cost, so the prototype was removed instead of extended.

This is a negative result for this implemented, guarded memory-buffer route under these conditions. It does not prove every possible streaming writer fails. Reopening requires a materially new basis addressing both its actual write cost and admission cost; the offline .NET 10 percentage alone is insufficient.

### Closeout

Commit `22b7f20` removed the writer and dedicated tests; the diagnostic probe was already removed before that trial. At the investigation closeout (`7e56be9`), runtime/tests exactly matched `main` at `252af87`, and records remained on `codex/shared-loading-investigation`. No experimental runtime remained active and no new feature was accepted. The subsequent owner-authorized repository cleanup merged this history into local `main` and deleted the completed branch; experimental commits remain reachable in Git, and raw evidence remains under `artifacts/new-loading-investigation/`.

Explicitly restored package **`668d628`**, verified its deployment/prepared identity, then captured **`newcost-e04-restored-main`** with default startup searches and Gagarin reuse off. It reached the menu at 43.182201 seconds, exited normally with code 0 and `automaticTestPassed=true`, and retained 3,559 XML hits including 266 template hits. Both XML hashes matched the integrated controls. This is a restoration check, not another speed comparison. The game is closed; normal game data was untouched; nothing was pushed or published.

Raw summaries: `cost-summary.json`, `save-benchmark.json`, `save-series.json`, `save-output-diff.json`, `save-canonical-comparison.json` and `restored-summary.json` in `artifacts/new-loading-investigation/`. Exact private captures remain under `.rlo-test-instance/results/`.

## 2026-09-06 remaining-loading investigation

**No new worthwhile optimization was demonstrated.** The investigation measured previously uncovered shared costs, including Harmony patch-class construction, empty inner-patch processing, method decoding and language loading. Their measured costs did not justify the required replacement complexity. Temporary probes were removed; the qualified runtime remains the product. These are bounded decisions on the authorized workloads, not a claim that loading has no remaining opportunities.

### Understanding and ranked questions

Started from verified clean `main` at `3bde795867e098bec419fa373ae36ba05f0ddb2c`, one checkout, only main and no stashes. The deployed `668d628` package, last capture `newcost-e04-restored-main`, and prior 53 managed/23 fixture checks matched the checkpoint. Research uses `codex/remaining-loading-investigation`; main is unchanged. Three read-only agents independently inspected early loading, Harmony and shared XML; one owner performed all edits, builds, deployment and launches. No competing investigation work ran during measurements.

Actual loading begins before Wake-Up: assembly loading/validation and initial type discovery precede mod constructors; texture work is largely deferred until later. Wake-Up's native-method probes cannot measure that earlier work. Existing raw Windows counter receipts provide a useful earlier boundary without new instrumentation: launch to observer installation was **5.992–6.441 seconds** in the template series, **10.126 seconds** in `newcost-e03-shared`, and **12.033 seconds** in `newcost-e04-restored-main`. Prepatcher's reported vanilla-load component also varied, while serialization stayed about 1.65–1.87 seconds. These historical conditions differ; the gap is neither a removable stage nor proof of a later-stage regression.

| Question / work potentially avoided | Evidence, uncertainty, effort and compatibility | Smallest deciding experiment and decision |
|---|---|---|
| 1. Reduce repeated Harmony patch-class discovery | Its constructor scans methods four times to find setup, cleanup and target callbacks. Previous patch timers excluded this constructor. Skipping arbitrary attribute construction can change behavior; caching only method enumeration has a smaller ceiling. | Native constructor and discovery timers first. Measured costs below ruled out an implementation on this workload. |
| 2. Avoid empty inner-patch processing | New inspection found that Harmony groups call instructions and creates empty sorters/replacement arrays even when no hooks inside the method are present. This differs from rejected patch-record caching. A native-list fast path could preserve instruction objects and ordinary patch publication, but needs supported bodies, empty-list checks and fallback for foreign changes. | Count actual empty cases and time native processing inside complete replacement creation. All 1,022 observed calls were empty, but eager processing took only 98.891 ms. Its final lazy enumeration was not measured separately; a complete fast-path benefit is untested. No guarded replacement justified by the measured cost. |
| 3. Reuse original-method decoding across repeated patches | Each wrapper builds a fresh instruction reader. Decoding has no reusable cache, but decoded objects hold wrapper-specific locals/branches and are subsequently mutated. Safe reuse requires immutable templates plus copying/remapping. | First measure all decoding as a conservative ceiling before collecting repeat identities. All 1,029 calls totalled 223.726 ms; only an unknown subset repeats a method, and copying/guards would consume savings. No cache implemented. |
| 4. Reduce shared translation and definition-lookup preparation | Actual source exposed unmeasured translation-key processing, language injection and numeric definition identifiers. Language reads already overlap and data has an existing loaded flag; the pinned translation-key mapping is hardcoded. Identifier ordering/collision behavior and language callbacks must remain intact. | Time the complete native entry points, including their existing parallel work and joins. Costs below were small. No replacement; other languages and larger workloads were not measured. |
| 5. Reuse early successful assembly type enumeration | Native assembly validation calls `Assembly.GetTypes()` and discards the result; later type inventory calls it again. This differs from the already-small post-Wake-Up ancestry searches. The second call may be cheap after the first resolves types. It needs earlier access than current Wake-Up activation and must preserve assembly identity, ordering, failed-resolution retries, dynamic behavior and reloads. | **Untested.** First establish the cost of the second enumeration with an early native timer. Do not build an early cache or broaden bootstrap infrastructure without a useful measured ceiling. Initial inheritance metadata discovery is also outside current probe coverage and may see only game types. |

The final ranking followed actual code and new costs. No worker-count, read-ahead, XML preparation, registration-replacement or cache-writer failure was reopened. The earlier complete registration measurement of 691.679 ms remains adequate; it was not confused with summed callbacks or remeasured.

### Native measurements

`remaining-e01-probe` used research package **`cd86a133da08b2d5908064697ea1733abd36a774`**. Its 834 patch-class constructors totalled **165.152 ms**; the nested 3,336 auxiliary-method searches took **71.479 ms**, and 834 patch-method discoveries **19.905 ms**. Type-attribute discovery across all callers took **130.844 ms** / 7,770 calls. Replacement creation took **2,629.949 ms** / 1,022 calls, including the **98.891 ms** eager inner-patch processing. These scopes overlap; they must not be added. Peak observed concurrency in these scopes was one, but first-entry/last-exit spans contain gaps and are not phase durations.

`remaining-e02-shared` used **`fe30031b32bbfccc1636953fb55cf8822544b093`**:

| Complete native entry point | Calls | Elapsed ms |
|---|---:|---:|
| Harmony instruction decoding | 1,029 | 223.726 summed |
| Translation-key XML parsing / mapping construction | 1 each | 2.672 / 0.766 |
| Language data loading | 1 | 111.898 |
| Language injection before / after implied definitions | 1 each | 7.210 / 5.483 |
| Language metadata initialization | 1 | 14.372 |
| All short-hash assignment, including scheduling and completion | 1 | 97.223 |
| Biography loading | 1 | 86.501 |

English settings were preserved from the fixture seed. These measurements do not qualify other-language performance. The decoding timer covers the named method, not all wrapper preparation, emission or publication; no saving is inferred for the remainder of replacement creation.

### Verification and closeout

| Capture | Purpose | Menu seconds |
|---|---|---:|
| `remaining-s01-probe` | Small activation; first probe installs and executes 14 wrapper builds | 16.630791 |
| `remaining-e01-probe` | Expanded Harmony discovery and inner-patch costs | 32.434130 |
| `remaining-e02-shared` | Expanded decoding, language and identifier costs | 29.512776 |

All three captured normal exit code zero and `automaticTestPassed=true`. Every intended probe installed, with zero recorded exceptions, probe errors or active calls at receipt. Expanded original and processed XML hashes match the integrated controls (`b37856c3…` and `5ab199de…`, respectively). Fresh seeded profiles, the frozen lane/order, independent observer and automatic exit were retained; Gagarin reuse was off and no application cache was restored. Windows file-cache state was not forced cold. The probe sets differ, so **the menu times are not a speed comparison** and support no percentage gain.

The initial probe passed 53 managed checks and 23 fixture checks with no skips. Each committed probe build had zero warnings/errors; later edits changed only temporary measurement targets/counters and were checked through successful native execution. Intermediate package `3fe557b` was built but never deployed. Final product source, tests, build inputs, fixture tool and observer are byte-for-byte unchanged from qualified `668d628`; its existing validation remains applicable without another rebuild. Probe removal commit **`fb38381`** preserves the measured implementations in reachable branch history. No candidate implementation was retained or merged into main.

Independent review confirmed intended source identities, unchanged expanded active order/XML and no new startup error signatures in all three probe captures. It also verified the early counter boundary and the eager/lazy timer limitation.

Raw source inspection, build/test logs, extraction scripts and `summary.json` remain under ignored `artifacts/remaining-loading-investigation/`; exact private captures remain in the fixture. The qualified package was explicitly redeployed after removal. Final `remaining-e03-restored-main` reached menu at **27.933424 seconds**, exited normally with `automaticTestPassed=true`, recorded 266 template hits, retained the same XML hashes and contained no probe receipt. The deployed DLL was rehashed to the qualified `cfb501d1…` identity, and no RimWorld process remains. This restoration check is not a matched performance comparison. Nothing was pushed, published or released; normal game data and the frozen selection were not modified.

## 2026-09-06 early loading and Harmony emission follow-up

**Discarded: no reliable overall loading-time improvement was demonstrated.** The Harmony experiment measurably reduced shared work, but its potential net benefit on the tested workload was too small to justify continued pursuit. Building the replacement functions that let mods change game behavior took about **512 ms less (18.1%)** in a matched four-run comparison. Installing the candidate later measured **272–291 ms**. Complete loading varied substantially in both enabled and disabled runs, so no whole-startup percentage is attributed to this change. At the owner's direction, the implementation, activation selector and dedicated tests were removed. Evidence and recoverable commits remain; main is unchanged.

### Independent questions and decisions

The investigation resumed on `codex/remaining-loading-investigation` after the earlier stopping point. Actual code showed two different instruction-writing steps in Harmony and repeated early assembly enumeration before Wake-Up's usual installation. Prior failures informed the following decisions without prescribing the route:

| Ranked hypothesis and why it could shorten loading | Evidence, uncertainty, effort and risk | Smallest experiment and disposition |
|---|---|---|
| 1. Avoid repeated reflection while Harmony writes replacement instructions | Every operand uses reflection to discover the generator, select an overload and invoke it, including an argument-array allocation. Direct calls can preserve the same generator and instruction order. This is a new mechanism, distinct from rejected patch-record caches, decoding reuse and empty-patch preparation. Moderate effort; wrong overload, exception or generator behavior would be a serious compatibility error. | Implement bounded typed calls with native fallback, test generated functions, then measure complete wrapper construction and menu startup. Source-only and combined-backend experiments executed successfully. The stage saving repeated, but overall benefit was inconclusive and too small to justify continued pursuit; approach discarded, evidence retained. |
| 2. Reuse type arrays discarded by assembly validation | Later discovery enumerates the same successful assemblies again. Earlier probes missed this work. A cache would require early installation and correct handling of assembly identity, failure retries and changing suppliers. | A contained Prepatcher hook measured the original restarted pass before mod constructors. Repeated enumeration cost only 18 ms small / 30 ms expanded. No cache justified; probe removed. |
| 3. Avoid repeated metadata parsing across the loading restart | `ModLister` constructs metadata for every discovered local mod, including inactive entries, before active selection. This differs from the rejected XML-definition catalog. Safe reuse would have to preserve metadata objects, filesystem observations and later mod-list behavior. | Measure instance `ModMetaData.Init` without prematurely invoking `ModLister`'s static initialization. All 316 calls cost 247 ms small / 333 ms expanded. This narrow ceiling does not justify an early persistent cache. |
| 4. Remove DDS copying, or revisit first-read overlap on a new basis | Native compressed DDS loading already passes mapped bytes to Unity. Only uncompressed BGR data takes the suspected extra copying/conversion path. First-read storage behavior remains distinct from the confirmed warm read-ahead negative. | Inspect headers in the unchanged active roots before implementing. All 4,320 DDS files were compressed: 3,758 BC7 and 562 DXT1, no uncompressed BGR. No copy-removal prototype or warm read-ahead rerun. Comparable naturally occurring first-read conditions remain untested. |

The DDS inventory read 628,120 header bytes, not pixel payloads, over 289,347,280 file bytes. Its coverage includes active roots and can include unused version folders; it is not a trace of runtime-selected texture winners. It establishes zero applicable BGR files in those roots, not that arbitrary modlists contain none. General texture creation/decoding was not rewritten.

### Early observation, with limits

Probe source/package `8da23ec0f8abd84d58a2172d39eada9bd63f2ad5` used the pinned Prepatcher API to insert one guarded call at restarted `Root.Start`. The first-pass load and Prepatcher serialization were outside that hook. Native calls, ordering, returns and exceptions were retained; all observed calls during installation, outstanding calls and exceptions were zero at completion.

| Native work | Small activation | XML-expanded |
|---|---:|---:|
| Probe installation | 445.825 ms | 486.874 ms |
| Metadata initialization, 316 calls | 246.784 ms | 332.729 ms |
| Assembly validation `GetTypes`, 9 / 23 calls | 30.999 ms | 254.859 ms |
| All inventory `GetTypes`, 11 / 25 calls | 209.301 ms | 307.352 ms |
| Inventory of already validated assemblies | 18.206 ms | 30.486 ms |
| Complete mod-class creation, including constructors | 612.482 ms | 5,359.487 ms |

These are overlapping scopes and must not be added. `early-s01` and `early-e01` passed normal automatic startup at 35.250825 and 78.666138 seconds. Their long menu times are not a speed comparison: the small run spent 22.980 seconds before the injected entry; the expanded run reached that entry at 4.723 seconds and had roughly 42 additional seconds later than `emit-e06-control`, unexplained by the measured stages. Independent review found matching expanded XML, settings, order and error signatures. Neither environmental delay nor probe perturbation was established as the cause. The narrow repeated-enumeration result supports stopping that cache route, not claiming that all early loading is cheap. Probe and API-reference changes were removed in `4f5ef84`; their code and receipts remain recoverable.

### Harmony mechanism, correctness and measured costs

The source emitter first writes instructions into Harmony's intermediate representation; `_DMDEmit.Generate` later writes the executable replacement. The candidate replaces only each authenticated existing `DynEmit` call. It uses typed overloads for supported operands and the same original generator. Unsupported operands, `sbyte`, changed incoming code, incompatible patch state, custom generators and unadmitted scopes retain native dispatch. Source emission admits only the actual validated factory proxy. Backend admission requires the actual core dynamic-method generator and Harmony's held patch-construction lock. A small counter changes before Harmony activates a new replacement; a nested patch invalidates subsequent direct emissions in an admitted scope. No gameplay-mod identities or content are special-cased.

Existing optional Gagarin behavior is unchanged. The research is pinned to the reviewed game/Harmony identities and stops using fast dispatch after menu startup. Ordinary fallbacks remain available. Executable tests compare native and typed branches, locals and operands, preserve invocation-error wrapping, reject changed callsites, and install a real nested Harmony patch to verify fallback and scope cleanup. The combined source passed **58 managed and 24 fixture checks**, zero skips and zero build warnings/errors, after early-probe removal. Independent source and capture reviews found no unresolved correctness blocker. This establishes focused behavior and menu startup, not arbitrary gameplay compatibility.

The initial source-only package was `fc9a342fec82bfcd4b25c6f5510b7f8eef4818dd`. Six expanded captures `emit-e01-control` through `emit-e06-control` all executed 1,008 wrappers without errors. Excluding the first two runs, the balanced counts of original/candidate measured source-emission means 599.433 → 431.230 ms (168.204 ms, 28.1%). Menu times in order were 36.906677, 28.761622, 29.226481, 31.039523, 31.950419 and 34.575044 seconds. Independent review identified run-order drift; the arithmetic 6.8% from the last four is **not an attributed startup gain**. Small `emit-s01-on` installed and exited normally but built no post-install wrappers, so it only qualifies installation.

The new basis for extending the candidate was the observed source-emission saving, not an assumed benefit from every Harmony stage. Backend source is `304c1f8`; the probe-free measured package is **`4f5ef847d1fc267d65b97e9d4813980ad9eceff7`**:

| Expanded capture | Emitter setting | Menu seconds | Source emission ms | Backend ms | Complete wrapper ms |
|---|---|---:|---:|---:|---:|
| `backend-e01-control` | timing | 32.401047 | 594.958 | 1,417.837 | 2,712.297 |
| `backend-e02-both` | both | 28.854492 | 376.236 | 1,224.483 | 2,303.554 |
| `backend-e03-both` | both | 31.267883 | 378.817 | 1,212.847 | 2,331.725 |
| `backend-e04-control` | timing | 29.877719 | 639.812 | 1,527.981 | 2,947.853 |

Control and candidate used identical guards and timers. Mean complete wrapper construction fell **2,830.075 → 2,317.640 ms: 512.436 ms / 18.1%**. Source and backend timers are inside wrapper work; do not add them to that saving. All runs built 1,008 wrappers and observed 1,053 backend calls. Each candidate executed 76,187 source and 76,739 backend typed calls, with 286 / 451 native calls retained. All 45 unadmitted backend scopes remained native; errors and epoch refusals were zero. Small `backend-s01-both` passed installation, with no admitted wrapper work. The four menu means give an arithmetic 3.46%, but the candidate overlapped its reverse control and that percentage is not promoted as a startup gain.

To include installation and observation costs, package **`318b9fdce95c7b6e264d9b6a189279fec43fb65f`** added only an installation-time receipt. A reversed arrangement compared `both` with `off`, which installs no emitter hooks or timers:

| Expanded capture | Setting | Menu seconds | Candidate installation ms |
|---|---|---:|---:|
| `cost-e01-both` | both | 68.013982 | 272.186 |
| `cost-e02-off` | off | 34.131986 | — |
| `cost-e03-off` | off | 49.008092 | — |
| `cost-e04-both` | both | 36.777233 | 290.941 |

The timer includes `TryInitialize` work but not the type's earlier static initialization, which occurs in both settings. Installation alone consumes much of the earlier stage saving; subtracting those different-run measurements is not a demonstrated net startup gain. Both settings had substantial variability. Retain all four captures rather than dropping slow runs to manufacture a win. **The complete-startup comparison is inconclusive, and no integration into main is justified by it.** More patch-heavy modlists could spread the fixed setup cost over more wrappers, but larger lists also add unrelated loading work, so a significant overall percentage improvement does not follow from mod count alone. Larger workloads were not authorized or run. Significant scaling is unproven, not an exception warranting continued pursuit. The owner discarded this approach after reviewing the limited potential benefit; reopen only with concrete new evidence of worthwhile complete-loading savings, not another noisy pair or linear extrapolation of stage timing.

All captured runs exited normally with code zero and `automaticTestPassed=true`. Intended fast paths were verified where described; fresh application profiles/caches, order, observer and automatic exit matched. Expanded original/processed XML hashes remain `b37856c3…` / `5ab199de…`, with no new normalized startup errors. OS file caches were neither forced cold nor evicted. The discarded implementation remains recoverable in Git and ignored packages; it is absent from working source. The qualified `668d628` package was restored and passed `reoriented-e01-restored-main`: menu **21.624703 seconds**, 266 template hits, matching XML, normal exit and automatic success, no research receipts; deployed DLL SHA-256 again matched `cfb501d1…`. That restoration is a functionality check, not a matched speed comparison. Useful raw evidence and summaries are under `artifacts/early-and-shared-loading/`, including `emitter-series.json`, `emitter-repeat.json`, `early-series.json`, `backend-series.json`, `product-cost-series.json`, `decision-summary.json` and `restored-summary.json`; complete private captures remain in the fixture.

## 2026-09-06 unexplained-delay investigation

**The earlier long delay did not recur. No avoidable wait or loading-time improvement was demonstrated.** Three identical XML-expanded probe runs reached the menu in 20.20-20.54 seconds; the qualified package restored afterward took 20.52 seconds. These fast runs do not diagnose or fix the earlier 49-79-second captures. No optimization was implemented, and the temporary flow probe was removed.

### What needed explaining

Existing stage timers did not account for substantial variability. Preserved file modification times provided a new, approximate clue: processed `Unified.xml` completion to menu was 28.470 seconds in `cost-e01-both`, 26.940 seconds in `cost-e03-off`, 57.991 seconds in `early-e01`, versus 7.506 seconds in `reoriented-e01-restored-main`. File-time/UTC alignment is not an exact game timer, but it directs attention after XML writing. Those old captures contain no CPU or memory samples; final Unity cleanup lasted only about 0.33-0.51 seconds. They cannot establish machine pressure, disk waiting or a particular game callback as the cause.

Actual `LongEventHandler` code starts a background thread, polls its completion on later main-thread updates, then drains finishing callbacks synchronously. Synchronous events may wait for a repaint; enumerator events execute in roughly 100 ms slices across frames. There is no fixed sleep or blocking thread join in this coordinator. The finishing-callback loop checks its changing list length, so newly appended callbacks run in the same drain. Earlier worker-count/concurrency failures were not reopened.

| Hypothesis | Smallest distinguishing measurement |
|---|---|
| Main-thread updates or repaint handoffs leave ready work waiting | Measure update entry/exit, long gaps outside those updates, loading-text/thread-state transitions, and the beginning of the finishing drain. |
| Unmeasured finishing callbacks perform substantial work | Time the unique native callback invocation, retaining order and exception handling; compare elapsed time with CPU time consumed by that thread. |
| Delays reflect resource pressure or time spent waiting outside measured code | Sample the verified fixture process's CPU/I/O counters and aggregate system CPU/free physical memory from launch. These are counters, not a CPU profiler or physical-disk-wait measurement. |

### Probe and evidence

`8ea2a658dc542c3b85ce00e4af8b62a32c255c69` added the default-off `--loading-flow` research selector. Installation occurs during the optimizer's mod constructor, after the existing searches; it intentionally misses the beginning of already-running background loading. It observes XML loading/combining/patching/conversion, `Root.Update`, coordinator state, and the original finishing-callback callsite. Native queue iteration, callback identity/order and exception propagation remain intact. No individual gameplay/UI mod behavior is optimized or skipped.

The probe uses the same raw Windows counter as the independent menu observer. Callback elapsed time and calling-thread CPU time are nested in the outer drain and must not be added to it. State sampling is limited to 100 ms intervals. Short callback totals are grouped by method; individual intervals of at least 5 ms and long update gaps are retained. Evidence is buffered until startup completion; its final write precedes menu repaint and adds observation overhead. An external artifact helper invokes the ordinary fixture launcher and samples only the verified child process plus aggregate system counters every approximately 0.5 seconds. It neither changes scheduling nor inspects or controls other applications.

The initial source passed **55 managed and 23 fixture checks**, zero skips and build warnings/errors. Two focused executable checks verify multicast callback order, preservation of the original exception, and a single callsite replacement with the surrounding queue loop/labels/exception blocks intact. `flow-s01` passed small activation at **9.157270 seconds**. Its thousands of tiny callbacks motivated bounded aggregation in **`b2cf2147854edd36d724169ed8602158209f01d4`**, which passed both focused checks and all 23 fixture checks and built cleanly before deployment.

| Expanded capture, same `b2cf214` package | Menu seconds | Complete finishing drain ms | Calling-thread CPU ms in drain | Largest gap outside measured updates ms |
|---|---:|---:|---:|---:|
| `flow-e01` | 20.464790 | 4,329.839 | 4,125.000 | 333.566 |
| `flow-e02` | 20.198194 | 4,405.970 | 4,125.000 | 354.568 |
| `flow-e03` | 20.542883 | 4,349.614 | 3,968.750 | 339.353 |

The last recorded observation of a live background thread preceded the main finishing drain by only 7-42 ms. That interval includes any remaining background work; it is an upper bound on that handoff delay in these runs, not an exact thread-exit timer. The longest measured update was the drain itself. Callback totals chiefly identify shared content/texture setup, definition post-loading, sound setup and final initialization. For example, the first expanded run spent about 1.02 seconds in `ThingDef` post-loading callbacks, 0.97 seconds in content reload callbacks, 0.96 seconds in sound setup and 0.74 seconds in final initialization; these are work costs, not demonstrated removable waits.

Resource samples retained at least **15.33 GiB of available physical RAM**. Independent analysis found roughly 9-12% aggregate host CPU use and 1.83-1.85 average CPU cores consumed by the game during these expanded runs. This supplies no sustained system-wide pressure explanation for them. Periodic and aggregate counters cannot rule out brief stalls, contention affecting one thread, GPU/rendering delays or storage effects. Calling-thread CPU excludes parallel workers and uses coarse OS accounting, so small per-callback differences must not be overinterpreted.

All four probe captures passed normal exit code zero and `automaticTestPassed=true`, with no trace errors, exceptions or dropped records. Independent review verified matching content/order, observer, fresh application-cache conditions, all 95 seeded profile files, expanded original/processed XML hashes, and normalized startup-error signatures. Windows file caches were not forced cold; no pressure was manufactured, and no larger workload was run.

### Disposition

This is **non-reproduction with useful new coverage**, not proof that earlier delays were environmental or that the game has no coordination problem. Without an observed slow interval, changing scheduling or callbacks would be speculative. Stop this bounded pass; if the long delay recurs, the useful next evidence is the same phase/resource trace during that recurrence, rather than more uninstrumented timing pairs or synthetic pressure.

`8f142ca` removed the probe, selector and dedicated tests. Runtime/build/test/fixture-script inputs again match qualified `668d628` exactly. The existing qualified package was explicitly restored and passed `flow-e04-restored-main`: **20.522592 seconds**, normal exit and automatic success, matching XML, 266 template hits, no probe receipt and verified deployed DLL SHA-256 `cfb501d1…`. This restoration/control check supports unchanged behavior; no percentage gain is claimed. Main remains `3bde795` and no new product code was integrated.

Recoverable probe commits and ignored packages remain available. Raw decompiled coordinator source, tests/build logs, per-run external resource samples, summaries, `flow-analysis.json`, restoration verification and `run_flow.py` are under `artifacts/unexplained-loading/`; full captured in-game traces remain under the corresponding private fixture result folders.

## Retained features

| Mechanism | Historical evidence | Current interpretation |
|---|---|---|
| Shared XML definition lookup | Isolated `perf-r10-def-lookup` patch stage 21.297 seconds; the earlier original stage was about 101 seconds. Actual hits, fallbacks, rebuilds and invalidations were captured, with matching XML/reports. | Useful work avoidance; historical scope included the now-removed CombatExtended-specific worker, so do not assign its exact percentage to the reduced worker set. |
| Shared named-template lookup | `steward-t01`–`t04` forward/reverse comparisons saved 1.740 seconds / 5.77% complete startup and 1.625 seconds / 38.14% of the patch stage; 266 template hits per candidate and identical captured XML. | Integrated in `668d628` through the existing vanilla worker set; final-package fresh/cache-hit checks passed. This is workload-specific evidence, not a universal percentage. |
| Harmony type fallback lookup | Isolated `perf-r09-type-lookup`: Loading Progress 235.271 versus absent 338.931 seconds; 791 calls and actual indexed fallback work, with zero recorded errors. | Supports the mechanism; this was an isolated single run, not a repeated measurement of the cleaned combined package. |
| Optional Gagarin parsed XML reuse | Matched forward/reverse XML-expanded cache-hit comparisons saved about 499 ms, 34% of the affected cache-loading stage. Mean observed menu was 0.416 seconds earlier. Integrated-main qualification also saved about 377 ms in that stage. | Separately qualified on supported supplier/input conditions; default off. Callbacks, exact input, mapping, ownership, cache misses and optional absence were checked. This does not cache completed definition objects. |

Details: [performance investigation](initial-loading-investigation.md), [startup-search closeout](startup-search-qualification.md), [Gagarin qualification](gagarin-cache-loading.md). Those are historical packages. Current source, builds, deployed fixture and research-package state are listed in [current state](../current-state.md).

## Historical combined result, including CharacterEditor

The accepted historical `startup-searches` configuration included XML lookup, Harmony type lookup **and CharacterEditor preset dictionary reuse**. Eight final comparisons included two runs per arm for each application-cache condition:

| Historical condition | Loading Progress mean, absent → combined | Cleanup mean, absent → combined |
|---|---|---|
| Fresh application cache | 339.959 → 152.623 seconds (55.1% stage-clock reduction) | 352.039 → 165.456 seconds (53.0%) |
| Gagarin cache hit | 234.885 → 125.761 seconds (46.5%) | 246.959 → 138.699 seconds (43.8%) |

Menus were observed roughly eight versus four minutes for the first condition and six minutes twenty versus three minutes twenty for the second. Those broad historical observations were not exact modern menu-observer timings. None of these percentages measures the current reduced product or a universal modlist average.

CharacterEditor-only `perf-r08-character-presets` preserved the preset counts and recorded 9,208 reuse hits, zero fallbacks/errors and no retained entries. Its object-preset scope took 2.728 seconds and turret scope 0.810 seconds. The menu was observed by 431.679 seconds against an absent observation interval of 437.230–478.070 seconds; the difference between first observations is not an exact isolated startup saving. Original-code timing and later combined runs also showed much smaller preset work, but do not isolate its contribution to the repeated combined startup result. Do not subtract a preset-stage duration from the table or add component speedups. CharacterEditor was removed for the former general-product scope, not because this historical work failed to execute. Its newly authorized optional reintegration is recorded separately.

## Experiments that did not justify promotion

| Route and avoided/overlapped work | Evidence and decision |
|---|---|
| Typed Harmony instruction emission | Saved 512 ms / 18.1% in measured wrapper construction, but installation cost 272-291 ms and no reliable overall loading-time improvement was demonstrated. Discarded at owner direction; code, selector and dedicated tests removed. Significant percentage scaling on larger modlists is unproven. [Follow-up results](#2026-09-06-early-loading-and-harmony-emission-follow-up) |
| Original XML metadata catalog and persistent-cache infrastructure | A small cold/warm catalog pair took 6.276/6.146 seconds for all mod loading versus 1.948 seconds with original-loader timers. Warm hits still read and parsed the files while paying verification and publication costs. No useful product gain; the legacy infrastructure was removed. This does not reject every possible cache design. [Performance investigation](initial-loading-investigation.md) |
| Indexed XML cursor | Avoided repeated linked-list indexing and really processed 24,581 cursors and 220,610 sequential steps on small content, without mutation fallback or parse failure. Conversion took 1086.387 ms; no small-profile improvement was established. Prototype removed. [Performance investigation](initial-loading-investigation.md) |
| Simple XML worker-count increases | Four workers saved about 30 ms in one small XML pair; eight workers were slower, and neither established an end-to-end improvement over absence. Vanilla already used two background threads plus the caller. Removed rather than tuning more workers without evidence. [Performance investigation](initial-loading-investigation.md) |
| DDS reads ahead of ordered texture creation | Old busy-machine full DDS scope was 340.099 seconds original versus 146.897/144.767 seconds with two readers, but failures and unmatched resource/cache conditions limited attribution. Overnight idle full runs reached the menu successfully; warmed original startup 193.943 seconds matched candidates 195.979/194.423 seconds. No useful warm whole-startup gain; reader code removed. Comparable first-read behavior remains unresolved. [DDS closeout](dds-read-ahead-qualification.md) |
| PNG reads ahead of unchanged native decoding/upload | Warm medium original mean startup 39.657 seconds versus candidate 39.609 seconds: 0.048 seconds, not useful. Whole texture scope improved about 0.677 seconds (12.680 → 12.004), which did not establish a meaningful startup gain. Reader code removed. [PNG closeout](png-read-ahead-qualification.md) |
| XML inheritance preparation | Work really overlapped, but complete small stage rose 252.612 → 562.843 ms, including preparation and ordered import. Rejected net loss. [Concurrency](startup-concurrency-experiments.md) |
| Cross-reference lookup batching | Removed contention but made the complete cross-reference stage roughly 24% slower. Summed waiting across overlapping workers was not wall time available to save. [Concurrency](startup-concurrency-experiments.md) |
| Patch-search splitting | Broad-query splitting slowed the expanded patch stage. A narrower Replace candidate's first improvement did not repeat in reverse controls (4431.953 → 4504.266 ms). [Concurrency](startup-concurrency-experiments.md) |
| Reuse of XML reader workers | First LoadModXML improvement reversed on repeat (830.699 ms control versus 1153.689 candidate), despite real worker reuse and complete joins. [Concurrency](startup-concurrency-experiments.md) |
| Parallel numeric/Boolean preparation for definition conversion | Values were genuinely prepared and consumed; repeat cache-miss conversion was 2411.805 ms control versus 3314.487 candidate, and a Gagarin-hit pair was 1918.076 versus 2058.635 ms. Prototype and dedicated diagnostics removed. [Conversion preparation](definition-conversion-preparation.md) |
| Avoiding shared XML index rebuilds | All measured index-build work totalled about 1.315 seconds. The unnamed-root trial avoided only one of 232 builds without a stage gain. An empty-tail query shortcut was considered but not implemented or timed; no gain is established for it. [Shared XML](xml-index-rebuild-investigation.md) |
| General definition registration | The earlier approximately 16-second metric accumulated per-type callback work; it was not complete registration wall time. The correctly placed outer scope measured 691.679 ms. No replacement implementation justified. [Registration](definition-registration-costs.md) |
| HugsLib enumeration reduction | Historical focused work avoidance existed, but this individual-mod route was parked after scope clarification. It is not retained or renamed as a general loading optimization. [HugsLib closeout](hugslib-enumeration-investigation.md) |

Raw XML construction already performed real parallel work: an earlier three-thread interval accumulated about 1,031 ms CPU within 447 ms elapsed time over 1,572 assets. More threads alone are not a fresh hypothesis. Do not revive failed routes without materially new evidence about shared costs.

The earlier [texture/XML investigation](texture-and-xml-investigation.md) records an inactive labelled candidate, a failed full run and the Mono risk that closed generic methods can share native code; those failures must not be counted as performance or compatibility successes. [Earlier PNG research](png-read-ahead-prototype.md) had functional small runs but no valid medium candidate. [Startup work reduction](startup-work-reduction.md) records historical resource pressure and fixture selection changes; changing inputs was not an optimization result.

## Remaining questions and stopping point

No additional tested route justified inclusion in this cleanup. DDS still has a plausible first-read mechanism: overlap storage reads with ordered native texture work when bytes are not already readily available. Existing candidate runs followed originals, and neither the initial slow original nor a later long texture stage proves physical disk cost or a known cold state. No matched first-read speedup is established. A future bounded, naturally occurring first-read comparison may be useful, but global cache eviction, manufactured resource pressure or another immediate warm pair is not justified by these records. New evidence about substantial shared work can still justify a fresh investigation.

Broad definition-object caching remains a hypothesis with substantial compatibility complexity, not an implementation recommendation supported by current costs. Avoid chasing arbitrary mod callbacks under the general-loading remit. The first investigation qualified the reduced searches and the subsequently integrated template feature; the second investigation did not establish another useful optimization. These remaining questions are examples, not a prescribed roadmap. Broader gameplay/platform qualification and release decisions remain separate.

## 2026-09-06 repository consolidation

The owner requested a clean single-main repository and a fresh investigation handoff. Both investigations' decisions and measurements are now on local `main`; their completed branches were deleted after merging. The tree retains the qualified `668d628` runtime and tests unchanged. Temporary probes and the rejected cache writer remain absent, with their original commits reachable through merged history.

Current state was shortened to a status summary linking here for detailed evidence. Fifteen retained historical reports were renamed by subject, with links updated and original-path provenance preserved in the archive index. Existing source modules already separate startup, the two search mechanisms, optional framework reuse and compatibility guards, so no runtime refactor was needed. Obsolete phase plans and product implementations had already been removed; useful negative-result history and ignored private evidence were preserved.

This documentation/Git cleanup required no new build, deployment or live launch. Verification covered local documentation links, unchanged runtime/test/build inputs, the commit fixture guard, and the final single branch/worktree with no uncommitted changes or stashes. The deployed and last captured package remains `668d628`, label `newcost-e04-restored-main`; source, deployment and measured performance claims are unchanged.

## 2026-09-06 shared-startup cost investigation

**No additional worthwhile optimization was demonstrated.** New observations bounded early assembly writing and unsuccessful asset searches. Their avoidable portions were too small on the authorized workloads to justify replacements. Probes were removed; the qualified package is restored. These are decisions about particular mechanisms and conditions, not proof that all remaining loading work is necessary.

### Starting understanding and ranked questions

Verified clean research `3f124d0`, main `3bde795`, qualified source/package `668d628`, deployed DLL `cfb501d1…`, and successful capture `flow-e04-restored-main` before switching. Runtime, tests, build inputs and fixture script matched qualified source. Created `codex/shared-startup-cost-investigation` from current main in the single checkout, preserved the older research branch and copied its newer documentation in `55d48db`. Root owned every mutation, build and fixture operation. Two agents independently reviewed native source and evidence; they stopped scans during measurements.

Actual loading starts before Wake-Up's constructor: Prepatcher writes and reloads assemblies first. XML assembly/patching/conversion happens later; content and other callbacks finish before the menu. Existing nested/worker timers cannot be added to explain that sequence. Native source and recent flow captures directed this short ranking:

An assembly is compiled code, usually a DLL. Serialization here means turning Prepatcher's edited code back into loadable bytes; Cecil is the library performing that editing and writing. An asset bundle is a packaged collection of assets that Unity searches and loads.

| Hypothesis and possible benefit | Evidence, uncertainty, effort and risk | Smallest experiment and decision |
|---|---|---|
| Overlap separate rewritten-assembly writes | Prepatcher writes reload candidates sequentially; its prior total was 1.377 seconds. Writing several independent modules could shorten startup, but source already reuses unchanged raw bytes. Moderate implementation effort; Cecil's shared resolution/metadata state and ordered loading would need protection. | First-pass per-assembly native timer. One game assembly dominates; only 242.393 ms of other modified writes in expanded. No parallel writer implemented. This does not settle optimization inside a single assembly's writer. |
| Avoid native XML searches for names absent from an existing index | `ScopedDefLookup` retains native behavior for missing keys. A proven empty result could avoid a document scan, but unknown nodes, mutation callbacks, namespaces and lazy result behavior require care. Small code change with nontrivial semantic risk; useful frequency was unknown. | Add two counters without changing search results. Expanded had only two missing keys and four duplicates among 496 fallbacks; small had none. No negative-result implementation or safety-contract expansion justified. |
| Avoid repeated XML field searches or parser creation | Generated parsers repeatedly call field/type helpers, separate from rejected scalar preparation. New exact-game decompilation shows dictionaries already cache direct field searches, hierarchy searches, aliases, custom loaders and post-load methods; parser delegates also already cache by type. | Source inspection disproved the simple repeated-reflection premise. No replacement/cache or another scalar experiment. |
| Reuse audio folder/clip results | Recent sound callbacks totalled 986.655 ms. Captured XML had 853 folder requests/788 unique paths and 463 clip requests/434 unique paths, but no repeated key within any one grain container. Folder enumeration already uses prefix indexes. Cross-callback reuse risks stale public content/native bundle state; repetition rate is not a time fraction. | One captured-XML count plus actual native source. No safe within-callback reuse opportunity in this content; no cross-callback cache implemented. |
| Skip unsuccessful native asset-bundle name trials | Fresh source evidence: the shared finder tries extensions and prefixes through `AssetBundle.LoadAsset`, whereas folder enumeration already uses an index. Another index could avoid failed native calls, but Unity might already make them cheap. Moderate compatibility risk from name semantics and mutable bundles. | Measure all post-Wake-Up non-generic managed single-asset load wrappers as a conservative ceiling, including completed native calls. Expanded failures totalled only 166.236 ms. No extra index implemented. |

Related archived XML/conversion/texture failures were consulted. No worker-count, read-ahead, scalar preparation, index-retention, cache-writing or Harmony-emission experiment was reopened. DDS still uploads mapped compressed bytes directly; the earlier zero-BGR inventory remains sufficient for that copy-removal question. Comparable naturally occurring first-read storage behavior remains untested.

### First-pass serialization and XML observations

Package **`ae17f0b821fb54654eb816900343d56d062770bc`** installed native prefix/finalizer timers from a Prepatcher free-patch callback returning `false`, without modifying a Cecil module or any serialized data. Completion was recorded after all writes and before original assemblies were retired. This reaches the first pass missed by the former restarted-Root probe. Native results, order and exceptions are retained.

| Capture | Menu seconds | Writes: modified / reused | Game-assembly write ms | Other modified writes ms | Probe installation ms |
|---|---:|---:|---:|---:|---:|
| `sharedcost-s01` | 36.899078 | 1 / 6 | 1,666.109 | 0 | 27.816 |
| `sharedcost-e01` | 35.106835 | 3 / 16 | 1,603.800 | 242.393 | 12.251 |

The expanded non-game writes were 17.619 and 224.774 ms. These were observations of the general supplier mechanism, not candidates targeting those mods. All unchanged assemblies reused matching positive raw/output lengths; their expanded calls summed to 0.027 ms. The game output was about 15.8 MB. Even perfect overlap of other assembly writes could save only roughly 242 ms in this capture before synchronization/admission costs. Replacing the stream/copy path has no separately measured material cost; persistent reuse would require preserving arbitrary prepatch callbacks and their external inputs. Neither was implemented.

Every serialization receipt completed with zero probe errors/exceptions. Per-call receipt writing occurs outside its individual write timer but inside the supplier's overall serialization interval; the totals must not be equated. The probe's API reference was private/nonpackaged and separately hashed because the ordinary build verifier checks only game/Harmony: `0PrepatcherAPI.dll` SHA-256 `39a3841d1c61c41d173e5cfb81262fde78db655a0d604a2dd91a5697e9d57300`. Inspected/guarded `PrepatcherImpl.dll` was `d55356423b6bc8d9d41cd46807ab83c7cefd09243bf13509cd432b178fc82095`.

Expanded XML counters retained 3,559 hits, including 266 templates, 57 index builds and 13 invalidations. The two missing-key observations establish a poor candidate frequency, not a measured negative-query time or proof that returning empty results would be safe. These counters and the serialization probe/API reference were removed before bundle measurements.

### Bundle lookup observations and invalid first attempt

Initial package `66c1891` safely refused target discovery in `bundlecost-s01`: Unity forwarded the same type through multiple assemblies. That capture reached menu at 11.484623 seconds and exited normally, but **supplies no bundle-cost evidence**. Revision **`9f5138917ce95229a2cf87e9b27607679b7109c7`** deduplicated identical type objects while still refusing genuinely ambiguous targets.

| Capture | Menu seconds | All calls / elapsed ms | Reference-null calls / elapsed ms | Probe installation ms |
|---|---:|---:|---:|---:|
| `bundlecost-s02` | 12.782794 | 91,478 / 1,208.807 | 83,147 / 69.376 | 171.895 |
| `bundlecost-e01` | 70.724616 | 196,120 / 5,384.563 | 186,834 / 166.236 | 174.252 |

Both probes actually executed and completed with peak concurrency one, no active calls left, no skipped native calls and no exceptions. The target is the non-generic managed `LoadAsset(string, Type)` wrapper; no generic/native-body hook was used. The sum includes downstream wrapper/hook work and covers all callers after Wake-Up initialization, not just the shared finder or all earlier startup work. Reference-null results are counted separately from Unity's destroyed-object equality semantics. Other calls' durations include successful asset loading and are not removable failed-search work.

Failed calls were numerous but cheap. Even eliminating their entire expanded measured time provides only 166 ms before index construction, lookups, validation and installation. The timing probe's 174 ms installation is not an exact cost prediction for an unbuilt candidate. It nevertheless illustrates why the observed failure ceiling alone does not support added machinery. No implementation, reverse speed comparison or startup percentage is claimed.

### Verification, restoration and limits

The first probe passed 53 managed and 23 Python fixture checks, zero skips. The bundle revision passed two focused startup checks plus all 23 fixture checks; the one-line forwarding fix built successfully and then demonstrated actual native calls in both live lanes. Every package build had zero warnings/errors, was explicitly deployed and had its intended revision verified after preparation. Independent source reviews found no result/order/exception defect.

All six launches, including the refused probe and final restoration, captured normal exit code zero and `automaticTestPassed=true`. Only activation and XML-expanded were used. Expanded active order, manifest, exclusions, all 95 prepared-profile file hashes, observer package and fresh application-cache condition matched the qualified control. Original/processed XML hashes matched; independent review found unchanged normalized startup error signatures/counts in the measured expanded captures. No global file-cache eviction occurred; operating-system file-cache state was uncontrolled. Probe package revisions and menu times differ: these are cost-location/functionality runs, **not matched performance arms**. The 70.72-second expanded capture and 38.70-second restoration leave variability unexplained and do not diagnose the earlier delay.

Removal **`aef1176`** restores runtime, tests, build/fixture-script inputs and observer exactly to qualified `668d628`. No new qualified build was necessary; its existing package was explicitly redeployed. Final **`sharedcost-e02-restored-main`** reached menu at **38.704710 seconds**, recorded 266 template hits, retained the matching XML and prepared-profile hashes, and exited normally with automatic success. Deployed DLL SHA-256 is again `cfb501d13b473abcc5ebf26b6ed6eb8a4c1026acb912a6b20938e7e3642eacb0`; no cost-probe receipt remains and no RimWorld process is alive.

Main remains `3bde795`; both research branches and rejected probe commits remain reachable. Latest research build `9f51389` is retained under ignored packages, not deployed. Raw decompilation, test/build/preparation/launch logs, identity checks and `summary.json`/`summarize.py` are under `artifacts/shared-startup-costs/`; authoritative captures remain private in the fixture. No new product runtime is retained, merged or published, and normal game data was untouched. Different languages/content, single-assembly writer mechanisms, naturally occurring first-read storage behavior and broader gameplay/platform qualification remain untested by this pass; none is an established gain or prescribed next implementation.

## 2026-09-06 comparative texture and XML assessment

**Recommendation: investigate a serial finite-name XML query planner for the native Replace/Remove workers next.** This means recognizing a bounded expression selecting several names, then finding those definitions in one pass using a set of names. It removes repeated comparisons while preserving the original XML objects and native mutation code. It is the strongest next bounded experiment among the areas assessed, not a proven significant startup improvement. No new optimization was retained.

The owner requested a thorough comparison of actual texture/asset loading and larger XML changes, with daytime resource competition accepted as the working explanation for timing variance. That assumption was respected; this pass did not diagnose contention or attempt to eliminate it. Research branch `codex/texture-xml-assessment` starts from current main `3bde795`; `0e0eb7d` preserves the preceding findings. Read-only agents independently inspected both mechanisms and challenged probe correctness. Only the root task mutated the checkout or operated the fixture; other local investigation stopped during launches.

### What actually happens in texture and asset loading

Loose files, asset bundles and atlases are different operations. `ModContentPack.ReloadContentInt(false)` loads audio, loose textures, strings and bundles during the ordered finishing queue. `ModContentLoader<T>` chooses supported files and gives DDS counterparts precedence over ordinary PNG siblings. Individual mod holders retain their texture objects; a later global lookup winner does not make earlier objects disposable.

DDS is already compressed for the graphics hardware. The inspected `ModDdsLoader` maps file bytes, checks headers, creates an uninitialized texture, copies compressed payload and uploads it. The fixture's Unity 2022.3.35f1 constructor maps `createUninitialized:true` to both `DontInitializePixels` and `DontUploadUponCreate`. Thus avoiding that initial DDS upload is already native behavior, and its final upload is still needed. Earlier observations of 4,320 DDS loads taking 4,350.032 ms are actual work, but do not identify 4.35 seconds of removable decoding. The workload-root inventory contained compressed BC7/DXT1 files rather than an eligible uncompressed BGR conversion path; an inventory is not a trace of all runtime winners.

PNG takes a different path: decoding, possible compression, mipmaps (smaller images used at a distance) and upload. Unity documents that `LoadImage` uploads its result, making the subsequent native `Apply` worth examining in paths with no intervening pixel edits. However, mipmap generation, readability, format and CPU-compression branches must remain equivalent. The unchanged workload predominantly chooses DDS. The historical PNG experiment deliberately exercised original PNG siblings and cannot establish the current workload's eligible cost. See [Unity's LoadImage documentation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ImageConversion.LoadImage.html) and the [PNG qualification](png-read-ahead-qualification.md). PNG processing remains an untested conditional opportunity, not a reason to change fixture selection.

An atlas combines many small textures into one larger image. Native `StaticTextureAtlas` sorts and packs textures, draws their pixels into a render target, constructs masks and smaller mip images, compresses where enabled and creates meshes describing each rectangle. DDS source textures can therefore still participate in rendering and recompression. The original texture objects remain accessible after atlas construction.

Two concrete source findings warranted a fresh cost check:

- `CalcRectsForAtlasNew` repeatedly calls `list.IndexOf(texture)` to recover sorted positions. For unique input, this performs n(n+1)/2 comparisons. A first-position map could remove those searches while preserving native packing and duplicate semantics.
- Color-atlas construction uses `DontInitializePixels` without `DontUploadUponCreate`, even though native rendering subsequently writes the image. Its possible unnecessary constructor work lies inside layout. This differs from the already optimized DDS constructor.

The fresh measurements below make both low priority on these workloads. Reusing temporary render targets or avoiding a second PNG upload remains less settled, but no measured eligible cost supports choosing either first. GPU submission scopes also do not independently measure when the GPU finishes its queued work.

### What actually happens in XML processing

Raw XML construction already uses workers. Later operations combine documents, apply patches in order, resolve inheritance and convert real XML nodes into game definitions. Source documents remain visible through public `LoadableXmlAsset.xmlDoc` references and callbacks. Native combination imports copies into a separate document. Custom loaders and optional Gagarin hooks can retain original or resolved nodes; sharing mutable inherited nodes or replacing them with a lazy overlay changes observable ownership.

The parser generates executable functions, but those functions still use reflection to assign many fields: they describe a field at runtime, then invoke `FieldInfo.SetValue`. The rejected scalar-preparation experiment explicitly retained these assignments, so direct stores were a materially new question. The new probe intercepts eight nongeneric generator emission sites and calls the original assignment exactly once. It does not hook a global reflection method or a shared generic native body.

More ambitious per-type parser specialization could remove additional name/type dispatch and boxing. It would also give up native sharing of ordinary `object`/`Def` parser bodies and increase code-generation cost. Existing field/type/delegate caches mean a new cache is not automatically new work avoidance. Constructors, random-state effects, custom loaders, reference registration and publication must remain ordered. The new store measurement rejects a direct-store-only change here; it does not prove that every larger parser redesign is useless.

Inheritance contains another real inefficiency: native code clones a resolved parent, then replaces portions of the clone with child content. A fused builder could avoid copying sections immediately discarded by overrides. That differs materially from failed parallel private trees plus ordered imports. However, previous complete inheritance was only about 0.25–0.39 seconds and nearly disappears on Gagarin cache hits. Preserving ownership, list merging, namespaces and mutation-event behavior is a substantial obligation for that ceiling. It ranks below query planning.

Replacing the entire XML representation or caching finished definitions has no current correctness argument covering real-node access, arbitrary callbacks, external inputs and mutable objects. Likewise, the rejected streaming Gagarin writer must address its measured live preparation/writing regression before reopening. These are unproven broad routes, not blanket failures of caching or XML redesign.

### Fresh measurements that changed the ranking

Temporary source/package **`d6d668c373e7e6dd83ccf132248165ce646b08f8`** measured original operations. Its focused checks passed two managed startup tests and all 23 fixture tests; build had zero warnings/errors. Independent review found no blocker. Deployment and prepared revision were checked separately before both authorized launches.

| Observation | Small `texturexml-s01` | Expanded `texturexml-e01` |
|---|---:|---:|
| Independent menu elapsed, seconds | 17.768411 | 41.048307 |
| Probe installation, ms | 291.074 | 305.694 |
| Original reflective stores | 193,394 | 323,487 |
| Summed original store elapsed, ms | 49.368 | 84.185 |
| Unique observed fields | 7,812 | 8,735 |
| Ordinary writable instance-field stores | 191,058 | 319,423 |
| Complete XML conversion scope, ms | 1,517.567 | 2,768.216 |
| Complete global atlas bake, ms | 272.071 | 577.234 |
| Root atlas layout elapsed, ms | 14.423 | 20.358 |
| Atlas color rendering scope, ms | 183.180 | 393.882 |
| Atlas mask scope, ms | 17.218 | 47.839 |
| Atlas mesh creation scope, ms | 12.321 | 22.238 |
| Atlas compression scope, ms | 40.005 | 87.763 |

All eight store emission sites were instrumented, and generated store wrappers actually executed; the three parser caches were empty at installation. Per-emitter execution counts were not recorded. Store calls had peak concurrency one, no exceptions and none active at completion. All measured stage hooks reported zero exceptions/skips. Both workloads baked eight atlases. Expanded layout had ten invocations because two recursively retried; reported root elapsed excludes that nested duplication. No fallback packer ran. Expanded atlas texture counts were 1,110, 1,009, 108, 163, 629, 133, 224 and 9.

The entire expanded reflective-store cost is roughly 0.2% of its observed startup; the entire layout scope is roughly 0.05%. Those are descriptive cost proportions, **not measured gains or precise future bounds**. A replacement would retain work and add admission/construction costs. Timestamp overhead is included in store timing; per-call aggregation is outside it but inside conversion. Probe installation is not a prediction of an unbuilt candidate's installation cost. Nested atlas/conversion scopes must not be added, and queued GPU work may complete outside an individual submission scope.

The quadratic atlas search is real, but the measured batches do not make it material. Merely citing a larger modlist does not establish larger single-atlas batches or a significant share of complete startup. No replacement was built for either low-cost finding.

### Ranked decision and smallest next experiment

| Rank / direction | Avoided work and reason it could help | Evidence, effort, risk and next decision |
|---|---|---|
| **1. Serial finite-name XML planning** | Scan roots once using literal-name membership instead of repeatedly evaluating a chain of OR predicates. Reuse existing worker boundaries and leave mutation code intact. | Medium bounded implementation effort. Current native Replace/Remove collect all selected nodes before mutation, so eager selection can match their behavior. Historical exercised families occupy hundreds of milliseconds, but exact eligible selection cost remains unmeasured. First compare plan construction plus selection against native XPath on real captured documents, checking node identity/order; then use a focused current live comparison if promising. |
| **2. PNG upload/mipmap path** | Potentially remove an upload after decoding on a branch where pixels did not change. | Narrow change but higher rendering/format risk and unmeasured natural eligibility in the DDS-heavy workload. First measure naturally executed decoder/finalization calls without selecting different files. Prove mip pixels, readability, format and error behavior before a replacement. |
| **3. Fused inheritance clone/merge** | Avoid copying parent subtrees that child overrides immediately discard. | Medium/high semantic effort for a small miss-only ceiling. First count discarded descendants in actual parent/child trees; stop without substantial waste. |
| **Deprioritized: direct stores, atlas lookup/constructor, broader parser or atlas rewrites** | Direct stores and remapping remove actual repeated work; broader redesigns might remove more. | Measured direct-store/layout costs are negligible here. More ambitious versions need a separate measured mechanism and compatibility premise. Atlas rendering/compression is mostly still-required work, not automatically available savings. |

For rank 1, initially admit same-field literal `defName` or `@Name` equalities joined by `or`, unnamespaced `Defs/<type>` roots and simple downward suffixes. Scan roots in document order, use a set for literal membership, and evaluate the original relative suffix only on matching roots. Preserve multiple matching roots and real node identities. Account for multiple `defName` children rather than assuming the first child represents XPath's node-set equality. Refuse mixed predicates, unsupported namespaces/nodes, escaping expressions and exceeded bounds. Keep ordinary fallback and original Replace/Remove mutation bodies. Do not extend the first version to Add: its selection can remain live during mutation.

This is an algorithm change from repeated predicate work proportional to roots times name alternatives toward one root pass plus a name set. It is **not** the rejected parallel splitting approach, which left XPath evaluation intact and added preparation, worker and completion costs. No particular mod, package ID or literal content should be recognized by the implementation. The benefit is limited to startups that actually execute patch searches: ordinary loading without the supplier or a supplier cache miss. A successful Gagarin processed-XML cache hit skips patching and has no corresponding search benefit. Keep Gagarin optional and avoid preparing query machinery when the work is skipped.

The historical `xml-query-work` receipts group by worker and query family, with only the first expression shown. They do not isolate individual search cost:

| Native inclusive family | `conc-e01-control`, ms | `conc-e04-control`, ms | `conc-e07-control-repeat`, ms |
|---|---:|---:|---:|
| Replace OR/suffix, 37 calls | 445.042 | 491.822 | 570.554 |
| Remove OR/suffix, 9 calls | 80.356 | 85.837 | 90.699 |
| Remove OR/root, 1 call | 2.511 | 4.436 | 2.121 |

The latest total of **663.374 ms includes mutation and potentially unsupported expressions**. It justifies the next bounded question; it is not projected savings, current eligible time or evidence for a large percentage. The accepted template lookup subsequently removed other historical patch work, so the old whole patch-stage time must not be treated as current opportunity.

The preserved `sharedcost-e02-restored-main` `Unified_Original.xml` contains 20,703 top-level nodes and 5,148 ThingDefs. A recorded two-name Replace expression still selects two real `thingClass` nodes. Despite its filename, Gagarin saves this document **after patching**; removed targets are already absent. It is suitable for offline selection/mechanism checks, not replay of historical execution times. A later promising candidate needs current eligible execution counts, preparation/fallback/install costs, nearby and reverse controls, complete-menu timing and separate behavior checks. No significant gain should be claimed unless those complete comparisons establish one.

### Restoration and evidence limits

Probe removal **`9e7a6dd`** restores runtime, tests, build and fixture scripts exactly to qualified `668d628`. Its existing package was explicitly redeployed and verified after preparation. Final **`texturexml-e02-restored-main`** reached menu at **61.926965 seconds**, exited normally with code zero and `automaticTestPassed=true`. All three launches used the independent observer and normal automatic exit, and actual exit/capture preceded the next run. No RimWorld process remains alive.

Expanded content/order, manifest, exclusions, all prepared-profile file hashes, observer package and fresh application-cache settings matched the preceding qualified control. Both captured XML hashes and normalized scanned error signatures matched in the expanded probe and restoration. This is startup/XML evidence, not full semantic/gameplay qualification. The different complete-menu durations are not a performance comparison and do not measure a gain or loss; operating-system file caches and the owner's concurrent activity were uncontrolled.

Main stays `3bde795`; research branches at `3f124d0` and `ab41f01` remain intact. Qualified built/deployed source is `668d628a3b55bcc6c5a63470e9e80a3db5e5d61f`, DLL SHA-256 `cfb501d13b473abcc5ebf26b6ed6eb8a4c1026acb912a6b20938e7e3642eacb0`. Research package `d6d668c` remains ignored and undeployed. Raw receipts, test/build/deployment logs, fresh atlas decompilation and `summary.json`/`summarize.py` are under `artifacts/texture-xml-assessment/`; prior conversion/texture source and query receipts remain in their referenced investigation artifacts/captures. No main integration, publication, normal-game changes or larger workload launches occurred.

## 2026-09-07 texture implementation experiments

**All four ideas were implemented and tested. None established a repeatable improvement in complete loading, so none is promoted.** Packing with full content validation was slower in both warm comparisons. Scheduling and the two GPU changes passed their exercised behavior checks but did not establish a useful startup gain. These conclusions apply to the tested implementations and workloads, not every possible texture-loading technique.

The owner authorized live launches while playing Stalker 2. Tests used one fixture process at a time: three small launches followed by sixteen XML-expanded launches. The full representative lane was not launched. The concurrent game and Windows file caches were uncontrolled; the investigation did not change, profile or interfere with the owner's game. Fresh application profiles are not cold operating-system caches. Large timing swings were treated as uncertainty, not candidate gains or regressions.

### Implemented mechanisms and behavior checks

An atlas is a larger texture containing many smaller textures. Mipmaps are smaller versions used when a texture is rendered at reduced size. These experiments preserve RimWorld's ordinary source selection, atlas layout, shader operations and texture publication; they change how input bytes or temporary resources reach those operations.

| Experiment | Actual implementation | Verification and decision |
|---|---|---|
| Texture-creation scheduling | Save Unity's `allowThreadedTextureCreation`, set it false only around native `ModContentHolder<Texture2D>.ReloadAll`, and restore the exact saved value in `finally`. Change the call inside `ReloadContentInt`, avoiding a detour of a shared generic method. | Both expanded candidates entered and restored all 16 scopes; initial setting was true. Their DDS times were 4.098 and 1.080 seconds. Later native control reached 0.953 seconds, so the faster candidate stage was not unique to the setting. Whole startup did not establish a repeatable gain. Not promoted. |
| Packed DDS inputs | Store unchanged DDS bytes in one private uncompressed file with an offset/length index. Reuse a shared memory mapping, passing bounded spans into the unchanged native DDS header and texture-construction code. Keep native acquisition on misses. Every hit hashes current source contents; pack integrity is checked once at installation. | Built 4,320 entries / 289,347,280 bytes. Both warm runs had 4,320 hits, zero misses and zero new records. A separate audit compared every packed byte range with its current source. Tests rejected same-size, same-timestamp source edits and damaged packed data. Warm startup was slower than both following controls; validation erased the acquisition benefit. Reject this implementation. |
| Temporary GPU surface pooling | Use Unity's temporary RenderTexture pool at the selected local-variable creation sites in color, mask, first mip surface and compression operations. Remove unused depth only from integer compute-compression surfaces. Preserve native initialization/shaders and return pooled surfaces through ownership-aware cleanup. Array-held lower-mip allocations remain native. | Small and expanded original-path replay comparisons matched. Expanded candidates used 54 pooled acquisitions with zero outstanding surfaces; small used 50. Timing-only atlas bakes were 0.605 and 0.534 seconds, overlapping controls. No repeatable startup gain. Not promoted. |
| Reduced atlas transfers | Borrow the already-rendered color surface for the first mip-generation input and bypass its otherwise redundant full-size blit. Preserve the native base copy and subsequent mip computation, compression and publication. Restore borrowed-resource state in `finally`. | Small and expanded replay comparisons matched; each candidate borrowed eight inputs and bypassed eight blits. Timing-only bakes were 0.533 and 0.599 seconds, overlapping controls. The apparent first stage saving did not repeat. No repeatable startup gain. Not promoted. |

These are shared engine mechanisms: no fixture mod names, IDs or asset keys select an optimization. Eligibility depends on supported game/Harmony identities, original method contracts, compatible published patches, main-thread execution and the loading phase. The cache is bounded to 8 MiB per file, 2 GiB total and 65,536 entries. It writes only an Wake-Up-owned profile cache and never rewrites mod textures. Its prototype does not automatically repair a damaged existing cache; such entries miss and use native source loading.

Research commit **`84414ebfb6c4241ec5f1750ed992197017e2637e`** contains scheduling, pooling and reduced transfers. **`1975f08c0929f2ea5b9a6bcce2a66f746503b6e0`** adds packed DDS and was built/deployed for every expanded run. Its runtime DLL SHA-256 is `548f10db83a21efb42c007292fb7f1d055f091793079619eba01562d11d3e2f0`. Both commits and their ignored packages remain recoverable on `codex/texture-loading-experiments`. The explicit research selectors are `control`, `schedule`, `pool`, `fused`, `verify-pool`, `verify-fused` and `pack`; absent/default operation does not activate them.

Offline validation passed **55 managed and 24 Python checks**; builds completed with zero warnings/errors. Original method-body identities and replacement callsites were checked before live admission. The game's Span type requires its Mono core-library identity: DDS bridge emission/body checks used the local original-PE helper, then actual game admission and execution. No additional System.Memory dependency was bundled. Early reference/type-resolution failures were corrected before the successful package and live tests.

Four separate atlas verification launches replayed all eight atlases with the experiment disabled and compared dimensions, format, mip count, readability, sampler properties and rendered hashes at each mip resolution. All matched, with zero reported errors/mismatches. This is rendered-output evidence on the tested graphics path; it is not a raw compressed-byte comparison, exhaustive shader/platform coverage or gameplay qualification. Verification includes extra original baking and GPU readback, so its durations are excluded from speed comparisons.

### Captured results

Every row below passed the independent menu observer, normal exit code zero and `automaticTestPassed=true`. Each expanded row executed 4,320 DDS loads and baked eight atlases. Timers are inclusive and cannot be added: texture creation sits inside DDS loading, and mip/compression work overlaps the atlas scopes. GPU submission timing is not a measurement of all completed GPU execution.

| Label | Mode | Menu seconds | DDS seconds | CreateTexture seconds | Atlas Bake ms |
|---|---|---:|---:|---:|---:|
| `tex-s01-control` | Small control | 22.335492 | No DDS calls | No DDS calls | 325.958 |
| `tex-s02-verify-pool` | Small verification | 29.388280 | No DDS calls | No DDS calls | Excluded |
| `tex-s03-verify-fused` | Small verification | 29.465501 | No DDS calls | No DDS calls | Excluded |
| `tex-e01-control` | Expanded control | 112.858044 | 42.485 | 1.140 | 603.566 |
| `tex-e02-schedule` | Scheduling | 37.532740 | 4.098 | 1.202 | 590.596 |
| `tex-e03-control` | Reverse control | 68.855442 | 22.435 | 6.475 | 585.438 |
| `tex-e04-verify-pool` | Expanded verification | 64.883634 | 4.271 | 1.283 | Excluded |
| `tex-e05-verify-fused` | Expanded verification | 57.025228 | 4.060 | 1.201 | Excluded |
| `tex-e06-pool` | Pooling | 39.680323 | 4.204 | 1.242 | 604.623 |
| `tex-e07-fused` | Reduced transfers | 42.004770 | 4.100 | 1.236 | 533.448 |
| `tex-e08-control` | Reverse control | 39.451691 | 4.431 | 1.366 | 689.604 |
| `tex-e09-pack-build` | Pack construction | 79.944642 | 23.821 | 0.343 | 669.946 |
| `tex-e10-pack-warm` | Warm pack | 43.874752 | 7.018 | 0.292 | 677.590 |
| `tex-e11-control` | Reverse control | 39.126670 | 4.214 | 1.273 | 569.389 |
| `tex-e12-schedule` | Scheduling repeat | 54.208983 | 1.080 | 0.244 | 652.533 |
| `tex-e13-fused` | Reduced transfers repeat | 37.640308 | 4.135 | 1.222 | 598.762 |
| `tex-e14-pool` | Pooling repeat | 76.262604 | 22.717 | 7.559 | 534.009 |
| `tex-e15-pack-warm` | Warm pack repeat | 35.760238 | 3.801 | 0.223 | 560.936 |
| `tex-e16-control` | Final reverse control | 29.787651 | 0.953 | 0.209 | 554.775 |

Ordinary probe installation cost 284–362 ms in these runs. Warm pack installation cost **2,927.061 / 2,962.963 ms**, including full-pack validation; it must not be omitted from the cache comparison. Both warm runs also hashed **289,347,280 current source bytes** inside loading. First cache construction finalized in **3,545.414 ms**; warm completion cost **45.376 / 44.899 ms**. Finalization runs before the observed menu repaint, so the menu durations include it. The first construction run is not a warm-cache speed comparison.

Warm packing versus its following native controls was **43.874752 versus 39.126670 seconds**, then **35.760238 versus 29.787651 seconds**. The corresponding DDS-stage gaps were approximately 2.80 and 2.85 seconds, before the extra installation/pack validation cost. These paired observations support rejecting this implementation; they are not a universal slowdown percentage. Reading source bytes earlier also moves page-fault work out of `CreateTexture`, so a shorter inner creation timer is not proof of faster upload or faster complete loading.

All expanded candidates matched the first expanded control's mod order, manifest, exclusions, observer package, selected optimizations and seeded profile file hashes. Only the documented TexturePack restoration differs in warm arms; no Gagarin cache was restored. Both captured XML hashes and normalized scanned error signatures matched across all expanded runs. The receipt/evidence checks are in `artifacts/texture-followup/analyze.py` and `analysis.json`; the independent full byte audit is `tex-e09-pack-build-byte-audit.json`. Packed data SHA-256 is `e9b0ebb4c3f1460f1303d5ed5d0f80d2429b792e125463ba073e5a1c11fa31e2`.

### Stopping decision and restoration

The result is implementation and qualification evidence, not a new product speedup. Scheduling and atlas changes remain unqualified under uncontrolled contention. The tested validated-pack design showed repeatable extra cost. Reopening that design requires a materially different way to reduce validation/acquisition cost while preserving freshness; repeating the same warmed comparison is not justified. Reopening the GPU changes needs evidence of meaningful avoidable atlas cost beyond this roughly half-second workload. Quiet-host or larger-workload behavior remains untested, not disproved. No extrapolation from mod count establishes a saving.

Removal **`a6aeb07cf31e672fa18d704442472748c0fda61d`** deletes the experimental runtime, fixture selectors, references and tests, restoring runtime/build/test/script inputs exactly to qualified **`668d628a3b55bcc6c5a63470e9e80a3db5e5d61f`**. A direct Git comparison confirmed that identity. The existing qualified package was explicitly redeployed; no rebuild or repeat of unchanged offline tests was needed. Preceding research documentation and all ignored evidence were preserved.

Final **`tex-e17-restored-main`** reached the menu in **42.885685 seconds**, exited normally with code zero and `automaticTestPassed=true`. Content/order/settings, observer and fresh-cache selections matched the expanded control. Both XML hashes and normalized scanned error signatures also matched the preceding qualified `texturexml-e02-restored-main`. The experimental receipt is absent. Deployed source is `668d628`, and its actual DLL SHA-256 was rechecked as `cfb501d13b473abcc5ebf26b6ed6eb8a4c1026acb912a6b20938e7e3642eacb0`. The final check is restoration evidence, not another speed comparison. See `artifacts/texture-followup/restoration.json` and its checker/logs.

All **twenty** launches completed normally; no fixture game process remains alive. Main stays `3bde795`, and work remains on `codex/texture-loading-experiments`. No main merge, push, publication or normal-game data change occurred.

## 2026-09-07 PNG processing implementations

**Decision: retain processed-PNG caching; remove the finalization-only experiment.** Reusing finished PNG texture data produced repeatable complete-startup savings in the explicitly selected PNG workload. Merely removing a redundant final upload reduced a small inner cost but did not establish reliable complete-startup improvement. Neither result is a speedup for the normal expanded fixture's DDS selection, which executed zero PNG loads.

The owner authorized autonomous overnight investigation, implementation, live tests and integration of meaningful gains, followed by a separate task investigating the whole loading process. This work used the sole checkout on `codex/png-processing`, started from main `3bde795` with newer research records preserved. No normal game, Workshop, profile, save, cache or other application was modified. Overnight authorization replaced the preceding session's busy-PC lane restriction; these PNG comparisons nevertheless used the bounded XML-expanded lane. Windows file caches and unrelated system activity were not controlled or forcibly cleared.

### What was implemented and why it differs from previous trials

**Finalization:** pass the request to discard CPU-readable image data into `LoadImage`, then skip the later `Apply` on paths that do not need CPU compression. `Apply` transfers or finalizes changed texture data; the idea was to avoid repeating work already done by image decoding. CPU compression kept its original path. All 4,320 images exercised the proposed change in the trials. Verification replay matched 32 sampled textures, but following and preceding complete-startup controls erased a reliable benefit. The implementation and its `finalize`, `both` and `verify-finalize` selectors are removed from the retained source; experiments remain recoverable in Git.

**Processed cache:** store the texture's finished compressed blocks or uncompressed pixels, including its smaller image levels (mipmaps). Later launches read the current PNG to validate its full contents, validate the complete cached payload, and upload that finished data without decoding, regenerating mipmaps or recompressing the PNG. This removes image processing; the earlier read-ahead proposal only moved file reads, and the DDS pack retained already processed bytes behind costly validation. No individual mod name or fixture-specific content allowlist is involved.

The default retains DDS precedence. `--png-source original` explicitly chooses existing original PNG siblings consistently in control and cache arms without changing frozen files. That selection loaded **4,320 PNGs / 58,137,529 source bytes**. Native-selection control `png-n03-control` loaded **zero PNGs** and spent 855.121 ms in the texture-holder reload, reaching the menu at 20.029858 seconds. It demonstrates why the PNG result must remain a conditional workload result, not a claim that this existing DDS list becomes faster.

### Compatibility, freshness and resource limits

Only the nongeneric texture-reload callsite is redirected. Private copies of original holder, image and compression bodies retain ordinary publication and item order; generic game methods are never detoured. Main-thread loading state, exact game/Harmony identity, original method hashes and relevant published Harmony hooks constrain admission. Texture objects remain separate, with original formats, mip counts, sampler settings and readability behavior. Unsupported paths retain original loading. The retained cache admits the exercised Direct3D 11 compute-compression path; CPU compression, other graphics backends and compression-disabled settings fall back normally. This is pinned-fixture startup qualification, not arbitrary gameplay or a public platform-support claim.

Cache keys include full source-content SHA-256, path, pipeline version, graphics/Unity identity and compression settings. Each complete payload is independently hashed and checked against bounded mip dimensions and byte layout. Cache construction originally used managed SHA-256; after a measured slow restore, Windows CNG was tested as a faster implementation of **the same** hash. Tests compared empty, short, block-boundary and 1 MiB inputs against managed SHA-256 and checked that input bytes were unchanged. Real Mono receipts confirm native hashing was used, with about 0.17-0.20 seconds across current-source and cache hashing. There is no timestamp-only shortcut. See the [architecture and API references](../architecture.md#optional-processed-png-cache).

Compressed data is copied bit-for-bit from the native integer compression surface to a compatible float surface for GPU readback; there is no numeric conversion or replacement compression algorithm. Restore uses the original CPU texture format and checks the resulting GPU format before upload. Invalid cache entries fall back and can be rebuilt. Entries are capped at 8 MiB and the store at 512 MiB; capture checks the budget before expensive readback. A full cache preserves existing entries and stops adding data rather than endlessly processing uncachable misses. It has no eviction policy. The exercised complete cache occupied **336,043,884 bytes**, approximately **320.5 MiB**, versus 55.4 MiB of source PNGs.

### Debugging failures are not successful optimization tests

| Capture / source | Outcome and correction |
|---|---|
| `png-n01-control` / `86da2f4` | Hook refused with invalid generated IL; native game reached menu normally at 39.203118 s. Replacement used the wrong `LoadImage` argument count. `0a96710` corrected it and added replacement-signature checking. |
| `png-n02-control` / `0a96710` | Native process crash, exit 2147483651, no menu success. Graphics-device queries were made from the background mod constructor. `03aa611` moved them into admitted main-thread loading. |
| `png-p06-cache-build` / `03aa611` | Normal menu/exit, but 4,294 GPU-readback errors and only 26 cache writes. Direct readback of the integer compression surface was unsupported. This was an invalid cache trial. |
| `png-p07-cache-build` / `2f25e89` | Float-surface bit-copy fix produced all 4,320 entries with no errors; 40 rendering comparisons matched. Build was expensive: 43.992124 s menu, including 13.115 s readback. |
| `png-p08-cache-verify` / `2f25e89` | Normal menu/exit and 4,294 compressed hits, but 26 uncompressed entries failed construction and fell back, with graphics errors. Restoring through the original CPU texture format fixed this. Its roughly 4.64 s PNG time after subtracting verification also motivated exact native hashing; it is not a successful final cache comparison. |
| `png-p09-cache-build` / `f980143` | All 4,320 writes, zero errors/mismatches; 38.368054 s menu, 10.299 s readback and 0.200 s hashing. |
| `png-p10-cache-verify` / `f980143` | All 4,320 hits, zero errors/mismatches; 40 varied rendering comparisons. Menu 22.085688 s includes 0.805 s verification and is not a speed arm. |

### Matched performance comparisons

All rows below use original-PNG selection, startup searches, fresh supplier/application state except the explicitly restored PNG cache, the same seeded settings/content/order, independent menu observer and normal automatic exit. Source `f980143a250f44f50c95ef4cfc43297fdcdecbba` is identical across the repeated cache comparison. Verification is disabled in these timing rows. PNG timing includes current-source reading/hashing, payload validation and restoration; installation is also included in the complete menu endpoint.

| Capture | Operation | Menu seconds | PNG seconds | Installation ms |
|---|---|---:|---:|---:|
| `png-p11-control` | Original processing | 24.789912 | 4.255167 | 162.971 |
| `png-p12-cache-warm` | 4,320 cache hits | 21.217703 | 1.968745 | 244.084 |
| `png-p13-control` | Reverse original control | 23.967404 | 4.307470 | 168.025 |
| `png-p14-cache-warm` | 4,320 cache hits, repeat | 21.208260 | 1.982987 | 253.130 |
| `png-p15-finalize` | Finalization only, repeat | 24.040535 | 3.853795 | 167.524 |
| `png-p16-control` | Final reverse original | 24.287570 | 4.308465 | 165.110 |

Warm cache versus following original controls saved **2.749701 / 3.079310 seconds** to the menu, about **11.5% / 12.7% in these paired PNG-workload observations**. The PNG stages saved 2.339 / 2.325 seconds respectively. Do not add installation or nested stage timings to the menu result, combine this percentage with prior search percentages, or extrapolate it to arbitrary modlists. The two cache runs agreed closely and reverse controls retained the advantage, supporting integration.

Finalization's earlier series on `03aa611` was control **24.452052**, candidate **24.093083**, reverse control **24.052394** seconds; PNG costs were **4.273883 / 4.047119 / 4.271106** seconds. The later candidate was also slower than preceding `png-p13-control`, despite reducing PNG work. These trials support removing this small optimization rather than maintaining it without a reliable user-visible gain. The first original PNG control `png-p01-control` took **37.800120 s**, including **18.737865 s** PNG work; it is a first-read observation, not a comparable baseline for warmed candidates.

First construction is a real tradeoff. The successful `f980143` builder took **38.368054 s**, far slower than the roughly 24-second original controls, while warm reuse took about 21.2 s. It would take several unchanged repeat launches to repay that construction cost in this workload. Verification adds some work to the recorded build, so these runs do not establish an exact break-even launch count. Changing source/graphics identity or losing the cache incurs misses again. No first-launch speedup is claimed.

### Retained-build qualification and integration

The retained implementation is **`382996ca20863e2da88f7bb01758ecb9dacf3cbd`**. It removes the finalization experiment, adds pre-capture budget admission and repairable invalid entries, constrains caching to the exercised graphics path, and expands rendering checks to include all uncompressed entries up to a bounded additional 40 comparisons. Offline validation passed **56 managed and 24 Python checks**, with zero build warnings/errors. Checks cover original method identities/callsites, cache layout/corruption/rebuild, hash equivalence and fixture source/mode isolation. A final fallback-name correction was compiled by the package build after that full run.

Raw captures and source/package identities are preserved under `.rlo-test-instance/results/` and `artifacts/png-processing/`; the latter contains test/build/deployment logs, summaries, `run.py` and the bounded content/XML/error comparison `analyze.py`. Early failures remain visible and are excluded from successful behavior/performance claims. The final captured rows and exact merged/deployed state follow.

| Retained-source capture | Operation | Menu seconds | PNG seconds | Verification |
|---|---|---:|---:|---|
| `png-p17-integrated-build` | Fresh cache construction | 39.326689 | 20.291835 | 62 matches, including all 26 uncompressed images; 4,320 writes |
| `png-p18-integrated-verify` | Restored cache | 21.666366 | 2.859843 | 62 matches; 4,320 hits; 1.054980 s replay included |
| `png-p19-integrated-warm` | Warm timing | 21.171827 | 1.952552 | Replay off; 4,320 hits; 0.173290 s full hashing |
| `png-p20-integrated-control` | Reverse original timing | 24.311461 | 4.295445 | Replay off |
| `png-n04-integrated-native` | Normal DDS selection | 20.001110 | 0 | Zero PNG calls; startup behavior check, not a PNG speed claim |

The retained-source warm/control pair confirms **3.139634 seconds** shorter startup, approximately **12.9% for this paired PNG workload**. Its cache installation cost was 245.305 ms versus 159.454 ms in the control; both are included in menu time. First construction included 11.184875 s readback and 0.589144 s rendering replay. All five retained-source captures exited normally with code zero and `automaticTestPassed=true`, with zero cache/loader errors or rendering mismatches.

Across this investigation, **24 live launches** were captured: 23 normal automatic menu successes and the documented early native crash. Two normally completed development runs had cache errors and are explicitly invalid cache qualifications. Every successful PNG arm matched content/order, seeded settings, source selection, manifest, observer, captured original/patched XML hashes and normalized scanned error signatures except those two documented graphics-error runs. Cache restoration is the intentional application-state difference. The final native-selection capture additionally matches the preceding native control and qualified restoration's XML/error evidence. These are targeted startup/rendering checks, not a claim of broad gameplay validation.

The retained branch was fast-forward merged into local `main` after qualification. Built and deployed runtime source remains **`382996ca20863e2da88f7bb01758ecb9dacf3cbd`**, package `artifacts/op7-fixture-package/382996ca20863e2da88f7bb01758ecb9dacf3cbd`, DLL SHA-256 **`ef8e69ce0b5d13965be17cc7e73078487993c67c935761a4c3daa4b38575480a`**. Later documentation-only commits do not change that package identity. Final capture is `png-n04-integrated-native`; no fixture game process remains alive. `artifacts/png-processing/closeout.json` records the 24 exact captures and the final deployment/content check. No push or publication occurred.

The next separately created task owns the single checkout after this closeout. It is asked to begin with a physically absent baseline versus all retained optimizations enabled together, preserve normal source selection and matched cache conditions, then produce a thorough report on remaining generalizable loading costs and new improvement ideas. This PNG result must not be substituted for that whole-suite comparison.

## 2026-09-07 Whole-loading assessment

The [complete report](../whole-loading-assessment.md) compares physically absent Wake-Up with the full suite on the frozen representative workload, then investigates the entire interval through the independent menu repaint. It preserves native texture selection, matched forward/reverse controls, separate fresh/warm supplier caches, and every slower observation. This investigation completes the request recorded at the end of the PNG section above.

The accepted package could not fully activate through Loading Progress 0.14.0: Gagarin refused its unknown XML hooks, and the PNG interception missed its replacement iterator. A temporary exact-version compatibility bridge made those paths reachable. With that bridge, fresh-cache absent/suite/suite/absent times were **1091.945396 / 904.209762 / 902.617396 / 1087.359847 seconds**, supporting **17.19% / 16.99%** shorter startup in the two directions. Warm-cache times were **983.571816 / 870.314809 / 871.523316 / 974.296652 seconds**, supporting **11.51% / 10.55%**. Actual PNG interception selected 39,975 DDS files and zero PNGs; none of this is a processed-PNG cache saving.

The largest new finding was thousands of forced progress-repaint stops. An initial finishing trace recorded 2,481 early stops and 661.814 seconds between update scopes. A narrowly guarded prototype preserved normal loading calls and the existing 100 ms time budget while allowing more work between repaint stops. A verified diagnostic reached the menu in **241.949873 seconds**, with no early-stop returns, matching Gagarin parse/mapping/ownership checks and available XML/error evidence. Its longer trace located **64.053339 seconds**, including **61.656250 seconds of calling-thread CPU**, in the first observed menu-drawing call. Eligible shared type-search cost there remains unmeasured; the whole minute is not predicted savings.

Timing-only repaint candidates took **462.535953 / 240.438655 seconds**, compared with preceding suite **871.523316** and following suite **871.976522**. The slower candidate was active and passed startup checks but spent **186.679963 seconds** loading texture holders. Both candidates beat the stable suite controls, by **46.93% / 72.43%** for those comparisons; their variation prevents a single dependable percentage. The earlier control was not immediately adjacent. Foreground cadence, frame-sensitive loading hooks and arbitrary gameplay remain unqualified.

All **15 representative launches** exited normally, were captured and passed automatic menu testing. Available content/order/settings, observer, original/processed XML and same-cache-condition scanned errors matched. Comparison source **`56612c63b01fc02018f16525e46608e70ba5a893`**, DLL **`6df17da3ba67cecab0e0867d55ec77470e5f5aed2f97893add381efe9e07d52a`**, remains recoverable in Git and ignored artifacts. Its package passed 56 managed and 25 Python checks, with zero build warnings/errors. Initial preparation/test/compile failures were resolved before these comparisons and remain recorded.

This was a report deliverable. Removal **`8bedddc98ff29e52775f563b37dc5133dbac2518`** restores runtime source exactly to accepted **`382996ca20863e2da88f7bb01758ecb9dacf3cbd`** and removes the temporary bridge, probe and repaint selectors. The narrow fixture-file restoration action and its test are retained; all 25 Python checks passed after restoration. The existing accepted package was redeployed and its files verified, including DLL SHA-256 **`ef8e69ce0b5d13965be17cc7e73078487993c67c935761a4c3daa4b38575480a`**. Final small native-selection capture **`restored-whole-native`** reached the menu in **22.828391 seconds**, with normal automatic success and matching prior native content/settings/XML/errors. The game is closed. Restoring accepted source restores its known Loading Progress compatibility limitation too; the bridged-suite percentages are not a claim for that unmodified package.

The report ranks repaint qualification, late shared type-search eligibility, serial finite-name XML selection, type-index segment costs and native-hash DDS validation as distinct opportunities. Each has its own evidence, limits and stopping condition. Raw captures remain private under `.rlo-test-instance/results/`; analysis, source inspection, tests, helpers and the final identity/closed-process receipt are under ignored `artifacts/whole-loading/`. The report and restoration are integrated into local main without pushing or publishing.


## 2026-09-07 Further loading improvements

[The further investigation](../further-loading-improvements.md) retains optional Giddy-Up center-column texture readback. Final original/candidate pairs are 28.171518 / 27.658425 and 27.970068 / 27.517790 seconds: **0.45-0.51 seconds shorter startup (1.6-1.8%)** beyond accepted searches on one small selection. Complete offset calls save about 0.850 seconds per pair; this is not added to menu savings. All 1,359 offsets and every verified column alpha match, along with available XML and generated settings. Exact supplier/build/patch checks preserve the soft dependency. Physical absence and 73 managed / 30 fixture checks pass. Runtime/package `818c7efa7cd032ae209db95ae6f0fbc2385746a1` is retained locally.

The optional Combat Extended XML-worker prototype is removed: the final reverse pair saved only 0.027545 seconds despite earlier positive pairs. Reopening needs a new premise addressing its small complete-startup effect. Early Giddy-Up setup/thread/instruction-admission failures are excluded from candidate claims. The report preserves exact runs and recovery identities; no larger workload, push or publication occurred.

## 2026-09-07 Normal activation and final bounded investigations

The [complete report](../product-activation-and-final-opportunities.md) records normal settings-based activation, the exact development support policy and new focused gameplay coverage. Wake-Up-absent and normal-activation runs both created a small colony, advanced it, saved/reloaded matching map and colonist identities, and exited normally. Available loading data and scanned messages match; settings persistence also passed. This is qualification, not a new startup-speed percentage.

The recovered HugsLib filter avoided 1,766 patch-record reads, but the original report took only 181 ms within a 99-second cached startup on the selected 99-package workload. Its insufficient cost ceiling ended the trial before long timing pairs; prototype and selector were removed. This is not a claim that the larger historical case was disproved.

The final combined-build assessment found zero type searches beyond the existing cutoff and first-menu calls of 2.78/3.45 seconds. The warm run's foreground game inventory changed, so its timing is excluded from comparisons. Existing counters locate variable Giddy-Up readback and smaller initialization/XML costs without identifying a new safe general replacement. No further optimizer is retained. Runtime/package `4422f6222a54c365618e154de66ba88ee048ab6f` remains deployed only to the fixture; original exclusions restored, audit passed, game closed. No publication or normal game-data changes occurred.
