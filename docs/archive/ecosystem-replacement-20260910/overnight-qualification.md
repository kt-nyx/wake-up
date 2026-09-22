# First overnight Windows qualification checkpoint

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent steward review

Accepted on 10 September as a bounded qualification/performance checkpoint through handoff `13205cebf1f448681c7046e5dc653c153cf35a5f`, with unchanged core `79dde43` and private controls `4242709`. The steward reviewed the actual diff, prepared-store admission, separation of functional probes from performance, run purposes/exit results, native prepared-hit receipt, helper receipt, observer DLL hash and preserved final timeout. The menu comparison does not justify the large first-run speedup or an isolated routing benefit. The scene-entry timeout is unresolved, not a passed save/background test or evidence that Wake-Up caused the wait. Earlier short forced stops were insufficient and must not become the stopping rule for future diagnostics.

Continue the authorized overnight workflow in a fresh task focused on identifying the scene-entry wait, repairing the concrete cause where permitted, and completing real save/background/focus behavior. Prefer direct evidence about the blocked scene/queue over another unchanged timed-out run. Remaining capability disposition and final candidate review follow; this checkpoint does not establish release readiness. All original goals and explicit coverage limits remain tracked.

This checkpoint verifies a real preparation workflow, cache maintenance and a
useful French translation-stage improvement. It does not complete ecosystem
replacement or establish release readiness. The final save-entry attempt timed
out before the reload checks; false-background-preference and focus behavior
remain unqualified. This is a bounded handoff for steward review.

## Scope and identities

Dispatch: `wake-up-overnight-qualification-f9725ec`. The clean assigned base was
`f9725ec76c4083da947dc42a916cc843950b043d`; work uses the single checkout on
`codex/overnight-qualification-f9725ec`. Fresh process inspection found no
RimWorld process. The owner authorized local functional and performance runs,
representative workloads and fixture-only visual interaction for this session.

The core was not changed or rebuilt. Every candidate run uses accepted source
`79dde439051e56dab5fb910958fafd689fd6750d`, DLL SHA-256
`dca48f2dadd4f0c503aa66f9c09887576ecec984d2e7b8cbec16dbc1e4e4e87a`, originally
deployed by `20260909-201553-2314b90e`. An absent control parks that core outside
discovery; its receipt does not mean Wake-Up was loaded.

The first UI run used accepted observer
`081ba050e6cab8acefe9d78be355cbdeea319a85`. Private observer/fixture correction
`4242709fb1380a1260069b1390aa48dc8444daf3` supplies subsequent probes and timing
controls. Its build had zero warnings/errors. The old observer was briefly
redeployed for the explicit scene-entry control, then the corrected observer
was restored. No observer is shipped in the core product.

The final observer DLL SHA-256 is
`692f13788886617c90d5e7cc0a408780328ba7e1606f0c166893b2d2fc3f677d`,
restored by deployment transaction `20260910-000357-6eff7865`. The frozen
generation is `20260904-182829-e058641c`, manifest SHA-256
`b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d`.

Each run retains its own core and observer inventories, run UUID, preparation
transaction, profile, actual process identity and exit result. The compact
[run summary](../../../artifacts/overnight-qualification-20260909/run-summary.json)
contains complete identities and the relevant receipts. Raw captures are under
`.rlo-test-instance/results/oq-*/`; all raw helper and investigation output is
under ignored `artifacts/overnight-qualification-20260909/`.

## Public preparation and visual checks

`oq-visual-01` used the actual Options → Mod options → Wake-Up → Prepare textures
path, with supported Windows computer-use controls targeting only the verified
fixture executable. The selected mod was Vanilla Ideology Expanded — Memes and
Structures. The native-preservation dry run found 698 selected files: one
eligible JPEG and 697 authored DDS files. Preparation completed one entry,
skipped the DDS files and reported zero failures. A second complete resume pass
reused that entry without rebuilding it. The resulting owned entry is 1,398,780
bytes; its complete hash is retained in the summary.

The public **Use prepared textures** checkbox was enabled, the settings dialog
was closed, and the game exited through **Quit to OS**. The captured native
settings file records the selection and the process exited zero. This is a
manual UI qualification, not an automatic-exit run or a speed result.

`oq-prepared-hit-06` then restored only that captured store through the fixture
tool, used ordinary settings activation and recorded a real native startup hit:
1024×1024, seven mips, DXT5. It passed normal automatic exit. The private observer
observed a successful return from the loader; it did not call the preparation
backend or manufacture a cache entry after the menu. Existing accepted rendered
output comparison remains the fidelity evidence for this JPEG.

`oq-display-08` visually showed Wake-Up's panel during native definition loading.
Its stage text, elapsed time, cache status and diagnostics were readable and did
not overlap the native central loading box in the current fixture layout.
Native loading and normal exit passed. Settings/preparation windows were also
visually inspected and usable. This does not qualify arbitrary resolutions,
localizations, focus/minimization or every loading stage. A resume completed
before a cancellation click could take effect; cancellation is **not** newly
qualified by that attempt.

