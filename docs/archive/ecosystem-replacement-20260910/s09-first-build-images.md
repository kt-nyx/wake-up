# S09 first-build image processing checkpoint

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

Expanding PNG image data before handing it to Unity did not pay for itself in the bounded offline prototype. All eight paired comparisons were slower after including preparation and image consumption. The transformation preserved the sampled image data, but enlarged Unity's prospective input 12.50 times. **This route is rejected before runtime integration; the checkpoint is accepted by the parent steward. First-build acceleration remains undelivered.**

This is a negative result for the tested Python preparation pipeline, not a rejection of CPU preparation generally or proof of its cost in .NET, Mono or Unity. No new runtime setting, decoder, helper or dependency is delivered. Existing defaults and behavior remain intact.

## Identity and ownership

- Assigned and verified base: `1ef6d8f68cb1db704a34d9b8926da3696fbe5b16`. Branch: `codex/s09-first-build-images`, in the existing checkout. Accepted S01 `691be0a`, S02 `436c652`, S03 `0a26702`, S07 `4be5fb7` and S08 `07d024c` remain ancestors. Neither private main nor public history was moved or merged.
- Preflight found a clean tree and the matching exclusive assignment in the steward record. The preceding team had stopped. The only listed build process was an idle reusable MSBuild node: its CPU counter stayed at 6.52 seconds across inspections. No competing build was observed.
- Production source is unchanged from the assigned base; its `src` Git tree is `b40689b513cf4feb1dfd18b3e8cc85779f4dfa93`. Tests, build inputs and scripts are unchanged too. The independent prototype and evidence remain ignored under [artifacts/s09](../../../artifacts/s09).
- The focused check built the unchanged runtime at the assigned base. Its test-build DLL SHA-256 is `5e1be4ecb9ae10b66e14f3efd72f1f18f64204b05a0d48fc8a2276cf0fa66eed`. This is a test-build identity, not an S09 package or deployment. **No S09 package was created.** The existing S08 package remains source `07d024cced75b9e17b21b4294b95eccf053d6ee0`, DLL SHA-256 `1eb8f6867c8192214f351c17395b697de8f896c91fccbbe54803a43c6ee07c3b`, as recorded in its package receipt.
- [Evidence identities](../../../artifacts/s09/evidence-identity.json) pin the probe, checks and results. Probe SHA-256: `ec0090616cdf354938b277b9d3102ef9a8ac0c96ee07cad5801be14f50becf27`; result SHA-256: `c0f841c2b631eb5d2279490c5806f2c462631edf4fbf2f775d07707cdfc9e4ad`.

At handoff, checkout/build ownership returns to parent steward `01a08711-7899-7ff3-a1b5-4a0b105ba661`. The read-only reviewer has completed its work. Dispatch: `wake-up-s09-20260909-1ef6d8f`. This task stops without starting S10 or editing `steward-state.json`.

## Chosen class and changed premise

The probe uses 32 real **256×256, noninterlaced, 8-bit RGBA PNGs** from one frozen active-root package. RGBA means red, green, blue and transparency samples. The [sample manifest](../../../artifacts/s09/probe/results.json) records exact private paths, source hashes and sizes. Selection was the first 32 sorted files satisfying this header class in the inspected root, not a production package allowlist.

All 32 have DDS siblings. They therefore represent an **explicitly PNG-selected offline workload**, not the ordinary fixture's selected textures. No DDS was hidden, deleted or replaced; no normal source-selection behavior changed. A narrow review of three active roots also found no unpaired eligible sample there; that is not a fixture-wide inventory or proof of zero eligible work elsewhere.

