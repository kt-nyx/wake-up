# C14: loading while unfocused

Wake-Up temporarily permits verified loading work to continue when RimWorld is
not the foreground application. It then returns to the player's current
background preference, including a preference changed during the load. It does
not unpause the game or change the saved pause-on-load policy.

This is an implementation record, not independent acceptance or supplier-removal
advice. Performance and platform qualification remain separate. All earlier
unattended windows are closed under the owner's 19 September instruction.
Current work uses isolated GOG functional checks only. C10 on-demand assets,
C11 progressive detail and the companion remain deferred; C14 depends only on
native loading. Earlier dated grants and results below are historical.

## Retained-candidate correctness handoff — 19 September 2026

The targeted hold exposed and corrected a real bug: gameplay could advance after
an unfocused load even though background gameplay was off. The previous guard
ended after the completion frame, while Unity still incorrectly reported the
minimized fixture as focused. The corrected product kept the saved tick unchanged
through a real five-second hold. Ordinary native-logging save/new-colony entry and
an actual pawn portal round trip also completed. **Actual owner focus return and
visible usability remain unqualified; this is a stopped handoff for independent
review, not full C14 acceptance.**

Action `wake-up-c14-retained-correctness-20260919-9e1ea53d` began at clean
`9e1ea53d15208847a89ef918b6bb24ae2e6173ca`, with exclusive ownership transferred
from C15A. The parent steward is now `01a08711-7899-7ff3-a1b5-4a0b105ba661`.
Current instructions permit incidental fixture foreground during GOG correctness
while the owner uses the PC, but no desktop focus manipulation or performance
work. The separate normal ASTER game remained untouched. The reviewed
`--allow-other-session-game` option used fresh Windows session checks; no permanent
PID exemption was introduced.

Preflight matched seven baseline hashes, eleven absences, 315 active packages and
the retained supplier repair. Initial core `40451d9a56f5d5967b91db3ed17ff2c067039a29`
and observer `9b2d10d5583e7410635d579003e72caafe8808d9` were reused after verifying
their source equivalence to the assigned base. The 25 reviewed September 13
foreground passes were not replayed. C15A's six retired routes remain native;
no deferred feature or optimization pilot was resumed.

### What changed and what the game actually did

Product `4da2e2df4709ff6a8229def22bbf8155b49b5cc5` keeps a completed load's
gameplay guard until actual focus returns or the current preference permits
background gameplay. Loading ownership still ends and engine permission returns
to the current preference at completion. The guard does not set simulation speed,
pause-on-load or saved preferences, and it does not take over ordinary later focus
loss after the player has returned. A new load can arm its own guard. Shared
compatibility refusal releases it; menu cancellation remains canceled through
the final loading-boundary drain. The existing Windows foreground observation is
read-only. No engine logging, native method identities or retired feature paths
were changed.

The private observer adds two narrowly selected cases. `portal-job-true` orders
the actual native `EnterPortal` pawn job: natural walking, its native wait, map
generation/transfer, job completion and return through the native exit. It never
calls a private transfer step or generates the target map beforehand. Walking is
deliberately enabled gameplay, with background preference true and Normal speed;
it is not evidence of false-preference background loading. `save-hold-false-unpaused`
leaves the completed save's speed, preferences, focus and ticks untouched for five
seconds, observes native loading-display state, then exits. Its result stays
pending until the hold succeeds. After the immutable success receipt, the observer
pauses only for shutdown. There are no worker-thread Unity calls or focus writes.

All seven runs below used functional purpose, the eight frozen activation packages
plus candidate and observer, the verified private `c13-final-shown-03` save where
needed, muted 1280×720 output, **ordinary native stack logging**, and normal exit
code zero. Five passed; the two failures are retained as failures. Raw captures
are `.rlo-test-instance/results/c14-r3-*/`. Exact source/settings/receipt hashes and
transactions are in `artifacts/ecosystem-next-20260910/c14-retained-20260919/handoff.json`.

