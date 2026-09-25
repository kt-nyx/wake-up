# Compatibility Phase 1

Wake-Up should keep independently safe work available alongside other loaders,
and explain when a requested operation cannot run. This authorized campaign
starts from restored private main `82350dcc77cf1c01ab044651e599f804c77338fa`.
The latest actual release remains **0.3.0**. These changes are unpublished;
source availability, compilation and offline tests do not qualify a game run or
prove a speed improvement.

## Foundation slice

The foundation separates code type-name lookup from the shared lifetime used by
leaf searches and loading reflection. Each operation keeps its own method,
callback and thread checks. XML query helpers can run when native patch workers
are occupied or definition-worker searches are deselected. They still need the
actual native patch stage and its mutable document; a supplier bypassing that
stage leaves the helpers available but unused. Individual failed worker setup
rolls back only Wake-Up's patch on that worker. Shared scope failures also roll
back Wake-Up's dependent helpers. Foreign patches are never removed.

A small operation registry records requested choices, actual availability,
provider and reason. Individual reflection overloads and XML workers retain
separate identities. Existing initial refusals and late guard losses feed the
same record. Missing optional mods, ordinary cache misses and bounded per-file
fallbacks do not mean a compatibility loss. Selection remains independent of
saved settings; the registry never rewrites preferences.

New compatibility losses persist before notification to
`<profile>/WakeUp/Compatibility/notices.xml`, outside evictable cache categories.
The dedicated aggregate window is independent of the optional loading display
and reports. It acknowledges only the snapshot whose entries actually rendered;
late events remain separate. Closing the window defers it for the session without
acknowledging it. A failed disk write leaves the notice pending and must not trap
the player in a modal window. Settings expose current effective status; logs
record each launch and state transitions. Semantic notice keys exclude DLL
hashes, build versions, timestamps and unrelated setting changes. A history of
4,096 acknowledged decisions is retained; very old evicted decisions can repeat.

The registry can be initialized before the first Mod constructor through the
early observation path. Supplier policy must be consulted there before observers
are attached, not only inside WakeUpMod. The supplier slice below implements
early WOWGAG selection. No Prepatcher rewrite changed in the foundation: retained and
retired early bridge activation still exists. A future touched rewrite must
prevalidate all changes or restore its own mutations; Prepatcher false/throw is
not rollback. No broad retired-code cleanup is assigned.

When nothing is requested and no maintenance is queued, runtime setup avoids
cache inventory and feature initializers after recording selection and installing
the independent notice delivery path. This is not zero overhead: early bridges
and the minimal settings/status setup remain.

## Supplier implementation

Wake-Up now lets the other loader keep the operations it owns while retaining
independent work. The settings page has separate XML and content choices:
Automatic, Wake-Up and WOWGAG. Automatic preserves attached WOWGAG operations.
Choosing Wake-Up requires disabling the corresponding setting in WOWGAG and
restarting; Wake-Up never changes foreign settings or removes foreign patches.
An acknowledged automatic decision does not hide a later action-required notice.

Before mod constructors, the policy reserves potentially conflicting content
observation using bounded, read-only settings/default checks. After constructors,
it inspects the actual installed hooks and freezes the choice before XML loading.
The existing pre-load bridge supplies the normal boundary. If that bridge is
unavailable, a one-shot prefix removes itself before WOWGAG checks the hook list.
That fallback discovers Gagarin directly; streaming XML reports its missing scope
boundary instead of pretending a newly installed prefix joined the current call.
Unknown or partial installations yield only affected operations and are described
as unqualified, not as successfully active suppliers.

Full WOWGAG XML selection omits the definition/query/replay hooks, XML-input scope,
optional Gagarin hooks and XML-file observation that its eligibility checks reject.
Patch-object observation and other native markers remain available. Content
selection omits the native texture bridge and only the content-reload observer.
Disabled or excluded preload is not inferred active from a package or an
`Installed` flag. Each omitted observation has an effective-status entry.

FGL original and stable keep their original single-result query expressions and
contexts; multi-result workers remain independent. The inspected Preview adapter
permits single-result redirection only while its actual thread-local patch scope
is active. It verifies the loaded callback bodies, their hook publications and
scope field; late changes fall back to the original query. Single-query memo and
completed-XML replay retain their existing refusals. Type-name/leaf refusals do
not disable independently admitted reflection helpers.

YaOpt keeps its eight changed XML workers and top-level asset lookup, including
texture completion. Wake-Up retains unmodified workers, leaf searches and other
independently admitted helpers. Native bundle lookup now has separate runtime
and early-rewrite admission. Either rewrite can succeed independently; failed
injection restores its own changes. Both rewrite orders preserve the supplier's
top-level wrapper. Native thread, body, callback, mod-order, bundle and mutation
checks remain. No image, background-loading or foreign-cache adapter was added.

