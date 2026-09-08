# Historical development checkpoints

These records preserve earlier package identities and activation defaults. See [current state](current-state.md) for the current product.

**Completed further investigation, 2026-09-07:** [Giddy-Up texture preparation](further-loading-improvements.md) retains guarded center-column readback. Final forward/reverse pairs save **0.45-0.51 seconds (1.6-1.8%)**, beyond accepted searches on the small combat/races/genes selection plus Giddy-Up. All 1,359 readbacks and generated offsets match. The Combat Extended XML prototype is removed after negligible final reverse benefit.

| Current item | Verified state |
|---|---|
| Retained runtime / package | `818c7efa7cd032ae209db95ae6f0fbc2385746a1` |
| Deployed DLL SHA-256 | `32fd23aef7c88260c31bd1c47b56d13cade50f20a4cd915e6d88f262587084b0` |
| New activation | Candidate startup searches plus `--giddy-textures on`; default off; exact optional Giddy-Up 2.2.5.0 / Direct3D 11 contract |
| Verification | 73 managed and 30 fixture checks; matched pairs, pixel/offset verification and physical-absence qualification |
| Final state | `gu-f06-absent` passed and exited normally at 2026-09-08 00:00:15 UTC; original exclusions restored, audit passed, game closed; local integration only |

The checkpoints below retain historical package identities and independent measurements. Their retained features remain in the current package.

**Completed Character Editor reintegration, 2026-09-07:** [Optional preset lookup reuse](character-editor-reintegration.md) is restored following the owner’s expansion to permit guarded individual-mod improvements. On the sorted patch-heavy selection plus Character Editor, both testing orders improve: **39.204 to 29.245 seconds by the two-run medians, 25.40% shorter startup** with the existing searches already enabled. This is an incremental feature result on one workload, not a new universal average.

| Current item | Verified state |
|---|---|
| Retained runtime / package | `743ecabb5043d360698b90815895c5fbab7f95a5` |
| Deployed DLL SHA-256 | `d40a1c1e0152cb8c3865ec1fee007f838f10f0b7a870ed43e29049209c678a96` |
| New activation | Candidate `startup-searches` plus `--character-presets on`; default off; `timing` is the original preset path with matching receipts |
| Optional supplier | Exact Character Editor assembly 1.6.3.3, file/module/method identities and unmodified relevant patch chain; absent/changed suppliers retain ordinary behavior |
| Validation | 66 managed and 29 fixture checks, zero build warnings/errors; four matched timing runs, one final activation qualification and one physical-absence qualification |
| Correctness evidence | All 3,827 object and 40 turret presets preserve their stored-value hashes; actual reuse and zero retained entries after each scope |
| Final state | `ce-final-absent` completed and exited normally at 22:52:35 UTC; original fixture exclusions restored, final package deployed, game closed; integrated locally without publishing |

The checkpoint below preserves the preceding Loading Progress integration and its independent measurements. Its package table is historical, not the current deployed identity.

**Completed integration, 2026-09-07:** The [five-opportunity investigation](five-loading-opportunities.md) retains **Loading Progress repaint coalescing**, which avoids unnecessary display pauses while preserving the original loading time budget. The final stable-background industrial-list comparison is **8.15% faster with fresh application caches and 8.24% faster with restored Gagarin caches**, beyond the preceding accepted mod. All four forward/reverse pairs favor the new build. These are workload-specific incremental results, not a universal or absent-versus-mod average.

Later type-search activation had no eligible calls in four probes. Broader XML searches, static type-index reuse, faster-hash DDS caching and the separate Gagarin XML compatibility bridge did not establish useful repeatable whole-startup gains on the tested selections; their prototypes and selectors are removed. Earlier gaming comparisons and the corrected mislabeled idle series remain separately documented.

| Current item | Verified state |
|---|---|
| Retained runtime / built package | `6b1eed1d29de156609bd5d76b73313d3254dcd1f` under `artifacts/op7-fixture-package/` |
| Deployed DLL SHA-256 | `a65a2efa915adcc6af17adcc5cc0132845dc417757ee6198e1ae5401a580a9b6` |
| Activation | Candidate mode plus fixture `--loading-progress coalesce`; default off, exact optional Loading Progress 0.14.0 contract |
| Offline checks | 57 managed and 28 Python fixture checks; zero build warnings/errors |
| Final live checks | Eight scored runs and three qualifications, all normal automatic success with matching loading data; final optional-absence capture `five-final-q03-optional-absent` at 22:14:23 UTC |
| Workspace state | Final package deployed only to the fixture; game closed, batch complete; documentation-only closeout integrated locally, no push or publication |

