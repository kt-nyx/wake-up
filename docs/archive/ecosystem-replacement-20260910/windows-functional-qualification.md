# Small Windows functional qualification — 9 September 2026

> Dated evidence: identities, results and pending steps below describe this report's original checkpoint. For the combined offline candidate and superseding decisions, see [current state](../../current-state.md), [current scope](retained-features.md) and [R5](r5-combined-offline-review.md). Historical live results do not qualify the new package.

The small individual checks and the final combined check passed after two
product corrections. This pass ran the delivered features in the isolated game
and fixed concrete failures found there. It is a functional qualification, not a performance
comparison or release acceptance. Results are submitted to the steward for
review; this task does not accept its own work.

## Scope and environment

Dispatch: `wake-up-windows-functional-20260909-8412f7c`. The clean checkout began
at `8412f7cc17e73ac1e6cc99865a93dab59e808350`, on the new
`codex/windows-functional-qualification` branch, without another worktree.
Only this task owned edits, packages and fixture operations. A bounded helper
worked on assigned source/tests and stopped before package builds and handoff.

All launches used `scripts/fixture.py`, saved purpose `functional`, independent
menu observation and normal automatic shutdown. Every listed run exited zero
and has `automaticTestPassed=true`; feature failures are identified separately.
The normal Steam process, initially PID 35536 at
`Z:\Games\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64.exe`, remained
untouched. No fixture process was forcibly closed. No normal data, Deck, UI
automation, security settings, publication or performance comparison was used.

The frozen fixture generation is `20260904-182829-e058641c`; manifest SHA-256
`b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d`.
Assembly-CSharp SHA-256 remains
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
The tooling contract calls this `gog-rev573`; the actual game banner reports
`1.6.4871 rev574`. Unity is `2022.3.35f1`, Windows Direct3D 11 on RTX 4080 SUPER.
These exact identities do not establish latest-Steam or Linux qualification.

The activation selection contains Prepatcher, Harmony, observer, Wake-Up, Core
and five official expansions. The image/routing selection adds only
`oskarpotocki.vanillafactionsexpanded.core` and `vanillaexpanded.vmemese` (about
108 MB of frozen files together). It uses the existing CLI's `representative`
selection mechanism with an explicit 12-item order, **not the full workload**.
Its exact order is captured in every run and in
[two-suppliers.json](../../../artifacts/windows-functional-20260909/two-suppliers.json).

## Fixes and evidence

- **Actual Mono method inspection:** `9589a93` passes null generic context for
  nongeneric owners. An empty array made Mono reject the original open
  `DefDatabase<>` operand. The unchanged expected `InjectIntoDefs` fingerprint
  then matched in the game. Exact genuine framework `Queue<>` is normalized
  between its .NET Framework `System` and Mono `mscorlib` homes; the complete
  loading-update instructions otherwise matched. Other types retain their
  distinct identities. The translation setter's expected fingerprint was
  corrected from the test host's `System.Private.Xml` to the original game's
  `System.Xml`; original IL and live canonical text matched otherwise.
- **Recorded ordinary settings:** `5355c56` adds a validated functional-only
  `--user-settings` JSON input, stages native mod-settings XML and records and
  verifies it with the prepared profile. `c53189e` adds the exact English/French
  language choice after English proved to have no useful translation lookups.
  Ordinary product defaults are unchanged.
- **Current cache format:** `5c84489` fixes captured-cache restoration to admit
  only the existing `v1` directory, exact generated entry names, usage file and
  zero-byte owner file, with complete captured-file verification retained.
  The initial restore preparation refused the new format before launching.
- **Required Prepatcher annotation:** `79dde43` permits only its verified
  `DrawPrestarterInfo` prefix on the native loading-window contents method.
  Owner, signature, module identity, complete method fingerprint and prefix-only
  registration must all match. Other competing hooks still refuse the display.
  The inspected method draws below the native window and restores its style;
  Wake-Up adds its separate panel. The live display then installed and ended
  normally, with three observed stages and actual panel layout execution.
  [Exact supplier identity](../../../artifacts/wfq-prepatcher-hook/result.txt)
  and canonical instructions are retained beside that receipt.
- **Private observer checks:** bounded native holder/object comparisons,
  before/queued/after save-load session observations, display layout evidence,
  and one native prepared texture capture/store/restore comparison. Two
  preparation probes failed to locate the comparison object because disk keys
  include `Textures` while holders use slash-normalized relative keys; the
  observer was corrected from the actual captured key. These were probe
  failures, not established product output failures. Method-token diagnostics
  remain private fixture code and are not in the core/public package.

