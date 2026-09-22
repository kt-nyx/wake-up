# C04 language performance qualification — 12 September 2026

Translation application has a measured benefit on the French official-content
workload. Parsed-language reuse preserves native output and avoids parsing, but
does **not** improve startup on either measured workload. Two bounded changes
reduced its storage cost without meeting that performance requirement. C04 is
therefore **not fully accepted**. The original functional handoff and its
independent review remain historical evidence, not approval of these changes.

This is a STOPPED handoff to the steward for independent review. The fixture is
restored and exclusive checkout/build/fixture ownership is released with the
handoff message. No merge, push, publication, Steam promotion or production
adoption occurred. Parent current-state, ledger and coordination files were not
edited by this task.

## What was measured

The question was whether reading previously parsed translations saves enough
work to repay source checking, cache restoration and setup. Applying those
translations to game definitions is a separate operation and was toggled
separately.

- Started from exact clean `3dccf1024b49c7aa4fb7eeeb41433c045123bb8d` on
  `codex/ecosystem-next-plan-review`, preserving all later-slice implementation.
- Used only isolated GOG generation `gog-rev573-20260910-194017-cf1a1eb0`;
  manifest `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`;
  game assembly `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
- Before every measured launch, checked active window
  `owner-unattended-20260912-173527` in the steward state. The owner grant had no
  fixed cutoff. All game instances were stopped before measurements. No child
  agents ran; builds, tests, source scans and other substantial local work paused
  during measured processes. Per-launch window receipts are retained.
- Every measurement used `fixture.py`, explicit performance purpose, French,
  muted audio, minimized nonactivating launch, the independent menu observer and
  normal automatic exit. The endpoint is `menuReadyObservation.elapsedSeconds`.
- Official-content lane: eight selected packages, ten active including Wake-Up
  and observer. Mixed lane: twelve selected, fourteen active, adding VEF,
  Vanilla Animals Expanded, Vanilla Plants Expanded and Combat Extended.
  Gagarin (`vr.missilegirl`) and Loading Progress (`ilyvion.loadingprogress`)
  were physically excluded from fixture discovery. Harmony and Prepatcher stayed.
- Ordinary saved settings held other selectable features off, with
  `loadingTimings=true`; existing definition/type-search defaults stayed the same.
  The exact JSON settings are under the evidence directory. XML comparisons
  additionally enabled parsed XML, processed XML, inheritance reuse, ordered
  input and XML query extensions in both arms.
- Each parsed-language sequence was native, cold, warm, warm, cold, native.
  Both warm runs imported the **same cold capture**, never the preceding warm
  capture. Cold means an empty language store, not a flushed Windows file cache.
  Preparation reset other caches unless explicitly restored for the XML pair.

## Parsed-language results

Avoided work alone did not make the feature faster. Every measured official
warm run skipped 91 keyed files, 1,451 DefInjected files and 186 Strings files:
1,728 native parser calls avoided, with zero native files in that French load.
The complete output still matched native loading.

| Implementation / capture prefix | Native menu A / B | Cold menu A / B | Warm menu A / B |
|---|---:|---:|---:|
| Original group files, `c04-perf2-fr` | 12.758 / 12.847 s | 15.943 / 15.730 s | 13.765 / 13.782 s |
| Packed groups, `c04-pack-fr` | 13.020 / 13.071 s | 13.873 / 13.681 s | 13.529 / 13.349 s |
| Packed groups and shared strings, `c04-symbol-fr` | 13.154 / 12.948 s | 13.709 / 13.701 s | 13.285 / 13.194 s |

All three sequences lose both directional comparisons. Different sequences are
different built packages and host moments; their menu differences are not a
matched measurement of one implementation against another.

The existing product counters identified a concrete storage cost in the first
sequence: 585 individual group publications consumed 2.383–2.455 seconds cold.
Warm store opening alone cost about 0.23 seconds, outside the native loader's
own timing marker. Rechecking the published Harmony contracts cost only a few
milliseconds; weakening those guards would not address the major cost.

The retained changes address that measured cost:

1. One authenticated owned-store record packs a load context's groups. Each
   group retains its complete source text, path, order, actual definition context
   and predecessor-state key. A context-only match never admits a group.
   Native parsing, callbacks, ownership and merge order remain in place. The
   bundle contains detached bytes, with at most 32,768 groups / 128 MiB; existing
   per-group 32 MiB bounds remain. Publication uses the existing atomic,
   quota-controlled owned store. Failure preserves ordinary loading.
2. The record format shares repeated immutable strings within each payload.
   Every current field is still visited and its exact ordinal contents encoded.
   Records and mutable lists are reconstructed freshly. This is format magic
   version 2, inside the existing owned category; runtime-module identity and
   the bundle key prevent old records being misidentified as current ones.

The captured French-plus-English store fell from 666 individual records totaling
11,178,382 bytes to two records totaling 6,320,018 bytes. This reduces storage
work but is **not** itself a loading-speed claim.

On measured source `392effe0`, native French loading took 0.456–0.483 seconds;
warm parsed reuse took 0.694–0.697 seconds inside the same native marker.
Warm setup additionally took 80–87 ms for feature initialization and about
17–18 ms for the language session's begin hook; end/ownership completion took
9–12 ms. Inside the loader, full source-key work took 123–131 ms and injection
predecessor encoding 90–105 ms. These are nested cost explanations, **not values
to sum with the loader or menu totals**. The remaining correctness-preserving
checking/reconstruction work still exceeds the parsing it replaces.

## Separate translation-application result

The existing application optimization improves the larger task of assigning
translations to definitions. Parsed-language reuse was off in all four arms.
The chronological comparison was `c04-symbol-fr-off-b`, `c04-app-fr-on-a`,
`c04-app-fr-on-b`, `c04-app-fr-off-b`, all on source `392effe0`.

| Arm | Menu | Early application | Later application |
|---|---:|---:|---:|
| Native A | 12.947853 s | 2.419938 s | 0.012084 s |
| Enabled A | 11.457738 s | 0.766975 s | 0.061790 s |
| Enabled B | 11.755368 s | 0.736727 s | 0.054561 s |
| Native B | 12.791140 s | 2.328994 s | 0.012279 s |

Menu savings are 1.490115 seconds forward and 1.035772 seconds reverse. The later
pass is slightly more expensive with the feature; it is not hidden in the early
pass result. Both enabled runs recorded 26,572 lookups, 27,814 seed visits,
448 completed package scopes, zero fallback scopes and zero retained entries.
All four complete text snapshots were identical. This is bounded evidence for
application on this workload, not a universal percentage or parsed-cache gain.

## Mixed compatibility and XML interaction

The mixed workload confirms that the protected compatibility path stays native,
but it does not establish useful partial language-cache coverage.

`c04-mixed-perf` native menus were 31.875753 / 19.876822 seconds; cold menus
20.592009 / 20.717988; warm menus 20.416430 / 20.834724. The first native result
shows substantial first-workload drift and is retained, not averaged into a
speedup. The reverse native control beats both warm runs. Native French loading
was 0.460 / 0.569 seconds versus warm 0.719 / 0.693 seconds. The real English
load recorded before menu was also slower warm: 0.188 / 0.166 seconds versus
native 0.087 / 0.047 seconds.

Warm mixed French reused 97 keyed files and 188 Strings files; all 1,451
DefInjected files remained native under the CE/VEF converter guards. English
reused 132 keyed and 75 Strings files, leaving two injection files native.
Complete snapshots matched both native controls, including the same existing
232 translation diagnostics from the mixed content. Those diagnostics are not
new cache failures; neither keyed nor injection XML-parser error flags appeared.

The XML interaction used `c04-xml-fr-cold` and
`c04-xml-language-fr-cold` as the respective fixed cache sources. Each arm's two
warm runs imported its own same validated cold capture. The seven parsed-XML,
one processed-XML and one inheritance payload hashes matched exactly across
the language-off/on controls. Both arms reused 1,558 XML files, skipped 29
processed patch calls and restored 4,554 inherited children. The combined arm
also skipped all 1,728 French language parser files, with identical final text.

XML-only warm menus were 14.127127 / 14.180447 seconds; XML plus language menus
were 14.400148 / 14.654200 seconds. Thus coexistence is demonstrated, but adding
language reuse has no useful gain here. This small official-content workload
does not qualify XML's general performance or any combined-release percentage.

## Verification, identities and limitations

Evidence distinguishes the measured package from the final functional package.
The final source only adds payload-bound enforcement for references/nulls and
preserves the former text capacity; it was functionally rechecked, not retimed.

| Identity | Exact value |
|---|---|
| Last measured core and observer source | `392effe06cc703f08325f4cba76855972ee62466` |
| Measured WakeUp.dll SHA-256 | `af8707f7a4711fb80806181cbf2f4b3558f7d5e2c1c2018d4338fdf43d892324` |
| Measured observer SHA-256 | `85c879384c4f4de95f185a986ca3d919960978c8c52374d29fb8c564ab6e50ce` |
| Final core and observer source | `542b8e1375ee00e7753a837560fb2822919d3b7d` |
| Final WakeUp.dll SHA-256 | `0744cd1563232ba5b079df199092cfadce09e5a01f67e3b149569dfe5670b4f3` |
| Final observer SHA-256 | `d8804b54322bd06a69e48734a9ba7e01609bc9e8a02ca92562b2c5621bbe5a8e` |

Revision-named packages remain in `artifacts/fixture-package/` and
`artifacts/fixture-menu-observer/`. Every capture has its exact source, deployed
package receipts, settings, order, cache source and content evidence in
`.rlo-test-instance/results/<label>/run.json`.

Final checks: 27 focused main checks; 46 language/translation checks including
native cold/warm record behavior, prior-state changes, source changes, foreign
converters and malformed bundle/string references; 65 fixture checks for the
measurement tooling change. Matching core/observer package builds passed.
Final live captures `c04-final-fr-cold` and `c04-final-fr-warm` passed normal
exit, all three language-family reuse and full output equality. They are
functional captures and make no speed claim.

There are 39 normal-exit captures: 34 performance-purpose runs and five functional
runs. Two preliminary captures have failed timing-observer installation and are
excluded from qualification; normal exit alone does not make those tests pass.
Their complete
official French observation hash is
`d5685da534af5853711f3bdccfd7d315788c0e3e323c8ce52bb9b2da1c41b6ad`;
the mixed hash is
`c58a57180fa8d0abf32a1d777b3c5a51fb0494dca1bc2e2b8b79ca5e441fa744`.
Official observations include 8,192 keyed records, 186 Strings groups,
26,572 injection records and all 3,415 ThingDef labels/descriptions.

The original broad French/Chinese, source-update, clear, switch, reload and
partial-error evidence was not repeated wholesale. English fallback was
observed after the menu where not already loaded; that post-menu lookup and
later language switches were not separately performance-qualified here.
No new gameplay, visual, Steam/Linux or broad supplier-removal claim is made.

Retained failures: the first added group-method timing hooks hit the pinned
Mono/Harmony exception-filter code-generation limitation. `c04-perf-fr-off-a`
and functional `c04-perf-readiness` retain that failure; the prepared
`c04-perf-fr-cold-a` was never launched. Supported outer boundaries replaced
those hooks before matched qualification. A stale fixture-test expectation,
an ambiguous NUnit delegate and a nullable compilation error were corrected.
`symbol-translation-check.log` ran against the preceding DLL after a failed
compile and is **not** evidence for the string-sharing change; the successful
rebuilt checks are `symbol-main-final.log` and `symbol-translation-final.log`.

Raw evidence and scripts are in
`artifacts/ecosystem-next-20260910/c04/performance/`: `audit.json`,
`measurements.json`, the six comparison summaries, build/test logs,
per-launch window receipts and `restoration.json`. No fixture content is tracked.

All 53 transactions created by this qualification were rolled back through
`fixture.py` in verified reverse order. All seven original hashes and ten
original absences match the C10 restoration baseline; metadata audit passed and
no RimWorld process remains. The restored core is the original `ba17b033` build
and observer the original `3d86b163` build, not the candidate above.

The steward must review the source changes and bounded application evidence.
Parsed-language performance remains an unmet release requirement, not merely
an unrun test. A broader redesign would need to reduce repeated predecessor
checking/reconstruction while preserving the full native contract. These
unchanged losing comparisons should not be repeated or treated as acceptance;
default-off experimental code and a documented failure do not close row 2b.

## Steward review: bounded storage pass; ordinary timing and native insertion next

Independent runtime and storage review found no new correctness blocker in
stopped `f8306d59`, final functional source `542b8e13`. Complete group keys,
atomic publication, failure invalidation, bounded decoding, exact strings and
fresh mutable records remain intact. The final bound correction is functionally
tested; the performance tables still identify measured `392effe0` separately.

The steward checked 40 run-manifest hashes, 22 package files across 11 package
identities, 197 captured observer files, 3,368 captured language payloads and
the seven restored hashes / ten absences. The 39 normal exits include two
explicitly excluded observer failures, and one additional preparation was never
launched. Both application savings and matched XML-payload equality were checked.
Private verification is `artifacts/ecosystem-next-20260910/c04/performance/steward-review.json`.
No game/helper process remained at review. No new build or live run was performed
by the steward.

The 1.04–1.49 second application savings are retained as bounded evidence on the
official French workload. Parsed reuse remains **performance unaccepted**. One
measurement limitation must be resolved before treating the final small penalty
as the ordinary product cost: added Harmony timing patches run on language-only
paths (585 source-key, 1,170 guard and 393 injection-encoding calls in one warm
capture). Each adds locking, allocation and a finalizer. Using the same observer
package in both arms does not make that execution cost equal. This is a concrete
instrumentation concern, not evidence that removing it will make reuse win.

The same C04 owner first disables these detailed hooks for a focused matched
native/cold/warm comparison of final source. Keep the independent menu clock,
normal exit and complete output checks. This changed measurement premise justifies
the rerun; do not repeat the instrumented losing sequence or build a new profiler.

If ordinary reuse still loses, the next implementation changes the cache boundary:
store ordered arguments produced by parsing injection XML, then call the game's
native insertion methods against current state. Current final-record reuse
serializes the whole predecessor package and sorts definition names per group.
Native `TryAddInjection` / `TryAddFullListInjection` already perform name conversion,
duplicate/list conflict handling and new-record creation against actual state.
Preserving those operations can remove that broad prediction work while retaining
source freshness, order and callbacks. It is a materially different design from
packing files or sharing strings, not permission to weaken cache identity.

The implementation must preserve per-file errors, old `rep` syntax, comments,
provenance, fresh lists and converter behavior. Validate changed predecessor
duplicates, list conflicts and definition names before matched live output and
performance checks. Unsupported parser changes stay native; no generic converter
admission is assumed. Keyed/Strings coverage, mixed compatibility and the whole
C04 capability remain required. A checkpoint or another failure does not close
them. C10 stays stopped during this bounded C04 interlude; no C11 handoff occurs.


## Native source instructions implemented; performance still open — 12 September UTC

This increment replaces cached **merged results** with cached **source instructions**.
A warm load reuses the interpretation of a source file, then lets RimWorld apply it
to the current language data. This removes the need to predict previous winners or
current definition names. All three families are implemented and produce native-equal
complete output. The measured menu benefit remains inconclusive, so this is **not
full C04 acceptance, a speedup claim, or permission to omit parsed-language reuse**.
Independent review is required. Translation application's earlier separate positive
result is retained; it was disabled throughout these parsed-language comparisons.

The exact assigned base was `8bbf80f2f30a48b06307cd8f9f4752241b4f15dd`.
All source remains on the preserved review ancestry. Private evidence is under
`artifacts/ecosystem-next-20260910/c04/native-insertion/`.

### What changed and which native behavior remains

- `c869aaff7b071ee7de4447bae68360b3a3fa69af` adds the private
  `--language-observer` preparation option. It captures complete language output
  after the independent menu timestamp without selecting detailed stage hooks.
  Ordinary settings keep `loadingTimings` and `loadingDisplay` off in every arm.
- `c9030f2770d8d2f56a4306f412fa70b9ba195a28` implements definition-injection
  instructions. Cold interpretation uses the pinned native parser on a detached
  package, intercepting only scalar/list insertion inputs. Warm replay calls the
  actual native `TryAddInjection` and `TryAddFullListInjection` methods inside the
  original `AddDataFromFile` exception region. It preserves ordered legacy `rep`
  flags, list comments, native path/name conversion, duplicates, list conflicts,
  provenance, fresh lists and fresh native records.
- `a1a8e4f5` extends that boundary to keyed XML rows and Strings lines. Only the
  source-producing call is replaced inside each native file method. Native file
  error handling, first duplicate within a keyed file, later-file winners,
  placeholder conversion, line provenance, path construction and list append
  still execute. A malformed or interrupted source enumeration is never published.
- Last measured product source `e7be10f11b6ec7ad3c4c1714ff318ddf3e4fbf52` reuses reflection
  metadata and patch-guard objects for a definition type within one load. Each
  validation still checks current patch publications, converter-chain identity
  and the current cached name-lookup delegate. The session clears this metadata
  at disposal. String decoding reads directly from the validated payload range,
  avoiding a temporary byte array per string. The private timing observer's
  removed encoding-method target is updated to instruction decoding.
- Final functional source `8b9509e94ad76b5cf5f176dab157a4e9587911d2` removes a
  redundant directory-path getter from the Strings bridge. The native method
  already constructs its destination path before consuming the parsed lines;
  reading that path again outside cache admission was unnecessary. This final
  correction is functionally checked separately. The performance rows remain
  identified as their actual measured sources.

Cache keys still bind the selected/default language, source providers and paths,
actual source text, package/folder order, parser code identity and resolved type.
Definition source groups retain file order; keyed and Strings native consumers
retain their original order. No predecessor table or sorted definition-name table
is used for admission. Every payload is decoded and validated before native replay
can change the live package. A native exception after partial insertion stays in
the original catch and never triggers a fallback that inserts the file twice.
Existing foreign parser and CE/VEF converter refusals remain; this increment does
not broaden their admission. Current names and aliases are deliberately resolved
by the native consumer, including after a warm source hit.

### Ordinary startup comparisons

Each row below is a separate exact-source sequence ordered native, cold, warm,
warm, cold, native. Both warm runs restore that row's first cold capture. All use
French official content, the same minimized non-activating launch, matching core
and observer packages, independent menu clock, normal automatic exit and complete
language-output capture. Timings are seconds to first completed main-menu repaint;
they are whole-startup observations, not sums of overlapping internal stages.

| Source / run prefix | Native A / B | Cold A / B | Warm A / B | Warm saving forward / reverse |
| --- | --- | --- | --- | --- |
| `c869aaff`, `c04-ordinary-fr` | 13.074411 / 12.442286 | 13.371222 / 13.465750 | 12.891938 / 12.720474 | +0.182473 / -0.278188 |
| `c9030f27`, `c04-insert-fr` | 12.783292 / 12.717861 | 12.822885 / 12.983133 | 12.692468 / 12.849184 | +0.090824 / -0.131323 |
| `a1a8e4f5`, `c04-rows-fr` | 12.769378 / 12.405733 | 13.110240 / 13.012967 | 12.403330 / 12.444732 | +0.366048 / -0.038999 |
| `e7be10f1`, `c04-rows-metadata-fr` | 12.503044 / 12.542090 | 12.694978 / 12.605444 | 12.415434 / 12.642387 | +0.087610 / -0.100297 |

Positive means faster than the corresponding native control; negative means slower.
No row gives useful savings in both directions. The small final differences are
inconclusive rather than proof that every source-instruction design must fail.
The final cold overhead was 0.191934 and 0.063354 seconds. No favorable-run selection,
averaging across different binaries, historical combined percentage or application
benefit is used to claim a parsed-cache gain.

A separate instrumented cold/warm pair, `c04-rows-diagnostic-fr-*`, is diagnostic
only. Its warm French stage totals were initialization 98.7075 ms, source keys
52.0947 ms, instruction decoding 36.8422 ms, guard checks 8.1935 ms, session begin
17.9143 ms and end 19.4055 ms. These overlap and include instrumentation; they are
not added or compared with ordinary endpoints. They do not justify weakening the
guards. One attempt to import an ordinary cache into changed diagnostic settings
was refused before launch; the diagnostic pair then used its own cold provenance.

### Correctness evidence and remaining acceptance

The final focused suite passes 51 language tests plus 27 selected core tests.
The fixture's 65 checks passed after the observer option change. Tests cover native
scalar/list conflicts against changed predecessors; partial-file native failure;
malformed source; legacy `rep` ordering and comments; fresh mutable records/lists;
current definition names and aliases; equal-length source edits; keyed duplicates,
placeholders and provenance; Strings append into changed predecessor lists; complete
payload validation; and current converter refusal with reused metadata. The
existing foreign-converter callback/application test remains passing.

The two packaged functional cold/warm pairs at `c9030f27` and `a1a8e4f5`, and all
ordinary official captures including final `e7be10f1`, have complete output SHA-256
`d5685da534af5853711f3bdccfd7d315788c0e3e323c8ce52bb9b2da1c41b6ad`, equal to
native output. Warm French reuse is 91 keyed, 1,451 definition-injection and 186
Strings files, with 26,572 actual native insertion calls and no native source parse
files. English fallback reuses 91 keyed, two injection and 73 Strings files. No
native game object survives in the persisted payload or after session disposal.

Final-source mixed native/cold/warm captures `c04-rows-mixed-selected-*` all match
`c58a57180fa8d0abf32a1d777b3c5a51fb0494dca1bc2e2b8b79ca5e441fa744`. The warm
French run reuses 97 keyed and 188 Strings files while keeping 1,451 injection files
native under the existing Combat Extended converter guard. English reuses 132 keyed
and 75 Strings files with two injection files native. Existing mixed diagnostics
remain identical to the native control; they are not newly introduced XML errors.

Final-source `c04-rows-xml-ordinary-cold/warm` verifies the language cache alongside
actual XML reuse. The warm run avoids 1,558 XML text parses, skips 29 admitted patch
operations and restores 4,554 inherited children, while retaining the complete
native-equal official language output and all French/English language hits. Cold
and warm stored payloads are identical: seven ParsedXml, one ProcessedXml, one
ResolvedInheritance and two ParsedLanguage files. This is a compatibility check,
not a matched claim about combined speed. The earlier functional-observer XML run
passed output checks, but its own hook caused processed XML and inheritance to
fall back; the attempted warm import correctly refused their empty stores. The
ordinary-observer pair resolves that specific test interference.

### Capture inventory and restoration

`audit.json` records 36 successful captures: 28 performance-purpose and eight
functional. Of the performance-purpose captures, 24 are the four ordinary matched
sequences, two are separate instrumented diagnostics, and two are the final XML
compatibility pair. All 36 have normal exit, automatic pass, complete native-equal
language output and matching core/observer source revisions. The audit rehashes
the captured language payloads and confirms detailed stage files and loading
reports are absent from ordinary runs. Test-authoring/build/preparation failures
remain in their individual logs and are not passing captures.

One additional **invalid functional run** is excluded: `c04-rows-mixed-off`
accidentally selected all 310 frozen entries because the helper omitted the prior
explicit selection. The queue helper was stopped first. After preserving its log,
selection and verified executable identity, fixture process 58064 was terminated
to stop the irrelevant long workload. The launch worker captured failure with
`automaticTestPassed=false` and exit code 4294967295; there was no menu success.
Corrected checks use the exact 12-entry prior CE/VEF selection and assert its count
before launch. Evidence is in `selection-failure/`; this run supports no functional
or performance claim.

That run also rewrote the fixture's Use This Instead `replacements.json`. The
normal-source restore correctly refused newer bytes. An existing private recovery
copy was independently matched to the frozen size, timestamp and SHA-256
`6c74ade7594ce8f350c7821ab691bbeaccae622989b4e21b6e3876799a942e2d` and restored
transactionally. No normal game or Workshop file was modified. `repair-frozen.log`,
`frozen-replacements-source.json` and the final restoration receipt preserve both
the proof and overwritten fixture bytes.

Fifty owned test/deployment/repair transactions were rolled back in verified
reverse order. A final restorative transaction reestablishes that one frozen mod
file after the rollback chain would otherwise restore its aborted-run rewrite.
Its identity is in `restoration.json`; it is deliberately retained as restoration,
not an active test deployment. All seven baseline file hashes and ten baseline
absences match, and the fixture audit passes. The GOG generation remains
`gog-rev573-20260910-194017-cf1a1eb0`. No game/owned launch helper remains.

Final functional core SHA-256 is
`11c29331107f96ad425dbe307c1f2a5f071f44a247d7598414bd0ba61e4e17ec`; matching
observer SHA-256 is
`367a1a7f699d15cfc1e74387b938b5fab95afbd15b04361714fedb48de023193`.
These final packages are preserved under their `8b9509e9...` artifact directories;
the fixture itself is back on its original baseline packages. The report-only
handoff commit is separate from both measured and final functional source.

Full C04 performance, combined acceptance and release readiness remain open.
There is now a real native-consumer implementation for independent review, not
just a feasibility design, but it has not yet shown repeatable useful menu savings.
No family is removed, no losing approach is declared acceptance, and no successor,
merge, publication, Steam promotion or Deck access is implied.

## Steward review: native consumers pass; Prepatcher installation next

Stopped `ecd4af635019419ee1f496f5718163d937a8d373` passes independent functional
review of the native-consumer increment. The reviewed final functional source is
`8b9509e94ad76b5cf5f176dab157a4e9587911d2`; measured `e7be10f1` remains separate.
Keyed and Strings feed the original native consumers. Injection instructions
retain ordered calls and legacy flags inside the original exception handler;
partial native failures do not cause a second insertion. Lookup metadata reuse
still reads current converter, delegate and patch state. The final Strings getter
removal preserves the destination already computed by native code.

The steward independently checked 37 run-manifest hashes, 20 package files across
ten identities, 174 captured observer files, 54 language payloads, all four matched
sequence identities/settings/cache provenance and their timing arithmetic. The
eleven final XML/language payloads match across cold/warm. All 36 successful
captures retain native-equal text; the separate 310-entry aborted functional run
is explicitly excluded. Fifty rollback receipts, seven baseline hashes, ten
absences and the exact restored frozen file were verified. No game/helper was
observed at review. Private evidence is
`artifacts/ecosystem-next-20260910/c04/native-insertion/steward-review.json`.

Preserve restorative transaction `20260912-170800-04578930`: it restores the exact
frozen Use This Instead `replacements.json`, SHA-256
`6c74ade7594ce8f350c7821ab691bbeaccae622989b4e21b6e3876799a942e2d`.
Its recovery copy retains the aborted run's altered bytes. Rolling back this
repair would reintroduce drift; it is part of the restored baseline for later
owners, not a leftover candidate deployment. No normal game data was modified.

**Parsed-language performance and full C04 remain unaccepted.** The final ordinary
sequence saves 0.087610 seconds forward and loses 0.100297 seconds reverse. Neither
that result nor completed native-consumer behavior closes the speed requirement.
Translation application's earlier separate bounded gain is unchanged.

The next concrete implementation removes runtime patch construction. Required
Prepatcher already supports reviewed native-body edits for parsed XML and loading
attribute lookup. **Correction after pinned-source inspection:** it does not persist
reusable output; processing and serialization occur every launch, as established
in the later Prepatcher report below. `ParsedLanguageRuntime.Install()` at this
reviewed source constructs seven
Harmony patches each process; the roughly 99 ms initialization diagnostic gives
a distinct cost to address. The same C04 owner will install the reviewed language
connections through Prepatcher, preserving native methods, error regions and
consumers, with exact original/post-edit body validation, callable public bridges,
live publication guards and native fallback. This is not a claim that all 99 ms
is removable or a request to defer installation until after the menu.

Acceptance of that increment requires actual packaged behavior plus total ordinary
startup comparisons, including Prepatcher processing on every launch and the
disabled feature's cost. The original request to compare reusable Prepatcher
output is superseded by the source finding above; cold/warm conditions apply to
the language cache. Merely moving
work into an earlier stage cannot establish a gain. Keep source/cache identities
and all language families, compatibility obligations and full C04 open. If the
route fails, preserve its concrete result and return a changed design premise;
do not close the capability through fallback, default-off status or omission.


## C04 coordinated Prepatcher installation — 12 September 2026

The seven language connections now run through the already required Prepatcher,
which edits the game methods before the game restarts with its patched assemblies.
The packaged implementation preserves the reviewed native language consumers and
passes the bounded correctness checks below. It still does **not** establish a
useful parsed-language startup gain: warm reuse is slower than the new product's
own disabled control in both directions. This increment is returned for independent
review; full C04, performance acceptance and release readiness remain open.

### Implementation and identity

Product commit `93e084207f700610f25195fdbfd552b06d844824` adds
`ParsedLanguagePrepatch` and removes all seven production `Harmony.Patch` calls
from `ParsedLanguageRuntime.Install`. The seven connections cover `LoadData`,
`LoadFromFile_Keyed`, `LoadFromFile_Strings`, `LanguageDatabase.Clear`,
`TryAddInjection`, `TryAddFullListInjection` and `AddDataFromFile`.
`Install` now validates the rewritten bodies and creates the existing live
compatibility guards before enabling reuse.

Prepatching first verifies all seven original method fingerprints. It edits cloned
method bodies and restores all original bodies and added assembly references on
failure. Missing, changed or already rewritten targets refuse the coordinated
installation. Runtime admission requires all seven known post-edit identities;
partial or unknown installation cannot enable a session. No partially accepted
family or duplicate installation is used to claim completion.

Calls from the game assembly use public bridges and a public operation record.
The original native methods remain the actual insertion and consumption entry
points. Replay stays within the native `AddDataFromFile` exception handler,
retains ordered scalar/list calls and old-replacement flags, and creates fresh
mutable list/comment values. The native source path remains when replay is
unavailable. `LoadData` has an outer `finally` for session cleanup; inner native
handlers remain intact. Existing discovery, read barriers, current name/alias
lookups and foreign-patch guards are retained. Existing Harmony transformation
helpers remain for focused test compatibility; production installation no longer
calls them.

Observer-only commit `e33846a2307165d5cd66980ea41f56df06e2d1b1` makes functional
contract diagnostics compare the intended seven rewrites to their pinned expected
post-edit hashes. It does not learn bodies from the running game. The first three
functional captures used the previous observer and therefore listed the seven
intended rewrites as mismatches against original bodies; their actual hashes match
the offline post-edit constants. Later mixed functional captures have empty
mismatch reports. Ordinary runs do not install these functional diagnostics.

The measured/final packaged source is `e33846a2307165d5cd66980ea41f56df06e2d1b1`.
Its core SHA-256 is
`02b5f26e0644cac92da9f43411b07ad19c10ea84186a2e821f626e47cf042c14`;
matching observer SHA-256 is
`9ce9a1fdfa5f00d46c400bc503a1da2d537c81510a4579fdee889062bd39fb10`.
The old disabled control uses the previous reviewed `8b9509e9` core and matching
observer identified above. Packages remain in their source-named artifact
folders. The report-only handoff commit is separate from these binaries.

### Correction: Prepatcher does not persist reusable output

The preceding steward review's persistent-output premise is inaccurate for the
pinned Prepatcher. Read-only ILSpy inspection shows `Loader.DoReload` creates a
new `AssemblySet` and calls `GameProcessing.Process` in each process.
`Reloader.Reload` serializes every changed assembly and loads the resulting bytes;
`RawBytes` and `Bytes` are process-local fields. File output is limited to the
explicit `dumpasms` diagnostic, with no persistent cache-loading path.

Consequently, every comparison includes Prepatcher processing and serialization.
There is no first-build versus reused-Prepatcher-output condition to measure in
this supplier version. Cold and warm below refer only to the language cache.
No separate Prepatcher cache platform was introduced. Source anchors are the
`PrepatcherLoader.cs`, `PrepatcherReloader.cs` and `PrepatcherAssembly.cs` extracts
under the private evidence directory. Each measured log also records processing
and serialization; those stage diagnostics are retained separately and are not
added to the already inclusive menu time.

### Correctness and compatibility

Thirty focused core/prepatch tests and 51 language tests pass. The new tests check
exact original and serialized post-edit identities, cross-assembly accessibility,
each missing/changed target, repeated installation refusal, native catch/finally
placement and coexistence with parsed XML and attribute-lookup Prepatcher edits
in both registration orders. Test-authoring loader/token-resolution failures were
corrected before the passing runs; they were not game failures. The observer
build also passes with zero warnings/errors.

Sixteen captured GOG runs all exited normally with `automaticTestPassed=true` and
native-equal complete language output. These comprise six functional checks,
two ordinary XML/language compatibility checks and the eight-run cost sequence.
Every preparation explicitly selects the recorded frozen workload, and every
launch rechecks the exact active order and current operational scope. Official
runs select eight frozen entries plus the local observer/product; mixed runs
select twelve plus those two local entries. The GOG generation and manifest
remain `gog-rev573-20260910-194017-cf1a1eb0` and
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.

Official disabled/cold/warm complete output SHA-256 remains
`d5685da534af5853711f3bdccfd7d315788c0e3e323c8ce52bb9b2da1c41b6ad`.
French warm reuse covers 91 keyed, 1,451 injection and 186 Strings files, with
26,572 actual native insertion calls and zero retained session owners. English
covers 91 keyed, two injection and 73 Strings files. Cold runs build real records;
warm runs import matching captured records rather than relying on a label.

Mixed Combat Extended/Vanilla Expanded disabled/cold/warm output remains
`c58a57180fa8d0abf32a1d777b3c5a51fb0494dca1bc2e2b8b79ca5e441fa744`.
Combat Extended's `BackCompatibleDefName` hook is detected and injection parsing
stays native. Keyed and Strings reuse remains available. This is tested fallback
behavior, not a claim of injection reuse under foreign name-conversion hooks.

The ordinary combined XML/language warm capture reports 1,558 parsed XML source
hits, 29 skipped native XML patch calls and 4,554 inherited-definition hits,
alongside all three language families. Seven parsed XML payloads, one processed
XML payload, one inheritance payload and two language payloads are byte-identical
between cold and warm. These runs use the ordinary after-menu language observer:
the functional XML observer itself adds a hook that correctly prevents the
processed/inheritance reuse being checked. Their two timings are not a matched
XML performance result. No new runtime errors were found by the capture audit.

### Ordinary total startup result

The active owner grant was `owner-unattended-20260912-173527`. All RimWorld
instances were stopped before each measurement, substantial competing campaign
work was paused, and each process exited/captured normally before the next
operation. Every launch saved the active grant and scope. No fixed cutoff was
invented. Functional-run durations are diagnostic only and excluded here.

The sequence uses French official content, application reuse off, all unrelated
features off, ordinary user activation, loading timing/display diagnostics off
and the after-menu language observer. Source, package, selection, settings and
cache provenance are retained per capture. The same new cold-a capture supplies
both new warm runs. No language cache is imported into either cold or disabled
control. Old/new observer packages match their respective core sources; the
observer-only change concerns functional diagnostics and preserves ordinary
measurement/output behavior.

| Condition | Forward seconds | Reverse seconds |
| --- | ---: | ---: |
| Previous reviewed product, language disabled | 12.770444 | 12.407221 |
| New product, language disabled | 12.374721 | 12.337096 |
| New product, first language cache build | 12.750642 | 12.767564 |
| New product, warm language cache | 12.573532 | 12.377897 |

Actual order is old-disabled, new-disabled, new-cold, new-warm, new-warm,
new-cold, new-disabled, old-disabled. New warm costs 0.198811 and 0.040801 seconds
relative to new disabled; first build costs 0.375921 and 0.430468 seconds.
New disabled is 0.395723 and 0.070125 seconds faster than the old disabled
controls in this bounded sequence. The broad difference between those two
control deltas does not establish a precise installation-cost saving or a
universal gain. Warm new-versus-old disabled differences are 0.196912 and
0.029324 seconds; they likewise do not qualify the parsed cache when the new
product's own native path is faster.

The concrete result is a working Prepatcher installation with no observed
disabled regression in this set, but no useful parsed-language gain. The earlier
separate translation-application result is preserved and is not combined with
these timings. No repeated unchanged sequence was run to search for a favorable
answer.

### Remaining work and restored handoff

A materially different next experiment could reduce transient allocations while
constructing exact source keys. `ParsedLanguageRuntime.Key` currently writes each
group's full source text into a `MemoryStream`, calls `ToArray`, then hashes that
copy; the per-file keyed/Strings path also allocates temporary one-element arrays.
Streaming the same canonical bytes into a digest and reusing bounded buffers
could remove those copies while retaining complete source identity and live
compatibility checks. This is a source-grounded, unmeasured premise, not a proved
bottleneck or approved performance result. It would require exact key-equivalence
and native-behavior checks plus a new matched measurement. Skipping source reads,
weakening guards or omitting a language family is not the proposed change.

Private evidence is under
`artifacts/ecosystem-next-20260910/c04/prepatch-installation/`: `audit.json` records
all 16 captures, `qualification.json` records the sequence/arithmetic/package and
XML payload identities, and `restoration.json` records restoration. All 28 owned
transactions were rolled back to this assignment's starting baseline. Seven
baseline hashes and ten absences match; fixture audit passes. The exact frozen
Use This Instead file hash remains restored, and baseline restorative transaction
`20260912-170800-04578930` remains committed. No normal game data was modified.

The owner returns STOPPED with no game/build/helper operation outstanding, no
successor dispatched and no changes to parent scope/ownership documents. Full
C04 remains open for independent functional review and a genuinely useful
parsed-language performance result. No merge, push, publication, Steam promotion,
Deck access or production adoption is implied.

## Steward review: Prepatcher installation passes; remove source-key copies next

Stopped `18908cbbf76df974ed42f737d959d1e6e33ca83b`, tested/measured
`e33846a2307165d5cd66980ea41f56df06e2d1b1`, passes independent rewrite and runtime
review. All seven original bodies are checked before edits; cloned bodies preserve
rollback. The resulting coordinated identities, public bridge access, native
exception regions, outer cleanup and live guards retain the reviewed behavior.
This is functional acceptance of the installation increment, not full C04.

The steward verified 16 run manifests, 12 package files across six identities,
96 observer files, 20 language payloads, all eight comparison identities/settings,
fixed warm provenance and timing arithmetic. The eleven XML/language payloads
match across the combined cold/warm check. All 16 captures exited normally with
native-equal text. Twenty-eight rollback receipts, seven restored hashes, ten
absences and the retained frozen-file repair were checked; no game/helper process
was observed. Evidence is `artifacts/ecosystem-next-20260910/c04/prepatch-installation/steward-review.json`.

The useful parsed-language gain remains **unmet**: warm costs 0.198811/0.040801
seconds and cold costs 0.375921/0.430468 versus this product's native path.
Old/new disabled differences support no observed regression in this bounded set,
not a precise isolated installation saving. Pinned-source inspection corrects
the earlier persistence premise: Prepatcher processing and serialization are
included on every launch. No source-key estimate is sufficient to waive the
whole-startup result.

The next bounded implementation removes avoidable source-key copies. `Key`
currently writes complete canonical bytes into a `MemoryStream`, then duplicates
them with `ToArray`; per-file consumers also create one-element arrays. Existing
`CacheDigest.Hash(byte[], offset, count)` already hashes an array range with
Windows acceleration. Prefer that established primitive and bounded load-scoped
buffer reuse over adding another hashing provider. Preserve exact canonical bytes,
UTF-8 handling, complete source reads, admission order, current guards and digest
values. Native fallback and teardown must remain unchanged. A streaming digest
is an alternative only if it materially improves the actual buffer path without
per-field native-call overhead or weakening the hash contract.

The same C04 owner receives this finite allocation change and focused exact-key,
native-output and ordinary matched qualification. It is a changed source-backed
premise, not a demonstrated solution to the entire remaining cost. Do not repeat
unchanged endpoints looking for a favorable pair. The result returns to the
steward whether positive or negative; all families, broader compatibility,
performance and release obligations remain required. C10 stays stopped during
this interlude, and no C11 handoff or feature exclusion is implied.


## C04 bounded source-key copy removal — 12 September 2026

The source-key implementation now avoids duplicating the complete serialized
input before hashing it and reuses a small scratch writer during each language
load. Native output and all three reuse families remain correct in the focused
checks. The one ordinary matched sequence still shows no useful startup gain:
warm reuse costs 0.024897/0.124354 seconds against paired native controls.
This finite increment returns for independent review; full C04 performance
remains unmet.

### Exact implementation and ownership

Product, functional and measured source is
`99ae330198a772aba842f10758b0002b9b2c4091`, based on reviewed
`0a784fc293d9b90bad7bd1048b6c151c8683a366`. `ParsedLanguageKeyBuffer` belongs to one
language session and its creating thread. It reuses `BinaryWriter` and
`MemoryStream`, resets position and length for each key, and calls the existing
`CacheDigest.Hash(buffer, 0, usedLength)` once for the complete serialized key.
No `ToArray` copy is needed. The existing Windows acceleration and configured
managed fallback remain untouched; no crypto provider or per-field hashing
scheme was introduced. The dedicated `SingleFileKey` path removes the two
one-element file/text arrays for keyed and Strings files.

The serialized format is unchanged: context, kind, predecessor digest, count,
ordered full paths/names and actual source text, followed by resolved type/module
identity and the existing current validator calls where applicable. The old
bounds, provider checks, UTF-8 `BinaryWriter` encoding and native fallback remain.
There is no cache-format, default, eligibility, consumer or Prepatcher bridge
change. Code identity still distinguishes builds; this is not a claim that a
cache from another product build bypasses existing identity validation.

Retained scratch capacity is capped at 256 KiB per live language session. Larger
valid sources remain supported, but their larger buffers are dropped after that
key. Serialization/hash failures drop scratch in `finally`; successful reuse
resets length/position and hashes only used bytes, never stale tails. Idle
same-thread invalidation releases scratch immediately. If invalidation happens
during key construction, the owner's unwind releases it; foreign-thread
invalidation never races a buffer disposal against a write. Session disposal
releases writer/buffer references deterministically. The buffer retains no
native files, language objects or source-string owners, and no backing array is
exported to a persisted record or native consumer. No global pool was added.

Core package SHA-256 is
`342553e9f0460695d237b002604ba3ea280504b83925ba112a90ae9b14e2f4cf`;
matching observer SHA-256 is
`cce954623a7295dd1838cb1ad4e1854e47e4bf4ee69d53852f9800879605f3fb`.
Both are preserved under their `99ae3301...` artifact directories. Their source
and captured identities are distinct from the later report-only handoff commit
and from the restored baseline deployment.

### Focused correctness evidence

Thirty core/prepatch tests and 54 language tests pass. Three additions to the
existing language runtime tests compare old canonical bytes/digests with the
new actual key paths, including all families, empty and Unicode text, malformed
surrogate handling, variable-length string-prefix boundaries, nonempty predecessor
bytes, full paths, file order, and same-length source/path changes. They also
check maximum source length and rejected count/length/null inputs, short keys
after long keys, oversized buffer release, failed serialization, repeated current
validator invocation, invalidation during validation and final disposal. Existing
native name/delegate/foreign-hook/error tests still pass. Two initial test-build
failures were NUnit overload/obsolete-delegate authoring errors, corrected by
selecting the `Action` overload; they were not game or product failures.

Three packaged functional runs (native, first build and warm) and six ordinary
measurements all exited normally, with `automaticTestPassed=true`, and match the
complete native language SHA-256
`d5685da534af5853711f3bdccfd7d315788c0e3e323c8ce52bb9b2da1c41b6ad`.
The functional observer reports no language-contract mismatch. French warm reuse
covers 91 keyed, 1,451 DefInjected and 186 Strings files with 26,572 actual native
insertion calls. English fallback covers 91 keyed, two DefInjected and 73 Strings
files. Warm native-file counts and retained session owners are zero; both measured
warm payload sets equal the same cold seed. No runtime error was found by the
capture audit.

The change does not alter supplier admission, native error handling or XML
connections. The preceding adequate mixed Combat Extended/Vanilla Expanded and
XML evidence remains applicable to those unchanged contracts; those entire live
sequences were not repeated for this allocation-only increment. This is focused
GOG evidence, not new Steam/Linux or general gameplay qualification.

### Ordinary total startup and remaining cost

The active `owner-unattended-20260912-173527` grant and current scope were checked
and saved before each measured launch. All RimWorld instances were stopped,
competing campaign builds/tests/substantial analysis were paused, and each process
exited and captured normally before another operation. Every prepare explicitly
selected the same eight frozen official entries plus local observer/product;
selected count and exact order were asserted before each launch. The fixture was
muted and launched minimized without activation. GOG generation and manifest
remain those recorded for the preceding increment.

All six measurements use French, ordinary user activation, application reuse off,
loading timings/display off, the independent menu clock and the after-menu
language observer. Only parsed-language selection/cache condition changes. Actual
order is native, cold, warm, warm, cold, native; both warm arms import
`c04-keycopy-cost-cold-a`. Cold/native controls import no cache. Prepatcher
processing and serialization remain included on every launch; there is no
reusable Prepatcher-output condition.

| Condition | Forward seconds | Reverse seconds |
| --- | ---: | ---: |
| Native language loading | 12.538302 | 12.768118 |
| First language cache build | 12.584583 | 12.992603 |
| Warm language reuse | 12.563199 | 12.892472 |

Warm costs 0.024897/0.124354 seconds; first build costs 0.046281/0.224485 seconds
against their paired native controls. Native controls differ by 0.229816 seconds,
so this set does not isolate a precise allocation saving from prior experiments.
No speedup is claimed from removed copies, diagnostic key-work estimates or
cross-sequence arithmetic. All samples are retained, and the unchanged sequence
was not repeated to seek a favorable result. Functional durations are excluded.

The remaining concrete work still includes reading and hashing actual complete
source text, computing the predecessor digest, validating current compatibility
state, loading/decoding the packed records and executing native consumers. Cold
loading additionally captures and publishes records. This increment changes
scratch allocation/copying only; it does not measure the separate share of these
remaining costs or establish which one explains the whole-startup result. The
steward chooses subsequent work after review; no new optimization chain, family
omission or change to the separate application result is started here.

### Restored STOPPED handoff

Private evidence is under
`artifacts/ecosystem-next-20260910/c04/key-copies/`: `audit.json` covers all nine
captures, `qualification.json` verifies exact settings/order/source/cache hits,
provenance and arithmetic, and `restoration.json` records rollback. All thirteen
owned transactions were rolled back. Seven baseline hashes and ten absences
match, fixture audit passes, and the exact frozen replacement file and baseline
repair transaction `20260912-170800-04578930` remain preserved. There is no
outstanding game/build/helper operation. The checkout returns clean and STOPPED
for independent review, with parent ownership/scope documents unchanged and no
successor, self-acceptance, merge, push, publication, normal-data change, Steam
promotion or Deck access.
## Steward review: key-copy increment passes; C04 remains open

The bounded source-key copy change is correct, but does not yet make persistent
parsed-language reuse faster. Product `99ae330198a772aba842f10758b0002b9b2c4091`
and stopped handoff `2dfb25e2767ca787345ef2d1e0fd05c8eecf5bf5` pass independent
review. The owner returned test-only correction
`9939fbeb60a5c75563d10b3540705b77d9091376`, restoring six corrupted Unicode
literals using explicit C# escapes; all 54 focused language tests pass against
the unchanged product DLL. The intentionally invalid surrogate remains tested.

The source review confirms identical canonical bytes and used-range hashing,
bounded session-owned scratch storage (256 KiB retention ceiling), and safe
owner cleanup after invalidation without a cross-thread dispose race. It found
no product blocker. The steward independently checked nine run manifests,
four package files across two identities, 51 observer files, 12 language payloads,
13 rollback receipts, seven baseline hashes, ten absences and the retained frozen
file repair. The three functional and six ordinary measured runs preserve native
outputs. Raw review: `artifacts/ecosystem-next-20260910/c04/key-copies/steward-review.json`;
test correction: `unicode-correction-handoff.json` and `unicode-correction-tests.log`
in the same directory. The correction changes no measured package identity.

Warm costs of 0.024897/0.124354 seconds and cold costs of 0.046281/0.224485 seconds
remain unaccepted. The difference between native controls prevents a precise
cross-experiment allocation claim. The separately measured translation-application
benefit remains bounded to its documented workload; it does not qualify parsed
reuse. No unchanged sequence is scheduled merely to seek a favorable result.

The steward next transfers ownership to the original C05 task for its outstanding
texture load-cost correction. C04 remains a required open capability and returns
after that interlude. Its next concrete coverage design is consumer-only admission
of the source-verified Combat Extended and Vanilla Expanded conversion behavior:
capture parser instructions before conversion, then execute current converters
once, in native order, during actual insertion. Never cache converter results or
silently ignore changed mappings. Keep parser/publication guards and refusal of
foreign insertion-entry hooks that would otherwise run twice. Arbitrary converters
that install parser patches during insertion require further engineering, not a
blanket allow rule. Focused acceptance is zero conversion during capture, native
callback counts/order/output on cold and warm loads, live mapping changes,
native partial-error behavior, and correct foreign-parser/entry-hook fallback.

This design is supported by `c04/vef-native-rename-converter.cs`, the current
capture prepatch/instruction source, and pinned `c12/r1/DefInjectionPackage.cs`
under the campaign artifacts. It expands coverage without claiming a speedup.
Remaining performance work must identify the material cost of complete source
validation, record reads/decoding and native consumption on representative
workloads before selecting another changed design. Unknown cost or feasibility
does not exclude the capability. Full C04 acceptance, broader compatibility and
combined qualification remain open; no default or release scope is changed.

## Consumer compatibility increment, 12 September 2026

The new code lets supported Combat Extended (CE) and Vanilla Expanded Framework
(VEF) name converters run during actual translation insertion while Wake-Up
reuses the earlier XML parsing work. A converter translates an older definition
name to its current name. Its result is not saved in the cache: the current
converter and current mappings run again for every native insertion, preserving
their order, duplicate handling, errors and source information.

This bounded assignment starts from reviewed base
`785a1913d697be144d61c4a9c6288c5f565ef538`; product and focused tests are committed
at `c60022831b728161805e79cf6e88e61c68859fc0`. It preserves later C05/C06-C13/C10
ancestry, the separate translation-application path, XMLC02/C03 connections,
Keyed/Strings parsing, existing cache bytes and default-off selection. It changes
which source-verified consumers may accompany parsed DefInjected reuse.

### Why these consumers can be admitted

The existing capture step records parser instructions before native insertion
reaches name conversion. The new admission checks establish that these exact
consumers cannot modify parsing while those instructions are replayed. They
check method signatures and original bodies, plus the current published Harmony
patches. Harmony patches are other mods' changes to a method's behavior. Supplier
names or patch-owner names alone do not grant admission.

CE's exact postfix and its generated string-hash helper are guarded. VEF's exact
converter and late postfix are guarded along with the live converter chain,
converter instance and save-check delegate. Its mapping dictionaries must use
ordinary comparers and actual runtime Type keys, preventing hidden comparison
callbacks. Definition lookup retains its native method/publication and delegate
guards and also rejects custom name comparers. Mapping values stay live and are
never included as cached conversion results. A merely loaded VEF assembly does
not trigger its migration initializer.

Each language session resolves its own guarded supplier methods. A later
published supplier patch must match that resolved set; a newly loaded or second
assembly cannot bypass its dependency checks. This closes the concrete issue
found by the independent read-only reviewer during development. Its final
rereview found no remaining source blocker. Foreign parser or insertion-entry
hooks and arbitrary converters remain native; allowing an insertion-entry hook
would otherwise risk invoking it during both capture and replay. Unknown
converters could also change parser behavior during insertion.

Pinned source and review anchors are recorded in private
`artifacts/ecosystem-next-20260910/c04/consumer-compatibility/source-review.md`.
They include native `DefInjectionPackage`, CE's generated helper, VEF migration
and late-hook source, and the native save-environment check. The pinned supplier
DLL hashes identify the evidence; production checks the consumed methods and
dependencies rather than requiring those entire DLL hashes.

### Focused checks

All 32 selected main tests and 58 language/translation tests pass. The new checks
verify zero conversion during detached capture and matching callback count,
order, scalar/list/legacy/duplicate output on cold and warm replay with changed
current mappings. They also check actual pinned CE postfix admission, refusal
when its helper is patched or its resolved dependency set is missing, refusal
of prefix placement, native fallback for foreign parser/insertion-entry hooks,
and acceptance of changed VEF mapping values while rejecting custom comparison
callbacks and non-runtime Type keys. Existing partial-failure, malformed-source,
source-change, invalidation, application, Keyed and Strings checks still pass.

The callback-order test uses an explicit counting-consumer test seam. Actual
supplier identities and guard admission are checked separately in the matching
.NET Framework host and isolated GOG fixture. The parser-hook fallback test
compares native output under the same hook because the modern test runtime can
inline that helper. Test-host setup corrections and their initial failed logs
are retained; production signature normalization was not weakened.

### Actual mixed GOG reuse and restored handoff

Three functional captures use matching core and observer source
`c60022831b728161805e79cf6e88e61c68859fc0`: `c04-consumer-mixed-off`,
`c04-consumer-mixed-cold` and `c04-consumer-mixed-warm`. Each explicitly selects
the same 12 frozen entries, with 14 active entries after observer/product
insertion. Exact order, package revisions, French language, muted profile and
ordinary user settings were verified before each minimized, nonactivating
launch. Only parsed-language selection/cache condition differs; application
reuse, XML reuse and other feature selections remain off. The warm capture
imports only the cold capture's parsed-language cache.

All three reached the independently observed menu, exited normally with code
zero and passed the automatic test. Their complete language observer output has
the same SHA-256,
`c58a57180fa8d0abf32a1d777b3c5a51fb0494dca1bc2e2b8b79ca5e441fa744`, also matching
the prior qualified mixed native output. Contract-mismatch files are empty and
the bounded runtime-error scan finds no error lines. This observation covers
translation outputs and native error/provenance records; it is not a broad
gameplay test.

| Warm load | Keyed files reused | DefInjected files reused | Strings files reused | Files parsed natively |
| --- | ---: | ---: | ---: | ---: |
| French | 97 | 1,451 | 188 | 0 |
| English fallback | 132 | 2 | 75 | 0 |

French cold and warm both execute 26,572 native insertion calls. Both warm cache
payloads match their cold seed exactly; warm publishes no new groups, neither
language reports failure, and both release all session owners. This demonstrates
actual parsed DefInjected reuse with CE/VEF active, replacing the earlier mixed
result that kept those files native. Current mapping changes and exact callback
order are supported by the focused tests and source proof described above;
the real mixed runs do not inject an artificial change into VEF's mapping table.

The GOG generation remains `gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`, original game
assembly `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Built, deployed and captured core DLL SHA-256 is
`0dd282c5beb824936ce091df271c2a2678336b65eec34c8317e0a380ebed04a2`; the separately
built matching observer DLL is
`a02ef9dbbbcbc1579f5b23a2f2a0c1b9dc07ae5862c724e4811ccb014fba3b73`.
The observer source did not change. Existing adequate official-language,
application and XML integration evidence was not repeated as whole workloads.

