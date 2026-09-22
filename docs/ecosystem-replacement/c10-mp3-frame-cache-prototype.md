# C10 MP3 decoded-frame prototype - 12 September UTC

The isolated prototype now reuses decoded MP3 samples in the game's ordinary
sound clip. A new game process performed **1 native conversion instead of 59**
for the authored LightSMG sound, supplied the remaining 58 frames from the
persistent cache, and produced exactly the same samples as native decoding.
This is avoided-work evidence, not a speed measurement.

**General admission remains closed.** The explicit `frames-prepare` and
`frames-warm` fixture modes assume that the native math settings remain fixed
while frames are skipped. A concrete setting-change problem remains before this
can become the general prepared-audio path. This record is an intermediate
prototype handback, not full MP3, C10, compatibility, or release acceptance.

## Source and operating scope

The assignment began from clean `bc77d16f121cf66d29b943fef0c8347732d9440f`.
The final tested implementation and all three matching GOG packages are
`3d4f2b01db873eeceb66a9799740682d941c6dbe`. Earlier implementation commits and
failed admission captures are retained. Main was not merged or published.

Only the existing isolated GOG fixture was operated, with functional purpose,
muted 1280x720 preferences, minimized nonactivating launches, the independent
menu observer, and normal automatic exit. No performance test or microbenchmark,
normal-game/data change, desktop automation, security change, Steam promotion,
Deck/Linux access, push, or publication occurred.

## What the implementation preserves

The original `Mp3FileReader`, `AcmMp3FrameDecompressor`, and `AcmStream` remain
in place. Original stream opening and automatic codec selection occur normally.
The first frame conversion executes once. Only later matching `DecompressFrame`
and `Reset` operations can be served from an ordered cache transcript. Native
`Read` and `Position` continue to own partial reads, leftover samples, seek preroll,
double-frame trimming, source progress, and EOF behavior.

If an operation no longer matches, the same retained decoder processes only the
compressed operations that were skipped after its last actual conversion.
Already performed work, including the first retained conversion, is not replayed.
A complete hit disposes the original selected buffers and stream without decoding
its skipped history. Original frame/destination validation stays on the native
path when a cache operation cannot safely satisfy the call. Successful disposal
removes the strong pending-reader registration after releasing the reader lock.

PCM means the decoded sample bytes supplied to the audio consumer. The shared
`OwnedCacheStore` holds bounded PCM/frame chunks and a final manifest that lists
the complete transcript. The manifest is published only after successful native
disposal and the reader's stored byte position reaches its advertised length.
Each chunk is bounded by the existing 2 MiB payload limit, and compressed catch-up
history is bounded to 2 MiB. There is no separate large MP3 cache budget. Missing,
incomplete, corrupt, or quota-refused data cannot supply an unverified chunk.
The OGG/WAV wrapper was not changed. Existing getter-free MP3 completion and
nested-load context masking remain in use.

## Finite selected-provider observation

A statically bundled MinHook 1.3.4 detour observes only the exported `DriverProc`
entry of the supported codec, with original calls/results preserved. Upstream
commit is `c3fcafdc10146beb5919319d0683e44e3c30d537`; source hashes and required
license notices are retained in source and the bootstrap package.

The native adapter links a successful original conversion to the actual owned
`HACMSTREAM` through documented `ACMDRVSTREAMINSTANCE.has`, exact source/destination
buffers and counts, format bytes, open/convert flags, and the retained stream
instance. It does not cast unrelated driver handles or instrument private ACM
dispatch. Native callbacks do not call managed code. Installation occurs before
reader construction; provider/module/trampoline storage remains pinned for the
process lifetime. Stop closes observation and joins active observation work.

The pinned provider is System32 `l3codeca.acm`, SHA256
`5d7f5961411fe64b2bc48297e7c424aaf19272aa814287b0a4ff1c469b8bd3df`.
Its actually resolved `_o_pow` and `_o_sqrt` imports must bind the expected RVAs
in the same mapped System32 `ucrtbase.dll`, SHA256
`5c52e3a303baaac0e0af8bd9b96134993da34bc9d834a31ef37e1d2cdc7fe192`.
The exact format bytes, arithmetic controls, and 13 selected UCRT function RVAs
enter the persistent identity. Current checks recheck retained stream identity,
configuration generation, arithmetic controls, imports, and accepted loader jumps.

Windows legitimately changes specifically declared jump operands while loading
these exact images. The adapter checks the declared finite alternatives and
requires all other executable bytes to match. For the control-flow dispatcher,
the observed OS-owned image-backed executable target is exactly `48 FF E0`
(`jmp rax`); the pinned local fallback is `FF E0`, the same complete instruction.
Actual-fixture allocation ownership, instruction bytes, slot pointer and jump
bytes are checked, rather than ignoring the entire thunk section. The first
three captures preserve why raw disk/memory equality and then direct target
address equality were insufficient. No arbitrary external target is accepted.