The measurements and package table below preserve the preceding accepted package as historical controls. They do not replace this current package identity. At that historical checkpoint, installation alone did not activate features; ordinary settings activation is now implemented above.

**Measurement checkpoint, 2026-09-07:** The [retained-feature benchmark report](feature-benchmark-results.md) completes four smaller sorted modlists, two Loading Progress interactions and selected individual-feature comparisons while STALKER 2 runs. Fresh-cache suite reductions are UI 10.4%, industrial 1.2% (uncertain direction), patch-heavy 49.9%, and visual 4.8%; their secondary equally weighted mean is 16.6%, not a player-wide average. Definition searches alone save 48.2% on the patch-heavy list, type searches 13.0% on the UI list, and extra Gagarin reuse 2.0% on the cached patch-heavy list. Loading Progress adds overhead and makes the retained Gagarin feature fall back, but substantial fresh-search benefit survives. The original broader cohort's 95-entry mixed list remains held for sufficient memory (14 GiB admission); that comparison is unfinished. Character Editor and its dependent EyePatch remain excluded. Last capture `fb-a-g3-biocombat-suite` passed and exited normally; the fixture audit passed and no test batch is running. Runtime source/package remains `382996ca...`, with no new product feature. Work is recorded on `codex/feature-benchmarks`; see the [campaign method](feature-benchmark-campaign.md) and ignored checkpoint for continuation.

## Preceding whole-loading investigation

Updated 2026-09-07 after the [whole-loading assessment](whole-loading-assessment.md). The representative comparison found **17.0â€“17.2% shorter fresh-cache startup** and **10.5â€“11.5% shorter warm-cache startup**, using a temporary Loading Progress compatibility bridge. A separate repaint experiment reached the menu in 462.536 / 240.439 seconds versus stable controls around 872 seconds; the variable slower trial remains part of the finding. **The accepted runtime is restored; the bridge, repaint prototype and probes are removed.** These experimental percentages do not describe the currently deployed unmodified package.

The investigation started from main `ec2edce` on `codex/whole-loading-assessment`, with ownership of the single checkout and session-specific overnight launch authorization. Removal `8bedddc` restores runtime source exactly to `382996ca20863e2da88f7bb01758ecb9dacf3cbd`; experiment source `56612c6` remains recoverable. The report and restoration are fast-forward integrated into local `main` at closeout. The fixture is closed and no investigation continues in the background. This completed session's permission does not establish permanent permission for disruptive runs.

