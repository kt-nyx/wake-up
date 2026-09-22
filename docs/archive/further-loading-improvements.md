# Further loading improvements: Giddy-Up texture preparation

> Historical record, archived 9 September 2026. Its status and next steps describe the original work, not the current roadmap. See [current state](../current-state.md).

> Recorded identifiers, commands and private artifact paths below retain their pre-Wake-Up spelling; they are historical evidence.

> Dated qualification record. Identities, activation defaults, permissions and proposed next steps describe this investigation, not the current release. See [current state](../current-state.md), [support policy](../user-guide.md#supported-build-policy) and [results](results.md) for current decisions.

Giddy-Up now prepares animal riding offsets using only the center image column it actually reads. The final build saves **0.45-0.51 seconds (1.6-1.8%)** on the small sorted combat/races/genes selection plus Giddy-Up, with accepted searches already enabled. Medians are 28.070793 versus 27.588108 seconds: **1.72% shorter startup on this workload**, not a general modlist average.

Work started from main `b0a0a60dbe0a85a9382ea8d1a0e38ec3f5e5a55d` on `codex/further-loading-improvements`. The preceding accepted package was `743ecabb5043d360698b90815895c5fbab7f95a5`. Only the canonical checkout and fixture were used, with no other agents or worktrees. Normal installations, Workshop subscriptions, profiles and saves were untouched.

## Mechanism and optional compatibility

The supplier converts an animal image into a readable texture, then scans its center column to calculate riding height. The replacement preserves its full-size graphics copy and color conversion, but reads back a one-pixel-wide column. It avoids reading unrelated pixels, generating unused mip levels and uploading an image used only on the CPU. Original offset calculation, height scanning, setup, definition order, dictionary updates and settings writes remain intact. Only one private texture-construction call is redirected.

There is no persistent cache or background worker. Only the original calling thread before the completed menu may use the replacement. Other threads, later calls, unsupported graphics, changed patches and capacity exhaustion use the original method. At most 4,096 temporary column textures are retained and these are released after the completed menu repaint. `offsetMs` sums complete offset calls; `scopeThroughMenuMs` includes later startup work and is not an isolated saving.

Activation is candidate `startup-searches` plus `--giddy-textures on`; default `off`. `timing` observes original readback. `verify` additionally compares every original alpha value in the center column; replay is excluded from timing comparisons. A mismatch disables replacement and falls back to original readback.

Giddy-Up is not required, linked or bundled. The exact supported identity is:

- Package `memegoddess.giddyup`, assembly `GiddyUpCore` version `2.2.5.0`.
- DLL SHA-256 `E54C416CA81A1542A42577E9B13C36A79D054B28F5EB0B3B47AAEBD13C3CAB19`.
- Module ID `e06e404d-ffd8-4a3b-a0ac-2285d47573a8`.
- Exact loaded bodies of `SetDrawOffset`, `GetBackHeight`, `GetReadableTexture` and `ProcessPawnKinds`, plus the supported game/Harmony binary checks.

Relevant Harmony prefix, postfix, transpiler and finalizer hooks are checked at admission and published state is rechecked during replacement. Unknown hooks disable this feature locally. Missing, updated or modified suppliers retain ordinary behavior; no supplier version is forced. Only Direct3D 11 is admitted. The supporting instruction comparator now compares floating constants by exact bits, including signed zero; unfamiliar operand forms still refuse. This is development-fixture qualification, not a latest-Steam, other-platform or broad gameplay claim.

## Final matched measurements

All final timing captures use package `818c7efa7cd032ae209db95ae6f0fbc2385746a1`, the same 31 content entries (33 including Wake-Up and the independent observer), dependency-checked author/community order, seeded settings, native texture selection and fresh application caches. Gagarin's normal cache creation remains present. Extra Wake-Up Gagarin reuse, PNG caching, Character Editor and Loading Progress modes are off in both arms. Controls retain accepted search behavior and observe Giddy-Up's original path with matching diagnostics. These are incremental feature comparisons, not absent-Wake-Up comparisons.

Windows file caches were not flushed; forward/reverse controls address warming. Background game inventory was checked before and after each launch, with no other game found. Initial available memory was about 19 GiB; final scored runs retained at least 14.73 GiB. Only the fixture used below-normal CPU priority. This does not mean the rest of Windows was inactive.

| Order / captures | Original menu s | Candidate menu s | Saving | Original / candidate offset ms |
|---|---:|---:|---:|---:|
| Original first: `gu-f01-control`, `gu-f02-candidate` | 28.171518 | 27.658425 | 0.513093 s / 1.82% | 1353.316 / 502.599 |
| Candidate first: `gu-f03-candidate`, `gu-f04-control` | 27.970068 | 27.517790 | 0.452278 s / 1.62% | 1353.315 / 503.043 |

Complete offset work saves about **0.850 seconds** per pair. This differs from the roughly half-second whole-startup saving; do not add them. Menu times use `menuReadyObservation.elapsedSeconds`, followed by normal exit code zero, capture and `automaticTestPassed=true`.

Earlier prototype pairs on `fb2aa8f67b6c2d82ea54946d232ea36741fcca49` were 28.760987 / 27.522139 and 28.110083 / 27.566789 seconds (original/candidate). They support the same direction but are not pooled with the final build.

## Verification and final identities

The final offline suite passes **73 managed and 30 Python fixture checks**, with no skips and zero build warnings/errors. Checks cover exact physical supplier and loaded bodies, same-version changed-byte/module/version rejection, all four foreign Harmony patch kinds, exact floating operands and fixture selector reconstruction. Character Editor was temporarily restored with the fixture tool for its existing physical-supplier test, then re-excluded.

Final `gu-f05-verify` compares **all 1,359 readbacks**, representing source images totaling 80,826,368 pixels. Every center-column alpha value matches. All generated offsets match digest `A9E67E4B9E68D912EF03EF718310976B0A68AD5B3D97631E62615C179F94762B`. Qualified candidate/verification runs have 1,359 hits, no fallbacks/errors/mismatches and zero retained temporary textures. Captured unified XML, content/order, seeded settings and scanned messages match. Generated Giddy-Up settings match byte-for-byte across qualified original, candidate and verification captures. Interactive mounted gameplay has not been newly tested.

For `gu-f06-absent`, Giddy-Up was physically parked outside game discovery through the fixture tool. Its enabled selector reports only `inactive: supplier-absent`; accepted searches still run and enabled absent Character Editor/Loading Progress features remain harmless. The smaller UI selection reaches the menu in 17.107853 seconds and exits normally. This is an absence qualification, not a speed comparison. Giddy-Up was then restored with the fixture tool. Original exclusions are restored, the fixture audit passes and no game process remains.

- Retained runtime/package source: **`818c7efa7cd032ae209db95ae6f0fbc2385746a1`**.
- Package: `artifacts/op7-fixture-package/818c7efa7cd032ae209db95ae6f0fbc2385746a1`.
- Deployed DLL SHA-256: **`32fd23aef7c88260c31bd1c47b56d13cade50f20a4cd915e6d88f262587084b0`**.
- Last capture: `gu-f06-absent`, completed 2026-09-08 00:00:15 UTC (September 7 locally).

Documentation commits do not change the package. Integration is local only, without pushing or publishing.

## Rejected and invalid attempts

**Combat Extended gun XML lookup was removed.** The old hook was removed for former scope restrictions, not a performance failure, so expanded scope justified testing it with exact optional binary/method checks. It redirected the existing bounded lookup while leaving conversion code intact. It exercised 110 searches, preserved available XML and passed identity/conflict checks. Prototype pairs saved 0.403 / 0.536 seconds, but the final reverse pair shrank to **0.027545 seconds** (26.393976 original versus 26.366431 candidate), after a forward saving of 0.470897 seconds. This does not establish a worthwhile dependable gain. All Combat Extended runtime/tests/selectors were removed in `094a894`; experiment package `be4ef153b0441675c3e44e69c3714ed3c3505272` remains recoverable. This is a negative for that implementation/workload, not every possible Combat Extended optimization.

**Early Giddy-Up qualifications are excluded from candidate claims.** `gu-q01-control` on `96a5aae` triggered setup prematurely by patching a method on its initialization type. The game exited normally but the supplier failed initialization: automatic menu success was not functional qualification. Removing the setup hook restored normal behavior. `gu-q02-control` on `20492df` had an incorrect thread admission assumption. `gu-q04-verify` on `119ce2d` retained original readback because instruction comparison refused float operands. `gu-q03-control` correctly observed original calls; `gu-q05-verify` on `fb2aa8f` established actual replacement and pixel equivalence. Early selection-path and package-path mistakes refused before launch and are not game failures.

Current shared XML/type code was reviewed against recorded broader-query, type-index, DDS/cache and Gagarin-bridge results. No materially new premise justified rerunning those weak routes. HugsLib warning enumeration remains historically parked rather than disproven; no substantial new small-workload opportunity was established here. Work stops with the qualified Giddy-Up change rather than expanding profiling or speculative optimizers.

Private raw evidence, decompiled inspection, order audit, test/build/deployment logs, all 25 launch summaries (including invalid qualifications), and generated-data comparisons remain under `artifacts/further-loading/` and `.rlo-test-instance/results/`.
