> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_STARTUP_CONCURRENCY.md` at `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Startup concurrency investigation

Subsequent owner-authorized work investigated private XML-to-Def scalar
preparation. It established real overlap and focused construction/reference
equivalence, but no repeatable stage/menu improvement. The owner subsequently
requested removal; its runtime code, diagnostics and selector are now removed.
Current package, final selection, costs and continuation are
in [the conversion record](definition-conversion-preparation.md); the checkpoint
and stopping point below are historical for that renewed request.

Continues clean `codex/startup-work-reduction` at `a4052db` in the single Z:
checkout. Owner authorizes small profiles and the specific modest XML expansion,
implementation and checkpoint commits. Medium/full lanes remain paused. Accepted searches, DDS and PNG read-ahead remain intact.
PNG remains promising for planning; a comparable DDS-scale gain is unmeasured.

## Decision at this checkpoint

**Real parallel execution is demonstrated, but no new reliable net loading gain
is qualified.** The modest larger profile did not reverse that conclusion. Keep
all new concurrency prototypes off by default. Accepted startup searches and
existing DDS/PNG implementations are preserved. No additional cache was added.

The bounded approaches supported by current measurements have reached a decision:
private inheritance preparation and lookup batching cost more overall; neighboring
independent patches are rare; query partitioning and worker reuse do not show a
repeatable complete-stage benefit. End this investigation checkpoint rather than
expanding profiling infrastructure or silently raising the live workload. This
is not a claim that all possible architectural parallelism has been exhausted.
The remaining larger redesigns need a new demonstrated cost opportunity or a
narrower compatibility contract before more implementation is justified.

### Requested larger workload and measured comparisons

The xml-expanded lane (historical path: `../../validation/XML_EXPANDED_PROFILE.md`) adds Vanilla
Expanded Framework, Vanilla Genetics Expanded, VFE Medieval 2 and VRE Android to
the small CE/Gagarin foundation. This adds about 4.68 MiB of active XML and 50 CE
compatibility patch files. All runs use the same frozen order, accepted searches,
two DDS readers, PNG read-ahead, timing diagnostics, menu observer and automatic
normal exit. No application cache is restored: Gagarin reports cache creation in
every run. OS file caching and other applications are uncontrolled.

Times below use the outer startup trace for the complete named method. Its First
prefix precedes worker setup and its Last finalizer follows worker disposal, so
LoadModXML includes pool admission, preparation and completion. Nested scope CPU
and waiting totals must not be added to these wall times.

| Comparison / package | Control stage ms | Candidate stage ms | Control / candidate menu seconds | Decision |
| --- | ---: | ---: | --- | --- |
| Broad query ranges, E01/E02, 960d4e3 | 3230.774 | 3991.095 | 42.165476 / 31.119792 | Patching slower despite faster menu |
| Narrow Replace ranges, E04/E05, a9535b5 | 3993.772 | 3705.409 | 40.921009 / 33.485171 | Initial decrease, repeat required |
| Narrow ranges reverse order, E07/E06, 0ff5197 | 4431.953 | 4504.266 | 34.241919 / 40.388027 | Complete-stage decrease did not repeat |
| XML worker reuse, E07/E08, 0ff5197 | 1203.008 | 1023.005 | 34.241919 / 33.367017 | Initial LoadModXML decrease |
| Worker reuse reverse order, E10/E09, 0ff5197 | 830.699 | 1153.689 | 31.197024 / 32.665602 | Complete-stage decrease did not repeat |

E03 and E04/E05's worker timing selection refused an unrelated projected field in
the old broad adapter contract. E03 therefore provides no active worker comparison.
The targeted exact-body admission correction in 0ff5197 installs in both arms;
E08/E09 actually reuse two workers across 32 groups in two scopes, then release
them. Constructor thread identities fall from 60/61 to eight; simultaneous file
construction remains three. Every arm constructs the same 2964 assets. Summed
joins are 342.275 / 7.631 ms in the first pair and 61.588 / 10.753 ms in the
reverse pair. Removing lifecycle waiting is real; converting it into a stable
whole-stage or menu benefit is not established.