Loading Progress display-only requests now report observation/invocation
unavailability instead of remaining pending; selecting reports still retains
observation. The legacy-package warning excludes the package owning the current
Wake-Up assembly, while duplicate runtime and external legacy warnings remain.

Release compilation against admitted Steam rev590/Prepatcher references passed.
Focused offline tests passed **44/44**, with no skipped cases in that selection.
Independent read-only review checked the fixes and found no actionable defect.
They cover provider settings/defaults, notice transitions, independent search
admission, actual hook-publication guards, Preview thread scope/late changes,
the fallback execution boundary, partial installation, both rewrite orders and
rollback. Generic game detouring is not supported on this desktop CLR: that guard
test uses real published Harmony records, not a claimed Mono execution test.
The existing game-copy reflection-load test was excluded; rewrite tests use Cecil
to inspect files without loading another game assembly. Initial test setup/host
failures and their resolutions are recorded with raw evidence under
`artifacts/compatibility-campaign-20260923/phase-1/suppliers/`.

## Focused GOG correctness

The requested startup and compatibility checks passed on the isolated GOG
1.6.4871 rev573 installation. This establishes the recorded behavior in the small
Core-based collections; it does not establish whole-mod gameplay safety or speed.
The steward reviewed the report, real framebuffers, corrected supplier receipts,
body-binding changes and restored audit, and accepted this bounded scope on
24 September 2026. No further tests or launches are assigned.

The game rendered its real warning window and framebuffer. The private observer
waited for genuine repaint events, scrolled existing notice lists when necessary,
and invoked the same acknowledgement handler used by the button only after all
entries rendered. It did not synthesize mouse input. Native close deferred four
FGL notices; the next launch showed and acknowledged them. A later restart with
an unrelated setting changed showed none. Continued and YaOpt naturally required
two pages, proving acknowledgement waited for the full list. The final WOWGAG
automatic run acknowledged nine decisions; carrying that actual captured ledger
into explicit Wake-Up selection produced nine new action-required decisions.

| Collection | Observed result |
| --- | --- |
| Default settings, optional suppliers absent | Menu and exit succeeded without warning noise. |
| FGL original / Continued | Conflicting type/leaf and Test/Conditional work yielded; independent reflection and XML work remained. Original's two texture mip warnings also occurred with Wake-Up absent. |
| FGL Preview + Loading Progress | All 11 XML workers, including Test/Conditional, admitted; the private patch workload produced its expected final definition. Repaint coalescing ran; the conflicting PNG loading bridge refused separately. |
| YaOpt | Its eight XML workers retained ownership; Wake-Up's other three workers and independent helpers remained. The XML result was correct. Bundle lookup was available but not exercised by a native bundle workload. |
| WOWGAG automatic / explicit Wake-Up | Complete XML/content ownership was verified; overlapping Wake-Up hooks were omitted. Explicit choice required changing WOWGAG's own setting and restarting, without removing its hooks. Both runs produced correct XML. |
| Loading Progress alone | Repaint coalescing suppressed 101 requests; 12 image reloads completed through its integration without errors. No PNG decode/cache call occurred. |
| Missile Girl | Gagarin adapter installed and its observed stage completed without failure. XML result was correct; unsupported streaming/completed XML paths refused. Warm parsed-cache reuse was not tested. |

Supplier recognition needed live fixes because Prepatcher loads rewritten assemblies
from memory and may reorder metadata identifiers. Wake-Up now finds the unique,
hash-qualified source owned by the loaded mod and compares callback instructions
using resolved member identities, preserving branches, local variables, exception
regions and volatile field modifiers. The observed unsigned Steam-game reference
may bind only to the actual running game assembly; Harmony 2.4.1/2.4.2 references
may bind only to the active unsigned Harmony 2.4.2 assembly. Other assembly identities
remain exact. Changed callback behavior remains refused.

The optional streaming-XML plus completed-XML combination still has its existing
conservative refusal; this task did not expand that contract. Popup wording now
explains supplier decisions without suggesting that retained supplier work is vanilla
or that the supplier implements Wake-Up's omitted observation features. Exact
diagnostics and unchanged semantic notice keys remain in settings/logs.