The ABI also exports `wakeup_acm_overrides(values, 16)` for the complete selected
RVA vector. Native stats 13-14 carry precise refusal code/detail, 15-17 carry the
observed dispatcher bytes/targets, and 18 counts accepted loader changes. These
supplement the earlier ABI header's base receipt/stat descriptions.

## Functional evidence

Both successful runs use the unchanged authored file
`tro.vwe.overhaul / Things/LightSMG.mp3` and `VWE_Shot_LightSMG`.

| Captured process | Native frame conversions | Cached frames | Cached PCM bytes | Catch-up frames on ordinary load |
| --- | ---: | ---: | ---: | ---: |
| `c10-mp3-frames-cold-04` | 59 | 0 | 0 | 0 |
| `c10-mp3-frames-warm-04` | 1 | 58 | 135936 | 0 |

Both report normal exit code zero, `automaticTestPassed=true`,
`audioPreparedTestPassed=true`, and successful native-entry shutdown. The cold
process publishes the complete transcript. The warm process restores that
captured shared audio cache through `fixture.py --audio-cache-from`, consumes
cached PCM in the ordinary background load, and disposes the retained native
decoder without catch-up. The source, package, content/order, profile selection,
and observer identities match between these processes.

All 67,968 mono 48 kHz samples in the actual Unity clip equal a separate native
reader through the normal sample conversion. Both process captures have the same
full float-sample SHA256:
`DCDFECC29F34086FFCE31BA4F859FC62B85827D38655D0199B9115FB9763DB56`.
The authored sound resolves to that clip, plays through its ordinary manager,
and remains correct through the gameplay save/reload check. Existing natural
OGG/WAV checks also pass in these captures; this does not repeat or broaden the
previous full readiness/lifetime qualification.

The separate partial-read/seek/EOF control matches native metadata and all
recorded output bytes. It serves one cached frame (4,608 bytes), then a changed
seek sequence causes exactly that one skipped frame to be replayed in the same
decoder. It performs five native-reader seeks/resets, reaches EOF, disposes the
reader, and preserves its borrowed input stream. This is a fallback correctness
check; it does not claim avoided work for the entire changed seek sequence.

Final focused validation passes **128 managed tests and 72 tooling tests**.
The seven new chunk/manifest tests cover bounded serialization, chunk crossing,
owned byte copies, corruption/truncation, missing chunks, quota failure, abort,
and final-manifest validation. These tests do not substitute for unperformed
live cache-corruption or arithmetic-setting-change controls.

## Remaining concrete contract and next design

The codec's `DRV_CONFIGURE`, private user messages, and `ACMDM_STREAM_RESET`
do not change decoder arithmetic. Actual reset uses the next conversion's START
flag. However, decoded frames call UCRT `pow`, which selects arithmetic using the
mutable global value exposed by `_get_FMA3_enable` / `_set_FMA3_enable`.
The provider also uses the calling thread's floating-point controls (MXCSR).
This is a specific per-frame dependency, not a demand to inventory all Windows
components. Exact call paths are in the ignored `provider-path-review.md`.

If these controls change after cached frames were returned, replaying those old
frames under the new controls is not established to reconstruct the native state
that would already exist. Merely rejecting the next cache operation or adding
its current settings to a key does not solve that history problem. The prototype
therefore remains explicitly frozen: a changed contract with skipped history
raises a qualification failure instead of silently replaying under changed
controls or reporting the failure as an original codec exception. This behavior
is not approved general compatibility.

A materially different next integration must preserve the old arithmetic context
for catch-up. Thread-local controls can potentially be restored only around replay
and then restored to the caller; the global FMA setting must not be changed to
replay history. A possible next experiment is a synchronous boundary before the
actual FMA setter that settles pending streams while the old value still applies.
That is an additional native entry beyond the approved DriverProc observer and
requires architecture/lifetime review, including a design that never calls managed
code under loader/codec locks. It is **not implemented or newly authorized here**.
Do not claim that the existing post-change `current()` check is that boundary.

Before general acceptance, also finish controlled provider/config mismatch,
live corrupt/incomplete-cache and fallback-error controls, cached reset sequences,
relevant-mutation settlement, and unrelated natural MP3 coverage. Broader MP3,
texture/graphics/icon, compatibility, and production integration obligations
remain open. Performance remains pending an explicit unattended window.

## Restoration and evidence ownership

