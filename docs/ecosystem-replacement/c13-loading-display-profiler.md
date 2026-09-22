# C13 loading display, profiler and diagnostics

## Steward functional review passed — 12 September 2026 UTC

The steward reviewed clean handoff `3af68ac87fb9393a0caf7632f0911e762a0d08bd`
and final source `0a046d6914e254a0d49b01d407c1cc26e6efa597`, retaining the exact
supplier-free evidence at `387caa3` and previously reviewed screens/gameplay/
exports at `87204ef`. **C13 passes functional coverage, correctness and
compatibility review for rows 8a/8b/8c and its independent scheduling contribution.**
Performance and full acceptance remain pending. This is not combined release
acceptance or permission to remove suppliers whose other required features remain
unfinished.

Parent and independent reviewers verified that constructor/callback observation
now operates with display off, the scheduler reuses these observations, and
Loading Progress's actual content steps receive their package context before
yielding. Hooks-only calls and repeated runtime initialization requests are
labelled honestly. The exact one-instruction early supplier rewrite preserves
native branches, locals, exception regions, Action dispatch and error continuation.
Its inactive bridge calls the original action directly. C10 bootstrap cooperation,
C12's real generic helper and earlier scheduling/asset contracts remain intact.
Changed unsupported suppliers still require ordinary behavior and explicit limits;
this review does not promise universal supplier-version compatibility.

Parent recomputed the actual report hashes and details: both successful captures
show 87 VEF/35 CE constructor rows and five VEF/two CE callback rows. All 56 native
content markers are attributed; all 60 Loading Progress content markers have
actual provider-step parents and package owners, with 15 later hooks-only notices.
Both runs passed normal functional exit. The final package hashes match; seven
restored endpoint hashes and five absences match live files, and no RimWorld
process remains. The final Loading Progress screen was inspected. Evidence is
`artifacts/ecosystem-next-20260910/c13/r1/steward-evidence-review.json`, complementing
the initial parent review record. The five failed observation attempts remain
failed history; their menu exits are not accepted as profiler success.

Earlier independently verified startup/later screens, summary space reclamation,
world/map/save/reload, native preview behavior, explicit export and native XML/
language equality remain sufficient for unchanged paths. No second full matrix
was required. Bounded detail, earlier unobserved engine work, one unfinished outer
callback marker and indivisible native operations remain explicit. Timing spans
include waits and can overlap; they are not CPU time or menu-speed evidence.

The next owner is the existing C10 orchestrator under the approved C12 → C13 →
C10 completion → C11 → C14 → C15 sequence. Exact review/continuation base and
ownership are recorded in steward state. C10 must integrate real deferred work
with these observations when implemented. C14 still owns coordinated focus and
background lifecycle qualification. C13 overhead, responsiveness and retained
optimization effects require later matched individual/combined measurements in
a new explicit unattended window. No performance, Steam/Deck promotion, merge,
push or publication is authorized by this review.

## R1 corrected handoff for independent review — 12 September 2026 UTC

Reports now identify individual constructor and callback work when Wake-Up's
display is off and when Loading Progress owns the screen. Loading Progress's real
content work carries the correct package identity; its later native call is
identified as a call containing hooks only when the original body is skipped.
This corrects the two steward findings below. **Functional acceptance remains
with the steward**; this is a stopped handoff, not self-acceptance or release readiness.

R1 began at clean assigned `67ba7ded899d9af8d77c73d36f46faea43c068c8` on the
preserved `codex/ecosystem-next-plan-review` ancestry. Final source/core/observer
is `0a046d6914e254a0d49b01d407c1cc26e6efa597`. The supplier-free case was captured
at `387caa317e359f0a50ea668378f3abda674bc87c`; subsequent product changes qualify
the optional supplier and add its inert pre-load callback bridge. They do not
change the supplier-free invocation path. Prior screen, gameplay, export and
native XML/language comparisons independently verified by the steward remain
applicable; they were not repeated as a new matrix.

### Actual execution and ordinary behavior

The observer surrounds the synchronous operation that is about to run. Native
constructor and callback loops replace only their respective
`RuntimeHelpers.RunClassConstructor(RuntimeTypeHandle)` and `Action.Invoke`
calls with same-thread wrappers. The original operation executes once, and its
exception propagates to the original catch. Enumeration, callback additions,
ordering, completion and the actual C12 generic attribute helper remain intact.
Display scheduling reuses these observations without adding a second wrapper;
recording alone does not select yielding or drawing. Exact observation hooks
are admitted by the existing scheduler/background/PNG compatibility guards.

