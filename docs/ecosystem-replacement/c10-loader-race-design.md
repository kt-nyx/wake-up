# C10 concurrent Harmony discovery: exact boundary and proposed dependency

## Steward review: product-owned lifetime passed; MP3 decoded reuse next — 12 September UTC

Wake-Up now performs its own orderly audio shutdown without the observer doing
that work first. The tested cross-domain bridge also restores the caller's
context and finishes before ordinary domain unload advances. Independent source
and captured-behavior review passes this bounded lifetime increment. Protection
ends at bridge return; later native Harmony/remoting unload errors remain native.
This is not full C10, complete third-copy patch compatibility or production adoption.

Reviewed stopped `af5a904afa974c9a7b0d4fc3c4c1f609af906f89` descends from assigned
`931429a3`. Product/native implementation is `af85f39d`; final matching tested
packages are `13f2614f352396d011897fcf4b989a76970c5d51`, with only observer setup
changes between those source revisions. The steward verified 65 local artifact
hashes, nine package receipts/30 files, six run manifests/58 captured observer
files, seven restored baseline hashes and ten absences. No RimWorld process was
present. Fifteen reversed transactions and metadata audit agree with restoration.
Two independent reviewers found no scoped blocker in product registration,
context ownership, the early unload leader or the actual held-entry probes.

Product stop evidence has observer stop/settlement false, one native reconstruction
and four replayed bytes before the held callback is released; native in-flight
count reaches zero before stop returns. The completed secondary-domain call
restores domain 1/context 1; the held unload case drains and joins before teardown.
Normal-player ProcessExit and early DoDomainUnload ordering are supported by the
pinned native instructions. No blind wait under the late native unload callback
was introduced. The natural advance pair preserves metadata and independently
compared 20/47/64 ordered sampled trace rows; no full-PCM or performance claim.
The two failed observer domain setups stay failed and separately attributed.

The steward repaired mixed Windows-1252/UTF-8 encoding and doubled CR line endings
in this report's newest section. Its accepted older UTF-8 text was compared with
the previous commit and preserved. Raw handoff evidence remains untouched.

### Next C10 behavior: native MP3 decoded-data reuse

MP3 currently reuses its seek index but still decodes during loading. Return the
same owner to implementing useful decoded-data reuse while retaining the game's
chosen decoder, native output, seek/read/error behavior, readiness and disposal.
Do not retry moving conversion-buffer allocation to the existing eager worker:
that route was already found to avoid no useful work for ordinary nonstreaming
files. Decoder selection/open may remain eager while genuinely repeated decoding
is avoided; name that retained cost honestly.

The precise prerequisite is trustworthy identity for reused decoded output.
The earlier supplier descriptor and list of candidate modules did not establish
the selected binary or future automatic selection. Revisit that dependency with
the selected native conversion instance retained, the now-qualified settlement
and lifecycle boundary, and a conservative alternative: identify the complete set
of code/configuration inputs that could affect that selected provider rather than
require a single selected-DLL hash if such a complete set can actually be proved.
This is a candidate design, not a claim that current enumeration supplies it.
Local drivers, loader resolution, dependent modules and selection changes must be
accounted for or cause ordinary per-asset fallback. Do not equate a descriptor,
matching test vector or an unproven candidate list with a closed identity set.

Microsoft documents that [automatic stream opening](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmstreamopen)
queries suitable drivers when no driver is specified, and that driver priority
controls this search order. [Priority and enumeration contract](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriverpriority).
[Enumeration](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriverenum)
can include local and disabled drivers; a global-only snapshot is not automatically
complete. Read these as API contracts, then establish what the pinned native
reader actually does. Do not alter system codecs, registration or priorities.

Implement and qualify actual avoided decoding on natural MP3 content once the
identity/state contract is sound. Preserve an already selected native instance
through replay and fallback rather than silently selecting a second decoder.
Use native byte/metadata/read-seek/error controls and cache-change invalidation,
not timing or an inactive hook. If a concrete identity dependency remains, return
its evidence and materially different implementation alternatives; that checkpoint
cannot exclude or close MP3. No broad new loader/profiling platform is warranted.

Textures/graphics/icons, broader loading/patch compatibility and production
Doorstop/native integration stay required after this focused continuation.
No C11 successor, scope reduction or backend adoption is approved. Optional
on-demand remains default-off, ordinary quality is preserved, and performance
qualification still requires a new explicit unattended window.

## Product-owned shutdown and domain lifetime tested — 12 September UTC

The product now finishes pending audio readers and waits for active native bridge
calls during ordinary shutdown. The observer no longer performs that stop first.
A separate domain-unload check also finishes the bridge before Mono starts tearing
down the caller's managed domain. This is a bounded implementation handoff for
independent steward review, not full C10 acceptance or production adoption.

Action `wake-up-c10-product-shutdown-931429a3-20260912` began from clean
`931429a3f1d33d89525c7801ccdf014cd959d48c`. Product and native changes are
`af85f39d3f973d794f36111da76e2e07a7f700e9`; the final tested packages are
`13f2614f352396d011897fcf4b989a76970c5d51`. The intervening `68abaed4` and
`13f2614f` change only secondary-domain observer setup. All runs use matching core,
observer and managed/native bootstrap packages for their recorded revision.

### Product behavior and supported lifetime

A managed domain (AppDomain) owns managed static state and loaded code. Keeping a
method pointer or native DLL alive does not keep that domain valid. The bridge
therefore needs both the right execution context and an earlier shutdown boundary.

- `NativeAudioLifetime` installs the product `Root.Shutdown` prefix and Unity
  `Application.quitting` handler independently of optional prepared-audio startup.
  Prepared native admission requires that registration. The early bootstrap also
  subscribes to its bound domain's `ProcessExit` before game/mod loading.
- The pinned standalone player binds this bridge to domain ID 0, named
  `Unity Root Domain`; native root/current identity and managed default-domain
  identity agree. Exact Unity/Mono instructions show normal player cleanup invokes
  `ProcessExit` before managed thread abort and profiler shutdown/metadata cleanup.
  The event dispatch releases the appdomain-list lock before invoking handlers.
