# Current state

**Source cleanup, 2026-09-08:** Current source separates reusable helpers from feature installation, shares diagnostic JSON writing, and isolates setup failures between features. Compatibility identities and optimization algorithms remain unchanged. The offline suite now includes release-packaging checks; 81 managed checks passed with one optional Character Editor identity skip, and all 35 Python checks passed. These source changes are not a new live-game or performance qualification.

**Release preparation, 2026-09-08:** Product metadata, source-inclusive packaging, GPL linking permission, CC documentation license, notices and contribution terms are implemented. See [release preparation](release-preparation.md) and [source build](source-build.md). The name and primary user-facing copy remain pending. Runtime behavior is unchanged; Steam and public-package live qualification remain pending. The tested fixture package below remains deployed.

The latest development build is `f14ec1bf5575f4150b612720c86f67cad137bf76`, packaged as `artifacts/releases/0.1.0-dev-f14ec1bf5575f4150b612720c86f67cad137bf76/RimWorldLoadingOptimizer-0.1.0-dev.zip` (SHA-256 `022d653388bf7d4e80ae7bf04662296f8166bebc9e6b22c726d1ae3d54c10b9c`). Its DLL hash is `d3fde1b42fa9804f5b4cb39adf376867b23e5aca243377f2d3b23905db1c3538`; the release copy is unchanged from the build. Build: zero warnings/errors. No deployment or live launch. The archive includes the matching source; this build record was added afterward. The [public source repository](https://github.com/kt-nyx/rimworld-loading-optimizer) excludes private investigation Git ancestry. The deployed/live-tested identity below is separate from this distributable.


**Completed normal activation and final bounded investigations, 2026-09-07:** [Normal activation, HugsLib decision and remaining-cost assessment](product-activation-and-final-opportunities.md) are complete. Supported optimizations now activate on ordinary launches through in-game settings; PNG caching remains opt-in. A new-colony/tick/save/reload check passed both with RLO physically absent and with normal activation. The HugsLib prototype was removed after the original report measured only 181 ms on the tested 99-package workload. Final larger observations found zero eligible late type searches.

| Last live-qualified checkpoint | Verified state |
|---|---|
| Retained runtime / package | `4422f6222a54c365618e154de66ba88ee048ab6f` |
| Runtime DLL SHA-256 | `722db104891f7d0c1add568684cefb3eca201b6e8bd839ef4b2a7bf05aaefec3` |
| Normal activation | In-game settings; established supported features default on, PNG cache off; explicit development arguments override settings |
| Verification | 80 managed and 32 fixture checks; zero skips or build warnings/errors; settings persistence and matched absent/present new-colony/save-reload checks |
| Larger assessment | 99 content packages plus RLO/observer; first menu calls 2.78/3.45 s and zero late type searches; changed foreground inventory excludes the warm timing comparison |
| Final state | `close-r02-user-warm` captured normally at 2026-09-08 00:53:18 UTC; original exclusions restored, audit passed, game closed; local integration only |

Read the [user guide and supported-build policy](user-guide.md) before normal installation. This is an unpublished development build with exact Windows GOG/dependency contracts, not latest-Steam or universal 1.6 support. Earlier identities and measurements are in [development checkpoints](archive/development-checkpoints.md).