| Run suffix after `c14-r3-` | Result and exact scope |
|---|---|
| `native-stack-control-01` | Passed copied-save entry with `backgroundLoading=false`, native background preference true and PauseOnLoad true. Reused `40451d9a` core / `9b2d10d5` observer. The known input dependency error still logged; no formatter workaround was selected. |
| `native-stack-save-02` | Passed enabled-candidate copied-save entry, true preference, native paused tick 601 → 602, same packages and native logging. |
| `native-stack-colony-03` | Passed enabled-candidate world/new-colony entry, true preference, native paused tick 0 → 1, same packages and native logging. These are real API-driven entries, not owner-operated menu/visual acceptance. |
| `portal-job-04` | Failed the observer's gameplay setup on core `40451d9a` / observer `48a5b32e`: the native speed setter rejected Normal while the normal post-load fade still denied player control. No pawn traversal occurred. The observer was corrected to wait for native player control and verify the accepted speed before issuing the job; no native pause bypass. |
| `hold-05` | Failed on core `40451d9a` / observer `09130f1c`: completion tick 601 became 602 after 0.272 seconds. Windows foreground PID 8128 differed from fixture PID 23760; the window was minimized, Unity's focused flag remained true, engine/preference were false, PauseOnLoad false and speed Normal. This is the concrete product defect, not a timeout or speed measurement. |
| `hold-corrected-06` | Passed matching `4da2e2df` core/observer. Actual Windows nonforeground/minimized state persisted; saved, completion and hold ticks all stayed **601 for 5.001 seconds**, Normal speed, engine/preference false and PauseOnLoad false. Loading-display state was inactive. `postCompletionHoldQualified=true`; `screenVisibilityQualified=false`; `holdReturnedToForeground=false`. Unity's stale true flag remained, so actual native focus-return behavior is still separate. |
| `portal-job-corrected-07` | Passed matching `4da2e2df` packages. Same pawn `Human72` walked and waited, transferred to the native 75-cell pocket at tick 799, returned through its native exit at tick 891, and its return job ended by tick 893. Outbound wait counts observed 89 → 1; return 90 → 1. Both maps, reciprocal links and source membership were correct, and generation ownership was clear. No displayed-map/camera or visible-usability claim. |

The historically diagnosed Unity stack-formatting loop was inspected before the
single current native control. Its passing result allowed the two candidate entry
checks to proceed. All seven current logs retain the `UnityEngine.InputLegacyModule`
dependency error. These passes demonstrate the stated ordinary-logging entries;
they do **not** establish a repaired engine/bootstrap or make the earlier stalls
disappear. No repeated loop, input-preload retry, security change or broad engine
workaround was attempted.

### Verification, restoration and remaining owner check

**70 focused managed background-loading tests passed**, including persistent
post-load protection, focus/preference release, menu cancellation through queue
drain, subsequent loads, and foreign shared hooks arriving after completion.
Two Python suite invocations encountered intermittent `WinError 5` renames in
temporary Steam-transition test directories (two errors, then three among 79
tests). All four affected cases and the C14/native-stack functional-boundary
checks passed in a six-test targeted rerun. These suite failures remain recorded;
there was no harness/security change or actual Steam fixture promotion. Final
core and observer builds passed. Bounded read-only review found no remaining
concrete blocker after the two guard-cleanup corrections. Steward review remains
independent. Evidence includes `test-hold-correction-final.txt`,
`test-fixture-targeted-recheck.txt` and `build-*-corrected.txt` in the current
evidence folder.

| Final tested artifact | Identity |
|---|---|
| Product and observer source | `4da2e2df4709ff6a8229def22bbf8155b49b5cc5` |
| Core package | `artifacts/fixture-package/4da2e2df4709ff6a8229def22bbf8155b49b5cc5` |
| Core DLL SHA-256 | `b826380f2be6b69558d2093c7f855923d7c823788eb2abe03c0b7fb94a614bba` |
| Observer package | `artifacts/fixture-menu-observer/4da2e2df4709ff6a8229def22bbf8155b49b5cc5` |
| Observer DLL SHA-256 | `19ed687019fbaf4bc591b7c599b46e19516b0d4ee9e6638c00c2d6c74e9ce301` |
| Final core / observer deployment, both restored | `20260919-152031-e066f773` / `20260919-152035-6875ff82` |

All **13 owned transactions** (seven run profiles, six deployments) are restored.
`restoration.json` at **2026-09-19 19:22:57 UTC** matches all seven baseline hashes,
eleven absences, 315 original active packages and the repair hash/size/timestamp;
no pending transaction or fixture process remains. The metadata audit passes.
The owned idle MSBuild server was shut down normally; the final process snapshot
contains only the unrelated ASTER RimWorld process in session 2. Existing GOG
generation and baseline packages are restored, not the corrected candidate.

No further automatic matrix replay is needed to repeat these results. The shortest
remaining owner check is an ordinary menu load on the exact final packages with
background gameplay and PauseOnLoad off, switching away during loading, then
returning to confirm usable scene, retired loading display and gameplay resumption.
The exact deploy/prepare commands, copied save identity and four owner steps are
prepared in `artifacts/ecosystem-next-20260910/c14-retained-20260919/owner-inspection.md`.
Nothing is left launched or staged awaiting the owner. The steward schedules that
manual check and records normal exit/restoration; no new general test grant is
needed. Broader unexercised native branches and real supplier coexistence remain
explicit scope limits, not inferred from this small selection. C08 visual checks,
performance, Steam/Deck qualification and release approval remain separate.

