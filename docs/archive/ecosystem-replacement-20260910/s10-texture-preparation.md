# S10 optional texture preparation

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**9 September 2026 — role/association corrections awaiting steward review; live qualification pending.**

Wake-Up now has an explicit main-menu preparation workflow and an optional Windows
CPU helper. It keeps original images intact, stores completed results separately,
and can use fresh results during a later ordinary startup. Using prepared textures
defaults off. The Linux build cannot be completed on this host because no Linux
build environment is installed; portable build inputs are supplied, without a
Linux binary or support claim.

## Scope and ordinary activation

Open **Options → Mod settings → Wake-Up → Prepare textures** at the main menu.
Choose an active mod and optionally type an exact native image path or folder
prefix. **Scan / refresh dry run** shows selected sources, native DDS, unsupported
items, dimensions, global shadowing, original bytes and conservative additional
disk use. Each provider's own files remain eligible even when another mod shadows
them globally: direct holder access still needs those objects. The path list can
be inspected before starting work.

The three choices are:

- **Preserve native output:** the default. Decode one admitted source with the
  guarded native S07 path on the main thread, capture finished bytes and sampler
  properties, then destroy the temporary texture. Existing loaded objects may be
  unreadable; this deliberately pays for a new decode. Native reduced mip chains
  are preserved exactly. No helper is needed.
- **Full size, recompressed:** explicit lossy conversion to BC1 for entirely
  opaque images or BC3 for any transparency. These block formats change image
  values. Dimensions remain unchanged; every mip through 1×1 is retained.
- **Smaller textures:** the same explicit lossy conversion, with each dimension
  halved once. It never silently shrinks an oversized image or drops mip levels
  to make it fit.

Lossy preparation now admits only compatible physical PNG/JPEG replacements of
RimWorld's `UI/HeroArt/BGPlanet` main-menu background. The verified native consumer
loads this exact name and draws it directly as color through `GUI.DrawTexture`.
Both consumer methods must retain their reviewed bodies and interception state.
The preset is the user's explicit quality choice; no technical certification
checkbox remains. Unknown roles, other artwork, mask pairs and unsupported image
layouts keep ordinary loading. Native preservation remains independently available.
This is one native consumer class, not a classification of all UI or mod textures.
The frozen fixture inventories contain **zero** qualifying background overrides;
this establishes a bounded capability, not current fixture coverage or a speedup.

**Prepare / resume selected content** rescans and hashes current originals.
Progress counts completed, reused, skipped and failed items. **Cancel** stops
scheduling, terminates only the owned helper when necessary, and joins it before
another job can start. Closing the window performs the same cleanup. Completed
entries survive; incomplete output is never published. A synchronous native
image decode finishes before cancellation can be observed and can briefly hold
a frame. No promise of a fixed frame-time bound is made.

After preparing, enable **Use prepared textures** in ordinary settings and restart
normally. The last chosen preset selects the next launch; changing this setting
does not replace currently displayed objects. Disable it to return to ordinary
loading. **Clear prepared textures only** removes this category; S01's next-launch
bypass/rebuild/clear controls apply to both categories without enabling either.
Rebuild bypasses reads; explicit preparation regenerates prepared entries rather
than silently running conversion at startup. Prepatcher remains required.
On-demand loading remains optional/off and S11 has not started.

## Source selection, restoration and freshness

[`PngRuntime.SelectedFiles`](../../../src/WakeUp/Textures/PngRuntime.cs) is shared by
discovery and the existing eager adapter. It uses current native active load
folders, native extension discovery and the reviewed DDS preference, including
the native four-character suffix behavior for `.jpeg`. It does not change holder
publication order, `LoadedContentItem.internalFile`, names or disposal ownership.
The existing guarded native and Loading Progress callsites both reach the single
[`PreparedTextureRuntime.TryResolve`](../../../src/WakeUp/Textures/PreparedTextureRuntime.cs)
boundary. No generic ContentFinder detour or global-winner-only loader is added.
S11 can extend this contract after revalidating provider state.

The embedded, length-framed manifest binds the exact selected physical source,
logical path, original SHA-256, preset, platform contract and helper SHA-256 or
native S07 contract. Its selection fingerprint includes active provider order,
package IDs, canonical provider roots, active load folders and the selected
provider's actual ordered file mapping. File timestamp/size alone never grants a
hit. Preparation rechecks current selection, full source contents, platform and
helper immediately before publication. A new helper/options/source/selection
identity yields a different entry. No missing-asset results are cached.

