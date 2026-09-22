# C07 complete atlases and adaptive construction

Dispatch `wake-up-next-c07-f95bae1-20260911` implemented from clean
`f95bae159a11c5e6e7b8b7a5f20bff0fa89a4b9c` in the single checkout. Main remains
unmerged; approved and previous implementation ancestry is preserved.

An atlas stores copied color and mask pixels together with coordinates and
meshes that let the game draw each original texture. Reusing only its layout
does not avoid copying and finalizing those images. C07 instead targets the
complete result, with all objects validated privately before publication.
The old layout investigation's small removable cost does not measure this
different mechanism. First-build batching is a separate required behavior.

The exact GOG game is revision 573, Assembly-CSharp SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Fresh native extracts and raw evidence are under ignored
`artifacts/ecosystem-next-20260910/c07/`. Pinned FastLoader and FGL sources
describe mechanisms, not accepted correctness or performance contracts.

No performance runs, microbenchmarks or cost experiments are authorized while
the owner uses the PC. Standing isolated GOG functional tests remain allowed.
The starting fixture endpoints must be restored exactly before handoff.
Independent review, full acceptance and later measurement remain open.

## First complete natural-group proof

The initial package `16fdca4` crashed during mod construction because it queried
graphics-device state on RimWorld's worker thread. Capture
`c07-functional-cold-01` preserves that failure. `c376151` moved the query to the
main-thread bake boundary. Subsequent natural batching matched native rendering,
but source verification refused compressed 2x2/1x1 mip levels. These levels cannot
be the base of a Unity compressed texture. `8aee0b0` copies each into the final
level of a legal native mip chain and samples that level explicitly through
point filtering and a clamped positive mip bias.

Core `8aee0b0`, observer `16fdca4`, cold capture `c07-functional-cold-04` saved all
eight ordinary activation-lane groups (2,794 ordered textures). Warm capture
`c07-functional-warm-05` restored all eight, reporting eight skipped actual
bakes and zero writes. Both exited normally with `automaticTestPassed: true`.
The checked store occupied 117,642,060 bytes. No supplier was enabled in this
official-content activation lane.

Independent private native bakes of `Building_True` (81 color/mask pairs),
`Item_True` (133 pairs) and `Terrain_False` (9 textures) matched every observed
color/mask mip, native descriptor and ordered UV/mesh result. On warm these were
three actual restored groups. All borrowed inputs survived and global atlas
membership remained unchanged by the comparisons. Synthetic GPU-only base and
non-base changes invalidated the revision despite unchanged CPU bytes; separate
GPU/CPU restoration, ordering, masks and sampling checks passed.

Cold construction produced 62 iterator advances across distinct actual frames;
the native cleanup suffix followed completion. These are functional progress
receipts, not performance or human-visible responsiveness acceptance. The first
proof predates later incremental input verification and additional failure
checks; do not transfer its exact package identity to later source.

## Ownership and input revisions

Each yielded group freezes privately owned GPU copies of its color/mask inputs.
The original texture objects remain the published tile keys and are never
destroyed by Wake-Up. The identity covers the exact frozen GPU texels, every
mip and sampling descriptor, ordered pairing, native producer methods, game and
Unity module identity, backend/driver, color space, compression and atlas-size
settings. This is a general current-content producer adapter, including generated
and mutable inputs; it does not use file timestamps, texture names or a mod
allowlist as freshness evidence. Equal-size source edits are observed when the
native loader supplies their new GPU contents. C08 can extend the representation
identity; later readiness adapters must provide completed inputs at this boundary.

Color/mask pairs are frozen and checked within one main-thread step. Subsequent
frames draw those immutable copies. A newly installed incompatible atlas hook or
changed bake setting discards the current private group and performs one native
bake with its original borrowed inputs; already completed groups are not replayed.
A changed loading-lifecycle hook switches remaining work to synchronous native
completion before cleanup and later callbacks. No mod callback runs on a worker.

