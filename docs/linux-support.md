# Linux and Steam Deck compatibility preview

Wake-Up recognizes the exact native Linux game build captured from the owner's
Steam Deck. The installed first preview reached the menu in the owner's launch:
four non-graphics improvements completed, and Gagarin reuse correctly declined
a known Loading Progress conflict. Current source also implements the two
graphics ports: Giddy-Up on OpenGL and PNG caching with CPU texture processing.
Those graphics changes passed offline checks and were installed at the owner's
request on 2026-09-08 at 20:07 UTC. All 15 package files were hash-verified and
the previous version was backed up outside game discovery. They have not yet
been tested in the game. See [current state](current-state.md) for the installed
DLL identity and installation receipt.

## What is implemented

| Feature | Linux preview behavior |
|---|---|
| Definition and named-template searches | Admitted for the reviewed Linux game; existing query and conflicting-patch checks remain |
| Harmony type searches | Admitted with the exact reviewed Harmony library; original fallback and startup cleanup remain |
| Character Editor preset lookups | Admitted with the exact reviewed optional 1.6.3.3 supplier and unchanged method checks |
| Gagarin parsed-XML reuse | Admitted with the exact reviewed optional supplier; interfering hooks, including the known Loading Progress XML replacement, still cause fallback |
| Loading Progress repaint pauses | Admitted with the exact reviewed optional 0.14.0 supplier and loading-loop checks |
| PNG cache | OpenGL CPU-processing path, selected automatically with the game's defaults; cache setting remains opt-in; live validation pending |
| Giddy-Up texture readback | Center-column readback on OpenGL with exact 2.2.5.0 supplier checks; live validation pending |

Admitted means the game version is allowed to reach the feature's checks. It
does not mean the feature installed, encountered eligible work or saved time.
Updated or missing optional mods still preserve their ordinary behavior.

The existing single runtime DLL supports the reviewed Windows and Linux game
identities. Users do not need a different Linux DLL or Proton for this preview.
This does not extend support to every Linux or RimWorld 1.6 build.

## Evidence and exact identity

The owner's 2026-09-08 launch used native Linux RimWorld **1.6.4871 rev600**, Unity
2022.3.35f1 and OpenGL 4.6, with `-disable-compute-shaders`. The old installed
Wake-Up binary refused it with `Unreviewed game build.` The physical Prepatcher
Harmony file matched the existing contract; the refusal occurred before its
runtime validation completed.

The Linux game assembly has version `1.6.9676.18020`, module identifier
`b4d967f0-d45a-413f-bb02-23eefee4f2ae` and SHA-256
`082DB1DD4F7F1D0B72960D7E1BEEAD8FBFE6957200E8627F65BDA0DBBE1DD8F8`.
The source names this exact identity `linux-rev600`. A module identifier (MVID)
identifies a compiled module; SHA-256 checks the complete physical file. Both
remain required, alongside the assembly name and version.

Read-only ILSpy inspection and comparison of 127 method bodies found matching
instructions, referenced types/methods/fields, local variables, method flags and
exception regions between the captured Linux assembly and the existing GOG
reference. The comparison covered the patch-operation classes, loaded-mod
manager, XML asset constructors, definition database, root/update methods,
long-event handler and main-menu drawer. References were compared by their
meaning rather than their numbered positions in each DLL (metadata tokens).
The XML asset constructor therefore retains the existing semantic fingerprint.
This is static code evidence; different Mono or graphics behavior is not tested
by that comparison.

The Deck's optional supplier files also matched the existing full-file hashes:

| File | SHA-256 |
|---|---|
| Prepatcher `0Harmony.dll` | `7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C` |
| `CharacterEditor.dll` | `BFF3B2268321F8C9566E15CBBD71CA6F975E9139F976C8BD4A9E0CA924C5B457` |
| `Gagarin.dll` | `D83EDC9FE3AE0381A71EE460F766563F1CB078F61211D8388614BAB3B0E1250C` |
| `ilyvion.LoadingProgress.dll` | `4F19F836FD41C8E702295CB90D7D5E66C53F38CDA8A13A3ED79F1D063E1AFCEF` |
| `GiddyUpCore.dll` | `E54C416CA81A1542A42577E9B13C36A79D054B28F5EB0B3B47AAEBD13C3CAB19` |

The private capture, downloaded references, decompiled code and comparison
records are under ignored
`artifacts/steam-deck-diagnosis/20260908-141344/`. None are included in source or
release packages. Investigation SSH activity only inspected and downloaded
files. The later authorized installation replaced only Wake-Up's package,
preserved the previous package outside game discovery and verified all profile
configuration files unchanged. No saves or game processes were changed.