Narrow ranges select six expensive replacement queries and retain only six
partition-boundary references until root membership changes. E05 preparation
cost 111.962 ms with 235.593 ms summed worker intervals and 203.125 ms CPU.
The wider 37-operation Replace OR family was 491.822 / 411.613 ms in the first
pair, but 570.554 / 559.151 ms in the repeat. The small repeat difference does
not pay for complete-stage overhead consistently. More content did not make
parallel preparation free, and broad Add splitting duplicated work on fallback.

All ten expanded runs reached the menu, exited normally and completed capture
with automaticTestPassed=true. Both Unified.xml and Unified_Original.xml are
byte-identical across all ten, including cache construction. A focused log scan
found no XML errors, exceptions, unresolved cross-references or failed patches.
There are 28 common Mono dynamic-library fallback notices in each run; these
were separated from those error signatures. This is startup/content evidence,
not save-load or gameplay qualification. No saves were loaded.

### Opportunities ranked by demonstrated cost and plausible benefit

| Rank / stage | What can overlap | What still orders or limits it | Evidence-led disposition |
| --- | --- | --- | --- |
| 1. XML patching, about 3.2–4.5 s expanded | Read-only selection over disjoint XML ranges; queries under distinct untouched roots | Patches change later queries' inputs; conditional/custom operations are barriers. Every original mutation and result order must remain intact. | Greatest measured CPU opportunity, but group trial found only three groups/six queries. Broad range trial slower; narrower repeat inconclusive. No supported enablement. |
| 2. Creating definitions, about 2.5–3.3 s expanded | Pure parser/metadata preparation could be independent | Static parser dictionaries and helpers share mutable state. Constructors, custom XML loaders and registration execute observable mod code. | Larger potential redesign, not safe to parallelize the native loop. Observed generated-parser preparation was only about 220–230 ms on small content; no demonstrated pure subset large enough to justify replacing the pipeline now. |
| 3. Raw XML files / cache-miss hashing | Private decoding, document construction, OuterXml serialization and per-file hashing already overlap on three threads | Original group joins; Gagarin's global CurrentLoadingMod prevents blindly overlapping whole mod callbacks | Worker reuse removes much join waiting but no repeatable complete-stage gain. Cross-group private file preparation remains possible with explicit context/order handling; no evidence now supports a larger pipeline rewrite. |
| 4. XML inheritance, about 0.25–0.34 s | Independent trees can run simultaneously; parent-before-child only applies within a tree | Shared duplicate-check scratch is an implementation constraint. Native owner-document identity and ordered diagnostics/publication must be preserved. | Private-tree prototype genuinely overlapped 350 trees but 252.612 ms became 562.843 ms, including 77.589 ms graph prep and 206.654 ms ordered import. Copying dominates this approach; larger input also increases its copies. |
| 5. Cross-reference resolution, about 0.15–0.18 s small | Native reference callbacks already run in parallel; immutable batch views avoid the lookup lock | Some callbacks mutate fields on workers; success collection and later application have their own ordering | Removing most lock contention made complete stages about 24% slower. Summed waiter time exaggerated the savings available. |
| 6. Cache writing / graphics / audio | Some private serialization or file reads could overlap | Gagarin Save mutates metadata and builds one output document; texture creation and graphics publication require the owning thread. Audio already reads outside its queue lock. | Gagarin Save was about 0.35–0.43 s mostly CPU. No demonstrated critical-path audio wait. Existing bounded DDS/PNG read-ahead already separates safe file work from graphics work. No new cache or speculative graphics workers. |

A concrete lock-owner example from A02: managed threads 26 (native 26528) and 58
(native 24496) waited around 3714.8–3716.4 ms for the GenDef lookup monitor;
managed thread 18 was the sampled owner when they encountered contention. The
owner can change during the wait. A02's loading thread 28 (native 6312) joined
worker 37 for 47.187 ms, although that worker's last measured file constructor
finished only 0.168 ms after the join began. This isolates an unproductive tail,
not a proven physical-disk wait or a single known scheduler cause.

