# First-pass combined review — 9 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**Current proposed release scope — 10 September:** the [retained-feature report](retained-features.md)
supersedes this first-pass coverage table and its pending omission-approval
language. Every original goal plus S14 now has an explicit proposed disposition
under the owner's redesign/drop authority. Candidate `d584eb3` retains native
preparation, removes public lossy choices and adds qualified native world-page
background loading. Final functional run `rf-retained-06` passed with the native
prepared hit, display and real minimized world completion. This handoff awaits
steward review; isolated Steam and reliable baseline/pre-revamp/new comparisons
remain mandatory and incomplete. The earlier text below records historical
checkpoints, not current permission gates or current package identities.

**Subsequent functional evidence:** the steward accepted the [small Windows qualification](windows-functional-qualification.md#parent-steward-acceptance), covering 21 captured runs, concrete fixes and a passing final combined run on source `79dde43`. This does not establish release readiness. The table below retains the first offline pass's historical coverage; its live-unqualified statements are superseded only within the qualification report's explicit scope. All teams have stopped, ownership is with the steward, and the recurring check is paused. The earlier proposed startup permission request below is historical and has been fulfilled by this authorized pass.

The first serial offline pass is reviewed. Several useful capabilities are implemented, but this is **not complete ecosystem replacement or a release-ready build**. Research checkpoints that stopped without integration do not count as delivered features. Every original goal remains tracked below; no permanent omission was approved. Linux helper work alone was explicitly deferred by the owner until later.

## Delivered scope and remaining coverage

| Original goal | Accepted source/offline scope | Missing or unqualified capability |
|---|---|---|
| 1. Independent XML reuse | S01 storage available; S05 feasibility research | S04 parsed input, S05 processed replay/inheritance reuse undelivered. Tested S04 encoding lost; further routes require changed evidence. Existing Gagarin integration remains a supplier dependency. |
| 2. Translation loading | S03 faster translation application, default off | S06 persistent language reuse undelivered after negative complete-cost checkpoint; production Mono/live translation behavior unqualified. |
| 3. Broader faithful texture caching | S07 PNG/JPEG coverage within existing limits | S09 first-build acceleration undelivered; native platform fidelity/restore/benefit for new coverage unqualified. |
| 4. Cache ownership and maintenance | S01 bounded stores, eviction, recovery and launch controls | New behavior has offline evidence, no live qualification. |
| 5. Optional on-demand loading | S11 successful texture-holder routing, independently useful and default off | On-demand itself undelivered: no useful complete-consumer family established. S12 progressive detail deferred at the missing readiness prerequisite. Resources/bundle route memoization also absent. |
| 6. Texture preparation and quality | S10 storage/controller, native preservation and Windows CPU helper, opt-in | Lossy role coverage is only guarded BGPlanet backgrounds with zero fixture matches; broad useful recompression/downscaling unfinished. Linux helper deferred. Live output/UX and benefit unqualified. |
| 7. Code searches | S02 scoped leaf-subclass lookup, retaining established searches | New runtime benefit and broader mod compatibility unqualified. |
| 8. Loading display | S08 independent optional display and bounded diagnostics | Live layout/overhead unqualified; full per-mod profiling not implemented. Previews already lazy natively. |
| 9. Background loading | S13 default-off ordinary save-loading lifetime and completion-frame guard | World creation/standalone map generation not admitted. Actual focus, minimization, engine hooks and save/gameplay behavior unqualified. |
| Additional S14 warnings | Existing two legacy conflicts retained; Loading Progress refusal text clarified | No new unconditional or active conditional conflict rule justified; no launch dialog added. Actual UI not exercised. Revisit catalog as delivered scope changes. |

Each slice handoff contains its exact source and focused evidence. S14 accepted source `d44a91252b8df80890cd3fa356547348aab34aab` carries the combined current runtime; handoff `0aef260cfcbe6c362b75e0bd18b7c364c4203ad0` adds documentation. Latest core package:
`artifacts/fixture-package/d44a91252b8df80890cd3fa356547348aab34aab/`.
Its 242,176-byte DLL SHA-256 is `3581a35a6f5cbd1a59307d10c0c0ea210c5416cce6f2938927e319c412957c77`, independently checked by the steward. This is the private two-file fixture package, not a public release or combined helper bundle. The earlier optional Windows helper has its own accepted S10 identity; no final helper-bundled release package was built.

## Operational and release limits

All slice teams have stopped and returned the clean checkout. No new runtime was deployed or launched in this pass. The read-only fixture candidate receipt still identifies old source `4422f6222a54c365618e154de66ba88ee048ab6f`; no `prepared.json` exists at fixture root. A fresh prepare is required for a future run. Normal Steam RimWorld was last verified running outside the fixture and must not be closed or changed by the steward.

Individual offline checks are not a combined production-runtime qualification. Before release, establish feature-by-feature Unity/Mono activation, output, lifecycle and meaningful workload behavior; then qualify the combined selection. Preserve source/package/deployment/run identities and normal exit evidence. Existing public template descriptions and historical release-manifest validation fields describe older baselines and must be reconciled to the actually qualified new scope before any public package or publication. Do not carry those statements forward as qualification of this runtime. Final missing-capability disposition requires an owner decision, with truthful replacement claims.

## Recommended next step and exact permission boundary

Request approval to deploy the exact current core package through `scripts/fixture.py`, deploy a matching menu observer if needed, prepare the **small Windows fixture profile with ordinary user activation and unchanged defaults**, and perform **one startup-to-menu check with the independent observer and normal automatic exit**. The owner subsequently approved concurrent functional checks: the normal game may remain open when its executable is freshly verified outside the fixture. An existing fixture process or uncertain identity still blocks the operation. Prepare this run with `--purpose functional`; do not close the normal game. Use a fresh run label, `--menu-observer --exit-after-menu-ready`, capture actual exit and `automaticTestPassed`, and stop on failure. No medium/full profile, save/gameplay run, UI automation, normal data modification, Deck work, publication or timing-benefit claim is included in this proposed check.

The process-policy change itself does not grant the still-pending live launch approval.

This narrow first check establishes whether the assembled default runtime starts, including the new required-Prepatcher integration. It does not qualify default-off features or solve missing capabilities. After its result, plan focused feature activation and live checks separately and bring consequential scope tradeoffs back to the owner. Do not reopen rejected XML/image routes without changed evidence or expand on-demand hooks without a justified consumer/ownership premise.

The five-minute workflow pauses awaiting that specific deployment/live-check decision. No further slice task or run is dispatched until the owner authorizes continuation. This closes the initial offline execution pass, not the full product objective.

## Owner continuation — 9 September 2026

On 9 September 2026, the owner explicitly authorized performing functional test runs now, iterating on failures and using separate tasks/sub-agents as appropriate. The next team owns small isolated Windows qualification: deployment/preparation, observed startup checks, focused activation/correctness checks and necessary fixes/retests. Use --purpose functional; the normal Steam game may stay open and must remain untouched. Keep workload small, one fixture process at a time, normal automatic exit and captured evidence. This grants no performance-comparison, medium/full workload, normal data, Deck, UI automation, security/system tooling or publication permission. Stop when sufficient focused evidence answers the admitted checks; do not broaden harness work without a concrete failure. This supersedes the pending authorization and pause above. A fresh qualification team begins from the ensuing authorization record commit, with first run using ordinary defaults before focused activation checks. No current runtime live qualification is claimed until its captured evidence is reviewed.
