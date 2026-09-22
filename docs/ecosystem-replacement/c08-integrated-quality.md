# C08 integrated texture quality and preparation

## Current disposition — owner-approved amendment, 11 September 2026

The owner made the standalone companion app and pre-first-load ordinary-user workflow (matrix 6c) **optional post-release** and authorized advancing the other features. Its inspection gate is canceled; implemented source, packages, bundled runtime decision and all evidence are preserved, not accepted or deleted. No full todds/ToDDS or external-workflow replacement is claimed. The active [proposal](next-campaign-proposal.md) and [matrix](next-campaign-matrix.md) now carry this amendment directly; earlier requirements below are historical where superseded.

In-game native preparation and quality/sampling/reset/coverage controls (6a/6b), shared helper/runtime/native storage and all other approved capabilities remain in scope. Source and automated functional review remain passed for exact tested `83a2930f60967eba4fcfb8e6acef10f35a415c62`. Actual in-game window readability/control behavior, scene alpha/masks/directions/UI/zoom, effective provider appearance, and native comparison/reset/restart remain **open for combined GOG correctness before release**. They are neither waived nor proven by automated tests. Feature-specific and combined performance/overhead obligations remain pending until an unattended grant.

No concrete C09 technical blocker was identified: the reviewed runtime/storage/exposure contracts remain available. The core project has no dependency on the companion executable; the companion compiles shared core source into its own distribution. Existing in-game functional evidence therefore remains applicable without the app. No product code, build or additional functional run was needed for this scope change. C09 may proceed after steward review and ownership transfer; this is not C08 self-acceptance.

The authorized standalone window was opened as PID 21184, with all 327 package files verified, but no owner visual pass was received. It was already closed when the scope change arrived; no close request or forced termination was needed. The manual label `c08-owner-visible-01` was never launched. All 109 profile files were preserved before restoration; their content matched staging (no app changes). The original staging receipts and app packages remain intact.

All five staging transactions were restored through `20260911-152942-c8833778` using the existing script and `fixture.py`. Seven original hashes, three absences and the GOG metadata audit pass. Evidence: `artifacts/ecosystem-next-20260910/c08-visible-stage/restoration.json`; preserved profile/run/close receipts: sibling `scope-amendment/`. Fixture, app, helper and test operations are stopped; normal Steam game PID 24740 remains untouched. Exclusive ownership returns to the steward. No successor, performance test, Steam/Deck work, merge, push or publication occurred.

C08 makes explicitly prepared texture choices available to the game's ordinary
texture loading path. Native preparation retains the actual game producer's
output; converted quality deliberately changes pixels or dimensions. Storage
compression is a separate option and does not change restored pixels.

## Ownership and decisions — 11 September 2026

Dispatch `wake-up-next-c08-5a273ad-20260911` starts from clean
`5a273ad91fe9531d69256208a741912b0d905fbb` on the preserved approved ancestry.
C08 held the single checkout and isolated GOG fixture during implementation.
Ownership now returns to the parent steward for independent review; this report does not accept C08 or
dispatch a successor. Seven original fixture hashes were freshly checked in
`artifacts/ecosystem-next-20260910/c08/initial-identities.json`.

The steward relayed the owner's explicit **yes** to decision
`c08-bundled-desktop-runtime`: a self-contained Windows x64 WinForms preparation
app may bundle its .NET runtime and the existing approved helper/notices. Users
must not need a separate dependency installation. The app remains a preparation
window, with no mod management, updater or automatic loading-time helper process.
Runtime version, complete local distribution, package size and supported Windows
prerequisites require package verification. The earlier pending request is resolved.

Performance tests, microbenchmarks, encoder cost trials and resource-pressure
experiments remain prohibited while the owner uses the PC. Functional durations
are diagnostic only. Visible inspection still requires coordination through the
steward; no window/controller probe will be called a human UI pass.

## Current implementation boundary

The exact GOG revision 573 queues texture loading until after definitions are
created during ordinary asynchronous startup. This allows current resolved
graphic/shader metadata to determine conversion roles before texture holder
publication. Other entry paths must retain native output when that boundary is
not established. Filesystem names and user consent do not establish shader roles.

Converted outputs retain each provider's identity and source content digest.
A color/mask group must have compatible completed representations and current
sources before the first member is exposed. Direct access to an earlier mod's
holder must receive that mod's own pixels. Unknown or conflicting consumer roles
retain native output with a coverage reason. External preparation can produce
provisional output, but the game's current metadata remains the admission
authority; an encoded result is not proof that the game applied it.

Implementation, functional qualification, packaging and coordinated inspection
are in progress. C01–C07 performance failures/pending measurements and other
recorded qualification gaps remain separate and unchanged.

## What the user chooses

Native preparation saves the representation produced by the game, including
all smaller versions used at a distance (mip levels), format and sampling.
The existing native preparation and DDS export actions remain separate.
The new quality window changes what a later ordinary launch actually loads.
Full-size compression changes pixels; half and quarter choices also reduce
both dimensions. Sampling controls choose smoothing, the preference for smaller
mip levels (mip bias), and detail on angled surfaces (anisotropic sampling).
Reset removes the selected quality override so the next launch uses ordinary
native pixels and sampling. None of these actions edits original artwork.

