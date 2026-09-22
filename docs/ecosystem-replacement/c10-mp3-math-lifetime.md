# C10 MP3 lifetime and arithmetic correction

The correction makes prepared MP3 audio follow the same reader limit and cleanup
ownership as the other supported audio formats. It also records the arithmetic
conditions of every decoded frame, so later fallback can reconstruct the retained
decoder under the conditions that produced the earlier cached output. The isolated
GOG controls demonstrate these behaviors; independent review and broader C10
acceptance remain separate.

This continues the [reviewed MP3 prototype](c10-mp3-frame-cache-prototype.md#steward-review-retain-prototype-correct-lifecycle-and-arithmetic-history--12-september-utc)
from assigned base `fe151dfaea64d423e663989affe6a4a64f299c40`, preserving the single
`codex/ecosystem-next-plan-review` ancestry. The initial correction is `88f7955f`,
the expanded controls and shared admission are `12f63cf0`, and disposed reader
reference release is `e70b0e9a140750d16048322916e22bf94bdd7300`.
Native loader diagnostics follow at `f5215b2e`; the exact equivalent loader-page
correction is `fde4073b37e9d3b7d954f31f04f1a91015d3a8f9`.

## Reader ownership and ordinary cleanup

A streaming clip can outlive its source file, and the original game does not
always dispose its MP3 decoder when it closes that file. The product now tracks
the source, original outer reader, clip and private MP3 session together.

`PreparedAudioRuntime` reserves a place under the shared **16,384 reader limit**
before publishing a prepared reader to a native worker or callback. The same
reservation applies to OGG, WAV and MP3. A load still being constructed is marked
as loading, so cleanup cannot mistake its unpublished clip for a destroyed clip.
MP3 registration also checks the pending-session bound under the registry lock.
Unsupported admission keeps ordinary native decoding.

After the original source close succeeds, the product joins the original outer
reader's `lockObject`. This waits for any entire read already in progress,
including a compressed frame fetched before source closure. It then drops cached
history and pending registration without replaying skipped frames. The original
decoder remains available until Unity confirms the clip is destroyed. Destroyed
clip cleanup disposes the borrowed-input MP3 reader, preserving native source
ownership. Failed unpublished loads release their reservation and decoder too.

When native disposal completes, the session clears its strong references to the
private reader and decoder. Diagnostic receipts retain only the integer decoder
identity and result fields. A native manager retaining a destroyed clip key
therefore does not retain the disposed reader's managed buffers through this
session. Full cache hits still do **no catch-up on disposal**.

The frame hooks now initialize through the shared supported-provider admission
inside the prepared-audio route, rather than only the `frames-*` observer modes.
The overall prepared-audio integration remains experimental and fixture-enabled;
this is not public production activation or support for arbitrary codecs.

## Preserving arithmetic across cached and native work

MP3 decoding calls a Windows math routine whose implementation can change when
the process changes its fused multiply-add setting (FMA). A thread can also
change how floating-point results are rounded. Checking those settings only at
the first frame cannot describe a recording made under several later settings.

Each frame now carries a **64-byte arithmetic context**. The version-2 persistent
frame format records the incoming and outgoing MXCSR register, which contains
SSE arithmetic controls and status flags, the qualified math-call counts, the
equivalent implementation identity, and incoming Win32 `LastError`. Reset
operations have their own transcript entry. Version-1 frame records are not
reused as version 2.

The exact supported provider is `l3codeca.acm`, SHA-256
`5d7f5961411fe64b2bc48297e7c424aaf19272aa814287b0a4ff1c469b8bd3df`;
the exact UCRT is
`5c52e3a303baaac0e0af8bd9b96134993da34bc9d834a31ef37e1d2cdc7fe192`.
The installed observer retains the original exported `DriverProc` and native
decoder. The pinned `_o_pow` entry at RVA `0x79BF0` uses its original UCRT wrapper
at `0x5E910`, including setup, cleanup and native exceptional cleanup. An RVA is
an address relative to that particular loaded binary, not an address guessed
across Windows versions.

The two actual math branches are FMA at `0x5E990` and SSE at `0x9DD10`.
Instrumentation observes the branch the selector actually entered on every
owned call. The public FMA getter is insufficient: it tests nonzero, whereas
selection tests `(flags & 3) == 3`. The first native conversion is certified too,
because its internal decoder state influences later frames.

For the pinned provider's positive integer bases and fixed exponent, the verifier
calls the other implementation through the **same original wrapper**, starting
from the same pre-call environment. Identical double results and effects are the
fast proof. Otherwise it compares the exact value the codec consumes: both signs
and all 128 pinned scale constants, with separate SSE multiply and float
conversion instructions. It compares result bits and arithmetic effects. This
establishes equivalent subsequent decoder state despite intervening FMA branch
choices; no persistent per-call branch tape is needed for an admitted frame.
Unsupported inputs, unavailable CPU/OS support, unmasked exceptions or failed
equivalence refuse optional recording while retaining the original result/error.

A cache hit checks compressed input, output capacity, arithmetic controls,
required incoming status flags and `LastError`, then applies the recorded raised
flags. If the next operation no longer matches, the retained decoder catches up
using its skipped compressed operations and their individual old contexts.
Historical controls apply **only inside the owned native `DriverProc` conversion**.
The equivalent SSE implementation is called through the original UCRT wrapper.
No managed parsing or bookkeeping occurs under temporary historical controls.

Native `__finally` restoration preserves the caller's x87 environment/registers,
MXCSR, the actual dynamic UCRT's `errno`/`doserrno`, and Win32 `LastError` on every
exit. The assembly snapshot uses FXSAVE64/FXRSTOR64 while retaining the current
XMM registers required by the Windows calling convention. Production verification
and replay never write the process-wide FMA selector or intercept its setter.
Explicit test controls change it through its existing API and restore the saved
raw selector correctly using its enable bit.

Skipped history retains compressed bytes and the 64-byte operation context,
bounded by **2 MiB and 65,536 operations**. Persistent chunks remain under the
existing shared cache-store budget. No unbounded PCM history or independent
large PCM cache is introduced. Native codec errors retain their original error
handling; qualification failures are not relabeled as original codec errors.

## Functional evidence

The final selection adds Melee Animation to the existing audio workload. Its
`AM/SummonWhoosh.mp3` is the only other MP3 found in the frozen mod inventory;
product admission contains no package or content allowlist. Actual natural
Unity clips are compared in full against independent original native loading.

| Natural sound | Cold conversions | Warm conversions at `fde4073b` | Complete float samples |
| --- | ---: | ---: | ---: |
| LightSMG, `tro.vwe.overhaul` | 59 native | 1 native + 58 cached | 67,968, mono 48 kHz |
| SummonWhoosh, `co.uk.epicguru.meleeanimation` | 72 native | 1 native + 71 cached | 165,888, stereo 48 kHz |

The corresponding full-sample SHA-256 values are
`DCDFECC29F34086FFCE31BA4F859FC62B85827D38655D0199B9115FB9763DB56` and
`79C97255B101E3CA1D5A0C910348E3E93A3B7E36AF03B9D86A27004D7DAA36BC`.
The warm clips reuse 135,936 and 331,776 PCM bytes respectively, with no disposal
replay. PCM means decoded audio samples before Unity's float representation.
LightSMG completes ordinary SoundDef playback and gameplay reload. The additional
sound starts through its ordinary SoundDef and survives reload; its complete
playback duration is not separately observed. Existing OGG/WAV and music controls
remain included. The fixture is muted; these are not audible-quality claims.

The same captured observer supplies the following specific controls:

- **Streaming teardown:** a real native `Manager.Load` streaming MP3 has 36 cached
  frames and 14,400 history bytes. Original holder `ClearDestroy` closes its source;
  history and pending registration are removed without replay. After actual Unity
  destruction, the decoder is disposed and both private references are absent.
- **Unpublished failure:** an injected error after the real Load prefix and native
  reader factory, before clip/worker publication, passes through the real finalizer.
  It preserves the original error object and borrowed source, releases the
  reservation and decoder, and performs no replay. This is deliberate injection,
  not a spontaneously occurring native failure.
- **Chunk/reset behavior:** 18 native seek/read-to-EOF cycles record 1,062
  conversions. A complete warm transcript uses one native and 1,061 cached frames,
  with 18 resets and no replay. Removing, corrupting or truncating the second
  chunk after consumption begins produces 768 cached frames followed by 768
  historical conversions in the same decoder. All read results, positions and
  samples match native behavior; refusal is `cache-operation-missing-or-corrupt`.
  The damaged chunk is restored after each control.
- **Original errors:** partial reads, seeks, EOF and metadata match the original
  reader. An invalid destination offset preserves the original exception type
  and HResult; disposal leaves the borrowed input open.
- **Changed arithmetic:** changing FMA after the second frame still permits all
  58 later cached frames with native-equal PCM. Changing rounding too causes one
  skipped frame to be reconstructed under its old context, followed by ordinary
  current native decoding. A cold recording spanning both rounding modes then
  reuses all 58 later frames under the matching mixed sequence.
- **Within-conversion change and reversal (ABA):** 43 actual owned `pow` calls
  observe 21 FMA and 22 SSE selections, with 86 test-only selector writes and zero
  restoration failures. The raw selector returns from 3 to 3. Cold publication
  and subsequent warm reuse remain native-equal. This is deterministic in-call
  variation, not random concurrent-thread stress.
- **Exceptional restoration:** the native structured-exception control reports
  caught/restored `[1,1]` and identical before/after MXCSR values `[8119,8119]`.
- **Relevant loader mutation:** after one cached frame and a rounding change, a
  real extra copy of the existing `0Harmony` assembly closes admission. The same
  decoder replays exactly one historical frame, clears history without faulting,
  and finishes with native-equal changed-rounding output. This control is last
  because admission remains closed for the disposable process.

The fixed arithmetic PCM hash is
`23FC176229AC69021EDF4147B0D4FE250096C094CAB19D869E185D180FF14BA5`;
the changed-rounding hash is
`7B3F10B42E957CAA7B4CB4B1427B3753983931E2A53525BC794D7EF58C80D239`.
These byte hashes describe the native decode control, distinct from the full
Unity float-sample hashes above. Observed pair qualification used double
equivalence; the weaker scale/suffix proof is source-backed but not claimed as
an exercised branch of these clips.

`cold-01` exited successfully but its initial cache-damage control fell back on
arithmetic state before reaching the damaged chunk. It is not damaged-boundary
proof. Preserving the test operation's native environment and requiring the
actual second-chunk refusal corrected that control in `cold-02` and `warm-02`.

## A captured Windows loader alternative

Windows can redirect the codec's indirect-call jump when it loads the DLL. The
first final-source warm run encountered a form outside the prototype's admitted
locations. `warm-03` at `e70b0e9a` and diagnostic `cold-04` at `f5215b2e` refused
MP3 frame admission before any prepared MP3 session was created. Both processes
exited normally with gameplay and shutdown checks passing, but their prepared
audio probes failed. They are retained failures, not reuse results.

The captured provider jump at RVA `0xF010` was `E9 AB E0 00 00`, targeting module
base plus `0x1D0C0`. The pinned DLL's declared image size is `0x1D000`; its dynamic
relocation table (DVRT) names only `0xE2E0`, so file metadata alone could not
qualify the new target. Diagnostic-only `f5215b2e` captured the actual target:
`48 FF E0`, a complete `JMP RAX`, in a committed read/execute `MEM_IMAGE` region
beginning at module base plus `0x1D000`, owned by that same module allocation.

The already qualified local dispatcher is `FF E0`, also `JMP RAX`. These complete
instructions have identical register, flag and stack effects; no subsequent
bytes are executed. The independent native reviewer supported extending the
existing exact NTDLL instruction-equivalence rule to this observed loader-added
image page. `fde4073b` preserves the pinned relocation chain and checks the exact
target instruction, module allocation, page start, commitment, image type,
read/execute protection and full instruction containment. Subsequent native
validation rechecks accepted allocation ownership, jump bytes and dispatcher.
This does not admit arbitrary jump chains or nearby executable allocations.

Full native statistics now have 45 entries. Entries 37–44 record the last loader
target's region/allocation addresses, state, type, protection, readable byte count
and first two 64-bit words. The diagnostic does not alter admission or execute
the captured bytes. This bounded correction follows an actual reproducible
refusal, rather than expanding the supported native contract speculatively.

## Final source, captures and restoration

The final packaged source is **`fde4073b37e9d3b7d954f31f04f1a91015d3a8f9`**.
Matching GOG core, independent menu observer and native bootstrap packages were
built and deployed from that exact revision. Final captures
`c10-mp3-math-cold-05` and `c10-mp3-math-warm-05` both pass bootstrap observation,
native-entry shutdown, preloader observation, prepared audio, automatic exit and
gameplay checks. Both exit with code 0 and retain the independent menu repaint
observation. The warm run restores the cache from that exact cold source.
The final pair repeats all controls above, including released reader references.

| Final payload | SHA-256 |
| --- | --- |
| `WakeUp.dll` | `fc3419c730a528b7895572369ec90f43a4a635a58be44836583a441714863ca3` |
| `FixtureMenuObserver.dll` | `ab4d1eea52b8a98de103d120f6d9ef02b3e13631f3d899ecff68f8b92b4df9d2` |
| `WakeUp.AudioBootstrap.dll` | `952aabe5fc711b37686b13add30381e58ded38ca83698a68f8d8a2c3aa59b00e` |
| `mono-profiler-wakeupentry.dll` | `023efc291fca0961f5f8a0f588a03b67fa2fd1d141435ceb709d88898f4b5069` |

Package directories and their adjacent immutable JSON receipts are under
`artifacts/fixture-package/<source>`, `artifacts/fixture-menu-observer/<source>`
and `artifacts/fixture-audio-bootstrap/<source>`. Focused managed validation
passes **128 tests**, with **72 tooling tests** passing, in
`artifacts/fixture-tests/dcfae0eb616d412abba5d492ea27eb1e`. The subsequent native
loader changes have successful native builds and the final packaged live pair;
the managed suite is not represented as a test of those native instructions.

Ignored correction evidence is under
`artifacts/ecosystem-next-20260910/c10/mp3-math-correction/`. It includes the
native source/disassembly reviews, operation/build/test logs, `run-summary.json`,
`package-audit.json`, `operations.json`, `restoration.json` and the evidence
manifest. The audit verifies **15 immutable receipts, 60 payload files and 126
captured observer files** across five source revisions and eight captures. Six
prepared-audio probes passed; the two admission failures remain explicit.

All **25 successful fixture transactions** were rolled back in reverse order.
The seven pre-assignment file hashes and ten expected absences match. The
fixture remains GOG generation `gog-rev573-20260910-194017-cf1a1eb0`. A fresh OS
process query confirms no fixture game is running. The normal Steam game and
Apollo's RimWorld host helper were present outside the fixture and were left
untouched. The final handoff is STOPPED with children stopped and checkout/build/
fixture ownership returned to the steward. No successor was dispatched.

## Remaining scope

No performance test, microbenchmark or speed claim was made. Conversion counts
show functional reuse only. No reader-cap saturation/resource-pressure test was
performed. Another native codec and actual configuration-message mutation remain
unexecuted; source qualification and prior finite refusal evidence do not make
those live-test claims. Arbitrary replacement of already executing native code
after skipped history remains an explicit qualification failure boundary.

The single GOG fixture is the only tested platform. Production integration,
broader texture/graphics/icon coverage, remaining compatibility, independent
functional review and later authorized performance work remain required. This
document does not grant full MP3/C10 acceptance, a successor, backend adoption,
Steam/Deck promotion, merge or publication.

## Steward review: arithmetic passed; isolate load-state inspection failure — 12 September UTC

Stopped `76670114`, tested `fde4073b`, preserves real MP3 reuse and fixes the prior
streaming lifetime/shared-cap and mixed-context recording defects. Independent
native review supports the finite per-call arithmetic equivalence, historical
wrapper replay/restoration and exact loader-page extension. It does not qualify
another codec, arbitrary native-code replacement, production adoption or speed.
The weaker scale/suffix equivalence has source review; captured clips exercised
the identical-double path. Extra cold verification work still needs measurement.

The steward verified 101 evidence-file hashes, 15 immutable package receipts,
60 payload files, 126 captured observer files and eight run/probe identities.
The final cold/warm pair passed all recorded audio/bootstrap/shutdown/gameplay
and automatic flags with normal exit. Both authored MP3 clips match native full
sample output and reuse 58/71 frames after one native conversion. Streaming
cleanup, mixed-rounding recordings, deterministic in-call selector changes,
cached resets, second-chunk damage fallback and extra-Harmony settlement are
preserved as bounded functional evidence. The two admission-refusal captures
remain failures. Seven baseline hashes and ten absences match after 25 restored
transactions; the verified normal Steam game and Apollo helper were untouched.

One source-identified race still requires the same owner's correction.
`FinishMp3Life` and `CleanupDestroyed` call `LoadReturnedSuccessfully` outside the
previous optional-completion catch. That helper enumerates the native mutable
`audioLoadState` dictionary. Native load and state publication can modify it
without the product's lifetime lock. A failed inspection can consequently replace
a normal Load return or escape `Root.Update`; this has not occurred in the
reviewed captures.

Return an explicit indeterminate state on inspection failure. Preserve the
original return/error, retain a non-null potentially live clip and its ownership,
and retry cleanup later. Only established failure/destruction should trigger
release. Do not call the public getter, introduce extra callbacks or add a lock
that native writers do not use. Prove the narrow failure/indeterminate/retry
behavior with focused correctness evidence and reuse the adequate arithmetic,
cache and lifetime captures above. Do not expand this into random stress or a new
validation framework.

The same C10 orchestrator receives this correction from the steward's new exact
review commit. Full functional acceptance of this increment remains pending that
fix; full C10, broader assets/compatibility/production integration and measured
performance remain open. No new operational or performance permission is implied.


## Optional load-state failure correction — 12 September UTC

Optional inspection can no longer turn a successful native audio load into an
error or dispose a clip whose state could not be read. The product now reports
`Indeterminate`, retains the non-null clip and its reader/source ownership, and
retries on a later cleanup call. This implements the bounded correction assigned
from `613708d8c27621ac22e30e1fd18417f7400b7786`.

`PreparedAudioNative.InspectLoadState` catches optional reflection/enumeration
failure. It inspects only stored key reference identity, without the public
load-state getter, Unity properties/equality, dictionary comparer callbacks, or
a lock that native writers do not share. Stored Loading/Loaded is active;
explicit Failed is conclusive failure. Missing/Unloaded/unknown state remains
indeterminate. A null managed clip reference is conclusive absence.

Completion publishes an optional index only after an active observation. The
original Load result/error and nested-context restoration remain intact.
`FinishMp3Life` retains a non-null potentially live result even if an original
error accompanies it; only known failure/absence releases its private resources.
`CleanupDestroyed` keeps indeterminate live clips, retries later, and removes
already disposed private sessions normally. The existing legitimate Unity
clip-destruction check remains unchanged. Removed lifetimes are not revisited by
subsequent cleanup calls.

The implementation is `2a1ede3a5c8f692bd5a1a7576011d5a40acabbcf`, followed by
`dd0530798e1b603820ab028f764aa1a8bf6f93a0` for the isolated failure-control seam.
Production assigns its private reflection handle once. It is now non-readonly
so the observer can temporarily clear that product-only handle; Mono otherwise
constant-folded it despite the reflection test write. No native state table,
native getter or native writer is replaced to induce inspection failure, and
there is no production fault-injection callback.

The first capture `c10-mp3-state-cold-01` at `2a1ede3a` exited normally but failed
the intended-injection assertion: inspection remained active, so the failure
control had not exercised an indeterminate result. It is retained as a failed
control, not an observed production race or a successful failure-isolation test.
The handle is restored in a finally block in every control exit.


Focused validation passes **7 managed completion tests and 72 tooling tests**
(`artifacts/fixture-tests/4daaa6d27b4340b684f5b6ae0fadbc2d`). The managed cases
cover optional inspection failure/retry, missing state, original error identity,
nested-context restoration, and reference inspection without comparer/getter
callbacks. The final private-handle adjustment was then built and exercised in
the real GOG process.

The passing final-source capture **`c10-mp3-state-cold-02`** exercises the actual
native `Manager.Load` and product cleanup while the product reflection handle is
unavailable. The returned streaming clip stays owned with one native conversion,
33 cached frames and 13,200 history bytes; cleanup returns without throwing and
retains both private reader and decoder. Restoring the handle yields Active,
which retains ownership. The fixture then sets explicit Failed through the
existing native setter: cleanup removes the lifetime, disposes the private
resources and clears history with zero replay. A further cleanup does not revisit
the removed lifetime. The original borrowed source remains open. This is a
controlled optional-reflection failure, not random concurrent-write stress or a
claim that the original race occurred spontaneously.

That capture also passes ordinary streaming holder teardown with cached history,
actual clip destruction, and the original failed-factory error/borrowed-source
control. Natural LightSMG loading has 59 native conversions and complete
native-equal Unity samples; the subsequent streaming loads reuse its recorded
frames through the changed completion/cleanup callers. OGG/WAV playback,
gameplay save/reload, bootstrap/preloader/native shutdown and normal automatic
exit pass. The `formats-*` observer route intentionally skips the prior full
math/ABA, repeated-reset and cache-corruption controls, whose code is unchanged.

The additional separate-process capture **`c10-mp3-state-warm-02`** at the same
source fails its frame-reuse assertion. The unchanged native cache gate observes
incoming Win32 `LastError` 487 instead of recorded 0 (`cache-math` reason 4),
refuses the first cache operation, and completes 59 native conversions with zero
cached frames, zero replay and no decoder fault. It exits normally with code 0.
This is not a passing separate-process reuse result. No native gate was relaxed,
no global arithmetic/error state was changed to obtain a hit, and this sample was
not retried until a favorable value appeared. The successful cached streaming
and lifecycle evidence above is the focused result for this correction; prior
separate-process arithmetic/reuse results retain their original source identity.

Final tested/package source is **`dd0530798e1b603820ab028f764aa1a8bf6f93a0`**.
Matching immutable core, observer and bootstrap receipts and package directories
use that source under `artifacts/fixture-package`,
`artifacts/fixture-menu-observer` and `artifacts/fixture-audio-bootstrap`.

| Final payload | SHA-256 |
| --- | --- |
| `WakeUp.dll` | `98b04e934fe097e8ea51aa976662c93220805cd755aba426200c3d0b7e6c413d` |
| `FixtureMenuObserver.dll` | `705691753244315de5960741ff37dce2a2eb8ded5bd3e02620aede49fd08791e` |
| `WakeUp.AudioBootstrap.dll` | `ecb06727f8fe778ab96fbf4f7d2b9ae542de88e9de69dcc8b9bb263a4a126004` |
| `mono-profiler-wakeupentry.dll` | `23135af6119269ae7040d4cf65fc0452ac1fdf2f788e44fc11f1f27926d2731f` |

Evidence is under
`artifacts/ecosystem-next-20260910/c10/mp3-load-state-correction/`, including
`run-summary.json`, `package-audit.json`, `operations.json`, `restoration.json`,
focused tests and operation logs. The audit verifies six immutable receipts,
24 package payloads and 46 captured observer files across three captures. Only
`cold-02` passes its complete prepared-audio probe; both failed assertions remain
explicitly retained. All nine fixture transactions were reversed, all seven
baseline hashes and ten absences match, and no fixture game remains running.
A final fresh OS query finds no RimWorld-named processes; the normal game and
Apollo helper observed earlier were never stopped or modified by this task.

The handoff is STOPPED for independent correction review, with the single
checkout/build/fixture ownership returned to the steward and no child or
successor running. No performance work or acceptance claim is added. Broader C10
assets, compatibility, production integration and platform promotion remain open.


## Steward review: load-state fix passed; remove unrelated error-state refusal — 12 September UTC

The optional state-reading correction passes bounded independent functional
review at stopped `95ce30ad41e5f0cb6df8af90b58dd43593f31d55`, tested
`dd0530798e1b603820ab028f764aa1a8bf6f93a0`. Unknown state preserves the original
load result and live ownership, cleanup retries without throwing, and an eventual
known failure releases private resources once. The parent checked actual code and
captured controls; the independent managed reviewer found no blocking defect.
Seven focused managed and 72 tooling checks support the recorded real GOG control.

The parent verified 38 artifact hashes, six immutable receipts, 24 package files,
46 captured observer files, three run/probe pairs, seven restored baseline hashes
and ten absences. The final source-to-handoff difference is documentation only.
The two failed assertions above remain failures. In particular, `warm-02` did not
reuse frames; successful menu/gameplay/exit does not establish replacement.
The parent's later process query found normal Steam PID 38292 and Apollo helper
PID 40752, with no fixture process. These normal processes remain untouched; this
is a later snapshot than the orchestrator's no-process observation.

The separate warm failure exposes a concrete usability gap: cache reuse depends
on a caller's unrelated Windows error value (`LastError`, a per-thread value left
by Windows calls). Independent native inspection found no incoming-value read on
the pinned conversion path: the provider imports no `GetLastError`, its inspected
thread-environment access is outside decoding, and the inspected arithmetic
wrappers' fiber bookkeeping does not select arithmetic using this error value.
The recording guard also rejects conversions that change it, although that check
alone would not prove non-consumption. This is finite source evidence for the
qualified provider and wrappers, not a claim about arbitrary codecs.

