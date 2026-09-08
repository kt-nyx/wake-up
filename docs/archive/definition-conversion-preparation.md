> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_DEF_CONVERSION_PREPARATION.md` at `54bee10c3e806bed346a31a1fb4ac470f68ef5e0`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# XML-to-definition preparation investigation

## Owner-requested removal

The owner requested removal after reviewing the negative performance result.
The scalar workers, generated-parser interception, conversion-only diagnostics,
definition fingerprint collector, dedicated tests and `--def-conversion` fixture
selector have been removed. The ordinary XML-to-Def path is restored. Existing
startup-search optimizations and DDS/PNG readers are unchanged. The independent
`test --test-filter` workflow improvement remains available for focused checks.

The measurements and correctness findings below are retained as historical
evidence, with original captures and analysis under ignored artifacts. Runtime
code can be inspected in historical checkpoint `e642b14`; the reproduction flags
below are no longer accepted by the current fixture script. There is no need to
retain an inactive runtime prototype to preserve what was learned.

Removal validation passed the pinned-game build (zero warnings/errors), seven
focused existing managed checks and all 35 fixture Python checks. Runtime source
and tests match pre-investigation `234269f` ignoring line endings; only the
independent focused-test selector remains in the fixture script. Evidence:
`artifacts/startup-concurrency/conversion-removal-checks.log`.
No new live launch is required for this removal. The last live capture
remains `conv-h04-control` on `e642b14`; it must not be attributed to the removal
package or reused as a prepared launch after deployment.

Removal implementation `897bdf1e53824db26fd6e8365e44b239da404a95` was explicitly
built and deployed using the fixture script. `candidate.json` confirms that
source revision. Build/deployment receipts are
`artifacts/startup-concurrency/conversion-removal-build.log` and
`conversion-removal-deploy.log`. The fixture is closed; its historical
`prepared.json` selection remains `conv-h04-control`. No new live result is
claimed for the removal package.

The owner renewed investigation, implementation, checkpoint and live-test
authority on 2026-09-05. This continues clean `234269f` on
`codex/startup-work-reduction` in the single source checkout. The last deployed
package was `0ff5197`; `conc-e10-worker-control` was captured and no RimWorld
process was running. Existing branches/stashes and optimizations are preserved.
Small selections and the documented xml-expanded selection are authorized;
medium/full remain paused. OP7 is incomplete and OP8 inactive.

The objective is to overlap independent work while preserving the original
construction, callbacks, reference ownership and publication semantics. Whole
definitions are not assumed independent. No persistent cache is proposed.

## Historical decision and tested checkpoint

**Private scalar parsing really overlapped and preserved the checked content,
but did not establish a repeatable loading improvement. Keep it disabled.**
The fresh-cache apparent gain reversed on the reverse-order repeat. With
Gagarin cache writing removed by a demonstrated cache hit, the prototype was
140.559 ms (7.3%) slower in the complete affected stage. Menu times were almost
equal. More CPU overlap is not a loading-time benefit.

Last tested/deployed package: `e642b14b064c383c706bce9ee00ce4f484c978e8`.
Final captured
selection: `conv-h04-control`, with `--def-conversion timing`, so preparation
is disabled. All 13 new live runs reached the menu, exited normally with code
zero and completed capture with `automaticTestPassed=true`. No forced closure,
UI automation, normal profile/cache writes or medium/full launches occurred.
OP7 remains incomplete and OP8 inactive. This decision ends the requested
investigation; it does not prove all architectural parallelism impossible.

### Complete measured costs

All seven final expanded comparison/verification captures use the same deployed
`e642b14` source, content/order, accepted startup searches, two-reader DDS/PNG
options, concurrency timing and menu observer. Only `--def-conversion` changes
within a cache condition. The complete outer timer includes admission, XML
scanning, worker startup, joins, guarded consumption, original ordered conversion
and publication, plus the other native/foreign work in ParseAndProcessXML.

| Run | Gagarin state | Preparation | Outer stage ms | DefFromNodeNew ms, nested | Menu seconds |
| --- | --- | --- | ---: | ---: | ---: |
| conv-c01-control | Miss | Disabled | 3437.723 | 2420.434 | 38.971708 |
| conv-c02-verify | Miss | Enabled, original-parser comparison | 3594.358 | 2207.509 | 36.347290 |
| conv-c03-scalars | Miss | Enabled | 2845.669 | 1863.825 | 35.330211 |
| conv-c04-scalars-repeat | Miss | Enabled, reverse-order repeat | 3314.487 | 2240.287 | 36.417980 |
| conv-c05-control-repeat | Miss | Disabled, reverse-order repeat | 2411.805 | 1344.571 | 28.886389 |
| conv-h03-scalars | Hit | Enabled | 2058.635 | 1977.609 | 27.694752 |
| conv-h04-control | Hit | Disabled | 1918.076 | 1886.053 | 27.608596 |

The verification row intentionally runs the native parser too; it is not a
performance arm. The first control/candidate pair has that verification run
between them. The reverse-order pair is consecutive. The large unrelated
variation is concrete: unchanged ApplyPatches took 4525.867 ms in C01 and
3916.720 ms in C03. The control Def interval itself ranged from 2420.434 to
1344.571 ms. These conditions cannot establish a small scalar saving from the
initial favorable pair, and the later cache-hit pair regressed.

| Candidate cost, ms | C03 | C04 | H03 |
| --- | ---: | ---: | ---: |
| Parser admission | 1.825 | 1.257 | 0.739 |
| XML scan and job allocation | 42.710 | 47.315 | 29.970 |
| Worker lifetime, including creation and joins | 19.798 | 16.614 | 15.162 |
| Join portion, already included above | 0.248 | 0.604 | 0.145 |
| Total preparation, including native root lookup/iteration | 70.073 | 71.814 | 47.891 |
| Consumption, including guards and native fallback calls | 161.693 | 208.842 | 175.969 |

Consumption is nested in DefFromNodeNew, and all rows are included in the outer
stage; do not add them to it. Each trial visited 420,037 nodes and admitted
65,536 scalar nodes at the entry cap, reserving 13,008,010 logical bytes. This
is a conservative work/storage estimate, not a physical process-memory cap.
44,346 prepared values were used; 140,277 intercepted calls used native parsing.
Some admitted nodes are used by unchanged custom loaders or other paths and
never consume a prepared value. Speculative parsing also computes an integer
and a float for numeric text without knowing which will be needed.

Three execution intervals overlap in every successful prototype run. For C03,
workers 104/105 and owner 28 started at 51.103/51.103/51.104 ms relative to
preparation and ended at 69.537/69.791/69.706 ms. They operate on disjoint private
jobs. No game constructor, mod callback, field assignment, reference registration
or publication ran on these workers. Original conversion starts only after
complete joins. This is parallel preparation, not a pipeline overlapping native
construction with workers.

### Correctness and actual cache state

The activation verification compared 44,156 values, and expanded verification
compared 44,346, against the original parser at the original callsite. Both
reported zero mismatches, including exact float bits. The native generated
assignment and list insertion instructions remain unchanged; only immutable
scalar values are supplied. Unsupported types, changed text/identity/parser,
foreign hooks, malformed input and permissive integer warnings retain their
original path and ordering.

All seven final expanded captures have the identical focused post-resolution
fingerprint `3a319c7fca4b465ab09d8371d564fba3d1d233c1378e0acb31b1f53451fc644d`:
6,496 ThingDefs, 25,474 stat values and 28,180 stat/category references, with
zero bad database-object references. This covers direct string/int/float/bool
fields, custom StatModifier results and actual reference identity, not just XML.
It does not cover every field of every Def type or save-load/gameplay behavior.

Unified.xml and Unified_Original.xml are byte-identical in all seven captures.
The targeted log scan found no exceptions, XML errors, unresolved cross-references,
definition-load errors or failed patch operations. Routine Mono dynamic-library
fallback notices are not those failures. Original logs remain available.

Fresh C01-C05 runs restored no foreign cache and explicitly logged cache missing,
expired and created. H03/H04 both restored only the captured Gagarin cache from
`conv-c05-control-repeat` and explicitly logged `GARGARIN: Finished loading XML
from cache!` (the spelling is the mod's). The prior H01/H02 pair similarly proved
cache use from E01. Restoration alone was not treated as proof of a hit. Gagarin
settings timestamps and unordered asset-hash XML are not content fingerprints.

### Why not expand into a general assignment-plan rewrite

The evidence identifies a small independent subset, rather than a large hidden
parallel conversion stage. Expanded instrumented field/type resolution took
about 160/184 ms; their normal work already uses cached fields and types. An
additional typed planning pass must repeat or replace this traversal and metadata
work before there are any field values to prepare. Shared scratch/cache state
is an implementation constraint, not proof that those lookups are intrinsically
sequential, but moving cheap cached lookups does not remove the new pass's cost.

Generated-parser construction was about 344-369 ms inclusive in the expanded
discovery runs, versus the earlier 220-230 ms small measurements. The delegates
are already reused within startup. Private parallel generation would require
separating its mutable helper caches and discovering the needed types in advance;
these measured costs do not justify a second general conversion implementation.
It is not a seconds-long compilation opportunity.

Native MakeInstanceOfTypeForEmptyNode took about 292-301 ms in profiled expanded
runs, including type resolution, allocation and constructor work. It is not a
pure allocation measurement: base Def construction consumes shared Rand, and
derived initialization may publish or depend on earlier state. Empty ordinary
constructors such as StatModifier are different, but the whole 23,000-call
StatModifier list path took only about 30 ms. Registration must still use the
real target in order. RulePack's whole custom field path was about 47 ms. Neither
justifies moving arbitrary custom code to workers.

The FieldInfo object-reference registration overload took 26.521 ms; this does
not include every list/dictionary/custom overload. AddDef publication was only
about 6-9 ms. The broad conversion residual includes DOM traversal, allocations,
assignments and callbacks; inclusive counters cannot partition it exactly.
No trustworthy allocation-byte result or complete callback-by-callback profile
is claimed. Building more profiling infrastructure to partition the remainder
would not unlock a demonstrated large independent operation here.

### Reproduction, checks and continuation

Useful checkpoints: d638d11 (diagnostics), d278a55 (bounded prototype and focused
data checks), e642b14 (worker BCL-hook admission; final deployed/tested package).
Focused final verification passes 16 managed checks and all 36 fixture Python
checks via `python scripts/fixture.py test --test-filter
'FullyQualifiedName~ScalarXmlPreparationTests|FullyQualifiedName~GameBuildContractTests|FullyQualifiedName~IndexedXmlRuntimeTests'`.
The earlier broad attempt was stopped; do not call it a full-suite pass.

Evidence: ignored `artifacts/startup-concurrency/conversion-final-summary.json`,
`conversion-outer-attribution.json`, discovery summary, final checks and per-run
prepare/verify/launch logs; originals remain in canonical fixture captures.
`conversion-candidate-pairs.py` and `conversion-candidate-hits.py` record exact
commands, revision checks, automatic acceptance and content comparisons. Use
new labels if reproducing; never replay a captured launch label.

The candidate command adds `--def-conversion on` to the shared candidate
startup-searches, xml-expanded, observation timing, concurrency timing, buffered
DDS/two-reader, PNG on, menu-observer/automatic-exit options. The control uses
`--def-conversion timing`; `verify` is separate correctness work. No earlier
inheritance, patch-query, lookup-batch or worker-reuse experiment is enabled.
No feature default or release is promoted. Leave the fixture closed and do not
resume medium/full testing without the owner's authorization.

## Initial source findings and diagnostic checkpoint

The native generated converters already reuse field-search results and parser
delegates within the process. `ResolveFieldForNode` uses `DoFieldSearch`, whose
ordinary path is a per-type field dictionary. The fallback includes aliases,
attributes, case diagnostics and missing-field diagnostics. Moving this entire
helper to workers would share mutable caches and reorder diagnostics.

The generated complex-object body resolves inheritance, constructs an object,
then visits fields in XML order. It registers Def references against the actual
target; ordinary fields invoke generated setters. Custom loaders construct their
target and call `LoadDataFromXmlCustom`; PostLoad executes after population.
Lists and dictionaries have separate paths and cross-reference registration.
`ParseHelper.FromString` includes registered mod delegates, compatibility enum
conversion, SlateRef construction and Type resolution, as well as basic scalar
parsers. It cannot be assumed entirely pure.

`--def-conversion timing` records the complete outer ParseAndProcessXML stage,
DefFromNodeNew and AddDef publication. `profile` additionally aggregates helper
and generated field/list delegate intervals. These are inclusive nested times,
not additive CPU or available savings. Up to 512 counters are retained; per-call
disk writes and system CPU profiling are absent. Both modes retain original
converter behavior. Profile overhead must be compared with timing-only before
using these measurements to justify implementation.

Local read-only decompilation/evidence: `artifacts/startup-concurrency`, including
DirectXmlToObjectNew.cs, XmlToObjectUtils.cs, ParseHelper.cs, LoadedModManager.cs
and GagarinCache.cs. Use the existing ILSpy CLI; do not load game assemblies in
inline PowerShell or retry blocked CPU profiling.

Next: explicitly build/deploy this checkpoint, qualify the small activation
lane, then measure the expanded selection with fresh and restored Gagarin caches.
Keep existing searches and DDS/PNG readers identical, the other concurrency
experiments disabled, and menu observation/normal automatic exit enabled.

## Bounded scalar prototype

Checkpoint d638d11 passed activation and expanded fresh/restored-cache runs.
Expanded fresh timing measured 3640.991 ms outer stage and 2419.267 ms in
DefFromNodeNew. Detailed profiling measured 272.224 ms in all non-generic
ParseHelper.FromString calls, 160.553 ms field resolution, 184.059 ms type
resolution and 300.654 ms native complex/empty construction. These are nested,
instrumented intervals. Field/list delegate retrieval was 509.630 ms inclusive
of classification and first-time generation; it is not all compilation.
The restored-cache reverse profile/timing pair measured 2356.235 / 2079.771 ms
outer stage, demonstrating material diagnostic overhead. Performance trials use
timing-only diagnostics. Both paths still convert all 19,534 concrete nodes.

The native Def base constructor initializes debugRandomId from shared Rand.
StatModifier has an empty constructor, but its custom loader registers a
reference before parsing its float. Its 23,000 expanded list calls total only
30.142 ms inclusive. These are specific boundaries, not a claim that all
constructors or custom loaders are intrinsically unsafe.

`--def-conversion on` tests speculative numeric/Boolean preparation after native
inheritance and before native conversion. Owner-thread XML traversal extracts
at most 65,536 ordinary text-node strings with a 16 MiB conservative logical
reservation and two-million-node visit limit. Two workers plus the owner run
only BCL invariant float/int/Boolean parsing over disjoint private jobs. All
join before the first Def constructor. Budget exhaustion retains the admitted
prefix and all other nodes use native conversion. No XML or Def is cloned.

The generated single-text-node parser call additionally receives its existing
node identity. The original generated branches, construction, assignment,
custom loaders, reference registration and publication remain ordered. A hit
requires the exact node/text/type, unchanged native registered delegate and no
foreign parser hooks. Unknown, malformed and permissive-warning cases retain
native parsing. `verify` separately compares every hit with the original
parser at the original callsite, including float bit patterns. This mode is
correctness evidence, not a performance arm.

Every new timing/candidate arm also fingerprints direct post-resolution ThingDef
fields, stat values and ordered category/stat reference names, and verifies that
those references are the actual DefDatabase objects. This focused check is not
a general object serializer or gameplay qualification. Admission, scanning,
worker lifetime/joins, consuming/fallback calls and complete outer stage costs
are reported separately. Preparation failure joins started workers and falls
back; no arbitrary game/mod callback executes on a worker.

The initial broad offline test invocation was stopped at its own test host after
it failed to finish promptly; its log remains conversion-tests-01.log. Focused
fixture tests are available through `test --test-filter` and still run all
fixture Python checks. This skips unrelated bootstrap/core qualification and
does not claim it passed. The source builds without warnings/errors.
