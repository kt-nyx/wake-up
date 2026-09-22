# R1: routing fix and nonshipping XML experiment

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

## Parent acceptance — 10 September

Corrected handoff `27e378871f77831b8fed019a6a370d9a0545bd36` is accepted for
the routing fix and withdrawal of the losing XML experiment from the product.
The parent verified that the net product/script diff from dispatch contains
only the two routing files, that experiment sources compile through the test
project only, and that focused checks assert their absence from the product.
No further unchanged cost test or broad regression rerun is required for this
correction. The 38 focused managed passes support it; the unrelated existing
temporary fixture-rename test still failed with Windows access denied (49/50
Python passes). Its implementation is unchanged from dispatch. Retain this
unresolved test limitation for combined review; do not call the suite all green
or attribute a cause beyond the recorded error.

No useful independent XML cache has been accepted. The 81-operation retained-node
region is a concrete R4 follow-up, with ownership/publication still unresolved.
R2 texture preparation proceeds independently; no live test or deployment is
authorized. Ownership returns to the steward before the new serial dispatch.

## Corrected disposition after parent review

**The routing fix remains in the product. Independent XML reuse does not.**
Parent correction `wake-up-r1-correction-1-6d077de` started from clean
`6d077de36d2170c102294c757762eb10c4f10ea6` in the same checkout and branch.
The normal product no longer contains the XML implementation, its ordinary
setting/selector handler, initialization, maintenance or automatic Prepatcher
registration. It therefore adds no XML bridge overhead to ordinary startup.
The private fixture setting allowlist also no longer offers this option.

The implementation is preserved under [experiments/XmlPrefix](../../../experiments/XmlPrefix/README.md)
and compiles only into the existing offline test project. Its automatic
registration attribute was removed as well. No new project, runner, dependency
or negative-cache/bypass heuristic was added. The test DLL is not a shipping
payload. Existing state, semantic and source-attribution tests remain available;
their historical results below still describe the withdrawn implementation.

The only measured real-content case cost 769.6 ms to reuse versus 22.1 ms to
patch normally. Its cost refusal still paid validation overhead on each selected
launch, and even the disabled option installed two Prepatcher bridges. The parent
rejected that as a product option. **No useful independent XML cache is delivered
yet.** Historical source/package `8452a8e` is a withdrawn experiment, not the
current candidate. The existing deployed candidate and reset fixture remain
untouched. No live run or publication occurred; parent re-review owns acceptance.

### One bounded R4 follow-up premise

The source-only follow-up identified Combat Extended's
`Patches/Core/ThingDefs_Misc/Apparel_Various.xml`: **81 consecutive top-level
operations containing 83 native operation objects** (46 Replace, 18 Add,
13 Remove, five AddModExtension, one Conditional including both children).
The frozen file is 21,748 bytes, SHA-256
`f4ace85772692c1d37ae00c17af94adee5d90fe58a7310833183f0e3c2e52443`.
It is in the supplier's unconditional root folder, after Adaptive Storage's
custom-operation boundary in both examined package orders. The region contains
no FindMod/custom worker. Mod-extension classes are value XML here, not executed
patch code. Within-file ordering is established; runtime admission must discover
the actual consecutive instance region and must not use a file/package allowlist.

All selectors are confined to **14 named definitions and two named templates**.
They change apparel statistics, costs, descriptions, coverage and extension XML,
without replacing top-level definitions or their source identities. The concrete
different premise is to validate the actual matching root subtrees immediately
before this region, including duplicate and absent targets, then replay completed
changes on the existing nodes. This avoids encoding every source and combined
document. Earlier custom operations still run; their resulting XML becomes the
input. The existing literal-selector optimization overlaps this region, and the
historical 7.302-second CE timing covers the entire mod, not this file. The lower
validation footprint makes a benefit plausible; there is no region timing or
admitted performance claim.

