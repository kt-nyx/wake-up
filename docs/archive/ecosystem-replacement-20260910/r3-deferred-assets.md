# R3: optional deferred audio — offline implementation

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

Wake-Up can postpone opening a filesystem audio stream and creating its native
clip until something asks for it. The new **Defer unused streamed audio
(experimental)** option is off by default. It implements a real loading path,
not a remembered lookup for an object already created. It has offline checks
only: no startup saving, smooth playback, menu rendering or gameplay result is
claimed. Progressive DDS detail is still absent.

Dispatch `wake-up-r3-deferred-offline-f10ea98` started at clean
`f10ea983d17bd5d40d5f1339f519761ce849410b`, on
`codex/wake-up-r3-deferred-offline-f10ea98`, in the sole checkout. R2's source,
budgets, native preparation and separate DDS exports remain intact. No R4 work
was started. The parent explicitly accepted the bounded experimental consumer
scope in `wake-up-r3-consumer-boundary-guidance-1`; that is not release acceptance.

## Supported family and useful avoided work

The family is the game's filesystem audio sources that normally stream: existing
files larger than **307,200 bytes**, selected with native extension and mod-folder
rules. No mod, fixture, directory-name or filename allowlist determines admission.
Smaller clips still use ordinary upfront loading/background decoding. Resources
and asset bundles retain their original routes.

The native `ModContentLoader<AudioClip>.LoadItem` opens the stream, constructs
`CustomAudioFileReader`, obtains sample/channel/rate metadata and creates the
Unity `AudioClip`. Large clips then decode through native playback callbacks.
R3 postpones that opening/header/decoder/clip work; it does **not** claim to save
decoding the whole encoded file. Native audio loading and sample callbacks remain
the implementation used at completion. There is no new audio codec or worker pool.

Pending completion explicitly uses the native stream overload with background
decoding disabled and requires its loaded state before publication. If an admitted
source is replaced by a smaller file, it retains the stream mode selected at
admission instead of exposing a newly started incomplete background decode.
Unpublished failed objects/streams are released. Ordinary initial small clips
continue through `LoadItem` with its unchanged native background behavior.

The current frozen P-Music source illustrates useful natural coverage: 56 SongDefs
reference 56 streaming-size files, totaling 289,832,040 encoded bytes. This is a
source-coverage example, not a runtime admission result, decoded-memory saving or
performance measurement. The provider has no special runtime treatment. The
older S11 study already established its streaming behavior; R3's different premise
is the broader holder and song-consumer rewrite, rather than another lookup hook.

## Actual consumer map

Required Prepatcher rewrites the generic game methods before compilation and
rewrites compiled song-field reads in eligible mod assemblies. The original
native lookup remains responsible for ordering, absence, Resources, bundles and
error reporting.

| Surface | Implemented behavior |
|---|---|
| `ModContentHolder<T>.ReloadAll` | Audio-only bridge discovers native ordered candidates. It records streamed sources without loading them, and publishes small clips through the native loader immediately. Other types and refused/off modes execute the original body. |
| `ContentFinder<T>.Get` | The existing texture-routing prefix remains. Its native provider loop obtains audio holders through a private, admitted lookup bridge. Original reverse provider priority, successful-object checks, Resources, bundle fallback and missing-result diagnostics remain. |
| `ModContentHolder<T>.Get` | Completes just the requested audio key before the ordinary dictionary lookup. |
| `ContentFinder<T>.GetAllInFolder` and holder `GetAllUnderPath` | Native folder enumeration uses the private lookup bridge; the holder completes the requested subtree before its native trie enumeration. Other folders remain pending. |
| Public `ModContentPack.GetContentHolder<T>` | Completes all pending audio before exposing the holder/dictionary. Preserves dictionary and clip identities and restores source insertion order on first exposure. A retained holder cannot acquire new deferred entries. Exposure before option initialization refuses deferral without retaining the holder in disabled mode. |
| `AnyNonTranslationContentLoaded` | Counts pending audio as content without forcing a diagnostic query to create every clip. |
| `SongDef.ResolveReferences` callback | Defers assignment only when a corresponding pending source exists. Other reference resolution remains native. |
| Compiled `SongDef.clip` reads | Rewritten to `GetSongClip`; a tracked song completes synchronously before returning the real clip. Native music entry/play managers and credits length reads are covered. External assignments remain authoritative. |
| `Root_Entry.Update` | At an idle loaded menu, attempts at most one scheduled song per update, inspecting at most 16 stale queue entries. It does not drain everything on first rendering or continue background warming during colony entry. |
| Holder `ClearDestroy` and `UnityData.DisposeStatic` | Cancel pending work and song requests. Native holders destroy published clips and dispose their native streams. Uncreated clips own no stream to close. |