File construction is not several threads merely taking turns behind one parser
lock: A02 records about 1031 ms accumulated CPU in 447 ms of active constructor
time, with three overlapping constructors. Gagarin cache-miss hash-table waits
were only 10–14 ms summed; private serialization/hashing happened outside that
lock. CPU counters are coarse, and elapsed-minus-CPU also includes runnable time,
page/file waits and other synchronization. These hooks do not distinguish physical
I/O from scheduling delays; standard permission-blocked CPU profiling was not retried.

### Reproducible continuation and validation

- Last tested/deployed source: 0ff5197bf6c090be5959ca03a274cd9e2e9f3097.
- Last prepared/captured label: conc-e10-worker-control; fixture process closed.
- Final focused checks: 43 managed and all 35 fixture Python checks pass. Earlier
  broad HugsLib test-process interference and isolated passes remain documented below.
- Evidence: artifacts/startup-concurrency/expanded-final-content.json, per-run
  summaries, thread-examples.json, final-checks.log and frozen canonical captures.
- Runtime prototypes, selectors and tests remain opt-in reproducible experiments;
  none is enabled by installing Wake-Up normally. No public release/default promotion.
- Continue in this checkout/branch only. Read prepared.json before operations;
  commit with the tracked hooks. Existing 12 stashes and other branches remain.
- A further trial should identify a cost large enough to repay preparation and
  ordered completion. Do not repeat blocked profilers, requalify accepted caching
  as a prerequisite, or launch medium/full without renewed explicit authority.

## Earlier measurements (chronological)

All times below are from fresh fixture profiles (no restored application
caches); filesystem caching and the owner's other applications remain
uncontrolled. DDS two-reader and PNG read-ahead stay enabled identically.

| Small run | Menu seconds | Result |
| --- | ---: | --- |
| conc-a02-trace (activation) | 14.212090 | Automatic pass, original lookup |
| conc-x01-trace (Combat Extended) | 25.149316 | Automatic pass, original lookup |
| conc-x02-batch (Combat Extended) | 20.955127 | Automatic pass, prototype refused; no candidate claim |
| conc-a03-batch (activation) | 13.721545 | Automatic pass, prototype refused |
| conc-a04-batch (activation) | 14.078407 | Automatic pass, prototype refused |

The activation trace measured 1,572 XML file constructors: their active
intervals covered 447 ms, with three overlapping constructors for 444 ms and
1,031 ms accumulated thread CPU. This is actual parallel CPU work. Nested
DOM loading accumulated 563 ms CPU. Windows thread CPU accounting is coarse,
so short individual events sometimes report zero or more CPU than wall time.
The aggregate and overlap are the useful evidence, not submillisecond CPU rows.

Combat Extended's fresh XML work measured 2,283 ms patching / 2,156 ms calling
thread CPU; 1,589 / 1,500 ms turning XML into definitions; 247 ms inheritance;
233 ms combining documents; 627 ms loading definition files. All 2,159 file
constructors still reached peak concurrency three. Native joins totaled 93 ms.
The inheritance parent-link pass was only 7 ms. Parser generation was roughly
220 ms inclusive across its families. Patching is ordered CPU work, not an
unidentified global lock around otherwise parallel patch execution.

The native `GenDefDatabase.cachedGetNamedSilentFailLock` was entered 86,515
times, contended 35,725 times, with 911 ms summed waiting and 26 ms holding the
lock. Trace events identify waiting managed/native threads and sample the
managed owner at contention (zero means no owner was observed during handoff).
Up to 31 recorded waits overlap. But all six complete cross-reference stages
together took only 181 ms: total waiting across threads is NOT a startup savings
estimate. Success collection and ordered application remain separate work.