The team is stopped and returns checkout/fixture ownership to the current parent.
No parent state/ledger/plan was edited, no successor dispatched, and no performance,
normal-data change, desktop automation, merge, push or publication occurred. The
remaining-acceptance lists below describe earlier handoffs and are superseded only
by the explicit new entry, hold and pawn-traversal results above.

## Independent review of resumed corrections — 13 September UTC

The steward reviewed stopped `d30cb693d116095777f693d9cfec780efbe441af` and
tested product/observer `c0f32f4069b8b193bc865230af380fbeb9e3344e`. **The
bounded corrections and foreground evidence pass review; C14 remains functionally
incomplete.** An independent reviewer checked all 42 old/new native identities,
the original and captured method bodies and the 27 exact framework bindings.
Production hashing/admission remains strict. The portal declaring-type correction
and shared-refusal status preserve guarded ownership and native fallback.

The steward checked all 29 run identities/results, 143 recorded evidence files,
both final DLLs, 39 rollback receipts, seven baseline hashes, ten absences and
the retained repair's hash/size/timestamp. Twenty-five runs pass; four failures
remain failures. Final source changes after `c0f32f40` are report-only. Observer
changes correctly separate foreground success from unfocused qualification and
leave hold/visibility unqualified. Private native fault injection restores portal
metadata and verifies actual cleanup/retry; it is not pawn traversal. Review
details are in ignored `c14/resume-steward-review.json`.

The remaining-acceptance section below remains binding. No main unfocused benefit
or ordinary unsuppressed entry is established by these foreground controls.
The original C14 owner is stopped and retained for completion after coordinated
focus/visual work and any other missing native coverage. Its open status does not
prevent serial measurement of independently reviewed C05 during the current owner
unattended window. No C15 prerequisite acceptance or supplier-removal claim follows.

## Resumed correctness increment — 13 September UTC

Real game operations exposed two defects missed by the earlier source review.
Both are corrected: native map loading now passes its exact supported-code
checks, and an error while creating an inherited native portal clears only that
portal's temporary pointer. The successful operations below establish foreground
functional behavior. They do not close unfocused loading, sustained post-load
pause, visible behavior, or overall C14 acceptance.

Action `wake-up-c14-resume-unattended-20260913-044927` resumed this same owner at
clean `39655f221b89e2907b978481a6750b51c09f8b32`. The preflight at 04:52:45 UTC
matched seven baseline hashes, ten absences, 315 active packages and the retained
repair's bytes, size and timestamp. No RimWorld or build process was present.
The window has no fixed cutoff and ends on owner return or revocation. Incidental
foreground fixture operation was permitted; no focus writes, desktop automation
or claimed owner-assisted inspection followed. The runner checks the steward's
current window before preparation and launch. All actual runs used functional
purpose and normal exit/capture; no performance measurement was performed.

### Corrections and focused verification

The native map admission guard compares the loaded game's methods against exact
expected identities. Its old expected values came from the modern .NET test
host, whose names for framework assemblies differ from Unity Mono's names.
That difference refused the unchanged supported GOG code. Product
`ee1fd074391aea6c6cadc0ce9566f9ff05ed0e14` replaces the 42 expected entries with
identities verified inside this Unity process. Runtime hashing and admission
rules are unchanged. `map-framework-comparison.json` proves that all 42 method
representations agree character-for-character after accounting for only 27
explicit framework type-owner bindings: 26 to `mscorlib`, one to `System.Core`.
Instructions, offsets, local variables, exception regions and other member
identities remain exact. The modern test translates only those documented
bindings; unknown differences still fail. Live settlement generation and renderer
cancellation/retry then passed. This corrects the earlier claim that the modern
map test alone established runtime admission.

Product `c0f32f4069b8b193bc865230af380fbeb9e3344e` fixes inherited portal
recognition. Unity returned the same native method through `AncientHatch` with
`reflectionObjectEqual=False` but `sameModuleAndToken=True`. The product now
requires that the selected method is actually declared by native `MapPortal`,
retaining the native guards and same-pointer cleanup. Overrides and hidden
methods remain refused. A real malformed-definition fault in native portal
generation failed before this change, then passed cleanup and native retry
after it. The same commit also makes map support status explicitly unavailable
when the shared loading guard refuses another patch; it no longer displays a
misleading map-support claim.

Focused checks passed: **67 managed background-loading tests**, **75 fixture
safety tests**, and the **one native map-contract test**. The map test ran after
the identity correction; the later portal/status change passed the managed and
safety suites. The new managed case covers inherited native selection, override
refusal and cleanup without clearing another portal's pointer. Read-only review
by the assigned native-lifetime agent found no blocker in either correction.
This is same-team review; independent steward acceptance remains separate.
Outputs: `r2-test-map-correction.txt`, `r2-test-original-map-correction.txt`,
`r2-test-portal-cleanup.txt`, and the two `r2-build-portal-*.txt` files under
`artifacts/ecosystem-next-20260910/c14/`.

### Captured native behavior

