# Compatibility Phase 2 — bounded correctness

This phase checks whether the retained integrations actually return correct data
when used. Phase 1 established startup compatibility but did not exercise native
bundle retrieval, image-cache reuse or Gagarin's warm parsed-XML reuse. The owner
authorized closing those three gaps after accepting Phase 1 at
`13b0d8eae795ce67b05863b3dc79cf265395386a`.

## Scope and limits

- Missile Girl: build its actual cache, carry it through supported fixture
  operations, and prove Wake-Up's parsed-XML reuse returns the expected result.
- Image cache: use a few small private PNGs, compare produced textures with the
  native game decoder, and observe actual publication and later cache hits.
  Include Loading Progress's reload integration.
- YaOpt: use a small existing legal asset bundle and prove native bundle lookup
  returns the same object while YaOpt's top-level loading wrapper remains intact.

All launches are isolated current GOG runs through `scripts/fixture.py --purpose
functional`, with independent menu observation, normal exit and feature-specific
output assertions. Cache-building and reuse runs are correctness checks; their
timings are not comparisons or performance samples. Performance planning does not
authorize execution. No benchmarks, stress tests, Steam/Linux/Deck qualification,
normal installation/profile changes, foreign unpatching/settings writes or
publication are assigned. Retired optimizations and additional suppliers are out
of scope. No large bundle toolchain or workload project is authorized.

## Executed sequence and results

Feasibility inspection supports seven initial functional launches: a fresh
Missile Girl cache-building/reuse pair; native image-cache build/reuse and Loading
Progress image-cache build/reuse pairs; then one YaOpt bundle run. Gagarin's existing
verification compares reused XML with a fresh parse and checks node ownership.
PNG verification compares rendered output at every mip level with the native
decoder. The bundle check uses the already installed Ideology `resources_ideology`
bundle and a known Texture2D asset; no asset project or toolchain is needed.
A narrow observer checks the independently enabled bundle route while leaving
YaOpt's top-level wrapper intact. One owner controls edits, builds and fixture operations;
independent agents only plan or review. Continue the accepted checkout without
resetting to private main. The permanent fixture is restored to its baseline at
completion, retaining all unsuccessful evidence.

Private inputs, run identities, review and final report belong under
`artifacts/compatibility-campaign-20260923/phase-2/`. The parent owns its
`steward-status.md` and a separate `performance-plan.md`; neither is an active
performance assignment.

All three correctness gaps are closed in this bounded GOG workload. Seven runs
passed their feature-specific acceptance checks. One additional image run returned
correct textures but failed to reuse the cache because preparation selected the
legacy cache directory; that unsuccessful reuse attempt remains recorded.

| Check | Actual evidence |
| --- | --- |
| Gagarin cache construction / reuse | Fresh supplier cache was carried with matching identities. Warm reuse was admitted and executed; verification matched a fresh parse for all 13,220 root mappings, with zero ownership or mapping errors. Both runs produced the expected private XML definition. |
| Native image cache construction / reuse | Two small PNGs produced two cache entries. The reuse run recorded two hits, zero misses and zero writes. Both 32×32 and 32×16 outputs matched the native decoder, including mip levels and texture properties. |
| Loading Progress image cache construction / reuse | The same two PNGs passed construction and reuse checks through 13 Loading Progress reloads, with zero reload fallbacks. Its own LP_Eye image used the ordinary DDS sibling, so it could not satisfy the PNG comparison assertions. |
| Native bundle lookup with YaOpt | The actual Ideology FluidIdeo texture was the same 128×128 object through native and cached lookup. Three route hits and two actual YaOpt wrapper calls were observed. Repeated lookup performed its native asset load. |

All seven accepted runs reached the independently observed menu, exited normally
with code zero and passed compatibility observation plus the expected XML output.
Image checks separately require successful comparison records for both private
dimensions and the exact build/reuse counters. Warm grouped restore bypasses the
PNG processing method, so its processing-call count is zero while hits and native
comparisons are both two. No decoder, error or fallback count is treated as a hit.

The current PNG cache shares `PreparedTextures/v1/groups`. Use the existing
`--prepared-cache-from` operation to carry its verified captured data. The initial
`--png-cache-from` attempt carried only the empty legacy PNG ownership marker and
therefore rebuilt both entries; it is not accepted as reuse evidence. No guard was
bypassed and no cache entries were fabricated. “Cold” here means the selected mod
cache begins empty; the operating system's file cache was neither reset nor controlled.

The bundle observer temporarily disabled only Wake-Up's bundle route to obtain
the native comparison, then restored it. It counted calls using a temporary
observer-owned prefix on YaOpt's wrapper and removed only that prefix afterward.
YaOpt's wrapper remained enabled and Wake-Up's top-level route remained disabled.
The actual bundle hit and matching object establish coexistence; this does not
exercise YaOpt's separate lazy-holder texture completion branch.

## Changes and verification

No product loading implementation changed in Phase 2. Every live run used the
accepted product source `a5b85683d275a288903ded515001e742e82b6b8a` and observer
`cad6c6d78bda73c2acdd5441584dd851d8a073a6`. The small observer extension uses
code-free private input markers; ordinary workloads do not select it. Observer
build completed with zero warnings/errors. Independent read-only reviews checked
the code, actual cache records, source selection, wrapper cleanup and final receipts
and found no blocker.

Fixture notice-history carry is now independent of the functional UI probe. A
future ordinary timing preparation can carry only the exact, hash-verified bytes
of a successfully captured functional version-1 ledger containing acknowledgements
and no pending entries. Functional carry still permits pending history. New notices
are not suppressed and require qualification before timing. Seventy-five synthetic
fixture tests passed, including ordinary preparation without probe flags, pending
history rejection and continued rejection of functional probes in performance mode.
No actual performance preparation or launch occurred.

The future performance plan is separate, private preparatory work. This phase makes
no speed claim and does not authorize running it. These correctness results also
do not qualify Steam, Linux/Deck, large modlists or gameplay.

## Restoration and handoff

Supported refresh restored Core, Prepatcher and Harmony with only generated
ModsConfig in the profile, no deployed/parked product or observer, and no fixture
process. Full content audit passed. Generation `20260924-182858-e02e5916`, manifest
SHA-256 `2ff581c1856b4d348d8cd5d497915fe133555b2ae8578e8c89aab06ff0aa616e`.
The permanent game assembly hash remains
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`; the native
Ideology bundle also retains its original SHA-256
`69f263f72799bd42fab17ac9fa8a96f627211950e727a3ae5f0915c0a35a3e07`.

The private final report, eight-run identity index, seven acceptance receipts,
unsuccessful reuse record, source-input hashes and restoration evidence are under
`artifacts/compatibility-campaign-20260923/phase-2/`. The steward accepted this
bounded correctness and tooling work after inspecting raw cache/bundle evidence,
the narrow changes, independent review and restoration. No further tests or
launches are assigned; performance execution still requires fresh authorization.