## Owned cache maintenance

The three `oq-cache-*-07` runs restored the same UI-created prepared entry and
used the ordinary one-shot settings. All passed normal automatic exit, selected
no prepared startup hit and reset the maintenance choice to its normal default.

| Choice | Captured result |
|---|---|
| Bypass | Existing prepared entry retained, no prepared read |
| Rebuild | Existing prepared entry retained, reads bypassed; conversion was not silently run at startup |
| Clear | Prepared entry removed, ordinary loading completed |

This is live coverage of the prepared category. Broader corruption/quota/eviction
behavior retains its existing focused offline evidence; it was not exhaustively
retested here. The PNG/JPEG category's existing first-build and warm rendered
checks remain separately recorded in the earlier Windows qualification.

## Windows helper and useful coverage limit

The accepted Windows helper SHA-256
`ba7fb9a3c70c3559af4fe9c5c25e3c506c8a2195e8fbce68eaa18861c8068abc`
converted the actual frozen `VME_TaoistCarpet.jpg` source. Its 185,121 bytes and
SHA-256 `3e6bf7aed15fe578c6ae0d3c31c1d7b316633cf8b29f0c891bec733041e9a046`
matched the frozen inventory and remained unchanged.

| Explicit helper request | Output | Process plus conversion |
|---|---|---:|
| Full size | BC1, 1024×1024, 11 mips, 699,064 payload bytes | 0.134 s |
| Half size | BC1, 512×512, 10 mips, 174,776 payload bytes | 0.081 s |

Handshake, response framing, dimensions, mip layout, independently decoded
payloads and exit-zero checks passed. Both owned helper processes exited.
[Exact helper evidence](../../../artifacts/overnight-qualification-20260909/helper-real-jpeg/receipt.json)
records this bounded raw conversion. Existing 15 image correctness checks were
trusted rather than repeated.

The carpet is outside the public lossy path's admitted BGPlanet background role.
No eligibility rule was loosened to make a fixture pass. There are still zero
recorded useful BGPlanet overrides in this workload. Therefore **a useful public
Windows lossy workflow remains unqualified**, despite working helper conversion.
No helper was installed into the fixture or bundled with this core. Broader
role coverage and final helper packaging remain explicit next-task decisions.

## Representative compatibility and performance

The recorded mixed selection contains 99 content packages plus Wake-Up and the
observer. The existing selection/order was reused; Character Editor was
temporarily restored through the fixture tool, then its original exclusion was
restored. The other original exclusions were retained. These are representative
subset results, not full-315-package qualification.

All four French runs below passed normal automatic exit. Both arms used ordinary
default features, native DDS selection, fresh application/Gagarin caches, PNG
caching off and prepared textures off. The enabled arm additionally selected
translation application and texture-supplier routing. The independent menu
observer was identical; no functional probes or preparation ran in these arms.
All RimWorld processes had to be stopped before each performance launch.

| Order / capture | Menu seconds | Native translation stages, ms |
|---|---:|---:|
| Forward control, `oq-mixed-control-09` | 215.516 | 2,506.279 |
| Forward enabled, `oq-mixed-on-09` | 67.870 | 940.180 |
| Reverse enabled, `oq-mixed-on-10` | 67.137 | 913.594 |
| Reverse control, `oq-mixed-control-10` | 68.324 | 2,556.942 |

The translation-stage saving repeated in both directions: approximately
1.57–1.64 seconds, or 62–64% of these particular two native translation scopes.
These scopes are the before/after-implied-definition application passes, not all
language loading. Enabled runs executed 680 package scopes and 27,939 lookups
with zero native fallbacks or retained lookup entries. This supports retaining
the opt-in translation feature for applicable language workloads.

The **large first menu difference is not an optimizer benefit**: Loading
Progress recorded about 135.4 seconds of texture-holder work in the first
control versus 4.65 seconds in the next run. Windows file-cache state was not
controlled, and the exact cause of this first-read/wait difference is unproven.
The reverse pair's menu difference is 1.19 seconds; these few runs do not establish
a universal whole-startup percentage or an isolated routing benefit. No default
was changed. Loading Progress stage/worker totals overlap and are not summed
into a new wall-time budget.

Character Editor exercised 6,268 object and 47 turret presets, with dictionary
reuse, no fallback and no retained entries. Giddy-Up exercised 1,380 offset
lookups without fallback/errors and released its textures. Loading Progress
coalescing installed. Gagarin's ordinary cache built, while Wake-Up correctly
refused its extra parsed-XML hook because Loading Progress owns an interfering
hook. Exact counts, hashes and refusals are preserved per run; supplier startup
coverage does not qualify every supplier gameplay interaction.

### Prepared-store comparison showed no menu benefit

