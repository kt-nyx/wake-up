# C00 baseline and GOG handoff

**10 September 2026 — accepted by the steward as an administrative prerequisite.** Reviewed handoff `2c7c1bdd66c62b048141ef750e6ff736521608b2`; see the [execution ledger](execution-ledger.md) for acceptance limits. The assigned source baseline is clean and preserved. The original isolated GOG runtime is recoverable, and the six inspected XML-loading types have the same decompiled source as the researched Steam versions. The current CLI cannot safely return to that GOG runtime: a small transactional `stage-gog` action must be implemented and checked before C01 live work. No transition, build, deployment or game launch occurred in C00. This is an administrative prerequisite, not feature acceptance.

## Baseline, ownership and actual checks

Dispatch `wake-up-next-c00-aa1a504-20260910` assigned task `01a08d9a-583f-74a1-962a-812341a29cf3` exclusive checkout ownership. Initial HEAD was exactly **`aa1a504d83e756563778850e79a24c89973715ae`** on `codex/ecosystem-next-plan-review`, with empty `git status --short`. Both reviewed plan `f70aa081de2e5a9d50efa48b55d49ab8b27399b6` and preserved implementation `fe687d4899696dcfffb1de2148f857ad94001d44` passed `git merge-base --is-ancestor`. Main remains **`5299ff4974367e5492181f2f72ccd20f91e51629`**. No branch switch, worktree, merge or publication.

The new steward record initially held the matching pending dispatch; it subsequently recorded this task as active owner and cleared the pending action. C00 did not edit that parent-owned record. The old record says complete, with no active task/team and returned ownership. Fresh app snapshots confirmed **Revamp Steward** and **RC2 fixture functional qualification** idle with completed turns. Saved `wake-up-slice-steward` is PAUSED; `wake-up-replacement-steward` is ACTIVE and attached to the new parent. Neither timer nor the old campaign state was changed.

During C00, owner follow-up `wake-up-next-c00-remove-review-20260910` requested removal of the redundant review summary and its navigation references. Separate commit **`34bc0fffe66b182229ac54f98f7f9ff2b7e466c5`** removed that report and the sole documentation-index link; tracked Markdown search found no remaining references. The approved proposal and matrix were unchanged. This cleanup does not change the original C00 dispatch base or operating scope.

Fresh process inspection found no RimWorld, Python fixture operation or dotnet build process. A nonblocking read-only lock of the existing `operation.lock` byte succeeded and was immediately released; no competing fixture lock was held. These are observations, not durable permission to skip the next operation's guards. The bounded source reviewer stopped before handoff; C00 returns exclusive ownership to the steward with its final commit.

Private evidence is under [artifacts/ecosystem-next-20260910/c00](../../artifacts/ecosystem-next-20260910/c00/): `ownership-observations.json`, reproducible read-only `inspect_baseline.py`, `baseline-identities.json`, and `source/findings.md` / `source/comparison.json`. The inspection hashed 28 selected files, with no missing file or recorded-identity mismatch; all 66 inventoried GOG top-level recovery units exist. It did **not** recursively audit their contents or the whole fixture. Source inspection re-extracted only six types with the existing ILSpy CLI. Documentation checks are `git diff --check`, scoped staged-file review and the tracked commit guard; no product test was necessary for this documentation-only handoff.

## Source, installed files and preserved evidence

Hashes below are SHA-256 content identities. A matching file identity proves which bytes are available, not that the game or feature works.

| Layer | Checked identity / limit |
|---|---|
| Active isolated game | `steam-rev590-20260910-020604-cb37ef1b`; Assembly-CSharp `5cf1b5be399d5b1c9c56ca72c9d35b4ecf307feacf5859d04ac5a1aa5926356a` |
| Active frozen manifest | `62d43aac98817efb8cebadafefe24101ddfff5bf193ad4765c6dea3f93017a93`, matching `current.json` |
| Deployed corrected core | Receipt source `a70873e300c51930ebdff63a43c051257d6bba40`; actual DLL `9c2dcec8cbe648ee78350ebb4460c7d3a3ba10412db6a4f8957f9eb51606c4cd`; target remains Steam |
| Deployed helper | `b351d1794b82f2310f643c307d54498693db8c6818380bc6dd5e4141489023e5`; separate origin from core |
| Deployed observer | Receipt source `b1aa6592c807c2ecc68d9fe85a8fd3d98d12a86e`; DLL `8aadc70b7ec54ae7b63a5dcdd3590bb363e67e4eaa62396444af918a05242042`; receipt binds Steam assembly above |
| Current profile | Preferences `b34d9738ee08fb3762d5e04a5f4bf7b1152fe7f69fcda878564f231bb618db2a`; mod order `9b3ed71fff9bdf5a6dea84486ee5209646ef98c0dcb18e30911698a59e6e96d7`; both match frozen GOG seed files |
| Pending work | `prepared.json`, `pending.json` and Wake-Up settings absent; Wake-Up absent from restored seed order |
| Captures and release package | No new capture or package. [F1](../archive/ecosystem-replacement-20260910/rc2-functional-qualification.md) remains evidence only for its recorded runs. Its original rc.2 ZIP is still distinct from the repaired deployed core; C00 did not re-audit the ZIP or seven captures. |

