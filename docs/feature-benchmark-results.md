# Retained-feature benchmark results

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.

> Dated qualification record. Identities, activation defaults, permissions and proposed next steps describe this investigation, not the current release. See [current state](current-state.md), [support policy](user-guide.md#supported-build-policy) and [results](results.md) for current decisions.

**Smaller-list measurements complete, 2026-09-07; larger mixed-list comparison held. Yes, the current general-purpose mod can approximately halve startup on a suitable workload:** the completed patch-heavy fresh-cache comparison is **77.7 → 39.0 seconds, 49.9% shorter**, with all three pairs between 49% and 51%. Character Editor and Loading Progress are absent. Across all four completed smaller lists, the equally weighted fresh-cache reduction is **16.6%**, with individual list estimates from 1.2% to 49.9%. That describes these selected lists under concurrent gaming, not a player-wide average. The originally planned broader comparison still lacks its larger mixed list.

The completed scored results below were collected while STALKER 2 ran on the same machine. A brief quieter interval produced qualification launches only; none contribute to these percentages. Only the fixture process has below-normal CPU priority. The machine has a Ryzen 9 7950X3D, approximately 32 GiB RAM (31.1 GiB OS-reported usable physical memory), RTX 4080 SUPER, and Windows 11 build 26200. There is no adjustment that pretends to remove competing CPU, GPU or memory use from the observed times.

## What is being measured

Every scored time ends when the independent observer sees the usable menu. Each run then exits normally and is captured with automatic startup checks passed. The optimizer is physically absent in the baseline. Character Editor and its dependent EyePatch are excluded throughout. Gagarin itself remains present in both arms.

“Wake-Up” or “suite” here means the retained search optimizations, Gagarin reuse and PNG cache are explicitly enabled through the fixture launch configuration. This campaign used explicit fixture selectors; ordinary activation through settings was implemented later. Unsupported conditions retain the original path, and a feature with no eligible work contributes no demonstrated saving.

“Fresh” means the application caches are reset; it does not mean Windows's file cache is cold. “Warm” means the matching captured Gagarin cache is restored, and its actual reuse is confirmed. First qualification launches are excluded from scored comparisons. The industrial baseline's first qualification took 99.55 seconds, whereas scored fresh baselines took approximately 29–31 seconds. Comparing that first launch with a later optimized run would produce a misleading large gain.

The accepted runtime is `382996ca20863e2da88f7bb01758ecb9dacf3cbd`, DLL SHA-256 `ef8e69ce0b5d13965be17cc7e73078487993c67c935761a4c3daa4b38575480a`. This campaign changes benchmark tooling and documentation, not runtime features. The earlier experimental Loading Progress bridge and repaint changes are absent. See the [campaign method and selection rules](feature-benchmark-campaign.md).

The private plot is retained at `artifacts/feature-benchmarks/paired-startup-results.png`; the tables below contain the published measurements.

## Completed smaller-list comparison at a glance

All rows below have Loading Progress off. Times are separate medians; the sections below retain the paired results and explain uncertainty. The cache conditions must stay separate because an existing Gagarin cache already skips work that Wake-Up speeds up on a fresh launch.

| Enabled collection | Fresh: absent → Wake-Up | Fresh reduction | Warm: absent → Wake-Up | Warm interpretation |
|---|---:|---:|---:|---|
| UI / quality of life | 29.29 → 26.25 s | 10.4% | 37.86 → 25.93 s | Favorable, but magnitude changed across the foreground-game restart |
| Industry / construction | 30.32 → 29.95 s | 1.2% | 28.90 → 28.17 s | No dependable saving; paired directions disagree |
| Combat / races / genes | 77.71 → 38.97 s | 49.9% | 32.64 → 31.97 s | Repeatable small additional saving, 2.1% |
| Visual / biome content | 26.41 → 25.13 s | 4.8% | 23.72 → 24.03 s | No saving; four of five pairs slightly slower |

These are deliberately varied collections, not a random sample of players' lists. The enabled-entry counts include common prerequisites and official content; the full installed frozen library is shared. Passing dependency/order checks and startup comparisons qualifies the tested loading behavior, not every gameplay interaction.

## Industrial list: completed comparison

This list contains industrial systems, construction/storage content and music: 30 frozen entries, or 31 with Loading Progress, before adding the observer and any optimizer. Each row has five paired repetitions; the extra two pairs were triggered by the preregistered variation rule.

Positive reductions mean faster with Wake-Up; negative reductions mean slower. The median is the middle observed time. The percentage based on separate medians is shown alongside adjacent paired results because a small positive median difference can hide pairs that disagree.

| Cache | Loading Progress | Absent median s | Wake-Up median s | Reduction from medians | Individual paired reductions |
|---|---|---:|---:|---:|---|
| Fresh | Off | 30.325 | 29.952 | 1.2% | +2.4%, -2.0%, +0.2%, -3.3%, -6.3% |
| Fresh | On | 35.138 | 33.965 | 3.3% | +3.3%, -1.7%, -2.8%, +1.0%, +1.7% |
| Warm | Off | 28.902 | 28.174 | 2.5% | -5.1%, -2.8%, +11.1%, +2.4%, -7.7% |
| Warm | On | 31.790 | 34.606 | -8.9% | -10.4%, -7.5%, -2.3%, +6.1%, -21.5% |

The fresh differences are small and change direction. Warm Loading Progress-on runs were slower with Wake-Up in four of five pairs, but the size varies widely. These observations neither establish a dependable saving nor support a precise universal regression percentage. They do not support halving startup on this list or claiming a consistent 10–20% reduction.

Loading Progress increased the absent median by **4.814 seconds fresh** and **2.888 seconds warm**. With Wake-Up present, it increased the median by **4.013 seconds fresh** and **6.432 seconds warm**. The difference in Wake-Up's saving between the LP-on and LP-off environments is therefore **+0.801 seconds fresh** and **-3.544 seconds warm**. These are descriptive interactions in this list under varying background load, not proof that the compatibility limitation alone caused every second of the difference. The minute-scale overhead from the earlier much larger workload has not been reproduced here.

Feature receipts show why no large gain should be presumed. A fresh industrial candidate recorded only 213 definition/template hits and three indexed type lookups; its entire XML patch stage was approximately 0.65 seconds. Warm supplier reuse skipped that patch work, leaving zero definition-index queries. Without Loading Progress, our Gagarin feature actually reused its parsed document and skipped the extra serialization/parse. With Loading Progress, the accepted feature refused its unknown replacement hook and retained fallback behavior. PNG calls were zero: this native texture selection offers no measured processed-PNG cache benefit.

All industrial scored captures match the available original/processed XML, seeded settings, content order, observer identity, exclusions and normalized scanned-message checks against their corresponding absent reference. This qualifies the exercised loading behavior, not arbitrary gameplay compatibility. Exact runs, receipts, resource samples and comparisons are in ignored `artifacts/feature-benchmarks/analysis.json`, `stats.json`, `runs/`, and `.rlo-test-instance/results/`.

## UI list: completed gaming comparison

This 36-entry quality-of-life/UI selection has relatively little added art or definition data. Three paired fresh-cache comparisons completed without triggering the extension rule. Absent times were **29.467966 / 28.748980 / 29.294086 seconds**; corresponding suite times were **26.254400 / 26.843549 / 25.616138 seconds**. All pairs favored Wake-Up, by **10.91% / 6.63% / 12.56%**. Separate medians were **29.294086 → 26.254400 seconds**, saving **3.039686 seconds / 10.38%**.

Available memory stayed above 6 GiB during the fresh scored runs. Their available loading-data checks match. The suite recorded 103 definition/template hits, 36 indexed type lookups and zero PNG calls. The later isolated-feature comparison below identifies type searches as the useful feature on this list; the search counts alone would not establish that.

STALKER exited around the original third warm pair. Its **28.064541 → 18.944986 second** result is retained as a workload-transition observation, excluded from the gaming comparison rather than credited as a 32.5% gain. STALKER then restarted. A replacement pair and the fixed two-pair extension completed, with game presence checked before and after each capture. All five comparable pairs favored Wake-Up, but their magnitudes differ substantially across the restart:

| Foreground session | Absent seconds, in paired order | Wake-Up seconds | Paired reductions |
|---|---|---|---|
| Before restart | 29.309721, 29.737930 | 25.934159, 25.990994 | 11.52%, 12.60% |
| After restart | 52.008583, 48.464075, 37.860998 | 25.675479, 27.710821, 24.618691 | 50.63%, 42.82%, 34.98% |

The aggregate five-pair medians are **37.860998 → 25.934159 seconds (31.50%)**, but this mixes two foreground sessions and is not a stable headline estimate. Session medians imply **12.06%** before versus **47.02%** after; there are only two and three pairs respectively, and the post-restart baselines still vary substantially. The honest result is a consistently favorable direction with strongly environment-dependent magnitude. One retained-package pair did halve observed startup, with nearby reverse/repeat pairs saving 43% and 35%; this does not establish that players generally halve loading time. No successful slow baseline was discarded.

## Patch-heavy list: completed gaming comparison

The 30-entry Combat Extended/races/genes/creatures selection exercises much more definition and template searching than the other smaller lists. Three scored fresh-cache pairs completed with no extension needed:

| Pair | Absent seconds | Wake-Up seconds | Reduction |
|---|---:|---:|---:|
| Forward | 78.324475 | 38.735194 | 50.55% |
| Reverse | 76.697905 | 38.976815 | 49.18% |
| Repeat | 77.709577 | 38.966505 | 49.86% |

Median saving is **38.743072 seconds / 49.856%**. Absent times span only 1.627 seconds and candidates 0.242 seconds. Host CPU averages were approximately 34–35%; available memory stayed above 3.85 GiB, and STALKER remained present throughout each capture. The passive samples also show approximately 43% less game-process CPU work with Wake-Up, supporting actual work reduction rather than relying only on wall-clock differences. All available loading-data comparisons match. The much slower first qualification (138.932985 seconds) is excluded and is not the baseline for this claim.

This establishes near-halving of **fresh application-cache startup on this list and machine while another game runs**, using the current accepted runtime. It does not establish a universal 50% average, an idle-PC percentage or gameplay compatibility. Fresh candidates exercise roughly 6,000 definition/template hits; the completed isolated-feature tests below confirm that those searches provide the large saving. Native PNG calls remain zero. The otherwise identical Loading Progress variant's completed interaction is reported below.

Once Gagarin already has the XML cached, the additional Wake-Up saving is much smaller. Warm absent times were **32.361160 / 32.747552 / 32.643174 seconds**; suite times were **31.912161 / 31.973824 / 32.048471 seconds**. All three pairs favored Wake-Up by approximately **1.4–2.4%**. Separate medians are **32.643174 → 31.973824 seconds**, saving **0.669350 seconds / 2.05%**. No extension was triggered. Both arms actually loaded their matching supplier cache, and all available loading-data checks match.

| Application cache condition | Without Wake-Up | With Wake-Up | Additional Wake-Up reduction |
|---|---:|---:|---:|
| Fresh | 77.71 s | 38.97 s | 49.86% |
| Gagarin cache restored | 32.64 s | 31.97 s | 2.05% |

These improvements overlap: Wake-Up makes patch searches faster, while an existing Gagarin cache skips the patch work in both arms. Do not add their percentages or credit Gagarin's baseline cache saving to Wake-Up. The warm qualification's 146.529923-second absent observation was excluded; the scored controls demonstrate that it was not a repeat baseline.

## Visual list: completed gaming comparison

The 32-entry facial-animation, texture, flora and biome selection saved a small amount with fresh caches. Absent times were **26.414079 / 26.564952 / 25.744282 seconds**; suite times were **25.273325 / 25.128102 / 25.134223 seconds**. All pairs favored Wake-Up by **4.32% / 5.41% / 2.37%**. Separate medians were **26.414079 → 25.134223 seconds**, a **1.279856-second / 4.85%** reduction. No fresh extension was needed, and the available loading-data checks match.

The first scored candidate recorded 728 definition/template hits, only one indexed type lookup, and zero PNG calls. Its candidate patch stage was 0.623 seconds and content reload 2.766 seconds; these are activity observations, not isolated stage savings.

Five warm pairs completed after the direction change triggered the fixed extension. Absent times were **23.319212 / 24.014574 / 24.908827 / 23.718993 / 23.379166 seconds**; suite times were **23.865579 / 24.034503 / 24.467298 / 24.167523 / 23.961130 seconds**. Paired reductions were **-2.34% / -0.08% / +1.77% / -1.89% / -2.49%**. Separate medians were **23.718993 → 24.034503 seconds**, a **0.315510-second slowdown / -1.33% reduction**. There is no reliable warm saving; four of five pairs were slightly slower with Wake-Up. The small magnitude and mixed direction limit the precision of any overhead claim. Available loading-data checks match throughout.

## Loading Progress on the patch-heavy list

This separate follow-up tests the same content with only Loading Progress added. It does not replace or enlarge the primary suite comparison above. Each round contains four runs: Wake-Up absent/present crossed with Loading Progress off/on. The fixed two-round extension was triggered by variation, giving five fresh rounds.

| Fresh condition | Absent median | Wake-Up median | Reduction | Individual paired reductions |
|---|---:|---:|---:|---|
| Loading Progress off | 88.037 s | 41.129 s | 53.28% | 50.04%, 54.80%, 59.13%, 63.23%, 44.86% |
| Loading Progress on | 87.265 s | 47.015 s | 46.12% | 43.11%, 44.46%, 45.63%, 46.45%, 27.53% |

Wake-Up helped in every fresh comparison with either Loading Progress setting. The absolute Wake-Up saving was smaller with Loading Progress in all five rounds. However, the LP-off absent controls ranged from 75.680 to 111.856 seconds, and the LP-on candidates from 45.878 to 63.675 seconds. The slow successful samples remain included. This supports a large surviving benefit and a reduced saving in this series, but not a precise general seven-percentage-point penalty.

Comparing separate medians, Loading Progress added **5.886 seconds with Wake-Up**, but **-0.773 seconds without Wake-Up**. The latter is not evidence that Loading Progress speeds up unoptimized startup: the direct absent-arm differences changed sign across rounds, from -24.591 to +12.187 seconds. The descriptive change in Wake-Up's saving is **-6.659 seconds**, with substantial environmental uncertainty.

The mechanism is narrower than “Loading Progress blocks the optimizer.” Its replacement of `CombineIntoUnifiedXML` is outside the accepted Gagarin reuse feature's supported hooks, so that feature records `unknown-interfering-hooks` and falls back. Definition/template searches still execute on fresh runs and retain substantial benefit. This campaign includes no new bridge, concurrency feature or repaint change.

The five warm rounds have also completed:

| Warm condition | Absent median | Wake-Up median | Reduction |
|---|---:|---:|---:|
| Loading Progress off | 33.075 s | 32.851 s | 0.68% |
| Loading Progress on | 38.699 s | 38.684 s | 0.04% |

Loading Progress added **5.624 seconds without Wake-Up** and **5.833 seconds with Wake-Up** by these medians. Its direct added time in the absent arm was positive in all five rounds. The Wake-Up comparison itself was variable: neither LP setting establishes a dependable additional cached-startup saving in this follow-up. One LP-off candidate took 58.130 seconds and remains included; the other four took 31.321–35.539 seconds. The descriptive interaction is just **-0.208 seconds**, too small relative to the observed variation for a precise effect claim.

All 47 available qualifying/successful interaction captures match the checked XML, settings, messages and content order across the LP variants. This includes the three successful captures from the interrupted original warm round, which are retained but excluded from the completed-round timing statistics. The scored analysis uses exactly five complete four-way rounds per cache condition. Exact times are in `artifacts/feature-benchmarks/factorial-stats.json` and the raw captures.

## Which retained features produce the saving?

These separate comparisons enable one search feature at a time. Definition/template searches find the game data targeted by XML patches; type searches find code classes by name. Each selected configuration first passes an excluded qualification launch, followed by three matched rounds with alternating order. The runs use fresh application caches and Loading Progress off. They are not added to the full-suite average, and their percentages must not be summed with it.

| List | Enabled feature | Absent median | Feature median | Reduction | Individual paired reductions |
|---|---|---:|---:|---:|---|
| UI | Definition/template searches only | 25.723 s | 25.885 s | -0.63% | +0.88%, -0.63%, +1.20% |
| UI | Type searches only | 25.723 s | 22.370 s | 13.04% | +14.35%, +12.50%, +13.25% |
| Patch-heavy | Definition/template searches only | 70.610 s | 36.577 s | 48.20% | +48.64%, +47.12%, +47.93% |
| Patch-heavy | Type searches only | 70.610 s | 70.331 s | 0.40% | +1.01%, -0.40%, +1.07% |

Type searches account for a repeatable useful improvement on the UI list. Definition searches alone are neutral at this measurement resolution: the differences are small and change direction. The attribution session's absolute times are lower than the earlier suite session, so its approximately 13% type-only estimate does not replace the earlier 10.4% suite estimate. It measures a different, internally paired comparison under the then-current foreground workload.

The patch-heavy list shows the opposite pattern: definition/template searches alone reliably save approximately half the startup time, while type searches alone are neutral at this resolution. Recorded CPU work in the three definition-only runs was 68.000 / 68.172 / 66.453 CPU-seconds, versus 119.953 / 117.484 / 114.328 in the matching controls. The reduction in CPU work supports a real avoided-work benefit, alongside the approximately 34-second median wall-clock saving. This is the existing general search feature, with no custom Combat Extended or Character Editor optimization.

The isolated Gagarin comparison keeps Gagarin itself, both searches and the PNG setting fixed, restores the exact same supplier cache in both arms, and toggles only Wake-Up's reuse of the already-parsed XML document. Loading Progress is absent. All three pairs improved:

| Pair | Wake-Up reuse off | Wake-Up reuse on | Reduction |
|---|---:|---:|---:|
| Forward | 30.991432 s | 30.562285 s | 1.38% |
| Reverse | 30.785969 s | 30.088921 s | 2.26% |
| Repeat | 30.692693 s | 30.174052 s | 1.69% |

Separate medians are **30.785969 → 30.174052 seconds**, saving **0.611917 seconds / 1.99%**. Both arms actually load Gagarin's cache; enabled receipts confirm parsed-document reuse and the skipped second serialization/parse. This establishes a small useful additional benefit on this cached list, not ownership of Gagarin's much larger cache benefit. It is also the specific retained feature that falls back when Loading Progress installs an unsupported replacement hook.

The feature receipts confirm the intended isolated activation, and available loading-data checks match throughout the search and Gagarin comparisons.

The processed-PNG cache has **no measured contribution in this campaign**: all native-texture candidate captures have zero eligible PNG calls. Many frozen packages provide DDS textures, which the normal loader selects. The earlier explicitly selected PNG workload remains separate evidence: the retained package's `png-p19-integrated-warm` / `png-p20-integrated-control` captures were **21.171827 / 24.311461 seconds**, a **12.9% additional reduction**, with 4,320 restored textures. Their source/package identities, menu observations and normal successful exits were rechecked from the captured records. Earlier forward/reverse observations on the pre-integration implementation supported roughly 11.5–12.7%. First cache construction was slower (the retained build took 39.327 seconds), so there is no first-launch benefit claim. See the [PNG investigation](archive/investigation-results.md#2026-09-07-png-processing-implementations). None of those deliberately selected PNG runs enter this campaign's representative-list average.

## Remaining coverage and limitations

All four smaller selections now have completed fresh comparisons. Their **secondary descriptive** equal-list average is **16.58%**: industry 1.23% (uncertain direction), UI 10.38%, patch-heavy 49.86%, and visual 4.85%. This includes every completed smaller list, rather than choosing only winners. It spans gaming sessions before and after STALKER's restart and is not the preregistered primary cohort, which includes mixed instead of visual.

Adding those four absent medians gives **163.742379 seconds**, versus **120.307418 seconds** with Wake-Up, a **26.53% reduction in their combined time**. That second calculation gives more influence to the long patch-heavy list. Neither number is an estimate for all players; a public claim would need to name the tested lists, machine, cache condition and background workload. Do not present the provisional approximately 17% equal-list summary as the still-incomplete broader campaign average.

Smaller-list comparisons, both Loading Progress interactions and the selected component comparisons are complete. The 95-entry mixed list completed an absent qualification, but briefly left only 1.215 GiB free alongside STALKER 2; repetitions remain held for a session with sufficient memory. Its current admission requirement is 14 GiB free. The earlier 309-entry attempt exhausted memory and was closed after preserving evidence. It has no successful menu time and is excluded from timing statistics. The original broader cohort is therefore incomplete; this report does not claim that the entire planned large-list campaign finished.

One later interaction launch lost its run record to a Windows atomic-replacement error after starting the fixture. Its exit code and normal resource checks were unavailable, so the full profile was preserved as a measurement failure and the whole interrupted four-way round was replaced. A bounded file-write retry passed all 28 fixture checks. This was a benchmark-tooling repair, not a new optimization; that failed run is never scored.

All selections use one frozen installed library with different enabled subsets. Disabled packages remain installed except the explicit parked exclusions and physically absent optimizer. An eventual average describes the named tested lists under their stated conditions; it must not be presented as an average for all players or silently combine idle large-list runs with concurrent-game small-list runs.

The readable fixture catalog (private: `artifacts/feature-benchmarks/fixture-catalog.md`) lists the exact ordered package names for all eight selections, including the held mixed variants. The selection JSON files retain dependency/order provenance and frozen identities.

At this checkpoint, the last capture is `fb-a-g3-biocombat-suite`, with normal exit and automatic startup checks passed. The fixture audit passed and the accepted runtime is installed. No further test batch is running. Runtime source is unchanged from `382996ca...`; the only implementation changes are benchmark selection support and the observed atomic-record write repair. No push or publication occurred.

The campaign retains **183 successful captures: 148 scored launches, 30 qualifications and five successful but excluded observations** (two across the foreground-game transition and three from the incomplete interaction round). Two unsuccessful launches are preserved separately. All 183 successful captures have both original and processed XML receipts, and the available loading-data comparisons match. At closeout STALKER 2 remained running and only 6.83 GiB was available, below the mixed list's 14 GiB starting requirement. The larger comparison is not scheduled in the background.
