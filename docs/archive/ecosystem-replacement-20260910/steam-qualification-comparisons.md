# Steam qualification and loading comparisons — 10 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent steward review

Handoff `8db94540b097ed116cf56f9357333770a400e92d` is accepted for the exact fixture controls, bounded functional evidence and repeated startup comparison. Independent read-only control review found no material blocker. The steward verified every raw comparison run's hash, normal automatic pass, actual menu timestamp, performance purpose, absence of gameplay/cache-restoration flags and matching content order after removing Wake-Up; recomputed all four groups' means, medians, ranges and sample deviations agree with this report. This fulfills the requested repeated startup figures for the disclosed workload/cache condition, including the small default regression and optional-bundle variation.

This does not accept ordinary scene-entry compatibility or release readiness. The next task will test an actual normal screen-driven colony/load path without the automated gameplay probe or logging suppression, investigate a demonstrated fixture/bootstrap cause where useful, and prepare the truthful local package. Existing workaround evidence remains useful but cannot substitute for the missing ordinary path. No production source change, public diagnostic suppression or publication follows from this review.

## Result and handoff

The retained candidate runs on the isolated installed Steam version. Native world
generation, native texture preparation and later-startup consumption passed.
Save/reload and real minimized holds passed **with the explicit private logging
workaround**. Ordinary scene entry stalled with both Wake-Up present and absent;
it is not qualified as production gameplay. No shipped runtime changed.

Four measured runs per arm on the same French mixed workload gave mean menu
times of **153.600 s absent**, **68.258 s public 0.2.1**, **68.634 s new defaults**,
and **67.164 s new with translation/routing enabled**. New defaults were 55.32%
shorter than absence but 0.55% slower than public 0.2.1. The optional bundle was
56.27% shorter than absence and 1.60% shorter than public 0.2.1. These are bounded
fresh-application-cache results, not a universal average or a new default win.

This is a handoff for parent review, not self-acceptance, release readiness or
publication. The retained-features release scope and its omissions remain intact.

## Source, package and isolated game identities

Dispatch `wake-up-steam-comparisons-f824947` started from exact clean
`f824947f4ab21acbf889ec7279414f26615b3378`, with no RimWorld process running,
on `codex/steam-comparisons-f824947` in the sole checkout. Two bounded researchers
worked read-only; one also reviewed the fixture transition. Only this owner
edited, built the private observer, deployed and launched.
SHA-256 values below are file fingerprints that distinguish exact bytes;
source revisions identify code history, not proof that a particular run used it.

| Layer | Exact identity |
|---|---|
| Retained core source | `d584eb36aa68cd374a26621f97295dbb63626a33` |
| Retained core DLL SHA-256 | `59ed42a69895930e8c5cfe04c903005f925cdf063beb02ffe6329244faac8825` |
| Public 0.2.1 source | `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f` |
| Public 0.2.1 DLL SHA-256 | `47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d` |
| Final private observer source | `41f50d5019950ec678131f138cff3d520320d776` |
| Final private observer DLL SHA-256 | `2e5433c950f3f7429b118f7f04fcdf050bb786c4803bccc782f7f251e8fdf4b8` |
| Exact Steam managed game SHA-256 | `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |
| Steam generation | `steam-rev590-20260910-020604-cb37ef1b` |
| Steam manifest SHA-256 | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93` |
| Game switch recovery transaction | `20260910-020635-a5abd2ff` |
| Preserved GOG generation / manifest | `20260904-182829-e058641c` / `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d` |

The installed `Version.txt` says **1.6.4871 rev590**; the player reports
**1.6.4871 rev591**. The existing exact assembly contract remains `steam-rev590`.
The complete 2,087-file, 968,993,794-byte game copy includes official content,
managed dependencies, native engine and existing bootstrap files. Normal Steam
Mods were excluded; the fixture's frozen mods stayed unchanged. Source and
destination were independently hashed. Old GOG files, profile, observer and
pointers remain in the existing recovery journal; old captures/snapshots were
not rewritten. This is one active fixture, not another installation to launch.
The inherited discovery fields describe the original mod snapshot; the new
transition and Steam fields describe the game switch.

