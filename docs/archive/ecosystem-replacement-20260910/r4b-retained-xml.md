# R4b: bounded reuse of native XML mutations

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Result and qualification boundary

**Disposition: implemented and rejected on complete offline cost.** The candidate
preserved the checked XML behavior, but warm reuse cost **37.204 ms versus
13.194 ms ordinary processing**. Cold capture cost **39.669 ms**. R4b therefore
does not deliver an independent XML reuse capability to the product. All new
region code is retained under `experiments/XmlRegion`, compiled only into tests.
There is no shipping setting, maintenance category, bridge, automatic prepatch
registration or search-observer integration seam. R4a and existing product
behavior remain unchanged.

This is a narrower experiment than the retired whole-document prefix. It reuses
native element edits in consecutive operations while retaining the original
document, existing nodes, removed-node references and source map. It does not
reuse parsed files, combined documents, inheritance results or arbitrary mod code.
The following sections document the tested candidate, not a supported feature.

No fixture deployment, preparation, cache/profile change, game launch, UI test,
Deck access or publication occurred. The reset English fixture and older deployed
candidate remain unchanged. R4c/R5 belong to the parent after handoff. There is no
new R4b setting to add to fixture tooling; R4a's `streamingXml` gap remains with R5.

Dispatch `wake-up-r4b-retained-xml-offline-6cafcdc` verified clean base
`6cafcdc6ed56bf756a492380d4ec8e7d0031ffab` and created
`codex/r4b-retained-xml-6cafcdc` in the sole checkout. Two bounded subagents
contributed publication/staging work, native contract review, tests and CPU
evidence under distinct ownership.

## Why this differs from the rejected prefix

R1 validated and restored whole documents to avoid a small official prefix. Its
769.6 ms reuse cost lost to 22.1 ms ordinary patching. That code remains only in
`experiments/XmlPrefix`; no old setting, registration or document snapshot is
restored by R4b.

The new premise is a later native interval in Combat Extended's frozen
`Patches/Core/ThingDefs_Misc/Apparel_Various.xml`, SHA-256
`f4ace85772692c1d37ae00c17af94adee5d90fe58a7310833183f0e3c2e52443`,
21,748 bytes. Its 81 top-level operations contain 83 native objects and address
14 named ThingDefs and two named templates. The file is evidence, never an
admission rule. The candidate did not select a package, filename, list of mod IDs
or a recognized fixture. It examines actual ordered operation objects and a
supported selector grammar.

Existing literal-name search acceleration already handles most of this work.
Two selectors contain several literal names joined by `or`, leaving broad native
searches over the combined input. R4b starts only at such an actual selector and
requires at least two broad selectors in its supported consecutive interval.
The candidate selected inputs with at least 13,000 top-level roots. An early
6,394-root Core kernel lost; a materially larger 13,809-root official input
initially provided headroom before final staging and integration costs. The final
complete comparison on that larger input also lost. Raising this cutoff or
shipping a permanent refusal would not establish a useful product.

The old 7.302-second whole-CE scope is not this interval's removable budget.
The later raw-official CPU input also does not recreate earlier official, mod or
custom patch outputs. Actual runtime input is bound immediately before reuse;
live qualification of a real mixed sequence remains separate.

## Actual operation and input boundary

The native `LoadedModManager.ApplyPatches` loop retains its original enumeration
and per-operation exception handler. Required Prepatcher changes only its one
`PatchOperation.Apply` call into an inert bridge. Disabled mode calls the ordinary
operation directly. R4a's filesystem constructor rewrite and its Gagarin fallback
remain untouched; both prepatch orders are checked offline.

At an eligible call, R4b finds that exact operation by reference in already-loaded
native patch lists. It never forces another mod's lazy patch getter early. An
unknown type or unsupported operation ends the local interval; it does not reject
every independent interval because custom code exists elsewhere. A cold miss
executes one operation per original loop call, so a native exception reaches the
original game handler without eagerly running later patches or retrying a partly
changed operation.

