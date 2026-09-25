# Development and validation

Use the [current release baseline](current-state.md) and existing checkout.
Compatibility Phases 1 and [2](compatibility-phase-2.md) are complete and accepted
for their bounded GOG correctness scope. The owner authorized the 25 September
[overnight continuation](overnight-integration-20260925.md), including representative
GOG performance comparisons and bounded integration improvements. Retain the
accepted `codex/compatibility-phase-1` checkout and preserve unrelated edits.
The owner subsequently authorized corrected 0.4.0 publication after the targeted
Windows check; follow the specific scope in [release procedure](release.md). No further
game launches, rebuild or platform/performance checks are assigned for publication.

## Build and source checks

Follow [source-build.md](source-build.md) for the pinned SDK, legal game/Prepatcher
references and commands. Output belongs under ignored artifacts/. A source
checkout does not contain game references or a ready private fixture.
Do not rebuild unchanged release binaries for documentation-only changes.

Run focused checks appropriate to changed behavior. Documentation cleanup needs
link/reference checks and source identity comparison, not live game runs.
Commit using `git -c core.hooksPath=.githooks commit -s`.
Never add private game/mod binaries, saves, profiles, caches or raw evidence.

## Isolated fixture

The supported path is .rlo-test-instance/ in this checkout. It is permanent
infrastructure. General cleanup, resets and generated-output removal never
authorize deleting it. Deleting or replacing its game installation requires
explicit owner authorization naming that installation. Do not run repository-wide
ignored-file cleanup that could remove it. Normal deployment, profile and cache
operations remain allowed within an explicitly authorized fixture task. Use scripts/fixture.py;
inspect current help and admission requirements before operating it. Cleanup
invalidates assumptions about old packages, snapshots, receipts and generations.
Recreation requires legally available sources and a new authorized task.

For a small correctness collection, `initialize --sources <private.json>` reads
only explicitly named packages and generates a clean ModsConfig profile. It does
not discover normal Steam mods or import the normal profile. The JSON has schema
`rlo-fixture-sources.v1`, `packages` entries containing an absolute `path` and the
expected lowercase `packageId`, an `official` list of installed GOG package IDs,
and an `activeOrder` list. Core is required; Prepatcher and Harmony must occupy
the first two active positions. Package IDs and folder names must be unique,
sources must be outside the fixture, and the ordinary game/path/content/process
checks still apply. Keep this private source list under ignored artifacts.

Use `discover --sources <private.json>` to check those selections without copying.
Use `refresh --sources <private.json>` to select a different collection, including
a different edition sharing the same package ID. Refresh retains the game but
resets the fixture profile and invalidates product/observer deployment receipts;
deploy those packages again before preparing a run. An explicitly initialized
fixture refuses refresh without `--sources`, preventing accidental import of the
normal collection. The default initialization workflow remains available.
The existing later `--selection` can only choose IDs in the frozen active order;
an omitted supplier requires an explicit refresh, not a handwritten manifest.

Normal Steam/Workshop installations, profiles, saves and caches are outside scope.
Do not access the Deck or automate the desktop without authorization.
Only one team owns edits, builds, deployment and launches at a time.

Functional checks use --purpose functional. A verified normal game outside the
fixture may coexist; an existing fixture process or uncertain identity blocks
operation. Never close a normal game.

Performance needs fresh explicit permission. All normal games must already be
stopped. Preserve other applications and the owner's stated measurement conditions.
Do not weaken process, content or package admission to obtain a run.

Keep independent menu observation and normal automatic exit. Wait for actual
process exit/capture, check automaticTestPassed and use
menuReadyObservation.elapsedSeconds. Forced closure of a verified fixture process
is failure recovery, not a successful sample. Restore changed state and verify
original identities/absences before returning ownership.

## Measuring useful improvement

Match inputs, source selection/order, settings, observer and diagnostics.
Compare native behavior and the actual release, separating first construction
from warm reuse. Use forward/reverse controls and disclose background activity.
Do not discard inconvenient samples to rescue a small gain. A declared warmup
is separate from counted samples.

Include source discovery/reads, checks, helper startup/transport, decoding,
restoration, waiting, publication, writes and cleanup. Overlapping work timers
are not additive elapsed time. Memory budgets are not observed process memory.
Keep exact source/build/deployment/run identities and evidence limits.

Correct output alone does not prove useful speed. Stop losing approaches unless
new evidence changes the premise. Do not broaden harness work or repeat adequate
validation without a concrete product risk. See [history](history.md).

## Release boundaries

Establish changes on the authorized isolated Windows fixture. Additional platform tests
need separate authorization; one platform does not qualify another.
Publication is separately authorized and follows [release procedure](release.md).
Preserve private/public histories, corresponding source/notices and unrelated
Workshop listing fields.
