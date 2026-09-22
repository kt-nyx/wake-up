# All retained options and warm-cache performance — 10 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Accepted result

With every retained option selected and the useful caches populated, the exact
new candidate averaged **57.052 seconds** to the menu on the French mixed-99
workload. That is **46.02% shorter than Wake-Up absent** (105.698 seconds) and
**4.26% shorter than public 0.2.1 with all its supported options selected**
(59.592 seconds). Four measured repeats per configuration passed normally.
The new version beat the old version in every matched block, saving 2.539 seconds
in the mean; their overall ranges slightly overlap.

This answers the requested all-selected, populated-cache comparison. It is a
workload-specific measured result, not a universal upper bound or proof that
every selected option helps. Native image work is sparse: only one prepared
texture is useful here. Most of the difference cannot be attributed to texture
preparation. No runtime algorithm, package or observer changed.

Dispatch `wake-up-all-options-performance-9e99f36` is accepted by the parent
steward at handoff `c887b71f626403308c58b3ab87e8c06c623e8150`. No correction
is required for this bounded comparison. No publication or new slice follows.

## Exact identities and controls

Source revisions identify code history; SHA-256 fingerprints identify the actual
file bytes. These are separate from proof of the bytes used in a captured run.

| Layer | Identity |
|---|---|
| Clean assigned base | `9e99f36e79deaa4bf5d3c493458b18b21f0e5224` |
| Sole checkout branch | `codex/all-options-performance-9e99f36` |
| Accepted runtime logic | `d584eb36aa68cd374a26621f97295dbb63626a33` |
| Final candidate source / DLL | `8ca0dd763a2ad530668b59d6c3488f28eac311f5` / `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f` |
| Public 0.2.1 source / DLL | `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f` / `47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d` |
| Observer source / DLL | `41f50d5019950ec678131f138cff3d520320d776` / `2e5433c950f3f7429b118f7f04fcdf050bb786c4803bccc782f7f251e8fdf4b8` |
| Steam generation | `steam-rev590-20260910-020604-cb37ef1b` |
| Generation manifest | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93` |
| Managed game DLL | `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |

The exact final Steam package was deployed directly from
`artifacts/fixture-package/8ca0dd763a2ad530668b59d6c3488f28eac311f5`.
The old release used its existing authenticated Steam comparison projection.
No package-binding exception, rebuild or receipt substitution was needed.
The accepted compiled-instruction equivalence to `d584eb3` is retained from the
[final-candidate inspection](ordinary-entry-final-candidate.md).

All arms use the existing [mixed-99 selection](../../../artifacts/steam-qualification-20260910/mixed-99.json):
99 frozen packages, plus the observer and, when present, Wake-Up. Character Editor
was temporarily restored through the fixture tool. Other exclusions, order,
French language and native DDS precedence stayed matched. Graphics: automatic
0×0 resolution, windowed, UI scale 1.75, compression enabled, Direct3D 11,
RTX 4080 SUPER, driver 32.0.16.1664, Unity 2022.3.35f1. Automatic runs set ordinary
`runInBackground=True` consistently. Windows file caches stayed unmanaged.

Both versions use ordinary user activation. Established switches remain at
their verified true defaults: enabled, definition/template searches, type searches,
Gagarin assistance, Character Editor, Loading Progress coalescing and Giddy-Up.
Old additionally selects `pngCache=true`. New selects all seven optional switches:
`translationApplication`, `assetRouting`, `backgroundLoading`, `loadingDisplay`,
`loadingDisplayDiagnostics`, `pngCache`, `preparedTextures`; native preset 0 and
`cacheMaintenanceNextLaunch=normal`. Clear/rebuild/bypass are maintenance actions,
not selected speed features. The public settings screen confirmed the true choices.

Every arm restores its own successful fixed conditioning capture: `aop-00-a`,
`aop-00-b`, `aop-00-c`. Gagarin and the exact terrain-color cache are restored for
all arms; both owned texture stores are restored for new. Gagarin reports
**“Finished loading XML from cache!”** in every measured log, under its actual
`GARGARIN` spelling. All report cached colors for 669 terrain definitions.
The main supplier cache holds about 82.97 MB; its mod-list record correctly differs
by Wake-Up presence. Supplier XML payloads and the underlying content are matched.
Old has no eligible PNG and therefore no owned PNG entry to restore.

All performance launches used `scripts/fixture.py`, freshly checked process
identities, the independent menu observer and normal automatic exit. No other
RimWorld process ran, launches did not overlap, and reviewers/builds were stopped
during measurement. No gameplay/verification probes or private stack suppression
were included. The endpoint is `menuReadyObservation.elapsedSeconds`, the first
completed menu repaint, not process lifetime or a sum of overlapping stages.

## Complete measurements

Conditioning order was A-B-C. Measured order was A-B-C / C-B-A / A-B-C / C-B-A.
All twelve measured captures have exit 0 and `automaticTestPassed=true`.
There were no failed timing runs, removed outliers or replacement measurements.

