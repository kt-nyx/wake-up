# S01 implementation handoff

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

S01 makes the existing PNG cache recoverable and maintainable without changing the textures it represents. It remains off by default. The parent steward accepted corrected source `691be0a` and its offline evidence on 9 September 2026. Live texture output, startup performance and gameplay compatibility have not been qualified for this change. Earlier review dispositions below describe their historical checkpoints.

## Source and ownership

The owner granted the S01 team sole write/build ownership in the single checkout. The other repository tasks were idle at preflight, and no repository build was running. Normal RimWorld was running and was left alone.

The implementation branch is `codex/s01-cache-maintenance`, created from private `main` at `5299ff4`. It fast-forwarded through documentation commit `3205a90`, then imported runtime, tests, build inputs and release metadata from public `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f` in reconciliation commit `bed726d`. A live `git ls-remote origin refs/heads/main` read confirmed that public revision. The imported paths matched the public tree exactly before S01 editing. Documentation was retained, neither main nor the public branches were moved, and the unrelated histories were not merged. There were no unresolved reconciliation conflicts.

Initial S01 implementation is `0cb8961`. The capture-capacity correction is `691be0a8fd8cf87f8824fe4ff52de43ccecff7b1`, following steward review-record commit `a3dde44`; both earlier commits remain in the branch ancestry. This is private-lineage source for steward review; it must not be pushed to public main.

## Behavior and recovery

The existing full source-content key and PNG2 payload validation remain. A new versioned storage directory, `WakeUp/PngCache/v1`, adds an immutable file header (an envelope) containing the input key, storage format version, expected payload length and SHA-256 digest. The old flat PNG2 store deliberately undergoes a one-time rebuild. Only exact uppercase SHA-256 filenames ending in `.bin` or `.bin.pending` are removed from the old directory; unrelated files and source images are preserved. Disabled or bypassed caches do not migrate data.

One owner holds an exclusive file handle for the category and serializes operations within the process. Another owner is refused and normal loading remains available. Readers open one file, validate its complete captured bytes and PNG format while holding the owner lock, then return private data. No reader validates one generation and reopens a different one. File deletion after a completed read cannot change the returned pixels.

Publication reserves space for both the old generation and temporary replacement, writes a unique pending file, flushes and verifies it, then publishes it in one filesystem step (an atomic move or replacement). Failed replacement preserves the previous generation. Changed source content still has a different key and cannot use that old entry. Unsupported replacement operations fall back without deleting the destination. Interrupted pending files are unreferenced and removed when the category next opens; failed cleanup disables initialization or remains charged against the session's quota.

Pixel capture still has the **8 MiB entry limit**. The **512 MiB category limit** includes completed envelopes, pending payloads and usage metadata, including space for replacing that metadata. In-flight memory is independently bounded by the entry size and serialized processing; it is not treated as disk reservation. Before expensive capture, the owner checks whether the complete entry and replacement space fit, reclaiming unused entries if necessary. If they cannot fit, ordinary PNG processing supplies the texture without the extra pixel copy or GPU readback. Publication checks capacity again before writing.

Eviction selects the least recently used entries not touched this launch. Each pruning pass removes at most 128 files. Menu completion also prunes entries unused for 30 days and flushes one usage summary; individual hits update memory only. Changed-input entries can therefore be reclaimed by age or pressure without assuming that an incomplete startup scan proves every unvisited file obsolete. Inventory is capped at 32,768 files and admission at 32,764 entries to bound maintenance; excess inventory refuses the optional store. Unknown files are never eviction candidates. Usage metadata is advisory and cannot admit a cache hit.

Settings queue one action, consumed centrally before cache consumers initialize:

| Action | Next ordinary launch | Following launch |
|---|---|---|
| Bypass once | No cache reads, writes or file cleanup | Normal saved settings |
| Rebuild | Skip reads; publish successful replacements for enabled caches | Normal saved settings |
| Clear | Delete owned cache data even when caching is disabled; suppress reconstruction | Normal saved settings |

Explicit development selections leave queued user requests untouched. No maintenance action enables a disabled cache. A failed settings save can leave the request on disk for the next launch; the selected action is still latched consistently for this launch. Storage and settings errors use existing feature-isolation warnings. Clear also writes a compact `cache-maintenance.jsonl` receipt; an incomplete clear is reported as a failure. The settings view scrolls so the additional controls can fit smaller windows, but actual game layout remains unqualified.

The existing PNG completion receipt adds stored bytes, pruned/pending/legacy counts, the latest storage reason and a bounded set of miss/rebuild reason counts. It retains PNG hit/miss/write/error and texture-verification counters. Diagnostics are not themselves a performance claim.

## Small interfaces for later slices