Both comparison DLLs remain byte-identical to their original GOG-built packages.
The public release's 13 manifest-listed files were verified before projecting
its unchanged DLL into the fixture's two-file private package. The same private
About metadata is used for old and new. Original receipt and metadata hashes,
true source revision, GOG build reference and exact Steam execution contract
remain separate. This binding admits a test; it does not confer qualification.
The ordinary deployment guard and operation lock remain in force.

[Identity record](../../../artifacts/steam-qualification-20260910/identities.json),
[functional run records](../../../artifacts/steam-qualification-20260910/functional-runs.json)
and [comparison run records](../../../artifacts/steam-qualification-20260910/comparison-runs.json)
contain full package inventories, observer hashes, deployment transactions,
run UUIDs, source/capture hashes and exact content order.

## Steam behavior and failures

Independent inspection found 95 of 106 managed DLLs identical to GOG and 11
changed. Seventeen relevant native classes decompile identically, including
save/world lifetimes, leaf searches, translation and loading-display targets.
Live admission remained authoritative; no production hook check was weakened.
The Steam and GOG executable, Unity player, Mono runtime and InputLegacy module
are identical. [Research and exact comparisons](../../../artifacts/steam-comparison-research/findings.md).

| Functional capture | Result |
|---|---|
| `stq-native-scene-01` | Game reached menu, but the private observer's old GOG-only module check refused Steam. No gameplay probe ran. Manual Quit to OS, exit 0; failed automatic/gameplay qualification. |
| `stq-native-scene-02` | Corrected observer; ordinary stack traces unchanged. Menu passed, scene entry stalled at Loading assets 90%. Preserved evidence, verified fixture-only forced stop; failed. |
| `stq-native-absent-03` | Wake-Up physically absent, same ordinary stack policy. Same scene-entry stall and busy CPU pattern; preserved evidence and verified fixture-only forced stop. Failed. |
| `stq-save-workaround-04` | Explicit private suppression. Native 75×75 map, three colonists, ticking, save and paused/unpaused reload checks passed; normal automatic exit. |
| `stq-background-hold-05` | Restored only the hash-verified fixture-created save from 04. Two real minimized loads/holds plus a changed-current-preference load passed; normal automatic exit. |
| `stq-native-world-06` | **Ordinary stack traces unchanged.** Two actual native 30% world-page generations passed, including minimized completion, current preference restoration and display coexistence; normal automatic exit. |
| `stq-native-ui-07` | Manual public preparation window: 698 selected, one native JPEG prepared, 697 authored DDS skipped, zero failures. Settings/window readable, normal Quit to OS and capture. |
| `stq-prepared-consume-08` | Restored that exact Steam-created prepared entry. Startup consumed 1024×1024, seven-mip DXT5 output; display layout executed; normal automatic exit. |
| `stq-mixed-opt-in-09` | Mixed French workload; translation and routing activation/output checks passed, retained suppliers executed; normal automatic exit. |
| `stq-mixed-absent-10` | Matching native French control passed; the 119-byte sampled translation output exactly matched 09. Normal automatic exit. |

The observer correction adds the exact already-reviewed Steam module identifier;
it does not broadly accept unknown games. A separately recorded functional-only
`--native-stack-traces` option permits testing without the private workaround.
All 48 Python checks passed, including switch/rollback preservation and rejection
of the scene control in performance runs. The final private observer built with
zero warnings/errors. Existing adequate managed product checks are retained;
there was no production change requiring their wholesale repetition.

The two unsuppressed scene failures reproduce the earlier frozen fixture's
symptom and busy-loop pattern on the identical engine. They are consistent with
the prior evidenced empty-stack formatter loop; the native instruction-level
diagnosis was not repeated. Wake-Up absence rules out this candidate as the sole
cause. A stack trace lists the program calls leading to an error. The private
workaround suppresses Exception/Error stack formatting only
in selected gameplay probes and retains error messages. It ships in no core
package. Neither the workaround-assisted pass nor successful startup establishes
ordinary Steam colony-entry compatibility or repairs Unity/Prepatcher. Earlier
root-lookup/input-preload routes were not retried as fixes.

