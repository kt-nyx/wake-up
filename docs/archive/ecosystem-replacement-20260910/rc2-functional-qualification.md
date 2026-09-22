# rc.2 Windows functional qualification — 10 September 2026

## Parent acceptance

Parent review accepts handoff `90c1199` for this bounded functional campaign.
Inspection covered the actual two texture fingerprints and closed-type regression
check, nonactivating launch, pinned helper transaction, prepared-only export
restore allowance, native feature-probe calls and the independent review. All
seven run-record hashes and successful functional exits were verified against
their captures; actual audio, preparation/fidelity/export receipts distinguish
passes from the preserved probe failures and supplier refusals. The final
profile/core/helper hashes and absent settings/prepared pointer were rechecked.
No further code correction, repeated test or game launch was needed.

The non-disruptive campaign is complete and the five-minute steward timer is
paused. Corrected build `a70873e` remains in the restored fixture; the immutable
original rc.2 ZIP remains unfixed. Interactive ordinary entry, focus/pause,
visible summary/layout and audible playback remain unqualified, as do performance,
Linux and release readiness. This acceptance does not broaden those claims.

The isolated game tests found and corrected an integration defect: texture
routing and prepared textures rejected the methods rewritten by the new audio
feature. The corrected core runs those features together. This campaign checks
functionality and correctness while the owner uses the PC; it makes no
performance claim and does not qualify release, Linux or ordinary colony entry.

## Scope and identities

Dispatch `wake-up-f1-functional-4427883` began at clean
`44278830c6d9bff3d438aa9c0e3bff3b3ebb8412`, using
`codex/wake-up-f1-functional` in the sole existing checkout. One owner operated
the fixture; a bounded helper edited/reviewed assigned code and ran focused
offline checks between live operations. An independent reviewer inspected the
guard fix and fixture boundaries without operating the game.

Every launch used `scripts/fixture.py`, ordinary user activation, English,
`--purpose functional`, independent menu observation and automatic normal exit.
Windows received `STARTF_USESHOWWINDOW / SW_SHOWMINNOACTIVE`; read-only window
inspection during run 02 confirmed the visible fixture window was minimized and
not foreground. The minimized game still produced its actual menu repaint.
There was no window restoration, desktop input, logging suppression, scene probe,
normal Steam/profile change, Deck access, security change or publication.

| Layer | Exact identity |
|---|---|
| Original rc.2 build source | `a9aed0e43bb10e032d75d4b6b843772b0f2a466a` |
| Original rc.2 DLL, runs 01–02 | `a19696a37378646e9739bfc092d68eaed1b80bac08c14fbc0a72763093bae612` |
| Product correction | `6e06429ce1c9db9722ab1a098d0943b62d89b7c6` |
| Corrected core build source | `a70873e300c51930ebdff63a43c051257d6bba40` |
| Corrected DLL | `9c2dcec8cbe648ee78350ebb4460c7d3a3ba10412db6a4f8957f9eb51606c4cd` |
| Final private observer DLL | `8aadc70b7ec54ae7b63a5dcdd3590bb363e67e4eaa62396444af918a05242042` |
| Unchanged reviewed helper | `b351d1794b82f2310f643c307d54498693db8c6818380bc6dd5e4141489023e5` |
| Original helper ZIP | `dc7a94213b77353570eb26ce52e7376f2b9fe7d76167b4b41afe737ec097957a` |
| Exact Steam game assembly | `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |
| Fixture generation | `steam-rev590-20260910-020604-cb37ef1b` |
| Frozen manifest | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93` |

The corrected private package is
`artifacts/fixture-package/a70873e300c51930ebdff63a43c051257d6bba40`.
The original source-inclusive rc.2 ZIP remains immutable and contains the
**original**, unfixed DLL. It is not a corrected release bundle. Fixture helper
deployment copies only the pinned executable into the existing private package,
hash-checks the exact reviewed ZIP and helper, retains the corrected DLL, and
records the two independent origins. No public duplicate mod was installed.

Raw logs, focused checks, deployment receipts and settings are under
`artifacts/rc2-functional-20260910`; canonical captures remain in
`.rlo-test-instance/results`. These private files are ignored, never committed.
The [run identity index](../../../artifacts/rc2-functional-20260910/run-identities.json)
records all seven complete run-record hashes, full observer/package identities,
settings, order, helper provenance and cache sources. Original core deployment
was transaction `20260910-172936-5d38f210`; the corrected helper overlay was
`20260910-173812-a0eb42ef`, after corrected core deployment
`20260910-173808-c87e9006`. Final observer deployment was
`20260910-174630-692e30a2`. Corresponding deployment receipts preserve every
transaction. Final observer source is `b1aa6592c807c2ecc68d9fe85a8fd3d98d12a86e`;
later fixture restoration/documentation edits do not change its binary.

