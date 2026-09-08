> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `records/implementation/OP7_STARTUP_SEARCH_CLOSEOUT.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# OP7 startup search investigation closeout

Status: functional feature accepted by the owner on 2026-09-05 for integration
into main. Two runs per arm passed for both fresh application caches and reused
Gagarin caches. The game is closed; this acceptance does not publish a release
or extend the validation claims below.

## What changed and why

The original optimizer added expensive checks without avoiding the underlying
XML loading work. Its saved catalog still required reading and parsing each
file, followed by repeated identity checks and source reads. Finding a cached
catalog therefore added work without delivering the intended saving. Increasing
the number of loading workers and changing how XML nodes were visited did not
improve time to the measured loading endpoint in the small profile. Those
experiments remain separate; the new combined option does not enable them.

A buffered texture-reader experiment appeared dramatically faster at first.
Repeating the run with the optimizer completely absent reproduced that speed:
Windows had cached the texture files. We rejected that apparent improvement.
No normal-game or system caches were cleared to manufacture a comparison.

Concrete rejected results, all measured to the same cleanup endpoint within
their respective profiles:

- Small catalog: 13.581 s fresh and 13.472 s warm, versus 8.790-8.858 s absent.
- Small original-loader worker experiment: 9.133 s with four workers and
  9.426 s with eight, versus 9.219 s original-code timing. No useful gain.
- Generated XML traversal cursor: it activated, but did not improve total
  loading in the small profile; full-profile exploration moved to measured
  larger costs instead.
- Buffered DDS without extra texture timers: 358.878 s versus 350.832 s absent
  after Windows had cached files. The early apparent win was not reliable.

One early profiling run (r02) changed audio behavior because Mono shared native
code between closed generic methods. It was invalidated, the offending generic
profiling hooks were removed, and none of its timings support this result.

The successful isolated candidates instead avoid repeatedly searching the same
large collections during a single startup:

1. Definition searches find a named game definition once in the current XML
   document, then use the original XML library for the remaining expression.
   Edits to definition names or document membership invalidate the affected
   lookup table (index), so the next search rebuilds it. Unsupported queries
   retain the original search.
2. Harmony, the library mods use to modify game methods, sometimes searches all
   loaded software types to find one by name. The change retains its direct
   lookups and indexes this expensive last-resort search. Search order and
   missing-type behavior stay original. Loading a new assembly, such as a mod
   DLL, invalidates the index; assemblies generated at runtime are examined
   afresh on each fallback search.
3. Character Editor preset creation reuses its original dictionary mapping
   turret names to gun definitions within each object or turret creation call.
   Reuse ends when that call returns. The game still creates every gene, object
   and turret preset and executes the original constructors and callbacks.

The `startup-searches` launch option combines these three independently guarded
changes. It adds no background workers and writes no persistent optimization
cache. Repeated fixture validation is complete for this frozen input. It is
now the fixture workflow default strategy; it has not been released.

## How the comparison is controlled

Every representative comparison uses the frozen 315 enabled packages, plus
RimWorld Loading Optimizer (RLO) where tested. In absent runs, RLO is physically
outside the game's Mods directory and is omitted from both mod order and
command line. Runs that retain the original code with added timers help locate
the cost of individual stages; they do not replace absent comparisons.
The fixed game is the accepted DRM-free fixture. Its exact game assembly hash is
`4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28`.

**Fresh** means a newly prepared fixture application profile with no restored
mod cache. Windows' filesystem cache, which retains recently read file data,
is uncontrolled and has already been exercised by prior runs. **Warm** means
only the captured MissileGirl/Gagarin mod cache was explicitly restored into a
newly prepared matching run; its actual cache-hit log is checked. Neither term
claims a cold operating system cache or a first launch after reboot.

Loading Progress's clock ends before late queued mod initialization. The Unity
startup-cleanup endpoint has the same limitation. Observations of the
unobscured, responsive main menu bound the total loading time between the last
unfinished observation and the first ready observation. Neither process
lifetime nor time spent waiting at the menu is counted as loading. Debug
overlays are dismissed, and late Character Editor work must finish. These
observation intervals are reported rather than treated as exact timings.

No builds, tests, large hashes or asset scans run alongside measured launches.
The frozen Loading Progress instrumentation stays identical in every full run;
RLO timing-only mode omits the earlier detailed diagnostic pipeline. Additional
texture-loader profiling is off. Timers for individual stages and creation
calls are separately identified.

## Results

Two fresh-application-cache runs per arm reproduced the improvement. The
Loading Progress mean fell from **339.959 s to 152.623 s (55.1%)**. The external
cleanup mean fell from **352.039 s to 165.456 s (53.0%)**. These endpoints exclude
late mod initialization; the separate menu observations show the practical
loading improvement as well.

| Fresh application-cache run | RLO | Loading Progress | Cleanup | Usable menu observation bounds |
| --- | --- | ---: | ---: | ---: |
| r05 | Physically absent | 338.931 s | 350.832 s | 437.230-478.070 s |
| r13 | Physically absent | 340.987 s | 353.245 s | 475.312-491.461 s |
| r12 | Combined | 152.773 s | 165.468 s | 222.881-238.444 s |
| r14 | Combined | 152.472 s | 165.444 s | 211.946-232.453 s |

Both candidate menus were observed by 238.444 s, while both absent runs were
still unresponsive at or after 437.230 s: a conservative 198.786 s separation
between the observation bounds. These are repeated comparable runs on this
machine, not a statistical guarantee for other computers or profiles.

The improvement also repeats with Gagarin's XML cache already available. The
warm Loading Progress mean fell from **234.885 s to 125.761 s (46.5%)**; cleanup
fell from **246.959 s to 138.699 s (43.8%)**. All four runs confirmed a cache hit.

| Warm application-cache run | RLO | Loading Progress | Cleanup | Usable menu observation bounds |
| --- | --- | ---: | ---: | ---: |
| r07 | Physically absent | 236.208 s | 248.440 s | 356.731-386.776 s |
| r16 | Physically absent | 233.563 s | 245.479 s | 367.513-379.801 s |
| r15 | Combined | 124.596 s | 137.359 s | 188.307-200.200 s |
| r17 | Combined | 126.926 s | 140.040 s | 190.355-202.472 s |

In practical terms, observed usable-menu times went from roughly **8 minutes
to 4 minutes** with fresh application caches, and **6 minutes 20 seconds to
3 minutes 20 seconds** with Gagarin's cache restored. Every menu interval above
is relative to the second-rounded recorded launch time, so allow up to one
additional second of timestamp rounding. No menu dwell is counted.

| Measured work | Original-code timing run r11 | Combined run r12 |
| --- | ---: | ---: |
| XML patch stage | 98.195 s | 20.062 s |
| 791 `TypeByName` calls, 119 missing results, 0 errors | 128.621 s | 7.876 s |
| 9,128 Character Editor object presets | 60.355 s | 1.872 s |
| 82 Character Editor turret presets | 0.970 s | 0.694 s |

These timed calls can overlap other loading work and must not be summed to
claim a total loading-time saving. The separate whole-run observations measure
that change.
Auxiliary original-code timing run r11 measured 340.029 s in Loading Progress
and 352.612 s to cleanup, versus 338.931 s and 350.832 s respectively for absent
r05. Those differences of 1.098 s and 1.780 s include ordinary variation; they
are not exact bounds on the cost of the timers.

Isolated evidence and all rejected experiments are retained in [the investigation record](initial-loading-investigation.md).
Raw results stay in the ignored permanent fixture and are never committed.
The final machine-readable summary is
`Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\results\perf-r17-searches-warm-repeat\eight-run-summary.json`.
Full labels are `perf-r05-absent`, `perf-r13-absent-repeat`,
`perf-r12-searches-cold`, `perf-r14-searches-cold-repeat`,
`perf-r07-absent-gagarin-warm`, `perf-r16-absent-warm-repeat`,
`perf-r15-searches-warm`, and `perf-r17-searches-warm-repeat`. Each result
contains its captured run, operator observations and comparison evidence.

## Correctness and resource boundaries

The XML after patching and the merged XML before patching were byte-identical
across all eight final comparisons and isolated r08/r09/r10. The four
per-definition reports also matched. Within each cache condition, functional
status lines matched and candidates introduced no new logged problem patterns. The full profile retained 47,449 parsed
definitions, 14,748 patch operations, 2,199 gene
presets, 9,128 object presets, and 82 turret presets in these fresh-cache runs.
This is direct output evidence for the frozen input; it is not a gameplay test.
Gagarin cache-hit runs report 45,449 parsed definitions and bypass the ordinary XML patching
work through their existing behavior, so warm results are compared with warm.

Recorded fresh candidate whole-process peaks were 9,113,755,648 and
9,187,667,968 bytes, versus 8,445,308,928 bytes in absent r13: about 668-742 MB
higher (7.9-8.8%). Original-code timing r11 peaked at 8,567,468,032 bytes.
These are process peaks affected by garbage collection and allocation timing,
not measurements of index allocation alone. The machine has 32 GiB RAM.

Warm process peaks varied more: 9,583,181,824 and 8,832,831,488 bytes with RLO,
versus 8,591,478,784 bytes in absent r16 (about 2.8-11.5% higher). This variation
is a reason not to claim one precise memory cost from process peaks alone.

The type index caps at 250,000 types and 16 million name characters; the XML
index caps at 65,536 entries and 4,096 cached parsed queries; the preset
dictionary caps at 4,096 entries. These counts bound index growth, not the
entire game's memory.
Indexes are discarded at the end of their startup stage or creation call. No
extra worker pool is created. Each component checks the game and library
versions it depends on and the relevant modifications made by other mods. When
those checks refuse optimization, it retains the original work; that refusal
does not prevent the other components from initializing independently.

Tests: 43 focused runtime tests and 17 fixture workflow checks passed before
combined qualification; the combined build had zero warnings or errors, and its
small-profile live launch reached the menu with all three components active.
Combined r12/r14 output review passed with identical XML and report hashes,
counts, and sets of logged problem patterns against r05/r13 absent and r11
original-code timing. Fresh and warm repetition are complete. Warm output
comparisons retain Gagarin's own behavior. No new concrete combined-code bug was found in
independent review.

## Remaining limitations and next steps

The small-profile combined smoke run took 9.844 s to cleanup and did not show
a total-loading gain over the earlier small controls (roughly 8.8-9.3 s).
The measured benefit is for this substantial frozen mod profile. Other mod
versions, changed third-party patches, and unknown game binaries may retain
original behavior through the compatibility checks and therefore lose the gain.

No existing saves were loaded, no gameplay acceptance is claimed, and no release
was published. The normal Steam installation, Workshop packages, normal profile,
saves and caches remain outside the task. Latest-Steam validation follows MVP,
using the then-current game and separately admitted supplier identities.

The tested runtime source is `65f641736917246bd0225f823d79cb2b0542fb87` on
`codex/op7-fixture-runtime-integration`; later commits record evidence and
closeout documentation. Its deployed package remains available at
`C:\rlo-fixture\artifacts\op7-fixture-package\65f641736917246bd0225f823d79cb2b0542fb87`.
To reproduce with that deployed package, run from `C:\rlo-fixture`, use fresh
unique labels, observe the usable menu, load no save, and quit normally:

```powershell
python scripts/op7_fixture.py prepare --mode candidate --lane representative --strategy startup-searches --observation timing --label verify-searches-next01
python scripts/op7_fixture.py launch --label verify-searches-next01 --authorize-live-launch
python scripts/op7_fixture.py prepare --mode absent --lane representative --observation timing --label verify-absent-next01
python scripts/op7_fixture.py launch --label verify-absent-next01 --authorize-live-launch
```

For warm comparisons, add `--foreign-cache-from verify-searches-next01` to a
new candidate preparation and `--foreign-cache-from verify-absent-next01` to a
new absent preparation. Never reuse a result label. A rebuilt package needs a
new matching candidate cache parent; the workflow checks that identity.

Next product steps after this investigation:

1. Exercise a newly created fixture colony for gameplay smoke coverage; existing
   saves remain untouched. Startup output equivalence is already checked, but
   this investigation does not certify gameplay.
2. Carry the measured strategy into MVP packaging. The fixture workflow now
   defaults to `startup-searches`; explicit selection in the commands above
   makes the experiment reproducible. Keep rejected experiments out of any
   claimed performance gain.
3. After MVP, admit and test the then-current Steam build and changed mod
   suppliers separately. The pinned GOG result does not grant compatibility
   authority to those binaries.

Final checks: all eight comparison runs exited normally with code 0. The last
fixture window and process were confirmed absent at 07:00:09 UTC on 2026-09-05.
The 17 fixture workflow tests passed after the default changed. Final metadata
audit passed for 309 physical mod packages and 315 original enabled packages,
with manifest `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d`
and the deployed runtime matching. This was a metadata audit, not a new full
content hash of every frozen mod. Output equivalence uses hashes recorded by
each post-exit capture.

Other worktrees and all 12 existing stashes were preserved. The main checkout
remains at `49bbdf0` with its pre-existing `.gitignore` edit. No fixture payload
is tracked, copied into another fixture, or published. The final commit records
this closeout and leaves `C:\rlo-fixture` clean.
