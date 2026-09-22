# C09 broad successful asset routes

## Independent steward functional review — 11 September 2026

Reviewed handoff `5e1c327b4ca0b2a97f1474918d65fc18816fdd22` against
`c7688cc`, with tested core/observer `8aeaa31a9640d449eb41631d6a8cf25681ad941a`.
Functional coverage, correctness and the exercised compatibility contracts pass.
The steward and two bounded read-only reviewers found no blocking source defect
in native precedence, callbacks, diagnostics, invalidation or asset ownership.
The original native folder/direct APIs supply their indexed behavior; no extra
folder-cache or deferred-creation benefit is claimed.

Independently verified all three captured run manifests, actual routing receipts,
selected coexistence receipts and their recorded package DLL hashes. Run 02 is
preserved as a feature failure despite menu success. Final run 04 passes 140
routing checks; only documentation changed after its tested source. Rehashed
seven restored endpoints and checked three absences. No fixture/app/helper/build
operation remained at handoff; normal Steam was untouched. Raw parent verification
is `artifacts/ecosystem-next-20260910/c09/steward-evidence-review.json`.

String file loading uses real unchanged Core files through two temporary native
providers. This is sufficient evidence for the general native file-loading and
routing contract; naturally authored root-Strings mod compatibility remains an
explicit coverage extension, not an omitted implementation. Natural Resources
audio passed on the earlier source. The final generic external-resolution source
and actual Resources-texture/bundle-audio/callback checks support the functional
review, while final-source natural Resources-audio resampling remains a focused
later compatibility check. Do not relabel either limitation as observed coverage.
C09 added only idempotent path exposure bookkeeping to C08; the DDS-heavy
coexistence run does not prove nonzero PNG cache or converted-quality usage.
Recheck those selected combinations during combined GOG correctness.

Performance and full slice acceptance remain pending. In particular, a garbage
collection can discard the complete weak validation snapshot even with providers
still live; measure rediscovery and per-request validation with ordinary allocation
behavior, not only immediate repeated hits. No loading-time benefit or YaOpt-wide
removal is claimed. Under the owner's functional-first sequencing, C10 may proceed
from the resulting reviewed commit after exclusive ownership transfer. C08 in-game
visual checks remain before-release obligations; companion app/6c stays optional
post-release. This review also normalizes this report to UTF-8 and ordinary line
endings after its handoff used a Windows-specific encoding.

## Functional handoff - 11 September 2026

C09 began from clean `c7688ccc9a7ed5dbb7e396010927ba4cdd0be6e7` on the
preserved campaign branch, with exclusive edit/build/fixture ownership from the
steward. Main remains unmerged. Implementation and focused GOG functional checks are
complete for steward review. Full acceptance and performance remain pending.

The opt-in **Remember successful asset routes** setting remembers where an asset
was found. Each request still obtains the current native object from its holder,
Resources or exact bundle/name. It owns no assets and stores no missing result.
Native quality, loading readiness and folder/direct exposure are preserved.

Native GOG `ContentFinder<T>.Get` searches reverse mod holders, then Resources
for textures/audio, then reverse mods/forward bundles for textures/audio/shaders.
Within a bundle, each extension tries the folder-name path before the package-id
path (the latter only for unofficial providers). Strings use only holders and
shaders skip holders. Direct bundle queries skip Resources. Resource callbacks
still run once even after a previous bundle success; they may change winners or
hooks. Missing routed results resume the original native diagnostics.

Folder queries preserve forward holders, Resources, then reverse-mod bundle
enumeration, including order and duplicates. Native holder and bundle prefix
tries already provide folder indexing. Public dictionary/name-list mutations do
not automatically update those separate tries; rebuilding them would change
native results and exceptions. C09 preserves these APIs instead of claiming a
second folder-index improvement. Direct holder Get already uses a dictionary.

Successful route proofs track live holders, dictionary/list mutation stamps,
provider replacement/order and bundle metadata/extension arrays. Bundle hits
check live bundle identity and invoke native typed LoadAsset. Unload/reload and
generation changes discard proofs. The entire validation generation, including
holders, dictionaries and list enumerators, is held weakly: it can be collected
when nothing else needs it. This avoids indirectly keeping retired assets alive.
Only path/provider-index metadata is retained strongly; a collected proof causes
native rediscovery. A managed collection test verifies retired holder/dictionary/
string objects are reclaimable. Routes never own returned Unity assets.
Unknown lookup hooks, custom dictionary comparers, unsupported bounds, workers
and reentrancy retain the original lookup. Legacy experimental deferred audio
keeps its native audio path when coenabled, avoiding whole-holder eager exposure;
C10 owns its broad readiness replacement. No new deferral is introduced here.

