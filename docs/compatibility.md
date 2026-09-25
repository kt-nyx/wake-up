# Compatibility and integration choices

Wake-Up keeps independent loading work available when another mod owns a different
part of loading. It disables only an affected operation when its safety checks fail.
It never changes another mod's settings or removes its patches. Compatibility does
not promise faster startup: the [0.4.0 measurements](release-notes-0.4.0.md) include
small added costs beside several loaders.

Corrected 0.4.0 fixes reported YaOpt worker rebuilding and Adaptive Storage
Framework inheritance conflicts. Build `3ac8973a` passed a targeted Windows menu check
with YaOpt 1.1.4, Image Opt 0.1.13 and full ASF at `31ee88b46651cfbe37da3f89620bc0b907165cc0`.
Expected XML output, all eight YaOpt worker hooks and ASF's inheritance hook were
present, with normal exit and no exception or XML-loading error. The first
in-progress Harmony rebuild interval is verified offline; the live admission
already saw installed YaOpt hooks. This is not a visual texture, gameplay or
performance guarantee. The earlier archive contains the defects; other live
results below remain historical evidence for their exact builds and collections.

These decisions cover the exact acquired distributions inspected in the September
2026 campaign. **Native** means RimWorld's ordinary implementation. A **guard** checks
that the loaded code and callbacks still match the behavior an integration expects.
An **exact adapter** supports that inspected implementation; a later supplier update
does not automatically inherit approval. Binary, module and method checks are in
[loader policy](../src/WakeUp/Compatibility/LoaderSupplierPolicy.cs),
[image policy](../src/WakeUp/Compatibility/ImageSupplierPolicy.cs) and
[lifecycle policy](../src/WakeUp/Compatibility/LifecycleSupplierPolicy.cs).

## Audited mod and tool decisions