Known address-taking and detected reflected song-field consumers receive no
admission marker. An unrewritten loaded assembly referencing the game refuses
activation. Native fingerprints cover holder operations, song assignment, folder
lookup, file discovery, extension admission, `LoadItem`, the streaming predicate
and the actual stream overload of `RuntimeAudioClipLoader.Manager.Load`.
Fifteen effective loaded method bodies are rechecked at runtime, so a later
prepatch cannot leave a stale admission marker over changed native ownership.
The inspected Windows implementation is admitted by methods, not by a game-version
allowlist; a different Linux audio method refuses this candidate. Published
Harmony changes to relevant method families, including inner hooks, refuse it.
PNG/prepared loading and texture routing explicitly recognize the coordinated
prepatch bodies; an unavailable audio rewrite still leaves their ordinary
supported bodies admissible.
The background-world guard permits only this exact in-package audio idle postfix,
in its postfix position; it does not permit arbitrary new root hooks or changes
to background preferences.

## State, completion and failure

Pending entries contain ordered source candidates, not placeholder textures or
clips. One owner thread changes state from pending to loading to published.
Nested duplicate requests do not create another object. Pending work is removed
only after native holder publication succeeds. Failed loading/publication retains
the entry for a later explicit request while the feature remains active; a failed
idle attempt is not retried every frame and cannot block later songs.

Native candidate order matters when two extensions normalize to the same key:
a loader failure can fall through to the next candidate. Delayed requests refresh
the provider's native file selection before opening it, preserving current folder
precedence and genuinely missing input. Initially eager clips reuse the initial
discovery instead of rescanning a provider for every small file. Loaded objects
retain native identity and authority; already-loaded source files are not watched
or replaced behind retained references. Current mod order governs each lookup.

Publication adds trie membership, stream ownership and the clip to the native
holder. Failed publication releases only the new unpublished object/stream.
Cancellation during completion rejects stale publication. Refusal attempts
ordinary completion of existing work, then cancels failed optional retries;
ordinary failed loads remain absent. Clearing a holder does not resurrect an old
pending song against a later generation.

Limits are 16,384 pending keys and queued songs, and 4,096 holders. These are
metadata/work bounds, not native decoder-memory or frame-time guarantees. A
native clip creation is indivisible; one creation can exceed one frame. Explicit
whole-holder exposure necessarily requests all of that holder's pending content.
There is no automatic unbounded first-render drain. Bounded native streaming
already supplies asynchronous playback reads; duplicating it with speculative
managed byte caches was not justified. R3 adds no disk store and changes neither
R2's 512 MiB prepared/export budget nor the separate 512 MiB automatic budget.

## Explicit compatibility limits

The supported first-use contract is an ordinary **main-thread** request. Compiled
reads through the bridge may now require clip creation. Worker-thread song reads
and holder access that would expose incomplete audio are unsupported and can
raise an explanatory exception; eager public fields did not require completion.

Arbitrary reflection or generated code can bypass compiled-field rewrites.
Detected uncovered consumers refuse before deferral. When a new uncovered
assembly loads on the owner thread, R3 requests ordinary completion there. When
it loads on a worker, R3 disables further deferral and posts a main-thread drain
request, processed before the idle callback's enabled check. **A bypassing read
before that drain can still see an unassigned song field.** This is not a guaranteed
safe fallback window. The settings description states these limits. The parent
directed retaining this bounded experimental candidate without building a blanket
reflection-analysis/interception system. It is not full on-demand parity or a
release-qualified compatibility claim.

Disabled/refused bridges retain ordinary getter and song-field behavior. Optional
private-field reflection is initialized only inside selected setup, so its failure
cannot poison an inert bridge's type initializer. The ordinary setting is
`deferredAudio`; explicit development selection is `--wake-up-deferred-audio=on`.
The fixture's existing normal-settings allowlist recognizes the setting for a
future authorized run; no launch configuration was prepared here.

## Progressive DDS: assessed alternatives and missing capability

Current Steam DDS creation allocates the authored dimensions/mips, uploads native
content and finishes unreadable. Generic consumers can immediately inspect
dimensions, pixels, masks, materials and atlas inputs. Returning a tiny placeholder
or permanently reduced texture would violate that contract.

Three distinct approaches were assessed against the actual Unity 2022.3 player:

1. Runtime-created DDS textures have no inspected API for registering a streaming
   source with Unity's mip residency system. Merely setting a requested mip level
   does not supply that missing source/lifetime.
2. A private mip-limit group could preserve full dimensions while controlling
   upload. The player exposes group inspection/settings but creation is Editor-only.
   An unknown group falls back to global settings. Changing the global limit would
   affect unrelated textures; no owned group is available.
3. GPU copies can restore real mip bytes into the same unreadable object. Full
   dimensions still require full storage, unknown shaders may sample incomplete
   high-detail mips, and restoring a source does not repair an atlas already baked
   from it. A privately owned, non-atlased consumer with controlled sampling would
   be a different viable integration, but no such complete family was established.

Therefore DDS remains ordinary and full fidelity. No silent inactive progressive
checkbox, permanent low-quality mode or R2 export injection is shipped. The
remaining capability is an owned runtime streaming/residency source, or exclusive
consumer/sampler ownership with completion before public exposure. This is an
engine/ownership limitation with assessed alternatives, not a performance rejection
or a claim that every progressive approach is impossible.

