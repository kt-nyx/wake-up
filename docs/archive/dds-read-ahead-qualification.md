> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_OVERNIGHT_DDS.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Overnight DDS qualification, 2026-09-06

Historical branch handoff: deployed runtime and fixture selection below describe
this experiment, not the current fixture. See [the later restored state](loading-experiments-closeout.md).
The later general-loading scope restriction supersedes the HugsLib next-step
recommendation below; that work is parked rather than promoted.

This investigation starts from main 7d464ea on
`codex/overnight-dds-qualification`. The owner explicitly authorizes overnight
implementation and automatic fixture testing, including full content while the
PC is idle. One agent owns checkout/build/deployment/launch at a time. Normal
Steam, Workshop, profiles, saves, caches and other applications remain untouched.

The first checkpoint selectively takes the two-reader DDS implementation and
focused tests from af21fea. Main's accepted searches and optional Gagarin feature
are preserved; no PNG, HugsLib, XML or other research experiment is imported.
Read-ahead stays optional and off by default. The fixture adds only the DDS
selectors and the existing small textures-observed lane from research.

Prior full research reads saved about 57% inside the DDS loader, but failed late
startup repeats prevent promotion. Retained next-r07-two-readers-repeat and
png-r01-control both end near unused-asset cleanup/Starting patches: 1066;
the latter did not enable PNG read-ahead. That pattern does not prove a cause.
Idle-host matching comparisons are new evidence; no blocked profiler is retried.

Comparison: candidate startup-searches in both arms, original DDS versus
`--buffered-dds --dds-read-ahead --dds-read-ahead-workers 2`, same
`--observation timing --texture-timing --menu-observer --exit-after-menu-ready`.
Gagarin is selected identically where present. Profiles start from the same seed;
application-cache restoration, if used, must name the identical captured seed.
Windows filesystem caching is uncontrolled and is not called cold.

DDS-call elapsed totals exclude scope setup and final joins. Complete startup
menu observation includes installation, preparation, worker start, ordered
processing and joins; use it for net product benefit. Do not add nested timers.
Successful menu startup, content preservation, gameplay compatibility and
performance remain separate claims. OP7 is incomplete and OP8 inactive.

Evidence and final decision will be appended after actual captures. The fixture
remains the single canonical `.rlo-test-instance`; private evidence is retained
under its results and `artifacts/overnight-dds`.

## Result: no demonstrated useful whole-startup improvement

The warmed original reader reaches the menu slightly sooner than either
two-reader run. Do not promote DDS read-ahead as a measured product improvement
or enable it routinely. The large first-pair difference is confounded by Windows
file caching: the original reader also becomes much faster after the first run.
No cache flush, artificial cache eviction, extra-reader experiment or security
change was attempted. This is a useful stopping point for DDS on this idle PC.

All four full runs use exact built/deployed runtime
`1d1528df7a78dc7b0d9ad9567fff61b8071de35c`, original XML algorithms, accepted
startup searches, optional Gagarin on, matching timing/observer settings and
fresh application caches. Every log confirms normal Gagarin cache creation;
no cache was restored and the cache-hit reuse path is not a measured cost here.
Only DDS selection changes. The frozen enabled order and existing reversible
Modern Dev Tools exclusion match in all arms.

| Full label | DDS reader | DDS calls s | Whole texture scope s | Menu s |
| --- | --- | ---: | ---: | ---: |
| night-dds-f01-original | Original, first full run | 207.777 | 209.669 | 439.751 |
| night-dds-f02-read-ahead | Two readers | 7.520 | 8.829 | 195.979 |
| night-dds-f03-read-ahead | Two readers, repeat | 8.209 | 9.965 | 194.423 |
| night-dds-f04-original | Original, warmed reverse control | 9.214 | 10.460 | 193.943 |

