# Overnight qualification closeout — 10 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

The authorized local testing and packaging pass is complete. The steward accepts
**0.3.0-rc.1 as a bounded Windows release candidate**, with ordinary colony-entry
evidence, reliable startup comparisons and an inspected source-inclusive package.
It is not full ecosystem replacement or an approved stable public release.
Nothing was published and normal game data was untouched. All teams stopped;
the steward owns the checkout and the five-minute check is paused at this endpoint.

## What works and what changed

The retained feature set includes existing definition/template and code searches,
optional supplier integrations, faster translation application, PNG/JPEG output
caching and maintenance, native texture preparation, remembered texture suppliers,
the loading display and background save/world loading. New optional features
remain off by default; Prepatcher stays required. Native world creation was added
and qualified, including actual minimization and current-preference restoration.

Public lossy compression/resizing presets were removed after alternative consumer
paths failed to establish a useful, safely admitted workflow. No helper binary
ships. Independent XML/language replay, first-build image acceleration, broader
texture/atlas reuse, on-demand assets/progressive DDS, full per-mod profiling and
new-colony/standalone-map background admission are deferred. The
[complete nine-goal plus S14 table](retained-features.md) records the evidence,
alternatives and user impact of every omission. Similar competitor features did
not justify blanket new conflicts or a launch popup; verified conflict/status
rules remain. Linux helper work is owner-deferred and this candidate has no new
Linux live qualification.

## Reliable startup comparison

Same Windows Steam version and French 99-package workload; four measured runs
per arm, balanced forward/reverse order, after one excluded warmup each. Generated
application caches were fresh, while Windows file caches were left unmanaged.
These measure menu readiness, not colony loading or a universal average.

| Configuration | Mean menu time | Change against old public 0.2.1 |
|---|---:|---:|
| Wake-Up absent | 153.600 s | — |
| Public 0.2.1 | 68.258 s | — |
| New defaults | 68.634 s | 0.55% slower |
| New translation/routing enabled | 67.164 s | 1.60% shorter |

Against absence, new defaults were 55.32% shorter and the optional bundle 56.27%
shorter. Most of that difference already existed in the old release. The optional
gain is small and variable; it lost to the old release in one of four blocks.
All 20 captures passed; four warmups were excluded by design, no measured run was
discarded. [Full results](steam-qualification-comparisons.md) retain individual
times, ranges, deviations, identities and qualification limits.

## Ordinary entry and remaining risk

Three normal screen-driven controls passed with native logging and required
Prepatcher: new colony/save/reload without observer, manual load with menu-only
observer, and manual load of the exact packaged DLL without observer. Visible
gameplay and normal exit were observed; no workaround or automatic gameplay probe
ran in these controls. The steward checked actual run hashes, arguments, package
receipts, UI action history and the independent package review.

The shared InputLegacy dependency error still logs. Earlier automatic scene
probes stalled with and without Wake-Up; a captured native engine formatting
loop was evidenced, but its trigger is not repaired or conclusively attributed
to the probe. The manual passes close the missing ordinary-path evidence,
not that underlying risk. Earlier minimized-save tests used private logging
suppression and remain separately labeled. No public suppression is shipped.
No demonstrated new Wake-Up defect justifies speculative engine changes or
repeating failed root-lookup/preload experiments. Keep this as a release candidate;
stable public release readiness remains unconfirmed while that risk persists.

## Exact accepted package and stopping point

Reviewed handoff: `9772af1f1d8f0bda6f710f00f4cedd9f0d592548`.
Built/source archive revision: `8ca0dd763a2ad530668b59d6c3488f28eac311f5`.
DLL SHA-256: `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`.

[Local candidate ZIP](../../../artifacts/releases/0.3.0-rc.1-8ca0dd763a2ad530668b59d6c3488f28eac311f5/wake-up-0.3.0-rc.1.zip)
SHA-256: `ab59fce2fcdc6eb4891cfb3b3e46e8b6e03c151885f7593b19e219b147db3c48`.
All 14 payload files and 209 source files were reviewed; source matches the exact
committed archive, required notices are present, and only WakeUp.dll is installed
as executable code. Its compiled instructions equal the accepted benchmarked core;
the recorded game assembly reference changed for the Steam rebuild. Final checks:
109 managed passes, one optional helper skip, 49 Python passes, no build warnings
or errors. The exact rebuilt DLL passed the final native load.

Package bytes remain the inspected pre-final-review checkpoint. The current
source documents and this acceptance supersede its conservative pending-check
wording; no artificial rebuild/retest loop is needed for subsequent review history.
[Detailed final evidence](ordinary-entry-final-candidate.md) remains authoritative
for source/build/deployment/run distinctions. Further publication or normal-game
deployment requires its own owner direction; neither was part of this pass.