- The existing public Mono method-entry filter additionally selects the exact
  instance `System.AppDomain.DoDomainUnload()` in mscorlib. The pinned runtime
  invokes it before creating the domain-unload worker. One separate, uncounted
  lifecycle leader drains readers while ordinary bridge admission remains active,
  then closes admission and joins admitted calls. Concurrent lifecycle callers and
  explicit stop wait for that completion. No native adapter lock spans a managed
  invocation, and the late native domain-unloading notification does no managed
  drain or blind join under teardown locks.
- Calls from another domain switch to the bound domain for registry operations and
  restore the caller's original domain and GC-rooted remoting context afterward.
  Cross-domain calls conservatively drain even if the Harmony image is shared with
  a recognized copy. Paired Unity Mono domain references are retained during this
  switch; these references themselves do not prevent thread abort.

The pinned `mono_domain_set(..., false)` rejects only already-unloaded state 3.
The early unload notification runs in state 1, before state 2 and worker creation,
so restoration does not require forcing entry into an unloaded domain. The normal
root cleanup chain and exact instructions are preserved in the ignored handoff
folder's `native-lifetime-review.md` and `native-lifetime-order.txt`.

Protection ends when the native entry bridge returns. Ordinary Harmony code and
remoting delivery afterward can still be aborted by an actual domain unload.
Direct process termination, hostile recursive unload and arbitrary embedding-host
cleanup are not qualified normal-player paths. The late profiler notification
alone remains insufficient to establish a managed-state lifetime guarantee.

### Focused GOG evidence

`c10-product-stop-01` at `af85f39d` passes the real product shutdown challenge.
The observer registers a genuinely deferred product WAV reader and holds an actual
third-copy UpdateWrapper entry. Only `Root.Shutdown` settles the pending reader:
one native construction, four recorded bytes replayed, zero mismatches. While the
product waits, native admission is closed and one call remains active; after the
observer releases its barrier, stop returns with zero active calls. The factory
runs after settlement, workers join, and a later actual entry makes no managed
bridge call. The ordinary root-domain third-copy factory outcome remains the
recorded `Invalid generic arguments / typeArguments` error; full patch installation
is not inferred from that result. The observer calls neither stop nor settlement.

`c10-product-domain-03` at `13f2614f` passes two distinct domain checks. A completed
synchronized remote UpdateWrapper reaches its real factory and returns in domain 1
and nondefault context 1. In the second case, actual `AppDomain.Unload` settles an
owned pending reader, closes admission and joins the held native bridge before
unload proceeds. Its later remote return receives the legitimate
`AppDomainUnloadedException`. The receipt records three cross-domain transitions
and three context restorations, one completed lifecycle drain, zero context,
lifecycle, bridge or recovery errors, and zero calls active on lifecycle return.
This does not establish the still-open root-domain/public-Patch integration.

`c10-product-advance-cold-01` and `c10-product-advance-warm-01`, both at `13f2614f`,
pass natural OGG/WAV loading, selected playback, current-biome preparation,
navigation, save/reload and normal shutdown. Ambience metadata agrees at 2051091
samples, stereo, 44100 Hz. The warm ambience starts deferred; the original map
worker prepares it. Ordered trace rows match cold/warm at menu (20), playback (47)
and reload (64), excluding thread identity. Digests sample at most 256 bytes per
read; this is not full-PCM verification or a performance result.

All six captures reached the menu and exited normally with code 0. Four pass their
requested probes. The two preserved failed domain setup captures are
`c10-product-domain-01` (active observer Assembly.Location empty because Prepatcher
loads bytes) and `c10-product-domain-02` (file reuse returned an inspection-only
image). The final observer loads executable deployed-package bytes into the
secondary domain before activation. Both failed attempts still passed normal
shutdown; neither is presented as successful domain-call evidence. One observer
compile nullability error was corrected before the first committed build.

Focused managed tests passed 122/122 and fixture tooling passed 63/63. All matching
managed builds and native `/W4 /WX` builds succeeded. No previous decoder-error
matrix, inactive same-file challenge or performance experiment was repeated.

### Restored handoff and remaining scope

All 15 assignment transactions were reversed latest-first. The seven baseline
file hashes and ten expected absences match the pre-assignment fixture, the metadata
audit passes, and the stopped process inventory contains no RimWorld process.
Normal game/data and other applications were untouched. No Steam/Deck operation,
UI automation, performance test, merge, push or publication occurred.

The ignored evidence root is
`artifacts/ecosystem-next-20260910/c10/product-shutdown/`. It retains six captures'
identities and 58 verified captured observer files, nine package receipts and 30
verified package files, the two failed setup results, operation journal, paired
correctness, baseline/restoration, tests and pinned runtime ordering evidence.
Source, package, deployed fixture and captured run identities remain separate.

The steward must independently review this bounded result. Full third-copy wrapper
and public-Patch integration, same-file/dependency compatibility, broad
textures/graphics/icons, MP3 decoded reuse/provider identity, and production
Doorstop/native dependency adoption remain open. Performance and full acceptance
remain pending an explicit unattended window. No successor is dispatched here.

## Steward review: bounded settlement correction passed — 12 September UTC

The corrected native entry path now completes pending readers before patching
continues, including when a reader's native decoder fails. The failed reader
retains its original exception for the normal audio consumer; following healthy
readers finish. Real GOG loading and playback evidence supports this behavior.
This passes the agreed bounded correction, not all C10 or production adoption.

Reviewed stopped commit `7de8311dc9734079c7b0a47919ba5193c72a2519` descends from
assigned `9e4f8dd2`. Final audio behavior was captured with matching core,
observer, managed bootstrap and native packages at
`4ef927c43d4c126797a8c7079de5547cb7af4e39`. Earlier stop and boundary controls at
`184ffd50` remain valid for unchanged native code; final package identities are
separate. The steward verified 93 handoff artifact hashes, 15 package receipts and
50 package files, 82 captured result/receipt hashes, seven restored baseline
hashes and ten absent paths. No fixture or normal RimWorld process was present
at review. The restored endpoint audit and reversal of 28 transactions agree.