## Workloads and actual outcomes

Official content means Core and five expansions, required Prepatcher, Harmony,
the private observer and one Wake-Up package. The small image/music selection
adds Vanilla Expanded Framework, Vanilla Ideology Expanded — Memes and
Structures, and P-Music. It preserves native source selection, including authored
DDS precedence; no PNG sibling was forced into use.

Selected runs use ordinary saved options for streaming XML, deferred audio,
texture routing, prepared textures, automatic PNG/JPEG caching, loading display
and diagnostics, summary hiding, XML timings and background loading. Product
defaults remain unchanged. Each run records its exact order, settings, observer,
core and captured-file hashes; selecting an option is not proof of execution.

| Run | Actual result |
|---|---|
| `f1-defaults-minimized-01` | Original rc.2, official content, settings absent. Independent menu and normal exit passed. Native definition receipt recorded 29 hits, no fallbacks. |
| `f1-selected-official-02` | Original rc.2, selected options. Menu/exit passed; streaming XML processed 1,572 files, no fallbacks, and normal report completed 30 calls with no unfinished/omitted calls. Texture routing and prepared loading refused their own rewritten bodies: a product defect, not feature success. No eligible filesystem audio/image in this workload. |
| `f1-small-combined-03` | Corrected core, small image/music selection. Menu/exit, same-native-winner routing, deferred song first-use and native texture preparation/fidelity passed. Export probe did not run because its sample filename was wrong; that private-probe typo was corrected. Timing report stopped as partial after a later XML hook appeared. |
| `f1-prepared-reuse-export-04` | Restored verified native output from 03. Actual startup prepared hit, controller reuse and rendered equality passed. Full DDS export published/reopened correctly, but a private probe compared backslashes with the normalized manifest path and reported failure. Preserved as a probe failure, then corrected. |
| `f1-export-audio-05` | Restored native/full-export output from 04. Native startup hit/reuse, full export reuse, half/quarter creation and all three DDS layout/mapping/source-preservation checks passed. Native audio holder completion and source-stream cleanup passed. |
| `f1-clear-export-06` | Restored all prepared entries/exports from 05; ordinary one-shot clear selected. Native entry and all six DDS/mapping files disappeared, only the zero-byte owner marker remained. The saved clear request was consumed, and the observer skipped generation under maintenance policy. Menu/exit passed. |
| `f1-supplier-coexistence-07` | Small selection plus Loading Progress and Gagarin. Menu/exit, native preparation/fidelity and all three exports passed. Display/summary yielded to Loading Progress; XML used 2,057 ordinary fallbacks with zero streamed reads; audio stopped on a new unsupported runtime consumer; timing report remained partial. These are refused/unavailable feature paths, not replacements. |

All seven have `automaticTestPassed=true`, actual exit code zero and a captured
independent menu event. There is **no gameplay-test pass** in this campaign and
no forced closure. These menu results do not erase the feature/probe failures.

| Feature | Final bounded disposition |
|---|---|
| Default startup / required Prepatcher | Passed on original rc.2 in 01; corrected core combined startup passed in 03–07. Default-only startup was not repeated on the corrected core. |
| Texture routing | Original rc.2 defect fixed; same native winner and positive reuse passed on corrected core. |
| Streaming XML | Passed actual reads: 1,572 official / 1,926 small-workload files, zero fallback in those runs. Supplier combination used ordinary fallback, not streaming. |
| Deferred audio | Compiled song first-use, repeated lookup/holder identity, ready sample metadata, idle completion, whole-holder exposure and native stream cleanup passed. Supplier combination stopped for unsupported consumer. |
| Native texture preparation/cache | Real controller publication, duplicate automatic-entry retirement, reopened native output, seven rendered mip comparisons, later startup hit and reuse passed for one 1024² JPEG. |
| Windows DDS exports | Actual in-game controller/helper, full/half/quarter files, full mip chains, mapping, original preservation and full-size reuse passed. No exported texture was installed or substituted in game. |
| Cache maintenance | Normal prepared/export clear and one-shot request consumption passed; bypass/rebuild/cancel/resume UI were not newly qualified. |
| XML report | Normal 30-call completion passed on official content. The image/supplier workloads stopped conservatively as partial after changed hooks. |
| Display and summary | Layout code/stage observations passed. Supplier-owned screen refused correctly. Actual hidden-summary screen and visible layout remain untested. |
| Background lifecycle | Save/world/new-colony hooks admitted together. Real colony entry, focus/preference/pause/abort completion remain untested. |

