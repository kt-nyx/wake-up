# C15A retained private candidate — 21 September 2026

## Current Windows candidate — 22 September 2026

The [Windows qualification report](windows-steam-qualification-20260922.md)
supersedes the stop-before-Steam state below. It records the compatibility fix,
bounded Steam functional/UI/performance evidence, same-binary GOG regression,
restoration and refreshed private archive. The parent independently accepted the
bounded Windows qualification at `66b23eb2`; Linux/Deck, global release readiness
and publication remain separate decisions.

## Current atlas-retirement candidate

The [atlas-retirement report](c15a-atlas-retirement.md) supersedes the package and
pending-atlas decision below. Owner-approved removal of atlas cache/batching
activation and options preserves shared display scheduling. This is a new runtime:
prior core `90844467` UI/performance proof is applicable history, not an exact test
of this binary. New build/package and focused validation are in that report.
Steam was explicitly deferred at this historical closeout; the 22 September grant
and report above supersede that boundary.

## Historical private closeout before atlas retirement

## Current qualification and private closeout

This is a bounded GOG-qualified private candidate, not a release-ready or complete
replacement for supplier mods. The parent accepted combined correctness, actual
agent-operated quality/menu/focus UI and final workload-specific measurements before
this closeout, at base `7cf52c4296b3cfffc7963cf6c0b643cf19c94c8a`. Assignment
`wake-up-c15a-private-closeout-20260921-7cf52c42` changes only metadata/docs/private
packaging, including a narrowly checked description-only exception in the reused-build
packager. No launch, measurement, runtime/default/scope change or platform promotion
occurs. Atlas release treatment remains pending; activation/defaults are preserved.

Combined correctness belongs to core `4da2e2df`; later source changes are preparation
wording and the settings scrolling correction. Tested core `90844467` then passed
three normal UI launches: native display, six half-dimension textures in two groups,
and native reset with actual stores carried forward. Menu loading and focus return
were usable. This is agent observation, not owner inspection or universal gameplay
compatibility. Prior unfocused hold/portal proof retains its separate identity.
Known InputLegacyModule logging remains. See the [accepted UI report](c15a-overnight-ui.md).

The [accepted performance report](retained-performance-20260921.md) records 60 counted
measurements, six warmups and one separate functional diagnostic. Small English
ordinary defaults tie native/public 0.2.1. Conditional startup gains are **3.23%** for
the complete type/reflection group, **14.46%** for French translation application,
and **16.20%** for PNG-selected warm reuse. PNG construction adds **12.24 seconds**
and **333.5 MB**, repaid after roughly **four subsequent warm launches** with stable
matching inputs. No individual subfeature attribution follows from a grouped test.

Atlas caching/batching lose; routing ties and XML-query extension benefit is
inconclusive. The final display-plus-observation/scheduling comparison, both atlas
options off, costs **0.914 seconds**: a progress-UI tradeoff, not a speed gain.
All-options remains slower, including the no-atlas comparison. These separate local
workload results are neither additive nor universal and are not cold-disk results.
No slower route is accepted as a beneficial conservative optimization.

## Package identity and remaining decisions

Private version remains `0.3.0-rc.2`, `releaseReady=false`, `steamQualified=false`,
`steamStartupValidated=false` and `linuxStartupValidated=false`. Ordinary-entry
qualification is true only within the recorded GOG scope. The new revision-specific
source-inclusive archive reuses these exact binaries, without rebuilding:

| Component | Exact identity |
| --- | --- |
| Tested core build | `908444675b1ddd33b818381fb67498eeb5faafc1` |
| Core DLL SHA-256 | `3f96a341d7c2a777fb26b1c724e53d7e711ec46edf58f17a016bd6555de5d3e1` |
| Qualified Windows helper SHA-256 | `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36` |
| Matching helper source | `83a2930f60967eba4fcfb8e6acef10f35a415c62` |

The refreshed private archive is
`artifacts/releases/0.3.0-rc.2-c944db784fcf6133887c03a6a80ba0d1b7b53ce0-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`.
ZIP SHA-256: `156efaf42b4820303fdef75a27a2131684718494afc1110eed1c7fad991ea79b`.
Archived source is `c944db784fcf6133887c03a6a80ba0d1b7b53ce0`; core build remains
the separately identified tested `90844467` above. The later documentation-only
closeout does not require another archive or build.