Two independent source reviews found no blocking ordinary error escape in the
owned terminal-outcome/recovery contract and no blocker in the held-entry stop
or real-reader ordering evidence. The emergency termination branch was not used;
it remains a failed-contract outcome, not supported decoder-error behavior.
Further generic drain hardening is not a prerequisite for the next increment.

The six final captures in the handoff pass their applicable functional checks
and exit normally. Actual pending-reader overlap performs one reconstruction,
replays 307200 bytes with zero mismatches and finishes before the actual third-copy
UpdateWrapper factory; workers join and ordinary later errors match control.
Natural OGG/WAV, selected playback and current-biome preparation regressions pass,
including navigation and save/reload. The steward independently compared ambience
metadata and ordered 20/47/64 sampled trace rows, excluding thread identity.
Sampling is limited to 256 bytes per read; no full-PCM or speed claim follows.

### Next implementation obligation: lifecycle without observer assistance

The current successful runs call stop from the observer before Root.Shutdown,
in addition to installing the product prefix. The next bounded C10 increment
must make and qualify the product's own normal shutdown integration: settle
pending readers and join active bridge calls while managed execution is valid,
without requiring the observer to do that work first. Use the existing held-entry
challenge and at most the focused natural regression needed for changed behavior.
Do not restart the completed settlement tests as a separate hardening project.

Establish which shutdown and domain-lifetime paths the pinned host can actually
reach, including whether root-domain teardown is possible before the normal stop
boundary. Automatic native unload callbacks currently only close admission; they
do not join a callback already admitted. Determine a concrete supported earlier
boundary or runtime ordering guarantee for reachable cases. Do not insert a blind
blocking wait under Mono unload locks. A missing proof for arbitrary domain abuse
is different from a reachable ordinary shutdown defect; qualify the latter and
state the supported lifetime precisely. If another domain can invoke the selected
methods, verify the managed bridge's bound-domain context before claiming coverage.
No new private-runtime interception or runtime fork is authorized.

Successful full third-copy wrapper installation, same-file early reuse and wider
load/patch compatibility remain separate open integration obligations; matched
native failures are not successful installation. Broad textures/graphics/icons,
MP3 decoded reuse/provider identity and production Doorstop/native dependency
adoption remain required and will follow the focused lifecycle increment. No C11
successor or scope reduction is authorized. Native quality and default-off
on-demand behavior remain; all measured performance is pending an unattended grant.

## Nonthrowing reader settlement implemented and tested — 12 September UTC

A newly discovered patching-library copy can now wait for outstanding audio
readers without turning an ordinary decoder error into a process-wide exception.
The isolated GOG tests retain the failing reader's original error for its next
normal consumer, finish the following healthy reader, and reach the real patch
factory only after both are settled. Explicit shutdown also waits for an active
native callback to finish. This is a bounded implementation/test handoff for
independent steward review, not acceptance of all C10 or production integration.

Action `wake-up-c10-nonthrowing-settlement-9e4f8dd2-20260912` started from clean
`9e4f8dd20da59acf1309f440ccb2f28abb79b515`. Final behavior and matching core,
observer, managed bootstrap and native packages are
`4ef927c43d4c126797a8c7079de5547cb7af4e39`. The original boundary/stop controls
used matching packages at `184ffd50eabec99dff5261cee96eda14843354ad` before
experimental prepared admission was opened. Native adapter code is unchanged
between these two revisions; package identities remain distinct in the evidence.

### What changed

The registry is the shared list of readers which still need their original
decoder. Its callbacks now return an explicit completed state: native-ready (1),
original decoder failure retained (2), or disposed (3). Only these outcomes remove
a reader from the list. Incomplete/unknown results and arbitrary callback errors
retain the owner; catching an error is never proof of completion. The owned
reader catches construction, metadata and historical replay errors, preserves the
original exception even if cleanup fails, and allows the remaining readers to
settle. Later normal consumer calls still throw that same original error.

Zero/unknown native identities drain conservatively. Observer/diagnostic errors
force a drain even for a recognized copy. The registry lock remains held until
the drain finishes, so a second entry cannot skip waiting merely because the
native-only flag is already set. Reader demand preserves registry-before-reader
lock order and can complete without waiting for main-thread progress.

The native callback no longer uses `mono_raise_exception`. The primary managed
bridge returns success; an exception-out or unsuccessful result invokes a separate
managed `DrainOnly` bridge with no observer hooks. Continuation requires a positive
complete-drain result. A recovery invariant failure records
`settlement-contract-failure` and terminates this isolated fixture with exit 86;
that last-resort guard is a failed qualification, never an accepted decoder error
outcome. It was not needed in these captures. No arbitrary corruption or resource
exhaustion claim is made.

The fallback bookkeeping correction retains the native OGG/WAV outer-reader
association using the existing exact stream identity after admission closes. It
does not call path getters on that closed path or reopen cache admission. MP3
context admission is unchanged. Experimental prepared admission is selected only
by the explicit functional prepared-audio argument; normal/production admission
is not enabled by these changes.

### Focused functional evidence

- `c10-settlement-stop-01`: held-entry stop is tested in its own process. It
  observes accepting=0 and inFlight=1 while stop is waiting, then inFlight=0 and
  stopCompleted=1 after release. A later entry makes no managed bridge call.
- `c10-settlement-boundary-01`: nine cases pass. The unguarded control reaches its
  real UpdateWrapper prefix factory before the held drain; compiled and reflected
  later-copy calls wait. Diagnostic failures cannot skip settlement. An injected
  primary bridge exception recovers through `DrainOnly` once, with zero recovery
  failures. Direct/reflected ReversePatch entries preserve their ordinary later
  error. Genuine in-memory native WAV recordings feed actual product proxies: the
  malformed-WAV constructor faults first, the healthy reader finishes next, both
  are terminal at factory entry, healthy bytes match native output, and subsequent
  public Read/Position retain the original exception object.
