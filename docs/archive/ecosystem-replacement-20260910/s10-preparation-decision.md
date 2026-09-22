# S10 texture preparation: dependency and design decision

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**9 September 2026 — decision reviewed and owner-approved. No S10 implementation is delivered.**

Recommend an optional, Wake-Up-owned native helper using **DirectXTex's portable CPU library**, built separately for Windows x64 and Linux x64. Users would explicitly prepare selected textures, keep all originals, and use completed output on subsequent ordinary launches. The helper would be bundled with the mod, run only for an explicit preparation request, and never install dependencies in the background. Core loading and a native-output preservation mode would work without it.

This buys a maintained encoder and a small dependency surface, but makes Wake-Up responsible for two executable builds, notices, updates and platform qualification. The Linux library exists; an upstream Linux `texconv` executable does not. Exact shipped helper size, Linux runtime dependencies and image quality are **not yet measured**. Approval would authorize implementing and qualifying this choice, not declaring it ready to ship.

## Scope, evidence and ownership

Branch `codex/s10-preparation-decision` began at assigned accepted base `960dbb48442e21e8ffeac7d69108236fc1ebee68`, with a clean tracked tree. Accepted S01 `691be0a`, S07 `4be5fb7` and S08 `07d024c` are ancestors. Private main and public history were not moved. The steward's existing record assigned this task exclusive documentation/research ownership; it was read, not modified. The preceding team had stopped. Process inspection found no fixture/game launch or active build command; the sole MSBuild node had `/nodeReuse:true` and unchanged 6.84375 CPU seconds across observations. It was left alone. One coordinated read-only agent researched encoder sources.

S09 report `fb30593` rejected its tested Python IDAT expansion: all eight offline pairs lost, 22.765 ms versus 15.642 ms. That route was not repeated. S10 moves optional work into a user-requested preparation session and offers explicit quality changes; neither mechanism establishes S09 first-build acceleration. S04 independent XML, S05 replay implementation, S06 language reuse and S09 first-build acceleration remain deferred, undelivered goals. No permanent omission is approved.

Raw source/API receipts and fresh native decompilation are under [ignored S10 evidence](../../../artifacts/s10-preparation-decision). Native inspection used the existing `artifacts/startup-next/tools/ilspycmd.exe` against fixture `Assembly-CSharp.dll`, SHA-256 `4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28`. No assembly was loaded through PowerShell, and no game, build, dependency installation or encoder benchmark ran. All interfaces and presets below are proposals.

## Encoder comparison and exact source notices

The comparison separates downloadable release sizes from an unbuilt Wake-Up helper. A project-level license is not clearance for every dependency. Current primary-source receipts were checked on 9 September; the pinned ecosystem snapshots remain historical references.

