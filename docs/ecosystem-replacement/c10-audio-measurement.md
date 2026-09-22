# C10 natural audio measurement — 12 September 2026

Warm audio caching now saves about 1.5 seconds at menu entry in the isolated GOG
workload after a focused correction to source-file hashing. The original measured
implementation was slower with both an empty and a populated audio cache. It
still read and hashed every source byte using managed SHA-256, even when cached
audio avoided decoding. Using Windows' SHA-256 implementation for those same
bytes removed enough cost for warm caching to help in both comparison orders.

The revised first-build result is mixed; it does not establish a cold speedup.
The small synchronous calls that start selected playback also show no meaningful
gain. These results qualify the bounded audio comparison for independent review;
they do not accept all of C10 or establish combined-release performance.

## Permission, identities and comparison scope

The steward relayed the owner's unattended grant at `2026-09-12T17:35:27Z`, window
`owner-unattended-20260912-173527`, continuing until owner return or revocation
with no fixed cutoff. No revocation was received during these runs. Public-patch
functional work finished first. All game measurements used `--purpose performance`
and the single GOG generation `gog-rev573-20260910-194017-cf1a1eb0`. All RimWorld
instances were already stopped before each launch; no normal process was closed.
Child agents remained stopped, and builds, tests and source scans were idle during
measurements. Each game exited and captured normally before the next preparation.

The assigned base was `71eb505200e30e11b5a42b0ff51d5b937861faf9` on the preserved
`codex/ecosystem-next-plan-review` ancestry. Matching core, menu-observer and native
bootstrap packages were built and deployed separately for each tested source:

| Role | Exact package source |
| --- | --- |
| Supported public patch and same-file functional result | `c3610d2e466115d9a5cc7d1cedb13d2c82cd938f` |
| Original valid matched measurement | `9ec60bdaef1d4105536c7c82338e960ec285582b` |
| Revised hashing, functional checks and final measurement | `a9e8a3ab717ac89947539789b33b0fc07f4a7f9d` |

All arms within each comparison used identical package sources, mod order,
selection, user settings, profile overrides and minimized nonactivating launches.
The frozen manifest is
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
The focused selection contains 16 frozen entries; the candidate and observer
bring the launched active count to 18. The larger frozen-manifest inventory was
previously mistaken for the launched selection; the steward corrected this from
all 17 captured run manifests on 12 September. Exact selection, package
receipts, content/order and captured observer hashes are in the private audit.

The three arms are audio **off**, **cold** (no prepared audio carried in), and
**warm** (only prepared audio copied from the matching successful cold capture).
These names describe our audio cache, not the Windows file cache. We did not flush
Windows' file cache. Both warm runs started from the same cold capture rather than
progressively warming the previous warm run. Other cache sources remained equal.

The early bootstrap and native companion stay deployed in every arm, including
off. This isolates the incremental audio route; it does **not** measure their
shared overhead against a bootstrap-absent installation. That obligation remains
separate, as do other C10 features and later combined-candidate measurements.

## What the workload measures

The independent menu observer supplies `menuReadyObservation.elapsedSeconds`.
The new sparse observer then follows the existing deterministic 75-by-75 colony
with three colonists through normal gameplay, save and reload. In that colony it
starts the authored `A_Place_of_Our_Own` song through the normal music manager and
`VWE_Shot_LightSMG` through the normal one-shot sound manager. It checks that both
advance on a later frame. The song uses the ordinary streaming route; LightSMG
uses the naturally loaded MP3 clip. Natural startup includes the other admitted
OGG/WAV sources. The measurement selector neither recognizes assets in product
code nor changes supplier admission rules.

The fixture is muted at 1280-by-720, scale 1. This verifies playback state, not an
audible listening assessment. Sparse counters describe normal loading and first
use. Reference decoding, sample hashing, injected failures, repeated diagnostic
decodes, readiness/advance experiments and public-patch stress controls are absent
from measurement runs. Cold recording, provider/math qualification, source hashes,
cache I/O and ordinary startup work remain inside the measured process.

All elapsed milestones use the launch clock. Columns below are overlapping
milestones, not durations to add together. The first-use column is the synchronous
time spent calling the two normal playback consumers; it excludes earlier loading
and is distinct from the later observed playback-progress milestone.

## Original implementation: useful cache hits, negative total result

At source `9ec60bda`, the exact sequence was:

| Capture suffix (`c10-audio-measure-`) | Menu seconds | Gameplay and reload complete seconds |
| --- | ---: | ---: |
| `off-03` | 15.527782 | 20.505344 |
| `cold-02` | 18.062870 | 23.026341 |
| `warm-01` | 16.292500 | 20.871419 |
| `warm-02` | 16.032007 | 20.577693 |
| `cold-03` | 18.244879 | 24.216989 |
| `off-04` | 15.384086 | 20.470959 |

