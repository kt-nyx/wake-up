# C12: loading searches

## Steward functional review passed — 12 September UTC

Reviewed clean `3701b34a50d904239f92f71fac6cb14046c70b51`, final tested
source/core/observer `3d428a59f076b8cf50f98d1391a23f0b2efb8669`, with the
earlier tested XML/leaf and R1 identities kept separate below. The parent and
independent reviewers found no remaining blocking correctness issue. C12 is
**implemented / functional review passed / performance pending**, not fully
accepted or release-qualified. C13 may proceed under the approved sequence.

Coverage assessment:

- **7a:** native name/construction-delegate behavior was verified with suppliers
  absent; retained ordered Harmony resolution and leaf behavior remain. The new
  startup scope performs real additional leaf work before AlertsReadout: 129
  full scans avoided with Proton retained and identical ordered patch targets.
  This satisfies the additional real-consumer requirement; zero supplier-absent
  startup leaf counters are not used as evidence. Native memoization covers the
  inspected repeated broad-base requests. No universal absence of other uncached
  discovery or supplier-absent Proton behavior is asserted.
- **7b:** actual field/method/property/field-array reuse and inner-helper boolean
  attribute reuse execute during loading and language reload, preserving native
  invocation, mutable results and attribute-construction effects. Both inspected
  deserializer paths retain existing native stores, including construction
  delegates. R1's unsafe generic-helper bypass is removed; R2 changes only the
  internal non-generic lookup, preserving the original closed helper dispatch.
- **1e:** retained live indexes/syntax reuse are extended to actual custom
  single-node and eager XML Extensions queries. Native node/order/mutation/error
  semantics and processed-cache coexistence match the controls. Arbitrary lazy
  custom results stay native; this is deliberate native-contract preservation,
  not a claim that every possible XPath expression is indexed.

The parent independently checked ordered XML/operation/source/Def records,
translation samples and all three native lifecycle snapshots. R2 language output
matches after only the exact two empty observer diagnostic rows; captured hashes,
tested DLL hashes and run purposes/exits match. Seven restored baseline hashes
and five absences match; no fixture/build/test process remained at review.
Raw parent reviews are in `c12/steward-evidence-review.json` and
`c12/r1/`, `c12/r2/steward-evidence-review.json` under the campaign artifacts.

Positive foreign generic-hook execution through compiled Mono and a separate
cached-Prepatcher launch are not claimed. Preserved call structure, focused
non-generic-hook checks and actual native behavior provide proportional evidence
for this implementation; do not repeat the failed intrusive generic probe.
Performance remains a substantive gate: separately measure member/attribute,
live-query and early-leaf contributions plus combined loading and later entry,
including admission/locking, cold setup, generation invalidation and cleanup
costs. Operation counts alone cannot qualify a speedup or hide a regression.
Search completion does not authorize removing FGL/YaOpt while asset replacement
is unfinished, or removing Performance Fish/Proton gameplay features.

## R2 handoff — attribute reuse inside the native helper — 12 September UTC

C12 now reuses attribute-existence results during ordinary startup and later
language loading. The native generic helper still receives every original call;
Prepatcher changes only its internal, non-generic `Attribute.IsDefined(MemberInfo,
Type, bool)` call. This preserves the helper's entry, actual attribute type,
inheritance argument, return and external hooks. It does not assume Mono gives
each closed generic helper independent machine code.

Tested source/core/observer is `3d428a59f076b8cf50f98d1391a23f0b2efb8669`,
descending from the exact steward-reviewed R1 handoff `8dce1a0`. Twelve member
lookup consumers remain. `DirectGetFieldByName` additionally establishes an
attribute loading scope with a prefix/finalizer; it has no member transpiler.
Existing type-search selection/defaults and translation composition are retained.