`DefinitionLookupBatchRuntime` tests immutable batch views of the existing
delegate dictionary, with original native misses/sound behavior and lookup
delegates. The view lasts only until the native batch has joined; no definition
results or new delegates are memoized. The first attempted candidates refused
at authentication with `semantic-il-exception-badimageformatexception`.
The typed metadata API trial did not fix this and was reverted. The next
checkpoint authenticates before installing our diagnostic detour; that result
is pending. Do not interpret faster refused runs as candidate improvements.

The initial `op7_fixture.py test` built successfully and passed 369/372 adapter
checks; three HugsLib tests failed in the combined process. All eight HugsLib
checks then passed in isolation (610 ms), consistent with test process state
interference. No HugsLib product code was changed. New focused batch checks and
27 targeted adapter checks passed, as did all 31 Python fixture checks.
The broad command stopped at adapter failures; it did not run its later phases.

Evidence: `artifacts/startup-concurrency`, including `summarize.py` and per-run
summaries; canonical fixture results retain originals. `conc-a01-trace` never
launched because the new option was initially missing from preflight argument
reconstruction; that wiring is fixed. All launched runs so far exited normally.

## Initial evidence and next measurement

### Batch experiment decision

Harmony's existing instruction decoder successfully authenticated the same
native body in Mono; the general semantic verifier remains unchanged. A06
activated and passed (13.846298 s) but its read-only snapshot found only 695
preexisting entries across 57,625 calls. A07 moved the exact native missing-type
delegate setup before the batch: 56,547 calls avoided the lock, only 1,078 used
the original path, 22 contentions / 1.043 ms wait remained, and no snapshot was
retained. Menu was 14.349774 s with automatic pass.

**Rejected for enablement: reducing contention did not improve the stage.**
Two matched pairs on package `9454b85`, small `xml-patches` lane:

| Label | Batch view | Cross-reference stages total ms | Menu seconds |
| --- | --- | ---: | ---: |
| conc-x03-control | timing only | 155.099 | 21.032632 |
| conc-x04-batch | enabled | 185.540 | 19.834651 |
| conc-x05-control | timing only | 144.098 | 20.198218 |
| conc-x06-batch | enabled | 184.296 | 20.074072 |

All four passed automatic acceptance and exited before the next launch. The
candidate was about 24% slower in the targeted stage, including preparation.
Menu variation cannot be attributed to that change; unrelated XML patch CPU
also varied. Keep `--definition-batch` off. Do not spend more effort removing a
small lock merely because its summed waiting looks large. The experiment keeps
the same native reference callbacks, application ordering, native missing/sound
paths, a 4,096-entry view bound and joined-batch lifetime; no lookup results are
cached. Four focused tests pass after correcting the NUnit Action overload.

Next diagnostic lane `xml-cache-miss` retains the small CE selection and adds
only frozen MissileGirl/Gagarin (5 MiB, 13 DDS files, sole required dependency
Harmony already present). It is a small profile, not medium/full. Fresh profile
preparation restores no Gagarin cache. Gagarin timing is installed after mod
constructors and before XML workers so late-loaded plugin assemblies are seen.
This measures its actual cache-miss hashing, lock and cache serialization.

The original XML file loader parses independent documents on the calling
loading thread and two newly created threads per folder group. It uses indexed
result slots and joins both workers before returning to the next mod. There is
no whole-parser monitor in this method or the asset constructor. Gagarin's
constructor postfix serializes each document and calculates hashes before a
lock on `Context.AssetsHashes`; the lock protects hash/asset collection updates
and cache validity. Measured contention is small: see the cache-miss results below.

Patch operations execute in order on one shared document. Inheritance resolves
parents before children and uses shared `tempUsedNodeNames` scratch storage and
one owner document; naive parallel tree mutation is unsafe. Generated definition
parsers use mutable static method dictionaries and arbitrary custom loaders.
Cross-re…625 tokens truncated… 33 fixture Python
checks, 118 remaining core checks, and five bootstrap contract methods passed.
Builds have no warnings/errors. The earlier combined HugsLib failure remains
reported above; isolated and final focused HugsLib checks pass.

### Next prototype: retain native XML workers across groups