Only the qualified D3D11 capture path is selected. Unsupported backends, formats,
hooks or bounds retain native baking. The CPU-compressed unreadable branch has
no capture boundary in this implementation and stays native for persistence.
Full acceptance, broad platform promotion and supplier-removal advice remain open.

## Implemented behavior and limits

Both ordinary settings are default-off and independently selectable: **Static
atlases** reuses completed images and mappings; **Atlas batching** spreads owned
construction steps across actual game frames. Neither requires FastLoader or
FGL Continued. The existing shared cache budget includes `StaticAtlases`; there
is no additional unbounded atlas allowance. Each complete serialized group and
each group's owned source snapshots have a 96 MiB limit. Unsupported groups
retain native baking. Snapshot disposal destroys only owned copies.

The disk record contains the complete color and optional mask mip chains,
texture descriptors, separate CPU bytes when those intentionally differ from
GPU contents, mip overrides and ordered tile rectangles. Restoration allocates
both textures and every native mesh privately before replacing atlas fields.
Any failed allocation or invalid mapping destroys the private result and leaves
the existing atlas untouched. One grouped-store entry publishes the whole result.
The source revision includes sampling state, all mip pixels and native packing
semantics, so names or equal-size/equal-time file edits cannot grant a stale hit.

First construction retains the native ordering, group partition, rectangle
packing, color/mask drawing, mip generation and compression. Source copying and
verification, drawing, and mesh construction yield through the existing
asynchronous startup event. The native constructor prefix precedes this work;
cleanup, remaining callbacks and event completion follow it. The scheduler
checks actual frame changes instead of letting native iterator consumption
exhaust nominal yields in one frame. An elapsed-work budget adjusts drawing and
mesh batch size. Source pairs are indivisible copying/verification units.

The eight-millisecond target is **not a maximum frame duration**. Native packing,
GPU mip generation/compression, one source pair's GPU readback, whole-group
restoration and serialization remain indivisible. No human-visible smoothness,
resource overhead or speed result is claimed from minimized functional runs.
Those are explicitly pending unattended measurement and coordinated visual
inspection. CPU-compressed unreadable atlases and non-D3D11 backends retain
native persistence behavior; they are not qualified replacements.

Exact native method and Harmony publication checks reject changed or overlapping
hooks at use time. Refusal reports the affected method and patch owners and
explains that its atlas behavior remains native; unrelated supplier features
are not removed. Loading-lifecycle overlap can separately refuse batching while
the cache setting remains independent. The actual frozen fixture contains no
FastLoader, FGL Continued or YaOpt atlas supplier, so real supplier-present and
supplier-option overlap runs remain unqualified. The mixed lane retains Gagarin
and its ordinary behavior. No whole-supplier removal advice follows from C07.

## Expanded-list and failure evidence

Core `7f903c0cdf1a55b83051d62d6608d489609f1729`, observer
`e7cd157721f7ac830388b7c6ce2ce59169b36d50`, is recorded separately from the
earlier first proof. The `xml-expanded` lane includes VFE Core/Medieval 2,
VRE Androids, Combat Extended, Vanilla Genetics Expanded and Gagarin alongside
official content and the two fixture packages. Both atlas settings were on;
PNG caching and first-build image decoding were off.

| Captured functional run | Actual production behavior | Independent result |
| --- | --- | --- |
| `c07-mixed-cold-06` | Eight complete groups written; 226,854,062 stored bytes; source verification also yielded across real frames | Three natural groups and five private cases passed; zero failures |
| `c07-mixed-warm-07` | Eight groups restored, zero writes; eight real bakes skipped | Three actual restored groups and five private cases passed; zero failures |
| `c07-mixed-corrupt-08` | Cache enabled, batching off; damaged Item_False block rejected, its 1,110-texture group baked and replaced; seven other groups restored | Three restored groups and five private cases passed; zero failures |