A static constructor initializes a type on its first use. The runtime can receive
later initialization requests that do nothing because the type is already ready.
Reports therefore name **RunClassConstructor requests**, not unique initializer
executions. Loading Progress's original constructor loop and its later native
`CallAll` for other hooks are both preserved. Multicast delegates remain one
ordinary delegate invocation and use shared/unknown attribution.

Loading Progress's complex callback iterator cannot be rewritten by Harmony:
even an identity transpiler reproduces `Unbalanced exception markers` in its
fault-region rewriter. The final implementation uses the existing Prepatcher
`FreePatchAll` API for this one exact supplier body. It changes the existing
`Action.Invoke` instruction to a public synchronous bridge with the same stack
effect and instruction size. Every other instruction, branch target, local and
exception region is retained. The bridge invokes the action directly when no
session is selected. It does not alter the iterator, queue or provider UI.

The original supplier file was inspected and pinned for qualification at SHA-256
`4f19f836fd41c8e702295cb90d7d5e66c53f38cda8a13a3ed79f1d063e1afcef`.
Runtime admission uses resolved method identities, not file paths, MVIDs or
metadata row numbers. The original Cecil callback fingerprint is
`D90E94252C9C825190FC77EC7878ED36D05D3D44C69DF3EB5B56AD4A619E6697`;
the derived rewritten runtime identity is
`C3B9CF53A0D6985D6354BBA57308A03199C6BA613E2AB186791E5AE115FB07A8`.
Constructor/content iterator identities remain independently checked. Changed
suppliers retain ordinary execution and explicit missing-coverage notices.

The content context surrounds each real provider `MoveNext`, the operation that
advances its iterator. It ends before control yields. Some advances only update
progress, which their label explicitly says. Actual native audio, texture, string
and bundle markers nest inside the correct package context. The later native
`ReloadContentInt` boundary is separate; Harmony's `__runOriginal` records when
that invocation contained hooks only. Its duration is not labelled content reload.

### Focused functional evidence

Raw evidence: `artifacts/ecosystem-next-20260910/c13/r1/`. Captures are under
`.rlo-test-instance/results/<label>/`. `capture-identities.json` records exact
packages, settings, reports, examples and verified parent relationships.

| Capture | Source | Observed result |
| --- | --- | --- |
| `c13-r1-report-only-02` | `387caa3` | Reports on/display off; 87 VEF and 35 CE constructor rows, five VEF and two CE owned callbacks; all 56 native content markers attributed across 14 active packages. Atlas/display scheduling off. |
| `c13-r1-loading-progress-07` | `0a046d6` | Loading Progress keeps its screen; the same VEF/CE constructor and callback detail; 438 constructor requests precede its native repeat. All 60 actual content markers have one of 15 package owners and a real provider-step parent; 135 advances are observed. Fifteen skipped native-call notices identify hooks-only boundaries. |

Both captures reach the independent menu repaint, exit normally with code zero,
capture successfully and set `automaticTestPassed=true`. Both reports contain
zero logged errors, zero untracked scopes and one explicitly unfinished outer
callback scope. Detail retention omits 294,787/295,132 rows respectively while
preserving their aggregates; these are bounded reports, not complete traces.
The LP report's 20 notices comprise the existing early/unmatched-marker coverage
notices and 15 hooks-only calls. No timing is used as performance evidence.

The native report SHA-256 is
`25e4c8d4ef6645fa363b74f88c151604f36948c6e3935502e2d082ba243e3669`;
the LP report is
`6d5636a69b96a495696d4892fcb4202500741cc178eff105af056fddf31c79d3`.
In LP capture 07, actual `10-loading-10.png` and `15-report-window.png` are readable
at 1280x720 / UI scale 1. Sixteen framebuffer captures and report/save/request/
cancel/preview receipts are retained. These use the existing private observer;
they do not claim desktop pointer interaction or verified OS foreground state.
The launcher requested minimized-noactivate and muted the fixture. Unity's
reported focus flag still does not establish the operating-system foreground;
C14 qualification remains pending.

Final validation passes **87 managed, four modern-host and 71 fixture-tool
tests**, with zero build warnings/errors. Tests preserve invocation order and
exceptions, native/C12 instruction contracts, exact source/rewritten supplier
identities, original exception regions, changed-body rejection and hooks-only
labelling. The modern host resolves the supplier's Mono string API; test-only
framework-name normalization is grounded in the original Cecil `System.Core`
references for HashSet/Enumerable. Runtime admission is unchanged and strict.
Independent read-only source review found no remaining concrete blocker.

