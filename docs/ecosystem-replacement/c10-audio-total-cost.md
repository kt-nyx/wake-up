# C10 audio: full startup cost comparison, 12 September 2026

The final warm audio cache reached the usable menu **0.98–1.24 seconds earlier**
than the same workload with the early audio loader physically absent. This
includes the loader's own startup cost. Building the audio cache on first use
still delayed the menu by **0.42–1.06 seconds**. These are two matched comparisons
in opposite run orders, not a universal average or full C10 acceptance.

The initial comparison had a mixed menu result and is retained below. One bounded
correction removed unnecessary copies of privately owned audio data. Focused
correctness checks and a new complete comparison then passed. The final evidence
supports a useful net warm benefit for this workload, pending independent review.
Production adoption, broader feature coverage and release approval remain open.

## Scope and exact identities

This assignment began at clean `144741d943ea9cbc4f4e2c7bdcdd898153f5dd63` on the
preserved `codex/ecosystem-next-plan-review` ancestry. It extends the accepted
bounded functionality and incremental measurement in [the prior audio report](c10-audio-measurement.md).
That report and its review remain intact.

- Absent-control tooling and initial measured source:
  `a8e6c16c54189803d85789b224f8fa2a2e0d6733`.
- Final product source and matching core, observer and bootstrap packages:
  `a1651680bdaa5ce39d03cc5ff3dc0cdeecef9715`.