The new store holds only booleans, keyed by the member, actual runtime attribute
Type and inheritance flag, with a 32,768-entry limit. It shares the existing
startup/language lifetime and assembly-generation invalidation. Custom metadata,
dynamic/open types, invalid arguments, option-off, pre-initialization and calls
outside admitted loading scopes use the exact native operation. Native errors
are neither caught nor cached; attribute instances and constructors are untouched.
Native work runs outside the cache lock.

The early rewrite qualifies the exact original body, core signatures and method
generic constraint. Runtime admission checks the actual rewritten helper body
and constraint, plus the shipped non-generic operation's module/IL identity.
The concrete operation has an all-hook publication guard. No guard on an open
generic helper is used to infer closed/shared patch state. Admission does not
depend on a fresh rewrite flag, so a cached rewritten assembly is inspected by
the same contract; no separate warm Prepatcher launch is claimed here. Missing
or changed rewriting records native fallback rather than reported activation.

### R2 exact evidence

Both packages are under their ordinary ignored package roots at the full source
revision above. Core DLL SHA-256 is
`a7c0bdeef0b5258d7f673e23dddc7afc1404b3a66a01ca7e1e04b10c257741e9`;
observer DLL SHA-256 is
`3cddf23a67340df5dc5cc8810a88595be40098ac4edee147d506ed7039d848bd`.
The GOG generation, game identity and frozen manifest are unchanged.

Twenty focused managed checks and 71 fixture-tool checks pass. Thirty-seven
modern-host reflection/translation checks pass, including both translation
installation orders, preserved original generic call operands, preexisting/late
non-generic operation callbacks and exceptions, and scope/option-off fallback.
The focused checks cover distinct actual attribute types, true/false results,
inheritance, custom metadata, repeated errors, construction/mutation effects,
generation clearing and missing/changed/duplicate rewrite refusal.

| Final capture | Functional result |
| --- | --- |
| `c12-r2-attributes-01` | Same 14-mod French selection/settings as the retained native control. Ordinary loading has 554,733 attribute hits and 5,760 misses before the observer probe. The probe preserves all six actual native call instructions and twice checks NoTranslate, Unsaved and permitted list-count decisions. Final attribute totals are 554,739 hits/5,766 misses; member hits are 155,437; retained metadata is zero. |
| `c12-r2-language-02` | Existing 12-mod CE/VEF selection with the combined XML/language settings, then native Chinese and French reloads. Four later injection scopes record 579,729, 14,840, 541,577 and 12,688 attribute hits; each clears all retained metadata. Both reloads report fresh objects/tables and restore the original language selection. |

Both captures have `automaticTestPassed=true`, normal exit code 0. Their run
SHA-256 values are respectively
`f7909156af7666f610127ff92c07f96599cc1dc3048c599a43840814d51fe35d` and
`c40c7f5f2c7a1c21f66e75daa2d019935844eec1b4958af99aa0d6f1fb8385a3`.

Ordered XML records and translation samples equal the matching retained native
controls. Full language observations, including all three lifecycle snapshots,
equal their native controls after removing only the two exact empty observer
diagnostic rows described in R1. Independent read-only review rederived the
comparisons from saved files and found no blocker. Later-language hits come from
native reload work; first-run probe work is separately counted above. These
counts demonstrate avoided lookups, not a loading-speed benefit.

All six R2 fixture transactions are restored in reverse order. Seven baseline
hashes and five expected absences match; metadata audit passes. The final named
process snapshot contains no RimWorld game or companion process; C12 did not
close or modify the normal applications. Evidence, exact settings/selections,
identities, comparisons, review and restoration are in ignored
`artifacts/ecosystem-next-20260910/c12/r2/`.

Work is stopped and exclusive ownership returns to the steward for independent
C12/row 7a/7b/1e assessment. R1's failed intrusive Mono generic-detour capture
remains failed and was not repeated; positive foreign generic-hook execution
through the compiled Mono setter remains unqualified. The revised boundary
preserves that dispatch structurally and tests actual native setter behavior.
No scope exclusion or self-acceptance is declared. Individual/combined performance
and release obligations remain open. No performance experiment, successor,
Steam/Deck promotion, normal-data change, UI operation, merge, push or publication
occurred.

