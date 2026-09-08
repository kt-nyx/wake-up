> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `records/implementation/OP7_STARTUP_WORK_REDUCTION.md` at `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Startup work beyond the accepted searches

Investigation checkpoint, 2026-09-05. Work starts from accepted main `7785ddb`
on `codex/startup-work-reduction` in the single Z: checkout. The accepted
`startup-searches` feature remains enabled in every new prototype comparison.
No release, gameplay qualification or latest-Steam claim is made.

## Automatic testing and the debug overlay

The owner identified the replacement debug window and requested removal of its
supplier from the fixture. Its exact identity is Modern Dev Tools,
`astryl.moderndevtools`, frozen folder `3771602203`. No other frozen package
declares it a required dependency. The new `exclude-mod` command physically
parks it outside game/Mods, records the exclusion and preserves the immutable
snapshot. `restore-mod` reverses this. Neither operation changes normal mods.
Current representative selection is 314 original packages, plus RLO and the
independent observer. All new comparisons retain that same selection; removing
this mod is not counted as an optimizer improvement.

The original fixture preference had `runInBackground=False`. Pinned game IL
shows Root.Start temporarily enables background updates, but Root.Update later
calls Prefs.Apply, which reinstates that false preference. Automatic preparation
now sets it true in the staged fixture profile and verifies the setting before
launch. This explains why early minimized loading can work while late startup
pauses. The normal profile and immutable seed are unchanged. Both this setting
and the mod exclusion changed before the successful repeat, so the repeat does
not isolate which change resolved the owner's observed obstruction.

The first run (`work-r01-searches-control`) required owner intervention and is
invalid as a performance comparison. Its native stack was waiting in
UnityPlayer/user32.GetMessageA; the last mod log line was not proof of a managed
deadlock. Evidence is retained under ignored artifacts/startup-work-reduction.
The owner subsequently authorized force-closing fixture RimWorld processes if
needed without window control or interruption of other applications. Routine
runs still use the observer's normal exit. The later failed r08 graphics run
required one verified fixture-only forced termination, documented below.

## Measurements and limits

The exact endpoint is the observer's first completed menu repaint, not cleanup,
Loading Progress completion, shutdown or process lifetime. The observer source
stays `f26270bc6fe7c7ad29664ce9a84aff50527a9b16` in all arms.

An optional external sampler records this game's process/thread CPU, I/O,
memory occupancy and system contention every five seconds, plus menu/exit
samples. Process CPU is actual accumulated processor work; dividing its change
by elapsed time gives the equivalent number of busy cores. Samples already show
up to six busy cores, so startup is not uniformly single-threaded. Scope
placement in Loading Progress alone cannot prove concurrency.

The sampler's own CPU is recorded separately: 4.984 s during r02's 512.737 s
load and 1.906 s during r03's 204.259 s load. Its menu counter is a nearby poll,
not a counter read at the exact in-game timestamp. Memory occupancy is not
allocated bytes. Unity Mono exposes a zero-returning per-thread allocation
counter stub; probes now report that counter unavailable, rather than claiming
zero allocation. Mapped-file traffic is not all visible in ordinary read-byte
counters; page-fault counts include soft faults, not just storage reads.

Both r02/r03 used fresh application caches, accepted searches, the same 314
packages, HugsLib scope timing and optional broad work timing. Windows file
caching and competing workloads remained uncontrolled. The broad work-timing
prototype refused installation on this full profile and removed its patches;
these runs provide no valid detailed callback timing from that prototype.

| Run | Change beyond accepted searches | Menu seconds | Game CPU seconds | HugsLib wall / thread CPU seconds |
| --- | --- | ---: | ---: | ---: |
| work-r02-control-diagnostic | Original HugsLib with scope timer | 512.737 | 347.766 | 4.154 / 1.063 |
| work-r03-filter-diagnostic | HugsLib filter requested, guard fell back | 204.259 | 297.000 | 5.099 / 1.078 |
| work-r04-hugs-filter-revised | Narrower guard; Performance Optimizer still refused | 678.987 | 474.063 | 2.920 / 1.422 |
| work-r05-original-matched | Original report, exact known-PO support available | 593.265 | 479.063 | 2.260 / 1.516 |
| work-r06-filter-matched | Filter active, known PO stripping preserved | 574.782 | 463.156 | 0.258 / 0.031 |

All passed automatic testing and exited normally. Patched/original combined
XML and all four captured definition reports are byte-identical. The early
r02/r03/r04 filter requests fell back, so their wall-time differences are
**not optimization results**. The later r05/r06 pair used identical package
`9a85a43cbfaa5126abca56e09b51147219d24a4d`, content, fresh application caches,
observer and diagnostics. The active r06 filter examined 4,097 methods, retained
one obsolete method and avoided 4,096 patch-info reads. Report CPU fell from
1,515.625 ms to 31.250 ms (97.9%); its wall scope fell from 2,259.779 ms to
257.794 ms. This is a confirmed small work reduction, not proof of a substantial
overall startup gain. Do not assign the whole 18.483-second menu difference or
15.906-second process CPU difference to it. Sampler CPU was 5.375/5.250 seconds;
minimum free physical memory was 591/355 MB, respectively.
Both record 316 loaded packages including RLO/observer, 47,388 definitions and
14,748 patch operations. XML Extensions reports 16,878 operations and zero
failures in both. Reported symbol, terrain, animal, landform, building and weapon
counts also match. After removing time/address noise, the kinds and counts of
logged problem messages match; existing CE injection, TakeCover/Achtung,
translation, piano and weapon-tweak messages are not new regressions.
The r04 sample minimum available physical memory was only 410 MB and sampler
CPU was 6.438 s; its total wall/CPU variation must not be assigned to the filter.

The detailed r04 refusal identified two concrete compatibility issues. The
diagnostic queue hook reached Harmony's `FaultBlockRewriter` exception,
"Unbalanced exception markers", on Loading Progress's iterator. That redundant
hook was removed in the next build; the remaining diagnostic was subsequently
retired when it did not produce usable totals. The HugsLib query's
`HasActivePatches` method has a Performance Optimizer
prefix. Its exact frozen implementation strips its own internal transpiler from
the temporary deserialized report object. Supporting that inspected local
mutation can preserve the original full-profile report without permitting
arbitrary unknown query changes.

## Where work remains

Rank depends strongly on file-cache and memory conditions. In slow r02,
texture content loading took 281.72 s, versus 9.75 s in historical warmed r14;
this explains about 86% of the Loading Progress increase. Many texture mods
were affected. Most of the long region used only 0.14–0.27 CPU-core equivalents:
waiting, rather than lack of enough CPU workers, dominated that interval.

Other r02 scopes were mod constructors 27.59 s, XML patch operations 25.83 s,
static constructors 23.68 s, XML processing 13.38 s, raw XML loading 12.38 s,
audio 11.83 s and ThingDef graphics callbacks 9.57 s. These scopes can overlap;
they must not be summed into a new loading endpoint. Historical warmed runs put
raw XML reading near 1.7 s, limiting the likely benefit of generic file-reader
worker changes. Earlier small worker-count tests therefore remain relevant but
do not disprove all concurrency ideas.

Whole graphics callbacks modify shared requester state, Unity objects and
atlases. XML patches mutate shared ordered documents; arbitrary mod constructors
install patches and touch global state. Moving these entire stages to workers
would need a demonstrated independent subset, not simply more worker threads.

Two smaller inspected opportunities remain hypotheses: Giddy-Up copies a whole
texture to inspect only its center alpha column (less than its four-second
static-constructor total is available), and Color Coded Mood Bar repeats an
assembly type enumeration whose first result could be reused locally (less
than its 1.6-second constructor total). Neither is a measured improvement.

## Non-cache prototypes

HugsLib's obsolete-patch report reads Harmony's patch records for every patched
method before rejecting methods without the Obsolete attribute. The prototype
moves the same metadata filter before expensive patch-record reads, preserving
lazy order, owner grouping and warning content. It retains original handling
for unusual method representations and unknown patch chains. Exact supplier,
game, Harmony and method bodies are checked. The small profile passed both
arms, avoiding 56 patch reads, but saved only milliseconds; total menu variation
there is not attributed to this change. Exact support for Performance
Optimizer's existing local report mutation is now covered by physical supplier
hash, loaded MVID, six relevant loaded IL bodies, exact target/prefix/owner
admission and unchanged patch-state stamps. Unknown hooks still refuse. The
small lane includes Performance Optimizer: h03 original/h04 filtered both pass;
h04 avoids 102 reads, with no obsolete records in that small selection.

The bounded DDS reader was reconsidered because of the new large measured
texture wait. Its previous apparent benefit was correctly rejected as Windows
file-cache warming. The additive `--buffered-dds` flag keeps all three accepted
searches; it does not replace their strategy. The reader uses at most two 8 MiB
buffers, retains the original DDS parser and Unity upload, and falls back to
mapped reading for unsupported cases.

Windows reports a restart at 13:42:41 local time between r03 and the following
series. The interrupted focused test was rerun: five HugsLib checks passed.
The new package is `0b4d04b35b873261cd0360631ec0d840e2fd6cfa`.
All three texture-lane runs below used it, fresh application caches, accepted
searches, texture timers and process sampling. Each loaded the same 2,853 DDS
files, had zero DDS errors, reached the menu and exited normally.

| Small texture run | Reader | Menu seconds | DDS load seconds | Nested texture creation seconds |
| --- | --- | ---: | ---: | ---: |
| work-t01-dds-control | Original mapped | 44.191 | 18.624 | 0.611 |
| work-t02-dds-buffered | Buffered | 17.322 | 0.745 | 0.087 |
| work-t03-dds-control-repeat | Original mapped again | 17.451 | 0.766 | 0.296 |

The candidate buffered 293,331,836 bytes, used one buffer, left zero outstanding
leases and needed only one patch-info read. But the repeated original reader
reproduced the speed, leaving only 21 ms difference in DDS loading and 129 ms
at menu. This was not a worthwhile demonstrated gain and initially stopped the
reader experiment. The dramatic first difference is consistent with Windows
file caching, not an optimizer win. The optional flag remains off by default.

### Full-profile texture attribution under pressure

Repeated full controls remained slow even after previous reads of the same
collection. Therefore the small profile's fit-in-memory result did not settle
the larger case. Diagnostic r07 (`work-r07-texture-original`) on package
`fc3d2a16d2f2a5d678dceedd7b52d61c700479bd` used accepted searches plus the now
measured HugsLib filter, existing texture timers and the process sampler. The
failed broad timer was physically removed from this package. Menu was
571.346437 s, process CPU 459.375 s, minimum free physical memory 1.643 GB and
sampler CPU 7.156 s. All content counts and XML/report hashes still match.

It loaded 39,975 DDS files: 268.879 s in the loader, with 18.391 s in its nested
texture-creation calls. Thus approximately 250.489 s was outside texture
creation, predominantly the mapped source path and parsing; this is not a pure
physical-disk counter. Total Loading Progress texture scopes were 271.640 s.
This measured full-profile result justifies revisiting the existing bounded
reader under pressure despite its warmed small-profile result. Subsequent
reader arms keep HugsLib filtering on in both, along with identical content,
application-cache policy, observer and diagnostic settings; only `--buffered-dds`
changes. Repeat the original reader after the candidate before assigning a win.

R08 (`work-r08-texture-buffered`) used that same package and comparison settings,
but failed before recording menu readiness. Unity logged repeated D3D11 texture,
render-target and buffer creation failures with `0x887A0005`, which Microsoft
identifies as [DXGI_ERROR_DEVICE_REMOVED](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-error).
Windows recorded NVIDIA `nvlddmkm` event 153 at 15:05:43 local time, with
`Error occurred on GPUID: 100`. This corroborates a graphics-device failure;
it does not identify its cause or exonerate the prototype. The buffered reader
had activated, but neither it nor the texture timer published final totals.
There is no valid DDS throughput, content-equivalence or menu result from r08.

After progress stalled, the owner-authorized forced close targeted only PID
32224 after checking its executable and command line against this fixture.
Player.log and driver-event details were preserved first. The existing launcher
then waited for process exit and completed capture: `automaticTestPassed=false`,
no menu event, exit code 4294967295, captured at 19:08:25 UTC. The external
sampler recorded 6.625 CPU seconds and zero sampler errors. See ignored
`artifacts/startup-work-reduction/r08-*` and fixture `results/work-r08-texture-buffered`.
No other process, window or driver was controlled. Further live reader repeats
were stopped after this concrete graphics failure; the reader remains optional,
off by default and **not qualified by this full-profile experiment**.

A bounded independent review found no concrete pointer-lifetime or content
defect in the additive wiring or existing reader. The focused original-method
audit passed. In the pinned game's `CreateTexture`, a pinned `System.Byte&`
local retains the managed payload through `LoadRawTextureData`; `Apply` finishes
before `TryLoadDds` releases the lease. The original BGR path already supplies
pooled managed arrays through that method. Payload spans exclude unused buffer
capacity, and pool closure does not invalidate outstanding leases. Evidence:
`artifacts/startup-work-reduction/dds-pointer-audit.txt`. This review narrows the
failure investigation but cannot exclude timing, resource or driver effects.

R07 also repeats the HugsLib result: 4,093 patch reads avoided, one obsolete
method retained, 15.625 ms thread CPU and 277.666 ms wall, without a new error.
The small count difference from r06 reflects removal of broad diagnostic
patches and addition of the two texture timing targets.

## Diagnostic stopping decision

The native callback timer installed after removing the incompatible iterator
hook, but still did not publish usable totals in small h03/h04 or full r05.
Further menu-hook changes also failed to resolve it. This diagnostic was
removed from source and the fixture CLI rather than extended into another
infrastructure task. Its historical captures remain intact. No callback totals
or allocation estimates from it support any performance claim. The accepted
menu observer, existing Loading Progress, HugsLib timer and external process
sampler remain the evidence sources. The r05/r06 pair deliberately uses the
same earlier package/settings so its comparison is not changed mid-run.

Performance Optimizer also has one background task for serial discovery of
component-access calls. It then installs patches in a Unity coroutine that
yields after roughly 1 ms of work. Inspection found no menu/loading completion
dependency on this coroutine; its handle is discarded and work can continue
after menu. Larger batches could delay the first repaint, so batching is not
changed without evidence of a useful dependency. This is implemented background
overlap, not proof of that task's share of the observed multicore CPU samples.

## Verification so far

The original adapter run passed 367 checks with one failure in the new HugsLib
differential test's tuple-enumeration assumption. That test was corrected; all
four focused HugsLib checks then passed, including the actual pinned report
pipeline and output equality. The 118 core checks and five offline fixture
runtime contracts passed. Fixture/process Python checks passed (27 before the
DDS flag, then 28 fixture/process checks with it). The exact Performance
Optimizer admission and unchanged Character Editor guard passed 15 focused
checks, including actual report-output comparison with the stripping prefix.
The small HugsLib lane now includes that frozen compatibility supplier. Product builds
completed without warnings or errors. No heavy builds/scans overlap measured
game runs. Detailed logs and intermediate summaries are ignored artifacts.
After the failed graphics run was closed, a final focused build/test passed all
20 DDS/HugsLib checks with zero skips (`final-focused.log` and
`final-focused.trx`). These managed tests do not qualify Unity/GPU behavior.

## Outcome and continuation

The confirmed product result is the small HugsLib work reduction: about 4,100
unneeded patch-record reads and 1.5 CPU seconds removed on the complete selected
profile, preserving the actual obsolete-method warning. Repeated filtered runs
remain near 16–31 ms report thread CPU. No substantial total startup improvement
is established beyond the already accepted caching feature. HugsLib filtering
is explicitly selected with `--hugslib-filter on`; default behavior is unchanged.

The largest remaining opportunity is DDS texture source loading, then mod static
constructors, mod constructors and XML patches. On r07 those measured scopes are
268.879, 40.621, 32.931 and 32.825 seconds respectively (the latter two are close
and reorder in other runs). Raw definition reads took 17.584 seconds and audio
13.595 seconds. These inclusive scopes can overlap and are not additive. CPU
work, file waiting and graphics/memory pressure must be distinguished before
choosing additional workers. The smaller Giddy-Up and type-enumeration ideas
remain untested hypotheses, not promised savings.

Continue from this branch and deployed tested package
`fc3d2a16d2f2a5d678dceedd7b52d61c700479bd`; later documentation commits do not
change those binaries. Keep Modern Dev Tools excluded and the automatic
background preference. The fixture was left closed and prepared as
`work-r09-texture-original-repeat`, with the original DDS reader, accepted
searches and HugsLib filtering; this label has not been launched. Its
offline `verify-prepared` passed (`final-preflight.log`). The next
useful live measurement is a repeat of original
DDS reading with the r07 settings after the graphics failure has been resolved:

```powershell
python scripts/op7_fixture.py prepare --label work-r09-texture-original-repeat --mode candidate --lane representative --observation timing --process-metrics --texture-timing --hugslib-filter on --menu-observer --exit-after-menu-ready
python scripts/op7_fixture.py verify-prepared --label work-r09-texture-original-repeat
python scripts/op7_fixture.py launch --label work-r09-texture-original-repeat --authorize-live-launch
```

Do not revive a buffered-reader speed claim from r08. Investigate its graphics
failure first; if safe to retry, use the same package/settings with only
`--buffered-dds` added, followed by the original reader again. An otherwise idle
PC comparison is needed before attributing a total wall-time improvement. Do not
stop or alter other applications to manufacture that condition. Existing content
counts, XML/report hashes and warning comparison are sufficient for the HugsLib
startup change; gameplay and latest-Steam qualification remain later work.
