# Current state

**Release preparation, 2026-09-08:** Product metadata, source-inclusive packaging, GPL linking permission, CC documentation license, notices and contribution terms are implemented. See [release preparation](release-preparation.md) and [source build](source-build.md). The name and primary user-facing copy remain pending. Runtime behavior is unchanged; Steam and public-package live qualification remain pending. The tested fixture package below remains deployed.

The release-preparation build is `765dff45b29e9bc239990489938267d277631e57`, packaged as `artifacts/releases/0.1.0-dev-765dff45b29e9bc239990489938267d277631e57/RimWorldLoadingOptimizer-0.1.0-dev.zip` (SHA-256 `b914a20cee86ca34ee4d4dc96516593564f77cdfb878daffb92f2682bcf62aba`). Its DLL is copied unchanged from that build; runtime source changes are license comments only. Offline checks: 79 managed passes, one optional Character Editor physical-identity skip because the supplier is excluded, and 35 Python passes (32 fixture plus three release checks). Build: zero warnings/errors. No deployment or live launch. The [public source repository](https://github.com/kt-nyx/rimworld-loading-optimizer) is prepared as a clean current-tree snapshot, with no private investigation Git ancestry. The deployed/live-tested identity below is separate from this new distributable.


**Completed normal activation and final bounded investigations, 2026-09-07:** [Normal activation, HugsLib decision and remaining-cost assessment](product-activation-and-final-opportunities.md) are complete. Supported optimizations now activate on ordinary launches through in-game settings; PNG caching remains opt-in. A new-colony/tick/save/reload check passed both with RLO physically absent and with normal activation. The HugsLib prototype was removed after the original report measured only 181 ms on the tested 99-package workload. Final larger observations found zero eligible late type searches.

| Current item | Verified state |
|---|---|
| Retained runtime / package | `4422f6222a54c365618e154de66ba88ee048ab6f` |
| Runtime DLL SHA-256 | `722db104891f7d0c1add568684cefb3eca201b6e8bd839ef4b2a7bf05aaefec3` |
| Normal activation | In-game settings; established supported features default on, PNG cache off; explicit development arguments override settings |
| Verification | 80 managed and 32 fixture checks; zero skips or build warnings/errors; settings persistence and matched absent/present new-colony/save-reload checks |
| Larger assessment | 99 content packages plus RLO/observer; first menu calls 2.78/3.45 s and zero late type searches; changed foreground inventory excludes the warm timing comparison |
| Final state | `close-r02-user-warm` captured normally at 2026-09-08 00:53:18 UTC; original exclusions restored, audit passed, game closed; local integration only |

Read the [user guide and supported-build policy](user-guide.md) before normal installation. This is an unpublished development build with exact Windows GOG/dependency contracts, not latest-Steam or universal 1.6 support. Earlier identities and measurements are in [development checkpoints](development-checkpoints.md).
