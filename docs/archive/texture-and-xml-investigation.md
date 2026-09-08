> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `records/implementation/OP7_TEXTURE_XML_NEXT.md` at `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Texture read-ahead and remaining XML work

Measured implementation checkpoint, 2026-09-05. Continues the clean
`codex/startup-work-reduction` checkout at `62785f0`; accepted startup searches
and the preceding HugsLib improvement remain the comparison baseline. Read
`OP7_STARTUP_WORK_REDUCTION.md` for the earlier results and graphics failure.

## Result and reliability limit

**Promising measured texture improvement, not ready for routine enablement.**
Two full two-reader runs completed normally, but the final original-XML repeat
failed to reach the menu after 941.647 seconds and required an owner-authorized
fixture-only forced close. Its cause is unresolved. Keep the feature opt-in and
off by default until that late-startup failure is understood.

Two bounded file readers substantially reduce the time spent loading DDS
textures on the full fixture. They prepare upcoming bytes together while the
game still creates and publishes each texture in its original order. This is
overlapping file work within one startup, not another persistent cache.

The matched original DDS stage took 340.099 seconds; two readers took 146.897
seconds, and a subsequent run with the same readers took 144.767 seconds.
That is about 57% less elapsed time in this stage. Total menu time varies much
more because later startup work and competing applications also vary. Do not
promise a fixed whole-startup percentage from these runs.

The two XML algorithms did not produce a reliable full-profile improvement.
Keep original XML behavior. The direct named-parent scan saved about half a
second in its targeted query families, but the complete XML stage's CPU cost
was essentially unchanged. The long-name-list rewrite was also effectively
unchanged. Both remain opt-in research code, off by default and not recommended.

Tested implementation: `af21fea`. Promising experimental fixture configuration:
`--mode candidate --strategy startup-searches --hugslib-filter on
--buffered-dds --dds-read-ahead --dds-read-ahead-workers 2`.
For comparable measurements add `--observation timing --process-metrics
--texture-timing --xml-query timing --menu-observer --exit-after-menu-ready`
in every arm. Omit the buffered/read-ahead flags for the original DDS control.
The feature remains opt-in; this checkpoint does not publish a release or
complete OP7/latest-Steam qualification.

## Full-profile measurements

All labels below are under `.rlo-test-instance/results/`. Times are seconds.
The menu endpoint is the observer's first completed main-menu repaint; the
one-second exit delay and shutdown are excluded. DDS is a nested stage, so do
not add it to menu time. CPU seconds measure work accumulated by the process
or the XML calling thread, rather than time on the clock.

| Run label (prefix `next-`) | DDS reader / XML | DDS elapsed | XML elapsed / CPU | Menu elapsed |
| --- | --- | ---: | ---: | ---: |
| r01-xml-original | Original / original, earlier diagnostics | 332.406 | 40.271 / unavailable | 684.502 |
| r02-read-ahead | Synchronous buffered; read-ahead inactive | 295.315 | 36.308 / unavailable | 613.338 |
| r03-active-read-ahead | One reader / original | 230.325 | 34.891 / unavailable | 543.026 |
| r04-original-matched | Original / original | 340.099 | 36.363 / 34.734 | 660.992 |
| r05-two-readers | Two readers / original | 146.897 | 37.024 / 35.578 | 489.394 |
| r06-combined | Two readers / both XML experiments | 144.767 | 37.638 / 35.469 | 607.722 |
| r07-two-readers-repeat | Two readers / original | Final receipt absent | 39.193 / 37.234 | **Failed; no menu** |

R04 onward use the same `af21fea` package and diagnostic settings except the
explicit XML algorithm switch in r06. Earlier arms informed the design and
are not a perfectly matched final package comparison.

R04 versus r05 saved 171.597 seconds to menu (26%) in that pair, but r06's
later work was much longer despite almost identical DDS time. The long pause
occurred near late cleanup logging while process CPU continued advancing.
The final Unity unload/marking report itself was only about two seconds;
the whole delay cannot be attributed to garbage collection from that marker.

R04 and r05 used 514.219 and 533.563 game CPU seconds at the menu observation.
This optimization reduced waiting, not total CPU work. Their external sampler
used 6.500 and 5.078 CPU seconds respectively, with no sampling errors.
Two readers consumed 39,959 buffered files (5,078,043,867 bytes), with 16 original
mapped fallbacks, two peak 8 MiB buffers, zero outstanding leases and zero DDS
exceptions. All 39,975 DDS textures were still loaded. Consumer waiting fell
from 218.660 seconds with one reader to 135.409 and 133.522 seconds with two.
Source acquisition includes file opening, scheduling, filesystem filters and
competition; these counters do not isolate physical disk latency.

Process read-byte counters were 1.757 GB in r04 and 6.843 GB in r05. The original
uses memory-mapped files while the prototype issues explicit reads, so these
counters account for the paths differently; this is not evidence of more or
less physical storage traffic. The prototype still requests the same 5.078 GB
of eligible DDS data. Peak sampled CPU use was 5.78 and 5.14 core-equivalents
respectively, confirming that other startup stages already run across cores.
R06 used 685.688 game CPU seconds and 8.578 sampler CPU seconds, reinforcing
the large unrelated whole-startup variation. Minimum available system RAM in
r04/r05/r06 was 0.492/0.240/0.857 GiB; these are whole-run minima, not DDS-only
memory usage or additional memory caused by the two bounded buffers.

### Final repeat failure

R07 retained the original XML behavior and the same package, observer and two
readers. It stopped advancing its log near LunarFramework/Unity unused-asset
cleanup and `Starting patches: 1066`. A worker accumulated roughly one CPU
second per wall second while the main thread was mostly waiting. Background
updates were verified enabled. There was no menu or cleanup event and no logged
D3D11 device error. The final DDS aggregate receipt was never emitted, so this
run supplies no valid DDS duration or completed-content count.

The standard Windows Performance Recorder CPU profile was attempted after the
run had already become abnormal. It refused with `0xc5585011`, "Failed to enable
the policy to profile system performance". Status confirmed no recording before
and after. No elevation, security-policy change, exclusion or alternate process
inspection injection was attempted. The documented standard profiler route is
[Microsoft WPR](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/wpr-command-line-options);
resolving its profiling permission requires an appropriately authorized host
session, not more automatic fixture launches in the present session.

At 941.647 seconds the verified fixture PID 30628 was force-closed under the
owner's standing authority. `op7_fixture.py launch` waited for actual exit and
finished evidence capture; `automaticTestPassed=false`, exit code 4294967295.
The forced-close note is `artifacts/startup-next/r07-failed-repeat.json`.
This does not prove a DDS defect, nor prove that competing workloads caused it.
It prevents recommending routine enablement from this checkpoint. No subsequent
game was launched. The fixture is closed.

Profiles were freshly prepared without restoring Gagarin application caches;
the logs record cache creation/misses. Windows filesystem caching was neither
flushed nor controlled. Other applications stayed untouched. GPU samples during
r04–r06 were 83–95% busy, with roughly 8.7–9.9 GiB of 16 GiB occupied. Low free
RAM and variable late work also limit wall-clock attribution. Managed allocation
volume is unavailable from this Mono runtime; memory occupancy and page-fault
counters must not be presented as allocation measurements.

## Remaining work, ranked by measured opportunity

1. **Getting texture bytes into memory:** still about 145 seconds with two
   readers, including about 134 seconds waiting for upcoming data. This is the
   largest demonstrated remaining stage. More readers or buffers are hypotheses,
   not established improvements; first confirm the current result on an idle PC.
2. **Mod startup and construction:** the original full capture attributed about
   41 seconds to late mod startup and 38 seconds to constructing mod objects.
   Later runs show this area can vary substantially. Individual expensive mods
   need narrow attribution before safe changes; arbitrary parallel execution
   can change shared game state and initialization order.
3. **Applying XML patches:** about 35 CPU seconds and 36–40 elapsed seconds.
   Combat Extended dominated an earlier per-mod capture. The tested direct scan
   and long-name-list rewrite did not materially lower the complete stage.
   A different target would need a demonstrated expensive operation; wholesale
   concurrent edits of a shared XML document are not a safe shortcut.
4. **Turning XML into game definitions:** about 21 seconds in the original
   capture, followed by audio loading (~16), graphics preparation (~13), raw
   definition-file loading (~13), cross-references (~7) and texture atlases (~6).
   These are follow-up opportunities, not measured implementations here.

These approximate scopes come from r01 and can overlap; they are not a sum of
startup time. RimWorld already uses multiple cores in some stages. The successful
change targets measured file waits, rather than assuming the entire loader is
single-threaded or increasing every worker count.

## Implementation and earlier experimental checkpoints

The following chronology preserves decisions made before the final comparisons
above; its prospective statements describe those earlier checkpoints.

Texture read-ahead prepares upcoming file bytes while the original thread
processes the current texture. It adds one or two file-reading workers, uses the existing
two 8 MiB buffer limit, and obtains file order from the game's existing dictionary.
It does not patch generic loaders, start additional scans, move graphics calls
to workers, change texture content or retain data for another startup.

The opt-in `--buffered-dds --dds-read-ahead` flags retain accepted searches.
Without read-ahead, `--buffered-dds` still selects the synchronous reader; omit
both for the original mapped reader. Scope is the original non-generic
`ModContentPack.ReloadContentInt(false)`, with the exact texture extension
predicate and `Textures/` dictionary from `GetAllFilesForMod`. Every consumed
file must match the expected path, length and modification time. Unsupported
files and unexpected order fall back. Scope exit cancels queued work, joins the
readers and returns their leases. An OS-stalled synchronous file read can
delay that join; no worker is abandoned or forcibly aborted.

The first full read-ahead-labelled run, `next-r02-read-ahead`, passed at
613.338376 s but reported **zero read-ahead scopes and reads**. It is a
synchronous-buffered result, not evidence for concurrency. The reader consumed
39,959 files (5,078,043,867 bytes), with 16 original mapped fallbacks, one peak
buffer and zero outstanding leases. DDS load took 295.315404 s, creation
2.799443 s, with zero exceptions. Content counts (316 mods, 47,388 defs,
14,748 patches), generated unified XML hashes and four reports match r01.

Frozen Loading Progress 0.14.0 explains the inactive worker: it runs its own
`ReloadContentIntReplacement.ReloadContentInt` enumerable, calling each content
holder directly, then invokes the native reload only to service hooks while
suppressing its body. The native scope therefore occurs after the actual reads.
The narrow compatibility fix wraps that replacement's enumerable factory,
entering/exiting a scope within each original MoveNext, never across yields.
The labelled small `textures-observed` lane adds Loading Progress to the
existing texture lane; comparisons must use that same lane in both arms.

Package `853c4f8` qualifies that integration. Small t05 passed at 48.085800 s,
with 2,854 reads, 20.275 s waiting for data, two peak buffers and no fallbacks
or remaining leases. Matched original t06 passed at 19.140196 s, DDS 612.012 ms.
The unchanged read-ahead repeat t07 passed at 18.660038 s, DDS 624.677 ms and
164.961 ms waiting. The initial long wait did not repeat; file warming and
contention are uncontrolled, and these results do not establish a small gain.
All reads and original texture counts match. The new source-open timer includes
queue waiting and fallback opening, not physical disk time.

Full `next-r03-active-read-ahead` passed at 543.025976 s with normal exit:
232 scopes, 39,959 buffered reads, 16 mapped fallbacks, two peak buffers and
zero outstanding leases. DDS load was 230.325000 s, texture creation 2.811083 s,
source acquisition 226.957332 s, and consumer waiting 218.660128 s. Thus most
measured DDS delay is obtaining source data, rather than the texture-creation
method. This does not isolate physical disk latency from thread scheduling,
file-system filters or other contention. All unified XML/report hashes and
316/47,388/14,748 content counts match the earlier full controls. The earlier
buffered run took 295.315 s in DDS loading, but a matched original repeat is
still required before attributing the difference to read-ahead.

That waiting justifies testing two simultaneous file readers. The experimental
`--dds-read-ahead-workers 2` retains the same two buffers and original-thread
enumeration/consumption order. It tests concurrent source requests, not parallel
graphics calls. The warmed small lane has little expected room for improvement;
full representative comparisons must decide whether the extra reader helps.

XML timing (`--xml-query timing`) measures the whole existing patch worker,
including lazy XML query evaluation. It groups at most 512 worker/query shapes,
retains one concrete example per group and writes one aggregate receipt at the
end of patching. Nested measurements are inclusive, not additive. Diagnostic
settings must match comparison arms; these timers are optional and off by
default. They do not alter the accepted definition-lookup algorithm.

The prior full r07 capture attributes 17.706 of 32.825 XML patch seconds to
Combat Extended. Better Architect Menu follows at 2.466 s, Muzzle Flash at
1.805 s, Giddy-Up at 1.304 s and VFE Tribals at 1.285 s. Existing captures
cannot identify CE's individual expensive operations. Standard ILSpy inspection
confirmed its `GetOrCreateNode` helper selects all matching children, counts
them and takes the first, despite fixed simple child-name callers. A direct
scan is a concrete candidate, but its time has not yet been established.

## Security alerts and live recovery

The owner reported Windows Security alerts and asked for less disruptive
commands. Defender records matched inline reflective PowerShell DLL inspection.
That pattern was stopped. ILSpyCmd 11.0.0.9375 was installed using its ordinary
.NET tool package under `artifacts/startup-next/tools` and successfully read the
frozen CE type. No Defender settings or exclusions were changed. Project
instructions now record this command preference.

R08's graphics failure remains unexplained. Its first D3D11 errors followed late
Geological Landforms/LunarFramework initialization and involved a 16384 x 8192
render target; the DDS stage may already have completed. A subsequent small
original-reader control, `next-t01-original-recovery`, reached the menu at
49.483223 seconds and exited normally with automatic acceptance. This proves
that the small fixture currently loads, not that the full graphics failure is
resolved or that either new prototype is faster. GPU contention remains present.

## Verification and continuation

Before deployment, 42 focused managed checks and 29 fixture/process Python
checks passed, including queue bounds, ordering, changed files, early disposal,
worker joins, existing DDS fallback and accepted XML lookup behavior. No skips.
Independent review found no blocking read-ahead integration defect. Empty DDS
selections do not start a worker. Evidence and source-inspection files are
under ignored `artifacts/startup-next`.

Small read-ahead `next-t02-read-ahead` passed at 18.127494 s: 2,853 files,
293,331,836 bytes, 200.900 ms consumer waiting, two buffers and zero fallbacks or
outstanding leases. DDS load was 761.324 ms (creation 127.196 ms). The same-package
synchronous buffered control t03 passed at 18.674006 s, DDS 855.370 ms; original
mapped t04 passed at 17.396977 s, DDS 887.932 ms. All had zero DDS exceptions.
This does not establish a useful small-profile gain. Package was `a00c763`.

The full original-reader control `next-r01-xml-original` passed at 684.501729 s
and exited normally. It loaded 39,975 DDS files in 332.406315 s, including
18.082631 s in texture creation, with zero DDS exceptions and no repeat device
failure. XML patch scope was 40.271117 s. Detailed query shapes exceeded the
512-key budget; 28.402 s fell into overflow. Among retained entries, simple
Name-attribute Add operations took 642 ms over 39 calls. This supports a bounded
direct-selector trial, but not a large speedup claim. The next diagnostic version
groups fixed query families instead, retaining an example and measuring existing
index-building time separately; don't compare its total overhead as identical
to this first diagnostic run.

An optional `--xml-query on` now enables a non-cache direct scan for a unique
`Defs/Type[@Name='literal']` root, then retains native evaluation of a safe
downward suffix. Multiple result suffixes retain the original document query,
as do missing/duplicate roots, malformed/unsupported expressions, entities and
custom XML objects. The frozen CE small lane is `--lane xml-patches`; only its
required Harmony dependency plus the established official/Prepatcher foundation
is included. Both XML arms retain the same content. 78 focused managed tests
and 29 Python checks pass after this addition, including differential native
XPath and mutation checks. Independent integration review found no blocker.

Corrected fixed-family r02 timing puts four large OR-query families at about
9.4 s inclusive worker time. An additional optional native XPath rewrite
(`--xml-query or`, or `both` with direct selectors) targets homogeneous lists
of 4–64 literal names. It avoids repeating the same child/attribute traversal
for each alternative, preserving the original XPath result list and edit
ordering. Query/output size is bounded; delimiters are rejected in literals
and guarded in candidate values; missing attributes and multiple defName
children preserve native semantics. No queries, indexes or results are retained.
These families include unsupported queries, so their full time is not a savings
estimate. The first 100 focused managed tests and 29 Python checks pass.
After the Loading Progress wrapper, 103 managed and 29 Python checks pass with
no skips. Small x06 OR-only passed at 21.117827 s, XML 2.127597 s with 22
rewrites. Matched original x07 passed at 17.791162 s, XML 2.289413 s. Combined
x08 passed at 17.022067 s, XML 1.571466 s, with 242 direct selections and 22
rewrites. Direct scanning appears to provide most of the small gain; the OR
rewrite alone remains a weak signal. Full fixed-family timing also measured
CE's entire gun worker at only 108.033 ms across 212 calls, so changing its
GetOrCreateNode helper is no longer justified as a substantial startup target.

The first small XML prototype was slower: x01 original patch scope 1.954796 s,
x02 direct 2.170994 s, and x03 original repeat 1.794977 s. Its lower menu time
was not an XML improvement. Removing repeated assembly-property inspection for
ordinary XML elements changed the result: x04 revised direct took 1.706014 s,
versus x05 matched original 2.128186 s. The 96 Name-based Replace operations
and 74 Name-based Add operations took 325.593 ms together in x04, versus
698.053 ms in x05 (543.118 ms in x01). All runs passed automatic acceptance
and exited normally. This is a modest stage-level signal under contention,
not yet a confirmed representative-profile improvement. The optimized code is
`5bb4529`; 78 managed and 29 Python checks still pass with no skips.

## Validation, rejected approaches and continuation

Final focused verification passed **111 managed tests and 29 Python checks**,
with no skips (`artifacts/startup-next/check-10.log`). It covers two-reader
ordering, unique claims, bounded buffers, changed-file fallback, early disposal,
worker joins, existing DDS parser behavior, native XML differential/mutation
cases and fixture preparation flags. Independent review found no blocking
ownership or texture-data lifetime defect. Graphics API calls remain on the
original thread; worker buffers remain owned until the original upload returns.

All six full completed runs r01–r06 passed automatic acceptance, exited zero
through the observer's normal shutdown, and retained 316 loaded mods (314 frozen
originals plus RLO and the observer), 47,388 definitions and 14,748 patch
operations. Both combined XML files and all four MissileGirl reports have
identical captured hashes across r01–r07, including the failed repeat's completed
XML output. R07 has no completed Loading Progress content-count receipt.
The accepted definition index retained 13,103 hits, 232 rebuilds and 121
invalidations in the final comparison arms. All arms used observer
`f26270bc6fe7c7ad29664ce9a84aff50527a9b16` with automatic normal exit enabled.
The six successful full runs show no repeat of the earlier D3D11 failure.
These checks support startup content preservation, not gameplay certification.

The consolidated captured summary is `artifacts/startup-next/run-summary.json`
with readable output in `summary-final.txt`; neither contains a fabricated menu
time for the failed repeat. Detailed private evidence stays under the fixture.

The Loading Progress integration is restricted to its verified frozen assembly
and wraps the ordinary enumerable factory, entering/exiting scope inside each
original MoveNext. It preserves yields and disposal. Unknown suppliers fall
back. Patching generated MoveNext directly was rejected because of Harmony's
exception-block rewriting hazard; broad generic loader patches were rejected
because Mono shares entrypoints with audio/string loaders.

Small lanes qualified native and Loading Progress texture paths before the full
set. Their DDS stages were already under one second when warm, and did not show
a useful repeatable gain. The full one-reader wait measurement supplied a new
reason to try two readers. This does not repeat the earlier unhelpful small
XML-worker-count experiment without evidence.

Small XML combined experiments improved one patch-stage comparison from 2.289
to 1.571 seconds, but the full trial did not generalize: r05→r06 CPU was
35.578→35.469 seconds. The direct named-parent families improved 7.291→6.769
seconds inclusive, and the OR families 11.521→11.478 seconds. Other worker and
index-building variation offset those small changes. Stop these experiments;
do not label lower menu time in a small run an XML optimization.

CE's GetOrCreateNode repeated selection/counting looked wasteful in source,
but its entire gun worker measured only 108.033 ms over 212 calls. It was
rejected as a substantial target. Broad parallel XML mutation, mod construction
and graphics callbacks were not implemented because of shared state/order
requirements. No further profiling framework or extra cache was needed to
identify the successful file-acquisition change.

Exact next steps after this checkpoint:

1. Diagnose the unresolved late-startup failure before promotion. Obtain a
   host session permitted to use the standard CPU profiler, then compare the
   same original reader and two-reader arms while capturing any recurrence.
   Do not infer a root cause from the last log line or repeatedly launch without
   a diagnostic reason. Keep the original reader for routine use meanwhile.
2. Confirm two-reader versus original DDS on the same pinned fixture while the
   PC is otherwise idle, with the same observer, content and diagnostic flags.
   Repeat fresh profiles; record that filesystem cache state remains uncontrolled.
3. Keep the original XML algorithm. Only resume XML redesign after identifying
   an operation with enough measured cost to justify it.
4. Before broad support or release, qualify other loader suppliers and the
   latest Steam revision after MVP. The accepted minor GOG revision difference
   is not a blocker for this local result. Existing saves/gameplay were not tested.

Normal Steam, Workshop, profiles, saves and caches were untouched. Modern Dev
Tools remains reversibly excluded in every full comparison, with the original
frozen snapshot preserved; removing it is not counted as a speedup. No release
was published. OP7 remains incomplete and OP8 inactive.