Exact GOG game hash is
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Native source references: `artifacts/ecosystem-next-20260910/c07/research-native/ContentFinder.cs`,
`c08-corrections/ModContentHolder.cs`, `c00/source/GOG-ModContentPack.cs`.
Pinned YaOpt `ab60621f` source is under
`artifacts/ecosystem-review-20260909/sources/yaopt/Source/YaOpt/Helpers/ContentManager.cs`.
YaOpt records filesystem/Resources/bundle routes but repeats whole bundle search
on bundle hits and stores missing signs. This implementation independently
records exact successful bundle names and leaves missing queries retryable.

All raw C09 evidence stays in `artifacts/ecosystem-next-20260910/c09/` and fixture
captures. Initial seven hashes and three absences match the assigned endpoint.
Only isolated GOG functional operations are permitted while the owner uses the
PC; no timings from these runs establish performance.

## Exact source, packages and checks

Tested final core and observer source is
`8aeaa31a9640d449eb41631d6a8cf25681ad941a`. Later handoff documentation does not
change either package. Both builds passed against the exact GOG assembly above.
Core DLL SHA256 is
`130729556b06276d1441eb5e780bc042ed8b34d24e50e37402aaa5aa9bbd7a8c`;
observer DLL SHA256 is
`9c70211e70be668c99705380a5f9d0abdb55503d449e22824c76cb6d07f59b33`.
Packages and adjacent build receipts are under
`artifacts/fixture-package/8aeaa31a9640d449eb41631d6a8cf25681ad941a/` and
`artifacts/fixture-menu-observer/8aeaa31a9640d449eb41631d6a8cf25681ad941a/`.
No texture helper was built, deployed or invoked; its deployed executable was
absent throughout. Preserved C08 helper evidence is unchanged.

Focused final verification passed **32 managed tests and 68 Python fixture tests**
(`focused-06.log`; TRX
`artifacts/fixture-tests/6c3444762db94beeb09016cc6a7efef7/features.trx`). The existing
`RepeatCostIncludesVersionProofAndRuntimeGuard` performance test was explicitly
excluded. Tests cover exact generic method bodies, prepatch diagnostics branches,
provider/dictionary mutation and replacement, external callback invalidation,
native folder independence, exact C08 hook admission and weak-reference lifetime.
Earlier compile/test failures remain in their logs; the intermittent Windows
fixture temporary-directory rename failure was not worked around by changing the
harness. The final focused command passed unchanged fixture tests.

A bounded read-only native-route review identified callback and indirect-lifetime
risks, which were corrected by this orchestrator. That reviewer is stopped.
This supporting review does not replace the steward's independent review.

## Captured functional evidence

The selection uses 13 frozen packages: required infrastructure, Core and installed
DLCs, Vanilla Expanded Framework, Pmusic, Vanilla Events Expanded, Vanilla
Furniture Expanded Architect and Vanilla Textures Expanded. With candidate and
observer, 15 packages are active. YaOpt is absent; other replacement suppliers
were not selected. No unrelated supplier-suite files were changed. Exact order,
settings, source/package identities and feature receipts are in
`artifacts/ecosystem-next-20260910/c09/handoff-evidence.json`.
Selection SHA256:
`98d1f41451945b078595d75fc4e47eb5a90d1b65f44dcb3ce885b615844ff47f`.
All started runs used functional purpose, independent menu observation and actual
normal exit. A menu pass alone is not treated as feature success.

| Label | Core / observer source | Result |
| --- | --- | --- |
| `c09-natural-functional-01` | `6c3fe2fa` / `6c3fe2fa` | Automatic test passed, exit 0; 109 routing checks passed. |
| `c09-lifecycle-combined-02` | `6c3fe2fa` / `062f6489` | Automatic test passed, exit 0; routing qualification **failed**, preserved below. |
| `c09-lifecycle-combined-03` | `05b4b157` / `05b4b157` | Prepared offline only; never launched. No process/start/capture/exit verdict. Superseded by lifetime correction. |
| `c09-lifecycle-combined-04` | `8aeaa31a` / `8aeaa31a` | Automatic test passed, exit 0; **140 routing checks passed**. |

