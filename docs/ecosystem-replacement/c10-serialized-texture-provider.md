# C10 serialized texture provider: construction works, deferral gate fails

## Steward review and proposed next experiment — not approved

Stopped `7cf169d108dc6087740591a56bdef124c945c9be`, tested source
`2f9ba1c8456384b3411aa59d7797524a4794d1f7`, passes independent review of the
checkpoint's narrow conclusion. Construction and native readiness worked for
this sample; it did not leave source-payload work deferred after texture return.
This rejects the tested candidate, not all serialized assets or the required
texture capability. Do not expand bundle-format variations under the same premise.

The steward checked 49 private files, five external references, four package
receipts/eight files, 24 captured observer/input files, seven rollback receipts,
seven restored hashes, ten absences and the retained frozen-file repair. Actual
event order and byte ranges agree with the report. The failed second observer
run and unlaunched first setup remain separate from the final passing functional
probe. Independent architecture/source review found no blocking finding and
confirmed the limits of the proposed compatibility change. Raw review:
`artifacts/ecosystem-next-20260910/c10/serialized-provider/steward-review.json`.
No game/helper remains; ownership has returned to the steward.

**Proposed owner decision:** authorize one bounded prototype under a changed
consumer assumption: selected pending assets are obtained through supported
demand paths; known worker use declares/prepares dependencies; known direct or
opaque access completes its exposed population first. Unknown direct dictionary,
reflection or undeclared worker acquisition would not be promised unchanged for
those pending assets. This must be explicit; merely choosing an asset provider
cannot control how other mods consume its textures, and unknown bypasses cannot
be promised an automatically detectable fallback.

The steward recommends testing this narrower assumption before deciding whether
it is acceptable in a release. The prototype must name real texture/graphic/icon
families and demonstrate useful remaining deferred work after required eager
completion, correct supported first use, native quality and ordinary off-mode.
One sample or a new API with no natural consumers cannot pass that test. A passing
prototype would return for an explicit coverage/compatibility decision; it would
not itself approve production adoption, supplier-removal claims, a release scope
change or performance acceptance. Normal full-compatible loading remains intact.

This experiment is **not approved**. The alternative is to retain the full
current public/worker contract, keep C10 open and require a newly concrete native
completion interface or private workload before another implementation attempt.
Neither such interface nor sufficient private work is established today. No
exclusion is inferred. C04/C05 performance and all C10/C11 release requirements
remain open. The owner has returned; performance testing stays prohibited.

The game successfully loaded a texture constructed from an existing faithful C05
native record, without installing Unity Editor. All nine mip levels matched the
reference on the GPU, and the completed object retained its settings, readability
and reference lifetime. However, Unity read the entire texture payload before
returning that object. This sample therefore establishes construction and native
readiness, **not useful texture loading left deferred behind a public object**.

The bounded candidate stops here. No general bundle pipeline or production
integration is justified by this result. Full C10/5b, graphics/icons, remaining
audio readiness, performance and release acceptance remain open; C11 is not
dispatched. This is a functional result only, pending steward review.

## Question and changed representation

An AssetBundle is Unity's packaged collection of serialized objects. A `.resS`
member holds their separate bulk data, such as texture pixels. The question was
whether this engine-owned representation could keep useful work deferred while
returning a complete native texture, including to previously supported workers
when the main thread is waiting for them.

This differs materially from the previously tested raw PNG/DDS registration,
managed `Apply`, separate D3D resources and custom texture-update paths. Unity
owns this serialized upload queue. None of those failed routes, private object
writes, raw detours, thread registration or global wait pumping was repeated.

