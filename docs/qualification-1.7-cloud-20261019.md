# Wake-Up 1.7 cloud loading investigation — 19 October 2026

This campaign is in progress. No reduced feature set or loading improvement is
accepted yet. Final Windows verification, played installation and release remain
with the owner.

## Target and evidence carried forward

The current public source parent is `6c5cc15743b4da4822f9c0021988de2ae1d5ed81`;
work continues on `codex/1.7-loading-reduction`, without private development ancestry.
Current [branch metadata](https://steamdb.info/app/294100/depots/) and
[Linux depot metadata](https://steamdb.info/depot/294103/) corroborated unstable
build **26031547**, Linux depot **294103**, manifest **4415223985052641865**.
Main remains build 23969874. The prepared game is **1.7.4982 rev1280** with the
exact reviewed Harmony rc.3. All 2,911 canonical files passed size/hash/mode
verification. SteamDB is a public mirror; retained acquisition records establish
the original official base-depot provenance. DLC data came unchanged from the
owner's installation. Exact runtime/build admission remains intact.

The older [Windows comparison](qualification-1.7-20261014.md) does not qualify
current-game speed. It rejects unchanged leaf-only and XML-search reuse as
benefit candidates; warm Gagarin also skips the XML work. Type-name reuse remains
inactive on the current metadata-update-enabled runtime. Existing current-game
Linux evidence supports bounded functionality and actual member reuse, not speed.
No on-demand, progressive-detail or postponed gameplay work is included.

## First product question

Compare the existing member-only implementation with Wake-Up physically absent.
The candidate DLL is `8a091c431ada4dc824d6ec8fc22b785a632b61d5001a32a3edc1a36318bc637b`,
originally built at `9d346a0`; product/build/1.7-package source is unchanged at the
public parent. Controls and candidates share Harmony, Core, all five DLCs and the
same separate test bridge. No personal mod snapshot is available. This is an
explicit official-content baseline, not the owner's full collection.

One delegated worker owns the new game/fixture/controller; the parent owns
product changes, builds and Git. Game runs are sequential. Each run has a fresh
profile and application cache; OS file caches are warm. Both arms use four logical
processors, the same heap cap and scheduling priority on the same cloud machine.
No builds or heavy investigations overlap counted runs.

The bridge uses the same fixed random seed and 100-cell map, verifies actual world
seed/tile/colonists, and records native monotonic timestamps for startup idle,
new-game start and first Playing. Startup idle follows a second idle entry frame,
after the candidate's first-frame cleanup. It is not a visible menu repaint.
Whole launch-to-Playing is the primary screen; nested intervals are never added
to that total. Each successful run also requires 600 native ticks, save/reload
into a new game object, retained colonists and acknowledged normal exit zero.
Declared warmups precede four forward/reverse counted pairs. All failures remain.

## English member-only screen

Four balanced counted pairs completed, with separate declared warmups. All eight
counted runs passed actual activation/absence checks, identical world seed/tile/map
and package order, 600 ticks, native save/reload and acknowledged normal exit zero.
The candidate reused 28,207 member lookups with 1,403 native lookups; its other
features were confirmed off.

| Boundary | Wake-Up absent median (range) | Members-only median (range) |
| --- | --- | --- |
| Startup idle | 24.377 s (22.775–31.782) | 24.398 s (23.279–76.939) |
| Launch to Playing | 34.550 s (32.413–49.824) | 36.830 s (32.282–91.957) |

There were two candidate wins and two losses. The median total was 6.6% slower,
but cloud variation prevents treating that as a causal product regression or
benefit. A 91.957-second candidate coincided with 38.1% aggregate host CPU steal,
compared with 6.1–10.2% in earlier counted runs; its cause is not established and
it remains in the results. Startup scope counters do not establish speed. No
feature is accepted from this screen. Native phase diagnostics are the next
bounded question before any speculative optimization.

## Records and usage

Private evidence and handoffs are retained under
`artifacts/cloud-reduction-20261019/`; new game/profile/run evidence is under the
permanent ignored `.rlo-test-instance/1.7/` lane. Existing fixtures are preserved.
Subscription readings, reset times and per-task token metadata are unavailable
through the exposed tools. Exact active model/reasoning settings are also not
exposed; the two ordinary subagents inherit the parent's settings. Elapsed work
and delegation are being recorded, without a token-to-plan or monetary conversion.
