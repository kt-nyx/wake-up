# S13 — Background save loading

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

The new **Allow save loading while unfocused** option temporarily lets a supported
save load continue after the player switches away from RimWorld. It restores the
player's current background preference afterward, without changing the saved
preference or the game's pause state. It defaults off and takes effect after a
restart. The steward accepted the save-loading implementation and offline checks; this is
not a live focus, minimization, gameplay or speed qualification.

## Scope and source

Dispatch `wake-up-s13-background-20260909-c8ad49c` assigned the existing checkout
from exact reviewed base `c8ad49c1aca00868b49efe8d6b937b75b6da3ff3`. HEAD matched,
the checkout was clean and the only running RimWorld process was PID 35536 at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe`, outside the
fixture. That normal game was left alone. The task used the single checkout on
`codex/s13-background-loading`; no other checkout, worktree or sub-agent was used.

Implementation source: **`5c3f4606154f57629cac39f88ed6bcdf48844072`**. It retains the
assigned S11 routing, S10 preparation and S08 display ancestry. S11 on-demand and
S12 remain deferred at their existing prerequisites; no scheduler was added.
Earlier XML, language reuse, first-build and broader lossy-preparation gaps remain
undelivered. The owner-deferred Linux helper and existing core Linux fallback
are unchanged. Prepatcher remains required; no dependency was added.

| Operation or interface | This checkpoint |
|---|---|
| Ordinary save loads through `GameDataSaveLoader.LoadGame(string)` | Eligible on the main thread after play data is loaded, with an idle native queue or an already owned load chain; includes the `FileInfo` overload that delegates to it |
| Scene transition, save worker, completion callbacks and queued follow-ups | Retained as one temporary permission lifetime, including nested save requests and error recovery |
| A different operation already occupying the queue | Ordinary behavior; the feature does not adopt unrelated work |
| Startup autostart directly in `Root_Play.Start`, direct `SavedGameLoaderNow` calls | No independent admission; only covered if reached within an already admitted ordinary save chain |
| World creation or standalone map generation | Not admitted. They require their own queue-entry and completion qualification before extension |
| Setting | `WakeUpSettings.BackgroundLoading`, persisted as `backgroundLoading`, default `false`; next ordinary launch |
| Development selector | Exactly one `--wake-up-background-loading=on` in candidate mode; explicit controls, master-disable and bypass keep existing precedence; fixture presets unchanged |
| Internal interfaces | `BackgroundLoadingSession` owns temporary permission and completion-frame state; `BackgroundLoadingRuntime` adapts native hooks; `BackgroundLoadingContract` checks required method behavior |

## Why these boundaries matter

The inspected game already permits background execution during initial content
startup. Ordinary save loading has a separate scene transition and main-thread
completion work, so extending startup's existing override would be too broad.
Fresh ILSpy reads used the frozen GOG 1.6.4871 rev573 assembly, SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
[Native reading aids and fingerprints](../../../artifacts/s13/research) remain
private and ignored; no native code was copied into the product.

1. `GameDataSaveLoader.LoadGame(string)` first queues an asynchronous pre-load
   action targeting the Play scene, then disposes the previous game. The prefix
   acquires permission before that verified queue call; its finalizer releases
   on an escaping exception. The action clears old world/maps and records the
   requested save in `GameInitData`.
2. `Root_Play.Start` queues the actual `SavedGameLoaderNow` worker and a final
   synchronous screen fade. The session explicitly waits for play-root arrival
   as well as queue completion, so an empty scene-transition gap cannot restore
   the preference early. Returning to the menu cancels that arrival wait.
3. Native asynchronous completion runs deferred actions and its callback before
   clearing the current event. Synchronous completion clears the current event
   first. The adapter counts active update/callback boundaries and checks the
   queue only after those boundaries return. A callback can therefore append
   another callback or queue another event without prematurely ending ownership.
4. `ClearQueuedEvents` clears only waiting work, not the running worker. It ends
   an idle canceled chain, but retains permission through an active worker or
   callback and its recovery/follow-up work. Native worker errors still use
   native handlers. Escaping update/callback errors release permission without
   swallowing the error or trying to repair the game's queue.
5. `PrefsData.Apply` normally writes `Application.runInBackground`, even for an
   unrelated options change. The adapter replaces only that write: while owned,
   the value stays true. On release it reads the current `Prefs.RunInBackground`
   value rather than restoring a stale snapshot. It never writes that preference,
   calls preference saving or changes `TickManager.CurTimeSpeed`.
6. `Root_Play.Update` calls `Game.UpdatePlay` after `base.Update` finishes loading
   in the same frame. A small instruction rewrite (transpiler) inserts a check
   immediately after `base.Update`, before the remaining play work. During an
   owned load and its completion frame, unfocused play is skipped when the player
   disallows background execution. Focused play and an enabled background
   preference retain ordinary behavior. The frame marker expires automatically;
   it does not impose a new permanent pause policy.

Native pause-on-load deliberately schedules one initialization tick before
pausing. That callback, saved time speed and ordinary forced pause during long
events are preserved. The guard prevents an additional regular gameplay update;
it does not delete the native initialization tick. Synchronous events still wait
for their window's native display acknowledgement. No global display-wait bypass
or GUI patch was added.

The contract checks 39 required native methods, including compiler-generated
load, scene and pause callbacks. Changed method behavior or unknown Harmony
interceptions cause ordinary fallback. Published patch-state checks run before
admission and throughout an owned chain; a later incompatible patch releases
permission and disables new admission for the launch. The exact existing S08
`AfterUpdate` finalizer is allowed to coexist. Broad loading replacements such
as an unreviewed LongEventHandler rewrite are not silently accepted. These are
feature-local checks, not a whole-game version restriction.

World creation was inspected: `Page_CreateWorldParams.CanDoNext` queues an async
generator and a callback that changes windows and redraws the world. Its shared
queue mechanism alone does not qualify that separate admission and completion
path. New-game map generation has its own branch in `Root_Play.Start`. This
checkpoint completes the requested save-first implementation and leaves those
extensions explicit rather than enabling all long events by text key.

## Offline checks and their limits

The final command was:

```powershell
py -3 scripts/fixture.py test --test-filter 'FullyQualifiedName~BackgroundLoading|FullyQualifiedName~UserStartupSelection|FullyQualifiedName~LoadingDisplay'
```

**53 managed cases and 37 Python cases passed**, with a zero-warning/error Release
build. [Final log](../../../artifacts/s13/offline-checks.log) and
[TRX results](../../../artifacts/fixture-tests/326e2fa1547a4db4ae72f108bf9e6063/features.trx)
record the result. The fixture receipt says `gameLaunched: false`; launcher text
in the Python output belongs to mocked unit tests, not a real launch.

Checks cover default-off/explicit selection and native setting persistence;
native queue admission before action execution; ineligible/busy queues; actual
synchronous display waiting; nested saves, deferred callbacks and queued tails;
idle cancellation and clear-inside-callback replacement; changed preferences;
worker and callback exceptions; repeated loads; early/late foreign hooks; S08
observer coexistence; native method fingerprints; and the completion-frame
decision for focused/unfocused and enabled/disabled background preferences.

The .NET Framework test host cannot compile some Unity internal calls (`ECall`).
Tests retain the native load entry, queue, callback drain and synchronous update.
They execute an instruction-for-instruction copy of native asynchronous update
with only Unity scene calls replaced by rejecting test stubs; game-data actions
are controlled delegates, and scene arrival is simulated. The native worker,
exception handling, callback order and event fields still execute. Tests also
inspect both complete native rewrites and execute the emitted completion guard
with observed substitutes for base update and gameplay. Full engine preference
and play-update hooks are installed only in production; the internal offline
installer seam skips those two engine-dependent bodies. No new test host,
package, security setting or runtime dependency was introduced.

These checks establish the managed lifetime and guarded instruction placement.
They do **not** establish actual Unity/Mono hook installation, OS focus behavior,
minimized progress, scene arrival timing, paused/unpaused save fidelity or gameplay
compatibility. Qualify those through a separately authorized fixture save-load
session, including focus loss before the worker, during loading and at completion;
both background-preference values; pause-on-load and saved pause states; and a
failed/canceled/repeated load. Windows and Linux need their own platform evidence.
No startup-time or loading-speed improvement is claimed.

## Package and return of ownership

`py -3 scripts/fixture.py build` passed with committed source and the frozen
game/Harmony/Prepatcher API references, with zero warnings/errors. The resulting
undeployed core package is
`artifacts/fixture-package/5c3f4606154f57629cac39f88ed6bcdf48844072/`.
Its [adjacent receipt](../../../artifacts/fixture-package/5c3f4606154f57629cac39f88ed6bcdf48844072.json)
records source and reference identities; the [build log](../../../artifacts/s13/package-build.log)
records the verification.

| Output | SHA-256 |
|---|---|
| `Assemblies/WakeUp.dll`, 241,664 bytes | `8763e304c65f292370f8d13f4a70557e3e8b9330fb97400ecff4d44d01dc4b71` |
| `About/About.xml` | `f6e07f9178757aab64b287a1940cf672db8c6416c804f56d4df541b5d8492cee` |
| Adjacent package receipt | `8c4999e4016b091fe3d2b1dba5c94486b18c449d340eb6327ad60c5f53460725` |

The package contains only those two core files; this documentation closeout does
not change it. No deployment, game launch, normal Steam/Workshop/profile/save/cache
change, Deck access, UI automation, security/system-tool change, push or publication
occurred. `steward-state.json` was not edited. The plan ledger is awaiting review;
the task has not accepted its own work or started S14.

The S13 task stops all writes/builds and explicitly returns checkout/build
ownership to steward task `01a08711-7899-7ff3-a1b5-4a0b105ba661` at handoff.

## Parent-steward acceptance — 9 September 2026

Accepted save-loading source `5c3f4606154f57629cac39f88ed6bcdf48844072` and handoff `a34019372d4e51b90af45ee6f674c1469e2f84dd` for implementation and focused offline checks. Reviewed actual session ownership, queue/scene/callback boundaries, preference-write replacement, completion-frame guard, guarded native bodies and test seams against the cited native load/root/queue code. The current preference is restored without writing saved preference or time speed; native pause-on-load initialization is retained. Unknown or later incompatible hooks decline/release this feature.

The recorded 53 managed and 37 Python passes, clean source-specific build and independently matching packaged DLL hash are sufficient for this scoped checkpoint; no extra test/build was repeated. The evidence does not execute actual Unity focus/minimization, production engine-hook installation or a live saved-game transition. Those claims remain unqualified and unauthorized. World creation and standalone map generation are explicitly not admitted and remain undelivered extensions of goal 9; no final omission is approved.

The clean checkout and all writes/builds returned to the steward. Proceed to a fresh S14 task for warnings based on actual delivered capability and verified conflicts, carrying this source and the ensuing review commit. Keep all prior XML/first-build/broad lossy/on-demand/progressive-detail gaps and the owner-deferred Linux helper visible in the final coverage review. No deployment or publication is authorized.
