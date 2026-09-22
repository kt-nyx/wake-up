# Ordinary entry and final local candidate — 10 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent steward acceptance

The steward reviewed clean handoff `9772af1f1d8f0bda6f710f00f4cedd9f0d592548`, actual manual-run hashes/arguments/deployed identities, UI action history, focused validation and an independent read-only package review. Accept the exact `8ca0dd7` package as a bounded local Windows candidate and the three ordinary-entry controls within their stated scope. No material package or private-control blocker remains. Shared InputLegacy/engine risk remains unresolved; stable public release readiness is not accepted. The [final closeout](overnight-closeout.md) records the outcome and paused timer. Dated pending-review wording below and inside the immutable package describes its pre-acceptance checkpoint, not the current steward status. No publication is authorized.

## Result and release status

The accepted retained core now has ordinary screen-driven colony/save evidence
with normal logging. New-colony creation, gameplay, saving and reloading passed
without the private observer. A second manual load passed with the observer
present but its gameplay automation disabled. Prepatcher remained required and
active. This closes the absence of an actual user-path observation; it does not
prove the shared engine/bootstrap error repaired or establish universal gameplay
compatibility. The final package passed focused checks, independent contents/code review and an
observer-free native load. Steward review remains pending. No publication is authorized.

The distribution is **0.3.0-rc.1**, a locally reviewable candidate, not an approved
release. Public 0.2.1 remains the baseline. `ordinaryEntryObserved=true` records
bounded evidence; `ordinaryEntryQualified=false`, `releaseReady=false` and
`steamQualified=false` reserve formal acceptance for the steward. No diagnostic
suppression is shipped. All retained capabilities and omissions in
[retained-features.md](retained-features.md) remain unchanged.

## Exact base and controls

Dispatch `wake-up-ordinary-entry-package-76e10a0` started from exact clean
`76e10a06a0e52a4723c135eedfc2b971f7651a1c` on
`codex/ordinary-entry-package-76e10a0`, in the sole checkout. No RimWorld process
was present. The owning task alone operated the fixture; bounded subagents
researched the bootstrap boundary and edited assigned documentation/private
manual-save controls. No second checkout/fixture or normal game/data was used.

| Layer | Identity |
|---|---|
| Accepted runtime source | `d584eb36aa68cd374a26621f97295dbb63626a33` |
| Accepted DLL SHA-256 | `59ed42a69895930e8c5cfe04c903005f925cdf063beb02ffe6329244faac8825` |
| Private observer source | `41f50d5019950ec678131f138cff3d520320d776` |
| Observer DLL SHA-256 | `2e5433c950f3f7429b118f7f04fcdf050bb786c4803bccc782f7f251e8fdf4b8` |
| Active Steam generation | `steam-rev590-20260910-020604-cb37ef1b` |
| Frozen manifest SHA-256 | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93` |
| Managed game SHA-256 | `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |

All live commands use only `.rlo-test-instance/game/RimWorldWin64.exe` and the
fixture profile through `scripts/fixture.py`. All are functional runs, not speed
measurements. Native logging is unchanged: no gameplay/world probe,
`--fixture-functional-probes`, suppression selector, root lookup or input preload.
The first control's only game argument is the fixture save-data-folder override.
Manual exit/capture is explicit, so these runs have no automatic-pass claim.

## Actual user-path evidence

| Run | Screen path and observed result |
|---|---|
| `oeq-native-ui-01` | Observer excluded. New colony → Crashlanded → Cassandra/Peaceful/reload-anytime → world seed `danielle`, 50% coverage → random temperate-forest mountainous site (Penguin Palm Refinery) → inactive ideology → three default colonists → Start. Native 250×250 map appeared with Leon, Bruce and Coaks. Pawns moved; normal Save wrote `New Arrivals1`; Quit to main menu → Load game restored the same map/colonists and gameplay. Quit to OS, exit 0, complete capture. |
| `oeq-observer-ui-02` | Same accepted DLL, observer present in menu-only mode. Load game → verified `RloFixtureSmoke` from `stq-save-workaround-04`. Native 75×75 map and Ripley, Flossie, Benski appeared; pawns moved and ordinary menus responded. Quit to OS, exit 0, complete capture. No gameplay automation or logging workaround was selected in this consuming process. |