The same C10 owner should remove only the historical incoming-value equality gate
in `TryCached`, retaining caller save/restore, historical replay restoration and
all arithmetic/provider/stream/error checks. Before accepting that relaxation,
compare original native decoding with incoming values 0 and 487, consume records
made under one while the other is present, and require full equal output,
preserved caller error value, actual cached consumption and correct subsequent
same-decoder native catch-up. A focused native control plus the affected real GOG
reuse path is sufficient; do not repeat the whole arithmetic qualification or
build new identity machinery. Failure must drive correction, not favorable reruns.

Evidence: `artifacts/ecosystem-next-20260910/c10/mp3-load-state-correction/steward-review.json`.
Only this load-state increment passes; full MP3/C10, broader textures/graphics/
icons, loading/patch compatibility, production integration and actual performance
remain open. The same owner continues from the new exact steward review commit.
Standing isolated GOG correctness permission applies; performance remains paused.

## LastError correction: focused functional results — 12 September UTC

Cached MP3 decoding now preserves the caller's Windows error value without
requiring it to match the value recorded by an earlier process. The unrelated
value previously prevented real cache reuse. This correction removes only that
incoming-value equality refusal in `TryCached`; it retains the finite provider
qualification described in the steward review above. Context format, recording
checks, arithmetic controls and sticky flags, stream identity and epoch checks,
native error guards, and historical replay save/set/restore are unchanged. The
exported cached-call boundary still saves and restores its own caller's value.
Diagnostic reason 4 remains reserved, preserving the receipt layout.

