# Wake-Up 0.4.1

0.4.1 lets Wake-Up's Giddy-Up texture-reading improvement operate beside the
specific current Faster Game Loading Continued Preview, Image Opt and
ImageOptCompat combination. Their default early loading remains enabled. Wake-Up
does not change their settings, remove their patches or replace their loader.
The 0.4.0 YaOpt worker-rebuilding and Adaptive Storage Framework inheritance fixes
remain included. See [current state](current-state.md) for publication status.

## What changed and why

Previously, finding Preview's early-loading coordinator made Wake-Up refuse its
optimized Image Opt readback. A coordinator's presence does not by itself mean
that an individual texture is unfinished. The new adapter checks the texture
being read: it must be live, read on the main thread and absent from Image Opt's
pending task collection. Existing managed and native completion checks remain.

The adapter also verifies the exact loaded supplier DLLs, method bodies and
Harmony hooks (callbacks inserted around a method), including the completion and
copy helpers that determine ownership. It checks Preview's current Image Opt
provider state and ImageOptCompat's current `destroyOriginalTexture=false` setting.
Changed code or relevant hooks revoke admission; pending or unsupported reads use
the original implementation. Enabling original destruction refuses the affected
readback with an explanatory notice and preserves unrelated Wake-Up work.

This is an adapter for completed-source reading. It neither reconstructs scheduler
history from a resettable queue nor approves arbitrary supplier scheduling. The
supplier's pixel-read, resource, audio, menu-status and copy-lifetime hooks remain
installed. Wake-Up owns its temporary readback output; it does not take ownership
of ImageOptCompat's copies. No foreign code was changed or bundled.

## Exact supported distributions

| Component | Inspected distribution and identity |
|---|---|
| Faster Game Loading Continued Preview | Workshop `3797541348`, version `2026.09.07.1`, content manifest `293728264872599594`; DLL SHA-256 `4cb5a8434d1e44f3e69d483860f377b4204ed00483036f98fc2b86e34b7283ec` |
| Image Opt | Workshop `3543873568`, version `0.1.13`, content manifest `4607835456179217044`; managed DLL `c4437b7ebcc10b4b3ff4f20636c81fe2511ee132a8a1a5e2ff932e3e8329035e`; Windows native DLL `72cec9dc93e3f470df1f81791e310c3e09b580bd615c146fefd85a6166f815d4` |
| ImageOptCompat | Actual [repository distribution](https://github.com/DegradingAnt/ImageOptCompat/tree/71fa60da3ed766853689e39e8550192c73f785d2), source `71fa60da3ed766853689e39e8550192c73f785d2`, About version `0.3.0`; distributed DLL `cfc159f52cd9ddc7d52d19975bda18c4157152e0087f2ca221898f25d412fefd` |

The current compatibility DLL is distinct from the older acquired DLL. Matching
package IDs or version labels do not admit an old or changed implementation.
Supplier defaults were retained: Preview early mod loading, multithreading and
XPath caching on; delayed graphics and static atlas baking off; ImageOptCompat
original-texture destruction off. Users need not disable early loading.

## Validation and practical limits

The Release builds completed without warnings or errors, and 35 focused managed
tests passed. The tests cover exact supplier identities, required Preview hooks,
late foreign patch revocation, current provider/destruction settings and existing
supplier policy. The wider suite's previously recorded intermittent notice-storage
failure is not claimed fixed; this change adds no storage fix.

Three final Windows fixture runs reached an independently observed menu and exited
normally with exit code 0 and automatic acceptance:

| Run | Result |
|---|---|
| `tuple-suppliers-cold-03` | Supplier-only baseline, without Wake-Up. |
| `tuple-wakeup-cold-01` | 392 optimized Giddy-Up reads verified against original offsets; zero errors, mismatches, fallbacks, pending or contract refusals. Two real compatibility notices were displayed and acknowledged. |
| `tuple-wakeup-user-01` | Normal user activation without Wake-Up command-line overrides: 392 optimized reads with the same offset digest; zero errors or refusals. The acknowledged notices did not reappear. |

All three runs checked the same 33 actual PNG sources: 12 Succulents, four More
Floors, 12 Erin's Hairstyles 2, four private directional Giddy-Up inputs and one
known-color asymmetric image. Rendered pixels were compared with decoded source
artwork. The complete pixel records matched between the supplier-only baseline
and both Wake-Up runs. Uncompressed sampled sources matched exactly; the observed
DXT5-compressed plants passed compression-aware color and transparency limits.
This is direct sampled-artwork evidence, not a claim that every texture was tested.

The real ImageOptCompat copy helper produced matching transparency, released its
owned copy and left the source alive and unchanged. This targeted helper check is
not vehicle gameplay or full content teardown/reload qualification. Supplier menu
and completion hooks remained present. Expected XML output also passed. Logs had
no exceptions or selected texture-load failures; nonfatal Mono diagnostics and
Unity shutdown allocation statistics remain, so the logs are not called warning-free.

Earlier supplier-only attempts `tuple-suppliers-cold-01` and `-02` reached the menu
and exited normally but failed observer checks: first a wrong setting-property
name, then an overly strict compressed-alpha threshold. They remain retained and
are not counted as passing validation. The corrected oracle measures actual source
colors, format and transparency; it does not exempt images by package identity.

No persistent supplier texture cache was produced in this workload. The third run
therefore establishes normal activation and notice persistence, not warm-cache
behavior. No performance measurements were authorized or made for 0.4.1. Earlier
[0.4.0 measurements](release-notes-0.4.0.md) retain their original build identities.
This release makes no universal black-texture, long-running colony, vehicle,
arbitrary-source readiness, full content reload or Linux/Steam Deck claim.

## Build and handoff identities

- Product build source: `82b8293e7cb4da7a6791523696ba3177f8d81b03`.
- Core DLL SHA-256: `ab473906055db58ca526d4216087070a1114963bc4edf5e9c69903983871bcff`.
- Final private observer source: `7c469beaacb2ed62a0cdeffc8772af2e265067a3`; its DLL is not shipped.
- Distribution version: `0.4.1`; stable assembly version: `1.0.0.0`.
- All 392 Giddy-Up reads covered 9,831,936 source pixels; offset SHA-256:
  `F7834346ECDAA0025C76456D7DC9FDE99386BD70D1048346694A65A6E148B207`.
- Windows build/reference target: `gog-rev573`, original game assembly
  `4a170804fbfefabdb620d8914e584e58f822a58c6e304dcb76a67003588dab28`.
  Installation Version.txt says rev573; the admitted runtime reports 1.6.4871 rev574.
- Optional unchanged Windows helper SHA-256:
  `ca12d392e0a173b1dd7b87d85b95621166f4aca53860550808319b4b0c285c36`.

The source-inclusive package reuses the exact tested core DLL. Its manifest records
the later archived source revision separately from the build revision. All matching
source and notices are included; private game/mod data and observer DLLs are excluded.

The fixture was restored to Prepatcher, Harmony and Core and passed full content
audit. Game/package inventories and generated ModsConfig bytes match the original
baseline; product and observer deployment are absent. New restoration manifest:
`1eb1c70e2edca106a1f06706a73631dc170e3be1c96e6f75807ed821507b0fd1`.
Compact private acceptance and restoration receipts are under
`artifacts/preview-imageopt-20260925/`. Normal profiles, saves and Deck were untouched.