The selection includes every provider of a connected graphic and its masks.
Color conversion filters in linear color with alpha-weighted color to avoid
transparent borders contaminating smaller mip levels. Masks retain separate
RGBA channels without lossy block compression; resizing still changes detail.
Unknown consumers, conflicting metadata, custom unsupported graphics and
unqualified runtime methods retain native output. File names alone never
authorize a conversion. Authored DDS can be converted only by an explicit
quality selection; ordinary native DDS behavior remains separate.

Before publishing, the application rechecks the source, provider selection,
helper and current consumer metadata. It saves the whole connected group by
replacing one index. A failed replacement retains the previous complete group.
At startup the game validates and constructs every group member privately
before giving any member to its provider's ordinary texture holder. The global
winner and direct earlier-provider access retain their distinct objects.

## Windows preparation window

The portable WinForms application reads an existing game folder, existing
`ModsConfig.xml`, optional local/Workshop roots, and the chosen output profile.
It displays provider order, source rows, selection and coverage reasons, then
lets users select mods/files and prepare, resume, cancel or reset. It does not
manage mods or rewrite load order. A scan previews metadata; the conversion
backend rereads the loadout before writing.

Before a first game launch, selected XML definitions and ordinary inheritance
provide provisional roles. XML patches and arbitrary mod code are not executed
by this app. Current resolved game metadata remains authoritative on startup;
provisional outputs that do not cover a valid whole group are ignored. Native
captures require the actual game's producer and cannot be manufactured by the
standalone application. Its native reset action restores ordinary behavior.

The standalone and in-game paths share the same owned texture groups, source
identities and disk ceiling. A small owned request applies enablement or
one-launch maintenance at the next game startup. A clear request does not claim
that files have already been cleared. The app executes the helper only during
explicit preparation; ordinary startup never starts that process.

The application bundles .NET 10.0.12 for Windows x64. Bundling removes the need
for a separate .NET installation; it does not replace supported operating-system
requirements. See Microsoft's [supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
`scripts/build_preparation_app.py` produces the local distribution, linked app
source, helper source and runtime/helper notices. This is a local preparation
package, not a published release.

## Functional qualification in progress

The first implementation checkpoint is `9cbc8aa`. Its GOG core and observer
builds passed. The portable application distribution contains 124,061,290 bytes
including source and notices; its runtime configuration names bundled
Microsoft.NETCore.App and Microsoft.WindowsDesktop.App 10.0.12, and the required
runtime DLLs are present. The helper is 424,960 bytes, SHA-256
`3b9c4e443e300907b169bd1556b8895078aff6e454e1d3ff098f58b4b8dbd64f`.
Both protocol handshakes pass. Its existing 42 functional image checks cover
the old export protocol and new conversion/mask protocol. These are encoder
checks, not proof of game consumption or visual quality.

The core passed 31 focused role/storage checks. The subsequent focused checks
and all 67 fixture-tool checks passed at
`artifacts/fixture-tests/4159ad05d8314734ae2f1e8d21726fc1`.
An earlier tooling run hit Windows error 5 while renaming a temporary simulated
Steam directory; the rerun passed without broadening the test machinery.
No actual Steam game or normal installation was involved.

`c08-prepare-01` reached the menu and exited normally, but the observer rejected
the unqualified selection. The small workload omitted Vanilla Expanded
Framework, which provides Vanilla Textures Expanded's settings patch type.
`c08-prepare-02` included that dependency and confirmed the remaining failure:
ordinary generated building blueprints reuse the color path with
`Map/EdgeDetect`. Rejecting that shader rejected the connected building group.
Commit `79992e4` adds that exact native outline shader as an unmasked color
consumer and checks the real blueprint material alongside the finished
building's color/mask material. Other unknown shaders remain native.

Neither failed run is a quality pass or a performance sample.

## Connected game and standalone checks — 11 September 2026

Prepared choices now reach real game texture holders and building materials.
The small natural workload contains the six official content packs, Harmony,
Prepatcher, Vanilla Expanded Framework and Vanilla Textures Expanded. The
selected artwork is PlantPot plus Brazier and its mask. The mask carries
separate color-selection channels; it must remain paired with the matching
building image when dimensions change. PlantPot retains full dimensions and
the Brazier pair uses half dimensions. These are intentional quality changes,
not native-pixel equivalence claims.

The first conversion records used sRGB graphics-format descriptors, but the
qualified GOG player creates UNorm texture objects in its Gamma color mode.
`c08-verify-04` correctly rejected those records and used native textures.
Core `0a76135` fixes the descriptors and changes the saved runtime contract to
`gog573|4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28|unity-gamma-srgb-pixels-full-mips-v2`.
The pixels remain sRGB-encoded; this does not qualify a Linear-color player.
An observer-only correction at `68fb40b` also distinguishes the native
`Map/EdgeDetect` resource path from its actual `Custom/Edgedetect` shader name.