- Final `c10-settlement-audio-cold-05` / `warm-05`: actual native content loading,
  three OGG sample comparisons, pending holder disposal, sticky native fallback
  and cleanup pass. The actual registered lower reader begins unconstructed. A
  worker's ordinary outer-reader demand reconstructs it once while assembly-load
  settlement holds the registry and another thread waits at real UpdateWrapper.
  It replays 307200 bytes with zero mismatches; settlement ordinal 1 precedes the
  sole factory ordinal 2. All workers join; no pending readers remain.
- Final `c10-settlement-advance-cold-02` / `warm-02`: natural OGG/WAV playback,
  native-format checks, selected readiness, current-biome advance preparation,
  native navigation cleanup and save/reload pass. Warm ambience prepares before
  first selection, constructs once and replays 282240 bytes with zero mismatches.
  Cold/warm ambience metadata matches (2051091 samples, stereo, 44100 Hz). Ordered
  reader traces match at menu (20 rows), after playback (47), and after reload
  (64), excluding thread identity. Digests inspect at most 256 bytes per read;
  this is not a full-PCM or performance claim.

All six listed captures exit normally with code 0 and pass their applicable
bootstrap, audio, native shutdown and automatic checks. Normal Root.Shutdown is
used; the prepared runtime installs its stop prefix, and the observer also calls
explicit stop before that shutdown. The native shutdown-end receipts contain
zero in-flight callbacks. The held-entry stop proof is independent of error cases.
Final focused managed tests: **122 passed**. Fixture tooling tests: **63 passed**.
Matching package builds have zero warnings/errors; the final x64 native companion
imports only KERNEL32.dll. No performance or audible inspection was performed.

Three failed captures remain explicit: `audio-cold-01` used the prior closed
bootstrap after a deployment refusal; `audio-cold-02` used the broad modlist which
had already recorded the chosen song and invalidated that first-recording premise;
`audio-warm-03` passed actual concurrent settlement but exposed the missing
sticky-native diagnostic association. Corrections and successful captures do not
relabel these failures. The final reader pair uses the established isolated reader
selection; the natural pair uses the broader approved advance selection.

### Boundaries and stopped handoff

Actual UpdateWrapper factory ordering is proved; successful complete third-copy
wrapper installation/public Harmony.Patch is not. The ordinary later
`ArgumentException` about invalid generic arguments remains identical to control.
ReversePatch entry/error preservation does not claim successful installation.
Same-file early reuse was not repeated and remains unresolved. Broader load/patch
compatibility, textures/graphics/icons, MP3 decoded reuse/provider identity, full
C10 acceptance and production native/Doorstop adoption remain open.

Explicit stop remains mandatory. Automatic shutdown/domain callbacks only close
native admission; an entry past its second check can still race domain teardown.
No blocking wait was added under Mono unload locks, and pinning the native module
is not treated as protection for managed domain state. That concrete automatic
teardown obligation remains open for production integration.

All **28** fixture transactions were reversed. The seven baseline file hashes and
ten absent paths match the pre-assignment state; metadata audit passes and no
fixture process remains. No normal game data, Steam/Deck state, desktop focus,
security settings, parent ledger/state/timer, merge, push or publication changed.
Raw evidence is under
`artifacts/ecosystem-next-20260910/c10/nonthrowing-settlement/`: `packages.json`,
`captures.json` (14 runs, including failures; 82 checked result/receipt hashes),
`paired-correctness.json`, `operations.json`, `restoration.json`, focused test
receipts, independent review notes and final evidence manifest. This owner stops
for steward review and dispatches no successor.


## Steward review: reject native exception propagation; complete settlement next — 12 September UTC

The native adapter can stop a newly discovered patching-library copy before its
patch factory runs. Its tested error path, however, terminated the GOG process.
The failure rejects raising a managed exception from this native callback; it
does not reject the loading capability or the supported early entry boundary.
The same C10 owner should now implement a nonthrowing settlement bridge: finish
each pending reader, retain its original decoder error for the normal audio
consumer when needed, and return to patching only after all readers are settled.
This bounded correction is authorized under standing isolated GOG permission.

### Independent review and evidence

Clean stopped `0893c03267cdaf98822b70793856d0e814a4eb17` descends from assigned
`77c1bf74`. The steward verified 25 artifact hashes, seven distinct package files,
27 captured observer receipts, seven restored baseline file hashes and ten absent
paths. No fixture process remains; normal Steam 50776 and local DeckHost 48204
retain their external executable identities. Ten transactions were reversed.
The final admission guard is source-tested, not the binary executed in the four
captures. Exact source/package identities and results remain in the handoff below
and ignored `native-entry/steward-review.json`.

Run 03 retains three useful byte-array loading cases: the unguarded control runs
its real UpdateWrapper prefix factory before synthetic settlement, while compiled
and reflected guarded calls run it afterward. All retain the same later ordinary
third-copy initialization error. Its overall native-entry test is false: same-file
reuse did not reach the target. Successful wrapper installation, public
Harmony.Patch, ReversePatch and real-reader settlement are not established.
Run 04 terminates at the first injected bridge error, before later tests. In the
captured log the exception and Unity unhandled handler are at lines 85–90; the
failure is directly evidenced, not inferred from the exit code alone.

Two independent source reviews support the correction below. Neither this review
nor an initialization or idle-shutdown receipt is functional acceptance of the
race fix, full C10 coverage, production integration or measured performance.

### Required correction and focused qualification

1. The registered production settlement callback is the owned
   `PreparedAudioWaveStream.SettleNative`. It already retains native constructor,
   metadata and replay failures for later ordinary consumer calls. Make this
   terminal outcome explicit: native-ready, disposed or faulted before removing a
   reader from pending state. A caught arbitrary callback exception is not proof
   of settlement. Continue through remaining readers after a latched reader fault.
2. Remove concrete pre-drain escapes: absent/unknown native identity must force
   conservative settlement, and observer/diagnostic failures must not bypass it.
   Preserve ordinary patch arguments, factory execution and subsequent errors.
   Do not simply swallow `mono_runtime_invoke` errors in C++ and continue with
   readers pending. Remove the rejected `mono_raise_exception` route from the
   new qualification sequence. No immunity to arbitrary memory corruption or
   process-wide resource exhaustion is required; ordinary owned failure paths are.
