# Wake-Up 0.4.0

Corrected 0.4.0 improves cooperation with other loaders and fixes two compatibility
defects also present in 0.3.0. The owner authorized Workshop/GitHub publication
after a targeted check of the exact release DLL on isolated Windows. Linux/Deck remains outside the tested scope. See [current state](current-state.md) for
publication completion. The older `c36e9a7d` archive contains the defects and is
not this corrected release.

## Corrected compatibility defects

When another mod rewrites an XML worker during loading, Wake-Up checks the incoming
instructions before changing them. A changed worker now passes through untouched,
disables only its affected operation and reports a persistent notice. This avoids
the YaOpt error caused by Harmony publishing its new patch record only after
running the rewrite callbacks. Independent operations remain available.

Retired inheritance optimizations no longer remove RimWorld's native
`XmlInheritance.Resolve` call or alter `ResolveXmlNodeFor`. ASF's
PostInheritanceOperation hook can attach normally. The active processed-XML
bridge remains guarded; there is no ASF-specific bypass.

All nine new offline regressions pass, including both supplier installation orders
and complete assembly rewriting. Wider runs each recorded 50 passes and one
intermittent notice-acknowledgement persistence failure; isolated reruns passed,
but that storage issue is not claimed fixed. The Release build passed.

The exact release build `3ac8973a` reached the independently observed menu with
Wake-Up before YaOpt 1.1.4, Image Opt 0.1.13 and full ASF at
`31ee88b46651cfbe37da3f89620bc0b907165cc0`. Expected XML output, all eight YaOpt
worker hooks and ASF's inheritance hook were present. All ten displayed notices
were acknowledged; normal exit and automatic/compatibility checks passed. Neither
reported patching error nor an exception/XML-loading error appeared. Two private
fixture dependency-link warnings, Mono fallback diagnostics and Unity shutdown
allocation statistics remain. This is not a warning-free or visual texture test.
The live admission already saw installed YaOpt hooks; the first in-progress
Harmony rebuild interval is covered by offline regressions.

No performance or colony test was rerun on this corrected DLL. The following
measurements and broader gameplay evidence belong to their named earlier builds.

## What changed

Wake-Up now tracks availability separately for individual loading operations.
Another mod can own one operation while unrelated Wake-Up work remains enabled.
New conflicts appear in one persistent window; acknowledgement requires the
entries to have been displayed. Saved preferences are preserved, and a materially
different decision gets a new notice. Supplier safety advisories are distinguished
from a Wake-Up operation being disabled. Current-launch diagnostics remain useful
after a popup has been acknowledged.

Exact integrations cover the inspected FGL editions, YaOpt, WOWGAG, Missile Girl,
FastLoader, Image Opt, Loading Progress and related display/framework interactions.
The [compatibility guide](compatibility.md) contains the complete audited mod/tool
matrix, explicit setting choices, supported subsets and live evidence limits.
There is no unconditional new external-mod blacklist, foreign patch removal,
automatic foreign-setting change or speculative support for unavailable builds.

Loading Progress keeps its screen and repaint improvement. Its selected native
completion mode can cooperate with loading-only background work. RimThemes keeps
its active custom loader. No Modlist on Loading keeps the official-content panel
unless the user explicitly chooses to hide the whole summary. Image Opt can
provide completed textures to Giddy-Up's optimized readback without Wake-Up waiting
on its workers or taking ownership of their texture lifetime.

FastLoader's native image misses can use Wake-Up's optional cache, while successful
supplier raw-cache hits bypass it. Prepared-quality replacement yields when the
supplier can reuse atlas output. DefLoadCache's unsafe source-skip/patch-skip
combination receives an advisory. Hyperdrive's constructor eligibility checks run
before Wake-Up adds relevant observation hooks.

Default/search-only startup no longer inventories and locks unused cache families.
Cache ownership is acquired when the cache is actually used. A final narrow fix
also avoids asking Harmony to remove an XML worker patch that Wake-Up never tried
to install; that request unnecessarily rebuilt supplier methods. Actual attempted
installation failures still remove their own partial state.

## Historical measured startup results

These are complete times to the independently observed usable menu, not added-up
stage timers. A is the supplier alone; B adds Wake-Up. The modest real content
collection uses Vanilla Expanded Framework, Succulents, MoreFloors and Erin's
Hairstyles 2. It exercises 300 image loads. The separate Giddy-Up collection
exercises 392 real readbacks over 9,831,936 source pixels.

Runs used fixed English, matched collections/order/settings/observer within each
block, normal automatic exit and no diagnostic verification readback. Windows file
caching was warm and uncontrolled. Empty mod-cache construction is not a cold
operating-system cache. Preparation, cache transfer, capture and shutdown are
recorded separately and are not included in menu time.