Receipts are under
`artifacts/ecosystem-next-20260910/overnight-20260921/private-closeout/`.
Licenses, notices and matching source are included. Installed
runtime payload contains only the core and optional quality helper; observer, trial,
bootstrap and companion executables are excluded. Tracked development source may
include their historical code; that does not install those components.

JSON/XML checks pass; all ten focused package tests pass, including the new
description-only reuse acceptance and dependency-change rejection. The original
packager refused `release/About.xml` even for prose; the narrow correction compares
the remaining XML contract exactly and preserves all runtime/build guards. Actual
ZIP/manifest bytes, current release metadata, archived core source, exact two runtime
binaries and the historical helper source hashes match. Independent read-only review
found no remaining concrete issue after the Steam-plan admission prerequisite was
made explicit. Current fixture binding still refuses this new candidate; its future
hash-pinned update needs approval and was not made here.

No fixture transaction or live run occurred. Fresh checks still match all seven
baseline hashes, eleven absences and the supplier repair; the prior accepted metadata
audit remains applicable without a fixture mutation. No game/helper/build is running.
Return is clean and stopped for parent acceptance, which is not granted here.

Six previously retired acceleration paths remain native; C10/C11/companion remain
deferred. The proposed PNG pilot is excluded. Parent review of this closeout,
owner atlas release treatment, [Windows Steam approval](windows-steam-qualification-plan.md),
then separately approved Linux/Deck and publication remain outstanding. There is no
self-acceptance, successor dispatch, merge, push or publication. The prior UI archive
and dated entries below are historical; the new archive supersedes that private package.

## Historical candidate records

## Inspection correction — fresh label build, unchanged behavior

Assignment `wake-up-c15a-inspection-corrections-20260919-68073f6f` starts from clean
`68073f6f019b74946b1db73578666b33aae158a0`. The actual preparation-window label now
states native PNG/JPEG preparation, optional PSD decoding, authored DDS bypass and
separate explicit quality/export choices. The sole runtime-source difference is
that label. One fresh GOG core build at `978f2242637b87e12006541f8d6eb6c442e2acf1`
passed with zero warnings/errors; DLL SHA-256 is
`d6cc7b30f3429cfc6f706c6d6254538815fa03fcdb1e0ddc1e85d7f46ff6d3c6`.
The earlier combined/C14/C09 behavioral evidence retains its original binary/run
identities and bounds; this new DLL has not been live-tested. No game run or repeated
matrix is warranted by this text-only change.

The owner guide now has three stages using the same saved display: native inspection
and preparation; restart with the captured prepared store, inspect conversion and
reset; restart with the captured reset store and verify native appearance/choices.
Preparation/reset occur at the idle main menu. The exact six-path filter covers
brazier color/mask and four antenna directions, including required related sources;
the window has no per-row selection. Icon and ordinary floor remain native comparisons.
Both carry transitions use the same deployment/settings/save and refuse missing
store evidence. Native Options are explicitly rechecked where focus checks repeat.

Evidence lives in `artifacts/ecosystem-next-20260910/c15a-inspection-corrections-20260919/`.
The refreshed archive receipt is `package-helper.json`; qualified helper `ca12d392`
retains matching source `83a2930f`, not trial helper `9a404c52`. Exact archive
identities and stopped handback follow. Historical
failed setup08 and successful setup09 evidence are unchanged. No manual inspection,
performance/platform qualification or release acceptance is claimed.

Refreshed private archive:
`artifacts/releases/0.3.0-rc.2-35a17020cff03a9b3cc7f22e9b4fdf27638c1cda-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`.
ZIP SHA-256: `4730a9f50cf638ff91d610bf8a16f6262a778eb035c045a18e95013a220debbc`.
Archived source: `35a17020cff03a9b3cc7f22e9b4fdf27638c1cda`; core build:
`978f2242637b87e12006541f8d6eb6c442e2acf1`. Package content/hash checks pass and
only WakeUp.dll plus the qualified quality-helper executable are installed binaries.
The packager and its previously passing nine tests were unchanged; that test suite
was not repeated. The independent bounded guide review found two concrete issues
(idle-menu admission and lack of row toggles); both were fixed and rechecked.

No fixture deployment, preparation or launch occurred, so no new transaction required
rollback. Fresh checks confirm all seven baseline hashes, eleven absences and retained
supplier repair; metadata audit passes. Only the separate session-2 ASTER game remains,
untouched. Source is handed back clean and stopped for the parent's independent
acceptance, with no self-acceptance or successor. The earlier failed scene setup08
and all prior evidence remain intact; this correction does not replace them.