Source changes were committed using the tracked fixture guard. The shared
method corrections passed 48 focused semantic/display/background tests and 17
translation tests; the settings/cache fixture changes passed all 41 Python
checks before the language addition; the changed language controls passed all
35 fixture tests afterward. The final display allowance passed 29 focused
display tests, including rejection of other patch kinds. Observer and committed
core package builds completed without warnings or errors. The tests that need
Unity drawing were qualified in the real fixture instead of emulating an engine.

## Feature results and practical limits

| Delivered feature | Direct live evidence | Limit |
|---|---|---|
| Ordinary/default activation | Default menu and 75×75 map/three-colonist tick/save/reload passed; definition receipt has 29 hits | This small official selection does not qualify arbitrary mod combinations |
| S02 leaf search | Native AlertsReadout construction: 133 predicates, 125 avoided full scans, zero retained types | Startup-only type-name search had zero calls; no speed claim |
| S03 translation application | Corrected French run: 444 completed scopes, 26,572 lookups, 27,814 seed visits, zero fallback/retained entries | English installed but had zero lookups; broader translation edge cases retain offline evidence, not a new universal language claim |
| S08 loading display | Final isolated/combined runs installed, observed three stages, executed panel layout and ended without errors | No visual inspection of overlap, resolution, localization wrapping, interaction or display overhead |
| S01/S07 owned cache/JPEG | One real 1024² grayscale JPEG captured; next run had one hit and one equal comparison across seven rendered mips, zero errors/mismatches | D3D11 compressed output only; RGB JPEG, uncompressed/large images, Linux and broad cache-maintenance scenarios not newly qualified |
| S10 native preparation components | One real JPEG published, read and restored with equal rendered output through the actual engine/store methods | Backend components only: no preparation-window interaction, later-startup prepared hit, resume/cancel UX or Windows lossy-helper run; BGPlanet supplier coverage remains absent |
| S11 texture supplier routing | Real native ContentFinder calls returned the same winning holder object twice; cache hit delta one | No Resources/bundle memoization or on-demand claim |
| S13 save loading | Hooks installed; session false before load, true after queueing, false after native completion; save/map/colonists/ticking passed | Automatic fixture preference is true throughout. Actual focus/minimization, false-preference restoration and saved pause-state fidelity remain unqualified |

The final combined run `wfq-combined-21` used ordinary activation with the
recorded eight settings enabled/selected, French, and the two-supplier order.
It passed `automaticTestPassed` and `gameplayTestPassed`, with exit code zero.
Its receipts show 26,569 translation lookups across 522 scopes, 128 avoided leaf
scans, a routing hit returning the ordinary winning object, three display stages,
one JPEG cache publication, equal native prepared-output comparison and the
complete background-load session transition. Five sampled native French values
(steel label/description, wood, component and soil labels) match the absent
control byte-for-byte: SHA-256
`906e027b389f47a8f5e0c212a82b6f19ce802dc333904ff4006cddc096245e4d`.
This is a bounded output sample, not complete translation equivalence.

Gagarin, Character Editor, Giddy-Up and Loading Progress were absent from these
selections. Their ordinary fallback was retained; no new supplier-path or
S14 mod-manager warning UI qualification is claimed. The image verification
sample is the only processed image in its run (`calls=jpegCalls=verified=1`),
so the equal comparison specifically covers the grayscale JPEG rather than an
unidentified PNG. Native DDS precedence was kept throughout.

The missing capabilities remain unchanged: S04/S05 independent XML reuse,
S06 persistent language reuse, S09 first-build acceleration, broad S10 lossy
coverage, S11 on-demand/S12, and world/map background extensions are undelivered.
Linux helper work remains owner-deferred. No omitted capability is approved for
permanent removal, and this is not an ecosystem-replacement or release claim.

## Preserved warnings and failures

The normal save/reload tests report two reflection-only
`UnityEngine.InputLegacyModule` dependency errors at scene changes. The
Wake-Up-absent control reproduced the same two errors while passing its own
save/reload and normal exit checks. This is an existing fixture/bootstrap
problem, not evidence of a Wake-Up regression; input behavior is not qualified
by these noninteractive tests. The frozen Prepatcher source deliberately retains
reflection-only original assemblies, consistent with that finding.