The owner continued from reviewed **`67b122f2be9f4ec5ccecaa0baa04a97a788d8231`**.
Product and focused observer controls are committed and tested at
**`6883fb3a8a6100869dadf2cd5ca0cf238ae2da49`**. Core, observer and native bootstrap
builds succeeded with matching immutable source identities. The native child
edited only its assigned C++ source/header, handed back before builds and ran no
fixture operations. This is a STOPPED submission for independent review, not
self-acceptance of this increment or completion of C10.

### Direct error-value controls

`C10Mp3LastErrorProbe` uses disposable, same-thread native test scopes with error
values 0 and 487. The scopes inject immediately around original conversion or
the real cached-call export, check the returned value, and restore their own
outer caller state in native `finally` blocks. Historical conversion continues
to use the existing production replay scope. No global error normalization or
arithmetic-setting change is used. The scopes are inactive during natural game
loading and are ended in the observer's `finally` block.

Both captured processes pass all eight complete-read controls:

| Operation | Incoming error value | Result |
| --- | --- | --- |
| Original decoding, no attached cache session | 0 and 487 | 59 native conversions each; identical complete output |
| Record under 0, consume under 487 | 487 | 1 native conversion, 58 cached frames, zero replay |
| Record under 487, consume under 0 | 0 | 1 native conversion, 58 cached frames, zero replay |
| Resume native decoding after either crossed-value cache read | 487 and 0 | One skipped frame replayed into the same retained decoder; complete output remains equal |