Final run 04 proves repeated successful routes and native identities/winners for
six filesystem textures across three providers, four filesystem audio clips
across two providers, three Resources textures, three bundle textures, three
bundle audio clips and two VEE bundle shaders. Natural Resources audio has three
passing samples in run 01, including Entry_Ambience_Full and two Planetfall clips;
run 04 sampled zero in that family. That earlier real-content evidence belongs
to source `6c3fe2fa`; it is not represented as final-source repeated coverage.
All family receipt counts, including zero counts, are preserved.

No naturally installed mod-level Strings source exists in this frozen selection.
Run 04 therefore uses **two temporary native providers over real, unchanged Core
English files**. Native ReloadAll reads and publishes 70 strings per provider;
distinct object references, provider priority/order/removal/replacement and a
28-entry native folder result are checked. This proves native file-loading and
routing behavior on real strings with synthetic providers, not natural root
Strings-mod compatibility. If independent acceptance requires a naturally authored
root Strings mod, select one in a future isolated GOG functional variant and
repeat these direct/global/folder checks; that natural-provider coverage remains
open. Do not claim the absent natural family was sampled.

Final run also checks ordinary missing retry and dictionary mutation; exact
native/global/direct/folder identity, order and duplicates; Resources callbacks
that publish a holder or a new hook, throw, retry or reenter lookup; real VEE
`haze.shader` bundle Unload(false), native handler reload and borrowed shader
survival; mutable extension invalidation; and destruction/replacement of only
observer-owned textures, including direct worker access. No borrowed asset is
destroyed. Native and candidate reported-missing messages match exactly at
Player.log lines 74–75. The original methods with routing disabled provide the
native comparison inside the same process; these are correctness comparisons,
not independent performance-control runs.

Run 02 exposed C08's `PreparedQualityRuntime.ReadOne` prefix causing complete
routing refusal. The correction admits only that exact in-package prefix on its
exact closed Texture2D holder target. It carries the same idempotent exposure
bookkeeping through external route hits, without admitting arbitrary hooks or
removing C08 protection. The final combined run admits routing and passes.

Combined run 04 explicitly enables assetRouting, preparedTextures, pngCache,
firstBuildImages, staticAtlases and atlasBatching. Its native DDS-heavy workload
passes 3,016 DDS files through ordinary loading with zero reload fallbacks/errors;
PNG decode/cache calls, hits and writes are zero. It does **not** establish PNG
first-build/cache reuse or changed-quality output. Existing atlas qualification
passes eight checks, compares two natural groups to native rendered mip/mesh
results, and reports zero actual restored groups among those selected groups.
Private atlas lifecycle checks also pass. Earlier C05–C08 evidence and limitations
remain authoritative for their own capabilities; this coexistence run does not
reaccept them or their carried in-game visual checks.

## Restoration and ownership

All 11 C09 transactions were rolled back through the first transaction
`20260911-162049-5a29a7ce`. Seven original endpoint hashes and three absences match;
the metadata audit passes. Evidence:
`artifacts/ecosystem-next-20260910/c09/restoration.json` and `restore.log`.
The restored fixture is GOG generation
`gog-rev573-20260910-194017-cf1a1eb0`, manifest
`b044a0670562480e281cc95a542f664bec66b7e5703545cbd528f334203bd2ef`.
Its original core DLL/source (`e5075750…` / `ba17b033…`) and observer DLL/source
(`b1fa3c65…` / `3d86b163…`) are restored. These old deployed identities are distinct
from the tested C09 packages above. Captures and packages remain preserved.
The prepared label, Wake-Up settings override and deployed helper are absent.

Exclusive editing/build/deployment/testing ownership returns to the steward.
Fixture operations, builds/tests and the read-only reviewer are stopped. The
normal Steam game outside the fixture was left untouched. No parent timer/state
was edited and no successor was dispatched. No merge, push or publication.

## Remaining acceptance work

Steward independent coverage, source, correctness and compatibility review remains
required; this report is a functional handoff, not self-acceptance. Natural root
Strings-provider coverage and final-source natural Resources-audio resampling are
explicit limitations above. C08's in-game windows, scene appearance and native
comparison/reset/restart checks remain required for combined GOG correctness before
release; companion row 6c remains optional post-release.

Performance is untested and prohibited until a new explicit unattended window.
Later measurements must compare matched native/control and candidate forward/reverse
runs, binding source/packages/settings and content order. Include all route proof
validation, invalidation and rediscovery after weak proof collection, successful
discovery work, startup, entry/first-use and memory. Fewer holder or LoadAsset
probes alone do not prove useful gain. C01–C08 and combined measurements remain
separate obligations. C09 does not justify YaOpt removal before the remaining
agreed capabilities are accepted.
