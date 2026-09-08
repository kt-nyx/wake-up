> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `records/implementation/OP7_OVERNIGHT_REGISTRATION.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Overnight definition registration investigation

Historical branch handoff: deployed runtime and fixture selection below describe
this experiment, not the current fixture. See [the later restored state](loading-experiments-closeout.md).

This main-based research branch measures how long it actually takes to place
loaded definitions in the game's databases. Loading Progress's accumulated
worker intervals are not the complete phase's elapsed time. No registration
optimization has been implemented.

The optional `--registration timing` selector starts a timer before the native
definition-type enumeration and stops after the native parallel loop joins.
An exact, nongeneric DoPlayLoad callsite replacement preserves its callbacks
and original Parallel.ForEach. Initialization uses the existing Root.Start
bootstrap, before DoPlayLoad begins. Only the pinned physical game identity,
unique original registration markers and exact native call signatures, and an
owner without foreign transpilers are supported. Default off; refusal leaves the
accepted main startup searches and optional Gagarin feature intact.

Native AddAllInMods repeats mod sorting and OfType scans per database. However,
registration also invokes arbitrary PostSetIndices overrides on removal, so
precomputed ordering/membership cannot assume objects remain unchanged. The
first decision is the complete elapsed ceiling, before designing avoidance.

Initial checks: 36 focused managed checks and 24 fixture checks pass, no skips.
The tests verify callsite scope, unchanged other loops, completed native work
and native aggregated exception behavior. Private evidence is under
`artifacts/overnight-registration/`. Live evidence and the decision follow.

The first small launch, night-reg-a01-cost (1b854c4), reached the menu in
15.435318 seconds and exited normally with automaticTestPassed=true. Its timer
refused the raw loaded-IL hash, so it supplies no registration cost evidence.
Prepatcher serializes the assembly with remapped metadata tokens. The next
package uses the physical identity and scoped structural checks above; no
general instruction normalization or expanded instrumentation is introduced.

Main remains 7d464ea. Prior overnight DDS, PNG and XML negative records and
the scope-parked HugsLib record are carried as documentation only. All fixture
sizes are authorized for this overnight task while the PC is idle; historical
busy-PC restrictions are unchanged. OP7 remains incomplete; OP8 is inactive.

## Result and stopping decision

Complete native registration took **691.679 ms** on the full frozen selection,
including enumeration of 528 definition types, scheduling, native registration
and callbacks, and the completed parallel join. Repeated sorting and OfType
scans are only a subset of this already small elapsed ceiling. No grouping,
shared sorting, registration replacement or further profiler was implemented.
The cost question is closed without a new optimization.

| Captured label | Runtime | Complete registration | Menu observation | Result |
| --- | --- | ---: | ---: | --- |
| night-reg-a01-cost | 1b854c4 | refused, unmeasured | 15.435318 s | Normal exit 0, automatic pass |
| night-reg-a02-cost | 0d4dedb | 108.263 ms, 255 types | 9.575172 s | Normal exit 0, automatic pass |
| night-reg-f01-cost | 0d4dedb | 691.679 ms, 528 types | 429.744676 s | Normal exit 0, automatic pass |

Both measured receipts say `registration-complete`, `native-joined` and
`completeJoin=true`; merely installing a hook is not the evidence. Each package
was explicitly deployed with `--package`; candidate.json and prepared run
sourceRevision matched before launch. All selections used accepted main
startup-searches, Gagarin on, timing observation, fresh application caches,
the menu observer and normal automatic exit. The representative selection
contains 314 frozen enabled suppliers plus optimizer and observer.

The full menu time is anomalously slower than earlier approximately 190-second
full runs. There is no matched control and no total-startup performance claim.
The process continued consuming CPU and advancing its log during the long wait,
then exited normally; no forced closure occurred. The completed registration
measurement is approximately 0.16% of this run's total startup. This is an
elapsed ceiling on removing the entire phase, not a forecast achievable by
avoiding sorting or scans, and not a universal modlist average.

For contrast, Loading Progress recorded 20216.252 ms of accumulated off-thread
registration intervals and 640.601 ms in its active-thread category in this
same run. Those overlapping intervals must not be added to each other or
called a 20-second startup bottleneck. This direct complete measurement resolves
the earlier 10–16-second accumulated-interval lead.

The structural-check revision again passed 36 focused managed checks and 24
fixture checks, no skips. Its test rewrites the pinned original DoPlayLoad and
asserts exactly two changed operands, in addition to scoped synthetic cases and
native callback completion/aggregate failure checks. Native registration
semantics are unchanged; no exhaustive gameplay qualification was attempted.

Portable private summary: `artifacts/overnight-registration/summary.json`,
reproduced by `summarize.py` alongside check/build/deploy/prepare/launch logs.
Detailed receipts remain in each fixture capture's profile directory.
Final deployed and captured runtime is
`0d4dedb749c57b9710cff02aab13113ea9f54914`, last label night-reg-f01-cost.
The game is closed. The final record commit changes documentation only.
The research timer remains optional/default off on this research branch;
main 7d464ea is unchanged. Recommend restoring accepted main and stopping this
overnight search, retaining the negative and scope-parked records.
