# C10 MP3 decoded reuse: retained-decoder design checkpoint

## Result and remaining prerequisite

MP3 decoded-data reuse remains **open and unimplemented**. This bounded pass
identified a useful frame-conversion cache design that preserves the existing
reader and selected decoder. It did not establish the identity needed to serve
persistent decoded bytes safely. No PCM was guessed, no index-only substitute was
introduced, and the rejected conversion-buffer allocation route was not repeated.

Action `wake-up-c10-mp3-decoded-reuse-b8329ef6-20260912` began from clean
`b8329ef6cc5222b72bdc8ae2d925fe88cba0add6`, under the same C10 owner's exclusive
checkout ownership. Product source, builds, packages and fixture content were not
changed. Prior reviewed settlement, lifetime and OGG/WAV/readiness behavior remain
intact. This report is a prerequisite checkpoint, not functional acceptance.

The one missing dependency is **a trustworthy connection from the retained native
conversion stream to its actual executing decoder code and effective configuration**.
Keeping the stream solves which selected instance this reader owns. It does not
identify the code/configuration that produced bytes stored by a previous process.

## What the supported interfaces establish

Windows Audio Compression Manager (ACM) selects and opens the native decoder.
Its [acmDriverID API](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriverid)
accepts the actual retained stream handle (HACMSTREAM) and returns its driver ID.
The descriptor obtained from that ID identifies capabilities and advertised
version; it is not a mapped-code hash or complete configuration identity.

The proposed conservative candidate-set fingerprint also remains unproved:

- Enumeration would need disabled and local entries, order and priority. Registry
  entries alone omit dynamically registered driver functions.
- On Win32, [function-driver registration](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriveradd)
  can add a function in an EXE or DLL, and even GLOBAL additions are private to the
  application. Absence of LOCAL flags cannot prove that registry files cover all
  candidates. A matching descriptor list does not connect each entry to its code.
- Priority notifications omit nonglobal changes; deferred notifications are not
  an atomic selection/configuration snapshot. Retention avoids future selection
  for this reader, but does not close a candidate-based persistent identity.
- Hashing current files at loaded module paths does not by itself establish the
  retained mapped image, its dependencies or output-determining configuration.

[acmDriverMessage](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdrivermessage)
allows driver-specific user messages plus About/configuration exceptions. The
reviewed API and matching SDK headers expose no generic selected-module or complete
configuration-readback query. No undocumented message was sent. GetDriverModuleHandle
requires a genuine Driver Manager handle (HDRVR), not HACMDRIVERID or HACMSTREAM;
opening a second driver does not identify the retained stream's instance.

The current registry names `l3codeca.acm` and `l3codecp.acm` among its candidates.
The candidate `C:/Windows/System32/l3codeca.acm` is 118784 bytes, version
`1, 9, 0, 0401`, SHA256
`5d7f5961411fe64b2bc48297e7c424aaf19272aa814287b0a4ff1c469b8bd3df`.
It exports plain `DriverProc` at RVA `0x3a10`, ordinal 1, without a forwarder.
That confirms an entry address in the candidate file, not selection.
Its imports do not directly read the registry; that observation does not prove
selected-image linkage, transitive behavior or a closed configuration contract.
No live selected-driver query or codec execution occurred in this pass.

## New native dispatch evidence

Read-only inspection of the current `C:/Windows/System32/msacm32.dll`, SHA256
`f77312070a57b67e6e9ee0f4b66bbf38d08f01a40e5721f04064485298ad1263`, found
both Driver Manager and stored-function dispatch. This rules out treating a
SendDriverMessage-only observer as complete evidence.

At RVA `0x5b6b`, a stored function pointer selects the route. The alternate branch
calls SendDriverMessage at `0x5b85`; the nonzero branch at `0x5bae` supplies five
DriverProc-shaped arguments and dispatches through `0x14010` / `0x13c90` to the
PE's GuardCFDispatchFunctionPointer slot `0x156e8`. Windows control-flow protection
is dispatching the stored function; that branch does not use the imported
SendDriverMessage. These are file instructions, not an observation of which route
the actual Fraunhofer stream takes. No private running-process layout was read.

## Actual decoded-reuse implementation after identity is sound

Keep native Mp3FileReader construction unchanged, including source ownership,
header/index handling, selected AcmMp3FrameDecompressor, conversion stream open,
output format and decoded-frame buffer allocation. Count that retained eager work
explicitly. The useful interception boundary is the existing nongeneric
`AcmMp3FrameDecompressor.DecompressFrame`, with its Reset and Dispose lifecycle.

1. Associate a private recording with the actual decompressor instance. Record
   ordered resets, exact compressed frame identity/content, and the returned PCM
   bytes. PCM means the decoded sample bytes before the native float conversion.
   The key includes source identity, pinned native methods, verified provider code
   and effective configuration, output format, and the recording version.