## Steward R1 review and remaining attribute implementation — 12 September UTC

The steward reviewed clean `c26137b` and tested source `e2b575f`; a separate
read-only reviewer confirms the attribute bypass is removed and the other twelve
metadata consumers remain. Original XML and translation samples match exactly.
The parent verified that language output matches after excluding exactly the two
empty observer-only diagnostic rows described below, verified captured hashes,
and rechecked seven restored baseline hashes/five absences. The failed intrusive
probe remains failed. This closes the R1 correctness defect, not C12 acceptance.

Continue the remaining attribute-reuse requirement in the same C12 task. The
materially different design preserves every actual `GenAttribute.HasAttribute<T>`
call and changes only its internal metadata lookup using supported Prepatcher
rewriting. The native helper passes the actual attribute Type and `inherit:true`
to `Attribute.IsDefined(MemberInfo, Type, bool)`. A bounded loading wrapper can
reuse that lookup while leaving the surrounding helper and its mod hooks in the
call path. Verify and guard the exact three-argument native overload, preserve
custom member behavior and errors, and fall back outside admitted loading scopes.
This is an implementation direction, not evidence of delivered attribute reuse.
Do not revive the failed generic-detour experiment or introduce a global patch
barrier. Real avoided attribute work and native-equivalent behavior must be
reviewed before C12 can proceed to C13. Performance remains separately pending.

## Steward correction R1 — attribute calls remain native

R1 returns from clean `dcd6275` with corrected functional evidence. The earlier attribute cache guarded
the open generic helper, which cannot qualify hooks on its closed versions or
Mono's shared code. The correction preserves the original native attribute call
instructions and removes the attribute cache, wrapper and guard. It does not
assume generic specializations have independent machine-code implementations.

Field, method, property and field-array reuse remain in 12 loading consumers.
`DirectGetFieldByName` stays entirely native because its only earlier replacement
was an attribute call. Attribute acceleration within row 7b remains open for
steward assessment; the earlier 88,563 attribute hits are historical evidence of
the removed route, not current product coverage. The prior captures and other
valid findings remain; this correction does not require their repeated execution.

The early live closed-helper hook probe at `3186baf` failed before its cases ran,
with unrelated loading errors. Its verified fixture process was closed after
preserving evidence (`c12-r1-attributes-01`, failed). The probe is intrusive and
unqualified; this is not proof of a product defect or a fully diagnosed Mono
limitation. No further live generic detours are used. The corrected observer
checks exact effective native attribute-call instructions and ordinary attribute
decisions through the real setter. Modern-host hook evidence applies to entering
the preserved call operands; positive foreign-hook execution through the actual
Mono setter remains unqualified. This limit returns to the steward explicitly.

### R1 qualification and remaining coverage

Final tested source/core/observer is
`e2b575f5a6effae4e7b1623bfcb2da11d8ebc805`. The production correction was made at
`3186baf`; the later commit only narrows the observer and records the failed probe.
Core package `artifacts/fixture-package/e2b575f5a6effae4e7b1623bfcb2da11d8ebc805`
has DLL SHA-256 `29c60a030afd86b6bb8f2aaf2a276cf9e8b1753b8be8abfb046976cb09481985`.
Matching observer package under `artifacts/fixture-menu-observer/` has DLL SHA-256
`41528a68fe46f75dd11ad87b61aff5efd65221d66f62c553186f32a294b01b79`.

Seven member-index checks, 71 fixture-tool checks and 33 modern-host
reflection/translation checks pass. The latter verify exact closed call operands
in both translation installation orders; preexisting/late hook returns,
side effects and exceptions when entering those operands; and unaffected native
member-lookup reuse. The compiled CoreCLR setter did not enter the closed detour
in that host, although reflected operand entry did. The tests make that limit
explicit instead of claiming compiled caller compatibility from it.