The current and GOG manifests have identical frozen package records and original active order: 309 third-party package records, 315 original active IDs including official content. Three current reversible exclusions remain recorded: `astryl.moderndevtools`, `neachi.charactereditor.facialanimation.eyepatch`, `void.charactereditor`. Preserve their parked files and selection when transitioning; do not restore an old exclusion receipt blindly.

## Exact GOG recovery source

The Steam snapshot's `transition.json` names transaction **`20260910-020635-a5abd2ff`**, purpose `stage-Steam-rev590-preserve-GOG`, and parent snapshot **`20260904-182829-e058641c`**. Parent manifest hash is **`b9b53c8b666ae4d44cb02dc9d84b9ed6441d57b444b115ee1e38009529350f5d`**. Its game inventory hash is **`4d34ae8f0eec5b672b43ae77acea2a52e9581e49a2f4f84003a5fd094544bd97`**; the inventory contains 2,746 file/directory rows outside Mods. Its six official-content inventory hashes and profile inventory hash also matched their manifest. The journal's backup `73` contains the matching old current pointer.

Recover from that journal's numbered backups inside `.rlo-test-instance/recovery/20260910-020635-a5abd2ff/`, using the recorded target-to-backup mapping. Do not treat the snapshot's now-moved `game/` staging paths as a complete game copy. These selected recovered files matched the original inventory:

| Original game-relative file | Recovery suffix | Actual hash |
|---|---|---|
| `RimWorldWin64.exe` | `13` | `4c30e2105b49f2d0130f5d861947fbe82b866042299da48ef1c8cf6979a8564d` |
| `RimWorldWin64_Data/Managed/Assembly-CSharp.dll` | `14/Managed/Assembly-CSharp.dll` | `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28` |
| `UnityPlayer.dll` | `21` | `e0c489f1683609247fede45ea049d30baa4f4542060e308e25c0ec87f6c0fb96` |
| `Version.txt` (`1.6.4871 rev573`) | `22` | `f35a16ca48e588492ca1495bd6b36cea45119ef78e717ae78c21604c014c4f63` |
| `goggame-1094900565.info` | journal-resolved in evidence | `b363fc76e2191366e856f544f6cad77bc1d21734ac028c9e8c1d93881eb9f5e7` |

Recovered Unity CoreModule and IMGUIModule also matched; full paths, sizes and hashes are in `baseline-identities.json`. Current frozen Prepatcher references matched `0Harmony.dll` **`7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`** and `0PrepatcherAPI.dll` **`39a3841d1c61c41d173e5cfb81262fde78db655a0d604a2dd91a5697e9d57300`**. [Verify-LocalReferences.ps1](../../build/Verify-LocalReferences.ps1) already selects this GOG assembly hash/version `1.6.9676.17238` for `gog-rev573`. No replacement reference contract is needed.

## Required small fixture extension before C01 live work

There is **no presently supported CLI return route**. [fixture.py](../../scripts/fixture.py) implements only GOG-to-Steam staging (lines 65–139). `initialize/refresh` requires an already active GOG marker and exact GOG assembly and refreshes normal-source mods/profile (558 onward); it is not a game-restoration command. `rollback` permits only the latest committed transaction (540–550). The latest is **`20260910-175028-032652f3`**, a profile reset, not the Steam transition. Do not unwind later deployment/profile transactions or weaken latest-only rollback.

Assign a focused prerequisite at the start of C01 (or a serial tooling assignment before it): add `stage-gog` to `scripts/fixture.py`, with one exact recovery-transaction selector. Its proposed invocation, **not an existing command**, is:

```powershell
python scripts/fixture.py stage-gog --transaction 20260910-020635-a5abd2ff
```

The implementation should reuse existing path checks, process guard, operation lock, staging, `swap` and `recover`; no general migration framework is needed. Required behavior:

1. Require no pending transaction and freshly validate active Steam pointer/manifest/runtime, the original transition journal, GOG parent pointer/manifest/inventory hashes and their relationship. Resolve paths through existing physical/containment checks; reject reparse paths, duplicate/foreign targets, missing units and mismatched identities. Read only fixture recovery/snapshot inputs, never normal Steam/Workshop/profile sources.
2. Derive the recoverable top-level game units from the GOG game inventory and journal. **Copy**, do not move, old backups into a fresh staging generation. Verify every staged game file against the original GOG inventory, including lengths and hashes; refuse missing/extra content or changed source bytes before publication. This bounded runtime-copy validation belongs to the transition, not a repeated whole-modlist audit. Check available space for staging while retaining both recoveries; no cleanup to manufacture room.
3. Preserve current frozen mod files, parked exclusions and captures. Create a new manifest with exact GOG runtime contract and official-content inventories, unchanged frozen package/order/seed metadata, and explicit provenance linking both the current Steam generation and original GOG recovery. Rebind the current exclusions to the new manifest after verifying their frozen package identities. Do not relabel Steam binaries or reuse an old snapshot ID.
4. Publish in one journaled transaction: replace the union of active/staged top-level game units **except Mods**, preserving removed Steam-only units in the new recovery. Reset the isolated profile from the verified frozen seed while backing up its present state. Preserve and remove the old candidate receipt and both possible active/parked candidate locations (including helper), Steam observer receipt/package, and prepared pointer. Thus the intermediate GOG state contains no launchable mismatched private core or observer. Keep old journals, original GOG backups, snapshots and results unchanged.
5. Check the new pointer/inventories/runtime and ordinary metadata audit after publication; report the new generation/transaction and that matching builds are required. Failure during swap uses existing recovery semantics, with no deletion of either generation. Keep latest-only rollback intact; the new transition itself is the latest reversible transaction until later operations begin.

Focused checks in the existing fixture test module should cover: a successful synthetic transition preserving later Steam profile/candidate/results and exclusions; wrong relationship/hash/missing backup rejected before swap; outside/reparse/duplicate target refusal; an injected mid-swap failure followed by recovery; latest-transition rollback restoring the prior state; and no stale candidate/observer/prepared admission. Use tiny temporary payloads, not game launches, benchmarks or another fixture. Extend guard dispatch coverage for `stage-gog`. Then use the reviewed action once in the canonical fixture and retain its receipt.

After transition, ordinary existing commands provide the matching artifacts. `reference_environment` derives `gog-rev573` and its managed paths from the active manifest; core build invokes the existing exact verifier. Commit inputs before building and use each returned package path:

```powershell
python scripts/fixture.py build
python scripts/fixture.py deploy --package <returned-GOG-core-package>
python scripts/fixture.py build-menu-observer
python scripts/fixture.py deploy-menu-observer --package <returned-GOG-observer-package>
```

Verify core receipt `referenceTarget`, `runtimeContract`, source revision and actual DLL hash; verify observer `gameSha256` and deployed generation. Core package output is revision-keyed, so an existing package for that revision must be checked rather than overwritten or assumed GOG. C01 does not need the optional image helper. Prepare fresh labels with `--purpose functional --menu-observer --exit-after-menu-ready`, then `verify-prepared`; use minimized nonactivating launches only within C01's assignment. Do not import Steam caches/captures as GOG reuse evidence. These later commands were **not run by C00**.

## GOG XML boundaries for C01

XML stores mod definitions as trees of nodes. Making those objects reachable by another mod is the relevant **publication boundary**: changing an object afterward may break a reference that mod retained. The game builds per-file trees and then imports distinct nodes into a combined tree. C01 must preserve that observable ownership while avoiding repeated parsing and import work.

The independent reviewer rehashed actual recovered GOG and active Steam assemblies, then used existing `artifacts/startup-next/tools/ilspycmd.exe` version `11.0.0.9375` to extract six GOG types. Every entire type text matches preserved GOG S05 and Steam R1 extracts byte-for-byte: `LoadedModManager`, `ModContentPack`, `DirectXmlLoader`, `LoadableXmlAsset`, `TKeySystem`, `PatchOperation`. **No material source-level difference was found in this bounded surface.** Different game assemblies remain different build targets; this is not proof of identical compiled instructions, transformed hooks or runtime behavior.

The following exact lines refer to fresh ignored `source/GOG-<type>.cs` files under the C00 evidence directory; [source/findings.md](../../artifacts/ecosystem-next-20260910/c00/source/findings.md) gives the full inspection:

| Boundary / source lines | Required C01 behavior |
|---|---|
| `LoadedModManager` 26–149, 184–265; `ModContentPack` 185–258 | Preserve `LoadAllActiveMods` stage/error-block order: content scheduling, mod construction, source XML, combination/map, ordinary-startup TKey and patch diagnostics, patching, inheritance/Def processing, completion and cleanup. Mod constructors run before XML and can install observers. `LoadModXML` consumes each mod's `LoadDefs` iterator serially. Keep hot reload native. |
| `DirectXmlLoader` 17–88 | Native descending folder priority, first relative-path winner, dot-file exclusion and actual enumeration order define selected inputs. Two workers plus caller fill indexed slots and join before returning. Do not introduce alphabetical ordering or move whole mod callbacks onto workers; C03 owns later pure input overlap. |
| `LoadableXmlAsset` 10–26, 55–81 | Fresh assets retain filename, full folder, mod and separate readonly `xmlDoc`. Native decoding is UTF-8 with one leading UTF-8 BOM removed; reader ignores comments/whitespace, disables character checking and async. Malformed files warn and leave null XML; combination emits its own attributed error. Retain native failure stages rather than replaying saved log text. |
| `LoadedModManager` 304–327 | `CombineIntoUnifiedXML` creates a fresh Defs document, deep-imports children in source order and maps each imported top-level node to its actual source asset. Wrong root names log but children still import. Source and combined nodes/documents are distinct. Pointing every asset's `xmlDoc` at the combined tree is not equivalent. |
| `LoadedModManager` 329–428 | Inheritance and final definition registration use the source map. Mapped roots retain original mod/filename; unmapped patch-created/replaced roots go through `patchedDefs`. Do not invent patch-author attribution. |
| `TKeySystem` 46, 57–63, 224–229 | Current hardcoded mapping mode visits roots but returns before retaining their nodes. Preserve the stage and check foreign hooks; this native branch alone does not establish private ownership. |
| `ModContentPack` 100–110, 357–387; `LoadedModManager` 268–301, 466–483; `PatchOperation` 18–75 | Patch XML lazily constructs real operation objects. Virtual `ConfigErrors` and `Complete`, `Apply` state and list cleanup remain in native order. C01 must not absorb arbitrary callbacks into cache construction; C02 owns processed-state reuse. |

An observer that retains a source asset or node is the smallest useful adversarial case for C01. Inspect effective Prepatcher/Harmony rewrites as well as native code before claiming that either representation stays private. Coordinate the existing default-off R4a constructor hook and XML timers with the new producer and early observation seam.

## C01 dispatch recommendation and remaining limits

After steward acceptance of C00, dispatch C01 from the accepted descendant of this handoff, owning matrix **1a**, its minimal **4** cache lifecycle foundation and early **8a/8b** observation seam. Include the `stage-gog` prerequisite above before live work. The first product increment should consume a real integrated parsed-input generation with native source attribution and a clear fallback point before publication. Require real supplier-absent warm parse avoidance, native selected-folder/order/error behavior, changed-input/corrupt-cache fallback and retained-reference checks. Use real active mod content, not an official-prefix-only demonstration; retain required bootstrap dependencies.

The changed premise is avoiding repeated source parse/import before observation, not reviving [S04 per-file reconstruction](../archive/ecosystem-replacement-20260910/s04-parsed-xml.md), [R1 whole-prefix restoration](../archive/ecosystem-replacement-20260910/r1-independent-xml.md) or [R4b retained-node journaling](../archive/ecosystem-replacement-20260910/r4b-retained-xml.md). Their recorded losing conditions remain relevant; no experiment was replayed. More workers alone is also an archived failed route. A viable interception point, useful real coverage and speed remain C01 work, not findings of this administrative inspection.

Remaining prerequisites are concrete: implement/check the transition, validate the complete copied runtime at transition time, rebuild GOG artifacts, and exercise actual transformed methods. No whole-runtime recoverability or new gameplay claim follows from selected-file hashes. Historical automated scene/bootstrap and private logging limitations in [development](../development.md) remain; do not repeat unchanged probes or count a menu pass as ordinary colony compatibility.

Carry forward: necessary isolated GOG functional/correctness work and modlist variants are standing-authorized once assigned. **No unattended window exists; no performance tests, microbenchmarks or overhead experiments are authorized.** Functional durations are diagnostic only, so C01 acceptance remains pending applicable measured performance. All features and the combined candidate are established on GOG first; Windows Steam promotion requires explicit owner approval and Linux/Deck is a later separate authorization. Preserve normal installations, profiles, saves, caches and other applications; no UI automation/focus interference, security changes, Deck access, merge or publication. The original C00 handoff did not self-accept or create C01. Subsequent steward acceptance closes only the administrative prerequisite.