The exact final rebuilt DLL also passed `oeq-final-ui-03`: observer absent,
ordinary defaults, native Load game of the same verified 75×75 save. The game's
mismatch dialog listed only the deliberately absent Fixture Menu Observer;
**Load anyway** preserved that selection. Map, all three colonists, visible pawn
movement and menus worked, then Quit to OS exited 0. Run SHA-256:
`37b815708a5069bc3d13f3ad6fe989a6abce6f898b1cfa92a77a60614f8d0b88`.
Its native alert-search receipt again records 125 avoided scans, eight native
predicates and zero retained types. One InputLegacy error remained; ordinary
logging stayed intact. None of the three current controls failed or required
forced closure; earlier failed captures remain explicitly recorded above.

The first captured save is 9,354,584 bytes, SHA-256
`28bc947a0b568fa65fe63a08c485326ff67b3c6af7f97319905afcf8dbd7e30a`, tick 471.
The second source save is 1,877,412 bytes, SHA-256
`0852204990cfffddf9c3e57b582c51b90137ff242a9d521fa29774e7ad6adefb`, tick 601.
Its original creation used the private workaround; the current manual load did
not. These are fixture-created saves, never imported normal-user data.

Run-record SHA-256 values are respectively
`e418c3cd490da69cce208eb3f94164f3104d44b8e700b447475ee9c479f60754` and
`6c4a047e606a1404ab458d2518a8594bc929262edc0421c5f2998da587998754`.
Full paths, arguments, package receipts, content order, save/log hashes and
native search receipts are in
[run-evidence.json](../../../artifacts/ordinary-entry-package/run-evidence.json).
UI observations are preserved in this task's Computer Use transcript. Existing
captures are unchanged. Each alert-interface construction recorded 125 avoided
full-type scans, eight native predicates, no refusal and zero retained types:
twice in 01, once in 02. This qualifies execution/lifetime, not isolated speed.

## What the controls establish, and what remains unresolved

The unsuppressed automated failures `stq-native-scene-02` and
`stq-native-absent-03` remain failures. The new controls materially change the
entry path: real user menus, no gameplay probe, initially no observer at all.
Both native manual paths succeeded. The observer-only control creates no probe
component; it records a menu repaint through its existing hook. Thus neither
observer assembly presence nor required Prepatcher presence alone prevents
ordinary entry in these controls.

The InputLegacy reflection-only dependency error nevertheless appeared twice in
01 and once in 02. A reflection-only assembly is loaded for inspection rather
than execution. Prepatcher marks original game assemblies this way and recreates
scene components with patched types; its resolver can leave that original
dependency unresolved. This is a plausible shared bootstrap boundary. The earlier
native evidence proved an empty-stack formatter busy loop in one failed process;
its memory-sensitive trigger was not repaired or conclusively assigned to the
probe. Successful manual controls do not prove the error harmless in every run.

No demonstrated product defect warranted a speculative engine/Prepatcher patch.
No dependency-absent control was needed after both required-dependency manual
controls passed; it could not qualify the candidate. Prior root-lookup/input
preload failures were not retried. Formal release disposition of the residual
shared error remains with the steward; do not hide it with public suppression.
Earlier minimized-save evidence remains workaround-assisted. No all-modlist,
long-colony, Linux, new-colony-background or standalone-map-background claim.

## Package source checkpoint and validation