All **17 fixture transactions** were rolled back latest-first. The pre-assignment
seven file hashes and ten expected absences match, and the fixture process guard
passes after restoration. No fixture process remains running. The normal game
and other applications were not closed or modified.

Ignored evidence is under
`artifacts/ecosystem-next-20260910/c10/mp3-frame-cache/`: `run-summary.json`,
`package-audit.json`, `restoration.json`, `operations.json`, `tests-final.log`,
provider/loader analysis, captured operation logs, and the final evidence manifest.
The package audit verifies 12 immutable receipts and 48 payload files across the
four sources actually used by the five captured processes. Failed admission runs
are not counted as frame reuse. The final documentation handoff is STOPPED,
with child work stopped, ownership returned to the steward, and no successor.

## Steward review: retain prototype, correct lifecycle and arithmetic history â€” 12 September UTC

Stopped `1b596a53`, tested `3d4f2b01`, supplies a working frozen-provider prototype.
The steward verified 87 artifact hashes, 12 immutable package receipts and their
48 payload files, 32 observer capture files, both process results, and restoration
of seven baseline hashes and ten absences. Both captures show native-equal full
sample output and ordinary playback/reload. Cold uses 59 native frame conversions;
warm uses one native conversion, 58 cached frames and 135936 cached bytes, without
catch-up on disposal. The changed-seek control catches up one skipped frame in the
same decoder. These are bounded functional facts, not general MP3 acceptance or
measured performance. Source after the tested revision changes documentation only.

Independent review found two corrections for the same C10 owner:

- Streaming MP3 sessions register strongly in `audioPending` but bypass the source
  and clip lifetime cleanup used by OGG/WAV. Native streaming holder teardown does
  not necessarily dispose the decoder. Associate MP3 sessions with the actual
  source/clip lifecycle, release their private resources without disrupting native
  disposal order, and enforce the shared reader admission bound. Demonstrate
  streaming teardown with pending cached history and no retained registration.
- Cold recordings check provider controls only at the first frame. Later native
  output can be recorded under changed arithmetic controls while the manifest
  still names the first frame's identity. Validate or record each operation's
  actual arithmetic context, including changes during conversion. Repairing warm
  catch-up alone does not fix mixed-context recordings.

The next preferred experiment preserves historical arithmetic **inside owned
replay**, without changing the process-wide FMA setting or draining managed readers
from its setter. Native review found that this pinned UCRT's `_o_pow` at RVA
`0x79BF0` passes `pow` at `0x766E0` and the two arguments through wrapper `0x5E910`.
That wrapper calls its existing setup, selected math function and cleanup. A
provider-scoped replay shim may use that same wrapper with the actually recorded
implementation (`0x5E990` or `0x9DD10`), if the exact native ABI, arguments, error
handling, arithmetic state and lifetime are qualified. Ordinary calls retain
original dispatch. The public FMA getter is insufficient to identify the branch:
it tests nonzero, while the actual selector tests `(flags & 3) == 3`.

The steward authorizes this **bounded private-UCRT wrapper/branch experiment**
under the standing isolated GOG native-testing permission. This is a new explicit
architecture decision, not production backend adoption. Bind the exact executing
code and actual branch, not guessed addresses. Preserve the caller's complete thread
floating-point environment on every exit, with no managed bookkeeping under
temporary historical controls; isolate replay state to the owned native
conversion, and demonstrate that unrelated calls retain native behavior. Establish
per-operation provenance and a concrete policy for concurrent changes; a current
boolean check alone does not qualify cached output or historical catch-up. If the
exact wrapper route cannot preserve its native contract, return the concrete
failure and alternatives. Controlled changes through the existing arithmetic APIs inside the disposable
GOG test process are authorized for correctness comparisons, with restoration
after each control. Global setter interception, global arithmetic writes for
replay and private ACM dispatch are not authorized by this decision. No normal
application or system codec configuration may be changed.

Continue toward real provider admission after the correction works, including
live missing/corrupt/incomplete records, cached reset sequences, original errors,
relevant mutation and additional MP3 content. Reuse adequate existing audio
validation. Keep the retained decoder and useful full-hit disposal behavior.
Performance remains prohibited, and full MP3/C10 acceptance remains open.

## Correction implementation returned for review — 12 September UTC

The same C10 owner continued from `fe151df` and implemented shared MP3 reader
admission/cleanup and per-operation arithmetic history. The
[correction record](c10-mp3-math-lifetime.md) identifies exact sources, natural
and controlled GOG evidence, retained failures, the observed equivalent Windows
loader-page correction, and restoration. It supersedes the frozen-math limitation
for the specifically qualified implementation; the historical prototype results
above remain unchanged. Independent review, broader compatibility, production
integration and full C10 acceptance remain separate.