The table describes product policy and source review. The evidence section below
separately identifies live tests. An unavailable or obsolete package has not been
qualified merely because it appears here.
| Mod or tool | Recommended behavior and implementation boundary |
|---|---|
| Faster Game Loading, original 1.6 | Combine independent reflection, multi-result XML queries, translation and resource lookup with the supplier. Yield its type/leaf lookup and image production. Its persistent failed-query memory also requires narrower refusal of affected single-result XML rewrites. Preserve its real atlas, audio and constructor callbacks. It is a current acquired 1.6 target, not an assumed legacy-only package. |
| FGL Continued stable | Same operation-level approach, qualified against its own DLL. Retain independent reflection and the XML workers that remain valid. Its patch-file query guard differs from Preview; a common package ID is not a common contract. |
| FGL Preview | Admit its narrower safe XML scope where verified, retain unrelated work, and leave its image production intact. Do not advertise the acquired Preview/Image Opt/compatibility-patch tuple as repaired by Wake-up. |
| YaOpt 1.1.4 | Yield its occupied type and XML operations and top-level lazy resource route. Retain independent reflection/query work and the native bundle lookup that YaOpt itself still calls. Do not create a competing lazy loader or infer completion from an empty supplier queue. |
| Adaptive Storage Framework / PostInheritanceOperation | Preserve the native `XmlInheritance.Resolve` call and `ResolveXmlNodeFor` body so its post-inheritance hook can attach. Removed retired Wake-Up inheritance injections; full prepatch preservation is verified offline, and the targeted corrected-build Windows check above confirms its hook installs. No new speed claim. Its parse hook still makes Wake-Up's optional processed-XML cache yield through the existing guard; no supplier allowlist or cache bypass. |
| WOWGAG | Select XML replay and content-preload ownership separately. Omit Wake-up's conflicting observers early enough that they do not make WOWGAG refuse its own feature. Preserve independently admitted searches, translation and resource work. An explicit Wake-up preference gives an actionable user choice; it does not change WOWGAG settings or unpatch it. |
| Missile Girl / Gagarin | Keep the exact private-parse integration and ordinary rebuild helpers. Yield competing whole-document replay and conflicting constructor streaming. A correct integration need not produce a speed improvement on every workload; the initial overnight collection did not show one. |
| Original RocketMan and unavailable forks | Prioritize the obtainable Missile Girl 1.6 successor. The acquired original targets older versions; no speculative 1.6 port. Choose one distribution where plugin assembly names collide. Wake-up stepping aside does not repair duplicate supplier assemblies. |
| DefLoadCache | Keep independent ordinary work, but do not import its cache or trust its metadata-only fingerprint as current source content. Give a separate persistent advisory for the exact unsafe source-skip-on/patch-skip-off configuration, without pretending Wake-up disabled a feature. Do not automatically repair its settings or caches. |
| Hyperdrive | Let its constructor finish eligibility checks before Wake-up adds relevant observation hooks. Keep independently admitted query helpers even when all native workers are occupied. Do not integrate its private index or imply its query semantics are certified merely because startup succeeds. |
| FastLoader | Allow only the exact outer reload callbacks so Wake-up can handle native image misses/off paths. Supplier raw hits bypass the inner Wake-up path naturally. Do not share or recapture its raw cache. Refuse explicit prepared-quality replacement while its atlas output can be reused; keep exports and saved choices. Author-release and repository binaries are distinct from the inaccessible Workshop build. |
| Image Opt | Let it produce textures. Keep ordinary resource routing, which re-reads the current holder entry. Allow Giddy-Up's optimized readback only for a ready source under the exact managed/native completion contract. Never join its workers, flush its queue or infer readiness from texture dimensions. |
| Graphics Settings+ | Let its actual replacement loader own image choice and quality. Its DDS checkbox does not restore the native loader. Preserve independent routing, ready readback and unrelated code/XML work; explain what the user must change if they want a native image setup. |
| Image Opt/FGL compatibility patch | Preserve its pixel-read, resource-path, audio, UI and copy-lifetime repairs. Do not borrow its CPU copies as Wake-up output or alter its settings. Its additional completion hook does not automatically qualify a whole supplier combination; unsupported completion remains guarded. The deeper review rejected an early-loading-off complete combination admission for this candidate because its resettable queue snapshot cannot establish prior loading history; this is a qualification limit, not proof that reading pixels from a completed texture itself is corrupting. |
| Loading Progress | Preserve its screen and the complementary repaint/image integrations. Support the conditional background mode described below. Its default deferred in-game replacement is not admitted as a harmless observer. |
| RimThemes | Yield Wake-up's drawing and native summary when its custom loader is active or cannot be safely ruled out. Retain separate timings/acceleration. Admit only the exact harmless queue/renderer observers for background work, with real lifecycle proof. Preserve its real theme-loading callbacks. With LP also present, ask the user to choose one external screen. |
| No Modlist on Loading | Default to its DLC-panel-preserving appearance. Offer an explicit Wake-up choice to hide the whole summary. Preserve the requested setting and warn once if the automatic policy yields it. No foreign patch removal is needed. |
| Load in Background | Preserve its ownership of the saved background preference and yield Wake-up's affected shared background group. It is not merely a harmless observer. Keep unrelated improvements and name the reason clearly. |
| Startup Impact original / Continued | No new adapter for the acquired obsolete original or unavailable Continued binary. Preserve independently guarded work. LP's declared conflict with Continued is a supplier-pair problem, not repaired by disabling Wake-up. |
| BetterLoading / RIMMSLoadUp | No ports or unchecked allowances for the acquired obsolete builds. Exact changed targets still receive normal guard refusals. Do not blacklist hypothetical maintained descendants by package name alone. |
| Prepatcher | Required infrastructure. Validate the final loaded methods and actual supplier publications; preserve reload generation and exact identities. Do not treat a failed rewrite callback as a transaction that automatically rolls back. |
| PurePatcher + Kingfisher | Complementary framework/gameplay work. The combined GOG assembly reload and retained operations passed; no whole-framework ban. Do not retain method references from an earlier discarded generation. |
| Performance Fish / named 1.6 fork | Acquired copies are the same older 1.4/1.5 implementation; the repository name is not a proven port. No new legacy support claim. Fish's thing-list bookkeeping collides with the acquired Kingfisher replacement: choose a provider; Wake-up cannot repair that pair by yielding. |
| Savegame Shrinker | Complementary user-selected save operation. Preserve its controls and backups; never auto-shrink a save or invoke it as a startup optimization. |
| RimSort / RimPy | Supply accurate mod metadata and respect actual converted-file selection. No runtime adapter, external database edit or automatic converter invocation. |
| todds / ToDDS | Treat DDS as selected input through the active loader. Preserve source/override precedence and invalidate Wake-up output on actual selected-byte changes. Do not resurrect removed PNG files from a cache. |
| RWMS | Do not advertise its historical folder-ID sorting workflow as an automatic 1.6 integration. This is an external-tool limitation, not a package conflict with Wake-up. |
| imutate | No integration: the inspected executable is an unfinished conversion scaffold. |

