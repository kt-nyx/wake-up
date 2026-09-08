# Current state

**0.2.1 supplier compatibility candidate, 2026-09-08:** Four optional supplier
integrations now discover and validate their required functions without whole-DLL
version/MVID/hash restrictions. Loading Progress repaint and PNG checks are
independent. The existing original-path and conflicting-patch fallbacks remain;
Harmony stays pinned. Offline tests passed 106 managed checks (one existing
optional supplier fixture skip) and 35 Python checks. Windows live testing with
automatic close and a subsequent release are owner-authorized and pending.
No Deck launch or deployment is part of this task. See
[supplier compatibility](supplier-forward-compatibility.md). The SDK pin advances
to the installed 10.0.401 patch release for reproducible local builds.

**Unreleased forward compatibility, 2026-09-08:** At the owner's request, runtime
admission no longer pins the game assembly version, MVID or full file hash.
Future Windows and native Linux builds attempt each feature's existing method,
supplier, graphics and patch checks. Harmony remains pinned. PNG cache keys now
include the actual game MVID so updates rebuild eligible entries. The first
offline pass completed 105 managed tests (one existing optional supplier skip)
and all 35 Python checks, with zero build warnings/errors. Compilation against
the captured Linux references also passed with zero warnings/errors. Validation used the
installed .NET 10.0.401 patch SDK via a temporary SDK-selection override; tracked
`global.json` remains unchanged. No game was launched or normal installation
changed. Published 0.2.0 remains unchanged. See [forward compatibility](forward-compatibility.md).
Future Workshop releases must preserve the owner's description unless explicitly
asked to edit it; this is recorded in AGENTS.md and release preparation.

**0.2.0 promotion after live checks, 2026-09-08:** Both the Windows Steam rev590
and native Steam Deck rev600 checks reached the menu and exited normally. Each
verified 1,682 Giddy-Up reads and six PNG cache hits against original output,
with zero mismatches, errors or texture-loader fallbacks. Modern Dev Tools was
temporarily disabled at the owner's request; original settings/load orders were
restored and temporary test assets removed. No new error headlines appeared.
See the [live validation record](steam-linux-live-validation.md) and
[0.2.0 release notes](../release/notes-0.2.0.md). The owner authorized a regular
release after these passes. Only release metadata/docs and the settings screen's
obsolete preview label differ from the tested implementation. Broader gameplay
qualification remains pending, separately from the completed startup checks.

**0.2.0-preview.1 release:** This compatibility preview packages the Windows
Steam rev590 and Linux rev600 support, Linux graphics ports, PNG / Loading
Progress integration and clearer startup diagnostics described below. The
[release notes](../release/notes-0.2.0-preview.1.md) distinguish implemented
support from pending final Windows Steam and PNG cache live validation. It
retains `steamQualified: false`; the release is not a universal game-version or
performance claim. Public release/package receipts identify the published
snapshot separately from the earlier local and Deck installations below.

**Windows Steam rev590 support implemented, 2026-09-08:** Source now admits the
owner's installed Windows Steam 1.6.4871 rev590 for all seven features' individual
compatibility checks. All 230 selected loading/texture methods matched the
reviewed GOG bodies after resolving metadata references. Direct3D 11 graphics
admission now includes this exact Steam contract; all file, supplier, method and
conflicting-patch checks remain. The full suite passed 101 managed checks (one
existing optional supplier skip) and 35 Python checks; 22 additional focused
tests passed using the actual installed Steam game/Harmony references, with no
skips or build warnings/errors. This change is built locally, not installed or
launched. Earlier installed `f658b44f...` packages below still have the previous
Steam feature restrictions. See [Windows Steam support](windows-steam-support.md)
for identities, local update metadata and pending runtime validation.

**PNG / Loading Progress fix installed on PC and Deck, 2026-09-08 20:40 UTC:**
At the owner's request, both normal `Mods/wake-up` packages were replaced with
the same tested DLL, SHA-256
`f658b44f4bce97aaca844a489f95d386d636f8a86753b9c4b6090e0bbc5c783f`.
All 15 files were verified per installation, including matching working source.
Both previous packages were preserved and profile configuration files were
verified unchanged. Both games were closed during installation; no launch was
performed. Receipts and PC backup are under
`artifacts/steam-deck-deployment/20260908-163800/`; the Deck backup is
`/home/deck/.local/share/wake-up-deployments/20260908-163800/previous-wake-up`.
The PC installation is `Z:\Games\SteamLibrary\steamapps\common\RimWorld\Mods\wake-up`.
Read-only binary inspection confirmed the PC game's existing `steam-rev590`
identity (assembly 1.6.9676.17735, MVID 61e41735-6189-4da4-9d21-0260257b5097,
SHA-256 5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A).
Installation does not expand feature admission to this Windows Steam build;
most features still require the reviewed GOG or Linux contract. Full Windows
Steam feature compatibility therefore remains a separate review/test task.