`c12-r1-native-attributes-02` uses the same 14-mod French workload/settings as the
retained native control `c12-mixed-native-01`, on the unchanged GOG generation.
It has `automaticTestPassed=true`, normal exit 0; run SHA-256
`d16c021970da7ff5b4dd5fa6368ff7db98e6676dca0d65d92def3e05bbd81d63`.
The actual Mono method after all transpilers retains all six native attribute
call opcodes and closed method operands. The real setter twice rejects the
NoTranslate and Unsaved fields and twice accepts the permitted list translation.
No live attribute-helper hooks are installed in this successful run.

The member store has 155,422 hits before the small probe and 155,437 afterward.
Final totals are 124,540 field, 279 method, 24,664 property and 5,954 field-array
hits, 4,266 misses, zero attribute hits and zero retained metadata. Probe work is
included in the final totals; the earlier hits establish ordinary loading reuse.
None of these counts establishes performance benefit.

Ordered XML output and translation samples match the retained native control
exactly. The full language observation matches after removing only one new,
empty `FixtureMenuObserver.C12AttributeProbeDef` diagnostic row from each of the
active/default language package lists (both have empty errors/suggestions and
`legacy=false`). This observer-only type did not exist in the older control;
no other language difference is normalized or discarded. Independent read-only
review rederived these comparisons and found no blocker for removing the bypass.

All eight R1 fixture transactions are restored in reverse order; seven original
hashes/five absences match and the metadata audit passes. The failed early probe
remains failed, including its forced-closure identity and pre-closure log. Its run
hash is `ad13b23fbd435a3604abe12c6c3707c5e4cc7d478e151007517c5edee711d158`.
R1 raw evidence is under `artifacts/ecosystem-next-20260910/c12/r1/`, including
`capture-identities.json`, `native-comparison.json`, `language-comparison.json`,
`final-endpoints.json`, `final-audit.json`, `final-processes.json` and `rollback.log`.
Fixture/build/test and child operations are stopped; exclusive ownership returns
to the steward with the clean documentation handoff. Normal game/data remain
untouched; no performance test, publication, successor or platform promotion.

**Remaining row 7b question:** suppliers such as Performance Fish avoid repeated
attribute-existence searches during loading. In this code the concrete repeated
queries include NoTranslate, Unsaved and TranslationCanChangeCount in translation
application, plus Unsaved checks in XML field discovery. These still execute
natively after R1; they are not replaced acceleration or proved native caching.
A future route would need to avoid that repeated search while preserving actual
closed/shared-helper dispatch and observable effects. R1 supplies no qualified
route for that behavior. The steward owns the next design/scope decision; this
handoff does not exclude attributes, accept row 7b or broaden the investigation.
Positive foreign-hook execution on compiled Mono also remains unqualified, as
agreed by the steward; retaining native dispatch makes no stronger guarantee.

## Preserved C12 implementation and earlier evidence

Wake-Up now reuses member descriptions during loading and extends live XML
queries to an additional custom patch family. Native resolvers, constructors,
setters and custom loaders still execute. The mixed native/candidate comparison,
cache coexistence, language reload and retained Proton startup checks pass.
C12 is ready for independent steward assessment, not self-accepted. Performance
and release acceptance remain pending.

The assignment began at clean `6a1c117b1505d273bd9ef947868ef910e365f469`, on
`codex/ecosystem-next-plan-review`, with exclusive C12 ownership. Main remains
unmerged. The single GOG fixture and normal-game boundaries remain.

## Implemented behavior under qualification

Reflection means discovering fields, methods and properties by their names.
The new loading-local metadata store avoids repeating that discovery in native
custom XML cross-reference registration, `XmlHelper`, language injection and
`GenGeneric` consumers. Twelve exact consumers have independent admission after R1.
Keys contain the actual closed type, exact name, binding flags and member kind
for the supported overload. Other signatures and custom/dynamic reflection types
remain native. Errors are not cached. Each field-array caller gets its own copy;
objects and mutable attribute instances are not cached. Attribute queries retain
the exact native helper calls; R1 removes the earlier attribute-existence cache.
Invocation, setters, constructors and custom loaders remain native.