No newly audited external mod requires an unconditional Wake-Up package blacklist.
Metadata still excludes duplicate/old Wake-Up installations. Two other mods can
conflict with each other even when Wake-Up yields its affected work.

## Choices that matter to users

Loading Progress 0.16 keeps its loading screen and repaint improvement. Background
cooperation requires the user to disable **Patch in-game renderer regeneration to
keep the loading window responsive** in Loading Progress. Wake-Up checks the exact
code, current false setting and completed startup before starting background work,
and checks again while it owns that work. Its default deferred mode remains refused;
Wake-Up does not replace that scheduler. A change of mode restores the current
background preference and preserves the guard that stops unfocused gameplay.

RimThemes keeps its custom loader when active. Disabling that loader can permit
Wake-Up's display, while RimThemes' real theme-loading callbacks still prevent
Wake-Up from replacing their scheduling. With RimThemes and Loading Progress
present together, choose one external loading screen.

No Modlist on Loading preserves its DLC panel by default. Wake-Up's explicit
**Hide summary with No Modlist on Loading** choice can hide the complete summary.
Both choices preserve the native loading dialog and tip; the saved choice is kept.

The inspected FastLoader author release has a native cache limitation: its builder
records English, but the next early XML check can compare an uninitialized language
and invalidate the cache. The unsuccessful default warm launch is retained.
A working inspected choice is FastLoader enabled, XML cache off, texture cache on,
atlas reuse off. Its own builder and a genuine subsequent raw-cache hit passed.
This gives up its XML cache; Wake-Up does not repair or change the supplier setting.
The inaccessible Workshop build and default all-cache mode are not qualified by it.
When a FastLoader raw hit succeeds, Wake-Up's inner image path is bypassed normally.
Native misses can use Wake-Up's optional image cache. Atlas ownership independently
blocks competing prepared-quality replacement, not ordinary PNG reuse or exports.

Image Opt + Graphics Settings+ + Giddy-Up has bounded ready-texture readback proof.
The full acquired FGL Preview + Image Opt + compatibility-patch combination remains
unqualified: its resettable early-load state cannot establish the required loading order. This is a qualification limit, not proof the whole combination corrupts
textures. Removing a declared dependency is not a supported workaround.

## Live evidence and its limits

