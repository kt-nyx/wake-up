> Historical snapshot, preserved during the 2026-09-06 cleanup. This is evidence, not current operating instructions or a claim about the cleaned product. See [current state](../current-state.md) and [results](../results.md).
> Original: `records/implementation/OP7_GAGARIN_CACHE_LOADING.md` at `f886277ace5ed9b71fb044e69b57cb2cee5269f3`. Former branches, runtime files, commands and local paths below may no longer exist. Unretained local links are written as historical paths; recover their originals through the [history index](README.md).

# Gagarin existing-cache loading improvement

Reusing a document that Gagarin has already parsed saves about half a second in
the authorized XML-expanded fixture. The final forward/reverse comparisons
reduced the complete cache-loading stage by 499 ms (34%), with menu observation
0.416 seconds earlier on average. This is a small startup improvement, not a
claim about every mod collection or the earlier 1.84/2.22-second controls.

## What changes and what stays observable

The optional `--gagarin-cache on` fixture selector requires candidate mode and
accepted `startup-searches`. `timing` keeps the original work through the same
wrappers; `profile` additionally times each import; `verify` independently checks
the original parse and actual output ownership/mapping. Default remains `off`.
No mandatory Missile Girl package or Gagarin assembly reference was added.

The exact supported Gagarin implementation reads cache text, constructs a real
LoadableXmlAsset (which reads/parses the file again), separately parses the first
text into another document, and imports its children into the destination.
Its constructor postfix also serializes OuterXml before Process discovers that
hashing is inactive. Fresh read-only decompilation of CachedDefHelper and the
asset postfix matches the retained earlier sources exactly.

The change preserves the supplier's original Load instruction sequence, including
its real constructor invocation, callbacks, dictionary clearing, ordered
ImportNode operations, path-based source mapping, destination AppendChild calls,
logging, reader disposal and exceptions. It substitutes small wrappers only at
six exact call sites. The constructor's original reader receives its original
string; an ordinal comparison against the first read proves both parses would
see identical input. Different input/decoding or an unavailable parse retains
the original reader and document. Constructor callbacks are never replayed.

When admitted, the existing constructor document supplies the import loop.
Clearing this reused document and parsing it again are unnecessary and skipped.
The destination document remains a separate original object. Source nodes are
still imported deeply, so no node moves between owners and the constructor's
XML remains intact. The original first text read is deliberately retained to
support exact-input proof and fallback; imports are deliberately retained for
ownership. There is no extra worker or persistent cache.

Inside this admitted cache load only, the postfix receives an empty string in
place of OuterXml when both inspected hashing flags are false. Its original
Process call still runs and takes its original early return. Outside this scope,
including definition/patch loading and cache validation, serialization and
hashing run unchanged. A null document still takes its original failing path.
Gagarin's cache validity, asset hashes, IsUsingCache decisions and miss behavior
are not replaced.

## Admission and optional dependency

Admission requires the pinned GOG game, reviewed native asset-constructor body,
active vr.missilegirl, one loaded supplier module, exact Gagarin disk SHA-256
D83EDC9FE3AE0381A71EE460F766563F1CB078F61211D8388614BAB3B0E1250C,
module 23d812a3-057c-4caf-aa0c-6aa2e37e1ba3, and exact loaded IL bytes for the
cache body, asset callbacks, context/path getters, combine callbacks and the
supplier's own timing callbacks. The memory-loaded plugin has no normal assembly
Location, so the active package file and loaded module/body checks are combined.

Unknown hooks on these methods, the real constructor, or the affected XML
construction/load/clear/serialization and StringReader sites refuse reuse.
Only exact supplier callbacks and this assembly's known diagnostics are allowed.
Hooks are checked again at cache entry. Failed installation removes this
integration's patches. Missing/unsupported supplier admission is independent of
the existing startup searches. Nothing in this class needs Gagarin to resolve
RLO's assembly or construct its Mod.

## Measurements and costs

