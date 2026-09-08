# Development and fixture

Use focused offline checks first, then an explicitly authorized fixture comparison when the question needs real game behavior. A successful build, a successful menu startup, matching behavior and a loading-time improvement are separate results.

## Development policy

The objective is faster, reliable RimWorld loading across modlists. Prioritize shared loading work. As of the owner’s 2026-09-07 scope expansion, targeted loading/UI preparation improvements for individual mods are also allowed when implemented as soft dependencies: the mod must remain optional, and changed or unsupported supplier code must fall back safely. Exact binary and method identity checks may restrict activation to verified builds without forcing users to retain those builds. The fixture remains a representative real-world workload. A general mechanism may help some workloads more than others; it need not produce the same gain in every modlist.

Read [results and decisions](results.md) before pursuing a related idea. Do not repeat an experiment that has already established no useful benefit under the relevant conditions. Reconsider it only when materially new evidence, a different mechanism or a relevant change in conditions addresses the recorded reason it failed; briefly record what changed and why that could alter the outcome. Renaming an old approach or rerunning the same comparison is not a new premise. Keep confirmed negative results separate from invalid tests, inconclusive measurements and untested conditionsâ€”for example, the DDS warm-start result does not settle first-read performance.

Outside these restrictions and the operational boundaries below, development is open-ended. No particular algorithm, caching strategy, concurrency model or architecture is prescribed or permanently ruled out as a whole. The current implementation and suggested next steps describe a starting point, not a required roadmap. Choose investigations and implementation methods from evidence about avoidable or overlapping loading work, likely user benefit, effort and compatibility risk.

Use enough verification to answer the actual question, and trust adequate existing evidence. Do not retain complexity merely because it was implemented, or expand testing, profiling and process into separate projects without a concrete need. Within an authorized task, routine design choices, focused checks and revisions do not require repeated permission. These policies do not grant additional authority to launch, publish or affect normal user data.

## Workspace and ownership

Use `Z:\Development\Large Projects\RimWorld Loading Optimizer` as the only checkout. Begin implementation from current `main` on a `codex/` branch, with one owner for edits, builds, deployment and launches. Read-only review may run alongside preparation; pause other substantial local work during measurements. Keep research, build output and raw evidence under ignored `artifacts/`. Use the tracked commit guard:

```powershell
git -c core.hooksPath=.githooks commit -m "Describe the completed change"
```

The only fixture is `.rlo-test-instance` at the checkout root. It contains private copies of the game, frozen mods, an isolated profile and captured results. Do not create another fixture or write normal Steam, Workshop, profile, save or cache state. Do not refresh the frozen collection merely to prepare another run. Routine preparation checks existing metadata; full content audits are a separate operation for a specific drift question.

## Build and offline checks

The repository pins .NET SDK `10.0.400` in [global.json](../global.json). The existing fixture supplies the reviewed GOG `Assembly-CSharp.dll` and frozen Harmony references. Do not substitute a convenient installed game DLL. Run from the checkout:

```powershell
python scripts/op7_fixture.py test
python scripts/op7_fixture.py build
```

`test` restores locked dependencies, builds, runs the focused managed tests and Python fixture checks without launching the game. An optional `--test-filter` narrows managed checks while retaining fixture safety checks. `build` requires committed build inputs and creates a revision-labelled package and adjacent JSON receipt under `artifacts/op7-fixture-package/`. It checks the selected references, packaged source revision and file identities. If that revision already has a package, use the existing package or a new committed source revision; do not overwrite its evidence.

The package contains only `About/About.xml` and `Assemblies/RimWorldLoadingOptimizer.RimWorld.dll`. It does not contain Core, Harmony or Gagarin DLLs. The local package declares Prepatcher as a dependency for the reviewed environment. This packaging does not establish a public support matrix.

## Explicit deployment and preparation

Building does not deploy. Use the exact path returned by a successful build, then check that deployment itself exits successfully:

```powershell
python scripts/op7_fixture.py deploy --package <package-path-returned-by-build>
```

Before preparing a series, read `.rlo-test-instance/candidate.json`: `sourceRevision` must equal the intended package revision. After preparation, require the same revision in `.rlo-test-instance/results/<label>/run.json` under `candidate.sourceRevision`. A receipt or log file existing is not proof that a deployment command succeeded. The tool refuses old packages containing the retired Core DLL.

Build and deploy the separate menu observer after its source changes, or if the fixture does not yet have its matching package:

```powershell
python scripts/op7_fixture.py build-menu-observer
python scripts/op7_fixture.py deploy-menu-observer --package <observer-package-path-returned-by-build>
```

Prepare a unique label. This example enables the two searches in the small activation lane:

```powershell
python scripts/op7_fixture.py prepare --mode candidate --strategy startup-searches --lane activation --label next-s01-candidate --menu-observer --exit-after-menu-ready
python scripts/op7_fixture.py verify-prepared --label next-s01-candidate
```