All three reached the independently observed menu and exited normally with
`automaticTestPassed: true`. Natural comparisons check every color/mask mip,
descriptor, ordered UV rectangle and mesh against a private ordinary native
bake, while preserving borrowed textures and global atlas membership. The
expanded-list size bound selected Terrain, Filth and Plant groups; source
reference tracing identifies Combat Extended and Medieval 2 contributions.
It does not claim that the larger masked groups were independently compared in
these expanded-list captures. The earlier activation proof includes masked
Building and Item groups under its own exact package identities.

Private cases verify GPU-only base/mip changes despite stale CPU bytes, stable
compressed 2x2/1x1 mip identity, ordering/mask/sampler changes, separate CPU/GPU
restoration, rejection of malformed mappings, rollback after failure following
actual allocation and mesh creation, mutation between yielded steps using
frozen owned inputs, and disposal after a real yield. These are focused
correctness probes, not actual gameplay reload or human-visible responsiveness
tests. Full hot-reload and third-party late-hook scenarios remain review and
compatibility obligations rather than inferred live passes.

The corrupt run's prepared receipt binds a one-bit change at offset 524401 in
one copied private group block to entry
`A9AFD3B854951B30915E1050760FB1C57672E9D6718CCB88FF35967C791AEDC6`.
The runtime's native rebuild uses that same entry key. Captured source data and
authored mod textures were not changed. The expected synthetic restore failure
is logged after production completion and must not be mistaken for a startup
failure.

## Later measurement obligations

After independent functional review, use a new explicit unattended window for
matched forward/reverse controls with exact package, settings, content and
backend identities. Measure cache-off native, batching-only first build,
cache-only cold/warm and both selected; keep earlier C01-C06 obligations separate.
Include source GPU verification/readback, snapshot allocation, store handling,
upload, native indivisible work, full menu, colony entry and relevant first use.
Record allocation/residency and actual frame stalls, not only iterator yields or
skipped operation counts. Compare identified supported suppliers when available.
No performance or full acceptance is granted by the functional table above.

## Final package qualification

The final product commit is `b87f9cb0fe59441e528c83fc575485258926b1c7`.
The last product change adds concrete overlap-refusal diagnostics; it does not
replace the recorded expanded-list package identity. The independently built
observer remains `e7cd157721f7ac830388b7c6ce2ce59169b36d50`.

| Identity | SHA-256 |
| --- | --- |
| Final built, deployed and captured `WakeUp.dll` | `e74dff46faa0bf10b0842b4ea632a7ae9ee4eeffd0045a9a423f5d69502319ce` |
| Observer DLL | `f61ac2e421c551e904e2b1be4d6d237ae48dab5e57a23dc1e28beb8f38b67779` |
| Local source-inclusive ZIP | `ea6d30cfa62254409daa368a485a8242cc646434cca848f65297e7f763527ee5` |

The ZIP is retained under ignored
`artifacts/releases/0.3.0-rc.2-b87f9cb0fe59441e528c83fc575485258926b1c7/`.
Its DLL exactly matches the final captured DLL and its source archive is the
same committed revision. Packaging was local; nothing was uploaded or published.

Final activation-lane captures `c07-final-cold-09` and `c07-final-warm-10`
both passed automatic exit and all eight observer checks, including independent
all-mip rendering and mesh comparison of `Building_True`, `Item_True` and
`Terrain_False`. Cold wrote eight groups; warm restored all eight with zero
writes, including the three independently compared restored groups. Stored size
was 117,642,060 bytes. Observer run IDs are
`53a8f624365d4037a2473ca11705039a` and
`60ee9a45ac3f4a6b993345f21cd066e2`, respectively. Cold and warm advanced on 1,903
and 1,841 distinct actual frames, including source verification. These counts
establish real yielding, not smoothness or performance.

`c07-batching-bypass-11` selected batching alone with shared cache bypass and a
copied warm atlas store. Native-equivalent output and all eight observer checks
passed with zero cache reads/writes; every one of the eleven owned atlas file
hashes remained unchanged. It advanced across 62 actual frames and exited
normally. The differing selected work is not a timed comparison.

