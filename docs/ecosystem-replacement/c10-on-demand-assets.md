# C10 on-demand assets: construction-boundary decision

## Serialized provider construction and functional result — 12 September UTC

The [engine-serialized provider check](c10-serialized-texture-provider.md) built a
faithful C05 texture record into an AssetBundle without a Unity Editor install.
The exact GOG player returned matching native descriptors and all nine GPU mips,
with retained-reference and already-ready worker behavior intact. Both synchronous
and asynchronous asset loading consumed the full payload before public texture
exposure. This sample establishes construction/readiness, not the useful remaining
deferral required by 5b. The candidate stops without a general bundle pipeline or
production integration; full C10 and the existing audio/graphics/icons obligations
remain open. The report records the exact consumer-contract decision and limits.

## Exact-player design follow-up - 12 September UTC

The [bounded design follow-up](c10-texture-design.md) corrects the earlier binary
choice, inspects the exact player's matching symbols and actual upload/command
functions, and reconciles the generated-graphics/icon/atlas ownership route.
The custom texture update has a concrete base-subresource limitation and depends
on engine graphics-producer ownership; no complete worker-safe implementation was
established. No new texture behavior is claimed. The report names the remaining
integration dependency and recommends independent audio product work while the
steward resolves it. Full 5b and all retained audio requirements stay open.

## Steward texture checkpoint review and continued investigation — 12 September UTC

Stopped handoff `2af729b4` contains a source/API investigation, not a texture
implementation. The steward independently checked all ten cited source hashes,
seven restored fixture hashes and nine absences; the checkout is clean and no
fixture process remains. A separate read-only reviewer agrees with the CPU/GPU
boundary and identified the scope clarification below. No new build or run exists.

The borrowed CPU buffer is real; final upload and native unreadability remain
unresolved for the tested worker-demand design. The supported APIs therefore do
not justify repeating a CPU-only or separate-GPU-resource demonstration. This
finding rejects that incomplete design, not all texture deferral.

**The report's binary decision is too strong.** Approved plan section 6.1 already
allows complete public populations, eager completion before opaque callbacks and
private deferred work with known-worker preparation. That route does not itself
need a compatibility concession. It must still preserve previously working
undeclared-worker/public/reflected access and demonstrate useful broad remaining
work. Keeping all public textures eager, or repeating already-lazy graphics,
cannot by itself close 5b. Earlier holder and opaque-callback findings remain
valid; do not rerun that failed construction boundary under a new name.

The same C10 owner continues a bounded investigation of (1) an actual exact-player
finalization/submission entry and its lifetime/thread/callback contract, and
(2) any materially different private ownership boundary with genuinely eager
remaining work. Source-only investigation is already authorized. A concrete new
native integration must return for architecture review before execution/adoption;
no raw detours, private object-layout writes or global synchronization changes
are implicitly approved. Match native submission ordering and readiness: do not
invent a requirement for physical GPU completion stronger than native publication.

The next result must name an actionable implementation path or the exact unresolved
material choice. Implement a viable route within existing approved boundaries with
focused natural-family correctness evidence; do not expand another harness. A
checkpoint cannot close the parent capability. Full C10, retained audio/readiness,
production integration and performance remain open; no C11 successor follows.
GOG correctness permission continues; no performance window exists.

## Original-texture storage checkpoint - 12 September UTC

The proposed use of the original texture's CPU storage is real, but it does not
remove the outstanding graphics-submission boundary. A worker can fill borrowed
CPU memory while main waits; it still needs a supported way to finish the same
texture's GPU upload and native readability state before returning it. No such
complete route was established in this bounded review. No candidate was enabled,
and no new natural-family deferral or avoided-work count is claimed. Full C10,
including the retained audio requirements, remains open.

This assignment began at exact clean
`c0c9ddbfa595832237ca1bef99c22d69d4262773` on the preserved review branch, under
`wake-up-c10-texture-return-c0c9ddbf-20260912`. The owning team performed source/API
review only. Two independent read-only reviews covered CPU storage and graphics
submission. No build, deployment, launch, performance experiment or production
change occurred. The earlier audio implementations and C05-C09 remain unchanged.

### What the changed premise establishes

