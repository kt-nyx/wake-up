# C10 prepared native audio experiment

## MP3 decoded-frame prototype - 12 September UTC

The [new prototype record](c10-mp3-frame-cache-prototype.md) reports actual
persistent reuse in the ordinary LightSMG clip: 1 native frame conversion and
58 cached frames, with full native sample equality and normal disposal without
catch-up. General admission remains closed because arithmetic-setting changes
still need a correct retained-history boundary. This is intermediate functional
evidence, not MP3/C10 acceptance. The [earlier design checkpoint](c10-mp3-decoded-reuse-design.md)
and existing index/completion/readiness evidence below remain preserved.

## What now works, and what remains open

The experiment records the bytes that RimWorld's own OGG decoder or PCM/float WAV reader supplies during
content loading. A later matching load supplies those bytes from a private cache,
allowing the real Unity audio clip to finish creation before the decoder exists.
The original decoder is constructed when later reading needs it. Replaying its
previous operations puts it at the same state as ordinary loading.

This is an isolated experimental implementation. It requires the explicitly
selected early-loader experiment; normal product defaults do not enable it.
It is not full C10 acceptance, proof of a speedup, or approval to ship Doorstop.
Natural streaming playback, two ordinary short OGG sounds and an ordinary PCM WAV
sound now have the bounded functional evidence recorded below. Textures, graphics/icons, other-format audio,
broader sound/public/reflected/worker consumers and gameplay coverage remain required. Native
clip creation itself is eager. The source is still fully hashed, cache bytes are
read, and later reconstruction replays earlier work; none of those costs is free.

## Boundaries and ownership

The [earlier-loader proof](c10-preloader-proposal.md) installs the Harmony mutation
guards before original game/mod execution and before codec execution exposure.
Its BCL-only owner survives Prepatcher's assembly replacement. A separate registry
serializes relevant mutations and settles every pending reader before Harmony
can call arbitrary patch factories or replace code. Once a foreign relevant
mutation occurs, new loads stay native for the remainder of that process.

Only the lower OGG `WaveStream` is replaced. The actual native file stream,
`CustomAudioFileReader`, `SampleChannel`, Unity `AudioClip` and callback ownership
remain. Byte reads retain offsets/counts/short reads and native position behavior;
the upper native reader still performs byte-to-float conversion and volume work.
Cache identity includes source contents, pinned native bodies/codec identities and
the versioned byte transcript. Optional misses or unusable entries load natively.

Concrete `FilesystemFile.CreateReadStream` observation identifies the original
`FileStream`, including its original sharing mode. A `FileStream.Dispose(bool)`
prefix settles matching pending readers before the original disposal body. This
preserves the holder's per-item disposal order without patching a generic holder.
Private decoder cleanup follows actual Unity destruction and contains private
cleanup errors; original raw-stream disposal errors are not swallowed.

Public clips remain real native objects. The internal lower reader type changes
while the experiment owns it; arbitrary inspection of private decoder fields has
not been qualified. The tested public/native output checks must not be presented
as proof that every reflected or extension consumer is compatible.

## Captured functional evidence

All runs use the isolated GOG generation
`gog-rev573-20260910-194017-cf1a1eb0`, activation workload, functional purpose,
baseline core selection, muted preferences, minimized nonactivating launch,
independent menu observation and normal exit/capture. The normal game is untouched.
Timings are diagnostic only. Captures live under `.rlo-test-instance/results/`.

| Run | Exact source/core/observer/bootstrap | Result |
|---|---|---|
| `c10-prepared-01` | `e7e4dc60002242b3580742875c8e6359d0527721` | Failed feature check before admission; normal exit. Cleanup transpiler found more than one disposal call, and the partially installed closed generic loader hook incorrectly routed a texture through audio. |
| `c10-prepared-03` | `78c2ffff7d26a82e35d0073791ea31c7c537ba05` | Native first-build recording, metadata/sample capture, original stream identity, holder clearing and end-frame cleanup passed. |
| `c10-prepared-04` | same `78c2fff` packages and bootstrap receipt | Warm native return, later demand, late patch-factory settlement, native control/sample equality and cleanup passed. |
| `c10-prepared-05` | `f9907b554b57e83afc32d504e067ab2740fdce70` | Final native recording and cleanup passed. |
| `c10-prepared-06` | same `f9907b5` packages and bootstrap receipt | All warm checks plus still-pending native holder disposal passed. |

The failed method is not retried: Mono shares reference-type generic machine
code, so Harmony hooks on `LoadItem<AudioClip>` also affected texture loading.
All closed generic runtime hooks were removed. Setup failure now removes partial
own hooks. The revised nongeneric route produced no corresponding loading errors.

Each of two warm returns served **307,200 cached bytes** without constructing a
decoder. The first ordinary read/seek constructed its decoder. The second pending
decoder was constructed before the late Harmony prefix factory executed; the
factory observed zero pending entries and permanent native fallback. A third
control used the actual original lower Vorbis reader. All three matched native
clip metadata and full float-buffer hashes at the tested positions. Replay
mismatches and private cleanup failures were zero; all original streams closed
and clips were destroyed. These are causal/correctness counts, not performance.

The original decoded format was stereo 48 kHz with 3,209,136 clip samples. The
fixture input is `Sounds/Strange_Feeling.ogg`, 1,086,292 bytes, SHA256
`2f70c7ddd156d1379d0ddbb8df1f959fec1ea0df28bd62a30ae9dd2371d5f371`.
Only validation selects that input; product admission has no mod/file allowlist.

Thirty focused managed tests and 72 existing Python fixture checks passed after
the nongeneric correction (record `70f153ada9a547c18a06702fbafcb351`). Packages built
with zero warnings/errors. Independent read-only inspection confirmed that native
streaming construction does not dispose the source before the load finalizer
publishes lifetime ownership; failure disposal happens after that finalizer.

## Final pending-disposal check and stopped handoff

`c10-prepared-06` adds a separate native holder whose decoder remains pending after
clip return. Its original `ClearDestroy` call constructs that decoder, decreases
pending entries by one, closes the original file, and leaves cache-byte counts,
mutation epoch and native-fallback state unchanged. The clip is destroyed by the
later native frame. This disposal-only case supplies no sample comparison; its
receipt explicitly excludes it from that claim. The other three cases still
match native samples/metadata. `pendingDisposalPassed`, `factoryDrainPassed`,
`sampleComparisonPassed`, `functionalPassed` and the captured menu/bootstrap/
preloader/prepared-audio checks all pass. Every tested process exits normally.

Source/core/observer/bootstrap for final captures 05/06 is
`f9907b554b57e83afc32d504e067ab2740fdce70`. The final small source correction avoids
locking private lifetime state for streams without the native source marker.
Thirty managed and 72 Python checks from the preceding nongeneric implementation
are retained; final matching packages build without warnings/errors and the
actual final cold/warm fixture runs supply the added lifecycle evidence.

All fourteen prepared-audio deployment/preparation transactions were restored
after capture. Seven original endpoint hashes, five prior absences and four new
bootstrap absences match; `fixture.py audit` passes. Evidence is
`artifacts/ecosystem-next-20260910/c10/preloader/after-prepared-endpoints.json`.
No fixture process remains. Normal Steam game PID 50776 was independently
identified at its normal installation and was not changed.

Ownership returns stopped to the steward for independent review of this narrow
implementation checkpoint. The same C10 orchestrator remains responsible for
corrections and remaining coverage; no successor is dispatched. The next product
work is wider native audio/worker/gameplay qualification and the separately open
texture publication boundary. This audio result neither solves that boundary nor
permits a changed compatibility contract or production preloader adoption. No
performance testing, normal data changes, Steam/Deck promotion, merge or
publication occurred.