Final focused checks passed 67 managed tests (atlas storage, startup settings,
shared budget and PNG processing) and all 63 Python fixture tests. These include
invalid records, corruption/truncation, publication cancellation, storage bounds
and shared maintenance. Builds used the matching GOG references. No performance
test, benchmark, UI automation, Steam/Deck promotion or normal-data change was
performed.

`c07-clear-off-12` copied the matching warm store, requested shared clear with
both atlas features off, and exited normally. Every atlas group payload and
index was absent afterward; only the empty ownership marker remained. No atlas
observer was requested with both features disabled. These results are functional
maintenance evidence, not a cache performance comparison.

## Restored handoff and review state

Original core `ba17b033479f28fb5902be7ff9e5da8ac4ad1bc8` and observer
`3d86b1636976678407583e9ba5b7e55c9ddd0f07` were redeployed through `fixture.py`.
Loading Progress was restored; Gagarin had already been restored before the
expanded-list runs. The three starting exclusions are unchanged. The original
profile is restored with candidate and observer disabled; prepared pointer,
Wake-Up user-settings file and helper are absent. All seven initial hashes
(current generation, exclusions, ModsConfig, Prefs, game DLL, core DLL and
observer DLL) match exactly. Fixture audit confirms the matching deployed GOG
contract. No RimWorld process remains. All three children have stopped and
returned file ownership; no build, test, deployment or live run is active.

Raw final evidence includes `restored-identities.json`, `restored-audit.json`,
`captured-summary.json`, `bypass-proof.json`, `clear-proof.json`,
`supplier-inventory.json`, package/build/test logs and restoration transaction
receipts under `artifacts/ecosystem-next-20260910/c07/`. The source-inclusive
package retains its exact product revision; later documentation commits do not
relabel its DLL. No fixture content is tracked.

The checkout and operating role return to the parent steward for independent
review of feature coverage, correctness, compatibility and expected behavior.
The implementation owner does not accept the slice, close its performance
obligations or dispatch C08. Pending review corrections return to the same C07
owner after ownership is transferred. Nothing was merged, pushed or published.

## Steward review: corrections required — 11 September 2026

Reviewed clean handoff `329b2983d7d490d98f9eb94d3e4f70eb32e3987a`, final
product `b87f9cb0fe59441e528c83fc575485258926b1c7`, observer `e7cd157`.
The implementation supplies real complete-atlas reuse and actual frame yields.
Independent review found one correctness blocker and one focused lifecycle
proof gap. C07 has not passed functional review; no C08 dispatch is authorized.

**P1 — frozen pixels and live layout dimensions can disagree.** `BakeOne`
freezes and hashes input copies across yields, then calls native layout on the
original textures (`AtlasRuntime.cs`, layout call at reviewed line 357). Native
layout reads their current dimensions. If a source is resized after its snapshot
but before layout, the result combines new-size rectangles with old-size pixels
and can be stored under the old snapshot key. The present pixel-mutation probe
keeps dimensions unchanged and does not cover this. Layout must use the same
frozen inputs as drawing/identity, or changed layout dependencies must discard
the private group and retry natively. A resize-between-yields check must verify
actual output, mapping and the subsequent cache behavior.

**Focused lifecycle evidence required.** Source review found no scheduler
ordering blocker, but existing cancellation checks dispose `BakeOne` directly.
They do not exercise the scheduler's new global event/callback ownership.
Use the existing observer for one real startup test that publishes a harmless
hook on a guarded lifecycle method after the first scheduler yield. Verify
`DrainAfterLifecycleChange` completes remaining work synchronously, releases
pending/global state, runs cleanup and trailing callbacks once in order, and
allows the original event to complete normally. A throwing queued callback and
following sentinel in that same run can cover native catch-and-continue without
creating a new harness or broad cancellation matrix.