The supported lanes are `activation` (small official content with the required bootstrap environment), `xml-expanded` (the documented frozen XML-heavy selection encoded by the tool) and `representative` (the frozen enabled order, subject to recorded exclusions). There is no current `presets`, medium or full CLI lane: older records used earlier selectors. Representative is the large comparison route; its use still depends on current workload authorization.

`--mode absent` physically parks RLO outside game discovery and preserves the comparison content order. `--mode baseline` keeps the selected original-path timers. `--mode candidate` enables the selected supported optimization. The strategy defaults to `startup-searches`; `def-lookup` and `type-lookup` permit isolated comparisons. Preparation defaults to timing observation, the independent menu observer and normal automatic exit. It also records the fixture-only background-loading preference.

Gagarin defaults off. `--gagarin-cache timing`, `on` or `verify` requires candidate startup searches. Preparation resets the application profile from its frozen seed. For a matching Gagarin cache-hit comparison, `--foreign-cache-from <successful-captured-label>` restores only the captured MissileGirl cache after checking content, package and observer compatibility. Inspect `foreignCacheFrom` and `restoredCacheKinds` in the run record; do not infer a cache hit from preparation alone. Actual supplier and optimization receipts must confirm it. Do not mix verification mode into only one timed arm.

`exclude-mod --package-id <exact-id>` and `restore-mod --package-id <exact-id>` maintain a reversible fixture selection without editing frozen files. An exclusion changes the workload and must match between comparison arms; it is not an optimizer gain. Follow current task scope rather than excluding mods opportunistically.

If ordinary startup rewrites an inventoried mod file, `restore-runtime-file --package-id <exact-id> --relative-path <inventoried-path>` can restore its frozen content and timestamp after the game exits. The action first verifies that the original source still matches the frozen length and complete SHA-256, preserves the overwritten fixture file in a recovery transaction, and refuses unlisted paths or newer source bytes. It does not refresh the manifest or write the original source. Keep the captured run and restoration receipt together when this maintenance is needed between comparisons.

## Authorized launch and capture

Live testing requires the owner's authorization for the task and workload. Given that authorization, the prepared label is launched with:

```powershell
python scripts/op7_fixture.py launch --label next-s01-candidate --authorize-live-launch
```

The launcher rechecks the prepared identity, profile, package and order. It waits for actual process exit and captures evidence before returning. Do not build, deploy, switch branches or start another run during a measurement. Do not use UI automation or interfere with another application. A label in an old document is historical; the authoritative selection is the current `prepared.json` and its matching run record.

The observer marks menu readiness after the first completed main-menu repaint. Both the launcher and observer use the Windows high-resolution counter, so polling delay does not inflate the reported elapsed time. The observer writes `profile/FixtureMenuObserver/menu-ready.json` and logs its event. One second later it requests the game's normal shutdown from an update callback, only while still at the startup menu with no game loaded. The one-second delay and shutdown time are excluded from loading time.

Inspect the captured `results/<label>/run.json`, profile log and relevant RLO receipts. A routine automatic success requires a valid `menuReadyObservation`, actual normal exit code zero and `automaticTestPassed=true`. Use `menuReadyObservation.elapsedSeconds`, not process lifetime, for complete startup. Installation or activation refusal is not a candidate result.

The existing bounded failure handling stops a run sequence when there is no menu signal after 30 minutes or no process exit within 60 seconds after the menu event. It records failure and leaves the process for investigation. Do not start another launch while it remains alive. Preserve evidence before any necessary forced closure, verify that the exact process belongs to this fixture, and treat forced exit as failure. Use the tool's `capture` action for a verified exited run if automatic capture did not complete; do not fabricate a success or overwrite an existing capture.