## Steward review: focused corrections required — 12 September UTC

The steward reviewed clean stopped handoff `addd2b40f87824f032141d6f0f1e01c5fe1a4b70`, with two independent read-only source reviews. Final tested source/core/observer/bootstrap is `f9907b554b57e83afc32d504e067ab2740fdce70`. The earlier-loader capture remains separately attributed to `234799120ab9ce3622803b27b1b943b437536efc`.

The demonstrated zero-start streaming OGG behavior is supported: cached lower reader bytes preserve native conversion and sampled output; ordinary demand and a known late patch factory settle readers; still-pending native holder disposal closes the original borrowed file normally. The steward rehashed core, observer, bootstrap and proxy binaries against all three capture manifests (`c10-preloader-01`, `c10-prepared-05`, `c10-prepared-06`), checked audio/bootstrap evidence hashes and functional pass/normal-exit fields, and independently verified seven restored endpoint hashes and nine absences. Details: ignored `artifacts/ecosystem-next-20260910/c10/preloader/steward-prepared-review.json`. No fixture game is running; the identified normal Steam game and separate DeckHost process were not operated on.

Four corrections are required before widening this increment:

1. **Additional Harmony copies:** `Entrypoint.AssemblyLoaded` records another `0Harmony` assembly without closing admission or settling pending readers. Runtime verification covers its referenced replacement only. Enforce the finite original/replacement assembly identity contract promised by the preloader proposal, including late unexpected copies, before they can bypass the guarded mutation entries. Do not expand this into a general patch-management framework.
2. **Recursive decoder initialization:** `PreparedAudioWaveStream.EnsureNative` calls hookable `LoadingObservationRuntime.Begin` while holding the reader lock and before assigning the native reader. A prefix that installs a decoder patch can re-enter settlement on that same unfinished reader. Move observation outside the unsettled critical path or protect the exact reachable boundary; a reentrant lock is insufficient. Keep terminal errors and ordinary callback semantics intact.
3. **Starting position:** `CreateReader` saves source position for reconstruction but excludes it from cache identity. Explicitly require zero-start admission with native fallback for other positions, or include the starting position in the transcript identity and qualify it. Existing zero-start evidence does not establish other positions. This is an increment boundary, not an exclusion from broader C10 coverage.
4. **Gameplay lifetime:** cleanup currently runs only through `Root_Entry.Update`. Add bounded cleanup during gameplay and verify reader/clip ownership through game entry, use, destruction and return/reload as applicable. Preserve ordinary stream ownership and avoid retaining destroyed clips for the rest of a colony session.

The marked-file `FileStream.Dispose(bool)` hook has process-wide installation but acts only on exact streams recorded at the native file creation seam. This is a limited ownership hook, not permission for general stream virtualization. Retain evidence that unrelated streams are unaffected; the removed closed-generic hooks must not return under the same failed Mono premise.

Return these corrections to the same C10 orchestrator from the exact steward review commit. Then continue useful audio coverage and lifecycle work under the approved isolated experiment, returning a stopped, reviewable increment. Keep validation focused on concrete risks and actual behavior. The single observer-selected OGG file, direct native reader calls and menu captures do not establish broad natural audio use, audible playback, worker/reflection compatibility, texture/graphic/icon deferral or release integration. Production Doorstop adoption remains a later material decision. C10 functional acceptance and all performance/release gates remain open; no C11 successor yet.

## R1 corrections and colony evidence — 12 September UTC

Resumed from exact clean steward base `066c73e183ab1bcaa24774ca27d1cdf49f6b69a4`
under action `wake-up-c10-prepared-r1-066c73e-20260912`. The same owner made the
four reviewed changes and extended actual native audio consumers into a colony.
Final tested source/core/observer/bootstrap is
`0ddd25aae11647a7548179de07d9c8b8ecffb1e0`. This is a correction/coverage handoff,
not C10 acceptance or production preloader approval.

### What changed

1. **Additional Harmony copies:** the bootstrap registry binds the original and
   one provisional Prepatcher replacement by exact `Assembly` object references.
   The original must arrive through the configured initial load; the replacement
   must follow the observed rewrite protocol while admission is closed and empty.
   Runtime verifies its exact replacement reference and emitted guards before
   opening admission. An unexpected executable copy closes admission permanently
   and settles pending readers synchronously, outside the diagnostic lock/catch.
2. **Recursive initialization:** removed the `LoadingObservationRuntime.Begin/End`
   pair from unfinished decoder reconstruction. Native construction, byte replay,
   terminal error identity and partial-native disposal remain. Other C13 scopes
   are unchanged; this unsafe per-reconstruction scope is no longer reported.
   Its regression uses real Harmony hooks and the actual bootstrap registry to
   demonstrate that a diagnostic hook capable of publishing a decoder patch
   cannot cause duplicate reconstruction. The attack is separately exercised
   after settlement so an inert test setup cannot pass.
3. **Starting position:** both admission and optional cache-key generation refuse
   nonzero initial positions without reading/rewinding that stream. Those loads
   use the original native decoder at their existing position. Tests retain
   position and zero hash-byte counts on refusal; zero-start cache evidence stays
   applicable. Other positions are a remaining coverage boundary.
4. **Gameplay lifetime:** cleanup now hooks base `Root.Update`, called by both
   `Root_Entry` and `Root_Play`, rather than menu-only updates. The live probe
   requires destroyed private readers to be gone during the colony, before the
   observer disposes any remaining native reader references.

### Actual functional results

| Capture | What passed |
|---|---|
| `c10-prepared-r1-cold-01` | Three actual native OGG recordings; native samples/metadata; original streams; destruction and cleanup. |
| `c10-prepared-r1-gameplay-02` | Warm creation in a new colony; worker reader demand while main waits; native song/content and music-manager consumers; late unexpected Harmony load; pending disposal; gameplay cleanup; paused and unpaused native save/reloads. |

Both captures use the same final packages and bootstrap receipt. Independent menu,
normal exit code 0, bootstrap/preloader and prepared-audio checks pass; the colony
capture also passes `gameplayTestPassed`. Thirty-four focused managed tests and
72 existing fixture-tool tests passed under record
`3783c2208fab4101bf09643f6276acf7`; final matching packages build without warnings
or errors. Two compile-only nullable/delegate issues were corrected before live
work. No additional failed live R1 run is omitted.

The three files are `Strange_Feeling.ogg`, `Spacekattommy.ogg` and `The_Fallen.ogg`
from the existing music pack. They cover stereo 48 kHz and 44.1 kHz recordings
and different lengths. Warm native returns serve 307,200 / 282,240 / 307,200 bytes
respectively with no private decoder construction. Subsequent native read/seek
sample hashes and clip metadata match each file's ordinary native control. The
worker performs these native reader operations without main-thread pumping.
These are correctness/causal observations, not cost or speed measurements.

The colony check temporarily publishes a test-owned clip in the observer's real
content holder. Native `SongDef.ResolveReferences` and `ContentFinder` retain it;
direct holder access and reflected public holder/song fields return the same clip
on worker thread 22 while main thread 1 waits. The native music manager accepts
that song, retains the actual `AudioSource.clip`, reports its native duration,
and stops through its ordinary method. Master volume was already zero. This tests
native consumer paths using controlled assets, not natural loading of an entire
content mod or audible output. Only temporary observer entries/references are
removed; unrelated holder entries remain.