Failed observation attempts remain retained, not accepted: report-only 01 found
Mono's additional private constructor-helper overload; LP 03/04 rejected empty
file location and unstable module/raw identity; LP 05 exposed the test-host
framework-name difference; LP 06 exposed Harmony's exception-layout limitation.
All exited/captured normally. No failed capture is counted as proof of provider
detail. Final source resolves each observed cause; no performance experiment was run.

### Restoration and ownership

Final package DLL SHA-256 values are core
`4c8cfdfb655592500e50c9af361f17f79d34260672298c5ecf97560e8ed2a385`
and observer `740f44265084793b52b2b8132cc9bb425e89edfa58dea054f00fd6275ba6b146`.
The fixture remains GOG generation `gog-rev573-20260910-194017-cf1a1eb0`,
manifest `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
All **19 R1 transactions were rolled back in reverse order**. Seven original
endpoint hashes and five required absences match the R1 starting state; audit
passes and no RimWorld process remains. The deployed endpoints are restored to
their original packages, not left on the tested candidate.

Child review, edits, builds, preparation, deployment and live operations stop at
handoff. Exclusive ownership returns to the steward for independent functional
review; corrections return to this same C13 owner. No successor, parent timer/state
edit, normal-game change, Steam/Deck access, merge, push or publication occurred.
Performance, full acceptance, C10/C11/C14/C15 and combined-release obligations remain.

## Steward review — corrections required — 12 September 2026 UTC

The steward reviewed handoff `52291b6bb4ac82eac1ac5f54ae3aed68986c7d3c`
against tested source `87204ef65d6f525ae66c526212a209663a2bc0e9`. C13 has
**not passed functional review**: the profiler currently loses useful detail
when Wake-Up does not own the display. Corrections return to the same C13 task
`01a09394-d4e0-7d72-98d3-b23de6813a37`; C10 remains idle until this review closes.

Individual static-constructor observations currently exist only in
`NativeLoadingUnits`, reached through the display scheduler. With reports on
and the display off, or Loading Progress owning the screen, only aggregate
native constructor markers remain. Likewise, known callback owners are lost:
the retained Loading Progress report identifies MVCF, Outposts, VEF and Combat
Extended callbacks but assigns them to shared/unknown work. This does not meet
row 8b's independent profiler behavior. Record actual Type/Action execution at
an observation boundary independent of UI/scheduling, preserving native dispatch,
order and errors; do not enable yielding merely to obtain report detail.

The same captured Loading Progress report exposes a second boundary error:
actual content work runs before a near-empty call labelled "Execute content
reload". Retained supplier source confirms its replacement iterator does the
work, then invokes the suppressed native method only to preserve other hooks
(`LongEventHandler_ExecuteToExecuteWhenFinished_Patches.cs:189-214` and
`ModContentPack_ReloadContentInt_Patch.cs:16-28` under the original research's
Loading Progress source). Observe the actual content execution and its available
package context, and label the hook-only call honestly. Preserve supplier
execution/display ownership; do not present its wrapper duration as content cost.

Parent checks confirmed seven functional normal-exit captures with the stated
source identities, report hashes, final package DLLs, and restored seven endpoint
hashes/five absences. The actual shown/hidden/later screens and report window are
readable at the tested scales; Loading Progress retains its screen. Parent
recomputed native XML/sample equality and full language equality after only the
two established empty observer diagnostic rows were removed. The display/report-
off explicit export contains both 13,809-node stages and 1,556 sources, consumes
its request and creates no report directory. Evidence is in
`artifacts/ecosystem-next-20260910/c13/steward-evidence-review.json`.

Independent scheduling/export review found no concrete blocker in native ordering,
appended callbacks, error continuation, atlas flush ownership or explicit export
lifecycle. Independent profiler review identified the selection-dependent gap
above; earlier no-blocker statements below describe the orchestrator's own review,
not this acceptance decision. Preserve sufficient earlier evidence instead of
rerunning every case. The focused correction must establish reports on/display
off and Loading Progress coexistence on actual native execution.

Performance, combined release review and C14 focus/lifecycle qualification remain
pending. No unattended window or performance claim is created by these functional
checks; no Steam/Deck promotion, merge, push or publication is authorized.

## Functional handoff for steward review — 12 September 2026 UTC

Wake-Up now shows actual startup and later loading work and saves coherent sessions. Ordinary settings provide report selection/save, explicit next-launch XML export and guidance. Optional collection remains off by default. This is a **handoff for independent functional review**, not self-acceptance, a performance claim or release readiness.

The assignment began clean at `71014127b77174d801d620545e5c40e16ac8b88c` on `codex/ecosystem-next-plan-review`, preserving approved plan and implementation ancestry. Final tested source/core/observer is `87204ef65d6f525ae66c526212a209663a2bc0e9`; the later handoff commit changes records only. No merge, push or publication occurred.

## Product behavior

The display and optional report use one bounded session. Recording begins before the first mod constructor and follows actual execution inside native methods and deferred actions. Exact native operation labels and the same thread's active scope supply stage/package context; arbitrary type/mod names do not imply audio, world, map or patch work. Unattributed work remains shared/unknown. Earlier bootstrap/assembly/core work is explicitly unobserved. Known constructor/callback/atlas totals are shown; other work is indeterminate.

The existing C07 callback owner progresses through safe main-thread units within an 8 ms frame budget. Constructors stay on their native thread and run once, in native order. Native callbacks, appended actions, error continuation, actual atlas flush helpers and cleanup retain their ordering. Unsupported entry contracts use ordinary indivisible execution. No sleep, constructor worker or duplicated callbacks are introduced; one indivisible call may still block drawing.

The detailed startup panel becomes a compact later-loading strip, preserving native tips and mod information. At the tested 720 effective UI pixels it ends at 62; the native summary group begins at 70. Hiding the summary changes the native condition used by both layout and drawing, re-centering the remaining content. Native acknowledgement, errors and other mod-information UI remain. Loading Progress keeps display ownership when present; Wake-Up declines both display and summary hiding.

Reports distinguish time including nested work (inclusive) from time excluding observed same-thread nested intervals (exclusive). Durations include waits/yielded frames and are not CPU execution time. Threads overlap; neither sum equals whole startup/menu time. Errors, unfinished scopes, missed regions and omitted detail remain explicit. Counts describe observations, not unique incidents or proof every asset succeeded.

History retains eight sessions, with 512 detailed spans per stage within 32,768 total rows. Large XML work cannot crowd out later phases; aggregates continue after detail retention fills. Scope depth, threads, packages, counters and error text also have explicit bounds. Reports retain eight generated files / 128 MiB. The UI distinguishes session IDs, refreshes snapshots and saves text; layout is cached and only visible report lines draw.

Requested XML contains actual combined-before-patches and processed-before-inheritance documents, already loaded source trees, origins and ordered mods. Origins do not claim ownership of every later patch. Requests are consumed once and close through the native callback queue independently of display/profiling. Missing stages cannot remain armed for an unrelated reload. Exports retain three launches / 256 MiB, with a 96 MiB per-file limit and incomplete receipts. Ordinary startup performs no bulk XML export.

Guidance preserves existing maintenance/preparation controls and warns against removing unreplaced supplier gameplay/assets/workflows. Existing preparation behavior is retained, not newly accepted by C13. Native lazy artwork access is preserved. C10/C11 assets and C14 background lifecycle remain unfinished requirements; no deferred-asset progress is invented.

## Evidence and identities

Raw records are under `artifacts/ecosystem-next-20260910/c13/`; captures are under `.rlo-test-instance/results/<label>/`. `capture-identities.json` records exact source, package files, settings, load orders, report hashes, controls and gameplay. Final packages are `artifacts/fixture-package/87204ef65d6f525ae66c526212a209663a2bc0e9` and the same revision under `artifacts/fixture-menu-observer/`.

- Core DLL SHA-256: `8f293c5f44b9a71e72f6e14dfc24b219d36f9843ae6697de4148a6bc2411dc11`.
- Observer DLL SHA-256: `ac31faa6d05ed688056fc24c715c77818e2a0b6c5f1a721a5d2fc0875f25685b`.
- Generation: `gog-rev573-20260910-194017-cf1a1eb0`.
- Manifest: `b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
- Game assembly: `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.

| Functional run | Source | Result |
| --- | --- | --- |
| `c13-startup-ui-01` | `9162630` | Actual 1280×720 startup/report and early constructors. Save/request/cancel handlers and native lazy preview proof. |
| `c13-later-ui-02` | `9162630` | Actual 1920×1080 at 1.5 scale; world/map creation and paused/unpaused save reload; explicit XML. Revealed panel overlap and misleading substring stages, corrected below. |
| `c13-final-shown-03` | `87204ef` | Corrected 1280×720 startup/later screens with native tip and summary unobscured; four sessions and successful create/save/reload. |
| `c13-final-hidden-04` | `87204ef` | Corrected 1920×1080 at 1.5 scale; summary actually hidden and layout re-centered during later loading; four sessions and successful create/save/reload. |
| `c13-final-mixed-05` | `87204ef` | French CE/VEF/XmlExtensions workload with earlier XML/language features selected; third-party constructors, controls, lazy previews and native-output comparison. |
| `c13-final-loading-progress-06` | `87204ef` | Loading Progress keeps its actual screen; Wake-Up display/summary hiding refuse clearly. Report and explicit controls still pass. |
| `c13-final-export-off-07` | `87204ef` | Display/timings off; explicit request consumed, both XML stages and sources exported, no timing report created. |

All seven exit normally with code 0 and `automaticTestPassed=true`. The six selected rendered probes also complete without probe failure. Framebuffers are actual in-game rendering, not mockups or OS screenshots. Control receipts call the same handlers as ordinary settings; they do **not** claim pointer-click testing. No desktop input was synthesized.

Visible-summary proof is `c13-final-shown-03/profile/FixtureMenuObserver/c13-loading/25-loading-25.png`; hidden/reclaimed proof is `c13-final-hidden-04/profile/FixtureMenuObserver/c13-loading/23-loading-23.png`. Loading Progress proof is `c13-final-loading-progress-06/profile/FixtureMenuObserver/c13-loading/10-loading-10.png`. Report windows are each probe's `*-report-window.png`. Native preview-loaded flags remain unchanged while the report renders. Requesting one native preview changes only that selected flag; its second getter returns the same object.

Mixed output equals retained native `c12-mixed-native-01`: all 143,932 XML observer rows are byte-identical (18,625 conversions, 114,629 reference resolutions, zero observer errors), as are translation samples. Full language JSON is equal after excluding exactly the two previously established empty `C12AttributeProbeDef` diagnostic rows, one in active/default language. No payload or other difference is excluded. See `mixed-native-comparison.json`.

Final display-off XML parses successfully: each stage has 13,809 root children; sources has 1,556 records. All three files are byte-identical to the earlier requested export. The request is absent and no LoadingReports directory exists. See `export-off-07-inspection.json`.

Reports preserve limits explicitly. Mixed startup retains 5,094 detailed spans, reports 295,128 omitted detail rows with aggregates preserved, and records one unfinished outer native callback marker. New-world sessions record the fixture's initial-random-seed error and two unfinished native world markers; both later reload sessions record zero errors/unfinished scopes. These are not relabelled completed measurements. Independent gameplay receipts verify three colonists, map identity, save/reload and native paused/unpaused behavior.

Earlier checks passed 133 managed / 71 fixture tests. Final focused checks pass 68 managed / 71 fixture tests (`test-08.log`), including exact classifiers, same-thread context, bounded reports, callback ordering and independent export completion. Both matching builds have zero warnings/errors. Independent read-only source review found no concrete blocker at both revisions; its scope is in `independent-review.md`. Steward functional review remains separate.

## Restoration, remaining limits and ownership

All eleven C13 transactions were rolled back in reverse order. `rollback.json`, `final-endpoints.json` and `final-audit.json` verify seven original hashes and five absences. Original core `ba17b03`, observer `3d86b16`, GOG generation, profile/preferences, mod order and exclusions are restored. Loading Progress and MissileGirl remain at their original endpoints. No RimWorld process remains. Child work, builds, tests and fixture operations are stopped. Exclusive ownership returns to steward `01a08d63-a8ef-7c40-8cd9-bbf9f512ce22`; corrections return to this same C13 owner. No successor or parent-state/timer edit occurred.

Every run was functional, muted and requested `minimized-noactivate`. This does not prove subsequent OS foreground ownership. Unity reported different focus flags; neither alone proves focus theft or non-disruption. Existing evidence has no OS foreground receipt. This uncertainty was reported to the steward; no window was restored/activated or owner-interruption evidence received. No normal data or other application was modified. Coordinated focus/lifecycle qualification remains with C14.

No performance runs, microbenchmarks, overhead experiments, Steam promotion, Deck/Linux access, merge, push or publication occurred. Functional durations are diagnostic only. Display/profiler overhead, matched enabled/disabled measurements, broader responsiveness and combined GOG visual/correctness/performance acceptance remain pending, with measurements requiring a new explicit unattended window. Earlier unobserved engine work, indivisible calls, unsupported hooks and incomplete markers remain explicit. C10/C11/C14/C15 requirements and the approved return to the existing C10 orchestrator remain unchanged.