## Previous retained integration and inspection artifact

**Stopped final handoff:** the private archive and exact manual profile are verified,
all ten owned transactions are restored, seven baseline hashes/eleven absences and
the supplier repair match, and metadata audit succeeds. No fixture/helper/build/test
operation remains; the separate session-2 ASTER game is untouched. Ownership returns
to the parent steward for independent review, without self-acceptance or successor.

Previous private archive:
`artifacts/releases/0.3.0-rc.2-4185a1029f8c4636afba965d247485ee2229b412-texture-helper-win-x64/wake-up-0.3.0-rc.2.zip`.
SHA-256: `ee0bff336b23abff58c77dfcd95034bdcb867a4abbdcacaaf1ef386e4061f4bd`.
Archived source is `4185a1029f8c4636afba965d247485ee2229b412`; embedded core build
source remains `4da2e2df4709ff6a8229def22bbf8155b49b5cc5`; the separately included
quality-helper source is `83a2930f60967eba4fcfb8e6acef10f35a415c62`. Runtime payload
contains only WakeUp.dll and the pinned optional WakeUp.TextureHelper.exe, with
metadata, notices and matching source. The superseded trial-helper archive is not
the final candidate. No public version, upload or platform qualification changed.

`c15a-final-inspection-ready` prepared and verified the final core, pinned helper,
corrected observer and completed scene save together, then was rolled back without
launch. `package-and-inspection-verified.json` records their exact identities.
Reprepare an unused label using the [owner guide](c15a-owner-inspection.md) when the
owner chooses the time; no live fixture is being held for immediate participation.
Remaining gates are actual C08 windows/appearance/reset/restart and C14 focus-return/
ordinary menu-load visibility, current performance qualification under a new explicit
unattended grant, and separately authorized Steam then Deck/Linux and publication.

The final C14-corrected core passes one combined retained startup check. The owner
inspection now has a normally saved/reloaded display colony, so no owner scene
construction is needed. No performance measurement or visual acceptance follows.
Assignment `wake-up-c15a-final-integration-20260919-3e6671db` began at clean
`3e6671db3a205a0d80f7f7deb8165acec3c13b09`, in the same checkout/branch under
exclusive C15A ownership. All runtime/build inputs matched the reviewed core
`4da2e2df4709ff6a8229def22bbf8155b49b5cc5`; it was reused without rebuilding.

`c15a-final-combined-07` used that exact core/observer, the existing ten-package
selection, French, retained settings together and stale retired flags, with the
existing image, XML/language and atlas probes. A fresh store was chosen to exercise
native construction and shared startup activation after the C14 change without
carrying caches from an older core. It exited normally with `automaticTestPassed`
and all 58 image samples passed. It created 46 retained image records with zero
image/cache errors, passed eight atlas checks, kept 2,861 DDS loads native and
reported zero raw-helper starts/requests. Routing native-winner smoke and loading
display execution passed; background support was installed. The full XML and French
files match the earlier/native control hashes exactly. Processed XML retains its
73-operation safe prefix; the unsupported custom suffix stays native.

The current small-selection C09 broad probe still reports false after 99 checks
because no real shader bundle is present. The accepted separate 140-check shader
lifetime evidence remains applicable; it was not rerun or relabeled. The three
French content diagnostics remain identical to the absent-Wake-Up native control.
The C14 hold/portal and ordinary native API-entry evidence belongs to its existing
accepted runs, not this menu startup. Known InputLegacyModule logging is not repaired.

### Saved inspection display

The steward additionally authorized one small native setup in the copied C08 save.
Observer-only `inspection-scene` uses existing gameplay/save/reload machinery; it
requires functional purpose, copied-save gameplay and ordinary native logging.
No general scene builder, custom artwork, UI automation or product change was added.
Setup08 exited normally but failed artifact checks: it required an unnecessarily
empty 21×15 region. That failure is preserved. Corrected footprint-only admission
avoids pawns/items/existing edifices and passed `c15a-owner-scene-setup-09`:
normal exit, automatic/gameplay/quality-artifact checks all true. Native reload
preserved one brazier, antenna rotations 0/1/2/3 and 16 ordinary straw floor cells
near (33,38). The original save hash is unchanged.