A separate unmarked `File.OpenRead` disposal leaves pending readers and decoder
counts unchanged. Still-pending holder disposal reconstructs before closing its
original borrowed stream. Later `Assembly.Load` of another original Harmony image
returns only after the last pending decoder has settled; a subsequent known patch
factory sees no pending readers. At gameplay cleanup, readers/pending/mismatches/
private cleanup failures are all zero. The 75-cell colony with three colonists
successfully saves and reloads through both native pause policies. Existing
`InputLegacyModule` reflection-only scene warnings also occur in earlier
`c02-save-smoke-07` and `wfq-default-smoke-02`; they are not a clean-log claim or
a new audio failure. Visible/focus and audible-quality qualification were not run.

### Precise remaining boundary and restoration

The unexpected-copy guarantee covers the synchronous loading caller. An
`AssemblyLoad` event does **not** establish a global barrier against another
thread discovering the already-loaded assembly through `GetAssemblies` during
settlement. Closing that stronger pre-publication race would require a different
loader boundary; no universal loader/patch framework or narrowed full-C10
compatibility contract was adopted. The initial bootstrap observation is captured
before the intentional late-copy challenge; the audio receipt separately proves
the resulting native-only transition. Do not read the initial bootstrap pass as
proof that the later unexpected copy became supported.

All five R1 transactions are rolled back. Seven original hashes and nine absences
match, and `fixture.py audit` passes. Endpoint evidence:
`artifacts/ecosystem-next-20260910/c10/preloader/after-prepared-r1-endpoints.json`.
Normal Steam PID 50776 and local DeckHost PID 48204 were freshly identified and
left untouched; no fixture process remains. Both bounded children are stopped.
Ownership returns to the steward for independent review, with corrections and
remaining C10 scope returning to this same owner. Broader sound/format/modlist,
private-reflection, arbitrary-worker/loader, texture/DDS/PNG/JPEG/graphic/icon,
production integration and performance obligations remain. No successor, merge,
push, publication, normal-data change, Deck access or performance run occurred.

## Steward R1 review and next audio increment — 12 September UTC

Clean stopped handoff `1c3ff465178f6448f0c71f38a54821d422981280` was reviewed against final tested source `0ddd25aae11647a7548179de07d9c8b8ecffb1e0`. The steward verified both run manifests, source-matched core/observer/bootstrap/proxy DLL hashes and relevant audio/bootstrap/gameplay receipt hashes. Metadata and sampled byte signatures match the native controls for all three assets. Seven original endpoint hashes and nine absences were independently rechecked after restoration; no fixture process remains. Ignored verification record: `artifacts/ecosystem-next-20260910/c10/preloader/steward-prepared-r1-capture-review.json`.

The steward reviewed finite Harmony identity handling, the removed unsafe diagnostic callback and its real Harmony regression. Independent source review also verified nonzero-position refusal, both native roots calling the shared cleanup hook, worker demand without main-thread pumping, and preserved public clip identity. No blocker was found in those bounded corrections. This supports retaining the corrected experimental increment; it is not full C10 functional acceptance or performance approval.

Two limits determine the next work. First, the music test publishes an observer-owned clip and explicitly reconstructs its decoder through worker reads before playback; immediate muted start/stop does not prove first-use reconstruction through Unity playback or sustained streaming. Second, synchronous extra-Harmony settlement does not close concurrent discovery of that assembly during its load callback. The finite loading-caller guarantee is reviewed; the stronger compatibility requirement remains open, with no owner-approved exclusion.

The same C10 owner continues from the new exact steward review commit to implement and demonstrate automatic admission through ordinary mod content loading, followed by actual native first-use playback and existing lifecycle/prewarming integration where needed. Keep the experiment isolated and on-demand off by default. Use source-defined natural routes and minimal additional observation; do not enlarge manual sample matrices as a substitute for activation. Broader sound/format coverage needs a concrete implementation path, and texture/graphic/icon coverage remains required. Production preloader adoption is still a later material decision. Do not repeat known losing generic hooks, exposed incomplete texture placeholders or late historical patch inference under unchanged premises. No successor, performance test or supplier-removal claim follows this review.

## Natural loading and first playback increment — 12 September UTC

Ordinary mod loading now records and reuses short OGG sounds through RimWorld's
existing background worker. The final matching cold/warm runs also demonstrate
that a naturally loaded streaming song first constructs its private decoder when
actual native playback requests more data. No observer-created clip, temporary
song definition, manual reader demand, changed streaming flag or earlier mutation
barrier supplies this proof. Full C10 remains open and normal defaults stay off.

### Implementation and exact tested source

RimWorld chooses streaming for source files larger than 307,200 bytes. Its queued
`ModContentPack.ReloadContentInt` runs after the Wake-Up constructor installs the
existing native reader boundary, so P-Music needs no additional activation hook.
The new nonstreaming path retains native `Manager.Load` flags, its decoder worker,
float conversion, `SetData` queue, source disposal and public loading-state order.
A nongeneric outer `CustomAudioFileReader.Dispose(bool)` prefix freezes the worker's
completed byte/position record; its finalizer publishes only after original disposal
succeeds. Reader ownership is associated before its factory returns, including
when the native worker finishes before the `Manager.Load` finalizer. Synchronous
nonstreaming loads freeze at return. Streaming/nonstreaming cache identities are
separate; older experimental streaming entries miss once under the new identity.

Runtime implementation is `607e5246c9d41ca02e8ee3d25d4fcdb00cae6c13`, with core and
bootstrap packages built from `23bc9545cd51e63c4722eff5e4bf3a9966565a6b`. Final observer
source/package is `ccc8d6fa326e4e6a05dec9f14f297e43ae59174e`; intervening changes affect
only the observer. Both final runs use identical core/observer/bootstrap receipts.

| Final binary | SHA-256 |
|---|---|
| WakeUp.dll | `0dea8baa8174289d1e393086f9a77e57f011501b349a8abb7efe3feb785b1120` |
| FixtureMenuObserver.dll | `57f8da77fa8fea962b7a4e2c7864c0582a357031bf6030a3abd246a9130a364c` |
| WakeUp.AudioBootstrap.dll | `be2ece6d4fd27f904616e7476c43286ed77c0d787fa376c7d221611a5697c2d2` |

The official Doorstop proxy and pinned GOG references retain their prior identities.
Bootstrap session is `5d40beecd52246b492ab52abd377d4d1`. The fixture selection keeps
Prepatcher/Harmony, Core and all five official expansions, Dubs Skylights and
P-Music, in frozen relative order. Neither relevant loading supplier appears in
the 309 frozen package identities or 308 actual fixture About files (the latter
includes the two validation mods and excludes the three previously parked mods).
This is absence evidence for this workload, not a supplier removal or universal
compatibility claim. Selection and actual metadata are preserved in
`artifacts/ecosystem-next-20260910/c10/natural-audio/content-identities.json`.

### Captured behavior and limits

Final captures `c10-natural-cold-04` and `c10-natural-warm-05` pass independent menu
observation, preloader/bootstrap, natural audio, gameplay save/reload and normal
exit code 0. The workload contains 56 P-Music clips and two short glass clips.
Cold loading publishes 57 records; one duplicate short sound legitimately reuses
its identical record within that first startup. Warm loading has 58 hits, no
misses, zero private decoder constructions and no replay mismatches at the menu.

