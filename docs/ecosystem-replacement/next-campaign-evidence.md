# Next replacement campaign: research and evidence

**10 September 2026 — supporting research for a proposed, not approved plan.**

Read the [proposal](next-campaign-proposal.md) and [replacement matrix](next-campaign-matrix.md) for recommendations. This record establishes what those recommendations rest on. Source inspection establishes an implementation mechanism, not its speed, binary equivalence, or compatibility on every modlist.

## 1. What was actually inspected

The working checkout was clean on `main`, **`5299ff4974367e5492181f2f72ccd20f91e51629`**, when research started. The requested `docs/ecosystem-replacement/` directory was absent there. The later campaign is preserved at **`fe687d4899696dcfffb1de2148f857ad94001d44`**, branch `codex/wake-up-f1-functional`; `git merge-base main fe687d4` returns `5299ff4`. Reading those objects did not require switching the shared checkout. That was the initial read-only research state. During the subsequently authorized documentation review, the steward created `codex/ecosystem-next-plan-review` directly from fe687d4, preserving all 146 commits beyond main and carrying the proposal onto that ancestry. Main was not merged or moved; no prior product edits were uncommitted. See [current state](../current-state.md) for the latest documentation/ownership snapshot.

In this document, **reviewed baseline** means `fe687d4`, the preserved source baseline, with documentation-only revisions on the review branch. Paths prefixed `fe687d4:` can be read with `git show fe687d4:<path>`. Those historical Git paths remain valid after the reports were moved into [the prior-campaign archive](../archive/ecosystem-replacement-20260910/README.md); use that archive for current clickable report links.

Read at their relevant identities:

- Working-tree `AGENTS.md`, `docs/current-state.md`, `docs/architecture.md`, and `docs/development.md`, followed by the later versions of the three reference documents at `fe687d4`.
- `fe687d4:docs/ecosystem-replacement/master-plan.md`, `independent-revamp-audit.md`, `retained-features.md`, R1, R2, R3, R4a, R4b, R4c, R5 and RC2 functional qualification reports.
- Earlier S04/S05/S06 native-flow and feasibility findings, S09 first-build evidence, S10 dependency/preparation decisions, S11 routing/on-demand findings, S14 warnings, the archived master plan and `docs/archive/results.md`; retained S01/S02/S03/S07/S08/S13 behavior was cross-checked through current code and the later reports. Earlier evidence remains dated evidence, not new approval requirements.
- The original [ecosystem report](../../artifacts/ecosystem-review-20260909/report.md), [23-entry comparison](../../artifacts/ecosystem-review-20260909/comparison.md), receipt files and selected linked source implementations.
- Current-baseline `WakeUpMod`, `PngCache`, `PreparedTextureRuntime`, `AssetRoutingRuntime`, `DeferredAssetSet`, and `BackgroundLoadingSession`, plus their initialization, selection, compatibility and lifecycle descriptions. In particular, `PreparedTextureRuntime.TryResolve` accepts native-output identity choice zero; exports never reach it. `AssetRoutingRuntime.TryGet` admits only Texture2D and returns an already published holder object. Neither implements the broader capability its name might suggest.

The original supplier directories are **archive extractions, not independent Git repositories**. An initial `git -C <snapshot> rev-parse` climbed to Wake-Up's parent repository; its result was discarded. Supplier identities below come from the archive receipts and live primary repository queries, not that command.

## 2. Ownership and qualification checked again

The app reports **Revamp Steward** (`01a08711-7899-7ff3-a1b5-4a0b105ba661`) idle. **RC2 fixture functional qualification** (`01a08d37-5053-7163-8fcf-054ec4b7c373`) is idle with its sole turn completed. The older **Revamp Windows functional qualification** task has an earlier completed handoff; it is not the latest run identity.