For authorized manual inspection use `--no-exit-after-menu-ready`, then wait for a normal-exit report. The normal automatic mode requires no human closure report. The observer works with RLO absent and must match between ordinary comparison arms. Its historical small-profile qualification is described in the [history index](archive/README.md#observer-history).

## PNG qualification and cache reuse

Use `--png cache` with candidate `startup-searches` to enable the retained processed-PNG cache; its default is `off`. `--png control` records original PNG processing and `--png verify-cache` adds sampled rendering comparisons. Normal DDS precedence is preserved. Only an explicit `--png-source original` qualification comparison selects existing PNG siblings instead of their DDS counterparts. Match that source choice between arms and report it as a PNG workload, not a gain for the normal DDS-heavy fixture.

`--png-cache-from <label>` restores only a normally completed capture's processed-PNG entries after checking file identities and matching package, content, lane, selectors, profile overrides and observer. The first cache build and subsequent hits are different performance conditions. Keep all source and payload validation inside startup timing; verification replay is a separate correctness arm. The cache needs no external conversion tool or mod-specific allowlist. Unsupported graphics/settings retain original processing.

## Character Editor preset preparation

`--character-presets on` enables optional startup preset lookup reuse in candidate `startup-searches`; `timing` measures the original preset construction with the same receipts, and default `off` installs nothing. The runtime selector is `--rlo-character-presets=on`. Character Editor is not a required package or assembly reference. Only the inspected 1.6.3.3 assembly, full file hash, module identity, method bodies and unmodified relevant patch chain are admitted. Updated or absent suppliers retain ordinary loading. This does not pin a Workshop subscription or change supplier files.

The original preset constructors and public dictionary getter remain intact. Only the first startup object/turret creation scopes may reuse the original lookup dictionary; later calls, changed definition lists, nested/cross-thread calls and foreign patches use fresh original lookups. Reuse ends at the loaded menu. Inspect `character-presets.jsonl` for scope counts, preset-content hashes, actual hits, no exceptions and zero retained entries after completion.

## Loading Progress repaint pauses

`--loading-progress coalesce` enables the optional startup repaint improvement in candidate mode; default `off` leaves it disabled. The runtime command-line selector is `--rlo-loading-progress=coalesce`. It supports the frozen Loading Progress 0.14.0 identity, preserves the native loading time budget and disables coalescing at the loaded menu. Missing or unknown Loading Progress versions and interfering patches use ordinary loading. Progress can update less frequently within the existing budget. Inspect `repaint-coalescing.jsonl` for installation and a completed receipt with suppressed pauses and no refusals. This does not enable Gagarin XML reuse through Loading Progress; the separately tested XML bridge was not retained.

## Giddy-Up riding-offset preparation

`--giddy-textures on` enables optional center-column readback in candidate `startup-searches`; default `off` installs nothing. `timing` observes original readback and `verify` compares every original center-column alpha value. Offset calculation, setup order and settings writes remain intact. Match diagnostics between performance arms; verification replay is separate.

The Giddy-Up 2.2.5.0 assembly must match its full file hash, module and four loaded bodies, with compatible patch state. Only Direct3D 11 is admitted. Missing/changed suppliers, unsupported conditions, later calls and other threads retain ordinary behavior. Inspect `giddy-textures.jsonl` for actual hits, matching offset hashes, zero errors/mismatches and released textures. `offsetMs` measures complete offset calls; `scopeThroughMenuMs` includes later startup work. See [qualification and limits](further-loading-improvements.md).

## Comparisons and stopping

Match content and order, selected features, supplier/application caches, observer and diagnostics. Use a reverse original control after a promising candidate to distinguish actual savings from Windows keeping recently read files in memory. Fresh application caches do not mean cold operating-system file caches. Do not evict global caches or create resource pressure merely to produce a win.

Show complete stage cost, including preparation, worker startup, joins and ordered completion where applicable. Nested or overlapping timers must not be added. Compare whole startup separately; stage savings may be too small or variable to improve the user-visible endpoint. Report exact runs and uncertainty instead of a universal percentage. Trust existing adequate semantics checks and add checks only for behavior actually changed. Stop a route when matched repeats erase its practical benefit.

Keep concise decisions and useful evidence summaries in [results](results.md). Raw logs, binary identities, private content and decompiled source remain ignored. Do not grow a new testing or documentation framework without a concrete product need.

## Publication

No release has been published. Normal activation and an exact development support policy are implemented in the [user guide](user-guide.md), with focused new-colony/save-reload coverage in the [qualification report](product-activation-and-final-opportunities.md). Broader game/dependency support and gameplay coverage remain necessary before broader release claims. The exact pinned GOG fixture is a development contract; it does not qualify latest Steam or other platforms.

The approved licensing direction is implemented in [LICENSE.md](../LICENSE.md): GPL-3.0-or-later with a separate linking permission, CC BY-SA 4.0 documentation/artwork, DCO and explicit Workshop contribution consent. See [release preparation](release-preparation.md) for packaging and the remaining Steam/copy work. The archived assessment is historical. Public source publication is separate from a qualified Workshop binary release.

## Normal activation and post-startup qualification

`prepare --mode candidate --activation user` includes the installed product and isolated save-data path but supplies no RLO feature selectors. The mod reads its ordinary settings, including defaults when no file exists. This mode refuses simultaneous feature-selector overrides, preventing mislabeled comparisons. Explicit fixture controls continue to bypass normal settings.

The separate observer supports `--residual-probe` for bounded first-menu-call and post-cutoff type-search observation. This is a qualification arm, not an ordinary timing comparison. It adds no diagnostic code to the product package.

`--gameplay-smoke` explicitly selects a fixture-only small new-colony/tick/save/reload check after the independent menu timestamp. It also checks normal settings persistence when RLO is present. It uses native game APIs, never UI automation or a normal-user save. The observer writes `FixtureMenuObserver/gameplay-smoke.json`, and launch records `gameplayTestPassed` separately from `automaticTestPassed`; both must pass. Failure is captured and rejected even when the process exits zero. This explicit mode allows ten minutes after menu readiness for gameplay and normal exit; routine startup comparisons retain the sixty-second limit and ordinary automatic exit. Do not count gameplay-smoke menu timings as scored speed comparisons. A nine-minute in-game timeout attempts normal shutdown and writes failure evidence.

Existing frozen seeds remain unchanged. Generated saves and settings belong only to the fixture capture, and the next ordinary preparation resets its profile from those seeds. These checks qualify a focused scenario; they do not establish arbitrary combat, long-running colony behavior or a public support matrix.
