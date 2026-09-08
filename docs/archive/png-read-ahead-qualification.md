> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_OVERNIGHT_PNG.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# General PNG read-ahead qualification, 2026-09-06

Historical branch handoff: deployed runtime and fixture selection below describe
this experiment, not the current fixture. See [the later restored state](loading-experiments-closeout.md).

This branch starts from main 7d464ea and selectively retrieves the shared
bounded texture readers and PNG adapter from research c25bf22. Existing main
startup searches and optional Gagarin remain. No HugsLib filtering or rejected
XML experiment is imported. The owner's corrected scope is general RimWorld
loading improvements across modlists; compatibility with loading frameworks
remains allowed. Single-mod-specific optimizations are outside scope.

The owner authorizes automatic fixture launches of all sizes overnight. This
agent alone owns checkout, build, deployment and launches until handoff. Normal
Steam/Workshop/profile/save/cache data, other applications and security settings
remain untouched. There is only the canonical fixture and one source checkout.

PNG read-ahead is optional, off by default. The existing research adapter uses
the DDS shared-reader infrastructure; control and PNG-on arms both retain
`--buffered-dds --dds-read-ahead --dds-read-ahead-workers 2`. It does not count
changing DDS as a PNG gain. `--fixture-png-source` selects retained original PNGs
instead of DDS siblings in both arms without changing frozen files. Ordinary
loading keeps native DDS precedence. A successful future integration should not
force an ineffective DDS optimization merely to enable PNG support; establish a
benefit before spending effort on that separation.

Readers prepare only physical bytes in at most two 8 MiB buffers. PNG receives
an independent exact-length byte array. Original decoding, mipmaps, compression,
upload and publication remain on their original thread and in their original
order. Unsupported/changed files and unknown reader hooks retain native reads.
All readers join and leases return before their content scope ends.

Comparison uses candidate startup-searches, matching timing and automatic menu
observer, fresh application caches and matching Gagarin selection when present.
Start with small actual activation, then the existing frozen twelve-package
medium lane. The final original reverse control is essential: the overnight DDS
experiment's large first-pair difference disappeared with a warmed original.
Windows file caches are uncontrolled; do not flush them or force memory pressure.

Use actual PNG reads/errors/bytes, existing whole texture scope and
menuReadyObservation.elapsedSeconds. Complete menu time includes hook setup,
worker creation, joins and ordered completion; inner read and texture timings
overlap and must not be added. Existing focused byte/bounds/order/fallback checks
and live captured content comparisons are proportional evidence. They are not
pixel-level or gameplay/save-load qualification.

Prior DDS and scope-parked HugsLib results are retained as documentation only in
OP7_OVERNIGHT_DDS.md and OP7_OVERNIGHT_HUGSLIB.md. Main is unchanged, OP7 remains
incomplete and OP8 inactive. Exact runtime and captures follow below.

## Result: small texture-stage saving, no useful established startup gain

Stop this route without promotion. The two warmed original controls averaged
39.657 seconds to menu; the two candidates averaged 39.609 seconds, just 0.048
seconds (about 0.12%) earlier. The final original run was faster than either
candidate. This does not establish a useful complete-startup benefit.

The PNG read calls themselves averaged 1.735 seconds original versus 0.979
candidate, and the complete texture scope averaged 12.680 versus 12.004 seconds.
That approximately 0.677-second texture-stage difference is a small positive
signal. Later startup variation leaves essentially no average menu saving in
this set. Do not convert the stage improvement into a claim about total startup,
or extrapolate it to all modlists. No full-profile expansion, worker-count tuning,
cache manipulation or PNG/DDS dependency redesign is justified by this result.

The first original run was much slower, but the original reader reproduced
nearly all of that improvement after Windows file warming. Its result must not
be combined with warmed controls to claim a large speedup.

All labels below use exact runtime
`2327212bafb3d66697f93175270455156edcf7a9`, built with zero warnings/errors and
explicitly deployed with a successful `deploy --package`. Deployed and prepared
source revisions were checked before every launch. There were no old-package
or failed-deployment comparison arms in this series.