All new live tests used the isolated Windows GOG fixture. The installation labels
itself rev573, while the admitted assembly reports **1.6.4871 rev574**. Its SHA-256 is
`4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
Linux/Deck remains outside the tested scope. Functional tests verify behavior;
measured startup and long-running gameplay are separate claims.

| Integration | Bounded result and retained run labels |
|---|---|
| FGL original 1.6, Continued stable, Preview; YaOpt; WOWGAG | Expected XML worker output, retained operations, genuine notices and normal startup passed; final ordinary comparisons are recorded in the release notes. |
| Missile Girl / Gagarin; YaOpt bundle routing | [Phase 2](compatibility-phase-2.md) proves actual warm Gagarin reuse and native bundle lookup. Earlier overnight matched Gagarin measurements showed no useful incremental gain. |
| FastLoader author release / repository | Native PNG output construction and reuse passed separately for both exact binaries; repository pair `ov-final-fastloader-repo-cold/warm`. Native supplier texture builder/hit pair `ov-fastloader-texture-only-build/warm` restored two entries with matching objects and stored pixel data. `ov-fastloader-native-warm` failed through the supplier's language invalidation and is not qualification. |
| FastLoader quality settings | `ov-fastloader-atlas-on-quality`, `ov-fastloader-texture-off-quality`, `ov-fastloader-master-off-quality` verify refusal only when the supplier can own atlas output. |
| Image Opt / Graphics Settings+ / Giddy-Up | `ov-giddy-native`, `ov-giddy-imageopt`, `ov-giddy-imageopt-gs`: each checked 392 real startup readbacks, 9,831,936 source pixels, identical offset hash and no mismatches, fallbacks, pending-source or completion-contract refusals. These verification runs are not performance samples. |
| Loading Progress background mode | `ov-lp-default-background-ack` refuses the default deferred mode. `ov-lp-isolated-ack` admits all four operation groups in the selected false mode. `ov-lp-isolated-save-hold` completed while actually unfocused and held saved tick 602 unchanged for 52.237 seconds; current false preference and engine state restored. `ov-lp-isolated-world-preference` preserves a current false-to-true preference change. `ov-lp-isolated-render-cancel` passes actual renderer cancellation/retry. `ov-lp-deferred-error-retry` verifies native callback failure cleanup, a following callback and a second actual save load. The latter two are focused tests. |
| RimThemes | `ov-themes-active-game` and `ov-themes-disabled-game-qualified` preserve the selected screen through actual colony/save/reload. `ov-themes-render-cancel` passes cancellation/retry. `ov-themes-save-hold-own-window` completes with actual focus absent, keeps tick 601 unchanged for 52.480 seconds, and restores current false preference. Earlier blocked/foreground-only attempts remain failures, not unfocused evidence. |
| No Modlist on Loading | `ov-dlc-preserve-game` and `ov-dlc-hide-game` show the actual Core-plus-five-DLC panel respectively present/absent, with loading dialog/tip and gameplay/save/reload preserved. |
| Hyperdrive | `ov-hyperdrive-timings-before/after` preserves the supplier's native parallel XML, thread count, prefetch and XPath setup on either side of Wake-Up's constructor. Tiny XML output/startup proof does not certify all supplier query semantics or performance. |
| DefLoadCache | `ov-defload-setting-advisory` proves visible advisory for cache enabled/source skip enabled/patch skip disabled. No foreign cache or setting is repaired. Fresh cache-miss startup does not qualify arbitrary cache hits. |
| PurePatcher / Kingfisher | `ov-rewriters-combined` proves the actual rewritten-assembly reload generation, expected XML output, retained Wake-Up operations and normal startup. Combined gameplay is recorded in release notes. |
| XML Extensions | Historical `c12-mixed-native-01` / `c12-mixed-candidate-01` at release source `12e616dcb2ec7ff3c94de63ea6a97da7d9612262` independently compared ordered XML/definition records and 9 actual eager-query hits. The same index/query mechanism remains; current focused tests cover ordered node identity, mutation/removal, native errors and later foreign-hook fallback. This reuses identified evidence rather than claiming a new live run. |
| Native DDS/PNG selection | `ov-source-selection-clean` verifies actual native holder cleanup/reload: PNG construction/hit, DDS precedence/replacement, removal falling back to the remaining PNG, actual changed bytes at the same timestamp, and missing sources staying missing. An earlier observer setup retained populated holders and failed; it was corrected without changing product code. |

Private captures retain exact product, observer, deployed collection, settings,
source hashes and normal exit receipts. They are not distributed with source.
Accepted work spans multiple identified builds; release notes identify the final
combined candidate and actual distributed DLL. Unknown source/code and untested
supplier settings continue through ordinary operation-level fallback.