The pinned GOG `Unity-Texture2D.cs:679-691` implements generic
`GetRawTextureData<byte>()` by wrapping `GetWritableImageData(0)` in a
`NativeArray<byte>` with `Allocator.None`. This is a borrowed view of Unity's own
CPU buffer, unlike the copied `byte[]` overload at lines 309-310. It covers the
full mip layout. Main must acquire it and capture metadata; the worker receives
only that view and validated lengths, avoiding further Texture2D calls.
The view does not own or pin the allocation. Keep the object alive and prevent
upload, resize/reinitialization, image replacement and destruction until the view
is retired. The pinned player's NativeArray has stripped safety checks; successful
access would not by itself prove lifetime safety. This distinguishes the CPU route
from the old separate GPU allocation, without private-layout writes.
[Unity CPU-data contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.GetRawTextureData.html),
[Unity job examples](https://github.com/Unity-Technologies/UnityTextureAccessApiExamples).

The original object, dimensions, format and all CPU storage would still be
allocated eagerly. Only later source reading, decoding, preparation or upload
could count as deferred, if actually demonstrated. No allocation saving or speed
claim follows. C05's `NativeTextureData` and prepared entries already describe
the format/mips/readability and exact payload; another cache is unnecessary.

### Why the complete candidate still stops

CPU writes do not update GPU contents. The pinned `Apply` wrapper at lines 694-700
still enters native `ApplyImpl`. Native `GOG-ModDdsLoader.cs:53-68` constructs the
final layout, copies raw data, sets metadata and calls
`Apply(!hasAuthoredMips, makeNoLongerReadable: true)`. The inspected PNG/JPEG loader
also finalizes unreadable textures. A retained readable CPU view therefore cannot
be the final public native state: Unity's ordinary finalization uploads and frees
that buffer. Faking `isReadable` or retaining a freed pointer is not a solution.
[Unity Apply contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.Apply.html).

Using the existing resource pointer avoids external-wrapper replacement, but
changes none of the earlier D3D11 submission findings. Resource creation is a
device operation; updating an existing resource, executing a recorded command
list, and establishing GPU completion require the rendering context. Access must
be coordinated with Unity's context and DXGI presentation. A local lock or an
already-enabled multithread flag does not establish that coordination. A GPU
completion signal (fence) proves completion after valid submission; it does not
provide a valid submission path.
[Microsoft threading contract](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-render-multi-thread-intro).

The newly examined pre-map/`WriteToSubresource` variant still needs context
map/unmap; a mapped resource cannot be published for ordinary GPU use. A separate
device cannot retrofit sharing onto Unity's ordinary allocation. A queued Unity
render callback is supported execution, but does not promise progress for an
arbitrary later worker request while main is waiting. Keeping that callback
permanently waiting would change the engine's shared rendering execution and can
block other Unity operations. It is not an approved shortcut.
[WriteToSubresource](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_3/nf-d3d11_3-id3d11device3-writetosubresource),
[Map contract](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-map),
[Unity render callbacks](https://docs.unity3d.com/2022.3/Documentation/Manual/NativePluginInterface.html).

The public holder still exposes a concrete dictionary without a main-thread check
(`c08-corrections/ModContentHolder.cs:13,80-96`). Thus the existing main-calls-worker
and synchronously waits example remains relevant; reflected acquisition has the
same boundary. This is not an expanding hypothetical API suite. Current pinned
YaOpt still completes selected pending-texture routes, and FGL still queues worker
texture requests for main. Those sources do not supply the missing engine contract;
neither supplier was runtime-tested against this scenario in this assignment.

### Concrete decision and next implementation paths

The decision for the steward/owner is whether to authorize a separately reviewed,
version-pinned engine integration to obtain **both** original Texture2D
finalization and graphics execution independent of a blocked main thread, or to
explicitly change which worker-first-use contracts the feature must support.
The current public API candidate does not justify another same-resource upload
run. This is not a claim that every deeper integration is impossible.

| Path | Concrete next work and consequence |
| --- | --- |
| Preserve the full worker contract through deeper engine integration | First identify an actual engine-owned command-submission/finalization entry and its thread, lifetime and callback contract for this exact player. Review the required native integration before executing it. Stop if no usable entry is established; do not substitute raw object-layout writes or global synchronization hooks. Only then extend the existing native probe to the original object, all mips, unreadable/readable semantics and actual holder exposure. This has new version/platform maintenance and remains unapproved and unproven. |
| Explicit cooperative worker preparation | Implement private pending descriptors using C09 provider selection and C05 payloads; complete known worker dependencies on main before dispatch, and complete main-thread holder exposure before returning. This can defer real work for remaining families, but cannot promise unchanged undeclared first-worker acquisition. It needs an explicit compatibility decision and does not currently satisfy full 5b. |
| Preserve native behavior with eager public textures | Continue existing C05-C09 preparation/reuse and native public loading. It has no new C10 texture-deferral claim and leaves the requested capability open. It is the current behavior, not an accepted substitute. |

Recommendation: resolve that architecture/compatibility choice before another
texture prototype. Do not run a CPU-only demonstration merely to accumulate a
partial receipt: its result cannot remove the identified GPU/finalization
prerequisite. If a supported submission contract is established, the small
CPU-view proof becomes a useful part of that single complete experiment. Audio
first-use/provider/readiness/prewarming and broad texture/graphic/icon obligations
are retained, not moved after release.

Fresh endpoint verification matches all seven assigned hashes and nine absences.
CoreModule remains `c5c58ea254834291780a1d6c388c241443d07167b4a4b890a23c9494f626ddba`
and UnityPlayer `e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96`.
No new package/run identity exists; `c10-native-device-02` retains its historical
GPU-resource-only meaning. Private checkpoint evidence is
`artifacts/ecosystem-next-20260910/c10/texture-return/checkpoint.json`.
Normal Steam 50776 and local DeckHost 48204 were verified unchanged; no fixture
process was present. Child work is stopped and ownership returns to the steward
with the clean committed report. No full acceptance or successor follows.

## Resumed after C12/C13: earlier-entry decision — 12 September UTC

C10 resumed from clean `d8ec2ad1343923c4b3ce8a32c467d1e7f465af5f`, preserving
the reviewed C12/C13 implementation. The restored GOG generation, all seven
assigned hashes and five absences match; no RimWorld process was present.
No code, build, test or fixture operation changed in this bounded source pass.
**C10 remains an implementation assignment with no functional acceptance.**

The changed audio premise was to account for code actually executed before the
guards, rather than infer harmful patching from shared-library exposure.
Prepatcher's existing `ModuleDefinition.AssemblyResolver` leads to the actual
`AssemblySet.AllAssemblies` list. Its `ModifiableAssembly.RawBytes` retains original
loaded metadata, allowing inspection of preceding FreePatch callbacks and module
initializers without reopening unrelated files. Native type discovery and
Prepatcher's attribute checks are metadata queries; ordinary static constructors
merely being present does not prove they executed.

However, the current native subclass cache cannot establish past constructor
order. `CreateModClasses` consumes `InstantiableDescendantsAndSelf`, which retains
the list returned by `GenTypes.AllSubclasses`. An earlier constructor could clear
and rebuild that cache, then throw before registration in `runningModClasses`.
The active iterator would still hold its previous list. Thus an empty completed
constructor dictionary plus Prepatcher first in today's cache is not a reliable
execution record. This is a concrete false-history case, not a claim that it
occurred in the captured startup. No extra snapshot probe or admission based on
that inference was implemented. Inspected Prestarter initialization offers no
earlier recording extension that resolves it.

A concrete external alternative is a bundled **Unity Doorstop 4.5.0 preloader**.
The project's pinned documentation describes executing a configured managed entry
point before Unity's managed startup, using the game's Mono runtime. It is
licensed LGPLv2.1. A bounded proof would establish the old Harmony guards before
game/mod execution, retain the existing replacement-generation bridge, and check
the actual initial assembly and execution order in the isolated GOG player.
This is proposed, untested, and introduces a native startup dependency/configuration
outside the ordinary mod-folder loading path. It requires steward/owner review
before adoption; nothing was downloaded, installed or executed. A reviewed
dependency would be bundled with its required license material, not left for
users to install separately. [Pinned upstream documentation](https://github.com/NeighTools/UnityDoorstop/blob/v4.5.0/README.md).

The proposal addresses the audio bootstrap prerequisite only. It supplies no
texture-completion backend and earns no broad-asset acceptance. Independent
source review also checked delaying materials, graphic collection children,
icons and atlas meshes: those products escape through plain field/array getters,
and native graphic cache hits precede the main-thread restriction. Moving their
creation repeats the established worker obstruction. Other inspected hidden
graphic caches are already lazy. Eager Vorbis window-table reuse or delayed
Wake-Up cache writes are adjacent optimizations, not substituted 5b delivery.

The integration decision was sent to the steward. Performance remains prohibited;
no repeated exposure, first-PCM or private-device probe was run. The full texture,
graphics/icons, audio/sound and colony-readiness requirements remain unchanged.

## Prepared-sample bootstrap observation — 12 September 2026 UTC

**The proposed safe startup rule rejects the ordinary startup that was tested.**
Both shared audio libraries were already loaded when Wake-Up's first Prepatcher
extension finished installing its guards. Those same library objects remained
active after Prepatcher replaced the game and Harmony assemblies. The guard
bridge worked, but cannot retrospectively account for a patch operation that
started before installation. This does not establish that such an operation
actually occurred; it establishes why this conservative admission rule cannot
enable prepared audio in this case. No prepared-sample feature is delivered.

The steward directed one minimal ordinary GOG observation before further cache
wiring. Source, core package and observer are
`f48f5e7326a17cbd09c7c139320d7dd1a8ff98f6`. The explicit fixture-only flag
`--fixture-c10-bootstrap-probe` installs guards on the old Harmony
`PatchFunctions.UpdateWrapper` and `ReversePatch` entries, then snapshots already
loaded `NAudio`/`NVorbis` assembly objects. The early path uses BCL (the standard
.NET library), Harmony and Cecil only; it does not resolve codec types or load
codec assemblies. A FreePatchAll extension inserts BCL delegate calls into both
replacement Harmony method bodies before they are loaded. An AppDomain
dictionary retains the observation across replacement. Admission stays closed
throughout; there is no pending reader, drain implementation or cache replay.

The captured `c10-audio-bootstrap-01` run used baseline mode, activation lane,
functional purpose, and minimized nonactivating launch. Its exact active order
was Prepatcher, Harmony, observer, Wake-Up, Core, Royalty, Ideology, Biotech,
Anomaly and Odyssey. It used the existing profile without a language or product
settings override. Generation remains `gog-rev573-20260910-194017-cf1a1eb0`,
manifest `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
The normal exit receipt records PID 13364, exit code 0,
`automaticTestPassed=true` and `audioBootstrapObservationPassed=true`.
`featureAcceptance=false` is explicit. Functional duration is diagnostic only.

| Observed runtime fact | Result |
| --- | --- |
| Old guards installed / replacement guard bodies verified | Both true |
| Actual calls from replacement Harmony into the bridge | 16 |
| Old and replacement Harmony assembly objects | Different references, same MVID `024a0e6e-c8c2-437e-ad04-7b6279389c23` |
| Old and replacement game assembly objects | Different references, same MVID `13bee51f-e6fa-4214-a4a5-e1e7b84a41ee` |
| NAudio already exposed after old-guard installation | Yes; MVID `fa168faf-488c-4ac3-8a7c-7c759315a688`; same live object after replacement |
| NVorbis already exposed after old-guard installation | Yes; MVID `6486cbdd-6db6-40cb-84f0-641a9b81a532`; same live object after replacement |
| Observer verification loaded a codec assembly | No |

The receipt contains object identity values, direct reference comparisons, both
emitted and runtime method hashes, and 21 bounded entry events. The two hash
forms have different encodings and are not compared to each other. Structural
guard checks and observed calls establish this bootstrap observation only;
they do not qualify every mutation route, concurrent drain, removal or reverse
patch scenario. No second run or cached-reload qualification was performed once
the prerequisite failed. This one small workload is not a universal modlist result.

### Why this affects the prepared-sample design

The native reader's saved Position is not a reliable replay cursor: its shipped
Ogg getter and setter use different time units. The draft instead retains the
ordered native seek arguments and read sizes, returned counts and exact sample
bits. It would replay that history through the existing decoder when prepared
data no longer suffices. A newly installed patch must not observe this historical
replay as if it were new native work. Published-patch metadata alone is too late;
the proposed barrier must settle existing readers before relevant mutation starts.

The reviewed Prepatcher reloads game/Harmony dependants but keeps shared system
NAudio/NVorbis dependencies. Protecting both Harmony generations therefore
does not close the earlier-exposed shared-codec gap. The next useful premise
must provide a supported boundary before that exposure, or a demonstrated way
to account for earlier operations, or materially redesign which work is deferred.
An arbitrary delay, metadata snapshot, module initializer, or a guard on newly
created game methods alone does not establish this. There is no authorization
here for a Prepatcher fork, forced system-library reload, global stream hooks,
raw detours or a host-wide worker-joining framework. This finding does not reject
prepared audio or broad on-demand assets as capabilities.

The inactive draft is preserved in the source commit: bounded sample transcript,
optional owned-store adapter, existing-native-reader adapter, and contract/tests.
Native identity constants remain `PENDING` and refuse admission. These classes
are unwired: no replacement clip path, production setting activation, shared
budget category, capture integration or lifetime/drain registry was added.
The existing experimental `deferredAudio` behavior is not replaced or newly
qualified. Do not ship or count this draft as functional replacement.

### Verification, restoration and remaining scope

The focused fixture admission/receipt test passed. Core and observer builds
passed with zero warnings/errors. Before this build, an attempted managed
contract test failed during test-project compilation on three ambiguous NUnit
overloads; those were corrected, but no managed draft tests ran and no contract
hash derivation is claimed. Independent bounded source review found no blocker
for the observation, including no direct early codec load and balanced emitted
IL; this was not a production implementation review.

Canonical run evidence is under
`.rlo-test-instance/results/c10-audio-bootstrap-01/`, including
`profile/FixtureMenuObserver/c10-bootstrap-probe.json`. A copied receipt and
restoration/audit records are under
`artifacts/ecosystem-next-20260910/c10/bootstrap-observation/`.
Supporting exact source reviews are `c10/bootstrap-review.md` and
`c10/stream-research/harmony-publication-review.md` beneath the same artifact root.
Core DLL SHA256 is `9f808b18c54aaf7512c3fb3b282e6d8a920085341c8b85d2ce7e5ccc8ccf4cb6`;
observer DLL SHA256 is `94aa6f39fb0979fcad960eac311b07f1ae8774031ca1a11cb0d8fb0871d08548`.

Preparation `20260911-203640-6eaf0ccd`, observer deployment
`20260911-203632-d0b08c3a`, and core deployment `20260911-203622-688ecef3` were
rolled back in that order. All seven assigned hashes and five absences match;
the metadata audit passes. The original GOG/core/observer endpoint is restored,
separately from the experimental source and captured package identities.
No performance, audible playback, ordinary sound, colony, texture/graphic/icon,
warm prepared playback or speed qualification occurred. Full C10/5b and earlier
C01–C09 measurement/C08 inspection obligations remain open. No successor, merge,
push or publication is authorized by this return.

## Steward streaming review and prepared-sample implementation — 12 September 2026 UTC

The steward independently reviewed clean `26eb50a2`, observer source `9386d608`, actual code and both captured results; a separate read-only reviewer found no blocking defect or material overclaim. The failed first probe remains failed despite normal menu exit. The successful probe establishes creation-time PCM demand, not candidate behavior. Package/captured receipt hashes and seven restored baseline hashes/five absences match. C10 has no functional acceptance.

Stop the first-callback-only decoder wrapper. The next materially different design supplies the initial required samples from validated prepared data, rather than assuming creation needs no samples. On first load, native loading remains authoritative and can produce reusable exact metadata/sample data. On later valid reuse, create the same complete native streaming clip and satisfy its actual read/seek requests from those prepared samples; initialize the existing decoder only when a request needs data not retained. Any request outside a prepared range must receive real native data, not silence or a placeholder. Source/decoder identity, exact native sample/seek units, metadata, errors, callback effects, stream ownership and continuation are necessary contracts. Native non-streaming clips stay native. The previous single Ogg receipt is causal evidence, not a hardcoded universal buffer size or fixture-recognition rule.

Implement a bounded real audio increment using existing cache/lifecycle foundations if those contracts can be satisfied. First-build eager work, source validation, stored audio data and later decoder initialization must be accounted separately. No new third-party decoder, native engine integration, end-user dependency or default is authorized. Guard incompatible hooks and use ordinary native fallback. Validate native-equivalent samples across the prepared-to-decoder boundary, seeks, changes/corruption/reload, natural unrelated sources, off mode and startup readiness. Later unattended performance must include preparation/storage costs and first-use effects. A warmed large-stream feature alone cannot close broad C10; texture/graphic/icon, ordinary sounds and full coverage remain open. This is a changed, evidence-backed implementation premise, not a repeat of the rejected zero-startup-sample assumption.

## Native streaming reader result — 11 September 2026

**The tested streaming clip needs decoded samples while Unity is creating it.**
Waiting until the first request for decoded audio (the PCM callback) would still
initialize the decoder during creation for this naturally streamed Ogg file.
The native reader also reads through the encoded file while establishing its
metadata. This settles the assigned narrow question without building a nominal
lazy adapter. It does not prove that every format or every audio strategy fails.

The same C10 owner resumed from clean `cd7c69fd4da552ee4cbd05d3617fd381afaae7a5`.
Only the isolated observer, fixture admission/capture and focused tool tests
changed; production code and settings did not. There is no candidate feature or
functional acceptance. Full C10/5b texture, graphic, icon, ordinary sound/audio
and colony readiness remain open, as do prior measurement obligations.

### What the functional probe establishes

The probe calls the unchanged native `Manager.Load(Stream, AudioFormat.ogg,
name, doStream: true)` twice on P-Music's `Strange_Feeling.ogg`, a 1,086,292-byte
file above the native 307,200-byte streaming threshold. Its frozen source SHA256
is `2f70c7ddd156d1379d0ddbb8df1f959fec1ea0df28bd62a30ae9dd2371d5f371`.
A transparent input-stream wrapper counts encoded reads/seeks. Observational
Harmony patches record constructor, decoded-read and `AudioClip.Create` order;
they capture but do not replace the original native read/seek callbacks.
The separate file identity read is excluded from reader counters.

| Observation | Native case 0 | Native case 1 |
| --- | ---: | ---: |
| Reader constructions before clip creation | 1 | 1 |
| Encoded bytes returned during construction, including rereads | 1,118,394 | 1,118,394 |
| Highest constructor read position, equal to file length | 1,086,292 | 1,086,292 |
| Constructor seeks | 1 | 1 |
| Decoded reads inside `AudioClip.Create`, before it returns | 19 | 19 |
| Returned float samples inside creation | 76,800 | 76,800 |
| Nonzero float samples inside creation | 76,800 | 76,800 |

Those same 19 reads precede any explicit test consumer. Creation also calls the
original seek callback with zero. All observed callbacks used managed thread 1
in this capture. Later explicit calls to the original callbacks at `Seek(0)`
and `Seek(8192)` each return 4,096 nonzero floats; both cases have identical
sample hashes at each position. The native seek argument is passed unchanged
to its byte-oriented reader; the probe does not correct or reinterpret units.

Both clips expose 3,209,136 samples, two channels and 48,000 Hz, with identical
length bits. Raw Unity metadata says `DecompressOnLoad`/`Loaded`, while the native
Manager registry says `Streaming`/`Loaded`. Public `AudioClip.GetData` returns
false and leaves the test buffer zero-filled in both cases. These native
limitations and distinctions are preserved, not normalized into a candidate.
`nativeComparisonPassed` means repeat native cases agree, not that an optimized
implementation passed comparison.

No `AudioSource`, playback or global mute was used. Direct calls to captured
original callbacks establish sample/seek causality, not audible playback or full
sound compatibility. After end-of-frame clip destruction, the probe disposes its
owned reader and input stream and removes only its own native registry entries.
Both cleanup receipts passed; this does not establish native lifetime behavior
for arbitrary game clips.

### Native source explains the early work

`CustomAudioFileReader` constructs its format reader and sample-conversion
pipeline, then obtains exact length before `AudioClip.Create`. The shipped Ogg
path initializes Vorbis decoding state and buffers; exact duration gathers Ogg
pages through end-of-stream. Construction alone is not proof of PCM decoding:
the runtime callback observations above supply that separate evidence.
Shipped MP3 code also scans frames and constructs its decoder; PCM WAV mostly
parses/seeks metadata, while compressed WAV constructs a conversion stream and
the shipped AIFF header parser can read payload chunks. These other formats were
inspected only, not tested live. No alternative faithful metadata/decoder path
was implemented or qualified.

The bounded conclusion is therefore to stop the proposed first-PCM lazy-reader
route for this native Ogg case. Reopening it requires a materially different
way to avoid work while supplying the original complete clip, metadata and
creation-time sample requests; a callback wrapper alone does not do that.

### Exact evidence, validation and restoration

- Observer source `239512521cd040da14ac91054ed9124813a055fa` adds the probe;
  `132c63c` restricts it to the ordinary menu cleanup controller. First capture
  `c10-stream-causal-01` failed before any cases because Harmony requires the
  inherited `Seek` patch on its declaring `WaveStream` type. It exited normally;
  its failed probe receipt is retained and supplies no reader/callback result.
- Correction and successful observer source:
  `9386d608ddb578a9ff546b8e171990bba8daedfa`. Packaged/deployed observer DLL SHA256:
  `06fedadc7215b1fad4462f8217529ca59b884caf8f9db8397169e0e439fbe258`.
  Observer builds had zero warnings/errors; no native probe DLL or new decoder
  dependency was packaged. Two existing managed checks and 70 fixture-tool
  checks passed. The inherited-method correction was then verified by the live
  functional probe.
- Successful capture `c10-stream-causal-02`: purpose functional, PID 24332,
  exit code 0, `automaticTestPassed=true`, `audioStreamProbeTestPassed=true`.
  Core remained the assigned GOG package from
  `ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8`, DLL SHA256
  `e507575091f92f2b0d1882e828943666d3557cc7c8b0fc4487f2068b590b6f68`.
  It was not rebuilt or redeployed from the newer observer source.
- Raw capture is under `.rlo-test-instance/results/c10-stream-causal-02`.
  Its exact copied receipt is `artifacts/ecosystem-next-20260910/c10/stream-research/native-result.json`,
  SHA256 `488010e96a9be2d72b4a44d0f86a3ba99e8df718f25e49562c9f1db2325431f3`.
  The same directory holds `run-summary.json`, `decoder-contract-review.md`,
  source extracts, build/test logs and transaction rollback receipts.
- Both preparations and both observer deployments were rolled back. Fresh
  `stream-research/restoration.json` verifies all seven assigned endpoint hashes
  and five absences; the metadata audit retains manifest
  `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef` and matching
  deployed runtime. Original observer source `3d86b163`/DLL `b1fa3c65…` is restored,
  with no prepared run, helper or experimental native probe payload installed.
  MSBuild was shut down and no RimWorld, testhost, MSBuild or helper process remained.

No performance test, benefit claim, normal-data operation, Steam/Deck promotion,
UI automation, security change, push, merge or publication occurred. Functional
timings are diagnostic only. This return does not accept C10 or authorize C11;
earlier C01–C09 measurement and C08 inspection obligations remain unchanged.
Final independent read-only review found no blocking defect or material overclaim
in the corrected probe, receipts and documentation. Child review and all local
operations are stopped; exclusive editing/operating ownership returns to the steward.

## Steward sound review and decoder decision question — 11 September 2026

The steward reviewed clean `84403744` and the actual native SubSoundDef/ResolvedGrain implementations. The callback-replay objections are supported: later worker duration reads are managed-only, whereas original resolution looks up clips and caches length; silence construction consumes random state. Seven hashes/five absences were independently rechecked. No sound functionality is accepted.

The native `RuntimeAudioClipLoader.Manager.Load(Stream, ...)` constructs `CustomAudioFileReader` before `AudioClip.Create`, obtains sample count/channels/rate, and captures that reader in native PCM read/seek callbacks. A distinct open question is whether complete, faithfully identified streaming clips can postpone decoder setup until those callbacks need it. First establish actual callback timing and reader work; native streaming itself is not new Wake-Up behavior. If Unity immediately requires the reader for every created clip, record that result rather than building a nominal lazy wrapper. If useful work remains, preserve first-build metadata/source identity, native sample and seek behavior, public data/load-state semantics, original errors and disposal before considering a real implementation. Changing non-streaming clips into streams is not an automatic substitute.

This authorizes one focused GOG correctness investigation and a concrete implementation route only if it demonstrates avoidable work under unchanged native contracts. No performance measurements, broad new decoder framework or new end-user dependency is authorized by this direction. It does not reopen device-creation tests, authorize unsupported engine internals, adopt narrower compatibility or close sound/texture requirements. The same C10 orchestrator owns the next bounded step after dispatch; exact base is recorded in steward state.

## Ordinary sound boundary returned — 11 September 2026

**Delaying the entire native sound-resolution callback would break existing
worker reads and change some sound behavior.** The private resolved-grain list
does offer a smaller allocation boundary, but preserving native semantics keeps
clip/provider lookup, duration acquisition and random draws eager. This bounded
pass did not find a larger drop-in operation that can be delayed safely. It does
not prove that all sound-loading approaches are impossible.

The same C10 task resumed from exact clean
`877bd56d6dc85e76a384b62deb4d9f78030e7ea7`. The steward then explicitly directed
it not to grow repeat-selection bookkeeping into a settings/harness/feature
project, and to return the source-supported obstruction promptly if the current
pass found no larger boundary. Accordingly, this return contains **no new runtime
implementation or functional acceptance**. No repeat native-device test occurred.
C10/5b, texture/graphic/icon deferral, full ordinary sound/audio and colony readiness
remain mandatory open work. This is not adoption of eager public assets as the
final scope. Exclusive operating/editing ownership returns to the steward.

### Native sound behavior and the exact obstruction

A sound grain is one possible clip or silence interval from which a sound chooses
a sample. `SubSoundDef.ResolveReferences` schedules an `ExecuteWhenFinished`
callback. At that callback's original turn, it clears its private resolved list,
enumerates the public grain definitions in order, creates resolved grains,
computes distinct choices/repeat-avoidance counts and reports an empty result.
`AudioGrain_Clip` calls `ContentFinder<AudioClip>.Get`; `AudioGrain_Folder` calls
`GetAllInFolder`. Both lookup APIs explicitly reject off-main-thread calls.
`ResolvedGrain_Clip` then stores the complete clip and reads `clip.length` once.

By contrast, public `SubSoundDef.Duration` later scans stored float durations;
`SoundDef.Duration` combines those values. A worker can perform that managed read
after native resolution, including through reflection. Making that read execute
the deferred native callback would introduce clip-lookup thread restrictions
absent from the old read. This conclusion relies on the explicit lookup guard;
the Unity length binding lacks a thread-safe declaration, but that absence alone
is not treated as proof of a runtime exception. No new worker failure or wait on
main was implemented.

Other source contracts prevent simply moving the callback to first playback:

- `ResolvedGrain_Silence` draws `durationRange.RandomInRange` during construction.
  Moving it changes global random-number chronology and cached public duration.
- `TryPlay` covers one-shots; `SubSustainer.StartSample` directly calls
  `RandomizedResolvedGrain`, reads the resolved duration, passes the real clip to
  `SampleSustainer.TryMakeAndPlay`, then calls `Notify_GrainPlayed`. Gating only
  one-shot playback leaves sustained sound and public selection uncovered.
- Native resolution freezes membership and duration at its original callback
  time. Reading public grain paths/lists or provider contents at later demand
  changes the effect of intervening mutations.
- Re-resolution clears membership and recomputes counts but does **not** clear
  `recentlyPlayedResolvedGrains` or `lastPlayedResolvedGrain`. Editor test playback
  explicitly re-resolves; hot reload reuses definitions; global clear destroys
  content and definitions. These are different lifetime boundaries.
- Custom grain/subsound subclasses, iterator bodies, constructor hooks, native
  lookup callbacks, errors and partial failures require their original behavior.
  A partially failed snapshot followed by native replay can duplicate effects.
  An arbitrary private-field reflection scan is not a universal readiness proof.

Native source evidence is in the existing `c10/SubSoundDef.cs`,
`AudioGrain_Clip.cs`, `ResolvedGrain_Clip.cs`, `Unity-AudioClip.cs`,
`c07/research-native/ContentFinder.cs`, and the new bounded
`c10/sound-research/Verse.Sound.*.cs`, `Verse.SoundDef.cs`,
`Verse.PlayDataLoader.cs`, `Verse.LongEventHandler.cs` extracts. All are under
`artifacts/ecosystem-next-20260910/`. Game-side sound extracts use the unchanged
GOG `Assembly-CSharp.dll`, SHA256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
`Unity-AudioClip.cs` instead comes from the shipped `UnityEngine.AudioModule.dll`,
freshly checked SHA256
`abc57ac9f40d0b5151c248726549f5470c338bbd03183d5146b61d486512b797`.
The independent review's `sound-research/contract-review.md` records exact method
and line references. No live worker or playback verdict is claimed by this pass.

### What remains technically possible, and what it would actually avoid

| Alternative | Actual deferred work and limits |
| --- | --- |
| Small delayed repeat-selection aggregation | Can postpone distinct-choice/count work for undemanded sounds, subject to preserving original identity/count semantics. Grain objects, clips, lookups, metadata and callbacks remain eager. This is bookkeeping, not deferred sound loading; no separate option or harness was built. |
| Frozen descriptors plus an exact managed grain factory | At the original callback, capture ordered complete clip references, native duration and stable identity keys; later materialize exact native grain objects without Unity calls. This can delay some wrapper/list allocations. It still performs clip loading, provider lookup, folder enumeration and metadata eagerly, and introduces descriptor storage plus constructor/iterator admission and per-resolution lifetime work. No useful net benefit or full callback contract was demonstrated; no general factory framework was built. |
| A separately bounded decoder/stream preparation design | Keep complete native public clip objects and faithful metadata eager, but investigate whether an owned native-equivalent PCM read/seek boundary can postpone decoder/reader setup. This is a materially different proposal, not an existing proven path: metadata provenance, first-build work, formats, public sample reads, original load state, seek/failure and disposal would all need a concrete faithful implementation. Native streaming/background decode already provided by the game must not be credited as new deferral. |
| Cooperative preparation before worker access | Could permit later native resolution if all relevant worker consumers agreed to prepare on main first. This changes compatibility and is not approved or adopted. |

No source-only result establishes a worthwhile net gain from substituting private
descriptor allocations for native grain allocations. The independent reviewer
also advised against a substantial factory/guard framework for this return.
Performance remains prohibited; none was measured to justify an incidental
bookkeeping feature. These alternatives leave the capability open rather than
quietly excluding ordinary sounds from C10.

### Real source context, earlier suppliers and closeout

Bounded current source examples include Core `CryptosleepCasket_Eject` and
sustained `CrashedShipPart_Ambience`, plus Vanilla Events Expanded
`VEE_PsychicStimulationSound` and `VEE_PsychicOverdriveSound` clip grains.
Exact XML paths/hashes are recorded in `sound-research/real-source-examples.json`;
the VEE inventory/source hashes match the frozen manifest. These are ordinary
source examples, **not runtime-resolved coverage, avoided-work counts or playback
checks**. No synthetic specimen or music-only result substitutes for a sound
implementation.

The original FGL source at pinned `0132c10a` queues native subsound callbacks and
temporarily refuses one-shots, direct `TryPlay` and sustainer creation, draining
before unpatching at world finalization. That establishes real postponement but
violates this assignment's no-muting/complete-consumer contract. The inspected
YaOpt source provides no ordinary sound-resolution hook to supply the missing
boundary. Earlier S11 and R3/RC2 records were checked: native large-clip streaming
already supplies deferred reads, and R3's compiled song-field rewrite explicitly
does not cover arbitrary dynamic/reflected/worker access. It was not reused.
Existing audio settings and their documented limitations remain unchanged.

Only documentation and ignored research changed. No source build, test, package,
deployment, preparation, game launch, audible/visible check or fixture mutation
occurred in this resumed pass. No restoration transaction was needed. Fresh
`sound-research/endpoint-verification.json` checks all seven assigned hashes and
five absences against `c10/native-restoration.json`; all match, including absent
experimental DLL/DDS. Final process inspection found no RimWorld/helper/testhost/
MSBuild process. The independent child review is stopped. No normal data, other
application, UI, security, Steam/Deck, performance, merge, push or publication
operation occurred. Final independent report review found no substantive
overclaim and corrected the separate AudioModule provenance above. Prior C01–C09
measurements and C08 inspection stay pending.

## Steward review and next C10 increment — 11 September 2026

The steward independently reviewed clean `68dd2f6d`, source/package/captured receipts and the restored fixture; a separate read-only reviewer found no blocking defect or material overclaim in the bounded experiment. Seven baseline hashes and five absences match. The successful receipt establishes private native device resource creation only, with no wrapper publication or GPU readback. The failed first capture remains failed. This is an accepted experiment result, **not functional acceptance of C10/5b**.

Do not repeat device-creation tests: the missing operation is complete, correctly synchronized publication through an existing Unity object, including native CPU data and callback semantics. No supported complete route has been demonstrated. Unsupported engine internals, global synchronization interception and compatibility reduction remain unapproved; texture/graphic/icon deferral is an open engineering obligation, not an excluded or post-release feature.

Continue useful work within the same C10 task on an independently bounded sound-resolution increment. Inspect the exact native resolution, duration/selection/playback/subclass and lifecycle contracts, then deliver real avoided eager resolution work on ordinary sound content where a private boundary can preserve those contracts. Keep public clips and exposed state complete. Native streaming already supplied by the game, mere routing, music-only behavior and eager clips must be counted honestly; none closes broad C10. The existing incomplete song-field approach is not a valid substitute. Actual correctness evidence precedes independent review; sound, texture, graphics and colony readiness obligations remain tracked separately within the same parent capability.

This is an implementation-order decision within already approved C10 scope, not adoption of the eager-public-assets alternative as the final product. It does not authorize bypassing the unresolved broad texture requirement or dispatching C11. If no useful sound work can be deferred under the native contracts, return that specific finding and alternatives without further generic harness work. The active assignment records the exact successor base and serial ownership. GOG functionality testing is standing-authorized; performance testing remains prohibited without a current unattended grant.

## Bounded native experiment returned — 11 September 2026

**The native DLL can reach Unity's graphics device and create a private texture
on a worker while the main thread waits. It does not establish safe completion
of an existing Unity texture.** The approved experiment stops at that remaining
submission boundary. C10/5b and both broad implementation increments remain open;
there is no production asset deferral or compatibility reduction. Exclusive
editing/operating ownership returns to the steward. The older dispositions below
are historical, superseded by this result and the intervening approval.

The exact resumed base was `69cd51b7ed8f66589a4ce193088f06dab9855ff2`.
Prototype `bb429cb96dd2db276f7ae8b7f1a7d67cbe23a7bc` added an optional observer-only
Windows SDK DLL and functional-only preparation/launch admission. Correction
`e77bb773c4d2ef1ee5c37439ed547af1addc7948` resolves its path from the native mod
content root. Product source/settings and the existing core package are unchanged.
The DLL is private test infrastructure, not a selected production dependency.

### What the live proof establishes

Main creates a private complete 4x4 Unity texture and obtains its documented
`ID3D11Resource*` pointer through `Texture.GetNativeTexturePtr`. The DLL calls
`GetDevice`, retaining the device reference. This is supported resource/device
bootstrap; it does **not** demonstrate automatic `UnityPluginLoad` registration
for a DLL loaded through `LoadLibraryEx`. No private Unity symbols are used.
[Unity pointer contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture.GetNativeTexturePtr.html)

The input is the frozen Vanilla Events Expanded
`Textures/Weather/HazeMask.dds`: 11,064 bytes, 128x128, BC1, eight authored mip
levels (successively smaller image versions used at smaller display sizes), SHA256
`e5d0e4b5cd93a668ac380e2e28767f14fc94097802c1a50911de577e74c92732`.
Its source inventory hash was verified. The private copy lives under `Tools/`,
outside RimWorld's eager texture loader. It is read as bytes before the worker
starts; this experiment makes no deferred-I/O claim.

Main then waits synchronously on a Task. The worker validates the entire bounded
DDS payload and creates a separate immutable D3D11 texture using initial data for
every mip. It checks the returned descriptor and releases that resource before
returning. It neither fills the bootstrap texture nor publishes any texture to a
holder, dictionary, material or callback. The retained device is released on main
after the Task finishes; the bootstrap Unity object is destroyed normally.

Run `c10-native-device-02` reports `bootstrapHr=0`, `createHr=0`, device flags `32`
(the single-thread-only flag is absent), feature level `45312`, main native thread
`44404`, worker `42404`, eight mips and DXGI format `71` (`BC1_UNORM`). Creation and
descriptor equality both passed. The worker uses no rendering context or DXGI
call. The receipt explicitly records `unityWrapperCompleted=false`,
`gpuReadbackVerified=false`, `c10Accepted=false`. These results establish successful
native API creation with initial data, not a measured GPU fence, GPU pixel
readback, faithful Unity color-space/CPU-storage equivalence or public readiness.

### Why this does not yet supply demanded Unity textures

The missing operation is safely finishing the texture that an existing Unity
object already represents, when main cannot help. Creating a different native
resource does not attach it to that object. Unity's documented
`UpdateExternalTexture` supplies no worker-thread guarantee; the shipped binding
also has no thread-safe declaration. That absence is not proof of impossibility.
[Unity external update](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.UpdateExternalTexture.html)

Updating Unity's existing resource through its immediate rendering context is not
admitted by the device's thread-safe creation contract. Even already-enabled
context locking would not prove coordination with Unity's DXGI/presentation work.
The prototype never reads or changes global context protection and never attempts
that unsafe inference. A private deferred context still requires the immediate
context to execute commands and retrieve completion results.
[Microsoft threading restrictions](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-render-multi-thread-intro),
[deferred-context execution](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-render-multi-thread-render).

A second device cannot write Unity's existing ordinary nonshared allocation.
Shared allocations require creation-time flags; documented baseline sharing
guarantees do not cover this authored compressed full-mip contract. Keyed sharing
also requires participating-device ownership operations that ordinary Unity
sampling does not promise. `WriteToSubresource` still needs context map/unmap.
Unity rendering events are the supported rendering-thread route, but their
documentation supplies no progress guarantee for arbitrary later worker demand
after main stops submitting work. No global pump, synchronization interception or
private engine interface was attempted.
[sharing flags](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_resource_misc_flag),
[D3D11.1 sharing guarantees](https://learn.microsoft.com/en-us/windows/win32/direct3d11/direct3d-11-1-features),
[WriteToSubresource](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_3/nf-d3d11_3-id3d11device3-writetosubresource),
[Unity rendering events](https://docs.unity3d.com/2022.3/Documentation/Manual/NativePluginInterface.html).

This bounded review found no supported completion route for the required existing
Unity object. It does not rule out every graphics technique or future integration.
Continuing would require a concrete new supported synchronization/binding contract
or a separate owner decision about deeper integration. The already-recorded
cooperative-worker or eager-public-assets alternatives reduce approved coverage;
neither is adopted. Audio, PNG/JPEG first-build creation, CPU/GPU equality,
color/mask pairs, atlas consumption, callbacks, cancellation/retry/generation,
borrowed lifetimes and real colony workflows remain unproved. A created native
texture cannot substitute for those obligations.

### Verification, exact packages and restoration

Both builds used the installed MSVC/Windows SDK and existing CMake, with static
CRT and no downloaded dependency or installed tool. The native DLL's only import
is `KERNEL32.dll`. Managed builds reported zero warnings/errors. Two existing
managed worker-boundary tests and all 69 fixture-tool checks passed; the new
fixture check rejects performance purpose at preparation and launch admission.
Results: `artifacts/fixture-tests/3c6c042a0f5f4cc583941559eecd8222`.
An independent read-only reviewer checked native lifetimes/threading and package
admission; its payload-placement correction is included. The subsequent root-path
correction was validated by the actual successful run. Final independent review
of that correction, this report and the result/restoration receipts found no
blockers or material overclaims; the reviewer is stopped.

The first functional run, `c10-native-device-01`, reached menu but failed before
the native call: Prepatcher loads the observer from memory, leaving
`Assembly.Location` empty. The old path lookup was outside the probe's catch and
prevented automatic exit from being armed. Player log, menu receipt and exact PID
identity were preserved before stopping only verified fixture PID `45708`.
Its capture remains failed (`automaticTestPassed=false`, exit `4294967295`).
The correction uses `ModContentPack.RootDir` and isolates probe-reporting exceptions
from automatic exit. The successful run's start receipt confirms the empty
assembly location and correct package root. No failed sample is treated as success.

Successful run `c10-native-device-02` used baseline activation, functional purpose,
minimized-noactivate, independent menu observation and normal automatic shutdown:
PID `38736`, exit `0`, `automaticTestPassed=true`. No colony/gameplay or performance
claim follows from that menu/device check. Source and package identities are:

| Item | Identity |
| --- | --- |
| GOG generation | `gog-rev573-20260910-194017-cf1a1eb0` |
| Manifest SHA256 | `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef` |
| Game assembly SHA256 | `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` |
| Unchanged core source | `ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8` |
| Unchanged core DLL SHA256 | `e507575091f92f2b0d1882e828943666d3557cc7c8b0fc4487f2068b590b6f68` |
| Successful observer/native source | `e77bb773c4d2ef1ee5c37439ed547af1addc7948` |
| Observer DLL SHA256 | `13043b08874e272b60d12cabfe7982387f81672db9c917bb30618b4cb6187ebd` |
| Native DLL SHA256 | `0c1c2feb7cea2c204e2584d61a37e3dcf1161aded27a8bad3636cec1fe967259` |

The complete package receipt is under `artifacts/fixture-menu-observer/e77bb773c4d2ef1ee5c37439ed547af1addc7948.json`;
both captures remain in the ignored fixture results. Compact native receipts,
build/test/launch logs and `native-restoration.json` are under
`artifacts/ecosystem-next-20260910/c10/`. The captured native receipt was verified
identical to the copied evidence. Restored transactions, in actual rollback order:
`20260911-191141-c1d0350e`, `20260911-191451-6cfc08d6`,
`20260911-191442-d1ed6a26`, `20260911-191132-927e9259`.
All seven assigned endpoint hashes and three original absences match; the
experimental DLL and DDS are also absent from the restored observer. Metadata
audit reports matching deployed runtime. No core/helper deployment occurred.
MSBuild server, child review and fixture operations are stopped. No normal game,
other application, Steam/Deck data, security setting or UI was changed. There was
no performance test, push, merge or publication. C01–C09 pending measurements and
C08 carried inspection remain separate and unchanged.

## C10 native-plugin experiment approved — 11 September 2026

The owner approved the bounded supported-interface native-plugin experiment described in the C10 decision review, and granted standing permission for further isolated GOG testing without repeat testing approval. The same C10 orchestrator resumes from the steward's approval-record commit on reviewed `e2fffc88` ancestry; the dispatch state records the exact base and ownership transfer. Full broad on-demand scope and correctness remain required. No prototype result is implementation acceptance.

First prove supported device access and completion without main-thread progress; then prove complete native texture data, CPU/GPU correctness, masks/mips/atlas use and safe lifetime. Stop before unsupported engine internals or global synchronization changes if the supported route fails, and return concrete evidence to the steward. Audio and remaining formats stay open. Any future shipped dependency must be bundled. Performance testing remains prohibited while the owner uses the PC; Steam and later platform promotion remain separately approved.

Earlier statements that this experiment awaits approval are historical and superseded by this decision. This is permission to investigate and test, not a compatibility reduction or automatic production-backend approval.

## Independent steward decision review — 11 September 2026

Reviewed clean checkpoint `3a81b730fb3d5c8775c129e7f36e4916bbc6f921`
against `823555a`. Only tests and documentation changed. The steward checked the
actual native getter/dictionary bodies, the two managed-shell tests, shipped
texture bindings and the official Unity 2022.3/Direct3D11 contracts. The supported
worker-access counterexample establishes a conflict for the simple delayed
construction design; it does not establish that a particular installed mod uses
that pattern, nor that every possible implementation is impossible. Broad wait
pumping lacks a demonstrated safe synchronization/callback boundary.

The seven assigned fixture hashes and three absences independently match.
No fixture/build/helper operation remains; the normal Steam game is outside the
fixture and untouched. Ownership returns to the steward. C10 is **not functionally
reviewed or accepted**: this is a decision-ready architecture checkpoint only.

**Proposed owner decision, not approved:** authorize one bounded Windows native
plugin proof using supported engine/graphics interfaces, preserving full 5b scope.
First establish safe device/bootstrap/submission and completion without main-thread
progress; then require a complete demanded native texture payload with correct
CPU/GPU data, mask/mip/atlas behavior and lifetime. Count wrappers/allocation that
remain eager honestly. Stop and return evidence if supported interfaces cannot
meet that gate; no private engine ABI, global synchronization interception or
production integration is authorized. Success would justify a further concrete
implementation plan, not close PNG/JPEG/audio/broad coverage or C10 automatically.
Functional proof would remain inside isolated GOG; no performance measurement
without a new unattended window. A successful future shipped dependency would be
bundled, consistent with the owner's no-manual-dependency requirement.

This requires an owner decision because a native DLL loaded inside the game adds
graphics-resource ownership and maintenance beyond the approved external CPU
helper. Alternatives are an explicitly narrower cooperative-worker compatibility
contract or eager-public-assets/private-work scope; either changes approved 5b
and needs the owner's acceptance of lost coverage. None is adopted by this review.

## Current disposition — 11 September 2026

C10 remains **open, not implemented or functionally complete**. The initial
private-holder design can safely delay main-thread requests, but cannot preserve
all currently working worker requests through asset hooks alone. The steward
requested this bounded assessment before an owner decision; no compatibility
reduction, new default, dependency or global synchronization hook was adopted.
This checkpoint does not close matrix 5b or either implementation increment.

The single checkout began clean at `823555a0902fa7c464d49fbcfbbf8f9783aad7cb`
on `codex/ecosystem-next-plan-review`. Exclusive operating ownership returns to
the steward with this bounded checkpoint. Main and campaign ancestry are preserved.
Only tests and this decision record changed; no product or fixture deployment
changed. Performance testing remains prohibited while the owner uses the PC.

## What the exact game allows

GOG revision 573, Assembly-CSharp SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`, privately
stores texture/audio holders inside `ModContentPack`. A holder becomes publicly
reachable through `GetContentHolder<T>()`; its `contentList` is a public concrete
dictionary. Complete that entire holder before exposing it, and previously
retained references remain safe. A holder exposed before initial loading must
stay eager. Main-thread internal `ContentFinder` requests can instead complete
one private source or the requested subtree before returning a real object.

`ContentFinder.Get` and folder enumeration already reject worker-thread calls.
Preserving that rule would be native behavior. **Public holder acquisition and
dictionary reads have no such restriction.** This supported native pattern needs
no engine call once eager loading has completed:

```csharp
// In a main-thread mod callback after ordinary content loading:
var texture = Task.Run(() => pack.GetContentHolder<Texture2D>().Get(path)).Result;
```

With uncreated content, the worker needs the main thread to create its texture,
while the main thread waits for that worker. Throwing, returning null or waiting
for a timeout changes working behavior. Reflecting the public getter or field
has the same problem. Scanning compiled readers cannot remove it. Any code mod
can reach other providers through `RunningMods`; data-only source mods are not
isolated from these consumers. This is a rejected construction boundary, not a
proof that all forms of on-demand assets are impossible.

Two focused tests execute the native managed getters on workers while their
caller waits, including reflected audio-holder/dictionary access. Both pass.
They use managed texture/clip shells to prove the getter/reference contract;
they do **not** create Unity assets or qualify candidate rendering/playback.
No deliberate fixture deadlock or repeated scheduling experiment was needed.

Exact extracts and independent findings:
`artifacts/ecosystem-next-20260910/c10/native-boundary-review.md`;
`c00/source/GOG-ModContentPack.cs:167`,
`c08-corrections/ModContentHolder.cs:13`,
`c00/source/GOG-LoadedModManager.cs:221` under that campaign artifact directory.

## Finite scheduling boundaries and pumping

A queue pump performs requested main-thread work while another operation waits.
It is safe only when that scheduling point and the work's side effects are owned.

| Caller or boundary | What a finite adapter can establish | What it cannot establish |
|---|---|---|
| Known game worker with known dependencies | Prepare those assets before dispatch, then preserve ordinary reads | Unknown dependencies introduced by callbacks or replacements |
| Native `LongEventHandler` worker | Its main loop polls `eventThread.IsAlive`; a specifically verified stage can prepare before starting it | Every mod worker, every nested wait, or arbitrary state held by its callbacks |
| Mod-created Task/Thread/ThreadPool/Parallel worker | An explicit integration can prepare declared holder families first | Generated dispatch, native dispatch, preexisting workers or arbitrary first holder selection |
| Already-running worker | Complete declared dependencies before making its native readiness signal visible | Infer all future content from the fact that no new dispatch occurs |
| Opaque callback | Eagerly complete all content it may expose before invocation | Preserve useful broad pending work when it may access every provider |

Patching `Task.Wait` or `Thread.Join` alone misses `Result`, wait handles,
monitors, custom spins and native waits. Pumping also changes callback order:
the waiting caller may hold locks or temporarily inconsistent state, and a
texture constructor/upload hook can inspect that state or wait for a worker
needing those locks. Reentrant locks do not repair inconsistent state. C08's
publication-history and Unity-callback rules forbid suppressing such effects.

Cancellation must reject old-generation publication, preserve native exception
propagation and avoid destroying borrowed objects. Those rules are necessary
but do not break a preexisting wait cycle. No safe universal scheduling point
was found in the inspected native loading methods. A finite game-worker adapter
is viable for its precise consumers; it is not a general answer for the current
public-holder contract. A cross-mod scheduler would be a substantial separate
architecture with continuing interception/compatibility costs, not a small fix.

## Other boundaries and actual coverage

Native `GraphicData.Graphic` already lazily fills a private cache. However,
`ThingDef.PostLoad` assigns the public graphic and registers atlas inputs;
`BuildableDef.PostLoad` assigns public icons/materials; `SongDef.ResolveReferences`
assigns public clips. These callbacks must finish with real ready objects.
Deferring them and repairing fields later repeats the rejected R3/public-state
problem. Whole graphics/mask groups must also remain complete for C07 atlases.

`SubSoundDef` has private resolved grains, but exposes duration, randomized grain
selection, playback and subclass behavior. Exact `AudioGrain_Clip` resolution
calls `ContentFinder` and creates a resolved grain around its clip. A future
private sound adapter must cover all these methods and native callback effects;
playback alone is insufficient. Keeping clips eager can permit narrower managed
work deferral, but cannot count stream opening or clip creation as avoided.

The existing C09 selection contains unrelated event, architectural, texture and
music content. Frozen inventory accounting under its reviewed 1.6 folder choices
and ordinary DDS sibling precedence gives:

| Real provider | Selected filesystem family available to the design |
|---|---|
| Vanilla Events Expanded | 93 DDS textures and four Ogg sources |
| Vanilla Textures Expanded | 2,751 DDS textures |
| Vanilla Furniture Expanded - Architect | 70 DDS textures |
| P-Music | 56 Ogg sources |

These **2,914 texture / 60 audio source candidates are not C10 deferral receipts**.
The accounting is in `artifacts/ecosystem-next-20260910/c10/source-inventory.json`;
it reuses hash-checked frozen inventories and explicitly recorded folder choices,
not a production package allowlist or new performance run. This narrow selection
has no naturally selected PNG/JPEG after DDS precedence; those remain required
additional real-content coverage. C05/C06 format evidence does not establish C10
readiness. New avoided creation, first-demand and undemanded counts are all **zero**
because no production C10 path has been enabled.

Resources, bundles and shaders already load on native demand. Keep that behavior
and credit the game. Earlier worker decoding or completed C05/C08 preparation can
reduce/reorder creation work, but keeping all public objects eagerly complete
does not become deferred creation merely by changing its representation.

Pinned FGL `0132c10a` explicitly repairs BadTex icons, refreshes map meshes and
suppresses sound until deferred resolution finishes. YaOpt `ab60621f` uses
selected completion paths over pending textures and removes pending state before
successful completion. Their source supports real delayed work, but does not
establish this stricter public/worker contract. Neither supplier was runtime-tested
against this worker scenario; no inferred pass or failure is reported.

## Recommendation and concrete choices

Do not implement global wait pumping as a routine C10 correction. Keep public
song/icon/graphic callbacks native, preserve C05–C09, and let the steward take
the remaining contract decision to the owner:

1. **Explicit cooperative worker preparation:** private per-source creation,
   complete public exposure and precise adapters for known workers. This is a
   manageable asset implementation, but formerly working undeclared first-worker
   acquisitions need a changed contract. It is not approved here.
2. **Unchanged public/worker semantics:** eager assets before uncontrolled access,
   with separately qualified private graphic/sound work. This preserves current
   behavior but may erase broad creation deferral; it cannot close 5b as written.
3. **Deeper engine ownership:** investigate a backend that exposes fully valid
   objects while the engine owns residency/input completion. This would require
   new native/graphics integration, Unity lifetime/device handling and platform
   maintenance. No inspected runtime API supplies it; no backend or dependency
   is proposed as already proven. This needs a separately bounded design approval.

All options retain native quality, explicit opt-in/master-off, proper cancellation
and exact provider ownership. None silently omits PNG/JPEG/DDS, audio, workers or
public consumers. The capability remains open for the resulting implementation.
No independent private sub-increment was added merely to create a nominal hook:
the useful public graphics and sound creation boundaries depend on this decision.

## Engine-interface addendum — final bounded assessment

**No inspected supported API closes the blocked-main-thread contract by itself.**
A native resource experiment remains plausible for part of the work, but is not
a proven replacement backend. This is an API/ownership finding, not an assertion
that undocumented engine integration is impossible or legally prohibited.

Local ILSpy extracts `Unity-Texture.cs`, `Unity-Texture2D.cs`, `Unity-AudioClip.cs`
and `Audio-Manager.cs` are preserved under the C10 artifact directory. They come
from the shipped GOG player, not a newer Unity reference source. CoreModule hash
is `c5c58ea254834291780a1d6c388c241443d07167b4a4b890a23c9494f626ddba`;
AudioModule is `abc57ac9f40d0b5151c248726549f5470c338bbd03183d5146b61d486512b797`;
UnityPlayer is `e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96`.
The previously captured engine version is 2022.3.35f1, Direct3D11.

| Interface | Actual opportunity and remaining barrier |
|---|---|
| `Texture.allowThreadedTextureCreation` | Moves internal resource work to engine workers. It does not permit C# texture construction off the main thread. The shipped constructor still calls `Internal_CreateImpl`. [Unity 2022.3](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture-allowThreadedTextureCreation.html) |
| Engine asynchronous upload / mip streaming | The documented upload path streams built `.resS` data; `LoadImage` forces synchronous upload. Runtime `streamingMipmaps` is read-only. No inspected binding registers arbitrary DDS/PNG files as streaming sources. Mip reduction would also be C11's separate quality choice. [Upload pipeline](https://docs.unity3d.com/2022.3/Documentation/Manual/LoadingTextureandMeshData.html), [streaming API](https://docs.unity3d.com/2022.3/Documentation/Manual/TextureStreaming-API.html) |
| `CreateExternalTexture` / `UpdateExternalTexture` | Can wrap/switch a matching native D3D11 texture. Creation still enters the main-thread Texture2D constructor; the update binding supplies no documented cross-thread guarantee. Eager private wrappers might postpone GPU filling, but empty public wrappers remain forbidden. [External textures](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.CreateExternalTexture.html) |
| Native D3D11 resource creation | Device resource creation supports multiple threads; context submission requires synchronization. A plugin must not race Unity's immediate context or assume a private context can submit independently. This is a genuine native work boundary, not a Unity-object publication solution. [Microsoft threading contract](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-render-multi-thread-intro) |
| Native rendering event | `GL.IssuePluginEvent` queues a render-thread callback; it is not an independent main-thread pump or a promise of progress during every blocked call. Device initialization/shutdown and resource lifetime need explicit handling. [Native plugin interface](https://docs.unity3d.com/2022.3/Documentation/Manual/NativePluginInterface.html) |

CPU and GPU data are separate. The shipped `GetPixelData`/`GetRawTextureData`
return Unity CPU storage; a plugin GPU upload does not establish correct CPU
pixels. Unity explicitly documents that the copies can differ. A borrowed raw
pointer can also outlive its valid storage if the texture changes. Mask pairs,
readability, all mips and C07's frozen atlas inputs require full consistency;
ordinary native-unreadable DDS must retain that behavior. [CPU texture data](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.GetRawTextureData.html)

Bypassing managed upload calls can skip C08-observed hooks even if pixels match.
Creating wrappers earlier can expose them through constructor callbacks. A
backend therefore needs exact no-escape/side-effect admission and safe handling
of late hook publication; checking hooks only after a worker blocks is too late.
`GetNativeTexturePtr` can itself synchronize with the render thread, so obtaining
it at arbitrary worker demand is not a general escape. [Native pointer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture.GetNativeTexturePtr.html)

Audio is independent. Shipped `Manager.Load(Stream,...)` opens a decoder, obtains
sample/channel/rate metadata and calls `AudioClip.Create`; streamed clips already
read PCM through callbacks. `AudioClip.Create` still calls `Construct_Internal`
and `CreateUserSound`. The public streaming callback API could support a narrow
lazy reader, but creation/metadata would remain eager, callback timing may read
ahead, and seek/failure/stream disposal must match native. It does not defer clip
creation or prove all public `GetData`/duration behavior. [AudioClip.Create](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioClip.Create.html)

Pinned supplier source adds no proven engine solution: FGL's texture-downscaler
worker request waits on a main-thread event and returns null on timeout
(`ModContentLoaderTexture2D_LoadTexture_Patch.cs:249-284`). Its cache-hit path
constructs a temporary small texture then synchronously fills it with LoadImage;
that is not a worker-safe engine interface. YaOpt's selected completion does not install a
complete public CPU/GPU readiness backend. These are source findings; the native
backend and worker scenario were not executed against either supplier.

**Concrete possible proof, only with a new native-integration grant:** a private
Windows D3D11 prototype would precreate and retain unexposed Unity wrappers on
main, then finish one exact full-mip native payload using owned CPU/native GPU
work before publishing to a waiting worker. First establish plugin/device
bootstrap in this already-built player and a supported submission/fence route
that needs no further main-thread progress. Then demonstrate direct and reflected
holder reads, CPU/GPU equality, a color/mask pair and native atlas input, plus
cancellation/disposal, without callbacks being skipped or any incomplete object
escaping. Begin with an existing faithful C05 payload or authored DDS: PNG/JPEG
first-build mip generation/compression may still require native main-thread work.
Count wrapper construction/allocation as eager and only actual later input,
decode or upload as deferred. No speed scoring is needed for that proof.

This would require a new in-process native plugin and graphics resource/fence
ownership, unlike the existing out-of-process CPU helper. If public interfaces
cannot register the device or safely submit, stop before private engine ABI or
global context changes and return that exact additional decision. The strongest
uncertainty is complete CPU/GPU publication while main is blocked, with ordinary
Unity and late-hook semantics. An audio lazy-reader proof would be separate and
cannot be substituted by the graphics result. Even success is an increment:
all-format first builds, audio creation, broader consumers and C10 remain open.

**Recommendation:** present this narrowly falsifiable native proof versus the
explicit compatibility/scope alternatives above to the owner. Do not approve a
production backend, promise eventual broad coverage, or renew indefinite research.
No tool installation, native implementation, extra test or game launch occurred
for this addendum. Engineering blockers are identified without inventing a
licensing prohibition; any selected dependency's actual license would be reviewed.

## Verification and remaining obligations

`fixture.py test --test-filter FullyQualifiedName~OnDemandConsumerBoundaryTests`
passed two managed checks and 68 fixture-tool checks, GOG references, zero build
warnings/errors, no game launch. Final log is
`artifacts/ecosystem-next-20260910/c10-consumer-test-02.log`, results directory
`artifacts/fixture-tests/34998d6f00634946a5f6552298cee8af`. The first compile failed
on an ambiguous NUnit overload; that log is preserved and the test was corrected.
No performance test was selected.

Read-only endpoint verification matches all seven assigned hashes and three
absences (`artifacts/ecosystem-next-20260910/c10/endpoint-verification.json`).
There were no restoration transactions because nothing was deployed or prepared.
The test-created reusable MSBuild server was shut down. No fixture, test, helper
or child operation remains; the freshly identified normal Steam game and other
applications were left untouched.

There is no new deployable C10 core, observer or helper, no functional game capture
and no performance conclusion. Existing restored endpoints remain separate from
the source base. Full implementation, ordinary on/off behavior, real source demand,
public/worker/callback/lifecycle correctness, colony/menu/save-reload workflows,
C05–C09 nonzero coexistence and owner-assisted visual/audio inspection remain.
Later unattended controls must include usable colony entry, first-use stalls,
RAM/VRAM/stream lifetime and all preparation/validation costs. Prior C01–C09
measurement obligations and C08 carried in-game inspection remain unchanged.
