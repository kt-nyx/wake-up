# Current state — 19 October 2026

## Public development and cloud setup

The owner authorized publishing the current 1.7 development source on the public
`1.7` branch and implementing a dedicated Wake-Up cloud workflow. This source
publication does not publish a runtime package or qualify Linux execution.
See [development branch status](development-1.7-branch.md). The current product
source is 0.5.0-preview.2 with exact Windows 1.7.4982/Harmony rc.3 admission.
The dedicated cloud environment, current Linux target and fresh-chat acceptance
are pending. The existing general cloud task's successful base-game/DLC smoke
test does not establish Wake-Up compatibility or speed. Final Windows candidate
verification and release publication remain local and owner-controlled.

The records below preserve earlier assignments and evidence with their original
scope; their dates are not renewed launch, installation or release authorization.

## Current compatibility update

The 18 October handoff authorizes an offline compatibility refresh for the running
1.7.4982 rev1280 game and installed Harmony rc.3. Preview 2 retains exact build
guards after static comparison and offline patch tests. Build and stage only;
do not alter the running installation or profiles, or launch/stop a game.
See [the compatibility review](compatibility-1.7.4982.md). The installed candidate
remains `c56e89ab`; publication and current-game performance qualification remain
unassigned. The 14 October records below refer to the older game and candidate.

**0.4.2 is the latest published runtime.** The installed package matches all 26
manifest entries, including DLL SHA-256
`53d93c72ead60e5acb07a6537719f71270082acb1b709f373a5c71cee12a64a3`
and public source `4ac8605fc1b5d1b62efbe1c281c7e22722705b46`. Its notice changes
passed offline validation; no new game/UI or performance qualification accompanied
that release. The listing and documentation were deliberately preserved, which
left the old 0.4.1 descriptions below stale. Publication evidence remains privately
under `artifacts/release-0.4.2/publication-receipt.json`.

## Current 1.7 assignment

The owner authorized overnight correctness/performance tests, supplier combinations
and evidence-led improvements on 14 October, until approximately 10:30–11:00 AM
Toronto time. The installed 1.7 game and current Host collection are copied into a
separate `.rlo-test-instance/1.7` lane; the old 1.6 fixture game and played profiles
are preserved. Use `scripts/fixture.py modern`. Publication remains unassigned.
The pre-existing `scripts/fixture.py` path correction is preserved. Follow the
[overnight plan](overnight-1.7-plan.md).

At the owner's real-play handoff, the exact `c56e89ab` DLL was installed into
`RimWorld/Mods/Wake-Up-1.7-Preview`, replacing the original private preview.
It passed bounded colony, simulation, save/reload and French-language checks;
consistent loading improvement remains unqualified. The test observer is excluded,
Host mod order/settings are preserved, and no game was launched during installation.
The previous preview and installation receipt are retained privately under
`artifacts/implementation-1.7/candidate-c56e89ab-playtest-20261014/`.
Normal logs rotated by the fixture have been restored, with test logs preserved.
See the [qualification record](qualification-1.7-20261014.md). Automated testing
is stopped for the owner's real play; publication remains unassigned.

The initial private `0.5.0-preview.1` candidate was built and packaged from `f09ca848`.
Release compilation has zero warnings/errors; 59 C# synthetic checks and 14 Python
release/package checks pass. The package audit verified 33 manifest entries,
version-specific dependency/code selection and unchanged released 1.6 payload.
The 1.7 DLL is Prepatcher-free by static assembly inspection. Full identities and
the retained offline evidence are recorded in the implementation plan. No live
compatibility or speed claim follows from these checks.

Read the [implementation plan](implementation-1.7.md) and
[testing handoff](testing-1.7.md). The private candidate targets the recorded
1.7.4977 rev1248 game and Harmony 3.0.0-rc.2; an update requires refreshed static
review and reference identities. Official Workshop publication is planned after
1.7 reaches the main branch and final qualification, with owner approval.

## Historical 0.4.1 publication

