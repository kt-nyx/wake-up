# C08 owner-assisted visual inspection

## Current scope — 11 September 2026 owner amendment

**Companion-app inspection canceled; app/6c optional post-release.** The owner explicitly deprioritized the companion app to advance other features. No app visual acceptance was received. The authorized app window opened successfully and was already closed at the scope change. The staged manual game was never launched. The fixture has now been restored, so the old label and commands below must not be launched as a current prepared inspection.

Remaining **in-game** preparation/quality/sampling/reset/coverage windows and scene appearance checks are still required during combined GOG correctness **before release**. Inspect control readability and prepare/cancel/resume, saved choices, coverage/refusals, alpha/masks, four actual building directions, UI/provider winners, floor near/far zoom and native comparison/reset/restart. Existing automated evidence does not pass those visual checks. The app is not a prerequisite; a later operator will prepare a current combined fixture and preserve any manual display save before restart. Owner-coordinated visible permission and separately authorized performance windows remain distinct.

All original packages, app work and staging evidence remain preserved. Restoration through `20260911-152942-c8833778` passed seven original hashes, three absences and metadata audit; no fixture/app/helper operation remains. C08 returns ownership to the steward for the next implementation assignment. See `artifacts/ecosystem-next-20260910/c08-visible-stage/restoration.json` and `scope-amendment/` for preserved profile/run/close receipts. The following guide is **historical staging evidence**, not an active companion-app or successor gate.

## Historical inspection plan and staged checkpoint

The automated checks verify saved choices, texture data, material references
and normal process exit. They do not verify whether the windows are usable or
whether changed artwork looks acceptable. This short inspection remains an
owner-coordinated step after the corrected functional package is ready.

The current corrected core/observer/app source is
`83a2930f60967eba4fcfb8e6acef10f35a415c62`; use the exact package identities in
the latest C08 follow-up record. The standalone package root is
`artifacts/preparation-package/83a2930f60967eba4fcfb8e6acef10f35a415c62/`.
Its `WakeUp.Preparation/WakeUp.Preparation.exe` includes the approved runtime.
It now prepares the real resolved terrain and declaratively patched icon
selection before first game load; that route passed the functional capture
`c08-external-seven-03` but still needs visible inspection.

## Coordinated setup

### Ready checkpoint — 11 September 2026

`c08-owner-visible-01` is prepared and verified, but has never been launched.
C08 retains exclusive operating ownership. **The fixture intentionally differs
from its original state; do not dispatch another operating team or restore it
before this coordinated inspection.** No app window was opened for staging.

The exact package source remains `83a2930f60967eba4fcfb8e6acef10f35a415c62`;
the documentation review base is `9cc70b78446562f6314037f4bd02eeb28ed8428f`.
No package was rebuilt. The GOG generation remains
`gog-rev573-20260910-194017-cf1a1eb0`. Complete paths, hashes, settings,
source receipts and rollback boundary are in the ignored local record
`artifacts/ecosystem-next-20260910/c08-visible-stage/ready.json`.

The standalone backend prepared the brazier color/mask pair at half dimensions
and the seven files listed below at quarter dimensions, all with trilinear
filtering, anisotropy 4 and mip bias 0.5. Both batches completed: nine prepared,
zero reused, skipped or failed; source files and mod configuration unchanged.
Prepared textures, PNG cache, first-build images, static atlases, atlas batching
and PSD support are enabled; texture-storage compression is disabled.

No existing captured save matched this generation and selection. One necessary
automatic setup run, `c08-visible-scene-setup-01`, created a 75 by 75 colony with
three colonists, saved/reloaded it and exited normally. Its automatic and gameplay
checks passed. The final label contains its verified `RloFixtureSmoke` save.
**This is a ready colony, not a finished display scene:** the existing builder
does not place inspection objects. No new scene-building system was introduced.

### Short first inspection

Allow **15–25 minutes** for the first pass: roughly 3–5 minutes for the app,
5–10 minutes for placing the small display, and 5–10 minutes for in-game windows
and appearance. This is a planning estimate from the steps, not a measured test
duration. A native-reset/restart comparison may need another 5–10 minutes.

1. After the owner coordinates a visible window, C08 supplies the exact app
   executable above. Paste the three fixture paths below; leave additional
   roots empty. Paste the contents of
   `artifacts/ecosystem-next-20260910/c08-visible-stage/source-filter.txt`
   into **Files or folders (; separated)**. Click **Scan / dry run**, then
   **Select all mods**. The file filter keeps the selection to nine files.
2. Resize the window, inspect a long row, and preview the selected output.
   Check the **Saved quality choice** column separately from pending controls.
   For the pairing check, filter to the brazier color alone and preview half
   dimensions: its required mask should appear. Keep this first app pass to
   scan/preview, then close it. Writing/resetting from the app changes the saved
   profile and requires C08 to refresh the preparation record before launch.
3. C08 re-verifies and launches the prepared label only after coordination.
   In the fixture choose **Load game → RloFixtureSmoke**, then pause. This save
   does not load automatically. Enable Development mode in this isolated game's
   options and its god-mode building tool for the small display. Place a brazier
   from Furniture, four **mechband antennas** from Biotech, rotated into four
   directions, and a small patch of **straw matting** from Floors. The actual
   antenna definition is `BurnoutMechlinkBooster`; use completed buildings for
   inspection. Do not activate its summon-warqueen action. If the build tool
   does not expose the antenna, stop that scene check and report it to C08.