3. Preserve registry-before-reader lock order and waiting through an already-set
   native-only flag while another thread is still draining. Use a faulting reader
   followed by a healthy reader, original consumer error checks, an injected
   pre-drain diagnostic failure and overlapping reader demand to qualify this
   contract. Reuse existing adequate decoder/replay/cleanup tests and run the
   actual native direct/reflected entry challenge against the revised bridge.
4. Explicit stop settles readers, releases the registry lock, then joins native
   callbacks before normal managed shutdown. Run the existing held-entry stop
   challenge independently of error cases. Automatic native shutdown/domain
   callbacks currently only close admission; they do not join a callback already
   past its second admission check. Their in-flight domain safety is unqualified.
   Keep explicit stop mandatory for this fixture increment, verify the normal
   Root.Shutdown integration, and retain other teardown routes as a concrete
   production-integration requirement. Do not add an unexamined blocking wait
   under Mono unload locks or claim that module pinning protects managed state.
5. Open experimental prepared admission only after the revised error/order/stop
   contract is qualified. Then use focused natural OGG/WAV and selected/advance
   audio regression for changed shared boundaries. Preserve same-file/dependency,
   ReversePatch and full patch-compatibility limits unless actually resolved.
   This checkpoint cannot close those parent obligations or the whole capability.

No production backend/default/compatibility decision changes. Native quality and
optional default-off on-demand behavior remain. No performance, sampling or cost
experiments are permitted without a new unattended grant. Broad textures,
graphics/icons, MP3 decoded reuse and remaining release requirements stay open;
no C11 successor is dispatched at this checkpoint.

## Native entry orders calls, but bridge failure is unsafe — 12 September UTC

The prototype reached the right point in the game: an unexpected Harmony copy
could be discovered while its load callback was still waiting, and its actual
patch factory waited for settlement when the native guard was enabled. However,
the required bridge-error test failed. Raising the captured managed exception
from the native entry callback bypassed the test caller's catch and reached
Unity's unhandled-exception handler; the fixture exited with code 1. **This is
not a qualified race fix. Prepared admission through this adapter remains closed.**

Action `wake-up-c10-native-entry-77c1bf74-20260912` started from clean
`77c1bf74a65be1f3d052fb0777dc0d954a3df6d6`. The supported host route is
`-monoProfiler wakeupentry`: pinned Unity calls the native profiler loader before
root-domain initialization. The companion additionally rejects registration if
`mono_get_root_domain()` is already non-null. It is explicitly selected through
`--audio-native-entry-probe`; this replaces default ETW selection for that
functional process. There was no sampling, timing collection added by the
adapter, or performance experiment.

### Captured results

All four runs used the isolated GOG generation and the same 18-entry selection as
the prior advance-audio workload, muted at 1280x720 and launched minimized without
activation. Core, bootstrap and companion packages were built at
`0004aef5a196542f170c49f9f65e337a8ca2295b`; observer revisions differ below.
The companion SHA-256 is
`65124b49c569b9456a341c0016d7fe39abd3d3ed76608924911753daee2d3432`,
140,288 bytes, with only KERNEL32 imported. Its observed loaded path is the
fixture game directory. These are package/run facts, separate from the later
source change that explicitly closes unqualified prepared admission.

| Capture | Observer source | Result |
| --- | --- | --- |
| `c10-native-entry-boundary-01` | `0004aef5` | Normal exit 0; early bootstrap and idle native shutdown passed. Initial challenge failed before the third mutation entry. |
| `c10-native-entry-boundary-02` | `384ecfc2` | Normal exit 0; the caller error identified third-copy HarmonySharedState/MonoMod generic proxy initialization before UpdateWrapper. |
| `c10-native-entry-boundary-03` | `7c6943bd` | Actual direct UpdateWrapper reached its prefix factory. Unguarded control ran one factory while the load callback held settlement; compiled and reflected guarded calls ran none before release and one afterward. Same ordinary post-factory ArgumentException was retained. Same-file LoadFile did not reach entry before the bounded hold ended; no early-return closure claim. Normal exit and idle shutdown passed. |
| `c10-native-entry-boundary-04` | `c334a225` | First injected direct bridge exception reached Unity's unhandled-exception handler; exit 1, no completed native probe receipt, no qualified shutdown. This rejects the tested exception propagation route. |

The third-copy shared-state failure also occurred in the unguarded control. The
observer consequently invoked the original `PatchFunctions.UpdateWrapper`
directly with genuine third-copy PatchInfo and typed prefix data; no Harmony
bytes or runtime were repaired. Its factory executes before the later shared-state
failure. That establishes the bounded factory-order observation, not successful
third-copy wrapper installation, public Harmony.Patch compatibility, or changed
ordinary exception behavior beyond the matched outcome in run 03.

### What remains unqualified

`mono_runtime_invoke` with exception-out followed by `mono_raise_exception` from
this profiler entry does **not** satisfy the caller-visible failure contract on
the pinned runtime. Do not repeat that route under the same conditions or turn
process termination into a functional pass. The managed registry's new
`audioNativeEntryQualified` flag defaults false and is never opened by binding;
prepared runtime checks it separately from the adapter's initialization status.

ReversePatch exception checks, deliberate in-flight shutdown joining, and the
real-reader worker-demand regression were implemented and compiled in the
observer, but run 04 stopped before they executed. No prepared-audio/native-entry
cold or warm run was started. The finite filter and ordinary idle shutdown worked;
that does not qualify failure recovery, all inlining or domain-lifetime behavior,
necessary dependency-binding routes, or same-file early-return closure.

The next architecture decision must provide either a complete nonthrowing
settlement bridge or another supported way to stop mutation after a bridge
failure. The existing Action/dictionary drain is not such a proven total contract;
ordinary decoder faults are already latched, but swallowing an escaped bridge
exception could still return with readers pending. No raw detour, private runtime
layout/context, replacement runtime or production backend adoption is authorized
by this failed prototype. Standing isolated GOG correctness permission remains;
this is a failed implementation condition, not a request to repeat testing approval.