0.4.1 adds specific FGL
Continued Preview + Image Opt + current ImageOptCompat support. The Windows test
collection included a supplier-only baseline, explicit Wake-Up verification and
ordinary user activation, all with normal exit. The two Wake-Up runs used the
exact `82b8293e` runtime. All 33 sampled image records matched, all 392 Giddy-Up
reads succeeded, and acknowledged notices stayed closed. The fixture is restored
and fully audited. The package reuses that DLL unchanged, with matching source.
Both uploads completed and the live Workshop description and other listing fields
were preserved. See the [0.4.1 publication receipt](release-0.4.1-publication.md)
and [release notes](release-notes-0.4.1.md). No further work is assigned.

## Published 0.4.2 notice work

The notice changes were published in 0.4.2 from `codex/clear-compatibility-notices`.
Presentation, grouping, saved-notice migration and known-mod attribution changed;
optimizer decisions did not. A Release build and 77 focused offline tests passed.
See the [notice policy](compatibility-notices.md). Those checks do not establish
1.7 compatibility or live visual correctness.

## Historical 0.4.0 release and corrected-build evidence

Corrected **0.4.0 is published on GitHub and Steam Workshop**. The exact reviewed
package, matching source and public tag are available. Both uploads completed;
the live Workshop description and unrelated listing fields are unchanged. See
the [publication receipt](release-0.4.0-publication.md) for verified identities,
successful source checks and the preserved first failed Steam attempt.

Two compatibility defects also present in 0.3.0 are corrected: a late supplier
rewrite could make Wake-Up throw while Harmony rebuilt an XML worker, and retired
inheritance hooks removed the native call needed by Adaptive Storage Framework.
Changed workers now pass through unchanged with a persistent notice, and native
inheritance code remains intact. Offline tests reproduce the Harmony rebuild
interval; the targeted Windows menu check below verifies the corrected build with
YaOpt, Image Opt and ASF together. This does not establish a black-texture fix,
universal gameplay compatibility, measured speed, or Linux/Deck qualification.

The published package reuses the exact tested `3ac8973a` DLL without rebuilding. Its later
documentation/source archive revision is recorded separately. The older
`c36e9a7dfa702d50d47f21f4fe426c60e0338b67` archive contains the confirmed
defects and must not be published as the corrected build. Earlier performance
and combined-gameplay evidence below remains tied to its original binaries.

The Release build and 47 focused compatibility/identity checks passed. All nine
new bug regressions pass. The wider regression suite recorded 50 passes and one
intermittent notice-acknowledgement persistence failure on each full run;
isolated reruns passed, but the storage issue is not claimed fixed. All ten real
notices were displayed and acknowledged in the targeted live run.

## Targeted Windows menu check

`bugfix-yaopt-imageopt-asf-01` tested source
`3ac8973a2aee7879c0e184bd124d911198018875`, DLL SHA-256
`ccfa3f0a6777856120a26181ada1ba642bbe2925fcee5d03702f45e0f67614c6`,
with Wake-Up before YaOpt 1.1.4, Image Opt 0.1.13 and the full ASF package at
`31ee88b46651cfbe37da3f89620bc0b907165cc0`. It reached the independently
observed menu and exited normally, with automatic and compatibility checks passed.
The expected XML output, all eight YaOpt XML-worker hooks and ASF's inheritance
hook were present. The complete log contains neither reported patching error nor
an exception/XML-loading error. Nonfatal fixture dependency-link warnings and Mono
fallback diagnostics remain, along with Unity shutdown allocation statistics.
This is not a warning-free log or visual texture/colony/performance qualification.
The actual in-progress Harmony rebuild interval remains covered by offline tests;
the live worker-admission log already sees YaOpt's installed hooks. Compact
evidence is retained privately under `artifacts/report-bugfix/live/receipt.json`.
The fixture was restored and fully audited; no further launch is assigned.

## Earlier candidate identity and result