`artifacts/ecosystem-review-20260909/steward-state.json` records no active team, accepted F1 at `fe687d4`, and returned ownership. Its old `nextPhase`/`nextBaseCommit` fields still name R2 and must not be replayed as instructions. The actual saved `wake-up-slice-steward` automation is **PAUSED**, attached to the old steward. No task was messaged or started, and no timer was changed for this proposal.

Read-only process inspection found no RimWorld process. `.rlo-test-instance/prepared.json` is absent. Rehashing the current fixture files confirmed:

| Layer | Verified identity |
|---|---|
| Game | Steam `steam-rev590-20260910-020604-cb37ef1b`; Assembly-CSharp SHA-256 `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |
| Frozen manifest pointer | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93` |
| Deployed private core | Source recorded as `a70873e300c51930ebdff63a43c051257d6bba40`; actual DLL SHA-256 `9c2dcec8cbe648ee78350ebb4460c7d3a3ba10412db6a4f8957f9eb51606c4cd` |
| Restored preferences | SHA-256 `b34d9738ee08fb3762d5e04a5f4bf7b1152fe7f69fcda878564f231bb618db2a`, matching F1 final-state record |
| Restored mod order | SHA-256 `9b3ed71fff9bdf5a6dea84486ee5209646ef98c0dcb18e30911698a59e6e96d7`, matching F1 final-state record |
| Original rc.2 package | R5 build source `a9aed0e43bb10e032d75d4b6b843772b0f2a466a`; its original DLL differs from the repaired fixture core. No new package inspection or rebuild was performed here. |

All seven canonical `results/<label>/run.json` files were rehashed against [the F1 identity index](../../artifacts/rc2-functional-20260910/run-identities.json). All matched, recorded `automaticTestPassed=true` and exit code zero, and had no gameplay-test pass. The [F1 final-state record](../../artifacts/rc2-functional-20260910/final-state.json) matches the checked profile/core. These are verification of preserved captures, not new live tests.

The seven menu successes include feature refusals and probe failures. The corrected runs establish one native JPEG preparation/restoration family, later startup reuse, DDS **file exports**, bounded compiled audio first-use/cleanup, and texture routing. They do not establish visible exported quality in game, audible playback, ordinary colony compatibility, a complete profiler or independent XML/language caching. Supplier coexistence run 07 refused streaming XML, audio and Wake-Up's display in the relevant combinations. That is fallback evidence, not replacement success.

## 3. Primary source refresh and confidence

Public GitHub branch heads were queried again through the GitHub API on 10 September. The following heads still match the original extracted snapshots. This supports reading those local source files as current **repository source**; it does not equate them with a Workshop binary.