## File discovery and diagnostics

Prepatcher can load an assembly directly from memory, leaving no usable physical
file location. The fallback now selects `RimWorldLinux_Data/Managed/Assembly-CSharp.dll`
for the reviewed Linux identity and keeps `RimWorldWin64_Data` for Windows.
An existing published assembly location still takes precedence, and selecting
a path never bypasses the file hash check. Harmony's existing same-library
Workshop resolver works with the Deck's `steamapps/common/RimWorld` layout and
is covered by an additional isolated path test.

An unknown game now reports that the RimWorld build has not been reviewed,
instead of blaming either the game or Harmony. Missing game/Harmony files and
changed Harmony identities have separate explanations. `Player.log` records the
specific rejection code, and feature receipts retain their own reasons. Settings
identify successful Linux admission as a preview, with graphics validation pending.

## Graphics ports and default launch settings

No manual launch flag is required. Read-only inspection found the owner's
RimWorld personal `LaunchOptions` empty and Steam's cached Linux launch entry
supplying `-disable-compute-shaders` to `start_RimWorld.sh`. The script passes
arguments through unchanged. Independently, the captured Linux game's
`Verse.UnityData.CopyUnityData` sets compute-shader support to false. The port
preserves that behavior and selects the CPU path from the game's effective
settings. It neither enables compute shaders nor changes Steam launch options.

PNG cache misses still use the original image loading, compression, texture
format and mipmap generation. Mipmaps are the smaller versions of a texture used
when it is rendered at a distance. The port lets the game's `Apply` finish those
versions while temporarily retaining readable pixels, copies the complete raw
payload, then discards readable data with `Apply(false, true)` when the original
call requested it. A capture failure skips the cache entry while preserving the
completed texture. Cache hits restore the saved format, full mip payload, color
space and sampler settings without decoding or recompressing the PNG. This
follows Unity's [Apply](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.Apply.html)
and [raw texture data](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.GetRawTextureData.html)
contracts. The extra first-build upload/copy can cost time; no Linux speedup is
claimed. The cache remains off by default and bounded to 512 MiB.

The supported CPU path includes compression disabled, uncompressed images,
DXT1/DXT5 compressed images, and the game's reduced mip chains for images whose
dimensions require them. Native DDS precedence remains unchanged. Cache keys
now use `png-v3`, include the capture path and retain device/driver/color-space,
settings and source identities. Earlier entries are not reused or deleted.
The existing Windows Direct3D 11 compute block capture remains available; Linux
OpenGL never uses its Direct3D-specific block-copy/readback operation. Unsupported
backends, including Vulkan, retain native processing. An unexpected Linux
compute-compression path also falls back to the game.

Giddy-Up keeps the supplier's full image conversion and original offset loop,
but reads only the middle column needed for its alpha scan. Unity defines
[ReadPixels](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.ReadPixels.html)
coordinates from the lower left; the OpenGL path adds no manual vertical flip.
Existing verify mode compares every scanned alpha with the supplier's full
readback, disables the optimization on a mismatch and falls back to the original.
Temporary render targets are released and retained columns are destroyed at the
menu. Its receipt now records the graphics backend.

Static comparison covered 42 relevant methods: 39 matched, with differences in
`UnityData.CopyUnityData`, `ModContentLoader<T>.LoadItem`'s audio branch, and an
audio-format helper absent from Linux. The texture-conversion and compression
methods, holder, enumeration and iterator bodies matched. All eight PNG target
checks remain; Linux has a separate exact `LoadItem` semantic fingerprint,
`BA99B5D41EAB8C9EC052A993DB210E88205297F3D51798A9E02DC0B3C29F4509`.
Seven runtime fingerprints were reproduced with an offline compiled metadata
probe. The iterator was compared through read-only Cecil metadata because newer
Mono APIs are unavailable in desktop .NET Framework; its runtime check remains.
Private decompilations, canonical method bodies and the comparison record are
under `artifacts/linux-graphics-port/`. These are static checks, not Unity or
Mono execution evidence.

## Offline checks and remaining live work

### PNG and Loading Progress compatibility