- Tested core: `fe01a28669a99713b1764a6947d1522516bdee54`.
- Core DLL SHA-256:
  `e5b0e1b30e68c882187078a3aecad0f3d289f15c37fb1d345edc07cdbf8611f2`.
- Private observer: `4f49c2ae1fffe296697d67a80b9213742621ffdf`; it is not a shipped runtime component.
- Final combined runs: `ov-fixed-combined-ack` and `ov-fixed-combined-game`.
  Loading Progress, PurePatcher, Kingfisher, Image Opt, Giddy-Up and real VEF content
  passed startup/XML/ready-texture checks and actual colony/save/reload, with normal
  exit and automatic acceptance. Affected VEF background-map work remains refused;
  ordinary map loading is preserved.
- Final worker/XML checks: 82 passed. Package boundary checks: 10 passed.
- 40 measured launches on `4f49c2ae`, followed by 12 affected-supplier repeats on
  `fe01a286`, retain all negative and mixed results. Loading Progress benefits in
  the recorded workload; stacking other loaders does not imply a net speed gain.
- That superseded candidate's metadata withheld publication authorization and
  additional platform qualification. Its source-inclusive package reused that DLL, recording the later
  source/archive revision separately. See its manifest for the archive identity.

Read the [0.4.0 release notes](release-notes-0.4.0.md) for measurements, changes and
build boundaries, and the [compatibility guide](compatibility.md) for the complete
mod/tool decisions, setting choices and live evidence limits. Phases
[1](compatibility-phase-1.md) and [2](compatibility-phase-2.md) remain accepted
bounded correctness evidence. The [overnight continuation](overnight-integration-20260925.md)
added exact integrations, gameplay/lifecycle checks and authorized GOG comparisons;
its implementation and measurement work is complete. Historical plans are not new
execution authorization.

## Fixture handoff

The permanent `.rlo-test-instance/` has been restored through its supported tool
and passed a full content audit. Its selected order is Prepatcher, Harmony and
Core; the profile contains only generated `Config/ModsConfig.xml`. Product and
observer deployment are absent, and no RimWorld process remains. The restored
manifest SHA-256 is
`1eb1c70e2edca106a1f06706a73631dc170e3be1c96e6f75807ed821507b0fd1`.
The generation metadata changed; package/game inventories, selected order and
ModsConfig bytes match the prior baseline.
The original game assembly remains
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
The GOG installation's Version.txt says rev573, while that admitted runtime reports
1.6.4871 rev574; binary identity defines the actual test target.

Private build packages, captured runs, source lists and unsuccessful attempts are
retained under ignored artifacts and fixture results. General cleanup never
permits deleting the permanent fixture. Normal installations, saves, profiles,
Workshop subscriptions and the Deck were outside this work.

## Published release and source history

Published 0.3.0 retains its own qualification and identities:

- Private release source: `12e616dcb2ec7ff3c94de63ea6a97da7d9612262`.
- Public `v0.3.0`: `d5fc9646d74a47a6f30fe0556be02f44cd1a67dc`.
  Their source trees match; their histories remain separate.
- Tested core: `67265e5696c80c9a461423722870a742054dd43b`.
- Published DLL: `a64f9a446aaf7743024e0246f5e795dee1986f9a5eb647bc12246955af2ddfda`.
- Optional unchanged Windows helper:
  `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.

See its [publication receipt](release-0.3.0-publication.md) and
[release notes](release-notes-0.3.0.md). Those Windows Steam/GOG claims belong to
that binary, not automatically to 0.4.0. The live Workshop description is unchanged.

The historical fixture work used `codex/compatibility-phase-1`; current 1.7 work
uses `codex/rimworld-1.7-core`. Local main retains private
history; origin/main and published tags retain separate public history. Never
push private main, merge unrelated histories as cleanup, or infer publication
permission from a prepared package. Follow the [release procedure](release.md).
Earlier cleanup and stopped performance experiments remain documented in
[history](history.md); they are failure context, not assignments.