Pixel/store review found no additional blocker in the qualified D3D11 paths.
The steward verified seven functional run identities and captured atlas receipt
hashes, actual hit/write/probe outcomes, the final ZIP/DLL and changed committed
source archive files, and all seven restored endpoint hashes. These include
separately attributed mixed-source, corrupt, final cold/warm, batching bypass and
clear/off captures. Raw proof is
`artifacts/ecosystem-next-20260910/c07/steward-evidence-review.json`.
The existing successful evidence remains valid for its tested conditions; no
repeat of unrelated tests or performance measurements is required by this review.

Corrections return to the same C07 owner from the ensuing exact review commit,
with exclusive edits/builds/fixture ownership on dispatch. Performance remains
prohibited while the owner uses the PC. Visible responsiveness, remaining
supplier/platform compatibility and full acceptance remain separately open.
The absence of a performance window does not waive these functional corrections.

## Geometry and lifecycle review corrections — 11 September 2026

Action `wake-up-c07-review-geometry-lifecycle-20260911` resumed the same C07
owner from verified clean `41ea3bfbf581144d0213e99b62e66b0aa68805bf`. All seven
assigned restored endpoints matched and no RimWorld process remained before
edits. Parent reviewers had stopped and transferred exclusive ownership.

### Frozen pixels now use frozen dimensions for packing

The corrected private atlas temporarily supplies its owned frozen texture list
to ordinary native packing. Both native packing routes, including recursive
retries and the dummy-texture fallback, therefore read the same dimensions and
sampling state used by drawing and cache identity. The synchronous call cannot
yield; a `finally` block restores the original borrowed texture list before
tile creation or return. The original objects remain public tile keys. This
does not resize, replace or destroy any game-owned input.

Mask-dimension validation also uses the frozen color/mask pair. The identity
revision is now `complete-atlas-v2-frozen-layout`, deliberately invalidating
entries from the previous potentially inconsistent geometry implementation.
The disk container version and unified cache policy are unchanged. The product
correction is in `3e72348`; subsequent commits correct observer setup and
assertions without changing the product algorithm.

The focused observer creates eight private 256x256 sources, confirms an actual
input yield after the first source was frozen and before native layout, and
resizes that same source object to 128x384 with new pixels. It checks actual
pixels, texture descriptors and ordered native meshes, not only key changes:

1. The completed frozen result matches the pre-resize private native bake and
   publishes its old snapshot entry, retaining original objects as tile keys.
2. A new store session sees the resized source, misses the old entry and
   publishes a new result matching an ordinary resized native bake.
3. A third reopened store session restores that new entry with native-equivalent
   output and mappings. Borrowed source objects and global atlas membership
   survive; only observer-owned images, groups and stores are disposed.

`c07-review-cold-04` passes every resize assertion and all nine observer checks.
Its ordinary cold startup writes eight complete natural groups. The private
probe's two writes and one restoration occur after the production `complete`
receipt, in a separate store under `FixtureMenuObserver/atlas-resize-cache`;
they must not be counted as natural startup hits or writes.

### Actual late-hook startup scenario

The opt-in `--atlas-lifecycle` fixture case publishes one harmless foreign
postfix on the already guarded native callback runner only after a real atlas
yield and a later actual frame. Observation hooks cover Wake-Up's native-drain
request, native group completion, cleanup and drain return. Existing trailing
callbacks are wrapped to count their actual invocation without changing their
exception behavior. A deliberately throwing queued action precedes a sentinel.
The existing event callback is preserved and invoked once when present.

The observer checks that remaining groups finish natively within the drain's
single frame, previous groups are preserved once, cleanup precedes trailing
work, scheduler ownership is released, the native callback loop catches the
injected exception and continues, and the original event completes normally.
It separately records later empty callback-loop calls outside that event.
The final receipt binds all checks to actual event identity and frame order.
Expected error marker `FIXTURE_EXPECTED_ATLAS_LIFECYCLE_CALLBACK_01` has its own
`expected-callback-injection` receipt; it is not an unanticipated startup error.

