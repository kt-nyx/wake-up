# Current state

**F1 functional qualification — 10 September:** live fixture checks found and
fixed two texture-method identity checks after the audio rewrite. The corrected
core has bounded Windows startup, streaming XML, audio first-use/holder cleanup,
native texture preparation/reuse and DDS export evidence. See the
[F1 report](rc2-functional-qualification.md) for actual
passes, refusals, preserved probe failures and remaining limits. There is no
performance, ordinary-colony, Linux or release-ready claim. Parent review accepted
handoff `90c1199`; the non-disruptive campaign is complete and its timer is paused.
The fixture profile is restored and publication remains unauthorized.

R5's original source-inclusive rc.2 ZIP remains immutable and contains the
unfixed core. Its packaging/offline evidence remains in the
[R5 report](r5-combined-offline-review.md). The corrected
F1 private build and deployment are separate; no new release ZIP was produced.

## Current source and product scope

F1 began at clean `44278830c6d9bff3d438aa9c0e3bff3b3ebb8412` on
`codex/wake-up-f1-functional` in the single existing checkout. Corrected build
source is `a70873e300c51930ebdff63a43c051257d6bba40`; later observer/tooling and
report commits do not change that package's source identity.

R1 fixes texture-routing hook admission and stale status. R2 adds broad active-mod
native preparation, matching automatic-entry promotion and optional Windows
full/half/quarter DDS exports. R3 adds experimental default-off streamed audio
for a bounded compiled consumer family. R4a adds experimental default-off streaming
XML input. R4c adds ordinary new-colony background lifetime, independent native
summary hiding, bounded per-mod XML timings and expanded diagnostics.

Read [retained features](retained-features.md) for all nine
original goals, S14 warnings and recovered BetterLoading ideas. Required Prepatcher
remains; suppliers are optional. Existing defaults remain, new options default
off. Automatic PNG/JPEG storage is 512 MiB; native preparation and exports share
another 512 MiB. Exports do not automatically change in-game quality.

Independent XML/parsed-language reuse, general deferred/progressive textures,
grouped stores, full atlases, broad automatic formats, full profiling and general
map/background coverage remain missing. R1/R4b XML experiments are test-only;
the revised R4c language experiment was stopped. R3's reflection/generated-code
and worker-first-use limitations remain explicit. This is partial coverage.

## Source, package, deployment and evidence

| Layer | Identity and meaning |
|---|---|
| Public 0.2.1 | Source `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f`; DLL `47164d9c94668d536a6d94922e047986710a6d5f62df9b639cee737e66de977d`. Publication state was not refreshed. |
| Historical 0.3.0-rc.1 | Source `8ca0dd763a2ad530668b59d6c3488f28eac311f5`; DLL `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`, superseded in the fixture. Its compiled instructions matched retained core `d584eb3`. |
| Older local ZIP | `ab59fce2fcdc6eb4891cfb3b3e46e8b6e03c151885f7593b19e219b147db3c48`; core-only, historical ordinary-entry review. |
| Original 0.3.0-rc.2 bundle | Built source `a9aed0e43bb10e032d75d4b6b843772b0f2a466a`; DLL `a19696a37378646e9739bfc092d68eaed1b80bac08c14fbc0a72763093bae612`; Windows-helper ZIP `dc7a94213b77353570eb26ce52e7376f2b9fe7d76167b4b41afe737ec097957a`. No inherited startup, ordinary-entry, Steam/Linux or release-ready flags. |
| Corrected F1 fixture candidate | Build source `a70873e300c51930ebdff63a43c051257d6bba40`; DLL `9c2dcec8cbe648ee78350ebb4460c7d3a3ba10412db6a4f8957f9eb51606c4cd`; pinned Windows helper `b351d1794b82f2310f643c307d54498693db8c6818380bc6dd5e4141489023e5`. Bounded functional evidence only; original rc.2 ZIP does not contain this fix. |
| Exact Steam reference | Assembly-CSharp SHA-256 `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a`; Version.txt rev590, player previously reported rev591. |
| Reset fixture | English, original seed order, Wake-Up settings absent and no prepared run; corrected DLL/helper retained separately. F1 captures are preserved; normal game/data were untouched. |

## Historical qualification and comparisons

Older manual colony/save/reload evidence belongs to the
[ordinary-entry candidate](ordinary-entry-final-candidate.md).
Shared InputLegacy/bootstrap errors and the older automated-probe engine risk
are not declared repaired. Earlier Linux evidence belongs to 0.2.0, not rc.2.

The French 99-package fresh-application-cache series averaged 153.600 s absent,
68.258 s public 0.2.1, 68.634 s old candidate defaults and 67.164 s old optional
translation/routing. The separate populated-cache series averaged 105.698 s absent,
59.592 s public supported options and 57.052 s old all-selected candidate, four
measured repeats each. Windows file caches were unmanaged. These are historical
menu results, not predictions for rc.2, universal averages or full replacement
proof. See [fresh comparisons](steam-qualification-comparisons.md)
and [warm comparisons](all-options-performance.md).

## Guidance and history

[Development](../../development.md), [architecture](../../architecture.md), [user guide](../../user-guide.md)
and [release](../../release.md) describe current operation. The
[master plan](master-plan.md) is the current execution ledger;
dated slice reports are evidence. Earlier current-state and plan checkpoint
stacks are preserved in [the pre-R5 snapshot](../current-state-pre-r5.md)
and [historical master plan](../ecosystem-master-pre-r5.md).

Private investigation and public source histories intentionally have no common
ancestor. Reconciliation `bed726d` imported public runtime inputs without merging
histories. Preserve both and the owner's Workshop description/unrelated fields.