The existing `A_Place_of_Our_Own` SongDef and its original mod holder retain the
same real clip. Immediately before `Find.MusicManagerPlay.ForcePlaySong`, its warm
reader still has zero decoder constructions. Playback first reconstructs on thread
96 through `Read` (main is thread 1), then supplies further successful bytes on
later frames. The final observation contains 39 reads and 614,400 returned bytes,
including 307,200 cached bytes and 307,200 replayed historical bytes, with zero
replay mismatches. These are byte/count observations, not measured benefit.
Callback origin follows the unchanged native music/Unity callback chain and the
absence of observer reader/seek/settlement calls; no direct Unity callback hook
was installed. The receipt states this inference explicitly.

Both naturally loaded short clips use native worker threads 67/68. Each supplies
147,644 cached bytes without constructing a decoder and reaches native
`Manager.GetAudioClipLoadState == Loaded`, after the original upload queue. This
proves native worker decode reuse for these sounds, not first-playback deferral
for nonstreaming clips. All original holder references and the selected SongDef
clip survive the paused and unpaused save/reloads with native Loaded state.

Public clip metadata and startup operation signatures match across all 58 clips.
The first 41 common playback operations also match, including read/seek arguments,
return values and sampled output signatures. Each signature covers at most the
first 256 returned bytes; it is not a full-waveform or audible-quality comparison.
Detailed receipts, their hashes and exact package records are in
`artifacts/ecosystem-next-20260910/c10/natural-audio/final-paired-correctness.json`.
Master preference and `AudioListener.volume` are zero throughout playback; the
native music source volume is independently 0.28. No speed, stutter, audible
quality or universal semantic-compatibility claim is made.

Thirty-five focused managed tests and 72 existing fixture-tool tests pass under
`artifacts/fixture-tests/c51008b0b2a04128af9f00469dd9d86c`. The initial observer
build needed two nullable annotations. `c10-natural-cold-01` exited normally but
failed its incorrect source-volume-zero assertion; native `PrefsData.Apply`
instead mutes the master listener. Corrected `cold-02`/`warm-03` passed. Independent
bounded review then found that attempted read counts could falsely count throwing
reads as playback progress. The final observer requires successful byte growth
across frames and clean retained read results; final `cold-04`/`warm-05` pass that
stronger predicate. Existing InputLegacyModule reflection-only warnings remain;
no clean-log claim follows these captures. No other live attempt is omitted.

### Concrete next format path and stopped handoff

PCM/float WAV is the next implementable format. Extend the same native factory
boundary to the exact `new WaveFileReader(Stream)` constructor, version the cache
record, retain native encoding/bit depth/block alignment/byte rate, and include
AudioFormat in its key. Ordinary PCM 8/16/24/32-bit and float 32/64-bit with no extra
format payload can then retain native SampleChannel conversion and exact byte/
position replay. Compressed, extended or unrecognized formats keep native fallback.
Do not infer WAV positions: its reader clamps and aligns them to native blocks.
This path is specified, not implemented or qualified by the OGG captures.

MP3 uses the neighboring `Mp3FileReader(Stream)` constructor, which builds its
frame index and Windows ACM decoder. It also requires PCM metadata support and
identification of the actual native ACM provider; pinned NAudio alone does not
identify that provider. Preserve native indexing/seek/disposal rather than adding
an alternative decoder. Broader audio, texture/DDS/PNG/JPEG/graphics/icons, worker/
reflection/prewarming and the stronger concurrent-loader guarantee remain C10
requirements. Production preloader adoption and measured performance remain open.

All ten assignment transactions are rolled back. Seven original hashes and nine
original absences match in `natural-audio/after-endpoints.json` under the artifact
folder above, and `fixture.py audit` passes. Normal Steam PID 50776 and local
DeckHost PID 48204 were freshly identified and left untouched; no fixture process
remains. Both bounded children are stopped. Checkout/fixture ownership returns
to the steward for independent review; corrections return to this same C10 owner.
No successor, parent timer change, merge, push, publication, Steam promotion,
Deck access, normal-data change or performance run occurred.

## Steward natural OGG review — 12 September UTC

The steward reviewed clean stopped handoff `5c04cd00f679d24c4ebd684e2e6255879f75fec0`, with independent review of the new nonstreaming ownership/disposal/publication and trace code. No concrete blocker was found in this bounded natural OGG increment. Ownership is registered before worker execution; a transcript is frozen before native disposal and published only after it succeeds. Native workers, conversion, upload/load-state and source ownership are retained. Fully served nonstreaming transcripts require no decoder construction on disposal.

The steward independently rehashed both final run manifests and captured audio/bootstrap/gameplay receipts against their inventories, and checked core `23bc9545`, observer `ccc8d6fa` and bootstrap `23bc9545` package DLL hashes. No product/bootstrap source changes follow the built core in the handoff. All 58 public metadata/startup sample traces and 41 common playback operations agree between final cold/warm captures. Warm selected-song construction is zero immediately before native play and one afterward on a non-main thread, with successful returned-byte growth across later frames. Observer source reads existing holders/SongDefs and calls native play/stop; it creates no substitute clips and does not pre-demand reader data. Callback origin remains the documented inference, and sampled signatures do not establish full-waveform or audible equality.

Seven restored endpoint hashes and nine absences were independently verified, with no fixture game running. Normal Steam and the separate local DeckHost remain untouched. Private review artifact: `artifacts/ecosystem-next-20260910/c10/natural-audio/steward-review.json`.

Retain this working experimental OGG increment. Short nonstreaming sounds reuse decoding during native background loading; they are not thereby on-demand first-playback sounds. Existing transcript bounds, synchronous storage/tracing costs, wider workloads, complete sound readiness, other formats, textures/graphics/icons, stronger concurrent-loader compatibility, production integration and performance remain open. Full C10 functional acceptance is not granted and C11 is not dispatched.

Next, the same owner extends native format metadata and exact factory boundaries to ordinary PCM/float WAV, with native conversion and block-aligned seek semantics preserved. Use focused native comparisons and actual content consumption; keep unimplemented compressed/extended formats as explicit native fallback boundaries, not approved exclusions. Investigate the actual Windows ACM provider prerequisite for the subsequent MP3 implementation without building a new decoder framework. Performance remains prohibited; GOG correctness testing remains authorized.

## PCM/float WAV increment and actual MP3 provider — 12 September UTC

WAV content now uses the same optional record/replay path while keeping RimWorld's
native conversion, background worker and Unity upload. The final cold/warm runs
load and play Allow Tool's original WAV through its existing SoundDef. OGG
regression checks also pass. This is a working experimental format increment,
not full C10 acceptance, a speed claim or production preloader approval.

### Representation and native behavior

Source `f17309fd0b3ba96cf8f4c2feaed754bafd881238` adds the exact nongeneric
`WaveFileReader(Stream)` constructor boundary beside the existing OGG constructor.
The original `CustomAudioFileReader` format switch, `SampleChannel`, streaming/
background/source-disposal flags, worker and `SetData`/Loaded sequence stay native.
No alternative decoder or new end-user dependency is introduced.

Transcript v3 stores encoding, bit depth, channels, sample rate, block alignment
(the bytes per sample frame), byte rate and extra-format size explicitly. The
warm reader reconstructs that exact supported tuple. Admission covers PCM
8/16/24/32-bit and IEEE float32/64 with canonical block/byte rates and zero extra
payload, including both 16-byte and 18-byte/cbSize-zero format chunks. Cache identity
includes v3, AudioFormat, streaming mode, source bytes and the pinned native code.
Old v2 entries become misses; malformed records retain ordinary fallback. Native
position results remain recorded operations, including WAV's actual negative,
aligned, clamped and end-of-file behavior; no replacement cursor is inferred.

