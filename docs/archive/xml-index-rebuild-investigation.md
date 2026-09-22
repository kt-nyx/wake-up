> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](results.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.
> Original: `records/implementation/OP7_OVERNIGHT_SHARED_XML.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Shared XML lookup work investigation

Historical branch handoff: deployed runtime and fixture selection below describe
this experiment, not the current fixture. See [the later restored state](loading-experiments-closeout.md).

**Closed without a meaningful improvement, 2026-09-06.** The narrow candidate
avoided only one of 232 builds. It is not promoted; do not expand mutation
bookkeeping to rescue this negligible opportunity. Main remains `7d464ea`.

Work was performed on `codex/overnight-shared-xml-work` directly from
main `7d464ea`. The owner's overnight authority covers implementation and all
fixture sizes while idle, within the permanent isolated fixture. New work must
generalize across modlists; no single-mod workaround is included.

The accepted definition lookup builds a temporary name index and invalidates
affected definition types when XML changes. Recent full captures report 232
builds and 121 invalidations, but do not measure that construction cost. They
also report 13,103 hits without counting how many ask only for the definition
itself. Those queries currently repeat a self/type/name XPath predicate after
the index already found an exact unique match.

The first package adds only optional aggregate measurement using
`--lookup-work timing` alongside candidate startup-searches. It measures all
index construction and counts successful empty-tail queries. Default is off.
Original lookup, validation, native lists, mutation ordering and fallback remain.
The complete patch-stage timer includes counter work; index time is nested in
that stage and must not be added to it. Both aggregate receipts are written
after that stage timer stops. No worker, extra cache or broad profiler is added.

Measured construction is an upper bound, not removable time: first builds are
necessary and complex invalidations may need rebuilding. Only if a material
cost is found will a narrow behavior change be considered. Native `.` selection
for exact singleton queries and avoiding invalidation for ordinary roots with
no defName contribution are hypotheses, not measured improvements. Duplicates,
custom nodes, entities, root replacement, nested/aborted mutations and
pendingMutations retain current conservative behavior. No internal XPath list
bridge, broad BCL interception or arbitrary worker discovery is planned.

Retained overnight records explain why newer DDS/PNG readers are not promoted
and HugsLib is parked after the scope correction. Only those records are carried
from `455ccbe`; their runtime implementations are absent from this branch.
Main searches/Gagarin and the original main DDS remain the product baseline.

Private evidence is under `artifacts/overnight-shared-xml` and canonical fixture
results. OP7 remains incomplete and OP8 inactive. No push or release is planned.

## First measured ceiling and narrow trial

Package `299a338d18f8251a78bf21b6816b0a50e2de360a` passed 38 managed checks and
24 fixture checks, built/deployed explicitly, and completed two automatic runs.
Activation `night-xml-a01-cost` observed menu at 9.481491 s, with 7.765 ms in
eight builds and six empty-tail hits. Full `night-xml-f01-cost` observed menu at
191.453176 s: complete patches 24965.103 ms, all 232 builds 1315.048 ms,
121 invalidations and 2545 empty-tail hits among 13103 hits. Both exited zero,
passed automatic testing and recorded original lookup behavior.

The measured total construction is about 5.3% of that patch stage and 0.7% of
observed startup. Only part could be avoided. The empty-tail count does not
justify a separate compiled-expression mechanism or broad BCL hooks.

The optional `--lookup-work unnamed-roots` trial retains an existing type map
only for an ordinary unnamed root insertion/removal with the opposite parent
null and mutation depth exactly one. Native unnamespaced elements and direct
children are checked; direct defName children, entities and custom nodes retain
invalidation. Queries during mutations still follow native XPath through the
unchanged pendingMutations mechanism. There is no incremental name dictionary
maintenance. Named/duplicate/complex changes retain the original algorithm.
`timing` remains the original comparison arm. The native XPath calls, list
construction, full-expression validation and multi-result fallback are unchanged.

Five added test cases exercise real output identity/order, skipped rebuilding
on repeated unnamed insert/removal, duplicate-name fallback, nested defName
mutation, abort fallback, and custom/entity refusal. Live qualification follows;
no candidate gain is yet established.

## Final result and stopping decision

Candidate package `6ba144a57f86ed5e918ba645af5d652b1fbdb66f` passes 43 focused
managed checks and all 24 fixture Python checks, with no skips. It builds with
zero warnings/errors and was explicitly deployed using `deploy --package`.
Every launch checked the prepared candidate revision. Independent steward
review found no concrete defect in the deliberately narrow condition/tests.

| Run | Lookup | Patch ms | Build ms | Builds / invalidations | Retained notifications | Menu seconds |
| --- | --- | ---: | ---: | --- | ---: | ---: |
| night-xml-f01-cost | Original measured, 299a338 | 24965.103 | 1315.048 | 232 / 121 | Not collected | 191.453176 |
| night-xml-s02-retention | Expanded small candidate, 6ba144a | 2193.913 | 85.887 | 46 / 8 | 0 | 20.969867 |
| night-xml-f02-retention | Full candidate, 6ba144a | 25004.159 | 1314.324 | 231 / 120 | 6 | 193.181474 |

The six retained before/after notifications are not six avoided builds or
necessarily six distinct edits. The actual reduction is only one build and
one invalidation. Construction and complete patching are effectively unchanged.
These discovery/candidate captures use different packages, so sub-millisecond
differences are not a matched-package performance result. No speedup is claimed;
the trivial eligibility is sufficient to stop without another pair. The
empty-tail `.` rewrite was not implemented or timed, and no broader incremental
index or expression-cache mechanism was added.

All four launches in this investigation exited normally with code zero and
`automaticTestPassed=true`. Both full runs retain 316 packages, 47,388
definitions, 14,748 patch operations, identical relative mod order, reversible
exclusion, background preference, observer and fresh application-cache policy.
Accepted startup-searches and Gagarin are on; both full runs create the ordinary
Gagarin cache rather than restoring a hit. Original main DDS is unchanged.
Combined original/patched XML and all four captured definition reports are
byte-identical across the full runs. This supports startup content preservation,
not save/gameplay qualification.

`artifacts/overnight-shared-xml/summary.json` and its small `summarize.py` reader
retain portable per-run identity, metrics, counts and six content hashes.
Build/deploy/check and per-run preparation/launch logs are alongside them.
Final selected/captured label is `night-xml-f02-retention`; runtime `6ba144a`
remains deployed with the experimental selector. The game and launcher are
closed. Later closeout changes are documentation only. Default `--lookup-work`
remains off, all experimental behavior stays on this research branch, and main
remains `7d464ea`. No merge, publication or normal installation writes occurred.

## Read-only interpretation of remaining common stages

The exact frozen Loading Progress implementation wraps each per-type
`AddAllInMods` callback, not the outer registration stage. Its profiler owns a
stopwatch per thread and sums the elapsed callback intervals into the
off-thread category. Current full captures record 10973.481 / 10791.076 ms
there (active-thread callback totals 406.689 / 625.355 ms). These are not serial
startup delays or CPU counters. Earlier roughly 16-second values of the same
category cannot be treated as a 16-second recoverable stage. The pinned native
`PlayDataLoader` already executes these callbacks with `Parallel.ForEach`.

One new general source-level lead exists: every `DefDatabase<T>.AddAllInMods`
re-sorts the mod list by OverwritePriority then RunningModsList.IndexOf, and
`GenDefDatabase.DefsToGoInDatabase<T>` rescans each mod's full definition list
using OfType<T>. This is repeated for each definition subclass. Sharing stable
mod order or preparing type-specific input could avoid common work, but actual
outer stage duration is unmeasured and existing concurrency reduces its possible
wall-time payoff. Native override order, duplicate handling, base-type
membership and callbacks/hook composition must remain. This is a possible next
cost question, not an implemented recommendation to replace the loader, and is
distinct from the rejected named-lookup lock-batching experiment.

Loading Progress's XML-processing category is 11943.611 / 11196.258 ms here.
It is attached to ParseAndProcessXML, whose native body registers/resolves
inheritance, creates definition objects through generated parsers, and assigns
them to mods. Generated parsers can invoke constructors, custom XML loaders and
PostLoad methods; this is not just generic numeric/text conversion. Gagarin also
attaches cache-save behavior on misses. The profiler's nested category timing
restarts its stopwatch, so this category must not be presented as a verified
inclusive outer duration or pure parser CPU. Existing scalar-conversion and
inheritance trials remain closed; no specific substantial avoidable parser
work was newly identified.

The source checks used the already installed read-only ILSpy CLI and retained
decompiled sources. New metric-source extracts are `lp-def-registration.cs`,
`lp-profiler.cs`, `lp-single-thread.cs`, `lp-parse-process.cs` and
`def-database.cs` under the private evidence directory. No new profiler,
instrumentation or live run was introduced for this final interpretation.