Supported exact types are Add, Replace, Remove, AddModExtension and Conditional,
with all conditional children inspected. Selectors must start at `/Defs/Type`
(or its equivalent relative spelling), select literal `defName` or `@Name`
values, optionally joined by `or`, and continue through child-element names.
Parent traversal, unions, arbitrary functions/axes, custom subclasses, FindMod,
root replacement/removal and identifier changes stay ordinary. AddModExtension
imports XML value data; it does not instantiate the extension class here.

A single top-level pass finds every matching root, including duplicate roots and
absent requested targets. Identity includes complete structural root contents,
root order, node kinds and text boundaries, attribute order, empty-element state,
actual operation fields/order/source filenames, initial success state and current
source-asset attribution. Unrelated root contents are not serialized. Earlier
unknown operations' real XML outputs therefore become the current dependency
input. The existing source map itself is retained and later inheritance/Complete
still see the original objects.

## Preparing and publishing changes safely

Replay must preserve references to old nodes, not merely produce the same XML
text. Cold capture records native insertion/removal order and operation outcomes.
Inserted contents refer to the current operation's validated XML templates;
stored data cannot supply arbitrary replacement XML text. A warm result imports
fresh nodes, validates a complete sequence of element-child edits, then applies
precompiled writes to the native parent/next/last-child links. Removed descendants
remain attached to their removed parent exactly as native execution leaves them.
The native empty-element marker and operation success state are retained.

Importing a detached node can still change its destination document's internal
name tables. R4b therefore uses a deliberately small staging rule: every imported
element/attribute name tuple and string atom must already exist in the actual
destination document. The native validity flag must already be false, with no
schema, default-attribute or ID-declaration state. Imported trees contain only
exact native element, attribute and text nodes. Missing names refuse locally;
ordinary execution may introduce them for a later independent interval. This
avoids a general name-table transaction or replacing the document's intern tables.

This rule matters in the evidence sample. Seven names in the full 81-operation
region were missing from raw official input: Bulk, CarryBulk, CarryWeight,
DevilstrandCloth, modExtensions, stats and WornBulk. The full interval is therefore
not admitted under the final contract. Discovery can retain the earlier interval
before the first operation introducing Bulk; the rest executes ordinarily.

Custom document/node implementations, foreign XML event subscribers, relevant
changed native methods and all foreign Harmony hook categories refuse. The exact
owned search-index observer is allowed only outside a pending mutation. Its index
stays valid because this grammar cannot alter roots or their identifying names.
Cold capture uses the same observer admission, preventing an outside listener's
effects from poisoning a future entry. Profiling/nested patch scopes stay native.

Every fallible validation/allocation happens before original node-link writes.
The commit has no XML virtual calls, reflection, allocations or observer callbacks.
There is no catch that reruns ordinary patches after commit has begun. Invalid
payloads are removed by the owned store's validator; staged node IDs are discarded
before ordinary fallback captures a replacement. Publication assumes the native
serialized loading thread owns the document/list; arbitrary concurrent mutation
is not a new supported contract.

## Selection, storage and lifetime

The tested integration included a default-off setting, explicit selector,
startup status and central maintenance. All were removed before the final product
build. The retained experimental prepatch has no `FreePatch` attribute. Its
observer adapter now reads existing search state through reflection inside the
test assembly, avoiding an unused product API; the measured integration used the
equivalent direct internal check. Private `source-tested/` retains the measured
candidate source. Timing receipts refer to that candidate, not the archived adapter.

`WakeUp/XmlRegion/v1` was the proposed S01-owned category. Its experimental bounds were:

| Resource | Bound |
|---|---:|
| Stored interval payload | 1 MiB |
| Category, including envelopes/pending files/usage data | 16 MiB |
| Native operation objects per interval | 128 |
| Matched roots, including duplicates | 64 |
| Dependency nodes / depth | 32,768 / 64 |
| Captured edits | 4,096 |