Saved display `RloFixtureSmoke.rws` is 1,908,603 bytes, SHA-256
`e949173b778500b82bd8dfb65621b830ccb936cd013ad74e5396cdf33440f3e6`.
Setup observer is `0412c65dd650332f60231dfbd46ec08781e22f58`, SHA-256
`2dfee0d628cdcba506e03fbc164eecf31d7a014656bdfc79e5c83a4f76f56a27`.
Core remains SHA-256 `b826380f2be6b69558d2093c7f855923d7c823788eb2abe03c0b7fb94a614bba`.
The [current owner guide](c15a-owner-inspection.md) uses one selection/save for C08
windows/appearance/reset and C14 actual focus-return/ordinary menu-load inspection.
No manual run has been launched; owner inspection is scheduled separately.

### Private source-inclusive packaging

The archive reuses the tested core and optional qualified Windows quality helper
(`ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`), with its
matching source/notices. No raw helper, companion, observer binary or bootstrap is
installed. The public version is unchanged and release/platform flags remain false.
Current qualification metadata replaces the stale offline-only/deferred-audio claim.
The source archive does contain tracked development/observer material, as documented.

A narrow `--reuse-build` path records exact build and archived-source revisions
separately. It permits current docs/qualification/packaging and observer-source
changes, plus fixture preparation/verification/helper-deployment/CLI changes only when the remaining
syntax tree (including build logic/constants) is identical. Runtime/build/reference
input changes remain refused. All nine focused package tests pass. Independent
read-only review found no blocking package-boundary or copied-scene issue; this is
not independent final acceptance. The preserved C08 helper's original source archive, notices and executable
also verify without a rebuild. Its matching source is included separately at
`Source/TextureHelper/source.zip`; current experimental helper source remains only
in the general tracked development archive. An earlier local package containing
the trial helper was superseded before deployment; its receipt remains as
`package-helper-superseded-trial.json`, not the inspection/release candidate.

Evidence and final package/restoration receipts are under
`artifacts/ecosystem-next-20260910/c15a-final-20260919/`. Earlier sections below retain
their own source/run identities; pending-scene language there is superseded by the
saved display, not by an appearance pass. Retained/deferred/retired scope is unchanged.

The candidate now exposes the retained features and leaves the six owner-retired
operations to the game. Deferred demand features and their automatic bootstrap
hooks cannot start from old settings. Five bounded combined GOG runs now complete
normally, including fresh, warm and changed-source image checks. The limits below
remain; this is not full C14 acceptance, a measured speedup or permission to remove suppliers.

## Native French control — 19 September 2026

The three French translation diagnostics also occur with Wake-Up absent. The
complete native language and downstream XML observer files are byte-identical to
the candidate's natural `c15a-live-fresh-02` capture. This classifies the three
missing Blackboard/SchoolDesk handles as pre-existing content issues in this
selection, not a Wake-Up regression. No product correction or second control is needed.

Assignment `wake-up-c15a-french-control-20260919-b51160d9` resumed from clean
`b51160d935715a70872b06efcdfad8e1fe367d98`. One control,
`c15a-french-native-06`, used `--mode absent`: fixture.py parked Wake-Up outside
the game Mods folder and omitted it from the active list. The original ten-package
selection/order, GOG generation, content manifest, French language and ordinary
assets matched fresh02. No staged image sources were selected. The independent
observer was the exact fresh02 `40451d9a` binary, SHA-256
`639c6b2a4c586c58f081c60755027d9fdf167a1eeee20acdd0f9450060fa517c`.
Native absence has no Wake-Up settings or atlas feature probe; it retains the same
language/XML observers. Matching content, order excluding Wake-Up, package/binary
and profile language/background preferences were verified before launch.

The control reached the menu and exited normally: `exitCode: 0`,
`automaticTestPassed: true`. Its complete 45,933-byte language file matches SHA-256
`1884af8d71e34e4bc896730c929b7c2c38d090fef0dccf509d57732780336910`;
the complete 23,064,687-byte XML observer file matches
`3a3c5465f1c35b55f29e04317dd5cfc0dbe25369fa0960121840597876b53c27`.
Native `anyError` remains true with the identical three diagnostics. This proves
native equivalence for the observed output, not that the content is error-free.
Durations are diagnostic only; no performance comparison was made.