Lossy identities additionally include `prepared-role-v2` and
`native-bgplanet-gui-v1`, preventing reuse of output admitted by the former manual
checkbox. Immediately before reading or publishing lossy output, the controller
and resolver recheck the guarded consumer and probe the relevant `_m` sibling
basename in every actual active load folder. The captured folder list is not a
cached assertion that masks are absent: newly added files/directories are seen
even when the selected provider, its source and active root/order list are
unchanged. These are bounded top-level probes of one relative name, not recursive
whole-modlist scans per image. Uncertain probes decline. Native-preservation
identities and its admission path remain unchanged.

S01 owns one lock and atomic publication for `WakeUp/PreparedTextures/v1`.
The embedded manifest contains texture dimensions/formats, mip offsets/lengths,
samplers, final nonreadability, exact input identity and raw-payload digest.
Validation checks the full envelope and payload before returning private bytes.
Restoration creates a distinct texture and verifies its actual format/mip layout;
failure destroys that new object and calls the original path once. Transformed
output requires full mip chains. The main thread obtains the current engine's
actual sRGB BC1/BC3 format mapping before publication rather than guessing it.

Native preservation opens an explicit one-item capture scope only after the menu
and existing method/patch/platform guards pass. Its callbacks reuse S07's CPU
capture or D3D11 compression-block readback. The startup hook lifetime is not
extended into arbitrary later loading. Missing/changed helpers, unsupported
platforms, quota failure, bad output and storage errors retain ordinary loading.
The feature does not accelerate S09 first-build loading.

## Bounds and estimates

The separate prepared category permits **512 MiB**, beside the unchanged
512 MiB PNG/JPEG category: up to **1 GiB combined**, with bounded one-item
publication staging. **8 MiB is the complete prepared entry**, including S01's
108-byte envelope, bounded identity, 208-byte descriptor/mip manifest and pixels.
The helper conservatively limits raw output to 8 MiB minus 64 KiB; native admission
uses a conservative full RGBA mip estimate before decoding, so some compressed
images that might fit are intentionally skipped. Large-entry coverage remains a gap.

Only one item is in flight. Encoded input is capped at 16 MiB and expanded RGBA at
64 MiB. The helper enforces a 256 MiB limit on its own Windows process before its
handshake; source-only Linux code requests a 256 MiB address-space limit. These
are different OS mechanisms, not a claim of measured equal memory use. Native
capture uses the smaller admitted image bound and S07 temporary textures; a
precise Unity peak-memory measurement remains pending. Managed source, raw output,
manifest/envelope copies and at most one 8 MiB pending file are bounded separately.
S01 includes pending replacement bytes in its category admission and protects
the prior generation until atomic replacement succeeds.

Dry run reads bounded headers and file metadata, not image pixels. Its output
upper bound includes the maximum framed identity and a conservative usage-record
allowance. Current headroom is informational; S01 rechecks quota before expensive
work and before publish, and may evict old owned entries. Freshness is labelled
unverified until the actual source hash/read occurs. Preparation duration and
future startup savings remain **unknown**; no invented throughput, break-even
launch count or performance percentage is displayed.

## Native helper, reproducibility and notices

The owned helper is a separate executable with an exact path:
`Tools/win-x64/WakeUp.TextureHelper.exe` (Linux candidate:
`Tools/linux-x64/WakeUp.TextureHelper`). It receives the single constant argument
`--stdio-v1`, with a fixed handshake and bounded binary request/response over its
private pipes. It accepts bytes, never source/output paths. This is a routine
binary framing refinement of the approved proposed JSON request, avoiding a
parser dependency and disk request staging. There are no shell-built commands,
background downloads or writable original files. The controller independently
validates dimensions, format, full mip count, exact byte length and child exit.
Cancellation/timeout touches only its own process and joins it.

[`dependencies.json`](../../../native/texture-helper/dependencies.json) records exact
source archives and SHA-256 pins. Windows builds use DirectXTex **may2026** commit
`4feb3e11a020f35b796fc769a74216a555d4f5ef`, DirectXMath **jun2026**, system WIC,
MSVC **19.51.36257.0** / toolset **14.51.36231**, Windows SDK **10.0.26100.0** and
one compile worker. The generated code targets Windows x64's SSE2 baseline,
without AVX/OpenMP/GPU encoder dependencies. Actual PE imports are **ole32.dll**
and **KERNEL32.dll**; Windows Imaging Component is activated through COM using
the OS implementation. The C/C++ runtime is statically linked under the Visual
Studio redistribution terms; no Windows system DLL is copied.