Each resume control starts with one cached frame and 400 bytes of retained
compressed history. Settlement replays that frame, clears history and retains
the exact decoder identity. The remaining read finishes normally, with 59 total
native conversions including that replay, one cached frame, and no decoder
fault. This exercises native continuation after skipped work, not just successful
cache playback. All control paths preserve borrowed input ownership.

Every row hashes the complete sequence of returned read lengths, positions and
decoded audio bytes, including end-of-file. All eight rows in each process equal
`6AAE50DAA65B42074A6BC6375A2E74EC0F4C22BF90A2E8F9E122A1F4D686BE75`.
Native receipts show actual conversion/cache/replay calls, zero error-value
mismatches, every tested boundary preserved, and successful outer restoration.
The full warm receipts contain 58 actual cache hits; each continuation receipt
contains one actual cache hit and one historical replay.

### Separate-process game result and restoration

Both **`c10-mp3-error-cold-01`** and **`c10-mp3-error-warm-01`** pass their complete
prepared-audio and automatic functional checks. Cold natural LightSMG loading
records 59 native conversions. The second process uses that captured cache and
loads the real sound with **1 native conversion, 58 cached frames, zero replay,
no refusal and no decoder fault**. All 67,968 Unity audio samples match original
decoding; their hash is
`DCDFECC29F34086FFCE31BA4F859FC62B85827D38655D0199B9115FB9763DB56`.
The natural reuse result is obtained before the explicit error-value controls.
This resolves the specific separate-process refusal recorded in the preceding
increment; its earlier failed capture remains a failure at its original source.