Forward and reverse cold menu penalties were 2.535088 and 2.860793 seconds; warm
penalties were 0.764718 and 0.647921 seconds. All six runs passed every required
audio/bootstrap/preloader/native-shutdown/gameplay/automatic-exit flag. This is a
negative result for that implementation and workload, not a failed measurement.

Warm counters nevertheless showed 108 cache hits, no prepared-reader decoder
constructions, 28,029,356 cached bytes and 58 cached MP3 frames with one native
qualification frame and no replay. Every enabled run still hashed 302,719,162
source bytes. Inspection found `PreparedAudioCache.SourceKey` doing those reads
through `SHA256Managed`. That concrete remaining cost justified one revised
experiment rather than repeating the losing route unchanged.

## Focused product correction and correctness checks

`PreparedAudioSourceHash` streams every byte from the already-owned source through
64 KiB blocks. On supported Windows it uses the same SHA-256 primitive as the
existing cache digest code, with one hash object per source. It releases that
object in `finally`; an unavailable native provider falls back to explicit
`SHA256Managed`. Hash errors remain optional-cache failures. Neither route asks
`CryptoConfig` for a custom provider. The outer source-key code still restores the
original cursor and refuses nonzero initial positions. No path reopening,
timestamp shortcut, content sampling, cache-format change or supplier exception
was introduced. The source identity still includes the full digest, byte count,
runtime identity and audio format.

The native API follows Microsoft's
[BCryptCreateHash](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcryptcreatehash)
and [BCryptHashData](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcrypthashdata)
contracts: block updates produce the digest of the concatenated input, and the
owned hash object is released after use.

The 51 focused managed tests passed, including empty and block-boundary inputs
through both hash routes, the unchanged pre-acceleration source key, source
ownership/cursor behavior and existing cache failure/data checks. Two broader
72-test tooling invocations each encountered one Windows access-denied rename in
unrelated temporary generation-transition tests. Those failed invocations are
retained; both individual failing tests passed on isolated retry. No tooling fix
or claim of a completely clean broad invocation follows from those retries.

Matching source `a9e8a3ab` then passed live functional captures
`c10-audio-hash-functional-cold-01` and `c10-audio-hash-functional-warm-01` before
new measurements. Natural formats, public patch/Unpatch compatibility, gameplay
save/reload, bootstrap, preloader and native shutdown all passed. Functional
timings are diagnostic only. Earlier supported public-patch and real same-file
settlement evidence remains separately documented in the
[loader-race record](c10-loader-race-design.md).

## Revised implementation: warm benefit, first-build cost remains

The final source `a9e8a3ab` used the same forward/reverse order and workload:

| Capture suffix (`c10-audio-hash-measure-`) | Menu seconds | Colony ready seconds | First-use calls ms | Gameplay and reload complete seconds |
| --- | ---: | ---: | ---: | ---: |
| `off-01` | 15.850989 | 17.945704 | 1.7349 | 20.803278 |
| `cold-01` | 15.801303 | 17.909373 | 1.8147 | 20.943965 |
| `warm-01` | 14.318074 | 16.268823 | 1.7956 | 18.839872 |
| `warm-02` | 13.971240 | 15.951671 | 1.6799 | 18.487970 |
| `cold-02` | 16.169840 | 18.311758 | 1.7068 | 21.186007 |
| `off-02` | 15.438361 | 17.569751 | 1.7319 | 20.426936 |

Compare each forward arm with `off-01`, and each reverse arm with `off-02`:

- Warm menu entry saved **1.532915 and 1.467121 seconds**, or **9.67% and 9.50%**.
- Warm colony entry saved 1.676881 and 1.618080 seconds. First observed playback
  progress was 1.672655 and 1.625568 seconds earlier from process launch.
- Warm gameplay and reload completion saved **1.963406 and 1.938966 seconds**.
- Cold menu deltas were **-0.049686 and +0.731479 seconds**; cold gameplay/reload
  deltas were **+0.140688 and +0.759072 seconds**. There is no demonstrated cold
  speedup. Cache construction and qualification remain real first-build costs.
- The roughly 1.7–1.8 ms first-use calls do not show a useful isolated improvement.
  Earlier playback from launch chiefly reflects work removed before colony entry.

Every enabled arm still hashed exactly 302,719,162 source bytes, now through 106
native hash calls and zero managed calls. Cold arms recorded 101 readers, had 103
misses and five within-run hits, and performed the natural MP3 scan/publication.
Warm arms had 108 hits, zero misses, no prepared-reader decoder constructions,
28,029,356 cached bytes and one skipped MP3 index scan. Their MP3 frame counters
remained one native frame, 58 cached frames and zero replay frames. Replay mismatch,
cleanup and MP3 completion-error counters were zero. Off counters describe an
inactive instrumented route; they do not mean ordinary audio performed no work.