Every resumed pass observed the fixture in the actual Windows foreground during
loading and records `unfocusedLifetimeQualified=false`. The private observer now
admits such runs as foreground functionality, recording actual focus separately;
it never changes focus or treats a minimized startup request as proof of being
unfocused. Foreground unpaused native gameplay may legitimately add a tick.
All receipts retain `postCompletionHoldQualified=false` and
`screenVisibilityQualified=false`.

The exact per-run source, observer, settings, transaction, capture hash and native
receipt are in `resume-runs.json`; raw captures remain under
`.rlo-test-instance/results/c14-r2-*/`. All runs select eight frozen packages plus
the candidate and observer, using muted 1280×720 output. Existing suppliers are
not activated in this small native selection. The copied save remains the
private `c13-final-shown-03` save identified in the historical record below.

| Run suffixes under `c14-r2-` | Outcome and scope |
|---|---|
| `save-paused-01` | Refused by the old actual-foreground precondition before loading; retained failure, not a pass. |
| `save-paused-02`, `save-unpaused-03`, `save-true-04`, `save-to-true-05`, `save-to-false-06` | Passed actual native save loading with false/true/current-changed preferences and paused/unpaused policies on core `eaf9704c`, observer `05b4726c`. Native paused initialization was tick 601 → 602; foreground unpaused behavior is not an unfocused tick guarantee. |
| `autostart-false-07`, `autostart-to-false-08`, `autostart-to-true-09` | Passed native initial autostart-save selection and completion, including both preference-change directions and an unpaused endpoint. The observer did not invoke `LoadGame` for these entry paths. Core `eaf9704c`, observer `05b4726c`. |
| `world-false-10`, `world-to-false-11`, `colony-false-12` | Passed real world-page generation/redraw and new-colony Play entry; colony native paused tick 0 → 1. Core `eaf9704c`, observer `05b4726c`. |
| `settle-false-13`, `render-admission-14` | Failed because the native map identity guard refused; exposed the framework-owner defect. No map-loading pass. |
| `standalone-15` | Passed native synchronous 75-cell standalone generation on `eaf9704c`/`61e96d4c`; supplied native method evidence for the guard correction. This synchronous pass did not establish queued map admission. |
| `settle-corrected-16` | Passed actual native second-settlement generation and pawn transfer after `ee1fd074`; source colony preserved, paused tick 602 unchanged, current false restored. Observer `61e96d4c`. |
| `render-cancel-17` | Passed cancellation of the actual waiting renderer iterator, its native flag cleanup, and native retry; current false restored and paused tick 602 unchanged. Core `ee1fd074`, observer `61e96d4c`. |
| `encounter-18`, `pocket-19` | Passed native non-player settlement attack generation (75 cells) and direct native Odyssey pocket generation (30 cells). Core `ee1fd074`, observer `61e96d4c`. |
| `portal-retry-20` | Failed same-owner pointer cleanup after deliberate malformed native portal metadata; metadata was restored. Exposed the inherited-method defect. |
| `camp-21` | Passed unchanged native `SetupCamp` action, 200-cell native camp size, player faction, source colony preservation and unchanged paused tick 602; current true. Both packages `c0f32f40`. |
| `foreign-save-22` | Passed synthetic foreign-prefix refusal and native save completion. Shared and map status explicitly stopped; this is a controlled compatibility case, not evidence about a named real supplier. Both packages `c0f32f40`. |
| `portal-corrected-23` | Passed the same native malformed-definition error cleanup, real 75-cell portal generation on retry and exact same-map repetition after `c0f32f40`; source map and paused tick preserved, current false restored. Both packages `c0f32f40`. No pawn walking/traversal claim. |
| `portal-native-control-24`, `save-native-control-25` | Passed with `backgroundLoading=false` in the same `c0f32f40` packages. Native portal code retained its pointer immediately after the injected error (`portalErrorOwnerCleared=false`), then successful retry cleared it and repeat returned the same map. Native paused save loaded at tick 601 → 602. These are feature-disabled controls, not candidate-absent measurements. |
| `save-final-26` | Passed final-source native unpaused save with current false. Early completion update tick 601, endpoint tick 602 during actual foreground gameplay; no unfocused hold or no-extra-tick claim. Both packages `c0f32f40`. |
| `world-final-27`, `colony-final-28` | Passed final-source false → true changes during native world-page generation and new-colony entry. New colony used PauseOnLoad=false, native Normal speed, endpoint tick 0. Both packages `c0f32f40`. |
| `render-final-29` | Passed actual world renderer generation with false → true current preference, source colony preserved and paused tick 602 unchanged. Both packages `c0f32f40`. |

The portal error is intentional fault injection: the observer temporarily removes
the actual hatch's portal definition and restores it in `finally`. It does not
invoke a product hook directly. Ordinary captured native scene logs retain the
known `UnityEngine.InputLegacyModule` dependency error; the private frozen-GOG
stack-formatting workaround remains explicit. These runs do not qualify ordinary
unsuppressed entry.

