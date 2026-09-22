# Scene-entry repair and background save qualification

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent steward acceptance

Accepted on 10 September through clean handoff `0df0c5e565d43b69d900e1021e489e86a2b71ecc`, with unchanged core `79dde43` and private observer `c6df2b5`. The steward reviewed the fixture/observer changes, bounded native-loop evidence, copied-save checks, actual minimized hold receipts, final gameplay/exit result and deployed observer hash. Final `ssq-hold-10` passed with run UUID `e3b31317bf904e2593df9dc0ffad2f41`; paused and Normal-speed new maps retained tick 1203 across the recorded minimized holds. The old-map key interaction is disclosed below and does not establish extra new-map ticks.

This accepts the stated frozen-fixture workaround and bounded save/background behavior. It is not a production engine fix, proof that every input error is harmless, or Steam qualification. No unchanged retry or additional diagnostic framework is required. Continue to remaining feature work/disposition, then the owner-required Steam and reliable three-version comparisons. All teams returned ownership and no fixture process remained at review.

The isolated game now reaches its colony and completes real minimized save loads.
The scene-entry stall was a busy loop in the frozen Unity player's exception
stack-trace formatter. A private fixture logging workaround avoids that path;
the shipped Wake-Up core is unchanged. Native pause/tick behavior, restoration
of the actual current preference and normal exit passed. This is a handoff for
steward review, not release acceptance. A stack trace is the list of program
calls leading to an error; the failure was in formatting that diagnostic text.

## Scope and identities

Dispatch `wake-up-overnight-scene-save-01da6b0` started from clean
`01da6b0f133ff9f878c8a50b8c64b4ad724ad289` in the sole checkout, on
`codex/scene-save-qualification-01da6b0`. Fresh inspection found no RimWorld
process. Two bounded researchers compared launches, inspected native semantics
and edited assigned fixture controls. Only the owner built/deployed/launched,
with one fixture process at a time. Prepatcher remained required and active.

| Layer | Identity |
|---|---|
| Unchanged accepted core source/package | `79dde439051e56dab5fb910958fafd689fd6750d` |
| Deployed core DLL SHA-256 | `dca48f2dadd4f0c503aa66f9c09887576ecec984d2e7b8cbec16dbc1e4e4e87a` |
| Final private observer source/package | `c6df2b5ad07a10136cf22726038cbbaa11448613` |
| Observer DLL SHA-256, 41,472 bytes | `18d3e23cc329d88e0385e84c9d4f75ad7d389c63cd23965fc0112cba5285e82c` |
| Observer deployment transaction | `20260910-005528-305599c0` |
| Frozen generation | `20260904-182829-e058641c` |
| Fixture manifest SHA-256 | `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d` |
| Managed game SHA-256 | `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` |
| UnityPlayer DLL SHA-256 | `e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96` |
| Mono runtime DLL SHA-256 | `d2f4348a5aa80bbdd0e73582cc2a00b3a17fe4a497da7052436e780cdee2a0fa` |

The core was neither edited nor rebuilt. Absent controls physically parked it.
The frozen player reports Unity `2022.3.35f1` and RimWorld `1.6.4871 rev574`;
the build reference remains the recorded `gog-rev573` managed contract above.
The [run summary](../../../artifacts/scene-save-qualification/run-summary.json)
separates package, observer, process, capture and copied-save identities.

## Diagnosis and fixture correction

Successful `wfq-absent-smoke-17` and failed `oq-old-observer-05` had identical
prepared profile hashes/content order and matching game, engine, driver and
manifest. Old/new observers both stalled before the false-preference probe.
The window showed **Loading assets 90%**; focusing it did not release the wait.
Root-component observation coincided with two passes, but narrower/repeated
lookups stalled. Ordinary copied-save loading also stalled. Explicit input
dependency preloading succeeded but did not fix it. Neither route was retained.

The decisive observation read the busy thread of verified fixture PID 11724 in
`ssq-save-entry-07`: three brief instruction/register reads, then three reads
of the implicated string, with thread resumption in `finally`. There was no
debug privilege, injection, security change, trace session or symbols download.

The [native finding and disassembly](../../../artifacts/scene-save-qualification/prepatcher-research/native-format-finding.md)
show Unity searching an empty stack-trace string for `(at `. Search returns
`-1`, but `UnityPlayer+0x77a16b` examines byte `-1 + 4`. The string starts with
NUL, yet stale byte 3 is `0x3c` (`<`). The code repeats the failed search and
jumps back unconditionally. This proves the captured busy loop, rather than a
worker/focus wait. Memory-layout sensitivity follows from this stale-byte test;
the stale bytes' origin and complete engine error path were not reconstructed.

The private gameplay observer now sets supported Unity Exception/Error stack
policies to `None` before scene entry. Error **messages remain**: the successful
ordinary-save control still logs the InputLegacy exception at all three scene
transitions. Only stack formatting changes, for this explicit gameplay probe's
process. Startup timing arms and the product package receive no workaround.
Its exact native branch is not proved; three final runs qualify practical use
in the frozen fixture. No engine/Prepatcher binary changed. This does not prove
all input errors harmless or justify disabling public diagnostics.

## Captures and real save behavior