The tested Windows executable is **373,248 bytes**, SHA-256
`ba7fb9a3c70c3559af4fe9c5c25e3c506c8a2195e8fbce68eaa18861c8068abc`.
Its local Tools/notices overlay is **408,386 bytes**, excluding the private
build receipt. Full MIT, PNG/zlib and libjpeg-turbo/IJG notices are retained in
[`native/texture-helper/notices`](../../../native/texture-helper/notices), with the
Independent JPEG Group acknowledgement and build/runtime distinctions in the
[helper README](../../../native/texture-helper/README.md) and top-level notices.

The Linux source/build recipe pins DirectX-Headers **v1.619.5**, libpng **v1.6.58**,
zlib **v1.3.2** and libjpeg-turbo **3.2.0**, with static image libraries. Actual
Linux linkage, compiler compatibility, glibc floor and runtime behavior are
unknown: `wsl --list --quiet` returned exit 1 stating WSL is not installed, and
no Linux compiler/container tool was available. No system tooling was installed.
The next Linux action requires an existing approved Linux x64 build host or
separately authorized tooling setup. No Deck connection is needed or authorized.

Build/package commands after committing inputs:

```powershell
# --fetch only for missing, hash-locked developer source archives; never runtime.
python scripts/build_texture_helper.py --fetch
python native/texture-helper/test_helper.py --helper artifacts/s10-helper/package/win-x64/Tools/win-x64/WakeUp.TextureHelper.exe
python scripts/fixture.py test --test-filter 'FullyQualifiedName~TexturePreparation|FullyQualifiedName~PreparedTextureStore|FullyQualifiedName~PngProcessing|FullyQualifiedName~PngLoadingProgress|FullyQualifiedName~UserStartupSelection'
python scripts/fixture.py build
python scripts/package_release.py --package artifacts/fixture-package/<source-revision> --texture-helper artifacts/s10-helper/package/win-x64
```

The optional packaging flag validates every helper build input, executable and
notice against its receipt/current committed inputs, then combines it with the
unchanged two-file core build contract and source/legal package. It does not ship
the private host receipt. Output remains under ignored artifacts; it neither
deploys nor publishes. Default packaging still works without a helper.

## Evidence, identities and remaining actions

The task verified clean accepted HEAD
`90703879f828f41df82262e44c320ab02d16ceb6` and created
`codex/s10-texture-preparation` directly in the sole checkout. S01 `691be0a`,
S07 `4be5fb7` and S08 `07d024c` remain ancestors. The decision approval is not
reopened. S04/S05/S06/S09 remain undelivered gaps; none is automatically reopened.

An initial integrated managed build passed with zero warnings/errors, **50 focused
managed checks** and **35 existing Python checks**, receipt
`artifacts/fixture-tests/0494bc431e524e64b23634ee35520281/features.trx`.
Later focused checks and final package identities are recorded in the closeout
below; this initial result alone does not validate later corrections.

The real Windows helper passed **15 focused offline cases**:
asymmetric orientation at every mip for both presets, independently decoded BC1
and BC3 output, linear-light final mip, straight alpha/transparent edges, grayscale
JPEG, dimensions/metadata/output limits, malformed input and owned-child
cancellation followed by a new request. See
`artifacts/s10-helper/image-tests.json`, `platform-check.json` and
`package/win-x64/helper-build.json`. Small synthetic image checks establish
offline output behavior, not perceptual quality across game art.

One bounded read-only integration review found and corrected earlier-provider
mask detection, complete-estimate overhead and nonthrowing cancellation callbacks.
The source now also includes focused managed helper-client, native selection,
freshness, hook reachability, setting and store checks. No broad new harness or
unchanged full benchmark campaign was added.

**Still unqualified:** Unity rendered output, native menu decode/capture,
main-menu layout and responsiveness, actual selected-role safety for real art,
mask/atlas/gameplay compatibility, Linux build/runtime, measured preparation cost,
peak Unity memory, complete startup benefits and final release readiness. No
deployment, game launch, normal Steam/Workshop/profile/save/cache change, Deck
access, UI automation, security change, push or publication occurred.