Final tested research package: fad9338033a925fe0d262825fa13cc01e30aab3f.
Every final comparison restores only the Gagarin cache from gcache-s04-miss,
using the fixture's matching-package restoration checks. All four logs explicitly
confirm `Finished loading XML from cache!`. Identical content/order, observer,
diagnostics, searches, two-reader DDS and PNG options are retained. No rejected
concurrency prototype is enabled. The last two rows reverse the first pair.

| Label | Change | Complete stage ms | Menu seconds |
| --- | --- | ---: | ---: |
| gcache-f01-control | Disabled | 1433.982 | 19.242016 |
| gcache-f02-reuse | Enabled | 983.539 | 18.711049 |
| gcache-f03-reuse-repeat | Enabled | 965.515 | 18.698661 |
| gcache-f04-control-repeat | Disabled | 1513.747 | 18.999652 |
| Mean control | | 1473.865 | 19.120834 |
| Mean enabled | | 974.527 | 18.704855 |

Savings are 450.443 ms forward and 548.232 ms reverse. The stage timer surrounds
CombineIntoUnifiedXML, including per-call admission, text comparison, original
constructor/hooks, imports/mapping, completion and the load receipt write. Menu
observation is the first completed repaint, not process lifetime or menu dwell.

Startup hook installation costs 12.4-13.5 ms, occurs before LoadModXML, and is
included in menu time. Cache-entry admission costs 3.18-3.23 ms enabled. Enabled
first reads took 164.0-165.7 ms, constructors including exact text comparison
558.3-558.9 ms, and the remaining stage about 239-241 ms. These are complete
observed costs, including unfavorable read variation. Controls spent 455-472 ms
on the second parse and 152-162 ms serializing OuterXml; the latter is nested in
constructor time, not additional to it. Total savings use the outer stage only.

Discovery gcache-p03-profile measured 237 ms in imports, 446 ms second parse and
158 ms OuterXml, with 1445 ms overall. Per-import timers are disabled in the final
pairs. Narrow wrapper/timer/receipt costs remain in both arms and in the outer
measurement; there is no claim of a separately isolated zero-overhead runtime.
The correctness-only verification run deliberately repeats parsing/hashing:
2339 ms stage, including 316 ms output mapping/ownership/hash checks. It is not a
performance arm.

Ordinary reuse adds constant-size scope state and no document-sized collection
or new string. It removes one parsed document and one serialized XML string from
the original work. The first string/reader and original imports remain. The
scope is released at Load exit, including exception exit. No reliable allocated-
byte or peak-process-memory measurement is claimed. Verification uses bounded
streaming hashes and, temporarily, the second document present in the original
algorithm; no general serializer/profiling framework was built.

## Correctness, fallback and limits

All seven final research captures passed automaticTestPassed with normal exit
code zero and no new errors in the focused log scan. All six Gagarin-enabled
captures have byte-identical Unified.xml and Unified_Original.xml.

gcache-v04-verify independently parsed the original reader and matched its whole
document hash against the reused source. The live output contained 19,534 roots,
19,525 mapped source assets, zero incorrect source object identities and zero
wrong document owners. The nine unmapped roots retain original behavior. Output
hash: 3564FD850F2C5230C4C484053AC1436314D28A763057B89B97DBDBE36CDB00E7.
One real cache-file constructor and one skipped serialization were observed.

gcache-s04-miss enabled the option with no restored cache: Gagarin reported its
normal miss/creation, never called the cache-load replacement, reached the menu
in 21.182259 seconds and exited normally. gcache-a04-no-supplier used the
activation selection: no Gagarin assembly loaded, the optional integration
refused locally, existing definition/type searches installed and completed, and
the menu/normal exit passed at 9.409226 seconds.

Nine focused managed tests (including game identity) and 37 fixture Python checks
pass. New tests cover no supplier assembly references, exact/different constructor
text, reader/rewrite shape refusal, retained ordered instructions and metadata,
unknown parser-hook refusal, original fallback parsing, independent node
ownership, unchanged source XML, missing mapping and repeated source identity.
No full-suite or gameplay/save-load qualification is claimed. Medium/full remain
paused; OP7 is incomplete and OP8 inactive.