| Capture | Core / observer | Actual result |
| --- | --- | --- |
| `c08-prepare-05` | `0a76135` / `79992e4` | 30 checks pass: native captures and the actual in-game preparation controller publish complete quality groups. |
| `c08-verify-07` | `0a76135` / `68fb40b` | 32 checks pass: three prepared textures in two groups, all mip levels, dimensions, format, sampling and provider/global holders; actual Brazier color/mask and blueprint materials use the selected textures. |
| `c08-reset-08` | `0a76135` / `68fb40b` | 21 checks pass: the in-game controller resets the selected groups. |
| `c08-native-09` | `0a76135` / `68fb40b` | 28 checks pass: next startup restores actual native pixels, descriptors, sampling and material use. |
| `c08-external-10` | `0a76135` / `68fb40b` | 32 checks pass on the first launch of a fresh profile prepared by the packaged standalone backend; three textures in two groups are applied. |

Each passing capture reached the independently observed menu, exited normally
with code zero and has both `automaticTestPassed` and
`textureQualityProbeTestPassed` true. No fixture process was forcibly closed.
Captures are under `.rlo-test-instance/results/<label>/`; raw build and focused
check evidence is under `artifacts/ecosystem-next-20260910/c08/`.

The tested standalone distribution is
`artifacts/preparation-package/68fb40b07c0de3617619beb46e3dadc8f9afaaba/`,
124,061,405 bytes including linked source and notices. This supersedes the
earlier `9cbc8aa` distribution. Its hidden functional-job entry point runs the
same backend as the ordinary WinForms window. In `c08-external-10`, cancellation
after one completed image retained that output; resume reused one and prepared
two more. A final full-size PlantPot selection completed successfully. All three
external attempts preserved source/configuration hashes and made no forbidden
profile changes. The owned next-launch request enabled preparation from a
profile whose initial setting was off, and was consumed on that launch.
The helper executable still has the SHA-256 recorded above. The deployed helper
receipt remains the original C08 receipt; identical executable bytes do not
imply identical build receipts.

The fixture now has a bounded `external-prepare` operation for this real
pre-first-launch workflow. Only a captured clean cancellation may resume within
the same prepared run: source, configuration and the captured profile must
still match. Other failures remain launch-blocking. This closes a concrete
workflow gap without exposing normal game data to the test operation.

## Combined compatibility correction

`c08-combined-11` exposed an interaction with atlas optimization: the PNG loader
treated Wake-Up's later atlas compression patch as foreign and rejected every
texture reload. The selected quality records remained intact and ordinary
textures loaded, but this was a functional failure. Core `ed2d929` admits only
the exact `AtlasRuntime.CompressionTranspiler` method under its expected owner
on `FastCompressDXT`. That method performs the native graphics copy and observes
its blocks for atlas capture. Other owners and other methods under the same
owner still refuse. Six focused managed checks and all 67 fixture-tool checks
pass at `artifacts/fixture-tests/6be14fc048d04769a9c60b44384694d0`.
The final fresh combined capture is `c08-combined-13`.

`c08-combined-12` reached the corrected loader but rejected both prepared groups
because the core redeployment had removed the fixture's optional helper. Its
`helper-missing-or-size` receipts and native fallback are preserved; it is not
a combined quality pass. The next deployment restores the same helper bytes
with build receipt SHA-256
`b3c1673e368b507731941df9414da2c4ac6585ab9cc74582e2973cb13b77519c`,
and the next prepared run verifies that candidate binding before launch.

`c08-combined-13` passes all 32 quality checks using core `ed2d929`, observer
`68fb40b`, the restored helper and newly prepared app `68fb40b` outputs.
Three textures in two groups are applied with zero refusals. The independent
menu and quality result flags are true and the process exits normally with
code zero. Atlas qualification reports eight passed checks and zero failed;
the PNG loader reports 12 guarded native reloads, zero reload fallbacks and
zero errors. Prepared quality, PNG caching, first-build images, static atlas
reuse and atlas batching are enabled together; storage compression is off.
This workload loads DDS sources and reports zero PNG/JPEG producer calls, so
it proves the corrected shared loader/atlas compatibility but does not add
PNG or first-build producer coverage to their separate C05/C06 evidence.

The final core source-inclusive local ZIP is
`artifacts/releases/0.3.0-rc.2-ed2d92915d587b7df9df68ec8ca3a2f760eea0e3-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`,
SHA-256 `a1d4e2a6738d5fa1d0532b4ae4af3b657f5ee0f6c4e1425af7915a39d7dfc149`.
It contains the exact `ed2d929` core, matching source, helper and notices.
Its inherited prerelease version label does not grant qualification or make
it the historical original rc.2 artifact. Nothing was published.

## Limits retained for independent review

These checks exercise the actual preparation controllers and backend but do
not inspect either visible window. Human inspection of window layout, progress,
cancel/reset interaction and understandable controls remains open. Real scene
appearance, transparent edges, UI scaling and zoom behavior also remain open.
Full and half dimensions have the live evidence above; quarter dimensions,
multi-directional graphics, true duplicate-provider collisions and a larger
workload do not yet have equivalent live C08 qualification. Unsupported roles
retain native behavior. The report does not infer those missing passes from
offline encoding or provider-slot tests.

No performance run or microbenchmark was performed. Later individual and
combined measurements need a new explicit unattended grant and exact recorded
settings/builds. No Steam, Linux/Deck, supplier-removal, full acceptance or
release-readiness claim follows from these GOG functional results.

## Restored endpoint and handoff