### Final closeout

This records the first implementation package; the role correction below supersedes
its lossy admission and source/package identity, while retaining unchanged helper evidence.

The existing fixture CLI initially refused a later offline test because a normal
Steam RimWorld process was running outside the fixture. No direct-function bypass
was used. The owner explicitly approved a narrow guard correction and continuation
through steward action `s10-offline-guard-approved-20260909`. Only the CLI's `test`
and `build` branches now permit fresh, verified executable paths outside the
canonical fixture. Unknown paths, linked paths, any fixture process and mixed
normal/fixture inventories refuse. Other actions retain the original no-game
guard. The fixture lock, frozen-reference validation and committed-package checks
remain unchanged. Focused cases cover no process, known normal, fixture, mixed
and unavailable/relative paths. The normal game and its data remain untouched.

The corrected integration passed **63 focused managed checks, zero skips/failures,
and 37 Python checks**, with a zero-warning/error Release build. Receipt:
`artifacts/fixture-tests/32471a8f3d3249baa955a568c47737ef/features.trx`.
This includes the actual managed-to-native helper handshake, validated output,
pre-start and during-child cancellation/join followed by another request,
native folder override/DDS selection (including `.jpeg`), ordinary setting
selection, actual eager/Loading Progress hook reachability, full-content/options
freshness, prepared-store limits/corruption and the narrow offline process guard.
The helper test environment explicitly pointed to the packaged executable and
`artifacts/s10-helper/orientation-16.png`. No Unity graphics call was executed by
these offline tests. The fixture Python tests' printed simulated launch messages
are mocked workflow tests; their final receipt records `gameLaunched: false`.

Implementation source: **`f9c598e66af1eb413febd131c27a31da567b66c0`** on
`codex/s10-texture-preparation`. The guarded core build completed with zero
warnings/errors and receipt
`artifacts/fixture-package/f9c598e66af1eb413febd131c27a31da567b66c0.json`.
The original two-file core package remains separate from the optional bundle.

Local combined bundle:
`artifacts/releases/0.2.1-f9c598e66af1eb413febd131c27a31da567b66c0-texture-helper-win-x64/wake-up`.
The directory's files total **1,392,994 bytes**, including source, notices and
manifest. The retained `0.2.1` metadata is an existing version label, not a new
publication or qualification of that release.

| Identity | SHA-256 |
|---|---|
| Runtime `WakeUp.dll` | `8588bb0242077e9867954f9fad28a647019925c0072c4099ba4d6675c635b800` |
| Windows helper | `ba7fb9a3c70c3559af4fe9c5c25e3c506c8a2195e8fbce68eaa18861c8068abc` |
| Included source ZIP | `d239ba8c72cb95b9bc9cd550a8514709dd43f9ba2725e7ec6d887fbb6cbe7a46` |
| Combined `wake-up-0.2.1.zip` | `1aadbabfe23cc63f0003648a579ec6fbd1f972456363bf149037500b68e023d8` |

The helper was built before the source commit; its receipt therefore records the
accepted base and a dirty-tree flag. The packager verified every helper source,
build-script and notice fingerprint against the now-committed inputs, and copied
the exact tested executable. No unnecessary native rebuild changed its identity.
The combined manifest records helper qualification separately and excludes the
private host receipt. ZIP integrity and every embedded file were checked by the
existing packager. This closeout documentation follows the packaged source commit.

The S10 implementation task and both bounded agents have stopped work and release
checkout/build ownership to parent steward
`01a08711-7899-7ff3-a1b5-4a0b105ba661` for review. Do not start S11 automatically.
The next concrete platform action is an approved existing Linux x64 compiler/CMake
host, or a separate owner decision about tooling setup; Linux decoder compilation,
imports/glibc floor and real handshake/image checks must precede a Linux package
claim. Separately authorized fixture work is still needed for native preservation,
rendering/samplers, live UX, masks/atlas/gameplay and measured startup benefit.

## Parent-steward review — corrections required, 9 September 2026

Reviewed implementation `f9c598e66af1eb413febd131c27a31da567b66c0` and closeout `2b7e24c2e91a167efe3b363ee05db4fc2c71f1a2` against the actual controller, resolver, store, helper, guard diff and reported focused evidence. The approved offline-only process exception is correctly limited to test/build and preserves uncertain/fixture-process refusal. Windows helper checks and local package identities are evidence of offline behavior only. No tests were rerun merely to repeat adequate records.