| Supplier | Pinned source / primary reference | Rechecked implementation basis |
|---|---|---|
| FastLoader | [`d95ae707`](https://github.com/solaris0115/FastLoader/tree/d95ae707661e46ff50a04b39d90173f6b90ed1e6) | `FastLoaderPatches`, `LanguageBinaryCache`, `DefInjectedBinaryCache`, `StaticAtlasCache`, original runtime/source-map analysis |
| FGL Continued | [`0132c10a`](https://github.com/mushroomTW/FasterGameLoading---Continued/tree/0132c10a3185f578815fb1dba012b7b24f78d5e3) | Deferred graphics/icons/sounds, adaptive atlas work, earlier type/XML-miss findings |
| YaOpt | [`ab60621f`](https://github.com/szszss/YetAnotherOptimizer/tree/ab60621f071fbee3e4f1a92bb163c2760b8605bf) | `ContentManager` provider types, Resources/bundles, pending completion and DDS restoration; earlier translation/search analysis |
| DefLoadCache | [`f674263b`](https://github.com/FluxxField/rimworld-defload-cache/tree/f674263b5d17e5a2191828d2003cff61e2b5b1c0) | `CacheHook` early bypass, restore, native-operation loop and checkpoints; original fingerprint analysis |
| Hyperdrive | [`3a96ce59`](https://github.com/vopaga/rimworld-hyperdrive/tree/3a96ce59648ae99880482a42d39cb44e26d84f74) | `OptimizedStartup` byte warming and worker ownership; original ordinary-mod/direct-patcher separation |
| Loading Progress | [`bcd71baf`](https://github.com/ilyvion/loading-progress/tree/bcd71bafd994b206ed644df0ae56583beb047646) | Stage inventory, StartupImpact integration/session storage, callback and scheduling analysis |
| Graphics Settings+ | [`db7e9804`](https://github.com/RealTelefonmast/GraphicsSetter/tree/db7e9804b4a80caacddac8560a49c671918687b7) | Original shipped 1.6 DLL decompilation; independently read settings groups and UI/loader behavior. Source project is not asserted available. |
| todds | [`bf78b230`](https://github.com/todds-encoder/todds/tree/bf78b2303b00ad0f99ffb409a4827eb305650820) | Archived encoder pipeline and original filters/format analysis; current author notice confirms archival |
| Performance Fish | [`a9997200`](https://github.com/bbradson/Performance-Fish/tree/a99972001e02d553a738611726cd9577ff41f86e) | Original reflection/DDS source review; historical 1.4/1.5 scope, not an unnamed 1.6 fork |
| Startup Impact | [`f70bddb4`](https://github.com/AUTOMATIC1111/StartupImpact/tree/f70bddb49460783a036df2eaff5c61b3941d7dd6) | Historical original implementation; current Loading Progress carries a separately inspected derivative |
| BetterLoading | [TBXin `c01f0d70`](https://github.com/TBXin/RimworldBetterLoading/tree/c01f0d70d0b0a3c661fc712428b61f06e89351f6), [SlavRim `ed5a2853`](https://github.com/SlavRim/RimworldBetterLoading/tree/ed5a285322a2381084cfea81bf452ee9c7e75050) | Refreshed TBXin head; directly read newer-fork `StageReadXML` and audit's type-memo finding. Historical versions, not verified current 1.6 suppliers. |
| Kingfisher | [`33158852`](https://github.com/realloon/Kingfisher/tree/33158852e1497114b233ec5f00245d72a234bfb4) | Current head matches original loading-scope screening; no loading-cache counterpart established |

Two heads changed:

- **Missile Girl:** [`f09c39f0`](https://github.com/ViralReaction/MissileGirl/commit/f09c39f0e9a026d164bc254cc4864db12ec5eaef) is one commit after `3170983d`. The primary compare lists warmup-popup/pause source changes in Cosmodrome and three binary changes; no Gagarin source file changed. The reviewed Gagarin mechanism remains the source basis. This does not verify those new binaries or justify gameplay-removal advice.
- **RimSort:** [`89cf5f45`](https://github.com/RimSort/RimSort/tree/89cf5f45a4728a4d663bbf709fc6dfb90ced130f) is five commits beyond the original `3b13dfc3`; the compare lists metadata, mod-panel/search tests and lockfile changes, no todds integration change. Its preparation role remains supported by the original inspected controller/wrapper. The audit's intermediate `36f8626` is no longer current head.

The [FastLoader README](https://github.com/solaris0115/FastLoader) describes binary language/injection caching broadly. Actual `LanguageBinaryCache.UseDefInjectedBinaryCache` is **false**, and the injection-prefix methods merely profile then continue natively. Thus keyed/string reuse is wired; the separate binary DefInjected cache and fast applier are not active at those inspected hooks. Persisting injection records remains a requested useful addition, with this distinction visible in the matrix.

FGL Continued's `DeferredLoader` performs real deferred graphics, icon recovery, map-mesh refresh and sound resolution. Its `AdaptiveAtlasBaker` changes bake batches toward an 8 ms slice target. It is more than a texture lookup cache. YaOpt's `ContentManager` covers filesystem texture/audio/string providers plus Resources and bundle routes (including shaders); its lazy pending entry is removed before completion succeeds, and later mip work has separate sampling/atlas implications. These are implementation lessons, not defects newly reproduced in game.

Loading Progress's stage enum covers mod classes, input/combination/TKey/patches, inheritance/Defs, languages, cross-references, implied definitions, resolution/error checks, callbacks, static constructors, atlases and GC. Its integrated profiler saves session reports. A two-boundary XML timer cannot replace that user-visible breadth.

## 4. Descriptions and unavailable implementation

Current author pages were read for [Image Opt](https://steamcommunity.com/sharedfiles/filedetails/?id=3543873568), [No Modlist on Loading](https://steamcommunity.com/sharedfiles/filedetails/?id=3618480405) and [Load in Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3696524379). Image Opt documents parallel PNG/JPEG processing, DDS reads, DDS.zstd storage/regeneration and Windows D3D11 work. Load in Background documents later loading without changing ordinary unfocused pause preference; startup already has native background behavior. No Modlist documents summary removal. These capabilities remain requirements even though their implementation is unavailable to this research.

The original evidence gap for Image Opt, those two narrow mods and RimPy remains. No subscriptions, installs or normal-game files were changed to close it. The community Image Opt source/crash discussion is a discovery lead, not primary proof. The plan calls for checking a lawfully available exact package/source later, while implementing the documented behavior independently. Missing access does not remove a capability or authorize a confident incompatibility warning.

The todds author points to **imutate** as a successor. Its [RimSort tracking issue](https://github.com/RimSort/RimSort/issues/2078) records very early development as of 2 June 2026. The Codeberg page was unavailable through this browser request. No present conversion functionality, license conclusion or readiness is inferred from that old issue. It is an optional future backend candidate, not a plan dependency or excuse to wait.

## 5. Actual RimWorld boundaries that change the design

The current game assembly hash above matches the Steam extracts already preserved under `artifacts/r4a-ordered-reads-types/`, `artifacts/r1-independent-xml/research/`, `artifacts/r4c/language-probe/`, and `artifacts/r3-deferred-20260910/dds/`. Those methods were read directly; no game or inline reflective loader ran. This is Steam-specific inspection. The existing build verifier records GOG target `gog-rev573`, Assembly-CSharp `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`, but the new designs still require corresponding GOG boundary checks before implementation. Recording that reference does not establish new GOG functional evidence.

1. **XML:** `LoadAllActiveMods` schedules content, constructs mods, reads XML, combines/imports into a new document with a node-to-source map, processes TKey and patch diagnostics, applies patches, resolves inheritance and creates Defs, then calls `Complete` and clears state. `LoadModXML` walks mods in order; per-mod file loading already uses two workers plus the caller and indexed results. Moving all of `LoadDefs` onto workers moves mod callbacks too. More workers alone is neither new capability nor established benefit.
2. **Observable ownership:** `CombineIntoUnifiedXML` imports nodes and records their actual `LoadableXmlAsset`. Later custom patches can retain the document or removed nodes. A late whole-document replacement changes identities even if serialized text matches. Early private construction can avoid that problem, but only if its publication boundary and foreign observers are controlled.
3. **Languages:** `LoadedLanguage.LoadData` sets its loaded flag early, uses native virtual-directory selection and duplicate-file registration, queues icon callbacks, reads groups in parallel, then parses/applies their records. Keyed parse failure and injection partial failure differ. WordInfo, legacy folder warnings, lazy English fallback and later language switching remain observable. Replaying a menu snapshot of mutated injection records is not equivalent to caching parser outputs.
4. **Types:** the current native type resolver already caches `(typeName, namespaceIfAmbiguous)` results, including misses. XML-construction delegates already cache by type. These are native coverage; Wake-Up should not add a duplicate memo merely to mark a row implemented.
5. **Textures:** current DDS loading allocates authored dimensions/mips, uploads data and ends unreadable. Atlas admission inspects dimensions and masks; completed atlases contain their own copied pixels and coordinates. Later source detail restoration does not fix an already baked atlas. The game already supplies basic DDS support and lazy mod previews.
6. **Background work:** `PageUtility.InitGameStart` and save/world paths have specific scene/queue completion rules. `GetOrGenerateMapUtility`, pocket maps, world-map opening and initial autostart do not share every boundary. Native startup's temporary background permission is not a general later-loading solution.

Unity's [raw texture upload contract](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.LoadRawTextureData.html) requires matching format and complete data, followed by upload; CPU preparation is not completed GPU work. The [streaming manual](https://docs.unity3d.com/2022.3/Documentation/Manual/TextureStreaming.html) describes imported streaming textures, not a demonstrated registration path for RimWorld's runtime DDS files. The inspected player exposes streaming status but no such source registration. R3's [DDS decision](../../artifacts/r3-deferred-20260910/dds/decision.md) explains the actual mip-group, copy and ownership limits. A native plugin remains an engineering alternative, not a proven necessity or proven impossibility.

## 6. Failed approaches and the changed premises required

| Preserved evidence | What failed or remained unknown | Different implementation premise in this proposal |
|---|---|---|
| S04 per-file parsed trees | Reconstructing general XML trees plus storage/validation exceeded native parse cost, even with steady-open storage. | Reuse an integrated selected-input generation; remove duplicate parse/import and dispatch work, materialize once at publication. Do not retry the old per-file node codec unchanged. |
| S06 grouped language records | Warm median 23.24 ms vs 21.23 ms native; 5/8 pairs lost; partial integration only. | Capture native pre-application results during the real load, share source bytes/store ownership, and replay final immutable parsed tables rather than execute a second parser-like record stream. |
| R4c revised language input | 36.387 ms vs 38.182 ms median, but lost 6/12 pairs; no real LoadData integration. | Address repeated capture/merge/materialization and actual Mono lifecycle; do not call another source-decoding tweak a new design. The parent capability remains open if the integrated route loses. |
| R1 whole-prefix replay | 769.6 ms reuse vs 22.1 ms native on the tested real-content prefix; restored too much to skip too little. | Whole-stage early ownership or substantial declared pure stages; no document snapshot per cheap prefix. |
| R4b retained-node mutations | Complete warm 37.204 ms vs 13.194 ms native; only six of 81 operations finally admitted. | Eliminate the need to preserve escaped node aliases by moving before publication; mixed custom regions continue natively unless a reviewed semantic adapter establishes more coverage. |
| S09 PNG IDAT expansion | All eight pairs lost; 12.5-fold enlarged input; consumer decoded another PNG. | Decode once to raw data or compressed GPU blocks, hand that result directly to the engine consumer. Explicit preparation remains a separate benefit. |
| Archived extra workers, inheritance preparation, field stores, cross-ref batching | No repeatable useful complete-startup saving under the tested conditions. | Native-ordered pure byte jobs across stage barriers; integrated inheritance reuse; actual uncached callsite accessor work only. No blind rerun or arbitrary constructor parallelism. |
| Archived DDS packs/read-ahead | Warm I/O/hash changes failed to establish repeatable complete-startup gain. | Group generated processed outputs, optionally compress storage, consume retained bytes once; measure disk reduction separately. Do not repack authored DDS wholesale. |
| Atlas layout | Small/overlapping layout budget did not justify replacement. | Cache complete color/mask payloads and mappings; preserve adaptive first-build scheduling as a separate behavior. Layout-only failure does not reject complete-atlas reuse. |
| R3 DDS alternatives | No runtime streaming source/private group; generic incomplete textures unsafe. | Private consumer-owned residency before generic exposure; evaluate real native resource ownership only if that cannot deliver broad useful coverage. |

These figures are preserved CPU/stage results on their recorded hosts and inputs. They are not new benchmarks, supplier comparisons, or predictions of the next design's performance.