For WAV, the diagnostic `decoderConstructions` counter counts lower WAV reader
construction. Reusing its native bytes does not eliminate SampleChannel conversion
or Unity upload. Nonstreaming reuse occurs during ordinary background loading,
not first sound playback. Compressed/extended formats and inconsistent metadata
abandon only optional recording, leaving original properties/readers/errors intact.
Compressed/extended WAV has lower-reader fallback checks here; no live converted-subtype
or streaming-WAV coverage is claimed by these PCM16 short-sound captures.

Sixty-five focused managed tests and 72 fixture-tool tests pass in
`artifacts/fixture-tests/1d8d7228f3ce481f99f0398ebe48899b`. Tests use the fixture's
actual NAudio WaveFileReader and SampleChannel for all six representations in mono
and stereo: bit-exact converted floats and untouched tails, byte reads, native
seek/error recovery and reconstruction. Additional cases cover nine unsupported
or inconsistent headers, cbSize-zero disposal without construction, and a real
v2 payload in a valid store envelope becoming a miss. These are original-reader
checks on the offline test host; live Unity format coverage here is PCM16 stereo.

### Final functional evidence and exact identities

Core/bootstrap packages are from `54fa1aaefe6e2f116aba09e9155746112838db08`;
final observer package is `eaae49152624838cc5ef5815283700d1618d7947`.
Changes after the core build affect only observer validation. Final captures
`c10-wav-cold-02` and `c10-wav-warm-03` share identical package/bootstrap receipts
and pass independent menu observation, audio/bootstrap/preloader checks, native
paused/unpaused save-reloads and normal exit code 0. Session:
`4218f80f107a403e8133c9c4f9669851`.

| Final binary | SHA-256 |
|---|---|
| WakeUp.dll | `05bc47a130d50b859ebd97e33765844901e5dad6501027af6619c1be366c22e4` |
| FixtureMenuObserver.dll | `bc4c5bc7cfd268a81c4d7c6cb35d25d834090cc35eb0da35ff236f2e817e2bc8` |
| WakeUp.AudioBootstrap.dll | `04329b5a481227f5bf5b06094302ffb02521dab2c8aa8bcc4a50eb3ca3b2a6a2` |

The GOG reference and official Doorstop proxy retain their prior exact identities.
The frozen natural selection adds HugsLib and Allow Tool to the earlier OGG
workload, preserving their required order. `metalGlint.wav` is the unmodified
145,774-byte fixture source, SHA-256
`335e3d176ddeb39a68dc60d1d85d3d2cc30027c3ff66ac1f9a580599a29860a4`:
PCM16 stereo, 44,100 Hz, four-byte blocks, 176,400 bytes/second and 145,728 data bytes.

All 59 clips have matching public metadata and sampled startup-operation traces
between cold and warm. Warm startup has 59 hits, no misses and zero lower-reader
constructions. Native WAV worker thread 81 supplies all 145,728 recorded bytes
without constructing WaveFileReader; native loading state is Loaded. Existing
`AllowToolMetalGlint` plays the original clip via `SoundDef.PlayOneShot`. The
observer sees playback beyond 90% of its native duration, then sees its sample
leave the native manager. That removal does not identify the completion reason;
no uninterrupted-completion or audible-quality claim follows. Original holder
and SongDef references survive save/reload.

The OGG song remains unconstructed immediately before native music playback and
first reconstructs through Read on thread 110, with later-frame successful byte
progress and zero replay mismatches. Forty-one common playback operations match
cold/warm. Sampled signatures cover at most 256 bytes per read, not full live
waveforms. Master preference and listener volume stay zero.

Raw evidence, selected source metadata, final package/receipt hashes and comparison:
`artifacts/ecosystem-next-20260910/c10/wav/final-paired-correctness.json` and
`natural-wav-source.json`. Initial `c10-wav-cold-01` also passed but predates the
near-end/removal wording correction. Independent bounded review found no WAV
runtime blocker; it identified that manager removal alone could reflect voice
interruption, which motivated that correction. Two ambiguous NUnit delegate calls
and one observer nullable annotation were corrected before their successful builds.
No other live attempt is omitted. Existing InputLegacyModule reflection-only
warnings remain; this is not a clean-log claim.

### MP3 provider prerequisite and concrete remaining paths

The MP3 control identifies the decoder actually selected by Windows's Audio
Compression Manager (ACM). It constructs the unchanged native Mp3FileReader over
the frozen `Sounds/Things/LightSMG.mp3` source, then queries its live conversion
stream before disposing the reader and its borrowed input. It runs after natural
playback, has no sample reads/playback, and does not claim ordinary mod activation
or cached/deferred MP3 coverage.