| Option | Maintenance and distribution | Capability and integration cost |
|---|---|---|
| **DirectXTex may2026**, release commit `4feb3e11a020f35b796fc769a74216a555d4f5ef` | MIT. Release 7 May 2026; current main `868198cb4bcbc4e359372e7ba38d7a6dda3a6afa` has a 6 September commit. Official Windows `texconv.exe`: **966,480 bytes**; no Linux executable asset. These bytes are not an estimate of our unbuilt helper/package. | CPU BC1/BC3/BC7 and uncompressed formats; explicit alpha, color conversion, resizing and mip filtering. Linux library is exercised by upstream GCC 12/13/14 CI with PNG/JPEG. Tools are built only under `BUILD_TOOLS AND WIN32`; Wake-Up must supply a thin portable entry point, pin dependencies and maintain two builds. Recommended. [Release](https://github.com/microsoft/DirectXTex/releases/tag/may2026), [pinned build](https://github.com/microsoft/DirectXTex/blob/4feb3e11a020f35b796fc769a74216a555d4f5ef/CMakeLists.txt), [Linux CI](https://github.com/microsoft/DirectXTex/blob/4feb3e11a020f35b796fc769a74216a555d4f5ef/.github/workflows/wsl.yml). |
| **Compressonator V4.5.52** | Available/nonarchived, but latest release is 31 January 2024 and last main commit `f4b53d79ec5abbb50924f58aebb7bf2793200b94` is 19 June 2024; recent active maintenance is not established. CLI Windows ZIP **65,898,132 bytes**; Linux tar.gz **19,644,847 bytes**. Installed/trimmed sizes unmeasured. | CPU block compression, DDS, resizing/mips and alpha/color controls, with ready CLI distributions. Broader codecs/plugins and nonuniform notices add packaging work; a trimmed BC-only SDK helper also needs a custom build. No measured speed/quality advantage here. [Release](https://github.com/GPUOpen-Tools/compressonator/releases/tag/V4.5.52), [CLI documentation](https://compressonator.readthedocs.io/en/latest/command_line_tool/commandline.html), [distribution notices](https://github.com/GPUOpen-Tools/compressonator/blob/V4.5.52/license/readme.md). |
| **todds 0.4.1**, historical reference | Release 16 November 2023; Windows ZIP **3,846,213 bytes**, Linux x86_64 ZIP **12,013,528 bytes**. Inspected source `bf78b2303b00ad0f99ffb409a4827eb305650820` is archived. MPL-2.0 plus dependency notices. | BC1 opaque/BC7 alpha, resizing, orientation and mip filters, progress/dry-run are useful references. Timestamp refresh and direct destination writes do not meet our contract. Maintaining a fork plus its wider dependency set is avoidable work; do not ship it on the assumption it is maintained. [Release](https://github.com/todds-encoder/todds/releases/tag/0.4.1), [pinned source](https://github.com/todds-encoder/todds/tree/bf78b2303b00ad0f99ffb409a4827eb305650820). |

DirectXTex supports low-level alpha and sRGB controls, but no encoder automatically understands RimWorld masks. Its [compression](https://github.com/microsoft/DirectXTex/wiki/Compress), [mipmap](https://github.com/microsoft/DirectXTex/wiki/GenerateMipMaps) and [PNG/JPEG integration](https://github.com/microsoft/DirectXTex/wiki/Using-JPEG-PNG-OSS) documentation establish available operations, not native fidelity or game support.

Recommended dependency candidates are pinned below. They are source-reviewed choices, **not a tested compatible lockfile**. Static linking reduces file count, not notices or update responsibility.

| Component / proposed pin | Exact notice and intended use |
|---|---|
| DirectXTex `may2026` / commit above | [MIT](https://github.com/microsoft/DirectXTex/blob/4feb3e11a020f35b796fc769a74216a555d4f5ef/LICENSE); retain Microsoft copyright/permission text. CPU library only. |
| DirectXMath `jun2026`; DirectX-Headers `v1.619.5` | [Math MIT](https://github.com/microsoft/DirectXMath/blob/jun2026/LICENSE), [Headers MIT](https://github.com/microsoft/DirectX-Headers/blob/v1.619.5/LICENSE); Linux source/header dependencies. No DirectX GPU device/runtime is needed for the CPU encoder. |
| Windows decoder | OS Windows Imaging Component (WIC), using system `windowscodecs`/`ole32`; no copied WIC binaries. Its decoding/metadata behavior need not match Unity or the Linux decoder. |
| Linux PNG: libpng `v1.6.58`, zlib `v1.3.2` | [PNG Reference Library License v2 / libpng-2.0](https://github.com/pnggroup/libpng/blob/v1.6.58/LICENSE), [zlib license](https://github.com/madler/zlib/blob/v1.3.2/LICENSE). Preserve notices and mark modifications as required. |
| Linux JPEG: libjpeg-turbo `3.2.0` | [LICENSE.md](https://github.com/libjpeg-turbo/libjpeg-turbo/blob/3.2.0/LICENSE.md), [README.ijg](https://github.com/libjpeg-turbo/libjpeg-turbo/blob/3.2.0/README.ijg): IJG terms for libjpeg API, BSD-3-Clause for TurboJPEG/build system, zlib terms for SIMD code. Preserve full notices and the Independent JPEG Group product acknowledgement. Do not ship cjpeg/djpeg or their unnecessary libspng tool dependency. |
| Build/runtime closure | Disable `BC_USE_OPENMP`, DX11/DX12 and OpenEXR paths; no GPU/OpenMP runtime proposed. Actual C/C++ runtime imports, static/dynamic policy, Linux glibc floor and compiler/CPU requirements must be recorded after an approved build. Their redistribution terms are not inferred from DirectXTex's MIT license. |

Compressonator's CLI/core/framework notices are MIT, but its full [license folder](https://github.com/GPUOpen-Tools/compressonator/tree/V4.5.52/license) also lists OpenCV 2.40/OpenEXR 1.4.0 BSD-3-Clause, DirectXTex/ImGui/JSON MIT, [ARM ASTC EULA v1.3](https://github.com/GPUOpen-Tools/compressonator/blob/V4.5.52/license/astc/license.txt) and [Ericsson ETCPack restricted-purpose SLA](https://github.com/GPUOpen-Tools/compressonator/blob/V4.5.52/license/etcpack/licence.txt). Include the required license folders for any distribution. A trimmed package may omit unused codecs, but its linked inventory has not been audited. Notice-folder versions do not prove exact release-binary dependencies.

Pinned todds adds OpenCV, TBB, hwloc, Boost, fmt and optional Hyperscan/mimalloc dependencies; its full binary notice closure is unverified. Its [bc7enc_rdo notice](https://github.com/todds-encoder/todds/blob/bf78b2303b00ad0f99ffb409a4827eb305650820/thirdparty/bc7enc_rdo/LICENSE) distinguishes Apache-2.0 `bc7e.ispc`, MIT-or-Unlicense remainder and LodePNG's zlib-style notice; libspng is BSD-2-Clause and miniz MIT. MPL-covered changes retain source/notice obligations even behind a separate executable. This checkpoint copies no third-party implementation.

The pinned [RimSort distribution code](https://github.com/RimSort/RimSort/blob/3b13dfc32dd22712e655cbd02277dbdd1230d84e/distribute.py#L361) still retrieves `joseasoler/todds/releases/latest`, redirecting to archived upstream; no maintained RimSort encoder fork was established. Its GPL-3.0 controller is a workflow reference, not copied code. Pinned FGL Continued `0132c10a3185f578815fb1dba012b7b24f78d5e3` has [MIT text, copyright Taranchuk 2022](https://github.com/mushroomTW/FasterGameLoading---Continued/blob/0132c10a3185f578815fb1dba012b7b24f78d5e3/LICENSE). Its TextureDownscaler reloads cached image bytes through `LoadImage`, then `Compress(true)`/`Apply`; this is not a finished native-byte preservation mechanism. Its safe PNG dimension bound is a parser check, not our storage/memory budget. These findings come from the requested [comparison and source snapshots](../../../artifacts/ecosystem-review-20260909/comparison.md).

The author's successor [imutate](https://codeberg.org/joseasoler/imutate) was screened: source `f77be1d50e4d1ac4099cdcc80ad567cf8682b95341068ce87bdbb614812319e7`, 26 August 2026, AGPL-3.0-or-later, manifest 0.1.0. Current API/source receipts establish activity, but no usable release binary, package size or finished PNG/JPEG/DDS encoder contract was established. Do not treat this successor name as a ready fourth backend. The [research note and saved primary receipts](../../../artifacts/s10-preparation-decision/encoder-research/recommendation.md) retain exact lookup details and distribution byte counts.

## Proposed owner decision and user experience

The specific recommended decision is:

> Approve development of an optional bundled Windows x64/Linux x64 CPU helper based on pinned DirectXTex, with explicit source-preserving, full-size recompression and smaller-texture choices; no automatic downloads or original-file edits. Approve a separately displayed prepared-output budget of at most 512 MiB, initially retaining an 8 MiB complete entry limit. Require a bounded Linux build/package check and image-output qualification before calling either helper supported. Leave the existing PNG/JPEG cache limits and defaults unchanged.

The separate 512 MiB category can add that much disk use beside the existing 512 MiB PNG/JPEG cache: up to **1 GiB combined category budgets**, plus explicitly bounded staging described below. This is a proposed additional category budget, not an already-approved change. If the owner declines the helper, native-output preparation remains useful; S10's quality choices remain deferred rather than silently removed. If the owner declines the extra budget, revise the capacity decision before implementation. Steward or reviewer agreement is not owner approval.

Users would open **Prepare textures**, choose active mods/folders or individual images, review eligible/skipped counts and disk estimates, choose one preset and press **Prepare**. No default selection changes texture quality. Progress shows completed, reused, skipped and failed items; **Cancel** stops new work, and **Resume** rescans and reuses only still-fresh completed entries. Completed output becomes eligible on the next normal launch; preparation does not replace currently displayed game objects. **Use prepared textures** is explicit and defaults off. Disable it to return to originals; **Clear prepared textures** removes only Wake-Up-owned output. S01's central bypass/rebuild/clear action applies consistently without enabling this feature.

| Proposed preset | What changes | Initial admission |
|---|---|---|
| **Preserve native output** (first/default choice) | Use the game's existing PNG/JPEG decoder, mip generation and compression, then capture its finished bytes and sampler settings using S07's approach. No external recompression or resizing. | Supported S07 source/platform contracts and existing size limit. Authored DDS stays original and is reported as already prepared; copying/repacking it has no new benefit premise. |
| **Full size, recompressed** | Keep dimensions; opaque color becomes BC1, color with transparency becomes BC3. These GPU block formats reduce storage; both are lossy and may differ from native output even when visually similar. | Verified ordinary color PNG/JPEG, supported mip layout; no unproven mask/data conversion. BC7 is a possible later measured refinement, not a native-equivalence claim or a fourth initial preset. |
| **Smaller textures** | Same explicit lossy policy, additionally halve dimensions once, never upscale. | Verified ordinary color assets with supported resulting dimensions. Explain smaller/duller fine detail; show exact resulting dimensions. Do not silently resize to satisfy storage or block alignment. |

Preservation is genuine only when finished bytes, mip levels (smaller images for distant rendering), color interpretation, orientation and sampler/readability properties match native processing. It needs Unity on its main thread, in an explicit menu preparation session after the first load. Existing S07 capture is startup-scoped: this requires a separately guarded short capture session reusing its implementation, **not** simply leaving the startup hooks active forever. Already-loaded textures may be unreadable; budget a new native decode/capture, not a free copy. Reuse an existing validated exact result where available. No standalone/headless native encoder or faster first launch is promised. It still provides selection, preparation ahead of later launches, progress and cancellation; the quality-changing helper is a separate useful part of S10.

Masks store control values rather than ordinary color. An alpha channel alone does not identify a mask. Keep masks, unknown texture roles, small/nonconforming dimensions, PSD, bundles and special color metadata on the native path initially. This is an explicit coverage gap: inspect actual color/mask associations and qualify pair handling before enabling transformed pairs. Never infer safety solely from `_m` or a directory name. Native preservation can include a mask only within its proven ordinary image contract. Do not introduce automatic gamma, alpha premultiplication, transparent-pixel removal, sharpening or mip omission under the preservation label.

For the first lossy implementation, propose power-of-two images with each output dimension at least four, full mip chains through 1x1, and explicit box filtering in linear-light color with separately filtered straight alpha, then sRGB color output. Choose BC1 only when decoded alpha is entirely opaque; otherwise BC3. Record the orientation transform needed to supply Unity's bottom-row-first payload and verify it with asymmetric markers on both platforms. These are quality-changing policies requiring validation, not inferred native behavior. Keep unsupported color profiles, uncertain role/alpha interpretation and changed mip contracts on the original route. No preset may silently remove a mip level or change a mask's channel meaning.

## Native selection and the shared S11 routing contract

The freshly inspected [native types](../../../artifacts/s10-preparation-decision/native) and current [PngRuntime](../../../src/WakeUp/Textures/PngRuntime.cs) establish these boundaries:

1. `ModContentPack.GetAllFilesForMod` uses actual `foldersToLoadDescendingOrder` and keeps the first exact relative filename. Active load-folder conditions, game-version folders and their precedence matter. Obtain discovery from these runtime structures, not an independent recursive scan of Workshop roots.
2. `ModContentLoader<Texture2D>.LoadAllForMod` then applies DDS sibling preference. Its inspected suffix logic removes four characters even for `.jpeg`; do not silently repair that behavior in S10. Preserve the actual selected file and ambiguous cases. `ModContentHolder.ReloadAll` normalizes slashes, removes the extension and retains the first duplicate logical path.
3. `ContentFinder.Get` searches mod holders in reverse active order, then Unity Resources, then bundles. Per-mod holders also have direct callers and folder enumeration. Prepare selected effective files within each selected mod, mark cross-mod shadowed files separately, and never skip their ordinary eager load merely because global lookup usually chooses another mod. A global-winner-only filter would change direct-holder semantics.
4. S07 intercepts guarded nongeneric callsites and uses private copies of native methods, preserving holder order and object ownership. Loading Progress has its own guarded bridge. S10 should extend that shared texture admission boundary rather than detour a generic `ContentFinder` or create a second competing enumeration. An unsupported supplier keeps its original route.

One proposed contract is sufficient for both slices:

```text
SourceSelection = contentType, logicalPath, providerInstance, physicalSource,
                  sourceKind, loadGeneration, selectionContract
PreparedTextureResolver.TryRead(SourceSelection, DesiredOptions, PlatformContract)
    -> Original(reason) | Prepared(ValidatedPrivatePayload, TextureDescriptor)
```

`SourceSelection` is an observation of actual native selection, not permission to change it. Provider identity includes package ID **and** canonical root/instance, selected load folder and physical source; package ID alone is insufficient. Preserve case distinctions on Linux and native lookup comparison rules. `selectionContract` records selection-policy version and relevant native/supplier method contracts. Record the ordered active providers/load-folder conditions in the discovery receipt; use a new load generation on reload/order/folder changes. Do not make a persisted mod-list hash substitute for current file discovery.

S10 calls the resolver for the exact selected file during eager loading, constructs a distinct texture, and retains the original `LoadedContentItem.internalFile`, logical name, holder publication order and disposal owner. S11 may later remember **successful source selections** and call this same resolver after revalidating generation/provider state. It must not build a second prepared-output table, cache missing assets indefinitely, or treat an empty deferred holder as proof of absence. Resources/bundles and dynamic providers retain ordinary fallback. On-demand loading is optional/off and is neither implemented nor needed here.

## Keys, output format and storage

Use [S01 OwnedCacheStore](../../../src/WakeUp/Caching/OwnedCacheStore.cs) ownership, envelope validation, atomic publication and maintenance. Proposed category: `<save-data>/WakeUp/PreparedTextures/v1/<KEY>.bin`, with S01's lock, usage metadata and recognized pending-file names. Register its maintenance with the centrally consumed `CacheLaunchPolicy`; the PNG store remains `WakeUp/PngCache/v1`. An item contains a bounded manifest **inside the same envelope** as its texture bytes, avoiding a separate manifest pointer that could publish before the data. A status/index file is advisory only.

```text
KEY = SHA256(canonical length-prefixed encoding of:
  schema + source-selection identity + SHA256(complete original bytes)
  + resolved options + encoder/build digest + native/platform contract)
```

Encode fields unambiguously, not by joining arbitrary paths with delimiters. Resolved options include actual dimensions, format, alpha/color policy, orientation, mip count/filter and all sampler fields. The encoder identity includes algorithm revision, pinned dependency/build recipe and executable SHA-256; native preservation includes S07 pipeline identity, game module identity, Unity version, graphics backend/capture path, compression/compute settings and relevant admitted hooks. A changed encoder must not select an old result merely because the preset name stayed the same. Output manifest includes payload length/digest and per-mip offsets/lengths, texture/graphics format, filter, wrap U/V/W, anisotropy, bias and final nonreadability. Hash every current input before a persisted hit; timestamp and length are hints only. Keep hashes of the actual bytes read for preparation and recheck current contents/selection before publication. Interrupted or changed files yield a miss/retry, never a stale hit.

For a future transformed color/mask group, key all members and publish the whole group as one entry or retain originals for the whole group. Initial transformed admission excludes these groups until association and aggregate-size behavior are qualified. Identical bytes from separate providers can share immutable storage only in a future justified change; they must never share mutable Unity textures.

The native DDS reader accepts DXT1/BC1, DXT5/BC3 and treats `DX10` as BC7; it does not establish arbitrary DX10-format acceptance. It requires a filesystem file and creates textures with `linear:false`, trilinear filtering and anisotropy 0. Native PNG/JPEG loading uses anisotropy 2 and has different mip/compression paths. Therefore a prepared DDS is **not** automatically a drop-in native-equivalent file. Parse and strictly validate helper output into the bounded descriptor/raw-block representation; restore using a shared S07-style adapter with explicit properties. Do not pass an S01 envelope or a memory virtual file to `ModDdsLoader`. Check real platform format support and fallback; BC4/BC5 masks and BC6H are not implied by backend support.

### Capacity is a real limitation

S07 caps pixel payloads at 8 MiB and its category at 512 MiB. S01 reads whole entries and its writer also makes bounded in-memory copies; `CanPublish` is an advisory early admission check, not a reservation across external work. Proposed S10 **complete payload including embedded manifest** remains at most 8 MiB initially. This cannot cover every texture:

| Square texture, full mip chain | BC1 bytes | BC3/BC7 bytes | RGBA32 bytes |
|---|---:|---:|---:|
| 2048 | 2,796,216 | 5,592,432 | 22,369,620 |
| 4096 | 11,184,824 | 22,369,648 | 89,478,484 |

These are layout calculations, not encoded files. DDS headers, manifest and the 108-byte S01 envelope add overhead. Even 4096 BC1 exceeds 8 MiB. Report it as **too large for current prepared storage** and retain original loading; do not truncate mips, silently downscale, split entries or raise constants to make the job pass. Larger entries/large native-preservation coverage require an explicit follow-up capacity and memory decision. A 32 MiB prepared entry could admit 4096 BC3 but still not 4096 RGBA; it is not proposed as an automatic fix. This unresolved coverage must remain in combined-release review.

The new category must charge output plus old generations, pending publication and usage metadata against its proposed 512 MiB ceiling. A one-item helper scratch area is additionally bounded by its admitted input snapshot plus at most one 8 MiB output. Show that temporary requirement separately; it is not covered by S01's current accounting. Do not copy a whole modlist into staging. Decoded input/workspace can be much larger than compressed output: require a concrete input-dimension/decoded-byte admission bound before encoder work; measure the actual backend workspace once approved. A low output size alone is not a memory bound. Keep one item in flight initially, without a worker pool or a global quota framework.

## Narrow helper protocol, cancellation and failure

Use an exact bundled executable path, process arguments as separately escaped arguments with `UseShellExecute=false`, and a versioned JSON request file under Wake-Up staging. Never construct a shell conversion command or pass a mod directory for the helper to walk. The controller owns discovery, permissions, final validation and store publication; the helper sees one read-only input snapshot and one designated temporary output.

```text
request v1: jobId, itemKey, expectedHelperSha256,
  input {absolutePath, length, sha256}, output {absoluteTemporaryPath, maxBytes},
  options {width, height, format, colorPolicy, alphaPolicy, orientation,
           mipCount, mipFilter}, limits {decodedBytes, threads:1}
response v1: jobId, itemKey, encoderBuildId,
  status: completed|unsupported|failed|cancelled,
  output {length, sha256, width, height, format, mipOffsets, mipLengths}, reason
```

Require a version/capability handshake before any conversion. Write bounded progress JSON lines separately from diagnostics; exit success without a complete matching response is failure. The parent independently checks output magic/header, dimensions, permitted formats, mip layout and complete digest; helper declarations are not validation. For an initial thin DirectXTex wrapper, disable internal OpenMP and process only one item. There is no network, update service or automatic dependency install.

After early capacity admission, snapshot/hash the exact selected source, invoke the helper, validate its staged output, revalidate the original and selection, then call `Publish`. S01 rechecks capacity and writes, flushes, rereads and atomically moves/replaces the completed envelope while retaining the previous generation on failure. Readers use `Read<T>` to validate and return private bytes under the owner lock; they do not validate a pathname then reopen another generation. The resolver never routes `.pending` or helper scratch files. Helper processes never hold the final store lock or publish directly.

Cancel stops scheduling immediately. A cooperative helper exits at a safe stage boundary; if it does not, terminate only the verified child started for that job, wait for its exit, then clean its owned scratch. A native Unity operation cannot be interrupted halfway: finish the current admitted item, release temporary textures, then stop. Present that distinction in progress. Only after exit/join may Resume start another job. Completed entries remain reusable by content key; partial entries never count as completed. Recheck selection/options on Resume, not just the prior job ID.

Missing/wrong helper, unsupported platform or role, changed source, full disk, lock contention, cancellation, corrupt output and failed texture restoration all leave ordinary loading available. For an invalid helper-derived mode, choose `Original` rather than secretly applying a different preset. If needed, the user explicitly chooses preservation. Failed restoration destroys only the newly created object before invoking the original once; it must not partially publish a holder object. No source deletion, replacement, timestamp rewrite or sibling-DDS cleanup occurs.

## Estimates and bounded validation after approval

**Dry run** performs active discovery, small header reads and key/availability checks without decoding/encoding. It shows unique selected inputs, native DDS/already-fresh counts, shadowed/unsupported/oversized counts, original bytes, exact output layout estimate where known, category headroom and peak scratch requirement. A fully verified freshness pass reads/hashes whole sources and has its own scan cost; label cheaper metadata-only status as unverified. Native output size may be a conservative range until the platform path is known.

Compressed mip bytes are `sum(ceil(w/4) * ceil(h/4) * blockBytes)` over admitted mip dimensions, with 8-byte BC1 or 16-byte BC3/BC7 blocks. Uncompressed size is the sum of pixel count times bytes per pixel. Report prepared disk use as additional space because originals remain. Space needed to rebuild includes both generations. Do not count duplicate native/quality outputs as free.

Until a backend is built, duration is **unknown**, not an invented MB/s number. After approval, time a small representative sample stratified by dimensions, alpha and format, include read/hash/encode/validate/publish costs, and show a range derived from those samples. Preparation duration and later startup time are separate measurements; calculate break-even launches only after actual matched startup savings exist. Rejected historical warm DDS packs and S09 IDAT expansion supply no speed claim for this design.

Validation should answer the specific open questions and then stop:

1. Build the thin helper once per target from pinned sources after dependency approval; inspect actual linked/imported libraries, notices, packaged bytes, CPU requirements and the oldest intended Linux userspace. A build/handshake failure may change the backend decision before UI work. No shipping bundle until this is understood.
2. Use a small synthetic image set with asymmetric orientation markers, transparent edges, opaque RGB, grayscale/JPEG, known masks and awkward dimensions. Preservation requires exact native bytes/properties where supported; lossy presets require explicit decoded pixel/alpha and mip comparisons plus visual review. No quality result is established by a parser round trip. Linux/Windows engine output and mask/atlas compatibility need separate authorized fixture work.
3. Focused adapter checks: active overrides/version folders, DDS siblings including `.jpeg`, same-size/time content edits, encoder/options changes, no helper, invalid response, bad mip/header, quota-before-work, failed replacement, cancel/resume, reload and source hashes unchanged. Reuse S01 storage tests instead of rebuilding their harness. Exercise native and Loading Progress routes when integration exists.
4. With separate live authorization, qualify settings flow, rendered output/samplers, complete eager startup and colony first use on each platform. Report source, package, deployment and captures separately. No performance or gameplay claim before those checks; do not widen this checkpoint into a build/test campaign.

## Checkpoint closeout

This checkpoint changes documentation and ignored research only. No production behavior, setting, dependency, build package or deployment changed. Native preservation outside startup, helper binaries, lossy quality qualification, mask-pair support, larger entries, Linux packaging/runtime support, preparation UX and measured benefit remain unimplemented or unqualified. Prepatcher remains required; on-demand remains optional/off. No deployment, launch, Deck access, normal game-data change, UI automation, security change, push or publication occurred.

Focused documentation checks found no missing local links; independent mip-layout calculations matched the table, and `git diff --check` passed. The source researcher performed one read-only factual review with no material corrections requested. [Checkpoint evidence](../../../artifacts/s10-preparation-decision/checkpoint-checks.json) records native-source hashes and calculations. These checks validate this decision record, not product behavior; no unchanged runtime tests were rerun.

The S10 decision task and its read-only researcher release checkout/build ownership to parent steward `01a08711-7899-7ff3-a1b5-4a0b105ba661` for review and stop. The next action is steward review of this concrete recommendation followed by the unresolved owner dependency/budget decision. Do not start helper-dependent implementation, bundle a dependency or begin S11 from this report alone.

## Parent-steward review — 9 September 2026

Accepted report `c0d755f33bbd343685e65a280ad3e9b7ab6e6f6c` as decision preparation only after reviewing the actual documentation diff, source/license/build receipts, native selection and storage proposal. The pinned DirectXTex build separates its portable library from Windows-only tools; a Wake-Up Linux executable and its actual runtime/dependency closure remain work to qualify. No product implementation, backend benchmark, live test or release readiness is accepted here.

The steward recommends owner approval to develop and locally package the optional Windows/Linux helper and the explicit presets described above, with a separate prepared category capped at 512 MiB and complete prepared entries capped at 8 MiB initially. Combined category allowance can reach 1 GiB; bounded one-item staging is additional. Large images that do not fit stay on their ordinary route. Exact source pins are candidates for a verified build, not a claim that their combined build has passed.

The report makes the previously unresolved dependency and additional disk-use decision concrete. No correction or further research campaign is needed before that decision. Checkout/build ownership has returned; the recurring steward check is paused awaiting the owner. Approval to continue this implementation would not authorize deployment, game launches, Deck access or publication. Deferred S04/S05/S06/S09 capabilities and all live/large-entry gaps remain recorded.

## Owner approval and continuation — 9 September 2026

On 9 September 2026, the owner approved both S10 proposals and explicitly requested continuation: develop and locally package an optional DirectXTex CPU helper for Windows x64/Linux x64, with explicit native-preserving, full-size recompressed and smaller-texture choices; add a separate prepared-output category capped at 512 MiB, initially retaining the 8 MiB complete-entry limit. Combined category allowance may reach 1 GiB plus bounded one-item staging. Existing defaults stay unchanged and use of prepared textures defaults off. This authorizes scoped dependency/helper implementation and offline packaging checks, not deployment, game launches, Deck access, publication or automatic system dependency installation.

This resolves the earlier pending approval in this report. Treat the unbuilt backend, output quality, Linux runtime closure and large-entry limitations as implementation/qualification work, not a reason to ask for the same dependency/budget approval again. Material deviations still need the owner. A fresh task receives implementation ownership; the decision task remains stopped.

Current platform scope: the owner subsequently deferred Linux helper implementation/packaging/qualification until later and directed this pass to Windows. Keep portable source for later; this supersedes any instruction above to require a Linux build before advancing independent roadmap work. See the [S10 platform deferral](s10-texture-preparation.md#owner-platform-deferral-and-next-checkpoint--9-september-2026).