- Isolated GOG generation: `gog-rev573-20260910-194017-cf1a1eb0`.
- Frozen manifest SHA-256:
  `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
- Selection SHA-256:
  `377da6d13f853ab66bcf2fe369d30828902cb61d312e5b9288d9c9bf8366f7a4`.
- Private evidence: `artifacts/ecosystem-next-20260910/c10/audio-total-cost/`.
  Raw captures are under `.rlo-test-instance/results/<label>/`.

Performance ran under the owner's explicit `owner-unattended-20260912-173527`
grant, checked afresh before every launch. All RimWorld instances were already
stopped; no normal game was closed. Child work, builds and substantial scans were
idle during measurements. Every run used normal automatic exit and capture.

## What the four arms measure

The bootstrap is the small loader installed before ordinary mod startup so audio
work can be intercepted safely. The comparison separates its cost from caching:

1. **Absent:** bootstrap files physically absent, audio optimization off.
2. **Off:** bootstrap installed and active, audio optimization off.
3. **Cold:** bootstrap installed, empty prepared audio cache, ordinary first build.
4. **Warm:** bootstrap installed, prepared audio cache restored from `cold-01`.

All four retain the same WakeUp core and other feature settings. Within each
sequence, core and observer receipts, game generation, content selection, active
order, profile overrides and launch style match exactly. The selection contains
16 frozen entries; adding core and observer produces 18 active entries. Its
historical name, `c10-mp3-load-state-lifecycle`, does not imply synthetic decoding
in these measurements.

The existing workload uses ordinary `A_Place_of_Our_Own` song playback and
`VWE_Shot_LightSMG`, including the MP3 supplied by `tro.vwe.overhaul`, followed by
the deterministic colony/save/reload check. Launches use candidate mode,
`startup-searches`, the representative lane, muted 1280×720 at UI scale 1, and
`minimized-noactivate`. Measured runs have no diagnostic decoding, public-patch
sentinel, injected fault, readiness or advancement workload. Source hashing,
qualification, cache I/O, native startup and ordinary decoding/recording costs
remain inside the measured launch.

Both warm arms restore only `wake-up-PreparedAudio` from the same source-specific
`cold-01` capture. The audit checks that capture's SHA-256, cache inventory and
file hashes. Other cache restoration is absent. “Cold” means the prepared audio
cache is empty; it does not mean Windows' filesystem cache was flushed.

Absent admission rejects the bootstrap receipt, native DLLs, loader directory,
Doorstop files and inherited bootstrap environment. At runtime, the observer
checks the actual registry, managed bootstrap assembly, native module and native
startup argument: all four are false when absent and true when installed.
`bootstrapPresencePassed` reports this check. Absent runs contain no fabricated
positive bootstrap, preloader or native-shutdown acceptance fields.

Menu time comes from the independent usable-menu repaint observation. Presence
checks occur afterwards. Later colony/reload milestones include the respective
installed-bootstrap validation or absent-state check; they are supporting whole
workload endpoints, not isolated decoder timing.

## Retained initial result and bounded correction

Initial capture prefix: `c10-audio-total-`. The sequence was
`absent-01, off-01, cold-01, warm-01, warm-02, cold-02, off-02, absent-02`.
Each forward arm is paired with `absent-01`; each reverse arm with `absent-02`.

| Arm | Forward menu seconds | Reverse menu seconds | Change versus absent, forward / reverse |
| --- | ---: | ---: | ---: |
| Absent | 14.862292 | 14.924288 | — |
| Off | 15.388870 | 15.066201 | +0.526578 / +0.141913 |
| Cold | 16.138168 | 16.202563 | +1.275876 / +1.278275 |
| Warm | 15.151388 | 14.426070 | +0.289096 / −0.498218 |

The warm menu result was mixed and did not establish a net menu benefit. All
eight captures passed; none was excluded or rerun to obtain a favorable sample.

Read-only native review found no evidenced safe shortcut worth another experiment.
The qualified hooks, entry bridge and shutdown contracts remain unchanged.
The bounded managed correction instead removes concrete redundant copies:

- `FinishRecording` returns the already detached, validated recording. Captured
  bytes were already copied when recorded, and subsequent reader activity cannot
  mutate the detached result.
- The runtime transfers freshly decoded cache data to
  `PreparedAudioWaveStream.FromOwnedData`. The existing constructor still copies
  shared caller data defensively; only the private ownership route avoids copying.
- Encoding allocates the exact validated output capacity up front, avoiding
  buffer growth copies without changing serialized data or validation.

Full source hashing, cache keys/checksums, admission, audio arithmetic, fallback,
public-patch behavior and native lifetime rules are unchanged. The measured
sequences establish the final workload result; they do not isolate the precise
number of milliseconds caused by these copies, because old and new copy paths
were not interleaved in a separate experiment.

## Final full-cost result

Final capture prefix: `c10-audio-copy-`, with the same eight suffixes and order.
Positive changes below are slower than the physically absent control.

| Arm | Forward menu seconds | Reverse menu seconds | Change versus absent, forward / reverse |
| --- | ---: | ---: | ---: |
| Absent | 15.129315 | 14.966047 | — |
| Off | 15.268581 | 15.489448 | +0.139266 / +0.523401 |
| Cold | 15.546696 | 16.028539 | +0.417381 / +1.062492 |
| Warm | 13.888685 | 13.984058 | −1.240630 / −0.981989 |

The warm menu reduction is 8.20% / 6.56% in these two pairs, after paying all
bootstrap startup cost. Warm colony readiness is 1.630817 / 1.071125 seconds
earlier, first playback progress is 1.630942 / 1.077764 seconds earlier, and the
complete gameplay/save/reload endpoint is 2.346614 / 1.387519 seconds earlier.
These milestones all begin at launch and overlap: do not add their savings.

Cold colony readiness costs +0.170409 / +1.091195 seconds. Cold complete workload
changes are −0.203938 / +1.103213 seconds, a mixed result. Installed/off later
milestones also vary in direction. Neither establishes a separate speedup.
Synchronous first-use calls remain around 1.7–1.8 milliseconds in the final
sequence; no useful isolated first-use gain is claimed.

Both sequences retain the same enabled-arm counters: 302,719,162 source bytes
hashed through 106 native full-content hash calls and zero managed hash calls.
Cold has 101 recordings/decoder constructions, 5 cache hits and 103 misses.
Warm has 108 hits, zero misses and zero ordinary decoder constructions, with
28,029,356 cached bytes. Warm MP3 has one native frame, 58 cached frames, zero
replay frames and a skipped index scan; the selected OGG stream uses its cache.
No replay mismatch, cleanup failure or MP3 completion failure was recorded.
Zero counters in absent/off describe inactive instrumentation, not zero ordinary
audio work.

## Correctness, restoration and remaining scope

The focused final check passed **90 managed audio tests and all 74 Python tooling
tests**. The exact transcript and backward-seek test covers both constructor
routes; existing recording-freeze coverage remains. Two focused absent-admission
tests also passed. All package builds succeeded without managed warnings/errors.

Four functional captures passed: `c10-audio-total-functional-{absent,off}-01`
and `c10-audio-copy-functional-{cold,warm}-01`. The final pair retained natural
format equality, public patch settlement/Unpatch, native shutdown and colony
save/reload checks. Existing adequate broader audio controls were not repeated.
All **20 captures passed**, including all 16 measurements; no samples in this
assignment were excluded. Earlier reports retain their own failures/exclusions.

The private audit verified six package receipts, 24 payloads, 271 captured
observer files and 314 cache-source files. All 28 transaction rollback receipts
exist. Seven baseline hashes and ten expected absences match after restoration;
no RimWorld or texture-helper process remained. `run-summary.json`,
`comparisons.json`, `package-audit.json` and `restoration.json` retain the details.
The closeout handoff and evidence manifest identify the later documentation
commit separately from the tested product/package source.

This is the bounded audio full-cost handoff for independent review. It does not
complete C10 textures/graphics/icons, readiness/advancement qualification,
production bootstrap adoption/distribution, combined release acceptance, Windows
Steam promotion or Linux/Deck validation. No normal installation was modified,
and nothing was merged, pushed or published.


## Steward review: net warm benefit retained; C04 qualification next

Stopped `94c1e0ad884d08050b2195038a4ddc43c67f55b3`, final tested
`a1651680bdaa5ce39d03cc5ff3dc0cdeecef9715`, passes bounded independent review.
The new ownership route receives freshly decoded private cache data with no
surviving alias. Recording is detached under its owner lock; capture already
copied source buffers, so later reads/disposal cannot mutate the returned data.
Other constructors still copy shared inputs. Validated two-MiB limits make exact
capacity calculation safe, with unchanged serialization and cache identity.

The genuinely absent control and both eight-arm comparison identities were
checked. All 116 private files, 21 external report/run references, six receipts,
24 payloads, 271 observer files, 314 source-cache files and 28 rollback receipts
match their hashes. Twenty actual run manifests/flags and normal exits agree with
the report. The parent recalculated the paired menu deltas and checked independent
menu endpoints, source/package/settings/order/profile identity and cache provenance.
Seven current baseline hashes and ten absences match; no game/helper process
remains. Independent source and measurement reviewers found no blocking defect.

The net warm menu savings of 1.240630/0.981989 seconds are supported for this
focused 16-entry selection plus Wake-Up and observer: 18 active entries. The
previous incremental report's 312/315 workload claim was incorrect and is now
corrected against every original run manifest. Frozen manifest inventory is not
active selection. No full-modlist or universal percentage follows from either
report. Later gameplay endpoints remain supporting observations because they
include different installed-versus-absent validation work after menu observation.

Cold startup penalties of 0.417381/1.062492 seconds and installed-disabled overhead
of 0.139266/0.523401 seconds remain explicit product costs; this review does not
approve them as release tradeoffs or close their qualification. No isolated
first-use gain, readiness/advance performance, broad non-audio coverage, production
distribution/adoption or combined acceptance is established. Those obligations
stay with the same C10 owner. Only the bounded data-ownership change and this
warm-workload result are retained; C10 is not functionally complete or fully accepted.

C10 now stays stopped while the steward uses the current unattended window for
outstanding C04 language-data performance with its original orchestrator. This
is a serial measurement interlude, not permission to skip C10 or start C11.
The feature sequence C10 completion, C11, C14 and C15 remains unchanged. The
owner window continues until return/revocation; normal game/data and platform
boundaries remain intact. Private review: `artifacts/ecosystem-next-20260910/c10/audio-total-cost/steward-review.json`.