Assembly changes discard metadata; startup completion releases the store. Later
language injection has its own bounded scope. Exact new hooks compose with the
retained translation optimization in either installation order; unknown hooks
disable only their affected consumer. Reuse follows the existing type-search
selection, default on. General gameplay reflection is not intercepted.

Live XML queries retain native nodes, order and errors. Literal indexes now
accept whitespace and consecutive predicates after a unique identity. Guarded
custom single-node calls can use the same index. The optional XML Extensions
adapter covers exactly `PatchOperationExtendedPathed.PreCheck`, whose native
caller fully counts results before later edits. That count and ordered mutation
remain, including multi-node results. Other custom lazy lists stay native.
Absent/changed suppliers fall back; XML Extensions is not a required dependency.
The adapter follows `xmlQueryExtensions`, still default off. Mutations invalidate
selected results and misses; nothing is persisted. Existing processed XML,
definition/template indexes and syntax-plan reuse remain.

The leaf proof is also available during startup before alert construction. It
publishes only fresh proven-empty lists into the native cache, then still runs
the native predicate. Existing mutable lists and nonempty discovery win. One
predicate thread owns the temporary proof; other threads stay native. Retained
Proton constructor discovery is the source-backed additional caller. The first
retained run exposed an overly broad guard: Proton's separate alerts-screen
constructor postfix disabled unrelated startup discovery. Startup now checks
the five native leaf/type methods independently; the constructor scope still
checks all six methods. Explicit constructor-entry tracking prevents a refused
constructor from entering through the startup scope, including on exceptions.
The corrected retained functional run avoids 129 full type scans during real
startup discovery, before the ordinary alerts-screen constructor.

## Native coverage and remaining discovery

GOG already caches `GenTypes.GetTypeInAnyAssembly`, construction delegates,
deserializer direct/hierarchy/alias field lookup and custom/post-load methods.
Both deserializer branches use those stores. They are preserved, not duplicated.
Existing Wake-Up Harmony fallback indexing preserves resolver precedence and
assembly generations. FGL/BetterLoading name caching overlaps native behavior;
YaOpt fallback work overlaps Wake-Up's retained index.

Fresh native and bounded unrelated-mod inspection found broad first-base scans
but no additional supplier-absent empty leaf request. Per-base stores do not
eliminate every different-base first scan. Replacing the unordered parallel input
with another list could alter published order; that route was not adopted.
Proton is supplier-present coverage with actual work avoidance. These findings
do not prove uncached discovery is absent across modlists. The steward must
assess row 7a against the preserved native coverage, retained resolver/leaf
behavior and this new startup consumer; no supplier-absent leaf improvement is
claimed from zero hits. Search work does not justify removing FGL/YaOpt asset
capabilities or Performance Fish gameplay features.

## Evidence and remaining qualification

Focused validation passed 101 managed and 71 fixture-tool checks. The modern-host
reflection/translation suite passed 21 checks, including native custom loaders,
constructor/attribute effects, closed generic keys, list ownership, generation
changes and foreign-hook fallback. Early compiler/host failures are preserved.
Three older C10 test casts were updated to NUnit's current `Action` overload;
no C10 runtime changed.

No performance run or microbenchmark is authorized. Later
matched individual/combined measurements must bind exact source, package,
settings and content; operation counts do not establish a speedup. Raw evidence
and detailed source mapping are under ignored
`artifacts/ecosystem-next-20260910/c12/`.

### Exact tested identities