The smallest unresolved contract preserves the original document and every
pre-existing node, including removed/replaced descendants that custom code may
still reference. It must preallocate new nodes, preserve exact edit order,
attribution and operation success state, and refuse custom node implementations,
foreign XML hooks and event subscribers. A restored final subtree alone is not
sufficient. Ordinary XML mutators can fail partway, so a reviewed non-observable,
non-allocating commit/rollback over node/link/attribute internals, or an equivalent
safe publication mechanism, remains necessary. Catching a partial restore and
rerunning native patches is unsafe. This is the next bounded R4 decision, not an
authorization to build a generic journal framework. No such framework, benchmark
or replay implementation was started. The private [source note](../../../artifacts/r1-independent-xml/research/r4-retained-node-region-premise.md)
preserves the exact source findings. R2 can proceed independently under the parent.

### Correction verification

Focused checks inspect the compiled product to prove that all five XML experiment
types and the ordinary setting are absent, and that the retained experimental
prepatch has no automatic registration attribute. Ordinary selection and routing
regressions pass; the fixture setting validator rejects `independentXmlReuse`.
The original callsite test still checks the historical rewritten bodies, accounting
only for the explicit bridge assembly relocation into WakeUp.Tests. The historical
state/semantic evidence was trusted rather than repeating the real-content cost
experiment.

Correction source commit: `19e27ca26141ae5e2e765c4c07354affc38af522`.
The product source difference from dispatch base `0df5a57` is now limited to
`AssetRoutingPatchGuard.cs` and `AssetRoutingRuntime.cs`; all XML shipping changes
are withdrawn. One offline package build succeeded with zero warnings/errors:
`artifacts/fixture-package/19e27ca26141ae5e2e765c4c07354affc38af522`.
Its `Assemblies/WakeUp.dll` SHA-256 is
`9cb1f843f16269b0510bbb0404afc47a154463ce65d43b987ecbcb7603d6af19`.

Both focused test invocations passed all **38 managed tests** with no skips.
The Python suite passed **49 of 50** on each invocation, including rejection of
the withdrawn setting. Both failed only in the existing
`test_steam_transition_preserves_source_mods_and_recoverable_gog`: Windows denied
`stage.rename(final)` at `scripts/fixture.py:115` while moving a temporary fake
fixture under `artifacts/fixture-tests/`. This is an unresolved filesystem-test
failure, so the correction suite is not reported as fully green. No harness or
security workaround was added. The earlier complete semantic-suite results below
remain historical evidence, not a replacement claim for this failed check.

Managed-test receipts:

- `artifacts/fixture-tests/7e54720738af4a1e86b5fa310c4f9e6b/features.trx`
- `artifacts/fixture-tests/1bde669c85384943852f4d61fe255a4e/features.trx`

Final read-only checks confirmed the deployed DLL remains
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`
and `.rlo-test-instance/prepared.json` is absent. The corrected package was not
deployed; no preparation or launch took place. `git diff --check` passed.
The team stops here and returns sole checkout/build ownership for parent re-review.

## Historical record — withdrawn implementation 8452a8e

The sections below preserve the original implementation and evidence. References
to a product setting, runtime initialization, production cost refusal or automatic
bridges describe that withdrawn checkpoint; they are not current shipping behavior.

## Result for the owner

The routing compatibility defect is fixed. There is also a working, default-off
path that can save and restore completed native XML patch work independently of
Gagarin. **Useful independent XML acceleration is not demonstrated on the examined
workload.** The safe initial patch region is too cheap to justify reconstructing
its document: the final offline official-content sample took 769.6 ms on the
complete hit path versus 22.1 ms for ordinary patching. Production therefore
refuses that case through a cost check. This is a partial R1 implementation
handoff, not achievement of the larger replacement/performance goal.

The work remains live-unqualified. No deployment, fixture preparation, profile
change, game launch, UI interaction, publication or normal-game operation took
place. The accepted deployed candidate is unchanged. Parent review owns
disposition and serial progression; this task does not accept itself or start R2.

## Source, build and ownership

Dispatch `wake-up-r1-xml-offline-0df5a57` confirmed a clean sole checkout at
`0df5a571f0102a45c8c3cb834b5079a8af869cf6`, then created
`codex/r1-independent-xml-0df5a57`. Implementation commit:
`8452a8ebfd7437207fdb009e609d4a8bc9e58a66`. No second checkout or worktree was used.
Bounded subagents owned distinct files or reviewed read-only; all have stopped.

The guarded offline build produced
`artifacts/fixture-package/8452a8ebfd7437207fdb009e609d4a8bc9e58a66`.
Its DLL SHA-256 is
`a85b304948a26e9725253e704d4103566b42c02b5715a1b3c0596614dace454c`.
This package was **not deployed**. The installed DLL still hashes to
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`,
source `8ca0dd763a2ad530668b59d6c3488f28eac311f5`; `prepared.json` remains absent.