### Evidence and restoration

Ignored `artifacts/ecosystem-next-20260910/c10/native-entry/` retains exact startup
and loader disassembly, module identity evidence, `companion-package.json`, the
operation journal, focused test output and the final stopped-state record. The
four complete run captures remain under `.rlo-test-instance/results/`; run 04's
Player.log lines 82–87 identify the retained exception and Unity unhandled handler.
All ten fixture transactions were reversed. Seven baseline file hashes and ten
absences, including the new companion, are checked again at handoff. Normal
Steam RimWorld and the local DeckHost process were not changed; no Deck access,
UI automation, focus change, merge, push or publication occurred.

Full C10 coverage, production integration, textures/graphics/icons, MP3 decoded
reuse and all pending individual/combined measurements remain open. The same
C10 owner returns this stopped prototype and precise error-contract dependency
to the steward for independent review; no self-acceptance or successor dispatch.

## Steward architecture review: bounded prototype authorized — 12 September UTC

Stopped `2f1befdd` passed review as source evidence and a conditional design,
not a working race fix. The steward verified seven evidence hashes, seven
fixture hashes, nine absences and the actual Mono binary. Its SHA-256 below
corrects a missing character in the original report/work-record transcription;
the recorded 63-character value was not a valid complete SHA-256. Raw evidence
remains preserved with the correction in `loader-race/steward-review.json`.
No fixture process remains and no instrumentation has been installed.

The owner's standing isolated GOG prototype/correctness permission covers the
next bounded experiment after this architecture review. No further testing
approval is required. This approves only the reviewed public Mono instrumentation
route with the conditions below, not production Doorstop/companion adoption,
new defaults, raw detours or a replacement runtime. No sampling, timing collection
or performance measurement is part of the prototype.

**Registration correction:** use a supported native profiler initialization
path before managed execution; then bind/root the managed bridge with prepared
admission still closed. Do not call `mono_profiler_create` or load the profiler
from `Entrypoint.Start` merely because Harmony has not compiled yet.
[Mono registration contract](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/profiler.c).
Establish that startup path in the pinned environment before executing it; if it
requires another unreviewed integration, return that precise design dependency.

The public filter takes `(prof, method)`, and method entry takes
`(prof, method, context)` with entry-only flag 2. Keep the filter native and
nonblocking, instrument the finite target methods regardless of later recognized
identity, and verify actual direct/reflected/later-copy entry placement and
inlining behavior. Export presence and upstream source do not qualify the shipped
runtime by themselves. No argument/context introspection is needed.
[Public callbacks](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/profiler-events.h),
[flags](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/profiler.h).

Release adapter identity locks before the registry bridge; preserve existing
registry-to-reader lock order. Native-only admission is set before draining ends,
so another entry must still wait for completed settlement. Do not bypass nested
entries with a blanket reentrancy flag. Method-entry callbacks have no veto return:
qualify bridge-error propagation or a complete nonthrowing settlement contract
before enabling prepared admission. A failed bridge must not return into mutation
with readers pending. Native reader faults retain their existing later-consumer
failure latch. Keep callback storage alive and prevent managed reentry/in-flight
use during domain teardown; retaining a DLL/delegate alone does not prove shutdown.

Existing synchronous reader settlement and prior worker-demand evidence support
testing the specified transition. Do not require proof about every conceivable
caller-held lock or create a universal loader framework. The focused deterministic
third-copy case, same-file reuse/necessary load routes and overlapping reader
demand must establish actual ordering without main-thread pumping. An unguarded
control must demonstrate the challenge is active. Add only the natural-audio
regression warranted by changed shared boundaries after this contract works.
The same C10 owner executes this bounded prototype from the exact new review base;
full C10, native adoption, other retained requirements and performance stay open.

The current load callback cannot prevent another thread from using a newly
visible Harmony copy while pending audio readers are still being completed.
The pinned game runtime confirms that an earlier assembly-load notification
also has a bypass. No sufficient managed-only correction was established.

The proposed next dependency is a small, bundled native companion using Mono's
supported method-entry instrumentation interface. It would guard only the two
Harmony operations already covered by our recognized copies, before an
unexpected copy can run them. **This is an architecture proposal, not an
implemented or qualified fix.** Its exact execution and lock-safety contract
must be established before it may settle readers in the live game.

## Assignment and unchanged behavior

Action `wake-up-c10-loader-race-fe9cb864-20260912` began from verified clean
`fe9cb8641608cc6a695044eea207f9a359cfe422` with exclusive single-checkout ownership.
This pass inspected current bootstrap/registry/contracts, R1 evidence, native
binary bytes and upstream source. It did not edit product code, build packages,
install hooks, execute native instrumentation or launch the fixture. No new
performance, texture or MP3 experiment occurred.

The previous advance-audio increment remains preserved. R1's synchronous
loading-caller guarantee remains narrower than concurrent use. Broad C10,
production bootstrap adoption and performance are still open; no new exclusion,
default change or successor is approved by this document.

## What the exact runtime establishes

The isolated GOG `MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll` is 7,895,744
bytes, SHA-256
`d2f4348a5aa80bbdd0e73582cc2a00b3a17fe4a497da7052436e780cdee2a0fa`.
The following addresses are offsets from that DLL's loaded base (relative virtual
addresses, or RVAs). Export names are exact; internal function roles were matched
by instructions, calls and diagnostic strings, without a matching debug-symbol
file. The raw evidence records the PDB identifier and this limitation.

| Boundary | Exact observed behavior | Consequence |
| --- | --- | --- |
| Native load core `0xB1530` | Writes `image->assembly` at `0xB1965` and the global assembly list at `0xB19BD`; unlocks before calling load hooks at `0xB19F2`. | A load-hook callback is already later than shared image publication. |
| Existing-image branch `0xB18D3`–`0xB1936` | Returns the existing assembly without dispatching load hooks. | Another loading thread can bypass the first thread's blocked hook. |
| Builtin domain hook `0x9FC70` | Registers assemblies at `0x9FD57`, unlocks the domain at `0x9FD69`/`0x9FD71`, then reaches the managed-event bridge at `0x9FD86`. | The domain list is available while managed `AssemblyLoad` is still running. |
| Recursive registration `0x9F610` | Adds the assembly and already-loaded references under the domain lock, recursively calling itself at `0x9F8D7`. | Checking only the top-level assembly does not cover referenced publication. |
| Exported `mono_domain_assembly_foreach` `0xA7DD0` | Invokes its visitor while holding the domain lock. | Its visitor is not a safe place to wait for reader settlement. |