Both owned transactions were restored: preparation `20260919-145542-b8177f38`
and observer deployment `20260919-145525-68fe3b16`. Seven baseline hashes, eleven
absences and the retained supplier repair match; metadata audit succeeds. No
fixture/helper/build/test operation remains. A normal inaccessible RimWorld process
in Windows session 2 remains untouched under the owner-approved ASTER exception.
Evidence: `artifacts/ecosystem-next-20260910/c15a-french-control-20260919/`, including
`matching-prelaunch.json`, `comparison.json`, `restoration.json`, `restored-audit.json`
and `handoff.json`. Ownership returns stopped for independent steward review.

The small combined selection's C09 broader probe remains `passed: false` for its
missing shader input. Separate accepted `c09-lifecycle-combined-04` evidence at
`8aeaa31a9640d449eb41631d6a8cf25681ad941a` passes 140 checks with real haze and
hazefullscreen shader unload(false), reload and borrowed-object lifetime checks.
The steward's independent review verified that shader routing, guards and ownership
are unchanged in the current core; that existing evidence remains applicable.
No redundant shader workload was run and the current false result is not relabeled.
C08 visual/window and C14 actual focus/hold/scene/traversal obligations remain open.
No rebuild, runtime change, performance work or platform/publication action occurred.

## Combined functional results — 19 September 2026

The retained candidate can build and reuse the tested image records, reject changed
content, and preserve the observed XML and translation results across those starts.
All five runs reached the menu and exited normally with `exitCode: 0` and
`automaticTestPassed: true`. This checks the bounded selection, not every modlist,
ordinary gameplay rendering or release readiness. No performance test ran; recorded
durations are diagnostic only and must not be compared as evidence of a speedup.

The owner resolved the possible foreground interruption and separately confirmed
that the inaccessible normal game belonged to another ASTER workstation. Resumption
`wake-up-c15a-aster-resume-20260919-5534009b` began at clean
`5534009b1e44a8a9a2eb2934d50d533a8e76d672`. The same C15A task retained exclusive
checkout and fixture ownership. No normal game, desktop focus or security state was changed.

| Captured label | Checked result |
|---|---|
| `c15a-live-default-01` | Ordinary default settings, English, normal menu/exit. |
| `c15a-live-fresh-02` | Retained options together, French, stale retired settings; 2,853 authored DDS loads stayed native, with zero image-cache calls/writes. |
| `c15a-live-images-fresh-03` | Existing private PNG/JPEG/PSD image probe added because the natural selection contained DDS only. Created 46 retained records; all 58 image samples passed. |
| `c15a-live-images-warm-04` | Reused all 46 records, no image decode calls or writes; all 58 samples passed. Processed XML replay skipped 73 captured top-level operations; two actual atlas groups restored. |
| `c15a-live-images-change-05` | Reused 44 unchanged records and rebuilt two changed records. All 58 samples passed, including new content with unchanged source size/timestamp. |

All runs used ordinary user activation and the existing ten-package C08 selection,
plus candidate and independent observer. The last three used the existing functional
texture-storage probe; only the final run selected texture-source-change. Warm04
restored the newly captured prepared-texture, processed-XML and atlas stores from
fresh03; change05 restored those stores from warm04. No retired cache was carried.
The source-change probe changes one private PSD and one private PNG. The recorded
PSD remained 104 bytes at ticks `639246816000000000`, while its SHA-256 changed from
`e5b40bcd9d404135a0df90a0c8c775697a089ae55a4f1ea4a1acde93efe199dc` to
`e6cee69549cb710f1713abbc8dfad7d35b2daff267f9d58f5a88025a6f6c6da5`.
The generic miss counter includes repeated lookup attempts (six); the prepared-record
counter reports the two stale records. Neither is a failed image comparison.

For fresh03/warm04/change05, the complete downstream XML observer file is identical
(SHA-256 `3a3c5465f1c35b55f29e04317dd5cfc0dbe25369fa0960121840597876b53c27`):
13,987 converted definitions, 80,282 resolution calls, zero observer errors.
The complete language observer file is also identical
(`1884af8d71e34e4bc896730c929b7c2c38d090fef0dccf509d57732780336910`).
Image/cache errors remain zero; 2,861 DDS startup loads stay native in each staged
image run. The raw-pixel helper remains unselected with zero child starts/requests.
Post-menu probes directly exercise preserved DDS research storage; those records
must not be mistaken for automatic authored-DDS caching during startup.

### Bounded limitations and next inspection