All captures below use GOG generation `gog-rev573-20260910-194017-cf1a1eb0`,
game assembly SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` and
frozen manifest `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
The initial comparison source is `97d58a18a77ddbd318c1af45aebe9b62c63be5bb`.
Core package is `artifacts/fixture-package/97d58a18a77ddbd318c1af45aebe9b62c63be5bb`,
DLL SHA-256 `119c82721be52558370b6634a4850565d99e1f741b100a24ed6b4bcfad28cf16`.
Observer package is `artifacts/fixture-menu-observer/97d58a18a77ddbd318c1af45aebe9b62c63be5bb`,
DLL SHA-256 `c1f97c8b152c8302da85cda89f6623c452b891fcdcb3349c72359f114c30f12c`.
The final leaf correction has its separate tested identity below. Later
documentation commits do not change either tested binary set.

Every run is functional, uses the independent menu observer, requests a minimized
nonactivating window, and waits for normal automatic exit. No duration here is
performance evidence. The ordinary Steam game remains outside the fixture and
untouched.

### Actual startup and cache behavior

The 14-mod workload combines official content, VEF/Vanilla Animals/Vanilla Plants,
Combat Extended, XML Extensions and Reinforced Walls Continued. The latter's
ordinary enabled patches exercise the additional XML queries; no artificial
production call was inserted. Selection and settings are preserved in
`c12/selection.json` and `c12/settings.json` under the evidence directory.
The settings enable `translationApplication` and `xmlQueryExtensions`.

| Captured run | Result and meaning |
| --- | --- |
| `c12-mixed-native-01` | Wake-Up physically absent; French native output and native name/delegate cache probes pass. |
| `c12-mixed-candidate-01` | Ordinary user activation; native-equivalent captured outputs; all 13 metadata consumers admitted, 243,984 metadata hits, zero entries retained after startup. |
| `c12-combined-cold-01` | Same content plus parsed/processed XML, inheritance and language options. Same outputs; processed XML correctly refuses the existing XML Extensions `ApplyPatches` hooks and keeps patching native. |
| `c12-cache-cold-01` | Previously supported C02 12-mod CE/VEF selection; publishes processed XML and inheritance caches. |
| `c12-cache-warm-lifecycle-01` | Restores the four owned stores from the preceding run. Initial output equals cold; native Chinese/French reloads pass with fresh objects and normal exit. |
| `c12-cache-native-lifecycle-01` | Wake-Up physically absent on the same 12-mod workload; complete initial French, reloaded Chinese and returned French snapshots equal the optimized lifecycle exactly. |

All six have `automaticTestPassed=true`, exit code 0. On the 14-mod runs,
ordered captures match for 18,625 XML consumers, 114,629 resolved records,
3,022 operation states, 2,025 sources and 5,629 ThingDefs, plus language data
and translation samples. The independent reviewer inspected the original ordered
records as well as the summary. Existing translation errors are unchanged.

The candidate records 2,860 indexed live queries, including nine newly adapted
eager custom queries and one expanded literal single-node query. The retained
Harmony resolver handles eight fallback requests with one ordered index build,
five assembly generations, no errors and no original-fallback escape.
The native name/construction-delegate probes verify native dictionary insertion,
repeat reuse and unchanged method patch state with suppliers absent; constructor
and custom-loader side effects also have focused managed coverage.

On the 12-mod cold/warm pair, all initial ordered XML records and language output
match, including 5,620 ThingDefs. Warm processed XML skips 2,998 patch calls,
replays 43 observed mod lookups and preserves 39 live wrappers. Parsed XML
avoids 2,024 source parses; inheritance avoids 8,205 child merges. Parsed language
reuses keyed/string records while CE's known injection hook preserves native
injection parsing. These are coexistence checks, not evidence for live-query
coverage or measured loading benefit.

The warm lifecycle selects Chinese then French through the game's own language
reload. Both steps report fresh language, keyed/string/injection tables and Steel
definition objects; final French selection is restored. The new reflection store
executes in later injection scopes and reports zero retained metadata at each
scope end. This checks real later loading beyond first-menu startup.