The tiny generated map can report that large ancient structures cannot fit.
Installed-mod metadata warnings include missing dependency URLs, including the
private Wake-Up template's Prepatcher URL. These did not prevent activation.
No supplier or normal installation was changed to suppress them.

Raw captures remain in `.rlo-test-instance/results/wfq-*/`; the compact
[run summary](../../../artifacts/windows-functional-20260909/run-summary.json)
contains every source, DLL hash, observer source, prepared transaction,
run UUID, selector, receipt and diagnostic menu time. Timings are deliberately
not interpreted as a gain while the normal game is running.


## Source, package, deployment and captured-run identities

Core package directories are named by the complete source revision below and
have adjacent JSON build receipts under `artifacts/fixture-package/`. Deployment
transactions are separate from the build and are preserved in fixture recovery.

| Source revision / built package | DLL SHA-256 | Deployment transaction |
|---|---|---|
| `d44a91252b8df80890cd3fa356547348aab34aab` | `3581a35a6f5cbd1a59307d10c0c0ea210c5416cce6f2938927e319c412957c77` | `20260909-194425-25e9d87f` |
| `9589a93061126bc75aaf767f423fbbb8d5eb8ed0` | `4a57fba2400c6df73db84a0b44b362e041b60599f33c410f0c85cb6291910c67` | `20260909-200042-9c62e902` |
| `79dde439051e56dab5fb910958fafd689fd6750d` | `dca48f2dadd4f0c503aa66f9c09887576ecec984d2e7b8cbec16dbc1e4e4e87a` | `20260909-201553-2314b90e` |

The old deployed `4422f62` package was replaced before the first run. The final
observer package is `081ba050e6cab8acefe9d78be355cbdeea319a85`, DLL SHA-256
`2545bde380dd609f37bd260a6191962f21a464db36c663798853d6f96a8a7caa`, deployed
by transaction `20260909-201449-2470838e`. Earlier observer source/package
revisions are listed per run and their complete receipts are embedded in each
`run.json`. No observer is part of the two-file core package.

All captures are retained, including feature refusals and probe failures. In
absent controls, the listed candidate is parked outside discovery: it identifies
the fixture's available package, not a loaded product. The observed run UUID
binds the menu signal to that actual process. Source abbreviations below resolve
to the package receipts; the compact raw summary retains full revisions,
selectors and preparation transactions.