- French reports the same three unresolved injection handles in `Verse.ThingDef`:
  Blackboard's `CompInspectStringBlackboard.inspectString`, and SchoolDesk's
  `CompStatEntrySchoolDesk.reportText` and `.statLabel`. `anyError` is true;
  the later native French control above reproduces the complete output exactly,
  classifying them as pre-existing content diagnostics. The content remains imperfect.
- Processed XML stops at Vanilla Textures Expanded's unsupported custom operation
  and leaves that suffix native. Its warm prefix and final outputs are verified;
  complete supplier replacement is not claimed.
- Routing smoke preserves the native winner. The broader C09 probe reaches 89
  checks in fresh02 and 99 in each staged image run, but reports `passed: false`
  because the small selection has no real single
  native shader bundle for its lifecycle check. This is a coverage gap, not a full
  C09 pass or proof of a routing defect. C12 native search and display layout smoke
  pass their recorded checks; display execution is not owner visual approval.
- C08 appearance/windows and C14 actual unfocused loading, untouched hold, focus
  return, ordinary scene and traversal obligations below remain open. No new
  optimization pilot, performance qualification or platform promotion follows.

### Tooling, exact packages and restoration

Commit `9b2d10d5583e7410635d579003e72caafe8808d9` adds the explicit
`--allow-other-session-game` opt-in for authorized functional work. A fresh query
must show an inaccessible game in a different nonzero Windows session. Known
fixture paths and recorded fixture PIDs still block, and performance/legacy launches
still require all games stopped. No permanent PID exemption is recorded. Three
focused guard tests pass; bounded independent read-only review found no blocking
defect. An initial plain rollback correctly refused when the normal ASTER game
reappeared; the opt-in then allowed restoration without touching it.

The same commit updates the observer's obsolete DDS compression assertion: retired
DDS bypass must make zero PNG platform calls rather than one refused call. All image
and native fallback comparisons remain. Rebuilt observer source is `9b2d10d5`, DLL
SHA-256 `4d1a8b720ba112ed7cce79ce3181e58bf8a256ad436cd85eab12d9dd73d67c72`;
the build has zero warnings/errors. Runs 01/02 used the earlier `40451d9a` observer;
runs 03/04/05 use the rebuilt observer. Every run uses the unchanged reviewed core
`40451d9a`, SHA-256 `387d70ac5dc5b2d604796829f92a1b73d9af08b3a20ebceb017bce0987aecb27`.
No new core build or release ZIP was produced for this tooling-only correction.

Evidence is under `artifacts/ecosystem-next-20260910/c15a-live-20260919/`:
`capture-summary.json` indexes the five separate captured results, package identities,
probe outcomes and hashes; original captures remain in `.rlo-test-instance/results/`.
The earlier blocked preflight, one safe deployment-lock refusal and the plain ASTER
rollback refusal are preserved alongside successful receipts. None is a hidden run.

All eight owned transactions are restored: preparations `20260919-143135-02472f84`,
`20260919-143350-aae0c46c`, `20260919-143849-d2d88dd9`,
`20260919-144321-68970a6e`, `20260919-144539-27c2b781`, then observer deployments
`20260919-143845-f8949339`, `20260919-143104-2c370248` and core deployment
`20260919-143047-4de08bfa`. The final seven baseline hashes, eleven absences and
retained supplier repair hash match. Metadata audit succeeds; final process snapshot
contains no RimWorld, helper, testhost, MSBuild or fixture operation.
`restoration.json`, `restored-audit.json`, `stopped-processes.json` and
`handoff-live.json` record the stopped handoff to the parent steward for independent
review. No self-acceptance or successor dispatch.

The sections below retain the earlier cleanup and preparation-only record. Their
launch coordination block and pending combined-run statements are superseded by
the results above; their remaining inspection obligations are not waived.

## Earlier cleanup scope and ownership

Assignment `wake-up-c15a-retained-consolidation-20260919-63b676d6` began on clean
`63b676d69fbbacc758536387f22500336287664d`, branch
`codex/ecosystem-next-plan-review`, in the sole checkout. Parent steward is
`01a08711-7899-7ff3-a1b5-4a0b105ba661`. Seven baseline hashes and eleven absences
matched the reviewed C06 recovery before operations. Generation remains
`gog-rev573-20260910-194017-cf1a1eb0`; Assembly-CSharp SHA-256 is
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
No fixture game/helper/build operation was running. One bounded reviewer inspected
source read-only; this task exclusively owns changes, builds and fixture operations.

## Actual behavior