A02's longest native join started at 1398.614 ms and returned at 1445.801 ms.
Its target's last measured constructor ended at 1398.782 ms; the other worker's
ended at 1398.795 ms. Thus nearly 47 ms had no measured file construction. This
can include scheduling, runtime/thread cleanup or unmeasured queue-tail work;
it does not prove one specific cause. Similar longest joins ranged 7–50 ms in
other small runs. Evidence: `thread-examples.json` and original interval traces.

The opt-in `--xml-workers on` experiment retains two workers only within each
native LoadModXML / ErrorCheckPatches scope. It invokes the same closures,
constructors, bag and indexed result array; owner participation and each group
join remain. Job execution contexts are captured separately; workers and queued
references are released at scope exit. Unknown loader/asset constructor hooks
refuse the scope; only own diagnostics and the exact inspected Gagarin constructor
postfix are allowed. Timing mode uses native thread operations through the same
wrappers. This tests repeated worker lifecycle costs, not cross-mod dependency
relaxation or increased worker count. Three new focused checks cover overlap,
repeated identity/context isolation, exception propagation and rewrite refusal.
Nine focused worker/parallel XML checks pass. Native Harmony instruction decoding
in .NET tests fails on this method's metadata; runtime identity uses the existing
Mono semantic contract and validates the replacement call shape at installation.

At that checkpoint the investigation remained open. The planned next comparison used small
xml-cache-miss content with matching diagnostics and all existing optimizations.
Then assess inheritance/private preparation and other architectural opportunities
by complete stage cost; additional caching is not the target. No medium/full runs.


Worker trial notes: W01/W02 accidentally retained deployed b29e07a and therefore
provide no worker-reuse comparison. W03/W04 used 4005174; timing installed, but
candidate correctly refused Gagarin whose memory-loaded assembly has no Location.
Its inspected module ID plus the active mod's exact hashed plugin file now bind
that allowed callback. W03/W04 are also not an enabled comparison. All exited
normally and passed automatic acceptance. No speedup is claimed for these runs.

### Worker mechanism and inheritance prototype checkpoint

W05/W06 both used 79ca200 and passed normally. Reuse is confirmed active:
20 groups in two completed scopes, released workers, eight constructor thread IDs
instead of 37 (late native groups remain outside scope), peak concurrency three.
Join total fell 103.012 -> 4.079 ms. Complete LoadModXML was 909.856 -> 876.781 ms;
menu 23.254008 -> 23.357071 seconds. Both XML cache files remain byte-identical
to G01/G02. Mechanism supported; one pair is not a worthwhile startup speedup.
Worker reuse stays opt-in. The earlier refusal was corrected without security
changes using standard read-only dnfile metadata under ignored artifacts.

The next opt-in `--inheritance on` prototype isolates whole independent trees
in private XmlDocuments, retaining parent-before-child resolution inside each.
It calls the exact authenticated native merge helper. Native ResolveXmlNodes
still traverses and publishes; at each original ResolveXmlNodeFor, original
duplicate checks run, and the private result is imported into the native parent's
owner document. Thus child content, result ownership, diagnostic and publication
ordering stay native. Root nodes retain their original identity. Preparation
failure occurs before any native mutation and falls back to the original path.
Unknown inheritance hooks / already resolved or cyclic input / external parents
refuse. Two added workers plus the owner process trees; all join before applying.
A conservative logical reservation counts 256 bytes per node plus text/name
characters and ancestor input sizes, capped at 256 MiB before parallel work.
This bounds admitted private node work; it is an estimate, not a physical RSS cap.
Timing mode performs no preparation so its total remains a fair disabled baseline.

Six focused checks passed for worker scheduling/context/error handling, private
parent-to-child preparation, unchanged source/output document ownership, budget
accounting and the three exact native helper hashes. Next run the small cache-miss
pair and judge the entire stage, including graph construction and ordered imports.

### Independent inheritance result: rejected

