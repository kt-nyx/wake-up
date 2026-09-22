# Retained loading features and proposed release scope — 10 September 2026

> Historical snapshot preserved by R5 on 10 September 2026. Decisions, identities and pending steps below describe earlier checkpoints. Use [current state](../current-state.md) and the [current master plan](ecosystem-replacement-20260910/master-plan.md).

**Current R4c source coverage:** use the
[current capability table](ecosystem-replacement-20260910/r4c-remaining-coverage.md#current-capability-table)
for the offline R1–R4c implementation. It adds R4a streaming XML input, R4c
ordinary new-colony background loading, native-summary hiding and partial
per-mod XML timings to the R2/R3 changes below. A revised parsed-language
candidate remained non-repeatable and was not integrated. All new behavior is
live-unqualified; the historical deployed-candidate table below remains evidence
for its own source only. No full ecosystem replacement or supplier removal is
claimed. R5 owns the broader historical wording consolidation.

**Later R3 experimental source checkpoint:** [R3](ecosystem-replacement-20260910/r3-deferred-assets.md) adds
default-off filesystem streamed-audio deferral with compiled consumer gates and
native completion/ownership. It has offline evidence only, with explicit
reflection/generated-code/worker-thread limits. This supersedes the complete
on-demand source omission for current development, not the unchanged deployed
candidate's scope or live evidence below. Progressive DDS remains absent.

**Later R2 source checkpoint:** [R2](ecosystem-replacement-20260910/r2-texture-preparation.md) implements broader
native batches and optional Windows full/half/quarter DDS exports. Those portable
outputs are deliberately separate from automatic game-texture consumption. This
supersedes the source-only helper/broad preparation omission for new development,
not the identity or live evidence of the retained candidate below. The existing
deployed candidate remains unchanged; R2 has offline evidence only.

## Parent steward acceptance

The steward accepts handoff `eee7a7d` and core `d584eb36aa68cd374a26621f97295dbb63626a33` for the retained scope and release dispositions below. The actual production diff, private controls, focused test receipt and captured final run were reviewed; an independent read-only reviewer found no world-lifetime correctness blocker. `rf-retained-06`, observer run `f2b1bea2d76e4124b5f5d5993a728578`, records automatic/world success and normal exit 0, native prepared consumption, display coexistence and two real world generations including minimized/current-preference behavior. Core DLL SHA-256 is `59ed42a69895930e8c5cfe04c903005f925cdf063beb02ffe6329244faac8825`; observer source is `9c5434d`.

This accepts explicit omissions under the owner's design/drop authority, including removal of public lossy texture presets after the alternatives examined below. It does not qualify arbitrary mod generators, new-colony or standalone map loading. Error/cancel tests substitute world content/rendering; accepted save evidence remains bounded. Functional timings establish no speed benefit. Steam qualification, reliable baseline/public-pre-revamp/new comparisons and final local packaging/release review remain required. All teams returned ownership before this review; the next serial task receives it on dispatch. No publication is authorized.

This candidate keeps useful loading improvements, extends background execution
to native world creation, and removes public lossy texture choices that lack a
qualified useful workflow. It is a narrower loading product, **not full ecosystem
replacement**. The following release dispositions use the owner's explicit
authority to redesign or drop capabilities after a fair evidence-based attempt;
they are proposed for steward review, not self-acceptance or publication.

## Scope and ownership

Dispatch `wake-up-retained-features-1d9feb6` began at exact clean
`1d9feb6cd4a16e6df87179c909b68e98f00823f0`, branch
`codex/retained-features-1d9feb6`, in the sole checkout. Fresh process inspection
found no RimWorld process. Three bounded subagents independently researched
coverage/consumers, implemented assigned world code/tests and retired assigned
lossy surfaces. Only the owning task builds, deploys and operates the fixture.
Prepatcher remains required; existing defaults and ordinary fallback remain.

## Every original goal and its release disposition

“Deferred” below means omitted from this proposed release, not proved impossible.
Historical failures reject only their tested route and conditions. A future
reopening needs a materially different premise addressing the recorded problem.

| Original goal | Retained delivery and evidence | Rejected or examined route | Deferred/dropped release capability, user impact and replacement limit |
|---|---|---|---|
| **1. Independent XML reuse (S04/S05)** | Existing definition/template searches and optional Gagarin assistance remain; native-flow/replay research retained. | [S04](ecosystem-replacement-20260910/s04-parsed-xml.md) complete warm reconstruction 58.94 ms versus 11.43 ms native, even steady-open 30.16 ms. [S05](ecosystem-replacement-20260910/s05-feasibility.md) found no useful fully admitted operation tree; custom operations can retain references/state outside XML. Partial intervals still need useful work and safe boundaries. Earlier worker/catalog/inheritance routes lack repeatable useful gains. | **Defer independent parsed caches, processed replay and inheritance reuse.** XML loads normally. Gagarin assistance still requires that optional supplier; this does not replace an independent XML-cache product. No broad purity analyzer or new cache-format project justified here. |
| **2. Translation loading (S03/S06)** | Retain default-off faster application. French native execution/sampled output passed; [overnight](ecosystem-replacement-20260910/overnight-qualification.md) repeated 1.57–1.64 s saving within the measured application stages. | [S06](ecosystem-replacement-20260910/s06-language-cache.md) grouped parsed records addressed per-file overhead differently but lost 5/8 pairs, 23.24 versus 21.23 ms complete warm median. Another format without a new cost premise is not a useful reopening. | **Retain application, defer persistent parsed-language reuse.** Files still parse each launch; no universal menu saving or elimination of language loading claimed. |
| **3. Faithful texture caching (S07/S09)** | Retain opt-in PNG/JPEG output reuse, 8 MiB entry/512 MiB category. [Windows pass](ecosystem-replacement-20260910/windows-functional-qualification.md) checked a real grayscale JPEG's rendered output across seven mips; established PNG behavior retained. | [S09](ecosystem-replacement-20260910/s09-first-build-images.md) expanded PNG input lost 8/8 offline pairs, 22.765 versus 15.642 ms, expanding 672,158 bytes to 8,400,288. Raw helper decoding lacks complete native-equivalent color/mip/compression and whole-cost evidence. [S07](ecosystem-replacement-20260910/s07-texture-coverage.md) found no justified per-file I/O/baking opportunity for storage regrouping/compression or completed-atlas reuse; archived layout-only work did not establish useful complete-startup benefit. | **Defer automatic first-build acceleration, PSD/other unsupported formats, larger entries, grouped/compressed cache storage and completed-atlas reuse.** Layout-only negative evidence does not reject every full-atlas technique. First construction may cost time; authored DDS stays ordinary. This is eligible reuse, not a general image-loader replacement. |
| **4. Cache ownership (S01)** | Retain integrity, interrupted-write recovery, bounded eviction and next-launch bypass/rebuild/clear. [Maintenance](ecosystem-replacement-20260910/s01-cache-maintenance.md) has focused quota/corruption evidence; all three prepared-category choices passed live in the overnight report. | No demonstrated gap calls for another storage framework. | **Retain implemented scope.** Only Wake-Up stores are maintained, including clearing while disabled; other mods' caches are unaffected. |
| **5. Optional on-demand assets (S11/S12)** | Retain default-off supplier routing, native winner/object identity and invalidation/fallback; [Windows routing evidence](ecosystem-replacement-20260910/windows-functional-qualification.md). | [Consumer study](ecosystem-replacement-20260910/s11-on-demand.md): native startup demands graphics/icons, direct fields bypass lookup hooks, atlases need pixels/dimensions. Previews already lazy; large audio already streams. Postponing whole callbacks changes object/atlas ownership; a DDS mip-tail lacks complete first-use readiness. | **Defer on-demand assets, Resources/bundle route memoization and progressive DDS detail.** Ordinary eager/native streaming remains. No placeholders, first-use scheduler or delayed-quality option; supplier routing is not on-demand loading and does not replace those competitor features. |
| **6. Texture preparation/quality (S10; conditional S12)** | Retain opt-in native preservation, preparation/resume and later-startup consumption; public workflow passed [overnight](ecosystem-replacement-20260910/overnight-qualification.md). | Working helper and 15 image cases do not establish a public consumer. BGPlanet has zero useful matches; broader UI-key check also had none. Real carpet terrain use is concrete, but safe consumer-local replacement would already pay eager decode and introduce separate texture/material ownership. One-JPEG native/prepared comparisons showed no menu benefit only for that case. | **Defer lossy recompression/downscaling; remove public presets and reject their runtime selectors.** No broad quality/memory-reduction control. Native preparation remains useful control, with no general speed promise. Helper source/evidence retained, binary omitted from proposed bundle; Linux helper remains owner-deferred. Progressive detail separately deferred under goal 5. |
| **7. Code searches (S02)** | Retain leaf-subclass proof during alert-interface construction plus established type/name searches. [Combined Windows run](ecosystem-replacement-20260910/windows-functional-qualification.md) recorded 128 avoided leaf scans; native ordering/lifetime checked offline. | Further name memoization had no justified work; archived late searches found no eligible calls. | **Retain implemented scope.** Selected repeated searches are avoided; no isolated new speed percentage or replacement of all reflection caches. |
| **8. Loading display (S08)** | Retain optional stage/time/cache panel and bounded diagnostics. `oq-display-08` visibly displayed native loading stages and exited normally. | Previews already load lazily; full per-mod profiling is a different undelivered product. | **Retain panel; defer full per-mod profiling and extra preview optimization.** Bounded diagnostics are not a detailed profiler. Loading Progress cooperation/refusal stays. |
| **9. Background loading (S13)** | Retain accepted ordinary save lifetime/current-preference restoration and minimized holds in [scene/save report](ecosystem-replacement-20260910/scene-save-qualification.md). Candidate adds independent native world-page admission; focused qualification below. | World remains in Entry, requiring no Play wait. Native new-game caller `PageUtility.InitGameStart` instead queues Play and uses `Game.InitNewGame`; existing direct observer map creation is different caller evidence. | **Retain saves and qualified native world creation; defer new-colony, standalone map and direct autostart admission.** No blanket all-loading or CPU speed claim. Private formatter workaround is not a shipped engine fix. |
| **Additional S14 warnings** | Retain exact two duplicate-product metadata conflicts, required Prepatcher and actionable Loading Progress refusal/status. [Catalog](ecosystem-replacement-20260910/s14-conflict-warnings.md) has focused reader/condition checks. | Inspected competing hooks generally cause feature-local refusal; similar features do not justify blanket conflicts. | **Retain catalog/status; decline a modal launch dialog without a verified rule needing it.** No automatic mod changes or universal compatibility promise. Deferred competitor capabilities remain distinct. Actual RimSort warning UI is not newly qualified. |

## Why lossy preparation is deferred

The Windows helper really converts images. Its actual carpet output was
699,064 bytes full-size and 174,776 bytes half-size; those are payload sizes,
not proven GPU-memory savings or public rendering quality. The native
preparation window, resume and startup consumer also really work. Neither fact
turns that carpet into a safely admitted lossy preparation role.

Independent research inspected the inherited carpet TerrainDef and native
`TerrainDef.PostLoad`/`Graphic_Single.Init` consumers. Custom shaders, explicit
mask paths and pollution overlays defeat folder/suffix-based classification.
Fresh definition-to-shader associations would be needed before eager loading.
A materially different consumer-local substitution would use current shader
information, but the original would already be decoded and retained; additional
material/texture ownership has no demonstrated net loading or memory benefit.
Standalone image export would require another installation/consumption workflow.
Authored DDS replacement and fixture-specific admission were not used.

The fixed native UI alternative had zero physical sources for 88 TexButton and
22 TexCommand keys as well as zero BGPlanet overrides. Evidence remains in
`artifacts/s10-role-correction/findings.md`, its decompiled `Verse.TerrainDef.cs`,
`artifacts/s11-on-demand-20260909/textures/Verse.Graphic_Single.cs`, and the
[real helper receipt](../../artifacts/overnight-qualification-20260909/helper-real-jpeg/receipt.json).
The two lossy UI choices/controller helper path are removed. Persisted old
preset values resolve to native preservation; explicit retired selectors are
refused, and startup cannot consume old lossy identities. Existing owned-cache
clear/eviction can reclaim old entries. Pure helper/store research contracts
remain for future work; no helper binary is required for native preservation.

## World lifetime and qualification

The added admission is the exact queue call inside
`Page_CreateWorldParams.CanDoNext`, after its native gate succeeds. The native
worker, page transition, redraw, error handling and queue ordering remain intact.
Its session does not wait for Play, while nested save requests preserve their
existing Play-arrival wait. Shared boundaries retain temporary background
permission through callbacks and queued tails, then restore the current
preference. World-only contract/patch refusal leaves the verified save path
available. Standalone map and new-game callers are deliberately not admitted.

The first live attempt correctly refused world admission because the added
all-hook guard treated Wake-Up's own type-search completion postfix on
`Root_Entry.Update` as foreign. The correction permits only three exact
same-module methods with their exact owners and postfix placement: type-search
cleanup, PNG/prepared-store cleanup and display cleanup. They cannot replace
the native root or queue loading work. Unknown owners/methods/placements still
refuse. No Prepatcher root-suppression hook is allowed. Independent review also
found that shared all-hook checking omitted Harmony inner-prefix/postfix records;
the final source includes those categories in refusal/release checks.

| Capture | Source / observer | Result |
|---|---|---|
| `rf-world-01` | `45c7221` / `45c7221` | Native page opened and actual fixture minimized, but own-postfix admission refusal left session inactive. World check failed; normal exit 0 and menu/exit field true do not make this a world pass. |
| `rf-world-admission-02` | `45c7221` / `8e594e0` | Diagnosed refusal. Private receipt generation hit Windows-invalid closure filename characters before arming automatic shutdown; exited via manual Quit to OS. Its automatic field records menu/exit only; observer sequencing failed. |
| `rf-world-admission-03` | `45c7221` / `9c5434d` | Corrected diagnostic filenames; matching native bodies and exact own type-lookup postfix identified. Normal automatic exit. No world execution claimed. |
| `rf-world-04` | `11a02df` / `9c5434d` | Two actual native 30% world-page generations passed: first minimized, then current preference changed during generation. Entry/page transition/redraw completed; false preference persisted through a 19.203 s minimized hold. Normal automatic exit, world result true. |
| `rf-native-ui-05` | `d584eb3` / `9c5434d` | Explicit manual UI run: settings and native-only window readable; 698 selected, one eligible JPEG prepared, 697 authored DDS skipped, zero failures; resume reused one entry. Old preset 2 normalized to native/default 0. Quit to OS, exit 0, captured native settings. |
| `rf-retained-06` | `d584eb3` / `9c5434d` | Final combined functional run: restored UI-created native entry consumed at startup, 1024×1024 / seven mips / DXT5; loading-display layout executed; all three own postfixes coexisted with admitted world loading. Two native page generations passed, including actual minimized completion and 39.009 s false-preference hold, then changed-current-preference restoration. Automatic and world results true; exit 0. |

All six captures are functional, not performance measurements. Runs 01–04 use
the activation lane; 05–06 use the same frozen two-supplier selection as the
earlier native-preparation workflow (VEF and Vanilla Ideology Expanded — Memes
and Structures). Representative is the CLI lane for that explicit selection,
not a claim of full-collection coverage. The attempted 05 preparation with
activation lane was refused before launch and corrected to representative;
no unrecorded game was launched. No forced process closure occurred.

Actual Computer Use actions targeted freshly verified fixture executables:
PID 8232 for run 04, PID 15596 for UI run 05 and PID 35440 for final run 06.
Alt+Enter exposed the title bar; minimize clicks were independently confirmed
by `IsIconic=true` and `Application.isFocused=false` receipts. Restores occurred
only after completion plus the minimum hold. Capturing a minimized window
reported its expected minimized-state error; it was not silently restored for
a screenshot. Run 01's first coordinate attempt failed with an unknown
screenshot ID; refreshed observation preceded the successful title-bar click.

Final run 06 completed its minimized world/page callbacks at frame 2380 with
session inactive and actual engine/preference false. Focus returned at frame
2381 after the 39.009 s hold. The second native run changed the stored preference
from false to true while active and finished at frame 2606 with engine/preference
true. No Play scene was required. Existing accepted save holds and failed
formatter/input routes are trusted rather than repeated without cause.

Errors and cancellation are qualified proportionally by focused native-queue
tests with substituted world content/rendering and error handlers: queued
cancellation, worker failure, deferred page/redraw failure, queued tails and
latest true/false preferences. These are not live injected world failures or
arbitrary mod-world-generation compatibility. The private formatter workaround
remains explicit-probe-only and ships in no core package.

## Final identities and checks

| Layer | Exact identity |
|---|---|
| Final core source/package | `d584eb36aa68cd374a26621f97295dbb63626a33` |
| Final deployed core DLL SHA-256 | `59ed42a69895930e8c5cfe04c903005f925cdf063beb02ffe6329244faac8825` |
| Core deployment transaction | `20260910-013323-1059ca92` |
| Private observer source/package | `9c5434d1cc3705415a81eb8decb031243381940f` |
| Observer DLL SHA-256 | `d825d39314a758289e290eecb59524d3fa7813f1604097466cad7568e3b01e8a` |
| Observer deployment transaction | `20260910-012734-0a676e47` |
| Frozen generation / manifest | `20260904-182829-e058641c` / `b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d` |
| Managed game contract | `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` (`gog-rev573` build reference; player reports 1.6.4871 rev574) |

The [run summary](../../artifacts/retained-features-20260910/run-summary.json)
records full per-run core/observer inventories, UUIDs, native receipts, source
capture identities, prepared-entry hashes and the diagnostic manual-exit caveat.
The created native entry is 1,398,780 bytes; its copied contents were verified
through the existing fixture restore rules. Original texture identity and
selection rules remain unchanged. Final fixture metadata audit passed, original
three exclusions remain, and deployed DLL hashes match their package receipts.

Final focused check: **110 managed passed**, one existing optional helper test
skipped, **46 Python checks passed**; Release builds had zero warnings/errors.
Evidence: `artifacts/fixture-tests/b1247bae0c794404874c8cefa9fcf278/features.trx`.
Earlier failures were one expected contract-hash collection checkpoint, one test
missing its required isolated save-data argument, and two attempted inner-hook
test installations hitting the pinned Harmony public API's null inner target.
The latter tests now exercise genuine serialized patch records and restoration,
not execution of inner hooks; production refusal is checked without inventing
an unsupported patch API. No optional helper binary was needed or rebuilt.

Independent read-only review checked lifecycle/consumer boundaries and the
scope table. Its concrete inner-record refusal and conditional texture-omission
findings were fixed; world qualification was left pending until the captures
above passed. This internal review does not accept the candidate for the steward.

## Release handoff limits

The steward must review this candidate. Isolated Steam-version qualification
and the owner's reliable absent-baseline / exact public pre-revamp 0.2.1 /
new-version comparisons remain mandatory and are not completed by earlier
stage measurements. They require matched content/settings, balanced repeats,
first-build/warm separation and menu/colony-entry endpoints. No Steam staging,
normal data changes, Deck/Linux helper work, security changes, push or
publication belong to this phase. Final packaging/release readiness follows
those reviews, not this proposed scope decision alone.

All team edits, builds, deployment and launches stop at the documentation
handoff. No fixture or helper process remains. Final deployment/profile and all
failures are retained; exclusive checkout ownership returns to steward task
`01a08711-7899-7ff3-a1b5-4a0b105ba661`. No additional slice is created and no
self-acceptance, Steam staging, normal game/data, other-application, Deck,
security or publication action occurred.