Archived read-ahead only moved file reads; finalization-only work skipped a later Unity call and failed to show repeatable startup benefit. This prototype instead performs actual compressed-stream expansion before consumption, while preserving the native decoder and finalization. That is the materially different premise. It does not repeat worker-count tuning, DDS packs, GPU readback experiments or the S04/S06 cache encodings. The [archived PNG results](../investigation-results.md#2026-09-07-png-processing-implementations) still govern those historical routes.

## What the prototype does

PNG stores filtered image rows in compressed image-data chunks (`IDAT`). The prototype verifies chunk integrity and structure, expands that compressed stream, and writes a new in-memory PNG using uncompressed DEFLATE blocks. **The filtered row bytes and all other chunks stay identical.** It does not implement a pixel decoder, reverse row orientation, alter transparency or gamma metadata, resize images, create mipmaps, or choose a new texture compressor. The [PNG specification](https://www.w3.org/TR/png-3/#10Compression) defines this lossless stream representation.

File reading and original-content SHA-256 run in the caller. One CPU preparation worker overlaps with ordered image consumption; at most two items are admitted. The offline consumer is installed Pillow 12.2.0, with Python 3.14.3 and zlib `1.3.1.zlib-ng`. These are research tools, not proposed shipped dependencies. Pillow's decoded output is an independent check, not Unity output.

Fresh read-only ILSpy inspection of the frozen game and Unity assemblies confirmed the prospective seam:

- [Native loader](../../../artifacts/s09/research-review/ModContentLoader.cs), lines 184–229: read bytes, construct a texture, call `LoadImage`, retain native non-power-of-two recreation, compression, sampler settings, mip generation, readability release and intermediate-object destruction.
- [Unity image binding](../../../artifacts/s09/research-review/ImageConversion.cs): `LoadImage` enters native engine code; managed offline tests cannot execute or qualify that implementation. Unity also exposes a PNG gamma-behavior toggle. Keeping original chunks and the native decoder avoids inventing replacement color rules. Unity documents automatic upload in its [version-matched API contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ImageConversion.LoadImage.html).
- [Unity thread identity](../../../artifacts/s09/research-review/UnityData.cs) and existing `PngRuntime.Active`: all engine operations stay on the admitted main thread. The existing `ImageTranspiler`/`ReadSource` seam can supply different input bytes; any future adapter must retain original bytes separately for `SourceKey`, then preserve native holder ordering and publication.

The game reference SHA-256 is `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`; ImageConversion module SHA-256 is `3d5bc897d8beb44a0de2f853995b9afe5e5d2398b0d62db72a3ff54f850f4840`. The build verifier independently passed the frozen game and exact Harmony reference checks.

**No S09 adapter was installed.** Existing native and Loading Progress callsite checks execute offline and pass, establishing the retained seam, not execution of this prototype in RimWorld. Integration was stopped at the cost checkpoint. S07 still uses native decode on misses and restores distinct textures on hits. Its full mip-chain rules, format/sampler/readability contract, source-content keys, 8 MiB entries, 512 MiB store, early capacity admission and S01 atomic publication remain unchanged.

## Output, memory and lifecycle evidence

For all 32 sources, original and prepared filtered streams match byte for byte; every non-IDAT chunk matches in order; Pillow's pixels, dimensions and mode match. These are base-image checks. Actual Unity formats, rendered pixels, gamma behavior, complete mip output and upload are untested.

The prototype refuses source buffers over 1 MiB, inflated rows over 8 MiB, more than 256 chunks, unsupported image headers, animated input and malformed checksums/lengths. Admission calculates source, chunk-copy, inflated, prepared and consumer-buffer costs together, allowing at most 32 MiB of reservations plus one bounded pending 1 MiB source. The highest **calculated reservation** in the sample was 8,739,023 bytes (8.33 MiB). This is not measured process memory or a production allocator guarantee; hidden decoder allocations and arbitrary changing files are not qualified. No GPU object exists in the worker. The zlib calls are serial and there is no nested encoder worker pool.

[Eleven focused assertions](../../../artifacts/s09/probe/checks.json) passed, including corrupted/truncated/oversized input, interlace/animation refusal, inflated overflow, an executed preparation failure that successfully consumes the original file, cancellation, restart and unchanged original hashes. Cancellation stops further consumption, attempts to cancel queued work and **waits for running bounded decompression** when closing the executor. It does not interrupt a native decompression call. A canceled two-item submission consumed one item; a subsequent fresh batch completed all 32. Every batch joins its worker before returning. Restart means a new batch/executor after idle, not a tested persistent background service. The prototype writes no prepared image store and publishes no runtime objects.

## Costs and decision

Eight alternating original/candidate and candidate/original pairs include source reads, SHA-256, preparation, synchronization and Pillow decoding/export. Output checks ran before timing, so these are warm-filesystem observations. They are **not complete Unity first-build times**.

| Measured scope, 32 sources | Original | Candidate |
|---|---:|---:|
| Median whole offline pipeline | 15.642 ms | 22.765 ms |
| Paired wins | 8 of 8 | 0 of 8 |
| Input handed to image consumer | 672,158 bytes | 8,400,288 bytes |
| Median summed read/hash time | 3.143 ms | 7.160 ms |
| Median summed image-consumption time | 11.296 ms | 12.354 ms |

Candidate median summed preparation was 16.274 ms: input validation 0.588 ms, inflation/filter-byte validation 7.246 ms, and new envelope/checksum/copy work 8.468 ms. These timers overlap other stages, and their separately calculated medians must not be added to predict wall time. The complete candidate pipeline was about 45.5% slower in this host. All eight pairs lost, including the reverse-order comparisons; there is no basis to retain this implementation or tune its worker count.

Prepared intermediate storage cost is zero because preparation is memory-only. **S07 finished-texture capture, validation, storage, Unity decoding, mip generation, compression and upload were not measured here.** They remain required costs; none is counted as eliminated. Historical first-cache construction already includes expensive capture and storage, so this prototype's small decode scope cannot establish a first-build gain. Warm processed-cache behavior is unchanged because no runtime code was integrated.

The native zlib/Pillow host supplies useful bounded cost evidence but does not predict .NET/Mono scheduling, engine decode or full startup. The read-only reviewer agreed to stop this specific prototype, with these host and memory limits. Reopening requires an evidence-backed mechanism addressing the expanded transfer/envelope cost; a different host alone is not evidence of a useful result. A direct portable decoder or native helper is unimplemented and would need its own equivalence/cost premise and any required dependency decision. No such dependency or quality change was necessary to complete this checkpoint.

## Reproduction and remaining coverage

Run the ignored `python artifacts/s09/probe/probe.py` and `python artifacts/s09/probe/checks.py` for this offline checkpoint. Raw per-pair/per-item timings and exact sample identities are in [results.json](../../../artifacts/s09/probe/results.json).

Executed the documented frozen-reference workflow:

```powershell
python scripts/fixture.py test --test-filter 'FullyQualifiedName~PinnedOriginalBodiesAndPngCallsitesAreAvailable|FullyQualifiedName~SupplierIteratorChangesOnlyTextureCallAndCanBePatched'
```

**Two managed callsite checks and 35 Python fixture checks passed**, with zero build warnings/errors. [Log](../../../artifacts/s09/frozen-checks.log), [TRX](../../../artifacts/fixture-tests/d9846f0268ae4671b2b28db5388f8f7e/features.trx). Launcher messages in that log are mocked test output; its receipt records `gameLaunched: false`. Broader unchanged runtime checks were not repeated.

Pinned todds `bf78b2303b00ad0f99ffb409a4827eb305650820` was consulted for its sequential OpenCV setting, capped TBB parallelism and file tokens. Its pipeline source is MPL-2.0; libspng has BSD-2-Clause terms and other dependencies have separate notices. Its token cap is not a byte cap, and its transformation/mip choices are not native-equivalence evidence. No code was copied or bundled. The pinned [comparison](../../../artifacts/ecosystem-review-20260909/comparison.md) and snapshots still do not establish accessible Image Opt implementation source; the community source claim remains a lead, not code evidence.

First-build acceleration, other PNG classes, JPEG preparation, portable runtime integration, process peak memory, actual Unity output/upload, Windows/Linux engine behavior and full-startup benefit remain unqualified or undelivered. S10 owns explicit preparation/quality choices and the unresolved helper decision. S04/S05/S06 remain separate deferred gaps. Prepatcher remains required and on-demand loading remains optional/off.

No deployment, game launch, Deck access, normal Steam/Workshop/profile/save/cache modification, UI automation, security change, push or publication occurred. Only ignored research outputs and this documentation checkpoint were added.

## Parent-steward disposition — 9 September 2026

Accepted report `fb3059336c4a7632221b19024925e8649904270e` after reviewing the actual ignored prototype, paired result data, focused assertions, identities and unchanged-source diff. The complete tested pipeline lost all eight alternating offline pairs. This is enough to stop this Python IDAT expansion route without a runtime adapter, more worker tuning or broader tests. Existing native callsite and fixture checks are adequate for this unchanged runtime checkpoint.

First-build acceleration is deferred and undelivered; no omission from final replacement scope is approved. This rejects the measured route, not all portable CPU work. The PNG-selected sample had authored DDS siblings, and Pillow consumption does not qualify Unity decoding, mips, compression, upload, full storage cost or startup. Reopening requires a materially different mechanism addressing the measured expansion and envelope costs, with useful eligible source coverage.

The clean checkout and build ownership returned to the steward, and no fixture process was found. Proceed to S10's bounded encoder/packaging decision preparation. A concrete recommendation and source-preserving routing/interface proposal can be prepared within current permissions; native-helper/dependency bundling and dependent implementation remain subject to the owner's unresolved decision. No live/deployment/publication permission is added.