Processed-PNG caching remains retained from the preceding [PNG investigation](results.md#2026-09-07-png-processing-implementations). Its repeatable benefit applies to explicitly selected PNG-source workloads; first cache construction is slower and normal DDS selection provided no eligible PNG work here.

## What is retained

| Feature | Purpose | Activation |
|---|---|---|
| XML definition and template searches | Find literal `defName` definitions and `@Name` inheritance templates directly through temporary indexes in supported vanilla patch workers | Candidate mode with `startup-searches` or `def-lookup` |
| Harmony type searches | Reuse an index for Harmony's expensive fallback search for a type by name | Candidate mode with `startup-searches` or `type-lookup` |
| Gagarin XML reuse | Reuse XML that a supported active loading framework already parsed, preserving normal constructor callbacks and source ownership | Candidate `startup-searches` plus `--gagarin-cache on`; default off |
| Character Editor preset reuse | Avoid repeated turret/gun lookup construction while preserving original UI presets | Candidate startup searches plus `--character-presets on`; default off, exact optional supplier |
| Loading Progress repaint coalescing | Avoid extra early stops for display updates while preserving the native loading time budget | Candidate mode plus `--loading-progress coalesce`; default off; exact optional 0.14.0 contract |
| Giddy-Up riding-offset preparation | Read only the texture column used by the original riding-height calculation | Candidate startup searches plus `--giddy-textures on`; default off; exact optional supplier |
| Processed-PNG cache | Reuse validated finished texture data to avoid repeated PNG decoding, mip generation and compression | Candidate `startup-searches` plus `--png cache`; default off; supported Direct3D 11 compute-compression path |

Ordinary launches use the [in-game settings](user-guide.md). Searches and supported optional improvements default on; PNG caching defaults off. The activation column above lists the independent fixture controls, which override normal settings when explicitly supplied. Baseline mode records the relevant original path; absent mode physically removes RLO from game discovery. Unsupported game, supplier, query, graphics or patch conditions retain the original path. See [architecture](architecture.md) and [development](development.md).

PNG cache hits still read and hash the full current source and complete cached payload. Separate texture objects preserve original format, mip levels and sampler behavior. Storage is capped at 512 MiB with 8 MiB entries and capture stops before exceeding the budget. First construction requires GPU readback and can substantially lengthen startup. Changed sources/settings cause misses. The normal expanded source selection loaded zero PNGs; the measured PNG saving used the explicit qualification-only selection of existing PNG siblings in both arms. It does not convert textures, weaken source validation, make an individual mod mandatory or establish arbitrary gameplay compatibility.

The owner now permits individual-mod loading/UI preparation improvements behind safe optional compatibility checks. Character Editor preset reuse is reintegrated under that policy; the explicit CombatExtended XML worker remains removed. Earlier general cache infrastructure, broad diagnostics, early bootstrap rewriting, read-ahead readers, indexed/parallel XML prototypes and their obsolete selectors are absent. Those historical removals are distinct from the newly retained narrow processed-PNG cache. The accepted template lookup still uses the existing 11 vanilla workers, bounds and fallback; it adds no special mod hook or persistent XML cache.

## Preceding package checkpoint

| Item | Verified checkpoint |
|---|---|
| Retained runtime source | `382996ca20863e2da88f7bb01758ecb9dacf3cbd`; subsequent records do not change built source |
| Built and deployed package | `artifacts/op7-fixture-package/382996ca20863e2da88f7bb01758ecb9dacf3cbd` |
| Package contents | `About/About.xml` and `Assemblies/RimWorldLoadingOptimizer.RimWorld.dll`; no Core DLL or bundled supplier DLL |
| Runtime DLL SHA-256 | `ef8e69ce0b5d13965be17cc7e73078487993c67c935761a4c3daa4b38575480a` |
| Offline verification | Accepted package: 56 managed and 24 Python checks passed, builds zero warnings/errors; final fallback-name correction compiled in package build. Whole-loading comparison: 56 managed and 25 Python checks passed. Restored fixture tooling: all 25 Python checks passed; runtime source matches accepted exactly |
| PNG rendering checks | Retained build and restored-cache launches each compared 62 textures, including all 26 uncompressed images; zero mismatches/errors; restored launch had 4,320 hits |
| Warm retained-source capture | `png-p19-integrated-warm`: 21.171827 s menu versus following control 24.311461 s; 1.952552 s complete PNG processing, 4,320 hits, normal automatic success |
| Last captured native-selection run | `restored-whole-native`: 22.828391 s menu in `xml-expanded`, zero PNG calls, exit 0 and `automaticTestPassed=true`; captured 2026-09-07 10:24:40 UTC, matching prior native content/settings/XML/errors, deployed files verified; game closed |
| Preceding qualified package | `668d628a3b55bcc6c5a63470e9e80a3db5e5d61f`, DLL `cfb501d13b473abcc5ebf26b6ed6eb8a4c1026acb912a6b20938e7e3642eacb0`; remains recoverable, no longer deployed |

Source, built package, deployed fixture and captured runs are separate identities. Building does not deploy. Check `candidate.json` and the prepared run before any launch. The retained PNG qualification used searches with Gagarin reuse off; the latest native restoration check enabled Gagarin and is not a new timing comparison. The full representative study is now complete in the [whole-loading report](whole-loading-assessment.md).

**Known representative compatibility limit:** accepted Gagarin reuse safely refuses the frozen Loading Progress 0.14.0 XML hooks, and accepted PNG interception does not reach that framework's replacement content iterator. Merely enabling all switches does not activate every improvement there. Temporary bridge `56612c6` demonstrated actual activation, but selected 39,975 DDS files and no PNGs, so it did not qualify PNG rendering through Loading Progress. The accepted package remains supported only by its previously exercised paths.

### Previous texture experiments

The four preceding texture proposals were implemented and tested without a qualified gain. Scheduling, temporary GPU surface pooling and reduced atlas transfers passed exercised behavior checks but did not establish repeatable whole-startup savings. Packed DDS with full managed content validation was slower than its following warm controls. Experimental commits `84414eb` and `1975f08` remain recoverable; removal `a6aeb07` restored the then-qualified runtime and `tex-e17-restored-main` confirmed normal startup. See [those results](results.md#2026-09-07-texture-implementation-experiments). The newer PNG cache uses a different mechanism and exact native hashing; reopening any former route still requires a concrete premise addressing its recorded failure.

The preceding texture/XML assessment measured only 84.185 ms of expanded reflective field assignment and 20.358 ms of atlas layout; neither justified replacement. Its serial finite-name XML query planner was subsequently tested and removed without a repeatable useful gain in the five-opportunity investigation. The discarded Harmony emission approach is not an active next step. Other research branches and ignored raw evidence remain intact, with older consolidation recovery in the private [history bundle](archive/README.md#recovering-other-history).

### Discarded research and preserved evidence

Harmony emission research source `318b9fdce95c7b6e264d9b6a189279fec43fb65f`, its earlier source-only/backend commits and ignored packages/captures remain recoverable as historical evidence. The implementation and `--harmony-emitter` selector are no longer in the working source. For that removal, no new build, deployment or live run was needed because restored runtime/build/test/script inputs matched the already qualified source exactly. The subsequent delay investigation and final restoration are recorded separately below.

The experiment passed its focused correctness and startup checks but did not demonstrate a reliable overall loading-time improvement. Installation cost 272-291 ms against about 512 ms of measured stage saving. The rough 0.2-second / under-1% potential on a 30-second startup was an estimate, not a measured net gain. More generated replacement functions could increase absolute savings, but larger modlists also have more other loading work; no significant percentage scaling was established. Reopen only with concrete new evidence of worthwhile complete-loading benefit, not mod count or extrapolated stage savings alone. See [follow-up results](results.md#2026-09-06-early-loading-and-harmony-emission-follow-up).

The early probe in `8da23ec` and its temporary Prepatcher API reference were already removed in `4f5ef84`. No early bootstrap/cache or Harmony emission experiment remains active. The later flow probe (`8ea2a65`, bounded version `b2cf214`) was removed in `8f142ca`; its raw evidence and artifact helper remain under `artifacts/unexplained-loading/`.

Current documentation is divided by purpose: this state summary, [architecture](architecture.md), [development](development.md) and [results](results.md). Detailed historical reports have descriptive filenames in [the archive](archive/README.md), which retains original-path recovery references. Source is organized into startup, XML search, type search, Gagarin compatibility and shared compatibility checks. Obsolete product implementations, duplicate phase plans and temporary early probes are absent from the tracked tree.

## Evidence and remaining decisions

The [whole-loading assessment](whole-loading-assessment.md) adds 15 normal representative captures and a final accepted-package restoration check. Reducing unnecessary forced repaint stops is its strongest measured opportunity, subject to frame-sensitive compatibility and cadence qualification. It also locates about 64 seconds of late CPU work in the first menu-drawing call, after the existing type index shuts down; the new four-list probes found no eligible shared searches after the existing cutoff on their Character Editor-free selections. Ranked follow-ups separate actual costs from untested savings and prior failures. Raw evidence is under `artifacts/whole-loading/`. The only retained tooling change restores an inventoried runtime-written fixture file from verified frozen source bytes; it does not change normal Workshop files or refresh the manifest.

The [first investigation](results.md#2026-09-06-steward-investigation) measured 36.88% shorter startup for the reduced searches against warmed absent controls, and a separate 5.77% startup improvement for named-template lookup against nearby reduced controls. These are different comparisons on one frozen workload: do not add the percentages or treat them as universal gains. The historical roughly 50% combined result included the removed CharacterEditor optimization.

The [integrated package checks](results.md#2026-09-06-template-lookup-integration) qualified fresh startup, actual template execution and optional Gagarin cache-hit parsing/mapping/ownership. Those checks and the later restoration run support startup behavior on the pinned fixture, not arbitrary gameplay or another performance percentage.

The [second investigation](results.md#2026-09-06-second-loading-investigation) measured several new shared costs and tested a streaming cache writer. Small measured costs did not justify replacements; the implemented writer made its complete live save 60.3% slower and was removed. Exact timings, coverage limits, unresolved questions and experimental source/package identities are consolidated in results. Raw evidence remains under `artifacts/new-loading-investigation/` and the private fixture captures.

The [third investigation](results.md#2026-09-06-remaining-loading-investigation) measured previously uncovered Harmony and language/identifier costs. Patch-class construction totalled 165 ms, empty inner-patch preparation 99 ms, and all observed instruction decoding 224 ms; these overlapping costs did not justify guarded replacements or caches. Language/identifier stages were also small on the preserved English workload. Three probe captures passed normal automatic startup and independent evidence review. Raw evidence is under `artifacts/remaining-loading-investigation/`.

The [follow-up](results.md#2026-09-06-early-loading-and-harmony-emission-follow-up) reached earlier restarted loading: repeated assembly enumeration cost only 18â€“30 ms, and metadata initialization 247â€“333 ms. Those specific caches were not implemented. DDS header inspection found no uncompressed BGR files to benefit from removing that copy path. The Harmony experiment removed real shared work, but installation consumed much of its small-workload saving and complete startup was too variable to establish a benefit. The approach is discarded. Significant scaling with larger modlists remains unproven; it is not an active follow-up. First-read DDS behavior also remains unresolved. Raw evidence is under `artifacts/early-and-shared-loading/`.

The [unexplained-delay investigation](results.md#2026-09-06-unexplained-delay-investigation) traced main-thread update gaps, the finishing-callback queue and XML phases, alongside passive game/system resource counters. The main finishing drain took 4.33-4.41 seconds, mostly calling-thread CPU work; the largest gap outside updates was 0.33-0.35 seconds. At least 15.33 GiB of physical RAM remained available in the sampled expanded runs. No prolonged handoff or resource-pressure explanation appeared. The old slow interval did not recur during that pass, so its cause remained unknown. The initial probe passed 55 managed/23 fixture checks; the bounded revision passed two focused checks plus all 23 fixture checks. Small and three expanded traces passed normal automatic startup and independent XML/error review. The qualified package was then restored and verified in `flow-e04-restored-main`. This pass established no speedup and made no scheduling or callback-order change.

The [shared-startup cost investigation](results.md#2026-09-06-shared-startup-cost-investigation) reached Prepatcher's first-pass serialization without changing its modules. Expanded writing spent 1,603.800 ms on the game assembly and only 242.393 ms on other modified assemblies; unchanged assemblies already reuse their bytes. Only two XML lookups missed an otherwise usable name index. Another probe measured 186,834 unsuccessful bundle lookups taking 166.236 ms altogether. These ceilings did not justify parallel assembly writing, negative XML results or another bundle-name index. Native field/parser/folder caches were confirmed in source; within-sound audio reuse had no eligible repeated key in the captured content. Final probe removal is `aef1176`; qualified source and fixture are restored. Full measurements, the initial bundle-probe refusal, verification and untested conditions are recorded in results and `artifacts/shared-startup-costs/`.

The [texture/XML assessment](results.md#2026-09-06-comparative-texture-and-xml-assessment) traced DDS/PNG loading, asset ownership, atlas construction, XML ownership, generated conversion and query execution. Probe `d6d668c` instrumented all eight field-store emission sites with initially empty parser caches and observed actual generated assignments; removal `9e7a6dd` restored qualified source. Atlas rendering contains much more work than layout, but there is no demonstrated broadly safe removal of that work. A finite-name query plan avoids repeated predicate checks rather than repeating rejected parallel splitting. Its proposed eligibility is limited to ordinary Replace/Remove selection; Add's live iterator semantics remain outside the first experiment. No new implementation or speed claim is retained. Raw evidence is under `artifacts/texture-xml-assessment/`.

The [texture implementation investigation](results.md#2026-09-07-texture-implementation-experiments) includes separate small/expanded atlas replay checks, two timing-only runs for each GPU/scheduling change, and pack construction plus two fully exercised warm-cache comparisons. All four atlas verification runs matched their original-path rendered output; packed byte ranges matched all 4,320 sources. The warm pack still paid for source validation and pack integrity, costing more overall. Atlas bake differences overlapped controls; faster DDS stages also appeared without the scheduling change. Experimental code/selectors are removed, with source and raw evidence preserved in Git and `artifacts/texture-followup/`.

Future investigation remains open-ended. Read the [recorded decisions](results.md#experiments-that-did-not-justify-promotion) before revisiting related routes; confirmed failures, inconclusive measurements and untested conditions remain distinct. The validated-pack design needs a materially different way to remove its measured validation cost before reopening. Quiet-host and larger-workload texture behavior remains untested. Large XML representation rewrites and other persistent asset cache designs remain unproven, not universally disproved.

The historical Windows MVP phase, OP7, remains incomplete; the next phase, OP8, is inactive. The qualified development target remains the exact pinned GOG game and frozen dependencies. Type lookup can admit a known historical Steam identity, while XML lookup and Gagarin require GOG; that admission is not evidence of current Steam or reduced-product qualification. Normal user activation, an exact development support policy and focused new-colony/save-reload coverage are now implemented. Broader supported-game/dependency coverage, prolonged gameplay qualification and the previously approved licensing work remain unfinished. This is not a release-ready or cross-platform-qualified package.