## Fix and focused validation

A generic method is a template that the game uses for several types. The audio
rewrite adds a check of that type to shared holder methods. The offline checks
had fingerprinted the open template, while the texture runtime checks the
concrete `Texture2D` form. Those instruction fingerprints differ even though the
rewrite is supported. The fix replaces only the two exact texture fingerprints
and makes the regression test inspect the same concrete methods as production.
The audio contract remains unchanged; unknown bodies and foreign hooks still
refuse. The initial failing check is preserved alongside the corrected results.

The correction passed **26 managed checks and 54 Python checks**, with no build
warnings/errors. The corrected core and private observer builds also completed
without warnings/errors. The helper overlay's subsequent focused fixture checks
passed all 45 cases. Independent read-only review found no actionable defect in
the product identity fix, nonactivating launch or helper transaction boundary.
The prepared-cache restoration allowlist initially rejected the new owned
export layout before any launch. It now admits only digest-named DDS/mapping
files in `v1/exports`, retaining full captured-file hash checks and source/run
identity checks. The first edit accidentally targeted the automatic-cache
block; its test failed and was preserved, then the edit was moved to the correct
prepared category. The final 45 fixture checks passed. No product or live run
used that intermediate restore edit.

## Meaning and remaining limits

Streaming XML executed in the real Mono/Prepatcher environment while retaining
native scheduling and publication. Full encoding, error identity, attribution
and ordering cases retain their existing focused offline evidence; this campaign
does not replace that with a menu-only claim or add an independent XML cache.

The song probe uses an ordinary compiled `SongDef.clip` read rewritten by
Prepatcher, then repeats native lookups and holder access. In run 03 it obtained
the same ready 44.1 kHz stereo object and reduced pending source count from 55
to 54. Native idle completion also ran without recorded failed attempts.
This does not test audible quality, playback glitches, arbitrary reflection,
generated consumers or worker-thread first use. Audio remains experimental/off.
In run 05, ordinary full-holder exposure completed its remaining 28 pending
sources, preserved the tested object and produced all 56 P-Music clips. Immediately
before normal shutdown, with no native audio source retaining those clips,
native `ClearDestroy` closed all 56 observed open source streams and cleared
the native holder lists and pending registry. This is real native cleanup in a
bounded menu check, not ordinary gameplay teardown qualification.

Native preparation uses the real incremental controller without putting its
window on the screen. The selected 1024×1024 JPEG was published, reopened and
restored, with matching texture properties and all seven rendered mip levels.
Mip levels are the smaller image versions used when rendering at a distance.
This is engine-output equality, not visual inspection or broad artwork coverage.
The full/half/quarter DDS outputs are 1024²/512²/256², respectively, with complete
chains and checked source mappings. They remain portable exports requiring an
independent review of any intended in-game consumer.

Display layout code executed and reported three native stages. Summary hiding
installed, but initial startup did not request a summary (`hidden=0`), so actual
hidden-screen behavior is untested. Save/world/new-colony background hooks were
admitted; lifecycle, pause preference, focus loss and ordinary new-colony entry
remain untested here. The prior automatic scene/bootstrap failures were not
repeated under an unchanged premise, and no private stack-trace workaround was
used. Earlier manual entry belongs to its older exact build.

New independent XML/language caches, broad progressive textures and general
map/background coverage remain absent. Platform-wide qualification,
`ordinaryEntryQualified`, `steamQualified` and `releaseReady` remain false.
Publication is not authorized.

## Restoration and ownership handoff

`restore-profile` transaction `20260910-175028-032652f3` restored the exact original
seed order and English preferences, removed the prepared pointer, and left
Wake-Up settings absent. Both `Prefs.xml` and `ModsConfig.xml` match their original
seed SHA-256 values; [final-state.json](../../../artifacts/rc2-functional-20260910/final-state.json)
records them with the retained deployed DLL/helper hashes. Metadata audit passed.
All seven captures and failed offline/probe evidence remain preserved. The
corrected private DLL/helper remain installed but Wake-Up is not enabled in the
restored seed order. Normal game/profile/cache/save state was untouched.

No fixture game, helper, test host or active task build remains. Both bounded
agents have completed; exclusive edit/build/fixture ownership returns to parent
`01a08711-7899-7ff3-a1b5-4a0b105ba661` for review. The parent owns its timer and next
action. The final response identifies the documentation handoff commit separately
from the immutable core and observer build identities above.