### Restored endpoint and tested packages

All **29 resumed runs** exited normally with exit code zero and were captured:
**25 passed**, and the four failed/refused checks remain recorded as failures.
`resume-restoration.json`, verified **2026-09-13 05:35:50 UTC**, records all
**39 resumed transactions restored** (29 profiles and 10 package deployments),
seven baseline hashes matching, ten required absences, no pending transaction,
315 original active packages, and a freshly absent fixture process. The retained
repair's hash, size and timestamp match exactly. The previous 12-transaction
`restoration.json` is preserved separately. The final deployment transactions
`20260913-012441-8644b92c` (core) and `20260913-012444-15b82f3e` (observer) were
restored after the last completed profile. The fixture is the original GOG
baseline again; it is not left running or deployed as the candidate.

| Tested artifact | Identity |
|---|---|
| Final product and observer source | `c0f32f4069b8b193bc865230af380fbeb9e3344e` |
| GOG core package | `artifacts/fixture-package/c0f32f4069b8b193bc865230af380fbeb9e3344e` |
| Core DLL SHA-256 | `5e5e8dce7fe846dce0bce847fceddb4646ac462f06272f4449c55aed733a0969` |
| Observer package | `artifacts/fixture-menu-observer/c0f32f4069b8b193bc865230af380fbeb9e3344e` |
| Observer DLL SHA-256 | `42c1625d3b86f930f92ea6a2309303c2416bb6cf0ecdad0f1404cf3c6988f833` |

The subsequent handoff commit changes this report only. No parent current-state,
plan, ledger or steward-state file was edited by this resumed C14 owner. The team
is stopped, and the steward receives checkout/fixture ownership for independent
review and further scheduling; no successor was dispatched by C14.

### Remaining acceptance and handoff limits

Actual focus transitions, a sustained untouched hold after loading with background
gameplay disabled, usable visible scenes/loading-display retirement, ordinary
unsuppressed entry, and actual pawn portal traversal remain open. Native portal
generation and repeated map retrieval do not establish the pawn's complete
walking/job transition. Other unexercised arrival subclasses and mod-created
queues are not inferred covered from one native encounter. The synthetic foreign
prefix proves the guarded fallback case only; it is not real supplier coexistence
or removal evidence. Initial native autostart-save and new-colony paths are listed
separately; untested direct-start branches are not inferred accepted.

No foreground functional duration is used as a speedup. The unresolved unfocused
behavior prevents these foreground cases from answering the main loading-benefit
question. Handing the fixture back for other retained-feature measurements is
preferable to an unrelated small-startup benchmark. C14 performance and full
functional acceptance remain pending with the steward. No C10/C11 development,
Steam/Deck promotion, normal-data change, merge, push or publication is included.

The sections below preserve the earlier assignment, stopped handoff and
independent review as historical evidence. Their then-current pending operations
and package identities are superseded only by the explicit resumed results above.

## Original assignment and baseline (historical)

Action `wake-up-c14-background-ae865a17-20260913` began at clean exact
`ae865a178f84525761f77ef011d149499495e974` on
`codex/ecosystem-next-plan-review`, in the single existing checkout. C13's reviewed
product `0a046d6914e254a0d49b01d407c1cc26e6efa597` and stopped handoff
`3af68ac87fb9393a0caf7632f0911e762a0d08bd` are preserved ancestors. The C14 owner
alone edits, builds and operates the fixture; bounded agents research native
code, review read-only, and implement the private observer in one assigned file.
The steward retains independent review and successor ownership.

The fresh preflight matched all seven assigned endpoint hashes and ten expected
absences. GOG generation is `gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
The restored profile has 315 active packages; functional runs explicitly select
the small activation order, not the representative selector's defaults.
Restored packages are older baseline packages, not the C14 source.

The retained `replacements.json` repair is preserved: SHA-256
`6c74ade7594ce8f350c7821ab691bbeaccae622989b4e21b6e3876799a942e2d`,
1,367,187 bytes, mtime_ns `1788549061179635300`, transaction
`20260912-170800-04578930`. The freshly identified normal Steam game is outside
the fixture and is untouched. Raw preflight, research and checks are under
`artifacts/ecosystem-next-20260910/c14/`.

## Native mechanisms and supplier evidence

The research inspected the pinned GOG assembly
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`
with the existing read-only ILSpy CLI. The earlier
[save/scene findings](../archive/ecosystem-replacement-20260910/scene-save-qualification.md),
[ordinary entry controls](../archive/ecosystem-replacement-20260910/ordinary-entry-final-candidate.md)
and [retained scope](../archive/ecosystem-replacement-20260910/retained-features.md)
are historical evidence, not proof for the new package.