`mono_install_assembly_load_hook` (`0xAE160`) prepends a callback before the
builtin domain callback. This establishes ordering within that hook list, not
complete publication coverage. The separate managed-array-shaped snapshot path
copies pointers and releases the domain lock before constructing wrappers, but
the exact internal-call name-table binding was not resolved in this bounded pass.
It must not be substituted for proof about the exported foreach visitor.

The upstream [domain-registration source](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/appdomain.c)
and [assembly-loader source](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/assembly.c)
provided the initial call-flow hypothesis. The binary observations above confirm
the relevant ordering locally; they do not establish every upstream caller's
lock state. In particular, releasing the assemblies/domain locks does not prove
absence of a loader, image, compiler, class-initialization or caller-held lock.

## Why a family of managed wrappers is not the selected fix

Pinned `mscorlib` implements public `AppDomain.GetAssemblies()` as a small
managed wrapper around a private native internal call. A result guard could keep
the exact array and assembly objects while completing settlement before that
particular caller receives them. It would close the specific array-return path
if installation and already-inlined callers were covered.

However, `Assembly.LoadFrom(string)` independently calls its native loading
entry, and the cached-image branch above can return while the first notification
is blocked. `Type.GetType` also reaches a separate native resolution entry,
`RuntimeTypeHandle.internal_from_name`. Dependency binding does not need a
managed array lookup. A wrapper around `LoadAssemblyRaw`, one public load
overload, or `GetAssemblies` alone does not cover these routes. Their full
resolution-to-use ordering remains unqualified. Adding wrappers until a few
tests pass would not establish the required executable-use boundary.

## Proposed companion and exact execution contract

The proposed companion would use the existing early bootstrap and only these
verified public Mono exports:

| Export | RVA | Intended use |
| --- | --- | --- |
| `mono_profiler_create` | `0x1E8980` | Register a process-lifetime adapter. |
| `mono_profiler_set_call_instrumentation_filter_callback` | `0x1E9A40` | Select instrumentation for the two patch-entry methods. |
| `mono_profiler_set_method_enter_callback` | `0x1EC350` | Observe execution before the selected method body. |

Mono calls this its profiler API, but this proposal uses execution ordering only:
no sampling, timings or performance measurements. The upstream
[profiler interface](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/profiler.h)
defines an entry-only instrumentation flag and separate filter/entry callbacks.
The pinned exports support investigating that interface; export presence alone
does not prove the exact ABI, generated-code behavior or safe managed reentry.

The proposed contract is:

1. Register the native profiler from its supported initialization path before
   managed execution; the existing managed bootstrap is too late for this step.
   Install the filter before either relevant Harmony copy compiles its mutation
   entries. Bind the rooted managed bridge later while admission is closed. Preserve exact original/replacement
   managed assembly references and register their corresponding native identities
   while admission is closed and empty. Prepatcher's old-copy reflection-only
   flag does not substitute for either identity or an execution guard.
2. The compilation filter selects only `0Harmony`'s
   `HarmonyLib.PatchFunctions.UpdateWrapper` and `ReversePatch`. It performs
   native metadata identification only. It must not acquire the reader registry,
   reconstruct decoders, invoke managed code or wait for another thread.
3. At actual method entry, an unexpected assembly identity synchronously enters
   the existing sticky native-fallback/epoch/settlement mechanism. The original
   method body cannot run until every pending reader is settled. This must also
   wait when another thread has already closed admission but is still draining.
   Recognized copies retain their existing emitted/managed mutation guards.
4. Preserve native arguments, return values, patch factories, transpilers and
   exceptions. No replacement audio factory, second decoder or altered public
   assembly-array result is introduced. A failed bridge cannot silently permit
   mutation with unsettled readers; the installation must remain unqualified if
   it cannot preserve the required error/continuation contract.
5. Keep the native module and callback storage alive until Mono shuts down.
   Root any managed bridge for the same lifetime. Do not unload the companion,
   remove live callbacks or change registry ownership during a map/save reload.
   A shutdown protocol must prevent calls into a destroyed managed domain.

The single missing implementation dependency is **a demonstrated lock-safe
method-entry bridge before the unrecognized Harmony body executes**. Required
pre-implementation facts are the pinned ABI and entry flag, placement before
the body for normal/reflected calls, later-copy compilation/inlining coverage,
and safe managed settlement in the actual invocation context. A method-entry
callback may still inherit locks held by a resolver or initializer caller.
Moving settlement from a JIT callback to method entry does not by itself prove
that those locks are absent. No draining inside an unverified runtime lock is
proposed as acceptable behavior.

If this bridge cannot be established, retain the capability as open and return
that failed contract. Do not install raw internal-address interception, add a
general detour framework, or label managed-wrapper-only coverage complete.
Changing the Mono runtime's publication protocol would be a substantially larger
alternative requiring its own explicit design decision, not an incidental fix.

## Bounded validation after architecture review

First qualify only the adapter's exact entry/lock contract in the isolated GOG
path. Use controlled barriers, not timing delays: hold the first thread's
managed load callback before settlement, let a second thread discover the third
copy, and observe its real relevant patch entry. Release settlement through a
coordinator and require settlement completion before the harmful factory or
transpiler begins. An unguarded control must demonstrate that the same challenge
can enter before settlement; an inactive challenge cannot pass.

Include the independent same-file reuse route and necessary byte-array/file
loading routes; explicitly test or retain the dependency-resolution limitation.
Verify unchanged lookup objects, ordinary patch return/exception behavior and
no deadlock when native reader demand overlaps settlement. Keep the two
recognized Harmony copies active by their original reference identities.