- [CacheLaunchPolicy](../../../src/WakeUp/Caching/CacheLaunchPolicy.cs) supplies one immutable `Current` action and `AllowRead`/`AllowWrite` decisions. Consumers never consume the saved request individually. Future cache adapters must participate in the centrally selected action and add their clear operation to startup maintenance.
- [OwnedCacheStore](../../../src/WakeUp/Caching/OwnedCacheStore.cs) supplies category ownership, `Read`/`Read<T>` with format validation under the owner lock, `CanPublish` for early capacity admission, `Publish`, `Invalidate`, `Complete` and `Dispose`, plus byte counts and rejection reasons. Each adapter supplies a Wake-Up-owned versioned category path, limits and content key. The PNG adapter remains the sole production consumer. There is no global quota scheduler, worker framework, dependency graph or new cache family.
- [CacheDigest](../../../src/WakeUp/Caching/CacheDigest.cs) retains the existing Windows SHA-256 acceleration and managed fallback for both storage and PNG validation.
- [PngCache](../../../src/WakeUp/Textures/PngCache.cs) retains format-specific decoding and pixel-layout checks, adapts the legacy format, and keeps the disk operation boundary separate from texture processing.

Format-specific validators should use `Read<T>` rather than read bytes and later invalidate a key from another thread. The standalone key invalidation operation is for serialized consumer failures; PNG texture restoration remains on its existing main-thread path. Future consumers must keep published input keys content-addressed and own their mutable results.

## Offline evidence and remaining qualification

The original `python scripts/fixture.py test` entry point refused because its general operation guard detected the normal game process. The guard was not changed. The documented offline commands were then run directly with the same frozen GOG rev573 game and Harmony references; the manifest digest and the build's approved reference verifier passed. Temporary test data and build output stayed under ignored `artifacts/`. Python launcher messages in the test log come from mocked unit tests, not game processes.

The check sequence is locked restore, Release build, managed tests and the existing Python fixture/release tests. Local command transcript: [offline-checks.log](../../../artifacts/s01/offline-checks.log); managed receipt: [features.trx](../../../artifacts/s01/test-results/features.trx). The source tree passed 137 managed cases with one existing optional Character Editor skip, and 35 Python cases. Build completed with zero warnings and errors. The repository's tracked-file boundary and whitespace checks also passed.

Focused additions cover interrupted/short/oversized writes; truncated and corrupted envelopes; format-validation rejection; reader snapshots across replacement and clear; competing owners; shared one-launch bypass; metadata-aware quota and least-recently-used eviction; 30-day cleanup; exact legacy filenames; clear/rebuild behavior; and changed source contents with unchanged path, size and timestamp. Existing mip payload and sampler-setting round trips remain. The content-identity test exercises the hashing primitive; the unchanged runtime key construction was reviewed, not executed inside Unity.

No package was created or deployed. No RimWorld launch, Deck access, normal game-data modification, push or publication occurred. Historical 0.2.1 packages and fixture receipts are unchanged and do not identify this candidate. Offline Windows filesystem tests do not establish Linux/Mono replacement behavior, rendered texture fidelity, settings layout, gameplay compatibility or a loading-time improvement. Power-loss durability beyond the filesystem's flushed-write and atomic-publication guarantees is not claimed. A full-store rebuild may skip entries when protecting the previous generation leaves insufficient temporary space; it does not delete that generation to force a replacement. No broader validation framework or benchmark service was added.

Parent-steward acceptance remains pending. After that review, any live qualification requires separate owner authorization and must identify the exact source, package, deployment and captured run.

## Parent steward review — 9 September 2026

**Disposition: correction requested for source `0cb8961`; S01 is not yet accepted.** The S01 task is idle at review. The steward owns this review-record update only; source correction remains with the S01 orchestrator when the owner sends the correction assignment.

Source comparison confirms that reconciliation `bed726d` matches public `b2d83c3` for runtime, tests, build inputs and release metadata. The recorded offline log and TRX support 137 managed passes, one optional supplier skip, 35 Python passes and a zero-warning build. The steward inspected these existing records rather than rerunning adequate tests. No S01 live or performance qualification is established.

One concrete regression needs correction: [PngCache.CanCapture](../../../src/WakeUp/Textures/PngCache.cs:95) now checks only action and entry size. Both CPU capture and the GPU compression-block path use it before copying texture bytes; [the GPU path](../../../src/WakeUp/Textures/PngRuntime.cs:394) requests readback and waits for completion. Disk quota is checked only later in [OwnedCacheStore.Publish](../../../src/WakeUp/Caching/OwnedCacheStore.cs:163), after capture and PNG serialization. When the store is full and all remaining entries are protected or no replacement fits, each subsequent eligible miss can perform this work even though publication will be refused. A full-store rebuild can have the same problem. The pre-S01 implementation stopped capture when disk space was unavailable. This is a source-established wasted-work path, not a measured startup slowdown.

Restore early capacity admission without discarding S01's useful eviction behavior. Before expensive capture, use the store owner to determine or reserve whether the complete entry can be published, including envelope/usage overhead and temporary space while preserving an existing generation. Refuse capture when it cannot fit; permit it when safe bounded eviction makes room. Keep original texture processing and warm hits working, and keep ownership consistent across mip callbacks and failure cleanup. Use the smallest reliable interface, not a new scheduling framework.