| Operation | Verified native mechanism | C14 treatment |
|---|---|---|
| Ordinary save | `GameDataSaveLoader.LoadGame(string)` queues pre-load and Play-scene arrival; `Root_Play.Start` queues save work and fade | Retain shared scene and callback lifetime |
| World page | `Page_CreateWorldParams.CanDoNext` queues world generation and page/redraw callback | End in Entry, without waiting for a Play scene |
| New colony | `PageUtility.InitGameStart` prepares state then enters Play; `Game.InitNewGame` owns native pause callbacks | Preserve native preparation, completion and pause order |
| Direct Play entry | `Root_Play.Start` has native save, autostart and new-game branches followed by fade | Use the shared completion guard with the relevant native contract |
| Initial startup | `Root.Start` sets engine background permission; `Root.Update` sets `prefsApplied` immediately before its first `Prefs.Apply` | Preserve native permission through earlier preference changes and nested releases; hand back at that exact native boundary |
| Settlement/camp | Native `Settle` queues generation then colonist arrival; the `SetupCamp` action queues both | Acquire before the exact native queue call and retain its tails |
| Encounter | Native site/escape-ship arrival and settlement attack queue synchronous map creation | Retain the main-thread action; select the native nonstandard loading window |
| Transporter encounter | Native travelling transporters dispatch a virtual arrival action | Only the verified native attack/site/space implementations are eligible |
| Direct standalone/pocket generation | `GetOrGenerateMapUtility` and `PocketMapUtility` converge on synchronous `MapGenerator.GenerateMap` | Keep synchronous native execution; no extra worker or synthetic loading queue |
| World-map opening | The button selects world view; `WorldRenderer.RegenerateLayersIfDirtyInLongEvent` queues the actual rendering iterator | Own that iterator's loading lifetime, including completion without a scene transition |

Load in Background's implementation was **unavailable** in the original
`artifacts/ecosystem-review-20260909/` research. Its Workshop description states
later loading behavior; it does not establish its internal hooks or prove exact
parity. No current evidence supports blanket incompatibility or uninstall advice.
Unknown mod-created queues and modified unsupported native operations retain
ordinary behavior and remain explicit coverage limits.

## Ownership, completion and cancellation

Each nested operation receives its own permission record (a lease). Failing or
refusing an inner world/map operation releases only its records. The shared
native queue and callback boundaries retain the complete loading chain through
scene arrival and follow-up work. An unrelated busy queue cannot be adopted by
a new save, world or map operation.

The preference adapter keeps temporary permission during owned work and reads
the current preference on final release. The completion-frame guard runs after
native `Root.Update`, before ordinary `Game.UpdatePlay`. It blocks unwanted
unfocused gameplay when background execution is disabled. Native pause-on-load's
intentional initialization tick remains; that tick is not an extra gameplay tick.

Windows can report Unity's `Application.isFocused=true` at a minimized launch
while another process owns the foreground window. The guard now also reads the
actual Windows foreground process, without changing any window. This extra check
runs only during loading/completion with the background preference disabled;
other platforms retain Unity's focus behavior. It does not establish sustained
native pause after the completion frame. That requires a real post-load hold.

Initial loading remains owned by the game. The shared engine writer preserves
its permission through a preference change or release of a nested Wake-Up load.
Only the exact native `Root.prefsApplied` handoff ends that protection; an empty
queue between callbacks does not. Refusing a modified loading contract cannot
release the native startup owner's permission.

Synchronous encounter events normally wait for their standard window to repaint.
For only the verified native map queue calls, C14 selects the game's existing
nonstandard loading-window mode. The original action still runs on the main
thread; C14 neither fabricates a repaint nor moves unknown code to a worker.
This presentation change requires the carried visible inspection in addition
to real minimized progress checks.

Native world rendering clears its busy flag only at normal enumeration end.
Native queue clearing drops waiting iterators without disposing them. C14 tracks
only its own queued renderer iterator, disposes it when that exact queued work
is canceled, and clears its flag only while the native contract and ownership
still apply. It leaves a running native event intact. An unadmitted later owner
prevents the old wrapper from clearing that renderer's flag.

Native portal generation also lacks error cleanup for
`PocketMapUtility.currentlyGeneratingPortal`. C14's guarded base-implementation
finalizer clears only the same portal after failure; it cannot clear a different
portal's ownership. Changed/overridden implementations retain native fallback.

C13's observation no longer declares a queue gap complete while an owned load
still awaits its Play scene. Its activity remains native loading activity, with
no deferred-texture/audio readiness dependency.

## Validation record

**Stopped, awaiting steward review and coordinated live qualification. C14 is
not functionally complete or accepted.** The candidate implementation is present,
but later-map/world/autostart coverage has not passed real game checks. No
performance, platform, supplier-removal or release-readiness claim is made.