Product code and runtime build-input files remain unchanged from `d584eb3`.
Changes are release metadata/user documentation and private fixture save staging.
The small fixture change allows the existing strictly verified automatic-created
save to be staged for manual functional loading, with or without the observer.
It preserves capture/content/save hash checks at preparation and launch, omits
the automatic load trigger, and rejects performance/automatic misuse.
All **49 Python checks passed**, including save provenance/drift and release
boundaries. Final rebuilt package identities and checks are recorded at closeout.

The existing packager must produce a matching committed source archive, notices
and hash manifest. It copies the built DLL unchanged and excludes game data,
fixture data, observer/helper binaries and build receipts. Tracked observer/helper
source and development tools remain in the source archive, explicitly described
as source, not installed runtime components. Public/private Git histories stay
separate. No helper overlay, tag, push or publication.

Accepted four-repeat Steam comparison figures remain authoritative: means
153.599894 s absent, 68.2582285 s public 0.2.1, 68.634004 s new defaults and
67.16399425 s translation/routing enabled. New defaults were 0.55% slower than
public; optional bundle 1.60% shorter. See the
[comparison report](steam-qualification-comparisons.md) for complete cache,
workload, ordering and variability limits. No unchanged benchmark repetition or
new universal speed claim is justified by documentation/private-control changes.

## Final package and ownership handoff

Built source/archive revision: `8ca0dd763a2ad530668b59d6c3488f28eac311f5`. Production and runtime build-input
trees have no differences from accepted `d584eb3`; the release rebuild records
the new source revision and uses exact Steam references. DLL SHA-256:
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`. The build, deployed fixture, loose release DLL and ZIP payload match.
Deployment transaction: `20260910-032427-fb62b11d`.

Final local package:
[`wake-up-0.3.0-rc.1.zip`](../../../artifacts/releases/0.3.0-rc.1-8ca0dd763a2ad530668b59d6c3488f28eac311f5/wake-up-0.3.0-rc.1.zip)

ZIP SHA-256: `ab59fce2fcdc6eb4891cfb3b3e46e8b6e03c151885f7593b19e219b147db3c48`.
The independent [package review](../../../artifacts/ordinary-entry-package/package-review/summary.json)
verified all 14 payload files, all manifest hashes, notices, metadata IDs/defaults
and exact `git archive` source correspondence (209 files). Source ZIP SHA-256:
`e34e1638d7a0450333ccef14ff4d4d4c9f8ff4f27b19f7f37867acfd3595ad77`.
ILSpy's full compiled-instruction output is identical to the accepted DLL.
All ten referenced-assembly rows match except the expected game reference version,
GOG `1.6.9676.17238` to Steam `1.6.9676.17735`. Thus this is a fresh binary identity,
not a new algorithm or a claim that the old DLL hash still applies. No startup
behavior change calls for repeating the accepted comparisons. Final native load
and the Steam-reference focused tests cover the rebuilt candidate within scope.

Validation: Release build zero warnings/errors; **109 managed passes, one optional
helper skip**, and **49 Python passes**. Managed results are
`artifacts/fixture-tests/33fd1ad45efb4ce587e1283323946b45/features.trx`.
Final fixture metadata audit passed with the original three exclusions, correct
deployment and unchanged generation manifest. All three actual fixture processes
exited normally and no RimWorld process remains. The final captured profile is
retained; no extra launch is prepared or implied.

The source archive and package metadata are the committed **pre-final-test
checkpoint**. Their conservative pending-check text is superseded by this
post-package report; package bytes are not rewritten after inspection. References
there to workaround-assisted save checks describe earlier/minimized evidence,
not the three current native controls. Formal acceptance/readiness flags remain
false pending steward review and disposition of the residual shared engine risk.
This report does not self-accept or authorize public release.

All subagent inspection/editing and owner writes/builds/deployments/launches stop
at final handoff. Exclusive checkout ownership returns to steward
`01a08711-7899-7ff3-a1b5-4a0b105ba661`. No tag, push, publication, normal game/data,
Deck, Linux helper, security or other-application changes occurred. The steward
reviews this concrete local candidate and residual limits; no new slice is created.
