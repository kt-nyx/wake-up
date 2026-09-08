# Current state

**Wake-Up rename, 2026-09-08:** The formal title is **Wake-Up: Loading Optimizations**. The repository and public package folder use `wake-up`, the C# project and runtime use `WakeUp`, and the public package ID is `kt.nyx.wakeup`. The optimization algorithms and supplier contracts are unchanged. Settings and cache data start fresh under the renamed identities; see the [replacement instructions](user-guide.md#replacing-a-pre-rename-development-copy). The fixture remains undeployed and no game was launched.

**Source cleanup, 2026-09-08:** Current source separates reusable helpers from feature installation, shares diagnostic JSON writing, and isolates setup failures between features. Compatibility identities and optimization algorithms remain unchanged. The offline suite now includes release-packaging checks; 81 managed checks passed with one optional Character Editor identity skip, and all 35 Python checks passed. These source changes are not a new live-game or performance qualification.

**Release preparation, 2026-09-08:** Product metadata, source-inclusive packaging, GPL linking permission, CC documentation license, notices and contribution terms are implemented. See [release preparation](release-preparation.md) and [source build](source-build.md). The chosen name is Wake-Up: Loading Optimizations. Final release copy remains pending. Runtime behavior is unchanged; Steam and public-package live qualification remain pending. The tested fixture package below remains deployed.

The latest development build is `30f005188ee17ac505845344f7a3541235cc4dd5`, packaged as `artifacts/releases/0.1.0-dev-30f005188ee17ac505845344f7a3541235cc4dd5/wake-up-0.1.0-dev.zip` (SHA-256 `662c32bf6d9dfd802e3c56f4c744f8f3725dc8ac5b985b11f389500438882a2f`). Its `WakeUp.dll` hash is `82c0cb0096ba10f71d488b71b1b9dfc3860f43c8e36a26984a4c0796048f3dec`; packaging copies it unchanged. The bundle includes matching source and legal material. The independent observer was rebuilt from `5ab28547cc9e002e89f39dd15bd41733f9294398`, without deployment. The later runtime build only corrects the formal DLL display title; runtime logic and observer source are unchanged. Both builds have zero warnings/errors; the renamed suite passed 81 managed checks with one optional Character Editor supplier skip and all 35 Python checks. This build record was added afterward.

The [public source repository](https://github.com/kt-nyx/wake-up) is renamed and retains its public history separately from private investigation ancestry. The pre-rename development bundle is recorded in [archived checkpoints](archive/development-checkpoints.md#final-pre-rename-cleanup-bundle). A new Wake-Up build does not inherit the live qualification of an earlier identity.


**Completed normal activation and final bounded investigations, 2026-09-07:** [Normal activation, HugsLib decision and remaining-cost assessment](product-activation-and-final-opportunities.md) are complete. Supported optimizations now activate on ordinary launches through in-game settings; PNG caching remains opt-in. A new-colony/tick/save/reload check passed both with Wake-Up physically absent and with normal activation. The HugsLib prototype was removed after the original report measured only 181 ms on the tested 99-package workload. Final larger observations found zero eligible late type searches.

| Last live-qualified checkpoint | Verified state |
|---|---|
| Retained runtime / package | `4422f6222a54c365618e154de66ba88ee048ab6f` |
| Runtime DLL SHA-256 | `722db104891f7d0c1add568684cefb3eca201b6e8bd839ef4b2a7bf05aaefec3` |
| Normal activation | In-game settings; established supported features default on, PNG cache off; explicit development arguments override settings |
| Verification | 80 managed and 32 fixture checks; zero skips or build warnings/errors; settings persistence and matched absent/present new-colony/save-reload checks |
| Larger assessment | 99 content packages plus Wake-Up/observer; first menu calls 2.78/3.45 s and zero late type searches; changed foreground inventory excludes the warm timing comparison |
| Final state | `close-r02-user-warm` captured normally at 2026-09-08 00:53:18 UTC; original exclusions restored, audit passed, game closed; local integration only |

Read the [user guide and supported-build policy](user-guide.md) before normal installation. This is an unpublished development build with exact Windows GOG/dependency contracts, not latest-Steam or universal 1.6 support. Earlier identities and measurements are in [development checkpoints](archive/development-checkpoints.md).