Fresh read-only inspection verified the current Steam reference against the
existing fixture contract: game DLL
`5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`,
generation `steam-rev590-20260910-020604-cb37ef1b`, manifest
`62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93`.
The private [source receipt](../../../artifacts/r1-independent-xml/research/source-receipt.json)
and refreshed native sources record this check. The 21 types comparable with S05
match its older GOG source text; this is disk evidence, not effective live-code
qualification. Runtime admission checks the loaded implementations separately.

## What changed

**Routing:** `AssetRoutingPatchGuard` now includes Harmony inner prefixes and
inner postfixes. These are additional places where another mod can intercept a
method. Two focused publication tests prove refusal, clearing of remembered
routes, and recovery after removal. Status now distinguishes readiness, a
successful route, ordinary fallback and a changed-hook refusal; it no longer
keeps saying active after the last lookup was refused.

**XML reuse:** a prefix is the uninterrupted initial group of supported patches,
before the first unsupported operation. It may encompass the entire patch stage
on a suitable list. This implementation saves that group's completed document,
source attribution and success state, while all later operations still run.
It does not save Def objects, source parsing, inheritance results or languages.

## Boundary and state contract

Prepatcher makes two small changes before the game methods are compiled:

1. In `LoadAllActiveMods`, replace the single `ApplyPatches` call with a bridge
   that can return both the document and its source dictionary to the caller.
   The dictionary passes through a typed, checked object local; the following
   reference assignment remains inside the native protected callsite.
2. In `ApplyPatches`, replace the single top-level `PatchOperation.Apply` call
   with an inert bridge. It normally calls that original method. On a validated
   hit it skips exactly the admitted top-level instances and returns their
   previously successful result. Native ordering and exception handling remain.

Both entire original callsite bodies must match reviewed fingerprints before
either edit occurs. At use, 31 loaded method contracts cover the transformed
calls, combination, error checking, TKey handling, native workers, completion,
patch-list access and metadata getters. All Harmony hook categories are checked,
including inner hooks. Only the exact same-module definition-search callbacks
are allowed at their reviewed targets. Foreign downstream parse hooks or hooks
in the XML library also refuse reuse. Missing or changed Prepatcher work retains
ordinary loading. This is per-feature method admission, not a blanket game
revision restriction or proof about an unseen live patch combination.

Production admits exact Add, AddModExtension, Insert, Remove, Replace, SetName,
AttributeAdd, AttributeRemove, AttributeSet, Test, Sequence and Conditional types.
Every nested branch must qualify, including branches not taken. **FindMod ends
the region**, as do unknown operations, subclasses, cycles, shared structural
children and changed field layouts. Its descriptor/state utility has offline
FindMod tests, but production explicitly excludes it: the native display-name
lookup has additional expansion/lazy-loading dependencies not admitted here.
No package allowlist, custom-DLL purity assumption or fixture recognition exists.

Patch lists must already be ordinary materialized lists. The descriptor binds
all effective instance fields, XPath strings, value nodes, enum modes, operation
order and original success/failure state. It stops before an entire unsupported
top-level tree. Unknown operation methods and their fields are not walked or
executed by the descriptor. Unknown suffix operations execute once, in their
normal position, including randomness, external reads, callbacks and failures.

