# Fresh loading discovery: target inputs required

**Input blocker resolved later in this chat.** The owner supplied an authenticated
official-depot refresh for build 26087487, version 1.7.4984 rev1242. The index and
assembled bundle checksums matched; reconstruction individually verified all
1,709 official base files, sizes and modes in a fresh directory. No DLC was
carried forward. Current-target work continues below; the original blocker record
is retained after it.

## Current-target checkpoint

Exact game and engine hashes are refreshed in `src/WakeUp17/Contracts.json` after
static review. All 212 guarded game methods retain equivalent decoded bodies and
signatures; 29 raw IL hashes change through metadata-token operands. The broader
review compared 942 selected game methods. Engine changes include a native
headless GPU-work skip and frame-rate/presentation handling; their effects belong
to vanilla. Windows byte equality and execution are unverified on this target.

A Core-only baseline with Harmony and the private bridge physically omitted
Wake-Up and all DLC. It verified loaded assembly hashes, exact package order,
three colonists, 600+ native ticks, save/reload into a changed Game object and
acknowledged normal exit zero. Launch to second idle entry update was 5.244 s,
new-game setup to first Playing 7.002 s, launch to Playing 12.250 s. OS caches
were warm from verification/copy; application profiles/XDG caches were fresh.
This single functional observation is not an acceleration comparison.

A separate English native diagnostic also passed. Its startup LoadAllPlayData
root was 2.552 s, including 0.898 s ParseAndProcessXML and 0.825 s definition
construction for 6,669 nodes. A separate main-thread callback root was 1.546 s.
These instrumented, overlapping parent/child observations are not additive
saved-time budgets or acceptance timings. Native save/reload markers were added;
neither run qualifies save-load speed. Both logs contain no ERROR lines; headless
and native shutdown warnings remain. The historical bridge build has four
nullable-flow warnings; the product builds with zero warnings/errors and all 67
synthetic checks pass against the current exact reference catalog.

Fresh bounded questions are linked child-list indexing in the new shared
deserializer and duplicate-target scans during translation application. Actual
cost, activation, equivalent behavior and repeated matched performance remain
to be measured. No new optimization is accepted at this checkpoint.

## First measured prototype

The pinned Core/VEF/Furniture/Plants French workload passed actual assembly,
constructor/owned-definition, 600-tick, save/reload and normal-exit checks without
ERROR lines. The linked-child-index probe covered all four new parser emitters:
141,201 calls occupied only 0.0063 aggregate seconds including timestamp overhead.
A larger explicitly retained-DLC/VEF/VPE/Furniture/Plants workload also passed;
330,122 calls occupied 0.0268 seconds. An XML child index is unwarranted on these
observations. The retained DLC files are unchanged owner-supplied data with old
metadata; this is a scale/compatibility workload, not refreshed official DLC
qualification or the personal full collection.

Duplicate translation scans occupied 0.1165 seconds on the Core public workload
and 0.4783 seconds on the larger workload. These original-loop diagnostics include
timer overhead and do not establish a whole-load gain. A stateful index needs
careful callback/mutation admission; the old enumerator-version argument remains
invalid. The first prototype instead keeps live native enumeration and reorders
pure field/string checks. It avoids key comparisons for inactive or nonmatching
entries, retains the null-self short circuit and null-other exception, and keeps
all native normalization, callbacks, first-match errors and field writes.

The prototype has one exact-body guarded rewrite, releases its hooks at startup
completion and defaults off (`translationPredicate17`). Existing true preview
flags do not select it. English does not install it. Seventy synthetic tests
pass, including the actual IL rewrite across order, alias, null, case-sensitive
path and live mutation cases. No performance or live prototype qualification is
claimed before the separate real-game comparisons.

The 21 October discovery task reached a game-input blocker before new live
profiling. No new loading feature or performance result is qualified. The task
prioritizes vanilla's shared loading machinery; public mod collections will be
stress and compatibility workloads. The earlier campaign's conclusion does not
close this new investigation.

## Target verification