All 26 C08 deployment/preparation transactions were rolled back in reverse
order through `fixture.py`, ending at initial transaction
`20260911-123826-e06b23ab`. `artifacts/ecosystem-next-20260910/c08/restoration.json`
records all seven original endpoint SHA-256 matches and a passing metadata
audit. Original GOG generation and manifest, exclusions, mod order, preferences,
core `ba17b03` and original observer are restored. The original absent
`prepared.json`, Wake-Up settings and helper are again absent. Captured test
evidence remains retained; fixture content is not tracked.

No fixture, helper or preparation-app process remains and all three child
agents have stopped. The normal RimWorld process was freshly verified at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe` and left
running untouched. The current documentation handoff commit is distinct from
tested core `ed2d929` and observer/app `68fb40b`. Checkout/build/fixture ownership
returns to the steward for independent review; corrections return to this
responsible C08 owner after explicit ownership transfer. No successor, merge,
push, publication or performance measurement was performed.

## Independent steward review — 11 September 2026

**Changes requested; C08 has not passed functional review.** Handoff
`4c71f846747f44d1efc9be5578558dc063141e1c` delivers real preparation and startup
consumption, but correctness defects and agreed coverage remain unfinished.
The following findings are source-review conclusions, not newly reproduced
live failures. Corrections return to the same C08 orchestrator; no C09 follows.

The steward verified six functional capture manifests and their quality
receipts: prepare-05, verify-07, reset-08, native-09, external-10 and combined-13.
Actual provider texture objects, all-mip comparisons and material binding
support the narrow results above. These do not establish scene appearance or
unexercised provider collisions. The source-inclusive final core ZIP hash,
30 changed packaged source files, all 323 files in the standalone package
manifest and seven restored endpoint hashes were independently checked.
The standalone cancellation retained its completed output and the subsequent
resume reused it; that canceled attempt remains a cancellation, not a success.
Raw review evidence is `artifacts/ecosystem-next-20260910/c08/steward-evidence-review.json`.
Only the verified normal Steam game remained running; it was left untouched.

### Correctness corrections

1. **A later source can change after a prepared group was checked.**
   `PreparedQualityRuntime.TryResolve` releases all group source leases after
   construction and serves subsequent `ready` members without revalidation.
   A `LoadedContentItem<Texture2D>` constructor callback can change the next
   mask/source after the first member. C06's guard only drains its own worker
   queue; it does not guard C08 consumption. Correct the publication boundary
   so callbacks remain native and a group cannot mix stale or incompatible
   members. Verify a focused callback/source-change case against native behavior.
2. **Mixed image formats do not share group admission.** A foreign DDS-loader
   patch disables the DDS prepared path in `PngRuntime`, but a PNG member can
   still cause C08 to construct/apply its entire group. A reduced PNG color can
   consequently accompany a full-size native DDS mask. Validate that every
   member can follow the prepared route before exposing any member, including
   both enumeration orders and changes at callback boundaries.
3. **Automatic cache reclamation can erase explicit quality choices.** Quality
   records share the ordinary grouped index and can be removed by quota pruning
   before their source is visited, or by age pruning. Their records also hold
   the only persistent choice. Preserve user choices and coherent groups under
   ordinary cache writes, or retain choices separately with an explicit recovery
   policy and visible missing-output reasons. Do not start the helper implicitly.
4. **Clear has an incomplete label.** The existing action says it clears native
   prepared output and DDS exports but now also deletes quality selections.
   Preserve those selections or state the complete effect before the action.

### Replacement behavior still required

The standalone dry-run displays selected source bytes, not the expected output
or the complete expanded selection's memory/storage estimate. Known format
refusals appear only during preparation. Add a shared bounded preview for
expanded provider groups, chosen output estimates and exact eligibility before
publication; keep the visible and functional paths on the same logic.

The input parsers also exclude ordinary format families independently of shader
roles: indexed/16-bit PNG, common color/transparency metadata, JPEG metadata,
non-power-of-two dimensions and PSD conversion among others. The role catalog
does not cover actual terrain artwork and several other native consumers.
These are implementation gaps, not merely missing test evidence or accepted
exclusions. Inventory the real boundaries, implement useful general families
under the approved explicit-quality policy and investigate materially different
designs where needed. Necessary bounds remain legitimate, but any proposed
capability reduction requires an evidence-backed owner decision.

Complete focused actual startup qualification for quarter-size choices,
directional graphics, unrelated UI/terrain artwork, genuine duplicate providers
and relevant PNG/first-build/atlas interaction. Use existing fixture facilities
and a small representative selection; a new broad harness is unnecessary.
Then provide a corrected concrete package and inspection instructions for
owner-assisted app/in-game window and alpha/UI/zoom appearance review. Those
checks remain open; hidden controllers cannot substitute for them. Performance
remains separately pending and prohibited until a new unattended grant.

## Correction implementation — 11 September 2026

The same C08 owner resumed from clean `6afa6bf` under dispatch
`wake-up-c08-review-groups-coverage-20260911`. These changes are under functional
qualification; they are not independent acceptance or release approval.

Prepared quality now waits until ordinary native loading has populated every
member's real holder. Constructor callbacks still receive native textures.
Foreign callbacks, earlier consumer reads, changed providers, changed source
bytes or changed resolved consumers leave the whole native group intact.
Private texture construction occurs without source locks; source selection,
bytes and consumer definitions are checked again before all holder entries are
replaced together. This deliberately retains ordinary native producer work.
There is no claim that this correction improves startup speed.

Explicit quality records now survive ordinary age and quota reclamation.
Their existing self-owned storage slots distinguish them from reproducible
native cache entries. A full budget can refuse additional writes, while
explicit reset and clear still remove choices. Both surfaces explain that
clear removes quality choices as well as native preparation and DDS exports.
Read-only scans display saved preset and sampling choices without preparing
missing output or launching a helper.

The standalone preview expands related providers and artwork, inspects the
same supported headers as the encoder, and estimates complete mip-chain
texture memory, stored records and publication space. Execution rebuilds and
compares that plan before writing. Corrupt compressed pixels or profiles can
still fail during decoding; the preview distinguishes header eligibility from
successful conversion and from the game's resolved consumer admission.

The quality helper's new `srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4`
identity covers broader PNG depths, palettes, transparency and color metadata,
baseline/progressive JPEG including Windows CMYK color conversion, arbitrary
bounded image dimensions, native uncompressed DDS families, and opt-in merged
PSD pixels through the existing managed decoder. Block-unaligned output uses
RGBA32 to retain exact dimensions. Masks retain channel values and reject
CMYK ink-channel interpretation. Common PNG/JPEG color metadata is converted
to sRGB; optional PSD retains the existing saved-composite decoder policy.
Source, decoded-memory and output limits remain explicit. Unsupported native
DDS DXT3 is refused by integrated admission even though the helper can decode
its blocks. No new backend or startup helper process was introduced.

Inspection of the exact GOG DDS producer also found that quality conversion
must retain stored upload row order. The new helper fixes that orientation
and reproduces the native 4444 channel interpretation. Legacy v2 export
behavior is unchanged. Actual terrain, ability/main-button icons and the
native constant UI requests now have exact consumer admission; all 28 GOG
method identities are pinned. These are consumer contracts, not mod allowlists.

Initial offline evidence under
`artifacts/ecosystem-next-20260910/c08-corrections/` includes 39 storage/role
checks and 67 fixture-tool checks (`managed-03.log`), 34 format inspection
checks (`managed-04.log`), and all 71 helper checks (`helper-checks.json`).
The later tooling run had one temporary-file rename failure; the same focused
test passed on retry in `offline-followup-01.log`. The app and observer compile
with zero warnings/errors. Helper CMYK tests record Windows-decoder colors
rather than assuming an unmanaged subtractive formula. Live qualification,
exact final packages, restoration and owner-assisted visual inspection are
recorded separately when completed. No performance experiment was run.

The stricter natural-building/floor selector in `c08-correct-prepare-09`
could not find an eligible loose ordinary floor in the small active workload;
that captured failure is retained. The private observer variant adds existing
`2260097569/Textures/Terrain/Surfaces/AncientConcrete.png` as
`Textures/Terrain/Surfaces/Concrete.png` to exercise the real core Concrete
terrain consumer. Its SHA256 is
`37634a88c45e6f6f703de5f2ec4b7a077ec488122de2731b274a2dfb48df4c80`.
This is a declared test content substitution, not a product eligibility rule
or a claim about unmodified vanilla floor pixels. The existing distinct PNG
main-button provider remains in that bounded variant. Both files are private
fixture artwork; they are excluded from product and public source packages.

`c08-correct-prepare-10` retained that floor refusal: native Concrete pollution
reuses its base image, so admission requires additional shader qualification.
The correction instead qualifies an ordinary terrain base only when every
active pollution/overlay use has a distinct source. Those other sources remain
refused, including shared uses claimed elsewhere. The exact pinned native
`TerrainDef.PostLoad` condition and null-coalescing source selection govern
this split; the role identity records those facts. Core StrawMatting has a
distinct polluted source and therefore supplies the next real consumer. The
same private existing concrete PNG is declared at
`Textures/Terrain/Surfaces/StrawMatting.png` for this bounded functional variant;
its visual suitability is not an acceptance claim.

## Corrected package and functional evidence — 11 September 2026

The corrected runtime keeps native producers and their callbacks intact before
publishing a complete prepared group. Explicit saved quality choices survive
automatic cache cleanup, both interfaces expose those choices, clear labels
describe their removal, and previews show expanded sources and output costs.
This record distinguishes functional evidence from the still-pending owner
inspection and performance work; it does not grant acceptance.

Exact final core source is
`46dcaa76dd82efe098bb07e803dbf5eec806dabd`. Preparation/restart/reset use
the matching observer; the final native-pixel observer correction is `3f6abf2`.
The standalone app remains
`6c66cfd3dabe969ad8709d05daf3892edced78e3`; subsequent changes affect the
runtime consumer catalog and private observer only. The shared helper SHA256
is `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
The source-inclusive, unpublished core ZIP is
`artifacts/releases/0.3.0-rc.2-46dcaa76dd82efe098bb07e803dbf5eec806dabd-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`,
SHA256 `8a8ea6fb6a5b892f4e8734452cf7afc0c88b87059551d93dfc816a03c7fca58c`.
The standalone package root is
`artifacts/preparation-package/6c66cfd3dabe969ad8709d05daf3892edced78e3/`,
containing `package.json`, `Source/`, and `WakeUp.Preparation/WakeUp.Preparation.exe`.
Core/observer fixture packages are under their respective artifact directories
at the exact recorded revisions. Copied private observer artwork is excluded
from product packages.