The key binds the actual ordered loaded source documents, source names/folders
and packages; the combined document and source mapping; selected mod order,
names and roots; ordered top-level operation identities/source files; the
complete admitted operation fields; and format, XML-library/game module and
effective-code identities. Earlier loading, parsing and error checking still
run on every launch. This late key deliberately describes their **actual parsed
snapshot**. A raw-file change discarded by ordinary parsing, such as an ignored
comment, need not invalidate identical native patch inputs. There is no separate
metadata-only filesystem freshness test or second discovery pipeline.

The result stores node kinds, namespaces, attribute and child order, empty-element
distinctions, values and document whitespace policy. XML declarations in source
inputs are supported. Unsupported node kinds, external entity/DTD representations
and custom node types refuse. Restored mapped nodes bind to this launch's real
`LoadableXmlAsset` objects. Added/replaced unmapped roots remain unmapped; removed
source-map roots remain detached nodes in the same owner document. No generic
“cached asset” substitutes for the original file/mod attribution.

TKey parsing remains before patching. Its reviewed hardcoded-mapping path does
not retain combined-node references. Inheritance registration/resolution,
dependency filtering, Def creation, each original top-level `Complete`, patch
clearing and inheritance clearing remain afterward. The complete operation tree's
`neverSucceeded` and sequence failure references are staged, including unselected
children. Typed field setters and their commit delegate are prepared before
publication. The commit performs field/reference assignments without reflection,
XML mutations or user callbacks. Original success and later completion behavior
are preserved; failed/throwing admitted prefixes do not publish an entry. Errors
in later unknown operations remain ordinary errors and do not rerun the prefix.

## Capture, restore, refusal and maintenance

The first build executes ordinary operations and captures only a successfully
completed admitted prefix. Reads validate the owned-store integrity envelope,
bounded document forest, current source bindings and operation-state identity
before changing live state. Failed reads, corrupt entries, unsupported inputs,
changed contracts and staging errors execute ordinary patching once on the
untouched inputs. Failed capture writes leave the already-computed result intact.
There is no fallback that reapplies patches to a half-restored document.

The default-off setting is **Reuse completed native XML patch prefix**, persisted
as `independentXmlReuse`. It obeys the master switch and explicit development
selection precedence; `--wake-up-xml-reuse=on` is the development selector.
The private fixture allowlist now recognizes the ordinary setting, but no
fixture selection was prepared. Other product defaults are unchanged.

Storage belongs to `WakeUp/XmlPrefix/v1`: maximum payload 64 MiB, category
512 MiB including envelopes/accounting, at most two million XML nodes and XML
depth 256. Operation descriptors allow 32,768 operations, depth 64 and 16 MiB
identity. The ordered source-tree writer is bounded at 64 MiB; the aggregate
key buffer is bounded at 128 MiB. Capacity is checked before result capture.
These finite limits intentionally refuse larger inputs instead of relaxing
identity. The same central next-launch bypass/rebuild/clear controls apply,
including clear with the feature disabled. Store ownership ends at the patch
stage; there is no background worker or retained startup document at the menu.

The settings status and `WakeUp/xml-prefix.jsonl` distinguish selection, an
unreached boundary, admitted counts, miss/capture, skipped top-level hits,
refusal and cost rejection. Supplier caches retain their own behavior. Gagarin
or Loading Progress ownership of the relevant loading functions refuses this
feature, or bypasses its boundary; the feature never disables a supplier or
uses its data. Removing Gagarin does not itself guarantee an admissible or useful
Wake-Up hit. Existing search savings must not be added to skipped patch work.

Production also requires **measured cost headroom**, not just semantic eligibility.
Initial wrapper setup, descriptor/key validation and staging are timed. Native
prefix time must exceed measured overhead by 50%; first publication additionally
accounts for capture and a private decode. A hit rechecks the stored native-work
budget against this launch's measured overhead before committing. This is a
conservative heuristic, not a speed guarantee or substitute for live comparisons.
Rejected launches still pay validation cost. Keep the option off for the examined
low-cost prefix; no extra persisted bypass heuristic was introduced.

