# Current state — 25 September 2026

**0.4.1 is published on GitHub and Steam Workshop.** It adds specific FGL
Continued Preview + Image Opt + current ImageOptCompat support. The Windows test
collection included a supplier-only baseline, explicit Wake-Up verification and
ordinary user activation, all with normal exit. The two Wake-Up runs used the
exact `82b8293e` runtime. All 33 sampled image records matched, all 392 Giddy-Up
reads succeeded, and acknowledged notices stayed closed. The fixture is restored
and fully audited. The package reuses that DLL unchanged, with matching source.
Both uploads completed and the live Workshop description and other listing fields
were preserved. See the [0.4.1 publication receipt](release-0.4.1-publication.md)
and [release notes](release-notes-0.4.1.md). No further work is assigned.

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

Keep the accepted `codex/compatibility-phase-1` checkout. Local main retains private
history; origin/main and published tags retain separate public history. Never
push private main, merge unrelated histories as cleanup, or infer publication
permission from a prepared package. Follow the [release procedure](release.md).
Earlier cleanup and stopped performance experiments remain documented in
[history](history.md); they are failure context, not assignments.