Corrected WOWGAG and Preview runs used product and observer source
`3dc5a67408b07ddf97b99473798a5ea1c0b48160`. The final wording-only product commit
`a5b85683d275a288903ded515001e742e82b6b8a` was exercised by Loading Progress and
Missile Girl with that same observer. Focused managed checks passed 40/40; fixture
tests passed 74/74. Final builds had zero warnings/errors. Independent final review
found no blocker. Earlier failed/superseded diagnostic runs remain recorded:
the initial observer ordering failure required recovery closure and is not a
successful sample; a strict no-warning XML run correctly failed when it discovered
the existing own-option refusal. Later runs corrected the observation assumptions.

Restoration used the supported source-list refresh and passed full content audit.
Generation `20260924-163135-9fa2ba28` contains only Core, Prepatcher and Harmony;
manifest SHA-256 is
`061fe5d1d2a53e115f91687d4a18e2fd01f971802b1817802a88b9a15cc95ddd`.
The profile contains only generated ModsConfig; no product/observer is deployed
and no fixture process remains. The permanent game assembly retains the hash
listed below. A normal Steam game was observed and left untouched.

Raw evidence, the full run identity index and final report are private under
`artifacts/compatibility-campaign-20260923/phase-1/functional/`. Thirteen bounded
evidence runs were accepted from 26 recorded runs, including the absent control;
the other records are failed or superseded diagnostics. Every accepted run reached
the independently observed menu and exited normally with code zero. Compatibility
probe runs also passed their separate receipt. No performance comparison, Steam
or Linux/Deck qualification, gameplay claim or publication is made.

## Fixture readiness

This section records the initial readiness handoff before the correctness work above.
At that handoff the isolated game was ready for package deployment, with no game launch yet.
On 24 September the owner's reinstalled GOG 1.6.4871 rev573 matched the existing
admitted game assembly SHA-256
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Its installation was preserved. The baseline contains only Core, Prepatcher and
Harmony, with a generated ModsConfig and no imported user settings or saves.
Core alone satisfies the selected suppliers' mandatory game-content dependencies;
owned DLC remains installed and unchanged but is not enabled in these plans.

`scripts/fixture.py initialize/refresh --sources` now accepts an explicit private
source list; ordinary discovery remains unchanged. The same copy/hash verification,
fixed GOG admission, path/link checks, process guard and reversible transaction
apply. Duplicate package IDs remain rejected. FGL original, Continued and Preview
have separate lists and are selected by explicit refresh, never installed together.
Additional lists select acquired YaOpt 1.1.4, WOWGAG, Loading Progress 0.16.0 and
Missile Girl individually, with three combined lists retained for later authorized
coexistence checks. Those lists are prepared inputs, not tested combinations.

The initialized generation is `20260924-150349-55556722`, manifest SHA-256
`9ca9ecbea112f6d6859d8906eedf38abd1a263b5ddda9ce8e6b1d59b2ffe6722`.
Its two copied packages contain 37 files / 12,364,450 bytes. Full content audit
passed. Offline fixture tests passed **78/78**, including five new focused checks
covering clean initialization, edition refresh, duplicate/path/identity refusal
and changed-source rejection. These tests use synthetic files and simulated
processes, not the installed game. Independent read-only selection/path review
found no actionable defect.

Readiness is limited to initialization: no product build, observer build, deployment,
prepared launch or live correctness evidence was produced. Refresh changes the
generation and clears deployment receipts, so select the collection before
deploying the product/observer and preparing a `--purpose functional` run. The
existing `--selection` cannot add suppliers omitted from that generation's active
order. Performance work and publication remain unauthorized.

Private source lists, supplier identities, audit output and handoff instructions
are under `artifacts/compatibility-campaign-20260923/phase-1/fixture/`.

## Foundation verification and evidence

Focused offline checks cover warning persistence/restart, acknowledgement/write
failure, semantic deduplication, late decisions, independent query admission,
existing leaf/reflection behavior and exact background-hook cooperation.
Release compilation against admitted Steam rev590/Prepatcher references passed
with zero warnings/errors. The focused .NET Framework suite passed **153/153**;
separate .NET 8 reflection/translation cooperation tests passed **37/37**. These
are offline host results, not Unity or GOG execution. XML Extensions tests use
an explicit read-only supplier path; an old test's iterator inspection was
corrected from its non-iterator wrapper to the existing iterator body.

Independent read-only review identified and checked fixes for overload identity,
late refusal reporting, child rollback status, aggregate dismissal on disk
failure, baseline/export selection and shared observation dependencies. Final
review found no remaining blocker, and steward acceptance is complete. The later live
window/acknowledgement results are recorded above. In the foundation slice, no installed files, fixture
admission, saves, profiles or game caches were changed; no game or performance
run and no publication occurred.

Raw logs and review notes belong in ignored
`artifacts/compatibility-campaign-20260923/phase-1/foundation/`.
