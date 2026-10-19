# Wake-Up 1.7 cloud loading investigation — 19 October 2026

**No useful, reliable loading improvement was established.** The credible shared
opportunities were reasonably exhausted for the available current-game workloads.
No 1.7 optimization is accepted. Development source now disables the unqualified
search features for both new and existing enabled profiles. Use native 1.7 loading.
This is not proof that every possible optimization or mod collection is exhausted.
Final Windows verification, played installation and binary/tag/Workshop release
remain with the owner.

## Target and evidence carried forward

The current public source parent is `6c5cc15743b4da4822f9c0021988de2ae1d5ed81`;
work continues on `codex/1.7-loading-reduction`, without private development ancestry.
Public metadata was checked at the start and again during this campaign; current
[branch metadata](https://steamdb.info/app/294100/depots/) and
[Linux depot metadata](https://steamdb.info/depot/294103/) corroborated unstable
build **26031547**, Linux depot **294103**, manifest **4415223985052641865**.
Main remains build 23969874. The prepared game is **1.7.4982 rev1280** with the
exact reviewed Harmony rc.3. All 2,911 canonical files passed size/hash/mode
verification. SteamDB is a public mirror; retained acquisition records establish
the original official base-depot provenance. DLC data came unchanged from the
owner's installation. Their metadata still labels 1.6 and the game reports
incompatible-version warnings, although actual activation and positive owned
definitions for all five DLCs were verified. This is not independently refreshed
1.7 DLC-depot qualification. Exact runtime/build admission remains intact.

The older [Windows comparison](qualification-1.7-20261014.md) does not qualify
current-game speed. It rejects unchanged leaf-only and XML-search reuse as
benefit candidates; warm Gagarin also skips the XML work. Type-name reuse remains
inactive on the current metadata-update-enabled runtime. Existing current-game
Linux evidence supports bounded functionality and actual member reuse, not speed.
No on-demand, progressive-detail or postponed gameplay work is included.

## First product question

Compare the existing member-only implementation with Wake-Up physically absent.
The candidate DLL is `8a091c431ada4dc824d6ec8fc22b785a632b61d5001a32a3edc1a36318bc637b`,
originally built at `9d346a0`; product/build/1.7-package source is unchanged at the
public parent. Controls and candidates share Harmony, Core, all five DLCs and the
same separate test bridge. No personal mod snapshot is available. This is an
explicit official-content baseline, not the owner's full collection.

One delegated worker owns the new game/fixture/controller; the parent owns
product changes, builds and Git. Game runs are sequential. Each run has a fresh
profile and application cache; OS file caches are warm. Both arms use four logical
processors, the same heap cap and scheduling priority on the same cloud machine.
No builds or heavy investigations overlap counted runs.

The bridge uses the same fixed random seed and 100-cell map, verifies actual world
seed/tile/colonists, and records native monotonic timestamps for startup idle,
new-game start and first Playing. Startup idle follows a second idle entry frame,
after the candidate's first-frame cleanup. It is not a visible menu repaint.
Whole launch-to-Playing is the primary screen; nested intervals are never added
to that total. Each successful run also requires 600 native ticks, save/reload
into a new game object, retained colonists and acknowledged normal exit zero.
Declared warmups precede four forward/reverse counted pairs. All failures remain.

## English member-only screen

Four balanced counted pairs completed, with separate declared warmups. All eight
counted runs passed actual activation/absence checks, identical world seed/tile/map
and package order, 600 ticks, native save/reload and acknowledged normal exit zero.
The candidate reused 28,207 member lookups with 1,403 native lookups; its other
features were confirmed off.

| Boundary | Wake-Up absent median (range) | Members-only median (range) |
| --- | --- | --- |
| Startup idle | 24.377 s (22.775–31.782) | 24.398 s (23.279–76.939) |
| New-game setup to Playing | 10.166 s (9.629–18.035) | 12.425 s (8.999–15.010) |
| Launch to Playing | 34.550 s (32.413–49.824) | 36.830 s (32.282–91.957) |

There were two candidate wins and two losses. The median total was 6.6% slower,
but cloud variation prevents treating that as a causal product regression or
benefit. A 91.957-second candidate coincided with 38.1% aggregate host CPU steal,
compared with 6.1–10.2% in earlier counted runs; its cause is not established and
it remains in the results. Startup scope counters do not establish speed. No
feature is accepted from this screen. Native phase diagnostics then bounded the
remaining work before any speculative optimization. Paired totals, in actual
forward/reverse execution order, were:

| Pair | Order | Absent launch-to-Playing | Members-only launch-to-Playing |
| --- | --- | --- | --- |
| 1 | Absent, members | 32.413 s | 34.983 s |
| 2 | Members, absent | 35.071 s | 32.282 s |
| 3 | Absent, members | 34.029 s | 91.957 s |
| 4 | Members, absent | 49.824 s | 38.677 s |

Save loading was checked functionally; it was not a matched save-load speed
benchmark. There is no save-load acceleration claim.

## Native work on a public content collection

A separately defined public collection was captured with the standard snapshot
workflow, then a dependency-closed child omitted Loading Progress. The full
snapshot is `rw-19b927f727156c0f`; the tested child is `rw-6cfd8345d7780a4d`.
It adds VEF at `32b2785dc1ddcec33650add585ef1066c2fea047` and Vanilla Psycasts
Expanded at `379b3235a4e91fa03d589af544fd8d5845d23690` to the official baseline.
All acquired Git files matched the immutable upstream blobs. This is a defined
public content workload, not the personal collection or a large-mod stress test.
Wake-Up was physically absent from the diagnostic runs. Actual 1.7 assemblies,
mod constructors and owned definitions were verified in-game.

Native verbose profiling and small private timing hooks are diagnostic overhead,
so these observations are not acceptance comparisons or additive saved-time
budgets. They show why another metadata-cache implementation is unwarranted:

- English: the selected consumers occupied 0.676 seconds of outermost thread
  intervals during a 52.609-second diagnostic startup. Those inclusive native
  bodies contain more work than a member cache could remove and can overlap
  across threads. Native `DoFieldSearch` handled 235,182 calls in 0.255 seconds.
- French: a final probe of every relevant native lookup site recorded 83,254
  `GetField`, 5,848 `GetFields` and 24,701 `GetProperty` calls, occupying 0.094222
  seconds in aggregate, including timing overhead. Translation application took
  2.7–3.3 seconds; metadata lookup is a small fraction of its required work.
- Static review confirms that current `XmlToObjectUtils` already owns field
  metadata, alias, custom-loader and postload caches. Native reflection and
  definition processing already avoid much of the work targeted by the preview.

Native phases identified larger costs in static constructors, content/texture
loading, audio callbacks and engine cleanup. No shared safe reduction follows
from a large required callback alone. Forced-GC suppression or moving work into
later gameplay is excluded. The non-power-of-two texture warnings do not prove
redecoding: current code logs before branching around that path for filesystem
sources. Waylith already consumes prefetched texture data and consults native
persistent caches. The old generic PNG pipeline remains rejected.

All three native diagnostics passed 600 ticks, save/reload into a new game object
and normal exit zero. Actual English and `French (Français)` were recorded.
Language changes the quicktest's generated word seed and tile, so cross-language
elapsed times are not comparisons. The existing headless 0×0 resolution error,
texture pixels/stub warnings, shutdown allocation diagnostics and three French
translation-data errors remain recorded. No graphics/audio playback or full
personal-collection qualification follows from these tests.

## Final bounded audio investigation

One further absent-Wake-Up diagnostic ran the actual MiniAudio backend on the
same public content child. Native path-loader bodies occupied 1.391 aggregate
seconds across 2,418 calls; memory-loader bodies occupied 2.257 seconds across
3,673 calls. Parent `AudioClip.Open` occupied 3.581 seconds. These inclusive,
possibly parallel intervals are not additive or removable wall-time budgets.
Only three repeated encoded identities occurred, costing about 3.49 milliseconds
across their six calls. The ten slowest bodies each took 7.2–10.7 milliseconds;
there was no concentrated expensive subset. Normalization analysis was not used,
and no stereo reopen was needed in this run.

The probe established a safe exact PCM roundtrip for 64 complete clips through
public APIs, with all sample hashes and checked metadata matching. It did not
establish a useful cache. Non-WAV inputs use native compressed-memory streaming
even when the managed requested `IsStream` flag is false. Actual memory resident
bytes matched the encoded 176.8 MB; the 2.664 GB figure was potential PCM capacity,
not audio already decoded at startup. Reading 92.7 MB of sample PCM took 1.409
seconds, including 1.093 seconds inside the sample accessor, which may materialize
decoded data. A PCM cache would change representation and potentially add startup
work and memory. Persistent read/write costs were not measured.

Separate outside-Open memory calls do not establish unused duplicate prefetch:
the registered prefetch producer itself invokes Open, and native embedded-bundle
and memory-download paths also exist. No matching unconsumed duplicate source
was demonstrated. Native prefetch transfers claimed objects and clears slots.
No audio cache or prefetch bypass was implemented. The probe completed 600 ticks,
save/reload and normal exit zero; no original sound was replaced.

## Product disposition and final verification

Product commit **`97e0ac6e7dbc46667c7ac63fd923cb79515e6ad3`** removes construction
of every 1.7 search feature and its session-lifetime hooks. The inactive settings
screen explains native loading and offers no activation toggles. The old settings
class, filename, keys, values and defaults are retained without migration; old
serialized true values cannot install hooks. Exact runtime, reference and method
contracts remain unchanged. Released 1.6 source and payload are untouched.

| Removed from runtime activation / rejected | Reason |
| --- | --- |
| Definition/XML search reuse | Prior matched stage loss; current native caches and small patch-application budget; warm Gagarin can skip it entirely. |
| Leaf-class reuse | Prior repeated loss, with no new measured premise for recovery. |
| Type-name reuse | Current metadata-update-enabled runtime correctly refuses it. |
| Loading-member reuse | Four current-game pairs show no reliable gain; actual remaining native work is small. |
| Translation-member reuse | All relevant lookup sites occupied only 0.094 seconds in the French probe. |
| Translation duplicate cache | Old dictionary mutation proof is invalid on .NET 10. |
| Generic texture/cache, parsed XML, broad parallelism | Earlier losses or current native ownership/caching; no new duplicate-work premise survived review. |
| Audio PCM cache / broad prefetch bypass | Compressed/file-backed native behavior already avoids work; no material duplicate source work demonstrated. |
| Deferred/progressive/on-demand work and GC suppression | Outside the requested objective; no such feature was added. |

Release compilation passed exact reference verification with **zero warnings and
errors**, followed by **67 C# synthetic adapter checks and four packaging checks**.
Those checks do not establish a loading benefit. The final DLL SHA-256 is
`85dedc743006b482019072493613e459fdb6cf3da308276b1b53504487d90966`.

A final functional-only game used this actual DLL, the public VEF/VPE child and
a legacy profile with **all six options true**. At startup, Playing and after
reload, the bridge verified the loaded DLL, the effective unchanged preferences
and **zero Wake-Up 1.7 Harmony owners**. The constructor reported deliberate
inactivity, not failed admission. The settings file hash stayed unchanged. Native
600-tick simulation, save/reload into a new game object, retained three colonists,
all five DLC definitions and acknowledged normal exit zero passed. Existing
headless resolution/warning diagnostics remain recorded. This inactive run is
not a speed comparison.

Across this task, 16 game runs completed normally: a preliminary control, two
declared warmups, eight counted samples, four native diagnostics and the final
inactive-product functional check. No forced termination is counted as success.
Windows, visible menu rendering, graphics/audio playback, an existing personal
save and the unavailable personal full/heavy collection remain unqualified.
The public content collection supports bounded compatibility and diagnosis, not
a large-mod performance claim. No binary, tag, Workshop upload or played setup
installation was performed.

## Source, evidence and usage

Public source branch: `codex/1.7-loading-reduction`, derived from public `1.7`
parent `6c5cc15743b4da4822f9c0021988de2ae1d5ed81`. Checkpoint `a226e0d` records
the first comparison; `97e0ac6` implements inactive native loading. Subsequent
commits update these findings without changing the tested runtime source.

Private evidence remains under ignored `artifacts/cloud-reduction-20261019/`:

- `screen/english-members-report.md` and `summary.json`: counted timings, variation,
  workload identity, controller and frozen bridge.
- `public-inputs/handoff.json` and `snapshots/`: full/subset provenance, immutable
  VEF/VPE identities and parent/child handoff.
- `native-diagnostic/`: English/French phase reports, exact lookup-site probe,
  language and warning/error inventories.
- `audio-probe/audio-report.md`, `summary.json` and
  `audio-native-contract-addendum.md`: positive API experiment and negative product
  decision, including limits of native-residency and timing evidence.
- `inactive-functional/` and `build-test-receipt.json`: final policy assertions,
  settings hashes, functional and build/check receipts; `decisions.txt`,
  `child-investigation-handoff.json` and `usage.json`:
  resume state, delegation and accounting limits.

Raw game/profile/save/run evidence is preserved in permanent ignored
`.rlo-test-instance/1.7/` lanes; existing fixtures were preserved. No private
material, third-party package or raw decompilation enters Git.

Subscription starting/ending balances, reset times and per-task token metadata
were unavailable through the exposed tools, including task metadata. A plan
percentage or monetary estimate cannot be derived responsibly. Two ordinary
subagents inherited the parent's settings and shared this prepared environment;
exact active model/reasoning settings were not exposed. The recorded task began
at **22:51:58 UTC** on 19 October. Final elapsed time is recorded in private
`usage.json`, including preparation, delegation, testing and reporting. Measured
aggregate owned game-process wall time was **834.079 seconds**, overlaps task
elapsed time and is not plan usage. No account-wide change is attributed to this
task, and no token-to-plan conversion is invented.