The native/prepared/native comparison used the same two-supplier order, same
core/observer and same ordinary prepared-texture setting. The native arms had no
prepared store; the other arms restored the verified UI-created entry. These
performance arms had no functional probes. Most textures remained authored DDS;
only the one JPEG was relevant to preparation.
Native startup consumption was observed directly in functional run 06; the
timed arms used matching order/settings and the restored entry without per-hit
observation.

| Forward and reverse order | Menu seconds |
|---|---:|
| Native, `oq-texture-native-11` | 11.611 |
| Prepared, `oq-texture-prepared-12` | 11.632 |
| Prepared, `oq-texture-prepared-13` | 12.003 |
| Native, `oq-texture-native-14` | 11.763 |

Both prepared arms were slower (about 0.02 and 0.24 seconds). This establishes
**no useful menu saving for this one-JPEG case**, not a rejection of all prepared
textures or PNG-selected workloads. The controller's one-time preparation cost
is separate from these startup times. No larger texture workload was introduced
to rescue this result.

## Save completion investigation and remaining work

The observer now selects the actual false background preference through native
Prefs, observes engine state before/queued/after a save load, and checks speed
before pausing it itself. Native RimWorld does not serialize selected time speed:
with PauseOnLoad enabled, it deliberately executes one completion tick and then
pauses. The check therefore expects that native tick, stable paused updates and
a separate unpaused-policy reload. It records actual `Application.isFocused`;
it never substitutes a focus boolean.

Runs `oq-prepared-background-02`, `oq-background-03`, `oq-absent-smoke-04` and
`oq-old-observer-05` were forcibly ended after apparent scene-entry stalls.
Each had reached the menu; none had reached the false-preference reload probe.
Logs, process observations and pre-stop run receipts were preserved before
closing only the freshly verified fixture process. Automatic/gameplay results
are false. The latter two controls had Wake-Up absent, and the final one used
the previously accepted observer.

These runs are **inconclusive**, not proof of a product deadlock. The later
representative control recovered after a similarly quiet/unresponsive period,
showing that the earlier stop criterion was insufficient.

Final run `oq-final-save-15`, observer UUID
`e0ea89b1cadf42a98f9ac907e25dceac`, reached the menu at 11.823495 seconds but
did not reach the reload probe during the full existing 600-second post-menu
allowance. The launcher reported `Normal shutdown did not finish within 600
seconds of menu-ready` and left the process alive. Its log and process identity
were preserved, then only verified fixture PID 42016 was forcibly closed and
the profile captured through the fixture tool. The run retains its actual
`automationFailure`; exit-code and success fields are absent because the
launcher had already stopped. This is a failed qualification with forced cleanup,
not a normal-exit pass. [Timeout and closure evidence](../../../artifacts/overnight-qualification-20260909/oq-final-save-15-timeout/closure.json)
is retained alongside the capture. The cause of the scene-entry wait remains
unestablished. No false-preference, unpaused-reload or OS-focus success is claimed,
and the earlier accepted small save check is not broadened by these attempts.

No permanent capability omission or redesign was made in this checkpoint.
Read-only reassessment recommends a bounded world/new-game background extension
after save qualification: world generation remains in the entry scene, so it
must not reuse the existing unconditional wait-for-Play-scene lifetime.
Standalone map generation needs its own boundary. The tested S04/S06/S09 routes
remain negative results; S05 still lacks useful admitted work/effective-hook
proof; broad S10 roles, S11 on-demand and S12 remain undelivered. No changed
premise justifies another large XML/cache/scheduler rewrite here. See the
individual slice reports for their retained measurements and consumer evidence.

## Checks and handback

Guarded commit `4242709` changes only the
private fixture/observer and tests: ordinary settings/language can be recorded
for matched performance arms, functional probes are excluded from those arms,
and prepared-store restoration verifies source/menu evidence, owned paths and
complete hashes. The public core and its default-off policies are unchanged.
The focused fixture suite passed 36 checks, including preserved-metadata byte
tampering and unowned-path rejection for the new prepared-store restore.
Final fixture/release Python verification passed all 43 tests. The observer
build passed with zero warnings/errors; unchanged core checks were not rerun.
An independent read-only review checked the representative arithmetic, supplier
hashes, preparation/helper receipts and performance wording.

All 19 run folders are captured: 13 normal automatic passes, one normal manual
exit, four early forced failures and the final full-budget timeout. The final
fixture metadata audit passed, the accepted core and corrected observer DLL
hashes match their receipts, and the original three mod exclusions are restored.
The retained current profile is the captured final save attempt; it is not a
successful combined release candidate.

This team stops all writes, builds and launches at its guarded documentation
handoff and returns exclusive checkout ownership to the parent steward. Both
subagents are stopped; no fixture or helper process remains alive. No normal
game/data, Deck, other application, security setting or public listing changed.
No permanent capability omission, release readiness or steward acceptance is
asserted. The next task should identify the scene-entry wait before expanding
save/focus claims, while retaining the measured translation improvement and
the now-qualified native preparation workflow.
