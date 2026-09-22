# S14 — Conflict warning disposition and handoff

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

**R4c reconciliation — 10 September:** no additional verified duplicate or
incompatible package rule was found. Existing public duplicate-copy metadata and
required Prepatcher stay. Native-summary hiding conservatively refuses active
`ilyvion.loadingprogress` or unknown drawing hooks; per-mod XML timings refuse
changed observed-method hooks. These are reduced feature coverage, not blanket
incompatibility or grounds to remove a supplier's missing replacement features.
No automatic disabling/reordering or modal launch warning was added. See the
[current capability table](r4c-remaining-coverage.md#current-capability-table).

Wake-Up keeps the existing warnings against enabling a second, older copy of
itself. The reviewed loading mods do not justify any additional blanket
incompatibility. Where they change the same functions, Wake-Up's existing checks
usually leave that feature to the other mod. The existing Loading Progress
display status now explains that decision and both resolution choices.

**Accepted for the catalog/status checkpoint and focused offline checks.** No conditional launch dialog is justified by this
checkpoint, so no dialog scheduler, dismissal state or empty runtime catalog was
added. This is an explicit surface disposition, not a universal compatibility
claim or delivery of the missing replacement capabilities.

## Catalog and exact user-facing text

Static metadata cannot inspect settings. These are the complete unconditional
entries in the public `kt.nyx.wakeup` package's [release template](../../../release/About.xml).
Both concern duplicate product code, regardless of which optimization is selected
or admitted. Metadata warns; it does not prevent assembly loading.

| Exact package ID | Affected capability and concrete reason | Version/mode and activation condition | User choice |
|---|---|---|---|
| `local.rimworldloadingoptimizer.op7validation` | A second development copy of Wake-Up's runtime, with overlapping loading hooks/assembly ownership | All versions carrying this retained private identity; public Wake-Up and the validation package both active. Independent of feature settings or admission | Disable the validation copy before enabling public Wake-Up, or disable public Wake-Up and keep the validation copy |
| `kt.nyx.startupfixes` | Pre-rename product bundle, a second copy of the loading optimizer | All versions of this retained old identity; both packages active. Independent of feature settings or admission | Disable the old bundle before enabling Wake-Up, or keep it and disable Wake-Up |

Identity evidence: the current [private fixture template](../../../validation/fixture-package/About/About.xml)
declares the first ID; `git show 5ab28547cc9e002e89f39dd15bd41733f9294398^:release/product.json`
declares the second (`0.1.0-dev`, RimWorld Loading Optimizer). The current public
identity is in [product.json](../../../release/product.json). No name-fragment or fork
matching is used. Prepatcher's required `zetrith.prepatcher` dependency and all
existing load-order metadata are unchanged.

The existing public description says exactly:

> Do not enable alongside another copy of Wake-Up or the old validation package.

RimWorld and RimSort supply their own metadata-warning wording; S14 does not
claim to control those localized messages.

The one improved **nonmodal status** is for active `ilyvion.loadingprogress`
(source metadata `ilyvion.LoadingProgress`, normalized by RimWorld). When the
launch-effective **Show Wake-Up loading display** option is selected, the existing
guard declines Wake-Up's display before installing its observers. All versions
of this exact active identity yield under the existing policy; the inspected
supplier is 0.14.0 at `bcd71bafd994b206ed644df0ae56583beb047646`. This is conservative
display selection, not a claim that every supplier version has identical hooks.
The settings entry and diagnostic retain this exact text:

> Wake-Up loading display: Loading Progress is active, so Wake-Up's display is inactive. To keep Loading Progress, turn off 'Show Wake-Up loading display'. To use Wake-Up's display, disable Loading Progress in Mods. Restart after changing either choice.

Other admission checks still apply when choosing Wake-Up's display. The message
does not claim an active takeover, disable any mod, or reject Loading Progress's
supported PNG/repaint integrations. An inactive installed copy, a similarly named
fork, or an unselected Wake-Up display does not produce this status. Explicit
development arguments retain precedence over persisted settings. See
[LoadingDisplayRuntime](../../../src/WakeUp/Startup/LoadingDisplayRuntime.cs) and the
existing settings presentation in [WakeUpMod](../../../src/WakeUp/Startup/WakeUpMod.cs).

## Metadata readers and active versus installed content

The freshly hash-checked frozen GOG 1.6.4871 rev573 `Assembly-CSharp.dll` is
SHA-256 `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Read-only ILSpy extracts are [ModsConfig.cs](../../../artifacts/s14/ModsConfig.cs)
and [ModIncompatibility.cs](../../../artifacts/s14/ModIncompatibility.cs).
`GetModWarnings` enumerates `ActiveModsInLoadOrder`; `FindConflicts` uses
`GetActiveModWithIdentifier(..., ignorePostfix: true)`, including the native
duplicate Steam suffix handling. `ModIncompatibility.IsSatisfied` also checks
an active match. Merely installed entries can have detail requirements, but
are not thereby active conflicts. Existing extracts
[ModMetaData.cs](../../../artifacts/s08/research/ModMetaData.cs) lines 230–256 and
730–745, and [Page_ModsConfig.cs](../../../artifacts/s08/research/Page_ModsConfig.cs)
lines 337/455 establish the version selection/detail/display distinction.

Pinned RimSort `3b13dfc32dd22712e655cbd02277dbdd1230d84e` reads the base list in
[metadata_factory.py](../../../artifacts/ecosystem-review-20260909/sources/rimsort/app/models/metadata/metadata_factory.py)
lines 600–632 and replaces it with a matching game-version override when present;
it stores IDs case-insensitively. Its
[mods_panel.py](../../../artifacts/ecosystem-review-20260909/sources/rimsort/app/views/mods_panel.py)
lines 3473–3479 build IDs from the current list; 3624–3638 restrict conflict checks
to the Active list, subject to ignored warnings; 3395–3419 intersect that list's
IDs. Thus the inspected warning calculation needs both mods active. No version
override or community rule is added.

These are verified source/reader observations. Neither application's actual UI
was exercised; warning visibility, localization, user suppression and click
behavior remain live-unqualified.

## Candidate rules that were not added

Source snapshots and full pinned revisions are in the original
[comparison ledger](../../../artifacts/ecosystem-review-20260909/comparison.md) and
[opportunity report](../../../artifacts/ecosystem-review-20260909/report.md).
They compare the older public baseline; the coverage below supersedes their
then-missing-feature descriptions. Supplier source is not proof that a currently
distributed DLL matches it.

| Candidate and inspected mode | Actual interaction and disposition |
|---|---|
| `sz.yaopt` 1.1.4, `ab60621f071f` | Its translation, type-name and XML-worker replacements are settings-dependent. Wake-Up's translation all-hook checks, type transpiler/enumeration checks and XML worker guards decline overlapping work. Lazy `LoadTexture` is also guarded by PNG/preparation admission. Its early `ContentFinder.Get` rewrite fails S11's original/rewritten-body checks in either rewrite order. No admitted concurrent takeover proved; retain YaOpt's distinct lazy/DDS functionality |
| `Taranchuk.FasterGameLoading`, original `1b23e89cbf56` and Continued 2026.08.09.1 `0132c10a3185` | Both use the same package ID. AllTypes/leaf patches are guarded; Continued's LoadTexture replacement is guarded by PNG/preparation checks. No fork-wide ban. Persisted XML-miss behavior is a supplier correctness question, not proof that Wake-Up causes a conflict. Deferred graphics/audio and atlas work are not replaced by S11 routing |
| `solaris.fastloader` 1.0.1, `d95ae707661e` | Its ReloadContentInt texture-cache prefix makes Wake-Up's image adapter decline. XML cache hits skip ApplyPatches; Wake-Up respects `__runOriginal`. Injection prefixes only profile and return true; the fast applier has no production callsites. Independent XML/language/atlas caches remain distinct missing Wake-Up capabilities |
| `vopaga.hyperdrive`, normal mod at `3a96ce59648a` | Foreign XML-worker transpilers make Wake-Up skip/rebuild/fall back for those workers. External PatcherTool mode cannot be inferred from an active package ID. No rule based on normal-mod presence |
| `ilyvion.loadingprogress` 0.14.0, `bcd71bafd994` | Keep supported repaint and texture bridges. Independent display yields as explained above. Its optional initialization hook on ExecuteToExecuteWhenFinished makes S13's shared lifecycle guard refuse/release background save/world loading; Wake-Up does not take over. Known Gagarin XML interference retains the existing fallback. No blanket ban or modal warning |
| `vr.missilegirl` / Gagarin | Retain the optional parsed-XML supplier integration. Wake-Up has no independent persistent XML replacement; supplier/constructor/hook admission remains primary protection |
| Image Opt; No Modlist on Loading; Load in Background | The reviewed inventory lacks verified source/package implementation identities. No guessed IDs or rules. Proposed S13 scope covers ordinary saves and native world creation, not new-colony/standalone map entry or all advertised background work; S11 does not deliver on-demand assets |
| Gameplay optimizers, Graphics Settings+, historical loaders, mod managers and conversion tools | Inventory membership or similar features do not prove an unconditional conflict. This checkpoint does not audit gameplay suites or infer rules for unknown versions. No new warning. Prepatcher remains required; no community rules or load orders changed |

Concrete source sites supporting the first five rows: YaOpt
`Patches/Early/Verse_DefInjectionPackage_InjectIntoDefs.cs`,
`Verse_DefInjectionPackage_SetDefFieldAtPath.cs`, `HarmonyLib_AccessTools_TypeByName.cs`,
`MultiTargets_PatchOperationSingle.cs`, `MultiTargets_PatchOperationMulti.cs`,
`Verse_ModContentLoader_LoadTexture.cs`, and `Patches/Prepatch/Verse_ContentFinder_Get.cs`;
FGL `AccessTools_AllTypes_Patch.cs`, `GenTypes_AllLeafSubclasses_Patch.cs` and
Continued `TextureDownscaler/ModContentLoaderTexture2D_LoadTexture_Patch.cs`;
FastLoader `Project/Source/FastLoaderPatches.cs` lines 41–75, 175–183, 270–329;
Hyperdrive `Patches/PatchOperationApplyWorker_Patch.cs` lines 18–58;
Loading Progress `LongEventHandler_ExecuteToExecuteWhenFinished_Patches.cs` lines
5–22. Wake-Up's corresponding protection is in `DefLookupRuntime`,
`TypeLookupRuntime`, `LeafSubclassRuntime`, `TranslationRuntime`, `PngRuntime`,
`AssetRoutingPrepatch`/`AssetRoutingRuntime`, and `BackgroundLoadingRuntime`.

## Coverage carried into review

S01 storage/maintenance, S02 code searches, S03 translation application, S07
PNG/JPEG coverage, S08 display, corrected Windows S10 preparation, S11 routing
(`950144b`) and S13 ordinary save loading (`5c3f460`) retain their accepted offline
scope. Their live qualification remains separate from the older public release.
The [10 September candidate](retained-features.md) supersedes the earlier S10
public scope: native-preserving preparation remains, while lossy presets and
helper execution are removed from the public workflow after consumer research
found no useful qualified role. S11 remembers texture holders; it does not delay asset loading.

Independent parsed XML/replay/inheritance reuse (S04/S05), persistent language
reuse (S06), first-build acceleration (S09), broad lossy preparation (S10),
on-demand assets (S11), progressive detail (S12), and new-colony/standalone map
background entry (S13) are explicitly proposed for release deferral under the
owner's redesign/drop authority. The candidate adds an independent world-page
extension; its exact qualification belongs to that report. Linux helper work
remains owner-deferred. No additional blanket metadata conflict or modal dialog
is justified by the reduced scope. Steward review remains required, and this is
not an ecosystem-replacement claim.

## Offline evidence and surface status

The exact assigned base was `47340fb4ea9a7a9428f7df57ec8f682ea48145ba`; initial
tracked status was clean. Work uses this checkout on `codex/s14-conflict-warnings`.
Normal Steam RimWorld PID 35536 was verified outside the fixture before work;
fixture process absence and the tool's fresh offline guard protect builds/tests.
Two bounded reviewers performed read-only metadata and conditional-hook research
and have stopped. Runtime change is only the existing refusal message; no loader,
constructor, callback, queue or continuation order changes.

The focused fixture command selects `LoadingDisplay`, `UserStartupSelection`,
`PngLoadingProgress` and `CompatibilityGuard`. Its new cases exercise selected/off,
active/inactive, exact/fork/unrelated identity, binary-refused and repeated
initialization through the real display entry. The offline test substitutes
logging/binary admission to avoid Unity UI execution. Existing cases cover
launch-effective precedence, early/late foreign screen hooks, callback order and
known versus unknown supplier hooks. No conditional catalog means multiple
conditional dialogs, dialog deduplication and safe dialog timing are not
applicable; no empty scheduler is tested as if it delivered a warning.

| Surface | Implementation | Offline evidence | Live status |
|---|---|---|---|
| Public metadata | Existing two legacy entries and required Prepatcher preserved | Exact catalog/dependency assertions in existing release checks; native and RimSort reader inspection | No new UI qualification |
| Existing display status/settings/log | Clear refusal and both choices; no mod mutation | Focused entry/selection/guard checks | Text layout and actual game presentation unqualified |
| Consolidated launch dialog | Not added: no justified active-conflict rule | Source disposition only; clean/off/refused paths add no dialog machinery | Timing/continuation not claimed |
| Other loading features | Existing fallback guards retained | Adequate accepted slice evidence retained | No new compatibility or speed claim |

Build/test identities and final checkout ownership are recorded below. No deployment, game launch, normal game-data
change, Deck access, UI automation, security/tooling change, push or publication
is authorized or performed. The private steward state is not edited.

Focused results: **47 managed passes, zero skips/failures; 37 Python passes**,
zero-warning/error Release compilation. The fixture receipt explicitly records
`gameLaunched: false`; launcher messages in Python output are mocked unit-test
output. See [focused log](../../../artifacts/s14/focused-tests.log) and
[TRX](../../../artifacts/fixture-tests/5bdbd02c09724f91bd1804fa74f7041e/features.trx).
After tightening the existing exact metadata assertion, all four release tests
passed again ([log](../../../artifacts/s14/release-metadata-tests.log)). This does not
repeat unrelated feature tests or claim actual mod-manager UI execution.

## Source-specific package and ownership return

Source commit **`d44a91252b8df80890cd3fa356547348aab34aab`** was committed with the
tracked fixture guard. `py -3 scripts/fixture.py build` then passed from committed
inputs and the frozen GOG/Harmony/Prepatcher API references with zero warnings or
errors. The undeployed [two-file core package](../../../artifacts/fixture-package/d44a91252b8df80890cd3fa356547348aab34aab)
and [receipt](../../../artifacts/fixture-package/d44a91252b8df80890cd3fa356547348aab34aab.json)
identify that source; [build log](../../../artifacts/s14/package-build.log).

| Output | SHA-256 |
|---|---|
| `Assemblies/WakeUp.dll`, 242,176 bytes | `3581a35a6f5cbd1a59307d10c0c0ea210c5416cce6f2938927e319c412957c77` |
| Fixture `About/About.xml`, 1,091 bytes | `f6e07f9178757aab64b287a1940cf672db8c6416c804f56d4df541b5d8492cee` |
| Adjacent package receipt | `eb857111a3cd9ae65ea5c341742d82973e71c672d75c18a4d18f4445b88819f2` |
| Public metadata-only projection | `1f4898cc34bc2221427095e5f825a973a39113cfd26476ee563c087d45cfcf5b` |

The [metadata check](../../../artifacts/s14/inspect_metadata.py) uses existing
`package_release.verify_input` on that exact receipt/package and
`package_release.about_bytes` on the source commit's public inputs. Its
[evidence](../../../artifacts/s14/metadata-evidence.json) records both identities and
the three frozen reference hashes. The fixture deliberately retains its private
package ID, required Prepatcher and no self-incompatibility. The public
[metadata-only projection](../../../artifacts/s14/public-about-projection.xml) contains
exactly the two legacy conflicts, required Prepatcher and no version override.
This is not a public release package; no combined release/helper build was made.

Final process inspection still found only normal Steam RimWorld PID 35536 at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe`. No fixture
process was started, no deployed package changed and no live run identity exists.
This documentation closeout does not change the built source/package.

S14 and both read-only reviewers stop all writes/builds and return exclusive
checkout/build ownership to steward task `01a08711-7899-7ff3-a1b5-4a0b105ba661` at
handoff. The catalog/status checkpoint remains **awaiting review**; the task has
not accepted itself, edited `steward-state.json`, begun another slice, or started
a combined release. Dispatch reference: `wake-up-s14-conflicts-20260909-47340fb`.

## Parent-steward acceptance — 9 September 2026

Accepted source `d44a91252b8df80890cd3fa356547348aab34aab` and handoff `0aef260cfcbe6c362b75e0bd18b7c364c4203ad0`. Reviewed the actual status-only runtime diff, active/exact-package tests, exact public metadata assertions, inspected native/RimSort active-list handling and candidate-rule reasoning against existing feature admission guards. The two legacy metadata entries remain justified; no new unconditional conflict or conditional launch-dialog rule is established. Accept the explicit no-dialog disposition, not a promise of universal compatibility or replacement of deferred features.

The recorded 47 managed/37 Python passes plus four updated release checks and matching final core DLL hash support this limited change. No unnecessary build/test repetition was performed. Actual mod-manager UI and in-game message layout remain unqualified. All team writes/builds are stopped and ownership returned. The steward completed the [first-pass combined review](combined-review.md); no release, deployment or live launch is authorized by this acceptance.