All six final runs passed all six required qualification flags and exited with
code zero. This establishes a bounded warm benefit in two orders on this Windows
GOG workload. It does not establish a universal percentage, a first-read Windows
cache result, long-session playback benefit, Linux behavior or combined-release
acceptance. The original and revised sequences are separately controlled; their
difference is not a standalone matched benchmark of the hash primitive itself.

## Exclusions, private evidence and restoration

Three setup captures remain separate from the twelve matched performance runs:
`c10-audio-measure-off-01` failed selected playback because the measurement selector
had not inherited the mute profile; `off-02` passed with the preceding `f21b4c47`
packages and corrected tooling but is not part of the later matched set;
`cold-01` passed measurement/gameplay but failed preloader observation because its
admission check did not yet recognize the measurement selector. The corrections
preserved existing guards and restarted the full comparison at `9ec60bda`.
The initial observer build/diagnostic-string failures are also retained.

Private evidence is under
`artifacts/ecosystem-next-20260910/c10/audio-measurement/`: `window.json`,
`operations.json`, `run-summary.json`, `comparisons.json`, `package-audit.json`,
build/test logs, `audit.py` and `restoration.json`. Captures remain under the
ignored fixture results directory. The audit verifies 17 captures (15 with every
flag passing), nine package receipts, 36 package payloads and 242 captured observer
files. It also checks equality of the comparison identities/settings/order.

All 26 measurement-assignment transactions were rolled back in reverse active
order. Seven baseline hashes and ten required absences matched afterward, with
no RimWorld or texture-helper process remaining. The earlier public-patch folder
separately records its nine restored transactions and original functional audit.
Normal installations/data were untouched. The final handoff manifest records the
closeout source and stopped child state; independent review and the remaining C10
obligations belong to the steward. No merge, push, publication or platform
promotion was performed.


## Steward review: bounded warm benefit verified; complete bootstrap cost next

Independent functional and measurement review passes the bounded hashing and
public-patch increments at stopped `7206939bdbeb443c0eb8b8a91a4894de1073ef94`.
Final tested/package source is `a9e8a3ab717ac89947539789b33b0fc07f4a7f9d`;
the exact supported public-patch/same-file source is `c3610d2e`. Source review
confirms full-content SHA-256 and unchanged cache keys, borrowed-stream ownership,
cursor restoration and explicit managed fallback. The public patch really executes
and unpatch restores behavior; a pending MP3 decoder settles before the factory.
The observed same-file wait is the synchronous notification/registry route, not
proof that every native publication race is exercised by that capture.

The parent independently verified all 137 handoff artifact hashes; the audio
nine receipts/36 payloads/242 observer files; public six receipts/24 payloads/46
observer files; 21 actual run manifests and reported flags; both sequences'
source/package/content/order/settings/profile/launch identities; matching cold
cache provenance for both warm arms; and the paired timing arithmetic. Seven
restored baseline hashes and ten absences match. Fresh OS inspection found no
RimWorld or texture-helper process. Final tested source to handoff changes only
this report. Existing setup failures and two broad tooling rename failures remain
retained; isolated retries do not erase those failed invocations.

The recorded 1.532915/1.467121-second warm menu benefit is supported for this
workload with the bootstrap shared by every arm. Cold cost remains mixed, including
the 0.731479-second reverse-order menu penalty. No useful isolated first-use gain
is established. Readiness and advance preparation are disabled by the measurement
selector; their performance remains unqualified. Neither the warm result nor
correct output establishes full audio/C10 acceptance or supplier removal.

The same C10 owner next measures the missing complete deployment cost: a genuinely
bootstrap-absent/audio-off baseline versus bootstrap-installed/audio-off and
installed cold/warm audio, with all other product settings and content held fixed.
Use the existing sparse workload and forward/reverse controls. The comparison
must physically omit the early injector/native startup argument in the absent
arm, not merely make an installed bootstrap return early. Reuse exact matching
product builds and existing functional evidence, with a focused absent-arm check.
Report installed-disabled overhead, net cold/warm menu/colony/reload cost and any
first-use movement separately. Preserve all results and correct concrete costs;
no timer/platform or new profiling project is needed. First-build cost remains
part of the product decision, not hidden by a warm-only percentage.

The owner window `owner-unattended-20260912-173527` remains active until return or
revocation. Serial GOG measurements and normal exit/capture continue under its
limits; no normal process may be closed to make the host idle. Broader textures,
graphics/icons, production distribution/adoption, readiness/advance measurements
and combined release remain open. No C11 successor or production backend adoption
follows from this bounded review.

Private parent review: `artifacts/ecosystem-next-20260910/c10/audio-measurement/steward-review.json`.