I01/I02 used identical 61e83b2 packages, fresh small xml-cache-miss content,
matching diagnostics and existing search/DDS/PNG options. Both exited normally
with automatic pass. Native inheritance took 252.612 ms; candidate took
562.843 ms including 77.589 graph/budget preparation, 271.425 parallel preparation
and 206.654 ordered import/check/application. It applied 8,005 results among
8,355 registered nodes and 350 roots, with a 236,300,518-byte conservative
reservation. Both XML cache files exactly match prior native hashes. Menus were
22.999902 / 22.410033 s; that apparent improvement is unrelated variation, not
an inheritance gain. Keep inheritance preparation disabled.

The trees are genuinely independent, not all fundamentally serial: BaseBullet
(1,269 nodes), AmmoBase (1,143), and AmmoRecipeBase (891) overlapped on three
managed threads. Their worker intervals were 111.9, 146.1 and 107.7 ms. Native
parent dependencies stay inside each tree. Shared scratch and document ownership
are implementation constraints; preserving document ownership with ordered imports
made this design lose. Rebalancing these workers cannot remove the measured
serial graph + import cost. Removing document ownership compatibility or replacing
the native node model is a larger redesign, not established safe by this trial.

### Next experiment: independent patch query preparation

`--patch-queries on` prepares up to eight consecutive native Add/Replace/Remove
queries within native PatchOperationSequence, only for distinct literal @Name
roots and strict downward element paths. The actual query runs on workers over
a read-only document; complete node references are returned to original ordered
Apply calls. No document clone or new index is introduced. The existing accepted
Def lookup path remains available and unchanged for every other query. Native
sequence early failure still controls which operations execute. Pending selections
are bounded to 65,536 nodes and discarded at group/sequence end or an outside-root
mutation callback. Unknown method hooks, custom DOM nodes/entities and unsupported
XPath shapes refuse. The native helpers and sequence body have exact hashes.
All worker reads join before any XML mutation; two persistent workers plus the
owner are released at ApplyPatches completion. Timing mode does no query preparation.
Thirty-six focused managed checks pass, including existing lookup semantics.
Next use a matching small cache-miss pair and include preparation, admission scan,
worker lifetime and ordered application in the complete patch-stage cost.

P01/P02 on 33dff34 refused installation, so they are not enabled query-preparation
comparisons. The refusal receipt now includes the specific method/body reason,
and authentication occurs before accepted worker-query hooks are installed; the
actual stage admission still checks their final published ownership. No gain is
claimed from these runs. Both retain native fallback and automatic closure.

P03/P04 and diagnostic P05 also refused. The captured instruction diff proves
the body is unchanged: .NET formats fields as `Boolean enabled` / `Success success`,
while Mono formats `System.Boolean enabled` / `Verse.PatchOperation+Success success`.
The new patch experiment now hashes explicit declaring type, field type and name;
other experiments keep their existing hashes. This was formatter variance, not a
changed instruction or loosened identity requirement. Evidence: `patch-body.diff`.
The next pair will be the first eligible patch-preparation measurement.

P06/P07 installed successfully on 366a9e5, but the sequence-only interception
found zero eligible groups. Actual CE content contains only three explicit
PatchOperationSequence objects; most expensive operations are top-level patches.
No query-preparation benefit is claimed. The same bounded grouping now wraps
the native top-level ApplyPatches callsite as well, preserving its foreach,
exception handling and operation ordering. A bounded reference-only view of the
already-loaded patch order locates consecutive candidates; an unexpected actual
operation fails the positional check and uses its original path. Both previous
runs closed normally. This follows the demonstrated caller rather than widening
XPath admission or adding an index.

P08/P09 on 4c91e86 confirmed top-level grouping: only three groups / six prepared
and consumed queries, 16.672 ms preparation, 28.995 ms summed worker intervals.
The complete candidate patch stage was 3354.803 ms, so this sparse opportunity
cannot justify its admission/dispatch overhead. Both runs passed normally; keep
`on` grouping disabled. Most neighboring expensive patches share roots or cross
conditional/custom barriers, establishing actual ordering limits in this content.