4. Inspect transparent edges and the brazier's material color, each antenna
   direction, and the floor at near/far zoom. The floor deliberately shows the
   private concrete test artwork through the real straw-matting definition.
   Inspect the Architect button: VTE's DDS is the winning provider; the earlier
   observer PNG is distinct test artwork, not the expected global button image.
5. Open Wake-Up's preparation and in-game quality windows using the path below.
   Check readability, saved choices and previews. Report appearance/window
   issues separately from preference for reduced detail. Exit through **Quit
   to OS** so C08 can capture the run normally. The write/cancel/reset and native
   comparison steps below remain a coordinated continuation, not a completed
   result of this staging checkpoint.

The app has no GUI preset-loading switch, so the three path pastes cannot be
eliminated without changing the reviewed app. Development mode and scene
placement are likewise still owner actions; no desktop automation was used.

### Operator commands and eventual restoration

From the canonical checkout, after the steward relays owner coordination:

```powershell
python scripts/fixture.py verify-prepared --label c08-owner-visible-01
python scripts/fixture.py launch --label c08-owner-visible-01 --authorize-live-launch --window-style normal
```

These commands have **not** been used to launch the manual label. Its observer
remains enabled and automatic exit is disabled. The launcher waits for normal
owner exit and captures the run. Preserve any owner-created display save before
preparing another label: the current save-copy admission accepts automatic
gameplay captures only and does not automatically carry a manual display forward.
C08 must resolve that continuation explicitly rather than promise a one-click
native restart on the same display.

After the coordinated inspection finishes and all fixture/app/helper processes
have exited, restoration returns through the first staging transaction:

```powershell
python artifacts/ecosystem-next-20260910/c08-visible-stage/restore-c08.py 20260911-152942-c8833778
```

The restoration script uses `fixture.py rollback`, then checks the original seven
hashes, three absent endpoints and metadata audit. It must not run at the ready
checkpoint. Recovery transactions, original identities and job receipts remain
under the fixture and the ignored `c08-visible-stage` directory. Normal Steam
RimWorld PID 24740 was separately identified during staging and left untouched.

The steward selects the exact corrected core, helper and standalone package
from the C08 correction record, verifies their identities, and prepares the
single isolated GOG fixture. A visible/manual inspection launch needs a
coordinated owner window; the background functional grant does not cover it.
The steward records the selected artwork and normal exit, and restores the
fixture after inspection. Performance measurement is a separate request.

For the standalone window, use these existing fixture locations:

- Game: `Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game`
- Mod configuration: `Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile\Config\ModsConfig.xml`
- Output profile: `Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\profile`

The steward supplies the exact packaged `WakeUp.Preparation.exe` and keeps the
fixture game closed while that app writes prepared choices. Additional mod
roots are unnecessary for this fixture. The small brazier color/mask group is
available at `Textures/Things/Building/Misc/Brazier.dds`; selecting the color
must also show the required mask. The corrected coverage receipt supplies the
actual selected directional family, terrain and icon names for scene review.

For the newly completed external coverage, use the declared private artwork
variant and the seven paths in
`artifacts/ecosystem-next-20260910/c08-unity-external/external-seven.json`.
These select StrawMatting terrain, all four MechbandAntenna directions and
the PNG/DDS Architect icon providers. Preview should admit all seven at
quarter dimensions, trilinear filtering, anisotropy 4 and mip bias 0.5, while
still explaining runtime validation. The terrain image is explicitly substituted
test artwork, so its use does not establish an owner's visual preference for
an unmodified vanilla floor. Do not activate the variant outside the fixture.

## Window and control checks

1. Open the standalone app at the owner's usual display scaling. Confirm that
   path fields, mod and source selection, preset, sampling controls, output
   preview, progress, cancel and reset are readable and reachable. Resize the
   window and inspect a long provider/path row.
2. Scan the fixture and select the brazier color. Choose half dimensions and
   preview. Confirm that its mask appears as required, the new dimensions and
   memory/storage estimates are understandable, and known refusals are visible
   before preparation. Changing a choice must require a fresh preview.
3. Prepare the selection, cancel during a suitable small multi-item batch,
   then resume. Completed work should remain available. Reopen the selection
   and confirm that the saved preset and sampling values are shown separately
   from pending controls. The automated cancellation receipt covers fast
   batches that finish before a person can click Cancel.
4. In the coordinated fixture game, open Wake-Up's mod settings, then
   **Prepare textures...** and **Manage in-game quality for these selected
   mods/files...**. Check the same selection, saved-choice rows, progress,
   cancellation and native reset. Read the clear label before any optional
   clear test: it removes saved quality choices as well as native cache and
   exports, with ordinary output restored on restart.

## Appearance checks

Use the exact qualified selection in a coordinated fixture scene. Compare
native, half and quarter choices at the same camera and UI scale. Inspect
transparent edges and colored masks, all four directional views, terrain at
near and far zoom, and the unrelated UI icon. Look for fringes, flipped rows,
wrong direction, severe blur, broken masks or a globally shadowed provider
replacing the expected icon. Reduced detail is expected at reduced dimensions;
the owner decides whether that tradeoff is useful.

Record each observation and any screenshot the owner elects to provide, with
the package, preset, sampling values and selected artwork. A window/controller
pass, a scene-appearance pass and an owner's quality preference are distinct
outcomes. After native reset and a normal restart, confirm ordinary detail and
sampling return. Close normally so the steward can capture and restore state.