This case is separate from the ordinary atlas observer: the deliberate late
hook selects the permanent native-drain path for the remaining startup, so the
normal private batching probes would no longer be testing their admitted mode.
Unity observer-object creation is queued from the worker-side mod constructor
to the ordinary main-thread completion boundary. No UI automation is used.

### Retained intermediate probe failures

`c07-review-cold-01` passed the existing natural checks but the new private probe
used 512-pixel sources, outside the admitted native atlas-source range. The
observer was corrected to 256x256 and 128x384 inputs. `c07-review-cold-02`
demonstrated correct frozen geometry, new-source invalidation and new native
output, but its warm assertion failed because the probe wrote after ending the
same store session. It now opens a distinct store for each simulated startup.
These are recorded observer corrections, not additional product changes.

`c07-review-lifecycle-03` observed one drain, eight native groups, one cleanup,
correct trailing/throw/sentinel/event order and released state, with zero
observer errors. Its final assertion incorrectly counted a later ordinary empty
callback-loop invocation outside the original event. The observer now scopes
that count to the actual event; the separate later call remains in the receipt.
The failed assertion and its exact package/capture remain preserved.

### Final packaged correction results and restoration

Final source-inclusive core and independently built observer both use
`916bba2defaba6ca3414229539de4aa6c09c71ab`. Their GOG reference contract remains
the assigned revision 573. The packaged runtime DLL exactly matches the built,
deployed and captured DLL; earlier packages retain their own identities.

| Final identity | SHA-256 |
| --- | --- |
| Core DLL | `4d26b84836561461aa0a652249ad9ee6e8d48bafddf9ae360b1ec646398fcfb8` |
| Observer DLL | `9571ff8e1cb7414a1697eb32af23af02309b8eaef081b1f95168f6df032ba6ad` |
| Source-inclusive ZIP | `9c6f2a298fd63d994e302b8d16999a1d5036047377a11235e0fcd69066d6fc94` |

The local ZIP is
`artifacts/releases/0.3.0-rc.2-916bba2defaba6ca3414229539de4aa6c09c71ab/wake-up-0.3.0-rc.2.zip`.

| Functional capture | Independent observer run ID | Actual result |
| --- | --- | --- |
| `c07-review-cold-04` | `411f2d9549654dc88cc104786810c583` | Eight ordinary groups written; all nine checks pass, including all resize/output/cache assertions |
| `c07-review-warm-05` | `8d776fb8449b409b93b64e111e3aa08a` | Eight ordinary groups restored, zero production writes; all nine checks pass, three natural compared groups actually restored |
| `c07-review-lifecycle-06` | `3b57d3fa1be346dfbe89348d03316262` | Real late-hook drain passes; eight native groups, one cleanup, one original trailing action, expected throw followed by sentinel, one observed-event callback loop and released event state |

All three have `automaticTestPassed: true` and normal exit; the last also has
`atlasLifecycleTestPassed: true`. The natural comparison groups remain masked
Building, masked Item and Terrain, with matching all-mip rendering, descriptors
and ordered meshes. The production store occupies 117,642,060 bytes in both
cold and warm captures. No timing comparison or speed claim follows.

In the final lifecycle capture, the first yield is observed at frame 2129,
the harmless foreign hook is published at 2130 and all remaining native work
and cleanup finish within frame 2131. Cleanup, the existing trailing action,
expected throw, sentinel, native callback-loop return and event boundary occur
once in that order. The native event originally had no completion delegate;
the observer's boundary delegate records state without replacing an original
action. At menu, pending work and its owning event are null, native callback
execution is false, the callback list is empty and the original event is cleared.
All eight native groups remain published exactly once. There are zero observer
errors, one expected native error-log receipt and one successful sentinel.