Two related lossy-admission corrections are required before acceptance:

1. Known mask eligibility is checked only during the menu scan. Adding `Thing_m` in an earlier active provider after preparing `Thing` in a later provider does not alter the later provider's selected-file fingerprint or the active-provider root/order list. The current resolver can therefore reuse transformed output for a now-known mask pair; publication likewise does not revalidate this cross-provider association. Revalidate relevant role/association eligibility before publication and restoration, and tie cached results to that eligibility. Add one focused earlier-provider mask-addition case covering publication and reuse; avoid a per-image whole-modlist rescan.
2. The ordinary workflow asks users to certify that an entire selected mod/folder has no mask or data uses. This substitutes manual specialist verification for the planned rule that unknown roles retain native behavior. Provide one bounded, source-verified ordinary-color admission class with ordinary user wording; explicit consent to lossy quality is still required, but cannot establish technical role safety. Do not build a universal shader/Def classifier. If a useful bounded class cannot be justified, finish the concrete invalidation fix and return the precise capability/UX decision needed instead of declaring unknown roles supported through a checkbox.

The native-preservation path remains separate and should be preserved. Linux packaging still lacks an authorized available build environment; portable source is not a Linux qualification. The same S10 team owns corrections; no S11 assignment or new deployment/live authorization follows from this review.

## Role and association correction — 9 September 2026

Steward action `s10-role-eligibility-correction-1-20260909` began from verified clean
HEAD `39044e4e553023889cc590ce80ce6e2b4e0ca2bd` on the existing S10 branch.
The two requested corrections are implemented as described above. The earlier
review remains an accurate record of the superseded implementation.