The following captures live under `.rlo-test-instance/results/`; command,
preview and offline evidence is under
`artifacts/ecosystem-next-20260910/c08-corrections/`.

- `c08-correct-boundaries-04` (core/observer `6c66cfd`) passes all eight clean,
  source-mutating callback, preexisting/late mixed DDS/PNG hook, both-member-order
  and changed-consumer cases. Separate native controls verify callback counts,
  source writes and fallback pixels. Its format sidecar passes five actual
  native sources and 12 conversions, including optional PSD, non-power-of-two
  PNG, DDS rows, RGB565 and native 4444 channel interpretation. Normal exit and
  automatic capture passed. Later runtime edits are restricted to consumer
  identity/terrain eligibility; these callback mechanics are unchanged.
- `c08-correct-external-11` (core `6798040`, observer `78a347d`, app `6c66cfd`)
  prepares three sources externally, then changes the plant pot to full size.
  The first game launch passes 32 checks and applies all three textures in two
  groups with no refusals. Original cancellation/resume captures remain valid
  historical evidence; this run verifies the corrected app/helper pipeline.
- `c08-correct-prepare-12` (final core/observer) passes 40 checks: seven newly
  prepared quarter-size sources and seven saved choices visible after reopening
  the in-game controller. It exercises two real PNG loads and two first-build
  decode/consume operations with no failures. This is functional evidence only.