At approximately 01:24 America/Toronto on 21 October, the
[branch-selected SteamDB metadata](https://steamdb.info/app/294100/depots/?branch=unstable)
identified unstable build **26087487**, updated 20 October at 19:03:48 UTC,
with Linux depot **294103**, manifest **8009297283328867975**. Public still
showed build 23969874. The
[official announcement](https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/)
corroborates using unstable for 1.7. SteamDB is a metadata mirror; the exact new
Version.txt, assembly identities and official acquisition provenance require the
actual payload. No product revision number is inferred from the build ID.

The restored environment's configuration ID matches the prepared environment.
Its historical 26031547 installation passed all 2,911 file size, SHA-256 and mode
checks, including equality of its three managed references with the retained
Windows references. Those bytes remain 1.7.4982 rev1280. The handoff's 302 payload
files and its manifest passed all 303 SHA256SUMS entries and the independent
manifest byte/hash checks. The clean checkout was synchronized to public
`e46e46578451c55509ea254ca766349517479c5b`; the new branch derives directly
from that commit.

Direct executor requests to Steam's news API, SteamDB and the SteamCMD metadata
service returned proxy CONNECT 403. Supported web research supplied metadata,
not game files. The selected environment exposes no configured secrets, runtime
variables or outbound identities for licensed Steam acquisition, and no TCP
destination grants. The retained setup also records an earlier anonymous
official-depot attempt with no entitlement and zero payload bytes; it was not
repeated. The known private input locations contain the historical payload.

The smallest input needed to resume is a legal private transfer of the current
Linux base game, including native apphost, bundled runtime/libraries, managed
assemblies, Assets.zip and Core data, plus Version.txt and official acquisition
build/depot/manifest and file-hash records. At this observation that means
26087487 / 294103 / 8009297283328867975. Recheck metadata when resuming. A complete
base depot allows a Core-only baseline; owner saves, a personal collection and
Steam credentials are unnecessary. Retained DLC files still have the previous
owner-Windows provenance and old metadata; they do not establish refreshed DLC
depot qualification. Network access alone would not provide paid-game entitlement.

## Work completed and next action

Two ordinary delegated investigations shared the restored filesystem: current
target/access research and controller review. The parent retained sole ownership
of builds, fixtures and games. No independent cloud chat or game was created.
The reviewed source built against historical exact references with zero warnings
and errors; 67 C# synthetic checks and four Python packaging checks passed.
An initial test compilation lacked the per-shell Harmony path; the corrected
run passed, and both logs are retained. These are environment and source checks,
not current-game behavior.

The smallest reusable functional baseline is the handoff's screen bridge and
controller, with target and absence assertions adapted after actual byte review.
It already supports a deterministic second idle entry frame, quicktest map,
600 native ticks, native save/reload into a new Game, retained colonists and
acknowledged normal exit. Review identified the following necessary adaptations:

- Add a prelaunch process guard and preserve existing fixtures.
- Verify exact loaded game/runtime/Harmony/bridge identities and physical Wake-Up
  absence; retire assertions for the previous definition/member optimizations.
- Keep native event timestamps and retain scheduling failures from the old
  controller's strict observation-lag limit.
- Add separate monotonic save/reload markers before making save-load speed claims.
- Review early native verbose profiling on the actual target. Old targeted hooks
  begin at a mod constructor and miss earlier loading/prefetch.

After acquiring the target, statically review changed assemblies and bridge
contracts without weakening exact admission. Then obtain an absent-Wake-Up
functional baseline early and profile startup, new-game creation and save loading
at separate boundaries. Shared discovery/pack access before mod construction,
definition construction and translation application remain measurement questions,
not accepted or measured new candidates. Historical inclusive phase durations
cannot be added into a removable-work budget. No new optimization prototype was
implemented or rejected in this blocked attempt.

Use the standard immutable full/subset snapshot and parent/child handoff workflow
for subsequent stress collections. The handoff's public VEF/VPE set is a small
defined workload, not the owner's full collection. Controller changes and new
candidate assertions must follow the actual feature and chosen collection.
Counted comparisons require repeated balanced absent-Wake-Up controls, declared
cache conditions, useful-work/equivalence checks and all slow/failing samples.

No live game, binary/tag/Workshop release or played-setup installation occurred.
New activation, compatibility, save/load and normal-exit results remain pending
the target. Final Windows behavior, graphics/audio playback and installation
remain owner-controlled. Private raw evidence is retained in the campaign's
ignored artifact directory. Plan balances, reset times and task-attributed token
usage were unavailable; no plan or monetary conversion is claimed.