In 05, the paused load completed at tick 1203 (one native initialization tick)
and held Paused for 16.324 seconds through focus return. The unpaused load
completed at tick 1203 with native Normal speed and zero ordinary completion
ticks, then held that tick for 12.679 seconds until restore. Both actual engine/background
preferences were false after completion; changed-current-preference restoration
also passed. In 06, the minimized world finished in Entry with the next native
page open, then held false preference for 14.204 seconds before restore; the
second world finished with the current preference true. These are functional
endpoints, not menu-speed measurements or all-mod gameplay claims.

The mixed workload recorded 6,268 object and 47 turret presets with zero fallback
and no retained lookup entries; Giddy-Up recorded 1,380 calls/hits with zero
errors/fallback/retained textures. Texture routing returned the same native
winner, with two observed reuse hits. The optional translation pass executed
680 package scopes and 27,939 lookups with no native fallback or retained
lookup entries. Loading Progress coalescing
installed; its XML hook caused Wake-Up's Gagarin parsed-XML assistance to refuse
safely, while Gagarin itself remained installed and built its ordinary cache.
The separate loading display was tested on the small non-Loading-Progress lane.
The Steam colony/save probe separately recorded 125 avoided full leaf-type
scans during each alert-interface construction, with eight native predicates
and zero retained types. This is first-use execution evidence, not menu gain.

## Comparison design and complete results

The workload is the existing mixed 99-package frozen selection, comprising
93 mod/bootstrap packages and Core plus five official expansions. Absent runs
load 100 packages including the observer; old/new load 101 including Wake-Up.
Character Editor was restored for this selection; Modern Dev Tools and the
previously excluded Character Editor facial-animation extension remain parked.
The selection's other existing omissions and full order are in
[mixed-99.json](../../../artifacts/steam-qualification-20260910/mixed-99.json).
The workload naturally exercises the retained suppliers and French translation;
no authored DDS was hidden or replaced with artificial PNG work.
The native and optimized functional controls both report 414 French
translation-data errors in this existing selection. The matching sample is a
bounded output check, not a claim that this modlist has complete translations.

All arms use **French (Français)**, native texture selection, texture compression,
the same frozen ordinary settings and automatic observer exit. The prepared
preferences use automatic resolution (`0×0`), windowed mode, UI scale 1.75 and
background execution enabled for observation. Captures report Direct3D 11 on
RTX 4080 SUPER, driver 32.0.16.1664, Unity 2022.3.35f1. This is default Wake-Up
behavior on a French workload, not a default-English benchmark.

| Arm | Wake-Up behavior |
|---|---|
| A | Physically absent; all other selected content/dependencies retained |
| B | Exact public pre-revamp 0.2.1, ordinary defaults via user activation |
| C | Final retained candidate, ordinary defaults via user activation |
| D | Same candidate/defaults plus faster translation application and texture-supplier routing |

Every run starts from the frozen profile seed with fresh Wake-Up/Gagarin and
other generated application caches; no cache-restore flags are selected. PNG
caching, prepared texture reuse, loading display and background-loading options
remain off in the measured arms. Windows file caches are left unmanaged, never
flushed. One complete excluded warmup per arm precedes measurements. Sequence:
warmup A-B-C-D, then A-B-C-D / D-C-B-A / A-B-C-D / D-C-B-A. All RimWorld processes
must be stopped before each performance launch; no parallel builds or substantial
research ran during measurement. Gameplay probes and private suppression are
absent from all timing arms.

All 20 startup captures passed normal automatic exit. The four warmups below are excluded by design; the 16 measured runs have no exclusions. Earlier failed functional scene/observer probes are not startup measurements.

| Excluded warmup | Arm | Menu seconds |
|---|---|---:|
| `stc-00-a` | absent | 151.932326 |
| `stc-00-b` | old-default | 67.965676 |
| `stc-00-c` | new-default | 68.457748 |
| `stc-00-d` | new-opt-in | 66.841654 |