## Offline evidence and honest limits

Final `scripts/fixture.py test` against the current Steam reference passed
**346 managed tests**, with two optional supplier/helper tests skipped, plus
**50 Python tests**. Build completed with zero warnings/errors. Full receipt:
`artifacts/fixture-tests/0b6b5ae31c674a5cab7a509bbd27339d/features.trx`.
Fixture safety tests print simulated launch/staging messages; the command's
result explicitly records `gameLaunched: false`.

Focused tests cover all hook kinds; original/transformed callsite contracts;
nested and conditional success/Complete state; cold capture and warm equivalence;
mapped, unmapped and removed attribution; changed definitions, patch values/order,
source names and environment; unknown suffix effects; failed/throwing prefixes;
corrupt entries; late admission refusal; and shared cache maintenance. Tests use
actual native XML combination and native operation methods. Their bounded host
mirrors top-level exception iteration; it does not execute full game loading.

An explicit read-only official-content sample used **1,558 real source files and
29 real top-level patches**, producing 19,884,956 encoded result bytes. Native
results, restored XML/source mapping and avoided operations matched. Its
correctness arm deliberately uses the internal cost-check bypass so the losing
restore can be inspected; production exposes no such bypass. Both warm-entry and
cold-miss arms with production cost checking refused reuse, preserved ordinary
results, and the cold arm published no entry.

The final sample recorded 1,020.2 ms cold, 769.6 ms complete hit and 22.1 ms ordinary
patch work under .NET Framework. Earlier checks showed the same large losing
direction. These are offline cost observations, **not game speed**, current
mixed-workload runtime admission, Mono timings or a public benchmark. The sample
uses an explicit official source selection, not a reconstructed mixed loader.

Read-only full/mixed content inspection finds those same 29 initial native
operations before Adaptive Storage's unknown nested operations. It does not
demonstrate runtime admission. Approximate selected-source encoding is 45.33 MiB
for the mixed sample and 85.85 MiB for the full original order, with unresolved
missing/parse cases; the 64 MiB format likely refuses the latter. No meaningful
full-workload reuse percentage or performance benefit is established.

Independent bounded review checked actual changed code. It caught source XML
declarations, the patch-list getter admission gap, incomplete FindMod dependencies,
unreached-boundary status, and cold-cost-refusal coverage; these were corrected.
The final reviewer reported no remaining actionable finding within that scope.
This review is not parent acceptance or live compatibility proof.

## Alternatives and next viable work

Whole-document replay cannot skip unknown operations just because their DLLs are
unchanged. Their external effects remain unknown. Repeating the rejected S04
per-file parse encoding also does not address this sample's small avoided budget.

Reusing regions after custom code is materially different and remains the more
promising broader direction. Replacing their document would invalidate node or
document references retained by custom operations. A retained-node mutation
journal could preserve those identities, but XML edits themselves raise events
and can fail after earlier edits. Rollback cannot undo arbitrary callback effects.
No safe general journal/rollback contract was established here; it was not silently
replaced by another XPath-only optimization.

The next viable implementation choice is an explicitly narrower retained-node
region contract: admit concrete XML nodes with reviewed event ownership, preserve
exact edit order and removed-node identities, and establish staged publication or
a recovery policy that cannot rerun partially applied work. Its first product
question is a useful real native region after an unknown boundary, not a broader
cache format or another generic harness. Alternatively, a naturally expensive
all-native prefix may use the implemented path, but no such useful real-content
case was established in R1. Unrelated R2 onward need not wait on that question.

Future owner-authorized live work must first prove actual required-Prepatcher
integration and safe refusal on the reset English workload, then source/state and
completion correctness. Useful-content timing needs supplier XML caching absent
or disabled, separate supplier comparisons, cold versus warm conditions, complete
validation cost and matched forward/reverse menu measurements. French translation
and gameplay compatibility remain separate claims. No live operation follows
automatically from this handoff. Exclusive checkout/build ownership returns to
the parent steward; all R1 agents and writes stop after the documentation commit.
