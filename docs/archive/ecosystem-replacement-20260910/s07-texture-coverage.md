# S07 faithful texture coverage handoff

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

S07 lets the existing opt-in cache reuse finished JPEG textures as well as PNGs. It adds useful source coverage without changing image quality, increasing quotas or introducing a decoder. Two active 1024-square JPEG sources in the frozen collection fit the existing limits; a larger JPEG still loads ordinarily without capture. This is implemented and checked offline, **accepted by the parent steward for implementation and offline checks**. New JPEG Unity output and loading-time benefit are unqualified.

## Source, package and ownership

- Assigned base: `21a4987779a81206be5ba711adbb9943a7a8ca7f`.
- Source: `4be5fb761d54ebba470a57cab35c40df0f750510`, branch `codex/s07-texture-coverage`, in the existing checkout. Accepted S01 `691be0a`, S02 `436c652` and S03 `0a26702` remain ancestors. Private main and public history were not moved or merged.
- Preflight found the exact base, a clean tree and no game/build process. The preceding team had stopped; this task held sole source/docs/build ownership. One bounded sub-agent inspected evidence and reviewed the diff read-only; it found no blocking issue and released ownership.
- Built package: [4be5fb7](../../../artifacts/fixture-package/4be5fb761d54ebba470a57cab35c40df0f750510), with [receipt](../../../artifacts/fixture-package/4be5fb761d54ebba470a57cab35c40df0f750510.json). `WakeUp.dll` SHA-256: `7205cb11a741c6fa957712173425d340dd7e9eb48fface79a352825a29efc6d8`.
- No deployment or live capture belongs to S07. Existing fixture receipts remain historical. No game launch, Deck access, normal Steam/Workshop/profile/save/cache change, UI automation, security change, push or publication occurred. `steward-state.json` was left untouched.

Checkout and build ownership are released to steward `01a08711-7899-7ff3-a1b5-4a0b105ba661` at handoff. Dispatch reference: `wake-up-s07-20260909-21a4987`. This task stops here; S08 is not started.

## Missing coverage and the selected extension

The existing receipts do not demonstrate a remaining PNG size or layout bottleneck. `png-p17-integrated-build` wrote all 4,320 selected PNGs with zero errors; `png-p19-integrated-warm` hit all 4,320 with no misses. The final 0.2.1 Windows receipt has seven verified hits and no misses/errors/mismatches. The larger supplier-bridge workload selected 39,975 DDS and zero PNG. Ordinary DDS selection therefore remains important: these are different workloads, not evidence of universally uncached image work.

A focused filename/header and load-folder inspection found five JPEG files beneath frozen mod texture directories. Two are in a legacy directory excluded by the owning mod's 1.6 load folders. The remaining three belong to active packages whose current load folders include those roots, with no same-name override or DDS sibling within the owning package. This establishes source eligibility, not an executed runtime load count. See the ignored [coverage evidence](../../../artifacts/s07/coverage-evidence.md) for exact private asset paths, snapshot and receipts, and [hashes](../../../artifacts/s07/reference-asset-hashes.txt).

| Coverage class | Before S07 | After S07 / disposition |
|---|---|---|
| Eligible PNG | Existing processed reuse | Unchanged; no evidenced residual miss in the complete PNG receipts |
| Two active 1024×1024 JPEGs: RGB terrain and grayscale carpet | Always ordinary processing | `.jpg` and `.jpeg`, case-insensitive, enter existing guarded capture/restore; both sizes fit |
| Active 1470×1961 JPEG | Always ordinary processing | Routed through the native pipeline, but its RGB24 base alone is 8,648,010 bytes: capture remains refused above 8 MiB |
| PSD / other source types | Ordinary loader | Not admitted merely because seven PSD files exist |
| Authored DDS | Native DDS preference/loading | Unchanged; no recache or conversion |
| Unsupported graphics or changed supplier/hook contract | Ordinary fallback | Unchanged; no new platform or supplier integration |

The extension is based on source type, never package names or an asset allowlist. It can apply to other JPEG textures satisfying the same native path and byte limits. Two additional sources are a bounded coverage improvement, not an invented large bottleneck or a measured startup gain.

## Native processing and fidelity

