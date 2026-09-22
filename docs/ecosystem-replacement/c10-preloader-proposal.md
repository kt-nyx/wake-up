# C10 early audio guard proposal

**Proposed for review, not approved or implemented â€” 12 September 2026.**
This would run Wake-Up's audio mutation guards before ordinary game/mod code,
instead of trying to reconstruct that earlier history afterward. It could unblock
prepared audio. It does not solve texture publication, deliver the audio feature,
or close C10. The official release archive and pinned license have been downloaded and hash-verified under ignored artifacts. Production adoption remains unapproved.

## Source-supported opportunity and actual added responsibility

Unity Doorstop 4.5.0 intercepts Windows runtime-function lookup through import-table
hooks. Its `init_mono` calls the original `mono_jit_init_version`, executes the
configured managed entry point, then returns the domain to Unity. This is a
concrete earlier boundary; exact RimWorld integration is untested. It is **native
startup interception**, not simply another ordinary managed mod. Doorstop also
installs handle/command-line hooks and can correct the process working directory.
No claim is made that turning off its optional settings removes every hook.
[Windows source](https://github.com/NeighTools/UnityDoorstop/blob/v4.5.0/src/windows/entrypoint.c),
[Mono bootstrap source](https://github.com/NeighTools/UnityDoorstop/blob/v4.5.0/src/bootstrap.c).

## Exact first fixture installation

All proposed additions stay beneath `.rlo-test-instance`; normal installations
remain untouched. Use the existing `fixture.py` transaction mechanism with a
small explicit bootstrap deployment action, not ad hoc file copying.

| Path relative to `.rlo-test-instance` | Proposed content |
| --- | --- |
| `game/winhttp.dll` | Reviewed x64 Doorstop 4.5.0 proxy, exact package hash recorded |
| `game/doorstop_config.ini` | Configuration below |
| `game/WakeUpBootstrap/WakeUp.AudioBootstrap.dll` | BCL-only `Doorstop.Entrypoint.Start()` |
| `game/WakeUpBootstrap/bootstrap.xml` | Exact fixture/profile paths, selected Harmony path/hash, session and explicit probe selection |
| `game/WakeUpBootstrap/UnityDoorstop-LICENSE.txt` | Upstream LGPLv2.1 license; source/version/build provenance alongside it |

```ini
[General]
enabled=true
target_assembly=WakeUpBootstrap/WakeUp.AudioBootstrap.dll
redirect_output_log=false
boot_config_override=
ignore_disable_switch=false
[UnityMono]
dll_search_path_override=
debug_enabled=false
debug_address=127.0.0.1:10000
debug_suspend=false
```

These keys and the entry-point signature come from the
[pinned configuration](https://github.com/NeighTools/UnityDoorstop/blob/v4.5.0/assets/windows/doorstop_config.ini).
No debugger, runtime search-path override or game assembly replacement is selected.
Keep the ordinary fixture executable, save-data/log arguments and launch path.
The expected working directory is already `game/`. Current read-only checks find
`winhttp.dll`, `doorstop_config.ini`, `version.dll`, `winmm.dll` and
`WakeUpBootstrap/` absent there. This is not a complete conflict audit.

## Avoid repeating the exposure assumption

The entry assembly must reference only the standard .NET library (BCL), with no
Wake-Up core, game, Unity, Prepatcher or codec type references. Its first action
records already loaded assembly objects. It then uses reflection to load and bind the **existing fixture Prepatcher's** exact
`game/Mods/2934420800/Assemblies/0Harmony.dll`, SHA256
`7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`.
Do not bundle/load a competing Harmony copy. Subscribe to assembly-load recording
before this step and take another snapshot after both guards are fully installed.
Any game/codec exposure during dependency initialization remains unqualified.

The original native `ModAssemblyHandler.ReloadAll` uses `Assembly.LoadFrom`.
The experiment must show that its actual Harmony object is the same guarded
object; file names/MVIDs alone are insufficient. Prepatcher's later replacement
Harmony must contain the existing emitted guards and connect to the same session.
Unexpected copies, early callbacks, missing guards or uncertain dependency order
keep prepared-reader admission closed. Source review of the small guard dependency
path plus actual entry/load order replaces the old retrospective-cache assumption;
Doorstop's presence alone proves nothing. No Unity work occurs inside this early
entry point, and no prepared reader exists in the first experiment.

## Smallest first experiment and stopping rule

Under the bounded experiment approval: one isolated GOG **functional** activation
startup, normal minimized nonactivating launch and automatic exit/capture. Record
early entry, dependency loads, guard completion, old/final Harmony references and
actual later callbacks in a receipt under the existing observer's profile folder.
The required causal order is guard completion before the first game/codec/mod
execution opportunity, with correct final-generation binding. A native-bootstrap
failure or ordinary startup failure is failed evidence, not native-fallback proof.
No audio output, speed, broad compatibility or cached-reload acceptance follows.
If this boundary works, proceed to real prepared-sample audio implementation;
do not expand the observation into another general framework.

Before deployment, check the exact target files and known proxy/config loaders;
refuse any non-owned existing proxy/configuration rather than overwrite or rename
to another proxy. Record initial bytes/absences and hashes for every added file.
Restore only owned files after the verified fixture process exits. If a file has
changed unexpectedly, preserve it and coordinate instead of blindly deleting it.
Reverify the original seven hashes/five absences plus all new bootstrap absences.
Reject conflicting Doorstop/debugger environment state; do not alter machine-wide
environment/security settings. The steward authorized this bounded experiment under the standing GOG prototype/correctness permission. All operating limits still apply.

## End-user implications requiring approval

A Workshop subscription alone cannot install this game-root preloader. If adopted,
bundle the native dependency, managed entry, license and reproducible provenance
with Wake-Up. A small in-mod setup/removal action could install those owned files
for the next restart after explicit user selection; an equally small bundled
setup helper could handle installations where the running game cannot write its
directory. Those are proposed product choices, not implemented workflows or a
promise that permissions/proxy conflicts disappear.

This helper would only install/remove startup files. It would not require the
deferred companion texture-preparation app or revive optional matrix row 6c.
Unsubscribing a Workshop mod would not itself remove game-root files, so explicit
removal and missing-core/default-off behavior must be delivered. The bootstrap
must honor ordinary master-disable/on-demand/one-shot policy without loading the
game API early. Compatibility with another preloader needs a reviewed integration;
the first design refuses a conflicting installation. Production adoption and this
setup/removal experience remain separate owner decisions from the first proof.

## Pinned experimental inputs

Official `v4.5.0` source commit: `33dab9a6733862eb81869ff08431d9478b28784b`.
The release archive SHA256 is `7bb953e8d883c8bde76ced96f6d0e45660ad6e0151880d8ab5856bf4f532b147`;
its `x64/winhttp.dll` SHA256 is `8c6cdbc38836dee87e3368f5de1994d7c0ccebf29e4ce7aba3c0981f9375412c`
(PE machine `0x8664`). The source license SHA256 is
`20c17d8b8c48a600800dfd14f95d5cb9ff47066a9641ddeab48dc54aec96e331`.
[Official release](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0).

`fixture.py build-audio-bootstrap` packages only these pinned native bytes and the
committed BCL-only project. `deploy-audio-bootstrap` generates the exact fixture
XML configuration and journals all added paths, including the private root receipt
`audio-bootstrap.json`. The early receipt is
`profile/FixtureMenuObserver/c10-preloader-early.xml`; the observer captures the
complete bounded session at the menu. The three game-root additions and private
root receipt begin absent and must return to absence on stopped handoff.

## First causal run — passed, 12 September 2026

Source, GOG core, observer and bootstrap package revision:
`234799120ab9ce3622803b27b1b943b437536efc`. Run `c10-preloader-01`
used functional purpose, the activation workload, baseline core selection,
muted fixture preferences and minimized nonactivating launch. The independent
menu observer passed and fixture PID 32096 exited normally with code 0.
Both bootstrap and preloader observation results passed; feature acceptance
remained false and prepared-reader admission stayed closed.

The actual event sequence was entry 1, Harmony loaded 5, both guards complete 9,
original game CreateModClasses entry 108, Prepatcher constructor entry 109,
and first Wake-Up extension 121. Captured executable-assembly snapshots excluded
the game and codecs before guard completion. The actual first extension used the
same guarded Harmony object and the observed original game object. The final
replacement contained the guards and invoked their persistent callback 16 times.
There was no bootstrap failure. This resolves the tested earlier-startup question;
it does not establish cached audio behavior, broad compatibility or performance.

All four deployment/preparation transactions were rolled back after capture.
The seven prior endpoint hashes, five prior absences and four new bootstrap
absences match the original endpoint. Raw run evidence remains at
`.rlo-test-instance/results/c10-preloader-01`; input/rollback records are under
`artifacts/ecosystem-next-20260910/c10/preloader/`. A separate normal Steam game
was present during the functional run and remained untouched.

The next work is actual prepared-audio integration. The existing 18 focused
prepared-audio managed tests passed after this boundary proof, together with the
72 existing Python fixture checks, under fixture test record
`ce5e18e8e285481496c14bbbaa826896`. These are correctness results only.