The warmed candidate mean is 195.201 seconds, versus the final original control
193.943 seconds: about 1.26 seconds later, not a startup saving. Candidate whole
texture means are 9.397 versus 10.460 seconds, a small stage difference that is
neither repeated against multiple warmed controls nor large enough to justify
further qualification without a net startup benefit. Do not average the first
and final original controls into a claimed speedup. These are one fixed mod
collection's runs, not universal or statistically established averages.

The whole texture scope comes from existing Loading Progress per-mod texture
measurements. DDS-call counters are nested within it. Complete menu time is the
product endpoint and includes earlier installation and all worker/scope costs.
Nothing is added across overlapping stage timers.

## Activation, correctness and reliability evidence

All four full runs have `automaticTestPassed=true`, exit code zero and a genuine
menu observation. Both full read-ahead runs report 232 scopes, 39,959 prepared
files / 5,078,043,867 bytes, 16 mapped-reader fallbacks, two peak buffers and zero
outstanding buffers. All four load 39,975 DDS textures with zero DDS exceptions,
316 packages, 47,388 definitions and 14,748 patch operations. Original/patched
Unified XML and all four per-definition reports are byte-identical across arms.
Cache bookkeeping files vary and are not asserted byte-identical. Normalized
logged problem-pattern sets match; only DLL addresses, elapsed-time text and
Unity allocator counters are normalized, with raw lines retained.

Small native `night-dds-s03-native` passes at 12.285 seconds with 2,853 prepared
reads; Loading Progress `night-dds-s04-observed` passes at 13.601 seconds with
2,854 reads. Both have two peak buffers, zero fallbacks and zero outstanding
buffers. These are activation checks, not speedup claims.

The earlier `night-dds-s01-native` and `night-dds-s02-observed` inadvertently used
previous deployed f37b34d because the first deployment command omitted its
required explicit package argument and refused. Both exited normally, but are
excluded from qualification and performance conclusions. Missing read-ahead
receipts exposed the mistake. Deployment was then explicit and successful;
candidate/prepared metadata binds every s03/s04/f01-f04 capture to 1d1528d.

The old late-startup stall did not recur in any of the four full runs. This is
additional successful startup reliability evidence, not proof that the prior
stall's cause is fixed or that gameplay/save loading is qualified. No forced
exit or UI interaction was needed. After f01 exit a single system snapshot found
about 18.65 GiB free system RAM; GPU use was 54% with 3,090/16,376 MiB occupied.
Large game memory pressure was absent, but the GPU was not literally idle.
Other applications were inspected read-only and never changed.

37 focused managed checks and 23 fixture Python checks pass. The managed checks
cover queue ordering, unique worker claims, bounded buffers, changed-input
fallback, early disposal/joins, existing DDS parser behavior, enumerable
exception/disposal behavior and preserved Gagarin integration. Build completed
with zero warnings/errors. The imported DDS implementation is unchanged from
af21fea; only its selective main integration and fixture selectors are new.

Exact evidence: `artifacts/overnight-dds/comparison.json`,
`content-comparison.json`, `run-summary.json`, `summary-final.txt`, small/full
prepare/launch logs and system snapshots; original captures remain under
`.rlo-test-instance/results/<label>`. Focused managed log is
`artifacts/overnight-dds-check.log`; fixture checks are `fixture-check.log`.

## Ownership handoff

Main remains 7d464ea. Keep this optional research implementation on
`codex/overnight-dds-qualification`; do not merge it merely because full startup
now succeeds. The final selected/captured label is `night-dds-f04-original`,
which disables DDS read-ahead and keeps accepted searches/Gagarin selected.
Runtime 1d1528d remains deployed. Following closeout commits change documentation
only. The game is closed; no source/package/process work remains in flight.
The next agent must prepare a fresh label and explicitly deploy its intended
package before testing. OP7 remains incomplete and OP8 inactive.

Recommended next step: pursue the separately identified HugsLib work reduction
or another measured opportunity; stop DDS tuning unless materially new evidence
shows repeatable net savings in an actual target loading condition.
