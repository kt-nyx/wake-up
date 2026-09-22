# S11 optional on-demand feasibility checkpoint

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

No useful asset family was admitted in this bounded review. Native startup
already requests the inspected graphics and sound references, and callers can
read the real objects directly. Delaying only file loading until those requests
would move work within startup; delaying the requests too would require broader
changes to graphics, definitions and playback. **On-demand loading remains
undelivered.** The steward accepted this bounded feasibility evidence, not a
negative performance experiment or a rejection of every possible approach.

Assigned base: `deed4d23abad6279bea894dab44d52493cb5c4a1`, branch
`codex/s11-asset-routing`, dispatch
`wake-up-s11-on-demand-20260909-deed4d2`. The checkout was clean at preflight.
The [separately accepted routing checkpoint](s11-asset-routing.md#parent-steward-routing-acceptance--9-september-2026)
remains unchanged. No selector, pending-object state machine, scheduler, new
dependency or runtime hook was added for this checkpoint.

## Inspected candidates and decision

An asset family needs both useful work that can remain undone and a complete
boundary around everyone who can obtain or retain its objects. A large file
inventory alone establishes neither condition.

| Candidate | Concrete native consumers and consequence | Disposition |
|---|---|---|
| Thing graphics and buildable icons | `ThingDef.PostLoad` schedules resolution of every non-null `graphicData`, including realtime-only graphics. Other graphics enter atlases, which combine textures for rendering. `BuildableDef.PostLoad` resolves explicit icons or derives them from real materials, and fills stuff icons. `Graphic_Single` and `Graphic_Multi` resolve textures and masks; directional fallback depends on genuine absence. | No bounded unused subset established. Skipping these callbacks also changes public graphic/icon fields, shared materials and atlas inputs. |
| Terrain and general texture folders | `TerrainDef.PostLoad` resolves graphics and overlays. `ModContentHolder<T>.contentList` is a public replaceable dictionary; direct reads and `GetAllUnderPath` bypass `ContentFinder.Get`. Atlas insertion reads actual dimensions and mask compatibility; baking consumes actual pixels. | No complete consumer boundary established by path prefixes or a central lookup hook. Keep eager loading. |
| Menu background and mod previews | The narrowly classified BGPlanet texture is demanded by a startup static constructor and drawn in the menu; S10 recorded zero matching physical overrides in this fixture. `ModMetaData.PreviewImage` already loads on first access, as recorded in S08. | Neither supplies a new useful deferred family for this checkpoint. |
| Music and sound clips | `SongDef.ResolveReferences` assigns public clips; sound reference resolution resolves all grains, including folder enumeration. `ResolvedGrain_Clip` immediately reads clip length. Music managers and samples later use those objects directly. | Delaying only lookup does not avoid eager reference demand. Delaying reference resolution broadens into sound state, duration, first-play readiness and stream disposal. No audio family admitted. |

`PlayDataLoader` invokes startup static constructors and bakes static atlases
before completion, then performs cleanup including `Resources.UnloadUnusedAssets`.
This ties graphics lifetime to more than a future rendering call. A 2×2
placeholder cannot stand in for a complete texture without changing dimensions,
mask checks, atlas pixels or public consumers.

Completing all pending assets before returning a holder could preserve ordinary
full-holder exposure at that gate. However, that getter has no requested asset
path, and native unsuccessful searches visit every provider. Those searches and
eager reference callbacks can therefore complete pending work during startup.
This review establishes no useful avoided work from that approach; it does not
prove all holder-based designs impossible.

Large filesystem audio files already stream: the native threshold is 307,200
bytes, and sample decoding occurs through read/seek callbacks. Smaller clips
already use background decoding followed by main-thread publication. In the
frozen P-Music inventory, all 56 tracks (289,832,040 encoded bytes) exceed that
threshold. Those bytes are **not** upfront decoded bytes that a new mode could
save. Remaining open/header/clip construction cost is unmeasured, and moving
small-clip decoding to playback has no established first-sound stall bound.

## Evidence and limits

Fresh targeted ILSpy extracts use the frozen GOG 1.6.4871 rev573 game assembly,
SHA-256 `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
The ignored [research index](../../../artifacts/s11-on-demand-20260909/findings.md)
maps methods to local extracts; the [audio report](../../../artifacts/s11-on-demand-20260909/audio/research.md)
records consumer and native-streaming details. These are native source findings,
not a completed inventory of all mod consumers or live compatibility evidence.

The bounded inventory read verified all 315 manifest-listed active supplier
inventory hashes for generation `20260904-182829-e058641c`. It counted 43,305 DDS
and 43,310 PNG/JPEG physical entries. These include all version folders and
paired sources, rather than effective native selection, unique logical assets,
decoded memory or on-demand eligibility. They show available source material;
they do not establish an unused safe subset. Full counts and caveats remain in
`artifacts/s11-on-demand-20260909/audio/active-texture-inventory.json`.

Local YaOpt source illustrates placeholder and completion-queue approaches;
these do not establish the complete consumer, retry or bounded-work guarantees
required here. Existing archive findings were consulted before inspection. No
previously failed technique was rerun as a performance experiment.

## Unchanged product and handoff

Runtime source remains `950144b80c22338e38ce2519b7489b667c366c0a`, carried through
the assigned documentation/review ancestry. Its existing undeployed core package
remains under `artifacts/fixture-package/950144b80c22338e38ce2519b7489b667c366c0a/`.
The packaged DLL remains 224,768 bytes, SHA-256
`97d27abf42a22b4295ad4be8b330f865c1f1e8f6dd0dc193572ccf8ef4a6b463`.
The adjacent receipt remains SHA-256
`0f9b86d80bcad56eb28dd2900c610834bfe1ea7cca3ee3fcf07c5fa66093e819`.
The accepted routing handoff records the prior 43 managed passes, one optional
helper skip and 37 Python passes. Those checks were not rerun: this checkpoint
changes documentation only and creates no new build, package or test evidence.

A future proposal needs a concrete useful family with an inspectable first-use
API and ownership lifetime, or evidence justifying a broader consumer change.
It must retain source/override freshness, direct access, masks, shared objects,
reload/disposal, retry and bounded first-use work. S10's resolver is currently
startup-scoped and closes its store at startup completion; a future on-demand
mode cannot simply call it after the menu or restore a new object per lookup.
No narrower capability or omission from the intended release is self-approved.
S12 has not begun; its S11 readiness prerequisite remains undelivered.

The task and read-only audio researcher stop all writes/builds and return
checkout ownership to the parent steward for review. No deployment, game launch,
normal game-data change, Deck access, UI automation, security change, push or
publication occurred. The verified normal Steam game was left running outside
the fixture. Ignored steward state was not edited.

## Parent-steward disposition — 9 September 2026

Accepted report `7b079188899d4e4a894483b2003f608195f86886` as a bounded feasibility checkpoint only. The steward checked actual ThingDef startup graphic resolution, static-atlas baking and cleanup, SongDef eager clip assignment, native streaming threshold and the recorded consumer/ownership findings. Production source is unchanged. No repeat test/build is needed for this documentation-only result.

Defer S11 on-demand implementation: no useful family with complete access/lifetime coverage was established within the assigned scope. This is not a performance rejection or proof that all on-demand designs fail. A new attempt needs a concrete useful family and consumer/ownership evidence, or an owner-approved broader change. On-demand remains undelivered and no final release omission is approved. Accepted texture supplier routing remains available independently.

S12 progressive DDS detail restoration is also deferred at its existing prerequisite: S11 readiness and first-use behavior are not implemented. Do not create another team merely to rediscover this dependency. Neither optional capability is accepted or claimed as replaced. Move independent S13 background loading next, starting with save loading and keeping live focus/exit/gameplay checks separate from authorized offline work. The stopped S11 team has returned clean checkout ownership; a fresh S13 task receives it from the ensuing steward record commit.