2. Leave Mp3FileReader.Read and Position native. They retain source/index progress,
   three-frame seek preroll, double-frame trimming, buffered leftovers, output
   position and existing errors. Keying only by decoded position is insufficient.
3. A matching frame/reset sequence can supply recorded output without native frame
   conversion. Count actual skipped conversions and consumed bytes, not merely
   cache hits. Do not skip original selection/open or construct a second decoder.
4. On mismatch, missing data or relevant mutation, replay the skipped frame/reset
   sequence into that SAME selected decompressor before allowing the current native
   operation. Use the reviewed registry settlement barrier and a recursion bypass
   for original replay. Retain a real replay failure for the ordinary consumer;
   neither guess a seek state nor replace the original exception.
5. On a complete nonstreaming hit, ordinary disposal frees the selected but unused
   conversion buffers/stream without replaying the skipped decode. Replaying at
   disposal would repay the work before Loaded and defeat useful avoidance.

The existing whole-WaveStream wrapper cannot be reused unchanged: native!=null
bypasses its cache, its warm disposal does not own a parked native instance, and
its construction counters have different meaning. Prefer the bounded frame seam
rather than weakening the reviewed OGG/WAV wrapper and completion contract.

The current audio record ceiling is 2 MiB and 256 operations. A truncated frame
prefix can force fallback and repay all skipped decoding during nonstreaming
startup. Larger complete recordings therefore need ordered bounded chunks and a
final manifest in the SAME OwnedCacheStore, integrity envelope and shared audio
budget. Keep only the bounded current chunk and required compressed replay history
in memory. No new 512 MiB pool or unbounded PCM copy is proposed. Missing/corrupt
chunks use same-instance catch-up; partial records are not broad coverage.

## Two materially different native alternatives for steward decision

Neither alternative is implemented, executed, or approved by this checkpoint.
Both need the selected provider's bounded configuration/dependency contract before
serving cached data; merely observing a target address does not finish that work.
The recommendation is to review alternative A first because it can avoid private
Windows ACM structure/dispatch coupling. This is a design recommendation, not a
request to execute unreviewed hooks.

**A. Observe the actual provider entry for a narrowly supported native decoder.**
An exact-binary-gated adapter observes the provider's DriverProc entry using its
Driver Manager/ACM message ABI. First conversions remain native. A warm reader
performs its first conversion normally, so the adapter observes the code actually
executing for that retained stream before any cached PCM is served. Associate only
the relevant owned stream, not unrelated codecs or applications. Establish the
actual mapped module, dependencies and output-determining open/configuration
inputs; retain the real decoder. Subsequent matching frame operations can use the
managed cache path above. Unsupported providers remain ordinary per-asset native
loads. This requires separately approved raw-native entry interception and exact
provider-lifetime review. Export availability, calls through that entry, complete
configuration and invalidation still require qualification. A first-frame match
alone is not a binary/configuration identity proof.

**B. Observe both selected ACM dispatch routes for the owned stream.**
An exact-msacm32-binary-gated adapter observes both the real HDRVR route and stored
function target during original stream open/first conversion, preserving every
native call and result. Correlate the successful selected dispatch to the returned
owned HACMSTREAM; do not infer it from module loads or failed selection candidates.
Use the genuine HDRVR or actual function target to establish its mapped module,
then the same bounded dependency/configuration contract. Only after that admission
may the managed frame cache bypass subsequent conversions. This covers more driver
forms but introduces Windows-implementation coupling and needs independent ABI,
correlation, lifetime and failure review. It requires new native instrumentation;
the existing Mono entry adapter does not supply or authorize it.

A documented provider-specific identity interface would be preferable if one is
available for the real selected decoder; none was established here. Re-decoding
all cached output with the current native codec validates bytes but repeats the
work, so it is not an implementation alternative for decode avoidance. Registry
expansion, test-vector equality or hashing every system file does not close this
checkpoint's dependency.

## Focused acceptance once the prerequisite is resolved

Use ordinary native MP3 content, beginning with the existing authored
`Things/LightSMG.mp3` and `VWE_Shot_LightSMG`, and unrelated natural MP3 content where
available. Keep the native sound/music consumer and public clip readiness. Compare
full decoded output, metadata, small partial reads/leftovers, native seeks/preroll,
EOF, decode failure and disposal. A persistent cold/warm pair must prove consumed
cached PCM and fewer actual native frame conversions across game processes while
retaining the same selected instance within each reader. If a first conversion is
retained for provenance, count it explicitly. Same-process evidence is only an
intermediate result, not persistent acceptance.

Exercise identity/configuration mismatch through owned fixture controls that do
not alter global codec registration, priority or security. Corrupt/incomplete
records and relevant mutation must fall back to that same decoder with native
bytes and errors. Reuse adequate previous OGG/WAV, readiness and lifetime evidence;
run only the regressions needed by the changed seam. No speed, overhead or codec-cost
measurements are authorized during active PC use.

## Stopped scope and evidence