The first graphics-preview launch reached the menu and completed 1,682 Giddy-Up
OpenGL reads with no errors/fallbacks or retained textures. PNG caching was
enabled but refused `hook-ReloadContentInt`, before selecting a graphics path.
This conflict also applies to Windows: Loading Progress patches the native
content method and can bypass it using its own staged loader. The successful
historical Windows PNG qualification (`png-p17-integrated-build` through
`png-p20-integrated-control`, plus `png-n04-integrated-native`) did not include
Loading Progress in its active mod list. That was a combination-coverage gap,
not evidence that Windows handled this conflict successfully.

The new source integrates with that staged loader. It replaces only the
`ModContentHolder<Texture2D>.ReloadAll` call inside
`ReloadContentIntReplacement`'s iterator with the same guarded PNG helper used
by the native loader. Loading Progress still owns its progress messages, yield
points, profiling, audio/string/asset-bundle stages and original-method skip
logic. The native route also remains patched, supporting its settings that
disable content replacement and installations without Loading Progress.

Admission authenticates the optional supplier's existing exact physical
hash/MVID and five loaded method bodies: iterator factory, iterator `MoveNext`,
and the three native-loader prefixes. Only those exact callback methods under
the expected owner are allowed on `ReloadContentInt`; changes elsewhere in the
PNG chain still refuse. Published patch guards monitor the reviewed supplier
methods, so a later foreign patch disables cache substitution and the ordinary
texture holder runs. Removing the original blanket guard alone would neither
cover the replacement loader nor preserve these checks.

The installed receipt now records `loadingProgressBridge`. Completion records
separate `nativeReloads`, `loadingProgressReloads` and `reloadFallbacks`, alongside
the existing cache hits/writes, capture-path and error counts. This distinguishes
successful installation from actual processing through the supplier route.

The fix passed 101 managed tests (one existing optional supplier skip), all 35
Python checks, and both normal and captured-Linux reference builds with no
warnings/errors. Tests use the actual reviewed Loading Progress assembly to
reproduce the Windows rejection, admit its known prefixes, reject unrelated
callbacks, compile the patched iterator and confirm that only its texture call
changes, including preservation of branch labels and exception regions. All
four kinds of later foreign patches disable admission. These checks do not
execute Unity or establish rendered texture equality. Evidence is under
`artifacts/png-loading-progress/` and
`artifacts/fixture-tests/d12bd5a50054428bbc374e1d823bbf39/`.

This compatibility fix was installed on both the owner's PC and Deck on
2026-09-08 at 20:40 UTC, with identical DLLs and all package files verified.
Neither game was launched during installation. Live validation must show
actual PNG cache misses/writes and subsequent hits
through Loading Progress, original-output comparison and normal completion.
The separate Gagarin/Loading Progress XML conflict is unchanged.

The owner's local Windows Steam game was separately identified as rev590.
The subsequently implemented [Windows Steam review](windows-steam-support.md)
admits that exact build for all features' checks. The installed package still
predates that source change, and Windows Steam runtime validation remains
necessary before a broader compatibility claim.

The preceding graphics-port check (`py scripts/fixture.py test`) passed 95 managed checks, with the existing
optional Character Editor physical-supplier test skipped, and all 35 Python
checks. The build had zero warnings/errors. New checks cover Linux game-file
fallback, preservation of published paths, same-library Harmony discovery,
unknown-game rejection before Harmony resolution and exact version/module
pairing. Additional graphics checks cover backend selection, Deck defaults,
capture after mip generation and before discarding readable pixels, failure
cleanup, and compressed/uncompressed mip payload and sampler round trips.
These tests ran on the Windows development host. Results:
`artifacts/fixture-tests/e70ebd6ad7e4435bbfcf2bbb5627e9ed/`.

The runtime also compiled successfully on that host against the captured Linux
game and Unity references, using the explicit `linux-rev600` build target and
unchanged reference validation. This compilation does not test Mono execution.
The normal fixture remains on its GOG references.

The installed first preview's reviewed launch is recorded at
`artifacts/steam-deck-diagnosis/20260908-152136-preview-launch/launch-review.md`.
It completed definition/template searches, type searches, Character Editor
lookups and Loading Progress coalescing. Gagarin reuse's interfering-hook refusal
is the same known compatibility restriction as Windows. The process remained
alive at capture, so that review did not establish normal exit or gameplay safety.

The new graphics build is installed. During separately authorized live
testing, check first-build and warm-cache PNG output against the original,
Giddy-Up alpha/offset equality, receipt errors, startup cleanup and normal exit.
Focused gameplay/save-reload checks remain before feature-parity qualification;
matched controls remain necessary before speed claims. No Windows benchmark
percentage applies to this preview.