| Medium label | PNG reader | PNG read s | Whole texture scope s | Menu s |
| --- | --- | ---: | ---: | ---: |
| night-png-m01-original | Original, first run | 28.996 | 40.323 | 64.501 |
| night-png-m02-on | Two readers | 0.969 | 11.878 | 39.711 |
| night-png-m03-on | Two readers, repeat | 0.989 | 12.129 | 39.507 |
| night-png-m04-original | Original, warmed | 1.631 | 12.550 | 39.928 |
| night-png-m05-original | Original, warmed repeat | 1.838 | 12.811 | 39.387 |

Both arms request accepted searches, Gagarin on, shared two-reader DDS settings,
PNG-source comparison, matching diagnostics and automatic observer/exit. The
medium selection has no Gagarin supplier; its optional integration correctly
refuses locally. No supplier cache was used or restored. All profiles were freshly
prepared from the same seed with the same frozen order/settings. No HugsLib
optimization or other supplier-specific fix runs in this branch.

The medium lane contains twelve dependency-ordered workshop packages plus the
official game/DLC and test foundation (22 loaded packages). It selects all 10,156
retained PNG counterparts consistently in both arms. There are no DDS files left
to read in this selection after the explicit PNG-source overlay, so matched DDS
infrastructure did not create DDS-reading work. PNG-off controls had no queued
file reads or workers. There is no claim about simultaneous mixed-format gains.

## Functional evidence and limits

All six launches (one small and five medium) reached the observer endpoint,
exited normally with code zero and captured `automaticTestPassed=true`. No
forced closure, UI automation, stall or game error dialog intervention occurred.

Small observed `night-png-s01-on` passed at 22.133 seconds: 2,854 PNG calls,
2,852 prepared reads, 50,265,807 bytes, two fallbacks, zero PNG errors and zero
outstanding buffers. Both medium candidates made 10,156 PNG calls and supplied
10,153 prepared reads / 139,454,995 bytes. Three unsupported reads fell back to
the original reader. Both used two peak buffers and left zero outstanding.
Waiting for prepared bytes totaled only 23.927 and 19.501 ms in medium runs.

All five medium captures match 22 loaded packages, 22,829 parsed definitions and
5,132 patch operations, the exact selected package order and normalized logged
problem-pattern sets. Raw matching log lines are retained; normalization removes
only memory-loaded DLL addresses, XML Extensions elapsed times and Unity
allocation counter values. PNG calls report zero errors in every arm.

No combined XML/cache report is generated in this supplier-absent medium lane,
so live XML byte equality is not claimed. Content preservation evidence here is
the unchanged original byte/decoder path, focused exact-byte and ownership tests,
matching native live counts/order and matching logged problem patterns. It does
not establish independently compared rendered pixels or gameplay/save behavior.

43 focused managed tests and 27 fixture Python checks pass, with no skips.
Coverage includes the pinned physical PNG reader's actual body, independent
exact-length arrays surviving pool reuse, mixed PNG/DDS ordering and DDS
precedence, unchanged-file/size fallback, two-buffer bounds, worker joins,
existing parser behavior, the Loading Progress enumerable boundary and optional
Gagarin. New fixture checks cover PNG selectors and exact medium order/dependencies.
No broad new validation or profiling system was built.

Evidence is private under `artifacts/overnight-png`: `check.log`, `build.log`,
`deploy.log`, per-run preparation/launch logs, `run-summary.json`,
`content-comparison.json` and `summary-final.txt`. Original captures remain in
the canonical fixture's `results/<label>` directories.

## Clean handoff

Main remains 7d464ea. This optional research implementation stays on
`codex/overnight-png-qualification`; neither the PNG adapter nor its DDS
prerequisites should be merged merely because the functional runs passed.
The last selected/captured label is `night-png-m05-original`: PNG timing only,
explicit PNG-source fixture comparison, matched DDS infrastructure, Gagarin on
with absent supplier. Runtime 2327212 remains deployed. Subsequent closeout
commits affect documentation only. The game and launcher are closed.

PNG/DDS read-ahead and the PNG-source comparison all remain off by default.
Next work starts from current main with a fresh branch and explicit package
deployment/preparation. The next general lead is avoidable expression processing
inside the shared definition-name index, not another mod-specific worker patch.
OP7 remains incomplete and OP8 inactive. No merge, push or publication occurred.