| Comparison | Observed result | Tested core |
|---|---|---|
| Original FGL 1.6, ordinary Wake-Up | 0.12–0.27 seconds added; no net gain | `fe01a286` |
| FGL Continued, ordinary Wake-Up | One pair 0.21 seconds faster, the other 0.43 slower; no repeatable gain | `fe01a286` |
| YaOpt, ordinary Wake-Up | 0.11–0.20 seconds added | `fe01a286` |
| FGL Preview, ordinary Wake-Up | 0.19–0.38 seconds added | `4f49c2ae` |
| WOWGAG, ordinary Wake-Up | 0.13–0.38 seconds added | `4f49c2ae` |
| Loading Progress, ordinary Wake-Up | 1.00–1.89 seconds saved across ABBA and reversed BAAB | `4f49c2ae` |
| Image Opt + Giddy-Up, ordinary Wake-Up | 0.06–0.24 seconds saved; too small for a substantial startup claim | `4f49c2ae` |
| FastLoader native misses, optional image-cache construction | 2.20–2.73 seconds added, with 300 actual writes | `4f49c2ae` |
| FastLoader native misses, optional warm image cache | 0.56–0.61 seconds saved, with 300 actual hits | `4f49c2ae` |

Forty runs at `4f49c2ae1fffe296697d67a80b9213742621ffdf` cover the first
final-candidate screen. After the narrow worker-refusal correction, twelve further
ABBA runs at `fe01a28669a99713b1764a6947d1522516bdee54` recheck the three
affected suppliers. Their earlier results remain retained: original FGL added
0.41–0.47 seconds, Continued 0.41–0.60 and YaOpt 0.26–0.38. Distinct time windows
prevent attributing the full change to the correction. Preview admitted every
worker; WOWGAG yielded before installation; the other measured integrations did
not use the corrected refusal path. Their measurements remain identified with the
earlier DLL rather than being presented as new-binary samples.

An earlier 40-run screen on a different candidate found no incremental Gagarin
benefit and modest optional native warm-image savings with a first-build cost.
Those results remain historical, not final-DLL claims. Do not add gains from
different workloads or combine image-cache savings with an overlapping default
improvement. These small collections do not predict every player's mod list.

## Historical correctness and remaining limits

The final combined GOG collection includes Loading Progress, PurePatcher,
Kingfisher, Image Opt, Giddy-Up and the real content above. It checks actual
rewritten-assembly reload, expected XML output, supplier display ownership,
retained operations and native ready-texture reads. The gameplay check creates a
75-by-75 colony with three colonists, advances simulation, saves and reloads, and
compares the resulting state and native pause behavior. VEF's additional map
callbacks keep the affected background-map path refused; ordinary map loading
remains available. Save/world/colony background paths retain their own decisions.
The final `ov-fixed-combined-ack` and `ov-fixed-combined-game` captures use core
`fe01a286`; both exited normally and passed automatic acceptance. The gameplay
receipt records tick 601 before saving and 663 after the observer's 60 deliberate
post-reload ticks. Immediate native paused completion is checked separately at
saved tick plus one, with matched map/colonist state, stable paused frames and
normal resumed speed.

Separate focused tests cover actual unfocused completion and preference changes,
renderer cancellation/retry, native deferred-callback failure and subsequent save
load, the official-content panel, exact supplier raw hits, DDS/PNG source changes
and removal, ordered XML Extensions results/mutation, and one-time notices.
The [compatibility guide](compatibility.md#live-evidence-and-its-limits) names the
runs and distinguishes newly executed, reused historical and source-only evidence.
Every unsuccessful attempt remains recorded; forced recovery is never a pass.

The final worker change passed 82 focused managed checks, including unchanged
foreign publication on refusal and cleanup after a real attempted-patch failure.
An initial run's four XML Extensions failures were a missing test-reference path;
the complete rerun with the explicitly selected dependency passed all 82. Earlier
accepted feature-specific reviews and checks remain identified with their changes.
The final candidate is not universal gameplay certification and does not qualify
all combinations of individually tested suppliers.

## Package identity and publication boundary

The tested corrected core build is `3ac8973a2aee7879c0e184bd124d911198018875`; the observer
is `4f49c2ae1fffe296697d67a80b9213742621ffdf` and is not shipped as a runtime mod.
The release manifest records the actual core DLL hash, this build revision and the
later documentation/source-archive revision separately. Packaging reuses the
tested DLL unchanged; the stable assembly version remains 1.0.0.0, while the
distribution version is 0.4.0.
Its SHA-256 is
`ccfa3f0a6777856120a26181ada1ba642bbe2925fcee5d03702f45e0f67614c6`.

The optional unchanged Windows x64 helper is reused from the verified source-inclusive
0.3.0 archive: SHA-256
`ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`, original
helper source `83a2930f60967eba4fcfb8e6acef10f35a415c62`. Matching helper source,
licenses and notices accompany it. The core source archive includes tracked
development tooling and tests, but no game references, observer runtime package,
private profiles, saves, mod content or raw captures.

The owner authorized this corrected package's source push, tag, GitHub release
and content-only Workshop update through the [release procedure](release.md#source-history-and-publication).
Preserve the separate public history, live Workshop description and unrelated
listing fields. Publication does not qualify Linux/Deck, and no further
game launch or rebuild is part of this authorization.