Next `--patch-queries split` partitions a single eligible XPath query across
three disjoint ranges of existing top-level XML elements. A read-only navigator
window bounds native sibling traversal; no XML is copied and no new index is
built. Native XPath evaluates the unchanged expression per range. Results are
joined in document order before the original mutation. Only literal same-field
@Name / OR equality predicates and downward paths qualify; single defName queries
stay with accepted caching. Root/global-position predicates and escaping axes
refuse. Replace/Remove already materialize results; Add qualifies only with at
most one result, retaining its original lazy multi-result path otherwise. Up to
65,536 roots/results, two workers plus owner, joined lifetime. Forty-two focused
checks pass, including exact original node identity/order across ranges with
duplicates, other element types, namespaces, text, comments and absent matches.
The next live pair compares the complete stage against preparation disabled.

### Owner-authorized modest expansion
The owner requested a somewhat larger compatible XML workload; this supersedes
small-only authority only for the specifically documented
xml-expanded selection (historical path: `../../validation/XML_EXPANDED_PROFILE.md`). Medium/full
remain paused. Four added frozen suppliers provide about 4.68 MiB of active XML
and activate 50 additional CE compatibility patch files. The exact dependency,
conflict, folder and size audit is under ignored artifacts. No package refresh,
normal profile changes or new fixture is involved. Test control first and inspect
new errors before interpreting candidate results.
Small R01/R02 (8a2f07e) both passed and produced byte-identical cache XML.
Complete patch times were 2174.339 / 2247.496 ms; menus 23.675075 / 20.251721 s.
This is not an established speedup. Range selection was exercised (six peak
result nodes), but its detailed counters were accidentally omitted from the
completion receipt. The output now includes query count, partition timing/CPU
and bounded execution intervals. Next measure on the requested expansion.
Expansion checkpoint 960d4e3 passes 48 focused managed checks and all 35 fixture
Python checks. The expansion retains the same permanent fixture and frozen order.


### Expanded measurements and narrower trial

E01/E02 on 960d4e3 passed automatic acceptance with byte-identical original and
patched cache XML. Outer ApplyPatches timing was 3230.774 / 3991.095 ms; menu observation was
42.165476 / 31.119792 seconds. The menu variation is not evidence of a patch gain.
The split candidate processed 239 queries: 1849.168 ms preparation, 2847.046 ms
summed worker intervals and 2218.750 ms summed thread CPU. This is real overlap
but a 23.5% regression in complete patch-stage cost. Add OR queries rose from
279.830 to 461.845 ms because multi-result cases then repeated native selection.
Replace OR queries were 445.042 / 433.805 ms; single-name queries also lost time.

The narrowed follow-up admits only Replace with at least four OR terms. It
retains six borrowed range-boundary references until top-level membership changes,
avoiding repeated root enumeration. This is a transient partition plan, not a
new lookup index or persistent cache. All workers join before ordered mutation.
Results remain bounded to 65536 per partition and 65536 accepted in aggregate.
E03 worker reuse passed menu acceptance at 41.260291 seconds but refused during
initialization, so it is not a worker-reuse measurement. The next control also
records the refusal stack; admission will not be weakened to force an experiment.


E04/E05 narrowed pair on a9535b5 passed with identical cache XML. Complete patch
cost was 3993.772 / 3705.409 ms; menus 40.921009 / 33.485171 seconds. Only six
queries qualified, with 111.962 ms preparation, 235.593 ms summed worker time,
203.125 ms CPU and three partition plans. This needs a reverse-order repeat:
unaffected query families also varied, so the 7.2% stage decrease is provisional.

E04 refusal stack identifies Op7RuntimeContract.RequireField inside ResolveCurrent,
before loader authentication. Worker reuse now binds its actual loader/constructor
by exact signature, authenticates original game/Harmony binaries and checks both
original semantic bodies. It no longer requires unrelated catalog projections.
Unknown loader/constructor hooks still refuse each scope. Desktop net472 lacks
Unity Dictionary.TryAdd (existing documented semantic-test limitation) and some
constructor operand types; native Mono acceptance must be verified live. General
catalog identity and semantic verification remain unchanged.