Add focused offline checks proving that an unreclaimable full store and an unfit rebuild do not invoke expensive capture, while reclaimable old entries still allow capture/publication and a failed replacement preserves the prior generation. Existing publication-writer tests alone do not cover the earlier PNG capture boundary. Update this handoff with the corrected source and results; retain the 8 MiB/512 MiB limits, default-off behavior and all current operational restrictions. S05 feasibility is still the next planned step after S01 acceptance.

## Capture-capacity correction returned for review — 9 September 2026

**Corrected source: `691be0a8fd8cf87f8824fe4ff52de43ccecff7b1`. Disposition: ready for parent-steward re-review; acceptance and live qualification remain pending.** This follows and preserves implementation `0cb8961` and review record `a3dde44` in the same checkout and branch.

Both CPU pixel copying and GPU compression-block readback now run through `PngCache.TryCapture`. Its key-aware `CanCapture` asks the store owner whether the complete entry fits before invoking the capture callback. The calculation includes the 84-byte PNG payload header, storage envelope, usage-summary space and the previous generation during replacement. It uses the same admission calculation as publication and permits existing bounded eviction of unused entries, protecting the key being rebuilt. Already-fitting entries take a fast check without sorting eviction candidates on every mip callback.

This is an admission check, not a reservation held across callbacks. The current PNG path captures synchronously on the main thread; the current image context carries its own key across mip callbacks and restores the previous context on exit. There is no reservation to release on capture failure, nested calls or completion. Publication rechecks under the same owner, so a capacity change still cannot exceed the budget. A future concurrent consumer must treat `CanPublish` as advisory, not a guarantee that its later publication will succeed. Storage I/O failure can still reject publication after capture.

Ordinary texture application and compression-copy calls remain in their original paths. Warm hits return before capture, disabled/bypass/clear behavior remains unchanged, and the production bounds remain 8 MiB per entry and 512 MiB per store. Focused tests supply a smaller internal store budget to exercise the same arithmetic without creating hundreds of megabytes of test data; that parameter cannot exceed the production limit and is not a user setting.

Five added offline cases cover the requested earlier boundary:

- A store whose entries are all protected invokes no expensive capture callback and still serves a warm hit.
- Reclaiming one unused entry admits repeated capture callbacks and successful publication without evicting the protected hit.
- A rebuild without room for both generations invokes no capture callback and retains the prior file byte-for-byte.
- A failure inside capture leaves the prior generation valid and does not leak capacity.
- Replacement denied after successful capture leaves the prior generation valid, removes pending data and permits another admission attempt.

The existing locked restore, approved frozen-reference check, Release build and full offline suites passed: **142 managed passes, one existing optional Character Editor skip, and 35 Python passes; zero build warnings or errors**. Existing PNG payload/sampler and publication-recovery tests also passed. The tests invoke the same capture gate used by both runtime routes; they do not execute Unity CPU/GPU capture. Wiring before CPU finalization and GPU readback was checked in the source diff. `git diff --check` and the guarded source commit passed.

New evidence is kept separately from the original S01 record: [offline transcript](../../../artifacts/s01-correction/offline-checks.log) and [managed results](../../../artifacts/s01-correction/test-results/features.trx). As in the original S01 work, documented offline commands used the frozen GOG/Harmony references directly while normal RimWorld remained running; fixture operation guards were not changed. Mock launcher messages in Python tests are not live launches.

No package, deployment, game launch, Deck access, normal game-data change, push or publication occurred. This corrects a source-established wasted-capture path; no measured startup speedup, rendered-output result or new platform qualification is claimed. Return to the parent steward for acceptance review.

## Parent steward acceptance — 9 September 2026

**Accepted for implementation and offline checks:** source `691be0a8fd8cf87f8824fe4ff52de43ccecff7b1`, handoff `cf0dc768f5fc35616abc15a225fd489cab77f92c`. The S01 task is finished and returns checkout ownership. The next assignment is S05's read-only replay-feasibility checkpoint.

The steward reviewed the correction in `OwnedCacheStore`, `PngCache` and both `PngRuntime` capture routes, the five added cases, and the recorded correction log and TRX. Both costly capture paths now check complete-entry capacity first; safe bounded eviction still permits useful writes. The advisory check and final locked publication are adequate for the current synchronous PNG consumer. Full protected stores, unfit rebuilds and capture/publication failures have focused coverage without broadening the storage framework.

Recorded evidence supports 142 managed passes, one existing optional supplier skip, 35 Python passes and a zero-warning Release build. Adequate offline checks were not rerun solely for review. No further blocking correction was found. Live rendering, settings layout, Linux filesystem behavior, gameplay compatibility and performance remain unqualified; acceptance permits the next independent phase, not deployment or release.
