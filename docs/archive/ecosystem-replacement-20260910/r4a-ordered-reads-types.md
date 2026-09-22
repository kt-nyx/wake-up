# R4a: XML input and native type results

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

R4a keeps the game's existing XML reading workers and ordering. The supported
new premise is to feed text into the XML parser in small pieces, avoiding the
complete decoded text string that the current constructor allocates for each
file. This is an experimental, default-off input improvement, not a persistent
XML cache or a change to patch execution. It is implemented with focused offline
checks. The small representative probe used less allocated memory and was faster
in seven of eight input comparisons; game startup benefit remains unqualified.

The proposed second type-name cache is unnecessary on the current game: its
native resolver already remembers results. No duplicate setting or cache is
being added. Existing Harmony fallback indexing and leaf-subclass behavior
remain separate and unchanged.

## Scope and actual game contract

Dispatch `wake-up-r4a-reads-types-offline-b686d12` began at clean
`b686d125860249378d6b742cd8ee40a7bcf4c4d8`, using the sole checkout on
`codex/wake-up-r4a-reads-types-offline-b686d12`. The owner permits offline
implementation, focused tests/builds and CPU cost probes, with no deployment,
fixture preparation, game launch or publication. R4b's retained-node XML mutation
work and R4c's remaining capabilities are outside this task.

The freshly hashed Steam `Assembly-CSharp.dll` is
`5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`.
Current native methods were inspected with the existing read-only ILSpy CLI.
Extracts and investigation notes are under ignored
`artifacts/r4a-ordered-reads-types/`. The original ecosystem report/comparison,
independent audit and recovered BetterLoading extracts were consulted; those
historical supplier implementations are ideas, not current game contracts.

### Reading and publication

`LoadedModManager.LoadModXML` visits mods serially. Within each mod,
`DirectXmlLoader.XmlAssetsInModFolder` enumerates selected folders in their native
descending order, collects `*.xml` files recursively, excludes dot-prefixed
filenames and uses the native relative-path dictionary to select the first
applicable override. It then starts two workers; the caller also consumes work.
Every result goes into its original array slot, and both workers join before
the array returns. Later combination preserves this array and mod order.

The native filesystem constructor maps each file, strips one leading UTF-8
byte-order marker, decodes its complete contents with `Encoding.UTF8`, wraps
that string in `StringReader`, and loads an `XmlDocument` using the native reader
settings. XML source names, folders and supplying mods remain on the asset.
This is separate from the game's virtual-directory APIs and string constructor.
Definition construction, inheritance, patch operations and Unity calls occur
outside the proposed input replacement.

### What changed from the losing worker experiments

The [archive](../results.md) records extra XML workers and worker reuse
without repeatable complete-startup benefit. Those implementations changed
scheduling overhead while retaining per-mod completion barriers. Raising the
worker count or renaming that pool is not this task's premise.

Cross-mod parallel `LoadDefs` was also assessed. Ordered final arrays would not
preserve arbitrary callback effects: Gagarin's current-mod context and Loading
Progress's stage callbacks are concrete examples. Broadly moving these methods
onto workers is deferred. A separate read-ahead queue would add bookkeeping
around read/parse work that is already concurrent within each mod.

Streaming instead removes a whole-file intermediate text allocation while
retaining the native scheduler. Parsing through a text reader is necessary:
parsing a byte stream directly would honor XML encoding declarations, whereas
the current constructor always decodes UTF-8 first. It must preserve that native
behavior, including byte-order marks and malformed UTF-8 handling.

## Native type-result disposition

`GenTypes.GetTypeInAnyAssembly` already has two reusable stores:

* A short-name dictionary for types in the game's ignored namespaces, populated
  from `AllTypes`. Later entries with the same name overwrite earlier entries.
* A dictionary keyed by the exact `(typeName, namespaceIfAmbiguous)` pair. It
  retains successful results and null misses from the internal resolver.

The ignored-namespace fast path precedes the pair dictionary. The internal
resolver tries the raw name, a supplied namespace when it is an ignored
namespace, the ignored namespaces in their native iteration order, and mixed
assembly generic syntax. Raw resolution checks primitive aliases, the game
assembly, active mod assemblies in native order, then `Type.GetType`; assembly
searches are case-insensitive. Nested and assembly-qualified names retain the
native reflection behavior. Generic construction and its caught-error logging
remain native.

The name-pair cache is not cleared by `GenTypes.ClearCache`; the ignored-name
table and several discovery caches are. Thus replacing native lookup with a
new supposedly safer successful memo could also change its existing behavior
after cache clearing, assembly changes or dynamic type creation. R4a neither
adds persistent misses nor changes the game's own miss/cache lifetime.