| Run in execution order | Configuration | Menu seconds |
|---|---|---:|
| `aop-01-a` | absent | 104.327484 |
| `aop-01-b` | old all-supported | 57.959504 |
| `aop-01-c` | new all-selected | 56.833898 |
| `aop-02-c` | new all-selected | 56.622036 |
| `aop-02-b` | old all-supported | 57.920923 |
| `aop-02-a` | absent | 105.493808 |
| `aop-03-a` | absent | 109.116638 |
| `aop-03-b` | old all-supported | 61.389323 |
| `aop-03-c` | new all-selected | 56.791723 |
| `aop-04-c` | new all-selected | 57.961764 |
| `aop-04-b` | old all-supported | 61.097133 |
| `aop-04-a` | absent | 103.853953 |

| Configuration | N | Mean s | Median s | Range s | Sample SD s | Change vs absent | Change vs old |
|---|---:|---:|---:|---|---:|---:|---:|
| Absent | 4 | 105.698 | 104.911 | 103.854–109.117 | 2.381 | — | — |
| Old all-supported | 4 | 59.592 | 59.528 | 57.921–61.389 | 1.911 | -43.62% | — |
| New all-selected | 4 | 57.052 | 56.813 | 56.622–57.962 | 0.613 | -46.02% | -4.26% |

Percentage change is `100 × (configuration mean / reference mean − 1)`;
negative means shorter loading. Sample SD describes variation between repeats.
New saves 48.646 seconds against absence and 1.126–4.598 seconds against old
within the four blocks. Four repeats do not establish a universal average.

The earlier fresh-cache means **153.600 / 68.258 / 68.634 / 67.164 seconds**
remain separate context for absent / old defaults / new defaults / new
translation-routing. They are not substituted into these warm comparisons.

## Preparation, first builds and actual feature work

First construction and preparation were completed before measured warm launches.
Every automatic run below passed; the manual run exited normally and has no
automatic-pass claim. None contributes to the table above.

| Excluded capture | Purpose / result | Menu seconds |
|---|---|---:|
| `aop-ui-01` | Manual public preparation; different manual background/focus condition | 257.325678 |
| `aop-build-a` | Absent, fresh application caches | 154.312012 |
| `aop-build-b` | Old all-supported, fresh application caches | 69.711836 |
| `aop-build-c` | New all-selected, empty owned/supplier stores | 67.974145 |
| `aop-activation-c` | Separate functional prepared-hit/routing/translation check | 55.419048 |
| `aop-00-a` | Warm conditioning, absent | 104.564447 |
| `aop-00-b` | Warm conditioning, old | 61.584781 |
| `aop-00-c` | Warm conditioning, new | 56.170573 |

The frozen inventory has zero native-selected PNG candidates. Mixed-99 supplies
two JPEGs: Alpha Animals' 1024×1024 sand texture (223,185 source bytes), and
ReGrowth's 1470×1961 BestDog image (377,746 bytes). The latter exceeds the public
preparation window's conservative 8 MiB complete-entry admission and produces no
automatic cache entry in these runs. Another frozen mod offers just one eligible
JPEG; two other JPEGs belong to inactive legacy folders. A naturally eligible
texture-heavy supplementary selection is therefore unavailable. No textures were
invented, DDS files hidden, or PNG-sibling experiment substituted.

The actual public workflow in `aop-ui-01` was Options → Mod options → Wake-Up →
Prepare textures → Alpha Animals → exact sand path → Scan → Prepare. It reported
one selected/eligible/completed, zero reused/skipped/failed. Completion was
observed within **0.6499213 seconds** after the click, including tool/screenshot
overhead; this is an upper bound, not an engine stopwatch reading. The prepared
entry is **1,398,772 bytes**, plus 85-byte usage data. Its SHA-256 is
`cb8cce6ced3ef6698180025400c722e13846b91f6f81861127d6c9a335183ac9`.

Automatic first construction attempted two JPEGs, stored one, and reported
39.703 ms of capture work within 102.967 ms of image processing. Those are nested
stage observations, not extra whole-startup savings. The PNG/JPEG store occupies
1,398,293 bytes including usage; the prepared store occupies 1,398,857 bytes.
Both populated stores cover the same sand image. Prepared resolution runs first.