Both final captures identify **Fraunhofer IIS MPEG Layer-3 Codec (decode only)**,
manufacturer 172/product 9, driver version value 17367441. Native output is PCM16,
mono, 48,000 Hz, block alignment 2 and byte rate 96,000. The actual chain is
`Mp3FileReader.decompressor -> AcmMp3FrameDecompressor.conversionStream ->
AcmStream.streamHandle`; its explicit driver handle is zero because the native
path lets ACM choose. `acmDriverID` accepts this stream handle, and driver-details
query results are successful. [Microsoft driver identification](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriverid)
and [driver details](https://learn.microsoft.com/en-us/windows/win32/api/msacm/nf-msacm-acmdriverdetailsw).

The descriptor does not establish the selected DLL's binary hash. Captures include
registered drivers and loaded `.acm` file hashes as explicitly separate candidates;
`selectedBinaryIdentified` stays false. Do not cast the ACM identifier into
GetDriverModuleHandle: that API needs a different handle from OpenDriver.
[Microsoft handle contract](https://learn.microsoft.com/en-us/windows/win32/api/mmiscapi/nf-mmiscapi-getdrivermodulehandle).
A previous selected descriptor also does not prove what future automatic
selection will choose before a deferred constructor exists. MP3 remains ordinary
native behavior while this admission prerequisite is unresolved.

A concrete next increment can separate provider-independent MP3 indexing from
provider-dependent PCM reuse. Native `CreateTableOfContents` runs before decoder
construction and fills `Mp3Index` file/sample positions, byte/sample counts and
`totalSamples`. On unchanged source bytes and exact NAudio/patch identity, record
and restore that successful table and native terminal input position at this
nongeneric method boundary; preserve the surrounding constructor, native frame
validation/fallback, current provider selection, duration/bitrate calculation and
ordinary reader disposal. This is a proposed bounded implementation path, not
implemented or measured here. Full deferred PCM reuse additionally needs a reliable
selected-binary/selection-policy check before serving cached samples, plus native
seek/reset/replay validation; seeking uses earlier frames and buffered leftovers,
so cached byte position alone is insufficient. No replacement decoder is proposed.

For omitted WAV subtypes, preserve a bounded lossless native format record rather
than discarding extra bytes/subformat information, reconstruct it with the native
format parser, and retain the existing `WaveFormatConversionStream.CreatePcmStream`
and `BlockAlignReductionStream` branch. Raw compressed-byte reuse need not defer
the ACM conversion itself; cached converted output would require the provider
identity work above. Exact extended-format/marshalling and original native failure
checks must accompany that extension. These remain open capabilities, not accepted
permanent exclusions.

All seven assignment transactions are restored. Seven original hashes and nine
absences match `wav/after-endpoints.json`, and fixture audit passes. Normal Steam
50776 and local DeckHost 48204 remain untouched; no fixture process or child work
remains. Ownership returns stopped to the steward for independent review, with
corrections assigned back to this same C10 owner. Broader audio readiness, textures/
DDS/PNG/JPEG/graphics/icons, worker/reflection/prewarming, concurrent unknown-loader
compatibility, production integration and performance remain required. No C11
successor, parent timer/state change, performance test, normal-data/Deck/UI change,
Steam promotion, merge, push or publication occurred.

## Steward PCM/float WAV review — 12 September UTC

Clean stopped handoff `dc9581809c85701cd8dc2bdf29cdeee2c9f233c4` passed bounded WAV review. The steward reviewed the exact native factory rewrite, format keys and observer behavior; independent source review found no blocker in v3 format preservation, native conversion/seek/error behavior, ownership or OGG compatibility. Actual WaveFileReader/SampleChannel tests cover six supported representations in mono/stereo, reconstruction and real v2 cache migration. Live coverage is PCM16 stereo nonstreaming WAV; other representations retain their specifically attributed offline evidence.

The steward independently rehashed final `c10-wav-cold-02`/`c10-wav-warm-03` run and audio/bootstrap/gameplay/provider receipts, and source-matched core/bootstrap `54fa1aae` and observer `eaae4915` DLLs. Product/bootstrap source is unchanged after the built core. All 59 public metadata/startup sampled traces and 41 common OGG playback operations agree. The authored WAV's 145,728 bytes are served from cache on its native worker with no lower-reader construction; its actual SoundDef plays near the end and subsequently leaves the native manager. That removal does not establish uninterrupted completion. OGG first-playback reconstruction and retained native references across save/reload remain supported. Seven restored hashes and nine absences were independently verified; no fixture process remains. Review artifact: `artifacts/ecosystem-next-20260910/c10/wav/steward-review.json`.

Retain the working PCM/float increment. No full C10, audible-quality or performance acceptance follows. The next same-owner increment covers streaming WAV through natural content and the bounded lossless extended/compressed WAV format route, preserving native conversion. It also implements provider-independent MP3 frame-index reuse while retaining the current native decoder selection/construction. Index reuse alone is not broad MP3 on-demand replacement: the remaining decoded-data/first-use capability needs a concrete implementation design and reliable provider ownership/identity. The live ACM descriptor is useful evidence but does not identify the selected binary or a future automatic selection.

Use existing test/content mechanisms and proportional native comparisons. Any unimplemented subtype stays an explicit open implementation boundary, not an approved exclusion. Broader sound readiness, textures/graphics/icons, public/reflected/worker compatibility, prewarming, production integration and measured improvement remain required. Corrections return to the same owner; no C11 successor. GOG correctness permission remains current and performance permission absent.


## Streaming WAV and native MP3 index functional handoff — 12 September UTC

This increment extends the working audio path to a naturally loaded streaming
WAV, preserves compressed/extended WAV format information without changing native
conversion, and implements reuse of the game's MP3 frame index (the list used to
find audio when seeking). MP3 still constructs its real native decoder during
loading. These are bounded functional results, not full C10 acceptance or a speed
claim. Work continued from reviewed `2c25efe84218313a1617bd38d1308d2fbc6c35fe`
under action `wake-up-c10-native-formats-2c25efe8-20260912`.

### Implementation and independent corrections

Implementation commit `857ff3418bb33afc42a508a080f5851a0af0956a` and reviewed
corrections `dc54512be83507a3dfded5af05564c33b03a8f7a` retain the native manager,
outer reader, sample conversion, clip, background worker, callbacks and disposal.
No generic Harmony hook, replacement decoder or manual dependency was introduced.

WAV record v4 preserves the actual native format type, all seven scalar fields,
and all 100 inline extra-data bytes of the pinned `WaveFormatExtraData` layout.
That includes valid bits, channel mask and subformat GUID for extensible WAV.
The original cold `WaveFileReader` parser supplies the record; warm restoration
uses the same exact type/private fields without invoking patchable NAudio
constructors or helpers. Earlier v3 records have a different key/version. Raw
encoded bytes remain below the unchanged `CreatePcmStream` and
`BlockAlignReductionStream` conversion branch. Converted provider-dependent PCM
is not cached by this WAV extension.

MP3 uses the exact private nongeneric `Mp3FileReader.CreateTableOfContents`
boundary. An admitted original filesystem stream is fully hashed without reopening
it; the key includes native identity and the MP3 record discriminator. A successful
scan records every native file/sample position, byte/sample count, total samples
and terminal source position. Publication requires a native load return without
an exception or a returned clip already marked Failed; native background Loading
is legitimate and is not a completed-playback claim. A hit restores those fields
and skips only the index scan. The surrounding ID3/Xing/header work, current ACM
provider selection, decoder construction, duration/bitrate calculation, read/seek
state and disposal remain original. Corrupt or ineligible records use native work.
The old/final bootstrap guards include this exact own-install target before
admission; relevant mutations invalidate the epoch. The previously open concurrent
unknown-Harmony-discovery race is not claimed solved.

Independent cross-review found and corrected three issues before any deployed
qualification: a patchable constructor/helper during warm format restoration;
a non-null Failed clip being treated as successful MP3 loading; and a proposed
nested MP3 store bypassing the shared budget. MP3 now uses the same
`PreparedAudioCache` owner/store, integrity envelope and shared ceiling as other
audio records, with separate key identities. The nested directory was never
used in a live run. Focused tests cover the constructor/helper exclusion and
shared-store ownership as well as native index failure/corruption/mutation cases.

### Exact functional evidence

Tested core/bootstrap source is `dc54512be83507a3dfded5af05564c33b03a8f7a`;
final observer source is `140b06fc89b86526b530ec965850cdab41a1351a`.
Core SHA-256 is `21d2d37e779f1ec61c05df74e04aa7d058f80d3c67122e0ce4b3832d4451169e`,
observer `58fe84b8470f56c16159aebdfca9d8ef044c113d879797cf5f72c4b9cf2c4f6f`,
bootstrap `cac13f01b63be67bff2c75470518978d19e01a4276efc454a279894ddca570b7`.
Doorstop proxy identity is unchanged; session is
`7d6d00e6d76d44adb0a1b19622597444`. Build identity is distinct from the restored
baseline currently deployed in the fixture.

The final captures are `c10-formats-cold-02` and `c10-formats-warm-03` on GOG
`gog-rev573-20260910-194017-cf1a1eb0`. The selection has 15 frozen package IDs
plus core/observer, adding Vanilla Expanded Framework, Vanilla Weapons Expanded
and its authored Sound Overhaul to the prior OGG/WAV selection. Both captures
passed independent menu/bootstrap/preloader, audio and gameplay/save-reload
checks, exited normally with code zero, and set `automaticTestPassed=true`.
Master preference/listener stayed muted. Functional timings are diagnostic only.

All 101 OGG/WAV clip metadata sets and their common sampled startup operations
agree between runs. Warm menu state has 101 audio admissions, zero lower-reader
constructions and zero replay mismatches; cache counters include the separate MP3
index hit (102 total hits, zero misses). Naturally loaded
`tro.vwe.overhaul` / `Things/AntiMaterialRifle` is a 310,336-byte PCM16 mono 48 kHz
WAV, with 310,154 data bytes. Its SHA-256 is
`6c62cc5ec9e599c98729422aafb0318289a0ea1d3eaf9fcb908699cc2fa62bc5`.
The existing `VWE_Shot_AntiMaterialRifle` sound definition plays the original clip.
Warm before-play state has zero constructors and 76,800 cached bytes. The first
native playback Read on thread 124 constructs the original lower reader; playback
returns 310,154 bytes, with zero replay mismatches. All 43 common sampled WAV
operations agree. The source progresses beyond 90% and later leaves the native
manager; its removal reason is not observed, so uninterrupted completion is not
claimed.

The existing OGG song also first reconstructs on a native playback Read on thread
124, progresses to 1,536,000 returned bytes and retains its original song/source/
holder ownership through paused and unpaused reloads. All 64 retained common
sampled operations agree. Live operation signatures cover at most 256 bytes per
read; they are not full waveform comparisons or direct Unity callback hooks.

Ordinary loading of authored `Things/LightSMG.mp3` changes from one native index
scan/publication to one skipped scan and zero native scans. The native record has
59 rows, 67,968 samples, source/data start 56, terminal source position 19,880 and
unchanged cache-file SHA-256
`2412efab134a0a193b25cebcf6b22906f0f2cfa4f1c0a6960ba3a42ddd3201e0`.
Its existing `VWE_Shot_LightSMG` SoundDef plays the original loaded clip near its
end; native holder/load state survives reload. Separate explicitly owned controls
then compare original ACM readers with and without index reuse: native output
metadata, length, bitrate, five seeks and full hashes of the subsequent reads
match within and across both captures. These controls dispose their readers and
verify the borrowed source remains open. The natural-playback probe's legacy
`observerReadSeekOrSettlement=false` describes natural playback only; the
separately labelled `explicitNativeControl` intentionally performs reads/seeks.

Final offline evidence is **95 managed tests and 72 tooling tests passed** under
`artifacts/fixture-tests/d7c454dbf9824281bbcfb1d6e6304c76`. Native format tests
compare serialization, marshalling and raw bytes. Windows conversion controls
produce matching output for A-law (10 bytes), mu-law (20) and GSM610 (2,048).
The chosen MS/IMA ADPCM controls return zero bytes on both paths; this does not
qualify useful decoded ADPCM output. Extensible PCM/float controls preserve
`AcmNotPossible calling acmFormatSuggest`; metadata support does not override the
native converter's inability to decode these cases. Further subtype/runtime
coverage remains open. Twelve prior PCM/float SampleChannel cases remain covered.

The only failed live attempt, `c10-formats-cold-01`, selected an unrelated streaming
WAV with no resolved SoundDef. Its native loading/MP3 publication worked; the
observer was bound to the intended authored WAV before the final pair. It exited
normally and is retained. Two ambiguous NUnit assertion casts and one test-output
analyzer requirement were corrected before final test success. Existing unrelated
log warnings remain; this is not a clean-log claim.

Raw evidence is under `artifacts/ecosystem-next-20260910/c10/native-formats/`:
`final-paired-correctness.json`, `native-mp3-index-record.json`,
`native-wav-conversion.json`, `natural-source-identities.json`,
`package-identities.json`, `independent-review.json` and `after-endpoints.json`.

### Remaining MP3 first-use design and stopped boundary

The next MP3 design should retain the actual native ACM conversion stream selected
for each reader, rather than predict a future provider from an earlier descriptor.
The current native constructor already obtains that stream and its output format.
Keep this selection/open step eager and retain that exact instance until native
reader disposal. A bounded next experiment can defer only its private conversion
buffer materialization and later decode work behind the original Read/Seek paths,
while preserving seek reset, earlier-frame replay and buffered leftovers. The
pinned `AcmStreamHeader` already delays prepare-header/convert operations until
Convert, so that work must not be described as newly deferred. Inspect and target
only remaining constructor allocations and prerequisite decoded-data work, using
exact early/final guards and native success/error controls.

Retaining the actual instance solves ownership of the provider selected in this
process; it does not qualify provider-dependent PCM from an earlier process, nor
remove the existing nonstreaming startup decode/readiness obligation. Persistent
PCM reuse still needs reliable selected-binary/selection identity before replay,
or current-native validation of the reused data. The observed Fraunhofer descriptor
is not that proof. Moving `acmStreamOpen` itself and replacing it later would again
change selection/error timing; this increment supplies no such equivalent route.
This is a concrete bounded continuation, not an approved exclusion or completed
MP3 on-demand replacement. No universal loader framework or replacement decoder
is proposed.

All seven assignment transactions were rolled back in reverse order. Seven
original hashes and nine absences match `after-endpoints.json`; metadata audit
passes. Normal Steam process 50776 and local DeckHost 48204 remain untouched,
and no fixture process remains. Independent final capture review found no new
bounded blocker. Ownership returns stopped to the steward; any corrections return
to this same C10 owner. Full sound readiness, textures/DDS/PNG/JPEG/graphics/icons,
public/reflected/worker compatibility, prewarming, concurrent unknown-loader
compatibility, production integration and measured individual/combined performance
remain release requirements. No C11 successor, parent timer/state change,
performance run, normal-data/UI/Deck action, Steam promotion, merge, push or
publication occurred.

## Steward native-formats review: correction required — 12 September UTC

Clean stopped handoff `66b8ff183f0057a12f79c94d4253fb62455c405b` was independently reviewed. The steward rehashed final core/bootstrap `dc54512b`, observer `140b06fc` and raw run/audio/bootstrap/gameplay receipts, compared 101 clip metadata/common startup samples, 43 WAV and 64 OGG common playback operations, and checked actual native/reused MP3 seek-read controls. Seven baseline hashes and nine absences match; no fixture process remains. Private review artifact: `artifacts/ecosystem-next-20260910/c10/native-formats/steward-review.json`.

Independent WAV metadata/store review found no concrete blocker. MP3 runtime review found an extra callback: `PreparedAudioRuntime.AfterLoad` calls patchable `Manager.GetAudioClipLoadState` after relevant late hooks close admission. `BeforeLoad` sets `Mp3Context` after PushContext even if no context was admitted; `enabled` remains true in native fallback. A counting hook receives an extra call, and a throwing hook can replace a successful native return before context restoration. Native Manager.Load does not make this getter call.

Return this correction to the same C10 owner: use pinned native state without additional hookable callbacks to reject Failed clips, distinguish admission from required context restoration, and always restore context while preserving native return/exception behavior. Retain Failed-clip publication rejection. Verify one focused real late-hook regression; reuse sufficient unchanged evidence rather than repeat the whole matrix.

The increment is not accepted yet. Successful captured behavior remains meaningful within its limits; MP3 index reuse is not first-use/decoded-data replacement. Zero-output ADPCM controls and native extensible conversion failures do not prove successful decoding. Full C10 assets/readiness/compatibility, production integration and performance remain open. No successor or performance permission. This review also normalizes invalid Windows-encoded punctuation in this report to UTF-8 without changing accepted history.


## MP3 completion callback correction handoff - 12 September UTC

The extra native getter call identified by the steward is removed. MP3 load
completion now reads the game's already-written state without invoking game or
Unity callbacks, and always restores the caller's outer context before optional
bookkeeping. A real late counting/throwing getter hook receives zero calls during
the corrected completion path. Ordinary successful MP3 loading still publishes
its native index. This corrects the bounded finding; acceptance remains with the
steward, and full C10 remains open.

Work began at exact clean `ced5e1d56402a902e141bf8cd7b871e19a6b40e0` under action
`wake-up-c10-mp3-completion-ced5e1d5-20260912`. Source, core, observer and bootstrap
for the targeted test are `e46b41d69486c2fc7478779beac2b485736f628b`. Core SHA-256:
`a2c1e9e2778cc2c38a4e094c1c003dd45021418fa0aeeeaf959fd23fe7088ffd`;
observer `98d7bd1f23b9eabc0686d21317a335a4d26cd773bbc9d0de84e25a30c61e9c55`;
bootstrap `c9752ece6deb588dec0f48dc34e3991722bac58f0532b3e6e99ee564ee631802`.
The bootstrap source/proxy behavior is unchanged; its new build is separately
identified. Deployed experimental session was `311e74bfd85e496ba53cff2371c49d4c`.

`PreparedAudioNative` qualifies the private static
`Dictionary<AudioClip, AudioDataLoadState>` field and the native setter's exact
five-instruction direct-storage shape before admission. The existing native Load
body contract remains enforced. Completion enumerates that dictionary and matches
keys using `ReferenceEquals`; it calls neither the public load-state getter nor
AudioClip properties, Unity null/equality operators or the dictionary's key
comparer. Only an observed Loading/Loaded state permits publication. Failed,
Unloaded, missing and null results do not. An optional inspection/storage failure
also cannot replace the original return or exception.

`PreparedMp3IndexRuntime` now separates scope suspension/restoration from actual
admission and publication. Every native Manager.Load masks the parent index
context, including ineligible nested loads. BeforeLoad installs that scope before
optional preparation and returns early when admission is closed. AfterLoad first
restores both contexts, then performs MP3 bookkeeping only for an actually
admitted, still-current context; it returns directly from the MP3 branch without
passing through the old Unity-null checks. Closed admission does no source hashing,
load-state inspection, publication or clip diagnostics. Original MP3 constructor,
provider choice, decoding and index contents are unchanged.

Final focused offline checks passed **13 managed tests and 72 fixture-tooling
tests**, recorded in `artifacts/fixture-tests/c185d4a22dfc45458e32c5aedd1a10ad`.
The six new completion cases exercise Loaded/Loading versus Failed/Unloaded,
missing state, an intentionally failing optional state read, nested restoration,
closed admission, a throwing key comparer and original exception object identity.
They use managed clip identities and the native stored-state field; they do not
claim engine-created clips. Existing native MP3 indexing/read/seek tests remain
included. An initial attempt to patch the actual getter in this host failed with
`ECall methods must be packaged into a system module` because its body references
Unity internal calls. No security setting was changed; the actual hook regression
was moved into the existing isolated GOG observer.

The single live correction run, `c10-mp3-completion-01`, used the existing 17-entry
native-formats selection, functional purpose, muted master/listener, nonactivating
launch, independent menu/bootstrap observer and normal automatic exit. It first
verified the ordinarily loaded authored LightSMG MP3's successful native scan and
publication. It then admitted an outer test context and installed a real late
`Manager.GetAudioClipLoadState` prefix, verifying the guard closed admission for
that exact method. Counting and throwing variants were each explicitly invoked
once to establish that the hook was active. Subsequent actual synchronous native
Manager.Load calls returned Loaded clips with **zero getter-prefix calls**, each
restoring the same outer context. Final scope closure left no context behind.
Source-hash bytes and MP3 publications stayed unchanged, and completion failures
remained zero. A separate capture-only factory prefix retained each explicit
control's native outer reader for required disposal; both control readers were
disposed and their clips destroyed. No playback, audible-quality or performance
claim is made by this correction case.

The run has `exitCode=0`, `automaticTestPassed=true`, and successful prepared-audio,
bootstrap and preloader flags. No failed live correction run is omitted. Independent
source and final-capture review found no new blocker; the reviewer-requested reader
capture/disposal assertion was included before the run. Evidence and package/receipt
hashes are in `artifacts/ecosystem-next-20260910/c10/mp3-completion/`:
`final-correction-evidence.json`, `before-endpoints.json`, `after-endpoints.json`.

The earlier full native-formats comparisons remain attributed to core/bootstrap
`dc54512b`, observer `140b06fc`, and `c10-formats-cold-02` / `c10-formats-warm-03`.
Their 101 metadata/startup trace comparisons, 43 WAV and 64 OGG playback operations,
and original/reused MP3 controls were not rerun merely to reproduce counts. This
correction adds targeted evidence; it does not relabel those older runs as the new
build or claim new broad gameplay acceptance.

All four correction transactions were restored in reverse order. The original
seven hashes and nine absences match, metadata audit passes, and no fixture process
remains. Normal Steam 50776 and local DeckHost 48204 remain untouched. Child review
is stopped; checkout/build/deploy/fixture ownership returns to the steward for
independent re-review. No C11 successor or self-acceptance, parent state/timer edit,
performance experiment, UI/normal-data/Deck action, Steam promotion, merge, push or
publication occurred. The existing MP3 actual-provider-retention/first-use design
and all broader assets/readiness/compatibility/production/performance obligations
remain open as documented above.

## Steward MP3 completion correction passed — 12 September UTC

The steward and the reviewer who found the defect passed the bounded correction at clean stopped handoff `77f002e4aea05c1c7cc6c3f28e5cf96a22889cb7`, tested source/core/observer/bootstrap `e46b41d69486c2fc7478779beac2b485736f628b`. Completion reads pinned stored state by reference without native getter, Unity equality or comparer callbacks; only Loading/Loaded allow publication. Every load masks/restores nested MP3 context independently of admission, restoration precedes optional bookkeeping, and closed admission avoids hashing/bookkeeping. Native return/exception behavior is preserved.

The steward independently rehashed the targeted run and captured receipts plus all matching package DLLs. In actual GOG `c10-mp3-completion-01`, counting and throwing getter hooks were proved active; both native load controls made zero getter calls, returned Loaded clips, restored the outer context and disposed their captured readers. Ordinary admitted MP3 publication passed; no leaked context, closed-path hashing or completion failures were observed. Seven baseline hashes and nine absences were independently rechecked after restoration, with no fixture process. Private review: `artifacts/ecosystem-next-20260910/c10/mp3-completion/steward-review.json`.

The prior native-formats increment is retained with this correction and its original source-bound evidence. The full formats matrix was appropriately not repeated for this isolated bookkeeping fix. No full C10 or performance acceptance follows.

### Next C10 focus: texture publication

Continue the same C10 owner on the major texture/graphic/icon gap, preserving the working audio increments and their open obligations. This is work ordering within C10, not a capability exclusion or successor dispatch. MP3 decoded-data/first-use, sound readiness, broad subtype/consumer qualification, prewarming and performance remain required and will return after the next texture architecture/behavior checkpoint.

Reopen texture work only with a materially different, source-backed premise that addresses the preserved failure: a complete native Texture2D must remain safe through direct/reflected/worker access, including when main waits for a worker. A GPU resource alone, incomplete public object, ordinary eager loading relabeled deferred, or a main-thread dispatch that deadlocks does not satisfy it. The new earlier startup boundary may change admission possibilities, but does not by itself solve native CPU/GPU publication. Use the existing authorized isolated native-plugin experiment where justified; production backend adoption remains a separate owner decision. Return a working natural texture-family proof if attainable, or a concrete evidence-backed architecture decision with alternatives and implementation paths. Do not silently reduce coverage or close the parent capability with a failed proof, and do not build an indefinite instrumentation project.

## Selected audio readiness increment - 12 September UTC

The [selected audio preparation report](c10-selected-audio-readiness.md) records the
next bounded MP3 finding and the implemented native music/sound preparation path.
Corrected cold/warm GOG captures at `994b88fc` pass with readiness active, native
playback and save/reload, unchanged public clips, and unrelated songs left deferred.
This synchronous selected-clip increment does not complete MP3 decoded-data reuse,
advance prewarming, texture/graphic/icon publication, broad compatibility or C10.
The fixture is restored and the team returns stopped for steward review.