Evidence is private under artifacts/gagarin-cache and canonical fixture results.
final-research-summary.json contains exact revision, acceptance, cache hashes,
errors and timing receipts. Earlier a3e723d/e03011e attempts safely refused:
they exposed the supplier's additional timing hooks and the actual inherited
XmlNode.RemoveAll call. They provide no reuse performance result. cc30cae gave
the initial 626 ms gain; fad9338 added exact-input proof before the final repeats.

## Local integration boundary

Original main 7785ddb is 48 commits behind the original research checkpoint.
Do not merge that history wholesale. Preserve codex/startup-work-reduction at
54bee10 and this dedicated research branch. Integrate this feature, focused
tests, and only the necessary fixture support for the current exclusion overlay,
automatic background loading, XML-expanded selection and focused test filter.
Existing main searches/DDS remain unchanged. Newer DDS/PNG implementations and
unrelated experiments remain intact on the research branch. No push, release,
normal game/profile/cache writes, UI automation or security changes are authorized
or performed. Main build and integration verification are recorded below.


## Completed selective integration

The integration branch starts directly at 7785ddb, with feature/prerequisite
commit 427ad8f and final reviewed runtime f37b34d. Review moved unknown-hook
refusal before invoking optional state getters. No unrelated research history
was merged. The existing main search/DDS source files are byte-unchanged; the
only existing product-code edit is one independent initializer call. The newer
DDS/PNG implementations remain on codex/startup-work-reduction (54bee10) and
codex/gagarin-cache-loading (2eb11b5). All twelve original stashes remain.

The integrated package f37b34dc9725ed99cdfa9759fa22af7502326756 was explicitly
built and deployed. Its build has zero warnings/errors. Forty focused managed
checks, including existing searches and all new cache checks, and 24 fixture
Python checks pass. The fixture prerequisite checks retain the original seed,
background opt-out, reversible exclusion, overlay identity and launch preflight.
Its metadata audit matches the existing frozen collection and owner exclusion.
The smaller main suite does not include unrelated research-only tests.

All five final integration captures bind exactly that source revision and pass
automaticTestPassed with normal exit code zero. The focused log scan finds no
new errors. No UI actions or forced exits occurred.

| Integrated-package run | Behavior | Complete stage ms | Menu seconds |
| --- | --- | ---: | ---: |
| gcache-m02-activation | Gagarin absent; existing searches active | Not installed | 9.232553 |
| gcache-m03-miss | Normal miss and cache creation | 665.194 | 21.378968 |
| gcache-m04-verify | Independent parse, mapping and ownership checks | 2274.553 | 19.502826 |
| gcache-m05-control | Confirmed hit, reuse disabled | 1388.107 | 19.076193 |
| gcache-m06-reuse | Confirmed hit, reuse enabled | 1011.251 | 18.242020 |

The supporting integrated pair saves 376.856 ms (27.1%) in the complete stage
and observes the menu 0.834173 seconds earlier. It uses existing main defaults
for graphics in both arms; research DDS/PNG options are not present in main.
Do not compare its total menu times directly against the research arms.
The forward/reverse research comparison above remains the repeated result.

Integrated hit installation costs 17.4 ms in each arm, with candidate cache-entry
admission 3.193 ms. Candidate first read costs 171.333 ms, constructor/input
comparison 572.048 ms, and the remainder about 265 ms. The complete outer stage
includes these costs and completion diagnostics. Control second parse costs
444.740 ms and its nested OuterXml serialization 163.714 ms. Actual net savings,
not those costs added together, are the result.

Verification again matched the independent source parse, all 19,525 asset-object
associations and ownership of all output nodes, and reproduced the exact output
hash and 19,534 roots above. All four Gagarin-enabled integration captures have
identical original/patched cache XML. The verification run's 312 ms output check
and deliberate extra parsing/hashing are diagnostic work, not performance data.

Closeout keeps the tested f37b34d package deployed and gcache-m06-reuse captured;
the game is closed. Following commits change only the closeout documentation.
Local main receives the selective integration by fast-forward, and its final
build is checked again (artifacts/gagarin-cache/main-closeout-build.log). That
additional documentation-revision package is a build check, not a new deployed
or live-tested package. main-final-summary.json retains the captured evidence.
No push, publication, release or full/gameplay qualification is claimed.
