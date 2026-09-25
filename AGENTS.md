# Repository guidance

Read [current state](docs/current-state.md) before project work and
[development](docs/development.md) before building or operating a fixture.

## Baseline and ownership

The latest actual release is 0.4.0 on GitHub and Steam Workshop.
See docs/release-0.4.0-publication.md for the verified state. The owner stopped the aggressive campaign,
then requested release restoration, generated-file cleanup, consolidated docs,
main checkout and deletion of other local branches. Compatibility Phases 1 and 2
are complete and accepted for their recorded bounded GOG correctness scope.
On 25 September the owner authorized an overnight continuation through integration,
representative GOG performance comparisons and a release-ready package for the
owner to publish. Keep one product/build/fixture owner and continue the accepted
codex/compatibility-phase-1 checkout. Never close normal games to obtain idle
conditions. Use matched controls, actual operation evidence and normal exits.
The owner subsequently authorized corrected 0.4.0 Windows publication to Workshop
and GitHub with matching source and developer docs. Preserve the live Workshop
description and unrelated listing fields; update only content and separate change
notes. Reuse the tested 3ac8973a DLL without rebuilding or further game launches.
Restore this checkout after public-source synchronization. Linux/Deck and unrelated
projects remain outside scope. See docs/release.md and docs/current-state.md.
Historical plans are not assignments.

Use the existing checkout; do not create worktrees unless requested. Keep one
editing/build/fixture owner at a time. Preserve unrelated user edits.
Start newly assigned implementation on a codex/ branch from current main.
Commit with git -c core.hooksPath=.githooks commit -s.

Local main retains private development history; origin/main and published tags
retain separate public history. Never push private main, --all or a mirror,
or merge unrelated histories as cleanup. A temporary public-source branch can
be recreated from origin/main for separately authorized publication.
Follow [release procedure](docs/release.md#source-history-and-publication).

Publication requires explicit authorization. Preserve the Workshop description,
preview, item ID and unrelated listing fields unless requested. Cleanup does not
publish or modify an installed game.

## Private data and testing

Generated builds, packages, decompiled research and raw evidence belong under
ignored artifacts/. The only supported fixture path is .rlo-test-instance/ here.
The fixture is permanent infrastructure. General cleanup, reset, or generated-output
removal never authorizes deleting `.rlo-test-instance/`. Deleting or replacing its
game installation requires explicit owner authorization naming that installation.
Never run repository-wide ignored-file cleanup that could remove the fixture.
Normal fixture deployment, profile and cache operations remain permitted within
an explicitly authorized fixture task.
Never add game/mod/profile/save/cache content to Git. Use git ls-files or rg --files
with ignore rules; do not recursively enumerate the repository root with PowerShell
or use rg --no-ignore / rg -uuu there. Inspect specific ignored directories instead.

Cleanup invalidates assumptions about old packages, receipts and fixture state.
Recreate and verify the isolated fixture under a new authorized task.
Use scripts/fixture.py for fixture operations. Normal Steam, Workshop, profiles,
saves and caches are outside scope. No Deck access or desktop automation without
authorization. Do not load game assemblies reflectively for inspection.

Performance needs explicit current permission and recorded conditions. All normal
game instances must already be stopped; never close them to obtain idle conditions.
Functional checks use --purpose functional and the fixture-specific process guard.
Use independent menu observation, normal exit/capture, actual exit status and
automaticTestPassed. Forced closure is failure recovery, not a successful sample.
Restore changed fixture state before handoff. GOG evidence does not qualify Steam
or Linux/Deck, and testing does not authorize publication.

## Product decisions and explanations

Prioritize useful behavior and proportional checks. Do not turn harnesses, reviews
or documentation into a separate project. Count all preparation, checks, reads,
restoration, waiting, upload, cleanup and memory costs. Stop repeatable losses.
Reopening a route needs a materially different evidence-backed premise; see
[history](docs/history.md). No on-demand, progressive-detail or companion work is
assigned.

Prefer shared loading improvements. Supplier integrations must remain optional
with ordinary behavior when absent or unsupported. Preserve callbacks, ordering,
object relationships and required threads. Use native behavior when no supported
beneficial implementation is established.

Explain results in plain language before technical detail. Introduce unfamiliar
terms when useful. Distinguish source, package, deployed fixture and captured-run
identities; startup, gameplay correctness and measured speed are separate claims.
Do not add overlapping timings or percentages from different workloads.