The owner handles integrity checks, exclusive ownership, atomic file publication,
replacement headroom, bounded eviction and interrupted files. Central bypass,
rebuild and clear actions apply. Native fallback and native exception handling
remain available when storage fails; cleanup errors do not replace a native
loading exception. Stage completion releases the capture/session/store.

No XML category ships. Product owned-category ceilings remain **1,024 MiB**:
512 MiB automatic PNG/JPEG plus 512 MiB shared native-prepared/DDS exports.
The candidate's additional 16 MiB was included in its cost probe and removed
with the product integration. These are disk ceilings, not process-memory claims.

## Offline evidence and remaining live checks

Initial kernel cost probes used actual current native operations with the
existing search acceleration. Output, operation state and 1,435 retained-node
aliases (including detached topology) matched ordinary execution. These earlier
numbers preceded the final existing-name admission and startup integration:

| Input / boundary | Native accelerated | Warm kernel, full store lifetime | Warm, already-open store | Cold capture |
|---|---:|---:|---:|---:|
| 6,394 Core roots | 4.652 ms | 6.168 ms | 3.354 ms | 15.316 ms |
| 13,809 official roots | 12.637 ms | 8.547 ms | 6.223 ms | 26.977 ms |

The full 81-operation numbers are exploratory cost evidence, not final admitted
coverage. Raw receipts, exact 1,558 input file identities and the larger sample's
12,682,411 source bytes are under `artifacts/r4b-retained-xml/cost/`. The host was
.NET 8.0.31 with tiered compilation disabled and warm filesystem conditions,
not the game's Mono execution. No menu-speed percentage follows from these costs.

Focused managed checks cover native operation trees/state, selectors, changed
inputs/source/order, duplicates and missing roots, detached aliases, cold/warm
and ordinary fallback, native partial failure without retry, observers, malformed
envelopes/journals, repair after failed staging, bounded repeated storage and the
actual Cecil callsite contract. Independent review found and resolved cold
observer poisoning, staging metadata mutation, corrupt-entry repair and staged
ID contamination. Existing S01 storage checks remain the storage contract;
no separate generalized journal/test framework was added.

Exact current game assembly SHA-256:
`5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`.
Inspected native System.Xml SHA-256:
`4f26013a653d66b1df99aa2f003dd9b6c629d82f5401f1b630aa3ff61771b01d`.
Runtime admission checks relevant effective method bodies and published hooks,
not a production mod/package/fixture allowlist.

## Complete integrated decision

The final comparison loaded the actually rewritten native `ApplyPatches` into a
separate CPU process as the sole game assembly. It exercised actual runtime
initialization, stage hooks, native running-mod lists, source-map discovery and
the original 81-operation loop. Existing-name staging admitted exactly six
operations (zero-based indices 2 through 7), including both broad selectors.
The remaining 75 operations ran normally. The native control restored the one
original `PatchOperation.Apply` call and paid no disabled-bridge overhead.

Eight alternating rounds charged native identity verification (52 method bodies
and pinned Harmony file hash), runtime registration, normal existing-store
maintenance, region discovery/validation, full storage lifetime, all 81 operations
and stage cleanup:

| Complete measured path | Median |
|---|---:|
| Ordinary processing with existing search acceleration | 13.194 ms |
| Warm integrated candidate, one actual six-operation hit | 37.204 ms |
| Cold integrated candidate, one six-operation capture | 39.669 ms |

Even the warm stage and cleanup alone cost 23.773 ms, more than the complete native path.
Removing startup verification or duplicate maintenance cannot recover a useful
result. The earlier full-81 kernel gain is not available under final staging and
must not be reported as a product gain. No further cost-cutoff tuning, repeated
same-premise trials or live qualification of this candidate is justified.
Reopening requires materially more avoided work or a different evidence-backed
mechanism that pays less than native processing while preserving the contract.