The existing read-only ILSpy CLI was used against the approved frozen GOG game and Unity assemblies. Local decompilations are [ModContentLoader](../../../artifacts/s07/ModContentLoader.cs), [StaticTextureAtlas](../../../artifacts/s07/StaticTextureAtlas.cs) and [Texture2D](../../../artifacts/s07/Texture2D.cs). The approved build verifier confirms game SHA-256 `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` and frozen Harmony `7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`. Unity module hashes are in the local hash record; the captured engine version is 2022.3.35f1.

`LoadTexture` uses `ModDdsLoader` for DDS and `LoadTextureViaImageConversion` for other accepted images. The latter loads image bytes, applies the original compression and sampler choices, and releases CPU readability. Unity's version-matched [LoadImage contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ImageConversion.LoadImage.html) describes JPEG decoding into RGB24. S07 uses that native decoder; it does not reproduce JPEG processing or alter color/gamma behavior.

`PngRuntime.Enumerate` now admits PNG/JPEG to the same helper. Both the native reload and reviewed Loading Progress iterator already reach this enumeration. DDS filtering and item order are untouched. Original method hashes, relevant patch guards, main-thread admission and normal holder fallback remain. The Loading Progress iterator retains its stages, yields and publication ownership.

On a miss, the original image method still supplies the texture. CPU capture observes completed native pixels and mips before discarding readability. D3D11 capture retains the native compression blocks for every produced level. A mip is a smaller image level used at distance; preserving the complete chain means all levels produced by the native path, including the deliberately shorter native GPU chain. Restore constructs a fresh texture each time, checks CPU/GPU format and mip count, uploads exact bytes with `Apply(false, true)`, and restores dimensions, filtering, wrapping, anisotropy and mip bias. All three native image-finalization calls request nonreadability. The [raw upload contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.LoadRawTextureData.html) requires matching format, dimensions and all recorded mip levels.

The extracted `SourceKey` retains the existing `png-v4` key bytes: complete current source-content SHA-256, full path, game module identity, Unity/graphics identity, color space, capture path and compression/compute settings. Existing PNG keys remain valid; changed source bytes of the same length still miss. Cache read failures and restore failures retain normal processing/rebuild. There is no shared mutable restored texture.

## Defaults, memory and cost

The existing persisted `pngCache` setting stays false by default and takes effect on next launch. Its checkbox and explanation now say PNG/JPEG. No independent toggle, selector or dependency is added; `--png` and `png-processing.jsonl` remain their historical identifiers. Receipt `calls`, `pngMs` and cache totals now cover both sources; new `jpegCalls` counts JPEG helper entries, including fallback attempts, not successful hits alone.

`PngCache` and `TexturePlatformSupport` are unchanged. Entry pixels remain capped at 8 MiB, category storage at 512 MiB. The existing dimension/mip validation uses bounded long arithmetic and rejects unsupported formats and excessive chains before capture. S01 checks complete-entry capacity, metadata and replacement headroom before CPU copy or GPU readback, then checks again at publication. Bounded eviction, safe failed replacement and central bypass/rebuild/clear maintenance remain intact.

There is no larger-entry admission. CPU capture copies at most one admitted 8 MiB payload; GPU mip arrays total at most that payload and flattening adds one payload-sized copy. Serialization and validation still use the existing additional bounded per-entry copies. Source reading and native decode/upload allocations remain part of ordinary processing plus the existing source-content key path. No new concurrent capture, prefetch queue or retained cross-image payload is introduced. These are source bounds, not a measured peak process-memory claim.

For each newly eligible 1024-square image, calculated processed bytes are 4,194,303 for RGB24/11 mips, 699,064 for CPU DXT1/11 mips, or 1,398,016 for native D3D11 DXT5/7 mips ending at 16×16. Stored files add 192 bytes each (PNG payload header plus S01 envelope), with category usage metadata charged separately. The two source JPEGs total 408,306 bytes; their two D3D11 entries would total 2,796,416 bytes before usage metadata, or 8,388,990 bytes for RGB24. These are layout-derived growth estimates, not captured files from Unity.

Available historical PNG evidence illustrates the tradeoff: 336,043,884 stored bytes from 58,137,529 source bytes; `png-p17` capture took 11,184.875 ms. Warm `png-p19` PNG scope was 1,952.552 ms versus `png-p20` ordinary 4,295.445 ms. Those old results include a different source/package/storage generation and thousands of PNGs. New JPEG capture, restore/upload, first-build cost, break-even launches and complete startup have no measurement. No percentage or speedup is transferred to S07.