**PNG / Loading Progress compatibility implemented, 2026-09-08:** Source now
connects PNG caching to the reviewed Loading Progress 0.14.0 staged texture
loader on both Windows and Linux. It preserves that loader's yields, progress
reporting, profiling and non-texture work, and accepts only its three reviewed
callbacks on the native content method. Unknown suppliers/callbacks retain
ordinary loading. The original native route remains available when Loading
Progress is absent or its content replacement is disabled. Offline checks passed
101 managed tests (one existing optional supplier skip), all 35 Python tests,
and normal/captured-Linux builds with zero warnings/errors. Regression tests
reproduce the prior refusal on Windows and patch the real supplier iterator
without invoking Unity. This change is local and has not been deployed or
launched. The original successful Windows PNG qualification did not include
Loading Progress in its active mod list; it did not establish this combination's
compatibility. See [Linux support](linux-support.md#png-and-loading-progress-compatibility).

**Owner's Linux graphics launch reviewed, 2026-09-08 20:21 UTC:** The installed
`61c2fbd9...` preview reached the menu. Giddy-Up completed 1,682 optimized OpenGL
reads with zero errors/fallbacks and zero retained textures; original-output
verification mode was off. The other four previously active improvements also
completed. PNG caching was enabled but refused `hook-ReloadContentInt` before
graphics processing; the reviewed Loading Progress binary patches that method,
so its loader integration is an outstanding compatibility issue, not a demonstrated
Linux graphics failure. Wake-Up's Gagarin reuse again declined the known Loading
Progress XML hook. No new distinct normalized error headline appeared. The game
remained running; no normal-exit, gameplay or speed claim is established. Evidence:
`artifacts/steam-deck-diagnosis/20260908-162100-graphics-launch/launch-review.md`.

**Linux graphics preview installed, 2026-09-08 20:07 UTC:** At the owner's request,
the Deck's `Mods/wake-up` was replaced with the tested graphics-port build,
DLL SHA-256 `61c2fbd900be3662aa521998829367e25196e6c69f48a819b484114e23deb0fd`.
All 15 installed files were hash-verified, including the complete matching
working-source archive. The previous package was preserved at
`/home/deck/.local/share/wake-up-deployments/20260908-160553/previous-wake-up`.
Profile configuration files were verified unchanged. The game was closed and
was not launched; graphics validation remains pending. Receipt:
`artifacts/steam-deck-deployment/20260908-160553/installation.json`.

**Linux graphics ports implemented, 2026-09-08:** Current source enables Giddy-Up
center-column texture reads on OpenGL and adds PNG caching of the game's CPU
compression/mipmap output. It automatically supports Steam's default disabled
compute shaders; no manual launch flag is required. Exact game, supplier and
method checks remain, including a Linux-specific loader fingerprint. The PNG
cache remains opt-in. Offline checks passed 95 managed tests (one existing
optional supplier skip), all 35 Python tests, and both normal and captured-Linux
reference builds without warnings/errors. This graphics iteration has not been
deployed or launched. See [Linux support](linux-support.md) for mechanisms,
evidence and pending live validation.

**Owner's first Linux preview launch reviewed, 2026-09-08:** The installed DLL
below reached the menu. Definition/template searches, type searches, Character
Editor and Loading Progress completed their work. Gagarin reuse declined the
known Loading Progress XML hook conflict; the installed binary deliberately
declined both graphics features. No new unique error headline appeared compared
with the earlier capture. The game remained running at capture; normal exit,
gameplay and a speedup were not established. Evidence:
`artifacts/steam-deck-diagnosis/20260908-152136-preview-launch/launch-review.md`.

**Steam Deck preview installed, 2026-09-08 18:45 UTC:** At the owner's request,
the old local `Mods/wake-up` package on the Deck was replaced with the offline
tested Linux-preview runtime (DLL SHA-256
`a35f1e9c6666827431f357a3d1e23630e484935a064287f199e9e8552bd78a19`).
All 16 installed files were hash-verified, matching working-tree source was
included, and the previous package was preserved outside game discovery. All
profile configuration files were unchanged. No game was launched. This is a
private preview from uncommitted changes, not a Workshop release or live Linux
qualification. Receipt: `artifacts/steam-deck-deployment/20260908-144348/installation.json`.

**Linux compatibility preview, 2026-09-08:** Source recognizes the exact native
Linux RimWorld 1.6.4871 rev600 build captured from the owner's Steam Deck. Static
comparison found matching relevant loading methods, and the five non-graphics
features now admit that build while preserving their individual supplier and
patch checks. Game-file discovery handles `RimWorldLinux_Data`; rejection
messages distinguish game and Harmony failures. At this initial stage PNG and
Giddy-Up texture work remained unavailable on Linux. Offline checks passed 84 managed tests with one
optional supplier skip and all 35 Python tests; compilation against the captured
Linux references also passed without warnings/errors. No candidate was deployed
and no game was launched. See [Linux support](linux-support.md) for exact
identities, evidence and remaining live qualification. The release and deployed
identities below predate these source changes.

**Workshop upload and description draft, 2026-09-08:** The owner reports uploading
Wake-Up to Workshop after its authorized installation into the normal Steam
game. The local publishing folder records item ID `3798073152`; visibility and
downloaded contents have not been checked. The
[description draft](../release/steam-description.bbcode) uses the recorded
four-list development average and repeatable best case with their limitations.
No separate Steam performance or compatibility qualification has been recorded.

**Wake-Up rename, 2026-09-08:** The formal title is **Wake-Up: Loading Optimizations**. The repository and public package folder use `wake-up`, the C# project and runtime use `WakeUp`, and the public package ID is `kt.nyx.wakeup`. The optimization algorithms and supplier contracts are unchanged. Settings and cache data start fresh under the renamed identities; see the [replacement instructions](user-guide.md#replacing-a-pre-rename-development-copy). The fixture remains undeployed and no game was launched.

**Source cleanup, 2026-09-08:** Current source separates reusable helpers from feature installation, shares diagnostic JSON writing, and isolates setup failures between features. Compatibility identities and optimization algorithms remain unchanged. The offline suite now includes release-packaging checks; 81 managed checks passed with one optional Character Editor identity skip, and all 35 Python checks passed. These source changes are not a new live-game or performance qualification.

**Release preparation, 2026-09-08:** Product metadata, source-inclusive packaging, GPL linking permission, CC documentation license, notices and contribution terms are implemented. See [release preparation](release-preparation.md) and [source build](source-build.md). The chosen name is Wake-Up: Loading Optimizations. Final release copy remains pending. Runtime behavior is unchanged; Steam and public-package live qualification remain pending. The tested fixture package below remains deployed.

The latest development build is `30f005188ee17ac505845344f7a3541235cc4dd5`, packaged as `artifacts/releases/0.1.0-dev-30f005188ee17ac505845344f7a3541235cc4dd5/wake-up-0.1.0-dev.zip` (SHA-256 `662c32bf6d9dfd802e3c56f4c744f8f3725dc8ac5b985b11f389500438882a2f`). Its `WakeUp.dll` hash is `82c0cb0096ba10f71d488b71b1b9dfc3860f43c8e36a26984a4c0796048f3dec`; packaging copies it unchanged. The bundle includes matching source and legal material. The independent observer was rebuilt from `5ab28547cc9e002e89f39dd15bd41733f9294398`, without deployment. The later runtime build only corrects the formal DLL display title; runtime logic and observer source are unchanged. Both builds have zero warnings/errors; the renamed suite passed 81 managed checks with one optional Character Editor supplier skip and all 35 Python checks. This build record was added afterward.

The [public source repository](https://github.com/kt-nyx/wake-up) is renamed and retains its public history separately from private investigation ancestry. The pre-rename development bundle is recorded in [archived checkpoints](archive/development-checkpoints.md#final-pre-rename-cleanup-bundle). A new Wake-Up build does not inherit the live qualification of an earlier identity.


**Completed normal activation and final bounded investigations, 2026-09-07:** [Normal activation, HugsLib decision and remaining-cost assessment](product-activation-and-final-opportunities.md) are complete. Supported optimizations now activate on ordinary launches through in-game settings; PNG caching remains opt-in. A new-colony/tick/save/reload check passed both with Wake-Up physically absent and with normal activation. The HugsLib prototype was removed after the original report measured only 181 ms on the tested 99-package workload. Final larger observations found zero eligible late type searches.

| Last live-qualified checkpoint | Verified state |
|---|---|
| Retained runtime / package | `4422f6222a54c365618e154de66ba88ee048ab6f` |
| Runtime DLL SHA-256 | `722db104891f7d0c1add568684cefb3eca201b6e8bd839ef4b2a7bf05aaefec3` |
| Normal activation | In-game settings; established supported features default on, PNG cache off; explicit development arguments override settings |
| Verification | 80 managed and 32 fixture checks; zero skips or build warnings/errors; settings persistence and matched absent/present new-colony/save-reload checks |
| Larger assessment | 99 content packages plus Wake-Up/observer; first menu calls 2.78/3.45 s and zero late type searches; changed foreground inventory excludes the warm timing comparison |
| Final state | `close-r02-user-warm` captured normally at 2026-09-08 00:53:18 UTC; original exclusions restored, audit passed, game closed; local integration only |

Read the [user guide and supported-build policy](user-guide.md) before normal installation. This remains a development build with exact Windows GOG, Windows Steam rev590 and Linux rev600 contracts; source admission and Workshop uploads do not establish universal 1.6 support or live qualification. Earlier identities and measurements are in [development checkpoints](archive/development-checkpoints.md).