The integrated probe matched complete XML, operation state, 1,435 retained-node
aliases and source-map identities. Actual-callsite cases also passed for earlier
custom XML changes, later custom code retaining removed nodes, and corrupt-entry
fallback/recapture followed by a fresh hit. These are CPU functional checks, not
menu, gameplay or arbitrary mixed-mod compatibility claims.

The host was .NET 8.0.31. Because its XML library differs from the game's Mono
library, a private probe seam admitted host method guards after separately
verifying and charging all exact native method hashes and the Harmony file hash.
Production platform/Harmony metadata branches were not exercised in that host.
The receipt preserves this limitation; no actual game-Mono admission or startup
speedup was demonstrated. Raw evidence is private under
`artifacts/r4b-retained-xml/integrated/cost.json`; earlier probes remain in `cost/`.
The tested candidate DLL hash is
`0e21967840bd99a5f228683085843dd0398ecc8b7175cbdc6a00ba9b69d84c5a`.
It was never deployed and is not the final package.

## Verification and handoff

Before retirement, guarded focused checks passed 152 managed tests with one
optional streaming cost probe skipped, plus 50 Python checks. The later focused
run passed 100 managed tests plus 50 Python checks. Receipts:
`artifacts/fixture-tests/48216a7b4cf84430b44570ed6d7df0a8` and
`artifacts/fixture-tests/f4364055376d41968448e82c996e4f47`.
Independent publication/integration review findings were fixed, including a
regression proving repair after staging failure hits on the next fresh input.

The final test-only retirement run passed **152 managed tests**, with one optional
streaming cost probe skipped, plus **50 Python checks**. Receipt:
`artifacts/fixture-tests/e3e7334ecc494637882177cb8f456baf`; `gameLaunched=false`.
It includes an explicit check that region types/settings are absent from the
product assembly and the experimental rewrite has no automatic registration.
Retirement changed the bridge's assembly identity and split search callbacks into
the product assembly; the archived contract/tests were adjusted for those two
mechanical changes and rerun successfully. No shipping source files changed from
the assigned base. Stable-source package and ownership closeout follow below.

The capability ledger remains explicit: independent parsed-XML/mutation/inheritance
reuse is missing. R4b's retained code and tests are research evidence, not delivery
of that capability.

### Stable package and ownership return

One guarded core package build passed with zero warnings/errors from committed
source `68e7db831e4e6ddc995bb934ffafbc44d71c5092`. Package:
`artifacts/fixture-package/68e7db831e4e6ddc995bb934ffafbc44d71c5092/`;
adjacent `.json` receipt records selected references and packaged file identities.
Packaged `WakeUp.dll` is 314,880 bytes, SHA-256
`ad07c4ac395c00a2ea4e477ab3f240eb60d2b2aacd681948cf247f89ea4b4e8c`.
This final package contains the existing product, not the measured rejected region
implementation. A subsequent documentation-only commit records this receipt.

The fixture pointer remains generation `steam-rev590-20260910-020604-cb37ef1b`,
manifest `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93`.
`prepared.json` remains absent; no fixture files are tracked. Shipping source and
fixture scripts have no changes against the assigned base. Both bounded subagents
finished and stopped all writes; all builds/probes finished. The clean committed
checkout is returned to parent task `01a08711-7899-7ff3-a1b5-4a0b105ba661` after
this closeout. No R4c/R5 work was started, and no push or publication occurred.

## Parent acceptance of the negative result — 10 September

The parent accepted handoff `c0916de`, source `68e7db8`, as a rejected experiment.
Review checked the raw complete-cost receipt and its actual six-operation hit,
the earlier 81-operation exploratory boundary, source/test compilation links and
explicit exclusion checks. Product source and fixture scripts have no diff from
the assigned base. The complete warm stage also lost without setup/maintenance,
so another unchanged probe or a setup-only adjustment is not justified.

No new build or test was needed to review the retirement. Independent XML reuse
remains absent; the retained code is historical evidence. The stopped team has
returned ownership, and R4c follows under the unchanged offline-only campaign.