- `c08-correct-verify-13` (final core/observer) passes 72 checks: seven applied
  sources in three complete groups, no refusals, exact mip pixels and texture
  descriptors, all four actual `BurnoutMechlinkBooster` material views, the
  actual `StrawMatting` terrain material and `Architect` icon getter. Distinct
  PNG/DDS providers retain separate owned texture objects and VTE remains the
  last-provider winner. The two native PNG cache hits coexist with quality
  application; the atlas observer passes all eight checks. This is not a
  claim about a new gameplay scene or simultaneous cold first-build speed.

The seven-source selection identity is
`08A6A68991B90E857360A4D05A00A4FD85E494FB0A443FB55DB7AF4B7FD4D2E6`.
All these runs use the isolated GOG generation
`gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`,
the recorded C08 small workload plus declared observer artwork, user activation,
and prepared textures, PNG cache, first-build images, static atlases, atlas
batching and optional PSD enabled; texture-storage compression is disabled.
Quarter sampling is trilinear, anisotropy 4, mip bias 0.5. Exact source/provider
order, helpers, settings and binaries are captured separately in each run.

`saved-preview-13.log` verifies the standalone app displays all seven saved
quarter choices without writes or source/config changes. Its conservative
original-XML planner accepts the four directional rows but refuses the terrain
and patched icon rows for conversion; those resolved live roles are available
through in-game preparation. This limitation is visible before preparation.
`preview-06` separately proves automatic color/mask and duplicate-provider
expansion with bounded memory/storage estimates and known refusal reasons.
The final terrain change passed focused pinned-role checks and all 68 fixture
tool checks (`managed-07.log`, result `ce69122ef82142eb98fee83fbd51e6ae`).

Failed captures remain preserved: boundaries01 exposed broad main-button
refusal; boundaries03 used a populated native holder in its control; prepare05
and07 exposed byte-loaded VTE identity handling; prepare09/10 proved the floor
selection/eligibility limitations above. Prepare08 passed seven sources but
selected a blueprint and developer-only terrain, and is not the final consumer
coverage claim. VTE admission pins the exact binary, loaded module and 16
reviewed method bodies; unknown replacements retain native behavior.

`c08-correct-reset-14` passes 32 checks and shows all seven saved choices as
native after reopening the real controller. `c08-correct-native-15` then
confirms the five DDS holders against native pixels before its observer hits
the production atlas comparator's deliberate sub-512-pixel size limit on the
1024-pixel terrain PNG. The native observer correction uses the same explicit
per-mip floating GPU readback in a private comparator bounded to 1024 pixels;
the product atlas limit and runtime are unchanged. That failed capture remains
preserved and is not treated as a successful full native-reset check.

`c08-correct-native-16` passes all 57 checks using core `46dcaa7` and observer
`3f6abf2b9c43f862c9c9c33b289bcece72b67123`. All seven actual native holders,
their pixels across every mip, sampling and material/provider relationships
match ordinary production again; zero quality overrides remain. This and the
other successful captures exited normally with `automaticTestPassed=true`,
the feature probe passed and exit code zero. A compact index is
`artifacts/ecosystem-next-20260910/c08-corrections/functional-closeout.json`.