The historical BetterLoading hook calls the native internal resolver behind its
own concurrent dictionary. On this Steam contract that does not establish new
repeat-work avoidance: the ordinary public method already caches the same key.
Making this dictionary concurrent is a different thread-safety proposal, not
evidence that another loading cache is useful.

A materially different alternative was checked: reusing reflection-created
XML construction delegates. Current `DirectXmlToObject.GetObjectFromXmlMethod`
already caches each constructed delegate by type; its list/dictionary helper
delegates are cached too. The archive also records existing field, hierarchy,
alias and custom-loader caches. A lower-level raw-name memo across different
namespace keys would cover only first pair lookups, requires preserving mutable
assembly/namespace order and side effects, and has no demonstrated repeated
work here to justify an additional index. No general reflection cache ships.

**Disposition:** native type memo rejected as redundant on the inspected
contract; broader uncached reflection work remains unproven, not universally
impossible. No production GenTypes/DirectXmlToObject method is changed. Harmony
`AccessTools.TypeByName` fallback indexing and the alerts leaf-subclass feature
keep their existing contracts and lifetimes.

## Offline evidence and remaining qualification

### Implemented boundary and defaults

**Stream XML input (experimental)** is a new saved setting, off by default.
`--wake-up-streaming-xml=on` selects it under explicit candidate controls. Master
disable, explicit-control precedence and every existing default are unchanged.
The option is independent of the existing type/definition search switches.

Required Prepatcher inserts a guarded entry after native source attribution in
the filesystem XML constructor. Every original instruction and exception handler
remains. The false branch falls through into the original try block; a successful
read assigns the original asset's `xmlDoc`. Unchanged native mod/file/patch order,
override selection and duplicate semantics remain authoritative. The separate
string and virtual-directory paths are untouched.

The bridge admits only the startup `LoadModXML` and `ErrorCheckPatches` scopes,
with balanced finalizers and the established loaded-menu cutoff. It opens at most
three streams concurrently and uses 4 KiB buffers rather than a complete decoded
string. Each admitted file is nonempty and at most 16 MiB, with actual open-stream
length matching native `FileInfo` length. The normal parser still allocates its
whole XML document; this bounds additional input work, not all native XML memory.
There is no added worker, completion queue, prefetch inventory, retained document
store or background cancellation task. Native worker joins and cancellation/error
flow remain in place. Cutoff abandons an unpublished stream result and falls back.

Changed constructor instructions, unavailable registration, published hooks on
the constructor or directly bypassed reading/parser helpers, nonstandard
DTD/schema/shared-name-table settings, over-limit/concurrent work and read/parse
failures use the original constructor. This includes all six published Harmony
hook kinds. A speculative failure emits no warning itself; native retry supplies
the original attributed warning once and null document. Files are never modified.

The constructor's native Cecil fingerprint is
`AD1D6439374DFDBF33BDED4C447B4C55E055FC3AFAB1837B1885FB3282B29A62`;
the rewritten runtime semantic fingerprint is
`6AF2664812D69439B58BB56AEC4F36FC5C315927B291E1D8C29C13972F8C5BB3`.
The existing original-PE identity helper reproduced the native expected
`8672989F…` before computing the new identity, retaining original framework
assembly ownership rather than mistaking modern-host type forwarding for game
instructions. Gagarin accepts these two exact constructor shapes. Its constructor
hook makes streaming refuse, leaving its existing original-reader callbacks
intact; no R3 generic-method fingerprint or asset behavior changes.

Settings and logs distinguish selected/waiting, first active read, installation
refusal and completed read/fallback counts. If a supplier skips the XML stage,
there are no active streaming reads. A constructor hook can also leave every
file on its ordinary path; no second saving is attributed to a supplier cache
hit. Loading Progress callbacks and Gagarin's globals are not moved or replaced.
Neither supplier becomes required.

### Focused checks and costs

The guarded focused suite passed **119 managed checks and all 50 Python checks**,
with one optional managed cost probe skipped because the separate native probe
was used. Receipt:
`artifacts/fixture-tests/60a3b0ec804344bfab18dacbd5dab688/features.trx`;
console evidence: `artifacts/r4a-ordered-reads-types/final-test.log`.
It covers startup selection, XML admission/encoding, duplicate independent
documents, bounds, cutoff, published hooks/removal recovery, Gagarin admission,
existing type/leaf searches, background loading and R3 prepatch combinations. The independent code
review found missing guards for the bypassed file-size getter and disposal;
both are fixed, with helper-hook refusal checks. No other actionable finding
was reported. Final integration inspection also added the exact owned streaming
menu-completion postfix to background loading's existing compatibility list,
with a focused coexistence case; otherwise selecting both options would have
unnecessarily refused background loading. No unrelated hook is admitted.