| Selected feature | Actual activity and limit |
|---|---|
| Definition/template searches | Installed; fresh new run: 7,273 hits plus 317 attribute hits. Warm runs: zero hits, two ordinary fallbacks because Gagarin skips the relevant patch work. |
| Type searches | Each new measured run: 130 calls, 70 index lookups, eight builds, two ordinary fallbacks, zero errors/refusals. Alert-only leaf searches do no work at this menu endpoint. |
| Character Editor | Each new measured run: 6,268 objects + 47 turret presets, 6,267 + 46 dictionary hits, zero fallback/retained entries. |
| Giddy-Up | Each new measured run: 1,380 calls/hits, zero errors/fallback/retained textures. |
| Loading Progress coalescing | Installed; 779/778/780/777 pauses suppressed in the measured new runs, zero refusals. |
| Translation application | Each new measured run: 680 package scopes, 27,939 lookups, zero native fallbacks/retained entries; functional French sample matches accepted native sample. |
| Texture routing | Separate functional check passed, same native winner, two observed reuse hits. No isolated routing percentage is claimed. |
| Prepared textures | Separate functional receipt proves one 1024×1024, seven-mip DXT5 startup hit. Timing runs restore the identical entry/selection and omit the hit probe. |
| PNG/JPEG cache | New measured runs: zero hits, one JPEG miss, zero writes/errors; prepared output wins for the overlapping image. Old: zero eligible PNG calls. Populated storage is not evidence of extra hits. |
| Wake-Up Gagarin assistance | Refuses Loading Progress's interfering XML hooks. Gagarin itself hits its own cache in every arm. No forced hooks. |
| Display and diagnostics | Both selected; display refuses Loading Progress, functional `panelLayoutExecuted=false`. Diagnostics never starts. Disabling these inactive options has no admitted display work to remove; no unsupported faster-display-off claim or redundant timing series. |
| Background save/world loading | Selected and supported status reported; no relevant save/world work before the menu. No startup CPU-speed claim. |

Preparation's independent contribution was not isolated and no useful
preparation-specific saving was established. **No preparation break-even is
claimed**: dividing its cost by the whole bundle's 2.539-second gain would falsely
credit translation/search/integration work to one image. The first-build versus
warm difference also includes supplier caches and is not a texture-cache effect.

## Configuration advice, validation and handoff

For this French workload, the all-selected new configuration is the fastest of
the three tested configurations. Keep ordinary cache maintenance and supported
search/integration features; translation/routing remain useful optional choices
within their accepted evidence. Keep Loading Progress if desired and leave
Wake-Up's redundant display/diagnostics off for a clear settings configuration;
this is not a measured additional speedup. Preparing/caching the lone image
does not support a practical loading-time recommendation on this DDS-heavy list.
Avoid maintaining duplicate owned stores solely for an unproven extra gain.
No lower-feature warm configuration was measured here, so this is not proof of
the fastest possible settings combination.

Fixture-only commit `55bc8dd` admits normal user-selected PNG-cache restoration
and restores only the exact recorded terrain-color cache alongside supplier
caches. Exact package, content/order, profile, owned-path and byte checks remain.
All **50 Python checks passed**, including focused disabled/maintenance choices,
source mismatch, tampered PNG and terrain bytes. No broad profile replay or
harness rewrite occurred. Independent review checked all 19 automatic run
hashes, all twelve measured controls, source-cache bytes, actual hit logs,
nonoverlap and statistics. Existing runtime correctness evidence was trusted.
No startup/preparation failure or forced game closure occurred in this task.

Raw records and reproduction driver are under
[`artifacts/all-options-performance-20260910/`](../../../artifacts/all-options-performance-20260910/run-evidence.json):
`run-evidence.json`, `statistics.json`, `progress.jsonl`, `preparation.json`,
`native-image-inventory.json`, settings JSON and `run-series.py`.
They include every source/capture fingerprint and distinguish manual,
functional, first-build, conditioning and measured runs.

All operations stop at handoff. Original three fixture exclusions are restored,
the exact final candidate and observer remain deployed, no new launch is prepared,
and no RimWorld process remains. Final state/check receipt is
[`handoff.json`](../../../artifacts/all-options-performance-20260910/handoff.json).
The original GOG recovery generation remains untouched. Normal Steam/Workshop,
profile/save/cache state, other applications, Deck, Linux helper, security and
publication were untouched. Exclusive checkout ownership returns to the steward
for actual evidence review. Retained omissions and the unresolved shared engine
risk remain as in the accepted candidate reports; menu results do not reopen or
repair the earlier gameplay-probe stall or authorize a release.

## Parent steward acceptance — 10 September 2026

Reviewed the actual diff from `9e99f36` through `c887b71`: private restoration
changes preserve package/content/profile checks and validate the exact terrain
file; no production or observer change. Existing 50 passing Python checks and
the independent review adequately cover this narrow change; no redundant rerun.
The steward independently verified all 20 recorded run fingerprints and the
12 measured captures' normal passes, timing endpoint, package identities,
matched content/language/observer, fixed supplier-cache sources and restored
hashes, optional selections, absence of gameplay probes and nonoverlap. Raw
logs confirm Gagarin cache hits in every measured arm. Recalculated all means
and medians and the 4.261% reduction against public 0.2.1.

Old's last two measurements were slower than its first two; no observations
were excluded. The new version was faster within all four blocks, but the
ranges overlap slightly and the size of the gain varies. Accept a useful
workload-specific result, not a guaranteed percentage or a mathematical upper
bound. One prepared image, inactive conflicting display/XML assistance, and
features outside the menu endpoint remain explicitly disclosed.

The checkout was clean and no RimWorld process remained at review. Ownership
returns to the steward; the requested follow-up is complete and the existing
five-minute check is paused. Package acceptance and unresolved release limits
remain unchanged. Further measurements need a new useful question rather than
repeating this series for proof completeness.