The fixture is restored through all 37 correction transactions, stopping at
`20260911-140025-6ae712f2`. `restoration.json` proves all seven initial hashes
match and the prepared pointer, Wake-Up settings and optional helper are again
absent. The metadata audit reports `deployedRuntimeMatches=true`, the original
manifest and original three exclusions. Fresh process inspection identifies
only the untouched normal Steam RimWorld process, PID 24740 at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe`;
no fixture game, preparation app, helper or testhost remains. All three child
assignments are stopped. The checkout and fixture return to the steward for
independent review, with no merge, push, publication, successor or acceptance.

Owner-assisted inspection of both actual windows and an actual gameplay scene
remains pending. The [concrete inspection guide](c08-visual-inspection.md)
provides the package/workload setup, control checks and appearance questions
for the steward to coordinate with the owner. No window was shown or scene
rendered by these controller checks. Performance, encoding resource cost,
individual/combined measured comparisons, Windows Steam promotion and later
Linux/Deck work remain separately pending under their respective permissions.

## Independent correction review — 11 September 2026

**Further changes requested; functional acceptance remains open.** The steward
reviewed handoff `3191845226965eae132a97a1b0529bf042023e85`, final core `46dcaa7`,
the separately attributed earlier callback/external captures and app `6c66cfd`.
The previous constructor/source-change and mixed PNG/DDS group defects are
resolved by the revised publication boundary. Explicit selections now survive
ordinary quota/age reclamation; clear labels, saved-choice display, expanded
preview estimates and broad image inspection are corrected. The source and
functional evidence support these conclusions, without claiming a visible pass.

Six correction capture manifests and their actual feature receipts were
checked, including the eight native/control boundary cases and the final
seven-source prepare/restart/reset/native chain. The final core ZIP/DLL,
25 changed packaged source files, all 326 standalone manifest files and seven
restored endpoint hashes match. The standalone package contains 124,249,419
bytes including source/notices and its bundled runtime. Evidence is preserved
in `artifacts/ecosystem-next-20260910/c08-corrections/steward-evidence-review.json`.

One concrete source-review ownership defect remains. A foreign managed
`ImageConversion.LoadImage(Texture2D, byte[])` callback can retain its texture
argument or bind it to a material. When native compression is disabled, that
object can become the native holder texture. C06's separate Unity callback
guards disable its worker queue, but `PngRuntime.QualityCallbacksAllowed` checks
only the higher-level loader/DDS guards. C08 therefore records no exposure and
can destroy the externally retained texture after replacing its holder entry.
Protect the actual producer and private-construction callback surfaces, retaining
native behavior when ownership cannot be established. Verify a focused retained
reference/material case against native behavior; a new general harness is not
needed. This is a source-established failure path, not a newly executed test.

The standalone terrain and patched-icon preparation limitations also remain
implementation work. In-game resolved roles do not satisfy those families'
pre-first-load workflow. Extend shared terrain policy and investigate a bounded
way to prepare useful patched icon selections while retaining authoritative
runtime checks. Do not execute arbitrary mod code or add a general mod manager
to the thin app. A proposed capability reduction needs an explicit owner decision
with evidence and alternatives; this review approves no exclusion.

Return these corrections to the same C08 owner. Preserve the valid evidence
and keep the next verification focused on changed behavior. Actual windows and
game-scene appearance remain pending owner coordination after the corrected
package is ready. Native producer work before quality replacement remains a
specific later loading/peak-memory measurement obligation; no performance
testing, speed claim, successor or full acceptance follows from this review.

## Unity ownership and external metadata follow-up — 11 September 2026

Dispatch `wake-up-c08-review-unity-exposure-external-20260911` resumed from
verified clean `7399fe5`. The correction preserves a texture when a callback
may have handed it to another consumer. Quality admission now checks the
actual Unity creation, upload, metadata and cleanup methods independently of
the optional first-build queue. It retains Harmony's immutable publication
identities, so a hook that installs and removes itself is still detected.
Source loading checks before and after callbacks; private construction and
failure cleanup use the same ownership check. Uncertain C08 routes call the
ordinary native loader, preserving callbacks even when a native cache entry
exists. Potentially borrowed private/native objects are not destroyed by quality
replacement cleanup. Source leases never cross those callbacks.

The external app and game now use `PreparationTerrainPolicy` for the pinned
native ordinary/polluted source split. The external app also projects a finite
set of declarative XML icon operations before inheritance: literal named
MainButton/Ability/buildable icon targets, scalar add/replace/remove, sequence
and conditional operations. It executes no supplier code or general XPath.
Unknown effects retain source refusals, including icons inherited by an
uncertain child. The game still validates resolved roles, provider order,
source bytes, helper/options and the whole group before applying provisional
external output. This supports the inspected VTE Architect patch and is not a
blanket filename or package permission.

Focused offline checks pass: five terrain/pinned-role tests and all 68 fixture
tool tests (`managed-02.log`, result `43b2fbd70f1849ae96804ce866fc40f5`), plus
12 external metadata assertions across four cases (VTE-shaped operations,
sequence failure, inherited uncertainty and unknown family XPath). The small
console check uses synthetic XML/source metadata only, without a game, image
decode or helper. Source review identified and corrected the inherited-icon
refusal before packaging. The observer's first compilation found one nullable
annotation after an assertion; the corrected observer builds with no warnings.

Core, observer and self-contained app package source is
`83a2930f60967eba4fcfb8e6acef10f35a415c62`. The helper is unchanged at SHA256
`ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
The source-inclusive unpublished ZIP is
`artifacts/releases/0.3.0-rc.2-83a2930f60967eba4fcfb8e6acef10f35a415c62-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`,
SHA256 `c8f568a5701d73dbd2fbf4f1ee8e3cf50e32606ba231e9496f14de457276a684`.
The app package root is
`artifacts/preparation-package/83a2930f60967eba4fcfb8e6acef10f35a415c62/`,
with its executable under `WakeUp.Preparation/`, `package.json`, and `Source/`.
It includes the approved runtime and unchanged helper; users install neither
manually. Raw follow-up evidence is under
`artifacts/ecosystem-next-20260910/c08-unity-external/`.