| Behavior | Retained candidate |
|---|---|
| Independent parsed XML, inheritance cache, ordered read-ahead, parsed-language persistence | No release initialization or visible setting. Old persisted booleans normalize false. Coordinated XML bridges remain for processed XML and diagnostics; inactive branches call native behavior. Explicit cache clear still maintains old stores with empty activation arguments. |
| Managed cold PNG/JPEG acceleration and raw-pixel trial | No trial initialization, first-build selection or native decode replacement. First-build-only modes are refused. Native image decoding still feeds normal output capture for retained cache construction. |
| Faithful authored DDS recaching | No warm queue read, prepared resolver read or automatic write; full-size native preparation skips DDS with an explanation. Native DDS precedence/provider order remains. |
| Processed-only XML, translation application, warm PNG/JPEG output reuse | Retained, default off. Historical conditional benefits do not qualify this package. Optional PSD decoding remains independent of first-build acceleration. |
| Explicit C08 quality, sampling, conversion and DDS export | Retained separately, including suitable DDS inputs. Native DDS capture foundations and the explicit quality helper remain. Old native DDS cache entries cannot substitute for intentional quality choices. |
| C10 on-demand textures/graphics/audio, C11 progressive detail, companion app | Deferred. No automatic demand/audio runtime initialization or audio/bootstrap Prepatcher registration. Research and standalone source remain. A stale deferred-audio flag no longer disables ordinary audio routing. |
| C07 atlas, C09 broad routes, C12 search extensions | Retained but provisional until measured. Existing native provider/order and fallback checks remain. |
| C13 display/reports, C14 loading-only background | Retained; actual visible/focus/ordinary-scene obligations remain open. Streaming XML remains a separate experimental option; it was not part of the six-path retirement. |

The shipping core still consists only of About metadata and WakeUp.dll. Prepatcher
remains required. No Doorstop, native audio bridge, raw-trial executable, observer,
test DLL or companion app is added to it. The separately selected quality-helper
bundle remains supported. Dormant research types and their native-game references
are not active features or extra installed dependencies. Supplier advice now states
partial coverage and preserves capabilities users still need.

## Focused validation

Final focused managed checks: **130 passed, 1 skipped, 0 failed**. The skipped case
requires a separately supplied live quality-helper test input; no helper pass is
claimed. Checks cover persisted stale settings, retained PSD/cache selection,
automatic prepatch registration, ordinary audio bodies, broad route contracts,
native cold image calls and output capture, DDS bypass, native provider/precedence,
processed replay, warm queue ownership, quality storage, preparation and fallback.
All **6 package-release tests passed**, including extra-binary and helper-overlay
rejection. Build completed without errors.

Two earlier failures were corrected: the old DDS miss expectation now reflects the
intentional bypass; an existing iterator-inspection test now follows EnumerateCore,
where the actual loop already lives. The final checks include both fixes.
The broad 78-test Python tooling suite was attempted twice: first 77 passed/1 error,
then 76 passed/2 errors. Errors were Windows access-denied directory renames in
synthetic GOG-transition cases, at differing temporary paths. No live fixture was
transitioned by those tests. Their failure logs remain; no tool/security workaround
or test-harness expansion was made. This is an explicit tooling-suite limitation.

Raw evidence: `artifacts/ecosystem-next-20260910/c15a-retained-20260919/`, including
`preflight-baseline.json`, `focused-tests.log`, `focused-tests-02.log`,
`python-tests-04.log`, `focused-final.log` and `retained-final.trx`.
Source/package/deployment/prepared/captured identities are recorded separately in
the final handoff; no old trial package is the new candidate.

## Earlier combined-check plan and remaining inspection

No live launch is safe to infer from the minimized flag: C14's actual
`c14-settle-false-05` took Windows foreground ownership. The assignment prohibits
repeating that surprise. Build, deploy, prepare and verify current functional cases,
preserve their receipts, then restore owned transactions. Do not launch until the
steward coordinates possible foreground interaction with the owner.

Small combined baseline to execute after coordination:

1. Ordinary default startup on the current package, observer and frozen selection;
   normal automatic exit/capture. Inspect actual activations and errors.
2. Retained optional features together on the same selection, French translation,
   fresh owned stores; capture native provider/order, processed XML output and cache
   receipts. Confirm retired counters/helper starts stay absent and native DDS loads.
