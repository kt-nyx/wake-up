# C15A agent-observed UI check — 21 September 2026

The quality controls now remain reachable in the settings window. Three ordinary
GOG launches then showed the same copied colony with native textures, prepared
half-dimension textures, and native textures again after reset. The actual saved
preparation and reset stores carried between launches. This is agent-observed
functional evidence, not owner inspection, performance qualification or release
acceptance. The responsible task returns stopped ownership for independent review.

## Scope and concrete fix

Assignment `wake-up-c15a-overnight-ui-20260921-fd83f8ff` began at clean
`fd83f8ff1a0c4cf823b166e86717b9ed8f9f5b2a`. The owner's 07:06 UTC overnight grant
authorized fixture UI interaction with the Windows computer-use skill. No other
application or normal game data was operated. This increment made no performance
measurement and did not start the excluded PNG pilot or any deferred/retired route.

The first launch, `c15a-ui-native-20260921-01`, exposed a real settings defect:
only the heading and first checkbox remained accessible. RimWorld's native listing
wraps overflowing rows into more columns by default. Its reported height then
describes the last column, so the enclosing scroll area shrank and hid the controls.
`WakeUpMod.DoSettingsWindowContents` now creates its listing with
`maxOneColumn = true`, keeping every row in the vertical scroll area. This is the
only runtime-source change. The original failed UI observation remains preserved.

Commit `908444675b1ddd33b818381fb67498eeb5faafc1` built with zero warnings/errors.
Core DLL SHA-256:
`3f96a341d7c2a777fb26b1c724e53d7e711ec46edf58f17a016bd6555de5d3e1`.
A read-only reviewer confirmed the native wrapping cause and bounded fix; live
inspection then verified scrolling from the first checkbox through the last cache
controls. Preparation/quality windows and their final long source rows were readable.

## Three-launch native, converted and reset cycle

All stages used the same ten-package C08 selection, retained-feature settings,
`c15a-owner-scene-setup-09` copied save and six-path brazier/antenna filter documented
in the [inspection guide](c15a-owner-inspection.md). Floor and icon textures remained
native comparison objects. The observer stayed at `0412c65d`; qualified helper
SHA-256 stayed `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.
No automatic gameplay/image probe or private logging workaround was enabled.

| Stage | Captured run | Observed result |
| --- | --- | --- |
| Native | `c15a-ui-native-20260921-02` | Normal menu loading, visible loading screen, usable colony after focus return, native brazier and all antenna directions. Fixed settings controls reachable. At idle main menu, half-dimension preparation completed for six sources with zero failures. |
| Converted | `c15a-ui-prepared-20260921-03` | Carried the native run's actual prepared store. Runtime applied six textures in two coherent groups, refused zero. Saved half-dimension choices persisted. Brazier color/mask and four antenna directions rendered coherently; no obvious missing texture, broken transparency or material mismatch. At idle main menu, reset completed and showed native choices. |
| Reset | `c15a-ui-reset-20260921-04` | Carried the converted run's actual reset store. Runtime applied zero quality overrides. Native appearance returned; read-only quality scanning showed saved native choices through the final row. |

Each run used normal menus and Quit to OS, exited with code 0 and captured its log,
profile and independent menu-ready observation. The first defect run also exited
normally. These manually operated functional runs are not automatic-test passes;
their diagnostic elapsed times do not establish a speedup. Captured store/run hashes
are in `run-receipts.json` and `abc-observations.json` under the evidence directory.

The visible comparison used matching close camera scale, but lighting and pawn
positions changed. It establishes bounded visual coherence, not pixel equality or
the owner's preference for reduced texture quality. The converted scene's native
pause button also produced a stationary scene. Cancellation and unsupported-group
refusal retain the prior automated evidence; this check did not repeat those cases.

## Loading and background behavior

Native Options showed Run in background and Pause on load disabled. During ordinary
save loading the fixture was put in the background, then reactivated after a short
wait. A usable colony appeared and ordinary interaction worked. Displayed preferences
remained intact. This closes the visible loading/focus-return observation for this
workload. It does not directly count ticks while unfocused: the prior corrected C14
five-second hold/portal evidence retains that separate claim and its original core.
No loading or gameplay code changed in this increment. Known InputLegacyModule log
messages remain present; they did not prevent these ordinary UI operations.

## Evidence, restoration and remaining decisions

Raw evidence is under
`artifacts/ecosystem-next-20260910/c15a-overnight-ui-20260921/`: screenshots, initial
failure, read-only native listing inspection, build log, deployment/preparation and
rollback receipts, run identities and observations. Actual run captures remain in
`.rlo-test-instance/results/` under the four labels above. These are private ignored
artifacts, not repository or publication payloads.

All ten owned transactions were rolled back. Fresh `restoration.json` verifies seven
baseline hashes, eleven absent optional files/directories, the retained supplier
repair and passing metadata audit. No fixture game, helper or build remains running.
The unchanged packaging checks already have nine passing tests; no repeated matrix
or new harness was needed for this GUI correction. A fresh source-inclusive private
archive retains the tested core and the qualified helper with matching source.

The refreshed archive is
`artifacts/releases/0.3.0-rc.2-01a509b465019dc42649c29d913faf8d2ef97c42-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`.
ZIP SHA-256:
`96e5a78abec307f823350e5fa43ff18f34c136782c4360ca3d2ef0eca8b9c340`.
Archived source is `01a509b465019dc42649c29d913faf8d2ef97c42`; embedded core build
remains `908444675b1ddd33b818381fb67498eeb5faafc1`. `package-verified.json` confirms
the actual ZIP bytes, archived fix, source identity and exactly two runtime binaries:
the tested core and pinned helper. The helper retains matching source `83a2930f`.
The subsequent documentation-only closeout does not require another build/archive.

Independent bounded review verified the four run hashes, prepared/reset store chain,
normal exits, reachable settings screenshots, one-column source fix and restoration
record. It found no blocking defect or overclaim. It performed no fixture operations,
builds or tests. Parent acceptance remains separate from this review.

Parent review, useful retained-feature performance work during the current window,
and separately authorized Windows Steam then Deck qualification remain distinct.
There is no self-acceptance, successor dispatch, merge, push or publication here.