Private `consumer-compatibility/audit.json` and `qualification.json` record
captures, exact settings/order, package/run hashes, cache payloads and actual
reuse. `restoration.json` records reversal of all seven owned transactions,
seven matching baseline hashes, ten baseline absences and a passing fixture
audit. The frozen replacement file retains its complete hash, size and timestamp;
baseline repair transaction `20260912-170800-04578930` remains committed.
All fixture/build operations and the bounded read-only reviewer are stopped.

The owner-return revocation remained active throughout. No performance run,
microbenchmark, cost attribution or speed claim was made. The existing unmet
performance obligation remains open, as do broader converter compatibility and
combined/release qualification. This is a clean STOPPED handoff for independent
steward review, not self-acceptance. Parent scope/ownership records were not
edited; no successor, merge, push, publication, Steam promotion, Deck access or
normal-data change was performed.
## Steward review: consumer compatibility passes; return to C10

Stopped `ebea82b9576eb549d1f6418bdb70ad6a34649fa8`, tested product/observer
`c60022831b728161805e79cf6e88e61c68859fc0`, passes independent functional review.
The tested Combat Extended and Vanilla Expanded converters now run as current
native consumers of cached parsing instructions. The independent source reviewer
found no concrete blocker: exact supplier methods and their dependency publication
checks remain linked to the session, including late publication; current converter
chain, mappings, comparer/type constraints and delegate are revalidated. Converted
results remain uncached. Foreign parser/insertion-entry behavior still falls back
to native, rather than executing arbitrary callbacks twice.