All three focused functional captures pass and exit normally with independent
menu observation, `automaticTestPassed=true`, feature probe success and exit
code zero:

- `c08-unity-off-01` and `c08-unity-on-02`: two directed cases each, with the
  actual first-build option respectively off/on and native compression disabled.
  A preexisting LoadImage callback and a late self-removing callback each run
  against native and candidate loading. Their retained PNG objects stay alive
  in the real holders, the material retains the same color object, all native
  mip pixels match, callbacks are not suppressed and quality application is
  zero. The private probe isolates these callbacks; the separate first-load
  run below verifies real first-build image consumption.
- `c08-external-seven-03`: the packaged app prepares all seven previously
  recorded sources at quarter dimensions before the first profile launch,
  with no reused, skipped or failed conversions. Both the ordinary terrain PNG
  and patched Architect icon providers are eligible. Source/config hashes stay
  unchanged. On the first game launch, all seven textures apply in three
  complete groups with zero refusals and all 72 consumer/pixel checks pass.
  The actual building views, terrain material and icon getter consume the
  outputs; provider-owned objects remain distinct and VTE remains the global
  icon winner. The two PNGs also execute two first-build decode/consume/raw
  uploads, with zero failures, during this same cold native-cache launch.
  The atlas observer passes its eight focused checks. These are functional
  counts, not a performance claim or visible scene inspection.

This reuses the declared private artwork variant from the previous correction:
existing concrete artwork is supplied through the real StrawMatting consumer,
and distinct PNG/DDS icon providers are present. Source selection identity is
`08A6A68991B90E857360A4D05A00A4FD85E494FB0A443FB55DB7AF4B7FD4D2E6`.
The former standalone terrain/patched-icon conversion gap for this useful
selection is closed by actual first-load evidence. Unknown/custom operations
remain reported as provisional refusals rather than being treated as authority.

`functional-closeout.json` indexes the captures and identities. Core DLL SHA256
is `70b058909f2dc85fe7be60f01a96a75a0162b504a9de21d63ad4e19f2a6f6857`;
observer DLL SHA256 is
`2a33d56dfa56cd440f17da8332df71351e6178fa5343393e9602bbcc8f60131a`;
app manifest SHA256 is
`c28c655c20ed679d873855ee2e48ba11a19adbd0a8a7a773196463b492de9d5d`.

All six follow-up fixture transactions are restored through
`20260911-151309-88d6486d`. `restoration.json` verifies the seven original hashes,
three original absences and passing metadata audit in the original GOG
generation. `final-processes.json` freshly identifies only the untouched normal
Steam RimWorld process at its original executable path; no fixture game,
helper, app or testhost remains. All children and operations are stopped;
the clean checkout and fixture return to the steward for independent review.
Earlier passing evidence is preserved rather than indiscriminately repeated.

The [updated concrete inspection guide](c08-visual-inspection.md) is ready for
owner coordination. No actual window or gameplay scene has been inspected.
Performance, including native work before quality replacement and peak-memory
cost, still needs a separately authorized unattended window. No successor,
self-acceptance, merge, push, publication, Steam promotion or Deck work occurred.

## Focused independent review — 11 September 2026

**Source and automated functional review pass; visible functional acceptance
remains pending.** The steward reviewed clean handoff
`1021f3f0ee72b021922a7c854e739be4048f379e`, exact tested core/observer/app
`83a2930f60967eba4fcfb8e6acef10f35a415c62`. Independent runtime and app reviews
found no further blocker in the focused changes. The retained-reference defect
is resolved independently of first-build settings. Shared terrain admission and
the bounded declarative icon projection deliver the previously missing external
selection without executing supplier code. Unknown/custom operations remain
explicitly unqualified; this is not universal mod compatibility.

The steward checked all three final functional capture manifests and actual
feature receipts, including native-control retained-reference/material behavior
and all seven externally prepared outputs consumed on the first profile launch.
The final source-inclusive ZIP and core DLL, ten changed packaged source files,
all 327 standalone manifest files, external source/config preservation and all
seven restored endpoint hashes match. The app package totals 124,284,545 bytes
including source/notices and the approved bundled runtime. Proof is retained at
`artifacts/ecosystem-next-20260910/c08-unity-external/steward-evidence-review.json`.
Earlier independently reviewed storage, image formats, reset and native-return
evidence remains separately attributed; it was not indiscriminately repeated.

At this historical review point, actual app/in-game controls and scene appearance still required the coordinated
[visible inspection](c08-visual-inspection.md). The same C08 orchestrator will
stage the exact isolated GOG inspection without opening windows; visible launch
waits for owner coordination. C08 was not fully functionally accepted and C09 was held at that review point. The later owner-approved amendment above supersedes that successor hold; in-game visual checks remain due before release. Performance remains pending and prohibited, including native work
before quality substitution, conservative retention after uncertain callbacks,
peak memory and individual/combined loading costs. No supplier-removal, release,
Steam/Deck promotion, merge or publication claim follows.