Existing lifetime/unknown-state cleanup, streaming teardown, failed-factory,
native seek/error, OGG/WAV, gameplay save/reload, bootstrap/preloader and native
shutdown checks also pass. Both processes exit normally with code 0. The
`formats-*` route skips the prior full arithmetic/ABA, repeated-reset and cache
corruption suite; those unchanged controls and prior managed checks were not
repeated. This evidence is functional only. No performance tests, microbenchmarks
or speedup claims are added, and full C10 scope and release requirements remain
open.

| Tested payload | SHA-256 |
| --- | --- |
| `WakeUp.dll` | `68e00378f2b9d8324f5f6d69b8da53afefbefe047882311c25b709b8cbda0fe0` |
| `FixtureMenuObserver.dll` | `7e1d8524ad87814dbb3477677232e26a5d2d6bd05bf30062a771e55584287ef7` |
| `WakeUp.AudioBootstrap.dll` | `514c73a703182dd709d84adf749f5396725e161173ef9eba38033622aece7c45` |
| `mono-profiler-wakeupentry.dll` | `84a175a6ed073ad5dea6020dc30c33277cd58d615bf151222bd3e45ad713047c` |

Private evidence is under
`artifacts/ecosystem-next-20260910/c10/mp3-last-error-correction/`: operation logs,
`run-summary.json`, `package-audit.json`, `operations.json`, `restoration.json`
and handoff inventory. The audit verifies three immutable package receipts,
12 package payloads and 32 captured observer files across the two passing runs.
All five fixture transactions were reversed in order; all seven baseline hashes
and ten absences match the original GOG generation. The final fresh OS query
finds no RimWorld-named processes. Normal game processes and the Apollo helper
seen earlier were never stopped or modified by this task. Checkout/build/fixture
ownership is returned to the steward at the STOPPED handoff, with no active child
or successor. Independent correction review and broader acceptance remain pending.