All ten captures used the small activation lane and functional purpose; timings
are diagnostic only. Each forced stop preserved logs and fresh executable/CPU
identity before closure and launcher capture. None counts as a pass.

| Run | Result |
|---|---|
| `ssq-scene-01` | Stalled; diagnostic initially sat behind reload gating and was corrected |
| `ssq-scene-02` | Absent map/tick/save/reload pass with recurring root observation |
| `ssq-background-03` | Enabled ordinary setting passed false preference and focused reloads |
| `ssq-no-root-04` | Removing component discovery restored the stall |
| `ssq-root-once-05` | One-time lookup stalled |
| `ssq-root-full-06` | Full early lookup stalled; not retained as fix |
| `ssq-save-entry-07` | Copied-save/input-preload stall; native loop captured |
| `ssq-no-trace-08` | Logging workaround: ordinary copied-save entry and both reloads passed, exit 0 |
| `ssq-new-map-09` | Same workaround: 75×75 map, ticks, save/reloads passed, exit 0 |
| `ssq-hold-10` | Minimized loads, two holds and preference-change load passed, exit 0 |

Final `ssq-hold-10`, observer UUID `e3b31317bf904e2593df9dc0ffad2f41`, has
`automaticTestPassed=true` and `gameplayTestPassed=true`. Ordinary user activation
overrides only `backgroundLoading=true`; other optional features retain defaults.
Source save `ssq-scene-02` is 1,878,696 bytes, SHA-256
`b5b8bd63b207c48f0b19c62d14661e53602469dca6923e6986d8caa6d13c8ef9`.

| Final case | Observed result |
|---|---|
| PauseOnLoad enabled | Saved tick 1202; completed at 1203 and Paused, exactly the native initialization tick |
| Paused minimized hold | Tick 1203 and Paused unchanged for 32.872 seconds through first focus return |
| PauseOnLoad disabled | Saved/completed tick 1203, native Normal speed, zero regular ticks at unfocused completion |
| Unpaused minimized hold | Tick 1203 and Normal unchanged for 34.834 seconds through first focus return |
| Current preference change | False at admission, changed to true while active; completion inactive with engine/preference true, Paused and tick 1203 |

Both minimized loads retained actual preference false, engine permission true
while loading and false at completion. Map/three colonist identities matched.
Paused state also stayed stable for three later frames. Native time speed is
not serialized: the unpaused check expects native Normal, not a saved speed.

`LateUpdate` samples completion before changing the NEW map's speed/preference.
Early observer execution and the focus callback sample before resumed gameplay;
no Harmony hooks were added to the product's guarded lifecycle. Computer Use
targeted only the verified fixture. Alt+Enter exposed its title bar; actual
minimize clicks and native `IsIconic` confirmed minimization. Neither restore
occurred before its hold elapsed. An initial Alt+Space attempt toggled the OLD
map while focused; it was paused again before minimization. That changed neither
the saved file nor the newly loaded map under test. Receipts and window-action
transcript establish this sequence, not every focus timing or long-term colony.

## Controls, checks and follow-up

`--gameplay-save-from <label>` copies only `RloFixtureSmoke.rws` from a successful
captured automatic gameplay run with matching frozen content. Capture/receipt/
save hashes are checked before copy and again before launch; arbitrary save
paths are refused. `--background-hold` requires functional smoke, user activation
and `backgroundLoading=true`. It selects two external minimize/restore holds
and the third preference-change load. Metadata/argument reconstruction pin both
controls. Existing locks, recovery, profile rollback and capture remain in use.

All **45 Python tests passed**, including focused invalid-control/source-drift
checks. Final observer build: zero warnings/errors. Final metadata audit passed
with unchanged exclusions and deployed core. The accepted core and overnight
preparation/maintenance suite were not rerun. Raw evidence/decompiled research
is ignored under `artifacts/scene-save-qualification/`; saves remain private.

No world/new-game background extension was added. Concrete next assignment:
independent admission at `Page_CreateWorldParams.CanDoNext`'s verified queue
call, with separate compatibility checks and a session that does **not** wait
for Play. Retain ownership through deferred page-change/redraw callbacks and
queued tails; qualify errors/cancellation and a real native page run. Preserve
save loading's scene wait. New-map background entry needs its page-to-scene
caller qualified separately. These new admission surfaces remain bounded
follow-ups, not delivered features or approved permanent omissions. Other gaps
are unchanged.

Owner instruction `wake-up-owner-steam-comparisons-20260910` authorizes serial
isolated Steam qualification after stable fixture tests. The steward assigns
new/current, pre-revamp and absent-baseline comparisons after scope review.
Pre-revamp is public 0.2.1 source `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f`,
expected DLL `47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d`;
verify before use. Keep one fixture, exact Steam binaries, matching content and
explicit settings, multiple balanced forward/reverse repeats, controlled app
caches and a stated Windows file-cache policy. Report count, average, median,
spread and percentages versus both arms; separate first-build/warm and menu/
colony entry. This phase did no Steam staging or performance measurement and
does not complete that request.

All team writes/builds/launches stop at handoff; ownership returns to steward
`01a08711-7899-7ff3-a1b5-4a0b105ba661`. Deployment/profile and failures are retained,
with no fixture process alive. No normal game/data, Deck, Linux-helper, security,
push/publication, new-slice or self-acceptance changes occurred.