This pass used read-only source/API/SDK/PE/config inspection. No runtime product
edit, build, codec execution, test, fixture deployment/launch/transaction, native
interception, global codec change, UI/Deck/Steam action or performance experiment
occurred. The pre-assignment seven hashes and ten absences were verified. The
ignored evidence is `artifacts/ecosystem-next-20260910/c10/mp3-decoded-reuse/`.
The capability remains open for the steward's material native-integration decision;
this is neither self-acceptance nor a successor dispatch.

## Steward review: implement a finite provider route â€” 12 September UTC

Stopped `998d5aa4` is a useful design checkpoint, not delivered MP3 decoded reuse.
The steward verified its six evidence-file hashes, seven unchanged fixture hashes,
ten absences and the actual NAudio, ACM and candidate-provider binary identities.
The native reader source confirms that keeping frame conversion as the seam leaves
seek preroll, leftover samples and output positioning with the existing reader.

The next action is **alternative A as a bounded isolated GOG experiment followed
by frame-cache implementation when its admission contract is established**. This
is steward architecture approval under the owner's standing native-experiment and
GOG testing permission. It does not approve production adoption of a new backend.
Alternative B's private Windows dispatch instrumentation is not selected.

The identity requirement is finite: identify the actual selected supported
provider for this retained stream, its relevant executable code and inputs that
can affect decoded output. Do not require a universal inventory of every codec
or proof of every transitive Windows dependency. Inspect the supported provider's
actual conversion/configuration path and bind relevant dependencies; unsupported
or changed providers keep ordinary native loading. A first-frame output match is
useful correctness evidence but is not the provider identity by itself. Narrow
initial provider support must remain visible as a coverage limit, not a supplier
replacement claim.

Correlate the provider's documented stream messages with the actual owned stream
and buffers; inspect the SDK's ACMDRVSTREAMINSTANCE contract rather than guessing
private ACM layouts or casting driver handles. Preserve original calls/results,
selection, stream lifetime and configuration. Install native observation outside
active conversion and loader callbacks; do not execute managed work under codec
or loader locks. Unrelated streams pass through unchanged. Validate module and
trampoline lifetime before enabling this fixture-only route.

Prefer an already available maintained native detour only if its exact shipped
API and lifetime fit. The merged Harmony native-detour types are internal, so
availability alone is not qualification. A pinned, statically bundled MinHook
1.3.4 prototype is also authorized; preserve its source identity and all notices,
with no manual installation or system modification. Its [public header](https://raw.githubusercontent.com/TsudaKageyu/minhook/v1.3.4/include/MinHook.h)
provides the original-call trampoline contract; its [license](https://raw.githubusercontent.com/TsudaKageyu/minhook/v1.3.4/LICENSE.txt)
requires notices with redistribution. Do not hand-write instruction patching or
expand into private Windows dispatch if this bounded route fails.

Implement real ordered frame/reset recording and replay through the shared owned
cache, preserving native validation, partial consumption failures, same-instance
catch-up and original consumer exceptions. Catch up only operations skipped since
the last native conversion; never replay the retained first conversion twice. A complete nonstreaming hit must close
the unused retained decoder without decoding all skipped frames at disposal.
Bound chunks and compressed replay history within the existing shared budget;
prove consumed cached samples and avoided conversions across process starts.
Preserve reviewed OGG/WAV, getter-free completion, settlement and lifetime behavior.

A fixture-only frozen-provider recording prototype may proceed while the finite
identity adapter is completed, with general admission closed. It is intermediate
evidence, not production provider qualification. Use a simpler supported binding
if it establishes the same actual stream/code contract without interception.

If the finite provider contract fails, return the concrete failing linkage/input
and a materially different next design. Do not broaden identity research without
a specific product question or close the parent capability at that checkpoint.
MP3, textures/graphics/icons, compatibility and production integration remain open;
full C10 acceptance and performance qualification have not occurred. Performance
runs and microbenchmarks remain prohibited during active PC use.

## Implementation checkpoint - 12 September UTC

The approved alternative-A experiment now has a working, explicitly frozen-provider
frame-cache prototype at tested source `3d4f2b01db873eeceb66a9799740682d941c6dbe`.
The [implementation/evidence record](c10-mp3-frame-cache-prototype.md) documents
actual retained-stream/provider binding, native math and loader-code inputs,
persistent consumed PCM, native sample equality, and bounded same-instance
seek fallback. Its warm process converts one native frame and uses 58 cached
frames for the ordinary LightSMG clip, with no disposal catch-up.

The finite remaining general-admission problem is a native arithmetic setting
that can change after frames have been skipped: UCRT's global FMA choice affects
per-frame `pow`, and post-change replay is not established to preserve the old
state. Thread-local floating-point controls also need scoped historical replay.
General admission remains closed; a further native pre-change boundary is a
material next design requiring review, not permission inferred from the working
frozen prototype. No performance, full MP3/C10 acceptance, or successor follows.