Read-only native research used fixture `Assembly-CSharp.dll` SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
`RimWorld.UI_BackgroundMain` loads the literal `UI/HeroArt/BGPlanet` into its private
static field and draws that field through `GUI.DrawTexture`; its independent
`overrideBGImage` objects and expansion overlays are not admitted by this proof.
The two guarded semantic method hashes (hashes of the method's meaningful code)
are:

| Native method | Semantic SHA-256 |
|---|---|
| `UI_BackgroundMain::.cctor()` | `D4C5894F267CAEA87628058F4961AEB5E8D0D3D407B6D83B917DCA938754B6C4` |
| `UI_BackgroundMain::BackgroundOnGUI()` | `BF86C34020E278BC6E4B6D391B74E159BBFED3442F34E512CF6967860405EC42` |

A changed body, unsupported runtime identity or foreign interception of either
method declines lossy use. This guard proves the scoped native use; it cannot
certify every arbitrary mod repurposing the same object. General shader/definition
classification, custom masks and other consumers remain outside admitted scope.
Background overrides must still satisfy existing helper dimensions, metadata,
source-size, output-size and native DDS-selection rules. Base Resources/bundles
are not physical-file preparation inputs. The observed fixture has no eligible
BGPlanet override, so broader useful workload coverage remains an explicit gap.
Terrain's shader/mask path and an image with no proven native consumer were not
admitted merely to create fixture coverage. Research and exact inventory evidence:
`artifacts/s10-role-correction/findings.md` and adjacent native decompilations.

Focused verification passed **14 managed tests and 37 Python tests**, with a
zero-warning/error Release build and `gameLaunched: false`. Receipt:
`artifacts/fixture-tests/d1636a2d2f4f4c28a5fadadc5838f700/features.trx`.
The new regression prepares and reads an entry, creates an earlier provider's
mask directory/file without refreshing captured folders or changing the later
source, then verifies both reuse rejection and publication refusal. It uses the
real eligibility probes and prepared store; independently checked native path
matching substitutes for the loaded game's guard environment in this offline test.
Other focused checks verify actual native consumer hashes and instructions,
unknown-path rejection, controller/resolver boundary reachability and removal of
the manual checkbox. They do not execute Unity drawing or native texture capture.

The unchanged helper's existing 15 image cases and prior store/integration checks
remain relevant; they were not rerun solely to duplicate adequate evidence. Linux
packaging, live quality/UX/gameplay and measured benefit remain unqualified. No
new dependency, deployment, game launch, normal game-data change, Deck access,
UI automation, security change, push or publication occurred. S11 is not started.

### Correction package and ownership closeout

Correction source is **`7196b5f50038b16f3488b19149337755d5dc4d5d`**. The guarded
core build completed with zero warnings/errors; receipt:
`artifacts/fixture-package/7196b5f50038b16f3488b19149337755d5dc4d5d.json`.
The optional combined package is:
`artifacts/releases/0.2.1-7196b5f50038b16f3488b19149337755d5dc4d5d-texture-helper-win-x64/wake-up`.
Its sibling `wake-up-0.2.1.zip` contains the current runtime/source and unchanged,
previously tested helper. The existing packager verified helper inputs/notices,
ZIP integrity and embedded files. No deployment followed.

| Identity | SHA-256 |
|---|---|
| Corrected runtime `WakeUp.dll` | `bb0c1885d56aaaabe977635be90d0c18a5d30b97abdc5d72a70c1ded220cbca0` |
| Unchanged Windows helper | `ba7fb9a3c70c3559af4fe9c5c25e3c506c8a2195e8fbce68eaa18861c8068abc` |
| Included source ZIP | `1bf245bbf50331b50f4e47f31b93f2a18bf14bdd1d1cc5452f7a511d527e37e2` |
| Combined `wake-up-0.2.1.zip` | `aa738df4b62d6f80d3b0fb70f54bfe727ada12769e2348124c855caf7b1b8cc2` |

The manifest's existing release version and historical startup-validation fields
belong to the earlier baseline, not qualification of this changed runtime. The
helper-specific fields still state no Unity rendering/live preparation or measured
startup benefit. This documentation closeout follows the packaged source commit.

The S10 task and bounded researcher have finished and release exclusive
checkout/build ownership to parent steward `01a08711-7899-7ff3-a1b5-4a0b105ba661`
for correction review. No S11 work or steward-state mutation was performed.

## Parent-steward correction review — 9 September 2026

Accepted correction source `7196b5f50038b16f3488b19149337755d5dc4d5d` and handoff `10c6e24be9f3d4fa3272086d5e80ffe101584fd1` for the implemented Windows/offline checkpoint, not full S10 completion. Reviewed the actual role guards, resolver/publication calls, targeted live-filesystem mask probes and regression for adding an earlier provider's mask without changing the later source. The correction makes material progress: both requested issues are resolved for its admitted path. Fourteen focused managed cases and 37 Python checks passed, with a clean source-specific package; retained actual-helper checks were not unnecessarily repeated.

Native preservation, owned storage, the preparation controller and Windows helper are delivered source/offline work. The lossy path now has a native-consumer proof for compatible BGPlanet overrides and invalidates older manually admitted output. This narrow route has zero observed fixture matches. Broad useful recompression/downscaling remains an explicit S10 capability gap, not delivered ecosystem replacement. Further coverage needs a useful source/consumer premise; no permanent omission is approved and no new broad classifier is requested in this review.

Linux packaging remains blocked by the missing build environment. The steward independently confirmed that WSL reports it is not installed; neither Docker nor Podman is available. Existing approval covers the helper/dependency source and local packages, but expressly excludes automatic system-tooling installation. The proposed next action is owner-authorized setup of local WSL/Ubuntu with C++/CMake tools, followed by the existing pinned Linux build/handshake/image checks. Any required restart must be left to the owner, with the normal game untouched. No Linux binary or compatibility claim exists yet.

The team is stopped and checkout/build ownership returned. Pause the recurring check for this tooling decision. S11 can be assigned after the owner chooses how to proceed. All live Unity, gameplay, performance and release qualification remain separate and unauthorized; S04/S05/S06/S09 and broad S10 lossy coverage stay explicitly unfinished.

## Owner platform deferral and next checkpoint — 9 September 2026

The owner chose to put off the Linux helper and continue with Windows. This supersedes the tooling approval request above: install no Linux environment and do not treat missing Linux packaging as a blocker for the next independent slice. Keep portable source for later; no Linux helper build or runtime support is claimed. Existing core Linux behavior is not removed.

Retain acceptance of corrected Windows/offline source `7196b5f` and handoff `10c6e24`. S10 remains partial because broader useful lossy coverage is unfinished and all live qualification is pending. The existing prepared-output selection, freshness and fallback contract is sufficient for S11 routing. Resume the five-minute workflow and give a fresh S11 task exclusive ownership for routing only, with a handoff-and-stop before the separate optional on-demand checkpoint. No deployment, game launch, system-tool installation or publication is authorized.