## Offline evidence and remaining live checks

Fresh Steam assembly SHA-256:
`5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`.
Unity CoreModule SHA-256:
`c5c58ea254834291780a1d6c388c241443d07167b4a4b890a23c9494f626ddba`.
Existing read-only ILSpy produced exact native extracts under ignored
`artifacts/r3-deferred-20260910/audio` and `dds`; the latter contains the detailed
alternative decision and official Unity API links. No inline PowerShell assembly
loading was used.

Focused checks execute the production pending state and holder publication using
real native holder/trie/file-discovery code with substituted Unity clip creation.
They cover delayed versus eager work, individual/subtree/full-holder access,
identity, ordered source failure, fresh provider selection, direct replacement,
retry, cancellation, stream ownership, song completion, queue fairness, settings,
known consumer refusal and worker-disable/owner-drain behavior. Cecil checks inspect
the actual rewritten native assembly and method identities. These are not native
Unity decoder/playback or actual Prepatcher/Mono execution tests.

One bounded independent review identified seven concrete issues: disabled getter
bypass, duplicate-source fallback, worker drain/concurrent admission state,
incomplete loader contracts, disabled holder retention, failed-song starvation
and repeated small-clip discovery. They are corrected and covered by focused
checks. The remaining dynamic/worker consumer limit is explicitly retained under
parent guidance, not described as repaired.

Future authorized live work must check actual Prepatcher registration and
selected/active/refused feedback, deferred counts, native song duration/playback,
credits/menu music, sound-folder consumers, first-colony/first-song stalls,
reload/unload/disposal and coexistence with the current supplier stack. Match
whole-startup and first-use costs before making any performance claim. Earlier
accepted captures qualify the unchanged deployed candidate only.

Exact final source/package and check receipt are recorded in the closeout below.

## Offline closeout — 10 September

The implementation is committed at
`7511948b71b1c1da0f39b6d3e9b8fbef5d17c2ed`, from assigned clean base
`f10ea983d17bd5d40d5f1339f519761ce849410b`, on
`codex/wake-up-r3-deferred-offline-f10ea98`. This closeout is a subsequent
documentation-only commit; the immutable package belongs to the implementation
revision, not the documentation revision.

The final guarded focused run passed **197 managed checks and all 50 Python
checks**, with one optional external-helper managed check skipped. It included
deferred audio, asset routing, PNG/prepared textures, startup selection and
background-loading coexistence. Its managed receipt is
`artifacts/fixture-tests/55152d8221b9412cab31f3d7021600e9/features.trx`.
The guarded command and Python result were captured in the task output;
`gameLaunched` was false. The build had zero warnings/errors. No source changed
after this run; only documentation and the committed revision identity changed.

One guarded package build then succeeded with zero warnings/errors:
`py -3 scripts/fixture.py build`. The receipt is
`artifacts/fixture-package/7511948b71b1c1da0f39b6d3e9b8fbef5d17c2ed.json`;
its console record is `artifacts/r3-deferred-20260910/package-build.log`.
The new core DLL is 306,176 bytes, SHA-256
`729dc04cb26accbb232f1ce4a4e3e75d75536d697336fd343f76641f57ec2e7f`.
It is built against the exact Steam revision 590 reference recorded above.
This is an offline core package, not a deployment or a new live-qualified release.

Read-only checks after packaging reconfirmed unchanged fixture `current.json`
SHA-256 `f86ec9e20090ed3a12bffbb439f477e02e33e6644e50de3f71ffe4c435139e7a`,
unchanged `candidate.json`
`864734c3c43c8f0ed1cc8e6b9d50cf94d458528dde2cc63f4331459b1b44189d`,
and the actual old deployed DLL
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`.
No `prepared.json` exists; the reset fixture remains English. The earlier
read-only inventory observations are retained in
`artifacts/r3-deferred-20260910/offline-evidence.json`.
No deployment, fixture preparation, game launch or publication occurred.

Both bounded subagents have stopped. The independent review corrections and
narrow native stream-ownership follow-up are complete; Unity playback and the
documented consumer limits remain live qualification work. No R4 work started.
After this documentation closeout, source writes and builds stop and exclusive
checkout ownership returns to the parent steward for review.

## Parent acceptance — 10 September

The parent accepted handoff `693cea9` and source `7511948` as a bounded,
experimental default-off implementation. Inspection covered native prepatch
registration, effective method contracts, routing/PNG/background coexistence,
settings, offline receipts and package identity. A separate bounded read-only
review checked pending completion, source order, retry/cancellation, holder and
stream ownership, song completion and disabled/refused behavior, with no further
actionable finding. Existing focused evidence is adequate; no review-only rebuild
or repeated test run was needed.

This does not resolve reflective/generated consumers, worker first-use semantics,
native playback or frame stalls. The user-visible limitations and later live
checks remain mandatory; neither progressive DDS nor general texture deferral
is delivered. The reset fixture/deployed package are unchanged. The R3 team and
parent reviewer have stopped; the steward proceeds to the split R4 work packages
under the unchanged offline-only authorization.