Only after that proof should changed shared-boundary regression cover natural
deferred OGG/WAV and selected/advance native sound behavior. Reuse adequate
existing audio data/lifecycle checks instead of restarting codec or texture
investigations. Unknown execution routes remain explicit; this work is bounded
to the recorded third-library-copy counterexample, not arbitrary private-memory
tampering or unrelated detour engines. No such new live test ran in this pass.

## Review boundary, evidence and stopped state

The steward must review the native dependency, callback/lock contract and lifetime
plan before this new runtime instrumentation is adopted or executed. An approved
bounded prototype would still not approve production Doorstop/companion adoption.
If later adopted, the companion must be bundled; no manual user installation is
part of the proposal.

The fixture was not changed. Seven baseline hashes and nine absent paths match
the prior restoration; the verified normal Steam process 50776 and local
DeckHost process 48204 remain outside the stopped fixture and were not modified.
No new package, capture, performance result or functional acceptance exists.

Private evidence is under
`artifacts/ecosystem-next-20260910/c10/loader-race/`: `baseline.json`,
`native-review.md`, `exports.json`, the bounded disassembly extracts and their
read-only extraction scripts. The existing R1 and advance-audio captures remain
separately attributed. Independent managed-route and native-boundary reviews
support the scope limits above; neither claims a qualified replacement barrier.

## Supported public patch integration and repeated file loads — 12 September UTC

A normal public Harmony patch now has direct baseline-versus-enabled evidence:
it installs, executes and can be removed while preserving complete MP3 output.
Wake-Up settles pending decoding before the patch's factory runs. No product
correction was needed for these supported operations. The consumer uses genuine
`Harmony` and `HarmonyMethod` types from the game's working library; it does not
manufacture substitute Harmony types or repair an unsupported third copy.

This increment starts from reviewed `71eb505200e30e11b5a42b0ff51d5b937861faf9`.
Final functional source and matching core/observer/bootstrap packages are
**`c3610d2e466115d9a5cc7d1cedb13d2c82cd938f`**. The only bootstrap change is a
disposable notification observer before its locks, with isolated diagnostic
exceptions. Other changes select and report focused fixture controls. Native
entry, provider, arithmetic, error and ownership behavior are unchanged.

### Public API behavior

`c10-public-patch-absent-02` physically parks Wake-Up and runs without Doorstop.
The public `Harmony.Patch` call targets the actual `Mp3FileReader.Read` method,
returns a wrapper, publishes the owned prefix and executes that prefix 131 times.
Public `Unpatch` removes the owned patch; a fresh read then executes no prefix.
The consumer records its actual library bindings and assembly-name resolution.
A copy of the original supplier file, made only in the private observer folder,
also supports repeated `Assembly.LoadFile` with the same returned assembly object.

`c10-public-patch-enabled-01` repeats the public operation with prepared audio
enabled and an actual registered MP3 session. Before patching, that reader has
one cached frame and 400 bytes of skipped compressed history. At the real patch
factory it has replayed the frame into the same retained decoder, cleared the
history, left the pending registry and entered native fallback without a fault.
The public patch returns and executes 131 times. Reading finishes with 59 native
conversions including the replay. Unpatch restores ordinary behavior, borrowed
input remains open, and a later attempted cache admission is correctly refused.

Baseline, patched and restored complete reads all match the same count/position/
decoded-byte trace hash:
`6AAE50DAA65B42074A6BC6375A2E74EC0F4C22BF90A2E8F9E122A1F4D686BE75`.
No duplicate reference decoding is counted as a performance result.

### What the same-file wait actually means

`c10-public-patch-same-file-01` holds the registered real MP3 settlement callback
while a first file load is notifying managed handlers. A second thread loads
that same file. The new observation sees its repeated load notification before
bootstrap/registry lock acquisition. Neither load returns and no patch factory
runs while settlement is held. After release, both loads return the same assembly,
the same MP3 decoder is settled, and the later public patch installs and executes
through the consumer's genuine working Harmony binding. Both workers join; public
unpatch and later fallback behave as above.

This proves waiting in the repeated synchronous notification/owned registry path,
not a Mono loader-lock wait. Exact retained filename-open inspection supports it:
the existing-image branch calls the load-hook dispatcher at `B053E` before returning
at `B0564`. The narrower losing-publication return in `B1530` requires both loads
to pass an earlier absent-image observation; starting the second after notification
is held does not deliberately reach it. Prior entry-order controls remain separate
evidence for discovered foreign copies. This increment does not force the earlier
unsupported third-copy public initialization or claim a new reverse-patch matrix.

### Retained failures and evidence

The first observer build at `7ebc0292` failed a nullable wrapper annotation; it was
corrected before launch. `c10-public-patch-absent-01` at `d3b1d9eb` failed because
the new fixture factory omitted the required `MethodBase` parameter. Exact
supplier `Patch.GetMethod` inspection identified the missing signature, corrected
at `c3610d2e`; no Harmony or native behavior was changed to make it pass. That
failed baseline capture remains failed and exits normally with code 0.

The final three functional captures pass public-patch and automatic checks.
Both enabled captures also pass natural OGG/WAV/MP3, gameplay save/reload,
bootstrap/preloader and native shutdown checks. Natural warm LightSMG uses one
native conversion and 58 cached frames with complete native-equal Unity samples.
The prior arithmetic, sentinel, repeated-reset and fault matrices were skipped.
The 72 existing tooling tests passed before the measurement-selector extension.

Private evidence: `artifacts/ecosystem-next-20260910/c10/public-patch-compatibility/`
contains operation logs, `run-summary.json`, `package-audit.json`, the finite
`loader-source-review.md` and restoration records. The functional audit verifies
six immutable receipts, 24 payloads and 46 observer files across four captures,
including the retained failed baseline. These are bounded functional results,
pending independent review; they do not close C10 or approve a production backend.
The same owner continues only into the separately authorized audio measurements
under the new 12 September unattended window. Performance results and final
restoration/STOPPED ownership handoff are recorded separately.