Independent review caught a changed full definition label/description digest
between initial and returned French. The focused native lifecycle control then
reproduced that change exactly: initial `5a839d52...`, Chinese `b4800454...`,
returned French `31f49ec4...`. All fields of all three language snapshots match
the optimized run. This establishes native-equivalent reload behavior on the
mixed workload; it does not claim the native reload restores every original
translated definition string. The comparison is recorded in
`c12/lifecycle-native-comparison.json`.

### Corrected startup leaf result

`c12-retained-proton-01` adds the frozen `vr.missilegirl` package to the 14-mod
selection. It passes but reports zero admitted startup leaf predicates. Captured
Proton logs and shipped `IPatchInfo` code establish that constructor discovery
executes eagerly. The prior guard incorrectly coupled it to a separate
`AlertsReadout` constructor postfix installed earlier by Proton.

The bounded guard correction is source
`b963fcb74edda67adafc2b63684693fc783543b3`. It changes only leaf runtime admission
and focused tests; the reflection/XML implementation remains the preceding
tested implementation. All 26 focused leaf tests and 71 fixture-tool checks pass,
including native foreign leaf/type hooks, actual foreign constructor effects,
return/exception cleanup and startup cutoff. No new supplier allowlist is used.

Matching final packages:

- Core: `artifacts/fixture-package/b963fcb74edda67adafc2b63684693fc783543b3`,
  DLL SHA-256 `2a25a7a4a4f0fbcac7fe55560dde85c97f5a1effe526ca5b7cda25872459e465`.
- Observer: `artifacts/fixture-menu-observer/b963fcb74edda67adafc2b63684693fc783543b3`,
  DLL SHA-256 `8111574f8196a3e9e17b670c532ad796cd9152daa87fe9f9a6ccf1ae5ff317a5`.

`c12-retained-proton-02` passes normal automatic exit, code 0. It records 137
startup leaf predicates, 129 avoided full scans and eight native predicates.
All 130 ordered Proton alert patch records match the preceding run, as do
captured ordered XML, language output and translation samples. Reflection again
records 243,984 hits and zero retained metadata. The ordered resolver handles ten
fallback calls through three builds across ten assembly generations, with no
errors or original-fallback escape. This is actual additional startup work
avoidance with Proton retained, not a loading-speed measurement or evidence for
unrelated gameplay optimizations.

### Restoration and handoff limits

All 16 C12 fixture transactions were rolled back in reverse order (the native
lifecycle control's two transactions were restored before the final package).
Seven original endpoint hashes and five original absences match the initial
handoff; the metadata audit passes. Original core/observer deployment, GOG game,
mod exclusions, preferences and mod order are restored. The ordinary Steam game
was not closed, operated or modified. Captures and private evidence remain under
ignored `artifacts/` and fixture results; no fixture data enters Git.

The eight captures are all functional passes with normal exit. Exact run hashes,
package identities, settings and selections are in `c12/capture-identities.json`;
restoration is in `c12/final-endpoints.json`, `final-audit.json` and rollback logs.
Independent read-only review of the initial capture sequences, native lifecycle
control, final leaf correction and retained capture found no blocking issue.
The final reviewer independently rederived all 130 ordered Proton patch records
and checked byte-identical XML/language/sample outputs and all three native
lifecycle snapshots. C12 work is stopped and exclusive editing/operating
ownership returns to the steward with the clean final documentation commit.

The handoff does not grant C12 acceptance or amend the approved matrix. Native
name/delegate behavior is covered, the new reflection/query work executes, and
one additional real startup leaf consumer is proven. Broader supplier-absent
empty-leaf discovery was not found in the bounded inspection. The steward owns
the row 7a/7b/1e coverage assessment and any required same-owner correction.
Individual/combined performance, release readiness, Steam promotion and all
prior campaign obligations remain separate and open. No performance experiment,
merge, push, publication, normal-game change, desktop automation or Deck access
was performed; no successor is dispatched by C12.
