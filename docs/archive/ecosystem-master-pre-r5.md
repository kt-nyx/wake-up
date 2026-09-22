# Wake-Up ecosystem replacement: master implementation plan

> Historical snapshot preserved by R5 on 10 September 2026. Decisions, identities and pending steps below describe earlier checkpoints. Use [current state](../current-state.md) and the [current master plan](ecosystem-replacement-20260910/master-plan.md).

**R4c parent acceptance — 10 September:** handoff `83b6487`, built source
`9601473`, is accepted for offline implementation. Parent inspection verified
the summary condition, bounded timing/exception behavior, corrected future-menu
completion, fixture-setting synchronization, package identity and cost receipts.
A separate bounded parent review found no new colony-lifecycle defect. Existing
160 managed and 50 Python passes are adequate; no extra build or live run was
needed. The language experiment remains excluded and missing capabilities stay
explicit. Proceed to **R5 combined offline review and package preparation**,
including consolidation of historical checkpoint wording. No deployment, fixture
preparation, game launch or publication is authorized.

**R4c offline handoff — 10 September:** ordinary new-colony background lifetime,
independent native-summary suppression, bounded per-mod XML report/attribution
and expanded display diagnostics are implemented. New choices default off;
existing defaults and optional suppliers remain. Revised no-source-decoding
parsed-language reuse lost half its pairs and was stopped before integration.
No new verified metadata conflict/launch warning. The
[R4c capability table](ecosystem-replacement-20260910/r4c-remaining-coverage.md#current-capability-table) reviews
all nine original goals, S14 and recovered BetterLoading ideas without claiming
full replacement. The bounded review's first-launch report-completion finding
is fixed; source/package/check identities and live gaps are in that report.
R4c resolved R4a's `streamingXml` allowlist/check item while adding its own
settings. The reset fixture and old deployed candidate remain unchanged.
Return clean ownership to the parent for review before R5; do not start R5 here.

**R4b parent disposition — 10 September:** handoff `c0916de`, source `68e7db8`,
is accepted as a documented rejected experiment, not XML capability delivery.
Parent review verified the complete-cost receipt, actual six-operation hit versus
the earlier exploratory 81-operation kernel, test-only compilation/registration
exclusion and the absence of product/script changes from the assigned base.
Even stage/cleanup cost exceeded ordinary processing, so removing setup charges
would not rescue the measured design. Do not repeat it unchanged or weaken node
identity/publication guarantees to make it appear faster. Independent XML reuse
remains missing. Continue **R4c remaining coverage**, then R5 combined offline
review; the reset fixture and no-live boundary remain in force.

**R4b offline disposition — 10 September:**
`wake-up-r4b-retained-xml-offline-6cafcdc` investigated a materially different,
small native mutation interval from clean `6cafcdc6ed56bf756a492380d4ec8e7d0031ffab`
on `codex/r4b-retained-xml-6cafcdc`. The fully integrated candidate preserved the
checked XML/node/source/state behavior but lost: 37.204 ms warm versus 13.194 ms
native. R4b is closed as a rejected experiment; **independent XML reuse remains
missing**. Code and tests are nonshipping under `experiments/XmlRegion`; product
wiring/settings were removed. See [R4b evidence and handoff](ecosystem-replacement-20260910/r4b-retained-xml.md).
No fixture changes or live runs occurred. R4c/R5 remain for the parent, and no new
R4b fixture setting exists. R4a's existing `streamingXml` allowlist gap remains.
Source/package `68e7db831e4e6ddc995bb934ffafbc44d71c5092` passed the guarded
core build after 152 managed tests (one optional skip) and 50 Python checks.
Both subagents stopped; the clean committed checkout returns to parent
`01a08711-7899-7ff3-a1b5-4a0b105ba661` after this documentation closeout.

**R4a parent acceptance — 10 September:** handoff `86ea094`, source `9249702`,
is accepted for experimental default-off streaming XML input and the evidenced
decision not to duplicate native type/delegate caches. Parent inspection checked
the actual constructor entry, scoped admission, UTF-8/parser behavior, native
fallback, Gagarin/background coexistence and offline receipts. Existing focused
checks and independent review are adequate. The memory/input-cost probe is not
a complete-startup benefit or release qualification. Proceed to **R4b retained-node
XML reuse**, then R4c and R5. No live/deployment/fixture-change permission.

One small tooling gap is deferred to R5: `scripts/fixture.py` does not yet include
the public `streamingXml` setting in its settings allowlist. Ordinary product
selection works; future fixture preparation would refuse that setting. Update
the allowlist and its focused test during combined offline integration, before
any separately authorized run preparation. This does not require a new live run
or block the independent XML implementation.

**R4a offline handoff — 10 September, parent review pending:** the
[reading/type report](ecosystem-replacement-20260910/r4a-ordered-reads-types.md) records implemented default-off
streaming XML input, retaining the native scheduler and all publication order.
The proposed native type memo was redundant with current native caches; no
duplicate option ships. Focused checks: 119 managed/50 Python passed, one optional
probe skipped; separate complete native constructor/scheduler checks and modest
CPU/input costs remain distinct from game startup qualification. The independent
code review's helper-hook guard finding is fixed. R4a did not implement R4b's
retained-node region or start later work. The parent reviews this handoff before
dispatching R4b; no deployment, preparation or live runs.
Implementation/package source is `92497021df5e8d235afc2d4fadb89e50119730aa`;
the report records the exact DLL and unchanged fixture identities. Both agents
are stopped and exclusive checkout/build ownership returns to the parent.

**R3 parent acceptance and R4 sequencing — 10 September:** handoff `693cea9`
and source `7511948` are accepted as an experimental, default-off audio
implementation with offline evidence. Parent inspection and a bounded read-only
lifecycle review found no further actionable defect. The documented reflective
and worker-thread bypass limits remain; acceptance is not a playback, first-use,
performance or release qualification. Progressive DDS and general texture
deferral remain absent. See [parent review](ecosystem-replacement-20260910/r3-deferred-assets.md#parent-acceptance--10-september).

Split the remaining R4 work into serial owning tasks to keep implementation and
review manageable and give the smaller native-loading opportunities an earlier
turn. This changes order, not coverage:

1. **R4a: ordered XML reading and native type-result reuse.** Address the newly
   identified BetterLoading/Hyperdrive loading ideas against the actual current
   game. Existing Harmony type indexing is separate. Establish what is already
   parallel/cached; do not revive the failed extra-worker experiment unchanged.
2. **R4b: bounded independent XML mutation reuse.** Retain R1's 81-operation
   apparel-region premise, original node identities and safe publication. Do not
   repeat the losing whole-document prefix or skip unknown effects.
3. **R4c: remaining cache and loading-experience coverage.** Assess materially
   different parsed-language/grouped-store opportunities; implement useful native
   summary/display/status and background-entry coverage; reconcile warnings and
   every outstanding original/audit capability with explicit dispositions.
4. **R5: combined offline review and package preparation.** Consolidate current
   documentation, review interactions/defaults and record the later live plan.

The private steward record tracks each subtask and the next dispatch. Only one
team owns edits/builds; no deployment, fixture preparation or live testing. The
campaign continues through R4a, R4b, R4c and R5 before the offline endpoint.

**R3 offline handoff — 10 September, accepted above:** dispatch
`wake-up-r3-deferred-offline-f10ea98`, clean base
`f10ea983d17bd5d40d5f1339f519761ce849410b`, branch
`codex/wake-up-r3-deferred-offline-f10ea98`. Experimental streamed-audio deferral
now connects actual source discovery, private path/subtree requests, full-holder
exposure, compiled song reads, bounded idle warming and native ownership/cleanup.
It remains off by default. Known unsupported consumers refuse; arbitrary dynamic
or reflected field reads and worker-thread audio accesses remain the explicit
consumer limitation retained under parent guidance
`wake-up-r3-consumer-boundary-guidance-1`. It is not guaranteed all-mod fallback
or release-qualified on-demand parity. [R3 report](ecosystem-replacement-20260910/r3-deferred-assets.md) records
the useful source family, checks, review corrections and exact package receipt.
DDS restoration remains missing after runtime streaming, private mip groups and
GPU-copy alternatives were examined. R2 budgets/export separation stay unchanged.
No deployment, preparation, game launch or R4 work; ownership returns to the
steward at the R3 closeout. Implementation `7511948` has one successful guarded
core package build. Final checks: 197 managed and 50 Python passed, one optional
helper check skipped. The R3 closeout records the immutable package separately
from the unchanged deployed candidate; native playback and loading benefit remain
unqualified.

**R2 parent acceptance — 10 September:** handoff `d1cc0bf`, implementation
`b6e4224`, passes the parent code/evidence review. Batch selection, shared storage,
successful-only automatic-entry retirement, helper protocol and export separation
are implemented and checked offline. A second bounded read-only review found no
actionable defect; existing focused checks are sufficient for this checkpoint.
DDS exports require a separately reviewed consumer and do not automatically
reduce in-game texture quality. Full converter replacement remains incomplete.
Advance to **R3 optional deferred assets**, retaining default-off behavior and
smooth first use. Native rendering, UI responsiveness and speed remain unqualified;
no deployment, fixture preparation or live run is authorized. The private steward
record identifies the sole active task. R4 retains the unmet XML-region follow-up;
R5 must consolidate historical checkpoint wording in current documentation.

**R2 execution — 10 September:** dispatch `wake-up-r2-textures-offline-5f41cc4`
started from clean `5f41cc4da3d20a0b63680b6439cccc4c225e95b0` on
`codex/r2-textures-5f41cc4`, sole checkout. Broad mod/path selection, incremental
scans, bounded format admission, native automatic-entry promotion and Windows
DDS quality exports are implemented. Both prepared representations share 512 MiB;
automatic storage stays 512 MiB. Exported DDS is reusable source-keyed output,
not automatic quality substitution into unknown shader/mask/atlas consumers.
[R2 report](ecosystem-replacement-20260910/r2-texture-preparation.md) records this partial replacement boundary,
109 managed/50 Python passes, 31 helper CPU cases and six actual DDS conversions,
the corrected independent review findings and later live qualification. No live
runs, preparation or deployment; original English fixture/candidate preserved.
Accepted by the parent above. R1 nonshipping XML disposition remains unchanged.

**R1 parent disposition — 10 September:** corrected handoff `27e3788` is accepted
as the routing fix plus nonshipping XML experiment, not useful independent XML
delivery. [Parent review](ecosystem-replacement-20260910/r1-independent-xml.md#parent-acceptance--10-september)
confirms product exclusion and focused checks, with the existing unrelated
temporary rename test failure explicitly unresolved. Advance to R2 broad texture
preparation. R4 must retain the different, bounded 81-operation apparel-region
premise and its node-identity/publication contract; no fixture/package allowlist
or replay of unknown effects. Do not repeat the losing whole-document prefix
approach. The timer continues through R2–R5; no deployment or live runs.

## Authorized post-audit implementation campaign — 10 September

**R1 corrected handoff, parent re-review pending:** correction
`wake-up-r1-correction-1-6d077de` follows clean `6d077de` on
`codex/r1-independent-xml-0df5a57`. Routing inner hooks/status remain fixed.
The parent rejected the XML prefix as an ordinary option: ~770 ms reuse versus
~22 ms patching, repeated refusal-validation cost and disabled-option bridges
do not deliver an optimization. XML code/tests are preserved as a **nonshipping
experiment**; normal product settings, initialization, maintenance and automatic
registration are removed, and no experiment types enter the runtime DLL.
**No useful independent XML cache is delivered.** Historical source/package
`8452a8e` and its state/semantic evidence remain in the [R1 report](ecosystem-replacement-20260910/r1-independent-xml.md).
One bounded source-only pass identifies a later CE apparel region (81 consecutive
top-level operations, 14 named definitions/two templates) for R4. The next concrete
decision is safe replay retaining original node identities with a small actual
subtree dependency set, not another full-document prefix or generic journal project.
No region implementation or benchmark was started. No deployment, preparation or
live run occurred; restored fixture and deployed candidate are unchanged. R1 team
stops and returns ownership for parent re-review; R2 may proceed independently
under the parent and has not been started by this task.

Correction source/package `19e27ca26141ae5e2e765c4c07354affc38af522` built once
offline with zero warnings/errors. Focused managed tests: 38 passed. Python:
49/50 passed, with the existing Steam-transition test twice failing a temporary
directory rename due to Windows access denial. This unresolved check and exact
package/deployed identities are recorded in the corrected R1 report; the suite
is not fully green. Only the two routing files differ in product source from
the R1 dispatch base.

The owner says: "go ahead with full implementation. don't do any live runs yet,
but do as much implementation of all of these potential improvements as possible."
This authorizes implementation and proportional offline tests/builds, supersedes
the research-only stop, and **withdraws earlier live-run permission for this
campaign**. Do not deploy, prepare a launch or alter the restored fixture profile.
Keep source/build acceptance separate from the unchanged deployed candidate and
all pending live correctness/performance work. Missing live evidence is a later
qualification item, not a reason to stop independent implementation now.

Serial work packages, using one owning orchestrator at a time:

* **R1: independent XML reuse**, with the routing inner-hook fix and honest
  selected/active/refused feedback for changed features. Pursue an integrated
  capture/restore/fallback product path. Refresh current Steam source contracts
  read-only and test operation semantics, attribution, invalidation and fallback
  offline. Existing performance evidence and offline cost probes guide choices;
  do not require a live timing run or mislabel offline costs as game speed.
* **R2: broad useful texture preparation**, format-aware bounded admission,
  practical batch selection, duplicate-store handling, and Windows native versus
  explicitly optional quality-changing preparation where useful and supportable.
  No Linux helper work. Public dependency/default/quality changes must be clearly
  documented; ordinary native fidelity remains available.
* **R3: optional deferred assets**, a complete consumer/ownership path for a
  useful family, safe first use, and progressive DDS restoration if its lifecycle
  can be supported. All deferred behavior remains off by default. Preserve smooth
  colony entry/first use rather than optimizing menu timing alone.
* **R4: remaining audit-led loading improvements**, revisit ordered parallel XML
  reads, type/reflection results, parsed languages/grouped stores, native summary
  suppression, display/status and background entry coverage where there is a
  concrete useful implementation premise. Split or combine this package based on
  R1–R3 results; do not repeat a failed design or substitute a broad harness project.
  Assign explicit implemented/deferred/rejected dispositions to every remaining
  original goal and newly identified BetterLoading capability.
* **R5: combined offline review/package preparation**, review feature interactions,
  defaults, cache ownership, dependencies, user guidance, warnings and focused
  regression results. Prepare a precise future live qualification plan. Stop and
  report the completed implementation, remaining gaps and unqualified behavior;
  no automatic deployment, live runs or publication.

The existing five-minute steward workflow resumes for corrections and serial
handoffs through this campaign, not only R1. Use the sole checkout directly;
read-only subagents may overlap, while one task owns all edits/builds. Require
real implementation rather than another feasibility-only conclusion wherever
the available contracts support it. A specific correctness barrier may defer
its affected capability after alternatives are assessed, but should not block
unrelated authorized packages or silently become "replacement complete".

The recommended sequence below remains the rationale; its requests for live
measurements now belong to later owner-authorized qualification. English remains
the primary future performance case, with French separately for translation.

## Next implementation sequence after the independent audit

The owner wants to pursue the larger missing improvements (10 September). The
next recommended slice is **independent reuse of completed XML work**, with the
small routing-guard correction included at its start. This supersedes the earlier
stop at an incremental candidate as the intended product direction, but does not
erase accepted negative results or declare any missing capability delivered.
No new implementation task or live run has been dispatched by this planning update.

1. **Independent XML reuse: first useful end-to-end implementation.** Start from
   reviewed `6c1631c` plus this planning record in the single checkout. Fix the
   routing inner-hook omission with focused existing-style coverage; do not grow
   that into a separate compatibility overhaul. Then establish the actual cost
   and effective behavior of source loading, combination, patch execution and
   reconstruction on the current Steam runtime. Prefer skipping completed work
   that costs materially more than validation/restoration; do not repeat the
   rejected per-file parsed-tree encoding. Choose a complete stage or bounded
   consecutive patch region only after its state and useful coverage are clear.
   Unknown custom operations must execute normally; their presence should not
   automatically rule out independent safe regions, but splitting around them
   requires proving the relevant input/state boundary. Do not assume unchanged
   assemblies make external effects safe to skip. Deliver an ordinary opt-in
   cold-build/warm-hit/fallback path, real source attribution and lifecycle
   behavior, and a measured useful hit on actual selected content. Planning and
   infrastructure alone do not count as delivery. If no useful boundary can be
   established, return the concrete blocker and alternatives before expanding
   the implementation or quietly substituting a smaller feature.
2. **Broad useful texture preparation.** Correct the compressed-output admission
   limitation, then expand naturally eligible batch coverage. Decide whether
   reusable native output or explicit external DDS/quality preparation serves
   the chosen sources; avoid two owned stores duplicating the same result.
   Windows remains first; native fidelity and optional quality/size tradeoffs
   must be separate, explicit user choices. Existing DDS-heavy one-image results
   are not a useful general texture decision workload.
3. **Optional deferred loading for one complete asset family.** Identify an actual
   expensive family whose access and ownership can be covered, then implement
   and measure the full first-use path. Include menu, colony-entry and first-use
   effects; on-demand stays off by default. Progressive DDS remains conditional
   on a workable restoration/ownership design, not automatically delivered here.

Use one orchestrator per substantial area, with serial checkout ownership and
bounded independent review. Keep each assignment aimed at a functioning product
path, with an early concrete cost/coverage decision rather than another open-ended
research cycle. Improve selected/active/refused feedback for the features being
changed; a general diagnostics redesign is not a prerequisite for XML work.

For future performance qualification, English is the primary user-facing
comparison; French remains a separate translation-specific case. Use a real
patch-heavy workload and retain the previous list for continuity where useful.
Establish the new XML feature's effect with supplier XML caching disabled or
absent, documenting all configuration differences; compare against the supplier
cache separately before claiming replacement. Keep an unchanged candidate
control, full startup costs, cold-build versus warm-hit results, and focused
semantic checks for changed inputs/order/settings and fallback. Additional live
operations follow the current owner authorization and fixture boundaries, not
this planning text alone. The existing timer remains paused until a task is
actually assigned. All nine original goals plus compatibility warnings remain
tracked; parsed languages, parallel XML reads, broader reflection/profiling and
other deferred capabilities are not removed by this prioritization.

**Independent audit reviewed — 10 September:** report handoff `416c537a3e74c0a91f281ae3a2217a3195d8ab9b` is accepted as a research-only assessment. The [detailed audit](ecosystem-replacement-20260910/independent-revamp-audit.md) accounts for all 23 comparators and nine goals plus compatibility warnings, independently refreshes source evidence and checks captured performance. Verdict: useful incremental product improvements, incomplete ecosystem replacement; most measured gain predates the revamp. Existing comparisons retain other optimizers and do not establish replacement parity. Parent source review confirms the routing guard omits inner prefix/postfix records (conditional correctness defect; no affected live combination demonstrated), conservative preparation admission, stale activation status and native mod-list wording gaps. Historical BetterLoading parallel XML reading and type-result reuse were omitted from the original comparison; parallel definition construction remains unhooked. These capabilities are explicitly unimplemented/unqualified alternatives, not approved ports. Prior prototype failures do not disprove entire techniques. No correction to the audit is required; implementation and restored English fixture are unchanged. Record these findings for the next product decision, not as repaired. Research scope is complete, ownership returns to the steward and the five-minute follow-up is paused. No implementation or publication follows automatically.

**All-options warm-performance accepted — 10 September:**
Dispatch `wake-up-all-options-performance-9e99f36` completed the owner-requested
all-selected/populated-cache comparison on the unchanged exact final DLL.
Four measured repeats each: absent **105.698 s**, old supported-options
**59.592 s**, new all-selected **57.052 s**; new is 46.02% shorter than absence
and 4.26% shorter than old. Every measured capture passed normal exit, with
matched supplier/terrain caches and actual hits. One native prepared texture
was consumed; populated PNG storage overlaps it and records no extra hit.
Loading Progress safely refuses Wake-Up display/XML assistance. Native image
inventory supplies no useful texture-heavy extension; no synthetic PNG workload
or preparation-specific break-even claim. All first-build/manual/conditioning
runs and feature limits are in the [report](ecosystem-replacement-20260910/all-options-performance.md).
Parent review of handoff `c887b71` independently verified raw fingerprints,
measured controls and arithmetic; no correction required. The requested
follow-up is complete and the five-minute check is paused. Private fixture restoration changed narrowly; 50 Python checks
passed, core/observer bytes unchanged. All teams and launches stopped, original
exclusions restored, no new launch prepared, ownership returned to steward.
No additional slice, publication or normal-game change. Original goal dispositions
and unresolved engine risk remain unchanged.

**Final steward closeout — 10 September:** handoff `9772af1` is accepted as the bounded Windows local candidate, including ordinary menu-driven entry and the exact reviewed package. [Overnight closeout](ecosystem-replacement-20260910/overnight-closeout.md) records the final numbers, all retained/deferred scope, package identity and unresolved shared engine risk. Original nine goals plus S14 retain explicit dispositions; this is not complete ecosystem replacement. All authorized local qualification/package work has reached its proportional endpoint, all teams stopped and the five-minute timer is paused. Stable public release readiness remains unconfirmed; no publication occurred. Current source acceptance supersedes the immutable package's pre-review pending text and older ledger checkpoints below.

**Final local-candidate handoff — 10 September, steward review pending:**
`oeq-native-ui-01`, `oeq-observer-ui-02` and exact rebuilt `oeq-final-ui-03`
passed ordinary screen-driven entry/load with native logging and required
Prepatcher; all exited normally. No product algorithm changed. Package source
`8ca0dd763a2ad530668b59d6c3488f28eac311f5`, DLL `6b7a50946c3fb2b2071e8896329bf3ad1f8595c0c4ac07c1e514569539710a2f`,
local 0.3.0-rc.1 ZIP `ab59fce2fcdc6eb4891cfb3b3e46e8b6e03c151885f7593b19e219b147db3c48` passed
independent contents/source/compiled-code inspection and 109 managed/49 Python
checks (one optional helper skip). Full details in the
[final-candidate report](ecosystem-replacement-20260910/ordinary-entry-final-candidate.md). The shared InputLegacy
error and earlier automated-probe engine stall are not declared repaired; formal
qualification/readiness remains false pending steward disposition. Accepted
comparisons/retained scope/omissions stand. All team operations stop and ownership
returns to the steward, with no RimWorld process alive. No publication or new
slice. This supersedes the pending-package checkpoint below.

**Ordinary-entry/local-candidate checkpoint — 10 September:** the serial task
from exact base `76e10a0` has ordinary UI new-colony/save/reload passes with normal
logging, both observer-absent and observer-only manual paths. Required Prepatcher
remains active; InputLegacy errors and the earlier automated-probe busy-loop risk
are not declared repaired. Product source remains `d584eb3`; private controls now
stage verified fixture-created saves for manual functional loads. Local 0.3.0-rc.1
metadata reflects the accepted retained scope and every omission. Final package
checks and steward review remain pending at this checkpoint; no self-acceptance
or publication. See [ordinary-entry/final-candidate report](ecosystem-replacement-20260910/ordinary-entry-final-candidate.md).
This supersedes older claims that no actual ordinary user path has been tested;
the older failures remain evidence. Ownership stays with this serial task until
its explicit final handoff.

**Steward execution ledger — 10 September:** Steam handoff `8db9454` is accepted for the exact fixture transition, bounded functional scope and required repeated startup comparison; [review limits](ecosystem-replacement-20260910/steam-qualification-comparisons.md#parent-steward-review) remain binding. The retained core is unchanged. Ordinary colony entry still stalls with and without Wake-Up under the automated probe. Insert one bounded ordinary-user-path qualification and final local-package assignment before readiness closeout: isolate that path from the probe/observer and investigate demonstrated bootstrap causes without repeating failed root/preload experiments. Preserve comparisons and all explicit goal omissions. Prepare packaging in parallel with independent diagnosis, but do not label workaround-assisted gameplay production-qualified. Continue the timer; no publication.

**Steam qualification/comparison handoff — 10 September, steward review pending:**
the [report](ecosystem-replacement-20260910/steam-qualification-comparisons.md) completes the assigned exact
Steam transition and repeated absent/public-0.2.1/retained-candidate comparisons.
Core `d584eb3` is unchanged. Four measured repeats per arm, after four excluded
warmups, yielded 153.600/68.258/68.634/67.164 s mean menu times for absent, public,
new defaults and new translation/routing opt-in. New defaults were 0.55% slower
than public, while opt-in was 1.60% faster; both substantially beat absence on
this workload. All 20 captures passed; independent review verified identities,
controls, arithmetic and bounded functional claims. Native world/preparation
passed; save/minimized checks require the private logging workaround. Ordinary
scene entry still stalls with and without Wake-Up, so production gameplay is
not qualified. Existing retained-goal dispositions remain intact. The sole
fixture retains a verified, unlaunched small functional handoff; original
exclusions are restored. All team operations stop and ownership returns to the
steward for review and final local packaging/readiness disposition, without
self-acceptance or publication. Prior pending-comparison statements below are
historical where superseded by this completed measurement checkpoint.

**Steward acceptance and next assignment — 10 September:** accept clean handoff `eee7a7d` and source `d584eb3` for the [retained scope, omissions and bounded qualification](retained-features-pre-campaign.md#parent-steward-acceptance). World creation joins supported save loading; native preparation remains and public lossy presets are removed. Independent code review found no world-lifetime blocker. All original goals plus S14 retain explicit dispositions. Next, assign isolated Steam qualification and reliable repeated baseline/pre-revamp/new comparisons in the sole fixture, followed by final local package/readiness review. Existing overnight and Steam permissions apply; five-minute supervision remains active. No universal speed, full replacement or publication claim follows from this acceptance. This is the current execution-ledger decision and supersedes older pending-review/omission-approval statements.

**Retained-feature handoff — 10 September, steward review pending:** source
`d584eb36aa68cd374a26621f97295dbb63626a33` and private observer `9c5434d` complete
the assigned manageable-feature pass. [All-nine-goal plus S14 scope table](ecosystem-replacement-20260910/retained-features.md)
explicitly proposes release delivery, retained partial capability and each
omission, with user impact, alternatives and replacement limits. Native world
creation now has independent Entry-only admission and real minimized live
evidence; ordinary save support remains. Native texture preparation remains,
while public lossy choices/helper execution are retired for this proposed release.
S04/S05/S06/S09, broader S07 extensions, S11 on-demand/resources routing/S12,
full per-mod profiling and new-colony/standalone map admission are explicitly
deferred as detailed in the table, using the owner's redesign/drop authority.
This supersedes historical pending-omission-approval statements; it is not full
ecosystem replacement or self-acceptance. Final run `rf-retained-06` passed
native prepared consumption, display coexistence and two world-page runs with
minimized/current-preference checks and normal exit. 110 managed/46 Python checks
passed, one optional helper check skipped. All team operations stop and checkout
ownership returns to the steward for candidate review, then mandatory isolated
Steam qualification and reliable three-version comparisons before final local
packaging/release-readiness review. No Steam staging or publication occurred.

**Steward update — 10 September, scene/save accepted:** handoff `0df0c5e` and private observer `c6df2b5` are reviewed; shipped core remains `79dde43`. Real minimized save loading now has bounded qualification, and the native formatter stall has an evidenced fixture-only workaround. See the [acceptance](ecosystem-replacement-20260910/scene-save-qualification.md#parent-steward-acceptance). Next, finish manageable useful feature extensions and make explicit release dispositions for the remaining goals under the owner's redesign/drop authority. The independently scoped world-generation lifetime is a concrete implementation candidate. Assess useful lossy preparation coverage fairly; do not convert a one-JPEG negative comparison into universal rejection. Previously failed XML/language/image routes require a materially different premise. After retained scope is reviewed, complete isolated Steam qualification and the required repeated three-version comparison, then final package/release-readiness review. No publication is authorized.

**Scene/save team handoff — 10 September, review pending:** private controls/observer through `c6df2b5` diagnose and avoid the frozen Unity empty-stack-trace formatter loop for gameplay probes. Core `79dde43` is unchanged. Ordinary fixture-save entry, new map and actual minimized paused/unpaused loads passed normal exit; the final run preserved ticks through 32.872/34.834-second holds and restored a preference changed during loading. All 45 Python checks and final metadata audit passed. See [report, exact identities, failed routes and next world-queue assignment](ecosystem-replacement-20260910/scene-save-qualification.md). Team writes/builds/launches stop; ownership returns to the steward. This does not self-accept S13, deliver world/map extensions or establish release readiness.

**Owner comparison requirement — 10 September:** following stable fixture work, the steward will assign isolated Steam-version qualification and reliable multi-repeat comparisons of the new candidate, public pre-revamp 0.2.1 source `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f` and Wake-Up absent. Verify the pre-revamp package/DLL and exact Steam binary identities; preserve one fixture and normal installations/data. Match content/order/settings, balance forward/reverse order, distinguish first-build/warm application caches and state Windows file-cache/warm-up policy. Report sample count, average, median, spread and percentage changes against both reference arms; separate menu, colony-entry and first-use endpoints. Failed runs remain evidence. No such comparisons were performed in the scene/save phase. Continue the existing five-minute steward timer; no new slice or publication is authorized by this handoff.

**Steward update — 10 September:** accepted the first overnight checkpoint at handoff `13205ce` for its bounded preparation, maintenance, visual and measured translation-stage evidence. Core remains `79dde43`; private fixture/observer controls are `4242709`. The scene-entry timeout remains a failed qualification, with cause unestablished. Next serial assignment is scene-entry diagnosis and real save/background/focus qualification before remaining capability disposition and final local candidate review. See the [review](ecosystem-replacement-20260910/overnight-qualification.md#parent-steward-review). Continue the heartbeat; do not pause for the superseded small-only or design-approval restrictions.

**Latest owner direction — overnight, 9 September 2026:** resume autonomous local-PC work toward a release-ready candidate. The owner authorizes all necessary local tests, including representative/full workloads and performance comparisons, iterative fixes, delegation and design changes. Capabilities may be dropped after a fair evidence-based attempt; explicitly record why, the alternatives tried, user impact and replacement-claim limits. This supersedes the earlier requirement to obtain a separate omission/design decision and the small-only workload restriction. It does not authorize publication, normal game/data changes, Deck work or changing security settings. Linux helper remains deferred. Fixture-only visual/focus tests are now within the testing scope; keep other applications untouched. Normal automatic exit, fresh process identity, separate source/package/deployment/run evidence and one owning team still apply.

Overnight sequence: (1) close practical functional gaps and measure useful local workloads; (2) review remaining original-goal gaps for credible, useful implementation or explicit evidence-based disposition; (3) qualify the final retained feature set and build/review the local release candidate with truthful claims. Reorder or combine these checkpoints when evidence improves the outcome. Do not repeat unchanged failed routes, spend the night expanding test infrastructure, or promise release readiness without supporting evidence. Continue useful independent work if a particular check is blocked. The five-minute steward loop is resumed; it reviews completed handoffs and creates fresh serial tasks as needed.

**Current execution — 10 September:** the first [overnight qualification checkpoint](ecosystem-replacement-20260910/overnight-qualification.md) is handed back for steward review from `codex/overnight-qualification-f9725ec`, assigned base `f9725ec`. Core `79dde43` remains unchanged; private fixture/observer correction is `4242709`. Native preparation/resume, startup consumption, maintenance and the visible loading panel passed. Matched French representative runs repeat a 1.57–1.64 second translation-stage saving; no universal menu gain is established. A one-JPEG prepared-store comparison showed no useful menu saving. Final save entry exhausted its full allowance before reload checks; false-background/focus and useful public lossy coverage remain gaps. All captures and forced failures are explicit in the report. The team and subagents stop writes/builds/launches at handoff and return exclusive ownership to the steward. The earlier [small Windows functional pass](ecosystem-replacement-20260910/windows-functional-qualification.md#parent-steward-acceptance) remains accepted within its scope; this new checkpoint is not self-accepted and does not establish release readiness or permanent omissions. The steward owns the next serial task and recurring workflow.

Created 9 September 2026. Planning baseline: Wake-Up `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f`.

## Purpose and decisions

Build one loading optimizer that combines Wake-Up's existing behavior-preserving improvements with the strongest useful ideas from the community. The aim is faster preparation of the same game and a smoother path into a colony. A menu appearing sooner is insufficient if the saved wait returns as a gameplay stall.

The original nine product changes are covered by **13 implementation slices**, plus owner-added **S14 compatibility warnings** for a total of **14 slices**. Each slice belongs to a separate orchestrator, which may delegate bounded work to sub-agents. A parent **steward** maintains this plan, reviews handoffs, resolves cross-slice decisions with the owner, and decides what is ready to advance. The larger XML and texture changes are split because they have different correctness boundaries and can deliver independently.

Owner decisions established in this conversation:

- Prepatcher is already a prerequisite. Preserve that dependency; there is no proposal to replace it.
- On-demand loading is optional and **off by default**. When enabled, prioritize smooth first use and colony entry over the earliest possible menu.
- Validate individual features as they are completed, then aim for **one combined release** after the accepted scope works. A finished slice does not authorize publishing it.
- Separate slice orchestrators report to the steward; this is not one orchestrator implementing the whole roadmap.
- S14 adds warnings for verified conflicts with loading behavior Wake-Up actually replaces, using mod metadata and conditional launch warnings where appropriate. Similar features alone do not establish incompatibility.

On 9 September 2026, the owner approved both S10 proposals and explicitly requested continuation: develop and locally package an optional DirectXTex CPU helper for Windows x64/Linux x64, with explicit native-preserving, full-size recompressed and smaller-texture choices; add a separate prepared-output category capped at 512 MiB, initially retaining the 8 MiB complete-entry limit. Combined category allowance may reach 1 GiB plus bounded one-item staging. Existing defaults stay unchanged and use of prepared textures defaults off. This authorizes scoped dependency/helper implementation and offline packaging checks, not deployment, game launches, Deck access, publication or automatic system dependency installation. Core Wake-Up retains ordinary loading without the helper.

On 9 September 2026, the owner deferred the Linux helper until later and directed current work to Windows. Keep portable source and ordinary Linux fallback; do not install Linux tooling or build/package/qualify the Linux helper in this pass. This is a temporary platform deferral, not a claim of Linux helper support or removal of existing core Linux behavior. The accepted S10 Windows/offline checkpoint supplies the routing contract for S11; broader useful lossy coverage remains unfinished and must stay visible at combined review.

This document authorizes no live launch, deployment, installation change or publication. Those remain governed by the owner's actual instructions and repository operation rules. The initial planning and documentation sweep changes no product code. Slice implementation requires a separate assignment and checkout ownership.

## Evidence and scope

Read the [opportunity report](<../../artifacts/ecosystem-review-20260909/report.md>) and [feature comparison/source ledger](<../../artifacts/ecosystem-review-20260909/comparison.md>) alongside this plan. The ledger accounts for all 23 inventory entries, with implementation evidence for 19; it also records exact downloaded revisions. Kingfisher was additionally checked. This does not mean every gameplay feature of every performance suite was audited.

Image Opt, No Modlist on Loading, Load in Background and RimPy remain source gaps. BetterLoading and RWMS have historical-source limitations. Their documented behavior can inspire independent work; their unseen/current implementations cannot be ranked. No competitor-versus-Wake-Up game benchmark establishes the estimates in this plan. Existing focused XML tests establish specific semantic differences, not universal superiority or failure rates.

“Replacement” here means coverage of useful **loading features**. It does not include replacing Prepatcher's public API, mod managers, gameplay tick optimizers, save-data deletion, mod sorting or every cosmetic preference in another loading screen. Keep the existing definition/type search, optional Gagarin assistance, Character Editor lookup, Giddy-Up texture and Loading Progress coalescing improvements unless new evidence justifies changing them. Co-loading competitor suites is not the review objective; ordinary safety guards must still avoid taking over an incompatible hook chain.

At slice start, inspect current [state](<../current-state.md>), [architecture](<../architecture.md>), [development rules](<../development.md>) and [recorded results](<results.md>). The source baseline above will become historical as slices land. Resolve contradictory status prose using current source, package receipts and captured results rather than assuming a document heading is current.

## Slice map and execution order

IDs are stable slice identifiers, not the original nine-item numbering or a requirement to execute strictly in numeric order. “Depends on” means a usable accepted interface is needed; independent read-only research can happen earlier. One checkout writer operates at a time.

| Slice | Deliverable | Original change | Depends on | Workload |
|---|---|---|---|---|
| S01 | Owned cache storage, recovery and maintenance | 4 | Existing PNG cache | Medium |
| S02 | Leaf-type search and justified repeated lookups | 7 | Existing type index | Medium |
| S03 | Faster translation application | 2 | Current native translation flow | Medium |
| S04 | Independent parsed XML reuse | 1 | S01 | Medium–large |
| S05 | Safe replay of completed XML processing | 1 | Early feasibility is read-only; implementation uses S01 and S04 native-flow findings | Large; explicit feasibility checkpoint |
| S06 | Persistent parsed language data | 2 | S01, S03 boundary | Medium–large |
| S07 | Broader faithful processed-texture caching | 3 | S01 | Large; bounded extensions |
| S08 | Independent lightweight loading UI and diagnostics | 8 | S01 status records | Medium |
| S09 | Faster first-build image processing | 3, 6 | S07 format/publication boundary | Large; prototype before backend expansion |
| S10 | Optional texture preparation and quality presets | 6 | S01, S07; reuse S09 where useful | Large |
| S11 | Asset routing and optional on-demand loading | 5 | S07, S08; S10 routing contract if present; S09 if sharing workers | Large; one initial asset family |
| S12 | Optional progressive DDS detail restoration | 5, 6 | S11, S10 quality policy | Medium–large; conditional |
| S13 | Background save/world/map loading | 9 | Current native focus/long-event flow | Medium; otherwise independent |
| S14 | Explain confirmed replacement conflicts in metadata and at launch | Additional owner requirement | Accepted capability coverage and actual coexistence findings; final rules after release scope settles | Medium |

The recommended sequence is **S01 → S05 feasibility checkpoint → S02 → S03 → S04 cost checkpoint → S06 → steward XML reassessment → S05 implementation, if justified → S07 → S08 → S09 → S10 Windows/offline checkpoint (Linux helper deferred) → S11 routing checkpoint → S11 optional on-demand checkpoint → S12, if justified → S13 → S14 → combined release review**. The early S05 checkpoint identifies the safe replay boundary and useful eligible work before the larger XML implementation commitment. It does not require S04 delivery, authorize a prototype or live run, or mark replay implemented. Record its decision in S05's ledger row; assign any later implementation ownership explicitly.

S02/S03 remain independent candidates if S01 cannot usefully advance. S08 or S13 can move earlier for a concrete product opportunity or to avoid an idle checkout. A blocked or rejected S05 approach must not hold unrelated work hostage. S14 conflict research can proceed read-only as earlier slices expose real conflicts, but final warning rules and metadata follow the accepted release scope. S11's routing checkpoint can be accepted independently; its on-demand capability remains unfinished until separately accepted. This keeps one slice identity while avoiding an all-or-nothing asset-loading handoff.

The steward may split work further when discovery reveals a separate deliverable or concrete unresolved risk, not merely to add process.

These sizes describe responsibility, not hours. Each orchestrator should finish a coherent feature and its focused checks. Backend experiments and conditional extensions have a bounded outcome: accepted implementation, rejected approach with evidence, or a precisely identified follow-up for the steward. An investigation being closed does not mean its feature was delivered.

## Steward and orchestrator contract

**The steward owns coherence.** Keep the execution ledger below current, select the next slice, identify its accepted dependencies and current base commit, resolve scope/default changes, and review the resulting behavior and evidence. The owner returns to this agent for further planning. The steward does not silently declare a missing replacement feature complete because an experiment ended.

**The slice orchestrator owns delivery.** Inspect its actual code and reference files, establish the smallest useful implementation, delegate independent source research or focused review, integrate the result, run appropriate checks and hand back a concise completion record. Suggested sub-agent responsibilities appear in each slice; use only those that help. The orchestrator remains accountable for source interpretation and the integrated behavior.

**One writer, one checkout.** Use only `Z:\Development\Large Projects\RimWorld Loading Optimizer`; no new checkout/worktree. Start implementation from current main on a `codex/` branch according to repository rules. The steward assigns checkout ownership before mutation. Other orchestrators may research read-only, but may not edit, build, deploy or launch against a checkout another slice owns. Within the owning team, delegate disjoint file edits explicitly and reserve shared startup/settings/package files for the orchestrator. Handoffs include branch/commit and dirty-state ownership; nobody resets another team's work. Use the required guarded commit command and do not push without authorization.

**Keep handoffs small.** Record: source commit; delivered behavior/default; changed interfaces; tests and exact run/package identities where applicable; rejected approach and remaining limitation; release impact; next dependency now available. Put raw research and run evidence in ignored `artifacts/`. Update relevant current product docs when behavior changes. Do not create a separate process-document hierarchy or require recursive reviewer-of-reviewer loops.

**Shared design rules.** Continue feature-local initialization and failure isolation through `StartupFeatureRunner`, normal `WakeUpSettings`/`UserStartupSelection`, and explicit development selectors. Validate only the required game/supplier method semantics and hook assumptions. An unknown method body disables the affected optimization and preserves ordinary loading. Do not restore old whole-game-version admission gates; cache invalidation can still include actual game identity. Prepatcher may provide an earlier hook when a demonstrated boundary requires it, not as a reason to rewrite all existing patches.

**Proposed release defaults.** Preserve existing user settings during development. S02/S03 may become normal enabled optimizations after qualification; persistent cache features should remain individually selectable and disclose construction cost, with final defaults selected from measured cold/warm behavior. New quality changes, preparation jobs, on-demand loading and progressive detail are opt-in; no automatic conversion job on install. S13 has its own setting and must respect ordinary pause/background preferences. Shared cache maintenance must not itself enable a cache the user disabled. The steward confirms new default changes in the final release review.

## S01 — Own, validate and maintain cached data

**Result:** cached data stays useful after mod updates, stays within its budget, and can be bypassed or removed safely. This supplies storage primitives for later slices while improving the existing PNG store immediately.

**Approach.** Begin at [PngCache.cs](<../../src/WakeUp/Textures/PngCache.cs>), its callers and `PngProcessingTests`. Add a small shared store layer under a proposed `Caching` area; retain format-specific readers and validation. A cache manifest is a record of what created the data and which files belong to it. It may be embedded in one validated immutable container; a separate sidecar file is not required. Expose input identity, format version, expected payload size/digest, publication state and rejection reason. Avoid a universal caching framework before two real consumers need an abstraction.

Use a Wake-Up-owned root and versioned categories. Write a temporary payload and manifest, verify them, then publish a completed generation through a final atomic filesystem operation supported on the destination. Readers select one complete generation and validate the bytes they actually consume; they must not validate one file then reopen an unverified replacement. Do not promise atomic replacement of two unrelated files. Interrupted writes remain unreferenced and removable. A failed rebuild must not damage the last valid generation, although changed inputs still forbid using that old generation.

Keep content hashes as freshness evidence. File metadata may narrow discovery, but timestamps and sizes are not proof that content is unchanged. Add bounded pruning of obsolete owned entries and least-recently-used unreferenced data at a safe point. Separate disk quota from in-flight memory. Start by preserving the existing 8 MiB entry/512 MiB PNG store behavior; S07 decides larger-entry policy with evidence. Maintenance must not invalidate active readers or delete authored DDS/source files.

Use one explicit concurrency rule: a designated store owner serializes quota reservation, publication and pruning. Workers may prepare private bytes, with unique temporary identities, but do not independently mutate shared counters/files or race a fixed `.pending` filename. Readers consume immutable published data. Later slices use this boundary rather than each adding a lock/publishing scheme; no general worker framework is needed in S01.

Latch “bypass caches this launch” once before consumers initialize. Make bypass, rebuild and clear distinct commands with explicit next-launch behavior. Add compact size/hit/miss/rebuild-reason records; S08 provides richer presentation. Migrate existing PNG data through an adapter or deliberate one-time format rebuild, not a silently changed key.

**Evidence and finish.** Extend existing PNG tests for partial writes, corrupted/truncated payloads, unchanged metadata with changed contents, quota/pruning, active reader ownership and bypass across two consumers. Use lightweight test consumers for the shared bypass contract; do not add a second production cache just to prove the abstraction. Verify existing PNG hit fidelity remains unchanged. Complete when normal caching plus update/recovery behavior works; do not build a cache benchmark service. A maintained cache is useful even if its maintenance is not itself a startup speedup.

**Inspiration / improve upon:** [DefLoadCache storage][cache-storage] and [bypass path][cache-hook]; preserve Wake-Up's content and payload checks. Avoid cache-file-exists admission and stage-by-stage consumption of a one-shot bypass. Suggested agents: current PNG lifecycle review; storage failure tests.

## S02 — Eliminate repeated code searches

**Result:** find final descendants of a code type without repeatedly searching the whole type list. Add other repeated-result caching only where it adds value above Wake-Up's existing index.

**Approach.** Start at [TypeLookupRuntime.cs](<../../src/WakeUp/TypeSearch/TypeLookupRuntime.cs>) and `TypeLookupRuntimeTests`. Review the current native `GenTypes.AllLeafSubclasses` implementation and call volume. A leaf type is a subclass with no further subclasses. Adopt FGL's “collect descendants, eliminate ancestors” algorithm, then filter the original ordered sequence to preserve native order. Return results with ordinary mutation/enumeration behavior rather than exposing a cached mutable set.

Associate caches with the current loaded-type universe. Handle newly loaded assemblies, dynamic types and failed enumeration; retain normal fallback where a stable view cannot be established. For repeated successful name queries, layer a bounded per-stage memo above the existing resolver only after counting actual repetition. Preserve assembly precedence and distinguish resolver APIs; do not cache missing/ambiguous names across launches.

Reflection means discovering fields or methods at runtime. Where S03 proves a repeated translation field/path lookup, let S03 own that local cache. This slice may supply a small reusable primitive, but should not globally patch reflection or revive the rejected field-store/late-type routes without a changed premise.

**Evidence and finish.** Test ordered results, multilevel inheritance, new assemblies, dynamic/partial enumeration, repeated enumeration and fallback. Use a small call-count sample and the existing startup timing path; stop adding memo layers when lookups are already cheap or absent. Leaf discovery is a standalone delivered feature; broader reflection caching is conditional.

**Inspiration / improve upon:** [FGL leaf discovery][leaf], [YaOpt type cache][type-cache], [Performance Fish reflection][reflection]. Keep Wake-Up's lookup precedence, invalidation and feature-local guards. Suggested agents: native semantic comparison; focused algorithm tests/review.

**Implementation handoff — 9 September:** [S02 source and evidence](ecosystem-replacement-20260910/s02-code-searches.md), original source `0d92037` from accepted `e17a50f`, corrected at `436c652` from steward review `735aa17`, is ready for re-review. Native mutable child caches and parallel-query ordering require a conservative adaptation: the ancestry proof skips terminal-child cache-miss scans and publishes native-equivalent empty lists; nonleaf misses retain native population. The corrected constructor-only lifetime reaches `AlertsReadout` during colony entry after the menu, without extending name searches. All 21 focused search/lifecycle checks passed; original algorithm evidence remains 155 managed passes, one optional skip and 35 Python passes. Extra successful-name memoization was not justified. No live leaf-call sample, deployment or loading-speed claim exists; checkout/build ownership is released.

## S03 — Apply translations with less repeated work

**Result:** translation-heavy lists load faster even when no disk cache exists.

**Approach.** Add a proposed `Translation` feature using the current `DefInjectionPackage.InjectIntoDefs`/`SetDefFieldAtPath` flow after verifying actual bodies and call sites. Start with duplicate detection: maintain a per-package/pass map from the game's normalized target path to the successfully applied injection. An injection assigns translated text to a game definition field. Only record success; preserve the first successful winner, ordinary duplicate warnings, renamed/backward-compatible keys and retry after a failed application.


Retain native assignment first. Keep before/after implied-definition passes distinct; implied definitions are definitions generated by code during loading. Native code skips successful previous injections but retries failures, clears pass diagnostics, and clears affected definition caches afterward. Rebuild or reconcile the successful-target map at the actual pass boundary rather than retaining normalized paths whose underlying definition/list structure may have changed. Handle full-list replacement and list element paths as the game does. Build scoped state with guaranteed cleanup; do not copy YaOpt's global current-mapping assumption without verifying reentrancy and lifecycle. Match verified operations when patching; do not assume compiler local-variable numbers remain fixed.

Only then consider repeated definition/field/path lookup tables if those remain material. `LoadedLanguage.LoadFromFile_DefInject` also repeatedly finds the package for a definition type; a per-language package table is a concrete small candidate. Cache immutable lookup descriptions or safe accessors with declaring type, path, flags and expected value shape. Preserve case-insensitive field lookup, `LoadAliasAttribute`, named list handles, translation restrictions and nested value-type writeback in the native setter. Fresh mutable results and diagnostic context remain per application. Avoid speculative global reflection caches.

**Evidence and finish.** Compare native versus candidate outcomes and diagnostics for duplicate aliases, failed-first/successful-later records, missing definitions, nested/list fields, full lists and both application passes. Check a translation-heavy sample for removed scans and stage cost; include ordinary English/fallback behavior. Finish with the duplicate map if further lookup changes add no demonstrated value.

**Inspiration / improve upon:** [YaOpt successful-injection map][translation-map]; [FastLoader fast applier][translation-applier] is a reference, not a currently wired fast path in that snapshot. S06 owns persistence. Suggested agents: native pipeline/semantic review; translation fixtures and diagnostic comparison.

## S04 — Reuse parsed XML independently

**Result:** Wake-Up reuses unchanged definition input files without requiring Gagarin and still executes ordinary mod patches and inheritance processing.

**Cost checkpoint returned, accepted for implementation and offline checks (9 September 2026).** [S04 findings](ecosystem-replacement-20260910/s04-parsed-xml.md) preserve native input/ownership evidence but stop the tested per-file encoding: complete warm reuse was slower than native parsing, including when store startup/completion were excluded. Production source and settings are unchanged. Independent parsed reuse remains undelivered; this does not dispose of the overall goal or authorize S05 implementation.

**Approach.** Start from [GagarinCacheRuntime.cs](<../../src/WakeUp/Gagarin/GagarinCacheRuntime.cs>) for ownership lessons, while adding an independent `XmlCache` feature. Trace current `LoadableXmlAsset`, `LoadedModManager.LoadModXML` and document combination before choosing the hook. Cache a representation of parsed input that can reconstruct fresh mutable XML trees cheaply; merely storing the original XML string and parsing it again does not deliver parse reuse.

Key per effective source content and parsing behavior. Preserve source/package attribution, actual selected load folders, node order, comments/whitespace where observable, document ownership and native error behavior. A virtual/archive source must either expose stable bytes and identity or use ordinary loading. Never infer a file's role from a package allowlist. Content and parser identity establish a hit; effective order and selection still govern combination.

Stage and validate a complete entry before giving it to the native loader. A bad entry falls back to parsing that source before global state changes. Retain the existing mutation-aware XML lookup optimization for subsequent patches. Ensure one parsed document cannot be borrowed into two owners and then mutated by both. If optional Gagarin assistance remains, assign one owner for a given reused parse; no duplicate interception of the same source.

**Evidence and finish.** Compare native and restored document structure plus attribution; exercise changed content with preserved timestamps, duplicates, comments, attribute-versus-element identities, malformed inputs, load-folder changes, missing files and independent mutable copies. Measure source validation plus reconstruction against ordinary parsing, including cache construction. Stop if the encoding is effectively reparsing at greater cost. S04 can land alone and must not be described as a full patch-processing replacement.

**Inspiration / improve upon:** [Gagarin input-content checks][gagarin-input], [FastLoader source metadata][xml-runtime], [DefLoadCache lifecycle][cache-hook]. Suggested agents: native XML hook/ownership research; serialization/equivalence implementation or review.

## S05 — Reuse completed XML patch work safely

**Result:** on eligible unchanged modlists, skip expensive already-completed XML processing while preserving the resulting game and required callbacks. This is the major warm-loading replacement feature.

**Feasibility checkpoint, scheduled immediately after S01.** Perform read-only boundary and existing-cost research first, before S04 implementation. No new replay implementation or live run is authorized by this checkpoint. First identify an exact boundary and useful real coverage. Arbitrary C# patch operations can read settings, assemblies, files or randomness, and can have effects beyond XML. Identical XML inputs do not authorize skipping them. Document which operations are safe to replay and what proves their dependencies; start with reviewed native operations. Unknown operations keep ordinary execution. If no useful safe boundary exists, report that coverage limitation to the steward rather than relaxing it or inventing a large automatic purity analyzer.

**Approach.** Build on S01 and S04's native-flow/ownership findings. Successful parsed-cache delivery is not required: if S04 reconstruction proves slower than native parsing, processed replay may still avoid useful patch work and can proceed independently. Begin with a complete patched-document generation before inheritance if that is the narrowest valid boundary. Fingerprint every contributing definition input as well as patch inputs, effective order, active folders, relevant configuration, participating code identity and game/pipeline identity. Preserve per-node original source attribution, translation-key processing, MayRequire behavior, diagnostics and patch completion timing. Mod IDs or a hash of file metadata are insufficient.

For the initial implementation, an unknown operation can disable processed replay for the entire load while leaving parsed-input reuse and live XML searches active. More granular checkpoints require proof that all inputs feeding each checkpoint are represented. Do not copy DefLoadCache's checkpoint pattern that includes later-mod definitions while validating only a patch prefix. A future explicit replay contract is a bounded extension, not a dependency for shipping safe initial coverage.

Validate and construct the complete candidate privately **before** skipping native input loading. Commit document, attribution and associated state together at the verified boundary. If commit cannot be made without partial mutation, redesign the boundary; catching an exception after skipping inputs is not ordinary fallback. Preserve callbacks through their actual lifecycle, not an early or duplicated invocation.

Resolved inheritance caching is a second checkpoint within this slice only if patched replay is already working and merge cost is material. Persist final semantics and provenance, not merely a visually similar XML document. If inheritance restoration requires a separate global-state design, return a proposed S05 follow-up to the steward instead of expanding this assignment indefinitely.

**Evidence and finish.** Compare candidate/native final definition meaning, source diagnostics and observable callback order on a small controlled list. Mutate settings, code, XML, active folders and order independently. Test corrupt/missing payload, later-mod definition changes and unknown operations. Warm timing includes validation/deserialization; cold and changed-mod loads remain reported. Measure whole startup separately and do not add XML-search savings to work the cache skips. Acceptance requires useful eligible coverage and safe refusal outside it; disclose remaining coverage rather than claiming every modlist is cached.

**Inspiration / improve upon:** [DefLoadCache hook][cache-hook], [FastLoader resolved XML and origins][xml-runtime], [Gagarin input checks][gagarin-input]. Suggested agents: replay dependency/callback review; generation serialization; focused native/candidate semantic comparison.

## S06 — Keep parsed language data between launches

**Result:** unchanged translations need less parsing on warm launches, complementing S03's faster application.

**Approach.** Cache keyed translation records, language string files and definition-injection records at the parsed-but-not-applied boundary. Use S01 storage and S03's confirmed lifecycle. A compact string table can store repeated text once, but begin with a simple format whose read cost beats native parsing. Reconstruct fresh mutable records with original filenames, paths, flags, list metadata and diagnostics.

Include actual selected language-file contents, effective mod order/folders, language and fallback language, parser/game identity and relevant settings. Handle missing/additional files and language changes in the same session. Native `LoadedLanguage.LoadData` already reads XML contents in parallel and processes ordered results; preserve its mod/folder order and duplicate-file registration instead of adding another generic reader pool. Preserve legacy `CodeLinked`/`DefLinked` folder handling and the separate deferred language-icon callback. Do not serialize injection records after their success/normalization fields have been mutated by application: `injected`, `replacedString`, `replacedList` and runtime-derived normalized paths are not parsed-input data. Do not persist translated definition objects as a shortcut.

Validate a package before bypassing its native parse. Unsupported source/provider behavior falls back locally. `LanguageDatabase.SelectLanguage` clears and reloads play data, so release previous language/definition references at that lifecycle boundary. Reuse file discovery with S04 only if both actually share semantics; do not force XML and language files through one synthetic ordering model.

**Evidence and finish.** Native/cached records produce the same translations and warnings, including fallback languages, list injections, duplicate aliases, missing files and both application passes. A same-size language edit with the old timestamp must invalidate. Measure first build, repeated launch and language switch; distinguish parsing saved by S06 from application saved by S03. Finish when ordinary selected-language loading works with safe misses and independent settings.

**Inspiration / improve upon:** [FastLoader language format/key][language-cache] and [injection string-table format][injection-cache]. Improve content freshness rather than relying on after-menu Workshop update notices. Suggested agents: payload/metadata design; language-switch and fallback validation.

## S07 — Expand faithful processed-texture caching

**Result:** more textures can reuse their already-processed output without changing image quality or the texture properties expected by mods.

**Approach.** Extend [PngRuntime.cs](<../../src/WakeUp/Textures/PngRuntime.cs>), `PngCache`, `TexturePlatformSupport` and existing tests. Preserve exact compressed blocks or uncompressed pixels, full mip chain, dimensions, format, filtering, wrapping, anisotropy, mip bias and readability. Mip levels are smaller versions of an image used at distance. Every hit produces an appropriately owned texture; a cache entry is not permission to share a mutable Unity object.

First quantify actual misses: entry/store limit, unsupported layout, source type, platform or supplier path. Extend the largest useful eligible class, rather than adding formats for checklist coverage. Larger entries need checked layout arithmetic and memory limits before increasing the 8 MiB cap. Other image formats can reuse the native processing path where captured output is faithful. Preserve native authored-DDS selection; a DDS that already contains GPU-ready data may gain nothing from recaching.

Retain current Windows/D3D11 and Linux/OpenGL paths and original-method fallback. Cache keys include actual source contents, pipeline and platform/settings identity. Keep the existing Loading Progress iterator bridge's ownership/order when that path is active. Use version-matched Unity APIs after confirming the target engine version; raw upload requires the correct format, dimensions and mip layout [Unity API][unity-raw].

Two extensions are conditional: grouped/compressed payloads only when per-file I/O is material, and a completed-atlas cache only when actual baking is material. An atlas combines images into one larger texture. Full atlas reuse needs ordered source-content/mask keys, complete color/mask payloads and rectangle mappings staged before publication. Neither an old failed DDS pack nor failed layout-only caching is reopened without this different measured premise. Return an independent follow-up if atlas restoration becomes another substantial system.

**Evidence and finish.** Extend existing texture fidelity checks for each admitted format/size/platform, changed pixels with unchanged dimensions, corruption and memory bounds. Measure capture cost, warm restore, store growth and startup. Do not broaden after useful coverage is delivered without a specific remaining bottleneck. Preparation that intentionally changes pixels belongs to S10.

**Inspiration / improve upon:** [FastLoader raw storage][raw-storage] and [atlas restoration][atlas]. Avoid its one-mip readback/recompression capture and incomplete state keys. Suggested agents: format/layout and platform review; focused capture/restore validation.

## S08 — Show useful progress without slowing loading

**Result:** Wake-Up provides its own inexpensive stage, elapsed-time and cache-status display, with optional deeper diagnostics.

**Approach.** Build on `RepaintCoalescer`, S01 status records and verified current long-event drawing. Observe native stage boundaries without reimplementing the entire loading closure or changing constructor/callback order. Update logical counters cheaply; format text and redraw on an elapsed-time budget. Show the current stage and cache reuse/rebuild reason. Omit a fabricated percentage or ETA when the work remaining is unknown.

Keep attribution/profiling opt-in. Nested or concurrent per-mod durations are not additive elapsed time. Display disabled/refused optimizations plainly without exposing internal implementation details in the ordinary flow. Offer a cheap or hidden mod-list display. Inspect current preview-loading behavior before adding preview deferral: an old RimWorld 1.0 patch is not evidence that the current game eagerly loads the same pictures.

If previews still load unnecessarily, defer only to the browser's real first need and release only owned resources. Avoid global `UnloadUnusedAssets` on every browser close. Do not recreate Loading Progress's manual static-constructor loop plus a second original call merely to preserve third-party hooks. Where a conflicting UI replaces the same flow, use an explicit guard/ownership choice rather than drawing two screens; a broad competitor compatibility project is out of scope.

**Evidence and finish.** Compare callback/constructor execution and failure cleanup, verify display state on cache hit/miss/error, and show that the UI does not introduce frame-per-item waits. Use the independent menu observer for performance, not Wake-Up's own progress counter. Preview changes are accepted only if current unnecessary work exists. Finish with a readable independent UI and bounded overhead; do not build a general profiler.

**Inspiration / improve upon:** [Loading Progress constructor flow][progress], [Startup Impact timing][impact], [historical preview deferral][previews], [No Modlist documented behavior][no-modlist]. Suggested agents: native observer/drawing hooks; UI overhead and lifecycle review.

**Implementation handoff — 9 September:** [S08 source, behavior and evidence](ecosystem-replacement-20260910/s08-loading-display.md) is accepted for implementation and offline checks at `07d024c`, from assigned base `59f92b3`. The independent default-off panel observes native messages and cache counters, preserves drawing/callback/constructor flow, and offers optional default-off bounded diagnostics. Current previews are already lazy; no preview patch was justified. Thirty-six focused managed and 35 Python cases passed; the source-specific package built without warnings/errors. No deployment or live qualification occurred. Task and read-only reviewer have stopped and released checkout/build ownership to the steward.

## S09 — Reduce first-build image processing cost

**Result:** reduce work on the first launch or after images change, where a warm texture cache cannot help.

**Approach.** Begin with one measured image class and the S07 output/publication contract. Separate file reading, CPU decode/processing and Unity object creation/upload. Prototype bounded independent CPU work; keep Unity-facing operations on their verified permitted thread. Do not simply increase generic workers or read/discard source bytes. Preserve orientation, alpha, color handling, mips and ordinary output; any quality-changing encoder belongs in S10.

Bound decoded bytes in flight as well as job count; a few huge images can dominate memory. Account for temporary buffers and nested library threads. Define cancellation, per-item failure, restart after idle and a definite completion boundary. Failed preparation must leave the original source available for normal loading. Reuse one worker facility with S10/S11 if those need it, rather than stacking pools.

Start with portable CPU preparation. A native Windows/D3D11 texture backend is a separate conditional extension requiring actual source/device/object-lifetime review and an advantage over the portable path. Image Opt's Workshop distribution reportedly includes C# and Rust source; the public community report is a discovery lead, not verified implementation evidence. Seek a legitimately available read-only package or public source before making claims about that code. Source unavailability does not block the independent CPU prototype.

**Evidence and finish.** Compare original and candidate outputs, total first-build time, warm behavior, peak memory and idle/failure recovery. Include processing and upload, not decode timing alone. Stop the backend route if output equivalence fails or overhead erases repeatable benefit. Deliver a supported path or a bounded negative result; the latter leaves first-build acceleration as an explicit roadmap gap.

**Inspiration / improve upon:** [todds bounded CPU pipeline][todds-pipeline], [Image Opt documented scope][image-opt], [secondary source-discovery lead][image-opt-lead]. todds limits job tokens and avoids nested OpenCV threading; Wake-Up also needs a decoded-byte limit. Suggested agents: CPU library/platform feasibility; worker lifetime/memory review; output comparison.

**Checkpoint handoff — 9 September:** [S09 lossless PNG preparation evidence](ecosystem-replacement-20260910/s09-first-build-images.md) is accepted as a negative cost checkpoint from assigned base `1ef6d8f`. The bounded Python IDAT expansion prototype preserved sampled filtered rows, metadata and Pillow pixels, but lost all eight paired comparisons: median 22.765 ms versus 15.642 ms including preparation and image consumption. Prepared inputs grew 12.50 times. This route stopped before runtime integration; no setting, helper or new package was added. First-build acceleration remains undelivered, and this result does not qualify .NET/Unity costs or reject CPU preparation generally. Task and read-only reviewer release checkout/build ownership to the steward; S10 subsequently completed decision preparation only, with implementation unapproved.

## S10 — Offer optional texture preparation and quality choices

**Result:** users can explicitly prepare textures ahead of a normal launch and choose a quality/disk tradeoff without changing mod originals.

**Approach.** Provide an explicit preparation action with selected active-content discovery, conservative presets, expected disk/work estimate, progress and cancellation. “Prepared” output is generated material owned by Wake-Up, not a converted Workshop file. Store it in S01 with a manifest tied to original contents, selected override, conversion options and encoder version. Publish completed items atomically and route only fresh matching output. Missing helper/output reverts to the original.

Use a source-preserving preset first; visually equivalent recompression is still a distinct quality-changing mode if bytes/behavior differ from native output. Offer downscaling or alternate compression only through clearly selected settings. Preserve transparency, masks, color space, orientation and mip policy. Respect existing authored DDS preference and don't delete paired DDS files merely because a PNG exists.

Evaluate a maintained encoder suitable for the supported platforms rather than assuming archived todds is the long-term shipped binary. Its alpha-aware formats, mip filters and bounded processing are useful implementation references. The helper, if approved, needs a narrow input/output protocol, versioned identity, explicit executable path and failure cleanup. It must operate on the exact selected inputs and owned output paths without shell-built conversion commands or background installation of dependencies. Core loading must remain functional without it.

Do not build another mod manager or automatic load-order optimizer. S10 owns the initial source/override-to-prepared-output routing contract, including freshness and fallback during ordinary eager loading. S11 consumes and extends that contract rather than creating competing routing and invalidation tables. Preparation must work without on-demand loading; do not require deferred loading to benefit from prepared textures.

**Evidence and finish.** Show dry-run estimates, cancel/restart, stale-output rejection, unsupported helper fallback and no source modification. Validate alpha/mask/orientation/mips with representative images; qualify Windows first; Linux helper packaging and qualification are owner-deferred until later. Report preparation duration and subsequent startup separately. Finish with a small useful preset set, not every encoder option.

**Inspiration / improve upon:** [todds pipeline][todds-pipeline], [mip filtering][todds-mips], [RimSort preset wrapper][rimsort], FGL's texture limits in the [Continued snapshot][fgl-continued]. Suggested agents: encoder and exact-file license review; owned-output routing/preset UX; image-quality qualification.

## S11 — Route assets efficiently and load unused assets on demand

**Result:** avoid repeated supplier searches and, when the user opts in, avoid preparing assets that the session never needs. On-demand mode remains **default-off** and prioritizes first use and colony entry.

**First acceptance checkpoint: routing.** First add a successful asset-route cache: remember the correct supplying mod/resource for a content type and path after ordinary resolution. Consume S10's prepared-output selection contract when present; keep source ownership and prepared replacement selection distinct within the same routing design. Preserve last-override precedence, Resources/bundle fallback, provider reload and dynamic changes. Do not permanently cache absence or assume an empty holder proves a deferred asset does not exist. This stage is useful independently and can use ordinary eager loading.

**Second acceptance checkpoint: optional on-demand loading.** After routing is accepted, select one asset family with a complete, inspectable consumer boundary. Routing acceptance alone does not deliver on-demand loading. Record pending, preparing, ready and failed/retryable states. Publish only a complete object where callers expect one; do not expose a 2×2 placeholder as a finished texture. Account for direct content-holder access, CPU pixel reads, masks, shared graphics and atlas construction. If all consumers cannot be covered, keep that family eager rather than chasing arbitrary hooks.

Use measured preparation of assets needed for colony entry before declaring that transition ready. Bound background work by bytes and elapsed work per frame. On first-use miss, correctness takes priority and ordinary synchronous completion is available, but a repeatable visible pause fails this mode's intended smoothness. Reduce admitted scope or prewarm earlier rather than accepting the stall as a startup win. Never use a fixed one-second wait returning null or global audio muting to hide unfinished work.

Do not include graphics, icons and audio all at once. Deliver the initial family and generic readiness boundary first; add another family only when the first's consumer/ownership evidence transfers. The steward can create a separate follow-up for materially different audio or graphics lifecycle work. Preserve existing object identity/sharing expectations and destroy only owned replacement resources.

**Evidence and finish.** Test correct supplier, overridden/missing/reloaded assets, direct consumers, failure/retry, a warm cache and an empty cache. Qualify menu time, save/new-map readiness, first-use frame stalls and peak memory separately with the feature both off and on. Owner preference makes smooth entry/first use a hard acceptance direction; negotiate a numeric threshold from an actual baseline rather than inventing a universal frame budget in this plan.

**Inspiration / improve upon:** [YaOpt routing/lazy completion][content-manager], [FGL Continued][fgl-continued]. Keep ordinary eager behavior outside admitted cases. Suggested agents: consumer/ownership mapping; scheduler implementation; first-use and override review.

## S12 — Restore DDS detail progressively, only when worthwhile

**Result:** an additional default-off quality/loading option can start with smaller stored DDS mip levels and restore full detail before the player needs it.

**Entry condition.** S11's readiness and first-use behavior must be sound. Confirm a meaningful eligible DDS workload and a reason this improves the owner's smoothness preference. If the necessary high-detail work immediately returns at colony entry, keep the ordinary full-detail path and report this mode as not justified.

**Approach.** Use the real DDS layout to select a valid smaller mip tail, with format/block/dimension checks. Key work by the shared source resource and settings, not just ThingDef, because multiple definitions can use one texture. Coordinate color/mask changes and maintain original dimensions for atlas decisions. Do not mutate a texture already published into an atlas without rebuilding the associated mappings safely.

Schedule restoration through the S11 worker/readiness rules. Bound work per frame and prepare before visible first use where possible. Handle destroyed owners, reload, cancellation and failed full-detail restoration. Keep this separate from permanent S10 downscaling: one promises restored detail, the other intentionally changes the prepared result. Expose the relationship in ordinary settings without asking users to understand mip arithmetic.

**Evidence and finish.** Validate dimensions/UVs, masks, shared users, zoom/first appearance, canceled owners and restored fidelity. Compare startup/entry/stalls/peak memory and retention of both low/full payloads. Do not accept an unbounded pre-render drain or visible detail popping as automatically worthwhile. Close with accepted opt-in scope or an explicit declined feature, not a hidden default change.

**Inspiration / improve upon:** [YaOpt DDS tail selection and restore queue][content-manager]. Its queue drains completely in one pre-render call; Wake-Up should not carry that behavior over. Suggested agents: DDS layout/resource sharing; visual transition and scheduling review.

## S13 — Keep long loading operations moving in the background

**Result:** save loading and supported world/map operations can finish while the window is unfocused, then preserve the player's normal pause/background preference. This is responsiveness, not a claim of faster CPU execution or faster initial startup.

**Approach.** Inspect the current game's `LongEventHandler`, root update/focus logic and preference application before choosing hooks. Unity's `Application.runInBackground` controls whether the application continues when unfocused [Unity API][unity-background]; it is not the same thing as RimWorld simulation pause. Confirm which operations already run and where main-thread finalization waits for focus.

Add an explicit setting and scoped ownership for only the relevant operation lifetime, acquired when an eligible event is queued before the application can become inactive. Nested/follow-up events and completion callbacks must not restore the background state too early. `PrefsData.Apply` resets the Unity property even for unrelated preference updates: preserve the temporary permission while the eligible operation is active, then restore the latest intended preference without writing `Prefs.RunInBackground`.

Restore on success, cancellation and exception. In the inspected development reference, `Root_Play.Update` calls `Current.Game.UpdatePlay` immediately after `base.Update` when no event remains. Resetting the Unity background flag alone cannot stop that same completion frame from advancing gameplay. Add a narrow completion-frame guard honoring existing pause state, pause-on-load and the player's background preference. Preserve synchronous events that require their window to be displayed; do not globally bypass that condition. Avoid permanent preference changes or blanket always-background behavior.

Start with save loading, then extend to world/map generation only where the same verified lifecycle applies. Treat focus, minimization and OS throttling as separate observed conditions; don't promise behavior that a single property cannot guarantee. No UI automation is permitted by repository rules, so owner-assisted focus qualification or an authorized existing fixture method is needed for the actual platform claim.

**Evidence and finish.** Focused logic checks cover nested/failed/canceled operations and changed preferences. Authorized live checks cover unfocused completion and post-completion pause behavior on each claimed platform. Existing startup menu success cannot establish this feature. Finish with clearly named supported operations and no unexplained preference changes.

**Inspiration / improve upon:** [Load in Background's stated behavior][background-mod]. Current mod source remains unavailable; the implementation must be derived from current game behavior, not guessed internals. Suggested agents: native long-event/focus flow; cleanup and pause-state review.

## S14 — Warn about incompatible replacement mods

**Result:** users receive a clear warning when an active mod conflicts with loading behavior actually delivered by Wake-Up. Use mod metadata so RimWorld and RimSort can show unconditional conflicts, and a consolidated in-game launch warning for conflicts that depend on enabled features or conditions. This is an additional owner-requested capability; it does not replace any of the original nine goals.

**Prerequisites and scope.** Build the final rules from the steward's accepted capability-coverage table, each slice's hook/ownership findings and exact package IDs. An ecosystem inventory entry is not automatically incompatible. Similar behavior or redundant work alone does not prove a conflict; explain harmless redundancy separately if useful. Never claim Wake-Up replaces missing, deferred or rejected functionality. Do not mark gameplay optimizers, mod managers or Prepatcher incompatible merely because they appear in the comparison. Preserve supported optional supplier integrations until evidence establishes a specific superseding/conflicting path.

**Approach.** Maintain a small explicit conflict catalog containing package ID, the affected Wake-Up capability, the actual conflict/replacement reason, relevant version or mode restrictions, the Wake-Up selection/admission conditions and the user's resolution choices. Match stable package identity, not display-name fragments. Verify current identity from the inspected source/package; record unknown identities as unresolved instead of guessing or broadly matching forks.

The release template already has an `incompatibleWith` list for old Wake-Up copies. The pinned [RimSort metadata reader](../../artifacts/ecosystem-review-20260909/sources/rimsort/app/models/metadata/metadata_factory.py:600) reads that field and can select a game-version-specific override; this source observation does not establish the eventual UI result. Confirm how the selected RimWorld build and RimSort interpret the metadata, including whether they consider enabled versus merely installed mods. Add unconditional incompatibilities only when running those packages together is unsupported regardless of the relevant Wake-Up settings. Metadata is static: do not use it to claim a settings-dependent conflict always exists. Retain old-copy warnings and Prepatcher's required-dependency declaration. Do not publish an independent RimSort community-rules change as part of this slice without separate authorization.

For conditional conflicts, evaluate the effective active mod list and launch settings, plus actual feature admission where it determines the conflict. A disabled or merely installed inactive mod should not trigger an in-game conflict popup. A Wake-Up feature that declined activation must not be described as having taken over that behavior. Existing guards must still preserve safe original behavior before the warning is displayed; warnings do not replace compatibility checks.

Queue at most one consolidated warning dialog at the earliest verified safe UI boundary after detection, without changing loading/constructor/callback order. If the conflict can prevent menu completion, rely on existing pre-activation refusal and mod-manager metadata rather than promising an unreachable popup. Show the mod name, conflicting capability, practical consequence and precise resolution choices: disable the conflicting feature or choose one mod as appropriate. Do not remove, unsubscribe, reorder or disable mods automatically. Preserve a readable diagnostic/settings entry; avoid a modal dialog per mod or per callback. Any persistent dismissal policy must retain a way to see the warning and reconsider changed conflict conditions.

The original request allows metadata and/or in-game warnings. Deliver both where they convey different verified conditions; if one surface is unavailable or unnecessary, report its disposition explicitly. Broad third-party compatibility work, automatic migration and mod management are outside this slice.

**Evidence and finish.** Use focused checks for catalog identity and conditions, optional features on/off/refused, inactive versus active mods, unknown supplier versions, multiple conflicts, repeated initialization, and a clean launch with no warning. Verify the generated package's metadata and its required Prepatcher declaration using existing packaging checks. Inspect RimSort's actual metadata reader or focused existing checks before claiming manager compatibility; do not count an XML element's presence alone as proof of UI behavior. Authorized in-game qualification verifies the consolidated message, safe timing and ordinary continuation; no UI automation or normal-install modification is authorized by this plan. Keep the warning path inexpensive, separate it from scored menu timing, and state each surface's implemented/offline/live status independently.

Finish before combined release review with the exact catalog and user-facing text, conflict evidence, metadata/runtime parity where applicable, unsupported or deferred rules, and updated replacement coverage. The absence of a warning is not a universal compatibility promise. Suggested agents for the eventual assigned orchestrator: package/metadata identity research; feature-condition and warning-lifecycle review.

## Validation and release decisions

Use existing checks and fixture tooling. Read development instructions before building or operating the fixture. `python scripts/fixture.py test` is the existing offline build/test entry; use focused test filters where appropriate and retain required safety checks. New tests should target observable semantic risks, not mirror helpers line for line. Do not add another benchmark harness unless existing evidence cannot answer a concrete product question.

Live testing is separately authorized. The sole fixture is `.rlo-test-instance`; keep normal Steam/Workshop/profile/save/cache state and the Deck untouched. For ordinary startup comparisons, use the independent menu observer and automatic normal exit, compare `menuReadyObservation.elapsedSeconds`, wait for actual process exit and require `automaticTestPassed`. Never start a second run while the first remains alive. Retain exact source/package/deployment/run identities. The small profile restriction persists until the owner changes it; this plan does not authorize disruptive larger runs.

For performance decisions, use matched forward/reverse controls to address OS file-cache warming. Separate empty-cache construction, validated warm hits, changed-input rebuild, PNG-selected and native-DDS workloads. Do not add overlapping stage durations or extrapolate a few runs into a universal average. For S09–S12, include time to usable colony, first-use stalls and peak memory. Existing gameplay smoke support can cover a narrow transition/save/reload check when authorized, but does not prove broad gameplay compatibility or serve as the timing arm.

Before a costly implementation, estimate the ceiling from actual eligible work. If the relevant stage takes little time, its optimization cannot produce a large startup gain. Once focused checks and adequate comparison support a useful result, stop expanding validation and return to product work. Reopen recorded failures only with an evidence-backed change of mechanism or workload: more XML workers, plain read-ahead, DDS packs, layout-only atlas caches and broad reflection changes do not become promising merely because another mod uses them.

The steward reviews each handoff for behavior, dependencies, defaults and unresolved risk. Mark a feature **implemented/offline checked**, **live qualified**, **rejected**, or **deferred** accurately. “Ready for next slice” is different from “ready to release.” No automatic per-slice publishing.

After accepted slices are integrated, perform a proportionate combined pass on the exact release candidate: normal settings activation, each optional mode both off and on where relevant, shared-cache invalidation/recovery, and a small set of interactions that share hooks or resources (XML replay plus translations; prepared textures plus on-demand/atlas consumers; background completion plus pause; replacement warnings with affected features on/off and confirmed conflicting packages active/inactive). Preserve existing qualified features. Qualify Windows and Linux claims independently, using authorized environments; untested paths remain explicitly unqualified.

Before calling this an ecosystem replacement, the steward presents a final coverage table: delivered loading capability, relevant competitor feature, supported conditions, remaining gap and evidence. Conditional experiments may be rejected, but replacement claims must then narrow accordingly. The owner decides release scope if a material feature remains missing. Source/license attribution and packaged notices are part of the concrete release candidate; copying unknown-license code is not assumed permitted. Publishing remains a separate owner decision and must preserve the Workshop description unless explicitly authorized to change it.

## Initial stewardship review — 9 September 2026

The owner requested a documentation reorganization and refreshed current guidance. The steward owns documentation edits only on `codex/ecosystem-roadmap-docs`, based on private `main` at `5299ff4974367e5492181f2f72ccd20f91e51629`. No implementation team is assigned and no build, deployment, live test or publication is authorized. The source starting point remains unresolved: the researched public baseline is `b2d83c324a1fdf6095d927d6ed12c695f4a49d5f`, also the locally recorded `origin/main`. Verify the remote again at handoff rather than treating a local tracking reference as a live remote check.

Private and public Git histories were deliberately separated by the existing release workflow. Preserve both. The earlier suggestion to replace private main with public history is withdrawn; use a reviewed source reconciliation under the established single-checkout workflow, or obtain an explicit owner decision to change that workflow. Do not implement from the older runtime merely because the current branch is named main. See [current state](../current-state.md) and [release/source-history rules](../release.md#source-history-and-publication).

S01 is the first recommendation because obsolete PNG entries currently consume the fixed budget and failed writes can leave pending files. Existing content/payload checks and temporary-file publication are already present. Keep the initial change narrow: the existing PNG consumer, unchanged default and size limits, a small shared maintenance boundary, and focused recovery checks. It is a maintenance improvement and prerequisite, not an assumed loading-speed gain.

All nine original goals remain in scope. The owner additionally requested S14 compatibility warnings, sequenced after capability acceptance and before combined release review. This review rejects or defers no original capability. Conditional capabilities remain explicitly unimplemented; closing an experiment will require its own delivered/rejected/deferred disposition and a stated coverage gap.

| Original goal | Slice coverage |
|---|---|
| 1. Independent XML reuse | S04 parsed input; S05 safe processed replay and conditional inheritance reuse |
| 2. Translation loading | S03 application; S06 persistent parsed records |
| 3. Broader faithful texture caching | S07 coverage; S09 first-build processing |
| 4. Cache ownership and maintenance | S01 |
| 5. Optional on-demand loading | S11; conditional S12 detail restoration |
| 6. Texture preparation and quality | S09 first-build work; S10 preparation; conditional S12 |
| 7. Code searches | S02 |
| 8. Loading display and diagnostics | S08 |
| 9. Background save/world/map loading | S13 |
| Additional owner requirement: replacement-conflict warnings | S14 |

The owner approved the S10 helper and storage scope above. Windows packaging has scoped offline evidence; Linux helper work is owner-deferred and live output still requires qualification; this does not approve unrelated native backends. Prepatcher remains required; on-demand loading and quality-changing modes remain optional and default-off; individual qualification precedes one intended combined release.

## Handoff prompt and execution ledger

The steward can hand a slice to a new task using this concise brief, filled with current values:

> Implement slice Sxx of this master plan. Read the current repository rules/state and the slice's cited implementation references. Your accepted dependencies are [commits/interfaces]; your checkout ownership is [scope]; current live authorization is [none or exact scope]. You own this slice and may delegate independent bounded work to sub-agents. Preserve unrelated changes, use this single checkout and keep validation proportional. Deliver the specified behavior, focused evidence, source/package/run identities where applicable, limitations and a short handoff for steward review. Do not publish or expand conditional scope silently.

The plan records confirmed owner decisions, including the now-approved S10 helper and storage scope. Updated implementation state after parent-steward acceptance of S01:

| Slice | State | Orchestrator / branch | Accepted commit / evidence | Steward decision or next action |
|---|---|---|---|---|
| S01 | Accepted: implementation and offline checks | S01 team finished / `codex/s01-cache-maintenance` | Source `691be0a`, handoff `cf0dc76`; [steward acceptance](ecosystem-replacement-20260910/s01-cache-maintenance.md#parent-steward-acceptance--9-september-2026) | Correction resolves unnecessary capture when no entry fits. 142 managed passes, one optional skip, 35 Python passes; live qualification and release readiness remain pending |
| S02 | Accepted: corrected implementation and offline checks | S02 task finished / `codex/s02-code-searches` | Source `436c652`, handoff `68e8f31`; [acceptance](ecosystem-replacement-20260910/s02-code-searches.md#parent-steward-acceptance--9-september-2026) | Known colony-entry caller is reached; 21 focused passes and clean build. Name cutoff preserved; extra memoization not justified. Live benefit and release readiness pending |
| S03 | Accepted: corrected implementation and scoped offline checks | S03 team finished / `codex/s03-translations` | Source `0a26702`, handoff `cc92d48`; [acceptance](ecosystem-replacement-20260910/s03-translations.md#parent-steward-acceptance--9-september-2026) | Normal default-off control works in focused offline checks; earlier algorithm evidence retained. Production Mono/Harmony and live benefit unqualified; further lookup caches not justified |
| S04 | Cost checkpoint accepted; tested encoding rejected; capability deferred | S04 task finished / `codex/s04-parsed-xml` | Report `cf2adc3`, unchanged production source; [disposition](ecosystem-replacement-20260910/s04-parsed-xml.md#parent-steward-disposition--9-september-2026) | Full warm 58.94 ms versus 11.43 ms native offline; steady-open 30.16 ms. Independent parsed reuse remains undelivered. Reopen only with changed premise addressing overhead; reassess after S06 |
| S05 | Research accepted; implementation deferred at existing prerequisite | Research task finished / `codex/s05-feasibility` | Report `5565954`; S04 cost findings `cf2adc3` | Useful eligible patch work and effective hooks remain unproven. Reassess after S06; replay/inheritance reuse stay undelivered and in scope. No new live authorization |
| S06 | Cost checkpoint accepted; capability deferred | S06 task finished / `codex/s06-language-cache` | Report `5dadf75`, unchanged production source; [disposition and XML reassessment](ecosystem-replacement-20260910/s06-language-cache.md#parent-steward-disposition-and-xml-reassessment--9-september-2026) | Tested grouped representation not retained: no repeatable complete-cost advantage. Persistent language reuse remains undelivered; reopen only with a changed premise |
| S07 | Accepted: implementation and offline checks | S07 task finished / `codex/s07-texture-coverage`; ownership released | Source `4be5fb7`, handoff `6f90607`; [acceptance](ecosystem-replacement-20260910/s07-texture-coverage.md#parent-steward-acceptance--9-september-2026) | JPEG source coverage: two active 1024-square inputs fit unchanged limits, larger source falls back. 84 focused managed + 35 Python passes; package built, no deployment/live JPEG fidelity or speed qualification |
| S08 | Accepted: implementation and offline checks | S08 task finished / `codex/s08-loading-display`; ownership released | Source `07d024c`, handoff `fa9e101`; [acceptance](ecosystem-replacement-20260910/s08-loading-display.md#parent-steward-acceptance--9-september-2026) | Default-off native-observer panel; 36 managed + 35 Python passes, clean package build. Preview already lazy; no deferral. No deployment/live layout, compatibility or overhead qualification |
| S09 | Negative cost checkpoint accepted; capability deferred | S09 task finished / `codex/s09-first-build-images`; ownership released | Report `fb30593`, unchanged production source; [disposition](ecosystem-replacement-20260910/s09-first-build-images.md#parent-steward-disposition--9-september-2026) | Tested Python lossless IDAT pipeline rejected before integration: 0/8 paired wins, 12.50x input growth. Unity/.NET cost and first-build acceleration unqualified; no new package/dependency/live authorization |
| S10 | Corrected Windows/offline checkpoint accepted; full slice incomplete | S10 task stopped; ownership returned | Source `7196b5f`, handoff `10c6e24`; [review](ecosystem-replacement-20260910/s10-texture-preparation.md#parent-steward-correction-review--9-september-2026) | Native preservation/store/controller and Windows helper delivered offline; lossy coverage only BGPlanet (zero fixture matches). Linux helper deferred by owner; proceed to S11 routing with the accepted Windows contract. Broader lossy capability and all live/release qualification remain gaps |
| S11 | Routing accepted offline; on-demand feasibility accepted, implementation deferred | S11 team stopped / `codex/s11-asset-routing`; ownership returned | Routing source `950144b`; on-demand report `7b07918`; [disposition](ecosystem-replacement-20260910/s11-on-demand.md#parent-steward-disposition--9-september-2026) | No useful complete-consumer family established. Routing remains independent; on-demand is undelivered, not universally rejected. Revisit with changed evidence; no final omission approved |
| S12 | Deferred at existing S11 prerequisite; not implemented | Unassigned | S11 [disposition](ecosystem-replacement-20260910/s11-on-demand.md#parent-steward-disposition--9-september-2026) | Readiness/first-use behavior absent; no progressive-detail claim. Revisit after justified S11 delivery and eligible DDS evidence |
| S13 | Save-loading implementation and offline checks accepted | S13 team stopped / `codex/s13-background-loading`; ownership returned | Source `5c3f460`, handoff `a340193`; [acceptance](ecosystem-replacement-20260910/s13-background-loading.md#parent-steward-acceptance--9-september-2026) | Default-off ordinary save loads only; preferences and completion frame guarded. 53 managed + 37 Python passes, package built; live focus/gameplay unqualified. World/map generation extensions undelivered |
| S14 | Catalog/status checkpoint and offline checks accepted | S14 team stopped / `codex/s14-conflict-warnings`; ownership returned | Source `d44a912`, handoff `0aef260`; [acceptance](ecosystem-replacement-20260910/s14-conflict-warnings.md#parent-steward-acceptance--9-september-2026) | Two legacy conflicts retained; no additional conflict/dialog rule justified. Status text clarified. 47 managed + 37 Python and four updated release passes; live UI unqualified |

### Automated stewardship — authorized 9 September 2026

The owner authorized a five-minute recurring check in steward task `01a08711-7899-7ff3-a1b5-4a0b105ba661` (automation `wake-up-slice-steward`). It reviews finished work against actual changes and evidence, sends concrete corrections to the same task, and assigns the next approved phase to a fresh task after acceptance. Tasks use this checkout directly and continue from the latest accepted slice base, preserving the reconciled runtime and unrelated work. Only one task owns repository changes, builds and operations at a time; read-only research may overlap.

The private operational record is `artifacts/ecosystem-review-20260909/steward-state.json`: active task, ownership, reviewed commits, evidence, next phase, correction progress and last action. Record a unique action reference and complete assignment before sending or creating a task, then save its result. Recover uncertain actions from existing task messages before retrying; never create duplicate work. This ledger records accepted outcomes; the private record tracks dispatch while a team owns the checkout.

Routine approved implementation, offline checks and review corrections may proceed autonomously. Planning and task dispatch do not authorize deployment, live testing, normal game-data changes, Deck access, publication or other restricted actions. Material changes to scope, dependencies, defaults or user experience still require the owner. Explain the specific blocked action and recommended decision; complete useful independent preparation within scope. Pause the recurring check while awaiting that decision or after authorized work is complete, and resume only when the owner authorizes continuation. Silence and another agent's agreement never grant approval.

Remain quiet while waiting or unchanged. Notify the owner when a slice is accepted and the next begins, input is needed, or authorized work is complete. If two consecutive correction rounds make no material progress on the same issue, pause and explain it rather than looping. Keep validation proportional and distinguish implementation, offline checks, live qualification and release readiness throughout.

### XML sequencing after the S04 cost stop — 9 September 2026

The steward accepted S04's bounded negative checkpoint, not a delivered cache. Its tested per-file encoding is rejected for integration; independent parsed reuse is deferred pending a changed premise that addresses measured overhead. S05 implementation remains conditional on useful eligible patch work and effective-hook handling. Move independent S06 next, then perform a steward XML reassessment before S07. Use new evidence only; do not automatically launch another format experiment. Goal 1, including parsed input, processed replay and conditional inheritance reuse, remains covered by the plan but undelivered. Final omission from replacement/release claims requires an explicit owner scope decision. This is an execution-order adjustment within the existing conditional plan, not removal of a goal.

### XML reassessment completed — 9 September 2026

S06's grouped-record cost checkpoint supplied no complete-cost win and no evidence for a viable grouped XML representation or safely eligible S05 workload. The steward therefore keeps S04, S05 implementation and S06 persistent reuse deferred and proceeds to S07. Their capabilities remain undelivered and in the original goal coverage; no final omission is approved. Reopening requires a materially changed, evidence-backed premise. This completes the scheduled reassessment rather than launching another automatic cache experiment. See [reviewed S06 evidence and disposition](ecosystem-replacement-20260910/s06-language-cache.md#parent-steward-disposition-and-xml-reassessment--9-september-2026).

## Reference material

### Further implementation research performed for this plan

The current release template [About.xml](<../../release/About.xml:11>) confirms Prepatcher is already required. Current `PngCache.Read` validates complete payload/layout before returning; `Write` already uses a pending file followed by a move. S01 extends this, rather than claiming publication/integrity is absent. The gaps include obsolete-entry reclamation, pending-file cleanup after errors and coordinated policy across new consumers. Keep hit accounting in memory and flush a summary rather than adding a disk write per cache hit.

Fresh read-only ILSpy inspection resolved the existing fixture's `current.json` to generation `20260904-182829-e058641c` and matched its game assembly to the manifest: **GOG 1.6.4871 rev573**, `Assembly-CSharp.dll` SHA-256 `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`. There was no active `prepared.json`. This is a development-reference identity, not the currently released Steam/Deck identity. Required method behavior must be compared on each newly qualified target; no game was launched for this research.

Inspected methods included `LoadedLanguage.LoadData`/`LoadFromFile_DefInject`, `DefInjectionPackage.InjectIntoDefs`/`SetDefFieldAtPath`, `PlayDataLoader.DoPlayLoad`, `LanguageDatabase.SelectLanguage`, `Root.Start`/`Update`, `Root_Play.Start`/`Update`, `PrefsData.Apply` and `LongEventHandler` asynchronous completion. The findings underpin S03/S06/S13: native parallel language reads, two distinct injection passes, late injection through completion callbacks, startup's existing background override, and the same-frame gameplay hazard at later completion.

Existing local reading aids: [LoadedLanguage.cs](<../../artifacts/remaining-loading-investigation/LoadedLanguage.cs:207>) and [LongEventHandler.cs](<../../artifacts/unexplained-loading/LongEventHandler.cs:265>). These older extracts are navigation aids; the freshly hash-verified assembly inspection establishes the reference above. Exact current XML replay hooks remain an explicit S04/S05 discovery task, not a falsely claimed completed native pipeline review.

The planning pass also reread YaOpt's successful-injection map and full pre-render restoration drain, FGL's actual leaf algorithm, FastLoader's one-mip texture capture, Wake-Up's current texture cache/activation paths, and todds' bounded pipeline. These support the split between faithful cache expansion, first-build work, explicit preparation and demand-driven publication. Current Unity documentation was checked as an API reference; engine-version-specific qualification remains with the implementing slices.

### Pinned community sources

The following links pin reviewed source revisions rather than floating branches. Use the [comparison ledger](<../../artifacts/ecosystem-review-20260909/comparison.md>) for all supplier versions, license discoveries, historical gaps and local receipts. Source snapshots are in `artifacts/ecosystem-review-20260909/sources/`; raw private research stays under artifacts and is not shipped with the mod. Exact file/dependency licenses must be checked before adaptation; independent design is available when copying is not authorized.

- XML/cache: [DefLoadCache hook][cache-hook], [storage][cache-storage], [FastLoader document/source restoration][xml-runtime], [Gagarin input checks][gagarin-input].
- Translations/searches: [YaOpt injection map][translation-map], [FastLoader application reference][translation-applier], [language cache][language-cache], [injection format][injection-cache], [FGL leaf search][leaf], [YaOpt type cache][type-cache], [Performance Fish reflection][reflection].
- Textures: [FastLoader raw store][raw-storage], [atlas][atlas], [YaOpt routing/DDS restoration][content-manager], [FGL Continued][fgl-continued], [todds CPU pipeline][todds-pipeline], [mip filter][todds-mips], [RimSort preparation presets][rimsort].
- UI/background: [Loading Progress constructor lifecycle][progress], [Startup Impact][impact], [historical preview deferral][previews], [No Modlist][no-modlist], [Load in Background][background-mod].
- Unreviewed native lead: [Image Opt Workshop description][image-opt] and [secondary investigation][image-opt-lead]. Verify an actual source/package identity before relying on reported implementation defects.
- Engine API research: [raw texture layout/upload][unity-raw] and [unfocused execution][unity-background]. These links are Unity 2022.3 references; verify the fixture's actual engine version before selecting APIs or claiming support.

[cache-hook]: https://github.com/FluxxField/rimworld-defload-cache/blob/f674263b5d17e5a2191828d2003cff61e2b5b1c0/src/Hook/CacheHook.cs#L65
[cache-storage]: https://github.com/FluxxField/rimworld-defload-cache/blob/f674263b5d17e5a2191828d2003cff61e2b5b1c0/src/Cache/CacheStorage.cs#L107
[xml-runtime]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/FastLoaderRuntime.cs#L641
[gagarin-input]: https://github.com/ViralReaction/MissileGirl/blob/3170983d90e726f47f105dd2f426af8c3ce60e29/Source/Gagarin/Core/Patches/LoadableXmlAsset_Patch.cs#L42
[translation-map]: https://github.com/szszss/YetAnotherOptimizer/blob/ab60621f071fbee3e4f1a92bb163c2760b8605bf/Source/YaOpt/Helpers/DefInjectionHelper.cs#L21
[translation-applier]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/FastDefInjectedApplier.cs#L31
[language-cache]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/LanguageBinaryCache.cs#L78
[injection-cache]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/DefInjectedBinaryCache.cs#L45
[leaf]: https://github.com/Taranchuk/FasterGameLoading/blob/1b23e89cbf56350e5dff3991332a148e49e725a9/1.6/Source/ReflectionOptimizations/GenTypes_AllLeafSubclasses_Patch.cs#L13
[type-cache]: https://github.com/szszss/YetAnotherOptimizer/blob/ab60621f071fbee3e4f1a92bb163c2760b8605bf/Source/YaOpt/Helpers/RuntimeInfoCache.cs#L66
[reflection]: https://github.com/bbradson/Performance-Fish/blob/a99972001e02d553a738611726cd9577ff41f86e/Source/PerformanceFish/System/ReflectionCaching.cs#L498
[raw-storage]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/RawTextureEntry.cs#L21
[atlas]: https://github.com/solaris0115/FastLoader/blob/d95ae707661e46ff50a04b39d90173f6b90ed1e6/Project/Source/StaticAtlasCache.cs#L41
[content-manager]: https://github.com/szszss/YetAnotherOptimizer/blob/ab60621f071fbee3e4f1a92bb163c2760b8605bf/Source/YaOpt/Helpers/ContentManager.cs#L132
[fgl-continued]: https://github.com/mushroomTW/FasterGameLoading---Continued/tree/0132c10a3185f578815fb1dba012b7b24f78d5e3
[todds-pipeline]: https://github.com/todds-encoder/todds/blob/bf78b2303b00ad0f99ffb409a4827eb305650820/src/pipeline/pipeline.cpp#L26
[todds-mips]: https://github.com/todds-encoder/todds/blob/bf78b2303b00ad0f99ffb409a4827eb305650820/src/pipeline/filter_generate_mipmaps.cpp#L21
[rimsort]: https://github.com/RimSort/RimSort/blob/3b13dfc32dd22712e655cbd02277dbdd1230d84e/app/utils/todds/wrapper.py#L65
[progress]: https://github.com/ilyvion/loading-progress/blob/bcd71bafd994b206ed644df0ae56583beb047646/Source/ilyvion.LoadingProgress/StaticConstructorOnStartupUtilityReplacement.cs#L31
[impact]: https://github.com/AUTOMATIC1111/StartupImpact/blob/f70bddb49460783a036df2eaff5c61b3941d7dd6/Source/Patch/ModContentPack.cs#L13
[previews]: https://github.com/razuhl/RIMMSLoadUp/blob/2d2f895a202523206f517b41c85c61c16922ac2e/Source/RIMMSLoadUp/DelayLoadingModPreviewImages.cs#L22
[no-modlist]: https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405
[background-mod]: https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379
[image-opt]: https://steamcommunity.com/sharedfiles/filedetails/?id=3543873568
[image-opt-lead]: https://gist.github.com/Lyoko-Jeremie/094c1700d33f55a26c5629bcd5be1385
[unity-raw]: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.LoadRawTextureData.html
[unity-background]: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-runInBackground.html

### S10 decision checkpoint — 9 September 2026

After accepting S09's negative result, begin S10 with a bounded encoder/packaging comparison and concrete recommended user workflow. The optional native-helper decision remains unresolved. Complete the decision's evidence and independent routing/interface preparation first, then bring a specific recommendation to the owner before dependent implementation or bundling. This splits S10's assignment into preparation and implementation, preserving its full capability and S11's eventual use of the same prepared-output contract. It does not authorize new dependencies or turn the preparation into a general framework.

**Prepared checkpoint:** [S10 decision report](ecosystem-replacement-20260910/s10-preparation-decision.md) compares current DirectXTex, Compressonator and archived todds with exact source/license/distribution evidence. It recommends an optional owned CPU helper using DirectXTex, explicit native-preserving and lossy workflows, and a separately disclosed prepared-store budget; Linux executable packaging and large-entry coverage remain open. Native loader inspection grounds one prepared-output resolver that S11 can extend. The decision task and read-only researcher release checkout/build ownership to the parent steward and stop. Steward review is complete; await the owner's dependency/budget decision; no S10 runtime, helper, UI or package is implemented. S04/S05/S06/S09 remain deferred gaps, not approved omissions.


**Steward review completed:** S10 report `c0d755f` is accepted as decision preparation only. The owner must decide optional Windows/Linux DirectXTex helper development/local packaging and the additional 512 MiB prepared-store budget (up to 1 GiB combined categories, plus bounded staging). The recurring check is paused pending that decision; no team owns ongoing edits/builds. No implementation, new dependency, live test or final capability omission is approved by this review.

### Owner approval and continuation — 9 September 2026

**Additional offline tooling approval:** steward action
`s10-offline-guard-approved-20260909` records the owner's explicit approval to let
only fixture `test` and `build` proceed with freshly verified running executable
paths outside the canonical fixture. Fixture, mixed or unknown process identities
still refuse. The fixture lock and frozen-reference/committed-package checks stay;
every other operation retains its no-game restriction. This resolves the actual
offline tool block while leaving the normal Steam game alone, and does not grant
Linux environment installation, deployment, game launch, Deck access or publication.

On 9 September 2026, the owner approved both S10 proposals and explicitly requested continuation: develop and locally package an optional DirectXTex CPU helper for Windows x64/Linux x64, with explicit native-preserving, full-size recompressed and smaller-texture choices; add a separate prepared-output category capped at 512 MiB, initially retaining the 8 MiB complete-entry limit. Combined category allowance may reach 1 GiB plus bounded one-item staging. Existing defaults stay unchanged and use of prepared textures defaults off. This authorizes scoped dependency/helper implementation and offline packaging checks, not deployment, game launches, Deck access, publication or automatic system dependency installation.

This supersedes the pending-decision statements in the historical S10 checkpoint and review below/above. Resume the existing five-minute automation and assign a fresh S10 implementation task from the current reviewed lineage. S04/S05/S06/S09 remain deferred gaps, not approved omissions.

### Sequence after S11 on-demand feasibility — 9 September 2026

The bounded on-demand checkpoint found no useful fully covered family; implementation remains deferred pending new concrete consumer/ownership evidence. S12 is deferred at its existing readiness prerequisite. Continue directly to independent S13, then S14 and combined review. Keep S11/S12 and earlier capability gaps explicit; this sequencing choice does not authorize permanent release-scope omission or an ecosystem-replacement claim.

### First offline pass closed — 9 September 2026

All dispatched checkpoints have been reviewed. The [combined review](ecosystem-replacement-20260910/combined-review.md) records delivered scope, every missing capability and release limits. No final omission or ecosystem-replacement claim is approved. All teams returned ownership; pause the recurring workflow for the proposed one-run small Windows fixture deployment/startup decision. Preserve the owner-deferred Linux helper and all other capability gaps. No live run or publication is authorized.

### Functional versus performance process policy — 9 September 2026

Functional checks (startup, correctness, activation and similar checks without a performance-benefit claim) may run alongside a freshly verified normal RimWorld process outside the fixture. Only an existing fixture process or uncertain executable identity blocks these operations. Offline work, deployment and preparation use the same fixture-specific check. Performance-measurement launches still require all RimWorld instances stopped. Prepare functional runs with `--purpose functional`; the saved run purpose controls launch admission, and legacy runs without a purpose remain performance runs. Functional-run timings are diagnostic only, not evidence of a speedup. Live launches still need separate owner authorization, and this never permits closing or modifying the normal game. This supersedes the earlier test/build-only process exception and the combined review's proposed requirement to close the normal game for a functional startup check. It does not independently authorize the pending live run. The steward implemented the purpose-aware guard and verified 38 Python checks without a game launch.

### Functional qualification authorized — 9 September 2026

On 9 September 2026, the owner explicitly authorized performing functional test runs now, iterating on failures and using separate tasks/sub-agents as appropriate. The next team owns small isolated Windows qualification: deployment/preparation, observed startup checks, focused activation/correctness checks and necessary fixes/retests. Use --purpose functional; the normal Steam game may stay open and must remain untouched. Keep workload small, one fixture process at a time, normal automatic exit and captured evidence. This grants no performance-comparison, medium/full workload, normal data, Deck, UI automation, security/system tooling or publication permission. Stop when sufficient focused evidence answers the admitted checks; do not broaden harness work without a concrete failure. The accepted starting base is `4c10558`, carrying latest runtime `d44a912`. Record actual deployed/run identities and corrections separately from earlier offline acceptance. The current qualification assignment supersedes the first-pass pause; original missing-capability disclosures remain.

### Functional qualification accepted — 9 September 2026

The steward reviewed the actual changes from assigned base `8412f7c` through clean handoff `a8ccd43`, the narrowed Mono/Prepatcher compatibility corrections, fixture controls, observer checks and captured evidence. Corrected runtime `79dde439051e56dab5fb910958fafd689fd6750d` is accepted for the bounded Windows functional scope. Its package and deployed receipt agree on DLL SHA-256 `dca48f2dadd4f0c503aa66f9c09887576ecec984d2e7b8cbec16dbc1e4e4e87a`. Final `wfq-combined-21`, observer UUID `b71b174f4dc24f778a3358d528f7ecf7`, passed startup, feature receipts, sampled French output and map/tick/save/reload with normal exit. Earlier feature refusals and probe failures remain captured; passing menu checks did not erase them.

This completes the authorized small functional pass. No further correction or unchanged retest is required for its stated scope. Pause the five-minute check and retain steward ownership. Recommend a short owner-assisted visual/focus check next, followed by a concrete decision about the undelivered capabilities before release planning. Preparation UI/lossy-helper/later-startup consumption, false-background-preference and focus behavior, broader compatibility, performance and Linux remain unqualified. All original nine goals plus S14 remain tracked; no missing capability is silently accepted as delivered or permanently omitted.