The steward checked three actual functional run manifests, four package files,
35 captured observer/language files, four cache payloads and complete native-equal
language reports. Warm French has 97 Keyed, 1,451 DefInjected and 188 Strings file
hits, zero native files and 26,572 actual native insertion calls; English fallback
has 132/2/75 hits. Focused 32-main and 58-language test logs pass. Callback order
and changed mappings are supported by the focused native replay seam plus pinned
supplier source, not an artificially modified live VEF mapping. The physical mixed
workload is exactly 12 selected/14 active; inventory size is not workload size.

Seven baseline hashes, ten absences, seven rollback receipts and the exact frozen
file repair were independently verified; no game/helper remains. Raw review:
`artifacts/ecosystem-next-20260910/c04/consumer-compatibility/steward-review.json`.
This closes the bounded CE/VEF functional gap. Unknown converters, broader gameplay
and combined compatibility, and useful persistent-language performance remain
open; the earlier separate application gain does not qualify them. No performance
test was run. Preserve this source for the next authorized measurement window.

The original C10 owner next resumes broad assets, beginning with the recorded
engine-serialized streamed-provider alternative and a finite actual-construction,
native-readiness and deferred-work proof. Production integration requires review;
a failed approach does not omit texture/graphics/icon scope. C04 remains required
for full acceptance and returns to its existing owner for subsequent corrections
or measurements. Performance testing is prohibited after owner return; no release,
supplier-removal, default or platform-promotion claim follows.