Unity documents separate header/payload upload for eligible unreadable textures.
Its background-loading contract retains main-thread object integration, and
accessing an unfinished asset request may block. These are source contracts,
distinct from the actual GOG observations below.
[Upload pipeline](https://docs.unity3d.com/2022.3/Documentation/Manual/LoadingTextureandMeshData.html),
[integration](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-backgroundLoadingPriority.html),
[request access](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundleRequest-asset.html).

## Actual construction and identities

Assignment `wake-up-c10-serialized-provider-881c422f-20260912` began at exact clean
`881c422f1a5c7b498818155bd5db8b78ad091b21`, retaining all intervening C04/C05 and
C06–C13 ancestry. Only fixture tooling and the private observer changed; the
product source, default-off policy and accepted audio implementation did not.

- Initial observer/core source: `d4f38f5e429d29f48c64c066a76669d5d1b928fa`.
- Tooling-only prepared-argument correction:
  `bdd9e2ed33bef68d19ca124643fcfddca1bd64a4`.
- Final tested observer/core and matching package source:
  `2f9ba1c8456384b3411aa59d7797524a4794d1f7`.
- GOG generation: `gog-rev573-20260910-194017-cf1a1eb0`.
- Frozen manifest:
  `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
- Private construction, source extracts, audit and logs:
  `artifacts/ecosystem-next-20260910/c10/serialized-provider/`.

Existing UnityPy 1.25.2 wrote a two-object uncompressed UnityFS version-8 bundle,
using Unity 2022.3.35f1 serialized-file version 22 schema from the private Royalty
bundle. The original template image was discarded. The inserted record came
unchanged from C05 capture `c05-perf-auto-build-13`: Alpha Animals'
`Textures/Things/Pawn/Animal/AA_RoyalAve/AA_RoyalAve13_south.dds`.
The current frozen source still hashes to
`433307d7d7d4b3caa6e9fcad388eb0d649c3f4fa219012ed38586d0f44de484d`
(87,556 bytes).

The record preserves 256×256 BC7, graphics format 109 (linear UNorm), nine authored
mips, unreadable CPU state, trilinear filtering, repeat wrapping on all axes,
anisotropy 0 and mip bias 0. No encoding, quality conversion or reduced initial
mip detail was introduced. A parser round-trip checks the metadata and all native
payload bytes; that check alone was not treated as engine proof.

The resulting bundle is 93,409 bytes, SHA-256
`ba83d9c8c8631db5c4b95d3626a4ba301d0ea35016bb3ac5a999f558b1451f60`.
Its separate uncompressed `.resS` member occupies byte range `[6001, 93409)`:
87,408 payload bytes. The original WNT2 record, `native.bin`, hashes to
`b4e682fdd631748c567789431be8079ced1d664c82136270c7262a6b3ed800ca`.
`construction/output/construction.json` binds the source index/group, template,
tool files, descriptors and independent UnityFS directory parsing.

UnityPy is MIT licensed; its pinned local license and relevant implementation
hashes are recorded. The private game schema/template and captured mod content
are not approved redistributable assets. No dependency or game asset was bundled
into a public product. An eventual approved writer would need a redistributable
schema/construction strategy and bundled dependency/license provenance; no
end-user editor installation is proposed or authorized here.

## Observed operations and readiness

Final run `c10-serialized-texture-03` used **3 selected + 2 local = 5 active**:
Prepatcher, Harmony, the observer, Wake-Up and Core. The natural source record was
an explicit private input, not a claim that Alpha Animals was active in this run.
The fixture was muted at 1280×720 and launched minimized without activation,
with functional purpose, independent menu observation and normal exit/capture.
The owner's unattended grant had ended; no performance experiment occurred.

The probe uses published `AssetBundle.LoadFromStream` with a recording, seekable
file stream and a 1,024-byte managed read buffer. Unique overlap with the verified
payload interval is counted, so rereads do not inflate coverage. Archive read-ahead
is included. The stream remains owned until bundle unload.
[Stream contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.LoadFromStream.html).

| Observation | Unique payload bytes already read |
| --- | ---: |
| Bundle returned; asset names enumerated | 143 / 87,408 |
| Synchronous `LoadAsset` returned the texture | 87,408 / 87,408 |
| Asynchronous request returned, sampled while `isDone=false` | 50,319 / 87,408 |
| Async request completed, before reading `request.asset` | 87,408 / 87,408 |
| Async public texture returned | 87,408 / 87,408 |

Opening the bundle left payload work pending. Loading the asset triggered its
consumption. Async loading spread that preparation across engine execution, but
did not leave source payload unread at public exposure. The async intermediate
count is a concurrent snapshot, not a stable scheduling promise or timing metric.

Both returned textures matched the C05 native-restoration reference in every
descriptor above and GPU readback of all nine mips, including orientation and
color/alpha content. CPU raw access retained this player's native unreadable
result (`null` plus its diagnostic). Direct dictionary enumeration, reflected
indexer access and a material's texture reference retained the same object.
An ordinary worker read the already-complete descriptor/CPU result while main
joined it. `Unload(false)` followed by closing the input stream preserved the
retained texture and all-mip GPU equality until explicit owned destruction.

These were bounded private-holder/reference checks, not insertion into every
natural mod holder, a live mask/atlas workload or broad worker-first-demand
qualification. No unfinished request was exposed to a worker and no borrowed
native texture was destroyed. There is no reason to widen those tests after the
candidate's required residual-work gate failed.

## Exact-player explanation and limits

The existing matched UnityPlayer/PDB identity was reused and independently
checked. Player SHA-256 is
`e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96`;
PDB GUID is `ff67de78-9de3-4c50-a915-72eed5f70d3b`, age 1.
The new extracts are in `native-review/` beside the earlier
[texture design evidence](c10-texture-design.md).

`Texture2D::AwakeFromLoadThreaded` at RVA `0x493EB0` enters
`Texture::BeginAsyncUpload` and `AsyncUploadManager::ScheduleAsyncCommands`.
However, `Texture2D::AwakeFromLoad` at `0x4944E0` invokes
`VerifyFileTextureUploadCompletion`; its unfinished branch depends on an engine
graphics device and joins native completion state. An unfinished
`AssetBundleRequest.asset` also reaches the preload manager's completion wait,
which executes main-thread integration jobs. These on-disk implementation
addresses are evidence, not callable or approved plugin interfaces.

Byte coverage alone does not prove that every physical GPU job has completed.
The native integration check and actual full-mip equality support the ordinary
readiness claim; no stronger GPU fence requirement is imposed. This one faithful
provider/sample did not establish post-publication payload deferral. It does not
disprove every possible serialized provider, and lower-detail mip streaming
remains the separate explicit C11 capability.

## Validation, retained failures and restoration

The 65 existing fixture unit tests passed. Focused private input checks also
refused performance purpose and detected changed staged bytes before engine
loading. Both core/observer build pairs passed with zero warnings/errors.

`c10-serialized-texture-01` never launched: preflight caught a missing stored
probe argument. `-02` exited normally but failed the observer's mistaken
assumption that unreadable CPU access always throws; it returned `null` here.
The correction preserves that native result. `-03` passed construction/readiness
and automatic exit. A premature observer-deploy attempt was refused by the
operation lock before it changed anything; the subsequent serial operation
succeeded. These records remain retained, not relabelled as passing samples.

Audit verified four package receipts/eight payload files, 24 captured observer
and input files, construction input/output/tool hashes and seven rollback
receipts. Seven baseline hashes and ten absences match. Retained repair
`20260912-170800-04578930` remains committed with its exact replacements.json
hash, size and timestamp. No RimWorld/helper remained. Independent bounded
construction/source/evidence review found no blocking finding; it is separate
from steward acceptance. No normal data, Steam/Deck, security, UI automation,
merge, push or publication operation occurred.

## Concrete decision for the remaining texture capability

Do not promote this provider into a general bundle pipeline: changing packaging
did not move the native public-readiness boundary. Complete public populations,
preparation before opaque callbacks and genuinely private deferred work remain
authorized under plan 6.1. This result does not revoke those options. The prior
inspection has not identified enough new private texture/graphic/icon work to
close broad 5b while preserving all existing public access.

The reviewable compatibility alternative is to let selected texture families
remain in C09-keyed private descriptors backed by C05 records until an explicit
demand API is called. Main-thread demand would load normally; known worker
families would declare dependencies for completion before dispatch. Direct holder
enumeration/reflection or opaque callbacks would require completing their entire
exposed population. To leave other selected objects pending after today's native
publication point, **undeclared direct/reflected first acquisition of those
objects could no longer be promised unchanged**. This is a concrete compatibility
change, not an approved fallback or current implementation. It would still need
real mixed-list coverage and first-use qualification, and might lose its benefit
where broad enumeration forces everything ready.

The steward/owner must explicitly decide whether that changed consumer contract
is acceptable before implementation. If the full current contract is retained,
C10 stays open: another native prototype needs a concrete, reviewed way to finish
the original full-quality object independently of a blocked main thread, or a
newly evidenced private workload. This is not a claim that such an interface has
been found, nor a request for another unbounded source search. No scope exclusion
or production adoption is inferred from this checkpoint.