Final matching builds passed without warnings; all 63 Python fixture tests pass.
The previous adequate managed pixel/store checks and mixed-list/corruption/
maintenance captures were trusted, not rerun as unrelated correction work.
Raw receipts, intermediate failures, final package and restoration checks are
under ignored `artifacts/ecosystem-next-20260910/c07-corrections/`, including
`review-evidence.json` and `restored-identities.json`.

Original core `ba17b033` and observer `3d86b163` were restored through
`fixture.py`, together with the original profile. Exclusions were unchanged
during this correction round. All seven assigned endpoint hashes match exactly;
candidate/observer are disabled and settings, prepared pointer and helper are
absent. The restored GOG audit passes. No fixture process remains. At final
verification the normal Steam game was identified at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe` (PID 32064)
and left running untouched, as the standing functional-testing permission allows.
All children and correction operations are stopped before ownership returns.

The same C07 owner returns the clean checkout to the steward for independent
re-review. Both requested correction cases now have live evidence; this does
not self-accept C07, dispatch C08, establish performance, or close the separately
recorded visual/platform/broader-supplier and gameplay-reload qualifications.
Default-off controls, native quality and dependencies are unchanged. No normal
data, UI, Steam generation, Deck, security, merge, push or publication operation
was performed. Parent timer and steward-state files were not changed.

## Steward functional re-review — 11 September 2026

**C07 passes functional re-review for complete atlas reuse and independently
selectable batching on the qualified GOG/D3D11 paths.** Reviewed clean handoff
`653fc3625760befb7de43890bde4fe17ae60911f`, correction base `41ea3bf`, and
final core/package/observer `916bba2defaba6ca3414229539de4aa6c09c71ab`.
Performance and full acceptance remain pending; the settings remain default off.

Independent source re-review confirms native packing, including recursive and
older fallback packing, now reads the same frozen copies used by identity and
drawing. The borrowed original list is restored in `finally` before tile keys
are built. Frozen mask validation and the new cache identity reject the older
inconsistent layout generation. The captured resize test checks original native
pixels/descriptors/meshes, changed-size rebuilding, then a genuine reopened-store
warm hit. Both required corrections are resolved.

The actual startup lifecycle probe installs a guarded foreign hook after an
observed scheduler yield. Its captured sequence shows one native drain, eight
completed groups, cleanup and the original trailing action, native handling of
an intentional exception, the following sentinel, then event completion and
release of all pending/global state. This event had no original completion
delegate: the observer verifies the real completion boundary with its injected
delegate, without claiming a pre-existing gameplay callback was exercised. The
later empty callback-loop call is correctly separate from the observed event.

The steward verified three final functional run manifests and atlas receipt
hashes, exact source/core/observer attribution, nine cold/warm observer cases,
eight writes then eight actual restores with zero warm writes, resize/cache
results and lifecycle state/order. The ZIP/DLL and changed committed archive
source files match; all seven original endpoint hashes match and no fixture
process remains. Raw proof:
`artifacts/ecosystem-next-20260910/c07-corrections/steward-correction-review.json`.
Earlier mixed-content, corrupt-entry, batching-only bypass and clear/off evidence
remains separately attributed to its tested packages and was not rerun without
need. Intermediate observer failures remain failures in their original records.

C07's original owner retains measured performance/overhead, coordinated visible
responsiveness, actual supplier-present overlap, gameplay reload and unqualified
platform/CPU-compressed persistence obligations. These are not exclusions or
supplier-removal approval. Actual yields do not establish an eight-millisecond
frame maximum: readback, native packing/compression and whole-group handling
still have indivisible costs. Measure cache-only, batching-only and combined
native/cold/warm paths, including first use and memory, only in a new explicit
unattended window. The owner's return keeps performance testing prohibited.

Returned stopped ownership and this functional review permit C08 integrated
quality/preparation from the ensuing exact review commit. Preserve C01–C07
ancestry and their separate unresolved coverage/performance/release obligations.
C08 must connect explicit quality selection to actual normal game consumption;
portable exports alone cannot satisfy it. No Steam/Deck promotion, merge or
publication follows from this review.