The product source is `eaf9704c24f0330e65a3b942b2c7909e3b6d2d60`, following
implementation `9de4a6bec408f6be0a81737fc50ef4b6f5faba27`, observer focus receipts
`eb9e47f4873269d5e82be68758ec43f9c40bc5c6`, and foreground correction
`6fe5d563c5d92e64862cbda44075344b461213e9`. The last commit changes only native
initial-permission handoff and its tests. The two successful save runs exercised
`6fe5d563`, not the later handoff change.

| Identity | Exact value |
|---|---|
| Final GOG package | `artifacts/fixture-package/eaf9704c24f0330e65a3b942b2c7909e3b6d2d60` |
| Final core DLL SHA-256 | `cfc9f183de1062ef5bb45d66f9a074559da6fe472ea2425b2ae1e529636bfd76` |
| Successful-save core DLL SHA-256 | `bce016a636b063c65aee3965409e8fe6c190b9052680d3dea234412f354aab22` |
| Final observer source/package | `6fe5d563c5d92e64862cbda44075344b461213e9`, under `artifacts/fixture-menu-observer/` |
| Final observer DLL SHA-256 | `f2faca638a7d238e1f3e3398b3c5b2838ad8bc3a2802a98f7e9cf00259439e9f` |
| Final core/observer deployment transactions, both restored | `20260912-232857-ffe99ac4` / `20260912-232900-3430b361` |

Each run used GOG generation above, `--purpose functional`, ordinary user
activation, muted 1280×720 windowed output, eight selected frozen packages and
ten active packages including candidate and observer. The copied fixture save
came from `c13-final-shown-03`, SHA-256
`2f6161b303ee6e2636a46dbe9586d330e39a979714cdcbe5c499980c183ab7a8`.
It was not a normal user's save. All five launched processes exited normally
with exit code zero and were captured; each profile transaction was restored.

| Captured run | Actual result |
|---|---|
| `c14-save-false-01` | Refused before load because the original observer required Unity's focused flag to be false. No loading pass. |
| `c14-save-focus-02` | Refused before load; new receipts showed minimized window, other foreground process, and Unity's stale true flag throughout observation. No loading pass. |
| `c14-save-false-03` | **Passed:** actual unfocused save load, current background=false restored, 75-cell colony loaded, native paused speed, save tick 601 → 602 (the intentional PauseOnLoad tick). `automaticTestPassed=true`. |
| `c14-save-unpaused-04` | **Passed:** actual unfocused save load, background=false and native Normal speed restored; tick remained 601 at completion, with no extra gameplay tick. `automaticTestPassed=true`. |
| `c14-settle-false-05` | Refused before save setup or map entry. Windows foreground PID equalled fixture PID 43916 for the entire three-second precondition, despite `IsIconic=true` and `minimized-noactivate`. `automaticTestPassed=false`. No settlement coverage. |

The last result differs from the previous two successful unfocused runs. The
cause of foreground ownership was not established; C14 performed no window or
focus writes. Further launches stopped because a nonactivating startup request
did not provide reliable evidence that owner focus would remain undisturbed.
The planned world-render and autostart runs did not start. This is an operating
limit, not a successful test or evidence that settlement generation is broken.

Final focused checks passed: **66 managed background-loading tests**, **75
fixture-safety tests**, and **one complete native map-contract test** using the
existing modern .NET test host. Earlier whole Python runs had varying temporary
Windows rename failures; the final two whole runs passed without a harness or
security change. Commands/outputs are in `test-native-startup.txt` and
`test-modern-native-startup.txt` under the C14 evidence directory. The native
map-contract check proves matching inspected method bodies, not live map behavior.

The private observer records actual `Application.isFocused`, Windows foreground
owner, minimized state, engine/background preference, loading frames, scene,
native map/world results and completion ticks.
It invokes real native entry APIs and does not patch those methods or replace
the focus property. Separate processes allow false-preference endpoints to be
captured in their final frame and exit normally without resuming gameplay.

Automated scene checks explicitly identify the previously accepted frozen-GOG
stack-formatting workaround when selected. Error messages remain; the workaround
is private, not shipped. This is not a production engine repair or a substitute
for ordinary unsuppressed entry and screen/focus-transition inspection.

### Required continuation and owner inspection

The smallest remaining work first needs coordinated permission for possible
foreground/visible interaction, because the latest actual launch did not stay
outside Windows foreground ownership. Do not retry launches on the assumption
that the startup flag guarantees this. The steward owns permission, independent
review and continuation; this record grants none.

After ownership is assigned and current process identity verified, deploy the
exact final core and observer packages above with `scripts/fixture.py`, retaining
their new transaction IDs. The ignored `c14/run-phase.py` script prepares,
verifies, launches, captures and restores one functional profile. It stops on
failure and never rolls back an incomplete live run. It is a convenience runner,
not product or independent review evidence. Prioritize these real operations:

1. Native startup/autostart with both preferences and changed current preference;
   repeat save false/unpaused against final `eaf9704c`, then true and changed
   preference endpoints. Initial startup's final handoff change has offline
   evidence only.
2. Native world page and new colony, including native page/redraw callbacks,
   both current preferences and pause-on-load policies.
3. Actual `settle`, `encounter`, `standalone`, `pocket`, `world-render`, and
   `world-render-cancel` phases. The last phase clears the real queued renderer
   and retries it. No one of these is currently a live pass. The prepared
   observer cases themselves may need correction when first exercised.
4. Real portal traversal/error cleanup and relevant chained/repeated operations.
   The observer currently covers direct native pocket generation, not a real
   portal traversal. Offline nested/failure tests do not close this gap.
5. Background-loading-off native controls, compatibility/fallback cases, and
   the separately coordinated visible and sustained-hold inspection below.

The ordinary owner-inspection preparation command, after package deployment, is:

```powershell
python scripts/fixture.py prepare --mode candidate --strategy startup-searches --lane activation --label c14-owner-inspection --purpose functional --activation user --user-settings artifacts/ecosystem-next-20260910/c14/settings.json --loading-ui-layout 1280x720@1 --gameplay-save-from c13-final-shown-03 --menu-observer --no-exit-after-menu-ready
python scripts/fixture.py verify-prepared --label c14-owner-inspection
python scripts/fixture.py launch --label c14-owner-inspection --authorize-live-launch --window-style noactivate
```

The owner explicitly chooses when to bring the fixture forward. In this manual
mode the observer records the menu only, starts no C14 probe, and leaves native
logging unchanged. Load the copied save, check ordinary entry and the loading
display, then inspect a queued synchronous map transition and world-map redraw.
Confirm that loading can finish after manually switching away/minimizing and
that returning shows a completed, usable scene without stale loading UI. With
background gameplay disabled and PauseOnLoad disabled, include a real untouched
post-load hold before returning; ensure gameplay did not run during that hold.
Also check native PauseOnLoad enabled and background gameplay enabled behavior.
Record actual focus transitions, preference/speed, visible result and limits;
the current automated probes exit immediately and cannot prove this hold.
Quit to OS normally, capture, then restore the exact manual profile and package
transactions. No manual result is recorded as `automaticTestPassed`.

### Restoration and stopped ownership

`artifacts/ecosystem-next-20260910/c14/restoration.json`, verified
2026-09-13 03:32:40 UTC, records all **12 C14 transactions restored**, all seven
baseline hashes matching, all ten required absences, no pending transaction and
315 original active packages. The retained repair matches its original hash,
size and timestamp exactly. The game/core/observer endpoint is the pre-assignment
GOG baseline, not the candidate packages listed above. A fresh executable-path
check found no fixture process; normal game/data were untouched.

The C14 team is stopped. No successor was dispatched, no parent ledger or
steward state was changed, and no merge, push, publication, performance run,
Steam promotion, Deck access or desktop automation was performed. Independent
review and the unresolved functional evidence remain with the steward.

## Independent steward review — 13 September UTC

**Scoped source review passes; C14 functional acceptance remains open.** The
reviewed handoff is `6dab1265e37171eb1d0dbe7c952c250c38b3ac81`, with final
product `eaf9704c24f0330e65a3b942b2c7909e3b6d2d60`. Two independent reviewers
examined lifetime/preference handling and native map/renderer ownership against
the inspected GOG source. Neither found a concrete blocking source defect.
This does not qualify untested native operations or the changed loading display.

The steward independently checked five captured run identities/results, 29
captured evidence files against their recorded hashes/sizes, three package DLLs,
12 rollback receipts, seven baseline hashes, ten required absences and the retained
repair. Successful live save evidence belongs to `6fe5d563`; no source changes
after `eaf9704c` alter product or observer files. The final startup handoff remains
offline-qualified only. Review details are preserved in ignored
`artifacts/ecosystem-next-20260910/c14/steward-review.json`.

The completion guard ends after the completion frame. An immediate normal exit
at the expected saved tick cannot demonstrate sustained post-load gameplay pause
or correct behavior across real focus transitions. Those checks, native operation
coverage and ordinary unsuppressed entry remain required, as listed above. The
exact-caller map checks and native iterator disposal support the implementation
design; they do not replace actual encounter, cancellation/retry or portal runs.

Continue through the same orchestrator after coordinating the possible foreground
interruption demonstrated by `c14-settle-false-05`. Use functional runs only;
prioritize the final-source save/startup checks, then the missing native operations
and controls. Coordinate owner-assisted visible/focus and sustained-hold inspection
without UI automation. Do not add unrelated harness work or reduce acceptance.
The steward retains ownership pending that coordination; no successor is dispatched.