## Steward review: error-state correction passed; remaining compatibility next — 12 September UTC

The separate-process MP3 reuse failure is corrected. Stopped
`a30452eec1b717846b042f23f489d016570557fc`, tested matching packages
`6883fb3a8a6100869dadf2cd5ca0cf238ae2da49`, passes bounded independent functional
review. The production admission change removes only the unrelated incoming
Windows error-value equality gate. Caller error preservation, historical replay,
provider identity and arithmetic safeguards remain. The explicit test scope sets
its sentinel before the real production export captures the caller, so its
preservation check exercises the production path rather than hiding a mismatch.

Both functional captures pass prepared audio, gameplay, bootstrap/preloader,
shutdown and normal automatic exit. Each has eight focused controls: original
native decoding under 0 and 487 produces identical complete sample/read/position
traces; both cross-sentinel directions use one native conversion and 58 cached
frames; later catch-up replays one skipped frame in the same decoder, clears
400 history bytes and completes native-equal output. Every boundary preserves
its incoming value and the test scope restores the outer value.
The natural warm LightSMG load occurs before sentinel injection and independently
shows one native plus 58 cached conversions, zero replay/refusal/fault and all
67,968 Unity samples equal to native. Existing earlier failed captures remain
failed; this new source supplies the passing correction.

The parent verified 24 artifacts, three immutable receipts, 12 package files,
32 captured observer files, both run/probe pairs, seven baseline hashes and ten
absences. The handoff difference is documentation only. Five fixture transactions
were restored. A fresh parent OS query finds no RimWorld-named process. No normal
process was closed or modified. Independent native review found no scoped blocker.
Evidence: `artifacts/ecosystem-next-20260910/c10/mp3-last-error-correction/steward-review.json`.

Next, the same C10 owner completes the remaining practical loading/patch-library
compatibility question: an actual public patch operation that succeeds without
Wake-Up must still install and work with deferred readers settled before mutation.
The earlier third-copy generic-argument failure also occurred in its unguarded
control; it proves neither a Wake-Up regression nor successful patch integration.
Use a baseline-supported operation and faithful dependency binding, preserving
ordinary native errors for unsupported cases. Do not turn this into a generic
loader test framework. Reuse adequate lifetime, arithmetic and codec evidence.

This bounded increment is functionally reviewed, not full C10 acceptance. Broad
texture/graphic/icon deferral, remaining compatibility, production integration and
actual measured improvements remain required. The steward is separately revisiting
the texture architecture; audio cannot substitute for those missing capabilities.
No performance window, C11 successor or production backend adoption is granted.