| Captured label | Core source | Observer source | Observed run UUID | Feature result |
|---|---|---|---|---|
| [wfq-default-01](../../../.rlo-test-instance/results/wfq-default-01/run.json) | `d44a912` | `8412f7c` | `119286331d2d4cf29856c5dc236fc1ac` | Defaults: activation/menu passed |
| [wfq-default-smoke-02](../../../.rlo-test-instance/results/wfq-default-smoke-02/run.json) | `d44a912` | `8412f7c` | `80ed482b102b4126ac958ddfa6dd887f` | Defaults: settings/map/tick/save/reload and leaf execution passed |
| [wfq-translations-03](../../../.rlo-test-instance/results/wfq-translations-03/run.json) | `d44a912` | `5355c56` | `87bcd4f2765c485b8c42d0141dd1a4a1` | Translation refused; menu passed |
| [wfq-translations-diag-04](../../../.rlo-test-instance/results/wfq-translations-diag-04/run.json) | `d44a912` | `428a48c` | `3ca0f9a280c64c7d948a556e1ec3f02d` | Translation contract diagnosis; refusal retained |
| [wfq-display-05](../../../.rlo-test-instance/results/wfq-display-05/run.json) | `d44a912` | `428a48c` | `eb562e80bf00470295f849a1b79681a7` | Display method refusal; menu passed |
| [wfq-translations-diag-06](../../../.rlo-test-instance/results/wfq-translations-diag-06/run.json) | `d44a912` | `5990c93` | `758aaa143d24485dad7c3064dad3257b` | Translation token diagnosis; refusal retained |
| [wfq-display-diag-07](../../../.rlo-test-instance/results/wfq-display-diag-07/run.json) | `d44a912` | `5990c93` | `9d34982132514824b5dbe9dca945e0b1` | Display contract diagnosis; refusal retained |
| [wfq-routing-08](../../../.rlo-test-instance/results/wfq-routing-08/run.json) | `d44a912` | `5990c93` | `5656d148736b41f7a40ea2cb85a367b6` | Routing native object/hit check passed |
| [wfq-png-build-09](../../../.rlo-test-instance/results/wfq-png-build-09/run.json) | `d44a912` | `5990c93` | `716379b6fb024b01b946b32817eb3c43` | JPEG first cache publication passed |
| [wfq-jpeg-verify-10](../../../.rlo-test-instance/results/wfq-jpeg-verify-10/run.json) | `d44a912` | `5990c93` | `bb7291314aab4d1f900e5d812e5bf460` | Restored JPEG rendered comparison passed |
| [wfq-translations-fixed-11](../../../.rlo-test-instance/results/wfq-translations-fixed-11/run.json) | `9589a93` | `5990c93` | `128f7f5a83c74237b312190e18ea1ab8` | Translations installed; English zero lookups |
| [wfq-display-fixed-12](../../../.rlo-test-instance/results/wfq-display-fixed-12/run.json) | `9589a93` | `5990c93` | `4de50c68e1364856b7e199781df8a25c` | Display blocked by required Prepatcher annotation |
| [wfq-background-13](../../../.rlo-test-instance/results/wfq-background-13/run.json) | `9589a93` | `5990c93` | `96f0c444734c45a8a48d603c545d8dc7` | Background session and save/reload passed |
| [wfq-french-14](../../../.rlo-test-instance/results/wfq-french-14/run.json) | `9589a93` | `5990c93` | `e5d19b16bf054d9d8eac301881234253` | French translation execution passed |
| [wfq-preparation-15](../../../.rlo-test-instance/results/wfq-preparation-15/run.json) | `9589a93` | `786ac25` | `6fa03905fc434688b3f8f72fca724ddf` | Preparation backend reached; observer holder path failed |
| [wfq-preparation-fixed-16](../../../.rlo-test-instance/results/wfq-preparation-fixed-16/run.json) | `9589a93` | `eec9ad1` | `672968fa6dea4f83b1597ce881bc2711` | Observer holder path diagnosis retained |
| [wfq-absent-smoke-17](../../../.rlo-test-instance/results/wfq-absent-smoke-17/run.json) | `9589a93` | `eec9ad1` | `f8209ae4b9f547dfb87af83eeab99f15` | Absent save/reload passed; same scene errors |
| [wfq-preparation-18](../../../.rlo-test-instance/results/wfq-preparation-18/run.json) | `9589a93` | `2f9570f` | `1194c145cc29458c979fb33763ffd699` | Native prepared capture/store/restore/output passed |
| [wfq-french-control-19](../../../.rlo-test-instance/results/wfq-french-control-19/run.json) | `9589a93` | `081ba05` | `cd12aec3749841d08725f8af007c9a4c` | Absent French text reference captured |
| [wfq-display-fixed-20](../../../.rlo-test-instance/results/wfq-display-fixed-20/run.json) | `79dde43` | `081ba05` | `c42dcf59208c40b38bec168d2c3400d4` | Display with exact Prepatcher allowance passed |
| [wfq-combined-21](../../../.rlo-test-instance/results/wfq-combined-21/run.json) | `79dde43` | `081ba05` | `b71b174f4dc24f778a3358d528f7ecf7` | Combined receipts/text sample/settings/save/reload passed |


## Ownership handback

Qualification is complete within the limits above and submitted for steward
review. All helper writes/builds are stopped. The final core remains deployed;
the last combined profile/capture is preserved, and no fixture process remains.
Checkout ownership is returned to the steward. No `steward-state.json` edit,
self-acceptance, next slice, push or release was performed.

## Parent steward acceptance

Accepted on 9 September 2026 after review of the actual source changes, focused validation evidence, package/deployment identity and captured results through handoff `a8ccd43`. The steward independently matched the final package hash, checked direct combined and JPEG receipts, verified identical French sample hashes, and confirmed that the same two scene dependency errors occur in the absent control. No RimWorld process remained at review. No further run was needed to substantiate this bounded result.

Accepted runtime is `79dde439051e56dab5fb910958fafd689fd6750d`. This closes the authorized small Windows functional pass and pauses the recurring check. It does not qualify visual presentation, actual OS focus/minimization or false-preference restoration, preparation UI/lossy-helper/later-startup prepared consumption, arbitrary modlists, performance or Linux. The original undelivered capabilities remain open. Recommended next step is a short owner-assisted visual/focus check, then a scope decision on remaining capabilities before release planning.
