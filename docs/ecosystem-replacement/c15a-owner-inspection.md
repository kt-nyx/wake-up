# Current retained candidate: inspection guide

The authorized overnight agent check completed the native, converted and reset
cycle on 21 September, finding and fixing a settings scrolling defect. See the
[C15A UI report](c15a-overnight-ui.md) for observations and limits. No fixture game
is left running. This guide remains the repeatable procedure; it does not require
a redundant owner inspection for the already authorized overnight scope. Future
operations must use the then-current authority. The companion app stays deferred.

## Exact package and workload

- Core build: `908444675b1ddd33b818381fb67498eeb5faafc1`, the tested settings scrolling correction.
  SHA-256: `3f96a341d7c2a777fb26b1c724e53d7e711ec46edf58f17a016bd6555de5d3e1`.
  Its `buildSourceRevision` is recorded in
  `artifacts/ecosystem-next-20260910/overnight-20260921/private-closeout/package-helper.json`.
  Three ordinary UI launches tested this DLL; prior combined evidence remains separately identified.
- Inspection observer: `0412c65dd650332f60231dfbd46ec08781e22f58`, GOG
  `gog-rev573-20260910-194017-cf1a1eb0`.
- Observer SHA-256: `2dfee0d628cdcba506e03fbc164eecf31d7a014656bdfc79e5c83a4f76f56a27`.
- Optional Windows quality helper: `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
  Use the refreshed private archive receipt above, retaining this qualified helper
  and its matching historical source `83a2930f` (not the raw-pixel trial helper).
  This helper serves explicit quality/export requests, not retired first-build work.
- One ten-package selection: `artifacts/ecosystem-next-20260910/c08/selection.json`,
  with candidate and observer inserted. It contains Prepatcher/Harmony, the game
  and DLC, Vanilla Expanded Framework and Vanilla Textures Expanded.
- One copied save: `c15a-owner-scene-setup-09` / `RloFixtureSmoke.rws`, SHA-256
  `e949173b778500b82bd8dfb65621b830ccb936cd013ad74e5396cdf33440f3e6`.
  Its automatic placement/save/reload passed; the original source save is unchanged.
  Use this same colony for the native menu-load/focus phase and quality windows;
  another modlist/save is unnecessary.
- Settings: `artifacts/ecosystem-next-20260910/c15a-final-20260919/inspection-c08-settings.json`.
  Retained features and background loading are enabled; retired flags are false.
  English/manual native menus; no automatic gameplay/image probe or private logging workaround.

The earlier `c15a-final-inspection-ready` profile verified the prior `4da2e2df`
core, this helper/observer and completed display save, then rolled back without launch.
The current core subsequently passed the three-launch UI cycle documented above.
No manual label is prepared or running. For any authorized repeat, use three unused
labels and change all references consistently if the example labels were already used.

## Ready display and source selection

The native display is centered at map coordinates **(33, 38)**. Its antenna row is
at z=35, x=27/31/35/39, rotations north/east/south/west respectively. The brazier is
at (38,42); the straw floor occupies x=30–33 and z=40–43. Do not activate an antenna's
summon action. The setup used native spawning and terrain assignment only in the
copied map, then normal save/reload verified the objects and rotations. No custom
terrain artwork or scene-building framework was added.

The six loose files in
`artifacts/ecosystem-next-20260910/c15a-inspection-corrections-20260919/inspection-quality-filter.txt`
select brazier color/mask DDS and all four antenna directional DDS files. They are a
subset of the eight verified sources in the earlier `inspection-prerequisites.json`;
the two Architect icon providers are deliberately excluded from conversion. Use
ordinary VTE artwork; the winning icon remains native DDS. The straw floor uses
ordinary game artwork, not the historical private PNG substitution. This guide does
not claim converted terrain or icons. Native save integrity is proved; visual
acceptance remains open.

## Operator preparation, only after scheduling

Use the sole checkout, fresh process/baseline checks and serial ownership. Run each
command separately, require success and retain its returned transaction. These are
three short stages using the same copied save, settings and deployed binaries.
Preparation and reset require the idle main menu and affect the **next launch**,
not the already loaded scene.

```powershell
$env:PYTHONUTF8='1'
$c15aBundle = Get-Content artifacts/ecosystem-next-20260910/overnight-20260921/private-closeout/package-helper.json -Raw | ConvertFrom-Json
$c15aCore = Join-Path artifacts/fixture-package $c15aBundle.buildSourceRevision
$c15aCoreDeploy = python scripts/fixture.py deploy --allow-other-session-game --package $c15aCore | ConvertFrom-Json
$c15aObserverDeploy = python scripts/fixture.py deploy-menu-observer --allow-other-session-game --package artifacts/fixture-menu-observer/0412c65dd650332f60231dfbd46ec08781e22f58 | ConvertFrom-Json
$c15aHelperDeploy = python scripts/fixture.py deploy-helper --allow-other-session-game --package $c15aBundle.folder | ConvertFrom-Json
$c15aCommon = @('--allow-other-session-game', '--mode', 'candidate', '--purpose', 'functional', '--activation', 'user', '--selection', 'artifacts/ecosystem-next-20260910/c08/selection.json', '--user-settings', 'artifacts/ecosystem-next-20260910/c15a-final-20260919/inspection-c08-settings.json', '--gameplay-save-from', 'c15a-owner-scene-setup-09', '--menu-observer', '--no-exit-after-menu-ready')
$c15aA = python scripts/fixture.py prepare @c15aCommon --label c15a-owner-quality-native-01 | ConvertFrom-Json
python scripts/fixture.py verify-prepared --allow-other-session-game --label c15a-owner-quality-native-01
python scripts/fixture.py launch --allow-other-session-game --label c15a-owner-quality-native-01 --authorize-live-launch --window-style minimized-noactivate
```

The functional ASTER option permits the separate session's normal game to remain
untouched; known fixture processes still block operations. Launch only at the agreed
time. Each launch command waits for normal exit and captures evidence before returning.

## A — Inspect native appearance and windows, then prepare

1. Bring the fixture forward. In native Options turn **Run in background** and
   **Pause on load** off. Load `RloFixtureSmoke` through the ordinary Load game menu,
   switch away during loading, wait for completion, then return once. Confirm a
   usable colony, loading display retired, and gameplay resuming on return without
   changed preferences or speed. If entry stalls, preserve the log for C14; the known
   InputLegacyModule logging is unchanged.
2. Pause normally and record native brazier edges/color/mask, all four antenna
   directions, straw floor near/far and Architect icon. Keep the same camera and
   scale for stages B and C. Return to the main menu, wait until idle, then open
   Wake-Up settings → **Prepare textures...**. Select Vanilla Textures Expanded and
   paste `inspection-quality-filter.txt` (six semicolon-separated paths), then open
   **Manage in-game quality for these selected mods/files...**.
3. Check readable/reachable controls and long rows. Choose half dimensions and scan
   effective selection. The six-path filter selects the brazier and all antenna
   directions; the window adds required related providers/masks. There are no per-row
   selection toggles. Record the actual admitted groups/members and any refusal. The
   filter alone does not prove that every listed source can be converted. Keep the
   icon and ordinary straw floor as native comparisons.
4. Inspect coverage and size estimates, prepare, cancel if still running, then resume to
   completion. Reopen saved choices. A batch completed before Cancel is clicked does
   not establish manual cancellation; retain existing automatic evidence separately.
   **Do not judge converted scene appearance or reset yet.** Quit to OS normally.

## B — Restart with the prepared store, inspect conversion, then reset

After A exits normally and is captured, roll back only its preparation. Keep the
core/helper/observer deployments in place so the candidate identity stays identical.
Carry A's actual captured texture store (including its saved quality choices):

```powershell
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aA.transaction
$c15aB = python scripts/fixture.py prepare @c15aCommon --label c15a-owner-quality-prepared-01 --prepared-cache-from c15a-owner-quality-native-01 | ConvertFrom-Json
python scripts/fixture.py verify-prepared --allow-other-session-game --label c15a-owner-quality-prepared-01
python scripts/fixture.py launch --allow-other-session-game --label c15a-owner-quality-prepared-01 --authorize-live-launch --window-style minimized-noactivate
```

Load the same `RloFixtureSmoke` save and pause. Reopen the same filtered quality window:
confirm saved choices survived and identify which groups actually loaded converted
output. Compare those groups' appearance with A at the same camera/scale, including
brazier alpha/mask and admitted antenna directions. Record refusals as native; inspect
the floor and icon separately as native comparisons. Record usability, correctness
and preference for reduced detail separately.

Then return to the idle main menu, reopen quality management with the same six-path
filter, choose Native quality, scan, and reset these selections. Confirm the saved
choices show native. Both preparation and reset require the idle main menu; pausing
a colony is insufficient. **This reset takes effect on the
next launch.** Quit to OS normally and preserve the captured reset store and logs.
Do not clear the whole store or delete its group index. If carry admission rejects
an empty/missing store, preserve the reason and leave persistence unproved.

## C — Restart with the reset store and verify native output

After B exits normally and is captured, carry B's actual reset store into C:

```powershell
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aB.transaction
$c15aC = python scripts/fixture.py prepare @c15aCommon --label c15a-owner-quality-reset-01 --prepared-cache-from c15a-owner-quality-prepared-01 | ConvertFrom-Json
python scripts/fixture.py verify-prepared --allow-other-session-game --label c15a-owner-quality-reset-01
python scripts/fixture.py launch --allow-other-session-game --label c15a-owner-quality-reset-01 --authorize-live-launch --window-style minimized-noactivate
```

Load the same save, pause, reopen the same selections and verify native choices
persisted. Compare the formerly converted groups with A's native appearance at the
same camera/scale. A fresh empty profile without the carried store is not reset
persistence evidence. Quit to OS normally; preserve observations, choices and logs.
Manual captures do not establish an automatic or visual pass by themselves.

All three stages reuse the explicit Wake-Up settings JSON. Fixture preparation
reseeds native Options; it does not carry their edits with the texture store. Reapply
Run in background/Pause on load off before any repeated focus check, and use the same
camera/scale for appearance comparisons. The required focus-return check is in A.

After C's normal exit/capture, restore owned transactions in reverse order:

```powershell
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aC.transaction
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aHelperDeploy.transaction
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aObserverDeploy.transaction
python scripts/fixture.py rollback --allow-other-session-game --transaction $c15aCoreDeploy.transaction
```

If stopping earlier, roll back only the currently owned preparation, then the three
deployments in that same order. Verify seven baseline hashes, eleven absences and
the supplier repair plus metadata audit. Return stopped ownership; never close the
normal game or unrelated applications.

This repeat procedure does not expand the bounded observations in the UI report.
Performance requires an active explicit unattended grant; the current grant ends by
14:00 UTC on 21 September or earlier on owner return. Windows Steam then Deck/Linux
and publication remain separate gates.