3. Warm reuse from that new captured run with matching content and settings; then
   one source-change/fallback case using the existing texture-source-change probe.
   Do not import old-build caches as evidence for this package. Existing focused
   invalidation tests support preparation but do not replace these new live results.

The new combined live results, fresh/warm consistency, normal exit and
`automaticTestPassed` remain **pending**, not inferred from offline checks.
Functional durations will be diagnostic only. No performance or platform testing
is authorized by this report.

The smallest coherent owner-assisted session uses one current combined fixture and
an existing copied inspection save. Check the C08 windows (readability,
prepare/cancel/resume, saved choices and coverage/refusals), alpha/masks, four actual
building directions, UI/provider winners and terrain at near/far zoom. Compare
native and selected quality, reset, then restart to verify reset persists.
During the same session, check C14 ordinary unsuppressed save/new-colony entry and
native map transitions; switch away during loading, leave an untouched hold with
background gameplay off and pause-on-load off, then return and verify the usable
scene and unchanged gameplay state. Repeat the relevant native pause/background
preferences. Actual portal traversal, world renderer/cancel and unsupported/failure
paths listed in C14 remain separate concrete coverage, not implied by this session.

Automated pixels cannot prove appearance; a foreground run cannot prove unfocused
loading or a sustained hold. No background redesign occurs here. Concrete C08/C14
subsystem corrections return through the steward to their original owners.
No Steam/Deck, normal-data changes, desktop automation, merge, push or publication.

## Earlier built packages, prepared cases and restored handoff

Runtime cleanup commit: `74399c04a61b796052f38145823208cc18eeecb2`.
Core/observer package source: `40451d9a56f5d5967b91db3ed17ff2c067039a29`
(documentation line-ending correction; same runtime source). Both final GOG builds
completed with zero warnings/errors. The earlier 74399c04 build was never deployed.

| Identity | SHA-256 |
|---|---|
| Core WakeUp.dll | `387d70ac5dc5b2d604796829f92a1b73d9af08b3a20ebceb017bce0987aecb27` |
| Independent FixtureMenuObserver.dll | `639c6b2a4c586c58f081c60755027d9fdf167a1eeee20acdd0f9450060fa517c` |
| Source-inclusive local ZIP | `f4b5a5aae38abb0ad9c97dbf1d95b399f38924081cc51fd3d5202d7592b51d0e` |

Core package: `artifacts/fixture-package/40451d9a56f5d5967b91db3ed17ff2c067039a29`.
Observer: `artifacts/fixture-menu-observer/40451d9a56f5d5967b91db3ed17ff2c067039a29`.
Source-inclusive archive:
`artifacts/releases/0.3.0-rc.2-40451d9a56f5d5967b91db3ed17ff2c067039a29/wake-up-0.3.0-rc.2.zip`.
The historical version label is unchanged; this is a private artifact, not a release.
No optional helper or experimental dependency is bundled in this archive.

Deployment transactions `20260919-111414-759b58e8` (core) and
`20260919-111439-853ad255` (observer) succeeded. Both functional profiles used the
existing C08 ten-package frozen selection, native source precedence, independent
menu observer and normal automatic exit configuration:

- `c15a-default-20260919`: ordinary settings, English; preparation transaction
  `20260919-111443-f1b5fb51` and verify-prepared succeeded.
- `c15a-combined-fresh-20260919`: retained options together, French, fresh stores,
  native XML/language observers, plus stale retired settings to exercise migration;
  transaction `20260919-111508-16deb080` and verify-prepared succeeded.

These are preparation identities, **not captured runs**. No game launched and no
`automaticTestPassed` exists. Fresh/warm/invalidation live results remain pending.
Both prepared profiles were rolled back, so the labels must be re-prepared after
coordination; they are not currently launchable. Exact settings and receipts remain
in the evidence directory. Warm/source-change preparation must use the eventual
new capture, not an older package's result.

All four owned transactions were restored via fixture.py, in reverse dependency
order. Seven original hashes, eleven absences and the retained supplier repair hash
matched; metadata audit reports 315 frozen / 312 originally selected entries and
matching deployed runtime. `restoration.json` and `restored-audit.json` preserve the
proof. No RimWorld, Wake-Up helper, testhost or fixture operation remained. One idle
MSBuild node-reuse worker remained after completed build commands; it was left alone.

Ownership returns stopped to the parent steward for independent review. No
self-acceptance or successor dispatch. Full combined functional acceptance remains
open for the coordinated launches and the explicit C08/C14 inspection above.