The desktop .NET Framework host cannot load the game's Span-based constructor
tail. Its checks therefore verify complete emitted instructions/exception regions
and execute the new entry in a copy with only that unsupported fallback tail
substituted. The separate **.NET 8.0.31** host executes the complete, actual
rewritten constructor and actual native XML scheduler, enabled and disabled.
It verified ordered source attribution and duplicate basenames, plus six native
missing/malformed/empty-input fallbacks with exactly one attributed warning each.
Only the native warning sink is intercepted to avoid Unity engine logging in the
offline process. These checks do not execute Unity or the actual Prepatcher loader.
See `native-integration.json`, `native-integration.log` and `identity.log` under
the same evidence directory.

The cost probe reads the prior representative 64-file sample (708,912 source
bytes) without changing the fixture. Its control executes the current native
filesystem constructor; its candidate executes the production streaming bridge,
including patch guards and bounds. All parsed documents matched before timing.
Eight alternating pairs produced about **14.29 ms native versus 13.53 ms
streaming median**, with streaming ahead in seven pairs. Allocation fell from
about **4.78 MB to 4.13 MB** per sample. Initial 16 KiB buffers allocated more
than native and were reduced to 4 KiB before retaining the candidate.

Already-initialized guard creation cost 0.134 ms in this host; the standalone
cold guard/Harmony initialization cost 142.918 ms. These are both recorded,
not subtracted selectively to claim a complete win. Actual game patch installation,
Prepatcher serialization, native worker overlap, cold disk reads and complete
startup are outside this probe. The small sample alone cannot repay its cold
standalone setup. The implemented candidate is retained for its bounded input
allocation reduction and modest steady-work result, with the larger in-game
break-even question explicitly pending. See `cost.json`, `cost-final.log` and
`cost-16k-initial.json`; no game startup speedup is claimed.

**Disposition:** bounded streaming input implemented and default off; more
workers/old worker reuse not reopened; whole-mod parallelism deferred at its
callback-state boundary; duplicate native type memo rejected as redundant.
This is not independent persistent XML reuse, completed mutation replay or an
ecosystem replacement claim.

Future authorized live work must establish actual Prepatcher registration,
selected/active/refused feedback, source/error equivalence, supplier coexistence
and complete startup costs separately from offline allocation/parsing costs.
Check native normal fallback with the option off, Gagarin hit/miss and Loading
Progress present/absent, then matched forward/reverse full-startup controls.
New source cannot inherit qualification from previous captures. No live check,
deployment or fixture preparation is authorized by this handoff.

## Source/package closeout and ownership

Implementation source is
`92497021df5e8d235afc2d4fadb89e50119730aa` on the assigned R4a branch.
One guarded `py -3 scripts/fixture.py build` completed with zero warnings/errors.
Its immutable offline package is
`artifacts/fixture-package/92497021df5e8d235afc2d4fadb89e50119730aa/`, with
adjacent JSON receipt and console log
`artifacts/r4a-ordered-reads-types/package-build.log`. Core DLL size is 314,880
bytes, SHA-256
`cc3a17d30b742f764c685e07afcb2a077185e0652866c93e86ae59c8bb9d8bbf`.
This closeout is a subsequent documentation-only commit, not another build.

Final read-only checks confirm the unchanged deployed DLL
`6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`,
fixture `current.json`
`f86ec9e20090ed3a12bffbb439f477e02e33e6644e50de3f71ffe4c435139e7a`
and `candidate.json`
`864734c3c43c8f0ed1cc8e6b9d50cf94d458528dde2cc63f4331459b1b44189d`.
The profile remains English and `prepared.json` is absent. No deployment,
fixture preparation/profile/mod/cache/save change, game launch, normal game/data
operation, Deck access or publication occurred.

The implementing subagent and independent reviewer are stopped. Correction
verification found no unresolved issue in the two focused compatibility fixes.
After this documentation commit, all edits/builds stop and clean checkout
ownership returns to the parent for review. R4b/R4c/R5 have not started here.

## Parent acceptance — 10 September

The parent accepted handoff `86ea094`, source `9249702`, after inspecting the
actual streaming runtime/prepatch, scoped admission, original constructor
fallback, encoding/parser semantics, Gagarin/background coexistence, native type
cache extracts and offline receipts. The existing independent review and focused
checks are adequate; no review-only rebuild or repeat timing was needed.

Acceptance covers an experimental default-off input implementation and the
redundant-type-cache disposition. The small input/allocation result remains
separate from installation costs, native Mono execution and complete startup
benefit. R4b follows under the unchanged offline-only boundary.

The fixture tool's ordinary-settings allowlist does not yet include `streamingXml`.
This would refuse future preparation with the option, although ordinary product
selection is wired. R5 owns the small allowlist/test synchronization before any
authorized live preparation; no fixture content is changed to address it now.