## Conditional work and references

Grouped/compressed storage is not justified by two extra eligible files or evidence of material per-file overhead here. The archived DDS pack repeatedly lost after full validation; unchanged-byte repacking is not reopened. Expanded atlas baking was about 577 ms with root layout about 20 ms; no new evidence makes completed-atlas restoration a useful bounded part of this slice. Layout-only reuse is not reopened. A future atlas proposal needs measured avoidable baking plus complete ordered source/mask keys, color/mask payloads and mappings staged before publication. It remains follow-up, not an S07 implementation dependency.

Pinned FastLoader `d95ae707661e46ff50a04b39d90173f6b90ed1e6` raw-store and atlas files were consulted in `artifacts/ecosystem-review-20260909/sources/fastloader`, alongside `comparison.md`. Its raw schema omits wrapping/bias, its texture readback/recompression records one mip, and atlas color/mask publication precedes tile restoration. These patterns were not copied. No license was established in that snapshot or inspected notices, so S07 is an independent extension of Wake-Up's existing code. No new third-party source or dependency is introduced.

S09/S10 retain first-build processing and intentional quality changes; S11 retains on-demand loading, optional/off. Prepatcher remains required and native-helper bundling remains unresolved. S04/S06 are accepted negative cost checkpoints, not delivered caches; S05 replay remains deferred.

## Offline checks and live gaps

Executed `python scripts/fixture.py test --test-filter 'FullyQualifiedName~Png|FullyQualifiedName~TexturePlatform|FullyQualifiedName~Cache|FullyQualifiedName~UserStartupSelection'`: locked restore, approved frozen-reference verification and Release build passed with zero warnings/errors; **84 managed cases and 35 Python cases passed**. [Transcript](../../../artifacts/s07/offline-checks.log) and [TRX](../../../artifacts/fixture-tests/c5797a7225b94454a37a106ef2cccd54/features.trx). Launcher text in Python checks is mocked; the receipt explicitly reports `gameLaunched: false`.

New cases cover JPEG routing and native image call reachability, unchanged PNG key encoding, changed same-length source bytes/platform/settings, complete admitted RGB24/DXT1/DXT5 mip payloads, sampler fields, independent payload reads, final-mip corruption/rebuild and no expensive capture for oversized/invalid layouts. Existing selected cases retain S01 quota-before-capture, failed replacement, activation/defaults, native method/callsite, Loading Progress and platform-path checks. The payload fixtures are synthetic processed bytes; they do not pretend to decode the actual JPEGs or render Unity textures.

Executed `python scripts/fixture.py build` after source commit: approved GOG frozen-reference package and revision checks passed, zero warnings/errors. [Build log](../../../artifacts/s07/package-build.log). The final wording-only settings cleanup was included in this build. `git diff --check` and the tracked fixture commit guard passed. Existing sufficient checks were not broadened into a new harness.

Steward review is the next step. Separately authorized live work must still establish JPEG decoder output, grayscale behavior, complete rendered mip fidelity and sampler/readability equality, native/Loading Progress execution, Windows D3D11 and Linux OpenGL behavior, first-build/warm costs, user-setting layout and any startup benefit. No live rendering, gameplay compatibility or speed qualification is claimed.

## Parent-steward acceptance — 9 September 2026

Accepted source `4be5fb761d54ebba470a57cab35c40df0f750510` and handoff `6f90607356892ff5cdd263e30a8a6852d7a16e9b` after reviewing the actual source/test diff, frozen native loader, coverage record and test/build receipts. The extension routes JPEG through the same original image conversion and bounded processed-payload pipeline. PNG key bytes, native file order, DDS preference, default-off activation and quotas remain unchanged. The 84 managed and 35 Python checks passed, and the source-specific package built cleanly; adequate checks were inspected rather than rerun.

This accepts the bounded coverage improvement, not a measured speedup, rendered JPEG fidelity or release readiness. Synthetic payload checks do not exercise Unity JPEG decoding, grayscale output or actual texture restoration. Those live/platform gaps remain as listed above. Larger entries, PSD, storage regrouping and completed-atlas reuse were not justified here; their conditional scope remains explicit. S04/S05/S06 remain undelivered as recorded in the master plan.

The completed team released ownership. Proceed to S08's independent loading display from this accepted lineage, with offline implementation/checks only and unchanged operational restrictions. No additional owner decision is needed to start S08.
