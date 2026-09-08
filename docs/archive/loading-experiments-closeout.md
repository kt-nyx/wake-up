> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_OVERNIGHT_STEWARD_CLOSEOUT.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Overnight stewardship closeout, 2026-09-06

Accepted main has been restored and passed a small automatic startup check.
The overnight experiments did not establish a new general loading optimization
worth promoting. Only these decision records and current-state documentation
were added to main; experimental runtime, fixture selectors and tests remain on
their research branches. Nothing was pushed or published.

## Current accepted, deployed and tested state

The restored package's exact source revision is
`7d464eac207d28d586d5c0321cdeb617b3ef0ed8`. The existing package was explicitly
deployed using `scripts/op7_fixture.py deploy --package`; package hashes and
contents passed verification, and deployed/prepared source revisions matched.
Main after this closeout differs from that source only in documentation.

The current captured selection is **night-main-s01-restored**: candidate
`startup-searches`, small activation lane, timing observation, default optional
selectors (including Gagarin off), independent menu observer and automatic
normal exit. Menu observation was **9.292139 seconds**, exit code **0** and
`automaticTestPassed=true`. The child process exited and capture completed;
the game is closed. This is a restoration smoke check, not a performance
comparison or a new qualification of every accepted optional feature.

Main retains accepted startup-search improvements and optional Gagarin XML
reuse, default off. The prior integrated Gagarin runtime f37b34d qualification
remains applicable: the difference from f37b34d to restored 7d464ea is
documentation. Gagarin remains optional; unsupported or absent suppliers keep
the original path. See [the accepted feature record](gagarin-cache-loading.md).

## Measured decisions

| Investigation | Practical result | Retained branch head / tested runtime |
| --- | --- | --- |
| [DDS read-ahead](dds-read-ahead-qualification.md) | Warmed full original 193.943 s, candidates 195.979 / 194.423 s: no useful warmed startup gain. First-read Windows-cache conditions were uncontrolled; a cold-like benefit remains unmeasured. | codex/overnight-dds-qualification: ae3dbfc / 1d1528d |
| [PNG read-ahead](png-read-ahead-qualification.md) | Warmed medium menu means 39.657 s original / 39.609 s candidate; about 0.677 s texture-stage saving did not establish useful startup improvement. | codex/overnight-png-qualification: 455ccbe / 2327212 |
| [Shared XML index](xml-index-rebuild-investigation.md) | Narrow unnamed-root retention avoided one rebuild; total index-build time remained about 1.314 s. No useful gain, no broader mutation redesign. | codex/overnight-shared-xml-work: 10b5907 / 6ba144a |
| [Definition registration](definition-registration-costs.md) | Complete phase including native work and joins was 691.679 ms. Sorting/scans are only part of that ceiling; no replacement implemented. | codex/overnight-definition-registration: f887863 / 0d4dedb |
| [HugsLib report](hugslib-enumeration-investigation.md) | Parked due scope correction, not performance rejection. Small activation passed; full original completed, full candidate unmeasured. | codex/overnight-hugslib-integration: 4e7f048 / d0ee269 |

These figures describe this small set of fixture runs, not a universal modlist
average. Stage savings are not interchangeable with complete startup savings.
The registration run's unusually slow 429.744676-second menu and 197.469-second
texture category reinforce uncertainty about first-read conditions; they do
not identify the cause or prove a DDS read-ahead benefit. Accumulated parallel
callback intervals must not be added as though they were sequential load time.

The owner narrowed the objective to improvements that generalize across
modlists, with compatibility for loading frameworks such as Gagarin allowed.
HugsLib work was parked; Muzzle Flash, MoodBar and GiddyUp-specific ideas were
not pursued as implementations. Earlier rejected inheritance/query splitting,
lookup batching, worker reuse and scalar-conversion experiments remain closed.
This is a stopping point based on the available evidence, not a claim that all
conceivable general optimizations or cold-cache opportunities are exhausted.

The first unresolved future measurement question is whether DDS read-ahead
helps first-read-heavy loading. Existing fixture commands do not control
Windows file residency, and more immediate pairs would mainly repeat warming.
The late 197.469-second texture category includes more than disk reads and is
not proof of a read bottleneck. Stop now rather than add cache-reset machinery
or globally evict operating-system caches. A future comparison needs naturally
comparable first-read conditions before claiming that benefit or rejecting it.

## Boundaries and evidence

One agent at a time owned the single checkout, package deployment and permanent
fixture. Research branches and prior evidence were preserved. The broader
medium/full live authority applied only to this idle overnight session; it
does not grant routine busy-PC launches or change historical workload limits.
Normal Steam, Workshop, profiles, saves and caches were untouched. There was
no UI automation, security change or forced closure during final restoration.

The linked focused records are historical snapshots of their own branch and
fixture state. This record and OP7_CURRENT_STATE identify the later restored
state. Private evidence lives in the canonical fixture captures and ignored
`artifacts/overnight-*` directories. Restoration deploy/prepare/launch logs and
portable summary are under `artifacts/overnight-closeout/`.

OP7 remains incomplete and OP8 inactive. Menu startup, semantic checks,
gameplay compatibility and performance are separate claims. No gameplay,
save-load, release or publication qualification is implied by this closeout.