| Measured run, execution order | Arm | Menu seconds |
|---|---|---:|
| `stc-01-a` | absent | 152.962832 |
| `stc-01-b` | old-default | 68.679883 |
| `stc-01-c` | new-default | 68.888807 |
| `stc-01-d` | new-opt-in | 66.982270 |
| `stc-02-d` | new-opt-in | 68.293743 |
| `stc-02-c` | new-default | 68.755258 |
| `stc-02-b` | old-default | 67.925063 |
| `stc-02-a` | absent | 153.330874 |
| `stc-03-a` | absent | 155.095911 |
| `stc-03-b` | old-default | 68.543496 |
| `stc-03-c` | new-default | 68.660602 |
| `stc-03-d` | new-opt-in | 66.542664 |
| `stc-04-d` | new-opt-in | 66.837300 |
| `stc-04-c` | new-default | 68.231349 |
| `stc-04-b` | old-default | 67.884472 |
| `stc-04-a` | absent | 153.009959 |

| Arm | N | Mean s | Median s | Range s | Sample SD s | Change vs absent | Change vs public 0.2.1 |
|---|---:|---:|---:|---|---:|---:|---:|
| absent | 4 | 153.600 | 153.170 | 152.963–155.096 | 1.011 | — | — |
| old-default | 4 | 68.258 | 68.234 | 67.884–68.680 | 0.412 | — | — |
| new-default | 4 | 68.634 | 68.708 | 68.231–68.889 | 0.284 | -55.32% | +0.55% |
| new-opt-in | 4 | 67.164 | 66.910 | 66.543–68.294 | 0.775 | -56.27% | -1.60% |

Statistics use only valid captured measured runs with normal exit 0 and
`automaticTestPassed=true`. The endpoint is
`menuReadyObservation.elapsedSeconds`, the first completed menu repaint, not
process lifetime or summed overlapping stages. Percentage change is
`100 × (new mean / reference mean − 1)`; negative means shorter loading time.
Range and sample standard deviation show within-series variation. New defaults
were 0.376 s slower than public 0.2.1 on average; all four paired differences
were positive (0.117–0.830 s), while their overall run ranges overlap. This small
measured regression is disclosed; no production correctness defect was found
and no retained capability was removed to erase it. The optional bundle saved
1.470 s versus new defaults in the mean and was faster in each matched block.
Versus public 0.2.1 it saved 1.094 s in the mean but was slower in one of four
blocks; that variability limits the strength of a small whole-startup claim. D is an
optional-settings bundle, not an isolated routing-feature measurement. Fresh
application caches do not imply cold Windows file caches or predict later
Gagarin/texture-cache-hit launches.

## Release limits and ownership

Retain every scope/omission in [retained-features.md](retained-features.md).
On-demand assets, new-colony/standalone-map background admission, lossy texture
presets and the deferred Linux helper are not delivered by this qualification.
There is no universal ecosystem-replacement or all-modlist speed claim. The
one-JPEG preparation check establishes behavior, not a useful menu saving.
Earlier 215-versus-67-second confounded runs and translation-only stage figures
are not incorporated into these aggregates. GOG results remain separate.

Independent read-only review checked all 20 actual timing receipts and their
summary hashes, execution order, settings/cache controls, exact packages and
recomputed statistics. It also checked the functional receipts and translation
sample comparison; no evidence blocker remained. All 48 Python fixture checks
passed and the final private observer built without warnings or errors. The
accepted production DLL was not rebuilt or changed.

The original three fixture exclusions are restored. The unchanged retained
Steam-bound package and final observer remain deployed. Small activation-lane
profile `stq-handoff-prepared-11`, transaction `20260910-030201-493cd232`, is
verified offline with ordinary user defaults and is **not launched**. It is not
one of the functional captures or measured runs. The Steam generation remains
active; the original GOG generation and game bytes remain recoverable.
Final read-only hashing verified all 2,087 normal Steam source game files
unchanged and all 2,135 original GOG game files intact in the recovery journal;
both manifest hashes still match. See the
[handoff verification](../../../artifacts/steam-qualification-20260910/handoff-verification.json).

All team writes, builds and launches stop at this handoff, with no RimWorld
process alive; both read-only subagents are finished. Exclusive checkout
ownership returns to the parent steward. Normal installations/data, Deck,
security settings and publication remain untouched. Steward review and final
local packaging/readiness disposition are next; this report does not accept
itself or turn the scene-entry limitation into a production qualification.
